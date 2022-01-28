using System;
using Unosquare.Labs.EmbedIO;
using System.IO;
using System.Web;
using System.Globalization;
using System.Collections.Specialized;
using System.Reflection;
using ServiceStack.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{

	class HttpStationEcowitt : WeatherStation
	{
		private readonly WeatherStation station;
		private bool starting = true;
		private bool stopping = false;
		private readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;
		private bool reportStationType = true;
		private EcowittApi api;
		private int maxArchiveRuns = 1;


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

			// Do not set these if we are only using extra sensors
			if (station == null)
			{
				// does not provide 10 min average wind speeds
				cumulus.StationOptions.UseWind10MinAvg = true;

				// does not send DP, so force MX to calculate it
				cumulus.StationOptions.CalculatedDP = true;
				// Same for Wind Chill
				cumulus.StationOptions.CalculatedWC = true;
				// does not provide a forecast, force MX to provide it
				cumulus.UseCumulusForecast = true;
				// does not provide pressure trend strings
				cumulus.StationOptions.UseCumulusPresstrendstr = true;
			}

			if (station == null || (station != null && cumulus.EcowittExtraUseAQI))
			{
				cumulus.Units.AirQualityUnitText = "µg/m³";
			}
			if (station == null || (station != null && cumulus.EcowittExtraUseSoilMoist))
			{
				cumulus.Units.SoilMoistureUnitText = "%";
			}
			if (station == null || (station != null && cumulus.EcowittExtraUseSoilMoist))
			{
				cumulus.Units.LeafWetnessUnitText = "%";
			}



			// Only perform the Start-up if we are a proper station, not a Extra Sensor
			if (station == null)
			{
				Task.Run(getAndProcessHistoryData);
			}
			else
			{
				cumulus.LogMessage("Extra Sensors - HTTP Station (Ecowitt) - Waiting for data...");
			}
		}

		public override void Start()
		{
			if (station == null)
			{
				cumulus.LogMessage("Starting HTTP Station (Ecowitt)");
				DoDayResetIfNeeded();
				cumulus.StartTimersAndSensors();
			}
			else
			{
				cumulus.LogMessage("Starting Extra Sensors - HTTP Station (Ecowitt)");
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

		public override void getAndProcessHistoryData()
		{
			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				Start();
				return;
			}

			int archiveRun = 0;
			cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			cumulus.LogDebugMessage("Lock: Station has the lock");

			try
			{

				api = new EcowittApi(cumulus);

				do
				{
					GetHistoricData();
					archiveRun++;
				} while (archiveRun < maxArchiveRuns);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Exception occurred reading archive data: " + ex.Message);
			}

			cumulus.LogDebugMessage("Lock: Station releasing the lock");
			_ = Cumulus.syncInit.Release();

			Start();
		}

		private void GetHistoricData()
		{
			cumulus.LogMessage("GetHistoricData: Starting Historic Data Process");

			// add one minute to avoid duplicating the last log entry
			var startTime = cumulus.LastUpdateTime.AddMinutes(1);
			var endTime = DateTime.Now;

			// The API call is limited to fetching 24 hours of data
			if ((endTime - startTime).TotalHours > 24.0)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime.AddHours(24);
				maxArchiveRuns++;
			}

			EcowittApi.EcowittHistoricData dat;

			// only fetch the data we are interested in...
			var sb = new StringBuilder("indoor,outdoor,wind,pressure");
			if (cumulus.EcowittExtraUseSolar || cumulus.EcowittExtraUseUv)
				sb.Append(",solar_and_uvi");
			if (cumulus.EcowittExtraUseTempHum)
				sb.Append(",temp_and_humidity_ch1,temp_and_humidity_ch2,temp_and_humidity_ch3,temp_and_humidity_ch4,temp_and_humidity_ch5,temp_and_humidity_ch6,temp_and_humidity_ch7,temp_and_humidity_ch8");
			if (cumulus.EcowittExtraUseSoilMoist)
				sb.Append(",soil_ch1,soil_ch2,soil_ch3,soil_ch4,soil_ch5,soil_ch6,soil_ch7,soil_ch8");
			if (cumulus.EcowittExtraUseUserTemp)
				sb.Append(",temp_ch1,temp_ch2,temp_ch3,temp_ch4,temp_ch5,temp_ch6,temp_ch7,temp_ch8");
			if (cumulus.EcowittExtraUseLeafWet)
				sb.Append(",leaf_ch1,leaf_ch2,leaf_ch3,leaf_ch4,leaf_ch5,leaf_ch6,leaf_ch7,leaf_ch8");

			var res = api.GetHistoricData(startTime, endTime, new string[] { sb.ToString() }, out dat);

			if (res)
			{

				ProcessHistoryData(dat);

			}
		}

		private void ProcessHistoryData(EcowittApi.EcowittHistoricData data)
		{
			// allocate a dictionary of data objects, keyed on the timestamp
			var buffer = new SortedDictionary<DateTime, EcowittApi.HistoricData>();

			// process each sensor type, and store them for adding to the system later
			// Indoor Data
			if (data.indoor != null)
			{
				// do the temperature
				if (data.indoor.temperature.list != null)
				{
					foreach (var item in data.indoor.temperature.list)
					{
						// not present value = 140
						if (!item.Value.HasValue || item.Value == 140)
							continue;

						var newItem = new EcowittApi.HistoricData();
						newItem.IndoorTemp = item.Value;
						buffer.Add(item.Key, newItem);
					}
				}
				// do the humidity
				if (data.indoor.humidity.list != null)
				{
					foreach (var item in data.indoor.humidity.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].IndoorHum = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.IndoorHum = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
			}
			// Outdoor Data
			if (data.outdoor != null)
			{
				// Temperature
				if (data.outdoor.temperature.list != null)
				{
					foreach (var item in data.outdoor.temperature.list)
					{
						// not present value = 140
						if (!item.Value.HasValue || item.Value == 140)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].Temp = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.Temp = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}

				// Humidity
				if (data.outdoor.humidity.list != null)
				{
					foreach (var item in data.outdoor.humidity.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].Humidity = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.Humidity = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
				// Dewpoint
				if (data.outdoor.dew_point.list != null)
				{
					foreach (var item in data.outdoor.dew_point.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].DewPoint = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.DewPoint = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
			}
			// Wind Data
			if (data.wind != null)
			{
				// Speed
				if (data.wind.wind_speed.list != null)
				{
					foreach (var item in data.wind.wind_speed.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].WindSpd = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.WindSpd = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}

				// Gust
				if (data.wind.wind_gust.list != null)
				{
					foreach (var item in data.wind.wind_gust.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].WindGust = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.WindGust = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
				// Direction
				if (data.wind.wind_direction.list != null)
				{
					foreach (var item in data.wind.wind_direction.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].WindDir = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.WindDir = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
			}
			// Pressure Data
			if (data.pressure != null)
			{
				// relative
				if (data.pressure.relative.list != null)
				{
					foreach (var item in data.pressure.relative.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].Pressure = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.Pressure = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
			}
			// Solar Data
			if (data.solar_and_uvi != null)
			{
				// solar
				if (cumulus.EcowittExtraUseSolar && data.solar_and_uvi.solar.list != null)
				{
					foreach (var item in data.solar_and_uvi.solar.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].Solar = (int)item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.Solar = (int)item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
				// uvi
				if (cumulus.EcowittExtraUseUv && data.solar_and_uvi.uvi.list != null)
				{
					foreach (var item in data.solar_and_uvi.uvi.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].UVI = (int)item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.UVI = (int)item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
			}
			// Extra 8 channel sensors
			for (var i = 1; i <= 8; i++)
			{
				EcowittApi.EcowittHistoricTempHum srcTH = null;
				EcowittApi.EcowittHistoricDataSoil srcSoil = null;
				EcowittApi.EcowittHistoricDataTemp srcTemp = null;
				EcowittApi.EcowittHistoricDataLeaf srcLeaf = null;
				switch (i)
				{
					case 1:
						srcTH = data.temp_and_humidity_ch1;
						srcSoil = data.soil_ch1;
						srcTemp = data.temp_ch1;
						srcLeaf = data.leaf_ch1;
						break;
					case 2:
						srcTH = data.temp_and_humidity_ch2;
						srcSoil = data.soil_ch2;
						srcTemp = data.temp_ch2;
						srcLeaf = data.leaf_ch2;
						break;
					case 3:
						srcTH = data.temp_and_humidity_ch3;
						srcSoil = data.soil_ch3;
						srcTemp = data.temp_ch3;
						srcLeaf = data.leaf_ch3;
						break;
					case 4:
						srcTH = data.temp_and_humidity_ch4;
						srcSoil = data.soil_ch4;
						srcTemp = data.temp_ch4;
						srcLeaf = data.leaf_ch4;
						break;
					case 5:
						srcTH = data.temp_and_humidity_ch5;
						srcSoil = data.soil_ch5;
						srcTemp = data.temp_ch5;
						srcLeaf = data.leaf_ch5;
						break;
					case 6:
						srcTH = data.temp_and_humidity_ch6;
						srcSoil = data.soil_ch6;
						srcTemp = data.temp_ch6;
						srcLeaf = data.leaf_ch6;
						break;
					case 7:
						srcTH = data.temp_and_humidity_ch7;
						srcSoil = data.soil_ch7;
						srcTemp = data.temp_ch7;
						srcLeaf = data.leaf_ch7;
						break;
					case 8:
						srcTH = data.temp_and_humidity_ch8;
						srcSoil = data.soil_ch8;
						srcTemp = data.temp_ch8;
						srcLeaf = data.leaf_ch8;
						break;
				}

				// Extra Temp/Hum Data
				if (cumulus.EcowittExtraUseTempHum && srcTH != null)
				{
					// temperature
					if (srcTH.temperature.list != null)
					{
						foreach (var item in srcTH.temperature.list)
						{
							if (!item.Value.HasValue)
								continue;

							if (buffer.ContainsKey(item.Key))
							{
								buffer[item.Key].ExtraTemp[i - 1] = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.ExtraTemp[i - 1] = item.Value;
								buffer.Add(item.Key, newItem);
							}
						}
					}
					// humidity
					if (srcTH.humidity.list != null)
					{
						foreach (var item in srcTH.humidity.list)
						{
							if (!item.Value.HasValue)
								continue;

							if (buffer.ContainsKey(item.Key))
							{
								buffer[item.Key].ExtraHumidity[i - 1] = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.ExtraHumidity[i - 1] = item.Value;
								buffer.Add(item.Key, newItem);
							}
						}
					}
				}
				// Extra Soil Moisture Data
				if (cumulus.EcowittExtraUseSoilMoist && srcSoil != null && srcSoil.soilmoisture.list != null)
				{
					// moisture
					foreach (var item in srcSoil.soilmoisture.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].SoilMoist[i - 1] = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.SoilMoist[i - 1] = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
				// User Temp Data
				if (cumulus.EcowittExtraUseUserTemp && srcTemp != null && srcTemp.temperature.list != null)
				{
					// temperature
					foreach (var item in srcTemp.temperature.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].UserTemp[i - 1] = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.UserTemp[i - 1] = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
				// Leaf Wetness Data
				if (cumulus.EcowittExtraUseLeafWet && srcLeaf != null && srcLeaf.leaf_wetness.list != null)
				{
					// wetness
					foreach (var item in srcLeaf.leaf_wetness.list)
					{
						if (!item.Value.HasValue)
							continue;

						if (buffer.ContainsKey(item.Key))
						{
							buffer[item.Key].LeafWetness[i - 1] = item.Value;
						}
						else
						{
							var newItem = new EcowittApi.HistoricData();
							newItem.LeafWetness[i - 1] = item.Value;
							buffer.Add(item.Key, newItem);
						}
					}
				}
			}


			// now we have all the data for this period, for each record create the string expected by ProcessData and get it processed
			var rollHour = Math.Abs(cumulus.GetHourInc());
			var luhour = cumulus.LastUpdateTime.Hour;
			var rolloverdone = luhour == rollHour;
			var midnightraindone = luhour == 0;

			foreach (var rec in buffer)
			{
				var sb = new StringBuilder();
				if (rec.Value.IndoorTemp.HasValue) sb.Append("tempinf=" + rec.Value.IndoorTemp);
				if (rec.Value.IndoorHum.HasValue) sb.Append("&humidityin=" + rec.Value.IndoorHum);
				if (rec.Value.Temp.HasValue) sb.Append("&tempf=" + rec.Value.Temp);
				if (rec.Value.Humidity.HasValue) sb.Append("&humidity=" + rec.Value.Humidity);
				if (rec.Value.DewPoint.HasValue) sb.Append("&dewptf" + rec.Value.DewPoint);
				if (rec.Value.WindDir.HasValue) sb.Append("&winddir=" + rec.Value.WindDir);
				if (rec.Value.WindSpd.HasValue) sb.Append("&windspeedmph=" + rec.Value.WindSpd);
				if (rec.Value.WindGust.HasValue) sb.Append("&windgustmph=" + rec.Value.WindGust);
				if (rec.Value.Pressure.HasValue) sb.Append("&baromrelin=" + rec.Value.Pressure);
				if (rec.Value.Solar.HasValue) sb.Append("&solarradiation=" + rec.Value.Solar);
				if (rec.Value.UVI.HasValue) sb.Append("&uv=" + rec.Value.UVI);
				for (var i = 1; i <= 8; i++)
				{
					if (rec.Value.ExtraTemp[i - 1].HasValue) sb.Append($"&temp{i}f={rec.Value.ExtraTemp[i - 1]}");
					if (rec.Value.ExtraHumidity[i - 1].HasValue) sb.Append($"&humidity{i}={rec.Value.ExtraHumidity[i - 1]}");
					if (rec.Value.UserTemp[i - 1].HasValue) sb.Append($"tf_ch{i}={rec.Value.UserTemp[i - 1]}");
					if (rec.Value.SoilMoist[i - 1].HasValue) sb.Append($"soilmoisture{i}={rec.Value.SoilMoist[i - 1]}");
					if (rec.Value.LeafWetness[i - 1].HasValue)
						if (i==1)
							sb.Append($"leafwetness={rec.Value.LeafWetness[i - 1]}");
						else
							sb.Append($"leafwetness{i}={rec.Value.LeafWetness[i - 1]}");
				}

				cumulus.LogMessage("Processing data for " + rec.Key);

				var h = rec.Key.Hour;

				//  if outside rollover hour, rollover yet to be done
				if (h != rollHour) rolloverdone = false;

				// In rollover hour and rollover not yet done
				if (h == rollHour && !rolloverdone)
				{
					// do rollover
					cumulus.LogMessage("Day rollover " + rec.Key.ToShortTimeString());
					DayReset(rec.Key);

					rolloverdone = true;
				}

				// Not in midnight hour, midnight rain yet to be done
				if (h != 0) midnightraindone = false;

				// In midnight hour and midnight rain (and sun) not yet done
				if (h == 0 && !midnightraindone)
				{
					ResetMidnightRain(rec.Key);
					ResetSunshineHours();
					midnightraindone = true;
				}

				// finally apply this data
				ApplyData(sb.ToString(), station == null, rec.Key);

				// add in archive period worth of sunshine, if sunny
				if (SolarRad > CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100 &&
					SolarRad >= cumulus.SolarOptions.SolarMinimum)
					SunshineHours += 5 / 60.0;



				// add in 'following interval' minutes worth of wind speed to windrun
				cumulus.LogMessage("Windrun: " + WindAverage.ToString(cumulus.WindFormat) + cumulus.Units.WindText + " for " + 5 + " minutes = " +
								   (WindAverage * WindRunHourMult[cumulus.Units.Wind] * 5 / 60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);

				WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] * 5 / 60.0;

				// update heating/cooling degree days
				UpdateDegreeDays(5);

				// update dominant wind bearing
				CalculateDominantWindBearing(Bearing, WindAverage, 5);

				CheckForWindrunHighLow(rec.Key);

				//bw?.ReportProgress((totalentries - datalist.Count) * 100 / totalentries, "processing");

				//UpdateDatabase(timestamp.ToUniversalTime(), historydata.interval, false);

				cumulus.DoLogFile(rec.Key, false);
				if (cumulus.StationOptions.LogExtraSensors) cumulus.DoExtraLogFile(rec.Key);

				//AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing,
				//    OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
				//    OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex);

				AddRecentDataWithAq(rec.Key, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
					OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);

				if (cumulus.StationOptions.CalculatedET && rec.Key.Minute == 0)
				{
					// Start of a new hour, and we want to calculate ET in Cumulus
					CalculateEvaoptranspiration(rec.Key);
				}

				DoTrendValues(rec.Key);
				UpdatePressureTrendString();
				UpdateStatusPanel(rec.Key);
				cumulus.AddToWebServiceLists(rec.Key);

			}
		}

		public string ProcessData(IHttpContext context, bool main, DateTime? ts = null)
		{
			/*
			 * Ecowitt doc:
			 *
			POST Parameters - all fields are URL escaped

			PASSKEY=<redacted>&stationtype=GW1000A_V1.6.8&dateutc=2021-07-23+17:13:34&tempinf=80.6&humidityin=50&baromrelin=29.940&baromabsin=29.081&tempf=81.3&humidity=43&winddir=296&windspeedmph=2.46&windgustmph=4.25&maxdailygust=14.09&solarradiation=226.28&uv=1&rainratein=0.000&eventrainin=0.000&hourlyrainin=0.000&dailyrainin=0.000&weeklyrainin=0.000&monthlyrainin=4.118&yearlyrainin=29.055&totalrainin=29.055&temp1f=83.48&humidity1=39&temp2f=87.98&humidity2=40&temp3f=82.04&humidity3=40&temp4f=93.56&humidity4=34&temp5f=-11.38&temp6f=87.26&humidity6=38&temp7f=45.50&humidity7=40&soilmoisture1=51&soilmoisture2=65&soilmoisture3=72&soilmoisture4=36&soilmoisture5=48&pm25_ch1=11.0&pm25_avg_24h_ch1=10.8&pm25_ch2=13.0&pm25_avg_24h_ch2=15.0&tf_co2=80.8&humi_co2=48&pm25_co2=4.8&pm25_24h_co2=6.1&pm10_co2=4.9&pm10_24h_co2=6.5&co2=493&co2_24h=454&lightning_time=1627039348&lightning_num=3&lightning=24&wh65batt=0&wh80batt=3.06&batt1=0&batt2=0&batt3=0&batt4=0&batt5=0&batt6=0&batt7=0&soilbatt1=1.5&soilbatt2=1.4&soilbatt3=1.5&soilbatt4=1.5&soilbatt5=1.6&pm25batt1=4&pm25batt2=4&wh57batt=4&co2_batt=6&freq=868M&model=GW1000_Pro
			PASSKEY=<redacted>&stationtype=GW1100A_V2.0.2&dateutc=2021-09-08+11:58:39&tempinf=80.8&humidityin=42&baromrelin=29.864&baromabsin=29.415&temp1f=87.8&tf_ch1=64.4&batt1=0&tf_batt1=1.48&freq=868M&model=GW1100A

			 */

			var procName = main ? "ProcessData" : "ProcessExtraData";

			if (starting || stopping)
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

				cumulus.LogDebugMessage($"{procName}: Processing posted data");

				var text = new StreamReader(context.Request.InputStream).ReadToEnd();

				cumulus.LogDataMessage($"{procName}: Payload = {text}");

				// force the wind chill calculation as it is not present in historic data
				var chillSave = cumulus.StationOptions.CalculatedWC;
				cumulus.StationOptions.CalculatedWC = true;

				var retVal = ApplyData(text, main, ts);

				// restore wind chill setting
				cumulus.StationOptions.CalculatedWC = chillSave;

				if (retVal != "")
				{
					context.Response.StatusCode = 500;
					return retVal;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"{procName}: Error - {ex.Message}");
				context.Response.StatusCode = 500;
				return "Failed: General error - " + ex.Message;
			}

			cumulus.LogDebugMessage($"{procName}: Complete");

			context.Response.StatusCode = 200;
			return "success";
		}

		public string ApplyData(string dataString, bool main, DateTime? ts = null)
		{ 
			var procName = main ? "ApplyData" : "ApplyExtraData";
			var thisStation = main ? this : station;

			try
			{
				DateTime recDate;


				var data = HttpUtility.ParseQueryString(dataString);

				// We will ignore the dateutc field if this "live" data to avoid any clock issues
				recDate = ts.HasValue ? ts.Value : DateTime.Now;

				// we only really want to do this once
				if (reportStationType && !ts.HasValue)
				{
					cumulus.LogDebugMessage($"{procName}: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");
					reportStationType = false;
				}

				// Only do the primary sensors if running as the main station
				if (main)
				{
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
						var spd = data["windspeedmph"];


						if (gust == null || dir == null || spd == null)
						{
							cumulus.LogMessage($"ProcessData: Error, missing wind data");
						}
						else
						{
							var gustVal = ConvertWindMPHToUser(Convert.ToDouble(gust, invNum));
							var dirVal = Convert.ToInt32(dir, invNum);
							var spdVal = ConvertWindMPHToUser(Convert.ToDouble(spd, invNum));

							// The protocol does not provide an average value
							// so feed in current MX average
							DoWind(spdVal, dirVal, WindAverage / cumulus.Calib.WindSpeed.Mult, recDate);

							var gustLastCal = gustVal * cumulus.Calib.WindGust.Mult;
							if (gustLastCal > RecentMaxGust)
							{
								cumulus.LogDebugMessage("Setting max gust from current value: " + gustLastCal.ToString(cumulus.WindFormat));
								CheckHighGust(gustLastCal, dirVal, recDate);

								// add to recent values so normal calculation includes this value
								WindRecent[nextwind].Gust = gustVal; // use uncalibrated value
								WindRecent[nextwind].Speed = WindAverage / cumulus.Calib.WindSpeed.Mult;
								WindRecent[nextwind].Timestamp = recDate;
								nextwind = (nextwind + 1) % MaxWindRecent;

								RecentMaxGust = gustLastCal;
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Wind data - " + ex.Message);
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
							var humVal = Convert.ToInt32(humIn, invNum);
							DoIndoorHumidity(humVal);
						}

						if (humOut == null)
						{
							cumulus.LogMessage("ProcessData: Error, missing outdoor humidity");
						}
						else
						{
							var humVal = Convert.ToInt32(humOut, invNum);
							DoOutdoorHumidity(humVal, recDate);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Humidity data - " + ex.Message);
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
							var pressVal = ConvertPressINHGToUser(Convert.ToDouble(press, invNum));
							DoPressure(pressVal, recDate);
							UpdatePressureTrendString();
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Pressure data - " + ex.Message);
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
							var tempVal = ConvertTempFToUser(Convert.ToDouble(temp, invNum));
							DoIndoorTemp(tempVal);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Indoor temp data - " + ex.Message);
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
							var tempVal = ConvertTempFToUser(Convert.ToDouble(temp, invNum));
							DoOutdoorTemp(tempVal, recDate);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Outdoor temp data - " + ex.Message);
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
						// totalrainin - not reliable, depends on console and firmware version as to whether this is available or not.
						// rainratein
						// 24hourrainin Ambient only?
						// eventrainin

						var rain = data["yearlyrainin"];
						var rRate = data["rainratein"];

						if (rRate == null)
						{
							// No rain rate, so we will calculate it
							calculaterainrate = true;
							rRate = "0";
						}
						else
						{
							// we have a rain rate, so we will NOT calculate it
							calculaterainrate = false;
						}

						if (rain == null)
						{
							cumulus.LogMessage($"ProcessData: Error, missing rainfall");
						}
						else
						{
							var rainVal = ConvertRainINToUser(Convert.ToDouble(rain, invNum));
							var rateVal = ConvertRainINToUser(Convert.ToDouble(rRate, invNum));
							DoRain(rainVal, rateVal, recDate);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Rain data - " + ex.Message);
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
							var val = ConvertTempFToUser(Convert.ToDouble(dewpnt, invNum));
							DoOutdoorDewpoint(val, recDate);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Dew point data - " + ex.Message);
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
								var val = ConvertTempFToUser(Convert.ToDouble(chill, invNum));
								DoWindChill(val, recDate);
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("ProcessData: Error in Dew point data - " + ex.Message);
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
				if (main || cumulus.EcowittExtraUseTempHum)
				{
					try
					{
						// temp[1-10]f
						ProcessExtraTemps(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in extra temperature data - {ex.Message}");
					}
				}

				// === Extra Humidity ===
				if (main || cumulus.EcowittExtraUseTempHum)
				{
					try
					{
						// humidity[1-10]
						ProcessExtraHumidity(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in extra humidity data - {ex.Message}");
					}
				}


				// === Solar ===
				if (main || cumulus.EcowittExtraUseSolar)
				{
					try
					{
						// solarradiation
						ProcessSolar(data, thisStation, recDate);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in solar data - {ex.Message}");
					}
				}


				// === UV ===
				if (main || cumulus.EcowittExtraUseUv)
				{
					try
					{
						// uv
						ProcessUv(data, thisStation, recDate);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in UV data - {ex.Message}");
					}
				}


				// === Soil Temp ===
				if (main || cumulus.EcowittExtraUseSoilTemp)
				{
					try
					{
						// soiltempf
						// soiltemp[2-16]f
						ProcessSoilTemps(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Soil temp data - {ex.Message}");
					}
				}


				// === Soil Moisture ===
				if (main || cumulus.EcowittExtraUseSoilMoist)
				{
					try
					{
						// soilmoisture[1-16]
						ProcessSoilMoist(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Soil moisture data - {ex.Message}");
					}
				}


				// === Leaf Wetness ===
				if (main || cumulus.EcowittExtraUseLeafWet)
				{
					try
					{
						// leafwetness
						// leafwetness[2-8]
						ProcessLeafWetness(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Leaf wetness data - {ex.Message}");
					}
				}


				// === User Temp (Soil or Water) ===
				if (main || cumulus.EcowittExtraUseUserTemp)
				{
					try
					{
						// tf_ch[1-8]
						ProcessUserTemp(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in User Temp data - {ex.Message}");
					}
				}


				// === Air Quality ===
				if (main || cumulus.EcowittExtraUseAQI)
				{
					try
					{
						// pm25_ch[1-4]
						// pm25_avg_24h_ch[1-4]
						ProcessAirQuality(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Air Quality data - {ex.Message}");
					}
				}


				// === CO₂ ===
				if (main || cumulus.EcowittExtraUseCo2)
				{
					try
					{
						// tf_co2
						// humi_co2
						// pm25_co2
						// pm25_24h_co2
						// pm10_co2
						// pm10_24h_co2
						// co2
						// co2_24h
						ProcessCo2(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in CO₂ data - {ex.Message}");
					}
				}


				// === Lightning ===
				if (main || cumulus.EcowittExtraUseLightning)
				{
					try
					{
						// lightning
						// lightning_time
						// lightning_num
						ProcessLightning(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Lightning data - {ex.Message}");
					}
				}


				// === Leak ===
				if (main || cumulus.EcowittExtraUseLeak)
				{
					try
					{
						// leak[1 - 4]
						ProcessLeak(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error in Leak data - {ex.Message}");
					}
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
					wh90batt
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
					cumulus.LogMessage($"{procName}: Error in Battery data - {ex.Message}");
				}


				// === Extra Dew point ===
				if (main || cumulus.EcowittExtraUseTempHum)
				{
					try
					{
						ProcessExtraDewPoint(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"{procName}: Error calculating extra sensor dew points - {ex.Message}");
					}
				}

				// === Firmware Version ===
				try
				{
					if (data["stationtype"] != null)
					{
						var fwString = data["stationtype"].Split(new string[] { "_V" }, StringSplitOptions.None);
						if (fwString.Length > 1)
						{
							// bug fix for WS90 which sends "stationtype=GW2000A_V2.1.0, runtime=253500"
							var str = fwString[1].Split(new string[] { ", " }, StringSplitOptions.None)[0];
							GW1000FirmwareVersion = str;
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"{procName}: Error extracting firmware version - {ex.Message}");
				}


				DoForecast(string.Empty, false);

				UpdateStatusPanel(recDate);
				UpdateMQTT();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"{procName}: Error - {ex.Message}");
				return "Failed: General error - " + ex.Message;
			}

			return "";
		}

		private void ProcessExtraTemps(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["temp" + i + "f"] != null)
				{
					station.DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(data["temp" + i + "f"], invNum)), i);
				}
			}
		}

		private void ProcessExtraHumidity(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["humidity" + i] != null)
				{
					station.DoExtraHum(Convert.ToDouble(data["humidity" + i], invNum), i);
				}
			}
		}

		private void ProcessSolar(NameValueCollection data, WeatherStation station, DateTime recDate)
		{
			if (data["solarradiation"] != null)
			{
				station.DoSolarRad((int)Convert.ToDouble(data["solarradiation"], invNum), recDate);
			}
		}

		private void ProcessUv(NameValueCollection data, WeatherStation station, DateTime recDate)
		{
			if (data["uv"] != null)
			{
				station.DoUV(Convert.ToDouble(data["uv"], invNum), recDate);
			}
		}

		private void ProcessSoilTemps(NameValueCollection data, WeatherStation station)
		{
			if (data["soiltempf"] != null)
			{
				station.DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(data["soiltempf"], invNum)), 1);
			}

			for (var i = 2; i <= 16; i++)
			{
				if (data["soiltemp" + i + "f"] != null)
				{
					station.DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(data["soiltemp" + i + "f"], invNum)), i - 1);
				}
			}
		}

		private void ProcessSoilMoist(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 16; i++)
			{
				if (data["soilmoisture" + i] != null)
				{
					station.DoSoilMoisture(Convert.ToDouble(data["soilmoisture" + i], invNum), i);
				}
			}
		}

		private void ProcessLeafWetness(NameValueCollection data, WeatherStation station)
		{
			if (data["leafwetness"] != null)
			{
				station.DoLeafWetness(Convert.ToInt32(data["leafwetness"], invNum), 1);
			}
			// Though Ecowitt supports up to 8 sensors, MX only supports the first 4
			for (var i = 1; i <= 8; i++)
			{
				if (data["leafwetness_ch" + i] != null)
				{
					station.DoLeafWetness(Convert.ToInt32(data["leafwetness_ch" + i], invNum), i);
				}
			}

		}

		private void ProcessUserTemp(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 8; i++)
			{
				if (data["tf_ch" + i] != null)
				{
					station.DoUserTemp(ConvertTempFToUser(Convert.ToDouble(data["tf_ch" + i], invNum)), i);
				}
			}
		}

		private void ProcessAirQuality(NameValueCollection data, WeatherStation station)
		{
			// pm25_ch[1-4]
			// pm25_avg_24h_ch[1-4]

			for (var i = 1; i <= 4; i++)
			{
				var pm = data["pm25_ch" + i];
				var pmAvg = data["pm25_avg_24h_ch" + i];
				if (pm != null)
				{
					station.DoAirQuality(Convert.ToDouble(pm, invNum), i);
				}
				if (pmAvg != null)
				{
					station.DoAirQualityAvg(Convert.ToDouble(pmAvg, invNum), i);
				}
			}
		}

		private void ProcessCo2(NameValueCollection data, WeatherStation station)
		{
			// tf_co2
			// humi_co2
			// pm25_co2
			// pm25_24h_co2
			// pm10_co2
			// pm10_24h_co2
			// co2
			// co2_24h

			if (data["tf_co2"] != null)
			{
				station.CO2_temperature = ConvertTempFToUser(Convert.ToDouble(data["tf_co2"], invNum));
			}
			if (data["humi_co2"] != null)
			{
				station.CO2_humidity = Convert.ToInt32(data["humi_co2"], invNum);
			}
			if (data["pm25_co2"] != null)
			{
				station.CO2_pm2p5 = Convert.ToDouble(data["pm25_co2"], invNum);
			}
			if (data["pm25_24h_co2"] != null)
			{
				station.CO2_pm2p5_24h = Convert.ToDouble(data["pm25_24h_co2"], invNum);
			}
			if (data["pm10_co2"] != null)
			{
				station.CO2_pm10 = Convert.ToDouble(data["pm10_co2"], invNum);
			}
			if (data["pm10_24h_co2"] != null)
			{
				station.CO2_pm10_24h = Convert.ToDouble(data["pm10_24h_co2"], invNum);
			}
			if (data["co2"] != null)
			{
				station.CO2 = Convert.ToInt32(data["co2"], invNum);
			}
			if (data["co2_24h"] != null)
			{
				station.CO2_24h = Convert.ToInt32(data["co2_24h"], invNum);
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
				var valDist = Convert.ToDouble(dist, invNum);
				if (valDist != 255)
				{
					station.LightningDistance = ConvertKmtoUserUnits(valDist);
				}

				var valTime = Convert.ToDouble(time, invNum);
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
				station.LightningStrikesToday = Convert.ToInt32(num, invNum);
			}
		}

		private void ProcessLeak(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 4; i++)
			{
				if (data["leak" + i] != null)
				{
					station.DoLeakSensor(Convert.ToInt32(data["leak" + i], invNum), i);
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
			lowBatt = lowBatt || (data["wh68batt"] != null && Convert.ToDouble(data["wh68batt"], invNum) <= 1.2);
			lowBatt = lowBatt || (data["wh80batt"] != null && Convert.ToDouble(data["wh80batt"], invNum) <= 1.2);
			lowBatt = lowBatt || (data["wh90batt"] != null && Convert.ToDouble(data["wh90batt"], invNum) <= 2.4);
			for (var i = 1; i < 5; i++)
			{
				lowBatt = lowBatt || (data["batt" + i]     != null && data["batt" + i] == "1");
				lowBatt = lowBatt || (data["soilbatt" + i] != null && Convert.ToDouble(data["soilbatt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["pm25batt" + i] != null && data["pm25batt" + i] == "1");
				lowBatt = lowBatt || (data["leakbatt" + i] != null && data["leakbatt" + i] == "1");
				lowBatt = lowBatt || (data["tf_batt" + i]  != null && Convert.ToDouble(data["tf_batt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["leaf_batt" + i] != null && Convert.ToDouble(data["leaf_batt" + i], invNum) <= 1.2);
			}
			for (var i = 5; i < 9; i++)
			{
				lowBatt = lowBatt || (data["batt" + i]     != null && data["batt" + i] == "1");
				lowBatt = lowBatt || (data["soilbatt" + i] != null && Convert.ToDouble(data["soilbatt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["tf_batt" + i]  != null && Convert.ToDouble(data["tf_batt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["leaf_batt" + i] != null && Convert.ToDouble(data["leaf_batt" + i], invNum) <= 1.2);
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
