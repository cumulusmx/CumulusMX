using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;


namespace CumulusMX.ThirdParty
{
	internal class WebUploadWund : WebUploadServiceBase
	{
		public bool RapidFireEnabled;
		public bool SendAverage;
		public bool SendSoilTemp1;
		public bool SendSoilTemp2;
		public bool SendSoilTemp3;
		public bool SendSoilTemp4;
		public bool SendSoilMoisture1;
		public bool SendSoilMoisture2;
		public bool SendSoilMoisture3;
		public bool SendSoilMoisture4;
		public bool SendLeafWetness1;
		public bool SendLeafWetness2;
		public int SendExtraTemp1;
		public int SendExtraTemp2;
		public int SendExtraTemp3;
		public int SendExtraTemp4;
		public int ErrorFlagCount;

		public WebUploadWund(Cumulus cumulus, string name) : base(cumulus, name)
		{
			IntTimer.Elapsed += TimerTick;
		}


		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage($"Wunderground: {(RapidFireEnabled ? "RapidFire " : string.Empty)}Not uploading, {reason}");
				return;
			}

			Updating = true;

			// Random jitter
			if (!RapidFireEnabled)
			{
				await Task.Delay(Program.RandGenerator.Next(5000, 20000));
			}

			string pwstring;
			string URL = GetURL(out pwstring, timestamp);

			string starredpwstring = "&PASSWORD=" + new string('*', PW.Length);

			string logUrl = URL.Replace(pwstring, starredpwstring);
			if (RapidFireEnabled)
			{
				cumulus.LogDebugMessage("Wunderground: Rapid fire");
			}
			else
			{
				cumulus.LogDebugMessage("Wunderground: URL = " + logUrl);
			}

			// we will try this twice in case the first attempt fails
			// but no retries for rapid fire
			var maxRetryAttempts = RapidFireEnabled ? 1 : 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.LogDebugMessage("Wunderground: Successful upload");
						cumulus.ThirdPartyAlarm.Triggered = false;
						ErrorFlagCount = 0;
						Updating = false;
						return;
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						// Flag the error immediately if no rapid fire
						// Flag error after every 12 rapid fire failures (1 minute)
						ErrorFlagCount++;
						if (!RapidFireEnabled || (RapidFireEnabled && ErrorFlagCount >= 12))
						{
							cumulus.LogWarningMessage("Wunderground: Unauthorized, check credentials");
						}
						cumulus.ThirdPartyAlarm.LastMessage = "Wunderground: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return;
					}
					else
					{
						// Flag the error immediately if no rapid fire
						// Flag error after every 12 rapid fire failures (1 minute)
						ErrorFlagCount++;
						if ((!RapidFireEnabled && retryCount == 0) || ErrorFlagCount >= 12)
						{
							cumulus.LogWarningMessage("Wunderground: Response = " + response.StatusCode + ": " + responseBodyAsText);
							cumulus.ThirdPartyAlarm.LastMessage = "Wunderground: HTTP response - " + response.StatusCode;
							cumulus.ThirdPartyAlarm.Triggered = true;
							ErrorFlagCount = 0;
						}
						else
						{
							cumulus.LogDebugMessage($"Wunderground Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							cumulus.LogMessage($"Wunderground: Retrying in {delay / retryCount} seconds");

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
							msg = $"Wunderground: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "Wunderground: " + ex.Message;
							cumulus.LogExceptionMessage(ex, "Wunderground: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"Wunderground: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("Wunderground: Error - " + ex.Message);
						}

						cumulus.LogMessage($"Wunderground: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
		}


		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			// API documentation: https://support.weather.com/s/article/PWS-Upload-Protocol?language=en_US

			var invC = new CultureInfo("");

			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder(1024);
			if (RapidFireEnabled && !CatchingUp)
			{
				URL.Append("http://rtupdate.wunderground.com/weatherstation/updateweatherstation.php?ID=");
			}
			else
			{
				URL.Append("http://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?ID=");
			}

			pwstring = PW;
			URL.Append(ID);
			URL.Append($"&PASSWORD={PW}");
			URL.Append($"&dateutc={dateUTC}");
			StringBuilder Data = new StringBuilder(1024);
			if (SendAverage && station.WindAverage >= 0)
			{
				// send average speed and bearing
				Data.Append($"&winddir={station.AvgBearing}&windspeedmph={station.WindMPHStr(station.WindAverage)}");
			}
			else if (station.WindLatest >= 0)
			{
				// send "instantaneous" speed (i.e. latest) and bearing
				Data.Append($"&winddir={station.Bearing}&windspeedmph={station.WindMPHStr(station.WindLatest)}");
			}
			if (station.RecentMaxGust >= 0)
				Data.Append($"&windgustmph={station.WindMPHStr(station.RecentMaxGust)}");
			// may not strictly be a 2 min average!
			if (station.WindAverage >= 0)
			{
				Data.Append($"&windspdmph_avg2m={station.WindMPHStr(station.WindAverage)}");
				Data.Append($"&winddir_avg2m={station.AvgBearing}");
			}
			if (station.OutdoorHumidity >= 0)
				Data.Append($"&humidity={station.OutdoorHumidity}");
			if (station.OutdoorTemperature >= Cumulus.DefaultHiVal)
				Data.Append($"&tempf={WeatherStation.TempFstr(station.OutdoorTemperature)}");
			Data.Append($"&rainin={WeatherStation.RainINstr(station.RainLastHour)}");
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
			if (station.Pressure >= Cumulus.DefaultHiVal)
				Data.Append($"&baromin={WeatherStation.PressINstr(station.Pressure)}");
			if (station.OutdoorDewpoint >= Cumulus.DefaultHiVal)
				Data.Append($"&dewptf={WeatherStation.TempFstr(station.OutdoorDewpoint)}");
			if (SendUV && station.UV.HasValue)
				Data.Append($"&UV={station.UV.Value.ToString(cumulus.UVFormat, invC)}");
			if (SendSolar && station.SolarRad.HasValue)
				Data.Append($"&solarradiation={station.SolarRad}");
			if (SendIndoor)
			{
				if (station.IndoorTemperature.HasValue)
					Data.Append($"&indoortempf={WeatherStation.TempFstr(station.IndoorTemperature.Value)}");
				if (station.IndoorHumidity.HasValue)
					Data.Append($"&indoorhumidity={station.IndoorHumidity}");
			}
			// Davis soil and leaf sensors
			if (SendSoilTemp1 && station.SoilTemp[1].HasValue)
				Data.Append($"&soiltempf={WeatherStation.TempFstr(station.SoilTemp[1].Value)}");
			if (SendSoilTemp2 && station.SoilTemp[2].HasValue)
				Data.Append($"&soiltempf2={WeatherStation.TempFstr(station.SoilTemp[2].Value)}");
			if (SendSoilTemp3 && station.SoilTemp[3].HasValue)
				Data.Append($"&soiltempf3={WeatherStation.TempFstr(station.SoilTemp[3].Value)}");
			if (SendSoilTemp4 && station.SoilTemp[4].HasValue)
				Data.Append($"&soiltempf4={WeatherStation.TempFstr(station.SoilTemp[4].Value)}");

			if (SendSoilMoisture1 && station.SoilMoisture[1].HasValue)
				Data.Append($"&soilmoisture={station.SoilMoisture[1]}");
			if (SendSoilMoisture2 && station.SoilMoisture[2].HasValue)
				Data.Append($"&soilmoisture2={station.SoilMoisture[2]}");
			if (SendSoilMoisture3 && station.SoilMoisture[3].HasValue)
				Data.Append($"&soilmoisture3={station.SoilMoisture[3]}");
			if (SendSoilMoisture4 && station.SoilMoisture[4].HasValue)
				Data.Append($"&soilmoisture4={station.SoilMoisture[4]}");

			if (SendLeafWetness1 && station.LeafWetness[1].HasValue)
				Data.Append($"&leafwetness={station.LeafWetness[1]:cumulus.LeafWetFormat}");
			if (SendLeafWetness2 && station.LeafWetness[2].HasValue)
				Data.Append($"&leafwetness2={station.LeafWetness[2]:cumulus.LeafWetFormat}");

			if (SendAirQuality && cumulus.StationOptions.PrimaryAqSensor > (int) Cumulus.PrimaryAqSensor.Undefined)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							Data.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5:F1}&AqPM10={cumulus.airLinkDataOut.pm10.ToString("F1", invC)}");
						}
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
						if (station.AirQuality1.HasValue)
							Data.Append($"&AqPM2.5={station.AirQuality1.Value.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						if (station.AirQuality2.HasValue)
							Data.Append($"&AqPM2.5={station.AirQuality2.Value.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						if (station.AirQuality3.HasValue)
							Data.Append($"&AqPM2.5={station.AirQuality3.Value.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						if (station.AirQuality4.HasValue)
							Data.Append($"&AqPM2.5={station.AirQuality4.Value.ToString("F1", invC)}");
						break;
				}
			}

			if (cumulus.Wund.SendExtraTemp1 > 0 && cumulus.Wund.SendExtraTemp1 <= 10 && station.ExtraTemp[cumulus.Wund.SendExtraTemp1].HasValue)
			{
				Data.Append($"&temp2f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp1].Value)}");
			}
			if (cumulus.Wund.SendExtraTemp2 > 0 && cumulus.Wund.SendExtraTemp2 <= 10 && station.ExtraTemp[cumulus.Wund.SendExtraTemp2].HasValue)
			{
				Data.Append($"&temp3f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp2].Value)}");
			}
			if (cumulus.Wund.SendExtraTemp3 > 0 && cumulus.Wund.SendExtraTemp3 <= 10 && station.ExtraTemp[cumulus.Wund.SendExtraTemp3].HasValue)
			{
				Data.Append($"&temp4f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp3].Value)}");
			}
			if (cumulus.Wund.SendExtraTemp4 > 0 && cumulus.Wund.SendExtraTemp4 <= 10 && station.ExtraTemp[cumulus.Wund.SendExtraTemp4].HasValue)
			{
				Data.Append($"&temp5f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp4].Value)}");
			}

			Data.Append($"&softwaretype=Cumulus%20v{cumulus.Version}");
			Data.Append("&action=updateraw");
			if (cumulus.Wund.RapidFireEnabled && !CatchingUp)
			{
				Data.Append("&realtime=1&rtfreq=5");
			}

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}

		private void TimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(ID))
			{
				_ = DoUpdate(DateTime.Now);
			}
		}
	}
}
