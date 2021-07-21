using System;
using Unosquare.Labs.EmbedIO;
using System.IO;
using System.Web;
using System.Globalization;
using System.Collections.Specialized;

namespace CumulusMX
{

	class HttpStationEcowitt : WeatherStation
	{
		private readonly WeatherStation station;
		private bool stopping = false;

		public HttpStationEcowitt(Cumulus cumulus, WeatherStation station = null) : base(cumulus)
		{
			this.station = station;

			if (station == null)
			{
				cumulus.LogMessage("Creating HTTP Station (Ecowitt)");
			}
			else
			{
				cumulus.LogMessage("Creating Extra Sensors - HTTP Station (Ecowitt)");
			}

			//cumulus.StationOptions.CalculatedWC = true;
			// GW1000 does not provide average wind speeds
			cumulus.StationOptions.UseWind10MinAve = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;
			// GW1000 does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

			cumulus.Manufacturer = cumulus.ECOWITT;
			cumulus.AirQualityUnitText = "µg/m³";
			cumulus.SoilMoistureUnitText = "%";

			// Only perform the Start-up if we are a proper station, not a Extra Sensor
			if (station == null)
			{
				Start();
			}
		}

		public override void Start()
		{
			if (station == null)
			{
				cumulus.LogMessage("Starting HTTP Station (Ecowitt)");

				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);
				timerStartNeeded = true;
			}
			else
			{
				cumulus.LogMessage("Starting Extra Sensors - HTTP Station (Ecowitt)");
			}
		}

		public override void Stop()
		{
			stopping = true;
			if (station == null)
			{
				StopMinuteTimer();
			}
		}

		public string ProcessData(IHttpContext context)
		{
			/*
			 * Ecowitt doc:
			 *
			POST Parameters - all fields are URL escaped

			 */

			DateTime recDate;

			if (stopping)
			{
				context.Response.StatusCode = 200;
				return "success";
			}

			try
			{
				// PASSKEY
				// dateutc
				// freq
				// model

				cumulus.LogDebugMessage("ProcessData: Processing posted data");

				var text = new StreamReader(context.Request.InputStream).ReadToEnd();

				cumulus.LogDataMessage("ProcessData: Payload = " + text);

				var data = HttpUtility.ParseQueryString(text);

				var dat = data["dateutc"];

				if (dat == null)
				{
					cumulus.LogMessage($"ProcessData: Error, no 'dateutc' parameter found");
					context.Response.StatusCode = 500;
					return "{\"result\":\"Failed\",\"Errors\":[\"No 'dateutc' parameter found\"]}";
				}
				else if (dat == "now")
				{
					recDate = DateTime.Now;
				}
				else
				{
					dat = dat.Replace(' ', 'T') + ".0000000Z";
					cumulus.LogDebugMessage($"ProcessData: Record date = {data["dateutc"]}");
					recDate = DateTime.ParseExact(dat, "o", CultureInfo.InvariantCulture);
				}

				cumulus.LogDebugMessage($"ProcessData: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");

				// === Wind ==
				try
				{
					// winddir
					// winddir_avg10m ??
					// windgustmph
					// windspeedmph
					// windspdmph_avg2m ??
					// windspdmph_avg10m ??
					// windgustmph_10m ??
					// maxdailygust

					var gust = data["windgustmph"];
					var dir = data["winddir"];
					var avg = data["windspeedmph"];


					if (gust == null || dir == null || avg == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing wind data");
					}
					else
					{
						var gustVal = ConvertWindMPHToUser(Convert.ToDouble(gust));
						var dirVal = Convert.ToInt32(dir);
						var avgVal = ConvertWindMPHToUser(Convert.ToDouble(avg));
						DoWind(gustVal, dirVal, avgVal, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Wind data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in wind data - " + ex.Message;
				}


				// === Humidity ===
				try
				{
					// humidity
					// humidityin

					var humIn = data["humidityin"];
					var humOut = data["humidity"];


					if (humIn == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing indoor humidity");
					}
					else
					{
						var humVal = Convert.ToInt32(humIn);
						DoIndoorHumidity(humVal);
					}

					if (humOut == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing outdoor humidity");
					}
					else
					{
						var humVal = Convert.ToInt32(humOut);
						DoOutdoorHumidity(humVal, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Humidity data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in humidity data - " + ex.Message;
				}


				// === Pressure ===
				try
				{
					// baromabsin
					// baromrelin

					var press = data["baromrelin"];

					if (press == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing baro pressure");
					}
					else
					{
						var pressVal = ConvertPressINHGToUser(Convert.ToDouble(press));
						DoPressure(pressVal, recDate);
						UpdatePressureTrendString();
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Pressure data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in baro pressure data - " + ex.Message;
				}


				// === Indoor temp ===
				try
				{
					// tempinf

					var temp = data["tempinf"];

					if (temp == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing indoor temp");
					}
					else
					{
						var tempVal = ConvertTempFToUser(Convert.ToDouble(temp));
						DoIndoorTemp(tempVal);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Indoor temp data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in indoor temp data - " + ex.Message;
				}


				// === Outdoor temp ===
				try
				{
					// tempf

					var temp = data["tempf"];

					if (temp == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing outdoor temp");
					}
					else
					{
						var tempVal = ConvertTempFToUser(Convert.ToDouble(temp));
						DoOutdoorTemp(tempVal, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Outdoor temp data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in outdoor temp data - " + ex.Message;
				}


				// === Rain ===
				try
				{
					// rainin
					// hourlyrainin
					// dailyrainin
					// weeklyrainin
					// monthlyrainin
					// yearlyrainin
					// totalrainin
					// rainratein
					// 24hourrainin Ambient only?
					// eventrainin

					var rain = data["totalrainin"];
					var rRate = data["rainratein"];

					if (rain == null || rRate == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing rainfall");
					}
					else
					{
						var rainVal = ConvertRainINToUser(Convert.ToDouble(rain));
						var rateVal = ConvertRainINToUser(Convert.ToDouble(rRate));
						DoRain(rainVal, rateVal, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Rain data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in rainfall data - " + ex.Message;
				}


				// === Dewpoint ===
				try
				{
					// dewptf

					if (cumulus.StationOptions.CalculatedDP)
					{
						DoOutdoorDewpoint(0, recDate);
					}
					else
					{
						var str = data["dewptf"];
						if (str == null)
						{
							cumulus.LogMessage($"ProcessData: Error, missing dew point");
						}
						else
						{
							var val = ConvertTempFToUser(Convert.ToDouble(str));
							DoOutdoorDewpoint(val, recDate);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Dew point data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in dew point data - " + ex.Message;
				}


				// === Wind Chill ===
				try
				{
					// windchillf

					if (cumulus.StationOptions.CalculatedWC && data["tempf"] != null && data["windspeedmph"] != null)
					{
						DoWindChill(0, recDate);
					}
					else
					{
						var chill = data["windchillf"];
						if (chill == null)
						{
							cumulus.LogMessage($"ProcessData: Error, missing dew point");
						}
						else
						{
							var val = ConvertTempFToUser(Convert.ToDouble(chill));
							DoWindChill(val, recDate);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Dew point data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in dew point data - " + ex.Message;
				}


				// === Humidex ===
				if (data["tempf"] != null && data["humidity"] != null)
				{
					DoHumidex(recDate);

				// === Apparent === - requires temp, hum, and windspeed
					if (data["windspeedmph"] != null)
					{
						DoApparentTemp(recDate);
						DoFeelsLike(recDate);
					}
					else
					{
						cumulus.LogMessage("ProcessData: Insufficent data to calculate Apparent/Feels Like temps");
					}
				}
				else
				{
					cumulus.LogMessage("ProcessData: Insufficent data to calculate Humidex and Apparent/Feels Like temps");
				}

				DoForecast(string.Empty, false);


				// === Extra Temperature ===
				try
				{
					// temp[1-10]f

					ProcessExtraTemps(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in extra temperature data - " + ex.Message);
				}


				// === Extra Humidity ===
				try
				{
					// humidity[1-10]

					ProcessExtraHumidity(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in extra humidity data - " + ex.Message);
				}


				// === Solar ===
				try
				{
					// solarradiation
					ProcessSolar(data, this, recDate);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in solar data - " + ex.Message);
				}


				// === UV ===
				try
				{
					// uv

					ProcessUv(data, recDate);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in UV data - " + ex.Message);
				}


				// === Soil Temp ===
				try
				{
					// soiltempf
					// soiltemp[2-16]f

					ProcessSoilTemps(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil temp data - " + ex.Message);
				}


				// === Soil Moisture ===
				try
				{
					// soilmoisture[1-16]

					ProcessSoilMoist(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil moisture data - " + ex.Message);
				}


				// === Leaf Wetness ===
				try
				{
					// leafwetness
					// leafwetness[2-8]

					ProcessLeafWetness(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Leaf wetness data - " + ex.Message);
				}


				// === Air Quality ===
				try
				{
					// pm25_ch[1-4]
					// pm25_avg_24h_ch[1-4]

					// tf_co2
					// humi_co2
					// pm25_co2
					// pm25_24_co2
					// pm10_co2
					// pm10_24h_co2
					// co2
					// co2_24

					ProcessAirQuality(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Air Quality data - " + ex.Message);
				}

				// === Lightning ===
				try
				{
					// lightning
					// lightning_time
					// lightning_num

					ProcessLightning(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Lightning data - " + ex.Message);
				}

				// === Leak ===
				try
				{
					// leak[1 - 4]

					ProcessLeak(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Leak data - " + ex.Message);
				}

				// === Batteries ===
				try
				{
					/*
					wh25batt
					wh26batt
					wh32batt
					wh40batt
					wh57batt
					wh65batt
					wh68batt
					wh80batt
					batt[1-8] (wh31)
					soilbatt[1-8] (wh51)
					pm25batt[1-4] (wh41/wh43)
					leakbatt[1-4] (wh55)
					co2_batt
					*/

					ProcessBatteries(data);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Battery data - " + ex.Message);
				}


				UpdateStatusPanel(recDate);
				UpdateMQTT();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ProcessData: Error - " + ex.Message);
				context.Response.StatusCode = 500;
				return "Failed: General error - " + ex.Message;
			}

			cumulus.LogDebugMessage($"ProcessData: Complete");

			context.Response.StatusCode = 200;
			return "success";
		}

		public string ProcessExtraData(IHttpContext context)
		{
			/*
			 * Ecowitt doc:
			 *
			POST Parameters - all fields are URL escaped

			 */

			if (stopping)
			{
				context.Response.StatusCode = 200;
				return "success";
			}

			DateTime recDate;

			try
			{
				// PASSKEY
				// dateutc
				// freq
				// model

				cumulus.LogDebugMessage("ProcessExtraData: Processing posted data");

				var text = new StreamReader(context.Request.InputStream).ReadToEnd();

				cumulus.LogDataMessage("ProcessExtraData: Payload = " + text);

				var data = HttpUtility.ParseQueryString(text);

				var dat = data["dateutc"];

				if (dat == null)
				{
					cumulus.LogMessage($"ProcessExtraData: Error, no 'dateutc' parameter found");
					context.Response.StatusCode = 500;
					return "{\"result\":\"Failed\",\"Errors\":[\"No 'dateutc' parameter found\"]}";
				}
				else if (dat == "now")
				{
					recDate = DateTime.Now;
				}
				else
				{
					dat = dat.Replace(' ', 'T') + ".0000000Z";
					cumulus.LogDebugMessage($"ProcessExtraData: Record date = {data["dateutc"]}");
					recDate = DateTime.ParseExact(dat, "o", CultureInfo.InvariantCulture);
				}

				cumulus.LogDebugMessage($"ProcessExtraData: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");


				// === Extra Temperature ===
				try
				{
					// temp[1-10]f

					ProcessExtraTemps(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in extra temperature data - " + ex.Message);
				}


				// === Extra Humidity ===
				try
				{
					// humidity[1-10]

					ProcessExtraHumidity(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in extra humidity data - " + ex.Message);
				}


				// === Solar ===
				try
				{
					// solarradiation

					ProcessSolar(data, station, recDate);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in solar data - " + ex.Message);
				}


				// === UV ===
				try
				{
					// uv

					ProcessUv(data, recDate);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in UV data - " + ex.Message);
				}


				// === Soil Temp ===
				try
				{
					// soiltempf
					// soiltemp[2-16]f

					ProcessSoilTemps(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Soil temp data - " + ex.Message);
				}


				// === Soil Moisture ===
				try
				{
					// soilmoisture[1-16]

					ProcessSoilMoist(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Soil moisture data - " + ex.Message);
				}


				// === Leaf Wetness ===
				try
				{
					// leafwetness
					// leafwetness[2-8]

					ProcessLeafWetness(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Leaf wetness data - " + ex.Message);
				}


				// === Air Quality ===
				try
				{
					// pm25_ch[1-4]
					// pm25_avg_24h_ch[1-4]

					// tf_co2
					// humi_co2
					// pm25_co2
					// pm25_24_co2
					// pm10_co2
					// pm10_24h_co2
					// co2
					// co2_24

					ProcessAirQuality(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Air Quality data - " + ex.Message);
				}

				// === Lightning ===
				try
				{
					// lightning
					// lightning_time
					// lightning_num

					ProcessLightning(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Lightning data - " + ex.Message);
				}

				// === Leak ===
				try
				{
					// leak[1 - 4]

					ProcessLeak(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Leak data - " + ex.Message);
				}

				// === Batteries ===
				try
				{
					/*
					wh25batt
					wh26batt
					wh32batt
					wh40batt
					wh57batt
					wh65batt
					wh68batt
					wh80batt
					batt[1-8] (wh31)
					soilbatt[1-8] (wh51)
					pm25batt[1-4] (wh41/wh43)
					leakbatt[1-4] (wh55)
					*/

					ProcessBatteries(data);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Battery data - " + ex.Message);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ProcessExtraData: Error - " + ex.Message);
				context.Response.StatusCode = 500;
				return "Failed: General error - " + ex.Message;
			}

			cumulus.LogDebugMessage($"ProcessExtraData: Complete");

			context.Response.StatusCode = 200;
			return "success";
		}

		private void ProcessExtraTemps(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["temp" + i + "f"] != null)
				{
					station.DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(data["temp" + i + "f"])), i);
				}
			}
		}


		private void ProcessExtraHumidity(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["humidity" + i] != null)
				{
					station.DoExtraHum(Convert.ToDouble(data["humidity" + i]), i);
				}
			}
		}
		
		private void ProcessSolar(NameValueCollection data, WeatherStation station, DateTime recDate)
		{
			if (data["solarradiation"] != null)
			{
				station.DoSolarRad((int)Convert.ToDouble(data["solarradiation"]), recDate);
			}
		}

		private void ProcessUv(NameValueCollection data, DateTime recDate)
		{
			if (data["uv"] != null)
			{
				DoUV(Convert.ToDouble(data["uv"]), recDate);
			}
		}
		
		private void ProcessSoilTemps(NameValueCollection data, WeatherStation station)
		{
			if (data["soiltempf"] != null)
			{
				station.DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(data["soiltempf"])), 1);
			}

			for (var i = 2; i <= 16; i++)
			{
				if (data["soiltemp" + i + "f"] != null)
				{
					station.DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(data["soiltemp" + i + "f"])), i - 1);
				}
			}
		}

		private void ProcessSoilMoist(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 16; i++)
			{
				if (data["soilmoisture" + i] != null)
				{
					station.DoSoilMoisture(Convert.ToDouble(data["soilmoisture" + i]), i);
				}
			}
		}

		private void ProcessLeafWetness(NameValueCollection data, WeatherStation station)
		{
			if (data["leafwetness"] != null)
			{
				station.DoLeafWetness(Convert.ToDouble(data["leafwetness"]), 1);
			}
			// Though Ecowitt supports up to 8 sensors, MX only supports the first 4
			for (var i = 2; i <= 4; i++)
			{
				if (data["leafwetness" + i] != null)
				{
					station.DoLeafWetness(Convert.ToDouble(data["leafwetness" + i]), i - 1);
				}
			}

		}

		private void ProcessAirQuality(NameValueCollection data, WeatherStation station)
		{
			// pm25_ch[1-4]
			// pm25_avg_24h_ch[1-4]

			// tf_co2
			// humi_co2
			// pm25_co2
			// pm25_24_co2
			// pm10_co2
			// pm10_24h_co2
			// co2
			// co2_24

			if (data["tf_co2"] != null)
			{
				station.CO2_temperature = Convert.ToDouble(data["tf_co2"]);
			}
			if (data["humi_co2"] != null)
			{
				station.CO2_humidity = Convert.ToInt32(data["humi_co2"]);
			}
			if (data["pm25_co2"] != null)
			{
				station.CO2_pm2p5 = Convert.ToDouble(data["pm25_co2"]);
			}
			if (data["pm25_24_co2"] != null)
			{
				station.CO2_pm2p5_24h = Convert.ToDouble(data["pm25_24_co2"]);
			}
			if (data["pm10_co2"] != null)
			{
				station.CO2_pm10 = Convert.ToDouble(data["pm10_co2"]);
			}
			if (data["pm10_24h_co2"] != null)
			{
				station.CO2_pm10_24h = Convert.ToDouble(data["pm10_24h_co2"]);
			}
			if (data["co2"] != null)
			{
				station.CO2 = Convert.ToInt32(data["co2"]);
			}
			if (data["co2_24"] != null)
			{
				station.CO2_24h = Convert.ToInt32(data["co2"]);
			}

			for (var i = 1; i <= 4; i++)
			{
				var pm = data["pm25_ch" + i];
				var pmAvg = data["pm25_avg_24h_ch" + i];
				if (pm != null)
				{
					station.DoAirQuality(Convert.ToDouble(pm), i);
				}
				if (pmAvg != null)
				{
					station.DoAirQualityAvg(Convert.ToDouble(pmAvg), i);
				}
			}
		}

		private void ProcessLightning(NameValueCollection data, WeatherStation station)
		{
			var dist = data["lightning"];
			var time = data["lightning_time"];
			var num = data["lightning_num"];

			if (!string.IsNullOrEmpty(dist) && !string.IsNullOrEmpty(time))
			{
				// Only set the lightning time/distance if it is newer than what we already have - the GW1000 seems to reset this value
				var valDist = Convert.ToDouble(dist);
				if (valDist != 255)
				{
					station.LightningDistance = ConvertKmtoUserUnits(valDist);
				}

				var valTime = Convert.ToDouble(time);
				// Sends a default value until the first strike is detected of 0xFFFFFFFF
				if (valTime != 0xFFFFFFFF)
				{
					var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					dtDateTime = dtDateTime.AddSeconds(valTime).ToLocalTime();

					if (dtDateTime > LightningTime)
					{
						station.LightningTime = dtDateTime;
					}
				}
			}

			if (!string.IsNullOrEmpty(num))
			{
				station.LightningStrikesToday = Convert.ToInt32(num);
			}
		}

		private void ProcessLeak(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 4; i++)
			{
				if (data["leak" + i] != null)
				{
					station.DoLeakSensor(Convert.ToInt32(data["leak" + i]), i);
				}
			}
		}

		private void ProcessBatteries(NameValueCollection data)
		{
			var lowBatt = false;
			lowBatt = lowBatt || (data["wh25batt"] != null && data["wh25batt"] == "1");
			lowBatt = lowBatt || (data["wh26batt"] != null && data["wh26batt"] == "1");
			lowBatt = lowBatt || (data["wh40batt"] != null && data["wh40batt"] == "1");
			lowBatt = lowBatt || (data["wh40batt"] != null && data["wh40batt"] == "1");
			lowBatt = lowBatt || (data["wh57batt"] != null && data["wh57batt"] == "1");
			lowBatt = lowBatt || (data["wh65batt"] != null && data["wh65batt"] == "1");
			lowBatt = lowBatt || (data["wh68batt"] != null && Convert.ToDouble(data["wh68batt"]) <= 1.2);
			lowBatt = lowBatt || (data["wh80batt"] != null && Convert.ToDouble(data["wh80batt"]) <= 1.2);
			for (var i = 1; i < 5; i++)
			{
				lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "1");
				lowBatt = lowBatt || (data["soilbatt" + i] != null && Convert.ToDouble(data["soilbatt" + i]) <= 1.2);
				lowBatt = lowBatt || (data["pm25batt" + i] != null && data["pm25batt" + i] == "1");
				lowBatt = lowBatt || (data["leakbatt" + i] != null && data["leakbatt" + i] == "1");
			}
			for (var i = 5; i < 9; i++)
			{
				lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "1");
				lowBatt = lowBatt || (data["soilbatt" + i] != null && Convert.ToDouble(data["soilbatt" + i]) <= 1.2);
			}

			cumulus.BatteryLowAlarm.Triggered = lowBatt;
		}
	}
}
