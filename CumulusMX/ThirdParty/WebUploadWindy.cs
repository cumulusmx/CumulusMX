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
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string apistring;
			string url = GetURL(out apistring, timestamp);
			string logUrl = url.Replace(apistring, "<<API_KEY>>");

			cumulus.LogDebugMessage("Windy: URL = " + logUrl);

			try
			{
				using var response = await cumulus.MyHttpClient.GetAsync(url);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				cumulus.LogDebugMessage("Windy: Response = " + response.StatusCode + ": " + responseBodyAsText);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					cumulus.LogMessage("Windy: ERROR - Response = " + response.StatusCode + ": " + responseBodyAsText);
					cumulus.ThirdPartyAlarm.LastMessage = "Windy: HTTP response - " + response.StatusCode;
					cumulus.ThirdPartyAlarm.Triggered = true;
				}
				else
				{
					cumulus.ThirdPartyAlarm.Triggered = false;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Windy: ERROR");
				cumulus.ThirdPartyAlarm.LastMessage = "Windy: " + ex.Message;
				cumulus.ThirdPartyAlarm.Triggered = true;
			}
			finally
			{
				Updating = false;
			}
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

			if (SendUV && station.UV >= 0)
				Data.Append("&uv=" + station.UV.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			if (SendSolar && station.SolarRad >= 0)
				Data.Append("&solarradiation=" + station.SolarRad.ToString("F0"));

			URL.Append(Data);

			return URL.ToString();
		}
	}
}
