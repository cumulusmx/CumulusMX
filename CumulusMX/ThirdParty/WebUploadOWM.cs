﻿using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using ServiceStack.Text;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadOwm : WebUploadServiceBase
	{

		internal WebUploadOwm(Cumulus cumulus, string name) : base(cumulus, name)
		{ }


		/// <summary>
		/// Process the list of OpenWeatherMap updates created at start-up from logger entries
		/// Documentation: https://openweathermap.org/stations#measurement
		/// </summary>
		internal override async Task DoCatchUp()
		{
			Updating = true;

			string url = "https://api.openweathermap.org/data/3.0/measurements?appid=" + PW;
			string logUrl = url.Replace(PW, "<key>");

			for (int i = 0; i < CatchupList.Count; i++)
			{
				cumulus.LogMessage("Uploading OpenWeatherMap archive #" + (i + 1));
				cumulus.LogDebugMessage("OpenWeatherMap: URL = " + logUrl);
				cumulus.LogDataMessage("OpenWeatherMap: Body = " + CatchupList[i]);

				try
				{
					var data = new StringContent(CatchupList[i], Encoding.UTF8, "application/json");
					using var response = await cumulus.MyHttpClient.PostAsync(url, data);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					var status = response.StatusCode == HttpStatusCode.NoContent ? "OK" : "Error";  // Returns a 204 response for OK!
					cumulus.LogDebugMessage($"OpenWeatherMap: Response code = {status} - {response.StatusCode}");

					if (response.StatusCode != HttpStatusCode.NoContent)
						cumulus.LogDataMessage($"OpenWeatherMap: Response data = {responseBodyAsText}");
				}
				catch (Exception ex)
				{
					string msg;

					if (ex.InnerException is TimeoutException)
					{
						msg = $"OpenWeatherMap: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
						cumulus.LogWarningMessage(msg);
					}
					else
					{
						msg = "OpenWeatherMap: " + ex.Message;
						cumulus.LogExceptionMessage(ex, "OpenWeatherMap: Update error");
					}

					cumulus.ThirdPartyAlarm.LastMessage = msg;
					cumulus.ThirdPartyAlarm.Triggered = true;
				}
			}

			cumulus.LogMessage("End of OpenWeatherMap archive upload");
			CatchupList.Clear();
			CatchingUp = false;
			Updating = false;
		}


		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("OpenWeatherMap: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string url = "https://api.openweathermap.org/data/3.0/measurements?appid=" + PW;
			string logUrl = url.Replace(PW, "<key>");

			string jsonData = GetURL(out _, timestamp);

			cumulus.LogDebugMessage("OpenWeatherMap: URL = " + logUrl);
			cumulus.LogDataMessage("OpenWeatherMap: Body = " + jsonData);

			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{

				try
				{
					var data = new StringContent(jsonData, Encoding.UTF8, "application/json");
					HttpResponseMessage response = await cumulus.MyHttpClient.PostAsync(url, data);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					var status = response.StatusCode == HttpStatusCode.NoContent ? "OK" : "Error";  // Returns a 204 response for OK!
					cumulus.LogDebugMessage($"OpenWeatherMap: Response code = {status} - {response.StatusCode}");
					if (response.StatusCode == HttpStatusCode.NoContent)
					{
						cumulus.ThirdPartyAlarm.Triggered = false;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "OpenWeatherMap: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else
					{
						cumulus.LogMessage($"OpenWeatherMap: ERROR - Response code = {response.StatusCode}, Response data = {responseBodyAsText}");
						cumulus.ThirdPartyAlarm.LastMessage = $"OpenWeatherMap: HTTP response - {response.StatusCode}, Response data = {responseBodyAsText}";
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					Updating = false;
					return;
				}
				catch (Exception ex)
				{
					string msg;

					if (retryCount > 0)
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"OpenWeatherMap: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("OpenWeatherMap: Error - " + ex.Message);
						}

						cumulus.LogMessage($"OpenWeatherMap: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
					else
					{

						if (ex.InnerException is TimeoutException)
						{
							msg = $"OpenWeatherMap: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "OpenWeatherMap: " + ex.Message;
							cumulus.LogExceptionMessage(ex, "OpenWeatherMap: Update error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
				}
			}

			Updating = false;
		}

		// GetURL actually returns the request body for OpenWeatherMap
		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = null;

			StringBuilder sb = new StringBuilder($"[{{\"station_id\":\"{ID}\",");
			var invC = new CultureInfo("");

			sb.Append($"\"dt\":{timestamp.ToUnixTime()},");
			if (station.OutdoorTemperature >= Cumulus.DefaultHiVal)
				sb.Append($"\"temperature\":{Math.Round(ConvertUnits.UserTempToC(station.OutdoorTemperature), 1).ToString(invC)},");
			sb.Append($"\"wind_deg\":{station.AvgBearing},");
			sb.Append($"\"wind_speed\":{Math.Round(ConvertUnits.UserWindToMS(station.WindAverage), 1).ToString(invC)},");
			if (station.RecentMaxGust >= 0)
				sb.Append($"\"wind_gust\":{Math.Round(ConvertUnits.UserWindToMS(station.RecentMaxGust), 1).ToString(invC)},");
			if (station.Pressure > 0)
				sb.Append($"\"pressure\":{Math.Round(ConvertUnits.UserPressToHpa(station.Pressure), 1).ToString(invC)},");
			if (station.OutdoorHumidity >= 0)
				sb.Append($"\"humidity\":{station.OutdoorHumidity},");
			sb.Append($"\"rain_1h\":{Math.Round(ConvertUnits.UserRainToMM(station.RainLastHour), 1).ToString(invC)},");
			sb.Append($"\"rain_24h\":{Math.Round(ConvertUnits.UserRainToMM(station.RainLast24Hour), 1).ToString(invC)}");
			sb.Append("}]");

			return sb.ToString();
		}


		// Find all stations associated with the users API key
		private OpenWeatherMapStation[] GetOpenWeatherMapStations()
		{
			OpenWeatherMapStation[] retVal = [];
			string url = "https://api.openweathermap.org/data/3.0/stations?appid=" + PW;
			try
			{
				using var client = new HttpClient();
				HttpResponseMessage response = client.GetAsync(url).Result;
				var responseBodyAsText = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDataMessage("OpenWeatherMap: Get Stations Response: " + response.StatusCode + ": " + responseBodyAsText);

				if (responseBodyAsText.Length > 10)
				{
					var respJson = JsonSerializer.DeserializeFromString<OpenWeatherMapStation[]>(responseBodyAsText);
					retVal = respJson;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "OpenWeatherMap: Get Stations ERROR");
			}

			return retVal;
		}

		// Create a new OpenWeatherMap station
		private void CreateOpenWeatherMapStation()
		{
			var invC = new CultureInfo("");

			string url = "https://api.openweathermap.org/data/3.0/stations?appid=" + PW;
			try
			{
				var datestr = DateTime.Now.ToUniversalTime().ToString("yyMMddHHmm");
				StringBuilder sb = new StringBuilder($"{{\"external_id\":\"CMX-{datestr}\",");
				sb.Append($"\"name\":\"{cumulus.LocationName}\",");
				sb.Append($"\"latitude\":{cumulus.Latitude.ToString(invC)},");
				sb.Append($"\"longitude\":{cumulus.Longitude.ToString(invC)},");
				sb.Append($"\"altitude\":{(int) ConvertUnits.AltitudeM(cumulus.Altitude)}}}");

				cumulus.LogMessage($"OpenWeatherMap: Creating new station");
				cumulus.LogMessage($"OpenWeatherMap: - {sb}");


				var data = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");

				HttpResponseMessage response = cumulus.MyHttpClient.PostAsync(url, data).Result;
				var responseBodyAsText = response.Content.ReadAsStringAsync().Result;
				var status = response.StatusCode == HttpStatusCode.Created ? "OK" : "Error";  // Returns a 201 response for OK
				cumulus.LogDebugMessage($"OpenWeatherMap: Create station response code = {status} - {response.StatusCode}");
				cumulus.LogDataMessage($"OpenWeatherMap: Create station response data = {responseBodyAsText}");

				if (response.StatusCode == HttpStatusCode.Created)
				{
					// It worked, save the result
					var respJson = JsonSerializer.DeserializeFromString<OpenWeatherMapNewStation>(responseBodyAsText);

					cumulus.LogMessage($"OpenWeatherMap: Created new station, id = {respJson.ID}, name = {respJson.name}");
					ID = respJson.ID;
					cumulus.WriteIniFile();
				}
				else
				{
					cumulus.LogMessage($"OpenWeatherMap: Failed to create new station. Error - {response.StatusCode}, text - {responseBodyAsText}");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "OpenWeatherMap: Create station ERROR");
			}
		}

		internal void EnableOpenWeatherMap()
		{
			if (Enabled && string.IsNullOrWhiteSpace(ID))
			{
				// oh, oh! OpenWeatherMap is enabled, but we do not have a station id
				// first check if one already exists
				var stations = GetOpenWeatherMapStations();

				if (stations.Length == 0)
				{
					// No stations defined, we will create one
					cumulus.LogMessage($"OpenWeatherMap: No station defined, attempting to create one");
					CreateOpenWeatherMapStation();
				}
				else if (stations.Length == 1)
				{
					// We have one station defined, lets use it!
					cumulus.LogMessage($"OpenWeatherMap: No station defined, but found one associated with this API key, using this station - {stations[0].id} : {stations[0].name}");
					ID = stations[0].id;
					// save the setting
					cumulus.WriteIniFile();
				}
				else
				{
					// multiple stations defined, the user must select which one to use
					var msg = $"Multiple OpenWeatherMap stations found, please select the correct station id and enter it into your configuration";
					Cumulus.LogConsoleMessage(msg);
					cumulus.LogMessage("OpenWeatherMap: " + msg);
					foreach (var station in stations)
					{
						msg = $"  Station Id = {station.id}, Name = {station.name}";
						Cumulus.LogConsoleMessage(msg);
						cumulus.LogMessage("OpenWeatherMap: " + msg);
					}
				}
			}
		}

		private sealed class OpenWeatherMapStation
		{
			public string id { get; set; }
			public string created_at { get; set; }
			public string updated_at { get; set; }
			public string external_id { get; set; }
			public string name { get; set; }
			public double longitude { get; set; }
			public double latitude { get; set; }
			public int altitude { get; set; }
			public int rank { get; set; }
		}

		private sealed class OpenWeatherMapNewStation
		{
			public string ID { get; set; }
			public string created_at { get; set; }
			public string updated_at { get; set; }
			public string user_id { get; set; }
			public string external_id { get; set; }
			public string name { get; set; }
			public double longitude { get; set; }
			public double latitude { get; set; }
			public int altitude { get; set; }
			public int source_type { get; set; }
		}
	}
}
