using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadWCloud(Cumulus cumulus, string name) : WebUploadServiceBase(cumulus, name)
	{
		public bool SendLeafWetness;
		public int LeafWetnessSensor;

		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				var reason = Updating ? "previous upload still in progress" : "data stopped condition";
				cumulus.LogDebugMessage("WeatherCloud: Not uploading, " + reason);
				return;
			}

			Updating = true;

			// Random jitter
			await Task.Delay(Program.RandGenerator.Next(5000, 20000));

			string pwstring;
			string url = GetURL(out pwstring, timestamp);

			string starredpwstring = "<key>";

			string logUrl = url.Replace(pwstring, starredpwstring);

			cumulus.LogDebugMessage("WeatherCloud: URL = " + logUrl);

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.GetAsync(url);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					var msg = string.Empty;
					switch ((int) response.StatusCode)
					{
						case 200:
							msg = "Success";
							cumulus.ThirdPartyAlarm.Triggered = false;
							break;
						case 400:
							msg = "Bad request";
							cumulus.ThirdPartyAlarm.LastMessage = "WeatherCloud: " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
							break;
						case 401:
							msg = "Incorrect WID or Key";
							cumulus.ThirdPartyAlarm.LastMessage = "WeatherCloud: " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
							break;
						case 429:
							msg = "Too many requests";
							cumulus.ThirdPartyAlarm.LastMessage = "WeatherCloud: " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
							break;
						case 500:
							msg = "Server error";
							cumulus.ThirdPartyAlarm.LastMessage = "WeatherCloud: " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
							break;
						default:
							msg = "Unknown error";
							cumulus.ThirdPartyAlarm.LastMessage = "WeatherCloud: " + msg;
							cumulus.ThirdPartyAlarm.Triggered = true;
							break;
					}
					if ((int) response.StatusCode == 200)
					{
						cumulus.LogDebugMessage($"WeatherCloud: Response = {msg} ({response.StatusCode}): {responseBodyAsText}");
					}
					else
					{
						var log = $"WeatherCloud: ERROR - Response = {msg} ({response.StatusCode}): {responseBodyAsText}";
						cumulus.LogWarningMessage(log);
						cumulus.ThirdPartyAlarm.LastMessage = log;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}

					Updating = false;
					return;
				}
				catch (Exception ex)
				{
					if (retryCount == 0)
					{

						string msg;

						if (ex.InnerException is TimeoutException)
						{
							msg = $"WeatherCloud: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "WeatherCloud: Error - " + ex.Message;
							cumulus.LogExceptionMessage(ex, "WeatherCloud: Error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogDebugMessage($"WeatherCloud: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogDebugMessage("WeatherCloud: Error - " + ex.Message);
						}

						cumulus.LogMessage($"WeatherCloud: Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = PW;
			StringBuilder sb = new StringBuilder($"https://api.weathercloud.net/v01/set?wid={ID}&key={PW}");

			//Temperature
			if (station.IndoorTemperature > Cumulus.DefaultHiVal)
				sb.Append("&tempin=" + (int) Math.Round(ConvertUnits.UserTempToC(station.IndoorTemperature) * 10));
			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				sb.Append("&temp=" + (int) Math.Round(ConvertUnits.UserTempToC(station.OutdoorTemperature) * 10));
			if (station.WindChill > Cumulus.DefaultHiVal)
				sb.Append("&chill=" + (int) Math.Round(ConvertUnits.UserTempToC(station.WindChill) * 10));
			if (station.OutdoorDewpoint > Cumulus.DefaultHiVal)
				sb.Append("&dew=" + (int) Math.Round(ConvertUnits.UserTempToC(station.OutdoorDewpoint) * 10));
			if (station.HeatIndex > Cumulus.DefaultHiVal)
				sb.Append("&heat=" + (int) Math.Round(ConvertUnits.UserTempToC(station.HeatIndex) * 10));

			// Humidity
			if (station.IndoorHumidity >= 0)
				sb.Append("&humin=" + station.IndoorHumidity);
			if (station.OutdoorHumidity >= 0)
				sb.Append("&hum=" + station.OutdoorHumidity);

			// Wind
			if (station.WindLatest >= 0)
				sb.Append("&wspd=" + (int) Math.Round(ConvertUnits.UserWindToMS(station.WindLatest) * 10));
			if (station.RecentMaxGust >= 0)
				sb.Append("&wspdhi=" + (int) Math.Round(ConvertUnits.UserWindToMS(station.RecentMaxGust) * 10));
			sb.Append("&wspdavg=" + (int) Math.Round(ConvertUnits.UserWindToMS(station.WindAverage) * 10));

			// Wind Direction
			sb.Append("&wdir=" + station.Bearing);
			sb.Append("&wdiravg=" + station.AvgBearing);

			// Pressure
			if (station.Pressure > 0)
				sb.Append("&bar=" + (int) Math.Round(ConvertUnits.UserPressToMB(station.Pressure * 10)));

			// rain
			if (station.RainToday >= 0)
				sb.Append("&rain=" + (int) Math.Round(ConvertUnits.UserRainToMM(station.RainToday) * 10));
			if (station.RainRate >= 0)
				sb.Append("&rainrate=" + (int) Math.Round(ConvertUnits.UserRainToMM(station.RainRate) * 10));

			// ET
			if (SendSolar && cumulus.Manufacturer == Cumulus.DAVIS)
			{
				sb.Append("&et=" + (int) Math.Round(ConvertUnits.UserRainToMM(station.ET) * 10));
			}

			// solar
			if (SendSolar && station.SolarRad >= 0)
			{
				sb.Append("&solarrad=" + station.SolarRad * 10);
			}

			// uv
			if (SendUV && station.UV >= 0)
			{
				sb.Append("&uvi=" + (int) Math.Round(station.UV * 10));
			}

			// aq
			if (SendAirQuality)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"&pm25={cumulus.airLinkDataOut.pm2p5:F0}");
							sb.Append($"&pm10={cumulus.airLinkDataOut.pm10:F0}");
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(cumulus.airLinkDataOut.pm2p5_24hr)}");
						}
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
						if (station.AirQuality1 >= 0)
							sb.Append($"&pm25={station.AirQuality1:F0}");
						if (station.AirQualityAvg1 >= 0)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg1)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						if (station.AirQuality2 >= 0)
							sb.Append($"&pm25={station.AirQuality2:F0}");
						if (station.AirQualityAvg2 >= 0)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg2)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						if (station.AirQuality3 >= 0)
							sb.Append($"&pm25={station.AirQuality3:F0}");
						if (station.AirQualityAvg3 >= 0)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg3)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						if (station.AirQuality4 >= 0)
							sb.Append($"&pm25={station.AirQuality4:F0}");
						if (station.AirQualityAvg4 >= 0)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg4)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
						if (station.CO2_pm2p5 >= 0)
							sb.Append($"&pm25={station.CO2_pm2p5:F0}");
						if (station.CO2_pm10 >= 0)
							sb.Append($"&pm10={station.CO2_pm10:F0}");
						if (station.CO2_pm2p5_24h >= 0)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.CO2_pm2p5_24h)}");
						break;
				}
			}

			// soil moisture
			if (SendSoilMoisture)
			{
				// Weathercloud wants soil moisture in centibar. Davis supplies this, but Ecowitt provide a percentage
				int moist = 0;

				switch (cumulus.WCloud.SoilMoistureSensor)
				{
					case 1:
						moist = station.SoilMoisture1;
						break;
					case 2:
						moist = station.SoilMoisture2;
						break;
					case 3:
						moist = station.SoilMoisture3;
						break;
					case 4:
						moist = station.SoilMoisture4;
						break;
					case 5:
						moist = station.SoilMoisture5;
						break;
					case 6:
						moist = station.SoilMoisture6;
						break;
					case 7:
						moist = station.SoilMoisture7;
						break;
					case 8:
						moist = station.SoilMoisture8;
						break;
					case 9:
						moist = station.SoilMoisture9;
						break;
					case 10:
						moist = station.SoilMoisture10;
						break;
					case 11:
						moist = station.SoilMoisture11;
						break;
					case 12:
						moist = station.SoilMoisture12;
						break;
					case 13:
						moist = station.SoilMoisture13;
						break;
					case 14:
						moist = station.SoilMoisture14;
						break;
					case 15:
						moist = station.SoilMoisture15;
						break;
					case 16:
						moist = station.SoilMoisture16;
						break;
				}

				if (cumulus.Manufacturer == Cumulus.EW)
				{
					// very! approximate conversion from percentage to cb
					moist = (100 - moist) * 2;
				}

				sb.Append($"&soilmoist={moist}");
			}

			// leaf wetness
			if (SendLeafWetness)
			{
				double wet = 0;

				switch (cumulus.WCloud.LeafWetnessSensor)
				{
					case 1:
						wet = station.LeafWetness1;
						break;
					case 2:
						wet = station.LeafWetness2;
						break;
					case 3:
						wet = station.LeafWetness3;
						break;
					case 4:
						wet = station.LeafWetness4;
						break;
					case 5:
						wet = station.LeafWetness5;
						break;
					case 6:
						wet = station.LeafWetness6;
						break;
					case 7:
						wet = station.LeafWetness7;
						break;
					case 8:
						wet = station.LeafWetness8;
						break;
				}

				sb.Append($"&leafwet={wet.ToString(cumulus.LeafWetFormat)}");
			}

			// date - UTC
			sb.Append("&date=" + timestamp.ToUniversalTime().ToString("yyyyMMdd"));

			// software identification
			sb.Append($"&software=Cumulus_MX_v{cumulus.Version}&softwareid=142787ebe716");

			return sb.ToString();
		}
	}
}
