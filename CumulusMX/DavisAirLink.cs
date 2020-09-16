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
	internal class DavisAirLink : WeatherStation
	{
		private string ipaddr;
		private int port;
		private int duration;
		private readonly System.Timers.Timer tmrCurrent;
		private readonly System.Timers.Timer tmrHealth;
		private readonly object threadSafer = new object();
		private static readonly SemaphoreSlim WebReq = new SemaphoreSlim(1);
		private bool startupDayResetIfRequired = true;

		private static readonly HttpClientHandler HistoricHttpHandler = new HttpClientHandler();
		private readonly HttpClient WlHttpClient = new HttpClient(HistoricHttpHandler);
		private readonly HttpClient dogsBodyClient = new HttpClient();
		private int weatherLinkArchiveInterval = 16 * 60; // Used to get historic Health, 16 minutes in seconds only for initial fetch after load
		private bool alVoltageLow = false;
		private bool stop = false;

		private bool indoor;

		public DavisAirLink(Cumulus cumulus, bool Indoor) : base(cumulus)
		{
			indoor = Indoor;

			cumulus.LogMessage($"Extra Sensor = Davis AirLink ({(indoor ? "Indoor" : "Outdoor")})");

			tmrCurrent = new System.Timers.Timer();

			// Perform zero-config
			// If it works - check IP address in config file and set/update if required
			// If it fails - just use the IP address from config file

			const string serviceType = "_airlink._tcp";
			var serviceBrowser = new ServiceBrowser();
			serviceBrowser.ServiceAdded += OnServiceAdded;
			serviceBrowser.ServiceRemoved += OnServiceRemoved;
			serviceBrowser.ServiceChanged += OnServiceChanged;
			serviceBrowser.QueryParameters.QueryInterval = cumulus.WllBroadcastDuration * 1000 * 4; // query at 4x the multicast time (default 20 mins)

			//Console.WriteLine($"Browsing for type: {serviceType}");
			serviceBrowser.StartBrowse(serviceType);

			cumulus.LogMessage("Attempting to find AirLink via zero-config...");

			// short wait for zero-config
			Thread.Sleep(1000);

			DateTime tooold = new DateTime(0);

			if ((cumulus.LastUpdateTime <= tooold) || !cumulus.UseDataLogger)
			{
				// there's nothing in the database, so we haven't got a rain counter
				// we can't load the history data, so we'll just have to go live

				//timerStartNeeded = true;
				//StartLoop();
				//DoDayResetIfNeeded();
				//DoTrendValues(DateTime.Now);

				cumulus.LogMessage($"Starting Davis AirLink ({(indoor ? "Indoor" : "Outdoor")})");
				//StartLoop();
			}
			else if (cumulus.UseDataLogger)
			{
				if ((indoor && !cumulus.AirLinkInIsNode) || (!indoor && !cumulus.AirLinkOutIsNode))

				// Read the data from the WL APIv2
				startReadingHistoryData();
			}


		}

		public override void Start()
		{
			try
			{

				// Create a current conditions thread to poll readings every 30 seconds
				GetAlCurrent(null, null);
				tmrCurrent.Elapsed += GetAlCurrent;
				tmrCurrent.Interval = 30 * 1000;  // Every 30 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();


				// Only poll health data here if the AirLink is not linked to another WLL station
				if (cumulus.UseDataLogger)
				{
					if ((indoor && !cumulus.AirLinkInIsNode) || (!indoor && !cumulus.AirLinkOutIsNode))
					{
						// get the health data every 15 minutes
						tmrHealth.Elapsed += HealthTimerTick;
						tmrHealth.Interval = 60 * 1000;  // Tick every minute
						tmrHealth.AutoReset = true;
						tmrHealth.Start();
					}
				}
			}
			catch (ThreadAbortException)
			{
			}

		}

		public override void Stop()
		{
		}

		private async void GetAlCurrent(object source, ElapsedEventArgs e)
		{
			string ip;
			var retry = 2;

			lock (threadSafer)
			{
				ip = (indoor ? cumulus.AirLinkInIPAddr : cumulus.AirLinkOutIPAddr);
			}

			if (CheckIpValid(ip))
			{

				var urlCurrent = $"http://{ip}/v1/current_conditions";

				cumulus.LogDebugMessage("GetAlCurrent: Waiting for lock");
				WebReq.Wait();
				cumulus.LogDebugMessage("GetAlCurrent: Has the lock");

				// The AL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogDebugMessage($"GetAlCurrent: Sending GET current conditions request to AL: {urlCurrent} ...");
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
								DecodeAlCurrent(responseBody);
								if (startupDayResetIfRequired)
								{
									DoDayResetIfNeeded();
									startupDayResetIfRequired = false;
								}
							}
							else
							{
								cumulus.LogMessage("GetAlCurrent: Invalid current conditions message received");
								cumulus.LogDebugMessage("GetAlCurrent: Received: " + responseBody);
							}
							retry = 0;
						}
					}
					catch (Exception exp)
					{
						retry--;
						cumulus.LogDebugMessage("GetAlCurrent: Exception Caught!");
						cumulus.LogDebugMessage($"Message :" + exp.Message);

						//Console.ForegroundColor = ConsoleColor.Red;
						//Console.WriteLine("GetWllCurrent():Exception Caught!");
						//Console.ForegroundColor = ConsoleColor.White;
						Thread.Sleep(2000);
					}
				} while (retry > 0);

				cumulus.LogDebugMessage("GetAlCurrent: Releasing lock");
				WebReq.Release();
			}
			else
			{
				cumulus.LogMessage($"GetAlCurrent: Invalid IP address: {ip}");
			}
		}

		private void DecodeAlCurrent(string currentJson)
		{
			try
			{
				cumulus.LogDataMessage("DecodeAlCurrent: " + currentJson);

				// Convert JSON string to an object
				//var data = Newtonsoft.Json.Linq.JObject.Parse(currentJson)["data"]["conditions"];
				var data = JObject.Parse(currentJson).SelectToken("data.conditions");

				// The WLL sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the WLL clock being correct, we will use our local time
				//var dateTime = FromUnixTime(data.Value<int>("ts"));
				var dateTime = DateTime.Now;

				foreach (var rec in data)
				{
					var type = rec.Value<int>("data_structure_type");

					switch (type)
					{
						case 5: // AirLink - original firmware
						case 6: // AirLink - newer firmware
							cumulus.LogDebugMessage("DecodeAlCurrent: Found AirLink data");

							// Temperature & Humidity
							/* Available fields
								* "temp": 62.7,                                  // most recent valid temperature **(°F)**
								* "hum":1.1,                                     // most recent valid humidity **(%RH)**
								* "dew_point": -0.3,                             // **(°F)**
								* "wet_bulb":null,                               // **(°F)**
								* "heat_index": 5.5,                             // **(°F)**
								*/

							if (string.IsNullOrEmpty(rec.Value<string>("temp")) || rec.Value<int>("temp") == -99)
							{
								cumulus.LogDebugMessage($"DecodeAlCurrent: No valid temperature value found [{rec.Value<string>("temp")}]");
							}
							else
							{
								cumulus.LogDebugMessage($"DecodeAlCurrent: Using temp/hum data");

								if (string.IsNullOrEmpty(rec.Value<string>("hum")))
								{
									cumulus.LogDebugMessage($"DecodeAlCurrent: No valid humidity value found [{rec.Value<string>("hum")}]");
								}
								else
								{
									if (indoor)
									{
										cumulus.airLinkDataIn.humidity = rec.Value<int>("hum");
									}
									else
									{
										cumulus.airLinkDataOut.humidity = rec.Value<int>("hum");
									}
								}

								if (indoor)
								{
									cumulus.airLinkDataIn.temperature = rec.Value<double>("temp");
								}
								else
								{
									cumulus.airLinkDataOut.temperature = rec.Value<double>("temp");
								}
							}

							// AQ fields
							/* Available fields
							 * pm_1_last					// the most recent valid PM 1.0 reading calculated using atmospheric calibration in µg/m^3
							 * pm_1							// the average of all PM 1.0 readings in the last minute calculated using atmospheric calibration in µg/m^3
							 * pm_2p5_last
							 * pm_2p5
							 * pm_2p5_last_1_hour			// the average of all PM 2.5 readings in the last hour calculated using atmospheric calibration in µg/m^3
							 * pm_2p5_last_3_hours			// the average of all PM 2.5 readings in the last 3 hours calculated using atmospheric calibration in µg/m^3
							 * pm_2p5_last_24_hours			// the weighted average of all PM 2.5 readings in the last 24 hours calculated using atmospheric calibration in µg/m^3
							 * pm_2p5_nowcast				// the weighted average of all PM 2.5 readings in the last 12 hours calculated using atmospheric calibration in µg/m^3
							 * pm_10_last					// type=5
							 * pm_10p0
							 * pm_10p0_last_1_hour
							 * pm_10p0_last_3_hours
							 * pm_10p0_last_24_hours
							 * pm_10p0_nowcast
							 * pm_10_last					// type=6
							 * pm_10
							 * pm_10_last_1_hour
							 * pm_10_last_3_hours
							 * pm_10_last_24_hours
							 * pm_10_nowcast
							 * last_report_time				// the UNIX timestamp of the last time a valid reading was received from the PM sensor (or time since boot if time has not been synced), with resolution of seconds
							 * pct_pm_data_last_1_hour		// the amount of PM data available to calculate averages in the last hour (rounded down to the nearest percent)
							 * pct_pm_data_last_3_hours
							 * pct_pm_data_last_24_hours
							 * pct_pm_data_nowcast
							 *
							 * With the exception of fields ending in _last, all pm_n_xxx fields are calculated using a rolling window with one minute granularity that is updated once per minute a few seconds after the end of each minute
							*/

							if (string.IsNullOrEmpty(rec.Value<string>("pm_1")))
							{
								cumulus.LogDebugMessage($"DecodeAlCurrent: No valid pm1 value found [{rec.Value<string>("pm_1")}]");
							}
							else
							{
								if (indoor)
								{
									cumulus.airLinkDataIn.pm1 = rec.Value<double>("pm_1");
									cumulus.airLinkDataIn.pm2p5 = rec.Value<double>("pm_2p5");
									cumulus.airLinkDataIn.pm2p5_1hr = rec.Value<double>("pm_2p5_last_1_hour");
									cumulus.airLinkDataIn.pm2p5_3hr = rec.Value<double>("pm_2p5_last_3_hours");
									cumulus.airLinkDataIn.pm2p5_24hr = rec.Value<double>("pm_2p5_last_24_hours");
									cumulus.airLinkDataIn.pm2p5_nowcast = rec.Value<double>("pm_2p5_nowcast");
									if (type == 5)
									{
										cumulus.airLinkDataIn.pm10 = rec.Value<double>("pm_10p0");
										cumulus.airLinkDataIn.pm10_1hr = rec.Value<double>("pm_10p0_last_1_hour");
										cumulus.airLinkDataIn.pm10_3hr = rec.Value<double>("pm_10p0_last_3_hours");
										cumulus.airLinkDataIn.pm10_24hr = rec.Value<double>("pm_10p0_last_24_hours");
										cumulus.airLinkDataIn.pm10_nowcast = rec.Value<double>("pm_10p0_nowcast");
									}
									else
									{
										cumulus.airLinkDataIn.pm10 = rec.Value<double>("pm_10");
										cumulus.airLinkDataIn.pm10_1hr = rec.Value<double>("pm_10_last_1_hour");
										cumulus.airLinkDataIn.pm10_3hr = rec.Value<double>("pm_10_last_3_hours");
										cumulus.airLinkDataIn.pm10_24hr = rec.Value<double>("pm_10_last_24_hours");
										cumulus.airLinkDataIn.pm10_nowcast = rec.Value<double>("pm_10_nowcast");
									}
									cumulus.airLinkDataIn.pct_1hr = rec.Value<int>("pct_pm_data_last_1_hour");
									cumulus.airLinkDataIn.pct_3hr = rec.Value<int>("pct_pm_data_last_3_hours");
									cumulus.airLinkDataIn.pct_24hr = rec.Value<int>("pct_pm_data_last_24_hours");
									cumulus.airLinkDataIn.pct_nowcast = rec.Value<int>("pct_pm_data_nowcast");
								}
								else
								{
									cumulus.airLinkDataOut.pm1 = rec.Value<double>("pm_1");
									cumulus.airLinkDataOut.pm2p5 = rec.Value<double>("pm_2p5");
									cumulus.airLinkDataOut.pm2p5_1hr = rec.Value<double>("pm_2p5_last_1_hour");
									cumulus.airLinkDataOut.pm2p5_3hr = rec.Value<double>("pm_2p5_last_3_hours");
									cumulus.airLinkDataOut.pm2p5_24hr = rec.Value<double>("pm_2p5_last_24_hours");
									cumulus.airLinkDataOut.pm2p5_nowcast = rec.Value<double>("pm_2p5_nowcast");
									if (type == 5)
									{
										cumulus.airLinkDataOut.pm10 = rec.Value<double>("pm_10p0");
										cumulus.airLinkDataOut.pm10_1hr = rec.Value<double>("pm_10p0_last_1_hour");
										cumulus.airLinkDataOut.pm10_3hr = rec.Value<double>("pm_10p0_last_3_hours");
										cumulus.airLinkDataOut.pm10_24hr = rec.Value<double>("pm_10p0_last_24_hours");
										cumulus.airLinkDataOut.pm10_nowcast = rec.Value<double>("pm_10p0_nowcast");
									}
									else
									{
										cumulus.airLinkDataOut.pm10 = rec.Value<double>("pm_10");
										cumulus.airLinkDataOut.pm10_1hr = rec.Value<double>("pm_10_last_1_hour");
										cumulus.airLinkDataOut.pm10_3hr = rec.Value<double>("pm_10_last_3_hours");
										cumulus.airLinkDataOut.pm10_24hr = rec.Value<double>("pm_10_last_24_hours");
										cumulus.airLinkDataOut.pm10_nowcast = rec.Value<double>("pm_10_nowcast");
									}
									cumulus.airLinkDataOut.pct_1hr = rec.Value<int>("pct_pm_data_last_1_hour");
									cumulus.airLinkDataOut.pct_3hr = rec.Value<int>("pct_pm_data_last_3_hours");
									cumulus.airLinkDataOut.pct_24hr = rec.Value<int>("pct_pm_data_last_24_hours");
									cumulus.airLinkDataOut.pct_nowcast = rec.Value<int>("pct_pm_data_nowcast");
								}
							}

							break;

						default:
							cumulus.LogDebugMessage($"DecodeAlCurrent: found an unknown tramsmitter type [{type}]!");
							break;
					}


					//UpdateStatusPanel(DateTime.Now);
					//UpdateMQTT();
				}


				// If the station isn't using the logger function for AirLink - i.e. no API key, then only alarm on Tx battery status
				// otherwise, trigger the alarm when we read the Health data which also contains the AirLink backup battery status???
				if (!cumulus.UseDataLogger)
				{
					cumulus.BatteryLowAlarmState = TxBatText.Contains("LOW");
				}

			}
			catch (Exception exp)
			{
				cumulus.LogDebugMessage("DecodeAlCurrent: Exception Caught!");
				cumulus.LogDebugMessage($"Message :" + exp.Message);
			}
		}

		public void DecodeHistoric(int dataType, int sensorType, JToken data)
		{

			try
			{
				switch (dataType)
				{
					case 17: // AirLink Archive record
						/* Just the fields we may be interested in - ignoring dew point, wet bulb, heat index
						 * temp_avg
						 * temp_hi
						 * temp_hi_at
						 * temp_lo
						 * temp_lo_at
						 * hum_last
						 * hum_hi
						 * hum_hi_at
						 * hum_lo
						 * hum_lo_at
						 * pm_1_avg
						 * pm_1_hi
						 * pm_1_hi_at
						 * pm_2p5_avg
						 * pm_2p5_hi
						 * pm_2p5_hi_at
						 * pm_10_avg
						 * pm_10_hi
						 * pm_10_hi_at
						 * pm_0p3_avg_num_part
						 * pm_0p3_hi_num_part
						 * pm_0p5_avg_num_part
						 * pm_0p5_hi_num_part
						 * pm_1_avg_num_part
						 * pm_1_hi_num_part
						 * pm_2p5_avg_num_part
						 * pm_2p5_hi_num_part
						 * pm_5_avg_num_part
						 * pm_5_hi_num_part
						 * pm_10_avg_num_part
						 * pm_10_hi_num_part
						 */

						break;

					default:
						// Unknown!
						break;
				}
			}
			catch (Exception ex)
			{

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
			}
		}

		private void GetWlHistoricHealth()
		{
			JObject jObject;

			var stationId = indoor ? cumulus.AirLinkInStationId : cumulus.AirLinkOutStationId;

			cumulus.LogMessage("AirLinkHealth: Get WL.com Historic Data");

			if (cumulus.AirLinkApiKey == String.Empty || cumulus.AirLinkApiSecret == String.Empty)
			{
				cumulus.LogMessage("AirLinkHealth: Missing WeatherLink API data in the cumulus.ini file, aborting!");
				return;
			}

			if (stationId == String.Empty || int.Parse(stationId) < 10)
			{
				var msg = "No WeatherLink API station ID in the cumulus.ini file";
				cumulus.LogMessage("AirLinkHealth: " + msg);
				cumulus.LogConsoleMessage(msg);

				if (!GetAvailableStationIds())
				{
					return;
				}
			}

			var unixDateTime = ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - weatherLinkArchiveInterval;
			int endTime = unixDateTime;

			cumulus.LogDebugMessage($"AirLinkHealth: Downloading the historic record from WL.com from: {FromUnixTime(startTime):s} to: {FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.AirLinkApiKey },
				{ "station-id", stationId },
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

			var apiSignature = CalculateApiSignature(cumulus.AirLinkApiSecret, data);

			parameters.Remove("station-id");
			parameters.Add("api-signature", apiSignature);

			StringBuilder historicUrl = new StringBuilder();
			historicUrl.Append("https://api.weatherlink.com/v2/historic/" + stationId + "?");
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				historicUrl.Append(entry.Key);
				historicUrl.Append("=");
				historicUrl.Append(entry.Value);
				historicUrl.Append("&");
			}
			// remove the trailing "&"
			historicUrl.Remove(historicUrl.Length - 1, 1);

			var logUrl = historicUrl.ToString().Replace(cumulus.AirLinkApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"AirLinkHealth: WeatherLink URL = {logUrl}");

			JToken sensorData = new JObject();

			try
			{
				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = WlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					var responseBody = response.Content.ReadAsStringAsync().Result;
					cumulus.LogDataMessage($"AirLinkHealth: WeatherLink API Response: {response.StatusCode}: {responseBody}");

					jObject = JObject.Parse(responseBody);

					if ((int)response.StatusCode != 200)
					{
						cumulus.LogMessage($"AirLinkHealth: WeatherLink API Error: {jObject.Value<string>("code")}, {jObject.Value<string>("message")}");
						return;
					}

					if (responseBody == "{}")
					{
						cumulus.LogMessage("AirLinkHealth: WeatherLink API: No data was returned. Check your Device Id.");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}
					else if (responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
					{
						// get the sensor data
						sensorData = jObject["sensors"];

						if (sensorData.Count() == 0)
						{
							cumulus.LogMessage("AirLinkHealth: No historic data available");
							return;
						}
						else
						{
							cumulus.LogDebugMessage($"AirLinkHealth: Found {sensorData.Count()} sensor records to process");
						}
					}
					else // No idea what we got, dump it to the log
					{
						cumulus.LogMessage("AirLinkHealth: Invalid historic message received");
						cumulus.LogDataMessage("AirLinkHealth: Received: " + responseBody);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("AirLinkHealth: exception: " + ex.Message);
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
				cumulus.LogMessage("AirLinkHealth: exception: " + ex.Message);
			}
			//cumulus.BatteryLowAlarmState = TxBatText.Contains("LOW") || wllVoltageLow;
		}

		internal void DecodeWlApiHealth(JToken sensor, bool startingup)
		{
			JToken data;
			if (sensor["data_structure"].Count() > 0)
			{
				data = sensor["data_structure"].Last;
			}
			else
			{
				if (sensor.Value<int>("data_structure_type") == 15)
				{
					cumulus.LogDebugMessage("AirLinkHealth: Did not find any health data for AirLink device");
				}
				return;
			}

			if (sensor.Value<int>("data_structure_type") == 18)
			{
				/* AirLink
				 * Available fields of interest to health
					"air_quality_firmware_version": 1598649428
					"application_version": "v1.0.0"
					"bootloader_version": null
					"dns_type_used": null
					"tx_packets": 40720
					"rx_packets": 50260
					"dropped_packets": 26635
					"packet_errors": 0
					"network_error": null
					"local_api_queries": 0
					"health_version": 2
					"internal_free_memory_chunk_size": 34812	- bytes
					"internal_free_memory_watermark": 54552		- bytes
					"internal_free_memory": 75596				- bytes
					"internal_used_memory": 139656				- bytes
					"ip_address_type": 1						- 1=Dynamic, 2=Dyn DNS Override, 3=Static
					"ip_v4_address": "192.168.68.137"
					"ip_v4_gateway": "192.168.68.1"
					"ip_v4_netmask": "255.255.255.0"
					"record_backlog_count": 0
					"record_stored_count": 2048
					"record_write_count": 5106
					"total_free_memory": 2212176
					"total_used_memory": 779380
					"uptime": 283862
					"link_uptime": 283856
					"wifi_rssi": -48
					"ts": 1598937300
				 */

				cumulus.LogDebugMessage("AirLinkHealth: Found health data for AirLink device");

				if (string.IsNullOrEmpty(data.Value<string>("air_quality_firmware_version")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: No valid firmware version [{data.Value<string>("air_quality_firmware_version")}]");
					if (indoor)
						cumulus.airLinkDataIn.firmwareVersion = "???";
					else
						cumulus.airLinkDataOut.firmwareVersion = "???";
				}
				else
				{
					var dat = FromUnixTime(data.Value<long>("air_quality_firmware_version"));
					if (indoor)
						cumulus.airLinkDataIn.firmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");
					else
						cumulus.airLinkDataOut.firmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");
				}

				if (string.IsNullOrEmpty(data.Value<string>("uptime")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: No valid uptime [{data.Value<string>("uptime")}]");
				}
				else
				{
					var upt = TimeSpan.FromSeconds(data.Value<double>("uptime"));
					var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage("AirLinkHealth: Uptime = " + uptStr);
				}

				if (string.IsNullOrEmpty(data.Value<string>("link_uptime")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: No valid link uptime [{data.Value<string>("link_uptime")}]");
				}
				else
				{
					var upt = TimeSpan.FromSeconds(data.Value<double>("link_uptime"));
					var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage("AirLinkHealth: Link Uptime = " + uptStr);
				}

				if (string.IsNullOrEmpty(data.Value<string>("wifi_rssi")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: No valid WiFi RSSI [{data.Value<string>("wifi_rssi")}]");
				}
				else
				{
					// Only present if WiFi attached
					if (data.SelectToken("wifi_rssi") != null)
					{
						if (indoor)
							cumulus.airLinkDataIn.wifiRssi = data.Value<int>("wifi_rssi");
						else
							cumulus.airLinkDataOut.wifiRssi = data.Value<int>("wifi_rssi");

						cumulus.LogDebugMessage("AirLinkHealth: WiFi RSSI = " + data.Value<string>("wifi_rssi") + "dB");
					}

				}

				if (string.IsNullOrEmpty(data.Value<string>("tx_packets")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: No valid xmt count [{data.Value<string>("tx_packets")}]");
				}
				else
				{
					var txCnt = data.Value<int>("tx_packets");
					var rxCnt = data.Value<int>("rx_packets");
					var dropped = data.Value<int>("dropped_packets");
					var bad = data.Value<int>("packet_errors");
					var error = data.Value<int>("network_error");
					cumulus.LogDebugMessage($"AirLinkHealth: Traffic info -  Tx:{txCnt}, Rx:{rxCnt}, drop:{dropped}, bad:{bad}, Net_Err:{error}");
				}

				if (startingup)
				{
					cumulus.LogMessage("AirLinkHealth: FW version = " + (indoor ? cumulus.airLinkDataIn.firmwareVersion : cumulus.airLinkDataOut.firmwareVersion));
				}
				else
				{
					cumulus.LogDebugMessage("AirLinkHealth: FW version = " + (indoor ? cumulus.airLinkDataIn.firmwareVersion : cumulus.airLinkDataOut.firmwareVersion));
				}
			}
		}

		private bool GetAvailableStationIds()
		{
			JObject jObject;

			var unixDateTime = ToUnixTime(DateTime.Now);

			// Are we using the same WL APIv2 as a WLL device?
			if (cumulus.StationType == 11 && cumulus.WllApiKey == cumulus.AirLinkApiKey)
				return true;

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.AirLinkApiKey },
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

			var logUrl = stationsUrl.ToString().Replace(cumulus.AirLinkApiKey, "<<API_KEY>>");
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
					cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your AirLink configuration to enable history downloads.");
				}

				if (stations.Count() == 1)
				{
					cumulus.LogMessage($"Only found 1 WeatherLink station, using id = {stations[0].Value<string>("station_id")}");
					if (indoor)
					{
						cumulus.AirLinkInStationId = stations[0].Value<string>("station_id");
					}
					else
					{
						cumulus.AirLinkOutStationId = stations[0].Value<string>("station_id");
					}
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

		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
		}

		private void OnServiceChanged(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('~', e.Announcement);
		}

		private void OnServiceRemoved(object sender, ServiceAnnouncementEventArgs e)
		{
			cumulus.LogMessage("ZeroConfig Service: AirLink service has been removed!");
		}

		private void OnServiceAdded(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('+', e.Announcement);
		}

		private void PrintService(char startChar, ServiceAnnouncement service)
		{
			cumulus.LogDebugMessage($"ZeroConf Service: {startChar} '{service.Instance}' on {service.NetworkInterface.Name}");
			cumulus.LogDebugMessage($"\tHost: {service.Hostname} ({string.Join(", ", service.Addresses)})");

			var currIpAddr = indoor ? cumulus.AirLinkInIPAddr : cumulus.AirLinkOutIPAddr;

			lock (threadSafer)
			{
				if (service.Addresses.Count > 1)
				{
					return;
				}
				ipaddr = service.Addresses[0].ToString();
				cumulus.LogMessage($"AirLink found, reporting its IP address as: {ipaddr}");
				if (currIpAddr != ipaddr)
				{
					cumulus.LogMessage($"AirLink IP address has changed from {currIpAddr} to {ipaddr}");
					if (cumulus.AirLinkAutoUpdateIpAddress)
					{
						cumulus.LogMessage($"AirLink changing Cumulus config to the new IP address {ipaddr}");
						if (indoor)
						{
							cumulus.AirLinkInIPAddr = ipaddr;
						}
						else
						{
							cumulus.AirLinkOutIPAddr = ipaddr;
						}
						cumulus.WriteIniFile();
					}
					else
					{
						cumulus.LogMessage($"AirLink ignoring new IP address {ipaddr} due to setting AirLinkAutoUpdateIpAddress");
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
	}
}
