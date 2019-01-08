using System;
using System.Collections.Generic;
using System.Text;

namespace Cumulus4.Configuration
{
    public class CumulusConfiguration
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);




        private const int DefaultWebUpdateInterval = 15;
        private const int DefaultWundInterval = 15;
        private const int DefaultPWSInterval = 15;
        private const int DefaultAPRSInterval = 9;
        private const int DefaultAwekasInterval = 15;
        private const int DefaultWCloudInterval = 10;

        private const int VP2SERIALCONNECTION = 0;
        private const int VP2USBCONNECTION = 1;
        private const int VP2TCPIPCONNECTION = 2;







        private readonly IniFile ini;

        public CumulusConfiguration(IniFile ini)
        {
            this.ini = ini;
            log.Info("Reading ini file");
            ReadIniFile();
        }

        #region System Configuration

        /// <summary>
        /// Temperature unit currently in use
        /// </summary>
        public string TempUnitText { get; set; }

        /// <summary>
        /// Temperature trend unit in use, eg "°C/hr"
        /// </summary>
        public string TempTrendUnitText { get; set; }

        public string RainUnitText { get; set; }


        public string RainTrendUnitText { get; set; }

        public string PressUnitText { get; set; }

        public string PressTrendUnitText { get; set; }

        public string WindUnitText { get; set; }

        public string WindRunUnitText { get; set; }






        public string Platform { get; private set; }

        public string Datapath { get; private set; }

        public string ListSeparator { get; private set; }
        public char DirectorySeparator { get; private set; }



        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        #endregion








        #region Station Settings




        #endregion




        public double WindRoseAngle { get; set; }

        public int NumWindRosePoints { get; set; }

        public int[] WUnitFact = new[] { 1000, 2237, 3600, 1944 };
        public int[] TUnitFact = new[] { 1000, 1800 };
        public int[] TUnitAdd = new[] { 0, 32 };
        public int[] PUnitFact = new[] { 1000, 1000, 2953 };
        public int[] PressFact = new[] { 1, 1, 100 };
        public int[] RUnitFact = new[] { 1000, 39 };

        public int[] logints = new[] { 1, 5, 10, 15, 20, 30 };

        public int UnitMult = 1000;

        public int GraphDays = 31;

        public string Newmoon = "New Moon",
            WaxingCrescent = "Waxing Crescent",
            FirstQuarter = "First Quarter",
            WaxingGibbous = "Waxing Gibbous",
            Fullmoon = "Full Moon",
            WaningGibbous = "Waning Gibbous",
            LastQuarter = "Last Quarter",
            WaningCrescent = "Waning Crescent";

        public String Calm = "Calm",
            Lightair = "Light air",
            Lightbreeze = "Light breeze",
            Gentlebreeze = "Gentle breeze",
            Moderatebreeze = "Moderate breeze",
            Freshbreeze = "Fresh breeze",
            Strongbreeze = "Strong breeze",
            Neargale = "Near gale",
            Gale = "Gale",
            Stronggale = "Strong gale",
            Storm = "Storm",
            Violentstorm = "Violent storm",
            Hurricane = "Hurricane";

        public String Risingveryrapidly = "Rising very rapidly",
            Risingquickly = "Rising quickly",
            Rising = "Rising",
            Risingslowly = "Rising slowly",
            Steady = "Steady",
            Fallingslowly = "Falling slowly",
            Falling = "Falling",
            Fallingquickly = "Falling quickly",
            Fallingveryrapidly = "Falling very rapidly";

        public string[] compassp = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        public string[] z_forecast =
        {
            "Settled fine", "Fine weather", "Becoming fine", "Fine, becoming less settled", "Fine, possible showers", "Fairly fine, improving",
            "Fairly fine, possible showers early", "Fairly fine, showery later", "Showery early, improving", "Changeable, mending",
            "Fairly fine, showers likely", "Rather unsettled clearing later", "Unsettled, probably improving", "Showery, bright intervals",
            "Showery, becoming less settled", "Changeable, some rain", "Unsettled, short fine intervals", "Unsettled, rain later", "Unsettled, some rain",
            "Mostly very unsettled", "Occasional rain, worsening", "Rain at times, very unsettled", "Rain at frequent intervals", "Rain, very unsettled",
            "Stormy, may improve", "Stormy, much rain"
        };

        public string[] DavisForecast1 =
        {
            "FORECAST REQUIRES 3 HRS. OF RECENT DATA", "Mostly cloudy with little temperature change. ", "Mostly cloudy and cooler. ",
            "Clearing, cooler and windy. ", "Clearing and cooler. ", "Increasing clouds and cooler. ",
            "Increasing clouds with little temperature change. ", "Increasing clouds and warmer. ",
            "Mostly clear for 12 to 24 hours with little temperature change. ", "Mostly clear for 6 to 12 hours with little temperature change. ",
            "Mostly clear and warmer. ", "Mostly clear for 12 to 24 hours and cooler. ", "Mostly clear for 12 hours with little temperature change. ",
            "Mostly clear with little temperature change. ", "Mostly clear and cooler. ", "Partially cloudy, Rain and/or snow possible or continuing. ",
            "Partially cloudy, Snow possible or continuing. ", "Partially cloudy, Rain possible or continuing. ",
            "Mostly cloudy, Rain and/or snow possible or continuing. ", "Mostly cloudy, Snow possible or continuing. ",
            "Mostly cloudy, Rain possible or continuing. ", "Mostly cloudy. ", "Partially cloudy. ", "Mostly clear. ",
            "Partly cloudy with little temperature change. ", "Partly cloudy and cooler. ", "Unknown forecast rule."
        };

        public string[] DavisForecast2 =
        {
            "", "Precipitation possible within 48 hours. ", "Precipitation possible within 24 to 48 hours. ",
            "Precipitation possible within 24 hours. ", "Precipitation possible within 12 to 24 hours. ",
            "Precipitation possible within 12 hours, possibly heavy at times. ", "Precipitation possible within 12 hours. ",
            "Precipitation possible within 6 to 12 hours. ", "Precipitation possible within 6 to 12 hours, possibly heavy at times. ",
            "Precipitation possible and windy within 6 hours. ", "Precipitation possible within 6 hours. ", "Precipitation ending in 12 to 24 hours. ",
            "Precipitation possibly heavy at times and ending within 12 hours. ", "Precipitation ending within 12 hours. ",
            "Precipitation ending within 6 hours. ", "Precipitation likely, possibly heavy at times. ", "Precipitation likely. ",
            "Precipitation continuing, possibly heavy at times. ", "Precipitation continuing. "
        };

        public string[] DavisForecast3 =
        {
            "", "Windy with possible wind shift to the W, SW, or S.", "Possible wind shift to the W, SW, or S.",
            "Windy with possible wind shift to the W, NW, or N.", "Possible wind shift to the W, NW, or N.", "Windy.", "Increasing winds."
        };



        // equivalents of Zambretti "dial window" letters A - Z
        public int[] rise_options = { 25, 25, 25, 24, 24, 19, 16, 12, 11, 9, 8, 6, 5, 2, 1, 1, 0, 0, 0, 0, 0, 0 };
        public int[] steady_options = { 25, 25, 25, 25, 25, 25, 23, 23, 22, 18, 15, 13, 10, 4, 1, 1, 0, 0, 0, 0, 0, 0 };
        public int[] fall_options = { 25, 25, 25, 25, 25, 25, 25, 25, 23, 23, 21, 20, 17, 14, 7, 3, 1, 1, 1, 0, 0, 0 };

        internal int[] FactorsOf60 = { 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60 };

        public bool UseWind10MinAve { get; set; }
        public bool UseSpeedForAvgCalc { get; set; }
        public bool UseZeroBearing { get; set; }
        public TimeSpan AvgSpeedTime { get; set; }
        public int AvgSpeedMinutes { get; set; }

        public int PeakGustMinutes { get; set; }
        public TimeSpan PeakGustTime { get; set; }
        public TimeSpan AvgBearingTime { get; set; }

        public bool UTF8encode { get; set; }

        internal int TempDPlaces { get; set; }
        public string TempFormat { get; set; }

        internal int WindDPlaces { get; set; }
        public string WindFormat { get; set; }

        internal int HumDPlaces { get; set; }
        public string HumFormat { get; set; }

        private readonly int WindRunDPlaces = 1;
        public string WindRunFormat;

        public int RainDPlaces = 1;
        public string RainFormat;

        internal int PressDPlaces = 1;
        internal bool DavisIncrementPressureDP;
        public string PressFormat;

        internal int UVDPlaces = 1;
        public string UVFormat;

        public string ETFormat;

        public int VPrainGaugeType = -1;

        public string ComportName { get; set; }
        public string DefaultComportName { get; set; }
        public int ImetBaudRate { get; set; }

        public int VendorID { get; set; }
        public int ProductID { get; set; }




        public int RolloverHour { get; set; }
        public bool Use10amInSummer { get; set; }



        public double RStransfactor = 0.8;


        public bool LogExtraSensors { get; set; }

        public bool UseDavisLoop2 { get; set; }
        public bool DavisReadReceptionStats { get; set; }
        public int DavisReadTimeout { get; set; }



        private string twitterKey = "lQiGNdtlYUJ4wS3d7souPw";
        private string twitterSecret = "AoB7OqimfoaSfGQAd47Hgatqdv3YeTTiqpinkje6Xg";

        public int FineOffsetReadTime { get; set; }

        private readonly string AlltimeFile;
        public string AlltimeIniFile;
        public string Alltimelogfile;
        public string MonthlyAlltimeIniFile;
        private readonly string LogFilePath;
        public string DayFile;
        public string YesterdayFile;
        public string TodayIniFile;
        public string MonthIniFile;
        public string YearIniFile;
        //private readonly string stringsFile;
        private readonly string backuppath;
        //private readonly string ExternaldataFile;
        public string WebTagFile;
        private readonly string Indexfile;
        private readonly string Todayfile;
        private readonly string Yesterfile;
        private readonly string Recordfile;
        private readonly string Trendsfile;
        private readonly string Gaugesfile;
        private readonly string ThisMonthfile;
        private readonly string ThisYearfile;
        private readonly string MonthlyRecordfile;

        private readonly string[] localwebtextfiles;
        private readonly string[] remotewebtextfiles;

        public bool SynchronisedWebUpdate;
        public bool SynchronisedWUUpdate;
        public bool SynchronisedAwekasUpdate;
        public bool SynchronisedWCloudUpdate;
        public bool SynchronisedWOWUpdate;
        public bool SynchronisedPWSUpdate;
        public bool SynchronisedTwitterUpdate;
        public bool SynchronisedWBUpdate;
        public bool SynchronisedAPRSUpdate;

        private List<string> WundList = new List<string>();
        private List<string> PWSList = new List<string>();
        private List<string> WeatherbugList = new List<string>();
        private List<string> WOWList = new List<string>();

        private List<string> MySqlList = new List<string>();

        // Calibration settings
        /// <summary>
        /// User pressure calibration
        /// </summary>
        public double PressOffset { get; private set; }

        public double TempOffset { get; private set; }
        public int HumOffset { get; private set; }
        public int WindDirOffset { get; private set; }
        public double InTempoffset { get; private set; }
        public double UVOffset { get; private set; }
        public double WetBulbOffset { get; private set; }

        public double WindSpeedMult = 1.0;
        public double WindGustMult = 1.0;
        public double TempMult = 1.0;
        public double TempMult2 = 0.0;
        public double HumMult = 1.0;
        public double HumMult2 = 0.0;
        public double RainMult = 1.0;
        public double UVMult = 1.0;
        public double WetBulbMult = 1.0;

        //private int CurrentYear;
        //private int CurrentMonth;
        //private int CurrentDay;

        public bool ListWebTags;

        public bool RealtimeEnabled; // The timer is to be started
        public bool RealtimeFTPEnabled; // The FTP connection is to be established
        public bool RealtimeTxtFTP; // The realtime.txt file is to be uploaded
        public bool RealtimeGaugesTxtFTP; // The realtimegauges.txt file is to be uploaded

        // Twitter settings
        public string Twitteruser = " ";
        public string TwitterPW = " ";
        public bool TwitterEnabled = false;
        public int TwitterInterval = 10;
        private string TwitterOauthToken = "unknown";
        private string TwitterOauthTokenSecret = "unknown";

        // Wunderground settings
        public string WundID = " ";
        public string WundPW = " ";
        public bool WundEnabled = false;
        public bool WundRapidFireEnabled = false;
        public int WundInterval = 15;
        private bool HTTPLogging = false;
        public bool SendUVToWund = false;
        public bool SendSRToWund = false;
        public bool SendIndoorToWund = false;
        public bool WundSendAverage = false;
        public bool WundCatchUp = true;
        public bool WundCatchingUp = false;

        // PWS Weather settings
        public string PWSID = " ";
        public string PWSPW = " ";
        public bool PWSEnabled = false;
        public int PWSInterval = 15;
        public bool SendUVToPWS = false;
        public bool SendSRToPWS = false;
        public bool PWSCatchUp = true;
        public bool PWSCatchingUp = false;

        // WOW settings
        public string WOWID = " ";
        public string WOWPW = " ";
        public bool WOWEnabled = false;
        public int WOWInterval = 15;
        public bool SendUVToWOW = false;
        public bool SendSRToWOW = false;
        public bool WOWCatchUp = true;
        public bool WOWCatchingUp = false;

        // Weatherbug settings
        public string WeatherbugID = " ";
        public string WeatherbugNumber = " ";
        public string WeatherbugPW = " ";
        public bool WeatherbugEnabled = false;
        public int WeatherbugInterval = 15;
        public bool SendUVToWeatherbug = false;
        public bool SendSRToWeatherbug = false;
        public bool WeatherbugCatchUp = true;
        public bool WeatherbugCatchingUp = false;

        // APRS settings
        public string APRSserver = "cwop.aprs.net";
        public int APRSport = 14580;
        public int APRSinterval = 9;
        public bool APRSenabled = false;
        public string APRSID = "";
        public string APRSpass = "-1";
        public bool SendSRToAPRS = false;

        // Awekas settings
        public string AwekasUser = " ";
        public string AwekasPW = " ";
        public bool AwekasEnabled = false;
        public int AwekasInterval = 15;
        public string AwekasLang = "en";
        public bool SendUVToAwekas;
        public bool SendSolarToAwekas;
        public bool SendSoilTempToAwekas;

        // WeatherCloud settings
        public string WCloudWid = " ";
        public string WCloudKey = " ";
        public bool WCloudEnabled = false;
        public int WCloudInterval = DefaultWCloudInterval;
        public bool SendUVToWCloud;
        public bool SendSolarToWCloud;

        // NOAA report settings
        public string NOAAname;
        public string NOAAcity;
        public string NOAAstate;
        public double NOAAheatingthreshold;
        public double NOAAcoolingthreshold;
        public double NOAAmaxtempcomp1;
        public double NOAAmaxtempcomp2;
        public double NOAAmintempcomp1;
        public double NOAAmintempcomp2;
        public double NOAAraincomp1;
        public double NOAAraincomp2;
        public double NOAAraincomp3;
        public bool NOAA12hourformat;
        public bool NOAAAutoSave;
        public bool NOAAAutoFTP;
        public bool NOAANeedFTP = false;
        public string NOAAMonthFileFormat;
        public string NOAAYearFileFormat;
        public string NOAAFTPDirectory;
        public string NOAALatestMonthlyReport;
        public string NOAALatestYearlyReport;
        public bool NOAAUseUTF8;

        public double NOAATempNormJan;
        public double NOAATempNormFeb;
        public double NOAATempNormMar;
        public double NOAATempNormApr;
        public double NOAATempNormMay;
        public double NOAATempNormJun;
        public double NOAATempNormJul;
        public double NOAATempNormAug;
        public double NOAATempNormSep;
        public double NOAATempNormOct;
        public double NOAATempNormNov;
        public double NOAATempNormDec;

        public double NOAARainNormJan;
        public double NOAARainNormFeb;
        public double NOAARainNormMar;
        public double NOAARainNormApr;
        public double NOAARainNormMay;
        public double NOAARainNormJun;
        public double NOAARainNormJul;
        public double NOAARainNormAug;
        public double NOAARainNormSep;
        public double NOAARainNormOct;
        public double NOAARainNormNov;
        public double NOAARainNormDec;

        public bool IsOSX;

        private const double DEFAULTFCLOWPRESS = 950.0;
        private const double DEFAULTFCHIGHPRESS = 1050.0;

        private const string ForumDefault = "https://cumulus.hosiene.co.uk/";

        private const string WebcamDefault = "";

        private const string DefaultSoundFile = "alert.wav";

        public int RealtimeInterval;

        public string ForecastNotAvailable;

        //// Custom HTTP - seconds
        //private static HttpClientHandler customHttpSecondsHandler = new HttpClientHandler();
        //private HttpClient customHttpSecondsClient = new HttpClient(customHttpSecondsHandler);
        //private bool updatingCustomHttpSeconds = false;
        //private TokenParser customHttpSecondsTokenParser = new TokenParser();
        //internal Timer CustomHttpSecondsTimer;
        internal bool CustomHttpSecondsEnabled;
        internal string CustomHttpSecondsString;
        internal int CustomHttpSecondsInterval;

        //// Custom HTTP - minutes
        //private static HttpClientHandler customHttpMinutesHandler = new HttpClientHandler();
        //private HttpClient customHttpMinutesClient = new HttpClient(customHttpMinutesHandler);
        //private bool updatingCustomHttpMinutes = false;
        //private TokenParser customHttpMinutesTokenParser = new TokenParser();
        internal bool CustomHttpMinutesEnabled;
        internal string CustomHttpMinutesString;
        internal int CustomHttpMinutesInterval;
        internal int CustomHttpMinutesIntervalIndex;

        //// Custom HTTP - rollover
        //private static HttpClientHandler customHttpRolloverHandler = new HttpClientHandler();
        //private HttpClient customHttpRolloverClient = new HttpClient(customHttpRolloverHandler);
        //private bool updatingCustomHttpRollover = false;
        //private TokenParser customHttpRolloverTokenParser = new TokenParser();
        internal bool CustomHttpRolloverEnabled;
        internal string CustomHttpRolloverString;

        public string xapHeartbeat;
        public string xapsource;

        //public MySqlConnection MonthlyMySqlConn = new MySqlConnection();
        //public MySqlConnection RealtimeSqlConn = new MySqlConnection();
        //public MySqlConnection CustomMysqlSecondsConn = new MySqlConnection();
        //public MySqlCommand CustomMysqlSecondsCommand = new MySqlCommand();
        //public MySqlConnection CustomMysqlMinutesConn = new MySqlConnection();
        //public MySqlCommand CustomMysqlMinutesCommand = new MySqlCommand();
        //public MySqlConnection CustomMysqlRolloverConn = new MySqlConnection();
        //public MySqlCommand CustomMysqlRolloverCommand = new MySqlCommand();
        public string MySqlHost;
        public int MySqlPort;
        public string MySqlUser;
        public string MySqlPass;
        public string MySqlDatabase;

        public bool RealtimeMySqlEnabled;
        public bool MonthlyMySqlEnabled;
        public bool DayfileMySqlEnabled;

        public string MySqlMonthlyTable;
        public string MySqlDayfileTable;
        public string MySqlRealtimeTable;
        public string MySqlRealtimeRetention;
        public string StartOfMonthlyInsertSQL;
        public string StartOfDayfileInsertSQL;
        public string StartOfRealtimeInsertSQL;
        public string DeleteRealtimeSQL;

        public string CreateMonthlySQL;
        public string CreateDayfileSQL;
        public string CreateRealtimeSQL;

        public string CustomMySqlSecondsCommandString;
        public string CustomMySqlMinutesCommandString;
        public string CustomMySqlRolloverCommandString;

        public bool CustomMySqlSecondsEnabled;
        public bool CustomMySqlMinutesEnabled;
        public bool CustomMySqlRolloverEnabled;

        public int CustomMySqlSecondsInterval;
        public int CustomMySqlMinutesInterval;
        public int CustomMySqlMinutesIntervalIndex;

        private bool customMySqlSecondsUpdateInProgress = false;
        private bool customMySqlMinutesUpdateInProgress = false;
        private bool customMySqlRolloverUpdateInProgress = false;


        public string[] StationDesc =
        {
            "Davis Vantage Pro", "Davis Vantage Pro2", "Oregon Scientific WMR-928", "Oregon Scientific WM-918", "EasyWeather", "Fine Offset",
            "LaCrosse WS2300", "Fine Offset with Solar", "Oregon Scientific WMR100", "Oregon Scientific WMR200", "Instromet"
        };

        public string[] APRSstationtype = { "DsVP", "DsVP", "WMR928", "WM918", "EW", "FO", "WS2300", "FOs", "WMR100", "WMR200", "Instromet" };





        public bool UseBlakeLarsen { get; set; }

        public double LuxToWM2 { get; set; }

        public int SolarMinimum { get; set; }

        public int SunThreshold { get; set; }

        public int SolarCalc { get; set; }

        public double BrasTurbidity { get; set; }

        public int xapPort { get; set; }

        public string xapUID { get; set; }

        public bool xapEnabled { get; set; }

        public bool APRSHumidityCutoff { get; set; }

        public bool CloudBaseInFeet { get; set; }

        public string WebcamURL { get; set; }

        public string ForumURL { get; set; }

        public string DailyParams { get; set; }

        public string RealtimeParams { get; set; }

        public string ExternalParams { get; set; }

        public string DailyProgram { get; set; }

        public string RealtimeProgram { get; set; }

        public string ExternalProgram { get; set; }

        public TExtraFiles[] ExtraFiles = new TExtraFiles[numextrafiles];

        public int MaxFTPconnectRetries { get; set; }

        public bool DeleteBeforeUpload { get; set; }

        public bool FTPRename { get; set; }

        public int UpdateInterval { get; set; }

        //public Timer RealtimeTimer = new Timer();

        //internal Timer CustomMysqlSecondsTimer;

        public bool ActiveFTPMode { get; set; }

        public bool Sslftp { get; set; }
        public bool DisableEPSV { get; set; }

        public bool FTPlogging { get; set; }

        public bool WebAutoUpdate { get; set; }

        public string ftp_directory { get; set; }

        public string ftp_password { get; set; }

        public string ftp_user { get; set; }

        public int ftp_port { get; set; }

        public string ftp_host { get; set; }

        public bool CreateWxnowTxt { get; set; }

        public int WMR200TempChannel { get; set; }

        public int WMR928TempChannel { get; set; }

        public int RTdisconnectcount { get; set; }

        public int VP2PeriodicDisconnectInterval { get; set; }

        public int VP2SleepInterval { get; set; }

        public int VPClosedownTime { get; set; }

        public bool WarnMultiple { get; set; }

        public string VP2IPAddr { get; set; }

        public int VP2TCPPort { get; set; }

        public int VP2ConnectionType { get; set; }

        public bool logging { get; set; }
        public bool DataLogging { get; set; }

        public bool solar_logging { get; set; }

        public bool special_logging { get; set; }

        public bool RG11DTRmode2 { get; set; }

        public bool RG11IgnoreFirst2 { get; set; }

        public double RG11tipsize2 { get; set; }

        public bool RG11TBRmode2 { get; set; }

        public string RG11Port2 { get; set; }

        public bool RG11DTRmode { get; set; }

        public bool RG11IgnoreFirst { get; set; }

        public double RG11tipsize { get; set; }

        public bool RG11TBRmode { get; set; }

        public string RG11Port { get; set; }

        public double ChillHourThreshold { get; set; }

        public int ChillHourSeasonStart { get; set; }

        public int RainSeasonStart { get; set; }

        public double FCPressureThreshold { get; set; }

        public double FChighpress { get; set; }

        public double FClowpress { get; set; }

        public bool FCpressinMB { get; set; }

        public double RainDayThreshold { get; set; }

        public int SnowDepthHour { get; set; }

        public bool UseWindChillCutoff { get; set; }

        public bool UseCumulusPresstrendstr { get; set; }

        public bool HourlyForecast { get; set; }

        public bool UseCumulusForecast { get; set; }

        public bool UseDataLogger { get; set; }

        public int ImetWaitTime { get; set; }

        public bool ImetUpdateLogPointer { get; set; }

        public bool DavisConsoleHighGust { get; set; }

        public bool DavisCalcAltPress { get; set; }

        public bool DavisUseDLLBarCalData { get; set; }

        public bool ForceVPBarUpdate { get; set; }

        public int LCMaxWind { get; set; }

        public double EWpressureoffset { get; set; }

        public int EWMaxRainTipDiff { get; set; }

        public int EWmaxpressureMB { get; set; }

        public int EWminpressureMB { get; set; }

        public double EWmaxHourlyRain { get; set; }

        public double EWmaxRainRate { get; set; }

        public double EWwinddiff { get; set; }

        public double EWgustdiff { get; set; }

        public double EWhumiditydiff { get; set; }

        public double EWpressurediff { get; set; }

        public double EWtempdiff { get; set; }

        public bool EWduplicatecheck { get; set; }

        public string RecordsBeganDate { get; set; }

        public bool EWdisablecheckinit { get; set; }

        public bool EWallowFF { get; set; }

        public string EWFile { get; set; }

        public double EWInterval { get; set; }

        public int YTDrainyear { get; set; }

        public double YTDrain { get; set; }

        public string LocationDesc { get; set; }

        public string LocationName { get; set; }

        public string HTTPProxyPassword { get; set; }

        public string HTTPProxyUser { get; set; }

        public int HTTPProxyPort { get; set; }

        public string HTTPProxyName { get; set; }

        public int[] WindDPlace = { 1, 1, 1, 1 };
        public int[] TempDPlace = { 1, 1 };
        public int[] PressDPlace = { 1, 1, 2 };
        public int[] RainDPlace = { 1, 2 };
        public const int numextrafiles = 99;

        public bool RoundWindSpeed { get; set; }

        public int RainUnit { get; set; }

        public int PressUnit { get; set; }

        public int WindUnit { get; set; }

        public bool WS2300Sync { get; set; }

        public int FOReadAvoidPeriod { get; set; }

        public bool SyncFOReads { get; set; }

        public bool ErrorLogSpikeRemoval { get; set; }

        public bool NoFlashWetDryDayRecords { get; set; }

        public bool ReportLostSensorContact { get; set; }

        public bool ReportDataStoppedErrors { get; set; }

        public bool WS2300IgnoreStationClock { get; set; }

        public bool SyncTime { get; set; }

        public int ClockSettingHour { get; set; }

        public bool RestartIfDataStops { get; set; }

        public bool RestartIfUnplugged { get; set; }

        public bool CloseOnSuspend { get; set; }

        public bool ConfirmClose { get; set; }

        public int DataLogInterval { get; set; }

        public bool CalculatedWC { get; set; }

        public bool CalculatedDP { get; set; }

        public bool NoSensorCheck { get; set; }

        public int serial_port { get; set; }

        public int UVdecimals { get; set; }

        public int UVdecimaldefault { get; set; }

        public string LonTxt { get; set; }

        public string LatTxt { get; set; }

        public int AvgBearingMinutes { get; set; }

        public bool Humidity98Fix { get; set; }

        public int TempUnit { get; set; }

        public bool AltitudeInFeet { get; set; }

        public string StationModel { get; set; }

        public int StationType { get; set; }


        public string LatestImetReading { get; set; }

        public bool FineOffsetStation { get; set; }

        public bool DavisStation { get; set; }
        public string TempTrendFormat { get; set; }
        public string AppDir { get; set; }

        public int Manufacturer { get; set; }
        public int ImetLoggerInterval { get; set; }
        public TimeSpan DayLength { get; set; }
        public DateTime Dawn;
        public DateTime Dusk;
        public TimeSpan DaylightLength { get; set; }
        public int DavisInitWaitTime { get; set; }
        public int DavisIPResponseTime { get; set; }
        public int GraphHours { get; set; }

        //private WeatherStation Station
        //{
        //    set { station = value; }
        //    get { return station; }

        //}

        //public Timer WundTimer = new Timer();
        //public Timer PWSTimer = new Timer();
        //public Timer WOWTimer = new Timer();
        //public Timer WeatherbugTimer = new Timer();
        //public Timer APRStimer = new Timer();
        //public Timer WebTimer = new Timer();
        //public Timer TwitterTimer = new Timer();
        //public Timer AwekasTimer = new Timer();
        //public Timer WCloudTimer = new Timer();

        public int DAVIS = 0;
        public int OREGON = 1;
        public int EW = 2;
        public int LACROSSE = 3;
        public int OREGONUSB = 4;
        public int INSTROMET = 5;
        public bool startingup = true;
        public bool StartOfDayBackupNeeded = false;
        public string ReportPath = "Reports";
        public string LatestError;
        public DateTime LatestErrorTS = DateTime.MinValue;
        public string wxnowfile = "wxnow.txt";
        private readonly string IndexTFile;
        private readonly string TodayTFile;
        private readonly string YesterdayTFile;
        private readonly string RecordTFile;
        private readonly string MonthlyRecordTFile;
        private readonly string TrendsTFile;
        private readonly string ThisMonthTFile;
        private readonly string ThisYearTFile;
        private readonly string GaugesTFile;
        private readonly string RealtimeFile = "realtime.txt";
        private readonly string RealtimeGaugesTxtTFile;
        private readonly string RealtimeGaugesTxtFile;
        private readonly string TwitterTxtFile;
        public bool IncludeStandardFiles = true;
        public bool IncludeGraphDataFiles;
        public bool TwitterSendLocation;
        private const int numwebtextfiles = 9;
        //private FtpClient RealtimeFTP = new FtpClient();
        private bool RealtimeInProgress = false;
        public bool SendSoilTemp1ToWund;
        public bool SendSoilTemp2ToWund;
        public bool SendSoilTemp3ToWund;
        public bool SendSoilTemp4ToWund;
        public bool SendSoilMoisture1ToWund;
        public bool SendSoilMoisture2ToWund;
        public bool SendSoilMoisture3ToWund;
        public bool SendSoilMoisture4ToWund;
        public bool SendLeafWetness1ToWund;
        public bool SendLeafWetness2ToWund;
        private string[] localgraphdatafiles;
        private readonly string[] remotegraphdatafiles;
        public string exceptional;
        //		private WebSocketServer wsServer;
        public string[] WMR200ExtraChannelCaptions = new string[11];
        public string[] ExtraTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
        public string[] ExtraHumCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
        public string[] ExtraDPCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
        public string[] SoilTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4" };
        public string[] SoilMoistureCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4" };
        public string[] LeafCaptions = { "", "Temp 1", "Temp 2", "Wetness 1", "Wetness 2" };
        private string thereWillBeMinSLessDaylightTomorrow = "There will be {0}min {1}s less daylight tomorrow";
        private string thereWillBeMinSMoreDaylightTomorrow = "There will be {0}min {1}s more daylight tomorrow";












        internal void ReadIniFile()
        {
            StationType = ini.GetValue("Station", "Type", -1);

            StationModel = ini.GetValue("Station", "Model", "");

            FineOffsetStation = (StationType == StationTypes.FineOffset || StationType == StationTypes.FineOffsetSolar);
            DavisStation = (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2);

            UseDavisLoop2 = ini.GetValue("Station", "UseDavisLoop2", true);
            DavisReadReceptionStats = ini.GetValue("Station", "DavisReadReceptionStats", false);
            DavisInitWaitTime = ini.GetValue("Station", "DavisInitWaitTime", 200);
            DavisIPResponseTime = ini.GetValue("Station", "DavisIPResponseTime", 1000);
            DavisReadTimeout = ini.GetValue("Station", "DavisReadTimeout", 1000);
            DavisIncrementPressureDP = ini.GetValue("Station", "DavisIncrementPressureDP", true);
            if (StationType == StationTypes.VantagePro)
            {
                UseDavisLoop2 = false;
            }

            serial_port = ini.GetValue("Station", "Port", 0);

            ComportName = ini.GetValue("Station", "ComportName", DefaultComportName);
            ImetBaudRate = ini.GetValue("Station", "ImetBaudRate", 19200);

            VendorID = ini.GetValue("Station", "VendorID", -1);
            ProductID = ini.GetValue("Station", "ProductID", -1);

            Latitude = ini.GetValue("Station", "Latitude", 0.0);
            Longitude = ini.GetValue("Station", "Longitude", 0.0);

            LatTxt = ini.GetValue("Station", "LatTxt", "");
            LatTxt = LatTxt.Replace(" ", "&nbsp;");
            LatTxt = LatTxt.Replace("°", "&#39;");
            LonTxt = ini.GetValue("Station", "LonTxt", "");
            LonTxt = LonTxt.Replace(" ", "&nbsp;");
            LonTxt = LonTxt.Replace("°", "&#39;");

            Altitude = ini.GetValue("Station", "Altitude", 0.0);
            AltitudeInFeet = ini.GetValue("Station", "AltitudeInFeet", true);

            Humidity98Fix = ini.GetValue("Station", "Humidity98Fix", false);
            UseWind10MinAve = ini.GetValue("Station", "Wind10MinAverage", false);
            UseSpeedForAvgCalc = ini.GetValue("Station", "UseSpeedForAvgCalc", false);

            AvgBearingMinutes = ini.GetValue("Station", "AvgBearingMinutes", 10);
            if (AvgBearingMinutes > 120)
            {
                AvgBearingMinutes = 120;
            }
            if (AvgBearingMinutes == 0)
            {
                AvgBearingMinutes = 1;
            }

            AvgBearingTime = new TimeSpan(AvgBearingMinutes / 60, AvgBearingMinutes % 60, 0);

            AvgSpeedMinutes = ini.GetValue("Station", "AvgSpeedMinutes", 10);
            if (AvgSpeedMinutes > 120)
            {
                AvgSpeedMinutes = 120;
            }
            if (AvgSpeedMinutes == 0)
            {
                AvgSpeedMinutes = 1;
            }

            AvgSpeedTime = new TimeSpan(AvgSpeedMinutes / 60, AvgSpeedMinutes % 60, 0);

            log.Debug("ASM=" + AvgSpeedMinutes + " AST=" + AvgSpeedTime.ToString());

            PeakGustMinutes = ini.GetValue("Station", "PeakGustMinutes", 10);
            if (PeakGustMinutes > 120)
            {
                PeakGustMinutes = 120;
            }

            if (PeakGustMinutes == 0)
            {
                PeakGustMinutes = 1;
            }

            PeakGustTime = new TimeSpan(PeakGustMinutes / 60, PeakGustMinutes % 60, 0);

            if ((StationType == StationTypes.VantagePro) || (StationType == StationTypes.VantagePro2))
            {
                UVdecimaldefault = 1;
            }
            else
            {
                UVdecimaldefault = 0;
            }

            UVdecimals = ini.GetValue("Station", "UVdecimals", UVdecimaldefault);

            NoSensorCheck = ini.GetValue("Station", "NoSensorCheck", false);

            CalculatedDP = ini.GetValue("Station", "CalculatedDP", false);
            CalculatedWC = ini.GetValue("Station", "CalculatedWC", false);
            RolloverHour = ini.GetValue("Station", "RolloverHour", 0);
            Use10amInSummer = ini.GetValue("Station", "Use10amInSummer", true);
            ConfirmClose = ini.GetValue("Station", "ConfirmClose", false);
            CloseOnSuspend = ini.GetValue("Station", "CloseOnSuspend", false);
            RestartIfUnplugged = ini.GetValue("Station", "RestartIfUnplugged", false);
            RestartIfDataStops = ini.GetValue("Station", "RestartIfDataStops", false);
            SyncTime = ini.GetValue("Station", "SyncDavisClock", false);
            ClockSettingHour = ini.GetValue("Station", "ClockSettingHour", 4);
            WS2300IgnoreStationClock = ini.GetValue("Station", "WS2300IgnoreStationClock", false);
            LogExtraSensors = ini.GetValue("Station", "LogExtraSensors", false);
            ReportDataStoppedErrors = ini.GetValue("Station", "ReportDataStoppedErrors", true);
            ReportLostSensorContact = ini.GetValue("Station", "ReportLostSensorContact", true);
            NoFlashWetDryDayRecords = ini.GetValue("Station", "NoFlashWetDryDayRecords", false);
            ErrorLogSpikeRemoval = ini.GetValue("Station", "ErrorLogSpikeRemoval", false);
            DataLogInterval = ini.GetValue("Station", "DataLogInterval", 2);
            // this is now an index
            if (DataLogInterval > 5)
            {
                DataLogInterval = 2;
            }

            SyncFOReads = ini.GetValue("Station", "SyncFOReads", false);
            FOReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3);
            FineOffsetReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150);

            WS2300Sync = ini.GetValue("Station", "WS2300Sync", false);
            WindUnit = ini.GetValue("Station", "WindUnit", 0);
            PressUnit = ini.GetValue("Station", "PressureUnit", 0);

            RainUnit = ini.GetValue("Station", "RainUnit", 0);
            TempUnit = ini.GetValue("Station", "TempUnit", 0);

            RoundWindSpeed = ini.GetValue("Station", "RoundWindSpeed", false);

            WindDPlaces = RoundWindSpeed ? 0 : WindDPlace[WindUnit];

            TempDPlaces = TempDPlace[TempUnit];
            PressDPlaces = PressDPlace[PressUnit];
            if ((StationType == 0 || StationType == 1) && DavisIncrementPressureDP)
            {
                // Use one more DP for Davis stations
                ++PressDPlaces;
            }
            RainDPlaces = RainDPlace[RainUnit];

            LocationName = ini.GetValue("Station", "LocName", "");
            LocationDesc = ini.GetValue("Station", "LocDesc", "");

            YTDrain = ini.GetValue("Station", "YTDrain", 0.0);
            YTDrainyear = ini.GetValue("Station", "YTDrainyear", 0);

            EWInterval = ini.GetValue("Station", "EWInterval", 1.0);
            EWFile = ini.GetValue("Station", "EWFile", "");
            EWallowFF = ini.GetValue("Station", "EWFF", false);
            EWdisablecheckinit = ini.GetValue("Station", "EWdisablecheckinit", false);
            EWduplicatecheck = ini.GetValue("Station", "EWduplicatecheck", true);

            EWtempdiff = ini.GetValue("Station", "EWtempdiff", 999.0);
            EWpressurediff = ini.GetValue("Station", "EWpressurediff", 999.0);
            EWhumiditydiff = ini.GetValue("Station", "EWhumiditydiff", 999.0);
            EWgustdiff = ini.GetValue("Station", "EWgustdiff", 999.0);
            EWwinddiff = ini.GetValue("Station", "EWwinddiff", 999.0);
            EWmaxRainRate = ini.GetValue("Station", "EWmaxRainRate", 999.0);
            EWmaxHourlyRain = ini.GetValue("Station", "EWmaxHourlyRain", 999.0);

            EWminpressureMB = ini.GetValue("Station", "EWminpressureMB", 900);
            EWmaxpressureMB = ini.GetValue("Station", "EWmaxpressureMB", 1200);

            EWMaxRainTipDiff = ini.GetValue("Station", "EWMaxRainTipDiff", 30);

            EWpressureoffset = ini.GetValue("Station", "EWpressureoffset", 9999.0);

            LCMaxWind = ini.GetValue("Station", "LCMaxWind", 9999);

            ForceVPBarUpdate = ini.GetValue("Station", "ForceVPBarUpdate", false);
            DavisUseDLLBarCalData = ini.GetValue("Station", "DavisUseDLLBarCalData", false);
            DavisCalcAltPress = ini.GetValue("Station", "DavisCalcAltPress", true);
            DavisConsoleHighGust = ini.GetValue("Station", "DavisConsoleHighGust", false);
            VPrainGaugeType = ini.GetValue("Station", "VPrainGaugeType", -1);

            RecordsBeganDate = ini.GetValue("Station", "StartDate", DateTime.Now.ToLongDateString());

            log.Debug("Cumulus start date: " + RecordsBeganDate);

            ImetWaitTime = ini.GetValue("Station", "ImetWaitTime", 500);
            ImetUpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);

            UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
            UseCumulusForecast = ini.GetValue("Station", "UseCumulusForecast", false);
            HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
            UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
            UseWindChillCutoff = ini.GetValue("Station", "UseWindChillCutoff", false);

            SnowDepthHour = ini.GetValue("Station", "SnowDepthHour", 0);

            UseZeroBearing = ini.GetValue("Station", "UseZeroBearing", false);

            RainDayThreshold = ini.GetValue("Station", "RainDayThreshold", -1.0);

            FCpressinMB = ini.GetValue("Station", "FCpressinMB", true);
            FClowpress = ini.GetValue("Station", "FClowpress", DEFAULTFCLOWPRESS);
            FChighpress = ini.GetValue("Station", "FChighpress", DEFAULTFCHIGHPRESS);
            FCPressureThreshold = ini.GetValue("Station", "FCPressureThreshold", -1.0);

            RainSeasonStart = ini.GetValue("Station", "RainSeasonStart", 1);
            ChillHourSeasonStart = ini.GetValue("Station", "ChillHourSeasonStart", 10);
            ChillHourThreshold = ini.GetValue("Station", "ChillHourThreshold", -999.0);

            RG11Port = ini.GetValue("Station", "RG11portName", DefaultComportName);
            RG11TBRmode = ini.GetValue("Station", "RG11TBRmode", false);
            RG11tipsize = ini.GetValue("Station", "RG11tipsize", 0.0);
            RG11IgnoreFirst = ini.GetValue("Station", "RG11IgnoreFirst", false);
            RG11DTRmode = ini.GetValue("Station", "RG11DTRmode", true);

            RG11Port2 = ini.GetValue("Station", "RG11port2Name", DefaultComportName);
            RG11TBRmode2 = ini.GetValue("Station", "RG11TBRmode2", false);
            RG11tipsize2 = ini.GetValue("Station", "RG11tipsize2", 0.0);
            RG11IgnoreFirst2 = ini.GetValue("Station", "RG11IgnoreFirst2", false);
            RG11DTRmode2 = ini.GetValue("Station", "RG11DTRmode2", true);

            if (ChillHourThreshold < -998)
            {
                if (TempUnit == 0)
                {
                    ChillHourThreshold = 7; // C
                }
                else
                {
                    ChillHourThreshold = 45; // F
                }
            }

            if (FCPressureThreshold < 0)
            {
                if (PressUnit == 2)
                {
                    FCPressureThreshold = 0.00295333727; // 0.1 mb in inHg
                }
                else
                {
                    FCPressureThreshold = 0.1;
                }
            }

            special_logging = ini.GetValue("Station", "SpecialLog", false);
            solar_logging = ini.GetValue("Station", "SolarLog", false);
            logging = ini.GetValue("Station", "Logging", false);
            DataLogging = ini.GetValue("Station", "DataLogging", false);

            VP2ConnectionType = ini.GetValue("Station", "VP2ConnectionType", VP2SERIALCONNECTION);
            VP2TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222);
            VP2IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");

            WarnMultiple = ini.GetValue("Station", "WarnMultiple", false);

            VPClosedownTime = ini.GetValue("Station", "VPClosedownTime", 99999999);

            VP2SleepInterval = ini.GetValue("Station", "VP2SleepInterval", 0);

            VP2PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0);

            RTdisconnectcount = ini.GetValue("Station", "RTdisconnectcount", 0);

            WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0);

            WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1);

            CreateWxnowTxt = ini.GetValue("Station", "CreateWxnowTxt", true);

            ListWebTags = ini.GetValue("Station", "ListWebTags", false);

            ftp_host = ini.GetValue("FTP site", "Host", "");
            ftp_port = ini.GetValue("FTP site", "Port", 21);
            ftp_user = ini.GetValue("FTP site", "Username", "");
            ftp_password = ini.GetValue("FTP site", "Password", "");
            ftp_directory = ini.GetValue("FTP site", "Directory", "");

            WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);
            ActiveFTPMode = ini.GetValue("FTP site", "ActiveFTP", false);
            Sslftp = ini.GetValue("FTP site", "Sslftp", false);
            DisableEPSV = ini.GetValue("FTP site", "DisableEPSV", false);
            FTPlogging = ini.GetValue("FTP site", "FTPlogging", false);
            RealtimeEnabled = ini.GetValue("FTP site", "EnableRealtime", false);
            RealtimeFTPEnabled = ini.GetValue("FTP site", "RealtimeFTPEnabled", false);
            RealtimeTxtFTP = ini.GetValue("FTP site", "RealtimeTxtFTP", false);
            RealtimeGaugesTxtFTP = ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);
            RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000);
            if (RealtimeInterval < 1) { RealtimeInterval = 1; }
            //RealtimeTimer.Change(0,RealtimeInterval);
            UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", DefaultWebUpdateInterval);
            if (UpdateInterval < 1) { UpdateInterval = 1; }
            SynchronisedWebUpdate = (60 % UpdateInterval == 0);
            IncludeStandardFiles = ini.GetValue("FTP site", "IncludeSTD", true);
            IncludeGraphDataFiles = ini.GetValue("FTP site", "IncludeGraphDataFiles", true);

            FTPRename = ini.GetValue("FTP site", "FTPRename", false);
            UTF8encode = ini.GetValue("FTP site", "UTF8encode", true);
            DeleteBeforeUpload = ini.GetValue("FTP site", "DeleteBeforeUpload", false);

            MaxFTPconnectRetries = ini.GetValue("FTP site", "MaxFTPconnectRetries", 3);

            for (int i = 0; i < numextrafiles; i++)
            {
                ExtraFiles[i].local = ini.GetValue("FTP site", "ExtraLocal" + i, "");
                ExtraFiles[i].remote = ini.GetValue("FTP site", "ExtraRemote" + i, "");
                ExtraFiles[i].process = ini.GetValue("FTP site", "ExtraProcess" + i, false);
                ExtraFiles[i].binary = ini.GetValue("FTP site", "ExtraBinary" + i, false);
                ExtraFiles[i].realtime = ini.GetValue("FTP site", "ExtraRealtime" + i, false);
                ExtraFiles[i].FTP = ini.GetValue("FTP site", "ExtraFTP" + i, true);
                ExtraFiles[i].UTF8 = ini.GetValue("FTP site", "ExtraUTF" + i, false);
            }

            ExternalProgram = ini.GetValue("FTP site", "ExternalProgram", "");
            RealtimeProgram = ini.GetValue("FTP site", "RealtimeProgram", "");
            DailyProgram = ini.GetValue("FTP site", "DailyProgram", "");
            ExternalParams = ini.GetValue("FTP site", "ExternalParams", "");
            RealtimeParams = ini.GetValue("FTP site", "RealtimeParams", "");
            DailyParams = ini.GetValue("FTP site", "DailyParams", "");

            ForumURL = ini.GetValue("Web Site", "ForumURL", ForumDefault);
            WebcamURL = ini.GetValue("Web Site", "WebcamURL", WebcamDefault);


            CloudBaseInFeet = ini.GetValue("Station", "CloudBaseInFeet", true);

            GraphDays = ini.GetValue("Graphs", "ChartMaxDays", 31);
            GraphHours = ini.GetValue("Graphs", "GraphHours", 24);

            WundID = ini.GetValue("Wunderground", "ID", "");
            WundPW = ini.GetValue("Wunderground", "Password", "");
            WundEnabled = ini.GetValue("Wunderground", "Enabled", false);
            WundRapidFireEnabled = ini.GetValue("Wunderground", "RapidFire", false);
            WundInterval = ini.GetValue("Wunderground", "Interval", DefaultWundInterval);
            HTTPLogging = ini.GetValue("Wunderground", "Logging", false);
            SendUVToWund = ini.GetValue("Wunderground", "SendUV", false);
            SendSRToWund = ini.GetValue("Wunderground", "SendSR", false);
            SendIndoorToWund = ini.GetValue("Wunderground", "SendIndoor", false);
            SendSoilTemp1ToWund = ini.GetValue("Wunderground", "SendSoilTemp1", false);
            SendSoilTemp2ToWund = ini.GetValue("Wunderground", "SendSoilTemp2", false);
            SendSoilTemp3ToWund = ini.GetValue("Wunderground", "SendSoilTemp3", false);
            SendSoilTemp4ToWund = ini.GetValue("Wunderground", "SendSoilTemp4", false);
            SendSoilMoisture1ToWund = ini.GetValue("Wunderground", "SendSoilMoisture1", false);
            SendSoilMoisture2ToWund = ini.GetValue("Wunderground", "SendSoilMoisture2", false);
            SendSoilMoisture3ToWund = ini.GetValue("Wunderground", "SendSoilMoisture3", false);
            SendSoilMoisture4ToWund = ini.GetValue("Wunderground", "SendSoilMoisture4", false);
            SendLeafWetness1ToWund = ini.GetValue("Wunderground", "SendLeafWetness1", false);
            SendLeafWetness2ToWund = ini.GetValue("Wunderground", "SendLeafWetness2", false);
            WundSendAverage = ini.GetValue("Wunderground", "SendAverage", false);
            WundCatchUp = ini.GetValue("Wunderground", "CatchUp", true);

            SynchronisedWUUpdate = (!WundRapidFireEnabled) && (60 % WundInterval == 0);

            AwekasUser = ini.GetValue("Awekas", "User", "");
            AwekasPW = ini.GetValue("Awekas", "Password", "");
            AwekasEnabled = ini.GetValue("Awekas", "Enabled", false);
            AwekasInterval = ini.GetValue("Awekas", "Interval", DefaultAwekasInterval);
            if (AwekasInterval < 1) { AwekasInterval = 1; }
            SendUVToAwekas = ini.GetValue("Awekas", "SendUV", false);
            SendSolarToAwekas = ini.GetValue("Awekas", "SendSR", false);
            SendSoilTempToAwekas = ini.GetValue("Awekas", "SendSoilTemp", false);

            SynchronisedAwekasUpdate = (60 % AwekasInterval == 0);

            WCloudWid = ini.GetValue("WeatherCloud", "Wid", "");
            WCloudKey = ini.GetValue("WeatherCloud", "Key", "");
            WCloudEnabled = ini.GetValue("WeatherCloud", "Enabled", false);
            //WCloudInterval = ini.GetValue("WeatherCloud", "Interval", DefaultWCloudInterval);
            SendUVToWCloud = ini.GetValue("WeatherCloud", "SendUV", false);
            SendSolarToWCloud = ini.GetValue("WeatherCloud", "SendSR", false);

            SynchronisedWCloudUpdate = (60 % WCloudInterval == 0);

            Twitteruser = ini.GetValue("Twitter", "User", "");
            TwitterPW = ini.GetValue("Twitter", "Password", "");
            TwitterEnabled = ini.GetValue("Twitter", "Enabled", false);
            TwitterInterval = ini.GetValue("Twitter", "Interval", 60);
            if (TwitterInterval < 1) { TwitterInterval = 1; }
            TwitterOauthToken = ini.GetValue("Twitter", "OauthToken", "unknown");
            TwitterOauthTokenSecret = ini.GetValue("Twitter", "OauthTokenSecret", "unknown");
            TwitterSendLocation = ini.GetValue("Twitter", "SendLocation", true);

            SynchronisedTwitterUpdate = (60 % TwitterInterval == 0);

            //if HTTPLogging then
            //  MainForm.WUHTTP.IcsLogger = MainForm.HTTPlogger;

            PWSID = ini.GetValue("PWSweather", "ID", "");
            PWSPW = ini.GetValue("PWSweather", "Password", "");
            PWSEnabled = ini.GetValue("PWSweather", "Enabled", false);
            PWSInterval = ini.GetValue("PWSweather", "Interval", DefaultPWSInterval);
            if (PWSInterval < 1) { PWSInterval = 1; }
            SendUVToPWS = ini.GetValue("PWSweather", "SendUV", false);
            SendSRToPWS = ini.GetValue("PWSweather", "SendSR", false);
            PWSCatchUp = ini.GetValue("PWSweather", "CatchUp", true);

            SynchronisedPWSUpdate = (60 % PWSInterval == 0);

            WOWID = ini.GetValue("WOW", "ID", "");
            WOWPW = ini.GetValue("WOW", "Password", "");
            WOWEnabled = ini.GetValue("WOW", "Enabled", false);
            WOWInterval = ini.GetValue("WOW", "Interval", DefaultPWSInterval);
            if (WOWInterval < 1) { WOWInterval = 1; }
            SendUVToWOW = ini.GetValue("WOW", "SendUV", false);
            SendSRToWOW = ini.GetValue("WOW", "SendSR", false);
            WOWCatchUp = ini.GetValue("WOW", "CatchUp", true);

            SynchronisedWOWUpdate = (60 % WOWInterval == 0);

            WeatherbugID = ini.GetValue("Weatherbug", "ID", "");
            WeatherbugNumber = ini.GetValue("Weatherbug", "Number", "");
            WeatherbugPW = ini.GetValue("Weatherbug", "Password", "");
            WeatherbugEnabled = ini.GetValue("Weatherbug", "Enabled", false);
            WeatherbugInterval = ini.GetValue("Weatherbug", "Interval", DefaultPWSInterval);
            if (WeatherbugInterval < 1) { WeatherbugInterval = 1; }
            SendUVToWeatherbug = ini.GetValue("Weatherbug", "SendUV", false);
            SendSRToWeatherbug = ini.GetValue("Weatherbug", "SendSR", false);
            WeatherbugCatchUp = ini.GetValue("Weatherbug", "CatchUp", true);

            SynchronisedWBUpdate = (60 % WeatherbugInterval == 0);

            APRSID = ini.GetValue("APRS", "ID", "");
            APRSpass = ini.GetValue("APRS", "pass", "-1");
            APRSserver = ini.GetValue("APRS", "server", "cwop.aprs.net");
            APRSport = ini.GetValue("APRS", "port", 14580);
            APRSenabled = ini.GetValue("APRS", "Enabled", false);
            APRSinterval = ini.GetValue("APRS", "Interval", DefaultAPRSInterval);
            if (APRSinterval < 1) { APRSinterval = 1; }
            APRSHumidityCutoff = ini.GetValue("APRS", "APRSHumidityCutoff", false);
            SendSRToAPRS = ini.GetValue("APRS", "SendSR", false);

            SynchronisedAPRSUpdate = (60 % APRSinterval == 0);


            //alarmlowtemp = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
            //LowTempAlarmSet = ini.GetValue("Alarms", "LowTempAlarmSet", false);
            //LowTempAlarmSound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
            //LowTempAlarmSoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);

            //alarmhightemp = ini.GetValue("Alarms", "alarmhightemp", 0.0);
            //HighTempAlarmSet = ini.GetValue("Alarms", "HighTempAlarmSet", false);
            //HighTempAlarmSound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
            //HighTempAlarmSoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);

            //alarmtempchange = ini.GetValue("Alarms", "alarmtempchange", 0.0);
            //TempChangeAlarmSet = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
            //TempChangeAlarmSound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
            //TempChangeAlarmSoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);

            //alarmlowpress = ini.GetValue("Alarms", "alarmlowpress", 0.0);
            //LowPressAlarmSet = ini.GetValue("Alarms", "LowPressAlarmSet", false);
            //LowPressAlarmSound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
            //LowPressAlarmSoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);

            //alarmhighpress = ini.GetValue("Alarms", "alarmhighpress", 0.0);
            //HighPressAlarmSet = ini.GetValue("Alarms", "HighPressAlarmSet", false);
            //HighPressAlarmSound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
            //HighPressAlarmSoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);

            //alarmpresschange = ini.GetValue("Alarms", "alarmpresschange", 0.0);
            //PressChangeAlarmSet = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
            //PressChangeAlarmSound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
            //PressChangeAlarmSoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);

            //alarmhighraintoday = ini.GetValue("Alarms", "alarmhighraintoday", 0.0);
            //HighRainTodayAlarmSet = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
            //HighRainTodayAlarmSound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
            //HighRainTodayAlarmSoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);

            //alarmhighrainrate = ini.GetValue("Alarms", "alarmhighrainrate", 0.0);
            //HighRainRateAlarmSet = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
            //HighRainRateAlarmSound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
            //HighRainRateAlarmSoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);

            //alarmhighgust = ini.GetValue("Alarms", "alarmhighgust", 0.0);
            //HighGustAlarmSet = ini.GetValue("Alarms", "HighGustAlarmSet", false);
            //HighGustAlarmSound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
            //HighGustAlarmSoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);

            //alarmhighwind = ini.GetValue("Alarms", "alarmhighwind", 0.0);
            //HighWindAlarmSet = ini.GetValue("Alarms", "HighWindAlarmSet", false);
            //HighWindAlarmSound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
            //HighWindAlarmSoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);

            //SensorAlarmSet = ini.GetValue("Alarms", "SensorAlarmSet", false);
            //SensorAlarmSound = ini.GetValue("Alarms", "SensorAlarmSound", false);
            //SensorAlarmSoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);

            PressOffset = ini.GetValue("Offsets", "PressOffset", 0.0);
            TempOffset = ini.GetValue("Offsets", "TempOffset", 0.0);
            HumOffset = ini.GetValue("Offsets", "HumOffset", 0);
            WindDirOffset = ini.GetValue("Offsets", "WindDirOffset", 0);
            InTempoffset = ini.GetValue("Offsets", "InTempOffset", 0.0);
            UVOffset = ini.GetValue("Offsets", "UVOffset", 0.0);
            WetBulbOffset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);

            WindSpeedMult = ini.GetValue("Offsets", "WindSpeedMult", 1.0);
            WindGustMult = ini.GetValue("Offsets", "WindGustMult", 1.0);
            TempMult = ini.GetValue("Offsets", "TempMult", 1.0);
            TempMult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
            HumMult = ini.GetValue("Offsets", "HumMult", 1.0);
            HumMult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
            RainMult = ini.GetValue("Offsets", "RainMult", 1.0);
            UVMult = ini.GetValue("Offsets", "UVMult", 1.0);
            WetBulbMult = ini.GetValue("Offsets", "WetBulbMult", 1.0);

            xapEnabled = ini.GetValue("xAP", "Enabled", false);
            xapUID = ini.GetValue("xAP", "UID", "4375");
            xapPort = ini.GetValue("xAP", "Port", 3639);

            SunThreshold = ini.GetValue("Solar", "SunThreshold", 75);
            RStransfactor = ini.GetValue("Solar", "RStransfactor", 0.8);
            SolarMinimum = ini.GetValue("Solar", "SolarMinimum", 0);
            LuxToWM2 = ini.GetValue("Solar", "LuxToWM2", 0.0079);
            UseBlakeLarsen = ini.GetValue("Solar", "UseBlakeLarsen", false);
            SolarCalc = ini.GetValue("Solar", "SolarCalc", 0);
            BrasTurbidity = ini.GetValue("Solar", "BrasTurbidity", 2.0);

            NOAAname = ini.GetValue("NOAA", "Name", " ");
            NOAAcity = ini.GetValue("NOAA", "City", " ");
            NOAAstate = ini.GetValue("NOAA", "State", " ");
            NOAA12hourformat = ini.GetValue("NOAA", "12hourformat", false);
            NOAAheatingthreshold = ini.GetValue("NOAA", "HeatingThreshold", -1000.0);
            if (NOAAheatingthreshold < -999)
            {
                if (TempUnit == 0)
                {
                    NOAAheatingthreshold = 18.3;
                }
                else
                {
                    NOAAheatingthreshold = 65;
                }
            }
            NOAAcoolingthreshold = ini.GetValue("NOAA", "CoolingThreshold", -1000.0);
            if (NOAAcoolingthreshold < -999)
            {
                if (TempUnit == 0)
                {
                    NOAAcoolingthreshold = 18.3;
                }
                else
                {
                    NOAAcoolingthreshold = 65;
                }
            }
            NOAAmaxtempcomp1 = ini.GetValue("NOAA", "MaxTempComp1", -1000.0);
            if (NOAAmaxtempcomp1 < -999)
            {
                if (TempUnit == 0)
                    NOAAmaxtempcomp1 = 27;
                else
                    NOAAmaxtempcomp1 = 80;
            }
            NOAAmaxtempcomp2 = ini.GetValue("NOAA", "MaxTempComp2", -1000.0);
            if (NOAAmaxtempcomp2 < -999)
            {
                if (TempUnit == 0)
                    NOAAmaxtempcomp2 = 0;
                else
                    NOAAmaxtempcomp2 = 32;
            }
            NOAAmintempcomp1 = ini.GetValue("NOAA", "MinTempComp1", -1000.0);
            if (NOAAmintempcomp1 < -999)
            {
                if (TempUnit == 0)
                    NOAAmintempcomp1 = 0;
                else
                    NOAAmintempcomp1 = 32;
            }
            NOAAmintempcomp2 = ini.GetValue("NOAA", "MinTempComp2", -1000.0);
            if (NOAAmintempcomp2 < -999)
            {
                if (TempUnit == 0)
                    NOAAmintempcomp2 = -18;
                else
                    NOAAmintempcomp2 = 0;
            }
            NOAAraincomp1 = ini.GetValue("NOAA", "RainComp1", -1000.0);
            if (NOAAraincomp1 < -999)
            {
                if (RainUnit == 0)
                    NOAAraincomp1 = 0.2;
                else
                    NOAAraincomp1 = 0.01;
            }
            NOAAraincomp2 = ini.GetValue("NOAA", "RainComp2", -1000.0);
            if (NOAAraincomp2 < -999)
            {
                if (RainUnit == 0)
                    NOAAraincomp2 = 2;
                else
                    NOAAraincomp2 = 0.1;
            }
            NOAAraincomp3 = ini.GetValue("NOAA", "RainComp3", -1000.0);
            if (NOAAraincomp3 < -999)
            {
                if (RainUnit == 0)
                    NOAAraincomp3 = 20;
                else
                    NOAAraincomp3 = 1;
            }

            NOAAAutoSave = ini.GetValue("NOAA", "AutoSave", false);
            NOAAAutoFTP = ini.GetValue("NOAA", "AutoFTP", false);
            NOAAMonthFileFormat = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
            NOAAYearFileFormat = ini.GetValue("NOAA", "YearFileFormat", "'NOAAYR'yyyy'.txt'");
            NOAAFTPDirectory = ini.GetValue("NOAA", "FTPDirectory", "");
            NOAAUseUTF8 = ini.GetValue("NOAA", "NOAAUseUTF8", false);

            NOAATempNormJan = ini.GetValue("NOAA", "NOAATempNormJan", -1000.0);
            NOAATempNormFeb = ini.GetValue("NOAA", "NOAATempNormFeb", -1000.0);
            NOAATempNormMar = ini.GetValue("NOAA", "NOAATempNormMar", -1000.0);
            NOAATempNormApr = ini.GetValue("NOAA", "NOAATempNormApr", -1000.0);
            NOAATempNormMay = ini.GetValue("NOAA", "NOAATempNormMay", -1000.0);
            NOAATempNormJun = ini.GetValue("NOAA", "NOAATempNormJun", -1000.0);
            NOAATempNormJul = ini.GetValue("NOAA", "NOAATempNormJul", -1000.0);
            NOAATempNormAug = ini.GetValue("NOAA", "NOAATempNormAug", -1000.0);
            NOAATempNormSep = ini.GetValue("NOAA", "NOAATempNormSep", -1000.0);
            NOAATempNormOct = ini.GetValue("NOAA", "NOAATempNormOct", -1000.0);
            NOAATempNormNov = ini.GetValue("NOAA", "NOAATempNormNov", -1000.0);
            NOAATempNormDec = ini.GetValue("NOAA", "NOAATempNormDec", -1000.0);

            NOAARainNormJan = ini.GetValue("NOAA", "NOAARainNormJan", -1000.0);
            NOAARainNormFeb = ini.GetValue("NOAA", "NOAARainNormFeb", -1000.0);
            NOAARainNormMar = ini.GetValue("NOAA", "NOAARainNormMar", -1000.0);
            NOAARainNormApr = ini.GetValue("NOAA", "NOAARainNormApr", -1000.0);
            NOAARainNormMay = ini.GetValue("NOAA", "NOAARainNormMay", -1000.0);
            NOAARainNormJun = ini.GetValue("NOAA", "NOAARainNormJun", -1000.0);
            NOAARainNormJul = ini.GetValue("NOAA", "NOAARainNormJul", -1000.0);
            NOAARainNormAug = ini.GetValue("NOAA", "NOAARainNormAug", -1000.0);
            NOAARainNormSep = ini.GetValue("NOAA", "NOAARainNormSep", -1000.0);
            NOAARainNormOct = ini.GetValue("NOAA", "NOAARainNormOct", -1000.0);
            NOAARainNormNov = ini.GetValue("NOAA", "NOAARainNormNov", -1000.0);
            NOAARainNormDec = ini.GetValue("NOAA", "NOAARainNormDec", -1000.0);

            HTTPProxyName = ini.GetValue("Proxies", "HTTPProxyName", "");
            HTTPProxyPort = ini.GetValue("Proxies", "HTTPProxyPort", 0);
            HTTPProxyUser = ini.GetValue("Proxies", "HTTPProxyUser", "");
            HTTPProxyPassword = ini.GetValue("Proxies", "HTTPProxyPassword", "");

            NumWindRosePoints = ini.GetValue("Display", "NumWindRosePoints", 16);
            WindRoseAngle = 360.0 / NumWindRosePoints;

            // MySQL - common
            MySqlHost = ini.GetValue("MySQL", "Host", "127.0.0.1");
            MySqlPort = ini.GetValue("MySQL", "Port", 3306);
            MySqlUser = ini.GetValue("MySQL", "User", "");
            MySqlPass = ini.GetValue("MySQL", "Pass", "");
            MySqlDatabase = ini.GetValue("MySQL", "Database", "database");
            // MySQL - monthly log file
            MonthlyMySqlEnabled = ini.GetValue("MySQL", "MonthlyMySqlEnabled", false);
            MySqlMonthlyTable = ini.GetValue("MySQL", "MonthlyTable", "Monthly");
            // MySQL - realtimne
            RealtimeMySqlEnabled = ini.GetValue("MySQL", "RealtimeMySqlEnabled", false);
            MySqlRealtimeTable = ini.GetValue("MySQL", "RealtimeTable", "Realtime");
            MySqlRealtimeRetention = ini.GetValue("MySQL", "RealtimeRetention", "");
            // MySQL - dayfile
            DayfileMySqlEnabled = ini.GetValue("MySQL", "DayfileMySqlEnabled", false);
            MySqlDayfileTable = ini.GetValue("MySQL", "DayfileTable", "Dayfile");
            // MySQL - custom seconds
            CustomMySqlSecondsCommandString = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString", "");
            CustomMySqlSecondsEnabled = ini.GetValue("MySQL", "CustomMySqlSecondsEnabled", false);
            CustomMySqlSecondsInterval = ini.GetValue("MySQL", "CustomMySqlSecondsInterval", 10);
            if (CustomMySqlSecondsInterval < 1) { CustomMySqlSecondsInterval = 1; }
            // MySQL - custom minutes
            CustomMySqlMinutesCommandString = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString", "");
            CustomMySqlMinutesEnabled = ini.GetValue("MySQL", "CustomMySqlMinutesEnabled", false);
            CustomMySqlMinutesIntervalIndex = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIndex", -1);
            if (CustomMySqlMinutesIntervalIndex >= 0 && CustomMySqlMinutesIntervalIndex < FactorsOf60.Length)
            {
                CustomMySqlMinutesInterval = FactorsOf60[CustomMySqlMinutesIntervalIndex];
            }
            else
            {
                CustomMySqlMinutesInterval = 10;
                CustomMySqlMinutesIntervalIndex = 6;
            }
            // MySQL - custom rollover
            CustomMySqlRolloverCommandString = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString", "");
            CustomMySqlRolloverEnabled = ini.GetValue("MySQL", "CustomMySqlRolloverEnabled", false);

            // Custom HTTP - seconds
            CustomHttpSecondsString = ini.GetValue("HTTP", "CustomHttpSecondsString", "");
            CustomHttpSecondsEnabled = ini.GetValue("HTTP", "CustomHttpSecondsEnabled", false);
            CustomHttpSecondsInterval = ini.GetValue("HTTP", "CustomHttpSecondsInterval", 10);
            if (CustomHttpSecondsInterval < 1) { CustomHttpSecondsInterval = 1; }
            // Custom HTTP - minutes
            CustomHttpMinutesString = ini.GetValue("HTTP", "CustomHttpMinutesString", "");
            CustomHttpMinutesEnabled = ini.GetValue("HTTP", "CustomHttpMinutesEnabled", false);
            CustomHttpMinutesIntervalIndex = ini.GetValue("HTTP", "CustomHttpMinutesIntervalIndex", -1);
            if (CustomHttpMinutesIntervalIndex >= 0 && CustomHttpMinutesIntervalIndex < FactorsOf60.Length)
            {
                CustomHttpMinutesInterval = FactorsOf60[CustomHttpMinutesIntervalIndex];
            }
            else
            {
                CustomHttpMinutesInterval = 10;
                CustomHttpMinutesIntervalIndex = 6;
            }
            // Http - custom rollover
            CustomHttpRolloverString = ini.GetValue("HTTP", "CustomHttpRolloverString", "");
            CustomHttpRolloverEnabled = ini.GetValue("HTTP", "CustomHttpRolloverEnabled", false);
        }

        internal void WriteIniFile()
        {
            log.Debug("Writing Cumulus.ini file");

            ini.SetValue("Station", "Type", StationType);
            ini.SetValue("Station", "Model", StationModel);
            ini.SetValue("Station", "ComportName", ComportName);
            ini.SetValue("Station", "Latitude", Latitude);
            ini.SetValue("Station", "Longitude", Longitude);
            ini.SetValue("Station", "LatTxt", LatTxt);
            ini.SetValue("Station", "LonTxt", LonTxt);
            ini.SetValue("Station", "Altitude", Altitude);
            ini.SetValue("Station", "AltitudeInFeet", AltitudeInFeet);
            ini.SetValue("Station", "Humidity98Fix", Humidity98Fix);
            ini.SetValue("Station", "Wind10MinAverage", UseWind10MinAve);
            ini.SetValue("Station", "UseSpeedForAvgCalc", UseSpeedForAvgCalc);
            ini.SetValue("Station", "DavisReadReceptionStats", DavisReadReceptionStats);
            ini.SetValue("Station", "CalculatedDP", CalculatedDP);
            ini.SetValue("Station", "CalculatedWC", CalculatedWC);
            ini.SetValue("Station", "RolloverHour", RolloverHour);
            ini.SetValue("Station", "Use10amInSummer", Use10amInSummer);
            ini.SetValue("Station", "ConfirmClose", ConfirmClose);
            ini.SetValue("Station", "CloseOnSuspend", CloseOnSuspend);
            ini.SetValue("Station", "RestartIfUnplugged", RestartIfUnplugged);
            ini.SetValue("Station", "RestartIfDataStops", RestartIfDataStops);
            ini.SetValue("Station", "SyncDavisClock", SyncTime);
            ini.SetValue("Station", "ClockSettingHour", ClockSettingHour);
            ini.SetValue("Station", "SyncFOReads", SyncFOReads);
            ini.SetValue("Station", "WS2300IgnoreStationClock", WS2300IgnoreStationClock);
            ini.SetValue("Station", "LogExtraSensors", LogExtraSensors);
            ini.SetValue("Station", "DataLogInterval", DataLogInterval);
            ini.SetValue("Station", "WindUnit", WindUnit);
            ini.SetValue("Station", "PressureUnit", PressUnit);
            ini.SetValue("Station", "RainUnit", RainUnit);
            ini.SetValue("Station", "TempUnit", TempUnit);
            ini.SetValue("Station", "LocName", LocationName);
            ini.SetValue("Station", "LocDesc", LocationDesc);
            ini.SetValue("Station", "StartDate", RecordsBeganDate);
            ini.SetValue("Station", "YTDrain", YTDrain);
            ini.SetValue("Station", "YTDrainyear", YTDrainyear);
            ini.SetValue("Station", "EWInterval", EWInterval);
            ini.SetValue("Station", "EWFile", EWFile);
            ini.SetValue("Station", "UseDataLogger", UseDataLogger);
            ini.SetValue("Station", "UseCumulusForecast", UseCumulusForecast);
            ini.SetValue("Station", "HourlyForecast", HourlyForecast);
            ini.SetValue("Station", "UseCumulusPresstrendstr", UseCumulusPresstrendstr);
            ini.SetValue("Station", "FCpressinMB", FCpressinMB);
            ini.SetValue("Station", "FClowpress", FClowpress);
            ini.SetValue("Station", "FChighpress", FChighpress);
            ini.SetValue("Station", "ForceVPBarUpdate", ForceVPBarUpdate);
            ini.SetValue("Station", "UseZeroBearing", UseZeroBearing);
            ini.SetValue("Station", "VP2ConnectionType", VP2ConnectionType);
            ini.SetValue("Station", "VP2TCPPort", VP2TCPPort);
            ini.SetValue("Station", "VP2IPAddr", VP2IPAddr);
            ini.SetValue("Station", "WarnMultiple", WarnMultiple);
            ini.SetValue("Station", "RoundWindSpeed", RoundWindSpeed);
            ini.SetValue("Station", "VP2PeriodicDisconnectInterval", VP2PeriodicDisconnectInterval);
            ini.SetValue("Station", "EWtempdiff", EWtempdiff);
            ini.SetValue("Station", "EWpressurediff", EWpressurediff);
            ini.SetValue("Station", "EWhumiditydiff", EWhumiditydiff);
            ini.SetValue("Station", "EWgustdiff", EWgustdiff);
            ini.SetValue("Station", "EWwinddiff", EWwinddiff);
            ini.SetValue("Station", "EWmaxHourlyRain", EWmaxHourlyRain);
            ini.SetValue("Station", "EWmaxRainRate", EWmaxRainRate);

            ini.SetValue("Station", "EWminpressureMB", EWminpressureMB);
            ini.SetValue("Station", "EWmaxpressureMB", EWmaxpressureMB);

            ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
            ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

            ini.SetValue("Station", "ErrorLogSpikeRemoval", ErrorLogSpikeRemoval);

            //ini.SetValue("Station", "ImetBaudRate", ImetBaudRate);

            ini.SetValue("Station", "RG11portName", RG11Port);
            ini.SetValue("Station", "RG11TBRmode", RG11TBRmode);
            ini.SetValue("Station", "RG11tipsize", RG11tipsize);
            ini.SetValue("Station", "RG11IgnoreFirst", RG11IgnoreFirst);
            ini.SetValue("Station", "RG11DTRmode", RG11DTRmode);

            ini.SetValue("Station", "RG11portName2", RG11Port2);
            ini.SetValue("Station", "RG11TBRmode2", RG11TBRmode2);
            ini.SetValue("Station", "RG11tipsize2", RG11tipsize2);
            ini.SetValue("Station", "RG11IgnoreFirst2", RG11IgnoreFirst2);
            ini.SetValue("Station", "RG11DTRmode2", RG11DTRmode2);

            ini.SetValue("Web Site", "ForumURL", ForumURL);
            ini.SetValue("Web Site", "WebcamURL", WebcamURL);

            ini.SetValue("FTP site", "Host", ftp_host);
            ini.SetValue("FTP site", "Port", ftp_port);
            ini.SetValue("FTP site", "Username", ftp_user);
            ini.SetValue("FTP site", "Password", ftp_password);
            ini.SetValue("FTP site", "Directory", ftp_directory);

            ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);
            ini.SetValue("FTP site", "ActiveFTP", ActiveFTPMode);
            ini.SetValue("FTP site", "Sslftp", Sslftp);
            ini.SetValue("FTP site", "FTPlogging", FTPlogging);
            ini.SetValue("FTP site", "UTF8encode", UTF8encode);
            ini.SetValue("FTP site", "EnableRealtime", RealtimeEnabled);
            ini.SetValue("FTP site", "RealtimeFTPEnabled", RealtimeFTPEnabled);
            ini.SetValue("FTP site", "RealtimeTxtFTP", RealtimeTxtFTP);
            ini.SetValue("FTP site", "RealtimeGaugesTxtFTP", RealtimeGaugesTxtFTP);
            ini.SetValue("FTP site", "RealtimeInterval", RealtimeInterval);
            ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
            ini.SetValue("FTP site", "IncludeSTD", IncludeStandardFiles);
            ini.SetValue("FTP site", "IncludeGraphDataFiles", IncludeGraphDataFiles);
            //ini.SetValue("FTP site", "IncludeSTDImages", IncludeStandardImages);
            //ini.SetValue("FTP site", "IncludeSolarChart", IncludeSolarChart);
            //ini.SetValue("FTP site", "IncludeUVChart", IncludeUVChart);
            //ini.SetValue("FTP site", "IncludeSunshineChart", IncludeSunshineChart);
            ini.SetValue("FTP site", "FTPRename", FTPRename);
            ini.SetValue("FTP site", "DeleteBeforeUpload", DeleteBeforeUpload);
            //ini.SetValue("FTP site", "ResizeGraphs", ResizeGraphs);
            //ini.SetValue("FTP site", "GraphHeight", GraphHeight);
            //ini.SetValue("FTP site", "GraphWidth", GraphWidth);
            //ini.SetValue("FTP site", "ImageFolder", ImageFolder);
            //ini.SetValue("FTP site", "ImageCopyRealtime", ImageCopyRealtime);

            for (int i = 0; i < numextrafiles; i++)
            {
                ini.SetValue("FTP site", "ExtraLocal" + i, ExtraFiles[i].local);
                ini.SetValue("FTP site", "ExtraRemote" + i, ExtraFiles[i].remote);
                ini.SetValue("FTP site", "ExtraProcess" + i, ExtraFiles[i].process);
                ini.SetValue("FTP site", "ExtraBinary" + i, ExtraFiles[i].binary);
                ini.SetValue("FTP site", "ExtraRealtime" + i, ExtraFiles[i].realtime);
                ini.SetValue("FTP site", "ExtraFTP" + i, ExtraFiles[i].FTP);
                ini.SetValue("FTP site", "ExtraUTF" + i, ExtraFiles[i].UTF8);
            }

            ini.SetValue("FTP site", "ExternalProgram", ExternalProgram);
            ini.SetValue("FTP site", "RealtimeProgram", RealtimeProgram);
            ini.SetValue("FTP site", "DailyProgram", DailyProgram);
            ini.SetValue("FTP site", "ExternalParams", ExternalParams);
            ini.SetValue("FTP site", "RealtimeParams", RealtimeParams);
            ini.SetValue("FTP site", "DailyParams", DailyParams);


            ini.SetValue("Station", "CloudBaseInFeet", CloudBaseInFeet);

            ini.SetValue("Wunderground", "ID", WundID);
            ini.SetValue("Wunderground", "Password", WundPW);
            ini.SetValue("Wunderground", "Enabled", WundEnabled);
            ini.SetValue("Wunderground", "RapidFire", WundRapidFireEnabled);
            ini.SetValue("Wunderground", "Interval", WundInterval);
            ini.SetValue("Wunderground", "SendUV", SendUVToWund);
            ini.SetValue("Wunderground", "SendSR", SendSRToWund);
            ini.SetValue("Wunderground", "SendIndoor", SendIndoorToWund);
            ini.SetValue("Wunderground", "SendAverage", WundSendAverage);
            ini.SetValue("Wunderground", "CatchUp", WundCatchUp);

            ini.SetValue("Awekas", "User", AwekasUser);
            ini.SetValue("Awekas", "Password", AwekasPW);
            ini.SetValue("Awekas", "Enabled", AwekasEnabled);
            ini.SetValue("Awekas", "Interval", AwekasInterval);
            ini.SetValue("Awekas", "SendUV", SendUVToAwekas);
            ini.SetValue("Awekas", "SendSR", SendSolarToAwekas);
            ini.SetValue("Awekas", "SendSoilTemp", SendSoilTempToAwekas);

            ini.SetValue("WeatherCloud", "Wid", WCloudWid);
            ini.SetValue("WeatherCloud", "Key", WCloudKey);
            ini.SetValue("WeatherCloud", "Enabled", WCloudEnabled);
            //ini.SetValue("WeatherCloud", "Interval", AwekasInterval);
            ini.SetValue("WeatherCloud", "SendUV", SendUVToWCloud);
            ini.SetValue("WeatherCloud", "SendSR", SendSolarToWCloud);

            ini.SetValue("Twitter", "User", Twitteruser);
            ini.SetValue("Twitter", "Password", TwitterPW);
            ini.SetValue("Twitter", "Enabled", TwitterEnabled);
            ini.SetValue("Twitter", "Interval", TwitterInterval);
            ini.SetValue("Twitter", "OauthToken", TwitterOauthToken);
            ini.SetValue("Twitter", "OauthTokenSecret", TwitterOauthTokenSecret);
            ini.SetValue("Twitter", "TwitterSendLocation", TwitterSendLocation);

            ini.SetValue("PWSweather", "ID", PWSID);
            ini.SetValue("PWSweather", "Password", PWSPW);
            ini.SetValue("PWSweather", "Enabled", PWSEnabled);
            ini.SetValue("PWSweather", "Interval", PWSInterval);
            ini.SetValue("PWSweather", "SendUV", SendUVToPWS);
            ini.SetValue("PWSweather", "SendSR", SendSRToPWS);
            ini.SetValue("PWSweather", "CatchUp", PWSCatchUp);

            ini.SetValue("WOW", "ID", WOWID);
            ini.SetValue("WOW", "Password", WOWPW);
            ini.SetValue("WOW", "Enabled", WOWEnabled);
            ini.SetValue("WOW", "Interval", WOWInterval);
            ini.SetValue("WOW", "SendUV", SendUVToWOW);
            ini.SetValue("WOW", "SendSR", SendSRToWOW);
            ini.SetValue("WOW", "CatchUp", WOWCatchUp);

            ini.SetValue("Weatherbug", "ID", WeatherbugID);
            ini.SetValue("Weatherbug", "Number", WeatherbugNumber);
            ini.SetValue("Weatherbug", "Password", WeatherbugPW);
            ini.SetValue("Weatherbug", "Enabled", WeatherbugEnabled);
            ini.SetValue("Weatherbug", "Interval", WeatherbugInterval);
            ini.SetValue("Weatherbug", "SendUV", SendUVToWeatherbug);
            ini.SetValue("Weatherbug", "SendSR", SendSRToWeatherbug);
            ini.SetValue("Weatherbug", "CatchUp", WeatherbugCatchUp);

            ini.SetValue("APRS", "ID", APRSID);
            ini.SetValue("APRS", "pass", APRSpass);
            ini.SetValue("APRS", "server", APRSserver);
            ini.SetValue("APRS", "port", APRSport);
            ini.SetValue("APRS", "Enabled", APRSenabled);
            ini.SetValue("APRS", "Interval", APRSinterval);
            ini.SetValue("APRS", "SendSR", SendSRToAPRS);


            //ini.SetValue("Alarms", "alarmlowtemp", alarmlowtemp);
            //ini.SetValue("Alarms", "LowTempAlarmSet", LowTempAlarmSet);
            //ini.SetValue("Alarms", "LowTempAlarmSound", LowTempAlarmSound);
            //ini.SetValue("Alarms", "LowTempAlarmSoundFile", LowTempAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhightemp", alarmhightemp);
            //ini.SetValue("Alarms", "HighTempAlarmSet", HighTempAlarmSet);
            //ini.SetValue("Alarms", "HighTempAlarmSound", HighTempAlarmSound);
            //ini.SetValue("Alarms", "HighTempAlarmSoundFile", HighTempAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmtempchange", alarmtempchange);
            //ini.SetValue("Alarms", "TempChangeAlarmSet", TempChangeAlarmSet);
            //ini.SetValue("Alarms", "TempChangeAlarmSound", TempChangeAlarmSound);
            //ini.SetValue("Alarms", "TempChangeAlarmSoundFile", TempChangeAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmlowpress", alarmlowpress);
            //ini.SetValue("Alarms", "LowPressAlarmSet", LowPressAlarmSet);
            //ini.SetValue("Alarms", "LowPressAlarmSound", LowPressAlarmSound);
            //ini.SetValue("Alarms", "LowPressAlarmSoundFile", LowPressAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighpress", alarmhighpress);
            //ini.SetValue("Alarms", "HighPressAlarmSet", HighPressAlarmSet);
            //ini.SetValue("Alarms", "HighPressAlarmSound", HighPressAlarmSound);
            //ini.SetValue("Alarms", "HighPressAlarmSoundFile", HighPressAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmpresschange", alarmpresschange);
            //ini.SetValue("Alarms", "PressChangeAlarmSet", PressChangeAlarmSet);
            //ini.SetValue("Alarms", "PressChangeAlarmSound", PressChangeAlarmSound);
            //ini.SetValue("Alarms", "PressChangeAlarmSoundFile", PressChangeAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighraintoday", alarmhighraintoday);
            //ini.SetValue("Alarms", "HighRainTodayAlarmSet", HighRainTodayAlarmSet);
            //ini.SetValue("Alarms", "HighRainTodayAlarmSound", HighRainTodayAlarmSound);
            //ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", HighRainTodayAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighrainrate", alarmhighrainrate);
            //ini.SetValue("Alarms", "HighRainRateAlarmSet", HighRainRateAlarmSet);
            //ini.SetValue("Alarms", "HighRainRateAlarmSound", HighRainRateAlarmSound);
            //ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", HighRainRateAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighgust", alarmhighgust);
            //ini.SetValue("Alarms", "HighGustAlarmSet", HighGustAlarmSet);
            //ini.SetValue("Alarms", "HighGustAlarmSound", HighGustAlarmSound);
            //ini.SetValue("Alarms", "HighGustAlarmSoundFile", HighGustAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighwind", alarmhighwind);
            //ini.SetValue("Alarms", "HighWindAlarmSet", HighWindAlarmSet);
            //ini.SetValue("Alarms", "HighWindAlarmSound", HighWindAlarmSound);
            //ini.SetValue("Alarms", "HighWindAlarmSoundFile", HighWindAlarmSoundFile);

            //ini.SetValue("Alarms", "SensorAlarmSet", SensorAlarmSet);
            //ini.SetValue("Alarms", "SensorAlarmSound", SensorAlarmSound);
            //ini.SetValue("Alarms", "SensorAlarmSoundFile", SensorAlarmSoundFile);

            ini.SetValue("Offsets", "PressOffset", PressOffset);
            ini.SetValue("Offsets", "TempOffset", TempOffset);
            ini.SetValue("Offsets", "HumOffset", HumOffset);
            ini.SetValue("Offsets", "WindDirOffset", WindDirOffset);
            ini.SetValue("Offsets", "InTempOffset", InTempoffset);
            ini.SetValue("Offsets", "UVOffset", UVOffset);
            ini.SetValue("Offsets", "WetBulbOffset", WetBulbOffset);
            //ini.SetValue("Offsets", "DavisCalcAltPressOffset", DavisCalcAltPressOffset);

            ini.SetValue("Offsets", "WindSpeedMult", WindSpeedMult);
            ini.SetValue("Offsets", "WindGustMult", WindGustMult);
            ini.SetValue("Offsets", "TempMult", TempMult);
            ini.SetValue("Offsets", "HumMult", HumMult);
            ini.SetValue("Offsets", "RainMult", RainMult);
            ini.SetValue("Offsets", "UVMult", UVMult);
            ini.SetValue("Offsets", "WetBulbMult", WetBulbMult);

            ini.SetValue("xAP", "Enabled", xapEnabled);
            ini.SetValue("xAP", "UID", xapUID);
            ini.SetValue("xAP", "Port", xapPort);

            ini.SetValue("Solar", "SunThreshold", SunThreshold);
            ini.SetValue("Solar", "RStransfactor", RStransfactor);
            ini.SetValue("Solar", "SolarMinimum", SolarMinimum);
            ini.SetValue("Solar", "UseBlakeLarsen", UseBlakeLarsen);
            ini.SetValue("Solar", "SolarCalc", SolarCalc);
            ini.SetValue("Solar", "BrasTurbidity", BrasTurbidity);


            ini.SetValue("NOAA", "Name", NOAAname);
            ini.SetValue("NOAA", "City", NOAAcity);
            ini.SetValue("NOAA", "State", NOAAstate);
            ini.SetValue("NOAA", "12hourformat", NOAA12hourformat);
            ini.SetValue("NOAA", "HeatingThreshold", NOAAheatingthreshold);
            ini.SetValue("NOAA", "CoolingThreshold", NOAAcoolingthreshold);
            ini.SetValue("NOAA", "MaxTempComp1", NOAAmaxtempcomp1);
            ini.SetValue("NOAA", "MaxTempComp2", NOAAmaxtempcomp2);
            ini.SetValue("NOAA", "MinTempComp1", NOAAmintempcomp1);
            ini.SetValue("NOAA", "MinTempComp2", NOAAmintempcomp2);
            ini.SetValue("NOAA", "RainComp1", NOAAraincomp1);
            ini.SetValue("NOAA", "RainComp2", NOAAraincomp2);
            ini.SetValue("NOAA", "RainComp3", NOAAraincomp3);
            ini.SetValue("NOAA", "AutoSave", NOAAAutoSave);
            ini.SetValue("NOAA", "AutoFTP", NOAAAutoFTP);
            ini.SetValue("NOAA", "MonthFileFormat", NOAAMonthFileFormat);
            ini.SetValue("NOAA", "YearFileFormat", NOAAYearFileFormat);
            ini.SetValue("NOAA", "FTPDirectory", NOAAFTPDirectory);
            ini.SetValue("NOAA", "NOAAUseUTF8", NOAAUseUTF8);

            ini.SetValue("NOAA", "NOAATempNormJan", NOAATempNormJan);
            ini.SetValue("NOAA", "NOAATempNormFeb", NOAATempNormFeb);
            ini.SetValue("NOAA", "NOAATempNormMar", NOAATempNormMar);
            ini.SetValue("NOAA", "NOAATempNormApr", NOAATempNormApr);
            ini.SetValue("NOAA", "NOAATempNormMay", NOAATempNormMay);
            ini.SetValue("NOAA", "NOAATempNormJun", NOAATempNormJun);
            ini.SetValue("NOAA", "NOAATempNormJul", NOAATempNormJul);
            ini.SetValue("NOAA", "NOAATempNormAug", NOAATempNormAug);
            ini.SetValue("NOAA", "NOAATempNormSep", NOAATempNormSep);
            ini.SetValue("NOAA", "NOAATempNormOct", NOAATempNormOct);
            ini.SetValue("NOAA", "NOAATempNormNov", NOAATempNormNov);
            ini.SetValue("NOAA", "NOAATempNormDec", NOAATempNormDec);

            ini.SetValue("NOAA", "NOAARainNormJan", NOAARainNormJan);
            ini.SetValue("NOAA", "NOAARainNormFeb", NOAARainNormFeb);
            ini.SetValue("NOAA", "NOAARainNormMar", NOAARainNormMar);
            ini.SetValue("NOAA", "NOAARainNormApr", NOAARainNormApr);
            ini.SetValue("NOAA", "NOAARainNormMay", NOAARainNormMay);
            ini.SetValue("NOAA", "NOAARainNormJun", NOAARainNormJun);
            ini.SetValue("NOAA", "NOAARainNormJul", NOAARainNormJul);
            ini.SetValue("NOAA", "NOAARainNormAug", NOAARainNormAug);
            ini.SetValue("NOAA", "NOAARainNormSep", NOAARainNormSep);
            ini.SetValue("NOAA", "NOAARainNormOct", NOAARainNormOct);
            ini.SetValue("NOAA", "NOAARainNormNov", NOAARainNormNov);
            ini.SetValue("NOAA", "NOAARainNormDec", NOAARainNormDec);

            ini.SetValue("Proxies", "HTTPProxyName", HTTPProxyName);
            ini.SetValue("Proxies", "HTTPProxyPort", HTTPProxyPort);
            ini.SetValue("Proxies", "HTTPProxyUser", HTTPProxyUser);
            ini.SetValue("Proxies", "HTTPProxyPassword", HTTPProxyPassword);

            ini.SetValue("Display", "NumWindRosePoints", NumWindRosePoints);

            ini.SetValue("Graphs", "ChartMaxDays", GraphDays);
            ini.SetValue("Graphs", "GraphHours", GraphHours);

            ini.SetValue("MySQL", "Host", MySqlHost);
            ini.SetValue("MySQL", "Port", MySqlPort);
            ini.SetValue("MySQL", "User", MySqlUser);
            ini.SetValue("MySQL", "Pass", MySqlPass);
            ini.SetValue("MySQL", "Database", MySqlDatabase);
            ini.SetValue("MySQL", "MonthlyMySqlEnabled", MonthlyMySqlEnabled);
            ini.SetValue("MySQL", "RealtimeMySqlEnabled", RealtimeMySqlEnabled);
            ini.SetValue("MySQL", "DayfileMySqlEnabled", DayfileMySqlEnabled);
            ini.SetValue("MySQL", "MonthlyTable", MySqlMonthlyTable);
            ini.SetValue("MySQL", "DayfileTable", MySqlDayfileTable);
            ini.SetValue("MySQL", "RealtimeTable", MySqlRealtimeTable);
            ini.SetValue("MySQL", "RealtimeRetention", MySqlRealtimeRetention);
            ini.SetValue("MySQL", "CustomMySqlSecondsCommandString", CustomMySqlSecondsCommandString);
            ini.SetValue("MySQL", "CustomMySqlMinutesCommandString", CustomMySqlMinutesCommandString);
            ini.SetValue("MySQL", "CustomMySqlRolloverCommandString", CustomMySqlRolloverCommandString);

            ini.SetValue("MySQL", "CustomMySqlSecondsEnabled", CustomMySqlSecondsEnabled);
            ini.SetValue("MySQL", "CustomMySqlMinutesEnabled", CustomMySqlMinutesEnabled);
            ini.SetValue("MySQL", "CustomMySqlRolloverEnabled", CustomMySqlRolloverEnabled);

            ini.SetValue("MySQL", "CustomMySqlSecondsInterval", CustomMySqlSecondsInterval);
            ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIndex", CustomMySqlMinutesIntervalIndex);

            ini.SetValue("HTTP", "CustomHttpSecondsString", CustomHttpSecondsString);
            ini.SetValue("HTTP", "CustomHttpMinutesString", CustomHttpMinutesString);
            ini.SetValue("HTTP", "CustomHttpRolloverString", CustomHttpRolloverString);

            ini.SetValue("HTTP", "CustomHttpSecondsEnabled", CustomHttpSecondsEnabled);
            ini.SetValue("HTTP", "CustomHttpMinutesEnabled", CustomHttpMinutesEnabled);
            ini.SetValue("HTTP", "CustomHttpRolloverEnabled", CustomHttpRolloverEnabled);

            ini.SetValue("HTTP", "CustomHttpSecondsInterval", CustomHttpSecondsInterval);
            ini.SetValue("HTTP", "CustomHttpMinutesIntervalIndex", CustomHttpMinutesIntervalIndex);

            ini.Flush();
        }

    }
}
