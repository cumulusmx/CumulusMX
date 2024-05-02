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
			if (!Updating)
			{
				Updating = true;

				// Random jitter
				await Task.Delay(Program.RandGenerator.Next(5000, 20000));

				string pwstring;
				string URL = GetURL(out pwstring, timestamp);

				string starredpwstring = "&PASSWORD=" + new string('*', PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);
				cumulus.LogDebugMessage(LogURL);

				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode != HttpStatusCode.OK)
					{
						cumulus.LogWarningMessage($"PWS Response: ERROR - Response code = {response.StatusCode},  Body = {responseBodyAsText}");
						cumulus.ThirdPartyAlarm.LastMessage = $"PWS: HTTP Response code = {response.StatusCode},  Body = {responseBodyAsText}";
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						cumulus.LogDebugMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
						cumulus.ThirdPartyAlarm.Triggered = false;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "PWS update error");
					cumulus.ThirdPartyAlarm.LastMessage = "PWS: " + ex.Message;
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
			if (SendUV && station.UV >= 0)
			{
				Data.Append("&UV=" + station.UV.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			}

			if (SendSolar && station.SolarRad >= 0)
			{
				Data.Append("&solarradiation=" + station.SolarRad.ToString("F0"));
			}

			Data.Append("&softwaretype=Cumulus%20v" + cumulus.Version);
			Data.Append("&action=updateraw");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}
	}
}
