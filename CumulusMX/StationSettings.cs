using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Web.UI;
using Unosquare.Labs.EmbedIO;


namespace CumulusMX
{
    public class StationSettings
    {
        private Cumulus cumulus;
        private string stationOptionsFile;
        private string stationSchemaFile;

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
            
            var data = new JsonStationSettingsData()
                       {
                           stationtype = cumulus.StationType, 
                           units = units, 
                           davisconn = davisconn, 
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


                // log interval
                cumulus.DataLogInterval = settings.loginterval;

                // com port
                cumulus.ComportName = settings.comportname ?? string.Empty; ;

                // Davis connection details
                cumulus.VP2ConnectionType = settings.davisconn.conntype;
                cumulus.VP2IPAddr = settings.davisconn.tcpsettings.ipaddress ?? string.Empty; 
                cumulus.VP2TCPPort = settings.davisconn.tcpsettings.tcpport;
                cumulus.VP2PeriodicDisconnectInterval = settings.davisconn.tcpsettings.disconperiod;
               
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