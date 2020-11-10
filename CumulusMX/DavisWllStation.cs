using ServiceStack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Tmds.MDns;
using Unosquare.Swan;

namespace CumulusMX
{
	internal class DavisWllStation : WeatherStation
	{
		private string ipaddr;
		private int port;
		private int duration;
		private readonly System.Timers.Timer tmrRealtime;
		private readonly System.Timers.Timer tmrCurrent;
		private readonly System.Timers.Timer tmrBroadcastWatchdog;
		private readonly System.Timers.Timer tmrHealth;
		private readonly object threadSafer = new object();
		private static readonly SemaphoreSlim WebReq = new SemaphoreSlim(1);
		private bool startupDayResetIfRequired = true;
		private bool savedUseSpeedForAvgCalc;
		private bool savedCalculatePeakGust;
		private int maxArchiveRuns = 1;
		private static readonly HttpClientHandler HistoricHttpHandler = new HttpClientHandler();
		private readonly HttpClient wlHttpClient = new HttpClient(HistoricHttpHandler);
		private readonly HttpClient dogsBodyClient = new HttpClient();
		private readonly bool checkWllGustValues;
		private bool broadcastReceived;
		private int weatherLinkArchiveInterval = 16 * 60; // Used to get historic Health, 16 minutes in seconds only for initial fetch after load
		private bool wllVoltageLow;
		private bool stop;
		private readonly List<WlSensor> sensorList = new List<WlSensor>();
		private readonly bool useWeatherLinkDotCom = true;

		public DavisWllStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = cumulus.DAVIS;
			calculaterainrate = false;
			//cumulus.UseDataLogger = false;
			// WLL does not provide a forecast string, so use the Cumulus forecast
			cumulus.UseCumulusForecast = true;
			// initialise the battery status
			TxBatText = "1-NA 2-NA 3-NA 4-NA 5-NA 6-NA 7-NA 8-NA";

			cumulus.LogMessage("Station type = Davis WLL");

			tmrRealtime = new System.Timers.Timer();
			tmrCurrent = new System.Timers.Timer();
			tmrBroadcastWatchdog = new System.Timers.Timer();
			tmrHealth = new System.Timers.Timer();

			wlHttpClient.Timeout = TimeSpan.FromSeconds(20); // 20 seconds for internet queries

			// used for kicking real time, and getting current conditions
			dogsBodyClient.Timeout = TimeSpan.FromSeconds(10); // 10 seconds for local queries
			dogsBodyClient.DefaultRequestHeaders.Add("Connection", "close");

			// If the user is using the default 10 minute Wind gust, always use gust data from the WLL - simple
			if (cumulus.PeakGustMinutes == 10)
			{
				CalcRecentMaxGust = false;
				checkWllGustValues = false;
			}
			else if (cumulus.PeakGustMinutes > 10)
			{
				// If the user period is greater that 10 minutes, then Cumulus must calculate Gust values
				// but we can check the WLL 10 min gust value in case we missed a gust
				CalcRecentMaxGust = true;
				checkWllGustValues = true;
			}
			else
			{
				// User period is less than 10 minutes, so we cannot use the station 10 min gust values
				CalcRecentMaxGust = true;
				checkWllGustValues = false;
			}


			// Sanity check - do we have all the info we need?
			if (string.IsNullOrEmpty(cumulus.WllApiKey) && string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// The basic API details have not been supplied
				cumulus.LogMessage("WLL - No WeatherLink.com API configuration supplied, just going to work locally");
				cumulus.LogMessage("WLL - Cannot start historic downloads or retrieve health data");
				cumulus.LogConsoleMessage("*** No WeatherLink.com API details supplied. Cannot start historic downloads or retrieve health data");
				useWeatherLinkDotCom = false;
			}
			else if (string.IsNullOrEmpty(cumulus.WllApiKey) || string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// One of the API details is missing
				if (string.IsNullOrEmpty(cumulus.WllApiKey))
				{
					cumulus.LogMessage("WLL - Missing WeatherLink.com API Key");
					cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Key. Cannot start historic downloads or retrieve health data");
				}
				else
				{
					cumulus.LogMessage("WLL - Missing WeatherLink.com API Secret");
					cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Secret. Cannot start historic downloads or retrieve health data");
				}
				useWeatherLinkDotCom = false;
			}


			// Perform Station ID checks - If we have API deatils!
			// If the Station ID is missing, this will populate it if the user only has one station associated with the API key
			if (useWeatherLinkDotCom && (string.IsNullOrEmpty(cumulus.WllStationId) || int.Parse(cumulus.WllStationId) < 10))
			{
				var msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage(msg);

				GetAvailableStationIds(true);
			}
			else if (useWeatherLinkDotCom)
			{
				GetAvailableStationIds(false);
			}

			// Sanity check the station id
			if (useWeatherLinkDotCom && (string.IsNullOrEmpty(cumulus.WllStationId) || int.Parse(cumulus.WllStationId) < 10))
			{
				// API details supplied, but Station Id is still invalid - do not start the station up.
				cumulus.LogMessage("WLL - The WeatherLink.com API is enabled, but no Station Id has been configured, not starting the station. Please correct this and restart Cumulus");
				cumulus.LogConsoleMessage("The WeatherLink.com API is enabled, but no Station Id has been configured. Please correct this and restart Cumulus");
				return;
			}


			// Now get the sensors associated with this station
			if (useWeatherLinkDotCom)
				GetAvailableSensors();

			// Perform zero-config
			// If it works - check IP address in config file and set/update if required
			// If it fails - just use the IP address from config file

			const string serviceType = "_weatherlinklive._tcp";
			var serviceBrowser = new ServiceBrowser();
			serviceBrowser.ServiceAdded += OnServiceAdded;
			serviceBrowser.ServiceRemoved += OnServiceRemoved;
			serviceBrowser.ServiceChanged += OnServiceChanged;
			serviceBrowser.QueryParameters.QueryInterval = cumulus.WllBroadcastDuration * 1000 * 4; // query at 4x the multicast time (default 20 mins)

			//Console.WriteLine($"Browsing for type: {serviceType}");
			serviceBrowser.StartBrowse(serviceType);

			cumulus.LogMessage("Attempting to find WLL via zero-config...");

			// short wait for zero-config
			Thread.Sleep(1000);

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
				cumulus.LogDebugMessage("Lock: Station waiting for lock");
				Cumulus.syncInit.Wait();
				cumulus.LogDebugMessage("Lock: Station has the lock");

				// Create a realtime thread to periodically restart broadcasts
				GetWllRealtime(null, null);
				tmrRealtime.Elapsed += GetWllRealtime;
				tmrRealtime.Interval = cumulus.WllBroadcastDuration * 1000 / 3 * 2; // give the multicasts a kick after 2/3 of the duration (default 200 secs)
				tmrRealtime.AutoReset = true;
				tmrRealtime.Start();

				// Create a current conditions thread to poll readings every 30 seconds
				GetWllCurrent(null, null);
				tmrCurrent.Elapsed += GetWllCurrent;
				tmrCurrent.Interval = 30 * 1000;  // Every 30 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();

				if (useWeatherLinkDotCom)
				{
					// Get the archive data health to do the initial value populations
					GetWlHistoricHealth();
					// And reset the fetch interval to 2 minutes
					weatherLinkArchiveInterval = 2 * 60;
				}

				// short wait for realtime response
				Thread.Sleep(1200);

				if (port == 0)
				{
					cumulus.LogMessage("WLL failed to get broadcast port via realtime request, defaulting to 22222");
					port = 22222;
				}
				// Create a broadcast listener
				Task.Run(() =>
				{
					using (var udpClient = new UdpClient())
					{
						udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
						udpClient.Client.ReceiveTimeout = 4000;  // We should get a message every 2.5 seconds
						var from = new IPEndPoint(0, 0);

						while (!stop)
						{
							try
							{
								if (!stop) // we may be waiting for a broadcast when a shutdown is started
								{
									DecodeBroadcast(Encoding.UTF8.GetString(udpClient.Receive(ref from)));
								}
							}
							catch (SocketException exp)
							{
								if (exp.SocketErrorCode == SocketError.TimedOut)
								{
									multicastsBad++;
									var msg = string.Format("WLL: Missed a WLL broadcast message. Percentage good packets {0:F2}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100), multicastsBad, multicastsGood);
									cumulus.LogDebugMessage(msg);
								}
								else
								{
									cumulus.LogMessage($"WLL: UDP socket exception: {exp.Message}");
								}
							}
						}
						udpClient.Close();
						cumulus.LogMessage("WLL broadcast listener stopped");
					}
				});

				cumulus.LogMessage($"WLL Now listening on broadcast port {port}");

				// Start a broadcast watchdog to warn if WLL broadcast messages are not being received
				tmrBroadcastWatchdog.Elapsed += BroadcastTimeout;
				tmrBroadcastWatchdog.Interval = 1000 * 30; // timeout after 30 seconds
				tmrBroadcastWatchdog.AutoReset = true;
				tmrBroadcastWatchdog.Start();

				if (useWeatherLinkDotCom)
				{
					// get the health data every 15 minutes
					tmrHealth.Elapsed += HealthTimerTick;
					tmrHealth.Interval = 60 * 1000;  // Tick every minute
					tmrHealth.AutoReset = true;
					tmrHealth.Start();
				}
			}
			catch (ThreadAbortException)
			{
			}
			finally
			{
				cumulus.LogDebugMessage("Lock: Station releasing lock");
				Cumulus.syncInit.Release();
			}
		}

		public override void Stop()
		{
			cumulus.LogMessage("Closing WLL connections");
			try
			{
				stop = true;
				tmrRealtime.Stop();
				tmrCurrent.Stop();
				tmrBroadcastWatchdog.Stop();
				tmrHealth.Stop();
				StopMinuteTimer();
			}
			catch
			{
				cumulus.LogMessage("Error stopping station timers");
			}
		}

		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
		}

		private async void GetWllRealtime(object source, ElapsedEventArgs e)
		{
			var retry = 2;

			cumulus.LogDebugMessage("GetWllRealtime: GetWllRealtime waiting for lock");
			WebReq.Wait();
			cumulus.LogDebugMessage("GetWllRealtime: GetWllRealtime has the lock");

			// The WLL will error if already responding to a request from another device, so add a retry
			do
			{
				// Call asynchronous network methods in a try/catch block to handle exceptions
				try
				{
					string ip;

					lock (threadSafer)
					{
						ip = cumulus.VP2IPAddr;
					}

					if (CheckIpValid(ip))
					{
						var urlRealtime = "http://" + ip + "/v1/real_time?duration=" + cumulus.WllBroadcastDuration;

						cumulus.LogDebugMessage($"GetWllRealtime: Sending GET realtime request to WLL: {urlRealtime} ...");

						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlRealtime))
						{
							var responseBody = await response.Content.ReadAsStringAsync();
							responseBody = responseBody.TrimEnd('\r', '\n');

							cumulus.LogDataMessage("GetWllRealtime: WLL response: " + responseBody);

							var respJson = responseBody.FromJson<WllBroadcastReqResponse>();
							var err = string.IsNullOrEmpty(respJson.error) ? "OK" : respJson.error;
							port = respJson.data.broadcast_port;
							duration = respJson.data.duration;
							cumulus.LogDebugMessage($"GetWllRealtime: GET response Code: {err}, Port: {port}");
							if (cumulus.WllBroadcastDuration != duration)
							{
								cumulus.LogMessage($"GetWllRealtime: WLL broadcast duration {duration} does not match requested duration {cumulus.WllBroadcastDuration}, continuing to use {cumulus.WllBroadcastDuration}");
							}
							if (cumulus.WllBroadcastPort != port)
							{
								cumulus.LogMessage($"GetWllRealtime: WLL broadcast port {port} does not match default {cumulus.WllBroadcastPort}, resetting to {port}");
								cumulus.WllBroadcastPort = port;
							}
						}
					}
					else
					{
						cumulus.LogMessage($"GetWllRealtime: Invalid IP address: {ip}");
					}
					retry = 0;
				}
				catch (Exception exp)
				{
					retry--;
					cumulus.LogDebugMessage("GetRealtime: Exception Caught!");
					cumulus.LogDebugMessage($"GetWllRealtime: Message :{exp.Message}");
					Thread.Sleep(2000);
				}
			} while (retry > 0);

			cumulus.LogDebugMessage("GetWllRealtime: Releasing lock");
			WebReq.Release();
		}

		private async void GetWllCurrent(object source, ElapsedEventArgs e)
		{
			string ip;
			int retry = 1;

			lock (threadSafer)
			{
				ip = cumulus.VP2IPAddr;
			}

			if (CheckIpValid(ip))
			{

				var urlCurrent = $"http://{ip}/v1/current_conditions";

				cumulus.LogDebugMessage("GetWllCurrent: Waiting for lock");
				WebReq.Wait();
				cumulus.LogDebugMessage("GetWllCurrent: Has the lock");

				// The WLL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogDebugMessage($"GetWllCurrent: Sending GET current conditions request {retry} to WLL: {urlCurrent} ...");
					try
					{
						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlCurrent))
						{
							response.EnsureSuccessStatusCode();
							var responseBody = await response.Content.ReadAsStringAsync();
							cumulus.LogDataMessage($"GetWllCurrent: response - {responseBody}");

							try
							{
								DecodeCurrent(responseBody);
								if (startupDayResetIfRequired)
								{
									DoDayResetIfNeeded();
									startupDayResetIfRequired = false;
								}
							}
							catch (Exception ex)
							{
								cumulus.LogMessage("GetWllCurrent: Error processing WLL response");
								cumulus.LogMessage($"GetWllCurrent: Error: {ex.Message}");
							}
							retry = 9;
						}
					}
					catch (Exception exp)
					{
						retry++;
						cumulus.LogDebugMessage("GetWllCurrent(): Exception Caught!");
						cumulus.LogDebugMessage("Message: " + exp.Message);
						Thread.Sleep(1000);
					}
				} while (retry < 3);

				cumulus.LogDebugMessage("GetWllCurrent: Releasing lock");
				WebReq.Release();
			}
			else
			{
				cumulus.LogMessage($"GetWllCurrent: Invalid IP address: {ip}");
			}
		}

		private void DecodeBroadcast(string broadcastJson)
		{
			try
			{
				cumulus.LogDataMessage("WLL Broadcast: " + broadcastJson);

				// sanity check
				if (broadcastJson.StartsWith("{\"did\":"))
				{
					var json = broadcastJson.FromJson<WllBroadcast>();

					// The WLL sends the timestamp in Unix ticks, and in UTC
					// rather than rely on the WLL clock being correct, we will use our local time
					var dateTime = DateTime.Now;
					foreach (var rec in json.conditions)
					{
						// Wind - All values in MPH
						/* Available fields:
						 * rec["wind_speed_last"]
						 * rec["wind_dir_last"]
						 * rec["wind_speed_hi_last_10_min"]
						 * rec["wind_dir_at_hi_speed_last_10_min"]
						 */
						if (cumulus.WllPrimaryWind == rec.txid)
						{
							try
							{
								// WLL BUG/FEATURE: The WLL sends a null wind direction for calm when the avg speed falls to zero, we use zero
								int windDir = rec.wind_dir_last ?? 0;

								// No average in the broadcast data, so use last value from current - allow for calibration
								DoWind(ConvertWindMPHToUser(rec.wind_speed_last), windDir, WindAverage / cumulus.Calib.WindSpeed.Mult, dateTime);

								var gust = ConvertWindMPHToUser(rec.wind_speed_hi_last_10_min);
								var gustCal = gust * cumulus.Calib.WindGust.Mult;
								if (checkWllGustValues)
								{
									if (gustCal > RecentMaxGust)
									{
										// See if the station 10 min high speed is higher than our current 10-min max
										// ie we missed the high gust

										// Check for spikes, and set highs
										if (CheckHighGust(gustCal, rec.wind_dir_at_hi_speed_last_10_min, dateTime))
										{
											cumulus.LogDebugMessage("Set max gust from broadcast 10 min high value: " + gustCal.ToString(cumulus.WindFormat) + " was: " + RecentMaxGust.ToString(cumulus.WindFormat));

											// add to recent values so normal calculation includes this value
											WindRecent[nextwind].Gust = gust; // use uncalibrated value
											WindRecent[nextwind].Speed = WindAverage / cumulus.Calib.WindSpeed.Mult;
											WindRecent[nextwind].Timestamp = dateTime;
											nextwind = (nextwind + 1) % MaxWindRecent;

											RecentMaxGust = gustCal;
										}
									}
								}
								else if (!CalcRecentMaxGust)
								{
									// Check for spikes, and set highs
									if (CheckHighGust(gustCal, rec.wind_dir_at_hi_speed_last_10_min, dateTime))
									{
										RecentMaxGust = gustCal;
									}
								}
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WLL broadcast: Error in wind speed found on TxId {rec.txid}");
								cumulus.LogDebugMessage($"WLL broadcast: Exception: {ex.Message}");
							}
						}

						// Rain
						/*
						 * All fields are *tip counts*
						 * Available fields:
						 * rec["rain_size"] - 0: Reseverved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
						 * rec["rain_rate_last"]
						 * rec["rain_15_min"]
						 * rec["rain_60_min"]
						 * rec["rain_24_hr"]
						 * rec["rain_storm"]
						 * rec["rain_storm_start_at"]
						 * rec["rainfall_daily"]
						 * rec["rainfall_monthly"]
						 * rec["rainfall_year"])
						 */
						if (cumulus.WllPrimaryRain != rec.txid) continue;

						try
						{
							var rain = ConvertRainClicksToUser(rec.rainfall_year, rec.rain_size);
							var rainrate = ConvertRainClicksToUser(rec.rain_rate_last, rec.rain_size);

							if (rainrate < 0)
							{
								rainrate = 0;
							}

							DoRain(rain, rainrate, dateTime);
						}
						catch (Exception ex)
						{
							cumulus.LogMessage($"WLL broadcast: no valid rainfall found on TxId {rec.txid}");
							cumulus.LogDebugMessage($"WLL broadcast: Exception: {ex.Message}");
						}
					}

					UpdateStatusPanel(DateTime.Now);
					UpdateMQTT();

					broadcastReceived = true;
					DataStopped = false;
					cumulus.DataStoppedAlarm.Triggered = false;
					multicastsGood++;
				}
				else
				{
					multicastsBad++;
					var msg = string.Format("WLL broadcast: Invalid payload in message. Percentage good packets {0:F2}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100), multicastsBad, multicastsGood);
					cumulus.LogMessage(msg);
				}
			}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeBroadcast(): Exception Caught!");
				cumulus.LogDebugMessage("Message :" + exp.Message);
				multicastsBad++;
				var msg = string.Format("WLL broadcast: Error processing broadcast. Percentage good packets {0:F2}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100), multicastsBad, multicastsGood);
				cumulus.LogMessage(msg);
			}
		}

		private void DecodeCurrent(string currentJson)
		{
			try
			{
				// Convert JSON string to an object
				WllCurrent json = currentJson.FromJson<WllCurrent>();

				// The WLL sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the WLL clock being correct, we will use our local time
				//var dateTime = FromUnixTime(data.Value<int>("ts"));
				var dateTime = DateTime.Now;
				var localSensorContactLost = false;

				foreach (var rec in json.data.conditions)
				{
					// Yuck, we have to find the data type in the string, then we know how to decode it to the correct object type
					int start = rec.IndexOf("data_structure_type:") + "data_structure_type:".Length;
					int end = rec.IndexOf(",", start);

					int type = int.Parse(rec.Substring(start, end - start));
					string idx = "";

					switch (type)
					{
						case 1: // ISS
							var data1 = rec.FromJsv<WllCurrentType1>();

							cumulus.LogDebugMessage($"WLL current: found ISS data on TxId {data1.txid}");

							// Battery
							SetTxBatteryStatus(data1.txid, data1.trans_battery_flag);

							if (data1.rx_state == 2)
							{
								localSensorContactLost = true;
								cumulus.LogMessage($"Warning: Sensor contact lost TxId {data1.txid}; ignoring data from this ISS");
								continue;
							}


							// Temperature & Humidity
							if (cumulus.WllPrimaryTempHum == data1.txid)
							{
								/* Available fields
									* "temp": 62.7,                                  // most recent valid temperature **(°F)**
									* "hum":1.1,                                     // most recent valid humidity **(%RH)**
									* "dew_point": -0.3,                             // **(°F)**
									* "wet_bulb":null,                               // **(°F)**
									* "heat_index": 5.5,                             // **(°F)**
									* "wind_chill": 6.0,                             // **(°F)**
									* "thw_index": 5.5,                              // **(°F)**
									* "thsw_index": 5.5,                             // **(°F)**
									*/

								try
								{
									cumulus.LogDebugMessage($"WLL current: using temp/hum data from TxId {data1.txid}");

									DoOutdoorHumidity(Convert.ToInt32(data1.hum), dateTime);

									DoOutdoorTemp(ConvertTempFToUser(data1.temp), dateTime);

									DoOutdoorDewpoint(ConvertTempFToUser(data1.dew_point), dateTime);

									if (!cumulus.CalculatedWC)
									{
										// use wind chill from WLL
										DoWindChill(ConvertTempFToUser(data1.wind_chill), dateTime);
									}

									//TODO: Wet Bulb? rec["wet_bulb"] - No, we already have humidity
									//TODO: Heat Index? rec["heat_index"] - No, Cumulus always calculates HI
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing temperature values on TxId {data1.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							else
							{   // Check for Extra temperature/humidity settings
								for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
								{
									if (cumulus.WllExtraTempTx[tempTxId - 1] != data1.txid) continue;

									try
									{
										if (cumulus.WllExtraTempTx[tempTxId - 1] == data1.txid)
										{
											if (data1.temp == -99)
											{
												cumulus.LogDebugMessage($"WLL current: no valid Extra temperature value found [{data1.temp}] on TxId {data1.txid}");
											}
											else
											{
												cumulus.LogDebugMessage($"WLL current: using extra temp data from TxId {data1.txid}");

												DoExtraTemp(ConvertTempFToUser(data1.temp), tempTxId);
											}

											if (cumulus.WllExtraHumTx[tempTxId - 1])
											{
												DoExtraHum(data1.hum, tempTxId);
											}
										}
									}
									catch (Exception ex)
									{
										cumulus.LogMessage($"WLL current: Error processing Extra temperature/humidity values on TxId {data1.txid}");
										cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
									}
								}
							}

							// Wind
							if (cumulus.WllPrimaryWind == data1.txid)
							{
								/*
									* Available fields
									* "wind_speed_last":2,                           // most recent valid wind speed **(mph)**
									* "wind_dir_last":null,                          // most recent valid wind direction **(°degree)**
									* "wind_speed_avg_last_1_min":4                  // average wind speed over last 1 min **(mph)**
									* "wind_dir_scalar_avg_last_1_min":15            // scalar average wind direction over last 1 min **(°degree)**
									* "wind_speed_avg_last_2_min":42606,             // average wind speed over last 2 min **(mph)**
									* "wind_dir_scalar_avg_last_2_min": 170.7,       // scalar average wind direction over last 2 min **(°degree)**
									* "wind_speed_hi_last_2_min":8,                  // maximum wind speed over last 2 min **(mph)**
									* "wind_dir_at_hi_speed_last_2_min":0.0,         // gust wind direction over last 2 min **(°degree)**
									* "wind_speed_avg_last_10_min":42606,            // average wind speed over last 10 min **(mph)**
									* "wind_dir_scalar_avg_last_10_min": 4822.5,     // scalar average wind direction over last 10 min **(°degree)**
									* "wind_speed_hi_last_10_min":8,                 // maximum wind speed over last 10 min **(mph)**
									* "wind_dir_at_hi_speed_last_10_min":0.0,        // gust wind direction over last 10 min **(°degree)**
								*/
								try
								{
									cumulus.LogDebugMessage($"WLL current: using wind data from TxId {data1.txid}");

									// pesky null values from WLL when it is calm
									int wdir = data1.wind_dir_last ?? 0;
									double wind = data1.wind_speed_last ?? 0;
									double wspdAvg10min = ConvertWindMPHToUser(data1.wind_speed_avg_last_10_min ?? 0);

									DoWind(ConvertWindMPHToUser(wind), wdir, wspdAvg10min, dateTime);

									WindAverage = wspdAvg10min * cumulus.Calib.WindSpeed.Mult;

									// Wind data can be a bit out of date compared to the broadcasts (1 minute update), so only use gust broadcast data
									/*
									var gust = ConvertWindMPHToUser(data1.wind_speed_hi_last_10_min);
									var gustCal = gust * cumulus.Calib.WindGust.Mult;

									if (checkWllGustValues)
									{
										// See if the current speed is higher than the current 10-min max
										// We can then update the figure before the next data packet is read

										if (gustCal > RecentMaxGust)
										{
											// Check for spikes, and set highs
											if (CheckHighGust(gustCal, data1.wind_dir_at_hi_speed_last_10_min, dateTime))
											{
												cumulus.LogDebugMessage("Setting max gust from current 10 min value: " + gustCal.ToString(cumulus.WindFormat) + " was: " + RecentMaxGust.ToString(cumulus.WindFormat));

												// add to recent values so normal calculation includes this value
												WindRecent[nextwind].Gust = gust; // use uncalibrated value
												WindRecent[nextwind].Speed = WindAverage / cumulus.Calib.WindSpeed.Mult;
												WindRecent[nextwind].Timestamp = dateTime;
												nextwind = (nextwind + 1) % cumulus.MaxWindRecent;

												RecentMaxGust = gustCal;
											}
										}
									}
									else if (!CalcRecentMaxGust)
									{
										// Check for spikes, and set highs
										if (CheckHighGust(gustCal, data1.wind_dir_at_hi_speed_last_10_min, dateTime))
										{
											RecentMaxGust = gustCal;
										}
									}
									*/
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing wind speeds on TxId {data1.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}


							// Rainfall
							if (cumulus.WllPrimaryRain == data1.txid)
							{
								/*
								* Available fields:
								* rec["rain_size"]
								* rec["rain_rate_last"], rec["rain_rate_hi"]
								* rec["rainfall_last_15_min"], rec["rain_rate_hi_last_15_min"]
								* rec["rainfall_last_60_min"]
								* rec["rainfall_last_24_hr"]
								* rec["rainfall_daily"]
								* rec["rainfall_monthly"]
								* rec["rainfall_year"]
								* rec["rain_storm"], rec["rain_storm_start_at"]
								* rec["rain_storm_last"], rec["rain_storm_last_start_at"], rec["rain_storm_last_end_at"]
								*/


								cumulus.LogDebugMessage($"WLL current: using rain data from TxId {data1.txid}");

								// Rain data can be a bit out of date compared to the broadcasts (1 minute update), so only use storm data

								// All rainfall values supplied as *tip counts*
								//double rain = ConvertRainINToUser((double)rec["rainfall_year"]);
								//double rainrate = ConvertRainINToUser((double)rec["rain_rate_last"]);

								//if (rainrate < 0)
								//{
								//	rainrate = 0;
								//}

								//DoRain(rain, rainrate, dateTime);

								if (!data1.rain_storm.HasValue || !data1.rain_storm_start_at.HasValue)
								{
									cumulus.LogDebugMessage("WLL current: No rain storm values present");
								}
								else
								{
									try
									{
										StormRain = ConvertRainClicksToUser(data1.rain_storm.Value, data1.rain_size) * cumulus.Calib.Rain.Mult;
										StartOfStorm = FromUnixTime(data1.rain_storm_start_at.Value);
									}
									catch (Exception ex)
									{
										cumulus.LogMessage($"WLL current: Error processing rain storm values on TxId {data1.txid}");
										cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
									}
								}
							}

							if (cumulus.WllPrimaryUV == data1.txid)
							{
								try
								{
									cumulus.LogDebugMessage($"WLL current: using UV data from TxId {data1.txid}");
									DoUV(data1.uv_index, dateTime);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing UV value on TxId {data1.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}

							if (cumulus.WllPrimarySolar == data1.txid)
							{
								try
								{
									cumulus.LogDebugMessage($"WLL current: using solar data from TxId {data1.txid}");
									DoSolarRad(data1.solar_rad, dateTime);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing Solar value on TxId {data1.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							break;

						case 2: // Leaf/Soil Moisture
							/*
							 * Available fields
							 * "temp_1":null,                                 // most recent valid soil temp slot 1 **(°F)**
							 * "temp_2":null,                                 // most recent valid soil temp slot 2 **(°F)**
							 * "temp_3":null,                                 // most recent valid soil temp slot 3 **(°F)**
							 * "temp_4":null,                                 // most recent valid soil temp slot 4 **(°F)**
							 * "moist_soil_1":null,                           // most recent valid soil moisture slot 1 **(|cb|)**
							 * "moist_soil_2":null,                           // most recent valid soil moisture slot 2 **(|cb|)**
							 * "moist_soil_3":null,                           // most recent valid soil moisture slot 3 **(|cb|)**
							 * "moist_soil_4":null,                           // most recent valid soil moisture slot 4 **(|cb|)**
							 * "wet_leaf_1":null,                             // most recent valid leaf wetness slot 1 **(no unit)**
							 * "wet_leaf_2":null,                             // most recent valid leaf wetness slot 2 **(no unit)**
							 * "rx_state":null,                               // configured radio receiver state **(no unit)**
							 * "trans_battery_flag":null                      // transmitter battery status flag **(no unit)**
							 */

							var data2 = rec.FromJsv<WllCurrentType2>();

							cumulus.LogDebugMessage($"WLL current: found Leaf/Soil data on TxId {data2.txid}");

							// Battery
							SetTxBatteryStatus(data2.txid, data2.trans_battery_flag);

							if (data2.rx_state == 2)
							{
								localSensorContactLost = true;
								cumulus.LogMessage($"Warning: Sensor contact lost TxId {data2.txid}; ignoring data from this Leaf/Soil transmitter");
								continue;
							}

							// For leaf wetness, soil temp/moisture we rely on user configuration, trap any errors

							// Leaf wetness
							try
							{
								if (cumulus.WllExtraLeafTx1 == data2.txid)
								{
									idx = "wet_leaf_" + cumulus.WllExtraLeafIdx1;
									DoLeafWetness((double)data2[idx], 1);
								}
								if (cumulus.WllExtraLeafTx2 == data2.txid)
								{
									idx = "wet_leaf_" + cumulus.WllExtraLeafIdx2;
									DoLeafWetness((double)data2[idx], 2);
								}
							}
							catch (Exception e)
							{
								cumulus.LogMessage($"WLL current: Error processung LeafWetness txid={data2.txid}, idx={idx}");
								cumulus.LogDebugMessage($"WLL current: Exception: {e.Message}");
							}

							// Soil moisture
							if (cumulus.WllExtraSoilMoistureTx1 == data2.txid)
							{
								idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx1;
								try
								{
									DoSoilMoisture((double)data2[idx], 1);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx1} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							if (cumulus.WllExtraSoilMoistureTx2 == data2.txid)
							{
								idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx2;
								try
								{
									DoSoilMoisture((double)data2[idx], 2);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx2} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							if (cumulus.WllExtraSoilMoistureTx3 == data2.txid)
							{
								idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx3;
								try
								{
									DoSoilMoisture((double)data2[idx], 3);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx3} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							if (cumulus.WllExtraSoilMoistureTx4 == data2.txid)
							{
								idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx4;
								try
								{
									DoSoilMoisture((double)data2[idx], 4);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing soil moisture #{cumulus.WllExtraSoilMoistureIdx4} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}

							// SoilTemperature
							if (cumulus.WllExtraSoilTempTx1 == data2.txid)
							{
								idx = "temp_" + cumulus.WllExtraSoilTempIdx1;
								try
								{
									DoSoilTemp(ConvertTempFToUser((double)data2[idx]), 1);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx1} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							if (cumulus.WllExtraSoilTempTx2 == data2.txid)
							{
								idx = "temp_" + cumulus.WllExtraSoilTempIdx2;
								try
								{
									DoSoilTemp(ConvertTempFToUser((double)data2[idx]), 2);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx2} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							if (cumulus.WllExtraSoilTempTx3 == data2.txid)
							{
								idx = "temp_" + cumulus.WllExtraSoilTempIdx3;
								try
								{
									DoSoilTemp(ConvertTempFToUser((double)data2[idx]), 3);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx3} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}
							if (cumulus.WllExtraSoilTempTx4 == data2.txid)
							{
								idx = "temp_" + cumulus.WllExtraSoilTempIdx4;
								try
								{
									DoSoilTemp(ConvertTempFToUser((double)data2[idx]), 4);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing extra soil temp #{cumulus.WllExtraSoilTempIdx4} on TxId {data2.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}

							// TODO: Extra Humidity? No type for this on WLL

							break;

						case 3: // Barometer
							/*
							 * Available fields:
							 * rec["bar_sea_level"]
							 * rec["bar_absolute"]
							 * rec["bar_trend"]
							 */

							cumulus.LogDebugMessage("WLL current: found Baro data");

							try
							{
								var data3 = rec.FromJsv<WllCurrentType3>();

								DoPressure(ConvertPressINHGToUser(data3.bar_sea_level), dateTime);
								DoPressTrend("Pressure trend");
								// Altimeter from absolute
								StationPressure = ConvertPressINHGToUser(data3.bar_absolute);
								// Or do we use calibration? The VP2 code doesn't?
								//StationPressure = ConvertPressINHGToUser(rec.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
								AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(PressureHPa(StationPressure), AltitudeM(cumulus.Altitude)));
							}
							catch (Exception ex)
							{
								cumulus.LogMessage("WLL current: Error processing baro data");
								cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
							}

							break;

						case 4: // WLL Temp/Humidity
							/*
							 * Available fields:
							 * rec["temp_in"]
							 * rec["hum_in"]
							 * rec["dew_point_in"]
							 * rec["heat_index_in"]
							 */

							cumulus.LogDebugMessage("WLL current: found Indoor temp/hum data");

							var data4 = rec.FromJsv<WllCurrentType4>();

							try
							{
								DoIndoorTemp(ConvertTempFToUser(data4.temp_in));
							}
							catch (Exception ex)
							{
								cumulus.LogMessage("WLL current: Error processing indoor temp data");
								cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
							}

							try
							{
								DoIndoorHumidity(Convert.ToInt32(data4.hum_in));
							}
							catch (Exception ex)
							{
								cumulus.LogMessage("WLL current: Error processing indoor humidity data");
								cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
							}

							break;

						default:
							cumulus.LogDebugMessage($"WLL current: found an unknown tramsmitter type [{type}]!");
							break;
					}

					DoForecast(string.Empty, false);

					UpdateStatusPanel(DateTime.Now);
					UpdateMQTT();
				}

				// Now we have the primary data, calculate the derived data
				if (cumulus.CalculatedWC)
				{
					if (ConvertUserWindToMS(WindAverage) < 1.5)
					{
						// wind speed too low, use the temperature
						DoWindChill(OutdoorTemperature, dateTime);
					}
					else
					{
						// calculate wind chill from calibrated C temp and calibrated wind in KPH
						DoWindChill(ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage))), dateTime);
					}
				}

				DoApparentTemp(dateTime);
				DoFeelsLike(dateTime);
				DoHumidex(dateTime);

				SensorContactLost = localSensorContactLost;

				// If the station isn't using the logger function for WLL - i.e. no API key, then only alarm on Tx battery status
				// otherwise, trigger the alarm when we read the Health data which also contains the WLL backup battery status
				if (!cumulus.UseDataLogger)
				{
					cumulus.BatteryLowAlarm.Triggered = TxBatText.Contains("LOW");
				}
			}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeCurrent: Exception Caught!");
				cumulus.LogDebugMessage("Message :" + exp.Message);
			}
		}

		private static DateTime FromUnixTime(long unixTime)
		{
			// WWL uses UTC ticks, convert to local time
			var utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
			return utcTime.ToLocalTime();
		}

		private static int ToUnixTime(DateTime dateTime)
		{
			return (int)dateTime.ToUniversalTime().ToUnixEpochDate();
		}

		private void OnServiceChanged(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('~', e.Announcement);
		}

		private void OnServiceRemoved(object sender, ServiceAnnouncementEventArgs e)
		{
			cumulus.LogMessage("ZeroConfig Service: WLL service has been removed!");
		}

		private void OnServiceAdded(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('+', e.Announcement);
		}

		private void PrintService(char startChar, ServiceAnnouncement service)
		{
			cumulus.LogDebugMessage($"ZeroConf Service: {startChar} '{service.Instance}' on {service.NetworkInterface.Name}");
			cumulus.LogDebugMessage($"\tHost: {service.Hostname} ({string.Join(", ", service.Addresses)})");

			lock (threadSafer)
			{
				ipaddr = service.Addresses[0].ToString();
				cumulus.LogMessage($"WLL found, reporting its IP address as: {ipaddr}");
				if (cumulus.VP2IPAddr != ipaddr)
				{
					cumulus.LogMessage($"WLL IP address has changed from {cumulus.VP2IPAddr} to {ipaddr}");
					if (cumulus.WLLAutoUpdateIpAddress)
					{
						cumulus.LogMessage($"WLL changing Cumulus config to the new IP address {ipaddr}");
						cumulus.VP2IPAddr = ipaddr;
						cumulus.WriteIniFile();
					}
					else
					{
						cumulus.LogMessage($"WLL ignoring new IP address {ipaddr} due to setting WLLAutoUpdateIpAddress");
					}
				}
			}
		}

		private double ConvertRainClicksToUser(double clicks, int size)
		{
			// 0: Reseverved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
			switch (size)
			{
				case 1:
					return ConvertRainINToUser(clicks * 0.01);
				case 2:
					return ConvertRainMMToUser(clicks * 0.2);
				case 3:
					return ConvertRainMMToUser(clicks * 0.1);
				case 4:
					return ConvertRainINToUser(clicks * 0.001);
				default:
					switch (cumulus.VPrainGaugeType)
					{
						// Hmm, no valid tip size from WLL...
						// One click is normally either 0.01 inches or 0.2 mm
						// Try the setting in Cumulus.ini
						// Rain gauge type not configured, assume from units
						case -1 when cumulus.RainUnit == 0:
							return clicks * 0.2;
						case -1:
							return clicks * 0.01;
						// Rain gauge is metric, convert to user unit
						case 0:
							return ConvertRainMMToUser(clicks * 0.2);
						default:
							return ConvertRainINToUser(clicks * 0.01);
					}
			}
		}

		private static bool CheckIpValid(string strIp)
		{
			if (string.IsNullOrEmpty(strIp))
				return false;
			//  Split string by ".", check that array length is 4
			var arrOctets = strIp.Split('.');
			if (arrOctets.Length != 4)
				return false;

			//Check each substring checking that parses to byte
			byte result;
			return arrOctets.All(strOctet => byte.TryParse(strOctet, out result));
		}

		private void SetTxBatteryStatus(int txId, uint status)
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
			cumulus.CurrentActivity = "Reading archive data";
			cumulus.LogMessage("WLL history: Reading history data from log files");
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			cumulus.LogMessage("WLL history: Reading archive data from WeatherLink API");
			bw = new BackgroundWorker();
			//histprog = new historyProgressWindow();
			//histprog.Owner = mainWindow;
			//histprog.Show();
			bw.DoWork += bw_ReadHistory;
			//bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
			bw.RunWorkerCompleted += bw_ReadHistoryCompleted;
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		private void bw_ReadHistoryCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cumulus.LogMessage("WLL history: WeatherLink API archive reading thread completed");
			if (e.Error != null)
			{
				cumulus.LogMessage("WLL history: Archive reading thread apparently terminated with an error: " + e.Error.Message);
			}
			cumulus.LogMessage("WLL history: Updating highs and lows");
			//using (cumulusEntities dataContext = new cumulusEntities())
			//{
			//    UpdateHighsAndLows(dataContext);
			//}
			cumulus.CurrentActivity = "Normal running";

			// restore settings
			cumulus.UseSpeedForAvgCalc = savedUseSpeedForAvgCalc;
			CalcRecentMaxGust = savedCalculatePeakGust;

			StartLoop();
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		/*
		private void bw_DoStart(object sender, DoWorkEventArgs e)
		{
			cumulus.LogDebugMessage("Lock: Station waiting for lock");
			Cumulus.syncInit.Wait();
			cumulus.LogDebugMessage("Lock: Station has the lock");

			// Wait a short while for Cumulus initialisation to complete
			Thread.Sleep(500);
			StartLoop();

			cumulus.LogDebugMessage("Lock: Station releasing lock");
			Cumulus.syncInit.Release();
		}
		*/

		private void bw_ReadHistory(object sender, DoWorkEventArgs e)
		{
			int archiveRun = 0;
			cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			cumulus.LogDebugMessage("Lock: Station has the lock");

			try
			{
				// set this temporarily, so speed is done from average and not peak gust from logger
				savedUseSpeedForAvgCalc = cumulus.UseSpeedForAvgCalc;
				cumulus.UseSpeedForAvgCalc = true;

				// same for gust values
				savedCalculatePeakGust = CalcRecentMaxGust;
				CalcRecentMaxGust = true;

				// Configure a web proxy if required
				if (!string.IsNullOrEmpty(cumulus.HTTPProxyName))
				{
					HistoricHttpHandler.Proxy = new WebProxy(cumulus.HTTPProxyName, cumulus.HTTPProxyPort);
					HistoricHttpHandler.UseProxy = true;
					if (!string.IsNullOrEmpty(cumulus.HTTPProxyUser))
					{
						HistoricHttpHandler.Credentials = new NetworkCredential(cumulus.HTTPProxyUser, cumulus.HTTPProxyPassword);
					}
				}

				do
				{
					GetWlHistoricData();
					archiveRun++;
				} while (archiveRun < maxArchiveRuns);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Exception occurred reading archive data: " + ex.Message);
			}
			cumulus.LogDebugMessage("Lock: Station releasing the lock");
			Cumulus.syncInit.Release();
		}

		private void GetWlHistoricData()
		{
			cumulus.LogMessage("GetWlHistoricData: Get WL.com Historic Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("GetWlHistoricData: Missing WeatherLink API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId == string.Empty || int.Parse(cumulus.WllStationId) < 10)
			{
				const string msg = "No WeatherLink API station ID in the configuration";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage("GetWlHistoricData: " + msg);

			}

			//int passCount;
			//const int maxPasses = 4;

			var unixDateTime = ToUnixTime(DateTime.Now);
			var startTime = ToUnixTime(cumulus.LastUpdateTime);
			int endTime = unixDateTime;
			int unix24hrs = 24 * 60 * 60;

			// The API call is limited to fetching 24 hours of data
			if (unixDateTime - startTime > unix24hrs)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime + unix24hrs;
				maxArchiveRuns++;
			}

			cumulus.LogConsoleMessage($"Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime:s} to: {FromUnixTime(endTime):s}");
			cumulus.LogMessage($"GetWlHistoricData: Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime:s} to: {FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.WllApiKey },
				{ "station-id", cumulus.WllStationId },
				{ "t", unixDateTime.ToString() },
				{ "start-timestamp", startTime.ToString() },
				{ "end-timestamp", endTime.ToString() }
			};

			StringBuilder dataStringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				dataStringBuilder.Append(entry.Key);
				dataStringBuilder.Append(entry.Value);
			}

			string data = dataStringBuilder.ToString();

			var apiSignature = WlDotCom.CalculateApiSignature(cumulus.WllApiSecret, data);

			parameters.Remove("station-id");
			parameters.Add("api-signature", apiSignature);

			StringBuilder historicUrl = new StringBuilder();
			historicUrl.Append("https://api.weatherlink.com/v2/historic/" + cumulus.WllStationId + "?");
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				historicUrl.Append(entry.Key);
				historicUrl.Append("=");
				historicUrl.Append(entry.Value);
				historicUrl.Append("&");
			}
			// remove the trailing "&"
			historicUrl.Remove(historicUrl.Length - 1, 1);

			var logUrl = historicUrl.ToString().Replace(cumulus.WllApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"WeatherLink URL = {logUrl}");

			lastDataReadTime = cumulus.LastUpdateTime;
			int luhour = lastDataReadTime.Hour;

			int rollHour = Math.Abs(cumulus.GetHourInc());

			cumulus.LogMessage($"Rollover hour = {rollHour}");

			bool rolloverdone = luhour == rollHour;

			bool midnightraindone = luhour == 0;

			WlHistory histObj;
			int noOfRecs = 0;
			WlHistorySensor sensorWithMostRecs;

			try
			{
				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = wlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					var responseBody = response.Content.ReadAsStringAsync().Result;
					cumulus.LogDebugMessage($"GetWlHistoricData: WeatherLink API Historic Response code: {response.StatusCode}");
					cumulus.LogDataMessage($"GetWlHistoricData: WeatherLink API Historic Response: {responseBody}");

					if ((int)response.StatusCode != 200)
					{
						var historyError = responseBody.FromJson<WlErrorResponse>();
						cumulus.LogMessage($"GetWlHistoricData: WeatherLink API Historic Error: {historyError.code}, {historyError.message}");
						cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.message}");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}

					histObj = responseBody.FromJson<WlHistory>();

					if (responseBody == "{}")
					{
						cumulus.LogMessage("GetWlHistoricData: WeatherLink API Historic: No data was returned. Check your Device Id.");
						cumulus.LogConsoleMessage(" - No historic data available");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}
					else if (responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
					{
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
							cumulus.LogConsoleMessage(" - No historic data available");
							cumulus.LastUpdateTime = FromUnixTime(endTime);
							return;
						}
						else
						{
							cumulus.LogMessage($"GetWlHistoricData: Found {noOfRecs} historic records to process");
						}
					}
					else // No idea what we got, dump it to the log
					{
						cumulus.LogMessage("GetWlHistoricData: Invalid historic message received");
						cumulus.LogDataMessage("GetWlHistoricData: Received: " + responseBody);
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetWlHistoricData:  Exception: " + ex.Message);
				cumulus.LastUpdateTime = FromUnixTime(endTime);
				return;
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				try
				{
					// Not all sensors may have the same number of records. We are using the WLL to create the historic data, the other sensors (AirLink) may have more or less records!
					// For the additional sensors, check if they have the same number of reocrds as the WLL. If they do great, we just process the next record.
					// If the sensor has more or less historic records than the WLL, then we find the record (if any) that matches the WLL record timestamp


					var refData = sensorWithMostRecs.data[dataIndex].FromJsv<WlHistorySensorDataType13Baro>();
					DecodeHistoric(sensorWithMostRecs.data_structure_type, sensorWithMostRecs.sensor_type, sensorWithMostRecs.data[dataIndex]);
					var timestamp = FromUnixTime(refData.ts);

					foreach (var sensor in histObj.sensors)
					{
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
									cumulus.LogDebugMessage("GetWlHistoricData: Warning. No outdoor Airlink data for this log interval !!");
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
									cumulus.LogDebugMessage("GetWlHistoricData: Warning. No indoor Airlink data for this log interval !!");
							}
							else
							{
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkIn.DecodeAlHistoric(dataStructureType, sensor.data[dataIndex]);
							}
						}
						else if (sensorType != 504 && sensorType != 506 && lsid != sensorWithMostRecs.lsid)
						{
							DecodeHistoric(dataStructureType, sensorType, sensor.data[dataIndex]);
							// sensor 504 (WLL info) does not always contain a full set of records, so grab the timestamp from a 'real' sensor
						}
					}

					var h = timestamp.Hour;

					if (cumulus.LogExtraSensors)
					{
						cumulus.DoExtraLogFile(timestamp);
					}

					if (cumulus.airLinkOut != null || cumulus.airLinkIn != null)
					{
						cumulus.DoAirLinkLogFile(timestamp);
					}

					// Now we have the primary data, calculate the derived data
					if (cumulus.CalculatedWC)
					{
						// DoWindChill does all the required checks and conversions
						DoWindChill(OutdoorTemperature, timestamp);
					}

					DoApparentTemp(timestamp);
					DoFeelsLike(timestamp);
					DoHumidex(timestamp);

					// Log all the data
					cumulus.DoLogFile(timestamp, false);
					cumulus.LogMessage("GetWlHistoricData: Log file entry written");

					AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
					AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
					AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
						IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV, FeelsLike, Humidex);
					AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
						OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex);
					RemoveOldLHData(timestamp);
					RemoveOldL3HData(timestamp);
					RemoveOldGraphData(timestamp);
					DoTrendValues(timestamp);
					UpdateStatusPanel(timestamp);
					cumulus.AddToWebServiceLists(timestamp);

					//  if outside rollover hour, rollover yet to be done
					if (h != rollHour)
					{
						rolloverdone = false;
					}

					// In rollover hour and rollover not yet done
					if ((h == rollHour) && !rolloverdone)
					{
						// do rollover
						cumulus.LogMessage("GetWlHistoricData: Day rollover " + timestamp.ToShortTimeString());
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
						ResetSunshineHours();
						midnightraindone = true;
					}


					if (!Program.service)
						Console.Write("\r - processed " + (((double)dataIndex + 1) / noOfRecs).ToString("P0"));
					cumulus.LogMessage($"GetWlHistoricData: {dataIndex + 1} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("GetWlHistoricData: Exception: " + ex.Message);
				}
			}

			if (!Program.service)
				Console.WriteLine(""); // flush the progress line
		}

		private void DecodeHistoric(int dataType, int sensorType, string json)
		{
			// The WLL sends the timestamp in Unix ticks, and in UTC

			try
			{
				switch (dataType)
				{
					case 11: // ISS data
						var data11 = json.FromJsv<WlHistorySensorDataType11>();
						var recordTs = FromUnixTime(data11.ts);
						//weatherLinkArchiveInterval = data.Value<int>("arch_int");

						// Temperature & Humidity
						if (cumulus.WllPrimaryTempHum == data11.tx_id)
						{
							/*
							 * Avaialable fields
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
								if (data11.temp_last == -99)
								{
									cumulus.LogMessage($"WL.com historic: no valid Primary temperature value found [-99] on TxId {data11.tx_id}");
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: using temp/hum data from TxId {data11.tx_id}");

									// do high temp
									ts = FromUnixTime(data11.temp_hi_at);
									DoOutdoorTemp(ConvertTempFToUser(data11.temp_hi), ts);
									// do low temp
									ts = FromUnixTime(data11.temp_lo_at);
									DoOutdoorTemp(ConvertTempFToUser(data11.temp_lo), ts);
									// do last temp
									DoOutdoorTemp(ConvertTempFToUser(data11.temp_last), recordTs);
								}
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing Primary temperature value on TxId {data11.tx_id}. Error: {ex.Message}");
							}

							try
							{
								// do high humidty
								ts = FromUnixTime(data11.hum_hi_at);
								DoOutdoorHumidity(Convert.ToInt32(data11.hum_hi), ts);
								// do low humidity
								ts = FromUnixTime(data11.hum_lo_at);
								DoOutdoorHumidity(Convert.ToInt32(data11.hum_lo), ts);
								// do current humidity
								DoOutdoorHumidity(Convert.ToInt32(data11.hum_last), recordTs);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing Primary humidity value on TxId {data11.tx_id}. Error: {ex.Message}");
							}

							try
							{
								// do high DP
								ts = FromUnixTime(data11.dew_point_hi_at);
								DoOutdoorDewpoint(ConvertTempFToUser(data11.dew_point_hi), ts);
								// do low DP
								ts = FromUnixTime(data11.dew_point_lo_at);
								DoOutdoorDewpoint(ConvertTempFToUser(data11.dew_point_lo), ts);
								// do last DP
								DoOutdoorDewpoint(ConvertTempFToUser(data11.dew_point_last), recordTs);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing dew point value on TxId {data11.tx_id}. Error: {ex.Message}");
							}

							if (!cumulus.CalculatedWC)
							{
								// use wind chill from WLL - otherwise we calculate it at the end of processing the historic record when we have all the data
								try
								{
									// do low WC
									ts = FromUnixTime(data11.wind_chill_lo_at);
									DoWindChill(ConvertTempFToUser(data11.wind_chill_lo), ts);
									// do last WC
									DoWindChill(ConvertTempFToUser(data11.wind_chill_last), recordTs);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing wind chill value on TxId {data11.tx_id}. Error: {ex.Message}");
								}
							}
						}
						else
						{   // Check for Extra temperature/humidity settings
							for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
							{
								if (cumulus.WllExtraTempTx[tempTxId - 1] != data11.tx_id) continue;

								try
								{
									if (data11.temp_last == -99)
									{
										cumulus.LogDebugMessage($"WL.com historic: no valid Extra temperature value on TxId {data11.tx_id}");
									}
									else
									{
										cumulus.LogDebugMessage($"WL.com historic: using extra temp data from TxId {data11.tx_id}");

										DoExtraTemp(ConvertTempFToUser(data11.temp_last), tempTxId);
									}
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing extra temp value on TxId {data11.tx_id}");
									cumulus.LogDebugMessage($"WL.com historic: Exception {ex.Message}");
								}

								if (!cumulus.WllExtraHumTx[tempTxId - 1]) continue;

								try
								{
									DoExtraHum(data11.hum_last, tempTxId);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing extra humidity value on TxId {data11.tx_id}. Error: {ex.Message}");
								}
							}
						}

						// Wind
						if (cumulus.WllPrimaryWind == data11.tx_id)
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
								cumulus.LogDebugMessage($"WL.com historic: using wind data from TxId {data11.tx_id}");
								DoWind(ConvertWindMPHToUser(data11.wind_speed_hi), data11.wind_speed_hi_dir, ConvertWindMPHToUser(data11.wind_speed_avg), recordTs);

								WindAverage = ConvertWindMPHToUser(data11.wind_speed_avg) * cumulus.Calib.WindSpeed.Mult;

								// add in 'archivePeriod' minutes worth of wind speed to windrun
								int interval = data11.arch_int / 60;
								WindRunToday += ((WindAverage * WindRunHourMult[cumulus.WindUnit] * interval) / 60.0);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing wind values on TxId {data11.tx_id}. Error: {ex.Message}");
							}
						}

						// Rainfall
						if (cumulus.WllPrimaryRain == data11.tx_id)
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

							cumulus.LogDebugMessage($"WL.com historic: using rain data from TxId {data11.tx_id}");

							// The WL API v2 does not provide any running totals for rainfall, only  :(
							// So we will have to add the interval data to the running total and hope it all works out!

							try
							{
								var rain = ConvertRainClicksToUser(data11.rainfall_clicks, data11.rain_size);
								var rainrate = ConvertRainClicksToUser(data11.rain_rate_hi_clicks, data11.rain_size);
								if (rain > 0)
								{
									cumulus.LogDebugMessage($"WL.com historic: Adding rain {rain.ToString(cumulus.RainFormat)}");
								}
								rain += Raincounter;

								if (rainrate < 0)
								{
									rainrate = 0;
								}

								DoRain(rain, rainrate, recordTs);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing rain data on TxId {data11.tx_id}. Error:{ex.Message}");
							}

						}

						// UV
						if (cumulus.WllPrimaryUV == data11.tx_id)
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
								cumulus.LogDebugMessage($"WL.com historic: using UV data from TxId {data11.tx_id}");

								DoUV(data11.uv_index_avg, recordTs);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing UV value on TxId {data11.tx_id}. Error: {ex.Message}");
							}

						}

						// Solar
						if (cumulus.WllPrimarySolar == data11.tx_id)
						{
							/*
							 * Available fields
							 * "solar_energy"
							 * "solar_rad_avg"
							 * "solar_rad_hi_at"
							 * "solar_rad_hi"
							 * "solar_rad_volt_last"
							 * "solar_volt_last"
							 * "et"
							 */
							try
							{
								cumulus.LogDebugMessage($"WL.com historic: using solar data from TxId {data11.tx_id}");
								DoSolarRad(data11.solar_rad_avg, recordTs);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing Solar value on TxId {data11.tx_id}. Error: {ex.Message}");
							}
						}
						break;

					case 13: // Non-ISS data
						switch (sensorType)
						{
							case 56: // Soil + Leaf
								var data13 = json.FromJsv<WlHistorySensorDataType13>();

								string idx = "";
								/*
								 * Leaf Wetness
								 * Avaialable fields
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

								cumulus.LogDebugMessage($"WL.com historic: found Leaf/Soil data on TxId {data13.tx_id}");

								// We are relying on user configuration here, trap any errors
								try
								{
									if (cumulus.WllExtraLeafTx1 == data13.tx_id)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx1;
										DoLeafWetness((double)data13[idx], 1);
									}
									if (cumulus.WllExtraLeafTx2 == data13.tx_id)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx2;
										DoLeafWetness((double)data13[idx], 2);
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, LeafWetness txid={data13.tx_id}, idx={idx}: {e.Message}");
								}
								/*
								 * Soil Moisture
								 * Avaialable fields
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
									if (cumulus.WllExtraSoilMoistureTx1 == data13.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx1;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilMoisture((double)data13[idx], 1);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx2 == data13.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx2;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilMoisture((double)data13[idx], 2);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx3 == data13.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx3;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilMoisture((double)data13[idx], 3);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx4 == data13.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx4;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilMoisture((double)data13[idx], 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, SoilMoisture txid={data13.tx_id}, idx={idx}: {e.Message}");
								}

								/*
								 * Soil Temperature
								 * Avaialble fields
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
									if (cumulus.WllExtraSoilTempTx1 == data13.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx1;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data13[idx]), 1);
										}
									}
									if (cumulus.WllExtraSoilTempTx2 == data13.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx2;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data13[idx]), 2);
										}
									}
									if (cumulus.WllExtraSoilTempTx3 == data13.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx3;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data13[idx]), 3);
										}
									}
									if (cumulus.WllExtraSoilTempTx4 == data13.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx4;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} on TxId {data13.tx_id}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data13[idx]), 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, SoilTemp txid={data13.tx_id}, idx={idx}: {e.Message}");
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
									// check the high
									var ts = FromUnixTime(data13baro.bar_hi_at);
									DoPressure(ConvertPressINHGToUser(data13baro.bar_hi), ts);
									// check the low
									ts = FromUnixTime(data13baro.bar_lo_at);
									DoPressure(ConvertPressINHGToUser(data13baro.bar_lo), ts);
									// leave it at current value
									ts = FromUnixTime(data13baro.ts);
									DoPressure(ConvertPressINHGToUser(data13baro.bar_sea_level), ts);
									DoPressTrend("Pressure trend");
									// Altimeter from absolute
									StationPressure = ConvertPressINHGToUser(data13baro.bar_absolute);
									// Or do we use calibration? The VP2 code doesn't?
									//StationPressure = ConvertPressINHGToUser(data.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
									AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(PressureHPa(StationPressure), AltitudeM(cumulus.Altitude)));
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing baro reading. Error: {ex.Message}");
								}
								break;

							case 243: // Inside temp/hum
								/*
								 * Avilable fields
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
									DoIndoorTemp(ConvertTempFToUser(data13temp.temp_in_last));
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing temp-in reading. Error: {ex.Message}]");
								}


								try
								{
									DoIndoorHumidity(Convert.ToInt32(data13temp.hum_in_last));
								}
								catch (Exception ex)
								{
									cumulus.LogDebugMessage($"WLL current: Error processing humidity-in. Error: {ex.Message}]");
								}

								break;

							default:
								cumulus.LogDebugMessage($"WL.com historic: found an unknown sensor type [{sensorType}]!");
								break;

						}
						break;

					case 17: // AirLink
						break;

					default:
						cumulus.LogDebugMessage($"WL.com historic: found an unknown data structure type [{dataType}]!");
						break;
				}
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error, DecodeHistoric, DataType={dataType}, SensorType={sensorType}: " + e.Message);
			}
		}

		private void DecodeWlApiHealth(WlHistorySensor sensor, bool startingup)
		{
			if (sensor.data.Count == 0)
			{
				if (sensor.data_structure_type == 15)
				{
					cumulus.LogDebugMessage("WLL Health - did not find any health data for WLL device");
				}
				else if (sensor.data_structure_type == 11)
				{
					cumulus.LogDebugMessage("WLL Health - did not find health data for ISS device");
				}
				return;
			}

			if (sensor.data_structure_type == 15)
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

				cumulus.LogDebugMessage("WLL Health - found health data for WLL device");

				try
				{
					var data15 = sensor.data.Last().FromJsv<WlHistorySensorDataType15>();

					var dat = FromUnixTime(data15.firmware_version);
					DavisFirmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");

					var battV = data15.battery_voltage / 1000.0;
					ConBatText = battV.ToString("F2");
					if (battV < 5.2)
					{
						wllVoltageLow = true;
						cumulus.LogMessage($"WLL WARNING: Backup battery voltage is low = {battV:0.##}V");
					}
					else
					{
						wllVoltageLow = false;
						cumulus.LogDebugMessage($"WLL Battery Voltage = {battV:0.##}V");
					}
					var inpV = data15.input_voltage / 1000.0;
					ConSupplyVoltageText = inpV.ToString("F2");
					if (inpV < 4.0)
					{
						cumulus.LogMessage($"WLL WARNING: Input voltage is low = {inpV:0.##}V");
					}
					else
					{
						cumulus.LogDebugMessage($"WLL Input Voltage = {inpV:0.##}V");
					}
					var upt = TimeSpan.FromSeconds(data15.uptime);
					var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage("WLL Uptime = " + uptStr);

					// Only present if WiFi attached
					if (data15.wifi_rssi.HasValue)
					{
						DavisTxRssi[0] = data15.wifi_rssi.Value;
						cumulus.LogDebugMessage("WLL WiFi RSSI = " + DavisTxRssi[0] + "dB");
					}

					upt = TimeSpan.FromSeconds(data15.link_uptime);
					uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage("WLL Link Uptime = " + uptStr);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"WL.com historic: Error processing WLL health. Error: {ex.Message}");
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
			else if (sensor.data_structure_type == 11)
			{
				/* ISS
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
					var data11 = sensor.data.Last().FromJsv<WlHistorySensorDataType11>();

					cumulus.LogDebugMessage("WLL Health - found health data for ISS device TxId = " + data11.tx_id);

					// Save the archive interval
					//weatherLinkArchiveInterval = data.Value<int>("arch_int");

					// Check battery state 0=Good, 1=Low
					SetTxBatteryStatus(data11.tx_id, data11.trans_battery_flag);
					if (data11.trans_battery_flag == 1)
					{
						cumulus.LogMessage($"WLL WARNING: Battery voltage is low in TxId {data11.tx_id}");
					}
					else
					{
						cumulus.LogDebugMessage($"WLL Health: ISS {data11.tx_id}: Battery state is OK");
					}

					//DavisTotalPacketsReceived[txid] = ;  // Do not have a value for this
					DavisTotalPacketsMissed[data11.tx_id] = data11.error_packets;
					DavisNumCRCerrors[data11.tx_id] = data11.error_packets;
					DavisNumberOfResynchs[data11.tx_id] = data11.resynchs;
					DavisMaxInARow[data11.tx_id] = data11.good_packets_streak;
					DavisReceptionPct[data11.tx_id] = data11.reception;
					DavisTxRssi[data11.tx_id] = data11.rssi;

					cumulus.LogDebugMessage($"WLL Health: IIS {data11.tx_id}: Errors={DavisTotalPacketsMissed[data11.tx_id]}, CRCs={DavisNumCRCerrors[data11.tx_id]}, Resyncs={DavisNumberOfResynchs[data11.tx_id]}, Streak={DavisMaxInARow[data11.tx_id]}, %={DavisReceptionPct[data11.tx_id]}, RSSI={DavisTxRssi[data11.tx_id]}");
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"WLL Health: Error processing transmitter health. Error: {ex.Message}");
				}
			}
		}

		private void HealthTimerTick(object source, ElapsedEventArgs e)
		{
			// Only run every 15 minutes
			// The WLL only reports its health every 15 mins, on the hour, :15, :30 and :45
			// We run at :01, :16, :31, :46 to allow time for wl.com to generate the stats
			if (DateTime.Now.Minute % 15 == 1)
			{
				GetWlHistoricHealth();
				var msg = string.Format("WLL: Percentage good packets received from WLL {0:F2}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100), multicastsBad, multicastsGood);
				cumulus.LogMessage(msg);
			}
		}

		// Extracts health infomation from the last archive record
		private void GetWlHistoricHealth()
		{
			cumulus.LogMessage("WLL Health: Get WL.com Historic Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("WLL Health: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (cumulus.WllStationId == string.Empty || int.Parse(cumulus.WllStationId) < 10)
			{
				const string msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogConsoleMessage("GetWlHistoricHealth: " + msg);
				cumulus.LogMessage($"WLL Health: {msg}, aborting!");
				return;
			}

			var unixDateTime = ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - weatherLinkArchiveInterval;
			int endTime = unixDateTime;

			cumulus.LogDebugMessage($"WLL Health: Downloading the historic record from WL.com from: {FromUnixTime(startTime):s} to: {FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.WllApiKey },
				{ "station-id", cumulus.WllStationId },
				{ "t", unixDateTime.ToString() },
				{ "start-timestamp", startTime.ToString() },
				{ "end-timestamp", endTime.ToString() }
			};

			StringBuilder dataStringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				dataStringBuilder.Append(entry.Key);
				dataStringBuilder.Append(entry.Value);
			}

			string data = dataStringBuilder.ToString();

			var apiSignature = WlDotCom.CalculateApiSignature(cumulus.WllApiSecret, data);

			parameters.Remove("station-id");
			parameters.Add("api-signature", apiSignature);

			StringBuilder historicUrl = new StringBuilder();
			historicUrl.Append("https://api.weatherlink.com/v2/historic/" + cumulus.WllStationId + "?");
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				historicUrl.Append(entry.Key);
				historicUrl.Append("=");
				historicUrl.Append(entry.Value);
				historicUrl.Append("&");
			}
			// remove the trailing "&"
			historicUrl.Remove(historicUrl.Length - 1, 1);

			var logUrl = historicUrl.ToString().Replace(cumulus.WllApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"WLL Health: WeatherLink URL = {logUrl}");

			try
			{
				// we want to do this synchronously, so .Result
				WlHistory histObj;
				using (HttpResponseMessage response = wlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					var responseBody = response.Content.ReadAsStringAsync().Result;
					cumulus.LogDataMessage($"WLL Health: WeatherLink API Response: {response.StatusCode} - {responseBody}");

					if ((int)response.StatusCode != 200)
					{
						var errObj = responseBody.FromJson<WlErrorResponse>();
						cumulus.LogMessage($"WLL Health: WeatherLink API Error: {errObj.code}, {errObj.message}");
						return;
					}

					if (responseBody == "{}")
					{
						cumulus.LogMessage("WLL Health: WeatherLink API: No data was returned. Check your Device Id.");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}

					if (!responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
					{
						// No idea what we got, dump it to the log
						cumulus.LogMessage("WLL Health: Invalid historic message received");
						cumulus.LogDataMessage("WLL Health: Received: " + responseBody);
						return;
					}

					histObj = responseBody.FromJson<WlHistory>();
				}

				// get the sensor data
				if (histObj.sensors.Count == 0)
				{
					cumulus.LogMessage("WLL Health: No historic data available");
					return;
				}
				else
				{
					cumulus.LogDebugMessage($"WLL Health: Found {histObj.sensors.Count} sensor records to process");
				}


				try
				{
					// Sensor types we are interested in...
					// 504 = WLL Health
					// 506 = AirLink Health

					// Get the LSID of the health station associated with each device
					//var wllHealthLsid = GetWlHistoricHealthLsid(cumulus.WllParentId, 504);
					var alInHealthLsid = GetWlHistoricHealthLsid(cumulus.airLinkInLsid, 506);
					var alOutHealthLsid = GetWlHistoricHealthLsid(cumulus.airLinkOutLsid, 506);

					foreach (var sensor in histObj.sensors)
					{
						var sensorType = sensor.sensor_type;
						var dataStructureType = sensor.data_structure_type;
						var lsid = sensor.lsid;

						switch (sensorType)
						{
							// AirLink Outdoor
							case 506 when lsid == alOutHealthLsid:
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkOut.DecodeWlApiHealth(sensor, true);
								break;
							// AirLink Indoor
							case 506 when lsid == alInHealthLsid:
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkIn.DecodeWlApiHealth(sensor, true);
								break;
							default:
								if (sensorType == 504 || dataStructureType == 11)
								{
									// Either a WLL (504) or ISS (data type = 11) record
									DecodeWlApiHealth(sensor, true);
								}
								break;
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("WLL Health: exception: " + ex.Message);
				}
				cumulus.BatteryLowAlarm.Triggered = TxBatText.Contains("LOW") || wllVoltageLow;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("WLL Health: exception: " + ex.Message);
			}

		}

		// Finds all stations associated with this API
		// Return true if only 1 result is found, else return false
		private void GetAvailableStationIds(bool logToConsole = false)
		{
			var unixDateTime = ToUnixTime(DateTime.Now);

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("WLLStations: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.WllApiKey },
				{ "t", unixDateTime.ToString() }
			};

			StringBuilder dataStringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				dataStringBuilder.Append(entry.Key);
				dataStringBuilder.Append(entry.Value);
			}
			string header = dataStringBuilder.ToString();

			var apiSignature = WlDotCom.CalculateApiSignature(cumulus.WllApiSecret, header);
			parameters.Add("api-signature", apiSignature);

			StringBuilder stationsUrl = new StringBuilder();
			stationsUrl.Append("https://api.weatherlink.com/v2/stations?");
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				stationsUrl.Append(entry.Key);
				stationsUrl.Append("=");
				stationsUrl.Append(entry.Value);
				stationsUrl.Append("&");
			}
			// remove the trailing "&"
			stationsUrl.Remove(stationsUrl.Length - 1, 1);

			var logUrl = stationsUrl.ToString().Replace(cumulus.WllApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"WLLStations: URL = {logUrl}");

			try
			{
				// We want to do this synchronously
				var response = wlHttpClient.GetAsync(stationsUrl.ToString()).Result;
				var responseBody = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDebugMessage("WLLStations: WeatherLink API Response: " + response.StatusCode + ": " + responseBody);

				if ((int)response.StatusCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"WLLStations: WeatherLink API Error: {errObj.code} - {errObj.message}");
					return;
				}

				var stationsObj = responseBody.FromJson<WlStationList>();

				foreach (var station in stationsObj.stations)
				{
					cumulus.LogMessage($"WLLStations: Found WeatherLink station id = {station.station_id}, name = {station.station_name}");
					if (stationsObj.stations.Count > 1 && logToConsole)
					{
						cumulus.LogConsoleMessage($" - Found WeatherLink station id = {station.station_id}, name = {station.station_name}, active = {station.active}");
					}
					if (station.station_id == int.Parse(cumulus.WllStationId))
					{
						cumulus.LogDebugMessage($"WLLStations: Setting WLL parent ID = {station.gateway_id}");
						cumulus.WllParentId = station.gateway_id;

						if (station.recording_interval != cumulus.logints[cumulus.DataLogInterval])
						{
							cumulus.LogMessage($"WLLStations:  - Cumulus log interval {cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station.recording_interval}");
						}
					}
				}
				if (stationsObj.stations.Count > 1 && int.Parse(cumulus.WllStationId) < 10)
				{
					if (logToConsole)
						cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your WLL configuration to enable history downloads.");
				}
				else if (stationsObj.stations.Count == 1 && int.Parse(cumulus.WllStationId) != stationsObj.stations[0].station_id)
				{
					cumulus.LogMessage($"WLLStations: Only found 1 WeatherLink station, using id = {stationsObj.stations[0].station_id}");
					cumulus.WllStationId = stationsObj.stations[0].station_id.ToString();
					// And save it to the config file
					cumulus.WriteIniFile();

					cumulus.LogDebugMessage($"WLLStations: Setting WLL parent ID = {stationsObj.stations[0].gateway_id}");
					cumulus.WllParentId = stationsObj.stations[0].gateway_id;
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
			var unixDateTime = ToUnixTime(DateTime.Now);

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("GetAvailableSensors: WeatherLink API data is missing in the configuration, aborting!");
				return;
			}

			if (cumulus.WllStationId == string.Empty || int.Parse(cumulus.WllStationId) < 10)
			{
				cumulus.LogMessage("GetAvailableSensors: No WeatherLink API station ID has been configured, aborting!");
				return;
			}

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.WllApiKey },
				{ "t", unixDateTime.ToString() }
			};

			StringBuilder dataStringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				dataStringBuilder.Append(entry.Key);
				dataStringBuilder.Append(entry.Value);
			}
			string header = dataStringBuilder.ToString();

			var apiSignature = WlDotCom.CalculateApiSignature(cumulus.WllApiSecret, header);
			parameters.Add("api-signature", apiSignature);

			StringBuilder stationsUrl = new StringBuilder();
			stationsUrl.Append("https://api.weatherlink.com/v2/sensors?");
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				stationsUrl.Append(entry.Key);
				stationsUrl.Append("=");
				stationsUrl.Append(entry.Value);
				stationsUrl.Append("&");
			}
			// remove the trailing "&"
			stationsUrl.Remove(stationsUrl.Length - 1, 1);

			var logUrl = stationsUrl.ToString().Replace(cumulus.WllApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"GetAvailableSensors: URL = {logUrl}");

			try
			{
				// We want to do this synchronously
				var response = wlHttpClient.GetAsync(stationsUrl.ToString()).Result;
				var responseBody = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDebugMessage("GetAvailableSensors: WeatherLink API Response: " + response.StatusCode + ": " + responseBody);

				if ((int)response.StatusCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"GetAvailableSensors: WeatherLink API Error: {errObj.code} - {errObj.message}");
					return;
				}

				var sensorsObj = responseBody.FromJson<WlSensorList>();

				// Sensor types we are interested in...
				// 323 = Outdoor AirLink
				// 326 = Indoor AirLink
				// 504 = WLL Health
				// 506 = AirLink Health
				var types = new[] { 45, 323, 326, 504, 506 };
				foreach (var sensor in sensorsObj.sensors)
				{
					cumulus.LogDebugMessage($"GetAvailableSensors: Found WeatherLink Sensor type={sensor.sensor_type}, lsid={sensor.lsid}, station_id={sensor.station_id}, name={sensor.product_name}, parentId={sensor.parent_device_id}, parent={sensor.parent_device_name}");

					if (types.Contains(sensor.sensor_type) || sensor.category == "ISS")
					{
						var wlSensor = new WlSensor(sensor.sensor_type, sensor.lsid, sensor.parent_device_id, sensor.product_name, sensor.parent_device_name);
						sensorList.Add(wlSensor);
						if (wlSensor.SensorType == 323 && sensor.station_id == int.Parse(cumulus.AirLinkOutStationId))
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Outdoor LSID to {wlSensor.LSID}");
							cumulus.airLinkOutLsid = wlSensor.LSID;
						}
						else if (wlSensor.SensorType == 326 && sensor.station_id == int.Parse(cumulus.AirLinkInStationId))
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Indoor LSID to {wlSensor.LSID}");
							cumulus.airLinkInLsid = wlSensor.LSID;
						}
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("GetAvailableSensors: WeatherLink API exception: " + ex.Message);
			}
		}

		private void BroadcastTimeout(object source, ElapsedEventArgs e)
		{
			if (broadcastReceived)
			{
				broadcastReceived = false;
				DataStopped = false;
				cumulus.DataStoppedAlarm.Triggered = false;
			}
			else
			{
				cumulus.LogMessage($"ERROR: No broadcast data received from the WLL for {tmrBroadcastWatchdog.Interval / 1000} seconds");
				DataStopped = true;
				cumulus.DataStoppedAlarm.Triggered = true;
				// Try and give the broadcasts a kick in case the last command did not get through
				GetWllRealtime(null, null);
			}
		}

		private int GetWlHistoricHealthLsid(int id, int type)
		{
			try
			{
				var sensor = sensorList.FirstOrDefault(i => i.LSID == id || i.ParentID == id);
				if (sensor != null)
				{
					var health = sensorList.FirstOrDefault(i => i.ParentID == sensor.ParentID && i.SensorType == type);
					if (health != null)
					{
						return health.LSID;
					}
				}
			}
			catch
			{ }
			return 0;
		}

		private class WllBroadcast
		{
			public string did { get; set; }
			public int ts { get; set; }
			public List<WllBroadcastRec> conditions { get; set; }
		}

		private class WllBroadcastRec
		{
			public string lsid { get; set; }
			public int txid { get; set; }
			public double wind_speed_last { get; set; }
			public int? wind_dir_last { get; set; }
			public int rain_size { get; set; }
			public double rain_rate_last { get; set; }
			public int rain_15_min { get; set; }
			public int rain_60_min { get; set; }
			public int rain_24_hr { get; set; }
			public int rain_storm { get; set; }
			public long rain_storm_start_at { get; set; }
			public int rainfall_daily { get; set; }
			public int rainfall_monthly { get; set; }
			public int rainfall_year { get; set; }
			public double wind_speed_hi_last_10_min { get; set; }
			public int wind_dir_at_hi_speed_last_10_min { get; set; }
		}

		// Response from WLL when asked to start multicasting
		private class WllBroadcastReqResponse
		{
			public WllBroadcastReqResponseData data { get; set; }
			public string error { get; set; }
		}

		private class WllBroadcastReqResponseData
		{
			public int broadcast_port { get; set; }
			public int duration { get; set; }
		}

		private class WllCurrent
		{
			public WllCurrentDevice data { get; set; }
			public string error { get; set; }
		}

		private class WllCurrentDevice
		{
			public string did { get; set; }
			public long ts { get; set; }
			public List<string> conditions { get; set; }  // We have no clue what these structures are going to be ahead of time
		}

		private class WllCurrentType1
		{
			public int lsid { get; set; }
			public int data_structure_type { get; set; }
			public int txid { get; set; }
			public double temp { get; set; }
			public double hum { get; set; }
			public double dew_point { get; set; }
			public double heat_index { get; set; }
			public double wind_chill { get; set; }
			public double thw_index { get; set; }
			public double thsw_index { get; set; }
			public double? wind_speed_last { get; set; }
			public int? wind_dir_last { get; set; }
			public double wind_speed_avg_last_1_min { get; set; }
			public double wind_dir_scalar_avg_last_1_min { get; set; }
			public double wind_speed_avg_last_2_min { get; set; }
			public double wind_dir_scalar_avg_last_2_min { get; set; }
			public double wind_speed_hi_last_2_min { get; set; }
			public int wind_dir_at_hi_speed_last_2_min { get; set; }
			public double? wind_speed_avg_last_10_min { get; set; }
			public double wind_dir_scalar_avg_last_10_min { get; set; }
			public double wind_speed_hi_last_10_min { get; set; }
			public int wind_dir_at_hi_speed_last_10_min { get; set; }
			public int rain_size { get; set; }
			public double rain_rate_last { get; set; }
			public double rain_rate_hi { get; set; }
			public double rainfall_last_15_min { get; set; }
			public double rain_rate_hi_last_15_min { get; set; }
			public double rainfall_last_60_min { get; set; }
			public double rainfall_last_24_hr { get; set; }
			public int? rain_storm { get; set; }
			public long? rain_storm_start_at { get; set; }
			public int solar_rad { get; set; }
			public double uv_index { get; set; }
			public int rx_state { get; set; }
			public uint trans_battery_flag { get; set; }
			public int rainfall_daily { get; set; }
			public int rainfall_monthly { get; set; }
			public int rainfall_year { get; set; }
			public int rain_storm_last { get; set; }
			public long rain_storm_last_start_at { get; set; }
			public long rain_storm_last_end_at { get; set; }
		}

		private class WllCurrentType2
		{
			public int lsid { get; set; }
			public int data_structure_type { get; set; }
			public int txid { get; set; }
			public double temp_1 { get; set; }
			public double temp_2 { get; set; }
			public double temp_3 { get; set; }
			public double temp_4 { get; set; }
			public double moist_soil_1 { get; set; }
			public double moist_soil_2 { get; set; }
			public double moist_soil_3 { get; set; }
			public double moist_soil_4 { get; set; }
			public double wet_leaf_1 { get; set; }
			public double wet_leaf_2 { get; set; }
			public int rx_state { get; set; }
			public uint trans_battery_flag { get; set; }
			public object this[string name]
			{
				get
				{
					Type myType = typeof(WllCurrentType2);
					PropertyInfo myPropInfo = myType.GetProperty(name);
					return myPropInfo.GetValue(this, null);
				}
			}
		}

		// WLL Current Baro
		private class WllCurrentType3
		{
			public int lsid { get; set; }
			public int data_structure_type { get; set; }
			public double bar_sea_level { get; set; }
			public double bar_trend { get; set; }
			public double bar_absolute { get; set; }
		}

		// WLL Current internal temp/hum
		private class WllCurrentType4
		{
			public int lsid { get; set; }
			public int data_structure_type { get; set; }
			public double temp_in { get; set; }
			public double hum_in { get; set; }
			public double dew_point_in { get; set; }
			public double heat_index_in { get; set; }
		}
	}
}