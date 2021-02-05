using System;
using System.IO;
using System.Net;
using System.Threading;
using ServiceStack.Text;
using Unosquare.Labs.EmbedIO;
using System.Reflection;

namespace CumulusMX
{
	internal class StationSettings
	{
		private readonly Cumulus cumulus;
		private readonly WeatherStation station;
		private readonly string stationOptionsFile;
		private readonly string stationSchemaFile;

		internal StationSettings(Cumulus cumulus, WeatherStation station)
		{
			this.cumulus = cumulus;
			this.station = station;

			stationOptionsFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "StationOptions.json";
			stationSchemaFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "StationSchema.json";
		}

		internal string GetStationAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it
			var optionsAdv = new JsonStationSettingsOptionsAdvanced()
			{
				avgbearingmins = cumulus.StationOptions.AvgBearingMinutes,
				avgspeedmins = cumulus.StationOptions.AvgSpeedMinutes,
				peakgustmins = cumulus.StationOptions.PeakGustMinutes
			};

			var options = new JsonStationSettingsOptions()
			{
				usezerobearing = cumulus.StationOptions.UseZeroBearing,
				calcwindaverage = cumulus.StationOptions.UseWind10MinAve,
				usespeedforavg = cumulus.StationOptions.UseSpeedForAvgCalc,
				use100for98hum = cumulus.StationOptions.Humidity98Fix,
				calculatedewpoint = cumulus.StationOptions.CalculatedDP,
				calculatewindchill = cumulus.StationOptions.CalculatedWC,
				syncstationclock = cumulus.StationOptions.SyncTime,
				syncclockhour = cumulus.StationOptions.ClockSettingHour,
				cumuluspresstrendnames = cumulus.StationOptions.UseCumulusPresstrendstr,
				extrasensors = cumulus.StationOptions.LogExtraSensors,
				ignorelacrosseclock = cumulus.StationOptions.WS2300IgnoreStationClock,
				roundwindspeeds = cumulus.StationOptions.RoundWindSpeed,
				nosensorcheck = cumulus.StationOptions.NoSensorCheck,
				advanced = optionsAdv
			};

			var unitsAdv = new JsonStationSettingsUnitsAdvanced
			{
				airqulaitydp = cumulus.AirQualityDPlaces,
				pressdp = cumulus.PressDPlaces,
				raindp = cumulus.RainDPlaces,
				sunshinedp = cumulus.SunshineDPlaces,
				tempdp = cumulus.TempDPlaces,
				uvdp = cumulus.UVDPlaces,
				windavgdp = cumulus.WindAvgDPlaces,
				winddp = cumulus.WindDPlaces,
				windrundp = cumulus.WindRunDPlaces
			};

			var units = new JsonStationSettingsUnits()
			{
				wind = cumulus.Units.Wind,
				pressure = cumulus.Units.Press,
				temp = cumulus.TempUnit,
				rain = cumulus.RainUnit,
				advanced = unitsAdv
			};

			var tcpsettings = new JsonStationSettingsTCPsettings()
			{
				ipaddress = cumulus.DavisOptions.IPAddr,
				tcpport = cumulus.DavisOptions.TCPPort,
				disconperiod = cumulus.DavisOptions.PeriodicDisconnectInterval
			};

			var davisconn = new JsonStationSettingsDavisConn()
			{
				conntype = cumulus.DavisOptions.ConnectionType,
				tcpsettings = tcpsettings
			};

			var davisCommonAdvanced = new JsonStationSettingsDavisCommonAdvanced()
			{
				raingaugetype = cumulus.DavisOptions.RainGaugeType
			};

			var davisCommon = new JsonStationSettingsDavisCommon()
			{
				 davisconn = davisconn,
				 advanced = davisCommonAdvanced
			};

			var davisvp2advanced = new JsonStationSettingsDavisVp2Advanced()
			{
				useloop2 = cumulus.DavisOptions.UseLoop2,
				vp1minbarupdate = cumulus.DavisOptions.ForceVPBarUpdate,
				initwaittime = cumulus.DavisOptions.InitWaitTime,
				ipresponsetime = cumulus.DavisOptions.IPResponseTime,
				baudrate = cumulus.DavisOptions.BaudRate
			};

			var davisvp2 = new JsonStationSettingsDavisVp2()
			{
				readreceptionstats = cumulus.DavisOptions.ReadReceptionStats,
				setloggerinterval = cumulus.DavisOptions.SetLoggerInterval,
				advanced = davisvp2advanced
			};

			var gw1000 = new JSonStationSettingsGw1000Conn() {ipaddress = cumulus.Gw1000IpAddress, autoDiscover = cumulus.Gw1000AutoUpdateIpAddress, macaddress = cumulus.Gw1000MacAddress };

			var logrollover = new JsonStationSettingsLogRollover() {time = "midnight",summer10am = cumulus.Use10amInSummer};

			if (cumulus.RolloverHour == 9)
			{
				logrollover.time = "9am";
			}

			var fineoffsetadvanced = new JsonStationSettingsFineOffsetAdvanced()
			{
				readtime = cumulus.FineOffsetOptions.FineOffsetReadTime,
				vid = cumulus.FineOffsetOptions.VendorID,
				pid = cumulus.FineOffsetOptions.ProductID
			};

			var fineoffset = new JsonStationSettingsFineOffset()
			{
				syncreads = cumulus.FineOffsetOptions.FineOffsetSyncReads,
				readavoid = cumulus.FineOffsetOptions.FineOffsetReadAvoidPeriod,
				advanced = fineoffsetadvanced
			};

			var easyweather = new JsonStationSettingsEasyWeather()
			{
				interval = cumulus.EwOptions.Interval,
				filename = cumulus.EwOptions.Filename,
				minpressmb = cumulus.EwOptions.MinPressMB,
				maxpressmb = cumulus.EwOptions.MaxPressMB,
				raintipdiff = cumulus.EwOptions.MaxRainTipDiff,
				pressoffset = cumulus.EwOptions.PressOffset
			};

			var imetAdvanced = new JsonStationSettingsImetAdvanced()
			{
				readdelay = cumulus.ImetOptions.ImetReadDelay,
				waittime = cumulus.ImetOptions.ImetWaitTime,
				updatelogpointer = cumulus.ImetOptions.ImetUpdateLogPointer
			};

			var imet = new JsonStationSettingsImet()
			{
				baudrate = cumulus.ImetOptions.ImetBaudRate,
				advanced = imetAdvanced
			};


			int deg, min, sec;
			string hem;

			LatToDMS(cumulus.Latitude, out deg, out min, out sec, out hem);

			var latitude = new JsonStationSettingsLatLong() {degrees = deg, minutes = min, seconds = sec, hemisphere = hem};

			LongToDMS(cumulus.Longitude, out deg, out min, out sec, out hem);

			var longitude = new JsonStationSettingsLatLong() { degrees = deg, minutes = min, seconds = sec, hemisphere = hem };

			var location = new JsonStationSettingsLocation()
			{
				altitude = (int) cumulus.Altitude,
				altitudeunit = "metres",
				description = cumulus.LocationDesc,
				Latitude = latitude,
				Longitude = longitude,
				sitename = cumulus.LocationName
			};

			if (cumulus.AltitudeInFeet)
			{
				location.altitudeunit = "feet";
			}

			var forecast = new JsonStationSettingsForecast()
			{
				highpressureextreme = cumulus.FChighpress,
				lowpressureextreme = cumulus.FClowpress,
				pressureunit = "mb/hPa",
				updatehourly = cumulus.HourlyForecast,
				usecumulusforecast = cumulus.UseCumulusForecast
			};

			if (!cumulus.FCpressinMB)
			{
				forecast.pressureunit = "inHg";
			}

			var solar = new JsonStationSettingsSolar()
			{
				solarmin = cumulus.SolarMinimum,
				transfactor = cumulus.RStransfactor,
				sunthreshold = cumulus.SunThreshold,
				solarcalc = cumulus.SolarCalc,
				turbidity = cumulus.BrasTurbidity
			};

			var annualrainfall = new JsonStationSettingsAnnualRainfall() {rainseasonstart = cumulus.RainSeasonStart, ytdamount = cumulus.YTDrain, ytdyear = cumulus.YTDrainyear};

			var graphDataTemp = new JsonStationSettingsGraphDataTemperature()
			{
				graphTempVis = cumulus.GraphOptions.TempVisible,
				graphInTempVis = cumulus.GraphOptions.InTempVisible,
				graphHeatIndexVis = cumulus.GraphOptions.HIVisible,
				graphDewPointVis = cumulus.GraphOptions.DPVisible,
				graphWindChillVis = cumulus.GraphOptions.WCVisible,
				graphAppTempVis = cumulus.GraphOptions.AppTempVisible,
				graphFeelsLikeVis = cumulus.GraphOptions.FeelsLikeVisible,
				graphHumidexVis = cumulus.GraphOptions.HumidexVisible
			};

			var graphDataHum = new JsonStationSettingsGraphDataHumidity()
			{
				graphHumVis = cumulus.GraphOptions.OutHumVisible,
				graphInHumVis = cumulus.GraphOptions.InHumVisible
			};

			var graphDataSolar = new JsonStationSettingsGraphDataSolar()
			{
				graphUvVis = cumulus.GraphOptions.UVVisible,
				graphSolarVis = cumulus.GraphOptions.SolarVisible,
				graphSunshineVis = cumulus.GraphOptions.SunshineVisible
			};

			var graphDataVis = new JsonStationSettingsGraphVisibility()
			{
				temperature = graphDataTemp,
				humidity = graphDataHum,
				solar = graphDataSolar
			};

			var graphs = new JsonStationSettingsGraphs()
			{
				graphdays = cumulus.GraphDays,
				graphhours = cumulus.GraphHours,
				datavisibility = graphDataVis
			};

			var wllNetwork = new JsonStationSettingsWLLNetwork()
			{
				autoDiscover = cumulus.WLLAutoUpdateIpAddress
			};

			var wllApi = new JsonStationSettingsWLLApi()
			{
				apiKey = cumulus.WllApiKey,
				apiSecret = cumulus.WllApiSecret,
				apiStationId = cumulus.WllStationId
			};

			var wllPrimary = new JsonStationSettingsWllPrimary()
			{
				wind = cumulus.WllPrimaryWind,
				temphum = cumulus.WllPrimaryTempHum,
				rain = cumulus.WllPrimaryRain,
				solar = cumulus.WllPrimarySolar,
				uv = cumulus.WllPrimaryUV
			};

			var wllExtraSoilTemp = new JsonStationSettingsWllSoilTemp()
			{

				soilTempTx1 = cumulus.WllExtraSoilTempTx1,
				soilTempIdx1 = cumulus.WllExtraSoilTempIdx1,
				soilTempTx2 = cumulus.WllExtraSoilTempTx2,
				soilTempIdx2 = cumulus.WllExtraSoilTempIdx2,
				soilTempTx3 = cumulus.WllExtraSoilTempTx3,
				soilTempIdx3 = cumulus.WllExtraSoilTempIdx3,
				soilTempTx4 = cumulus.WllExtraSoilTempTx4,
				soilTempIdx4 = cumulus.WllExtraSoilTempIdx4
			};

			var wllExtraSoilMoist = new JsonStationSettingsWllSoilMoist()
			{
				soilMoistTx1 = cumulus.WllExtraSoilMoistureTx1,
				soilMoistIdx1 = cumulus.WllExtraSoilMoistureIdx1,
				soilMoistTx2 = cumulus.WllExtraSoilMoistureTx2,
				soilMoistIdx2 = cumulus.WllExtraSoilMoistureIdx2,
				soilMoistTx3 = cumulus.WllExtraSoilMoistureTx3,
				soilMoistIdx3 = cumulus.WllExtraSoilMoistureIdx3,
				soilMoistTx4 = cumulus.WllExtraSoilMoistureTx4,
				soilMoistIdx4 = cumulus.WllExtraSoilMoistureIdx4
			};

			var wllExtraLeaf = new JsonStationSettingsWllExtraLeaf()
			{
				leafTx1 = cumulus.WllExtraLeafTx1,
				leafIdx1 = cumulus.WllExtraLeafIdx1,
				leafTx2 = cumulus.WllExtraLeafTx2,
				leafIdx2 = cumulus.WllExtraLeafIdx2
			};

			var wllSoilLeaf = new JsonStationSettingsWllSoilLeaf()
			{
				extraSoilTemp = wllExtraSoilTemp,
				extraSoilMoist = wllExtraSoilMoist,
				extraLeaf = wllExtraLeaf
			};

			var wllExtraTemp = new JsonStationSettingsWllExtraTemp();
			for (int i = 1; i <= 8; i++)
			{
				PropertyInfo propInfo = wllExtraTemp.GetType().GetProperty("extraTempTx" + i);
				propInfo.SetValue(wllExtraTemp, Convert.ChangeType(cumulus.WllExtraTempTx[i - 1], propInfo.PropertyType), null);

				propInfo = wllExtraTemp.GetType().GetProperty("extraHumTx" + i);
				propInfo.SetValue(wllExtraTemp, Convert.ChangeType(cumulus.WllExtraHumTx[i - 1], propInfo.PropertyType), null);
			};

			var wll = new JsonStationSettingsWLL()
			{
				network = wllNetwork,
				api = wllApi,
				primary = wllPrimary,
				soilLeaf = wllSoilLeaf,
				extraTemp = wllExtraTemp
			};

			var general = new JsonStationGeneral()
			{
				stationtype = cumulus.StationType,
				comportname = cumulus.ComportName,
				loginterval = cumulus.DataLogInterval,
				logrollover = logrollover,
				units = units,
				Location = location
			};

			var data = new JsonStationSettingsData()
			{
				stationid = cumulus.StationType,
				general = general,
				daviscommon = davisCommon,
				davisvp2 = davisvp2,
				daviswll = wll,
				gw1000 = gw1000,
				fineoffset = fineoffset,
				easyw = easyweather,
				imet = imet,
				Options = options,
				Forecast = forecast,
				Solar = solar,
				AnnualRainfall = annualrainfall,
				Graphs = graphs
			};

			//return JsonConvert.SerializeObject(data);
			return JsonSerializer.SerializeToString(data);
		}

		internal string GetStationAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(stationOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		internal string GetStationAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(stationSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		private void LongToDMS(double longitude, out int d, out int m, out int s, out string hem)
		{
			double coordinate;
			if (longitude < 0)
			{
				coordinate = -longitude;
				hem = "West";
			}
			else
			{
				coordinate = longitude;
				hem = "East";
			}
			int secs = (int)(coordinate * 60 * 60);

			s = secs % 60;

			secs = (secs - s) / 60;

			m = secs % 60;
			d = secs / 60;
		}

		private void LatToDMS(double latitude, out int d, out int m, out int s, out string hem)
		{
			double coordinate;
			if (latitude < 0)
			{
				coordinate = -latitude;
				hem = "South";
			}
			else
			{
				coordinate = latitude;
				hem = "North";
			}

			int secs = (int)(coordinate * 60 * 60);

			s = secs % 60;

			secs = (secs - s) / 60;

			m = secs % 60;
			d = secs / 60;
		}

		internal string UpdateStationConfig(IHttpContext context)
		{
			var errorMsg = "";
			context.Response.StatusCode = 200;
			// get the response
			try
			{
				cumulus.LogMessage("Updating station settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = JsonSerializer.DeserializeFromString<JsonStationSettingsData>(json);

				// process the settings

				// Graph Config
				try
				{
					cumulus.GraphHours = settings.Graphs.graphhours;
					cumulus.GraphDays = settings.Graphs.graphdays;
					cumulus.GraphOptions.TempVisible = settings.Graphs.datavisibility.temperature.graphTempVis;
					cumulus.GraphOptions.InTempVisible = settings.Graphs.datavisibility.temperature.graphInTempVis;
					cumulus.GraphOptions.HIVisible = settings.Graphs.datavisibility.temperature.graphHeatIndexVis;
					cumulus.GraphOptions.DPVisible = settings.Graphs.datavisibility.temperature.graphDewPointVis;
					cumulus.GraphOptions.WCVisible = settings.Graphs.datavisibility.temperature.graphWindChillVis;
					cumulus.GraphOptions.AppTempVisible = settings.Graphs.datavisibility.temperature.graphAppTempVis;
					cumulus.GraphOptions.FeelsLikeVisible = settings.Graphs.datavisibility.temperature.graphFeelsLikeVis;
					cumulus.GraphOptions.HumidexVisible = settings.Graphs.datavisibility.temperature.graphHumidexVis;
					cumulus.GraphOptions.OutHumVisible = settings.Graphs.datavisibility.humidity.graphHumVis;
					cumulus.GraphOptions.InHumVisible = settings.Graphs.datavisibility.humidity.graphInHumVis;
					cumulus.GraphOptions.UVVisible = settings.Graphs.datavisibility.solar.graphUvVis;
					cumulus.GraphOptions.SolarVisible = settings.Graphs.datavisibility.solar.graphSolarVis;
					cumulus.GraphOptions.SunshineVisible = settings.Graphs.datavisibility.solar.graphSunshineVis;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Graph hours: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Annual Rainfall
				try
				{
					cumulus.RainSeasonStart = settings.AnnualRainfall.rainseasonstart;
					cumulus.YTDrain = settings.AnnualRainfall.ytdamount;
					cumulus.YTDrainyear = settings.AnnualRainfall.ytdyear;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Rainfall settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Solar
				try
				{
					if (settings.Solar != null)
					{
						cumulus.SolarCalc = settings.Solar.solarcalc;
						cumulus.SolarMinimum = settings.Solar.solarmin;
						cumulus.SunThreshold = settings.Solar.sunthreshold;
						if (cumulus.SolarCalc == 0)
							cumulus.RStransfactor = settings.Solar.transfactor;
						else
							cumulus.BrasTurbidity = settings.Solar.turbidity;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Solar settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Forecast
				try
				{
					cumulus.UseCumulusForecast = settings.Forecast.usecumulusforecast;
					if (cumulus.UseCumulusForecast)
					{
						cumulus.FChighpress = settings.Forecast.highpressureextreme;
						cumulus.FClowpress = settings.Forecast.lowpressureextreme;
						cumulus.HourlyForecast = settings.Forecast.updatehourly;
						cumulus.FCpressinMB = (settings.Forecast.pressureunit == "mb/hPa");
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Forecast settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Location
				try
				{
					cumulus.Altitude = settings.general.Location.altitude;
					cumulus.AltitudeInFeet = (settings.general.Location.altitudeunit == "feet");
					cumulus.LocationName = settings.general.Location.sitename ?? string.Empty;
					cumulus.LocationDesc = settings.general.Location.description ?? string.Empty;

					cumulus.Latitude = settings.general.Location.Latitude.degrees + (settings.general.Location.Latitude.minutes / 60.0) + (settings.general.Location.Latitude.seconds / 3600.0);
					if (settings.general.Location.Latitude.hemisphere == "South")
					{
						cumulus.Latitude = -cumulus.Latitude;
					}

					cumulus.LatTxt = string.Format("{0}&nbsp;{1:D2}&deg;&nbsp;{2:D2}&#39;&nbsp;{3:D2}&quot;", settings.general.Location.Latitude.hemisphere[0], settings.general.Location.Latitude.degrees, settings.general.Location.Latitude.minutes,
						settings.general.Location.Latitude.seconds);

					cumulus.Longitude = settings.general.Location.Longitude.degrees + (settings.general.Location.Longitude.minutes / 60.0) + (settings.general.Location.Longitude.seconds / 3600.0);
					if (settings.general.Location.Longitude.hemisphere == "West")
					{
						cumulus.Longitude = -cumulus.Longitude;
					}

					cumulus.LonTxt = string.Format("{0}&nbsp;{1:D2}&deg;&nbsp;{2:D2}&#39;&nbsp;{3:D2}&quot;", settings.general.Location.Longitude.hemisphere[0], settings.general.Location.Longitude.degrees, settings.general.Location.Longitude.minutes,
						settings.general.Location.Longitude.seconds);
				}
				catch (Exception ex)
				{
					var msg = "Error processing Location settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Options
				try
				{
					cumulus.StationOptions.UseZeroBearing = settings.Options.usezerobearing;
					cumulus.StationOptions.UseWind10MinAve = settings.Options.calcwindaverage;
					cumulus.StationOptions.UseSpeedForAvgCalc = settings.Options.usespeedforavg;
					cumulus.StationOptions.Humidity98Fix = settings.Options.use100for98hum;
					cumulus.StationOptions.CalculatedDP = settings.Options.calculatedewpoint;
					cumulus.StationOptions.CalculatedWC = settings.Options.calculatewindchill;
					cumulus.StationOptions.SyncTime = settings.Options.syncstationclock;
					cumulus.StationOptions.ClockSettingHour = settings.Options.syncclockhour;
					cumulus.StationOptions.UseCumulusPresstrendstr = settings.Options.cumuluspresstrendnames;
					cumulus.StationOptions.LogExtraSensors = settings.Options.extrasensors;
					cumulus.StationOptions.WS2300IgnoreStationClock = settings.Options.ignorelacrosseclock;
					cumulus.StationOptions.RoundWindSpeed = settings.Options.roundwindspeeds;
					cumulus.StationOptions.NoSensorCheck = settings.Options.nosensorcheck;

					cumulus.StationOptions.AvgBearingMinutes = settings.Options.advanced.avgbearingmins;
					cumulus.StationOptions.AvgSpeedMinutes = settings.Options.advanced.avgspeedmins;
					cumulus.StationOptions.PeakGustMinutes = settings.Options.advanced.peakgustmins;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Options settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Log rollover
				try
				{
					cumulus.RolloverHour = settings.general.logrollover.time == "9am" ? 9 : 0;
					if (cumulus.RolloverHour == 9)
						cumulus.Use10amInSummer = settings.general.logrollover.summer10am;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Log rollover settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis VP/VP2/Vue
				try
				{
					if (settings.davisvp2 != null)
					{
						cumulus.DavisOptions.ReadReceptionStats = settings.davisvp2.readreceptionstats;
						cumulus.DavisOptions.SetLoggerInterval = settings.davisvp2.setloggerinterval;
						cumulus.DavisOptions.UseLoop2 = settings.davisvp2.advanced.useloop2;
						cumulus.DavisOptions.ForceVPBarUpdate = settings.davisvp2.advanced.vp1minbarupdate;
						cumulus.DavisOptions.InitWaitTime = settings.davisvp2.advanced.initwaittime;
						cumulus.DavisOptions.IPResponseTime = settings.davisvp2.advanced.ipresponsetime;
						cumulus.DavisOptions.BaudRate = settings.davisvp2.advanced.baudrate;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Davis VP/VP2/Vue settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WLL
				try
				{
					if (settings.daviswll != null)
					{
						cumulus.WLLAutoUpdateIpAddress = settings.daviswll.network.autoDiscover;
						cumulus.WllApiKey = settings.daviswll.api.apiKey;
						cumulus.WllApiSecret = settings.daviswll.api.apiSecret;
						cumulus.WllStationId = settings.daviswll.api.apiStationId;

						cumulus.WllPrimaryRain = settings.daviswll.primary.rain;
						cumulus.WllPrimarySolar = settings.daviswll.primary.solar;
						cumulus.WllPrimaryTempHum = settings.daviswll.primary.temphum;
						cumulus.WllPrimaryUV = settings.daviswll.primary.uv;
						cumulus.WllPrimaryWind = settings.daviswll.primary.wind;

						cumulus.WllExtraLeafTx1 = settings.daviswll.soilLeaf.extraLeaf.leafTx1;
						cumulus.WllExtraLeafTx2 = settings.daviswll.soilLeaf.extraLeaf.leafTx2;
						cumulus.WllExtraLeafIdx1 = settings.daviswll.soilLeaf.extraLeaf.leafIdx1;
						cumulus.WllExtraLeafIdx2 = settings.daviswll.soilLeaf.extraLeaf.leafIdx2;

						cumulus.WllExtraSoilMoistureIdx1 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistIdx1;
						cumulus.WllExtraSoilMoistureIdx2 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistIdx2;
						cumulus.WllExtraSoilMoistureIdx3 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistIdx3;
						cumulus.WllExtraSoilMoistureIdx4 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistIdx4;
						cumulus.WllExtraSoilMoistureTx1 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistTx1;
						cumulus.WllExtraSoilMoistureTx2 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistTx2;
						cumulus.WllExtraSoilMoistureTx3 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistTx3;
						cumulus.WllExtraSoilMoistureTx4 = settings.daviswll.soilLeaf.extraSoilMoist.soilMoistTx4;

						cumulus.WllExtraSoilTempIdx1 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempIdx1;
						cumulus.WllExtraSoilTempIdx2 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempIdx2;
						cumulus.WllExtraSoilTempIdx3 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempIdx3;
						cumulus.WllExtraSoilTempIdx4 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempIdx4;
						cumulus.WllExtraSoilTempTx1 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempTx1;
						cumulus.WllExtraSoilTempTx2 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempTx2;
						cumulus.WllExtraSoilTempTx3 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempTx3;
						cumulus.WllExtraSoilTempTx4 = settings.daviswll.soilLeaf.extraSoilTemp.soilTempTx4;

						cumulus.WllExtraTempTx[0] = settings.daviswll.extraTemp.extraTempTx1;
						cumulus.WllExtraTempTx[1] = settings.daviswll.extraTemp.extraTempTx2;
						cumulus.WllExtraTempTx[2] = settings.daviswll.extraTemp.extraTempTx3;
						cumulus.WllExtraTempTx[3] = settings.daviswll.extraTemp.extraTempTx4;
						cumulus.WllExtraTempTx[4] = settings.daviswll.extraTemp.extraTempTx5;
						cumulus.WllExtraTempTx[5] = settings.daviswll.extraTemp.extraTempTx6;
						cumulus.WllExtraTempTx[6] = settings.daviswll.extraTemp.extraTempTx7;
						cumulus.WllExtraTempTx[7] = settings.daviswll.extraTemp.extraTempTx8;

						cumulus.WllExtraHumTx[0] = settings.daviswll.extraTemp.extraHumTx1;
						cumulus.WllExtraHumTx[1] = settings.daviswll.extraTemp.extraHumTx2;
						cumulus.WllExtraHumTx[2] = settings.daviswll.extraTemp.extraHumTx3;
						cumulus.WllExtraHumTx[3] = settings.daviswll.extraTemp.extraHumTx4;
						cumulus.WllExtraHumTx[4] = settings.daviswll.extraTemp.extraHumTx5;
						cumulus.WllExtraHumTx[5] = settings.daviswll.extraTemp.extraHumTx6;
						cumulus.WllExtraHumTx[6] = settings.daviswll.extraTemp.extraHumTx7;
						cumulus.WllExtraHumTx[7] = settings.daviswll.extraTemp.extraHumTx8;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WLL settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// log interval
				try
				{
					cumulus.DataLogInterval = settings.general.loginterval;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Log interval setting: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// com port
				try
				{
					cumulus.ComportName = settings.general.comportname ?? cumulus.ComportName;
				}
				catch (Exception ex)
				{
					var msg = "Error processing COM port setting: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis Common details
				try
				{
					if (settings.daviscommon != null)
					{
						cumulus.DavisOptions.ConnectionType = settings.daviscommon.davisconn.conntype;
						if (settings.daviscommon.davisconn.tcpsettings != null)
						{
						cumulus.DavisOptions.IPAddr = settings.daviscommon.davisconn.tcpsettings.ipaddress ?? string.Empty;
						cumulus.DavisOptions.TCPPort = settings.daviscommon.davisconn.tcpsettings.tcpport;
						cumulus.DavisOptions.PeriodicDisconnectInterval = settings.daviscommon.davisconn.tcpsettings.disconperiod;
						cumulus.DavisOptions.RainGaugeType = settings.daviscommon.advanced.raingaugetype;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Davis settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// GW1000 connection details
				try
				{
					if (settings.gw1000 != null)
					{
						cumulus.Gw1000IpAddress = settings.gw1000.ipaddress;
						cumulus.Gw1000AutoUpdateIpAddress = settings.gw1000.autoDiscover;
						cumulus.Gw1000MacAddress = settings.gw1000.macaddress;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing GW1000 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// EasyWeather
				try
				{
					if (settings.easyw != null)
					{
						cumulus.EwOptions.Interval = settings.easyw.interval;
						cumulus.EwOptions.Filename = settings.easyw.filename;
						cumulus.EwOptions.MinPressMB = settings.easyw.minpressmb;
						cumulus.EwOptions.MaxPressMB = settings.easyw.maxpressmb;
						cumulus.EwOptions.MaxRainTipDiff = settings.easyw.raintipdiff;
						cumulus.EwOptions.PressOffset = settings.easyw.pressoffset;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing EasyWeather settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// FineOffset
				try
				{
					if (settings.fineoffset != null)
					{
						cumulus.FineOffsetOptions.FineOffsetSyncReads = settings.fineoffset.syncreads;
						cumulus.FineOffsetOptions.FineOffsetReadAvoidPeriod = settings.fineoffset.readavoid;
						cumulus.FineOffsetOptions.FineOffsetReadTime = settings.fineoffset.advanced.readtime;
						cumulus.FineOffsetOptions.VendorID = settings.fineoffset.advanced.vid;
						cumulus.FineOffsetOptions.ProductID = settings.fineoffset.advanced.pid;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Fine Offset settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Instromet
				try
				{
					if (settings.imet != null)
					{
						cumulus.ImetOptions.ImetBaudRate = settings.imet.baudrate;
						cumulus.ImetOptions.ImetReadDelay = settings.imet.advanced.readdelay;
						cumulus.ImetOptions.ImetWaitTime = settings.imet.advanced.waittime;
						cumulus.ImetOptions.ImetUpdateLogPointer = settings.imet.advanced.updatelogpointer;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Instromet settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Units
				try
				{
					cumulus.Units.Wind = settings.general.units.wind;
					cumulus.Units.Press = settings.general.units.pressure;
					cumulus.TempUnit = settings.general.units.temp;
					cumulus.RainUnit = settings.general.units.rain;
					cumulus.SetupUnitText();

					cumulus.AirQualityDPlaces = settings.general.units.advanced.airqulaitydp;
					cumulus.PressDPlaces = settings.general.units.advanced.pressdp;
					cumulus.RainDPlaces = settings.general.units.advanced.raindp;
					cumulus.SunshineDPlaces = settings.general.units.advanced.sunshinedp;
					cumulus.TempDPlaces = settings.general.units.advanced.tempdp;
					cumulus.UVDPlaces = settings.general.units.advanced.uvdp;
					cumulus.WindAvgDPlaces = settings.general.units.advanced.windavgdp;
					cumulus.WindDPlaces = settings.general.units.advanced.winddp;
					cumulus.WindRunDPlaces = settings.general.units.advanced.windrundp;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Units settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Station type
				try
				{
					if (cumulus.StationType != settings.general.stationtype)
					{
						cumulus.LogMessage("Station type changed, restart required");
						cumulus.LogConsoleMessage("*** Station type changed, restart required ***");
					}
					cumulus.StationType = settings.general.stationtype;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Station Type setting: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		internal string FtpNow(IHttpContext context)
		{
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();
				var json = WebUtility.UrlDecode(data);

				// Dead simple (dirty), there is only one setting at present!
				var includeGraphs = json.Contains("true");

				if (string.IsNullOrEmpty(cumulus.FtpHostname))
					return "{\"result\":\"No FTP host defined\"}";


				if (cumulus.WebUpdating == 1)
				{
					cumulus.LogMessage("FTP Now: Warning, a previous web update is still in progress, first chance, skipping attempt");
					return "{\"result\":\"A web update is already in progress\"}";
				}

				if (cumulus.WebUpdating >= 2)
				{
					cumulus.LogMessage("FTP Now: Warning, a previous web update is still in progress, second chance, aborting connection");
					if (cumulus.ftpThread.ThreadState == ThreadState.Running)
						cumulus.ftpThread.Abort();

					// If enabled (re)generate the daily graph data files, and upload
					if (includeGraphs && cumulus.IncludeGraphDataFiles)
					{
						cumulus.LogDebugMessage("FTP Now: Generating the daily graph data files");
						station.CreateEodGraphDataFiles();
						cumulus.DailyGraphDataFilesNeedFTP = true;
					}

					cumulus.LogMessage("FTP Now: Trying new web update");
					cumulus.WebUpdating = 1;
					cumulus.ftpThread = new Thread(cumulus.DoHTMLFiles) { IsBackground = true };
					cumulus.ftpThread.Start();
					return "{\"result\":\"An existing FTP process was aborted, and a new FTP process invoked\"}";
				}

				// If enabled (re)generate the daily graph data files, and upload
				if (includeGraphs && cumulus.IncludeGraphDataFiles)
				{
					cumulus.LogDebugMessage("FTP Now: Generating the daily graph data files");
					station.CreateEodGraphDataFiles();
					cumulus.DailyGraphDataFilesNeedFTP = true;
				}

				cumulus.WebUpdating = 1;
				cumulus.ftpThread = new Thread(cumulus.DoHTMLFiles) { IsBackground = true };
				cumulus.ftpThread.Start();
				return "{\"result\":\"FTP process invoked\"}";
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"FTP Now: {ex.Message}");
				context.Response.StatusCode = 500;
				return $"{{\"result\":\"Error: {ex.Message}\"}}";
			}
		}

		internal string SetSelectaChartOptions(IHttpContext context)
		{
			var errorMsg = "";
			context.Response.StatusCode = 200;
			// get the response
			try
			{
				cumulus.LogMessage("Updating select-a-chart settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				var json = WebUtility.UrlDecode(data);

				// de-serialize it to the settings structure
				var settings = JsonSerializer.DeserializeFromString<JsonSelectaChartSettings>(json);

				// process the settings
				try
				{
					cumulus.SelectaChartOptions.series = settings.series;
					cumulus.SelectaChartOptions.colours = settings.colours;
				}
				catch (Exception ex)
				{
					var msg = "Error select-a-chart Options: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}


		internal string GetWSport()
		{
			return "{\"wsport\":\"" + cumulus.wsPort + "\"}";
		}

		internal string GetVersion()
		{
			return "{\"Version\":\"" + cumulus.Version + "\",\"Build\":\"" + cumulus.Build + "\"}";
		}
	}

	internal class JsonStationSettingsData
	{
		public int stationid { get; set; }
		public JsonStationGeneral general { get; set; }
		public JsonStationSettingsDavisCommon daviscommon { set; get; }
		public JsonStationSettingsDavisVp2 davisvp2 { get; set; }
		public JSonStationSettingsGw1000Conn gw1000 { get; set; }
		public JsonStationSettingsWLL daviswll { get; set; }
		public JsonStationSettingsFineOffset fineoffset { get; set; }
		public JsonStationSettingsEasyWeather easyw { get; set; }
		public JsonStationSettingsImet imet { get; set; }
		public JsonStationSettingsOptions Options { get; set; }
		public JsonStationSettingsForecast Forecast { get; set; }
		public JsonStationSettingsSolar Solar { get; set; }
		public JsonStationSettingsAnnualRainfall AnnualRainfall { get; set; }
		public JsonStationSettingsGraphs Graphs { get; set; }
	}

	internal class JsonStationGeneral
	{
		public int stationtype { get; set; }
		public string comportname { get; set; }
		public int loginterval { get; set; }
		public JsonStationSettingsLogRollover logrollover { get; set; }
		public JsonStationSettingsUnits units { get; set; }
		public JsonStationSettingsLocation Location { get; set; }
	}


	internal class JsonStationSettingsUnitsAdvanced
	{
		public int uvdp { get; set; }
		public int raindp { get; set; }
		public int tempdp { get; set; }
		public int pressdp { get; set; }
		public int winddp { get; set; }
		public int windavgdp { get; set; }
		public int windrundp { get; set; }
		public int sunshinedp { get; set; }
		public int airqulaitydp { get; set; }

	}

	internal class JsonStationSettingsUnits
	{
		public int wind { get; set; }
		public int pressure { get; set; }
		public int temp { get; set; }
		public int rain { get; set; }
		public JsonStationSettingsUnitsAdvanced advanced { get; set; }
	}

	internal class JsonStationSettingsOptionsAdvanced
	{
		public int avgbearingmins { get; set; }
		public int avgspeedmins { get; set; }
		public int peakgustmins { get; set; }
	}

	internal class JsonStationSettingsOptions
	{
		public bool usezerobearing { get; set; }
		public bool calcwindaverage { get; set; }
		public bool usespeedforavg { get; set; }
		public bool use100for98hum { get; set; }
		public bool calculatedewpoint { get; set; }
		public bool calculatewindchill { get; set; }
		public bool syncstationclock { get; set; }
		public int syncclockhour { get; set; }
		public bool cumuluspresstrendnames { get; set; }
		public bool roundwindspeeds { get; set; }
		public bool ignorelacrosseclock { get; set; }
		public bool extrasensors { get; set; }
		public bool debuglogging { get; set; }
		public bool datalogging { get; set; }
		public bool stopsecondinstance { get; set; }
		public bool nosensorcheck { get; set; }
		public JsonStationSettingsOptionsAdvanced advanced { get; set; }
	}

	internal class JsonStationSettingsTCPsettings
	{
		public string ipaddress { get; set; }
		public int tcpport { get; set; }
		public int disconperiod { get; set; }
	}

	internal class JsonStationSettingsDavisCommon
	{
		public JsonStationSettingsDavisConn davisconn { get; set; }
		public JsonStationSettingsDavisCommonAdvanced advanced { get; set; }
	}

	internal class JsonStationSettingsDavisConn
	{
		public int conntype { get; set; }
		public JsonStationSettingsTCPsettings tcpsettings { get; set; }
	}

	internal class JsonStationSettingsDavisCommonAdvanced
	{
		public int raingaugetype { get; set; }
	}

	internal class JsonStationSettingsDavisVp2
	{
		public bool readreceptionstats { get; set; }
		public bool setloggerinterval { get; set; }
		public JsonStationSettingsDavisVp2Advanced advanced { get; set; }
	}

	internal class JsonStationSettingsDavisVp2Advanced
	{
		public bool useloop2 { get; set; }
		public bool vp1minbarupdate { get; set; }
		public int initwaittime { get; set; }
		public int ipresponsetime { get; set; }
		public int baudrate { get; set; }
	}

	internal class JsonStationSettingsFineOffsetAdvanced
	{
		public int readtime { get; set; }
		public int vid { get; set; }
		public int pid { get; set; }
	}

	internal class JsonStationSettingsFineOffset
	{
		public bool syncreads { get; set; }
		public int readavoid { get; set; }
		public JsonStationSettingsFineOffsetAdvanced advanced { get; set; }
	}

	internal class JsonStationSettingsEasyWeather
	{
		public double interval { get; set; }
		public string filename { get; set; }
		public int minpressmb { get; set; }
		public int maxpressmb { get; set; }
		public int raintipdiff { get; set; }
		public double pressoffset { get; set; }
	}

	internal class JSonStationSettingsGw1000Conn
	{
		public string ipaddress { get; set; }
		public bool autoDiscover { get; set; }
		public string macaddress { get; set; }
	}

	internal class JsonStationSettingsImet
	{
		public int baudrate { get; set; }
		public JsonStationSettingsImetAdvanced advanced { get; set; }
	}

	internal class JsonStationSettingsImetAdvanced
	{
		public int waittime { get; set; }
		public int readdelay { get; set; }
		public bool updatelogpointer { get; set; }
	}

	internal class JsonStationSettingsLogRollover
	{
		public string time { get; set; }
		public bool summer10am { get; set; }
	}

	internal class JsonStationSettingsLatLong
	{
		public int degrees { get; set; }
		public int minutes { get; set; }
		public int seconds { get; set; }
		public string hemisphere { get; set; }
	}

	internal class JsonStationSettingsLocation
	{
		public JsonStationSettingsLatLong Latitude { get; set; }
		public JsonStationSettingsLatLong Longitude { get; set; }
		public int altitude { get; set; }
		public string altitudeunit { get; set; }
		public string sitename { get; set; }
		public string description { get; set; }
	}

	internal class JsonStationSettingsForecast
	{
		public bool usecumulusforecast { get; set; }
		public bool updatehourly { get; set; }
		public double lowpressureextreme { get; set; }
		public double highpressureextreme { get; set; }
		public string pressureunit { get; set; }
	}

	internal class JsonStationSettingsSolar
	{
		public int sunthreshold { get; set; }
		public int solarmin { get; set; }
		public double transfactor { get; set; }
		public int solarcalc { get; set; }

		public double turbidity { get; set; }
	}

	internal class JsonStationSettingsWLL
	{
		public JsonStationSettingsWLLNetwork network { get; set; }
		public JsonStationSettingsWLLApi api { get; set; }
		public JsonStationSettingsWllPrimary primary { get; set; }
		public JsonStationSettingsWllSoilLeaf soilLeaf { get; set; }
		public JsonStationSettingsWllExtraTemp extraTemp { get; set; }
	}

	internal class JsonStationSettingsWLLNetwork
	{
		public bool autoDiscover { get; set; }
	}

	internal class JsonStationSettingsWLLApi
	{
		public string apiKey { get; set; }
		public string apiSecret { get; set; }
		public int apiStationId { get; set; }
	}

	internal class JsonStationSettingsWllPrimary
	{
		public int wind { get; set; }
		public int temphum { get; set; }
		public int rain { get; set; }
		public int solar { get; set; }
		public int uv { get; set; }
	}

	internal class JsonStationSettingsWllSoilLeaf
	{
		public JsonStationSettingsWllSoilTemp extraSoilTemp { get; set; }
		public JsonStationSettingsWllSoilMoist extraSoilMoist { get; set; }
		public JsonStationSettingsWllExtraLeaf extraLeaf { get; set; }
	}

	internal class JsonStationSettingsWllSoilTemp
	{
		public int soilTempTx1 { get; set; }
		public int soilTempIdx1 { get; set; }
		public int soilTempTx2 { get; set; }
		public int soilTempIdx2 { get; set; }
		public int soilTempTx3 { get; set; }
		public int soilTempIdx3 { get; set; }
		public int soilTempTx4 { get; set; }
		public int soilTempIdx4 { get; set; }
	}

	internal class JsonStationSettingsWllSoilMoist
	{
		public int soilMoistTx1 { get; set; }
		public int soilMoistIdx1 { get; set; }
		public int soilMoistTx2 { get; set; }
		public int soilMoistIdx2 { get; set; }
		public int soilMoistTx3 { get; set; }
		public int soilMoistIdx3 { get; set; }
		public int soilMoistTx4 { get; set; }
		public int soilMoistIdx4 { get; set; }
	}

	internal class JsonStationSettingsWllExtraLeaf
	{
		public int leafTx1 { get; set; }
		public int leafIdx1 { get; set; }
		public int leafTx2 { get; set; }
		public int leafIdx2 { get; set; }
	}

	public class JsonStationSettingsWllExtraTemp
	{
		public int extraTempTx1 { get; set; }
		public int extraTempTx2 { get; set; }
		public int extraTempTx3 { get; set; }
		public int extraTempTx4 { get; set; }
		public int extraTempTx5 { get; set; }
		public int extraTempTx6 { get; set; }
		public int extraTempTx7 { get; set; }
		public int extraTempTx8 { get; set; }

		public bool extraHumTx1 { get; set; }
		public bool extraHumTx2 { get; set; }
		public bool extraHumTx3 { get; set; }
		public bool extraHumTx4 { get; set; }
		public bool extraHumTx5 { get; set; }
		public bool extraHumTx6 { get; set; }
		public bool extraHumTx7 { get; set; }
		public bool extraHumTx8 { get; set; }
	}

	public class JsonStationSettingsAnnualRainfall
	{
		public double ytdamount { get; set; }
		public int ytdyear { get; set; }
		public int rainseasonstart { get; set; }
	}

	public class JsonStationSettingsGraphs
	{
		public int graphhours { get; set; }
		public int graphdays { get; set; }

		public JsonStationSettingsGraphVisibility datavisibility { get; set; }
	}

	public class JsonStationSettingsGraphVisibility
	{
		public JsonStationSettingsGraphDataTemperature temperature { get; set; }
		public JsonStationSettingsGraphDataHumidity humidity { get; set; }
		public JsonStationSettingsGraphDataSolar solar { get; set; }
	}

	public class JsonStationSettingsGraphDataTemperature
	{
		public bool graphTempVis { get; set; }
		public bool graphInTempVis { get; set; }
		public bool graphHeatIndexVis { get; set; }
		public bool graphDewPointVis { get; set; }
		public bool graphWindChillVis { get; set; }
		public bool graphAppTempVis { get; set; }
		public bool graphFeelsLikeVis { get; set; }
		public bool graphHumidexVis { get; set; }
	}

	public class JsonStationSettingsGraphDataHumidity
	{
		public bool graphHumVis { get; set; }
		public bool graphInHumVis { get; set; }
	}

	public class JsonStationSettingsGraphDataSolar
	{
		public bool graphUvVis { get; set; }
		public bool graphSolarVis { get; set; }
		public bool graphSunshineVis { get; set; }
	}

	public class JsonSelectaChartSettings
	{
		public string[] series { get; set; }
		public string[] colours { get; set; }
	}
}
