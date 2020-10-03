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
		private readonly System.Timers.Timer tmrCurrent;
		private System.Timers.Timer tmrHealth;
		private readonly object threadSafer = new object();
		private static readonly SemaphoreSlim WebReq = new SemaphoreSlim(1);
		private bool startupDayResetIfRequired = true;
		private int MaxArchiveRuns = 1;

		private static readonly HttpClientHandler HistoricHttpHandler = new HttpClientHandler();
		private readonly HttpClient WlHttpClient = new HttpClient(HistoricHttpHandler);
		private readonly HttpClient dogsBodyClient = new HttpClient();
		private readonly int weatherLinkArchiveInterval = 16 * 60; // Used to get historic Health, 16 minutes in seconds only for initial fetch after load
		//private bool alVoltageLow = false;
		private readonly List<WlSensor> sensorList = new List<WlSensor>();
		private readonly int HealthLsid;

		private readonly bool indoor;
		private readonly string locationStr;
		private readonly bool standalone;		// used to flag if this AirLink is linked to a WLL
		private readonly bool standaloneHistory; // Used to flag if we need to get history data on catch-up

		public DavisAirLink(Cumulus cumulus, bool Indoor) : base(cumulus)
		{
			indoor = Indoor;

			locationStr = indoor ? "Indoor" : "Outdoor";

			// Working out if we are standalone or integrated with WLL is a bit tricky.
			// Easist to see if we are a node of a WLL station, and the station id is same
			standalone = !(
							cumulus.StationType == StationTypes.WLL &&
							(indoor ? cumulus.AirLinkInIsNode : cumulus.AirLinkOutIsNode) &&
							(indoor ? cumulus.AirLinkInStationId == cumulus.WllStationId : cumulus.AirLinkOutStationId == cumulus.WllStationId)
						);

			// If we are standalone, are we configured to read history data?
			standaloneHistory = standalone &&
								!string.IsNullOrEmpty(cumulus.AirLinkApiKey) &&
								!string.IsNullOrEmpty(cumulus.AirLinkApiSecret) &&
								!(indoor ? string.IsNullOrEmpty(cumulus.AirLinkInStationId) : string.IsNullOrEmpty(cumulus.AirLinkOutStationId));

			cumulus.LogMessage($"Extra Sensor = Davis AirLink ({locationStr}) - standalone={standalone}");

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

			// Only start reading history if the main station isn't a WLL
			// and we have a station id
			if (standaloneHistory)
			{
				// Read the data from the WL APIv2
				//AlReadHistory();

				// Get the available sensor id - required to find the Health Sensor Id
				GetAvailableSensors();

				// Now find our corresponding Health sensor LSID
				HealthLsid = GetWlHistoricHealthLsid((indoor ? cumulus.airLinkInLsid : cumulus.airLinkOutLsid), 506);

				// Fetch the current health data to pre-polulate web tags
				GetWlHistoricHealth();
			}

			// If we are standalone then start-up the live reading process
			// Otherwise let the WLL station start us up after historic data download if applicable
			if (standalone)
			{
				Start();
			}
		}

		public override void Start()
		{
			cumulus.LogMessage($"AirLink {locationStr} Starting up");
			try
			{
				// Create a current conditions thread to poll readings every 30 seconds
				GetAlCurrent(null, null);
				tmrCurrent.Elapsed += GetAlCurrent;
				tmrCurrent.Interval = 30 * 1000;  // Every 30 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();


				// Only poll health data here if the AirLink is a standalone device - the standalone history flag shows we have all the required info to poll wl.com
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

		public override void Stop()
		{
			try
			{
				cumulus.LogMessage($"AirLink {locationStr} Stopping");
				tmrCurrent.Stop();
				if (tmrHealth != null) tmrHealth.Stop();
				cumulus.LogMessage($"AirLink {locationStr} Stopped");
			}
			catch { }
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

				cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Waiting for lock");
				WebReq.Wait();
				cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Has the lock");

				// The AL will error if already responding to a request from another device, so add a retry
				do
				{
					cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Sending GET current conditions request to AL: {urlCurrent} ...");
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
							/*
							if (responseBody.StartsWith("{\"data\":{\"did\":") || responseBody.StartsWith("{ \"data\": { \"did\":"))
							{
							*/
								DecodeAlCurrent(responseBody);
								if (startupDayResetIfRequired)
								{
									DoDayResetIfNeeded();
									startupDayResetIfRequired = false;
								}
							/*
							}
							else
							{
								cumulus.LogMessage("GetAlCurrent: Invalid current conditions message received");
								cumulus.LogDebugMessage("GetAlCurrent: Received: " + responseBody);
							}
							*/
							retry = 0;
						}
					}
					catch (Exception exp)
					{
						retry--;
						cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Exception: {exp.Message}");
						Thread.Sleep(2000);
					}
				} while (retry > 0);

				cumulus.LogDebugMessage($"GetAlCurrent: {locationStr} - Releasing lock");
				WebReq.Release();
			}
			else
			{
				cumulus.LogMessage($"GetAlCurrent: {locationStr} - Invalid IP address: {ip}");
			}
		}

		private void DecodeAlCurrent(string currentJson)
		{
			try
			{
				cumulus.LogDataMessage($"DecodeAlCurrent: {locationStr} - {currentJson}");

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
							cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Found AirLink data");

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
								cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - No valid temperature value found [{rec.Value<string>("temp")}]");
							}
							else
							{
								cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Using temp/hum data");

								if (string.IsNullOrEmpty(rec.Value<string>("hum")))
								{
									cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - No valid humidity value found [{rec.Value<string>("hum")}]");
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
									cumulus.airLinkDataIn.temperature = ConvertTempFToUser(rec.Value<double>("temp"));
								}
								else
								{
									cumulus.airLinkDataOut.temperature = ConvertTempFToUser(rec.Value<double>("temp"));
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
								cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - No valid pm1 value found [{rec.Value<string>("pm_1")}]");
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

									// now do the AQIs
									DoAqi(cumulus.airLinkDataIn);
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

									// now do the AQIs
									DoAqi(cumulus.airLinkDataOut);
								}
							}

							break;

						default:
							cumulus.LogDebugMessage($"DecodeAlCurrent: {locationStr} - Found an unknown tramsmitter type [{type}]!");
							break;
					}
					//UpdateStatusPanel(DateTime.Now);
					//UpdateMQTT();
				}
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
				} while (archiveRun < MaxArchiveRuns);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("AirLink: Exception occurred reading archive data: " + ex.Message);
			}
		}

		private void GetWlHistoricData()
		{
			JObject jObject;
			var stationId = indoor ? cumulus.AirLinkInStationId : cumulus.AirLinkOutStationId;

			cumulus.LogMessage("GetWlHistoricData: Get WL.com Historic Data");

			if (cumulus.AirLinkApiKey == String.Empty || cumulus.AirLinkApiSecret == String.Empty)
			{
				cumulus.LogMessage("GetWlHistoricData: Missing AirLink WeatherLink API data in the configuration, aborting!");
				cumulus.LastUpdateTime = DateTime.Now;
				return;
			}

			if (stationId == String.Empty || int.Parse(stationId) < 10)
			{
				var msg = "No AirLink WeatherLink API station ID in the configuration";
				cumulus.LogMessage(msg);
				cumulus.LogConsoleMessage("GetWlHistoricData: " + msg);
				return;
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
			cumulus.LogMessage($"GetWlHistoricData: Downloading Historic Data from WL.com from: {cumulus.LastUpdateTime.ToString("s")} to: {FromUnixTime(endTime).ToString("s")}");

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

			var logUrl = historicUrl.ToString().Replace(cumulus.AirLinkApiKey, "<<API_KEY>>");
			cumulus.LogDebugMessage($"GetWlHistoricData: WeatherLink URL = {logUrl}");

			//lastDataReadTime = cumulus.LastUpdateTime;

			JToken sensorData = new JObject();
			int noOfRecs = 0;

			try
			{
				// we want to do this synchronously, so .Result
				using (HttpResponseMessage response = WlHttpClient.GetAsync(historicUrl.ToString()).Result)
				{
					var responseBody = response.Content.ReadAsStringAsync().Result;
					cumulus.LogDebugMessage($"GetWlHistoricData: WeatherLink API Historic Response: {response.StatusCode}: {responseBody}");

					jObject = JObject.Parse(responseBody);

					if ((int)response.StatusCode != 200)
					{
						cumulus.LogMessage($"GetWlHistoricData: WeatherLink API Historic Error: {jObject.Value<string>("code")}, {jObject.Value<string>("message")}");
						cumulus.LogConsoleMessage($" - Error {jObject.Value<string>("code")}: {jObject.Value<string>("message")}");
						cumulus.LastUpdateTime = FromUnixTime(endTime);
						return;
					}

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
						sensorData = jObject.GetValue("sensors");

						foreach (JToken sensor in sensorData)
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
					DateTime timestamp = new DateTime();
					foreach (var sensor in sensorData)
					{
						var sensorType = sensor.Value<int>("sensor_type");

						if (sensorType == 323 && !indoor) // AirLink Outdoor
						{
							DecodeAlHistoric(sensor.Value<int>("data_structure_type"), sensor.Value<int>("sensor_type"), sensor["data"][dataIndex]);
						}
						else if (sensorType == 326 && indoor) // AirLink Indoor
						{
							DecodeAlHistoric(sensor.Value<int>("data_structure_type"), sensor.Value<int>("sensor_type"), sensor["data"][dataIndex]);
						}
					}

					var h = timestamp.Hour;

					cumulus.DoAirLinkLogFile(timestamp);

					if (!Program.service)
						Console.Write("\r - processed " + (((double)dataIndex + 1) / (double)noOfRecs).ToString("P0"));
					cumulus.LogMessage($"GetWlHistoricData: {(dataIndex + 1)} of {noOfRecs} archive entries processed");
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("GetWlHistoricData: Exception: " + ex.Message);
				}
			}

			if (!Program.service)
				Console.WriteLine(""); // flush the progress line
			return;
		}


		public void DecodeAlHistoric(int dataType, int sensorType, JToken data)
		{
			DateTime recordTs;
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

						recordTs = FromUnixTime(data.Value<long>("ts"));

						if (string.IsNullOrEmpty(data.Value<string>("temp_avg")) || data.Value<int>("temp_avg") == -99)
						{
							cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - No valid avg temperature value found [{data.Value<string>("temp_avg")}]");
						}
						else
						{
							cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - Using temp/hum data");

							if (string.IsNullOrEmpty(data.Value<string>("hum_last")))
							{
								cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - No valid humidity value found [{data.Value<string>("hum_last")}]");
							}
							else
							{
								if (indoor)
								{
									cumulus.airLinkDataIn.humidity = data.Value<int>("hum_last");
								}
								else
								{
									cumulus.airLinkDataOut.humidity = data.Value<int>("hum_last");
								}
							}

							if (indoor)
							{
								cumulus.airLinkDataIn.temperature = ConvertTempFToUser(data.Value<double>("temp_avg"));
							}
							else
							{
								cumulus.airLinkDataOut.temperature = ConvertTempFToUser(data.Value<double>("temp_avg"));
							}
						}

						if (string.IsNullOrEmpty(data.Value<string>("pm_1_avg")))
						{
							cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - No valid pm1 value found [{data.Value<string>("pm_1_avg")}]");
						}
						else
						{
							cumulus.LogDebugMessage($"DecodeAlHistoric: {locationStr} - Using PM data");
							if (indoor)
							{
								cumulus.airLinkDataIn.pm1 = data.Value<double>("pm_1_avg");
								cumulus.airLinkDataIn.pm2p5 = data.Value<double>("pm_2p5_avg");
								//cumulus.airLinkDataIn.pm2p5_1hr = data.Value<double>("pm_2p5_last_1_hour");
								//cumulus.airLinkDataIn.pm2p5_3hr = rec.Value<double>("pm_2p5_last_3_hours");
								//cumulus.airLinkDataIn.pm2p5_24hr = rec.Value<double>("pm_2p5_last_24_hours");
								//cumulus.airLinkDataIn.pm2p5_nowcast = rec.Value<double>("pm_2p5_nowcast");

								cumulus.airLinkDataIn.pm10 = data.Value<double>("pm_10_avg");
								//cumulus.airLinkDataIn.pm10_1hr = rec.Value<double>("pm_10_last_1_hour");
								//cumulus.airLinkDataIn.pm10_3hr = rec.Value<double>("pm_10_last_3_hours");
								//cumulus.airLinkDataIn.pm10_24hr = rec.Value<double>("pm_10_last_24_hours");
								//cumulus.airLinkDataIn.pm10_nowcast = rec.Value<double>("pm_10_nowcast");

								cumulus.airLinkDataIn.pct_1hr = data.Value<int>("pm_10_avg_num_part");
								//cumulus.airLinkDataIn.pct_3hr = rec.Value<int>("pct_pm_data_last_3_hours");
								//cumulus.airLinkDataIn.pct_24hr = rec.Value<int>("pct_pm_data_last_24_hours");
								//cumulus.airLinkDataIn.pct_nowcast = rec.Value<int>("pct_pm_data_nowcast");

								DoAqi(cumulus.airLinkDataIn);
							}
							else
							{
								cumulus.airLinkDataOut.pm1 = data.Value<double>("pm_1_avg");
								cumulus.airLinkDataOut.pm2p5 = data.Value<double>("pm_2p5");
								//cumulus.airLinkDataOut.pm2p5_1hr = rec.Value<double>("pm_2p5_last_1_hour");
								//cumulus.airLinkDataOut.pm2p5_3hr = rec.Value<double>("pm_2p5_last_3_hours");
								//cumulus.airLinkDataOut.pm2p5_24hr = rec.Value<double>("pm_2p5_last_24_hours");
								//cumulus.airLinkDataOut.pm2p5_nowcast = rec.Value<double>("pm_2p5_nowcast");

								cumulus.airLinkDataOut.pm10 = data.Value<double>("pm_10_avg");
								//cumulus.airLinkDataOut.pm10_1hr = rec.Value<double>("pm_10_last_1_hour");
								//cumulus.airLinkDataOut.pm10_3hr = rec.Value<double>("pm_10_last_3_hours");
								//cumulus.airLinkDataOut.pm10_24hr = rec.Value<double>("pm_10_last_24_hours");
								//cumulus.airLinkDataOut.pm10_nowcast = rec.Value<double>("pm_10_nowcast");

								cumulus.airLinkDataOut.pct_1hr = data.Value<int>("pm_10_avg_num_part");
								//cumulus.airLinkDataOut.pct_3hr = rec.Value<int>("pct_pm_data_last_3_hours");
								//cumulus.airLinkDataOut.pct_24hr = rec.Value<int>("pct_pm_data_last_24_hours");
								//cumulus.airLinkDataOut.pct_nowcast = rec.Value<int>("pct_pm_data_nowcast");

								DoAqi(cumulus.airLinkDataOut);
							}
						}
						break;

					default:
						// Unknown!
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
					var sensorType = sensor.Value<int>("sensor_type");
					var lsid = sensor.Value<int>("lsid");

					if (sensorType == 506 && lsid == HealthLsid) // AirLink Outdoor
					{
						DecodeWlApiHealth(sensor, true);
					}
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
			if (sensor["data"].Count() > 0)
			{
				data = sensor["data"].Last;
			}
			else
			{
				if (sensor.Value<int>("data_structure_type") == 18)
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
					"bootloader_version": 527991452
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

				cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Found health data for AirLink device");

				if (string.IsNullOrEmpty(data.Value<string>("air_quality_firmware_version")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - No valid firmware version [{data.Value<string>("air_quality_firmware_version")}]");
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
				if (startingup)
				{
					cumulus.LogMessage($"AirLinkHealth: {locationStr} - FW version = " + (indoor ? cumulus.airLinkDataIn.firmwareVersion : cumulus.airLinkDataOut.firmwareVersion));
				}
				else
				{
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - FW version = " + (indoor ? cumulus.airLinkDataIn.firmwareVersion : cumulus.airLinkDataOut.firmwareVersion));
				}


				if (string.IsNullOrEmpty(data.Value<string>("uptime")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - No valid uptime [{data.Value<string>("uptime")}]");
				}
				else
				{
					var upt = TimeSpan.FromSeconds(data.Value<double>("uptime"));
					var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Uptime = " + uptStr);
				}

				if (string.IsNullOrEmpty(data.Value<string>("link_uptime")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - No valid link uptime [{data.Value<string>("link_uptime")}]");
				}
				else
				{
					var upt = TimeSpan.FromSeconds(data.Value<double>("link_uptime"));
					var uptStr = string.Format("{0}d:{1:D2}h:{2:D2}m:{3:D2}s",
							(int)upt.TotalDays,
							upt.Hours,
							upt.Minutes,
							upt.Seconds);
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Link Uptime = " + uptStr);
				}

				if (string.IsNullOrEmpty(data.Value<string>("wifi_rssi")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - No valid WiFi RSSI [{data.Value<string>("wifi_rssi")}]");
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

						cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - WiFi RSSI = " + data.Value<string>("wifi_rssi") + "dB");
					}

				}

				if (string.IsNullOrEmpty(data.Value<string>("tx_packets")))
				{
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - No valid xmt count [{data.Value<string>("tx_packets")}]");
				}
				else
				{
					var txCnt = data.Value<int>("tx_packets");
					var rxCnt = data.Value<int>("rx_packets");
					var dropped = data.Value<int>("dropped_packets");
					var bad = data.Value<int>("packet_errors");
					var error = string.IsNullOrEmpty(data.Value<string>("network_error")) ? "none" : data.Value<string>("network_error");
					cumulus.LogDebugMessage($"AirLinkHealth: {locationStr} - Network:  Tx={txCnt}, Rx={rxCnt}, drop={dropped}, bad={bad}, error='{error}'");
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

		private void GetAvailableSensors()
		{
			JObject jObject;

			var unixDateTime = ToUnixTime(DateTime.Now);

			if (cumulus.WllApiKey == String.Empty || cumulus.WllApiSecret == String.Empty)
			{
				cumulus.LogMessage("GetAvailableSensors: WeatherLink API data is missing in the configuration, aborting!");
				return;
			}

			if (cumulus.WllStationId == String.Empty || int.Parse(cumulus.WllStationId) < 10)
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

			var apiSignature = CalculateApiSignature(cumulus.WllApiSecret, header);
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
				var response = WlHttpClient.GetAsync(stationsUrl.ToString()).Result;
				var responseBody = response.Content.ReadAsStringAsync().Result;
				cumulus.LogDebugMessage("GetAvailableSensors: WeatherLink API Response: " + response.StatusCode + ": " + responseBody);

				jObject = JObject.Parse(responseBody);

				if ((int)response.StatusCode != 200)
				{
					cumulus.LogMessage("GetAvailableSensors: WeatherLink API Error: " + jObject.Value<string>("code") + ", " + jObject.Value<string>("message"));
					return;
				}

				var sensors = jObject["sensors"];
				WlSensor wl_sensor;

				// Sensor types we are interested in...
				// 323 = Outdoor AirLink
				// 326 = Indoor AirLink
				// 504 = WLL Health
				// 506 = AirLink Health
				var types = new int[] { 45, 323, 326, 504, 506 };
				foreach (var sensor in sensors)
				{
					cumulus.LogDebugMessage($"GetAvailableSensors: Found WeatherLink Sensor type={sensor.Value<int>("sensor_type")}, lsid={sensor.Value<int>("lsid")}, station_id={sensor.Value<int>("station_id")}, name={sensor.Value<string>("product_name")}, parentId={sensor.Value<int>("parent_device_id")}, parent={sensor.Value<string>("parent_device_name")}");

					if (types.Contains(sensor.Value<int>("sensor_type")) || sensor.Value<string>("category") == "ISS")
					{
						wl_sensor = new WlSensor(sensor.Value<int>("sensor_type"), sensor.Value<int>("lsid"), sensor.Value<int>("parent_device_id"), sensor.Value<string>("product_name"), sensor.Value<string>("parent_device_name"));
						sensorList.Add(wl_sensor);
						if (wl_sensor.SensorType == 323 && sensor.Value<int>("station_id") == int.Parse(cumulus.AirLinkOutStationId))
						{
							cumulus.LogDebugMessage($"GetAvailableSensors: Setting AirLink Outdoor LSID to {wl_sensor.LSID}");
							cumulus.airLinkOutLsid = wl_sensor.LSID;
						}
						else if (wl_sensor.SensorType == 326 && sensor.Value<int>("station_id") == int.Parse(cumulus.AirLinkInStationId))
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

					data.aqiPm10 = AirQualityIndices.US_EPApm2p5(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.US_EPApm2p5(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.US_EPApm2p5(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.US_EPApm2p5(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.US_EPApm2p5(data.pm10_nowcast);
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
					data.aqiPm2p5_24hr = AirQualityIndices.euAqi2p5h24(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.euAqi2p5h24(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.euAqi10h1(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.euAqi10h1(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.euAqi10h1(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.euAqi10h24(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.euAqi10h24(data.pm10_nowcast);
					break;
				case 3: // EU CAQI
					data.aqiPm2p5 = AirQualityIndices.euCaqi2p5h1(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.euCaqi2p5h1(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.euCaqi2p5h1(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.euCaqi2p5h24(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.euCaqi2p5h24(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.euCaqi10h1(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.euCaqi10h1(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.euCaqi10h1(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.euCaqi10h24(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.euCaqi10h24(data.pm10_nowcast);
					break;
				case 4: // Canada AQHI
					data.aqiPm2p5 = -1;
					data.aqiPm2p5_1hr = -1;
					data.aqiPm2p5_3hr = AirQualityIndices.canadaAqhi(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = -1;
					data.aqiPm2p5_nowcast = -1;

					data.aqiPm10 = -1;
					data.aqiPm10_1hr = -1;
					data.aqiPm10_3hr = -1;
					data.aqiPm10_24hr = -1;
					data.aqiPm10_nowcast = -1;
					break;
				case 5: // Austrialia NEPM
					data.aqiPm2p5 = AirQualityIndices.australiaNepm2p5(data.pm2p5);
					data.aqiPm2p5_1hr = AirQualityIndices.australiaNepm2p5(data.pm2p5_1hr);
					data.aqiPm2p5_3hr = AirQualityIndices.australiaNepm2p5(data.pm2p5_3hr);
					data.aqiPm2p5_24hr = AirQualityIndices.australiaNepm2p5(data.pm2p5_24hr);
					data.aqiPm2p5_nowcast = AirQualityIndices.australiaNepm2p5(data.pm2p5_nowcast);

					data.aqiPm10 = AirQualityIndices.australiaNepm10(data.pm10);
					data.aqiPm10_1hr = AirQualityIndices.australiaNepm10(data.pm10_1hr);
					data.aqiPm10_3hr = AirQualityIndices.australiaNepm10(data.pm10_3hr);
					data.aqiPm10_24hr = AirQualityIndices.australiaNepm10(data.pm10_24hr);
					data.aqiPm10_nowcast = AirQualityIndices.australiaNepm10(data.pm10_nowcast);
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

		private class WlSensor
		{
			public WlSensor(int sensorType, int lsid, int parentId, string name, string parentName)
			{
				SensorType = sensorType;
				LSID = lsid;
				ParentID = parentId;
				Name = name;
				ParentName = parentName;
			}
			public int SensorType { get; set; }
			public int LSID { get; set; }
			public int ParentID { get; set; }
			public string Name { get; set; }
			public string ParentName { get; set; }
		}

	}
}
