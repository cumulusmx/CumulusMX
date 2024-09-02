using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using static System.Runtime.InteropServices.JavaScript.JSType;
using static CumulusMX.GW1000Api;


namespace CumulusMX
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
	internal class EcowittHttpApiStation : WeatherStation
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
	{
		private string ipaddr;
		private readonly string macaddr;
		private string deviceModel;
		private string deviceFirmware;
		private int updateRate = 10000; // 10 seconds by default
		private int lastMinute = -1;
		private int lastHour = -1;

		private readonly EcowittApi ecowittApi;
		private readonly EcowittLocalApi localApi;

		private int maxArchiveRuns = 1;

		private bool dataReceived = false;

		private readonly System.Timers.Timer tmrDataWatchdog;

		private readonly Task historyTask;
		private Task liveTask;

		private Version fwVersion;
		internal static readonly char[] dotSeparator = ['.'];
		internal static readonly string[] underscoreV = ["_V"];

		// local variables to hold data until all sensors have been read. Then they are set and derviced values calculated
		double windSpeedLast = -999, rainRateLast = -999, rainLast = -999, gustLast = -999;
		int windDirLast = -999;
		double outdoortemp = -999;
		double dewpoint;
		double windchill = -999;
		bool batteryLow = false;

		// We check the new value against what we have already, if older then ignore it!
		double newLightningDistance = 999;
		DateTime newLightningTime = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);


		public EcowittHttpApiStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Units.AirQualityUnitText = "µg/m³";
			cumulus.Units.SoilMoistureUnitText = "%";
			cumulus.Units.LeafWetnessUnitText = "%";

			// GW1000 does not provide 10 min average wind speeds
			cumulus.StationOptions.CalcuateAverageWindSpeed = true;

			// GW1000 does not provide an interval gust value, it gives us a 30 second high
			// so force using the wind speed for the average calculation
			cumulus.StationOptions.UseSpeedForAvgCalc = true;
			// also use it for the Latest value
			cumulus.StationOptions.UseSpeedForLatest = true;

			LightningTime = DateTime.MinValue;
			LightningDistance = -1.0;

			tmrDataWatchdog = new System.Timers.Timer();

			// GW1000 does not send DP, so force MX to calculate it
			cumulus.StationOptions.CalculatedDP = true;

			// does not provide a forecast, force MX to provide it
			cumulus.UseCumulusForecast = true;

			// GW1000 does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;

			if (cumulus.Gw1000PrimaryTHSensor == 0)
			{
				// We are using the primary T/H sensor
				cumulus.LogMessage("Using the default outdoor temp/hum sensor data");
			}
			else if (cumulus.Gw1000PrimaryTHSensor == 99)
			{
				// We are overriding the outdoor with the indoor T/H sensor
				cumulus.LogMessage("Overriding the default outdoor temp/hum data with Indoor temp/hum sensor");
				cumulus.StationOptions.CalculatedDP = true;
				cumulus.StationOptions.CalculatedWC = true;
			}
			else
			{
				// We are not using the primary T/H sensor so MX must calculate the wind chill as well
				cumulus.StationOptions.CalculatedDP = true;
				cumulus.StationOptions.CalculatedWC = true;
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

			ipaddr = cumulus.Gw1000IpAddress;
			macaddr = cumulus.Gw1000MacAddress;

			localApi = new EcowittLocalApi(cumulus);

			ecowittApi = new EcowittApi(cumulus, this);

			_ = CheckAvailableFirmware();

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			historyTask = Task.Run(getAndProcessHistoryData, cumulus.cancellationToken);
		}


		public override void Start()
		{
			lastMinute = DateTime.Now.Minute;

			// Start a broadcast watchdog to warn if messages are not being received
			tmrDataWatchdog.Elapsed += DataTimeout;
			tmrDataWatchdog.Interval = 1000 * 30; // timeout after 30 seconds
			tmrDataWatchdog.AutoReset = true;
			tmrDataWatchdog.Start();


			// just incase we did not catch-up any history
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			cumulus.LogMessage("Starting Ecowitt Local HTTP API");

			cumulus.StartTimersAndSensors();

			liveTask = Task.Run(() =>
			{
				try
				{
					var dataLastRead = DateTime.MinValue;
					double delay;

					while (!cumulus.cancellationToken.IsCancellationRequested)
					{
						var rawData = localApi.GetLiveData(cumulus.cancellationToken);
						dataLastRead = DateTime.Now;

						// process the common_list sensors
						ProcessCommonList(rawData.common_list, dataLastRead);

						// process base station values
						ProcessWh25(rawData.wh25, dataLastRead);

						// process rain values
						if (cumulus.Gw1000PrimaryRainSensor == 0 && rawData.rain != null)
						{
							ProcessRain(rawData.rain, dataLastRead);
							// TODO: battery status for piezo
						}
						else if (cumulus.Gw1000PrimaryRainSensor == 1 && rawData.piezoRain != null)
						{
							ProcessRain(rawData.piezoRain, dataLastRead);
							// TODO: battery status for tipper
						}

						if (rawData.lightning != null)
						{
							ProcessLightning(rawData.lightning, dataLastRead);
						}

						if (rawData.co2 != null)
						{
							ProcessCo2(rawData.co2, dataLastRead);
						}

						if (rawData.ch_pm25 != null)
						{
							ProcessChPm25(rawData.ch_pm25);
						}

						if (rawData.ch_leak != null)
						{
							ProcessLeak(rawData.ch_leak);
						}

						if (rawData.ch_aisle != null)
						{
							ProcessExtraTempHum(rawData.ch_aisle, dataLastRead);
						}

						if (rawData.ch_temp != null)
						{
							ProcessUserTemp(rawData.ch_temp, dataLastRead);
						}

						if (rawData.ch_soil != null)
						{
							ProcessSoilMoisture(rawData.ch_soil);
						}

						// TODO: Soil Temperature sensors
						//if (rawData.ch_ ??? != null)
						//{
						//	ProcessSoilTemp(rawData.ch_ ???);
						//}

						if (rawData.ch_leaf != null)
						{
							ProcessLeafWet(rawData.ch_leaf);
						}

						// Now do the stuff that requires more than one input parameter

						// Only set the lightning time/distance if it is newer than what we already have - the GW1000 seems to reset this value
						if (newLightningTime > LightningTime)
						{
							LightningTime = newLightningTime;
							if (newLightningDistance < 999)
								LightningDistance = newLightningDistance;
						}

						// Process outdoor temperature here, as GW1000 currently does not supply Dew Point so we have to calculate it in DoOutdoorTemp()
						if (outdoortemp > -999)
							DoOutdoorTemp(ConvertUnits.TempCToUser(outdoortemp), dataLastRead);

						// Same for extra T/H sensors
						for (var i = 1; i <= 8; i++)
						{
							if (ExtraHum[i] > 0)
							{
								var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[i]), ExtraHum[i]);
								ExtraDewPoint[i] = ConvertUnits.TempCToUser(dp);
							}
						}

						if (gustLast > -999 && windSpeedLast > -999 && windDirLast > -999)
						{
							DoWind(gustLast, windDirLast, windSpeedLast, dataLastRead);
						}

						if (rainLast > -999 && rainRateLast > -999)
						{
							DoRain(rainLast, rainRateLast, dataLastRead);
						}

						if (outdoortemp > -999)
						{
							DoWindChill(windchill, dataLastRead);
							DoApparentTemp(dataLastRead);
							DoFeelsLike(dataLastRead);
							DoHumidex(dataLastRead);
							DoCloudBaseHeatIndex(dataLastRead);

							if (cumulus.StationOptions.CalculateSLP)
							{
								var abs = cumulus.Calib.Press.Calibrate(StationPressure);
								var slp = MeteoLib.GetSeaLevelPressure(AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToMB(abs), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
								DoPressure(ConvertUnits.PressMBToUser(slp), dataLastRead);
							}
						}

						DoForecast("", false);

						cumulus.BatteryLowAlarm.Triggered = batteryLow;

						UpdateStatusPanel(dataLastRead);
						UpdateMQTT();

						dataReceived = true;
						DataStopped = false;
						cumulus.DataStoppedAlarm.Triggered = false;

						var minute = DateTime.Now.Minute;
						if (minute != lastMinute)
						{
							lastMinute = minute;

							// every day dump the clock drift at midday each day
							if (minute == 0 && DateTime.Now.Hour == 12)
							{
								GetSystemInfo(true);
							}

							var hour = DateTime.Now.Hour;
							if (lastHour != hour)
							{
								lastHour = hour;

								if (hour == 13)
								{
									var fw = GetFirmwareVersion();
									if (fw != "???")
									{
										GW1000FirmwareVersion = fw;
										deviceModel = GW1000FirmwareVersion.Split('_')[0];
										deviceFirmware = GW1000FirmwareVersion.Split('_')[1];

										var fwString = GW1000FirmwareVersion.Split(underscoreV, StringSplitOptions.None);
										if (fwString.Length > 1)
										{
											fwVersion = new Version(fwString[1]);
										}
										else
										{
											// failed to get the version, lets assume it's fairly new
											fwVersion = new Version("1.6.5");
										}
									}

									_ = CheckAvailableFirmware();
								}
							}
						}

						delay = Math.Min(updateRate - (dataLastRead - DateTime.Now).TotalMilliseconds, updateRate);

						if (cumulus.cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(delay)))
						{
							break;
						}
					}
				}
				// Catch the ThreadAbortException
				catch (ThreadAbortException)
				{
					//do nothing
				}
				finally
				{
					cumulus.LogMessage("Local API task ended");
				}
			}, cumulus.cancellationToken);
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing connection");
			try
			{
				tmrDataWatchdog.Stop();
				StopMinuteTimer();
				Task.WaitAll(historyTask, liveTask);
			}
			catch
			{
				//do nothing
			}
		}


		public override void getAndProcessHistoryData()
		{
			Cumulus.SyncInit.Wait();

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
					do
					{
						GetHistoricData();
						archiveRun++;
					} while (archiveRun < maxArchiveRuns && !cumulus.cancellationToken.IsCancellationRequested);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Exception occurred reading archive data: " + ex.Message);
				}

				// get the station list
				ecowittApi.GetStationList(true, cumulus.EcowittMacAddress, cumulus.cancellationToken);
			}

			_ = Cumulus.SyncInit.Release();

			if (cumulus.cancellationToken.IsCancellationRequested)
			{
				return;
			}

			StartLoop();
		}

		private void GetHistoricData()
		{
			cumulus.LogMessage("GetHistoricData: Starting Historic Data Process");

			// add one minute to the time to avoid duplicating the last log entry
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

		public override string GetEcowittCameraUrl()
		{
			if (!string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
			{
				try
				{
					EcowittCameraUrl = ecowittApi.GetCurrentCameraImageUrl(EcowittCameraUrl, cumulus.cancellationToken);
					return EcowittCameraUrl;
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "Error runing Ecowitt Camera URL");
				}
			}

			return null;
		}

		public override string GetEcowittVideoUrl()
		{
			if (!string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
			{
				try
				{
					EcowittVideoUrl = ecowittApi.GetLastCameraVideoUrl(EcowittVideoUrl, cumulus.cancellationToken);
					return EcowittVideoUrl;
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "Error running Ecowitt Video URL");
				}
			}

			return null;
		}


		private string GetFirmwareVersion()
		{
			var response = "???";
			return response;
		}

		private async Task CheckAvailableFirmware()
		{
			if (deviceModel == null)
			{
				cumulus.LogMessage("Device Model not determined, firmware check skipped.");
				return;
			}

			if (EcowittApi.FirmwareSupportedModels.Contains(deviceModel[..6]))
			{
				_ = await ecowittApi.GetLatestFirmwareVersion(deviceModel, cumulus.EcowittMacAddress, deviceFirmware, cumulus.cancellationToken);
			}
			else
			{
				var retVal = ecowittApi.GetSimpleLatestFirmwareVersion(deviceModel, cumulus.cancellationToken).Result;
				if (retVal != null)
				{
					var verVer = new Version(retVal[0]);
					if (fwVersion < verVer)
					{
						cumulus.FirmwareAlarm.LastMessage = $"A new firmware version is available: {retVal[0]}.\nChange log:\n{string.Join('\n', retVal[1].Split(';'))}";
						cumulus.FirmwareAlarm.Triggered = true;
						cumulus.LogWarningMessage($"FirmwareVersion: Latest Version {retVal[0]}, Change log:\n{string.Join('\n', retVal[1].Split(';'))}");
					}
					else
					{
						cumulus.FirmwareAlarm.Triggered = false;
						cumulus.LogDebugMessage($"FirmwareVersion: Already on the latest Version {retVal[0]}");
					}
				}
			}
		}

		private void GetSensorIdsNew()
		{
			cumulus.LogMessage("Reading sensor ids");

		}

		private bool PrintSensorInfoNew(byte[] data, int idx)
		{
			var batteryLow = false;

			return batteryLow;
		}

		private void ProcessCommonList(EcowittLocalApi.commonSensor[] sensors, DateTime dateTime)
		{
			cumulus.LogDebugMessage($"ProcessCommonList: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					switch (sensor.id)
					{
						case "0x02": //Outdoor Temperature
							if (sensor.valDbl.HasValue && cumulus.Gw1000PrimaryTHSensor == 0)
							{
								// do not process temperature here as if "MX calculates DP" is enabled, we have not yet read the humidity value. Have to do it at the end.
								outdoortemp = sensor.valDbl.Value;
								outdoortemp = sensor.unit == "C" ? ConvertUnits.TempCToUser(outdoortemp) : ConvertUnits.TempFToUser(outdoortemp);
							}
							break;
						case "0x03": //Dew point
							if (sensor.valDbl.HasValue && cumulus.Gw1000PrimaryTHSensor == 0 && !cumulus.StationOptions.CalculatedDP)
							{
								var temp = sensor.valDbl.Value;
								temp = sensor.unit == "C" ? ConvertUnits.TempCToUser(temp) : ConvertUnits.TempFToUser(temp);

								DoOutdoorDewpoint(temp, dateTime);
							}
							break;
						case "3": //Feels like
								  // do nothing with this for now - MX calcuates feels like
							break;
						case "0x04": //Wind chill
							if (sensor.valDbl.HasValue && cumulus.Gw1000PrimaryTHSensor == 0)
							{
								windchill = sensor.valDbl.Value;
								windchill = sensor.unit == "C" ? ConvertUnits.TempCToUser(windchill) : ConvertUnits.TempFToUser(windchill);
							}
							break;
						case "0x05": //Heat index
									 // cumulus calculates this
							break;
						case "0x07": //Outdoor Humidity (%)
							if (sensor.valInt.HasValue && cumulus.Gw1000PrimaryTHSensor == 0)
							{
								DoOutdoorHumidity(sensor.valInt.Value, dateTime);
							}
							break;
						case "0x0A": //Wind Direction (360°)
							if (sensor.valInt.HasValue)
							{
								windDirLast = sensor.valInt.Value;
							}
							break;
						case "0x0B": //Wind Speed (val unit)
							var arr = sensor.val.Split(' ');
							if (arr.Length == 2 && double.TryParse(arr[0], out var valDbl))
							{
								var spd = arr[1] switch
								{
									"km/h" => ConvertUnits.WindKPHToUser(valDbl),
									"m/s" => ConvertUnits.WindMSToUser(valDbl),
									"mph" => ConvertUnits.WindMPHToUser(valDbl),
									"knots" => ConvertUnits.WindKnotsToUser(valDbl),
									_ => -999
								};

								if (spd >= 0)
								{
									windSpeedLast = spd;
								}
							}
							break;
						case "0x0C": // Gust speed (val unit)
							arr = sensor.val.Split(' ');
							if (arr.Length == 2 && double.TryParse(arr[0], out valDbl))
							{
								var spd = arr[1] switch
								{
									"km/h" => ConvertUnits.WindKPHToUser(valDbl),
									"m/s" => ConvertUnits.WindMSToUser(valDbl),
									"mph" => ConvertUnits.WindMPHToUser(valDbl),
									"knots" => ConvertUnits.WindKnotsToUser(valDbl),
									_ => -999
								};

								if (spd >= 0)
								{
									gustLast = spd;
								}
							}
							break;
						case "0x15": //Light (value unit)
							arr = sensor.val.Split(' ');
							if (arr.Length == 2 && double.TryParse(arr[0], out valDbl))
							{
								var light = arr[1] switch
								{
									"fc" => valDbl * 0.015759751708199,
									"lux" => valDbl * cumulus.SolarOptions.LuxToWM2, // convert Lux to W/m² - approximately!
									"W/m2" => valDbl,
									_ => -999
								};

								LightValue = valDbl;
								if (light >= 0)
								{
									DoSolarRad((int) light, dateTime);
								}
							}
							break;
						case "0x17": //UVI (0-15 index)
							if (sensor.valDbl.HasValue)
							{
								DoUV(sensor.valDbl.Value, dateTime);
							}
							break;

						case "0x19": // max wind today (value unit)
									 // not used
							break;

						default:
							cumulus.LogDebugMessage($"Error: Unknown common_list sensor id found = {sensor.id}");
							break;
					}
				}
				catch(Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessCommonList: Error processing sensor id {sensor.id}");
				}
			}

			// Some debugging info
			cumulus.LogDebugMessage($"LiveData: Wind Decode >> Last={windSpeedLast:F1}, LastDir={windDirLast}, Gust={gustLast:F1}, (MXAvg={WindAverage:F1})");

		}

		private void ProcessWh25(EcowittLocalApi.wh25Sensor[] sensors, DateTime dateTime)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				// Indoor Temperature
				try
				{
					var temp = sensor.intemp;
					temp = sensor.unit == "C" ? ConvertUnits.TempCToUser(temp) : ConvertUnits.TempFToUser(temp);

					// user has mapped indoor temp to outdoor temp
					if (cumulus.Gw1000PrimaryTHSensor == 99)
					{
						// do not process temperature here as if "MX calculates DP" is enabled, we have not yet read the humidity value. Have to do it at the end.
						DoOutdoorTemp(temp, dateTime);
					}
					DoIndoorTemp(temp);
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessWh25: Error processing indoor temp}");
				}

				// Indoor Humidity
				try
				{
					var hum = sensor.inhumiInt ?? 0;
					// user has mapped indoor hum to outdoor hum
					if (cumulus.Gw1000PrimaryTHSensor == 99)
					{
						DoOutdoorHumidity(hum, dateTime);
					}
					DoIndoorHumidity(hum);
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessWh25: Error processing indoor humidity}");
				}

				// Pressure
				try
				{
					if (sensor.rel != null && !cumulus.StationOptions.CalculateSLP)
					{
						var arr = sensor.rel.Split(' ');
						if (arr.Length == 2 && double.TryParse(arr[0], out var val))
						{
							var slp = arr[1] switch
							{
								"hPa" => ConvertUnits.PressKPAToUser(val / 10),
								"inHg" => ConvertUnits.PressINHGToUser(val),
								"mmHg" => ConvertUnits.PressINHGToUser(val * 25.4),
								_ => -999,
							};

							if (slp > 0)
							{
								DoPressure(slp, dateTime);
							}
						}
					}
					if (sensor.abs != null)
					{
						var arr = sensor.abs.Split(' ');
						if (arr.Length == 2 && double.TryParse(arr[0], out var val))
						{
							var abs = arr[1] switch
							{
								"hPa" => ConvertUnits.PressKPAToUser(val / 10),
								"inHg" => ConvertUnits.PressINHGToUser(val),
								"mmHg" => ConvertUnits.PressINHGToUser(val * 25.4),
								_ => -999
							};

							if (abs > 0)
							{
								StationPressure = abs;
								AltimeterPressure = ConvertUnits.PressMBToUser(MeteoLib.StationToAltimeter(abs, AltitudeM(cumulus.Altitude)));
								// Leave calculate SLP until the end as it depends on temperature
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessWh25: Error processing pressure}");
				}

				// TODO: battery status
			}
		}

		private void ProcessRain(EcowittLocalApi.commonSensor[] sensors, DateTime dateTime)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					switch (sensor.id)
					{
						case "0x0D":
							//Rain Event (val unit)
							try
							{
								var arr = sensor.val.Split(' ');
								if (arr.Length == 2 && double.TryParse(arr[0], out var val))
								{
									var evnt = arr[1] switch
									{
										"mm" => ConvertUnits.RainMMToUser(val),
										"in" => ConvertUnits.RainINToUser(val),
										_ => -999
									};

									if (evnt >= 0)
									{
										StormRain = evnt;
									}
								}
							}
							catch (Exception ex)
							{
								//TODO: log a message
							}
							break;

						case "0x0E":
							//Rain Rate (val unit/h)
							try
							{
								var arr = sensor.val.Split(' ');
								if (arr.Length == 2 && double.TryParse(arr[0], out var val))
								{
									var rate = arr[1] switch
									{
										"mm/Hr" => ConvertUnits.RainMMToUser(val),
										"in/Hr" => ConvertUnits.RainINToUser(val),
										_ => -999
									};

									if (rate >= 0)
									{
										rainRateLast = rate;
									}
								}
							}
							catch (Exception ex)
							{
								//TODO: log a message
							}
							break;

						case "0x13":
							//Rain Year (val unit)
							try
							{
								// TODO: battery status

								var arr = sensor.val.Split(' ');
								if (arr.Length == 2 && double.TryParse(arr[0], out var val))
								{
									var yr = arr[1] switch
									{
										"mm" => ConvertUnits.RainMMToUser(val),
										"in" => ConvertUnits.RainINToUser(val),
										_ => -999
									};

									if (yr >= 0)
									{
										rainLast = yr;
									}
								}
							}
							catch (Exception ex)
							{
								//TODO: log a message
							}
							break;

						case "0x10": // Rain day
						case "0x11": // Rain week
						case "0x12": // Rain month
									 // do nothing
							break;

						default:
							cumulus.LogDebugMessage($"Error: Unknown rain sensor id found = {sensor.id}");
							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessRain: Error processing sensor id {sensor.id}");
				}

			}
		}

		private void ProcessLightning(EcowittLocalApi.lightningSensor[] sensors, DateTime dateTime)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				try
				{
					var sensor = sensors[i];

					//Lightning dist (1-40km)
					if (sensor.distanceVal.HasValue && sensor.distanceUnit != null)
					{
						// Sends a default value of 255km until the first strike is detected
						if (sensor.distanceVal.Value > 254.9)
						{
							newLightningDistance = 999;
						}
						else
						{
							newLightningDistance = ConvertUnits.KmtoUserUnits(sensor.distanceVal.Value);
						}
					}

					//Lightning time (UTC)
					if (!string.IsNullOrEmpty(sensor.timestamp))
					{
						// oh my god, it sends the time as "MM/dd/yyyy HH: mm: ss"
						// TODO: what is default time if not strikes detecetd yet?
						var arr = sensor.timestamp.Split(' ');
						var date = arr[0].Split('/');

						//if (sensor.timestamp == "default string")
						//{
						//	newLightningTime = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
						//}
						//else
						//{
							newLightningTime = new DateTime(
								int.Parse(date[2]),
								int.Parse(date[0]),
								int.Parse(date[1]), 
								int.Parse(arr[1][0..^1]),
								int.Parse(arr[2][0..^1]),
								int.Parse(arr[3][0..^1]),
								0, DateTimeKind.Utc);
						//}
					}

					//Lightning strikes today
					if (sensor.count.HasValue)
					{
						if (sensor.count == 0 && dateTime.Minute == 59 && dateTime.Hour == 23)
						{
							// Ecowitt clock drift - if the count resets in the minute before midnight, ignore it until after midnight
						}
						else
						{
							LightningStrikesToday = sensor.count.Value;
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessLightning: Error");
				}
			}
		}

		private void ProcessCo2(EcowittLocalApi.co2Sensor[] sensors, DateTime dateTime)
		{
			cumulus.LogDebugMessage("WH45 CO₂: Decoding...");

			for (var i = 0; i < sensors.Length; i++)
			{
				try
				{
					var sensor = sensors[i];

					if (sensor.temp.HasValue && !string.IsNullOrEmpty(sensor.unit))
					{
						CO2_temperature = sensor.unit == "C" ? ConvertUnits.TempCToUser(sensor.temp.Value) : ConvertUnits.TempFToUser(sensor.temp.Value);
					}
					if (sensor.humidityVal.HasValue)
					{
						// humidty sent as "value%"
						CO2_humidity = sensor.humidityVal.Value;
					}
					if (sensor.PM25.HasValue)
					{
						CO2_pm2p5 = sensor.PM25.Value;
					}
					if (sensor.PM10.HasValue)
					{
						CO2_pm10 = sensor.PM10.Value;
					}
					// HTTP protocol does not send a pm 24 hour values :( On useless AQI values
					if (sensor.CO2.HasValue)
					{
						CO2 = sensor.CO2.Value;
					}
					if (sensor.CO2_24H.HasValue)
					{
						CO2_24h = sensor.CO2_24H.Value;
					}

					// TODO: Battery status
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessCo2: Error");
				}
			}

			CO2_pm2p5_aqi = GetAqi(AqMeasure.pm2p5, CO2_pm2p5);
			CO2_pm10_aqi = GetAqi(AqMeasure.pm10, CO2_pm10);
		}

		private void ProcessChPm25(EcowittLocalApi.ch_pm25Sensor[] sensors)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					if (sensor.channel.HasValue && sensor.PM25.HasValue)
					{
						DoAirQuality(sensor.PM25.Value, sensor.channel.Value);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessChPm25: Error");
				}

				// TODO: Battery status
			}
		}

		private void ProcessLeak(EcowittLocalApi.ch_leakSensor[] sensors)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					if (sensor.channel.HasValue && !string.IsNullOrEmpty(sensor.status))
					{
						var val = sensor.status == "NORMAL" ? 0 : 1;
						DoLeakSensor(val, sensor.channel.Value);

						// TODO: Battery status
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessLeak: Error processing channel {sensor.channel.Value}");
				}

			}
		}


		private void ProcessExtraTempHum(EcowittLocalApi.tempHumSensor[] sensors, DateTime dateTime)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					if (sensor.temp.HasValue)
					{
						DoExtraTemp(sensor.temp.Value, sensor.channel);
					}

					if (sensor.humidityVal.HasValue)
					{
						if (cumulus.Gw1000PrimaryTHSensor == sensor.channel)
						{
							DoOutdoorHumidity(sensor.humidityVal.Value, dateTime);
						}
						DoExtraHum(sensor.humidityVal.Value, sensor.channel);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessExtraTempHum: Error processind sensor channel {sensor.channel}");
				}

				// TODO: battery status
			}
		}

		private void ProcessUserTemp(EcowittLocalApi.tempHumSensor[] sensors, DateTime dateTime)
		{
			// user temp = WH34 8 channel Soil or Water temperature sensors

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					if (sensor.temp.HasValue)
					{
						var val = sensor.unit == "C" ? ConvertUnits.TempCToUser(sensor.temp.Value) : ConvertUnits.TempFToUser(sensor.temp.Value);
						if (cumulus.EcowittMapWN34[sensor.channel] == 0) // false = user temp, true = soil temp
						{
							DoUserTemp(val, sensor.channel);
						}
						else
						{
							DoSoilTemp(val, cumulus.EcowittMapWN34[sensor.channel]);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessUserTemp: Error processing sensor channel {sensor.channel}");
				}

				// TODO: Battery status
			}
		}

		private void ProcessSoilMoisture(EcowittLocalApi.tempHumSensor[] sensors)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				try
				{
					var sensor = sensors[i];

					if (sensor.humidityVal.HasValue)
					{
						DoSoilMoisture(sensor.humidityVal.Value, sensor.channel);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessSoilMoisture: Error");
				}

				// TODO: Battery status
			}
		}


		private void ProcessSoilTemp(EcowittLocalApi.tempHumSensor[] sensors)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];
				
				try
				{

					if (sensor.temp.HasValue)
					{
						var val = sensor.unit == "C" ? ConvertUnits.TempCToUser(sensor.temp.Value) : ConvertUnits.TempFToUser(sensor.temp.Value);
						DoSoilTemp(val, sensor.channel);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessSoilTemp: Error processing channel {sensor.channel}");
				}

				// TODO: Battery status
			}

		}

		private void ProcessLeafWet(EcowittLocalApi.tempHumSensor[] sensors)
		{
			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					if (sensor.humidityVal.HasValue)
					{
						DoLeafWetness(sensor.humidityVal.Value, sensor.channel);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessLeafWet: Error processing channel {sensor.channel}");
				}

				// TODO: Battery status
			}
		}


		private void GetSystemInfo(bool driftOnly)
		{
			cumulus.LogMessage("Reading Ecowitt system info");

		}


		private bool DoWH34BatteryStatus(byte[] data, int index)
		{
			// No longer used in firmware 1.6.0+
			cumulus.LogDebugMessage("WH34 battery status...");
			var str = "wh34>" +
				" ch1=" + TestBattery3(data[index + 1]) +
				" ch2=" + TestBattery3(data[index + 2]) +
				" ch3=" + TestBattery3(data[index + 3]) +
				" ch4=" + TestBattery3(data[index + 4]) +
				" ch5=" + TestBattery3(data[index + 5]) +
				" ch6=" + TestBattery3(data[index + 6]) +
				" ch7=" + TestBattery3(data[index + 7]) +
				" ch8=" + TestBattery3(data[index + 8]);

			cumulus.LogDebugMessage(str);

			return str.Contains("Low");
		}

		private static string TestBattery1(byte value, byte mask)
		{
			return (value & mask) == 0 ? "OK" : "Low";
		}

		private static string TestBattery3(byte value)
		{
			if (value == 6)
				return "DC";
			if (value == 9)
				return "OFF";
			return value > 1 ? "OK" : "Low";
		}

		private static string TestBattery10(byte value)
		{
			// consider 1.2V as low
			if (value == 255)
				return "n/a";

			return value > 12 ? "OK" : "Low";
		}

		private static double TestBattery10V(byte value)
		{
			return value / 10.0;
		}

		private static string TestBatteryWh40(byte value, double volts)
		{
			if (value == 255)
				return "n/a";

			return volts > 1.2 ? "OK" : "Low";
		}

		private void DataTimeout(object source, ElapsedEventArgs e)
		{
			if (dataReceived)
			{
				dataReceived = false;
				DataStopped = false;
				cumulus.DataStoppedAlarm.Triggered = false;
			}
			else
			{
				cumulus.LogErrorMessage($"ERROR: No data received from the GW1000 for {tmrDataWatchdog.Interval / 1000} seconds");
				if (!DataStopped)
				{
					DataStoppedTime = DateTime.Now;
					DataStopped = true;
				}
				cumulus.DataStoppedAlarm.LastMessage = $"No data received from the GW1000 for {tmrDataWatchdog.Interval / 1000} seconds";
				cumulus.DataStoppedAlarm.Triggered = true;
			}
		}
	}
}
