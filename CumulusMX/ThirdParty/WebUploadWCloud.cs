using System;
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
			if (station.IndoorTemperature.HasValue)
				sb.Append("&tempin=" + (int) Math.Round(ConvertUnits.UserTempToC(station.IndoorTemperature.Value) * 10));
			if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
				sb.Append("&temp=" + (int) Math.Round(ConvertUnits.UserTempToC(station.OutdoorTemperature) * 10));
			if (station.WindChill > Cumulus.DefaultHiVal)
				sb.Append("&chill=" + (int) Math.Round(ConvertUnits.UserTempToC(station.WindChill) * 10));
			if (station.OutdoorDewpoint > Cumulus.DefaultHiVal)
				sb.Append("&dew=" + (int) Math.Round(ConvertUnits.UserTempToC(station.OutdoorDewpoint) * 10));
			if (station.HeatIndex > Cumulus.DefaultHiVal)
				sb.Append("&heat=" + (int) Math.Round(ConvertUnits.UserTempToC(station.HeatIndex) * 10));

			// Humidity
			if (station.IndoorHumidity.HasValue)
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
			if (SendSolar && cumulus.Manufacturer == Cumulus.StationManufacturer.DAVIS)
			{
				sb.Append("&et=" + (int) Math.Round(ConvertUnits.UserRainToMM(station.ET) * 10));
			}

			// solar
			if (SendSolar && station.SolarRad.HasValue)
			{
				sb.Append("&solarrad=" + station.SolarRad * 10);
			}

			// uv
			if (SendUV && station.UV.HasValue)
			{
				sb.Append("&uvi=" + (int) Math.Round(station.UV.Value * 10));
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
						if (station.AirQuality[1].HasValue)
							sb.Append($"&pm25={station.AirQuality[1]:F0}");
						if (station.AirQualityAvg[1].HasValue)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg[1].Value)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						if (station.AirQuality[2].HasValue)
							sb.Append($"&pm25={station.AirQuality[2]:F0}");
						if (station.AirQualityAvg[2].HasValue)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg[2].Value)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						if (station.AirQuality[3].HasValue)
							sb.Append($"&pm25={station.AirQuality[3]:F0}");
						if (station.AirQualityAvg[3].HasValue)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg[3].Value)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						if (station.AirQuality[4].HasValue)
							sb.Append($"&pm25={station.AirQuality[4]:F0}");
						if (station.AirQualityAvg[4].HasValue)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.AirQualityAvg[4].Value)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
						if (station.CO2_pm2p5.HasValue)
							sb.Append($"&pm25={station.CO2_pm2p5:F0}");
						if (station.CO2_pm10.HasValue)
							sb.Append($"&pm10={station.CO2_pm10:F0}");
						if (station.CO2_pm2p5_24h.HasValue)
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(station.CO2_pm2p5_24h.Value)}");
						break;
				}
			}

			// soil moisture
			if (SendSoilMoisture)
			{
				// Weathercloud wants soil moisture in centibar. Davis supplies this, but Ecowitt provide a percentage
				int? moist = null;
				if (cumulus.WCloud.SoilMoistureSensor < station.SoilMoisture.Length)
				{
					moist = station.SoilMoisture[cumulus.WCloud.SoilMoistureSensor];
				}

				if (moist.HasValue)
				{
					if (cumulus.Manufacturer == Cumulus.StationManufacturer.EW)
					{
						// very! approximate conversion from percentage to cb
						moist = (100 - moist) * 2;
					}

					sb.Append($"&soilmoist={moist}");
				}
			}

			// leaf wetness
			if (SendLeafWetness)
			{
				double? wet = null;

				if (cumulus.WCloud.LeafWetnessSensor < station.LeafWetness.Length)
				{
					wet = station.LeafWetness[cumulus.WCloud.LeafWetnessSensor];
				}

				if (wet.HasValue)
				{
					sb.Append($"&leafwet={wet.Value.ToString(cumulus.LeafWetFormat)}");
				}
			}

			// date - UTC
			sb.Append("&date=" + timestamp.ToUniversalTime().ToString("yyyyMMdd"));

			// software identification
			sb.Append($"&software=Cumulus_MX_v{cumulus.Version}&softwareid=142787ebe716");

			return sb.ToString();
		}
	}
}
