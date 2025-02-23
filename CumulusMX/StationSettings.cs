using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using EmbedIO;

using ServiceStack;
using ServiceStack.Text;


namespace CumulusMX
{
	public class StationSettings
	{
		private readonly Cumulus cumulus;
		private WeatherStation station;

		internal StationSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		internal void SetStation(WeatherStation station)
		{
			this.station = station;
		}

		internal string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it

			// Common > Advanced Settings
			var optionsAdv = new JsonOptionsAdvanced()
			{
				usespeedforavg = cumulus.StationOptions.UseSpeedForAvgCalc,
				avgbearingmins = cumulus.StationOptions.AvgBearingMinutes,
				avgspeedmins = cumulus.StationOptions.AvgSpeedMinutes,
				peakgustmins = cumulus.StationOptions.PeakGustMinutes,
				maxwind = cumulus.LCMaxWind,
				recordtimeout = cumulus.RecordSetTimeoutHrs,
				snowdepthhour = cumulus.SnowDepthHour,
				raindaythreshold = cumulus.RainDayThreshold,
				uselogger = cumulus.StationOptions.UseDataLogger
			};

			// Common Settings
			var options = new JsonOptions()
			{
				usezerobearing = cumulus.StationOptions.UseZeroBearing,
				calcwindaverage = cumulus.StationOptions.CalcuateAverageWindSpeed,
				use100for98hum = cumulus.StationOptions.Humidity98Fix,
				calculatedewpoint = cumulus.StationOptions.CalculatedDP,
				calculatewindchill = cumulus.StationOptions.CalculatedWC,
				calculateet = cumulus.StationOptions.CalculatedET,
				calculateslp = cumulus.StationOptions.CalculateSLP,
				cumuluspresstrendnames = cumulus.StationOptions.UseCumulusPresstrendstr,
				extrasensors = cumulus.StationOptions.LogExtraSensors,
				ignorelacrosseclock = cumulus.StationOptions.WS2300IgnoreStationClock,
				roundwindspeeds = cumulus.StationOptions.RoundWindSpeed,
				nosensorcheck = cumulus.StationOptions.NoSensorCheck,
				leafwetisrainingidx = cumulus.StationOptions.LeafWetnessIsRainingIdx,
				leafwetisrainingthrsh = cumulus.StationOptions.LeafWetnessIsRainingThrsh,
				userainforisraining = cumulus.StationOptions.UseRainForIsRaining,
				snowseasonstart = cumulus.SnowSeasonStart,
				advanced = optionsAdv
			};

			// Units > Advanced
			var unitsAdv = new JsonUnitsAdvanced
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

			// Units
			var units = new JsonUnits()
			{
				wind = cumulus.Units.Wind,
				pressure = cumulus.Units.Press,
				temp = cumulus.Units.Temp,
				rain = cumulus.Units.Rain,
				cloudbaseft = cumulus.CloudBaseInFeet,
				snow = cumulus.Units.SnowDepth,
				laser = cumulus.Units.LaserDistance,
				advanced = unitsAdv
			};

			var tcpsettings = new JsonTCPsettings()
			{
				ipaddress = cumulus.DavisOptions.IPAddr,
				disconperiod = cumulus.DavisOptions.PeriodicDisconnectInterval
			};


			var davisvp2advanced = new JsonsDavisVp2Advanced()
			{
				syncstationclock = cumulus.StationOptions.SyncTime,
				syncclockhour = cumulus.StationOptions.ClockSettingHour,
				useloop2 = cumulus.DavisOptions.UseLoop2,
				raingaugetype = cumulus.DavisOptions.RainGaugeType,
				vp1minbarupdate = cumulus.DavisOptions.ForceVPBarUpdate,
				initwaittime = cumulus.DavisOptions.InitWaitTime,
				ipresponsetime = cumulus.DavisOptions.IPResponseTime,
				baudrate = cumulus.DavisOptions.BaudRate,
				readreceptionstats = cumulus.DavisOptions.ReadReceptionStats,
				tcpport = cumulus.DavisOptions.TCPPort,
				setloggerinterval = cumulus.DavisOptions.SetLoggerInterval
			};

			var davisvp2conn = new JsonDavisVp2Connection()
			{
				conntype = cumulus.DavisOptions.ConnectionType,
				comportname = cumulus.ComportName,
				tcpsettings = tcpsettings
			};

			var davisvp2 = new JsonDavisVp2()
			{
				davisconn = davisvp2conn,
				advanced = davisvp2advanced
			};

			var weatherflow = new JsonWeatherFlow()
			{
				deviceid = cumulus.WeatherFlowOptions.WFDeviceId,
				tcpport = cumulus.WeatherFlowOptions.WFTcpPort,
				token = cumulus.WeatherFlowOptions.WFToken,
				dayshistory = cumulus.WeatherFlowOptions.WFDaysHist
			};

			var ecowittmaps = new JsonEcowittMappings()
			{
				primaryTHsensor = cumulus.Gw1000PrimaryTHSensor,
				primaryRainSensor = cumulus.Gw1000PrimaryRainSensor,
				piezosrain = cumulus.EcowittIsRainingUsePiezo,
				wn34chan1 = cumulus.EcowittMapWN34[1],
				wn34chan2 = cumulus.EcowittMapWN34[2],
				wn34chan3 = cumulus.EcowittMapWN34[3],
				wn34chan4 = cumulus.EcowittMapWN34[4],
				wn34chan5 = cumulus.EcowittMapWN34[5],
				wn34chan6 = cumulus.EcowittMapWN34[6],
				wn34chan7 = cumulus.EcowittMapWN34[7],
				wn34chan8 = cumulus.EcowittMapWN34[8]
			};

			var gw1000 = new JsonGw1000Conn()
			{
				ipaddress = cumulus.Gw1000IpAddress,
				autoDiscover = cumulus.Gw1000AutoUpdateIpAddress,
				macaddress = cumulus.Gw1000MacAddress,
			};

			var ecowittHttpApi = new JsonHttpApi()
			{
				ipaddress = cumulus.Gw1000IpAddress,
				password = cumulus.EcowittHttpPassword,
				usesdcard = cumulus.EcowittUseSdCard
			};

			var ecowitt = new JsonEcowitt
			{
				setcustom = cumulus.EcowittSetCustomServer,
				gwaddr = cumulus.EcowittGatewayAddr,
				localaddr = cumulus.EcowittLocalAddr,
				interval = cumulus.EcowittCustomInterval,
				forward = []
			};

			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.EcowittForwarders[i]))
				{
					ecowitt.forward.Add(new JsonEcowittForwardList() { url = cumulus.EcowittForwarders[i] });
				}
			}

			var ecowittapi = new JsonEcowittApi()
			{
				applicationkey = cumulus.EcowittApplicationKey,
				userkey = cumulus.EcowittUserApiKey,
				mac = cumulus.EcowittMacAddress
			};

			var logrollover = new JsonLogRollover()
			{
				time = cumulus.RolloverHour == 9 ? "9am" : "midnight",
				summer10am = cumulus.Use10amInSummer
			};

			var fineoffsetadvanced = new JsonFineOffsetAdvanced()
			{
				readtime = cumulus.FineOffsetOptions.ReadTime,
				setlogger = cumulus.FineOffsetOptions.SetLoggerInterval,
				vid = cumulus.FineOffsetOptions.VendorID,
				pid = cumulus.FineOffsetOptions.ProductID
			};

			var fineoffset = new JsonFineOffset()
			{
				syncreads = cumulus.FineOffsetOptions.SyncReads,
				readavoid = cumulus.FineOffsetOptions.ReadAvoidPeriod,
				advanced = fineoffsetadvanced
			};

			var easyweather = new JsonEasyWeather()
			{
				interval = cumulus.EwOptions.Interval,
				filename = cumulus.EwOptions.Filename,
				minpressmb = cumulus.EwOptions.MinPressMB,
				maxpressmb = cumulus.EwOptions.MaxPressMB,
				raintipdiff = cumulus.EwOptions.MaxRainTipDiff,
				pressoffset = cumulus.EwOptions.PressOffset
			};

			var wmr928 = new JsonWmr928()
			{
				comportname = cumulus.ComportName
			};

			var imetAdvanced = new JsonImetAdvanced()
			{
				syncstationclock = cumulus.StationOptions.SyncTime,
				syncclockhour = cumulus.StationOptions.ClockSettingHour,
				readdelay = cumulus.ImetOptions.ReadDelay,
				waittime = cumulus.ImetOptions.WaitTime,
				updatelogpointer = cumulus.ImetOptions.UpdateLogPointer
			};

			var imet = new JsonImet()
			{
				comportname = cumulus.ComportName,
				baudrate = cumulus.ImetOptions.BaudRate,
				advanced = imetAdvanced
			};

			int deg, min, sec;
			string hem;

			LatToDMS(cumulus.Latitude, out deg, out min, out sec, out hem);

			var latitude = new JsonLatLong() { degrees = deg, minutes = min, seconds = sec, hemisphere = hem };

			LongToDMS(cumulus.Longitude, out deg, out min, out sec, out hem);

			var longitude = new JsonLatLong() { degrees = deg, minutes = min, seconds = sec, hemisphere = hem };

			var location = new JsonLocation()
			{
				altitude = (int) cumulus.Altitude,
				altitudeunit = cumulus.AltitudeInFeet ? "feet" : "metres",
				description = cumulus.LocationDesc,
				Latitude = latitude,
				Longitude = longitude,
				sitename = cumulus.LocationName
			};

			var forecast = new JsonForecast()
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

			var solar = new JsonSolar()
			{
				solarmin = cumulus.SolarOptions.SolarMinimum,
				sunthreshold = cumulus.SolarOptions.SunThreshold,
				solarcalc = cumulus.SolarOptions.SolarCalc,
				transfactorJun = cumulus.SolarOptions.RStransfactorJun,
				transfactorDec = cumulus.SolarOptions.RStransfactorDec,
				turbidityJun = cumulus.SolarOptions.BrasTurbidityJun,
				turbidityDec = cumulus.SolarOptions.BrasTurbidityDec
			};

			var annualrainfall = new JsonAnnualRainfall()
			{
				rainseasonstart = cumulus.RainSeasonStart,
				rainweekstart = cumulus.RainWeekStart,
				ytdamount = cumulus.YTDrain,
				ytdyear = cumulus.YTDrainyear
			};

			var growingdd = new JsonGrowingDDSettings()
			{
				basetemp1 = cumulus.GrowingBase1,
				basetemp2 = cumulus.GrowingBase2,
				starts = cumulus.GrowingYearStarts,
				cap30C = cumulus.GrowingCap30C
			};

			var tempsum = new JsonTempSumSettings()
			{
				basetemp1 = cumulus.TempSumBase1,
				basetemp2 = cumulus.TempSumBase2,
				starts = cumulus.TempSumYearStarts
			};

			var chillhrs = new JsonChillHours()
			{
				threshold = cumulus.ChillHourThreshold,
				basetemp = cumulus.ChillHourBase,
				month = cumulus.ChillHourSeasonStart
			};

			var wllNetwork = new JsonWllNetwork()
			{
				autoDiscover = cumulus.WLLAutoUpdateIpAddress,
				ipaddress = cumulus.DavisOptions.IPAddr
			};

			var wllAdvanced = new JsonWllAdvanced()
			{
				raingaugetype = cumulus.DavisOptions.RainGaugeType,
				tcpport = cumulus.DavisOptions.TCPPort,
				datastopped = cumulus.WllTriggerDataStoppedOnBroadcast
			};

			var wllApi = new JsonWllApi()
			{
				apiKey = cumulus.WllApiKey,
				apiSecret = cumulus.WllApiSecret,
				apiStationId = cumulus.WllStationId,
				apiStationUuid = cumulus.WllStationUuid
			};

			var wllPrimary = new JsonWllPrimary()
			{
				wind = cumulus.WllPrimaryWind,
				temphum = cumulus.WllPrimaryTempHum,
				rain = cumulus.WllPrimaryRain,
				solar = cumulus.WllPrimarySolar,
				uv = cumulus.WllPrimaryUV
			};

			var wllExtraSoilTemp = new JsonWllSoilTemp()
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

			var wllExtraSoilMoist = new JsonWllSoilMoist()
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

			var wllExtraLeaf = new JsonWllExtraLeaf()
			{
				leafTx1 = cumulus.WllExtraLeafTx1,
				leafIdx1 = cumulus.WllExtraLeafIdx1,
				leafTx2 = cumulus.WllExtraLeafTx2,
				leafIdx2 = cumulus.WllExtraLeafIdx2
			};

			var wllSoilLeaf = new JsonWllSoilLeaf()
			{
				extraSoilTemp = wllExtraSoilTemp,
				extraSoilMoist = wllExtraSoilMoist,
				extraLeaf = wllExtraLeaf
			};

			var wllExtraTemp = new JsonWllExtraTemp();
			for (int i = 1; i <= 8; i++)
			{
				PropertyInfo propInfo = wllExtraTemp.GetType().GetProperty("extraTempTx" + i);
				propInfo.SetValue(wllExtraTemp, Convert.ChangeType(cumulus.WllExtraTempTx[i], propInfo.PropertyType), null);

				propInfo = wllExtraTemp.GetType().GetProperty("extraHumTx" + i);
				propInfo.SetValue(wllExtraTemp, Convert.ChangeType(cumulus.WllExtraHumTx[i], propInfo.PropertyType), null);
			}

			var wll = new JsonWll()
			{
				network = wllNetwork,
				api = wllApi,
				primary = wllPrimary,
				soilLeaf = wllSoilLeaf,
				extraTemp = wllExtraTemp,
				advanced = wllAdvanced
			};

			var generalAdvanced = new JsonAdvanced()
			{
				recsbegandate = cumulus.RecordsBeganDateTime.ToString("yyyy-MM-dd")
			};

			var general = new JsonGeneral()
			{
				manufacturer = (int) cumulus.Manufacturer,
				stationtype = cumulus.StationType,
				stationmodel = cumulus.StationModel,
				loginterval = cumulus.DataLogInterval,
				logrollover = logrollover,
				units = units,
				Location = location,
				advanced = generalAdvanced
			};

			var jsonstnadv = new JsonJsonStationAdvanced()
			{
				filedelay = cumulus.JsonStationOptions.FileReadDelay,
				fileignore = cumulus.JsonStationOptions.FileIgnoreTime,
				mqtttls = cumulus.JsonStationOptions.MqttUseTls
			};

			var jsonstn = new JsonJsonStation()
			{
				conntype = cumulus.JsonStationOptions.Connectiontype,
				filename = cumulus.JsonStationOptions.SourceFile,
				mqttserver = cumulus.JsonStationOptions.MqttServer,
				mqttport = cumulus.JsonStationOptions.MqttPort,
				mqttuser = cumulus.JsonStationOptions.MqttUsername,
				mqttpass = cumulus.JsonStationOptions.MqttPassword,
				mqtttopic = cumulus.JsonStationOptions.MqttTopic,
				advanced = jsonstnadv
			};

			var data = new JsonData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				stationid = cumulus.StationType,
				general = general,
				davisvp2 = davisvp2,
				daviswll = wll,
				gw1000 = gw1000,
				ecowitthttpapi = ecowittHttpApi,
				ecowitt = ecowitt,
				ecowittapi = ecowittapi,
				ecowittmaps = ecowittmaps,
				weatherflow = weatherflow,
				fineoffset = fineoffset,
				easyw = easyweather,
				imet = imet,
				wmr928 = wmr928,
				jsonstation = jsonstn,
				Options = options,
				Forecast = forecast,
				Solar = solar,
				AnnualRainfall = annualrainfall,
				GrowingDD = growingdd,
				TempSum = tempsum,
				ChillHrs = chillhrs
			};

			return JsonSerializer.SerializeToString(data);
		}

		private static void LongToDMS(decimal longitude, out int d, out int m, out int s, out string hem)
		{
			decimal coordinate;
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
			int secs = (int) (coordinate * 60 * 60);

			s = secs % 60;

			secs = (secs - s) / 60;

			m = secs % 60;
			d = secs / 60;
		}

		private static void LatToDMS(decimal latitude, out int d, out int m, out int s, out string hem)
		{
			decimal coordinate;
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

			int secs = (int) (coordinate * 60 * 60);

			s = secs % 60;

			secs = (secs - s) / 60;

			m = secs % 60;
			d = secs / 60;
		}

		internal string UpdateConfig(IHttpContext context)
		{
			var errorMsg = string.Empty;
			var json = string.Empty;
			context.Response.StatusCode = 200;
			JsonData settings;

			// get the response
			try
			{
				cumulus.LogMessage("Updating station settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json=" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.DeserializeFromString<JsonData>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Station Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Station Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				// Annual Rainfall
				try
				{
					cumulus.RainSeasonStart = settings.AnnualRainfall.rainseasonstart;
					cumulus.YTDrain = settings.AnnualRainfall.ytdamount;
					cumulus.YTDrainyear = settings.AnnualRainfall.ytdyear;
					if (cumulus.RainWeekStart != settings.AnnualRainfall.rainweekstart)
					{
						cumulus.RainWeekStart = settings.AnnualRainfall.rainweekstart;
						// we need to recalculate the rainfall
						station.UpdateWeekRainfall();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Rainfall settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Growing Degree Day
				try
				{
					cumulus.GrowingBase1 = settings.GrowingDD.basetemp1;
					cumulus.GrowingBase2 = settings.GrowingDD.basetemp2;
					cumulus.GrowingYearStarts = settings.GrowingDD.starts;
					cumulus.GrowingCap30C = settings.GrowingDD.cap30C;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Growing Degree Day settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Temp Sum
				try
				{
					cumulus.TempSumBase1 = settings.TempSum.basetemp1;
					cumulus.TempSumBase2 = settings.TempSum.basetemp2;
					cumulus.TempSumYearStarts = settings.TempSum.starts;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Temperature Sum settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Chill Hours
				try
				{
					cumulus.ChillHourThreshold = settings.ChillHrs.threshold;
					cumulus.ChillHourBase = settings.ChillHrs.basetemp;
					cumulus.ChillHourSeasonStart = settings.ChillHrs.month;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Chill Hours settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Solar
				try
				{
					if (settings.Solar != null)
					{
						cumulus.SolarOptions.SolarCalc = settings.Solar.solarcalc;
						cumulus.SolarOptions.SolarMinimum = settings.Solar.solarmin;
						cumulus.SolarOptions.SunThreshold = settings.Solar.sunthreshold;
						if (cumulus.SolarOptions.SolarCalc == 0)
						{
							cumulus.SolarOptions.RStransfactorJun = settings.Solar.transfactorJun;
							cumulus.SolarOptions.RStransfactorDec = settings.Solar.transfactorDec;
						}
						else
						{
							cumulus.SolarOptions.BrasTurbidityJun = settings.Solar.turbidityJun;
							cumulus.SolarOptions.BrasTurbidityDec = settings.Solar.turbidityDec;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Solar settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Location
				try
				{
					cumulus.Altitude = settings.general.Location.altitude;
					cumulus.AltitudeInFeet = (settings.general.Location.altitudeunit == "feet");
					cumulus.LocationName = string.IsNullOrWhiteSpace(settings.general.Location.sitename) ? null : settings.general.Location.sitename.Trim();
					cumulus.LocationDesc = string.IsNullOrWhiteSpace(settings.general.Location.description) ? null : settings.general.Location.description.Trim();

					cumulus.Latitude = (decimal) (settings.general.Location.Latitude.degrees + (settings.general.Location.Latitude.minutes / 60.0) + (settings.general.Location.Latitude.seconds / 3600.0));
					string northSouth = cumulus.Trans.compassp[0];
					if (settings.general.Location.Latitude.hemisphere == "South")
					{
						cumulus.Latitude = -cumulus.Latitude;
						northSouth = cumulus.Trans.compassp[8];
					}

					cumulus.LatTxt = string.Format("{0}&nbsp;{1:D2}&deg;&nbsp;{2:D2}&#39;&nbsp;{3:D2}&quot;", northSouth, settings.general.Location.Latitude.degrees, settings.general.Location.Latitude.minutes,
						settings.general.Location.Latitude.seconds);

					string eastWest = cumulus.Trans.compassp[4];
					cumulus.Longitude = (decimal) (settings.general.Location.Longitude.degrees + (settings.general.Location.Longitude.minutes / 60.0) + (settings.general.Location.Longitude.seconds / 3600.0));
					if (settings.general.Location.Longitude.hemisphere == "West")
					{
						cumulus.Longitude = -cumulus.Longitude;
						eastWest = cumulus.Trans.compassp[12];
					}

					cumulus.LonTxt = string.Format("{0}&nbsp;{1:D2}&deg;&nbsp;{2:D2}&#39;&nbsp;{3:D2}&quot;", eastWest, settings.general.Location.Longitude.degrees, settings.general.Location.Longitude.minutes,
						settings.general.Location.Longitude.seconds);
				}
				catch (Exception ex)
				{
					var msg = "Error processing Location settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Options
				try
				{
					cumulus.StationOptions.UseZeroBearing = settings.Options.usezerobearing;
					cumulus.StationOptions.CalcuateAverageWindSpeed = settings.Options.calcwindaverage;
					cumulus.StationOptions.Humidity98Fix = settings.Options.use100for98hum;
					cumulus.StationOptions.CalculatedDP = settings.Options.calculatedewpoint;
					cumulus.StationOptions.CalculatedWC = settings.Options.calculatewindchill;
					cumulus.StationOptions.CalculatedET = settings.Options.calculateet;
					cumulus.StationOptions.CalculateSLP = settings.Options.calculateslp;
					cumulus.StationOptions.UseCumulusPresstrendstr = settings.Options.cumuluspresstrendnames;
					cumulus.StationOptions.LogExtraSensors = settings.Options.extrasensors;
					cumulus.StationOptions.WS2300IgnoreStationClock = settings.Options.ignorelacrosseclock;
					cumulus.StationOptions.RoundWindSpeed = settings.Options.roundwindspeeds;
					cumulus.StationOptions.NoSensorCheck = settings.Options.nosensorcheck;
					cumulus.StationOptions.LeafWetnessIsRainingIdx = settings.Options.leafwetisrainingidx;
					cumulus.StationOptions.LeafWetnessIsRainingThrsh = settings.Options.leafwetisrainingthrsh;
					cumulus.StationOptions.UseRainForIsRaining = settings.Options.userainforisraining;
					cumulus.SnowSeasonStart = settings.Options.snowseasonstart;

					cumulus.StationOptions.UseSpeedForAvgCalc = settings.Options.advanced.usespeedforavg;
					cumulus.StationOptions.AvgBearingMinutes = settings.Options.advanced.avgbearingmins;
					cumulus.StationOptions.AvgSpeedMinutes = settings.Options.advanced.avgspeedmins;
					cumulus.StationOptions.PeakGustMinutes = settings.Options.advanced.peakgustmins;
					cumulus.StationOptions.UseDataLogger = settings.Options.advanced.uselogger;
					cumulus.LCMaxWind = settings.Options.advanced.maxwind;
					cumulus.RecordSetTimeoutHrs = settings.Options.advanced.recordtimeout;
					cumulus.SnowDepthHour = settings.Options.advanced.snowdepthhour;
					cumulus.RainDayThreshold = settings.Options.advanced.raindaythreshold;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Options settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Log roll-over
				try
				{
					cumulus.RolloverHour = settings.general.logrollover.time == "9am" ? 9 : 0;
					if (cumulus.RolloverHour == 9)
						cumulus.Use10amInSummer = settings.general.logrollover.summer10am;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Log roll-over settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis VP/VP2/Vue
				try
				{
					if (settings.davisvp2 != null)
					{
						cumulus.DavisOptions.ConnectionType = settings.davisvp2.davisconn.conntype;
						if (settings.davisvp2.davisconn.tcpsettings != null)
						{
							cumulus.DavisOptions.IPAddr = string.IsNullOrWhiteSpace(settings.davisvp2.davisconn.tcpsettings.ipaddress) ? null : settings.davisvp2.davisconn.tcpsettings.ipaddress.Trim();
							cumulus.DavisOptions.PeriodicDisconnectInterval = settings.davisvp2.davisconn.tcpsettings.disconperiod;
						}
						cumulus.DavisOptions.ReadReceptionStats = settings.davisvp2.advanced.readreceptionstats;
						cumulus.DavisOptions.SetLoggerInterval = settings.davisvp2.advanced.setloggerinterval;
						cumulus.DavisOptions.UseLoop2 = settings.davisvp2.advanced.useloop2;
						cumulus.DavisOptions.ForceVPBarUpdate = settings.davisvp2.advanced.vp1minbarupdate;
						cumulus.DavisOptions.RainGaugeType = settings.davisvp2.advanced.raingaugetype;
						cumulus.StationOptions.SyncTime = settings.davisvp2.advanced.syncstationclock;
						cumulus.StationOptions.ClockSettingHour = settings.davisvp2.advanced.syncclockhour;
						if (cumulus.DavisOptions.ConnectionType == 0)
						{
							cumulus.ComportName = string.IsNullOrWhiteSpace(settings.davisvp2.davisconn.comportname) ? null : settings.davisvp2.davisconn.comportname.Trim();
							cumulus.DavisOptions.BaudRate = settings.davisvp2.advanced.baudrate;
						}
						else // TCP/IP
						{
							cumulus.DavisOptions.InitWaitTime = settings.davisvp2.advanced.initwaittime;
							cumulus.DavisOptions.IPResponseTime = settings.davisvp2.advanced.ipresponsetime;
							cumulus.DavisOptions.TCPPort = settings.davisvp2.advanced.tcpport;
						}

					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Davis VP/VP2/Vue settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WLL/Davis Cloud
				try
				{
					if (settings.daviswll != null)
					{
						if (settings.general.stationtype == 11) // WLL only
						{
							cumulus.DavisOptions.ConnectionType = 2; // Always TCP/IP for WLL
							cumulus.WLLAutoUpdateIpAddress = settings.daviswll.network.autoDiscover;
							cumulus.DavisOptions.IPAddr = string.IsNullOrWhiteSpace(settings.daviswll.network.ipaddress) ? null : settings.daviswll.network.ipaddress.Trim();

							cumulus.DavisOptions.TCPPort = settings.daviswll.advanced.tcpport;
							cumulus.WllTriggerDataStoppedOnBroadcast = settings.daviswll.advanced.datastopped;
						}

						cumulus.WllApiKey = string.IsNullOrWhiteSpace(settings.daviswll.api.apiKey) ? null : settings.daviswll.api.apiKey.Trim();
						cumulus.WllApiSecret = string.IsNullOrWhiteSpace(settings.daviswll.api.apiSecret) ? null : settings.daviswll.api.apiSecret.Trim();
						cumulus.WllStationId = settings.daviswll.api.apiStationId;
						cumulus.WllStationUuid = settings.daviswll.api.apiStationUuid;

						if (settings.general.stationtype == 11 || settings.general.stationtype == 19) // WLL & Cloud WLL/WLC only
						{
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

							cumulus.WllExtraTempTx[1] = settings.daviswll.extraTemp.extraTempTx1;
							cumulus.WllExtraTempTx[2] = settings.daviswll.extraTemp.extraTempTx2;
							cumulus.WllExtraTempTx[3] = settings.daviswll.extraTemp.extraTempTx3;
							cumulus.WllExtraTempTx[4] = settings.daviswll.extraTemp.extraTempTx4;
							cumulus.WllExtraTempTx[5] = settings.daviswll.extraTemp.extraTempTx5;
							cumulus.WllExtraTempTx[6] = settings.daviswll.extraTemp.extraTempTx6;
							cumulus.WllExtraTempTx[7] = settings.daviswll.extraTemp.extraTempTx7;
							cumulus.WllExtraTempTx[8] = settings.daviswll.extraTemp.extraTempTx8;

							cumulus.WllExtraHumTx[1] = settings.daviswll.extraTemp.extraHumTx1;
							cumulus.WllExtraHumTx[2] = settings.daviswll.extraTemp.extraHumTx2;
							cumulus.WllExtraHumTx[3] = settings.daviswll.extraTemp.extraHumTx3;
							cumulus.WllExtraHumTx[4] = settings.daviswll.extraTemp.extraHumTx4;
							cumulus.WllExtraHumTx[5] = settings.daviswll.extraTemp.extraHumTx5;
							cumulus.WllExtraHumTx[6] = settings.daviswll.extraTemp.extraHumTx6;
							cumulus.WllExtraHumTx[7] = settings.daviswll.extraTemp.extraHumTx7;
							cumulus.WllExtraHumTx[8] = settings.daviswll.extraTemp.extraHumTx8;
						}

						cumulus.DavisOptions.RainGaugeType = settings.daviswll.advanced.raingaugetype;


						// Automatically enable extra logging?
						// Should we auto disable it too?
						if (cumulus.WllExtraLeafTx1 > 0 ||
							cumulus.WllExtraLeafTx2 > 0 ||
							cumulus.WllExtraSoilMoistureTx1 > 0 ||
							cumulus.WllExtraSoilMoistureTx2 > 0 ||
							cumulus.WllExtraSoilMoistureTx3 > 0 ||
							cumulus.WllExtraSoilMoistureTx4 > 0 ||
							cumulus.WllExtraSoilTempTx1 > 0 ||
							cumulus.WllExtraSoilTempTx2 > 0 ||
							cumulus.WllExtraSoilTempTx3 > 0 ||
							cumulus.WllExtraSoilTempTx4 > 0 ||
							Array.Exists(cumulus.WllExtraTempTx, el => el > 0)
							)
						{
							cumulus.StationOptions.LogExtraSensors = true;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WLL/Davis Cloud settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// GW1000 connection details
				try
				{
					if (settings.gw1000 != null)
					{
						cumulus.Gw1000IpAddress = string.IsNullOrWhiteSpace(settings.gw1000.ipaddress) ? null : settings.gw1000.ipaddress.Trim();
						cumulus.Gw1000AutoUpdateIpAddress = settings.gw1000.autoDiscover;
						cumulus.Gw1000MacAddress = string.IsNullOrWhiteSpace(settings.gw1000.macaddress) ? null : settings.gw1000.macaddress.Trim().ToUpper();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing GW1000 settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// HTTP Local API connection details
				try
				{
					if (settings.ecowitthttpapi != null)
					{
						cumulus.Gw1000IpAddress = string.IsNullOrWhiteSpace(settings.ecowitthttpapi.ipaddress) ? null : settings.ecowitthttpapi.ipaddress.Trim();
						cumulus.EcowittHttpPassword = string.IsNullOrWhiteSpace(settings.ecowitthttpapi.password) ? null : settings.ecowitthttpapi.password.Trim();
						cumulus.EcowittUseSdCard = settings.ecowitthttpapi.usesdcard;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt Local HTTP API settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ecowitt configuration details
				try
				{
					if (settings.ecowitt != null)
					{
						cumulus.EcowittSetCustomServer = settings.ecowitt.setcustom;
						cumulus.EcowittGatewayAddr = string.IsNullOrWhiteSpace(settings.ecowitt.gwaddr) ? null : settings.ecowitt.gwaddr.Trim();
						cumulus.EcowittLocalAddr = string.IsNullOrWhiteSpace(settings.ecowitt.localaddr) ? null : settings.ecowitt.localaddr.Trim();
						cumulus.EcowittCustomInterval = settings.ecowitt.interval;

						for (var i = 0; i < 10; i++)
						{
							if (i < settings.ecowitt.forward.Count)
							{
								cumulus.EcowittForwarders[i] = string.IsNullOrWhiteSpace(settings.ecowitt.forward[i].url) ? null : settings.ecowitt.forward[i].url.Trim();
							}
							else
							{
								cumulus.EcowittForwarders[i] = null;
							}
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ecowitt sensor mappings
				try
				{
					if (settings.ecowittmaps != null)
					{
						cumulus.Gw1000PrimaryTHSensor = settings.ecowittmaps.primaryTHsensor;
						cumulus.Gw1000PrimaryRainSensor = settings.ecowittmaps.primaryRainSensor;
						cumulus.EcowittIsRainingUsePiezo = settings.ecowittmaps.piezosrain;

						if (cumulus.EcowittMapWN34[1] != settings.ecowittmaps.wn34chan1)
						{
							if (cumulus.EcowittMapWN34[1] == 0)
								station.UserTemp[1] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[1]] = null;

							cumulus.EcowittMapWN34[1] = settings.ecowittmaps.wn34chan1;
						}

						if (cumulus.EcowittMapWN34[2] != settings.ecowittmaps.wn34chan2)
						{
							if (cumulus.EcowittMapWN34[2] == 0)
								station.UserTemp[2] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[2]] = null;

							cumulus.EcowittMapWN34[2] = settings.ecowittmaps.wn34chan2;
						}

						if (cumulus.EcowittMapWN34[3] != settings.ecowittmaps.wn34chan3)
						{
							if (cumulus.EcowittMapWN34[3] == 0)
								station.UserTemp[3] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[3]] = null;

							cumulus.EcowittMapWN34[3] = settings.ecowittmaps.wn34chan3;
						}

						if (cumulus.EcowittMapWN34[4] != settings.ecowittmaps.wn34chan4)
						{
							if (cumulus.EcowittMapWN34[4] == 0)
								station.UserTemp[4] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[4]] = null;

							cumulus.EcowittMapWN34[4] = settings.ecowittmaps.wn34chan4;
						}

						if (cumulus.EcowittMapWN34[5] != settings.ecowittmaps.wn34chan5)
						{
							if (cumulus.EcowittMapWN34[5] == 0)
								station.UserTemp[5] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[5]] = null;

							cumulus.EcowittMapWN34[5] = settings.ecowittmaps.wn34chan5;
						}

						if (cumulus.EcowittMapWN34[6] != settings.ecowittmaps.wn34chan6)
						{
							if (cumulus.EcowittMapWN34[6] == 0)
								station.UserTemp[6] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[6]] = null;

							cumulus.EcowittMapWN34[6] = settings.ecowittmaps.wn34chan6;
						}

						if (cumulus.EcowittMapWN34[7] != settings.ecowittmaps.wn34chan7)
						{
							if (cumulus.EcowittMapWN34[7] == 0)
								station.UserTemp[7] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[7]] = null;

							cumulus.EcowittMapWN34[7] = settings.ecowittmaps.wn34chan7;
						}

						if (cumulus.EcowittMapWN34[8] != settings.ecowittmaps.wn34chan8)
						{
							if (cumulus.EcowittMapWN34[8] == 0)
								station.UserTemp[8] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[8]] = null;

							cumulus.EcowittMapWN34[8] = settings.ecowittmaps.wn34chan8;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt sensor mapping: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// weatherflow connection details
				try
				{
					if (settings.weatherflow != null)
					{
						cumulus.WeatherFlowOptions.WFDeviceId = settings.weatherflow.deviceid;
						cumulus.WeatherFlowOptions.WFTcpPort = settings.weatherflow.tcpport;
						cumulus.WeatherFlowOptions.WFToken = string.IsNullOrWhiteSpace(settings.weatherflow.token) ? null : settings.weatherflow.token.Trim();
						cumulus.WeatherFlowOptions.WFDaysHist = settings.weatherflow.dayshistory;
					}
				}
				catch (Exception ex)
				{
					var msg = $"Error processing WeatherFlow settings: {ex.Message}";
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// EasyWeather
				try
				{
					if (settings.easyw != null)
					{
						cumulus.EwOptions.Interval = settings.easyw.interval;
						cumulus.EwOptions.Filename = string.IsNullOrWhiteSpace(settings.easyw.filename) ? null : settings.easyw.filename.Trim();
						cumulus.EwOptions.MinPressMB = settings.easyw.minpressmb;
						cumulus.EwOptions.MaxPressMB = settings.easyw.maxpressmb;
						cumulus.EwOptions.MaxRainTipDiff = settings.easyw.raintipdiff;
						cumulus.EwOptions.PressOffset = settings.easyw.pressoffset;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing EasyWeather settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// FineOffset
				try
				{
					if (settings.fineoffset != null)
					{
						cumulus.FineOffsetOptions.SyncReads = settings.fineoffset.syncreads;
						cumulus.FineOffsetOptions.ReadAvoidPeriod = settings.fineoffset.readavoid;
						cumulus.FineOffsetOptions.ReadTime = settings.fineoffset.advanced.readtime;
						cumulus.FineOffsetOptions.SetLoggerInterval = settings.fineoffset.advanced.setlogger;
						cumulus.FineOffsetOptions.VendorID = settings.fineoffset.advanced.vid;
						cumulus.FineOffsetOptions.ProductID = settings.fineoffset.advanced.pid;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Fine Offset settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Instromet
				try
				{
					if (settings.imet != null)
					{
						cumulus.ComportName = string.IsNullOrWhiteSpace(settings.imet.comportname) ? cumulus.ComportName : settings.imet.comportname.Trim();
						cumulus.ImetOptions.BaudRate = settings.imet.baudrate;
						cumulus.StationOptions.SyncTime = settings.imet.advanced.syncstationclock;
						cumulus.StationOptions.ClockSettingHour = settings.imet.advanced.syncclockhour;
						cumulus.ImetOptions.ReadDelay = settings.imet.advanced.readdelay;
						cumulus.ImetOptions.WaitTime = settings.imet.advanced.waittime;
						cumulus.ImetOptions.UpdateLogPointer = settings.imet.advanced.updatelogpointer;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Instromet settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WMR928
				try
				{
					if (settings.wmr928 != null)
					{
						cumulus.ComportName = string.IsNullOrWhiteSpace(settings.wmr928.comportname) ? cumulus.ComportName : settings.wmr928.comportname.Trim();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WMR928 settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ecowitt API
				try
				{
					if (settings.ecowittapi != null)
					{
						cumulus.EcowittApplicationKey = string.IsNullOrWhiteSpace(settings.ecowittapi.applicationkey) ? null : settings.ecowittapi.applicationkey.Trim();
						cumulus.EcowittUserApiKey = string.IsNullOrWhiteSpace(settings.ecowittapi.userkey) ? null : settings.ecowittapi.userkey.Trim();
						if (cumulus.StationType == 12)
						{
							// For GW1000 local API, the cloud MAC MUST be the same as the device MAC
							cumulus.EcowittMacAddress = cumulus.Gw1000MacAddress;
						}
						else
						{
							// For all others, there is no local MAC, so we have to define it in the API
							cumulus.EcowittMacAddress = string.IsNullOrWhiteSpace(settings.ecowittapi.mac) ? null : settings.ecowittapi.mac.Trim().ToUpper();
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt API settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// JSON data input
				try
				{
					if (settings.jsonstation != null)
					{
						cumulus.JsonStationOptions.Connectiontype = settings.jsonstation.conntype;
						if (cumulus.JsonStationOptions.Connectiontype == 0)
						{
							cumulus.JsonStationOptions.SourceFile = string.IsNullOrWhiteSpace(settings.jsonstation.filename) ? null : settings.jsonstation.filename.Trim();
							cumulus.JsonStationOptions.FileReadDelay = settings.jsonstation.advanced.filedelay;
							cumulus.JsonStationOptions.FileIgnoreTime = settings.jsonstation.advanced.fileignore;
						}
						if (cumulus.JsonStationOptions.Connectiontype == 2)
						{
							cumulus.JsonStationOptions.MqttServer = string.IsNullOrWhiteSpace(settings.jsonstation.mqttserver) ? null : settings.jsonstation.mqttserver.Trim();
							cumulus.JsonStationOptions.MqttPort = settings.jsonstation.mqttport;
							cumulus.JsonStationOptions.MqttUsername = string.IsNullOrWhiteSpace(settings.jsonstation.mqttuser) ? null : settings.jsonstation.mqttuser.Trim();
							cumulus.JsonStationOptions.MqttPassword = string.IsNullOrWhiteSpace(settings.jsonstation.mqttpass) ? null : settings.jsonstation.mqttpass.Trim();
							cumulus.JsonStationOptions.MqttTopic = string.IsNullOrWhiteSpace(settings.jsonstation.mqtttopic) ? null : settings.jsonstation.mqtttopic.Trim();

							cumulus.JsonStationOptions.MqttUseTls = settings.jsonstation.advanced.mqtttls;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing JSON Data Input settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Units
				try
				{
					if (cumulus.Units.Wind != settings.general.units.wind)
					{
						cumulus.Limit.WindHigh = settings.general.units.wind switch
						{
							0 => ConvertUnits.UserWindToMS(cumulus.Limit.WindHigh),
							1 => ConvertUnits.UserWindToMPH(cumulus.Limit.WindHigh),
							2 => ConvertUnits.UserWindToKPH(cumulus.Limit.WindHigh),
							3 => ConvertUnits.UserWindToKnots(cumulus.Limit.WindHigh),
							_ => cumulus.Limit.WindHigh
						};

						cumulus.Units.Wind = settings.general.units.wind;
						cumulus.ChangeWindUnits();
						cumulus.WindDPlaces = cumulus.StationOptions.RoundWindSpeed ? 0 : cumulus.WindDPlaceDefaults[cumulus.Units.Wind];

						settings.general.units.advanced.winddp = cumulus.WindDPlaces;
					}
					if (cumulus.Units.Press != settings.general.units.pressure)
					{
						switch (settings.general.units.pressure)
						{
							case 0:
							case 1:
								cumulus.Limit.PressHigh = ConvertUnits.UserPressToHpa(cumulus.Limit.PressHigh);
								cumulus.Limit.PressLow = ConvertUnits.UserPressToHpa(cumulus.Limit.PressLow);
								break;
							case 2:
								cumulus.Limit.PressHigh = ConvertUnits.UserPressToIN(cumulus.Limit.PressHigh);
								cumulus.Limit.PressLow = ConvertUnits.UserPressToIN(cumulus.Limit.PressLow);
								break;
							case 3:
								cumulus.Limit.PressHigh = ConvertUnits.UserPressToKpa(cumulus.Limit.PressHigh);
								cumulus.Limit.PressLow = ConvertUnits.UserPressToKpa(cumulus.Limit.PressLow);
								break;
						}
						cumulus.Units.Press = settings.general.units.pressure;
						cumulus.ChangePressureUnits();
						settings.general.units.advanced.pressdp = cumulus.PressDPlaceDefaults[cumulus.Units.Press];
					}
					if (cumulus.Units.Temp != settings.general.units.temp)
					{
						switch (settings.general.units.temp)
						{
							case 0:
								cumulus.Limit.TempHigh = ConvertUnits.UserTempToC(cumulus.Limit.TempHigh);
								cumulus.Limit.TempLow = ConvertUnits.UserTempToC(cumulus.Limit.TempLow);
								cumulus.Limit.DewHigh = ConvertUnits.UserTempToC(cumulus.Limit.DewHigh);
								break;
							case 1:
								cumulus.Limit.TempHigh = ConvertUnits.UserTempToF(cumulus.Limit.TempHigh);
								cumulus.Limit.TempLow = ConvertUnits.UserTempToF(cumulus.Limit.TempLow);
								cumulus.Limit.DewHigh = ConvertUnits.UserTempToF(cumulus.Limit.DewHigh);
								break;
						}
						cumulus.Units.Temp = settings.general.units.temp;
						cumulus.ChangeTempUnits();
					}
					if (cumulus.Units.Rain != settings.general.units.rain)
					{
						cumulus.Units.Rain = settings.general.units.rain;
						cumulus.ChangeRainUnits();
						settings.general.units.advanced.raindp = cumulus.RainDPlaceDefaults[cumulus.Units.Rain];
					}
					cumulus.Units.SnowDepth = settings.general.units.snow;
					cumulus.Units.LaserDistance = settings.general.units.laser;
					cumulus.SetupUnitText();

					cumulus.CloudBaseInFeet = settings.general.units.cloudbaseft;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Units settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Units Advanced
				try
				{
					cumulus.TempDPlaces = settings.general.units.advanced.tempdp;
					cumulus.TempFormat = "F" + cumulus.TempDPlaces;

					cumulus.WindDPlaces = settings.general.units.advanced.winddp;
					cumulus.WindFormat = "F" + cumulus.WindDPlaces;

					cumulus.WindAvgDPlaces = settings.general.units.advanced.windavgdp;
					cumulus.WindAvgFormat = "F" + cumulus.WindAvgDPlaces;

					cumulus.RainDPlaces = settings.general.units.advanced.raindp;
					cumulus.RainFormat = "F" + cumulus.RainDPlaces;
					cumulus.ETFormat = "F" + (cumulus.RainDPlaces + 1);

					cumulus.PressDPlaces = settings.general.units.advanced.pressdp;
					cumulus.PressFormat = "F" + cumulus.PressDPlaces;

					cumulus.UVDPlaces = settings.general.units.advanced.uvdp;
					cumulus.UVFormat = "F" + cumulus.UVDPlaces;

					cumulus.SunshineDPlaces = settings.general.units.advanced.sunshinedp;
					cumulus.SunFormat = "F" + cumulus.SunshineDPlaces;

					cumulus.WindRunDPlaces = settings.general.units.advanced.windrundp;
					cumulus.WindRunFormat = "F" + cumulus.WindRunDPlaces;

					cumulus.AirQualityDPlaces = settings.general.units.advanced.airqulaitydp;
					cumulus.AirQualityFormat = "F" + cumulus.AirQualityDPlaces;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Units settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// General Advanced
				try
				{
					cumulus.RecordsBeganDateTime = DateTime.ParseExact(settings.general.advanced.recsbegandate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
				}
				catch (Exception ex)
				{
					var msg = "Error processing Records Began Date: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Station type
				try
				{

					if (cumulus.StationType != settings.general.stationtype)
					{
						cumulus.LogWarningMessage("Station type changed, restart required");
						Cumulus.LogConsoleMessage("*** Station type changed, restart required ***", ConsoleColor.Yellow, true);
					}
					cumulus.Manufacturer = (Cumulus.StationManufacturer) settings.general.manufacturer;
					cumulus.StationType = settings.general.stationtype;
					cumulus.StationModel = settings.general.stationmodel;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Station Type setting: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Accessible
				try
				{
					cumulus.ProgramOptions.EnableAccessibility = settings.accessible;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Accessibility setting: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Station settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Station Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		internal string UploadNow(IHttpContext context)
		{
			cumulus.LogDebugMessage("Upload Now: Starting process");

			if (station == null)
			{
				cumulus.LogDebugMessage("Upload Now: Not possible, station is not initialised}");
				return "Not possible, station is not initialised}";
			}

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();
				var json = WebUtility.UrlDecode(data);

				var options = json.FromJson<UploadNowData>();

				if (!cumulus.FtpOptions.Enabled && !cumulus.FtpOptions.LocalCopyEnabled)
					return "Upload/local copy is not enabled!";


				if (cumulus.WebUpdating == 1)
				{
					cumulus.LogMessage("Upload Now: Warning, a previous web update is still in progress, first chance, skipping attempt");
					return "A web update is already in progress";
				}

				var returnMsg = "Upload process invoked";

				if (cumulus.WebUpdating >= 2)
				{
					try
					{
						cumulus.LogMessage("Upload Now: Warning, a previous web update is still in progress, second chance, aborting connection");
						if (cumulus.ftpThread.ThreadState == ThreadState.Running)
							cumulus.ftpThread.Interrupt();

						returnMsg = "An existing upload process was aborted, and a new FTP process invoked";
					}
					catch (Exception ex)
					{
						returnMsg = "Error aborting a currently running upload";
						cumulus.LogErrorMessage($"Upload Now: {returnMsg}: {ex.Message}");
						return returnMsg;
					}
				}

				// Graph configs may have changed, so force re-create and upload the json files - just flag everything!
				cumulus.LogDebugMessage("Upload Now: Flagging the graph data files for recreation and upload/copy");
				if (options.graphs)
					cumulus.LogDebugMessage("Upload Now: Flagging graph data files for full upload rather than incremental");

				for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
				{
					cumulus.GraphDataFiles[i].CreateRequired = true;
					cumulus.GraphDataFiles[i].FtpRequired = true;
					cumulus.GraphDataFiles[i].CopyRequired = true;
					if (options.graphs)
						cumulus.GraphDataFiles[i].Incremental = false;
					cumulus.GraphDataFiles[i].LastDataTime = DateTime.MinValue;
				}

				// (re)generate the daily graph data files, and upload if required
				if (options.dailygraphs)
				{
					cumulus.LogDebugMessage("Upload Now: Flagging the daily graph data files for recreation and upload/copy");
					for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
					{
						cumulus.GraphDataEodFiles[i].CreateRequired = true;
						cumulus.GraphDataEodFiles[i].FtpRequired = true;
						cumulus.GraphDataEodFiles[i].CopyRequired = true;
						cumulus.GraphDataEodFiles[i].Incremental = false;
						cumulus.GraphDataEodFiles[i].LastDataTime = DateTime.MinValue;
					}
				}

				// flag the latest NOAA files for upload
				if (options.noaa)
				{
					cumulus.LogDebugMessage("Upload Now: Flagging the latest NOAA report files for upload/copy");
					cumulus.NOAAconf.NeedFtp = true;
					cumulus.NOAAconf.NeedCopy = true;
				}

				// flag the incremental log files for full upload
				if (options.logfiles)
				{
					cumulus.LogDebugMessage("Upload Now: Flagging incremental log files for full upload rather than incremental");
					for (var i = 0; i < cumulus.ActiveExtraFiles.Count; i++)
					{
						cumulus.ActiveExtraFiles[i].logFileLastLineNumber = 0;
					}
				}

				try
				{
					cumulus.LogDebugMessage("Upload Now: Starting the main update process in the background");
					cumulus.WebUpdating = 1;
					cumulus.ftpThread = new Thread(async () => await cumulus.DoHTMLFiles()) { IsBackground = true };
					cumulus.ftpThread.Start();
				}
				catch (Exception ex)
				{
					returnMsg = "Error starting a new upload";
					cumulus.LogErrorMessage($"Upload Now: {returnMsg}: {ex.Message}");
				}

				cumulus.LogDebugMessage("Upload Now: Process complete");
				return returnMsg;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"Upload Now: General error: {ex.Message}");
				context.Response.StatusCode = 500;
				return $"Error: {ex.Message}";
			}
		}


#pragma warning disable S3459 // Unassigned members should be removed
#pragma warning disable S1144 // Unused private types or members should be removed
		private sealed class UploadNowData
		{
			public bool dailygraphs { get; set; }
			public bool noaa { get; set; }
			public bool graphs { get; set; }
			public bool logfiles { get; set; }
		}
#pragma warning restore S1144 // Unused private types or members should be removed
#pragma warning restore S3459 // Unassigned members should be removed

		internal string SetSelectaChartOptions(IHttpContext context)
		{
			var errorMsg = string.Empty;
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Update Selectaschhrt options error: " + ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		internal string SetSelectaPeriodOptions(IHttpContext context)
		{
			var errorMsg = string.Empty;
			context.Response.StatusCode = 200;
			// get the response
			try
			{
				cumulus.LogMessage("Updating select-a-period settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				var json = WebUtility.UrlDecode(data);

				// de-serialize it to the settings structure
				var settings = JsonSerializer.DeserializeFromString<JsonSelectaChartSettings>(json);

				// process the settings
				try
				{
					cumulus.SelectaPeriodOptions.series = settings.series;
					cumulus.SelectaPeriodOptions.colours = settings.colours;
				}
				catch (Exception ex)
				{
					var msg = "Error select-a-period Options: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Update select-a-period options error: " + ex.Message);
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

		private sealed class JsonData
		{
			public bool accessible { get; set; }
			public int stationid { get; set; }
			public JsonGeneral general { get; set; }
			public JsonDavisVp2 davisvp2 { get; set; }
			public JsonGw1000Conn gw1000 { get; set; }
			public JsonHttpApi ecowitthttpapi { get; set; }
			public JsonEcowitt ecowitt { get; set; }
			public JsonEcowittApi ecowittapi { get; set; }
			public JsonEcowittMappings ecowittmaps { get; set; }
			public JsonWeatherFlow weatherflow { get; set; }
			public JsonWll daviswll { get; set; }
			public JsonFineOffset fineoffset { get; set; }
			public JsonEasyWeather easyw { get; set; }
			public JsonImet imet { get; set; }
			public JsonWmr928 wmr928 { get; set; }
			public JsonJsonStation jsonstation { get; set; }
			public JsonOptions Options { get; set; }
			public JsonForecast Forecast { get; set; }
			public JsonSolar Solar { get; set; }
			public JsonAnnualRainfall AnnualRainfall { get; set; }
			public JsonGrowingDDSettings GrowingDD { get; set; }
			public JsonTempSumSettings TempSum { get; set; }
			public JsonChillHours ChillHrs { get; set; }
		}

		private sealed class JsonGeneral
		{
			public int manufacturer { get; set; }
			public int stationtype { get; set; }
			public string stationmodel { get; set; }
			public int loginterval { get; set; }
			public JsonLogRollover logrollover { get; set; }
			public JsonUnits units { get; set; }
			public JsonLocation Location { get; set; }
			public JsonAdvanced advanced { get; set; }
		}

		private sealed class JsonAdvanced
		{
			public string recsbegandate { get; set; }
		}

		private sealed class JsonUnitsAdvanced
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

		private sealed class JsonUnits
		{
			public int wind { get; set; }
			public int pressure { get; set; }
			public int temp { get; set; }
			public int rain { get; set; }
			public bool cloudbaseft { get; set; }
			public int snow { get; set; }
			public int laser { get; set; }

			public JsonUnitsAdvanced advanced { get; set; }
		}

		private sealed class JsonOptionsAdvanced
		{
			public bool usespeedforavg { get; set; }
			public int avgbearingmins { get; set; }
			public int avgspeedmins { get; set; }
			public int peakgustmins { get; set; }
			public int maxwind { get; set; }
			public int recordtimeout { get; set; }
			public int snowdepthhour { get; set; }
			public double raindaythreshold { get; set; }
			public bool uselogger { get; set; }
		}

		private sealed class JsonOptions
		{
			public bool usezerobearing { get; set; }
			public bool calcwindaverage { get; set; }
			public bool use100for98hum { get; set; }
			public bool calculatedewpoint { get; set; }
			public bool calculatewindchill { get; set; }
			public bool calculateet { get; set; }
			public bool calculateslp { get; set; }
			public bool cumuluspresstrendnames { get; set; }
			public bool roundwindspeeds { get; set; }
			public bool ignorelacrosseclock { get; set; }
			public bool extrasensors { get; set; }
			public bool debuglogging { get; set; }
			public bool datalogging { get; set; }
			public bool stopsecondinstance { get; set; }
			public bool nosensorcheck { get; set; }
			public int leafwetisrainingidx { get; set; }
			public double leafwetisrainingthrsh { get; set; }
			public int userainforisraining { get; set; }
			public int snowseasonstart { get; set; }
			public JsonOptionsAdvanced advanced { get; set; }
		}

		public class JsonTCPsettings
		{
			public string ipaddress { get; set; }
			public int disconperiod { get; set; }
		}

		private sealed class JsonDavisVp2Connection
		{
			public int conntype { get; set; }
			public string comportname { get; set; }
			public JsonTCPsettings tcpsettings { get; set; }
		}

		private sealed class JsonDavisVp2
		{
			public JsonDavisVp2Connection davisconn { get; set; }

			public JsonsDavisVp2Advanced advanced { get; set; }
		}

		private sealed class JsonsDavisVp2Advanced
		{
			public bool syncstationclock { get; set; }
			public int syncclockhour { get; set; }
			public bool readreceptionstats { get; set; }
			public bool setloggerinterval { get; set; }
			public bool useloop2 { get; set; }
			public int raingaugetype { get; set; }
			public bool vp1minbarupdate { get; set; }
			public int initwaittime { get; set; }
			public int ipresponsetime { get; set; }
			public int baudrate { get; set; }
			public int tcpport { get; set; }

		}

		private sealed class JsonFineOffsetAdvanced
		{
			public int readtime { get; set; }
			public bool setlogger { get; set; }
			public int vid { get; set; }
			public int pid { get; set; }
		}

		private sealed class JsonFineOffset
		{
			public bool syncreads { get; set; }
			public int readavoid { get; set; }
			public JsonFineOffsetAdvanced advanced { get; set; }
		}

		private sealed class JsonEasyWeather
		{
			public double interval { get; set; }
			public string filename { get; set; }
			public int minpressmb { get; set; }
			public int maxpressmb { get; set; }
			public int raintipdiff { get; set; }
			public double pressoffset { get; set; }
		}

		public class JsonWeatherFlow
		{
			public int tcpport { get; set; }
			public int deviceid { get; set; }
			public string token { get; set; }
			public int dayshistory { get; set; }
		}

		public class JsonGw1000Conn
		{
			public string ipaddress { get; set; }
			public bool autoDiscover { get; set; }
			public string macaddress { get; set; }
			public int primaryTHsensor { get; set; }
			public int primaryRainSensor { get; set; }
		}

		public class JsonHttpApi
		{
			public string ipaddress { get; set; }
			public string password { get; set; }
			public bool usesdcard { get; set; }
		}

		private sealed class JsonEcowitt
		{
			public bool setcustom { get; set; }
			public string gwaddr { get; set; }
			public string localaddr { get; set; }
			public int interval { get; set; }
			public List<JsonEcowittForwardList> forward { get; set; }
		}

		public class JsonEcowittForwardList
		{
			public string url { get; set; }
		}

		public class JsonEcowittApi
		{
			public string applicationkey { get; set; }
			public string userkey { get; set; }
			public string mac { get; set; }
		}

		public class JsonEcowittMappings
		{
			public int primaryTHsensor { get; set; }
			public int primaryRainSensor { get; set; }
			public bool piezosrain { get; set; }

			public int wn34chan1 { get; set; }
			public int wn34chan2 { get; set; }
			public int wn34chan3 { get; set; }
			public int wn34chan4 { get; set; }
			public int wn34chan5 { get; set; }
			public int wn34chan6 { get; set; }
			public int wn34chan7 { get; set; }
			public int wn34chan8 { get; set; }
		}

		public class JsonWmr928
		{
			public string comportname { get; set; }
		}

		private sealed class JsonImet
		{
			public string comportname { get; set; }

			public int baudrate { get; set; }
			public JsonImetAdvanced advanced { get; set; }
		}

		private sealed class JsonImetAdvanced
		{
			public bool syncstationclock { get; set; }
			public int syncclockhour { get; set; }
			public int waittime { get; set; }
			public int readdelay { get; set; }
			public bool updatelogpointer { get; set; }
		}

		public class JsonLogRollover
		{
			public string time { get; set; }
			public bool summer10am { get; set; }
		}

		private sealed class JsonLatLong
		{
			public int degrees { get; set; }
			public int minutes { get; set; }
			public int seconds { get; set; }
			public string hemisphere { get; set; }
		}

		private sealed class JsonLocation
		{
			public JsonLatLong Latitude { get; set; }
			public JsonLatLong Longitude { get; set; }
			public int altitude { get; set; }
			public string altitudeunit { get; set; }
			public string sitename { get; set; }
			public string description { get; set; }
		}

		private sealed class JsonForecast
		{
			public bool usecumulusforecast { get; set; }
			public bool updatehourly { get; set; }
			public double lowpressureextreme { get; set; }
			public double highpressureextreme { get; set; }
			public string pressureunit { get; set; }
		}

		private sealed class JsonSolar
		{
			public int sunthreshold { get; set; }
			public int solarmin { get; set; }
			public int solarcalc { get; set; }
			public double transfactorJun { get; set; }
			public double transfactorDec { get; set; }
			public double turbidityJun { get; set; }
			public double turbidityDec { get; set; }
		}

		private sealed class JsonWll
		{
			public JsonWllNetwork network { get; set; }
			public JsonWllApi api { get; set; }
			public JsonWllPrimary primary { get; set; }
			public JsonWllSoilLeaf soilLeaf { get; set; }
			public JsonWllExtraTemp extraTemp { get; set; }
			public JsonWllAdvanced advanced { get; set; }
		}

		private sealed class JsonWllAdvanced
		{
			public int raingaugetype { get; set; }
			public int tcpport { get; set; }
			public bool datastopped { get; set; }
		}

		public class JsonWllNetwork
		{
			public bool autoDiscover { get; set; }
			public string ipaddress { get; set; }

		}

		public class JsonWllApi
		{
			public string apiKey { get; set; }
			public string apiSecret { get; set; }
			public int apiStationId { get; set; }
			public string apiStationUuid { get; set; }
		}

		public class JsonWllPrimary
		{
			public int wind { get; set; }
			public int temphum { get; set; }
			public int rain { get; set; }
			public int solar { get; set; }
			public int uv { get; set; }
		}

		private sealed class JsonWllSoilLeaf
		{
			public JsonWllSoilTemp extraSoilTemp { get; set; }
			public JsonWllSoilMoist extraSoilMoist { get; set; }
			public JsonWllExtraLeaf extraLeaf { get; set; }
		}

		private sealed class JsonWllSoilTemp
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

		private sealed class JsonWllSoilMoist
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

		private sealed class JsonWllExtraLeaf
		{
			public int leafTx1 { get; set; }
			public int leafIdx1 { get; set; }
			public int leafTx2 { get; set; }
			public int leafIdx2 { get; set; }
		}

		private sealed class JsonWllExtraTemp
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

		private sealed class JsonAnnualRainfall
		{
			public double ytdamount { get; set; }
			public int ytdyear { get; set; }
			public int rainseasonstart { get; set; }
			public int rainweekstart { get; set; }
		}

		private sealed class JsonSelectaChartSettings
		{
			public string[] series { get; set; }
			public string[] colours { get; set; }
		}

		private sealed class JsonGrowingDDSettings
		{
			public double basetemp1 { get; set; }
			public double basetemp2 { get; set; }
			public int starts { get; set; }
			public bool cap30C { get; set; }
		}

		private sealed class JsonTempSumSettings
		{
			public int starts { get; set; }
			public double basetemp1 { get; set; }
			public double basetemp2 { get; set; }
		}

		private sealed class JsonChillHours
		{
			public double threshold { get; set; }
			public double basetemp { get; set; }
			public int month { get; set; }
		}

		public class JsonJsonStation
		{
			public int conntype { get; set; }
			public string filename { get; set; }
			public string mqttserver { get; set; }
			public int mqttport { get; set; }
			public string mqttuser { get; set; }
			public string mqttpass { get; set; }
			public string mqtttopic { get; set; }

			public JsonJsonStationAdvanced advanced { get; set; }
		}

		public class JsonJsonStationAdvanced
		{
			public int filedelay { get; set; }
			public int fileignore { get; set; }
			public bool mqtttls { get; set; }
		}
	}
}
