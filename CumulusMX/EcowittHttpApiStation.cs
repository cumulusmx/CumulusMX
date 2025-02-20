using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using static System.Collections.Specialized.BitVector32;
using Swan.Parsers;

using static CumulusMX.EcowittApi;
using Org.BouncyCastle.Ocsp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ServiceStack.Text;
using System.Collections;


namespace CumulusMX
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
	internal class EcowittHttpApiStation : WeatherStation
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
	{
		private static readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;

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

		internal static readonly char[] dotSeparator = ['.'];
		internal static readonly string[] underscoreV = ["_V"];

		// local variables to hold data until all sensors have been read. Then they are set and derived values calculated
		double windSpeedLast = -999, rainRateLast = -999, rainLast = -999, gustLast = -999;
		int windDirLast = -999;
		double outdoortemp = -999;
		double windchill = -999;
		bool batteryLow = false;

		// We check the new value against what we have already, if older then ignore it!
		double newLightningDistance = 999;
		DateTime newLightningTime = DateTime.MinValue;


		public EcowittHttpApiStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Units.AirQualityUnitText = "µg/m³";
			Array.Fill(cumulus.Units.SoilMoistureUnitText, "%");
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

			localApi = new EcowittLocalApi(cumulus);

			ecowittApi = new EcowittApi(cumulus, this);

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

			// Get the sensor list
			GetSensorIds(false).Wait();

			// Check firmware
			try
			{
				_ = localApi.CheckForUpgrade(cumulus.cancellationToken).Result;
				GW1000FirmwareVersion = localApi.GetVersion(cumulus.cancellationToken).Result;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Error checking for firmware upgrade/version");
				GW1000FirmwareVersion = "unknown";
			}

			liveTask = Task.Run(() =>
			{
				var excepMsg = "unknown error";

				try
				{
					DateTime dataLastRead = DateTime.Now;
					double delay;

					while (!cumulus.cancellationToken.IsCancellationRequested)
					{
						if (!DayResetInProgress)
						{
							var rawData = localApi.GetLiveData(cumulus.cancellationToken);
							if (rawData is not null)
							{
								dataLastRead = DateTime.Now;

								// process the common_list sensors
								if (rawData.common_list != null)
								{
									ProcessCommonList(rawData.common_list, dataLastRead);
								}

								// process base station values
								if (rawData.wh25 != null)
								{
									ProcessWh25(rawData.wh25, dataLastRead);
								}

								// process rain values
								if (cumulus.Gw1000PrimaryRainSensor == 0 && rawData.rain != null)
								{
									ProcessRain(rawData.rain, false);
								}

								if ((cumulus.Gw1000PrimaryRainSensor == 1 || (cumulus.Gw1000PrimaryRainSensor == 0 && cumulus.EcowittIsRainingUsePiezo)) && rawData.piezoRain != null)
								{
									// if we are using piezo as the primary rain sensor
									// or using the tipper at the primary, but want to use the piezo srain value for IsRaining
									ProcessRain(rawData.piezoRain, cumulus.Gw1000PrimaryRainSensor == 0 && cumulus.EcowittIsRainingUsePiezo);
								}

								if (rawData.lightning != null)
								{
									ProcessLightning(rawData.lightning, dataLastRead);
								}

								if (rawData.co2 != null)
								{
									ProcessCo2(rawData.co2);
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
									ProcessUserTemp(rawData.ch_temp);
								}

								if (rawData.ch_soil != null)
								{
									ProcessSoilMoisture(rawData.ch_soil);
								}

								if (rawData.ch_leaf != null)
								{
									ProcessLeafWet(rawData.ch_leaf);
								}

								if (rawData.ch_lds != null)
								{
									ProcessLds(rawData.ch_lds);
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
									if (ExtraHum[i].HasValue && ExtraTemp[i].HasValue)
									{
										var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(ExtraTemp[i].Value), ExtraHum[i].Value);
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
										var slp = MeteoLib.GetSeaLevelPressure(AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToMB(StationPressure), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
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
								LastDataReadTime = dataLastRead;

								var minute = DateTime.Now.Minute;
								if (minute != lastMinute)
								{
									lastMinute = minute;

									// at the start of every 20 minutes to trigger battery status check
									if ((minute % 20) == 0 && !cumulus.cancellationToken.IsCancellationRequested)
									{
										_ = GetSensorIds(true);
									}

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
											_ = localApi.CheckForUpgrade(cumulus.cancellationToken);
											GW1000FirmwareVersion = localApi.GetVersion(cumulus.cancellationToken).Result;
										}
									}
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
				catch (ThreadAbortException ex)
				{
					//do nothing
					excepMsg = ex.Message;
				}
				catch (Exception ex)
				{
					excepMsg = ex.Message;
				}
				finally
				{
					if (cumulus.cancellationToken.IsCancellationRequested)
					{
						cumulus.LogMessage("Local API task cancelled by request");
					}
					else
					{
						cumulus.LogCriticalMessage("Local API task ended unexpectedly: " + excepMsg);
					}
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

			// Use SDcard or ecowitt.net
			if (cumulus.EcowittUseSdCard)
			{
				// do this until we are fully caught-up, or we have done it three times
				int archiveRun = 0;
				while (GetHistoricDataSdCard() && archiveRun < 3)
				{
					archiveRun++;
				};
			}
			else if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogWarningMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
			}
			else
			{
				int archiveRun = 0;

				try
				{
					do
					{
						GetHistoricDataOnline();
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

		private void GetHistoricDataOnline()
		{
			cumulus.LogMessage("GetHistoricDataOnline: Starting Historic Data Process");

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

		/// <summary>
		/// Retrieves and processes historic data from the SD card of the weather station.
		/// This method performs the following steps:
		/// 1. Logs the start of the historic data process.
		/// 2. Retrieves the list of log files from the SD card.
		/// 3. Sorts the files into base files and extra files.
		/// 4. Processes the base files to extract and store data.
		/// 5. Merges extra sensor data into the existing data.
		/// 6. Processes the combined data, handling day rollover, midnight rain reset, and other periodic tasks.
		/// 7. Applies the processed data to the system and updates various metrics and logs.
		/// </summary>
		/// <returns>Returns true if there could be more data to process, otherwise false.</returns>
		private bool GetHistoricDataSdCard()
		{
			cumulus.LogMessage("GetHistoricDataSdCard: Starting Historic Data Process");
			Cumulus.LogConsoleMessage("Starting historic data catchup from SD card");

			// add one minute to the time to avoid duplicating the last log entry
			var startTime = cumulus.LastUpdateTime.AddMinutes(1);

			var files = localApi.GetSdFileList(startTime, cumulus.cancellationToken).Result;

			if (files == null || files.Count == 0)
			{
				cumulus.LogMessage("GetHistoricDataSdCard: No log files returned from localApi.GetSdFileList(), exiting catch-up");
				Cumulus.LogConsoleMessage("No log files returned from your station, exiting catch-up");
				return false;
			}

			var baseFiles = new List<string>();
			var extraFiles = new List<string>();

			cumulus.LogMessage("GetHistoricDataSdCard: Sort the files into base files and extra files");

			foreach (var file in files)
			{
				if (cumulus.cancellationToken.IsCancellationRequested)
				{
					cumulus.LogMessage("GetHistoricDataSdCard: Cancellation requested");
					return false;
				}

				cumulus.LogDebugMessage($"GetHistoricDataSdCard: Checking file {file} with prefix {file[..6]}");

				// split into two lists
				// filename is YYYYMM[A-Z].csv  or  YYYMMAllSensors_[A-Z].csv
				if (file.Contains("All"))
				{
					cumulus.LogMessage("GetHistoricDataSdCard: Adding base file " + file);
					extraFiles.Add(file);
				}
				else
				{
					cumulus.LogMessage("GetHistoricDataSdCard: Adding extra file " + file);
					baseFiles.Add(file);
				}
			}

			// sort the lists - just in case!
			cumulus.LogDebugMessage("GetHistoricDataSdCard: Sorting the file lists");
			baseFiles.Sort();
			extraFiles.Sort();

			var buffer = new SortedList<DateTime, HistoricData>();

			// process the base files first

			cumulus.LogDebugMessage($"GetHistoricDataSdCard: Processing {baseFiles.Count} base files");
			Cumulus.LogConsoleMessage("Preprocessing the base sensor file(s)...");

			foreach (var file in baseFiles)
			{
				cumulus.LogMessage($"GetHistoricDataSdCard: Processing file {file}");
				Cumulus.LogConsoleMessage($"  Processing file {file}");

				var lines = localApi.GetSdFileContents(file, startTime, cumulus.cancellationToken).Result;

				var logfile = new EcowittLogFile(lines, cumulus);

				var data = logfile.DataParser();

				cumulus.LogDebugMessage($"GetHistoricDataSdCard: EcowittLogFile.DataParser returned {data.Count} records for file {file}");

				cumulus.LogDebugMessage($"GetHistoricDataSdCard: Adding {data.Count} records to the processing list");

				if (data.Count == 0)
				{
					cumulus.LogMessage($"GetHistoricDataSdCard: No data to process, exiting catch-up");
					return false;
				}

				foreach (var rec in data)
				{
					buffer.Add(rec.Key, rec.Value);
				}
			}

			// now merge in the extra sensor data

			cumulus.LogDebugMessage($"GetHistoricDataSdCard: Processing {extraFiles.Count} extra files");
			Cumulus.LogConsoleMessage("Preprocessing the extra sensor file(s)...");


			foreach (var file in extraFiles)
			{
				cumulus.LogMessage($"GetHistoricDataSdCard: Processing file {file}");
				Cumulus.LogConsoleMessage($"  Processing file {file}");

				var lines = localApi.GetSdFileContents(file, startTime, cumulus.cancellationToken).Result;

				var logfile = new EcowittExtraLogFile(lines, cumulus);

				var data = logfile.DataParser();

				cumulus.LogDebugMessage($"GetHistoricDataSdCard: EcowittExtraLogFile.DataParser returned {data.Count} records for file {file}");

				cumulus.LogDebugMessage($"GetHistoricDataSdCard: Merging {data.Count} extra sensor records into the existing processing list records");

				foreach (var rec in data)
				{
					if (buffer.TryGetValue(rec.Key, out var value))
					{
						buffer[rec.Key] = EcowittExtraLogFile.Merge(value, rec.Value);
					}
					else
					{
						cumulus.LogMessage($"GetHistoricDataSdCard: Warning - Extra sensor record {rec.Key} not added because no matching primary record found");
					}
				}
			}

			// finally we can process the data

			// now we have all the data for this period, for each record create the string expected by ProcessData and get it processed
			var rollHour = Math.Abs(cumulus.GetHourInc());
			var luhour = cumulus.LastUpdateTime.Hour;
			var rolloverdone = luhour == rollHour;
			var midnightraindone = luhour == 0;
			var rollover9amdone = luhour == 9;
			bool snowhourdone = luhour == cumulus.SnowDepthHour;

			cumulus.LogMessage("GetHistoricDataSdCard: Adding historic data into Cumulus...");
			Cumulus.LogConsoleMessage("Adding historic data...");

			var recNo = 1;
			var lastRecTime = DateTime.MinValue;
			var interval = 1;

			foreach (var rec in buffer)
			{
				if (cumulus.cancellationToken.IsCancellationRequested)
				{
					return false;
				}

				//cumulus.LogMessage("Processing data for " + rec.Key);

				if (lastRecTime != DateTime.MinValue)
				{
					interval = (int) rec.Key.Subtract(lastRecTime).TotalMinutes;
					lastRecTime = rec.Key;
				}
				else
				{
					lastRecTime = rec.Key;
				}

				var h = rec.Key.Hour;

				rollHour = Math.Abs(cumulus.GetHourInc(rec.Key));

				//  if outside rollover hour, rollover yet to be done
				if (h != rollHour)
				{
					rolloverdone = false;
				}
				else if (!rolloverdone)
				{
					// In rollover hour and rollover not yet done
					// do rollover
					cumulus.LogMessage("Day rollover " + rec.Key.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
					Cumulus.LogConsoleMessage("\n  Day rollover " + rec.Key.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

					DayReset(rec.Key);

					rolloverdone = true;
				}

				// Not in midnight hour, midnight rain yet to be done
				if (h != 0)
				{
					midnightraindone = false;
				}
				else if (!midnightraindone)
				{
					// In midnight hour and midnight rain (and sun) not yet done
					ResetMidnightRain(rec.Key);
					ResetSunshineHours(rec.Key);
					ResetMidnightTemperatures(rec.Key);
					midnightraindone = true;
				}

				// 9am rollover items
				if (h != 9)
				{
					rollover9amdone = false;
				}
				else if (!rollover9amdone)
				{
					Reset9amTemperatures(rec.Key);
					rollover9amdone = true;
				}

				// Not in snow hour, snow yet to be done
				if (h != 0)
				{
					snowhourdone = false;
				}
				else if ((h == cumulus.SnowDepthHour) && !snowhourdone)
				{
					// snowhour items
					if (cumulus.SnowAutomated > 0)
					{
						CreateNewSnowRecord(rec.Key);
					}

					// reset the accumulated snow depth(s)
					for (int i = 0; i < Snow24h.Length; i++)
					{
						Snow24h[i] = null;
					}

					snowhourdone = true;
				}

				// finally apply this data
				ecowittApi.ApplyHistoricData(rec);

				// Do the CMX calculate SLP now as it depends on temperature
				if (cumulus.StationOptions.CalculateSLP)
				{
					var slp = MeteoLib.GetSeaLevelPressure(AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToMB(StationPressure), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
					DoPressure(ConvertUnits.PressMBToUser(slp), rec.Key);
				}

				// add in archive period worth of sunshine, if sunny
				if (CurrentSolarMax > 0 && SolarRad.HasValue &&
				SolarRad > CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100 &&
					SolarRad >= cumulus.SolarOptions.SolarMinimum &&
					!cumulus.SolarOptions.UseBlakeLarsen)
				{
					SunshineHours += interval / 60.0;
					cumulus.LogDebugMessage($"Adding {interval} minutes to Sunshine Hours");
				}

				// add in archive period minutes worth of temperature to the temp samples
				tempsamplestoday += interval;
				TempTotalToday += (OutdoorTemperature * interval);

				// add in 'following interval' minutes worth of wind speed to windrun
				cumulus.LogMessage("Windrun: " + WindAverage.ToString(cumulus.WindFormat) + cumulus.Units.WindText + " for " + interval + " minutes = " +
				(WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval / 60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);
				WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval / 60.0;

				// update heating/cooling degree days
				UpdateDegreeDays(5);

				// update dominant wind bearing
				CalculateDominantWindBearing(Bearing, WindAverage, interval);
				CheckForWindrunHighLow(rec.Key);
				DoTrendValues(rec.Key);

				if (cumulus.StationOptions.CalculatedET && rec.Key.Minute == 0)
				{
					// Start of a new hour, and we want to calculate ET in Cumulus
					CalculateEvapotranspiration(rec.Key);
				}

				_ = cumulus.DoLogFile(rec.Key, false);
				cumulus.DoCustomIntervalLogs(rec.Key);

				cumulus.MySqlRealtimeFile(999, false, rec.Key);

				if (cumulus.StationOptions.LogExtraSensors)
				{
					_ = cumulus.DoExtraLogFile(rec.Key);
				}

				// Custom MySQL update - minutes interval
				if (cumulus.MySqlSettings.CustomMins.Enabled)
				{
					_ = cumulus.CustomMysqlMinutesUpdate(rec.Key, false);
				}

				AddRecentDataWithAq(rec.Key, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
					OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);

				UpdateStatusPanel(rec.Key);
				cumulus.AddToWebServiceLists(rec.Key);
				LastDataReadTime = rec.Key;

				if (!Program.service)
				{
					Console.Write("\r - processed " + (((double) recNo++) / buffer.Count).ToString("P0"));
				}
			}

			Cumulus.LogConsoleMessage("Historic data processing complete");

			return cumulus.LastUpdateTime.AddMinutes(interval + 1) < DateTime.Now;

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
			else
			{
				cumulus.LogWarningMessage("GetEcowittCameraUrl: Warning - URL requested, but no camera MAC address is configured");
			}

			return string.Empty;
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
			else
			{
				cumulus.LogWarningMessage("GetEcowittVideoUrl: Warning - URL requested, but no camera MAC address is configured");
			}

			return string.Empty;
		}
		private async Task GetSensorIds(bool delay)
		{
			// pause for two seconds to get out of sync with the live data requests
			if (delay)
			{
				await Task.Delay(2000, cumulus.cancellationToken);
				if (cumulus.cancellationToken.IsCancellationRequested)
				{
					return;
				}
			}

			cumulus.LogMessage("Reading sensor ids");
			var sensors = await localApi.GetSensorInfo(cumulus.cancellationToken);
			if (cumulus.cancellationToken.IsCancellationRequested)
			{
				return;
			}
			batteryLow = false;
			LowBatteryDevices.Clear();
			if (sensors != null && sensors.Length > 0)
			{
				for (var i = 0; i< sensors.Length; i++)
				{
					var sensor = sensors[i];
					var name = string.Empty;
					try
					{
						cumulus.LogDebugMessage($" - enabled={sensor.idst}, type={sensor.img}, sensor id={sensor.id}, signal={sensor.signal}, battery={sensor.batt}, name={sensor.name}");

						// check the battery status
						if (sensor.idst && sensor.signal > 0)
						{
#pragma warning disable S907
							switch (sensor.type)
							{
								case 0: // wh69
									break;
								case 1: // wh68
									name = "wh68";
									goto case 1003;
								case 2: // wh80
									name = "wh80";
									// if a WS80 is connected, it has a 4.75 second update rate, so reduce the MX update rate from the default 10 seconds
									if (updateRate > 4000 && updateRate != 4000)
									{
										cumulus.LogMessage($"GetSensorIds: WS80 sensor detected, changing the update rate from {(updateRate / 1000):D} seconds to 4 seconds");
										updateRate = 4000;
									}
									goto case 1003;
								case 3: // wh40 - rain gauge
									name = "wh25";
									if (sensor.batt > 0) // some send a voltage, some don't :(
									{
										goto case 1003;
									}
									break;
								case 4: // wh25
									name = "wh25";
									goto case 1003;
								case 5: // wh26
									name = "wh326";
									goto case 1001;
								case int n when (n > 5 && n < 14): // wh31 - T&H (8 chan)
									name = "wh31ch" + (sensor.type - 5);
									goto case 1001;
								case int n when (n > 13 && n < 22): // wh51 - soil moisture (8 chan)
									name = "wh51ch" + (sensor.type - 13);
									goto case 1001;
								case int n when (n > 21 && n < 26): // wh41 - pm2.5 (4 chan)
									name = "wh41ch" + (sensor.type - 21);
									goto case 1003;
								case 26: // wh57 - lightning
									name = "wh57";
									goto case 1003;
								case int n when (n > 26 && n < 31): // wh55 - leak (4 chan)
									name = "wh55ch" + (sensor.type - 26);
									goto case 1003;
								case int n when (n > 30 && n < 39): // wh34 - Temp (8 chan)
									name = "wh34ch" + (sensor.type - 30);
									goto case 1003;
								case 39: // wh45 - co2
									name = "wh45";
									goto case 1003;
								case int n when (n > 39 && n < 48): // wh35 - leaf wet (8 chan)
									name = "wh35ch" + (sensor.type - 39);
									goto case 1003;
								case 48: // wh90
									name = "wh90";
									// if a WS90 is connected, it has a 8.8 second update rate, so reduce the MX update rate from the default 10 seconds
									if (updateRate > 8000 && updateRate != 8000)
									{
										cumulus.LogMessage($"GetSensorIds: WS90 sensor detected, changing the update rate from {(updateRate / 1000):D} seconds to 8 seconds");
										updateRate = 8000;
									}
									goto case 1003;
								case 49: // wh85
									name = "wh85";
									// if a WH85 is connected, it has a 8.5 second update rate, so reduce the MX update rate from the default 10 seconds
									if (updateRate > 8000 && updateRate != 8000)
									{
										cumulus.LogMessage($"GetSensorIds: WH85 sensor detected, changing the update rate from {(updateRate / 1000):D} seconds to 8 seconds");
										updateRate = 8000;
									}
									goto case 1003;
								case int n when (n > 57 && n < 66): // wh51 - soil moisture (chan 9-16)
									name = "wh51ch" + (sensor.type - 57 + 8);
									goto case 1001;
								case int n when (n > 65 && n < 70): // wh54 - laser depth (4 chan)
									name = "wh54ch" + (sensor.type - 65);
									goto case 1003;


								case 1001: // battery type 1 (0=OK, 1=LOW)
									if (sensor.batt == 1)
									{
										batteryLow = true;
										LowBatteryDevices.Add(name + " - LOW");
									}
									break;

								case 1003: // battery type 3 (0-1=LOW, 2-5=OK, 6=DC, 9=OFF)
									if (!TestBattery3(sensor.batt))
									{
										batteryLow = true;
										LowBatteryDevices.Add(name + " - " + sensor.batt);
									}
									break;

								default:
									cumulus.LogWarningMessage($"Unknown sensor type in SendorIds. Model={sensor.img}, type={sensor.type}");
									break;
							}
#pragma warning restore S907
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"GetSensorIds: Error processing sensor[{i}]");
					}
				}
			}
			else
			{
				cumulus.LogWarningMessage("GetSensorIds: No sensor data returned");
			}
		}

		private void ProcessCommonList(EcowittLocalApi.CommonSensor[] sensors, DateTime dateTime)
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
						case "4": //Apparent
								  // do nothing with this for now - MX calcuates apparent
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
							if (arr.Length == 2 && double.TryParse(arr[0], invNum, out var valDbl))
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
							if (arr.Length == 2 && double.TryParse(arr[0], invNum, out valDbl))
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
							if (arr.Length == 2 && double.TryParse(arr[0], invNum, out valDbl))
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

		private void ProcessWh25(EcowittLocalApi.Wh25Sensor[] sensors, DateTime dateTime)
		{
#pragma warning disable S125
			//"wh25":	[{
			//	"intemp":	"23.8",
			//	"unit":	"C",
			//	"inhumi":	"68%",
			//	"abs":	"1006.5 hPa",
			//	"rel":	"1010.5 hPa"
			//}]
#pragma warning restore S125

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
						if (arr.Length == 2 && double.TryParse(arr[0], invNum, out var val))
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
						if (arr.Length == 2 && double.TryParse(arr[0], invNum, out var val))
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
								DoStationPressure(abs);
								// Leave calculate SLP until the end as it depends on temperature
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessWh25: Error processing pressure}");
				}
			}
		}

		private void ProcessRain(EcowittLocalApi.CommonSensor[] sensors, bool isRainingOnly)
		{
#pragma warning disable S125
			//"rain"/"piezoRain": [
			//	{
			//		"id": "0x0D",
			//		"val": "0.0 mm"
			//	},
			//	{
			//		"id": "0x0E",
			//		"val": "0.0 mm/Hr"
			//	},
			//	{
			//		"id": "0x10",
			//		"val": "0.0 mm"
			//	},
			//	{
			//		"id": "0x11",
			//		"val": "5.0 mm"
			//	},
			//	{
			//		"id": "0x12",
			//		"val": "27.1 mm"
			//	},
			//	{
			//		"id": "0x13",
			//		"val": "681.4 mm",
			//		"battery": "5"
			//	}
			//],
#pragma warning restore S125

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					switch (sensor.id)
					{
						case "0x0D":
							//Rain Event (val unit)
							if (isRainingOnly)
								break;
							try
							{
								var arr = sensor.val.Split(' ');
								if (arr.Length == 2 && double.TryParse(arr[0], invNum, out var val))
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
								cumulus.LogExceptionMessage(ex, $"ProcessRain: Error processing Rain Event for sensor id {sensor.id}");
							}
							break;

						case "0x0E":
							//Rain Rate (val unit/h)
							if (isRainingOnly)
								break;
							try
							{
								var arr = sensor.val.Split(' ');
								if (arr.Length == 2 && double.TryParse(arr[0], invNum, out var val))
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

									if (cumulus.StationOptions.UseRainForIsRaining == 1 && !cumulus.EcowittIsRainingUsePiezo)
									{
										IsRaining = rate > 0;
										cumulus.IsRainingAlarm.Triggered = IsRaining;
									}
								}
							}
							catch (Exception ex)
							{
								cumulus.LogExceptionMessage(ex, $"ProcessRain: Error processing Rain Rate for sensor id {sensor.id}");
							}
							break;

						case "0x13":
							//Rain Year (val unit)
							if (isRainingOnly)
								break;
							try
							{
								var arr = sensor.val.Split(' ');
								if (arr.Length == 2 && double.TryParse(arr[0], invNum, out var val))
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
								cumulus.LogExceptionMessage(ex, $"ProcessRain: Error processing Rain Year for sensor id {sensor.id}");
							}
							break;

						case "srain_piezo":
							if (cumulus.EcowittIsRainingUsePiezo)
							{
								IsRaining = sensor.val == "1";
								cumulus.IsRainingAlarm.Triggered = IsRaining;							}
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

		private void ProcessLightning(EcowittLocalApi.LightningSensor[] sensors, DateTime dateTime)
		{
#pragma warning disable S125
			// "lightning":	[{
			//		"distance": "16.7 mi",
			//		"timestamp": "09/01/2024 18:45:14",
			//		"count": "0",
			//		"battery": "5"
			// }]
#pragma warning restore S125

			for (var i = 0; i < sensors.Length; i++)
			{
				try
				{
					var sensor = sensors[i];

					//Lightning dist (1-40km)
					if (sensor.distanceVal.HasValue && sensor.distanceUnit != null)
					{
						// Sends a default value of 255km until the first strike is detected
						if (sensor.distanceVal.Value > 254.9 )
						{
							newLightningDistance = 999;
						}
						else
						{
							newLightningDistance = ConvertUnits.KmtoUserUnits(sensor.distanceVal.Value);
						}
					}
					else if (sensor.distance.Contains("--"))
					{
						newLightningDistance = 999;
					}

					//Lightning time (UTC)
					if (!string.IsNullOrEmpty(sensor.timestamp))
					{
						if (sensor.timestamp.Contains("--"))
						{
							newLightningTime = DateTime.MinValue;
						}
						else
						{
							// oh my god, it sends the time as "MM/dd/yyyy HH: mm: ss" for some locales
							// TODO: what is default time if not strikes detected yet?
							var arr = sensor.timestamp.Replace(": ", ":").Split(' ');
							var date = arr[0].Split('/');
							var time = arr[1].Split(':');

							newLightningTime = new DateTime(
								int.Parse(date[2]),
								int.Parse(date[0]),
								int.Parse(date[1]),
								int.Parse(time[0]),
								int.Parse(time[1]),
								int.Parse(time[2]),
								0, DateTimeKind.Utc);
						}
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

		private void ProcessCo2(EcowittLocalApi.Co2Sensor[] sensors)
		{
#pragma warning disable S125
			// "co2": [
			//	{
			//		"temp": "24.4",
			//		"unit": "C",
			//		"humidity": "62%",
			//		"PM25": "0.9",
			//		"PM25_RealAQI": "4",
			//		"PM25_24HAQI": "7",
			//		"PM10": "0.9",
			//		"PM10_RealAQI": "1",
			//		"PM10_24HAQI": "2",
			//		"CO2": "323",
			//		"CO2_24H": "348",
			//		"battery": "6"
			//	}
			// ]
#pragma warning restore S125

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
					else
					{
						CO2_temperature = null;
					}
					// humidty sent as "value%"
					CO2_humidity = sensor.humidityVal;
					CO2_pm2p5 = sensor.PM25;
					CO2_pm10 = sensor.PM10;
					// HTTP protocol does not send a pm 24 hour values :( Only useless AQI values
					CO2 = sensor.CO2;
					CO2_24h = sensor.CO2_24H;
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "ProcessCo2: Error");
				}
			}


			CO2_pm2p5_aqi = CO2_pm2p5.HasValue ? GetAqi(AqMeasure.pm2p5, CO2_pm2p5.Value) : null;
			CO2_pm10_aqi = CO2_pm10.HasValue ? GetAqi(AqMeasure.pm10, CO2_pm10.Value) : null;
		}

		private void ProcessChPm25(EcowittLocalApi.ChPm25Sensor[] sensors)
		{
#pragma warning disable S125
			//"ch_pm25": [
			//	{
			//		"channel": "1",
			//		"PM25": "6.0",
			//		"PM25_RealAQI": "25",
			//		"PM25_24HAQI": "24",
			//		"battery": "5"
			//	}
			//]
#pragma warning restore S125

			cumulus.LogDebugMessage($"ProcessChPm25: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				if (sensor.channel.HasValue && sensor.PM25.HasValue)
				{
					try
					{
						DoAirQuality(sensor.PM25.Value, sensor.channel.Value);
						//DoAirQualityAvg(sensor.PM25_24H.Value, sensor.channel.Value);
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"ProcessChPm25: Error on sensor {sensor.channel}");
					}
				}
			}
		}

		private void ProcessLeak(EcowittLocalApi.ChLeakSensor[] sensors)
		{
#pragma warning disable S125
			//"ch_leak": [
			//	{
			//		"channel": "2",
			//		"name": "",
			//		"battery": "4",
			//		"status": "Normal"
			//	}
			//]
#pragma warning restore S125

			cumulus.LogDebugMessage($"ProcessLeak: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				if (sensor.channel.HasValue && !string.IsNullOrEmpty(sensor.status))
				{
					try
					{
						var val = sensor.status == "NORMAL" ? 0 : 1;
						DoLeakSensor(val, sensor.channel.Value);
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"ProcessLeak: Error processing channel {sensor.channel.Value}");
					}
				}
			}
		}

		private void ProcessExtraTempHum(EcowittLocalApi.TempHumSensor[] sensors, DateTime dateTime)
		{
#pragma warning disable S125
			//"ch_aisle": [
			//	{
			//		"channel": "1",
			//		"name": "",
			//		"battery": "0",
			//		"temp": "24.9",
			//		"unit": "C",
			//		"humidity": "61%"
			//	}
			//]
#pragma warning restore S125

			cumulus.LogDebugMessage($"ProcessExtraTempHum: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					if (sensor.temp.HasValue)
					{
						DoExtraTemp(sensor.temp.Value, sensor.channel);

						if (cumulus.Gw1000PrimaryTHSensor == sensor.channel)
						{
							DoOutdoorTemp(sensor.temp.Value, dateTime);
						}
					}

					if (sensor.humidityVal.HasValue)
					{
						DoExtraHum(sensor.humidityVal.Value, sensor.channel);

						if (cumulus.Gw1000PrimaryTHSensor == sensor.channel)
						{
							DoOutdoorHumidity(sensor.humidityVal.Value, dateTime);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessExtraTempHum: Error processind sensor channel {sensor.channel}");
				}
			}
		}

		private void ProcessUserTemp(EcowittLocalApi.TempHumSensor[] sensors)
		{
#pragma warning disable S125
			//"ch_temp": [
			//	{
			//		"channel": "1",
			//		"name": "",
			//		"temp": "21.5",
			//		"unit": "C",
			//		"battery": "3"
			//	}
			//]
#pragma warning restore S125

			// user temp = WH34 8 channel Soil or Water temperature sensors
			cumulus.LogDebugMessage($"ProcessUserTemp: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				if (sensor.temp.HasValue)
				{
					try
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
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"ProcessUserTemp: Error processing sensor channel {sensor.channel}");
					}
				}
			}
		}

		private void ProcessSoilMoisture(EcowittLocalApi.TempHumSensor[] sensors)
		{
#pragma warning disable S125
			//"ch_soil": [
			//	{
			//		"channel": "1",
			//		"name": "",
			//		"battery": "5",
			//		"humidity": "56%"
			//	}
			//]
#pragma warning restore S125

			cumulus.LogDebugMessage($"ProcessSoilMoisture: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				if (sensor.humidityVal.HasValue)
				{
					try
					{
						DoSoilMoisture(sensor.humidityVal.Value, sensor.channel);
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"ProcessSoilMoisture: Error on sensor {sensor.channel}");
					}
				}
			}
		}

		private void ProcessLeafWet(EcowittLocalApi.TempHumSensor[] sensors)
		{
#pragma warning disable S125
			//"ch_leaf": [
			//	{
			//		"channel": "1",
			//		"name": "CH1 Leaf Wetness",
			//		"humidity": "10%",
			//		"battery": "5"
			//	}
			//]
#pragma warning restore S125

			cumulus.LogDebugMessage($"ProcessLeafWet: Processing {sensors.Length} sensors");

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				if (sensor.humidityVal.HasValue)
				{
					try
					{
						DoLeafWetness(sensor.humidityVal.Value, sensor.channel);
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, $"ProcessLeafWet: Error processing channel {sensor.channel}");
					}
				}
			}
		}

		private void ProcessLds(EcowittLocalApi.LdsSensor[] sensors)
		{
#pragma warning disable S125
			//"ch_lds": [
			//	{
			//		"channel": "1",
			//		"name": "Laser Dist Tank",
			//		"unit": "mm",
			//		"battery": "5",
			//		"air": "13 mm",
			//		"depth": "3987 mm"
			//	}
			//]
#pragma warning restore S125

			for (var i = 0; i < sensors.Length; i++)
			{
				var sensor = sensors[i];

				try
				{
					var val = sensor.unit == "mm" ? ConvertUnits.LaserMmToUser(sensor.airVal.Value) : ConvertUnits.LaserInchesToUser(sensor.airVal.Value);
					DoLaserDistance(val, sensor.channel);
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessLds: Error processing channel {sensor.channel} 'air' distance");
				}

				try
				{
					if (cumulus.LaserDepthBaseline[sensor.channel] == -1)
					{
						// MX is not calculating depth

						decimal? val = null;

						if (sensor.depthVal.HasValue)
						{
							val = sensor.unit == "mm" ? ConvertUnits.LaserMmToUser(sensor.depthVal.Value) : ConvertUnits.LaserInchesToUser(sensor.depthVal.Value);
						}
						DoLaserDepth(val, sensor.channel);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"ProcessLds: Error processing channel {sensor.channel} 'depth' value");
				}
			}
		}

		private void GetSystemInfo(bool driftOnly)
		{
			cumulus.LogMessage("NOT Reading Ecowitt system info");
		}

		private static bool TestBattery3(int value)
		{
			if (value == 6)
			{
				// DC
				return true;
			}
			if (value == 9)
			{
				// OFF
				return false;
			}
			if (value > 1)
			{
				return true;
			}

			return false;
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
				cumulus.LogErrorMessage($"ERROR: No data received from the station for {tmrDataWatchdog.Interval / 1000} seconds");
				if (!DataStopped)
				{
					DataStoppedTime = DateTime.Now;
					DataStopped = true;
				}
				cumulus.DataStoppedAlarm.LastMessage = $"No data received from the station for {tmrDataWatchdog.Interval / 1000} seconds";
				cumulus.DataStoppedAlarm.Triggered = true;
			}
		}
	}
}
