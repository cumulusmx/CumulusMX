using System;
using Unosquare.Labs.EmbedIO;
using System.IO;
using System.Web;
using System.Globalization;

namespace CumulusMX
{
	class HttpStationAmbient : WeatherStation
	{
		private readonly WeatherStation station;
		private bool stopping = false;

		public HttpStationAmbient(Cumulus cumulus, WeatherStation station = null) : base(cumulus)
		{
			this.station = station;

			if (station == null)
			{
				cumulus.LogMessage("Creating HTTP Station (Ambient)");
			}
			else
			{
				cumulus.LogMessage("Creating Extra Sensors - HTTP Station (Ambient)");
			}


			//cumulus.StationOptions.CalculatedWC = true;
			// Ambient does not provide average wind speeds
			cumulus.StationOptions.UseWind10MinAve = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;
			// Ambient does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

			cumulus.Manufacturer = cumulus.AMBIENT;
			if (station == null || (station != null && cumulus.AmbientExtraUseAQI))
			{
				cumulus.AirQualityUnitText = "µg/m³";
			}
			if (station == null || (station != null && cumulus.AmbientExtraUseSoilMoist))
			{
				cumulus.SoilMoistureUnitText = "%";
			}

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
				cumulus.LogMessage("Starting HTTP Station (Ambient)");

				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);
				timerStartNeeded = true;
			}
			else
			{
				cumulus.LogMessage("Starting Extra Sensors - HTTP Station (Ambient)");
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
			 * Ambient doc: https://help.ambientweather.net/help/advanced/
			 *
			 *	GET Parameters - all fields are URL escaped
			 *
			 */

			DateTime recDate;

			try
			{
				// MAC
				// dateutc
				// freq
				// model

				cumulus.LogDebugMessage($"ProcessData: Processing query - {context.Request.RawUrl}");

				var data = context.Request.QueryString;

				// We will ignore the dateutc field other than for reporting, this is "live" data so just use "now" to avoid any clock issues

				var dat = data["dateutc"];

				if (dat == null)
				{
					cumulus.LogMessage($"ProcessData: Error, no 'dateutc' parameter found");
					//context.Response.StatusCode = 500;
					//return "{\"result\":\"Failed\",\"Errors\":[\"No 'dateutc' parameter found\"]}";
				}
				else if (dat == "now")
				{
					//recDate = DateTime.Now;
				}
				else
				{
					dat = dat.Replace(' ', 'T') + ".0000000Z";
					cumulus.LogDebugMessage($"ProcessData: Record date = {data["dateutc"]}");
					//recDate = DateTime.ParseExact(dat, "o", CultureInfo.InvariantCulture);
				}

				recDate = DateTime.Now;

				cumulus.LogDebugMessage($"ProcessData: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");

				// === Wind ==
				try
				{
					// winddir					instantaneous wind direction - [int - degrees]
					// windspeedmph				instantaneous wind speed - [float - mph]
					// windgustmph				Instantaneous wind gust - [float - mph]
					// windgustdir				Wind direction at which the wind gust occurred - [int - degrees]
					// maxdailygust				Max daily gust - [float - mph]
					// windspdmph_avg2m			Average wind speed, 2 minute average - [float - mph]
					// winddir_avg2m			Average wind direction, 2 minute average - [int - degrees]
					// windspdmph_avg10m		Average wind speed, 10 minute average - [float - mph]
					// winddir_avg10m			Average wind direction, 10 minute average - [int - degrees]
					// windgustmph_interval		Max Wind Speed in update interval, the default is one minute - [int - minutes]

					var gust = data["windgustmph"];
					var dir = data["winddir"];
					var avg = data["windspeedmph"];


					if (gust == null || dir == null || avg == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing wind data");
					}
					else
					{
						var gustVal = ConvertWindMPHToUser(Convert.ToDouble(gust, CultureInfo.InvariantCulture));
						var dirVal = Convert.ToInt32(dir, CultureInfo.InvariantCulture);
						var avgVal = ConvertWindMPHToUser(Convert.ToDouble(avg, CultureInfo.InvariantCulture));
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
						var humVal = Convert.ToInt32(humIn, CultureInfo.InvariantCulture);
						DoIndoorHumidity(humVal);
					}

					if (humOut == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing outdoor humidity");
					}
					else
					{
						var humVal = Convert.ToInt32(humOut, CultureInfo.InvariantCulture);
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
						var pressVal = ConvertPressINHGToUser(Convert.ToDouble(press, CultureInfo.InvariantCulture));
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
						var tempVal = ConvertTempFToUser(Convert.ToDouble(temp, CultureInfo.InvariantCulture));
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
						var tempVal = ConvertTempFToUser(Convert.ToDouble(temp, CultureInfo.InvariantCulture));
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
					// hourlyrainin
					// dailyrainin
					// 24hourrainin
					// weeklyrainin
					// monthlyrainin
					// yearlyrainin
					// eventrainin
					// totalrainin

					var rain = data["totalrainin"];
					var rRate = data["hourlyrainin"]; // no rain rate, have to use the hourly rain

					if (rain == null || rRate == null)
					{
						cumulus.LogMessage($"ProcessData: Error, missing rainfall");
					}
					else
					{
						var rainVal = ConvertRainINToUser(Convert.ToDouble(rain, CultureInfo.InvariantCulture));
						var rateVal = ConvertRainINToUser(Convert.ToDouble(rRate, CultureInfo.InvariantCulture));
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
							var val = ConvertTempFToUser(Convert.ToDouble(str, CultureInfo.InvariantCulture));
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
							var val = ConvertTempFToUser(Convert.ToDouble(chill, CultureInfo.InvariantCulture));
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
						cumulus.LogMessage("ProcessData: Insufficient data to calculate Apparent/Feels Like temps");
					}
				}
				else
				{
					cumulus.LogMessage("ProcessData: Insufficient data to calculate Humidex and Apparent/Feels Like temps");
				}

				// === Extra Temperature ===
				try
				{
					// temp[1-10]f

					processExtraTemps(data, this);
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
					ProcessUv(data, this, recDate);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in UV data - " + ex.Message);
				}


				// === Soil Temp ===
				try
				{
					// soiltemp[1-10]
					ProcessSoilTemps(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil temp data - " + ex.Message);
				}


				// === Soil Moisture ===
				try
				{
					// soilhum[1-10]
					ProcessSoilMoist(data, this);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil moisture data - " + ex.Message);
				}


				// === Air Quality ===
				try
				{
					// pm25 - [int, µg/m^3]
					// pm25_24h - [float, µg/m^3]
					// pm25_in - [int, µg/m^3]
					// pm25_in_24h - [float, µg/m^3]
					// pm10_in - [int, µg/m^3]
					// pm10_in_24h - [float, µg/m^3]
					// pm_in_temp - [float, F]
					// pm_in_humidity - [int, %]

					ProcessAirQuality(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Air Quality data - " + ex.Message);
				}


				// === CO₂ ===
				try
				{
					// co2 - [int, ppm]
					// co2_in - [int, ppm]
					// co2_in_24h - [float, ppm]

					ProcessCo2(data, this);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in CO₂ data - " + ex.Message);
				}


				// === Lightning ===
				try
				{
					// lightning_day - [int, count]
					// lightning_time - [int, Unix time]
					// lightning_distance - [float, km]

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
					Low Battery = 0, Normal Batetry = 1
					battout			outdoor sensor array or suite [0 or 1]
					battin			indoor sensor or console [0 or 1]
					batt[1-10]		sensors 1-10 [0 or 1]
					battr[1-10]		relay 10 [0 or 1]
					batt_25			PM2.5 [0 or 1]
					batt_25in		PM2.5 indoor [0 or 1]
					batleak[1-4]	Leak sensors 1-4 [0 or 1]
					batt_lightning	Lighting detector [0 or 1]
					battsm[1-4]		Soil Moisture 1-4 [0 or 1]
					battrain		Rain Gauge [0 or 1]
					*/

					ProcessBatteries(data);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Battery data - " + ex.Message);
				}

				DoForecast(string.Empty, false);

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
			 * Ambient doc: https://help.ambientweather.net/help/advanced/
			 *
			 *	GET Parameters - all fields are URL escaped
			 */

			if (stopping)
			{
				context.Response.StatusCode = 200;
				return "success";
			}

			DateTime recDate;

			try
			{
				// MAC
				// dateutc
				// freq
				// model

				cumulus.LogDebugMessage($"ProcessData: Processing query - {context.Request.RawUrl}");

				var data = context.Request.QueryString;

				// We will ignore the dateutc field other than for reporting, this is "live" data so just use "now" to avoid any clock issues

				var dat = data["dateutc"];

				if (dat == null)
				{
					cumulus.LogMessage($"ProcessExtraData: Error, no 'dateutc' parameter found");
					//context.Response.StatusCode = 500;
					//return "{\"result\":\"Failed\",\"Errors\":[\"No 'dateutc' parameter found\"]}";
				}
				else if (dat == "now")
				{
					//recDate = DateTime.Now;
				}
				else
				{
					dat = dat.Replace(' ', 'T') + ".0000000Z";
					cumulus.LogDebugMessage($"ProcessExtraData: Record date = {data["dateutc"]}");
					//recDate = DateTime.ParseExact(dat, "o", CultureInfo.InvariantCulture);
				}

				recDate = DateTime.Now;

				cumulus.LogDebugMessage($"ProcessExtraData: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");


				// === Extra Temperature ===
				try
				{
					// temp[1-10]f

					if (cumulus.AmbientExtraUseTempHum)
					{
						ProcessExtraTemps(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in extra temperature data - " + ex.Message);
				}


				// === Extra Humidity ===
				try
				{
					// humidity[1-10]

					if (cumulus.AmbientExtraUseTempHum)
					{
						ProcessExtraHumidity(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in extra humidity data - " + ex.Message);
				}


				// === Solar ===
				try
				{
					// solarradiation

					if (cumulus.AmbientExtraUseSolar)
					{
						ProcessSolar(data, station, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in solar data - " + ex.Message);
				}


				// === UV ===
				try
				{
					// uv

					if (cumulus.AmbientExtraUseUv)
					{
						ProcessUv(data, recDate);
					}
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

					if (cumulus.AmbienttExtraUseSoilTemp)
					{
						ProcessSoilTemps(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Soil temp data - " + ex.Message);
				}


				// === Soil Moisture ===
				try
				{
					// soilmoisture[1-16]

					if (cumulus.AmbienttExtraUseSoilMoist)
					{
						ProcessSoilMoist(data, station);
					}
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

					if (cumulus.AmbientExtraUseLeafWet)
					{
						ProcessLeafWetness(data, station);
					}
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

					if (cumulus.AmbientExtraUseAQI)
					{
						ProcessAirQuality(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Air Quality data - " + ex.Message);
				}

				// === CO₂ ===
				try
				{
					// tf_co2
					// humi_co2
					// pm25_co2
					// pm25_24_co2
					// pm10_co2
					// pm10_24h_co2
					// co2
					// co2_24

					if (cumulus.AmbientExtraUseCo2)
					{
						ProcessCo2(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in CO₂ data - " + ex.Message);
				}


				// === Lightning ===
				try
				{
					// lightning
					// lightning_time
					// lightning_num

					if (cumulus.AmbientExtraUseLightning)
					{
						ProcessLightning(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Lightning data - " + ex.Message);
				}


				// === Leak ===
				try
				{
					// leak[1 - 4]

					if (cumulus.AmbientExtraUseLeak)
					{
						ProcessLeak(data, station);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Leak data - " + ex.Message);
				}


				// === Batteries ===
				try
				{
					/*
					Low Battery = 0, Normal Batetry = 1
					battout			outdoor sensor array or suite [0 or 1]
					battin			indoor sensor or console [0 or 1]
					batt[1-10]		sensors 1-10 [0 or 1]
					battr[1-10]		relay 10 [0 or 1]
					batt_25			PM2.5 [0 or 1]
					batt_25in		PM2.5 indoor [0 or 1]
					batleak[1-4]	Leak sensors 1-4 [0 or 1]
					batt_lightning	Lighting detector [0 or 1]
					battsm[1-4]		Soil Moisture 1-4 [0 or 1]
					battrain		Rain Gauge [0 or 1]
					*/

					ProcessBatteries(data);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessExtraData: Error in Battery data - " + ex.Message);
				}


				// === Extra Dew point ===
				try
				{
					ProcessExtraDewPoint(data, station);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error calculating extra sensor dew points - " + ex.Message);
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
					station.DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(data["temp" + i + "f"], CultureInfo.InvariantCulture)), i);
				}
			}
		}

		private void ProcessExtraHumidity(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["humidity" + i] != null)
				{
					station.DoExtraHum(Convert.ToDouble(data["humidity" + i], CultureInfo.InvariantCulture), i);
				}
			}
		}

		private void ProcessSolar(NameValueCollection data, WeatherStation station, DateTime recDate)
		{
			if (data["solarradiation"] != null)
			{
				station.DoSolarRad((int)Convert.ToDouble(data["solarradiation"], CultureInfo.InvariantCulture), recDate);
			}
		}

		private void ProcessUv(NameValueCollection data, WeatherStation station, DateTime recDate)
		{
			if (data["uv"] != null)
			{
				station.DoUV(Convert.ToDouble(data["uv"], CultureInfo.InvariantCulture), recDate);
			}
		}

		private void ProcessSoilTemps(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["soiltemp" + i] != null)
				{
					station.DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(data["soiltemp" + i], CultureInfo.InvariantCulture)), i - 1);
				}
			}
		}

		private void ProcessSoilMoist(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["soilhum" + i] != null)
				{
					station.DoSoilMoisture(Convert.ToDouble(data["soilhum" + i], CultureInfo.InvariantCulture), i);
				}
			}
		}

		private void ProcessAirQuality(NameValueCollection data, WeatherStation station)
		{
			// pm25
			// pm25_24h

			var pm = data["pm25"];
			var pmAvg = data["pm25_24h"];
			if (pm != null)
			{
				station.DoAirQuality(Convert.ToDouble(pm, CultureInfo.InvariantCulture), 1);
			}
			if (pmAvg != null)
			{
				station.DoAirQualityAvg(Convert.ToDouble(pmAvg, CultureInfo.InvariantCulture), 1);
			}
		}

		private void ProcessCo2(NameValueCollection data, WeatherStation station)
		{
			// co2 - [int, ppm]
			// co2_in - [int, ppm]
			// co2_in_24h - [float, ppm]

			if (data["co2_in"] != null)
			{
				station.CO2 = Convert.ToInt32(data["co2_in"], CultureInfo.InvariantCulture);
			}
			if (data["co2_in_24"] != null)
			{
				station.CO2_24h = Convert.ToInt32(data["co2_in_24"], CultureInfo.InvariantCulture);
			}
		}

		private void ProcessLightning(NameValueCollection data, WeatherStation station)
		{
			var dist = data["lightning_day"];
			var time = data["lightning_time"];
			var num = data["lightning_distance"];

			if (!string.IsNullOrEmpty(dist) && !string.IsNullOrEmpty(time))
			{
				// Only set the lightning time/distance if it is newer than what we already have - the GW1000 seems to reset this value
				var valDist = Convert.ToDouble(dist, CultureInfo.InvariantCulture);
				if (valDist != 255)
				{
					LightningDistance = ConvertKmtoUserUnits(valDist);
				}

				var valTime = Convert.ToDouble(time, CultureInfo.InvariantCulture);
				// Sends a default value until the first strike is detected of 0xFFFFFFFF
				if (valTime != 0xFFFFFFFF)
				{
					var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					dtDateTime = dtDateTime.AddSeconds(valTime).ToLocalTime();

					if (dtDateTime > LightningTime)
					{
						LightningTime = dtDateTime;
					}
				}
			}

			if (!string.IsNullOrEmpty(num))
			{
				station.LightningStrikesToday = Convert.ToInt32(num, CultureInfo.InvariantCulture);
			}
		}

		private void ProcessLeak(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 4; i++)
			{
				if (data["leak" + i] != null)
				{
					station.DoLeakSensor(Convert.ToInt32(data["leak" + i], CultureInfo.InvariantCulture), i);
				}
			}
		}

		private void ProcessBatteries(NameValueCollection data)
		{
			/*
			Low Battery = 0, Normal Batetry = 1
			battout			outdoor sensor array or suite [0 or 1]
			battin			indoor sensor or console [0 or 1]
			batt[1-10]		sensors 1-10 [0 or 1]
			battr[1-10]		relay 10 [0 or 1]
			batt_25			PM2.5 [0 or 1]
			batt_25in		PM2.5 indoor [0 or 1]
			batleak[1-4]	Leak sensors 1-4 [0 or 1]
			batt_lightning	Lighting detector [0 or 1]
			battsm[1-4]		Soil Moisture 1-4 [0 or 1]
			battrain		Rain Gauge [0 or 1]
			*/

			var lowBatt = false;
			lowBatt = lowBatt || (data["battout"] != null && data["battout"] == "0");
			lowBatt = lowBatt || (data["battin"] != null && data["battin"] == "0");
			lowBatt = lowBatt || (data["batt_25"] != null && data["batt_25"] == "0");
			lowBatt = lowBatt || (data["batt_25in"] != null && data["batt_25in"] == "0");
			lowBatt = lowBatt || (data["batt_lightning"] != null && data["batt_lightning"] == "0");
			lowBatt = lowBatt || (data["battrain"] != null && data["battrain"] == "0");
			for (var i = 1; i <= 4; i++)
			{
				lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "0");
				lowBatt = lowBatt || (data["batleak" + i] != null && data["batleak" + i] == "0");
				lowBatt = lowBatt || (data["battsm" + i] != null && data["battsm" + i] == "0");
			}
			for (var i = 5; i <= 10; i++)
			{
				lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "0");
				lowBatt = lowBatt || (data["battr" + i] != null && data["battr" + i] == "0");
			}

			cumulus.BatteryLowAlarm.Triggered = lowBatt;
		}
	}
}
