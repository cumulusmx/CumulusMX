using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Swan;

using static System.Collections.Specialized.BitVector32;

namespace CumulusMX
{
	internal class EcowittCloud : WeatherStation
	{
		private readonly WeatherStation station;
		private EcowittApi ecowittApi;
		private int maxArchiveRuns = 1;
		private Task liveTask;
		private readonly bool main;


		public EcowittCloud(Cumulus cumulus, WeatherStation station = null) : base(cumulus)
		{
			this.station = station;

			main = station == null;

			if (main)
			{
				cumulus.LogMessage("Creating Ecowitt Cloud");
			}
			else
			{
				cumulus.LogMessage("Creating Extra Sensors - Ecowitt Cloud");
			}

			if (string.IsNullOrEmpty(cumulus.EcowittApplicationKey) || string.IsNullOrEmpty(cumulus.EcowittUserApiKey) || string.IsNullOrEmpty(cumulus.EcowittMacAddress))
			{
				cumulus.LogMessage("API.GetHistoricData: Missing Ecowitt API data in the configuration, aborting!");
				return;
			}


			// Do not set these if we are only using extra sensors
			if (main)
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

			}

			if (main || (!main && cumulus.EcowittExtraUseAQI))
			{
				cumulus.Units.AirQualityUnitText = "µg/m³";
			}
			if (main || (!main && cumulus.EcowittExtraUseSoilMoist))
			{
				cumulus.Units.SoilMoistureUnitText = "%";
			}
			if (main || (!main && cumulus.EcowittExtraUseSoilMoist))
			{
				cumulus.Units.LeafWetnessUnitText = "%";
			}

			// Only perform the Start-up if we are a proper station, not a Extra Sensor
			if (main)
			{
				Task.Run(getAndProcessHistoryData);
			}
			else
			{
				cumulus.LogMessage("Extra Sensors - Ecowitt Cloud");
			}
		}

		public override void Start()
		{
			// just incase we did not catch-up any history
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

			cumulus.LogMessage("Starting Ecowitt Cloud station");

			cumulus.StartTimersAndSensors();

			liveTask = Task.Run(() =>
			{
				try
				{
					var piezoLastRead = DateTime.MinValue;
					var dataLastRead = DateTime.MinValue;
					var delay = 0;
					var nextFetch = DateTime.MinValue;


					while (!cumulus.cancellationToken.IsCancellationRequested)
					{
						if (DateTime.Now >= nextFetch)
						{
							var data = ecowittApi.GetCurrentData(cumulus.cancellationToken, ref delay);
							if (data != null)
							{
								ProcessCurrentData(data, cumulus.cancellationToken);
							}
							cumulus.LogDebugMessage($"EcowittCloud; Waiting {delay} seconds before next update");
							nextFetch = DateTime.Now.AddSeconds(delay);
						}
						Thread.Sleep(1000);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "Error ruuning Ecowitt Cloud station");
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
			//cumulus.LogDebugMessage("Lock: Station waiting for the lock");
			Cumulus.syncInit.Wait();
			//cumulus.LogDebugMessage("Lock: Station has the lock");

			int archiveRun = 0;

			try
			{

				ecowittApi = new EcowittApi(cumulus, this);

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

			//cumulus.LogDebugMessage("Lock: Station releasing the lock");
			_ = Cumulus.syncInit.Release();

			StartLoop();
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
			var thisStation = main ? this : station;

			// Only do the primary sensors if running as the main station
			if (main)
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
							DoOutdoorDewpoint(data.outdoor.dew_point.value, time);
							DoFeelsLike(time);
							DoApparentTemp(time);
							DoOutdoorHumidity(data.outdoor.humidity.value, time);
							DoHumidex(time);
							DoCloudBaseHeatIndex(time);
						}
						catch (Exception ex)
						{
							cumulus.LogMessage($"ProcessCurrentData: Error in Outdoor temp data - {ex.Message}");
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
						DoIndoorTemp(data.indoor.temperature.value);
						DoIndoorHumidity(data.indoor.humidity.value);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"ProcessCurrentData: Error in indoor data - {ex.Message}");
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
						DoPressure(data.pressure.relative.value, Utils.FromUnixTime(data.pressure.relative.time));
						StationPressure = data.pressure.absolute.value;
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"ProcessCurrentData: Error in pressure data - {ex.Message}");
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
						DoWindChill(0, Utils.FromUnixTime(data.wind.wind_gust.time));
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"ProcessCurrentData: Error in wind data - {ex.Message}");
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
							cumulus.LogMessage($"ProcessCurrentData: Error in tipper rainfall data - {ex.Message}");
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
							cumulus.LogMessage($"ProcessCurrentData: Error in piezo rainfall data - {ex.Message}");
						}
					}
				}
			}

			// Solar
			if ((main || cumulus.EcowittExtraUseSolar) && data.solar_and_uvi != null)
			{
				try
				{
					DoSolarRad((int) data.solar_and_uvi.solar.value, Utils.FromUnixTime(data.solar_and_uvi.solar.time));
					DoUV(data.solar_and_uvi.uvi.value, Utils.FromUnixTime(data.solar_and_uvi.solar.time));
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in solar data - {ex.Message}");
				}
			}

			// Extra Temperature
			if (main || cumulus.EcowittExtraUseTempHum)
			{
				try
				{
					ProcessExtraTempHum(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in extra temperature data - {ex.Message}");
				}
			}

			// === Soil/Water Temp ===
			if (main || cumulus.EcowittExtraUseUserTemp)
			{
				try
				{
					ProcessUserTemps(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in user temperature data - {ex.Message}");
				}
			}

			// === Soil Moisture ===
			if (main || cumulus.EcowittExtraUseSoilMoist)
			{
				try
				{
					ProcessSoilMoist(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in Soil moisture data - {ex.Message}");
				}
			}

			// === Leaf Wetness ===
			if (main || cumulus.EcowittExtraUseLeafWet)
			{
				try
				{
					ProcessLeafWetness(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in Leaf wetness data - {ex.Message}");
				}
			}

			// === Air Quality ===
			if (main || cumulus.EcowittExtraUseAQI)
			{
				try
				{
					ProcessAirQuality(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in Air Quality data - {ex.Message}");
				}
			}

			// === CO₂ ===
			if (main || cumulus.EcowittExtraUseCo2)
			{
				try
				{
					ProcessCo2(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in CO₂ data - {ex.Message}");
				}
			}

			// === Lightning ===
			if (main || cumulus.EcowittExtraUseLightning)
			{
				try
				{
					ProcessLightning(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in Lightning data - {ex.Message}");
				}
			}

			// === Leak ===
			if (main || cumulus.EcowittExtraUseLeak)
			{
				try
				{
					ProcessLeak(data, thisStation);
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"ProcessCurrentData: Error in Leak data - {ex.Message}");
				}
			}

			// === Batteries ===
			try
			{
				ProcessBatteries(data);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"ProcessCurrentData: Error in Battery data - {ex.Message}");
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
				cumulus.LogMessage($"ProcessCurrentData: Error in Camera data - {ex.Message}");
			}


			thisStation.DoForecast("", false);

			cumulus.BatteryLowAlarm.Triggered = batteryLow;


			var updateTime = Utils.FromUnixTime(data.outdoor == null ? data.indoor.temperature.time : data.indoor.temperature.time);
			thisStation.UpdateStatusPanel(updateTime);
			thisStation.UpdateMQTT();

			DataStopped = false;
			cumulus.DataStoppedAlarm.Triggered = false;
		}
		private void ProcessExtraTempHum(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.temp_and_humidity_ch1 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 1)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch1.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch1.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch1.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch1.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch1.temperature.value, 1);
				station.DoExtraHum(data.temp_and_humidity_ch1.humidity.value, 1);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[1]), station.ExtraHum[1]);
				station.ExtraDewPoint[1] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch2 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 2)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch2.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch2.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch2.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch2.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch2.temperature.value, 2);
				station.DoExtraHum(data.temp_and_humidity_ch2.humidity.value, 2);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[2]), station.ExtraHum[2]);
				station.ExtraDewPoint[2] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch3 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 3)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch3.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch3.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch3.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch3.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch3.temperature.value, 3);
				station.DoExtraHum(data.temp_and_humidity_ch3.humidity.value, 3);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[3]), station.ExtraHum[3]);
				station.ExtraDewPoint[3] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch4 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 4)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch4.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch4.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch4.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch4.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch4.temperature.value, 4);
				station.DoExtraHum(data.temp_and_humidity_ch4.humidity.value, 4);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[4]), station.ExtraHum[4]);
				station.ExtraDewPoint[4] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch5 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 5)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch5.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch5.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch5.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch5.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch5.temperature.value, 5);
				station.DoExtraHum(data.temp_and_humidity_ch5.humidity.value, 5);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[5]), station.ExtraHum[5]);
				station.ExtraDewPoint[5] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch6 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 6)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch6.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch6.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch6.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch6.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch6.temperature.value, 6);
				station.DoExtraHum(data.temp_and_humidity_ch6.humidity.value, 6);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[6]), station.ExtraHum[6]);
				station.ExtraDewPoint[6] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch7 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 7)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch7.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch7.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch7.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch7.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch7.temperature.value, 7);
				station.DoExtraHum(data.temp_and_humidity_ch7.humidity.value, 7);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[7]), station.ExtraHum[7]);
				station.ExtraDewPoint[7] = ConvertTempCToUser(dp);
			}

			if (data.temp_and_humidity_ch8 != null)
			{
				if (cumulus.Gw1000PrimaryTHSensor == 8)
				{
					station.DoOutdoorTemp(data.temp_and_humidity_ch8.temperature.value, Utils.FromUnixTime(data.temp_and_humidity_ch8.temperature.time));
					station.DoOutdoorHumidity(data.temp_and_humidity_ch8.humidity.value, Utils.FromUnixTime(data.temp_and_humidity_ch8.humidity.time));
				}
				station.DoExtraTemp(data.temp_and_humidity_ch8.temperature.value, 8);
				station.DoExtraHum(data.temp_and_humidity_ch8.humidity.value, 8);

				var dp = MeteoLib.DewPoint(ConvertUserTempToC(station.ExtraTemp[8]), station.ExtraHum[8]);
				station.ExtraDewPoint[8] = ConvertTempCToUser(dp);
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
				station.DoSoilTemp(data.soil_ch1.soilmoisture.value, 1);
			}

			if (data.soil_ch2 != null)
			{
				station.DoSoilTemp(data.soil_ch2.soilmoisture.value, 2);
			}

			if (data.soil_ch3 != null)
			{
				station.DoSoilTemp(data.soil_ch3.soilmoisture.value, 3);
			}

			if (data.soil_ch4 != null)
			{
				station.DoSoilTemp(data.soil_ch4.soilmoisture.value, 4);
			}

			if (data.soil_ch5 != null)
			{
				station.DoSoilTemp(data.soil_ch5.soilmoisture.value, 5);
			}

			if (data.soil_ch6 != null)
			{
				station.DoSoilTemp(data.soil_ch6.soilmoisture.value, 6);
			}

			if (data.soil_ch7 != null)
			{
				station.DoSoilTemp(data.soil_ch7.soilmoisture.value, 7);
			}

			if (data.soil_ch8 != null)
			{
				station.DoSoilTemp(data.soil_ch8.soilmoisture.value, 8);
			}
		}

		private void ProcessLeafWetness(EcowittApi.CurrentDataData data, WeatherStation station)
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

		private void ProcessAirQuality(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.pm25_ch1 != null)
			{
				station.DoAirQuality(data.pm25_ch1.pm25.value, 1);
				//station.DoAirQualityAvg(data.pm25_ch1.Avg24h.value, 1);
			}

			if (data.pm25_ch2 != null)
			{
				station.DoAirQuality(data.pm25_ch2.pm25.value, 2);
				//station.DoAirQualityAvg(data.pm25_ch2.Avg24h.value, 2);
			}
			if (data.pm25_ch3 != null)
			{
				station.DoAirQuality(data.pm25_ch3.pm25.value, 3);
				//station.DoAirQualityAvg(data.pm25_ch3.Avg24h.value, 3);
			}
			if (data.pm25_ch4 != null)
			{
				station.DoAirQuality(data.pm25_ch4.pm25.value, 4);
				//station.DoAirQualityAvg(data.pm25_ch1.Avg24h.value, 4);
			}
		}

		private void ProcessCo2(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.co2_aqi_combo != null)
			{
				station.CO2 = data.co2_aqi_combo.co2.value;
				station.CO2_24h = data.co2_aqi_combo.Avg24h;
			}
			// indoor overrides the combo
			if (data.indoor_co2 != null)
			{
				station.CO2 = data.indoor_co2.co2.value;
				station.CO2_24h = data.indoor_co2.Avg24h;
			}
		}

		private void ProcessLightning(EcowittApi.CurrentDataData data, WeatherStation station)
		{
			if (data.lightning != null)
			{
				if (data.lightning.distance.value != 255)
				{
					station.LightningStrikesToday = data.lightning.count.value;
					station.LightningDistance = ConvertKmtoUserUnits(data.lightning.distance.value);

					var tim = Utils.FromUnixTime(data.lightning.distance.time);

					if (tim > LightningTime)
					{
						station.LightningTime = tim;
					}
				}
			}
		}

		private void ProcessLeak(EcowittApi.CurrentDataData data, WeatherStation station)
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

		private void ProcessBatteries(EcowittApi.CurrentDataData data)
		{
			var lowBatt = false;

			if (data.battery != null)
			{
				lowBatt = lowBatt || (data.battery.t_rh_p_sensor != null && data.battery.t_rh_p_sensor.value == 1);                 // flag
				lowBatt = lowBatt || (data.battery.ws1900_console != null && data.battery.ws1900_console.value < 1.2);              // volts - val ???
				lowBatt = lowBatt || (data.battery.ws1800_console != null && data.battery.ws1800_console.value < 1.2);              // volts - val ???
				lowBatt = lowBatt || (data.battery.ws6006_console != null && data.battery.ws6006_console.value < 15);               // %  val ???
				lowBatt = lowBatt || (data.battery.console != null && data.battery.console.value < 2.4);                            // volts  val ???
				lowBatt = lowBatt || (data.battery.outdoor_t_rh_sensor != null && data.battery.outdoor_t_rh_sensor.value == 1);     // flag
				lowBatt = lowBatt || (data.battery.wind_sensor != null && data.battery.wind_sensor.value < 1.2);                    // volts
				lowBatt = lowBatt || (data.battery.haptic_array_battery != null && data.battery.haptic_array_battery.value < 2.4);  // volts
																																	//lowBatt = lowBatt || (data.battery.haptic_array_capacitor != null && data.battery.haptic_array_capacitor.value == 2.4); // volts
				lowBatt = lowBatt || (data.battery.sonic_array != null && data.battery.sonic_array.value < 1.2);                    // volts
				lowBatt = lowBatt || (data.battery.rainfall_sensor != null && data.battery.rainfall_sensor.value < 1.2);            // volts
				lowBatt = lowBatt || (data.battery.sensor_array != null && data.battery.sensor_array.value == 1);                   // flag
				lowBatt = lowBatt || (data.battery.lightning_sensor != null && data.battery.lightning_sensor.value == 1);           // flag
				lowBatt = lowBatt || (data.battery.aqi_combo_sensor != null && data.battery.aqi_combo_sensor.value == 1);           // flag
				lowBatt = lowBatt || (data.battery.water_leak_sensor_ch1 != null && data.battery.water_leak_sensor_ch1.value == 1); // flag
				lowBatt = lowBatt || (data.battery.water_leak_sensor_ch2 != null && data.battery.water_leak_sensor_ch2.value == 1); // flag
				lowBatt = lowBatt || (data.battery.water_leak_sensor_ch3 != null && data.battery.water_leak_sensor_ch3.value == 1); // flag
				lowBatt = lowBatt || (data.battery.water_leak_sensor_ch4 != null && data.battery.water_leak_sensor_ch4.value == 1); // flag
				lowBatt = lowBatt || (data.battery.pm25_sensor_ch1 != null && data.battery.pm25_sensor_ch1.value == 1);             // flag
				lowBatt = lowBatt || (data.battery.pm25_sensor_ch2 != null && data.battery.pm25_sensor_ch2.value == 1);             // flag
				lowBatt = lowBatt || (data.battery.pm25_sensor_ch3 != null && data.battery.pm25_sensor_ch3.value == 1);             // flag
				lowBatt = lowBatt || (data.battery.pm25_sensor_ch4 != null && data.battery.pm25_sensor_ch4.value == 1);             // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch1 != null && data.battery.temp_humidity_sensor_ch1.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch2 != null && data.battery.temp_humidity_sensor_ch2.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch3 != null && data.battery.temp_humidity_sensor_ch3.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch4 != null && data.battery.temp_humidity_sensor_ch4.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch5 != null && data.battery.temp_humidity_sensor_ch5.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch6 != null && data.battery.temp_humidity_sensor_ch6.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch7 != null && data.battery.temp_humidity_sensor_ch7.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.temp_humidity_sensor_ch8 != null && data.battery.temp_humidity_sensor_ch8.value == 1);  // flag
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch1 != null && data.battery.soilmoisture_sensor_ch1.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch2 != null && data.battery.soilmoisture_sensor_ch2.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch3 != null && data.battery.soilmoisture_sensor_ch3.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch4 != null && data.battery.soilmoisture_sensor_ch4.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch5 != null && data.battery.soilmoisture_sensor_ch5.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch6 != null && data.battery.soilmoisture_sensor_ch6.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch7 != null && data.battery.soilmoisture_sensor_ch7.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.soilmoisture_sensor_ch8 != null && data.battery.soilmoisture_sensor_ch8.value < 1.2);    // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch1 != null && data.battery.temperature_sensor_ch1.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch2 != null && data.battery.temperature_sensor_ch2.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch3 != null && data.battery.temperature_sensor_ch3.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch4 != null && data.battery.temperature_sensor_ch4.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch5 != null && data.battery.temperature_sensor_ch5.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch6 != null && data.battery.temperature_sensor_ch6.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch7 != null && data.battery.temperature_sensor_ch7.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.temperature_sensor_ch8 != null && data.battery.temperature_sensor_ch8.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch1 != null && data.battery.leaf_wetness_sensor_ch1.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch2 != null && data.battery.leaf_wetness_sensor_ch2.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch3 != null && data.battery.leaf_wetness_sensor_ch3.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch4 != null && data.battery.leaf_wetness_sensor_ch4.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch5 != null && data.battery.leaf_wetness_sensor_ch5.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch6 != null && data.battery.leaf_wetness_sensor_ch6.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch7 != null && data.battery.leaf_wetness_sensor_ch7.value < 1.2);      // volts
				lowBatt = lowBatt || (data.battery.leaf_wetness_sensor_ch8 != null && data.battery.leaf_wetness_sensor_ch8.value < 1.2);      // volts

				cumulus.BatteryLowAlarm.Triggered = lowBatt;
			}
		}
	}
}
