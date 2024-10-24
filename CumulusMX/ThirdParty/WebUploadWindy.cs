using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadWindy(Cumulus cumulus, string name) : WebUploadServiceBase(cumulus, name)
	{
		public string ApiKey;
		public int StationIdx;

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
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string apistring;
			string url = GetURL(out apistring, timestamp);
			string logUrl = url.Replace(apistring, "<<API_KEY>>");

			cumulus.LogDebugMessage("Windy: URL = " + logUrl);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(url);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					cumulus.LogDebugMessage("Windy: Response = " + response.StatusCode + ": " + responseBodyAsText);
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "Windy: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
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

							await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
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

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
		}


		// Documentation on the API can be found here...
		// https://community.windy.com/topic/8168/report-your-weather-station-data-to-windy
		//
		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH':'mm':'ss");
			StringBuilder URL = new StringBuilder("https://stations.windy.com/pws/update/", 1024);

			pwstring = ApiKey;

			URL.Append(ApiKey);
			URL.Append("?station=" + StationIdx);
			URL.Append("&dateutc=" + dateUTC);

			StringBuilder Data = new StringBuilder(1024);
			Data.Append("&winddir=" + station.AvgBearing);
			if (station.WindAverage >= 0)
				Data.Append("&wind=" + station.WindMSStr(station.WindAverage));
			if (station.RecentMaxGust >= 0)
				Data.Append("&gust=" + station.WindMSStr(station.RecentMaxGust));
			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				Data.Append("&temp=" + WeatherStation.TempCstr(station.OutdoorTemperature));
			Data.Append("&precip=" + WeatherStation.RainMMstr(station.RainLastHour));
			if (station.Pressure > 0)
				Data.Append("&pressure=" + WeatherStation.PressPAstr(station.Pressure));
			if (station.OutdoorDewpoint > Cumulus.DefaultHiVal)
				Data.Append("&dewpoint=" + WeatherStation.TempCstr(station.OutdoorDewpoint));
			if (station.OutdoorHumidity >= 0)
				Data.Append("&humidity=" + station.OutdoorHumidity);

			if (SendUV && station.UV.HasValue)
				Data.Append("&uv=" + station.UV.Value.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			if (SendSolar && station.SolarRad.HasValue)
				Data.Append("&solarradiation=" + station.SolarRad);

			URL.Append(Data);

			return URL.ToString();
		}
	}
}
