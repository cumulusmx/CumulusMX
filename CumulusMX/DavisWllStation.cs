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
using System.Globalization;

namespace CumulusMX
{
	internal class DavisWllStation : WeatherStation
	{
		private string ipaddr;
		private int port;
		private int duration;
		private readonly System.Timers.Timer tmrRealtime;
		private readonly System.Timers.Timer tmrCurrent;
		private readonly object threadSafer = new object();
		private static readonly SemaphoreSlim WebReq = new SemaphoreSlim(1);
		private bool startupDayResetIfRequired = true;
		private bool savedUseSpeedForAvgCalc;
		private int MaxArchiveRuns = 1;
		private static readonly HttpClientHandler HistoricHttpHandler = new HttpClientHandler();
		private readonly HttpClient WlHttpClient = new HttpClient(HistoricHttpHandler);


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

			// Get the firmware version from WL.com API
			GetWlCurrentData();

			// Perform zero-config
			// If it works - check IP address in config file and set/update if required
			// If it fails - just use the IP address from config file
			const string serviceType = "_weatherlinklive._tcp";
			var serviceBrowser = new ServiceBrowser();
			serviceBrowser.ServiceAdded += OnServiceAdded;
			serviceBrowser.ServiceRemoved += OnServiceRemoved;
			serviceBrowser.ServiceChanged += OnServiceChanged;
			serviceBrowser.QueryParameters.QueryInterval = cumulus.WllBroadcastDuration * 1000 / 2;

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
				tmrRealtime.Interval = cumulus.WllBroadcastDuration * 1000;
				tmrRealtime.AutoReset = true;
				tmrRealtime.Start();

				// Create a current conditions thread to poll readings once a minute
				GetWllCurrent(null, null);
				tmrCurrent.Elapsed += GetWllCurrent;
				tmrCurrent.Interval = 60 * 1000;  // Every 60 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();

				// short wait for realtime response
				Thread.Sleep(1200);

				if (port == 0)
				{
					cumulus.LogMessage("WLL failed to get broadcast port via realtime request, defaulting to 22222");
					port = 22222;
				}
				// Create a broadcast listener
				var udpClient = new UdpClient();
				udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));

				var from = new IPEndPoint(0, 0);
				Task.Run(() =>
				{
					while (!Program.exitSystem)
					{
						var recvBuffer = udpClient.Receive(ref @from);
						DecodeBroadcast(Encoding.UTF8.GetString(recvBuffer));
					}
				});

				cumulus.LogMessage($"WLL Now listening on broadcast port {port}");
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
				tmrRealtime.Stop();
				tmrCurrent.Stop();
			}
			catch
			{
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

			using (HttpClient client = new HttpClient())
			{
				// The WLL will error if already responding to a request from another device, so add a retry
				do
				{
					// Call asynchronous network methods in a try/catch block to handle exceptions
					try
					{
						string ip;

						lock (threadSafer)
						{
							ip = ipaddr;
						}

						if (CheckIpValid(ip))
						{
							var urlRealtime = "http://" + ip + "/v1/real_time?duration=" + cumulus.WllBroadcastDuration;

							cumulus.LogDebugMessage($"Sending GET realtime request to WLL: {urlRealtime} ...");

							client.DefaultRequestHeaders.Add("Connection", "close");

							var response = await client.GetAsync(urlRealtime);
							//response.EnsureSuccessStatusCode();
							var responseBody = await response.Content.ReadAsStringAsync();

							responseBody = responseBody.TrimEnd('\r', '\n');

							cumulus.LogDataMessage("WLL Real_time response: " + responseBody);

							//Console.WriteLine($" - Realtime response: {responseBody}");
							var jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
							var err = (string)jObject["error"] ?? "OK";
							port = (int)jObject["data"]["broadcast_port"];
							duration = (int)jObject["data"]["duration"];
							cumulus.LogDebugMessage("GET realtime request WLL response Code: " + err + ", Port: " + (string)jObject["data"]["broadcast_port"]);
							if (cumulus.WllBroadcastDuration != duration)
							{
								cumulus.LogMessage("WLL broadcast duration " + duration + " does not match requested duration " + cumulus.WllBroadcastDuration + ", continuing to use " + cumulus.WllBroadcastDuration);
							}
							if (cumulus.WllBroadcastPort != port)
							{
								cumulus.LogMessage("WLL broadcast port " + port + " does not match default " + cumulus.WllBroadcastPort + ", resetting to " + port);
								cumulus.WllBroadcastPort = port;
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
						cumulus.LogDebugMessage($"Message :" + exp.Message);
						//Console.ForegroundColor = ConsoleColor.Red;
						//Console.WriteLine("GetRealtime():Exception Caught!");
						//Console.ForegroundColor = ConsoleColor.White;
						Thread.Sleep(2000);
					}
				} while (retry > 0);

				cumulus.LogDebugMessage("Lock: GetWllRealtime releasing lock");
				WebReq.Release();
			}
		}

		private async void GetWllCurrent(object source, ElapsedEventArgs e)
		{
			string ip;
			var retry = 2;

			lock (threadSafer)
			{
				ip = ipaddr;
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
					using (HttpClient client = new HttpClient())
					{
						// Call asynchronous network methods in a try/catch block to handle exceptions
						try
						{
							client.DefaultRequestHeaders.Add("Connection", "close");

							var response = await client.GetAsync(urlCurrent);
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

				var data = Newtonsoft.Json.Linq.JObject.Parse(broadcastJson);

				// The WLL sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the WLL clock being correct, we will use our local time
				//var dateTime = FromUnixTime((int)data["ts"]);
				var dateTime = DateTime.Now;
				foreach (var rec in data["conditions"])
				{
					var txid = (int)rec["txid"];

					// Wind
					/* Available fields:
					 * rec["wind_speed_last"]
					 * rec["wind_dir_last"]
					 * rec["wind_speed_hi_last_10_min"]
					 * rec["wind_dir_at_hi_speed_last_10_min"]
					 */
					if (cumulus.WllPrimaryWind == txid)
					{
						if (string.IsNullOrEmpty((string)rec["wind_speed_last"]) || string.IsNullOrEmpty((string)rec["wind_dir_last"]))
						{
							cumulus.LogDebugMessage($"WLL broadcast: no valid wind speed found [speed={(string)rec["wind_speed_last"]}, dir= {(string)rec["wind_dir_last"]}] on TxId {txid}");
						}
						else
						{
							// No average in the broadcast data, so use last value from current - allow for calibration
							DoWind((double)rec["wind_speed_last"], (int)rec["wind_dir_last"], WindAverage / cumulus.WindSpeedMult, dateTime);

							if (!CalcRecentMaxGust)
							{
								// See if the station 10 min high speed is higher than our current 10-min max
								// ie we missed the high gust
								var gust = ConvertWindMPHToUser((double)rec["wind_speed_hi_last_10_min"]) * cumulus.WindGustMult;
								CheckHighGust(gust, (int)rec["wind_dir_at_hi_speed_last_10_min"], dateTime);

								RecentMaxGust = gust;
								cumulus.LogDebugMessage("Setting max gust from broadcast value: " + RecentMaxGust.ToString(cumulus.WindFormat));
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

					if (string.IsNullOrEmpty((string)rec["rainfall_year"]) || string.IsNullOrEmpty((string)rec["rain_rate_last"]) || string.IsNullOrEmpty((string)rec["rain_size"]))
					{
						cumulus.LogDebugMessage($"WLL broadcast: no valid rainfall found [total={(string)rec["rainfall_year"]}, rate= {(string)rec["rain_rate_last"]}] on TxId {txid}");
					}
					else
					{
						var rain = ConvertRainClicksToUser((double)rec["rainfall_year"], (int)rec["rain_size"]);
						var rainrate = ConvertRainClicksToUser((double)rec["rain_rate_last"], (int)rec["rain_size"]);

						if (rainrate < 0)
						{
							rainrate = 0;
						}

						DoRain(rain, rainrate, dateTime);
					}
				}
				UpdateStatusPanel(DateTime.Now);
			}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeBroadcast(): Exception Caught!");
				cumulus.LogDebugMessage($"Message :" + exp.Message);
			}
		}

		private void DecodeCurrent(string currentJson)
		{
			try
			{
				cumulus.LogDataMessage("WLL Current conditions: " + currentJson);

				// Convert JSON string to an object
				var jObject = Newtonsoft.Json.Linq.JObject.Parse(currentJson);

				var data = jObject["data"];

				// The WLL sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the WLL clock being correct, we will use our local time
				//var dateTime = FromUnixTime((int)data["ts"]);
				var dateTime = DateTime.Now;

				foreach (var rec in data["conditions"])
				{
					var type = (int)rec["data_structure_type"];
					int txid;
					string idx = "";
					uint batt;

					switch (type)
					{
						case 1: // ISS
							txid = (int)rec["txid"];

							cumulus.LogDebugMessage($"WLL current: found ISS data on TxId {txid}");

							// Battery
							batt = (uint)rec["trans_battery_flag"];
							SetTxBatteryStatus(txid, batt);

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

								if (string.IsNullOrEmpty((string)rec["temp"]) || (int)rec["temp"] == -99)
								{
									cumulus.LogDebugMessage($"WLL current: no valid Primary temperature value found [{(string)rec["temp"]}] on TxId {txid}");
								}
								else
								{
									cumulus.LogDebugMessage($"WLL current: using temp/hum data from TxId {txid}");

									if (string.IsNullOrEmpty((string)rec["hum"]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid Primary humidity value found [{(string)rec["hum"]}] on TxId {txid}");
									}
									else
									{
										DoOutdoorHumidity((int)rec["hum"], dateTime);
									}

									DoOutdoorTemp(ConvertTempFToUser((double)rec["temp"]), dateTime);

									if (string.IsNullOrEmpty((string)rec["dew_point"]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid dewpoint value found [{(string)rec["dew_point"]}] on TxId {txid}");
									}
									else
									{
										DoOutdoorDewpoint(ConvertTempFToUser((double)rec["dew_point"]), dateTime);
									}

									if (!cumulus.CalculatedWC)
									{
										// use wind chill from WLL
										if (string.IsNullOrEmpty((string)rec["wind_chill"]))
										{
											cumulus.LogDebugMessage($"WLL current: no valid wind chill value found [{(string)rec["wind_chill"]}] on TxId {txid}");
										}
										else
										{
											DoWindChill(ConvertTempFToUser((double)rec["wind_chill"]), dateTime);
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

									if (string.IsNullOrEmpty((string)rec["temp"]) || (int)rec["temp"] == -99)
									{
										cumulus.LogDebugMessage($"WLL current: no valid Extra temperature value found [{(string)rec["temp"]}] on TxId {txid}");
									}
									else
									{
										cumulus.LogDebugMessage($"WLL current: using extra temp data from TxId {txid}");

										DoExtraTemp(ConvertTempFToUser((double)rec["temp"]), tempTxId);

										if (!cumulus.WllExtraHumTx[tempTxId - 1]) continue;

										if (string.IsNullOrEmpty((string)rec["hum"]))
										{
											cumulus.LogDebugMessage($"WLL current: no valid Extra humidity value found [{(string)rec["hum"]}] on TxId {txid}");
										}
										else
										{
											DoExtraHum((int)rec["hum"], tempTxId);
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

								if (string.IsNullOrEmpty((string)rec["wind_speed_last"]))
								{
									cumulus.LogDebugMessage($"WLL current: no wind_speed_last value found [{(string)rec["wind_speed_last"]}] on TxId {txid}");
								}
								else
								{
									cumulus.LogDebugMessage($"WLL current: using wind data from TxId {txid}");

									DoWind((double)rec["wind_speed_last"], (int)rec["wind_dir_last"], (double)rec["wind_speed_avg_last_10_min"], dateTime);

									if (string.IsNullOrEmpty((string)rec["wind_speed_avg_last_10_min"]))
									{
										cumulus.LogDebugMessage("WLL current: no wind speed 10 min average value found [avg=" +
													(string)rec["wind_speed_avg_last_10_min"] + "] on TxId " + txid);
									}
									else
									{
										WindAverage = ConvertWindMPHToUser((double)rec["wind_speed_avg_last_10_min"]) * cumulus.WindSpeedMult;
									}

									if (!CalcRecentMaxGust)
									{
										// See if the current speed is higher than the current 10-min max
										// We can then update the figure before the next LOOP2 packet is read
										if (string.IsNullOrEmpty((string)rec["wind_speed_hi_last_10_min"]) ||
											string.IsNullOrEmpty((string)rec["wind_dir_at_hi_speed_last_10_min"]))
										{
											cumulus.LogDebugMessage("WLL current: no wind speed 10 min high values found [speed=" +
												(string)rec["wind_speed_hi_last_10_min"] +
												", dir=" + (string)rec["wind_dir_at_hi_speed_last_10_min"] +
												"] on TxId " + txid);
										}
										else
										{
											var gust = ConvertWindMPHToUser((double)rec["wind_speed_hi_last_10_min"]) * cumulus.WindGustMult;

											if (gust > RecentMaxGust)
											{
												RecentMaxGust = gust;
												cumulus.LogDebugMessage("Setting max gust from current value: " + RecentMaxGust.ToString(cumulus.WindFormat));
												CheckHighGust(gust, (int)rec["wind_dir_at_hi_speed_last_10_min"], dateTime);

												// add to recent values so normal calculation includes this value
												WindRecent[nextwind].Gust = gust;
												WindRecent[nextwind].Speed = WindAverage / cumulus.WindSpeedMult;
												WindRecent[nextwind].Timestamp = dateTime;
												nextwind = (nextwind + 1) % cumulus.MaxWindRecent;

											}
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

								if (string.IsNullOrEmpty((string)rec["rain_storm"]) ||
									string.IsNullOrEmpty((string)rec["rain_storm_start_at"]) ||
									string.IsNullOrEmpty((string)rec["rain_size"]))
								{
									cumulus.LogDebugMessage("WLL current: no rain storm values found [speed=" +
										(string)rec["rain_storm"] +
										", start=" + (string)rec["rain_storm_start_at"] +
										", size=" + (string)rec["rain_size"] +
										"] on TxId " + txid);
								}
								else
								{
									StormRain = ConvertRainClicksToUser((double)rec["rain_storm"], (int)rec["rain_size"]) * cumulus.RainMult;
									StartOfStorm = FromUnixTime((int)rec["rain_storm_start_at"]);
								}

							}

							if (cumulus.WllPrimaryUV == txid)
							{
								if (string.IsNullOrEmpty((string)rec["uv_index"]))
								{
									cumulus.LogDebugMessage($"WLL current: no valid UV value found [{(string)rec["uv_index"]}] on TxId {txid}");
								}
								else
								{
									cumulus.LogDebugMessage($"WLL current: using UV data from TxId {txid}");
									DoUV((double)rec["uv_index"], dateTime);
								}
							}

							if (cumulus.WllPrimarySolar == txid)
							{
								if (string.IsNullOrEmpty((string)rec["solar_rad"]))
								{
									cumulus.LogDebugMessage($"WLL current: no valid Solar value found [{(string)rec["solar_rad"]}] on TxId {txid}");
								}
								else
								{
									cumulus.LogDebugMessage($"WLL current: using solar data from TxId {txid}");
									DoSolarRad((int)rec["solar_rad"], dateTime);
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

							txid = (int)rec["txid"];

							cumulus.LogDebugMessage($"WLL current: found Leaf/Soil data on TxId {txid}");

							// Battery
							batt = (uint)rec["trans_battery_flag"];
							SetTxBatteryStatus(txid, batt);

							// For leaf wetness, soil temp/moisture we rely on user configuration, trap any errors

							// Leaf wetness
							try
							{
								if (cumulus.WllExtraLeafTx1 == txid)
								{
									idx = "wet_leaf_" + cumulus.WllExtraLeafIdx1;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid leaf wetness #{cumulus.WllExtraLeafIdx1} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoLeafWetness((double)rec[idx], 1);
									}
								}
								if (cumulus.WllExtraLeafTx2 == txid)
								{
									idx = "wet_leaf_" + cumulus.WllExtraLeafIdx2;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid leaf wetness #{cumulus.WllExtraLeafIdx2} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoLeafWetness((double)rec[idx], 2);
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
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilMoisture((double)rec[idx], 1);
									}
								}
								if (cumulus.WllExtraSoilMoistureTx2 == txid)
								{
									idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx2;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilMoisture((double)rec[idx], 2);
									}
								}
								if (cumulus.WllExtraSoilMoistureTx3 == txid)
								{
									idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx3;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilMoisture((double)rec[idx], 3);
									}
								}
								if (cumulus.WllExtraSoilMoistureTx4 == txid)
								{
									idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx4;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilMoisture((double)rec[idx], 4);
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
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilTemp(ConvertTempFToUser((double)rec[idx]), 1);
									}
								}
								if (cumulus.WllExtraSoilTempTx2 == txid)
								{
									idx = "temp_" + cumulus.WllExtraSoilTempIdx2;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilTemp(ConvertTempFToUser((double)rec[idx]), 2);
									}
								}
								if (cumulus.WllExtraSoilTempTx3 == txid)
								{
									idx = "temp_" + cumulus.WllExtraSoilTempIdx3;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilTemp(ConvertTempFToUser((double)rec[idx]), 3);
									}
								}
								if (cumulus.WllExtraSoilTempTx4 == txid)
								{
									idx = "temp_" + cumulus.WllExtraSoilTempIdx4;
									if (string.IsNullOrEmpty((string)rec[idx]))
									{
										cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} found [{(string)rec[idx]}] on TxId {txid}");
									}
									else
									{
										DoSoilTemp(ConvertTempFToUser((double)rec[idx]), 4);
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogMessage($"Error, DecodeHistoric SoilTemp txid={txid}, idx={idx}: {e.Message}");
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

							if (string.IsNullOrEmpty((string)rec["bar_sea_level"]))
							{
								cumulus.LogDebugMessage($"WLL current: no valid baro reading found [{(string)rec["bar_sea_level"]}]");
							}
							else
							{
								DoPressure(ConvertPressINHGToUser((double)rec["bar_sea_level"]), dateTime);
								DoPressTrend("Pressure trend");
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

							if (string.IsNullOrEmpty((string)rec["temp_in"]))
							{
								cumulus.LogDebugMessage($"WLL current: no valid temp-in reading found [{(string)rec["temp_in"]}]");
							}
							else
							{
								DoIndoorTemp(ConvertTempFToUser((double)rec["temp_in"]));
							}

							if (string.IsNullOrEmpty((string)rec["hum_in"]))
							{
								cumulus.LogDebugMessage($"WLL current: no valid humidity-in reading found [{(string)rec["hum_in"]}]");
							}
							else
							{
								DoIndoorHumidity((int)rec["hum_in"]);
							}

							break;

						default:
							cumulus.LogDebugMessage($"WLL current: found an unknown tramsmitter type [{type}]!");
							break;
					}

					DoForecast("", false);

					UpdateStatusPanel(DateTime.Now);
				}

				// Now we have the primary data, calculate the derived data
				if (ConvertUserWindToMS(WindAverage) < 1.5)
				{
					// wind speed too low, use the temperature
					DoWindChill(OutdoorTemperature, dateTime);
				}
				else
				{
					if (cumulus.CalculatedWC)
					{
						// calculate wind chill from calibrated C temp and calibrated wind in KPH
						DoWindChill(ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage))), dateTime);
					}
				}

				DoApparentTemp(dateTime);

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
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return (Int32)(dateTime.ToUniversalTime() - epoch).TotalSeconds;

		}

		private void CheckHighGust(double gust, int gustdir, DateTime timestamp)
		{
			if (gust > RecentMaxGust)
			{
				if (gust > highgusttoday)
				{
					highgusttoday = gust;
					highgusttodaytime = timestamp;
					highgustbearing = gustdir;
					WriteTodayFile(timestamp, false);
				}
				if (gust > HighGustThisMonth)
				{
					HighGustThisMonth = gust;
					HighGustThisMonthTS = timestamp;
					WriteMonthIniFile();
				}
				if (gust > HighGustThisYear)
				{
					HighGustThisYear = gust;
					HighGustThisYearTS = timestamp;
					WriteYearIniFile();
				}
				// All time high gust?
				if (gust > alltimerecarray[AT_highgust].value)
				{
					SetAlltime(AT_highgust, gust, timestamp);
				}

				// check for monthly all time records (and set)
				CheckMonthlyAlltime(AT_highgust, gust, true, timestamp);
			}
		}

		private void OnServiceChanged(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('~', e.Announcement);
		}

		private void OnServiceRemoved(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('-', e.Announcement);
		}

		private void OnServiceAdded(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('+', e.Announcement);
		}

		private void PrintService(char startChar, ServiceAnnouncement service)
		{
			cumulus.LogDebugMessage("ZeroConf Service: " + startChar + " '" + service.Instance + "' on " + service.NetworkInterface.Name);
			cumulus.LogDebugMessage("\tHost: " + service.Hostname + " (" + string.Join(", ", service.Addresses) + ")");

			lock (threadSafer)
			{
				ipaddr = service.Addresses[0].ToString();
				if (cumulus.VP2IPAddr != ipaddr)
				{
					cumulus.LogMessage("WLL IP address changed from " + cumulus.VP2IPAddr + " to " + ipaddr);
					cumulus.VP2IPAddr = ipaddr;
				}
				else
				{
					cumulus.LogMessage("WLL found at IP address " + ipaddr);
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
			bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();

		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			//histprog.histprogTB.Text = "Processed 100%";
			//histprog.histprogPB.Value = 100;
			//histprog.Close();
			//mainWindow.FillLastHourGraphData();
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
			if (cumulus.PeakGustMinutes >= 10)
			{
				CalcRecentMaxGust = false;
			}
			// restore this setting
			cumulus.UseSpeedForAvgCalc = savedUseSpeedForAvgCalc;
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
			// Cleanup
			WlHttpClient.Dispose();
		}

		private void GetWlHistoricData()
		{

			Newtonsoft.Json.Linq.JObject jObject;

			cumulus.LogMessage("Get WL.com Historic Data");

			if (cumulus.WllApiKey == String.Empty || cumulus.WllApiSecret == String.Empty)
			{
				cumulus.LogMessage("Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (cumulus.WllStationId == String.Empty)
			{
				var msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogMessage(msg);
				Console.WriteLine(msg);
				if (!GetAvailableStationIds())
				{
					return;
				}
			}

			Console.WriteLine("Downloading Historic Data from WL.com");

			//int passCount;
			//const int maxPasses = 4;

			var unixDateTime = ToUnixTime(DateTime.Now);
			var startTime = ToUnixTime(cumulus.LastUpdateTime);
			int endTime = unixDateTime;
			int unix24hrs = 24 * 60 * 60;

			// The API call is limited to fetching 24 hours of data
			if (unixDateTime - startTime > unix24hrs)
			{
				// only fecth 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime + unix24hrs;
				MaxArchiveRuns++;
			}

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>();
			parameters.Add("api-key", cumulus.WllApiKey);
			parameters.Add("station-id", cumulus.WllStationId.ToString());
			parameters.Add("t", unixDateTime.ToString());
			parameters.Add("start-timestamp", startTime.ToString());
			parameters.Add("end-timestamp", endTime.ToString());

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

			cumulus.LogDebugMessage($"WeatherLink URL = {historicUrl.ToString()}");

			lastDataReadTime = cumulus.LastUpdateTime;
			int luhour = lastDataReadTime.Hour;

			int rollHour = Math.Abs(cumulus.GetHourInc());

			cumulus.LogMessage("Rollover hour = " + rollHour);

			bool rolloverdone = luhour == rollHour;

			bool midnightraindone = luhour == 0;

			JToken sensorData = new JObject();
			int noOfRecs = 0;

			try
			{
				// we want to do this synchronously, so .Result
				var response = WlHttpClient.GetAsync(historicUrl.ToString()).Result;
				var responseBody = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDebugMessage("WeatherLink API Historic Response: " + response.StatusCode + ": " + responseBody);

				jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

				if ((int)response.StatusCode != 200)
				{
					cumulus.LogMessage("WeatherLink API Historic Error: " + (string)jObject["code"] + ", " + (string)jObject["message"]);
					return;
				}
				// get the sensor data
				sensorData = jObject["sensors"];

				foreach (Newtonsoft.Json.Linq.JToken sensor in sensorData)
				{
					if ((int)sensor["sensor_type"] != 504)
					{
						var recs = sensor["data"].Count();
						if (recs > noOfRecs)
							noOfRecs = recs;
					}
				}

				if (noOfRecs == 0)
				{
					cumulus.LogMessage("No historic data available");
					Console.WriteLine(" - No historic data available");
					return;
				}
				else
				{
					cumulus.LogMessage($"Found {noOfRecs} historic records to process");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("GetWlHistoricData exception: " + ex.Message);
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				try
				{
					foreach (Newtonsoft.Json.Linq.JToken sensor in sensorData)
					{
						if ((int)sensor["sensor_type"] != 504)
						{
							DecodeHistoric((int)sensor["data_structure_type"], (int)sensor["sensor_type"], sensor["data"][dataIndex]);
						}
					}

					var timestamp = FromUnixTime((long)sensorData[0]["data"][dataIndex]["ts"]);
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

					// Log all the data
					cumulus.DoLogFile(timestamp, false);
					cumulus.LogMessage("Log file entry written");

					AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
					AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
					AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
						IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV);
					AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
						OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter);
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

					Console.Write("\r - processed " + (((double)dataIndex + 1) / (double)noOfRecs).ToString("P0"));
					cumulus.LogMessage($"{(dataIndex + 1)} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage("GetWlHistoricData exception: " + ex.Message);
				}
			}

			Console.WriteLine(""); // flush the progress line
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
						txid = (int)data["tx_id"];
						recordTs = FromUnixTime((long)data["ts"]);

						// Temperature & Humidity
						if (cumulus.WllPrimaryTempHum == txid)
						{
							/*
							 * Avaialble fields
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

							if (string.IsNullOrEmpty((string)data["temp_last"]) || (int)data["temp_last"] == -99)
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid Primary temperature value found [{(string)data["temp_last"]}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using temp/hum data from TxId {txid}");
								DateTime ts;

								if (string.IsNullOrEmpty((string)data["hum_last"]))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid Primary humidity value found [{(string)data["hum_last"]}] on TxId {txid}");
								}
								else
								{
									// do high humidty
									ts = FromUnixTime((long)data["hum_hi_at"]);
									DoOutdoorHumidity((int)data["hum_hi"], ts);
									// do low humidity
									ts = FromUnixTime((long)data["hum_lo_at"]);
									DoOutdoorHumidity((int)data["hum_lo"], ts);
									// do current humidity
									DoOutdoorHumidity((int)data["hum_last"], recordTs);
								}

								// do high temp
								ts = FromUnixTime((long)data["temp_hi_at"]);
								DoOutdoorTemp(ConvertTempFToUser((double)data["temp_max"]), ts);
								// do low temp
								ts = FromUnixTime((long)data["temp_lo_at"]);
								DoOutdoorTemp(ConvertTempFToUser((double)data["temp_lo"]), ts);
								// do last temp
								DoOutdoorTemp(ConvertTempFToUser((double)data["temp_last"]), recordTs);

								if (string.IsNullOrEmpty((string)data["dew_point_last"]))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid dewpoint value found [{(string)data["dew_point_last"]}] on TxId {txid}");
								}
								else
								{
									// do high DP
									ts = FromUnixTime((long)data["dew_point_hi_at"]);
									DoOutdoorDewpoint(ConvertTempFToUser((double)data["dew_point_hi"]), ts);
									// do low DP
									ts = FromUnixTime((long)data["dew_point_lo_at"]);
									DoOutdoorDewpoint(ConvertTempFToUser((double)data["dew_point_lo"]), ts);
									// do last DP
									DoOutdoorDewpoint(ConvertTempFToUser((double)data["dew_point_last"]), recordTs);
								}

								if (!cumulus.CalculatedWC)
								{
									// use wind chill from WLL - otherwise we calculate it at the end of processing the historic record when we have all the data
									if (string.IsNullOrEmpty((string)data["wind_chill_last"]))
									{
										cumulus.LogDebugMessage($"WL.com historic: no valid wind chill value found [{(string)data["wind_chill_last"]}] on TxId {txid}");
									}
									else
									{
										// do low WC
										ts = FromUnixTime((long)data["wind_chill_lo_at"]);
										DoWindChill(ConvertTempFToUser((double)data["wind_chill_lo"]), ts);
										// do last WC
										DoWindChill(ConvertTempFToUser((double)data["wind_chill_last"]), recordTs);
									}
								}
							}
						}
						else
						{   // Check for Extra temperature/humidity settings
							for (var tempTxId = 1; tempTxId <= 8; tempTxId++)
							{
								if (cumulus.WllExtraTempTx[tempTxId - 1] != txid) continue;

								if (string.IsNullOrEmpty((string)data["temp_last"]) || (int)data["temp_last"] == -99)
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid Extra temperature value found [{(string)data["temp_last"]}] on TxId {txid}");
								}
								else
								{
									cumulus.LogDebugMessage($"WL.com historic: using extra temp data from TxId {txid}");

									DoExtraTemp(ConvertTempFToUser((double)data["temp_last"]), tempTxId);

									if (!cumulus.WllExtraHumTx[tempTxId - 1]) continue;

									if (string.IsNullOrEmpty((string)data["hum_last"]))
									{
										cumulus.LogDebugMessage($"WL.com historic: no valid Extra humidity value found [{(string)data["hum_last"]}] on TxId {txid}");
									}
									else
									{
										DoExtraHum((double)data["hum_last"], tempTxId);
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

							if (string.IsNullOrEmpty((string)data["wind_speed_avg"]))
							{
								cumulus.LogDebugMessage($"WL.com historic: no wind_speed_avg value found [{(string)data["wind_speed_avg"]}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using wind data from TxId {txid}");
								DoWind((double)data["wind_speed_hi"], (int)data["wind_speed_hi_dir"], (double)data["wind_speed_avg"], recordTs);

								if (string.IsNullOrEmpty((string)data["wind_speed_avg"]))
								{
									cumulus.LogDebugMessage("WL.com historic: no wind speed 10 min average value found [avg=" +
												(string)data["wind_speed_avg"] + "] on TxId " + txid);
								}
								else
								{
									WindAverage = ConvertWindMPHToUser((double)data["wind_speed_avg"]) * cumulus.WindSpeedMult;
								}

								// add in 'archivePeriod' minutes worth of wind speed to windrun
								int interval = (int)data["arch_int"] / 60;
								WindRunToday += ((WindAverage * WindRunHourMult[cumulus.WindUnit] * interval) / 60.0);

								if (!CalcRecentMaxGust)
								{
									// See if the current speed is higher than the current 10-min max
									// We can then update the figure before the next LOOP2 packet is read
									if (string.IsNullOrEmpty((string)data["wind_speed_hi"]) ||
										string.IsNullOrEmpty((string)data["wind_speed_hi_dir"]))
									{
										cumulus.LogDebugMessage("WL.com historic: no wind speed 10 min high values found [speed=" +
											(string)data["wind_speed_hi"] +
											", dir=" + (string)data["wind_speed_hi_dir"] +
											"] on TxId " + txid);
									}
									else
									{
										var gust = (double)data["wind_speed_hi"] * cumulus.WindGustMult;

										if (gust > RecentMaxGust)
										{
											RecentMaxGust = WindLatest;
											cumulus.LogDebugMessage("Setting max gust from current value: " + RecentMaxGust.ToString(cumulus.WindFormat));
											var ts = FromUnixTime((long)data["wind_speed_hi_at"]);
											CheckHighGust(gust, (int)data["wind_speed_hi"], ts);

											// add to recent values so normal calculation includes this value
											WindRecent[nextwind].Gust = ConvertWindMPHToUser(gust);
											WindRecent[nextwind].Speed = WindAverage / cumulus.WindSpeedMult;
											WindRecent[nextwind].Timestamp = recordTs;
											nextwind = (nextwind + 1) % cumulus.MaxWindRecent;

											RecentMaxGust = gust;
										}
									}
								}
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

							if (string.IsNullOrEmpty((string)data["rainfall_clicks"]) || string.IsNullOrEmpty((string)data["rainfall_clicks"]))
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid rain data found [{(string)data["rainfall_clicks"]}] on TxId {txid}");
							}
							else
							{
								var rain = ConvertRainClicksToUser((double)data["rainfall_clicks"], (int)data["rain_size"]);
								var rainrate = ConvertRainClicksToUser((double)data["rain_rate_hi_clicks"], (int)data["rain_size"]);
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
							if (string.IsNullOrEmpty((string)data["uv_index_avg"]))
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid UV value found [{(string)data["uv_index_avg"]}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using UV data from TxId {txid}");

								DoUV((double)data["uv_index_avg"], recordTs);
							}
						}

						// Solar
						if (cumulus.WllPrimarySolar == txid)
						{
							if (string.IsNullOrEmpty((string)data["solar_rad_avg"]))
							{
								cumulus.LogDebugMessage($"WL.com historic: no valid Solar value found [{(string)data["solar_rad_avg"]}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WL.com historic: using solar data from TxId {txid}");
								DoSolarRad((int)data["solar_rad_avg"], recordTs);
							}
						}
						break;

					case 13: // Non-ISS data
						switch (sensorType)
						{
							case 56: // Soil + Leaf
								txid = (int)data["tx_id"];
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
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid leaf wetness #{cumulus.WllExtraLeafIdx1} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoLeafWetness((double)data[idx], 1);
										}
									}
									if (cumulus.WllExtraLeafTx2 == txid)
									{
										idx = "wet_leaf_last_" + cumulus.WllExtraLeafIdx2;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid leaf wetness #{cumulus.WllExtraLeafIdx2} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoLeafWetness((double)data[idx], 2);
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
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx1} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture((double)data[idx], 1);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx2 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx2;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx2} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture((double)data[idx], 2);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx3 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx3;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx3} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture((double)data[idx], 3);
										}
									}
									if (cumulus.WllExtraSoilMoistureTx4 == txid)
									{
										idx = "moist_soil_last_" + cumulus.WllExtraSoilMoistureIdx4;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilMoisture((double)data[idx], 4);
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
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx1} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data[idx]), 1);
										}
									}
									if (cumulus.WllExtraSoilTempTx2 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx2;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx2} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data[idx]), 2);
										}
									}
									if (cumulus.WllExtraSoilTempTx3 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx3;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx3} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data[idx]), 3);
										}
									}
									if (cumulus.WllExtraSoilTempTx4 == txid)
									{
										idx = "temp_last_" + cumulus.WllExtraSoilTempIdx4;
										if (string.IsNullOrEmpty((string)data[idx]))
										{
											cumulus.LogDebugMessage($"WL.com historic: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} found [{(string)data[idx]}] on TxId {txid}");
										}
										else
										{
											DoSoilTemp(ConvertTempFToUser((double)data[idx]), 4);
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
								if (string.IsNullOrEmpty((string)data["bar_sea_level"]))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid baro reading found [{(string)data["bar_sea_level"]}]");
								}
								else
								{
									// check the high
									var ts = FromUnixTime((long)data["bar_hi_at"]);
									DoPressure(ConvertPressINHGToUser((double)data["bar_hi"]), ts);
									// check the low
									ts = FromUnixTime((long)data["bar_lo_at"]);
									DoPressure(ConvertPressINHGToUser((double)data["bar_lo"]), ts);
									// leave it at current value
									ts = FromUnixTime((long)data["ts"]);
									DoPressure(ConvertPressINHGToUser((double)data["bar_sea_level"]), ts);
									DoPressTrend("Pressure trend");
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
								if (string.IsNullOrEmpty((string)data["temp_in_last"]))
								{
									cumulus.LogDebugMessage($"WL.com historic: no valid temp-in reading found [{(string)data["temp_in_last"]}]");
								}
								else
								{
									DoIndoorTemp(ConvertTempFToUser((double)data["temp_in_last"]));
								}

								if (string.IsNullOrEmpty((string)data["hum_in_last"]))
								{
									cumulus.LogDebugMessage($"WLL current: no valid humidity-in reading found [{(string)data["hum_in_last"]}]");
								}
								else
								{
									DoIndoorHumidity((int)data["hum_in_last"]);
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

		private void DecodeWlApiHealth(JToken data)
		{
			cumulus.LogDebugMessage("WL.com API: found WLL health data");

			/*
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

			if (string.IsNullOrEmpty((string)data["firmware_version"]))
			{
				cumulus.LogDebugMessage($"WL.com historic: no valid firmware version [{(string)data["firmware_version"]}]");
				DavisFirmwareVersion = "???";
			}
			else
			{
				var dat = FromUnixTime((long)data["firmware_version"]);
				DavisFirmwareVersion = dat.ToUniversalTime().ToString("s", DateTimeFormatInfo.InvariantInfo);
			}
			cumulus.LogMessage("FW version = " + DavisFirmwareVersion);
		}

		private async void GetWlCurrentData()
		{
			Newtonsoft.Json.Linq.JObject jObject;

			cumulus.LogMessage("Get WL.com Current Data");

			if (cumulus.WllApiKey == String.Empty || cumulus.WllApiSecret == String.Empty)
			{
				cumulus.LogMessage("Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (cumulus.WllStationId == String.Empty)
			{
				cumulus.LogMessage("No WeatherLink API station ID in the cumulus.ini file");
				if (!GetAvailableStationIds())
				{
					return;
				}
			}

			var unixDateTime = ToUnixTime(DateTime.Now);

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>();
			parameters.Add("api-key", cumulus.WllApiKey);
			parameters.Add("station-id", cumulus.WllStationId.ToString());
			parameters.Add("t", unixDateTime.ToString());

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

			cumulus.LogDebugMessage($"WeatherLink URL = {currentUrl.ToString()}");

			try
			{
				var response = await WlHttpClient.GetAsync(currentUrl.ToString());
				var responseBody = await response.Content.ReadAsStringAsync();
				cumulus.LogDataMessage("WeatherLink API Current Response: " + response.StatusCode + ": " + responseBody);

				jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

				if ((int)response.StatusCode != 200)
				{
					cumulus.LogMessage("WeatherLink API Current Error: " + (string)jObject["code"] + ", " + (string)jObject["message"]);
					return;
				}

				// get the sensor data
				JToken sensorData = jObject["sensors"];

				foreach (Newtonsoft.Json.Linq.JToken sensor in sensorData)
				{
					// The only thing we are doing at the moment is extracting "health" data
					if ((int)sensor["sensor_type"] == 504)
					{
						DecodeWlApiHealth(sensor["data"][0]);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("WeatherLink API Current exception: " + ex.Message);
			}
		}

		// Finds all stations associated with this API
		// Return true if only 1 result is found, else return false
		private bool GetAvailableStationIds()
		{
			Newtonsoft.Json.Linq.JObject jObject;

			var unixDateTime = ToUnixTime(DateTime.Now);

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>();
			parameters.Add("api-key", cumulus.WllApiKey);
			parameters.Add("t", unixDateTime.ToString());

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

			cumulus.LogDebugMessage($"WeatherLink Stations URL = {stationsUrl.ToString()}");

			try
			{
				// We want to do this synchronously
				var response = WlHttpClient.GetAsync(stationsUrl.ToString()).Result;
				var responseBody = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDebugMessage("WeatherLink API Response: " + response.StatusCode + ": " + responseBody);

				jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

				if ((int)response.StatusCode != 200)
				{
					cumulus.LogMessage("WeatherLink API Error: " + (string)jObject["code"] + ", " + (string)jObject["message"]);
					return false;
				}

				var stations = jObject["stations"];

				foreach (var station in stations)
				{
					cumulus.LogMessage($"Found WeatherLink station id = {station["station_id"]}, name = {station["station_name"]}");
					if (stations.Count() > 1)
					{
						Console.WriteLine($" - Found WeatherLink station id = {station["station_id"]}, name = {station["station_name"]}, active = {station["active"]}");
					}
					if ((int)station["recording_interval"] != cumulus.logints[cumulus.DataLogInterval])
					{
						cumulus.LogMessage($" - Cumulus log interval {cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station["recording_interval"]}");
					}
				}
				if (stations.Count() > 1)
				{
					Console.WriteLine(" - Enter the required station id above into your WLL configuration to enable history downloads.");
				}

				if (stations.Count() == 1)
				{
					cumulus.LogMessage($"Only found 1 WeatherLink station, using id = {stations[0]["station_id"]}");
					cumulus.WllStationId = stations[0]["station_id"].ToString();
					return true;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("WeatherLink API exception: " + ex.Message);
			}
			return false;
		}
	}
}
