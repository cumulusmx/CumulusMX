using System;
using System.Text;
using System.Threading;
using System.Linq;
using System.Timers;
using System.Net.Http;
using Tmds.MDns;
using System.Net;
using System.ComponentModel;
using System.Collections.Generic;
using ServiceStack;
using Unosquare.Swan;

namespace CumulusMX
{
	internal class DavisAirLink
	{
		private Cumulus cumulus;
		private WeatherStation station;

		private string ipaddr;
		private readonly System.Timers.Timer tmrCurrent;
		private System.Timers.Timer tmrHealth;
		private readonly object threadSafer = new object();
		private static readonly SemaphoreSlim WebReq = new SemaphoreSlim(1);
		private bool startupDayResetIfRequired = true;
		private int maxArchiveRuns = 1;

		private static readonly HttpClientHandler HistoricHttpHandler = new HttpClientHandler();
		private readonly HttpClient wlHttpClient = new HttpClient(HistoricHttpHandler);
		private readonly HttpClient dogsBodyClient = new HttpClient();
		private const int WeatherLinkArchiveInterval = 16 * 60; // Used to get historic Health, 16 minutes in seconds only for initial fetch after load

		//private bool alVoltageLow = false;
		private readonly List<WlSensor> sensorList = new List<WlSensor>();
		private readonly int healthLsid;
		private bool updateInProgress;
		private readonly bool indoor;
		private readonly string locationStr;
		private readonly bool standalone;
		private readonly bool standaloneHistory; // Used to flag if we need to get history data on catch-up

		private DateTime airLinkLastUpdateTime;

		private DiscoveredDevices discovered = new DiscoveredDevices();

		public DavisAirLink(Cumulus cumulus, bool indoor, WeatherStation station)
		{
			this.indoor = indoor;
			this.cumulus = cumulus;
			this.station = station;


			locationStr = this.indoor ? "Indoor" : "Outdoor";

			airLinkLastUpdateTime = cumulus.LastUpdateTime;

			// Working out if we are stand-alone or integrated with WLL is a bit tricky.
			// Easiest to see if we are a node of a WLL station
			standalone = !(
				cumulus.StationType == StationTypes.WLL &&
				cumulus.AirLinkIsNode &&
				!string.IsNullOrEmpty(cumulus.WllApiKey) &&
				!string.IsNullOrEmpty(cumulus.WllApiSecret) &&
				!(cumulus.WllStationId < 10)
			);

			// If we are stand-alone, are we configured to read history data?
			standaloneHistory = standalone &&
								!string.IsNullOrEmpty(cumulus.AirLinkApiKey) &&
								!string.IsNullOrEmpty(cumulus.AirLinkApiSecret) &&
								!(this.indoor ? cumulus.AirLinkInStationId < 10 : cumulus.AirLinkOutStationId < 10);

			cumulus.LogMessage($"Extra Sensor = Davis AirLink ({locationStr}) - stand-alone={standalone}");

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

			cumulus.LogMessage("ZeroConf Service: Attempting to find AirLink via zero-config...");

			// short wait for zero-config
			Thread.Sleep(1000);

			// Process the discovered device list
			ipaddr = indoor ? cumulus.AirLinkInIPAddr : cumulus.AirLinkOutIPAddr;
			var hostname = indoor ? cumulus.AirLinkInHostName : cumulus.AirLinkOutHostName;
			string msg;

			if (discovered.IP.Count == 0)
			{
				// We didn't find anything on the network
				msg = "Failed to discover any AirLink devices";
				cumulus.LogMessage("ZeroConf Service: " + msg);
				cumulus.LogConsoleMessage(msg);
			}
			else if (discovered.IP.Count == 1 && (string.IsNullOrEmpty(hostname) || discovered.Hostname[0] == hostname))
			{
				var writeConfig = false;

				// If only one device is discovered, and its Host-name address matches (or our Host-name is blank), then just use it
				if (string.IsNullOrEmpty(hostname))
				{
					writeConfig = true;

					if (indoor)
						cumulus.AirLinkInHostName = discovered.Hostname[0];
					else
						cumulus.AirLinkOutHostName = discovered.Hostname[0];
				}

				if (discovered.IP[0] != ipaddr)
				{
					writeConfig = true;

					cumulus.LogMessage($"ZeroConf Service: Discovered a new IP address for the {locationStr} AirLink that does not match our current one");
					cumulus.LogMessage($"ZeroConf Service: Changing previous {locationStr} IP address: {ipaddr} to {discovered.IP[0]}");

					ipaddr = discovered.IP[0];

					if (indoor)
						cumulus.AirLinkInIPAddr = ipaddr;
					else
						cumulus.AirLinkOutIPAddr = ipaddr;
				}
				else
				{
					cumulus.LogMessage($"ZeroConf Service: Auto-discovery found the AirLink, reporting its IP address as: {ipaddr}");
				}

				if (writeConfig)
				{
					cumulus.WriteIniFile();
					cumulus.LogMessage($"ZeroConf Service: Auto-discovered AirLink name {discovered.Hostname[0]}, on IP address {ipaddr}");
				}
			}
			else if (discovered.Hostname.Contains(hostname))
			{
				// Multiple devices discovered, but we have a Host-name match
				cumulus.LogDebugMessage($"ZeroConf Service: Matching {locationStr} AirLink host name found on the network");

				var idx = discovered.Hostname.IndexOf(hostname);

				if (discovered.IP[idx] != ipaddr)
				{
					cumulus.LogMessage($"ZeroConf Service: Discovered a new IP address for the {locationStr} AirLink that does not match our current one");
					cumulus.LogMessage($"ZeroConf Service: Changing previous {locationStr} IP address: {ipaddr} to {discovered.IP[idx]}");
					ipaddr = discovered.IP[idx];
					if (indoor)
						cumulus.AirLinkInIPAddr = ipaddr;
					else
						cumulus.AirLinkOutIPAddr = ipaddr;

					cumulus.WriteIniFile();
				}
				else
				{
					cumulus.LogDebugMessage($"ZeroConf Service: {locationStr} AirLink IP address has not changed");
				}
			}
			else if (discovered.IP.Contains(ipaddr))
			{
				// Multiple devices discovered, no host-name match but we have an IP match
				cumulus.LogDebugMessage($"ZeroConf Service: Matching {locationStr} AirLink IP address found on the network");

				var idx = discovered.IP.IndexOf(ipaddr);

				if (discovered.Hostname[idx] != hostname)
				{
					cumulus.LogDebugMessage($"ZeroConf Service: Changing previous {locationStr} host name '{hostname}' to '{discovered.Hostname[idx]}'");
					hostname = discovered.Hostname[idx];
					if (indoor)
						cumulus.AirLinkInHostName = hostname;
					else
						cumulus.AirLinkOutHostName = hostname;

					cumulus.WriteIniFile();
				}
			}
			else
			{
				// Multiple devices discovered, and we do not have a clue!
				string list = "";
				msg = "*** Discovered more than one potential AirLink device.";
				cumulus.LogMessage("ZeroConf Service: " + msg);
				cumulus.LogConsoleMessage(msg);
				msg = "*** Please select the Host name/IP address from the list and enter it manually into the configuration";
				cumulus.LogMessage("ZeroConf Service: " + msg);
				cumulus.LogConsoleMessage(msg);
				for (var i = 0; i < discovered.IP.Count; i++)
				{
					list += discovered.Hostname[i] + "/" + discovered.IP[i] + " ";
				}
				msg = "*** Discovered AirLinks = " + list;
				cumulus.LogMessage("ZeroConf Service: " + msg);
				cumulus.LogConsoleMessage(msg);
			}

			wlHttpClient.Timeout = TimeSpan.FromSeconds(20); // 20 seconds for internet queries
			dogsBodyClient.Timeout = TimeSpan.FromSeconds(10); // 10 seconds for local queries

			// Only start reading history if the main station isn't a WLL
			// and we have a station id
			if (standaloneHistory)
			{
				// Read the data from the WL APIv2
				//AlReadHistory();

				// Get the available sensor id - required to find the Health Sensor Id
				GetAvailableSensors();

				// Now find our corresponding Health sensor LSID
				healthLsid = GetWlHistoricHealthLsid((this.indoor ? cumulus.airLinkInLsid : cumulus.airLinkOutLsid), 506);

				// Fetch the current health data to pre-polulate web tags
				GetWlHistoricHealth();
			}
		}

		public void Start()
		{
			cumulus.LogMessage($"AirLink {locationStr} Starting up");
			try
			{
				// Get the current conditions and health immediately to populate the web tags
				GetAlCurrent(null, null);
				GetWlHistoricHealth();

				// Create a current conditions thread to poll readings every 30 seconds
				tmrCurrent.Elapsed += GetAlCurrent;
				tmrCurrent.Interval = 30 * 1000;  // Every 30 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();

				// Only poll health data here if the AirLink is a stand-alone device - the stand-alone history flag shows we have all the required info to poll wl.com
				if (standaloneHistory)
				{
					// get the health data every 15 minutes
					tmrHealth = new System.Timers.Timer();
					tmrHealth.Elapsed += HealthTimerTick;
					tmrHealth.Interval = 60 * 1000;  // Tick every minute
					tmrHealth.AutoReset = true;
					tmrHealth.Start();
				}
			}
			catch (ThreadAbortException)
			{
			}
			cumulus.LogMessage($"AirLink {locationStr} Started");
		}

		public void Stop()
		{
			try
			{
				cumulus.LogMessage($"AirLink {locationStr} Stopping");
				tmrCurrent.Stop();
				tmrHealth?.Stop();
				cumulus.LogMessage($"AirLink {locationStr} Stopped");
			}
			catch { }
		}

		private async void GetAlCurrent(object source, ElapsedEventArgs e)
		{
			string ip;
			int retry = 1;

			if (updateInProgress)
			{
				cumulus.LogDebugMessage("GetAlCurrent: Previous update is still running");
				return;
			}
			updateInProgress = true;

			lock (threadSafer)
			{
				ip = (indoor ? cumulus.AirLinkInIPAddr : cumulus.AirLinkOutIPAddr);
			}

			if (CheckIpValid(ip))
			{

				var urlCurrent = $"http://{ip}/v1/current_conditions";

				cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Waiting for lock");
				WebReq.Wait();
				cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Has the lock");

				// The AL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Sending GET current conditions request {retry} to AL: {urlCurrent} ...");
					// Call asynchronous network methods in a try/catch block to handle exceptions
					try
					{
						string responseBody;

						using (HttpResponseMessage response = await dogsBodyClient.GetAsync(urlCurrent))
						{
							response.EnsureSuccessStatusCode();
							responseBody = await response.Content.ReadAsStringAsync();
							cumulus.LogDataMessage($"GetAlCurrent: Response - {responseBody}");
						}

						try
						{
							DecodeAlCurrent(responseBody);
							if (startupDayResetIfRequired)
							{
								station.DoDayResetIfNeeded();
								startupDayResetIfRequired = false;
							}
						}
						catch (Exception ex)
						{
							cumulus.LogMessage("GetAlCurrent: Error processing the AirLink response");
							cumulus.LogMessage("GetAlCurrent: Error: " + ex.Message);
						}
						retry = 9;
					}
					catch (Exception exp)
					{
						retry++;
						cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Exception: {exp.Message}");
						Thread.Sleep(1000);
					}
				} while (retry < 3);

				cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Releasing lock");
				WebReq.Release();
			}
			else
			{
				cumulus.LogMessage($"GetAlCurrent: {locationStr} - Invalid IP address: {ip}");
			}
			updateInProgress = false;
		}

		private void DecodeAlCurrent(string currentJson)
		{
			try
			{
				// Convert JSON string to an object
				var json = currentJson.FromJson<AlCurrent>();

				// The WLL sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the WLL clock being correct, we will use our local time
				//var dateTime = Utils.FromUnixTime(data.Value<int>("ts"));

				// The current conditions is sent as an array, even though it only contains 1 record
				var rec = json.data.conditions.First();

				var type = rec.data_structure_type;

				switch (type)
				{
					case 5: // AirLink - original firmware
					case 6: // AirLink - newer firmware
						cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Found AirLink data");

						// Temperature & Humidity
						/* Available fields
							* "temp": 62.7,                                  // most recent valid temperature **(°F)**
							* "hum":1.1,                                     // most recent valid humidity **(%RH)**
							* "dew_point": -0.3,                             // **(°F)**
							* "wet_bulb":null,                               // **(°F)**
							* "heat_index": 5.5,                             // **(°F)**
							*/

						try
						{
							cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Using temp/hum data");

							if (indoor)
							{
								cumulus.airLinkDataIn.temperature = station.ConvertTempFToUser(rec.temp);
							}
							else
							{
								cumulus.airLinkDataOut.temperature = station.ConvertTempFToUser(rec.temp);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Error processing temperature value. Error msg: {ex.Message}");
						}


						try
						{
							if (indoor)
							{
								cumulus.airLinkDataIn.humidity = Convert.ToInt32(rec.hum);
							}
							else
							{
								cumulus.airLinkDataOut.humidity = Convert.ToInt32(rec.hum);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Error processing humidity value. Error: {ex.Message}");
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

						try
						{
							if (indoor)
							{
								cumulus.airLinkDataIn.pm1 = rec.pm_1;
								cumulus.airLinkDataIn.pm2p5 = rec.pm_2p5;
								cumulus.airLinkDataIn.pm2p5_1hr = rec.pm_2p5_last_1_hour;
								cumulus.airLinkDataIn.pm2p5_3hr = rec.pm_2p5_last_3_hours;
								cumulus.airLinkDataIn.pm2p5_24hr = rec.pm_2p5_last_24_hours;
								cumulus.airLinkDataIn.pm2p5_nowcast = rec.pm_2p5_nowcast;
								if (type == 5)
								{
									cumulus.airLinkDataIn.pm10 = rec.pm_10p0;
									cumulus.airLinkDataIn.pm10_1hr = rec.pm_10p0_last_1_hour;
									cumulus.airLinkDataIn.pm10_3hr = rec.pm_10p0_last_3_hours;
									cumulus.airLinkDataIn.pm10_24hr = rec.pm_10p0_last_24_hours;
									cumulus.airLinkDataIn.pm10_nowcast = rec.pm_10p0_nowcast;
								}
								else
								{
									cumulus.airLinkDataIn.pm10 = rec.pm_10;
									cumulus.airLinkDataIn.pm10_1hr = rec.pm_10_last_1_hour;
									cumulus.airLinkDataIn.pm10_3hr = rec.pm_10_last_3_hours;
									cumulus.airLinkDataIn.pm10_24hr = rec.pm_10_last_24_hours;
									cumulus.airLinkDataIn.pm10_nowcast = rec.pm_10_nowcast;
								}
								cumulus.airLinkDataIn.pct_1hr = rec.pct_pm_data_last_1_hour;
								cumulus.airLinkDataIn.pct_3hr = rec.pct_pm_data_last_3_hours;
								cumulus.airLinkDataIn.pct_24hr = rec.pct_pm_data_last_24_hours;
								cumulus.airLinkDataIn.pct_nowcast = rec.pct_pm_data_nowcast;

								// now do the AQIs
								DoAqi(cumulus.airLinkDataIn);
							}
							else
							{
								cumulus.airLinkDataOut.pm1 = rec.pm_1;
								cumulus.airLinkDataOut.pm2p5 = rec.pm_2p5;
								cumulus.airLinkDataOut.pm2p5_1hr = rec.pm_2p5_last_1_hour;
								cumulus.airLinkDataOut.pm2p5_3hr = rec.pm_2p5_last_3_hours;
								cumulus.airLinkDataOut.pm2p5_24hr = rec.pm_2p5_last_24_hours;
								cumulus.airLinkDataOut.pm2p5_nowcast = rec.pm_2p5_nowcast;
								if (type == 5)
								{
									cumulus.airLinkDataOut.pm10 = rec.pm_10p0;
									cumulus.airLinkDataOut.pm10_1hr = rec.pm_10p0_last_1_hour;
									cumulus.airLinkDataOut.pm10_3hr = rec.pm_10p0_last_3_hours;
									cumulus.airLinkDataOut.pm10_24hr = rec.pm_10p0_last_24_hours;
									cumulus.airLinkDataOut.pm10_nowcast = rec.pm_10p0_nowcast;
								}
								else
								{
									cumulus.airLinkDataOut.pm10 = rec.pm_10;
									cumulus.airLinkDataOut.pm10_1hr = rec.pm_10_last_1_hour;
									cumulus.airLinkDataOut.pm10_3hr = rec.pm_10_last_3_hours;
									cumulus.airLinkDataOut.pm10_24hr = rec.pm_10_last_24_hours;
									cumulus.airLinkDataOut.pm10_nowcast = rec.pm_10_nowcast;
								}
								cumulus.airLinkDataOut.pct_1hr = rec.pct_pm_data_last_1_hour;
								cumulus.airLinkDataOut.pct_3hr = rec.pct_pm_data_last_3_hours;
								cumulus.airLinkDataOut.pct_24hr = rec.pct_pm_data_last_24_hours;
								cumulus.airLinkDataOut.pct_nowcast = rec.pct_pm_data_nowcast;

								// now do the AQIs
								DoAqi(cumulus.airLinkDataOut);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Error processing PM values. Error: {ex.Message}");
						}

						break;

					default:
						cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Found an unknown transmitter type [{type}]!");
						break;
				}
				//UpdateStatusPanel(DateTime.Now);
				//UpdateMQTT();
			}
			catch (Exception exp)
			{

				cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Exception: {exp.Message}");
			}
		}


		private void AlReadHistory(object sender, DoWorkEventArgs e)
		{
			int archiveRun = 0;

			try
			{
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
				cumulus.LogMessage("AirLink: Exception occurred reading archive data: " + ex.Message);
			}
		}


		private void GetWlHistoricData()
		{
			var stationId = indoor ? cumulus.AirLinkInStationId : cumulus.AirLinkOutStationId;

			cumulus.LogMessage("GetWlHistoricData: Get WL.com Historic Data");

			if (cumulus.AirLinkApiKey == string.Empty || cumulus.AirLinkApiSecret == string.Empty)
			{
				cumulus.LogMessage("GetWlHistoricData: Missing AirLink WeatherLink API data in the configuration, aborting!");
				return;
			}

			if (stationId < 10)
			{
				var msg = "No AirLink WeatherLink API station ID in the configuration";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage("GetWlHistoricData: " + msg);
				return;
			}

			//int passCount;
			//const int maxPasses = 4;

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = Utils.ToUnixTime(airLinkLastUpdateTime);
			int endTime = unixDateTime;
			int unix24hrs = 24 * 60 * 60;

			// The API call is limited to fetching 24 hours of data
			if (unixDateTime - startTime > unix24hrs)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime + unix24hrs;
				maxArchiveRuns++;
			}

			cumulus.LogConsoleMessage($"Downloading Historic Data from WL.com from: {airLinkLastUpdateTime:s} to: {Utils.FromUnixTime(endTime):s}");
			cumulus.LogMessage($"GetWlHistoricData: Downloading Historic Data from WL.com from: {airLinkLastUpdateTime:s} to: {Utils.FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", cumulus.AirLinkApiKey },
				{ "station-id", stationId.ToString() },
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

			string apiSignature = WlDotCom.CalculateApiSignature(cumulus.AirLinkApiSecret, data);

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

			string logUrl = historicUrl.ToString().Replace(cumulus.AirLinkApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"GetWlHistoricData: WeatherLink URL = {logUrl}");
			station.lastDataReadTime = airLinkLastUpdateTime;

			WlHistory histObj;
			WlHistorySensor sensorWithMostRecs;

			int noOfRecs = 0;

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
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"GetWlHistoricData: WeatherLink API Historic Error: {errObj.code}, {errObj.message}");
					cumulus.LogConsoleMessage($" - Error {errObj.code}: {errObj.message}");
					airLinkLastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogMessage("GetWlHistoricData: WeatherLink API Historic: No data was returned. Check your Device Id.");
					cumulus.LogConsoleMessage(" - No historic data available");
					airLinkLastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				if (!responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
				{
					cumulus.LogMessage("GetWlHistoricData: Invalid historic message received");
					cumulus.LogDataMessage("GetWlHistoricData: Received: " + responseBody);
					airLinkLastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				histObj = responseBody.FromJson<WlHistory>();

				// get the sensor data with the most number of history records
				int idxOfSensorWithMostRecs = 0;
				for (var i = 0; i < histObj.sensors.Count; i++)
				{
					if (histObj.sensors[i].sensor_type != 504)
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
					airLinkLastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				cumulus.LogMessage($"GetWlHistoricData: Found {noOfRecs} historic records to process");
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetWlHistoricData:  Exception: " + ex.Message);
				airLinkLastUpdateTime = Utils.FromUnixTime(endTime);
				return;
			}

			for (int dataIndex = 0; dataIndex < noOfRecs; dataIndex++)
			{
				try
				{
					var refData = sensorWithMostRecs.data[dataIndex].FromJsv<WlHistorySensorDataType13Baro>();

					DateTime timestamp = new DateTime();
					foreach (WlHistorySensor sensor in histObj.sensors)
					{
						var sensorType = sensor.sensor_type;

						if (sensorType == 323 && !indoor) // AirLink Outdoor
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
										cumulus.airLinkOut.DecodeAlHistoric(sensor.data_structure_type, dataRec);
										found = true;
										break;
									}
								}
								if (!found)
								{
									cumulus.LogDebugMessage("GetWlHistoricData: Warning. No outdoor AirLink data for this log interval !!");
								}
							}
							else
							{
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkOut.DecodeAlHistoric(sensor.data_structure_type, sensor.data[dataIndex]);
							}
						}
						else if (sensorType == 326 && indoor) // AirLink Indoor
						{
							if (sensor.data.Count != noOfRecs)
							{
								var found = false;
								foreach (var dataJson in sensor.data)
								{
									var rec = dataJson.FromJsv<WlHistorySensorDataType17>();
									if (rec.ts == refData.ts)
									{
										// Pass AirLink historic record to the AirLink module to process
										cumulus.airLinkIn.DecodeAlHistoric(sensor.data_structure_type, dataJson);
										found = true;
										break;
									}
								}
								if (!found)
								{
									cumulus.LogDebugMessage("GetWlHistoricData: Warning. No indoor AirLink data for this log interval !!");
								}
							}
							else
							{
								// Pass AirLink historic record to the AirLink module to process
								cumulus.airLinkIn.DecodeAlHistoric(sensor.data_structure_type, sensor.data[dataIndex]);
							}
						}
						else
						{
							// Pass AirLink historic record to the AirLink module to process
							cumulus.airLinkOut.DecodeAlHistoric(sensor.data_structure_type, sensor.data[dataIndex]);
						}
					}

					cumulus.DoAirLinkLogFile(timestamp);

					if (!Program.service)
						Console.Write("\r - processed " + (((double)dataIndex + 1) / noOfRecs).ToString("P0"));
					cumulus.LogMessage($"GetWlHistoricData: {(dataIndex + 1)} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("GetWlHistoricData: Exception: " + ex.Message);
				}
			}

			if (!Program.service)
				Console.WriteLine(""); // flush the progress line
		}

		public void DecodeAlHistoric(int dataType, string json)
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


						var data17 = json.FromJsv<WlHistorySensorDataType17>();

						try
						{
							cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - Using temp/hum data");

							if (data17.temp_avg == -99)
							{
								cumulus.LogMessage($"DecodeAlHistoric: No valid temperature value found");
							}
							else
							{
								if (indoor)
								{
									cumulus.airLinkDataIn.temperature = station.ConvertTempFToUser(data17.temp_avg);
								}
								else
								{
									cumulus.airLinkDataOut.temperature = station.ConvertTempFToUser(data17.temp_avg);
								}
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage(
								$"DecodeAlHistoric: {locationStr} - Error processing avg temperature. Error: {ex.Message}");
						}


						try
						{
							if (indoor)
							{
								cumulus.airLinkDataIn.humidity = Convert.ToInt32(data17.hum_last);
							}
							else
							{
								cumulus.airLinkDataOut.humidity = Convert.ToInt32(data17.hum_last);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage(
								$"DecodeAlHistoric: {locationStr} - Error processing humidity. Error: {ex.Message}");
						}

						try
						{
							cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - Using PM data");
							if (indoor)
							{
								cumulus.airLinkDataIn.pm1 = data17.pm_1_avg;
								cumulus.airLinkDataIn.pm2p5 = data17.pm_2p5_avg;
								//cumulus.airLinkDataIn.pm2p5_1hr = data.Value<double>("pm_2p5_last_1_hour");
								//cumulus.airLinkDataIn.pm2p5_3hr = rec.Value<double>("pm_2p5_last_3_hours");
								//cumulus.airLinkDataIn.pm2p5_24hr = rec.Value<double>("pm_2p5_last_24_hours");
								//cumulus.airLinkDataIn.pm2p5_nowcast = rec.Value<double>("pm_2p5_nowcast");

								cumulus.airLinkDataIn.pm10 = data17.pm_10_avg;
								//cumulus.airLinkDataIn.pm10_1hr = rec.Value<double>("pm_10_last_1_hour");
								//cumulus.airLinkDataIn.pm10_3hr = rec.Value<double>("pm_10_last_3_hours");
								//cumulus.airLinkDataIn.pm10_24hr = rec.Value<double>("pm_10_last_24_hours");
								//cumulus.airLinkDataIn.pm10_nowcast = rec.Value<double>("pm_10_nowcast");

								//cumulus.airLinkDataIn.pct_1hr = (int)data.pm_10_avg_num_part;
								//cumulus.airLinkDataIn.pct_3hr = rec.Value<int>("pct_pm_data_last_3_hours");
								//cumulus.airLinkDataIn.pct_24hr = rec.Value<int>("pct_pm_data_last_24_hours");
								//cumulus.airLinkDataIn.pct_nowcast = rec.Value<int>("pct_pm_data_nowcast");

								DoAqi(cumulus.airLinkDataIn);

								// If we are the primary AQ sensor,
								// and we are not linked to a WLL,
								// then add the PM data into the graphdata list
								if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkIndoor && standaloneHistory)
								{
									//station.UpdateGraphDataAqEntry(Utils.FromUnixTime(data17.ts), cumulus.airLinkDataIn.pm2p5, cumulus.airLinkDataIn.pm10);
									station.UpdateRecentDataAqEntry(Utils.FromUnixTime(data17.ts), cumulus.airLinkDataIn.pm2p5, cumulus.airLinkDataIn.pm10);
								}
							}
							else
							{
								cumulus.airLinkDataOut.pm1 = data17.pm_1_avg;
								cumulus.airLinkDataOut.pm2p5 = data17.pm_2p5_avg;
								//cumulus.airLinkDataOut.pm2p5_1hr = rec.Value<double>("pm_2p5_last_1_hour");
								//cumulus.airLinkDataOut.pm2p5_3hr = rec.Value<double>("pm_2p5_last_3_hours");
								//cumulus.airLinkDataOut.pm2p5_24hr = rec.Value<double>("pm_2p5_last_24_hours");
								//cumulus.airLinkDataOut.pm2p5_nowcast = rec.Value<double>("pm_2p5_nowcast");

								cumulus.airLinkDataOut.pm10 = data17.pm_10_avg;
								//cumulus.airLinkDataOut.pm10_1hr = rec.Value<double>("pm_10_last_1_hour");
								//cumulus.airLinkDataOut.pm10_3hr = rec.Value<double>("pm_10_last_3_hours");
								//cumulus.airLinkDataOut.pm10_24hr = rec.Value<double>("pm_10_last_24_hours");
								//cumulus.airLinkDataOut.pm10_nowcast = rec.Value<double>("pm_10_nowcast");

								//cumulus.airLinkDataOut.pct_1hr = (int)data.pm_10_avg_num_part;
								//cumulus.airLinkDataOut.pct_3hr = rec.Value<int>("pct_pm_data_last_3_hours");
								//cumulus.airLinkDataOut.pct_24hr = rec.Value<int>("pct_pm_data_last_24_hours");
								//cumulus.airLinkDataOut.pct_nowcast = rec.Value<int>("pct_pm_data_nowcast");

								DoAqi(cumulus.airLinkDataOut);

								// If we are the primary AQ sensor,
								// and we are not linked to a WLL,
								// then add the PM data into the graphdata list
								if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor && standaloneHistory)
								{
									//station.UpdateGraphDataAqEntry(Utils.FromUnixTime(data17.ts), cumulus.airLinkDataOut.pm2p5, cumulus.airLinkDataOut.pm10);
									station.UpdateRecentDataAqEntry(Utils.FromUnixTime(data17.ts), cumulus.airLinkDataOut.pm2p5, cumulus.airLinkDataOut.pm10);
								}
							}
						}
						catch (Exception ex)
						{
							cumulus.LogDebugMessage(
								$"DecodeAlHistoric: {locationStr} - Error processing PM data. Error: {ex.Message}");
						}

						break;

					default:
						cumulus.LogDebugMessage($"DecodeAlHistoric: Unknown data type found - {dataType}");
						break;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"DecodeAlHistoric: {locationStr} - Exception: {ex.Message}");
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
			WlHistory histObj;

			string apiKey;
			string apiSecret;
			int stationId;

			// Are we stand-alone?
			if (standalone)
			{
				if (cumulus.AirLinkApiKey == string.Empty || cumulus.AirLinkApiSecret == string.Empty)
				{
					var msg = "Missing AirLink WeatherLink API key/secret in the cumulus.ini file";
					cumulus.LogConsoleMessage(msg);
					cumulus.LogMessage("AirLinkHealth: " + msg);
					return;
				}
				apiKey = cumulus.AirLinkApiKey;
				apiSecret = cumulus.AirLinkApiSecret;

				if ((indoor ? cumulus.AirLinkInStationId : cumulus.AirLinkOutStationId) < 10)
				{
					var msg = "Missing AirLink WeatherLink API station Id in the cumulus.ini file";
					cumulus.LogConsoleMessage(msg);
					cumulus.LogMessage("AirLinkHealth: " + msg);
					GetAvailableStationIds();
					return;
				}
				stationId = indoor ? cumulus.AirLinkInStationId : cumulus.AirLinkOutStationId;
			}
			else
			{
				if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty || cumulus.WllStationId < 10)
				{
					cumulus.LogMessage("AirLinkHealth: Missing WLL WeatherLink API key/secret/station Id in the cumulus.ini file, aborting!");
					return;
				}
				apiKey = cumulus.WllApiKey;
				apiSecret = cumulus.WllApiSecret;
				stationId = cumulus.WllStationId;
			}

			cumulus.LogMessage("AirLinkHealth: Get WL.com Historic Data");

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);
			var startTime = unixDateTime - WeatherLinkArchiveInterval;
			int endTime = unixDateTime;

			cumulus.LogDebugMessage($"AirLinkHealth: Downloading the historic record from WL.com from: {Utils.FromUnixTime(startTime):s} to: {Utils.FromUnixTime(endTime):s}");

			SortedDictionary<string, string> parameters = new SortedDictionary<string, string>
			{
				{ "api-key", apiKey },
				{ "station-id", stationId.ToString() },
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

			var apiSignature = WlDotCom.CalculateApiSignature(apiSecret, data);

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

			var logUrl = historicUrl.ToString().Replace(apiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"AirLinkHealth: WeatherLink URL = {logUrl}");

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = wlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDataMessage($"AirLinkHealth: WeatherLink API Response: {responseCode}: {responseBody}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"AirLinkHealth: WeatherLink API Error: {errObj.code}, {errObj.message}");
					return;
				}

				if (responseBody == "{}")
				{
					cumulus.LogMessage("AirLinkHealth: WeatherLink API: No data was returned. Check your Device Id.");
					airLinkLastUpdateTime = Utils.FromUnixTime(endTime);
					return;
				}

				if (!responseBody.StartsWith("{\"sensors\":[{\"lsid\"")) // sanity check
				{
					// No idea what we got, dump it to the log
					cumulus.LogMessage("AirLinkHealth: Invalid historic message received");
					cumulus.LogDataMessage("AirLinkHealth: Received: " + responseBody);
					return;
				}

				histObj = responseBody.FromJson<WlHistory>();

				if (histObj.sensors.Count == 0)
				{
					cumulus.LogMessage("AirLinkHealth: No historic data available");
					return;
				}
				else
				{
					cumulus.LogDebugMessage($"AirLinkHealth: Found {histObj.sensors.Count} sensor records to process");
				}

				try
				{
					foreach (var sensor in histObj.sensors)
					{
						if (sensor.sensor_type == 506 && sensor.lsid == healthLsid) // AirLink Outdoor
						{
							DecodeWlApiHealth(sensor, true);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("AirLinkHealth: exception: " + ex.Message);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("AirLinkHealth: exception: " + ex.Message);
			}

			//cumulus.BatteryLowAlarmState = TxBatText.Contains("LOW") || wllVoltageLow;
		}

		internal void DecodeWlApiHealth(WlHistorySensor sensor, bool startingup)
		{
			if (sensor.data.Count == 0)
			{
				if (sensor.data_structure_type == 18)
				{
					cumulus.LogDebugMessage("AirLinkHealth: Did not find any health data for AirLink device");
				}
				return;
			}

			if (sensor.data_structure_type == 18)
			{
				/* AirLink
				 * Available fields of interest to health
					"air_quality_firmware_version": 1598649428	- OLD
					"firmware_version": 1598649428				- NEW
					"application_version": "v1.0.0"
					"bootloader_version": 527991452
					"dns_type_used": null
					"tx_packets": 40720
					"rx_packets": 50260
					"dropped_packets": 26635
					"packet_errors": 0
					"network_error": null
					"local_api_queries": 0
					"health_version": 2
					"internal_free_mem_chunk_size": 34812	- bytes
					"internal_free_mem_watermark": 54552	- bytes
					"internal_free_mem": 75596				- bytes
					"internal_used_mem": 139656				- bytes
					"ip_address_type": 1					- 1=Dynamic, 2=Dyn DNS Override, 3=Static
					"ip_v4_address": "192.168.68.137"
					"ip_v4_gateway": "192.168.68.1"
					"ip_v4_netmask": "255.255.255.0"
					"record_backlog_count": 0
					"record_stored_count": 2048
					"record_write_count": 5106
					"total_free_mem": 2212176
					"total_used_mem": 779380
					"uptime": 283862
					"link_uptime": 283856
					"wifi_rssi": -48
					"ts": 1598937300
				 */

				cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Found health data for AirLink device");
				try
				{
					var data = sensor.data.Last().FromJsv<WlHistorySensorDataType18>();

					try
					{
						// Davis are changing the API, from air_quality_firmware_version to firmware_version
						var dat = Utils.FromUnixTime(data.air_quality_firmware_version ?? data.firmware_version.Value);
						if (indoor)
							cumulus.airLinkDataIn.firmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");
						else
							cumulus.airLinkDataOut.firmwareVersion = dat.ToUniversalTime().ToString("yyyy-MM-dd");
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - Error processing firmware version: {ex.Message}");
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - No valid firmware version found");
						if (indoor)
						{
							cumulus.airLinkDataIn.firmwareVersion = "???";
						}
						else
						{
							cumulus.airLinkDataOut.firmwareVersion = "???";
						}
					}

					if (startingup)
					{
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - FW version = " + (indoor ? cumulus.airLinkDataIn.firmwareVersion : cumulus.airLinkDataOut.firmwareVersion));
					}
					else
					{
						cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - FW version = " + (indoor ? cumulus.airLinkDataIn.firmwareVersion : cumulus.airLinkDataOut.firmwareVersion));
					}


					try
					{
						var upt = TimeSpan.FromSeconds(data.uptime);
						var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
								(int)upt.TotalDays,
								upt.Hours,
								upt.Minutes,
								upt.Seconds);
						cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Uptime = " + uptStr);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - Error processing uptime: {ex.Message}");
					}

					try
					{
						var upt = TimeSpan.FromSeconds(data.link_uptime);
						var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
								(int)upt.TotalDays,
								upt.Hours,
								upt.Minutes,
								upt.Seconds);
						cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Link Uptime = " + uptStr);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - Error processing link uptime: {ex.Message}");
					}

					// Only present if WiFi attached
					if (!data.wifi_rssi.HasValue)
					{
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - No WiFi RSSI value found");
					}
					else
					{
						if (indoor)
						{
							cumulus.airLinkDataIn.wifiRssi = data.wifi_rssi.Value;
						}
						else
						{
							cumulus.airLinkDataOut.wifiRssi = data.wifi_rssi.Value;
						}
						cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - WiFi RSSI={data.wifi_rssi}dB");
					}

					try
					{
						var txCnt = (int)data.tx_packets;
						var rxCnt = (int)data.rx_packets;
						var dropped = data.dropped_packets;
						var bad = data.packet_errors;
						var error = data.network_error.HasValue ? data.network_error.Value.ToString() : "none";
						cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Network:  Tx={txCnt}, Rx={rxCnt}, drop={dropped}, bad={bad}, error='{error}'");
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"AirLinkHealth: {locationStr} - Error processing xmt count: {ex.Message}");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"AirLinkHealth: {locationStr} - Exception caught in health data: {ex.Message}");
				}
			}
		}

		private bool GetAvailableStationIds()
		{
			WlStationList stationsObj;

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);

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

			var logUrl = stationsUrl.ToString().Replace(cumulus.AirLinkApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"WeatherLink Stations URL = {logUrl}");

			try
			{
				string responseBody;
				int responseCode;
				// We want to do this synchronously

				using (HttpResponseMessage response = wlHttpClient.GetAsync(stationsUrl.ToString()).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int)response.StatusCode;
					cumulus.LogDebugMessage($"WeatherLink API Response: {responseCode}: {responseBody}");
				}

				if (responseCode != 200)
				{
					var errObj = responseBody.FromJson<WlErrorResponse>();
					cumulus.LogMessage($"WeatherLink API Error: {errObj.code} - {errObj.message}");
					return false;
				}

				stationsObj = responseBody.FromJson<WlStationList>();

				foreach (var station in stationsObj.stations)
				{
					cumulus.LogMessage($"Found WeatherLink station id = {station.station_id}, name = {station.station_name}");
					if (stationsObj.stations.Count > 1)
					{
						cumulus.LogConsoleMessage($" - Found WeatherLink station id = {station.station_id}, name = {station.station_name}, active = {station.active}");
					}

					if ((station.station_id == cumulus.AirLinkInStationId || station.station_id == cumulus.AirLinkOutStationId) && station.recording_interval != cumulus.logints[cumulus.DataLogInterval])
					{
						cumulus.LogMessage($" - Cumulus log interval {cumulus.logints[cumulus.DataLogInterval]} does not match this WeatherLink stations log interval {station.recording_interval}");
					}
				}
				if (stationsObj.stations.Count > 1)
				{
					cumulus.LogConsoleMessage(" - Enter the required station id from the above list into your AirLink configuration to enable history downloads.");
				}

				if (stationsObj.stations.Count == 1)
				{
					cumulus.LogMessage($"Only found 1 WeatherLink station, using id = {stationsObj.stations[0].station_id}");
					if (indoor)
					{
						cumulus.AirLinkInStationId = stationsObj.stations[0].station_id;
					}
					else
					{
						cumulus.AirLinkOutStationId = stationsObj.stations[0].station_id;
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

		private void GetAvailableSensors()
		{
			WlSensorList sensorsObj;

			var unixDateTime = Utils.ToUnixTime(DateTime.Now);

			if (cumulus.WllApiKey == string.Empty || cumulus.WllApiSecret == string.Empty)
			{
				cumulus.LogMessage("GetAvailableSensors: WeatherLink API data is missing in the configuration, aborting!");
				return;
			}

			if (cumulus.WllStationId < 10)
			{
				cumulus.LogMessage($"GetAvailableSensors: No WeatherLink API station ID has been configured, aborting!");
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

			StringBuilder sensorsUrl = new StringBuilder();
			sensorsUrl.Append("https://api.weatherlink.com/v2/sensors?");
			foreach (KeyValuePair<string, string> entry in parameters)
			{
				sensorsUrl.Append(entry.Key);
				sensorsUrl.Append("=");
				sensorsUrl.Append(entry.Value);
				sensorsUrl.Append("&");
			}
			// remove the trailing "&"
			sensorsUrl.Remove(sensorsUrl.Length - 1, 1);

			var logUrl = sensorsUrl.ToString().Replace(cumulus.WllApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"GetAvailableSensors: URL = {logUrl}");

			try
			{
				string responseBody;
				int responseCode;
				// We want to do this synchronously
				using (HttpResponseMessage response = wlHttpClient.GetAsync(sensorsUrl.ToString()).Result)
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

				//sensorsObj = JsonConvert.DeserializeObject<WlSensorList>(responseBody);
				sensorsObj = responseBody.FromJson<WlSensorList>();

				WlSensor wl_sensor;

				// Sensor types we are interested in...
				// 323 = Outdoor AirLink
				// 326 = Indoor AirLink
				// 504 = WLL Health
				// 506 = AirLink Health
				var types = new int[] { 45, 323, 326, 504, 506 };
				foreach (var sensor in sensorsObj.sensors)
				{
					cumulus.LogDebugMessage($"GetAvailableSensors: Found WeatherLink Sensor type={sensor.sensor_type}, lsid={sensor.lsid}, station_id={sensor.station_id}, name={sensor.product_name}, parentId={sensor.parent_device_id}, parent={sensor.parent_device_name}");

					if (types.Contains(sensor.sensor_type) || sensor.category == "ISS")
					{
						wl_sensor = new WlSensor(sensor.sensor_type, sensor.lsid, sensor.parent_device_id, sensor.product_name, sensor.parent_device_name);
						sensorList.Add(wl_sensor);
						if (wl_sensor.SensorType == 323 && sensor.station_id == cumulus.AirLinkOutStationId)
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Outdoor LSID to {wl_sensor.LSID}");
							cumulus.airLinkOutLsid = wl_sensor.LSID;
						}
						else if (wl_sensor.SensorType == 326 && sensor.station_id == cumulus.AirLinkInStationId)
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Indoor LSID to {wl_sensor.LSID}");
							cumulus.airLinkInLsid = wl_sensor.LSID;
						}
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("GetAvailableSensors: WeatherLink API exception: " + ex.Message);
			}
		}

		private void OnServiceChanged(object sender, ServiceAnnouncementEventArgs e)
		{
			cumulus.LogMessage($"ZeroConfig Service: AirLink {e.Announcement.Hostname} service has changed!");
			if (discovered.Hostname.Contains(e.Announcement.Hostname))
			{
				var idx = discovered.Hostname.IndexOf(e.Announcement.Hostname);
				cumulus.LogDebugMessage($"Changing AirLink {e.Announcement.Hostname}  IP address from {discovered.IP[idx]} to {e.Announcement.Addresses[0]}");
				discovered.IP[idx] = e.Announcement.Addresses[0].ToString();
			}
		}

		private void OnServiceRemoved(object sender, ServiceAnnouncementEventArgs e)
		{
			cumulus.LogMessage($"ZeroConfig Service: AirLink {e.Announcement.Hostname} service has been removed!");
			if (discovered.Hostname.Contains(e.Announcement.Hostname))
			{
				//cumulus.LogDebugMessage($"ZeroConfig Service: Removing {e.Announcement.Hostname} / {e.Announcement.Addresses[0]} from the discovered device list");
				//discovered.Hostname.Remove(e.Announcement.Hostname);
				//discovered.IP.Remove(e.Announcement.Addresses[0].ToString());
			}
		}

		private void OnServiceAdded(object sender, ServiceAnnouncementEventArgs e)
		{
			PrintService('+', e.Announcement);
		}

		private void PrintService(char startChar, ServiceAnnouncement service)
		{
			cumulus.LogDebugMessage($"ZeroConf Service: {startChar} '{service.Instance}' on {service.NetworkInterface.Name}");
			cumulus.LogDebugMessage($"\tHost: {service.Hostname} ({string.Join(", ", service.Addresses)})");

			//var currIpAddr = indoor ? cumulus.AirLinkInIPAddr : cumulus.AirLinkOutIPAddr;
			//var hostname = indoor ? cumulus.airLinkInHostName : cumulus.airLinkInHostName;

			lock (threadSafer)
			{
				foreach (var ip in service.Addresses)
				{
					//ipaddr = service.Addresses[0].ToString();
					cumulus.LogMessage($"ZeroConfig Service: AirLink found '{service.Hostname}', reporting its IP address as: {ip}");

					if (!discovered.Hostname.Contains(service.Hostname))
					{
						cumulus.LogDebugMessage($"ZeroConfig Service: Adding AirLink {service.Hostname} to list of discovered devices");
						discovered.IP.Add(ip.ToString());
						discovered.Hostname.Add(service.Hostname);
					}
				}

				/*
				if (currIpAddr != ipaddr && service.Hostname == hostname)
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
				*/
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

		private void DoAqi(AirLinkData data)
		{
			switch (cumulus.airQualityIndex)
			{
				case 0: // US EPA
					data.aqiPm2p5 = AirQualityIndices.US_EPApm2p5(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.US_EPApm2p5(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.US_EPApm2p5(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.US_EPApm2p5(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.US_EPApm2p5(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.US_EPApm10(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.US_EPApm10(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.US_EPApm10(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.US_EPApm10(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.US_EPApm10(data.pm10_nowcast);
					break;
				case 1: // UK CMEAP
					data.aqiPm2p5 = AirQualityIndices.UK_COMEAPpm2p5(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.UK_COMEAPpm2p5(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.UK_COMEAPpm2p5(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.UK_COMEAPpm2p5(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.UK_COMEAPpm2p5(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.UK_COMEAPpm10(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.UK_COMEAPpm10(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.UK_COMEAPpm10(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.UK_COMEAPpm10(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.UK_COMEAPpm10(data.pm10_nowcast);
					break;
				case 2: // EU AQI
					data.aqiPm2p5 = AirQualityIndices.EU_AQIpm2p5h1(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.EU_AQIpm2p5h1(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.EU_AQIpm2p5h1(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.EU_AQI2p5h24(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.EU_AQI2p5h24(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.EU_AQI10h1(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.EU_AQI10h1(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.EU_AQI10h1(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.EU_AQI10h24(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.EU_AQI10h24(data.pm10_nowcast);
					break;
				case 3: // EU CAQI
					data.aqiPm2p5 = AirQualityIndices.EU_CAQI2p5h1(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.EU_CAQI2p5h1(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.EU_CAQI2p5h1(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.EU_CAQI2p5h24(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.EU_CAQI2p5h24(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.EU_CAQI10h1(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.EU_CAQI10h1(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.EU_CAQI10h1(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.EU_CAQI10h24(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.EU_CAQI10h24(data.pm10_nowcast);
					break;
				case 4: // Canada AQHI
					data.aqiPm2p5 = -1;
					data.aqiPm2p5_1hr = -1;
					data.aqiPm2p5_3hr = AirQualityIndices.CA_AQHI(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = -1;
					data.aqiPm2p5_nowcast = -1;

					data.aqiPm10 = -1;
					data.aqiPm10_1hr = -1;
					data.aqiPm10_3hr = -1;
					data.aqiPm10_24hr = -1;
					data.aqiPm10_nowcast = -1;
					break;
				case 5: // Australia NEPM
					data.aqiPm2p5 = AirQualityIndices.AU_NEpm2p5(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.AU_NEpm2p5(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.AU_NEpm2p5(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.AU_NEpm2p5(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.AU_NEpm2p5(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.AU_NEpm10(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.AU_NEpm10(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.AU_NEpm10(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.AU_NEpm10(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.AU_NEpm10(data.pm10_nowcast);
					break;
				case 6: // Netherlands LKI
					data.aqiPm2p5 = AirQualityIndices.NL_LKIpm2p5(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.NL_LKIpm2p5(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.NL_LKIpm2p5(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.NL_LKIpm2p5(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.NL_LKIpm2p5(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.NL_LKIpm10(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.NL_LKIpm10(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.NL_LKIpm10(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.NL_LKIpm10(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.NL_LKIpm10(data.pm10_nowcast);
					break;
				case 7: // Belgium BelAQI
					data.aqiPm2p5 = AirQualityIndices.BE_BelAQIpm2p5(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.BE_BelAQIpm2p5(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.BE_BelAQIpm2p5(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.BE_BelAQIpm2p5(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.BE_BelAQIpm2p5(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.BE_BelAQIpm10(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.BE_BelAQIpm10(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.BE_BelAQIpm10(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.BE_BelAQIpm10(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.BE_BelAQIpm10(data.pm10_nowcast);
					break;

				default:
					cumulus.LogMessage($"DoAqi: Invalid AQI formula value set [cumulus.airQualityIndex]");
					break;
			}

		}

		private int GetWlHistoricHealthLsid(int id, int type)
		{
			try
			{
				var sensor = sensorList.Where(i => i.LSID == id || i.ParentID == id).FirstOrDefault();
				if (sensor != null)
				{
					var health = sensorList.Where(i => i.ParentID == sensor.ParentID && i.SensorType == type).FirstOrDefault();
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

		private class AlCurrent
		{
			public AlCurrentData data { get; set; }
		}

		private class AlCurrentData
		{
			public string did { get; set; }
			public string name { get; set; }
			public int ts { get; set; }
			public List<AlCurrentRec> conditions { get; set; }
		}

		private class AlCurrentRec
		{
			// only added fields we may need
			public string lsid { get; set; }
			public int data_structure_type { get; set; }
			public double temp { get; set; }
			public double hum { get; set; }

			public double pm_1 { get; set; }
			public double pm_1_last { get; set; }

			public double pm_2p5 { get; set; }
			public double pm2p5_last { get; set; }
			public double pm_2p5_last_1_hour { get; set; }
			public double pm_2p5_last_3_hours { get; set; }
			public double pm_2p5_last_24_hours { get; set; }
			public double pm_2p5_nowcast { get; set; }


			public double pm_10 { get; set; }		// Type 6
			public double pm_10p0 { get; set; }	// Type 5
			public double pm_10_last { get; set; }
			public double pm_10_last_1_hour { get; set; }		// Type 6
			public double pm_10p0_last_1_hour { get; set; }	// Type 5
			public double pm_10_last_3_hours { get; set; }		// Type 6
			public double pm_10p0_last_3_hours { get; set; }   // Type 5
			public double pm_10_last_24_hours { get; set; }	// Type 6
			public double pm_10p0_last_24_hours { get; set; }  // Type 5
			public double pm_10_nowcast { get; set; }		// Type 6
			public double pm_10p0_nowcast { get; set; }	// Type 5

			public int pct_pm_data_last_1_hour { get; set; }
			public int pct_pm_data_last_3_hours { get; set; }
			public int pct_pm_data_last_24_hours { get; set; }
			public int pct_pm_data_nowcast { get; set; }
		}

		private class DiscoveredDevices
		{
			public List<string> IP { get; set; }
			public List<string> Hostname { get; set; }

			public DiscoveredDevices()
			{
				IP = new List<string>();
				Hostname = new List<string>();
			}
		}
	}

	public class AirLinkData
	{
		public double temperature { get; set; }
		public int humidity { get; set; }
		public double pm1 { get; set; }
		public double pm2p5 { get; set; }
		public double pm2p5_1hr { get; set; }
		public double pm2p5_3hr { get; set; }
		public double pm2p5_nowcast { get; set; }
		public double pm2p5_24hr { get; set; }
		public double pm10 { get; set; }
		public double pm10_1hr { get; set; }
		public double pm10_3hr { get; set; }
		public double pm10_nowcast { get; set; }
		public double pm10_24hr { get; set; }
		public int pct_1hr { get; set; }
		public int pct_3hr { get; set; }
		public int pct_nowcast { get; set; }
		public int pct_24hr { get; set; }
		public double aqiPm2p5 { get; set; }
		public double aqiPm2p5_1hr { get; set; }
		public double aqiPm2p5_3hr { get; set; }
		public double aqiPm2p5_24hr { get; set; }
		public double aqiPm2p5_nowcast { get; set; }
		public double aqiPm10 { get; set; }
		public double aqiPm10_1hr { get; set; }
		public double aqiPm10_3hr { get; set; }
		public double aqiPm10_24hr { get; set; }
		public double aqiPm10_nowcast { get; set; }
		public string firmwareVersion { get; set; }
		public int wifiRssi { get; set; }
	}
}
