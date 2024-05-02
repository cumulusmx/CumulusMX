using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using ServiceStack.Text;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadWindGuru(Cumulus cumulus, string name) : WebUploadServiceBase(cumulus, name)
	{
		public bool SendRain;

		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 10000));

			string apistring;
			string url = GetURL(out apistring, timestamp);
			string logUrl = url.Replace(apistring, "<<StationUID>>");

			cumulus.LogDebugMessage("WindGuru: URL = " + logUrl);

			try
			{
				using var response = await cumulus.MyHttpClient.GetAsync(url);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				cumulus.LogDebugMessage("WindGuru: " + response.StatusCode + ": " + responseBodyAsText);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					cumulus.LogMessage("WindGuru: ERROR - " + response.StatusCode + ": " + responseBodyAsText);
					cumulus.ThirdPartyAlarm.LastMessage = "WindGuru: HTTP response - " + response.StatusCode;
					cumulus.ThirdPartyAlarm.Triggered = true;
				}
				else
				{
					cumulus.ThirdPartyAlarm.Triggered = false;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "WindGuru: ERROR");
				cumulus.ThirdPartyAlarm.LastMessage = "WindGuru: " + ex.Message;
				cumulus.ThirdPartyAlarm.Triggered = true;
			}
			finally
			{
				Updating = false;
			}
		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			var InvC = new CultureInfo("");

			string salt = timestamp.ToUnixTime().ToString();
			string hash = Utils.GetMd5String(salt + cumulus.WindGuru.ID + cumulus.WindGuru.PW);

			pwstring = cumulus.WindGuru.ID;

			int numvalues = 0;
			double totalwind = 0;
			double maxwind = 0;
			double minwind = 999;
			for (int i = 0; i < WeatherStation.MaxWindRecent; i++)
			{
				if (station.WindRecent[i].Timestamp >= DateTime.Now.AddMinutes(-cumulus.WindGuru.Interval))
				{
					numvalues++;
					totalwind += station.WindRecent[i].Gust;

					if (station.WindRecent[i].Gust > maxwind)
					{
						maxwind = station.WindRecent[i].Gust;
					}

					if (station.WindRecent[i].Gust < minwind)
					{
						minwind = station.WindRecent[i].Gust;
					}
				}
			}
			// average the values
			double avgwind = totalwind / numvalues;

			StringBuilder URL = new StringBuilder("http://www.windguru.cz/upload/api.php?", 1024);

			URL.Append("uid=" + HttpUtility.UrlEncode(cumulus.WindGuru.ID));
			URL.Append("&salt=" + salt);
			URL.Append("&hash=" + hash);
			URL.Append("&interval=" + cumulus.WindGuru.Interval * 60);
			URL.Append("&wind_avg=" + ConvertUnits.UserWindToKnots(avgwind).ToString("F1", InvC));
			URL.Append("&wind_max=" + ConvertUnits.UserWindToKnots(maxwind).ToString("F1", InvC));
			URL.Append("&wind_min=" + ConvertUnits.UserWindToKnots(minwind).ToString("F1", InvC));
			URL.Append("&wind_direction=" + station.AvgBearing);
			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				URL.Append("&temperature=" + ConvertUnits.UserTempToC(station.OutdoorTemperature).ToString("F1", InvC));
			if (station.OutdoorHumidity >= 0)
				URL.Append("&rh=" + station.OutdoorHumidity);
			if (station.Pressure > 0)
				URL.Append("&mslp=" + ConvertUnits.UserPressToHpa(station.Pressure).ToString("F1", InvC));
			if (cumulus.WindGuru.SendRain)
			{
				URL.Append("&precip=" + ConvertUnits.UserRainToMM(station.RainLastHour).ToString("F1", InvC));
				URL.Append("&precip_interval=3600");
			}

			return URL.ToString();
		}
	}
}
