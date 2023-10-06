using System;
using System.Collections.Generic;
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

		private static readonly string historyUrl = "https://api.ecowitt.net/api/v3/device/history?";
		private static readonly string currentUrl = "https://api.ecowitt.net/api/v3/device/real_time?";

		private static readonly int EcowittApiFudgeFactor = 5; // Number of minutes that Ecowitt API data is delayed

		private DateTime LastCurrentDataTime = DateTime.MinValue;

		public EcowittApi(Cumulus cuml, WeatherStation stn)
		{
			cumulus = cuml;
			station = stn;

			//httpClient.DefaultRequestHeaders.ConnectionClose = true;

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
		}


		internal bool GetHistoricData(DateTime startTime, DateTime endTime, CancellationToken token)
		{
			// DOC: https://doc.ecowitt.net/web/#/apiv3en?page_id=19

			var data = new EcowittHistoricData();

			cumulus.LogMessage("API.GetHistoricData: Get Ecowitt Historic Data");

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!", Cumulus.LogLevel.Warning);
				cumulus.LastUpdateTime = DateTime.Now;
				return false;
			}

			var apiStartDate = startTime.AddMinutes(-EcowittApiFudgeFactor);
			var apiEndDate = endTime;

			var sb = new StringBuilder(historyUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			sb.Append($"&mac={cumulus.EcowittMacAddress}");
			sb.Append($"&start_date={apiStartDate:yyyy-MM-dd'%20'HH:mm:ss}");
			sb.Append($"&end_date={apiEndDate:yyyy-MM-dd'%20'HH:mm:ss}");

			// Request the data in the correct units
			sb.Append($"&temp_unitid={cumulus.Units.Temp + 1}"); // 1=C, 2=F
			sb.Append($"&pressure_unitid={(cumulus.Units.Press == 2 ? "4" : "3")}"); // 3=hPa, 4=inHg, 5=mmHg
			string windUnit;
			switch (cumulus.Units.Wind)
			{
				case 0: // m/s
					windUnit = "6";
					break;
				case 1: // mph
					windUnit = "9";
					break;
				case 2: // km/h
					windUnit = "7";
					break;
				case 3: // knots
					windUnit = "8";
					break;
				default:
					windUnit = "?";
					break;
			}
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
				"indoor_co2",
				"co2_aqi_combo",
				"pm25_ch1",
				"pm25_ch2",
				"pm25_ch3",
				"pm25_ch4"
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
			cumulus.LogConsoleMessage(msg);

			var logUrl = url.Replace(cumulus.EcowittApplicationKey, "<<App-key>>").Replace(cumulus.EcowittUserApiKey, "<<User-key>>");
			cumulus.LogDebugMessage($"Ecowitt URL = {logUrl}");

			try
			{
				string responseBody;
				int responseCode;
				int retries = 3;
				bool success = false;
				do
				{
					// we want to do this synchronously, so .Result
					using (var response = Cumulus.MyHttpClient.GetAsync(url).Result)
					{
						responseBody = response.Content.ReadAsStringAsync().Result;
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"API.GetHistoricData: Ecowitt API Historic Response code: {responseCode}");
						cumulus.LogDataMessage($"API.GetHistoricData: Ecowitt API Historic Response: {responseBody}");
					}

					if (responseCode != 200)
					{
						var historyError = responseBody.FromJson<ErrorResp>();
						cumulus.LogMessage($"API.GetHistoricData: Ecowitt API Historic Error: {historyError.code}, {historyError.msg}, Cumulus.LogLevel.Warning");
						cumulus.LogConsoleMessage($" - Error {historyError.code}: {historyError.msg}", ConsoleColor.Red);
						cumulus.LastUpdateTime = endTime;
						return false;
					}


					if (responseBody == "{}")
					{
						cumulus.LogMessage("API.GetHistoricData: Ecowitt API Historic: No data was returned.");
						cumulus.LogConsoleMessage(" - No historic data available");
						cumulus.LastUpdateTime = endTime;
						return false;
					}
					else if (responseBody.StartsWith("{\"code\":")) // sanity check
					{
						// get the sensor data
						var histObj = responseBody.FromJson<HistoricResp>();

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
									cumulus.LogMessage("API.GetHistoricData: Error decoding the response - " + ex.Message, Cumulus.LogLevel.Error);
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

								cumulus.LogMessage("API.GetHistoricData: System Busy or Rate Limited, waiting 5 secs before retry...");
								Task.Delay(5000, token).Wait();
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
						cumulus.LogMessage("API.GetHistoricData: Invalid historic message received", Cumulus.LogLevel.Error);
						cumulus.LogDataMessage("API.GetHistoricData: Received: " + responseBody);
						cumulus.LastUpdateTime = endTime;
						return false;
					}

					//
				} while (!success);

				if (!token.IsCancellationRequested)
				{
					ProcessHistoryData(data, token);
				}

				return true;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("API.GetHistoricData: Exception: " + ex.Message, Cumulus.LogLevel.Error);
				cumulus.LastUpdateTime = endTime;
				return false;
			}
		}

		private void ProcessHistoryData(EcowittHistoricData data, CancellationToken token)
		{
			// allocate a dictionary of data objects, keyed on the timestamp
			var buffer = new SortedDictionary<DateTime, EcowittApi.HistoricData>();

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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].IndoorTemp = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.IndoorTemp = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].IndoorHum = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.IndoorHum = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing indoor data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].Temp = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.Temp = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].Humidity = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.Humidity = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].DewPoint = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.DewPoint = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing outdoor data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].WindSpd = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.WindSpd = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].WindGust = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.WindGust = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].WindDir = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.WindDir = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing wind data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].Pressure = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.Pressure = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing pressure data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
				}
			}
			// Rainfall Data
			if (data.rainfall != null)
			{
				try
				{
					if (cumulus.Gw1000PrimaryRainSensor == 0)
					{
						// rain rate
						if (data.rainfall.rain_rate != null && data.rainfall.rain_rate.list != null)
						{
							foreach (var item in data.rainfall.rain_rate.list)
							{
								var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

								if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
									continue;

								if (buffer.ContainsKey(itemDate))
								{
									buffer[itemDate].RainRate = item.Value;
								}
								else
								{
									var newItem = new EcowittApi.HistoricData();
									newItem.RainRate = item.Value;
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

								if (buffer.ContainsKey(itemDate))
								{
									buffer[itemDate].RainYear = item.Value;
								}
								else
								{
									var newItem = new EcowittApi.HistoricData();
									newItem.RainYear = item.Value;
									buffer.Add(itemDate, newItem);
								}
							}
						}
					}
					else // rainfall piezo
					{
						// rain rate
						if (data.rainfall_piezo.rain_rate != null && data.rainfall_piezo.rain_rate.list != null)
						{
							foreach (var item in data.rainfall_piezo.rain_rate.list)
							{
								var itemDate = item.Key.AddMinutes(EcowittApiFudgeFactor);

								if (!item.Value.HasValue || itemDate < cumulus.LastUpdateTime)
									continue;

								if (buffer.ContainsKey(itemDate))
								{
									buffer[itemDate].RainRate = item.Value;
								}
								else
								{
									var newItem = new EcowittApi.HistoricData();
									newItem.RainRate = item.Value;
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

								if (buffer.ContainsKey(itemDate))
								{
									buffer[itemDate].RainYear = item.Value;
								}
								else
								{
									var newItem = new EcowittApi.HistoricData();
									newItem.RainYear = item.Value;
									buffer.Add(itemDate, newItem);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing rainfall data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].Solar = (int) item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.Solar = (int) item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].UVI = (int) item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.UVI = (int) item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing solar data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
				}
			}
			// Extra 8 channel sensors
			for (var i = 1; i <= 8; i++)
			{
				EcowittApi.HistoricTempHum srcTH = null;
				EcowittApi.HistoricDataSoil srcSoil = null;
				EcowittApi.HistoricDataTemp srcTemp = null;
				EcowittApi.HistoricDataLeaf srcLeaf = null;
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

								if (buffer.ContainsKey(itemDate))
								{
									buffer[itemDate].ExtraTemp[i] = item.Value;
								}
								else
								{
									var newItem = new EcowittApi.HistoricData();
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

								if (buffer.ContainsKey(itemDate))
								{
									buffer[itemDate].ExtraHumidity[i] = item.Value;
								}
								else
								{
									var newItem = new EcowittApi.HistoricData();
									newItem.ExtraHumidity[i] = item.Value;
									buffer.Add(itemDate, newItem);
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"API.ProcessHistoryData: Error in pre-processing extra T/H data - chan[{i}]. Exception: {ex.Message}", Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].SoilMoist[i] = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.SoilMoist[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"API.ProcessHistoryData: Error in pre-processing extra soil moisture data - chan[{i}]. Exception: {ex.Message}", Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].UserTemp[i] = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.UserTemp[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"API.ProcessHistoryData: Error in pre-processing extra user temp data - chan[{i}]. Exception: {ex.Message}", Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].LeafWetness[i] = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.LeafWetness[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"API.ProcessHistoryData: Error in pre-processing extra leaf wetness data - chan[{i}]. Exception:{ex.Message}", Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].IndoorCo2 = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.IndoorCo2 = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].IndoorCo2hr24 = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.IndoorCo2hr24 = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing indoor CO2 data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].CO2pm2p5 = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.CO2pm2p5 = item.Value;
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].CO2pm2p5hr24 = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.CO2pm2p5hr24 = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing CO2 combo data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].AqiComboPm25 = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.AqiComboPm25 = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing pm 2.5 combo data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].AqiComboPm10 = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.AqiComboPm10 = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("API.ProcessHistoryData: Error in pre-processing pm 10 combo data. Exception: " + ex.Message, Cumulus.LogLevel.Error);
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

							if (buffer.ContainsKey(itemDate))
							{
								buffer[itemDate].pm25[i] = item.Value;
							}
							else
							{
								var newItem = new EcowittApi.HistoricData();
								newItem.pm25[i] = item.Value;
								buffer.Add(itemDate, newItem);
							}
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"API.ProcessHistoryData: Error in pre-processing 4 chan pm 2.5 data - chan[{i}] . Exception: {ex.Message}", Cumulus.LogLevel.Error);
				}
			}

			// now we have all the data for this period, for each record create the string expected by ProcessData and get it processed
			var rollHour = Math.Abs(cumulus.GetHourInc());
			var luhour = cumulus.LastUpdateTime.Hour;
			var rolloverdone = luhour == rollHour;
			var midnightraindone = luhour == 0;

			foreach (var rec in buffer)
			{
				if (token.IsCancellationRequested)
				{
					return;
				}

				cumulus.LogMessage("Processing data for " + rec.Key);

				var h = rec.Key.Hour;

				//  if outside rollover hour, rollover yet to be done
				if (h != rollHour) rolloverdone = false;

				// In rollover hour and rollover not yet done
				if (h == rollHour && !rolloverdone)
				{
					// do rollover
					cumulus.LogMessage("Day rollover " + rec.Key.ToShortTimeString());
					station.DayReset(rec.Key);

					rolloverdone = true;
				}

				// Not in midnight hour, midnight rain yet to be done
				if (h != 0) midnightraindone = false;

				// In midnight hour and midnight rain (and sun) not yet done
				if (h == 0 && !midnightraindone)
				{
					station.ResetMidnightRain(rec.Key);
					station.ResetSunshineHours(rec.Key);
					station.ResetMidnightTemperatures(rec.Key);
					midnightraindone = true;
				}

				// finally apply this data
				ApplyHistoricData(rec);

				// add in archive period worth of sunshine, if sunny
				if (station.CurrentSolarMax > 0 &&
					station.SolarRad > station.CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100 &&
					station.SolarRad >= cumulus.SolarOptions.SolarMinimum &&
					!cumulus.SolarOptions.UseBlakeLarsen)
				{
					station.SunshineHours += 5 / 60.0;
					cumulus.LogDebugMessage("Adding 5 minutes to Sunshine Hours");
				}

				// add in archive period minutes worth of temperature to the temp samples
				station.tempsamplestoday += 5;
				station.TempTotalToday += (station.OutdoorTemperature * 5);

				// add in 'following interval' minutes worth of wind speed to windrun
				cumulus.LogMessage("Windrun: " + station.WindAverage.ToString(cumulus.WindFormat) + cumulus.Units.WindText + " for " + 5 + " minutes = " +
								   (station.WindAverage * station.WindRunHourMult[cumulus.Units.Wind] * 5 / 60.0).ToString(cumulus.WindRunFormat) + cumulus.Units.WindRunText);

				station.WindRunToday += station.WindAverage * station.WindRunHourMult[cumulus.Units.Wind] * 5 / 60.0;

				// update heating/cooling degree days
				station.UpdateDegreeDays(5);

				// update dominant wind bearing
				station.CalculateDominantWindBearing(station.Bearing, station.WindAverage, 5);

				station.CheckForWindrunHighLow(rec.Key);

				//bw?.ReportProgress((totalentries - datalist.Count) * 100 / totalentries, "processing");

				//UpdateDatabase(timestamp.ToUniversalTime(), historydata.interval, false);

				cumulus.DoLogFile(rec.Key, false);
				cumulus.DoCustomIntervalLogs(rec.Key);

				if (cumulus.StationOptions.LogExtraSensors) cumulus.DoExtraLogFile(rec.Key);

				//AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing,
				//    OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
				//    OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex);

				station.AddRecentDataWithAq(rec.Key, station.WindAverage, station.RecentMaxGust, station.WindLatest, station.Bearing, station.AvgBearing, station.OutdoorTemperature, station.WindChill, station.OutdoorDewpoint, station.HeatIndex,
					station.OutdoorHumidity, station.Pressure, station.RainToday, station.SolarRad, station.UV, station.Raincounter, station.FeelsLike, station.Humidex, station.ApparentTemperature, station.IndoorTemperature, station.IndoorHumidity, station.CurrentSolarMax, station.RainRate);

				if (cumulus.StationOptions.CalculatedET && rec.Key.Minute == 0)
				{
					// Start of a new hour, and we want to calculate ET in Cumulus
					station.CalculateEvaoptranspiration(rec.Key);
				}

				station.DoTrendValues(rec.Key);
				station.UpdatePressureTrendString();
				station.UpdateStatusPanel(rec.Key);
				cumulus.AddToWebServiceLists(rec.Key);

			}
		}

		private void ApplyHistoricData(KeyValuePair<DateTime, HistoricData> rec)
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
					var dirVal = (int) rec.Value.WindDir.Value;

					station.DoWind(gustVal, dirVal, spdVal, rec.Key);
				}
				else
				{
					cumulus.LogMessage("ApplyHistoricData: Insufficient data process wind", Cumulus.LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Wind data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Humidity ===
			// = avg for period
			try
			{
				if (rec.Value.IndoorHum.HasValue)
				{
					station.DoIndoorHumidity(rec.Value.IndoorHum.Value);
				}
				else
				{
					cumulus.LogMessage("ApplyHistoricData: Missing indoor humidity data", Cumulus.LogLevel.Warning);
				}

				if (rec.Value.Humidity.HasValue && cumulus.Gw1000PrimaryTHSensor == 0)
				{
					station.DoOutdoorHumidity(rec.Value.Humidity.Value, rec.Key);
				}
				else
				{
					cumulus.LogMessage("ApplyHistoricData: Missing outdoor humidity data", Cumulus.LogLevel.Warning);
				}

			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Humidity data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Pressure ===
			// = avg for period
			try
			{
				if (rec.Value.Pressure.HasValue)
				{
					var pressVal = (double) rec.Value.Pressure;
					station.DoPressure(pressVal, rec.Key);
					station.UpdatePressureTrendString();
				}
				else
				{
					cumulus.LogMessage("ApplyHistoricData: Missing pressure data", Cumulus.LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Pressure data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Indoor temp ===
			// = avg for period
			try
			{
				if (rec.Value.IndoorTemp.HasValue)
				{
					var tempVal = (double) rec.Value.IndoorTemp;
					station.DoIndoorTemp(tempVal);
				}
				else
				{
					cumulus.LogMessage("ApplyHistoricData: Missing indoor temperature data", Cumulus.LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Indoor temp data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Outdoor temp ===
			// = avg for period
			try
			{
				if (rec.Value.Temp.HasValue && cumulus.Gw1000PrimaryTHSensor == 0)
				{
					var tempVal = (double) rec.Value.Temp;
					station.DoOutdoorTemp(tempVal, rec.Key);
				}
				else
				{
					cumulus.LogMessage("ApplyHistoricData: Missing outdoor temperature data", Cumulus.LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Outdoor temp data - " + ex.Message, Cumulus.LogLevel.Error);
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
					cumulus.LogMessage("ApplyHistoricData: Missing rain data", Cumulus.LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Rain data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Solar ===
			// = max for period
			try
			{
				if (rec.Value.Solar.HasValue)
				{
					station.DoSolarRad(rec.Value.Solar.Value, rec.Key);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Solar data - " + ex.Message, Cumulus.LogLevel.Error);
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
				cumulus.LogMessage("ApplyHistoricData: Error in Solar data - " + ex.Message, Cumulus.LogLevel.Error);
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
						if (i == cumulus.Gw1000PrimaryTHSensor)
						{
							station.DoOutdoorTemp(tempVal, rec.Key);
						}
						station.DoExtraTemp(tempVal, i);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ApplyHistoricData: Error in extra temperature data - {ex.Message}", Cumulus.LogLevel.Error);
				}
				// === Extra Humidity ===
				try
				{
					if (rec.Value.ExtraHumidity[i].HasValue)
					{
						if (i == cumulus.Gw1000PrimaryTHSensor)
						{
							station.DoOutdoorHumidity(rec.Value.ExtraHumidity[i].Value, rec.Key);
						}
						station.DoExtraHum(rec.Value.ExtraHumidity[i].Value, i);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ApplyHistoricData: Error in extra humidity data - {ex.Message}", Cumulus.LogLevel.Error);
				}


				// === User Temperature ===
				try
				{
					if (rec.Value.UserTemp[i].HasValue)
					{
						if (cumulus.EcowittMapWN34[i] == 0)
						{
							station.DoUserTemp((double) rec.Value.UserTemp[i], i);
						}
						else
						{
							station.DoSoilTemp((double) rec.Value.UserTemp[i], cumulus.EcowittMapWN34[i]);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ApplyHistoricData: Error in extra user temperature data - {ex.Message}", Cumulus.LogLevel.Error);
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
					cumulus.LogMessage($"ApplyHistoricData: Error in soil moisture data - {ex.Message}", Cumulus.LogLevel.Error);
				}
			}

			// === Indoor CO2 ===
			try
			{
				if (rec.Value.IndoorCo2.HasValue)
				{
					station.CO2_pm2p5 = rec.Value.IndoorCo2.Value;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in CO2 data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Indoor CO2 24hr avg ===
			try
			{
				if (rec.Value.IndoorCo2hr24.HasValue)
				{
					station.CO2_pm2p5_24h = rec.Value.CO2pm2p5hr24.Value;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in CO2 24hr avg data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === PM 2.5 Combo
			try
			{
				if (rec.Value.AqiComboPm25.HasValue)
				{
					station.CO2_pm2p5 = (double) rec.Value.AqiComboPm25.Value;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in AQI Combo pm2.5 data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === PM 10 Combo
			try
			{
				if (rec.Value.AqiComboPm10.HasValue)
				{
					station.CO2_pm10 = (double) rec.Value.AqiComboPm10.Value;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in AQI Combo pm10 data - " + ex.Message, Cumulus.LogLevel.Error);
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
					cumulus.LogMessage($"ApplyHistoricData: Error in extra temperature data - {ex.Message}", Cumulus.LogLevel.Error);
				}
			}


			// Do all the derived values after the primary data

			// === Dewpoint ===
			try
			{
				if (cumulus.StationOptions.CalculatedDP)
				{
					station.DoOutdoorDewpoint(0, rec.Key);
				}
				else if (rec.Value.DewPoint.HasValue)
				{
					var val = (double) rec.Value.DewPoint;
					station.DoOutdoorDewpoint(val, rec.Key);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Dew point data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Wind Chill ===
			try
			{
				if (cumulus.StationOptions.CalculatedWC && rec.Value.WindSpd.HasValue)
				{
					station.DoWindChill(0, rec.Key);
				}
				else
				{
					// historic API does not provide Wind Chill so force calculation
					cumulus.StationOptions.CalculatedWC = true;
					station.DoWindChill(0, rec.Key);
					cumulus.StationOptions.CalculatedWC = false;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Wind chill data - " + ex.Message, Cumulus.LogLevel.Error);
			}

			// === Humidex ===
			try
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
					cumulus.LogMessage("ApplyHistoricData: Insufficient data to calculate Apparent/Feels Like temps", Cumulus.LogLevel.Warning);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("ApplyHistoricData: Error in Humidex/Apparant/Feels Like - " + ex.Message, Cumulus.LogLevel.Error);
			}
		}


		// returns the data structure and the number of seconds to wait before the next update
		internal CurrentDataData GetCurrentData(CancellationToken token, ref int delay)
		{
			// Doc: https://doc.ecowitt.net/web/#/apiv3en?page_id=17

			cumulus.LogMessage("API.GetCurrentData: Get Ecowitt Current Data");

			var sb = new StringBuilder(currentUrl);

			sb.Append($"application_key={cumulus.EcowittApplicationKey}");
			sb.Append($"&api_key={cumulus.EcowittUserApiKey}");
			sb.Append($"&mac={cumulus.EcowittMacAddress}");

			// Request the data in the correct units
			sb.Append($"&temp_unitid={cumulus.Units.Temp + 1}"); // 1=C, 2=F
			sb.Append($"&pressure_unitid={(cumulus.Units.Press == 2 ? "4" : "3")}"); // 3=hPa, 4=inHg, 5=mmHg
			string windUnit;
			switch (cumulus.Units.Wind)
			{
				case 0: // m/s
					windUnit = "6";
					break;
				case 1: // mph
					windUnit = "9";
					break;
				case 2: // km/h
					windUnit = "7";
					break;
				case 3: // knots
					windUnit = "8";
					break;
				default:
					windUnit = "?";
					break;
			}
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
				using (var response = Cumulus.MyHttpClient.GetAsync(url).Result)
				{
					responseBody = response.Content.ReadAsStringAsync().Result;
					responseCode = (int) response.StatusCode;
					cumulus.LogDebugMessage($"API.GetCurrentData: Ecowitt API Current Response code: {responseCode}");
					cumulus.LogDataMessage($"API.GetCurrentData: Ecowitt API Current Response: {responseBody}");
				}

				if (responseCode != 200)
				{
					var currentError = responseBody.FromJson<ErrorResp>();
					cumulus.LogMessage($"API.GetCurrentData: Ecowitt API Current Error: {currentError.code}, {currentError.msg}", Cumulus.LogLevel.Warning);
					cumulus.LogConsoleMessage($" - Error {currentError.code}: {currentError.msg}", ConsoleColor.Red);
					delay = 10;
					return null;
				}


				if (responseBody == "{}")
				{
					cumulus.LogMessage("API.GetCurrentData: Ecowitt API Current: No data was returned.", Cumulus.LogLevel.Warning);
					cumulus.LogConsoleMessage(" - No current data available");
					delay = 10;
					return null;
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
								delay = 10;
								return null;
							}
						}
						else if (currObj.code == -1 || currObj.code == 45001)
						{
							// -1 = system busy, 45001 = rate limited

							cumulus.LogMessage("API.GetCurrentData: System Busy or Rate Limited, waiting 5 secs before retry...");
							delay = 5;
							return null;
						}
						else
						{
							delay = 10;
							return null;
						}
					}
					else
					{
						delay = 10;
						return null;
					}

				}
				else // No idea what we got, dump it to the log
				{
					cumulus.LogMessage("API.GetCurrentData: Invalid current message received", Cumulus.LogLevel.Error);
					cumulus.LogDataMessage("API.GetCurrentData: Received: " + responseBody);
					delay = 10;
					return null;
				}

				if (!token.IsCancellationRequested)
				{
					// indoor values should always be present, so use them for teh data timestamp
					var dataTime = Utils.FromUnixTime(currObj.data.indoor.temperature.time);
					cumulus.LogDebugMessage($"EcowittCloud: Last data update {dataTime:s}");

					if (dataTime != LastCurrentDataTime)
					{
						//ProcessCurrentData(currObj.data, token);
						LastCurrentDataTime = dataTime;

						// how many seconds to the next update?
						// the data is updated once a minute, so wait for 5 seonds after the next update

						var lastUpdate = (DateTime.Now - LastCurrentDataTime).TotalSeconds;
						if (lastUpdate > 65)
						{
							// hum the data is already out of date, query again after a short delay
							delay = 10;
							return null;
						}
						else
						{
							delay = (int) (60 - lastUpdate + 3);
							return currObj.data;
						}
					}
					else
					{
						delay = 10;
						return null;
					}
				}

				delay = 20;
				return null;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("API.GetCurrentData: Exception: " + ex.Message, Cumulus.LogLevel.Error);
				delay = 10;
				return null;
			}
		}


		/*
		private string ErrorCode(int code)
		{
			switch (code)
			{
				case -1: return "System is busy";
				case 0: return "Success!";
				case 40000: return "Illegal parameter";
				case 40010: return "Illegal Application_Key Parameter";
				case 40011: return "Illegal Api_Key Parameter";
				case 40012: return "Illegal MAC/IMEI Parameter";
				case 40013: return "Illegal start_date Parameter";
				case 40014: return "Illegal end_date Parameter";
				case 40015: return "Illegal cycle_type Parameter";
				case 40016: return "Illegal call_back Parameter";
				case 40017: return "Missing Application_Key Parameter";
				case 40018: return "Missing Api_Key Parameter";
				case 40019: return "Missing MAC Parameter";
				case 40020: return "Missing start_date Parameter";
				case 40021: return "Missing end_date Parameter";
				case 40022: return "Illegal Voucher type";
				case 43001: return "Needs other service support";
				case 44001: return "Media file or data packet is null";
				case 45001: return "Over the limit or other error";
				case 46001: return "No existing request";
				case 47001: return "Parse JSON/XML contents error";
				case 48001: return "Privilege Problem";
				default: return "Unknown error code";
			}
		}
		*/

		private class ErrorResp
		{
			public int code { get; set; }
			public string msg { get; set; }
			public DateTime time { get; set; }
			public object data { get; set; }
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
			public HistoricDataPm25Aqi pm25_ch1 { get; set; }
			public HistoricDataPm25Aqi pm25_ch2 { get; set; }
			public HistoricDataPm25Aqi pm25_ch3 { get; set; }
			public HistoricDataPm25Aqi pm25_ch4 { get; set; }

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
		}

		internal class HistoricDataPressure
		{
			public HistoricDataTypeDbl relative { get; set; }
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
			public HistoricDataTypeInt co2 { get; set; }
			[DataMember(Name = "24_hours_average")]
			public HistoricDataTypeInt average24h { get; set; }
		}

		internal class HistoricDataPm25Aqi
		{
			public HistoricDataTypeDbl pm25 { get; set; }
		}

		internal class HistoricDataPm10Aqi
		{
			public HistoricDataTypeDbl pm10 { get; set; }
		}

		internal class HistoricData
		{
			public decimal? IndoorTemp { get; set; }
			public int? IndoorHum { get; set; }
			public decimal? Temp { get; set; }
			public decimal? DewPoint { get; set; }
			public decimal? FeelsLike { get; set; }
			public int? Humidity { get; set; }
			public decimal? RainRate { get; set; }
			public decimal? RainYear { get; set; }
			public decimal? WindSpd { get; set; }
			public decimal? WindGust { get; set; }
			public int? WindDir { get; set; }
			public decimal? Pressure { get; set; }
			public int? Solar { get; set; }
			public decimal? UVI { get; set; }
			public decimal? LightningDist { get; set; }
			public int? LightningCount { get; set; }
			public decimal?[] ExtraTemp { get; set; }
			public int?[] ExtraHumidity { get; set; }
			public int?[] SoilMoist { get; set; }
			public decimal?[] UserTemp { get; set; }
			public int?[] LeafWetness { get; set; }
			public decimal?[] pm25 { get; set; }
			public decimal? AqiComboPm25 { get; set; }
			public decimal? AqiComboPm10 { get; set; }
			public decimal? AqiComboTemp { get; set; }
			public int? AqiComboHum { get; set; }
			public int? CO2pm2p5 { get; set; }
			public int? CO2pm2p5hr24 { get; set; }
			public int? IndoorCo2 { get; set; }
			public int? IndoorCo2hr24 { get; set; }

			public HistoricData()
			{
				pm25 = new decimal?[5];
				ExtraTemp = new decimal?[9];
				ExtraHumidity = new int?[9];
				SoilMoist = new int?[9];
				UserTemp = new decimal?[9];
				LeafWetness = new int?[9];
			}
		}




		private class CurrentData
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

			[IgnoreDataMember]
			public CurrentSensorValDbl Event { get; set; }

			[DataMember(Name = "event")]
			public CurrentSensorValDbl EventVal
			{
				get => Event;
				set { Event = value; }
			}


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

			[IgnoreDataMember]
			public int Avg24h { get; set; }

			[DataMember(Name = "24_hours_average")]
			public int Average
			{
				get => Avg24h;
				set { Avg24h = value; }
			}
		}

		internal class CurrentPm25
		{
			public CurrentSensorValInt real_time_aqi { get; set; }
			public CurrentSensorValInt pm25 { get; set; }

			[IgnoreDataMember]
			public CurrentSensorValInt Avg24h { get; set; }

			[DataMember(Name = "24_hours_aqi")]
			public CurrentSensorValInt AvgVal
			{
				get => Avg24h;
				set { Avg24h = value; }
			}
		}

		internal class CurrentPm10
		{
			public CurrentSensorValInt real_time_aqi { get; set; }
			public CurrentSensorValInt pm10 { get; set; }

			[IgnoreDataMember]
			public CurrentSensorValInt Avg24h { get; set; }

			[DataMember(Name = "24_hours_aqi")]
			public CurrentSensorValInt AvgVal
			{
				get => Avg24h;
				set { Avg24h = value; }
			}
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
	}
}
