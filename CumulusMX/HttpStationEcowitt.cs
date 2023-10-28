using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using EmbedIO;


namespace CumulusMX
{

	class HttpStationEcowitt : WeatherStation
	{
		private readonly WeatherStation station;
		private bool starting = true;
		private bool stopping = false;
		private readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;
		private bool reportStationType = true;
		private int lastMinute = -1;
		private EcowittApi ecowittApi;
		private int maxArchiveRuns = 1;

		public HttpStationEcowitt(Cumulus cumulus, WeatherStation station = null) : base(cumulus)
		{
			this.station = station;

			var mainStation = station == null;

			if (mainStation)
			{
				cumulus.LogMessage("Creating HTTP Station (Ecowitt)");
			}
			else
			{
				cumulus.LogMessage("Creating Extra Sensors - HTTP Station (Ecowitt)");
			}

			// Do not set these if we are only using extra sensors
			if (mainStation)
			{
				// does not provide 10 min average wind speeds
				cumulus.StationOptions.CalcuateAverageWindSpeed = true;

				// GW1000 does not provide an interval gust value, it gives us a 2 minute high
				// The speed is the average for that update
				// Therefore we need to force using the speed for the average calculation
				cumulus.StationOptions.UseSpeedForAvgCalc = true;
				// also use it for the Latest value
				cumulus.StationOptions.UseSpeedForLatest = true;

				// does not send DP, so force MX to calculate it
				cumulus.StationOptions.CalculatedDP = true;
				// Same for Wind Chill
				cumulus.StationOptions.CalculatedWC = true;
				// does not provide a forecast, force MX to provide it
				cumulus.UseCumulusForecast = true;
				// does not provide pressure trend strings
				cumulus.StationOptions.UseCumulusPresstrendstr = true;

				if (cumulus.Gw1000PrimaryTHSensor == 0)
				{
					// We are using the primary T/H sensor
					cumulus.LogMessage("Using the default outdoor temp/hum sensor data");
				}
				else
				{
					// We are not using the primary T/H sensor
					cumulus.LogMessage("Overriding the default outdoor temp/hum data with Extra temp/hum sensor #" + cumulus.Gw1000PrimaryTHSensor);
				}

				if (cumulus.Gw1000PrimaryRainSensor == 0)
				{
					// We are using the traditional rain tipper
					cumulus.LogMessage("Using the default traditional rain sensor data");
				}
				else
				{
					cumulus.LogMessage("Using the piezo rain sensor data");
				}

				if (cumulus.EcowittSetCustomServer)
				{
					cumulus.LogMessage("Checking Ecowitt Gateway Custom Server configuration...");
					var api = new GW1000Api(cumulus);
					api.OpenTcpPort(cumulus.EcowittGatewayAddr, 45000);
					SetCustomServer(api, mainStation);
					api.CloseTcpPort();
					cumulus.LogMessage("Ecowitt Gateway Custom Server configuration complete");
				}
			}
			else if (cumulus.EcowittSetCustomServer)
			{
				cumulus.LogMessage("Checking Ecowitt Extra Gateway Custom Server configuration...");
				var api = new GW1000Api(cumulus);
				api.OpenTcpPort(cumulus.EcowittGatewayAddr, 45000);
				SetCustomServer(api, mainStation);
				api.CloseTcpPort();
				cumulus.LogMessage("Ecowitt Extra Gateway Custom Server configuration complete");
			}

			if (mainStation || (!mainStation && cumulus.EcowittExtraUseAQI))
			{
				cumulus.Units.AirQualityUnitText = "µg/m³";
			}
			if (mainStation || (!mainStation && cumulus.EcowittExtraUseSoilMoist))
			{
				cumulus.Units.SoilMoistureUnitText = "%";
			}
			if (mainStation || (!mainStation && cumulus.EcowittExtraUseSoilMoist))
			{
				cumulus.Units.LeafWetnessUnitText = "%";
			}

			// Only perform the Start-up if we are a proper station, not a Extra Sensor
			if (mainStation)
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
			Api.stationEcowitt = null;
			Api.stationEcowittExtra = null;
			if (station == null)
			{
				StopMinuteTimer();
				cumulus.LogMessage("HTTP Station (Ecowitt) Stopped");
			}
		}

		public override void getAndProcessHistoryData()
		{
			//cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			//cumulus.LogDebugMessage("Lock: Station has the lock");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogWarningMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
			}
			else
			{
				int archiveRun = 0;

				try
				{

					ecowittApi = new EcowittApi(cumulus, this);

					do
					{
						GetHistoricData();
						archiveRun++;
					} while (archiveRun < maxArchiveRuns);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Exception occurred reading archive data: " + ex.Message);
				}
			}

			//cumulus.LogDebugMessage("Lock: Station releasing the lock");
			_ = Cumulus.syncInit.Release();

			StartLoop();
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

			ecowittApi.GetHistoricData(startTime, endTime, cumulus.cancellationToken);

		}

		public string ProcessData(IHttpContext context, bool main, DateTime? ts = null)
		{
			/*
			 * Ecowitt doc:
			 *
			POST Parameters - all fields are URL escaped

			PASSKEY=<redacted>&stationtype=GW1000A_V1.6.8&dateutc=2021-07-23+17:13:34&tempinf=80.6&humidityin=50&baromrelin=29.940&baromabsin=29.081&tempf=81.3&humidity=43&winddir=296&windspeedmph=2.46&windgustmph=4.25&maxdailygust=14.09&solarradiation=226.28&uv=1&rainratein=0.000&eventrainin=0.000&hourlyrainin=0.000&dailyrainin=0.000&weeklyrainin=0.000&monthlyrainin=4.118&yearlyrainin=29.055&totalrainin=29.055&temp1f=83.48&humidity1=39&temp2f=87.98&humidity2=40&temp3f=82.04&humidity3=40&temp4f=93.56&humidity4=34&temp5f=-11.38&temp6f=87.26&humidity6=38&temp7f=45.50&humidity7=40&soilmoisture1=51&soilmoisture2=65&soilmoisture3=72&soilmoisture4=36&soilmoisture5=48&pm25_ch1=11.0&pm25_avg_24h_ch1=10.8&pm25_ch2=13.0&pm25_avg_24h_ch2=15.0&tf_co2=80.8&humi_co2=48&pm25_co2=4.8&pm25_24h_co2=6.1&pm10_co2=4.9&pm10_24h_co2=6.5&co2=493&co2_24h=454&lightning_time=1627039348&lightning_num=3&lightning=24&wh65batt=0&wh80batt=3.06&batt1=0&batt2=0&batt3=0&batt4=0&batt5=0&batt6=0&batt7=0&soilbatt1=1.5&soilbatt2=1.4&soilbatt3=1.5&soilbatt4=1.5&soilbatt5=1.6&pm25batt1=4&pm25batt2=4&wh57batt=4&co2_batt=6&freq=868M&model=GW1000_Pro
			PASSKEY=<redacted>&stationtype=GW1100A_V2.0.2&dateutc=2021-09-08+11:58:39&tempinf=80.8&humidityin=42&baromrelin=29.864&baromabsin=29.415&temp1f=87.8&tf_ch1=64.4&batt1=0&tf_batt1=1.48&freq=868M&model=GW1100A

			PASSKEY=<PassKey>&stationtype=GW1100A_V2.1.4&runtime=2336207&dateutc=2022-05-13+08:55:11&tempinf=73.0&humidityin=38&baromrelin=30.156&baromabsin=29.297&tempf=63.0&humidity=49&winddir=71&windspeedmph=2.68&windgustmph=11.86&maxdailygust=15.21&solarradiation=694.87&uv=5&rainratein=0.000&eventrainin=0.000&hourlyrainin=0.000&dailyrainin=0.000&weeklyrainin=0.000&monthlyrainin=0.591&yearlyrainin=14.591&temp1f=67.8&humidity1=43&temp2f=68.7&humidity2=47&temp3f=62.6&humidity3=51&temp4f=62.6&humidity4=51&temp5f=-1.5&temp6f=76.1&humidity6=47&temp7f=44.4&humidity7=49&soilmoisture1=14&soilmoisture2=50&soilmoisture3=20&soilmoisture4=25&pm25_ch1=7.0&pm25_avg_24h_ch1=7.6&pm25_ch2=6.0&pm25_avg_24h_ch2=8.3&tf_co2=72.9&humi_co2=44&pm25_co2=1.2&pm25_24h_co2=2.6&pm10_co2=1.5&pm10_24h_co2=3.0&co2=387&co2_24h=536&lightning_num=0&lightning=31&lightning_time=1652304268&leak_ch2=0&wh65batt=0&wh80batt=2.74&wh26batt=0&batt1=0&batt2=0&batt3=0&batt4=0&batt5=0&batt6=0&batt7=0&soilbatt1=1.3&soilbatt2=1.4&soilbatt3=1.4&soilbatt4=1.4&pm25batt1=4&pm25batt2=4&wh57batt=5&leakbatt2=4&co2_batt=6&freq=868M&model=GW1100A

			PASSKEY=<PassKey>&stationtype=EasyWeatherPro_V5.1.1&runtime=4&dateutc=2023-09-03+07:29:16&tempinf=70.0&humidityin=89&baromrelin=29.811&baromabsin=29.811&tempf=52.7&humidity=92&winddir=223&windspeedmph=1.79&windgustmph=4.92&maxdailygust=15.88&solarradiation=39.31&uv=0&pm25_ch1=4.0&pm25_avg_24h_ch1=3.1&rrain_piezo=0.254&erain_piezo=0.126&hrain_piezo=0.016&drain_piezo=0.126&wrain_piezo=0.189&mrain_piezo=0.126&yrain_piezo=34.882&ws90cap_volt=3.7&ws90_ver=126&gain10_piezo=1.00&gain20_piezo=1.00&gain30_piezo=1.00&gain40_piezo=1.00&gain50_piezo=1.00&pm25batt1=3&wh90batt=2.94&freq=868M&model=HP2564AE_Pro_V1.8.1&interval=16
			 */

			var procName = main ? "ProcessData" : "ProcessExtraData";

			if (starting || stopping)
			{
				cumulus.LogMessage($"Station {(starting ? "starting" : "stopping")}, incoming data ignored");
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

				cumulus.LogDataMessage($"{procName}: Payload = {System.Text.RegularExpressions.Regex.Replace(text, "PASSKEY=[^&]+", "PASSKEY=<PassKey>")}");

				var retVal = ApplyData(text, main, ts);

				ForwardData(text, main);

				if (retVal != "")
				{
					context.Response.StatusCode = 500;
					return retVal;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"{procName}: Error - {ex.Message}");
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
			string thisTemp = null;
			string thisHum = null;

			try
			{
				DateTime recDate;


				var data = HttpUtility.ParseQueryString(dataString);

				// We will ignore the dateutc field if this "live" data to avoid any clock issues
				recDate = ts.HasValue ? ts.Value : DateTime.Now;

				if (recDate.Minute != lastMinute)
				{

					// at start-up or every 20 minutes trigger output of uptime
					if ((recDate.Minute % 20) == 0 || lastMinute == -1 && data["runtime"] != null)
					{
						var runtime = Convert.ToInt32(data["runtime"]);
						if (runtime > 0)
						{
							var uptime = TimeSpan.FromSeconds(runtime);

							cumulus.LogMessage($"Ecowitt Gateway uptime = {runtime} secs - {uptime:c}");
						}
					}

					lastMinute = recDate.Minute;
				}

				// we only really want to do this once
				if (reportStationType && !ts.HasValue)
				{
					cumulus.LogDebugMessage($"{procName}: StationType = {data["stationtype"]}, Model = {data["model"]}, Frequency = {data["freq"]}Hz");
					reportStationType = false;
				}

				// Only do the primary sensors if running as the main station
				if (main)
				{
					thisTemp = cumulus.Gw1000PrimaryTHSensor == 0 ? data["tempf"] : data["temp" + cumulus.Gw1000PrimaryTHSensor + "f"];
					thisHum = cumulus.Gw1000PrimaryTHSensor == 0 ? data["humidity"] : data["humidity" + cumulus.Gw1000PrimaryTHSensor];

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
							cumulus.LogDebugMessage($"{procName}: Error, missing wind data");
						}
						else
						{
							var gustVal = ConvertWindMPHToUser(Convert.ToDouble(gust, invNum));
							var dirVal = Convert.ToInt32(dir, invNum);
							var spdVal = ConvertWindMPHToUser(Convert.ToDouble(spd, invNum));

							DoWind(gustVal, dirVal, spdVal, recDate);

						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Wind data - {ex.Message}");
						return "Failed: Error in wind data - " + ex.Message;
					}


					// === Humidity ===
					try
					{
						// humidity
						// humidityin

						var humIn = data["humidityin"];

						if (humIn == null)
						{
							cumulus.LogWarningMessage($"{procName}: Error, missing indoor humidity");
						}
						else
						{
							var humVal = Convert.ToInt32(humIn, invNum);
							DoIndoorHumidity(humVal);
						}

						if (cumulus.Gw1000PrimaryTHSensor == 0)
						{
							if (thisHum == null)
							{
								cumulus.LogDebugMessage($"{procName}: Error, missing outdoor humidity");
							}
							else
							{
								var humVal = Convert.ToInt32(thisHum, invNum);
								DoOutdoorHumidity(humVal, recDate);
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Humidity data - {ex.Message}");
						return "Failed: Error in humidity data - " + ex.Message;
					}


					// === Pressure ===
					try
					{
						// baromabsin
						// baromrelin

						var press = data["baromrelin"];
						var stnPress = data["baromabsin"];

						if (press == null)
						{
							cumulus.LogDebugMessage($"{procName}: Error, missing baro pressure");
						}
						else
						{
							var pressVal = ConvertPressINHGToUser(Convert.ToDouble(press, invNum));
							DoPressure(pressVal, recDate);
							UpdatePressureTrendString();
						}

						if (stnPress == null)
						{
							cumulus.LogDebugMessage($"{procName}: Error, missing absolute baro pressure");
						}
						else
						{
							StationPressure = ConvertPressINHGToUser(Convert.ToDouble(stnPress, invNum));
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Pressure data - {ex.Message}");
						return "Failed: Error in baro pressure data - " + ex.Message;
					}


					// === Indoor temp ===
					try
					{
						// tempinf

						var temp = data["tempinf"];

						if (temp == null)
						{
							cumulus.LogDebugMessage($"{procName}: Error, missing indoor temp");
						}
						else
						{
							var tempVal = ConvertTempFToUser(Convert.ToDouble(temp, invNum));
							DoIndoorTemp(tempVal);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Indoor temp data - {ex.Message}");
						return "Failed: Error in indoor temp data - " + ex.Message;
					}


					// === Outdoor temp ===
					try
					{
						// tempf
						if (cumulus.Gw1000PrimaryTHSensor == 0)
						{
							if (thisTemp == null)
							{
								cumulus.LogDebugMessage($"{procName}: Error, missing outdoor temp");
							}
							else
							{
								var tempVal = ConvertTempFToUser(Convert.ToDouble(thisTemp, invNum));
								DoOutdoorTemp(tempVal, recDate);
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Outdoor temp data - {ex.Message}");
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
						// yearlyrainin - also turns out that not all stations send this :(
						// totalrainin - not reliable, depends on console and firmware version as to whether this is available or not.
						// rainratein
						// 24hourrainin Ambient only?
						// eventrainin

						// same for piezo data
						// ​rrain_piezo
						// erain_piezo - event rain
						// hrain_piezo
						// drain_piezo
						// wrain_piezo
						// mrain_piezo
						// yrain_piezo

						string rain, rRate;
						// if no yearly counter, try the total counter
						if (cumulus.Gw1000PrimaryRainSensor == 0)
						{
							rain = data["yearlyrainin"] ?? data["totalrainin"];
							rRate = data["rainratein"];
							if (cumulus.StationOptions.UseRainForIsRaining == 2)
							{
								IsRaining = Convert.ToDouble(data["​rrain_piezo"], invNum) > 0;
								cumulus.IsRainingAlarm.Triggered = IsRaining;
							}
						}
						else
						{
							rain = data["yrain_piezo"];
							rRate = data["rrain_piezo"];
						}

						if (rRate == null)
						{
							// No rain rate, so we will calculate it
							calculaterainrate = true;
							rRate = "-999";
						}
						else
						{
							// we have a rain rate, so we will NOT calculate it
							calculaterainrate = false;
						}

						if (rain == null)
						{
							cumulus.LogDebugMessage($"{procName}: Error, missing rainfall");
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
						cumulus.LogErrorMessage($"{procName}: Error in Rain data - {ex.Message}");
						return "Failed: Error in rainfall data - " + ex.Message;
					}
				}

				// === Extra Temperature ===
				if (main || cumulus.EcowittExtraUseTempHum)
				{
					try
					{
						// temp[1-10]f
						ProcessExtraTemps(data, thisStation, recDate);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in extra temperature data - {ex.Message}");
					}
				}


				// === Extra Humidity ===
				if (main || cumulus.EcowittExtraUseTempHum)
				{
					try
					{
						// humidity[1-10]
						ProcessExtraHumidity(data, thisStation, recDate);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in extra humidity data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in solar data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in UV data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in Soil temp data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in Soil moisture data - {ex.Message}");
					}
				}

				// === Soil Moisture Raw ===
				if (main || cumulus.EcowittExtraUseSoilMoist)
				{
					try
					{
						// soilad[1-16]
						ProcessSoilMoistRaw(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Soil moisture Raw data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in Leaf wetness data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in User Temp data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in Air Quality data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in CO₂ data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in Lightning data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error in Leak data - {ex.Message}");
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
					cumulus.LogErrorMessage($"{procName}: Error in Battery data - {ex.Message}");
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
						cumulus.LogErrorMessage($"{procName}: Error calculating extra sensor dew points - {ex.Message}");
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
					cumulus.LogErrorMessage($"{procName}: Error extracting firmware version - {ex.Message}");
				}

				if (main)
				{
					// Do derived values after the primary values

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
							cumulus.LogWarningMessage($"{procName}: Error, missing dew point");
						}
						else
						{
							var val = ConvertTempFToUser(Convert.ToDouble(dewpnt, invNum));
							DoOutdoorDewpoint(val, recDate);
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"{procName}: Error in Dew point data - " + ex.Message);
						return "Failed: Error in dew point data - " + ex.Message;
					}


					// === Wind Chill ===
					try
					{
						// windchillf
						if (cumulus.StationOptions.CalculatedWC)
						{
							if (thisTemp != null && data["windspeedmph"] != null)
							{
								DoWindChill(0, recDate);
							}
							else
							{
								cumulus.LogWarningMessage($"{procName}: Insufficient data to calculate wind chill");
							}
						}
						else
						{
							var chill = data["windchillf"];
							if (chill == null)
							{
								cumulus.LogWarningMessage($"{procName}: Error, missing wind chill");
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
						cumulus.LogErrorMessage($"{procName}: Error in wind chill data - " + ex.Message);
						return "Failed: Error in wind chill data - " + ex.Message;
					}


					// === Humidex ===
					if (thisTemp != null && thisHum != null)
					{
						DoHumidex(recDate);
						DoCloudBaseHeatIndex(recDate);

						// === Apparent === - requires temp, hum, and windspeed
						if (data["windspeedmph"] != null)
						{
							DoApparentTemp(recDate);
							DoFeelsLike(recDate);
						}
						else
						{
							cumulus.LogWarningMessage($"{procName}: Insufficient data to calculate Apparent/Feels Like temps");
						}
					}
					else
					{
						cumulus.LogWarningMessage($"{procName}: Insufficient data to calculate Humidex and Apparent/Feels Like temps");
					}
				}

				thisStation.DoForecast(string.Empty, false);

				thisStation.UpdateStatusPanel(recDate);
				thisStation.UpdateMQTT();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"{procName}: Error - {ex.Message}");
				return "Failed: General error - " + ex.Message;
			}

			cumulus.LogDebugMessage($"{procName}: Complete");

			return "";
		}

		private void ProcessExtraTemps(NameValueCollection data, WeatherStation station, DateTime ts)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["temp" + i + "f"] != null)
				{
					if (i == cumulus.Gw1000PrimaryTHSensor)
					{
						station.DoOutdoorTemp(ConvertTempFToUser(Convert.ToDouble(data["temp" + i + "f"], invNum)), ts);
					}
					station.DoExtraTemp(ConvertTempFToUser(Convert.ToDouble(data["temp" + i + "f"], invNum)), i);
				}
			}
		}

		private void ProcessExtraHumidity(NameValueCollection data, WeatherStation station, DateTime ts)
		{
			for (var i = 1; i <= 10; i++)
			{
				if (data["humidity" + i] != null)
				{
					if (i == cumulus.Gw1000PrimaryTHSensor)
					{
						station.DoOutdoorHumidity(Convert.ToInt32(data["humidity" + i], invNum), ts);
					}
					station.DoExtraHum(Convert.ToDouble(data["humidity" + i], invNum), i);
				}
			}
		}

		private void ProcessSolar(NameValueCollection data, WeatherStation station, DateTime recDate)
		{
			if (data["solarradiation"] != null)
			{
				station.DoSolarRad((int) Convert.ToDouble(data["solarradiation"], invNum), recDate);
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

		private void ProcessSoilMoistRaw(NameValueCollection data, WeatherStation station)
		{
			for (var i = 1; i <= 16; i++)
			{
				if (data["soilad" + i] != null)
				{
					// Do nothing for now
					//station.DoSoilMoistureRaw(Convert.ToDouble(data["soilad" + i], invNum), i);
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
					if (cumulus.EcowittMapWN34[i] == 0)
					{
						station.DoUserTemp(ConvertTempFToUser(Convert.ToDouble(data["tf_ch" + i], invNum)), i);
					}
					else
					{
						station.DoSoilTemp(ConvertTempFToUser(Convert.ToDouble(data["tf_ch" + i], invNum)), cumulus.EcowittMapWN34[i]);
					}
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
				lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "1");
				lowBatt = lowBatt || (data["soilbatt" + i] != null && Convert.ToDouble(data["soilbatt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["pm25batt" + i] != null && data["pm25batt" + i] == "1");
				lowBatt = lowBatt || (data["leakbatt" + i] != null && data["leakbatt" + i] == "1");
				lowBatt = lowBatt || (data["tf_batt" + i] != null && Convert.ToDouble(data["tf_batt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["leaf_batt" + i] != null && Convert.ToDouble(data["leaf_batt" + i], invNum) <= 1.2);
			}
			for (var i = 5; i < 9; i++)
			{
				lowBatt = lowBatt || (data["batt" + i] != null && data["batt" + i] == "1");
				lowBatt = lowBatt || (data["soilbatt" + i] != null && Convert.ToDouble(data["soilbatt" + i], invNum) <= 1.2);
				lowBatt = lowBatt || (data["tf_batt" + i] != null && Convert.ToDouble(data["tf_batt" + i], invNum) <= 1.2);
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

		private void SetCustomServer(GW1000Api api, bool main)
		{
			cumulus.LogMessage("Reading Ecowitt Gateway Custom Server config");

			var customPath = main ? "/station/ecowitt" : "/station/ecowittextra";
			var customServer = main ? cumulus.EcowittLocalAddr : cumulus.EcowittExtraLocalAddr;
			var customPort = cumulus.wsPort;
			var customIntv = main ? cumulus.EcowittCustomInterval : cumulus.EcowittExtraCustomInterval;

			var data = api.DoCommand(GW1000Api.Commands.CMD_READ_CUSTOMIZED);

			// expected response
			// 0     - 0xff - header
			// 1     - 0xff - header
			// 2     - 0x2A - read customized
			// 3     - 0x?? - size of response
			// 4     - 0x?? - ID length
			// 5-len - ID field (max 40)
			// a     - 0x?? - password length
			// a1+   - Password field (max 40)
			// b     - 0x?? - server length
			// b1+   - Server field (max 64)
			// c-d   - Port Id (0-65536)
			// e-f   - Interval (5-600)
			// g     - 0x?? - Active (0-disabled, 1-active)
			// h  - 0x?? - checksum

			if (data != null)
			{
				try
				{
					//  ID field
					var id = Encoding.ASCII.GetString(data, 5, data[4]);
					// Password field
					var idx = 5 + data[4];
					var pass = Encoding.ASCII.GetString(data, idx + 1, data[idx]);
					// get server string
					idx += data[idx] + 1;
					var server = Encoding.ASCII.GetString(data, idx + 1, data[idx]);
					// get port id
					idx += data[idx] + 1;
					var port = GW1000Api.ConvertBigEndianUInt16(data, idx);
					// interval
					idx += 2;
					var intv = GW1000Api.ConvertBigEndianUInt16(data, idx);
					// type
					idx += 2;
					var type = data[idx] == 0 ? "Ecowitt" : "WUnderground";
					idx += 1;
					var active = data[idx];

					var data2 = api.DoCommand(GW1000Api.Commands.CMD_READ_USER_PATH);
					var ecPath = Encoding.ASCII.GetString(data2, 5, data2[4]);
					idx = 5 + data2[4];
					var wuPath = Encoding.ASCII.GetString(data2, idx + 1, data2[idx]);

					// Ecowitt actually sends data at interval + 1 seconds! :(
					customIntv -= 1;
					if (customIntv < 1)
						customIntv = 1;

					cumulus.LogMessage($"Ecowitt Gateway Custom Server config: Server={server}, Port={port}, Path={ecPath}, Interval={intv}, Protocol={type}, Enabled={active}");

					if (server != customServer || port != customPort || intv != customIntv || type != "Ecowitt" || active != 1)
					{
						cumulus.LogMessage("Ecowitt Gateway Custom Server config does not match the required config, reconfiguring it...");

						// Payload
						// 1    - ID length
						// n+   - ID
						// 1    - Password length
						// n+   - Password
						// 0	- Server length
						// 1+   - Server Name
						// a-b  - Port
						// c-d  - Interval
						// e    - Type (EC=0, WU=1)
						// f    - Active

						var length = 1 + id.Length + 1 + pass.Length; // id.len + id + pass.len + pass
						length += customServer.Length + 1; // Server name + length byte
						length += 2 + 2 + 1 + 1; // + port + interval + type + active
						var send = new byte[length];
						// set ID
						send[0] = (byte) id.Length;
						Encoding.ASCII.GetBytes(id).CopyTo(send, 1);

						// set password
						idx = 1 + id.Length;
						send[idx] = (byte) pass.Length;
						Encoding.ASCII.GetBytes(id).CopyTo(send, idx + 1);

						// set server string length
						idx += 1 + pass.Length;
						send[idx] = (byte) customServer.Length;
						// set server string
						Encoding.ASCII.GetBytes(customServer).CopyTo(send, idx + 1);
						idx += 1 + customServer.Length;
						// set the port id
						GW1000Api.ConvertUInt16ToLittleEndianByteArray((ushort) customPort).CopyTo(send, idx);
						// set the interval
						idx += 2;
						GW1000Api.ConvertUInt16ToLittleEndianByteArray((ushort) customIntv).CopyTo(send, idx);
						// set type
						idx += 2;
						send[idx] = 0;
						// set enabled
						idx += 1;
						send[idx] = 1;

						// do the config
						var retData = api.DoCommand(GW1000Api.Commands.CMD_WRITE_CUSTOMIZED, send);

						if (retData == null || retData[4] != 0)
						{
							cumulus.LogErrorMessage("Error - failed to set the Ecowitt Gateway config");
						}
						else
						{
							cumulus.LogMessage($"Set Ecowitt Gateway Custom Server config to: Server={customServer}, Port={customPort}, Interval={customIntv}, Protocol={0}, Enabled={1}");
							cumulus.LogMessage("Ecowitt Gateway Custom Server. Note, the set interval should be 1 less than the value set in the CMX configuration");
						}
					}

					// does the path need setting as well?
					if (ecPath != customPath)
					{
						ecPath = customPath;
						var path = new byte[ecPath.Length + wuPath.Length + 2];
						path[0] = (byte) ecPath.Length;
						Encoding.ASCII.GetBytes(ecPath).CopyTo(path, 1);
						idx = 1 + ecPath.Length;
						path[idx] = (byte) wuPath.Length;
						Encoding.ASCII.GetBytes(wuPath).CopyTo(path, idx + 1);

						var retData = api.DoCommand(GW1000Api.Commands.CMD_WRITE_USER_PATH, path);

						if (retData == null || retData[4] != 0)
						{
							cumulus.LogErrorMessage("Error - failed to set the Ecowitt Gateway Custom Server Path");
						}
						else
						{
							cumulus.LogMessage($"Set Ecowitt Gateway Custom Server Path={customPath}");
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error setting Ecowitt Gateway Custom Server config: " + ex.Message);
				}
			}
			else
			{
				cumulus.LogErrorMessage("Error reading Ecowitt Gateway Custom Server config, cannot configure it");
			}
		}

		private void ForwardData(string data, bool main)
		{
			var encoding = new UTF8Encoding(false);

			var forwarders = main || cumulus.EcowittExtraUseMainForwarders ? cumulus.EcowittForwarders : cumulus.EcowittExtraForwarders;

			for (int i = 0; i < forwarders.Length; i++)
			{
				if (!string.IsNullOrEmpty(forwarders[i]))
				{
					var url = forwarders[i];
					var idx = i;
					cumulus.LogDebugMessage("ForwardData: Forwarding Ecowitt data to: " + url);

					// we are just going to fire and forget
					_ = Task.Run(async () =>
					{
						try
						{
							using (var request = new HttpRequestMessage(HttpMethod.Post, url))
							{
								request.Content = new StringContent(data, encoding, "application/x-www-form-urlencoded");
								using (var response = await Cumulus.MyHttpClient.SendAsync(request))
									cumulus.LogDebugMessage($"ForwardData: Forward to {url}: Result: {response.StatusCode}");
							}
						}
						catch (Exception ex)
						{
							var msg = ex.Message + (ex.InnerException != null ? " (" + ex.InnerException.Message + ")" : "");
							cumulus.LogMessage($"ForwardData: Error forwarding to {url}: {msg}");
						}
					});
				}
			}
		}
	}
}
