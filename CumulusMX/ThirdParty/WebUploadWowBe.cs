using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadWowBe : WebUploadServiceBase
	{
		// See: https://wow.meteo.be/docs/api/#/

		private static string url = "https://wow.meteo.be/api/v2/send/wow";

		internal WebUploadWowBe(Cumulus cumulus, string name) : base(cumulus, name)
		{
		}

		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("WOW-BE: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			cumulus.LogDebugMessage("WOW-BE URL = " + url);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					var body = GetBody(timestamp);
					var data = new StringContent(body, Encoding.UTF8, "application/json");

					cumulus.LogDataMessage("WOW-BE: Posting data - " + body.Replace(PW, "******"));

					using var response = await cumulus.MyHttpClient.PostAsync(url, data);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.LogDebugMessage("WOW-BE Response: " + response.StatusCode + ": " + responseBodyAsText);
						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "WOW-BE: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else
					{
						// we get a too many requests response on the first retry if the inital atttempt worked but we did not see a response
						if (retryCount == 1 && response.StatusCode == HttpStatusCode.TooManyRequests)
						{
							Updating = false;
							return;
						}

						if (retryCount == 0 || response.StatusCode == HttpStatusCode.TooManyRequests)
						{
							cumulus.LogWarningMessage($"WOW-BE Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							cumulus.ThirdPartyAlarm.LastMessage = $"WOW-BE: HTTP response - Response code = {response.StatusCode}, body = {responseBodyAsText}";
							cumulus.ThirdPartyAlarm.Triggered = true;
							Updating = false;
							return;
						}

						cumulus.LogDebugMessage($"WOW-BE Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
						cumulus.LogMessage($"WOW-BE: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
				catch (Exception ex)
				{
					string msg;

					if (retryCount == 0)
					{
						if (ex.InnerException is TimeoutException)
						{
							msg = $"WOW-BE: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "WOW-BE: Error - " + ex.Message;
							cumulus.LogExceptionMessage(ex, "WOW-BE: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"WOW-BE: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("WOW-BE: Error - " + ex.Message);
						}

						cumulus.LogMessage($"WOW-BE: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
		}

		internal string GetBody(DateTime timestamp)
		{

			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss");

			var bodyObj = new JsonObject();

			bodyObj.Add("dateutc", dateUTC);
			bodyObj.Add("siteAuthenticationKey", PW);
			bodyObj.Add("siteid", ID);
			bodyObj.Add("softwaretype", "Cumulus v" + cumulus.Version);
			bodyObj.Add("model", cumulus.StationModel);

			if (station.Pressure > 0)
				bodyObj.Add("baromin", ConvertUnits.UserPressToIN(station.Pressure));

			bodyObj.Add("dailyrainin", ConvertUnits.UserRainToIN(cumulus.RolloverHour == 0 ? station.RainToday : station.RainSinceMidnight));
			bodyObj.Add("rainin", ConvertUnits.UserRainToIN(station.RainLastHour));

			if (station.OutdoorDewpoint > Cumulus.DefaultHiVal)
				bodyObj.Add("dewptf", ConvertUnits.UserTempToF(station.OutdoorDewpoint));

			if (station.OutdoorHumidity >= 0)
				bodyObj.Add("humidity", station.OutdoorHumidity);

			if (SendSoilMoisture && station.SoilMoisture[SoilMoistureSensor].HasValue && cumulus.Units.SoilMoistureUnitText[SoilMoistureSensor] == "%")
				bodyObj.Add("soilmoisture", station.SoilMoisture[SoilMoistureSensor].Value);

			if (SendSoilTemp && station.SoilTemp[SoilTempSensor].HasValue)
				bodyObj.Add("soiltempf", ConvertUnits.UserTempToF(station.SoilTemp[SoilTempSensor].Value));

			if (SendSolar && station.SolarRad.HasValue)
				bodyObj.Add("solarradiation", station.SolarRad);

			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				bodyObj.Add("tempf", ConvertUnits.UserTempToF(station.OutdoorTemperature));

			// send average speed and bearing
			bodyObj.Add("winddir", station.AvgBearing);

			if (station.WindAverage >= 0)
				bodyObj.Add("windspeedmph", ConvertUnits.UserWindToMPH(station.WindAverage));

			if (station.RecentMaxGust >= 0)
				bodyObj.Add("windgustmph", ConvertUnits.UserWindToMPH(station.RecentMaxGust));

			//if (SendUV && station.UV.HasValue)
			//	Data.Append("&UV=" + station.UV.Value.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture.NumberFormat));

			return bodyObj.ToJsonString();
		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = null;
			return null;
		}
	}
}
