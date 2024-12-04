using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadPws : WebUploadServiceBase
	{

		internal WebUploadPws(Cumulus cumulus, string name) : base(cumulus, name)
		{ }

		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("PWS: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string pwstring;
			string URL = GetURL(out pwstring, timestamp);

			string starredpwstring = "&PASSWORD=" + new string('*', PW.Length);

			string LogURL = URL.Replace(pwstring, starredpwstring);
			cumulus.LogDebugMessage(LogURL);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.LogDebugMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "PWS: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else
					{
						if (retryCount == 0)
						{
							cumulus.LogWarningMessage($"PWS Response: ERROR - Response code = {response.StatusCode},  Body = {responseBodyAsText}");
							cumulus.ThirdPartyAlarm.LastMessage = $"PWS: HTTP Response code = {response.StatusCode},  Body = {responseBodyAsText}";
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else
						{
							cumulus.LogDebugMessage($"PWS Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							cumulus.LogMessage($"PWS: Retrying in {delay / retryCount} seconds");

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
							msg = $"PWS: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "PWS: " + ex.Message;
							cumulus.LogExceptionMessage(ex, "PWS update error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"PWS: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("PWS: Error - " + ex.Message);
						}

						cumulus.LogMessage($"PWS: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder("http://www.pwsweather.com/pwsupdate/pwsupdate.php?ID=", 1024);

			pwstring = PW;
			URL.Append(ID + "&PASSWORD=" + HttpUtility.UrlEncode(PW));
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
			if (SendUV && station.UV.HasValue)
			{
				Data.Append("&UV=" + station.UV.Value.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			}

			if (SendSolar && station.SolarRad.HasValue)
			{
				Data.Append("&solarradiation=" + station.SolarRad);
			}

			Data.Append("&softwaretype=Cumulus%20v" + cumulus.Version);
			Data.Append("&action=updateraw");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}
	}
}
