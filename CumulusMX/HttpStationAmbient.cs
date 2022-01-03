using System;
using Unosquare.Labs.EmbedIO;
using System.Globalization;
using System.Collections.Specialized;


namespace CumulusMX
{
	class HttpStationAmbient : WeatherStation
	{
		private readonly WeatherStation station;
		private bool starting = true;
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
			//cumulus.StationOptions.UseSpeedForAvgCalc = false;
			// Ambient does not send the rain rate, so we will calculate it
			calculaterainrate = true;
			// Ambient does not send DP, so force MX to calculate it
			//cumulus.StationOptions.CalculatedDP = true;

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
				timerStartNeeded = true;
			}
			else
			{
				cumulus.LogMessage("Starting Extra Sensors - HTTP Station (Ambient)");
			}
			starting = false;
		}

		public override void Stop()
		{
			stopping = true;
			if (station == null)
			{
				StopMinuteTimer();
			}
		}

		public string ProcessData(IHttpContext context, bool main)
		{
			/*
			 * Ambient doc: https://help.ambientweather.net/help/advanced/
			 *
			 *	GET Parameters - all fields are URL escaped
			 *	PASSKEY=123&stationtype=EasyWeatherV1.6.0&dateutc=2021-08-01+12:21:31&tempinf=76.3&humidityin=51&baromrelin=29.929&baromabsin=28.866&tempf=72.5&humidity=47&winddir=12&winddir_avg10m=97&windspeedmph=2.2&windspdmph_avg10m=4.7&windgustmph=13.2&maxdailygust=15.0&rainratein=0.000&eventrainin=0.000&hourlyrainin=0.000&dailyrainin=0.000&weeklyrainin=0.000&monthlyrainin=0.000&yearlyrainin=29.953&solarradiation=855.09&uv=8&temp1f=77.5&humidity1=42&temp2f=76.8&humidity2=57&temp3f=72.3&humidity3=47&temp4f=72.3&humidity4=49&temp5f=-3.6&temp6f=87.4&humidity6=46&temp7f=43.9&humidity7=39&soilhum1=61&soilhum2=68&soilhum3=88&soilhum4=46&soilhum5=55&pm25=14.0&pm25_24h=14.2&pm25_in=5.0&pm25_in_24h=9.0&lightning_day=0&lightning_time=1627320749&lightning_distance=17&pm_in_temp_aqin=77.0&pm_in_humidity_aqin=47&pm10_in_aqin=1.4&pm10_in_24h_aqin=3.0&pm25_in_aqin=1.1&pm25_in_24h_aqin=2.6&co2_in_aqin=336&co2_in_24h_aqin=529&battout=1&wh80batt=3.06&wh25batt=0&wh26batt=0&batt1=1&batt2=1&batt3=1&batt4=1&batt5=1&batt6=1&batt7=1&battsm1=1&battsm2=1&battsm3=1&battsm4=1&battsm5=1&batt_25=1&batt_25in=1&batt_lightning=4&batt_co2=1&freq=868M&model=HP1000SE-PRO_Pro_V1.7.4&dewptf=51.1&windchillf=72.5&feelslikef=72.5&heatindexf=71.7&pm25_AQI_ch1=55&pm25_AQIlvl_ch1=2&pm25_AQI_avg_24h_ch1=55&pm25_AQIlvl_avg_24h_ch1=2&pm25_AQI_ch2=21&pm25_AQIlvl_ch2=1&pm25_AQI_avg_24h_ch2=38&pm25_AQIlvl_avg_24h_ch2=1&co2lvl=1&pm25_AQI_co2=5&pm25_AQIlvl_co2=1&pm25_AQI_24h_co2=11&pm25_AQIlvl_24h_co2=1&pm10_AQI_co2=1&pm10_AQIlvl_co2=1&pm10_AQI_24h_co2=3&pm10_AQIlvl_24h_co2=1&windgustmph_max10m=13.2&brightness=108339.9&sunhours=5.48&ptrend1=-1&pchange1=-0.0177&ptrend3=1&pchange3=0.003
			 *
			 */

			DateTime recDate;

			var procName = main ? "ProcessData" : "ProcessExtraData";
			var thisStation = main ? this : station;

			if (starting || stopping)
			{
				context.Response.StatusCode = 200;
				return "success";
			}


			try
			{
				// MAC
				// dateutc
				// freq
				// model

				cumulus.LogDebugMessage($"{procName}: Processing query - {context.Request.RawUrl}");


				var data = context.Request.QueryString;

				// We will ignore the dateutc field, this is "live" data so just use "now" to avoid any clock issues
				recDate = DateTime.Now;

				cumulus.LogDebugMessage($"{procName}: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");

				if (main)
				{
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
						// totalrainin ??? MISSING ???

						var rain = data["yearlyrainin"];
						//var rRate = data["hourlyrainin"]; // no rain rate, have to use the hourly rain

						if (rain == null)
						{
							cumulus.LogMessage($"ProcessData: Error, missing rainfall");
						}
						else
						{
							var rainVal = ConvertRainINToUser(Convert.ToDouble(rain, CultureInfo.InvariantCulture));
							//var rateVal = ConvertRainINToUser(Convert.ToDouble(rRate, CultureInfo.InvariantCulture));
							DoRain(rainVal, 0, recDate);
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
						var dewpnt = data["dewptf"];

						if (cumulus.StationOptions.CalculatedDP)
						{
							DoOutdoorDewpoint(0, recDate);
						}
						else if (dewpnt == null)
						{
							cumulus.LogMessage($"ProcessData: Error, missing dew point");
						}
						else
						{
							var val = ConvertTempFToUser(Convert.ToDouble(dewpnt, CultureInfo.InvariantCulture));
							DoOutdoorDewpoint(val, recDate);
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
				}


				// === Extra Temperature ===
				if (main || cumulus.AmbientExtraUseTempHum)
				{
					try
					{
						// temp[1-10]f
						ProcessExtraTemps(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in extra temperature data - " + ex.Message);
					}
				}


				// === Extra Humidity ===
				if (main || cumulus.AmbientExtraUseTempHum)
				{
					try
					{
						// humidity[1-10]
						ProcessExtraHumidity(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in extra humidity data - " + ex.Message);
					}
				}


				// === Solar ===
				if (main || cumulus.AmbientExtraUseSolar)
				{
					try
					{
						// solarradiation
						ProcessSolar(data, thisStation, recDate);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in solar data - " + ex.Message);
					}
				}


				// === UV ===
				if (main || cumulus.AmbientExtraUseUv)
				{
					try
					{
						// uv
						ProcessUv(data, thisStation, recDate);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in UV data - " + ex.Message);
					}
				}


				// === Soil Temp ===
				if (main || cumulus.AmbientExtraUseSoilTemp)
				{
					try
					{
						// soiltemp[1-10]
						ProcessSoilTemps(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Soil temp data - " + ex.Message);
					}
				}


				// === Soil Moisture ===
				if (main || cumulus.AmbientExtraUseSoilMoist)
				{
					try
					{
						// soilhum[1-10]
						ProcessSoilMoist(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Soil moisture data - " + ex.Message);
					}
				}


				// === Air Quality ===
				if (main || cumulus.AmbientExtraUseAQI)
				{
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

						ProcessAirQuality(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Air Quality data - " + ex.Message);
					}
				}


				// === CO₂ ===
				if (main || cumulus.AmbientExtraUseCo2)
				{
					try
					{
						// co2 - [int, ppm]
						// co2_in - [int, ppm]
						// co2_in_24h - [float, ppm]

						// NOT YET IMPLEMENTED
						//ProcessCo2(data, this);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in CO₂ data - " + ex.Message);
					}
				}


				// === Lightning ===
				if (main || cumulus.AmbientExtraUseLightning)
				{
					try
					{
						// lightning_day - [int, count]
						// lightning_time - [int, Unix time]
						// lightning_distance - [float, km]

						ProcessLightning(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Lightning data - " + ex.Message);
					}
				}


				// === Leak ===
				if (main || cumulus.AmbientExtraUseLeak)
				{
					try
					{
						// leak[1 - 4]
						ProcessLeak(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Leak data - " + ex.Message);
					}
				}


				// === Batteries ===
				try
				{
					/*
					Low Battery = 0, Normal Battery = 1
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
					cumulus.LogMessage($"{procName}: Error in Battery data - " + ex.Message);
				}


				// === Extra Dew point ===
				if (main || cumulus.AmbientExtraUseTempHum)
				{
					try
					{
						ProcessExtraDewPoint(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error calculating extra sensor dew points - " + ex.Message);
					}
				}


				DoForecast(string.Empty, false);

				UpdateStatusPanel(recDate);
				UpdateMQTT();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"{procName}: Error - " + ex.Message);
				context.Response.StatusCode = 500;
				return "Failed: General error - " + ex.Message;
			}

			cumulus.LogDebugMessage($"{procName}: Complete");

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

			// From FOSKplugin
			// pm25_AQIlvl_ch[1-4]
			// pm25_AQIlvl_avg_24h_ch1

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

			/*
			if (data["co2_in"] != null)
			{
				station.CO2 = Convert.ToInt32(data["co2_in"], CultureInfo.InvariantCulture);
			}
			if (data["co2_in_24"] != null)
			{
				station.CO2_24h = Convert.ToInt32(data["co2_in_24"], CultureInfo.InvariantCulture);
			}
			*/

			// From FOSKplugin
			// co2lvl
			// pm25_AQIlvl_co2
			// pm25_AQIlvl_24h_co2
			// pm10_AQIlvl_co2
			// pm10_AQIlvl_24h_co2


			if (data["co2lvl"] != null)
			{
				station.CO2 = Convert.ToInt32(data["co2lvl"], CultureInfo.InvariantCulture);
			}
			if (data["pm25_AQIlvl_co2"] != null)
			{
				station.CO2_pm2p5 = Convert.ToDouble(data["pm25_AQIlvl_co2"], CultureInfo.InvariantCulture);
			}
			if (data["pm25_AQIlvl_24h_co2"] != null)
			{
				station.CO2_pm2p5_24h = Convert.ToDouble(data["pm25_AQIlvl_24h_co2"], CultureInfo.InvariantCulture);
			}
			if (data["pm10_AQIlvl_co2"] != null)
			{
				station.CO2_pm10 = Convert.ToDouble(data["pm10_AQIlvl_co2"], CultureInfo.InvariantCulture);
			}
			if (data["pm10_AQIlvl_24h_co2"] != null)
			{
				station.CO2_pm10_24h = Convert.ToDouble(data["pm10_AQIlvl_24h_co2"], CultureInfo.InvariantCulture);
			}
		}

		private void ProcessLightning(NameValueCollection data, WeatherStation station)
		{
			var num = data["lightning_day"];
			var time = data["lightning_time"];
			var dist = data["lightning_distance"];

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
			Low Battery = 0, Normal Battery = 1
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

		private void ProcessExtraDewPoint(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["temp" + i + "f"] != null && data["humidity" + i] != null)
				{
					var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[i]), station.ExtraHum[i]);
					station.ExtraDewPoint[i] = ConvertTempCToUser(dp);
				}
			}
		}

	}
}
