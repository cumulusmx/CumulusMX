using System;
using Unosquare.Labs.EmbedIO;
using System.IO;
using System.Web;
using System.Globalization;

namespace CumulusMX
{
	class HttpStationEcowitt : WeatherStation
	{
		public HttpStationEcowitt(Cumulus cumulus) : base(cumulus)
		{
			cumulus.LogMessage("Starting HTTP Station");

			//cumulus.StationOptions.CalculatedWC = true;
			// GW1000 does not provide average wind speeds
			cumulus.StationOptions.UseWind10MinAve = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;
			// GW1000 does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

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
			PASSKEY					ignore

			dateutc

			tempf
			tempinf
			dewptf
			windchillf

			humidity
			humidityin

			winddir
			windgustmph
			windspeedmph
			windspdmph_avg2m
			windgustmph_10m
			maxdailygust

			rainin
			dailyrainin
			weeklyrainin
			monthlyrainin
			yearlyrainin
			totalrainin
			rainratein
			eventrainin

			baromabsin
			baromrelin

			### extra sensors ###

			temp[1-8]f
			humidity[1-8]

			solarradiation
			uv

			soiltempf
			soiltemp[2-16]f
			soilmoisture[1-16]

			leafwetness
			leafwetness[2-8]

			leak[1-4]

			### AQ ###

			AqNO
			AqNO2
			AqNO2T
			AqNO2Y
			AqNOX
			AqNOY
			AqNO3
			AqSO4
			AqSO2
			AqSO2T
			AqCO
			AqCOT
			AqEC
			AqOC
			AqBC
			AqUV-AETH
			AqPM2.5
			AqPM10
			AqOZONE
			pm25_ch[1-4]
			pm25_avg_24h_ch[1-4]

			### Lightning ###

			lightning
			lightning_time
			lightning_num

			### Battery ###
			 - see below -

			### Misc ###

			freq
			model

			 */

			DateTime recDate;

			try
			{
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

				// Wind
				try
				{
					var gust = data["windgustmph"];
					var dir = data["winddir"];
					var avg = data["windspdmph_avg2m"];


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

				// Humidity
				try
				{
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

				// Pressure
				try
				{
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

				// Indoor temp
				try
				{
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

				// Outdoor temp
				try
				{
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

				// Rain
				try
				{
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

				// Dewpoint
				try
				{
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

				// Wind Chill
				try
				{
					if (cumulus.StationOptions.CalculatedWC && data["tempf"] != null && data["windspdmph_avg2m"] != null)
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

				// Apparent
				if (data["tempf"] != null && data["windspdmph_avg2m"] != null && data["humidity"] != null)
				{
					DoApparentTemp(recDate);
					DoFeelsLike(recDate);
				}

				// Humidex
				if (data["tempf"] != null && data["humidity"] != null)
				{
					DoHumidex(recDate);
				}

				DoForecast(string.Empty, false);

				// Temperature
				try
				{
					for (var i = 1; i <= 8; i++)
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

				// Humidity
				try
				{
					for (var i = 1; i <= 8; i++)
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

				// Solar
				try
				{
					var str = data["solarradiation"];
					if (str != null)
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

				// Soil Temp
				try
				{
					var str1 = data["soiltempf"];
					if (str1 != null)
					{
						DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(str1)), 1);
					}

					for (var i = 2; i <= 16; i++)
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

				// Soil Mositure
				try
				{
					for (var i = 1; i <= 16; i++)
					{
						var str = data["soilmoisture" + i];
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

				// Leaf Wetness
				try
				{
					var str1 = data["leafwetness"];
					if (str1 != null)
					{
						DoLeafWetness(Convert.ToDouble(str1), 1);
					}
					for (var i = 2; i <= 8; i++)
					{
						var str = data["leafwetness" + i];
						if (str != null)
						{
							DoLeafWetness(Convert.ToDouble(str), i - 1);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Leaf wetness data - " + ex.Message);
				}

				// Air Quality
				try
				{
					for (var i = 1; i <= 4; i++)
					{
						var pm = data["pm25_ch" + i];
						var pmAvg = data["pm25_avg_24h_ch" + i];
						if (pm != null)
						{
							DoAirQuality(Convert.ToDouble(pm), i);
						}
						if (pmAvg != null)
						{
							DoAirQualityAvg(Convert.ToDouble(pmAvg), i);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Air Quality data - " + ex.Message);
				}

				// Lightning
				try
				{
					var dist = data["lightning"];
					var time = data["lightning_time"];
					var num = data["lightning_num"];

					if (dist != null && time != null)
					{
						// Only set the lightning time/distance if it is newer than what we already have - the GW1000 seems to reset this value
						var valDist = Convert.ToDouble(dist);
						if (valDist != 255)
						{
							LightningDistance = valDist;
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

					if (num != null)
					{
						LightningStrikesToday = Convert.ToInt32(num);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("ProcessData: Error in Lightning data - " + ex.Message);
				}

				// Leak
				try
				{
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

				// Batteries
				/*
				?lowbatt?
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

				var lowBatt = false;
				//lowBatt = lowBatt || (data["lowbatt"] != null && data["lowbatt"] == "1");
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
