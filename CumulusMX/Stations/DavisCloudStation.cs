using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Timers;

using ServiceStack;
using ServiceStack.Text;

namespace CumulusMX
{
	internal partial class DavisCloudStation : WeatherStation
	{
		private readonly System.Timers.Timer tmrCurrent;
		private int maxArchiveRuns = 1;
		private int wlStationArchiveInterval = 5;
		private readonly AutoResetEvent bwDoneEvent = new(false);
		private List<WlSensorListSensor> sensorList;
		private readonly Dictionary<int, int> lsidToTx = [];
		private readonly Dictionary<int, long> lsidLastUpdate = [];

		private bool startingUp = true;
		private DateTime lastRecordTime = DateTime.MinValue;
		private DateTime lastHistoricData;
		private string subscriptionLevel = string.Empty;

		private int numLeafWetnessSensors = 8;
		private int numSoilMoistureSensors = 16;
		private int numSoiltempSensors = 16;

		private bool isVp2Station = false;

		public DavisCloudStation(Cumulus cumulus) : base(cumulus)
		{
			calculaterainrate = false;
			// WLL does not provide a forecast string, so use the Cumulus forecast
			cumulus.UseCumulusForecast = true;
			// WLL does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;

			noET = false;
			// initialise the battery status
			TxBatText = "1-NA 2-NA 3-NA 4-NA 5-NA 6-NA 7-NA 8-NA";

			cumulus.LogMessage("Station type = Davis Cloud Station");

			// Override the ServiceStack De-serialization function
			// Check which format provided, attempt to parse as datetime or return minValue.
			// Formats to use for the different date kinds
			string utcTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
			string localTimeFormat = "yyyy-MM-dd'T'HH:mm:ss";

			ServiceStack.Text.JsConfig<DateTime>.DeSerializeFn = datetimeStr =>
			{
				if (string.IsNullOrWhiteSpace(datetimeStr))
				{
					return DateTime.MinValue;
				}

				if (datetimeStr.EndsWith('Z') &&
					DateTime.TryParseExact(datetimeStr, utcTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime resultUtc))
				{
					return resultUtc;
				}
				else if (!datetimeStr.EndsWith('Z') &&
					DateTime.TryParseExact(datetimeStr, localTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime resultLocal))
				{
					return resultLocal;
				}

				return DateTime.MinValue;
			};

			tmrCurrent = new System.Timers.Timer();

			if (cumulus.StationType == StationTypes.DavisCloudVP2)
			{
				cumulus.LogMessage("Davis Cloud Station (VP2/Vue) selected");
				isVp2Station = true;

				// Do not ue the user sensor mappings for VP2 station - they are fixed
				numLeafWetnessSensors = 2;
				cumulus.WllLeafWetIdx[1] = 1;
				cumulus.WllLeafWetIdx[2] = 2;

				numSoilMoistureSensors = 4;
				cumulus.WllSoilMoistureTx[1] = -1;
				cumulus.WllSoilMoistureTx[2] = -1;
				cumulus.WllSoilMoistureTx[3] = -1;
				cumulus.WllSoilMoistureTx[4] = -1;
				cumulus.WllSoilMoistureIdx[1] = 1;
				cumulus.WllSoilMoistureIdx[2] = 2;
				cumulus.WllSoilMoistureIdx[3] = 3;
				cumulus.WllSoilMoistureIdx[4] = 4;

				numSoiltempSensors = 4;
				cumulus.WllSoilTempTx[1] = -1;
				cumulus.WllSoilTempTx[2] = -1;
				cumulus.WllSoilTempTx[3] = -1;
				cumulus.WllSoilTempTx[4] = -1;
				cumulus.WllSoilTempIdx[1] = 1;
				cumulus.WllSoilTempIdx[2] = 2;
				cumulus.WllSoilTempIdx[3] = 3;
				cumulus.WllSoilTempIdx[4] = 4;
			}
			else
			{
				cumulus.LogErrorMessage("Davis Cloud Station (WLL/WLC) selected");

				// The Davis leafwetness sensors send a decimal value via WLL (only integer available via VP2/Vue)
				cumulus.LeafWetDPlaces = 1;
				cumulus.LeafWetFormat = "F1";
			}

			CalcRecentMaxGust = false;
			cumulus.StationOptions.CalcuateAverageWindSpeed = false;
			cumulus.StationOptions.UseSpeedForAvgCalc = true;
			cumulus.StationOptions.UseSpeedForLatest = true;
			cumulus.StationOptions.CalculatedDP = false;
			cumulus.StationOptions.CalculatedWC = false;

			// Sanity check - do we have all the info we need?
			if (string.IsNullOrEmpty(cumulus.WllApiKey) && string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// The basic API details have not been supplied
				cumulus.LogWarningMessage("No WeatherLink.com API configuration supplied, cannot continue");
				cumulus.LogMessage("Cannot continue");
				Cumulus.LogConsoleMessage("*** No WeatherLink.com API details supplied. Cannot start station", ConsoleColor.DarkCyan);
				return;
			}
			else if (string.IsNullOrEmpty(cumulus.WllApiKey) || string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// One of the API details is missing
				if (string.IsNullOrEmpty(cumulus.WllApiKey))
				{
					cumulus.LogWarningMessage("Missing WeatherLink.com API Key");
					Cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Key. Cannot start station", ConsoleColor.Yellow);
				}
				else
				{
					cumulus.LogWarningMessage("Missing WeatherLink.com API Secret");
					Cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Secret. Cannot start station", ConsoleColor.Yellow);
				}

				return;
			}

			// Get wl.com status
			GetSystemStatus();

			// Perform Station ID checks - If we have API details!
			// If the Station ID is missing, this will populate it if the user only has one station associated with the API key
			if (cumulus.WllStationId < 10)
			{
				var msg = $"No WeatherLink API station ID {(cumulus.WllStationUuid == string.Empty ? "or UUID" : "")} in the cumulus.ini file" + (cumulus.WllStationUuid == string.Empty ? "" : ", but a UUID has been configured");
				cumulus.LogWarningMessage(msg);
				Cumulus.LogConsoleMessage(msg);

				GetAvailableStationIds(true);
			}
			else
			{
				GetAvailableStationIds(false);
			}

			// Sanity check the station id
			if (cumulus.WllStationId < 10)
			{
				// API details supplied, but Station Id is still invalid - do not start the station up.
				cumulus.LogErrorMessage("The WeatherLink.com API is enabled, but no Station Id has been configured, not starting the station. Please correct this and restart Cumulus");
				Cumulus.LogConsoleMessage("The WeatherLink.com API is enabled, but no Station Id has been configured. Please correct this and restart Cumulus", ConsoleColor.Yellow);
				return;
			}


			// Now get the sensors associated with this station
			GetAvailableSensors();

			DateTime tooOld = new DateTime(0, DateTimeKind.Local);

			if ((cumulus.LastUpdateTime <= tooOld) || subscriptionLevel == "basic" || !cumulus.StationOptions.UseDataLogger)
			{
				// there's nothing in the database, so we haven't got a rain counter
				// we can't load the history data, so we'll just have to go live

				timerStartNeeded = true;
				LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);

				cumulus.LogMessage("Starting Davis Cloud Station");
				StartLoop();
			}
			else
			{
				// Read the data from the WL APIv2
				startReadingHistoryData();
			}
		}

		public override void Start()
		{
			try
			{
				// Wait for the lock
				Cumulus.SyncInit.Wait();

				// Create a current conditions thread to poll readings
				GetCurrent(null, null);
				tmrCurrent.Elapsed += GetCurrent;
				tmrCurrent.Interval = 60 * 1000;  // Every 60 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();
			}
			catch (ThreadAbortException)
			{
				// do nothing
			}
			finally
			{
				Cumulus.SyncInit.Release();
			}
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing Davis Cloud (WLL/WLC) connections");
			try
			{
				if (tmrCurrent != null)
				{
					cumulus.LogMessage("Stopping current data timer");
					tmrCurrent.Stop();
				}
			}
			catch
			{
				cumulus.LogMessage("Error stopping station timers");
			}

			StopMinuteTimer();
			try
			{
				if (bw != null && bw.WorkerSupportsCancellation)
				{
					cumulus.LogMessage("Stopping background worker");
					bw.CancelAsync();
				}
			}
			catch
			{
				cumulus.LogMessage("Error stopping station background tasks");
			}
		}


		private async void GetCurrent(object source, ElapsedEventArgs e)
		{
			if (DayResetInProgress)
			{
				return;
			}

			cumulus.LogMessage("GetCurrent: Get WL.com Current Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("GetCurrent: Missing WeatherLink API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId < 10 && cumulus.WllStationUuid == string.Empty)
			{
				const string msg = "No WeatherLink API station ID/UUID in the configuration";
				cumulus.LogWarningMessage(msg);
				Cumulus.LogConsoleMessage("GetCurrent: " + msg);
				return;
			}

			cumulus.LogMessage($"GetWlCurrent: Downloading Current Data from weatherlink.com");

			StringBuilder currentUrl = new StringBuilder("https://api.weatherlink.com/v2/current/");
			currentUrl.Append(cumulus.WllStationId > 10 ? cumulus.WllStationId.ToString() : cumulus.WllStationUuid);
			currentUrl.Append("?api-key=" + cumulus.WllApiKey);

			cumulus.LogDebugMessage($"WeatherLink URL = {currentUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");

			WlCurrent currObj;

			try
			{
				string responseBody;
				int responseCode;

				var request = new HttpRequestMessage(HttpMethod.Get, currentUrl.ToString());
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// we want to do this synchronously, so .Result
				using (var response = await cumulus.MyHttpClient.SendAsync(request, Program.ExitSystemToken))
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetCurrent: WeatherLink API Current Response code: {responseCode}");
					cumulus.LogDataMessage($"GetCurrent: WeatherLink API Current Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var error = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"GetCurrent: WeatherLink API Current Error: {error.code}, {error.message}");
					Cumulus.LogConsoleMessage($" - Error {error.code}: {error.message}", ConsoleColor.Red);
					return;
				}

				currObj = responseBody.FromJson<WlCurrent>();

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("GetCurrent: WeatherLink API Current: No data was returned. Check your Device Id.");
					return;
				}
				else if (responseBody.StartsWith("{\"")) // basic sanity check
				{
					if (currObj.sensors.Count == 0)
					{
						cumulus.LogMessage("GetCurrent: No current data available");
						return;
					}
					else
					{
						cumulus.LogMessage($"GetCurrent: Found {currObj.sensors.Count} sensors to process");

						DecodeCurrent(currObj.sensors);

						if (startingUp)
							startingUp = false;
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("GetCurrent: Invalid current response received");
					cumulus.LogMessage("Response body = " + responseBody.ToString());
					return;
				}
			}
			catch (Exception ex)
			{
				if (ex.InnerException is TimeoutException)
				{
					cumulus.LogWarningMessage($"GetCurrent: Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
				}
				else if (Program.ExitSystemToken.IsCancellationRequested)
				{
					// do nothing - shutting down
				}
				else
						{
					cumulus.LogExceptionMessage(ex, "GetCurrent: Error");
				}
			}
		}


		private double ConvertRainClicksToUser(double clicks, int size)
		{
			// 0: Reserved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
			return size switch
			{
				1 => ConvertUnits.RainINToUser(clicks * 0.01),
				2 => ConvertUnits.RainMMToUser(clicks * 0.2),
				3 => ConvertUnits.RainMMToUser(clicks * 0.1),
				4 => ConvertUnits.RainINToUser(clicks * 0.001),
				_ => cumulus.DavisOptions.RainGaugeType switch
				{
					// Hmm, no valid tip size from WLL...
					// One click is normally either 0.01 inches or 0.2 mm
					// Try the setting in Cumulus.ini
					// Rain gauge type not configured, assume from units
					-1 when cumulus.Units.Rain == 0 => clicks * 0.2,
					-1 => clicks * 0.01,
					// Rain gauge is metric, convert to user unit
					0 => ConvertUnits.RainMMToUser(clicks * 0.2),
					_ => ConvertUnits.RainINToUser(clicks * 0.01),
				},
			};
		}

		private void SetTxBatteryStatus(int txId, int status)
		{
			// Split the string
			var delimiters = new[] { ' ', '-' };
			var sl = TxBatText.Split(delimiters);

			TxBatText = string.Empty;
			for (var i = 1; i <= 8; i++)
			{
				TxBatText += i;
				if (i == txId)
				{
					TxBatText += (status == 0 ? "-OK " : "-LOW ");
				}
				else
				{
					TxBatText += "-" + sl[(i - 1) * 2 + 1] + " ";
				}
			}
			TxBatText = TxBatText.Trim();
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("History: Reading history data from log files");
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
			lastHistoricData = cumulus.LastUpdateTime;

			cumulus.LogMessage("History: Reading archive data from WeatherLink API");
			bw = new BackgroundWorker { WorkerSupportsCancellation = true };
			bw.DoWork += bw_ReadHistory;
			bw.RunWorkerCompleted += bw_ReadHistoryCompleted;
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		private void bw_ReadHistoryCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cumulus.LogMessage("History: WeatherLink API archive reading thread completed");
			if (e.Error != null)
			{
				cumulus.LogErrorMessage("History: Archive reading thread apparently terminated with an error: " + e.Error.Message);
			}
			cumulus.NormalRunning = true;

			StartLoop();
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		private void bw_ReadHistory(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;

			int archiveRun = 0;
			Cumulus.SyncInit.Wait();

			try
			{
				do
				{
					GetHistoricData(worker);
					archiveRun++;
				} while (archiveRun < maxArchiveRuns && !worker.CancellationPending && !Program.ExitSystemToken.IsCancellationRequested);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Exception occurred reading archive data: " + ex.Message);
			}

			Cumulus.SyncInit.Release();
			bwDoneEvent.Set();
		}

		private void GetHistoricData(BackgroundWorker worker)
		{
			cumulus.LogMessage("GetHistoricData: Get WL.com Historic Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("GetHistoricData: Missing WeatherLink API data in the configuration, aborting!");
				lastHistoricData = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId < 10 && cumulus.WllStationUuid == string.Empty)
			{
				const string msg = "No WeatherLink API station ID/UUID in the configuration";
				cumulus.LogWarningMessage(msg);
				Cumulus.LogConsoleMessage("GetHistoricData: " + msg);
				return;
			}

			var unixDateTime = DateTime.UtcNow.ToUnixTime();
			var startTime = lastHistoricData.ToUnixTime();
			long endTime = unixDateTime;
			int unix24hrs = 24 * 60 * 60;

			// The API call is limited to fetching 24 hours of data
			if (unixDateTime - startTime > unix24hrs)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime + unix24hrs;
				maxArchiveRuns++;
			}

			Cumulus.LogConsoleMessage($"Downloading Historic Data from WL.com from: {lastHistoricData:s} to: {Utils.FromUnixTime(endTime):s}");
			cumulus.LogMessage($"GetHistoricData: Downloading Historic Data from WL.com from: {lastHistoricData:s} to: {Utils.FromUnixTime(endTime):s}");

			StringBuilder historicUrl = new StringBuilder("https://api.weatherlink.com/v2/historic/");
			historicUrl.Append(cumulus.WllStationId > 10 ? cumulus.WllStationId.ToString() : cumulus.WllStationUuid);
			historicUrl.Append("?api-key=" + cumulus.WllApiKey);
			historicUrl.Append("&start-timestamp=" + startTime.ToString());
			historicUrl.Append("&end-timestamp=" + endTime.ToString());

			cumulus.LogDebugMessage($"WeatherLink URL = {historicUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");

			LastDataReadTime = cumulus.LastUpdateTime;
			int luhour = LastDataReadTime.Hour;

			int rollHour = Math.Abs(cumulus.GetHourInc(lastHistoricData));

			cumulus.LogMessage($"Roll over hour = {rollHour}");

			bool rolloverdone = luhour == rollHour;

			bool midnightraindone = luhour == 0;
			bool rollover9amdone = luhour == 9;
			bool snowhourdone = luhour == cumulus.SnowDepthHour;

			WlHistory histObj;
			int noOfRecs = 0;
			WlHistorySensor sensorWithMostRecs;

			try
			{
				string responseBody;
				int responseCode;

				var request = new HttpRequestMessage(HttpMethod.Get, historicUrl.ToString());
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.SendAsync(request, Program.ExitSystemToken).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetHistoricData: WeatherLink API Historic Response code: {responseCode}");
					cumulus.LogDataMessage($"GetHistoricData: WeatherLink API Historic Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var historyError = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"GetHistoricData: WeatherLink API Historic Error: {historyError.code}, {historyError.message}");
					Cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.message}", ConsoleColor.Red);
					//cumulus.LastUpdateTime = Utils.FromUnixTime(endTime)
					maxArchiveRuns = -1;
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("GetHistoricData: WeatherLink API Historic: No data was returned. Check your Device Id.");
					Cumulus.LogConsoleMessage(" - No historic data available");
					lastHistoricData = Utils.FromUnixTime(endTime);
					maxArchiveRuns = -1;
					return;
				}
				else if (responseBody.StartsWith("{\"")) // basic sanity check
				{
					histObj = responseBody.FromJson<WlHistory>();

					// get the sensor data
					int idxOfSensorWithMostRecs = 0;
					for (var i = 0; i < histObj.sensors.Count; i++)
					{
						// Find the WLL/WLC baro, oth use sensor type=242, WLL structure=13, WLC structure=20
						if (histObj.sensors[i].sensor_type == 242 && (histObj.sensors[i].data_structure_type == 13 || histObj.sensors[i].data_structure_type == 20))
						{
							var recs = histObj.sensors[i].data.Count;
							if (recs > noOfRecs)
							{
								noOfRecs = recs;
								idxOfSensorWithMostRecs = i;
							}
						}
					}
					sensorWithMostRecs = histObj.sensors[idxOfSensorWithMostRecs];

					if (noOfRecs == 0)
					{
						cumulus.LogMessage("GetHistoricData: No historic data available");
						Cumulus.LogConsoleMessage(" - No historic data available");
						lastHistoricData = Utils.FromUnixTime(endTime);
						return;
					}
					else
					{
						cumulus.LogMessage($"GetHistoricData: Found {noOfRecs} historic records to process");
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("GetHistoricData: Invalid historic message received");
					cumulus.LogMessage("GetHistoricData: Received: " + responseBody);
					lastHistoricData = Utils.FromUnixTime(endTime);
					maxArchiveRuns = -1;
					return;
				}
			}
			catch (Exception ex)
			{
				if (!Program.ExitSystemToken.IsCancellationRequested)
				{
					cumulus.LogErrorMessage("GetHistoricData:  Exception: " + ex.Message);
					if (ex.InnerException != null)
					{
						ex = Utils.GetOriginalException(ex);
						cumulus.LogMessage($"GetHistoricData: Base exception - {ex.Message}");
					}

					lastHistoricData = Utils.FromUnixTime(endTime);
				}
				maxArchiveRuns = -1;
				return;
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				if (worker.CancellationPending)
					return;

				try
				{
					// Not all sensors may have the same number of records. We are using the WLL to create the historic data, the other sensors (AirLink) may have more or less records!
					// For the additional sensors, check if they have the same number of records as the WLL. If they do great, we just process the next record.
					// If the sensor has more or less historic records than the WLL, then we find the record (if any) that matches the WLL record timestamp

					var refData = sensorWithMostRecs.data[dataIndex].FromJsv<WlHistorySensorDataType13Baro>();
					var timestamp = Utils.FromUnixTime(refData.ts);
					DataDateTime = timestamp;

					cumulus.LogMessage($"GetHistoricData: Processing record {timestamp:yyyy-MM-dd HH:mm}");

					var h = timestamp.Hour;

					// Things that really "should" to be done before we reset the day because the roll-over data contains data for the previous day for these values
					// Windrun
					// Dominant wind bearing
					// ET - if MX calculated
					// Degree days
					// Rainfall

					//  if outside roll-over hour, roll-over yet to be done
					if (h != rollHour)
					{
						rolloverdone = false;
					}
					else if (!rolloverdone)
					{
						// In roll-over hour and roll-over not yet done
						// do roll-over
						cumulus.LogMessage("GetHistoricData: Day roll-over " + timestamp.ToShortTimeString());
						DayReset(timestamp);
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
						ResetMidnightRain(timestamp);
						ResetSunshineHours(timestamp);
						ResetMidnightTemperatures(timestamp);
						midnightraindone = true;
					}

					// 9am rollover items
					if (h != 9)
					{
						rollover9amdone = false;
					}
					else if (!rollover9amdone)
					{
						Reset9amTemperatures(timestamp);
						rollover9amdone = true;
					}

					// Not in snow hour, snow yet to be done
					if (h != cumulus.SnowDepthHour)
					{
						snowhourdone = false;
					}
					else if (!snowhourdone)
					{
						// snowhour items
						if (cumulus.SnowAutomated > 0)
						{
							CreateNewSnowRecord(timestamp);
						}

						// reset the accumulated snow depth(s)
						for (int i = 0; i < Snow24h.Length; i++)
						{
							Snow24h[i] = null;
						}

						snowhourdone = true;
					}

					DecodeHistoric(sensorWithMostRecs.data_structure_type, sensorWithMostRecs.sensor_type, sensorWithMostRecs.data[dataIndex]);

					foreach (var sensor in histObj.sensors)
					{
						if (worker.CancellationPending)
							return;

						int sensorType = sensor.sensor_type;
						int dataStructureType = sensor.data_structure_type;
						int lsid = sensor.lsid;

						if (sensorType == 323 && cumulus.airLinkOut != null) // AirLink Outdoor
						{
							if (sensor.data.Count != noOfRecs)
							{
								var found = false;
								foreach (var dataRec in sensor.data)
								{
									if (worker.CancellationPending)
										return;

									var rec = dataRec.FromJsv<WlHistorySensorDataType17>();
									if (rec.ts == refData.ts)
									{
										// Pass AirLink historic record to the AirLink module to process
										cumulus.airLinkOut.DecodeAlHistoric(dataStructureType, dataRec);
										found = true;
										break;
									}
								}
								if (!found)
									cumulus.LogDebugMessage("GetHistoricData: Warning. No outdoor AirLink data for this log interval !!");
							}
							else
							{
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkOut.DecodeAlHistoric(dataStructureType, sensor.data[dataIndex]);
							}
						}
						else if (sensorType == 326 && cumulus.airLinkIn != null) // AirLink Indoor
						{
							if (sensor.data.Count != noOfRecs)
							{
								var found = false;
								foreach (var dataRec in sensor.data)
								{
									if (worker.CancellationPending)
										return;

									var rec = dataRec.FromJsv<WlHistorySensorDataType17>();

									if (rec.ts == refData.ts)
									{
										// Pass AirLink historic record to the AirLink module to process
										cumulus.airLinkIn.DecodeAlHistoric(dataStructureType, dataRec);
										found = true;
										break;
									}
								}
								if (!found)
									cumulus.LogDebugMessage("GetHistoricData: Warning. No indoor AirLink data for this log interval !!");
							}
							else
							{
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkIn.DecodeAlHistoric(dataStructureType, sensor.data[dataIndex]);
							}
						}
						else if (sensorType != 504 && sensorType != 506 && lsid != sensorWithMostRecs.lsid && sensor.data.Count > dataIndex)
						{
							DecodeHistoric(dataStructureType, sensorType, sensor.data[dataIndex]);
							// sensor 504 (WLL info) does not always contain a full set of records, so grab the timestamp from a 'real' sensor
						}
					}


					// Now we have the primary data, calculate the derived data
					if (cumulus.StationOptions.CalculatedWC)
					{
						// DoWindChill does all the required checks and conversions
						DoWindChill(OutdoorTemperature, timestamp);
					}

					DoApparentTemp(timestamp);
					DoFeelsLike(timestamp);
					DoHumidex(timestamp);
					DoCloudBaseHeatIndex(timestamp);
					DoTrendValues(timestamp);

					if (cumulus.StationOptions.CalculatedET && timestamp.Minute == 0)
					{
						// Start of a new hour, and we want to calculate ET in Cumulus
						CalculateEvapotranspiration(timestamp);
					}

					if (cumulus.StationOptions.CalculateSLP && StationPressure > 0)
					{
						var slp = MeteoLib.GetSeaLevelPressure(ConvertUnits.AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToHpa(StationPressure), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
						DoPressure(ConvertUnits.PressMBToUser(slp), timestamp);
					}

					// Log all the data
					_ = cumulus.DoLogFile(timestamp, false);
					cumulus.MySqlRealtimeFile(999, false, timestamp);
					cumulus.DoCustomIntervalLogs(timestamp);

					if (cumulus.StationOptions.LogExtraSensors)
					{
						_ = cumulus.DoExtraLogFile(timestamp);
					}

					if (cumulus.airLinkOut != null || cumulus.airLinkIn != null)
					{
						_ = cumulus.DoAirLinkLogFile(timestamp);
					}

					// Custom MySQL update - minutes interval
					if (cumulus.MySqlSettings.CustomMins.Enabled)
					{
						_ = cumulus.CustomMysqlMinutesUpdate(timestamp, false);
					}

					AddRecentDataWithAq(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
						OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);

					UpdateStatusPanel(timestamp);
					cumulus.AddToWebServiceLists(timestamp);


					if (!Program.service)
						Console.Write("\r - processed " + (((double) dataIndex + 1) / noOfRecs).ToString("P0"));
					cumulus.LogMessage($"GetHistoricData: {dataIndex + 1} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("GetHistoricData: Exception: " + ex.Message);
				}
			}

			if (!Program.service)
				Console.WriteLine(""); // flush the progress line

			lastHistoricData = Utils.FromUnixTime(endTime);
		}


		private void DecodeCurrent(List<WlCurrentSensor> sensors)
		{
			// The WLL sends the timestamp in Unix ticks, and in UTC
			var dataUpdated = false;

			foreach (var sensor in sensors)
			{
				if (!Program.ExitSystemToken.IsCancellationRequested)
				{
					return;
				}

				try
				{
					// first separate out the "special" sensors
					switch (sensor.sensor_type)
					{
						// Leaf/soil sensor
						case 56:
							{
								var data = sensor.data.FromJsv<WLCurrentSensorDataType12_25[]>();
								var txid = lsidToTx[sensor.lsid];
								/*
								 * Leaf Wetness
								 * wet_leaf_1
								 * wet_leaf_2
								 */

								foreach (var rec in data)
								{
									try
									{
										if (rec.ts == lsidLastUpdate[sensor.lsid])
										{
											cumulus.LogDebugMessage($"DecodeCurrent: Skipping Leaf/Soil sensor {txid}, already processed this ts");
											continue;
										}
										cumulus.LogDebugMessage($"DecodeCurrent: Using Leaf/Soil {txid}");
										dataUpdated = true;
									}
									catch (KeyNotFoundException)
									{
										lsidLastUpdate.Add(sensor.lsid, rec.ts);
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error determining Leaf/Soil last ts");
									}

									lsidLastUpdate[sensor.lsid] = rec.ts;

									// We are relying on user configuration here, trap any errors
									for (var i = 1; i <= numLeafWetnessSensors; i++)
									{
										try
										{
											if (cumulus.WllLeafWetTx[i] == txid || isVp2Station)
											{
												var idx = "wet_leaf_" + cumulus.WllLeafWetIdx[i];
												var val = (double?) rec[idx];
												if (val.HasValue)
												{
													DoLeafWetness(val.Value, i);
												}
											}
										}
										catch (Exception e)
										{
											cumulus.LogExceptionMessage(e, $"Error, DecodeCurrent, LeafWetness tx={txid}");
										}
									}

									/*
									 * Soil Moisture
									 * Available fields
									 * moist_soil_1
									 * moist_soil_2
									 * moist_soil_3
									 * moist_soil_4
									 *
									 */

									for (var i = 1; i <= numSoilMoistureSensors; i++)
									{
										try
										{
											if (cumulus.WllSoilMoistureTx[i] == txid || isVp2Station)
											{
												var idx = "moist_soil_" + cumulus.WllSoilMoistureIdx[i];
												var val = (double?) rec[idx];
												if (val.HasValue)
												{
													DoSoilMoisture(val.Value, i);
												}
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing soil moisture #{cumulus.WllSoilMoistureIdx[i]} on TxId {txid}");
										}
									}

									/*
									* Soil Temperature
									* Available fields
									* temp_1
									* temp_2
									* temp_3
									* temp_4
									*/

									for (var i = 1; i <= numSoiltempSensors; i++)
									{
										try
										{
											// allocated to soil temp?
											if (cumulus.WllSoilTempTx[i] == txid || isVp2Station)
											{
												var idx = "temp_" + cumulus.WllSoilTempIdx[i];
												var val = (double?) rec[idx];
												if (val.HasValue)
												{
													DoSoilTemp(ConvertUnits.TempFToUser(val.Value), i);
												}
											}

											// allocated to extra temp?
											else if (cumulus.WllExtraTempTx[i] == txid)
											{
												var idx = "temp_" + cumulus.WllExtraTempIdx[i];
												var val = (double?) rec[idx];
												if (val.HasValue)
												{
													DoExtraTemp(ConvertUnits.TempFToUser(val.Value), i);
												}
											}

										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing extra soil temp #{cumulus.WllSoilTempIdx[i]} on TxId {txid}");
										}
									}

									// health data
									if (rec.trans_battery_flag.HasValue)
									{
										SetTxBatteryStatus(txid, rec.trans_battery_flag.Value);

										if (rec.trans_battery_flag == 1)
										{
											cumulus.LogWarningMessage($"Battery voltage is low in TxId {txid}");
										}
									}
									if (rec.rx_state.HasValue && rec.rx_state != 0)
									{
										cumulus.LogWarningMessage($"Receive state for ISS TxId {txid} is: {(rec.rx_state == 1 ? "Rescan" : "Lost")}");
									}

								}
							}
							break;

						// WLL/WLC barometer
						case 242:
							/*
							* Available fields
							* bar_sea_level
							* bar_trend
							* bar_absolute
							* bar_offset
							*/
							{
								// log the current value
								try
								{
									var data = sensor.data.FromJsv<WlCurrentSensorDataType12_19Baro[]>();

									foreach (var rec in data)
									{
										try
										{
											if (rec.ts == lsidLastUpdate[sensor.lsid])
											{
												cumulus.LogDebugMessage($"DecodeCurrent: Skipping barometer, already processed this ts");
												continue;
											}
											cumulus.LogDebugMessage("DecodeCurrent: Using barometer");
											dataUpdated = true;
										}
										catch (KeyNotFoundException)
										{
											lsidLastUpdate.Add(sensor.lsid, rec.ts);
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error determining barometer last ts");
										}

										lsidLastUpdate[sensor.lsid] = rec.ts;
										var ts = Utils.FromUnixTime(rec.ts);

										if (!cumulus.StationOptions.CalculateSLP)
										{
											if (rec.bar_sea_level.HasValue)
											{
												// leave it at current value
												DoPressure(ConvertUnits.PressINHGToUser(rec.bar_sea_level.Value), ts);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Baro data (slp)");
											}
										}

										// Altimeter from absolute
										if (rec.bar_absolute.HasValue)
										{
											DoStationPressure(ConvertUnits.PressINHGToUser(rec.bar_absolute.Value));
										}

									}
								}
								catch (Exception ex)
								{
									cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing baro reading");
								}
							}

							break;

						// WLL/WLC Inside temp/hum
						case 243:
						case 365:
							/*
							 * Available fields
							 * temp_in
							 * hum_in
							 * dew_point_in
							 * heat_index_in
							 * wet_bulb_in
							 * wbgt_in
							 */
							{
								var data = sensor.data.FromJsv<WlCurrentSensorDataType12_21Temp[]>();

								foreach (var rec in data)
								{
									try
									{
										if (rec.ts == lsidLastUpdate[sensor.lsid])
										{
											cumulus.LogDebugMessage("DecodeCurrent: Skipping inside temp/hum, already processed this ts");
											continue;
										}
										cumulus.LogDebugMessage($"DecodeCurrent: Using Inside temp/hum");
										dataUpdated = true;
									}
									catch (KeyNotFoundException)
									{
										lsidLastUpdate.Add(sensor.lsid, rec.ts);
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error determining inside t/h last ts");
									}

									lsidLastUpdate[sensor.lsid] = rec.ts;

									try
									{
										if (rec.temp_in.HasValue)
										{
											DoIndoorTemp(ConvertUnits.TempFToUser(rec.temp_in.Value));
										}
										else
										{
											cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Inside Temperature");
										}
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing temp-in reading");
									}


									try
									{
										if (rec.hum_in.HasValue)
										{
											DoIndoorHumidity(Convert.ToInt32(rec.hum_in.Value));
										}
										else
										{
											cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Inside Humidity");
										}
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing humidity-in");
									}
								}
							}

							break;

						// WLL Health
						case 504:
							{
								var data = sensor.data.FromJsv<WlHealthDataType15[]>();
								foreach (var rec in data)
								{
									try
									{
										if (rec.ts == lsidLastUpdate[sensor.lsid])
										{
											cumulus.LogDebugMessage($"DecodeCurrent: Skipping WLL Health, already processed this ts");
											continue;
										}
										cumulus.LogDebugMessage("DecodeCurrent: Using WLL health");
										dataUpdated = true;
									}
									catch (KeyNotFoundException)
									{
										lsidLastUpdate.Add(sensor.lsid, rec.ts);
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error determining WLL health last ts");
									}

									lsidLastUpdate[sensor.lsid] = rec.ts;

									DecodeWllHealth(rec, false);
								}
							}
							break;

						// WLC health
						case 509:
							{
								var data = sensor.data.FromJsv<WlHealthDataType27[]>();
								foreach (var rec in data)
								{
									try
									{
										if (rec.ts == lsidLastUpdate[sensor.lsid])
										{
											cumulus.LogDebugMessage($"DecodeCurrent: Skipping WLC Health, already processed this ts");
											continue;
										}
										cumulus.LogDebugMessage("DecodeCurrent: Using WLC health");
										dataUpdated = true;
									}
									catch (KeyNotFoundException)
									{
										lsidLastUpdate.Add(sensor.lsid, rec.ts);
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error determining WLC health last ts");
									}

									lsidLastUpdate[sensor.lsid] = rec.ts;
									DecodeWlcHealth(rec, false);
								}
							}
							break;

						// AirLink - Outdoor/Indoor
						case 323:
						case 326:
							//TODO: Decode AirLink
							break;

						// AirLink Health
						case 506:
							//TODO: Decode AirLink Health
							break;

						// everything else is ISS - too many sensor types to list, just go on structure type!
						default:
							switch (sensor.data_structure_type)
							{
								// VP2 ISS archive revision A/B
								case 1:
								case 2:
									{
										// VP2 sensor data is sent as an array of one, so we will strip off the enclosing [ ]
										// and uck, uck, uck, we need to re-quote the forecast description as ithe quotes get stripped by the .FromJson() into a string
										var str = sensor.data[1..^2].Replace("forecast_desc:", "forecast_desc:\"").Replace(",dew_point", "\",dew_point");
										var data = str.FromJsv<WLCurrentSensordDataType1_2>();

										try
										{
											if (data.ts == lsidLastUpdate[sensor.lsid])
											{
												cumulus.LogDebugMessage($"DecodeCurrent: Skipping record type {sensor.data_structure_type}, already processed this ts");
												continue;
											}
											cumulus.LogDebugMessage($"DecodeCurrent: Using this record type {sensor.data_structure_type}");
											dataUpdated = true;
										}
										catch (KeyNotFoundException)
										{
											lsidLastUpdate.Add(sensor.lsid, data.ts);
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error determining last ts");
										}

										lsidLastUpdate[sensor.lsid] = data.ts;

										lastRecordTime = Utils.FromUnixTime(data.ts);
										// Temperature & Humidity
										/*
										 * Available fields
										 * hum_in
										 * hum_out
										 * temp_in
										 * temp_out
										 * dew_point
										 * wet_bulb
										 * heat_index
										 * wind_chill
										 * thw_index
										 * thsw_index
										 * wbgt
										 * hdd_day
										 * cdd_day
										 */

										try
										{
											// do humidity
											if (data.hum_out.HasValue)
											{
												DoOutdoorHumidity(Convert.ToInt32(data.hum_out.Value), lastRecordTime);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Humidity data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing Primary humidity");
										}

										// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
										try
										{
											if (data.temp_out.HasValue && data.temp_out < -98)
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Primary temperature value found [-99]");
											}
											else
											{
												cumulus.LogDebugMessage("DecodeCurrent: using temp/hum data");

												// do last temp
												if (data.temp_out.HasValue)
												{
													DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out.Value), lastRecordTime);
												}
												else
												{
													cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Temperature data");
												}
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing Primary temperature");
										}


										try
										{
											// do DP
											if (!cumulus.StationOptions.CalculatedDP && data.dew_point.HasValue)
											{
												DoOutdoorDewpoint(ConvertUnits.TempFToUser(data.dew_point.Value), lastRecordTime);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Dew Point data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing dew point value");
										}

										// use wind chill from station - otherwise we calculate it at the end of processing the record when we have all the data
										try
										{
											// do last WC
											if (!cumulus.StationOptions.CalculatedWC && data.wind_chill.HasValue)
											{
												DoWindChill(ConvertUnits.TempFToUser(data.wind_chill.Value), lastRecordTime);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Wind Chill data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing wind chill");
										}

										// indoor data
										try
										{
											if (data.temp_in.HasValue)
											{
												DoIndoorTemp(ConvertUnits.TempFToUser(data.temp_in.Value));
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid indoor temperature data");
											}

											if (data.hum_in.HasValue)
											{
												DoIndoorHumidity(data.hum_in.Value);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid indoor temperature data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing indoor temp/humidity");
										}

										// Wind
										/*
											* Available fields
											*
											* wind_speed
											* wind_dir
											* wind_speed_10_min_avg
											* wind_gust_10_min
											*
											*/

										try
										{
											if (data.wind_gust_10_min.HasValue && data.wind_speed.HasValue && data.wind_dir.HasValue)
											{
												// The cloud data isn't coming in a a rapid enough rate to perform averaging, so just use the Davis values
												var gust = ConvertUnits.WindMPHToUser(data.wind_gust_10_min.Value);
												var spd = ConvertUnits.WindMPHToUser(data.wind_speed.Value);
												// dir is a direction code: 0=N, 1=NNE, ... 14=NW, 15=NNW - convert to degress
												var dir = (int) ((data.wind_dir ?? 0) * 22.5);

												cumulus.LogDebugMessage("DecodeCurrent: using wind data");

												DoWind(gust, dir, spd, lastRecordTime);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Wind data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing wind values");
										}

										// Rainfall
										/*
											* Available fields:
											*
											* rain_rate_clicks
											* rain_rate_in
											* rain_rate_mm
											* rain_storm_clicks
											* rain_storm_in
											* rain_storm_mm
											* rain_storm_start_date
											* rain_day_clicks
											* rain_day_in
											* rain_day_mm
											* rain_month_clicks
											* rain_month_in
											* rain_month_mm
											* rain_year_clicks
											* rain_year_in
											* rain_year_mm
											*
											*/

										try
										{
											if (data.rain_year_clicks.HasValue && data.rain_rate_clicks.HasValue)
											{
												cumulus.LogDebugMessage("DecodeCurrent: using rain data");

												var rain = ConvertRainClicksToUser(data.rain_year_clicks.Value, cumulus.DavisOptions.RainGaugeType);
												var rainrate = ConvertRainClicksToUser(data.rain_rate_clicks.Value, cumulus.DavisOptions.RainGaugeType);
												if (rainrate < 0)
												{
													rainrate = 0;
												}

												DoRain(rain, rainrate, lastRecordTime);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Rain data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing rain data");
										}

										// Pressure
										/*
											bar
										*/
										// log the current value
										try
										{
											if (data.bar != null)
											{
												// leave it at current value
												cumulus.LogDebugMessage("DecodeCurrent: found Baro data");
												DoPressure(ConvertUnits.PressINHGToUser((double) data.bar), lastRecordTime);
											}
											else
											{
												cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Baro data");
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing baro reading");
										}


										// UV
										/*
											* Available fields
											* "uv"
											*/
										try
										{
											if (data.uv.HasValue)
											{
												cumulus.LogDebugMessage("DecodeCurrent: using UV data");

												DoUV(data.uv.Value, lastRecordTime);
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing UV value");
										}

										// Solar
										/*
											* Available fields
											* solar_rad
											* et_day (inches) - ET field is populated in the ISS archive records, which may not be the same as the solar
											* et_month - only incremented at rollover
											* et_year - only incremented at rollover
											*/
										try
										{
											if (data.solar_rad.HasValue)
											{
												cumulus.LogDebugMessage("DecodeCurrent: using solar data");
												DoSolarRad(data.solar_rad.Value, lastRecordTime);

												if (data.et_year.HasValue && data.et_day.HasValue && !cumulus.StationOptions.CalculatedET && (data.et_year.Value >= 0) && (data.et_year.Value < 32000))
												{
													DoET(ConvertUnits.RainINToUser(data.et_year.Value + data.et_day.Value), lastRecordTime);
												}
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing Solar value");
										}


										// Leaf Wetness
										try
										{

											if (data.wet_leaf_1.HasValue)
											{
												DoLeafWetness(data.wet_leaf_1.Value, 1);
											}
											if (data.wet_leaf_2.HasValue)
											{
												DoLeafWetness(data.wet_leaf_2.Value, 2);
											}
											if (data.wet_leaf_3.HasValue)
											{
												DoLeafWetness(data.wet_leaf_3.Value, 3);
											}
											if (data.wet_leaf_4.HasValue)
											{
												DoLeafWetness(data.wet_leaf_4.Value, 4);
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing LeafWetness");
										}


										// Soil Moisture
										try
										{
											if (data.moist_soil_1.HasValue)
											{
												DoSoilMoisture(data.moist_soil_1.Value, 1);
											}
											if (data.moist_soil_2.HasValue)
											{
												DoSoilMoisture(data.moist_soil_2.Value, 2);
											}
											if (data.moist_soil_3.HasValue)
											{
												DoSoilMoisture(data.moist_soil_3.Value, 3);
											}
											if (data.moist_soil_4.HasValue)
											{
												DoSoilMoisture(data.moist_soil_4.Value, 4);
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing SoilMoisture");
										}


										// Soil Temperature
										try
										{
											if (data.temp_soil_1.HasValue)
											{
												DoSoilMoisture(ConvertUnits.TempFToUser(data.temp_soil_1.Value), 1);
											}
											if (data.temp_soil_2.HasValue)
											{
												DoSoilMoisture(ConvertUnits.TempFToUser(data.temp_soil_2.Value), 2);
											}
											if (data.temp_soil_3.HasValue)
											{
												DoSoilMoisture(ConvertUnits.TempFToUser(data.temp_soil_3.Value), 3);
											}
											if (data.temp_soil_4.HasValue)
											{
												DoSoilMoisture(ConvertUnits.TempFToUser(data.temp_soil_4.Value), 4);
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing SoilTemp");
										}

										// Extra Temperature
										try
										{
											if (data.temp_extra_1.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_1.Value), 1);
											}
											if (data.temp_extra_2.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_2.Value), 2);
											}
											if (data.temp_extra_3.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_3.Value), 3);
											}
											if (data.temp_extra_4.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_4.Value), 4);
											}
											if (data.temp_extra_5.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_5.Value), 5);
											}
											if (data.temp_extra_6.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_6.Value), 6);
											}
											if (data.temp_extra_7.HasValue)
											{
												DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_7.Value), 7);
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing ExtraTemp");
										}

										// Extra Humidity
										try
										{
											if (data.hum_extra_1.HasValue)
											{
												DoExtraHum(data.hum_extra_1.Value, 1);
											}
											if (data.hum_extra_2.HasValue)
											{
												DoExtraHum(data.hum_extra_2.Value, 2);
											}
											if (data.hum_extra_3.HasValue)
											{
												DoExtraHum(data.hum_extra_3.Value, 3);
											}
											if (data.hum_extra_4.HasValue)
											{
												DoExtraHum(data.hum_extra_4.Value, 4);
											}
											if (data.hum_extra_5.HasValue)
											{
												DoExtraHum(data.hum_extra_5.Value, 5);
											}
											if (data.hum_extra_6.HasValue)
											{
												DoExtraHum(data.hum_extra_6.Value, 6);
											}
											if (data.hum_extra_7.HasValue)
											{
												DoExtraHum(data.hum_extra_7.Value, 7);
											}
										}
										catch (Exception ex)
										{
											cumulus.LogExceptionMessage(ex, "DecodeCurrent: Error processing ExtraHum");
										}
									}
									break;

								// WLL/WLC ISS data
								case 10:
								case 23:
									{
										var data = sensor.data.FromJsv<WLCurrentSensorDataType10_23[]>();

										foreach (var rec in data)
										{
											try
											{
												if (rec.ts == lsidLastUpdate[sensor.lsid])
												{
													cumulus.LogDebugMessage($"DecodeCurrent: Skipping ISS {rec.tx_id}, type {sensor.data_structure_type}, already processed this ts");
													continue;
												}
												cumulus.LogDebugMessage($"DecodeCurrent: Using ISS {rec.tx_id}, type {sensor.data_structure_type}");
												dataUpdated = true;
											}
											catch (KeyNotFoundException)
											{
												lsidLastUpdate.Add(sensor.lsid, rec.ts);
											}
											catch (Exception ex)
											{
												cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error determining ISS {rec.tx_id} last ts");
											}

											lsidLastUpdate[sensor.lsid] = rec.ts;

											lastRecordTime = Utils.FromUnixTime(rec.ts);

											// Temperature & Humidity
											if (cumulus.WllPrimaryTempHum == rec.tx_id)
											{
												/*
												 * Available fields
												 * temp
												 * hum
												 * dew_point
												 * wet_bulb
												 * heat_index
												 * wind_chill
												 * thw_index
												 * thsw_index
												 * wbgt
												 */


												try
												{
													// do humidity
													if (rec.hum.HasValue)
													{
														// do current humidity
														DoOutdoorHumidity(Convert.ToInt32(rec.hum), lastRecordTime);
													}
													else
													{
														cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid Humidity data on TxId {rec.tx_id}");
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing Primary humidity value on TxId {rec.tx_id}");
												}

												// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
												try
												{
													if (rec.temp < -98)
													{
														cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid Primary temperature value found [-99] on TxId {rec.tx_id}");
													}
													else
													{
														cumulus.LogDebugMessage($"DecodeCurrent: using temp/hum data from TxId {rec.tx_id}");

														// do last temp
														if (rec.hum.HasValue)
														{
															DoOutdoorTemp(ConvertUnits.TempFToUser(rec.temp.Value), lastRecordTime);
														}
														else
														{
															cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid Temperature data on TxId {rec.tx_id}");
														}
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing Primary temperature value on TxId {rec.tx_id}");
												}


												try
												{
													// do DP
													if (rec.dew_point.HasValue)
													{
														DoOutdoorDewpoint(ConvertUnits.TempFToUser(rec.dew_point.Value), lastRecordTime);
													}
													else
													{
														cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid Dew Point data on TxId {rec.tx_id}");
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing dew point value on TxId {rec.tx_id}");
												}

												// use wind chill from WL.com - otherwise we calculate it at the end of processing the historic record when we have all the data
												try
												{
													// do last WC
													if (!cumulus.StationOptions.CalculatedWC && rec.wind_chill.HasValue)
													{
														DoWindChill(ConvertUnits.TempFToUser(rec.wind_chill.Value), lastRecordTime);
													}
													else
													{
														cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid Wind Chill data on TxId {rec.tx_id}");
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing wind chill value on TxId {rec.tx_id}");
												}
											}

											// Wind
											if (cumulus.WllPrimaryWind == rec.tx_id)
											{
												/*
												 * Available fields
												 *
												 * wind_speed_last
												 * wind_dir_last
												 * wind_speed_avg_last_1_min
												 * wind_dir_scalar_avg_last_1_min
												 * wind_speed_avg_last_2_min
												 * wind_dir_scalar_avg_last_2_min
												 * wind_speed_hi_last_2_min
												 * wind_dir_at_hi_speed_last_2_min
												 * wind_speed_avg_last_10_min
												 * wind_dir_scalar_avg_last_10_min
												 * wind_speed_hi_last_10_min
												 * wind_dir_at_hi_speed_last_10_min
												 * wind_run_day
												*/

												try
												{
													double speed;
													int bearing;
													double gust;
													int gustDir;

													if (cumulus.StationOptions.AvgSpeedMinutes < 10)
													{
														speed = ConvertUnits.WindMPHToUser(rec.wind_speed_avg_last_2_min ?? 0);
													}
													else
													{
														speed = ConvertUnits.WindMPHToUser(rec.wind_speed_avg_last_10_min ?? 0);
													}

													if (cumulus.StationOptions.AvgBearingMinutes < 10)
													{
														bearing = rec.wind_dir_scalar_avg_last_2_min;
													}
													else
													{
														bearing = rec.wind_dir_scalar_avg_last_10_min;
													}

													if (cumulus.StationOptions.PeakGustMinutes < 10)
													{
														gust = ConvertUnits.WindMPHToUser(rec.wind_speed_hi_last_2_min ?? 0);
													}
													else
													{
														gust = ConvertUnits.WindMPHToUser(rec.wind_speed_hi_last_10_min ?? 0);
													}

													DoWind(gust, bearing, speed, lastRecordTime);

													gust = ConvertUnits.WindMPHToUser(rec.wind_speed_hi_last_10_min ?? 0);
													gustDir = rec.wind_dir_at_hi_speed_last_10_min;

													var gustCal = cumulus.Calib.WindGust.Calibrate(gust);
													var gustDirCal = gustDir == 0 ? 0 : (int) cumulus.Calib.WindDir.Calibrate(gustDir);

													// See if the current speed is higher than the current max
													// We can then update the figure before the next data packet is read

													cumulus.LogDebugMessage($"DecodeCurrent: Checking recent gust using wind data from TxId {rec.tx_id}");

													// Check for spikes, and set highs
													if (CheckHighGust(gustCal, gustDirCal, lastRecordTime))
													{
														cumulus.LogDebugMessage("Setting max gust from current value: " + gustCal.ToString(cumulus.WindFormat) + " was: " + RecentMaxGust.ToString(cumulus.WindFormat));
														AddValuesToRecentWind(gust, WindAverage, gustDir, lastRecordTime, lastRecordTime);
														RecentMaxGust = gustCal;
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing wind values on TxId {rec.tx_id}");
												}
											}

											// Rainfall
											if (cumulus.WllPrimaryRain == rec.tx_id)
											{
												/*
												 * Available fields:
												 * rain_size
												 * rain_rate_last_clicks
												 * rain_rate_last_in
												 * rain_rate_last_mm
												 * rain_rate_hi_clicks
												 * rain_rate_hi_in
												 * rain_rate_hi_mm
												 *
												 * rainfall_last_15_min_clicks
												 * rainfall_last_15_min_in
												 * rainfall_last_15_min_mm
												 * rain_rate_hi_last_15_min_clicks
												 * rain_rate_hi_last_15_min_in
												 * rain_rate_hi_last_15_min_mm
												 *
												 * rainfall_last_60_min_clicks
												 * rainfall_last_60_min_in
												 * rainfall_last_60_min_mm
												 *
												 * rainfall_last_24_hr_clicks
												 * rainfall_last_24_hr_in
												 * rainfall_last_24_hr_mm
												 *
												 * rain_storm_clicks
												 * rain_storm_in
												 * rain_storm_mm
												 * rain_storm_start_at
												 * rain_storm_last_clicks
												 * rain_storm_last_in
												 * rain_storm_last_mm
												 * rain_storm_last_start_at
												 * rain_storm_last_end_at
												 *
												 * rainfall_daily_clicks
												 * rainfall_daily_in
												 * rainfall_daily_mm
												 * rainfall_monthly_clicks
												 * rainfall_monthly_in
												 * rainfall_monthly_mm
												 * rainfall_year_clicks
												 * rainfall_year_in
												 * rainfall_year_mm
												 */


												try
												{
													if (!rec.rainfall_year_clicks.HasValue || !rec.rain_rate_last_clicks.HasValue || !rec.rain_size.HasValue)
													{
														cumulus.LogDebugMessage("DecodeCurrent: No rain values present!");
													}
													else
													{
														var rain = ConvertRainClicksToUser(rec.rainfall_year_clicks.Value, rec.rain_size.Value);
														var rainrate = ConvertRainClicksToUser(rec.rain_rate_last_clicks.Value, rec.rain_size.Value);

														DoRain(rain, rainrate, lastRecordTime);
													}

													if (!rec.rain_storm_clicks.HasValue || !rec.rain_storm_start_at.HasValue || !rec.rain_size.HasValue)
													{
														cumulus.LogDebugMessage("DecodeCurrent: No rain storm values present");
													}
													else
													{
														try
														{
															StormRain = ConvertRainClicksToUser(rec.rain_storm_clicks.Value, rec.rain_size.Value) * cumulus.Calib.Rain.Mult;
															StartOfStorm = Utils.FromUnixTime(rec.rain_storm_start_at.Value);
														}
														catch (Exception ex)
														{
															cumulus.LogErrorMessage($"DecodeCurrent: Error processing rain storm values on TxId {rec.tx_id}");
															cumulus.LogDebugMessage($"DecodeCurrent: Exception: {ex.Message}");
														}
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing rain data on TxId {rec.tx_id}");
												}

											}

											// UV
											if (cumulus.WllPrimaryUV == rec.tx_id)
											{
												/*
												 * Available fields
												 * uv_index
												 * uv_dose_day
												 */
												try
												{
													if (rec.uv_index.HasValue)
													{
														cumulus.LogDebugMessage($"DecodeCurrent: using UV data from TxId {rec.tx_id}");

														DoUV((double) rec.uv_index, lastRecordTime);
													}
													else
													{
														cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid UV data on TxId {rec.tx_id}");
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing UV value on TxId {rec.tx_id}");
												}

											}

											// Solar
											if (cumulus.WllPrimarySolar == rec.tx_id)
											{
												/*
												 * Available fields
												 * solar_rad
												 * solar_energy_day
												 * et_day (inches) - ET field is populated in the ISS archive records, which may not be the same as the solar
												 * et_month
												 * et_year
												 */
												try
												{
													if (rec.solar_rad.HasValue)
													{
														cumulus.LogDebugMessage($"DecodeCurrent: using solar data from TxId {rec.tx_id}");
														DoSolarRad((int) rec.solar_rad, lastRecordTime);
													}
													else
													{
														cumulus.LogWarningMessage($"DecodeCurrent: Warning, no valid Solar data on TxId {rec.tx_id}");
													}

													if (rec.et_year.HasValue && rec.et_day.HasValue && !cumulus.StationOptions.CalculatedET)
													{
														DoET(ConvertUnits.RainINToUser(rec.et_year.Value + rec.et_day.Value), lastRecordTime);
													}
												}
												catch (Exception ex)
												{
													cumulus.LogExceptionMessage(ex, $"DecodeCurrent: Error processing Solar value on TxId {rec.tx_id}");
												}
											}

											// health data
											if (rec.trans_battery_flag.HasValue)
											{
												SetTxBatteryStatus(rec.tx_id, rec.trans_battery_flag.Value);

												if (rec.trans_battery_flag == 1)
												{
													cumulus.LogWarningMessage($"Battery voltage is low in TxId {rec.tx_id}");
												}
											}
											if (rec.rx_state != 0)
											{
												cumulus.LogWarningMessage($"Receive state for ISS TxId {rec.tx_id} is: {(rec.rx_state == 1 ? "Rescan" : "Lost")}");
											}
										}
									}
									break;

								default:
									cumulus.LogDebugMessage($"DecodeCurrent: found an unknown data structure type [{sensor.data_structure_type}], SensorType={sensor.sensor_type}!");
									break;
							}

							break;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, $"Error, DecodeCurrent, DataType={sensor.data_structure_type}, SensorType={sensor.sensor_type}");
				}
			}

			if (!dataUpdated)
			{
				// no new data, bail out
				return;
			}

			var dateTime = DateTime.Now;

			// Now we have the primary data, calculate the derived data
			if (cumulus.StationOptions.CalculatedWC)
			{
				DoWindChill(OutdoorTemperature, dateTime);
			}

			DoApparentTemp(dateTime);
			DoFeelsLike(dateTime);
			DoHumidex(dateTime);
			DoCloudBaseHeatIndex(dateTime);

			if (cumulus.StationOptions.CalculateSLP)
			{
				if (StationPressure > 0)
				{
					var slp = MeteoLib.GetSeaLevelPressure(ConvertUnits.AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToHpa(StationPressure), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
					DoPressure(ConvertUnits.PressMBToUser(slp), dateTime);
				}
				else
				{
					cumulus.LogWarningMessage("DecodeCurrent: Warning, no valid Baro data (absolute)");
				}
			}


			DoForecast(string.Empty, false);

			UpdateStatusPanel(DateTime.Now);
			UpdateMQTT();

			LastDataReadTime = DateTime.Now;

			// If the station isn't using the logger function for WLL - i.e. no API key, then only alarm on Tx battery status
			// otherwise, trigger the alarm when we read the Health data which also contains the WLL backup battery status
			LowBatteryDevices.Clear();

			if (!cumulus.StationOptions.UseDataLogger && TxBatText.Contains("LOW"))
			{
				cumulus.BatteryLowAlarm.Triggered = true;
				// Just the low battery list
				var arr = TxBatText.Split(' ');
				for (int i = 0; i < arr.Length; i++)
				{
					if (arr[i].Contains("LOW"))
					{
						LowBatteryDevices.Add(arr[i]);
					}
				}
			}
		}



		private void DecodeHistoric(int dataType, int sensorType, string json, bool current = false)
		{
			// The WLL sends the timestamp in Unix ticks, and in UTC

			try
			{
				switch (dataType)
				{
					case 3: // VP2 ISS archive revision A
					case 4: // VP2 ISS archive revision B
					case 7: // EnviroMonitor ISS Archive Record
						{
							var data = json.FromJsv<WlHistorySensorDataType3_4>();
							lastRecordTime = Utils.FromUnixTime(data.ts);

							CheckLoggingDataStopped(data.arch_int / 60);

							// Temperature & Humidity
							/*
							 * Available fields
							 * deg_days_cool
							 * deg_days_heat
							 * dew_point_in
							 * dew_point_out
							 * heat_index_in
							 * heat_index_out
							 * hum_in
							 * hum_out
							 * temp_in
							 * temp_out
							 * temp_out_hi
							 * temp_out_lo
							 * wind_chill
							 */

							try
							{
								// do humidity
								if (data.hum_out.HasValue)
								{
									DoOutdoorHumidity(Convert.ToInt32(data.hum_out.Value), lastRecordTime);
								}
								else
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Warning, no valid Humidity data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing Primary humidity value on TxId {data.tx_id}. Error: {ex.Message}");
							}

							// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
							try
							{
								if (data.temp_out.HasValue && data.temp_out < -98)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Warning, no valid Primary temperature value found [-99] on TxId {data.tx_id}");
								}
								else
								{
									cumulus.LogDebugMessage($"DecodeHistoric: using temp/hum data from TxId {data.tx_id}");

									// do high temp
									if (data.temp_out_hi.HasValue)
									{
										DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out_hi.Value), lastRecordTime);
									}
									else
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Warning, no valid Temperature (high) data on TxId {data.tx_id}");
									}

									// do low temp
									if (data.temp_out_lo.HasValue)
									{
										DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out_lo.Value), lastRecordTime);
									}
									else
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Warning, no valid Temperature (low) data on TxId {data.tx_id}");
									}

									// do last temp
									if (data.temp_out.HasValue)
									{
										DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out.Value), lastRecordTime);

										if (!current)
										{
											// set the values for daily average, arch_int is in seconds, but always whole minutes
											tempsamplestoday += data.arch_int / 60;
											TempTotalToday += ConvertUnits.TempFToUser(data.temp_out.Value) * data.arch_int / 60;

											// update chill hours
											if (OutdoorTemperature < cumulus.ChillHourThreshold && OutdoorTemperature > cumulus.ChillHourBase)
											{
												// add interval minutes to chill hours - arch_int in seconds
												ChillHours += (data.arch_int / 3600.0);
											}

											// update heating/cooling degree days
											if (!current)
											{
												UpdateDegreeDays(data.arch_int / 60);
											}
										}
									}
									else
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Warning, no valid Temperature data on TxId {data.tx_id}");
									}
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing Primary temperature value on TxId {data.tx_id}. Error: {ex.Message}");
							}


							try
							{
								// do last DP
								if (data.dew_point_out.HasValue)
								{
									DoOutdoorDewpoint(ConvertUnits.TempFToUser(data.dew_point_out.Value), lastRecordTime);
								}
								else
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Warning, no valid Dew Point data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing dew point value on TxId {data.tx_id}. Error: {ex.Message}");
							}

							if (!cumulus.StationOptions.CalculatedWC)
							{
								// use wind chill from WLL - otherwise we calculate it at the end of processing the historic record when we have all the data
								try
								{
									// do last WC
									if (data.wind_chill.HasValue)
									{
										DoWindChill(ConvertUnits.TempFToUser(data.wind_chill.Value), lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Wind Chill data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing wind chill value on TxId {data.tx_id}. Error: {ex.Message}");
								}
							}

							// Wind
							/*
								* Available fields
								*
								* wind_dir_of_hi
								* wind_dir_of_prevail
								* wind_num_samples
								* wind_run
								* wind_speed_avg
								* wind_speed_hi
								*
								*/

							try
							{
								if (data.wind_speed_hi.HasValue && data.wind_dir_of_hi.HasValue && data.wind_speed_avg.HasValue)
								{
									var gust = ConvertUnits.WindMPHToUser(data.wind_speed_hi.Value);
									var spd = ConvertUnits.WindMPHToUser(data.wind_speed_avg.Value);
									// dir is a direction code: 0=N, 1=NNE, ... 14=NW, 15=NNW - convert to degress
									var dir = (int) ((data.wind_dir_of_hi ?? 0) * 22.5);
									cumulus.LogDebugMessage($"DecodeHistoric: using wind data from TxId {data.tx_id}");
									DoWind(gust, dir, spd, lastRecordTime);
									RecentMaxGust = cumulus.Calib.WindGust.Calibrate(gust);
								}
								else
								{
									cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid Wind data on TxId {data.tx_id}");
								}

								if (data.wind_speed_avg.HasValue)
								{
									WindAverage = cumulus.Calib.WindSpeed.Calibrate(ConvertUnits.WindMPHToUser(data.wind_speed_avg.Value));

									if (!current)
									{
										// add in 'archivePeriod' minutes worth of wind speed to windrun
										int interval = data.arch_int / 60;
										WindRunToday += ((WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval) / 60.0);
									}
								}
								else
								{
									cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid Wind data (avg) on TxId {data.tx_id}");
								}

								if (current)
								{
									// we do not have the latest value, so set a pseudo value between average and gust
									WindLatest = Program.RandGenerator.Next((int) WindAverage, (int) RecentMaxGust);
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing wind values on TxId {data.tx_id}. Error: {ex.Message}");
							}

							// Rainfall
							/*
								* Available fields:
								*
								* rain_rate_hi_clicks
								* rain_rate_hi_in
								* rain_rate_hi_mm
								* rainfall_clicks
								* rainfall_in
								* rainfall_mm
								*/


							// The WL API v2 does not provide any running totals for rainfall  :(
							// So we will have to add the interval data to the running total and hope it all works out!

							try
							{
								if (data.rainfall_clicks != null && data.rain_rate_hi_clicks.HasValue)
								{
									cumulus.LogDebugMessage($"DecodeHistoric: using rain data from TxId {data.tx_id}");

									var rain = ConvertRainClicksToUser(data.rainfall_clicks.Value, cumulus.DavisOptions.RainGaugeType);
									var rainrate = ConvertRainClicksToUser(data.rain_rate_hi_clicks.Value, cumulus.DavisOptions.RainGaugeType);
									if (rain > 0)
									{
										cumulus.LogDebugMessage($"DecodeHistoric: Adding rain {rain.ToString(cumulus.RainFormat)}");
									}
									rain += RainCounter;

									if (rainrate < 0)
									{
										rainrate = 0;
									}

									DoRain(rain, rainrate, lastRecordTime);
								}
								else
								{
									cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Rain data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing rain data on TxId {data.tx_id}. Error:{ex.Message}");
							}

							// Pressure
							/*
								bar
								abs_press
								bar_noaa
								bar_alt
							*/
							// log the current value
							cumulus.LogDebugMessage("DecodeHistoric: found Baro data");
							try
							{
								if (data.bar != null)
								{
									// leave it at current value
									DoPressure(ConvertUnits.PressINHGToUser((double) data.bar), lastRecordTime);
								}
								else
								{
									cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Baro data");
								}

								// Altimeter from absolute
								if (data.abs_press != null)
								{
									DoStationPressure(ConvertUnits.PressINHGToUser((double) data.abs_press));
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing baro reading. Error: {ex.Message}");
							}


							// UV
							/*
								* Available fields
								* "uv_dose"
								* "uv_index_avg"
								* "uv_index_hi"
								*/
							try
							{
								if (data.uv_index_avg.HasValue)
								{
									cumulus.LogDebugMessage($"DecodeHistoric: using UV data from TxId {data.tx_id}");

									DoUV(data.uv_index_avg.Value, lastRecordTime);
								}
								else
								{
									cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid UV data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing UV value on TxId {data.tx_id}. Error: {ex.Message}");
							}

							// Solar
							/*
								* Available fields
								* "solar_energy"
								* "solar_rad_avg"
								* "solar_rad_hi"
								* "et" (inches) - ET field is populated in the ISS archive records, which may not be the same as the solar
								*/
							try
							{
								if (data.solar_rad_avg.HasValue)
								{
									cumulus.LogDebugMessage($"DecodeHistoric: using solar data from TxId {data.tx_id}");
									DoSolarRad(data.solar_rad_avg.Value, lastRecordTime);

									if (!current && IsSunny)
									{
										// add in archive period worth of sunshine, if sunny - arch_int in seconds
										SunshineHours += (data.arch_int / 3600.0);
									}
								}
								else
								{
									cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Solar data on TxId {data.tx_id}");
								}

								if (data.et.HasValue && !cumulus.StationOptions.CalculatedET)
								{
									// wl.com ET is only available in record the start of each hour.
									// The number is the total for the one hour period.
									// This is unlike the existing VP2 when the ET is an annual running total
									// So we try and mimic the VP behaviour
									var newET = AnnualETTotal + ConvertUnits.RainINToUser(data.et.Value);
									cumulus.LogDebugMessage($"DecodeHistoric: Adding {ConvertUnits.RainINToUser(data.et.Value):F3} to ET");
									DoET(newET, lastRecordTime);
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"DecodeHistoric: Error processing Solar value on TxId {data.tx_id}. Error: {ex.Message}");
							}

							string idx = string.Empty;

							// Leaf Wetness
							cumulus.LogDebugMessage($"DecodeHistoric: found Leaf/Soil data on TxId {data.tx_id}");
							try
							{

								if (data.wet_leaf_1.HasValue)
								{
									DoLeafWetness(data.wet_leaf_1.Value, 1);
								}
								if (data.wet_leaf_2.HasValue)
								{
									DoLeafWetness(data.wet_leaf_2.Value, 2);
								}
							}
							catch (Exception e)
							{
								cumulus.LogErrorMessage($"Error, DecodeHistoric, LeafWetness txid={data.tx_id}: {e.Message}");
							}


							// Soil Moisture
							try
							{
								for (var i = 1; i <= 4; i++)
								{
									idx = "moist_soil_" + i;

									if (data[idx] != null)
									{
										DoSoilMoisture((double) data[idx], i);
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogErrorMessage($"Error, DecodeHistoric, SoilMoisture txid={data.tx_id}, idx={idx}: {e.Message}");
							}


							// Soil Temperature
							try
							{
								for (var i = 1; i <= 4; i++)
								{
									idx = "temp_soil_" + i;

									if (data[idx] != null)
									{
										DoSoilTemp((double) data[idx], i);
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogErrorMessage($"Error, DecodeHistoric, SoilTemp txid={data.tx_id}, idx={idx}: {e.Message}");
							}

							// Extra Temperature
							try
							{
								for (var i = 1; i <= 4; i++)
								{
									idx = "temp_extra_" + i;

									if (data[idx] != null)
									{
										DoExtraTemp((double) data[idx], i);
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogErrorMessage($"Error, DecodeHistoric, ExtraTemp txid={data.tx_id}, idx={idx}: {e.Message}");
							}

							// Extra Humidity
							try
							{
								for (var i = 1; i <= 4; i++)
								{
									idx = "hum_extra_" + i;

									if (data[idx] != null)
									{
										DoExtraHum((int) data[idx], i);
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogErrorMessage($"Error, DecodeHistoric, ExtraHum txid={data.tx_id}, idx={idx}: {e.Message}");
							}
						}
						break;

					case 11: // WLL ISS data
					case 24: // WL console ISS data
						{
							var data = json.FromJsv<WlHistorySensorDataType24>();
							lastRecordTime = Utils.FromUnixTime(data.ts);

							CheckLoggingDataStopped(data.arch_int / 60);

							// Temperature & Humidity
							if (cumulus.WllPrimaryTempHum == data.tx_id)
							{
								/*
								 * Available fields
								 * "cooling_degree_days"
								 * "dew_point_hi_at"
								 * "dew_point_hi"
								 * "dew_point_last"
								 * "dew_point_lo_at"
								 * "dew_point_lo"
								 * "heat_index_hi_at"
								 * "heat_index_hi"
								 * "heat_index_last"
								 * "heating_degree_days"
								 * "hum_hi_at"
								 * "hum_hi"
								 * "hum_last"
								 * "hum_lo_at"
								 * "hum_lo"
								 * "temp_avg"
								 * "temp_hi_at"
								 * "temp_last"
								 * "temp_lo_at"
								 * "temp_lo"
								 * "temp_max"
								 * "wind_chill_last"
								 * "wind_chill_lo_at"
								 * "wind_chill_lo"
								 */

								DateTime ts;

								try
								{
									// do high humidity
									if (data.hum_hi_at != 0 && data.hum_hi != null)
									{
										ts = Utils.FromUnixTime(data.hum_hi_at);
										DoOutdoorHumidity(Convert.ToInt32(data.hum_hi), ts);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Humidity (high) data on TxId {data.tx_id}");
									}

									// do low humidity
									if (data.hum_lo_at != 0 && data.hum_lo != null)
									{
										ts = Utils.FromUnixTime(data.hum_lo_at);
										DoOutdoorHumidity(Convert.ToInt32(data.hum_lo), ts);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Humidity (low) data on TxId {data.tx_id}");
									}

									if (data.hum_last != null)
									{
										// do current humidity
										DoOutdoorHumidity(Convert.ToInt32(data.hum_last), lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Humidity data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing Primary humidity value on TxId {data.tx_id}. Error: {ex.Message}");
								}

								// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
								try
								{
									if (data.temp_last < -98)
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Primary temperature value found [-99] on TxId {data.tx_id}");
									}
									else
									{
										cumulus.LogDebugMessage($"DecodeHistoric: using temp/hum data from TxId {data.tx_id}");

										// do high temp
										if (data.temp_hi_at != 0 && data.temp_hi != null)
										{
											ts = Utils.FromUnixTime(data.temp_hi_at);
											DoOutdoorTemp(ConvertUnits.TempFToUser((double) data.temp_hi), ts);
										}
										else
										{
											cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Temperature (high) data on TxId {data.tx_id}");
										}

										// do low temp
										if (data.temp_lo_at != 0 && data.temp_lo != null)
										{
											ts = Utils.FromUnixTime(data.temp_lo_at);
											DoOutdoorTemp(ConvertUnits.TempFToUser((double) data.temp_lo), ts);
										}
										else
										{
											cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Temperature (low) data on TxId {data.tx_id}");
										}

										// do last temp
										if (data.temp_last != null)
										{
											DoOutdoorTemp(ConvertUnits.TempFToUser((double) data.temp_last), lastRecordTime);

											if (!current)
											{
												// set the values for daily average, arch_int is in seconds, but always whole minutes
												tempsamplestoday += data.arch_int / 60;
												TempTotalToday += ConvertUnits.TempFToUser(data.temp_avg) * data.arch_int / 60;

												// update chill hours
												if (OutdoorTemperature < cumulus.ChillHourThreshold && OutdoorTemperature > cumulus.ChillHourBase)
												{
													// add interval minutes to chill hours - arch_int in seconds
													ChillHours += (data.arch_int / 3600.0);
												}

												// update heating/cooling degree days
												UpdateDegreeDays(data.arch_int / 60);
											}
										}
										else
										{
											cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Temperature data on TxId {data.tx_id}");
										}
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing Primary temperature value on TxId {data.tx_id}. Error: {ex.Message}");
								}


								try
								{
									// do high DP
									if (data.dew_point_hi_at != 0 && data.dew_point_hi != null)
									{
										ts = Utils.FromUnixTime(data.dew_point_hi_at);
										DoOutdoorDewpoint(ConvertUnits.TempFToUser((double) data.dew_point_hi), ts);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Dew Point (high) data on TxId {data.tx_id}");
									}

									// do low DP
									if (data.dew_point_lo_at != 0 && data.dew_point_lo != null)
									{
										ts = Utils.FromUnixTime(data.dew_point_lo_at);
										DoOutdoorDewpoint(ConvertUnits.TempFToUser((double) data.dew_point_lo), ts);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Dew Point (low) data on TxId {data.tx_id}");
									}

									// do last DP
									if (data.dew_point_last != null)
									{
										DoOutdoorDewpoint(ConvertUnits.TempFToUser((double) data.dew_point_last), lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Dew Point data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing dew point value on TxId {data.tx_id}. Error: {ex.Message}");
								}

								if (!cumulus.StationOptions.CalculatedWC)
								{
									// use wind chill from WLL - otherwise we calculate it at the end of processing the historic record when we have all the data
									try
									{
										// do low WC
										if (data.wind_chill_lo_at != 0 && data.wind_chill_lo != null)
										{
											ts = Utils.FromUnixTime(data.wind_chill_lo_at);
											DoWindChill(ConvertUnits.TempFToUser((double) data.wind_chill_lo), ts);
										}
										else
										{
											cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Wind Chill (low) data on TxId {data.tx_id}");
										}

										// do last WC
										if (data.wind_chill_last != null)
										{
											DoWindChill(ConvertUnits.TempFToUser((double) data.wind_chill_last), lastRecordTime);
										}
										else
										{
											cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Wind Chill data on TxId {data.tx_id}");
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Error processing wind chill value on TxId {data.tx_id}. Error: {ex.Message}");
									}
								}
							}
							else
							{   // Check for Extra temperature/humidity settings
								for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
								{
									if (cumulus.WllExtraTempTx[tempTxId] != data.tx_id) continue;

									try
									{
										if (data.temp_last == null || data.temp_last < -98)
										{
											cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid Extra temperature value on TxId {data.tx_id}");
										}
										else
										{
											cumulus.LogDebugMessage($"DecodeHistoric: using extra temp data from TxId {data.tx_id}");

											DoExtraTemp(ConvertUnits.TempFToUser((double) data.temp_last), tempTxId);
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Error processing extra temp value on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"DecodeHistoric: Exception {ex.Message}");
									}

									try
									{
										if (data.hum_last != null)
										{
											DoExtraHum((double) data.hum_last, tempTxId);
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Error processing extra humidity value on TxId {data.tx_id}. Error: {ex.Message}");
									}
								}
							}

							// Wind
							if (cumulus.WllPrimaryWind == data.tx_id)
							{
								/*
								 * Available fields
								 * "wind_dir_of_prevail"
								 * "wind_run"
								 * "wind_speed_avg"
								 * "wind_speed_hi_at"
								 * "wind_speed_hi_dir"
								 * "wind_speed_hi"
								*/

								try
								{
									if (data.wind_speed_hi != null && data.wind_speed_hi_dir != null && data.wind_speed_avg != null)
									{
										var gust = ConvertUnits.WindMPHToUser((double) data.wind_speed_hi);
										var spd = ConvertUnits.WindMPHToUser((double) data.wind_speed_avg);
										var dir = data.wind_speed_hi_dir ?? 0;
										cumulus.LogDebugMessage($"DecodeHistoric: using wind data from TxId {data.tx_id}");
										DoWind(gust, dir, spd, lastRecordTime);
										AddValuesToRecentWind(spd, spd, dir, lastRecordTime.AddSeconds(-data.arch_int), lastRecordTime);
									}
									else
									{
										cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid Wind data on TxId {data.tx_id}");
									}

									if (data.wind_speed_avg != null)
									{
										WindAverage = cumulus.Calib.WindSpeed.Calibrate(ConvertUnits.WindMPHToUser((double) data.wind_speed_avg));

										if (!current)
										{
											// add in 'archivePeriod' minutes worth of wind speed to windrun
											int interval = data.arch_int / 60;
											WindRunToday += ((WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval) / 60.0);
										}
									}
									else
									{
										cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid Wind data (avg) on TxId {data.tx_id}");
									}

									if (current)
									{
										// we do not have the latest value, so set a pseudo value between average and gust
										WindLatest = Program.RandGenerator.Next((int) WindAverage, (int) RecentMaxGust);
									}

								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing wind values on TxId {data.tx_id}. Error: {ex.Message}");
								}
							}

							// Rainfall
							if (cumulus.WllPrimaryRain == data.tx_id)
							{
								/*
								 * Available fields:
								 * "rain_rate_hi_at"
								 * "rain_rate_hi_clicks"
								 * "rain_rate_hi_in"
								 * "rain_rate_hi_mm"
								 * "rain_size"
								 * "rainfall_clicks"
								 * "rainfall_in"
								 * "rainfall_mm"
								 */


								// The WL API v2 does not provide any running totals for rainfall, only  :(
								// So we will have to add the interval data to the running total and hope it all works out!

								try
								{
									if (data.rain_rate_hi_at != 0 && data.rainfall_clicks != null && data.rain_rate_hi_clicks != null)
									{
										cumulus.LogDebugMessage($"DecodeHistoric: using rain data from TxId {data.tx_id}");

										var rain = ConvertRainClicksToUser((double) data.rainfall_clicks, data.rain_size);
										var rainrate = ConvertRainClicksToUser((double) data.rain_rate_hi_clicks, data.rain_size);
										if (rain > 0)
										{
											cumulus.LogDebugMessage($"DecodeHistoric: Adding rain {rain.ToString(cumulus.RainFormat)}");
										}
										rain += RainCounter;

										if (rainrate < 0)
										{
											rainrate = 0;
										}

										DoRain(rain, rainrate, lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Rain data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing rain data on TxId {data.tx_id}. Error:{ex.Message}");
								}

							}

							// UV
							if (cumulus.WllPrimaryUV == data.tx_id)
							{
								/*
								 * Available fields
								 * "uv_dose"
								 * "uv_index_avg"
								 * "uv_index_hi_at"
								 * "uv_index_hi"
								 * "uv_volt_last"
								 */
								try
								{
									if (data.uv_index_avg != null)
									{
										cumulus.LogDebugMessage($"DecodeHistoric: using UV data from TxId {data.tx_id}");

										DoUV((double) data.uv_index_avg, lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid UV data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing UV value on TxId {data.tx_id}. Error: {ex.Message}");
								}

							}

							// Solar
							if (cumulus.WllPrimarySolar == data.tx_id)
							{
								/*
								 * Available fields
								 * "solar_energy"
								 * "solar_rad_avg"
								 * "solar_rad_hi_at"
								 * "solar_rad_hi"
								 * "solar_rad_volt_last"
								 * "solar_volt_last"
								 * "et" (inches) - ET field is populated in the ISS archive records, which may not be the same as the solar
								 */
								try
								{
									if (data.solar_rad_avg != null)
									{
										cumulus.LogDebugMessage($"DecodeHistoric: using solar data from TxId {data.tx_id}");
										DoSolarRad((int) data.solar_rad_avg, lastRecordTime);

										if (!current && IsSunny)
										{
											// add in archive period worth of sunshine, if sunny - arch_int in seconds
											SunshineHours += (data.arch_int / 3600.0);
										}
									}
									else
									{
										cumulus.LogWarningMessage($"DecodeHistoric: Warning, no valid Solar data on TxId {data.tx_id}");
									}

									if (data.et != null && !cumulus.StationOptions.CalculatedET)
									{
										// wl.com ET is only available in record the start of each hour.
										// The number is the total for the one hour period.
										// This is unlike the existing VP2 when the ET is an annual running total
										// So we try and mimic the VP behaviour
										var newET = AnnualETTotal + ConvertUnits.RainINToUser((double) data.et);
										cumulus.LogDebugMessage($"DecodeHistoric: Adding {ConvertUnits.RainINToUser((double) data.et):F3} to ET");
										DoET(newET, lastRecordTime);
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing Solar value on TxId {data.tx_id}. Error: {ex.Message}");
								}
							}
						}
						break;

					case 13: // WeatherLink Live Non-ISS data
					case 20: // WL console Baro
					case 22: // WL console internal T/H
					case 26: // WL console Soil/Leaf data
						switch (sensorType)
						{
							case 56: // Soil + Leaf
								var data = json.FromJsv<WlHistorySensorDataType13>();

								string idx = string.Empty;
								/*
								 * Leaf Wetness
								 * Available fields
								 * "wet_leaf_at_1"
								 * "wet_leaf_hi_1"
								 * "wet_leaf_hi_2":
								 * "wet_leaf_hi_at_1"
								 * "wet_leaf_hi_at_2"
								 * "wet_leaf_last_1"
								 * "wet_leaf_last_2"
								 * "wet_leaf_last_volt_1"
								 * "wet_leaf_last_volt_2"
								 * "wet_leaf_lo_1"
								 * "wet_leaf_lo_2"
								 * "wet_leaf_lo_at_2"
								 * "wet_leaf_min_1"
								 * "wet_leaf_min_2"
								 */

								cumulus.LogDebugMessage($"DecodeHistoric: found Leaf/Soil data on TxId {data.tx_id}");

								// We are relying on user configuration here, trap any errors
								for (var i = 1; i <= numLeafWetnessSensors; i++)
								{
									try
									{
										if (cumulus.WllLeafWetTx[i] == data.tx_id || isVp2Station)
										{
											idx = "wet_leaf_last_" + cumulus.WllLeafWetIdx[i];
											if (data[idx] != null)
											{
												DoLeafWetness((double) data[idx], i);
											}
										}
									}
									catch (Exception e)
									{
										cumulus.LogErrorMessage($"Error, DecodeHistoric, LeafWetness txid={data.tx_id}, idx={idx}: {e.Message}");
									}
								}
								/*
								 * Soil Moisture
								 * Available fields
								 * "moist_soil_hi_1"
								 * "moist_soil_hi_2"
								 * "moist_soil_hi_3"
								 * "moist_soil_hi_4"
								 * "moist_soil_hi_at_1"
								 * "moist_soil_hi_at_2"
								 * "moist_soil_hi_at_3"
								 * "moist_soil_hi_at_4"
								 * "moist_soil_last_1"
								 * "moist_soil_last_2"
								 * "moist_soil_last_3"
								 * "moist_soil_last_4"
								 * "moist_soil_last_volt_1"
								 * "moist_soil_last_volt_2"
								 * "moist_soil_last_volt_3"
								 * "moist_soil_last_volt_4"
								 * "moist_soil_lo_1"
								 * "moist_soil_lo_2"
								 * "moist_soil_lo_3"
								 * "moist_soil_lo_4"
								 * "moist_soil_lo_at_1"
								 * "moist_soil_lo_at_2"
								 * "moist_soil_lo_at_3"
								 * "moist_soil_lo_at_4"
								 */

								for (var i = 1; i <= numSoilMoistureSensors; i++)
								{
									try
									{
										if (cumulus.WllSoilMoistureTx[i] == data.tx_id || isVp2Station)
										{
											idx = "moist_soil_last_" + cumulus.WllSoilMoistureIdx[i];

											if (data[idx] == null)
											{
												cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid soil moisture #{cumulus.WllSoilMoistureIdx[i]} on TxId {data.tx_id}");
											}
											else
											{
												DoSoilMoisture((double) data[idx], i);
											}
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"DecodeHistoric: Error processing soil moisture #{cumulus.WllSoilMoistureIdx[i]} on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"DecodeHistoric: Exception: {ex.Message}");
									}
								}

								/*
								 * Soil Temperature
								 * Available fields
								 * "temp_hi_1"
								 * "temp_hi_2"
								 * "temp_hi_3"
								 * "temp_hi_4"
								 * "temp_hi_at_1"
								 * "temp_hi_at_2"
								 * "temp_hi_at_3"
								 * "temp_hi_at_4"
								 * "temp_last_1"
								 * "temp_last_2"
								 * "temp_last_3"
								 * "temp_last_4"
								 * "temp_last_volt_1"
								 * "temp_last_volt_2"
								 * "temp_last_volt_3"
								 * "temp_last_volt_4"
								 * "temp_lo_1"
								 * "temp_lo_2"
								 * "temp_lo_3"
								 * "temp_lo_4"
								 * "temp_lo_at_1"
								 * "temp_lo_at_2"
								 * "temp_lo_at_3"
								 * "temp_lo_at_4"
								 */

								for (var i = 1; i <= numSoiltempSensors; i++)
								{
									try
									{
										// allocated to soil temp?
										if (cumulus.WllSoilTempTx[i] == data.tx_id || isVp2Station)
										{
											idx = "temp_last_" + cumulus.WllSoilTempIdx[i];
											if (data[idx] == null)
											{
												cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid extra soil temp #{cumulus.WllSoilTempIdx[i]} on TxId {data.tx_id}");
											}
											else
											{
												DoSoilTemp(ConvertUnits.TempFToUser((double) data[idx]), i);
											}
										}

										// alocated to extra temp?
										else if (cumulus.WllExtraTempTx[i] == data.tx_id)
										{
											idx = "temp_last_" + cumulus.WllExtraTempIdx[i];
											if (data[idx] == null)
											{
												cumulus.LogDebugMessage($"DecodeHistoric: Warning, no valid extra soil temp #{cumulus.WllSoilTempIdx[i]} on TxId {data.tx_id}");
											}
											else
											{
												DoExtraTemp(ConvertUnits.TempFToUser((double) data[idx]), i);
											}
										}

									}
									catch (Exception e)
									{
										cumulus.LogErrorMessage($"Error, DecodeHistoric, SoilTemp txid={data.tx_id}, idx={idx}: {e.Message}");
									}
								}

								break;

							case 242: // Baro
								/*
								 * Available fields
								 * "bar_absolute"
								 * "bar_hi_at"
								 * "bar_sea_level"
								 * "arch_int"
								 * "bar_lo"
								 * "bar_hi"
								 * "bar_lo_at"
								 */
								// log the current value
								cumulus.LogDebugMessage("DecodeHistoric: found Baro data");
								try
								{
									var data13baro = json.FromJsv<WlHistorySensorDataType13Baro>();
									DateTime ts;

									// Only check hi/lo if we are using the Davis SLP
									if (!cumulus.StationOptions.CalculateSLP)
									{
										// check the high
										if (data13baro.bar_hi_at != 0 && data13baro.bar_hi != null)
										{
											ts = Utils.FromUnixTime(data13baro.bar_hi_at);
											DoPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_hi), ts);
										}
										else
										{
											cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Baro data (high)");
										}
										// check the low
										if (data13baro.bar_lo_at != 0 && data13baro.bar_lo != null)
										{
											ts = Utils.FromUnixTime(data13baro.bar_lo_at);
											DoPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_lo), ts);
										}
										else
										{
											cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Baro data (high)");
										}

										if (data13baro.bar_sea_level != null)
										{
											// leave it at current value
											ts = Utils.FromUnixTime(data13baro.ts);
											DoPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_sea_level), ts);
										}
										else
										{
											cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Baro data (sea level)");
										}
									}
									else
									{
										cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Baro data (absolute)");
									}

									// Altimeter from absolute
									if (data13baro.bar_absolute != null)
									{
										// leave possible calculation of SLP until later when we have temp and humidity
										DoStationPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_absolute));
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing baro reading. Error: {ex.Message}");
								}
								break;

							case 243: // WeatherLink Live Inside temp/hum
							case 365: // WL console Inside temp/hum
								/*
								 * Available fields
								 * "dew_point_in"
								 * "heat_index_in"
								 * "hum_in_hi"
								 * "hum_in_hi_at"
								 * "hum_in_last"
								 * "hum_in_lo"
								 * "hum_in_lo_at"
								 * "temp_in_hi"
								 * "temp_in_hi_at"
								 * "temp_in_last"
								 * "temp_in_lo"
								 * "temp_in_lo_at"
								 */
								cumulus.LogDebugMessage("DecodeHistoric: found inside temp/hum data");

								var data13temp = json.FromJsv<WlHistorySensorDataType13Temp>();
								try
								{
									if (data13temp.temp_in_last != null)
									{
										DoIndoorTemp(ConvertUnits.TempFToUser((double) data13temp.temp_in_last));
									}
									else
									{
										cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Inside Temperature");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"DecodeHistoric: Error processing temp-in reading. Error: {ex.Message}]");
								}


								try
								{
									if (data13temp.hum_in_last != null)
									{
										DoIndoorHumidity(Convert.ToInt32(data13temp.hum_in_last));
									}
									else
									{
										cumulus.LogWarningMessage("DecodeHistoric: Warning, no valid Inside Humidity");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogDebugMessage($"WLL current: Error processing humidity-in. Error: {ex.Message}]");
								}

								break;

							default:
								cumulus.LogDebugMessage($"DecodeHistoric: Not processing sensor type [{sensorType}]!");
								break;

						}
						break;

					case 15: // WLL Health
						{
							var data = json.FromJsv<WlHealthDataType15>();
							DecodeWllHealth(data, false);
						}
						break;

					case 17: // AirLink
						cumulus.LogDebugMessage("TODO: Decode AirLink");
						break;

					case 18: // AirLink Health
						cumulus.LogDebugMessage("TODO: Decode AirLink Health");
						break;

					case 27: // WLC Health
						{
							var data = json.FromJsv<WlHealthDataType27>();
							DecodeWlcHealth(data, false);
						}
						break;

					default:
						cumulus.LogDebugMessage($"DecodeHistoric: found an unknown data structure type [{dataType}]!");
						break;
				}
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"Error, DecodeHistoric, DataType={dataType}, SensorType={sensorType}: " + e.Message);
			}
		}

		private void DecodeWllHealth(WlHealthDataType15 data, bool startingup)
		{
			/* WLL Device
				*
				* Available fields
				* "battery_voltage"
				* "bgn"					- historic only
				* "bluetooth_version"		- historic only
				* "bootloader_version"
				* "dns_type_used"			- historic only
				* "espressif_version"
				* "firmware_version"
				* "health_version"
				* "input_voltage"
				* "ip_address_type"
				* "ip_v4_address"
				* "ip_v4_gateway"
				* "ip_v4_netmask"
				* "link_uptime"
				* "local_api_queries"
				* "network_error"
				* "network_type":
				* "radio_version"
				* "rapid_records_sent"
				* "rx_bytes"
				* "touchpad_wakeups"
				* "tx_bytes"
				* "uptime"
				* "wifi_rssi"
				* "ts"						- historic only
				*/

			try
			{
				var dat = Utils.FromUnixTime(data.firmware_version);
				DavisFirmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");

				var battV = data.battery_voltage / 1000.0;
				ConBatText = battV.ToString("F2");
				// Allow voltage to drop to 1.35V per cell before triggering the alarm. This should leave a good reserve without changing them too often
				// 1.35 * 4 = 5.4
				if (battV < 5.4)
				{
					cumulus.LogWarningMessage($"WARNING: WLL Backup battery voltage is low = {battV:0.##}V");
				}
				else
				{
					cumulus.LogDebugMessage($"WLL Battery Voltage = {battV:0.##}V");
				}
				var inpV = data.input_voltage / 1000.0;
				ConSupplyVoltageText = inpV.ToString("F2");
				if (inpV < 4.0)
				{
					cumulus.LogWarningMessage($"WARNING: WLL Input voltage is low = {inpV:0.##}V");
				}
				else
				{
					cumulus.LogDebugMessage($"WLL Input Voltage = {inpV:0.##}V");
				}
				var upt = TimeSpan.FromSeconds(data.uptime);
				var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
						(int) upt.TotalDays,
						upt.Hours,
						upt.Minutes,
						upt.Seconds);
				cumulus.LogDebugMessage("WLL/WLC Uptime = " + uptStr);
				StationUptime = upt;

				// Only present if WiFi attached
				if (data.wifi_rssi.HasValue)
				{
					DavisTxRssi[0] = data.wifi_rssi.Value;
					cumulus.LogDebugMessage("WLL WiFi RSSI = " + DavisTxRssi[0] + "dB");
				}

				upt = TimeSpan.FromSeconds(data.link_uptime);
				uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
						(int) upt.TotalDays,
						upt.Hours,
						upt.Minutes,
						upt.Seconds);
				cumulus.LogDebugMessage("WLL Link Uptime = " + uptStr);
				StationLinkUptime = upt;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"DecodeWllHealth: Error processing health. Error: {ex.Message}");
				DavisFirmwareVersion = "???";
			}

			if (startingup)
			{
				cumulus.LogMessage("WLL FW version = " + DavisFirmwareVersion);
			}
			else
			{
				cumulus.LogDebugMessage("WLL FW version = " + DavisFirmwareVersion);
			}
		}

		private void DecodeWlcHealth(WlHealthDataType27 data, bool startingup)
		{
			/* WLL Device
				*
				* Available fields
				* health_version
				* console_sw_version
				* console_os_version
				* console_radio_version
				* console_api_level
				*
				* battery_voltage
				* battery_percent
				* battery_condition  - 1:unknown, 2:good, 3:overheat, 4:dead, 5:overvoltage, 6:unspecified failure, 7:cold
				* battery_current - mA
				* battery_temp - Celcius
				* charger_plugged - 0:unplugged, 1:plugged in
				* battery_status - 1:unknown, 2:charging, 3:discharging, 4:not charging, 5:full
				* battery_cycle_count
				*
				* os_uptime
				* app_uptime
				*
				* bgn					- historic only?
				*
				* bootloader_version
				* clock_source
				* gnss_sip_tx_id
				*
				* free_mem
				* internal_free_space
				* system_free_space
				*
				* queue_kilobytes
				* database_kilobytes
				*
				* ip_v4_address
				* ip_v4_gateway
				* ip_v4_netmask
				* dns_type_used - 1:Primary, 2:secondary, 3:public
				* rx_kilobytes
				* tx_kilobytes
				* local_api_queries
				* wifi_rssi
				* link_uptime
				* connection_uptime
				* "ts"						- historic only ?
				*/

			try
			{
				DavisFirmwareVersion = data.console_sw_version;

				StationFreeMemory = (int) ((data.free_mem ?? 0.0) * 1024);

				StationRuntime = data.app_uptime ?? 0;

				var condition = data.battery_condition.Value switch
				{
					1 => "unknown",
					2 => "good",
					3 => "overheat",
					4 => "dead",
					5 => "over voltage",
					6 => "unspecified failure",
					7 => "cold",
					_ => data.battery_condition.Value.ToString(),
				};
				var status = data.battery_status switch
				{
					1 => "unknown",
					2 => "charging",
					3 => "discharging",
					4 => "not charging",
					5 => "full",
					_ => data.battery_status.Value.ToString(),
				};
				cumulus.LogDebugMessage($"WLC: Battery: Percent={data.battery_percent}, voltage={data.battery_voltage.Value / 1000.0:F2}V, current={data.battery_current} mA, temp={data.battery_temp}°C");
				cumulus.LogDebugMessage($"WLC: Battery: Condition={condition}, charger={(data.charger_plugged == 0 ? "unplugged" : "plugged-in")}, status={status}");

				if (data.battery_condition.Value != 2)
				{
					cumulus.LogWarningMessage("WARNING: WLC battery condition = " + condition);
				}

				var upt = TimeSpan.FromSeconds(data.app_uptime.Value);
				var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
						(int) upt.TotalDays,
						upt.Hours,
						upt.Minutes,
						upt.Seconds);
				cumulus.LogDebugMessage("WLC App Uptime = " + uptStr);

				StationUptime = upt;

				upt = TimeSpan.FromSeconds(data.os_uptime.Value);
				uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
						(int) upt.TotalDays,
						upt.Hours,
						upt.Minutes,
						upt.Seconds);
				cumulus.LogDebugMessage("WLC OS Uptime = " + uptStr);

				// Only present if WiFi attached
				if (data.wifi_rssi.HasValue)
				{
					DavisTxRssi[0] = data.wifi_rssi.Value;
					cumulus.LogDebugMessage("WLC WiFi RSSI = " + DavisTxRssi[0] + "dB");
				}

				upt = TimeSpan.FromSeconds(data.link_uptime.Value);
				uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
						(int) upt.TotalDays,
						upt.Hours,
						upt.Minutes,
						upt.Seconds);
				cumulus.LogDebugMessage("Link Uptime = " + uptStr);

				StationLinkUptime = upt;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"DecodeWlcHealth: Error processing health. Error: {ex.Message}");
				DavisFirmwareVersion = "???";
			}

			if (startingup)
			{
				cumulus.LogMessage("WLC FW version = " + DavisFirmwareVersion);
			}
			else
			{
				cumulus.LogDebugMessage("WLC FW version = " + DavisFirmwareVersion);
			}
		}

		// Finds all stations associated with this API
		// Return true if only 1 result is found, else return false
		private void GetAvailableStationIds(bool logToConsole = false)
		{
			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("GetStations: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			var stationsUrl = "https://api.weatherlink.com/v2/stations?api-key=" + cumulus.WllApiKey;

			cumulus.LogDebugMessage($"GetStations: URL = {stationsUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");
			cumulus.LogDebugMessage($"GetStations: Looking for station id = {cumulus.WllStationId}");

			try
			{
				string responseBody;
				int responseCode;

				var request = new HttpRequestMessage(HttpMethod.Get, stationsUrl.ToString());
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// We want to do this synchronously
				using (var response = cumulus.MyHttpClient.SendAsync(request).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					var resp = UserEmailRegEx().Replace(responseBody, "user_email\":\"<<email>>\"");
					cumulus.LogDebugMessage($"GetStations: WeatherLink API Response: {responseCode}: {resp}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"GetStations: WeatherLink API Error: {errObj.code} - {errObj.message}");
					return;
				}

				var stationsObj = responseBody.FromJson<WlStationList>();

				foreach (var station in stationsObj.stations)
				{
					cumulus.LogMessage($"GetStations: WeatherLink station id = {station.station_id}, name = {station.station_name}, subscription = {station.subscription_type}");
					if (stationsObj.stations.Count > 1 && logToConsole)
					{
						Cumulus.LogConsoleMessage($" - Found WeatherLink station id = {station.station_id}, name = {station.station_name}, active = {station.active}, subscription = {station.subscription_type}");
					}
					if (station.station_id == cumulus.WllStationId || station.station_id_uuid == cumulus.WllStationUuid)
					{
						cumulus.LogDebugMessage($"GetStations: Setting WLL parent ID = {station.gateway_id}");
						cumulus.WllParentId = station.gateway_id;

						if (station.recording_interval != Cumulus.logints[cumulus.DataLogInterval])
						{
							cumulus.LogWarningMessage($"GetStations: - Cumulus log interval {Cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station.recording_interval}");
						}

						wlStationArchiveInterval = station.recording_interval;
						SetDataTimeout(station.subscription_type);

						if (cumulus.WllStationId < 10)
						{
							cumulus.WllStationId = station.station_id;
						}
						else if (cumulus.WllStationUuid == string.Empty)
						{
							cumulus.WllStationUuid = station.station_id_uuid;
						}

						cumulus.WriteIniFile();
					}
				}
				if (stationsObj.stations.Count > 1 && (cumulus.WllStationId < 10 && cumulus.WllStationUuid == string.Empty))
				{
					if (logToConsole)
						Cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your WLL configuration to enable history downloads.");
				}
				else if (stationsObj.stations.Count == 1 && (cumulus.WllStationId != stationsObj.stations[0].station_id || cumulus.WllStationUuid != stationsObj.stations[0].station_id_uuid))
				{
					var usedId = cumulus.WllStationId < 10 ? cumulus.WllStationId.ToString() : cumulus.WllStationUuid;

					cumulus.LogMessage($"GetStations: Only found 1 WeatherLink station, using id = {usedId}");
					cumulus.WllStationId = stationsObj.stations[0].station_id;
					cumulus.WllStationUuid = stationsObj.stations[0].station_id_uuid;
					// And save it to the config file
					cumulus.WriteIniFile();

					cumulus.LogDebugMessage($"GetStations: Setting WLL parent ID = {stationsObj.stations[0].gateway_id}");
					cumulus.WllParentId = stationsObj.stations[0].gateway_id;
					wlStationArchiveInterval = stationsObj.stations[0].recording_interval;
					SetDataTimeout(stationsObj.stations[0].subscription_type);
					return;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetStations: WeatherLink API exception: " + ex.Message);
			}
		}

		private void SetDataTimeout(string subscription)
		{
			subscriptionLevel = (subscription ?? "basic").ToLower();

			DataTimeoutMins = subscriptionLevel switch
			{
				"basic" => 15 + 3,
				"pro" => 5 + 3,
				"pro+" => 1 + 3,
				_ => 15 + 3,
			};

			cumulus.LogMessage($"GetStations: Subscription type = {subscriptionLevel}, data timeout = {DataTimeoutMins} minutes");
		}

		private void GetAvailableSensors()
		{
			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("GetAvailableSensors: WeatherLink API data is missing in the configuration, aborting!");
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				cumulus.LogMessage("GetAvailableSensors: No WeatherLink API station ID has been configured, aborting!");
				return;
			}

			var stationsUrl = "https://api.weatherlink.com/v2/sensors?api-key=" + cumulus.WllApiKey;

			cumulus.LogDebugMessage($"GetAvailableSensors: URL = {stationsUrl.Replace(cumulus.WllApiKey, "API_KEY")}");

			WlSensorList sensorsObj;

			try
			{
				string responseBody;
				int responseCode;
				var request = new HttpRequestMessage(HttpMethod.Get, stationsUrl);
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// We want to do this synchronously
				using (var response = cumulus.MyHttpClient.SendAsync(request).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetAvailableSensors: WeatherLink API Response: {responseCode}: {responseBody}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"GetAvailableSensors: WeatherLink API Error: {errObj.code} - {errObj.message}");
					return;
				}

				sensorsObj = responseBody.FromJson<WlSensorList>();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetAvailableSensors: WeatherLink API exception: " + ex.Message);
				return;
			}

			sensorList = sensorsObj.sensors;

			foreach (var sensor in sensorList)
			{
				try
				{
					cumulus.LogDebugMessage($"GetAvailableSensors: Found WeatherLink Sensor type={sensor.sensor_type}, lsid={sensor.lsid}, station_id={sensor.station_id}, name={sensor.product_name}, parentId={sensor.parent_device_id}, parent={sensor.parent_device_name}");

					// we need a lookup of LSID to TX_ID for Soil/Leaf transmitters as they do not contain the tx_id in Current data
					if (sensor.station_id == cumulus.WllStationId)
					{
						if (sensor.tx_id.HasValue)
						{
							lsidToTx.Add(sensor.lsid, sensor.tx_id.Value);
						}

						lsidLastUpdate.Add(sensor.lsid, 0);
					}

					if (sensor.sensor_type == 323 && sensor.station_id == cumulus.AirLinkOutStationId)
					{
						cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Outdoor LSID to {sensor.lsid}");
						cumulus.airLinkOutLsid = sensor.lsid;
					}
					else if (sensor.sensor_type == 326 && sensor.station_id == cumulus.AirLinkInStationId)
					{
						cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Indoor LSID to {sensor.lsid}");
						cumulus.airLinkInLsid = sensor.lsid;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("GetAvailableSensors: Processing sensors exception: " + ex.Message);
				}
			}
		}

		private void GetSystemStatus()
		{
			WlComSystemStatus status;
			try
			{
				string responseBody;
				int responseCode;

				cumulus.LogDebugMessage("GetSystemStatus: Getting WeatherLink.com system status");

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.GetAsync("https://0886445102835570.hostedstatus.com/1.0/status/600712dea9c1290530967bc6").Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetSystemStatus: WeatherLink.com system status Response code: {responseCode}");
					cumulus.LogDataMessage($"GetSystemStatus: WeatherLink.com system status Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"GetSystemStatus: WeatherLink.com system status Error: {responseCode}");
					Cumulus.LogConsoleMessage($" - Error {responseCode}");
					return;
				}

				status = responseBody.FromJson<WlComSystemStatus>();

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("GetSystemStatus: WeatherLink.com system status: No data was returned.");
					return;
				}
				else if (status != null)
				{
					string msg;
					if (status.result.status_overall.status_code != 100)
					{
						msg = status.ToString(true);
						cumulus.LogWarningMessage(msg);
						Console.WriteLine(msg);
					}
					else
					{
						msg = status.ToString(false);
						cumulus.LogDebugMessage(msg);
					}
				}
				else
				{
					cumulus.LogWarningMessage("GetSystemStatus: Something went wrong!");
				}

			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetSystemStatus: Exception: " + ex);
			}
		}

		private void CheckLoggingDataStopped(int mins)
		{
			if (mins != wlStationArchiveInterval || DataTimeoutMins != mins + 3)
			{
				wlStationArchiveInterval = mins;
				DataTimeoutMins = wlStationArchiveInterval + 3;
			}
		}

		[System.Text.RegularExpressions.GeneratedRegex("user_email\":\"[^\"]*\"")]
		private static partial System.Text.RegularExpressions.Regex UserEmailRegEx();
	}
}
