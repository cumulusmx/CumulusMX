using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Timers;
using System.Net.Http;
using System.Net.Sockets;
using Tmds.MDns;
using System.Net;


namespace CumulusMX
{
	internal class DavisWllStation : WeatherStation
	{
		private string ipaddr;
		private int port;
		private int duration;
		private System.Timers.Timer tmrRealtime;
		private System.Timers.Timer tmrCurrent;
		private ServiceBrowser serviceBrowser;
		private static object threadSafer = new object();
		private static SemaphoreSlim webReq = new SemaphoreSlim(1);
		private bool startupDayResetIfRequired = true;

		public DavisWllStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = cumulus.DAVIS;
			calculaterainrate = false;
			//cumulus.UseDataLogger = false;

			cumulus.LogMessage("Station type = Davis WLL");

			tmrRealtime = new System.Timers.Timer();
			tmrCurrent = new System.Timers.Timer();


			// Perform zero-config
			// If it works - check IP address in config file and set/update if required
			// If it fails - just use the IP address from config file
			string serviceType = "_weatherlinklive._tcp";
			serviceBrowser = new ServiceBrowser();
			serviceBrowser.ServiceAdded += OnServiceAdded;
			serviceBrowser.ServiceRemoved += OnServiceRemoved;
			serviceBrowser.ServiceChanged += OnServiceChanged;
			serviceBrowser.QueryParameters.QueryInterval = cumulus.WllBroadcastDuration * 1000 / 2;

			//Console.WriteLine($"Browsing for type: {serviceType}");
			serviceBrowser.StartBrowse(serviceType);

			cumulus.LogMessage("Attempting to find WLL via zero-config...");

			// short wait for zero-config
			Thread.Sleep(1000);

			timerStartNeeded = true;
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
			DoTrendValues(DateTime.Now);

			// WLL does not provide a forecast string, so use the Cumulus forecast
			cumulus.UseCumulusForecast = true;

			cumulus.LogMessage("Starting Davis WLL");

			StartLoop();
		}

		// destructor
		~DavisWllStation()
		{
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
				tmrRealtime.Elapsed += new ElapsedEventHandler(GetWllRealtime);
				tmrRealtime.Interval = cumulus.WllBroadcastDuration * 1000;
				tmrRealtime.AutoReset = true;
				tmrRealtime.Start();

				// Create a current conditions thread to poll readings once a minute
				GetWllCurrent(null, null);
				tmrCurrent.Elapsed += new ElapsedEventHandler(GetWllCurrent);
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
				UdpClient udpClient = new UdpClient();
				udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));

				var from = new IPEndPoint(0, 0);
				Task t = Task.Run(() =>
				{
					while (true)
					{
						var recvBuffer = udpClient.Receive(ref from);
						decodeBroadcast(Encoding.UTF8.GetString(recvBuffer));
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
			string ip;
			int retry = 2;

			cumulus.LogDebugMessage("Lock: GetWllRealtime waiting for lock");
			webReq.Wait();
			cumulus.LogDebugMessage("Lock: GetWllRealtime has the lock");

			using (HttpClient client = new HttpClient())
			{
				// The WLL will error if already responding to a request from another device, so add a retry
				do
				{
					// Call asynchronous network methods in a try/catch block to handle exceptions
					try
					{
						lock (threadSafer)
						{
							ip = ipaddr;
						}

						if (CheckIPValid(ip))
						{
							var urlRealtime = "http://" + ip + "/v1/real_time?duration=" + cumulus.WllBroadcastDuration;

							cumulus.LogDebugMessage($"Sending GET realtime request to WLL: {urlRealtime} ...");

							client.DefaultRequestHeaders.Add("Connection", "close");

							HttpResponseMessage response = await client.GetAsync(urlRealtime);
							//response.EnsureSuccessStatusCode();
							string responseBody = await response.Content.ReadAsStringAsync();

							responseBody = responseBody.TrimEnd('\r', '\n');

							cumulus.LogDataMessage("WLL Real_time response: " + responseBody);

							//Console.WriteLine($" - Realtime response: {responseBody}");
							var jObject = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
							string err = (string)jObject["error"];
							if (err == null) err = "OK";
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
					catch (HttpRequestException exp)
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
				webReq.Release();
			}
		}


		private async void GetWllCurrent(object source, ElapsedEventArgs e)
		{
			string ip;
			int retry = 2;

			lock (threadSafer)
			{ 
				ip = ipaddr;
			}

			if (CheckIPValid(ip))
			{

				var urlCurrent = $"http://{ip}/v1/current_conditions";


				cumulus.LogDebugMessage("Lock: GetWllCurrent waiting for lock");
				webReq.Wait();
				cumulus.LogDebugMessage("Lock: GetWllCurrent has the lock");

				// The WLL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogMessage($"Sending GET current conditions request to WLL: {urlCurrent} ...");
					// First time run it synchronously
					using (HttpClient client = new HttpClient())
					{
						// Call asynchronous network methods in a try/catch block to handle exceptions
						try
						{
							client.DefaultRequestHeaders.Add("Connection", "close");

							HttpResponseMessage response = await client.GetAsync(urlCurrent);
							response.EnsureSuccessStatusCode();
							string responseBody = await response.Content.ReadAsStringAsync();

							//Console.WriteLine($" - Current conds response: {responseBody}");
							// sanity check
							if (responseBody.StartsWith("{\"data\":{\"did\":"))
							{
								decodeCurrent(responseBody);
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
						catch (HttpRequestException exp)
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
				webReq.Release();
			}
			else
			{
				cumulus.LogMessage($"WLL current: Invalid IP address: {ip}");
			}
		}


		private void decodeBroadcast(string broadcastJson)
		{
			int txid;
			var data = Newtonsoft.Json.Linq.JObject.Parse(broadcastJson);

			cumulus.LogDataMessage("WLL Broadcast: " + broadcastJson);

			//var dateTime = DateTimeOffset.FromUnixTimeSeconds((int)data["ts"]).DateTime;  // .Net 4.6 or later :(
			var dateTime = FromUnixTime((int)data["ts"]);
			foreach (var rec in data["conditions"])
			{
				txid = (int)rec["txid"];

				// Wind
				/* Available fields:
				 * rec["wind_speed_last"]
				 * rec["wind_dir_last"]
				 * rec["wind_speed_hi_last_10_min"]
				 * rec["wind_dir_at_hi_speed_last_10_min"]
				 */
				if (cumulus.WllPrimaryWind == txid)
				{
					if (String.IsNullOrEmpty((string)rec["wind_speed_last"]) || String.IsNullOrEmpty((string)rec["wind_dir_last"]))
					{
						cumulus.LogDebugMessage($"WLL broadcast: no valid wind speed found [speed={(string)rec["wind_speed_last"]}, dir= {(string)rec["wind_dir_last"]}] on TxId {txid}");
					}
					else
					{
						// No average in the broadcast data, so use last value from current
						DoWind((double)rec["wind_speed_last"], (int)rec["wind_dir_last"], WindAverage, dateTime);

						if (!CalcRecentMaxGust)
						{
							// See if the current speed is higher than the current 10-min max
							// We can then update the figure before the next LOOP2 packet is read

							CheckHighGust((double)rec["wind_speed_hi_last_10_min"], (int)rec["wind_dir_at_hi_speed_last_10_min"], dateTime);

							if (WindLatest > RecentMaxGust)
							{
								RecentMaxGust = WindLatest;
								cumulus.LogDebugMessage("Setting max gust from current value: " + RecentMaxGust.ToString(cumulus.WindFormat));
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
				if (cumulus.WllPrimaryRain == txid)
				{
					if (String.IsNullOrEmpty((string)rec["rainfall_year"]) || String.IsNullOrEmpty((string)rec["rain_rate_last"]) || String.IsNullOrEmpty((string)rec["rain_size"]))
					{
						cumulus.LogDebugMessage($"WLL broadcast: no valid rainfall found [total={(string)rec["rainfall_year"]}, rate= {(string)rec["rain_rate_last"]}] on TxId {txid}");
					}
					else
					{
						double rain = ConvertRainClicksToUser((double)rec["rainfall_year"], (int)rec["rain_size"]);
						double rainrate = ConvertRainClicksToUser((double)rec["rain_rate_last"], (int)rec["rain_size"]);

						if (rainrate < 0)
						{
							rainrate = 0;
						}

						DoRain(rain, rainrate, dateTime);
					}
				}
			}
			UpdateStatusPanel(DateTime.Now);
		}

		private void decodeCurrent(string currentJson)
		{
			cumulus.LogDataMessage("WLL Current conditions: " + currentJson);
			// Convert JSON string to an object
			var jObject = Newtonsoft.Json.Linq.JObject.Parse(currentJson);

			var data = jObject["data"];

			//var dateTime = DateTimeOffset.FromUnixTimeSeconds((int)data["ts"]).DateTime;  // .Net 4.6 or later :(
			var dateTime = FromUnixTime((int)data["ts"]);

			foreach (var rec in data["conditions"])
			{
				int type = (int)rec["data_structure_type"];
				int txid;
				string idx;

				switch (type)
				{
					case 1: // ISS
						txid = (int)rec["txid"];

						cumulus.LogDebugMessage($"WLL current: found ISS data on TxId {txid}");

						// Temperature & Humidity
						if (cumulus.WllPrimaryTempHum == txid)
						{
							if (String.IsNullOrEmpty((string)rec["temp"]) || (int)rec["temp"] == -99)
							{
								cumulus.LogDebugMessage($"WLL current: no valid Primary temperature value found [{(string)rec["temp"]}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WLL current: using temp/hum data from TxId {txid}");

								if (String.IsNullOrEmpty((string)rec["hum"]))
								{
									cumulus.LogDebugMessage($"WLL current: no valid Primary humidity value found [{(string)rec["hum"]}] on TxId {txid}");
								}
								{
									DoOutdoorHumidity((int)rec["hum"], dateTime);
								}

								DoOutdoorTemp(ConvertTempFToUser((double)rec["temp"]), dateTime);

								if (ConvertUserWindToMS(WindAverage) < 1.5)
								{
									DoWindChill(OutdoorTemperature, dateTime);
								}
								else
								{
									// calculate wind chill from calibrated C temp and calibrated win in KPH
									DoWindChill(ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage))), dateTime);
								}

								DoApparentTemp(dateTime);

								if (String.IsNullOrEmpty((string)rec["dew_point"]))
								{
									cumulus.LogDebugMessage($"WLL current: no valid dewpoint value found [{(string)rec["dew_point"]}] on TxId {txid}");
								}
								{
									DoOutdoorDewpoint(ConvertTempFToUser((double)rec["dew_point"]), dateTime);
								}

								//TODO: Wet Bulb? rec["wet_bulb"]
								//TODO: Heat Index? rec["heat_index"]
							}
						}
						else
						{   // Check for Extra temperature/humidity settings
							for (int tempTxId = 1; tempTxId <= 8; tempTxId++)
							{
								if (cumulus.WllExtraTempTx[tempTxId - 1] == txid)
								{
									if (String.IsNullOrEmpty((string)rec["temp"]) || (int)rec["temp"] == -99)
									{
										cumulus.LogDebugMessage($"WLL current: no valid Extra temperature value found [{(string)rec["temp"]}] on TxId {txid}");
									}
									else
									{
										cumulus.LogDebugMessage($"WLL current: using extra temp data from TxId {txid}");

										DoExtraTemp(ConvertTempFToUser((double)rec["temp"]), tempTxId);

										if (cumulus.WllExtraHumTx[tempTxId - 1])
										{
											if (String.IsNullOrEmpty((string)rec["hum"]))
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
							}
						}

						// Wind
						if (cumulus.WllPrimaryWind == txid)
						{
							if (String.IsNullOrEmpty((string)rec["wind_speed_last"]))
							{
								cumulus.LogDebugMessage($"WLL current: no wind_speed_last value found [{(string)rec["wind_speed_last"]}] on TxId {txid}");
							}
							else
							{
								cumulus.LogDebugMessage($"WLL current: using wind data from TxId {txid}");
								// Do the basic wind from the broadcast data to avoid conflicts
								//	DoWind((double)rec["wind_speed_last"], (int)rec["wind_dir_last"], (double)rec["wind_speed_avg_last_10_min"], dateTime);

								if (!CalcRecentMaxGust)
								{
									// See if the current speed is higher than the current 10-min max
									// We can then update the figure before the next LOOP2 packet is read
									if (String.IsNullOrEmpty((string)rec["wind_speed_hi_last_10_min"]) ||
										String.IsNullOrEmpty((string)rec["wind_dir_at_hi_speed_last_10_min"]))
									{
										cumulus.LogDebugMessage("WLL current: no wind speed 10 min high values found [speed=" +
											(string)rec["wind_speed_hi_last_10_min"] +
											", dir=" + (string)rec["wind_dir_at_hi_speed_last_10_min"] +
											"] on TxId " + txid);
									}
									else
									{
										CheckHighGust((double)rec["wind_speed_hi_last_10_min"], (int)rec["wind_dir_at_hi_speed_last_10_min"], dateTime);

										if (WindLatest > RecentMaxGust)
										{
											RecentMaxGust = WindLatest;
											cumulus.LogDebugMessage("Setting max gust from current value: " + RecentMaxGust.ToString(cumulus.WindFormat));
										}
									}
								}
								if (String.IsNullOrEmpty((string)rec["wind_speed_avg_last_10_min"]))
								{
									cumulus.LogDebugMessage("WLL current: no wind speed 10 min average value found [avg=" +
												(string)rec["wind_speed_avg_last_10_min"] + "] on TxId " + txid);
								}
								else
								{
									WindAverage = ConvertWindMPHToUser((double)rec["wind_speed_avg_last_10_min"]);
								}

							}
						}

						// Rainfall
						if (cumulus.WllPrimaryRain == txid)
						{
							cumulus.LogDebugMessage($"WLL current: using rain data from TxId {txid}");

							// All rainfall values supplied as *tip counts*
							//double rain = ConvertRainINToUser((double)rec["rainfall_year"]);
							//double rainrate = ConvertRainINToUser((double)rec["rain_rate_last"]);

							//if (rainrate < 0)
							//{
							//	rainrate = 0;
							//}

							//DoRain(rain, rainrate, dateTime);

							if (String.IsNullOrEmpty((string)rec["rain_storm"]) || 
								String.IsNullOrEmpty((string)rec["rain_storm_start_at"]) ||
								String.IsNullOrEmpty((string)rec["rain_size"]))
							{
								cumulus.LogDebugMessage("WLL current: no rain storm values found [speed=" +
									(string)rec["rain_storm"] +
									", start=" + (string)rec["rain_storm_start_at"] +
									", size=" + (string)rec["rain_size"] +
									"] on TxId " + txid);
							}
							else
							{
								StormRain = ConvertRainClicksToUser((double)rec["rain_storm"], (int)rec["rain_size"]);
								StartOfStorm = FromUnixTime((int)rec["rain_storm_start_at"]);
							}

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
						}

						if (cumulus.WllPrimaryUV == txid)
						{
							if (String.IsNullOrEmpty((string)rec["uv_index"]))
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
							if (String.IsNullOrEmpty((string)rec["solar_rad"]))
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

					case 2: // Leaf/Soil Mositure
						txid = (int)rec["txid"];

						cumulus.LogDebugMessage($"WLL current: found Leaf/Soil data on TxId {txid}");

						// Leaf wetness
						if (cumulus.WllExtraLeafTx1 == txid)
						{
							idx = "wet_leaf_" + cumulus.WllExtraLeafIdx1;
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
							{
								cumulus.LogDebugMessage($"WLL current: no valid leaf wetness #{cumulus.WllExtraLeafIdx2} found [{(string)rec[idx]}] on TxId {txid}");
							}
							else
							{
								DoLeafWetness((double)rec[idx], 2);
							}
						}

						// Soil moisture
						if (cumulus.WllExtraSoilMoistureTx1 == txid)
						{
							idx = "moist_soil_" + cumulus.WllExtraSoilMoistureIdx1;
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
							{
								cumulus.LogDebugMessage($"WLL current: no valid soil moisture #{cumulus.WllExtraSoilMoistureIdx4} found [{(string)rec[idx]}] on TxId {txid}");
							}
							else
							{
								DoSoilMoisture((double)rec[idx], 4);
							}
						}

						// SoilTemperature
						if (cumulus.WllExtraSoilTempTx1 == txid)
						{
							idx = "temp_" + cumulus.WllExtraSoilTempIdx1;
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
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
							if (String.IsNullOrEmpty((string)rec[idx]))
							{
								cumulus.LogDebugMessage($"WLL current: no valid extra soil temp #{cumulus.WllExtraSoilTempIdx4} found [{(string)rec[idx]}] on TxId {txid}");
							}
							else
							{
								DoSoilTemp(ConvertTempFToUser((double)rec[idx]), 4);
							}
						}

						// TODO: Extra Humidity? No type for this on WLL
						break;

					case 3: // Barometer

						cumulus.LogDebugMessage("WLL current: found Baro data");

						if (String.IsNullOrEmpty((string)rec["bar_sea_level"]))
						{
							cumulus.LogDebugMessage($"WLL current: no valid baro reading found [{(string)rec["bar_sea_level"]}]");
						}
						else
						{
							DoPressure(ConvertPressINHGToUser((double)rec["bar_sea_level"]), dateTime);
							DoPressTrend("Pressure trend");
						}

						/*
						 * Available fields:
						 * rec["bar_sea_level"]
						 * rec["bar_absolute"]
						 * rec["bar_trend"]
						 */
						break;

					case 4: // WLL Temp/Humidity

						cumulus.LogDebugMessage("WLL current: found Indoor temp/hum data");

						if (String.IsNullOrEmpty((string)rec["temp_in"]))
						{
							cumulus.LogDebugMessage($"WLL current: no valid temp-in reading found [{(string)rec["temp_in"]}]");
						}
						else
						{
							DoIndoorTemp(ConvertTempFToUser((double)rec["temp_in"]));
						}

						if (String.IsNullOrEmpty((string)rec["hum_in"]))
						{
							cumulus.LogDebugMessage($"WLL current: no valid humidity-in reading found [{(string)rec["hum_in"]}]");
						}
						else
						{
							DoIndoorHumidity((int)rec["hum_in"]);
						}

						/*
						 * Available fields:
						 * rec["temp_in"]
						 * rec["hum_in"]
						 * rec["dew_point_in"]
						 * rec["heat_index_in"]
						 */
						break;
				}

				DoForecast("", false);

				UpdateStatusPanel(DateTime.Now);
			}
		}

		private DateTime FromUnixTime(long unixTime)
		{
			return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
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
			cumulus.LogDebugMessage("ZeroConf Service: " + startChar +" '" + service.Instance + "' on " + service.NetworkInterface.Name);
			cumulus.LogDebugMessage("\tHost: " + service.Hostname + " (" + string.Join(", ", service.Addresses) + ")");

			lock (threadSafer)
			{
				ipaddr = service.Addresses[0].ToString();
			}
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

		private Boolean CheckIPValid(String strIP)
		{
			if (String.IsNullOrEmpty(strIP))
				return false;
			//  Split string by ".", check that array length is 4
			string[] arrOctets = strIP.Split('.');
			if (arrOctets.Length != 4)
				return false;

			//Check each substring checking that parses to byte
			byte obyte = 0;
			foreach (string strOctet in arrOctets)
				if (!byte.TryParse(strOctet, out obyte))
					return false;

			return true;
		}

		private class CommTimer
		{
			public System.Timers.Timer tmrComm = new System.Timers.Timer();
			public bool timedout = false;
			public CommTimer()
			{
				timedout = false;
				tmrComm.AutoReset = false;
				tmrComm.Enabled = false;
				tmrComm.Interval = 1000; //default to 1 second
				tmrComm.Elapsed += new ElapsedEventHandler(OnTimedCommEvent);
			}

			public void OnTimedCommEvent(object source, ElapsedEventArgs e)
			{
				timedout = true;
				tmrComm.Stop();
			}

			public void Start(double timeoutperiod)
			{
				tmrComm.Interval = timeoutperiod;             //time to time out in milliseconds
				tmrComm.Stop();
				timedout = false;
				tmrComm.Start();
			}

			public void Stop()
			{
				tmrComm.Stop();
				timedout = true;
			}
		}

	}
}
