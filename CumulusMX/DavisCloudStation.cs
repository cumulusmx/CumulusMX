using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Timers;

using ServiceStack;

using static System.Collections.Specialized.BitVector32;

namespace CumulusMX
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
	internal class DavisCloudStation : WeatherStation
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
	{
		private readonly System.Timers.Timer tmrCurrent;
		private readonly System.Timers.Timer tmrHealth;
		//private bool savedUseSpeedForAvgCalc;
		private bool savedCalculatePeakGust;
		private int maxArchiveRuns = 1;
		private int weatherLinkArchiveInterval = 16 * 60; // Used to get historic Health, 16 minutes in seconds only for initial fetch after load
		private int wlStationArchiveInterval = 5;
		private bool wlLastArchiveFetchOk;
		private bool wllVoltageLow;
		private readonly AutoResetEvent bwDoneEvent = new AutoResetEvent(false);
		private List<WlSensorListSensor> sensorList;
		private bool startingUp = true;
		private new readonly Random random = new Random();
		private DateTime lastRecordTime = DateTime.MinValue;

		public DavisCloudStation(Cumulus cumulus) : base(cumulus)
		{
			calculaterainrate = false;
			//cumulus.UseDataLogger = false;
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

				if (datetimeStr.EndsWith("Z") &&
					DateTime.TryParseExact(datetimeStr, utcTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime resultUtc))
				{
					return resultUtc;
				}
				else if (!datetimeStr.EndsWith("Z") &&
					DateTime.TryParseExact(datetimeStr, localTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime resultLocal))
				{
					return resultLocal;
				}

				return DateTime.MinValue;
			};

			tmrCurrent = new System.Timers.Timer();
			tmrHealth = new System.Timers.Timer();

			// The Davis leafwetness sensors send a decimal value via WLL (only integer available via VP2/Vue)
			cumulus.LeafWetDPlaces = 1;
			cumulus.LeafWetFormat = "F1";

			CalcRecentMaxGust = false;
			cumulus.StationOptions.CalcuateAverageWindSpeed = false;
			cumulus.StationOptions.UseSpeedForAvgCalc = true;
			cumulus.StationOptions.CalculatedDP = false;
			cumulus.StationOptions.CalculatedWC = false;

			/*
			// If the user is using the default 10 minute Wind gust, always use gust data from the WLL - simple
			if (cumulus.StationOptions.PeakGustMinutes == 10)
			{
				CalcRecentMaxGust = true;
				checkWllGustValues = true;
			}
			else if (cumulus.StationOptions.PeakGustMinutes > 10)
			{
				// If the user period is greater that 10 minutes, then Cumulus must calculate Gust values
				// but we can check the WLL 10 min gust value in case we missed a gust
				CalcRecentMaxGust = true;
				checkWllGustValues = true;
			}
			else
			{
				// User period is less than 10 minutes
				CalcRecentMaxGust = true;
				checkWllGustValues = true;
			}
			*/


			// Sanity check - do we have all the info we need?
			if (string.IsNullOrEmpty(cumulus.WllApiKey) && string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// The basic API details have not been supplied
				cumulus.LogWarningMessage("WLL - No WeatherLink.com API configuration supplied, just going to work locally");
				cumulus.LogMessage("WLL - Cannot start historic downloads or retrieve health data");
				Cumulus.LogConsoleMessage("*** No WeatherLink.com API details supplied. Cannot start station", ConsoleColor.DarkCyan);
			}
			else if (string.IsNullOrEmpty(cumulus.WllApiKey) || string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// One of the API details is missing
				if (string.IsNullOrEmpty(cumulus.WllApiKey))
				{
					cumulus.LogWarningMessage("WLL - Missing WeatherLink.com API Key");
					Cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Key. Cannot start station", ConsoleColor.Yellow);
				}
				else
				{
					cumulus.LogWarningMessage("WLL - Missing WeatherLink.com API Secret");
					Cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Secret. Cannot start station", ConsoleColor.Yellow);
				}
			}

			// Get wl.com status
			GetSystemStatus();

			// Perform Station ID checks - If we have API details!
			// If the Station ID is missing, this will populate it if the user only has one station associated with the API key
			if (cumulus.WllStationId < 10)
			{
				var msg = "No WeatherLink API station ID in the cumulus.ini file";
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
				cumulus.LogErrorMessage("WLL - The WeatherLink.com API is enabled, but no Station Id has been configured, not starting the station. Please correct this and restart Cumulus");
				Cumulus.LogConsoleMessage("The WeatherLink.com API is enabled, but no Station Id has been configured. Please correct this and restart Cumulus", ConsoleColor.Yellow);
				return;
			}


			// Now get the sensors associated with this station
			GetAvailableSensors();

			DateTime tooOld = new DateTime(0);

			if ((cumulus.LastUpdateTime <= tooOld) || !cumulus.UseDataLogger)
			{
				// there's nothing in the database, so we haven't got a rain counter
				// we can't load the history data, so we'll just have to go live

				timerStartNeeded = true;
				LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
				//StartLoop();
				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);

				cumulus.LogMessage("Starting Davis WLL");
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
				//cumulus.LogDebugMessage("Lock: Station waiting for lock");
				Cumulus.syncInit.Wait();
				//cumulus.LogDebugMessage("Lock: Station has the lock");

				// Create a current conditions thread to poll readings every 10 seconds as temperature updates every 10 seconds
				//GetWlCurrent(null, null);
				//tmrCurrent.Elapsed += GetWlCurrent;
				GetWlLastArchive(null, null);
				tmrCurrent.Elapsed += GetWlLastArchive;
				tmrCurrent.Interval = 30 * 1000;  // Every 30 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();

				// Get the archive data health to do the initial value populations
				GetWlHistoricHealth();
				// And reset the fetch interval to 2 minutes
				weatherLinkArchiveInterval = 2 * 60;

			}
			catch (ThreadAbortException)
			{
			}
			finally
			{
				//cumulus.LogDebugMessage("Lock: Station releasing lock");
				Cumulus.syncInit.Release();
			}
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing WLL connections");
			try
			{
				if (tmrCurrent != null)
					tmrCurrent.Stop();
				if (tmrHealth != null)
					tmrHealth.Stop();
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
					bw.CancelAsync();
				}

				bwDoneEvent.WaitOne();
			}
			catch
			{
				cumulus.LogMessage("Error stopping station background tasks");
			}
		}

		/*
		private async void GetWlCurrent(object source, ElapsedEventArgs e)
		{
			// We only want to process at every archive interval, or on intial load, or if the last fetch was unsucessful
			// Initial run, source will be null
			// Possible intervals are 1, 5, or 30 minutes, fetch one minute after the archive time to allow for clock differences etc
			var now = DateTime.Now;

			if ((source != null && wlLastArchiveFetchOk && ((now.Minute - 1) % wlStationArchiveInterval != 0)) || now.Second > 35)
			{
				cumulus.LogDebugMessage("GetWlCurrent: Skipping");
				return;
			}

			wlLastArchiveFetchOk = false;

			//cumulus.LogMessage("GetWlCurrent: Get WL.com Current Data");
			cumulus.LogMessage("GetWlCurrent: Get WL.com last archive Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("GetWlCurrent: Missing WeatherLink API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				const string msg = "No WeatherLink API station ID in the configuration";
				cumulus.LogWarningMessage(msg);
				Cumulus.LogConsoleMessage("GetWlCurrent: " + msg);

			}

			//cumulus.LogMessage($"GetWlCurrent: Downloading Current Data from weatherlink.com");
			cumulus.LogMessage($"GetWlCurrent: Downloading last archive Data from weatherlink.com");

			StringBuilder currentUrl = new StringBuilder("https://api.weatherlink.com/v2/current/" + cumulus.WllStationId);
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
				using (var response = await Cumulus.MyHttpClient.SendAsync(request))
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetWlCurrent: WeatherLink API Current Response code: {responseCode}");
					cumulus.LogDataMessage($"GetWlCurrent: WeatherLink API Current Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var error = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"GetWlCurrent: WeatherLink API Current Error: {error.code}, {error.message}");
					Cumulus.LogConsoleMessage($" - Error {error.code}: {error.message}", ConsoleColor.Red);
					return;
				}

				currObj = responseBody.FromJson<WlCurrent>();

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("GetWlCurrent: WeatherLink API Current: No data was returned. Check your Device Id.");
					return;
				}
				else if (responseBody.StartsWith("{\"")) // basic sanity check
				{
					if (currObj.sensors.Count == 0)
					{
						cumulus.LogMessage("GetWlCurrent: No current data available");
						return;
					}
					else
					{
						cumulus.LogMessage($"GetWlCurrent: Found {currObj.sensors.Count} sensors to process");

						DecodeCurrent(currObj.sensors);

						if (startingUp)
							startingUp = false;
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("GetWlCurrent: Invalid current response received");
					cumulus.LogMessage("Response body = " + responseBody.ToString());
					return;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetWlCurrent:  Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					cumulus.LogMessage($"GetWlCurrent: Base exception - {ex.Message}");
				}
			}

			wlLastArchiveFetchOk = true;

			return;
		}
		*/

		private async void GetWlLastArchive(object source, ElapsedEventArgs e)
		{
			// We only want to process at every archive interval, or on intial load, or if the last fetch was unsucessful
			// Initial run, source will be null
			// Possible intervals are 1, 5, or 30 minutes, fetch one minute after the archive time to allow for clock differences etc
			var now = DateTime.Now;

			if ((source != null && wlLastArchiveFetchOk && ((now.Minute - 1) % wlStationArchiveInterval != 0)) || now.Second > 35)
			{
				cumulus.LogDebugMessage("GetWlLastArchive: Skipping");
				return;
			}

			wlLastArchiveFetchOk = false;

			cumulus.LogMessage("GetWlLastArchive: Get WL.com last archive Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("GetWlLastArchive: Missing WeatherLink API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				const string msg = "No WeatherLink API station ID in the configuration";
				cumulus.LogWarningMessage(msg);
				Cumulus.LogConsoleMessage("GetWlLastArchive: " + msg);

			}

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - weatherLinkArchiveInterval;
			long endTime = unixDateTime;

			cumulus.LogDebugMessage($"GetWlLastArchive: Downloading the historic record from WL.com from: {Utils.FromUnixTime(startTime):s} to: {Utils.FromUnixTime(endTime):s}");

			StringBuilder historicUrl = new StringBuilder("https://api.weatherlink.com/v2/historic/" + cumulus.WllStationId);
			historicUrl.Append("?api-key=" + cumulus.WllApiKey);
			historicUrl.Append("&start-timestamp=" + startTime.ToString());
			historicUrl.Append("&end-timestamp=" + endTime.ToString());

			cumulus.LogDebugMessage($"WeatherLink URL = {historicUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");

			WlHistory histObj;

			try
			{
				string responseBody;
				int responseCode;

				var request = new HttpRequestMessage(HttpMethod.Get, historicUrl.ToString());
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// we want to do this synchronously, so .Result
				using (var response = await Cumulus.MyHttpClient.SendAsync(request))
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetWlLastArchive: WeatherLink API Historic Response code: {responseCode}");
					cumulus.LogDataMessage($"GetWlLastArchive: WeatherLink API Historic Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var error = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogWarningMessage($"GetWlLastArchive: WeatherLink API Historic Error: {error.code}, {error.message}");
					Cumulus.LogConsoleMessage($" - Error {error.code}: {error.message}", ConsoleColor.Red);
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("GetWlLastArchive: WeatherLink API Historic: No data was returned. Check your Device Id.");
					return;
				}
				else if (responseBody.StartsWith("{\"")) // basic sanity check
				{
					histObj = responseBody.FromJson<WlHistory>();

					if (histObj.sensors.Count == 0)
					{
						cumulus.LogMessage("GetWlLastArchive: No historic data available");
						return;
					}
					else
					{
						cumulus.LogMessage($"GetWlLastArchive: Found {histObj.sensors.Count} sensors to process");

						foreach (var sensor in histObj.sensors)
						{
							int sensorType = sensor.sensor_type;
							int dataStructureType = sensor.data_structure_type;

							if (sensor.data.Count > 0)
							{
								DecodeHistoric(dataStructureType, sensorType, sensor.data[sensor.data.Count - 1], true);
							}
						}

						if (lastRecordTime != DateTime.MinValue)
						{
							// Now we have the primary data, calculate the derived data
							if (cumulus.StationOptions.CalculatedWC)
							{
								// DoWindChill does all the required checks and conversions
								DoWindChill(OutdoorTemperature, lastRecordTime);
							}

							DoApparentTemp(lastRecordTime);
							DoFeelsLike(lastRecordTime);
							DoHumidex(lastRecordTime);
							DoCloudBaseHeatIndex(lastRecordTime);

							DoForecast(string.Empty, false);

							UpdateStatusPanel(lastRecordTime);
							UpdateMQTT();

							// SensorContactLost = localSensorContactLost;

							cumulus.BatteryLowAlarm.Triggered = TxBatText.Contains("LOW");

							cumulus.LogDebugMessage("WL current: Last data update = " + lastRecordTime.ToString("s"));


							//// syncronise the timer to read just after the next update
							//var time = (DateTime.Now - lastDataUpdate).TotalSeconds % 60;
							//// get difference to 60 seconds and add 10 seconds to allow the servers to update
							//// allow a drift of 5 seconds (10 + 5 = 15)
							//if (time > 15)
							//{
							//	tmrCurrent.Interval = (70 - time) * 1000;
							//	tmrCurrent.Stop();
							//	tmrCurrent.Start();
							//	cumulus.LogMessage($"WL current: Amending fetch timimg by {(time):F1} seconds");
							//};

						}

						if (startingUp)
							startingUp = false;
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("GetWlLastArchive: Invalid current response received");
					cumulus.LogMessage("Response body = " + responseBody.ToString());
					return;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetWlLastArchive:  Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					cumulus.LogMessage($"GetWlLastArchive: Base exception - {ex.Message}");
				}
			}

			wlLastArchiveFetchOk = true;

			return;
		}

		/*
		private void DecodeCurrent(List<WlCurrentSensor> sensors)
		{
			try
			{
				var timestamp = DateTime.MinValue;
				var lastDataUpdate = DateTime.MinValue;
				var localSensorContactLost = false;

				foreach (var sensor in sensors)
				{
					switch (sensor.data_structure_type)
					{
						case 1: // VP2 ISS current record type A
						case 2: // VP2 ISS record type B
							{
								cumulus.LogDebugMessage("WL current: Processing VP ISS Current Data");

								// For some reason the API returns the data as an array, even though there is only one set of values?
								var data = sensor.data[0].FromJsv<WLCurrentSensordDataType1_2>();

								timestamp = Utils.FromUnixTime(data.ts);
								if (timestamp > lastDataUpdate)
									lastDataUpdate = timestamp;

								// No battery info in VP2 records
								// No RX state in VP2 records

								// Temperature & Humidity
								//Available fields
								//	*"temp_out": 62.7,                              // most recent valid temperature **(°F)**
								//	*"hum_out":1.1,                                 // most recent valid humidity **(%RH)**
								//	*"dew_point": -0.3,                             // **(°F)**
								//	*"heat_index": 5.5,                             // **(°F)**
								//	*"wind_chill": 6.0,                             // **(°F)**
								//	*"temp_in": 70.4,                               // **(°F)**
								//	*"hum_in": 45,                                  // **(%RH)**


								try
								{
									cumulus.LogDebugMessage($"WL current: using temp/hum data from TxId {data.tx_id}");
									if (data.hum_out.HasValue)
										DoOutdoorHumidity(Convert.ToInt32(data.hum_out.Value), timestamp);

									if (data.temp_out.HasValue)
										DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out.Value), timestamp);

									if (data.dew_point.HasValue)
										DoOutdoorDewpoint(ConvertUnits.TempFToUser(data.dew_point.Value), timestamp);

									if (data.wind_chill.HasValue)
									{
										// use wind chill from WLL
										DoWindChill(ConvertUnits.TempFToUser(data.wind_chill.Value), timestamp);
									}
									//TODO: Wet Bulb? rec["wet_bulb"] - No, we already have humidity
									//TODO: Heat Index? rec["heat_index"] - No, Cumulus always calculates HI
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL current: Error processing temperature values on TxId {data.tx_id}");
									cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
								}

								// Extra temperature/humidity - only 7 in VP records
								try
								{
									if (data.temp_extra_1.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_1.Value), 1);
									if (data.temp_extra_2.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_2.Value), 2);
									if (data.temp_extra_3.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_3.Value), 3);
									if (data.temp_extra_4.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_4.Value), 4);
									if (data.temp_extra_5.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_5.Value), 5);
									if (data.temp_extra_6.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_6.Value), 6);
									if (data.temp_extra_7.HasValue)
										DoExtraTemp(ConvertUnits.TempFToUser(data.temp_extra_7.Value), 7);

									if (data.hum_extra_1.HasValue)
										DoExtraHum(data.hum_extra_1.Value, 1);
									if (data.hum_extra_2.HasValue)
										DoExtraHum(data.hum_extra_2.Value, 2);
									if (data.hum_extra_3.HasValue)
										DoExtraHum(data.hum_extra_3.Value, 3);
									if (data.hum_extra_4.HasValue)
										DoExtraHum(data.hum_extra_4.Value, 4);
									if (data.hum_extra_5.HasValue)
										DoExtraHum(data.hum_extra_5.Value, 5);
									if (data.hum_extra_6.HasValue)
										DoExtraHum(data.hum_extra_6.Value, 6);
									if (data.hum_extra_7.HasValue)
										DoExtraHum(data.hum_extra_7.Value, 7);
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL current: Error processing Extra temperature/humidity values on TxId {data.tx_id}");
									cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
								}


								// wind
								try
								{
									if (timestamp == DateTime.MinValue)
										timestamp = Utils.FromUnixTime(data.ts);

									var gust = ConvertUnits.WindMPHToUser(data.wind_gust_10_min ?? 0);
									var avg = ConvertUnits.WindMPHToUser(data.wind_speed_10_min_avg ?? 0);
									double wind = ConvertUnits.WindMPHToUser(data.wind_speed ?? 0);
									int wdir = data.wind_dir ?? 0;

									DoWind(gust, wdir, avg, timestamp);

									RecentMaxGust = cumulus.Calib.WindGust.Calibrate(gust);
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL current: Error processing wind speeds on TxId {data.tx_id}");
									cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
								}


								// Rainfall
								cumulus.LogDebugMessage($"WL current: Using rain data from TxId {data.tx_id}");

								if (!data.rain_year_clicks.HasValue || !data.rain_rate_clicks.HasValue)
								{
									cumulus.LogDebugMessage("WL current: No rain values present!");
								}
								else
								{
									if (timestamp == DateTime.MinValue)
										timestamp = Utils.FromUnixTime(data.ts);

									// double check that the rainfall isn't out of date so we double count when it catches up
									var rain = ConvertRainClicksToUser(data.rain_year_clicks.Value, cumulus.DavisOptions.RainGaugeType);
									var rainrate = ConvertRainClicksToUser(data.rain_rate_clicks.Value, cumulus.DavisOptions.RainGaugeType);

									if (rain > 0 && rain < Raincounter)
									{
										cumulus.LogDebugMessage("WL current: The current yearly rainfall value is less than the value we had previously, ignoring it to avoid double counting");
									}
									else
									{
										DoRain(rain, rainrate, timestamp);
									}
								}

								if (!data.rain_storm_clicks.HasValue)
								{
									cumulus.LogDebugMessage("WL current: No rain storm values present");
								}
								else
								{
									try
									{
										StormRain = ConvertRainClicksToUser(data.rain_storm_clicks.Value, cumulus.DavisOptions.RainGaugeType) * cumulus.Calib.Rain.Mult;
										if (data.rain_storm_start_date.HasValue)
											StartOfStorm = Utils.FromUnixTime(data.rain_storm_start_date.Value);
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing rain storm values on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}


								// UV
								if (data.uv.HasValue)
								{
									try
									{
										cumulus.LogDebugMessage($"WL current: using UV data from TxId {data.tx_id}");
										if (timestamp == DateTime.MinValue)
											timestamp = Utils.FromUnixTime(data.ts);

										DoUV(data.uv.Value, timestamp);
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing UV value on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}


								// Solar
								if (data.solar_rad.HasValue)
								{
									try
									{
										cumulus.LogDebugMessage($"WL current: using solar data from TxId {data.tx_id}");
										if (timestamp == DateTime.MinValue)
											timestamp = Utils.FromUnixTime(data.ts);

										DoSolarRad(data.solar_rad.Value, timestamp);
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing Solar value on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}

							}
							break;

						case 10: // WeatherLink Live ISS current record
						case 23: // WeatherLink Console ISS current record
							{
								// For some reason the API returns the data as an array, even though there is only one set of values?
								var data = sensor.data[0].FromJsv<WLCurrentSensorDataType10_23>();

								timestamp = Utils.FromUnixTime(data.ts);
								if (timestamp >  lastDataUpdate)
									lastDataUpdate = timestamp;

								// need to look up lsid to get the transmitter id
								//var tx_id = sensorList.Where(item => item.lsid == sensor.lsid).FirstOrDefault().tx_id.Value;

								cumulus.LogDebugMessage($"WL current: found ISS data on TxId {data.tx_id}");

								// Battery
								if (data.trans_battery_flag.HasValue)
									SetTxBatteryStatus(data.tx_id, data.trans_battery_flag.Value);

								if (data.rx_state == 2)
								{
									localSensorContactLost = true;
									cumulus.LogWarningMessage($"WL current: Warning: Sensor contact lost TxId {data.tx_id}; ignoring data from this ISS");
									continue;
								}


								// Temperature & Humidity
								if (cumulus.WllPrimaryTempHum == data.tx_id)
								{
									 //Available fields
										//* "temp": 62.7,                                  // most recent valid temperature **(°F)**
										//* "hum":1.1,                                     // most recent valid humidity **(%RH)**
										//* "dew_point": -0.3,                             // **(°F)**
										//* "wet_bulb":null,                               // **(°F)**
										//* "heat_index": 5.5,                             // **(°F)**
										//* "wind_chill": 6.0,                             // **(°F)**
										//* "thw_index": 5.5,                              // **(°F)**
										//* "thsw_index": 5.5,                             // **(°F)**


									try
									{
										cumulus.LogDebugMessage($"WL current: using temp/hum data from TxId {data.tx_id}");
										if (data.hum.HasValue)
											DoOutdoorHumidity(Convert.ToInt32(data.hum.Value), timestamp);

										if (data.temp.HasValue)
											DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp.Value), timestamp);

										if (data.dew_point.HasValue)
											DoOutdoorDewpoint(ConvertUnits.TempFToUser(data.dew_point.Value), timestamp);

										if (data.wind_chill.HasValue)
										{
											// use wind chill from WLL
											DoWindChill(ConvertUnits.TempFToUser(data.wind_chill.Value), timestamp);
										}

										if (data.thsw_index.HasValue)
										{
											THSWIndex = ConvertUnits.TempFToUser(data.thsw_index.Value);
										}

										//TODO: Wet Bulb? rec["wet_bulb"] - No, we already have humidity
										//TODO: Heat Index? rec["heat_index"] - No, Cumulus always calculates HI
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing temperature values on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}
								else
								{   // Check for Extra temperature/humidity settings
									for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
									{
										if (cumulus.WllExtraTempTx[tempTxId] != data.tx_id) continue;

										try
										{
											if (cumulus.WllExtraTempTx[tempTxId] == data.tx_id)
											{
												if (!data.temp.HasValue || data.temp.Value == -99)
												{
													cumulus.LogDebugMessage($"WL current: no valid Extra temperature value found [{data.temp}] on TxId {data.tx_id}");
												}
												else
												{
													cumulus.LogDebugMessage($"WL current: using extra temp data from TxId {data.tx_id}");

													DoExtraTemp(ConvertUnits.TempFToUser(data.temp.Value), tempTxId);
												}

												if (cumulus.WllExtraHumTx[tempTxId] && data.hum.HasValue)
												{
													DoExtraHum(data.hum.Value, tempTxId);
												}
											}
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing Extra temperature/humidity values on TxId {data.tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
								}

								// Wind
								if (cumulus.WllPrimaryWind == data.tx_id)
								{

										//* Available fields
										//* "wind_speed_last":2,                           // most recent valid wind speed **(mph)**
										//* "wind_dir_last":null,                          // most recent valid wind direction **(°degree)**
										//* "wind_speed_avg_last_1_min":4                  // average wind speed over last 1 min **(mph)**
										//* "wind_dir_scalar_avg_last_1_min":15            // scalar average wind direction over last 1 min **(°degree)**
										//* "wind_speed_avg_last_2_min":42606,             // average wind speed over last 2 min **(mph)**
										//* "wind_dir_scalar_avg_last_2_min": 170.7,       // scalar average wind direction over last 2 min **(°degree)**
										//* "wind_speed_hi_last_2_min":8,                  // maximum wind speed over last 2 min **(mph)**
										//* "wind_dir_at_hi_speed_last_2_min":0.0,         // gust wind direction over last 2 min **(°degree)**
										//* "wind_speed_avg_last_10_min":42606,            // average wind speed over last 10 min **(mph)**
										//* "wind_dir_scalar_avg_last_10_min": 4822.5,     // scalar average wind direction over last 10 min **(°degree)**
										//* "wind_speed_hi_last_10_min":8,                 // maximum wind speed over last 10 min **(mph)**
										//* "wind_dir_at_hi_speed_last_10_min":0.0,        // gust wind direction over last 10 min **(°degree)**

									try
									{
										if (timestamp == DateTime.MinValue)
											timestamp = Utils.FromUnixTime(data.ts);

										var gust = ConvertUnits.WindMPHToUser(data.wind_speed_hi_last_10_min ?? 0);
										var avg = ConvertUnits.WindMPHToUser(data.wind_speed_avg_last_10_min ?? 0);
										var last = ConvertUnits.WindMPHToUser(data.wind_speed_last ?? 0);

										// pesky null values from WLL when it is calm
										int wdir = data.wind_dir_scalar_avg_last_1_min ?? 0;
										double wind = ConvertUnits.WindMPHToUser(data.wind_speed_last ?? 0);

										DoWind(last, wdir, avg, timestamp);

										RecentMaxGust = cumulus.Calib.WindGust.Calibrate(gust);



										//if (cumulus.StationOptions.PeakGustMinutes >= 2)
										//{
										//	var gust = ConvertUnits.WindMPHToUser(data.wind_speed_hi_last_2_min ?? 0);
										//	var gustCal = cumulus.Calib.WindGust.Calibrate(gust);
										//	var gustDir = data.wind_dir_at_hi_speed_last_2_min ?? 0;
										//	var gustDirCal = gustDir == 0 ? 0 : (int) cumulus.Calib.WindDir.Calibrate(gustDir);

										//	// See if the current speed is higher than the current max
										//	// We can then update the figure before the next data packet is read

										//	cumulus.LogDebugMessage($"WL current: Checking recent gust using wind data from TxId {data.tx_id}");

										//	if (gustCal > HiLoToday.HighGust)
										//	{
										//		// Check for spikes, and set highs
										//		if (CheckHighGust(gustCal, gustDirCal, timestamp))
										//		{
										//			cumulus.LogDebugMessage("Setting max gust from current value: " + gustCal.ToString(cumulus.WindFormat) + " was: " + RecentMaxGust.ToString(cumulus.WindFormat));
										//			RecentMaxGust = gustCal;
										//		}
										//	}
										//}

	}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing wind speeds on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}


								// Rainfall
								if (cumulus.WllPrimaryRain == data.tx_id)
								{

									//* Available fields:
									//* rec["rain_size"] - 0: Reserved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
									//* rec["rain_rate_last"], rec["rain_rate_hi"]
									//* rec["rainfall_last_15_min"], rec["rain_rate_hi_last_15_min"]
									//* rec["rainfall_last_60_min"]
									//* rec["rainfall_last_24_hr"]
									//* rec["rainfall_daily"]
									//* rec["rainfall_monthly"]
									//* rec["rainfall_year"]
									//* rec["rain_storm"], rec["rain_storm_start_at"]
									//* rec["rain_storm_last"], rec["rain_storm_last_start_at"], rec["rain_storm_last_end_at"]



									cumulus.LogDebugMessage($"WL current: using storm rain data from TxId {data.tx_id}");

									if (data.rain_size.HasValue)
									{
										switch (data.rain_size.Value)
										{
											case 1:
												if (cumulus.DavisOptions.RainGaugeType != 1)
												{
													cumulus.LogMessage($"Setting Davis rain tipper size - was {cumulus.DavisOptions.RainGaugeType}, now 1 = 0.01 in");
													cumulus.DavisOptions.RainGaugeType = 1;
													cumulus.WriteIniFile();
												}
												break;
											case 2:
												if (cumulus.DavisOptions.RainGaugeType != 0)
												{
													cumulus.LogMessage($"Setting Davis rain tipper size - was {cumulus.DavisOptions.RainGaugeType}, now 0 = 0.2 mm");
													cumulus.DavisOptions.RainGaugeType = 0;
													cumulus.WriteIniFile();
												}
												break;
											case 3:
												if (cumulus.DavisOptions.RainGaugeType != 2)
												{
													cumulus.LogMessage($"Setting Davis rain tipper size - was {cumulus.DavisOptions.RainGaugeType}, now 2 = 0.1 mm");
													cumulus.DavisOptions.RainGaugeType = 2;
													cumulus.WriteIniFile();
												}
												break;
											case 4:
												if (cumulus.DavisOptions.RainGaugeType != 3)
												{
													cumulus.LogMessage($"Setting Davis rain tipper size - was {cumulus.DavisOptions.RainGaugeType}, now 1 = 0.001 in");
													cumulus.DavisOptions.RainGaugeType = 1;
													cumulus.WriteIniFile();
												}
												break;

											default:
												cumulus.LogErrorMessage($"Error: Unknown Davis rain tipper size defined in data = {data.rain_size.Value}");
												break;
										}
									}

									// Rain data can be a bit out of date compared to the broadcasts (1 minute update), so only use storm data unless we are not receiving broadcasts

									cumulus.LogDebugMessage($"WL current: Using rain data from TxId {data.tx_id}");

									if (!data.rainfall_year_clicks.HasValue || !data.rain_rate_last_clicks.HasValue || !data.rain_size.HasValue)
									{
										cumulus.LogDebugMessage("WL current: No rain values present!");
									}
									else
									{
										if (timestamp == DateTime.MinValue)
											timestamp = Utils.FromUnixTime(data.ts);

										// double check that the rainfall isn't out of date so we double count when it catches up
										var rain = ConvertRainClicksToUser(data.rainfall_year_clicks.Value, data.rain_size.Value);
										var rainrate = ConvertRainClicksToUser(data.rain_rate_last_clicks.Value, data.rain_size.Value);

										if (rain > 0 && rain < Raincounter)
										{
											cumulus.LogDebugMessage("WL current: The current yearly rainfall value is less than the value we had previously, ignoring it to avoid double counting");
										}
										else
										{
											DoRain(rain, rainrate, timestamp);
										}
									}

									if (!data.rain_storm_clicks.HasValue || !data.rain_size.HasValue)
									{
										cumulus.LogDebugMessage("WL current: No rain storm values present");
									}
									else
									{
										try
										{
											StormRain = ConvertRainClicksToUser(data.rain_storm_clicks.Value, data.rain_size.Value) * cumulus.Calib.Rain.Mult;
											if (data.rain_storm_start_at.HasValue)
												StartOfStorm = Utils.FromUnixTime(data.rain_storm_start_at.Value);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing rain storm values on TxId {data.tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
								}

								if (cumulus.WllPrimaryUV == data.tx_id && data.uv_index.HasValue)
								{
									try
									{
										cumulus.LogDebugMessage($"WL current: using UV data from TxId {data.tx_id}");
										if (timestamp == DateTime.MinValue)
											timestamp = Utils.FromUnixTime(data.ts);

										DoUV(data.uv_index.Value, timestamp);
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing UV value on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}

								if (cumulus.WllPrimarySolar == data.tx_id && data.solar_rad.HasValue)
								{
									try
									{
										cumulus.LogDebugMessage($"WL current: using solar data from TxId {data.tx_id}");
										if (timestamp == DateTime.MinValue)
											timestamp = Utils.FromUnixTime(data.ts);

										DoSolarRad(data.solar_rad.Value, timestamp);
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL current: Error processing Solar value on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
								}
							}
							break;

						case 12: // WeatherLink Live - Non-ISS & Baro
						case 19: // WeatherLink Console - Baro
						case 25: // WeatherLink Console - Non-ISS
						case 21: // WeatherLink Console - Inside temp
							// test if ISS or other data
							switch (sensor.sensor_type)
							{
								case 242:
									// Barometer data
									cumulus.LogDebugMessage("WL current: found Baro data");

									try
									{
										// For some reason the API returns the data as an array, even though there is only one set of values?
										var data3 = sensor.data[0].FromJsv<WlCurrentSensorDataType12_19Baro>();

										if (data3.bar_sea_level.HasValue)
										{
											DoPressure(ConvertUnits.PressINHGToUser(data3.bar_sea_level.Value), Utils.FromUnixTime(data3.ts));
										}
										// Altimeter from absolute
										if (data3.bar_absolute.HasValue)
										{
											StationPressure = ConvertUnits.PressINHGToUser(data3.bar_absolute.Value);
											// Or do we use calibration? The VP2 code doesn't?
											//StationPressure = ConvertUnits.PressINHGToUser(rec.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
											AltimeterPressure = ConvertUnits.PressMBToUser(StationToAltimeter(ConvertUnits.UserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage("WL current: Error processing baro data");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
									break;

								case 243: // WLL
								case 365: // WLC
									// WeatherLink Live Inside temp data
									cumulus.LogDebugMessage("WL current: found Indoor temp/hum data");

									// For some reason the API returns the data as an array, even though there is only one set of values?
									var data4 = sensor.data[0].FromJsv<WlCurrentSensorDataType12_21Temp>();

									try
									{
										if (data4.temp_in.HasValue)
											DoIndoorTemp(Convert.TempFToUser(data4.temp_in.Value));
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage("WL current: Error processing indoor temp data");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}

									try
									{
										if (data4.hum_in.HasValue)
											DoIndoorHumidity(Convert.ToInt32(data4.hum_in.Value));
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage("WL current: Error processing indoor humidity data");
										cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
									}
									break;

								case 104: // Leaf/soil

									 //* Available fields
									 //* "temp_1":null,                                 // most recent valid soil temp slot 1 **(°F)**
									 //* "temp_2":null,                                 // most recent valid soil temp slot 2 **(°F)**
									 //* "temp_3":null,                                 // most recent valid soil temp slot 3 **(°F)**
									 //* "temp_4":null,                                 // most recent valid soil temp slot 4 **(°F)**
									 //* "moist_soil_1":null,                           // most recent valid soil moisture slot 1 **(|cb|)**
									 //* "moist_soil_2":null,                           // most recent valid soil moisture slot 2 **(|cb|)**
									 //* "moist_soil_3":null,                           // most recent valid soil moisture slot 3 **(|cb|)**
									 //* "moist_soil_4":null,                           // most recent valid soil moisture slot 4 **(|cb|)**
									 //* "wet_leaf_1":null,                             // most recent valid leaf wetness slot 1 **(no unit)**
									 //* "wet_leaf_2":null,                             // most recent valid leaf wetness slot 2 **(no unit)**
									 //*
									 //* "rx_state":null,                               // configured radio receiver state **(no unit)**
									 //* "trans_battery_flag":null                      // transmitter battery status flag **(no unit)**


									string idx = string.Empty;
									// For some reason the API returns the data as an array, even though there is only one set of values?
									var data = sensor.data[0].FromJsv<WLCurrentSensorDataType12_25>();

									// need to look up lsid to get the transmitter id
									var tx_id = sensorList.Where(item => item.lsid == sensor.lsid).FirstOrDefault().tx_id.Value;

									cumulus.LogDebugMessage($"WL current: found Leaf/Soil data on TxId {tx_id}");

									// Battery
									if (data.trans_battery_flag.HasValue)
										SetTxBatteryStatus(tx_id, data.trans_battery_flag.Value);

									if (data.rx_state == 2)
									{
										localSensorContactLost = true;
										cumulus.LogWarningMessage($"Warning: Sensor contact lost TxId {tx_id}; ignoring data from this Leaf/Soil transmitter");
										continue;
									}

									// For leaf wetness, soil temp/moisture we rely on user configuration, trap any errors

									// Leaf wetness
									try
									{
										if (cumulus.WllExtraLeafTx1 == tx_id)
										{
											idx = "wet_leaf_" + cumulus.WllExtraLeafIdx1;
											var val = (double?) data[idx];
											if (val.HasValue)
												DoLeafWetness(val.Value, 1);
										}
										if (cumulus.WllExtraLeafTx2 == tx_id)
										{
											idx = "wet_leaf_" + cumulus.WllExtraLeafIdx2;
											var val = (double?) data[idx];
											if (val.HasValue)
												DoLeafWetness(val.Value, 2);
										}
									}
									catch (Exception e)
									{
										cumulus.LogErrorMessage($"WL current: Error processing LeafWetness txid={tx_id}, idx={idx}");
										cumulus.LogDebugMessage($"LL current: Exception: {e.Message}");
									}

									// Soil moisture
									if (cumulus.WllExtraSoilMoistureTx1 == tx_id)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx1;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilMoisture(val.Value, 1);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx1} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									if (cumulus.WllExtraSoilMoistureTx2 == tx_id)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx2;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilMoisture(val.Value, 2);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx2} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									if (cumulus.WllExtraSoilMoistureTx3 == tx_id)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx3;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilMoisture(val.Value, 3);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx3} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									if (cumulus.WllExtraSoilMoistureTx4 == tx_id)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx4;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilMoisture(val.Value, 4);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx4} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}

									// SoilTemperature
									if (cumulus.WllExtraSoilTempTx1 == tx_id)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx1;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilTemp(ConvertUnits.TempFToUser(val.Value), 1);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx1} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									if (cumulus.WllExtraSoilTempTx2 == tx_id)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx2;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilTemp(ConvertUnits.TempFToUser(val.Value), 2);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx2} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									if (cumulus.WllExtraSoilTempTx3 == tx_id)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx3;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilTemp(ConvertUnits.TempFToUser(val.Value), 3);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx3} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									if (cumulus.WllExtraSoilTempTx4 == tx_id)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx4;
										try
										{
											var val = (double?) data[idx];
											if (val.HasValue)
												DoSoilTemp(ConvertUnits.TempFToUser(val.Value), 4);
										}
										catch (Exception ex)
										{
											cumulus.LogErrorMessage($"WL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx4} on TxId {tx_id}");
											cumulus.LogDebugMessage($"WL current: Exception: {ex.Message}");
										}
									}
									break;

								// TODO: Extra Humidity? No type for this on WLL

								default:
									cumulus.LogWarningMessage($"WL current: Error. Uknown data type found. Sensor Type={sensor.sensor_type}, Data Structure={sensor.data_structure_type}");
									break;

							}
							break;

						case 15: // WeatherLink Live Health Data
							{
								var data = sensor.data[0].FromJsv<WlHealthDataType15>();
								DecodeWllHealth(data, startingUp);
							}
							break;

						case 16: // AirLink current
							cumulus.LogMessage("WL current: TODO - add AirLink Current Data processing");
							break;

						case 18: // Airlink Health
							cumulus.LogMessage("WL current: TODO - add AirLink Health Data processing");
							break;

						case 27: // WeatherLink Console health
							cumulus.LogMessage("WL current: TODO - add WeatherLink Console Health Data processing");
							break;


						default:
							cumulus.LogWarningMessage($"WL current: Error. Uknown data type found. Sensor Type={sensor.sensor_type}, Data Structure={sensor.data_structure_type}");
							break;
					}
				}

				// Now we have the primary data, calculate the derived data
				if (cumulus.StationOptions.CalculatedWC)
				{
					if (ConvertUnits.UserWindToMS(WindAverage) < 1.5)
					{
						// wind speed too low, use the temperature
						DoWindChill(OutdoorTemperature, timestamp);
					}
					else
					{
						// calculate wind chill from calibrated C temp and calibrated wind in KPH
						DoWindChill(ConvertUnits.TempCToUser(MeteoLib.WindChill(ConvertUnits.UserTempToC(OutdoorTemperature), ConvertUnits.UserWindToKPH(WindAverage))), timestamp);
					}
				}

				DoApparentTemp(timestamp);
				DoFeelsLike(timestamp);
				DoHumidex(timestamp);
				DoCloudBaseHeatIndex(timestamp);

				DoForecast(string.Empty, false);

				UpdateStatusPanel(timestamp);
				UpdateMQTT();

				SensorContactLost = localSensorContactLost;

				cumulus.BatteryLowAlarm.Triggered = TxBatText.Contains("LOW");

				cumulus.LogDebugMessage("WL current: Last data update = " + lastDataUpdate.ToString("s"));


				//// syncronise the timer to read just after the next update
				//var time = (DateTime.Now - lastDataUpdate).TotalSeconds % 60;
				//// get difference to 60 seconds and add 10 seconds to allow the servers to update
				//// allow a drift of 5 seconds (10 + 5 = 15)
				//if (time > 15)
				//{
				//	tmrCurrent.Interval = (70 - time) * 1000;
				//	tmrCurrent.Stop();
				//	tmrCurrent.Start();
				//	cumulus.LogMessage($"WL current: Amending fetch timimg by {(time):F1} seconds");
				//};

	}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeCurrent: Exception Caught!");
				cumulus.LogDebugMessage("Message :" + exp.Message);
			}
		}
		*/

		private double ConvertRainClicksToUser(double clicks, int size)
		{
			// 0: Reserved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
			switch (size)
			{
				case 1:
					return ConvertUnits.RainINToUser(clicks * 0.01);
				case 2:
					return ConvertUnits.RainMMToUser(clicks * 0.2);
				case 3:
					return ConvertUnits.RainMMToUser(clicks * 0.1);
				case 4:
					return ConvertUnits.RainINToUser(clicks * 0.001);
				default:
					switch (cumulus.DavisOptions.RainGaugeType)
					{
						// Hmm, no valid tip size from WLL...
						// One click is normally either 0.01 inches or 0.2 mm
						// Try the setting in Cumulus.ini
						// Rain gauge type not configured, assume from units
						case -1 when cumulus.Units.Rain == 0:
							return clicks * 0.2;
						case -1:
							return clicks * 0.01;
						// Rain gauge is metric, convert to user unit
						case 0:
							return ConvertUnits.RainMMToUser(clicks * 0.2);
						default:
							return ConvertUnits.RainINToUser(clicks * 0.01);
					}
			}
		}

		private void SetTxBatteryStatus(int txId, int status)
		{
			// Split the string
			var delimiters = new[] { ' ', '-' };
			var sl = TxBatText.Split(delimiters);

			TxBatText = "";
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
			cumulus.LogMessage("WLL history: Reading history data from log files");
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			cumulus.LogMessage("WLL history: Reading archive data from WeatherLink API");
			bw = new BackgroundWorker { WorkerSupportsCancellation = true };
			bw.DoWork += bw_ReadHistory;
			bw.RunWorkerCompleted += bw_ReadHistoryCompleted;
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		private void bw_ReadHistoryCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cumulus.LogMessage("WLL history: WeatherLink API archive reading thread completed");
			if (e.Error != null)
			{
				cumulus.LogErrorMessage("WLL history: Archive reading thread apparently terminated with an error: " + e.Error.Message);
			}
			cumulus.LogMessage("WLL history: Updating highs and lows");
			//using (cumulusEntities dataContext = new cumulusEntities())
			//{
			//    UpdateHighsAndLows(dataContext);
			//}
			cumulus.NormalRunning = true;

			CalcRecentMaxGust = savedCalculatePeakGust;

			StartLoop();
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		private void bw_ReadHistory(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;

			int archiveRun = 0;
			//cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			//cumulus.LogDebugMessage("Lock: Station has the lock");

			try
			{
				// set this temporarily, so speed is done from average and not peak gust from logger
				cumulus.StationOptions.UseSpeedForAvgCalc = true;

				// same for gust values
				savedCalculatePeakGust = CalcRecentMaxGust;
				CalcRecentMaxGust = true;

				do
				{
					GetWlHistoricData(worker);
					archiveRun++;
				} while (archiveRun < maxArchiveRuns && worker.CancellationPending == false);

				// restore the setting
				cumulus.StationOptions.UseSpeedForAvgCalc = false;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Exception occurred reading archive data: " + ex.Message);
			}

			// force a calculation of the current gust and average wind speed so we do not get a zero values at startup


			//cumulus.LogDebugMessage("Lock: Station releasing the lock");
			Cumulus.syncInit.Release();
			bwDoneEvent.Set();
		}

		private void GetWlHistoricData(BackgroundWorker worker)
		{
			cumulus.LogMessage("GetWlHistoricData: Get WL.com Historic Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("GetWlHistoricData: Missing WeatherLink API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				const string msg = "No WeatherLink API station ID in the configuration";
				cumulus.LogWarningMessage(msg);
				Cumulus.LogConsoleMessage("GetWlHistoricData: " + msg);

			}

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = Utils.ToUnixTime(cumulus.LastUpdateTime);
			long endTime = unixDateTime;
			int unix24hrs = 24 * 60 * 60;

			// The API call is limited to fetching 24 hours of data
			if (unixDateTime - startTime > unix24hrs)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime + unix24hrs;
				maxArchiveRuns++;
			}

			Cumulus.LogConsoleMessage($"Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime:s} to: {Utils.FromUnixTime(endTime):s}");
			cumulus.LogMessage($"GetWlHistoricData: Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime:s} to: {Utils.FromUnixTime(endTime):s}");

			StringBuilder historicUrl = new StringBuilder("https://api.weatherlink.com/v2/historic/" + cumulus.WllStationId);
			historicUrl.Append("?api-key=" + cumulus.WllApiKey);
			historicUrl.Append("&start-timestamp=" + startTime.ToString());
			historicUrl.Append("&end-timestamp=" + endTime.ToString());

			cumulus.LogDebugMessage($"WeatherLink URL = {historicUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");

			lastDataReadTime = cumulus.LastUpdateTime;
			int luhour = lastDataReadTime.Hour;

			int rollHour = Math.Abs(cumulus.GetHourInc());

			cumulus.LogMessage($"Roll over hour = {rollHour}");

			bool rolloverdone = luhour == rollHour;

			bool midnightraindone = luhour == 0;

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
				using (var response = Cumulus.MyHttpClient.SendAsync(request).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"GetWlHistoricData: WeatherLink API Historic Response code: {responseCode}");
					cumulus.LogDataMessage($"GetWlHistoricData: WeatherLink API Historic Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var historyError = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"GetWlHistoricData: WeatherLink API Historic Error: {historyError.code}, {historyError.message}");
					Cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.message}", ConsoleColor.Red);
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("GetWlHistoricData: WeatherLink API Historic: No data was returned. Check your Device Id.");
					Cumulus.LogConsoleMessage(" - No historic data available");
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}
				else if (responseBody.StartsWith("{\"")) // basic sanity check
				{
					histObj = responseBody.FromJson<WlHistory>();

					// get the sensor data
					int idxOfSensorWithMostRecs = 0;
					for (var i = 0; i < histObj.sensors.Count; i++)
					{
						// Find the WLL baro, or internal temp/hum sensors
						if (histObj.sensors[i].sensor_type == 242 && histObj.sensors[i].data_structure_type == 13)
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
						cumulus.LogMessage("GetWlHistoricData: No historic data available");
						Cumulus.LogConsoleMessage(" - No historic data available");
						cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
						return;
					}
					else
					{
						cumulus.LogMessage($"GetWlHistoricData: Found {noOfRecs} historic records to process");
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("GetWlHistoricData: Invalid historic message received");
					cumulus.LogMessage("GetWlHistoricData: Received: " + responseBody);
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetWlHistoricData:  Exception: " + ex.Message);
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					cumulus.LogMessage($"GetWlHistoricData: Base exception - {ex.Message}");
				}

				cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
				return;
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				if (worker.CancellationPending == true)
					return;

				try
				{
					// Not all sensors may have the same number of records. We are using the WLL to create the historic data, the other sensors (AirLink) may have more or less records!
					// For the additional sensors, check if they have the same number of records as the WLL. If they do great, we just process the next record.
					// If the sensor has more or less historic records than the WLL, then we find the record (if any) that matches the WLL record timestamp

					var refData = sensorWithMostRecs.data[dataIndex].FromJsv<WlHistorySensorDataType13Baro>();
					var timestamp = Utils.FromUnixTime(refData.ts);

					cumulus.LogMessage($"GetWlHistoricData: Processing record {timestamp:yyyy-MM-dd HH:mm}");

					var h = timestamp.Hour;

					//  if outside roll-over hour, roll-over yet to be done
					if (h != rollHour)
					{
						rolloverdone = false;
					}

					// Things that really "should" to be done before we reset the day because the roll-over data contains data for the previous day for these values
					// Windrun
					// Dominant wind bearing
					// ET - if MX calculated
					// Degree days
					// Rainfall

					// In roll-over hour and roll-over not yet done
					if ((h == rollHour) && !rolloverdone)
					{
						// do roll-over
						cumulus.LogMessage("GetWlHistoricData: Day roll-over " + timestamp.ToShortTimeString());
						DayReset(timestamp);
						rolloverdone = true;
					}

					// Not in midnight hour, midnight rain yet to be done
					if (h != 0)
					{
						midnightraindone = false;
					}

					// In midnight hour and midnight rain (and sun) not yet done
					if ((h == 0) && !midnightraindone)
					{
						ResetMidnightRain(timestamp);
						ResetSunshineHours(timestamp);
						ResetMidnightTemperatures(timestamp);
						midnightraindone = true;
					}

					DecodeHistoric(sensorWithMostRecs.data_structure_type, sensorWithMostRecs.sensor_type, sensorWithMostRecs.data[dataIndex]);

					foreach (var sensor in histObj.sensors)
					{
						if (worker.CancellationPending == true)
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
									if (worker.CancellationPending == true)
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
									cumulus.LogDebugMessage("GetWlHistoricData: Warning. No outdoor AirLink data for this log interval !!");
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
									if (worker.CancellationPending == true)
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
									cumulus.LogDebugMessage("GetWlHistoricData: Warning. No indoor AirLink data for this log interval !!");
							}
							else
							{
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkIn.DecodeAlHistoric(dataStructureType, sensor.data[dataIndex]);
							}
						}
						else if (sensorType != 504 && sensorType != 506 && lsid != sensorWithMostRecs.lsid)
						{
							if (sensor.data.Count > dataIndex)
							{
								DecodeHistoric(dataStructureType, sensorType, sensor.data[dataIndex]);
								// sensor 504 (WLL info) does not always contain a full set of records, so grab the timestamp from a 'real' sensor
							}
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

					// Log all the data
					cumulus.DoLogFile(timestamp, false);
					cumulus.LogMessage("GetWlHistoricData: Log file entry written");
					cumulus.MySqlRealtimeFile(999, false, timestamp);
					cumulus.DoCustomIntervalLogs(timestamp);

					if (cumulus.StationOptions.LogExtraSensors)
					{
						cumulus.DoExtraLogFile(timestamp);
					}

					if (cumulus.airLinkOut != null || cumulus.airLinkIn != null)
					{
						cumulus.DoAirLinkLogFile(timestamp);
					}

					AddRecentDataWithAq(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
						OutdoorHumidity, Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);
					DoTrendValues(timestamp);

					if (cumulus.StationOptions.CalculatedET && timestamp.Minute == 0)
					{
						// Start of a new hour, and we want to calculate ET in Cumulus
						CalculateEvapotranspiration(timestamp);
					}

					UpdateStatusPanel(timestamp);
					cumulus.AddToWebServiceLists(timestamp);


					if (!Program.service)
						Console.Write("\r - processed " + (((double) dataIndex + 1) / noOfRecs).ToString("P0"));
					cumulus.LogMessage($"GetWlHistoricData: {dataIndex + 1} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("GetWlHistoricData: Exception: " + ex.Message);
				}
			}

			if (!Program.service)
				Console.WriteLine(""); // flush the progress line
		}

		private void DecodeHistoric(int dataType, int sensorType, string json, bool current=false)
		{
			// The WLL sends the timestamp in Unix ticks, and in UTC

			try
			{
				switch (dataType)
				{
					case 3: // VP2 ISS archive revision A
					case 4: // VP2 ISS archive revision B
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
									cumulus.LogErrorMessage($"WL.com historic: Warning, no valid Humidity data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing Primary humidity value on TxId {data.tx_id}. Error: {ex.Message}");
							}

							// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
							try
							{
								if (data.temp_out.HasValue && data.temp_out == -99)
								{
									cumulus.LogErrorMessage($"WL.com historic: Warning, no valid Primary temperature value found [-99] on TxId {data.tx_id}");
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: using temp/hum data from TxId {data.tx_id}");

									// do high temp
									if (data.temp_out_hi.HasValue)
									{
										DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out_hi.Value), lastRecordTime);
									}
									else
									{
										cumulus.LogErrorMessage($"WL.com historic: Warning, no valid Temperature (high) data on TxId {data.tx_id}");
									}

									// do low temp
									if (data.temp_out_lo.HasValue)
									{
										DoOutdoorTemp(ConvertUnits.TempFToUser(data.temp_out_lo.Value), lastRecordTime);
									}
									else
									{
										cumulus.LogErrorMessage($"WL.com historic: Warning, no valid Temperature (low) data on TxId {data.tx_id}");
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
											if (OutdoorTemperature < cumulus.ChillHourThreshold)
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
										cumulus.LogErrorMessage($"WL.com historic: Warning, no valid Temperature data on TxId {data.tx_id}");
									}
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing Primary temperature value on TxId {data.tx_id}. Error: {ex.Message}");
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
									cumulus.LogErrorMessage($"WL.com historic: Warning, no valid Dew Point data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing dew point value on TxId {data.tx_id}. Error: {ex.Message}");
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
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Wind Chill data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing wind chill value on TxId {data.tx_id}. Error: {ex.Message}");
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
									cumulus.LogDebugMessage($"WL.com historic: using wind data from TxId {data.tx_id}");
									DoWind(gust, dir, spd, lastRecordTime);
									RecentMaxGust = cumulus.Calib.WindGust.Calibrate(gust);
									//AddValuesToRecentWind(spd, spd, recordTs.AddSeconds(-data.arch_int), recordTs);
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Wind data on TxId {data.tx_id}");
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
									cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Wind data (avg) on TxId {data.tx_id}");
								}

								if (current)
								{
									// we do not have the latest value, so set a pseudo value between average and gust
									WindLatest = random.Next((int) WindAverage, (int) RecentMaxGust);
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing wind values on TxId {data.tx_id}. Error: {ex.Message}");
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
									cumulus.LogDebugMessage($"WL.com historic: using rain data from TxId {data.tx_id}");

									var rain = ConvertRainClicksToUser(data.rainfall_clicks.Value, cumulus.DavisOptions.RainGaugeType);
									var rainrate = ConvertRainClicksToUser(data.rain_rate_hi_clicks.Value, cumulus.DavisOptions.RainGaugeType);
									if (rain > 0)
									{
										cumulus.LogDebugMessage($"WL.com historic: Adding rain {rain.ToString(cumulus.RainFormat)}");
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
									cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Rain data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing rain data on TxId {data.tx_id}. Error:{ex.Message}");
							}

							// Pressure
							/*
								bar
								abs_press
								bar_noaa
								bar_alt
							*/
							// log the current value
							cumulus.LogDebugMessage("WL.com historic: found Baro data");
							try
							{
								if (data.bar != null)
								{
									// leave it at current value
									DoPressure(ConvertUnits.PressINHGToUser((double) data.bar), lastRecordTime);
								}
								else
								{
									cumulus.LogWarningMessage("WL.com historic: Warning, no valid Baro data");
								}

								// Altimeter from absolute
								if (data.abs_press != null)
								{
									StationPressure = ConvertUnits.PressINHGToUser((double) data.abs_press);
									// Or do we use calibration? The VP2 code doesn't?
									//StationPressure = ConvertUnits.PressINHGToUser(data.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
									AltimeterPressure = ConvertUnits.PressMBToUser(StationToAltimeter(ConvertUnits.UserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing baro reading. Error: {ex.Message}");
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
									cumulus.LogDebugMessage($"WL.com historic: using UV data from TxId {data.tx_id}");

									DoUV(data.uv_index_avg.Value, lastRecordTime);
								}
								else
								{
									cumulus.LogWarningMessage($"WL.com historic: Warning, no valid UV data on TxId {data.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing UV value on TxId {data.tx_id}. Error: {ex.Message}");
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
									cumulus.LogDebugMessage($"WL.com historic: using solar data from TxId {data.tx_id}");
									DoSolarRad(data.solar_rad_avg.Value, lastRecordTime);

									if (!current)
									{
										// add in archive period worth of sunshine, if sunny - arch_int in seconds
										if ((SolarRad > CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100.00) && (SolarRad >= cumulus.SolarOptions.SolarMinimum))
										{
											SunshineHours += (data.arch_int / 3600.0);
										}
									}
								}
								else
								{
									cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Solar data on TxId {data.tx_id}");
								}

								if (data.et.HasValue && !cumulus.StationOptions.CalculatedET)
								{
									// wl.com ET is only available in record the start of each hour.
									// The number is the total for the one hour period.
									// This is unlike the existing VP2 when the ET is an annual running total
									// So we try and mimic the VP behaviour
									var newET = AnnualETTotal + ConvertUnits.RainINToUser(data.et.Value);
									cumulus.LogDebugMessage($"WLL DecodeHistoric: Adding {ConvertUnits.RainINToUser(data.et.Value):F3} to ET");
									DoET(newET, lastRecordTime);
								}
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"WL.com historic: Error processing Solar value on TxId {data.tx_id}. Error: {ex.Message}");
							}

							string idx = "";

							// Leaf Wetness
							cumulus.LogDebugMessage($"WL.com historic: found Leaf/Soil data on TxId {data.tx_id}");
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
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Humidity (high) data on TxId {data.tx_id}");
									}

									// do low humidity
									if (data.hum_lo_at != 0 && data.hum_lo != null)
									{
										ts = Utils.FromUnixTime(data.hum_lo_at);
										DoOutdoorHumidity(Convert.ToInt32(data.hum_lo), ts);
									}
									else
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Humidity (low) data on TxId {data.tx_id}");
									}

									if (data.hum_last != null)
									{
										// do current humidity
										DoOutdoorHumidity(Convert.ToInt32(data.hum_last), lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Humidity data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing Primary humidity value on TxId {data.tx_id}. Error: {ex.Message}");
								}

								// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
								try
								{
									if (data.temp_last == -99)
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Primary temperature value found [-99] on TxId {data.tx_id}");
									}
									else
									{
										cumulus.LogDebugMessage($"WL.com historic: using temp/hum data from TxId {data.tx_id}");

										// do high temp
										if (data.temp_hi_at != 0 && data.temp_hi != null)
										{
											ts = Utils.FromUnixTime(data.temp_hi_at);
											DoOutdoorTemp(ConvertUnits.TempFToUser((double) data.temp_hi), ts);
										}
										else
										{
											cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Temperature (high) data on TxId {data.tx_id}");
										}

										// do low temp
										if (data.temp_lo_at != 0 && data.temp_lo != null)
										{
											ts = Utils.FromUnixTime(data.temp_lo_at);
											DoOutdoorTemp(ConvertUnits.TempFToUser((double) data.temp_lo), ts);
										}
										else
										{
											cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Temperature (low) data on TxId {data.tx_id}");
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
												if (OutdoorTemperature < cumulus.ChillHourThreshold)
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
											cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Temperature data on TxId {data.tx_id}");
										}
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing Primary temperature value on TxId {data.tx_id}. Error: {ex.Message}");
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
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Dew Point (high) data on TxId {data.tx_id}");
									}

									// do low DP
									if (data.dew_point_lo_at != 0 && data.dew_point_lo != null)
									{
										ts = Utils.FromUnixTime(data.dew_point_lo_at);
										DoOutdoorDewpoint(ConvertUnits.TempFToUser((double) data.dew_point_lo), ts);
									}
									else
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Dew Point (low) data on TxId {data.tx_id}");
									}

									// do last DP
									if (data.dew_point_last != null)
									{
										DoOutdoorDewpoint(ConvertUnits.TempFToUser((double) data.dew_point_last), lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Dew Point data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing dew point value on TxId {data.tx_id}. Error: {ex.Message}");
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
											cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Wind Chill (low) data on TxId {data.tx_id}");
										}

										// do last WC
										if (data.wind_chill_last != null)
										{
											DoWindChill(ConvertUnits.TempFToUser((double) data.wind_chill_last), lastRecordTime);
										}
										else
										{
											cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Wind Chill data on TxId {data.tx_id}");
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL.com historic: Error processing wind chill value on TxId {data.tx_id}. Error: {ex.Message}");
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
										if (data.temp_last == -99 || data.temp_last == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Extra temperature value on TxId {data.tx_id}");
										}
										else
										{
											cumulus.LogDebugMessage($"WL.com historic: using extra temp data from TxId {data.tx_id}");

											DoExtraTemp(ConvertUnits.TempFToUser((double) data.temp_last), tempTxId);
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL.com historic: Error processing extra temp value on TxId {data.tx_id}");
										cumulus.LogDebugMessage($"WL.com historic: Exception {ex.Message}");
									}

									if (!cumulus.WllExtraHumTx[tempTxId]) continue;

									try
									{
										if (data.hum_last != null)
										{
											DoExtraHum((double) data.hum_last, tempTxId);
										}
									}
									catch (Exception ex)
									{
										cumulus.LogErrorMessage($"WL.com historic: Error processing extra humidity value on TxId {data.tx_id}. Error: {ex.Message}");
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
										cumulus.LogDebugMessage($"WL.com historic: using wind data from TxId {data.tx_id}");
										DoWind(gust, dir, spd, lastRecordTime);
										AddValuesToRecentWind(spd, spd, dir, lastRecordTime.AddSeconds(-data.arch_int), lastRecordTime);
									}
									else
									{
										cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Wind data on TxId {data.tx_id}");
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
										cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Wind data (avg) on TxId {data.tx_id}");
									}

									if (current)
									{
										// we do not have the latest value, so set a pseudo value between average and gust
										WindLatest = random.Next((int) WindAverage, (int) RecentMaxGust);
									}

								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing wind values on TxId {data.tx_id}. Error: {ex.Message}");
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
										cumulus.LogDebugMessage($"WL.com historic: using rain data from TxId {data.tx_id}");

										var rain = ConvertRainClicksToUser((double) data.rainfall_clicks, data.rain_size);
										var rainrate = ConvertRainClicksToUser((double) data.rain_rate_hi_clicks, data.rain_size);
										if (rain > 0)
										{
											cumulus.LogDebugMessage($"WL.com historic: Adding rain {rain.ToString(cumulus.RainFormat)}");
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
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Rain data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing rain data on TxId {data.tx_id}. Error:{ex.Message}");
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
										cumulus.LogDebugMessage($"WL.com historic: using UV data from TxId {data.tx_id}");

										DoUV((double) data.uv_index_avg, lastRecordTime);
									}
									else
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid UV data on TxId {data.tx_id}");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing UV value on TxId {data.tx_id}. Error: {ex.Message}");
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
										cumulus.LogDebugMessage($"WL.com historic: using solar data from TxId {data.tx_id}");
										DoSolarRad((int) data.solar_rad_avg, lastRecordTime);

										if (!current)
										{
											// add in archive period worth of sunshine, if sunny - arch_int in seconds
											if ((SolarRad > CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100.00) && (SolarRad >= cumulus.SolarOptions.SolarMinimum))
											{
												SunshineHours += (data.arch_int / 3600.0);
											}
										}
									}
									else
									{
										cumulus.LogWarningMessage($"WL.com historic: Warning, no valid Solar data on TxId {data.tx_id}");
									}

									if (data.et != null && !cumulus.StationOptions.CalculatedET)
									{
										// wl.com ET is only available in record the start of each hour.
										// The number is the total for the one hour period.
										// This is unlike the existing VP2 when the ET is an annual running total
										// So we try and mimic the VP behaviour
										var newET = AnnualETTotal + ConvertUnits.RainINToUser((double) data.et);
										cumulus.LogDebugMessage($"WLL DecodeHistoric: Adding {ConvertUnits.RainINToUser((double) data.et):F3} to ET");
										DoET(newET, lastRecordTime);
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing Solar value on TxId {data.tx_id}. Error: {ex.Message}");
								}
							}
						}
						break;

					case 13: // WeatherLink Live Non-ISS data
					case 26:
						switch (sensorType)
						{
							case 56: // Soil + Leaf
								var data = json.FromJsv<WlHistorySensorDataType13>();

								string idx = "";
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

								cumulus.LogDebugMessage($"WL.com historic: found Leaf/Soil data on TxId {data.tx_id}");

								// We are relying on user configuration here, trap any errors
								try
								{
									if (cumulus.WllExtraLeafTx1 == data.tx_id)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx1;
										if (data[idx] != null)
											DoLeafWetness((double) data[idx], 1);
									}
									if (cumulus.WllExtraLeafTx2 == data.tx_id)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx2;
										if (data[idx] != null)
											DoLeafWetness((double) data[idx], 2);
									}
								}
								catch (Exception e)
								{
									cumulus.LogErrorMessage($"Error, DecodeHistoric, LeafWetness txid={data.tx_id}, idx={idx}: {e.Message}");
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

								try
								{
									if (cumulus.WllExtraSoilMoistureTx1 == data.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx1;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilMoisture((double) data[idx], 1);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx2 == data.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx2;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilMoisture((double) data[idx], 2);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx3 == data.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx3;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilMoisture((double) data[idx], 3);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx4 == data.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx4;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilMoisture((double) data[idx], 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogErrorMessage($"Error, DecodeHistoric, SoilMoisture txid={data.tx_id}, idx={idx}: {e.Message}");
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

								try
								{
									if (cumulus.WllExtraSoilTempTx1 == data.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx1;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertUnits.TempFToUser((double) data[idx]), 1);
										}
									}
									if (cumulus.WllExtraSoilTempTx2 == data.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx2;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertUnits.TempFToUser((double) data[idx]), 2);
										}
									}
									if (cumulus.WllExtraSoilTempTx3 == data.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx3;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertUnits.TempFToUser((double) data[idx]), 3);
										}
									}
									if (cumulus.WllExtraSoilTempTx4 == data.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx4;
										if (data[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} on TxId {data.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertUnits.TempFToUser((double) data[idx]), 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogErrorMessage($"Error, DecodeHistoric, SoilTemp txid={data.tx_id}, idx={idx}: {e.Message}");
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
								cumulus.LogDebugMessage("WL.com historic: found Baro data");
								try
								{
									var data13baro = json.FromJsv<WlHistorySensorDataType13Baro>();
									DateTime ts;
									// check the high
									if (data13baro.bar_hi_at != 0 && data13baro.bar_hi != null)
									{
										ts = Utils.FromUnixTime(data13baro.bar_hi_at);
										DoPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_hi), ts);
									}
									else
									{
										cumulus.LogWarningMessage("WL.com historic: Warning, no valid Baro data (high)");
									}
									// check the low
									if (data13baro.bar_lo_at != 0 && data13baro.bar_lo != null)
									{
										ts = Utils.FromUnixTime(data13baro.bar_lo_at);
										DoPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_lo), ts);
									}
									else
									{
										cumulus.LogWarningMessage("WL.com historic: Warning, no valid Baro data (high)");
									}

									if (data13baro.bar_sea_level != null)
									{
										// leave it at current value
										ts = Utils.FromUnixTime(data13baro.ts);
										DoPressure(ConvertUnits.PressINHGToUser((double) data13baro.bar_sea_level), ts);
									}
									else
									{
										cumulus.LogWarningMessage("WL.com historic: Warning, no valid Baro data (high)");
									}

									// Altimeter from absolute
									if (data13baro.bar_absolute != null)
									{
										StationPressure = ConvertUnits.PressINHGToUser((double) data13baro.bar_absolute);
										// Or do we use calibration? The VP2 code doesn't?
										//StationPressure = ConvertUnits.PressINHGToUser(data.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
										AltimeterPressure = ConvertUnits.PressMBToUser(StationToAltimeter(ConvertUnits.UserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing baro reading. Error: {ex.Message}");
								}
								break;

							case 243: // Inside temp/hum
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
								cumulus.LogDebugMessage("WL.com historic: found inside temp/hum data");

								var data13temp = json.FromJsv<WlHistorySensorDataType13Temp>();
								try
								{
									if (data13temp.temp_in_last != null)
									{
										DoIndoorTemp(ConvertUnits.TempFToUser((double) data13temp.temp_in_last));
									}
									else
									{
										cumulus.LogWarningMessage("WL.com historic: Warning, no valid Inside Temperature");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage($"WL.com historic: Error processing temp-in reading. Error: {ex.Message}]");
								}


								try
								{
									if (data13temp.hum_in_last != null)
									{
										DoIndoorHumidity(Convert.ToInt32(data13temp.hum_in_last));
									}
									else
									{
										cumulus.LogWarningMessage("WL.com historic: Warning, no valid Inside Humidity");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogDebugMessage($"WLL current: Error processing humidity-in. Error: {ex.Message}]");
								}

								break;

							default:
								cumulus.LogDebugMessage($"WL.com historic: Not processing sensor type [{sensorType}]!");
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

					default:
						cumulus.LogDebugMessage($"WL.com historic: found an unknown data structure type [{dataType}]!");
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

			cumulus.LogDebugMessage("WL Health - found health data for WLL device");

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
					wllVoltageLow = true;
					cumulus.LogWarningMessage($"WLL WARNING: Backup battery voltage is low = {battV:0.##}V");
				}
				else
				{
					wllVoltageLow = false;
					cumulus.LogDebugMessage($"WLL Battery Voltage = {battV:0.##}V");
				}
				var inpV = data.input_voltage / 1000.0;
				ConSupplyVoltageText = inpV.ToString("F2");
				if (inpV < 4.0)
				{
					cumulus.LogWarningMessage($"WLL WARNING: Input voltage is low = {inpV:0.##}V");
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
				cumulus.LogDebugMessage("WLL Uptime = " + uptStr);

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
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"WL.com historic: Error processing WLL health. Error: {ex.Message}");
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

		private void DecodeISSHealth(WlHistorySensor sensor)
		{
			/* ISS & Non-ISS have the same health fields
				* Available fields of interest to health
				* "afc": -1
				* "error_packets": 0
				* "good_packets_streak": 602
				* "reception": 100
				* "resynchs": 0
				* "rssi": -60
				* "supercap_volt_last": null
				* "trans_battery_flag": 0
				* "trans_battery": null
				* "tx_id": 2
				*/

			try
			{
				string type;
				if (sensor.sensor_type == 37 || sensor.sensor_type == 84 || sensor.sensor_type == 85)
					type = "Vue";
				else
					type = sensor.data_structure_type == 11 ? "ISS" : "Soil-Leaf/Temperature";

				var data = sensor.data.Last().FromJsv<WlHealthDataType11_13>();

				cumulus.LogDebugMessage($"XMTR Health - found health data for {type} device TxId = {data.tx_id}");

				// Check battery state 0=Good, 1=Low
				SetTxBatteryStatus(data.tx_id, data.trans_battery_flag);
				if (data.trans_battery_flag == 1)
				{
					cumulus.LogWarningMessage($"XMTR WARNING: Battery voltage is low in TxId {data.tx_id}");
				}
				else
				{
					cumulus.LogDebugMessage($"XMTR Health: {type} {data.tx_id}: Battery state is OK");
				}

				//DavisTotalPacketsReceived[txid] = ;  // Do not have a value for this
				DavisTotalPacketsMissed[data.tx_id] = data.error_packets;
				DavisNumCRCerrors[data.tx_id] = data.error_packets;
				DavisNumberOfResynchs[data.tx_id] = data.resynchs;
				DavisMaxInARow[data.tx_id] = data.good_packets_streak;
				DavisReceptionPct[data.tx_id] = data.reception;
				DavisTxRssi[data.tx_id] = data.rssi;

				var logMsg = $"XMTR Health: {type} {data.tx_id}: Errors={DavisTotalPacketsMissed[data.tx_id]}, CRCs={DavisNumCRCerrors[data.tx_id]}, Resyncs={DavisNumberOfResynchs[data.tx_id]}, Streak={DavisMaxInARow[data.tx_id]}, %={DavisReceptionPct[data.tx_id]}, RSSI={DavisTxRssi[data.tx_id]}";
				logMsg += data.supercap_volt_last != null ? $", Supercap={data.supercap_volt_last:F2}V" : "";
				logMsg += data.solar_volt_last != null ? $", Supercap={data.solar_volt_last:F2}V" : "";
				cumulus.LogDebugMessage(logMsg);

				// Is there any ET in this record?
				if (sensor.data_structure_type == 11 && data.et != null)
				{
					// wl.com ET is only available in record the start of each hour.
					// The number is the total for the one hour period.
					// This is unlike the existing VP2 when the ET is an annual running total
					// So we try and mimic the VP behaviour
					var newET = AnnualETTotal + ConvertUnits.RainINToUser((double) data.et);
					cumulus.LogDebugMessage($"XMTR Health: Adding {ConvertUnits.RainINToUser((double) data.et):F3} to ET");
					DoET(newET, DateTime.Now);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"XMTR Health: Error processing transmitter health. Error: {ex.Message}");
			}
		}

		/*
		private void HealthTimerTick(object source, ElapsedEventArgs e)
		{
			// Only run every 15 minutes
			// We run at :01, :16, :31, :46 to allow time for wl.com to generate the stats
			if (DateTime.Now.Minute % 15 == 1)
			{
				GetWlHistoricHealth();
			}
		}
		*/

		// Extracts health information from the last archive record
		private void GetWlHistoricHealth()
		{
			cumulus.LogMessage("WL Health: Get WL.com Historic Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("WL Health: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				const string msg = "No WeatherLink API station ID in the cumulus.ini file";
				Cumulus.LogConsoleMessage("GetWlHistoricHealth: " + msg);
				cumulus.LogWarningMessage($"WL Health: {msg}, aborting!");
				return;
			}

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - weatherLinkArchiveInterval;
			long endTime = unixDateTime;

			cumulus.LogDebugMessage($"WL Health: Downloading the historic record from WL.com from: {Utils.FromUnixTime(startTime):s} to: {Utils.FromUnixTime(endTime):s}");

			StringBuilder historicUrl = new StringBuilder("https://api.weatherlink.com/v2/historic/" + cumulus.WllStationId);
			historicUrl.Append("?api-key=" + cumulus.WllApiKey);
			historicUrl.Append("&start-timestamp=" + startTime.ToString());
			historicUrl.Append("&end-timestamp=" + endTime.ToString());

			cumulus.LogDebugMessage($"WL Health: WeatherLink URL = {historicUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");

			try
			{
				WlHistory histObj;
				string responseBody;
				int responseCode;

				var request = new HttpRequestMessage(HttpMethod.Get, historicUrl.ToString());
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// we want to do this synchronously, so .Result
				using (var response = Cumulus.MyHttpClient.SendAsync(request).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"WL Health: WeatherLink API Response code: {responseCode}");
					cumulus.LogDataMessage($"WL Health: WeatherLink API Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogWarningMessage($"WL Health: WeatherLink API Error: {errObj.code}, {errObj.message}");
					// Get wl.com status
					GetSystemStatus();
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("WL Health: WeatherLink API: No data was returned. Check your Device Id.");
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					// Get wl.com status
					GetSystemStatus();
					return;
				}

				if (!responseBody.StartsWith("{\"")) // basic sanity check
				{
					// No idea what we got, dump it to the log
					cumulus.LogErrorMessage("WL Health: Invalid historic message received");
					cumulus.LogDataMessage("WL Health: Received: " + responseBody);
					return;
				}

				histObj = responseBody.FromJson<WlHistory>();

				// get the sensor data
				if (histObj.sensors.Count == 0)
				{
					cumulus.LogMessage("WL Health: No historic data available");
					return;
				}
				else
				{
					cumulus.LogDebugMessage($"WL Health: Found {histObj.sensors.Count} sensor records to process");
				}


				try
				{
					// Sensor types we are interested in...
					// 504 = WLL Health - Now in current
					// 506 = AirLink Health - Now in current

					// Get the LSID of the health station associated with each device
					//var wllHealthLsid = GetWlHistoricHealthLsid(cumulus.WllParentId, 504);
					var alInHealthLsid = GetWlHistoricHealthLsid(cumulus.airLinkInLsid, 506);
					var alOutHealthLsid = GetWlHistoricHealthLsid(cumulus.airLinkOutLsid, 506);

					foreach (var sensor in histObj.sensors)
					{
						var sensorType = sensor.sensor_type;
						var dataStructureType = sensor.data_structure_type;
						var lsid = sensor.lsid;

						if (sensorType >= 23 && sensorType < 100)
						{
							DecodeISSHealth(sensor);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("WLL Health: exception: " + ex.Message);
				}
				cumulus.BatteryLowAlarm.Triggered = TxBatText.Contains("LOW") || wllVoltageLow;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("WLL Health: exception: " + ex.Message);
			}

		}

		// Finds all stations associated with this API
		// Return true if only 1 result is found, else return false
		private void GetAvailableStationIds(bool logToConsole = false)
		{
			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogWarningMessage("WLLStations: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			var stationsUrl = "https://api.weatherlink.com/v2/stations?api-key=" + cumulus.WllApiKey;

			cumulus.LogDebugMessage($"WLLStations: URL = {stationsUrl.ToString().Replace(cumulus.WllApiKey, "API_KEY")}");

			try
			{
				string responseBody;
				int responseCode;

				var request = new HttpRequestMessage(HttpMethod.Get, stationsUrl.ToString());
				request.Headers.Add("X-Api-Secret", cumulus.WllApiSecret);

				// We want to do this synchronously
				using (var response = Cumulus.MyHttpClient.SendAsync(request).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					var resp = System.Text.RegularExpressions.Regex.Replace(responseBody, "user_email\":\"[^\"]*\"", "user_email\":\"<<email>>\"");
					cumulus.LogDebugMessage($"WLLStations: WeatherLink API Response: {responseCode}: {resp}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogErrorMessage($"WLLStations: WeatherLink API Error: {errObj.code} - {errObj.message}");
					return;
				}

				var stationsObj = responseBody.FromJson<WlStationList>();

				foreach (var station in stationsObj.stations)
				{
					cumulus.LogMessage($"WLLStations: Found WeatherLink station id = {station.station_id}, name = {station.station_name}");
					if (stationsObj.stations.Count > 1 && logToConsole)
					{
						Cumulus.LogConsoleMessage($" - Found WeatherLink station id = {station.station_id}, name = {station.station_name}, active = {station.active}");
					}
					if (station.station_id == cumulus.WllStationId)
					{
						cumulus.LogDebugMessage($"WLLStations: Setting WLL parent ID = {station.gateway_id}");
						cumulus.WllParentId = station.gateway_id;

						if (station.recording_interval != cumulus.logints[cumulus.DataLogInterval])
						{
							cumulus.LogWarningMessage($"WLLStations: - Cumulus log interval {cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station.recording_interval}");
						}

						wlStationArchiveInterval = station.recording_interval;
						DataTimeoutMins = wlStationArchiveInterval + 3;
					}
				}
				if (stationsObj.stations.Count > 1 && cumulus.WllStationId < 10)
				{
					if (logToConsole)
						Cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your WLL configuration to enable history downloads.");
				}
				else if (stationsObj.stations.Count == 1 && cumulus.WllStationId != stationsObj.stations[0].station_id)
				{
					cumulus.LogMessage($"WLLStations: Only found 1 WeatherLink station, using id = {stationsObj.stations[0].station_id}");
					cumulus.WllStationId = stationsObj.stations[0].station_id;
					// And save it to the config file
					cumulus.WriteIniFile();

					cumulus.LogDebugMessage($"WLLStations: Setting WLL parent ID = {stationsObj.stations[0].gateway_id}");
					cumulus.WllParentId = stationsObj.stations[0].gateway_id;
					wlStationArchiveInterval = stationsObj.stations[0].recording_interval;
					return;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("WLLStations: WeatherLink API exception: " + ex.Message);
			}
			return;
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
				using (var response = Cumulus.MyHttpClient.SendAsync(request).Result)
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
				cumulus.LogDebugMessage("GetAvailableSensors: WeatherLink API exception: " + ex.Message);
				return;
			}

			sensorList = sensorsObj.sensors;

			foreach (var sensor in sensorList)
			{
				try
				{
					cumulus.LogDebugMessage($"GetAvailableSensors: Found WeatherLink Sensor type={sensor.sensor_type}, lsid={sensor.lsid}, station_id={sensor.station_id}, name={sensor.product_name}, parentId={sensor.parent_device_id}, parent={sensor.parent_device_name}");

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
					cumulus.LogDebugMessage("GetAvailableSensors: Processing sensors exception: " + ex.Message);
				}
			}

			return;
		}

		private int GetWlHistoricHealthLsid(int id, int type)
		{
			try
			{
				var sensor = sensorList.FirstOrDefault(i => i.lsid == id || i.parent_device_id == id);
				if (sensor != null)
				{
					var health = sensorList.FirstOrDefault(i => i.parent_device_id == sensor.parent_device_id && i.sensor_type == type);
					if (health != null)
					{
						return health.lsid;
					}
				}
			}
			catch
			{ }
			return 0;
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
				using (var response = Cumulus.MyHttpClient.GetAsync("https://0886445102835570.hostedstatus.com/1.0/status/600712dea9c1290530967bc6").Result)
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

			return;
		}

		private void CheckLoggingDataStopped(int mins)
		{
			if (mins != wlStationArchiveInterval || DataTimeoutMins != mins + 3)
			{
				wlStationArchiveInterval = mins;
				DataTimeoutMins = wlStationArchiveInterval + 3;
			}
		}
	}
}
