using System;
using Unosquare.Labs.EmbedIO;
using System.Globalization;

namespace CumulusMX
{
	class HttpStationWund : WeatherStation
	{
		private double previousRainCount = -1;
		private double rainCount = 0;

		public HttpStationWund(Cumulus cumulus) : base(cumulus)
		{
			cumulus.LogMessage("Starting HTTP Station");

			cumulus.StationOptions.CalculatedWC = true;
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
			GET Parameters - all fields are URL escaped
			===========================================
			ID					= ignored
			PASSWORD			= ignored
			dateutc				= "YYYY-MM-DD HH:mm:SS" or "now"
			action				= "updateraw"
			winddir				- [0-360 instantaneous wind direction]
			windspeedmph		- [mph instantaneous wind speed]
			windgustmph			- [mph current wind gust, using software specific time period]
			windgustdir			- [0-360 using software specific time period]
			windspdmph_avg2m	- [mph 2 minute average wind speed mph]
			winddir_avg2m		- [0-360 2 minute average wind direction]
			windgustmph_10m		- [mph past 10 minutes wind gust mph ]
			windgustdir_10m		- [0-360 past 10 minutes wind gust direction]
			humidity			- [% outdoor humidity 0-100%]
			dewptf				- [F outdoor dewpoint F]
			tempf				- [F outdoor temperature]
				* for extra outdoor sensors use temp2f, temp3f, and so on
			rainin				- [rain inches over the past hour)] -- the accumulated rainfall in the past 60 min
			dailyrainin			- [rain inches so far today in local time]
			baromin				- [barometric pressure inches]
			weather				- ignored
			clouds				- ignored
			soiltempf			- [F soil temperature]
				* for sensors 2,3,4 use soiltemp2f, soiltemp3f, and soiltemp4f
			soilmoisture		- [%]
				* for sensors 2,3,4 use soilmoisture2, soilmoisture3, and soilmoisture4
			leafwetness			- [%]
			leafwetness2
			solarradiation		- [W/m^2]
			UV					- [index]
			visibility			- ignored
			indoortempf			- [F indoor temperature F]
			indoorhumidity		- [% indoor humidity 0-100]

			AqPM2.5				- PM2.5 mass - UG/M3
			AqPM10				- PM10 mass - PM10 mass

			softwaretype		- ignored
			 */

			DateTime recDate;

			try
			{
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
					dat = dat.Replace(' ', 'T') + ".00000Z";
					recDate = DateTime.ParseExact(dat, "u", CultureInfo.InvariantCulture);
				}

				// Wind
				try
				{
					var gust = data["windgustmph"];
					var dir = data["winddir"];
					var avg = data["windspdmph"];

					if (gust == null || dir == null || avg == null ||
						gust == "-9999" || dir == "-9999" || avg == "-9999")
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

				// Humidity
				try
				{
					var humIn = data["indoorhumidity"];
					var humOut = data["humidity"];

					if (humIn == null || humIn == "-9999")
					{
						cumulus.LogMessage($"ProcessData: Error, missing indoor humidity");
					}
					else
					{
						var humVal = Convert.ToInt32(humIn);
						DoIndoorHumidity(humVal);
					}

					if (humOut == null || humOut == "-9999")
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

				// Pressure
				try
				{
					var press = data["baromin"];
					if (press == null || press == "-9999")
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

				// Indoor temp
				try
				{
					var temp = data["indoortempf"];
					if (temp == null || temp == "-9999")
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

				// Outdoor temp
				try
				{
					var temp = data["tempf"];
					if (temp == null || temp == "-9999")
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

				// Rain
				try
				{
					var rain = data["dailyrainin"];
					var rHour = data["rainin"]; //- User rain in last hour as rainfall rate

					if (rain == null || rHour == null || rain == "-9999" || rHour == "-9999")
					{
						cumulus.LogMessage($"ProcessData: Error, missing rainfall");
					}
					else
					{
						var rainVal = ConvertRainINToUser(Convert.ToDouble(rain));
						var rateVal = ConvertRainINToUser(Convert.ToDouble(rHour));

						if (rainVal < previousRainCount)
						{
							// rain counter has reset
							rainCount += previousRainCount;
							previousRainCount = rainVal;
						}

						DoRain(rainCount + rainVal, rateVal, recDate);

					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Rain data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in rainfall data - " + ex.Message;
				}

				// Dewpoint
				try
				{
					var dewpnt = data["dewptf"];
					if (dewpnt == null || dewpnt == "-9999")
					{
						cumulus.LogMessage($"ProcessData: Error, missing dew point");
					}
					else
					{
						var dpVal = ConvertTempFToUser(Convert.ToDouble(dewpnt));
						DoOutdoorDewpoint(dpVal, recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Dew point data - " + ex.Message);
					context.Response.StatusCode = 500;
					return "Failed: Error in dew point data - " + ex.Message;
				}

				// Wind Chill - no w/c in wunderground data, so it must be set to CMX calculated
				if (data["windspdmph"] != null && data["tempf"] != null && data["windspdmph"] != "-9999" && data["tempf"] != "-9999")
				{
					DoWindChill(0, recDate);

					if (data["humidity"] != null && data["humidity"] != "-9999")
					{
						DoApparentTemp(recDate);
						DoFeelsLike(recDate);
					}
				}

				if (data["tempf"] != null && data["humidity"] != null && data["tempf"] != "-9999" && data["humidity"] != "-9999")
				{
					DoHumidex(recDate);
				}

				DoForecast(string.Empty, false);

				// Extra Temperature
				try
				{
					for (var i = 2; i < 5; i++)
					{
						var str = data["temp" + i + "f"];
						if (str != null && str != "-9999")
						{
							DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(str)), i - 1);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in extra temperature data - " + ex.Message);
				}

				// Solar
				try
				{
					var str = data["solarradiation"];
					if (str != null && str != "-9999")
					{
						DoSolarRad(Convert.ToInt32(str), recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in solar data - " + ex.Message);
				}

				// UV
				try
				{
					var str = data["UV"];
					if (str != null && str != "-9999")
					{
						DoUV(Convert.ToDouble(str), recDate);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in UV data - " + ex.Message);
				}

				// Soil Temp
				try
				{
					var str1 = data["soiltempf"];
					if (str1 != null && str1 != "-9999")
					{
						DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str1)), 1);
					}
					var str2 = data["soiltemp2f"];
					if (str2 != null && str2 != "-9999")
					{
						DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str2)), 2);
					}
					var str3 = data["soiltemp3f"];
					if (str3 != null && str3 != "-9999")
					{
						DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str3)), 3);
					}
					var str4 = data["soiltemp4f"];
					if (str4 != null && str4 != "-9999")
					{
						DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str4)), 4);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil temp data - " + ex.Message);
				}

				// Soil Mositure
				try
				{
					var str1 = data["soilmoisture"];
					if (str1 != null && str1 != "-9999")
					{
						DoSoilMoisture(Convert.ToDouble(str1), 1);
					}
					var str2 = data["soilmoisture2"];
					if (str2 != null && str2 != "-9999")
					{
						DoSoilMoisture(Convert.ToDouble(str2), 2);
					}
					var str3 = data["soilmoisture3"];
					if (str3 != null && str3 != "-9999")
					{
						DoSoilMoisture(Convert.ToDouble(str3), 3);
					}
					var str4 = data["soilmoisture4"];
					if (str4 != null && str4 != "-9999")
					{
						DoSoilMoisture(Convert.ToDouble(str4), 4);
					}

				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Soil moisture data - " + ex.Message);
				}

				// Leaf Wetness
				try
				{
					var str1 = data["leafwetness"];
					if (str1 != null && str1 != "-9999")
					{
						DoLeafWetness(Convert.ToDouble(str1), 1);
					}
					var str2 = data["leafwetness2"];
					if (str2 != null && str2 != "-9999")
					{
						DoLeafWetness(Convert.ToDouble(str2), 2);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Leaf wetness data - " + ex.Message);
				}

				// Air Quality
				try
				{
					var str2 = data["AqPM2.5"];
					if (str2 != null && str2 != "-9999")
					{
						CO2_pm2p5 = Convert.ToDouble(str2);
					}
					var str10 = data["AqPM10"];
					if (str10 != null && str10 != "-9999")
					{
						CO2_pm10 = Convert.ToDouble(str10);
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
