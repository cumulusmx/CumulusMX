using System;
using Unosquare.Labs.EmbedIO;
using System.IO;
using System.Web;
using System.Globalization;

namespace CumulusMX
{
	class HttpStationAmbient : WeatherStation
	{
		public HttpStationAmbient(Cumulus cumulus) : base(cumulus)
		{
			cumulus.LogMessage("Starting HTTP Station (Ambient)");

			//cumulus.StationOptions.CalculatedWC = true;
			// Ambient does not provide average wind speeds
			cumulus.StationOptions.UseWind10MinAve = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;
			// Ambient does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

			cumulus.Manufacturer = cumulus.AMBIENT;
			cumulus.AirQualityUnitText = "µg/m³";
			cumulus.SoilMoistureUnitText = "%";

			Start();
		}

		public override void Start()
		{
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			timerStartNeeded = true;
		}

		public override void Stop()
		{
			StopMinuteTimer();
		}

		public string ProcessData(IHttpContext context)
		{
			/*
			 * Ambient doc: https://help.ambientweather.net/help/advanced/
			 *
			 *	GET Parameters - all fields are URL escaped
			 * - Uses HTTPS ? That sounds like a non-starter?
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

					for (var i = 1; i <= 10; i++)
					{
						var str = data["temp" + i + "f"];
						if (str != null)
						{
							DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(str)), i);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in extra temperature data - " + ex.Message);
				}


				// === Extra Humidity ===
				try
				{
					// humidity[1-10]

					for (var i = 1; i <= 10; i++)
					{
						var str = data["humidity" + i];
						if (str != null)
						{
							DoExtraHum(Convert.ToDouble(str), i);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in extra humidity data - " + ex.Message);
				}


				// === Solar ===
				try
				{
					// solarradiation

					var str = data["solarradiation"];
					if (str != null)
					{
						DoSolarRad((int)Convert.ToDouble(str), recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in solar data - " + ex.Message);
				}


				// === UV ===
				try
				{
					// uv

					var str = data["uv"];
					if (str != null)
					{
						DoUV(Convert.ToDouble(str), recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in UV data - " + ex.Message);
				}


				// === Soil Temp ===
				try
				{
					// soiltemp[1-10]f

					for (var i = 1; i <= 10; i++)
					{
						var str = data["soiltemp" + i + "f"];
						if (str != null)
						{
							DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str)), i - 1);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil temp data - " + ex.Message);
				}


				// === Soil Moisture ===
				try
				{
					// soilhum[1-10]

					for (var i = 1; i <= 10; i++)
					{
						var str = data["soilhum" + i];
						if (str != null)
						{
							DoSoilMoisture(Convert.ToDouble(str), i);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil moisture data - " + ex.Message);
				}


				// === Air Quality ===
				try
				{
					// co2 - [int, ppm]
					// pm25 - [int, µg/m^3]
					// pm25_24h - [float, µg/m^3]
					// pm25_in - [int, µg/m^3]
					// pm25_in_24h - [float, µg/m^3]
					// pm10_in - [int, µg/m^3]
					// pm10_in_24h - [float, µg/m^3]
					// co2_in - [int, ppm]
					// co2_in_24h - [float, ppm]
					// pm_in_temp - [float, F]
					// pm_in_humidity - [int, %]

					var pm = data["pm25"];
					var pmAvg = data["pm25_24h"];
					if (pm != null)
					{
						DoAirQuality(Convert.ToDouble(pm), 1);
					}
					if (pmAvg != null)
					{
						DoAirQualityAvg(Convert.ToDouble(pmAvg), 1);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Air Quality data - " + ex.Message);
				}

				// === Lightning ===
				try
				{
					// lightning_day - [int, count]
					// lightning_time - [int, Unix time]
					// lightning_distance - [float, km]

					var dist = data["lightning_day"];
					var time = data["lightning_time"];
					var num = data["lightning_distance"];

					if (!string.IsNullOrEmpty(dist) && !string.IsNullOrEmpty(time))
					{
						// Only set the lightning time/distance if it is newer than what we already have - the GW1000 seems to reset this value
						var valDist = Convert.ToDouble(dist);
						if (valDist != 255)
						{
							LightningDistance =  ConvertKmtoUserUnits(valDist);
						}

						var valTime = Convert.ToDouble(time);
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
						LightningStrikesToday = Convert.ToInt32(num);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Lightning data - " + ex.Message);
				}

				// === Leak ===
				try
				{
					// leak[1 - 4]

					for (var i = 1; i <= 4; i++)
					{
						var str = data["leak" + i];
						if (str != null)
						{
							DoLeakSensor(Convert.ToInt32(str), i);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Leak data - " + ex.Message);
				}

				// === Batteries ===
				try
				{
					/*
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
					lowBatt = lowBatt || (data["battout"] != null && data["battout"] == "1");
					lowBatt = lowBatt || (data["battin"] != null && data["battin"] == "1");
					lowBatt = lowBatt || (data["batt_25"] != null && data["batt_25"] == "1");
					lowBatt = lowBatt || (data["batt_25in"] != null && data["batt_25in"] == "1");
					lowBatt = lowBatt || (data["batt_lightning"] != null && data["batt_lightning"] == "1");
					lowBatt = lowBatt || (data["battrain"] != null && data["battrain"] == "1");
					for (var i = 1; i <= 4; i++)
					{
						lowBatt = lowBatt || (data["batleak" + i] != null && data["batleak" + i] == "1");
						lowBatt = lowBatt || (data["battsm" + i] != null && data["battsm" + i] == "1");
					}
					for (var i = 5; i <= 10; i++)
					{
						lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "1");
						lowBatt = lowBatt || (data["battr" + i] != null && data["battr" + i] == "1");
					}

					cumulus.BatteryLowAlarm.Triggered = lowBatt;
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
	}
}
