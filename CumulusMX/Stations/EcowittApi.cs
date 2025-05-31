using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ServiceStack;
using ServiceStack.Text;


namespace CumulusMX
{
	internal class EcowittApi
	{
		private readonly Cumulus cumulus;
		private readonly WeatherStation station;

		private const string historyUrl = "https://api.ecowitt.net/api/v3/device/history?";
		private const string currentUrl = "https://api.ecowitt.net/api/v3/device/real_time?";
		private const string stationUrl = "https://api.ecowitt.net/api/v3/device/list?";
		private const string firmwareUrl = "http://ota.ecowitt.net/api/ota/v1/version/info?";
		private const string simpleFirmwareUrl = "http://download.ecowitt.net/down/filewave?v=FirwaveReadme.txt";
		public static readonly string[] SimpleSupportedModels = ["GW1000", "WH2650", "WS1900", "HP10", "WH2680", "WH6006", "WL6006"];



		private static readonly int EcowittApiFudgeFactor = 5; // Number of minutes that Ecowitt API data is delayed

		private DateTime LastCurrentDataTime = DateTime.MinValue; // Stored in UTC to avoid DST issues
		private DateTime LastCameraImageTime = DateTime.MinValue; // Stored in UTC to avoid DST issues
		private DateTime LastCameraCallTime = DateTime.MinValue; // Stored in UTC to avoid DST issues

		private string LastCameraVideoTime = string.Empty;
		private readonly DateTime LastCameraVideoCallTime = DateTime.MinValue;

		private int delayTime = 10;

		private int PrimaryTHSensor;
		private int PrimaryIndoorTHSensor;
		private int[] MapWN34 = new int[9];

		public EcowittApi(Cumulus cuml, WeatherStation stn, bool mainStation)
		{
			cumulus = cuml;
			station = stn;

			// Let's decode the Unix ts to DateTime
			JsConfig.Init(new Config
			{
				DateHandler = DateHandler.UnixTime
			});

			// override the default deserializer which returns a UTC time to return a local time
			JsConfig<DateTime>.DeSerializeFn = datetimeStr =>
			{
				if (string.IsNullOrWhiteSpace(datetimeStr))
				{
					return DateTime.MinValue;
				}

				if (long.TryParse(datetimeStr, out var date))
				{
					return Utils.FromUnixTime(date);
				}

				return DateTime.MinValue;
			};

			// sensor mappings
			PrimaryTHSensor = cumulus.Gw1000PrimaryTHSensor;
			PrimaryIndoorTHSensor = cumulus.Gw1000PrimaryIndoorTHSensor;
			for (int i = 0; i < cumulus.EcowittMapWN34.Length; i++)
			{
				MapWN34[i] = cumulus.EcowittMapWN34[i];
			}
		}


		internal bool GetHistoricData(DateTime startTime, DateTime endTime, CancellationToken token)
		{
			// DOC: https://doc.ecowitt.net/web/#/apiv3en?page_id=19

			var data = new EcowittHistoricData();

			cumulus.LogMessage("API.GetHistoricData: Get Ecowitt Historic Data");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogWarningMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting process");
				cumulus.LastUpdateTime = DateTime.Now;
				return false;
			}

			var apiStartDate = startTime.AddMinutes(-EcowittApiFudgeFactor);
			var apiEndDate = endTime;

			var sb = new StringBuilder(historyUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			if (ulong.TryParse(cumulus.EcowittMacAddress, out _))
			{
				sb.Append($"&imei={cumulus.EcowittMacAddress}");
			}
			else
			{
				sb.Append($"&mac={cumulus.EcowittMacAddress.ToUpper()}");
			}
			sb.Append($"&start_date={apiStartDate:yyyy-MM-dd'%20'HH:mm:ss}");
			sb.Append($"&end_date={apiEndDate:yyyy-MM-dd'%20'HH:mm:ss}");

			// Request the data in the correct units
			sb.Append($"&temp_unitid={cumulus.Units.Temp + 1}"); // 1=C, 2=F
			sb.Append($"&pressure_unitid={(cumulus.Units.Press == 2 ? "4" : "3")}"); // 3=hPa, 4=inHg, 5=mmHg
			var windUnit = cumulus.Units.Wind switch
			{
				// m/s
				0 => "6",
				// mph
				1 => "9",
				// km/h
				2 => "7",
				// knots
				3 => "8",
				_ => "?",
			};
			sb.Append($"&wind_speed_unitid={windUnit}");
			sb.Append($"&rainfall_unitid={cumulus.Units.Rain + 12}"); // 13=inches, 14=mm

			// available callbacks:
			//	outdoor, indoor, solar_and_uvi, rainfall, wind, pressure, lightning
			//	indoor_co2, co2_aqi_combo, pm25_aqi_combo, pm10_aqi_combo, temp_and_humidity_aqi_combo
			//	pm25_ch[1-4]
			//	temp_and_humidity_ch[1-8]
			//	soil_ch[1-8]
			//	temp_ch[1-8]
			//	leaf_ch[1-8]
			//	batt
			var callbacks = new string[]
			{
				"indoor",
				"outdoor",
				"wind",
				"pressure",
				"rainfall",
				"rainfall_piezo",
				"solar_and_uvi",
				"temp_and_humidity_ch1",
				"temp_and_humidity_ch2",
				"temp_and_humidity_ch3",
				"temp_and_humidity_ch4",
				"temp_and_humidity_ch5",
				"temp_and_humidity_ch6",
				"temp_and_humidity_ch7",
				"temp_and_humidity_ch8",
				"soil_ch1",
				"soil_ch2",
				"soil_ch3",
				"soil_ch4",
				"soil_ch5",
				"soil_ch6",
				"soil_ch7",
				"soil_ch8",
				"temp_ch1",
				"temp_ch2",
				"temp_ch3",
				"temp_ch4",
				"temp_ch5",
				"temp_ch6",
				"temp_ch7",
				"temp_ch8",
				"leaf_ch1",
				"leaf_ch2",
				"leaf_ch3",
				"leaf_ch4",
				"leaf_ch5",
				"leaf_ch6",
				"leaf_ch7",
				"leaf_ch8",
				"pm25_ch1",
				"pm25_ch2",
				"pm25_ch3",
				"pm25_ch4",
				"indoor_co2",
				"co2_aqi_combo",
				"pm1_aqi_combo",
				"pm25_aqi_combo",
				"pm10_aqi_combo",
				"t_rh_aqi_combo",
				"ch_lds1",
				"ch_lds2",
				"ch_lds3",
				"ch_lds4"
			};

			sb.Append("&call_back=");
			foreach (var cb in callbacks)
				sb.Append(cb + ",");
			sb.Length--;

			//TODO: match time to logging interval
			// available times 5min, 30min, 4hour, 1day
			sb.Append($"&cycle_type=5min");

			var url = sb.ToString();

			var msg = $"Processing history data from {startTime:yyyy-MM-dd HH:mm} to {endTime.AddMinutes(5):yyyy-MM-dd HH:mm}...";
			cumulus.LogMessage($"API.GetHistoricData: " + msg);
			Cumulus.LogConsoleMessage(msg);

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-key>>").Replace(cumulus.EcowittUserApiKey, "<<User-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");

			try
			{
				string responseBody;
				int responseCode;
				int retries = 3;
				int retryTime = 10; // seconds
				bool success = false;

				do
				{
					// we want to do this synchronously, so .Result
					using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
					{
						responseBody = response.Content.ReadAsStringAsync(token).Result;
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"API.GetHistoricData: Ecowitt API Historic Response code: {responseCode}");
						cumulus.LogDataMessage($"API.GetHistoricData: Ecowitt API Historic Response: {responseBody}");
					}

					if (responseCode != 200)
					{
						var historyError = responseBody.FromJson<ErrorResp>();
						cumulus.LogMessage($"API.GetHistoricData: Ecowitt API Historic Error: {historyError.code}, {historyError.msg}, Cumulus.LogLevel.Warning");
						Cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.msg}", ConsoleColor.Red);
						cumulus.LastUpdateTime = endTime;
						return false;
					}


					if (responseBody == "{}")
					{
						cumulus.LogMessage("API.GetHistoricData: Ecowitt API Historic: No data was returned.");
						Cumulus.LogConsoleMessage(" - No historic data available");
						cumulus.LastUpdateTime = endTime;
						return false;
					}
					else if (responseBody.StartsWith("{\"code\":")) // sanity check
					{
						// Ecowitt send null values as the string "-", so we have to change all those to null before we parse...
						var json = responseBody.Replace("\"-\"", "null");

						// get the sensor data
						var histObj = json.FromJson<HistoricResp>();

						if (histObj != null)
						{
							// success
							if (histObj.code == 0)
							{
								try
								{
									if (histObj.data != null)
									{
										data = histObj.data;
										success = true;
									}
									else
									{
										// There was no data returned.
										cumulus.LastUpdateTime = endTime;
										return false;
									}
								}
								catch (Exception ex)
								{
									cumulus.LogErrorMessage("API.GetHistoricData: Error decoding the response - " + ex.Message);
									cumulus.LastUpdateTime = endTime;
									return false;
								}
							}
							else if (histObj.code == -1 || histObj.code == 45001)
							{
								// -1 = system busy, 45001 = rate limited

								// have we reached the retry limit?
								if (--retries <= 0)
								{
									cumulus.LastUpdateTime = endTime;
									return false;
								}

								cumulus.LogMessage($"API.GetHistoricData: System Busy or Rate Limited, waiting {retryTime} secs before retry...");
								Task.Delay(retryTime * 1000, token).Wait(token);
								retryTime *= 2; // double the retry time

							}
							else
							{
								return false;
							}
						}
						else
						{
							cumulus.LastUpdateTime = endTime;
							return false;
						}

					}
					else // No idea what we got, dump it to the log
					{
						cumulus.LogErrorMessage("API.GetHistoricData: Invalid historic message received");
						cumulus.LogDataMessage("API.GetHistoricData: Received: " + responseBody);
						cumulus.LastUpdateTime = endTime;
						return false;
					}
				} while (!success);

				if (!token.IsCancellationRequested)
				{
					ProcessHistoryData(data, token);
				}

				return true;
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetHistoricData: Error - Request timed out, no response");
				return false;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetHistoricData: Exception occurred");
				cumulus.LastUpdateTime = endTime;
				return false;
			}
		}

		private void ProcessHistoryData(EcowittHistoricData data, CancellationToken token)
		{
			// allocate a dictionary of data objects, keyed on the timestamp
			var buffer = new SortedDictionary<DateTime, HistoricData>();

			// process each sensor type, and store them for adding to the system later
			// Indoor Data
			if (data.indoor != null)
			{
				try
				{
					// do the temperature
					if (data.indoor.temperature != null && data.indoor.temperature.list != null)
					{
						foreach (var item in data.indoor.temperature.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							// not present value = 140
							if (!item.Value.HasValue || item.Value == 140 || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.IndoorTemp = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ IndoorTemp = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
					// do the humidity
					if (data.indoor.humidity != null && data.indoor.humidity.list != null)
					{
						foreach (var item in data.indoor.humidity.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.IndoorHum = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ IndoorHum = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing indoor data. Exception: " + ex.Message);
				}
			}
			// Outdoor Data
			if (data.outdoor != null)
			{
				try
				{
					// Temperature
					if (data.outdoor.temperature != null && data.outdoor.temperature.list != null)
					{
						foreach (var item in data.outdoor.temperature.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							// not present value = 140
							if (!item.Value.HasValue || item.Value == 140 || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.Temp = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ Temp = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// Humidity
					if (data.outdoor.humidity != null && data.outdoor.humidity.list != null)
					{
						foreach (var item in data.outdoor.humidity.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.Humidity = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ Humidity = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// Dewpoint
					if (data.outdoor.dew_point != null && data.outdoor.dew_point.list != null)
					{
						foreach (var item in data.outdoor.dew_point.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.DewPoint = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ DewPoint = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// Feels like
					if (data.outdoor.feels_like != null && data.outdoor.feels_like.list != null)
					{
						foreach (var item in data.outdoor.feels_like.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.FeelsLike = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ FeelsLike = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// Apparent
					if (data.outdoor.app_temp != null && data.outdoor.app_temp.list != null)
					{
						foreach (var item in data.outdoor.app_temp.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.Apparent = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ Apparent = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing outdoor data. Exception: " + ex.Message);
				}
			}
			// Wind Data
			if (data.wind != null)
			{
				try
				{
					// Speed
					if (data.wind.wind_speed != null && data.wind.wind_speed.list != null)
					{
						foreach (var item in data.wind.wind_speed.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.WindSpd = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ WindSpd = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// Gust
					if (data.wind.wind_gust != null && data.wind.wind_gust.list != null)
					{
						foreach (var item in data.wind.wind_gust.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.WindGust = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ WindGust = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// Direction
					if (data.wind.wind_direction != null && data.wind.wind_direction.list != null)
					{
						foreach (var item in data.wind.wind_direction.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.WindDir = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ WindDir = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing wind data. Exception: " + ex.Message);
				}
			}
			// Pressure Data
			if (data.pressure != null)
			{
				try
				{
					// relative
					if (data.pressure.relative != null && data.pressure.relative.list != null)
					{
						foreach (var item in data.pressure.relative.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								// We have to request kPa values in hPa, so divide by 10
								value.Pressure = cumulus.Units.Press == 3 ? item.Value / 10 : item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{
									// We have to request kPa values in hPa, so divide by 10
									Pressure = cumulus.Units.Press == 3 ? item.Value / 10 : item.Value
								};
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// absolute
					if (data.pressure.absolute != null && data.pressure.absolute.list != null)
					{
						foreach (var item in data.pressure.absolute.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								// We have to request kPa values in hPa, so divide by 10
								value.StationPressure = cumulus.Units.Press == 3 ? item.Value / 10 : item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{
									// We have to request kPa values in hPa, so divide by 10
									StationPressure = cumulus.Units.Press == 3 ? item.Value / 10 : item.Value
								};
								buffer.Add(itemDate, newItem);
							}
						}
					}

				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing pressure data. Exception: " + ex.Message);
				}
			}
			// Rainfall Data - Tipper
			if (cumulus.Gw1000PrimaryRainSensor == 0 && data.rainfall != null)
			{
				try
				{
					// rain rate
					if (data.rainfall.rain_rate != null && data.rainfall.rain_rate.list != null)
					{
						foreach (var item in data.rainfall.rain_rate.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.RainRate = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ RainRate = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// yearly rain
					if (data.rainfall.yearly != null && data.rainfall.yearly.list != null)
					{
						foreach (var item in data.rainfall.yearly.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.RainYear = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ RainYear = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing rainfall data. Exception: " + ex.Message);
				}
			}
			// Rainfall Data - Piezo
			if (cumulus.Gw1000PrimaryRainSensor == 1 && data.rainfall_piezo != null)
			{
				try
				{
					// rain rate
					if (data.rainfall_piezo.rain_rate != null && data.rainfall_piezo.rain_rate.list != null)
					{
						foreach (var item in data.rainfall_piezo.rain_rate.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.RainRate = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ RainRate = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// yearly rain
					if (data.rainfall_piezo.yearly != null && data.rainfall_piezo.yearly.list != null)
					{
						foreach (var item in data.rainfall_piezo.yearly.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.RainYear = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ RainYear = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing piezo rainfall data. Exception: " + ex.Message);
				}
			}
			// Solar Data
			if (data.solar_and_uvi != null)
			{
				try
				{
					// solar
					if (data.solar_and_uvi.solar != null && data.solar_and_uvi.solar.list != null)
					{
						foreach (var item in data.solar_and_uvi.solar.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.Solar = (int) item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ Solar = (int) item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// uvi
					if (data.solar_and_uvi.uvi != null && data.solar_and_uvi.uvi.list != null)
					{
						foreach (var item in data.solar_and_uvi.uvi.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.UVI = (int) item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ UVI = (int) item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing solar data. Exception: " + ex.Message);
				}
			}
			// Extra 8 channel sensors
			for (var i = 1; i <= 8; i++)
			{
				HistoricTempHum srcTH = null;
				HistoricDataSoil srcSoil = null;
				HistoricDataTemp srcTemp = null;
				HistoricDataLeaf srcLeaf = null;
				switch (i)
				{
					case 1:
						srcTH = data.temp_and_humidity_ch1;
						srcSoil = data.soil_ch1;
						srcTemp = data.temp_ch1;
						srcLeaf = data.leaf_ch1;
						break;
					case 2:
						srcTH = data.temp_and_humidity_ch2;
						srcSoil = data.soil_ch2;
						srcTemp = data.temp_ch2;
						srcLeaf = data.leaf_ch2;
						break;
					case 3:
						srcTH = data.temp_and_humidity_ch3;
						srcSoil = data.soil_ch3;
						srcTemp = data.temp_ch3;
						srcLeaf = data.leaf_ch3;
						break;
					case 4:
						srcTH = data.temp_and_humidity_ch4;
						srcSoil = data.soil_ch4;
						srcTemp = data.temp_ch4;
						srcLeaf = data.leaf_ch4;
						break;
					case 5:
						srcTH = data.temp_and_humidity_ch5;
						srcSoil = data.soil_ch5;
						srcTemp = data.temp_ch5;
						srcLeaf = data.leaf_ch5;
						break;
					case 6:
						srcTH = data.temp_and_humidity_ch6;
						srcSoil = data.soil_ch6;
						srcTemp = data.temp_ch6;
						srcLeaf = data.leaf_ch6;
						break;
					case 7:
						srcTH = data.temp_and_humidity_ch7;
						srcSoil = data.soil_ch7;
						srcTemp = data.temp_ch7;
						srcLeaf = data.leaf_ch7;
						break;
					case 8:
						srcTH = data.temp_and_humidity_ch8;
						srcSoil = data.soil_ch8;
						srcTemp = data.temp_ch8;
						srcLeaf = data.leaf_ch8;
						break;
				}

				// Extra Temp/Hum Data
				if (srcTH != null)
				{
					try
					{
						// temperature
						if (srcTH.temperature != null && srcTH.temperature.list != null)
						{
							foreach (var item in srcTH.temperature.list)
							{
								var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

								if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
									continue;

								if (buffer.TryGetValue(itemDate, out var value))
								{
									value.ExtraTemp[i] = item.Value;
								}
								else
								{
									var newItem = new HistoricData();
									newItem.ExtraTemp[i] = item.Value;
									buffer.Add(itemDate, newItem);
								}
							}
						}

						// humidity
						if (srcTH.humidity != null && srcTH.humidity.list != null)
						{
							foreach (var item in srcTH.humidity.list)
							{
								var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

								if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
									continue;

								if (buffer.TryGetValue(itemDate, out var value))
								{
									value.ExtraHumidity[i] = item.Value;
								}
								else
								{
									var newItem = new HistoricData();
									newItem.ExtraHumidity[i] = item.Value;
									buffer.Add(itemDate, newItem);
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"API.ProcessHistoryData: Error in pre-processing extra T/H data - chan[{i}]. Exception: {ex.Message}");
					}

				}
				// Extra Soil Moisture Data
				try
				{
					if (srcSoil != null && srcSoil.soilmoisture != null && srcSoil.soilmoisture.list != null)
					{
						// moisture
						foreach (var item in srcSoil.soilmoisture.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.SoilMoist[i] = item.Value;
							}
							else
							{
								var newItem = new HistoricData();
								newItem.SoilMoist[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"API.ProcessHistoryData: Error in pre-processing extra soil moisture data - chan[{i}]. Exception: {ex.Message}");
				}
				// User Temp Data
				try
				{
					if (srcTemp != null && srcTemp.temperature != null && srcTemp.temperature.list != null)
					{
						// temperature
						foreach (var item in srcTemp.temperature.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.UserTemp[i] = item.Value;
							}
							else
							{
								var newItem = new HistoricData();
								newItem.UserTemp[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"API.ProcessHistoryData: Error in pre-processing extra user temp data - chan[{i}]. Exception: {ex.Message}");
				}
				// Leaf Wetness Data
				try
				{
					if (srcLeaf != null && srcLeaf.leaf_wetness != null && srcLeaf.leaf_wetness.list != null)
					{
						// wetness
						foreach (var item in srcLeaf.leaf_wetness.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.LeafWetness[i] = item.Value;
							}
							else
							{
								var newItem = new HistoricData();
								newItem.LeafWetness[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"API.ProcessHistoryData: Error in pre-processing extra leaf wetness data - chan[{i}]. Exception:{ex.Message}");
				}
			}
			// Indoor CO2
			if (data.indoor_co2 != null)
			{
				try
				{
					// CO2
					if (data.indoor_co2.co2 != null && data.indoor_co2.co2.list != null)
					{
						foreach (var item in data.indoor_co2.co2.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.IndoorCo2 = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ IndoorCo2 = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// 24 Avg
					if (data.indoor_co2.average24h != null && data.indoor_co2.average24h.list != null)
					{
						foreach (var item in data.indoor_co2.average24h.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.IndoorCo2hr24 = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ IndoorCo2hr24 = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing indoor CO2 data. Exception: " + ex.Message);
				}
			}
			// CO2 Combi
			if (data.co2_aqi_combo != null)
			{
				try
				{
					// CO2
					if (data.co2_aqi_combo.co2 != null && data.co2_aqi_combo.co2.list != null)
					{
						foreach (var item in data.co2_aqi_combo.co2.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.AqiComboCO2 = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ AqiComboCO2 = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					// 24 Avg
					if (data.co2_aqi_combo.average24h != null && data.co2_aqi_combo.average24h.list != null)
					{
						foreach (var item in data.co2_aqi_combo.average24h.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.AqiComboCO2hr24 = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ AqiComboCO2hr24 = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing CO2 combo data. Exception: " + ex.Message);
				}
			}
			// pm2.5 Combi
			if (data.pm25_aqi_combo != null)
			{
				try
				{
					if (data.pm25_aqi_combo.pm25 != null && data.pm25_aqi_combo.pm25.list != null)
					{
						foreach (var item in data.pm25_aqi_combo.pm25.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.AqiComboPm25 = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ AqiComboPm25 = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing pm 2.5 combo data. Exception: " + ex.Message);
				}
			}
			// pm10 Combi
			if (data.pm10_aqi_combo != null)
			{
				try
				{
					if (data.pm10_aqi_combo.pm10 != null && data.pm10_aqi_combo.pm10.list != null)
					{
						foreach (var item in data.pm10_aqi_combo.pm10.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.AqiComboPm10 = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ AqiComboPm10 = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing pm 10 combo data. Exception: " + ex.Message);
				}
			}
			// temp/hum Combo
			if (data.t_rh_aqi_combo != null)
			{
				try
				{
					if (data.t_rh_aqi_combo.temperature != null && data.t_rh_aqi_combo.temperature.list != null)
					{
						foreach (var item in data.t_rh_aqi_combo.temperature.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.AqiComboTemp = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ AqiComboTemp = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

					if (data.t_rh_aqi_combo.humidity != null && data.t_rh_aqi_combo.humidity.list != null)
					{
						foreach (var item in data.t_rh_aqi_combo.humidity.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.AqiComboHum = item.Value;
							}
							else
							{
								var newItem = new HistoricData()
								{ AqiComboHum = item.Value };
								buffer.Add(itemDate, newItem);
							}
						}
					}

				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("API.ProcessHistoryData: Error in pre-processing temp/hum combo data. Exception: " + ex.Message);
				}
			}

			// 4 channel PM 2.5 sensors
			for (var i = 1; i <= 4; i++)
			{
				HistoricDataPm25Aqi sensor = null;
				switch (i)
				{
					case 1:
						sensor = data.pm25_ch1;
						break;
					case 2:
						sensor = data.pm25_ch2;
						break;
					case 3:
						sensor = data.pm25_ch3;
						break;
					case 4:
						sensor = data.pm25_ch4;
						break;
				}

				try
				{
					if (sensor != null && sensor.pm25 != null && sensor.pm25.list != null)
					{
						foreach (var item in sensor.pm25.list)
						{
							var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

							if (!item.Value.HasValue || itemDate <= cumulus.LastUpdateTime)
								continue;

							if (buffer.TryGetValue(itemDate, out var value))
							{
								value.pm25[i] = item.Value;
							}
							else
							{
								var newItem = new HistoricData();
								newItem.pm25[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"API.ProcessHistoryData: Error in pre-processing 4 chan pm 2.5 data - chan[{i}] . Exception: {ex.Message}");
				}
			}

			// 4 channel LDS-01 sensors
			try
			{
				// Laser AIR
				if (data.ch_lds1 != null)
				{
					foreach (var item in data.ch_lds1.air_ch1.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds1.air_ch1.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsAir[1] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsAir[1] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				if (data.ch_lds2 != null)
				{
					foreach (var item in data.ch_lds2.air_ch2.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds2.air_ch2.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsAir[2] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsAir[2] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				if (data.ch_lds3 != null)
				{
					foreach (var item in data.ch_lds3.air_ch3.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds3.air_ch3.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsAir[3] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsAir[3] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				if (data.ch_lds4 != null)
				{
					foreach (var item in data.ch_lds4.air_ch4.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds4.air_ch4.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsAir[4] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsAir[4] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				// Laser DEPTH
				if (data.ch_lds1 != null && data.ch_lds1.depth_ch1 != null)
				{
					foreach (var item in data.ch_lds1.depth_ch1.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds1.depth_ch1.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsDepth[1] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsDepth[1] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				if (data.ch_lds2 != null && data.ch_lds2.depth_ch2 != null)
				{
					foreach (var item in data.ch_lds2.depth_ch2.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds2.depth_ch2.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsDepth[2] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsDepth[2] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				if (data.ch_lds3 != null && data.ch_lds3.depth_ch3 != null)
				{
					foreach (var item in data.ch_lds3.depth_ch3.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds3.depth_ch3.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsDepth[3] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsDepth[3] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
				if (data.ch_lds4 != null && data.ch_lds4.depth_ch4 != null)
				{
					foreach (var item in data.ch_lds4.depth_ch4.list)
					{
						var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

						if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
							continue;

						decimal? dist = data.ch_lds4.depth_ch4.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(item.Value.Value),
							"cm" => ConvertUnits.LaserMmToUser(item.Value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(item.Value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(item.Value.Value / 12),
							_ => item.Value.Value
						};

						if (buffer.TryGetValue(itemDate, out var value))
						{
							value.LdsDepth[4] = dist;
						}
						else
						{
							var newItem = new HistoricData();
							newItem.LdsDepth[4] = dist;
							buffer.Add(itemDate, newItem);
						}
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"API.ProcessHistoryData: Error in pre-processing LDS-01 data. Exception: {ex.Message}");
			}



			// now we have all the data for this period, for each record create the string expected by ProcessData and get it processed
			var rollHour = Math.Abs(cumulus.GetHourInc());
			var luhour = cumulus.LastUpdateTime.Hour;
			var rolloverdone = luhour == rollHour;
			var midnightraindone = luhour == 0;
			var rollover9amdone = luhour == 9;
			bool snowhourdone = luhour == cumulus.SnowDepthHour;
			var lastRecDate = DateTime.MinValue;

			foreach (var rec in buffer)
			{
				if (token.IsCancellationRequested)
				{
					return;
				}

				cumulus.LogMessage("Processing data for " + rec.Key);

				if (lastRecDate == DateTime.MinValue)
				{
					rec.Value.Interval = 5;
					lastRecDate = rec.Key;
				}
				else
				{
					rec.Value.Interval = (rec.Key - lastRecDate).Minutes;
					lastRecDate = rec.Key;
				}

				var h = rec.Key.Hour;

				rollHour = Math.Abs(cumulus.GetHourInc(rec.Key));

				station.DataDateTime = rec.Key;

				//  if outside rollover hour, rollover yet to be done
				if (h != rollHour)
				{
					rolloverdone = false;
				}
				else if (!rolloverdone)
				{
					// In rollover hour and rollover not yet done
					// do rollover
					cumulus.LogMessage("Day rollover " + rec.Key.ToShortTimeString());
					station.DayReset(rec.Key);

					rolloverdone = true;
				}

				// Not in midnight hour, midnight rain yet to be done
				if (h != 0)
				{
					midnightraindone = false;
				}
				else if (!midnightraindone)
				{
					// In midnight hour and midnight rain (and sun) not yet done
					station.ResetMidnightRain(rec.Key);
					station.ResetSunshineHours(rec.Key);
					station.ResetMidnightTemperatures(rec.Key);
					midnightraindone = true;
				}

				// 9am rollover items
				if (h != 9)
				{
					rollover9amdone = false;
				}
				else if (!rollover9amdone)
				{
					station.Reset9amTemperatures(rec.Key);
					rollover9amdone = true;
				}

				// Not in snow hour, snow yet to be done
				if (h != 0)
				{
					snowhourdone = false;
				}
				else if ((h == cumulus.SnowDepthHour) && !snowhourdone)
				{
					// snowhour items
					if (cumulus.SnowAutomated > 0)
					{
						station.CreateNewSnowRecord(rec.Key);
					}

					// reset the accumulated snow depth(s)
					for (int i = 0; i < station.Snow24h.Length; i++)
					{
						station.Snow24h[i] = null;
					}

					snowhourdone = true;
				}

				// finally apply this data
				ApplyHistoricData(rec);

				// Do the CMX calculate SLP now as it depends on temperature
				if (cumulus.StationOptions.CalculateSLP)
				{
					var slp = MeteoLib.GetSeaLevelPressure(ConvertUnits.AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToMB(station.StationPressure), ConvertUnits.UserTempToC(station.OutdoorTemperature), cumulus.Latitude);

					station.DoPressure(ConvertUnits.PressMBToUser(slp), rec.Key);
				}

				// add in archive period worth of sunshine, if sunny
				if (station.CurrentSolarMax > 0 && station.SolarRad.HasValue &&
					station.SolarRad > station.CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100 &&
					station.SolarRad >= cumulus.SolarOptions.SolarMinimum &&
					!cumulus.SolarOptions.UseBlakeLarsen)
				{
					station.SunshineHours += rec.Value.Interval / 60.0;
					cumulus.LogDebugMessage($"Adding {rec.Value.Interval} minutes to Sunshine Hours");
				}

				// add in archive period minutes worth of temperature to the temp samples
				station.tempsamplestoday += 5;
				station.TempTotalToday += (station.OutdoorTemperature * 5);

				// add in 'following interval' minutes worth of wind speed to windrun
				cumulus.LogMessage("Windrun: " + station.WindAverage.ToString(cumulus.WindFormat) + cumulus.Units.WindText + " for " + rec.Value.Interval + " minutes = " +
								   (station.WindAverage * station.WindRunHourMult[cumulus.Units.Wind] * rec.Value.Interval / 60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);

				station.WindRunToday += station.WindAverage * station.WindRunHourMult[cumulus.Units.Wind] * rec.Value.Interval / 60.0;

				// update heating/cooling degree days
				station.UpdateDegreeDays(5);

				// update dominant wind bearing
				station.CalculateDominantWindBearing(station.Bearing, station.WindAverage, rec.Value.Interval);
				station.DoTrendValues(rec.Key);

				if (cumulus.StationOptions.CalculatedET && rec.Key.Minute == 0)
				{
					// Start of a new hour, and we want to calculate ET in Cumulus
					station.CalculateEvapotranspiration(rec.Key);
				}

				station.CheckForWindrunHighLow(rec.Key);

				_ = cumulus.DoLogFile(rec.Key, false);
				cumulus.DoCustomIntervalLogs(rec.Key);

				cumulus.MySqlRealtimeFile(999, false, rec.Key);

				if (cumulus.StationOptions.LogExtraSensors)
				{
					_ = cumulus.DoExtraLogFile(rec.Key);
				}

				// Custom MySQL update - minutes interval
				if (cumulus.MySqlSettings.CustomMins.Enabled)
				{
					_ = cumulus.CustomMysqlMinutesUpdate(rec.Key, false);
				}

				station.AddRecentDataWithAq(rec.Key, station.WindAverage, station.RecentMaxGust, station.WindLatest, station.Bearing, station.AvgBearing, station.OutdoorTemperature, station.WindChill, station.OutdoorDewpoint, station.HeatIndex,
					station.OutdoorHumidity, station.Pressure, station.RainToday, station.SolarRad, station.UV, station.RainCounter, station.FeelsLike, station.Humidex, station.ApparentTemperature, station.IndoorTemperature, station.IndoorHumidity, station.CurrentSolarMax, station.RainRate);

				station.UpdateStatusPanel(rec.Key);
				cumulus.AddToWebServiceLists(rec.Key);
				station.LastDataReadTime = rec.Key;
			}
		}

		public void ApplyHistoricData(KeyValuePair<DateTime, HistoricData> rec)
		{
			// === Wind ==
			// WindGust = max for period
			// WindSpd = avg for period
			// WindDir = avg for period
			try
			{
				if (rec.Value.WindGust.HasValue && rec.Value.WindSpd.HasValue && rec.Value.WindDir.HasValue)
				{
					var gustVal = (double) rec.Value.WindGust;
					var spdVal = (double) rec.Value.WindSpd;
					var dirVal = rec.Value.WindDir.Value;

					station.DoWind(gustVal, dirVal, spdVal, rec.Key);
				}
				else
				{
					cumulus.LogWarningMessage("ApplyHistoricData: Insufficient data process wind");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Wind data - " + ex.Message);
			}

			// === Humidity ===
			// = avg for period
			try
			{
				if (rec.Value.IndoorHum.HasValue)
				{
					// user has mapped indoor humidity to outdoor
					if (PrimaryTHSensor == 99)
					{
						station.DoOutdoorHumidity(rec.Value.IndoorHum.Value, rec.Key);
					}

					if (PrimaryIndoorTHSensor == 0)
					{
						station.DoIndoorHumidity(rec.Value.IndoorHum.Value);
					}
				}
				else
				{
					cumulus.LogWarningMessage("ApplyHistoricData: Missing indoor humidity data");
				}

				if (PrimaryTHSensor == 0)
				{
					if (rec.Value.Humidity.HasValue)
					{
						station.DoOutdoorHumidity(rec.Value.Humidity.Value, rec.Key);
					}
					else
					{
						cumulus.LogWarningMessage("ApplyHistoricData: Missing outdoor humidity data");
					}
				}

			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Humidity data - " + ex.Message);
			}

			// === Pressure ===
			// = avg for period
			try
			{
				if (rec.Value.Pressure.HasValue)
				{
					if (!cumulus.StationOptions.CalculateSLP)
					{
						var pressVal = (double) rec.Value.Pressure;
						station.DoPressure(pressVal, rec.Key);
					}
				}
				else
				{
					cumulus.LogWarningMessage("ApplyHistoricData: Missing relative pressure data");
				}

				if (rec.Value.StationPressure.HasValue)
				{
					station.DoStationPressure((double) rec.Value.StationPressure);
					// Leave CMX calculated SLP until the end as it uses Temperature
				}
				else
				{
					cumulus.LogWarningMessage("ApplyHistoricData: Missing absolute pressure data");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Pressure data - " + ex.Message);
			}

			// === Indoor temp ===
			// = avg for period
			try
			{
				if (rec.Value.IndoorTemp.HasValue)
				{
					var tempVal = (double) rec.Value.IndoorTemp;
					// user has mapped indoor temperature to outdoor
					if (PrimaryTHSensor == 99)
					{
						station.DoOutdoorTemp(tempVal, rec.Key);
					}

					if (PrimaryIndoorTHSensor == 0)
					{
						station.DoIndoorTemp(tempVal);
					}
				}
				else
				{
					cumulus.LogWarningMessage("ApplyHistoricData: Missing indoor temperature data");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Indoor temp data - " + ex.Message);
			}

			// === Outdoor temp ===
			// = avg for period
			try
			{
				if (PrimaryTHSensor == 0)
				{
					if (rec.Value.Temp.HasValue)
					{
						var tempVal = (double) rec.Value.Temp;
						station.DoOutdoorTemp(tempVal, rec.Key);
					}
					else
					{
						cumulus.LogWarningMessage("ApplyHistoricData: Missing outdoor temperature data");
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Outdoor temp data - " + ex.Message);
			}

			// === Rain ===
			try
			{
				double rRate = 0;
				if (rec.Value.RainRate.HasValue)
				{
					// we have a rain rate, so we will NOT calculate it
					station.calculaterainrate = false;
					rRate = (double) rec.Value.RainRate;
				}
				else
				{
					// No rain rate, so we will calculate it
					station.calculaterainrate = true;
				}

				if (rec.Value.RainYear.HasValue)
				{
					var rainVal = (double) rec.Value.RainYear;
					var rateVal = rRate;
					station.DoRain(rainVal, rateVal, rec.Key);
				}
				else
				{
					cumulus.LogWarningMessage("ApplyHistoricData: Missing rain data");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Rain data - " + ex.Message);
			}

			// === Solar ===
			// = max for period
			try
			{
				if (rec.Value.Solar.HasValue)
				{
					station.DoSolarRad((int)rec.Value.Solar.Value, rec.Key);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Solar data - " + ex.Message);
			}

			// === UVI ===
			// = max for period
			try
			{
				if (rec.Value.UVI.HasValue)
				{
					station.DoUV((double) rec.Value.UVI, rec.Key);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Solar data - " + ex.Message);
			}

			// === Extra Sensors ===
			for (var i = 1; i <= 8; i++)
			{
				// === Extra Temperature ===
				try
				{
					if (rec.Value.ExtraTemp[i].HasValue)
					{
						var tempVal = (double) rec.Value.ExtraTemp[i];
						station.DoExtraTemp(tempVal, i);

						if (i == PrimaryTHSensor)
						{
							station.DoOutdoorTemp(tempVal, rec.Key);
						}

						if (i == PrimaryIndoorTHSensor)
						{
							station.DoIndoorTemp(tempVal);
						}
					}
					else if (i == PrimaryTHSensor)
					{
						cumulus.LogErrorMessage($"ApplyHistoricData: Missing Extra temperature #{i} mapped to outdoor temperature data");
					}
					else if (i == PrimaryIndoorTHSensor)
					{
						cumulus.LogErrorMessage($"ApplyHistoricData: Missing Extra temperature #{i} mapped to indoor temperature data");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in extra temperature data - {ex.Message}");
				}
				// === Extra Humidity ===
				try
				{
					if (rec.Value.ExtraHumidity[i].HasValue)
					{
						station.DoExtraHum(rec.Value.ExtraHumidity[i].Value, i);

						if (i == PrimaryTHSensor)
						{
							station.DoOutdoorHumidity(rec.Value.ExtraHumidity[i].Value, rec.Key);
						}

						if (i == PrimaryIndoorTHSensor)
						{
							station.DoIndoorHumidity(rec.Value.ExtraHumidity[i].Value);
						}
					}
					else if (i == PrimaryTHSensor)
					{
						cumulus.LogErrorMessage($"ApplyHistoricData: Missing Extra humidity #{i} mapped to outdoor humidity data");
					}
					else if (i == PrimaryIndoorTHSensor)
					{
						cumulus.LogErrorMessage($"ApplyHistoricData: Missing Extra humidity #{i} mapped to indoor humidity data");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in extra humidity data - {ex.Message}");
				}
				// === Extra Dewpoint ===
				if (rec.Value.ExtraTemp[i].HasValue && rec.Value.ExtraHumidity[i].HasValue)
				{
					station.DoExtraDP(ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC((double) rec.Value.ExtraTemp[i].Value), rec.Value.ExtraHumidity[i].Value)), i);
				}


				// === User Temperature ===
				try
				{
					if (rec.Value.UserTemp[i].HasValue)
					{
						if (MapWN34[i] == 0)
						{
							station.DoUserTemp((double) rec.Value.UserTemp[i], i);
						}
						else
						{
							station.DoSoilTemp((double) rec.Value.UserTemp[i], MapWN34[i]);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in extra user temperature data - {ex.Message}");
				}


				// === Soil Moisture ===
				try
				{
					if (rec.Value.SoilMoist[i].HasValue)
					{
						station.DoSoilMoisture((double) rec.Value.SoilMoist[i], i);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in soil moisture data - {ex.Message}");
				}

				// === Leaf Wetness ===
				try
				{
					if (rec.Value.LeafWetness[i].HasValue)
					{
						station.DoLeafWetness((double) rec.Value.LeafWetness[i], i);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in leaf wetness data - {ex.Message}");
				}

			}

			// === Indoor CO2 ===
			try
			{
				station.CO2 = rec.Value.IndoorCo2;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in indoor CO2 data - " + ex.Message);
			}

			// === Indoor CO2 24hr avg ===
			try
			{
				station.CO2_24h = rec.Value.IndoorCo2hr24;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in indoor CO2 24hr avg data - " + ex.Message);
			}

			// === Combo CO2 ===
			try
			{
				if (rec.Value.AqiComboCO2.HasValue && !rec.Value.IndoorCo2.HasValue)
				{
					station.CO2 = rec.Value.AqiComboCO2;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in combo CO2 data - " + ex.Message);
			}

			// === Combo CO2 24hr avg ===
			try
			{
				if (rec.Value.AqiComboCO2hr24.HasValue && !rec.Value.IndoorCo2hr24.HasValue)
				{
					station.CO2_24h = rec.Value.AqiComboCO2hr24;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in combo CO2 24hr avg data - " + ex.Message);
			}

			// === PM 2.5 Combo ===
			try
			{
				station.CO2_pm2p5 = (double?) rec.Value.AqiComboPm25;
				station.CO2_pm2p5_aqi = station.CO2_pm2p5.HasValue ? station.GetAqi(WeatherStation.AqMeasure.pm2p5, station.CO2_pm2p5.Value) : null;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in AQI Combo pm2.5 data - " + ex.Message);
			}

			// === PM 2.5 Combo 24h ===
			try
			{
				station.CO2_pm2p5_24h = (double?) rec.Value.AqiComboPm25hr24;
				station.CO2_pm2p5_24h_aqi = station.CO2_pm2p5.HasValue ? station.GetAqi(WeatherStation.AqMeasure.pm2p5, station.CO2_pm2p5_24h.Value) : null;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in AQI Combo pm2.5 24h data - " + ex.Message);
			}

			// === PM 10 Combo ===
			try
			{
				station.CO2_pm10 = (double?) rec.Value.AqiComboPm10;
				station.CO2_pm10_aqi = station.CO2_pm10.HasValue ? station.GetAqi(WeatherStation.AqMeasure.pm10, station.CO2_pm10.Value) : null;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in AQI Combo pm10 data - " + ex.Message);
			}

			// === PM 10 Combo  24h ===
			try
			{
				station.CO2_pm10_24h = (double?) rec.Value.AqiComboPm10hr24;
				station.CO2_pm10_24h_aqi = station.CO2_pm10.HasValue ? station.GetAqi(WeatherStation.AqMeasure.pm10, station.CO2_pm10_24h.Value) : null;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in AQI Combo pm10 24h data - " + ex.Message);
			}

			// === temp Combo ===
			try
			{
				station.CO2_temperature = (double?) rec.Value.AqiComboTemp;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in AQI Combo temp data - " + ex.Message);
			}

			// === humidity Combo ===
			try
			{
				station.CO2_humidity = rec.Value.AqiComboHum;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in AQI Combo temp data - " + ex.Message);
			}

			// === 4 channel pm 2.5 ===
			for (var i = 1; i <= 4; i++)
			{
				try
				{
					if (rec.Value.pm25[i].HasValue)
					{
						station.DoAirQuality((double) rec.Value.pm25[i].Value, i);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in extra temperature data - {ex.Message}");
				}
			}

			// === Laser Distance ====
			for (var i = 1; i <= 4; i++)
			{
				try
				{
					station.DoLaserDistance(rec.Value.LdsAir[i], i);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in laser air data - {ex.Message}");
				}
			}

			// === Laser Depth ====
			for (var i = 1; i <= 4; i++)
			{
				try
				{
					station.DoLaserDepth(rec.Value.LdsDepth[i], i);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ApplyHistoricData: Error in laser depth data - {ex.Message}");
				}
			}

			// Do all the derived values after the primary data

			// === Dewpoint ===
			try
			{
				if (rec.Value.DewPoint.HasValue)
				{
					var val = (double) rec.Value.DewPoint;
					station.DoOutdoorDewpoint(val, rec.Key);
				}
				else if (rec.Value.Temp.HasValue && rec.Value.Humidity.HasValue)
				{
					station.DoOutdoorDewpoint(-999, rec.Key);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Dew point data - " + ex.Message);
			}

			// === Wind Chill ===
			try
			{
				if (rec.Value.Temp.HasValue && rec.Value.WindSpd.HasValue)
				{
					// historic API does not provide Wind Chill so force calculation
					station.DoWindChill(-999, rec.Key);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Wind chill data - " + ex.Message);
			}

			// === Humidex etc ===
			try
			{
				if (rec.Value.Temp.HasValue && rec.Value.Humidity.HasValue)
				{
					station.DoHumidex(rec.Key);
					station.DoCloudBaseHeatIndex(rec.Key);

					// === Apparent & Feels Like === - requires temp, hum, and windspeed
					if (rec.Value.WindSpd.HasValue)
					{
						station.DoApparentTemp(rec.Key);
						station.DoFeelsLike(rec.Key);
					}
					else
					{
						cumulus.LogWarningMessage("ApplyHistoricData: Insufficient data to calculate Humidex/Apparent/Feels Like temps");
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("ApplyHistoricData: Error in Humidex/Apparant/Feels Like - " + ex.Message);
			}
		}


		// returns the data structure and the number of seconds to wait before the next update
		internal CurrentDataData GetCurrentData(ref int delay, CancellationToken token)
		{
			// Doc: https://doc.ecowitt.net/web/#/apiv3en?page_id=17

			cumulus.LogMessage("API.GetCurrentData: Get Ecowitt Current Data");

			var sb = new StringBuilder(currentUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			if (ulong.TryParse(cumulus.EcowittMacAddress, out _))
			{
				sb.Append($"&imei={cumulus.EcowittMacAddress}");
			}
			else
			{
				sb.Append($"&mac={cumulus.EcowittMacAddress.ToUpper()}");
			}

			// Request the data in the correct units
			sb.Append($"&temp_unitid={cumulus.Units.Temp + 1}"); // 1=C, 2=F
			sb.Append($"&pressure_unitid={(cumulus.Units.Press == 2 ? "4" : "3")}"); // 3=hPa, 4=inHg, 5=mmHg
			var windUnit = cumulus.Units.Wind switch
			{
				// m/s
				0 => "6",
				// mph
				1 => "9",
				// km/h
				2 => "7",
				// knots
				3 => "8",
				_ => "?",
			};
			sb.Append($"&wind_speed_unitid={windUnit}");
			sb.Append($"&rainfall_unitid={cumulus.Units.Rain + 12}");

			// available callbacks:
			// all
			//	outdoor, indoor, solar_and_uvi, rainfall, wind, pressure, lightning
			//	indoor_co2, co2_aqi_combo, pm25_aqi_combo, pm10_aqi_combo, temp_and_humidity_aqi_combo
			//	pm25_ch[1-4]
			//	temp_and_humidity_ch[1-8]
			//	soil_ch[1-8]
			//	temp_ch[1-8]
			//	leaf_ch[1-8]
			//	batt

			sb.Append("&call_back=all");

			var url = sb.ToString();

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-key>>").Replace(cumulus.EcowittUserApiKey, "<<User-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");

			CurrentData currObj;

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetCurrentData: Ecowitt API Current Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetCurrentData: Ecowitt API Current Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var currentError = responseBody.FromJson<ErrorResp>();
					cumulus.LogWarningMessage($"API.GetCurrentData: Ecowitt API Current Error: {currentError.code}, {currentError.msg}");
					Cumulus.LogConsoleMessage($" - Error {currentError.code}: {currentError.msg}", ConsoleColor.Red);
					delay = 10;
					return null;
				}


				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("API.GetCurrentData: Ecowitt API Current: No data was returned.");
					Cumulus.LogConsoleMessage(" - No current data available");
					delay = 10;
					return null;
				}
				else if (responseBody.StartsWith("{\"code\":")) // sanity check
				{
					// Ecowitt send null values as the string "-", so we have to change all those to null before we parse...
					var json = responseBody.Replace("\"-\"", "null");

					// get the sensor data
					currObj = json.FromJson<CurrentData>();

					if (currObj != null)
					{
						// success
						if (currObj.code == 0)
						{
							if (currObj.data == null)
							{
								// There was no data returned.
								delay = delayTime;
								delayTime *= 2; // double the delay time
								return null;
							}
						}
						else if (currObj.code == -1 || currObj.code == 45001)
						{
							// -1 = system busy, 45001 = rate limited

							cumulus.LogMessage($"API.GetCurrentData: System Busy or Rate Limited, waiting {delayTime} secs before retry...");
							delay = delayTime;
							delayTime *= 2; // double the delay time
							return null;
						}
						else
						{
							delay = delayTime;
							delayTime *= 2; // double the delay time
							return null;
						}
					}
					else
					{
						delay = delayTime;
						delayTime *= 2; // double the delay time
						return null;
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("API.GetCurrentData: Invalid current message received");
					cumulus.LogDataMessage("API.GetCurrentData: Received: " + responseBody);
					delay = delayTime;
					delayTime *= 2; // double the delay time
					return null;
				}

				if (!token.IsCancellationRequested)
				{
					// pressure values should always be present, so use them for the data timestamp, if not try the outdoor temp
					DateTime dataTime;
					if (currObj.data.pressure != null)
					{
						dataTime = Utils.FromUnixTime(currObj.data.pressure.absolute.time);
					}
					else if (currObj.data.outdoor.temperature != null)
					{
						dataTime = Utils.FromUnixTime(currObj.data.outdoor.temperature.time);
					}
					else if (currObj.data.indoor.temperature != null)
					{
						dataTime = Utils.FromUnixTime(currObj.data.indoor.temperature.time);
					}
					else
					{
						dataTime = Utils.FromUnixTime(currObj.time);
					}


					cumulus.LogDebugMessage($"EcowittCloud: Last data update {dataTime:s}");

					if (dataTime.ToUniversalTime() != LastCurrentDataTime)
					{
						delayTime = 10; // reset the delay time to 10 seconds

						LastCurrentDataTime = dataTime.ToUniversalTime();

						// how many seconds to the next update?
						// wait for 15 seonds after the next update
						// Use the data update rate defined by the user
						var expectedUpdate = cumulus.EcowittCloudDataUpdateInterval * 60;

						var lastUpdateSecs = (int) (DateTime.UtcNow - LastCurrentDataTime).TotalSeconds;
						if (lastUpdateSecs > expectedUpdate + 30)
						{
							// hmm the data is already out of date, query again after a short delay
							delay = 30;
							return null;
						}
						else if (lastUpdateSecs < 15)
						{
							// we're OK, just update again in an "Ecowitt" minute of the interval
							delay = expectedUpdate + 5;
							return currObj.data;
						}
						else
						{
							// lets try and shift the time to be closer to 15 seconds after the next expected update
							delay = expectedUpdate + 15 - lastUpdateSecs;
							return currObj.data;
						}
					}
					else
					{
						delay = delayTime;
						delayTime *= 2; // double the delay time
						return null;
					}
				}

				delay = delayTime;
				delayTime *= 2; // double the delay time
				return null;
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetCurrentData: Error - Request timed out, no response");
				delay = delayTime;
				delayTime *= 2; // double the delay time
				return null;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetCurrentData: Exception occurred");
				delay = delayTime;
				delay *= 2; // double the delay time
				return null;
			}
		}


		internal string GetCurrentCameraImageUrl(string defaultUrl, CancellationToken token)
		{
			// Doc: https://doc.ecowitt.net/web/#/apiv3en?page_id=17

			cumulus.LogMessage("API.GetCurrentCameraImageUrl: Get Ecowitt Current Camera Data");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
			{
				cumulus.LogWarningMessage("API.GetCurrentCameraImageUrl: Missing Ecowitt API data in the configuration, aborting process");
				return defaultUrl;
			}


			// rate limit to one call per minute
			if (LastCameraCallTime.AddMinutes(1) > DateTime.UtcNow)
			{
				cumulus.LogMessage("API.GetCurrentCameraImageUrl: Last call was less than 1 minute ago, using last image URL");
				return defaultUrl;
			}

			LastCameraCallTime = DateTime.UtcNow;

			if (LastCameraImageTime.AddMinutes(5) > DateTime.UtcNow)
			{
				cumulus.LogMessage("API.GetCurrentCameraImageUrl: Last image was less than 5 minutes ago, using last image URL");
				return defaultUrl;
			}

			var sb = new StringBuilder(currentUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			sb.Append($"&mac={cumulus.EcowittCameraMacAddress}");
			sb.Append("&call_back=camera");

			var url = sb.ToString();

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-key>>").Replace(cumulus.EcowittUserApiKey, "<<User-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");

			CurrentData currObj;

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetCurrentCameraImageUrl: Ecowitt API Current Camera Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetCurrentCameraImageUrl: Ecowitt API Current Camera Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var currentError = responseBody.FromJson<ErrorResp>();
					cumulus.LogWarningMessage($"API.GetCurrentCameraImageUrl: Ecowitt API Current Camera Error: {currentError.code}, {currentError.msg}");
					Cumulus.LogConsoleMessage($" - Error {currentError.code}: {currentError.msg}", ConsoleColor.Red);
					return defaultUrl;
				}


				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("API.GetCurrentCameraImageUrl: Ecowitt API Current Camera Data: No data was returned.");
					Cumulus.LogConsoleMessage(" - No current data available");
					return defaultUrl;
				}
				else if (responseBody.StartsWith("{\"code\":")) // sanity check
				{
					// get the sensor data
					currObj = responseBody.FromJson<CurrentData>();

					if (currObj != null)
					{
						// success
						if (currObj.code == 0)
						{
							if (currObj.data == null)
							{
								// There was no data returned.
								cumulus.LogWarningMessage("API.GetCurrentCameraImageUrl: Ecowitt API Current Camera Data: No camera data was returned.");
								return defaultUrl;
							}

							if (currObj.data.camera == null)
							{
								cumulus.LogWarningMessage("API.GetCurrentCameraImageUrl: Ecowitt API Current Camera Data: No camera data was returned.");
								return defaultUrl;
							}

							LastCameraImageTime = Utils.FromUnixTime(currObj.data.camera.photo.time).ToUniversalTime();
							cumulus.LogDebugMessage($"API.GetCurrentCameraImageUrl: Last image update {LastCameraImageTime.ToLocalTime():s}");
							return currObj.data.camera.photo.url;
						}
						else if (currObj.code == -1 || currObj.code == 45001)
						{
							// -1 = system busy, 45001 = rate limited

							cumulus.LogMessage("API.GetCurrentCameraImageUrl: System Busy or Rate Limited, waiting 5 secs before retry...");
							return defaultUrl;
						}
						else
						{
							return defaultUrl;
						}
					}
					else
					{
						return defaultUrl;
					}

				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("API.GetCurrentCameraImageUrl: Invalid current message received");
					cumulus.LogDataMessage("API.GetCurrentCameraImageUrl: Received: " + responseBody);
					return defaultUrl;
				}
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetCurrentCameraImageUrl: Error - Request timed out, no response");
				return defaultUrl;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetCurrentCameraImageUrl: Exception occurred");
				return defaultUrl;
			}
		}

		internal string GetLastCameraVideoUrl(string defaultUrl, CancellationToken token)
		{
			// Doc: https://doc.ecowitt.net/web/#/apiv3en?page_id=19


#pragma warning disable S125 // Sections of code should not be commented out
			/*
							{
								"code": 0,
								"msg": "success",
								"time": "1701950253",
								"data": {
									"camera": {
										"20231206": {
											"video": "https://osswww.ecowitt.net/videos/webvideo/v0/2023_12_06/158185/29f0493644eb87ef7a0ffea30221605c.mp4"
										}
									}
								}
							}
						*/
#pragma warning restore S125 // Sections of code should not be commented out
			cumulus.LogMessage("API.GetLastCameraVideoUrl: Get Ecowitt Last Camera Video");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
			{
				cumulus.LogWarningMessage("API.GetLastCameraVideoUrl: Missing Ecowitt API data in the configuration, aborting process");
				return defaultUrl;
			}

			// do we already have the latest video
			if (LastCameraVideoTime == DateTime.Now.Date.AddDays(-1).ToString("yyyyMMdd"))
			{
				cumulus.LogMessage("API.GetLastCameraVideoUrl: The video we have is still current");
				return defaultUrl;
			}

			// rate limit to one call per minute
			if (LastCameraVideoCallTime.AddMinutes(1) > DateTime.Now)
			{
				cumulus.LogMessage("API.GetCurrentCameraImageUrl: Last call was less than 1 minute ago, using last video URL");
				return defaultUrl;
			}


			var sb = new StringBuilder(historyUrl);
			var end = DateTime.Now.Date;
			var start = end.AddDays(-1);
			end = end.AddMinutes(-1);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			sb.Append($"&mac={cumulus.EcowittCameraMacAddress}");
			sb.Append($"&start_date={start:yyyy-MM-dd'%20'HH:mm:ss}");
			sb.Append($"&end_date={end:yyyy-MM-dd'%20'HH:mm:ss}");
			sb.Append("&call_back=camera.video");

			var url = sb.ToString();

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-key>>").Replace(cumulus.EcowittUserApiKey, "<<User-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");


			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetLastCameraVideoUrl: Ecowitt API Current Camera Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetLastCameraVideoUrl: Ecowitt API Current Camera Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var currentError = responseBody.FromJson<ErrorResp>();
					cumulus.LogWarningMessage($"API.GetLastCameraVideoUrl: Ecowitt API Current Camera Error: {currentError.code}, {currentError.msg}");
					Cumulus.LogConsoleMessage($" - Error {currentError.code}: {currentError.msg}", ConsoleColor.Red);
					return defaultUrl;
				}


				dynamic vidObj = null;

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("API.GetLastCameraVideoUrl: Ecowitt API Current Camera Data: No data was returned.");
					Cumulus.LogConsoleMessage(" - No current data available");
					return defaultUrl;
				}
				else if (responseBody.StartsWith("{\"code\":")) // sanity check
				{
					// get the sensor data
					vidObj = DynamicJson.Deserialize(responseBody);

					if (vidObj != null)
					{
						// success
						if (vidObj.code == "0")
						{
							if (vidObj.data == null)
							{
								// There was no data returned.
								cumulus.LogWarningMessage("API.GetLastCameraVideoUrl: Ecowitt API Current Camera Data: No camera data was returned.");
								return defaultUrl;
							}

							if (vidObj.data.camera == null)
							{
								cumulus.LogWarningMessage("API.GetLastCameraVideoUrl: Ecowitt API Current Camera Data: No camera data was returned.");
								return defaultUrl;
							}

							var found = System.Text.RegularExpressions.Regex.Match(vidObj.data.camera.ToString(), "https.*mp4");

							if (found.Success)
							{
								var link = found.Groups[0].Value.Replace("\\", "");

								LastCameraVideoTime = start.ToString("yyyyMMdd");

								cumulus.LogDebugMessage($"API.GetLastCameraVideoUrl: Last image update {LastCameraVideoTime:s}, link = {link}");
								return link;
							}
							else
							{
								cumulus.LogWarningMessage("API.GetLastCameraVideoUrl: Failed to find URL");
								return defaultUrl;
							}
						}
						else if (vidObj.code == "-1" || vidObj.code == "45001")
						{
							// -1 = system busy, 45001 = rate limited

							cumulus.LogMessage("API.GetLastCameraVideoUrl: System Busy or Rate Limited, waiting 5 secs before retry...");
							return defaultUrl;
						}
						else
						{
							cumulus.LogMessage($"API.GetLastCameraVideoUrl: Unknown error: {vidObj.code} - {vidObj.msg}");
							return defaultUrl;
						}
					}
					else
					{
						return defaultUrl;
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("API.GetLastCameraVideoUrl: Invalid message received");
					cumulus.LogDataMessage("API.GetLastCameraVideoUrl: Received: " + responseBody);
					return defaultUrl;
				}
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetLastCameraVideoUrl: Error - Request timed out, no response");
				return defaultUrl;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetLastCameraVideoUrl: Exception occurred ");
				return defaultUrl;
			}
		}

		internal string[] GetStationList(bool CheckCamera, string macAddress, CancellationToken token)
		{
			cumulus.LogMessage("API.GetStationList: Get Ecowitt Station List - mac=" + macAddress);

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey))
			{
				cumulus.LogWarningMessage("API.GetStationList: Missing Ecowitt API data in the configuration, cannot get the station list");
				return [];
			}

			var sb = new StringBuilder(stationUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");

			var url = sb.ToString();

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-key>>").Replace(cumulus.EcowittUserApiKey, "<<User-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");

			StationList stnObj;

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetStationList: Ecowitt API Station List Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetStationList: Ecowitt API Station List Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var currentError = responseBody.FromJson<ErrorResp>();
					cumulus.LogWarningMessage($"API.GetStationList: Ecowitt API Station List Error: {currentError.code}, {currentError.msg}");
					Cumulus.LogConsoleMessage($" - Error {currentError.code}: {currentError.msg}", ConsoleColor.Red);
					return [];
				}

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("API.GetStationList: Ecowitt API Station List: No data was returned.");
					Cumulus.LogConsoleMessage(" - No current data available");
					return [];
				}
				else if (responseBody.StartsWith("{\"code\":")) // sanity check
				{
					// get the sensor data
					stnObj = responseBody.FromJson<StationList>();

					if (stnObj != null)
					{
						// success
						if (stnObj.code == 0)
						{
							if (stnObj.data == null)
							{
								// There was no data returned.
								return [];
							}
						}
						else if (stnObj.code == -1 || stnObj.code == 45001)
						{
							// -1 = system busy, 45001 = rate limited

							cumulus.LogMessage("API.GetStationList: System Busy or Rate Limited, waiting 5 secs before retry...");
							return [];
						}
						else
						{
							return [];
						}
					}
					else
					{
						return [];
					}

				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("API.GetStationList: Invalid message received");
					cumulus.LogDataMessage("API.GetStationList: Received: " + responseBody);
					return [];
				}

				if (!token.IsCancellationRequested)
				{
					if (stnObj.data.list == null)
					{
						cumulus.LogWarningMessage("API.GetStationList: Ecowitt API: No station data was returned.");
						return [];
					}

					var vers = string.Empty;
					var model = string.Empty;

					foreach (var stn in stnObj.data.list)
					{
						cumulus.LogDebugMessage($"API.GetStationList: Station: id={stn.id}, mac/imei={stn.mac ?? stn.imei}, name={stn.name}, type={stn.stationtype}");
						if (stn.type == 2 && CheckCamera)
						{
							// we have a camera
							cumulus.EcowittCameraMacAddress = stn.mac;
							cumulus.LogDebugMessage($"API.GetStationList: Found Camera name={stn.name ?? "-"}, vers={stn.stationtype ?? "-"}, mac={cumulus.EcowittCameraMacAddress}");
						}
						else if (stn.type == 1 && stn.mac.Equals(macAddress, StringComparison.CurrentCultureIgnoreCase))
						{
							// weather station - check the version
							vers = stn.stationtype.Split('V')[^1];
							model = stn.stationtype.Replace("_", string.Empty).Split('V')[0];
							cumulus.LogDebugMessage($"API.GetStationList: Found Station model={model}, vers={vers}");
						}
						else
						{
							// no idea what we got!
							cumulus.LogDebugMessage($"API.GetStationList: Found type={stn.type}, name={stn.name?? "-"}, model={stn.stationtype ?? "-"}");
						}
					}

					if (vers != string.Empty && model != string.Empty)
					{
						return [vers, model];
					}
				}

				return [];
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetStationList: Error - Request timed out, no response");
				return [];
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetStationList: Exception occurred");
				return [];
			}
		}

		internal async Task<string> GetLatestFirmwareVersion(string model, string mac, string version, CancellationToken token)
		{
			// Credit: https://www.wxforum.net/index.php?topic=46414.msg469692#msg469692

			if (version == null)
			{
				cumulus.LogMessage("API.GetLatestFirmwareVersion: No version supplied, cannot continue");
				return null;
			}

			await Task.Delay(Program.RandGenerator.Next(0, 5000), token);

			cumulus.LogMessage("API.GetLatestFirmwareVersion: Get Ecowitt Latest Firmware Version");

			mac ??= GetRandomMacAddress();
			var url = $"id={Uri.EscapeDataString(mac.ToUpper())}&model={model}&time={DateTime.Now.ToUnixTime()}&user=1&version={version}";
			var sig = Utils.GetMd5String(url + "@ecowittnet");
			url = firmwareUrl + url + $"&sign={sig}";

			cumulus.LogDataMessage($"Ecowitt URL: {url}");

			FirmwareResponse retObj;

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (var response = await cumulus.MyHttpClient.GetAsync(url, token))
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetLatestFirmwareVersion: Ecowitt API Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetLatestFirmwareVersion: Ecowitt API Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var currentError = responseBody.FromJson<ErrorResp>();
					cumulus.LogWarningMessage($"API.GetLatestFirmwareVersion: Ecowitt API Error: {currentError.code}, {currentError.msg}");
					return null;
				}

				if (responseBody == "{}")
				{
					cumulus.LogWarningMessage("API.GetLatestFirmwareVersion: Ecowitt API: No data was returned.");
					return null;
				}
				else if (responseBody.StartsWith("{\"code\":")) // sanity check
				{
					// get the sensor data
					retObj = responseBody.FromJson<FirmwareResponse>();

					if (retObj == null || retObj.data == null)
					{
						return null;
					}
					else if (retObj.code == -1)
					{
						// -1 = no update required or error
						switch (retObj.msg)
						{
							case "The firmware is up to date":
								cumulus.LogMessage($"API.GetLatestFirmwareVersion: No update required, already on the latest version: {version}");
								cumulus.FirmwareAlarm.Triggered = false;
								return version;
							case "Operation too frequent":
								cumulus.LogMessage("API.GetLatestFirmwareVersion: Operation throttled, retrying later...");
								// delay 5 minutes and try again
								await Task.Delay(5 * 60 * 1000, token);
								await GetLatestFirmwareVersion(model, mac, version, token);
								return null;
							default:
								cumulus.LogMessage($"API.GetLatestFirmwareVersion: {retObj.msg}");
								return null;
						}
					}
					else if (retObj.data.name == null)
					{
						cumulus.LogWarningMessage("API.GetLatestFirmwareVersion: Ecowitt API: No version was returned.");
						return null;
					}
					else if (retObj.code == 0)
					{
						if (retObj.data.content.Contains("test"))  // "- This is a test firmware."
						{
							cumulus.LogMessage($"(\"API.GetLatestFirmwareVersion: You are running on test firmware: {retObj.data.name}");
							cumulus.FirmwareAlarm.Triggered = false;
							return null;
						}
						else
						{
							cumulus.FirmwareAlarm.LastMessage = $"A new firmware version is available: {retObj.data.name}.\nChange log:\n{retObj.data.content}";
							cumulus.FirmwareAlarm.Triggered = true;
							cumulus.LogWarningMessage($"API.GetLatestFirmwareVersion: Latest Version {retObj.data.name}, Change log:\n{retObj.data.content}");
							return retObj.data.name;
						}
					}
					else
					{
						cumulus.LogErrorMessage("API.GetLatestFirmwareVersion: Invalid message received");
						cumulus.LogDataMessage("API.GetLatestFirmwareVersion: Received: " + responseBody);
						return null;
					}
				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogErrorMessage("API.GetLatestFirmwareVersion: Invalid message received");
					cumulus.LogDataMessage("API.GetLatestFirmwareVersion: Received: " + responseBody);
					return null;
				}
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetLatestFirmwareVersion: Error - Request timed out, no response");
				return null;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetLatestFirmwareVersion: Exception occurred");
				return null;
			}
		}

		internal async Task<string[]> GetSimpleLatestFirmwareVersion(string model, CancellationToken token)
		{
			// Credit: https://www.wxforum.net/index.php?topic=46414.msg469763#msg469763

			cumulus.LogMessage("API.GetSimpleLatestFirmwareVersion: Get Ecowitt Latest Firmware Version");

			if (model == null || !SimpleSupportedModels.Contains(model[0..6]))
			{
				cumulus.LogMessage($"API.GetSimpleLatestFirmwareVersion: Your model - {model ?? "null"} - is not not currently supported");
				return null;
			}

			if (model.StartsWith("GW1000"))
			{
				model = "GW1000";
			}

			try
			{
				string responseBody;
				int responseCode;

				// we want to do this synchronously, so .Result
				using (var response = await cumulus.MyHttpClient.GetAsync(simpleFirmwareUrl, token))
				{
					responseBody = response.Content.ReadAsStringAsync(token).Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetSimpleLatestFirmwareVersion: Ecowitt API Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetSimpleLatestFirmwareVersion: Ecowitt API Response:\n{responseBody}");
				}

				if (responseCode != 200)
				{
					cumulus.LogWarningMessage($"API.GetSimpleLatestFirmwareVersion: Ecowitt API Error: Response Code={responseCode}, Content={responseBody}");
					return null;
				}

				IniFile ini = new IniFile();
				ini.LoadString(responseBody.Split('\n'));

				var ver = ini.GetValue(model, "VER", "");
				var notes = ini.GetValue(model, "NOTES", "");

				if (string.IsNullOrEmpty(ver))
				{
					cumulus.LogMessage($"API.GetSimpleLatestFirmwareVersion: No firmware version found for your model - {model}");
					return null;
				}

				return [ver.Split('V')[^1], notes];
			}
			catch (TaskCanceledException)
			{
				cumulus.LogWarningMessage("API.GetSimpleLatestFirmwareVersion: Error - Request timed out, no response");
				return null;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "API.GetSimpleLatestFirmwareVersion: Exception occurred");
				return null;
			}
		}

		private static string GetRandomMacAddress()
		{
			var buffer = new byte[6];
			Program.RandGenerator.NextBytes(buffer);
			var result = string.Concat(buffer.Select(x => string.Format("{0}:", x.ToString("X2"))).ToArray());
			return result.TrimEnd(':');
		}

#pragma warning disable S3459 // Unassigned members should be removed
#pragma warning disable S1144 // Unused private types or members should be removed
		private sealed class ErrorResp
		{
			public int code { get; set; }
			public string msg { get; set; }
			public DateTime time { get; set; }
			public object data { get; set; }

			public string GetErrorMessage()
			{
				return code switch
				{
					-1 => "System is busy",
					0 => "Success!",
					40000 => "Illegal parameter",
					40010 => "Illegal Application_Key Parameter",
					40011 => "Illegal Api_Key Parameter",
					40012 => "Illegal MAC/IMEI Parameter",
					40013 => "Illegal start_date Parameter",
					40014 => "Illegal end_date Parameter",
					40015 => "Illegal cycle_type Parameter",
					40016 => "Illegal call_back Parameter",
					40017 => "Missing Application_Key Parameter",
					40018 => "Missing Api_Key Parameter",
					40019 => "Missing MAC Parameter",
					40020 => "Missing start_date Parameter",
					40021 => "Missing end_date Parameter",
					40022 => "Illegal Voucher type",
					43001 => "Needs other service support",
					44001 => "Media file or data packet is null",
					45001 => "Over the limit or other error",
					46001 => "No existing request",
					47001 => "Parse JSON/XML contents error",
					48001 => "Privilege Problem",
					_ => "Unknown error code",
				};
			}
		}

		internal class HistoricResp
		{
			public int code { get; set; }
			public string msg { get; set; }
			public DateTime time { get; set; }
			public EcowittHistoricData data { get; set; }
		}

		//TODO: OK this works, but ouch!
		// refactor data as a dictionary object and parse each item indivually
		internal class EcowittHistoricData
		{
			public HistoricTempHum indoor { get; set; }
			public HistoricDataPressure pressure { get; set; }
			public HistoricOutdoor outdoor { get; set; }
			public HistoricDataWind wind { get; set; }
			public HistoricDataSolar solar_and_uvi { get; set; }
			public HistoricDataRainfall rainfall { get; set; }
			public HistoricDataRainfall rainfall_piezo { get; set; }
			public HistoricTempHum temp_and_humidity_ch1 { get; set; }
			public HistoricTempHum temp_and_humidity_ch2 { get; set; }
			public HistoricTempHum temp_and_humidity_ch3 { get; set; }
			public HistoricTempHum temp_and_humidity_ch4 { get; set; }
			public HistoricTempHum temp_and_humidity_ch5 { get; set; }
			public HistoricTempHum temp_and_humidity_ch6 { get; set; }
			public HistoricTempHum temp_and_humidity_ch7 { get; set; }
			public HistoricTempHum temp_and_humidity_ch8 { get; set; }
			public HistoricDataSoil soil_ch1 { get; set; }
			public HistoricDataSoil soil_ch2 { get; set; }
			public HistoricDataSoil soil_ch3 { get; set; }
			public HistoricDataSoil soil_ch4 { get; set; }
			public HistoricDataSoil soil_ch5 { get; set; }
			public HistoricDataSoil soil_ch6 { get; set; }
			public HistoricDataSoil soil_ch7 { get; set; }
			public HistoricDataSoil soil_ch8 { get; set; }
			public HistoricDataTemp temp_ch1 { get; set; }
			public HistoricDataTemp temp_ch2 { get; set; }
			public HistoricDataTemp temp_ch3 { get; set; }
			public HistoricDataTemp temp_ch4 { get; set; }
			public HistoricDataTemp temp_ch5 { get; set; }
			public HistoricDataTemp temp_ch6 { get; set; }
			public HistoricDataTemp temp_ch7 { get; set; }
			public HistoricDataTemp temp_ch8 { get; set; }
			public HistoricDataLeaf leaf_ch1 { get; set; }
			public HistoricDataLeaf leaf_ch2 { get; set; }
			public HistoricDataLeaf leaf_ch3 { get; set; }
			public HistoricDataLeaf leaf_ch4 { get; set; }
			public HistoricDataLeaf leaf_ch5 { get; set; }
			public HistoricDataLeaf leaf_ch6 { get; set; }
			public HistoricDataLeaf leaf_ch7 { get; set; }
			public HistoricDataLeaf leaf_ch8 { get; set; }
			public HistoricDataLightning lightning { get; set; }
			public HistoricDataCo2 indoor_co2 { get; set; }
			public HistoricDataCo2 co2_aqi_combo { get; set; }
			public HistoricDataPm25Aqi pm25_aqi_combo { get; set; }
			public HistoricDataPm10Aqi pm10_aqi_combo { get; set; }
			public HistoricTempHum t_rh_aqi_combo { get; set; }
			public HistoricDataPm25Aqi pm25_ch1 { get; set; }
			public HistoricDataPm25Aqi pm25_ch2 { get; set; }
			public HistoricDataPm25Aqi pm25_ch3 { get; set; }
			public HistoricDataPm25Aqi pm25_ch4 { get; set; }
			public HistoricDataLdsCh1 ch_lds1 { get; set; }
			public HistoricDataLdsCh2 ch_lds2 { get; set; }
			public HistoricDataLdsCh3 ch_lds3 { get; set; }
			public HistoricDataLdsCh4 ch_lds4 { get; set; }
		}

		internal class HistoricDataTypeInt
		{
			public string unit { get; set; }
			public Dictionary<DateTime, int?> list { get; set; }
		}

		internal class HistoricDataTypeDbl
		{
			public string unit { get; set; }
			public Dictionary<DateTime, decimal?> list { get; set; }
		}

		internal class HistoricTempHum
		{
			public HistoricDataTypeDbl temperature { get; set; }
			public HistoricDataTypeInt humidity { get; set; }
		}

		internal class HistoricOutdoor : HistoricTempHum
		{
			public HistoricDataTypeDbl dew_point { get; set; }
			public HistoricDataTypeDbl feels_like { get; set; }
			public HistoricDataTypeDbl app_temp { get; set; }
		}

		internal class HistoricDataPressure
		{
			public HistoricDataTypeDbl relative { get; set; }
			public HistoricDataTypeDbl absolute { get; set; }
		}

		internal class HistoricDataWind
		{
			public HistoricDataTypeInt wind_direction { get; set; }
			public HistoricDataTypeDbl wind_speed { get; set; }
			public HistoricDataTypeDbl wind_gust { get; set; }
		}

		internal class HistoricDataSolar
		{
			public HistoricDataTypeDbl solar { get; set; }
			public HistoricDataTypeDbl uvi { get; set; }
		}

		internal class HistoricDataRainfall
		{
			public HistoricDataTypeDbl rain_rate { get; set; }
			public HistoricDataTypeDbl yearly { get; set; }
		}

		internal class HistoricDataSoil
		{
			public HistoricDataTypeInt soilmoisture { get; set; }
		}

		internal class HistoricDataTemp
		{
			public HistoricDataTypeDbl temperature { get; set; }
		}

		internal class HistoricDataLeaf
		{
			public HistoricDataTypeInt leaf_wetness { get; set; }
		}

		internal class HistoricDataLightning
		{
			public HistoricDataTypeDbl distance { get; set; }
			public HistoricDataTypeInt count { get; set; }
		}

		[DataContract]
		internal class HistoricDataCo2
		{
			[DataMember(Name = "co2")]
			public HistoricDataTypeInt co2 { get; set; }
			[DataMember(Name = "24_hours_average")]
			public HistoricDataTypeInt average24h { get; set; }
		}

		internal class HistoricDataPm25Aqi
		{
			public HistoricDataTypeInt pm25 { get; set; }
		}

		internal class HistoricDataPm10Aqi
		{
			public HistoricDataTypeInt pm10 { get; set; }
		}

		internal class HistoricDataLdsCh1
		{
			public HistoricDataTypeDbl air_ch1 { get; set; }
			public HistoricDataTypeDbl depth_ch1 { get; set; }
			public HistoricDataTypeInt ldsheat_ch1 { get; set; }
		}

		internal class HistoricDataLdsCh2
		{
			public HistoricDataTypeDbl air_ch2 { get; set; }
			public HistoricDataTypeDbl depth_ch2 { get; set; }
			public HistoricDataTypeInt ldsheat_ch2 { get; set; }
		}

		internal class HistoricDataLdsCh3
		{
			public HistoricDataTypeDbl air_ch3 { get; set; }
			public HistoricDataTypeDbl depth_ch3 { get; set; }
			public HistoricDataTypeInt ldsheat_ch3 { get; set; }
		}

		internal class HistoricDataLdsCh4
		{
			public HistoricDataTypeDbl air_ch4 { get; set; }
			public HistoricDataTypeDbl depth_ch4 { get; set; }
			public HistoricDataTypeInt ldsheat_ch4 { get; set; }
		}


		internal class HistoricData
		{
			public int Interval { get; set; }
			public decimal? IndoorTemp { get; set; }
			public int? IndoorHum { get; set; }
			public decimal? Temp { get; set; }
			public decimal? DewPoint { get; set; }
			public decimal? FeelsLike { get; set; }
			public decimal? Apparent { get; set; }
			public int? Humidity { get; set; }
			public decimal? RainRate { get; set; }
			public decimal? RainYear { get; set; }
			public decimal? WindSpd { get; set; }
			public decimal? WindGust { get; set; }
			public int? WindDir { get; set; }
			public decimal? Pressure { get; set; }
			public decimal? StationPressure { get; set; }
			public double? Solar { get; set; }
			public decimal? UVI { get; set; }
			public DateTime LightningTime { get; set; }
			public decimal? LightningDist { get; set; }
			public int? LightningCount { get; set; }
			public decimal?[] ExtraTemp { get; set; } = new decimal?[9];
			public int?[] ExtraHumidity { get; set; } = new int?[9];
			public int?[] SoilMoist { get; set; } = new int?[9];
			public decimal?[] UserTemp { get; set; } = new decimal?[9];
			public int?[] LeafWetness { get; set; } = new int?[9];
			public decimal?[] pm25 { get; set; } = new decimal?[5];
			public decimal? AqiComboPm25 { get; set; }
			public decimal? AqiComboPm25hr24 { get; set; }
			public decimal? AqiComboPm10 { get; set; }
			public decimal? AqiComboPm10hr24 { get; set; }
			public decimal? AqiComboTemp { get; set; }
			public int? AqiComboHum { get; set; }
			public int? AqiComboCO2 { get; set; }
			public int? AqiComboCO2hr24 { get; set; }
			public int? IndoorCo2 { get; set; }
			public int? IndoorCo2hr24 { get; set; }
			public decimal?[] LdsAir { get; set; } = new decimal?[5];
			public decimal?[] LdsDepth { get; set; } = new decimal?[5];
		}


		private sealed class CurrentData
		{
			public int code { get; set; }
			public string msg { get; set; }
			public long time { get; set; }

			public CurrentDataData data { get; set; }
		}

		internal class CurrentDataData
		{
			public CurrentOutdoor outdoor { get; set; }
			public CurrentTempHum indoor { get; set; }
			public CurrentSolar solar_and_uvi { get; set; }
			public CurrentRain rainfall { get; set; }
			public CurrentRain rainfall_piezo { get; set; }
			public CurrentWind wind { get; set; }
			public CurrentPress pressure { get; set; }
			public CurrentLightning lightning { get; set; }
			public CurrentCo2 indoor_co2 { get; set; }
			public CurrentCo2 co2_aqi_combo { get; set; }
			public CurrentPm25 pm25_aqi_combo { get; set; }
			public CurrentPm10 pm10_aqi_combo { get; set; }
			public CurrentTempHum t_rh_aqi_combo { get; set; }
			public CurrentLeak water_leak { get; set; }
			public CurrentPm25 pm25_ch1 { get; set; }
			public CurrentPm25 pm25_ch2 { get; set; }
			public CurrentPm25 pm25_ch3 { get; set; }
			public CurrentPm25 pm25_ch4 { get; set; }
			public CurrentTempHum temp_and_humidity_ch1 { get; set; }
			public CurrentTempHum temp_and_humidity_ch2 { get; set; }
			public CurrentTempHum temp_and_humidity_ch3 { get; set; }
			public CurrentTempHum temp_and_humidity_ch4 { get; set; }
			public CurrentTempHum temp_and_humidity_ch5 { get; set; }
			public CurrentTempHum temp_and_humidity_ch6 { get; set; }
			public CurrentTempHum temp_and_humidity_ch7 { get; set; }
			public CurrentTempHum temp_and_humidity_ch8 { get; set; }
			public CurrentSoil soil_ch1 { get; set; }
			public CurrentSoil soil_ch2 { get; set; }
			public CurrentSoil soil_ch3 { get; set; }
			public CurrentSoil soil_ch4 { get; set; }
			public CurrentSoil soil_ch5 { get; set; }
			public CurrentSoil soil_ch6 { get; set; }
			public CurrentSoil soil_ch7 { get; set; }
			public CurrentSoil soil_ch8 { get; set; }
			public CurrentTemp temp_ch1 { get; set; }
			public CurrentTemp temp_ch2 { get; set; }
			public CurrentTemp temp_ch3 { get; set; }
			public CurrentTemp temp_ch4 { get; set; }
			public CurrentTemp temp_ch5 { get; set; }
			public CurrentTemp temp_ch6 { get; set; }
			public CurrentTemp temp_ch7 { get; set; }
			public CurrentTemp temp_ch8 { get; set; }
			public CurrentLeaf leaf_ch1 { get; set; }
			public CurrentLeaf leaf_ch2 { get; set; }
			public CurrentLeaf leaf_ch3 { get; set; }
			public CurrentLeaf leaf_ch4 { get; set; }
			public CurrentLeaf leaf_ch5 { get; set; }
			public CurrentLeaf leaf_ch6 { get; set; }
			public CurrentLeaf leaf_ch7 { get; set; }
			public CurrentLeaf leaf_ch8 { get; set; }
			public CurrentBattery battery { get; set; }
			public CurrentCamera camera { get; set; }
			public CurrentDataLdsCh1 ch_lds1 { get; set; }
			public CurrentDataLdsCh2 ch_lds2 { get; set; }
			public CurrentDataLdsCh3 ch_lds3 { get; set; }
			public CurrentDataLdsCh4 ch_lds4 { get; set; }
		}

		internal class CurrentOutdoor
		{
			public CurrentSensorValDbl temperature { get; set; }
			public CurrentSensorValDbl feels_like { get; set; }
			public CurrentSensorValDbl app_temp { get; set; }
			public CurrentSensorValDbl dew_point { get; set; }
			public CurrentSensorValInt humidity { get; set; }
		}

		internal class CurrentTemp
		{
			public CurrentSensorValDbl temperature { get; set; }
		}

		internal class CurrentTempHum
		{
			public CurrentSensorValDbl temperature { get; set; }
			public CurrentSensorValInt humidity { get; set; }

		}

		internal class CurrentSolar
		{
			public CurrentSensorValDbl solar { get; set; }
			public CurrentSensorValInt uvi { get; set; }
		}

		internal class CurrentRain
		{
			public CurrentSensorValDbl rain_rate { get; set; }
			public CurrentSensorValDbl daily { get; set; }

			[DataMember(Name = "event")]
			public CurrentSensorValDbl Event { get; set; }
			public CurrentSensorValDbl hourly { get; set; }
			public CurrentSensorValDbl yearly { get; set; }
		}

		internal class CurrentWind
		{
			public CurrentSensorValDbl wind_speed { get; set; }
			public CurrentSensorValDbl wind_gust { get; set; }
			public CurrentSensorValInt wind_direction { get; set; }
		}

		internal class CurrentPress
		{
			public CurrentSensorValDbl relative { get; set; }
			public CurrentSensorValDbl absolute { get; set; }
		}

		internal class CurrentLightning
		{
			public CurrentSensorValInt distance { get; set; }
			public CurrentSensorValInt count { get; set; }
		}

		internal class CurrentCo2
		{
			public CurrentSensorValInt co2 { get; set; }

			[DataMember(Name = "24_hours_average")]
			public CurrentSensorValInt Avg24h { get; set; }
		}

		internal class CurrentPm25
		{
			public CurrentSensorValInt real_time_aqi { get; set; }
			public CurrentSensorValInt pm25 { get; set; }

			[DataMember(Name = "24_hours_aqi")]
			public CurrentSensorValInt AqiAvg24h { get; set; }
		}

		internal class CurrentPm10
		{
			public CurrentSensorValInt real_time_aqi { get; set; }
			public CurrentSensorValInt pm10 { get; set; }

			[DataMember(Name = "24_hours_aqi")]
			public CurrentSensorValInt AqiAvg24h { get; set; }
		}

		internal class CurrentLeak
		{
			public CurrentSensorValInt leak_ch1 { get; set; }
			public CurrentSensorValInt leak_ch2 { get; set; }
			public CurrentSensorValInt leak_ch3 { get; set; }
			public CurrentSensorValInt leak_ch4 { get; set; }
		}

		internal class CurrentSoil
		{
			public CurrentSensorValInt soilmoisture { get; set; }
		}

		internal class CurrentLeaf
		{
			public CurrentSensorValInt leaf_wetness { get; set; }
		}

		internal class CurrentBattery
		{
			public CurrentSensorValInt t_rh_p_sensor { get; set; }
			public CurrentSensorValDbl ws1900_console { get; set; }
			public CurrentSensorValDbl ws1800_console { get; set; }
			public CurrentSensorValInt ws6006_console { get; set; }
			public CurrentSensorValDbl console { get; set; }
			public CurrentSensorValInt outdoor_t_rh_sensor { get; set; }
			public CurrentSensorValDbl wind_sensor { get; set; }
			public CurrentSensorValDbl haptic_array_battery { get; set; }
			public CurrentSensorValDbl haptic_array_capacitor { get; set; }
			public CurrentSensorValDbl sonic_array { get; set; }
			public CurrentSensorValDbl rainfall_sensor { get; set; }
			public CurrentSensorValInt sensor_array { get; set; }
			public CurrentSensorValInt lightning_sensor { get; set; }
			public CurrentSensorValInt aqi_combo_sensor { get; set; }
			public CurrentSensorValInt water_leak_sensor_ch1 { get; set; }
			public CurrentSensorValInt water_leak_sensor_ch2 { get; set; }
			public CurrentSensorValInt water_leak_sensor_ch3 { get; set; }
			public CurrentSensorValInt water_leak_sensor_ch4 { get; set; }
			public CurrentSensorValInt pm25_sensor_ch1 { get; set; }
			public CurrentSensorValInt pm25_sensor_ch2 { get; set; }
			public CurrentSensorValInt pm25_sensor_ch3 { get; set; }
			public CurrentSensorValInt pm25_sensor_ch4 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch1 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch2 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch3 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch4 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch5 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch6 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch7 { get; set; }
			public CurrentSensorValInt temp_humidity_sensor_ch8 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch1 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch2 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch3 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch4 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch5 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch6 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch7 { get; set; }
			public CurrentSensorValDbl soilmoisture_sensor_ch8 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch1 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch2 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch3 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch4 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch5 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch6 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch7 { get; set; }
			public CurrentSensorValDbl temperature_sensor_ch8 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch1 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch2 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch3 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch4 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch5 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch6 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch7 { get; set; }
			public CurrentSensorValDbl leaf_wetness_sensor_ch8 { get; set; }
			public CurrentSensorValDbl ldsbatt_1 { get; set; }
			public CurrentSensorValDbl ldsbatt_2 { get; set; }
			public CurrentSensorValDbl ldsbatt_3 { get; set; }
			public CurrentSensorValDbl ldsbatt_4 { get; set; }
		}

		internal class CurrentCamera
		{
			public CurrentCameraVal photo { get; set; }
		}

		internal class CurrentSensorValDbl
		{
			public long time { get; set; }
			public string unit { get; set; }
			public double value { get; set; }
		}

		internal class CurrentSensorValInt
		{
			public long time { get; set; }
			public string unit { get; set; }
			public int value { get; set; }
		}

		internal class CurrentCameraVal
		{
			public long time { get; set; }
			public string url { get; set; }
		}

		internal class CurrentDataLdsCh1
		{
			public CurrentDataLdsProp air_ch1 { get; set; }
			public CurrentDataLdsProp depth_ch1 { get; set; }
			public CurrentDataLdsProp ldsheat_ch1 { get; set; }
		}

		internal class CurrentDataLdsCh2
		{
			public CurrentDataLdsProp air_ch2 { get; set; }
			public CurrentDataLdsProp depth_ch2 { get; set; }
			public CurrentDataLdsProp ldsheat_ch2 { get; set; }
		}

		internal class CurrentDataLdsCh3
		{
			public CurrentDataLdsProp air_ch3 { get; set; }
			public CurrentDataLdsProp depth_ch3 { get; set; }
			public CurrentDataLdsProp ldsheat_ch3 { get; set; }
		}

		internal class CurrentDataLdsCh4
		{
			public CurrentDataLdsProp air_ch4 { get; set; }
			public CurrentDataLdsProp depth_ch4 { get; set; }
			public CurrentDataLdsProp ldsheat_ch4 { get; set; }
		}

		internal class CurrentDataLdsProp
		{
			public int time { get; set; }
			public string unit { get; set; }
			public decimal? value { get; set; }
		}

		internal class StationList
		{
			public int code { get; set; }
			public string msg { get; set; }
			public long time { get; set; }

			public StationListData data { get; set; }
		}

		internal class StationListData
		{
			public int total { get; set; }
			public int totalPage { get; set; }
			public int pageNum { get; set; }
			public StationListDataStations[] list { get; set; }
		}

		internal class StationListDataStations
		{
			public int id { get; set; }
			public string name { get; set; }
			public string mac { get; set; }
			public string imei { get; set; }
			public int type { get; set; }
			public string date_zone_id { get; set; }
			public long createtime { get; set; }
			public double longitude { get; set; }
			public double latitude { get; set; }
			public string stationtype { get; set; }
		}

		private sealed class FirmwareResponse
		{
			public int code { get; set; }
			public string msg { get; set; }
			public int time { get; set; }
			public FirmwareData data { get; set; }
		}

		private sealed class FirmwareData
		{
			public int id { get; set; }
			public string name { get; set; }
			public string content { get; set; }
			public string attach1file { get; set; }
			public string attach2file { get; set; }
			public int queryintval { get; set; }
		}
#pragma warning restore S1144 // Unused private types or members should be removed
#pragma warning restore S3459 // Unassigned members should be removed
	}
}
