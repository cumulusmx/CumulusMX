using System;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;
using System.Reflection;

namespace CumulusMX
{
	public class StationSettings
	{
		private readonly Cumulus cumulus;
		private readonly string stationOptionsFile;
		private readonly string stationSchemaFile;

		public StationSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;

			stationOptionsFile = AppDomain.CurrentDomain.BaseDirectory+ "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "StationOptions.json";
			stationSchemaFile = AppDomain.CurrentDomain.BaseDirectory + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "StationSchema.json";
		}

		public string GetStationAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it
			var options = new JsonStationSettingsOptions()
						  {
							  usezerobearing = cumulus.UseZeroBearing,
							  calcwindaverage = cumulus.UseWind10MinAve,
							  usespeedforavg = cumulus.UseSpeedForAvgCalc,
							  use100for98hum = cumulus.Humidity98Fix,
							  calculatedewpoint = cumulus.CalculatedDP,
							  calculatewindchill = cumulus.CalculatedWC,
							  syncstationclock = cumulus.SyncTime,
							  cumuluspresstrendnames = cumulus.UseCumulusPresstrendstr,
							  vp1minbarupdate = cumulus.ForceVPBarUpdate,
							  extrasensors = cumulus.LogExtraSensors,
							  ignorelacrosseclock = cumulus.WS2300IgnoreStationClock,
							  roundwindspeeds = cumulus.RoundWindSpeed,
							  synchroniseforeads = cumulus.SyncFOReads,
							  debuglogging = cumulus.logging,
							  datalogging = cumulus.DataLogging,
							  stopsecondinstance = cumulus.WarnMultiple,
							  readreceptionstats = cumulus.DavisReadReceptionStats
						  };

			var units = new JsonStationSettingsUnits()
						{
							wind = cumulus.WindUnit,
							pressure = cumulus.PressUnit,
							temp = cumulus.TempUnit,
							rain = cumulus.RainUnit
						};

			var tcpsettings = new JsonStationSettingsTCPsettings()
							  {
								  ipaddress = cumulus.VP2IPAddr,
								  tcpport = cumulus.VP2TCPPort,
								  disconperiod = cumulus.VP2PeriodicDisconnectInterval
							  };

			var davisconn = new JsonStationSettingsDavisConn() {conntype = cumulus.VP2ConnectionType, tcpsettings = tcpsettings};

			var gw1000 = new JSonStationSettingsGw1000Conn() {ipaddress = cumulus.Gw1000IpAddress};

			var logrollover = new JsonStationSettingsLogRollover() {time = "midnight",summer10am = cumulus.Use10amInSummer};

			if (cumulus.RolloverHour == 9)
			{
				logrollover.time = "9am";
			}

			int deg, min, sec;
			string hem;

			LatToDMS(cumulus.Latitude, out deg, out min, out sec, out hem);

			var Latitude = new JsonStationSettingsLatLong() {degrees = deg, minutes = min, seconds = sec, hemisphere = hem};

			LongToDMS(cumulus.Longitude, out deg, out min, out sec, out hem);

			var Longitude = new JsonStationSettingsLatLong() { degrees = deg, minutes = min, seconds = sec, hemisphere = hem };

			var Location = new JsonStationSettingsLocation()
							{
								altitude = (int) cumulus.Altitude,
								altitudeunit = "metres",
								description = cumulus.LocationDesc,
								Latitude = Latitude,
								Longitude = Longitude,
								sitename = cumulus.LocationName
							};

			if (cumulus.AltitudeInFeet)
			{
				Location.altitudeunit = "feet";
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
							useblakelarsen = cumulus.UseBlakeLarsen,
							solarcalc = cumulus.SolarCalc,
							turbidity = cumulus.BrasTurbidity
						};

			var annualrainfall = new JsonStationSettingsAnnualRainfall() {rainseasonstart = cumulus.RainSeasonStart, ytdamount = cumulus.YTDrain, ytdyear = cumulus.YTDrainyear};

			var graphs = new JsonStationSettingsGraphs() {graphdays = cumulus.GraphDays, graphhours = cumulus.GraphHours};

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
				api = wllApi,
				primary = wllPrimary,
				soilLeaf = wllSoilLeaf,
				extraTemp = wllExtraTemp
			};

			var data = new JsonStationSettingsData()
				{
				stationtype = cumulus.StationType,
				units = units,
				davisconn = davisconn,
				daviswll = wll,
				gw1000 = gw1000,
				comportname = cumulus.ComportName,
				loginterval = cumulus.DataLogInterval,
				logrollover = logrollover,
				Location = Location,
				Options = options,
				Forecast = forecast,
				Solar = solar,
				AnnualRainfall = annualrainfall,
				Graphs = graphs
			};

			return JsonConvert.SerializeObject(data);
		}

		public string GetStationAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(stationOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetStationAlpacaFormSchema()
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

		//public string UpdateStationConfig(HttpListenerContext context)
		public string UpdateStationConfig(IHttpContext context)
		{
			// get the response
			try
			{
				cumulus.LogMessage("Updating station settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = JsonConvert.DeserializeObject<JsonStationSettingsData>(json);
				// process the settings

				cumulus.GraphHours = settings.Graphs.graphhours;
				cumulus.GraphDays = settings.Graphs.graphdays;

				// Annual Rainfall
				cumulus.RainSeasonStart = settings.AnnualRainfall.rainseasonstart;
				cumulus.YTDrain = settings.AnnualRainfall.ytdamount;
				cumulus.YTDrainyear = settings.AnnualRainfall.ytdyear;

				// Solar
				cumulus.SolarMinimum = settings.Solar.solarmin;
				cumulus.RStransfactor = settings.Solar.transfactor;
				cumulus.SunThreshold = settings.Solar.sunthreshold;
				cumulus.UseBlakeLarsen = settings.Solar.useblakelarsen;
				cumulus.SolarCalc = settings.Solar.solarcalc;
				cumulus.BrasTurbidity = settings.Solar.turbidity;

				// Forecast
				cumulus.FChighpress = settings.Forecast.highpressureextreme;
				cumulus.FClowpress = settings.Forecast.lowpressureextreme;
				cumulus.HourlyForecast = settings.Forecast.updatehourly;
				cumulus.UseCumulusForecast = settings.Forecast.usecumulusforecast;
				cumulus.FCpressinMB = (settings.Forecast.pressureunit == "mb/hPa");

				// Location
				cumulus.Altitude = settings.Location.altitude;
				cumulus.AltitudeInFeet = (settings.Location.altitudeunit == "feet");
				cumulus.LocationName = settings.Location.sitename ?? string.Empty;
				cumulus.LocationDesc = settings.Location.description ?? string.Empty;

				cumulus.Latitude = settings.Location.Latitude.degrees + (settings.Location.Latitude.minutes / 60.0) + (settings.Location.Latitude.seconds / 3600.0);
				if (settings.Location.Latitude.hemisphere == "South")
				{
					cumulus.Latitude = -cumulus.Latitude;
				}

				cumulus.LatTxt = String.Format("{0}&nbsp;{1:D2}&deg;&nbsp;{2:D2}&#39;&nbsp;{3:D2}&quot;", settings.Location.Latitude.hemisphere[0], settings.Location.Latitude.degrees, settings.Location.Latitude.minutes,
					settings.Location.Latitude.seconds);

				cumulus.Longitude = settings.Location.Longitude.degrees + (settings.Location.Longitude.minutes / 60.0) + (settings.Location.Longitude.seconds / 3600.0);
				if (settings.Location.Longitude.hemisphere == "West")
				{
					cumulus.Longitude = -cumulus.Longitude;
				}

				cumulus.LonTxt = String.Format("{0}&nbsp;{1:D2}&deg;&nbsp;{2:D2}&#39;&nbsp;{3:D2}&quot;", settings.Location.Longitude.hemisphere[0], settings.Location.Longitude.degrees, settings.Location.Longitude.minutes,
					settings.Location.Longitude.seconds);

				// Options
				cumulus.UseZeroBearing = settings.Options.usezerobearing;
				cumulus.UseWind10MinAve = settings.Options.calcwindaverage;
				cumulus.UseSpeedForAvgCalc = settings.Options.usespeedforavg;
				cumulus.Humidity98Fix = settings.Options.use100for98hum;
				cumulus.CalculatedDP = settings.Options.calculatedewpoint;
				cumulus.CalculatedWC = settings.Options.calculatewindchill;
				cumulus.SyncTime = settings.Options.syncstationclock;
				cumulus.UseCumulusPresstrendstr = settings.Options.cumuluspresstrendnames;
				cumulus.ForceVPBarUpdate = settings.Options.vp1minbarupdate;
				cumulus.LogExtraSensors = settings.Options.extrasensors;
				cumulus.WS2300IgnoreStationClock = settings.Options.ignorelacrosseclock;
				cumulus.RoundWindSpeed = settings.Options.roundwindspeeds;
				cumulus.SyncFOReads = settings.Options.synchroniseforeads;
				cumulus.logging = settings.Options.debuglogging;
				cumulus.DataLogging = settings.Options.datalogging;
				cumulus.WarnMultiple = settings.Options.stopsecondinstance;
				cumulus.DavisReadReceptionStats = settings.Options.readreceptionstats;

				// Log rollover
				if (settings.logrollover.time == "9am")
				{
					cumulus.RolloverHour = 9;
				}
				else
				{
					cumulus.RolloverHour = 0;
				}

				cumulus.Use10amInSummer = settings.logrollover.summer10am;

				// WLL
				cumulus.WllApiKey = settings.daviswll.api.apiKey;
				cumulus.WllApiSecret = settings.daviswll.api.apiSecret;
				cumulus.WllStationId = settings.daviswll.api.apiStationId == "-1" ? "" : settings.daviswll.api.apiStationId;

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
				cumulus.WllExtraTempTx[3]= settings.daviswll.extraTemp.extraTempTx4;
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

				// log interval
				cumulus.DataLogInterval = settings.loginterval;

				// com port
				cumulus.ComportName = settings.comportname ?? string.Empty; ;

				// Davis connection details
				cumulus.VP2ConnectionType = settings.davisconn.conntype;
				cumulus.VP2IPAddr = settings.davisconn.tcpsettings.ipaddress ?? string.Empty;
				cumulus.VP2TCPPort = settings.davisconn.tcpsettings.tcpport;
				cumulus.VP2PeriodicDisconnectInterval = settings.davisconn.tcpsettings.disconperiod;

				// GW1000 connection details
				cumulus.Gw1000IpAddress = settings.gw1000.ipaddress;

				// Units
				cumulus.WindUnit = settings.units.wind;
				cumulus.PressUnit = settings.units.pressure;
				cumulus.TempUnit= settings.units.temp;
				cumulus.RainUnit = settings.units.rain;

				cumulus.SetupUnitText();

				// Station type
				if (cumulus.StationType != settings.stationtype)
				{
					cumulus.LogMessage("Station type changed, restart required");
					Console.WriteLine("*** Station type changed, restart required ***");
				}

				cumulus.StationType = settings.stationtype;

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;

			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return "success";
		}

		public string FtpNow()
		{
			if (!string.IsNullOrEmpty(cumulus.ftp_host))
			{
				if (cumulus.WebUpdating)
				{
					return "{\"result\":\"A web update is already in progress\"}";
				}
				else
				{
					cumulus.WebUpdating = true;
					cumulus.ftpThread = new Thread(cumulus.DoHTMLFiles) { IsBackground = true };
					cumulus.ftpThread.Start();
					return "{\"result\":\"FTP process invoked\"}";
				}
			}
			else
			{
				return "{\"result\":\"No FTP host defined\"}";
			}
		}

		public string GetWSport()
		{
			return "{\"wsport\":\"" + cumulus.wsPort + "\"}";
		}

		public string GetVersion()
		{
			return "{\"Version\":\"" + cumulus.Version + "\",\"Build\":\"" + cumulus.Build + "\"}";
		}
	}

	public class JsonStationSettingsData
	{
		public int stationtype { get; set; }
		public JsonStationSettingsUnits units { get; set; }
		public JsonStationSettingsDavisConn davisconn { set; get; }
		public JSonStationSettingsGw1000Conn gw1000 { get; set; }
		public JsonStationSettingsWLL daviswll { get; set; }
		public string comportname { get; set; }
		public int loginterval { get; set; }
		public JsonStationSettingsLogRollover logrollover { get; set; }
		public JsonStationSettingsOptions Options { get; set; }
		public JsonStationSettingsLocation Location { get; set; }
		public JsonStationSettingsForecast Forecast { get; set; }
		public JsonStationSettingsSolar Solar { get; set; }
		public JsonStationSettingsAnnualRainfall AnnualRainfall { get; set; }
		public JsonStationSettingsGraphs Graphs { get; set; }
	}

	public class JsonStationSettingsUnits
	{
		public int wind { get; set; }
		public int pressure { get; set; }
		public int temp { get; set; }
		public int rain { get; set; }
	}

	public class JsonStationSettingsOptions
	{
		public bool usezerobearing { get; set; }
		public bool calcwindaverage { get; set; }
		public bool usespeedforavg { get; set; }
		public bool use100for98hum { get; set; }
		public bool calculatedewpoint { get; set; }
		public bool calculatewindchill { get; set; }
		public bool syncstationclock { get; set; }
		public bool cumuluspresstrendnames { get; set; }
		public bool vp1minbarupdate { get; set; }
		public bool roundwindspeeds { get; set; }
		public bool ignorelacrosseclock { get; set; }
		public bool extrasensors { get; set; }
		public bool synchroniseforeads { get; set; }
		public bool debuglogging { get; set; }
		public bool datalogging { get; set; }
		public bool stopsecondinstance { get; set; }
		public bool readreceptionstats { get; set; }
	}

	public class JsonStationSettingsTCPsettings
	{
		public string ipaddress { get; set; }
		public int tcpport { get; set; }
		public int disconperiod { get; set; }
	}

	public class JsonStationSettingsDavisConn
	{
		public int conntype { get; set; }
		public JsonStationSettingsTCPsettings tcpsettings { get; set; }
	}

	public class JSonStationSettingsGw1000Conn
	{
		public string ipaddress { get; set; }
	}

	public class JsonStationSettingsLogRollover
	{
		public string time { get; set; }
		public bool summer10am { get; set; }
	}

	public class JsonStationSettingsLatLong
	{
		public int degrees { get; set; }
		public int minutes { get; set; }
		public int seconds { get; set; }
		public string hemisphere { get; set; }
	}

	public class JsonStationSettingsLocation
	{
		public JsonStationSettingsLatLong Latitude { get; set; }
		public JsonStationSettingsLatLong Longitude { get; set; }
		public int altitude { get; set; }
		public string altitudeunit { get; set; }
		public string sitename { get; set; }
		public string description { get; set; }
	}

	public class JsonStationSettingsForecast
	{
		public bool usecumulusforecast { get; set; }
		public bool updatehourly { get; set; }
		public double lowpressureextreme { get; set; }
		public double highpressureextreme { get; set; }
		public string pressureunit { get; set; }
	}

	public class JsonStationSettingsSolar
	{
		public int sunthreshold { get; set; }
		public int solarmin { get; set; }
		public double transfactor { get; set; }
		public bool useblakelarsen { get; set; }
		public int solarcalc { get; set; }

		public double turbidity { get; set; }
	}

	public class JsonStationSettingsWLL
	{
		public JsonStationSettingsWLLApi api { get; set; }
		public JsonStationSettingsWllPrimary primary { get; set; }
		public JsonStationSettingsWllSoilLeaf soilLeaf { get; set; }
		public JsonStationSettingsWllExtraTemp extraTemp { get; set; }
	}

	public class JsonStationSettingsWLLApi
	{
		public string apiKey { get; set; }
		public string apiSecret { get; set; }
		public string apiStationId { get; set; }
	}

	public class JsonStationSettingsWllPrimary
	{
		public int wind { get; set; }
		public int temphum { get; set; }
		public int rain { get; set; }
		public int solar { get; set; }
		public int uv { get; set; }
	}

	public class JsonStationSettingsWllSoilLeaf
	{
		public JsonStationSettingsWllSoilTemp extraSoilTemp { get; set; }
		public JsonStationSettingsWllSoilMoist extraSoilMoist { get; set; }
		public JsonStationSettingsWllExtraLeaf extraLeaf { get; set; }
	}

	public class JsonStationSettingsWllSoilTemp
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

	public class JsonStationSettingsWllSoilMoist
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

	public class JsonStationSettingsWllExtraLeaf
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
	}
}
