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
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string pwstring;
			string URL = GetURL(out pwstring, timestamp);

			string starredpwstring = "&PASSWORD=" + new string('*', PW.Length);

			string logUrl = URL.Replace(pwstring, starredpwstring);
			if (!RapidFireEnabled)
			{
				cumulus.LogDebugMessage("Wunderground: URL = " + logUrl);
			}

			try
			{
				using var response = await cumulus.MyHttpClient.GetAsync(URL);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				if (response.StatusCode != HttpStatusCode.OK)
				{
					// Flag the error immediately if no rapid fire
					// Flag error after every 12 rapid fire failures (1 minute)
					ErrorFlagCount++;
					if (!RapidFireEnabled || ErrorFlagCount >= 12)
					{
						cumulus.LogMessage("Wunderground: Response = " + response.StatusCode + ": " + responseBodyAsText);
						cumulus.ThirdPartyAlarm.LastMessage = "Wunderground: HTTP response - " + response.StatusCode;
						cumulus.ThirdPartyAlarm.Triggered = true;
						ErrorFlagCount = 0;
					}
				}
				else
				{
					cumulus.ThirdPartyAlarm.Triggered = false;
					ErrorFlagCount = 0;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Wunderground: ERROR");
				cumulus.ThirdPartyAlarm.LastMessage = "Wunderground: " + ex.Message;
				cumulus.ThirdPartyAlarm.Triggered = true;
			}
			finally
			{
				Updating = false;
			}
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
			if (SendUV && station.UV >= 0)
				Data.Append($"&UV={station.UV.ToString(cumulus.UVFormat, invC)}");
			if (SendSolar)
				Data.Append($"&solarradiation={station.SolarRad:F0}");
			if (SendIndoor)
			{
				if (station.IndoorTemperature >= Cumulus.DefaultHiVal)
					Data.Append($"&indoortempf={WeatherStation.TempFstr(station.IndoorTemperature)}");
				if (station.IndoorHumidity >= 0)
					Data.Append($"&indoorhumidity={station.IndoorHumidity}");
			}
			// Davis soil and leaf sensors
			if (SendSoilTemp1 && station.SoilTemp[1] >= Cumulus.DefaultHiVal)
				Data.Append($"&soiltempf={WeatherStation.TempFstr(station.SoilTemp[1])}");
			if (SendSoilTemp2 && station.SoilTemp[2] >= Cumulus.DefaultHiVal)
				Data.Append($"&soiltempf2={WeatherStation.TempFstr(station.SoilTemp[2])}");
			if (SendSoilTemp3 && station.SoilTemp[3] >= Cumulus.DefaultHiVal)
				Data.Append($"&soiltempf3={WeatherStation.TempFstr(station.SoilTemp[3])}");
			if (SendSoilTemp4 && station.SoilTemp[4] >= Cumulus.DefaultHiVal)
				Data.Append($"&soiltempf4={WeatherStation.TempFstr(station.SoilTemp[4])}");

			if (SendSoilMoisture1 && station.SoilMoisture1 >= Cumulus.DefaultHiVal)
				Data.Append($"&soilmoisture={station.SoilMoisture1}");
			if (SendSoilMoisture2 && station.SoilMoisture2 >= Cumulus.DefaultHiVal)
				Data.Append($"&soilmoisture2={station.SoilMoisture2}");
			if (SendSoilMoisture3 && station.SoilMoisture3 >= Cumulus.DefaultHiVal)
				Data.Append($"&soilmoisture3={station.SoilMoisture3}");
			if (SendSoilMoisture4 && station.SoilMoisture4 >= Cumulus.DefaultHiVal)
				Data.Append($"&soilmoisture4={station.SoilMoisture4}");

			if (SendLeafWetness1 && station.LeafWetness1 >= Cumulus.DefaultHiVal)
				Data.Append($"&leafwetness={station.LeafWetness1}");
			if (SendLeafWetness2 && station.LeafWetness2 >= Cumulus.DefaultHiVal)
				Data.Append($"&leafwetness2={station.LeafWetness2}");

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
						if (station.AirQuality1 >= Cumulus.DefaultHiVal)
							Data.Append($"&AqPM2.5={station.AirQuality1.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						if (station.AirQuality2 >= Cumulus.DefaultHiVal)
							Data.Append($"&AqPM2.5={station.AirQuality2.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						if (station.AirQuality3 >= Cumulus.DefaultHiVal)
							Data.Append($"&AqPM2.5={station.AirQuality3.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						if (station.AirQuality4 >= Cumulus.DefaultHiVal)
							Data.Append($"&AqPM2.5={station.AirQuality4.ToString("F1", invC)}");
						break;
				}
			}

			if (cumulus.Wund.SendExtraTemp1 > 0 && cumulus.Wund.SendExtraTemp1 <= 10)
			{
				Data.Append($"&temp2f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp1])}");
			}
			if (cumulus.Wund.SendExtraTemp2 > 0 && cumulus.Wund.SendExtraTemp2 <= 10)
			{
				Data.Append($"&temp3f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp2])}");
			}
			if (cumulus.Wund.SendExtraTemp3 > 0 && cumulus.Wund.SendExtraTemp3 <= 10)
			{
				Data.Append($"&temp4f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp3])}");
			}
			if (cumulus.Wund.SendExtraTemp4 > 0 && cumulus.Wund.SendExtraTemp4 <= 10)
			{
				Data.Append($"&temp5f={WeatherStation.TempFstr(station.ExtraTemp[cumulus.Wund.SendExtraTemp4])}");
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
