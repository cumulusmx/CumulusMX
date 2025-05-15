using System;
using System.Globalization;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Timers;

using ServiceStack;



namespace CumulusMX
{
	internal class PurpleAir
	{
		private readonly Cumulus cumulus;
		private readonly WeatherStation station;

		private readonly System.Timers.Timer tmrCurrent;


		private bool updateInProgress;


		public PurpleAir(Cumulus cumulus, WeatherStation station)
		{
			this.cumulus = cumulus;
			this.station = station;

			cumulus.LogMessage($"Extra Sensor = PurpleAir");

			tmrCurrent = new System.Timers.Timer();
		}

		public void Start()
		{
			cumulus.LogMessage("PurpleAir Starting up");
			try
			{
				// Get the live values immediately to populate the web tags
				GetPaLiveData(null, null);

				// Create a current conditions thread to poll readings every 30 seconds
				tmrCurrent.Elapsed += GetPaLiveData;
				tmrCurrent.Interval = 30 * 1000;  // Every 30 seconds
				tmrCurrent.AutoReset = true;
				tmrCurrent.Start();
			}
			catch (ThreadAbortException)
			{
				// do nothing
			}
			cumulus.LogMessage("PurpleAir Started");
		}

		public void Stop()
		{
			try
			{
				cumulus.LogMessage("PurpleAir Stopping");
				tmrCurrent.Stop();
				cumulus.LogMessage("PurpleAir Stopped");
			}
			catch
			{
				// continue on error
			}
		}

		private async void GetPaLiveData(object source, ElapsedEventArgs e)
		{
			if (updateInProgress)
			{
				cumulus.LogDebugMessage("GetPaLiveData: Previous update is still running");
				return;
			}

			updateInProgress = true;

			for (var i = 0; i < 4; i++)
			{
				try
				{
					if (!string.IsNullOrEmpty(cumulus.PurpleAirIpAddress[i]))
					{
						var url = $"http://{cumulus.PurpleAirIpAddress[i]}/json";

						cumulus.LogDebugMessage("GetPaLiveData: Sending live data request - " + url);

						using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 5)))
						using (var response = await cumulus.MyHttpClient.GetAsync(url, cts.Token))
						{
							response.EnsureSuccessStatusCode();
							var responseBody = await response.Content.ReadAsStringAsync();
							cumulus.LogDataMessage($"GetPaLiveData: Response - {responseBody}");
							DecodePaLive(i + 1, responseBody, cumulus.PurpleAirAlgorithm[i], cumulus.PurpleAirThSensor[i]);
						}

						// Get the average from the database
						station.GetAqAvgFromDb(i + 1);

					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "GetPaLiveData: Exception");
				}
			}

			// Now update the database and calculate the averages
			station.UpdateAirQualityDb();

			for (var i = 0;i < 4; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.PurpleAirIpAddress[i]))
				{
					// Get the average from the database
					station.GetAqAvgFromDb(i + 1);
				}
			}

			updateInProgress = false;
		}

		private void DecodePaLive(int indx, string jsonText, int algo, int sensor)
		{
			/* Live
			{
				"SensorId": "84:f3:eb:28:d:ec",
				"DateTime": "2025/05/12T22:59:13z",
				"Geo": "PurpleAir-dec",
				"Mem": 15552,
				"memfrag": 33,
				"memfb": 10312,
				"memcs": 1008,
				"Id": 14188,
				"lat": -41.428600,
				"lon": 148.272095,
				"Adc": 0.02,
				"loggingrate": 15,
				"place": "outside",
				"version": "7.02",
				"uptime": 1651094,
				"rssi": -70,
				"period": 120,
				"httpsuccess": 14199,
				"httpsends": 14215,
				"hardwareversion": "2.0",
				"hardwarediscovered": "2.0+OPENLOG+30953 MB+DS3231+BME280+PMSX003-B+PMSX003-A",
				"current_temp_f": 70,
				"current_humidity": 100,
				"current_dewpoint_f": 70,
				"pressure": 1024.51,

				"p25aqic_b": "rgb(0,228,0)",
				"p25aqic": "rgb(4,228,0)",

				"pm2.5_aqi_b": 0,

				"pm2.5_aqi": 13,

				"pm1_0_cf_1_b": 0.00,
				"pm2_5_cf_1_b": 0.00,
				"pm10_0_cf_1_b": 0.00,

				"pm1_0_cf_1": 0.00,
				"pm2_5_cf_1": 3.00,
				"pm10_0_cf_1": 3.00,

				"p_0_3_um_b": 0.00,
				"p_0_5_um_b": 0.00,
				"p_1_0_um_b": 0.00,
				"p_2_5_um_b": 0.00,
				"p_5_0_um_b": 0.00,
				"p_10_0_um_b": 0.00,

				"p_0_3_um": 36.00,
				"p_0_5_um": 12.00,
				"p_1_0_um": 12.00,
				"p_2_5_um": 12.00,
				"p_5_0_um": 0.00,
				"p_10_0_um": 0.00,

				"pm1_0_atm_b": 0.00,
				"pm2_5_atm_b": 0.00,
				"pm10_0_atm_b": 0.00,

				"pm1_0_atm": 0.00,
				"pm2_5_atm": 3.00,
				"pm10_0_atm": 3.00,

				"pa_latency": 508,
				"wlstate": "Connected",
				"status_0": 2,
				"status_1": 2,
				"status_2": 2,
				"status_3": 2,
				"status_4": 0,
				"status_5": 0,
				"status_7": 0,
				"status_8": 0,
				"status_9": 0,
				"ssid": "TelstraE7F275"
			}
			*/

			/* Current 2-minute average
			 {
				  "SensorId": "5c:cf:7f:5c:a4:24",
				  "DateTime": "2025/05/15T11:21:02z",
				  "Geo": "PurpleAir-a424",
				  "Mem": 9664,
				  "memfrag": 24,
				  "memfb": 7328,
				  "memcs": 340,
				  "Id": 42206,
				  "lat": 54.634201,
				  "lon": -5.672,
				  "Adc": 0.02,
				  "loggingrate": 15,
				  "place": "outside",
				  "version": "7.04",
				  "uptime": 395621,
				  "rssi": -80,
				  "period": 120,
				  "httpsuccess": 6691,
				  "httpsends": 6702,
				  "hardwareversion": "2.0",
				  "hardwarediscovered": "2.0+OPENLOG+16567 MB+DS3231+BME280+BME68X+PMSX003-B+PMSX003-A",
				  "current_temp_f": 80,
				  "current_humidity": 26,
				  "current_dewpoint_f": 42,
				  "pressure": 1024.99,
				  "current_temp_f_680": 79,
				  "current_humidity_680": 32,
				  "current_dewpoint_f_680": 46,
				  "pressure_680": 1024.82,
				  "gas_680": 61.44,

				  "p25aqic_b": "rgb(8,229,0)",
				  "p25aqic": "rgb(12,229,0)",

				  "pm2.5_aqi_b": 16,

				  "pm2.5_aqi": 18,

				  "pm1_0_cf_1_b": 2.28,
				  "pm2_5_cf_1_b": 3.9,
				  "pm10_0_cf_1_b": 5.57,

				  "pm1_0_cf_1": 2.91,
				  "pm2_5_cf_1": 4.36,
				  "pm10_0_cf_1": 5.34,

				  "p_0_3_um_b": 651.05,
				  "p_0_5_um_b": 178.84,
				  "p_1_0_um_b": 29.22,
				  "p_2_5_um_b": 4.83,
				  "p_5_0_um_b": 2.34,
				  "p_10_0_um_b": 0.67,

				  "p_0_3_um": 675.93,
				  "p_0_5_um": 188.64,
				  "p_1_0_um": 31.09,
				  "p_2_5_um": 3.95,
				  "p_5_0_um": 1.05,
				  "p_10_0_um": 0.22,

				  "pm1_0_atm_b": 2.28,
				  "pm2_5_atm_b": 3.9,
				  "pm10_0_atm_b": 5.57,

				  "pm1_0_atm": 2.91,
				  "pm2_5_atm": 4.36,
				  "pm10_0_atm": 5.34,

				  "pa_latency": 317,
				  "response": 201,
				  "response_date": 1747308031,
				  "latency": 913,
				  "wlstate": "Connected",
				  "status_0": 2,
				  "status_1": 0,
				  "status_2": 2,
				  "status_3": 2,
				  "status_4": 2,
				  "status_5": 2,
				  "status_6": 2,
				  "status_7": 0,
				  "status_8": 2,
				  "status_9": 2,
				  "ssid": "xxxx"
				}
			 */
			try
			{
				// Convert JSON string to an object
				var json = jsonText.FromJson<PaLive>();

				// The PA sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the PA clock being correct, we will use our local time

				if (algo == 0)
				{
					// Algorithm 0 = indoor - use the averaged PM2.5 CF1 value
					cumulus.LogDebugMessage($"DecodePaLive: Sensor #{indx}, using CF_1 values, a={json.pm2_5_cf_1}, b={json.pm2_5_cf_1_b}");
					station.DoAirQuality(Math.Round((json.pm2_5_cf_1 + json.pm2_5_cf_1_b) / 2.0, 1), indx);
				}
				else
				{
					// Algorithm 1 = outdoor - use the averaged PM2.5 ATM value
					cumulus.LogDebugMessage($"DecodePaLive: Sensor #{indx}, using CF_1 values, a={json.pm2_5_atm}, b={json.pm2_5_atm_b}");
					station.DoAirQuality(Math.Round((json.pm2_5_atm + json.pm2_5_atm_b) / 2.0, 1), indx);
				}

				if (sensor > 0)
				{
					cumulus.LogDebugMessage($"DecodePaLive: Extra T/H  #{sensor}, using values, T={json.current_temp_f}, H={json.current_humidity}, DP={json.current_dewpoint_f}");
					station.DoExtraTemp(ConvertUnits.TempFToUser(json.current_temp_f), sensor);
					station.DoExtraHum(json.current_humidity, sensor);
					station.DoExtraDP(ConvertUnits.TempFToUser(json.current_dewpoint_f), sensor);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "DecodePaCurrent: Error processing returned data");
			}

		}


		/*
		private async void GetPaCurrentData(object source, ElapsedEventArgs e)
		{
			if (updateInProgress)
			{
				cumulus.LogDebugMessage("GetPaCurrentData: Previous update is still running");
				return;
			}

			updateInProgress = true;

			if (string.IsNullOrEmpty(cumulus.PurpleAirApiKey))
			{
				cumulus.LogErrorMessage("GetPaCurrentData: Missing PurpleAir API key in the configuration, aborting!");
				return;
			}

			var urlCurrent = $"https://api.purpleair.com/v1/sensors/";


			cumulus.LogDebugMessage($"GetPaCurrentData: Sending GET current conditions request to: {urlCurrent} ...");
			// Call asynchronous network methods in a try/catch block to handle exceptions
			try
			{
				string responseBody;

				var url = GetURL(urlCurrent);

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				request.Headers.Add("X-API-Key", cumulus.PurpleAirApiKey);


				using (var response = await cumulus.MyHttpClient.SendAsync(request))
				{
					response.EnsureSuccessStatusCode();
					responseBody = await response.Content.ReadAsStringAsync();
					cumulus.LogDataMessage($"GetPaCurrentData: Response - {responseBody}");
				}

				try
				{
					DecodePaCurrent(responseBody);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("GetPaCurrentData: Error processing the PurpleAir response");
					cumulus.LogErrorMessage("GetPaCurrentData: Error: " + ex.Message);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "GetPaCurrentData: Exception");
				Thread.Sleep(1000);
			}

			updateInProgress = false;
		}

		private void DecodePaCurrent(string currentJson)
		{
			try
			{
				// Convert JSON string to an object
				var json = currentJson.FromJson<PaCurrent>();

				// The PA sends the timestamp in Unix ticks, and in UTC
				// rather than rely on the PA clock being correct, we will use our local time

				station.DoAirQuality(json.sensor.pm_2p5, 1);
				station.DoAirQualityAvg(json.sensor.pm_2p5_24_hours, 1);

			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "DecodePaCurrent: Error processing returned data");
			}
		}

		private string GetURL(string baseUrl)
		{

			StringBuilder sb = new StringBuilder(baseUrl);
			var invC = new CultureInfo("");

			if (cumulus.PurpleAirSensorIndex >= 0)
			{
				sb.Append(cumulus.PurpleAirSensorIndex.ToString());
			}
			else if (!string.IsNullOrEmpty(cumulus.PurpleAirReadKey))
			{
				sb.Append(cumulus.PurpleAirReadKey);
			}
			else
			{
				cumulus.LogErrorMessage("GetURL: Missing PurpleAir Sensor Index or private Read Key, aborting!");
				return string.Empty;
			}

			sb.Append("?fields=");

			sb.Append("%2Crssi");
			sb.Append("%2Cuptime");
			sb.Append("%2Chumidity");
			sb.Append("%2Ctemperature");
			sb.Append("%2Cpm1.0");
			sb.Append("%2Cpm2.5");
			sb.Append("%2Cpm2.5_60minute");
			sb.Append("%2Cpm2.5_24hour");
			sb.Append("%2Cpm10.0");

			return sb.ToString();
		}


#pragma warning disable S3459, S1144 // Unassigned members should be removed

		private sealed class PaCurrent
		{
			public string api_version { get; set; }
			public int time_stamp { get; set; }
			public PaSensor sensor { get; set; }
		}

		[DataContract]
		private sealed class PaSensor
		{
			// only added fields we may need
			public int sensor_index { get; set; }
			public int rssi { get; set; }
			public int uptime { get; set; }
			public double temperature { get; set; }
			public int humidity { get; set; }

			[DataMember(Name = "pm1.0")]
			public double pm1 { get; set; }

			[DataMember(Name = "pm2.5")]
			public double pm_2p5 { get; set; }

			[DataMember(Name = "pm2.5_60minute")]
			public double pm_2p5_1_hour { get; set; }

			[DataMember(Name = "pm2.5_24hour")]
			public double pm_2p5_24_hours { get; set; }

			[DataMember(Name = "pm10.0")]
			public double pm_10 { get; set; }
		}
#pragma warning restore S3459, S1144 // Unassigned members should be removed
		*/

		private sealed class PaLive
		{
			public string SensorId { get; set; }
			public string DateTime { get; set; }
			public int Id { get; set; }
			public int uptime { get; set; }
			public int rssi { get; set; }
			public int current_temp_f { get; set; }
			public int current_humidity { get; set; }
			public int current_dewpoint_f { get; set; }
			public double pm1_0_cf_1 { get; set; }
			public double pm2_5_cf_1 { get; set; }
			public double pm10_0_cf_1 { get; set; }
			public double pm1_0_atm { get; set; }
			public double pm2_5_atm { get; set; }
			public double pm10_0_atm { get; set; }
			public double pm1_0_cf_1_b { get; set; }
			public double pm2_5_cf_1_b { get; set; }
			public double pm10_0_cf_1_b { get; set; }
			public double pm1_0_atm_b { get; set; }
			public double pm2_5_atm_b { get; set; }
			public double pm10_0_atm_b { get; set; }
		}
	}
}
