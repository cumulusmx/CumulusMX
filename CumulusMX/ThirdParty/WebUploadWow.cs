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
			if (!Updating)
			{
				Updating = true;

				// Random jitter
				await Task.Delay(Program.RandGenerator.Next(5000, 20000));

				string pwstring;
				string URL = GetURL(out pwstring, timestamp);

				string starredpwstring = "&siteAuthenticationKey=" + new string('*', PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);
				cumulus.LogDebugMessage("WOW URL = " + LogURL);

				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode != HttpStatusCode.OK)
					{
						cumulus.LogWarningMessage($"WOW Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
						cumulus.ThirdPartyAlarm.LastMessage = $"WOW: HTTP response - Response code = {response.StatusCode}, body = {responseBodyAsText}";
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						cumulus.LogDebugMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
						cumulus.ThirdPartyAlarm.Triggered = false;
					}
				}
				catch (Exception ex)
				{
					string msg;

					if (ex.InnerException is TimeoutException)
					{
						msg = $"WOW: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
						cumulus.LogWarningMessage(msg);
					}
					else
					{
						msg = "WOW: " + ex.Message;
						cumulus.LogExceptionMessage(ex, "WOW: Error");
					}

					cumulus.ThirdPartyAlarm.LastMessage = msg;
					cumulus.ThirdPartyAlarm.Triggered = true;
				}
				finally
				{
					Updating = false;
				}
			}
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
