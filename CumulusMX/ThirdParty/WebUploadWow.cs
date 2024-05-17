using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadWow : WebUploadServiceBase
	{

		internal WebUploadWow(Cumulus cumulus, string name) : base(cumulus, name)
		{
		}


		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("WOW: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string pwstring;
			string URL = GetURL(out pwstring, timestamp);

			string starredpwstring = "&siteAuthenticationKey=" + new string('*', PW.Length);

			string LogURL = URL.Replace(pwstring, starredpwstring);
			cumulus.LogDebugMessage("WOW URL = " + LogURL);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0 ; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.LogDebugMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "WOW: Unauthorized, check credentials";
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
							cumulus.LogWarningMessage($"WOW Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							cumulus.ThirdPartyAlarm.LastMessage = $"WOW: HTTP response - Response code = {response.StatusCode}, body = {responseBodyAsText}";
							cumulus.ThirdPartyAlarm.Triggered = true;
							Updating = false;
							return;
						}

						cumulus.LogDebugMessage($"WOW Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
						cumulus.LogMessage($"WOW: Retrying in {delay / retryCount} seconds");

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
							msg = $"WOW: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "WOW: Error - " + ex.Message;
							cumulus.LogExceptionMessage(ex, "WOW: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"WOW: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("WOW: Error - " + ex.Message);
						}

						cumulus.LogMessage($"WOW: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder("http://wow.metoffice.gov.uk/automaticreading?siteid=", 1024);

			pwstring = PW;
			URL.Append(ID);
			URL.Append("&siteAuthenticationKey=" + PW);
			URL.Append("&dateutc=" + dateUTC);

			StringBuilder Data = new StringBuilder(1024);

			// send average speed and bearing
			Data.Append("&winddir=" + station.AvgBearing);
			if (station.WindAverage >= 0)
				Data.Append("&windspeedmph=" + station.WindMPHStr(station.WindAverage));
			if (station.RecentMaxGust >= 0)
				Data.Append("&windgustmph=" + station.WindMPHStr(station.RecentMaxGust));
			if (station.OutdoorHumidity >= 0)
				Data.Append("&humidity=" + station.OutdoorHumidity);
			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				Data.Append("&tempf=" + WeatherStation.TempFstr(station.OutdoorTemperature));
			Data.Append("&rainin=" + WeatherStation.RainINstr(station.RainLastHour));
			Data.Append("&dailyrainin=");
			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data.Append(WeatherStation.RainINstr(station.RainToday));
			}
			else
			{
				Data.Append(WeatherStation.RainINstr(station.RainSinceMidnight));
			}
			if (station.Pressure > 0)
				Data.Append("&baromin=" + WeatherStation.PressINstr(station.Pressure));
			if (station.OutdoorDewpoint > Cumulus.DefaultHiVal)
				Data.Append("&dewptf=" + WeatherStation.TempFstr(station.OutdoorDewpoint));
			if (SendUV && station.UV >= 0)
				Data.Append("&UV=" + station.UV.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture.NumberFormat));
			if (SendSolar && station.SolarRad >= 0)
				Data.Append("&solarradiation=" + station.SolarRad);
			if (SendSoilTemp && station.SoilTemp[SoilTempSensor] > Cumulus.DefaultHiVal)
				Data.Append($"&soiltempf=" + WeatherStation.TempFstr(station.SoilTemp[SoilTempSensor]));

			Data.Append("&softwaretype=Cumulus%20v" + cumulus.Version);
			Data.Append("&action=updateraw");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}
	}
}
