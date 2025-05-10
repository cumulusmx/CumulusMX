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
						var url = $"http://{cumulus.PurpleAirIpAddress[i]}/json?live=true";

						cumulus.LogDebugMessage("GetPaLiveData: Sending live data request - " + url);

						using (var cts = new CancellationTokenSource(new TimeSpan(0, 0, 5)))
						using (var response = await cumulus.MyHttpClient.GetAsync(url, cts.Token))
						{
							response.EnsureSuccessStatusCode();
							var responseBody = await response.Content.ReadAsStringAsync();
							cumulus.LogDataMessage($"GetPaLiveData: Response - {responseBody}");
							DecodePaLive(i + 1, responseBody, cumulus.PurpleAirAlgorithm[i], cumulus.PurpleAirThSensor[i]);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "GetPaLiveData: Exception");
				}
			}

			updateInProgress = false;
		}

		private void DecodePaLive(int indx, string jsonText, int algo, int sensor)
		{
			/*
			{
					"SensorId": "5c:cf:7f:5c:a4:24",
					"DateTime": "2025/05/08T20:27:22z",
					"Geo": "PurpleAir-a424",
					"Mem": 9600,
					"memfrag": 30,
					"memfb": 6600,
					"memcs": 352,
					"Id": 47756,
					"lat": 54.634201,
					"lon": -5.672,
					"Adc": 0.02,
					"loggingrate": 15,
					"place": "outside",
					"version": "7.04",
					"uptime": 469314,
					"rssi": -70,
					"period": 120,
					"httpsuccess": 7900,
					"httpsends": 7951,
					"hardwareversion": "2.0",
					"hardwarediscovered": "2.0+OPENLOG+16567 MB+DS3231+BME280+BME68X+PMSX003-B+PMSX003-A",
					"current_temp_f": 64,
					"current_humidity": 39,
					"current_dewpoint_f": 38,
					"pressure": 1018.62,
					"current_temp_f_680": 64,
					"current_humidity_680": 46,
					"current_dewpoint_f_680": 43,
					"pressure_680": 1018.51,
					"gas_680": 79.36,
					"p25aqic_b": "rgb(199,249,0)",
					"pm2.5_aqi_b": 46,
					"pm1_0_cf_1_b": 6,
					"p_0_3_um_b": 1344,
					"pm2_5_cf_1_b": 11,
					"p_0_5_um_b": 361,
					"pm10_0_cf_1_b": 11,
					"p_1_0_um_b": 64,
					"pm1_0_atm_b": 6,
					"p_2_5_um_b": 5,
					"pm2_5_atm_b": 11,
					"p_5_0_um_b": 0,
					"pm10_0_atm_b": 11,
					"p_10_0_um_b": 0,
					"p25aqic": "rgb(73,236,0)",
					"pm2.5_aqi": 33,
					"pm1_0_cf_1": 6,
					"p_0_3_um": 1203,
					"pm2_5_cf_1": 8,
					"p_0_5_um": 337,
					"pm10_0_cf_1": 11,
					"p_1_0_um": 60,
					"pm1_0_atm": 6,
					"p_2_5_um": 7,
					"pm2_5_atm": 8,
					"p_5_0_um": 1,
					"pm10_0_atm": 11,
					"p_10_0_um": 0,
					"pa_latency": 648,
					"response": 201,
					"response_date": 1746736011,
					"latency": 898,
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
					"ssid": "xxxxxx"
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
					// Algorithm 0 - use the PM2.5 CF1 value
					station.DoAirQuality(Math.Min(json.pm2_5_cf_1, json.pm2_5_cf_1_b), indx);
				}
				else
				{
					// Algorithm 1 - use the PM2.5 ATM value
					station.DoAirQuality(Math.Min(json.pm2_5_cf_1, json.pm2_5_cf_1_b), indx);
				}

				// Get the average from the recent data database

				if (sensor > 0)
				{
					station.DoExtraTemp(ConvertUnits.TempFToUser(json.current_temp_f), indx);
					station.DoExtraHum(json.current_humidity, indx);
					station.DoExtraDP(ConvertUnits.TempFToUser(json.current_dewpoint_f), indx);
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
			public int pm1_0_cf_1 { get; set; }
			public int pm2_5_cf_1 { get; set; }
			public int pm10_0_cf_1 { get; set; }
			public int pm1_0_atm { get; set; }
			public int pm2_5_atm { get; set; }
			public int pm10_0_atm { get; set; }
			public int pm1_0_cf_1_b { get; set; }
			public int pm2_5_cf_1_b { get; set; }
			public int pm10_0_cf_1_b { get; set; }
			public int pm1_0_atm_b { get; set; }
			public int pm2_5_atm_b { get; set; }
			public int pm10_0_atm_b { get; set; }
		}
	}
}
