using ServiceStack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
			calculaterainrate = false;
			//cumulus.UseDataLogger = false;
			// WLL does not provide a forecast string, so use the Cumulus forecast
			cumulus.UseCumulusForecast = true;
			// WLL does not provide pressure trend strings
			cumulus.StationOptions.UseCumulusPresstrendstr = true;

			noET = false;
			// initialise the battery status
			TxBatText = "1-NA 2-NA 3-NA 4-NA 5-NA 6-NA 7-NA 8-NA";

			cumulus.LogMessage("Station type = Davis WLL");

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

			tmrRealtime = new System.Timers.Timer();
			tmrCurrent = new System.Timers.Timer();
			tmrBroadcastWatchdog = new System.Timers.Timer();
			tmrHealth = new System.Timers.Timer();

			wlHttpClient.Timeout = TimeSpan.FromSeconds(20); // 20 seconds for internet queries

			// used for kicking real time, and getting current conditions
			dogsBodyClient.Timeout = TimeSpan.FromSeconds(10); // 10 seconds for local queries
			dogsBodyClient.DefaultRequestHeaders.Add("Connection", "close");

			// The Davis leafwetness sensors send a decimal value via WLL (only integer available via VP2/Vue)
			cumulus.LeafWetDPlaces = 1;
			cumulus.LeafWetFormat = "F1";


			// If the user is using the default 10 minute Wind gust, always use gust data from the WLL - simple
			if (cumulus.StationOptions.PeakGustMinutes == 10)
			{
				CalcRecentMaxGust = false;
				checkWllGustValues = false;
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
				cumulus.LogConsoleMessage("*** No WeatherLink.com API details supplied. Cannot start historic downloads or retrieve health data", ConsoleColor.DarkCyan);
				useWeatherLinkDotCom = false;
			}
			else if (string.IsNullOrEmpty(cumulus.WllApiKey) || string.IsNullOrEmpty(cumulus.WllApiSecret))
			{
				// One of the API details is missing
				if (string.IsNullOrEmpty(cumulus.WllApiKey))
				{
					cumulus.LogMessage("WLL - Missing WeatherLink.com API Key");
					cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Key. Cannot start historic downloads or retrieve health data", ConsoleColor.Yellow);
				}
				else
				{
					cumulus.LogMessage("WLL - Missing WeatherLink.com API Secret");
					cumulus.LogConsoleMessage("*** Missing WeatherLink.com API Secret. Cannot start historic downloads or retrieve health data", ConsoleColor.Yellow);
				}
				useWeatherLinkDotCom = false;
			}

			if (useWeatherLinkDotCom)
			{
				// Get wl.com status
				GetSystemStatus();
			}

			// Perform Station ID checks - If we have API details!
			// If the Station ID is missing, this will populate it if the user only has one station associated with the API key
			if (useWeatherLinkDotCom && cumulus.WllStationId < 10)
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
			if (useWeatherLinkDotCom && cumulus.WllStationId < 10)
			{
				// API details supplied, but Station Id is still invalid - do not start the station up.
				cumulus.LogMessage("WLL - The WeatherLink.com API is enabled, but no Station Id has been configured, not starting the station. Please correct this and restart Cumulus");
				cumulus.LogConsoleMessage("The WeatherLink.com API is enabled, but no Station Id has been configured. Please correct this and restart Cumulus", ConsoleColor.Yellow);
				return;
			}


			// Now get the sensors associated with this station
			if (useWeatherLinkDotCom)
				GetAvailableSensors();

			// Perform zero-config
			// If it works - check IP address in config file and set/update if required
			// If it fails - just use the IP address from config file

			if (cumulus.WLLAutoUpdateIpAddress)
			{
				const string serviceType = "_weatherlinklive._tcp";
				var serviceBrowser = new ServiceBrowser();
				serviceBrowser.ServiceAdded += OnServiceAdded;
				serviceBrowser.ServiceRemoved += OnServiceRemoved;
				serviceBrowser.ServiceChanged += OnServiceChanged;
				serviceBrowser.QueryParameters.QueryInterval = cumulus.WllBroadcastDuration * 1000 * 4; // query at 4x the multicast time (default 20 mins)

				//Console.WriteLine($"Browsing for type: {serviceType}");
				serviceBrowser.StartBrowse(serviceType);

				cumulus.LogMessage("ZeroConf Service: Attempting to find WLL via mDNS...");

				// short wait for zero-config
				Thread.Sleep(1000);
			}
			else
			{
				cumulus.LogMessage($"ZeroConf Service: WLL auto-discovery is disabled");
			}


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
				tmrRealtime.Interval = cumulus.WllBroadcastDuration * 1000 / 3 * 2; // give the multi-casts a kick after 2/3 of the duration (default 200 secs)
				tmrRealtime.AutoReset = true;
				tmrRealtime.Start();

				// Create a current conditions thread to poll readings every 10 seconds as temperature updates every 10 seconds
				GetWllCurrent(null, null);
				tmrCurrent.Elapsed += GetWllCurrent;
				tmrCurrent.Interval = 10 * 1000;  // Every 10 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();

				if (useWeatherLinkDotCom)
				{
					// Get the archive data health to do the initial value populations
					GetWlHistoricHealth();
					// And reset the fetch interval to 2 minutes
					weatherLinkArchiveInterval = 2 * 60;
				}

				// short wait for real time response
				Thread.Sleep(1200);

				if (port == 0)
				{
					cumulus.LogMessage("WLL failed to get broadcast port via real time request, defaulting to 22222");
					port = cumulus.DavisOptions.TCPPort;
				}
				else if (port != cumulus.DavisOptions.TCPPort)
				{
					cumulus.LogMessage($"WLL Discovered broadcast port ({port}) is not the same as in the config ({cumulus.DavisOptions.TCPPort}), resetting config to match");
					cumulus.DavisOptions.TCPPort = port;
					cumulus.WriteIniFile();
				}

				// Create a broadcast listener
				Task.Run(() =>
				{
					using (var udpClient = new UdpClient())
					{
						udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
						udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
						udpClient.Client.ReceiveTimeout = 4000;  // We should get a message every 2.5 seconds
						var from = new IPEndPoint(0, 0);

						while (!stop)
						{
							try
							{
								var jsonBtye = udpClient.Receive(ref from);
								var jsonStr = Encoding.UTF8.GetString(jsonBtye);
								if (!stop) // we may be waiting for a broadcast when a shutdown is started
								{
									DecodeBroadcast(jsonStr);
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
						ip = cumulus.DavisOptions.IPAddr;
					}

					if (CheckIpValid(ip))
					{
						var urlRealtime = "http://" + ip + "/v1/real_time?duration=" + cumulus.WllBroadcastDuration;

						cumulus.LogDebugMessage($"GetWllRealtime: Sending GET real time request to WLL: {urlRealtime} ...");

						string responseBody;

						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlRealtime))
						{
							responseBody = await response.Content.ReadAsStringAsync();
							responseBody = responseBody.TrimEnd('\r', '\n');

							cumulus.LogDataMessage("GetWllRealtime: WLL response: " + responseBody);
						}

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
				ip = cumulus.DavisOptions.IPAddr;
			}

			if (CheckIpValid(ip))
			{
				var urlCurrent = $"http://{ip}/v1/current_conditions";

				// wait a random time of 0 to 5 seconds before making the request to try and avoid continued clashes with other software or instances of MX
				await Task.Delay(random.Next(0, 5000));

				cumulus.LogDebugMessage("GetWllCurrent: Waiting for lock");
				WebReq.Wait();
				cumulus.LogDebugMessage("GetWllCurrent: Has the lock");

				// The WLL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogDebugMessage($"GetWllCurrent: Sending GET current conditions request {retry} to WLL: {urlCurrent} ...");
					try
					{
						string responseBody;
						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlCurrent))
						{
							response.EnsureSuccessStatusCode();
							responseBody = await response.Content.ReadAsStringAsync();
							cumulus.LogDataMessage($"GetWllCurrent: response - {responseBody}");
						}

						DecodeCurrent(responseBody);
						if (startupDayResetIfRequired)
						{
							DoDayResetIfNeeded();
							startupDayResetIfRequired = false;
						}
						retry = 9;
					}
					catch (Exception ex)
					{
						retry++;
						cumulus.LogMessage("GetWllCurrent: Error processing WLL response");
						if (ex.InnerException == null)
							cumulus.LogMessage($"GetWllCurrent: Error: {ex.Message}");
						else
							cumulus.LogMessage($"GetWllCurrent: Error: {ex.InnerException.Message}");
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
										// i.e. we missed the high gust

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
						 * rec["rain_size"] - 0: Reserved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
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

					json = null;

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
							if (data1.trans_battery_flag.HasValue)
								SetTxBatteryStatus(data1.txid, data1.trans_battery_flag.Value);

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
									if (data1.hum.HasValue)
										DoOutdoorHumidity(Convert.ToInt32(data1.hum.Value), dateTime);

									if (data1.temp.HasValue)
										DoOutdoorTemp(ConvertTempFToUser(data1.temp.Value), dateTime);

									if (data1.dew_point.HasValue)
									DoOutdoorDewpoint(ConvertTempFToUser(data1.dew_point.Value), dateTime);

									if (!cumulus.StationOptions.CalculatedWC && data1.wind_chill.HasValue)
									{
										// use wind chill from WLL
										DoWindChill(ConvertTempFToUser(data1.wind_chill.Value), dateTime);
									}

									if (data1.thsw_index.HasValue)
									{
										THSWIndex = ConvertTempFToUser(data1.thsw_index.Value);
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
											if (!data1.temp.HasValue || data1.temp.Value == -99)
											{
												cumulus.LogDebugMessage($"WLL current: no valid Extra temperature value found [{data1.temp}] on TxId {data1.txid}");
											}
											else
											{
												cumulus.LogDebugMessage($"WLL current: using extra temp data from TxId {data1.txid}");

												DoExtraTemp(ConvertTempFToUser(data1.temp.Value), tempTxId);
											}

											if (cumulus.WllExtraHumTx[tempTxId - 1] && data1.hum.HasValue)
											{
												DoExtraHum(data1.hum.Value, tempTxId);
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
									int wdir = data1.wind_dir_last.HasValue ? data1.wind_dir_last.Value : 0;
									double wind = data1.wind_speed_last.HasValue ? data1.wind_speed_last.Value : 0;
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
								* rec["rain_size"] - 0: Reserved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
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

								if (data1.rain_size.HasValue)
								{
									switch (data1.rain_size.Value)
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
									}
								}

								// Rain data can be a bit out of date compared to the broadcasts (1 minute update), so only use storm data

								// All rainfall values supplied as *tip counts*
								//double rain = ConvertRainINToUser((double)rec["rainfall_year"]);
								//double rainrate = ConvertRainINToUser((double)rec["rain_rate_last"]);

								//if (rainrate < 0)
								//{
								//	rainrate = 0;
								//}

								//DoRain(rain, rainrate, dateTime);

								if (!data1.rain_storm.HasValue || !data1.rain_storm_start_at.HasValue || !data1.rain_size.HasValue)
								{
									cumulus.LogDebugMessage("WLL current: No rain storm values present");
								}
								else
								{
									try
									{
										StormRain = ConvertRainClicksToUser(data1.rain_storm.Value, data1.rain_size.Value) * cumulus.Calib.Rain.Mult;
										StartOfStorm = Utils.FromUnixTime(data1.rain_storm_start_at.Value);
									}
									catch (Exception ex)
									{
										cumulus.LogMessage($"WLL current: Error processing rain storm values on TxId {data1.txid}");
										cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
									}
								}
							}

							if (cumulus.WllPrimaryUV == data1.txid && data1.uv_index.HasValue)
							{
								try
								{
									cumulus.LogDebugMessage($"WLL current: using UV data from TxId {data1.txid}");
									DoUV(data1.uv_index.Value, dateTime);
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WLL current: Error processing UV value on TxId {data1.txid}");
									cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
								}
							}

							if (cumulus.WllPrimarySolar == data1.txid && data1.solar_rad.HasValue)
							{
								try
								{
									cumulus.LogDebugMessage($"WLL current: using solar data from TxId {data1.txid}");
									DoSolarRad(data1.solar_rad.Value, dateTime);
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
							if (data2.trans_battery_flag.HasValue)
								SetTxBatteryStatus(data2.txid, data2.trans_battery_flag.Value);

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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoLeafWetness(val.Value, 1);
								}
								if (cumulus.WllExtraLeafTx2 == data2.txid)
								{
									idx = "wet_leaf_" + cumulus.WllExtraLeafIdx2;
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoLeafWetness(val.Value, 2);
								}
							}
							catch (Exception e)
							{
								cumulus.LogMessage($"WLL current: Error processing LeafWetness txid={data2.txid}, idx={idx}");
								cumulus.LogDebugMessage($"WLL current: Exception: {e.Message}");
							}

							// Soil moisture
							if (cumulus.WllExtraSoilMoistureTx1 == data2.txid)
							{
								idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx1;
								try
								{
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilMoisture(val.Value, 1);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilMoisture(val.Value, 2);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilMoisture(val.Value, 3);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilMoisture(val.Value, 4);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilTemp(ConvertTempFToUser(val.Value), 1);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilTemp(ConvertTempFToUser(val.Value), 2);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilTemp(ConvertTempFToUser(val.Value), 3);
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
									var val = (double?)data2[idx];
									if (val.HasValue)
										DoSoilTemp(ConvertTempFToUser(val.Value), 4);
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
								if (data3.bar_sea_level.HasValue)
									DoPressure(ConvertPressINHGToUser(data3.bar_sea_level.Value), dateTime);
								// Altimeter from absolute
								if (data3.bar_absolute.HasValue)
								{
									StationPressure = ConvertPressINHGToUser(data3.bar_absolute.Value);
									// Or do we use calibration? The VP2 code doesn't?
									//StationPressure = ConvertPressINHGToUser(rec.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
									AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(ConvertUserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
								}
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
								if (data4.temp_in.HasValue)
									DoIndoorTemp(ConvertTempFToUser(data4.temp_in.Value));
							}
							catch (Exception ex)
							{
								cumulus.LogMessage("WLL current: Error processing indoor temp data");
								cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
							}

							try
							{
								if (data4.hum_in.HasValue)
									DoIndoorHumidity(Convert.ToInt32(data4.hum_in.Value));
							}
							catch (Exception ex)
							{
								cumulus.LogMessage("WLL current: Error processing indoor humidity data");
								cumulus.LogDebugMessage($"WLL current: Exception: {ex.Message}");
							}

							break;

						default:
							cumulus.LogDebugMessage($"WLL current: found an unknown transmitter type [{type}]!");
							break;
					}

					DoForecast(string.Empty, false);

					UpdateStatusPanel(DateTime.Now);
					UpdateMQTT();
				}

				// Now we have the primary data, calculate the derived data
				if (cumulus.StationOptions.CalculatedWC)
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

		private void OnServiceChanged(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('~', e.Announcement);
		}

		private void OnServiceRemoved(object sender, ServiceAnnouncementEventArgs e)
		{
			cumulus.LogMessage("ZeroConf Service: WLL service has been removed!");
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
				cumulus.LogMessage($"ZeroConf Service: WLL found, reporting its IP address as: {ipaddr}");
				if (cumulus.DavisOptions.IPAddr != ipaddr)
				{
					cumulus.LogMessage($"ZeroConf Service: WLL IP address has changed from {cumulus.DavisOptions.IPAddr} to {ipaddr}");
					if (cumulus.WLLAutoUpdateIpAddress)
					{
						cumulus.LogMessage($"ZeroConf Service: WLL changing Cumulus config to the new IP address {ipaddr}");
						cumulus.DavisOptions.IPAddr = ipaddr;
						cumulus.WriteIniFile();
					}
					else
					{
						cumulus.LogMessage($"ZeroConf Service: WLL ignoring new IP address {ipaddr} due to setting WLLAutoUpdateIpAddress");
					}
				}
			}
		}

		private double ConvertRainClicksToUser(double clicks, int size)
		{
			// 0: Reserved, 1: 0.01", 2: 0.2mm, 3: 0.1mm, 4: 0.001"
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
			cumulus.StationOptions.UseSpeedForAvgCalc = savedUseSpeedForAvgCalc;
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
				savedUseSpeedForAvgCalc = cumulus.StationOptions.UseSpeedForAvgCalc;
				cumulus.StationOptions.UseSpeedForAvgCalc = true;

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

			if (cumulus.WllStationId < 10)
			{
				const string msg = "No WeatherLink API station ID in the configuration";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage("GetWlHistoricData: " + msg);

			}

			//int passCount;
			//const int maxPasses = 4;

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = Utils.ToUnixTime(cumulus.LastUpdateTime);
			int endTime = unixDateTime;
			int unix24hrs = 24 * 60 * 60;

			// The API call is limited to fetching 24 hours of data
			if (unixDateTime - startTime > unix24hrs)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime + unix24hrs;
				maxArchiveRuns++;
			}

			cumulus.LogConsoleMessage($"Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime:s} to: {Utils.FromUnixTime(endTime):s}");
			cumulus.LogMessage($"GetWlHistoricData: Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime:s} to: {Utils.FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.WllApiKey },
				{ "station-id", cumulus.WllStationId.ToString() },
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

				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = wlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDebugMessage($"GetWlHistoricData: WeatherLink API Historic Response code: {responseCode}");
					cumulus.LogDataMessage($"GetWlHistoricData: WeatherLink API Historic Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var historyError = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"GetWlHistoricData: WeatherLink API Historic Error: {historyError.code}, {historyError.message}");
					cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.message}", ConsoleColor.Red);
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				histObj = responseBody.FromJson<WlHistory>();

				if (responseBody == "{}")
				{
					cumulus.LogMessage("GetWlHistoricData: WeatherLink API Historic: No data was returned. Check your Device Id.");
					cumulus.LogConsoleMessage(" - No historic data available");
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
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
					cumulus.LogMessage("GetWlHistoricData: Invalid historic message received");
					cumulus.LogDataMessage("GetWlHistoricData: Received: " + responseBody);
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetWlHistoricData:  Exception: " + ex.Message);
				if (ex.InnerException != null) {
					cumulus.LogMessage("GetWlHistoricData:  Inner Exception: " + ex.InnerException.Message);
				}
				cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
				return;
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				try
				{
					// Not all sensors may have the same number of records. We are using the WLL to create the historic data, the other sensors (AirLink) may have more or less records!
					// For the additional sensors, check if they have the same number of records as the WLL. If they do great, we just process the next record.
					// If the sensor has more or less historic records than the WLL, then we find the record (if any) that matches the WLL record timestamp

					var refData = sensorWithMostRecs.data[dataIndex].FromJsv<WlHistorySensorDataType13Baro>();
					var timestamp = Utils.FromUnixTime(refData.ts);

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
						ResetSunshineHours();
						midnightraindone = true;
					}

					DecodeHistoric(sensorWithMostRecs.data_structure_type, sensorWithMostRecs.sensor_type, sensorWithMostRecs.data[dataIndex]);

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

					// Log all the data
					cumulus.DoLogFile(timestamp, false);
					cumulus.LogMessage("GetWlHistoricData: Log file entry written");
					cumulus.MySqlRealtimeFile(999, false, timestamp);

					if (cumulus.StationOptions.LogExtraSensors)
					{
						cumulus.DoExtraLogFile(timestamp);
					}

					if (cumulus.airLinkOut != null || cumulus.airLinkIn != null)
					{
						cumulus.DoAirLinkLogFile(timestamp);
					}

					AddRecentDataWithAq(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
						OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);
					DoTrendValues(timestamp);

					if (cumulus.StationOptions.CalculatedET && timestamp.Minute == 0)
					{
						// Start of a new hour, and we want to calculate ET in Cumulus
						CalculateEvaoptranspiration(timestamp);
					}

					UpdateStatusPanel(timestamp);
					cumulus.AddToWebServiceLists(timestamp);


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
						var recordTs = Utils.FromUnixTime(data11.ts);

						// Temperature & Humidity
						if (cumulus.WllPrimaryTempHum == data11.tx_id)
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
								if (data11.hum_hi_at != 0 && data11.hum_hi != null)
								{
									ts = Utils.FromUnixTime(data11.hum_hi_at);
									DoOutdoorHumidity(Convert.ToInt32(data11.hum_hi), ts);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Humidity (high) data on TxId {data11.tx_id}");
								}

								// do low humidity
								if (data11.hum_lo_at != 0 && data11.hum_lo != null)
								{
									ts = Utils.FromUnixTime(data11.hum_lo_at);
									DoOutdoorHumidity(Convert.ToInt32(data11.hum_lo), ts);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Humidity (low) data on TxId {data11.tx_id}");
								}

								if (data11.hum_last != null)
								{
									// do current humidity
									DoOutdoorHumidity(Convert.ToInt32(data11.hum_last), recordTs);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Humidity data on TxId {data11.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing Primary humidity value on TxId {data11.tx_id}. Error: {ex.Message}");
							}

							// do temperature after humidity as DoOutdoorTemp contains dewpoint calculation (if user selected)
							try
							{
								if (data11.temp_last == -99)
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Primary temperature value found [-99] on TxId {data11.tx_id}");
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: using temp/hum data from TxId {data11.tx_id}");

									// do high temp
									if (data11.temp_hi_at != 0 && data11.temp_hi != null)
									{
										ts = Utils.FromUnixTime(data11.temp_hi_at);
										DoOutdoorTemp(ConvertTempFToUser((double)data11.temp_hi), ts);
									}
									else
									{
										cumulus.LogMessage($"WL.com historic: Warning, no valid Temperature (high) data on TxId {data11.tx_id}");
									}

									// do low temp
									if (data11.temp_lo_at != 0 && data11.temp_lo != null)
									{
										ts = Utils.FromUnixTime(data11.temp_lo_at);
										DoOutdoorTemp(ConvertTempFToUser((double)data11.temp_lo), ts);
									}
									else
									{
										cumulus.LogMessage($"WL.com historic: Warning, no valid Temperature (low) data on TxId {data11.tx_id}");
									}

									// do last temp
									if (data11.temp_last != null)
									{
										DoOutdoorTemp(ConvertTempFToUser((double)data11.temp_last), recordTs);

										// set the values for daily average, arch_int is in seconds, but always whole minutes
										tempsamplestoday += data11.arch_int / 60;
										TempTotalToday += ConvertTempFToUser(data11.temp_avg) * data11.arch_int / 60;

										// update chill hours
										if (OutdoorTemperature < cumulus.ChillHourThreshold)
										{
											// add interval minutes to chill hours - arch_int in seconds
											ChillHours += (data11.arch_int / 3600.0);
										}

										// update heating/cooling degree days
										UpdateDegreeDays(data11.arch_int / 60);
									}
									else
									{
										cumulus.LogMessage($"WL.com historic: Warning, no valid Temperature data on TxId {data11.tx_id}");
									}
								}
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing Primary temperature value on TxId {data11.tx_id}. Error: {ex.Message}");
							}


							try
							{
								// do high DP
								if (data11.dew_point_hi_at != 0 && data11.dew_point_hi != null)
								{
									ts = Utils.FromUnixTime(data11.dew_point_hi_at);
									DoOutdoorDewpoint(ConvertTempFToUser((double)data11.dew_point_hi), ts);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Dew Point (high) data on TxId {data11.tx_id}");
								}

								// do low DP
								if (data11.dew_point_lo_at != 0 && data11.dew_point_lo != null)
								{
									ts = Utils.FromUnixTime(data11.dew_point_lo_at);
									DoOutdoorDewpoint(ConvertTempFToUser((double)data11.dew_point_lo), ts);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Dew Point (low) data on TxId {data11.tx_id}");
								}

								// do last DP
								if (data11.dew_point_last != null)
								{
									DoOutdoorDewpoint(ConvertTempFToUser((double)data11.dew_point_last), recordTs);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Dew Point data on TxId {data11.tx_id}");
								}
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"WL.com historic: Error processing dew point value on TxId {data11.tx_id}. Error: {ex.Message}");
							}

							if (!cumulus.StationOptions.CalculatedWC)
							{
								// use wind chill from WLL - otherwise we calculate it at the end of processing the historic record when we have all the data
								try
								{
									// do low WC
									if (data11.wind_chill_lo_at != 0 && data11.wind_chill_lo != null)
									{
										ts = Utils.FromUnixTime(data11.wind_chill_lo_at);
										DoWindChill(ConvertTempFToUser((double)data11.wind_chill_lo), ts);
									}
									else
									{
										cumulus.LogMessage($"WL.com historic: Warning, no valid Wind Chill (low) data on TxId {data11.tx_id}");
									}

									// do last WC
									if (data11.wind_chill_last != null)
									{
										DoWindChill(ConvertTempFToUser((double)data11.wind_chill_last), recordTs);
									}
									else
									{
										cumulus.LogMessage($"WL.com historic: Warning, no valid Wind Chill data on TxId {data11.tx_id}");
									}
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
									if (data11.temp_last == -99 || data11.temp_last == null)
									{
										cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Extra temperature value on TxId {data11.tx_id}");
									}
									else
									{
										cumulus.LogDebugMessage($"WL.com historic: using extra temp data from TxId {data11.tx_id}");

										DoExtraTemp(ConvertTempFToUser((double)data11.temp_last), tempTxId);
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
									if (data11.hum_last != null)
									{
										DoExtraHum((double)data11.hum_last, tempTxId);
									}
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
								if (data11.wind_speed_hi != null && data11.wind_speed_hi_dir != null && data11.wind_speed_avg != null)
								{
									cumulus.LogDebugMessage($"WL.com historic: using wind data from TxId {data11.tx_id}");
									DoWind(ConvertWindMPHToUser((double)data11.wind_speed_hi), (int)data11.wind_speed_hi_dir, ConvertWindMPHToUser((double)data11.wind_speed_avg), recordTs);
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Wind data on TxId {data11.tx_id}");
								}

								if (data11.wind_speed_avg != null)
								{
									WindAverage = ConvertWindMPHToUser((double)data11.wind_speed_avg) * cumulus.Calib.WindSpeed.Mult;

									// add in 'archivePeriod' minutes worth of wind speed to windrun
									int interval = data11.arch_int / 60;
									WindRunToday += ((WindAverage * WindRunHourMult[cumulus.Units.Wind] * interval) / 60.0);
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: Warning, no valid Wind data (avg) on TxId {data11.tx_id}");
								}
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


							// The WL API v2 does not provide any running totals for rainfall, only  :(
							// So we will have to add the interval data to the running total and hope it all works out!

							try
							{
								if (data11.rain_rate_hi_at != 0 && data11.rainfall_clicks != null && data11.rain_rate_hi_clicks != null)
								{
									cumulus.LogDebugMessage($"WL.com historic: using rain data from TxId {data11.tx_id}");

									var rain = ConvertRainClicksToUser((double)data11.rainfall_clicks, data11.rain_size);
									var rainrate = ConvertRainClicksToUser((double)data11.rain_rate_hi_clicks, data11.rain_size);
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
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Rain data on TxId {data11.tx_id}");
								}
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
								if (data11.uv_index_avg != null)
								{
									cumulus.LogDebugMessage($"WL.com historic: using UV data from TxId {data11.tx_id}");

									DoUV((double)data11.uv_index_avg, recordTs);
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid UV data on TxId {data11.tx_id}");
								}
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
							 * "et" (inches) - ET field is populated in the ISS archive records, which may not be the same as the solar
							 */
							try
							{
								if (data11.solar_rad_avg != null)
								{
									cumulus.LogDebugMessage($"WL.com historic: using solar data from TxId {data11.tx_id}");
									DoSolarRad((int)data11.solar_rad_avg, recordTs);

									// add in archive period worth of sunshine, if sunny - arch_int in seconds
									if ((SolarRad > CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100.00) && (SolarRad >= cumulus.SolarOptions.SolarMinimum))
									{
										SunshineHours += (data11.arch_int / 3600.0);
									}
								}
								else
								{
									cumulus.LogMessage($"WL.com historic: Warning, no valid Solar data on TxId {data11.tx_id}");
								}

								if (data11.et != null && !cumulus.StationOptions.CalculatedET)
								{
									// wl.com ET is only available in record the start of each hour.
									// The number is the total for the one hour period.
									// This is unlike the existing VP2 when the ET is an annual running total
									// So we try and mimic the VP behaviour
									var newET = AnnualETTotal + ConvertRainINToUser((double)data11.et);
									cumulus.LogDebugMessage($"WLL DecodeHistoric: Adding {ConvertRainINToUser((double)data11.et):F3} to ET");
									DoET(newET, recordTs);
								}
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

								cumulus.LogDebugMessage($"WL.com historic: found Leaf/Soil data on TxId {data13.tx_id}");

								// We are relying on user configuration here, trap any errors
								try
								{
									if (cumulus.WllExtraLeafTx1 == data13.tx_id)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx1;
										if (data13[idx] != null)
											DoLeafWetness((double)data13[idx], 1);
									}
									if (cumulus.WllExtraLeafTx2 == data13.tx_id)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx2;
										if (data13[idx] != null)
											DoLeafWetness((double)data13[idx], 2);
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, LeafWetness txid={data13.tx_id}, idx={idx}: {e.Message}");
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
									if (cumulus.WllExtraSoilMoistureTx1 == data13.tx_id)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx1;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} on TxId {data13.tx_id}");
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
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} on TxId {data13.tx_id}");
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
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} on TxId {data13.tx_id}");
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
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} on TxId {data13.tx_id}");
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
									if (cumulus.WllExtraSoilTempTx1 == data13.tx_id)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx1;
										if (data13[idx] == null)
										{
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} on TxId {data13.tx_id}");
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
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} on TxId {data13.tx_id}");
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
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} on TxId {data13.tx_id}");
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
											cumulus.LogDebugMessage($"WL.com historic: Warning, no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} on TxId {data13.tx_id}");
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
									DateTime ts;
									// check the high
									if (data13baro.bar_hi_at != 0 && data13baro.bar_hi != null)
									{
										ts = Utils.FromUnixTime(data13baro.bar_hi_at);
										DoPressure(ConvertPressINHGToUser((double)data13baro.bar_hi), ts);
									}
									else
									{
										cumulus.LogMessage("WL.com historic: Warning, no valid Baro data (high)");
									}
									// check the low
									if (data13baro.bar_lo_at != 0 && data13baro.bar_lo != null)
									{
										ts = Utils.FromUnixTime(data13baro.bar_lo_at);
										DoPressure(ConvertPressINHGToUser((double)data13baro.bar_lo), ts);
									}
									else
									{
										cumulus.LogMessage("WL.com historic: Warning, no valid Baro data (high)");
									}

									if (data13baro.bar_sea_level != null)
									{
										// leave it at current value
										ts = Utils.FromUnixTime(data13baro.ts);
										DoPressure(ConvertPressINHGToUser((double)data13baro.bar_sea_level), ts);
									}
									else
									{
										cumulus.LogMessage("WL.com historic: Warning, no valid Baro data (high)");
									}

									// Altimeter from absolute
									if (data13baro.bar_absolute != null)
									{
										StationPressure = ConvertPressINHGToUser((double)data13baro.bar_absolute);
										// Or do we use calibration? The VP2 code doesn't?
										//StationPressure = ConvertPressINHGToUser(data.Value<double>("bar_absolute")) * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
										AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(ConvertUserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
									}
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing baro reading. Error: {ex.Message}");
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
										DoIndoorTemp(ConvertTempFToUser((double)data13temp.temp_in_last));
									}
									else
									{
										cumulus.LogMessage("WL.com historic: Warning, no valid Inside Temperature");
									}
								}
								catch (Exception ex)
								{
									cumulus.LogMessage($"WL.com historic: Error processing temp-in reading. Error: {ex.Message}]");
								}


								try
								{
									if (data13temp.hum_in_last != null)
									{
										DoIndoorHumidity(Convert.ToInt32(data13temp.hum_in_last));
									}
									else
									{
										cumulus.LogMessage("WL.com historic: Warning, no valid Inside Humidity");
									}
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

					var dat = Utils.FromUnixTime(data15.firmware_version);
					DavisFirmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");

					var battV = data15.battery_voltage / 1000.0;
					ConBatText = battV.ToString("F2");
					// Allow voltage to drop to 1.35V per cell before triggering the alarm. This should leave a good reserve without changing them too often
					// 1.35 * 4 = 5.4
					if (battV < 5.4)
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
			else if (sensor.data_structure_type == 11 || sensor.data_structure_type == 13)
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
						type= sensor.data_structure_type == 11 ? "ISS" : "Soil/Leaf";

					var data = sensor.data.Last().FromJsv<WlHistoryHealthType11_13>();

					cumulus.LogDebugMessage($"WLL Health - found health data for {type} device TxId = {data.tx_id}");

					// Save the archive interval
					//weatherLinkArchiveInterval = data.Value<int>("arch_int");

					// Check battery state 0=Good, 1=Low
					SetTxBatteryStatus(data.tx_id, data.trans_battery_flag);
					if (data.trans_battery_flag == 1)
					{
						cumulus.LogMessage($"WLL WARNING: Battery voltage is low in TxId {data.tx_id}");
					}
					else
					{
						cumulus.LogDebugMessage($"WLL Health: {type} {data.tx_id}: Battery state is OK");
					}

					//DavisTotalPacketsReceived[txid] = ;  // Do not have a value for this
					DavisTotalPacketsMissed[data.tx_id] = data.error_packets;
					DavisNumCRCerrors[data.tx_id] = data.error_packets;
					DavisNumberOfResynchs[data.tx_id] = data.resynchs;
					DavisMaxInARow[data.tx_id] = data.good_packets_streak;
					DavisReceptionPct[data.tx_id] = data.reception;
					DavisTxRssi[data.tx_id] = data.rssi;

					var logMsg = $"WLL Health: {type} {data.tx_id}: Errors={DavisTotalPacketsMissed[data.tx_id]}, CRCs={DavisNumCRCerrors[data.tx_id]}, Resyncs={DavisNumberOfResynchs[data.tx_id]}, Streak={DavisMaxInARow[data.tx_id]}, %={DavisReceptionPct[data.tx_id]}, RSSI={DavisTxRssi[data.tx_id]}";
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
						var newET = AnnualETTotal + ConvertRainINToUser((double)data.et);
						cumulus.LogDebugMessage($"WLL Health: Adding {ConvertRainINToUser((double)data.et):F3} to ET");
						DoET(newET, DateTime.Now);
					}
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

		// Extracts health information from the last archive record
		private void GetWlHistoricHealth()
		{
			cumulus.LogMessage("WLL Health: Get WL.com Historic Data");

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("WLL Health: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				const string msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogConsoleMessage("GetWlHistoricHealth: " + msg);
				cumulus.LogMessage($"WLL Health: {msg}, aborting!");
				return;
			}

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - weatherLinkArchiveInterval;
			int endTime = unixDateTime;

			cumulus.LogDebugMessage($"WLL Health: Downloading the historic record from WL.com from: {Utils.FromUnixTime(startTime):s} to: {Utils.FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.WllApiKey },
				{ "station-id", cumulus.WllStationId.ToString() },
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
				string responseBody;
				int responseCode;

				using (HttpResponseMessage response = wlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDataMessage($"WLL Health: WeatherLink API Response: {responseCode} - {responseBody}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"WLL Health: WeatherLink API Error: {errObj.code}, {errObj.message}");
					// Get wl.com status
					GetSystemStatus();
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogMessage("WLL Health: WeatherLink API: No data was returned. Check your Device Id.");
					cumulus.LastUpdateTime = Utils.FromUnixTime(endTime);
					// Get wl.com status
					GetSystemStatus();
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
								if (cumulus.airLinkOut != null)
									cumulus.airLinkOut.DecodeWlApiHealth(sensor, true);
								break;
							// AirLink Indoor
							case 506 when lsid == alInHealthLsid:
								// Pass AirLink historic record to the AirLink module to process
								if (cumulus.airLinkIn != null)
									cumulus.airLinkIn.DecodeWlApiHealth(sensor, true);
								break;
							// WLL or ISS
							case 504:
							case int n when (n >= 23 && n < 100):
								// Davis don't make this easy! Either a...
								// 504 - WLL
								//  23 - ISS VP2, Cabled (6322C)
								//  24 - ISS VP2 Plus, Cabled (6327C)
								//  27 - ISS VP2, Cabled, Metric (6322CM)
								//  28 - ISS VP2 Plus, Cabled, Metric (6327CM)
								//  37 - Vue, wireless (6357)
								//  43 - ISS VP2, wireless (6152)
								//  44 - ISS VP2, 24hr fan, wireless (6153)
								//  45 - ISS VP2 Plus, wireless (6162)
								//  46 - ISS VP2 Plus, 24hr fan, wireless (6163)
								//  48 - ISS VP2, wireless (6322)
								//  49 - ISS VP2, 24hr fan, wireless (6323)
								//  50 - ISS VP2 Plus, wireless (6327)
								//  51 - ISS VP2 Plus, 24hr fan, wireless (6328)
								//  55 - ISS
								//  56 - Leaf/Soil
								//  76 - ISS VP2, 24hr fan, wireless, metric (6323M)
								//  77 - ISS VP2, 24hr fan, wireless, OV (6323OV)
								//  78 - ISS VP2, wireless, metric (6322M)
								//  79 - ISS VP2, wireless, OV (6322OV)
								//  80 - ISS VP2 Plus, 24hr fan, wireless, metric (6328M)
								//  81 - ISS VP2 Plus, 24hr fan, wireless, OV (6328OV)
								//  82 - ISS VP2 Plus, wireless metric (6327M)
								//  83 - ISS VP2 Plus, wireless, OV (6327OV)
								//  84 - Vue, wireless, metric (6357M)
								//  85 - Vue, wireless, OV (6357OV)
								DecodeWlApiHealth(sensor, true);
								break;
							default:
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
			var unixDateTime = Utils.ToUnixTime(DateTime.Now);

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
				string responseBody;
				int responseCode;

				using (HttpResponseMessage response = wlHttpClient.GetAsync(stationsUrl.ToString()).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					var resp = System.Text.RegularExpressions.Regex.Replace(responseBody, "user_email\":\"[^\"]*\"", "user_email\":\"<<email>>\"");
					cumulus.LogDebugMessage($"WLLStations: WeatherLink API Response: {responseCode}: {resp}");
				}

				if (responseCode != 200)
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
					if (station.station_id == cumulus.WllStationId)
					{
						cumulus.LogDebugMessage($"WLLStations: Setting WLL parent ID = {station.gateway_id}");
						cumulus.WllParentId = station.gateway_id;

						if (station.recording_interval != cumulus.logints[cumulus.DataLogInterval])
						{
							cumulus.LogMessage($"WLLStations: - Cumulus log interval {cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station.recording_interval}");
						}
					}
				}
				if (stationsObj.stations.Count > 1 && cumulus.WllStationId < 10)
				{
					if (logToConsole)
						cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your WLL configuration to enable history downloads.");
				}
				else if (stationsObj.stations.Count == 1 && cumulus.WllStationId != stationsObj.stations[0].station_id)
				{
					cumulus.LogMessage($"WLLStations: Only found 1 WeatherLink station, using id = {stationsObj.stations[0].station_id}");
					cumulus.WllStationId = stationsObj.stations[0].station_id;
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
			var unixDateTime = Utils.ToUnixTime(DateTime.Now);

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

			WlSensorList sensorsObj = new WlSensorList();

			try
			{
				// We want to do this synchronously
				string responseBody;
				int responseCode;

				using (HttpResponseMessage response = wlHttpClient.GetAsync(stationsUrl.ToString()).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDebugMessage($"GetAvailableSensors: WeatherLink API Response: {responseCode}: {responseBody}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"GetAvailableSensors: WeatherLink API Error: {errObj.code} - {errObj.message}");
					return;
				}

				sensorsObj = responseBody.FromJson<WlSensorList>();
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("GetAvailableSensors: WeatherLink API exception: " + ex.Message);
			}

			// Sensor types we are interested in...
			// 323 = Outdoor AirLink
			// 326 = Indoor AirLink
			// 504 = WLL Health
			// 506 = AirLink Health
			var types = new[] { 45, 323, 326, 504, 506 };
			foreach (var sensor in sensorsObj.sensors)
			{
				try
				{
					cumulus.LogDebugMessage($"GetAvailableSensors: Found WeatherLink Sensor type={sensor.sensor_type}, lsid={sensor.lsid}, station_id={sensor.station_id}, name={sensor.product_name}, parentId={sensor.parent_device_id}, parent={sensor.parent_device_name}");

					if (types.Contains(sensor.sensor_type) || sensor.category == "ISS")
					{
						var wlSensor = new WlSensor(sensor.sensor_type, sensor.lsid, sensor.parent_device_id, sensor.product_name, sensor.parent_device_name);
						sensorList.Add(wlSensor);
						if (wlSensor.SensorType == 323 && sensor.station_id == cumulus.AirLinkOutStationId)
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Outdoor LSID to {wlSensor.LSID}");
							cumulus.airLinkOutLsid = wlSensor.LSID;
						}
						else if (wlSensor.SensorType == 326 && sensor.station_id == cumulus.AirLinkInStationId)
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Indoor LSID to {wlSensor.LSID}");
							cumulus.airLinkInLsid = wlSensor.LSID;
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage("GetAvailableSensors: Processing sensors exception: " + ex.Message);
				}
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
				cumulus.DataStoppedAlarm.LastError = $"No broadcast data received from the WLL for {tmrBroadcastWatchdog.Interval / 1000} seconds";
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

		private void GetSystemStatus()
		{
			WlComSystemStatus status;
			try
			{
				string responseBody;
				int responseCode;

				cumulus.LogDebugMessage("GetSystemStatus: Getting WeatherLink.com system status");

				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = wlHttpClient.GetAsync("https://0886445102835570.hostedstatus.com/1.0/status/600712dea9c1290530967bc6").Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDebugMessage($"GetSystemStatus: WeatherLink.com system status Response code: {responseCode}");
					cumulus.LogDataMessage($"GetSystemStatus: WeatherLink.com system status Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogMessage($"GetSystemStatus: WeatherLink.com system status Error: {responseCode}");
					cumulus.LogConsoleMessage($" - Error {responseCode}");
					return;
				}

				status = responseBody.FromJson<WlComSystemStatus>();

				if (responseBody == "{}")
				{
					cumulus.LogMessage("GetSystemStatus: WeatherLink.com system status: No data was returned.");
					return;
				}
				else if (status != null)
				{
					string msg;
					if (status.result.status_overall.status_code != 100)
					{
						msg = status.ToString(true);
						cumulus.LogMessage(msg);
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
					cumulus.LogMessage("GetSystemStatus: Something went wrong!");
				}

			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetSystemStatus: Exception: " + ex);
			}

			return;
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

		// Response from WLL when asked to start multi-casting
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
			public double? temp { get; set; }
			public double? hum { get; set; }
			public double? dew_point { get; set; }
			public double? heat_index { get; set; }
			public double? wind_chill { get; set; }
			public double? thw_index { get; set; }
			public double? thsw_index { get; set; }
			public double? wind_speed_last { get; set; }
			public int? wind_dir_last { get; set; }
			public double? wind_speed_avg_last_1_min { get; set; }
			public double? wind_dir_scalar_avg_last_1_min { get; set; }
			public double? wind_speed_avg_last_2_min { get; set; }
			public double? wind_dir_scalar_avg_last_2_min { get; set; }
			public double? wind_speed_hi_last_2_min { get; set; }
			public int? wind_dir_at_hi_speed_last_2_min { get; set; }
			public double? wind_speed_avg_last_10_min { get; set; }
			public double? wind_dir_scalar_avg_last_10_min { get; set; }
			public double? wind_speed_hi_last_10_min { get; set; }
			public int? wind_dir_at_hi_speed_last_10_min { get; set; }
			public int? rain_size { get; set; }
			public double? rain_rate_last { get; set; }
			public double? rain_rate_hi { get; set; }
			public double? rainfall_last_15_min { get; set; }
			public double? rain_rate_hi_last_15_min { get; set; }
			public double? rainfall_last_60_min { get; set; }
			public double? rainfall_last_24_hr { get; set; }
			public int? rain_storm { get; set; }
			public long? rain_storm_start_at { get; set; }
			public int? solar_rad { get; set; }
			public double? uv_index { get; set; }
			public int? rx_state { get; set; }
			public uint? trans_battery_flag { get; set; }
			public int? rainfall_daily { get; set; }
			public int? rainfall_monthly { get; set; }
			public int? rainfall_year { get; set; }
			public int? rain_storm_last { get; set; }
			public long? rain_storm_last_start_at { get; set; }
			public long? rain_storm_last_end_at { get; set; }
		}

		private class WllCurrentType2
		{
			public int lsid { get; set; }
			public int data_structure_type { get; set; }
			public int txid { get; set; }
			public double? temp_1 { get; set; }
			public double? temp_2 { get; set; }
			public double? temp_3 { get; set; }
			public double? temp_4 { get; set; }
			public double? moist_soil_1 { get; set; }
			public double? moist_soil_2 { get; set; }
			public double? moist_soil_3 { get; set; }
			public double? moist_soil_4 { get; set; }
			public double? wet_leaf_1 { get; set; }
			public double? wet_leaf_2 { get; set; }
			public int rx_state { get; set; }
			public uint? trans_battery_flag { get; set; }
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
			public double? bar_sea_level { get; set; }
			public double? bar_trend { get; set; }
			public double? bar_absolute { get; set; }
		}

		// WLL Current internal temp/hum
		private class WllCurrentType4
		{
			public int lsid { get; set; }
			public int data_structure_type { get; set; }
			public double? temp_in { get; set; }
			public double? hum_in { get; set; }
			public double? dew_point_in { get; set; }
			public double? heat_index_in { get; set; }
		}
	}
}
