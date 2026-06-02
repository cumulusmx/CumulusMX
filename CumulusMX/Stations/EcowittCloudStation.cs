using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace CumulusMX.Stations
{
	internal class EcowittCloudStation : WeatherStation
	{
		private readonly WeatherStation station;
		private readonly EcowittApi ecowittApi;
		private int maxArchiveRuns = 1;
		private Task liveTask;
		private readonly bool mainStation;
		private readonly int stationIndex;
		private string deviceModel;
		private Version deviceFirmware;
		private int lastHour = -1;

		public EcowittCloudStation(Cumulus cumulus, int id) : base(cumulus, id)
		{
			this.station = station ?? this;

			mainStation = station == null;
			stationIndex = mainStation ? 0 : 1;

			if (mainStation)
			{
				cumulus.LogMessage("Creating Ecowitt Cloud Station");
			}
			else
			{
				cumulus.LogMessage("Creating Extra Sensors - Ecowitt Cloud");
			}

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogErrorMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
				return;
			}

			// Do not set these if we are only using extra sensors
			if (mainStation)
			{
				// cloud provides 10 min average wind speeds
				cumulus.StationOptions.CalcuateAverageWindSpeed = true;

				// GW1000 does not provide an interval gust value, it gives us a 2 minute high
				// The speed is the average for that update
				// Therefore we need to force using the speed for the average calculation
				cumulus.StationOptions.UseSpeedForAvgCalc = true;
				// also use it for the Latest value
				cumulus.StationOptions.UseSpeedForLatest = false;

				// does send DP
				cumulus.StationOptions.CalculatedDP = false;
				// does not send Wind Chill
				cumulus.StationOptions.CalculatedWC = true;
				// does not provide a forecast, force MX to provide it
				if (cumulus.ForecastSource == 0)
				{
					cumulus.ForecastSource = 1;
				}
				// does not provide pressure trend strings
				cumulus.StationOptions.UseCumulusPresstrendstr = true;

				if (cumulus.SensorMaps.PrimaryTempHum == 0)
				{
					// We are using the primary T/H sensor
					cumulus.LogMessage("Using the default outdoor temp/hum sensor data");
				}
				else if (cumulus.SensorMaps.PrimaryTempHum == 99)
				{
					cumulus.LogMessage("Overriding the default outdoor temp/hum data with the internal sensor");
					cumulus.StationOptions.CalculatedDP = true;
				}
				else
				{
					// We are not using the primary T/H sensor
					cumulus.LogMessage("Overriding the default outdoor temp/hum data with Extra temp/hum sensor #" + cumulus.SensorMaps.PrimaryTempHum);
				}

				if (cumulus.Gw1000PrimaryRainSensor == 0)
				{
					// We are using the traditional rain tipper
					cumulus.LogMessage("Using the default traditional rain sensor data");
				}
				else
				{
					cumulus.LogMessage("Using the piezo rain sensor data");
				}

				if (cumulus.SensorMaps.PrimaryIndoorTempHum == 0)
				{
					// We are using the primary indoor T/H sensor
					cumulus.LogMessage("Using the default indoor temp/hum sensor data");
				}
				else
				{
					// We are not using the primary indoor T/H sensor
					cumulus.LogMessage("Overriding the default indoor temp/hum data with Extra temp/hum sensor #" + cumulus.SensorMaps.PrimaryIndoorTempHum);
				}

				DataTimeoutMins = cumulus.EcowittCloudDataUpdateInterval + 2;
			}

			SetSoilMoistUnits(stationIndex, "%");
			SetAirQualUnits(stationIndex, "µg/m³");
			SetLeafWetUnits(stationIndex, "%");

			ecowittApi = new EcowittApi(cumulus, this, mainStation);

			// Only perform the Start-up if we are a proper Stations, not a Extra Sensor
			try
			{
				if (mainStation)
				{
					Task.Run(getAndProcessHistoryData);
					var retVal = ecowittApi.GetStationList(true, cumulus.EcowittMacAddress, Program.ExitSystemToken);
					if (retVal.Length == 2 && !retVal[1].StartsWith("EasyWeather"))
					{
						// EasyWeather seems to contain the WiFi version
						deviceFirmware = new Version(retVal[0]);
						deviceModel = retVal[1];
					}
				}
				else
				{
					// see if we have a camera attached
					var retVal = ecowittApi.GetStationList(cumulus.SensorMaps.Camera == stationIndex, cumulus.EcowittMacAddress, Program.ExitSystemToken);
					if (retVal.Length == 2 && !retVal[1].StartsWith("EasyWeather"))
					{
						// EasyWeather seems to contain the WiFi version
						deviceFirmware = new Version(retVal[0]);
						deviceModel = retVal[1];
					}
				}

				_ = CheckAvailableFirmware();
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Error checking firmware version");
			}
		}

		public override void Start()
		{
			if (mainStation)
			{
				// just incase we did not catch-up any history
				DoDayResetIfNeeded();
				DoTrendValues(DateTime.Now);

				cumulus.LogMessage("Starting Ecowitt Cloud station");
			}
			else
			{
				cumulus.LogMessage("Starting Ecowitt Cloud Extra Sensors station");
			}


			if (mainStation)
			{
				cumulus.StartTimersAndSensors();
			}

			// main data task
			liveTask = Task.Run(() =>
			{

				var delay = 0;
				var nextFetch = DateTime.MinValue;

				while (!Program.ExitSystemToken.IsCancellationRequested)
				{
					if (DateTime.UtcNow >= nextFetch && !DayResetInProgress)
					{
						try
						{

							var data = ecowittApi.GetCurrentData(ref delay, Program.ExitSystemToken);

							if (data != null)
							{
								ProcessCurrentData(data, Program.ExitSystemToken);
							}
							else
							{
								cumulus.LogDebugMessage($"EcowittCloud: No new data to process");
							}
							cumulus.LogDebugMessage($"EcowittCloud: Waiting {delay} seconds before next update");
							nextFetch = DateTime.UtcNow.AddSeconds(delay);

							var hour = DateTime.Now.Hour;
							if (lastHour != hour)
							{
								lastHour = hour;

								if (hour == 13)
								{
									try
									{
										var retVal = ecowittApi.GetStationList(stationIndex == cumulus.SensorMaps.Camera, cumulus.EcowittMacAddress, Program.ExitSystemToken);
										if (retVal.Length == 2 && !retVal[1].StartsWith("EasyWeather"))
										{
											// EasyWeather seems to contain the WiFi version
											deviceFirmware = new Version(retVal[0]);
											deviceModel = retVal[1];
											GW1000FirmwareVersion = retVal[0];
										}
										_ = CheckAvailableFirmware();
									}
									catch (Exception ex)
									{
										cumulus.LogExceptionMessage(ex, "Error decoding firmware version");
									}
								}
							}
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, "Error running Ecowitt Cloud station");
							nextFetch = DateTime.UtcNow.AddMinutes(1);
						}
					}

					_ = Program.ExitSystemToken.WaitHandle.WaitOne(1000);
				}
			}, Program.ExitSystemToken);
		}

		public override void Stop()
		{
			if (mainStation)
			{
				StopMinuteTimer();
			}

			cumulus.LogMessage($"Ecowitt Cloud {(mainStation ? "Extra Sensors" : "")} station Stopped");
		}


		public override void getAndProcessHistoryData()
		{
			Cumulus.SyncInit.Wait();

			var archiveRun = 0;

			try
			{
				do
				{
					GetHistoricData();
					archiveRun++;
				} while (archiveRun < maxArchiveRuns && !Program.ExitSystemToken.IsCancellationRequested);
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Exception occurred reading archive data.");
			}

			_ = Cumulus.SyncInit.Release();

			StartLoop();
		}

		public override string GetEcowittCameraUrl(string mac)
		{
			if (stationIndex == cumulus.SensorMaps.Camera)
			{
				if (string.IsNullOrEmpty(mac))
				{
					cumulus.LogWarningMessage("GetEcowittCameraUrl: Warning - URL requested, but no camera MAC address is configured");
				}
				else
				{
					try
					{
						return ecowittApi.GetCurrentCameraImageUrl(mac, Program.ExitSystemToken);
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "Error getting Ecowitt Camera URL");
					}
				}
			}

			return string.Empty;
		}

		public override string GetEcowittVideoUrl(string mac)
		{
			if (stationIndex == cumulus.SensorMaps.Camera)
			{
				if (string.IsNullOrEmpty(mac))
				{
					cumulus.LogWarningMessage("GetEcowittCameraUrl: Warning - URL requested, but no camera MAC address is configured");
				}
				else
				{
					try
					{
						return ecowittApi.GetLastCameraVideoUrl(mac, Program.ExitSystemToken);
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "Error getting Ecowitt Video URL");
					}
				}
			}

			return string.Empty;
		}

		private void GetHistoricData()
		{
			cumulus.LogMessage("GetHistoricData: Starting Historic Data Process");

			// add one minute to avoid duplicating the last log entry
			var startTime = cumulus.LastUpdateTime.AddMinutes(1);
			var endTime = DateTime.Now;

			// The API call is limited to fetching 24 hours of data
			if ((endTime - startTime).TotalHours > 24.0)
			{
				// only fetch 24 hours worth of data, and schedule another run to fetch the rest
				endTime = startTime.AddHours(24);
			}

			_ = ecowittApi.GetHistoricData(startTime, endTime, Program.ExitSystemToken);

			if ((DateTime.Now - cumulus.LastUpdateTime.AddMinutes(1)).TotalMinutes > Cumulus.logints[cumulus.DataLogInterval] + 1)
			{
				maxArchiveRuns++;
			}

		}

		private void ProcessCurrentData(EcowittApi.CurrentDataData data, CancellationToken token)
		{
			var batteryLow = false;
			token.ThrowIfCancellationRequested();

			try
			{
				// Only do the primary sensors if running as the main Stations
				if (mainStation)
				{
					// Outdoor temp/hum
					if (cumulus.SensorMaps.PrimaryTempHum == 0)
					{
						if (data.outdoor == null)
						{
							cumulus.LogErrorMessage("ProcessCurrentData: Error outdoor temp/humidity is missing");
						}
						else
						{
							try
							{
								var time = data.outdoor.temperature.time.LocalFromUnixTime();
								DoOutdoorHumidity(data.outdoor.humidity.value, time);
								DoOutdoorTemp(data.outdoor.temperature.value, time);
								DoOutdoorDewpoint(data.outdoor.dew_point.value, time);
								DoFeelsLike(time);
								DoApparentTemp(time);
								DoHumidex(time);
								DoCloudBaseHeatIndex(time);
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"ProcessCurrentData: Error in Outdoor temp data - {ex.Message}");
							}
						}
					}

					// Indoor temp/hum
					if (data.indoor == null)
					{
						cumulus.LogErrorMessage("ProcessCurrentData: Error indoor temp/humidity is missing");
					}
					else
					{
						try
						{
							// user has mapped the indoor sensor to the outdoor sensor
							if (cumulus.SensorMaps.PrimaryTempHum == 99)
							{
								var time = data.outdoor.temperature.time.LocalFromUnixTime();
								DoOutdoorHumidity(data.indoor.humidity.value, time);
								DoOutdoorTemp(data.indoor.temperature.value, time);
								DoOutdoorDewpoint(data.outdoor.dew_point.value, time);
								DoFeelsLike(time);
								DoApparentTemp(time);
								DoHumidex(time);
								DoCloudBaseHeatIndex(time);
							}

							if (cumulus.SensorMaps.PrimaryIndoorTempHum == 0)
							{
								DoIndoorTemp(data.indoor.temperature.value);
								DoIndoorHumidity(data.indoor.humidity.value);
							}
						}
						catch (Exception ex)
						{
							cumulus.LogErrorMessage($"ProcessCurrentData: Error in indoor data - {ex.Message}");
						}
					}

					// Pressure
					if (data.pressure == null)
					{
						cumulus.LogErrorMessage("ProcessCurrentData: Error pressure data is missing");
					}
					else
					{
						try
						{
							DoStationPressure(data.pressure.absolute.value);

							// leave cmx calculated SLP until the end as it depends on temperature

							if (!cumulus.StationOptions.CalculateSLP)
							{
								DoPressure(data.pressure.relative.value, data.pressure.relative.time.LocalFromUnixTime());
							}
						}
						catch (Exception ex)
						{
							cumulus.LogErrorMessage($"ProcessCurrentData: Error in pressure data - {ex.Message}");
						}
					}

					// Wind
					if (data.wind == null)
					{
						cumulus.LogErrorMessage("ProcessCurrentData: Error wind data is missing");
					}
					else
					{
						try
						{
							DoWind(data.wind.wind_gust.value, data.wind.wind_direction.value, data.wind.wind_speed.value, data.wind.wind_gust.time.LocalFromUnixTime());
							DoWindChill(-999, data.wind.wind_gust.time.LocalFromUnixTime());
						}
						catch (Exception ex)
						{
							cumulus.LogErrorMessage($"ProcessCurrentData: Error in wind data - {ex.Message}");
						}
					}

					// Rain
					if (cumulus.Gw1000PrimaryRainSensor == 0) // tipper
					{
						if (data.rainfall == null)
						{
							cumulus.LogErrorMessage("ProcessCurrentData: Error tipper rainfall is missing");
						}
						else
						{
							try
							{
								DoRain(data.rainfall.yearly.value, data.rainfall.rain_rate.value, data.rainfall.yearly.time.LocalFromUnixTime());
								MetData.StormRain = data.rainfall.Event.value;
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"ProcessCurrentData: Error in tipper rainfall data - {ex.Message}");
							}
						}
					}
					else // piezo
					{
						if (data.rainfall_piezo == null)
						{
							cumulus.LogErrorMessage("ProcessCurrentData: Error piezo rainfall is missing");
						}
						else
						{
							try
							{
								DoRain(data.rainfall_piezo.yearly.value, data.rainfall_piezo.rain_rate.value, data.rainfall_piezo.yearly.time.LocalFromUnixTime());
								MetData.StormRain = data.rainfall_piezo.Event.value;
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"ProcessCurrentData: Error in piezo rainfall data - {ex.Message}");
							}
						}
					}
				}

				// Solar
				if (data.solar_and_uvi != null)
				{
					try
					{
						if (data.solar_and_uvi.solar != null && (mainStation ? 0 : 1) == cumulus.SensorMaps.Solar)
							station.DoSolarRad((int) data.solar_and_uvi.solar.value, data.solar_and_uvi.solar.time.LocalFromUnixTime());

						if (data.solar_and_uvi.uvi != null && (mainStation ? 0 : 1) == cumulus.SensorMaps.UV)
							station.DoUV(data.solar_and_uvi.uvi.value, data.solar_and_uvi.solar.time.LocalFromUnixTime());
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in solar data - {ex.Message}");
					}
				}

				// Extra Temperature
				try
				{
					ProcessExtraTempHum(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in extra temperature data - {ex.Message}");
				}

				// === Soil/Water Temp ===
				try
				{
					ProcessUserTemps(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in user temperature data - {ex.Message}");
				}

				// === Soil Moisture ===
				try
				{
					ProcessSoilMoist(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Soil moisture data - {ex.Message}");
				}

				// === Soil Moisture EC ===
				try
				{
					ProcessSoilMoistEc(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Soil moisture data - {ex.Message}");
				}

				// === Leaf Wetness ===
				try
				{
					ProcessLeafWetness(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Leaf wetness data - {ex.Message}");
				}

				// === Air Quality ===
				try
				{
					ProcessAirQuality(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Air Quality data - {ex.Message}");
				}

				// === CO₂ ===
				try
				{
					ProcessCo2(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in CO₂ data - {ex.Message}");
				}

				// === Lightning ===
				try
				{
					ProcessLightning(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Lightning data - {ex.Message}");
				}

				// === Leak ===
				try
				{
					ProcessLeak(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Leak data - {ex.Message}");
				}

				// === Batteries ===
				try
				{
					ProcessBatteries(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Battery data - {ex.Message}");
				}

				// === Camera ===
				/*
				try
				{
					if (data.camera != null && data.camera.photo != null)
					{
						EcowittCameraUrl[0] = data.camera.photo.url;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Camera data - {ex.Message}");
				}
				*/

				// === LDS ===
				try
				{
					ProcessLDS(data);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in LDS data - {ex.Message}");
				}

				// === BGT ===
				try
				{
					if (stationIndex == cumulus.SensorMaps.BlackGlobe)
					{
						if (data.black_globe_temperature != null)
						{
							station.DoBGT(data.black_globe_temperature.bgt.value, data.black_globe_temperature.bgt.time.LocalFromUnixTime());

							if (cumulus.StationOptions.CalculatedWBGT)
							{
								station.CalculateWBGT(data.black_globe_temperature.bgt.time.LocalFromUnixTime());
							}
							else
							{
								station.DoWBGT(data.black_globe_temperature.wbgt.value, data.black_globe_temperature.wbgt.time.LocalFromUnixTime());
							}
						}
						else
						{
							station.DoBGT(null, DateTime.Now);
							station.DoWBGT(null, DateTime.Now);
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in BGT data - {ex.Message}");
				}

				cumulus.BatteryLowAlarm.Triggered = batteryLow;

				if (mainStation)
				{
					if (cumulus.StationOptions.CalculateSLP)
					{
						var slp = MeteoLib.GetSeaLevelPressure(ConvertUnits.AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToHpa(MetData.StationPressure), ConvertUnits.UserTempToC(MetData.Temperature), cumulus.Latitude);
						DoPressure(ConvertUnits.PressMBToUser(slp), data.pressure.absolute.time.LocalFromUnixTime());
					}

					station.DoForecast(string.Empty, false);

					var updateTime = (data.pressure == null ? data.outdoor.temperature.time : data.pressure.absolute.time).LocalFromUnixTime();
					station.UpdateStatusPanel(updateTime.ToUniversalTime());
					station.UpdateMQTT();
				}
			}
			catch (OperationCanceledException)
			{
				// asked to cancel
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "ProcessCurrentData: Exception");
			}
		}

		private void ProcessExtraTempHum(EcowittApi.CurrentDataData data)
		{
			for (var i = 1; i <= 8; i++)
			{
				if (data["temp_and_humidity_ch" + i] != null && (mainStation ? 0 : 1) == cumulus.SensorMaps.ExtraTempHum[i - 1])
				{
					var sensor = (EcowittApi.CurrentTempHum) data["temp_and_humidity_ch" + i];
					ApplyExtraTempHum(i, sensor.temperature.value, sensor.humidity, sensor.temperature.time);

				}
			}
		}

		private void ApplyExtraTempHum(int chan, double? temp, EcowittApi.CurrentSensorValInt hum, long ts)
		{
			if (temp.HasValue)
			{
				WeatherStation.DoExtraTemp(temp, chan);

				// Not all sensor types have humidity
				if (hum != null)
				{
					WeatherStation.DoExtraHum(hum.value, chan);

					if (cumulus.SensorMaps.PrimaryTempHum == chan)
					{
						station.DoOutdoorHumidity(hum.value, ts.LocalFromUnixTime());
					}

					if (cumulus.SensorMaps.PrimaryIndoorTempHum == chan)
					{
						station.DoIndoorHumidity(hum.value);
					}

					var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(temp.Value), hum.value);
					MetData.ExtraDewPoint[chan] = ConvertUnits.TempCToUser(dp);
				}

				if (cumulus.SensorMaps.PrimaryTempHum == chan && temp.HasValue)
				{
					station.DoOutdoorTemp(temp.Value, ts.LocalFromUnixTime());
				}

				if (cumulus.SensorMaps.PrimaryIndoorTempHum == chan && temp.HasValue)
				{
					station.DoIndoorTemp(temp.Value);
				}
			}
		}

		private void ProcessUserTemps(EcowittApi.CurrentDataData data)
		{
			for (var i = 1; i <= 8; i++)
			{
				if (stationIndex == cumulus.SensorMaps.UserTemp[i - 1])
				{
					ApplyUserTemp(i, data["temp_ch" + i] == null ? null : ((EcowittApi.CurrentTemp) data["temp_ch" + i]).temperature.value);
				}
			}
		}

		private void ApplyUserTemp(int chan, double? temp)
		{
			if (cumulus.EcowittMapWN34[chan] == 0)
			{
				WeatherStation.DoUserTemp(temp, chan);
			}
			else
			{
				WeatherStation.DoSoilTemp(temp, cumulus.EcowittMapWN34[chan]);
			}
		}


		private void ProcessSoilMoist(EcowittApi.CurrentDataData data)
		{
			for (var i = 1; i <= 8; i++)
			{
				if (stationIndex == cumulus.SensorMaps.SoilMoist[i - 1])
				{
					var sensor = (EcowittApi.CurrentSoil) data["soil_ch" + i];
					WeatherStation.DoSoilMoisture(sensor == null ? null : sensor.soilmoisture.value, i);
				}
			}
		}

		private void ProcessSoilMoistEc(EcowittApi.CurrentDataData data)
		{
			for (var i = 1; i <= 16; i++)
			{
				if (stationIndex == cumulus.SensorMaps.SoilEc[i - 1])
				{
					var sensor = (EcowittApi.CurrentSoilEc) data["ch_soil_ec_temp_hum" + i];

					WeatherStation.DoSoilMoisture(sensor == null ? null : sensor.soilmoisture.value, i);
					WeatherStation.DoSoilTemp(sensor == null ? null : sensor.temperature.value, i);
					WeatherStation.DoSoilEc(sensor == null ? null : sensor.ec.value, i);
				}
			}
		}

		private void ProcessLeafWetness(EcowittApi.CurrentDataData data)
		{
			for (var i = 1; i <= 8; i++)
			{
				if (stationIndex == cumulus.SensorMaps.LeafWet[0])
				{
					var sensor = (EcowittApi.CurrentLeaf) data["leaf_ch" + i];
					station.DoLeafWetness(sensor == null ? null : sensor.leaf_wetness.value, i);
				}
			}
		}

		private void ProcessAirQuality(EcowittApi.CurrentDataData data)
		{
			for (var i = 1; i <= 4; i++)
			{
				if (stationIndex == cumulus.SensorMaps.AirQual[0])
				{
					var sensor = (EcowittApi.CurrentPm25) data["pm25_ch" + i];
					station.DoAirQuality(sensor == null ? null : sensor.pm25.value, i);
					//Stations.DoAirQualityAvg(sensor == null ? null : sensor.AqiAvg24h.value, i)
				}
			}
		}

		private void ProcessCo2(EcowittApi.CurrentDataData data)
		{
			// indoor overrides the combo
			if (stationIndex == cumulus.SensorMaps.CO2)
			{
				if (data.indoor_co2 != null)
				{
					MetData.CO2 = data.indoor_co2.co2.value;
					MetData.CO2_24h = data.indoor_co2.Avg24h.value;
				}
				else if (data.co2_aqi_combo != null)
				{
					MetData.CO2 = data.co2_aqi_combo.co2.value;
					MetData.CO2_24h = data.co2_aqi_combo.Avg24h.value;
				}
				else
				{
					MetData.CO2 = null;
				}

				if (data.pm25_aqi_combo != null)
				{
					MetData.CO2_pm2p5 = data.pm25_aqi_combo.pm25.value;
					MetData.CO2_pm2p5_aqi = station.GetAqi(AqMeasure.pm2p5, MetData.CO2_pm2p5);
					if (data.pm25_aqi_combo.AqiAvg24h != null)
					{
						// kludge, convert US EPA to average PM2.5
						MetData.CO2_pm2p5_24h = AirQualityIndices.US_EPApm2p5toPm(data.pm25_aqi_combo.AqiAvg24h.value);
						MetData.CO2_pm2p5_24h_aqi = station.GetAqi(AqMeasure.pm2p5, MetData.CO2_pm2p5_24h);
					}
					else
					{
						MetData.CO2_pm2p5_24h = null;
					}
				}
				else
				{
					MetData.CO2_pm2p5 = null;
					MetData.CO2_pm2p5_24h = null;
				}


				if (data.pm10_aqi_combo != null)
				{
					MetData.CO2_pm10 = data.pm10_aqi_combo.pm10.value;
					MetData.CO2_pm10_aqi = station.GetAqi(AqMeasure.pm10, MetData.CO2_pm10);
					if (data.pm10_aqi_combo.AqiAvg24h != null)
					{
						// kludge, convert US EPA to average PM10
						MetData.CO2_pm10_24h = AirQualityIndices.US_EPApm10toPm(data.pm10_aqi_combo.AqiAvg24h.value);
						MetData.CO2_pm10_24h_aqi = station.GetAqi(AqMeasure.pm10, MetData.CO2_pm10_24h);
					}
					else
					{
						MetData.CO2_pm10_24h = null;
					}
				}
				else
				{
					MetData.CO2_pm10 = null;
					MetData.CO2_pm10_24h = null;
				}

				if (data.t_rh_aqi_combo != null)
				{
					MetData.CO2_temperature = data.t_rh_aqi_combo.temperature.value;
					MetData.CO2_humidity = data.t_rh_aqi_combo.humidity.value;
				}
				else
				{
					MetData.CO2_temperature = null;
					MetData.CO2_humidity = null;
				}
			}
		}

		private void ProcessLightning(EcowittApi.CurrentDataData data)
		{
			if (stationIndex == cumulus.SensorMaps.Lightning)
			{
				if (data.lightning != null && data.lightning.distance != null && data.lightning.distance.value != 255)
				{
					// add the incremental strikes to the total, allow for the counter being reset
					if (data.lightning.count.value > MetData.LightningCounter)
					{
						MetData.LightningStrikesToday += data.lightning.count.value - MetData.LightningCounter;
						cumulus.LogDebugMessage($"Lightning: Adding {data.lightning.count.value - MetData.LightningCounter} strikes, total = {MetData.LightningStrikesToday} strikes today");
					}
					MetData.LightningCounter = data.lightning.count.value;
					MetData.LightningDistance = ConvertUnits.KmtoUserUnits(data.lightning.distance.value);

					var tim = data.lightning.distance.time.LocalFromUnixTime();

					if (tim > MetData.LightningTime)
					{
						MetData.LightningTime = tim;
					}
				}
			}
		}

		private void ProcessLeak(EcowittApi.CurrentDataData data)
		{
			if (data.water_leak != null)
			{
				for (var i = 1; i <= 4; i++)
				{
					var sensor = (EcowittApi.CurrentSensorValInt) data.water_leak["water_leak.leak_ch" + i];
					WeatherStation.DoLeakSensor(sensor == null ? null : sensor.value, i);
				}
			}
			else
			{
				for (var i = 0; i < 4; i++)
				{
					if (stationIndex == cumulus.SensorMaps.Leak[i])
						WeatherStation.DoLeakSensor(null, i + 1);
				}
			}
		}

		private void ProcessLDS(EcowittApi.CurrentDataData data)
		{
			if (stationIndex == cumulus.SensorMaps.LaserDist[0])
			{
				if (data.ch_lds1 != null)
				{
					ApplyLDS(data.ch_lds1.air_ch1.value, data.ch_lds1.depth_ch1.value, data.ch_lds1.air_ch1.unit, 1, data.ch_lds1.air_ch1.time.LocalFromUnixTime());
				}
				else
				{
					ApplyLDS(null, null, string.Empty, 1, DateTime.Now);
				}
			}

			if (stationIndex == cumulus.SensorMaps.LaserDist[1])
			{
				if (data.ch_lds2 != null)
				{
					ApplyLDS(data.ch_lds2.air_ch2.value, data.ch_lds2.depth_ch2.value, data.ch_lds2.air_ch2.unit, 2, data.ch_lds2.air_ch2.time.LocalFromUnixTime());
				}
				else
				{
					ApplyLDS(null, null, string.Empty, 2, DateTime.Now);
				}
			}

			if (stationIndex == cumulus.SensorMaps.LaserDist[2])
			{
				if (data.ch_lds3 != null)
				{
					ApplyLDS(data.ch_lds3.air_ch3.value, data.ch_lds3.depth_ch3.value, data.ch_lds3.air_ch3.unit, 3, data.ch_lds3.air_ch3.time.LocalFromUnixTime());
				}
				else
				{
					ApplyLDS(null, null, string.Empty, 3, DateTime.Now);
				}
			}

			if (stationIndex == cumulus.SensorMaps.LaserDist[3])
			{
				if (data.ch_lds4 != null)
				{
					ApplyLDS(data.ch_lds4.air_ch4.value, data.ch_lds4.depth_ch4.value, data.ch_lds4.air_ch4.unit, 4, data.ch_lds3.air_ch3.time.LocalFromUnixTime());
				}
				else
				{
					ApplyLDS(null, null, string.Empty, 2, DateTime.Now);
				}

			}
		}

		private void ApplyLDS(double? dist, double? depth, string unit, int chan, DateTime dataTime)
		{
			if (dist.HasValue)
			{
				dist = unit switch
				{
					"mm" => ConvertUnits.LaserMmToUser(dist),
					"cm" => ConvertUnits.LaserMmToUser(dist * 10),
					"in" => ConvertUnits.LaserInchesToUser(dist),
					"ft" => ConvertUnits.LaserInchesToUser(dist * 12),
					_ => dist
				};
			}

			station.DoLaserDistance(dist, chan, dataTime);

			if (cumulus.LaserDepthBaseline[chan] == -1)
			{
				// MX is NOT calculating depth

				if (depth.HasValue)
				{
					depth = unit switch
					{
						"mm" => ConvertUnits.LaserMmToUser(depth),
						"cm" => ConvertUnits.LaserMmToUser(depth * 10),
						"in" => ConvertUnits.LaserInchesToUser(depth),
						"ft" => ConvertUnits.LaserInchesToUser(depth * 12),
						_ => depth
					};
				}

				station.DoLaserDepth(depth, chan, dataTime);
			}
			// else DoLaserDistance() calcs the depth

		}

		private void ProcessBatteries(EcowittApi.CurrentDataData data)
		{
			var lowBatt = false;

			if (data.battery != null)
			{
				LowBatteryDevices.Clear();

				if (data.battery.t_rh_p_sensor != null && data.battery.t_rh_p_sensor.value == 1) // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("THP=LOW");
				}
				if (data.battery.ws1900_console != null && data.battery.ws1900_console.value < 1.2)              // volts - val ???
				{
					lowBatt = true;
					LowBatteryDevices.Add("WS1900=" + data.battery.ws1900_console.value + "V");
				}
				if (data.battery.ws1800_console != null && data.battery.ws1800_console.value < 1.2)              // volts - val ???
				{
					lowBatt = true;
					LowBatteryDevices.Add("WS1800=" + data.battery.ws1800_console.value + "V");
				}
				if (data.battery.ws6006_console != null && data.battery.ws6006_console.value < 15)               // %  val ???
				{
					lowBatt = true;
					LowBatteryDevices.Add("WS6006=" + data.battery.ws6006_console.value / 10 + "V");
				}
				if (data.battery.console != null && data.battery.console.value < 2.4)                            // volts  val ???
				{
					lowBatt = true;
					LowBatteryDevices.Add("Console=" + data.battery.console.value + "V");
				}
				if (data.battery.outdoor_t_rh_sensor != null && data.battery.outdoor_t_rh_sensor.value == 1)     // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("OutdoorTH=LOW");
				}
				if (data.battery.wind_sensor != null && data.battery.wind_sensor.value < 1.2)                    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("WindSensor=" + data.battery.wind_sensor.value + "V");
				}
				if (data.battery.haptic_array_battery != null && data.battery.haptic_array_battery.value < 2.4)  // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Haptic=" + data.battery.haptic_array_battery.value + "V");
				}
				//lowBatt = lowBatt || (data.battery.haptic_array_capacitor != null && data.battery.haptic_array_capacitor.value == 2.4); // volts
				if (data.battery.sonic_array != null && data.battery.sonic_array.value < 1.2)                    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Sonic=" + data.battery.sonic_array.value + "V");
				}
				if (data.battery.rainfall_sensor != null && data.battery.rainfall_sensor.value < 1.2)            // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Rainfall=" + data.battery.rainfall_sensor.value + "V");
				}
				if (data.battery.sensor_array != null && data.battery.sensor_array.value == 1)                   // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("SensorArray=LOW");
				}
				if (data.battery.lightning_sensor != null && data.battery.lightning_sensor.value == 1)           // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("Lightning=LOW");
				}
				if (data.battery.aqi_combo_sensor != null && data.battery.aqi_combo_sensor.value == 1)           // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("AQIcombo=LOW");
				}
				for (var i = 1; i <= 4; i++)
				{
					if (data.battery["water_leak_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValInt) data.battery["water_leak_sensor_ch" + i]).value == 1) // flag
					{
						lowBatt = true;
						LowBatteryDevices.Add($"Leak#{i}=LOW");
					}
				}
				for (var i = 1; i <= 4; i++)
				{
					if (data.battery["pm25_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValInt) data.battery["pm25_sensor_ch" + i]).value == 1)             // flag
					{
						lowBatt = true;
						LowBatteryDevices.Add($"pm2.5#{i}=LOW");
					}
				}
				for (var i = 1; i <= 8; i++)
				{
					if (data.battery["temp_humidity_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValInt) data.battery["temp_humidity_sensor_ch" + i]).value == 1)  // flag
					{
						lowBatt = true;
						LowBatteryDevices.Add($"TH#{i}=LOW");
					}
				}
				for (var i = 1; i <= 8; i++)
				{
					if (data.battery["soilmoisture_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValDbl) data.battery["soilmoisture_sensor_ch" + i]).value < 1.2)    // volts
					{
						lowBatt = true;
						LowBatteryDevices.Add($"SoilMoist#{i}=" + ((EcowittApi.CurrentSensorValDbl) data.battery["soilmoisture_sensor_ch" + i]).value + "V");
					}
				}
				for (var i = 1; i <= 8; i++)
				{
					if (data.battery["temperature_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValDbl) data.battery["temperature_sensor_ch" + i]).value < 1.2)      // volts
					{
						lowBatt = true;
						LowBatteryDevices.Add($"Temp#{i}=" + ((EcowittApi.CurrentSensorValDbl) data.battery["temperature_sensor_ch" + i]).value + "V");
					}
				}
				for (var i = 1; i <= 8; i++)
				{
					if (data.battery["leaf_wetness_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValDbl) data.battery["leaf_wetness_sensor_ch" + i]).value < 1.2)      // volts
					{
						lowBatt = true;
						LowBatteryDevices.Add($"Leaf#{i}=" + ((EcowittApi.CurrentSensorValDbl) data.battery["leaf_wetness_sensor_ch" + i]).value + "V");
					}
				}
				for (var i = 1; i <= 4; i++)
				{
					if (data.battery["ldsbatt_" + i] != null && ((EcowittApi.CurrentSensorValDbl) data.battery["ldsbatt_" + i]).value < 1.2)      // volts
					{
						lowBatt = true;
						LowBatteryDevices.Add($"LDS#{i}=" + ((EcowittApi.CurrentSensorValDbl) data.battery["ldsbatt_" + i]).value + "V");
					}
				}
				if (data.battery.bgt_sensor != null && data.battery.bgt_sensor.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("BGT=" + data.battery.bgt_sensor.value + "V");
				}
				for (var i = 1; i <= 16; i++)
				{
					if (data.battery["soilmoisture_ec_sensor_ch" + i] != null && ((EcowittApi.CurrentSensorValDbl) data.battery["soilmoisture_ec_sensor_ch" + i]).value < 1.2) // volts
					{
						lowBatt = true;
						LowBatteryDevices.Add($"SoilEC#{i}=" + ((EcowittApi.CurrentSensorValDbl) data.battery["soilmoisture_ec_sensor_ch" + i]).value + "V");
					}
				}

				cumulus.BatteryLowAlarm.LastMessage = string.Join(",", LowBatteryDevices);
				cumulus.BatteryLowAlarm.Triggered = lowBatt;
			}
		}

		private async Task CheckAvailableFirmware()
		{
			if (deviceModel == null)
				return;

			if (EcowittApi.SimpleSupportedModels.Contains(deviceModel[..6]))
			{
				var retVal = ecowittApi.GetSimpleLatestFirmwareVersion(deviceModel, Program.ExitSystemToken).Result;
				if (retVal != null)
				{
					var verVer = new Version(retVal[0]);
					if (deviceFirmware.CompareTo(verVer) < 0)
					{
						cumulus.FirmwareAlarm.LastMessage = $"A new firmware version is available: {retVal[0]}.\nChange log:\n{string.Join('\n', retVal[1].Split(';'))}";
						cumulus.FirmwareAlarm.Triggered = true;
						cumulus.LogWarningMessage($"FirmwareVersion: Latest Version {retVal[0]}, Change log:\n{string.Join('\n', retVal[1].Split(';'))}");
					}
					else
					{
						cumulus.LogDebugMessage($"FirmwareVersion: Already on the latest Version {retVal[0]}");
					}
				}
			}
			else
			{
				_ = await ecowittApi.GetLatestFirmwareVersion(deviceModel, cumulus.EcowittMacAddress, "V" + deviceFirmware.ToString(), Program.ExitSystemToken);
			}
		}
	}
}
