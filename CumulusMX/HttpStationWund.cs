using System;
using Unosquare.Labs.EmbedIO;
using System.Globalization;

namespace CumulusMX
{
	class HttpStationWund : WeatherStation
	{
		private bool starting = true;
		private bool stopping = false;
		private double previousRainCount = -1;
		private double rainCount = 0;

		public HttpStationWund(Cumulus cumulus) : base(cumulus)
		{
			cumulus.LogMessage("Starting HTTP Station (Wunderground)");

			cumulus.StationOptions.CalculatedWC = true;
			cumulus.Units.AirQualityUnitText = "µg/m³";
			cumulus.Units.SoilMoistureUnitText = "%";
			cumulus.Units.LeafWetnessUnitText = "%";

			// Wunderground does not send the rain rate, so we will calculate it
			calculaterainrate = true;

			Start();
		}

		public override void Start()
		{
			DoDayResetIfNeeded();
			timerStartNeeded = true;
			starting = false;
		}

		public override void Stop()
		{
			stopping = true;
			StopMinuteTimer();
		}

		public string ProcessData(IHttpContext context)
		{
			/*
			GET Parameters - all fields are URL escaped
			===========================================
			ID					- ignored
			PASSWORD			- ignored
			weather				- ignored
			clouds				- ignored
			visibility			- ignored
			action				- ignored
			softwaretype		- ignored
			realtime			- ignored
			rtfreq				- ignored

			ID=ISAARB3&PASSWORD=key&tempf=81.5&humidity=43&dewptf=56.8&windchillf=81.5&winddir=329&windspeedmph=0.00&windgustmph=5.82&rainin=0.000&dailyrainin=0.000&weeklyrainin=0.000&monthlyrainin=4.118&yearlyrainin=29.055&solarradiation=253.20&UV=1&indoortempf=80.6&indoorhumidity=50&baromin=29.943&AqPM2.5=10.0&soilmoisture=51&soilmoisture2=65&soilmoisture3=72&soilmoisture4=36&soilmoisture5=48&lowbatt=0&dateutc=now&softwaretype=GW1000A_V1.6.8&action=updateraw&realtime=1&rtfreq=5

			 */

			DateTime recDate;

			if (starting || stopping)
			{
				context.Response.StatusCode = 200;
				return "success";
			}

			try
			{
				// dateutc = "YYYY-MM-DD HH:mm:SS" or "now"

				cumulus.LogDebugMessage($"ProcessData: Processing query - {context.Request.RawUrl}");

				var data = context.Request.QueryString;

				// We will ignore the dateutc field, this is "live" data so just use "now" to avoid any clock issues
				recDate = DateTime.Now;

				// === Wind ===
				try
				{
					// winddir - [0 - 360 instantaneous wind direction]
					// windspeedmph - [mph instantaneous wind speed]
					// windgustmph - [mph current wind gust, using software specific time period]
					// windgustdir - [0 - 360 using software specific time period]
					// - values below are not always provided
					// windspdmph_avg2m - [mph 2 minute average wind speed mph]
					// winddir_avg2m - [0 - 360 2 minute average wind direction]
					// windgustmph_10m - [mph past 10 minutes wind gust mph]
					// windgustdir_10m - [0 - 360 past 10 minutes wind gust direction]

					var gust = data["windgustmph"];
					var dir = data["winddir"];
					var avg = data["windspeedmph"];

					if (gust == null || dir == null || avg == null ||
						gust == "-9999" || dir == "-9999" || avg == "-9999")
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
					// humidity - [% outdoor humidity 0 - 100 %]
					// indoorhumidity - [% indoor humidity 0 - 100]

					var humIn = data["indoorhumidity"];
					var humOut = data["humidity"];

					if (humIn == null || humIn == "-9999")
					{
						cumulus.LogMessage($"ProcessData: Error, missing indoor humidity");
					}
					else
					{
						var humVal = Convert.ToInt32(humIn, CultureInfo.InvariantCulture);
						DoIndoorHumidity(humVal);
					}

					if (humOut == null || humOut == "-9999")
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
					// baromin - [barometric pressure inches]

					var press = data["baromin"];
					if (press == null || press == "-9999")
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
					// indoortempf - [F indoor temperature F]

					var temp = data["indoortempf"];
					if (temp == null || temp == "-9999")
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
					// tempf - [F outdoor temperature]

					var temp = data["tempf"];
					if (temp == null || temp == "-9999")
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
					// rainin - [rain inches over the past hour)] --the accumulated rainfall in the past 60 min
					// dailyrainin - [rain inches so far today in local time]

					var rain = data["dailyrainin"];

					if (rain == null || rain == "-9999")
					{
						cumulus.LogMessage($"ProcessData: Error, missing rainfall");
					}
					else
					{
						var rainVal = ConvertRainINToUser(Convert.ToDouble(rain, CultureInfo.InvariantCulture));

						if (rainVal < previousRainCount)
						{
							// rain counter has reset
							rainCount += previousRainCount;
							previousRainCount = rainVal;
						}

						DoRain(rainCount + rainVal, 0, recDate);

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
					// dewptf - [F outdoor dewpoint F]

					var dewpnt = data["dewptf"];

					if (cumulus.StationOptions.CalculatedDP)
					{
						DoOutdoorDewpoint(0, recDate);
					}
					else if (dewpnt == null || dewpnt == "-9999")
					{
						cumulus.LogMessage($"ProcessData: Error, missing dew point");
					}
					else
					{
						var dpVal = ConvertTempFToUser(Convert.ToDouble(dewpnt, CultureInfo.InvariantCulture));
						DoOutdoorDewpoint(dpVal, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Dew point data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in dew point data - " + ex.Message;
				}


				// === Wind Chill ===
				// - no w/c in wunderground data, so it must be set to CMX calculated
				if (data["windspeedmph"] != null && data["tempf"] != null && data["windspeedmph"] != "-9999" && data["tempf"] != "-9999")
				{
					DoWindChill(0, recDate);

				// === Apparent/Feels Like ===
					if (data["humidity"] != null && data["humidity"] != "-9999")
					{
						DoApparentTemp(recDate);
						DoFeelsLike(recDate);
					}
					else
					{
						cumulus.LogMessage("ProcessData: Insufficient data to calculate Apparent/Feels like Temps");
					}
				}
				else
				{
					cumulus.LogMessage("ProcessData: Insufficient data to calculate Wind Chill and Apparent/Feels like Temps");
				}


				// === Humidex ===
				// - CMX calculated
				if (data["tempf"] != null && data["humidity"] != null && data["tempf"] != "-9999" && data["humidity"] != "-9999")
				{
					DoHumidex(recDate);
					DoCloudBaseHeatIndex(recDate);
				}
				else
				{
					cumulus.LogMessage("ProcessData: Insufficient data to calculate Humidex");
				}

				DoForecast(string.Empty, false);


				// === Extra Temperature ===
				try
				{
					// temp[2-4]f

					for (var i = 2; i < 5; i++)
					{
						var str = data["temp" + i + "f"];
						if (str != null && str != "-9999")
						{
							DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(str, CultureInfo.InvariantCulture)), i - 1);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in extra temperature data - " + ex.Message);
				}


				// === Solar ===
				try
				{
					// solarradiation - [W/m^2]

					var str = data["solarradiation"];
					if (str != null && str != "-9999")
					{
						DoSolarRad((int)Convert.ToDouble(str, CultureInfo.InvariantCulture), recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in solar data - " + ex.Message);
				}


				// === UV ===
				try
				{
					// UV - [index]

					var str = data["UV"];
					if (str != null && str != "-9999")
					{
						DoUV(Convert.ToDouble(str, CultureInfo.InvariantCulture), recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in UV data - " + ex.Message);
				}


				// === Soil Temp ===
				try
				{
					// soiltempf - [F soil temperature]
					// soiltemp[2-4]f

					var str1 = data["soiltempf"];
					if (str1 != null && str1 != "-9999")
					{
						DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str1, CultureInfo.InvariantCulture)), 1);
					}

					for (var i = 2; i <= 4; i++)
					{
						var str = data["soiltemp" + i + "f"];
						if (str != null && str != "-9999")
						{
							DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str, CultureInfo.InvariantCulture)), i);
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
					// soilmoisture - [%]
					// soilmoisture[2-4]

					var str1 = data["soilmoisture"];
					if (str1 != null && str1 != "-9999")
					{
						DoSoilMoisture(Convert.ToDouble(str1, CultureInfo.InvariantCulture), 1);
					}

					for (var i = 2; i <= 4; i++)
					{
						var str = data["soilmoisture" + i];
						if (str != null && str != "-9999")
						{
							DoSoilMoisture(Convert.ToDouble(str, CultureInfo.InvariantCulture), i);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil moisture data - " + ex.Message);
				}


				// === Leaf Wetness ===
				try
				{
					// leafwetness - [%]
					// leafwetness2

					var str1 = data["leafwetness"];
					if (str1 != null && str1 != "-9999")
					{
						DoLeafWetness(Convert.ToDouble(str1, CultureInfo.InvariantCulture), 1);
					}
					var str2 = data["leafwetness2"];
					if (str2 != null && str2 != "-9999")
					{
						DoLeafWetness(Convert.ToDouble(str2, CultureInfo.InvariantCulture), 2);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Leaf wetness data - " + ex.Message);
				}


				// === Air Quality ===
				try
				{
					// AqPM2.5 - PM2.5 mass - UG / M3
					// AqPM10 - PM10 mass - PM10 mass

					var str2 = data["AqPM2.5"];
					if (str2 != null && str2 != "-9999")
					{
						CO2_pm2p5 = Convert.ToDouble(str2, CultureInfo.InvariantCulture);
					}
					var str10 = data["AqPM10"];
					if (str10 != null && str10 != "-9999")
					{
						CO2_pm10 = Convert.ToDouble(str10, CultureInfo.InvariantCulture);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Air Quality data - " + ex.Message);
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
