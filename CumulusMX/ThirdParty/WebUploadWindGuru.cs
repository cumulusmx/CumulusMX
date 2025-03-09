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
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("WindGuru: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 10000));

			string apistring;
			string url = GetURL(out apistring, timestamp);
			string logUrl = url.Replace(apistring, "<<StationUID>>");

			cumulus.LogDebugMessage("WindGuru: URL = " + logUrl);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(url);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					cumulus.LogDebugMessage("WindGuru: " + response.StatusCode + ": " + responseBodyAsText);
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.ThirdPartyAlarm.Triggered = false;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						cumulus.ThirdPartyAlarm.LastMessage = "WindGuru: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else
					{

						if (retryCount == 0)
						{
							cumulus.LogWarningMessage($"WindGuru Response: ERROR - Response code = {response.StatusCode},  Body = {responseBodyAsText}");
							cumulus.ThirdPartyAlarm.LastMessage = $"WindGuru: HTTP Response code = {response.StatusCode},  Body = {responseBodyAsText}";
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else
						{
							cumulus.LogDebugMessage($"WindGuru Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							cumulus.LogMessage($"WindGuru: Retrying in {delay / retryCount} seconds");

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
							msg = $"WindGuru: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "WindGuru: Error - " + ex.Message;
							cumulus.LogExceptionMessage(ex, "WindGuru: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"WindGuru: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("WindGuru: Error - " + ex.Message);
						}

						cumulus.LogMessage($"WindGuru: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
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
			lock (station.recentwindLock)
			{
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
