using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MySqlConnector;
using FluentFTP;
using LinqToTwitter;
using ServiceStack.Text;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;
using Timer = System.Timers.Timer;
using SQLite;
using Renci.SshNet;
using FluentFTP.Helpers;

namespace CumulusMX
{
	public class Cumulus
	{
		/////////////////////////////////
		/// Now derived from app properties
		public string Version;
		public string Build;
		/////////////////////////////////



		public static SemaphoreSlim syncInit = new SemaphoreSlim(1);

		/*
		public enum VPRainGaugeTypes
		{
			MM = 0,
			IN = 1
		}
		*/

		/*
		public enum VPConnTypes
		{
			Serial = 0,
			TCPIP = 1
		}
		*/

		public enum PressUnits
		{
			MB,
			HPA,
			IN
		}

		public enum WindUnits
		{
			MS,
			MPH,
			KPH,
			KNOTS
		}

		public enum TempUnits
		{
			C,
			F
		}

		public enum RainUnits
		{
			MM,
			IN
		}

		/*
		public enum SolarCalcTypes
		{
			RyanStolzenbach = 0,
			Bras = 1
		}
		*/

		public enum FtpProtocols
		{
			FTP = 0,
			FTPS = 1,
			SFTP = 2
		}

		public enum PrimaryAqSensor
		{
			Undefined = -1,
			AirLinkOutdoor = 0,
			Ecowitt1 = 1,
			Ecowitt2 = 2,
			Ecowitt3 = 3,
			Ecowitt4 = 4,
			AirLinkIndoor = 5,
			EcowittCO2 = 6
		}

		private readonly string[] sshAuthenticationVals = { "password", "psk", "password_psk" };

		/*
		public struct Dataunits
		{
			public Units.Presss Units.Press;
			public Units.Winds Units.Wind;
			public tempunits tempunit;
			public rainunits rainunit;
		}
		*/

		/*
		public struct CurrentData
		{
			public double OutdoorTemperature;
			public double AvgTempToday;
			public double IndoorTemperature;
			public double OutdoorDewpoint;
			public double WindChill;
			public int IndoorHumidity;
			public int OutdoorHumidity;
			public double Pressure;
			public double WindLatest;
			public double WindAverage;
			public double Recentmaxgust;
			public double WindRunToday;
			public int Bearing;
			public int Avgbearing;
			public double RainToday;
			public double RainYesterday;
			public double RainMonth;
			public double RainYear;
			public double RainRate;
			public double RainLastHour;
			public double HeatIndex;
			public double Humidex;
			public double AppTemp;
			public double FeelsLike;
			public double TempTrend;
			public double PressTrend;
		}
		*/

		/*
		public struct HighLowData
		{
			public double TodayLow;
			public DateTime TodayLowDT;
			public double TodayHigh;
			public DateTime TodayHighDT;
			public double YesterdayLow;
			public DateTime YesterdayLowDT;
			public double YesterdayHigh;
			public DateTime YesterdayHighDT;
			public double MonthLow;
			public DateTime MonthLowDT;
			public double MonthHigh;
			public DateTime MonthHighDT;
			public double YearLow;
			public DateTime YearLowDT;
			public double YearHigh;
			public DateTime YearHighDT;
		}
		*/

		public struct TExtraFiles
		{
			public string local;
			public string remote;
			public bool process;
			public bool binary;
			public bool realtime;
			public bool endofday;
			public bool FTP;
			public bool UTF8;
		}

		//public Dataunits Units;

		public const int DayfileFields = 52;

		private readonly WeatherStation station;

		internal DavisAirLink airLinkIn;
		public int airLinkInLsid;
		public string AirLinkInHostName;
		internal DavisAirLink airLinkOut;
		public int airLinkOutLsid;
		public string AirLinkOutHostName;

		public DateTime LastUpdateTime;

		public PerformanceCounter UpTime;

		private readonly WebTags webtags;
		private readonly TokenParser tokenParser;
		private readonly TokenParser realtimeTokenParser;
		private readonly TokenParser customMysqlSecondsTokenParser = new TokenParser();
		private readonly TokenParser customMysqlMinutesTokenParser = new TokenParser();
		private readonly TokenParser customMysqlRolloverTokenParser = new TokenParser();

		public string CurrentActivity = "Stopped";

		private static readonly TraceListener FtpTraceListener = new TextWriterTraceListener("ftplog.txt", "ftplog");


		public string AirQualityUnitText = "µg/m³";
		public string SoilMoistureUnitText = "cb";
		public string CO2UnitText = "ppm";

		public volatile int WebUpdating;

		public double WindRoseAngle { get; set; }

		public int NumWindRosePoints { get; set; }

		//public int[] WUnitFact = new[] { 1000, 2237, 3600, 1944 };
		//public int[] TUnitFact = new[] { 1000, 1800 };
		//public int[] TUnitAdd = new[] { 0, 32 };
		//public int[] PUnitFact = new[] { 1000, 1000, 2953 };
		//public int[] PressFact = new[] { 1, 1, 100 };
		//public int[] RUnitFact = new[] { 1000, 39 };

		public int[] logints = new[] { 1, 5, 10, 15, 20, 30 };

		//public int UnitMult = 1000;

		public int GraphDays = 31;

		public string Newmoon = "New Moon",
			WaxingCrescent = "Waxing Crescent",
			FirstQuarter = "First Quarter",
			WaxingGibbous = "Waxing Gibbous",
			Fullmoon = "Full Moon",
			WaningGibbous = "Waning Gibbous",
			LastQuarter = "Last Quarter",
			WaningCrescent = "Waning Crescent";

		public string Calm = "Calm",
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

		public string Risingveryrapidly = "Rising very rapidly",
			Risingquickly = "Rising quickly",
			Rising = "Rising",
			Risingslowly = "Rising slowly",
			Steady = "Steady",
			Fallingslowly = "Falling slowly",
			Falling = "Falling",
			Fallingquickly = "Falling quickly",
			Fallingveryrapidly = "Falling very rapidly";

		public string[] compassp = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

		public string[] zForecast =
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

		public int[,] DavisForecastLookup =
		{
			{14, 0, 0}, {13, 0, 0}, {12, 0, 0}, {11, 0, 0}, {13, 0, 0}, {25, 0, 0}, {24, 0, 0}, {24, 0, 0}, {10, 0, 0}, {24, 0, 0}, {24, 0, 0},
			{13, 0, 0}, {7, 2, 0}, {24, 0, 0}, {13, 0, 0}, {6, 3, 0}, {13, 0, 0}, {24, 0, 0}, {13, 0, 0}, {6, 6, 0}, {13, 0, 0}, {24, 0, 0},
			{13, 0, 0}, {7, 3, 0}, {10, 0, 6}, {24, 0, 0}, {13, 0, 0}, {7, 6, 6}, {10, 0, 6}, {7, 0, 0}, {24, 0, 0}, {13, 0, 0}, {7, 6, 6},
			{10, 0, 6}, {7, 0, 0}, {24, 0, 0}, {13, 0, 0}, {7, 6, 6}, {24, 0, 0}, {13, 0, 0}, {10, 1, 0}, {10, 0, 0}, {24, 0, 0}, {13, 0, 0},
			{6, 2, 0}, {6, 0, 0}, {24, 0, 0}, {13, 0, 0}, {7, 4, 0}, {24, 0, 0}, {13, 0, 0}, {7, 4, 5}, {24, 0, 0}, {13, 0, 0}, {7, 4, 5},
			{24, 0, 0}, {13, 0, 0}, {7, 7, 0}, {24, 0, 0}, {13, 0, 0}, {7, 7, 5}, {24, 0, 0}, {13, 0, 0}, {7, 4, 5}, {24, 0, 0}, {13, 0, 0},
			{7, 6, 0}, {24, 0, 0}, {13, 0, 0}, {7, 16, 0}, {4, 14, 0}, {24, 0, 0}, {4, 14, 0}, {13, 0, 0}, {4, 14, 0}, {25, 0, 0}, {24, 0, 0},
			{14, 0, 0}, {4, 14, 0}, {13, 0, 0}, {4, 14, 0}, {14, 0, 0}, {24, 0, 0}, {13, 0, 0}, {6, 3, 0}, {2, 18, 0}, {24, 0, 0}, {13, 0, 0},
			{2, 16, 0}, {1, 18, 0}, {1, 16, 0}, {24, 0, 0}, {13, 0, 0}, {5, 9, 0}, {6, 9, 0}, {2, 18, 6}, {24, 0, 0}, {13, 0, 0}, {2, 16, 6},
			{1, 18, 6}, {1, 16, 6}, {24, 0, 0}, {13, 0, 0}, {5, 4, 4}, {6, 4, 4}, {24, 0, 0}, {13, 0, 0}, {5, 10, 4}, {6, 10, 4}, {2, 13, 4},
			{2, 0, 4}, {1, 13, 4}, {1, 0, 4}, {2, 13, 4}, {24, 0, 0}, {13, 0, 0}, {2, 3, 4}, {1, 13, 4}, {1, 3, 4}, {3, 14, 0}, {3, 0, 0},
			{2, 14, 3}, {2, 0, 3}, {3, 0, 0}, {24, 0, 0}, {13, 0, 0}, {1, 6, 5}, {24, 0, 0}, {13, 0, 0}, {5, 5, 5}, {2, 14, 5}, {24, 0, 0},
			{13, 0, 0}, {2, 6, 5}, {2, 11, 0}, {2, 0, 0}, {2, 17, 5}, {24, 0, 0}, {13, 0, 0}, {2, 7, 5}, {1, 17, 5}, {24, 0, 0}, {13, 0, 0},
			{1, 7, 5}, {24, 0, 0}, {13, 0, 0}, {6, 5, 5}, {2, 0, 5}, {2, 17, 5}, {24, 0, 0}, {13, 0, 0}, {2, 15, 5}, {1, 17, 5}, {1, 15, 5},
			{24, 0, 0}, {13, 0, 0}, {5, 10, 5}, {6, 10, 5}, {5, 18, 3}, {24, 0, 0}, {13, 0, 0}, {2, 16, 3}, {1, 18, 3}, {1, 16, 3}, {5, 10, 3},
			{24, 0, 0}, {13, 0, 0}, {5, 10, 4}, {6, 10, 3}, {6, 10, 4}, {24, 0, 0}, {13, 0, 0}, {5, 10, 3}, {6, 10, 3}, {24, 0, 0}, {13, 0, 0},
			{5, 4, 3}, {6, 4, 3}, {2, 12, 3}, {24, 0, 0}, {13, 0, 0}, {2, 8, 3}, {1, 13, 3}, {1, 8, 3}, {2, 18, 0}, {24, 0, 0}, {13, 0, 0},
			{2, 16, 3}, {1, 18, 0}, {1, 16, 0}, {24, 0, 0}, {13, 0, 0}, {2, 5, 5}, {0, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}, {26, 0, 0}
		};

		// equivalents of Zambretti "dial window" letters A - Z
		public int[] riseOptions = { 25, 25, 25, 24, 24, 19, 16, 12, 11, 9, 8, 6, 5, 2, 1, 1, 0, 0, 0, 0, 0, 0 };
		public int[] steadyOptions = { 25, 25, 25, 25, 25, 25, 23, 23, 22, 18, 15, 13, 10, 4, 1, 1, 0, 0, 0, 0, 0, 0 };
		public int[] fallOptions = { 25, 25, 25, 25, 25, 25, 25, 25, 23, 23, 21, 20, 17, 14, 7, 3, 1, 1, 1, 0, 0, 0 };

		internal int[] FactorsOf60 = { 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60 };

		public TimeSpan AvgSpeedTime { get; set; }

		public TimeSpan PeakGustTime { get; set; }
		public TimeSpan AvgBearingTime { get; set; }

		public bool UTF8encode { get; set; }

		internal int TempDPlaces = 1;
		public string TempFormat;

		internal int WindDPlaces = 1;
		internal int WindAvgDPlaces = 1;
		public string WindFormat;
		public string WindAvgFormat;

		internal int HumDPlaces = 0;
		public string HumFormat;

		internal int AirQualityDPlaces = 1;
		public string AirQualityFormat;

		public int WindRunDPlaces = 1;
		public string WindRunFormat;

		public int RainDPlaces = 1;
		public string RainFormat;

		internal int PressDPlaces = 1;
		public string PressFormat;

		internal int SunshineDPlaces = 1;
		public string SunFormat;

		internal int UVDPlaces = 1;
		public string UVFormat;

		public string ETFormat;

		public string ComportName;
		public string DefaultComportName;

		//public string IPaddress;

		//public int TCPport;

		//public VPConnTypes VPconntype;

		public string Platform;

		public string dbfile;
		public SQLiteConnection LogDB;

		public string diaryfile;
		public SQLiteConnection DiaryDB;

		public string Datapath;

		public string ListSeparator;
		public char DirectorySeparator;

		public int RolloverHour;
		public bool Use10amInSummer;

		public double Latitude;
		public double Longitude;
		public double Altitude;

		public double RStransfactor = 0.8;

		internal int wsPort;
		private readonly bool DebuggingEnabled;

		public SerialPort cmprtRG11;
		public SerialPort cmprt2RG11;

		private const int DefaultWebUpdateInterval = 15;

		public int RecordSetTimeoutHrs = 24;

		private const int VP2SERIALCONNECTION = 0;
		//private const int VP2USBCONNECTION = 1;
		//private const int VP2TCPIPCONNECTION = 2;

		private readonly string twitterKey = "lQiGNdtlYUJ4wS3d7souPw";
		private readonly string twitterSecret = "AoB7OqimfoaSfGQAd47Hgatqdv3YeTTiqpinkje6Xg";

		public string AlltimeIniFile;
		public string Alltimelogfile;
		public string MonthlyAlltimeIniFile;
		public string MonthlyAlltimeLogFile;
		private readonly string logFilePath;
		public string DayFileName;
		public string YesterdayFile;
		public string TodayIniFile;
		public string MonthIniFile;
		public string YearIniFile;
		//private readonly string stringsFile;
		private readonly string backupPath;
		//private readonly string ExternaldataFile;
		public string WebTagFile;

		public bool SynchronisedWebUpdate;

		private List<string> WundList = new List<string>();
		private List<string> WindyList = new List<string>();
		private List<string> PWSList = new List<string>();
		private List<string> WOWList = new List<string>();
		private List<string> OWMList = new List<string>();

		private List<string> MySqlList = new List<string>();

		// Calibration settings
		/// <summary>
		/// User value calibration settings
		/// </summary>
		public Calibrations Calib = new Calibrations();

		/// <summary>
		/// User extreme limit settings
		/// </summary>
		public Limits Limit = new Limits();

		/// <summary>
		/// User spike limit settings
		/// </summary>
		public Spikes Spike = new Spikes();

		public ProgramOptionsClass ProgramOptions = new ProgramOptionsClass();

		public StationOptions StationOptions = new StationOptions();

		public StationUnits Units = new StationUnits();

		public DavisOptions DavisOptions = new DavisOptions();
		public FineOffsetOptions FineOffsetOptions = new FineOffsetOptions();
		public ImetOptions ImetOptions = new ImetOptions();
		public EasyWeatherOptions EwOptions = new EasyWeatherOptions();

		public GraphOptions GraphOptions = new GraphOptions();

		public SelectaChartOptions SelectaChartOptions = new SelectaChartOptions();

		public DisplayOptions DisplayOptions = new DisplayOptions();

		public EmailSender emailer;
		public EmailSender.SmtpOptions SmtpOptions = new EmailSender.SmtpOptions();

		public string AlarmEmailPreamble;
		public string AlarmEmailSubject;
		public string AlarmFromEmail;
		public string[] AlarmDestEmail;
		public bool AlarmEmailHtml;

		public bool ListWebTags;

		public bool RealtimeEnabled; // The timer is to be started
		public bool RealtimeFTPEnabled; // The FTP connection is to be established
		private int realtimeFTPRetries; // Count of failed realtime FTP attempts

		// Twitter settings
		public WebUploadTwitter Twitter = new WebUploadTwitter();

		// Wunderground settings
		public WebUploadWund Wund = new WebUploadWund();

		// Windy.com settings
		public  WebUploadWindy Windy = new WebUploadWindy();

		// Wind Guru settings
		public WebUploadWindGuru WindGuru = new WebUploadWindGuru();

		// PWS Weather settings
		public WebUploadService PWS = new WebUploadService();

		// WOW settings
		public WebUploadService WOW = new WebUploadService();

		// APRS settings
		public WebUploadAprs APRS = new WebUploadAprs();

		// Awekas settings
		public WebUploadAwekas AWEKAS = new WebUploadAwekas();

		// WeatherCloud settings
		public WebUploadWCloud WCloud = new WebUploadWCloud();

		// OpenWeatherMap settings
		public WebUploadService OpenWeatherMap = new WebUploadService();


		// MQTT settings
		public struct MqttSettings
		{
			public string Server;
			public int Port;
			public int IpVersion;
			public bool UseTLS;
			public string Username;
			public string Password;
			public bool EnableDataUpdate;
			public string UpdateTopic;
			public string UpdateTemplate;
			public bool UpdateRetained;
			public bool EnableInterval;
			public int IntervalTime;
			public string IntervalTopic;
			public string IntervalTemplate;
			public bool IntervalRetained;
		}
		public MqttSettings MQTT;

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
		public bool NOAANeedFTP;
		public string NOAAMonthFileFormat;
		public string NOAAYearFileFormat;
		public string NOAAFTPDirectory;
		public string NOAALatestMonthlyReport;
		public string NOAALatestYearlyReport;
		public bool NOAAUseUTF8;
		public bool NOAAUseDotDecimal;

		public double[] NOAATempNorms = new double[13];

		public double[] NOAARainNorms = new double[13];

		// Growing Degree Days
		public double GrowingBase1;
		public double GrowingBase2;
		public int GrowingYearStarts;
		public bool GrowingCap30C;

		public int TempSumYearStarts;
		public double TempSumBase1;
		public double TempSumBase2;

		public bool EODfilesNeedFTP;

		public bool IsOSX;
		public double CPUtemp = -999;

		// Alarms
		public Alarm DataStoppedAlarm = new Alarm();
		public Alarm BatteryLowAlarm = new Alarm();
		public Alarm SensorAlarm = new Alarm();
		public Alarm SpikeAlarm = new Alarm();
		public Alarm HighWindAlarm = new Alarm();
		public Alarm HighGustAlarm = new Alarm();
		public Alarm HighRainRateAlarm = new Alarm();
		public Alarm HighRainTodayAlarm = new Alarm();
		public AlarmChange PressChangeAlarm = new AlarmChange();
		public Alarm HighPressAlarm = new Alarm();
		public Alarm LowPressAlarm = new Alarm();
		public AlarmChange TempChangeAlarm = new AlarmChange();
		public Alarm HighTempAlarm = new Alarm();
		public Alarm LowTempAlarm = new Alarm();
		public Alarm UpgradeAlarm = new Alarm();


		private const double DEFAULTFCLOWPRESS = 950.0;
		private const double DEFAULTFCHIGHPRESS = 1050.0;

		private const string ForumDefault = "https://cumulus.hosiene.co.uk/";

		private const string WebcamDefault = "";

		private const string DefaultSoundFile = "alarm.mp3";
		private const string DefaultSoundFileOld = "alert.wav";

		public int RealtimeInterval;

		public string ForecastNotAvailable = "Not available";

		public WebServer httpServer;
		//public WebSocket websock;

		//private Thread httpThread;

		private static readonly HttpClientHandler WUhttpHandler = new HttpClientHandler();
		private readonly HttpClient WUhttpClient = new HttpClient(WUhttpHandler);

		private static readonly HttpClientHandler WindyhttpHandler = new HttpClientHandler();
		private readonly HttpClient WindyhttpClient = new HttpClient(WindyhttpHandler);

		private static readonly HttpClientHandler WindGuruhttpHandler = new HttpClientHandler();
		private readonly HttpClient WindGuruhttpClient = new HttpClient(WindGuruhttpHandler);

		private static readonly HttpClientHandler AwekashttpHandler = new HttpClientHandler();
		private readonly HttpClient AwekashttpClient = new HttpClient(AwekashttpHandler);

		private static readonly HttpClientHandler WCloudhttpHandler = new HttpClientHandler();
		private readonly HttpClient WCloudhttpClient = new HttpClient(WCloudhttpHandler);

		private static readonly HttpClientHandler PWShttpHandler = new HttpClientHandler();
		private readonly HttpClient PWShttpClient = new HttpClient(PWShttpHandler);

		private static readonly HttpClientHandler WOWhttpHandler = new HttpClientHandler();
		private readonly HttpClient WOWhttpClient = new HttpClient(WOWhttpHandler);

		// Custom HTTP - seconds
		private static readonly HttpClientHandler customHttpSecondsHandler = new HttpClientHandler();
		private readonly HttpClient customHttpSecondsClient = new HttpClient(customHttpSecondsHandler);
		private bool updatingCustomHttpSeconds;
		private readonly TokenParser customHttpSecondsTokenParser = new TokenParser();
		internal Timer CustomHttpSecondsTimer;
		internal bool CustomHttpSecondsEnabled;
		internal string CustomHttpSecondsString;
		internal int CustomHttpSecondsInterval;

		// Custom HTTP - minutes
		private static readonly HttpClientHandler customHttpMinutesHandler = new HttpClientHandler();
		private readonly HttpClient customHttpMinutesClient = new HttpClient(customHttpMinutesHandler);
		private bool updatingCustomHttpMinutes;
		private readonly TokenParser customHttpMinutesTokenParser = new TokenParser();
		internal bool CustomHttpMinutesEnabled;
		internal string CustomHttpMinutesString;
		internal int CustomHttpMinutesInterval;
		internal int CustomHttpMinutesIntervalIndex;

		// Custom HTTP - rollover
		private static readonly HttpClientHandler customHttpRolloverHandler = new HttpClientHandler();
		private readonly HttpClient customHttpRolloverClient = new HttpClient(customHttpRolloverHandler);
		private bool updatingCustomHttpRollover;
		private readonly TokenParser customHttpRolloverTokenParser = new TokenParser();
		internal bool CustomHttpRolloverEnabled;
		internal string CustomHttpRolloverString;

		public Thread ftpThread;
		public Thread MySqlCatchupThread;

		public string xapHeartbeat;
		public string xapsource;

		public MySqlConnection MonthlyMySqlConn = new MySqlConnection();
		public MySqlConnection RealtimeSqlConn = new MySqlConnection();
		public MySqlConnection CustomMysqlSecondsConn = new MySqlConnection();
		public MySqlCommand CustomMysqlSecondsCommand = new MySqlCommand();
		public MySqlConnection CustomMysqlMinutesConn = new MySqlConnection();
		public MySqlCommand CustomMysqlMinutesCommand = new MySqlCommand();
		public MySqlConnection CustomMysqlRolloverConn = new MySqlConnection();
		public MySqlCommand CustomMysqlRolloverCommand = new MySqlCommand();

		public MySqlConnectionStringBuilder MySqlConnSettings = new MySqlConnectionStringBuilder();

		public string LatestBuild = "n/a";

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

		private bool customMySqlSecondsUpdateInProgress;
		private bool customMySqlMinutesUpdateInProgress;
		private bool customMySqlRolloverUpdateInProgress;

		public AirLinkData airLinkDataIn;
		public AirLinkData airLinkDataOut;

		public string[] StationDesc =
		{
			"Davis Vantage Pro", "Davis Vantage Pro2", "Oregon Scientific WMR-928", "Oregon Scientific WM-918", "EasyWeather", "Fine Offset",
			"LaCrosse WS2300", "Fine Offset with Solar", "Oregon Scientific WMR100", "Oregon Scientific WMR200", "Instromet", "Davis WLL", "GW1000"
		};

		public string[] APRSstationtype = { "DsVP", "DsVP", "WMR928", "WM918", "EW", "FO", "WS2300", "FOs", "WMR100", "WMR200", "Instromet", "DsVP", "Ecowitt" };

		public string loggingfile;

		public Cumulus(int HTTPport, bool DebugEnabled, string startParms)
		{
			var fullVer = Assembly.GetExecutingAssembly().GetName().Version;
			Version = $"{fullVer.Major}.{fullVer.Minor}.{fullVer.Build}";
			Build = Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString();

			DirectorySeparator = Path.DirectorySeparatorChar;

			AppDir = Directory.GetCurrentDirectory() + DirectorySeparator;
			TwitterTxtFile = AppDir + "twitter.txt";
			WebTagFile = AppDir + "WebTags.txt";

			//b3045>, use same port for WS...  WS port = HTTPS port
			//wsPort = WSport;
			wsPort = HTTPport;

			DebuggingEnabled = DebugEnabled;

			// Set up the diagnostic tracing
			loggingfile = GetLoggingFileName("MXdiags" + DirectorySeparator);

			Program.svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Creating main MX log file - " + loggingfile);
			Program.svcTextListener.Flush();

			TextWriterTraceListener myTextListener = new TextWriterTraceListener(loggingfile, "MXlog");
			Trace.Listeners.Add(myTextListener);
			Trace.AutoFlush = true;

			// Read the configuration file

			LogMessage(" ========================== Cumulus MX starting ==========================");

			LogMessage("Command line: " + Environment.CommandLine + " " + startParms);

			//Assembly thisAssembly = this.GetType().Assembly;
			//Version = thisAssembly.GetName().Version.ToString();
			//VersionLabel.Content = "Cumulus v." + thisAssembly.GetName().Version;
			LogMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Working Dir: " + AppDir);

			IsOSX = IsRunningOnMac();

			Platform = IsOSX ? "Mac OS X" : Environment.OSVersion.Platform.ToString();

			// Set the default comport name depending on platform
			DefaultComportName = Platform.Substring(0, 3) == "Win" ? "COM1" : "/dev/ttyUSB0";

			LogMessage("Platform: " + Platform);

			LogMessage("OS version: " + Environment.OSVersion);

			Type type = Type.GetType("Mono.Runtime");
			if (type != null)
			{
				MethodInfo displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
				if (displayName != null)
					LogMessage("Mono version: "+displayName.Invoke(null, null));
			}

			// determine system uptime based on OS
			if (Platform.Substring(0, 3) == "Win")
			{
				try
				{
					// Windows enable the performance counter method
					UpTime = new PerformanceCounter("System", "System Up Time");
				}
				catch (Exception e)
				{
					LogMessage("Error: Unable to acces the System Up Time performance counter. System up time will not be available");
					LogDebugMessage($"Error: {e}");
				}
			}

			LogMessage("Current culture: " + CultureInfo.CurrentCulture.DisplayName);
			ListSeparator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

			DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

			LogMessage("Directory separator=[" + DirectorySeparator + "] Decimal separator=[" + DecimalSeparator + "] List separator=[" + ListSeparator + "]");
			LogMessage("Date separator=[" + CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator + "] Time separator=[" + CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator + "]");

			TimeZone localZone = TimeZone.CurrentTimeZone;
			DateTime now = DateTime.Now;

			LogMessage("Standard time zone name:   " + localZone.StandardName);
			LogMessage("Daylight saving time name: " + localZone.DaylightName);
			LogMessage("Daylight saving time? " + localZone.IsDaylightSavingTime(now));

			LogMessage(DateTime.Now.ToString("G"));

			// find the data folder
			//datapath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + DirectorySeparator + "Cumulus" + DirectorySeparator;

			Datapath = "data" + DirectorySeparator;
			backupPath = "backup" + DirectorySeparator;
			ReportPath = "Reports" + DirectorySeparator;
			var WebPath = "web" + DirectorySeparator;

			dbfile = Datapath + "cumulusmx.db";
			diaryfile = Datapath + "diary.db";

			//AlltimeFile = Datapath + "alltime.rec";
			AlltimeIniFile = Datapath + "alltime.ini";
			Alltimelogfile = Datapath + "alltimelog.txt";
			MonthlyAlltimeIniFile = Datapath + "monthlyalltime.ini";
			MonthlyAlltimeLogFile = Datapath + "monthlyalltimelog.txt";
			logFilePath = Datapath;
			DayFileName = Datapath + "dayfile.txt";
			YesterdayFile = Datapath + "yesterday.ini";
			TodayIniFile = Datapath + "today.ini";
			MonthIniFile = Datapath + "month.ini";
			YearIniFile = Datapath + "year.ini";
			//stringsFile = "strings.ini";

			// Set the default upload intervals for web services
			Wund.DefaultInterval = 15;
			Windy.DefaultInterval = 15;
			WindGuru.DefaultInterval = 1;
			PWS.DefaultInterval = 15;
			APRS.DefaultInterval = 9;
			AWEKAS.DefaultInterval = 15 * 60;
			WCloud.DefaultInterval = 10;
			OpenWeatherMap.DefaultInterval = 15;

			StdWebFiles = new FileGenerationFtpOptions[2];
			StdWebFiles[0] = new FileGenerationFtpOptions()
			{
				TemplateFileName = WebPath + "websitedataT.json",
				LocalPath = WebPath,
				LocalFileName = "websitedata.json",
				RemoteFileName = "websitedata.json"
			};
			StdWebFiles[1] = new FileGenerationFtpOptions()
			{
				LocalPath = "",
				LocalFileName = "wxnow.txt",
				RemoteFileName = "wxnow.txt"
			};

			RealtimeFiles = new FileGenerationFtpOptions[2];
			RealtimeFiles[0] = new FileGenerationFtpOptions()
			{
				LocalFileName = "realtime.txt",
				RemoteFileName = "realtime.txt"
			};
			RealtimeFiles[1] = new FileGenerationFtpOptions()
			{
				TemplateFileName = WebPath + "realtimegaugesT.txt",
				LocalPath = WebPath,
				LocalFileName = "realtimegauges.txt",
				RemoteFileName = "realtimegauges.txt"
			};

			GraphDataFiles = new FileGenerationFtpOptions[13];
			GraphDataFiles[0] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "graphconfig.json",
				RemoteFileName = "graphconfig.json"
			};
			GraphDataFiles[1] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "availabledata.json",
				RemoteFileName = "availabledata.json"
			};
			GraphDataFiles[2] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "tempdata.json",
				RemoteFileName ="tempdata.json"
			};
			GraphDataFiles[3] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "pressdata.json",
				RemoteFileName = "pressdata.json"
			};
			GraphDataFiles[4] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "winddata.json",
				RemoteFileName = "winddata.json"
			};
			GraphDataFiles[5] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "wdirdata.json",
				RemoteFileName = "wdirdata.json"
			};
			GraphDataFiles[6] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "humdata.json",
				RemoteFileName = "humdata.json"
			};
			GraphDataFiles[7] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "raindata.json",
				RemoteFileName = "raindata.json"
			};
			GraphDataFiles[8] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "dailyrain.json",
				RemoteFileName = "dailyrain.json"
			};
			GraphDataFiles[9] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "dailytemp.json",
				RemoteFileName = "dailytemp.json"
			};
			GraphDataFiles[10] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "solardata.json",
				RemoteFileName = "solardata.json"
			};
			GraphDataFiles[11] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "sunhours.json",
				RemoteFileName = "sunhours.json"
			};
			GraphDataFiles[12] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "airquality.json",
				RemoteFileName = "airquality.json"
			};

			GraphDataEodFiles = new FileGenerationFtpOptions[8];
			GraphDataEodFiles[0] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailytempdata.json",
				RemoteFileName = "alldailytempdata.json"
			};
			GraphDataEodFiles[1] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailypressdata.json",
				RemoteFileName = "alldailypressdata.json"
			};
			GraphDataEodFiles[2] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailywinddata.json",
				RemoteFileName = "alldailywinddata.json"
			};
			GraphDataEodFiles[3] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailyhumdata.json",
				RemoteFileName = "alldailyhumdata.json"
			};
			GraphDataEodFiles[4] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailyraindata.json",
				RemoteFileName = "alldailyraindata.json"
			};
			GraphDataEodFiles[5] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailysolardata.json",
				RemoteFileName = "alldailysolardata.json"
			};
			GraphDataEodFiles[6] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alldailydegdaydata.json",
				RemoteFileName = "alldailydegdaydata.json"
			};
			GraphDataEodFiles[7] = new FileGenerationFtpOptions()
			{
				LocalPath = WebPath,
				LocalFileName = "alltempsumdata.json",
				RemoteFileName = "alltempsumdata.json"
			};

			ReadIniFile();

			// Do we prevent more than one copy of CumulusMX running?
			if (ProgramOptions.WarnMultiple && !Program.appMutex.WaitOne(0, false))
			{
				LogConsoleMessage("Cumulus is already running - terminating");
				LogConsoleMessage("Program exit");
				LogMessage("Cumulus is already running - terminating");
				LogMessage("Program exit");
				Environment.Exit(1);
			}

			// Do we wait for a ping response from a remote host before starting?
			if (!string.IsNullOrWhiteSpace(ProgramOptions.StartupPingHost))
			{
				var msg1 = $"Waiting for PING reply from {ProgramOptions.StartupPingHost}";
				var msg2 = $"Received PING response from {ProgramOptions.StartupPingHost}, continuing...";
				var msg3 = $"No PING response received in {ProgramOptions.StartupPingEscapeTime} minutes, continuing anyway";
				LogConsoleMessage(msg1);
				LogMessage(msg1);
				using (var ping = new Ping())
				{
					var endTime = DateTime.Now.AddMinutes(ProgramOptions.StartupPingEscapeTime);
					PingReply reply = null;

					do
					{
						try
						{

							reply = ping.Send(ProgramOptions.StartupPingHost, 2000);  // 2 second timeout
							LogMessage($"PING response = {reply.Status}");
						}
						catch (Exception e)
						{
							LogErrorMessage($"PING to {ProgramOptions.StartupPingHost} failed with error: {e.InnerException.Message}");
						}

						if (reply == null || reply.Status != IPStatus.Success)
						{
							// no response wait 10 seconds before trying again
							Thread.Sleep(10000);
							// Force a DNS refresh if not an IPv4 address
							if (!Utils.ValidateIPv4(ProgramOptions.StartupPingHost))
							{
								// catch and ignore IPv6 and invalid hostname for now
								try
								{
									Dns.GetHostEntry(ProgramOptions.StartupPingHost);
								}
								catch (Exception ex)
								{
									LogMessage($"PING: Error with DNS refresh - {ex.Message}");
								}
							}
						}
					} while ((reply == null || reply.Status != IPStatus.Success) && DateTime.Now < endTime);

					if (DateTime.Now >= endTime)
					{
						LogConsoleMessage(msg3);
						LogMessage(msg3);
					}
					else
					{
						LogConsoleMessage(msg2);
						LogMessage(msg2);
					}
				}
			}
			else
			{
				LogMessage("No start-up PING");
			}

			// Do we delay the start of Cumulus MX for a fixed period?
			if (ProgramOptions.StartupDelaySecs > 0)
			{
				// Check uptime
				double ts = 0;
				if (Platform.Substring(0, 3) == "Win" && UpTime != null)
				{
					UpTime.NextValue();
					ts = UpTime.NextValue();
				}
				else if (File.Exists(@"/proc/uptime"))
				{
					var text = File.ReadAllText(@"/proc/uptime");
					var strTime = text.Split(' ')[0];
					double.TryParse(strTime, out ts);
				}

				// Only delay if the delay uptime is undefined (0), or the current uptime is less than the user specified max uptime to apply the delay
				LogMessage($"System uptime = {(int)ts} secs");
				if (ProgramOptions.StartupDelayMaxUptime == 0 || ProgramOptions.StartupDelayMaxUptime > ts)
				{
					var msg1 = $"Delaying start for {ProgramOptions.StartupDelaySecs} seconds";
					var msg2 = $"Start-up delay complete, continuing...";
					LogConsoleMessage(msg1);
					LogMessage(msg1);
					Thread.Sleep(ProgramOptions.StartupDelaySecs * 1000);
					LogConsoleMessage(msg2);
					LogMessage(msg2);
				}
				else
				{
					LogMessage("No start-up delay, max uptime exceeded");
				}
			}
			else
			{
				LogMessage("No start-up delay - disabled");
			}

			GC.Collect();

			LogMessage("Data path = " + Datapath);

			AppDomain.CurrentDomain.SetData("DataDirectory", Datapath);

			// Open database (create file if it doesn't exist)
			SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
			//LogDB = new SQLiteConnection(dbfile, flags)
			//LogDB.CreateTable<StandardData>();

			// Open diary database (create file if it doesn't exist)
			//DiaryDB = new SQLiteConnection(diaryfile, flags, true);  // We should be using this - storing datetime as ticks, but historically string storage has been used, so we are stuck with it?
			DiaryDB = new SQLiteConnection(diaryfile, flags);
			DiaryDB.CreateTable<DiaryData>();

			BackupData(false, DateTime.Now);

			LogMessage("Debug logging is " + (ProgramOptions.DebugLogging ? "enabled" : "disabled"));
			LogMessage("Data logging is " + (ProgramOptions.DataLogging ? "enabled" : "disabled"));
			LogMessage("FTP logging is " + (FTPlogging ? "enabled" : "disabled"));
			LogMessage("Spike logging is " + (ErrorLogSpikeRemoval ? "enabled" : "disabled"));
			LogMessage("Logging interval = " + logints[DataLogInterval] + " mins");
			LogMessage("Real time interval = " + RealtimeInterval / 1000 + " secs");
			LogMessage("NoSensorCheck = " + (StationOptions.NoSensorCheck ? "1" : "0"));

			TempFormat = "F" + TempDPlaces;
			WindFormat = "F" + WindDPlaces;
			WindAvgFormat = "F" + WindAvgDPlaces;
			RainFormat = "F" + RainDPlaces;
			PressFormat = "F" + PressDPlaces;
			HumFormat = "F" + HumDPlaces;
			UVFormat = "F" + UVDPlaces;
			SunFormat = "F" + SunshineDPlaces;
			ETFormat = "F" + (RainDPlaces + 1);
			WindRunFormat = "F" + WindRunDPlaces;
			TempTrendFormat = "+0.0;-0.0;0";
			AirQualityFormat = "F" + AirQualityDPlaces;

			SetMonthlySqlCreateString();

			SetDayfileSqlCreateString();

			SetRealtimeSqlCreateString();

			if (Sslftp == FtpProtocols.FTP || Sslftp == FtpProtocols.FTPS)
			{
				if (ActiveFTPMode)
				{
					RealtimeFTP.DataConnectionType = FtpDataConnectionType.PORT;
				}
				else if (DisableFtpsEPSV)
				{
					RealtimeFTP.DataConnectionType = FtpDataConnectionType.PASV;
				}

				if (Sslftp == FtpProtocols.FTPS)
				{
					RealtimeFTP.EncryptionMode = DisableFtpsExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
					RealtimeFTP.DataConnectionEncryption = true;
					RealtimeFTP.ValidateAnyCertificate = true;
					// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specifiy protocols
					RealtimeFTP.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
				}
			}

			ReadStringsFile();

			SetUpHttpProxy();

			if (MonthlyMySqlEnabled)
			{
				MonthlyMySqlConn.ConnectionString = MySqlConnSettings.ToString();

				SetStartOfMonthlyInsertSQL();
			}

			if (DayfileMySqlEnabled)
			{
				SetStartOfDayfileInsertSQL();
			}

			if (RealtimeMySqlEnabled)
			{
				RealtimeSqlConn.ConnectionString = MySqlConnSettings.ToString();
				SetStartOfRealtimeInsertSQL();
			}


			CustomMysqlSecondsConn.ConnectionString = MySqlConnSettings.ToString();
			customMysqlSecondsTokenParser.OnToken += TokenParserOnToken;
			CustomMysqlSecondsCommand.Connection = CustomMysqlSecondsConn;
			CustomMysqlSecondsTimer = new Timer { Interval = CustomMySqlSecondsInterval * 1000 };
			CustomMysqlSecondsTimer.Elapsed += CustomMysqlSecondsTimerTick;
			CustomMysqlSecondsTimer.AutoReset = true;

			CustomMysqlMinutesConn.ConnectionString = MySqlConnSettings.ToString();
			customMysqlMinutesTokenParser.OnToken += TokenParserOnToken;
			CustomMysqlMinutesCommand.Connection = CustomMysqlMinutesConn;

			CustomMysqlRolloverConn.ConnectionString = MySqlConnSettings.ToString();
			customMysqlRolloverTokenParser.OnToken += TokenParserOnToken;
			CustomMysqlRolloverCommand.Connection = CustomMysqlRolloverConn;

			CustomHttpSecondsTimer = new Timer { Interval = CustomHttpSecondsInterval * 1000 };
			CustomHttpSecondsTimer.Elapsed += CustomHttpSecondsTimerTick;
			CustomHttpSecondsTimer.AutoReset = true;

			customHttpSecondsTokenParser.OnToken += TokenParserOnToken;
			customHttpMinutesTokenParser.OnToken += TokenParserOnToken;
			customHttpRolloverTokenParser.OnToken += TokenParserOnToken;

			if (SmtpOptions.Enabled)
			{
				emailer = new EmailSender(this);
			}

			DoSunriseAndSunset();
			DoMoonPhase();
			MoonAge = MoonriseMoonset.MoonAge();
			DoMoonImage();

			LogMessage("Station type: " + (StationType == -1 ? "Undefined" : StationDesc[StationType]));

			SetupUnitText();

			LogMessage($"Units.Wind={Units.WindText} RainUnit={Units.RainText} TempUnit={Units.TempText} PressureUnit={Units.PressText}");
			LogMessage($"YTDRain={YTDrain:F3} Year={YTDrainyear}");
			LogMessage($"RainDayThreshold={RainDayThreshold:F3}");
			LogMessage($"Roll over hour={RolloverHour}");

			LogOffsetsMultipliers();

			LogPrimaryAqSensor();

			// initialise the alarms
			DataStoppedAlarm.cumulus = this;
			BatteryLowAlarm.cumulus = this;
			SensorAlarm.cumulus = this;
			SpikeAlarm.cumulus = this;
			HighWindAlarm.cumulus = this;
			HighWindAlarm.Units = Units.WindText;
			HighGustAlarm.cumulus = this;
			HighGustAlarm.Units = Units.WindText;
			HighRainRateAlarm.cumulus = this;
			HighRainRateAlarm.Units = Units.RainTrendText;
			HighRainTodayAlarm.cumulus = this;
			HighRainTodayAlarm.Units = Units.RainText;
			PressChangeAlarm.cumulus = this;
			PressChangeAlarm.Units = Units.PressTrendText;
			HighPressAlarm.cumulus = this;
			HighPressAlarm.Units = Units.PressText;
			LowPressAlarm.cumulus = this;
			LowPressAlarm.Units = Units.PressText;
			TempChangeAlarm.cumulus = this;
			TempChangeAlarm.Units = Units.TempTrendText;
			HighTempAlarm.cumulus = this;
			HighTempAlarm.Units = Units.TempText;
			LowTempAlarm.cumulus = this;
			LowTempAlarm.Units = Units.TempText;
			UpgradeAlarm.cumulus = this;

			GetLatestVersion();

			LogMessage("Cumulus Starting");

			// switch off logging from Unosquare.Swan which underlies embedIO
			Unosquare.Swan.Terminal.Settings.DisplayLoggingMessageType = Unosquare.Swan.LogMessageType.Fatal;

			httpServer = new WebServer(HTTPport, RoutingStrategy.Wildcard);

			var assemblyPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
			var htmlRootPath = Path.Combine(assemblyPath, "interface");

			LogMessage("HTML root path = " + htmlRootPath);

			httpServer.RegisterModule(new StaticFilesModule(htmlRootPath, new Dictionary<string, string>() { { "Cache-Control", "max-age=300" } }));
			httpServer.Module<StaticFilesModule>().UseRamCache = true;

			// Set up the API web server
			// Some APi functions require the station, so set them after station initialisation
			Api.Setup(httpServer);
			Api.programSettings = new ProgramSettings(this);
			Api.stationSettings = new StationSettings(this);
			Api.internetSettings = new InternetSettings(this);
			Api.thirdpartySettings = new ThirdPartySettings(this);
			Api.extraSensorSettings = new ExtraSensorSettings(this);
			Api.calibrationSettings = new CalibrationSettings(this);
			Api.noaaSettings = new NOAASettings(this);
			Api.alarmSettings = new AlarmSettings(this);
			Api.mySqlSettings = new MysqlSettings(this);
			Api.dataEditor = new DataEditor(this);
			Api.tagProcessor = new ApiTagProcessor(this);

			// Set up the Web Socket server
			WebSocket.Setup(httpServer, this);

			httpServer.RunAsync();

			LogConsoleMessage("Cumulus running at: " + httpServer.Listener.Prefixes.First());
			LogConsoleMessage("  (Replace * with any IP address on this machine, or localhost)");
			LogConsoleMessage("  Open the admin interface by entering this URL in a browser.");

			LogDebugMessage("Lock: Cumulus waiting for the lock");
			syncInit.Wait();
			LogDebugMessage("Lock: Cumulus has lock");

			LogMessage("Opening station");

			switch (StationType)
			{
				case StationTypes.FineOffset:
				case StationTypes.FineOffsetSolar:
					Manufacturer = EW;
					station = new FOStation(this);
					break;
				case StationTypes.VantagePro:
				case StationTypes.VantagePro2:
					Manufacturer = DAVIS;
					station = new DavisStation(this);
					break;
				case StationTypes.WMR928:
					Manufacturer = OREGON;
					station = new WMR928Station(this);
					break;
				case StationTypes.WM918:
					Manufacturer = OREGON;
					station = new WM918Station(this);
					break;
				case StationTypes.WS2300:
					Manufacturer = LACROSSE;
					station = new WS2300Station(this);
					break;
				case StationTypes.WMR200:
					Manufacturer = OREGONUSB;
					station = new WMR200Station(this);
					break;
				case StationTypes.Instromet:
					Manufacturer = INSTROMET;
					station = new ImetStation(this);
					break;
				case StationTypes.WMR100:
					Manufacturer = OREGONUSB;
					station = new WMR100Station(this);
					break;
				case StationTypes.EasyWeather:
					Manufacturer = EW;
					station = new EasyWeather(this);
					station.LoadLastHoursFromDataLogs(DateTime.Now);
					break;
				case StationTypes.WLL:
					Manufacturer = DAVIS;
					station = new DavisWllStation(this);
					break;
				case StationTypes.GW1000:
					Manufacturer = ECOWITT;
					station = new GW1000Station(this);
					break;
				default:
					LogConsoleMessage("Station type not set");
					LogMessage("Station type not set");
					break;
			}

			if (station != null)
			{
				Api.Station = station;
				Api.stationSettings.SetStation(station);
				Api.dataEditor.SetStation(station);

				LogMessage("Creating extra sensors");
				if (AirLinkInEnabled)
				{
					airLinkDataIn = new AirLinkData();
					airLinkIn = new DavisAirLink(this, true, station);
				}
				if (AirLinkOutEnabled)
				{
					airLinkDataOut = new AirLinkData();
					airLinkOut = new DavisAirLink(this, false, station);
				}

				webtags = new WebTags(this, station);
				webtags.InitialiseWebtags();

				Api.dataEditor.SetWebTags(webtags);
				Api.tagProcessor.SetWebTags(webtags);

				tokenParser = new TokenParser();
				tokenParser.OnToken += TokenParserOnToken;

				realtimeTokenParser = new TokenParser();
				realtimeTokenParser.OnToken += TokenParserOnToken;

				RealtimeTimer.Interval = RealtimeInterval;
				RealtimeTimer.Elapsed += RealtimeTimerTick;
				RealtimeTimer.AutoReset = true;

				SetFtpLogging(FTPlogging);

				WundTimer.Elapsed += WundTimerTick;
				AwekasTimer.Elapsed += AwekasTimerTick;
				WebTimer.Elapsed += WebTimerTick;

				xapsource = "sanday.cumulus." + Environment.MachineName;

				xapHeartbeat = "xap-hbeat\n{\nv=12\nhop=1\nuid=FF" + xapUID + "00\nclass=xap-hbeat.alive\nsource=" + xapsource + "\ninterval=60\n}";

				if (xapEnabled)
				{
					Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					IPEndPoint iep1 = new IPEndPoint(IPAddress.Broadcast, xapPort);

					byte[] data = Encoding.ASCII.GetBytes(xapHeartbeat);

					sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
					sock.SendTo(data, iep1);
					sock.Close();
				}

				if (MQTT.EnableDataUpdate || MQTT.EnableInterval)
				{
					MqttPublisher.Setup(this);

					if (MQTT.EnableInterval)
					{
						MQTTTimer.Elapsed += MQTTTimerTick;
					}
				}

				InitialiseRG11();

				if (station.timerStartNeeded)
				{
					StartTimersAndSensors();
				}

				if ((StationType == StationTypes.WMR100) || (StationType == StationTypes.EasyWeather) || (Manufacturer == OREGON))
				{
					station.StartLoop();
				}

				// If enabled generate the daily graph data files, and upload at first opportunity
				LogDebugMessage("Generating the daily graph data files");
				station.CreateEodGraphDataFiles();
			}

			LogDebugMessage("Lock: Cumulus releasing the lock");
			syncInit.Release();
		}

		internal void SetUpHttpProxy()
		{
			if (!string.IsNullOrEmpty(HTTPProxyName))
			{
				WUhttpHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				WUhttpHandler.UseProxy = true;

				PWShttpHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				PWShttpHandler.UseProxy = true;

				WOWhttpHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				WOWhttpHandler.UseProxy = true;

				AwekashttpHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				AwekashttpHandler.UseProxy = true;

				WindyhttpHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				WindyhttpHandler.UseProxy = true;

				WCloudhttpHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				WCloudhttpHandler.UseProxy = true;

				customHttpSecondsHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				customHttpSecondsHandler.UseProxy = true;

				customHttpMinutesHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				customHttpMinutesHandler.UseProxy = true;

				customHttpRolloverHandler.Proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				customHttpRolloverHandler.UseProxy = true;

				if (!string.IsNullOrEmpty(HTTPProxyUser))
				{
					WUhttpHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					PWShttpHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					WOWhttpHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					AwekashttpHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					WindyhttpHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					WCloudhttpHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					customHttpSecondsHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					customHttpMinutesHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					customHttpRolloverHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
				}
			}
		}

		private void CustomHttpSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!station.DataStopped)
				CustomHttpSecondsUpdate();
		}

		internal void SetStartOfRealtimeInsertSQL()
		{
			StartOfRealtimeInsertSQL = "INSERT IGNORE INTO " + MySqlRealtimeTable + " (" +
				"LogDateTime,temp,hum,dew,wspeed,wlatest,bearing,rrate,rfall,press," +
				"currentwdir,beaufortnumber,windunit,tempunitnodeg,pressunit,rainunit," +
				"windrun,presstrendval,rmonth,ryear,rfallY,intemp,inhum,wchill,temptrend," +
				"tempTH,TtempTH,tempTL,TtempTL,windTM,TwindTM,wgustTM,TwgustTM," +
				"pressTH,TpressTH,pressTL,TpressTL,version,build,wgust,heatindex,humidex," +
				"UV,ET,SolarRad,avgbearing,rhour,forecastnumber,isdaylight,SensorContactLost," +
				"wdir,cloudbasevalue,cloudbaseunit,apptemp,SunshineHours,CurrentSolarMax,IsSunny," +
				"FeelsLike)";
		}

		internal void SetRealtimeSqlCreateString()
		{
			CreateRealtimeSQL = "CREATE TABLE " + MySqlRealtimeTable + " (LogDateTime DATETIME NOT NULL," +
				"temp decimal(4," + TempDPlaces + ") NOT NULL," +
				"hum decimal(4," + HumDPlaces + ") NOT NULL," +
				"dew decimal(4," + TempDPlaces + ") NOT NULL," +
				"wspeed decimal(4," + WindDPlaces + ") NOT NULL," +
				"wlatest decimal(4," + WindDPlaces + ") NOT NULL," +
				"bearing VARCHAR(3) NOT NULL," +
				"rrate decimal(4," + RainDPlaces + ") NOT NULL," +
				"rfall decimal(4," + RainDPlaces + ") NOT NULL," +
				"press decimal(6," + PressDPlaces + ") NOT NULL," +
				"currentwdir varchar(3) NOT NULL," +
				"beaufortnumber varchar(2) NOT NULL," +
				"windunit varchar(4) NOT NULL," +
				"tempunitnodeg varchar(1) NOT NULL," +
				"pressunit varchar(3) NOT NULL," +
				"rainunit varchar(2) NOT NULL," +
				"windrun decimal(4," + WindRunDPlaces + ") NOT NULL," +
				"presstrendval varchar(6) NOT NULL," +
				"rmonth decimal(4," + RainDPlaces + ") NOT NULL," +
				"ryear decimal(4," + RainDPlaces + ") NOT NULL," +
				"rfallY decimal(4," + RainDPlaces + ") NOT NULL," +
				"intemp decimal(4," + TempDPlaces + ") NOT NULL," +
				"inhum decimal(4," + HumDPlaces + ") NOT NULL," +
				"wchill decimal(4," + TempDPlaces + ") NOT NULL," +
				"temptrend varchar(5) NOT NULL," +
				"tempTH decimal(4," + TempDPlaces + ") NOT NULL," +
				"TtempTH varchar(5) NOT NULL," +
				"tempTL decimal(4," + TempDPlaces + ") NOT NULL," +
				"TtempTL varchar(5) NOT NULL," +
				"windTM decimal(4," + WindDPlaces + ") NOT NULL," +
				"TwindTM varchar(5) NOT NULL," +
				"wgustTM decimal(4," + WindDPlaces + ") NOT NULL," +
				"TwgustTM varchar(5) NOT NULL," +
				"pressTH decimal(6," + PressDPlaces + ") NOT NULL," +
				"TpressTH varchar(5) NOT NULL," +
				"pressTL decimal(6," + PressDPlaces + ") NOT NULL," +
				"TpressTL varchar(5) NOT NULL," +
				"version varchar(8) NOT NULL," +
				"build varchar(5) NOT NULL," +
				"wgust decimal(4," + WindDPlaces + ") NOT NULL," +
				"heatindex decimal(4," + TempDPlaces + ") NOT NULL," +
				"humidex decimal(4," + TempDPlaces + ") NOT NULL," +
				"UV decimal(3," + UVDPlaces + ") NOT NULL," +
				"ET decimal(4," + RainDPlaces + ") NOT NULL," +
				"SolarRad decimal(5,1) NOT NULL," +
				"avgbearing varchar(3) NOT NULL," +
				"rhour decimal(4," + RainDPlaces + ") NOT NULL," +
				"forecastnumber varchar(2) NOT NULL," +
				"isdaylight varchar(1) NOT NULL," +
				"SensorContactLost varchar(1) NOT NULL," +
				"wdir varchar(3) NOT NULL," +
				"cloudbasevalue varchar(5) NOT NULL," +
				"cloudbaseunit varchar(2) NOT NULL," +
				"apptemp decimal(4," + TempDPlaces + ") NOT NULL," +
				"SunshineHours decimal(3," + SunshineDPlaces + ") NOT NULL," +
				"CurrentSolarMax decimal(5,1) NOT NULL," +
				"IsSunny varchar(1) NOT NULL," +
				"FeelsLike decimal(4," + TempDPlaces + ") NOT NULL," +
				"PRIMARY KEY (LogDateTime)) COMMENT = \"Realtime log\"";
		}

		internal void SetStartOfDayfileInsertSQL()
		{
			StartOfDayfileInsertSQL = "INSERT IGNORE INTO " + MySqlDayfileTable + " (" +
				"LogDate,HighWindGust,HWindGBear,THWindG,MinTemp,TMinTemp,MaxTemp,TMaxTemp," +
				"MinPress,TMinPress,MaxPress,TMaxPress,MaxRainRate,TMaxRR,TotRainFall,AvgTemp," +
				"TotWindRun,HighAvgWSpeed,THAvgWSpeed,LowHum,TLowHum,HighHum,THighHum,TotalEvap," +
				"HoursSun,HighHeatInd,THighHeatInd,HighAppTemp,THighAppTemp,LowAppTemp,TLowAppTemp," +
				"HighHourRain,THighHourRain,LowWindChill,TLowWindChill,HighDewPoint,THighDewPoint," +
				"LowDewPoint,TLowDewPoint,DomWindDir,HeatDegDays,CoolDegDays,HighSolarRad," +
				"THighSolarRad,HighUV,THighUV,HWindGBearSym,DomWindDirSym," +
				"MaxFeelsLike,TMaxFeelsLike,MinFeelsLike,TMinFeelsLike,MaxHumidex,TMaxHumidex)";
		}

		internal void SetStartOfMonthlyInsertSQL()
		{
			StartOfMonthlyInsertSQL = "INSERT IGNORE INTO " + MySqlMonthlyTable + " (" +
				"LogDateTime,Temp,Humidity,Dewpoint,Windspeed,Windgust,Windbearing,RainRate,TodayRainSoFar," +
				"Pressure,Raincounter,InsideTemp,InsideHumidity,LatestWindGust,WindChill,HeatIndex,UVindex," +
				"SolarRad,Evapotrans,AnnualEvapTran,ApparentTemp,MaxSolarRad,HrsSunShine,CurrWindBearing," +
				"RG11rain,RainSinceMidnight,WindbearingSym,CurrWindBearingSym,FeelsLike,Humidex)";
		}

		internal void SetupUnitText()
		{
			switch (Units.Temp)
			{
				case 0:
					Units.TempText = "°C";
					Units.TempTrendText = "°C/hr";
					break;
				case 1:
					Units.TempText = "°F";
					Units.TempTrendText = "°F/hr";
					break;
			}

			switch (Units.Rain)
			{
				case 0:
					Units.RainText = "mm";
					Units.RainTrendText = "mm/hr";
					break;
				case 1:
					Units.RainText = "in";
					Units.RainTrendText = "in/hr";
					break;
			}

			switch (Units.Press)
			{
				case 0:
					Units.PressText = "mb";
					Units.PressTrendText = "mb/hr";
					break;
				case 1:
					Units.PressText = "hPa";
					Units.PressTrendText = "hPa/hr";
					break;
				case 2:
					Units.PressText = "in";
					Units.PressTrendText = "in/hr";
					break;
			}

			switch (Units.Wind)
			{
				case 0:
					Units.WindText = "m/s";
					Units.WindRunText = "km";
					break;
				case 1:
					Units.WindText = "mph";
					Units.WindRunText = "miles";
					break;
				case 2:
					Units.WindText = "km/h";
					Units.WindRunText = "km";
					break;
				case 3:
					Units.WindText = "kts";
					Units.WindRunText = "nm";
					break;
			}
		}

		public void SetFtpLogging(bool isSet)
		{
			try
			{
				FtpTrace.RemoveListener(FtpTraceListener);
			}
			catch
			{
				// ignored
			}

			if (isSet)
			{
				FtpTrace.AddListener(FtpTraceListener);
				FtpTrace.FlushOnWrite = true;
			}
		}


		/*
		private string LocalIPAddress()
		{
			IPHostEntry host;
			string localIP = "";
			host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					localIP = ip.ToString();
					break;
				}
			}
			return localIP;
		}
		*/

		/*
		private void OnDisconnect(UserContext context)
		{
			LogDebugMessage("Disconnect From : " + context.ClientAddress.ToString());

			foreach (var conn in WSconnections.ToList())
			{
				if (context.ClientAddress.ToString().Equals(conn.ClientAddress.ToString()))
				{
					WSconnections.Remove(conn);
				}
			}
		}

		private void OnConnected(UserContext context)
		{
			LogDebugMessage("Connected From : " + context.ClientAddress.ToString());
		}

		private void OnConnect(UserContext context)
		{
			LogDebugMessage("OnConnect From : " + context.ClientAddress.ToString());
			WSconnections.Add(context);
		}

		internal List<UserContext> WSconnections = new List<UserContext>();

		private void OnSend(UserContext context)
		{
			LogDebugMessage("OnSend From : " + context.ClientAddress.ToString());
		}

		private void OnReceive(UserContext context)
		{
			LogDebugMessage("WS receive : " + context.DataFrame.ToString());
		}
*/
		private void InitialiseRG11()
		{
			if (RG11Enabled && RG11Port.Length > 0)
			{
				cmprtRG11 = new SerialPort(RG11Port, 9600, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, RtsEnable = true, DtrEnable = true };

				cmprtRG11.PinChanged += RG11StateChange;
			}

			if (RG11Enabled2 && RG11Port2.Length > 0 && (!RG11Port2.Equals(RG11Port)))
			{
				// a second RG11 is in use, using a different com port
				cmprt2RG11 = new SerialPort(RG11Port2, 9600, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, RtsEnable = true, DtrEnable = true };

				cmprt2RG11.PinChanged += RG11StateChange;
			}
		}

		private void RG11StateChange(object sender, SerialPinChangedEventArgs e)
		{
			bool isDSR = e.EventType == SerialPinChange.DsrChanged;
			bool isCTS = e.EventType == SerialPinChange.CtsChanged;

			// Is this a trigger that the first RG11 is configured for?
			bool isDevice1 = (((SerialPort)sender).PortName == RG11Port) && ((isDSR && RG11DTRmode) || (isCTS && !RG11DTRmode));
			// Is this a trigger that the second RG11 is configured for?
			bool isDevice2 = (((SerialPort)sender).PortName == RG11Port2) && ((isDSR && RG11DTRmode2) || (isCTS && !RG11DTRmode2));

			// is the pin on or off?
			bool isOn = (isDSR && ((SerialPort)sender).DsrHolding) || (isCTS && ((SerialPort)sender).CtsHolding);

			if (isDevice1)
			{
				if (RG11TBRmode)
				{
					if (isOn)
					{
						// relay closed, record a 'tip'
						station.RG11RainToday += RG11tipsize;
					}
				}
				else
				{
					station.IsRaining = isOn;
				}
			}
			else if (isDevice2)
			{
				if (RG11TBRmode2)
				{
					if (isOn)
					{
						// relay closed, record a 'tip'
						station.RG11RainToday += RG11tipsize;
					}
				}
				else
				{
					station.IsRaining = isOn;
				}
			}
		}

		private void APRSTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrEmpty(APRS.ID))
			{
				station.UpdateAPRS();
			}
		}

		private void WebTimerTick(object sender, ElapsedEventArgs e)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything else
				return;
			}

			if (WebUpdating == 1)
			{
				LogMessage("Warning, previous web update is still in progress, first chance, skipping this interval");
				WebUpdating++;
			}
			else if (WebUpdating >= 2)
			{
				LogMessage("Warning, previous web update is still in progress, second chance, aborting connection");
				if (ftpThread.ThreadState == System.Threading.ThreadState.Running)
					ftpThread.Abort();
				LogMessage("Trying new web update");
				WebUpdating = 1;
				ftpThread = new Thread(DoHTMLFiles) { IsBackground = true };
				ftpThread.Start();
			}
			else
			{
				WebUpdating = 1;
				ftpThread = new Thread(DoHTMLFiles) { IsBackground = true };
				ftpThread.Start();
			}
		}

		internal async void UpdateTwitter()
		{
			if (station.DataStopped)
			{
				// No data coming in, do nothing
				return;
			}

			LogDebugMessage("Starting Twitter update");
			var auth = new XAuthAuthorizer
			{
				CredentialStore = new XAuthCredentials { ConsumerKey = twitterKey, ConsumerSecret = twitterSecret, UserName = Twitter.ID, Password = Twitter.PW }
			};

			if (Twitter.OauthToken == "unknown")
			{
				// need to get tokens using xauth
				LogDebugMessage("Obtaining Twitter tokens");
				await auth.AuthorizeAsync();

				Twitter.OauthToken = auth.CredentialStore.OAuthToken;
				Twitter.OauthTokenSecret = auth.CredentialStore.OAuthTokenSecret;
				//LogDebugMessage("Token=" + TwitterOauthToken);
				//LogDebugMessage("TokenSecret=" + TwitterOauthTokenSecret);
				LogDebugMessage("Tokens obtained");
			}
			else
			{
				auth.CredentialStore.OAuthToken = Twitter.OauthToken;
				auth.CredentialStore.OAuthTokenSecret = Twitter.OauthTokenSecret;
			}

			using (var twitterCtx = new TwitterContext(auth))
			{
				StringBuilder status = new StringBuilder(1024);

				if (File.Exists(TwitterTxtFile))
				{
					// use twitter.txt file
					LogDebugMessage("Using twitter.txt file");
					var twitterTokenParser = new TokenParser();
					var utf8WithoutBom = new UTF8Encoding(false);
					var encoding = utf8WithoutBom;
					twitterTokenParser.Encoding = encoding;
					twitterTokenParser.SourceFile = TwitterTxtFile;
					twitterTokenParser.OnToken += TokenParserOnToken;
					status.Append(twitterTokenParser);
				}
				else
				{
					// default message
					status.Append($"Wind {station.WindAverage.ToString(WindAvgFormat)} {Units.WindText} {station.AvgBearingText}.");
					status.Append($" Barometer {station.Pressure.ToString(PressFormat)} {Units.PressText}, {station.Presstrendstr}.");
					status.Append($" Temperature {station.OutdoorTemperature.ToString(TempFormat)} {Units.TempText}.");
					status.Append($" Rain today {station.RainToday.ToString(RainFormat)}{Units.RainText}.");
					status.Append($" Humidity {station.OutdoorHumidity}%");
				}

				LogDebugMessage($"Updating Twitter: {status}");

				var statusStr = status.ToString();

				try
				{
					Status tweet;

					if (Twitter.SendLocation)
					{
						tweet = await twitterCtx.TweetAsync(statusStr, (decimal)Latitude, (decimal)Longitude);
					}
					else
					{
						tweet = await twitterCtx.TweetAsync(statusStr);
					}

					if (tweet == null)
					{
						LogDebugMessage("Null Twitter response");
					}
					else
					{
						LogDebugMessage($"Status returned: ({tweet.StatusID}) {tweet.User.Name}, {tweet.Text}, {tweet.CreatedAt}");
					}
				}
				catch (Exception ex)
				{
					LogMessage($"UpdateTwitter: {ex.Message}");
				}
				//if (tweet != null)
				//    Console.WriteLine("Status returned: " + "(" + tweet.StatusID + ")" + tweet.User.Name + ", " + tweet.Text + "\n");
			}
		}

		private void WundTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(Wund.ID))
				UpdateWunderground(DateTime.Now);
		}


		private void AwekasTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(AWEKAS.ID))
				UpdateAwekas(DateTime.Now);
		}

		public void MQTTTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!station.DataStopped)
				MqttPublisher.UpdateMQTTfeed("Interval");
		}

		/*
		 * 15/1020 - This does nothing!
		public void AirLinkTimerTick(object sender, ElapsedEventArgs e)
		{
			if (AirLinkInEnabled && airLinkIn != null)
			{
				airLinkIn.GetAirQuality();
			}
			if (AirLinkOutEnabled && airLinkOut != null)
			{
				airLinkOut.GetAirQuality();
			}
		}
		*/

		internal async void UpdateWunderground(DateTime timestamp)
		{
			if (Wund.Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			Wund.Updating = true;

			string pwstring;
			string URL = station.GetWundergroundURL(out pwstring, timestamp, false);

			string starredpwstring = "&PASSWORD=" + new string('*', Wund.PW.Length);

			string logUrl = URL.Replace(pwstring, starredpwstring);
			if (!Wund.RapidFireEnabled)
			{
				LogDebugMessage("Wunderground: URL = " + logUrl);
			}

			try
			{
				HttpResponseMessage response = await WUhttpClient.GetAsync(URL);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				if (!Wund.RapidFireEnabled || response.StatusCode != HttpStatusCode.OK)
				{
					LogDebugMessage("Wunderground: Response = " + response.StatusCode + ": " + responseBodyAsText);
				}
			}
			catch (Exception ex)
			{
				LogMessage("Wunderground: ERROR - " + ex.Message);
			}
			finally
			{
				Wund.Updating = false;
			}
		}

		internal async void UpdateWindy(DateTime timestamp)
		{
			if (Windy.Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			Windy.Updating = true;

			string apistring;
			string url = station.GetWindyURL(out apistring, timestamp);
			string logUrl = url.Replace(apistring, "<<API_KEY>>");

			LogDebugMessage("Windy: URL = " + logUrl);

			try
			{
				HttpResponseMessage response = await WindyhttpClient.GetAsync(url);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				LogDebugMessage("Windy: Response = " + response.StatusCode + ": " + responseBodyAsText);
			}
			catch (Exception ex)
			{
				LogMessage("Windy: ERROR - " + ex.Message);
			}
			finally
			{
				Windy.Updating = false;
			}
		}

		internal async void UpdateWindGuru(DateTime timestamp)
		{
			if (WindGuru.Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			WindGuru.Updating = true;

			string apistring;
			string url = station.GetWindGuruURL(out apistring, timestamp);
			string logUrl = url.Replace(apistring, "<<StationUID>>");

			LogDebugMessage("WindGuru: URL = " + logUrl);

			try
			{
				HttpResponseMessage response = await WindGuruhttpClient.GetAsync(url);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				LogDebugMessage("WindGuru: " + response.StatusCode + ": " + responseBodyAsText);
			}
			catch (Exception ex)
			{
				LogDebugMessage("WindGuru: ERROR - " + ex.Message);
			}
			finally
			{
				WindGuru.Updating = false;
			}
		}


		internal async void UpdateAwekas(DateTime timestamp)
		{
			if (AWEKAS.Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			AWEKAS.Updating = true;

			string pwstring;
			string url = station.GetAwekasURLv4(out pwstring, timestamp);

			string starredpwstring = "<password>";

			string logUrl = url.Replace(pwstring, starredpwstring);

			LogDebugMessage("AWEKAS: URL = " + logUrl);

			try
			{
				using (HttpResponseMessage response = await AwekashttpClient.GetAsync(url))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("AWEKAS Response code = " + response.StatusCode);
					LogDataMessage("AWEKAS: Response text = " + responseBodyAsText);
					//var respJson = JsonConvert.DeserializeObject<AwekasResponse>(responseBodyAsText);
					var respJson = JsonSerializer.DeserializeFromString<AwekasResponse>(responseBodyAsText);

					// Check the status response
					if (respJson.status == 2)
						LogDebugMessage("AWEKAS: Data stored OK");
					else if (respJson.status == 1)
					{
						LogMessage("AWEKAS: Data PARIALLY stored");
						// TODO: Check errors and disabled
					}
					else if (respJson.status == 0)  // Authenication error or rate limited
					{
						if (respJson.minuploadtime > 0 && respJson.authentication == 0)
						{
							LogMessage("AWEKAS: Authentication error");
							if (AWEKAS.Interval < 300)
							{
								AWEKAS.RateLimited = true;
								AWEKAS.OriginalInterval = AWEKAS.Interval;
								AWEKAS.Interval = 300;
								AwekasTimer.Enabled = false;
								AWEKAS.SynchronisedUpdate = true;
								LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 300 seconds due to authenication error");
							}
						}
						else if (respJson.minuploadtime == 0)
						{
							LogMessage("AWEKAS: Too many requests, rate limited");
							// AWEKAS PLus allows minimum of 60 second updates, try that first
							if (!AWEKAS.RateLimited && AWEKAS.Interval < 60)
							{
								AWEKAS.OriginalInterval = AWEKAS.Interval;
								AWEKAS.RateLimited = true;
								AWEKAS.Interval = 60;
								AwekasTimer.Enabled = false;
								AWEKAS.SynchronisedUpdate = true;
								LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 60 seconds due to rate limit");
							}
							// AWEKAS normal allows minimum of 300 second updates, revert to that
							else
							{
								AWEKAS.RateLimited = true;
								AWEKAS.Interval = 300;
								AwekasTimer.Interval = AWEKAS.Interval * 1000;
								AwekasTimer.Enabled = !AWEKAS.SynchronisedUpdate;
								AWEKAS.SynchronisedUpdate = AWEKAS.Interval % 60 == 0;
								LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 300 seconds due to rate limit");
							}
						}
						else
						{
							LogMessage("AWEKAS: Unknown error");
						}
					}

					// check the min upload time is greater than our upload time
					if (respJson.status > 0 && respJson.minuploadtime > AWEKAS.OriginalInterval)
					{
						LogMessage($"AWEKAS: The minimum upload time to AWEKAS for your station is {respJson.minuploadtime} sec, Cumulus is configured for {AWEKAS.OriginalInterval} sec, increasing Cumulus interval to match AWEKAS");
						AWEKAS.Interval = respJson.minuploadtime;
						WriteIniFile();
						AwekasTimer.Interval = AWEKAS.Interval * 1000;
						AWEKAS.SynchronisedUpdate = AWEKAS.Interval % 60 == 0;
						AwekasTimer.Enabled = !AWEKAS.SynchronisedUpdate;
						// we got a successful upload, and reset the interval, so clear the rate limited values
						AWEKAS.OriginalInterval = AWEKAS.Interval;
						AWEKAS.RateLimited = false;
					}
					else if (AWEKAS.RateLimited && respJson.status > 0)
					{
						// We are currently rate limited, it could have been a transient thing because
						// we just got a valid response, and our interval is >= the minimum allowed.
						// So we just undo the limit, and resume as before
						LogMessage($"AWEKAS: Removing temporary increase in upload interval to 60 secs, resuming uploads every {AWEKAS.OriginalInterval} secs");
						AWEKAS.Interval = AWEKAS.OriginalInterval;
						AwekasTimer.Interval = AWEKAS.Interval * 1000;
						AWEKAS.SynchronisedUpdate = AWEKAS.Interval % 60 == 0;
						AwekasTimer.Enabled = !AWEKAS.SynchronisedUpdate;
						AWEKAS.RateLimited = false;
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("AWEKAS: Exception = " + ex.Message);
			}
			finally
			{
				AWEKAS.Updating = false;
			}
		}

		internal async void UpdateWCloud(DateTime timestamp)
		{
			if (WCloud.Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			WCloud.Updating = true;

			string pwstring;
			string url = station.GetWCloudURL(out pwstring, timestamp);

			string starredpwstring = "<key>";

			string logUrl = url.Replace(pwstring, starredpwstring);

			LogDebugMessage("WeatherCloud: URL = " + logUrl);

			try
			{
				HttpResponseMessage response = await WCloudhttpClient.GetAsync(url);
				var responseBodyAsText = await response.Content.ReadAsStringAsync();
				var msg = "";
				switch ((int)response.StatusCode)
				{
					case 200:
						msg = "Success";
						break;
					case 400:
						msg = "Bad reuest";
						break;
					case 401:
						msg = "Incorrect WID or Key";
						break;
					case 429:
						msg = "Too many requests";
						break;
					case 500:
						msg = "Server error";
						break;
					default:
						msg = "Unknown error";
						break;
				}
				LogDebugMessage($"WeatherCloud: Response = {msg} ({response.StatusCode}): {responseBodyAsText}");
			}
			catch (Exception ex)
			{
				LogDebugMessage("WeatherCloud: ERROR - " + ex.Message);
			}
			finally
			{
				WCloud.Updating = false;
			}
		}

		internal async void UpdateOpenWeatherMap(DateTime timestamp)
		{
			if (OpenWeatherMap.Updating || station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			OpenWeatherMap.Updating = true;

			string url = "http://api.openweathermap.org/data/3.0/measurements?appid=" + OpenWeatherMap.PW;
			string logUrl = url.Replace(OpenWeatherMap.PW, "<key>");

			string jsonData = station.GetOpenWeatherMapData(timestamp);

			LogDebugMessage("OpenWeatherMap: URL = " + logUrl);
			LogDataMessage("OpenWeatherMap: Body = " + jsonData);

			try
			{
				using (var client = new HttpClient())
				{
					var data = new StringContent(jsonData, Encoding.UTF8, "application/json");
					HttpResponseMessage response = await client.PostAsync(url, data);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					var status = response.StatusCode == HttpStatusCode.NoContent ? "OK" : "Error";  // Returns a 204 reponse for OK!
					LogDebugMessage($"OpenWeatherMap: Response code = {status} - {response.StatusCode}");
					if (response.StatusCode != HttpStatusCode.NoContent)
						LogDataMessage($"OpenWeatherMap: Response data = {responseBodyAsText}");
				}
			}
			catch (Exception ex)
			{
				LogMessage("OpenWeatherMap: ERROR - " + ex.Message);
			}
			finally
			{
				OpenWeatherMap.Updating = false;
			}
		}

		// Find all stations associated with the users API key
		internal OpenWeatherMapStation[] GetOpenWeatherMapStations()
		{
			OpenWeatherMapStation[] retVal = new OpenWeatherMapStation[0];
			string url = "http://api.openweathermap.org/data/3.0/stations?appid=" + OpenWeatherMap.PW;
			try
			{
				using (var client = new HttpClient())
				{
					HttpResponseMessage response = client.GetAsync(url).Result;
					var responseBodyAsText = response.Content.ReadAsStringAsync().Result;
					LogDataMessage("OpenWeatherMap: Get Stations Response: " + response.StatusCode + ": " + responseBodyAsText);

					if (responseBodyAsText.Length > 10)
					{
						var respJson = JsonSerializer.DeserializeFromString<OpenWeatherMapStation[]>(responseBodyAsText);
						retVal = respJson;
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("OpenWeatherMap: Get Stations ERROR - " + ex.Message);
			}

			return retVal;
		}

		// Create a new OpenWeatherMap station
		internal void CreateOpenWeatherMapStation()
		{
			var invC = new CultureInfo("");

			string url = "http://api.openweathermap.org/data/3.0/stations?appid=" + OpenWeatherMap.PW;
			try
			{
				var datestr = DateTime.Now.ToUniversalTime().ToString("yyMMddHHmm");
				StringBuilder sb = new StringBuilder($"{{\"external_id\":\"CMX-{datestr}\",");
				sb.Append($"\"name\":\"{LocationName}\",");
				sb.Append($"\"latitude\":{Latitude.ToString(invC)},");
				sb.Append($"\"longitude\":{Longitude.ToString(invC)},");
				sb.Append($"\"altitude\":{(int)station.AltitudeM(Altitude)}}}");

				LogMessage($"OpenWeatherMap: Creating new station");
				LogMessage($"OpenWeatherMap: - {sb}");


				using (var client = new HttpClient())
				{
					var data = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");

					HttpResponseMessage response = client.PostAsync(url, data).Result;
					var responseBodyAsText = response.Content.ReadAsStringAsync().Result;
					var status = response.StatusCode == HttpStatusCode.Created ? "OK" : "Error";  // Returns a 201 reponse for OK
					LogDebugMessage($"OpenWeatherMap: Create station response code = {status} - {response.StatusCode}");
					LogDataMessage($"OpenWeatherMap: Create station response data = {responseBodyAsText}");

					if (response.StatusCode == HttpStatusCode.Created)
					{
						// It worked, save the result
						var respJson = JsonSerializer.DeserializeFromString<OpenWeatherMapNewStation>(responseBodyAsText);

						LogMessage($"OpenWeatherMap: Created new station, id = {respJson.ID}, name = {respJson.name}");
						OpenWeatherMap.ID = respJson.ID;
						WriteIniFile();
					}
					else
					{
						LogMessage($"OpenWeatherMap: Failed to create new station. Error - {response.StatusCode}, text - {responseBodyAsText}");
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("OpenWeatherMap: Create station ERROR - " + ex.Message);
			}
		}

		internal void EnableOpenWeatherMap()
		{
			if (OpenWeatherMap.Enabled && string.IsNullOrWhiteSpace(OpenWeatherMap.ID))
			{
				// oh, oh! OpenWeatherMap is enabled, but we do not have a station id
				// first check if one already exists
				var stations = GetOpenWeatherMapStations();

				if (stations.Length == 0)
				{
					// No stations defined, we will create one
					LogMessage($"OpenWeatherMap: No station defined, attempting to create one");
					CreateOpenWeatherMapStation();
				}
				else if (stations.Length == 1)
				{
					// We have one station defined, lets use it!
					LogMessage($"OpenWeatherMap: No station defined, but found one associated with this API key, using this station - {stations[0].id} : {stations[0].name}");
					OpenWeatherMap.ID = stations[0].id;
					// save the setting
					WriteIniFile();
				}
				else
				{
					// multiple stations defined, the user must select which one to use
					var msg = $"Multiple OpenWeatherMap stations found, please select the correct station id and enter it into your configuration";
					LogConsoleMessage(msg);
					LogMessage("OpenWeatherMap: " + msg);
					foreach (var station in stations)
					{
						msg = $"  Station Id = {station.id}, Name = {station.name}";
						LogConsoleMessage(msg);
						LogMessage("OpenWeatherMap: " + msg);
					}
				}
			}
		}

		internal void RealtimeTimerTick(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			bool connectionFailed = false;
			var cycle = RealtimeCycleCounter++;

			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			LogDebugMessage($"Realtime[{cycle}]: Start cycle");
			try
			{
				// Process any files
				if (RealtimeCopyInProgress)
				{
					LogMessage($"Realtime[{cycle}]: Warning, a previous cycle is still processing local files. Skipping this interval.");
				}
				else
				{
					RealtimeCopyInProgress = true;
					CreateRealtimeFile(cycle);
					CreateRealtimeHTMLfiles(cycle);
					RealtimeCopyInProgress = false;

					if (RealtimeFTPEnabled && !string.IsNullOrWhiteSpace(FtpHostname))
					{
						// Is a previous cycle still running?
						if (RealtimeFtpInProgress)
						{
							LogMessage($"Realtime[{cycle}]: Warning, a previous cycle is still trying to connect to FTP server, skip count = {++realtimeFTPRetries}");
							// realtimeinvertval is in ms, if a session has been uploading for 5 minutes - abort it and reconnect
							if (realtimeFTPRetries * RealtimeInterval / 1000 > 5 * 60)
							{
								LogMessage($"Realtime[{cycle}]: Realtime has been in progress for more than 5 minutes, attempting to reconnect.");
								RealtimeFTPConnectionTest(cycle);
							}
							else
							{
								LogMessage($"Realtime[{cycle}]: No FTP attempted this cycle");
							}
						}
						else
						{
							RealtimeFtpInProgress = true;

							// This only happens if the user enables realtime FTP after starting Cumulus
							if (Sslftp == FtpProtocols.SFTP)
							{
								if (RealtimeSSH == null || !RealtimeSSH.ConnectionInfo.IsAuthenticated)
								{
									RealtimeSSHLogin(cycle);
								}
							}
							else
							{
								if (!RealtimeFTP.IsConnected)
								{
									RealtimeFTPLogin(cycle);
								}
							}
							// Force a test of the connection, IsConnected is not always reliable
							try
							{
								string pwd;
								if (Sslftp == FtpProtocols.SFTP)
								{
									pwd = RealtimeSSH.WorkingDirectory;
									// Double check
									if (!RealtimeSSH.IsConnected)
									{
										connectionFailed = true;
									}
								}
								else
								{
									pwd = RealtimeFTP.GetWorkingDirectory();
									// Double check
									if (!RealtimeFTP.IsConnected)
									{
										connectionFailed = true;
									}
								}
								if (pwd.Length == 0)
								{
									connectionFailed = true;
								}
							}
							catch (Exception ex)
							{
								LogDebugMessage($"Realtime[{cycle}]: Test of FTP connection failed: {ex.Message}");
								connectionFailed = true;
							}

							if (connectionFailed)
							{
								RealtimeFTPConnectionTest(cycle);
							}
							else
							{
								realtimeFTPRetries = 0;
							}

							try
							{
								RealtimeFTPUpload(cycle);
							}
							catch (Exception ex)
							{
								LogMessage($"Realtime[{cycle}]: Error during realtime FTP update: {ex.Message}");
								RealtimeFTPConnectionTest(cycle);
							}
							RealtimeFtpInProgress = false;
						}
					}

					if (!string.IsNullOrEmpty(RealtimeProgram))
					{
						LogDebugMessage($"Realtime[{cycle}]: Execute realtime program - {RealtimeProgram}");
						ExecuteProgram(RealtimeProgram, RealtimeParams);
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage($"Realtime[{cycle}]: Error during update: {ex.Message}");
				if (ex is NullReferenceException)
				{
					// If we haven't initialised the object (eg. user enables realtime FTP after starting Cumulus)
					// then start from the beginning
					if (Sslftp == FtpProtocols.SFTP)
					{
						RealtimeSSHLogin(cycle);
					}
					else
					{
						RealtimeFTPLogin(cycle);
					}
				}
				else
				{
					RealtimeFTPConnectionTest(cycle);
				}
				RealtimeFtpInProgress = false;
			}
			LogDebugMessage($"Realtime[{cycle}]: End cycle");
		}

		private void RealtimeFTPConnectionTest(uint cycle)
		{
			LogDebugMessage($"Realtime[{cycle}]: Realtime ftp attempting disconnect");
			try
			{
				if (Sslftp == FtpProtocols.SFTP && RealtimeSSH != null)
				{
					RealtimeSSH.Disconnect();
				}
				else
				{
					RealtimeFTP.Disconnect();
				}
				LogDebugMessage($"Realtime[{cycle}]: Realtime ftp disconnected OK");
			}
			catch(Exception ex)
			{
				LogDebugMessage($"Realtime[{cycle}]: Error disconnecting from server - " + ex.Message);
			}

			LogDebugMessage($"Realtime[{cycle}]: Realtime ftp attempting to reconnect");
			try
			{
				if (Sslftp == FtpProtocols.SFTP && RealtimeSSH != null)
				{
					RealtimeSSH.Connect();
				}
				else
				{
					RealtimeFTP.Connect();
				}
				LogMessage($"Realtime[{cycle}]: Reconnected with server OK");
			}
			catch (Exception ex)
			{
				LogMessage($"Realtime[{cycle}]: Error reconnecting ftp server - " + ex.Message);
				LogDebugMessage($"Realtime[{cycle}]: Realtime ftp attempting to reinitialise the connection");
				if (Sslftp == FtpProtocols.SFTP)
				{
					RealtimeSSHLogin(cycle);
				}
				else
				{
					RealtimeFTPLogin(cycle);
				}
			}
			if (Sslftp == FtpProtocols.SFTP && RealtimeSSH == null)
			{
				LogDebugMessage($"Realtime[{cycle}]: Realtime ftp attempting to reinitialise the connection");
				RealtimeSSHLogin(cycle);
			}
		}

		private void RealtimeFTPUpload(byte cycle)
		{
			var remotePath = "";

			if (FtpDirectory.Length > 0)
			{
				remotePath = (FtpDirectory.EndsWith("/") ? FtpDirectory : FtpDirectory + "/");
			}

			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].Create && RealtimeFiles[i].FTP)
				{
					var remoteFile = remotePath + RealtimeFiles[i].RemoteFileName;
					var localFile = RealtimeFiles[i].LocalPath + RealtimeFiles[i].LocalFileName;

					LogFtpMessage($"Realtime[{cycle}]: Uploading - {RealtimeFiles[i].LocalFileName}");
					if (Sslftp == FtpProtocols.SFTP)
					{
						UploadFile(RealtimeSSH, localFile, remoteFile, cycle);
					}
					else
					{
						UploadFile(RealtimeFTP, localFile, remoteFile, cycle);
					}

				}
			}

			// Extra files
			for (int i = 0; i < numextrafiles; i++)
			{
				var uploadfile = ExtraFiles[i].local;
				var remotefile = ExtraFiles[i].remote;

				if ((uploadfile.Length > 0) && (remotefile.Length > 0) && ExtraFiles[i].realtime && ExtraFiles[i].FTP)
				{
					if (uploadfile == "<currentlogfile>")
					{
						uploadfile = GetLogFileName(DateTime.Now);
					}
					else if (uploadfile == "<currentextralogfile>")
					{
						uploadfile = GetExtraLogFileName(DateTime.Now);
					}
					else if (uploadfile == "<airlinklogfile")
					{
						uploadfile = GetAirLinkLogFileName(DateTime.Now);
					}

					if (File.Exists(uploadfile))
					{
						if (remotefile.Contains("<currentlogfile>"))
						{
							remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));
						}
						else if (remotefile.Contains("<currentextralogfile>"))
						{
							remotefile = remotefile.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(DateTime.Now)));
						}
						else if (remotefile.Contains("<airlinklogfile"))
						{
							remotefile = remotefile.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(DateTime.Now)));
						}

						// all checks OK, file needs to be uploaded
						if (ExtraFiles[i].process)
						{
							// we've already processed the file
							uploadfile += "tmp";
						}
						LogFtpMessage($"Realtime[{cycle}]: Uploading extra web file[{i}] {uploadfile} to {remotefile}");
						if (Sslftp == FtpProtocols.SFTP)
						{
							UploadFile(RealtimeSSH, uploadfile, remotefile, cycle);
						}
						else
						{
							UploadFile(RealtimeFTP, uploadfile, remotefile, cycle);
						}
					}
					else
					{
						LogMessage($"Realtime[{cycle}]: Warning, extra web file[{i}] not found! - {uploadfile}");
					}
				}
			}
		}

		private void CreateRealtimeHTMLfiles(int cycle)
		{
			// Process realtime files
			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].Create && !string.IsNullOrWhiteSpace(RealtimeFiles[i].TemplateFileName))
				{
					LogDebugMessage($"Realtime[{cycle}]: Processing realtime file - {RealtimeFiles[i].LocalFileName}");
					var destFile = RealtimeFiles[i].LocalPath + RealtimeFiles[i].LocalFileName;
					ProcessTemplateFile(RealtimeFiles[i].TemplateFileName, destFile, realtimeTokenParser);
				}
			}

			for (int i = 0; i < numextrafiles; i++)
			{
				if (ExtraFiles[i].realtime)
				{
					var uploadfile = ExtraFiles[i].local;
					var remotefile = ExtraFiles[i].remote;

					if ((uploadfile.Length > 0) && (remotefile.Length > 0))
					{
						if (uploadfile == "<currentlogfile>")
						{
							uploadfile = GetLogFileName(DateTime.Now);
						}
						else if (uploadfile == "<currentextralogfile>")
						{
							uploadfile = GetExtraLogFileName(DateTime.Now);
						}
						else if (uploadfile == "<airlinklogfile")
						{
							uploadfile = GetAirLinkLogFileName(DateTime.Now);
						}

						if (File.Exists(uploadfile))
						{
							if (remotefile.Contains("<currentlogfile>"))
							{
								remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));
							}
							else if (remotefile.Contains("<currentextralogfile>"))
							{
								remotefile = remotefile.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(DateTime.Now)));
							}
							else if (remotefile.Contains("<airlinklogfile"))
							{
								remotefile = remotefile.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(DateTime.Now)));
							}

							if (ExtraFiles[i].process)
							{
								// process the file
								LogDebugMessage($"Realtime[{cycle}]: Processing extra file[{i}] - {uploadfile}");
								var utf8WithoutBom = new UTF8Encoding(false);
								var encoding = UTF8encode ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
								realtimeTokenParser.Encoding = encoding;
								realtimeTokenParser.SourceFile = uploadfile;
								var output = realtimeTokenParser.ToString();
								uploadfile += "tmp";
								try
								{
									using (StreamWriter file = new StreamWriter(uploadfile, false, encoding))
									{
										file.Write(output);
										file.Close();
									}
								}
								catch (Exception ex)
								{
									LogMessage($"Realtime[{cycle}]: Error writing to extra realtime file[{i}] - {uploadfile}: {ex.Message}");
								}
							}

							if (!ExtraFiles[i].FTP)
							{
								// just copy the file
								try
								{
									LogDebugMessage($"Realtime[{cycle}]: Copying extra file[{i}] {uploadfile} to {remotefile}");
									File.Copy(uploadfile, remotefile, true);
								}
								catch (Exception ex)
								{
									LogDebugMessage($"Realtime[{cycle}]: Error copying extra realtime file[{i}] - {uploadfile}: {ex.Message}");
								}
							}
						}
						else
						{
							LogMessage($"Realtime[{cycle}]: Extra realtime web file[{i}] not found - {uploadfile}");
						}

					}
				}
			}
		}

		public void TokenParserOnToken(string strToken, ref string strReplacement)
		{
			var tagParams = new Dictionary<string, string>();
			var paramList = ParseParams(strToken);
			var webTag = paramList[0];

			tagParams.Add("webtag", webTag);
			for (int i = 1; i < paramList.Count; i += 2)
			{
				// odd numbered entries are keys with "=" on the end - remove that
				string key = paramList[i].Remove(paramList[i].Length - 1);
				// even numbered entries are values
				string value = paramList[i + 1];
				tagParams.Add(key, value);
			}

			strReplacement = webtags.GetWebTagText(webTag, tagParams);
		}

		private List<string> ParseParams(string line)
		{
			var insideQuotes = false;
			var start = -1;

			var parts = new List<string>();

			for (var i = 0; i < line.Length; i++)
			{
				if (char.IsWhiteSpace(line[i]))
				{
					if (!insideQuotes && start != -1)
					{
						parts.Add(line.Substring(start, i - start));
						start = -1;
					}
				}
				else if (line[i] == '"')
				{
					if (start != -1)
					{
						parts.Add(line.Substring(start, i - start));
						start = -1;
					}
					insideQuotes = !insideQuotes;
				}
				else if (line[i] == '=')
				{
					if (!insideQuotes)
					{
						if (start != -1)
						{
							parts.Add(line.Substring(start, (i - start) + 1));
							start = -1;
						}
					}
				}
				else
				{
					if (start == -1)
						start = i;
				}
			}

			if (start != -1)
				parts.Add(line.Substring(start));

			return parts;
		}

		public string DecimalSeparator { get; set; }

		[DllImport("libc")]
		private static extern int uname(IntPtr buf);

		private static bool IsRunningOnMac()
		{
			IntPtr buf = IntPtr.Zero;
			try
			{
				buf = Marshal.AllocHGlobal(8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname(buf) == 0)
				{
					string os = Marshal.PtrToStringAnsi(buf);
					if (os == "Darwin")
						return true;
				}
			}
			catch
			{
			}
			finally
			{
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal(buf);
			}
			return false;
		}

		internal void DoMoonPhase()
		{
			DateTime now = DateTime.Now;
			double[] moonriseset = MoonriseMoonset.MoonRise(now.Year, now.Month, now.Day, TimeZone.CurrentTimeZone.GetUtcOffset(now).TotalHours, Latitude, Longitude);
			MoonRiseTime = TimeSpan.FromHours(moonriseset[0]);
			MoonSetTime = TimeSpan.FromHours(moonriseset[1]);

			DateTime utcNow = DateTime.UtcNow;
			MoonPhaseAngle = MoonriseMoonset.MoonPhase(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour);
			MoonPercent = (100.0 * (1.0 + Math.Cos(MoonPhaseAngle * Math.PI / 180)) / 2.0);

			// If between full moon and new moon, angle is between 180 and 360, make percent negative to indicate waning
			if (MoonPhaseAngle > 180)
			{
				MoonPercent = -MoonPercent;
			}
			/*
			// New   = -0.4 -> 0.4
			// 1st Q = 45 -> 55
			// Full  = 99.6 -> -99.6
			// 3rd Q = -45 -> -55
			if ((MoonPercent > 0.4) && (MoonPercent < 45))
				MoonPhaseString = WaxingCrescent;
			else if ((MoonPercent >= 45) && (MoonPercent <= 55))
				MoonPhaseString = FirstQuarter;
			else if ((MoonPercent > 55) && (MoonPercent < 99.6))
				MoonPhaseString = WaxingGibbous;
			else if ((MoonPercent >= 99.6) || (MoonPercent <= -99.6))
				MoonPhaseString = Fullmoon;
			else if ((MoonPercent < -55) && (MoonPercent > -99.6))
				MoonPhaseString = WaningGibbous;
			else if ((MoonPercent <= -45) && (MoonPercent >= -55))
				MoonPhaseString = LastQuarter;
			else if ((MoonPercent > -45) && (MoonPercent < -0.4))
				MoonPhaseString = WaningCrescent;
			else
				MoonPhaseString = Newmoon;
			*/

			// Use Phase Angle to determine string - it's linear unlike Illuminated Percentage
			// New  = 186 - 180 - 174
			// 1st  =  96 -  90 -  84
			// Full =   6 -   0 - 354
			// 3rd  = 276 - 270 - 264
			if (MoonPhaseAngle < 174 && MoonPhaseAngle > 96)
				MoonPhaseString = WaxingCrescent;
			else if (MoonPhaseAngle <= 96 && MoonPhaseAngle >= 84)
				MoonPhaseString = FirstQuarter;
			else if (MoonPhaseAngle < 84 && MoonPhaseAngle > 6)
				MoonPhaseString = WaxingGibbous;
			else if (MoonPhaseAngle <= 6 || MoonPhaseAngle >= 354)
				MoonPhaseString = Fullmoon;
			else if (MoonPhaseAngle < 354 && MoonPhaseAngle > 276)
				MoonPhaseString = WaningGibbous;
			else if (MoonPhaseAngle <= 276 && MoonPhaseAngle >= 264)
				MoonPhaseString = LastQuarter;
			else if (MoonPhaseAngle < 264 && MoonPhaseAngle > 186)
				MoonPhaseString = WaningCrescent;
			else
				MoonPhaseString = Newmoon;
		}

		internal void DoMoonImage()
		{
			if (MoonImageEnabled)
			{
				LogDebugMessage("Generating new Moon image");
				var ret = MoonriseMoonset.CreateMoonImage(MoonPhaseAngle, Latitude, MoonImageSize);

				if (ret == "OK")
				{
					// set a flag to show file is ready for FTP
					MoonImageReady = true;
				}
				else
				{
					LogMessage(ret);
				}
			}
		}


		/*
		private string GetMoonStage(double fAge)
		{
			string sStage;

			if (fAge < 1.84566)
			{
				sStage = Newmoon;
			}
			else if (fAge < 5.53699)
			{
				sStage = WaxingCrescent;
			}
			else if (fAge < 9.22831)
			{
				sStage = FirstQuarter;
			}
			else if (fAge < 12.91963)
			{
				sStage = WaxingGibbous;
			}
			else if (fAge < 16.61096)
			{
				sStage = Fullmoon;
			}
			else if (fAge < 20.30228)
			{
				sStage = WaningGibbous;
			}
			else if (fAge < 23.9931)
			{
				sStage = LastQuarter;
			}
			else if (fAge < 27.68493)
			{
				sStage = WaningCrescent;
			}
			else
			{
				sStage = Newmoon;
			}

			return sStage;
		}
		*/

		public double MoonAge { get; set; }

		public string MoonPhaseString { get; set; }

		public double MoonPhaseAngle { get; set; }

		public double MoonPercent { get; set; }

		public TimeSpan MoonSetTime { get; set; }

		public TimeSpan MoonRiseTime { get; set; }

		public bool MoonImageEnabled;

		public int MoonImageSize;

		public string MoonImageFtpDest;

		private bool MoonImageReady;

		private void GetSunriseSunset(DateTime time, out DateTime sunrise, out DateTime sunset, out bool alwaysUp, out bool alwaysDown)
		{
			string rise = SunriseSunset.SunRise(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			string set = SunriseSunset.SunSet(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);

			if (rise.Equals("Always Down") || set.Equals("Always Down"))
			{
				alwaysDown = true;
				alwaysUp = false;
				sunrise = DateTime.MinValue;
				sunset = DateTime.MinValue;
			}
			else if (rise.Equals("Always Up") || set.Equals("Always Up"))
			{
				alwaysDown = false;
				alwaysUp = true;
				sunrise = DateTime.MinValue;
				sunset = DateTime.MinValue;
			}
			else
			{
				alwaysDown = false;
				alwaysUp = false;
				try
				{
					int h = Convert.ToInt32(rise.Substring(0, 2));
					int m = Convert.ToInt32(rise.Substring(2, 2));
					int s = Convert.ToInt32(rise.Substring(4, 2));
					sunrise = DateTime.Now.Date.Add(new TimeSpan(h, m, s));
				}
				catch (Exception)
				{
					sunrise = DateTime.MinValue;
				}

				try
				{
					int h = Convert.ToInt32(set.Substring(0, 2));
					int m = Convert.ToInt32(set.Substring(2, 2));
					int s = Convert.ToInt32(set.Substring(4, 2));
					sunset = DateTime.Now.Date.Add(new TimeSpan(h, m, s));
				}
				catch (Exception)
				{
					sunset = DateTime.MinValue;
				}
			}
		}

		/*
		private DateTime getSunriseTime(DateTime time)
		{
			string rise = SunriseSunset.sunrise(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			//LogMessage("Sunrise: " + rise);
			int h = Convert.ToInt32(rise.Substring(0, 2));
			int m = Convert.ToInt32(rise.Substring(2, 2));
			int s = Convert.ToInt32(rise.Substring(4, 2));
			return DateTime.Now.Date.Add(new TimeSpan(h, m, s));
		}
		*/

		/*
		private DateTime getSunsetTime(DateTime time)
		{
			string rise = SunriseSunset.sunset(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			//LogMessage("Sunrise: " + rise);
			int h = Convert.ToInt32(rise.Substring(0, 2));
			int m = Convert.ToInt32(rise.Substring(2, 2));
			int s = Convert.ToInt32(rise.Substring(4, 2));
			return DateTime.Now.Date.Add(new TimeSpan(h, m, s));
		}
		*/

		private void GetDawnDusk(DateTime time, out DateTime dawn, out DateTime dusk, out bool alwaysUp, out bool alwaysDown)
		{
			string dawnStr = SunriseSunset.CivilTwilightEnds(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			string duskStr = SunriseSunset.CivilTwilightStarts(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);

			if (dawnStr.Equals("Always Down") || duskStr.Equals("Always Down"))
			{
				alwaysDown = true;
				alwaysUp = false;
				dawn = DateTime.MinValue;
				dusk = DateTime.MinValue;
			}
			else if (dawnStr.Equals("Always Up") || duskStr.Equals("Always Up"))
			{
				alwaysDown = false;
				alwaysUp = true;
				dawn = DateTime.MinValue;
				dusk = DateTime.MinValue;
			}
			else
			{
				alwaysDown = false;
				alwaysUp = false;
				try
				{
					int h = Convert.ToInt32(dawnStr.Substring(0, 2));
					int m = Convert.ToInt32(dawnStr.Substring(2, 2));
					int s = Convert.ToInt32(dawnStr.Substring(4, 2));
					dawn = DateTime.Now.Date.Add(new TimeSpan(h, m, s));
				}
				catch (Exception)
				{
					dawn = DateTime.MinValue;
				}

				try
				{
					int h = Convert.ToInt32(duskStr.Substring(0, 2));
					int m = Convert.ToInt32(duskStr.Substring(2, 2));
					int s = Convert.ToInt32(duskStr.Substring(4, 2));
					dusk = DateTime.Now.Date.Add(new TimeSpan(h, m, s));
				}
				catch (Exception)
				{
					dusk = DateTime.MinValue;
				}
			}
		}

		/*
		private DateTime getDawnTime(DateTime time)
		{
			string rise = SunriseSunset.CivilTwilightEnds(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			//LogMessage("Sunrise: " + rise);
			try
			{
				int h = Convert.ToInt32(rise.Substring(0, 2));
				int m = Convert.ToInt32(rise.Substring(2, 2));
				int s = Convert.ToInt32(rise.Substring(4, 2));
				return DateTime.Now.Date.Add(new TimeSpan(h, m, s));
			}
			catch (Exception)
			{
				return DateTime.Now.Date;
			}
		}
		*/

		/*
		private DateTime getDuskTime(DateTime time)
		{
			string rise = SunriseSunset.CivilTwilightStarts(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			//LogMessage("Sunrise: " + rise);
			try
			{
				int h = Convert.ToInt32(rise.Substring(0, 2));
				int m = Convert.ToInt32(rise.Substring(2, 2));
				int s = Convert.ToInt32(rise.Substring(4, 2));
				return DateTime.Now.Date.Add(new TimeSpan(h, m, s));
			}
			catch (Exception)
			{
				return DateTime.Now.Date;
			}
		}
		*/

		internal void DoSunriseAndSunset()
		{
			LogMessage("Calculating sunrise and sunset times");
			DateTime now = DateTime.Now;
			DateTime tomorrow = now.AddDays(1);
			GetSunriseSunset(now, out SunRiseTime, out SunSetTime, out SunAlwaysUp, out SunAlwaysDown);

			if (SunAlwaysUp)
			{
				LogMessage("Sun always up");
				DayLength = new TimeSpan(24, 0, 0);
			}
			else if (SunAlwaysDown)
			{
				LogMessage("Sun always down");
				DayLength = new TimeSpan(0, 0, 0);
			}
			else
			{
				LogMessage("Sunrise: " + SunRiseTime.ToString("HH:mm:ss"));
				LogMessage("Sunset : " + SunSetTime.ToString("HH:mm:ss"));
				if (SunRiseTime == DateTime.MinValue)
				{
					DayLength = SunSetTime - DateTime.Now.Date;
				}
				else if (SunSetTime == DateTime.MinValue)
				{
					DayLength = DateTime.Now.Date.AddDays(1) - SunRiseTime;
				}
				else if (SunSetTime > SunRiseTime)
				{
					DayLength = SunSetTime - SunRiseTime;
				}
				else
				{
					DayLength = new TimeSpan(24, 0, 0) - (SunRiseTime - SunSetTime);
				}
			}

			DateTime tomorrowSunRiseTime;
			DateTime tomorrowSunSetTime;
			TimeSpan tomorrowDayLength;
			bool tomorrowSunAlwaysUp;
			bool tomorrowSunAlwaysDown;

			GetSunriseSunset(tomorrow, out tomorrowSunRiseTime, out tomorrowSunSetTime, out tomorrowSunAlwaysUp, out tomorrowSunAlwaysDown);

			if (tomorrowSunAlwaysUp)
			{
				LogMessage("Tomorrow sun always up");
				tomorrowDayLength = new TimeSpan(24, 0, 0);
			}
			else if (tomorrowSunAlwaysDown)
			{
				LogMessage("Tomorrow sun always down");
				tomorrowDayLength = new TimeSpan(0, 0, 0);
			}
			else
			{
				LogMessage("Tomorrow sunrise: " + tomorrowSunRiseTime.ToString("HH:mm:ss"));
				LogMessage("Tomorrow sunset : " + tomorrowSunSetTime.ToString("HH:mm:ss"));
				tomorrowDayLength = tomorrowSunSetTime - tomorrowSunRiseTime;
			}

			int tomorrowdiff = Convert.ToInt32(tomorrowDayLength.TotalSeconds - DayLength.TotalSeconds);
			LogDebugMessage("Tomorrow length diff: " + tomorrowdiff);

			bool tomorrowminus;

			if (tomorrowdiff < 0)
			{
				tomorrowminus = true;
				tomorrowdiff = -tomorrowdiff;
			}
			else
			{
				tomorrowminus = false;
			}

			int tomorrowmins = tomorrowdiff / 60;
			int tomorrowsecs = tomorrowdiff % 60;

			if (tomorrowminus)
			{
				try
				{
					TomorrowDayLengthText = string.Format(thereWillBeMinSLessDaylightTomorrow, tomorrowmins, tomorrowsecs);
				}
				catch (Exception)
				{
					TomorrowDayLengthText = "Error in LessDaylightTomorrow format string";
				}
			}
			else
			{
				try
				{
					TomorrowDayLengthText = string.Format(thereWillBeMinSMoreDaylightTomorrow, tomorrowmins, tomorrowsecs);
				}
				catch (Exception)
				{
					TomorrowDayLengthText = "Error in MoreDaylightTomorrow format string";
				}
			}

			GetDawnDusk(now, out Dawn, out Dusk, out TwilightAlways, out TwilightNever);

			if (TwilightAlways)
			{
				DaylightLength = new TimeSpan(24, 0, 0);
			}
			else if (TwilightNever)
			{
				DaylightLength = new TimeSpan(0, 0, 0);
			}
			else
			{
				if (Dawn == DateTime.MinValue)
				{
					DaylightLength = Dusk - DateTime.Now.Date;
				}
				else if (Dusk == DateTime.MinValue)
				{
					DaylightLength = DateTime.Now.Date.AddDays(1) - Dawn;
				}
				else if (Dusk > Dawn)
				{
					DaylightLength = Dusk - Dawn;
				}
				else
				{
					DaylightLength = new TimeSpan(24, 0, 0) - (Dawn - Dusk);
				}
			}
		}

		public DateTime SunSetTime;

		public DateTime SunRiseTime;

		internal bool SunAlwaysUp;
		internal bool SunAlwaysDown;

		internal bool TwilightAlways;
		internal bool TwilightNever;

		public string TomorrowDayLengthText { get; set; }

		public bool IsDaylight()
		{
			if (TwilightAlways)
			{
				return true;
			}
			if (TwilightNever)
			{
				return false;
			}
			if (Dusk > Dawn)
			{
				// 'Normal' case where sun sets before midnight
				return (DateTime.Now >= Dawn) && (DateTime.Now <= Dusk);
			}
			else
			{
				return !((DateTime.Now >= Dusk) && (DateTime.Now <= Dawn));
			}
		}

		public bool IsSunUp()
		{
			if (SunAlwaysUp)
			{
				return true;
			}
			if (SunAlwaysDown)
			{
				return false;
			}
			if (SunSetTime > SunRiseTime)
			{
				// 'Normal' case where sun sets before midnight
				return (DateTime.Now >= SunRiseTime) && (DateTime.Now <= SunSetTime);
			}
			else
			{
				return !((DateTime.Now >= SunSetTime) && (DateTime.Now <= SunRiseTime));
			}
		}

		private string GetLoggingFileName(string directory)
		{
			const int maxEntries = 15;

			List<string> fileEntries = new List<string>(Directory.GetFiles(directory));

			fileEntries.Sort();

			while (fileEntries.Count >= maxEntries)
			{
				File.Delete(fileEntries.First());
				fileEntries.RemoveAt(0);
			}

			return $"{directory}{DateTime.Now:yyyyMMdd-HHmmss}.txt";
		}

		public void RotateLogFiles()
		{
			// cycle the MXdiags log file?
			var logfileSize = new FileInfo(loggingfile).Length;
			// if > 20 MB
			if (logfileSize > 20971520)
			{
				var oldfile = loggingfile;
				loggingfile = GetLoggingFileName("MXdiags" + DirectorySeparator);
				LogMessage("Rotating log file, new log file will be: " + loggingfile.Split(DirectorySeparator).Last());
				TextWriterTraceListener myTextListener = new TextWriterTraceListener(loggingfile, "MXlog");
				Trace.Listeners.Remove("MXlog");
				Trace.Listeners.Add(myTextListener);
				LogMessage("Rotated log file, old log file was: " + oldfile.Split(DirectorySeparator).Last());
			}
		}

		private void ReadIniFile()
		{
			var DavisBaudRates = new List<int> { 1200, 2400, 4800, 9600, 14400, 19200 };
			ImetOptions.BaudRates = new List<int> { 19200, 115200 };

			LogMessage("Reading Cumulus.ini file");
			//DateTimeToString(LongDate, "ddddd", Now);

			IniFile ini = new IniFile("Cumulus.ini");

			// check for Cumulus 1 [FTP Site] and correct it
			if (ini.GetValue("FTP Site", "Port", -999) != -999)
			{
				if (File.Exists("Cumulus.ini"))
				{
					var contents = File.ReadAllText("Cumulus.ini");
					contents = contents.Replace("[FTP Site]", "[FTP site]");
					File.WriteAllText("Cumulus.ini", contents);
					ini.Refresh();
				}
			}

			ProgramOptions.EnableAccessibility = ini.GetValue("Program", "EnableAccessibility", false);
			ProgramOptions.StartupPingHost = ini.GetValue("Program", "StartupPingHost", "");
			ProgramOptions.StartupPingEscapeTime = ini.GetValue("Program", "StartupPingEscapeTime", 999);
			ProgramOptions.StartupDelaySecs = ini.GetValue("Program", "StartupDelaySecs", 0);
			ProgramOptions.StartupDelayMaxUptime = ini.GetValue("Program", "StartupDelayMaxUptime", 300);
			ProgramOptions.WarnMultiple = ini.GetValue("Station", "WarnMultiple", true);
			SmtpOptions.Logging = ini.GetValue("SMTP", "Logging", false);
			if (DebuggingEnabled)
			{
				ProgramOptions.DebugLogging = true;
				ProgramOptions.DataLogging = true;
			}
			else
			{
				ProgramOptions.DebugLogging = ini.GetValue("Station", "Logging", false);
				ProgramOptions.DataLogging = ini.GetValue("Station", "DataLogging", false);
			}

			ComportName = ini.GetValue("Station", "ComportName", DefaultComportName);

			StationType = ini.GetValue("Station", "Type", -1);
			StationModel = ini.GetValue("Station", "Model", "");

			FineOffsetStation = (StationType == StationTypes.FineOffset || StationType == StationTypes.FineOffsetSolar);
			DavisStation = (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2);

			// Davis Options
			DavisOptions.UseLoop2 = ini.GetValue("Station", "UseDavisLoop2", true);
			DavisOptions.ReadReceptionStats = ini.GetValue("Station", "DavisReadReceptionStats", true);
			DavisOptions.SetLoggerInterval = ini.GetValue("Station", "DavisSetLoggerInterval", false);
			DavisOptions.InitWaitTime = ini.GetValue("Station", "DavisInitWaitTime", 2000);
			DavisOptions.IPResponseTime = ini.GetValue("Station", "DavisIPResponseTime", 500);
			//StationOptions.DavisReadTimeout = ini.GetValue("Station", "DavisReadTimeout", 1000); // Not currently used
			DavisOptions.IncrementPressureDP = ini.GetValue("Station", "DavisIncrementPressureDP", false);
			if (StationType == StationTypes.VantagePro)
			{
				DavisOptions.UseLoop2 = false;
			}
			DavisOptions.BaudRate = ini.GetValue("Station", "DavisBaudRate", 19200);
			// Check we have a valid value
			if (!DavisBaudRates.Contains(DavisOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Error, the value for DavisBaudRate in the ini file " + DavisOptions.BaudRate + " is not valid, using default 19200.");
				DavisOptions.BaudRate = 19200;
			}
			DavisOptions.ForceVPBarUpdate = ini.GetValue("Station", "ForceVPBarUpdate", false);
			//DavisUseDLLBarCalData = ini.GetValue("Station", "DavisUseDLLBarCalData", false);
			//DavisCalcAltPress = ini.GetValue("Station", "DavisCalcAltPress", true);
			//DavisConsoleHighGust = ini.GetValue("Station", "DavisConsoleHighGust", false);
			DavisOptions.RainGaugeType = ini.GetValue("Station", "VPrainGaugeType", -1);
			if (DavisOptions.RainGaugeType > 3)
			{
				DavisOptions.RainGaugeType = -1;
			}
			DavisOptions.ConnectionType = ini.GetValue("Station", "VP2ConnectionType", VP2SERIALCONNECTION);
			DavisOptions.TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222);
			DavisOptions.IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");
			//VPClosedownTime = ini.GetValue("Station", "VPClosedownTime", 99999999);
			//VP2SleepInterval = ini.GetValue("Station", "VP2SleepInterval", 0);
			DavisOptions.PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0);

			Latitude = ini.GetValue("Station", "Latitude", 0.0);
			if (Latitude > 90 || Latitude < -90)
			{
				Latitude = 0;
				LogMessage($"Error, invalid latitude value in Cumulus.ini [{Latitude}], defaulting to zero.");
			}
			Longitude = ini.GetValue("Station", "Longitude", 0.0);
			if (Longitude > 180 || Longitude < -180)
			{
				Longitude = 0;
				LogMessage($"Error, invalid longitude value in Cumulus.ini [{Longitude}], defaulting to zero.");
			}

			LatTxt = ini.GetValue("Station", "LatTxt", "");
			LatTxt = LatTxt.Replace(" ", "&nbsp;");
			LatTxt = LatTxt.Replace("°", "&#39;");
			LonTxt = ini.GetValue("Station", "LonTxt", "");
			LonTxt = LonTxt.Replace(" ", "&nbsp;");
			LonTxt = LonTxt.Replace("°", "&#39;");

			Altitude = ini.GetValue("Station", "Altitude", 0.0);
			AltitudeInFeet = ini.GetValue("Station", "AltitudeInFeet", true);

			StationOptions.Humidity98Fix = ini.GetValue("Station", "Humidity98Fix", false);
			StationOptions.UseWind10MinAve = ini.GetValue("Station", "Wind10MinAverage", false);
			StationOptions.UseSpeedForAvgCalc = ini.GetValue("Station", "UseSpeedForAvgCalc", false);

			StationOptions.AvgBearingMinutes = ini.GetValue("Station", "AvgBearingMinutes", 10);
			if (StationOptions.AvgBearingMinutes > 120)
			{
				StationOptions.AvgBearingMinutes = 120;
			}
			if (StationOptions.AvgBearingMinutes == 0)
			{
				StationOptions.AvgBearingMinutes = 1;
			}

			AvgBearingTime = new TimeSpan(StationOptions.AvgBearingMinutes / 60, StationOptions.AvgBearingMinutes % 60, 0);

			StationOptions.AvgSpeedMinutes = ini.GetValue("Station", "AvgSpeedMinutes", 10);
			if (StationOptions.AvgSpeedMinutes > 120)
			{
				StationOptions.AvgSpeedMinutes = 120;
			}
			if (StationOptions.AvgSpeedMinutes == 0)
			{
				StationOptions.AvgSpeedMinutes = 1;
			}

			AvgSpeedTime = new TimeSpan(StationOptions.AvgSpeedMinutes / 60, StationOptions.AvgSpeedMinutes % 60, 0);

			LogMessage("AvgSpdMins=" + StationOptions.AvgSpeedMinutes + " AvgSpdTime=" + AvgSpeedTime.ToString());

			StationOptions.PeakGustMinutes = ini.GetValue("Station", "PeakGustMinutes", 10);
			if (StationOptions.PeakGustMinutes > 120)
			{
				StationOptions.PeakGustMinutes = 120;
			}

			if (StationOptions.PeakGustMinutes == 0)
			{
				StationOptions.PeakGustMinutes = 1;
			}

			PeakGustTime = new TimeSpan(StationOptions.PeakGustMinutes / 60, StationOptions.PeakGustMinutes % 60, 0);

			if ((StationType == StationTypes.VantagePro) || (StationType == StationTypes.VantagePro2))
			{
				UVdecimaldefault = 1;
			}
			else
			{
				UVdecimaldefault = 0;
			}

			UVdecimals = ini.GetValue("Station", "UVdecimals", UVdecimaldefault);

			StationOptions.NoSensorCheck = ini.GetValue("Station", "NoSensorCheck", false);

			StationOptions.CalculatedDP = ini.GetValue("Station", "CalculatedDP", false);
			StationOptions.CalculatedWC = ini.GetValue("Station", "CalculatedWC", false);
			RolloverHour = ini.GetValue("Station", "RolloverHour", 0);
			Use10amInSummer = ini.GetValue("Station", "Use10amInSummer", true);
			//ConfirmClose = ini.GetValue("Station", "ConfirmClose", false);
			//CloseOnSuspend = ini.GetValue("Station", "CloseOnSuspend", false);
			//RestartIfUnplugged = ini.GetValue("Station", "RestartIfUnplugged", false);
			//RestartIfDataStops = ini.GetValue("Station", "RestartIfDataStops", false);
			StationOptions.SyncTime = ini.GetValue("Station", "SyncDavisClock", false);
			StationOptions.ClockSettingHour = ini.GetValue("Station", "ClockSettingHour", 4);
			StationOptions.WS2300IgnoreStationClock = ini.GetValue("Station", "WS2300IgnoreStationClock", false);
			//WS2300Sync = ini.GetValue("Station", "WS2300Sync", false);
			StationOptions.LogExtraSensors = ini.GetValue("Station", "LogExtraSensors", false);
			ReportDataStoppedErrors = ini.GetValue("Station", "ReportDataStoppedErrors", true);
			ReportLostSensorContact = ini.GetValue("Station", "ReportLostSensorContact", true);
			//NoFlashWetDryDayRecords = ini.GetValue("Station", "NoFlashWetDryDayRecords", false);
			ErrorLogSpikeRemoval = ini.GetValue("Station", "ErrorLogSpikeRemoval", true);
			DataLogInterval = ini.GetValue("Station", "DataLogInterval", 2);
			// this is now an index
			if (DataLogInterval > 5)
			{
				DataLogInterval = 2;
			}

			FineOffsetOptions.SyncReads = ini.GetValue("Station", "SyncFOReads", true);
			FineOffsetOptions.ReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3);
			FineOffsetOptions.ReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150);
			FineOffsetOptions.SetLoggerInterval = ini.GetValue("Station", "FineOffsetSetLoggerInterval", false);
			FineOffsetOptions.VendorID = ini.GetValue("Station", "VendorID", -1);
			FineOffsetOptions.ProductID = ini.GetValue("Station", "ProductID", -1);


			Units.Wind = ini.GetValue("Station", "WindUnit", 0);
			Units.Press = ini.GetValue("Station", "PressureUnit", 0);

			Units.Rain = ini.GetValue("Station", "RainUnit", 0);
			Units.Temp = ini.GetValue("Station", "TempUnit", 0);

			StationOptions.RoundWindSpeed = ini.GetValue("Station", "RoundWindSpeed", false);
			StationOptions.PrimaryAqSensor = ini.GetValue("Station", "PrimaryAqSensor", -1);


			// Unit decimals
			RainDPlaces = RainDPlaceDefaults[Units.Rain];
			TempDPlaces = TempDPlaceDefaults[Units.Temp];
			PressDPlaces = PressDPlaceDefaults[Units.Press];
			WindDPlaces = StationOptions.RoundWindSpeed ? 0 : WindDPlaceDefaults[Units.Wind];
			WindAvgDPlaces = WindDPlaces;
			AirQualityDPlaces = 1;

			// Unit decimal overrides
			WindDPlaces = ini.GetValue("Station", "WindSpeedDecimals", WindDPlaces);
			WindAvgDPlaces = ini.GetValue("Station", "WindSpeedAvgDecimals", WindAvgDPlaces);
			WindRunDPlaces = ini.GetValue("Station", "WindRunDecimals", WindRunDPlaces);
			SunshineDPlaces = ini.GetValue("Station", "SunshineHrsDecimals", 1);

			if ((StationType == 0 || StationType == 1) && DavisOptions.IncrementPressureDP)
			{
				// Use one more DP for Davis stations
				++PressDPlaces;
			}
			PressDPlaces = ini.GetValue("Station", "PressDecimals", PressDPlaces);
			RainDPlaces = ini.GetValue("Station", "RainDecimals", RainDPlaces);
			TempDPlaces = ini.GetValue("Station", "TempDecimals", TempDPlaces);
			UVDPlaces = ini.GetValue("Station", "UVDecimals", UVDPlaces);
			AirQualityDPlaces = ini.GetValue("Station", "AirQualityDecimals", AirQualityDPlaces);


			LocationName = ini.GetValue("Station", "LocName", "");
			LocationDesc = ini.GetValue("Station", "LocDesc", "");

			YTDrain = ini.GetValue("Station", "YTDrain", 0.0);
			YTDrainyear = ini.GetValue("Station", "YTDrainyear", 0);

			EwOptions.Interval = ini.GetValue("Station", "EWInterval", 1.0);
			EwOptions.Filename = ini.GetValue("Station", "EWFile", "");
			//EWallowFF = ini.GetValue("Station", "EWFF", false);
			//EWdisablecheckinit = ini.GetValue("Station", "EWdisablecheckinit", false);
			//EWduplicatecheck = ini.GetValue("Station", "EWduplicatecheck", true);
			EwOptions.MinPressMB = ini.GetValue("Station", "EWminpressureMB", 900);
			EwOptions.MaxPressMB = ini.GetValue("Station", "EWmaxpressureMB", 1200);
			EwOptions.MaxRainTipDiff = ini.GetValue("Station", "EWMaxRainTipDiff", 30);
			EwOptions.PressOffset = ini.GetValue("Station", "EWpressureoffset", 9999.0);

			Spike.TempDiff = ini.GetValue("Station", "EWtempdiff", 999.0);
			Spike.PressDiff = ini.GetValue("Station", "EWpressurediff", 999.0);
			Spike.HumidityDiff = ini.GetValue("Station", "EWhumiditydiff", 999.0);
			Spike.GustDiff = ini.GetValue("Station", "EWgustdiff", 999.0);
			Spike.WindDiff = ini.GetValue("Station", "EWwinddiff", 999.0);
			Spike.MaxRainRate = ini.GetValue("Station", "EWmaxRainRate", 999.0);
			Spike.MaxHourlyRain = ini.GetValue("Station", "EWmaxHourlyRain", 999.0);

			LCMaxWind = ini.GetValue("Station", "LCMaxWind", 9999);

			RecordsBeganDate = ini.GetValue("Station", "StartDate", DateTime.Now.ToLongDateString());

			LogMessage("Cumulus start date: " + RecordsBeganDate);

			ImetOptions.WaitTime = ini.GetValue("Station", "ImetWaitTime", 500);			// delay to wait for a reply to a command
			ImetOptions.ReadDelay = ini.GetValue("Station", "ImetReadDelay", 500);			// delay between sending read live data commands
			ImetOptions.UpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);   // keep the logger pointer pointing at last data read
			ImetOptions.BaudRate = ini.GetValue("Station", "ImetBaudRate", 19200);
			// Check we have a valid value
			if (!ImetOptions.BaudRates.Contains(ImetOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Error, the value for ImetOptions.ImetBaudRate in the ini file " + ImetOptions.BaudRate + " is not valid, using default 19200.");
				ImetOptions.BaudRate = 19200;
			}

			UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
			UseCumulusForecast = ini.GetValue("Station", "UseCumulusForecast", false);
			HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
			StationOptions.UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
			UseWindChillCutoff = ini.GetValue("Station", "UseWindChillCutoff", false);
			RecordSetTimeoutHrs = ini.GetValue("Station", "RecordSetTimeoutHrs", 24);

			SnowDepthHour = ini.GetValue("Station", "SnowDepthHour", 0);

			StationOptions.UseZeroBearing = ini.GetValue("Station", "UseZeroBearing", false);

			RainDayThreshold = ini.GetValue("Station", "RainDayThreshold", -1.0);

			FCpressinMB = ini.GetValue("Station", "FCpressinMB", true);
			FClowpress = ini.GetValue("Station", "FClowpress", DEFAULTFCLOWPRESS);
			FChighpress = ini.GetValue("Station", "FChighpress", DEFAULTFCHIGHPRESS);
			FCPressureThreshold = ini.GetValue("Station", "FCPressureThreshold", -1.0);

			RainSeasonStart = ini.GetValue("Station", "RainSeasonStart", 1);
			if (RainSeasonStart < 1 || RainSeasonStart > 12)
				RainSeasonStart = 1;
			ChillHourSeasonStart = ini.GetValue("Station", "ChillHourSeasonStart", 10);
			if (ChillHourSeasonStart < 1 || ChillHourSeasonStart > 12)
				ChillHourSeasonStart = 1;
			ChillHourThreshold = ini.GetValue("Station", "ChillHourThreshold", -999.0);

			RG11Enabled = ini.GetValue("Station", "RG11Enabled", false);
			RG11Port = ini.GetValue("Station", "RG11portName", DefaultComportName);
			RG11TBRmode = ini.GetValue("Station", "RG11TBRmode", false);
			RG11tipsize = ini.GetValue("Station", "RG11tipsize", 0.0);
			RG11IgnoreFirst = ini.GetValue("Station", "RG11IgnoreFirst", false);
			RG11DTRmode = ini.GetValue("Station", "RG11DTRmode", true);

			RG11Enabled2 = ini.GetValue("Station", "RG11Enabled2", false);
			RG11Port2 = ini.GetValue("Station", "RG11port2Name", DefaultComportName);
			RG11TBRmode2 = ini.GetValue("Station", "RG11TBRmode2", false);
			RG11tipsize2 = ini.GetValue("Station", "RG11tipsize2", 0.0);
			RG11IgnoreFirst2 = ini.GetValue("Station", "RG11IgnoreFirst2", false);
			RG11DTRmode2 = ini.GetValue("Station", "RG11DTRmode2", true);

			if (ChillHourThreshold < -998)
			{
				ChillHourThreshold = Units.Temp == 0 ? 7 : 45;
			}

			if (FCPressureThreshold < 0)
			{
				FCPressureThreshold = Units.Press == 2 ? 0.00295333727 : 0.1;
			}

			//special_logging = ini.GetValue("Station", "SpecialLog", false);
			//solar_logging = ini.GetValue("Station", "SolarLog", false);


			//RTdisconnectcount = ini.GetValue("Station", "RTdisconnectcount", 0);

			WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0);

			WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1);

			ListWebTags = ini.GetValue("Station", "ListWebTags", false);

			// WeatherLink Live device settings
			WllApiKey = ini.GetValue("WLL", "WLv2ApiKey", "");
			WllApiSecret = ini.GetValue("WLL", "WLv2ApiSecret", "");
			WllStationId = ini.GetValue("WLL", "WLStationId", -1);
			//if (WllStationId == "-1") WllStationId = "";
			WLLAutoUpdateIpAddress = ini.GetValue("WLL", "AutoUpdateIpAddress", true);
			WllBroadcastDuration = ini.GetValue("WLL", "BroadcastDuration", 1200);     // Readonly setting, default 20 minutes
			WllBroadcastPort = ini.GetValue("WLL", "BroadcastPort", 22222);            // Readonly setting, default 22222
			WllPrimaryRain = ini.GetValue("WLL", "PrimaryRainTxId", 1);
			WllPrimaryTempHum = ini.GetValue("WLL", "PrimaryTempHumTxId", 1);
			WllPrimaryWind = ini.GetValue("WLL", "PrimaryWindTxId", 1);
			WllPrimaryRain = ini.GetValue("WLL", "PrimaryRainTxId", 1);
			WllPrimarySolar = ini.GetValue("WLL", "PrimarySolarTxId", 0);
			WllPrimaryUV = ini.GetValue("WLL", "PrimaryUvTxId", 0);
			WllExtraSoilTempTx1 = ini.GetValue("WLL", "ExtraSoilTempTxId1", 0);
			WllExtraSoilTempIdx1 = ini.GetValue("WLL", "ExtraSoilTempIdx1", 1);
			WllExtraSoilTempTx2 = ini.GetValue("WLL", "ExtraSoilTempTxId2", 0);
			WllExtraSoilTempIdx2 = ini.GetValue("WLL", "ExtraSoilTempIdx2", 2);
			WllExtraSoilTempTx3 = ini.GetValue("WLL", "ExtraSoilTempTxId3", 0);
			WllExtraSoilTempIdx3 = ini.GetValue("WLL", "ExtraSoilTempIdx3", 3);
			WllExtraSoilTempTx4 = ini.GetValue("WLL", "ExtraSoilTempTxId4", 0);
			WllExtraSoilTempIdx4 = ini.GetValue("WLL", "ExtraSoilTempIdx4", 4);
			WllExtraSoilMoistureTx1 = ini.GetValue("WLL", "ExtraSoilMoistureTxId1", 0);
			WllExtraSoilMoistureIdx1 = ini.GetValue("WLL", "ExtraSoilMoistureIdx1", 1);
			WllExtraSoilMoistureTx2 = ini.GetValue("WLL", "ExtraSoilMoistureTxId2", 0);
			WllExtraSoilMoistureIdx2 = ini.GetValue("WLL", "ExtraSoilMoistureIdx2", 2);
			WllExtraSoilMoistureTx3 = ini.GetValue("WLL", "ExtraSoilMoistureTxId3", 0);
			WllExtraSoilMoistureIdx3 = ini.GetValue("WLL", "ExtraSoilMoistureIdx3", 3);
			WllExtraSoilMoistureTx4 = ini.GetValue("WLL", "ExtraSoilMoistureTxId4", 0);
			WllExtraSoilMoistureIdx4 = ini.GetValue("WLL", "ExtraSoilMoistureIdx4", 4);
			WllExtraLeafTx1 = ini.GetValue("WLL", "ExtraLeafTxId1", 0);
			WllExtraLeafIdx1 = ini.GetValue("WLL", "ExtraLeafIdx1", 1);
			WllExtraLeafTx2 = ini.GetValue("WLL", "ExtraLeafTxId2", 0);
			WllExtraLeafIdx2 = ini.GetValue("WLL", "ExtraLeafIdx2", 2);
			for (int i = 1; i <=8; i++)
			{
				WllExtraTempTx[i - 1] = ini.GetValue("WLL", "ExtraTempTxId" + i, 0);
				WllExtraHumTx[i - 1] = ini.GetValue("WLL", "ExtraHumOnTxId" + i, false);
			}

			// GW1000 settings
			Gw1000IpAddress = ini.GetValue("GW1000", "IPAddress", "0.0.0.0");
			Gw1000MacAddress = ini.GetValue("GW1000", "MACAddress", "");
			Gw1000AutoUpdateIpAddress = ini.GetValue("GW1000", "AutoUpdateIpAddress", true);

			// AirLink settings
			// We have to convert previous per AL IsNode config to global
			// So check if the global value exists
			if (ini.ValueExists("AirLink", "IsWllNode"))
			{
				AirLinkIsNode = ini.GetValue("AirLink", "IsWllNode", false);
			}
			else
			{
				AirLinkIsNode = ini.GetValue("AirLink", "In-IsNode", false) || ini.GetValue("AirLink", "Out-IsNode", false);
			}
			AirLinkApiKey = ini.GetValue("AirLink", "WLv2ApiKey", "");
			AirLinkApiSecret = ini.GetValue("AirLink", "WLv2ApiSecret", "");
			AirLinkAutoUpdateIpAddress = ini.GetValue("AirLink", "AutoUpdateIpAddress", true);
			AirLinkInEnabled = ini.GetValue("AirLink", "In-Enabled", false);
			AirLinkInIPAddr = ini.GetValue("AirLink", "In-IPAddress", "0.0.0.0");
			AirLinkInStationId = ini.GetValue("AirLink", "In-WLStationId", -1);
			if (AirLinkInStationId == -1 && AirLinkIsNode) AirLinkInStationId = WllStationId;
			AirLinkInHostName = ini.GetValue("AirLink", "In-Hostname", "");

			AirLinkOutEnabled = ini.GetValue("AirLink", "Out-Enabled", false);
			AirLinkOutIPAddr = ini.GetValue("AirLink", "Out-IPAddress", "0.0.0.0");
			AirLinkOutStationId = ini.GetValue("AirLink", "Out-WLStationId", -1);
			if (AirLinkOutStationId == -1 && AirLinkIsNode) AirLinkOutStationId = WllStationId;
			AirLinkOutHostName = ini.GetValue("AirLink", "Out-Hostname", "");

			airQualityIndex = ini.GetValue("AirLink", "AQIformula", 0);

			FtpHostname = ini.GetValue("FTP site", "Host", "");
			FtpHostPort = ini.GetValue("FTP site", "Port", 21);
			FtpUsername = ini.GetValue("FTP site", "Username", "");
			FtpPassword = ini.GetValue("FTP site", "Password", "");
			FtpDirectory = ini.GetValue("FTP site", "Directory", "");

			ActiveFTPMode = ini.GetValue("FTP site", "ActiveFTP", false);
			Sslftp = (FtpProtocols)ini.GetValue("FTP site", "Sslftp", 0);
			// BUILD 3092 - added alternate SFTP authenication options
			SshftpAuthentication = ini.GetValue("FTP site", "SshFtpAuthentication", "password"); // valid options: password, psk, password_psk
			if (!sshAuthenticationVals.Any(SshftpAuthentication.Contains))
			{
				SshftpAuthentication = "password";
				LogMessage($"Error, invalid SshFtpAuthentication value in Cumulus.ini [{SshftpAuthentication}], defaulting to Password.");
			}
			SshftpPskFile = ini.GetValue("FTP site", "SshFtpPskFile", "");
			if (SshftpPskFile.Length > 0 && (SshftpAuthentication == "psk" || SshftpAuthentication == "password_psk") && !File.Exists(SshftpPskFile))
			{
				SshftpPskFile = "";
				LogMessage($"Error, file name specified by SshFtpPskFile value in Cumulus.ini does not exist [{SshftpPskFile}], defaulting to None.");
			}
			DisableFtpsEPSV = ini.GetValue("FTP site", "DisableEPSV", false);
			DisableFtpsExplicit = ini.GetValue("FTP site", "DisableFtpsExplicit", false);
			FTPlogging = ini.GetValue("FTP site", "FTPlogging", false);
			RealtimeEnabled = ini.GetValue("FTP site", "EnableRealtime", false);
			RealtimeFTPEnabled = ini.GetValue("FTP site", "RealtimeFTPEnabled", false);

			RealtimeFiles[0].Create = ini.GetValue("FTP site", "RealtimeTxtCreate", false);
			RealtimeFiles[0].FTP = RealtimeFiles[0].Create && ini.GetValue("FTP site", "RealtimeTxtFTP", false);
			RealtimeFiles[1].Create = ini.GetValue("FTP site", "RealtimeGaugesTxtCreate", false);
			RealtimeFiles[1].FTP = RealtimeFiles[1].Create && ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);

			RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000);
			if (RealtimeInterval < 1) { RealtimeInterval = 1; }
			//RealtimeTimer.Change(0,RealtimeInterval);

			WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);
			// Have to allow for upgrade, set interval enabled to old WebAutoUpdate
			if (ini.ValueExists("FTP site", "IntervalEnabled"))
			{
				WebIntervalEnabled = ini.GetValue("FTP site", "IntervalEnabled", false);
			}
			else
			{
				WebIntervalEnabled = WebAutoUpdate;
			}

			UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", DefaultWebUpdateInterval);
			if (UpdateInterval<1) { UpdateInterval = 1; }
			SynchronisedWebUpdate = (60 % UpdateInterval == 0);

			var IncludeStandardFiles = false;
			if (ini.ValueExists("FTP site", "IncludeSTD"))
			{
				IncludeStandardFiles = ini.GetValue("FTP site", "IncludeSTD", false);
			}
			for (var i = 0; i < StdWebFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				StdWebFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeStandardFiles);
				StdWebFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeStandardFiles);
			}

			var IncludeGraphDataFiles = false;
			if (ini.ValueExists("FTP site", "IncludeGraphDataFiles"))
			{
				IncludeGraphDataFiles = ini.GetValue("FTP site", "IncludeGraphDataFiles", true);
			}
			for (var i = 0; i < GraphDataFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				GraphDataFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeGraphDataFiles);
				GraphDataFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeGraphDataFiles);
			}
			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				GraphDataEodFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeGraphDataFiles);
				GraphDataEodFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeGraphDataFiles);
			}

			IncludeMoonImage = ini.GetValue("FTP site", "IncludeMoonImage", false);

			FTPRename = ini.GetValue("FTP site", "FTPRename", false);
			UTF8encode = ini.GetValue("FTP site", "UTF8encode", true);
			DeleteBeforeUpload = ini.GetValue("FTP site", "DeleteBeforeUpload", false);

			//MaxFTPconnectRetries = ini.GetValue("FTP site", "MaxFTPconnectRetries", 3);

			for (int i = 0; i < numextrafiles; i++)
			{
				ExtraFiles[i].local = ini.GetValue("FTP site", "ExtraLocal" + i, "");
				ExtraFiles[i].remote = ini.GetValue("FTP site", "ExtraRemote" + i, "");
				ExtraFiles[i].process = ini.GetValue("FTP site", "ExtraProcess" + i, false);
				ExtraFiles[i].binary = ini.GetValue("FTP site", "ExtraBinary" + i, false);
				ExtraFiles[i].realtime = ini.GetValue("FTP site", "ExtraRealtime" + i, false);
				ExtraFiles[i].FTP = ini.GetValue("FTP site", "ExtraFTP" + i, true);
				ExtraFiles[i].UTF8 = ini.GetValue("FTP site", "ExtraUTF" + i, false);
				ExtraFiles[i].endofday = ini.GetValue("FTP site", "ExtraEOD" + i, false);
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
			GraphHours = ini.GetValue("Graphs", "GraphHours", 72);
			MoonImageEnabled = ini.GetValue("Graphs", "MoonImageEnabled", false);
			MoonImageSize = ini.GetValue("Graphs", "MoonImageSize", 100);
			if (MoonImageSize < 10)
				MoonImageSize = 10;
			MoonImageFtpDest = ini.GetValue("Graphs", "MoonImageFtpDest", "images/moon.png");
			GraphOptions.TempVisible = ini.GetValue("Graphs", "TempVisible", true);
			GraphOptions.InTempVisible = ini.GetValue("Graphs", "InTempVisible", true);
			GraphOptions.HIVisible = ini.GetValue("Graphs", "HIVisible", true);
			GraphOptions.DPVisible = ini.GetValue("Graphs", "DPVisible", true);
			GraphOptions.WCVisible = ini.GetValue("Graphs", "WCVisible", true);
			GraphOptions.AppTempVisible = ini.GetValue("Graphs", "AppTempVisible", true);
			GraphOptions.FeelsLikeVisible = ini.GetValue("Graphs", "FeelsLikeVisible", true);
			GraphOptions.HumidexVisible = ini.GetValue("Graphs", "HumidexVisible", true);
			GraphOptions.InHumVisible = ini.GetValue("Graphs", "InHumVisible", true);
			GraphOptions.OutHumVisible = ini.GetValue("Graphs", "OutHumVisible", true);
			GraphOptions.UVVisible = ini.GetValue("Graphs", "UVVisible", true);
			GraphOptions.SolarVisible = ini.GetValue("Graphs", "SolarVisible", true);
			GraphOptions.SunshineVisible = ini.GetValue("Graphs", "SunshineVisible", true);
			GraphOptions.DailyAvgTempVisible = ini.GetValue("Graphs", "DailyAvgTempVisible", true);
			GraphOptions.DailyMaxTempVisible = ini.GetValue("Graphs", "DailyMaxTempVisible", true);
			GraphOptions.DailyMinTempVisible = ini.GetValue("Graphs", "DailyMinTempVisible", true);
			GraphOptions.GrowingDegreeDaysVisible1 = ini.GetValue("Graphs", "GrowingDegreeDaysVisible1", true);
			GraphOptions.GrowingDegreeDaysVisible2 = ini.GetValue("Graphs", "GrowingDegreeDaysVisible2", true);
			GraphOptions.TempSumVisible0 = ini.GetValue("Graphs", "TempSumVisible0", true);
			GraphOptions.TempSumVisible1 = ini.GetValue("Graphs", "TempSumVisible1", true);
			GraphOptions.TempSumVisible2 = ini.GetValue("Graphs", "TempSumVisible2", true);


			Wund.ID = ini.GetValue("Wunderground", "ID", "");
			Wund.PW = ini.GetValue("Wunderground", "Password", "");
			Wund.Enabled = ini.GetValue("Wunderground", "Enabled", false);
			Wund.RapidFireEnabled = ini.GetValue("Wunderground", "RapidFire", false);
			Wund.Interval = ini.GetValue("Wunderground", "Interval", Wund.DefaultInterval);
			//WundHTTPLogging = ini.GetValue("Wunderground", "Logging", false);
			Wund.SendUV = ini.GetValue("Wunderground", "SendUV", false);
			Wund.SendSolar = ini.GetValue("Wunderground", "SendSR", false);
			Wund.SendIndoor = ini.GetValue("Wunderground", "SendIndoor", false);
			Wund.SendSoilTemp1 = ini.GetValue("Wunderground", "SendSoilTemp1", false);
			Wund.SendSoilTemp2 = ini.GetValue("Wunderground", "SendSoilTemp2", false);
			Wund.SendSoilTemp3 = ini.GetValue("Wunderground", "SendSoilTemp3", false);
			Wund.SendSoilTemp4 = ini.GetValue("Wunderground", "SendSoilTemp4", false);
			Wund.SendSoilMoisture1 = ini.GetValue("Wunderground", "SendSoilMoisture1", false);
			Wund.SendSoilMoisture2 = ini.GetValue("Wunderground", "SendSoilMoisture2", false);
			Wund.SendSoilMoisture3 = ini.GetValue("Wunderground", "SendSoilMoisture3", false);
			Wund.SendSoilMoisture4 = ini.GetValue("Wunderground", "SendSoilMoisture4", false);
			Wund.SendLeafWetness1 = ini.GetValue("Wunderground", "SendLeafWetness1", false);
			Wund.SendLeafWetness2 = ini.GetValue("Wunderground", "SendLeafWetness2", false);
			Wund.SendAirQuality = ini.GetValue("Wunderground", "SendAirQuality", false);
			Wund.SendAverage = ini.GetValue("Wunderground", "SendAverage", false);
			Wund.CatchUp = ini.GetValue("Wunderground", "CatchUp", true);

			Wund.SynchronisedUpdate = !Wund.RapidFireEnabled;

			Windy.ApiKey = ini.GetValue("Windy", "APIkey", "");
			Windy.StationIdx = ini.GetValue("Windy", "StationIdx", 0);
			Windy.Enabled = ini.GetValue("Windy", "Enabled", false);
			Windy.Interval = ini.GetValue("Windy", "Interval", Windy.DefaultInterval);
			if (Windy.Interval < 5) { Windy.Interval = 5; }
			//WindyHTTPLogging = ini.GetValue("Windy", "Logging", false);
			Windy.SendUV = ini.GetValue("Windy", "SendUV", false);
			Windy.SendSolar = ini.GetValue("Windy", "SendSolar", false);
			Windy.CatchUp = ini.GetValue("Windy", "CatchUp", false);

			AWEKAS.ID = ini.GetValue("Awekas", "User", "");
			AWEKAS.PW = ini.GetValue("Awekas", "Password", "");
			AWEKAS.Enabled = ini.GetValue("Awekas", "Enabled", false);
			AWEKAS.Interval = ini.GetValue("Awekas", "Interval", AWEKAS.DefaultInterval);
			if (AWEKAS.Interval < 15) { AWEKAS.Interval = 15; }
			AWEKAS.Lang = ini.GetValue("Awekas", "Language", "en");
			AWEKAS.OriginalInterval = AWEKAS.Interval;
			AWEKAS.SendUV = ini.GetValue("Awekas", "SendUV", false);
			AWEKAS.SendSolar = ini.GetValue("Awekas", "SendSR", false);
			AWEKAS.SendSoilTemp = ini.GetValue("Awekas", "SendSoilTemp", false);
			AWEKAS.SendIndoor = ini.GetValue("Awekas", "SendIndoor", false);
			AWEKAS.SendSoilMoisture = ini.GetValue("Awekas", "SendSoilMoisture", false);
			AWEKAS.SendLeafWetness = ini.GetValue("Awekas", "SendLeafWetness", false);
			AWEKAS.SendAirQuality = ini.GetValue("Awekas", "SendAirQuality", false);

			AWEKAS.SynchronisedUpdate = (AWEKAS.Interval % 60 == 0);

			WindGuru.ID = ini.GetValue("WindGuru", "StationUID", "");
			WindGuru.PW = ini.GetValue("WindGuru", "Password", "");
			WindGuru.Enabled = ini.GetValue("WindGuru", "Enabled", false);
			WindGuru.Interval = ini.GetValue("WindGuru", "Interval", WindGuru.DefaultInterval);
			if (WindGuru.Interval < 1) { WindGuru.Interval = 1; }
			WindGuru.SendRain = ini.GetValue("WindGuru", "SendRain", false);

			WCloud.ID = ini.GetValue("WeatherCloud", "Wid", "");
			WCloud.PW = ini.GetValue("WeatherCloud", "Key", "");
			WCloud.Enabled = ini.GetValue("WeatherCloud", "Enabled", false);
			WCloud.Interval = ini.GetValue("WeatherCloud", "Interval", WCloud.DefaultInterval);
			WCloud.SendUV = ini.GetValue("WeatherCloud", "SendUV", false);
			WCloud.SendSolar = ini.GetValue("WeatherCloud", "SendSR", false);
			WCloud.SendAirQuality = ini.GetValue("WeatherCloud", "SendAirQuality", false);
			WCloud.SendSoilMoisture = ini.GetValue("WeatherCloud", "SendSoilMoisture", false);
			WCloud.SoilMoistureSensor= ini.GetValue("WeatherCloud", "SoilMoistureSensor", 1);
			WCloud.SendLeafWetness = ini.GetValue("WeatherCloud", "SendLeafWetness", false);
			WCloud.LeafWetnessSensor = ini.GetValue("WeatherCloud", "LeafWetnessSensor", 1);

			Twitter.ID = ini.GetValue("Twitter", "User", "");
			Twitter.PW = ini.GetValue("Twitter", "Password", "");
			Twitter.Enabled = ini.GetValue("Twitter", "Enabled", false);
			Twitter.Interval = ini.GetValue("Twitter", "Interval", 60);
			if (Twitter.Interval < 1) { Twitter.Interval = 1; }
			Twitter.OauthToken = ini.GetValue("Twitter", "OauthToken", "unknown");
			Twitter.OauthTokenSecret = ini.GetValue("Twitter", "OauthTokenSecret", "unknown");
			Twitter.SendLocation = ini.GetValue("Twitter", "SendLocation", true);

			//if HTTPLogging then
			//  MainForm.WUHTTP.IcsLogger = MainForm.HTTPlogger;

			PWS.ID = ini.GetValue("PWSweather", "ID", "");
			PWS.PW = ini.GetValue("PWSweather", "Password", "");
			PWS.Enabled = ini.GetValue("PWSweather", "Enabled", false);
			PWS.Interval = ini.GetValue("PWSweather", "Interval", PWS.DefaultInterval);
			if (PWS.Interval < 1) { PWS.Interval = 1; }
			PWS.SendUV = ini.GetValue("PWSweather", "SendUV", false);
			PWS.SendSolar = ini.GetValue("PWSweather", "SendSR", false);
			PWS.CatchUp = ini.GetValue("PWSweather", "CatchUp", true);

			WOW.ID = ini.GetValue("WOW", "ID", "");
			WOW.PW = ini.GetValue("WOW", "Password", "");
			WOW.Enabled = ini.GetValue("WOW", "Enabled", false);
			WOW.Interval = ini.GetValue("WOW", "Interval", WOW.DefaultInterval);
			if (WOW.Interval < 1) { WOW.Interval = 1; }
			WOW.SendUV = ini.GetValue("WOW", "SendUV", false);
			WOW.SendSolar = ini.GetValue("WOW", "SendSR", false);
			WOW.CatchUp = ini.GetValue("WOW", "CatchUp", true);

			APRS.ID = ini.GetValue("APRS", "ID", "");
			APRS.PW = ini.GetValue("APRS", "pass", "-1");
			APRS.Server = ini.GetValue("APRS", "server", "cwop.aprs.net");
			APRS.Port = ini.GetValue("APRS", "port", 14580);
			APRS.Enabled = ini.GetValue("APRS", "Enabled", false);
			APRS.Interval = ini.GetValue("APRS", "Interval", APRS.DefaultInterval);
			if (APRS.Interval < 1) { APRS.Interval = 1; }
			APRS.HumidityCutoff = ini.GetValue("APRS", "APRSHumidityCutoff", false);
			APRS.SendSolar = ini.GetValue("APRS", "SendSR", false);

			OpenWeatherMap.Enabled = ini.GetValue("OpenWeatherMap", "Enabled", false);
			OpenWeatherMap.CatchUp = ini.GetValue("OpenWeatherMap", "CatchUp", true);
			OpenWeatherMap.PW = ini.GetValue("OpenWeatherMap", "APIkey", "");
			OpenWeatherMap.ID = ini.GetValue("OpenWeatherMap", "StationId", "");
			OpenWeatherMap.Interval = ini.GetValue("OpenWeatherMap", "Interval", OpenWeatherMap.DefaultInterval);

			MQTT.Server = ini.GetValue("MQTT", "Server", "");
			MQTT.Port = ini.GetValue("MQTT", "Port", 1883);
			MQTT.IpVersion = ini.GetValue("MQTT", "IPversion", 0); // 0 = unspecified, 4 = force IPv4, 6 = force IPv6
			if (MQTT.IpVersion != 0 && MQTT.IpVersion != 4 && MQTT.IpVersion != 6)
				MQTT.IpVersion = 0;
			MQTT.UseTLS = ini.GetValue("MQTT", "UseTLS", false);
			MQTT.Username = ini.GetValue("MQTT", "Username", "");
			MQTT.Password = ini.GetValue("MQTT", "Password", "");
			MQTT.EnableDataUpdate = ini.GetValue("MQTT", "EnableDataUpdate", false);
			MQTT.UpdateTopic = ini.GetValue("MQTT", "UpdateTopic", "CumulusMX/DataUpdate");
			MQTT.UpdateTemplate = ini.GetValue("MQTT", "UpdateTemplate", "DataUpdateTemplate.txt");
			MQTT.UpdateRetained = ini.GetValue("MQTT", "UpdateRetained", false);
			MQTT.EnableInterval = ini.GetValue("MQTT", "EnableInterval", false);
			MQTT.IntervalTime = ini.GetValue("MQTT", "IntervalTime", 600); // default to 10 minutes
			MQTT.IntervalTopic = ini.GetValue("MQTT", "IntervalTopic", "CumulusMX/Interval");
			MQTT.IntervalTemplate = ini.GetValue("MQTT", "IntervalTemplate", "IntervalTemplate.txt");
			MQTT.IntervalRetained = ini.GetValue("MQTT", "IntervalRetained", false);

			LowTempAlarm.Value = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
			LowTempAlarm.Enabled = ini.GetValue("Alarms", "LowTempAlarmSet", false);
			LowTempAlarm.Sound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
			LowTempAlarm.SoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
			if (LowTempAlarm.SoundFile.Contains(DefaultSoundFileOld)) LowTempAlarm.SoundFile = DefaultSoundFile;
			LowTempAlarm.Notify = ini.GetValue("Alarms", "LowTempAlarmNotify", false);
			LowTempAlarm.Email = ini.GetValue("Alarms", "LowTempAlarmEmail", false);
			LowTempAlarm.Latch = ini.GetValue("Alarms", "LowTempAlarmLatch", false);
			LowTempAlarm.LatchHours = ini.GetValue("Alarms", "LowTempAlarmLatchHours", 24);

			HighTempAlarm.Value = ini.GetValue("Alarms", "alarmhightemp", 0.0);
			HighTempAlarm.Enabled = ini.GetValue("Alarms", "HighTempAlarmSet", false);
			HighTempAlarm.Sound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
			HighTempAlarm.SoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
			if (HighTempAlarm.SoundFile.Contains(DefaultSoundFileOld)) HighTempAlarm.SoundFile = DefaultSoundFile;
			HighTempAlarm.Notify = ini.GetValue("Alarms", "HighTempAlarmNotify", false);
			HighTempAlarm.Email = ini.GetValue("Alarms", "HighTempAlarmEmail", false);
			HighTempAlarm.Latch = ini.GetValue("Alarms", "HighTempAlarmLatch", false);
			HighTempAlarm.LatchHours = ini.GetValue("Alarms", "HighTempAlarmLatchHours", 24);

			TempChangeAlarm.Value = ini.GetValue("Alarms", "alarmtempchange", 0.0);
			TempChangeAlarm.Enabled = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
			TempChangeAlarm.Sound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
			TempChangeAlarm.SoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
			if (TempChangeAlarm.SoundFile.Contains(DefaultSoundFileOld)) TempChangeAlarm.SoundFile = DefaultSoundFile;
			TempChangeAlarm.Notify = ini.GetValue("Alarms", "TempChangeAlarmNotify", false);
			TempChangeAlarm.Email = ini.GetValue("Alarms", "TempChangeAlarmEmail", false);
			TempChangeAlarm.Latch = ini.GetValue("Alarms", "TempChangeAlarmLatch", false);
			TempChangeAlarm.LatchHours = ini.GetValue("Alarms", "TempChangeAlarmLatchHours", 24);

			LowPressAlarm.Value = ini.GetValue("Alarms", "alarmlowpress", 0.0);
			LowPressAlarm.Enabled = ini.GetValue("Alarms", "LowPressAlarmSet", false);
			LowPressAlarm.Sound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
			LowPressAlarm.SoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
			if (LowPressAlarm.SoundFile.Contains(DefaultSoundFileOld)) LowPressAlarm.SoundFile = DefaultSoundFile;
			LowPressAlarm.Notify = ini.GetValue("Alarms", "LowPressAlarmNotify", false);
			LowPressAlarm.Email = ini.GetValue("Alarms", "LowPressAlarmEmail", false);
			LowPressAlarm.Latch = ini.GetValue("Alarms", "LowPressAlarmLatch", false);
			LowPressAlarm.LatchHours = ini.GetValue("Alarms", "LowPressAlarmLatchHours", 24);

			HighPressAlarm.Value = ini.GetValue("Alarms", "alarmhighpress", 0.0);
			HighPressAlarm.Enabled = ini.GetValue("Alarms", "HighPressAlarmSet", false);
			HighPressAlarm.Sound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
			HighPressAlarm.SoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
			if (HighPressAlarm.SoundFile.Contains(DefaultSoundFileOld)) HighPressAlarm.SoundFile = DefaultSoundFile;
			HighPressAlarm.Notify = ini.GetValue("Alarms", "HighPressAlarmNotify", false);
			HighPressAlarm.Email = ini.GetValue("Alarms", "HighPressAlarmEmail", false);
			HighPressAlarm.Latch = ini.GetValue("Alarms", "HighPressAlarmLatch", false);
			HighPressAlarm.LatchHours = ini.GetValue("Alarms", "HighPressAlarmLatchHours", 24);

			PressChangeAlarm.Value = ini.GetValue("Alarms", "alarmpresschange", 0.0);
			PressChangeAlarm.Enabled = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
			PressChangeAlarm.Sound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
			PressChangeAlarm.SoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
			if (PressChangeAlarm.SoundFile.Contains(DefaultSoundFileOld)) PressChangeAlarm.SoundFile = DefaultSoundFile;
			PressChangeAlarm.Notify = ini.GetValue("Alarms", "PressChangeAlarmNotify", false);
			PressChangeAlarm.Email = ini.GetValue("Alarms", "PressChangeAlarmEmail", false);
			PressChangeAlarm.Latch = ini.GetValue("Alarms", "PressChangeAlarmLatch", false);
			PressChangeAlarm.LatchHours = ini.GetValue("Alarms", "PressChangeAlarmLatchHours", 24);

			HighRainTodayAlarm.Value = ini.GetValue("Alarms", "alarmhighraintoday", 0.0);
			HighRainTodayAlarm.Enabled = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
			HighRainTodayAlarm.Sound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
			HighRainTodayAlarm.SoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
			if (HighRainTodayAlarm.SoundFile.Contains(DefaultSoundFileOld)) HighRainTodayAlarm.SoundFile = DefaultSoundFile;
			HighRainTodayAlarm.Notify = ini.GetValue("Alarms", "HighRainTodayAlarmNotify", false);
			HighRainTodayAlarm.Email = ini.GetValue("Alarms", "HighRainTodayAlarmEmail", false);
			HighRainTodayAlarm.Latch = ini.GetValue("Alarms", "HighRainTodayAlarmLatch", false);
			HighRainTodayAlarm.LatchHours = ini.GetValue("Alarms", "HighRainTodayAlarmLatchHours", 24);

			HighRainRateAlarm.Value = ini.GetValue("Alarms", "alarmhighrainrate", 0.0);
			HighRainRateAlarm.Enabled = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
			HighRainRateAlarm.Sound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
			HighRainRateAlarm.SoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
			if (HighRainRateAlarm.SoundFile.Contains(DefaultSoundFileOld)) HighRainRateAlarm.SoundFile = DefaultSoundFile;
			HighRainRateAlarm.Notify = ini.GetValue("Alarms", "HighRainRateAlarmNotify", false);
			HighRainRateAlarm.Email = ini.GetValue("Alarms", "HighRainRateAlarmEmail", false);
			HighRainRateAlarm.Latch = ini.GetValue("Alarms", "HighRainRateAlarmLatch", false);
			HighRainRateAlarm.LatchHours = ini.GetValue("Alarms", "HighRainRateAlarmLatchHours", 24);

			HighGustAlarm.Value = ini.GetValue("Alarms", "alarmhighgust", 0.0);
			HighGustAlarm.Enabled = ini.GetValue("Alarms", "HighGustAlarmSet", false);
			HighGustAlarm.Sound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
			HighGustAlarm.SoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
			if (HighGustAlarm.SoundFile.Contains(DefaultSoundFileOld)) HighGustAlarm.SoundFile = DefaultSoundFile;
			HighGustAlarm.Notify = ini.GetValue("Alarms", "HighGustAlarmNotify", false);
			HighGustAlarm.Email = ini.GetValue("Alarms", "HighGustAlarmEmail", false);
			HighGustAlarm.Latch = ini.GetValue("Alarms", "HighGustAlarmLatch", false);
			HighGustAlarm.LatchHours = ini.GetValue("Alarms", "HighGustAlarmLatchHours", 24);

			HighWindAlarm.Value = ini.GetValue("Alarms", "alarmhighwind", 0.0);
			HighWindAlarm.Enabled = ini.GetValue("Alarms", "HighWindAlarmSet", false);
			HighWindAlarm.Sound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
			HighWindAlarm.SoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
			if (HighWindAlarm.SoundFile.Contains(DefaultSoundFileOld)) HighWindAlarm.SoundFile = DefaultSoundFile;
			HighWindAlarm.Notify = ini.GetValue("Alarms", "HighWindAlarmNotify", false);
			HighWindAlarm.Email = ini.GetValue("Alarms", "HighWindAlarmEmail", false);
			HighWindAlarm.Latch = ini.GetValue("Alarms", "HighWindAlarmLatch", false);
			HighWindAlarm.LatchHours = ini.GetValue("Alarms", "HighWindAlarmLatchHours", 24);

			SensorAlarm.Enabled = ini.GetValue("Alarms", "SensorAlarmSet", false);
			SensorAlarm.Sound = ini.GetValue("Alarms", "SensorAlarmSound", false);
			SensorAlarm.SoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
			if (SensorAlarm.SoundFile.Contains(DefaultSoundFileOld)) SensorAlarm.SoundFile = DefaultSoundFile;
			SensorAlarm.Notify = ini.GetValue("Alarms", "SensorAlarmNotify", false);
			SensorAlarm.Email = ini.GetValue("Alarms", "SensorAlarmEmail", false);
			SensorAlarm.Latch = ini.GetValue("Alarms", "SensorAlarmLatch", false);
			SensorAlarm.LatchHours = ini.GetValue("Alarms", "SensorAlarmLatchHours", 24);

			DataStoppedAlarm.Enabled = ini.GetValue("Alarms", "DataStoppedAlarmSet", false);
			DataStoppedAlarm.Sound = ini.GetValue("Alarms", "DataStoppedAlarmSound", false);
			DataStoppedAlarm.SoundFile = ini.GetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
			if (DataStoppedAlarm.SoundFile.Contains(DefaultSoundFileOld)) SensorAlarm.SoundFile = DefaultSoundFile;
			DataStoppedAlarm.Notify = ini.GetValue("Alarms", "DataStoppedAlarmNotify", false);
			DataStoppedAlarm.Email = ini.GetValue("Alarms", "DataStoppedAlarmEmail", false);
			DataStoppedAlarm.Latch = ini.GetValue("Alarms", "DataStoppedAlarmLatch", false);
			DataStoppedAlarm.LatchHours = ini.GetValue("Alarms", "DataStoppedAlarmLatchHours", 24);

			BatteryLowAlarm.Enabled = ini.GetValue("Alarms", "BatteryLowAlarmSet", false);
			BatteryLowAlarm.Sound = ini.GetValue("Alarms", "BatteryLowAlarmSound", false);
			BatteryLowAlarm.SoundFile = ini.GetValue("Alarms", "BatteryLowAlarmSoundFile", DefaultSoundFile);
			BatteryLowAlarm.Notify = ini.GetValue("Alarms", "BatteryLowAlarmNotify", false);
			BatteryLowAlarm.Email = ini.GetValue("Alarms", "BatteryLowAlarmEmail", false);
			BatteryLowAlarm.Latch = ini.GetValue("Alarms", "BatteryLowAlarmLatch", false);
			BatteryLowAlarm.LatchHours = ini.GetValue("Alarms", "BatteryLowAlarmLatchHours", 24);

			SpikeAlarm.Enabled = ini.GetValue("Alarms", "DataSpikeAlarmSet", false);
			SpikeAlarm.Sound = ini.GetValue("Alarms", "DataSpikeAlarmSound", false);
			SpikeAlarm.SoundFile = ini.GetValue("Alarms", "DataSpikeAlarmSoundFile", DefaultSoundFile);
			SpikeAlarm.Notify = ini.GetValue("Alarms", "SpikeAlarmNotify", true);
			SpikeAlarm.Email = ini.GetValue("Alarms", "SpikeAlarmEmail", true);
			SpikeAlarm.Latch = ini.GetValue("Alarms", "SpikeAlarmLatch", true);
			SpikeAlarm.LatchHours = ini.GetValue("Alarms", "SpikeAlarmLatchHours", 24);

			UpgradeAlarm.Enabled = ini.GetValue("Alarms", "UpgradeAlarmSet", true);
			UpgradeAlarm.Sound = ini.GetValue("Alarms", "UpgradeAlarmSound", true);
			UpgradeAlarm.SoundFile = ini.GetValue("Alarms", "UpgradeAlarmSoundFile", DefaultSoundFile);
			UpgradeAlarm.Notify = ini.GetValue("Alarms", "UpgradeAlarmNotify", true);
			UpgradeAlarm.Email = ini.GetValue("Alarms", "UpgradeAlarmEmail", false);
			UpgradeAlarm.Latch = ini.GetValue("Alarms", "UpgradeAlarmLatch", false);
			UpgradeAlarm.LatchHours = ini.GetValue("Alarms", "UpgradeAlarmLatchHours", 24);

			AlarmFromEmail = ini.GetValue("Alarms", "FromEmail", "");
			AlarmDestEmail = ini.GetValue("Alarms", "DestEmail", "").Split(';');
			AlarmEmailHtml = ini.GetValue("Alarms", "UseHTML", false);

			Calib.Press.Offset = ini.GetValue("Offsets", "PressOffset", 0.0);
			Calib.Temp.Offset = ini.GetValue("Offsets", "TempOffset", 0.0);
			Calib.Hum.Offset = ini.GetValue("Offsets", "HumOffset", 0);
			Calib.WindDir.Offset = ini.GetValue("Offsets", "WindDirOffset", 0);
			Calib.InTemp.Offset = ini.GetValue("Offsets", "InTempOffset", 0.0);
			Calib.Solar.Offset = ini.GetValue("Offsers", "SolarOffset", 0.0);
			Calib.UV.Offset = ini.GetValue("Offsets", "UVOffset", 0.0);
			Calib.WetBulb.Offset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);

			Calib.Press.Mult = ini.GetValue("Offsets", "PressMult", 1.0);
			Calib.WindSpeed.Mult = ini.GetValue("Offsets", "WindSpeedMult", 1.0);
			Calib.WindGust.Mult = ini.GetValue("Offsets", "WindGustMult", 1.0);
			Calib.Temp.Mult = ini.GetValue("Offsets", "TempMult", 1.0);
			Calib.Temp.Mult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
			Calib.Hum.Mult = ini.GetValue("Offsets", "HumMult", 1.0);
			Calib.Hum.Mult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
			Calib.Rain.Mult = ini.GetValue("Offsets", "RainMult", 1.0);
			Calib.Solar.Mult = ini.GetValue("Offsets", "SolarMult", 1.0);
			Calib.UV.Mult = ini.GetValue("Offsets", "UVMult", 1.0);
			Calib.WetBulb.Mult = ini.GetValue("Offsets", "WetBulbMult", 1.0);

			Limit.TempHigh = ini.GetValue("Limits", "TempHighC", 60.0);
			Limit.TempLow = ini.GetValue("Limits", "TempLowC", -60.0);
			Limit.DewHigh = ini.GetValue("Limits", "DewHighC", 40.0);
			Limit.PressHigh = ini.GetValue("Limits", "PressHighMB", 1090.0);
			Limit.PressLow = ini.GetValue("Limits", "PressLowMB", 870.0);
			Limit.WindHigh = ini.GetValue("Limits", "WindHighMS", 90.0);

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
			//SolarFactorSummer = ini.GetValue("Solar", "SolarFactorSummer", -1);
			//SolarFactorWinter = ini.GetValue("Solar", "SolarFactorWinter", -1);

			NOAAname = ini.GetValue("NOAA", "Name", " ");
			NOAAcity = ini.GetValue("NOAA", "City", " ");
			NOAAstate = ini.GetValue("NOAA", "State", " ");
			NOAA12hourformat = ini.GetValue("NOAA", "12hourformat", false);
			NOAAheatingthreshold = ini.GetValue("NOAA", "HeatingThreshold", -1000.0);
			if (NOAAheatingthreshold < -99 || NOAAheatingthreshold > 150)
			{
				NOAAheatingthreshold = Units.Temp == 0 ? 18.3 : 65;
			}
			NOAAcoolingthreshold = ini.GetValue("NOAA", "CoolingThreshold", -1000.0);
			if (NOAAcoolingthreshold < -99 || NOAAcoolingthreshold > 150)
			{
				NOAAcoolingthreshold = Units.Temp == 0 ? 18.3 : 65;
			}
			NOAAmaxtempcomp1 = ini.GetValue("NOAA", "MaxTempComp1", -1000.0);
			if (NOAAmaxtempcomp1 < -99 || NOAAmaxtempcomp1 > 150)
			{
				NOAAmaxtempcomp1 = Units.Temp == 0 ? 27 : 80;
			}
			NOAAmaxtempcomp2 = ini.GetValue("NOAA", "MaxTempComp2", -1000.0);
			if (NOAAmaxtempcomp2 < -99 || NOAAmaxtempcomp2 > 99)
			{
				NOAAmaxtempcomp2 = Units.Temp == 0 ? 0 : 32;
			}
			NOAAmintempcomp1 = ini.GetValue("NOAA", "MinTempComp1", -1000.0);
			if (NOAAmintempcomp1 < -99 || NOAAmintempcomp1 > 99)
			{
				NOAAmintempcomp1 = Units.Temp == 0 ? 0 : 32;
			}
			NOAAmintempcomp2 = ini.GetValue("NOAA", "MinTempComp2", -1000.0);
			if (NOAAmintempcomp2 < -99 || NOAAmintempcomp2 > 99)
			{
				NOAAmintempcomp2 = Units.Temp == 0 ? -18 : 0;
			}
			NOAAraincomp1 = ini.GetValue("NOAA", "RainComp1", -1000.0);
			if (NOAAraincomp1 < 0 || NOAAraincomp1 > 99)
			{
				NOAAraincomp1 = Units.Rain == 0 ? 0.2 : 0.01;
			}
			NOAAraincomp2 = ini.GetValue("NOAA", "RainComp2", -1000.0);
			if (NOAAraincomp2 < 0 || NOAAraincomp2 > 99)
			{
				NOAAraincomp2 = Units.Rain == 0 ? 2 : 0.1;
			}
			NOAAraincomp3 = ini.GetValue("NOAA", "RainComp3", -1000.0);
			if (NOAAraincomp3 < 0 || NOAAraincomp3 > 99)
			{
				NOAAraincomp3 = Units.Rain == 0 ? 20 : 1;
			}

			NOAAAutoSave = ini.GetValue("NOAA", "AutoSave", false);
			NOAAAutoFTP = ini.GetValue("NOAA", "AutoFTP", false);
			NOAAMonthFileFormat = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
			// Check for Cumulus 1 default format - and update
			if (NOAAMonthFileFormat == "'NOAAMO'mmyy'.txt'" || NOAAMonthFileFormat == "\"NOAAMO\"mmyy\".txt\"")
			{
				NOAAMonthFileFormat = "'NOAAMO'MMyy'.txt'";
			}
			NOAAYearFileFormat = ini.GetValue("NOAA", "YearFileFormat", "'NOAAYR'yyyy'.txt'");
			NOAAFTPDirectory = ini.GetValue("NOAA", "FTPDirectory", "");
			NOAAUseUTF8 = ini.GetValue("NOAA", "NOAAUseUTF8", true);
			NOAAUseDotDecimal = ini.GetValue("NOAA", "UseDotDecimal", false);

			NOAATempNorms[1] = ini.GetValue("NOAA", "NOAATempNormJan", -1000.0);
			NOAATempNorms[2] = ini.GetValue("NOAA", "NOAATempNormFeb", -1000.0);
			NOAATempNorms[3] = ini.GetValue("NOAA", "NOAATempNormMar", -1000.0);
			NOAATempNorms[4] = ini.GetValue("NOAA", "NOAATempNormApr", -1000.0);
			NOAATempNorms[5] = ini.GetValue("NOAA", "NOAATempNormMay", -1000.0);
			NOAATempNorms[6] = ini.GetValue("NOAA", "NOAATempNormJun", -1000.0);
			NOAATempNorms[7] = ini.GetValue("NOAA", "NOAATempNormJul", -1000.0);
			NOAATempNorms[8] = ini.GetValue("NOAA", "NOAATempNormAug", -1000.0);
			NOAATempNorms[9] = ini.GetValue("NOAA", "NOAATempNormSep", -1000.0);
			NOAATempNorms[10] = ini.GetValue("NOAA", "NOAATempNormOct", -1000.0);
			NOAATempNorms[11] = ini.GetValue("NOAA", "NOAATempNormNov", -1000.0);
			NOAATempNorms[12] = ini.GetValue("NOAA", "NOAATempNormDec", -1000.0);

			NOAARainNorms[1] = ini.GetValue("NOAA", "NOAARainNormJan", -1000.0);
			NOAARainNorms[2] = ini.GetValue("NOAA", "NOAARainNormFeb", -1000.0);
			NOAARainNorms[3] = ini.GetValue("NOAA", "NOAARainNormMar", -1000.0);
			NOAARainNorms[4] = ini.GetValue("NOAA", "NOAARainNormApr", -1000.0);
			NOAARainNorms[5] = ini.GetValue("NOAA", "NOAARainNormMay", -1000.0);
			NOAARainNorms[6] = ini.GetValue("NOAA", "NOAARainNormJun", -1000.0);
			NOAARainNorms[7] = ini.GetValue("NOAA", "NOAARainNormJul", -1000.0);
			NOAARainNorms[8] = ini.GetValue("NOAA", "NOAARainNormAug", -1000.0);
			NOAARainNorms[9] = ini.GetValue("NOAA", "NOAARainNormSep", -1000.0);
			NOAARainNorms[10] = ini.GetValue("NOAA", "NOAARainNormOct", -1000.0);
			NOAARainNorms[11] = ini.GetValue("NOAA", "NOAARainNormNov", -1000.0);
			NOAARainNorms[12] = ini.GetValue("NOAA", "NOAARainNormDec", -1000.0);

			HTTPProxyName = ini.GetValue("Proxies", "HTTPProxyName", "");
			HTTPProxyPort = ini.GetValue("Proxies", "HTTPProxyPort", 0);
			HTTPProxyUser = ini.GetValue("Proxies", "HTTPProxyUser", "");
			HTTPProxyPassword = ini.GetValue("Proxies", "HTTPProxyPassword", "");

			NumWindRosePoints = ini.GetValue("Display", "NumWindRosePoints", 16);
			WindRoseAngle = 360.0 / NumWindRosePoints;
			DisplayOptions.UseApparent = ini.GetValue("Display", "UseApparent", false);
			DisplayOptions.ShowSolar = ini.GetValue("Display", "DisplaySolarData", false);
			DisplayOptions.ShowUV = ini.GetValue("Display", "DisplayUvData", false);

			// MySQL - common
			MySqlConnSettings.Server = ini.GetValue("MySQL", "Host", "127.0.0.1");
			MySqlConnSettings.Port = (uint)ini.GetValue("MySQL", "Port", 3306);
			MySqlConnSettings.UserID = ini.GetValue("MySQL", "User", "");
			MySqlConnSettings.Password = ini.GetValue("MySQL", "Pass", "");
			MySqlConnSettings.Database = ini.GetValue("MySQL", "Database", "database");

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

			// Select-a-Chart settings
			for (int i = 0; i < SelectaChartOptions.series.Length; i++)
			{
				SelectaChartOptions.series[i] = ini.GetValue("Select-a-Chart", "Series" + i, "0");
				SelectaChartOptions.colours[i] = ini.GetValue("Select-a-Chart", "Colour" + i, "");
			}

			// Email settings
			SmtpOptions.Enabled = ini.GetValue("SMTP", "Enabled", false);
			SmtpOptions.Server = ini.GetValue("SMTP", "ServerName", "");
			SmtpOptions.Port = ini.GetValue("SMTP", "Port", 587);
			SmtpOptions.SslOption = ini.GetValue("SMTP", "SSLOption", 1);
			SmtpOptions.RequiresAuthentication = ini.GetValue("SMTP", "RequiresAuthentication", false);
			SmtpOptions.User = ini.GetValue("SMTP", "User", "");
			SmtpOptions.Password = ini.GetValue("SMTP", "Password", "");

			// Growing Degree Days
			GrowingBase1 = ini.GetValue("GrowingDD", "BaseTemperature1", (Units.Temp == 0 ? 5.0 : 40.0));
			GrowingBase2 = ini.GetValue("GrowingDD", "BaseTemperature2", (Units.Temp == 0 ? 10.0 : 50.0));
			GrowingYearStarts = ini.GetValue("GrowingDD", "YearStarts", (Latitude >= 0 ? 1 : 7));
			GrowingCap30C = ini.GetValue("GrowingDD", "Cap30C", true);

			// Temperature Sum
			TempSumYearStarts = ini.GetValue("TempSum", "TempSumYearStart", (Latitude >= 0 ? 1 : 7));
			if (TempSumYearStarts < 1 || TempSumYearStarts > 12)
				TempSumYearStarts = 1;
			TempSumBase1 = ini.GetValue("TempSum", "BaseTemperature1", GrowingBase1);
			TempSumBase2 = ini.GetValue("TempSum", "BaseTemperature2", GrowingBase2);
		}

		internal void WriteIniFile()
		{
			LogMessage("Writing Cumulus.ini file");

			IniFile ini = new IniFile("Cumulus.ini");

			ini.SetValue("Program", "EnableAccessibility", ProgramOptions.EnableAccessibility);

			ini.SetValue("Program", "StartupPingHost", ProgramOptions.StartupPingHost);
			ini.SetValue("Program", "StartupPingEscapeTime", ProgramOptions.StartupPingEscapeTime);

			ini.SetValue("Program", "StartupDelaySecs", ProgramOptions.StartupDelaySecs);
			ini.SetValue("Program", "StartupDelayMaxUptime", ProgramOptions.StartupDelayMaxUptime);

			ini.SetValue("Station", "WarnMultiple", ProgramOptions.WarnMultiple);

			ini.SetValue("Station", "Type", StationType);
			ini.SetValue("Station", "Model", StationModel);
			ini.SetValue("Station", "ComportName", ComportName);
			ini.SetValue("Station", "Latitude", Latitude);
			ini.SetValue("Station", "Longitude", Longitude);
			ini.SetValue("Station", "LatTxt", LatTxt);
			ini.SetValue("Station", "LonTxt", LonTxt);
			ini.SetValue("Station", "Altitude", Altitude);
			ini.SetValue("Station", "AltitudeInFeet", AltitudeInFeet);
			ini.SetValue("Station", "Humidity98Fix", StationOptions.Humidity98Fix);
			ini.SetValue("Station", "Wind10MinAverage", StationOptions.UseWind10MinAve);
			ini.SetValue("Station", "UseSpeedForAvgCalc", StationOptions.UseSpeedForAvgCalc);
			ini.SetValue("Station", "AvgBearingMinutes", StationOptions.AvgBearingMinutes);
			ini.SetValue("Station", "AvgSpeedMinutes", StationOptions.AvgSpeedMinutes);
			ini.SetValue("Station", "PeakGustMinutes", StationOptions.PeakGustMinutes);

			ini.SetValue("Station", "Logging", ProgramOptions.DebugLogging);
			ini.SetValue("Station", "DataLogging", ProgramOptions.DataLogging);

			ini.SetValue("Station", "DavisReadReceptionStats", DavisOptions.ReadReceptionStats);
			ini.SetValue("Station", "DavisSetLoggerInterval", DavisOptions.SetLoggerInterval);
			ini.SetValue("Station", "UseDavisLoop2", DavisOptions.UseLoop2);
			ini.SetValue("Station", "DavisInitWaitTime", DavisOptions.InitWaitTime);
			ini.SetValue("Station", "DavisIPResponseTime", DavisOptions.IPResponseTime);
			ini.SetValue("Station", "DavisBaudRate", DavisOptions.BaudRate);
			ini.SetValue("Station", "VPrainGaugeType", DavisOptions.RainGaugeType);
			ini.SetValue("Station", "VP2ConnectionType", DavisOptions.ConnectionType);
			ini.SetValue("Station", "VP2TCPPort", DavisOptions.TCPPort);
			ini.SetValue("Station", "VP2IPAddr", DavisOptions.IPAddr);
			ini.SetValue("Station", "VP2PeriodicDisconnectInterval", DavisOptions.PeriodicDisconnectInterval);
			ini.SetValue("Station", "ForceVPBarUpdate", DavisOptions.ForceVPBarUpdate);

			ini.SetValue("Station", "NoSensorCheck", StationOptions.NoSensorCheck);
			ini.SetValue("Station", "CalculatedDP", StationOptions.CalculatedDP);
			ini.SetValue("Station", "CalculatedWC", StationOptions.CalculatedWC);
			ini.SetValue("Station", "RolloverHour", RolloverHour);
			ini.SetValue("Station", "Use10amInSummer", Use10amInSummer);
			//ini.SetValue("Station", "ConfirmClose", ConfirmClose);
			//ini.SetValue("Station", "CloseOnSuspend", CloseOnSuspend);
			//ini.SetValue("Station", "RestartIfUnplugged", RestartIfUnplugged);
			//ini.SetValue("Station", "RestartIfDataStops", RestartIfDataStops);
			ini.SetValue("Station", "SyncDavisClock", StationOptions.SyncTime);
			ini.SetValue("Station", "ClockSettingHour", StationOptions.ClockSettingHour);
			ini.SetValue("Station", "WS2300IgnoreStationClock", StationOptions.WS2300IgnoreStationClock);
			ini.SetValue("Station", "LogExtraSensors", StationOptions.LogExtraSensors);
			ini.SetValue("Station", "DataLogInterval", DataLogInterval);

			ini.SetValue("Station", "SyncFOReads", FineOffsetOptions.SyncReads);
			ini.SetValue("Station", "FOReadAvoidPeriod", FineOffsetOptions.ReadAvoidPeriod);
			ini.SetValue("Station", "FineOffsetReadTime", FineOffsetOptions.ReadTime);
			ini.SetValue("Station", "FineOffsetSetLoggerInterval", FineOffsetOptions.SetLoggerInterval);
			ini.SetValue("Station", "VendorID", FineOffsetOptions.VendorID);
			ini.SetValue("Station", "ProductID", FineOffsetOptions.ProductID);


			ini.SetValue("Station", "WindUnit", Units.Wind);
			ini.SetValue("Station", "PressureUnit", Units.Press);
			ini.SetValue("Station", "RainUnit", Units.Rain);
			ini.SetValue("Station", "TempUnit", Units.Temp);

			ini.SetValue("Station", "WindSpeedDecimals", WindDPlaces);
			ini.SetValue("Station", "WindSpeedAvgDecimals", WindAvgDPlaces);
			ini.SetValue("Station", "WindRunDecimals", WindRunDPlaces);
			ini.SetValue("Station", "SunshineHrsDecimals", SunshineDPlaces);
			ini.SetValue("Station", "PressDecimals", PressDPlaces);
			ini.SetValue("Station", "RainDecimals", RainDPlaces);
			ini.SetValue("Station", "TempDecimals", TempDPlaces);
			ini.SetValue("Station", "UVDecimals", UVDPlaces);
			ini.SetValue("Station", "AirQualityDecimals", AirQualityDPlaces);


			ini.SetValue("Station", "LocName", LocationName);
			ini.SetValue("Station", "LocDesc", LocationDesc);
			ini.SetValue("Station", "StartDate", RecordsBeganDate);
			ini.SetValue("Station", "YTDrain", YTDrain);
			ini.SetValue("Station", "YTDrainyear", YTDrainyear);
			ini.SetValue("Station", "UseDataLogger", UseDataLogger);
			ini.SetValue("Station", "UseCumulusForecast", UseCumulusForecast);
			ini.SetValue("Station", "HourlyForecast", HourlyForecast);
			ini.SetValue("Station", "UseCumulusPresstrendstr", StationOptions.UseCumulusPresstrendstr);
			ini.SetValue("Station", "FCpressinMB", FCpressinMB);
			ini.SetValue("Station", "FClowpress", FClowpress);
			ini.SetValue("Station", "FChighpress", FChighpress);
			ini.SetValue("Station", "UseZeroBearing", StationOptions.UseZeroBearing);
			ini.SetValue("Station", "RoundWindSpeed", StationOptions.RoundWindSpeed);
			ini.SetValue("Station", "PrimaryAqSensor", StationOptions.PrimaryAqSensor);

			ini.SetValue("Station", "EWInterval", EwOptions.Interval);
			ini.SetValue("Station", "EWFile", EwOptions.Filename);
			ini.SetValue("Station", "EWminpressureMB", EwOptions.MinPressMB);
			ini.SetValue("Station", "EWmaxpressureMB", EwOptions.MaxPressMB);
			ini.SetValue("Station", "EWMaxRainTipDiff", EwOptions.MaxRainTipDiff);
			ini.SetValue("Station", "EWpressureoffset", EwOptions.PressOffset);

			ini.SetValue("Station", "EWtempdiff", Spike.TempDiff);
			ini.SetValue("Station", "EWpressurediff", Spike.PressDiff);
			ini.SetValue("Station", "EWhumiditydiff", Spike.HumidityDiff);
			ini.SetValue("Station", "EWgustdiff", Spike.GustDiff);
			ini.SetValue("Station", "EWwinddiff", Spike.WindDiff);
			ini.SetValue("Station", "EWmaxHourlyRain", Spike.MaxHourlyRain);
			ini.SetValue("Station", "EWmaxRainRate", Spike.MaxRainRate);

			ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
			ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

			ini.SetValue("Station", "ChillHourSeasonStart", ChillHourSeasonStart);
			ini.SetValue("Station", "ChillHourThreshold", ChillHourThreshold);

			ini.SetValue("Station", "ErrorLogSpikeRemoval", ErrorLogSpikeRemoval);

			ini.SetValue("Station", "ImetBaudRate", ImetOptions.BaudRate);
			ini.SetValue("Station", "ImetWaitTime", ImetOptions.WaitTime);					// delay to wait for a reply to a command
			ini.SetValue("Station", "ImetReadDelay", ImetOptions.ReadDelay);				// delay between sending read live data commands
			ini.SetValue("Station", "ImetUpdateLogPointer", ImetOptions.UpdateLogPointer);	// keep the logger pointer pointing at last data read

			ini.SetValue("Station", "RG11Enabled", RG11Enabled);
			ini.SetValue("Station", "RG11portName", RG11Port);
			ini.SetValue("Station", "RG11TBRmode", RG11TBRmode);
			ini.SetValue("Station", "RG11tipsize", RG11tipsize);
			ini.SetValue("Station", "RG11IgnoreFirst", RG11IgnoreFirst);
			ini.SetValue("Station", "RG11DTRmode", RG11DTRmode);

			ini.SetValue("Station", "RG11Enabled2", RG11Enabled2);
			ini.SetValue("Station", "RG11portName2", RG11Port2);
			ini.SetValue("Station", "RG11TBRmode2", RG11TBRmode2);
			ini.SetValue("Station", "RG11tipsize2", RG11tipsize2);
			ini.SetValue("Station", "RG11IgnoreFirst2", RG11IgnoreFirst2);
			ini.SetValue("Station", "RG11DTRmode2", RG11DTRmode2);

			// WeatherLink Live device settings
			ini.SetValue("WLL", "AutoUpdateIpAddress", WLLAutoUpdateIpAddress);
			ini.SetValue("WLL", "WLv2ApiKey", WllApiKey);
			ini.SetValue("WLL", "WLv2ApiSecret", WllApiSecret);
			ini.SetValue("WLL", "WLStationId", WllStationId);
			ini.SetValue("WLL", "PrimaryRainTxId", WllPrimaryRain);
			ini.SetValue("WLL", "PrimaryTempHumTxId", WllPrimaryTempHum);
			ini.SetValue("WLL", "PrimaryWindTxId", WllPrimaryWind);
			ini.SetValue("WLL", "PrimaryRainTxId", WllPrimaryRain);
			ini.SetValue("WLL", "PrimarySolarTxId", WllPrimarySolar);
			ini.SetValue("WLL", "PrimaryUvTxId", WllPrimaryUV);
			ini.SetValue("WLL", "ExtraSoilTempTxId1", WllExtraSoilTempTx1);
			ini.SetValue("WLL", "ExtraSoilTempIdx1", WllExtraSoilTempIdx1);
			ini.SetValue("WLL", "ExtraSoilTempTxId2", WllExtraSoilTempTx2);
			ini.SetValue("WLL", "ExtraSoilTempIdx2", WllExtraSoilTempIdx2);
			ini.SetValue("WLL", "ExtraSoilTempTxId3", WllExtraSoilTempTx3);
			ini.SetValue("WLL", "ExtraSoilTempIdx3", WllExtraSoilTempIdx3);
			ini.SetValue("WLL", "ExtraSoilTempTxId4", WllExtraSoilTempTx4);
			ini.SetValue("WLL", "ExtraSoilTempIdx4", WllExtraSoilTempIdx4);
			ini.SetValue("WLL", "ExtraSoilMoistureTxId1", WllExtraSoilMoistureTx1);
			ini.SetValue("WLL", "ExtraSoilMoistureIdx1", WllExtraSoilMoistureIdx1);
			ini.SetValue("WLL", "ExtraSoilMoistureTxId2", WllExtraSoilMoistureTx2);
			ini.SetValue("WLL", "ExtraSoilMoistureIdx2", WllExtraSoilMoistureIdx2);
			ini.SetValue("WLL", "ExtraSoilMoistureTxId3", WllExtraSoilMoistureTx3);
			ini.SetValue("WLL", "ExtraSoilMoistureIdx3", WllExtraSoilMoistureIdx3);
			ini.SetValue("WLL", "ExtraSoilMoistureTxId4", WllExtraSoilMoistureTx4);
			ini.SetValue("WLL", "ExtraSoilMoistureIdx4", WllExtraSoilMoistureIdx4);
			ini.SetValue("WLL", "ExtraLeafTxId1", WllExtraLeafTx1);
			ini.SetValue("WLL", "ExtraLeafIdx1", WllExtraLeafIdx1);
			ini.SetValue("WLL", "ExtraLeafTxId2", WllExtraLeafTx2);
			ini.SetValue("WLL", "ExtraLeafIdx2", WllExtraLeafIdx2);
			for (int i = 1; i <= 8; i++)
			{
				ini.SetValue("WLL", "ExtraTempTxId" + i, WllExtraTempTx[i - 1]);
				ini.SetValue("WLL", "ExtraHumOnTxId" + i, WllExtraHumTx[i - 1]);
			}

			// GW1000 settings
			ini.SetValue("GW1000", "IPAddress", Gw1000IpAddress);
			ini.SetValue("GW1000", "MACAddress", Gw1000MacAddress);
			ini.SetValue("GW1000", "AutoUpdateIpAddress", Gw1000AutoUpdateIpAddress);

			// AirLink settings
			ini.SetValue("AirLink", "IsWllNode", AirLinkIsNode);
			ini.SetValue("AirLink", "WLv2ApiKey", AirLinkApiKey);
			ini.SetValue("AirLink", "WLv2ApiSecret", AirLinkApiSecret);
			ini.SetValue("AirLink", "AutoUpdateIpAddress", AirLinkAutoUpdateIpAddress);
			ini.SetValue("AirLink", "In-Enabled", AirLinkInEnabled);
			ini.SetValue("AirLink", "In-IPAddress", AirLinkInIPAddr);
			ini.SetValue("AirLink", "In-WLStationId", AirLinkInStationId);
			ini.SetValue("AirLink", "In-Hostname", AirLinkInHostName);

			ini.SetValue("AirLink", "Out-Enabled", AirLinkOutEnabled);
			ini.SetValue("AirLink", "Out-IPAddress", AirLinkOutIPAddr);
			ini.SetValue("AirLink", "Out-WLStationId", AirLinkOutStationId);
			ini.SetValue("AirLink", "Out-Hostname", AirLinkOutHostName);
			ini.SetValue("AirLink", "AQIformula", airQualityIndex);

			ini.SetValue("Web Site", "ForumURL", ForumURL);
			ini.SetValue("Web Site", "WebcamURL", WebcamURL);

			ini.SetValue("FTP site", "Host", FtpHostname);
			ini.SetValue("FTP site", "Port", FtpHostPort);
			ini.SetValue("FTP site", "Username", FtpUsername);
			ini.SetValue("FTP site", "Password", FtpPassword);
			ini.SetValue("FTP site", "Directory", FtpDirectory);

			ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);
			ini.SetValue("FTP site", "Sslftp", (int)Sslftp);
			// BUILD 3092 - added alternate SFTP authenication options
			ini.SetValue("FTP site", "SshFtpAuthentication", SshftpAuthentication);
			ini.SetValue("FTP site", "SshFtpPskFile", SshftpPskFile);

			ini.SetValue("FTP site", "FTPlogging", FTPlogging);
			ini.SetValue("FTP site", "UTF8encode", UTF8encode);
			ini.SetValue("FTP site", "EnableRealtime", RealtimeEnabled);
			ini.SetValue("FTP site", "RealtimeInterval", RealtimeInterval);
			ini.SetValue("FTP site", "RealtimeFTPEnabled", RealtimeFTPEnabled);
			ini.SetValue("FTP site", "RealtimeTxtCreate", RealtimeFiles[0].Create);
			ini.SetValue("FTP site", "RealtimeTxtFTP", RealtimeFiles[0].FTP);
			ini.SetValue("FTP site", "RealtimeGaugesTxtCreate", RealtimeFiles[1].Create);
			ini.SetValue("FTP site", "RealtimeGaugesTxtFTP", RealtimeFiles[1].FTP);

			ini.SetValue("FTP site", "IntervalEnabled", WebIntervalEnabled);
			ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
			for (var i = 0; i < StdWebFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, StdWebFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, StdWebFiles[i].FTP);
			}

			for (var i = 0; i < GraphDataFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, GraphDataFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, GraphDataFiles[i].FTP);
			}

			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, GraphDataEodFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, GraphDataEodFiles[i].FTP);
			}

			ini.SetValue("FTP site", "IncludeMoonImage", IncludeMoonImage);
			ini.SetValue("FTP site", "FTPRename", FTPRename);
			ini.SetValue("FTP site", "DeleteBeforeUpload", DeleteBeforeUpload);
			ini.SetValue("FTP site", "ActiveFTP", ActiveFTPMode);
			ini.SetValue("FTP site", "DisableEPSV", DisableFtpsEPSV);
			ini.SetValue("FTP site", "DisableFtpsExplicit", DisableFtpsExplicit);


			for (int i = 0; i < numextrafiles; i++)
			{
				ini.SetValue("FTP site", "ExtraLocal" + i, ExtraFiles[i].local);
				ini.SetValue("FTP site", "ExtraRemote" + i, ExtraFiles[i].remote);
				ini.SetValue("FTP site", "ExtraProcess" + i, ExtraFiles[i].process);
				ini.SetValue("FTP site", "ExtraBinary" + i, ExtraFiles[i].binary);
				ini.SetValue("FTP site", "ExtraRealtime" + i, ExtraFiles[i].realtime);
				ini.SetValue("FTP site", "ExtraFTP" + i, ExtraFiles[i].FTP);
				ini.SetValue("FTP site", "ExtraUTF" + i, ExtraFiles[i].UTF8);
				ini.SetValue("FTP site", "ExtraEOD" + i, ExtraFiles[i].endofday);
			}

			ini.SetValue("FTP site", "ExternalProgram", ExternalProgram);
			ini.SetValue("FTP site", "RealtimeProgram", RealtimeProgram);
			ini.SetValue("FTP site", "DailyProgram", DailyProgram);
			ini.SetValue("FTP site", "ExternalParams", ExternalParams);
			ini.SetValue("FTP site", "RealtimeParams", RealtimeParams);
			ini.SetValue("FTP site", "DailyParams", DailyParams);

			ini.SetValue("Station", "CloudBaseInFeet", CloudBaseInFeet);

			ini.SetValue("Wunderground", "ID", Wund.ID);
			ini.SetValue("Wunderground", "Password", Wund.PW);
			ini.SetValue("Wunderground", "Enabled", Wund.Enabled);
			ini.SetValue("Wunderground", "RapidFire", Wund.RapidFireEnabled);
			ini.SetValue("Wunderground", "Interval", Wund.Interval);
			ini.SetValue("Wunderground", "SendUV", Wund.SendUV);
			ini.SetValue("Wunderground", "SendSR", Wund.SendSolar);
			ini.SetValue("Wunderground", "SendIndoor", Wund.SendIndoor);
			ini.SetValue("Wunderground", "SendAverage", Wund.SendAverage);
			ini.SetValue("Wunderground", "CatchUp", Wund.CatchUp);
			ini.SetValue("Wunderground", "SendSoilTemp1", Wund.SendSoilTemp1);
			ini.SetValue("Wunderground", "SendSoilTemp2", Wund.SendSoilTemp2);
			ini.SetValue("Wunderground", "SendSoilTemp3", Wund.SendSoilTemp3);
			ini.SetValue("Wunderground", "SendSoilTemp4", Wund.SendSoilTemp4);
			ini.SetValue("Wunderground", "SendSoilMoisture1", Wund.SendSoilMoisture1);
			ini.SetValue("Wunderground", "SendSoilMoisture2", Wund.SendSoilMoisture2);
			ini.SetValue("Wunderground", "SendSoilMoisture3", Wund.SendSoilMoisture3);
			ini.SetValue("Wunderground", "SendSoilMoisture4", Wund.SendSoilMoisture4);
			ini.SetValue("Wunderground", "SendLeafWetness1", Wund.SendLeafWetness1);
			ini.SetValue("Wunderground", "SendLeafWetness2", Wund.SendLeafWetness2);
			ini.SetValue("Wunderground", "SendAirQuality", Wund.SendAirQuality);

			ini.SetValue("Windy", "APIkey", Windy.ApiKey);
			ini.SetValue("Windy", "StationIdx", Windy.StationIdx);
			ini.SetValue("Windy", "Enabled", Windy.Enabled);
			ini.SetValue("Windy", "Interval", Windy.Interval);
			ini.SetValue("Windy", "SendUV", Windy.SendUV);
			ini.SetValue("Windy", "CatchUp", Windy.CatchUp);

			ini.SetValue("Awekas", "User", AWEKAS.ID);
			ini.SetValue("Awekas", "Password", AWEKAS.PW);
			ini.SetValue("Awekas", "Language", AWEKAS.Lang);
			ini.SetValue("Awekas", "Enabled", AWEKAS.Enabled);
			ini.SetValue("Awekas", "Interval", AWEKAS.Interval);
			ini.SetValue("Awekas", "SendUV", AWEKAS.SendUV);
			ini.SetValue("Awekas", "SendSR", AWEKAS.SendSolar);
			ini.SetValue("Awekas", "SendSoilTemp", AWEKAS.SendSoilTemp);
			ini.SetValue("Awekas", "SendIndoor", AWEKAS.SendIndoor);
			ini.SetValue("Awekas", "SendSoilMoisture", AWEKAS.SendSoilMoisture);
			ini.SetValue("Awekas", "SendLeafWetness", AWEKAS.SendLeafWetness);
			ini.SetValue("Awekas", "SendAirQuality", AWEKAS.SendAirQuality);

			ini.SetValue("WeatherCloud", "Wid", WCloud.ID);
			ini.SetValue("WeatherCloud", "Key", WCloud.PW);
			ini.SetValue("WeatherCloud", "Enabled", WCloud.Enabled);
			ini.SetValue("WeatherCloud", "Interval", WCloud.Interval);
			ini.SetValue("WeatherCloud", "SendUV", WCloud.SendUV);
			ini.SetValue("WeatherCloud", "SendSR", WCloud.SendSolar);
			ini.SetValue("WeatherCloud", "SendAQI", WCloud.SendAirQuality);
			ini.SetValue("WeatherCloud", "SendSoilMoisture", WCloud.SendSoilMoisture);
			ini.SetValue("WeatherCloud", "SoilMoistureSensor", WCloud.SoilMoistureSensor);
			ini.SetValue("WeatherCloud", "SendLeafWetness", WCloud.SendLeafWetness);
			ini.SetValue("WeatherCloud", "LeafWetnessSensor", WCloud.LeafWetnessSensor);

			ini.SetValue("Twitter", "User", Twitter.ID);
			ini.SetValue("Twitter", "Password", Twitter.PW);
			ini.SetValue("Twitter", "Enabled", Twitter.Enabled);
			ini.SetValue("Twitter", "Interval", Twitter.Interval);
			ini.SetValue("Twitter", "OauthToken", Twitter.OauthToken);
			ini.SetValue("Twitter", "OauthTokenSecret", Twitter.OauthTokenSecret);
			ini.SetValue("Twitter", "TwitterSendLocation", Twitter.SendLocation);

			ini.SetValue("PWSweather", "ID", PWS.ID);
			ini.SetValue("PWSweather", "Password", PWS.PW);
			ini.SetValue("PWSweather", "Enabled", PWS.Enabled);
			ini.SetValue("PWSweather", "Interval", PWS.Interval);
			ini.SetValue("PWSweather", "SendUV", PWS.SendUV);
			ini.SetValue("PWSweather", "SendSR", PWS.SendSolar);
			ini.SetValue("PWSweather", "CatchUp", PWS.CatchUp);

			ini.SetValue("WOW", "ID", WOW.ID);
			ini.SetValue("WOW", "Password", WOW.PW);
			ini.SetValue("WOW", "Enabled", WOW.Enabled);
			ini.SetValue("WOW", "Interval", WOW.Interval);
			ini.SetValue("WOW", "SendUV", WOW.SendUV);
			ini.SetValue("WOW", "SendSR", WOW.SendSolar);
			ini.SetValue("WOW", "CatchUp", WOW.CatchUp);

			ini.SetValue("APRS", "ID", APRS.ID);
			ini.SetValue("APRS", "pass", APRS.PW);
			ini.SetValue("APRS", "server", APRS.Server);
			ini.SetValue("APRS", "port", APRS.Port);
			ini.SetValue("APRS", "Enabled", APRS.Enabled);
			ini.SetValue("APRS", "Interval", APRS.Interval);
			ini.SetValue("APRS", "SendSR", APRS.SendSolar);
			ini.SetValue("APRS", "APRSHumidityCutoff", APRS.HumidityCutoff);

			ini.SetValue("OpenWeatherMap", "Enabled", OpenWeatherMap.Enabled);
			ini.SetValue("OpenWeatherMap", "CatchUp", OpenWeatherMap.CatchUp);
			ini.SetValue("OpenWeatherMap", "APIkey", OpenWeatherMap.PW);
			ini.SetValue("OpenWeatherMap", "StationId", OpenWeatherMap.ID);
			ini.SetValue("OpenWeatherMap", "Interval", OpenWeatherMap.Interval);

			ini.SetValue("WindGuru", "Enabled", WindGuru.Enabled);
			ini.SetValue("WindGuru", "StationUID", WindGuru.ID);
			ini.SetValue("WindGuru", "Password", WindGuru.PW);
			ini.SetValue("WindGuru", "Interval", WindGuru.Interval);
			ini.SetValue("WindGuru", "SendRain", WindGuru.SendRain);

			ini.SetValue("MQTT", "Server", MQTT.Server);
			ini.SetValue("MQTT", "Port", MQTT.Port);
			ini.SetValue("MQTT", "UseTLS", MQTT.UseTLS);
			ini.SetValue("MQTT", "Username", MQTT.Username);
			ini.SetValue("MQTT", "Password", MQTT.Password);
			ini.SetValue("MQTT", "EnableDataUpdate", MQTT.EnableDataUpdate);
			ini.SetValue("MQTT", "UpdateTopic", MQTT.UpdateTopic);
			ini.SetValue("MQTT", "UpdateTemplate", MQTT.UpdateTemplate);
			ini.SetValue("MQTT", "UpdateRetained", MQTT.UpdateRetained);
			ini.SetValue("MQTT", "EnableInterval", MQTT.EnableInterval);
			ini.SetValue("MQTT", "IntervalTime", MQTT.IntervalTime);
			ini.SetValue("MQTT", "IntervalTopic", MQTT.IntervalTopic);
			ini.SetValue("MQTT", "IntervalTemplate", MQTT.IntervalTemplate);
			ini.SetValue("MQTT", "IntervalRetained", MQTT.IntervalRetained);

			ini.SetValue("Alarms", "alarmlowtemp", LowTempAlarm.Value);
			ini.SetValue("Alarms", "LowTempAlarmSet", LowTempAlarm.Enabled);
			ini.SetValue("Alarms", "LowTempAlarmSound", LowTempAlarm.Sound);
			ini.SetValue("Alarms", "LowTempAlarm.SoundFile", LowTempAlarm.SoundFile);
			ini.SetValue("Alarms", "LowTempAlarmNotify", LowTempAlarm.Notify);
			ini.SetValue("Alarms", "LowTempAlarmEmail", LowTempAlarm.Email);
			ini.SetValue("Alarms", "LowTempAlarmLatch", LowTempAlarm.Latch);
			ini.SetValue("Alarms", "LowTempAlarmLatchHours", LowTempAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmhightemp", HighTempAlarm.Value);
			ini.SetValue("Alarms", "HighTempAlarmSet", HighTempAlarm.Enabled);
			ini.SetValue("Alarms", "HighTempAlarmSound", HighTempAlarm.Sound);
			ini.SetValue("Alarms", "HighTempAlarmSoundFile", HighTempAlarm.SoundFile);
			ini.SetValue("Alarms", "HighTempAlarmNotify", HighTempAlarm.Notify);
			ini.SetValue("Alarms", "HighTempAlarmEmail", HighTempAlarm.Email);
			ini.SetValue("Alarms", "HighTempAlarmLatch", HighTempAlarm.Latch);
			ini.SetValue("Alarms", "HighTempAlarmLatchHours", HighTempAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmtempchange", TempChangeAlarm.Value);
			ini.SetValue("Alarms", "TempChangeAlarmSet", TempChangeAlarm.Enabled);
			ini.SetValue("Alarms", "TempChangeAlarmSound", TempChangeAlarm.Sound);
			ini.SetValue("Alarms", "TempChangeAlarmSoundFile", TempChangeAlarm.SoundFile);
			ini.SetValue("Alarms", "TempChangeAlarmNotify", TempChangeAlarm.Notify);
			ini.SetValue("Alarms", "TempChangeAlarmEmail", TempChangeAlarm.Email);
			ini.SetValue("Alarms", "TempChangeAlarmLatch", TempChangeAlarm.Latch);
			ini.SetValue("Alarms", "TempChangeAlarmLatchHours", TempChangeAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmlowpress", LowPressAlarm.Value);
			ini.SetValue("Alarms", "LowPressAlarmSet", LowPressAlarm.Enabled);
			ini.SetValue("Alarms", "LowPressAlarmSound", LowPressAlarm.Sound);
			ini.SetValue("Alarms", "LowPressAlarmSoundFile", LowPressAlarm.SoundFile);
			ini.SetValue("Alarms", "LowPressAlarmNotify", LowPressAlarm.Notify);
			ini.SetValue("Alarms", "LowPressAlarmEmail", LowPressAlarm.Email);
			ini.SetValue("Alarms", "LowPressAlarmLatch", LowPressAlarm.Latch);
			ini.SetValue("Alarms", "LowPressAlarmLatchHours", LowPressAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmhighpress", HighPressAlarm.Value);
			ini.SetValue("Alarms", "HighPressAlarmSet", HighPressAlarm.Enabled);
			ini.SetValue("Alarms", "HighPressAlarmSound", HighPressAlarm.Sound);
			ini.SetValue("Alarms", "HighPressAlarmSoundFile", HighPressAlarm.SoundFile);
			ini.SetValue("Alarms", "HighPressAlarmNotify", HighPressAlarm.Notify);
			ini.SetValue("Alarms", "HighPressAlarmEmail", HighPressAlarm.Email);
			ini.SetValue("Alarms", "HighPressAlarmLatch", HighPressAlarm.Latch);
			ini.SetValue("Alarms", "HighPressAlarmLatchHours", HighPressAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmpresschange", PressChangeAlarm.Value);
			ini.SetValue("Alarms", "PressChangeAlarmSet", PressChangeAlarm.Enabled);
			ini.SetValue("Alarms", "PressChangeAlarmSound", PressChangeAlarm.Sound);
			ini.SetValue("Alarms", "PressChangeAlarmSoundFile", PressChangeAlarm.SoundFile);
			ini.SetValue("Alarms", "PressChangeAlarmNotify", PressChangeAlarm.Notify);
			ini.SetValue("Alarms", "PressChangeAlarmEmail", PressChangeAlarm.Email);
			ini.SetValue("Alarms", "PressChangeAlarmLatch", PressChangeAlarm.Latch);
			ini.SetValue("Alarms", "PressChangeAlarmLatchHours", PressChangeAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmhighraintoday", HighRainTodayAlarm.Value);
			ini.SetValue("Alarms", "HighRainTodayAlarmSet", HighRainTodayAlarm.Enabled);
			ini.SetValue("Alarms", "HighRainTodayAlarmSound", HighRainTodayAlarm.Sound);
			ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", HighRainTodayAlarm.SoundFile);
			ini.SetValue("Alarms", "HighRainTodayAlarmNotify", HighRainTodayAlarm.Notify);
			ini.SetValue("Alarms", "HighRainTodayAlarmEmail", HighRainTodayAlarm.Email);
			ini.SetValue("Alarms", "HighRainTodayAlarmLatch", HighRainTodayAlarm.Latch);
			ini.SetValue("Alarms", "HighRainTodayAlarmLatchHours", HighRainTodayAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmhighrainrate", HighRainRateAlarm.Value);
			ini.SetValue("Alarms", "HighRainRateAlarmSet", HighRainRateAlarm.Enabled);
			ini.SetValue("Alarms", "HighRainRateAlarmSound", HighRainRateAlarm.Sound);
			ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", HighRainRateAlarm.SoundFile);
			ini.SetValue("Alarms", "HighRainRateAlarmNotify", HighRainRateAlarm.Notify);
			ini.SetValue("Alarms", "HighRainRateAlarmEmail", HighRainRateAlarm.Email);
			ini.SetValue("Alarms", "HighRainRateAlarmLatch", HighRainRateAlarm.Latch);
			ini.SetValue("Alarms", "HighRainRateAlarmLatchHours", HighRainRateAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmhighgust", HighGustAlarm.Value);
			ini.SetValue("Alarms", "HighGustAlarmSet", HighGustAlarm.Enabled);
			ini.SetValue("Alarms", "HighGustAlarmSound", HighGustAlarm.Sound);
			ini.SetValue("Alarms", "HighGustAlarmSoundFile", HighGustAlarm.SoundFile);
			ini.SetValue("Alarms", "HighGustAlarmNotify", HighGustAlarm.Notify);
			ini.SetValue("Alarms", "HighGustAlarmEmail", HighGustAlarm.Email);
			ini.SetValue("Alarms", "HighGustAlarmLatch", HighGustAlarm.Latch);
			ini.SetValue("Alarms", "HighGustAlarmLatchHours", HighGustAlarm.LatchHours);

			ini.SetValue("Alarms", "alarmhighwind", HighWindAlarm.Value);
			ini.SetValue("Alarms", "HighWindAlarmSet", HighWindAlarm.Enabled);
			ini.SetValue("Alarms", "HighWindAlarmSound", HighWindAlarm.Sound);
			ini.SetValue("Alarms", "HighWindAlarmSoundFile", HighWindAlarm.SoundFile);
			ini.SetValue("Alarms", "HighWindAlarmNotify", HighWindAlarm.Notify);
			ini.SetValue("Alarms", "HighWindAlarmEmail", HighWindAlarm.Email);
			ini.SetValue("Alarms", "HighWindAlarmLatch", HighWindAlarm.Latch);
			ini.SetValue("Alarms", "HighWindAlarmLatchHours", HighWindAlarm.LatchHours);

			ini.SetValue("Alarms", "SensorAlarmSet", SensorAlarm.Enabled);
			ini.SetValue("Alarms", "SensorAlarmSound", SensorAlarm.Sound);
			ini.SetValue("Alarms", "SensorAlarmSoundFile", SensorAlarm.SoundFile);
			ini.SetValue("Alarms", "SensorAlarmNotify", SensorAlarm.Notify);
			ini.SetValue("Alarms", "SensorAlarmEmail", SensorAlarm.Email);
			ini.SetValue("Alarms", "SensorAlarmLatch", SensorAlarm.Latch);
			ini.SetValue("Alarms", "SensorAlarmLatchHours", SensorAlarm.LatchHours);

			ini.SetValue("Alarms", "DataStoppedAlarmSet", DataStoppedAlarm.Enabled);
			ini.SetValue("Alarms", "DataStoppedAlarmSound", DataStoppedAlarm.Sound);
			ini.SetValue("Alarms", "DataStoppedAlarmSoundFile", DataStoppedAlarm.SoundFile);
			ini.SetValue("Alarms", "DataStoppedAlarmNotify", DataStoppedAlarm.Notify);
			ini.SetValue("Alarms", "DataStoppedAlarmEmail", DataStoppedAlarm.Email);
			ini.SetValue("Alarms", "DataStoppedAlarmLatch", DataStoppedAlarm.Latch);
			ini.SetValue("Alarms", "DataStoppedAlarmLatchHours", DataStoppedAlarm.LatchHours);

			ini.SetValue("Alarms", "BatteryLowAlarmSet", BatteryLowAlarm.Enabled);
			ini.SetValue("Alarms", "BatteryLowAlarmSound", BatteryLowAlarm.Sound);
			ini.SetValue("Alarms", "BatteryLowAlarmSoundFile", BatteryLowAlarm.SoundFile);
			ini.SetValue("Alarms", "BatteryLowAlarmNotify", BatteryLowAlarm.Notify);
			ini.SetValue("Alarms", "BatteryLowAlarmEmail", BatteryLowAlarm.Email);
			ini.SetValue("Alarms", "BatteryLowAlarmLatch", BatteryLowAlarm.Latch);
			ini.SetValue("Alarms", "BatteryLowAlarmLatchHours", BatteryLowAlarm.LatchHours);

			ini.SetValue("Alarms", "DataSpikeAlarmSet", SpikeAlarm.Enabled);
			ini.SetValue("Alarms", "DataSpikeAlarmSound", SpikeAlarm.Sound);
			ini.SetValue("Alarms", "DataSpikeAlarmSoundFile", SpikeAlarm.SoundFile);
			ini.SetValue("Alarms", "DataSpikeAlarmNotify", SpikeAlarm.Notify);
			ini.SetValue("Alarms", "DataSpikeAlarmEmail", SpikeAlarm.Email);
			ini.SetValue("Alarms", "DataSpikeAlarmLatch", SpikeAlarm.Latch);
			ini.SetValue("Alarms", "DataSpikeAlarmLatchHours", SpikeAlarm.LatchHours);

			ini.SetValue("Alarms", "UpgradeAlarmSet", UpgradeAlarm.Enabled);
			ini.SetValue("Alarms", "UpgradeAlarmSound", UpgradeAlarm.Sound);
			ini.SetValue("Alarms", "UpgradeAlarmSoundFile", UpgradeAlarm.SoundFile);
			ini.SetValue("Alarms", "UpgradeAlarmNotify", UpgradeAlarm.Notify);
			ini.SetValue("Alarms", "UpgradeAlarmEmail", UpgradeAlarm.Email);
			ini.SetValue("Alarms", "UpgradeAlarmLatch", UpgradeAlarm.Latch);
			ini.SetValue("Alarms", "UpgradeAlarmLatchHours", UpgradeAlarm.LatchHours);

			ini.SetValue("Alarms", "FromEmail", AlarmFromEmail);
			ini.SetValue("Alarms", "DestEmail", AlarmDestEmail.Join(";"));
			ini.SetValue("Alarms", "UseHTML", AlarmEmailHtml);


			ini.SetValue("Offsets", "PressOffset", Calib.Press.Offset);
			ini.SetValue("Offsets", "TempOffset", Calib.Temp.Offset);
			ini.SetValue("Offsets", "HumOffset", Calib.Hum.Offset);
			ini.SetValue("Offsets", "WindDirOffset", Calib.WindDir.Offset);
			ini.SetValue("Offsets", "InTempOffset", Calib.InTemp.Offset);
			ini.SetValue("Offsets", "UVOffset", Calib.UV.Offset);
			ini.SetValue("Offsets", "SolarOffset", Calib.Solar.Offset);
			ini.SetValue("Offsets", "WetBulbOffset", Calib.WetBulb.Offset);
			//ini.SetValue("Offsets", "DavisCalcAltPressOffset", DavisCalcAltPressOffset);

			ini.SetValue("Offsets", "PressMult", Calib.Press.Mult);
			ini.SetValue("Offsets", "WindSpeedMult", Calib.WindSpeed.Mult);
			ini.SetValue("Offsets", "WindGustMult", Calib.WindGust.Mult);
			ini.SetValue("Offsets", "TempMult", Calib.Temp.Mult);
			ini.SetValue("Offsets", "HumMult", Calib.Hum.Mult);
			ini.SetValue("Offsets", "RainMult", Calib.Rain.Mult);
			ini.SetValue("Offsets", "SolarMult", Calib.Solar.Mult);
			ini.SetValue("Offsets", "UVMult", Calib.UV.Mult);
			ini.SetValue("Offsets", "WetBulbMult", Calib.WetBulb.Mult);

			ini.SetValue("Limits", "TempHighC", Limit.TempHigh);
			ini.SetValue("Limits", "TempLowC", Limit.TempLow);
			ini.SetValue("Limits", "DewHighC", Limit.DewHigh);
			ini.SetValue("Limits", "PressHighMB", Limit.PressHigh);
			ini.SetValue("Limits", "PressLowMB", Limit.PressLow);
			ini.SetValue("Limits", "WindHighMS", Limit.WindHigh);

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
			ini.SetValue("NOAA", "UseDotDecimal", NOAAUseDotDecimal);

			ini.SetValue("NOAA", "NOAATempNormJan", NOAATempNorms[1]);
			ini.SetValue("NOAA", "NOAATempNormFeb", NOAATempNorms[2]);
			ini.SetValue("NOAA", "NOAATempNormMar", NOAATempNorms[3]);
			ini.SetValue("NOAA", "NOAATempNormApr", NOAATempNorms[4]);
			ini.SetValue("NOAA", "NOAATempNormMay", NOAATempNorms[5]);
			ini.SetValue("NOAA", "NOAATempNormJun", NOAATempNorms[6]);
			ini.SetValue("NOAA", "NOAATempNormJul", NOAATempNorms[7]);
			ini.SetValue("NOAA", "NOAATempNormAug", NOAATempNorms[8]);
			ini.SetValue("NOAA", "NOAATempNormSep", NOAATempNorms[9]);
			ini.SetValue("NOAA", "NOAATempNormOct", NOAATempNorms[10]);
			ini.SetValue("NOAA", "NOAATempNormNov", NOAATempNorms[11]);
			ini.SetValue("NOAA", "NOAATempNormDec", NOAATempNorms[12]);

			ini.SetValue("NOAA", "NOAARainNormJan", NOAARainNorms[1]);
			ini.SetValue("NOAA", "NOAARainNormFeb", NOAARainNorms[2]);
			ini.SetValue("NOAA", "NOAARainNormMar", NOAARainNorms[3]);
			ini.SetValue("NOAA", "NOAARainNormApr", NOAARainNorms[4]);
			ini.SetValue("NOAA", "NOAARainNormMay", NOAARainNorms[5]);
			ini.SetValue("NOAA", "NOAARainNormJun", NOAARainNorms[6]);
			ini.SetValue("NOAA", "NOAARainNormJul", NOAARainNorms[7]);
			ini.SetValue("NOAA", "NOAARainNormAug", NOAARainNorms[8]);
			ini.SetValue("NOAA", "NOAARainNormSep", NOAARainNorms[9]);
			ini.SetValue("NOAA", "NOAARainNormOct", NOAARainNorms[10]);
			ini.SetValue("NOAA", "NOAARainNormNov", NOAARainNorms[11]);
			ini.SetValue("NOAA", "NOAARainNormDec", NOAARainNorms[12]);

			ini.SetValue("Proxies", "HTTPProxyName", HTTPProxyName);
			ini.SetValue("Proxies", "HTTPProxyPort", HTTPProxyPort);
			ini.SetValue("Proxies", "HTTPProxyUser", HTTPProxyUser);
			ini.SetValue("Proxies", "HTTPProxyPassword", HTTPProxyPassword);

			ini.SetValue("Display", "NumWindRosePoints", NumWindRosePoints);
			ini.SetValue("Display", "UseApparent", DisplayOptions.UseApparent);
			ini.SetValue("Display", "DisplaySolarData", DisplayOptions.ShowSolar);
			ini.SetValue("Display", "DisplayUvData", DisplayOptions.ShowUV);

			ini.SetValue("Graphs", "ChartMaxDays", GraphDays);
			ini.SetValue("Graphs", "GraphHours", GraphHours);
			ini.SetValue("Graphs", "MoonImageEnabled", MoonImageEnabled);
			ini.SetValue("Graphs", "MoonImageSize", MoonImageSize);
			ini.SetValue("Graphs", "MoonImageFtpDest", MoonImageFtpDest);
			ini.SetValue("Graphs", "TempVisible", GraphOptions.TempVisible);
			ini.SetValue("Graphs", "InTempVisible", GraphOptions.InTempVisible);
			ini.SetValue("Graphs", "HIVisible", GraphOptions.HIVisible);
			ini.SetValue("Graphs", "DPVisible", GraphOptions.DPVisible);
			ini.SetValue("Graphs", "WCVisible", GraphOptions.WCVisible);
			ini.SetValue("Graphs", "AppTempVisible", GraphOptions.AppTempVisible);
			ini.SetValue("Graphs", "FeelsLikeVisible", GraphOptions.FeelsLikeVisible);
			ini.SetValue("Graphs", "HumidexVisible", GraphOptions.HumidexVisible);
			ini.SetValue("Graphs", "InHumVisible", GraphOptions.InHumVisible);
			ini.SetValue("Graphs", "OutHumVisible", GraphOptions.OutHumVisible);
			ini.SetValue("Graphs", "UVVisible", GraphOptions.UVVisible);
			ini.SetValue("Graphs", "SolarVisible", GraphOptions.SolarVisible);
			ini.SetValue("Graphs", "SunshineVisible", GraphOptions.SunshineVisible);
			ini.SetValue("Graphs", "DailyAvgTempVisible", GraphOptions.DailyAvgTempVisible);
			ini.SetValue("Graphs", "DailyMaxTempVisible", GraphOptions.DailyMaxTempVisible);
			ini.SetValue("Graphs", "DailyMinTempVisible", GraphOptions.DailyMinTempVisible);
			ini.SetValue("Graphs", "GrowingDegreeDaysVisible1", GraphOptions.GrowingDegreeDaysVisible1);
			ini.SetValue("Graphs", "GrowingDegreeDaysVisible2", GraphOptions.GrowingDegreeDaysVisible2);
			ini.SetValue("Graphs", "TempSumVisible0", GraphOptions.TempSumVisible0);
			ini.SetValue("Graphs", "TempSumVisible1", GraphOptions.TempSumVisible1);
			ini.SetValue("Graphs", "TempSumVisible2", GraphOptions.TempSumVisible2);


			ini.SetValue("MySQL", "Host", MySqlConnSettings.Server);
			ini.SetValue("MySQL", "Port", MySqlConnSettings.Port);
			ini.SetValue("MySQL", "User", MySqlConnSettings.UserID);
			ini.SetValue("MySQL", "Pass", MySqlConnSettings.Password);
			ini.SetValue("MySQL", "Database", MySqlConnSettings.Database);
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

			for (int i = 0; i < SelectaChartOptions.series.Length; i++)
			{
				ini.SetValue("Select-a-Chart", "Series" + i, SelectaChartOptions.series[i]);
				ini.SetValue("Select-a-Chart", "Colour" + i, SelectaChartOptions.colours[i]);
			}

			// Email settings
			ini.SetValue("SMTP", "Enabled", SmtpOptions.Enabled);
			ini.SetValue("SMTP", "ServerName", SmtpOptions.Server);
			ini.SetValue("SMTP", "Port", SmtpOptions.Port);
			ini.SetValue("SMTP", "SSLOption", SmtpOptions.SslOption);
			ini.SetValue("SMTP", "RequiresAuthentication", SmtpOptions.RequiresAuthentication);
			ini.SetValue("SMTP", "User", SmtpOptions.User);
			ini.SetValue("SMTP", "Password", SmtpOptions.Password);
			ini.SetValue("SMTP", "Logging", SmtpOptions.Logging);

			// Growing Degree Days
			ini.SetValue("GrowingDD", "BaseTemperature1", GrowingBase1);
			ini.SetValue("GrowingDD", "BaseTemperature2", GrowingBase2);
			ini.SetValue("GrowingDD", "YearStarts", GrowingYearStarts);
			ini.SetValue("GrowingDD", "Cap30C", GrowingCap30C);

			// Temperature Sum
			ini.SetValue("TempSum", "TempSumYearStart", TempSumYearStarts);
			ini.SetValue("TempSum", "BaseTemperature1", TempSumBase1);
			ini.SetValue("TempSum", "BaseTemperature2", TempSumBase2);

			ini.Flush();

			LogMessage("Completed writing Cumulus.ini file");
		}

		private void ReadStringsFile()
		{
			IniFile ini = new IniFile("strings.ini");

			// forecast

			ForecastNotAvailable = ini.GetValue("Forecast", "notavailable", ForecastNotAvailable);

			exceptional = ini.GetValue("Forecast", "exceptional", exceptional);
			zForecast[0] = ini.GetValue("Forecast", "forecast1", zForecast[0]);
			zForecast[1] = ini.GetValue("Forecast", "forecast2", zForecast[1]);
			zForecast[2] = ini.GetValue("Forecast", "forecast3", zForecast[2]);
			zForecast[3] = ini.GetValue("Forecast", "forecast4", zForecast[3]);
			zForecast[4] = ini.GetValue("Forecast", "forecast5", zForecast[4]);
			zForecast[5] = ini.GetValue("Forecast", "forecast6", zForecast[5]);
			zForecast[6] = ini.GetValue("Forecast", "forecast7", zForecast[6]);
			zForecast[7] = ini.GetValue("Forecast", "forecast8", zForecast[7]);
			zForecast[8] = ini.GetValue("Forecast", "forecast9", zForecast[8]);
			zForecast[9] = ini.GetValue("Forecast", "forecast10", zForecast[9]);
			zForecast[10] = ini.GetValue("Forecast", "forecast11", zForecast[10]);
			zForecast[11] = ini.GetValue("Forecast", "forecast12", zForecast[11]);
			zForecast[12] = ini.GetValue("Forecast", "forecast13", zForecast[12]);
			zForecast[13] = ini.GetValue("Forecast", "forecast14", zForecast[13]);
			zForecast[14] = ini.GetValue("Forecast", "forecast15", zForecast[14]);
			zForecast[15] = ini.GetValue("Forecast", "forecast16", zForecast[15]);
			zForecast[16] = ini.GetValue("Forecast", "forecast17", zForecast[16]);
			zForecast[17] = ini.GetValue("Forecast", "forecast18", zForecast[17]);
			zForecast[18] = ini.GetValue("Forecast", "forecast19", zForecast[18]);
			zForecast[19] = ini.GetValue("Forecast", "forecast20", zForecast[19]);
			zForecast[20] = ini.GetValue("Forecast", "forecast21", zForecast[20]);
			zForecast[21] = ini.GetValue("Forecast", "forecast22", zForecast[21]);
			zForecast[22] = ini.GetValue("Forecast", "forecast23", zForecast[22]);
			zForecast[23] = ini.GetValue("Forecast", "forecast24", zForecast[23]);
			zForecast[24] = ini.GetValue("Forecast", "forecast25", zForecast[24]);
			zForecast[25] = ini.GetValue("Forecast", "forecast26", zForecast[25]);
			// moon phases
			Newmoon = ini.GetValue("MoonPhases", "Newmoon", Newmoon);
			WaxingCrescent = ini.GetValue("MoonPhases", "WaxingCrescent", WaxingCrescent);
			FirstQuarter = ini.GetValue("MoonPhases", "FirstQuarter", FirstQuarter);
			WaxingGibbous = ini.GetValue("MoonPhases", "WaxingGibbous", WaxingGibbous);
			Fullmoon = ini.GetValue("MoonPhases", "Fullmoon", Fullmoon);
			WaningGibbous = ini.GetValue("MoonPhases", "WaningGibbous", WaningGibbous);
			LastQuarter = ini.GetValue("MoonPhases", "LastQuarter", LastQuarter);
			WaningCrescent = ini.GetValue("MoonPhases", "WaningCrescent", WaningCrescent);
			// beaufort
			Calm = ini.GetValue("Beaufort", "Calm", Calm);
			Lightair = ini.GetValue("Beaufort", "Lightair", Lightair);
			Lightbreeze = ini.GetValue("Beaufort", "Lightbreeze", Lightbreeze);
			Gentlebreeze = ini.GetValue("Beaufort", "Gentlebreeze", Gentlebreeze);
			Moderatebreeze = ini.GetValue("Beaufort", "Moderatebreeze", Moderatebreeze);
			Freshbreeze = ini.GetValue("Beaufort", "Freshbreeze", Freshbreeze);
			Strongbreeze = ini.GetValue("Beaufort", "Strongbreeze", Strongbreeze);
			Neargale = ini.GetValue("Beaufort", "Neargale", Neargale);
			Gale = ini.GetValue("Beaufort", "Gale", Gale);
			Stronggale = ini.GetValue("Beaufort", "Stronggale", Stronggale);
			Storm = ini.GetValue("Beaufort", "Storm", Storm);
			Violentstorm = ini.GetValue("Beaufort", "Violentstorm", Violentstorm);
			Hurricane = ini.GetValue("Beaufort", "Hurricane", Hurricane);
			// trends
			Risingveryrapidly = ini.GetValue("Trends", "Risingveryrapidly", Risingveryrapidly);
			Risingquickly = ini.GetValue("Trends", "Risingquickly", Risingquickly);
			Rising = ini.GetValue("Trends", "Rising", Rising);
			Risingslowly = ini.GetValue("Trends", "Risingslowly", Risingslowly);
			Steady = ini.GetValue("Trends", "Steady", Steady);
			Fallingslowly = ini.GetValue("Trends", "Fallingslowly", Fallingslowly);
			Falling = ini.GetValue("Trends", "Falling", Falling);
			Fallingquickly = ini.GetValue("Trends", "Fallingquickly", Fallingquickly);
			Fallingveryrapidly = ini.GetValue("Trends", "Fallingveryrapidly", Fallingveryrapidly);
			// compass points
			compassp[0] = ini.GetValue("Compass", "N", compassp[0]);
			compassp[1] = ini.GetValue("Compass", "NNE", compassp[1]);
			compassp[2] = ini.GetValue("Compass", "NE", compassp[2]);
			compassp[3] = ini.GetValue("Compass", "ENE", compassp[3]);
			compassp[4] = ini.GetValue("Compass", "E", compassp[4]);
			compassp[5] = ini.GetValue("Compass", "ESE", compassp[5]);
			compassp[6] = ini.GetValue("Compass", "SE", compassp[6]);
			compassp[7] = ini.GetValue("Compass", "SSE", compassp[7]);
			compassp[8] = ini.GetValue("Compass", "S", compassp[8]);
			compassp[9] = ini.GetValue("Compass", "SSW", compassp[9]);
			compassp[10] = ini.GetValue("Compass", "SW", compassp[10]);
			compassp[11] = ini.GetValue("Compass", "WSW", compassp[11]);
			compassp[12] = ini.GetValue("Compass", "W", compassp[12]);
			compassp[13] = ini.GetValue("Compass", "WNW", compassp[13]);
			compassp[14] = ini.GetValue("Compass", "NW", compassp[14]);
			compassp[15] = ini.GetValue("Compass", "NNW", compassp[15]);
			// graphs
			/*
			SmallGraphWindSpeedTitle = ini.GetValue("Graphs", "SmallGraphWindSpeedTitle", "Wind Speed");
			SmallGraphOutsideTemperatureTitle = ini.GetValue("Graphs", "SmallGraphOutsideTemperatureTitle", "Outside Temperature");
			SmallGraphInsideTemperatureTitle = ini.GetValue("Graphs", "SmallGraphInsideTemperatureTitle", "Inside Temperature");
			SmallGraphPressureTitle = ini.GetValue("Graphs", "SmallGraphPressureTitle", "Pressure");
			SmallGraphRainfallRateTitle = ini.GetValue("Graphs", "SmallGraphRainfallRateTitle", "Rainfall Rate");
			SmallGraphWindDirectionTitle = ini.GetValue("Graphs", "SmallGraphWindDirectionTitle", "Wind Direction");
			SmallGraphTempMinMaxAvgTitle = ini.GetValue("Graphs", "SmallGraphTempMinMaxAvgTitle", "Temp Min/Max/Avg");
			SmallGraphHumidityTitle = ini.GetValue("Graphs", "SmallGraphHumidityTitle", "Humidity");
			SmallGraphRainTodayTitle = ini.GetValue("Graphs", "SmallGraphRainTodayTitle", "Rain Today");
			SmallGraphDailyRainTitle = ini.GetValue("Graphs", "SmallGraphDailyRainTitle", "Daily Rain");
			SmallGraphSolarTitle = ini.GetValue("Graphs", "SmallGraphSolarTitle", "Solar Radiation");
			SmallGraphUVTitle = ini.GetValue("Graphs", "SmallGraphUVTitle", "UV Index");
			SmallGraphSunshineTitle = ini.GetValue("Graphs", "SmallGraphSunshineTitle", "Daily Sunshine (hrs)");

			LargeGraphWindSpeedTitle = ini.GetValue("Graphs", "LargeGraphWindSpeedTitle", "Wind Speed");
			LargeGraphWindGustTitle = ini.GetValue("Graphs", "LargeGraphWindGustTitle", "Wind Gust");
			LargeGraphOutsideTempTitle = ini.GetValue("Graphs", "LargeGraphOutsideTempTitle", "Temperature");
			LargeGraphHeatIndexTitle = ini.GetValue("Graphs", "LargeGraphHeatIndexTitle", "Heat Index");
			LargeGraphDewPointTitle = ini.GetValue("Graphs", "LargeGraphDewPointTitle", "Dew Point");
			LargeGraphWindChillTitle = ini.GetValue("Graphs", "LargeGraphWindChillTitle", "Wind Chill");
			LargeGraphApparentTempTitle = ini.GetValue("Graphs", "LargeGraphApparentTempTitle", "Apparent Temperature");
			LargeGraphInsideTempTitle = ini.GetValue("Graphs", "LargeGraphInsideTempTitle", "Inside Temperature");
			LargeGraphPressureTitle = ini.GetValue("Graphs", "LargeGraphPressureTitle", "Pressure");
			LargeGraphRainfallRateTitle = ini.GetValue("Graphs", "LargeGraphRainfallRateTitle", "Rainfall Rate");
			LargeGraphWindDirectionTitle = ini.GetValue("Graphs", "LargeGraphWindDirectionTitle", "Wind Direction");
			LargeGraphWindAvgDirectionTitle = ini.GetValue("Graphs", "LargeGraphWindAvgDirectionTitle", "Average");
			LargeGraphMinTempTitle = ini.GetValue("Graphs", "LargeGraphMinTempTitle", "Min Temp");
			LargeGraphMaxTempTitle = ini.GetValue("Graphs", "LargeGraphMaxTempTitle", "Max Temp");
			LargeGraphAvgTempTitle = ini.GetValue("Graphs", "LargeGraphAvgTempTitle", "Avg Temp");
			LargeGraphInsideHumidityTitle = ini.GetValue("Graphs", "LargeGraphInsideHumidityTitle", "Inside Humidity");
			LargeGraphOutsideHumidityTitle = ini.GetValue("Graphs", "LargeGraphOutsideHumidityTitle", "Outside Humidity");
			LargeGraphRainfallTodayTitle = ini.GetValue("Graphs", "LargeGraphRainfallTodayTitle", "Rainfall Today");
			LargeGraphDailyRainfallTitle = ini.GetValue("Graphs", "LargeGraphDailyRainfallTitle", "Daily Rainfall");
			LargeGraphSolarTitle = ini.GetValue("Graphs", "LargeGraphSolarTitle", "Solar Radiation");
			LargeGraphMaxSolarTitle = ini.GetValue("Graphs", "LargeGraphMaxSolarTitle", "Theoretical Max");
			LargeGraphUVTitle = ini.GetValue("Graphs", "LargeGraphUVTitle", "UV Index");
			LargeGraphSunshineTitle = ini.GetValue("Graphs", "LargeGraphSunshineTitle", "Daily Sunshine (hrs)");
			*/
			// Extra sensor captions
			WMR200ExtraChannelCaptions[1] = ini.GetValue("ExtraSensorCaptions", "Solar", WMR200ExtraChannelCaptions[1]);
			WMR200ExtraChannelCaptions[2] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel2", WMR200ExtraChannelCaptions[2]);
			WMR200ExtraChannelCaptions[3] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel3", WMR200ExtraChannelCaptions[3]);
			WMR200ExtraChannelCaptions[4] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel4", WMR200ExtraChannelCaptions[4]);
			WMR200ExtraChannelCaptions[5] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel5", WMR200ExtraChannelCaptions[5]);
			WMR200ExtraChannelCaptions[6] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel6", WMR200ExtraChannelCaptions[6]);
			WMR200ExtraChannelCaptions[7] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel7", WMR200ExtraChannelCaptions[7]);
			WMR200ExtraChannelCaptions[8] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel8", WMR200ExtraChannelCaptions[8]);
			WMR200ExtraChannelCaptions[9] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel9", WMR200ExtraChannelCaptions[9]);
			WMR200ExtraChannelCaptions[10] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel10", WMR200ExtraChannelCaptions[10]);

			// Extra temperature captions (for Extra Sensor Data screen)
			ExtraTempCaptions[1] = ini.GetValue("ExtraTempCaptions", "Sensor1", ExtraTempCaptions[1]);
			ExtraTempCaptions[2] = ini.GetValue("ExtraTempCaptions", "Sensor2", ExtraTempCaptions[2]);
			ExtraTempCaptions[3] = ini.GetValue("ExtraTempCaptions", "Sensor3", ExtraTempCaptions[3]);
			ExtraTempCaptions[4] = ini.GetValue("ExtraTempCaptions", "Sensor4", ExtraTempCaptions[4]);
			ExtraTempCaptions[5] = ini.GetValue("ExtraTempCaptions", "Sensor5", ExtraTempCaptions[5]);
			ExtraTempCaptions[6] = ini.GetValue("ExtraTempCaptions", "Sensor6", ExtraTempCaptions[6]);
			ExtraTempCaptions[7] = ini.GetValue("ExtraTempCaptions", "Sensor7", ExtraTempCaptions[7]);
			ExtraTempCaptions[8] = ini.GetValue("ExtraTempCaptions", "Sensor8", ExtraTempCaptions[8]);
			ExtraTempCaptions[9] = ini.GetValue("ExtraTempCaptions", "Sensor9", ExtraTempCaptions[9]);
			ExtraTempCaptions[10] = ini.GetValue("ExtraTempCaptions", "Sensor10", ExtraTempCaptions[10]);

			// Extra humidity captions (for Extra Sensor Data screen)
			ExtraHumCaptions[1] = ini.GetValue("ExtraHumCaptions", "Sensor1", ExtraHumCaptions[1]);
			ExtraHumCaptions[2] = ini.GetValue("ExtraHumCaptions", "Sensor2", ExtraHumCaptions[2]);
			ExtraHumCaptions[3] = ini.GetValue("ExtraHumCaptions", "Sensor3", ExtraHumCaptions[3]);
			ExtraHumCaptions[4] = ini.GetValue("ExtraHumCaptions", "Sensor4", ExtraHumCaptions[4]);
			ExtraHumCaptions[5] = ini.GetValue("ExtraHumCaptions", "Sensor5", ExtraHumCaptions[5]);
			ExtraHumCaptions[6] = ini.GetValue("ExtraHumCaptions", "Sensor6", ExtraHumCaptions[6]);
			ExtraHumCaptions[7] = ini.GetValue("ExtraHumCaptions", "Sensor7", ExtraHumCaptions[7]);
			ExtraHumCaptions[8] = ini.GetValue("ExtraHumCaptions", "Sensor8", ExtraHumCaptions[8]);
			ExtraHumCaptions[9] = ini.GetValue("ExtraHumCaptions", "Sensor9", ExtraHumCaptions[9]);
			ExtraHumCaptions[10] = ini.GetValue("ExtraHumCaptions", "Sensor10", ExtraHumCaptions[10]);

			// Extra dew point captions (for Extra Sensor Data screen)
			ExtraDPCaptions[1] = ini.GetValue("ExtraDPCaptions", "Sensor1", ExtraDPCaptions[1]);
			ExtraDPCaptions[2] = ini.GetValue("ExtraDPCaptions", "Sensor2", ExtraDPCaptions[2]);
			ExtraDPCaptions[3] = ini.GetValue("ExtraDPCaptions", "Sensor3", ExtraDPCaptions[3]);
			ExtraDPCaptions[4] = ini.GetValue("ExtraDPCaptions", "Sensor4", ExtraDPCaptions[4]);
			ExtraDPCaptions[5] = ini.GetValue("ExtraDPCaptions", "Sensor5", ExtraDPCaptions[5]);
			ExtraDPCaptions[6] = ini.GetValue("ExtraDPCaptions", "Sensor6", ExtraDPCaptions[6]);
			ExtraDPCaptions[7] = ini.GetValue("ExtraDPCaptions", "Sensor7", ExtraDPCaptions[7]);
			ExtraDPCaptions[8] = ini.GetValue("ExtraDPCaptions", "Sensor8", ExtraDPCaptions[8]);
			ExtraDPCaptions[9] = ini.GetValue("ExtraDPCaptions", "Sensor9", ExtraDPCaptions[9]);
			ExtraDPCaptions[10] = ini.GetValue("ExtraDPCaptions", "Sensor10", ExtraDPCaptions[10]);

			// soil temp captions (for Extra Sensor Data screen)
			SoilTempCaptions[1] = ini.GetValue("SoilTempCaptions", "Sensor1", SoilTempCaptions[1]);
			SoilTempCaptions[2] = ini.GetValue("SoilTempCaptions", "Sensor2", SoilTempCaptions[2]);
			SoilTempCaptions[3] = ini.GetValue("SoilTempCaptions", "Sensor3", SoilTempCaptions[3]);
			SoilTempCaptions[4] = ini.GetValue("SoilTempCaptions", "Sensor4", SoilTempCaptions[4]);
			SoilTempCaptions[5] = ini.GetValue("SoilTempCaptions", "Sensor5", SoilTempCaptions[5]);
			SoilTempCaptions[6] = ini.GetValue("SoilTempCaptions", "Sensor6", SoilTempCaptions[6]);
			SoilTempCaptions[7] = ini.GetValue("SoilTempCaptions", "Sensor7", SoilTempCaptions[7]);
			SoilTempCaptions[8] = ini.GetValue("SoilTempCaptions", "Sensor8", SoilTempCaptions[8]);
			SoilTempCaptions[9] = ini.GetValue("SoilTempCaptions", "Sensor9", SoilTempCaptions[9]);
			SoilTempCaptions[10] = ini.GetValue("SoilTempCaptions", "Sensor10", SoilTempCaptions[10]);
			SoilTempCaptions[11] = ini.GetValue("SoilTempCaptions", "Sensor11", SoilTempCaptions[11]);
			SoilTempCaptions[12] = ini.GetValue("SoilTempCaptions", "Sensor12", SoilTempCaptions[12]);
			SoilTempCaptions[13] = ini.GetValue("SoilTempCaptions", "Sensor13", SoilTempCaptions[13]);
			SoilTempCaptions[14] = ini.GetValue("SoilTempCaptions", "Sensor14", SoilTempCaptions[14]);
			SoilTempCaptions[15] = ini.GetValue("SoilTempCaptions", "Sensor15", SoilTempCaptions[15]);
			SoilTempCaptions[16] = ini.GetValue("SoilTempCaptions", "Sensor16", SoilTempCaptions[16]);

			// soil moisture captions (for Extra Sensor Data screen)
			SoilMoistureCaptions[1] = ini.GetValue("SoilMoistureCaptions", "Sensor1", SoilMoistureCaptions[1]);
			SoilMoistureCaptions[2] = ini.GetValue("SoilMoistureCaptions", "Sensor2", SoilMoistureCaptions[2]);
			SoilMoistureCaptions[3] = ini.GetValue("SoilMoistureCaptions", "Sensor3", SoilMoistureCaptions[3]);
			SoilMoistureCaptions[4] = ini.GetValue("SoilMoistureCaptions", "Sensor4", SoilMoistureCaptions[4]);
			SoilMoistureCaptions[5] = ini.GetValue("SoilMoistureCaptions", "Sensor5", SoilMoistureCaptions[5]);
			SoilMoistureCaptions[6] = ini.GetValue("SoilMoistureCaptions", "Sensor6", SoilMoistureCaptions[6]);
			SoilMoistureCaptions[7] = ini.GetValue("SoilMoistureCaptions", "Sensor7", SoilMoistureCaptions[7]);
			SoilMoistureCaptions[8] = ini.GetValue("SoilMoistureCaptions", "Sensor8", SoilMoistureCaptions[8]);
			SoilMoistureCaptions[9] = ini.GetValue("SoilMoistureCaptions", "Sensor9", SoilMoistureCaptions[9]);
			SoilMoistureCaptions[10] = ini.GetValue("SoilMoistureCaptions", "Sensor10", SoilMoistureCaptions[10]);
			SoilMoistureCaptions[11] = ini.GetValue("SoilMoistureCaptions", "Sensor11", SoilMoistureCaptions[11]);
			SoilMoistureCaptions[12] = ini.GetValue("SoilMoistureCaptions", "Sensor12", SoilMoistureCaptions[12]);
			SoilMoistureCaptions[13] = ini.GetValue("SoilMoistureCaptions", "Sensor13", SoilMoistureCaptions[13]);
			SoilMoistureCaptions[14] = ini.GetValue("SoilMoistureCaptions", "Sensor14", SoilMoistureCaptions[14]);
			SoilMoistureCaptions[15] = ini.GetValue("SoilMoistureCaptions", "Sensor15", SoilMoistureCaptions[15]);
			SoilMoistureCaptions[16] = ini.GetValue("SoilMoistureCaptions", "Sensor16", SoilMoistureCaptions[16]);

			// leaf temp captions (for Extra Sensor Data screen)
			LeafTempCaptions[1] = ini.GetValue("LeafTempCaptions", "Sensor1", LeafTempCaptions[1]);
			LeafTempCaptions[2] = ini.GetValue("LeafTempCaptions", "Sensor2", LeafTempCaptions[2]);
			LeafTempCaptions[3] = ini.GetValue("LeafTempCaptions", "Sensor3", LeafTempCaptions[3]);
			LeafTempCaptions[4] = ini.GetValue("LeafTempCaptions", "Sensor4", LeafTempCaptions[4]);

			// leaf wetness captions (for Extra Sensor Data screen)
			LeafWetnessCaptions[1] = ini.GetValue("LeafWetnessCaptions", "Sensor1", LeafWetnessCaptions[1]);
			LeafWetnessCaptions[2] = ini.GetValue("LeafWetnessCaptions", "Sensor2", LeafWetnessCaptions[2]);
			LeafWetnessCaptions[3] = ini.GetValue("LeafWetnessCaptions", "Sensor3", LeafWetnessCaptions[3]);
			LeafWetnessCaptions[4] = ini.GetValue("LeafWetnessCaptions", "Sensor4", LeafWetnessCaptions[4]);
			LeafWetnessCaptions[5] = ini.GetValue("LeafWetnessCaptions", "Sensor5", LeafWetnessCaptions[5]);
			LeafWetnessCaptions[6] = ini.GetValue("LeafWetnessCaptions", "Sensor6", LeafWetnessCaptions[6]);
			LeafWetnessCaptions[7] = ini.GetValue("LeafWetnessCaptions", "Sensor7", LeafWetnessCaptions[7]);
			LeafWetnessCaptions[8] = ini.GetValue("LeafWetnessCaptions", "Sensor8", LeafWetnessCaptions[8]);

			// air quality captions (for Extra Sensor Data screen)
			AirQualityCaptions[1] = ini.GetValue("AirQualityCaptions", "Sensor1", AirQualityCaptions[1]);
			AirQualityCaptions[2] = ini.GetValue("AirQualityCaptions", "Sensor2", AirQualityCaptions[2]);
			AirQualityCaptions[3] = ini.GetValue("AirQualityCaptions", "Sensor3", AirQualityCaptions[3]);
			AirQualityCaptions[4] = ini.GetValue("AirQualityCaptions", "Sensor4", AirQualityCaptions[4]);
			AirQualityAvgCaptions[1] = ini.GetValue("AirQualityCaptions", "SensorAvg1", AirQualityAvgCaptions[1]);
			AirQualityAvgCaptions[2] = ini.GetValue("AirQualityCaptions", "SensorAvg2", AirQualityAvgCaptions[2]);
			AirQualityAvgCaptions[3] = ini.GetValue("AirQualityCaptions", "SensorAvg3", AirQualityAvgCaptions[3]);
			AirQualityAvgCaptions[4] = ini.GetValue("AirQualityCaptions", "SensorAvg4", AirQualityAvgCaptions[4]);

			// CO2 captions - Ecowitt WH45 sensor
			CO2_CurrentCaption = ini.GetValue("CO2Captions", "CO2-Current", CO2_CurrentCaption);
			CO2_24HourCaption = ini.GetValue("CO2Captions", "CO2-24hr", CO2_24HourCaption);
			CO2_pm2p5Caption = ini.GetValue("CO2Captions", "CO2-Pm2p5", CO2_pm2p5Caption);
			CO2_pm2p5_24hrCaption = ini.GetValue("CO2Captions", "CO2-Pm2p5-24hr", CO2_pm2p5_24hrCaption);
			CO2_pm10Caption = ini.GetValue("CO2Captions", "CO2-Pm10", CO2_pm10Caption);
			CO2_pm10_24hrCaption = ini.GetValue("CO2Captions", "CO2-Pm10-24hr", CO2_pm10_24hrCaption);

			// User temperature captions (for Extra Sensor Data screen)
			UserTempCaptions[1] = ini.GetValue("UserTempCaptions", "Sensor1", UserTempCaptions[1]);
			UserTempCaptions[2] = ini.GetValue("UserTempCaptions", "Sensor2", UserTempCaptions[2]);
			UserTempCaptions[3] = ini.GetValue("UserTempCaptions", "Sensor3", UserTempCaptions[3]);
			UserTempCaptions[4] = ini.GetValue("UserTempCaptions", "Sensor4", UserTempCaptions[4]);
			UserTempCaptions[5] = ini.GetValue("UserTempCaptions", "Sensor5", UserTempCaptions[5]);
			UserTempCaptions[6] = ini.GetValue("UserTempCaptions", "Sensor6", UserTempCaptions[6]);
			UserTempCaptions[7] = ini.GetValue("UserTempCaptions", "Sensor7", UserTempCaptions[7]);
			UserTempCaptions[8] = ini.GetValue("UserTempCaptions", "Sensor8", UserTempCaptions[8]);

			thereWillBeMinSLessDaylightTomorrow = ini.GetValue("Solar", "LessDaylightTomorrow", thereWillBeMinSLessDaylightTomorrow);
			thereWillBeMinSMoreDaylightTomorrow = ini.GetValue("Solar", "MoreDaylightTomorrow", thereWillBeMinSMoreDaylightTomorrow);

			DavisForecast1[0] = ini.GetValue("DavisForecast1", "forecast1", DavisForecast1[0]);
			DavisForecast1[1] = ini.GetValue("DavisForecast1", "forecast2", DavisForecast1[1]) + " ";
			DavisForecast1[2] = ini.GetValue("DavisForecast1", "forecast3", DavisForecast1[2]) + " ";
			DavisForecast1[3] = ini.GetValue("DavisForecast1", "forecast4", DavisForecast1[3]) + " ";
			DavisForecast1[4] = ini.GetValue("DavisForecast1", "forecast5", DavisForecast1[4]) + " ";
			DavisForecast1[5] = ini.GetValue("DavisForecast1", "forecast6", DavisForecast1[5]) + " ";
			DavisForecast1[6] = ini.GetValue("DavisForecast1", "forecast7", DavisForecast1[6]) + " ";
			DavisForecast1[7] = ini.GetValue("DavisForecast1", "forecast8", DavisForecast1[7]) + " ";
			DavisForecast1[8] = ini.GetValue("DavisForecast1", "forecast9", DavisForecast1[8]) + " ";
			DavisForecast1[9] = ini.GetValue("DavisForecast1", "forecast10", DavisForecast1[9]) + " ";
			DavisForecast1[10] = ini.GetValue("DavisForecast1", "forecast11", DavisForecast1[10]) + " ";
			DavisForecast1[11] = ini.GetValue("DavisForecast1", "forecast12", DavisForecast1[11]) + " ";
			DavisForecast1[12] = ini.GetValue("DavisForecast1", "forecast13", DavisForecast1[12]) + " ";
			DavisForecast1[13] = ini.GetValue("DavisForecast1", "forecast14", DavisForecast1[13]) + " ";
			DavisForecast1[14] = ini.GetValue("DavisForecast1", "forecast15", DavisForecast1[14]) + " ";
			DavisForecast1[15] = ini.GetValue("DavisForecast1", "forecast16", DavisForecast1[15]) + " ";
			DavisForecast1[16] = ini.GetValue("DavisForecast1", "forecast17", DavisForecast1[16]) + " ";
			DavisForecast1[17] = ini.GetValue("DavisForecast1", "forecast18", DavisForecast1[17]) + " ";
			DavisForecast1[18] = ini.GetValue("DavisForecast1", "forecast19", DavisForecast1[18]) + " ";
			DavisForecast1[19] = ini.GetValue("DavisForecast1", "forecast20", DavisForecast1[19]) + " ";
			DavisForecast1[20] = ini.GetValue("DavisForecast1", "forecast21", DavisForecast1[20]) + " ";
			DavisForecast1[21] = ini.GetValue("DavisForecast1", "forecast22", DavisForecast1[21]) + " ";
			DavisForecast1[22] = ini.GetValue("DavisForecast1", "forecast23", DavisForecast1[22]) + " ";
			DavisForecast1[23] = ini.GetValue("DavisForecast1", "forecast24", DavisForecast1[23]) + " ";
			DavisForecast1[24] = ini.GetValue("DavisForecast1", "forecast25", DavisForecast1[24]) + " ";
			DavisForecast1[25] = ini.GetValue("DavisForecast1", "forecast26", DavisForecast1[25]) + " ";
			DavisForecast1[26] = ini.GetValue("DavisForecast1", "forecast27", DavisForecast1[26]);

			DavisForecast2[0] = ini.GetValue("DavisForecast2", "forecast1", DavisForecast2[0]);
			DavisForecast2[1] = ini.GetValue("DavisForecast2", "forecast2", DavisForecast2[1]) + " ";
			DavisForecast2[2] = ini.GetValue("DavisForecast2", "forecast3", DavisForecast2[2]) + " ";
			DavisForecast2[3] = ini.GetValue("DavisForecast2", "forecast4", DavisForecast2[3]) + " ";
			DavisForecast2[4] = ini.GetValue("DavisForecast2", "forecast5", DavisForecast2[5]) + " ";
			DavisForecast2[5] = ini.GetValue("DavisForecast2", "forecast6", DavisForecast2[5]) + " ";
			DavisForecast2[6] = ini.GetValue("DavisForecast2", "forecast7", DavisForecast2[6]) + " ";
			DavisForecast2[7] = ini.GetValue("DavisForecast2", "forecast8", DavisForecast2[7]) + " ";
			DavisForecast2[8] = ini.GetValue("DavisForecast2", "forecast9", DavisForecast2[8]) + " ";
			DavisForecast2[9] = ini.GetValue("DavisForecast2", "forecast10", DavisForecast2[9]) + " ";
			DavisForecast2[10] = ini.GetValue("DavisForecast2", "forecast11", DavisForecast2[10]) + " ";
			DavisForecast2[11] = ini.GetValue("DavisForecast2", "forecast12", DavisForecast2[11]) + " ";
			DavisForecast2[12] = ini.GetValue("DavisForecast2", "forecast13", DavisForecast2[12]) + " ";
			DavisForecast2[13] = ini.GetValue("DavisForecast2", "forecast14", DavisForecast2[13]) + " ";
			DavisForecast2[14] = ini.GetValue("DavisForecast2", "forecast15", DavisForecast2[14]) + " ";
			DavisForecast2[15] = ini.GetValue("DavisForecast2", "forecast16", DavisForecast2[15]) + " ";
			DavisForecast2[16] = ini.GetValue("DavisForecast2", "forecast17", DavisForecast2[16]) + " ";
			DavisForecast2[17] = ini.GetValue("DavisForecast2", "forecast18", DavisForecast2[17]) + " ";
			DavisForecast2[18] = ini.GetValue("DavisForecast2", "forecast19", DavisForecast2[18]) + " ";

			DavisForecast3[0] = ini.GetValue("DavisForecast3", "forecast1", DavisForecast3[0]);
			DavisForecast3[1] = ini.GetValue("DavisForecast3", "forecast2", DavisForecast3[1]);
			DavisForecast3[2] = ini.GetValue("DavisForecast3", "forecast3", DavisForecast3[2]);
			DavisForecast3[3] = ini.GetValue("DavisForecast3", "forecast4", DavisForecast3[3]);
			DavisForecast3[4] = ini.GetValue("DavisForecast3", "forecast5", DavisForecast3[4]);
			DavisForecast3[5] = ini.GetValue("DavisForecast3", "forecast6", DavisForecast3[5]);
			DavisForecast3[6] = ini.GetValue("DavisForecast3", "forecast7", DavisForecast3[6]);

			// alarm emails
			AlarmEmailSubject = ini.GetValue("AlarmEmails", "subject", "Cumulus MX Alarm");
			AlarmEmailPreamble = ini.GetValue("AlarmEmails", "preamble", "A Cumulus MX alarm has been triggered.");
			HighGustAlarm.EmailMsg = ini.GetValue("AlarmEmails", "windGustAbove", "A wind gust above {0} {1} has occurred.");
			HighPressAlarm.EmailMsg = ini.GetValue("AlarmEmails", "pressureAbove", "The pressure has risen above {0} {1}.");
			HighTempAlarm.EmailMsg = ini.GetValue("AlarmEmails", "tempAbove", "The temperature has risen above {0} {1}.");
			LowPressAlarm.EmailMsg = ini.GetValue("AlarmEmails", "pressBelow", "The pressure has fallen below {0} {1}.");
			LowTempAlarm.EmailMsg = ini.GetValue("AlarmEmails", "tempBelow", "The temperature has fallen below {0} {1}.");
			PressChangeAlarm.EmailMsgDn = ini.GetValue("AlarmEmails", "pressDown", "The pressure has decreased by more than {0} {1}.");
			PressChangeAlarm.EmailMsgUp = ini.GetValue("AlarmEmails", "pressUp", "The pressure has increased by more than {0} {1}.");
			HighRainTodayAlarm.EmailMsg = ini.GetValue("AlarmEmails", "rainAbove", "The rainfall today has exceeded {0} {1}.");
			HighRainRateAlarm.EmailMsg = ini.GetValue("AlarmEmails", "rainRateAbove", "The rainfall rate has exceeded {0} {1}.");
			SensorAlarm.EmailMsg = ini.GetValue("AlarmEmails", "sensorLost", "Contact has been lost with a remote sensor,");
			TempChangeAlarm.EmailMsgDn = ini.GetValue("AlarmEmails", "tempDown", "The temperature decreased by more than {0} {1}.");
			TempChangeAlarm.EmailMsgUp = ini.GetValue("AlarmEmails", "tempUp", "The temperature has increased by more than {0} {1}.");
			HighWindAlarm.EmailMsg = ini.GetValue("AlarmEmails", "windAbove", "The average wind speed has exceeded {0} {1}.");
			DataStoppedAlarm.EmailMsg = ini.GetValue("AlarmEmails", "dataStopped", "Cumulus has stopped receiving data from your weather station.");
			BatteryLowAlarm.EmailMsg = ini.GetValue("AlarmEmails", "batteryLow", "A low battery condition has been detected.");
			SpikeAlarm.EmailMsg = ini.GetValue("AlarmEmails", "dataSpike", "A data spike from your weather station has been suppressed.");
			UpgradeAlarm.EmailMsg = ini.GetValue("AlarmEmails", "upgrade", "An upgrade to Cumulus MX is now available.");
		}


		public bool UseBlakeLarsen { get; set; }

		public double LuxToWM2 { get; set; }

		public int SolarMinimum { get; set; }

		public int SunThreshold { get; set; }

		public int SolarCalc { get; set; }

		public double BrasTurbidity { get; set; }

		//public double SolarFactorSummer { get; set; }
		//public double SolarFactorWinter { get; set; }

		public int xapPort { get; set; }

		public string xapUID { get; set; }

		public bool xapEnabled { get; set; }

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

		//public int MaxFTPconnectRetries { get; set; }

		public bool DeleteBeforeUpload { get; set; }

		public bool FTPRename { get; set; }

		public int UpdateInterval { get; set; }

		public Timer RealtimeTimer = new Timer();

		internal Timer CustomMysqlSecondsTimer;

		public bool ActiveFTPMode { get; set; }

		public FtpProtocols Sslftp { get; set; }

		public string SshftpAuthentication { get; set; }

		public string SshftpPskFile { get; set; }

		public bool DisableFtpsEPSV { get; set; }

		public bool DisableFtpsExplicit { get; set; }

		public bool FTPlogging { get; set; }

		public bool WebIntervalEnabled { get; set; }

		public bool WebAutoUpdate { get; set; }

		public string FtpDirectory { get; set; }

		public string FtpPassword { get; set; }

		public string FtpUsername { get; set; }

		public int FtpHostPort { get; set; }

		public string FtpHostname { get; set; }

		public int WMR200TempChannel { get; set; }

		public int WMR928TempChannel { get; set; }

		public int RTdisconnectcount { get; set; }

		//public int VP2SleepInterval { get; set; }

		//public int VPClosedownTime { get; set; }
		public string AirLinkInIPAddr { get; set; }
		public string AirLinkOutIPAddr { get; set; }

		public bool AirLinkInEnabled { get; set; }
		public bool AirLinkOutEnabled { get; set; }

		//public bool solar_logging { get; set; }

		//public bool special_logging { get; set; }

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

		public bool RG11Enabled { get; set; }
		public bool RG11Enabled2 { get; set; }

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

		public bool HourlyForecast { get; set; }

		public bool UseCumulusForecast { get; set; }

		public bool UseDataLogger { get; set; }

		public bool DavisConsoleHighGust { get; set; }

		public bool DavisCalcAltPress { get; set; }

		public bool DavisUseDLLBarCalData { get; set; }

		public int LCMaxWind { get; set; }

		//public bool EWduplicatecheck { get; set; }

		public string RecordsBeganDate { get; set; }

		//public bool EWdisablecheckinit { get; set; }

		//public bool EWallowFF { get; set; }

		public int YTDrainyear { get; set; }

		public double YTDrain { get; set; }

		public string LocationDesc { get; set; }

		public string LocationName { get; set; }

		public string HTTPProxyPassword { get; set; }

		public string HTTPProxyUser { get; set; }

		public int HTTPProxyPort { get; set; }

		public string HTTPProxyName { get; set; }

		public int[] WindDPlaceDefaults = { 1, 0, 0, 0 }; // m/s, mph, km/h, knots
		public int[] TempDPlaceDefaults = { 1, 1 };
		public int[] PressDPlaceDefaults = { 1, 1, 2 };
		public int[] RainDPlaceDefaults = { 1, 2 };
		public const int numextrafiles = 99;
		public const int numOfSelectaChartSeries = 6;

		//public bool WS2300Sync { get; set; }

		public bool ErrorLogSpikeRemoval { get; set; }

		//public bool NoFlashWetDryDayRecords { get; set; }

		public bool ReportLostSensorContact { get; set; }

		public bool ReportDataStoppedErrors { get; set; }

		//public bool RestartIfDataStops { get; set; }

		//public bool RestartIfUnplugged { get; set; }

		//public bool CloseOnSuspend { get; set; }

		//public bool ConfirmClose { get; set; }

		public int DataLogInterval { get; set; }

		public int UVdecimals { get; set; }

		public int UVdecimaldefault { get; set; }

		public string LonTxt { get; set; }

		public string LatTxt { get; set; }

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
		public int GraphHours { get; set; }

		// WeatherLink Live transmitter Ids and indexes
		public string WllApiKey;
		public string WllApiSecret;
		public int WllStationId;
		public int WllParentId;

		public int WllBroadcastDuration = 300;
		public int WllBroadcastPort = 22222;
		public bool WLLAutoUpdateIpAddress = true;
		public int WllPrimaryWind = 1;
		public int WllPrimaryTempHum = 1;
		public int WllPrimaryRain = 1;
		public int WllPrimarySolar;
		public int WllPrimaryUV;

		public int WllExtraSoilTempTx1;
		public int WllExtraSoilTempIdx1 = 1;
		public int WllExtraSoilTempTx2;
		public int WllExtraSoilTempIdx2 = 2;
		public int WllExtraSoilTempTx3;
		public int WllExtraSoilTempIdx3 = 3;
		public int WllExtraSoilTempTx4;
		public int WllExtraSoilTempIdx4 = 4;

		public int WllExtraSoilMoistureTx1;
		public int WllExtraSoilMoistureIdx1 = 1;
		public int WllExtraSoilMoistureTx2;
		public int WllExtraSoilMoistureIdx2 = 2;
		public int WllExtraSoilMoistureTx3;
		public int WllExtraSoilMoistureIdx3 = 3;
		public int WllExtraSoilMoistureTx4;
		public int WllExtraSoilMoistureIdx4 = 4;

		public int WllExtraLeafTx1;
		public int WllExtraLeafIdx1 = 1;
		public int WllExtraLeafTx2;
		public int WllExtraLeafIdx2 = 2;

		public int[] WllExtraTempTx = { 0, 0, 0, 0, 0, 0, 0, 0 };

		public bool[] WllExtraHumTx = { false, false, false, false, false, false, false, false };

		// WeatherLink Live transmitter Ids and indexes
		public bool AirLinkIsNode;
		public string AirLinkApiKey;
		public string AirLinkApiSecret;
		public int AirLinkInStationId;
		public int AirLinkOutStationId;
		public bool AirLinkAutoUpdateIpAddress = true;

		public int airQualityIndex = -1;

		public string Gw1000IpAddress;
		public string Gw1000MacAddress;
		public bool Gw1000AutoUpdateIpAddress = true;

		public Timer WundTimer = new Timer();
		public Timer WebTimer = new Timer();
		public Timer AwekasTimer = new Timer();
		public Timer MQTTTimer = new Timer();
		//public Timer AirLinkTimer = new Timer();

		public int DAVIS = 0;
		public int OREGON = 1;
		public int EW = 2;
		public int LACROSSE = 3;
		public int OREGONUSB = 4;
		public int INSTROMET = 5;
		public int ECOWITT = 6;
		//public bool startingup = true;
		public string ReportPath;
		public string LatestError;
		public DateTime LatestErrorTS = DateTime.MinValue;
		//public DateTime defaultRecordTS = new DateTime(2000, 1, 1, 0, 0, 0);
		public DateTime defaultRecordTS = DateTime.MinValue;
		public string WxnowFile = "wxnow.txt";
		private readonly string RealtimeFile = "realtime.txt";
		private readonly string TwitterTxtFile;
		public bool IncludeMoonImage;
		private readonly FtpClient RealtimeFTP = new FtpClient();
		private SftpClient RealtimeSSH;
		private volatile bool RealtimeFtpInProgress;
		private volatile bool RealtimeCopyInProgress;
		private byte RealtimeCycleCounter;

		public FileGenerationFtpOptions[] StdWebFiles;
		public FileGenerationFtpOptions[] RealtimeFiles;
		public FileGenerationFtpOptions[] GraphDataFiles;
		public FileGenerationFtpOptions[] GraphDataEodFiles;


		public string exceptional = "Exceptional Weather";
//		private WebSocketServer wsServer;
		public string[] WMR200ExtraChannelCaptions = new string[11];
		public string[] ExtraTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
		public string[] ExtraHumCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
		public string[] ExtraDPCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
		public string[] SoilTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10", "Sensor 11", "Sensor 12", "Sensor 13", "Sensor 14", "Sensor 15", "Sensor 16" };
		public string[] SoilMoistureCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10", "Sensor 11", "Sensor 12", "Sensor 13", "Sensor 14", "Sensor 15", "Sensor 16" };
		public string[] AirQualityCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4" };
		public string[] AirQualityAvgCaptions = { "", "Sensor Avg 1", "Sensor Avg 2", "Sensor Avg 3", "Sensor Avg 4" };
		public string[] LeafTempCaptions = { "", "Temp 1", "Temp 2", "Temp 3", "Temp 4" };
		public string[] LeafWetnessCaptions = { "", "Wetness 1", "Wetness 2", "Wetness 3", "Wetness 4", "Wetness 5", "Wetness 6", "Wetness 7", "Wetness 8" };
		public string[] UserTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8" };
		private string thereWillBeMinSLessDaylightTomorrow = "There will be {0}min {1}s less daylight tomorrow";
		private string thereWillBeMinSMoreDaylightTomorrow = "There will be {0}min {1}s more daylight tomorrow";
		// WH45 CO2 sensor captions
		public string CO2_CurrentCaption = "CO&#8322 Current";
		public string CO2_24HourCaption = "CO&#8322 24h avg";
		public string CO2_pm2p5Caption = "PM 2.5";
		public string CO2_pm2p5_24hrCaption = "PM 2.5 24h avg";
		public string CO2_pm10Caption = "PM 10";
		public string CO2_pm10_24hrCaption = "PM 10 24h avg";

		/*
		public string Getversion()
		{
			return Version;
		}

		public void SetComport(string comport)
		{
			ComportName = comport;
		}

		public string GetComport()
		{
			return ComportName;
		}

		public void SetStationType(int type)
		{
			StationType = type;
		}

		public int GetStationType()
		{
			return StationType;
		}

		public void SetVPRainGaugeType(int type)
		{
			VPrainGaugeType = type;
		}

		public int GetVPRainGaugeType()
		{
			return VPrainGaugeType;
		}

		public void SetVPConnectionType(VPConnTypes type)
		{
			VPconntype = type;
		}

		public VPConnTypes GetVPConnectionType()
		{
			return VPconntype;
		}

		public void SetIPaddress(string address)
		{
			IPaddress = address;
		}

		public string GetIPaddress()
		{
			return IPaddress;
		}

		public void SetTCPport(int port)
		{
			TCPport = port;
		}

		public int GetTCPport()
		{
			return TCPport;
		}
		*/

		public string GetLogFileName(DateTime thedate)
		{
			// First determine the date for the logfile.
			// If we're using 9am rollover, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate;

			if (RolloverHour == 0)
			{
				logfiledate = thedate;
			}
			else
			{
				TimeZone tz = TimeZone.CurrentTimeZone;

				if (Use10amInSummer && tz.IsDaylightSavingTime(thedate))
				{
					// Locale is currently on Daylight (summer) time
					logfiledate = thedate.AddHours(-10);
				}
				else
				{
					// Locale is currently on Standard time or unknown
					logfiledate = thedate.AddHours(-9);
				}
			}

			var datestring = logfiledate.ToString("MMMyy").Replace(".", "");

			return Datapath + datestring + "log.txt";
		}

		public string GetExtraLogFileName(DateTime thedate)
		{
			// First determine the date for the logfile.
			// If we're using 9am rollover, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate;

			if (RolloverHour == 0)
			{
				logfiledate = thedate;
			}
			else
			{
				TimeZone tz = TimeZone.CurrentTimeZone;

				if (Use10amInSummer && tz.IsDaylightSavingTime(thedate))
				{
					// Locale is currently on Daylight (summer) time
					logfiledate = thedate.AddHours(-10);
				}
				else
				{
					// Locale is currently on Standard time or unknown
					logfiledate = thedate.AddHours(-9);
				}
			}

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Datapath + "ExtraLog" + datestring + ".txt";
		}

		public string GetAirLinkLogFileName(DateTime thedate)
		{
			// First determine the date for the logfile.
			// If we're using 9am rollover, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate;

			if (RolloverHour == 0)
			{
				logfiledate = thedate;
			}
			else
			{
				TimeZone tz = TimeZone.CurrentTimeZone;

				if (Use10amInSummer && tz.IsDaylightSavingTime(thedate))
				{
					// Locale is currently on Daylight (summer) time
					logfiledate = thedate.AddHours(-10);
				}
				else
				{
					// Locale is currently on Standard time or unknown
					logfiledate = thedate.AddHours(-9);
				}
			}

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Datapath + "AirLink" + datestring + "log.txt";
		}

		public const int NumLogFileFields = 29;

		public async void DoLogFile(DateTime timestamp, bool live)
		{
			// Writes an entry to the n-minute logfile. Fields are comma-separated:
			// 0  Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  Current time - hh:mm
			// 2  Current temperature
			// 3  Current humidity
			// 4  Current dewpoint
			// 5  Current wind speed
			// 6  Recent (10-minute) high gust
			// 7  Average wind bearing
			// 8  Current rainfall rate
			// 9  Total rainfall today so far
			// 10  Current sea level pressure
			// 11  Total rainfall counter as held by the station
			// 12  Inside temperature
			// 13  Inside humidity
			// 14  Current gust (i.e. 'Latest')
			// 15  Wind chill
			// 16  Heat Index
			// 17  UV Index
			// 18  Solar Radiation
			// 19  Evapotranspiration
			// 20  Annual Evapotranspiration
			// 21  Apparent temperature
			// 22  Current theoretical max solar radiation
			// 23  Hours of sunshine so far today
			// 24  Current wind bearing
			// 25  RG-11 rain total
			// 26  Rain since midnight
			// 27  Feels like
			// 28  Humidex

			// make sure solar max is calculated for those stations without a solar sensor
			LogMessage("DoLogFile: Writing log entry for " + timestamp);
			LogDebugMessage("DoLogFile: max gust: " + station.RecentMaxGust.ToString(WindFormat));
			station.CurrentSolarMax = AstroLib.SolarMax(timestamp, Longitude, Latitude, station.AltitudeM(Altitude), out station.SolarElevation, RStransfactor, BrasTurbidity, SolarCalc);
			var filename = GetLogFileName(timestamp);

			using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
			using (StreamWriter file = new StreamWriter(fs))
			{
				file.Write(timestamp.ToString("dd/MM/yy") + ListSeparator);
				file.Write(timestamp.ToString("HH:mm") + ListSeparator);
				file.Write(station.OutdoorTemperature.ToString(TempFormat) + ListSeparator);
				file.Write(station.OutdoorHumidity + ListSeparator);
				file.Write(station.OutdoorDewpoint.ToString(TempFormat) + ListSeparator);
				file.Write(station.WindAverage.ToString(WindAvgFormat) + ListSeparator);
				file.Write(station.RecentMaxGust.ToString(WindFormat) + ListSeparator);
				file.Write(station.AvgBearing + ListSeparator);
				file.Write(station.RainRate.ToString(RainFormat) + ListSeparator);
				file.Write(station.RainToday.ToString(RainFormat) + ListSeparator);
				file.Write(station.Pressure.ToString(PressFormat) + ListSeparator);
				file.Write(station.Raincounter.ToString(RainFormat) + ListSeparator);
				file.Write(station.IndoorTemperature.ToString(TempFormat) + ListSeparator);
				file.Write(station.IndoorHumidity + ListSeparator);
				file.Write(station.WindLatest.ToString(WindFormat) + ListSeparator);
				file.Write(station.WindChill.ToString(TempFormat) + ListSeparator);
				file.Write(station.HeatIndex.ToString(TempFormat) + ListSeparator);
				file.Write(station.UV.ToString(UVFormat) + ListSeparator);
				file.Write(station.SolarRad + ListSeparator);
				file.Write(station.ET.ToString(ETFormat) + ListSeparator);
				file.Write(station.AnnualETTotal.ToString(ETFormat) + ListSeparator);
				file.Write(station.ApparentTemperature.ToString(TempFormat) + ListSeparator);
				file.Write((Math.Round(station.CurrentSolarMax)) + ListSeparator);
				file.Write(station.SunshineHours.ToString(SunFormat) + ListSeparator);
				file.Write(station.Bearing + ListSeparator);
				file.Write(station.RG11RainToday.ToString(RainFormat) + ListSeparator);
				file.Write(station.RainSinceMidnight.ToString(RainFormat) + ListSeparator);
				file.Write(station.FeelsLike.ToString(TempFormat) + ListSeparator);
				file.WriteLine(station.Humidex.ToString(TempFormat));
				file.Close();
			}

			LastUpdateTime = timestamp;
			LogMessage("DoLogFile: Written log entry for " + timestamp);
			station.WriteTodayFile(timestamp, true);

			if (MonthlyMySqlEnabled)
			{
				var InvC = new CultureInfo("");

				StringBuilder values = new StringBuilder(StartOfMonthlyInsertSQL, 600);
				values.Append(" Values('");
				values.Append(timestamp.ToString("yy-MM-dd HH:mm") + "',");
				values.Append(station.OutdoorTemperature.ToString(TempFormat, InvC) + ",");
				values.Append(station.OutdoorHumidity + ",");
				values.Append(station.OutdoorDewpoint.ToString(TempFormat, InvC) + ",");
				values.Append(station.WindAverage.ToString(WindAvgFormat, InvC) + ",");
				values.Append(station.RecentMaxGust.ToString(WindFormat, InvC) + ",");
				values.Append(station.AvgBearing + ",");
				values.Append(station.RainRate.ToString(RainFormat, InvC) + ",");
				values.Append(station.RainToday.ToString(RainFormat, InvC) + ",");
				values.Append(station.Pressure.ToString(PressFormat, InvC) + ",");
				values.Append(station.Raincounter.ToString(RainFormat, InvC) + ",");
				values.Append(station.IndoorTemperature.ToString(TempFormat, InvC) + ",");
				values.Append(station.IndoorHumidity + ",");
				values.Append(station.WindLatest.ToString(WindFormat, InvC) + ",");
				values.Append(station.WindChill.ToString(TempFormat, InvC) + ",");
				values.Append(station.HeatIndex.ToString(TempFormat, InvC) + ",");
				values.Append(station.UV.ToString(UVFormat, InvC) + ",");
				values.Append(station.SolarRad + ",");
				values.Append(station.ET.ToString(ETFormat, InvC) + ",");
				values.Append(station.AnnualETTotal.ToString(ETFormat, InvC) + ",");
				values.Append(station.ApparentTemperature.ToString(TempFormat, InvC) + ",");
				values.Append((Math.Round(station.CurrentSolarMax)) + ",");
				values.Append(station.SunshineHours.ToString(SunFormat, InvC) + ",");
				values.Append(station.Bearing + ",");
				values.Append(station.RG11RainToday.ToString(RainFormat, InvC) + ",");
				values.Append(station.RainSinceMidnight.ToString(RainFormat, InvC) + ",'");
				values.Append(station.CompassPoint(station.AvgBearing) + "','");
				values.Append(station.CompassPoint(station.Bearing) + "',");
				values.Append(station.FeelsLike.ToString(TempFormat, InvC) + ",");
				values.Append(station.Humidex.ToString(TempFormat, InvC));
				values.Append(")");

				string queryString = values.ToString();

				if (live)
				{
					// do the update
					await MySqlCommandAsync(queryString, MonthlyMySqlConn, "DoLogFile", true, true);
				}
				else
				{
					// save the string for later
					MySqlList.Add(queryString);
				}
			}
		}

		public const int NumExtraLogFileFields = 92;

		public void DoExtraLogFile(DateTime timestamp)
		{
			// Writes an entry to the n-minute extralogfile. Fields are comma-separated:
			// 0  Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  Current time - hh:mm
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum

			var filename = GetExtraLogFileName(timestamp);

			using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
			using (StreamWriter file = new StreamWriter(fs))
			{
				file.Write(timestamp.ToString("dd/MM/yy") + ListSeparator);						//0
				file.Write(timestamp.ToString("HH:mm") + ListSeparator);						//1

				for (int i = 1; i < 11; i++)
				{
					file.Write(station.ExtraTemp[i].ToString(TempFormat) + ListSeparator);		//2-11
				}
				for (int i = 1; i < 11; i++)
				{
					file.Write(station.ExtraHum[i].ToString(HumFormat) + ListSeparator);		//12-21
				}
				for (int i = 1; i < 11; i++)
				{
					file.Write(station.ExtraDewPoint[i].ToString(TempFormat) + ListSeparator);	//22-31
				}

				file.Write(station.SoilTemp1.ToString(TempFormat) + ListSeparator);		//32
				file.Write(station.SoilTemp2.ToString(TempFormat) + ListSeparator);		//33
				file.Write(station.SoilTemp3.ToString(TempFormat) + ListSeparator);		//34
				file.Write(station.SoilTemp4.ToString(TempFormat) + ListSeparator);		//35

				file.Write(station.SoilMoisture1 + ListSeparator);						//36
				file.Write(station.SoilMoisture2 + ListSeparator);						//37
				file.Write(station.SoilMoisture3 + ListSeparator);						//38
				file.Write(station.SoilMoisture4 + ListSeparator);						//39

				file.Write(station.LeafTemp1.ToString(TempFormat) + ListSeparator);		//40
				file.Write(station.LeafTemp2.ToString(TempFormat) + ListSeparator);		//41

				file.Write(station.LeafWetness1 + ListSeparator);						//42
				file.Write(station.LeafWetness2 + ListSeparator);						//43

				file.Write(station.SoilTemp5.ToString(TempFormat) + ListSeparator);		//44
				file.Write(station.SoilTemp6.ToString(TempFormat) + ListSeparator);		//45
				file.Write(station.SoilTemp7.ToString(TempFormat) + ListSeparator);		//46
				file.Write(station.SoilTemp8.ToString(TempFormat) + ListSeparator);		//47
				file.Write(station.SoilTemp9.ToString(TempFormat) + ListSeparator);		//48
				file.Write(station.SoilTemp10.ToString(TempFormat) + ListSeparator);	//49
				file.Write(station.SoilTemp11.ToString(TempFormat) + ListSeparator);	//50
				file.Write(station.SoilTemp12.ToString(TempFormat) + ListSeparator);	//51
				file.Write(station.SoilTemp13.ToString(TempFormat) + ListSeparator);	//52
				file.Write(station.SoilTemp14.ToString(TempFormat) + ListSeparator);	//53
				file.Write(station.SoilTemp15.ToString(TempFormat) + ListSeparator);	//54
				file.Write(station.SoilTemp16.ToString(TempFormat) + ListSeparator);	//55

				file.Write(station.SoilMoisture5 + ListSeparator);		//56
				file.Write(station.SoilMoisture6 + ListSeparator);		//57
				file.Write(station.SoilMoisture7 + ListSeparator);		//58
				file.Write(station.SoilMoisture8 + ListSeparator);		//59
				file.Write(station.SoilMoisture9 + ListSeparator);		//60
				file.Write(station.SoilMoisture10 + ListSeparator);		//61
				file.Write(station.SoilMoisture11 + ListSeparator);		//62
				file.Write(station.SoilMoisture12 + ListSeparator);		//63
				file.Write(station.SoilMoisture13 + ListSeparator);		//64
				file.Write(station.SoilMoisture14 + ListSeparator);		//65
				file.Write(station.SoilMoisture15 + ListSeparator);		//66
				file.Write(station.SoilMoisture16 + ListSeparator);		//67

				file.Write(station.AirQuality1.ToString("F1") + ListSeparator);		//68
				file.Write(station.AirQuality2.ToString("F1") + ListSeparator);		//69
				file.Write(station.AirQuality3.ToString("F1") + ListSeparator);		//70
				file.Write(station.AirQuality4.ToString("F1") + ListSeparator);		//71
				file.Write(station.AirQualityAvg1.ToString("F1") + ListSeparator);	//72
				file.Write(station.AirQualityAvg2.ToString("F1") + ListSeparator);	//73
				file.Write(station.AirQualityAvg3.ToString("F1") + ListSeparator);	//74
				file.Write(station.AirQualityAvg4.ToString("F1") + ListSeparator);	//75

				for (int i = 1; i < 8; i++)
				{
					file.Write(station.UserTemp[i].ToString(TempFormat) + ListSeparator);	//76-82
				}
				file.Write(station.UserTemp[8].ToString(TempFormat) + ListSeparator);		//83

				file.Write(station.CO2 + ListSeparator);									//84
				file.Write(station.CO2_24h + ListSeparator);								//85
				file.Write(station.CO2_pm2p5.ToString("F1") + ListSeparator);				//86
				file.Write(station.CO2_pm2p5_24h.ToString("F1") + ListSeparator);			//87
				file.Write(station.CO2_pm10.ToString("F1") + ListSeparator);				//88
				file.Write(station.CO2_pm10_24h.ToString("F1") + ListSeparator);			//89
				file.Write(station.CO2_temperature.ToString(TempFormat) + ListSeparator);	//90
				file.Write(station.CO2_humidity);											//91

				file.WriteLine();
				file.Close();
			}
		}

		public void DoAirLinkLogFile(DateTime timestamp)
		{
			// Writes an entry to the n-minute airlinklogfile. Fields are comma-separated:
			// 0  Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  Current time - hh:mm
			// 2  Indoor Temperature
			// 3  Indoor Humidity
			// 4  Indoor PM 1
			// 5  Indoor PM 2.5
			// 6  Indoor PM 2.5 1-hour
			// 7  Indoor PM 2.5 3-hour
			// 8  Indoor PM 2.5 24-hour
			// 9  Indoor PM 2.5 nowcast
			// 10 Indoor PM 10
			// 11 Indoor PM 10 1-hour
			// 12 Indoor PM 10 3-hour
			// 13 Indoor PM 10 24-hour
			// 14 Indoor PM 10 nowcast
			// 15 Indoor Percent received 1-hour
			// 16 Indoor Percent received 3-hour
			// 17 Indoor Percent received nowcast
			// 18 Indoor Percent received 24-hour
			// 19 Indoor AQI PM2.5
			// 20 Indoor AQI PM2.5 1-hour
			// 21 Indoor AQI PM2.5 3-hour
			// 22 Indoor AQI PM2.5 24-hour
			// 23 Indoor AQI PM2.5 nowcast
			// 24 Indoor AQI PM10
			// 25 Indoor AQI PM10 1-hour
			// 26 Indoor AQI PM10 3-hour
			// 27 Indoor AQI PM10 24-hour
			// 28 Indoor AQI PM10 nowcast
			// 29 Outdoor Temperature
			// 30 Outdoor Humidity
			// 31 Outdoor PM 1
			// 32 Outdoor PM 2.5
			// 33 Outdoor PM 2.5 1-hour
			// 34 Outdoor PM 2.5 3-hour
			// 35 Outdoor PM 2.5 24-hour
			// 36 Outdoor PM 2.5 nowcast
			// 37 Outdoor PM 10
			// 38 Outdoor PM 10 1-hour
			// 39 Outdoor PM 10 3-hour
			// 40 Outdoor PM 10 24-hour
			// 41 Outdoor PM 10 nowcast
			// 42 Outdoor Percent received 1-hour
			// 43 Outdoor Percent received 3-hour
			// 44 Outdoor Percent received nowcast
			// 45 Outdoor Percent received 24-hour
			// 46 Outdoor AQI PM2.5
			// 47 Outdoor AQI PM2.5 1-hour
			// 48 Outdoor AQI PM2.5 3-hour
			// 49 Outdoor AQI PM2.5 24-hour
			// 50 Outdoor AQI PM2.5 nowcast
			// 51 Outdoor AQI PM10
			// 52 Outdoor AQI PM10 1-hour
			// 53 Outdoor AQI PM10 3-hour
			// 54 Outdoor AQI PM10 24-hour
			// 55 Outdoor AQI PM10 nowcast

			var filename = GetAirLinkLogFileName(timestamp);

			using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
			using (StreamWriter file = new StreamWriter(fs))
			{
				file.Write(timestamp.ToString("dd/MM/yy") + ListSeparator);
				file.Write(timestamp.ToString("HH:mm") + ListSeparator);

				if (AirLinkInEnabled && airLinkDataIn != null)
				{
					file.Write(airLinkDataIn.temperature.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.humidity + ListSeparator);
					file.Write(airLinkDataIn.pm1.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm2p5.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm2p5_1hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm2p5_3hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm2p5_24hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm2p5_nowcast.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm10.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm10_1hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm10_3hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm10_24hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pm10_nowcast.ToString("F1") + ListSeparator);
					file.Write(airLinkDataIn.pct_1hr + ListSeparator);
					file.Write(airLinkDataIn.pct_3hr + ListSeparator);
					file.Write(airLinkDataIn.pct_24hr + ListSeparator);
					file.Write(airLinkDataIn.pct_nowcast + ListSeparator);
					if (AirQualityDPlaces > 0)
					{
						file.Write(airLinkDataIn.aqiPm2p5.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm2p5_1hr.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm2p5_3hr.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm2p5_24hr.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm2p5_nowcast.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm10.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm10_1hr.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm10_3hr.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm10_24hr.ToString(AirQualityFormat) + ListSeparator);
						file.Write(airLinkDataIn.aqiPm10_nowcast.ToString(AirQualityFormat) + ListSeparator);
					}
					else // Zero decimals - trucate value rather than round
					{
						file.Write((int)airLinkDataIn.aqiPm2p5 + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm2p5_1hr + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm2p5_3hr + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm2p5_24hr + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm2p5_nowcast + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm10 + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm10_1hr + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm10_3hr + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm10_24hr + ListSeparator);
						file.Write((int)airLinkDataIn.aqiPm10_nowcast + ListSeparator);
					}
				}
				else
				{
					// write zero values - subtract 2 for firmware version, wifi RSSI
					for (var i = 0; i < typeof(AirLinkData).GetProperties().Length - 2; i++)
					{
						file.Write("0" + ListSeparator);
					}
				}

				if (AirLinkOutEnabled && airLinkDataOut != null)
				{
					file.Write(airLinkDataOut.temperature.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.humidity + ListSeparator);
					file.Write(airLinkDataOut.pm1.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm2p5.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm2p5_1hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm2p5_3hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm2p5_24hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm2p5_nowcast.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm10.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm10_1hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm10_3hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm10_24hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pm10_nowcast.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.pct_1hr + ListSeparator);
					file.Write(airLinkDataOut.pct_3hr + ListSeparator);
					file.Write(airLinkDataOut.pct_24hr + ListSeparator);
					file.Write(airLinkDataOut.pct_nowcast + ListSeparator);
					file.Write(airLinkDataOut.aqiPm2p5.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm2p5_1hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm2p5_3hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm2p5_24hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm2p5_nowcast.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm10.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm10_1hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm10_3hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm10_24hr.ToString("F1") + ListSeparator);
					file.Write(airLinkDataOut.aqiPm10_nowcast.ToString("F1"));
				}
				else
				{
					// write zero values - subtract 2 for firmware version, wifi RSSI - subtract 1 for end field
					for (var i = 0; i < typeof(AirLinkData).GetProperties().Length - 3; i++)
					{
						file.Write("0" + ListSeparator);
					}
					file.Write("0");
				}

				file.WriteLine();
				file.Close();
			}
		}

		public void BackupData(bool daily, DateTime timestamp)
		{
			string dirpath = daily ? backupPath + "daily" + DirectorySeparator : backupPath;

			if (!Directory.Exists(dirpath))
			{
				LogMessage("BackupData: *** Error - backup folder does not exist - " + dirpath);
			}
			else
			{
				string[] dirs = Directory.GetDirectories(dirpath);
				Array.Sort(dirs);
				var dirlist = new List<string>(dirs);

				while (dirlist.Count > 10)
				{
					if (Path.GetFileName(dirlist[0]) == "daily")
					{
						LogMessage("BackupData: *** Error - the backup folder has unexpected contents");
						break;
					}
					else
					{
						Directory.Delete(dirlist[0], true);
						dirlist.RemoveAt(0);
					}
				}

				string foldername = timestamp.ToString("yyyyMMddHHmmss");

				foldername = dirpath + foldername + DirectorySeparator;

				LogMessage("BackupData: Creating backup folder " + foldername);

				var alltimebackup = foldername + "alltime.ini";
				var monthlyAlltimebackup = foldername + "monthlyalltime.ini";
				var daybackup = foldername + "dayfile.txt";
				var yesterdaybackup = foldername + "yesterday.ini";
				var todaybackup = foldername + "today.ini";
				var monthbackup = foldername + "month.ini";
				var yearbackup = foldername + "year.ini";
				var diarybackup = foldername + "diary.db";
				var configbackup = foldername + "Cumulus.ini";

				var LogFile = GetLogFileName(timestamp);
				var logbackup = foldername + LogFile.Replace(logFilePath, "");

				var extraFile = GetExtraLogFileName(timestamp);
				var extraBackup = foldername + extraFile.Replace(logFilePath, "");

				var AirLinkFile = GetAirLinkLogFileName(timestamp);
				var AirLinkBackup = foldername + AirLinkFile.Replace(logFilePath, "");

				if (!Directory.Exists(foldername))
				{
					Directory.CreateDirectory(foldername);
					CopyBackupFile(AlltimeIniFile, alltimebackup);
					CopyBackupFile(MonthlyAlltimeIniFile, monthlyAlltimebackup);
					CopyBackupFile(DayFileName, daybackup);
					CopyBackupFile(TodayIniFile, todaybackup);
					CopyBackupFile(YesterdayFile, yesterdaybackup);
					CopyBackupFile(LogFile, logbackup);
					CopyBackupFile(MonthIniFile, monthbackup);
					CopyBackupFile(YearIniFile, yearbackup);
					CopyBackupFile(diaryfile, diarybackup);
					CopyBackupFile("Cumulus.ini", configbackup);
					CopyBackupFile(extraFile, extraBackup);
					CopyBackupFile(AirLinkFile, AirLinkBackup);
					// Do not do this extra backup between 00:00 & Rollover hour on the first of the month
					// as the month has not yet rolled over - only applies for start-up backups
					if (timestamp.Day == 1 && timestamp.Hour >= RolloverHour)
					{
						// on the first of month, we also need to backup last months files as well
						var LogFile2 = GetLogFileName(timestamp.AddDays(-1));
						var logbackup2 = foldername + LogFile2.Replace(logFilePath, "");

						var extraFile2 = GetExtraLogFileName(timestamp.AddDays(-1));
						var extraBackup2 = foldername + extraFile2.Replace(logFilePath, "");

						var AirLinkFile2 = GetAirLinkLogFileName(timestamp.AddDays(-1));
						var AirLinkBackup2 = foldername + AirLinkFile2.Replace(logFilePath, "");

						CopyBackupFile(LogFile2, logbackup2, true);
						CopyBackupFile(extraFile2, extraBackup2, true);
						CopyBackupFile(AirLinkFile2, AirLinkBackup2, true);
					}

					LogMessage("Created backup folder " + foldername);
				}
				else
				{
					LogMessage("Backup folder " + foldername + " already exists, skipping backup");
				}
			}
		}

		private void CopyBackupFile(string src, string dest, bool overwrite=false)
		{
			try
			{
				if (File.Exists(src))
				{
					File.Copy(src, dest, overwrite);
				}
			}
			catch (Exception e)
			{
				LogMessage($"BackupData: Error copying {src} - {e}");
			}
		}

		/*
		/// <summary>
		/// Get a snapshot of the current data values
		/// </summary>
		/// <returns>Structure containing current values</returns>
		public CurrentData GetCurrentData()
		{
			CurrentData currentData = new CurrentData();

			if (station != null)
			{
				currentData.Avgbearing = station.AvgBearing;
				currentData.Bearing = station.Bearing;
				currentData.HeatIndex = station.HeatIndex;
				currentData.Humidex = station.Humidex;
				currentData.AppTemp = station.ApparentTemperature;
				currentData.FeelsLike = station.FeelsLike;
				currentData.IndoorHumidity = station.IndoorHumidity;
				currentData.IndoorTemperature = station.IndoorTemperature;
				currentData.OutdoorDewpoint = station.OutdoorDewpoint;
				currentData.OutdoorHumidity = station.OutdoorHumidity;
				currentData.OutdoorTemperature = station.OutdoorTemperature;
				currentData.AvgTempToday = station.TempTotalToday / station.tempsamplestoday;
				currentData.Pressure = station.Pressure;
				currentData.RainMonth = station.RainMonth;
				currentData.RainRate = station.RainRate;
				currentData.RainToday = station.RainToday;
				currentData.RainYesterday = station.RainYesterday;
				currentData.RainYear = station.RainYear;
				currentData.RainLastHour = station.RainLastHour;
				currentData.Recentmaxgust = station.RecentMaxGust;
				currentData.WindAverage = station.WindAverage;
				currentData.WindChill = station.WindChill;
				currentData.WindLatest = station.WindLatest;
				currentData.WindRunToday = station.WindRunToday;
				currentData.TempTrend = station.temptrendval;
				currentData.PressTrend = station.presstrendval;
			}

			return currentData;
		}
		*/

		/*public HighLowData GetHumidityHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.HighHumidityToday;
				data.TodayHighDT = station.HighHumidityTodayTime;

				data.TodayLow = station.LowHumidityToday;
				data.TodayLowDT = station.LowHumidityTodayTime;

				data.YesterdayHigh = station.Yesterdayhighouthumidity;
				data.YesterdayHighDT = station.Yesterdayhighouthumiditydt.ToLocalTime();

				data.YesterdayLow = station.Yesterdaylowouthumidity;
				data.YesterdayLowDT = station.Yesterdaylowouthumiditydt.ToLocalTime();

				data.MonthHigh = station.Monthhighouthumidity;
				data.MonthHighDT = station.Monthhighouthumiditydt.ToLocalTime();

				data.MonthLow = station.Monthlowouthumidity;
				data.MonthLowDT = station.Monthlowouthumiditydt.ToLocalTime();

				data.YearHigh = station.Yearhighouthumidity;
				data.YearHighDT = station.Yearhighouthumiditydt.ToLocalTime();

				data.YearLow = station.Yearlowouthumidity;
				data.YearLowDT = station.Yearlowouthumiditydt.ToLocalTime();
			}

			return data;
		}

		public HighLowData GetOuttempHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.Todayhighouttemp;
				data.TodayHighDT = station.Todayhighouttempdt.ToLocalTime();

				data.TodayLow = station.Todaylowouttemp;
				data.TodayLowDT = station.Todaylowouttempdt.ToLocalTime();

				data.YesterdayHigh = station.Yesterdayhighouttemp;
				data.YesterdayHighDT = station.Yesterdayhighouttempdt.ToLocalTime();

				data.YesterdayLow = station.Yesterdaylowouttemp;
				data.YesterdayLowDT = station.Yesterdaylowouttempdt.ToLocalTime();

				data.MonthHigh = station.Monthhighouttemp;
				data.MonthHighDT = station.Monthhighouttempdt.ToLocalTime();

				data.MonthLow = station.Monthlowouttemp;
				data.MonthLowDT = station.Monthlowouttempdt.ToLocalTime();

				data.YearHigh = station.Yearhighouttemp;
				data.YearHighDT = station.Yearhighouttempdt.ToLocalTime();

				data.YearLow = station.Yearlowouttemp;
				data.YearLowDT = station.Yearlowouttempdt.ToLocalTime();
			}

			return data;
		}

		public HighLowData GetPressureHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.Todayhighpressure;
				data.TodayHighDT = station.Todayhighpressuredt.ToLocalTime();

				data.TodayLow = station.Todaylowpressure;
				data.TodayLowDT = station.Todaylowpressuredt.ToLocalTime();

				data.YesterdayHigh = station.Yesterdayhighpressure;
				data.YesterdayHighDT = station.Yesterdayhighpressuredt.ToLocalTime();

				data.YesterdayLow = station.Yesterdaylowpressure;
				data.YesterdayLowDT = station.Yesterdaylowpressuredt.ToLocalTime();

				data.MonthHigh = station.Monthhighpressure;
				data.MonthHighDT = station.Monthhighpressuredt.ToLocalTime();

				data.MonthLow = station.Monthlowpressure;
				data.MonthLowDT = station.Monthlowpressuredt.ToLocalTime();

				data.YearHigh = station.Yearhighpressure;
				data.YearHighDT = station.Yearhighpressuredt.ToLocalTime();

				data.YearLow = station.Yearlowpressure;
				data.YearLowDT = station.Yearlowpressuredt.ToLocalTime();
			}

			return data;
		}

		public HighLowData GetRainRateHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.Todayhighrainrate;
				data.TodayHighDT = station.Todayhighrainratedt.ToLocalTime();

				data.YesterdayHigh = station.Yesterdayhighrainrate;
				data.YesterdayHighDT = station.Yesterdayhighrainratedt.ToLocalTime();

				data.MonthHigh = station.Monthhighrainrate;
				data.MonthHighDT = station.Monthhighrainratedt.ToLocalTime();

				data.YearHigh = station.Yearhighrainrate;
				data.YearHighDT = station.Yearhighrainratedt.ToLocalTime();
			}

			return data;
		}

		public HighLowData GetRainHourHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.Todayhighrainhour;
				data.TodayHighDT = station.Todayhighrainhourdt.ToLocalTime();

				data.MonthHigh = station.Monthhighrainhour;
				data.MonthHighDT = station.Monthhighrainhourdt.ToLocalTime();

				data.YearHigh = station.Yearhighrainhour;
				data.YearHighDT = station.Yearhighrainhourdt.ToLocalTime();
			}

			return data;
		}

		public HighLowData GetGustHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.Todayhighgust;
				data.TodayHighDT = station.Todayhighgustdt.ToLocalTime();

				data.YesterdayHigh = station.Yesterdayhighgust;
				data.YesterdayHighDT = station.Yesterdayhighgustdt.ToLocalTime();

				data.MonthHigh = station.ThisMonthRecs.HighGust.Val;
				data.MonthHighDT = station.ThisMonthRecs.HighGust.Ts.ToLocalTime();

				data.YearHigh = station.Yearhighgust;
				data.YearHighDT = station.Yearhighgustdt.ToLocalTime();
			}

			return data;
		}

		public HighLowData GetSpeedHighLowData()
		{
			HighLowData data = new HighLowData();

			if (station != null)
			{
				data.TodayHigh = station.Todayhighspeed;
				data.TodayHighDT = station.Todayhighspeeddt.ToLocalTime();

				data.YesterdayHigh = station.Yesterdayhighspeed;
				data.YesterdayHighDT = station.Yesterdayhighspeeddt.ToLocalTime();

				data.MonthHigh = station.Monthhighspeed;
				data.MonthHighDT = station.Monthhighspeeddt.ToLocalTime();

				data.YearHigh = station.Yearhighspeed;
				data.YearHighDT = station.Yearhighspeeddt.ToLocalTime();
			}

			return data;
		}*/

		/*
		public string GetForecast()
		{
			return station.Forecast;
		}

		public string GetCurrentActivity()
		{
			return CurrentActivity;
		}

		public bool GetImportDataSetting()
		{
			return ImportData;
		}

		public void SetImportDataSetting(bool setting)
		{
			ImportData = setting;
		}

		public bool GetLogExtraDataSetting()
		{
			return LogExtraData;
		}

		public void SetLogExtraDataSetting(bool setting)
		{
			LogExtraData = setting;
		}

		public string GetCumulusIniPath()
		{
			return CumulusIniPath;
		}

		public void SetCumulusIniPath(string inipath)
		{
			CumulusIniPath = inipath;
		}

		public int GetLogInterval()
		{
			return LogInterval;
		}

		public void SetLogInterval(int interval)
		{
			LogInterval = interval;
		}
		*/

		public int GetHourInc(DateTime timestamp)
		{
			if (RolloverHour == 0)
			{
				return 0;
			}
			else
			{
				try
				{
					if (Use10amInSummer && TimeZoneInfo.Local.IsDaylightSavingTime(timestamp))
					{
						// Locale is currently on Daylight time
						return -10;
					}

					else
					{
						// Locale is currently on Standard time or unknown
						return -9;
					}
				}
				catch (Exception)
				{
					return -9;
				}
			}
		}

		public int GetHourInc()
		{
			return GetHourInc(DateTime.Now);
		}

		/*
		private bool IsDaylightSavings()
		{
			return TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now);
		}
		*/

		public string Beaufort(double Bspeed) // Takes speed in current unit, returns Bft number as text
		{
			return station.Beaufort(Bspeed).ToString();
		}

		public string BeaufortDesc(double Bspeed)
		{
			// Takes speed in current units, returns Bft description

			// Convert to Force
			var force = station.Beaufort(Bspeed);
			switch (force)
			{
				case 0:
					return Calm;
				case 1:
					return Lightair;
				case 2:
					return Lightbreeze;
				case 3:
					return Gentlebreeze;
				case 4:
					return Moderatebreeze;
				case 5:
					return Freshbreeze;
				case 6:
					return Strongbreeze;
				case 7:
					return Neargale;
				case 8:
					return Gale;
				case 9:
					return Stronggale;
				case 10:
					return Storm;
				case 11:
					return Violentstorm;
				case 12:
					return Hurricane;
				default:
					return "UNKNOWN";
			}
		}

		public void LogErrorMessage(string message)
		{
			LatestError = message;
			LatestErrorTS = DateTime.Now;
			LogMessage(message);
		}

		public void LogSpikeRemoval(string message)
		{
			if (ErrorLogSpikeRemoval)
			{
				LogErrorMessage("Spike removal: " + message);
			}
		}

		public void Stop()
		{
			LogMessage("Cumulus closing");

			//WriteIniFile();

			//httpServer.Stop();

			//if (httpServer != null) httpServer.Dispose();

			// Stop the timers
			try
			{
				LogMessage("Stopping timers");
				RealtimeTimer.Stop();
				WundTimer.Stop();
				WebTimer.Stop();
				AwekasTimer.Stop();
				MQTTTimer.Stop();
				//AirLinkTimer.Stop();
				CustomHttpSecondsTimer.Stop();
				CustomMysqlSecondsTimer.Stop();
				MQTTTimer.Stop();
			}
			catch { }

			if (station != null)
			{
				LogMessage("Stopping station...");
				station.Stop();
				LogMessage("Station stopped");

				if (station.HaveReadData)
				{
					LogMessage("Writing today.ini file");
					station.WriteTodayFile(DateTime.Now, false);
					LogMessage("Completed writing today.ini file");
				}
				else
				{
					LogMessage("No data read this session, today.ini not written");
				}

				LogMessage("Stopping extra sensors...");
				// If we have a Outdoor AirLink sensor, and it is linked to this WLL then stop it now
				airLinkOut?.Stop();
				// If we have a Indoor AirLink sensor, and it is linked to this WLL then stop it now
				airLinkIn?.Stop();
				LogMessage("Extra sensors stopped");

			}
			LogMessage("Station shutdown complete");
		}

		public void ExecuteProgram(string externalProgram, string externalParams)
		{
			// Prepare the process to run
			ProcessStartInfo start = new ProcessStartInfo()
			{
				// Enter in the command line arguments
				Arguments = externalParams,
				// Enter the executable to run, including the complete path
				FileName = externalProgram,
				// Dont show a console window
				CreateNoWindow = true
			};

			// Run the external process
			Process.Start(start);
		}

		public void DoHTMLFiles()
		{
			try
			{
				if (!RealtimeEnabled)
				{
					CreateRealtimeFile(999);
				}

				LogDebugMessage("Creating standard web files");
				for (var i = 0; i < StdWebFiles.Length; i++)
				{
					if (StdWebFiles[i].Create && !string.IsNullOrWhiteSpace(StdWebFiles[i].TemplateFileName))
					{
						var destFile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
						ProcessTemplateFile(StdWebFiles[i].TemplateFileName, destFile, tokenParser);
					}
				}
				LogDebugMessage("Done creating standard Data file");

				LogDebugMessage("Creating graph data files");
				station.CreateGraphDataFiles();
				LogDebugMessage("Done creating graph data files");

				//LogDebugMessage("Creating extra files");
				// handle any extra files
				for (int i = 0; i < numextrafiles; i++)
				{
					if (!ExtraFiles[i].realtime && !ExtraFiles[i].endofday)
					{
						var uploadfile = ExtraFiles[i].local;
						var remotefile = ExtraFiles[i].remote;

						if ((uploadfile.Length > 0) && (remotefile.Length > 0))
						{
							if (uploadfile == "<currentlogfile>")
							{
								uploadfile = GetLogFileName(DateTime.Now);
							}
							else if (uploadfile == "<currentextralogfile>")
							{
								uploadfile = GetExtraLogFileName(DateTime.Now);
							}
							else if (uploadfile == "<airlinklogfile")
							{
								uploadfile = GetAirLinkLogFileName(DateTime.Now);
							}

							if (File.Exists(uploadfile))
							{
								if (remotefile.Contains("<currentlogfile>"))
								{
									remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));
								}
								else if (remotefile.Contains("<currentextralogfile>"))
								{
									remotefile = remotefile.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(DateTime.Now)));
								}
								else if (remotefile.Contains("<airlinklogfile"))
								{
									remotefile = remotefile.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(DateTime.Now)));
								}

								if (ExtraFiles[i].process)
								{
									LogDebugMessage($"Interval: Processing extra file[{i}] - {uploadfile}");
									// process the file
									var utf8WithoutBom = new UTF8Encoding(false);
									var encoding = UTF8encode ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
									tokenParser.Encoding = encoding;
									tokenParser.SourceFile = uploadfile;
									var output = tokenParser.ToString();
									uploadfile += "tmp";
									try
									{
										using (StreamWriter file = new StreamWriter(uploadfile, false, encoding))
										{
											file.Write(output);

											file.Close();
										}
									}
									catch (Exception ex)
									{
										LogDebugMessage($"Interval: Error writing file[{i}] - {uploadfile}");
										LogDebugMessage(ex.Message);
									}
									//LogDebugMessage("Finished processing extra file " + uploadfile);
								}

								if (!ExtraFiles[i].FTP)
								{
									// just copy the file
									LogDebugMessage($"Interval: Copying extra file[{i}] {uploadfile} to {remotefile}");
									try
									{
										File.Copy(uploadfile, remotefile, true);
									}
									catch (Exception ex)
									{
										LogDebugMessage($"Interval: Error copying extra file[{i}]: " + ex.Message);
									}
									//LogDebugMessage("Finished copying extra file " + uploadfile);
								}
							}
							else
							{
								LogMessage($"Interval: Warning, extra web file[{i}] not found - {uploadfile}");
							}
						}
					}
				}

				if (!string.IsNullOrEmpty(ExternalProgram))
				{
					LogDebugMessage("Interval: Executing program " + ExternalProgram + " " + ExternalParams);
					try
					{
						ExecuteProgram(ExternalProgram, ExternalParams);
						LogDebugMessage("Interval: External program started");
					}
					catch (Exception ex)
					{
						LogMessage("Interval: Error starting external program: " + ex.Message);
					}
				}

				//LogDebugMessage("Done creating extra files");

				if (!string.IsNullOrEmpty(FtpHostname))
				{
					DoFTPLogin();
				}
			}
			finally
			{
				WebUpdating = 0;
			}
		}

		public void DoFTPLogin()
		{
			var remotePath = "";

			if (FtpDirectory.Length > 0)
			{
				remotePath = (FtpDirectory.EndsWith("/") ? FtpDirectory : FtpDirectory + "/");
			}

			if (Sslftp == FtpProtocols.SFTP)
			{
				// BUILD 3092 - added alternate SFTP authenication options
				ConnectionInfo connectionInfo;
				if (SshftpAuthentication == "password")
				{
					connectionInfo = new ConnectionInfo(FtpHostname, FtpHostPort, FtpUsername, new PasswordAuthenticationMethod(FtpUsername, FtpPassword));
					LogFtpDebugMessage("SFTP[Int]: Connecting using password authentication");
				}
				else if (SshftpAuthentication == "psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(SshftpPskFile);
					connectionInfo = new ConnectionInfo(FtpHostname, FtpHostPort, FtpUsername, new PrivateKeyAuthenticationMethod(FtpUsername, pskFile));
					LogFtpDebugMessage("SFTP[Int]: Connecting using PSK authentication");
				}
				else if (SshftpAuthentication == "password_psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(SshftpPskFile);
					connectionInfo = new ConnectionInfo(FtpHostname, FtpHostPort, FtpUsername, new PasswordAuthenticationMethod(FtpUsername, FtpPassword), new PrivateKeyAuthenticationMethod(FtpUsername, pskFile));
					LogFtpDebugMessage("SFTP[Int]: Connecting using password or PSK authentication");
				}
				else
				{
					LogFtpMessage($"SFTP[Int]: Invalid SshftpAuthentication specified [{SshftpAuthentication}]");
					return;
				}

				using (SftpClient conn = new SftpClient(connectionInfo))
				{
					try
					{
						LogFtpDebugMessage($"SFTP[Int]: CumulusMX Connecting to {FtpHostname} on port {FtpHostPort}");
						conn.Connect();
					}
					catch (Exception ex)
					{
						LogFtpMessage($"SFTP[Int]: Error connecting sftp - {ex.Message}");
						return;
					}

					if (conn.IsConnected)
					{
						if (NOAANeedFTP)
						{
							try
							{
								// upload NOAA reports
								LogFtpDebugMessage("SFTP[Int]: Uploading NOAA reports");

								var uploadfile = ReportPath + NOAALatestMonthlyReport;
								var remotefile = NOAAFTPDirectory + '/' + NOAALatestMonthlyReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								uploadfile = ReportPath + NOAALatestYearlyReport;
								remotefile = NOAAFTPDirectory + '/' + NOAALatestYearlyReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								LogFtpDebugMessage("SFTP[Int]: Done uploading NOAA reports");
							}
							catch (Exception e)
							{
								LogFtpMessage($"SFTP[Int]: Error uploading file - {e.Message}");
							}
							NOAANeedFTP = false;
						}

						LogFtpDebugMessage("SFTP[Int]: Uploading extra files");
						// Extra files
						for (int i = 0; i < numextrafiles; i++)
						{
							var uploadfile = ExtraFiles[i].local;
							var remotefile = ExtraFiles[i].remote;

							if ((uploadfile.Length > 0) &&
								(remotefile.Length > 0) &&
								!ExtraFiles[i].realtime &&
								(!ExtraFiles[i].endofday || EODfilesNeedFTP == ExtraFiles[i].endofday) && // Either, it's not flagged as an EOD file, OR: It is flagged as EOD and EOD FTP is required
								ExtraFiles[i].FTP)
							{
								// For EOD files, we want the previous days log files since it is now just past the day rollover time. Makes a difference on month rollover
								var logDay = ExtraFiles[i].endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

								if (uploadfile == "<currentlogfile>")
								{
									uploadfile = GetLogFileName(logDay);
								}
								else if (uploadfile == "<currentextralogfile>")
								{
									uploadfile = GetExtraLogFileName(logDay);
								}
								else if (uploadfile == "<airlinklogfile")
								{
									uploadfile = GetAirLinkLogFileName(logDay);
								}

								if (File.Exists(uploadfile))
								{
									if (remotefile.Contains("<currentlogfile>"))
									{
										remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(logDay)));
									}
									else if (remotefile.Contains("<currentextralogfile>"))
									{
										remotefile = remotefile.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(logDay)));
									}
									else if (remotefile.Contains("<airlinklogfile"))
									{
										remotefile = remotefile.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(logDay)));
									}

									// all checks OK, file needs to be uploaded
									if (ExtraFiles[i].process)
									{
										// we've already processed the file
										uploadfile += "tmp";
									}

									try
									{
										UploadFile(conn, uploadfile, remotefile, -1);
									}
									catch (Exception e)
									{
										LogFtpMessage($"SFTP[Int]: Error uploading Extra web file #{i} [{uploadfile}]");
										LogFtpMessage($"SFTP[Int]: Error = {e.Message}");
									}
								}
								else
								{
									LogFtpMessage($"SFTP[Int]: Extra web file #{i} [{uploadfile}] not found!");
								}
							}
						}
						if (EODfilesNeedFTP)
						{
							EODfilesNeedFTP = false;
						}
						LogFtpDebugMessage("SFTP[Int]: Done uploading extra files");

						// standard files
						LogFtpDebugMessage("SFTP[Int]: Uploading standard web files");
						for (var i = 0; i < StdWebFiles.Length; i++)
						{
							if (StdWebFiles[i].FTP && StdWebFiles[i].FtpRequired)
							{
								try
								{
									var localFile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
									var remotefile = remotePath + StdWebFiles[i].RemoteFileName;
									UploadFile(conn, localFile, remotefile, -1);
								}
								catch (Exception e)
								{
									LogFtpMessage($"SFTP[Int]: Error uploading standard data file [{StdWebFiles[i].LocalFileName}]");
									LogFtpMessage($"SFTP[Int]: Error = {e}");
								}
							}
						}
						LogFtpDebugMessage("SFTP[Int]: Done uploading standard web files");

						LogFtpDebugMessage("SFTP[Int]: Uploading graph data files");

						for (int i = 0; i < GraphDataFiles.Length; i++)
						{
							if (GraphDataFiles[i].FTP && GraphDataFiles[i].FtpRequired)
							{
								var uploadfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].LocalFileName;
								var remotefile = remotePath + GraphDataFiles[i].RemoteFileName;

								try
								{
									UploadFile(conn, uploadfile, remotefile, -1);
									// The config files only need uploading once per change
									if (GraphDataFiles[i].LocalFileName == "availabledata.json" ||
										GraphDataFiles[i].LocalFileName == "graphconfig.json")
									{
										GraphDataFiles[i].FtpRequired = false;
									}
								}
								catch (Exception e)
								{
									LogFtpMessage($"SFTP[Int]: Error uploading graph data file [{uploadfile}]");
									LogFtpMessage($"SFTP[Int]: Error = {e}");
								}
							}
						}
						LogFtpDebugMessage("SFTP[Int]: Done uploading graph data files");

						LogFtpMessage("SFTP[Int]: Uploading daily graph data files");
						for (int i = 0; i < GraphDataEodFiles.Length; i++)
						{
							if (GraphDataEodFiles[i].FTP && GraphDataEodFiles[i].FtpRequired)
							{
								var uploadfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].LocalFileName;
								var remotefile = remotePath + GraphDataEodFiles[i].RemoteFileName;
								try
								{
									UploadFile(conn, uploadfile, remotefile, -1);
									// Uploaded OK, reset the upload required flag
									GraphDataEodFiles[i].FtpRequired = false;
								}
								catch (Exception e)
								{
									LogFtpMessage($"SFTP[Int]: Error uploading daily graph data file [{uploadfile}]");
									LogFtpMessage($"SFTP[Int]: Error = {e}");
								}
							}
						}
						LogFtpMessage("SFTP[Int]: Done uploading daily graph data files");

						if (IncludeMoonImage && MoonImageReady)
						{
							try
							{
								LogFtpMessage("SFTP[Int]: Uploading Moon image file");
								UploadFile(conn, "web" + DirectorySeparator + "moon.png", remotePath + MoonImageFtpDest, -1);
								LogFtpMessage("SFTP[Int]: Done uploading Moon image file");
								// clear the image ready for FTP flag, only upload once an hour
								MoonImageReady = false;
							}
							catch (Exception e)
							{
								LogMessage($"SFTP[Int]: Error uploading moon image - {e.Message}");
							}
						}
					}
					try
					{
						// do not error on disconnect
						conn.Disconnect();
					}
					catch { }
				}
				LogFtpDebugMessage("SFTP[Int]: Connection process complete");
			}
			else
			{
				using (FtpClient conn = new FtpClient())
				{
					if (FTPlogging) FtpTrace.WriteLine(""); // insert a blank line
					LogFtpDebugMessage($"FTP[Int]: CumulusMX Connecting to " + FtpHostname);
					conn.Host = FtpHostname;
					conn.Port = FtpHostPort;
					conn.Credentials = new NetworkCredential(FtpUsername, FtpPassword);

					if (Sslftp == FtpProtocols.FTPS)
					{
						// Explicit = Current protocol - connects using FTP and switches to TLS
						// Implicit = Old depreciated protcol - connects using TLS
						conn.EncryptionMode = DisableFtpsExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
						conn.DataConnectionEncryption = true;
						conn.ValidateAnyCertificate = true;
						// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specify protocols
						conn.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
					}

					if (ActiveFTPMode)
					{
						conn.DataConnectionType = FtpDataConnectionType.PORT;
					}
					else if (DisableFtpsEPSV)
					{
						conn.DataConnectionType = FtpDataConnectionType.PASV;
					}

					try
					{
						conn.Connect();
					}
					catch (Exception ex)
					{
						LogFtpMessage("FTP[Int]: Error connecting ftp - " + ex.Message);
						return;
					}

					conn.EnableThreadSafeDataConnections = false; // use same connection for all transfers

					if (conn.IsConnected)
					{
						if (NOAANeedFTP)
						{
							try
							{
								// upload NOAA reports
								LogFtpDebugMessage("FTP[Int]: Uploading NOAA reports");

								var uploadfile = ReportPath + NOAALatestMonthlyReport;
								var remotefile = NOAAFTPDirectory + '/' + NOAALatestMonthlyReport;

								UploadFile(conn, uploadfile, remotefile);

								uploadfile = ReportPath + NOAALatestYearlyReport;
								remotefile = NOAAFTPDirectory + '/' + NOAALatestYearlyReport;

								UploadFile(conn, uploadfile, remotefile);
								LogFtpDebugMessage("FTP[Int]: Upload of NOAA reports complete");
							}
							catch (Exception e)
							{
								LogFtpMessage($"FTP[Int]: Error uploading NOAA files: {e.Message}");
							}
							NOAANeedFTP = false;
						}

						// Extra files
						LogFtpDebugMessage("FTP[Int]: Uploading Extra files");
						for (int i = 0; i < numextrafiles; i++)
						{
							var uploadfile = ExtraFiles[i].local;
							var remotefile = ExtraFiles[i].remote;

							if ((uploadfile.Length > 0) &&
								(remotefile.Length > 0) &&
								!ExtraFiles[i].realtime &&
								(EODfilesNeedFTP || (EODfilesNeedFTP == ExtraFiles[i].endofday)) &&
								ExtraFiles[i].FTP)
							{
								// For EOD files, we want the previous days log files since it is now just past the day rollover time. Makes a difference on month rollover
								var logDay = ExtraFiles[i].endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

								if (uploadfile == "<currentlogfile>")
								{
									uploadfile = GetLogFileName(logDay);
								}
								else if (uploadfile == "<currentextralogfile>")
								{
									uploadfile = GetExtraLogFileName(logDay);
								}
								else if (uploadfile == "<airlinklogfile")
								{
									uploadfile = GetAirLinkLogFileName(logDay);
								}

								if (File.Exists(uploadfile))
								{
									if (remotefile.Contains("<currentlogfile>"))
									{
										remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(logDay)));
									}
									else if (remotefile.Contains("<currentextralogfile>"))
									{
										remotefile = remotefile.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(logDay)));
									}
									else if (remotefile.Contains("<airlinklogfile"))
									{
										remotefile = remotefile.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(logDay)));
									}

									// all checks OK, file needs to be uploaded
									if (ExtraFiles[i].process)
									{
										// we've already processed the file
										uploadfile += "tmp";
									}

									try
									{
										UploadFile(conn, uploadfile, remotefile);
									}
									catch (Exception e)
									{
										LogFtpMessage($"FTP[Int]: Error uploading file {uploadfile}: {e.Message}");
									}
								}
								else
								{
									LogFtpMessage("FTP[Int]: Extra web file #" + i + " [" + uploadfile + "] not found!");
								}
							}
						}
						if (EODfilesNeedFTP)
						{
							EODfilesNeedFTP = false;
						}
						// standard files
						LogFtpDebugMessage("FTP[Int]: Uploading standard Data file");
						for (int i = 0; i < StdWebFiles.Length; i++)
						{
							if (StdWebFiles[i].FTP && StdWebFiles[i].FtpRequired)
							{
								try
								{
									var localfile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
									UploadFile(conn, localfile, remotePath + StdWebFiles[i].RemoteFileName);
								}
								catch (Exception e)
								{
									LogFtpMessage($"FTP[Int]: Error uploading file {StdWebFiles[i].LocalFileName}: {e}");
								}
							}
						}
						LogFtpMessage("Done uploading standard Data file");

						LogFtpDebugMessage("FTP[Int]: Uploading graph data files");
						for (int i = 0; i < GraphDataFiles.Length; i++)
						{
							if (GraphDataFiles[i].FTP && GraphDataFiles[i].FtpRequired)
							{
								try
								{
									var localfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].LocalFileName;
									var remotefile = remotePath + GraphDataFiles[i].RemoteFileName;
									UploadFile(conn, localfile, remotefile);
								}
								catch (Exception e)
								{
									LogFtpMessage($"FTP[Int]: Error uploading graph data file [{GraphDataFiles[i].LocalFileName}]");
									LogFtpMessage($"FTP[Int]: Error = {e}");
								}
							}
						}
						LogFtpMessage("Done uploading graph data files");

						LogFtpMessage("FTP[Int]: Uploading daily graph data files");
						for (int i = 0; i < GraphDataEodFiles.Length; i++)
						{
							if (GraphDataEodFiles[i].FTP && GraphDataEodFiles[i].FtpRequired)
							{
								var localfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].LocalFileName;
								var remotefile = remotePath + GraphDataEodFiles[i].RemoteFileName;
								try
								{
									UploadFile(conn, localfile, remotefile, -1);
									// Uploaded OK, reset the upload required flag
									GraphDataEodFiles[i].FtpRequired = false;
								}
								catch (Exception e)
								{
									LogFtpMessage($"SFTP[Int]: Error uploading daily graph data file [{GraphDataEodFiles[i].LocalFileName}]");
									LogFtpMessage($"SFTP[Int]: Error = {e}");
								}
							}
						}
						LogFtpMessage("FTP[Int]: Done uploading daily graph data files");

						if (IncludeMoonImage && MoonImageReady)
						{
							try
							{
								LogFtpDebugMessage("FTP[Int]: Uploading Moon image file");
								UploadFile(conn, "web" + DirectorySeparator + "moon.png", remotePath + MoonImageFtpDest);
								// clear the image ready for FTP flag, only upload once an hour
								MoonImageReady = false;
							}
							catch (Exception e)
							{
								LogMessage($"FTP[Int]: Error uploading moon image - {e.Message}");
							}
						}
					}

					// b3045 - dispose of connection
					conn.Disconnect();
					LogFtpDebugMessage("FTP[Int]: Disconnected from " + FtpHostname);
				}
				LogFtpMessage("FTP[Int]: Process complete");
			}
		}

		private void UploadFile(FtpClient conn, string localfile, string remotefile, int cycle = -1)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (FTPlogging) FtpTrace.WriteLine("");
			try
			{
				if (!File.Exists(localfile))
				{
					LogMessage($"FTP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
					return;
				}

				if (DeleteBeforeUpload)
				{
					// delete the existing file
					try
					{
						LogFtpDebugMessage($"FTP[{cycleStr}]: Deleting {remotefile}");
						conn.DeleteFile(remotefile);
					}
					catch (Exception ex)
					{
						LogFtpMessage($"FTP[{cycleStr}]: Error deleting {remotefile} : {ex.Message}");
					}
				}

				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {localfile} to {remotefilename}");

				using (Stream ostream = conn.OpenWrite(remotefilename))
				using (Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					try
					{
						var buffer = new byte[4096];
						int read;
						while ((read = istream.Read(buffer, 0, buffer.Length)) > 0)
						{
							ostream.Write(buffer, 0, read);
						}

						LogFtpDebugMessage($"FTP[{cycleStr}]: Uploaded {localfile}");
					}
					catch (Exception ex)
					{
						LogFtpMessage($"FTP[{cycleStr}]: Error uploading {localfile} to {remotefilename} : {ex.Message}");
					}
					finally
					{
						ostream.Close();
						istream.Close();
						conn.GetReply(); // required FluentFTP 19.2
					}
				}

				if (FTPRename)
				{
					// rename the file
					LogFtpDebugMessage($"FTP[{cycleStr}]: Renaming {remotefilename} to {remotefile}");

					try
					{
						conn.Rename(remotefilename, remotefile);
						LogFtpDebugMessage($"FTP[{cycleStr}]: Renamed {remotefilename}");
					}
					catch (Exception ex)
					{
						LogFtpMessage($"FTP[{cycleStr}]: Error renaming {remotefilename} to {remotefile} : {ex.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error uploading {localfile} to {remotefile} : {ex.Message}");
			}
		}

		private void UploadFile(SftpClient conn, string localfile, string remotefile, int cycle)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (!File.Exists(localfile))
			{
				LogMessage($"SFTP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
				return;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {localfile}");
					return;
				}
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {localfile}");
				return;
			}

			try
			{
				// No delete before upload required for SFTP as we use the overwrite flag

				using (Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					try
					{
						LogFtpDebugMessage($"SFTP[{cycleStr}]: Uploading {localfile} to {remotefilename}");

						conn.OperationTimeout = TimeSpan.FromSeconds(15);
						conn.UploadFile(istream, remotefilename, true);

						LogFtpDebugMessage($"SFTP[{cycleStr}]: Uploaded {localfile}");
					}
					catch (Exception ex)
					{
						LogFtpMessage($"SFTP[{cycleStr}]: Error uploading {localfile} to {remotefilename} : {ex.Message}");

						if (ex.Message.Contains("Permission denied")) // Non-fatal
							return;

						// Lets start again anyway! Too hard to tell if the error is recoverable
						conn.Dispose();
						return;
					}
				}

				if (FTPRename)
				{
					// rename the file
					try
					{
						LogFtpDebugMessage($"SFTP[{cycleStr}]: Renaming {remotefilename} to {remotefile}");
						conn.RenameFile(remotefilename, remotefile, true);
						LogFtpDebugMessage($"SFTP[{cycleStr}]: Renamed {remotefilename}");
					}
					catch (Exception ex)
					{
						LogFtpMessage($"SFTP[{cycleStr}]: Error renaming {remotefilename} to {remotefile} : {ex.Message}");
						return;
					}
				}
				LogFtpDebugMessage($"SFTP[{cycleStr}]: Completed uploading {localfile} to {remotefile}");
			}
			catch (Exception ex)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: Error uploading {localfile} to {remotefile} - {ex.Message}");
			}
		}

		public void LogMessage(string message)
		{
			Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
		}

		public void LogDebugMessage(string message)
		{
			if (ProgramOptions.DebugLogging || ProgramOptions.DataLogging)
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogDataMessage(string message)
		{
			if (ProgramOptions.DataLogging)
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogFtpMessage(string message)
		{
			LogMessage(message);
			if (FTPlogging)
			{
				FtpTraceListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogFtpDebugMessage(string message)
		{
			if (FTPlogging)
			{
				LogDebugMessage(message);
				FtpTraceListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogConsoleMessage(string message)
		{
			if (!Program.service)
			{
				Console.WriteLine(message);
			}

			Program.svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			Program.svcTextListener.Flush();
		}

		/*
		public string ReplaceCommas(string AStr)
		{
			return AStr.Replace(',', '.');
		}
		*/

		private void CreateRealtimeFile(int cycle)
		{
			/*
			Example: 18/10/08 16:03:45 8.4 84 5.8 24.2 33.0 261 0.0 1.0 999.7 W 6 mph C mb mm 146.6 +0.1 85.2 588.4 11.6 20.3 57 3.6 -0.7 10.9 12:00 7.8 14:41 37.4 14:38 44.0 14:28 999.8 16:01 998.4 12:06 1.8.2 448 36.0 10.3 10.5 0 9.3

			Field  Example    Description
			1      18/10/08   date (always dd/mm/yy)
			2      16:03:45   time (always hh:mm:ss)
			3      8.4        outside temperature
			4      84         relative humidity
			5      5.8        dewpoint
			6      24.2       wind speed (average)
			7      33.0       latest wind speed
			8      261        wind bearing
			9      0.0        current rain rate
			10     1.0        rain today
			11     999.7      barometer
			12     W          wind direction
			13     6          wind speed (beaufort)
			14     mph        wind units
			15     C          temperature units
			16     mb         pressure units
			17     mm         rain units
			18     146.6      wind run (today)
			19     +0.1       pressure trend value
			20     85.2       monthly rain
			21     588.4      yearly rain
			22     11.6       yesterday's rainfall
			23     20.3       inside temperature
			24     57         inside humidity
			25     3.6        wind chill
			26     -0.7       temperature trend value
			27     10.9       today's high temp
			28     12:00      time of today's high temp (hh:mm)
			29     7.8        today's low temp
			30     14:41      time of today's low temp (hh:mm)
			31     37.4       today's high wind speed (average)
			32     14:38      time of today's high wind speed (average) (hh:mm)
			33     44.0       today's high wind gust
			34     14:28      time of today's high wind gust (hh:mm)
			35     999.8      today's high pressure
			36     16:01      time of today's high pressure (hh:mm)
			37     998.4      today's low pressure
			38     12:06      time of today's low pressure (hh:mm)
			39     1.8.2      Cumulus version
			40     448        Cumulus build
			41     36.0       10-minute high gust
			42     10.3       heat index
			43     10.5       humidex
			44                UV
			45                ET
			46                Solar radiation
			47     234        Average Bearing (degrees)
			48     2.5        Rain last hour
			49     5          Forecast number
			50     1          Is daylight? (1 = yes)
			51     0          Sensor contact lost (1 = yes)
			52     NNW        wind direction (average)
			53     2040       Cloudbase
			54     ft         Cloudbase units
			55     12.3       Apparent Temp
			56     11.4       Sunshine hours today
			57     420        Current theoretical max solar radiation
			58     1          Is sunny?
			59     8.4        Feels Like temperature
		  */

			// Does the user want to create the realtime.txt file?
			if (!RealtimeFiles[0].Create)
			{
				return;
			}

			var filename = AppDir + RealtimeFile;
			DateTime timestamp = DateTime.Now;

			try
			{
				LogDebugMessage($"Realtime[{cycle}]: Creating realtime.txt");
				using (StreamWriter file = new StreamWriter(filename, false))
				{
					var InvC = new CultureInfo("");

					file.Write(timestamp.ToString("dd/MM/yy HH:mm:ss "));                          // 1, 2
					file.Write(station.OutdoorTemperature.ToString(TempFormat, InvC) + ' ');       // 3
					file.Write(station.OutdoorHumidity.ToString() + ' ');                          // 4
					file.Write(station.OutdoorDewpoint.ToString(TempFormat, InvC) + ' ');          // 5
					file.Write(station.WindAverage.ToString(WindAvgFormat, InvC) + ' ');           // 6
					file.Write(station.WindLatest.ToString(WindFormat, InvC) + ' ');               // 7
					file.Write(station.Bearing.ToString() + ' ');                                  // 8
					file.Write(station.RainRate.ToString(RainFormat, InvC) + ' ');                 // 9
					file.Write(station.RainToday.ToString(RainFormat, InvC) + ' ');                // 10
					file.Write(station.Pressure.ToString(PressFormat, InvC) + ' ');                // 11
					file.Write(station.CompassPoint(station.Bearing) + ' ');                       // 12
					file.Write(Beaufort(station.WindAverage) + ' ');                               // 13
					file.Write(Units.WindText + ' ');                                              // 14
					file.Write(Units.TempText[1].ToString() + ' ');                                // 15
					file.Write(Units.PressText + ' ');                                             // 16
					file.Write(Units.RainText + ' ');                                              // 17
					file.Write(station.WindRunToday.ToString(WindRunFormat, InvC) + ' ');          // 18
					if (station.presstrendval > 0)
						file.Write('+' + station.presstrendval.ToString(PressFormat, InvC) + ' '); // 19
					else
						file.Write(station.presstrendval.ToString(PressFormat, InvC) + ' ');
					file.Write(station.RainMonth.ToString(RainFormat, InvC) + ' ');                // 20
					file.Write(station.RainYear.ToString(RainFormat, InvC) + ' ');                 // 21
					file.Write(station.RainYesterday.ToString(RainFormat, InvC) + ' ');            // 22
					file.Write(station.IndoorTemperature.ToString(TempFormat, InvC) + ' ');        // 23
					file.Write(station.IndoorHumidity.ToString() + ' ');                           // 24
					file.Write(station.WindChill.ToString(TempFormat, InvC) + ' ');                // 25
					file.Write(station.temptrendval.ToString(TempTrendFormat, InvC) + ' ');        // 26
					file.Write(station.HiLoToday.HighTemp.ToString(TempFormat, InvC) + ' ');       // 27
					file.Write(station.HiLoToday.HighTempTime.ToString("HH:mm ") );                // 28
					file.Write(station.HiLoToday.LowTemp.ToString(TempFormat, InvC) + ' ');        // 29
					file.Write(station.HiLoToday.LowTempTime.ToString("HH:mm "));                  // 30
					file.Write(station.HiLoToday.HighWind.ToString(WindAvgFormat, InvC) + ' ');    // 31
					file.Write(station.HiLoToday.HighWindTime.ToString("HH:mm "));                 // 32
					file.Write(station.HiLoToday.HighGust.ToString(WindFormat, InvC) + ' ');       // 33
					file.Write(station.HiLoToday.HighGustTime.ToString("HH:mm "));                 // 34
					file.Write(station.HiLoToday.HighPress.ToString(PressFormat, InvC) + ' ');     // 35
					file.Write(station.HiLoToday.HighPressTime.ToString("HH:mm "));                // 36
					file.Write(station.HiLoToday.LowPress.ToString(PressFormat, InvC) + ' ');      // 37
					file.Write(station.HiLoToday.LowPressTime.ToString("HH:mm "));                 // 38
					file.Write(Version + ' ');                                                     // 39
					file.Write(Build + ' ');                                                       // 40
					file.Write(station.RecentMaxGust.ToString(WindFormat, InvC) + ' ');            // 41
					file.Write(station.HeatIndex.ToString(TempFormat, InvC) + ' ');                // 42
					file.Write(station.Humidex.ToString(TempFormat, InvC) + ' ');                  // 43
					file.Write(station.UV.ToString(UVFormat, InvC) + ' ');                         // 44
					file.Write(station.ET.ToString(ETFormat, InvC) + ' ');                         // 45
					file.Write((Convert.ToInt32(station.SolarRad)).ToString() + ' ');              // 46
					file.Write(station.AvgBearing.ToString() + ' ');                               // 47
					file.Write(station.RainLastHour.ToString(RainFormat, InvC) + ' ');             // 48
					file.Write(station.Forecastnumber.ToString() + ' ');                           // 49
					file.Write(IsDaylight() ? "1 " : "0 ");                                        // 50
					file.Write(station.SensorContactLost ? "1 " : "0 ");                           // 51
					file.Write(station.CompassPoint(station.AvgBearing) + ' ');                    // 52
					file.Write((Convert.ToInt32(station.CloudBase)).ToString() + ' ');             // 53
					file.Write(CloudBaseInFeet ? "ft " : "m ");                                    // 54
					file.Write(station.ApparentTemperature.ToString(TempFormat, InvC) + ' ');      // 55
					file.Write(station.SunshineHours.ToString(SunFormat, InvC) + ' ');             // 56
					file.Write(Convert.ToInt32(station.CurrentSolarMax).ToString() + ' ');         // 57
					file.Write(station.IsSunny ? "1 " : "0 ");                                     // 58
					file.WriteLine(station.FeelsLike.ToString(TempFormat, InvC));                  // 59

				file.Close();
				}
			}
			catch (Exception ex)
			{
				LogMessage("Error encountered during Realtime file update.");
				LogMessage(ex.Message);
			}


			if (RealtimeMySqlEnabled)
			{
				var InvC = new CultureInfo("");

				StringBuilder values = new StringBuilder(StartOfRealtimeInsertSQL, 1024);
				values.Append(" Values('");
				values.Append(timestamp.ToString("yy-MM-dd HH:mm:ss") + "',");
				values.Append(station.OutdoorTemperature.ToString(TempFormat, InvC) + ',');
				values.Append(station.OutdoorHumidity.ToString() + ',');
				values.Append(station.OutdoorDewpoint.ToString(TempFormat, InvC) + ',');
				values.Append(station.WindAverage.ToString(WindAvgFormat, InvC) + ',');
				values.Append(station.WindLatest.ToString(WindFormat, InvC) + ',');
				values.Append(station.Bearing.ToString() + ',');
				values.Append(station.RainRate.ToString(RainFormat, InvC) + ',');
				values.Append(station.RainToday.ToString(RainFormat, InvC) + ',');
				values.Append(station.Pressure.ToString(PressFormat, InvC) + ",'");
				values.Append(station.CompassPoint(station.Bearing) + "','");
				values.Append(Beaufort(station.WindAverage) + "','");
				values.Append(Units.WindText + "','");
				values.Append(Units.TempText[1].ToString() + "','");
				values.Append(Units.PressText + "','");
				values.Append(Units.RainText + "',");
				values.Append(station.WindRunToday.ToString(WindRunFormat, InvC) + ",'");
				values.Append((station.presstrendval > 0 ? '+' + station.presstrendval.ToString(PressFormat, InvC) : station.presstrendval.ToString(PressFormat, InvC)) + "',");
				values.Append(station.RainMonth.ToString(RainFormat, InvC) + ',');
				values.Append(station.RainYear.ToString(RainFormat, InvC) + ',');
				values.Append(station.RainYesterday.ToString(RainFormat, InvC) + ',');
				values.Append(station.IndoorTemperature.ToString(TempFormat, InvC) + ',');
				values.Append(station.IndoorHumidity.ToString() + ',');
				values.Append(station.WindChill.ToString(TempFormat, InvC) + ',');
				values.Append(station.temptrendval.ToString(TempTrendFormat, InvC) + ',');
				values.Append(station.HiLoToday.HighTemp.ToString(TempFormat, InvC) + ",'");
				values.Append(station.HiLoToday.HighTempTime.ToString("HH:mm") + "',");
				values.Append(station.HiLoToday.LowTemp.ToString(TempFormat, InvC) + ",'");
				values.Append(station.HiLoToday.LowTempTime.ToString("HH:mm") + "',");
				values.Append(station.HiLoToday.HighWind.ToString(WindAvgFormat, InvC) + ",'");
				values.Append(station.HiLoToday.HighWindTime.ToString("HH:mm") + "',");
				values.Append(station.HiLoToday.HighGust.ToString(WindFormat, InvC) + ",'");
				values.Append(station.HiLoToday.HighGustTime.ToString("HH:mm") + "',");
				values.Append(station.HiLoToday.HighPress.ToString(PressFormat, InvC) + ",'");
				values.Append(station.HiLoToday.HighPressTime.ToString("HH:mm") + "',");
				values.Append(station.HiLoToday.LowPress.ToString(PressFormat, InvC) + ",'");
				values.Append(station.HiLoToday.LowPressTime.ToString("HH:mm") + "','");
				values.Append(Version + "','");
				values.Append(Build + "',");
				values.Append(station.RecentMaxGust.ToString(WindFormat, InvC) + ',');
				values.Append(station.HeatIndex.ToString(TempFormat, InvC) + ',');
				values.Append(station.Humidex.ToString(TempFormat, InvC) + ',');
				values.Append(station.UV.ToString(UVFormat, InvC) + ',');
				values.Append(station.ET.ToString(ETFormat, InvC) + ',');
				values.Append(((int)station.SolarRad).ToString() + ',');
				values.Append(station.AvgBearing.ToString() + ',');
				values.Append(station.RainLastHour.ToString(RainFormat, InvC) + ',');
				values.Append(station.Forecastnumber.ToString() + ",'");
				values.Append((IsDaylight() ? "1" : "0") + "','");
				values.Append((station.SensorContactLost ? "1" : "0") + "','");
				values.Append(station.CompassPoint(station.AvgBearing) + "',");
				values.Append((station.CloudBase).ToString() + ",'");
				values.Append((CloudBaseInFeet ? "ft" : "m") + "',");
				values.Append(station.ApparentTemperature.ToString(TempFormat, InvC) + ',');
				values.Append(station.SunshineHours.ToString(SunFormat, InvC) + ',');
				values.Append(((int)Math.Round(station.CurrentSolarMax)).ToString() + ",'");
				values.Append((station.IsSunny ? "1" : "0") + "',");
				values.Append(station.FeelsLike.ToString(TempFormat, InvC));
				values.Append(")");

				string valuesString = values.ToString();
				List<string> cmds = new List<string>() { valuesString };

				if (!string.IsNullOrEmpty(MySqlRealtimeRetention))
				{
					cmds.Add($"DELETE IGNORE FROM {MySqlRealtimeTable} WHERE LogDateTime < DATE_SUB('{DateTime.Now:yyyy-MM-dd HH:mm:ss}', INTERVAL {MySqlRealtimeRetention});");
				}

				// do the update
				MySqlCommandSync(cmds, RealtimeSqlConn, $"Realtime[{cycle}]", true, true);
			}
		}

		private void ProcessTemplateFile(string template, string outputfile, TokenParser parser)
		{
			string templatefile = AppDir + template;
			if (File.Exists(templatefile))
			{
				var utf8WithoutBom = new UTF8Encoding(false);
				var encoding = UTF8encode ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
				parser.Encoding = encoding;
				parser.SourceFile = template;
				var output = parser.ToString();

				try
				{
					using (StreamWriter file = new StreamWriter(outputfile, false, encoding))
					{
						file.Write(output);
						file.Close();
					}
				}
				catch (Exception e)
				{
					LogMessage($"ProcessTemplateFile: Error writing to file '{outputfile}', error was - {e}");
				}
			}
		}

		public void StartTimersAndSensors()
		{
			LogMessage("Start Extra Sensors");
			airLinkOut?.Start();
			airLinkIn?.Start();

			LogMessage("Start Timers");
			// start the general one-minute timer
			LogMessage("Starting 1-minute timer");
			station.StartMinuteTimer();
			LogMessage($"Data logging interval = {DataLogInterval} ({logints[DataLogInterval]} mins)");


			if (RealtimeEnabled)
			{
				if (RealtimeFTPEnabled)
				{
					LogConsoleMessage("Connecting real time FTP");
					if (Sslftp == FtpProtocols.SFTP)
					{
						RealtimeSSHLogin(RealtimeCycleCounter++);
					}
					else
					{
						RealtimeFTPLogin(RealtimeCycleCounter++);
					}
				}

				LogMessage("Starting Realtime timer, interval = " + RealtimeInterval / 1000 + " seconds");
			}
			else
			{
				LogMessage("Realtime not enabled");
			}

			RealtimeTimer.Enabled = RealtimeEnabled;

			CustomMysqlSecondsTimer.Enabled = CustomMySqlSecondsEnabled;

			CustomHttpSecondsTimer.Enabled = CustomHttpSecondsEnabled;

			if (Wund.RapidFireEnabled)
			{
				WundTimer.Interval = 5000; // 5 seconds in rapid-fire mode
			}
			else
			{
				WundTimer.Interval = Wund.Interval * 60 * 1000; // mins to millisecs
			}


			AwekasTimer.Interval = AWEKAS.Interval * 1000;

			MQTTTimer.Interval = MQTT.IntervalTime * 1000; // secs to millisecs


			// 15/10/20 What is doing? Nothing
			/*
			if (AirLinkInEnabled || AirLinkOutEnabled)
			{
				AirLinkTimer.Interval = 60 * 1000; // 1 minute
				AirLinkTimer.Enabled = true;
				AirLinkTimer.Elapsed += AirLinkTimerTick;
			}
			*/

			if (MQTT.EnableInterval)
			{
				MQTTTimer.Enabled = true;
			}

			if (WundList == null)
			{
				// we've already been through here
				// do nothing
				LogDebugMessage("Wundlist is null");
			}
			else if (WundList.Count == 0)
			{
				// No archived entries to upload
				WundList = null;
				LogDebugMessage("Wundlist count is zero");
				WundTimer.Enabled = Wund.Enabled && !Wund.SynchronisedUpdate;
			}
			else
			{
				// start the archive upload thread
				Wund.CatchingUp = true;
				WundCatchup();
			}

			if (WindyList == null)
			{
				// we've already been through here
				// do nothing
				LogDebugMessage("Windylist is null");
			}
			else if (WindyList.Count == 0)
			{
				// No archived entries to upload
				WindyList = null;
				LogDebugMessage("Windylist count is zero");
			}
			else
			{
				// start the archive upload thread
				Windy.CatchingUp = true;
				WindyCatchUp();
			}

			if (PWSList == null)
			{
				// we've already been through here
				// do nothing
			}
			else if (PWSList.Count == 0)
			{
				// No archived entries to upload
				PWSList = null;
			}
			else
			{
				// start the archive upload thread
				PWS.CatchingUp = true;
				PWSCatchUp();
			}

			if (WOWList == null)
			{
				// we've already been through here
				// do nothing
			}
			else if (WOWList.Count == 0)
			{
				// No archived entries to upload
				WOWList = null;
			}
			else
			{
				// start the archive upload thread
				WOW.CatchingUp = true;
				WOWCatchUp();
			}

			if (OWMList == null)
			{
				// we've already been through here
				// do nothing
			}
			else if (OWMList.Count == 0)
			{
				// No archived entries to upload
				OWMList = null;
			}
			else
			{
				// start the archive upload thread
				OpenWeatherMap.CatchingUp = true;
				OpenWeatherMapCatchUp();
			}

			if (MySqlList == null)
			{
				// we've already been through here
				// do nothing
				LogDebugMessage("MySqlList is null");
			}
			else if (MySqlList.Count == 0)
			{
				// No archived entries to upload
				MySqlList = null;
				LogDebugMessage("MySqlList count is zero");
			}
			else
			{
				// start the archive upload thread
				LogMessage("Starting MySQL catchup thread");
				MySqlCatchupThread = new Thread(MySqlCatchup) {IsBackground = true};
				MySqlCatchupThread.Start();
			}

			WebTimer.Interval = UpdateInterval * 60 * 1000; // mins to millisecs
			WebTimer.Enabled = WebIntervalEnabled && !SynchronisedWebUpdate;

			AwekasTimer.Enabled = AWEKAS.Enabled && !AWEKAS.SynchronisedUpdate;

			EnableOpenWeatherMap();

			LogMessage("Normal running");
			LogConsoleMessage("Normal running");
		}

		private void CustomMysqlSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			_ = CustomMysqlSecondsWork();
		}

		private async Task CustomMysqlSecondsWork()
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if (!customMySqlSecondsUpdateInProgress)
			{
				customMySqlSecondsUpdateInProgress = true;

				customMysqlSecondsTokenParser.InputText = CustomMySqlSecondsCommandString;
				CustomMysqlSecondsCommand.CommandText = customMysqlSecondsTokenParser.ToStringFromString();

				await MySqlCommandAsync(CustomMysqlSecondsCommand.CommandText, CustomMysqlSecondsConn, "CustomSqlSecs", true, true);

				customMySqlSecondsUpdateInProgress = false;
			}
		}


		internal async Task CustomMysqlMinutesTimerTick()
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if (!customMySqlMinutesUpdateInProgress)
			{
				customMySqlMinutesUpdateInProgress = true;

				customMysqlMinutesTokenParser.InputText = CustomMySqlMinutesCommandString;
				CustomMysqlMinutesCommand.CommandText = customMysqlMinutesTokenParser.ToStringFromString();

				await MySqlCommandAsync(CustomMysqlMinutesCommand.CommandText, CustomMysqlMinutesConn, "CustomSqlMins", true, true);

				customMySqlMinutesUpdateInProgress = false;
			}
		}

		internal async Task CustomMysqlRolloverTimerTick()
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if (!customMySqlRolloverUpdateInProgress)
			{
				customMySqlRolloverUpdateInProgress = true;

				customMysqlRolloverTokenParser.InputText = CustomMySqlRolloverCommandString;
				CustomMysqlRolloverCommand.CommandText = customMysqlRolloverTokenParser.ToStringFromString();

				await MySqlCommandAsync(CustomMysqlRolloverCommand.CommandText, CustomMysqlRolloverConn, "CustomSqlRollover", true, true);

				customMySqlRolloverUpdateInProgress = false;
			}
		}

		public void DoExtraEndOfDayFiles()
		{
			int i;

			// handle any extra files that only require EOD processing
			for (i = 0; i < numextrafiles; i++)
			{
				if (ExtraFiles[i].endofday)
				{
					var uploadfile = ExtraFiles[i].local;
					var remotefile = ExtraFiles[i].remote;

					if ((uploadfile.Length > 0) && (remotefile.Length > 0))
					{
						// For EOD files, we want the previous days log files since it is now just past the day rollover time. Makes a difference on month rollover
						var logDay = DateTime.Now.AddDays(-1);

						if (uploadfile == "<currentlogfile>")
						{
							uploadfile = GetLogFileName(logDay);
						}
						else if (uploadfile == "<currentextralogfile>")
						{
							uploadfile = GetExtraLogFileName(logDay);
						}
						else if (uploadfile == "<airlinklogfile")
						{
							uploadfile = GetAirLinkLogFileName(logDay);
						}

						if (File.Exists(uploadfile))
						{
							if (remotefile.Contains("<currentlogfile>"))
							{
								remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(logDay)));
							}
							else if (remotefile.Contains("<currentextralogfile>"))
							{
								remotefile = remotefile.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(logDay)));
							}
							else if (remotefile.Contains("<airlinklogfile"))
							{
								remotefile = remotefile.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(logDay)));
							}

							if (ExtraFiles[i].process)
							{
								LogDebugMessage("EOD: Processing extra file " + uploadfile);
								// process the file
								var utf8WithoutBom = new UTF8Encoding(false);
								var encoding = UTF8encode ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
								tokenParser.Encoding = encoding;
								tokenParser.SourceFile = uploadfile;
								var output = tokenParser.ToString();
								uploadfile += "tmp";
								try
								{
									using (StreamWriter file = new StreamWriter(uploadfile, false, encoding))
									{
										file.Write(output);

										file.Close();
									}
								}
								catch (Exception ex)
								{
									LogDebugMessage("EOD: Error writing file " + uploadfile);
									LogDebugMessage(ex.Message);
								}
								//LogDebugMessage("Finished processing extra file " + uploadfile);
							}

							if (ExtraFiles[i].FTP)
							{
								// FTP the file at the next interval
								EODfilesNeedFTP = true;
							}
							else
							{
								// just copy the file
								LogDebugMessage($"EOD: Copying extra file {uploadfile} to {remotefile}");
								try
								{
									File.Copy(uploadfile, remotefile, true);
								}
								catch (Exception ex)
								{
									LogDebugMessage("EOD: Error copying extra file: " + ex.Message);
								}
								//LogDebugMessage("Finished copying extra file " + uploadfile);
							}
						}
					}
				}
			}
		}

		private void MySqlCatchup()
		{
			try
			{
				using (var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString()))
				{
					MySqlCommandSync(MySqlList, mySqlConn, "MySQL Archive", true, true);
				}
			}
			catch (Exception ex)
			{
				LogMessage("MySQL Archive: Error encountered during catchup MySQL operation.");
				LogMessage(ex.Message);
			}

			LogMessage("MySQL Archive: End of MySQL archive upload");
			MySqlList.Clear();
		}

		public void RealtimeFTPDisconnect()
		{
			try
			{
				if (Sslftp == FtpProtocols.SFTP && RealtimeSSH != null)
				{
					RealtimeSSH.Disconnect();
				}
				else if (RealtimeFTP != null)
				{
					RealtimeFTP.Disconnect();
				}
				LogDebugMessage("Disconnected Realtime FTP session");
			}
			catch { }
		}

		private void RealtimeFTPLogin(uint cycle)
		{
			//RealtimeTimer.Enabled = false;
			RealtimeFTP.Host = FtpHostname;
			RealtimeFTP.Port = FtpHostPort;
			RealtimeFTP.Credentials = new NetworkCredential(FtpUsername, FtpPassword);

			if (Sslftp == FtpProtocols.FTPS)
			{
				RealtimeFTP.EncryptionMode = DisableFtpsExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
				RealtimeFTP.DataConnectionEncryption = true;
				RealtimeFTP.ValidateAnyCertificate = true;
				// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specifiy protocols
				RealtimeFTP.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
				LogDebugMessage($"FTP[{cycle}]: Using FTPS protocol");
			}

			if (ActiveFTPMode)
			{
				RealtimeFTP.DataConnectionType = FtpDataConnectionType.PORT;
				LogDebugMessage($"FTP[{cycle}]: Using Active FTP mode");
			}
			else if (DisableFtpsEPSV)
			{
				RealtimeFTP.DataConnectionType = FtpDataConnectionType.PASV;
				LogDebugMessage($"FTP[{cycle}]: Disabling EPSV mode");
			}

			if (FtpHostname.Length > 0 && FtpHostname.Length > 0)
			{
				LogMessage($"FTP[{ cycle}]: Attempting realtime FTP connect to host {FtpHostname} on port {FtpHostPort}");
				try
				{
					RealtimeFTP.Connect();
					LogMessage($"FTP[{cycle}]: Realtime FTP connected");
					RealtimeFTP.SocketKeepAlive = true;
				}
				catch (Exception ex)
				{
					LogMessage($"FTP[{cycle}]: Error connecting ftp - " + ex.Message);
					RealtimeFTP.Disconnect();
				}

				RealtimeFTP.EnableThreadSafeDataConnections = false; // use same connection for all transfers
			}
			//RealtimeTimer.Enabled = true;
		}

		private void RealtimeSSHLogin(uint cycle)
		{
			if (FtpHostname != "" && FtpHostname != " ")
			{
				LogMessage($"SFTP[{cycle}]: Attempting realtime SFTP connect to host {FtpHostname} on port {FtpHostPort}");
				try
				{
					// BUILD 3092 - added alternate SFTP authentication options
					ConnectionInfo connectionInfo;
					PrivateKeyFile pskFile;
					if (SshftpAuthentication == "password")
					{
						connectionInfo = new ConnectionInfo(FtpHostname, FtpHostPort, FtpUsername, new PasswordAuthenticationMethod(FtpUsername, FtpPassword));
						LogDebugMessage($"SFTP[{cycle}]: Connecting using password authentication");
					}
					else if (SshftpAuthentication == "psk")
					{
						pskFile = new PrivateKeyFile(SshftpPskFile);
						connectionInfo = new ConnectionInfo(FtpHostname, FtpHostPort, FtpUsername, new PrivateKeyAuthenticationMethod(FtpUsername, pskFile));
						LogDebugMessage($"SFTP[{cycle}]: Connecting using PSK authentication");
					}
					else if (SshftpAuthentication == "password_psk")
					{
						pskFile = new PrivateKeyFile(SshftpPskFile);
						connectionInfo = new ConnectionInfo(FtpHostname, FtpHostPort, FtpUsername, new PasswordAuthenticationMethod(FtpUsername, FtpPassword), new PrivateKeyAuthenticationMethod(FtpUsername, pskFile));
						LogDebugMessage($"SFTP[{cycle}]: Connecting using password or PSK authentication");
					}
					else
					{
						LogMessage($"SFTP[{cycle}]: Invalid SshftpAuthentication specified [{SshftpAuthentication}]");
						return;
					}

					RealtimeSSH = new SftpClient(connectionInfo);

					//if (RealtimeSSH != null) RealtimeSSH.Dispose();
					//RealtimeSSH = new SftpClient(ftp_host, ftp_port, ftp_user, ftp_password);

					RealtimeSSH.Connect();
					RealtimeSSH.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);  // 15 seconds to match FTP default timeout
					LogMessage($"SFTP[{cycle}]: Realtime SFTP connected");
				}
				catch (Exception ex)
				{
					LogMessage($"SFTP[{cycle}]: Error connecting sftp - {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Process the list of WU updates created at startup from logger entries
		/// </summary>
		private async void WundCatchup()
		{
			Wund.Updating = true;
			for (int i = 0; i < WundList.Count; i++)
			{
				LogMessage("Uploading WU archive #" + (i + 1));
				try
				{
					HttpResponseMessage response = await WUhttpClient.GetAsync(WundList[i]);
					LogMessage("WU Response: " + response.StatusCode + ": " + response.ReasonPhrase);
				}
				catch (Exception ex)
				{
					LogMessage("WU update: " + ex.Message);
				}
			}

			LogMessage("End of WU archive upload");
			WundList.Clear();
			Wund.CatchingUp = false;
			WundTimer.Enabled = Wund.Enabled && !Wund.SynchronisedUpdate;
			Wund.Updating = false;
		}

		private async void WindyCatchUp()
		{
			Windy.Updating = true;
			for (int i = 0; i < WindyList.Count; i++)
			{
				LogMessage("Uploading Windy archive #" + (i + 1));
				try
				{
					HttpResponseMessage response = await WindyhttpClient.GetAsync(WindyList[i]);
					LogMessage("Windy Response: " + response.StatusCode + ": " + response.ReasonPhrase);
				}
				catch (Exception ex)
				{
					LogMessage("Windy update: " + ex.Message);
				}
			}

			LogMessage("End of Windy archive upload");
			WindyList.Clear();
			Windy.CatchingUp = false;
			Windy.Updating = false;
		}

		/// <summary>
		/// Process the list of PWS Weather updates created at startup from logger entries
		/// </summary>
		private async void PWSCatchUp()
		{
			PWS.Updating = true;

			for (int i = 0; i < PWSList.Count; i++)
			{
				LogMessage("Uploading PWS archive #" + (i + 1));
				try
				{
					HttpResponseMessage response = await PWShttpClient.GetAsync(PWSList[i]);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogMessage("PWS update: " + ex.Message);
				}
			}

			LogMessage("End of PWS archive upload");
			PWSList.Clear();
			PWS.CatchingUp = false;
			PWS.Updating = false;
		}

		/// <summary>
		/// Process the list of WOW updates created at startup from logger entries
		/// </summary>
		private async void WOWCatchUp()
		{
			WOW.Updating = true;

			for (int i = 0; i < WOWList.Count; i++)
			{
				LogMessage("Uploading WOW archive #" + (i + 1));
				try
				{
					HttpResponseMessage response = await PWShttpClient.GetAsync(WOWList[i]);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogMessage("WOW update: " + ex.Message);
				}
			}

			LogMessage("End of WOW archive upload");
			WOWList.Clear();
			WOW.CatchingUp = false;
			WOW.Updating = false;
		}

		/// <summary>
		/// Process the list of OpenWeatherMap updates created at startup from logger entries
		/// </summary>
		private async void OpenWeatherMapCatchUp()
		{
			OpenWeatherMap.Updating = true;

			string url = "http://api.openweathermap.org/data/3.0/measurements?appid=" + OpenWeatherMap.PW;
			string logUrl = url.Replace(OpenWeatherMap.PW, "<key>");

			using (var client = new HttpClient())
			{
				for (int i = 0; i < OWMList.Count; i++)
				{
					LogMessage("Uploading OpenWeatherMap archive #" + (i + 1));
					LogDebugMessage("OpenWeatherMap: URL = " + logUrl);
					LogDataMessage("OpenWeatherMap: Body = " + OWMList[i]);

					try
					{
						var data = new StringContent(OWMList[i], Encoding.UTF8, "application/json");
						HttpResponseMessage response = await client.PostAsync(url, data);
						var responseBodyAsText = await response.Content.ReadAsStringAsync();
						var status = response.StatusCode == HttpStatusCode.NoContent ? "OK" : "Error";  // Returns a 204 reponse for OK!
						LogDebugMessage($"OpenWeatherMap: Response code = {status} - {response.StatusCode}");
						if (response.StatusCode != HttpStatusCode.NoContent)
							LogDataMessage($"OpenWeatherMap: Response data = {responseBodyAsText}");
					}
					catch (Exception ex)
					{
						LogMessage("OpenWeatherMap: Update error = " + ex.Message);
					}
				}
			}

			LogMessage("End of OpenWeatherMap archive upload");
			OWMList.Clear();
			OpenWeatherMap.CatchingUp = false;
			OpenWeatherMap.Updating = false;
		}


		public async void UpdatePWSweather(DateTime timestamp)
		{
			if (!PWS.Updating)
			{
				PWS.Updating = true;

				string pwstring;
				string URL = station.GetPWSURL(out pwstring, timestamp);

				string starredpwstring = "&PASSWORD=" + new string('*', PWS.PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);
				LogDebugMessage(LogURL);

				try
				{
					HttpResponseMessage response = await PWShttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogDebugMessage("PWS update: " + ex.Message);
				}
				finally
				{
					PWS.Updating = false;
				}
			}
		}

		public async void UpdateWOW(DateTime timestamp)
		{
			if (!WOW.Updating)
			{
				WOW.Updating = true;

				string pwstring;
				string URL = station.GetWOWURL(out pwstring, timestamp);

				string starredpwstring = "&siteAuthenticationKey=" + new string('*', WOW.PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);
				LogDebugMessage(LogURL);

				try
				{
					HttpResponseMessage response = await WOWhttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogDebugMessage("WOW update: " + ex.Message);
				}
				finally
				{
					WOW.Updating = false;
				}
			}
		}

		public async Task MySqlCommandAsync(string Cmd, MySqlConnection Connection, string CallingFunction, bool OpenConnection, bool CloseConnection)
		{
			try
			{
				if (OpenConnection)
				{
					LogDebugMessage($"{CallingFunction}: Opening MySQL Connection");
					await Connection.OpenAsync();
				}

				using (MySqlCommand cmd = new MySqlCommand(Cmd, Connection))
				{
					LogDebugMessage($"{CallingFunction}: MySQL executing - {Cmd}");

					int aff = await cmd.ExecuteNonQueryAsync();
					LogDebugMessage($"{CallingFunction}: MySQL {aff} rows were affected.");
				}
			}
			catch (Exception ex)
			{
				LogMessage($"{CallingFunction}: Error encountered during MySQL operation.");
				LogMessage($"{CallingFunction}: SQL was - \"{Cmd}\"");
				LogMessage(ex.Message);
			}
			finally
			{
				if (CloseConnection)
				{
					try
					{
						Connection.Close();
					}
					catch { }
				}
			}

		}

		public Task MySqlCommandSync(List<string> Cmds, MySqlConnection Connection, string CallingFunction, bool OpenConnection, bool CloseConnection, bool ClearCommands=false)
		{
			return Task.Run(() =>
			{
				try
				{
					if (OpenConnection)
					{
						LogDebugMessage($"{CallingFunction}: Opening MySQL Connection");
						Connection.Open();
					}

					for (var i = 0; i < Cmds.Count; i++)
					{
						try
						{
							using (MySqlCommand cmd = new MySqlCommand(Cmds[i], Connection))
							{
								LogDebugMessage($"{CallingFunction}: MySQL executing[{i + 1}] - {Cmds[i]}");

								int aff = cmd.ExecuteNonQuery();
								LogDebugMessage($"{CallingFunction}: MySQL {aff} rows were affected.");
							}
						}
						catch (Exception ex)
						{
							LogMessage($"{CallingFunction}: Error encountered during MySQL operation.");
							LogMessage($"{CallingFunction}: SQL was - \"{Cmds[i]}\"");
							LogMessage(ex.Message);
						}
					}

					if (CloseConnection)
					{
						try
						{
							Connection.Close();
						}
						catch
						{ }
					}
				}
				catch (Exception e)
				{
					LogMessage($"{CallingFunction}: Error opening MySQL Connection");
					LogMessage(e.Message);
				}

				if (ClearCommands)
				{
					Cmds.Clear();
				}
			});
		}

		public async void GetLatestVersion()
		{
			var http = new HttpClient();
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			try
			{
				var retVal = await http.GetAsync("https://github.com/cumulusmx/CumulusMX/releases/latest");
				var latestUri = retVal.RequestMessage.RequestUri.AbsolutePath;
				LatestBuild = new string(latestUri.Split('/').Last().Where(char.IsDigit).ToArray());
				if (int.Parse(Build) < int.Parse(LatestBuild))
				{
					var msg = $"You are not running the latest version of Cumulus MX, build {LatestBuild} is available.";
					LogConsoleMessage(msg);
					LogMessage(msg);
					UpgradeAlarm.Triggered = true;
				}
				else if (int.Parse(Build) == int.Parse(LatestBuild))
				{
					LogMessage("This Cumulus MX instance is running the latest version");
					UpgradeAlarm.Triggered = false;
				}
				else
				{
					LogMessage($"Could not determine if you are running the latest Cumulus MX build or not. This build = {Build}, latest build = {LatestBuild}");
				}
			}
			catch (Exception ex)
			{
				LogMessage("Failed to get the latest build version from Github: " + ex.Message);
			}
		}

		public async void CustomHttpSecondsUpdate()
		{
			if (!updatingCustomHttpSeconds)
			{
				updatingCustomHttpSeconds = true;

				try
				{
					customHttpSecondsTokenParser.InputText = CustomHttpSecondsString;
					var processedString = customHttpSecondsTokenParser.ToStringFromString();
					LogDebugMessage("CustomHttpSeconds: Querying - " + processedString);
					var response = await customHttpSecondsClient.GetAsync(processedString);
					response.EnsureSuccessStatusCode();
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("CustomHttpSeconds: Response - " + response.StatusCode);
					LogDataMessage("CustomHttpSeconds: Response Text - " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogDebugMessage("CustomHttpSeconds: " + ex.Message);
				}
				finally
				{
					updatingCustomHttpSeconds = false;
				}
			}
		}

		public async void CustomHttpMinutesUpdate()
		{
			if (!updatingCustomHttpMinutes)
			{
				updatingCustomHttpMinutes = true;

				try
				{
					customHttpMinutesTokenParser.InputText = CustomHttpMinutesString;
					var processedString = customHttpMinutesTokenParser.ToStringFromString();
					LogDebugMessage("CustomHttpMinutes: Querying - " + processedString);
					var response = await customHttpMinutesClient.GetAsync(processedString);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("CustomHttpMinutes: Response code - " + response.StatusCode);
					LogDataMessage("CustomHttpMinutes: Response text - " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogDebugMessage("CustomHttpMinutes: " + ex.Message);
				}
				finally
				{
					updatingCustomHttpMinutes = false;
				}
			}
		}

		public async void CustomHttpRolloverUpdate()
		{
			if (!updatingCustomHttpRollover)
			{
				updatingCustomHttpRollover = true;

				try
				{
					customHttpRolloverTokenParser.InputText = CustomHttpRolloverString;
					var processedString = customHttpRolloverTokenParser.ToStringFromString();
					LogDebugMessage("CustomHttpRollover: Querying - " + processedString);
					var response = await customHttpRolloverClient.GetAsync(processedString);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("CustomHttpRollover: Response code - " + response.StatusCode);
					LogDataMessage("CustomHttpRollover: Response text - " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogDebugMessage("CustomHttpRollover: " + ex.Message);
				}
				finally
				{
					updatingCustomHttpRollover = false;
				}
			}
		}

		public void DegToDMS(double degrees, out int d, out int m, out int s)
		{
			int secs = (int)(degrees * 60 * 60);

			s = secs % 60;

			secs = (secs - s) / 60;

			m = secs % 60;
			d = secs / 60;
		}

		public void AddToWebServiceLists(DateTime timestamp)
		{
			AddToWundList(timestamp);
			AddToWindyList(timestamp);
			AddToPWSList(timestamp);
			AddToWOWList(timestamp);
			AddToOpenWeatherMapList(timestamp);
		}

		/// <summary>
		/// Add an archive entry to the WU 'catchup' list for sending to WU
		/// </summary>
		/// <param name="timestamp"></param>
		private void AddToWundList(DateTime timestamp)
		{
			if (Wund.Enabled && Wund.CatchUp)
			{
				string pwstring;
				string URL = station.GetWundergroundURL(out pwstring, timestamp, true);

				WundList.Add(URL);

				string starredpwstring = "&PASSWORD=" + new string('*', Wund.PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogMessage("Creating WU URL #" + WundList.Count);

				LogMessage(LogURL);
			}
		}

		private void AddToWindyList(DateTime timestamp)
		{
			if (Windy.Enabled && Windy.CatchUp)
			{
				string apistring;
				string URL = station.GetWindyURL(out apistring, timestamp);

				WindyList.Add(URL);

				string LogURL = URL.Replace(apistring, "<<API_KEY>>");

				LogMessage("Creating Windy URL #" + WindyList.Count);

				LogMessage(LogURL);
			}
		}

		private void AddToPWSList(DateTime timestamp)
		{
			if (PWS.Enabled && PWS.CatchUp)
			{
				string pwstring;
				string URL = station.GetPWSURL(out pwstring, timestamp);

				PWSList.Add(URL);

				string starredpwstring = "&PASSWORD=" + new string('*', PWS.PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogMessage("Creating PWS URL #" + PWSList.Count);

				LogMessage(LogURL);
			}
		}

		private void AddToWOWList(DateTime timestamp)
		{
			if (WOW.Enabled && WOW.CatchUp)
			{
				string pwstring;
				string URL = station.GetWOWURL(out pwstring, timestamp);

				WOWList.Add(URL);

				string starredpwstring = "&siteAuthenticationKey=" + new string('*', WOW.PW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogMessage("Creating WOW URL #" + WOWList.Count);

				LogMessage(LogURL);
			}
		}

		private void AddToOpenWeatherMapList(DateTime timestamp)
		{
			if (OpenWeatherMap.Enabled && OpenWeatherMap.CatchUp)
			{
				OWMList.Add(station.GetOpenWeatherMapData(timestamp));

				LogMessage("Creating OpenWeatherMap data #" + OWMList.Count);
			}
		}

		public void SetMonthlySqlCreateString()
		{
			StringBuilder strb = new StringBuilder("CREATE TABLE " + MySqlMonthlyTable + " (", 1500);
			strb.Append("LogDateTime DATETIME NOT NULL,");
			strb.Append("Temp decimal(4," + TempDPlaces + ") NOT NULL,");
			strb.Append("Humidity decimal(4," + HumDPlaces + ") NOT NULL,");
			strb.Append("Dewpoint decimal(4," + TempDPlaces + ") NOT NULL,");
			strb.Append("Windspeed decimal(4," + WindAvgDPlaces + ") NOT NULL,");
			strb.Append("Windgust decimal(4," + WindDPlaces + ") NOT NULL,");
			strb.Append("Windbearing VARCHAR(3) NOT NULL,");
			strb.Append("RainRate decimal(4," + RainDPlaces + ") NOT NULL,");
			strb.Append("TodayRainSoFar decimal(4," + RainDPlaces + ") NOT NULL,");
			strb.Append("Pressure decimal(6," + PressDPlaces + ") NOT NULL,");
			strb.Append("Raincounter decimal(6," + RainDPlaces + ") NOT NULL,");
			strb.Append("InsideTemp decimal(4," + TempDPlaces + ") NOT NULL,");
			strb.Append("InsideHumidity decimal(4," + HumDPlaces + ") NOT NULL,");
			strb.Append("LatestWindGust decimal(5," + WindDPlaces + ") NOT NULL,");
			strb.Append("WindChill decimal(4," + TempDPlaces + ") NOT NULL,");
			strb.Append("HeatIndex decimal(4," + TempDPlaces + ") NOT NULL,");
			strb.Append("UVindex decimal(4," + UVDPlaces + "),");
			strb.Append("SolarRad decimal(5,1),");
			strb.Append("Evapotrans decimal(4," + RainDPlaces + "),");
			strb.Append("AnnualEvapTran decimal(5," + RainDPlaces + "),");
			strb.Append("ApparentTemp decimal(4," + TempDPlaces + "),");
			strb.Append("MaxSolarRad decimal(5,1),");
			strb.Append("HrsSunShine decimal(3," + SunshineDPlaces + "),");
			strb.Append("CurrWindBearing varchar(3),");
			strb.Append("RG11rain decimal(4," + RainDPlaces + "),");
			strb.Append("RainSinceMidnight decimal(4," + RainDPlaces + "),");
			strb.Append("WindbearingSym varchar(3),");
			strb.Append("CurrWindBearingSym varchar(3),");
			strb.Append("FeelsLike decimal(4," + TempDPlaces + "),");
			strb.Append("Humidex decimal(4," + TempDPlaces + "),");
			strb.Append("PRIMARY KEY (LogDateTime)) COMMENT = \"Monthly logs from Cumulus\"");
			CreateMonthlySQL = strb.ToString();
		}

		internal void SetDayfileSqlCreateString()
		{
			StringBuilder strb = new StringBuilder("CREATE TABLE " + MySqlDayfileTable + " (", 2048);
			strb.Append("LogDate date NOT NULL ,");
			strb.Append("HighWindGust decimal(4," + WindDPlaces + ") NOT NULL,");
			strb.Append("HWindGBear varchar(3) NOT NULL,");
			strb.Append("THWindG varchar(5) NOT NULL,");
			strb.Append("MinTemp decimal(5," + TempDPlaces + ") NOT NULL,");
			strb.Append("TMinTemp varchar(5) NOT NULL,");
			strb.Append("MaxTemp decimal(5," + TempDPlaces + ") NOT NULL,");
			strb.Append("TMaxTemp varchar(5) NOT NULL,");
			strb.Append("MinPress decimal(6," + PressDPlaces + ") NOT NULL,");
			strb.Append("TMinPress varchar(5) NOT NULL,");
			strb.Append("MaxPress decimal(6," + PressDPlaces + ") NOT NULL,");
			strb.Append("TMaxPress varchar(5) NOT NULL,");
			strb.Append("MaxRainRate decimal(4," + RainDPlaces + ") NOT NULL,");
			strb.Append("TMaxRR varchar(5) NOT NULL,TotRainFall decimal(6," + RainDPlaces + ") NOT NULL,");
			strb.Append("AvgTemp decimal(4," + TempDPlaces + ") NOT NULL,");
			strb.Append("TotWindRun decimal(5," + WindRunDPlaces +") NOT NULL,");
			strb.Append("HighAvgWSpeed decimal(3," + WindAvgDPlaces + "),");
			strb.Append("THAvgWSpeed varchar(5),LowHum decimal(4," + HumDPlaces + "),");
			strb.Append("TLowHum varchar(5),");
			strb.Append("HighHum decimal(4," + HumDPlaces + "),");
			strb.Append("THighHum varchar(5),TotalEvap decimal(5," + RainDPlaces + "),");
			strb.Append("HoursSun decimal(3," + SunshineDPlaces + "),");
			strb.Append("HighHeatInd decimal(4," + TempDPlaces + "),");
			strb.Append("THighHeatInd varchar(5),");
			strb.Append("HighAppTemp decimal(4," + TempDPlaces + "),");
			strb.Append("THighAppTemp varchar(5),");
			strb.Append("LowAppTemp decimal(4," + TempDPlaces + "),");
			strb.Append("TLowAppTemp varchar(5),");
			strb.Append("HighHourRain decimal(4," + RainDPlaces + "),");
			strb.Append("THighHourRain varchar(5),");
			strb.Append("LowWindChill decimal(4," + TempDPlaces + "),");
			strb.Append("TLowWindChill varchar(5),");
			strb.Append("HighDewPoint decimal(4," + TempDPlaces + "),");
			strb.Append("THighDewPoint varchar(5),");
			strb.Append("LowDewPoint decimal(4," + TempDPlaces + "),");
			strb.Append("TLowDewPoint varchar(5),");
			strb.Append("DomWindDir varchar(3),");
			strb.Append("HeatDegDays decimal(4,1),");
			strb.Append("CoolDegDays decimal(4,1),");
			strb.Append("HighSolarRad decimal(5,1),");
			strb.Append("THighSolarRad varchar(5),");
			strb.Append("HighUV decimal(3," + UVDPlaces + "),");
			strb.Append("THighUV varchar(5),");
			strb.Append("HWindGBearSym varchar(3),");
			strb.Append("DomWindDirSym varchar(3),");
			strb.Append("MaxFeelsLike decimal(5," + TempDPlaces + "),");
			strb.Append("TMaxFeelsLike varchar(5),");
			strb.Append("MinFeelsLike decimal(5," + TempDPlaces + "),");
			strb.Append("TMinFeelsLike varchar(5),");
			strb.Append("MaxHumidex decimal(5," + TempDPlaces + "),");
			strb.Append("TMaxHumidex varchar(5),");
			//strb.Append("MinHumidex decimal(5," + TempDPlaces + "),");
			//strb.Append("TMinHumidex varchar(5),");
			strb.Append("PRIMARY KEY(LogDate)) COMMENT = \"Dayfile from Cumulus\"");
			CreateDayfileSQL = strb.ToString();
		}

		public void LogOffsetsMultipliers()
		{
			LogMessage("Offsets and Multipliers:");
			LogMessage($"PO={Calib.Press.Offset:F3} TO={Calib.Temp.Offset:F3} HO={Calib.Hum.Offset} WDO={Calib.WindDir.Offset} ITO={Calib.InTemp.Offset:F3} SO={Calib.Solar.Offset:F3} UVO={Calib.UV.Offset:F3}");
			LogMessage($"PM={Calib.Press.Mult:F3} WSM={Calib.WindSpeed.Mult:F3} WGM={Calib.WindGust.Mult:F3} TM={Calib.Temp.Mult:F3} TM2={Calib.Temp.Mult2:F3} " +
						$"HM={Calib.Hum.Mult:F3} HM2={Calib.Hum.Mult2:F3} RM={Calib.Rain.Mult:F3} SM={Calib.Solar.Mult:F3} UVM={Calib.UV.Mult:F3}");
			LogMessage("Spike removal:");
			LogMessage($"TD={Spike.TempDiff:F3} GD={Spike.GustDiff:F3} WD={Spike.WindDiff:F3} HD={Spike.HumidityDiff:F3} PD={Spike.PressDiff:F3} MR={Spike.MaxRainRate:F3} MH={Spike.MaxHourlyRain:F3}");
			LogMessage("Limits:");
			LogMessage($"TH={Limit.TempHigh.ToString(TempFormat)} TL={Limit.TempLow.ToString(TempFormat)} DH={Limit.DewHigh.ToString(TempFormat)} PH={Limit.PressHigh.ToString(PressFormat)} PL={Limit.PressLow.ToString(PressFormat)} GH={Limit.WindHigh:F3}");

		}

		private void LogPrimaryAqSensor()
		{
			switch (StationOptions.PrimaryAqSensor)
			{
				case (int)PrimaryAqSensor.Undefined:
					LogMessage("Primary AQ Sensor = Undefined");
					break;
				case (int)PrimaryAqSensor.Ecowitt1:
				case (int)PrimaryAqSensor.Ecowitt2:
				case (int)PrimaryAqSensor.Ecowitt3:
				case (int)PrimaryAqSensor.Ecowitt4:
					LogMessage("Primary AQ Sensor = Ecowitt" + StationOptions.PrimaryAqSensor);
					break;
				case (int)PrimaryAqSensor.EcowittCO2:
					LogMessage("Primary AQ Sensor = Ecowitt CO2");
					break;
				case (int)PrimaryAqSensor.AirLinkIndoor:
					LogMessage("Primary AQ Sensor = Airlink Indoor");
					break;
				case (int)PrimaryAqSensor.AirLinkOutdoor:
					LogMessage("Primary AQ Sensor = Airlink Outdoor");
					break;
			}
		}
	}

	/*
	internal class Raintotaldata
	{
		public DateTime timestamp;
		public double raintotal;

		public Raintotaldata(DateTime ts, double rain)
		{
			timestamp = ts;
			raintotal = rain;
		}
	}
	*/

	public static class StationTypes
	{
		public const int Undefined = -1;
		public const int VantagePro = 0;
		public const int VantagePro2 = 1;
		public const int WMR928 = 2;
		public const int WM918 = 3;
		public const int EasyWeather = 4;
		public const int FineOffset = 5;
		public const int WS2300 = 6;
		public const int FineOffsetSolar = 7;
		public const int WMR100 = 8;
		public const int WMR200 = 9;
		public const int Instromet = 10;
		public const int WLL = 11;
		public const int GW1000 = 12;
	}

	/*
	public static class AirQualityIndex
	{
		public const int US_EPA = 0;
		public const int UK_COMEAP = 1;
		public const int EU_AQI = 2;
		public const int CANADA_AQHI = 3;
		public const int EU_CAQI = 4;
	}
	*/

	/*
	public static class DoubleExtensions
	{
		public static string ToUKString(this double value)
		{
			return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
		}
	}
	*/

	public class DiaryData
	{
		[PrimaryKey]
		public DateTime Timestamp { get; set; }
		public string entry { get; set; }
		public int snowFalling { get; set; }
		public int snowLying { get; set; }
		public double snowDepth { get; set; }
	}

	public class ProgramOptionsClass
	{
		public bool EnableAccessibility { get; set; }
		public string StartupPingHost { get; set; }
		public int StartupPingEscapeTime { get; set; }
		public int StartupDelaySecs { get; set; }
		public int StartupDelayMaxUptime { get; set; }
		public bool DebugLogging { get; set; }
		public bool DataLogging { get; set; }
		public bool WarnMultiple { get; set; }
	}

	public class StationUnits
	{
		public int Wind { get; set; }
		public int Press { get; set; }
		public int Rain { get; set; }
		public int Temp { get; set; }

		public string WindText { get; set; }
		public string PressText { get; set; }
		public string RainText { get; set; }
		public string TempText { get; set; }

		public string TempTrendText { get; set; }
		public string RainTrendText { get; set; }
		public string PressTrendText { get; set; }
		public string WindRunText { get; set; }
		public string AirQualityUnitText { get; set; }
		public string SoilMoistureUnitText { get; set; }
		public string CO2UnitText { get; set; }

		public StationUnits()
		{
			AirQualityUnitText = "µg/m³";
			SoilMoistureUnitText = "cb";
			CO2UnitText = "ppm";
		}
	}

	public class StationOptions
	{
		public bool UseZeroBearing { get; set; }
		public bool UseWind10MinAve { get; set; }
		public bool UseSpeedForAvgCalc { get; set; }
		public bool Humidity98Fix { get; set; }
		public bool CalculatedDP { get; set; }
		public bool CalculatedWC { get; set; }
		public bool SyncTime { get; set; }
		public int ClockSettingHour { get; set; }
		public bool UseCumulusPresstrendstr { get; set; }
		public bool LogExtraSensors { get; set; }
		public bool WS2300IgnoreStationClock { get; set; }
		public bool RoundWindSpeed { get; set; }
		public int PrimaryAqSensor { get; set; }
		public bool NoSensorCheck { get; set; }
		public int AvgBearingMinutes { get; set; }
		public int AvgSpeedMinutes { get; set; }
		public int PeakGustMinutes { get; set; }
	}

	public class FileGenerationFtpOptions
	{
		public string TemplateFileName { get; set; }
		public string LocalFileName { get; set; }
		public string LocalPath { get; set; }
		public string RemoteFileName { get; set; }
		public bool Create { get; set; }
		public bool FTP { get; set; }
		public bool FtpRequired { get; set; }
		public bool CreateRequired { get; set; }
		public FileGenerationFtpOptions()
		{
			CreateRequired = true;
			FtpRequired = true;
		}
	}

	public class DavisOptions
	{
		public bool ForceVPBarUpdate { get; set; }
		public bool ReadReceptionStats { get; set; }
		public bool SetLoggerInterval { get; set; }
		public bool UseLoop2 { get; set; }
		public int InitWaitTime { get; set; }
		public int IPResponseTime { get; set; }
		public int ReadTimeout { get; set; }
		public bool IncrementPressureDP { get; set; }
		public int BaudRate { get; set; }
		public int RainGaugeType { get; set; }
		public int ConnectionType { get; set; }
		public int TCPPort { get; set; }
		public string IPAddr { get; set; }
		public int PeriodicDisconnectInterval { get; set; }
	}

	public class FineOffsetOptions
	{
		public bool SyncReads { get; set; }
		public int ReadAvoidPeriod { get; set; }
		public int ReadTime { get; set; }
		public bool SetLoggerInterval { get; set; }
		public int VendorID { get; set; }
		public int ProductID { get; set; }
}

	public class ImetOptions
	{
		public List<int> BaudRates { get; set; }
		public int BaudRate { get; set; }
		public int WaitTime { get; set; }
		public int ReadDelay { get; set; }
		public bool UpdateLogPointer { get; set; }
	}

	public class EasyWeatherOptions
	{
		public double Interval { get; set; }
		public string Filename { get; set; }
		public int MinPressMB { get; set; }
		public int MaxPressMB { get; set; }
		public int MaxRainTipDiff { get; set; }
		public double PressOffset { get; set; }
	}

	public class GraphOptions
	{
		public bool TempVisible { get; set; }
		public bool InTempVisible { get; set; }
		public bool HIVisible { get; set; }
		public bool DPVisible { get; set; }
		public bool WCVisible { get; set; }
		public bool AppTempVisible { get; set; }
		public bool FeelsLikeVisible { get; set; }
		public bool HumidexVisible { get; set; }
		public bool InHumVisible { get; set; }
		public bool OutHumVisible { get; set; }
		public bool UVVisible { get; set; }
		public bool SolarVisible { get; set; }
		public bool SunshineVisible { get; set; }
		public bool DailyMaxTempVisible { get; set; }
		public bool DailyAvgTempVisible { get; set; }
		public bool DailyMinTempVisible { get; set; }
		public bool GrowingDegreeDaysVisible1 { get; set; }
		public bool GrowingDegreeDaysVisible2 { get; set; }
		public bool TempSumVisible0 { get; set; }
		public bool TempSumVisible1 { get; set; }
		public bool TempSumVisible2 { get; set; }
	}

	public class SelectaChartOptions
	{
		public string[] series { get; set; }
		public string[] colours { get; set; }

		public SelectaChartOptions()
		{
			series = new string[6];
			colours = new string[6];
		}
	}

	public class AwekasResponse
	{
		public int status { get; set; }
		public int authentication { get; set; }
		public int minuploadtime { get; set; }
		public AwekasErrors error { get; set; }
		public AwekasDisabled disabled { get; set; }
	}

	public class AwekasErrors
	{
		public int count { get; set; }
		public int time { get; set; }
		public int date { get; set; }
		public int temp { get; set; }
		public int hum { get; set; }
		public int airp { get; set; }
		public int rain { get; set; }
		public int rainrate { get; set; }
		public int wind { get; set; }
		public int gust { get; set; }
		public int snow { get; set; }
		public int solar { get; set; }
		public int uv { get; set; }
		public int brightness { get; set; }
		public int suntime { get; set; }
		public int indoortemp { get; set; }
		public int indoorhumidity { get; set; }
		public int soilmoisture1 { get; set; }
		public int soilmoisture2 { get; set; }
		public int soilmoisture3 { get; set; }
		public int soilmoisture4 { get; set; }
		public int soiltemp1 { get; set; }
		public int soiltemp2 { get; set; }
		public int soiltemp3 { get; set; }
		public int soiltemp4 { get; set; }
		public int leafwetness1 { get; set; }
		public int leafwetness2 { get; set; }
		public int warning { get; set; }
	}

	public class AwekasDisabled
	{
		public int temp { get; set; }
		public int hum { get; set; }
		public int airp { get; set; }
		public int rain { get; set; }
		public int rainrate { get; set; }
		public int wind { get; set; }
		public int snow { get; set; }
		public int solar { get; set; }
		public int uv { get; set; }
		public int indoortemp { get; set; }
		public int indoorhumidity { get; set; }
		public int soilmoisture1 { get; set; }
		public int soilmoisture2 { get; set; }
		public int soilmoisture3 { get; set; }
		public int soilmoisture4 { get; set; }
		public int soiltemp1 { get; set; }
		public int soiltemp2 { get; set; }
		public int soiltemp3 { get; set; }
		public int soiltemp4 { get; set; }
		public int leafwetness1 { get; set; }
		public int leafwetness2 { get; set; }
		public int report { get; set; }
	}

	public class OpenWeatherMapStation
	{
		public string id { get; set; }
		public string created_at { get; set; }
		public string updated_at { get; set; }
		public string external_id { get; set; }
		public string name { get; set; }
		public double longitude { get; set; }
		public double latitude { get; set; }
		public int altitude { get; set; }
		public int rank { get; set; }
	}

	public class OpenWeatherMapNewStation
	{
		public string ID { get; set; }
		public string created_at { get; set; }
		public string updated_at { get; set; }
		public string user_id { get; set; }
		public string external_id { get; set; }
		public string name { get; set; }
		public double longitude { get; set; }
		public double latitude { get; set; }
		public int altitude { get; set; }
		public int source_type { get; set; }
	}

	public class Alarm
	{
		public Cumulus cumulus { get;  set; }

		public bool Enabled { get; set; }
		public double Value { get; set; }
		public bool Sound { get; set; }
		public string SoundFile { get; set; }

		bool triggered;
		public bool Triggered
		{
			get => triggered;
			set
			{
				if (value)
				{
					// If we were not set before, so we need to send an email?
					if (!triggered && Enabled && Email && cumulus.SmtpOptions.Enabled)
					{
						// Construct the message - preamble, plus values
						var msg = cumulus.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsg, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					// If we get a new trigger, record the time
					triggered = true;
					TriggeredTime = DateTime.Now;
				}
				else
				{
					// If the trigger is cleared, check if we should be latching the value
					if (Latch)
					{
						if (DateTime.Now > TriggeredTime.AddHours(LatchHours))
						{
							// We are latching, but the latch period has expired, clear the trigger
							triggered = false;
						}
					}
					else
					{
						// No latch, just clear the trigger
						triggered = false;
					}
				}
			}
		}
		public DateTime TriggeredTime { get; set; }
		public bool Notify { get; set; }
		public bool Email { get; set; }
		public bool Latch { get; set; }
		public int LatchHours { get; set; }
		public string EmailMsg { get; set; }
		public string Units { get; set; }
	}

	public class AlarmChange : Alarm
	{
		//public bool changeUp { get; set; }
		//public bool changeDown { get; set; }

		bool upTriggered;
		public bool UpTriggered
		{
			get => upTriggered;
			set
			{
				if (value)
				{
					// If we were not set before, so we need to send an email?
					if (!upTriggered && Enabled && Email && cumulus.SmtpOptions.Enabled)
					{
						// Construct the message - preamble, plus values
						var msg = Program.cumulus.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsgUp, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					// If we get a new trigger, record the time
					upTriggered = true;
					UpTriggeredTime = DateTime.Now;
				}
				else
				{
					// If the trigger is cleared, check if we should be latching the value
					if (Latch)
					{
						if (DateTime.Now > UpTriggeredTime.AddHours(LatchHours))
						{
							// We are latching, but the latch period has expired, clear the trigger
							upTriggered = false;
						}
					}
					else
					{
						// No latch, just clear the trigger
						upTriggered = false;
					}
				}
			}
		}
		public DateTime UpTriggeredTime { get; set; }


		bool downTriggered;
		public bool DownTriggered
		{
			get => downTriggered;
			set
			{
				if (value)
				{
					// If we were not set before, so we need to send an email?
					if (!downTriggered && Enabled && Email && cumulus.SmtpOptions.Enabled)
					{
						// Construct the message - preamble, plus values
						var msg = Program.cumulus.AlarmEmailPreamble + "\n" + string.Format(EmailMsgDn, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					// If we get a new trigger, record the time
					downTriggered = true;
					DownTriggeredTime = DateTime.Now;
				}
				else
				{
					// If the trigger is cleared, check if we should be latching the value
					if (Latch)
					{
						if (DateTime.Now > DownTriggeredTime.AddHours(LatchHours))
						{
							// We are latching, but the latch period has expired, clear the trigger
							downTriggered = false;
						}
					}
					else
					{
						// No latch, just clear the trigger
						downTriggered = false;
					}
				}
			}
		}

		public DateTime DownTriggeredTime { get; set; }

		public string EmailMsgUp { get; set; }
		public string EmailMsgDn { get; set; }

	}

	public class WebUploadService
	{
		public string Server;
		public int Port;
		public string ID;
		public string PW;
		public bool Enabled;
		public int Interval;
		public int DefaultInterval;
		public bool SynchronisedUpdate;
		public bool SendUV;
		public bool SendSolar;
		public bool SendIndoor;
		public bool SendAirQuality;
		public bool CatchUp;
		public bool CatchingUp;
		public bool Updating;
	}

	public class WebUploadTwitter : WebUploadService
	{
		public string OauthToken;
		public string OauthTokenSecret;
		public bool SendLocation;
	}

	public class WebUploadWund : WebUploadService
	{
		public bool RapidFireEnabled;
		public bool SendAverage;
		public bool SendSoilTemp1;
		public bool SendSoilTemp2;
		public bool SendSoilTemp3;
		public bool SendSoilTemp4;
		public bool SendSoilMoisture1;
		public bool SendSoilMoisture2;
		public bool SendSoilMoisture3;
		public bool SendSoilMoisture4;
		public bool SendLeafWetness1;
		public bool SendLeafWetness2;
	}

	public class WebUploadWindy : WebUploadService
	{
		public string ApiKey;
		public int StationIdx;
	}

	public class WebUploadWindGuru : WebUploadService
	{
		public bool SendRain;
	}

	public class WebUploadAwekas : WebUploadService
	{
		public bool RateLimited;
		public int OriginalInterval;
		public string Lang;
		public bool SendSoilTemp;
		public bool SendSoilMoisture;
		public bool SendLeafWetness;
	}

	public class WebUploadWCloud : WebUploadService
	{
		public bool SendSoilMoisture;
		public int SoilMoistureSensor;
		public bool SendLeafWetness;
		public int LeafWetnessSensor;
	}

	public class WebUploadAprs : WebUploadService
	{
		public bool HumidityCutoff;
	}

	public class DisplayOptions
	{
		public bool UseApparent { get; set; }
		public bool ShowSolar { get; set; }
		public bool ShowUV { get; set; }
	}

	public class AlarmEmails
	{
		public string Preamble { get; set; }
		public string HighGust { get; set; }
		public string HighWind { get; set; }
		public string HighTemp { get; set; }
		public string LowTemp { get; set; }
		public string TempDown { get; set; }
		public string TempUp { get; set; }
		public string HighPress { get; set; }
		public string LowPress { get; set; }
		public string PressDown { get; set; }
		public string PressUp { get; set; }
		public string Rain { get; set; }
		public string RainRate { get; set; }
		public string SensorLost { get; set; }
		public string DataStopped { get; set; }
		public string BatteryLow { get; set; }
		public string DataSpike { get; set; }
		public string Upgrade { get; set; }
	}
}
