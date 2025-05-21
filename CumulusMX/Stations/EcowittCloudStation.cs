using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace CumulusMX
{
	internal class EcowittCloudStation : WeatherStation
	{
		private readonly WeatherStation station;
		private readonly EcowittApi ecowittApi;
		private int maxArchiveRuns = 1;
		private Task liveTask;
		private readonly bool mainStation;
		private string deviceModel;
		private Version deviceFirmware;
		private int lastHour = -1;

		public EcowittCloudStation(Cumulus cumulus, WeatherStation station = null) : base(cumulus, station != null)
		{
			this.station = station;

			mainStation = station == null;

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
				cumulus.UseCumulusForecast = true;
				// does not provide pressure trend strings
				cumulus.StationOptions.UseCumulusPresstrendstr = true;

				if (cumulus.Gw1000PrimaryTHSensor == 0)
				{
					// We are using the primary T/H sensor
					cumulus.LogMessage("Using the default outdoor temp/hum sensor data");
				}
				else if (cumulus.Gw1000PrimaryTHSensor == 99)
				{
					cumulus.LogMessage("Overriding the default outdoor temp/hum data with the internal sensor");
					cumulus.StationOptions.CalculatedDP = true;
				}
				else
				{
					// We are not using the primary T/H sensor
					cumulus.LogMessage("Overriding the default outdoor temp/hum data with Extra temp/hum sensor #" + cumulus.Gw1000PrimaryTHSensor);
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

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 0)
				{
					// We are using the primary indoor T/H sensor
					cumulus.LogMessage("Using the default indoor temp/hum sensor data");
				}
				else
				{
					// We are not using the primary indoor T/H sensor
					cumulus.LogMessage("Overriding the default indoor temp/hum data with Extra temp/hum sensor #" + cumulus.Gw1000PrimaryIndoorTHSensor);
				}

				DataTimeoutMins = 2;
			}

			if (mainStation || cumulus.ExtraSensorUseAQI)
			{
				cumulus.Units.AirQualityUnitText = "µg/m³";
			}
			if (mainStation)
			{
				Array.Fill(cumulus.Units.SoilMoistureUnitText, "%");
			}
			if (mainStation || cumulus.ExtraSensorUseLeafWet)
			{
				cumulus.Units.LeafWetnessUnitText = "%";
			}

			ecowittApi = new EcowittApi(cumulus, this);

			// Only perform the Start-up if we are a proper station, not a Extra Sensor
			try
			{
				if (mainStation)
				{
					Task.Run(getAndProcessHistoryData);
					var retVal = ecowittApi.GetStationList(true, cumulus.EcowittMacAddress, cumulus.cancellationToken);
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
					var retVal = ecowittApi.GetStationList(cumulus.ExtraSensorUseCamera, cumulus.EcowittMacAddress, cumulus.cancellationToken);
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
			// just incase we did not catch-up any history
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			cumulus.LogMessage("Starting Ecowitt Cloud station");

			if (station == null)
			{
				cumulus.StartTimersAndSensors();
			}

			// main data task
			liveTask = Task.Run(() =>
			{
				var delay = 0;
				var nextFetch = DateTime.MinValue;

				while (!cumulus.cancellationToken.IsCancellationRequested)
				{
					if (DateTime.Now >= nextFetch && !DayResetInProgress)
					{
						try
						{

							var data = ecowittApi.GetCurrentData(ref delay, cumulus.cancellationToken);

							if (data != null)
							{
								ProcessCurrentData(data, cumulus.cancellationToken);
							}
							else
							{
								cumulus.LogDebugMessage($"EcowittCloud: No new data to process");
							}
							cumulus.LogDebugMessage($"EcowittCloud: Waiting {delay} seconds before next update");
							nextFetch = DateTime.Now.AddSeconds(delay);

							var hour = DateTime.Now.Hour;
							if (lastHour != hour)
							{
								lastHour = hour;

								if (hour == 13)
								{
									try
									{
										var retVal = ecowittApi.GetStationList(mainStation || cumulus.ExtraSensorUseCamera, cumulus.EcowittMacAddress, cumulus.cancellationToken);
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
							nextFetch = DateTime.Now.AddMinutes(1);
						}
					}

					Thread.Sleep(1000);
				}
			}, cumulus.cancellationToken);
		}

		public override void Stop()
		{
			if (station == null)
			{
				StopMinuteTimer();
				liveTask.Wait();
				cumulus.LogMessage("Ecowitt Cloud station Stopped");
			}
		}


		public override void getAndProcessHistoryData()
		{
			Cumulus.SyncInit.Wait();

			int archiveRun = 0;

			try
			{
				do
				{
					GetHistoricData();
					archiveRun++;
				} while (archiveRun < maxArchiveRuns);
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Exception occurred reading archive data.");
			}

			_ = Cumulus.SyncInit.Release();

			StartLoop();
		}

		public override string GetEcowittCameraUrl()
		{
			if ((cumulus.ExtraSensorUseCamera ^ mainStation))
			{
				if (string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
				{
					cumulus.LogWarningMessage("GetEcowittCameraUrl: Warning - URL requested, but no camera MAC address is configured");
				}
				else
				{
					try
					{
						EcowittCameraUrl = ecowittApi.GetCurrentCameraImageUrl(EcowittCameraUrl, cumulus.cancellationToken);
						return EcowittCameraUrl;
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "Error getting Ecowitt Camera URL");
					}
				}
			}

			return string.Empty;
		}

		public override string GetEcowittVideoUrl()
		{
			if ((cumulus.ExtraSensorUseCamera ^ mainStation))
			{
				if (string.IsNullOrEmpty(cumulus.EcowittCameraMacAddress))
				{
					cumulus.LogWarningMessage("GetEcowittCameraUrl: Warning - URL requested, but no camera MAC address is configured");
				}
				else
				{
					try
					{
						EcowittVideoUrl = ecowittApi.GetLastCameraVideoUrl(EcowittVideoUrl, cumulus.cancellationToken);
						return EcowittVideoUrl;
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
				maxArchiveRuns++;
			}

			ecowittApi.GetHistoricData(startTime, endTime, cumulus.cancellationToken);
		}

		private void ProcessCurrentData(EcowittApi.CurrentDataData data, CancellationToken token)
		{
			bool batteryLow = false;
			var thisStation = mainStation ? this : station;
			token.ThrowIfCancellationRequested();

			try
			{
				// Only do the primary sensors if running as the main station
				if (mainStation)
				{
					// Outdoor temp/hum
					if (cumulus.Gw1000PrimaryTHSensor == 0)
					{
						if (data.outdoor == null)
						{
							cumulus.LogErrorMessage("ProcessCurrentData: Error outdoor temp/humidity is missing");
						}
						else
						{
							try
							{
								var time = Utils.FromUnixTime(data.outdoor.temperature.time);
								DoOutdoorTemp(data.outdoor.temperature.value, time);
								DoOutdoorHumidity(data.outdoor.humidity.value, time);
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
							if (cumulus.Gw1000PrimaryTHSensor == 99)
							{
								var time = Utils.FromUnixTime(data.outdoor.temperature.time);
								DoOutdoorTemp(data.indoor.temperature.value, time);
								DoOutdoorHumidity(data.indoor.humidity.value, time);
								DoOutdoorDewpoint(data.outdoor.dew_point.value, time);
								DoFeelsLike(time);
								DoApparentTemp(time);
								DoHumidex(time);
								DoCloudBaseHeatIndex(time);
							}

							if (cumulus.Gw1000PrimaryIndoorTHSensor == 0)
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
								DoPressure(data.pressure.relative.value, Utils.FromUnixTime(data.pressure.relative.time));
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
							DoWind(data.wind.wind_gust.value, data.wind.wind_direction.value, data.wind.wind_speed.value, Utils.FromUnixTime(data.wind.wind_gust.time));
							DoWindChill(-999, Utils.FromUnixTime(data.wind.wind_gust.time));
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
								DoRain(data.rainfall.yearly.value, data.rainfall.rain_rate.value, Utils.FromUnixTime(data.rainfall.yearly.time));
								StormRain = data.rainfall.Event.value;
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
								DoRain(data.rainfall_piezo.yearly.value, data.rainfall_piezo.rain_rate.value, Utils.FromUnixTime(data.rainfall_piezo.yearly.time));
								StormRain = data.rainfall_piezo.Event.value;
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"ProcessCurrentData: Error in piezo rainfall data - {ex.Message}");
							}
						}
					}
				}

				// Solar
				if ((mainStation || cumulus.ExtraSensorUseSolar) && data.solar_and_uvi != null)
				{
					try
					{
						if (data.solar_and_uvi.solar != null)
							DoSolarRad((int) data.solar_and_uvi.solar.value, Utils.FromUnixTime(data.solar_and_uvi.solar.time));

						if (data.solar_and_uvi.uvi != null)
							DoUV(data.solar_and_uvi.uvi.value, Utils.FromUnixTime(data.solar_and_uvi.solar.time));
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in solar data - {ex.Message}");
					}
				}

				// Extra Temperature
				if (mainStation || cumulus.ExtraSensorUseTempHum)
				{
					try
					{
						ProcessExtraTempHum(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in extra temperature data - {ex.Message}");
					}
				}

				// === Soil/Water Temp ===
				if (mainStation || cumulus.ExtraSensorUseUserTemp)
				{
					try
					{
						ProcessUserTemps(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in user temperature data - {ex.Message}");
					}
				}

				// === Soil Moisture ===
				if (mainStation || cumulus.ExtraSensorUseSoilMoist)
				{
					try
					{
						ProcessSoilMoist(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in Soil moisture data - {ex.Message}");
					}
				}

				// === Leaf Wetness ===
				if (mainStation || cumulus.ExtraSensorUseLeafWet)
				{
					try
					{
						ProcessLeafWetness(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in Leaf wetness data - {ex.Message}");
					}
				}

				// === Air Quality ===
				if (mainStation || cumulus.ExtraSensorUseAQI)
				{
					try
					{
						ProcessAirQuality(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in Air Quality data - {ex.Message}");
					}
				}

				// === CO₂ ===
				if (mainStation || cumulus.ExtraSensorUseCo2)
				{
					try
					{
						ProcessCo2(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in CO₂ data - {ex.Message}");
					}
				}

				// === Lightning ===
				if (mainStation || cumulus.ExtraSensorUseLightning)
				{
					try
					{
						ProcessLightning(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in Lightning data - {ex.Message}");
					}
				}

				// === Leak ===
				if (mainStation || cumulus.ExtraSensorUseLeak)
				{
					try
					{
						ProcessLeak(data, thisStation);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"ProcessCurrentData: Error in Leak data - {ex.Message}");
					}
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
				try
				{
					if (data.camera != null && data.camera.photo != null)
					{
						EcowittCameraUrl = data.camera.photo.url;
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in Camera data - {ex.Message}");
				}

				if (cumulus.StationOptions.CalculateSLP)
				{
					var slp = MeteoLib.GetSeaLevelPressure(ConvertUnits.AltitudeM(cumulus.Altitude), ConvertUnits.UserPressToHpa(StationPressure), ConvertUnits.UserTempToC(OutdoorTemperature), cumulus.Latitude);
					DoPressure(ConvertUnits.PressMBToUser(slp), Utils.FromUnixTime(data.pressure.absolute.time));
				}

				// === LDS ===
				try
				{
					ProcessLDS(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage($"ProcessCurrentData: Error in LDS data - {ex.Message}");
				}


				thisStation.DoForecast("", false);

				cumulus.BatteryLowAlarm.Triggered = batteryLow;


				var updateTime = Utils.FromUnixTime(data.pressure == null ? data.outdoor.temperature.time : data.pressure.absolute.time);
				thisStation.UpdateStatusPanel(updateTime);
				thisStation.UpdateMQTT();

				DataStopped = false;
				cumulus.DataStoppedAlarm.Triggered = false;
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

		private void ProcessExtraTempHum(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.temp_and_humidity_ch1 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 1)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch1.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch1.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 1)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch1.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch1.temperature.value, 1);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch1.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 1)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch1.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch1.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 1)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch1.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch1.humidity.value, 1);

					if (station.ExtraTemp[1].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[1].Value), station.ExtraHum[1].Value);
						station.ExtraDewPoint[1] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch2 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 2)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch2.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch2.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 2)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch2.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch2.temperature.value, 2);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch2.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 2)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch2.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch2.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 2)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch2.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch2.humidity.value, 2);

					if (station.ExtraTemp[2].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[2].Value), station.ExtraHum[2].Value);
						station.ExtraDewPoint[2] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch3 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 3)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch3.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch3.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 3)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch3.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch3.temperature.value, 3);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch3.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 3)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch3.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch3.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 3)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch3.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch3.humidity.value, 3);

					if (station.ExtraTemp[3].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[3].Value), station.ExtraHum[3].Value);
						station.ExtraDewPoint[3] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch4 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 4)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch4.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch4.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 4)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch4.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch4.temperature.value, 4);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch4.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 4)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch4.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch4.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 4)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch4.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch4.humidity.value, 4);

					if (station.ExtraTemp[4].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[4].Value), station.ExtraHum[4].Value);
						station.ExtraDewPoint[4] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch5 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 5)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch5.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch5.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 5)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch5.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch5.temperature.value, 5);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch5.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 5)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch5.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch5.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 5)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch5.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch5.humidity.value, 5);

					if (station.ExtraTemp[5].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[5].Value), station.ExtraHum[5].Value);
						station.ExtraDewPoint[5] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch6 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 6)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch6.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch6.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 6)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch6.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch6.temperature.value, 6);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch6.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 6)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch6.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch6.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 6)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch6.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch6.humidity.value, 6);

					if (station.ExtraTemp[6].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[6].Value), station.ExtraHum[6].Value);
						station.ExtraDewPoint[6] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch7 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 7)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch7.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch7.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 7)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch7.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch7.temperature.value, 7);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch7.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 7)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch7.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch7.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 7)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch7.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch7.humidity.value, 7);

					if (station.ExtraTemp[7].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[7].Value), station.ExtraHum[7].Value);
						station.ExtraDewPoint[7] = ConvertUnits.TempCToUser(dp);
					}
				}
			}

			if (data.temp_and_humidity_ch8 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 8)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch8.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch8.temperature.time));
				}

				if (cumulus.Gw1000PrimaryIndoorTHSensor == 8)
				{
					station.DoIndoorTemp(data.temp_and_humidity_ch8.temperature.value);
				}

				station.DoExtraTemp(data.temp_and_humidity_ch8.temperature.value, 8);

				// Not all sensor types have humidity
				if (data.temp_and_humidity_ch8.humidity != null)
				{
					if (cumulus.Gw1000PrimaryTHSensor == 8)
					{
						station.DoOutdoorHumidity(data.temp_and_humidity_ch8.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch8.humidity.time));
					}

					if (cumulus.Gw1000PrimaryIndoorTHSensor == 8)
					{
						station.DoIndoorHumidity(data.temp_and_humidity_ch8.humidity.value);
					}

					station.DoExtraHum(data.temp_and_humidity_ch8.humidity.value, 8);

					if (station.ExtraTemp[8].HasValue)
					{
						var dp = MeteoLib.DewPoint(ConvertUnits.UserTempToC(station.ExtraTemp[8].Value), station.ExtraHum[8].Value);
						station.ExtraDewPoint[8] = ConvertUnits.TempCToUser(dp);
					}
				}
			}
		}

		private void ProcessUserTemps(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.temp_ch1 != null)
			{
				if (cumulus.EcowittMapWN34[1] == 0)
				{
					station.DoUserTemp(data.temp_ch1.temperature.value, 1);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch1.temperature.value, cumulus.EcowittMapWN34[1]);
				}
			}

			if (data.temp_ch2 != null)
			{
				if (cumulus.EcowittMapWN34[2] == 0)
				{
					station.DoUserTemp(data.temp_ch2.temperature.value, 2);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch2.temperature.value, cumulus.EcowittMapWN34[2]);
				}
			}

			if (data.temp_ch3 != null)
			{
				if (cumulus.EcowittMapWN34[3] == 0)
				{
					station.DoUserTemp(data.temp_ch3.temperature.value, 3);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch3.temperature.value, cumulus.EcowittMapWN34[3]);
				}
			}

			if (data.temp_ch4 != null)
			{
				if (cumulus.EcowittMapWN34[4] == 0)
				{
					station.DoUserTemp(data.temp_ch4.temperature.value, 4);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch4.temperature.value, cumulus.EcowittMapWN34[4]);
				}
			}

			if (data.temp_ch5 != null)
			{
				if (cumulus.EcowittMapWN34[5] == 0)
				{
					station.DoUserTemp(data.temp_ch5.temperature.value, 5);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch5.temperature.value, cumulus.EcowittMapWN34[5]);
				}
			}

			if (data.temp_ch6 != null)
			{
				if (cumulus.EcowittMapWN34[6] == 0)
				{
					station.DoUserTemp(data.temp_ch6.temperature.value, 6);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch6.temperature.value, cumulus.EcowittMapWN34[6]);
				}
			}

			if (data.temp_ch7 != null)
			{
				if (cumulus.EcowittMapWN34[7] == 0)
				{
					station.DoUserTemp(data.temp_ch7.temperature.value, 7);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch7.temperature.value, cumulus.EcowittMapWN34[7]);
				}
			}

			if (data.temp_ch8 != null)
			{
				if (cumulus.EcowittMapWN34[8] == 0)
				{
					station.DoUserTemp(data.temp_ch8.temperature.value, 8);
				}
				else
				{
					station.DoSoilTemp(data.temp_ch8.temperature.value, cumulus.EcowittMapWN34[8]);
				}
			}
		}

		private void ProcessSoilMoist(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.soil_ch1 != null)
			{
				station.DoSoilMoisture(data.soil_ch1.soilmoisture.value, 1);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[0] = "%";
				}
			}

			if (data.soil_ch2 != null)
			{
				station.DoSoilMoisture(data.soil_ch2.soilmoisture.value, 2);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[1] = "%";
				}
			}

			if (data.soil_ch3 != null)
			{
				station.DoSoilMoisture(data.soil_ch3.soilmoisture.value, 3);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[2] = "%";
				}
			}

			if (data.soil_ch4 != null)
			{
				station.DoSoilMoisture(data.soil_ch4.soilmoisture.value, 4);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[3] = "%";
				}
			}

			if (data.soil_ch5 != null)
			{
				station.DoSoilMoisture(data.soil_ch5.soilmoisture.value, 5);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[4] = "%";
				}
			}

			if (data.soil_ch6 != null)
			{
				station.DoSoilMoisture(data.soil_ch6.soilmoisture.value, 6);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[5] = "%";
				}
			}

			if (data.soil_ch7 != null)
			{
				station.DoSoilMoisture(data.soil_ch7.soilmoisture.value, 7);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[6] = "%";
				}
			}

			if (data.soil_ch8 != null)
			{
				station.DoSoilMoisture(data.soil_ch8.soilmoisture.value, 8);
				if (!mainStation)
				{
					cumulus.Units.SoilMoistureUnitText[7] = "%";
				}
			}
		}

		private static void ProcessLeafWetness(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.leaf_ch1 != null)
			{
				station.DoLeafWetness(data.leaf_ch1.leaf_wetness.value, 1);
			}

			if (data.leaf_ch2 != null)
			{
				station.DoLeafWetness(data.leaf_ch2.leaf_wetness.value, 2);
			}

			if (data.leaf_ch3 != null)
			{
				station.DoLeafWetness(data.leaf_ch3.leaf_wetness.value, 3);
			}

			if (data.leaf_ch4 != null)
			{
				station.DoLeafWetness(data.leaf_ch4.leaf_wetness.value, 4);
			}

			if (data.leaf_ch5 != null)
			{
				station.DoLeafWetness(data.leaf_ch5.leaf_wetness.value, 5);
			}

			if (data.leaf_ch6 != null)
			{
				station.DoLeafWetness(data.leaf_ch6.leaf_wetness.value, 6);
			}

			if (data.leaf_ch7 != null)
			{
				station.DoLeafWetness(data.leaf_ch7.leaf_wetness.value, 7);
			}

			if (data.leaf_ch8 != null)
			{
				station.DoLeafWetness(data.leaf_ch8.leaf_wetness.value, 8);
			}
		}

		private static void ProcessAirQuality(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.pm25_ch1 != null)
			{
				station.DoAirQuality(data.pm25_ch1.pm25.value, 1);
				//station.DoAirQualityAvg(data.pm25_ch1.AqiAvg24h.value, 1)
			}

			if (data.pm25_ch2 != null)
			{
				station.DoAirQuality(data.pm25_ch2.pm25.value, 2);
				//station.DoAirQualityAvg(data.pm25_ch2.AqiAvg24h.value, 2)
			}
			if (data.pm25_ch3 != null)
			{
				station.DoAirQuality(data.pm25_ch3.pm25.value, 3);
				//station.DoAirQualityAvg(data.pm25_ch3.AqiAvg24h.value, 3)
			}
			if (data.pm25_ch4 != null)
			{
				station.DoAirQuality(data.pm25_ch4.pm25.value, 4);
				//station.DoAirQualityAvg(data.pm25_ch1.AqiAvg24h.value, 4)
			}
		}

		private static void ProcessCo2(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			// indoor overrides the combo
			if (data.indoor_co2 != null)
			{
				station.CO2 = data.indoor_co2.co2.value;
				station.CO2_24h = data.indoor_co2.Avg24h.value;
			}
			else if (data.co2_aqi_combo != null)
			{
				station.CO2 = data.co2_aqi_combo.co2.value;
				station.CO2_24h = data.co2_aqi_combo.Avg24h.value;
			}

			if (data.pm25_aqi_combo != null)
			{
				station.CO2_pm2p5 = data.pm25_aqi_combo.pm25.value;
				//station.CO2_pm2p5_24h = data.pm25_aqi_combo.AqiAvg24h.value
				station.CO2_pm2p5_aqi = station.GetAqi(WeatherStation.AqMeasure.pm2p5, station.CO2_pm2p5.Value);
			}

			if (data.pm10_aqi_combo != null)
			{
				station.CO2_pm10 = data.pm10_aqi_combo.pm10.value;
				//station.CO2_pm10_24h = data.pm10_aqi_combo.AqiAvg24h.value
				station.CO2_pm10_aqi = station.GetAqi(WeatherStation.AqMeasure.pm10, station.CO2_pm10.Value);
			}

			if (data.t_rh_aqi_combo != null)
			{
				station.CO2_temperature = data.t_rh_aqi_combo.temperature.value;
				station.CO2_humidity = data.t_rh_aqi_combo.humidity.value;
			}
		}

		private void ProcessLightning(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.lightning != null && data.lightning.distance != null && data.lightning.distance.value != 255)
			{
				// add the incremental strikes to the total, allow for the counter being reset
				station.LightningStrikesToday += data.lightning.count.value >= station.LightningCounter ? data.lightning.count.value - station.LightningCounter : data.lightning.count.value;
				station.LightningCounter = data.lightning.count.value;
				station.LightningDistance = ConvertUnits.KmtoUserUnits(data.lightning.distance.value);

				var tim = Utils.FromUnixTime(data.lightning.distance.time);

				if (tim > LightningTime)
				{
					station.LightningTime = tim;
				}
			}
		}

		private static void ProcessLeak(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.water_leak != null)
			{
				if (data.water_leak.leak_ch1 != null)
				{
					station.DoLeakSensor(data.water_leak.leak_ch1.value, 1);
				}

				if (data.water_leak.leak_ch2 != null)
				{
					station.DoLeakSensor(data.water_leak.leak_ch2.value, 2);
				}

				if (data.water_leak.leak_ch3 != null)
				{
					station.DoLeakSensor(data.water_leak.leak_ch3.value, 3);
				}

				if (data.water_leak.leak_ch4 != null)
				{
					station.DoLeakSensor(data.water_leak.leak_ch4.value, 4);
				}
			}
		}

		private static void ProcessLDS(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.ch_lds1 != null)
			{
				if (data.ch_lds1.air_ch1 != null)
				{
					decimal? dist = data.ch_lds1.air_ch1.unit switch
					{
						"mm" => ConvertUnits.LaserMmToUser(data.ch_lds1.air_ch1.value.Value),
						"cm" => ConvertUnits.LaserMmToUser(data.ch_lds1.air_ch1.value.Value / 10),
						"in" => ConvertUnits.LaserInchesToUser(data.ch_lds1.air_ch1.value.Value),
						"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds1.air_ch1.value.Value / 12),
						_ => data.ch_lds1.air_ch1.value
					};
					station.DoLaserDistance(dist, 1);
				}
				if (data.ch_lds1.depth_ch1 != null)
				{
					decimal? dist = data.ch_lds1.depth_ch1.unit switch
					{
						"mm" => ConvertUnits.LaserMmToUser(data.ch_lds1.depth_ch1.value.Value),
						"cm" => ConvertUnits.LaserMmToUser(data.ch_lds1.depth_ch1.value.Value),
						"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds1.depth_ch1.value.Value / 12),
						_ => data.ch_lds1.depth_ch1.value
					};
					station.DoLaserDepth(dist, 1);
				}

				if (data.ch_lds2 != null)
				{
					if (data.ch_lds2.air_ch2 != null)
					{
						decimal? dist = data.ch_lds2.air_ch2.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(data.ch_lds2.air_ch2.value.Value),
							"cm" => ConvertUnits.LaserMmToUser(data.ch_lds2.air_ch2.value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(data.ch_lds2.air_ch2.value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds2.air_ch2.value.Value / 12),
							_ => data.ch_lds2.air_ch2.value
						};
						station.DoLaserDistance(dist, 2);
					}
					if (data.ch_lds2.depth_ch2 != null)
					{
						decimal? dist = data.ch_lds2.depth_ch2.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(data.ch_lds2.depth_ch2.value.Value),
							"cm" => ConvertUnits.LaserMmToUser(data.ch_lds2.depth_ch2.value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(data.ch_lds2.depth_ch2.value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds2.depth_ch2.value.Value / 12),
							_ => data.ch_lds2.depth_ch2.value
						};
						station.DoLaserDepth(dist, 1);
					}
				}

				if (data.ch_lds3 != null)
				{
					if (data.ch_lds3.air_ch3 != null)
					{
						decimal? dist = data.ch_lds3.air_ch3.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(data.ch_lds3.air_ch3.value.Value),
							"cm" => ConvertUnits.LaserMmToUser(data.ch_lds3.air_ch3.value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(data.ch_lds3.air_ch3.value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds3.air_ch3.value.Value / 12),
							_ => data.ch_lds3.air_ch3.value
						};
						station.DoLaserDistance(dist, 2);
					}
					if (data.ch_lds3.depth_ch3 != null)
					{
						decimal? dist = data.ch_lds3.depth_ch3.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(data.ch_lds3.depth_ch3.value.Value),
							"cm" => ConvertUnits.LaserMmToUser(data.ch_lds3.depth_ch3.value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(data.ch_lds3.depth_ch3.value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds3.depth_ch3.value.Value / 12),
							_ => data.ch_lds3.depth_ch3.value
						};
						station.DoLaserDepth(dist, 1);
					}
				}

				if (data.ch_lds4 != null)
				{
					if (data.ch_lds4.air_ch4 != null)
					{
						decimal? dist = data.ch_lds4.air_ch4.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(data.ch_lds4.air_ch4.value.Value),
							"cm" => ConvertUnits.LaserMmToUser(data.ch_lds4.air_ch4.value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(data.ch_lds4.air_ch4.value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds4.air_ch4.value.Value / 12),
							_ => data.ch_lds4.air_ch4.value
						};
						station.DoLaserDistance(dist, 2);
					}
					if (data.ch_lds4.depth_ch4 != null)
					{
						decimal? dist = data.ch_lds4.depth_ch4.unit switch
						{
							"mm" => ConvertUnits.LaserMmToUser(data.ch_lds4.depth_ch4.value.Value),
							"cm" => ConvertUnits.LaserMmToUser(data.ch_lds4.depth_ch4.value.Value / 10),
							"in" => ConvertUnits.LaserInchesToUser(data.ch_lds4.depth_ch4.value.Value),
							"ft" => ConvertUnits.LaserInchesToUser(data.ch_lds4.depth_ch4.value.Value / 12),
							_ => data.ch_lds4.depth_ch4.value
						};
						station.DoLaserDepth(dist, 1);
					}
				}
			}
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
					LowBatteryDevices.Add("WS6006=" + (data.battery.ws6006_console.value / 10) + "V");
				}
				if (data.battery.console != null && data.battery.console.value < 2.4)                            // volts  val ???
				{
					lowBatt = true;
					LowBatteryDevices.Add("Console=" + (data.battery.console.value) + "V");
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
				if (data.battery.water_leak_sensor_ch1 != null && data.battery.water_leak_sensor_ch1.value == 1) // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leak#1=LOW");
				}
				if (data.battery.water_leak_sensor_ch2 != null && data.battery.water_leak_sensor_ch2.value == 1) // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leak#2=LOW");
				}
				if (data.battery.water_leak_sensor_ch3 != null && data.battery.water_leak_sensor_ch3.value == 1) // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leak#3=LOW");
				}
				if (data.battery.water_leak_sensor_ch4 != null && data.battery.water_leak_sensor_ch4.value == 1) // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leak#4=LOW");
				}
				if (data.battery.pm25_sensor_ch1 != null && data.battery.pm25_sensor_ch1.value == 1)             // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("pm2.5#1=LOW");
				}
				if (data.battery.pm25_sensor_ch2 != null && data.battery.pm25_sensor_ch2.value == 1)             // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("pm2.5#2=LOW");
				}
				if (data.battery.pm25_sensor_ch3 != null && data.battery.pm25_sensor_ch3.value == 1)             // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("pm2.5#3=LOW");
				}
				if (data.battery.pm25_sensor_ch4 != null && data.battery.pm25_sensor_ch4.value == 1)             // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("pm2.5#4=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch1 != null && data.battery.temp_humidity_sensor_ch1.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#1=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch2 != null && data.battery.temp_humidity_sensor_ch2.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#2=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch3 != null && data.battery.temp_humidity_sensor_ch3.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#3=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch4 != null && data.battery.temp_humidity_sensor_ch4.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#4=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch5 != null && data.battery.temp_humidity_sensor_ch5.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#5=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch6 != null && data.battery.temp_humidity_sensor_ch6.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#6=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch7 != null && data.battery.temp_humidity_sensor_ch7.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#7=LOW");
				}
				if (data.battery.temp_humidity_sensor_ch8 != null && data.battery.temp_humidity_sensor_ch8.value == 1)  // flag
				{
					lowBatt = true;
					LowBatteryDevices.Add("TH#8=LOW");
				}
				if (data.battery.soilmoisture_sensor_ch1 != null && data.battery.soilmoisture_sensor_ch1.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#1=" + data.battery.soilmoisture_sensor_ch1.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch2 != null && data.battery.soilmoisture_sensor_ch2.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#2=" + data.battery.soilmoisture_sensor_ch2.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch3 != null && data.battery.soilmoisture_sensor_ch3.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#3=" + data.battery.soilmoisture_sensor_ch3.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch4 != null && data.battery.soilmoisture_sensor_ch4.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#4=" + data.battery.soilmoisture_sensor_ch4.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch5 != null && data.battery.soilmoisture_sensor_ch5.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#5=" + data.battery.soilmoisture_sensor_ch5.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch6 != null && data.battery.soilmoisture_sensor_ch6.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#6=" + data.battery.soilmoisture_sensor_ch6.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch7 != null && data.battery.soilmoisture_sensor_ch7.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#7=" + data.battery.soilmoisture_sensor_ch7.value + "V");
				}
				if (data.battery.soilmoisture_sensor_ch8 != null && data.battery.soilmoisture_sensor_ch8.value < 1.2)    // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("SoilMoist#8=" + data.battery.soilmoisture_sensor_ch8.value + "V");
				}
				if (data.battery.temperature_sensor_ch1 != null && data.battery.temperature_sensor_ch1.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#1=" + data.battery.temperature_sensor_ch1.value + "V");
				}
				if (data.battery.temperature_sensor_ch2 != null && data.battery.temperature_sensor_ch2.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#2=" + data.battery.temperature_sensor_ch2.value + "V");
				}
				if (data.battery.temperature_sensor_ch3 != null && data.battery.temperature_sensor_ch3.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#3=" + data.battery.temperature_sensor_ch3.value + "V");
				}
				if (data.battery.temperature_sensor_ch4 != null && data.battery.temperature_sensor_ch4.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#4=" + data.battery.temperature_sensor_ch4.value + "V");
				}
				if (data.battery.temperature_sensor_ch5 != null && data.battery.temperature_sensor_ch5.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#5=" + data.battery.temperature_sensor_ch5.value + "V");
				}
				if (data.battery.temperature_sensor_ch6 != null && data.battery.temperature_sensor_ch6.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#6=" + data.battery.temperature_sensor_ch6.value + "V");
				}
				if (data.battery.temperature_sensor_ch7 != null && data.battery.temperature_sensor_ch7.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#7=" + data.battery.temperature_sensor_ch7.value + "V");
				}
				if (data.battery.temperature_sensor_ch8 != null && data.battery.temperature_sensor_ch8.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Temp#8=" + data.battery.temperature_sensor_ch8.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch1 != null && data.battery.leaf_wetness_sensor_ch1.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#1=" + data.battery.leaf_wetness_sensor_ch1.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch2 != null && data.battery.leaf_wetness_sensor_ch2.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#2=" + data.battery.leaf_wetness_sensor_ch2.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch3 != null && data.battery.leaf_wetness_sensor_ch3.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#3=" + data.battery.leaf_wetness_sensor_ch3.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch4 != null && data.battery.leaf_wetness_sensor_ch4.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#4=" + data.battery.leaf_wetness_sensor_ch4.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch5 != null && data.battery.leaf_wetness_sensor_ch5.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#5=" + data.battery.leaf_wetness_sensor_ch5.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch6 != null && data.battery.leaf_wetness_sensor_ch6.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#6=" + data.battery.leaf_wetness_sensor_ch6.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch7 != null && data.battery.leaf_wetness_sensor_ch7.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#7=" + data.battery.leaf_wetness_sensor_ch7.value + "V");
				}
				if (data.battery.leaf_wetness_sensor_ch8 != null && data.battery.leaf_wetness_sensor_ch8.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("Leaf#8=" + data.battery.leaf_wetness_sensor_ch8.value + "V");
				}
				if (data.battery.ldsbatt_1 != null && data.battery.ldsbatt_1.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("LDS#1=" + data.battery.ldsbatt_1.value + "V");
				}
				if (data.battery.ldsbatt_2 != null && data.battery.ldsbatt_2.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("LDS#2=" + data.battery.ldsbatt_2.value + "V");
				}
				if (data.battery.ldsbatt_3 != null && data.battery.ldsbatt_3.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("LDS#3=" + data.battery.ldsbatt_3.value + "V");
				}
				if (data.battery.ldsbatt_4 != null && data.battery.ldsbatt_4.value < 1.2)      // volts
				{
					lowBatt = true;
					LowBatteryDevices.Add("LDS#4=" + data.battery.ldsbatt_4.value + "V");
				}

				cumulus.BatteryLowAlarm.Triggered = lowBatt;
			}
		}

		private async Task CheckAvailableFirmware()
		{
			if (EcowittApi.SimpleSupportedModels.Contains(deviceModel[..6]))
			{
				var retVal = ecowittApi.GetSimpleLatestFirmwareVersion(deviceModel, cumulus.cancellationToken).Result;
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
				_ = await ecowittApi.GetLatestFirmwareVersion(deviceModel, cumulus.EcowittMacAddress, "V" + deviceFirmware.ToString(), cumulus.cancellationToken);
			}
		}
	}
}
