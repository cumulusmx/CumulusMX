using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadWindy(Cumulus cumulus, string name) : WebUploadServiceBase(cumulus, name)
	{
		public string ApiKey { get; set; }
		public string StationId { get; set; }

		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("Windy: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000), Program.ExitSystemToken);

			string apistring;
			string url = GetURL(out apistring, timestamp);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					cumulus.LogDebugMessage("Windy: URL = " + url);

					using var request = new HttpRequestMessage(HttpMethod.Get, url);
					if (!string.IsNullOrEmpty(PW))
					{
						request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", PW);
					}
					else if (!string.IsNullOrEmpty(ApiKey))
					{
						request.Headers.Add("windy-api-key", ApiKey);
					}
					else
					{
						cumulus.LogErrorMessage("Windy: Missing Password or API key in configuration");
						Updating = false;
						return;
					}

					using var response = await cumulus.MyHttpClient.SendAsync(request, Program.ExitSystemToken);
					var responseBodyAsText = await response.Content.ReadAsStringAsync(Program.ExitSystemToken);
					cumulus.LogDebugMessage("Windy: Response = " + response.StatusCode + ": " + responseBodyAsText);
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.BadRequest)
					{
						var resp = JsonSerializer.Deserialize<ErrorResponse>(responseBodyAsText);
						string msg = string.Empty;
						if (resp != null)
						{
							msg = resp.message[0];
						}
						cumulus.LogWarningMessage($"Windy: Station password is invalid, does not match the station, or payload failed validation - '{msg}'");
						cumulus.ThirdPartyAlarm.LastMessage = "Windy: Station password is invalid, does not match the station, or payload failed validation";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.LogWarningMessage("Windy: Missing station password in query or Authorization header");
						cumulus.ThirdPartyAlarm.LastMessage = "Windy: Missing station password in query or Authorization header";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Conflict)
					{
						cumulus.LogWarningMessage("Windy: Duplicate request detected (the same payload was sent multiple times)");
						cumulus.ThirdPartyAlarm.LastMessage = "Windy: Duplicate request detected (the same payload was sent multiple times)";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.TooManyRequests)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "Windy: Rate limit exceeded";
						cumulus.ThirdPartyAlarm.Triggered = true;

						var json = JsonSerializer.Deserialize<RateLimited>(responseBodyAsText);
						if (json != null)
						{
							var wait = json.retry_after - DateTime.UtcNow;
							var waitSecs = Convert.ToInt32(wait.TotalSeconds) + 10;
							cumulus.LogWarningMessage($"Windy: Rate limit exceeded, retrying in {waitSecs} seconds");

							await Task.Delay(waitSecs * 1000, Program.ExitSystemToken);
						}
						else
						{
							cumulus.LogWarningMessage("Windy: Rate limit exceeded and no 'try after' time sent by Windy");

							Updating = false;
							return;
						}
					}
					else
					{
						if (retryCount == 0)
						{
							cumulus.LogWarningMessage("Windy: ERROR - Response = " + response.StatusCode + ": " + responseBodyAsText);
							cumulus.ThirdPartyAlarm.LastMessage = "Windy: HTTP response - " + response.StatusCode;
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else
						{
							cumulus.LogDebugMessage($"Windy Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							cumulus.LogMessage($"Windy: Retrying in {delay / retryCount} seconds");

							await Task.Delay(TimeSpan.FromSeconds(delay / retryCount), Program.ExitSystemToken);
						}
					}
				}
				catch (Exception ex)
				{
					string msg;

					if (retryCount == 0)
					{
						if (ex.InnerException is TimeoutException)
						{
							msg = $"Windy: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "Windy: " + ex.Message;
							cumulus.LogExceptionMessage(ex, "Windy: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"Windy: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("Windy: Error - " + ex.Message);
						}

						cumulus.LogMessage($"Windy: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount), Program.ExitSystemToken);
					}
				}
			}

			Updating = false;
		}


		// Documentation on the API can be found here...
		// https://stations.windy.com/api-reference
		//
		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = null;

			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
			StringBuilder URL = new StringBuilder("https://stations.windy.com/api/v2/observation/update/", 1024);

			URL.Append("?id=" + StationId);
			URL.Append("&time=" + dateUTC);

			if (station.WindAverage >= 0)
				URL.Append("&wind=" + station.WindMSStr(station.WindAverage));

			if (station.RecentMaxGust >= 0)
				URL.Append("&gust=" + station.WindMSStr(station.RecentMaxGust));

			URL.Append("&winddir=" + station.AvgBearing % 360); // Windy v2 API only accepts 0-359 :(

			if (station.OutdoorHumidity >= 0)
				URL.Append("&rh=" + station.OutdoorHumidity);

			if (station.OutdoorDewpoint > Cumulus.DefaultHiVal)
				URL.Append("&dewpoint=" + WeatherStation.TempCstr(station.OutdoorDewpoint));

			if (station.Pressure > 0)
				URL.Append("&pressure=" + WeatherStation.PressPAstr(station.Pressure));

			if (SendUV && station.UV.HasValue)
				URL.Append("&uv=" + station.UV.Value.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));

			if (SendSolar && station.SolarRad.HasValue)
				URL.Append("&solarradiation=" + station.SolarRad);

			URL.Append("&precip=" + WeatherStation.RainMMstr(station.RainLastHour));

			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				URL.Append("&temp=" + WeatherStation.TempCstr(station.OutdoorTemperature));

			URL.Append("&softwaretype=CumulusMX+v" + cumulus.Version);
			URL.Append("&stationtype=" + System.Web.HttpUtility.UrlEncode(cumulus.StationModel));

			return URL.ToString();
		}

		private class RateLimited
		{
			public DateTime retry_after {  get; set; }
		}

		private class ErrorResponse
		{
			public string[] message { get; set; }
			public string error { get; set; }
			public int statusCode { get; set; }
		}
	}
}
