using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Linq;
using System.Timers;
using System.Net.Http;
using System.Net.Sockets;
using Tmds.MDns;
using System.Net;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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
		private int MaxArchiveRuns = 1;
		private static readonly HttpClientHandler HistoricHttpHandler = new HttpClientHandler();
		private readonly HttpClient WlHttpClient = new HttpClient(HistoricHttpHandler);
		private readonly HttpClient dogsBodyClient = new HttpClient();
		private readonly bool checkWllGustValues;
		private bool broadcastReceived = false;
		private int weatherLinkArchiveInterval = 16 * 60; // Used to get historic Health, 16 minutes in seconds only for initial fetch after load
		private bool wllVoltageLow = false;
		private bool stop = false;

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

			// used for kicking real time, and getting current conditions
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

			DateTime tooold = new DateTime(0);

			if ((cumulus.LastUpdateTime <= tooold) || !cumulus.UseDataLogger)
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
				// Read the data from the logger
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

				// Get the archive data health to do the initial value populations
				GetWlHistoricHealth();
				// And reset the fetch interval to 2 minutes
				weatherLinkArchiveInterval = 2 * 60;

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
								var recvBuffer = udpClient.Receive(ref @from);
								if (!stop) // we may be waiting for a broadcast when a shutdown is started
								{
									DecodeBroadcast(Encoding.UTF8.GetString(recvBuffer));
								}
								recvBuffer = null;
							}
							catch (SocketException exp)
							{
								if (exp.SocketErrorCode == SocketError.TimedOut)
								{
									multicastsBad++;
									var msg = string.Format("WLL: Missed a WLL broadcast message. Percentage good packets {0}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100).ToString("F2"), multicastsBad, multicastsGood);
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

				if (cumulus.UseDataLogger)
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

			cumulus.LogDebugMessage("Lock: GetWllRealtime waiting for lock");
			WebReq.Wait();
			cumulus.LogDebugMessage("Lock: GetWllRealtime has the lock");

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

						cumulus.LogDebugMessage($"Sending GET realtime request to WLL: {urlRealtime} ...");

						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlRealtime))
						{
							//response.EnsureSuccessStatusCode();
							var responseBody = await response.Content.ReadAsStringAsync();
							responseBody = responseBody.TrimEnd('\r', '\n');

							cumulus.LogDataMessage("WLL Real_time response: " + responseBody);

							//Console.WriteLine($" - Realtime response: {responseBody}");
							var jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
							var err = String.IsNullOrEmpty(jObject.Value<string>("error")) ? "OK" : jObject["error"].Value<string>("code");
							port = jObject["data"].Value<int>("broadcast_port");
							duration = jObject["data"].Value<int>("duration");
							cumulus.LogDebugMessage($"GET realtime request WLL response Code: {err}, Port: {jObject["data"].Value<string>("broadcast_port")}");
							if (cumulus.WllBroadcastDuration != duration)
							{
								cumulus.LogMessage($"WLL broadcast duration {duration} does not match requested duration {cumulus.WllBroadcastDuration}, continuing to use {cumulus.WllBroadcastDuration}");
							}
							if (cumulus.WllBroadcastPort != port)
							{
								cumulus.LogMessage($"WLL broadcast port {port} does not match default {cumulus.WllBroadcastPort}, resetting to {port}");
								cumulus.WllBroadcastPort = port;
							}
						}
					}
					else
					{
						cumulus.LogMessage($"WLL realtime: Invalid IP address: {ip}");
					}
					retry = 0;
				}
				catch (Exception exp)
				{
					retry--;
					cumulus.LogDebugMessage("GetRealtime(): Exception Caught!");
					cumulus.LogDebugMessage($"Message :{exp.Message}");
					Thread.Sleep(2000);
				}
			} while (retry > 0);

			cumulus.LogDebugMessage("Lock: GetWllRealtime releasing lock");
			WebReq.Release();
		}

		private async void GetWllCurrent(object source, ElapsedEventArgs e)
		{
			string ip;
			var retry = 2;

			lock (threadSafer)
			{
				ip = cumulus.VP2IPAddr;
			}

			if (CheckIpValid(ip))
			{

				var urlCurrent = $"http://{ip}/v1/current_conditions";

				cumulus.LogDebugMessage("Lock: GetWllCurrent waiting for lock");
				WebReq.Wait();
				cumulus.LogDebugMessage("Lock: GetWllCurrent has the lock");

				// The WLL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogDebugMessage($"Sending GET current conditions request to WLL: {urlCurrent} ...");
					// First time run it synchronously
					// Call asynchronous network methods in a try/catch block to handle exceptions
					try
					{
						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlCurrent))
						{
							response.EnsureSuccessStatusCode();
							var responseBody = await response.Content.ReadAsStringAsync();
							//Console.WriteLine($" - Current conds response: {responseBody}");
							// sanity check
							if (responseBody.StartsWith("{\"data\":{\"did\":"))
							{
								DecodeCurrent(responseBody);
								if (startupDayResetIfRequired)
								{
									DoDayResetIfNeeded();
									startupDayResetIfRequired = false;
								}
							}
							else
							{
								cumulus.LogMessage("Invalid current conditions message received");
								cumulus.LogDebugMessage("Received: " + responseBody);
							}
							retry = 0;
						}
					}
					catch (Exception exp)
					{
						retry--;
						cumulus.LogDebugMessage("GetWllCurrent(): Exception Caught!");
						cumulus.LogDebugMessage($"Message :" + exp.Message);

						//Console.ForegroundColor = ConsoleColor.Red;
						//Console.WriteLine("GetWllCurrent():Exception Caught!");
						//Console.ForegroundColor = ConsoleColor.White;
						Thread.Sleep(2000);
					}
				} while (retry > 0);

				cumulus.LogDebugMessage("Lock: GetWllCurrent releasing lock");
				WebReq.Release();
			}
			else
			{
				cumulus.LogMessage($"WLL current: Invalid IP address: {ip}");
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
					var data = JObject.Parse(broadcastJson).GetValue("conditions");

					// The WLL sends the timestamp in Unix ticks, and in UTC
					// rather than rely on the WLL clock being correct, we will use our local time
					//var dateTime = FromUnixTime((int)data["ts"]);
					var dateTime = DateTime.Now;
					foreach (var rec in data)
					{
						var txid = rec.Value<int>("txid");

						// Wind - All values in MPH
						/* Available fields:
						 * rec["wind_speed_last"]
						 * rec["wind_dir_last"]
						 * rec["wind_speed_hi_last_10_min"]
						 * rec["wind_dir_at_hi_speed_last_10_min"]
						 */
						if (cumulus.WllPrimaryWind == txid)
						{
							if (string.IsNullOrEmpty(rec.Value<string>("wind_speed_last")))
							{
								cumulus.LogDebugMessage($"WLL broadcast: no valid wind speed found [speed={rec.Value<string>("wind_speed_last")}, dir= {rec.Value<string>("wind_dir_last")}] on TxId {txid}");
							}
							else
							{
								// WLL BUG: The WLL sends a null wind direction for calm when the avg speed falls to zero, we use zero
								int windDir = string.IsNullOrEmpty(rec.Value<string>("wind_dir_last")) ? 0 : rec.Value<int>("wind_dir_last");

								// No average in the broadcast data, so use last value from current - allow for calibration
								DoWind(ConvertWindMPHToUser(rec.Value<double>("wind_speed_last")), windDir, WindAverage / cumulus.WindSpeedMult, dateTime);

								var gust = ConvertWindMPHToUser(rec.Value<double>("wind_speed_hi_last_10_min"));
								var gustCal = gust * cumulus.WindGustMult;
								if (checkWllGustValues)
								{
									if (gust > RecentMaxGust)
									{
										// See if the station 10 min high speed is higher than our current 10-min max
										// ie we missed the high gust

										if (CheckHighGust(gustCal, rec.Value<int>("wind_dir_at_hi_speed_last_10_min"), dateTime))
										{
											cumulus.LogDebugMessage("Set max gust from broadcast 10 min high value: " + gustCal.ToString(cumulus.WindFormat) + " was: " + RecentMaxGust.ToString(cumulus.WindFormat));

											// add to recent values so normal calculation includes this value
											WindRecent[nextwind].Gust = gust; // use uncalibrated value
											WindRecent[nextwind].Speed = WindAverage / cumulus.WindSpeedMult;
											WindRecent[nextwind].Timestamp = dateTime;
											nextwind = (nextwind + 1) % cumulus.MaxWindRecent;

											RecentMaxGust = gustCal;
										}
									}
								}
								else if (!CalcRecentMaxGust)
								{
									if (CheckHighGust(gust, rec.Value<int>("wind_dir_at_hi_speed_last_10_min"), dateTime))
									{
										RecentMaxGust = gustCal;
									}
								}
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
						if (cumulus.WllPrimaryRain != txid) continue;

						if (string.IsNullOrEmpty(rec.Value<string>("rainfall_year")) || string.IsNullOrEmpty(rec.Value<string>("rain_rate_last")) || string.IsNullOrEmpty(rec.Value<string>("rain_size")))
						{
							cumulus.LogDebugMessage($"WLL broadcast: no valid rainfall found [total={rec.Value<string>("rainfall_year")}, rate= {rec.Value<string>("rain_rate_last")}] on TxId {txid}");
						}
						else
						{
							var rain = ConvertRainClicksToUser(rec.Value<double>("rainfall_year"), rec.Value<int>("rain_size"));
							var rainrate = ConvertRainClicksToUser(rec.Value<double>("rain_rate_last"), rec.Value<int>("rain_size"));

							if (rainrate < 0)
							{
								rainrate = 0;
							}

							DoRain(rain, rainrate, dateTime);
						}
					}

					data = null;

					UpdateStatusPanel(DateTime.Now);
					UpdateMQTT();

					broadcastReceived = true;
					DataStopped = false;
					multicastsGood++;
				}
				else
				{
					multicastsBad++;
					var msg = string.Format("WLL broadcast: Invalid payload in message. Percentage good packets {0}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100).ToString("F2"), multicastsBad, multicastsGood);
					cumulus.LogMessage(msg);
				}
			}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeBroadcast(): Exception Caught!");
				cumulus.LogDebugMessage($"Message :" + exp.Message);
				multicastsBad++;
				var msg = string.Format("WLL broadcast: Error processing broadcast. Percentage good packets {0}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100).ToString("F2"), multicastsBad, multicastsGood);
				cumulus.LogMessage(msg);
			}
		}

		private void DecodeCurrent(string currentJson)
		{
			try
			{
				cumulus.LogDataMessage("WLL Current conditions: " + currentJson);

				// Convert JSON string to an object
				//var data = Newtonsoft.Json.Linq.JObject.Parse(currentJson)["data"]["conditions"];
				var data = Newtonsoft.Json.Linq.JObject.Parse(currentJson).SelectToken("data.conditions");

				// The WLL sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the WLL clock being correct, we will use our local time
				//var dateTime = FromUnixTime(data.Value<int>("ts"));
				var dateTime = DateTime.Now;
				var localSensorContactLost = false;

				foreach (var rec in data)
				{
					var type = rec.Value<int>("data_structure_type");
					int txid;
					string idx = "";
					uint batt;

					switch (type)
					{
						case 1: // ISS
							txid = rec.Value<int>("txid");

							cumulus.LogDebugMessage($"WLL current: found ISS data on TxId {txid}");

							// Battery
							batt = rec.Value<uint>("trans_battery_flag");
							SetTxBatteryStatus(txid, batt);

							if (rec.Value<int>("rx_state") == 2)
							{
								localSensorContactLost = true;
								cumulus.LogMessage($"Warning: Sensor contact lost TxId {txid}; ignoring data from this ISS");
							}
							else
							{
								// Temperature & Humidity
								if (cumulus.WllPrimaryTempHum == txid)
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

									if (string.IsNullOrEmpty(rec.Value<string>("temp")) || rec.Value<int>("temp") == -99)
									{
										cumulus.LogDebugMessage($"WLL current: no valid Primary temperature value found [{rec.Value<string>("temp")}] on TxId {txid}");
									}
									else
									{
										cumulus.LogDebugMessage($"WLL current: using temp/hum data from TxId {txid}");

										if (string.IsNullOrEmpty(rec.Value<string>("hum")))
										{
											cumulus.LogDebugMessage($"WLL current: no valid Primary humidity value found [{rec.Value<string>("hum")}] on TxId {txid}");
										}
										else
										{
											DoOutdoorHumidity(rec.Value<int>("hum"), dateTime);
										}

										DoOutdoorTemp(ConvertTempFToUser(rec.Value<double>("temp")), dateTime);

										if (string.IsNullOrEmpty(rec.Value<string>("dew_point")))
										{
											cumulus.LogDebugMessage($"WLL current: no valid dewpoint value found [{rec.Value<string>("dew_point")}] on TxId {txid}");
										}
										else
										{
											DoOutdoorDewpoint(ConvertTempFToUser(rec.Value<double>("dew_point")), dateTime);
										}

										if (!cumulus.CalculatedWC)
										{
											// use wind chill from WLL
											if (string.IsNullOrEmpty(rec.Value<string>("wind_chill")))
											{
												cumulus.LogDebugMessage($"WLL current: no valid wind chill value found [{rec.Value<string>("wind_chill")}] on TxId {txid}");
											}
											else
											{
												DoWindChill(ConvertTempFToUser(rec.Value<double>("wind_chill")), dateTime);
											}
										}

										//TODO: Wet Bulb? rec["wet_bulb"] - No, we already have humidity
										//TODO: Heat Index? rec["heat_index"] - No, Cumulus always calculates HI
									}
								}
								else
								{   // Check for Extra temperature/humidity settings
									for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
									{
										if (cumulus.WllExtraTempTx[tempTxId - 1] != txid) continue;

										if (string.IsNullOrEmpty(rec.Value<string>("temp")) || rec.Value<int>("temp") == -99)
										{
											cumulus.LogDebugMessage($"WLL current: no valid Extra temperature value found [{rec.Value<string>("temp")}] on TxId {txid}");
										}
										else
										{
											cumulus.LogDebugMessage($"WLL current: using extra temp data from TxId {txid}");

											DoExtraTemp(ConvertTempFToUser(rec.Value<double>("temp")), tempTxId);

											if (!cumulus.WllExtraHumTx[tempTxId - 1]) continue;

											if (string.IsNullOrEmpty(rec.Value<string>("hum")))
											{
												cumulus.LogDebugMessage($"WLL current: no valid Extra humidity value found [{rec.Value<string>("hum")}] on TxId {txid}");
											}
											else
											{
												DoExtraHum(rec.Value<int>("hum"), tempTxId);
											}
										}
									}
								}

								// Wind
								if (cumulus.WllPrimaryWind == txid)
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

									if (string.IsNullOrEmpty(rec.Value<string>("wind_speed_last")))
									{
										cumulus.LogDebugMessage($"WLL current: no wind_speed_last value found [{rec.Value<string>("wind_speed_last")}] on TxId {txid}");
									}
									else
									{
										cumulus.LogDebugMessage($"WLL current: using wind data from TxId {txid}");

										// pesky null values from WLL when its calm
										int wdir = string.IsNullOrEmpty(rec.Value<string>("wind_dir_last")) ? 0 : rec.Value<int>("wind_dir_last");
										double wspdAvg10min = string.IsNullOrEmpty(rec.Value<string>("wind_speed_avg_last_10_min")) ? 0 : ConvertWindMPHToUser(rec.Value<double>("wind_speed_avg_last_10_min"));

										DoWind(ConvertWindMPHToUser(rec.Value<double>("wind_speed_last")), wdir, wspdAvg10min, dateTime);

										WindAverage = wspdAvg10min * cumulus.WindSpeedMult;

										if (checkWllGustValues)
										{
											// See if the current speed is higher than the current 10-min max
											// We can then update the figure before the next LOOP2 packet is read
											if (string.IsNullOrEmpty(rec.Value<string>("wind_speed_hi_last_10_min")) ||
												string.IsNullOrEmpty(rec.Value<string>("wind_dir_at_hi_speed_last_10_min")))
											{
												cumulus.LogDebugMessage("WLL current: no wind speed 10 min high values found [speed=" +
													rec.Value<string>("wind_speed_hi_last_10_min") +
													", dir=" + rec.Value<string>("wind_dir_at_hi_speed_last_10_min") +
													"] on TxId " + txid);
											}
											else
											{
												var gust = ConvertWindMPHToUser(rec.Value<double>("wind_speed_hi_last_10_min"));
												var gustCal = gust * cumulus.WindGustMult;

												if (gustCal > RecentMaxGust)
												{
													if (CheckHighGust(gustCal, rec.Value<int>("wind_dir_at_hi_speed_last_10_min"), dateTime))
													{
														cumulus.LogDebugMessage("Setting max gust from current 10 min value: " + gustCal.ToString(cumulus.WindFormat) + " was: " + RecentMaxGust.ToString(cumulus.WindFormat));

														// add to recent values so normal calculation includes this value
														WindRecent[nextwind].Gust = gust; // use uncalibrated value
														WindRecent[nextwind].Speed = WindAverage / cumulus.WindSpeedMult;
														WindRecent[nextwind].Timestamp = dateTime;
														nextwind = (nextwind + 1) % cumulus.MaxWindRecent;

														RecentMaxGust = gustCal;
													}
												}
											}
										}
										else if (!CalcRecentMaxGust)
										{
											var gust = ConvertWindMPHToUser(rec.Value<int>("wind_speed_hi_last_10_min") * cumulus.WindGustMult);
											if (CheckHighGust(gust, rec.Value<int>("wind_dir_at_hi_speed_last_10_min"), dateTime))
											{
												RecentMaxGust = gust;
											}
										}
									}
								}

								// Rainfall
								if (cumulus.WllPrimaryRain == txid)
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

									cumulus.LogDebugMessage($"WLL current: using rain data from TxId {txid}");

									// All rainfall values supplied as *tip counts*
									//double rain = ConvertRainINToUser((double)rec["rainfall_year"]);
									//double rainrate = ConvertRainINToUser((double)rec["rain_rate_last"]);

									//if (rainrate < 0)
									//{
									//	rainrate = 0;
									//}

									//DoRain(rain, rainrate, dateTime);

									if (string.IsNullOrEmpty(rec.Value<string>("rain_storm")) ||
										string.IsNullOrEmpty(rec.Value<string>("rain_storm_start_at")) ||
										string.IsNullOrEmpty(rec.Value<string>("rain_size")))
									{
										cumulus.LogDebugMessage("WLL current: no rain storm values found [speed=" +
											rec.Value<string>("rain_storm") +
											", start=" + rec.Value<string>("rain_storm_start_at") +
											", size=" + rec.Value<string>("rain_size") +
											"] on TxId " + txid);
									}
									else
									{
										StormRain = ConvertRainClicksToUser(rec.Value<double>("rain_storm"), rec.Value<int>("rain_size")) * cumulus.RainMult;
										StartOfStorm = FromUnixTime(rec.Value<int>("rain_storm_start_at"));
									}

								}

								if (cumulus.WllPrimaryUV == txid)
								{
									if (string.IsNullOrEmpty(rec.Value<string>("uv_index")))
									{
										cumulus.LogDebugMessage($"WLL current: no valid UV value found [{rec.Value<string>("uv_index")}] on TxId {txid}");
									}
									else
									{
										cumulus.LogDebugMessage($"WLL current: using UV data from TxId {txid}");
										DoUV(rec.Value<double>("uv_index"), dateTime);
									}
								}

								if (cumulus.WllPrimarySolar == txid)
								{
									if (string.IsNullOrEmpty(rec.Value<string>("solar_rad")))
									{
										cumulus.LogDebugMessage($"WLL current: no valid Solar value found [{rec.Value<string>("solar_rad")}] on TxId {txid}");
									}
									else
									{
										cumulus.LogDebugMessage($"WLL current: using solar data from TxId {txid}");
										DoSolarRad(rec.Value<int>("solar_rad"), dateTime);
									}
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

							txid = rec.Value<int>("txid");

							cumulus.LogDebugMessage($"WLL current: found Leaf/Soil data on TxId {txid}");

							// Battery
							batt = rec.Value<uint>("trans_battery_flag");
							SetTxBatteryStatus(txid, batt);

							if (rec.Value<int>("rx_state") == 2)
							{
								localSensorContactLost = true;
								cumulus.LogMessage($"Warning: Sensor contact lost TxId {txid}; ignoring data from this Leaf/Soil transmitter");
							}
							else
							{

								// For leaf wetness, soil temp/moisture we rely on user configuration, trap any errors

								// Leaf wetness
								try
								{
									if (cumulus.WllExtraLeafTx1 == txid)
									{
										idx = "wet_leaf_" + cumulus.WllExtraLeafIdx1;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid leaf wetness #{cumulus.WllExtraLeafIdx1} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoLeafWetness(rec.Value<double>(idx), 1);
										}
									}
									if (cumulus.WllExtraLeafTx2 == txid)
									{
										idx = "wet_leaf_" + cumulus.WllExtraLeafIdx2;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid leaf wetness #{cumulus.WllExtraLeafIdx2} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoLeafWetness(rec.Value<double>(idx), 2);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeCurrent LeafWetness txid={txid}, idx={idx}: {e.Message}");
								}

								// Soil moisture
								try
								{
									if (cumulus.WllExtraSoilMoistureTx1 == txid)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx1;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(rec.Value<double>(idx), 1);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx2 == txid)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx2;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(rec.Value<double>(idx), 2);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx3 == txid)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx3;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(rec.Value<double>(idx), 3);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx4 == txid)
									{
										idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx4;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(rec.Value<double>(idx), 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric SoilMoisture txid={txid}, idx={idx}: {e.Message}");
								}

								// SoilTemperature
								try
								{
									if (cumulus.WllExtraSoilTempTx1 == txid)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx1;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(rec.Value<double>(idx)), 1);
										}
									}
									if (cumulus.WllExtraSoilTempTx2 == txid)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx2;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(rec.Value<double>(idx)), 2);
										}
									}
									if (cumulus.WllExtraSoilTempTx3 == txid)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx3;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(rec.Value<double>(idx)), 3);
										}
									}
									if (cumulus.WllExtraSoilTempTx4 == txid)
									{
										idx = "temp_" + cumulus.WllExtraSoilTempIdx4;
										if (string.IsNullOrEmpty(rec.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} found [{rec.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(rec.Value<double>(idx)), 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric SoilTemp txid={txid}, idx={idx}: {e.Message}");
								}

								// TODO: Extra Humidity? No type for this on WLL
							}
							break;

						case 3: // Barometer
							/*
							 * Available fields:
							 * rec["bar_sea_level"]
							 * rec["bar_absolute"]
							 * rec["bar_trend"]
							 */

							cumulus.LogDebugMessage("WLL current: found Baro data");

							if (string.IsNullOrEmpty(rec.Value<string>("bar_sea_level")))
							{
								cumulus.LogDebugMessage($"WLL current: no valid baro reading found [{rec.Value<string>("bar_sea_level")}]");
							}
							else
							{
								DoPressure(ConvertPressINHGToUser(rec.Value<double>("bar_sea_level")), dateTime);
								DoPressTrend("Pressure trend");
								// Altimeter from absolute
								StationPressure = ConvertPressINHGToUser(rec.Value<double>("bar_absolute"));
								// Or do we use calibration? The VP2 code doesn't?
								//StationPressure = ConvertPressINHGToUser(rec.Value<double>("bar_absolute")) * cumulus.PressMult + cumulus.PressOffset;
								AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(PressureHPa(StationPressure), AltitudeM(cumulus.Altitude)));
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

							if (string.IsNullOrEmpty(rec.Value<string>("temp_in")))
							{
								cumulus.LogDebugMessage($"WLL current: no valid temp-in reading found [{rec.Value<string>("temp_in")}]");
							}
							else
							{
								DoIndoorTemp(ConvertTempFToUser(rec.Value<double>("temp_in")));
							}

							if (string.IsNullOrEmpty(rec.Value<string>("hum_in")))
							{
								cumulus.LogDebugMessage($"WLL current: no valid humidity-in reading found [{rec.Value<string>("hum_in")}]");
							}
							else
							{
								DoIndoorHumidity(rec.Value<int>("hum_in"));
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
					cumulus.BatteryLowAlarmState = TxBatText.Contains("LOW");
				}

			}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeCurrent(): Exception Caught!");
				cumulus.LogDebugMessage($"Message :" + exp.Message);
			}
		}

		private static DateTime FromUnixTime(long unixTime)
		{
			// WWL uses UTC ticks, convert to local time
			var utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
			return utcTime.ToLocalTime();
		}

		private static Int32 ToUnixTime(DateTime dateTime)
		{
			return (Int32)dateTime.ToUniversalTime().ToUnixEpochDate();
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

		internal double ConvertRainClicksToUser(double clicks, int size)
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
					// Hmm, no valid tip size from WLL...
					// One click is normally either 0.01 inches or 0.2 mm
					// Try the setting in Cumulus.ini
					if (cumulus.VPrainGaugeType == -1)
					{
						// Rain gauge type not configured, assume from units
						if (cumulus.RainUnit == 0)
						{
							return clicks * 0.2;
						}
						else
						{
							return clicks * 0.01;
						}
					}
					else
					{
						if (cumulus.VPrainGaugeType == 0)
							// Rain gauge is metric, convert to user unit
							return ConvertRainMMToUser(clicks * 0.2);
						else
						{
							return ConvertRainINToUser(clicks * 0.01);
						}
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

		private string CalculateApiSignature(string apiSecret, string data)
		{
			/*
			 Calculate the HMAC SHA-256 hash that will be used as the API Signature.
			 */
			string apiSignatureString = null;
			using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
			{
				byte[] apiSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
				apiSignatureString = BitConverter.ToString(apiSignatureBytes).Replace("-", "").ToLower();
			}
			return apiSignatureString;
		}

		public override void startReadingHistoryData()
		{
			cumulus.CurrentActivity = "Reading archive data";
			cumulus.LogMessage("Reading history data from log files");
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			cumulus.LogMessage("Reading archive data from WeatherLink API");
			bw = new BackgroundWorker();
			//histprog = new historyProgressWindow();
			//histprog.Owner = mainWindow;
			//histprog.Show();
			bw.DoWork += bw_ReadHistory;
			//bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
			bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_ReadHistoryCompleted);
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();

		}

		private void bw_ReadHistoryCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			cumulus.LogMessage("WeatherLink API archive reading thread completed");
			if (e.Error != null)
			{
				cumulus.LogMessage("Archive reading thread apparently terminated with an error: " + e.Error.Message);
			}
			cumulus.LogMessage("Updating highs and lows");
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
			cumulus.StartTimers();
		}

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
				} while (archiveRun < MaxArchiveRuns);
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

			Newtonsoft.Json.Linq.JObject jObject;

			cumulus.LogMessage("Get WL.com Historic Data");

			if (cumulus.WllApiKey == String.Empty || cumulus.WllApiSecret == String.Empty)
			{
				cumulus.LogMessage("Missing WeatherLink API data in the cumulus.ini file, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (cumulus.WllStationId == String.Empty || int.Parse(cumulus.WllStationId) < 10)
			{
				var msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage(msg);

				if (!GetAvailableStationIds())
				{
					cumulus.LastUpdateTime = DateTime.Now;
					return;
				}
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
				MaxArchiveRuns++;
			}

			cumulus.LogConsoleMessage($"Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime.ToString("s")} to: {FromUnixTime(endTime).ToString("s")}");
			cumulus.LogMessage($"Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime.ToString("s")} to: {FromUnixTime(endTime).ToString("s")}");

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

			var apiSignature = CalculateApiSignature(cumulus.WllApiSecret, data);

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

			JToken sensorData = new JObject();
			int noOfRecs = 0;

			try
			{
				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = WlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					var responseBody = response.Content.ReadAsStringAsync().Result;
					cumulus.LogDebugMessage($"WeatherLink API Historic Response: {response.StatusCode}: {responseBody}");

					jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

					if ((int)response.StatusCode != 200)
					{
						cumulus.LogMessage($"WeatherLink API Historic Error: {jObject.Value<string>("code")}, {jObject.Value<string>("message")}");
						cumulus.LogConsoleMessage($" - Error {jObject.Value<string>("code")}: {jObject.Value<string>("message")}");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}

					if (responseBody == "{}")
					{
						cumulus.LogMessage("WeatherLink API Historic: No data was returned. Check your Device Id.");
						cumulus.LogConsoleMessage(" - No historic data available");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}
					else if (responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
					{
						// get the sensor data
						sensorData = jObject.GetValue("sensors");

						foreach (Newtonsoft.Json.Linq.JToken sensor in sensorData)
						{
							if (sensor.Value<int>("sensor_type") != 504)
							{
								var recs = sensor["data"].Count();
								if (recs > noOfRecs)
									noOfRecs = recs;
							}
						}

						if (noOfRecs == 0)
						{
							cumulus.LogMessage("No historic data available");
							cumulus.LogConsoleMessage(" - No historic data available");
							cumulus.LastUpdateTime = FromUnixTime(endTime);
							return;
						}
						else
						{
							cumulus.LogMessage($"Found {noOfRecs} historic records to process");
						}
					}
					else // No idea what we got, dump it to the log
					{
						cumulus.LogMessage("Invalid historic message received");
						cumulus.LogDataMessage("Received: " + responseBody);
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetWlHistoricData exception: " + ex.Message);
				cumulus.LastUpdateTime = FromUnixTime(endTime);
				return;
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				try
				{
					DateTime timestamp = new DateTime();
					foreach (var sensor in sensorData)
					{
						if (sensor.Value<int>("sensor_type") != 504)
						{
							DecodeHistoric(sensor.Value<int>("data_structure_type"), sensor.Value<int>("sensor_type"), sensor["data"][dataIndex]);
							// sensor 504 (WLL info) does not always contain a full set of records, so grab the timestamp from a 'real' sensor
							timestamp = FromUnixTime(sensor["data"][dataIndex].Value<long>("ts"));
						}
					}

					var h = timestamp.Hour;

					if (cumulus.LogExtraSensors)
					{
						cumulus.DoExtraLogFile(timestamp);
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
					cumulus.LogMessage("Log file entry written");

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
						cumulus.LogMessage("Day rollover " + timestamp.ToShortTimeString());
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
						Console.Write("\r - processed " + (((double)dataIndex + 1) / (double)noOfRecs).ToString("P0"));
					cumulus.LogMessage($"{(dataIndex + 1)} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("GetWlHistoricData exception: " + ex.Message);
				}
			}

			if (!Program.service)
				Console.WriteLine(""); // flush the progress line
			return;
		}

		private void DecodeHistoric(int dataType, int sensorType, JToken data)
		{
			// The WLL sends the timestamp in Unix ticks, and in UTC
			int txid;
			DateTime recordTs;

			try
			{
				switch (dataType)
				{
					case 11: // ISS data
						txid = data.Value<int>("tx_id");
						recordTs = FromUnixTime(data.Value<long>("ts"));
						//weatherLinkArchiveInterval = data.Value<int>("arch_int");

						// Temperature & Humidity
						if (cumulus.WllPrimaryTempHum == txid)
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

							if (string.IsNullOrEmpty(data.Value<string>("temp_last")) || data.Value<int>("temp_last") == -99)
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid Primary temperature value found [{data.Value<string>("temp_last")}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using temp/hum data from TxId {txid}");
								DateTime ts;

								if (string.IsNullOrEmpty(data.Value<string>("hum_last")))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid Primary humidity value found [{data.Value<string>("hum_last")}] on TxId {txid}");
								}
								else
								{
									// do high humidty
									ts = FromUnixTime(data.Value<long>("hum_hi_at"));
									DoOutdoorHumidity(data.Value<int>("hum_hi"), ts);
									// do low humidity
									ts = FromUnixTime(data.Value<long>("hum_lo_at"));
									DoOutdoorHumidity(data.Value<int>("hum_lo"), ts);
									// do current humidity
									DoOutdoorHumidity(data.Value<int>("hum_last"), recordTs);
								}

								// do high temp
								ts = FromUnixTime(data.Value<long>("temp_hi_at"));
								DoOutdoorTemp(ConvertTempFToUser(data.Value<double>("temp_hi")), ts);
								// do low temp
								ts = FromUnixTime(data.Value<long>("temp_lo_at"));
								DoOutdoorTemp(ConvertTempFToUser(data.Value<double>("temp_lo")), ts);
								// do last temp
								DoOutdoorTemp(ConvertTempFToUser(data.Value<double>("temp_last")), recordTs);

								if (string.IsNullOrEmpty(data.Value<string>("dew_point_last")))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid dewpoint value found [{data.Value<string>("dew_point_last")}] on TxId {txid}");
								}
								else
								{
									// do high DP
									ts = FromUnixTime(data.Value<long>("dew_point_hi_at"));
									DoOutdoorDewpoint(ConvertTempFToUser(data.Value<double>("dew_point_hi")), ts);
									// do low DP
									ts = FromUnixTime(data.Value<long>("dew_point_lo_at"));
									DoOutdoorDewpoint(ConvertTempFToUser(data.Value<double>("dew_point_lo")), ts);
									// do last DP
									DoOutdoorDewpoint(ConvertTempFToUser(data.Value<double>("dew_point_last")), recordTs);
								}

								if (!cumulus.CalculatedWC)
								{
									// use wind chill from WLL - otherwise we calculate it at the end of processing the historic record when we have all the data
									if (string.IsNullOrEmpty(data.Value<string>("wind_chill_last")))
									{
										cumulus.LogDebugMessage($"WL.com historic: no valid wind chill value found [{data.Value<string>("wind_chill_last")}] on TxId {txid}");
									}
									else
									{
										// do low WC
										ts = FromUnixTime(data.Value<long>("wind_chill_lo_at"));
										DoWindChill(ConvertTempFToUser(data.Value<double>("wind_chill_lo")), ts);
										// do last WC
										DoWindChill(ConvertTempFToUser(data.Value<double>("wind_chill_last")), recordTs);
									}
								}
							}
						}
						else
						{   // Check for Extra temperature/humidity settings
							for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
							{
								if (cumulus.WllExtraTempTx[tempTxId - 1] != txid) continue;

								if (string.IsNullOrEmpty(data.Value<string>("temp_last")) || data.Value<int>("temp_last") == -99)
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid Extra temperature value found [{data.Value<string>("temp_last")}] on TxId {txid}");
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: using extra temp data from TxId {txid}");

									DoExtraTemp(ConvertTempFToUser(data.Value<double>("temp_last")), tempTxId);

									if (!cumulus.WllExtraHumTx[tempTxId - 1]) continue;

									if (string.IsNullOrEmpty(data.Value<string>("hum_last")))
									{
										cumulus.LogDebugMessage($"WL.com historic: no valid Extra humidity value found [{data.Value<string>("hum_last")}] on TxId {txid}");
									}
									else
									{
										DoExtraHum(data.Value<double>("hum_last"), tempTxId);
									}
								}
							}
						}

						// Wind
						if (cumulus.WllPrimaryWind == txid)
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

							if (string.IsNullOrEmpty(data.Value<string>("wind_speed_avg")))
							{
								cumulus.LogDebugMessage($"WL.com historic: no wind_speed_avg value found [{data.Value<string>("wind_speed_avg")}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using wind data from TxId {txid}");
								DoWind(ConvertWindMPHToUser(data.Value<double>("wind_speed_hi")), data.Value<int>("wind_speed_hi_dir"), ConvertWindMPHToUser(data.Value<double>("wind_speed_avg")), recordTs);

								if (string.IsNullOrEmpty(data.Value<string>("wind_speed_avg")))
								{
									cumulus.LogDebugMessage($"WL.com historic: no wind speed 10 min average value found [avg={data.Value<string>("wind_speed_avg")}] on TxId {txid}");
								}
								else
								{
									WindAverage = ConvertWindMPHToUser(data.Value<double>("wind_speed_avg")) * cumulus.WindSpeedMult;
								}

								// add in 'archivePeriod' minutes worth of wind speed to windrun
								int interval = data.Value<int>("arch_int") / 60;
								WindRunToday += ((WindAverage * WindRunHourMult[cumulus.WindUnit] * interval) / 60.0);
							}
						}

						// Rainfall
						if (cumulus.WllPrimaryRain == txid)
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

							cumulus.LogDebugMessage($"WL.com historic: using rain data from TxId {txid}");

							// The WL API v2 does not provide any running totals for rainfall, only  :(
							// So we will have to add the interval data to the running total and hope it all works out!

							if (string.IsNullOrEmpty(data.Value<string>("rainfall_clicks")) || string.IsNullOrEmpty(data.Value<string>("rainfall_clicks")))
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid rain data found [{data.Value<string>("rainfall_clicks")}] on TxId {txid}");
							}
							else
							{
								var rain = ConvertRainClicksToUser(data.Value<double>("rainfall_clicks"), data.Value<int>("rain_size"));
								var rainrate = ConvertRainClicksToUser(data.Value<double>("rain_rate_hi_clicks"), data.Value<int>("rain_size"));
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

						}

						// UV
						if (cumulus.WllPrimaryUV == txid)
						{
							/*
							 * Available fields
							 * "uv_dose"
							 * "uv_index_avg"
							 * "uv_index_hi_at"
							 * "uv_index_hi"
							 * "uv_volt_last"
							 */
							if (string.IsNullOrEmpty(data.Value<string>("uv_index_avg")))
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid UV value found [{data.Value<string>("uv_index_avg")}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using UV data from TxId {txid}");

								DoUV(data.Value<double>("uv_index_avg"), recordTs);
							}
						}

						// Solar
						if (cumulus.WllPrimarySolar == txid)
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
							if (string.IsNullOrEmpty(data.Value<string>("solar_rad_avg")))
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid Solar value found [{data.Value<string>("solar_rad_avg")}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using solar data from TxId {txid}");
								DoSolarRad(data.Value<int>("solar_rad_avg"), recordTs);
							}
						}
						break;

					case 13: // Non-ISS data
						switch (sensorType)
						{
							case 56: // Soil + Leaf
								txid = data.Value<int>("tx_id");
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

								cumulus.LogDebugMessage($"WL.com historic: found Leaf/Soil data on TxId {txid}");

								// We are relying on user configuration here, trap any errors
								try
								{
									if (cumulus.WllExtraLeafTx1 == txid)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx1;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid leaf wetness #{cumulus.WllExtraLeafIdx1} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoLeafWetness(data.Value<double>(idx), 1);
										}
									}
									if (cumulus.WllExtraLeafTx2 == txid)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx2;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid leaf wetness #{cumulus.WllExtraLeafIdx2} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoLeafWetness(data.Value<double>(idx), 2);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, LeafWetness txid={txid}, idx={idx}: {e.Message}");
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
									if (cumulus.WllExtraSoilMoistureTx1 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx1;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(data.Value<double>(idx), 1);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx2 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx2;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(data.Value<double>(idx), 2);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx3 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx3;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(data.Value<double>(idx), 3);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx4 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx4;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture(data.Value<double>(idx), 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, SoilMoisture txid={txid}, idx={idx}: {e.Message}");
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
									if (cumulus.WllExtraSoilTempTx1 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx1;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(data.Value<double>(idx)), 1);
										}
									}
									if (cumulus.WllExtraSoilTempTx2 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx2;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(data.Value<double>(idx)), 2);
										}
									}
									if (cumulus.WllExtraSoilTempTx3 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx3;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(data.Value<double>(idx)), 3);
										}
									}
									if (cumulus.WllExtraSoilTempTx4 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx4;
										if (string.IsNullOrEmpty(data.Value<string>(idx)))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} found [{data.Value<string>(idx)}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser(data.Value<double>(idx)), 4);
										}
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"Error, DecodeHistoric, SoilTemp txid={txid}, idx={idx}: {e.Message}");
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
								if (string.IsNullOrEmpty(data.Value<string>("bar_sea_level")))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid baro reading found [{data.Value<string>("bar_sea_level")}]");
								}
								else
								{
									// check the high
									var ts = FromUnixTime(data.Value<long>("bar_hi_at"));
									DoPressure(ConvertPressINHGToUser(data.Value<double>("bar_hi")), ts);
									// check the low
									ts = FromUnixTime(data.Value<long>("bar_lo_at"));
									DoPressure(ConvertPressINHGToUser(data.Value<double>("bar_lo")), ts);
									// leave it at current value
									ts = FromUnixTime(data.Value<long>("ts"));
									DoPressure(ConvertPressINHGToUser(data.Value<double>("bar_sea_level")), ts);
									DoPressTrend("Pressure trend");
									// Altimeter from absolute
									StationPressure = ConvertPressINHGToUser(data.Value<double>("bar_absolute"));
									// Or do we use calibration? The VP2 code doesn't?
									//StationPressure = ConvertPressINHGToUser(data.Value<double>("bar_absolute")) * cumulus.PressMult + cumulus.PressOffset;
									AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(PressureHPa(StationPressure), AltitudeM(cumulus.Altitude)));
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
								if (string.IsNullOrEmpty(data.Value<string>("temp_in_last")))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid temp-in reading found [{data.Value<string>("temp_in_last")}]");
								}
								else
								{
									DoIndoorTemp(ConvertTempFToUser(data.Value<double>("temp_in_last")));
								}

								if (string.IsNullOrEmpty(data.Value<string>("hum_in_last")))
								{
									cumulus.LogDebugMessage($"WLL current: no valid humidity-in reading found [{data.Value<string>("hum_in_last")}]");
								}
								else
								{
									DoIndoorHumidity(data.Value<int>("hum_in_last"));
								}
								break;

							default:
								cumulus.LogDebugMessage($"WL.com historic: found an unknown sensor type [{sensorType}]!");
								break;

						}
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

		private void DecodeWlApiHealth(JToken sensor, bool startingup)
		{
			JToken data;
			if (sensor["data"].Count() > 0)
			{
				data = sensor["data"].Last;
			}
			else
			{
				if (sensor.Value<int>("data_structure_type") == 15)
				{
					cumulus.LogDebugMessage("WLL Health - did not find any health data for WLL device");
				}
				else if (sensor.Value<int>("data_structure_type") == 11)
				{
					cumulus.LogDebugMessage("WLL Health - did not find health data for ISS device");
				}
				return;
			}

			if (sensor.Value<int>("data_structure_type") == 15)
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

				if (string.IsNullOrEmpty(data.Value<string>("firmware_version")))
				{
					cumulus.LogDebugMessage($"WL.com historic: no valid firmware version [{data.Value<string>("firmware_version")}]");
					DavisFirmwareVersion = "???";
				}
				else
				{
					var dat = FromUnixTime(data.Value<long>("firmware_version"));
					DavisFirmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");
					var battV = data.Value<double>("battery_voltage") / 1000.0;
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
					var inpV = data.Value<double>("input_voltage") / 1000.0;
					ConSupplyVoltageText = inpV.ToString("F2");
					if (inpV < 4.0)
					{
						cumulus.LogMessage($"WLL WARNING: Input voltage is low = {inpV:0.##}V");
					}
					else
					{
						cumulus.LogDebugMessage($"WLL Input Voltage = {inpV:0.##}V");
					}
					var upt = TimeSpan.FromSeconds(data.Value<double>("uptime"));
					var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage("WLL Uptime = " + uptStr);

					// Only present if WiFi attached
					if (data.SelectToken("wifi_rssi") != null)
					{
						DavisTxRssi[0] = data.Value<int>("wifi_rssi");
						cumulus.LogDebugMessage("WLL WiFi RSSI = " + data.Value<string>("wifi_rssi") + "dB");
					}

					upt = TimeSpan.FromSeconds(data.Value<double>("link_uptime"));
					uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage("WLL Link Uptime = " + uptStr);
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
			else if (sensor.Value<int>("data_structure_type") == 11)
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

				var txid = data.Value<int>("tx_id");

				cumulus.LogDebugMessage("WLL Health - found health data for ISS device TxId = " + txid);

				// Save the archive interval
				//weatherLinkArchiveInterval = data.Value<int>("arch_int");

				// Check battery state 0=Good, 1=Low
				var battState = data.Value<uint>("trans_battery_flag");
				SetTxBatteryStatus(txid, battState);
				if (battState == 1)
				{
					cumulus.LogMessage($"WLL WARNING: Battery voltage is low in TxId {txid}");
				}
				else
				{
					cumulus.LogDebugMessage($"WLL Health: ISS {txid}: Battery state is OK");
				}

				//DavisTotalPacketsReceived[txid] = ;  // Do not have a value for this
				DavisTotalPacketsMissed[txid] = data.Value<int>("error_packets");
				DavisNumCRCerrors[txid] = data.Value<int>("error_packets");
				DavisNumberOfResynchs[txid] = data.Value<int>("resynchs");
				DavisMaxInARow[txid] = data.Value<int>("good_packets_streak");
				DavisReceptionPct[txid] = data.Value<int>("reception");
				DavisTxRssi[txid] = data.Value<int>("rssi");

				cumulus.LogDebugMessage($"WLL Health: IIS {txid}: Errors={DavisTotalPacketsMissed[txid]}, CRCs={DavisNumCRCerrors[txid]}, Resyncs={DavisNumberOfResynchs[txid]}, Streak={DavisMaxInARow[txid]}, %={DavisReceptionPct[txid]}, RSSI={DavisTxRssi[txid]}");
			}
		}
		/*
				private async Task GetWlCurrentData(bool firstTime = false)
				{
					Newtonsoft.Json.Linq.JObject jObject;

					cumulus.LogMessage("Get WL.com Current Data");

					if (cumulus.WllApiKey == String.Empty || cumulus.WllApiSecret == String.Empty)
					{
						cumulus.LogMessage("Missing WeatherLink API data in the cumulus.ini file, aborting!");
						return;
					}

					if (cumulus.WllStationId == String.Empty || int.Parse(cumulus.WllStationId) < 10)
					{
						cumulus.LogMessage("No WeatherLink API station ID in the cumulus.ini file");
						if (!GetAvailableStationIds())
						{
							return;
						}
					}

					var unixDateTime = ToUnixTime(DateTime.Now);

					SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
					{
						{ "api-key", cumulus.WllApiKey },
						{ "station-id", cumulus.WllStationId.ToString() },
						{ "t", unixDateTime.ToString() }
					};

					StringBuilder dataStringBuilder = new StringBuilder();
					foreach (KeyValuePair<string, string> entry in parameters)
					{
						dataStringBuilder.Append(entry.Key);
						dataStringBuilder.Append(entry.Value);
					}

					string data = dataStringBuilder.ToString();

					var apiSignature = CalculateApiSignature(cumulus.WllApiSecret, data);

					parameters.Remove("station-id");
					parameters.Add("api-signature", apiSignature);

					StringBuilder currentUrl = new StringBuilder();
					currentUrl.Append("https://api.weatherlink.com/v2/current/" + cumulus.WllStationId + "?");
					foreach (KeyValuePair<string, string> entry in parameters)
					{
						currentUrl.Append(entry.Key);
						currentUrl.Append("=");
						currentUrl.Append(entry.Value);
						currentUrl.Append("&");
					}
					// remove the trailing "&"
					currentUrl.Remove(currentUrl.Length - 1, 1);

					var logUrl = currentUrl.ToString().Replace(cumulus.WllApiKey, "<<API_KEY>>");
					cumulus.LogDebugMessage($"WeatherLink URL = {logUrl}");

					try
					{
						var response = await WlHttpClient.GetAsync(currentUrl.ToString());
						var responseBody = await response.Content.ReadAsStringAsync();
						cumulus.LogDataMessage($"WeatherLink API Current Response: {response.StatusCode}: {responseBody}");

						jObject = JObject.Parse(responseBody);

						if ((int)response.StatusCode != 200)
						{
							cumulus.LogMessage($"WeatherLink API Current Error: {jObject.Value<string>("code")}, {jObject.Value<string>("message")}");
							return;
						}

						if (responseBody == "{}")
						{
							cumulus.LogMessage("WeatherLink API Current: No data was returned. Check your Device Id.");
							return;
						}

						// get the sensor data
						JToken sensorData = jObject["sensors"];

						foreach (JToken sensor in sensorData)
						{
							// The only thing we are doing at the moment is extracting "health" data
							if (sensor.Value<int>("sensor_type") == 504)
							{
								DecodeWlApiHealth(sensor, firstTime);
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogDebugMessage("WeatherLink API Current exception: " + ex.Message);
					}
				}
		*/

		/*
				private async void GetWlHealth(object source, ElapsedEventArgs e)
				{
					await GetWlCurrentData();

				}
		*/
		private void HealthTimerTick(object source, ElapsedEventArgs e)
		{
			// Only run every 15 minutes
			// The WLL only reports its health every 15 mins, on the hour, :15, :30 and :45
			// We run at :01, :16, :31, :46 to allow time for wl.com to generate the stats
			if (DateTime.Now.Minute % 15 == 1)
			{
				GetWlHistoricHealth();
				var msg = string.Format("WLL: Percentage good packets received from WLL {0}% - ({1},{2})", (multicastsGood / (float)(multicastsBad + multicastsGood) * 100).ToString("F2"), multicastsBad, multicastsGood);
				cumulus.LogMessage(msg);
			}
		}

		// Extracts health infomation from the last archive record
		private void GetWlHistoricHealth()
		{
			JObject jObject;

			cumulus.LogMessage("WLL Health: Get WL.com Historic Data");

			if (cumulus.WllApiKey == String.Empty || cumulus.WllApiSecret == String.Empty)
			{
				cumulus.LogMessage("WLL Health: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (cumulus.WllStationId == String.Empty || int.Parse(cumulus.WllStationId) < 10)
			{
				var msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage(msg);

				if (!GetAvailableStationIds())
				{
					return;
				}
			}

			var unixDateTime = ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - weatherLinkArchiveInterval;
			int endTime = unixDateTime;

			cumulus.LogDebugMessage($"WLL Health: Downloading the historic record from WL.com from: {FromUnixTime(startTime):s} to: {FromUnixTime(endTime):s}");

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

			var apiSignature = CalculateApiSignature(cumulus.WllApiSecret, data);

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

			JToken sensorData = new JObject();

			try
			{
				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = WlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					var responseBody = response.Content.ReadAsStringAsync().Result;
					cumulus.LogDataMessage($"WLL Health: WeatherLink API Response: {response.StatusCode}: {responseBody}");

					jObject = JObject.Parse(responseBody);

					if ((int)response.StatusCode != 200)
					{
						cumulus.LogMessage($"WLL Health: WeatherLink API Error: {jObject.Value<string>("code")}, {jObject.Value<string>("message")}");
						return;
					}

					if (responseBody == "{}")
					{
						cumulus.LogMessage("WLL Health: WeatherLink API: No data was returned. Check your Device Id.");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}
					else if (responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
					{
						// get the sensor data
						sensorData = jObject["sensors"];

						if (sensorData.Count() == 0)
						{
							cumulus.LogMessage("WLL Health: No historic data available");
							return;
						}
						else
						{
							cumulus.LogDebugMessage($"WLL Health: Found {sensorData.Count()} sensor records to process");
						}
					}
					else // No idea what we got, dump it to the log
					{
						cumulus.LogMessage("WLL Health: Invalid historic message received");
						cumulus.LogDataMessage("WLL Health: Received: " + responseBody);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("WLL Health: exception: " + ex.Message);
			}

			try
			{
				foreach (JToken sensor in sensorData)
				{
					DecodeWlApiHealth(sensor, true);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("WLL Health: exception: " + ex.Message);
			}
			cumulus.BatteryLowAlarmState = TxBatText.Contains("LOW") || wllVoltageLow;
		}


		// Finds all stations associated with this API
		// Return true if only 1 result is found, else return false
		private bool GetAvailableStationIds()
		{
			Newtonsoft.Json.Linq.JObject jObject;

			var unixDateTime = ToUnixTime(DateTime.Now);

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

			var apiSignature = CalculateApiSignature(cumulus.WllApiSecret, header);
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
			cumulus.LogDebugMessage($"WeatherLink Stations URL = {logUrl}");

			try
			{
				// We want to do this synchronously
				var response = WlHttpClient.GetAsync(stationsUrl.ToString()).Result;
				var responseBody = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDebugMessage("WeatherLink API Response: " + response.StatusCode + ": " + responseBody);

				jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

				if ((int)response.StatusCode != 200)
				{
					cumulus.LogMessage("WeatherLink API Error: " + jObject.Value<string>("code") + ", " + jObject.Value<string>("message"));
					return false;
				}

				var stations = jObject["stations"];

				foreach (var station in stations)
				{
					cumulus.LogMessage($"Found WeatherLink station id = {station.Value<string>("station_id")}, name = {station.Value<string>("station_name")}");
					if (stations.Count() > 1)
					{
						cumulus.LogConsoleMessage($" - Found WeatherLink station id = {station.Value<string>("station_id")}, name = {station.Value<string>("station_name")}, active = {station.Value<string>("active")}");
					}
					if (station.Value<int>("recording_interval") != cumulus.logints[cumulus.DataLogInterval])
					{
						cumulus.LogMessage($" - Cumulus log interval {cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station.Value<string>("recording_interval")}");
					}
				}
				if (stations.Count() > 1)
				{
					cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your WLL configuration to enable history downloads.");
				}

				if (stations.Count() == 1)
				{
					cumulus.LogMessage($"Only found 1 WeatherLink station, using id = {stations[0].Value<string>("station_id")}");
					cumulus.WllStationId = stations[0].Value<string>("station_id");
					// And save it to the config file
					cumulus.WriteIniFile();
					return true;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("WeatherLink API exception: " + ex.Message);
			}
			return false;
		}

		private void BroadcastTimeout(object source, ElapsedEventArgs e)
		{
			if (broadcastReceived)
			{
				broadcastReceived = false;
				DataStopped = false;
			}
			else
			{
				cumulus.LogMessage($"ERROR: No broadcast data received from the WLL for {tmrBroadcastWatchdog.Interval / 1000} seconds");
				DataStopped = true;
				// Try and give the broadcasts a kick in case the last command did not get through
				GetWllRealtime(null, null);
			}
		}
	}
}
