using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Files;
using EmbedIO.Utilities;
using FluentFTP;
using FluentFTP.Helpers;
using MySqlConnector;
using Renci.SshNet;
using SQLite;
using ServiceStack;
using ServiceStack.Text;
using Swan;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;
using System.Web;
using System.Web.Caching;
using System.Web.UI.WebControls;
using System.Xml.Linq;

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
			SFTP = 2,
			PHP = 3
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

		private FileStream _lockFile;
		private static string _lockFilename;

		public const double DefaultHiVal = -9999;
		public const double DefaultLoVal = 9999;

		public const int DayfileFields = 55;

		public const int LogFileRetries = 3;

		private readonly WeatherStation station;

		internal DavisAirLink airLinkIn;
		public int airLinkInLsid;
		public string AirLinkInHostName;
		internal DavisAirLink airLinkOut;
		public int airLinkOutLsid;
		public string AirLinkOutHostName;

		internal HttpStationEcowitt ecowittExtra;
		internal HttpStationAmbient ambientExtra;

		public DateTime LastUpdateTime;

		public PerformanceCounter UpTime;

		private readonly WebTags webtags;

		internal Lang Trans = new Lang();

		public bool NormalRunning = false;

		private TraceListener FtpTraceListener;

		public volatile int WebUpdating;
		public volatile bool SqlCatchingUp;

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

		internal int LeafWetDPlaces = 0;
		public string LeafWetFormat = "F0";

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

		public decimal Latitude;
		public decimal Longitude;
		public double Altitude;

		internal int wsPort;
		private readonly bool DebuggingEnabled;

		public SerialPort cmprtRG11;
		public SerialPort cmprt2RG11;

		private const int DefaultWebUpdateInterval = 15;

		public int RecordSetTimeoutHrs = 24;

		private const int VP2SERIALCONNECTION = 0;
		//private const int VP2USBCONNECTION = 1;
		//private const int VP2TCPIPCONNECTION = 2;

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

		// Use thread safe queues for the MySQL command lists
		private ConcurrentQueue<SqlCache> MySqlList = new ConcurrentQueue<SqlCache>();
		public ConcurrentQueue<SqlCache> MySqlFailedList = new ConcurrentQueue<SqlCache>();

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
		public FtpOptionsClass FtpOptions = new FtpOptionsClass();

		public StationUnits Units = new StationUnits();

		public DavisOptions DavisOptions = new DavisOptions();
		public FineOffsetOptions FineOffsetOptions = new FineOffsetOptions();
		public ImetOptions ImetOptions = new ImetOptions();
		public EasyWeatherOptions EwOptions = new EasyWeatherOptions();
		public WeatherFlowOptions WeatherFlowOptions = new WeatherFlowOptions();

		public GraphOptions GraphOptions = new GraphOptions();

		public SelectaChartOptions SelectaChartOptions = new SelectaChartOptions();

		public DisplayOptions DisplayOptions = new DisplayOptions();

		public EmailSender emailer;
		public EmailSender.SmtpOptions SmtpOptions = new EmailSender.SmtpOptions();

		public SolarOptions SolarOptions = new SolarOptions();

		public string AlarmFromEmail;
		public string[] AlarmDestEmail;
		public bool AlarmEmailHtml;

		public bool RealtimeIntervalEnabled; // The timer is to be started
		private int realtimeFTPRetries; // Count of failed realtime FTP attempts

		// Wunderground settings
		public WebUploadWund Wund = new WebUploadWund();

		// Windy.com settings
		public WebUploadWindy Windy = new WebUploadWindy();

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
			public string UpdateTemplate;
			public bool EnableInterval;
			public int IntervalTime;
			public string IntervalTemplate;
		}
		public MqttSettings MQTT;

		// NOAA report settings
		public NOAAconfig NOAAconf = new NOAAconfig();

		// Growing Degree Days
		public double GrowingBase1;
		public double GrowingBase2;
		public int GrowingYearStarts;
		public bool GrowingCap30C;

		public int TempSumYearStarts;
		public double TempSumBase1;
		public double TempSumBase2;

		public bool EODfilesNeedFTP;

		public bool IsOSX = false;
		public double CPUtemp = -999;

		// Alarms
		public Alarm DataStoppedAlarm = new Alarm("Data Stopped", AlarmTypes.Trigger);
		public Alarm BatteryLowAlarm = new Alarm("Battery Low", AlarmTypes.Trigger);
		public Alarm SensorAlarm = new Alarm("Sensor Data Stopped", AlarmTypes.Trigger);
		public Alarm SpikeAlarm = new Alarm("Data Spike", AlarmTypes.Trigger);
		public Alarm HighWindAlarm = new Alarm("High Wind", AlarmTypes.Above);
		public Alarm HighGustAlarm = new Alarm("High Gust", AlarmTypes.Above);
		public Alarm HighRainRateAlarm = new Alarm("High Rainfall Rate", AlarmTypes.Above);
		public Alarm HighRainTodayAlarm = new Alarm("Total Rainfall Today", AlarmTypes.Above);
		public AlarmChange PressChangeAlarm = new AlarmChange("Pressure Change");
		public Alarm HighPressAlarm = new Alarm("High Pressure", AlarmTypes.Above);
		public Alarm LowPressAlarm = new Alarm("Low Pressure", AlarmTypes.Below);
		public AlarmChange TempChangeAlarm = new AlarmChange("Temperature Change");
		public Alarm HighTempAlarm = new Alarm("High Temperature", AlarmTypes.Above);
		public Alarm LowTempAlarm = new Alarm("Low Temperature", AlarmTypes.Below);
		public Alarm UpgradeAlarm = new Alarm("Upgrade Available", AlarmTypes.Trigger);
		public Alarm HttpUploadAlarm = new Alarm("HTTP Uploads", AlarmTypes.Trigger);
		public Alarm MySqlUploadAlarm = new Alarm("MySQL Uploads", AlarmTypes.Trigger);
		public Alarm IsRainingAlarm = new Alarm("IsRaining", AlarmTypes.Trigger);


		private const double DEFAULTFCLOWPRESS = 950.0;
		private const double DEFAULTFCHIGHPRESS = 1050.0;

		private const string ForumDefault = "https://cumulus.hosiene.co.uk/";

		private const string WebcamDefault = "";

		private const string DefaultSoundFile = "alarm.mp3";
		private const string DefaultSoundFileOld = "alert.wav";

		public int RecentDataDays = 7;

		public int RealtimeInterval;

		public WebServer httpServer;

		//public WebServer httpServer;
		public MxWebSocket WebSock;

		public static readonly HttpClient MyHttpClient = new HttpClient();
		public static readonly HttpClientHandler MyHttpHandler = new HttpClientHandler();

		// Custom HTTP - seconds
		private bool updatingCustomHttpSeconds;
		internal Timer CustomHttpSecondsTimer;
		internal bool CustomHttpSecondsEnabled;
		internal string[] CustomHttpSecondsStrings = new string[10];
		internal int CustomHttpSecondsInterval;

		// Custom HTTP - minutes
		private bool updatingCustomHttpMinutes;
		internal bool CustomHttpMinutesEnabled;
		internal string[] CustomHttpMinutesStrings = new string[10];
		internal int CustomHttpMinutesInterval;
		internal int CustomHttpMinutesIntervalIndex;

		// Custom HTTP - roll-over
		private bool updatingCustomHttpRollover;
		internal bool CustomHttpRolloverEnabled;
		internal string[] CustomHttpRolloverStrings = new string[10];

		// PHP upload HTTP
		internal HttpClientHandler phpUploadHttpHandler;
		internal HttpClient phpUploadHttpClient;

		public Thread ftpThread;
		public Thread MySqlCatchupThread;

		public string xapHeartbeat;
		public string xapsource;

		public string LatestBuild = "n/a";

		public MySqlConnectionStringBuilder MySqlConnSettings = new MySqlConnectionStringBuilder();

		public MySqlGeneralSettings MySqlSettings = new MySqlGeneralSettings();

		public DateTime MySqlLastRealtimeTime;
		public DateTime MySqlILastntervalTime;

		internal MySqlTable RealtimeTable;
		internal MySqlTable MonthlyTable;
		internal MySqlTable DayfileTable;

		public int CustomMySqlMinutesIntervalIndex;

		private bool customMySqlSecondsUpdateInProgress;
		private bool customMySqlMinutesUpdateInProgress;
		private bool customMySqlRolloverUpdateInProgress;
		private bool customMySqlTimedUpdateInProgress;

		private static HttpFiles httpFiles;

		public AirLinkData airLinkDataIn;
		public AirLinkData airLinkDataOut;

		public CustomLogSettings[] CustomIntvlLogSettings = new CustomLogSettings[10];
		public CustomLogSettings[] CustomDailyLogSettings = new CustomLogSettings[10];

		public string[] StationDesc =
		{
			"Davis Vantage Pro",			// 0
			"Davis Vantage Pro2",			// 1
			"Oregon Scientific WMR-928",	// 2
			"Oregon Scientific WM-918",		// 3
			"EasyWeather",					// 4
			"Fine Offset",					// 5
			"LaCrosse WS2300",				// 6
			"Fine Offset with Solar",		// 7
			"Oregon Scientific WMR100",		// 8
			"Oregon Scientific WMR200",		// 9
			"Instromet",					// 10
			"Davis WLL",					// 11
			"GW1000",						// 12
			"HTTP WUnderground",			// 13
			"HTTP Ecowitt",					// 14
			"HTTP Ambient",					// 15
			"WeatherFlow Tempest",			// 16
			"Simulator"						// 17
		};

		public string[] APRSstationtype = { "DsVP", "DsVP", "WMR928", "WM918", "EW", "FO", "WS2300", "FOs", "WMR100", "WMR200", "IMET", "DsVP", "Ecow", "Unkn", "Ecow", "Ambt", "Tmpt", "Simul" };

		public string loggingfile;
		public string ftpLogfile;

		//private PingReply pingReply;

		private SemaphoreSlim uploadCountLimitSemaphoreSlim;

		// Global cancellation token for when CMX is stopping
		public readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
		public CancellationToken cancellationToken;

		private static bool boolWindows;


		public Cumulus(int HTTPport, bool DebugEnabled, string startParms)
		{
			cancellationToken = tokenSource.Token;

			var fullVer = Assembly.GetExecutingAssembly().GetName().Version;
			Version = $"{fullVer.Major}.{fullVer.Minor}.{fullVer.Build}";
			Build = Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString();

			DirectorySeparator = Path.DirectorySeparatorChar;

			AppDir = Directory.GetCurrentDirectory() + DirectorySeparator;
			WebTagFile = AppDir + "WebTags.txt";

			//b3045>, use same port for WS...  WS port = HTTPS port
			//wsPort = WSport;
			wsPort = HTTPport;

			DebuggingEnabled = DebugEnabled;

			// Set up the diagnostic tracing
			loggingfile = RemoveOldDiagsFiles("CMX");
			ftpLogfile = RemoveOldDiagsFiles("FTP");

			Program.svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Creating main MX log file - " + loggingfile);
			Program.svcTextListener.Flush();

			TextWriterTraceListener myTextListener = new TextWriterTraceListener(loggingfile, "MXlog");
			Trace.Listeners.Add(myTextListener);
			Trace.AutoFlush = true;

			if (FtpOptions.Logging)
			{
				CreateFtpLogFile(ftpLogfile);
			}

			// Read the configuration file

			LogMessage(" ========================== Cumulus MX starting ==========================");

			LogMessage("Command line: " + Environment.CommandLine + " " + startParms);

			//Assembly thisAssembly = this.GetType().Assembly;
			//Version = thisAssembly.GetName().Version.ToString();
			//VersionLabel.Content = "Cumulus v." + thisAssembly.GetName().Version;
			LogMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Working Dir: " + AppDir);

			Type type = Type.GetType("Mono.Runtime");
			if (type != null)
			{
				IsOSX = IsRunningOnMac();

				MethodInfo displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
				if (displayName != null)
					LogMessage("Mono version: " + displayName.Invoke(null, null));
			}

			// restrict the threadpool size - for Mono which does not seem to have very good pool management!
			ThreadPool.SetMinThreads(Properties.Settings.Default.MinThreadPoolSize, Properties.Settings.Default.MinThreadPoolSize);
			ThreadPool.SetMaxThreads(Properties.Settings.Default.MaxThreadPoolSize, Properties.Settings.Default.MaxThreadPoolSize);


			Platform = IsOSX ? "Mac OS X" : Environment.OSVersion.Platform.ToString();

			LogMessage("Platform: " + Platform);

			LogMessage("OS version: " + Environment.OSVersion);

			LogMessage($"Current culture: {CultureInfo.CurrentCulture.DisplayName} [{CultureInfo.CurrentCulture.Name}]");

			LogMessage($"Running as a {(IntPtr.Size == 4 ? "32" : "64")} bit process");

			// Messy, but Windows and Linux use different mechanisms for loading DLLs, neither are very pretty
			// Simplest method is to leave them searching the application directory, and copy the correct file there before it is loaded by the first SQlite connect()
			// copy the correct sqlite DLL for your bitness
			var dstfile = "sqlite3.dll";
			var srcfile = (IntPtr.Size == 4 ? "x86" : "x64") + DirectorySeparator + dstfile;
			if (!Utils.FilesEqual(srcfile, dstfile))
			{
				try
				{
					LogMessage($"Copying {srcfile} to {dstfile}");
					File.Copy(srcfile, dstfile, true);
				}
				catch (FileNotFoundException)
				{
					var msg = "Error: cannot find the file: " + srcfile;
					LogMessage(msg);
					LogConsoleMessage(msg);
					Program.exitSystem = true;
					return;
				}
				catch (Exception ex)
				{
					var msg = $"Error copying file {srcfile}: {ex.Message}";
					LogMessage(msg);
					LogConsoleMessage(msg);
					Program.exitSystem = true;
					return;
				}
			}

			boolWindows = Platform.Substring(0, 3) == "Win";

			// Set the default comport name depending on platform
			DefaultComportName = boolWindows ? "COM1" : "/dev/ttyUSB0";

			// determine system uptime based on OS
			if (boolWindows)
			{
				try
				{
					// Windows enable the performance counter method
					UpTime = new PerformanceCounter("System", "System Up Time");
				}
				catch (Exception e)
				{
					LogMessage("Error: Unable to access the System Up Time performance counter. System up time will not be available");
					LogDebugMessage($"Error: {e}");
				}
			}

			// Check if all the folders required by CMX exist, if not create them
			CreateRequiredFolders();

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

			StdWebFiles = new FileGenerationOptions[]
			{
				new FileGenerationOptions()
				{
					TemplateFileName = WebPath + "websitedataT.json",
					LocalPath = WebPath,
					LocalFileName = "websitedata.json",
					RemoteFileName = "websitedata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = "",
					LocalFileName = "wxnow.txt",
					RemoteFileName = "wxnow.txt"
				}
			};

			RealtimeFiles = new FileGenerationOptions[]
			{
				new FileGenerationOptions()
				{
					LocalFileName = "realtime.txt",
					RemoteFileName = "realtime.txt"
				},
				new FileGenerationOptions()
				{
					TemplateFileName = WebPath + "realtimegaugesT.txt",
					LocalPath = WebPath,
					LocalFileName = "realtimegauges.txt",
					RemoteFileName = "realtimegauges.txt"
				}
			};

			GraphDataFiles = new FileGenerationOptions[]
			{
				new FileGenerationOptions()		// 0
				{
					LocalPath = WebPath,
					LocalFileName = "graphconfig.json",
					RemoteFileName = "graphconfig.json"
				},
				new FileGenerationOptions()		// 1
				{
					LocalPath = WebPath,
					LocalFileName = "availabledata.json",
					RemoteFileName = "availabledata.json"
				},
				new FileGenerationOptions()		// 2
				{
					LocalPath = WebPath,
					LocalFileName = "tempdata.json",
					RemoteFileName = "tempdata.json"
				},
				new FileGenerationOptions()		// 3
				{
					LocalPath = WebPath,
					LocalFileName = "pressdata.json",
					RemoteFileName = "pressdata.json"
				},
				new FileGenerationOptions()		// 4
				{
					LocalPath = WebPath,
					LocalFileName = "winddata.json",
					RemoteFileName = "winddata.json"
				},
				new FileGenerationOptions()		// 5
				{
					LocalPath = WebPath,
					LocalFileName = "wdirdata.json",
					RemoteFileName = "wdirdata.json"
				},
				new FileGenerationOptions()		// 6
				{
					LocalPath = WebPath,
					LocalFileName = "humdata.json",
					RemoteFileName = "humdata.json"
				},
				new FileGenerationOptions()		// 7
				{
					LocalPath = WebPath,
					LocalFileName = "raindata.json",
					RemoteFileName = "raindata.json"
				},
				new FileGenerationOptions()		// 8
				{
					LocalPath = WebPath,
					LocalFileName = "dailyrain.json",
					RemoteFileName = "dailyrain.json"
				},
				new FileGenerationOptions()		// 9
				{
					LocalPath = WebPath,
					LocalFileName = "dailytemp.json",
					RemoteFileName = "dailytemp.json"
				},
				new FileGenerationOptions()		// 10
				{
					LocalPath = WebPath,
					LocalFileName = "solardata.json",
					RemoteFileName = "solardata.json"
				},
				new FileGenerationOptions()		// 11
				{
					LocalPath = WebPath,
					LocalFileName = "sunhours.json",
					RemoteFileName = "sunhours.json"
				},
				new FileGenerationOptions()		// 12
				{
					LocalPath = WebPath,
					LocalFileName = "airquality.json",
					RemoteFileName = "airquality.json"
				},
				new FileGenerationOptions()		// 13
				{
					LocalPath = WebPath,
					LocalFileName = "extratempdata.json",
					RemoteFileName = "extratempdata.json"
				},
				new FileGenerationOptions()		// 14
				{
					LocalPath = WebPath,
					LocalFileName = "extrahumdata.json",
					RemoteFileName = "extrahumdata.json"
				},
				new FileGenerationOptions()		// 15
				{
					LocalPath = WebPath,
					LocalFileName = "extradewdata.json",
					RemoteFileName = "extradewdata.json"
				},
				new FileGenerationOptions()		// 16
				{
					LocalPath = WebPath,
					LocalFileName = "soiltempdata.json",
					RemoteFileName = "soiltempdata.json"
				},
				new FileGenerationOptions()		// 17
				{
					LocalPath = WebPath,
					LocalFileName = "soilmoistdata.json",
					RemoteFileName = "soilmoistdata.json"
				},
				new FileGenerationOptions()		// 18
				{
					LocalPath = WebPath,
					LocalFileName = "usertempdata.json",
					RemoteFileName = "usertempdata.json"
				},
				new FileGenerationOptions()		// 19
				{
					LocalPath = WebPath,
					LocalFileName = "co2sensordata.json",
					RemoteFileName = "co2sensordata.json"
				},
				new FileGenerationOptions()     // 20
				{
					LocalPath = WebPath,
					LocalFileName = "leafwetdata.json",
					RemoteFileName = "leafwetdata.json"
				}
			};

			GraphDataEodFiles = new FileGenerationOptions[]
			{
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailytempdata.json",
					RemoteFileName = "alldailytempdata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailypressdata.json",
					RemoteFileName = "alldailypressdata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailywinddata.json",
					RemoteFileName = "alldailywinddata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailyhumdata.json",
					RemoteFileName = "alldailyhumdata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailyraindata.json",
					RemoteFileName = "alldailyraindata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailysolardata.json",
					RemoteFileName = "alldailysolardata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailydegdaydata.json",
					RemoteFileName = "alldailydegdaydata.json"
				},
				new FileGenerationOptions()
				{
					LocalPath = WebPath,
					LocalFileName = "alltempsumdata.json",
					RemoteFileName = "alltempsumdata.json"
				}
			};

			ProgramOptions.Culture = new CultureConfig();

			for (var i = 0; i < 10; i++)
			{
				CustomIntvlLogSettings[i] = new CustomLogSettings();
				CustomDailyLogSettings[i] = new CustomLogSettings();
			}

			ReadIniFile();

			// Do we prevent more than one copy of CumulusMX running?
			CheckForSingleInstance(boolWindows);

			if (FtpOptions.FtpMode == FtpProtocols.PHP)
				LogMessage("Maximum concurrent PHP Uploads = " + FtpOptions.MaxConcurrentUploads);
			uploadCountLimitSemaphoreSlim = new SemaphoreSlim(FtpOptions.MaxConcurrentUploads);

			ListSeparator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

			DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

			LogMessage($"Directory separator=[{DirectorySeparator}] Decimal separator=[{DecimalSeparator}] List separator=[{ListSeparator}]");
			LogMessage($"Date separator=[{CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator}] Time separator=[{CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator}]");

			TimeZone localZone = TimeZone.CurrentTimeZone;
			DateTime now = DateTime.Now;

			LogMessage("Standard time zone name:   " + localZone.StandardName);
			LogMessage("Daylight saving time name: " + localZone.DaylightName);
			LogMessage("Daylight saving time? " + localZone.IsDaylightSavingTime(now));

			LogMessage(DateTime.Now.ToString("G"));

			// Do we delay the start of Cumulus MX for a fixed period?
			if (ProgramOptions.StartupDelaySecs > 0)
			{
				// Check uptime
				double ts = -1;
				if (boolWindows && UpTime != null)
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
				LogMessage($"System uptime = {ts:F0} secs");
				if (ProgramOptions.StartupDelayMaxUptime == 0 || (ts > -1 && ProgramOptions.StartupDelayMaxUptime > ts))
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

			// Do we wait for a ping response from a remote host before starting?
			if (!string.IsNullOrWhiteSpace(ProgramOptions.StartupPingHost))
			{
				var msg0 = $"Sending PING to {ProgramOptions.StartupPingHost}";
				var msg1 = $"Waiting for PING reply from {ProgramOptions.StartupPingHost}";
				var msg2 = $"Received PING response from {ProgramOptions.StartupPingHost}, continuing...";
				var msg3 = $"No PING response received in {ProgramOptions.StartupPingEscapeTime} minutes, continuing anyway";
				var escapeTime = DateTime.Now.AddMinutes(ProgramOptions.StartupPingEscapeTime);
				var attempt = 1;
				var pingSuccess = false;
				// This is the timeout for "hung" attempts, we will double this at every failure so we do not create too many hung resources
				var pingTimeoutSecs = 10;

				do
				{
					var pingTimeoutDT = DateTime.Now.AddSeconds(pingTimeoutSecs);
					var pingTokenSource = new CancellationTokenSource(pingTimeoutSecs * 1000);
					var pingCancelToken = pingTokenSource.Token;

					LogDebugMessage($"Starting PING #{attempt} task with time-out of {pingTimeoutSecs} seconds");

					var pingTask = Task.Run(() =>
					{
						var cnt = attempt;
						using (var ping = new Ping())
						{
							try
							{
								LogMessage($"Sending PING #{cnt} to {ProgramOptions.StartupPingHost}");

								// set the actual ping timeout 5 secs less than the task timeout
								var reply = ping.Send(ProgramOptions.StartupPingHost, (pingTimeoutSecs - 5) * 1000);

								// were we hung on the network and now cancelled? if so just exit silently
								if (pingCancelToken.IsCancellationRequested)
								{
									LogDebugMessage($"Cancelled PING #{cnt} task exiting");
								}
								else
								{
									var msg = $"Received PING #{cnt} response from {ProgramOptions.StartupPingHost}, status: {reply.Status}";

									LogMessage(msg);
									LogConsoleMessage(msg);

									if (reply.Status == IPStatus.Success)
									{
										pingSuccess = true;
									}
								}
							}
							catch (Exception e)
							{
								LogMessage($"PING #{cnt} to {ProgramOptions.StartupPingHost} failed with error: {e.InnerException.Message}");
							}

						}
					}, pingCancelToken);

					// wait for the ping to return
					do
					{
						Thread.Sleep(100);
					} while (pingTask.Status == TaskStatus.Running && DateTime.Now < pingTimeoutDT);

					LogDebugMessage($"PING #{attempt} task status: {pingTask.Status}");

					// did we timeout waiting for the task to end?
					if (DateTime.Now >= pingTimeoutDT)
					{
						// yep, so attempt to cancel the task
						LogMessage($"Nothing returned from PING #{attempt}, attempting the cancel the task");
						pingTokenSource.Cancel();
						// and double the timeout for next attempt
						pingTimeoutSecs *= 2;
					}

					if (!pingSuccess)
					{
						// no response wait 10 seconds before trying again
						LogDebugMessage("Waiting 10 seconds before retry...");
						Thread.Sleep(10000);
						attempt++;
						// Force a DNS refresh if not an IPv4 address
						if (!Utils.ValidateIPv4(ProgramOptions.StartupPingHost))
						{
							// catch and ignore IPv6 and invalid host name for now
							try
							{
								Dns.GetHostEntry(ProgramOptions.StartupPingHost);
							}
							catch (Exception ex)
							{
								LogMessage($"PING #{attempt}: Error with DNS refresh - {ex.Message}");
							}
						}
					}
				} while (!pingSuccess && DateTime.Now < escapeTime);

				if (DateTime.Now >= escapeTime)
				{
					LogConsoleMessage(msg3, ConsoleColor.Yellow);
					LogMessage(msg3);
				}
				else
				{
					LogConsoleMessage(msg2);
					LogMessage(msg2);
				}
			}
			else
			{
				LogMessage("No start-up PING");
			}

			// do we have a start-up task to run?
			if (!string.IsNullOrEmpty(ProgramOptions.StartupTask))
			{
				LogMessage($"Running start-up task: {ProgramOptions.StartupTask}, arguments: {ProgramOptions.StartupTaskParams}, wait: {ProgramOptions.StartupTaskWait}");
				try
				{
					Utils.RunExternalTask(ProgramOptions.StartupTask, ProgramOptions.StartupTaskParams, ProgramOptions.StartupTaskWait);
				}
				catch (Exception ex)
				{
					LogMessage($"Error running start-up task: {ex.Message}");
				}
			}

			GC.Collect();

			LogMessage("Data path = " + Datapath);

			AppDomain.CurrentDomain.SetData("DataDirectory", Datapath);

			// Open database (create file if it doesn't exist)
			SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;

			// Open diary database (create file if it doesn't exist)
			//DiaryDB = new SQLiteConnection(diaryfile, flags, true);  // We should be using this - storing datetime as ticks, but historically string storage has been used, so we are stuck with it?
			//DiaryDB = new SQLiteConnection(diaryfile, flags, false);
			DiaryDB = new SQLiteConnection(new SQLiteConnectionString(diaryfile, flags, false, null, null, null, null, "yyyy-MM-dd 00:00:00"));
			DiaryDB.CreateTable<DiaryData>();

			try
			{
				// clean-up the diary db, change any entries to use date+time to just use date
				// first see if there will be any days with more than one record
				var duplicates = DiaryDB.Query<DiaryData>("SELECT * FROM DiaryData WHERE rowid < (SELECT max(rowid) FROM DiaryData d2 WHERE date(DiaryData.Timestamp) = date(d2.Timestamp))");
				if (duplicates.Count > 0)
				{
					LogConsoleMessage($"WARNING: Duplicate entries ({duplicates.Count}) found in your Weather Diary database - please see log file for details");
					LogMessage($"Duplicate entries ({duplicates.Count}) found in the Weather Diary database. The following entries will be removed...");

					foreach (var rec in duplicates)
					{
						LogMessage($"  Date: {rec.Timestamp.Date}, Falling: {rec.snowFalling}, Lying: {rec.snowLying}, Depth: {rec.snowDepth}, Entry: '{rec.entry}'");
					}

					// Remove the duplicates, leave the latest, remove the oldest
					var deleted = DiaryDB.Execute("DELETE FROM DiaryData WHERE rowid < (SELECT max(rowid) FROM DiaryData d2 WHERE date(DiaryData.Timestamp) = date(d2.Timestamp))");
					if (deleted > 0)
					{
						LogMessage($"{deleted} duplicate records deleted from the weather diary database");
					}
				}
				// Now reset the now unique-by-day records to have a time of 00:00:00
				DiaryDB.Execute("UPDATE DiaryData SET Timestamp = datetime(date(TimeStamp)) WHERE time(Timestamp) <> '00:00:00'");
			}
			catch (Exception ex)
			{
				LogErrorMessage("Error cleaning up the Diary DB, exception = " + ex.Message);
			}

			BackupData(false, DateTime.Now);

			LogMessage("Debug logging :" + (ProgramOptions.DebugLogging ? "enabled" : "disabled"));
			LogMessage("Data logging  :" + (ProgramOptions.DataLogging ? "enabled" : "disabled"));
			LogMessage("FTP logging   :" + (FtpOptions.Logging ? "enabled" : "disabled"));
			LogMessage("Email logging :" + (SmtpOptions.Logging ? "enabled" : "disabled"));
			LogMessage("Spike logging :" + (ErrorLogSpikeRemoval ? "enabled" : "disabled"));
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
			TempTrendFormat = "+0.0;-0.0;0.0";
			PressTrendFormat = $"+0.{new string('0', PressDPlaces)};-0.{new string('0', PressDPlaces)};0.{new string('0', PressDPlaces)}";
			AirQualityFormat = "F" + AirQualityDPlaces;

			SetupRealtimeMySqlTable();
			SetupMonthlyMySqlTable();
			SetupDayfileMySqlTable();

			ReadStringsFile();

			MyHttpClient.Timeout = new TimeSpan(0, 0, 15);  // 15 second timeout on all http calls

			SetUpHttpProxy();

			if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				SetupPhpUploadClients();
				TestPhpUploadCompression();
			}


			CustomMysqlSecondsTimer = new Timer { Interval = MySqlSettings.CustomSecs.Interval * 1000 };
			CustomMysqlSecondsTimer.Elapsed += CustomMysqlSecondsTimerTick;
			CustomMysqlSecondsTimer.AutoReset = true;

			CustomHttpSecondsTimer = new Timer { Interval = CustomHttpSecondsInterval * 1000 };
			CustomHttpSecondsTimer.Elapsed += CustomHttpSecondsTimerTick;
			CustomHttpSecondsTimer.AutoReset = true;

			if (SmtpOptions.Enabled)
			{
				emailer = new EmailSender(this);
			}

			DoSunriseAndSunset();
			DoMoonPhase();
			MoonAge = MoonriseMoonset.MoonAge();
			DoMoonImage();

			LogMessage("Station type: " + (StationType == -1 ? "Undefined" : StationType + " - " + StationDesc[StationType]));

			SetupUnitText();

			LogMessage($"WindUnit={Units.WindText} RainUnit={Units.RainText} TempUnit={Units.TempText} PressureUnit={Units.PressText}");
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
			HttpUploadAlarm.cumulus = this;
			MySqlUploadAlarm.cumulus = this;
			IsRainingAlarm.cumulus = this;

			GetLatestVersion();

			LogMessage("Cumulus Starting");

			// switch off logging from Unosquare.Swan which underlies embedIO
			Swan.Logging.Logger.NoLogging();

			httpFiles = new HttpFiles(this);

			var assemblyPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
			var htmlRootPath = Path.Combine(assemblyPath, "interface");

			LogMessage("HTML root path = " + htmlRootPath);

			WebSock = new MxWebSocket("/ws/", this);

			WebServer httpServer = new WebServer(o => o
					.WithUrlPrefix($"http://*:{HTTPport}/")
					.WithMode(HttpListenerMode.EmbedIO)
				)
				.WithWebApi("/api/", m => m
					.WithController<Api.EditController>()
					.WithController<Api.DataController>()
					.WithController<Api.TagController>()
					.WithController<Api.GraphDataController>()
					.WithController<Api.RecordsController>()
					.WithController<Api.TodayYestDataController>()
					.WithController<Api.ExtraDataController>()
					.WithController<Api.SettingsController>()
					.WithController<Api.ReportsController>()
					.WithController<Api.UtilsController>()
				)
				.WithWebApi("/station", m => m
					.WithController<Api.HttpStation>()
				)
				.WithModule(WebSock)
				.WithStaticFolder("/", htmlRootPath, true, m => m
					.WithoutContentCaching()
				);

			// Set up the API web server
			// Some APi functions require the station, so set them after station initialisation
			Api.programSettings = new ProgramSettings(this);
			Api.stationSettings = new StationSettings(this);
			Api.internetSettings = new InternetSettings(this);
			Api.thirdpartySettings = new ThirdPartySettings(this);
			Api.extraSensorSettings = new ExtraSensorSettings(this);
			Api.calibrationSettings = new CalibrationSettings(this);
			Api.noaaSettings = new NOAASettings(this);
			Api.alarmSettings = new AlarmSettings(this);
			Api.mySqlSettings = new MysqlSettings(this);
			Api.customLogs = new CustomLogs(this);
			Api.httpFiles = httpFiles;
			Api.dataEditor = new DataEditor(this);
			Api.tagProcessor = new ApiTagProcessor(this);
			Api.wizard = new Wizard(this);
			Api.langSettings = new LangSettings(this);
			Api.displaySettings = new DisplaySettings(this);

			_ = httpServer.RunAsync();

			// get the local v4 IP addresses
			Console.WriteLine();
			var ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
			LogMessage($"Cumulus running at: http://localhost:{HTTPport}/");
			Console.Write("Cumulus running at: ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"http://localhost:{HTTPport}/");
			Console.ResetColor();
			foreach (var ip in ips)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					LogConsoleMessage($"                    http://{ip}:{HTTPport}/", ConsoleColor.Yellow);
				}

			}

			Console.WriteLine();
			if (File.Exists("Cumulus.ini"))
			{
				LogConsoleMessage("  Open the admin interface by entering one of the above URLs into a web browser.", ConsoleColor.Cyan);
			}
			else
			{
				LogConsoleMessage("  Leave this window open, then...", ConsoleColor.Cyan);
				LogConsoleMessage("  Run the First Time Configuration Wizard by entering one of the URLs above plus \"wizard.html\" into your browser", ConsoleColor.Cyan);
				LogConsoleMessage($"  e.g. http://localhost:{HTTPport}/wizard.html", ConsoleColor.Cyan);
			}
			Console.WriteLine();

			//LogDebugMessage("Lock: Cumulus waiting for the lock");
			syncInit.Wait();
			//LogDebugMessage("Lock: Cumulus has lock");

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
				case StationTypes.Tempest:
					Manufacturer = WEATHERFLOW;
					station = new TempestStation(this);
					break;
				case StationTypes.HttpWund:
					Manufacturer = HTTPSTATION;
					station = new HttpStationWund(this);
					break;
				case StationTypes.HttpEcowitt:
					Manufacturer = ECOWITT;
					station = new HttpStationEcowitt(this);
					break;
				case StationTypes.HttpAmbient:
					Manufacturer = AMBIENT;
					station = new HttpStationAmbient(this);
					break;
				case StationTypes.Simulator:
					Manufacturer = SIMULATOR;
					station = new Simulator(this);
					break;
				default:
					LogConsoleMessage("Station type not set", ConsoleColor.Red);
					LogMessage("Station type not set");
					break;
			}

			if (station != null)
			{
				Api.Station = station;
				Api.stationSettings.SetStation(station);
				Api.extraSensorSettings.SetStation(station);
				Api.dataEditor.SetStation(station);

				if (StationType == StationTypes.HttpWund)
				{
					Api.stationWund = (HttpStationWund) station;
				}
				else if (StationType == StationTypes.HttpEcowitt)
				{
					Api.stationEcowitt = (HttpStationEcowitt) station;
				}
				else if (StationType == StationTypes.HttpAmbient)
				{
					Api.stationAmbient = (HttpStationAmbient) station;
				}

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
				if (EcowittExtraEnabled)
				{
					ecowittExtra = new HttpStationEcowitt(this, station);
					Api.stationEcowittExtra = ecowittExtra;
				}
				if (AmbientExtraEnabled)
				{
					ambientExtra = new HttpStationAmbient(this, station);
					Api.stationAmbientExtra = ambientExtra;
				}

				webtags = new WebTags(this, station);
				webtags.InitialiseWebtags();

				Api.dataEditor.SetWebTags(webtags);
				Api.tagProcessor.SetWebTags(webtags);

				RealtimeTimer.Interval = RealtimeInterval;
				RealtimeTimer.Elapsed += RealtimeTimerTick;
				RealtimeTimer.AutoReset = true;

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

				if ((StationType == StationTypes.WMR100) || (StationType == StationTypes.EasyWeather) || (Manufacturer == OREGON) || StationType == StationTypes.Simulator)
				{
					station.StartLoop();
				}

				// let the web socket know about the station
				WebSock.SetStation = station;

				// If enabled generate the daily graph data files, and upload at first opportunity
				LogDebugMessage("Generating the daily graph data files");
				station.CreateEodGraphDataFiles();
				station.CreateDailyGraphDataFiles();
			}

			LogDebugMessage("Lock: Cumulus releasing the lock");
			syncInit.Release();
		}

		internal void SetUpHttpProxy()
		{
			if (!string.IsNullOrEmpty(HTTPProxyName))
			{
				var proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				MyHttpHandler.Proxy = proxy;
				MyHttpHandler.UseProxy = true;

				phpUploadHttpHandler.Proxy = proxy;
				phpUploadHttpHandler.UseProxy = true;

				if (!string.IsNullOrEmpty(HTTPProxyUser))
				{
					var creds = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					MyHttpHandler.Credentials = creds;
					phpUploadHttpHandler.Credentials = creds;
				}
			}
		}

		internal void SetupPhpUploadClients()
		{
			phpUploadHttpHandler = new HttpClientHandler
			{
				ClientCertificateOptions = ClientCertificateOption.Manual,
				ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) =>
				{
					return FtpOptions.PhpIgnoreCertErrors ? true : errors == System.Net.Security.SslPolicyErrors.None;
				},
				MaxConnectionsPerServer = 50,
				AllowAutoRedirect = false
			};

			phpUploadHttpClient = new HttpClient(phpUploadHttpHandler);

			phpUploadHttpClient.Timeout = TimeSpan.FromSeconds(20);
		}


		internal async void TestPhpUploadCompression()
		{
			LogDebugMessage($"Testing PHP upload compression: '{FtpOptions.PhpUrl}'");
			using (var request = new HttpRequestMessage(HttpMethod.Get, FtpOptions.PhpUrl))
			{
				try
				{
					request.Headers.Add("Accept", "text/html");
					request.Headers.Add("Accept-Encoding", "gzip, deflate");
					var response = await phpUploadHttpClient.SendAsync(request);
					response.EnsureSuccessStatusCode();
					var encoding = response.Content.Headers.ContentEncoding;

					FtpOptions.PhpCompression = encoding.Count == 0 ? "none" : encoding.First();

					if (FtpOptions.PhpCompression == "none")
					{
						LogDebugMessage("PHP upload does not support compression");
					}
					else
					{
						LogDebugMessage($"PHP upload supports {FtpOptions.PhpCompression} compression");
					}
				}
				catch (Exception ex)
				{
					LogMessage("TestPhpUploadCompression: Error - " + ex.Message);
				}
			}
		}

		private void CustomHttpSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!station.DataStopped)
				CustomHttpSecondsUpdate();
		}


		internal void SetupRealtimeMySqlTable()
		{
			RealtimeTable = new MySqlTable(MySqlSettings.Realtime.TableName);
			RealtimeTable.AddColumn("LogDateTime", "DATETIME NOT NULL");
			RealtimeTable.AddColumn("temp", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("hum", "decimal(4," + HumDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("dew", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("wspeed", "decimal(4," + WindDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("wlatest", "decimal(4," + WindDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("bearing", "VARCHAR(3) NOT NULL");
			RealtimeTable.AddColumn("rrate", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("rfall", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("press", "decimal(6," + PressDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("currentwdir", "VARCHAR(3) NOT NULL");
			RealtimeTable.AddColumn("beaufortnumber", "varchar(2) NOT NULL");
			RealtimeTable.AddColumn("windunit", "varchar(4) NOT NULL");
			RealtimeTable.AddColumn("tempunitnodeg", "varchar(1) NOT NULL");
			RealtimeTable.AddColumn("pressunit", "varchar(3) NOT NULL");
			RealtimeTable.AddColumn("rainunit", "varchar(2) NOT NULL");
			RealtimeTable.AddColumn("windrun", "decimal(4," + WindRunDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("presstrendval", "varchar(6) NOT NULL");
			RealtimeTable.AddColumn("rmonth", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("ryear", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("rfallY", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("intemp", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("inhum", "decimal(4," + HumDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("wchill", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("temptrend", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("tempTH", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("TtempTH", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("tempTL", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("TtempTL", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("windTM", "decimal(4," + WindDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("TwindTM", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("wgustTM", "decimal(4," + WindDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("TwgustTM", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("pressTH", "decimal(6," + PressDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("TpressTH", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("pressTL", "decimal(6," + PressDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("TpressTL", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("version", "varchar(8) NOT NULL");
			RealtimeTable.AddColumn("build", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("wgust", "decimal(4," + WindDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("heatindex", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("humidex", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("UV", "decimal(3," + UVDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("ET", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("SolarRad", "decimal(5,1) NOT NULL");
			RealtimeTable.AddColumn("avgbearing", "varchar(3) NOT NULL");
			RealtimeTable.AddColumn("rhour", "decimal(4," + RainDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("forecastnumber", "varchar(2) NOT NULL");
			RealtimeTable.AddColumn("isdaylight", "varchar(1) NOT NULL");
			RealtimeTable.AddColumn("SensorContactLost", "varchar(1) NOT NULL");
			RealtimeTable.AddColumn("wdir", "varchar(3) NOT NULL");
			RealtimeTable.AddColumn("cloudbasevalue", "varchar(5) NOT NULL");
			RealtimeTable.AddColumn("cloudbaseunit", "varchar(2) NOT NULL");
			RealtimeTable.AddColumn("apptemp", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("SunshineHours", "decimal(3," + SunshineDPlaces + ") NOT NULL");
			RealtimeTable.AddColumn("CurrentSolarMax", "decimal(5,1) NOT NULL");
			RealtimeTable.AddColumn("IsSunny", "varchar(1) NOT NULL");
			RealtimeTable.AddColumn("FeelsLike", "decimal(4," + TempDPlaces + ") NOT NULL");
			RealtimeTable.PrimaryKey = "LogDateTime";
			RealtimeTable.Comment = "\"Realtime log\"";
		}

		internal void SetupMonthlyMySqlTable()
		{
			MonthlyTable = new MySqlTable(MySqlSettings.Monthly.TableName);
			MonthlyTable.AddColumn("LogDateTime", "DATETIME NOT NULL");
			MonthlyTable.AddColumn("Temp", "decimal(4," + TempDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Humidity", "decimal(4," + HumDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Dewpoint", "decimal(4," + TempDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Windspeed", "decimal(4," + WindAvgDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Windgust", "decimal(4," + WindDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Windbearing", "VARCHAR(3) NOT NULL");
			MonthlyTable.AddColumn("RainRate", "decimal(4," + RainDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("TodayRainSoFar", "decimal(4," + RainDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Pressure", "decimal(6," + PressDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("Raincounter", "decimal(6," + RainDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("InsideTemp", "decimal(4," + TempDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("InsideHumidity", "decimal(4," + HumDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("LatestWindGust", "decimal(5," + WindDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("WindChill", "decimal(4," + TempDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("HeatIndex", "decimal(4," + TempDPlaces + ") NOT NULL");
			MonthlyTable.AddColumn("UVindex", "decimal(4," + UVDPlaces + ")");
			MonthlyTable.AddColumn("SolarRad", "decimal(5,1)");
			MonthlyTable.AddColumn("Evapotrans", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("AnnualEvapTran", "decimal(5," + RainDPlaces + ")");
			MonthlyTable.AddColumn("ApparentTemp", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("MaxSolarRad", "decimal(5,1)");
			MonthlyTable.AddColumn("HrsSunShine", "decimal(3," + SunshineDPlaces + ")");
			MonthlyTable.AddColumn("CurrWindBearing", "varchar(3)");
			MonthlyTable.AddColumn("RG11rain", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("RainSinceMidnight", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("WindbearingSym", "varchar(3)");
			MonthlyTable.AddColumn("CurrWindBearingSym", "varchar(3)");
			MonthlyTable.AddColumn("FeelsLike", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("Humidex", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.PrimaryKey = "LogDateTime";
			MonthlyTable.Comment = "\"Monthly logs from Cumulus\"";
		}

		internal void SetupDayfileMySqlTable()
		{
			DayfileTable = new MySqlTable(MySqlSettings.Dayfile.TableName);
			DayfileTable.AddColumn("LogDate", "date NOT NULL");
			DayfileTable.AddColumn("HighWindGust", "decimal(4," + WindDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("HWindGBear", "varchar(3) NOT NULL");
			DayfileTable.AddColumn("THWindG", "varchar(5) NOT NULL");
			DayfileTable.AddColumn("MinTemp", "decimal(5," + TempDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("TMinTemp", "varchar(5) NOT NULL");
			DayfileTable.AddColumn("MaxTemp", "decimal(5," + TempDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("TMaxTemp", "varchar(5) NOT NULL");
			DayfileTable.AddColumn("MinPress", "decimal(6," + PressDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("TMinPress", "varchar(5) NOT NULL");
			DayfileTable.AddColumn("MaxPress", "decimal(6," + PressDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("TMaxPress", "varchar(5) NOT NULL");
			DayfileTable.AddColumn("MaxRainRate", "decimal(4," + RainDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("TMaxRR", "varchar(5) NOT NULL");
			DayfileTable.AddColumn("TotRainFall", "decimal(6," + RainDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("AvgTemp", "decimal(5," + TempDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("TotWindRun", "decimal(5," + WindRunDPlaces + ") NOT NULL");
			DayfileTable.AddColumn("HighAvgWSpeed", "decimal(3," + WindAvgDPlaces + ")");
			DayfileTable.AddColumn("THAvgWSpeed", "varchar(5)");
			DayfileTable.AddColumn("LowHum", "decimal(4," + HumDPlaces + ")");
			DayfileTable.AddColumn("TLowHum", "varchar(5)");
			DayfileTable.AddColumn("HighHum", "decimal(4," + HumDPlaces + ")");
			DayfileTable.AddColumn("THighHum", "varchar(5)");
			DayfileTable.AddColumn("TotalEvap", "decimal(5," + RainDPlaces + ")");
			DayfileTable.AddColumn("HoursSun", "decimal(3," + SunshineDPlaces + ")");
			DayfileTable.AddColumn("HighHeatInd", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("THighHeatInd", "varchar(5)");
			DayfileTable.AddColumn("HighAppTemp", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("THighAppTemp", "varchar(5)");
			DayfileTable.AddColumn("LowAppTemp", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TLowAppTemp", "varchar(5)");
			DayfileTable.AddColumn("HighHourRain", "decimal(4," + RainDPlaces + ")");
			DayfileTable.AddColumn("THighHourRain", "varchar(5)");
			DayfileTable.AddColumn("LowWindChill", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TLowWindChill", "varchar(5)");
			DayfileTable.AddColumn("HighDewPoint", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("THighDewPoint", "varchar(5)");
			DayfileTable.AddColumn("LowDewPoint", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TLowDewPoint", "varchar(5)");
			DayfileTable.AddColumn("DomWindDir", "varchar(3)");
			DayfileTable.AddColumn("HeatDegDays", "decimal(4,1)");
			DayfileTable.AddColumn("CoolDegDays", "decimal(4,1)");
			DayfileTable.AddColumn("HighSolarRad", "decimal(5,1)");
			DayfileTable.AddColumn("THighSolarRad", "varchar(5)");
			DayfileTable.AddColumn("HighUV", "decimal(3," + UVDPlaces + ")");
			DayfileTable.AddColumn("THighUV", "varchar(5)");
			DayfileTable.AddColumn("HWindGBearSym", "varchar(3)");
			DayfileTable.AddColumn("DomWindDirSym", "varchar(3)");
			DayfileTable.AddColumn("MaxFeelsLike", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TMaxFeelsLike", "varchar(5)");
			DayfileTable.AddColumn("MinFeelsLike", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TMinFeelsLike", "varchar(5)");
			DayfileTable.AddColumn("MaxHumidex", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TMaxHumidex", "varchar(5)");
			DayfileTable.AddColumn("ChillHours", "decimal(7," + TempDPlaces + ")");
			DayfileTable.AddColumn("HighRain24h", "decimal(6," + RainDPlaces + ")");
			DayfileTable.AddColumn("THighRain24h", "varchar(5)");
			DayfileTable.PrimaryKey = "LogDate";
			DayfileTable.Comment = "\"Dayfile from Cumulus\"";
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

		// If the temperature units are changed, reset NOAA thresholds to defaults
		internal void ChangeTempUnits()
		{
			SetupUnitText();

			NOAAconf.HeatThreshold = Units.Temp == 0 ? 18.3 : 65;
			NOAAconf.CoolThreshold = Units.Temp == 0 ? 18.3 : 65;
			NOAAconf.MaxTempComp1 = Units.Temp == 0 ? 27 : 80;
			NOAAconf.MaxTempComp2 = Units.Temp == 0 ? 0 : 32;
			NOAAconf.MinTempComp1 = Units.Temp == 0 ? 0 : 32;
			NOAAconf.MinTempComp2 = Units.Temp == 0 ? -18 : 0;

			ChillHourThreshold = Units.Temp == 0 ? 7 : 45;

			GrowingBase1 = Units.Temp == 0 ? 5.0 : 40.0;
			GrowingBase2 = Units.Temp == 0 ? 10.0 : 50.0;

			TempChangeAlarm.Units = Units.TempTrendText;
			HighTempAlarm.Units = Units.TempText;
			LowTempAlarm.Units = Units.TempText;
		}

		internal void ChangeRainUnits()
		{
			SetupUnitText();

			NOAAconf.RainComp1 = Units.Rain == 0 ? 0.2 : 0.01;
			NOAAconf.RainComp2 = Units.Rain == 0 ? 2 : 0.1;
			NOAAconf.RainComp3 = Units.Rain == 0 ? 20 : 1;

			HighRainRateAlarm.Units = Units.RainTrendText;
			HighRainTodayAlarm.Units = Units.RainText;
		}

		internal void ChangePressureUnits()
		{
			SetupUnitText();

			FCPressureThreshold = Units.Press == 2 ? 0.00295333727 : 0.1;

			PressChangeAlarm.Units = Units.PressTrendText;
			HighPressAlarm.Units = Units.PressText;
			LowPressAlarm.Units = Units.PressText;
		}

		internal void ChangeWindUnits()
		{
			SetupUnitText();

			HighWindAlarm.Units = Units.WindText;
			HighGustAlarm.Units = Units.WindText;
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
					IsRainingAlarm.Triggered = isOn;
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
					IsRainingAlarm.Triggered = isOn;
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

			var now = DateTime.Now;

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
				ftpThread = new Thread(() => DoHTMLFiles()) { IsBackground = true };
				ftpThread.Start();
			}
			else
			{
				WebUpdating = 1;
				ftpThread = new Thread(() => DoHTMLFiles()) { IsBackground = true };
				ftpThread.Start();
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
				using (var response = await MyHttpClient.GetAsync(URL))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode != HttpStatusCode.OK)
					{
						// Flag the error immediately if no rapid fire
						// Flag error after every 12 rapid fire failures (1 minute)
						Wund.ErrorFlagCount++;
						if (!Wund.RapidFireEnabled || Wund.ErrorFlagCount >= 12)
						{
							LogMessage("Wunderground: Response = " + response.StatusCode + ": " + responseBodyAsText);
							HttpUploadAlarm.LastError = "Wunderground: HTTP response - " + response.StatusCode;
							HttpUploadAlarm.Triggered = true;
							Wund.ErrorFlagCount = 0;
						}
					}
					else
					{
						HttpUploadAlarm.Triggered = false;
						Wund.ErrorFlagCount = 0;
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("Wunderground: ERROR - " + ex.Message);
				HttpUploadAlarm.LastError = "Wunderground: " + ex.Message;
				HttpUploadAlarm.Triggered = true;
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
				using (var response = await MyHttpClient.GetAsync(url))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("Windy: Response = " + response.StatusCode + ": " + responseBodyAsText);
					if (response.StatusCode != HttpStatusCode.OK)
					{
						LogMessage("Windy: ERROR - Response = " + response.StatusCode + ": " + responseBodyAsText);
						HttpUploadAlarm.LastError = "Windy: HTTP response - " + response.StatusCode;
						HttpUploadAlarm.Triggered = true;
					}
					else
					{
						HttpUploadAlarm.Triggered = false;
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("Windy: ERROR - " + ex.Message);
				HttpUploadAlarm.LastError = "Windy: " + ex.Message;
				HttpUploadAlarm.Triggered = true;
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
				using (var response = await MyHttpClient.GetAsync(url))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("WindGuru: " + response.StatusCode + ": " + responseBodyAsText);
					if (response.StatusCode != HttpStatusCode.OK)
					{
						LogMessage("WindGuru: ERROR - " + response.StatusCode + ": " + responseBodyAsText);
						HttpUploadAlarm.LastError = "WindGuru: HTTP response - " + response.StatusCode;
						HttpUploadAlarm.Triggered = true;
					}
					else
					{
						HttpUploadAlarm.Triggered = false;
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("WindGuru: ERROR - " + ex.Message);
				HttpUploadAlarm.LastError = "WindGuru: " + ex.Message;
				HttpUploadAlarm.Triggered = true;
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
				using (var response = await MyHttpClient.GetAsync(url))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("AWEKAS Response code = " + response.StatusCode);
					LogDataMessage("AWEKAS: Response text = " + responseBodyAsText);

					if (response.StatusCode != HttpStatusCode.OK)
					{
						LogMessage($"AWEKAS: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
						HttpUploadAlarm.LastError = $"AWEKAS: HTTP Response code = {response.StatusCode}, body = {responseBodyAsText}";
						HttpUploadAlarm.Triggered = true;
						AWEKAS.Updating = false;
						return;
					}
					else
					{
						HttpUploadAlarm.Triggered = false;
					}

					AwekasResponse respJson;
					try
					{
						respJson = JsonSerializer.DeserializeFromString<AwekasResponse>(responseBodyAsText);
					}
					catch (Exception ex)
					{
						LogMessage("AWEKAS: Exception deserializing response = " + ex.Message);
						LogMessage($"AWEKAS: ERROR - Response body = {responseBodyAsText}");
						HttpUploadAlarm.LastError = "AWEKAS deserializing response: " + ex.Message;
						HttpUploadAlarm.Triggered = true;
						AWEKAS.Updating = false;
						return;
					}

					// Check the status response
					if (respJson.status == 2)
					{
						LogDebugMessage("AWEKAS: Data stored OK");
					}
					else if (respJson.status == 1)
					{
						LogMessage("AWEKAS: Data PARIALLY stored");
						// TODO: Check errors and disabled
					}
					else if (respJson.status == 0)  // Authentication error or rate limited
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
								LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 300 seconds due to authentication error");
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
							HttpUploadAlarm.LastError = "AWEKAS: Unknown error";
							HttpUploadAlarm.Triggered = true;
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
				HttpUploadAlarm.LastError = "AWEKAS: " + ex.Message;
				HttpUploadAlarm.Triggered = true;
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
				using (var response = await MyHttpClient.GetAsync(url))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					var msg = "";
					switch ((int) response.StatusCode)
					{
						case 200:
							msg = "Success";
							HttpUploadAlarm.Triggered = false;
							break;
						case 400:
							msg = "Bad request";
							HttpUploadAlarm.LastError = "WeatherCloud: " + msg;
							HttpUploadAlarm.Triggered = true;
							break;
						case 401:
							msg = "Incorrect WID or Key";
							HttpUploadAlarm.LastError = "WeatherCloud: " + msg;
							HttpUploadAlarm.Triggered = true;
							break;
						case 429:
							msg = "Too many requests";
							HttpUploadAlarm.LastError = "WeatherCloud: " + msg;
							HttpUploadAlarm.Triggered = true;
							break;
						case 500:
							msg = "Server error";
							HttpUploadAlarm.LastError = "WeatherCloud: " + msg;
							HttpUploadAlarm.Triggered = true;
							break;
						default:
							msg = "Unknown error";
							HttpUploadAlarm.LastError = "WeatherCloud: " + msg;
							HttpUploadAlarm.Triggered = true;
							break;
					}
					if ((int) response.StatusCode == 200)
						LogDebugMessage($"WeatherCloud: Response = {msg} ({response.StatusCode}): {responseBodyAsText}");
					else
						LogMessage($"WeatherCloud: ERROR - Response = {msg} ({response.StatusCode}): {responseBodyAsText}");
				}
			}
			catch (Exception ex)
			{
				LogMessage("WeatherCloud: ERROR - " + ex.Message);
				HttpUploadAlarm.LastError = "WeatherCloud: " + ex.Message;
				HttpUploadAlarm.Triggered = true;
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
				var data = new StringContent(jsonData, Encoding.UTF8, "application/json");
				using (var response = await MyHttpClient.PostAsync(url, data))
				{
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					var status = response.StatusCode == HttpStatusCode.NoContent ? "OK" : "Error";  // Returns a 204 response for OK!
					LogDebugMessage($"OpenWeatherMap: Response code = {status} - {response.StatusCode}");
					if (response.StatusCode != HttpStatusCode.NoContent)
					{
						LogMessage($"OpenWeatherMap: ERROR - Response code = {response.StatusCode}, Response data = {responseBodyAsText}");
						HttpUploadAlarm.LastError = $"OpenWeatherMap: HTTP response - {response.StatusCode}, Response data = {responseBodyAsText}";
						HttpUploadAlarm.Triggered = true;
					}
					else
					{
						HttpUploadAlarm.Triggered = false;
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("OpenWeatherMap: ERROR - " + ex.Message);
				HttpUploadAlarm.LastError = "OpenWeatherMap: " + ex.Message;
				HttpUploadAlarm.Triggered = true;
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
				using (var response = MyHttpClient.GetAsync(url).Result)
				{
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
				sb.Append($"\"altitude\":{(int) station.AltitudeM(Altitude)}}}");

				LogMessage($"OpenWeatherMap: Creating new station");
				LogMessage($"OpenWeatherMap: - {sb}");

				var data = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");

				using (var response = MyHttpClient.PostAsync(url, data).Result)
				{
					var responseBodyAsText = response.Content.ReadAsStringAsync().Result;
					var status = response.StatusCode == HttpStatusCode.Created ? "OK" : "Error";  // Returns a 201 response for OK
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
			var cycle = RealtimeCycleCounter++;
			var reconnecting = false;

			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"Realtime[{cycle}]: Not all data is ready, aborting process");
				return;
			}

			LogDebugMessage($"Realtime[{cycle}]: Start cycle");
			try
			{
				// Process any files
				if (RealtimeCopyInProgress || RealtimeFtpInProgress)
				{
					LogMessage($"Realtime[{cycle}]: Warning, a previous cycle is still processing local files. Skipping this interval.");
				}
				else
				{
					RealtimeCopyInProgress = true;
					CreateRealtimeFile(cycle);
					CreateRealtimeHTMLfiles(cycle);
					RealtimeCopyInProgress = false;

					if (FtpOptions.LocalCopyEnabled)
					{
						RealtimeLocalCopy(cycle);
					}

					if (FtpOptions.RealtimeEnabled && FtpOptions.Enabled && (!RealtimeFtpReconnecting || FtpOptions.FtpMode == FtpProtocols.PHP))
					{
						// Is a previous cycle still running?
						if (RealtimeFtpInProgress)
						{
							LogMessage($"Realtime[{cycle}]: Warning, a previous cycle is still trying to connect to upload server, skip count = {++realtimeFTPRetries}");
							// real time interval is in ms, if a session has been uploading for 3 minutes - abort it and reconnect
							if (realtimeFTPRetries * RealtimeInterval / 1000 > 3 * 60)
							{
								LogMessage($"Realtime[{cycle}]: Realtime has been in progress for more than 3 minutes, attempting to reconnect.");
								//RealtimeFTPConnectionTest(cycle);
								RealtimeFTPReconnect();
							}
							else
							{
								LogMessage($"Realtime[{cycle}]: No FTP attempted this cycle");
							}
						}
						else
						{
							// This only happens if the user enables realtime FTP after starting Cumulus
							if (FtpOptions.FtpMode == FtpProtocols.SFTP && (RealtimeSSH == null || !RealtimeSSH.ConnectionInfo.IsAuthenticated))
							{
								RealtimeFTPReconnect();
								reconnecting = true;
							}
							if ((FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS) && !RealtimeFTP.IsConnected)
							{
								RealtimeFTPReconnect();
								reconnecting = true;
							}

							if (!reconnecting || FtpOptions.FtpMode == FtpProtocols.PHP)
							{
								// Finally we can do some FTP!
								RealtimeFtpInProgress = true;

								try
								{
									RealtimeUpload(cycle);
									realtimeFTPRetries = 0;
								}
								catch (Exception ex)
								{
									LogMessage($"Realtime[{cycle}]: Error during realtime upload. Message = {ex.Message}");
								}
								RealtimeFtpInProgress = false;
							}
						}
					}

					if (!string.IsNullOrEmpty(RealtimeProgram))
					{
						try
						{
							var args = string.Empty;

							if (!string.IsNullOrEmpty(RealtimeParams))
							{
								var parser = new TokenParser(TokenParserOnToken);
								parser.InputText = RealtimeParams;
								args = parser.ToStringFromString();
							}
							LogDebugMessage($"Realtime[{cycle}]: Execute realtime program - {RealtimeProgram}, with parameters - {args}");
							Utils.RunExternalTask(RealtimeProgram, args, false);
						}
						catch(Exception ex)
						{
							LogDebugMessage($"Realtime[{cycle}]: Error in realtime program - {RealtimeProgram}. Error: {ex.Message}");
						}
					}
				}

				MySqlRealtimeFile(cycle, true);
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, $"Realtime[{cycle}]: Error during update");
				if (FtpOptions.RealtimeEnabled && FtpOptions.Enabled)
				{
					RealtimeFTPReconnect();
					RealtimeFtpInProgress = false;
				}
			}
			LogDebugMessage($"Realtime[{cycle}]: End cycle");
		}

		private async void RealtimeFTPReconnect()
		{
			if (FtpOptions.FtpMode == FtpProtocols.PHP)
				return;

			RealtimeFtpReconnecting = true;
			await Task.Run(() =>
			{
				bool connected;
				bool reinit;

				do
				{
					connected = false;
					reinit = false;

					// Try to disconnect cleanly first
					//TODO: Just bypassing this for now for FTP, if it works refactor to remove redundant code
					if (FtpOptions.FtpMode == FtpProtocols.SFTP)
					{
						try
						{
							LogMessage("RealtimeReconnect: Realtime ftp attempting disconnect");
							if (FtpOptions.FtpMode == FtpProtocols.SFTP && RealtimeSSH != null)
							{
								RealtimeSSH.Disconnect();
							}
							if (FtpOptions.FtpMode != FtpProtocols.SFTP && RealtimeFTP != null)
							{
								RealtimeFTP.Config.DisconnectWithQuit = false;
								RealtimeFTP.Disconnect();
							}
							LogMessage("RealtimeReconnect: Realtime ftp disconnected");
						}
						catch (ObjectDisposedException)
						{
							LogDebugMessage($"RealtimeReconnect: Error, connection is disposed");
						}
						catch (Exception ex)
						{
							LogDebugMessage($"RealtimeReconnect: Error disconnecting from server - {ex.Message}");
						}
						finally
						{
							if (FtpOptions.FtpMode != FtpProtocols.SFTP && RealtimeFTP != null)
								RealtimeFTP.Config.DisconnectWithQuit = true;
						}

						// Attempt a simple reconnect
						try
						{
							LogMessage("RealtimeReconnect: Realtime ftp attempting to reconnect");
							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								RealtimeSSH.Connect();
								connected = RealtimeSSH.ConnectionInfo.IsAuthenticated;
							}
							else
							{
								if (FtpOptions.AutoDetect)
								{
									RealtimeFTP.AutoConnect();
								}
								else
								{
									RealtimeFTP.Connect();
								}
								connected = RealtimeFTP.IsConnected;
							}
							LogMessage("RealtimeReconnect: Reconnected with server (we think)");
						}
						catch (ObjectDisposedException)
						{
							reinit = true;
							LogDebugMessage($"RealtimeReconnect: Error, connection is disposed");
						}
						catch (Exception ex)
						{
							reinit = true;
							LogDebugMessage($"RealtimeReconnect: Error reconnecting ftp server - {ex.Message}");
							if (ex.InnerException != null)
							{
								ex = Utils.GetOriginalException(ex);
								LogDebugMessage($"RealtimeReconnect: Base exception - {ex.Message}");
							}
						}
					}
					else
					{
						reinit = true;
					}

					// Simple reconnect failed - start again and reinitialise the connections
					// RealtimeXXXLogin() has its own error handling
					if (reinit)
					{
						LogMessage("RealtimeReconnect: Realtime ftp attempting to reinitialise the connection");
						if (FtpOptions.FtpMode == FtpProtocols.SFTP)
						{
							RealtimeSSHLogin();
							connected = RealtimeSSH.ConnectionInfo.IsAuthenticated;
						}
						else
						{
							RealtimeFTPLogin();
							connected = RealtimeFTP.IsConnected;
						}
						if (connected)
						{
							LogMessage("RealtimeReconnect: Realtime ftp connection reinitialised");
						}
						else
						{
							LogMessage("RealtimeReconnect: Realtime ftp connection failed to connect after reinitialisation");
						}
					}

					// We *think* we are connected, now try and do something!
					if (connected)
					{
						try
						{
							string pwd;
							LogMessage("RealtimeReconnect: Realtime ftp testing the connection");
							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								pwd = RealtimeSSH.WorkingDirectory;
								// Double check
								if (!RealtimeSSH.IsConnected)
								{
									connected = false;
								}
							}
							else
							{
								pwd = RealtimeFTP.GetWorkingDirectory();
								// Double check
								if (!RealtimeFTP.IsConnected)
								{
									connected = false;
								}
							}
							if (pwd.Length == 0)
							{
								connected = false;
								LogMessage("RealtimeReconnect: Realtime ftp connection test failed to get Present Working Directory");
							}
							else
							{
								LogMessage($"RealtimeReconnect: Realtime ftp connection test found Present Working Directory OK - [{pwd}]");
							}
						}
						catch (Exception ex)
						{
							LogDebugMessage($"RealtimeReconnect: Realtime ftp connection test Failed - {ex.Message}");

							if (ex.InnerException != null)
							{
								ex = Utils.GetOriginalException(ex);
								LogDebugMessage($"RealtimeReconnect: Base exception - {ex.Message}");
							}

							connected = false;
						}
					}

					if (!connected)
					{
						LogMessage("RealtimeReconnect: Sleeping for 20 seconds before trying again...");
						Thread.Sleep(20 * 1000);
					}
				} while (!connected);

				// OK we are reconnected, let the FTP recommence
				RealtimeFtpReconnecting = false;
				RealtimeFtpInProgress = false;
				realtimeFTPRetries = 0;
				RealtimeCopyInProgress = false;
				LogMessage("RealtimeReconnect: Realtime FTP now connected to server (tested)");
				LogMessage("RealtimeReconnect: Realtime FTP operations will be restarted");
			});
		}

		private void RealtimeLocalCopy(byte cycle)
		{
			var dstPath = "";
			var folderSep1 = Path.DirectorySeparatorChar.ToString();
			var folderSep2 = Path.AltDirectorySeparatorChar.ToString();


			if (FtpOptions.LocalCopyFolder.Length > 0)
			{
				dstPath = (FtpOptions.Directory.EndsWith(folderSep1) || FtpOptions.Directory.EndsWith(folderSep2) ? FtpOptions.LocalCopyFolder : FtpOptions.LocalCopyFolder + folderSep1);
			}

			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].Copy)
				{
					var dstFile = dstPath + RealtimeFiles[i].RemoteFileName;
					var srcFile = RealtimeFiles[i].LocalPath + RealtimeFiles[i].LocalFileName;

					try
					{
						LogDebugMessage($"RealtimeLocalCopy[{cycle}]: Copying - {RealtimeFiles[i].LocalFileName}");

						if (RealtimeFiles[i].Create)
						{
							File.Copy(srcFile, dstFile, true);
						}
						else
						{
							var text = String.Empty;

							if (RealtimeFiles[i].LocalFileName == "realtime.txt")
							{
								text = CreateRealtimeFileString(cycle);
							}
							else if (RealtimeFiles[i].LocalFileName == "realtimegauges.txt")
							{
								text = ProcessTemplateFile2String(RealtimeFiles[i].TemplateFileName, false);
							}

							File.WriteAllText(dstFile, text);
						}
					}
					catch (Exception ex)
					{
						LogMessage($"RealtimeLocalCopy[{cycle}]: Error copying [{srcFile}] to [{dstFile}. Error = {ex.Message}");
					}
				}
			}
		}


		private async void RealtimeUpload(byte cycle)
		{
			var remotePath = "";
			var tasklist = new List<Task>();

			var taskCount = 0;
			var runningTaskCount = 0;


			if (FtpOptions.Directory.Length > 0)
			{
				remotePath = (FtpOptions.Directory.EndsWith("/") ? FtpOptions.Directory : FtpOptions.Directory + "/");
			}

			LogDebugMessage($"Realtime[{cycle}]: Real time files starting");

			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].FTP)
				{
					var remoteFile = remotePath + RealtimeFiles[i].RemoteFileName;
					var localFile = RealtimeFiles[i].LocalPath + RealtimeFiles[i].LocalFileName;

					LogFtpDebugMessage($"Realtime[{cycle}]: Uploading - {RealtimeFiles[i].RemoteFileName}");

					string data = string.Empty;

					if (FtpOptions.FtpMode != FtpProtocols.PHP)
					{
						// realtime file
						if (RealtimeFiles[i].LocalFileName == "realtime.txt")
						{
							data = CreateRealtimeFileString(cycle);
						}

						if (RealtimeFiles[i].LocalFileName == "realtimegauges.txt")
						{
							data = ProcessTemplateFile2String(RealtimeFiles[i].TemplateFileName, true, true);
						}

						using (var dataStream = GenerateStreamFromString(data))
						{
							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{

								_ = UploadStream(RealtimeSSH, remoteFile, dataStream, cycle);
							}
							else if (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS)
							{
								_ = UploadStream(RealtimeFTP, remoteFile, dataStream, cycle);
							}
						}
					}
					else // PHP
					{

						try
						{
#if DEBUG
							LogDebugMessage($"Realtime[{cycle}]: Real time file {RealtimeFiles[i].RemoteFileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
							await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
							LogDebugMessage($"Realtime[{cycle}]: Real time file {RealtimeFiles[i].RemoteFileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
							await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
#endif
						}
						catch (OperationCanceledException)
						{
							return;
						}

						Interlocked.Increment(ref taskCount);

						var idx = i;
						tasklist.Add(Task.Run(async () =>
						{
							// realtime file
							if (RealtimeFiles[idx].LocalFileName == "realtime.txt")
							{
								data = CreateRealtimeFileString(cycle);
							}

							if (RealtimeFiles[idx].LocalFileName == "realtimegauges.txt")
							{
								data = await ProcessTemplateFile2StringAsync(RealtimeFiles[idx].TemplateFileName, true, true);
							}

							try
							{
								_ = await UploadString(phpUploadHttpClient, false, string.Empty, data, RealtimeFiles[idx].RemoteFileName, cycle);
								// no realtime files are incremental, so no need to update LastDataTime
							}
							finally
							{
								uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
								LogDebugMessage($"Realtime[{cycle}]: Real time file {RealtimeFiles[idx].RemoteFileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
							}
							return true;
						}, cancellationToken));

						Interlocked.Increment(ref runningTaskCount);
					}
				}
			}

			if (FtpOptions.FtpMode != FtpProtocols.PHP)
			{
				LogDebugMessage($"Realtime[{cycle}]: Real time files complete");
			}

			// Extra files

			if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogDebugMessage($"Realtime[{cycle}]: Extra Files starting");

				ExtraFiles.Where(x => x.local.Length > 0 && x.remote.Length > 0 && x.realtime && x.FTP)
					.ToList()
					.ForEach(item =>
					{

						var uploadfile = item.local;
						var remotefile = item.remote;

						uploadfile = GetUploadFilename(uploadfile, DateTime.Now);

						if (!File.Exists(uploadfile))
						{
							LogMessage($"Realtime[{cycle}]: Warning, extra web file not found! - {uploadfile}");
							return;
						}

						try
						{
#if DEBUG
							LogDebugMessage($"Realtime[{cycle}]: Extra File {uploadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
							uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
							LogDebugMessage($"Realtime[{cycle}]: Extra File {uploadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
							uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif
						}
						catch (OperationCanceledException)
						{
							return;
						}

						Interlocked.Increment(ref taskCount);

						tasklist.Add(Task.Run(async () =>
						{
							try
							{
								if (cancellationToken.IsCancellationRequested)
									return false;

								remotefile = GetRemoteFileName(remotefile, DateTime.Now);

								// all checks OK, file needs to be uploaded
								LogDebugMessage($"Realtime[{cycle}]: Uploading extra web file {uploadfile} to {remotefile}");

								if (item.process)
								{
									var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);

									_ = await UploadString(phpUploadHttpClient, false, string.Empty, data, remotefile, cycle, item.binary, item.UTF8);
								}
								else
								{
									_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, cycle, item.binary, item.UTF8);
								}
								// no extra files are incremental for now, so no need to update LastDataTime
							}
							finally
							{
								uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
								LogDebugMessage($"Realtime[{cycle}]: Extra Web File {uploadfile} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
							}

							// no void return which cannot be tracked
							return true;
						}, cancellationToken));

						Interlocked.Increment(ref runningTaskCount);
					});

				// wait for all the tasks to start
				while (runningTaskCount < taskCount)
				{
					if (cancellationToken.IsCancellationRequested)
						return;

					await Task.Delay(10);
				}

				// wait for all the tasks to complete
				if (tasklist.Count > 0)
				{
					try
					{
						Task.WaitAll(tasklist.ToArray(), cancellationToken);
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"Realtime[{cycle}]: Eror waiting on upload tasks");
					}

				}
				LogDebugMessage($"Realtime[{cycle}]: Real time files complete, {tasklist.Count()} files uploaded");
				tasklist.Clear();
			}
			else
			{
				for (int i = 0; i < numextrafiles; i++)
				{
					var uploadfile = ExtraFiles[i].local;
					var remotefile = ExtraFiles[i].remote;

					if ((uploadfile.Length > 0) && (remotefile.Length > 0) && ExtraFiles[i].realtime && ExtraFiles[i].FTP)
					{
						uploadfile = GetUploadFilename(uploadfile, DateTime.Now);

						if (File.Exists(uploadfile))
						{
							remotefile = GetRemoteFileName(remotefile, DateTime.Now);

							// all checks OK, file needs to be uploaded
							LogFtpMessage("");
							LogFtpDebugMessage($"Realtime[{cycle}]: Uploading extra web file[{i}] {uploadfile} to {remotefile}");

							string data = string.Empty;

							if (ExtraFiles[i].process)
							{
								data = ProcessTemplateFile2String(uploadfile, false, ExtraFiles[i].UTF8);
							}

							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								if (ExtraFiles[i].process)
								{
									using (var strm = GenerateStreamFromString(data))
									{
										UploadStream(RealtimeSSH, remotefile, strm, cycle);
									}
								}
								else
								{
									UploadFile(RealtimeSSH, uploadfile, remotefile, cycle);
								}
								continue;
							}

							if (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS)
							{
								if (ExtraFiles[i].process)
								{
									using (var strm = GenerateStreamFromString(data))
									{
										UploadStream(RealtimeFTP, remotefile, strm, cycle);
									}
								}
								else
								{
									UploadFile(RealtimeFTP, uploadfile, remotefile, cycle);
								}
							}
						}
						else
						{
							LogMessage($"Realtime[{cycle}]: Warning, extra web file[{i}] not found! - {uploadfile}");
						}
					}
				}
			}
		}

		private void CreateRealtimeHTMLfiles(int cycle)
		{
			// file [0] is the realtime.txt - it does not need a template processing
			for (var i = 1; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].Create && !string.IsNullOrWhiteSpace(RealtimeFiles[i].TemplateFileName))
				{
					var destFile = RealtimeFiles[i].LocalPath + RealtimeFiles[i].LocalFileName;
					LogDebugMessage($"Realtime[{cycle}]: Creating realtime file - {RealtimeFiles[i].LocalFileName}");
					try
					{
						ProcessTemplateFile(RealtimeFiles[i].TemplateFileName, destFile, true);
					}
					catch (Exception ex)
					{
						LogDebugMessage($"Realtime[{cycle}]: Error creating file [{RealtimeFiles[i].LocalFileName}] to [{destFile}]. Error = {ex.Message}");
					}
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
						uploadfile = GetUploadFilename(uploadfile, DateTime.Now);

						if (File.Exists(uploadfile))
						{
							remotefile = GetRemoteFileName(remotefile, DateTime.Now);

							if (!ExtraFiles[i].FTP)
							{
								// just copy the file
								try
								{
									LogDebugMessage($"Realtime[{cycle}]: Copying extra file[{i}] {uploadfile} to {remotefile}");
									if (ExtraFiles[i].process)
									{
										ProcessTemplateFile(uploadfile, remotefile, false);
									}
									else
									{
										File.Copy(uploadfile, remotefile, true);
									}
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
			double[] moonriseset = MoonriseMoonset.MoonRise(now.Year, now.Month, now.Day, TimeZone.CurrentTimeZone.GetUtcOffset(now).TotalHours, (double)Latitude, (double)Longitude);
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
				MoonPhaseString = FullMoon;
			else if ((MoonPercent < -55) && (MoonPercent > -99.6))
				MoonPhaseString = WaningGibbous;
			else if ((MoonPercent <= -45) && (MoonPercent >= -55))
				MoonPhaseString = LastQuarter;
			else if ((MoonPercent > -45) && (MoonPercent < -0.4))
				MoonPhaseString = WaningCrescent;
			else
				MoonPhaseString = NewMoon;
			*/

			// Use Phase Angle to determine string - it's linear unlike Illuminated Percentage
			// New  = 186 - 180 - 174
			// 1st  =  96 -  90 -  84
			// Full =   6 -   0 - 354
			// 3rd  = 276 - 270 - 264
			if (MoonPhaseAngle < 174 && MoonPhaseAngle > 96)
				MoonPhaseString = Trans.WaxingCrescent;
			else if (MoonPhaseAngle <= 96 && MoonPhaseAngle >= 84)
				MoonPhaseString = Trans.FirstQuarter;
			else if (MoonPhaseAngle < 84 && MoonPhaseAngle > 6)
				MoonPhaseString = Trans.WaxingGibbous;
			else if (MoonPhaseAngle <= 6 || MoonPhaseAngle >= 354)
				MoonPhaseString = Trans.FullMoon;
			else if (MoonPhaseAngle < 354 && MoonPhaseAngle > 276)
				MoonPhaseString = Trans.WaningGibbous;
			else if (MoonPhaseAngle <= 276 && MoonPhaseAngle >= 264)
				MoonPhaseString = Trans.LastQuarter;
			else if (MoonPhaseAngle < 264 && MoonPhaseAngle > 186)
				MoonPhaseString = Trans.WaningCrescent;
			else
				MoonPhaseString = Trans.NewMoon;
		}

		internal void DoMoonImage()
		{
			if (MoonImage.Enabled)
			{
				LogDebugMessage("Generating new Moon image");
				var ret = MoonriseMoonset.CreateMoonImage(MoonPhaseAngle, (double)Latitude, MoonImage.Size);

				if (ret == "OK")
				{
					// set a flag to show file is ready for FTP
					MoonImage.ReadyToFtp = true;
					MoonImage.ReadyToCopy = true;
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
				sStage = NewMoon;
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
				sStage = FullMoon;
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
				sStage = NewMoon;
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

		public MoonImageOptionsClass MoonImage = new MoonImageOptionsClass();

		private void GetSunriseSunset(DateTime time, out DateTime sunrise, out DateTime sunset, out bool alwaysUp, out bool alwaysDown)
		{
			string rise = SunriseSunset.SunRise(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);
			string set = SunriseSunset.SunSet(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);

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
			string dawnStr = SunriseSunset.CivilTwilightEnds(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);
			string duskStr = SunriseSunset.CivilTwilightStarts(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);

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
					TomorrowDayLengthText = string.Format(Trans.thereWillBeMinSLessDaylightTomorrow, tomorrowmins, tomorrowsecs);
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
					TomorrowDayLengthText = string.Format(Trans.thereWillBeMinSMoreDaylightTomorrow, tomorrowmins, tomorrowsecs);
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

		public string RemoveOldDiagsFiles(string logType)
		{
			const int maxEntries = 12;

			var directory = "MXdiags" + DirectorySeparator;
			string filename = string.Empty;

			if (logType == "CMX")
			{
				List<string> fileEntries = new List<string>(Directory.GetFiles(directory).Where(f => System.Text.RegularExpressions.Regex.Match(f, @"[\\/]+\d{8}-\d{6}\.txt").Success));

				fileEntries.Sort();

				while (fileEntries.Count >= maxEntries)
				{
					File.Delete(fileEntries.First());
					fileEntries.RemoveAt(0);
				}

				filename = $"{directory}{DateTime.Now:yyyyMMdd-HHmmss}.txt";
			}
			else if (logType == "FTP")
			{
				List<string> fileEntries = new List<string>(Directory.GetFiles(directory).Where(f => System.Text.RegularExpressions.Regex.Match(f, @"[\\/]+FTP-\d{8}-\d{6}\.txt").Success));

				fileEntries.Sort();

				while (fileEntries.Count >= maxEntries)
				{
					File.Delete(fileEntries.First());
					fileEntries.RemoveAt(0);
				}

				filename=  $"{directory}FTP-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
			}

			return filename;
		}

		public void RotateLogFiles()
		{
			// cycle the MXdiags log file?
			var logfileSize = new FileInfo(loggingfile).Length;
			// if > 20 MB
			if (logfileSize > 20971520)
			{
				var oldfile = loggingfile;
				loggingfile = RemoveOldDiagsFiles("CMX");
				LogMessage("Rotating log file, new log file will be: " + loggingfile.Split(DirectorySeparator).Last());
				TextWriterTraceListener myTextListener = new TextWriterTraceListener(loggingfile, "MXlog");
				Trace.Listeners.Remove("MXlog");
				Trace.Listeners.Add(myTextListener);
				LogMessage("Rotated log file, old log file was: " + oldfile.Split(DirectorySeparator).Last());
			}

			// cycle the FTP log file
			if (FtpOptions.Logging && File.Exists(ftpLogfile))
			{
				logfileSize = new FileInfo(ftpLogfile).Length;
				// if > 20 MB
				if (logfileSize > 20971520)
				{
					var oldfile = ftpLogfile;
					ftpLogfile = RemoveOldDiagsFiles("FTP");
					LogFtpMessage("Rotating FTP log file, new log file will be: " + ftpLogfile.Split(DirectorySeparator).Last());
					CreateFtpLogFile(ftpLogfile);
					LogFtpMessage("Rotated FTP log file, old log file was: " + oldfile.Split(DirectorySeparator).Last());
				}
			}
		}

		public void CreateFtpLogFile(string filename)
		{
			if (FtpTraceListener != null)
			{
				FtpTraceListener.Close();
				FtpTraceListener.Dispose();
			}
			FtpTraceListener = new TextWriterTraceListener(ftpLogfile, "ftplog");
			LogFtpMessage("Create FTP log file: " + ftpLogfile.Split(DirectorySeparator).Last());
		}

		private void ReadIniFile()
		{
			var DavisBaudRates = new List<int> { 1200, 2400, 4800, 9600, 14400, 19200 };
			ImetOptions.BaudRates = new List<int> { 19200, 115200 };
			var rewriteRequired = false; // Do we need to re-save the ini file after migration processing or resetting options?

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

			ProgramOptions.StartupTask = ini.GetValue("Program", "StartupTask", "");
			ProgramOptions.StartupTaskParams = ini.GetValue("Program", "StartupTaskParams", "");
			ProgramOptions.StartupTaskWait = ini.GetValue("Program", "StartupTaskWait", false);

			ProgramOptions.ShutdownTask = ini.GetValue("Program", "ShutdownTask", "");
			ProgramOptions.ShutdownTaskParams = ini.GetValue("Program", "ShutdownTaskParams", "");

			ProgramOptions.DataStoppedExit = ini.GetValue("Program", "DataStoppedExit", false);
			ProgramOptions.DataStoppedMins = ini.GetValue("Program", "DataStoppedMins", 10);
			ProgramOptions.Culture.RemoveSpaceFromDateSeparator = ini.GetValue("Culture", "RemoveSpaceFromDateSeparator", false);
			// if the culture names match, then we apply the new date separator if change is enabled and it contains a space
			if (ProgramOptions.Culture.RemoveSpaceFromDateSeparator && CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator.Contains(" "))
			{
				// get the existing culture
				var newCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
				// change the date separator
				newCulture.DateTimeFormat.DateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator.Replace(" ", "");
				// set current thread culture
				Thread.CurrentThread.CurrentCulture = newCulture;
				// set the default culture for other threads
				CultureInfo.DefaultThreadCurrentCulture = newCulture;
			}

			ProgramOptions.TimeFormat = ini.GetValue("Program", "TimeFormat", "t");
			if (ProgramOptions.TimeFormat == "t")
				ProgramOptions.TimeFormatLong = "T";
			else if (ProgramOptions.TimeFormat == "h:mm tt")
				ProgramOptions.TimeFormatLong = "h:mm:ss tt";
			else
				ProgramOptions.TimeFormatLong = "HH:mm:ss";

			ProgramOptions.WarnMultiple = ini.GetValue("Station", "WarnMultiple", true);
			ProgramOptions.ListWebTags = ini.GetValue("Station", "ListWebTags", false);

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
			if (StationType == StationTypes.VantagePro && DavisOptions.UseLoop2 == true)
			{
				DavisOptions.UseLoop2 = false;
				rewriteRequired = true;
			}
			DavisOptions.BaudRate = ini.GetValue("Station", "DavisBaudRate", 19200);
			// Check we have a valid value
			if (!DavisBaudRates.Contains(DavisOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Error, the value for DavisBaudRate in the ini file " + DavisOptions.BaudRate + " is not valid, using default 19200.");
				DavisOptions.BaudRate = 19200;
				rewriteRequired = true;
			}
			DavisOptions.ForceVPBarUpdate = ini.GetValue("Station", "ForceVPBarUpdate", false);
			//DavisUseDLLBarCalData = ini.GetValue("Station", "DavisUseDLLBarCalData", false);
			//DavisCalcAltPress = ini.GetValue("Station", "DavisCalcAltPress", true);
			//DavisConsoleHighGust = ini.GetValue("Station", "DavisConsoleHighGust", false);
			DavisOptions.RainGaugeType = ini.GetValue("Station", "VPrainGaugeType", -1);
			if (DavisOptions.RainGaugeType > 3)
			{
				DavisOptions.RainGaugeType = -1;
				rewriteRequired = true;
			}
			DavisOptions.ConnectionType = ini.GetValue("Station", "VP2ConnectionType", VP2SERIALCONNECTION);
			DavisOptions.TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222);
			DavisOptions.IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");

			WeatherFlowOptions.WFDeviceId = ini.GetValue("Station", "WeatherFlowDeviceId", 0);
			WeatherFlowOptions.WFTcpPort = ini.GetValue("Station", "WeatherFlowTcpPort", 50222);
			WeatherFlowOptions.WFToken = ini.GetValue("Station", "WeatherFlowToken", "api token");
			WeatherFlowOptions.WFDaysHist = ini.GetValue("Station", "WeatherFlowDaysHist", 0);

			//VPClosedownTime = ini.GetValue("Station", "VPClosedownTime", 99999999);
			//VP2SleepInterval = ini.GetValue("Station", "VP2SleepInterval", 0);
			DavisOptions.PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0);

			Latitude = ini.GetValue("Station", "Latitude", (decimal) 0.0);
			if (Latitude > 90 || Latitude < -90)
			{
				Latitude = 0;
				LogMessage($"Error, invalid latitude value in Cumulus.ini [{Latitude}], defaulting to zero.");
				rewriteRequired = true;
			}
			Longitude = ini.GetValue("Station", "Longitude", (decimal) 0.0);
			if (Longitude > 180 || Longitude < -180)
			{
				Longitude = 0;
				LogMessage($"Error, invalid longitude value in Cumulus.ini [{Longitude}], defaulting to zero.");
				rewriteRequired = true;
			}

			LatTxt = ini.GetValue("Station", "LatTxt", "");
			LatTxt = LatTxt.Replace(" ", "&nbsp;");
			LatTxt = LatTxt.Replace("°", "&#39;");
			LonTxt = ini.GetValue("Station", "LonTxt", "");
			LonTxt = LonTxt.Replace(" ", "&nbsp;");
			LonTxt = LonTxt.Replace("°", "&#39;");

			Altitude = ini.GetValue("Station", "Altitude", 0.0);
			AltitudeInFeet = ini.GetValue("Station", "AltitudeInFeet", false);

			StationOptions.Humidity98Fix = ini.GetValue("Station", "Humidity98Fix", false);
			StationOptions.CalcuateAverageWindSpeed = ini.GetValue("Station", "Wind10MinAverage", false);
			StationOptions.UseSpeedForAvgCalc = ini.GetValue("Station", "UseSpeedForAvgCalc", false);
			StationOptions.UseSpeedForLatest = ini.GetValue("Station", "UseSpeedForLatest", false);
			StationOptions.UseRainForIsRaining = ini.GetValue("Station", "UseRainForIsRaining", 1);  // 0=station, 1=rain sensor, 2=haptic sensor
			StationOptions.LeafWetnessIsRainingIdx = ini.GetValue("Station", "LeafWetnessIsRainingIdx", -1);
			StationOptions.LeafWetnessIsRainingThrsh = ini.GetValue("Station", "LeafWetnessIsRainingVal", 0.0);

			StationOptions.AvgBearingMinutes = ini.GetValue("Station", "AvgBearingMinutes", 10);
			if (StationOptions.AvgBearingMinutes > 120)
			{
				StationOptions.AvgBearingMinutes = 120;
				rewriteRequired = true;
			}
			if (StationOptions.AvgBearingMinutes == 0)
			{
				StationOptions.AvgBearingMinutes = 1;
				rewriteRequired = true;
			}

			AvgBearingTime = new TimeSpan(StationOptions.AvgBearingMinutes / 60, StationOptions.AvgBearingMinutes % 60, 0);

			StationOptions.AvgSpeedMinutes = ini.GetValue("Station", "AvgSpeedMinutes", 10);
			if (StationOptions.AvgSpeedMinutes > 120)
			{
				StationOptions.AvgSpeedMinutes = 120;
				rewriteRequired = true;
			}
			if (StationOptions.AvgSpeedMinutes == 0)
			{
				StationOptions.AvgSpeedMinutes = 1;
				rewriteRequired = true;
			}

			AvgSpeedTime = new TimeSpan(StationOptions.AvgSpeedMinutes / 60, StationOptions.AvgSpeedMinutes % 60, 0);

			LogMessage("AvgSpdMins=" + StationOptions.AvgSpeedMinutes + " AvgSpdTime=" + AvgSpeedTime.ToString());

			StationOptions.PeakGustMinutes = ini.GetValue("Station", "PeakGustMinutes", 10);
			if (StationOptions.PeakGustMinutes > 120)
			{
				StationOptions.PeakGustMinutes = 120;
				rewriteRequired = true;
			}

			if (StationOptions.PeakGustMinutes == 0)
			{
				StationOptions.PeakGustMinutes = 1;
				rewriteRequired = true;
			}

			PeakGustTime = new TimeSpan(StationOptions.PeakGustMinutes / 60, StationOptions.PeakGustMinutes % 60, 0);

			StationOptions.NoSensorCheck = ini.GetValue("Station", "NoSensorCheck", false);

			StationOptions.CalculatedDP = ini.GetValue("Station", "CalculatedDP", false);
			StationOptions.CalculatedWC = ini.GetValue("Station", "CalculatedWC", false);
			StationOptions.CalculatedET = ini.GetValue("Station", "CalculatedET", false);
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
				rewriteRequired = true;
			}

			FineOffsetOptions.SyncReads = ini.GetValue("Station", "SyncFOReads", true);
			FineOffsetOptions.ReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3);
			FineOffsetOptions.ReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150);
			FineOffsetOptions.SetLoggerInterval = ini.GetValue("Station", "FineOffsetSetLoggerInterval", false);
			FineOffsetOptions.VendorID = ini.GetValue("Station", "VendorID", -1);
			FineOffsetOptions.ProductID = ini.GetValue("Station", "ProductID", -1);


			Units.Wind = ini.GetValue("Station", "WindUnit", 2);
			Units.Press = ini.GetValue("Station", "PressureUnit", 1);

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
			PressDPlaces = ini.GetValue("Station", "PressDecimals", PressDPlaces);
			RainDPlaces = ini.GetValue("Station", "RainDecimals", RainDPlaces);
			TempDPlaces = ini.GetValue("Station", "TempDecimals", TempDPlaces);
			UVDPlaces = ini.GetValue("Station", "UVDecimals", UVDPlaces);
			AirQualityDPlaces = ini.GetValue("Station", "AirQualityDecimals", AirQualityDPlaces);

			if (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2)
			{
				// Use one more DP for Davis stations
				if (DavisOptions.IncrementPressureDP)
				{
					++PressDPlaces;
				}
			}


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
			Spike.InTempDiff = ini.GetValue("Station", "EWinTempdiff", 999.0);
			Spike.InHumDiff = ini.GetValue("Station", "EWinHumiditydiff", 999.0);

			LCMaxWind = ini.GetValue("Station", "LCMaxWind", 9999);

			if (ini.ValueExists("Station", "StartDate"))
			{
				var RecordsBeganDate = ini.GetValue("Station", "StartDate", DateTime.Now.ToLongDateString());
				try
				{
					RecordsBeganDateTime = DateTime.Parse(RecordsBeganDate);
					rewriteRequired = true;
				}
				catch (Exception ex)
				{
					LogErrorMessage($"Error parsing the RecordsBegan date {RecordsBeganDate}: {ex.Message}");
				}
			}
			else
			{
				var RecordsBeganDate = ini.GetValue("Station", "StartDateIso", DateTime.Now.ToString("yyyy-MM-dd"));
				RecordsBeganDateTime = DateTime.ParseExact(RecordsBeganDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
			}

			LogMessage($"Cumulus start date Parsed: {RecordsBeganDateTime:yyyy-MM-dd}" );

			ImetOptions.WaitTime = ini.GetValue("Station", "ImetWaitTime", 500);
			ImetOptions.ReadDelay = ini.GetValue("Station", "ImetReadDelay", 500);
			ImetOptions.UpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);
			ImetOptions.BaudRate = ini.GetValue("Station", "ImetBaudRate", 19200);
			// Check we have a valid value
			if (!ImetOptions.BaudRates.Contains(ImetOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Error, the value for ImetOptions.ImetBaudRate in the ini file " + ImetOptions.BaudRate + " is not valid, using default 19200.");
				ImetOptions.BaudRate = 19200;
				rewriteRequired = true;
			}

			UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
			UseCumulusForecast = ini.GetValue("Station", "UseCumulusForecast", false);
			HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
			StationOptions.UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
			//UseWindChillCutoff = ini.GetValue("Station", "UseWindChillCutoff", false);
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
			{
				RainSeasonStart = 1;
				rewriteRequired = true;
			}
			ChillHourSeasonStart = ini.GetValue("Station", "ChillHourSeasonStart", Latitude >= 0 ? 10 : 4);
			if (ChillHourSeasonStart < 1 || ChillHourSeasonStart > 12)
			{
				ChillHourSeasonStart = 1;
				rewriteRequired = true;
			}
			ChillHourThreshold = ini.GetValue("Station", "ChillHourThreshold", -999.0);
			if (ChillHourThreshold < -998)
			{
				ChillHourThreshold = Units.Temp == 0 ? 7 : 45;
				rewriteRequired = true;
			}

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

			if (FCPressureThreshold < 0)
			{
				FCPressureThreshold = Units.Press == 2 ? 0.00295333727 : 0.1;
			}

			//special_logging = ini.GetValue("Station", "SpecialLog", false);
			//solar_logging = ini.GetValue("Station", "SolarLog", false);


			//RTdisconnectcount = ini.GetValue("Station", "RTdisconnectcount", 0);

			WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0);
			WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1);

			// WeatherLink Live device settings
			WllApiKey = ini.GetValue("WLL", "WLv2ApiKey", "");
			WllApiSecret = ini.GetValue("WLL", "WLv2ApiSecret", "");
			WllStationId = ini.GetValue("WLL", "WLStationId", -1);
			//if (WllStationId == "-1") WllStationId = "";
			WllTriggerDataStoppedOnBroadcast = ini.GetValue("WLL", "DataStoppedOnBroadcast", true);
			WLLAutoUpdateIpAddress = ini.GetValue("WLL", "AutoUpdateIpAddress", true);
			WllBroadcastDuration = ini.GetValue("WLL", "BroadcastDuration", WllBroadcastDuration);
			WllBroadcastPort = ini.GetValue("WLL", "BroadcastPort", WllBroadcastPort);
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
			for (int i = 1; i <= 8; i++)
			{
				WllExtraTempTx[i] = ini.GetValue("WLL", "ExtraTempTxId" + i, 0);
				WllExtraHumTx[i] = ini.GetValue("WLL", "ExtraHumOnTxId" + i, false);
			}

			// GW1000 settings
			Gw1000IpAddress = ini.GetValue("GW1000", "IPAddress", "0.0.0.0");
			Gw1000MacAddress = ini.GetValue("GW1000", "MACAddress", "");
			Gw1000AutoUpdateIpAddress = ini.GetValue("GW1000", "AutoUpdateIpAddress", true);
			Gw1000PrimaryTHSensor = ini.GetValue("GW1000", "PrimaryTHSensor", 0);  // 0=default, 1-8=extra t/h sensor number
			Gw1000PrimaryRainSensor = ini.GetValue("GW1000", "PrimaryRainSensor", 0); //0=main station (tipping bucket) 1=piezo
			EcowittExtraEnabled = ini.GetValue("GW1000", "ExtraSensorDataEnabled", false);
			EcowittExtraUseSolar = ini.GetValue("GW1000", "ExtraSensorUseSolar", true);
			EcowittExtraUseUv = ini.GetValue("GW1000", "ExtraSensorUseUv", true);
			EcowittExtraUseTempHum = ini.GetValue("GW1000", "ExtraSensorUseTempHum", true);
			EcowittExtraUseSoilTemp = ini.GetValue("GW1000", "ExtraSensorUseSoilTemp", true);
			EcowittExtraUseSoilMoist = ini.GetValue("GW1000", "ExtraSensorUseSoilMoist", true);
			EcowittExtraUseLeafWet = ini.GetValue("GW1000", "ExtraSensorUseLeafWet", true);
			EcowittExtraUseUserTemp = ini.GetValue("GW1000", "ExtraSensorUseUserTemp", true);
			EcowittExtraUseAQI = ini.GetValue("GW1000", "ExtraSensorUseAQI", true);
			EcowittExtraUseCo2 = ini.GetValue("GW1000", "ExtraSensorUseCo2", true);
			EcowittExtraUseLightning = ini.GetValue("GW1000", "ExtraSensorUseLightning", true);
			EcowittExtraUseLeak = ini.GetValue("GW1000", "ExtraSensorUseLeak", true);
			EcowittSetCustomServer = ini.GetValue("GW1000", "SetCustomServer", false);
			EcowittGatewayAddr = ini.GetValue("GW1000", "EcowittGwAddr", "0.0.0.0");
			var localIp = Utils.GetIpWithDefaultGateway();
			EcowittLocalAddr = ini.GetValue("GW1000", "EcowittLocalAddr", localIp.ToString());
			EcowittCustomInterval = ini.GetValue("GW1000", "EcowittCustomInterval", 16);
			//
			EcowittExtraSetCustomServer = ini.GetValue("GW1000", "ExtraSetCustomServer", false);
			EcowittExtraGatewayAddr = ini.GetValue("GW1000", "EcowittExtraGwAddr", "0.0.0.0");
			EcowittExtraLocalAddr = ini.GetValue("GW1000", "EcowittExtraLocalAddr", localIp.ToString());
			EcowittExtraCustomInterval = ini.GetValue("GW1000", "EcowittExtraCustomInterval", 16);
			// api
			EcowittApplicationKey = ini.GetValue("GW1000", "EcowittAppKey", "");
			EcowittUserApiKey = ini.GetValue("GW1000", "EcowittUserKey", "");
			EcowittMacAddress = ini.GetValue("GW1000", "EcowittMacAddress", "");
			if (string.IsNullOrEmpty(EcowittMacAddress) && !string.IsNullOrEmpty(Gw1000MacAddress))
			{
				EcowittMacAddress = Gw1000MacAddress;
			}
			// WN34 sensor mapping
			for (int i = 1; i <= 8; i++)
			{
				EcowittMapWN34[i] = ini.GetValue("GW1000", "WN34MapChan" + i, 0);
			}
			// forwarders
			for (int i = 0; i < EcowittForwarders.Length; i++)
			{
				EcowittForwarders[i] = ini.GetValue("GW1000", "Forwarder" + i, "");
			}

			// Ambient settings
			AmbientExtraEnabled = ini.GetValue("Ambient", "ExtraSensorDataEnabled", false);
			AmbientExtraUseSolar = ini.GetValue("Ambient", "ExtraSensorUseSolar", true);
			AmbientExtraUseUv = ini.GetValue("Ambient", "ExtraSensorUseUv", true);
			AmbientExtraUseTempHum = ini.GetValue("Ambient", "ExtraSensorUseTempHum", true);
			AmbientExtraUseSoilTemp = ini.GetValue("Ambient", "ExtraSensorUseSoilTemp", true);
			AmbientExtraUseSoilMoist = ini.GetValue("Ambient", "ExtraSensorUseSoilMoist", true);
			//AmbientExtraUseLeafWet = ini.GetValue("Ambient", "ExtraSensorUseLeafWet", true);
			AmbientExtraUseAQI = ini.GetValue("Ambient", "ExtraSensorUseAQI", true);
			AmbientExtraUseCo2 = ini.GetValue("Ambient", "ExtraSensorUseCo2", true);
			AmbientExtraUseLightning = ini.GetValue("Ambient", "ExtraSensorUseLightning", true);
			AmbientExtraUseLeak = ini.GetValue("Ambient", "ExtraSensorUseLeak", true);

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
				rewriteRequired = true;
			}
			AirLinkApiKey = ini.GetValue("AirLink", "WLv2ApiKey", "");
			AirLinkApiSecret = ini.GetValue("AirLink", "WLv2ApiSecret", "");
			AirLinkAutoUpdateIpAddress = ini.GetValue("AirLink", "AutoUpdateIpAddress", true);
			AirLinkInEnabled = ini.GetValue("AirLink", "In-Enabled", false);
			AirLinkInIPAddr = ini.GetValue("AirLink", "In-IPAddress", "0.0.0.0");
			AirLinkInStationId = ini.GetValue("AirLink", "In-WLStationId", -1);
			if (AirLinkInStationId == -1 && AirLinkIsNode)
			{
				AirLinkInStationId = WllStationId;
				rewriteRequired = true;
			}
			AirLinkInHostName = ini.GetValue("AirLink", "In-Hostname", "");

			AirLinkOutEnabled = ini.GetValue("AirLink", "Out-Enabled", false);
			AirLinkOutIPAddr = ini.GetValue("AirLink", "Out-IPAddress", "0.0.0.0");
			AirLinkOutStationId = ini.GetValue("AirLink", "Out-WLStationId", -1);
			if (AirLinkOutStationId == -1 && AirLinkIsNode)
			{
				AirLinkOutStationId = WllStationId;
				rewriteRequired = true;
			}
			AirLinkOutHostName = ini.GetValue("AirLink", "Out-Hostname", "");

			airQualityIndex = ini.GetValue("AirLink", "AQIformula", 0);

			FtpOptions.Enabled = ini.GetValue("FTP site", "Enabled", true);
			FtpOptions.Hostname = ini.GetValue("FTP site", "Host", "");
			FtpOptions.Port = ini.GetValue("FTP site", "Port", 21);
			FtpOptions.Username = ini.GetValue("FTP site", "Username", "");
			FtpOptions.Password = ini.GetValue("FTP site", "Password", "");
			FtpOptions.Directory = ini.GetValue("FTP site", "Directory", "");
			if (FtpOptions.Hostname == "" && FtpOptions.Enabled)
			{
				FtpOptions.Enabled = false;
				rewriteRequired = true;
			}

			FtpOptions.AutoDetect = ini.GetValue("FTP site", "ConnectionAutoDetect", false);
			FtpOptions.IgnoreCertErrors = ini.GetValue("FTP site", "IgnoreCertErrors", false);
			FtpOptions.ActiveMode = ini.GetValue("FTP site", "ActiveFTP", false);
			FtpOptions.FtpMode = (FtpProtocols)ini.GetValue("FTP site", "Sslftp", 0);
			// BUILD 3092 - added alternate SFTP authentication options
			FtpOptions.SshAuthen = ini.GetValue("FTP site", "SshFtpAuthentication", "password");
			if (!sshAuthenticationVals.Contains(FtpOptions.SshAuthen))
			{
				FtpOptions.SshAuthen = "password";
				LogMessage($"Error, invalid SshFtpAuthentication value in Cumulus.ini [{FtpOptions.SshAuthen}], defaulting to Password.");
				rewriteRequired = true;
			}
			FtpOptions.SshPskFile = ini.GetValue("FTP site", "SshFtpPskFile", "");
			if (FtpOptions.SshPskFile.Length > 0 && (FtpOptions.SshAuthen == "psk" || FtpOptions.SshAuthen == "password_psk") && !File.Exists(FtpOptions.SshPskFile))
			{
				FtpOptions.SshPskFile = "";
				LogMessage($"Error, file name specified by SshFtpPskFile value in Cumulus.ini does not exist [{FtpOptions.SshPskFile}].");
				rewriteRequired = true;
			}
			FtpOptions.DisableEPSV = ini.GetValue("FTP site", "DisableEPSV", false);
			FtpOptions.DisableExplicit = ini.GetValue("FTP site", "DisableFtpsExplicit", false);
			FtpOptions.Logging = ini.GetValue("FTP site", "FTPlogging", false);
			RealtimeIntervalEnabled = ini.GetValue("FTP site", "EnableRealtime", false);
			FtpOptions.RealtimeEnabled = ini.GetValue("FTP site", "RealtimeFTPEnabled", false);

			// Local Copy Options
			FtpOptions.LocalCopyEnabled = ini.GetValue("FTP site", "EnableLocalCopy", false);
			//FtpOptions.LocalCopyRealtimeEnabled = ini.GetValue("FTP site", "EnableRealtimeLocalCopy", false);
			FtpOptions.LocalCopyFolder = ini.GetValue("FTP site", "LocalCopyFolder", "");
			var sep1 = Path.DirectorySeparatorChar.ToString();
			var sep2 = Path.AltDirectorySeparatorChar.ToString();
			if (FtpOptions.LocalCopyFolder.Length > 1 &&
				!(FtpOptions.LocalCopyFolder.EndsWith(sep1) || FtpOptions.LocalCopyFolder.EndsWith(sep2))
				)
			{
				FtpOptions.LocalCopyFolder += sep1;
				rewriteRequired = true;
			}

			// PHP upload options
			FtpOptions.PhpUrl = ini.GetValue("FTP site", "PHP-URL", "");
			FtpOptions.PhpSecret = ini.GetValue("FTP site", "PHP-Secret", "");
			if (FtpOptions.PhpSecret == string.Empty)
				FtpOptions.PhpSecret = Guid.NewGuid().ToString();
			FtpOptions.PhpIgnoreCertErrors = ini.GetValue("FTP site", "PHP-IgnoreCertErrors", false);
			FtpOptions.MaxConcurrentUploads = ini.GetValue("FTP site", "MaxConcurrentUploads", boolWindows ? 4 : 1);

			MoonImage.Ftp = ini.GetValue("FTP site", "IncludeMoonImage", false);
			MoonImage.Copy = ini.GetValue("FTP site", "CopyMoonImage", false);


			RealtimeFiles[0].Create = ini.GetValue("FTP site", "RealtimeTxtCreate", false);
			RealtimeFiles[0].FTP = ini.GetValue("FTP site", "RealtimeTxtFTP", false);
			RealtimeFiles[0].Copy = ini.GetValue("FTP site", "RealtimeTxtCopy", false);
			RealtimeFiles[1].Create = ini.GetValue("FTP site", "RealtimeGaugesTxtCreate", false);
			RealtimeFiles[1].FTP = ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);
			RealtimeFiles[1].Copy = ini.GetValue("FTP site", "RealtimeGaugesTxtCopy", false);

			RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000);
			if (RealtimeInterval < 1)
			{
				RealtimeInterval = 1;
				rewriteRequired = true;
			}
			//RealtimeTimer.Change(0,RealtimeInterval);

			WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);  // Deprecated, to be remove at some future date
																			// Have to allow for upgrade, set interval enabled to old WebAutoUpdate
			WebIntervalEnabled = ini.GetValue("FTP site", "IntervalEnabled", WebAutoUpdate);
			FtpOptions.IntervalEnabled = ini.GetValue("FTP site", "IntervalFtpEnabled", WebAutoUpdate); ;

			UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", DefaultWebUpdateInterval);
			if (UpdateInterval < 1)
			{
				UpdateInterval = 1;
				rewriteRequired = true;
			}
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
				var keyNameCopy = "Copy-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				StdWebFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeStandardFiles);
				StdWebFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeStandardFiles);
				StdWebFiles[i].Copy = ini.GetValue("FTP site", keyNameCopy, IncludeStandardFiles);
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
				var keyNameCopy = "Copy-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				GraphDataFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeGraphDataFiles);
				GraphDataFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeGraphDataFiles);
				GraphDataFiles[i].Copy = ini.GetValue("FTP site", keyNameCopy, IncludeGraphDataFiles);
			}
			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				GraphDataEodFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeGraphDataFiles);
				GraphDataEodFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeGraphDataFiles);
				GraphDataEodFiles[i].Copy = ini.GetValue("FTP site", keyNameCopy, IncludeGraphDataFiles);
			}

			for (var i = 0; i < HttpFilesConfig.Length; i++)
			{
				HttpFilesConfig[i] = new HttpFileProps();
			}

			FTPRename = ini.GetValue("FTP site", "FTPRename", true);
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
				ExtraFiles[i].FTP = ini.GetValue("FTP site", "ExtraFTP" + i, false);
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
			RecentDataDays = (int)Math.Ceiling(Math.Max(7, GraphHours / 24.0));
			MoonImage.Enabled = ini.GetValue("Graphs", "MoonImageEnabled", false);
			MoonImage.Size = ini.GetValue("Graphs", "MoonImageSize", 100);
			if (MoonImage.Size < 10)
			{
				MoonImage.Size = 10;
				rewriteRequired = true;
			}
			MoonImage.FtpDest = ini.GetValue("Graphs", "MoonImageFtpDest", "images/moon.png");
			MoonImage.CopyDest = ini.GetValue("Graphs", "MoonImageCopyDest", FtpOptions.LocalCopyFolder + "images" + sep1 + "moon.png");
			GraphOptions.Visible.Temp.Val = ini.GetValue("Graphs", "TempVisible", 1);
			GraphOptions.Visible.InTemp.Val = ini.GetValue("Graphs", "InTempVisible", 1);
			GraphOptions.Visible.HeatIndex.Val = ini.GetValue("Graphs", "HIVisible", 1);
			GraphOptions.Visible.DewPoint.Val = ini.GetValue("Graphs", "DPVisible", 1);
			GraphOptions.Visible.WindChill.Val = ini.GetValue("Graphs", "WCVisible", 1);
			GraphOptions.Visible.AppTemp.Val = ini.GetValue("Graphs", "AppTempVisible", 1);
			GraphOptions.Visible.FeelsLike.Val = ini.GetValue("Graphs", "FeelsLikeVisible", 1);
			GraphOptions.Visible.Humidex.Val = ini.GetValue("Graphs", "HumidexVisible", 1);
			GraphOptions.Visible.InHum.Val = ini.GetValue("Graphs", "InHumVisible", 1);
			GraphOptions.Visible.OutHum.Val = ini.GetValue("Graphs", "OutHumVisible", 1);
			GraphOptions.Visible.UV.Val = ini.GetValue("Graphs", "UVVisible", 1);
			GraphOptions.Visible.Solar.Val = ini.GetValue("Graphs", "SolarVisible", 1);
			GraphOptions.Visible.Sunshine.Val = ini.GetValue("Graphs", "SunshineVisible", 1);
			GraphOptions.Visible.AvgTemp.Val = ini.GetValue("Graphs", "DailyAvgTempVisible", 1);
			GraphOptions.Visible.MaxTemp.Val = ini.GetValue("Graphs", "DailyMaxTempVisible", 1);
			GraphOptions.Visible.MinTemp.Val = ini.GetValue("Graphs", "DailyMinTempVisible", 1);
			GraphOptions.Visible.GrowingDegreeDays1.Val = ini.GetValue("Graphs", "GrowingDegreeDaysVisible1", 1);
			GraphOptions.Visible.GrowingDegreeDays2.Val = ini.GetValue("Graphs", "GrowingDegreeDaysVisible2", 1);
			GraphOptions.Visible.TempSum0.Val = ini.GetValue("Graphs", "TempSumVisible0", 1);
			GraphOptions.Visible.TempSum1.Val = ini.GetValue("Graphs", "TempSumVisible1", 1);
			GraphOptions.Visible.TempSum2.Val = ini.GetValue("Graphs", "TempSumVisible2", 1);
			GraphOptions.Visible.ExtraTemp.Vals = ini.GetValue("Graphs", "ExtraTempVisible", new int[10]);
			GraphOptions.Visible.ExtraHum.Vals = ini.GetValue("Graphs", "ExtraHumVisible", new int[10]);
			GraphOptions.Visible.ExtraDewPoint.Vals = ini.GetValue("Graphs", "ExtraDewPointVisible", new int[10]);
			GraphOptions.Visible.SoilTemp.Vals = ini.GetValue("Graphs", "SoilTempVisible", new int[16]);
			GraphOptions.Visible.SoilMoist.Vals = ini.GetValue("Graphs", "SoilMoistVisible", new int[16]);
			GraphOptions.Visible.UserTemp.Vals = ini.GetValue("Graphs", "UserTempVisible", new int[8]);
			GraphOptions.Visible.LeafWetness.Vals = ini.GetValue("Graphs", "LeafWetnessVisible", new int[8]);
			GraphOptions.Visible.AqSensor.Pm.Vals = ini.GetValue("Graphs", "Aq-PmVisible", new int[4]);
			GraphOptions.Visible.AqSensor.PmAvg.Vals = ini.GetValue("Graphs", "Aq-PmAvgVisible", new int[4]);
			GraphOptions.Visible.AqSensor.Temp.Vals = ini.GetValue("Graphs", "Aq-TempVisible", new int[4]);
			GraphOptions.Visible.AqSensor.Hum.Vals = ini.GetValue("Graphs", "Aq-HumVisible", new int[4]);
			GraphOptions.Visible.CO2Sensor.CO2.Val = ini.GetValue("Graphs", "CO2-CO2", 1);
			GraphOptions.Visible.CO2Sensor.CO2Avg.Val = ini.GetValue("Graphs", "CO2-CO2Avg", 1);
			GraphOptions.Visible.CO2Sensor.Pm25.Val = ini.GetValue("Graphs", "CO2-Pm25", 1);
			GraphOptions.Visible.CO2Sensor.Pm25Avg.Val = ini.GetValue("Graphs", "CO2-Pm25Avg", 1);
			GraphOptions.Visible.CO2Sensor.Pm10.Val = ini.GetValue("Graphs", "CO2-Pm10", 1);
			GraphOptions.Visible.CO2Sensor.Pm10Avg.Val = ini.GetValue("Graphs", "CO2-Pm10Avg", 1);
			GraphOptions.Visible.CO2Sensor.Temp.Val = ini.GetValue("Graphs", "CO2-Temp", 0);
			GraphOptions.Visible.CO2Sensor.Hum.Val = ini.GetValue("Graphs", "CO2-Hum", 0);

			GraphOptions.Colour.Temp = ini.GetValue("GraphColours", "TempColour", "#ff0000");
			GraphOptions.Colour.InTemp = ini.GetValue("GraphColours", "InTempColour", "#50b432");
			GraphOptions.Colour.HeatIndex= ini.GetValue("GraphColours", "HIColour", "#9161c9");
			GraphOptions.Colour.DewPoint = ini.GetValue("GraphColours", "DPColour", "#ffff00");
			GraphOptions.Colour.WindChill = ini.GetValue("GraphColours", "WCColour", "#ffa500");
			GraphOptions.Colour.AppTemp = ini.GetValue("GraphColours", "AppTempColour", "#00fffe");
			GraphOptions.Colour.FeelsLike = ini.GetValue("GraphColours", "FeelsLikeColour", "#00fffe");
			GraphOptions.Colour.Humidex = ini.GetValue("GraphColours", "HumidexColour", "#008000");
			GraphOptions.Colour.InHum = ini.GetValue("GraphColours", "InHumColour", "#008000");
			GraphOptions.Colour.OutHum = ini.GetValue("GraphColours", "OutHumColour", "#ff0000");
			GraphOptions.Colour.Press = ini.GetValue("GraphColours", "PressureColour", "#6495ed");
			GraphOptions.Colour.WindGust = ini.GetValue("GraphColours", "WindGustColour", "#ff0000");
			GraphOptions.Colour.WindAvg = ini.GetValue("GraphColours", "WindAvgColour", "#6495ed");
			GraphOptions.Colour.WindRun = ini.GetValue("GraphColours", "WindRunColour", "#3dd457");
			GraphOptions.Colour.WindBearing = ini.GetValue("GraphColours", "WindBearingColour", "#6495ed");
			GraphOptions.Colour.WindBearingAvg = ini.GetValue("GraphColours", "WindBearingAvgColour", "#ff0000");
			GraphOptions.Colour.Rainfall = ini.GetValue("GraphColours", "Rainfall", "#6495ed");
			GraphOptions.Colour.RainRate = ini.GetValue("GraphColours", "RainRate", "#ff0000");
			GraphOptions.Colour.UV = ini.GetValue("GraphColours", "UVColour", "#8a2be2");
			GraphOptions.Colour.Solar = ini.GetValue("GraphColours", "SolarColour", "#ff8c00");
			GraphOptions.Colour.SolarTheoretical = ini.GetValue("GraphColours", "SolarTheoreticalColour", "#6464ff");
			GraphOptions.Colour.Sunshine = ini.GetValue("GraphColours", "SunshineColour", "#ff8c00");
			GraphOptions.Colour.MaxTemp = ini.GetValue("GraphColours", "MaxTempColour", "#ff0000");
			GraphOptions.Colour.AvgTemp = ini.GetValue("GraphColours", "AvgTempColour", "#008000");
			GraphOptions.Colour.MinTemp = ini.GetValue("GraphColours", "MinTempColour", "#6495ed");
			GraphOptions.Colour.MaxPress = ini.GetValue("GraphColours", "MaxPressColour", "#6495ed");
			GraphOptions.Colour.MinPress = ini.GetValue("GraphColours", "MinPressColour", "#39ef74");
			GraphOptions.Colour.MaxOutHum = ini.GetValue("GraphColours", "MaxHumColour", "#6495ed");
			GraphOptions.Colour.MinOutHum = ini.GetValue("GraphColours", "MinHumColour", "#39ef74");
			GraphOptions.Colour.MaxHeatIndex = ini.GetValue("GraphColours", "MaxHIColour", "#ffa500");
			GraphOptions.Colour.MaxDew = ini.GetValue("GraphColours", "MaxDPColour", "#dada00");
			GraphOptions.Colour.MinDew = ini.GetValue("GraphColours", "MinDPColour", "#ffc0cb");
			GraphOptions.Colour.MaxFeels = ini.GetValue("GraphColours", "MaxFeelsLikeColour", "#00ffff");
			GraphOptions.Colour.MinFeels = ini.GetValue("GraphColours", "MinFeelsLikeColour", "#800080");
			GraphOptions.Colour.MaxApp = ini.GetValue("GraphColours", "MaxAppTempColour", "#808080");
			GraphOptions.Colour.MinApp = ini.GetValue("GraphColours", "MinAppTempColour", "#a52a2a");
			GraphOptions.Colour.MaxHumidex = ini.GetValue("GraphColours", "MaxHumidexColour", "#c7b72a");
			GraphOptions.Colour.Pm2p5 = ini.GetValue("GraphColours", "Pm2p5Colour", "#6495ed");
			GraphOptions.Colour.Pm10 = ini.GetValue("GraphColours", "Pm10Colour", "#008000");
			var colours16 = new List<string>(16) { "#ff0000", "#008000", "#0000ff", "#ffa500", "#dada00", "#ffc0cb", "#00ffff", "#800080", "#808080", "#a52a2a", "#c7b72a", "#7fffd4", "#adff2f", "#ff7f50", "#ff00ff", "#00b2ff" };
			var colours10 = colours16.Take(10).ToArray();
			var colours8 = colours16.Take(8).ToArray();
			var colours2 = colours16.Take(2).ToArray();
			GraphOptions.Colour.ExtraTemp = ini.GetValue("GraphColours", "ExtraTempColour", colours10);
			GraphOptions.Colour.ExtraHum = ini.GetValue("GraphColours", "ExtraHumColour", colours10);
			GraphOptions.Colour.ExtraDewPoint = ini.GetValue("GraphColours", "ExtraDewPointColour", colours10);
			GraphOptions.Colour.SoilTemp = ini.GetValue("GraphColours", "SoilTempColour", colours16.ToArray());
			GraphOptions.Colour.SoilMoist = ini.GetValue("GraphColours", "SoilMoistColour", colours16.ToArray());
			GraphOptions.Colour.LeafWetness = ini.GetValue("GraphColours", "LeafWetness", colours2);
			GraphOptions.Colour.UserTemp = ini.GetValue("GraphColours", "UserTempColour", colours8);
			GraphOptions.Colour.CO2Sensor.CO2 = ini.GetValue("GraphColours", "CO2-CO2Colour", "#dc143c");
			GraphOptions.Colour.CO2Sensor.CO2Avg = ini.GetValue("GraphColours", "CO2-CO2AvgColour", "#8b0000");
			GraphOptions.Colour.CO2Sensor.Pm25 = ini.GetValue("GraphColours", "CO2-Pm25Colour", "#00bfff");
			GraphOptions.Colour.CO2Sensor.Pm25Avg = ini.GetValue("GraphColours", "CO2-Pm25AvgColour", "#1e90ff");
			GraphOptions.Colour.CO2Sensor.Pm10 = ini.GetValue("GraphColours", "CO2-Pm10Colour", "#d2691e");
			GraphOptions.Colour.CO2Sensor.Pm10Avg = ini.GetValue("GraphColours", "CO2-Pm10AvgColour", "#b8860b");
			GraphOptions.Colour.CO2Sensor.Temp = ini.GetValue("GraphColours", "CO2-TempColour", "#ff0000");
			GraphOptions.Colour.CO2Sensor.Hum = ini.GetValue("GraphColours", "CO2-HumColour", "#008000");


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
			Wund.SendExtraTemp1 = ini.GetValue("Wunderground", "SendExtraTemp1", 0);
			Wund.SendExtraTemp2 = ini.GetValue("Wunderground", "SendExtraTemp2", 0);
			Wund.SendExtraTemp3 = ini.GetValue("Wunderground", "SendExtraTemp3", 0);
			Wund.SendExtraTemp4 = ini.GetValue("Wunderground", "SendExtraTemp4", 0);
			Wund.SendAverage = ini.GetValue("Wunderground", "SendAverage", false);
			Wund.CatchUp = ini.GetValue("Wunderground", "CatchUp", true);

			Wund.SynchronisedUpdate = !Wund.RapidFireEnabled;

			Windy.ApiKey = ini.GetValue("Windy", "APIkey", "");
			Windy.StationIdx = ini.GetValue("Windy", "StationIdx", 0);
			Windy.Enabled = ini.GetValue("Windy", "Enabled", false);
			Windy.Interval = ini.GetValue("Windy", "Interval", Windy.DefaultInterval);
			if (Windy.Interval < 5)
			{
				Windy.Interval = 5;
				rewriteRequired = true;
			}
			//WindyHTTPLogging = ini.GetValue("Windy", "Logging", false);
			Windy.SendUV = ini.GetValue("Windy", "SendUV", false);
			Windy.SendSolar = ini.GetValue("Windy", "SendSolar", false);
			Windy.CatchUp = ini.GetValue("Windy", "CatchUp", false);

			AWEKAS.ID = ini.GetValue("Awekas", "User", "");
			AWEKAS.PW = ini.GetValue("Awekas", "Password", "");
			AWEKAS.Enabled = ini.GetValue("Awekas", "Enabled", false);
			AWEKAS.Interval = ini.GetValue("Awekas", "Interval", AWEKAS.DefaultInterval);
			if (AWEKAS.Interval < 15)
			{
				AWEKAS.Interval = 15;
				rewriteRequired = true;
			}
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
			if (WindGuru.Interval < 1)
			{
				WindGuru.Interval = 1;
				rewriteRequired = true;
			}
			WindGuru.SendRain = ini.GetValue("WindGuru", "SendRain", false);

			WCloud.ID = ini.GetValue("WeatherCloud", "Wid", "");
			WCloud.PW = ini.GetValue("WeatherCloud", "Key", "");
			WCloud.Enabled = ini.GetValue("WeatherCloud", "Enabled", false);
			WCloud.Interval = ini.GetValue("WeatherCloud", "Interval", WCloud.DefaultInterval);
			WCloud.SendUV = ini.GetValue("WeatherCloud", "SendUV", false);
			WCloud.SendSolar = ini.GetValue("WeatherCloud", "SendSR", false);
			WCloud.SendAirQuality = ini.GetValue("WeatherCloud", "SendAirQuality", false);
			WCloud.SendSoilMoisture = ini.GetValue("WeatherCloud", "SendSoilMoisture", false);
			WCloud.SoilMoistureSensor = ini.GetValue("WeatherCloud", "SoilMoistureSensor", 1);
			WCloud.SendLeafWetness = ini.GetValue("WeatherCloud", "SendLeafWetness", false);
			WCloud.LeafWetnessSensor = ini.GetValue("WeatherCloud", "LeafWetnessSensor", 1);

			//if HTTPLogging then
			//  MainForm.WUHTTP.IcsLogger = MainForm.HTTPlogger;

			PWS.ID = ini.GetValue("PWSweather", "ID", "");
			PWS.PW = ini.GetValue("PWSweather", "Password", "");
			PWS.Enabled = ini.GetValue("PWSweather", "Enabled", false);
			PWS.Interval = ini.GetValue("PWSweather", "Interval", PWS.DefaultInterval);
			if (PWS.Interval < 1)
			{
				PWS.Interval = 1;
				rewriteRequired = true;
			}
			PWS.SendUV = ini.GetValue("PWSweather", "SendUV", false);
			PWS.SendSolar = ini.GetValue("PWSweather", "SendSR", false);
			PWS.CatchUp = ini.GetValue("PWSweather", "CatchUp", true);

			WOW.ID = ini.GetValue("WOW", "ID", "");
			WOW.PW = ini.GetValue("WOW", "Password", "");
			WOW.Enabled = ini.GetValue("WOW", "Enabled", false);
			WOW.Interval = ini.GetValue("WOW", "Interval", WOW.DefaultInterval);
			if (WOW.Interval < 1)
			{
				WOW.Interval = 1;
				rewriteRequired = true;
			}
			WOW.SendUV = ini.GetValue("WOW", "SendUV", false);
			WOW.SendSolar = ini.GetValue("WOW", "SendSR", false);
			WOW.SendSoilTemp = ini.GetValue("WOW", "SendSoilTemp", false);
			WOW.SoilTempSensor = ini.GetValue("WOW", "SoilTempSensor", 1);
			WOW.CatchUp = ini.GetValue("WOW", "CatchUp", false);

			APRS.ID = ini.GetValue("APRS", "ID", "");
			APRS.PW = ini.GetValue("APRS", "pass", "-1");
			APRS.Server = ini.GetValue("APRS", "server", "cwop.aprs.net");
			APRS.Port = ini.GetValue("APRS", "port", 14580);
			APRS.Enabled = ini.GetValue("APRS", "Enabled", false);
			APRS.Interval = ini.GetValue("APRS", "Interval", APRS.DefaultInterval);
			if (APRS.Interval < 1)
			{
				APRS.Interval = 1;
				rewriteRequired = true;
			}
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
			{
				MQTT.IpVersion = 0;
				rewriteRequired = true;
			}
			MQTT.UseTLS = ini.GetValue("MQTT", "UseTLS", false);
			MQTT.Username = ini.GetValue("MQTT", "Username", "");
			MQTT.Password = ini.GetValue("MQTT", "Password", "");
			MQTT.EnableDataUpdate = ini.GetValue("MQTT", "EnableDataUpdate", false);
			MQTT.UpdateTemplate = ini.GetValue("MQTT", "UpdateTemplate", "DataUpdateTemplate.txt");
			MQTT.EnableInterval = ini.GetValue("MQTT", "EnableInterval", false);
			MQTT.IntervalTime = ini.GetValue("MQTT", "IntervalTime", 600); // default to 10 minutes
			MQTT.IntervalTemplate = ini.GetValue("MQTT", "IntervalTemplate", "IntervalTemplate.txt");

			LowTempAlarm.Value = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
			LowTempAlarm.Enabled = ini.GetValue("Alarms", "LowTempAlarmSet", false);
			LowTempAlarm.Sound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
			LowTempAlarm.SoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
			if (LowTempAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				LowTempAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			LowTempAlarm.Notify = ini.GetValue("Alarms", "LowTempAlarmNotify", false);
			LowTempAlarm.Email = ini.GetValue("Alarms", "LowTempAlarmEmail", false);
			LowTempAlarm.Latch = ini.GetValue("Alarms", "LowTempAlarmLatch", false);
			LowTempAlarm.LatchHours = ini.GetValue("Alarms", "LowTempAlarmLatchHours", 24);
			LowTempAlarm.Action = ini.GetValue("Alarms", "LowTempAlarmAction", "");
			LowTempAlarm.ActionParams = ini.GetValue("Alarms", "LowTempAlarmActionParams", "");

			HighTempAlarm.Value = ini.GetValue("Alarms", "alarmhightemp", 0.0);
			HighTempAlarm.Enabled = ini.GetValue("Alarms", "HighTempAlarmSet", false);
			HighTempAlarm.Sound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
			HighTempAlarm.SoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
			if (HighTempAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighTempAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			HighTempAlarm.Notify = ini.GetValue("Alarms", "HighTempAlarmNotify", false);
			HighTempAlarm.Email = ini.GetValue("Alarms", "HighTempAlarmEmail", false);
			HighTempAlarm.Latch = ini.GetValue("Alarms", "HighTempAlarmLatch", false);
			HighTempAlarm.LatchHours = ini.GetValue("Alarms", "HighTempAlarmLatchHours", 24);
			HighTempAlarm.Action = ini.GetValue("Alarms", "HighTempAlarmAction", "");
			HighTempAlarm.ActionParams = ini.GetValue("Alarms", "HighTempAlarmActionParams", "");

			TempChangeAlarm.Value = ini.GetValue("Alarms", "alarmtempchange", 0.0);
			TempChangeAlarm.Enabled = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
			TempChangeAlarm.Sound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
			TempChangeAlarm.SoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
			if (TempChangeAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				TempChangeAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			TempChangeAlarm.Notify = ini.GetValue("Alarms", "TempChangeAlarmNotify", false);
			TempChangeAlarm.Email = ini.GetValue("Alarms", "TempChangeAlarmEmail", false);
			TempChangeAlarm.Latch = ini.GetValue("Alarms", "TempChangeAlarmLatch", false);
			TempChangeAlarm.LatchHours = ini.GetValue("Alarms", "TempChangeAlarmLatchHours", 24);
			TempChangeAlarm.Action = ini.GetValue("Alarms", "TempChangeAlarmAction", "");
			TempChangeAlarm.ActionParams = ini.GetValue("Alarms", "TempChangeAlarmActionParams", "");

			LowPressAlarm.Value = ini.GetValue("Alarms", "alarmlowpress", 0.0);
			LowPressAlarm.Enabled = ini.GetValue("Alarms", "LowPressAlarmSet", false);
			LowPressAlarm.Sound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
			LowPressAlarm.SoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
			if (LowPressAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				LowPressAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			LowPressAlarm.Notify = ini.GetValue("Alarms", "LowPressAlarmNotify", false);
			LowPressAlarm.Email = ini.GetValue("Alarms", "LowPressAlarmEmail", false);
			LowPressAlarm.Latch = ini.GetValue("Alarms", "LowPressAlarmLatch", false);
			LowPressAlarm.LatchHours = ini.GetValue("Alarms", "LowPressAlarmLatchHours", 24);
			LowPressAlarm.Action = ini.GetValue("Alarms", "LowPressAlarmAction", "");
			LowPressAlarm.ActionParams = ini.GetValue("Alarms", "LowPressAlarmActionParams", "");

			HighPressAlarm.Value = ini.GetValue("Alarms", "alarmhighpress", 0.0);
			HighPressAlarm.Enabled = ini.GetValue("Alarms", "HighPressAlarmSet", false);
			HighPressAlarm.Sound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
			HighPressAlarm.SoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
			if (HighPressAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighPressAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			HighPressAlarm.Notify = ini.GetValue("Alarms", "HighPressAlarmNotify", false);
			HighPressAlarm.Email = ini.GetValue("Alarms", "HighPressAlarmEmail", false);
			HighPressAlarm.Latch = ini.GetValue("Alarms", "HighPressAlarmLatch", false);
			HighPressAlarm.LatchHours = ini.GetValue("Alarms", "HighPressAlarmLatchHours", 24);
			HighPressAlarm.Action = ini.GetValue("Alarms", "HighPressAlarmAction", "");
			HighPressAlarm.ActionParams = ini.GetValue("Alarms", "HighPressAlarmActionParams", "");

			PressChangeAlarm.Value = ini.GetValue("Alarms", "alarmpresschange", 0.0);
			PressChangeAlarm.Enabled = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
			PressChangeAlarm.Sound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
			PressChangeAlarm.SoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
			if (PressChangeAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				PressChangeAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			PressChangeAlarm.Notify = ini.GetValue("Alarms", "PressChangeAlarmNotify", false);
			PressChangeAlarm.Email = ini.GetValue("Alarms", "PressChangeAlarmEmail", false);
			PressChangeAlarm.Latch = ini.GetValue("Alarms", "PressChangeAlarmLatch", false);
			PressChangeAlarm.LatchHours = ini.GetValue("Alarms", "PressChangeAlarmLatchHours", 24);
			PressChangeAlarm.Action = ini.GetValue("Alarms", "PressChangeAlarmAction", "");
			PressChangeAlarm.ActionParams = ini.GetValue("Alarms", "PressChangeAlarmActionParams", "");

			HighRainTodayAlarm.Value = ini.GetValue("Alarms", "alarmhighraintoday", 0.0);
			HighRainTodayAlarm.Enabled = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
			HighRainTodayAlarm.Sound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
			HighRainTodayAlarm.SoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
			if (HighRainTodayAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighRainTodayAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			HighRainTodayAlarm.Notify = ini.GetValue("Alarms", "HighRainTodayAlarmNotify", false);
			HighRainTodayAlarm.Email = ini.GetValue("Alarms", "HighRainTodayAlarmEmail", false);
			HighRainTodayAlarm.Latch = ini.GetValue("Alarms", "HighRainTodayAlarmLatch", false);
			HighRainTodayAlarm.LatchHours = ini.GetValue("Alarms", "HighRainTodayAlarmLatchHours", 24);
			HighRainTodayAlarm.Action = ini.GetValue("Alarms", "HighRainTodayAlarmAction", "");
			HighRainTodayAlarm.ActionParams = ini.GetValue("Alarms", "HighRainTodayAlarmActionParams", "");

			HighRainRateAlarm.Value = ini.GetValue("Alarms", "alarmhighrainrate", 0.0);
			HighRainRateAlarm.Enabled = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
			HighRainRateAlarm.Sound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
			HighRainRateAlarm.SoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
			if (HighRainRateAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighRainRateAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			HighRainRateAlarm.Notify = ini.GetValue("Alarms", "HighRainRateAlarmNotify", false);
			HighRainRateAlarm.Email = ini.GetValue("Alarms", "HighRainRateAlarmEmail", false);
			HighRainRateAlarm.Latch = ini.GetValue("Alarms", "HighRainRateAlarmLatch", false);
			HighRainRateAlarm.LatchHours = ini.GetValue("Alarms", "HighRainRateAlarmLatchHours", 24);
			HighRainRateAlarm.Action = ini.GetValue("Alarms", "HighRainRateAlarmAction", "");
			HighRainRateAlarm.ActionParams = ini.GetValue("Alarms", "HighRainRateAlarmActionParams", "");

			IsRainingAlarm.Enabled = ini.GetValue("Alarms", "IsRainingAlarmSet", false);
			IsRainingAlarm.Sound = ini.GetValue("Alarms", "IsRainingAlarmSound", false);
			IsRainingAlarm.SoundFile = ini.GetValue("Alarms", "IsRainingAlarmSoundFile", DefaultSoundFile);
			IsRainingAlarm.Notify = ini.GetValue("Alarms", "IsRainingAlarmNotify", false);
			IsRainingAlarm.Email = ini.GetValue("Alarms", "IsRainingAlarmEmail", false);
			IsRainingAlarm.Latch = ini.GetValue("Alarms", "IsRainingAlarmLatch", false);
			IsRainingAlarm.LatchHours = ini.GetValue("Alarms", "IsRainingAlarmLatchHours", 1);
			IsRainingAlarm.Action = ini.GetValue("Alarms", "IsRainingAlarmAction", "");
			IsRainingAlarm.ActionParams = ini.GetValue("Alarms", "IsRainingAlarmActionParams", "");

			HighGustAlarm.Value = ini.GetValue("Alarms", "alarmhighgust", 0.0);
			HighGustAlarm.Enabled = ini.GetValue("Alarms", "HighGustAlarmSet", false);
			HighGustAlarm.Sound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
			HighGustAlarm.SoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
			if (HighGustAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighGustAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			HighGustAlarm.Notify = ini.GetValue("Alarms", "HighGustAlarmNotify", false);
			HighGustAlarm.Email = ini.GetValue("Alarms", "HighGustAlarmEmail", false);
			HighGustAlarm.Latch = ini.GetValue("Alarms", "HighGustAlarmLatch", false);
			HighGustAlarm.LatchHours = ini.GetValue("Alarms", "HighGustAlarmLatchHours", 24);
			HighGustAlarm.Action = ini.GetValue("Alarms", "HighGustAlarmAction", "");
			HighGustAlarm.ActionParams = ini.GetValue("Alarms", "HighGustAlarmActionParams", "");

			HighWindAlarm.Value = ini.GetValue("Alarms", "alarmhighwind", 0.0);
			HighWindAlarm.Enabled = ini.GetValue("Alarms", "HighWindAlarmSet", false);
			HighWindAlarm.Sound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
			HighWindAlarm.SoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
			if (HighWindAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighWindAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			HighWindAlarm.Notify = ini.GetValue("Alarms", "HighWindAlarmNotify", false);
			HighWindAlarm.Email = ini.GetValue("Alarms", "HighWindAlarmEmail", false);
			HighWindAlarm.Latch = ini.GetValue("Alarms", "HighWindAlarmLatch", false);
			HighWindAlarm.LatchHours = ini.GetValue("Alarms", "HighWindAlarmLatchHours", 24);
			HighWindAlarm.Action = ini.GetValue("Alarms", "HighWindAlarmAction", "");
			HighWindAlarm.ActionParams = ini.GetValue("Alarms", "HighWindAlarmActionParams", "");

			SensorAlarm.Enabled = ini.GetValue("Alarms", "SensorAlarmSet", true);
			SensorAlarm.Sound = ini.GetValue("Alarms", "SensorAlarmSound", false);
			SensorAlarm.SoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
			if (SensorAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				SensorAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			SensorAlarm.Notify = ini.GetValue("Alarms", "SensorAlarmNotify", true);
			SensorAlarm.Email = ini.GetValue("Alarms", "SensorAlarmEmail", false);
			SensorAlarm.Latch = ini.GetValue("Alarms", "SensorAlarmLatch", true);
			SensorAlarm.LatchHours = ini.GetValue("Alarms", "SensorAlarmLatchHours", 1);
			SensorAlarm.TriggerThreshold = ini.GetValue("Alarms", "SensorAlarmTriggerCount", 2);
			SensorAlarm.Action = ini.GetValue("Alarms", "SensorAlarmAction", "");
			SensorAlarm.ActionParams = ini.GetValue("Alarms", "SensorAlarmActionParams", "");

			DataStoppedAlarm.Enabled = ini.GetValue("Alarms", "DataStoppedAlarmSet", true);
			DataStoppedAlarm.Sound = ini.GetValue("Alarms", "DataStoppedAlarmSound", false);
			DataStoppedAlarm.SoundFile = ini.GetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
			if (DataStoppedAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				SensorAlarm.SoundFile = DefaultSoundFile;
				rewriteRequired = true;
			}
			DataStoppedAlarm.Notify = ini.GetValue("Alarms", "DataStoppedAlarmNotify", true);
			DataStoppedAlarm.Email = ini.GetValue("Alarms", "DataStoppedAlarmEmail", false);
			DataStoppedAlarm.Latch = ini.GetValue("Alarms", "DataStoppedAlarmLatch", true);
			DataStoppedAlarm.LatchHours = ini.GetValue("Alarms", "DataStoppedAlarmLatchHours", 1);
			DataStoppedAlarm.TriggerThreshold = ini.GetValue("Alarms", "DataStoppedAlarmTriggerCount", 2);
			DataStoppedAlarm.Action = ini.GetValue("Alarms", "DataStoppedAlarmAction", "");
			DataStoppedAlarm.ActionParams = ini.GetValue("Alarms", "DataStoppedAlarmActionParams", "");

			// Alarms below here were created after the change in default sound file, so no check required
			BatteryLowAlarm.Enabled = ini.GetValue("Alarms", "BatteryLowAlarmSet", false);
			BatteryLowAlarm.Sound = ini.GetValue("Alarms", "BatteryLowAlarmSound", false);
			BatteryLowAlarm.SoundFile = ini.GetValue("Alarms", "BatteryLowAlarmSoundFile", DefaultSoundFile);
			BatteryLowAlarm.Notify = ini.GetValue("Alarms", "BatteryLowAlarmNotify", false);
			BatteryLowAlarm.Email = ini.GetValue("Alarms", "BatteryLowAlarmEmail", false);
			BatteryLowAlarm.Latch = ini.GetValue("Alarms", "BatteryLowAlarmLatch", false);
			BatteryLowAlarm.LatchHours = ini.GetValue("Alarms", "BatteryLowAlarmLatchHours", 24);
			BatteryLowAlarm.TriggerThreshold = ini.GetValue("Alarms", "BatteryLowAlarmTriggerCount", 1);
			BatteryLowAlarm.Action = ini.GetValue("Alarms", "BatteryLowAlarmAction", "");
			BatteryLowAlarm.ActionParams = ini.GetValue("Alarms", "BatteryLowAlarmActionParams", "");

			SpikeAlarm.Enabled = ini.GetValue("Alarms", "DataSpikeAlarmSet", false);
			SpikeAlarm.Sound = ini.GetValue("Alarms", "DataSpikeAlarmSound", false);
			SpikeAlarm.SoundFile = ini.GetValue("Alarms", "DataSpikeAlarmSoundFile", DefaultSoundFile);
			SpikeAlarm.Notify = ini.GetValue("Alarms", "DataSpikeAlarmNotify", true);
			SpikeAlarm.Email = ini.GetValue("Alarms", "DataSpikeAlarmEmail", true);
			SpikeAlarm.Latch = ini.GetValue("Alarms", "DataSpikeAlarmLatch", true);
			SpikeAlarm.LatchHours = ini.GetValue("Alarms", "DataSpikeAlarmLatchHours", 24);
			SpikeAlarm.TriggerThreshold = ini.GetValue("Alarms", "DataSpikeAlarmTriggerCount", 1);
			SpikeAlarm.Action = ini.GetValue("Alarms", "DataSpikeAlarmAction", "");
			SpikeAlarm.ActionParams = ini.GetValue("Alarms", "DataSpikeAlarmActionParams", "");

			UpgradeAlarm.Enabled = ini.GetValue("Alarms", "UpgradeAlarmSet", true);
			UpgradeAlarm.Sound = ini.GetValue("Alarms", "UpgradeAlarmSound", false);
			UpgradeAlarm.SoundFile = ini.GetValue("Alarms", "UpgradeAlarmSoundFile", DefaultSoundFile);
			UpgradeAlarm.Notify = ini.GetValue("Alarms", "UpgradeAlarmNotify", true);
			UpgradeAlarm.Email = ini.GetValue("Alarms", "UpgradeAlarmEmail", false);
			UpgradeAlarm.Latch = ini.GetValue("Alarms", "UpgradeAlarmLatch", false);
			UpgradeAlarm.LatchHours = ini.GetValue("Alarms", "UpgradeAlarmLatchHours", 24);
			UpgradeAlarm.Action = ini.GetValue("Alarms", "UpgradeAlarmAction", "");
			UpgradeAlarm.ActionParams = ini.GetValue("Alarms", "UpgradeAlarmActionParams", "");

			HttpUploadAlarm.Enabled = ini.GetValue("Alarms", "HttpUploadAlarmSet", false);
			HttpUploadAlarm.Sound = ini.GetValue("Alarms", "HttpUploadAlarmSound", false);
			HttpUploadAlarm.SoundFile = ini.GetValue("Alarms", "HttpUploadAlarmSoundFile", DefaultSoundFile);
			HttpUploadAlarm.Notify = ini.GetValue("Alarms", "HttpUploadAlarmNotify", false);
			HttpUploadAlarm.Email = ini.GetValue("Alarms", "HttpUploadAlarmEmail", false);
			HttpUploadAlarm.Latch = ini.GetValue("Alarms", "HttpUploadAlarmLatch", false);
			HttpUploadAlarm.LatchHours = ini.GetValue("Alarms", "HttpUploadAlarmLatchHours", 24);
			HttpUploadAlarm.TriggerThreshold = ini.GetValue("Alarms", "HttpUploadAlarmTriggerCount", 1);
			HttpUploadAlarm.Action = ini.GetValue("Alarms", "HttpUploadAlarmAction", "");
			HttpUploadAlarm.ActionParams = ini.GetValue("Alarms", "HttpUploadAlarmActionParams", "");

			MySqlUploadAlarm.Enabled = ini.GetValue("Alarms", "MySqlUploadAlarmSet", false);
			MySqlUploadAlarm.Sound = ini.GetValue("Alarms", "MySqlUploadAlarmSound", false);
			MySqlUploadAlarm.SoundFile = ini.GetValue("Alarms", "MySqlUploadAlarmSoundFile", DefaultSoundFile);
			MySqlUploadAlarm.Notify = ini.GetValue("Alarms", "MySqlUploadAlarmNotify", false);
			MySqlUploadAlarm.Email = ini.GetValue("Alarms", "MySqlUploadAlarmEmail", false);
			MySqlUploadAlarm.Latch = ini.GetValue("Alarms", "MySqlUploadAlarmLatch", false);
			MySqlUploadAlarm.LatchHours = ini.GetValue("Alarms", "MySqlUploadAlarmLatchHours", 24);
			MySqlUploadAlarm.TriggerThreshold = ini.GetValue("Alarms", "MySqlUploadAlarmTriggerCount", 1);
			MySqlUploadAlarm.Action = ini.GetValue("Alarms", "MySqlUploadAlarmAction", "");
			MySqlUploadAlarm.ActionParams = ini.GetValue("Alarms", "MySqlUploadAlarmActionParams", "");


			AlarmFromEmail = ini.GetValue("Alarms", "FromEmail", "");
			AlarmDestEmail = ini.GetValue("Alarms", "DestEmail", "").Split(';');
			AlarmEmailHtml = ini.GetValue("Alarms", "UseHTML", false);

			Calib.Press.Offset = ini.GetValue("Offsets", "PressOffset", 0.0);
			Calib.Temp.Offset = ini.GetValue("Offsets", "TempOffset", 0.0);
			Calib.Hum.Offset = ini.GetValue("Offsets", "HumOffset", 0);
			Calib.WindDir.Offset = ini.GetValue("Offsets", "WindDirOffset", 0);
			Calib.Solar.Offset = ini.GetValue("Offsets", "SolarOffset", 0.0);
			Calib.UV.Offset = ini.GetValue("Offsets", "UVOffset", 0.0);
			Calib.WetBulb.Offset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);
			Calib.InTemp.Offset = ini.GetValue("Offsets", "InTempOffset", 0.0);
			Calib.InHum.Offset = ini.GetValue("Offsets", "InHumOffset", 0);

			Calib.Press.Mult = ini.GetValue("Offsets", "PressMult", 1.0);
			Calib.WindSpeed.Mult = ini.GetValue("Offsets", "WindSpeedMult", 1.0);
			Calib.WindGust.Mult = ini.GetValue("Offsets", "WindGustMult", 1.0);
			Calib.Temp.Mult = ini.GetValue("Offsets", "TempMult", 1.0);
			Calib.Hum.Mult = ini.GetValue("Offsets", "HumMult", 1.0);
			Calib.Rain.Mult = ini.GetValue("Offsets", "RainMult", 1.0);
			Calib.Solar.Mult = ini.GetValue("Offsets", "SolarMult", 1.0);
			Calib.UV.Mult = ini.GetValue("Offsets", "UVMult", 1.0);
			Calib.WetBulb.Mult = ini.GetValue("Offsets", "WetBulbMult", 1.0);
			Calib.InTemp.Mult = ini.GetValue("Offsets", "InTempMult", 1.0);
			Calib.InHum.Mult = ini.GetValue("Offsets", "InHumMult", 1.0);

			Calib.Press.Mult2 = ini.GetValue("Offsets", "PressMult2", 0.0);
			Calib.WindSpeed.Mult2 = ini.GetValue("Offsets", "WindSpeedMult2", 0.0);
			Calib.WindGust.Mult2 = ini.GetValue("Offsets", "WindGustMult2", 0.0);
			Calib.Temp.Mult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
			Calib.Hum.Mult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
			Calib.InTemp.Mult2 = ini.GetValue("Offsets", "InTempMult2", 0.0);
			Calib.InHum.Mult2 = ini.GetValue("Offsets", "InHumMult2", 0.0);
			Calib.Solar.Mult2 = ini.GetValue("Offsets", "SolarMult2", 0.0);
			Calib.UV.Mult2 = ini.GetValue("Offsets", "UVMult2", 0.0);

			Limit.TempHigh = ini.GetValue("Limits", "TempHighC", 60.0);
			Limit.TempLow = ini.GetValue("Limits", "TempLowC", -60.0);
			Limit.DewHigh = ini.GetValue("Limits", "DewHighC", 40.0);
			Limit.PressHigh = ini.GetValue("Limits", "PressHighMB", 1090.0);
			Limit.PressLow = ini.GetValue("Limits", "PressLowMB", 870.0);
			Limit.WindHigh = ini.GetValue("Limits", "WindHighMS", 90.0);

			xapEnabled = ini.GetValue("xAP", "Enabled", false);
			xapUID = ini.GetValue("xAP", "UID", "4375");
			xapPort = ini.GetValue("xAP", "Port", 3639);

			SolarOptions.SunThreshold = ini.GetValue("Solar", "SunThreshold", 75);
			SolarOptions.SolarMinimum = ini.GetValue("Solar", "SolarMinimum", 30);
			SolarOptions.LuxToWM2 = ini.GetValue("Solar", "LuxToWM2", 0.0079);
			SolarOptions.UseBlakeLarsen = ini.GetValue("Solar", "UseBlakeLarsen", false);
			SolarOptions.SolarCalc = ini.GetValue("Solar", "SolarCalc", 0);

			// Migrate old single solar factors to the new dual scheme
			if (ini.ValueExists("Solar", "RStransfactor"))
			{
				SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactor", 0.8);
				SolarOptions.RStransfactorDec = SolarOptions.RStransfactorJun;
				rewriteRequired = true;
			}
			else
			{
				if (ini.ValueExists("Solar", "RStransfactorJul"))
				{
					SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactorJul", 0.8);
					rewriteRequired = true;
				}
				else
				{
					SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactorJun", 0.8);
				}
				SolarOptions.RStransfactorDec = ini.GetValue("Solar", "RStransfactorDec", 0.8);
			}
			if (ini.ValueExists("Solar", "BrasTurbidity"))
			{
				SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidity", 2.0);
				SolarOptions.BrasTurbidityDec = SolarOptions.BrasTurbidityJun;
				rewriteRequired = true;
			}
			else
			{
				if (ini.ValueExists("Solar", "BrasTurbidityJul"))
				{
					SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidityJul", 2.0);
					rewriteRequired = true;
				}
				else
				{
					SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidityJun", 2.0);
				}
				SolarOptions.BrasTurbidityDec = ini.GetValue("Solar", "BrasTurbidityDec", 2.0);
			}

			NOAAconf.Name = ini.GetValue("NOAA", "Name", " ");
			NOAAconf.City = ini.GetValue("NOAA", "City", " ");
			NOAAconf.State = ini.GetValue("NOAA", "State", " ");
			NOAAconf.Use12hour = ini.GetValue("NOAA", "12hourformat", false);
			NOAAconf.HeatThreshold = ini.GetValue("NOAA", "HeatingThreshold", -1000.0);
			if (NOAAconf.HeatThreshold < -99 || NOAAconf.HeatThreshold > 150)
			{
				NOAAconf.HeatThreshold = Units.Temp == 0 ? 18.3 : 65;
				rewriteRequired = true;
			}
			NOAAconf.CoolThreshold = ini.GetValue("NOAA", "CoolingThreshold", -1000.0);
			if (NOAAconf.CoolThreshold < -99 || NOAAconf.CoolThreshold > 150)
			{
				NOAAconf.CoolThreshold = Units.Temp == 0 ? 18.3 : 65;
				rewriteRequired = true;
			}
			NOAAconf.MaxTempComp1 = ini.GetValue("NOAA", "MaxTempComp1", -1000.0);
			if (NOAAconf.MaxTempComp1 < -99 || NOAAconf.MaxTempComp1 > 150)
			{
				NOAAconf.MaxTempComp1 = Units.Temp == 0 ? 27 : 80;
				rewriteRequired = true;
			}
			NOAAconf.MaxTempComp2 = ini.GetValue("NOAA", "MaxTempComp2", -1000.0);
			if (NOAAconf.MaxTempComp2 < -99 || NOAAconf.MaxTempComp2 > 99)
			{
				NOAAconf.MaxTempComp2 = Units.Temp == 0 ? 0 : 32;
				rewriteRequired = true;
			}
			NOAAconf.MinTempComp1 = ini.GetValue("NOAA", "MinTempComp1", -1000.0);
			if (NOAAconf.MinTempComp1 < -99 || NOAAconf.MinTempComp1 > 99)
			{
				NOAAconf.MinTempComp1 = Units.Temp == 0 ? 0 : 32;
				rewriteRequired = true;
			}
			NOAAconf.MinTempComp2 = ini.GetValue("NOAA", "MinTempComp2", -1000.0);
			if (NOAAconf.MinTempComp2 < -99 || NOAAconf.MinTempComp2 > 99)
			{
				NOAAconf.MinTempComp2 = Units.Temp == 0 ? -18 : 0;
				rewriteRequired = true;
			}
			NOAAconf.RainComp1 = ini.GetValue("NOAA", "RainComp1", -1000.0);
			if (NOAAconf.RainComp1 < 0 || NOAAconf.RainComp1 > 99)
			{
				NOAAconf.RainComp1 = Units.Rain == 0 ? 0.2 : 0.01;
				rewriteRequired = true;
			}
			NOAAconf.RainComp2 = ini.GetValue("NOAA", "RainComp2", -1000.0);
			if (NOAAconf.RainComp2 < 0 || NOAAconf.RainComp2 > 99)
			{
				NOAAconf.RainComp2 = Units.Rain == 0 ? 2 : 0.1;
				rewriteRequired = true;
			}
			NOAAconf.RainComp3 = ini.GetValue("NOAA", "RainComp3", -1000.0);
			if (NOAAconf.RainComp3 < 0 || NOAAconf.RainComp3 > 99)
			{
				NOAAconf.RainComp3 = Units.Rain == 0 ? 20 : 1;
				rewriteRequired = true;
			}

			NOAAconf.Create = ini.GetValue("NOAA", "AutoSave", false);
			NOAAconf.AutoFtp = ini.GetValue("NOAA", "AutoFTP", false);
			NOAAconf.FtpFolder = ini.GetValue("NOAA", "FTPDirectory", "");
			NOAAconf.AutoCopy = ini.GetValue("NOAA", "AutoCopy", false);
			NOAAconf.CopyFolder = ini.GetValue("NOAA", "CopyDirectory", "");
			NOAAconf.MonthFile = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
			// Check for Cumulus 1 default format - and update
			if (NOAAconf.MonthFile == "'NOAAMO'mmyy'.txt'" || NOAAconf.MonthFile == "\"NOAAMO\"mmyy\".txt\"")
			{
				NOAAconf.MonthFile = "'NOAAMO'MMyy'.txt'";
				rewriteRequired = true;
			}
			NOAAconf.YearFile = ini.GetValue("NOAA", "YearFileFormat", "'NOAAYR'yyyy'.txt'");
			NOAAconf.UseUtf8 = ini.GetValue("NOAA", "NOAAUseUTF8", true);
			NOAAconf.UseDotDecimal = ini.GetValue("NOAA", "UseDotDecimal", false);
			NOAAconf.UseNoaaHeatCoolDays = ini.GetValue("NOAA", "UseNoaaHeatCoolDays", false);
			NOAAconf.UseMinMaxAvg = ini.GetValue("NOAA", "UseMinMaxAvg", false);

			NOAAconf.TempNorms[1] = ini.GetValue("NOAA", "NOAATempNormJan", -1000.0);
			NOAAconf.TempNorms[2] = ini.GetValue("NOAA", "NOAATempNormFeb", -1000.0);
			NOAAconf.TempNorms[3] = ini.GetValue("NOAA", "NOAATempNormMar", -1000.0);
			NOAAconf.TempNorms[4] = ini.GetValue("NOAA", "NOAATempNormApr", -1000.0);
			NOAAconf.TempNorms[5] = ini.GetValue("NOAA", "NOAATempNormMay", -1000.0);
			NOAAconf.TempNorms[6] = ini.GetValue("NOAA", "NOAATempNormJun", -1000.0);
			NOAAconf.TempNorms[7] = ini.GetValue("NOAA", "NOAATempNormJul", -1000.0);
			NOAAconf.TempNorms[8] = ini.GetValue("NOAA", "NOAATempNormAug", -1000.0);
			NOAAconf.TempNorms[9] = ini.GetValue("NOAA", "NOAATempNormSep", -1000.0);
			NOAAconf.TempNorms[10] = ini.GetValue("NOAA", "NOAATempNormOct", -1000.0);
			NOAAconf.TempNorms[11] = ini.GetValue("NOAA", "NOAATempNormNov", -1000.0);
			NOAAconf.TempNorms[12] = ini.GetValue("NOAA", "NOAATempNormDec", -1000.0);

			NOAAconf.RainNorms[1] = ini.GetValue("NOAA", "NOAARainNormJan", -1000.0);
			NOAAconf.RainNorms[2] = ini.GetValue("NOAA", "NOAARainNormFeb", -1000.0);
			NOAAconf.RainNorms[3] = ini.GetValue("NOAA", "NOAARainNormMar", -1000.0);
			NOAAconf.RainNorms[4] = ini.GetValue("NOAA", "NOAARainNormApr", -1000.0);
			NOAAconf.RainNorms[5] = ini.GetValue("NOAA", "NOAARainNormMay", -1000.0);
			NOAAconf.RainNorms[6] = ini.GetValue("NOAA", "NOAARainNormJun", -1000.0);
			NOAAconf.RainNorms[7] = ini.GetValue("NOAA", "NOAARainNormJul", -1000.0);
			NOAAconf.RainNorms[8] = ini.GetValue("NOAA", "NOAARainNormAug", -1000.0);
			NOAAconf.RainNorms[9] = ini.GetValue("NOAA", "NOAARainNormSep", -1000.0);
			NOAAconf.RainNorms[10] = ini.GetValue("NOAA", "NOAARainNormOct", -1000.0);
			NOAAconf.RainNorms[11] = ini.GetValue("NOAA", "NOAARainNormNov", -1000.0);
			NOAAconf.RainNorms[12] = ini.GetValue("NOAA", "NOAARainNormDec", -1000.0);

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
			MySqlSettings.UpdateOnEdit = ini.GetValue("MySQL", "UpdateOnEdit", true);
			MySqlSettings.BufferOnfailure = ini.GetValue("MySQL", "BufferOnFailure", false);

			// MySQL - monthly log file
			MySqlSettings.Monthly.Enabled = ini.GetValue("MySQL", "MonthlyMySqlEnabled", false);
			MySqlSettings.Monthly.TableName = ini.GetValue("MySQL", "MonthlyTable", "Monthly");
			// MySQL - real-time
			MySqlSettings.Realtime.Enabled = ini.GetValue("MySQL", "RealtimeMySqlEnabled", false);
			MySqlSettings.Realtime.TableName = ini.GetValue("MySQL", "RealtimeTable", "Realtime");
			MySqlSettings.RealtimeRetention = ini.GetValue("MySQL", "RealtimeRetention", "");
			MySqlSettings.RealtimeLimit1Minute = ini.GetValue("MySQL", "RealtimeMySql1MinLimit", false) && RealtimeInterval < 60000; // do not enable if real time interval is greater than 1 minute
																																	 // MySQL - dayfile
			MySqlSettings.Dayfile.Enabled = ini.GetValue("MySQL", "DayfileMySqlEnabled", false);
			MySqlSettings.Dayfile.TableName = ini.GetValue("MySQL", "DayfileTable", "Dayfile");

			// MySQL - custom seconds
			MySqlSettings.CustomSecs.Commands[0] = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString", "");
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlSecondsCommandString" + i))
					MySqlSettings.CustomSecs.Commands[i] = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString" + i, "");
			}

			MySqlSettings.CustomSecs.Enabled = ini.GetValue("MySQL", "CustomMySqlSecondsEnabled", false);
			MySqlSettings.CustomSecs.Interval = ini.GetValue("MySQL", "CustomMySqlSecondsInterval", 10);
			if (MySqlSettings.CustomSecs.Interval < 1) { MySqlSettings.CustomSecs.Interval = 1; }

			// MySQL - custom minutes
			MySqlSettings.CustomMins.Commands[0] = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString", "");
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlMinutesCommandString" + i))
					MySqlSettings.CustomMins.Commands[i] = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString" + i, "");
			}

			MySqlSettings.CustomMins.Enabled = ini.GetValue("MySQL", "CustomMySqlMinutesEnabled", false);
			CustomMySqlMinutesIntervalIndex = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIndex", -1);
			if (CustomMySqlMinutesIntervalIndex >= 0 && CustomMySqlMinutesIntervalIndex < FactorsOf60.Length)
			{
				MySqlSettings.CustomMins.Interval = FactorsOf60[CustomMySqlMinutesIntervalIndex];
			}
			else
			{
				MySqlSettings.CustomMins.Interval = 10;
				CustomMySqlMinutesIntervalIndex = 6;
				rewriteRequired = true;
			}

			// MySql - Timed
			MySqlSettings.CustomTimed.Enabled = ini.GetValue("MySQL", "CustomMySqlTimedEnabled", false);
			for (var i = 0; i < 10; i++)
			{
				MySqlSettings.CustomTimed.Commands[i] = ini.GetValue("MySQL", "CustomMySqlTimedCommandString" + i, "");
				MySqlSettings.CustomTimed.SetStartTime(i, ini.GetValue("MySQL", "CustomMySqlTimedStartTime" + i, "00:00"));
				MySqlSettings.CustomTimed.Intervals[i] = ini.GetValue("MySQL", "CustomMySqlTimedInterval" + i, 1440);

				if (!string.IsNullOrEmpty(MySqlSettings.CustomTimed.Commands[i]))
					MySqlSettings.CustomTimed.SetInitialNextInterval(i, DateTime.Now);
			}


			// MySQL - custom roll-over
			MySqlSettings.CustomRollover.Enabled = ini.GetValue("MySQL", "CustomMySqlRolloverEnabled", false);
			MySqlSettings.CustomRollover.Commands[0] = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString", "");
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlRolloverCommandString" + i))
					MySqlSettings.CustomRollover.Commands[i] = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString" + i, "");
			}


			// Custom HTTP - seconds
			CustomHttpSecondsStrings[0] = ini.GetValue("HTTP", "CustomHttpSecondsString", "");
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpSecondsString" + i))
					CustomHttpSecondsStrings[i] = ini.GetValue("HTTP", "CustomHttpSecondsString" + i, "");
			}

			CustomHttpSecondsEnabled = ini.GetValue("HTTP", "CustomHttpSecondsEnabled", false);
			CustomHttpSecondsInterval = ini.GetValue("HTTP", "CustomHttpSecondsInterval", 10);
			if (CustomHttpSecondsInterval < 1) { CustomHttpSecondsInterval = 1; }

			// Custom HTTP - minutes
			CustomHttpMinutesStrings[0] = ini.GetValue("HTTP", "CustomHttpMinutesString", "");
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpMinutesString" + i))
					CustomHttpMinutesStrings[i] = ini.GetValue("HTTP", "CustomHttpMinutesString" + i, "");
			}

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
				rewriteRequired = true;
			}

			// Http - custom roll-over
			CustomHttpRolloverEnabled = ini.GetValue("HTTP", "CustomHttpRolloverEnabled", false);
			CustomHttpRolloverStrings[0] = ini.GetValue("HTTP", "CustomHttpRolloverString", "");
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpRolloverString" + i))
					CustomHttpRolloverStrings[i] = ini.GetValue("HTTP", "CustomHttpRolloverString" + i, "");
			}

			// Http files
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "HttpFileUrl" + i))
				{
					HttpFilesConfig[i].Enabled = ini.GetValue("HTTP", "HttpFileEnabled" + i, false);
					HttpFilesConfig[i].Url = ini.GetValue("HTTP", "HttpFileUrl" + i, "");
					HttpFilesConfig[i].Remote = ini.GetValue("HTTP", "HttpFileRemote" + i, "");
					HttpFilesConfig[i].Interval = ini.GetValue("HTTP", "HttpFileInterval" + i, 0);
					HttpFilesConfig[i].Upload = ini.GetValue("HTTP", "HttpFileUpload" + i, false);
					HttpFilesConfig[i].Timed = ini.GetValue("HTTP", "HttpFileTimed" + i, false);
					HttpFilesConfig[i].StartTimeString = ini.GetValue("HTTP", "HttpFileStartTime" + i, "00:00");
					if (HttpFilesConfig[i].Timed)
					{
						HttpFilesConfig[i].SetInitialNextInterval(DateTime.Now);
					}
				}
			}

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
			SmtpOptions.IgnoreCertErrors = ini.GetValue("SMTP", "IgnoreCertErrors", false);

			// Growing Degree Days
			GrowingBase1 = ini.GetValue("GrowingDD", "BaseTemperature1", (Units.Temp == 0 ? 5.0 : 40.0));
			GrowingBase2 = ini.GetValue("GrowingDD", "BaseTemperature2", (Units.Temp == 0 ? 10.0 : 50.0));
			GrowingYearStarts = ini.GetValue("GrowingDD", "YearStarts", (Latitude >= 0 ? 1 : 7));
			GrowingCap30C = ini.GetValue("GrowingDD", "Cap30C", true);

			// Temperature Sum
			TempSumYearStarts = ini.GetValue("TempSum", "TempSumYearStart", (Latitude >= 0 ? 1 : 7));
			if (TempSumYearStarts < 1 || TempSumYearStarts > 12)
			{
				TempSumYearStarts = 1;
				rewriteRequired = true;
			}
			TempSumBase1 = ini.GetValue("TempSum", "BaseTemperature1", GrowingBase1);
			TempSumBase2 = ini.GetValue("TempSum", "BaseTemperature2", GrowingBase2);

			// Custom Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("CustomLogs", "DailyFilename" + i))
					CustomDailyLogSettings[i].FileName = ini.GetValue("CustomLogs", "DailyFilename" + i, "");

				if (ini.ValueExists("CustomLogs", "DailyContent" + i))
					CustomDailyLogSettings[i].ContentString = ini.GetValue("CustomLogs", "DailyContent" + i, "").Replace("\n", "").Replace("\r", "");

				if (string.IsNullOrEmpty(CustomDailyLogSettings[i].FileName) || string.IsNullOrEmpty(CustomDailyLogSettings[i].ContentString))
					CustomDailyLogSettings[i].Enabled = false;
				else
					CustomDailyLogSettings[i].Enabled = ini.GetValue("CustomLogs", "DailyEnabled" + i, false);



				if (ini.ValueExists("CustomLogs", "IntervalFilename" + i))
					CustomIntvlLogSettings[i].FileName = ini.GetValue("CustomLogs", "IntervalFilename" + i, "");

				if (ini.ValueExists("CustomLogs", "IntervalContent" + i))
					CustomIntvlLogSettings[i].ContentString = ini.GetValue("CustomLogs", "IntervalContent" + i, "").Replace("\n", "").Replace("\r", "");

				if (string.IsNullOrEmpty(CustomIntvlLogSettings[i].FileName) || string.IsNullOrEmpty(CustomIntvlLogSettings[i].ContentString))
					CustomIntvlLogSettings[i].Enabled = false;
				else
					CustomIntvlLogSettings[i].Enabled = ini.GetValue("CustomLogs", "IntervalEnabled" + i, false);

				if (ini.ValueExists("CustomLogs", "IntervalIdx" + i))
				{
					CustomIntvlLogSettings[i].IntervalIdx = ini.GetValue("CustomLogs", "IntervalIdx" + i, DataLogInterval);

					if (CustomIntvlLogSettings[i].IntervalIdx >= 0 && CustomIntvlLogSettings[i].IntervalIdx < FactorsOf60.Length)
					{
						CustomIntvlLogSettings[i].Interval = FactorsOf60[CustomIntvlLogSettings[i].IntervalIdx];
					}
					else
					{
						CustomIntvlLogSettings[i].Interval = FactorsOf60[DataLogInterval];
						CustomIntvlLogSettings[i].IntervalIdx = DataLogInterval;
						rewriteRequired = true;
					}
				}
				else
				{
					CustomIntvlLogSettings[i].Interval = FactorsOf60[DataLogInterval];
					CustomIntvlLogSettings[i].IntervalIdx = DataLogInterval;
				}
			}


			LogMessage("Reading Cumulus.ini file completed");

			if (rewriteRequired && File.Exists("Cumulus.ini"))
			{
				LogMessage("Some values in Cumulus.ini had invalid values, or new required entries have been created.");
				LogMessage("Recreating Cumulus.ini to reflect the new configuration.");
				LogDebugMessage("Deleting existing Cumulus.ini");
				File.Delete("Cumulus.ini");
				WriteIniFile();
			}
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

			ini.SetValue("Program", "StartupTask", ProgramOptions.StartupTask);
			ini.SetValue("Program", "StartupTaskParams", ProgramOptions.StartupTaskParams);
			ini.SetValue("Program", "StartupTaskWait", ProgramOptions.StartupTaskWait);

			ini.SetValue("Program", "ShutdownTask", ProgramOptions.ShutdownTask);
			ini.SetValue("Program", "ShutdownTaskParams", ProgramOptions.ShutdownTaskParams);

			ini.SetValue("Program", "DataStoppedExit", ProgramOptions.DataStoppedExit);
			ini.SetValue("Program", "DataStoppedMins", ProgramOptions.DataStoppedMins);
			ini.SetValue("Program", "TimeFormat", ProgramOptions.TimeFormat);

			ini.SetValue("Culture", "RemoveSpaceFromDateSeparator", ProgramOptions.Culture.RemoveSpaceFromDateSeparator);

			ini.SetValue("Station", "WarnMultiple", ProgramOptions.WarnMultiple);
			ini.SetValue("Station", "ListWebTags", ProgramOptions.ListWebTags);



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
			ini.SetValue("Station", "Wind10MinAverage", StationOptions.CalcuateAverageWindSpeed);
			ini.SetValue("Station", "UseSpeedForAvgCalc", StationOptions.UseSpeedForAvgCalc);
			ini.SetValue("Station", "AvgBearingMinutes", StationOptions.AvgBearingMinutes);
			ini.SetValue("Station", "AvgSpeedMinutes", StationOptions.AvgSpeedMinutes);
			ini.SetValue("Station", "PeakGustMinutes", StationOptions.PeakGustMinutes);
			ini.SetValue("Station", "LCMaxWind", LCMaxWind);
			ini.SetValue("Station", "RecordSetTimeoutHrs", RecordSetTimeoutHrs);
			ini.SetValue("Station", "SnowDepthHour", SnowDepthHour);
			ini.SetValue("Station", "UseRainForIsRaining", StationOptions.UseRainForIsRaining);
			ini.SetValue("Station", "LeafWetnessIsRainingIdx", StationOptions.LeafWetnessIsRainingIdx);
			ini.SetValue("Station", "LeafWetnessIsRainingVal", StationOptions.LeafWetnessIsRainingThrsh);


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
			ini.SetValue("Station", "CalculatedET", StationOptions.CalculatedET);

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
			ini.SetValue("Station", "StartDateIso", RecordsBeganDateTime.ToString("yyyy-MM-dd"));
			ini.SetValue("Station", "YTDrain", YTDrain);
			ini.SetValue("Station", "YTDrainyear", YTDrainyear);
			ini.SetValue("Station", "UseDataLogger", UseDataLogger);
			ini.SetValue("Station", "UseCumulusForecast", UseCumulusForecast);
			ini.SetValue("Station", "HourlyForecast", HourlyForecast);
			ini.SetValue("Station", "UseCumulusPresstrendstr", StationOptions.UseCumulusPresstrendstr);
			ini.SetValue("Station", "FCpressinMB", FCpressinMB);
			ini.SetValue("Station", "FClowpress", FClowpress);
			ini.SetValue("Station", "FChighpress", FChighpress);
			//ini.SetValue("Station", "FCPressureThreshold", FCPressureThreshold);
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
			ini.SetValue("Station", "EWinTempdiff", Spike.InTempDiff);
			ini.SetValue("Station", "EWinHumiditydiff", Spike.InHumDiff);

			ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
			ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

			ini.SetValue("Station", "ChillHourSeasonStart", ChillHourSeasonStart);
			ini.SetValue("Station", "ChillHourThreshold", ChillHourThreshold);

			ini.SetValue("Station", "ErrorLogSpikeRemoval", ErrorLogSpikeRemoval);

			ini.SetValue("Station", "ImetBaudRate", ImetOptions.BaudRate);
			ini.SetValue("Station", "ImetWaitTime", ImetOptions.WaitTime);
			ini.SetValue("Station", "ImetReadDelay", ImetOptions.ReadDelay);
			ini.SetValue("Station", "ImetUpdateLogPointer", ImetOptions.UpdateLogPointer);

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

			// WeatherFlow Options
			ini.SetValue("Station", "WeatherFlowDeviceId", WeatherFlowOptions.WFDeviceId);
			ini.SetValue("Station", "WeatherFlowTcpPort", WeatherFlowOptions.WFTcpPort);
			ini.SetValue("Station", "WeatherFlowToken", WeatherFlowOptions.WFToken);
			ini.SetValue("Station", "WeatherFlowDaysHist", WeatherFlowOptions.WFDaysHist);


			// WeatherLink Live device settings
			ini.SetValue("WLL", "AutoUpdateIpAddress", WLLAutoUpdateIpAddress);
			ini.SetValue("WLL", "WLv2ApiKey", WllApiKey);
			ini.SetValue("WLL", "WLv2ApiSecret", WllApiSecret);
			ini.SetValue("WLL", "WLStationId", WllStationId);
			ini.SetValue("WLL", "DataStoppedOnBroadcast", WllTriggerDataStoppedOnBroadcast);
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
				ini.SetValue("WLL", "ExtraTempTxId" + i, WllExtraTempTx[i]);
				ini.SetValue("WLL", "ExtraHumOnTxId" + i, WllExtraHumTx[i]);
			}

			// GW1000 settings
			ini.SetValue("GW1000", "IPAddress", Gw1000IpAddress);
			ini.SetValue("GW1000", "MACAddress", Gw1000MacAddress);
			ini.SetValue("GW1000", "AutoUpdateIpAddress", Gw1000AutoUpdateIpAddress);
			ini.SetValue("GW1000", "PrimaryTHSensor", Gw1000PrimaryTHSensor);
			ini.SetValue("GW1000", "PrimaryRainSensor", Gw1000PrimaryRainSensor);
			ini.SetValue("GW1000", "ExtraSensorDataEnabled", EcowittExtraEnabled);
			ini.SetValue("GW1000", "ExtraSensorUseSolar", EcowittExtraUseSolar);
			ini.SetValue("GW1000", "ExtraSensorUseUv", EcowittExtraUseUv);
			ini.SetValue("GW1000", "ExtraSensorUseTempHum", EcowittExtraUseTempHum);
			ini.SetValue("GW1000", "ExtraSensorUseSoilTemp", EcowittExtraUseSoilTemp);
			ini.SetValue("GW1000", "ExtraSensorUseSoilMoist", EcowittExtraUseSoilMoist);
			ini.SetValue("GW1000", "ExtraSensorUseLeafWet", EcowittExtraUseLeafWet);
			ini.SetValue("GW1000", "ExtraSensorUseUserTemp", EcowittExtraUseUserTemp);
			ini.SetValue("GW1000", "ExtraSensorUseAQI", EcowittExtraUseAQI);
			ini.SetValue("GW1000", "ExtraSensorUseCo2", EcowittExtraUseCo2);
			ini.SetValue("GW1000", "ExtraSensorUseLightning", EcowittExtraUseLightning);
			ini.SetValue("GW1000", "ExtraSensorUseLeak", EcowittExtraUseLeak);
			ini.SetValue("GW1000", "SetCustomServer", EcowittSetCustomServer);
			ini.SetValue("GW1000", "EcowittGwAddr", EcowittGatewayAddr);
			ini.SetValue("GW1000", "EcowittLocalAddr", EcowittLocalAddr);
			ini.SetValue("GW1000", "EcowittCustomInterval", EcowittCustomInterval);
			ini.SetValue("GW1000", "ExtraSetCustomServer", EcowittExtraSetCustomServer);
			ini.SetValue("GW1000", "EcowittExtraGwAddr", EcowittExtraGatewayAddr);
			ini.SetValue("GW1000", "EcowittExtraLocalAddr", EcowittExtraLocalAddr);
			ini.SetValue("GW1000", "EcowittExtraCustomInterval", EcowittExtraCustomInterval);
			// api
			ini.SetValue("GW1000", "EcowittAppKey", EcowittApplicationKey);
			ini.SetValue("GW1000", "EcowittUserKey", EcowittUserApiKey);
			ini.SetValue("GW1000", "EcowittMacAddress", EcowittMacAddress);
			// WN34 sensor mapping
			for (int i = 1; i <= 8; i++)
			{
				ini.SetValue("GW1000", "WN34MapChan" + i, EcowittMapWN34[i]);
			}
			// forwarders
			for (int i = 0; i < EcowittForwarders.Length; i++)
			{
				if (string.IsNullOrEmpty(EcowittForwarders[i]))
					ini.DeleteValue("GW1000", "Forwarder" + i);
				else
					ini.SetValue("GW1000", "Forwarder" + i, EcowittForwarders[i].ToString());
			}

			// Ambient settings
			ini.SetValue("Ambient", "ExtraSensorDataEnabled", AmbientExtraEnabled);
			ini.SetValue("Ambient", "ExtraSensorUseSolar", AmbientExtraUseSolar);
			ini.SetValue("Ambient", "ExtraSensorUseUv", AmbientExtraUseUv);
			ini.SetValue("Ambient", "ExtraSensorUseTempHum", AmbientExtraUseTempHum);
			ini.SetValue("Ambient", "ExtraSensorUseSoilTemp", AmbientExtraUseSoilTemp);
			ini.SetValue("Ambient", "ExtraSensorUseSoilMoist", AmbientExtraUseSoilMoist);
			//ini.SetValue("Ambient", "ExtraSensorUseLeafWet", AmbientExtraUseLeafWet);
			ini.SetValue("Ambient", "ExtraSensorUseAQI", AmbientExtraUseAQI);
			ini.SetValue("Ambient", "ExtraSensorUseCo2", AmbientExtraUseCo2);
			ini.SetValue("Ambient", "ExtraSensorUseLightning", AmbientExtraUseLightning);
			ini.SetValue("Ambient", "ExtraSensorUseLeak", AmbientExtraUseLeak);


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

			ini.SetValue("FTP site", "Enabled", FtpOptions.Enabled);
			ini.SetValue("FTP site", "Host", FtpOptions.Hostname);
			ini.SetValue("FTP site", "Port", FtpOptions.Port);
			ini.SetValue("FTP site", "Username", FtpOptions.Username);
			ini.SetValue("FTP site", "Password", FtpOptions.Password);
			ini.SetValue("FTP site", "Directory", FtpOptions.Directory);

			//ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);  // Deprecated - now read-only
			ini.SetValue("FTP site", "Sslftp", (int)FtpOptions.FtpMode);
			// BUILD 3092 - added alternate SFTP authentication options
			ini.SetValue("FTP site", "SshFtpAuthentication", FtpOptions.SshAuthen);
			ini.SetValue("FTP site", "SshFtpPskFile", FtpOptions.SshPskFile);

			ini.SetValue("FTP site", "ConnectionAutoDetect", FtpOptions.AutoDetect);
			ini.SetValue("FTP site", "IgnoreCertErrors", FtpOptions.IgnoreCertErrors);

			ini.SetValue("FTP site", "FTPlogging", FtpOptions.Logging);
			ini.SetValue("FTP site", "UTF8encode", UTF8encode);
			ini.SetValue("FTP site", "EnableRealtime", RealtimeIntervalEnabled);
			ini.SetValue("FTP site", "RealtimeInterval", RealtimeInterval);
			ini.SetValue("FTP site", "RealtimeFTPEnabled", FtpOptions.RealtimeEnabled);
			ini.SetValue("FTP site", "RealtimeTxtCreate", RealtimeFiles[0].Create);
			ini.SetValue("FTP site", "RealtimeTxtFTP", RealtimeFiles[0].FTP);
			ini.SetValue("FTP site", "RealtimeTxtCopy", RealtimeFiles[0].Copy);

			ini.SetValue("FTP site", "RealtimeGaugesTxtCreate", RealtimeFiles[1].Create);
			ini.SetValue("FTP site", "RealtimeGaugesTxtFTP", RealtimeFiles[1].FTP);
			ini.SetValue("FTP site", "RealtimeGaugesTxtCopy", RealtimeFiles[1].Copy);

			ini.SetValue("FTP site", "IntervalEnabled", WebIntervalEnabled);
			ini.SetValue("FTP site", "IntervalFtpEnabled", FtpOptions.IntervalEnabled);

			ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
			for (var i = 0; i < StdWebFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, StdWebFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, StdWebFiles[i].FTP);
				ini.SetValue("FTP site", keyNameCopy, StdWebFiles[i].Copy);
			}

			for (var i = 0; i < GraphDataFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, GraphDataFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, GraphDataFiles[i].FTP);
				ini.SetValue("FTP site", keyNameCopy, GraphDataFiles[i].Copy);
			}

			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, GraphDataEodFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, GraphDataEodFiles[i].FTP);
				ini.SetValue("FTP site", keyNameCopy, GraphDataEodFiles[i].Copy);
			}

			ini.SetValue("FTP site", "IncludeMoonImage", MoonImage.Ftp);
			ini.SetValue("FTP site", "CopyMoonImage", MoonImage.Copy);
			ini.SetValue("FTP site", "FTPRename", FTPRename);
			ini.SetValue("FTP site", "DeleteBeforeUpload", DeleteBeforeUpload);
			ini.SetValue("FTP site", "ActiveFTP", FtpOptions.ActiveMode);
			ini.SetValue("FTP site", "DisableEPSV", FtpOptions.DisableEPSV);
			ini.SetValue("FTP site", "DisableFtpsExplicit", FtpOptions.DisableExplicit);


			for (int i = 0; i < numextrafiles; i++)
			{
				if (string.IsNullOrEmpty(ExtraFiles[i].local) && string.IsNullOrEmpty(ExtraFiles[i].remote))
				{
					ini.DeleteValue("FTP site", "ExtraLocal" + i);
					ini.DeleteValue("FTP site", "ExtraRemote" + i);
					ini.DeleteValue("FTP site", "ExtraProcess" + i);
					ini.DeleteValue("FTP site", "ExtraBinary" + i);
					ini.DeleteValue("FTP site", "ExtraRealtime" + i);
					ini.DeleteValue("FTP site", "ExtraFTP" + i);
					ini.DeleteValue("FTP site", "ExtraUTF" + i);
					ini.DeleteValue("FTP site", "ExtraEOD" + i);

				}
				else
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
			}

			ini.SetValue("FTP site", "ExternalProgram", ExternalProgram);
			ini.SetValue("FTP site", "RealtimeProgram", RealtimeProgram);
			ini.SetValue("FTP site", "DailyProgram", DailyProgram);
			ini.SetValue("FTP site", "ExternalParams", ExternalParams);
			ini.SetValue("FTP site", "RealtimeParams", RealtimeParams);
			ini.SetValue("FTP site", "DailyParams", DailyParams);

			// Local Copy Options
			ini.SetValue("FTP site", "EnableLocalCopy", FtpOptions.LocalCopyEnabled);
			ini.SetValue("FTP site", "LocalCopyFolder", FtpOptions.LocalCopyFolder);

			// PHP upload options
			ini.SetValue("FTP site", "PHP-URL", FtpOptions.PhpUrl);
			ini.SetValue("FTP site", "PHP-Secret", FtpOptions.PhpSecret);
			ini.SetValue("FTP site", "PHP-IgnoreCertErrors", FtpOptions.PhpIgnoreCertErrors);
			ini.SetValue("FTP site", "MaxConcurrentUploads", FtpOptions.MaxConcurrentUploads);


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
			ini.SetValue("Wunderground", "SendExtraTemp1", Wund.SendExtraTemp1);
			ini.SetValue("Wunderground", "SendExtraTemp2", Wund.SendExtraTemp2);
			ini.SetValue("Wunderground", "SendExtraTemp3", Wund.SendExtraTemp3);
			ini.SetValue("Wunderground", "SendExtraTemp4", Wund.SendExtraTemp4);

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
			ini.SetValue("WOW", "SendSoilTemp", WOW.SendSoilTemp);
			ini.SetValue("WOW", "SoilTempSensor", WOW.SoilTempSensor);
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
			ini.SetValue("MQTT", "UpdateTemplate", MQTT.UpdateTemplate);
			ini.SetValue("MQTT", "EnableInterval", MQTT.EnableInterval);
			ini.SetValue("MQTT", "IntervalTime", MQTT.IntervalTime);
			ini.SetValue("MQTT", "IntervalTemplate", MQTT.IntervalTemplate);

			ini.SetValue("Alarms", "alarmlowtemp", LowTempAlarm.Value);
			ini.SetValue("Alarms", "LowTempAlarmSet", LowTempAlarm.Enabled);
			ini.SetValue("Alarms", "LowTempAlarmSound", LowTempAlarm.Sound);
			ini.SetValue("Alarms", "LowTempAlarmSoundFile", LowTempAlarm.SoundFile);
			ini.SetValue("Alarms", "LowTempAlarmNotify", LowTempAlarm.Notify);
			ini.SetValue("Alarms", "LowTempAlarmEmail", LowTempAlarm.Email);
			ini.SetValue("Alarms", "LowTempAlarmLatch", LowTempAlarm.Latch);
			ini.SetValue("Alarms", "LowTempAlarmLatchHours", LowTempAlarm.LatchHours);
			ini.SetValue("Alarms", "LowTempAlarmAction", LowTempAlarm.Action);
			ini.SetValue("Alarms", "LowTempAlarmActionParams", LowTempAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhightemp", HighTempAlarm.Value);
			ini.SetValue("Alarms", "HighTempAlarmSet", HighTempAlarm.Enabled);
			ini.SetValue("Alarms", "HighTempAlarmSound", HighTempAlarm.Sound);
			ini.SetValue("Alarms", "HighTempAlarmSoundFile", HighTempAlarm.SoundFile);
			ini.SetValue("Alarms", "HighTempAlarmNotify", HighTempAlarm.Notify);
			ini.SetValue("Alarms", "HighTempAlarmEmail", HighTempAlarm.Email);
			ini.SetValue("Alarms", "HighTempAlarmLatch", HighTempAlarm.Latch);
			ini.SetValue("Alarms", "HighTempAlarmLatchHours", HighTempAlarm.LatchHours);
			ini.SetValue("Alarms", "HighTempAlarmAction", HighTempAlarm.Action);
			ini.SetValue("Alarms", "HighTempAlarmActionParams", HighTempAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmtempchange", TempChangeAlarm.Value);
			ini.SetValue("Alarms", "TempChangeAlarmSet", TempChangeAlarm.Enabled);
			ini.SetValue("Alarms", "TempChangeAlarmSound", TempChangeAlarm.Sound);
			ini.SetValue("Alarms", "TempChangeAlarmSoundFile", TempChangeAlarm.SoundFile);
			ini.SetValue("Alarms", "TempChangeAlarmNotify", TempChangeAlarm.Notify);
			ini.SetValue("Alarms", "TempChangeAlarmEmail", TempChangeAlarm.Email);
			ini.SetValue("Alarms", "TempChangeAlarmLatch", TempChangeAlarm.Latch);
			ini.SetValue("Alarms", "TempChangeAlarmLatchHours", TempChangeAlarm.LatchHours);
			ini.SetValue("Alarms", "TempChangeAlarmAction", TempChangeAlarm.Action);
			ini.SetValue("Alarms", "TempChangeAlarmActionParams", TempChangeAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmlowpress", LowPressAlarm.Value);
			ini.SetValue("Alarms", "LowPressAlarmSet", LowPressAlarm.Enabled);
			ini.SetValue("Alarms", "LowPressAlarmSound", LowPressAlarm.Sound);
			ini.SetValue("Alarms", "LowPressAlarmSoundFile", LowPressAlarm.SoundFile);
			ini.SetValue("Alarms", "LowPressAlarmNotify", LowPressAlarm.Notify);
			ini.SetValue("Alarms", "LowPressAlarmEmail", LowPressAlarm.Email);
			ini.SetValue("Alarms", "LowPressAlarmLatch", LowPressAlarm.Latch);
			ini.SetValue("Alarms", "LowPressAlarmLatchHours", LowPressAlarm.LatchHours);
			ini.SetValue("Alarms", "LowPressAlarmAction", LowPressAlarm.Action);
			ini.SetValue("Alarms", "LowPressAlarmActionParams", LowPressAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhighpress", HighPressAlarm.Value);
			ini.SetValue("Alarms", "HighPressAlarmSet", HighPressAlarm.Enabled);
			ini.SetValue("Alarms", "HighPressAlarmSound", HighPressAlarm.Sound);
			ini.SetValue("Alarms", "HighPressAlarmSoundFile", HighPressAlarm.SoundFile);
			ini.SetValue("Alarms", "HighPressAlarmNotify", HighPressAlarm.Notify);
			ini.SetValue("Alarms", "HighPressAlarmEmail", HighPressAlarm.Email);
			ini.SetValue("Alarms", "HighPressAlarmLatch", HighPressAlarm.Latch);
			ini.SetValue("Alarms", "HighPressAlarmLatchHours", HighPressAlarm.LatchHours);
			ini.SetValue("Alarms", "HighPressAlarmAction", HighPressAlarm.Action);
			ini.SetValue("Alarms", "HighPressAlarmActionParams", HighPressAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmpresschange", PressChangeAlarm.Value);
			ini.SetValue("Alarms", "PressChangeAlarmSet", PressChangeAlarm.Enabled);
			ini.SetValue("Alarms", "PressChangeAlarmSound", PressChangeAlarm.Sound);
			ini.SetValue("Alarms", "PressChangeAlarmSoundFile", PressChangeAlarm.SoundFile);
			ini.SetValue("Alarms", "PressChangeAlarmNotify", PressChangeAlarm.Notify);
			ini.SetValue("Alarms", "PressChangeAlarmEmail", PressChangeAlarm.Email);
			ini.SetValue("Alarms", "PressChangeAlarmLatch", PressChangeAlarm.Latch);
			ini.SetValue("Alarms", "PressChangeAlarmLatchHours", PressChangeAlarm.LatchHours);
			ini.SetValue("Alarms", "PressChangeAlarmAction", PressChangeAlarm.Action);
			ini.SetValue("Alarms", "PressChangeAlarmActionParams", PressChangeAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhighraintoday", HighRainTodayAlarm.Value);
			ini.SetValue("Alarms", "HighRainTodayAlarmSet", HighRainTodayAlarm.Enabled);
			ini.SetValue("Alarms", "HighRainTodayAlarmSound", HighRainTodayAlarm.Sound);
			ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", HighRainTodayAlarm.SoundFile);
			ini.SetValue("Alarms", "HighRainTodayAlarmNotify", HighRainTodayAlarm.Notify);
			ini.SetValue("Alarms", "HighRainTodayAlarmEmail", HighRainTodayAlarm.Email);
			ini.SetValue("Alarms", "HighRainTodayAlarmLatch", HighRainTodayAlarm.Latch);
			ini.SetValue("Alarms", "HighRainTodayAlarmLatchHours", HighRainTodayAlarm.LatchHours);
			ini.SetValue("Alarms", "HighRainTodayAlarmAction", HighRainTodayAlarm.Action);
			ini.SetValue("Alarms", "HighRainTodayAlarmActionParams", HighRainTodayAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhighrainrate", HighRainRateAlarm.Value);
			ini.SetValue("Alarms", "HighRainRateAlarmSet", HighRainRateAlarm.Enabled);
			ini.SetValue("Alarms", "HighRainRateAlarmSound", HighRainRateAlarm.Sound);
			ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", HighRainRateAlarm.SoundFile);
			ini.SetValue("Alarms", "HighRainRateAlarmNotify", HighRainRateAlarm.Notify);
			ini.SetValue("Alarms", "HighRainRateAlarmEmail", HighRainRateAlarm.Email);
			ini.SetValue("Alarms", "HighRainRateAlarmLatch", HighRainRateAlarm.Latch);
			ini.SetValue("Alarms", "HighRainRateAlarmLatchHours", HighRainRateAlarm.LatchHours);
			ini.SetValue("Alarms", "HighRainRateAlarmAction", HighRainRateAlarm.Action);
			ini.SetValue("Alarms", "HighRainRateAlarmActionParams", HighRainRateAlarm.ActionParams);

			ini.SetValue("Alarms", "IsRainingAlarmSet", IsRainingAlarm.Enabled);
			ini.SetValue("Alarms", "IsRainingAlarmSound", IsRainingAlarm.Sound);
			ini.SetValue("Alarms", "IsRainingAlarmSoundFile", IsRainingAlarm.SoundFile);
			ini.SetValue("Alarms", "IsRainingAlarmNotify", IsRainingAlarm.Notify);
			ini.SetValue("Alarms", "IsRainingAlarmEmail", IsRainingAlarm.Email);
			ini.SetValue("Alarms", "IsRainingAlarmLatch", IsRainingAlarm.Latch);
			ini.SetValue("Alarms", "IsRainingAlarmLatchHours", IsRainingAlarm.LatchHours);
			ini.SetValue("Alarms", "IsRainingAlarmTriggerCount", IsRainingAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "IsRainingAlarmAction", IsRainingAlarm.Action);
			ini.SetValue("Alarms", "IsRainingAlarmActionParams", IsRainingAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhighgust", HighGustAlarm.Value);
			ini.SetValue("Alarms", "HighGustAlarmSet", HighGustAlarm.Enabled);
			ini.SetValue("Alarms", "HighGustAlarmSound", HighGustAlarm.Sound);
			ini.SetValue("Alarms", "HighGustAlarmSoundFile", HighGustAlarm.SoundFile);
			ini.SetValue("Alarms", "HighGustAlarmNotify", HighGustAlarm.Notify);
			ini.SetValue("Alarms", "HighGustAlarmEmail", HighGustAlarm.Email);
			ini.SetValue("Alarms", "HighGustAlarmLatch", HighGustAlarm.Latch);
			ini.SetValue("Alarms", "HighGustAlarmLatchHours", HighGustAlarm.LatchHours);
			ini.SetValue("Alarms", "HighGustAlarmAction", HighGustAlarm.Action);
			ini.SetValue("Alarms", "HighGustAlarmActionParams", HighGustAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhighwind", HighWindAlarm.Value);
			ini.SetValue("Alarms", "HighWindAlarmSet", HighWindAlarm.Enabled);
			ini.SetValue("Alarms", "HighWindAlarmSound", HighWindAlarm.Sound);
			ini.SetValue("Alarms", "HighWindAlarmSoundFile", HighWindAlarm.SoundFile);
			ini.SetValue("Alarms", "HighWindAlarmNotify", HighWindAlarm.Notify);
			ini.SetValue("Alarms", "HighWindAlarmEmail", HighWindAlarm.Email);
			ini.SetValue("Alarms", "HighWindAlarmLatch", HighWindAlarm.Latch);
			ini.SetValue("Alarms", "HighWindAlarmLatchHours", HighWindAlarm.LatchHours);
			ini.SetValue("Alarms", "HighWindAlarmAction", HighWindAlarm.Action);
			ini.SetValue("Alarms", "HighWindAlarmActionParams", HighWindAlarm.ActionParams);

			ini.SetValue("Alarms", "SensorAlarmSet", SensorAlarm.Enabled);
			ini.SetValue("Alarms", "SensorAlarmSound", SensorAlarm.Sound);
			ini.SetValue("Alarms", "SensorAlarmSoundFile", SensorAlarm.SoundFile);
			ini.SetValue("Alarms", "SensorAlarmNotify", SensorAlarm.Notify);
			ini.SetValue("Alarms", "SensorAlarmEmail", SensorAlarm.Email);
			ini.SetValue("Alarms", "SensorAlarmLatch", SensorAlarm.Latch);
			ini.SetValue("Alarms", "SensorAlarmLatchHours", SensorAlarm.LatchHours);
			ini.SetValue("Alarms", "SensorAlarmTriggerCount", SensorAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "SensorAlarmAction", SensorAlarm.Action);
			ini.SetValue("Alarms", "SensorAlarmActionParams", SensorAlarm.ActionParams);


			ini.SetValue("Alarms", "DataStoppedAlarmSet", DataStoppedAlarm.Enabled);
			ini.SetValue("Alarms", "DataStoppedAlarmSound", DataStoppedAlarm.Sound);
			ini.SetValue("Alarms", "DataStoppedAlarmSoundFile", DataStoppedAlarm.SoundFile);
			ini.SetValue("Alarms", "DataStoppedAlarmNotify", DataStoppedAlarm.Notify);
			ini.SetValue("Alarms", "DataStoppedAlarmEmail", DataStoppedAlarm.Email);
			ini.SetValue("Alarms", "DataStoppedAlarmLatch", DataStoppedAlarm.Latch);
			ini.SetValue("Alarms", "DataStoppedAlarmLatchHours", DataStoppedAlarm.LatchHours);
			ini.SetValue("Alarms", "DataStoppedAlarmTriggerCount", DataStoppedAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "DataStoppedAlarmAction", DataStoppedAlarm.Action);
			ini.SetValue("Alarms", "DataStoppedAlarmActionParams", DataStoppedAlarm.ActionParams);

			ini.SetValue("Alarms", "BatteryLowAlarmSet", BatteryLowAlarm.Enabled);
			ini.SetValue("Alarms", "BatteryLowAlarmSound", BatteryLowAlarm.Sound);
			ini.SetValue("Alarms", "BatteryLowAlarmSoundFile", BatteryLowAlarm.SoundFile);
			ini.SetValue("Alarms", "BatteryLowAlarmNotify", BatteryLowAlarm.Notify);
			ini.SetValue("Alarms", "BatteryLowAlarmEmail", BatteryLowAlarm.Email);
			ini.SetValue("Alarms", "BatteryLowAlarmLatch", BatteryLowAlarm.Latch);
			ini.SetValue("Alarms", "BatteryLowAlarmLatchHours", BatteryLowAlarm.LatchHours);
			ini.SetValue("Alarms", "BatteryLowAlarmTriggerCount", BatteryLowAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "BatteryLowAlarmAction", BatteryLowAlarm.Action);
			ini.SetValue("Alarms", "BatteryLowAlarmActionParams", BatteryLowAlarm.ActionParams);

			ini.SetValue("Alarms", "DataSpikeAlarmSet", SpikeAlarm.Enabled);
			ini.SetValue("Alarms", "DataSpikeAlarmSound", SpikeAlarm.Sound);
			ini.SetValue("Alarms", "DataSpikeAlarmSoundFile", SpikeAlarm.SoundFile);
			ini.SetValue("Alarms", "DataSpikeAlarmNotify", SpikeAlarm.Notify);
			ini.SetValue("Alarms", "DataSpikeAlarmEmail", SpikeAlarm.Email);
			ini.SetValue("Alarms", "DataSpikeAlarmLatch", SpikeAlarm.Latch);
			ini.SetValue("Alarms", "DataSpikeAlarmLatchHours", SpikeAlarm.LatchHours);
			ini.SetValue("Alarms", "DataSpikeAlarmTriggerCount", SpikeAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "DataSpikeAlarmAction", SpikeAlarm.Action);
			ini.SetValue("Alarms", "DataSpikeAlarmActionParams", SpikeAlarm.ActionParams);

			ini.SetValue("Alarms", "UpgradeAlarmSet", UpgradeAlarm.Enabled);
			ini.SetValue("Alarms", "UpgradeAlarmSound", UpgradeAlarm.Sound);
			ini.SetValue("Alarms", "UpgradeAlarmSoundFile", UpgradeAlarm.SoundFile);
			ini.SetValue("Alarms", "UpgradeAlarmNotify", UpgradeAlarm.Notify);
			ini.SetValue("Alarms", "UpgradeAlarmEmail", UpgradeAlarm.Email);
			ini.SetValue("Alarms", "UpgradeAlarmLatch", UpgradeAlarm.Latch);
			ini.SetValue("Alarms", "UpgradeAlarmLatchHours", UpgradeAlarm.LatchHours);
			ini.SetValue("Alarms", "UpgradeAlarmAction", UpgradeAlarm.Action);
			ini.SetValue("Alarms", "UpgradeAlarmActionParams", UpgradeAlarm.ActionParams);

			ini.SetValue("Alarms", "HttpUploadAlarmSet", HttpUploadAlarm.Enabled);
			ini.SetValue("Alarms", "HttpUploadAlarmSound", HttpUploadAlarm.Sound);
			ini.SetValue("Alarms", "HttpUploadAlarmSoundFile", HttpUploadAlarm.SoundFile);
			ini.SetValue("Alarms", "HttpUploadAlarmNotify", HttpUploadAlarm.Notify);
			ini.SetValue("Alarms", "HttpUploadAlarmEmail", HttpUploadAlarm.Email);
			ini.SetValue("Alarms", "HttpUploadAlarmLatch", HttpUploadAlarm.Latch);
			ini.SetValue("Alarms", "HttpUploadAlarmLatchHours", HttpUploadAlarm.LatchHours);
			ini.SetValue("Alarms", "HttpUploadAlarmTriggerCount", HttpUploadAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "HttpUploadAlarmAction", HttpUploadAlarm.Action);
			ini.SetValue("Alarms", "HttpUploadAlarmActionParams", HttpUploadAlarm.ActionParams);

			ini.SetValue("Alarms", "MySqlUploadAlarmSet", MySqlUploadAlarm.Enabled);
			ini.SetValue("Alarms", "MySqlUploadAlarmSound", MySqlUploadAlarm.Sound);
			ini.SetValue("Alarms", "MySqlUploadAlarmSoundFile", MySqlUploadAlarm.SoundFile);
			ini.SetValue("Alarms", "MySqlUploadAlarmNotify", MySqlUploadAlarm.Notify);
			ini.SetValue("Alarms", "MySqlUploadAlarmEmail", MySqlUploadAlarm.Email);
			ini.SetValue("Alarms", "MySqlUploadAlarmLatch", MySqlUploadAlarm.Latch);
			ini.SetValue("Alarms", "MySqlUploadAlarmLatchHours", MySqlUploadAlarm.LatchHours);
			ini.SetValue("Alarms", "MySqlUploadAlarmTriggerCount", MySqlUploadAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "MySqlUploadAlarmAction", MySqlUploadAlarm.Action);
			ini.SetValue("Alarms", "MySqlUploadAlarmActionParams", MySqlUploadAlarm.ActionParams);

			ini.SetValue("Alarms", "FromEmail", AlarmFromEmail);
			ini.SetValue("Alarms", "DestEmail", AlarmDestEmail.Join(";"));
			ini.SetValue("Alarms", "UseHTML", AlarmEmailHtml);


			ini.SetValue("Offsets", "PressOffset", Calib.Press.Offset);
			ini.SetValue("Offsets", "TempOffset", Calib.Temp.Offset);
			ini.SetValue("Offsets", "HumOffset", Calib.Hum.Offset);
			ini.SetValue("Offsets", "WindDirOffset", Calib.WindDir.Offset);
			ini.SetValue("Offsets", "UVOffset", Calib.UV.Offset);
			ini.SetValue("Offsets", "SolarOffset", Calib.Solar.Offset);
			ini.SetValue("Offsets", "WetBulbOffset", Calib.WetBulb.Offset);
			//ini.SetValue("Offsets", "DavisCalcAltPressOffset", DavisCalcAltPressOffset);
			ini.SetValue("Offsets", "InTempOffset", Calib.InTemp.Offset);
			ini.SetValue("Offsets", "InHumOffset", Calib.InHum.Offset);

			ini.SetValue("Offsets", "PressMult", Calib.Press.Mult);
			ini.SetValue("Offsets", "WindSpeedMult", Calib.WindSpeed.Mult);
			ini.SetValue("Offsets", "WindGustMult", Calib.WindGust.Mult);
			ini.SetValue("Offsets", "TempMult", Calib.Temp.Mult);
			ini.SetValue("Offsets", "HumMult", Calib.Hum.Mult);
			ini.SetValue("Offsets", "RainMult", Calib.Rain.Mult);
			ini.SetValue("Offsets", "SolarMult", Calib.Solar.Mult);
			ini.SetValue("Offsets", "UVMult", Calib.UV.Mult);
			ini.SetValue("Offsets", "WetBulbMult", Calib.WetBulb.Mult);
			ini.SetValue("Offsets", "InTempMult", Calib.InTemp.Mult);
			ini.SetValue("Offsets", "InHumMult", Calib.InHum.Mult);

			ini.SetValue("Offsets", "PressMult2", Calib.Press.Mult2);
			ini.SetValue("Offsets", "WindSpeedMult2", Calib.WindSpeed.Mult2);
			ini.SetValue("Offsets", "WindGustMult2", Calib.WindGust.Mult2);
			ini.SetValue("Offsets", "TempMult2", Calib.Temp.Mult2);
			ini.SetValue("Offsets", "HumMult2", Calib.Hum.Mult2);
			ini.SetValue("Offsets", "InTempMult2", Calib.InTemp.Mult2);
			ini.SetValue("Offsets", "InHumMult2", Calib.InHum.Mult2);
			ini.SetValue("Offsets", "SolarMult2", Calib.Solar.Mult2);
			ini.SetValue("Offsets", "UVMult2", Calib.UV.Mult2);

			ini.SetValue("Limits", "TempHighC", Limit.TempHigh);
			ini.SetValue("Limits", "TempLowC", Limit.TempLow);
			ini.SetValue("Limits", "DewHighC", Limit.DewHigh);
			ini.SetValue("Limits", "PressHighMB", Limit.PressHigh);
			ini.SetValue("Limits", "PressLowMB", Limit.PressLow);
			ini.SetValue("Limits", "WindHighMS", Limit.WindHigh);

			ini.SetValue("xAP", "Enabled", xapEnabled);
			ini.SetValue("xAP", "UID", xapUID);
			ini.SetValue("xAP", "Port", xapPort);

			ini.SetValue("Solar", "SunThreshold", SolarOptions.SunThreshold);
			ini.SetValue("Solar", "SolarMinimum", SolarOptions.SolarMinimum);
			ini.SetValue("Solar", "UseBlakeLarsen", SolarOptions.UseBlakeLarsen);
			ini.SetValue("Solar", "SolarCalc", SolarOptions.SolarCalc);
			ini.SetValue("Solar", "LuxToWM2", SolarOptions.LuxToWM2);
			ini.SetValue("Solar", "RStransfactorJun", SolarOptions.RStransfactorJun);
			ini.SetValue("Solar", "RStransfactorDec", SolarOptions.RStransfactorDec);
			ini.SetValue("Solar", "BrasTurbidityJun", SolarOptions.BrasTurbidityJun);
			ini.SetValue("Solar", "BrasTurbidityDec", SolarOptions.BrasTurbidityDec);

			ini.SetValue("NOAA", "Name", NOAAconf.Name);
			ini.SetValue("NOAA", "City", NOAAconf.City);
			ini.SetValue("NOAA", "State", NOAAconf.State);
			ini.SetValue("NOAA", "12hourformat", NOAAconf.Use12hour);
			ini.SetValue("NOAA", "HeatingThreshold", NOAAconf.HeatThreshold);
			ini.SetValue("NOAA", "CoolingThreshold", NOAAconf.CoolThreshold);
			ini.SetValue("NOAA", "MaxTempComp1", NOAAconf.MaxTempComp1);
			ini.SetValue("NOAA", "MaxTempComp2", NOAAconf.MaxTempComp2);
			ini.SetValue("NOAA", "MinTempComp1", NOAAconf.MinTempComp1);
			ini.SetValue("NOAA", "MinTempComp2", NOAAconf.MinTempComp2);
			ini.SetValue("NOAA", "RainComp1", NOAAconf.RainComp1);
			ini.SetValue("NOAA", "RainComp2", NOAAconf.RainComp2);
			ini.SetValue("NOAA", "RainComp3", NOAAconf.RainComp3);
			ini.SetValue("NOAA", "AutoSave", NOAAconf.Create);
			ini.SetValue("NOAA", "AutoFTP", NOAAconf.AutoFtp);
			ini.SetValue("NOAA", "FTPDirectory", NOAAconf.FtpFolder);
			ini.SetValue("NOAA", "AutoCopy", NOAAconf.AutoCopy);
			ini.SetValue("NOAA", "CopyDirectory", NOAAconf.CopyFolder);
			ini.SetValue("NOAA", "MonthFileFormat", NOAAconf.MonthFile);
			ini.SetValue("NOAA", "YearFileFormat", NOAAconf.YearFile);
			ini.SetValue("NOAA", "NOAAUseUTF8", NOAAconf.UseUtf8);
			ini.SetValue("NOAA", "UseDotDecimal", NOAAconf.UseDotDecimal);
			ini.SetValue("NOAA", "UseNoaaHeatCoolDays", NOAAconf.UseNoaaHeatCoolDays);
			ini.SetValue("NOAA", "UseMinMaxAvg", NOAAconf.UseMinMaxAvg);

			ini.SetValue("NOAA", "NOAATempNormJan", NOAAconf.TempNorms[1]);
			ini.SetValue("NOAA", "NOAATempNormFeb", NOAAconf.TempNorms[2]);
			ini.SetValue("NOAA", "NOAATempNormMar", NOAAconf.TempNorms[3]);
			ini.SetValue("NOAA", "NOAATempNormApr", NOAAconf.TempNorms[4]);
			ini.SetValue("NOAA", "NOAATempNormMay", NOAAconf.TempNorms[5]);
			ini.SetValue("NOAA", "NOAATempNormJun", NOAAconf.TempNorms[6]);
			ini.SetValue("NOAA", "NOAATempNormJul", NOAAconf.TempNorms[7]);
			ini.SetValue("NOAA", "NOAATempNormAug", NOAAconf.TempNorms[8]);
			ini.SetValue("NOAA", "NOAATempNormSep", NOAAconf.TempNorms[9]);
			ini.SetValue("NOAA", "NOAATempNormOct", NOAAconf.TempNorms[10]);
			ini.SetValue("NOAA", "NOAATempNormNov", NOAAconf.TempNorms[11]);
			ini.SetValue("NOAA", "NOAATempNormDec", NOAAconf.TempNorms[12]);

			ini.SetValue("NOAA", "NOAARainNormJan", NOAAconf.RainNorms[1]);
			ini.SetValue("NOAA", "NOAARainNormFeb", NOAAconf.RainNorms[2]);
			ini.SetValue("NOAA", "NOAARainNormMar", NOAAconf.RainNorms[3]);
			ini.SetValue("NOAA", "NOAARainNormApr", NOAAconf.RainNorms[4]);
			ini.SetValue("NOAA", "NOAARainNormMay", NOAAconf.RainNorms[5]);
			ini.SetValue("NOAA", "NOAARainNormJun", NOAAconf.RainNorms[6]);
			ini.SetValue("NOAA", "NOAARainNormJul", NOAAconf.RainNorms[7]);
			ini.SetValue("NOAA", "NOAARainNormAug", NOAAconf.RainNorms[8]);
			ini.SetValue("NOAA", "NOAARainNormSep", NOAAconf.RainNorms[9]);
			ini.SetValue("NOAA", "NOAARainNormOct", NOAAconf.RainNorms[10]);
			ini.SetValue("NOAA", "NOAARainNormNov", NOAAconf.RainNorms[11]);
			ini.SetValue("NOAA", "NOAARainNormDec", NOAAconf.RainNorms[12]);

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
			ini.SetValue("Graphs", "MoonImageEnabled", MoonImage.Enabled);
			ini.SetValue("Graphs", "MoonImageSize", MoonImage.Size);
			ini.SetValue("Graphs", "MoonImageFtpDest", MoonImage.FtpDest);
			ini.SetValue("Graphs", "MoonImageCopyDest", MoonImage.CopyDest);
			ini.SetValue("Graphs", "TempVisible", GraphOptions.Visible.Temp.Val);
			ini.SetValue("Graphs", "InTempVisible", GraphOptions.Visible.InTemp.Val);
			ini.SetValue("Graphs", "HIVisible", GraphOptions.Visible.HeatIndex.Val);
			ini.SetValue("Graphs", "DPVisible", GraphOptions.Visible.DewPoint.Val);
			ini.SetValue("Graphs", "WCVisible", GraphOptions.Visible.WindChill.Val);
			ini.SetValue("Graphs", "AppTempVisible", GraphOptions.Visible.AppTemp.Val);
			ini.SetValue("Graphs", "FeelsLikeVisible", GraphOptions.Visible.FeelsLike.Val);
			ini.SetValue("Graphs", "HumidexVisible", GraphOptions.Visible.Humidex.Val);
			ini.SetValue("Graphs", "InHumVisible", GraphOptions.Visible.InHum.Val);
			ini.SetValue("Graphs", "OutHumVisible", GraphOptions.Visible.OutHum.Val);
			ini.SetValue("Graphs", "UVVisible", GraphOptions.Visible.UV.Val);
			ini.SetValue("Graphs", "SolarVisible", GraphOptions.Visible.Solar.Val);
			ini.SetValue("Graphs", "SunshineVisible", GraphOptions.Visible.Sunshine.Val);
			ini.SetValue("Graphs", "DailyAvgTempVisible", GraphOptions.Visible.AvgTemp.Val);
			ini.SetValue("Graphs", "DailyMaxTempVisible", GraphOptions.Visible.MaxTemp.Val);
			ini.SetValue("Graphs", "DailyMinTempVisible", GraphOptions.Visible.MinTemp.Val);
			ini.SetValue("Graphs", "GrowingDegreeDaysVisible1", GraphOptions.Visible.GrowingDegreeDays1.Val);
			ini.SetValue("Graphs", "GrowingDegreeDaysVisible2", GraphOptions.Visible.GrowingDegreeDays2.Val);
			ini.SetValue("Graphs", "TempSumVisible0", GraphOptions.Visible.TempSum0.Val);
			ini.SetValue("Graphs", "TempSumVisible1", GraphOptions.Visible.TempSum1.Val);
			ini.SetValue("Graphs", "TempSumVisible2", GraphOptions.Visible.TempSum2.Val);
			ini.SetValue("Graphs", "ExtraTempVisible", GraphOptions.Visible.ExtraTemp.Vals);
			ini.SetValue("Graphs", "ExtraHumVisible", GraphOptions.Visible.ExtraHum.Vals);
			ini.SetValue("Graphs", "ExtraDewPointVisible", GraphOptions.Visible.ExtraDewPoint.Vals);
			ini.SetValue("Graphs", "SoilTempVisible", GraphOptions.Visible.SoilTemp.Vals);
			ini.SetValue("Graphs", "SoilMoistVisible", GraphOptions.Visible.SoilMoist.Vals);
			ini.SetValue("Graphs", "UserTempVisible", GraphOptions.Visible.UserTemp.Vals);
			ini.SetValue("Graphs", "LeafWetnessVisible", GraphOptions.Visible.LeafWetness.Vals);
			ini.SetValue("Graphs", "Aq-PmVisible", GraphOptions.Visible.AqSensor.Pm.Vals);
			ini.SetValue("Graphs", "Aq-PmAvgVisible", GraphOptions.Visible.AqSensor.PmAvg.Vals);
			ini.SetValue("Graphs", "Aq-TempVisible", GraphOptions.Visible.AqSensor.Temp.Vals);
			ini.SetValue("Graphs", "Aq-HumVisible", GraphOptions.Visible.AqSensor.Hum.Vals);
			ini.SetValue("Graphs", "CO2-CO2", GraphOptions.Visible.CO2Sensor.CO2.Val);
			ini.SetValue("Graphs", "CO2-CO2Avg", GraphOptions.Visible.CO2Sensor.CO2Avg.Val);
			ini.SetValue("Graphs", "CO2-Pm25", GraphOptions.Visible.CO2Sensor.Pm25.Val);
			ini.SetValue("Graphs", "CO2-Pm25Avg", GraphOptions.Visible.CO2Sensor.Pm25Avg.Val);
			ini.SetValue("Graphs", "CO2-Pm10", GraphOptions.Visible.CO2Sensor.Pm10.Val);
			ini.SetValue("Graphs", "CO2-Pm10Avg", GraphOptions.Visible.CO2Sensor.Pm10Avg.Val);
			ini.SetValue("Graphs", "CO2-Temp", GraphOptions.Visible.CO2Sensor.Temp.Val);
			ini.SetValue("Graphs", "CO2-Hum", GraphOptions.Visible.CO2Sensor.Hum.Val);

			ini.SetValue("GraphColours", "TempColour", GraphOptions.Colour.Temp);
			ini.SetValue("GraphColours", "InTempColour", GraphOptions.Colour.InTemp);
			ini.SetValue("GraphColours", "HIColour", GraphOptions.Colour.HeatIndex);
			ini.SetValue("GraphColours", "DPColour", GraphOptions.Colour.DewPoint);
			ini.SetValue("GraphColours", "WCColour", GraphOptions.Colour.WindChill);
			ini.SetValue("GraphColours", "AppTempColour", GraphOptions.Colour.AppTemp);
			ini.SetValue("GraphColours", "FeelsLikeColour", GraphOptions.Colour.FeelsLike);
			ini.SetValue("GraphColours", "HumidexColour", GraphOptions.Colour.Humidex);
			ini.SetValue("GraphColours", "InHumColour", GraphOptions.Colour.InHum);
			ini.SetValue("GraphColours", "OutHumColour", GraphOptions.Colour.OutHum);
			ini.SetValue("GraphColours", "PressureColour", GraphOptions.Colour.Press);
			ini.SetValue("GraphColours", "WindGustColour", GraphOptions.Colour.WindGust);
			ini.SetValue("GraphColours", "WindAvgColour", GraphOptions.Colour.WindAvg);
			ini.SetValue("GraphColours", "WindRunColour", GraphOptions.Colour.WindRun);
			ini.SetValue("GraphColours", "Rainfall", GraphOptions.Colour.Rainfall);
			ini.SetValue("GraphColours", "RainRate", GraphOptions.Colour.RainRate);
			ini.SetValue("GraphColours", "UVColour", GraphOptions.Colour.UV);
			ini.SetValue("GraphColours", "SolarColour", GraphOptions.Colour.Solar);
			ini.SetValue("GraphColours", "SolarTheoreticalColour", GraphOptions.Colour.SolarTheoretical);
			ini.SetValue("GraphColours", "SunshineColour", GraphOptions.Colour.Sunshine);
			ini.SetValue("GraphColours", "MaxTempColour", GraphOptions.Colour.MaxTemp);
			ini.SetValue("GraphColours", "AvgTempColour", GraphOptions.Colour.AvgTemp);
			ini.SetValue("GraphColours", "MinTempColour", GraphOptions.Colour.MinTemp);
			ini.SetValue("GraphColours", "MaxPressColour", GraphOptions.Colour.MaxPress);
			ini.SetValue("GraphColours", "MinPressColour", GraphOptions.Colour.MinPress);
			ini.SetValue("GraphColours", "MaxHumColour", GraphOptions.Colour.MaxOutHum);
			ini.SetValue("GraphColours", "MinHumColour", GraphOptions.Colour.MinOutHum);
			ini.SetValue("GraphColours", "MaxHIColour", GraphOptions.Colour.MaxHeatIndex);
			ini.SetValue("GraphColours", "MaxDPColour", GraphOptions.Colour.MaxDew);
			ini.SetValue("GraphColours", "MinDPColour", GraphOptions.Colour.MinDew);
			ini.SetValue("GraphColours", "MaxFeelsLikeColour", GraphOptions.Colour.MaxFeels);
			ini.SetValue("GraphColours", "MinFeelsLikeColour", GraphOptions.Colour.MinFeels);
			ini.SetValue("GraphColours", "MaxAppTempColour", GraphOptions.Colour.MaxApp);
			ini.SetValue("GraphColours", "MinAppTempColour", GraphOptions.Colour.MinApp);
			ini.SetValue("GraphColours", "MaxHumidexColour", GraphOptions.Colour.MaxHumidex);
			ini.SetValue("GraphColours", "Pm2p5Colour", GraphOptions.Colour.Pm2p5);
			ini.SetValue("GraphColours", "Pm10Colour", GraphOptions.Colour.Pm10);
			ini.SetValue("GraphColours", "ExtraTempColour", GraphOptions.Colour.ExtraTemp);
			ini.SetValue("GraphColours", "ExtraHumColour", GraphOptions.Colour.ExtraHum);
			ini.SetValue("GraphColours", "ExtraDewPointColour", GraphOptions.Colour.ExtraDewPoint);
			ini.SetValue("GraphColours", "SoilTempColour", GraphOptions.Colour.SoilTemp);
			ini.SetValue("GraphColours", "SoilMoistColour", GraphOptions.Colour.SoilMoist);
			ini.SetValue("GraphColours", "LeafWetness", GraphOptions.Colour.LeafWetness);

			ini.SetValue("GraphColours", "UserTempColour", GraphOptions.Colour.UserTemp);
			ini.SetValue("GraphColours", "CO2-CO2Colour", GraphOptions.Colour.CO2Sensor.CO2);
			ini.SetValue("GraphColours", "CO2-CO2AvgColour", GraphOptions.Colour.CO2Sensor.CO2Avg);
			ini.SetValue("GraphColours", "CO2-Pm25Colour", GraphOptions.Colour.CO2Sensor.Pm25);
			ini.SetValue("GraphColours", "CO2-Pm25AvgColour", GraphOptions.Colour.CO2Sensor.Pm25Avg);
			ini.SetValue("GraphColours", "CO2-Pm10Colour", GraphOptions.Colour.CO2Sensor.Pm10);
			ini.SetValue("GraphColours", "CO2-Pm10AvgColour", GraphOptions.Colour.CO2Sensor.Pm10Avg);
			ini.SetValue("GraphColours", "CO2-TempColour", GraphOptions.Colour.CO2Sensor.Temp);
			ini.SetValue("GraphColours", "CO2-HumColour", GraphOptions.Colour.CO2Sensor.Hum);

			ini.SetValue("MySQL", "Host", MySqlConnSettings.Server);
			ini.SetValue("MySQL", "Port", (int) MySqlConnSettings.Port);
			ini.SetValue("MySQL", "User", MySqlConnSettings.UserID);
			ini.SetValue("MySQL", "Pass", MySqlConnSettings.Password);
			ini.SetValue("MySQL", "Database", MySqlConnSettings.Database);
			ini.SetValue("MySQL", "MonthlyMySqlEnabled", MySqlSettings.Monthly.Enabled);
			ini.SetValue("MySQL", "RealtimeMySqlEnabled", MySqlSettings.Realtime.Enabled);
			ini.SetValue("MySQL", "RealtimeMySql1MinLimit", MySqlSettings.RealtimeLimit1Minute);
			ini.SetValue("MySQL", "DayfileMySqlEnabled", MySqlSettings.Dayfile.Enabled);
			ini.SetValue("MySQL", "UpdateOnEdit", MySqlSettings.UpdateOnEdit);
			ini.SetValue("MySQL", "BufferOnFailure", MySqlSettings.BufferOnfailure);


			ini.SetValue("MySQL", "MonthlyTable", MySqlSettings.Monthly.TableName);
			ini.SetValue("MySQL", "DayfileTable", MySqlSettings.Dayfile.TableName);
			ini.SetValue("MySQL", "RealtimeTable", MySqlSettings.Realtime.TableName);
			ini.SetValue("MySQL", "RealtimeRetention", MySqlSettings.RealtimeRetention);

			ini.SetValue("MySQL", "CustomMySqlSecondsEnabled", MySqlSettings.CustomSecs.Enabled);
			ini.SetValue("MySQL", "CustomMySqlMinutesEnabled", MySqlSettings.CustomMins.Enabled);
			ini.SetValue("MySQL", "CustomMySqlRolloverEnabled", MySqlSettings.CustomRollover.Enabled);

			ini.SetValue("MySQL", "CustomMySqlSecondsInterval", MySqlSettings.CustomSecs.Interval);
			ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIndex", CustomMySqlMinutesIntervalIndex);

			ini.SetValue("MySQL", "CustomMySqlSecondsCommandString", MySqlSettings.CustomSecs.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlMinutesCommandString", MySqlSettings.CustomMins.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlRolloverCommandString", MySqlSettings.CustomRollover.Commands[0]);

			for (var i = 1; i < 10; i++)
			{
				if (string.IsNullOrEmpty(MySqlSettings.CustomSecs.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlSecondsCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlSecondsCommandString" + i, MySqlSettings.CustomSecs.Commands[i]);

				if (string.IsNullOrEmpty(MySqlSettings.CustomMins.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlMinutesCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlMinutesCommandString" + i, MySqlSettings.CustomMins.Commands[i]);

				if (string.IsNullOrEmpty(MySqlSettings.CustomRollover.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlRolloverCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlRolloverCommandString" + i, MySqlSettings.CustomRollover.Commands[i]);
			}

			// MySql - Timed
			ini.SetValue("MySQL", "CustomMySqlTimedEnabled", MySqlSettings.CustomTimed.Enabled);

			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(MySqlSettings.CustomTimed.Commands[i]))
				{
					ini.DeleteValue("MySQL", "CustomMySqlTimedCommandString" + i);
					ini.DeleteValue("MySQL", "CustomMySqlTimedStartTime" + i);
					ini.DeleteValue("MySQL", "CustomMySqlTimedInterval" + i);
				}
				else
				{
					ini.SetValue("MySQL", "CustomMySqlTimedCommandString" + i, MySqlSettings.CustomTimed.Commands[i]);
					ini.SetValue("MySQL", "CustomMySqlTimedStartTime" + i, MySqlSettings.CustomTimed.GetStartTimeString(i));
					ini.SetValue("MySQL", "CustomMySqlTimedInterval" + i, MySqlSettings.CustomTimed.Intervals[i]);
				}
			}


			ini.SetValue("HTTP", "CustomHttpSecondsString", CustomHttpSecondsStrings[0]);
			ini.SetValue("HTTP", "CustomHttpMinutesString", CustomHttpMinutesStrings[0]);
			ini.SetValue("HTTP", "CustomHttpRolloverString", CustomHttpRolloverStrings[0]);

			for (var i = 1; i < 10; i++)
			{
				if (string.IsNullOrEmpty(CustomHttpSecondsStrings[i]))
					ini.DeleteValue("HTTP", "CustomHttpSecondsString" + i);
				else
					ini.SetValue("HTTP", "CustomHttpSecondsString" + i, CustomHttpSecondsStrings[i]);

				if (string.IsNullOrEmpty(CustomHttpMinutesStrings[i]))
					ini.DeleteValue("HTTP", "CustomHttpMinutesString" + i);
				else
					ini.SetValue("HTTP", "CustomHttpMinutesString" + i, CustomHttpMinutesStrings[i]);

				if (string.IsNullOrEmpty(CustomHttpRolloverStrings[i]))
					ini.DeleteValue("HTTP", "CustomHttpRolloverString" + i);
				else
					ini.SetValue("HTTP", "CustomHttpRolloverString" + i, CustomHttpRolloverStrings[i]);
			}

			ini.SetValue("HTTP", "CustomHttpSecondsEnabled", CustomHttpSecondsEnabled);
			ini.SetValue("HTTP", "CustomHttpMinutesEnabled", CustomHttpMinutesEnabled);
			ini.SetValue("HTTP", "CustomHttpRolloverEnabled", CustomHttpRolloverEnabled);

			ini.SetValue("HTTP", "CustomHttpSecondsInterval", CustomHttpSecondsInterval);
			ini.SetValue("HTTP", "CustomHttpMinutesIntervalIndex", CustomHttpMinutesIntervalIndex);

			// Http files
			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(HttpFilesConfig[i].Url) && string.IsNullOrEmpty(HttpFilesConfig[i].Remote))
				{
					ini.DeleteValue("HTTP", "HttpFileEnabled" + i);
					ini.DeleteValue("HTTP", "HttpFileUrl" + i);
					ini.DeleteValue("HTTP", "HttpFileRemote" + i);
					ini.DeleteValue("HTTP", "HttpFileInterval" + i);
					ini.DeleteValue("HTTP", "HttpFileUpload" + i);
					ini.DeleteValue("HTTP", "HttpFileTimed" + i);
					ini.DeleteValue("HTTP", "HttpFileStartTime" + i);
				}
				else
				{
					ini.SetValue("HTTP", "HttpFileEnabled" + i, HttpFilesConfig[i].Enabled);
					ini.SetValue("HTTP", "HttpFileUrl" + i, HttpFilesConfig[i].Url);
					ini.SetValue("HTTP", "HttpFileRemote" + i, HttpFilesConfig[i].Remote);
					ini.SetValue("HTTP", "HttpFileInterval" + i, HttpFilesConfig[i].Interval);
					ini.SetValue("HTTP", "HttpFileUpload" + i, HttpFilesConfig[i].Upload);
					ini.SetValue("HTTP", "HttpFileTimed" + i, HttpFilesConfig[i].Timed);
					ini.SetValue("HTTP", "HttpFileStartTime" + i, HttpFilesConfig[i].StartTimeString);
				}
			}

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
			ini.SetValue("SMTP", "IgnoreCertErrors", SmtpOptions.IgnoreCertErrors);

			// Growing Degree Days
			ini.SetValue("GrowingDD", "BaseTemperature1", GrowingBase1);
			ini.SetValue("GrowingDD", "BaseTemperature2", GrowingBase2);
			ini.SetValue("GrowingDD", "YearStarts", GrowingYearStarts);
			ini.SetValue("GrowingDD", "Cap30C", GrowingCap30C);

			// Temperature Sum
			ini.SetValue("TempSum", "TempSumYearStart", TempSumYearStarts);
			ini.SetValue("TempSum", "BaseTemperature1", TempSumBase1);
			ini.SetValue("TempSum", "BaseTemperature2", TempSumBase2);


			// Custom Daily Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(CustomDailyLogSettings[i].FileName) && string.IsNullOrEmpty(CustomDailyLogSettings[i].ContentString))
				{
					ini.DeleteValue("CustomLogs", "DailyEnabled" + i);
					ini.DeleteValue("CustomLogs", "DailyFilename" + i);
					ini.DeleteValue("CustomLogs", "DailyContent" + i);
				}
				else
				{
					ini.SetValue("CustomLogs", "DailyEnabled" + i, CustomDailyLogSettings[i].Enabled);
					ini.SetValue("CustomLogs", "DailyFilename" + i, CustomDailyLogSettings[i].FileName);
					ini.SetValue("CustomLogs", "DailyContent" + i, CustomDailyLogSettings[i].ContentString);
				}
			}

			// Custom Interval Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(CustomIntvlLogSettings[i].FileName) && string.IsNullOrEmpty(CustomIntvlLogSettings[i].ContentString))
				{
					ini.DeleteValue("CustomLogs", "IntervalEnabled" + i);
					ini.DeleteValue("CustomLogs", "IntervalFilename" + i);
					ini.DeleteValue("CustomLogs", "IntervalContent" + i);
					ini.DeleteValue("CustomLogs", "IntervalIdx" + i);
				}
				else
				{
					ini.SetValue("CustomLogs", "IntervalEnabled" + i, CustomIntvlLogSettings[i].Enabled);
					ini.SetValue("CustomLogs", "IntervalFilename" + i, CustomIntvlLogSettings[i].FileName);
					ini.SetValue("CustomLogs", "IntervalContent" + i, CustomIntvlLogSettings[i].ContentString);
					ini.SetValue("CustomLogs", "IntervalIdx" + i, CustomIntvlLogSettings[i].IntervalIdx);
				}
			}


			ini.Flush();

			LogMessage("Completed writing Cumulus.ini file");
		}

		private void ReadStringsFile()
		{
			IniFile ini = new IniFile("strings.ini");

			// forecast
			Trans.ForecastNotAvailable = ini.GetValue("Forecast", "notavailable", "Not available");

			Trans.Exceptional = ini.GetValue("Forecast", "exceptional", "Exceptional Weather");
			for (var i = 0; i <= 25; i++)
			{
				Trans.zForecast[i] = ini.GetValue("Forecast", "forecast" + (i + 1), Trans.zForecast[i]);
			}
			// moon phases
			Trans.NewMoon = ini.GetValue("MoonPhases", "Newmoon", "New Moon");
			Trans.WaxingCrescent = ini.GetValue("MoonPhases", "WaxingCrescent", "Waxing Crescent");
			Trans.FirstQuarter = ini.GetValue("MoonPhases", "FirstQuarter", "First Quarter");
			Trans.WaxingGibbous = ini.GetValue("MoonPhases", "WaxingGibbous", "Waxing Gibbous");
			Trans.FullMoon = ini.GetValue("MoonPhases", "Fullmoon", "Full Moon");
			Trans.WaningGibbous = ini.GetValue("MoonPhases", "WaningGibbous", "Waning Gibbous");
			Trans.LastQuarter = ini.GetValue("MoonPhases", "LastQuarter", "Last Quarter");
			Trans.WaningCrescent = ini.GetValue("MoonPhases", "WaningCrescent", "Waning Crescent");
			// Beaufort
			Trans.Calm = ini.GetValue("Beaufort", "Calm", "Calm");
			Trans.Lightair = ini.GetValue("Beaufort", "Lightair", "Light air");
			Trans.Lightbreeze = ini.GetValue("Beaufort", "Lightbreeze", "Light breeze");
			Trans.Gentlebreeze = ini.GetValue("Beaufort", "Gentlebreeze", "Gentle breeze");
			Trans.Moderatebreeze = ini.GetValue("Beaufort", "Moderatebreeze", "Moderate breeze");
			Trans.Freshbreeze = ini.GetValue("Beaufort", "Freshbreeze", "Fresh breeze");
			Trans.Strongbreeze = ini.GetValue("Beaufort", "Strongbreeze", "Strong breeze");
			Trans.Neargale = ini.GetValue("Beaufort", "Neargale", "Near gale");
			Trans.Gale = ini.GetValue("Beaufort", "Gale", "Gale");
			Trans.Stronggale = ini.GetValue("Beaufort", "Stronggale", "Strong gale");
			Trans.Storm = ini.GetValue("Beaufort", "Storm", "Storm");
			Trans.Violentstorm = ini.GetValue("Beaufort", "Violentstorm", "Violent storm");
			Trans.Hurricane = ini.GetValue("Beaufort", "Hurricane", "Hurricane");
			Trans.Unknown = ini.GetValue("Beaufort", "Unknown", "UNKNOWN");
			// trends
			Trans.Risingveryrapidly = ini.GetValue("Trends", "Risingveryrapidly", "Rising very rapidly");
			Trans.Risingquickly = ini.GetValue("Trends", "Risingquickly", "Rising quickly");
			Trans.Rising = ini.GetValue("Trends", "Rising", "Rising");
			Trans.Risingslowly = ini.GetValue("Trends", "Risingslowly", "Rising slowly");
			Trans.Steady = ini.GetValue("Trends", "Steady", "Steady");
			Trans.Fallingslowly = ini.GetValue("Trends", "Fallingslowly", "Falling slowly");
			Trans.Falling = ini.GetValue("Trends", "Falling", "Falling");
			Trans.Fallingquickly = ini.GetValue("Trends", "Fallingquickly", "Falling quickly");
			Trans.Fallingveryrapidly = ini.GetValue("Trends", "Fallingveryrapidly", "Falling very rapidly");
			// compass points
			Trans.compassp[0] = ini.GetValue("Compass", "N", "N");
			Trans.compassp[1] = ini.GetValue("Compass", "NNE", "NNE");
			Trans.compassp[2] = ini.GetValue("Compass", "NE", "NE");
			Trans.compassp[3] = ini.GetValue("Compass", "ENE", "ENE");
			Trans.compassp[4] = ini.GetValue("Compass", "E", "E");
			Trans.compassp[5] = ini.GetValue("Compass", "ESE", "ESE");
			Trans.compassp[6] = ini.GetValue("Compass", "SE", "SE");
			Trans.compassp[7] = ini.GetValue("Compass", "SSE", "SSE");
			Trans.compassp[8] = ini.GetValue("Compass", "S", "S");
			Trans.compassp[9] = ini.GetValue("Compass", "SSW", "SSW");
			Trans.compassp[10] = ini.GetValue("Compass", "SW", "SW");
			Trans.compassp[11] = ini.GetValue("Compass", "WSW", "WSW");
			Trans.compassp[12] = ini.GetValue("Compass", "W", "W");
			Trans.compassp[13] = ini.GetValue("Compass", "WNW", "WNW");
			Trans.compassp[14] = ini.GetValue("Compass", "NW", "NW");
			Trans.compassp[15] = ini.GetValue("Compass", "NNW", "NNW");

			for (var i = 0; i < 4; i++)
			{
				// air quality captions (for Extra Sensor Data screen)
				Trans.AirQualityCaptions[i] = ini.GetValue("AirQualityCaptions", "Sensor" + (i + 1), Trans.AirQualityCaptions[i]);
				Trans.AirQualityAvgCaptions[i] = ini.GetValue("AirQualityCaptions", "SensorAvg" + (i + 1), Trans.AirQualityAvgCaptions[1]);
			}

			for (var i = 0; i < 8; i++)
			{
				// leaf wetness captions (for Extra Sensor Data screen)
				Trans.LeafWetnessCaptions[i] = ini.GetValue("LeafWetnessCaptions", "Sensor" + (i + 1), Trans.LeafWetnessCaptions[i]);

				// User temperature captions (for Extra Sensor Data screen)
				Trans.UserTempCaptions[i] = ini.GetValue("UserTempCaptions", "Sensor" + (i + 1), Trans.UserTempCaptions[i]);
			}

			for (var i = 0; i < 10; i++)
			{
				// Extra temperature captions (for Extra Sensor Data screen)
				Trans.ExtraTempCaptions[i] = ini.GetValue("ExtraTempCaptions", "Sensor" + (i + 1), Trans.ExtraTempCaptions[i]);

				// Extra humidity captions (for Extra Sensor Data screen)
				Trans.ExtraHumCaptions[i] = ini.GetValue("ExtraHumCaptions", "Sensor" + (i + 1), Trans.ExtraHumCaptions[i]);

				// Extra dew point captions (for Extra Sensor Data screen)
				Trans.ExtraDPCaptions[i] = ini.GetValue("ExtraDPCaptions", "Sensor" + (i + 1), Trans.ExtraDPCaptions[i]);
			}

			for (var i = 0; i < 16; i++)
			{
				// soil temp captions (for Extra Sensor Data screen)
				Trans.SoilTempCaptions[i] = ini.GetValue("SoilTempCaptions", "Sensor" + (i + 1), Trans.SoilTempCaptions[i]);

				// soil moisture captions (for Extra Sensor Data screen)
				Trans.SoilMoistureCaptions[i] = ini.GetValue("SoilMoistureCaptions", "Sensor" + (i + 1), Trans.SoilMoistureCaptions[i]);
			}

			// CO2 captions - Ecowitt WH45 sensor
			Trans.CO2_CurrentCaption = ini.GetValue("CO2Captions", "CO2-Current", "CO&#8322 Current");
			Trans.CO2_24HourCaption = ini.GetValue("CO2Captions", "CO2-24hr", "CO&#8322 24h avg");
			Trans.CO2_pm2p5Caption = ini.GetValue("CO2Captions", "CO2-Pm2p5", "PM 2.5");
			Trans.CO2_pm2p5_24hrCaption = ini.GetValue("CO2Captions", "CO2-Pm2p5-24hr", "PM 2.5 24h avg");
			Trans.CO2_pm10Caption = ini.GetValue("CO2Captions", "CO2-Pm10", "PM 10");
			Trans.CO2_pm10_24hrCaption = ini.GetValue("CO2Captions", "CO2-Pm10-24hr", "PM 10 24h avg");


			Trans.thereWillBeMinSLessDaylightTomorrow = ini.GetValue("Solar", "LessDaylightTomorrow", "There will be {0}min {1}s less daylight tomorrow");
			Trans.thereWillBeMinSMoreDaylightTomorrow = ini.GetValue("Solar", "MoreDaylightTomorrow", "There will be {0}min {1}s more daylight tomorrow");

			// Davis forecast 1
			Trans.DavisForecast1[0] = ini.GetValue("DavisForecast1", "forecast1", Trans.DavisForecast1[0]);
			for (var i = 1; i <= 25; i++)
			{
				Trans.DavisForecast1[i] = ini.GetValue("DavisForecast1", "forecast" + (i + 1), Trans.DavisForecast1[i]) + " ";
			}
			Trans.DavisForecast1[26] = ini.GetValue("DavisForecast1", "forecast27", Trans.DavisForecast1[26]);

			// Davis forecast 2
			Trans.DavisForecast2[0] = ini.GetValue("DavisForecast2", "forecast1", Trans.DavisForecast2[0]);
			for (var i = 1; i <= 18; i++)
			{
				Trans.DavisForecast2[i] = ini.GetValue("DavisForecast2", "forecast" + (i + 1), Trans.DavisForecast2[i]) + " ";
			}

			// Davis forecast 3
			for (var i = 0; i <= 6; i++)
			{
				Trans.DavisForecast3[i] = ini.GetValue("DavisForecast3", "forecast" + (i + 1), Trans.DavisForecast3[i]);
			}

			// alarm emails
			Trans.AlarmEmailSubject = ini.GetValue("AlarmEmails", "subject", "Cumulus MX Alarm");
			Trans.AlarmEmailPreamble = ini.GetValue("AlarmEmails", "preamble", "A Cumulus MX alarm has been triggered.");
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
			HttpUploadAlarm.EmailMsg = ini.GetValue("AlarmEmails", "httpStopped", "HTTP uploads are failing.");
			MySqlUploadAlarm.EmailMsg = ini.GetValue("AlarmEmails", "mySqlStopped", "MySQL uploads are failing.");
			IsRainingAlarm.EmailMsg = ini.GetValue("AlarmEmails", "isRaining", "It has started to rain.");

			if (!File.Exists("strings.ini"))
			{
				WriteStringsFile();
			}
		}

		public void WriteStringsFile()
		{
			LogMessage("Writing strings.ini file");

			IniFile ini = new IniFile("strings.ini");

			// forecast
			ini.SetValue("Forecast", "notavailable", Trans.ForecastNotAvailable);

			ini.SetValue("Forecast", "exceptional", Trans.Exceptional);
			for (var i = 0; i <= 25; i++)
			{
				ini.SetValue("Forecast", "forecast" + (i + 1), Trans.zForecast[i]);
			}
			// moon phases
			ini.SetValue("MoonPhases", "Newmoon", Trans.NewMoon);
			ini.SetValue("MoonPhases", "WaxingCrescent", Trans.WaxingCrescent);
			ini.SetValue("MoonPhases", "FirstQuarter", Trans.FirstQuarter);
			ini.SetValue("MoonPhases", "WaxingGibbous", Trans.WaxingGibbous);
			ini.SetValue("MoonPhases", "Fullmoon", Trans.FullMoon);
			ini.SetValue("MoonPhases", "WaningGibbous", Trans.WaningGibbous);
			ini.SetValue("MoonPhases", "LastQuarter", Trans.LastQuarter);
			ini.SetValue("MoonPhases", "WaningCrescent", Trans.WaningCrescent);
			// Beaufort
			ini.SetValue("Beaufort", "Calm", Trans.Calm);
			ini.SetValue("Beaufort", "Lightair", Trans.Lightair);
			ini.SetValue("Beaufort", "Lightbreeze", Trans.Lightbreeze);
			ini.SetValue("Beaufort", "Gentlebreeze", Trans.Gentlebreeze);
			ini.SetValue("Beaufort", "Moderatebreeze", Trans.Moderatebreeze);
			ini.SetValue("Beaufort", "Freshbreeze", Trans.Freshbreeze);
			ini.SetValue("Beaufort", "Strongbreeze", Trans.Strongbreeze);
			ini.SetValue("Beaufort", "Neargale", Trans.Neargale);
			ini.SetValue("Beaufort", "Gale", Trans.Gale);
			ini.SetValue("Beaufort", "Stronggale", Trans.Stronggale);
			ini.SetValue("Beaufort", "Storm", Trans.Storm);
			ini.SetValue("Beaufort", "Violentstorm", Trans.Violentstorm);
			ini.SetValue("Beaufort", "Hurricane", Trans.Hurricane);
			ini.SetValue("Beaufort", "Unknown", Trans.Unknown);
			// trends
			ini.SetValue("Trends", "Risingveryrapidly", Trans.Risingveryrapidly);
			ini.SetValue("Trends", "Risingquickly", Trans.Risingquickly);
			ini.SetValue("Trends", "Rising", Trans.Rising);
			ini.SetValue("Trends", "Risingslowly", Trans.Risingslowly);
			ini.SetValue("Trends", "Steady", Trans.Steady);
			ini.SetValue("Trends", "Fallingslowly", Trans.Fallingslowly);
			ini.SetValue("Trends", "Falling", Trans.Falling);
			ini.SetValue("Trends", "Fallingquickly", Trans.Fallingquickly);
			ini.SetValue("Trends", "Fallingveryrapidly", Trans.Fallingveryrapidly);
			// compass points
			ini.SetValue("Compass", "N", Trans.compassp[0]);
			ini.SetValue("Compass", "NNE", Trans.compassp[1]);
			ini.SetValue("Compass", "NE", Trans.compassp[2]);
			ini.SetValue("Compass", "ENE", Trans.compassp[3]);
			ini.SetValue("Compass", "E", Trans.compassp[4]);
			ini.SetValue("Compass", "ESE", Trans.compassp[5]);
			ini.SetValue("Compass", "SE", Trans.compassp[6]);
			ini.SetValue("Compass", "SSE", Trans.compassp[7]);
			ini.SetValue("Compass", "S", Trans.compassp[8]);
			ini.SetValue("Compass", "SSW", Trans.compassp[9]);
			ini.SetValue("Compass", "SW", Trans.compassp[10]);
			ini.SetValue("Compass", "WSW", Trans.compassp[11]);
			ini.SetValue("Compass", "W", Trans.compassp[12]);
			ini.SetValue("Compass", "WNW", Trans.compassp[13]);
			ini.SetValue("Compass", "NW", Trans.compassp[14]);
			ini.SetValue("Compass", "NNW", Trans.compassp[15]);

			for (var i = 0; i < 4; i++)
			{
				// air quality captions (for Extra Sensor Data screen)
				ini.SetValue("AirQualityCaptions", "Sensor" + (i + 1), Trans.AirQualityCaptions[i]);
				ini.SetValue("AirQualityCaptions", "SensorAvg" + (i + 1), Trans.AirQualityAvgCaptions[1]);
			}

			for (var i = 0; i < 8; i++)
			{
				// leaf wetness captions (for Extra Sensor Data screen)
				ini.SetValue("LeafWetnessCaptions", "Sensor" + (i + 1), Trans.LeafWetnessCaptions[i]);

				// User temperature captions (for Extra Sensor Data screen)
				ini.SetValue("UserTempCaptions", "Sensor" + (i + 1), Trans.UserTempCaptions[i]);
			}

			for (var i = 0; i < 10; i++)
			{
				// Extra temperature captions (for Extra Sensor Data screen)
				ini.SetValue("ExtraTempCaptions", "Sensor" + (i + 1), Trans.ExtraTempCaptions[i]);

				// Extra humidity captions (for Extra Sensor Data screen)
				ini.SetValue("ExtraHumCaptions", "Sensor" + (i + 1), Trans.ExtraHumCaptions[i]);

				// Extra dew point captions (for Extra Sensor Data screen)
				ini.SetValue("ExtraDPCaptions", "Sensor" + (i + 1), Trans.ExtraDPCaptions[i]);
			}

			for (var i = 0; i < 16; i++)
			{
				// soil temp captions (for Extra Sensor Data screen)
				ini.SetValue("SoilTempCaptions", "Sensor" + (i + 1), Trans.SoilTempCaptions[i]);

				// soil moisture captions (for Extra Sensor Data screen)
				ini.SetValue("SoilMoistureCaptions", "Sensor" + (i + 1), Trans.SoilMoistureCaptions[i]);
			}

			// CO2 captions - Ecowitt WH45 sensor
			ini.SetValue("CO2Captions", "CO2-Current", Trans.CO2_CurrentCaption);
			ini.SetValue("CO2Captions", "CO2-24hr", Trans.CO2_24HourCaption);
			ini.SetValue("CO2Captions", "CO2-Pm2p5", Trans.CO2_pm2p5Caption);
			ini.SetValue("CO2Captions", "CO2-Pm2p5-24hr", Trans.CO2_pm2p5_24hrCaption);
			ini.SetValue("CO2Captions", "CO2-Pm10", Trans.CO2_pm10Caption);
			ini.SetValue("CO2Captions", "CO2-Pm10-24hr", Trans.CO2_pm10_24hrCaption);


			ini.SetValue("Solar", "LessDaylightTomorrow", Trans.thereWillBeMinSLessDaylightTomorrow);
			ini.SetValue("Solar", "MoreDaylightTomorrow", Trans.thereWillBeMinSMoreDaylightTomorrow);

			// Davis forecast 1
			for (var i = 0; i <= 26; i++)
			{
				ini.SetValue("DavisForecast1", "forecast" + (i + 1), Trans.DavisForecast1[i]);
			}

			// Davis forecast 2
			for (var i = 0; i <= 18; i++)
			{
				ini.SetValue("DavisForecast2", "forecast" + (i + 1), Trans.DavisForecast2[i]);
			}

			// Davis forecast 3
			for (var i = 0; i <= 6; i++)
			{
				ini.SetValue("DavisForecast3", "forecast" + (i + 1), Trans.DavisForecast3[i]);
			}

			// alarm emails
			ini.SetValue("AlarmEmails", "subject", Trans.AlarmEmailSubject);
			ini.SetValue("AlarmEmails", "preamble", Trans.AlarmEmailPreamble);
			ini.SetValue("AlarmEmails", "windGustAbove", HighGustAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "pressureAbove", HighPressAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "tempAbove", HighTempAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "pressBelow", LowPressAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "tempBelow", LowTempAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "pressDown", PressChangeAlarm.EmailMsgDn);
			ini.SetValue("AlarmEmails", "pressUp", PressChangeAlarm.EmailMsgUp);
			ini.SetValue("AlarmEmails", "rainAbove", HighRainTodayAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "rainRateAbove", HighRainRateAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "sensorLost", SensorAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "tempDown", TempChangeAlarm.EmailMsgDn);
			ini.SetValue("AlarmEmails", "tempUp", TempChangeAlarm.EmailMsgUp);
			ini.SetValue("AlarmEmails", "windAbove", HighWindAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "dataStopped", DataStoppedAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "batteryLow", BatteryLowAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "dataSpike", SpikeAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "upgrade", UpgradeAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "httpStopped", HttpUploadAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "mySqlStopped", MySqlUploadAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "isRaining", IsRainingAlarm.EmailMsg);

			ini.Flush();

			LogMessage("Completed writing strings.ini file");
		}


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

		public HttpFileProps[] HttpFilesConfig = new HttpFileProps[10];

		//public int MaxFTPconnectRetries { get; set; }

		public bool DeleteBeforeUpload { get; set; }

		public bool FTPRename { get; set; }

		public int UpdateInterval { get; set; }

		public Timer RealtimeTimer = new Timer();

		internal Timer CustomMysqlSecondsTimer;

		public bool WebIntervalEnabled { get; set; }

		public bool WebAutoUpdate { get; set; }

		public int WMR200TempChannel { get; set; }

		public int WMR928TempChannel { get; set; }

		public int RTdisconnectcount { get; set; }

		//public int VP2SleepInterval { get; set; }

		//public int VPClosedownTime { get; set; }
		public string AirLinkInIPAddr { get; set; }

		public string AirLinkOutIPAddr { get; set; }

		public bool AirLinkInEnabled { get; set; }

		public bool AirLinkOutEnabled { get; set; }

		public bool EcowittExtraEnabled { get; set; }
		public bool EcowittExtraUseSolar { get; set; }
		public bool EcowittExtraUseUv { get; set; }
		public bool EcowittExtraUseTempHum { get; set; }
		public bool EcowittExtraUseSoilTemp { get; set; }
		public bool EcowittExtraUseSoilMoist { get; set; }
		public bool EcowittExtraUseLeafWet { get; set; }
		public bool EcowittExtraUseUserTemp { get; set; }
		public bool EcowittExtraUseAQI { get; set; }
		public bool EcowittExtraUseCo2 { get; set; }
		public bool EcowittExtraUseLightning { get; set; }
		public bool EcowittExtraUseLeak { get; set; }
		public string EcowittApplicationKey { get; set; }
		public string EcowittUserApiKey { get; set; }
		public string EcowittMacAddress { get; set; }
		public bool EcowittSetCustomServer { get; set; }
		public string EcowittGatewayAddr { get; set; }
		public string EcowittLocalAddr { get; set; }
		public int EcowittCustomInterval { get; set; }
		public bool EcowittExtraSetCustomServer { get; set; }
		public string EcowittExtraGatewayAddr { get; set; }
		public string EcowittExtraLocalAddr { get; set; }
		public int EcowittExtraCustomInterval { get; set; }
		public string[] EcowittForwarders = new string[10];

		public int[] EcowittMapWN34 = new int[9];

		public bool AmbientExtraEnabled { get; set; }
		public bool AmbientExtraUseSolar { get; set; }
		public bool AmbientExtraUseUv { get; set; }
		public bool AmbientExtraUseTempHum { get; set; }
		public bool AmbientExtraUseSoilTemp { get; set; }
		public bool AmbientExtraUseSoilMoist { get; set; }
		//public bool AmbientExtraUseLeafWet { get; set; }
		public bool AmbientExtraUseAQI { get; set; }
		public bool AmbientExtraUseCo2 { get; set; }
		public bool AmbientExtraUseLightning { get; set; }
		public bool AmbientExtraUseLeak { get; set; }

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

		//public bool UseWindChillCutoff { get; set; }

		public bool HourlyForecast { get; set; }

		public bool UseCumulusForecast { get; set; }

		public bool UseDataLogger { get; set; }

		public bool DavisConsoleHighGust { get; set; }

		public bool DavisCalcAltPress { get; set; }

		public bool DavisUseDLLBarCalData { get; set; }

		public int LCMaxWind { get; set; }

		//public bool EWduplicatecheck { get; set; }

		public DateTime RecordsBeganDateTime { get; set; }

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

		public string LonTxt { get; set; }

		public string LatTxt { get; set; }

		public bool AltitudeInFeet { get; set; }

		public string StationModel { get; set; }

		public int StationType { get; set; }

		public string LatestImetReading { get; set; }

		public bool FineOffsetStation { get; set; }

		public bool DavisStation { get; set; }
		public string TempTrendFormat { get; set; }
		public string PressTrendFormat { get; set; }
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
		public bool WllTriggerDataStoppedOnBroadcast; // trigger a data stopped state if broadcasts stop being received but current data is OK
		/// <value>Read-only setting, default 20 minutes (1200 sec)</value>
		public int WllBroadcastDuration = 1200;
		/// <value>Read-only setting, default 22222</value>
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

		public int[] WllExtraTempTx = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		public bool[] WllExtraHumTx = { false, false, false, false, false, false, false, false, false };

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
		public int Gw1000PrimaryTHSensor;
		public int Gw1000PrimaryRainSensor;

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
		public int HTTPSTATION = 7;
		public int AMBIENT = 8;
		public int WEATHERFLOW = 9;
		public int SIMULATOR = 10;

		//public bool startingup = true;
		public string ReportPath;
		public string LatestError;
		public DateTime LatestErrorTS = DateTime.MinValue;
		//public DateTime defaultRecordTS = new DateTime(2000, 1, 1, 0, 0, 0);
		public DateTime defaultRecordTS = DateTime.MinValue;
		public string WxnowFile = "wxnow.txt";
		private readonly string RealtimeFile = "realtime.txt";
		private FtpClient RealtimeFTP;
		private SftpClient RealtimeSSH;
		private volatile bool RealtimeFtpInProgress;
		private volatile bool RealtimeCopyInProgress;
		private volatile bool RealtimeFtpReconnecting;
		private byte RealtimeCycleCounter;

		public FileGenerationOptions[] StdWebFiles;
		public FileGenerationOptions[] RealtimeFiles;
		public FileGenerationOptions[] GraphDataFiles;
		public FileGenerationOptions[] GraphDataEodFiles;


//		private WebSocketServer wsServer;

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
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = thedate.AddHours(GetHourInc(thedate));

			var datestring = logfiledate.ToString("MMMyy").Replace(".", "");

			return Datapath + datestring + "log.txt";
		}

		public string GetExtraLogFileName(DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = thedate.AddHours(GetHourInc(thedate));

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Datapath + "ExtraLog" + datestring + ".txt";
		}

		public string GetAirLinkLogFileName(DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = thedate.AddHours(GetHourInc(thedate));

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Datapath + "AirLink" + datestring + "log.txt";
		}

		public string GetCustomIntvlLogFileName(int idx, DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = thedate.AddHours(GetHourInc(thedate));

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Datapath + CustomIntvlLogSettings[idx].FileName + "-" + datestring + ".txt";
		}

		public string GetCustomDailyLogFileName(int idx)
		{
			return Datapath + CustomDailyLogSettings[idx].FileName + ".txt";
		}

		public const int NumLogFileFields = 29;

		public async void DoLogFile(DateTime timestamp, bool live)
		{
			// Writes an entry to the n-minute log file. Fields are comma-separated:
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
			station.CurrentSolarMax = AstroLib.SolarMax(timestamp, (double) Longitude, (double) Latitude, station.AltitudeM(Altitude), out station.SolarElevation, SolarOptions);
			var filename = GetLogFileName(timestamp);

			var sb = new StringBuilder(256);
			sb.Append(timestamp.ToString("dd/MM/yy") + ListSeparator);
			sb.Append(timestamp.ToString("HH:mm") + ListSeparator);
			sb.Append(station.OutdoorTemperature.ToString(TempFormat) + ListSeparator);
			sb.Append(station.OutdoorHumidity + ListSeparator);
			sb.Append(station.OutdoorDewpoint.ToString(TempFormat) + ListSeparator);
			sb.Append(station.WindAverage.ToString(WindAvgFormat) + ListSeparator);
			sb.Append(station.RecentMaxGust.ToString(WindFormat) + ListSeparator);
			sb.Append(station.AvgBearing + ListSeparator);
			sb.Append(station.RainRate.ToString(RainFormat) + ListSeparator);
			sb.Append(station.RainToday.ToString(RainFormat) + ListSeparator);
			sb.Append(station.Pressure.ToString(PressFormat) + ListSeparator);
			sb.Append(station.Raincounter.ToString(RainFormat) + ListSeparator);
			sb.Append(station.IndoorTemperature.ToString(TempFormat) + ListSeparator);
			sb.Append(station.IndoorHumidity + ListSeparator);
			sb.Append(station.WindLatest.ToString(WindFormat) + ListSeparator);
			sb.Append(station.WindChill.ToString(TempFormat) + ListSeparator);
			sb.Append(station.HeatIndex.ToString(TempFormat) + ListSeparator);
			sb.Append(station.UV.ToString(UVFormat) + ListSeparator);
			sb.Append(station.SolarRad + ListSeparator);
			sb.Append(station.ET.ToString(ETFormat) + ListSeparator);
			sb.Append(station.AnnualETTotal.ToString(ETFormat) + ListSeparator);
			sb.Append(station.ApparentTemperature.ToString(TempFormat) + ListSeparator);
			sb.Append(Math.Round(station.CurrentSolarMax) + ListSeparator);
			sb.Append(station.SunshineHours.ToString(SunFormat) + ListSeparator);
			sb.Append(station.Bearing + ListSeparator);
			sb.Append(station.RG11RainToday.ToString(RainFormat) + ListSeparator);
			sb.Append(station.RainSinceMidnight.ToString(RainFormat) + ListSeparator);
			sb.Append(station.FeelsLike.ToString(TempFormat) + ListSeparator);
			sb.Append(station.Humidex.ToString(TempFormat));

			var success = false;
			var retries = LogFileRetries;
			do
			{
				try
				{
					using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
					using (StreamWriter file = new StreamWriter(fs))
					{
						await file.WriteLineAsync(sb.ToString());
						file.Close();
						fs.Close();
					}

					success = true;

					LastUpdateTime = timestamp;
					LogMessage($"DoLogFile: log entry for {timestamp} written");
				}
				catch (Exception ex)
				{
					LogErrorMessage($"DoLogFile: Error writing entry for {timestamp} - {ex.Message}");
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);


			station.WriteTodayFile(timestamp, true);

			if (MySqlSettings.Monthly.Enabled)
			{
				var InvC = new CultureInfo("");

				MySqlILastntervalTime = timestamp;

				StringBuilder values = new StringBuilder(MonthlyTable.StartOfInsert, 600);
				values.Append(" Values('");
				values.Append(timestamp.ToString("yyyy-MM-dd HH:mm") + "',");
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
				values.Append(')');

				string queryString = values.ToString();

				if (live)
				{
					// do the update
					_ = CheckMySQLFailedUploads("DoLogFile", queryString);
				}
				else
				{
					// save the string for later
					MySqlList.Enqueue(new SqlCache() { statement = queryString });
				}
			}
		}

		public const int NumExtraLogFileFields = 92;

		public async void DoExtraLogFile(DateTime timestamp)
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

			LogDebugMessage($"DoExtraLogFile: Writing log entry for {timestamp}");

			var sb = new StringBuilder(512);
			sb.Append(timestamp.ToString("dd/MM/yy") + ListSeparator);                     //0
			sb.Append(timestamp.ToString("HH:mm") + ListSeparator);                        //1

			for (int i = 1; i <= 10; i++)
			{
				sb.Append(station.ExtraTemp[i].ToString(TempFormat) + ListSeparator);      //2-11
			}
			for (int i = 1; i <= 10; i++)
			{
				sb.Append(station.ExtraHum[i].ToString(HumFormat) + ListSeparator);        //12-21
			}
			for (int i = 1; i <= 10; i++)
			{
				sb.Append(station.ExtraDewPoint[i].ToString(TempFormat) + ListSeparator);  //22-31
			}
			for (int i = 1; i <= 4; i++)
			{
				sb.Append(station.SoilTemp[i].ToString(TempFormat) + ListSeparator);     //32-35
			}

			sb.Append(station.SoilMoisture1 + ListSeparator);                      //36
			sb.Append(station.SoilMoisture2 + ListSeparator);                      //37
			sb.Append(station.SoilMoisture3 + ListSeparator);                      //38
			sb.Append(station.SoilMoisture4 + ListSeparator);                      //39

			sb.Append("0" + ListSeparator);     //40 - was leaf temp 1
			sb.Append("0" + ListSeparator);     //41 - was leaf temp 2

			sb.Append(station.LeafWetness1.ToString(LeafWetFormat) + ListSeparator);	//42
			sb.Append(station.LeafWetness2.ToString(LeafWetFormat) + ListSeparator);    //43

			for (int i = 5; i <= 16; i++)
			{
				sb.Append(station.SoilTemp[i].ToString(TempFormat) + ListSeparator);     //44-55
			}

			sb.Append(station.SoilMoisture5 + ListSeparator);      //56
			sb.Append(station.SoilMoisture6 + ListSeparator);      //57
			sb.Append(station.SoilMoisture7 + ListSeparator);      //58
			sb.Append(station.SoilMoisture8 + ListSeparator);      //59
			sb.Append(station.SoilMoisture9 + ListSeparator);      //60
			sb.Append(station.SoilMoisture10 + ListSeparator);     //61
			sb.Append(station.SoilMoisture11 + ListSeparator);     //62
			sb.Append(station.SoilMoisture12 + ListSeparator);     //63
			sb.Append(station.SoilMoisture13 + ListSeparator);     //64
			sb.Append(station.SoilMoisture14 + ListSeparator);     //65
			sb.Append(station.SoilMoisture15 + ListSeparator);     //66
			sb.Append(station.SoilMoisture16 + ListSeparator);     //67

			sb.Append(station.AirQuality1.ToString("F1") + ListSeparator);     //68
			sb.Append(station.AirQuality2.ToString("F1") + ListSeparator);     //69
			sb.Append(station.AirQuality3.ToString("F1") + ListSeparator);     //70
			sb.Append(station.AirQuality4.ToString("F1") + ListSeparator);     //71
			sb.Append(station.AirQualityAvg1.ToString("F1") + ListSeparator);  //72
			sb.Append(station.AirQualityAvg2.ToString("F1") + ListSeparator);  //73
			sb.Append(station.AirQualityAvg3.ToString("F1") + ListSeparator);  //74
			sb.Append(station.AirQualityAvg4.ToString("F1") + ListSeparator);  //75

			for (int i = 1; i < 9; i++)
			{
				sb.Append(station.UserTemp[i].ToString(TempFormat) + ListSeparator);   //76-83
			}

			sb.Append(station.CO2 + ListSeparator);                                    //84
			sb.Append(station.CO2_24h + ListSeparator);                                //85
			sb.Append(station.CO2_pm2p5.ToString("F1") + ListSeparator);               //86
			sb.Append(station.CO2_pm2p5_24h.ToString("F1") + ListSeparator);           //87
			sb.Append(station.CO2_pm10.ToString("F1") + ListSeparator);                //88
			sb.Append(station.CO2_pm10_24h.ToString("F1") + ListSeparator);            //89
			sb.Append(station.CO2_temperature.ToString(TempFormat) + ListSeparator);   //90
			sb.Append(station.CO2_humidity);                                           //91

			var success = false;
			var retries = LogFileRetries;
			do
			{
				try
				{
					using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
					using (StreamWriter file = new StreamWriter(fs))
					{
						await file.WriteLineAsync(sb.ToString());
						file.Close();
						fs.Close();

						success = true;

						LogDebugMessage($"DoExtraLogFile: Log entry for {timestamp} written");
					}
				}
				catch (Exception ex)
				{
					LogMessage($"DoExtraLogFile: Error writing log entry {timestamp} - {ex.Message}");
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);
		}

		public async void DoAirLinkLogFile(DateTime timestamp)
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

			LogDebugMessage($"DoAirLinkLogFile: Writing log entry for {timestamp}");

			var sb = new StringBuilder(256);

			sb.Append(timestamp.ToString("dd/MM/yy") + ListSeparator);
			sb.Append(timestamp.ToString("HH:mm") + ListSeparator);

			if (AirLinkInEnabled && airLinkDataIn != null)
			{
				sb.Append(airLinkDataIn.temperature.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.humidity + ListSeparator);
				sb.Append(airLinkDataIn.pm1.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm2p5.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm2p5_1hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm2p5_3hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm2p5_24hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm2p5_nowcast.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm10.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm10_1hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm10_3hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm10_24hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pm10_nowcast.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataIn.pct_1hr + ListSeparator);
				sb.Append(airLinkDataIn.pct_3hr + ListSeparator);
				sb.Append(airLinkDataIn.pct_24hr + ListSeparator);
				sb.Append(airLinkDataIn.pct_nowcast + ListSeparator);
				if (AirQualityDPlaces > 0)
				{
					sb.Append(airLinkDataIn.aqiPm2p5.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm2p5_1hr.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm2p5_3hr.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm2p5_24hr.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm2p5_nowcast.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm10.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm10_1hr.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm10_3hr.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm10_24hr.ToString(AirQualityFormat) + ListSeparator);
					sb.Append(airLinkDataIn.aqiPm10_nowcast.ToString(AirQualityFormat) + ListSeparator);
				}
				else // Zero decimals - truncate value rather than round
				{
					sb.Append((int)airLinkDataIn.aqiPm2p5 + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm2p5_1hr + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm2p5_3hr + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm2p5_24hr + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm2p5_nowcast + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm10 + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm10_1hr + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm10_3hr + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm10_24hr + ListSeparator);
					sb.Append((int)airLinkDataIn.aqiPm10_nowcast + ListSeparator);
				}
			}
			else
			{
				// write zero values - subtract 2 for firmware version, WiFi RSSI
				for (var i = 0; i < typeof(AirLinkData).GetProperties().Length - 2; i++)
				{
					sb.Append("0" + ListSeparator);
				}
			}

			if (AirLinkOutEnabled && airLinkDataOut != null)
			{
				sb.Append(airLinkDataOut.temperature.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.humidity + ListSeparator);
				sb.Append(airLinkDataOut.pm1.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm2p5.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm2p5_1hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm2p5_3hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm2p5_24hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm2p5_nowcast.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm10.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm10_1hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm10_3hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm10_24hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pm10_nowcast.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.pct_1hr + ListSeparator);
				sb.Append(airLinkDataOut.pct_3hr + ListSeparator);
				sb.Append(airLinkDataOut.pct_24hr + ListSeparator);
				sb.Append(airLinkDataOut.pct_nowcast + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm2p5.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm2p5_1hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm2p5_3hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm2p5_24hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm2p5_nowcast.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm10.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm10_1hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm10_3hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm10_24hr.ToString("F1") + ListSeparator);
				sb.Append(airLinkDataOut.aqiPm10_nowcast.ToString("F1"));
			}
			else
			{
				// write zero values - subtract 2 for firmware version, WiFi RSSI - subtract 1 for end field
				for (var i = 0; i < typeof(AirLinkData).GetProperties().Length - 3; i++)
				{
					sb.Append("0" + ListSeparator);
				}
				sb.Append('0');
			}

			var success = false;
			var retries = LogFileRetries;
			do
			{
				try
				{
					using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
					using (StreamWriter file = new StreamWriter(fs))
					{
						file.WriteLine(sb);
						file.Close();
						fs.Close();

						success = true;

						LogDebugMessage($"DoAirLinkLogFile: Log entry for {timestamp} written");
					}
				}
				catch(Exception ex)
				{
					LogDebugMessage($"DoAirLinkLogFile: Error writing log entry for {timestamp} - {ex.Message}");
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);
		}

		public void DoCustomIntervalLogs(DateTime timestamp)
		{
			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"DoCustomIntervalLogs: Not all data is ready, aborting process");
				return;
			}

			for (var i = 0; i < 10; i++)
			{
				if (CustomIntvlLogSettings[i].Enabled && timestamp.Minute % CustomIntvlLogSettings[i].Interval == 0)
				{
					DoCustomIntervalLog(i, timestamp);
				}
			}
		}

		private async void DoCustomIntervalLog(int idx, DateTime timestamp)
		{
			// Writes a custom log file

			// create the filename
			var filename = GetCustomIntvlLogFileName(idx, timestamp);

			LogDebugMessage($"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Writing log entry for {timestamp}");

			// create the line to be appended
			var sb = new StringBuilder(256);

			sb.Append(timestamp.ToString("dd/MM/yy") + ListSeparator);
			sb.Append(timestamp.ToString("HH:mm") + ListSeparator);

			var tokenParser = new TokenParser(TokenParserOnToken);

			// process the webtags in the content string
			tokenParser.InputText = CustomIntvlLogSettings[idx].ContentString;
			sb.Append(tokenParser.ToStringFromString());

			LogDataMessage("DoCustomIntervalLog: entry: " + sb);

			var success = false;
			var retries = LogFileRetries;
			do
			{
				try
				{
					using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
					using (StreamWriter file = new StreamWriter(fs))
					{
						file.WriteLine(sb);
						file.Close();
						fs.Close();

						success = true;

						LogDebugMessage($"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Log entry for {timestamp} written");
					}
				}
				catch (Exception ex)
				{
					LogDebugMessage($"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Error writing log entry for {timestamp} - {ex.Message}");
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);
		}

		public void DoCustomDailyLogs(DateTime timestamp)
		{
			for (var i = 0; i < 10; i++)
			{
				if (CustomDailyLogSettings[i].Enabled)
				{
					DoCustomDailyLog(i, timestamp);
				}
			}
		}

		private async void DoCustomDailyLog(int idx, DateTime timestamp)
		{
			LogDebugMessage($"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Writing log entry");

			// create the filename
			var filename = GetCustomDailyLogFileName(idx);

			string datestring = timestamp.AddDays(-1).ToString("dd/MM/yy");
			// NB this string is just for logging, the dayfile update code is further down
			var sb = new StringBuilder(300);
			sb.Append(datestring + ListSeparator);

			var tokenParser = new TokenParser(TokenParserOnToken);

			// process the webtags in the content string
			tokenParser.InputText = CustomDailyLogSettings[idx].ContentString;
			sb.Append(tokenParser.ToStringFromString());

			LogDataMessage("DoCustomDailyLog: entry: " + sb);

			var success = false;
			var retries = LogFileRetries;
			do
			{
				try
				{
					using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
					using (StreamWriter file = new StreamWriter(fs))
					{
						file.WriteLine(sb);
						file.Close();
						fs.Close();

						success = true;

						LogDebugMessage($"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Log entry written");
					}
				}
				catch (Exception ex)
				{
					LogDebugMessage($"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Error writing log entry - {ex.Message}");
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);
		}

		private void CheckForSingleInstance(bool Windows)
		{
			try
			{
				if (Windows)
				{
					var tempFolder = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Machine);
					_lockFilename = tempFolder + "\\cumulusmx-" + wsPort + ".lock";
				}
				else
				{
					// /tmp seems to be universal across Linux and Mac
					_lockFilename = "/tmp/cumulusmx-" + wsPort + ".lock";
				}

				LogMessage("Creating lock file " + _lockFilename);
				// must include Write access in order to lock file
				_lockFile = new FileStream(_lockFilename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

				using (var thisProcess = System.Diagnostics.Process.GetCurrentProcess())
				{
					var procId = thisProcess.Id.ToString().ToAsciiBytes();
					_lockFile.Write(procId, 0, procId.Length);
					_lockFile.Flush();
				}
				_lockFile.Lock(0, 0); // 0,0 has special meaning to lock entire file regardless of length

				// give everyone access to the file
				if (Windows)
				{
					FileInfo fileInfo = new FileInfo(_lockFilename);
					FileSecurity accessControl = fileInfo.GetAccessControl();
					accessControl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
					fileInfo.SetAccessControl(accessControl);
				}
				else
				{
					Utils.RunExternalTask("chmod", "777 " + _lockFilename, false, true);
				}

				LogMessage("Stop second instance: No other running instances of Cumulus found");
			}
			catch (IOException ex) when ((ex.HResult & 0x0000FFFF) == 32 || (ex.HResult & 0x0000FFFF) == 33)
			{
				// sharing violation 32, or lock violation 33
				if (ProgramOptions.WarnMultiple)
				{
					LogConsoleMessage("Cumulus is already running - terminating", ConsoleColor.Red);
					LogConsoleMessage("Program exit");
					LogMessage("Stop second instance: Cumulus is already running and 'Stop second instance' is enabled - terminating");
					LogMessage("Stop second instance: Program exit");
					Environment.Exit(3);
					return;
				}
				else
				{
					LogConsoleMessage("Cumulus is already running - but 'Stop second instance' is disabled", ConsoleColor.Yellow);
					LogMessage("Stop second instance: Cumulus is already running but 'Stop second instance' is disabled - continuing");
				}
			}
			catch (Exception ex)
			{
				LogMessage("Stop second instance: File Error! - " + ex);
				LogMessage("Stop second instance: File HResult - " + ex.HResult);
				LogMessage("Stop second instance: File HResult - " + (ex.HResult & 0x0000FFFF));

				if (ProgramOptions.WarnMultiple)
				{
					LogMessage("Stop second instance: Terminating this instance of Cumulus");
					LogConsoleMessage("An error occurred during second instance detection and 'Stop second instance' is enabled - terminating", ConsoleColor.Red);
					LogConsoleMessage("Program exit");
					Environment.Exit(3);
					return;
				}
				else
				{
					LogMessage("Stop second instance: 'Stop second instance' is disabled - continuing this instance of Cumulus");
				}
			}
		}

		public void BackupData(bool daily, DateTime timestamp)
		{
			string dirpath = daily ? backupPath + "daily" + DirectorySeparator : backupPath;

			if (!Directory.Exists(dirpath))
			{
				LogMessage("BackupData: *** Error - backup folder does not exist - " + dirpath);
				CreateRequiredFolders();
				if (!Directory.Exists(dirpath))
				{
					return;
				}
			}
			else
			{
				string[] dirs = Directory.GetDirectories(dirpath);
				Array.Sort(dirs);
				var dirlist = new List<string>(dirs);

				while (dirlist.Count > 10)
				{
					try
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
					catch (UnauthorizedAccessException)
					{
						LogErrorMessage("BackupData: Error, no permission to read/delete folder: " + dirlist[0]);
					}
					catch (Exception ex)
					{
						LogErrorMessage($"BackupData: Error while attempting to read/delete folder: {dirlist[0]}, error message: {ex.Message}");
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
				var dbBackup = foldername + "cumulusmx.db";
				var stringsbackup = foldername + "strings.ini";

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
					CopyBackupFile(dbfile, dbBackup);
					CopyBackupFile(extraFile, extraBackup);
					CopyBackupFile(AirLinkFile, AirLinkBackup);
					CopyBackupFile("strings.ini", stringsbackup);
					// custom logs
					for (var i = 0; i < 10; i++)
					{
						if (CustomIntvlLogSettings[i].Enabled)
						{
							var filename = GetCustomIntvlLogFileName(i, timestamp);
							CopyBackupFile(filename, foldername + filename.Replace(logFilePath, ""));
						}

						if (CustomDailyLogSettings[i].Enabled)
						{
							var filename = GetCustomDailyLogFileName(i);
							CopyBackupFile(filename, foldername + filename.Replace(logFilePath, ""));
						}
					}

					// Do not do this extra backup between 00:00 & Roll-over hour on the first of the month
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

						for (var i = 0; i < 10; i++)
						{
							if (CustomIntvlLogSettings[i].Enabled)
							{
								var filename = GetCustomIntvlLogFileName(i, timestamp.AddDays(-1));
								CopyBackupFile(filename, foldername + filename.Replace(logFilePath, ""), true);
							}
						}
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
					return Trans.Calm;
				case 1:
					return Trans.Lightair;
				case 2:
					return Trans.Lightbreeze;
				case 3:
					return Trans.Gentlebreeze;
				case 4:
					return Trans.Moderatebreeze;
				case 5:
					return Trans.Freshbreeze;
				case 6:
					return Trans.Strongbreeze;
				case 7:
					return Trans.Neargale;
				case 8:
					return Trans.Gale;
				case 9:
					return Trans.Stronggale;
				case 10:
					return Trans.Storm;
				case 11:
					return Trans.Violentstorm;
				case 12:
					return Trans.Hurricane;
				default:
					return Trans.Unknown;
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

			try
			{
				tokenSource.Cancel();
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

			try
			{
				LogMessage("Stopping extra sensors...");
				// If we have a Outdoor AirLink sensor, and it is linked to this WLL then stop it now
				airLinkOut?.Stop();
				// If we have a Indoor AirLink sensor, and it is linked to this WLL then stop it now
				airLinkIn?.Stop();
				// If we have a Ecowitt Extra Sensors, stop it
				ecowittExtra?.Stop();
				// If we have a Ambient Extra Sensors, stop it
				ambientExtra?.Stop();

				LogMessage("Extra sensors stopped");

			}
			catch { }

			if (station != null)
			{
				LogMessage("Stopping station...");
				try
				{
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
				}
				catch { }
			}

			// do we have a shutdown task to run?
			if (!string.IsNullOrEmpty(ProgramOptions.ShutdownTask))
			{
				try
				{
					var args = string.Empty;

					if (!string.IsNullOrEmpty(ProgramOptions.ShutdownTaskParams))
					{
						var parser = new TokenParser(TokenParserOnToken);
						parser.InputText = ProgramOptions.ShutdownTaskParams;
						args = parser.ToStringFromString();
					}
					LogMessage($"Running shutdown task: {ProgramOptions.ShutdownTask}, arguments: {args}");
					Utils.RunExternalTask(ProgramOptions.ShutdownTask, args, false);
				}
				catch (Exception ex)
				{
					LogMessage($"Error running shutdown task: {ex.Message}");
				}
			}

			try
			{
				if (null != _lockFile)
				{
					LogMessage("Releasing lock file...");
					_lockFile.Close();
					_lockFile.Dispose();
					File.Delete(_lockFilename);
				}
			}
			catch { }

			LogMessage("Station shutdown complete");
		}

		public async void DoHTMLFiles()
		{
			try
			{
				if (!RealtimeIntervalEnabled)
				{
					CreateRealtimeFile(999);
					MySqlRealtimeFile(999, true);
				}

				LogDebugMessage("Interval: Creating standard web files");
				for (var i = 0; i < StdWebFiles.Length; i++)
				{
					if (StdWebFiles[i].Create && !string.IsNullOrWhiteSpace(StdWebFiles[i].TemplateFileName))
					{
						var destFile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
						if (StdWebFiles[i].LocalFileName == "wxnow.txt")
						{
							station.CreateWxnowFile();
						}
						else
						{
							ProcessTemplateFile(StdWebFiles[i].TemplateFileName, destFile, true);
						}
					}
				}
				LogDebugMessage("Interval: Done creating standard Data file");

				LogDebugMessage("Interval: Creating graph data files");
				station.CreateGraphDataFiles();
				LogDebugMessage("Interval: Done creating graph data files");

				LogDebugMessage("Interval: Creating extra files");
				// handle any extra files
				for (int i = 0; i < numextrafiles; i++)
				{
					if (!ExtraFiles[i].realtime && !ExtraFiles[i].endofday)
					{
						var uploadfile = ExtraFiles[i].local;
						var remotefile = ExtraFiles[i].remote;

						if ((uploadfile.Length > 0) && (remotefile.Length > 0) && !ExtraFiles[i].FTP)
						{
							uploadfile = GetUploadFilename(uploadfile, DateTime.Now);

							if (File.Exists(uploadfile))
							{
								remotefile = GetRemoteFileName(remotefile, DateTime.Now);

								LogDebugMessage($"Interval: Copying extra file[{i}] {uploadfile} to {remotefile}");
								try
								{
									if (ExtraFiles[i].process)
									{
										var data = ProcessTemplateFile2String(uploadfile, false, ExtraFiles[i].UTF8);
										File.WriteAllText(remotefile, data);
									}
									else
									{
										// just copy the file
										File.Copy(uploadfile, remotefile, true);
									}
								}
								catch (Exception ex)
								{
									LogDebugMessage($"Interval: Error copying extra file[{i}]: " + ex.Message);
								}
								//LogDebugMessage("Finished copying extra file " + uploadfile);
							}
							else
							{
								LogMessage($"Interval: Warning, extra web file[{i}] not found - {uploadfile}");
							}
						}
					}
				}

				LogDebugMessage("Interval: Done creating extra files");

				if (!string.IsNullOrEmpty(ExternalProgram))
				{
					try
					{
						var args = string.Empty;

						if (!string.IsNullOrEmpty(ExternalParams))
						{
							var parser = new TokenParser(TokenParserOnToken);
							parser.InputText = ExternalParams;
							args = parser.ToStringFromString();
						}
						LogDebugMessage("Interval: Executing program " + ExternalProgram + " " + args);
						Utils.RunExternalTask(ExternalProgram, args, false);
						LogDebugMessage("Interval: External program started");
					}
					catch (Exception ex)
					{
						LogMessage("Interval: Error starting external program: " + ex.Message);
					}
				}

				DoLocalCopy();

				await DoIntervalUpload();
			}
			finally
			{
				WebUpdating = 0;
			}
		}

		public void DoLocalCopy()
		{
			var remotePath = FtpOptions.LocalCopyFolder;

			try
			{
				var folderSep2 = Path.AltDirectorySeparatorChar;

				if (!FtpOptions.LocalCopyEnabled)
					return;

				if (FtpOptions.LocalCopyFolder.Length > 0)
				{
					remotePath = (FtpOptions.LocalCopyFolder.EndsWith(DirectorySeparator.ToString()) || FtpOptions.LocalCopyFolder.EndsWith(folderSep2.ToString())) ? FtpOptions.LocalCopyFolder : FtpOptions.LocalCopyFolder + DirectorySeparator;
				}
				else
				{
					return;
				}
			}
			catch (Exception ex)
			{
				LogMessage("LocalCopy: Error with paths - " + ex.Message);
			}


			var srcfile = "";
			string dstfile;

			if (NOAAconf.NeedCopy)
			{
				try
				{
					// upload NOAA reports
					LogDebugMessage("LocalCopy: Copying NOAA reports");

					try
					{
						var dstPath = string.IsNullOrEmpty(NOAAconf.CopyFolder) ? remotePath : NOAAconf.CopyFolder;

						srcfile = ReportPath + NOAAconf.LatestMonthReport;
						dstfile = dstPath + DirectorySeparator + NOAAconf.LatestMonthReport;

						File.Copy(srcfile, dstfile, true);

						srcfile = ReportPath + NOAAconf.LatestYearReport;
						dstfile = dstPath + DirectorySeparator + NOAAconf.LatestYearReport;

						File.Copy(srcfile, dstfile, true);

						NOAAconf.NeedCopy = false;

						LogDebugMessage("LocalCopy: Done copying NOAA reports");
					}
					catch (Exception ex)
					{
						LogMessage("LocalCopy: Error copy NOAA reports - " + ex.Message);
					}
				}
				catch (Exception e)
				{
					LogMessage($"LocalCopy: Error copying file {srcfile} - {e.Message}");
				}
			}

			// standard files
			LogDebugMessage("LocalCopy: Copying standard web files");
			var success = 0;
			var failed = 0;
			for (var i = 0; i < StdWebFiles.Length; i++)
			{
				if (StdWebFiles[i].Copy && StdWebFiles[i].CopyRequired)
				{
					try
					{
						dstfile = remotePath + StdWebFiles[i].RemoteFileName;
						// the files are no longer always created, so gen them on the fly if required
						if (StdWebFiles[i].Create)
						{
							srcfile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
							File.Copy(srcfile, dstfile, true);
						}
						else
						{
							string text;
							if (StdWebFiles[i].LocalFileName == "wxnow.txt")
							{
								text = station.CreateWxnowFileString();
							}
							else
							{
								text = ProcessTemplateFile2String(StdWebFiles[i].TemplateFileName, true);
							}

							File.WriteAllText(dstfile, text);
						}
						success++;
					}
					catch (Exception e)
					{
						LogMessage($"LocalCopy: Error copying standard data file [{StdWebFiles[i].LocalFileName}]");
						LogMessage($"LocalCopy: Error = {e.Message}");
						failed++;
					}
				}
			}
			LogDebugMessage($"LocalCopy: Done copying standard web files - Success: {success}, Failed: {failed}");

			var oldset = DateTime.Now.AddHours(-GraphHours);

			LogDebugMessage("LocalCopy: Copying graph data files");
			success = 0;
			failed = 0;
			for (int i = 0; i < GraphDataFiles.Length; i++)
			{
				if (GraphDataFiles[i].Copy && GraphDataFiles[i].CopyRequired)
				{
					try
					{
						dstfile = remotePath + GraphDataFiles[i].RemoteFileName;

						// the files are no longer created when using PHP upload, so gen them on the fly
						if (GraphDataFiles[i].Create)
						{
							srcfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].LocalFileName;
							File.Copy(srcfile, dstfile, true);
						}
						else
						{
							var text = station.CreateGraphDataJson(GraphDataFiles[i].LocalFileName, false);
							File.WriteAllText(dstfile, text);
						}
						// The config files only need uploading once per change
						// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
						if (i == 0 || i == 1 || i == 8 || i == 9 || i == 11)
						{
							GraphDataFiles[i].CopyRequired = false;
						}
						success++;
					}
					catch (Exception e)
					{
						LogMessage($"LocalCopy: Error copying graph data file [{srcfile}]");
						LogMessage($"LocalCopy: Error = {e.Message}");
						failed++;
					}
				}
			}
			LogDebugMessage($"LocalCopy: Done copying graph data files - Success: {success}, Failed: {failed}");

			LogDebugMessage("LocalCopy: Copying daily graph data files");
			success = 0;
			failed = 0;
			for (int i = 0; i < GraphDataEodFiles.Length; i++)
			{
				if (GraphDataEodFiles[i].Copy && GraphDataEodFiles[i].CopyRequired)
				{
					try
					{
						dstfile = remotePath + GraphDataEodFiles[i].RemoteFileName;

						// the files are no longer created when using PHP upload, so gen them on the fly
						if (GraphDataEodFiles[i].Create)
						{
							srcfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].LocalFileName;
							File.Copy(srcfile, dstfile, true);
						}
						else
						{
							var text = station.CreateEodGraphDataJson(GraphDataEodFiles[i].LocalFileName);
							File.WriteAllText(dstfile, text);
						}
						// Uploaded OK, reset the upload required flag
						GraphDataEodFiles[i].CopyRequired = false;
						success++;
					}
					catch (Exception e)
					{
						LogMessage($"LocalCopy: Error copying daily graph data file [{srcfile}]");
						LogMessage($"LocalCopy: Error = {e.Message}");
						failed++;
					}
				}
			}
			LogDebugMessage($"LocalCopy: Done copying daily graph data files - Success: {success}, Failed: {failed}");

			if (MoonImage.Copy && MoonImage.ReadyToCopy)
			{
				try
				{
					LogDebugMessage("LocalCopy: Copying Moon image file to " + MoonImage.CopyDest);
					File.Copy("web" + DirectorySeparator + "moon.png", MoonImage.CopyDest, true);
					LogDebugMessage("LocalCopy: Done copying Moon image file");
					// clear the image ready for copy flag, only upload once an hour
					MoonImage.ReadyToCopy = false;
				}
				catch (Exception e)
				{
					LogMessage($"LocalCopy: Error copying moon image - {e.Message}");
				}
			}

			LogDebugMessage("LocalCopy: Copy process complete");
		}


		public void DoHttpFiles(DateTime now)
		{
			// sanity check - is there anything to do?
			if (HttpFilesConfig.Where(x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now).Count() == 0)
			{
#if DEBUG
				LogDebugMessage("ProcessHttpFiles: No files to process at this time");
#endif
				return;
			}

			// second sanity check, are there any local copies?
			var localOnly = HttpFilesConfig.Where(x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now && !x.Upload);
			if (localOnly.Count() > 0)
			{
				LogDebugMessage("ProcessHttpFiles: Creating local http files");
				foreach (var item in localOnly)
				{
					_ = Task.Run(async () =>
					{
						// just create a file from the download
						await httpFiles.DownloadHttpFile(item.Url, item.Remote);

						item.SetNextInterval(now);
					});
				};

				LogDebugMessage("ProcessHttpFiles: Done creating local http file tasks, not waiting for them to complete");
			}

			// third sanity check, are there any uploads?
			var uploads = HttpFilesConfig.Where(x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now && x.Upload);
			if (uploads.Count() == 0)
			{
#if DEBUG
				LogDebugMessage("ProcessHttpFiles: No files to upload at this time");
#endif
				return;
			}

			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				ConnectionInfo connectionInfo;
				if (FtpOptions.SshAuthen == "password")
				{
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password));
					LogFtpDebugMessage("ProcessHttpFiles: Connecting using password authentication");
				}
				else if (FtpOptions.SshAuthen == "psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogFtpDebugMessage("ProcessHttpFiles: Connecting using PSK authentication");
				}
				else if (FtpOptions.SshAuthen == "password_psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password), new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogFtpDebugMessage("ProcessHttpFiles: Connecting using password or PSK authentication");
				}
				else
				{
					LogFtpMessage($"ProcessHttpFiles: Invalid SshftpAuthentication specified [{FtpOptions.SshAuthen}]");
					return;
				}

				try
				{
					using (SftpClient conn = new SftpClient(connectionInfo))
					{
						// If the ECDSA ciphers are not available - yes I'm talking about you Mono! - remove them from the list to be tried
						try
						{
							using (var ecdsa = new System.Security.Cryptography.ECDsaCng())
								_ = 0; // do nothing
						}
						catch (NotImplementedException)
						{
							var algsToRemove = connectionInfo.HostKeyAlgorithms.Keys.Where(algName => algName.StartsWith("ecdsa")).ToArray();
							foreach (var algName in algsToRemove)
								connectionInfo.HostKeyAlgorithms.Remove(algName);
						}

						try
						{
							LogFtpDebugMessage($"ProcessHttpFiles: CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}");
							conn.Connect();
							if (ServicePointManager.DnsRefreshTimeout == 0)
							{
								ServicePointManager.DnsRefreshTimeout = 120000; // two minutes default
							}
						}
						catch (Exception ex)
						{
							LogFtpMessage($"ProcessHttpFiles: Error connecting SFTP - {ex.Message}");

							if ((uint) ex.HResult == 0x80004005) // Could not resolve host
							{
								// Disable the DNS cache for the next query
								ServicePointManager.DnsRefreshTimeout = 0;
							}
							return;
						}

						foreach (var item in uploads)
						{
							Stream strm = null;

							try
							{

								if (cancellationToken.IsCancellationRequested)
									return;

								strm = httpFiles.DownloadHttpFileStream(item.Url).Result;
								UploadStream(conn, item.Remote, strm, -1);

								item.SetNextInterval(now);
							}
							catch (Exception ex) when (!(ex is TaskCanceledException))
							{
								LogExceptionMessage(ex, $"ProcessHttpFiles: Error uploading http file {item.Url} to: {item.Remote}");
							}
							finally
							{
								if (null != strm)
									strm.Dispose();
							}
						};

						LogDebugMessage("ProcessHttpFiles: Done uploading http files");


						if (conn.IsConnected)
						{
							try
							{
								// do not error on disconnect
								conn.Disconnect();
							}
							catch { }
						}
					}
				}
				catch (Exception ex)
				{
					LogFtpMessage($"ProcessHttpFiles: Error using SFTP connection - {ex.Message}");
				}
				LogFtpDebugMessage("ProcessHttpFiles: Connection process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || (FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				using (FtpClient conn = new FtpClient())
				{
					LogFtpMessage(""); // insert a blank line
					LogFtpDebugMessage($"ProcessHttpFiles: CumulusMX Connecting to " + FtpOptions.Hostname);
					conn.Host = FtpOptions.Hostname;
					conn.Port = FtpOptions.Port;
					conn.Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password);
					conn.LegacyLogger = LogFluentFtpMessage;

					if (!FtpOptions.AutoDetect)
					{

						if (FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							// Explicit = Current protocol - connects using FTP and switches to TLS
							// Implicit = Old depreciated protocol - connects using TLS
							conn.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
							conn.Config.DataConnectionEncryption = true;
							// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specify protocols
							// b3155 - switch to default again - this will use the highest version available in the OS
							//conn.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
						}

						if (FtpOptions.ActiveMode)
						{
							conn.Config.DataConnectionType = FtpDataConnectionType.PORT;
						}
						else if (FtpOptions.DisableEPSV)
						{
							conn.Config.DataConnectionType = FtpDataConnectionType.PASV;
						}
					}

					if (FtpOptions.FtpMode == FtpProtocols.FTPS)
					{
						conn.Config.ValidateAnyCertificate = FtpOptions.IgnoreCertErrors;
					}

					try
					{
						if (FtpOptions.AutoDetect)
						{
							conn.AutoConnect();
						}
						else
						{
							conn.Connect();
						}

						if (ServicePointManager.DnsRefreshTimeout == 0)
						{
							ServicePointManager.DnsRefreshTimeout = 120000; // two minutes default
						}
					}
					catch (Exception ex)
					{
						LogFtpMessage("ProcessHttpFiles: Error connecting ftp - " + ex.Message);

						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"ProcessHttpFiles: Base exception - {ex.Message}");
						}

						if ((uint) ex.HResult == 0x80004005) // Could not resolve host
						{
							// Disable the DNS cache for the next query
							ServicePointManager.DnsRefreshTimeout = 0;
						}
						return;
					}

					LogDebugMessage("ProcessHttpFiles: Uploading http files");

					foreach (var item in uploads)
					{
						Stream strm = null;

						try
						{

							if (cancellationToken.IsCancellationRequested)
								return;

							strm = httpFiles.DownloadHttpFileStream(item.Url).Result;
							UploadStream(conn, item.Remote, strm, -1);

							item.SetNextInterval(now);
						}
						catch (Exception ex) when (!(ex is TaskCanceledException))
						{
							LogExceptionMessage(ex, $"ProcessHttpFiles: Error uploading http file {item.Url} to: {item.Remote}");
						}
						finally
						{
							if (null != strm)
								strm.Dispose();
						}
					};

					LogDebugMessage("ProcessHttpFiles: Done uploading http files");



					if (conn.IsConnected)
					{
						conn.Disconnect();
						LogFtpDebugMessage("ProcessHttpFiles: Disconnected from " + FtpOptions.Hostname);
					}
				}
				LogFtpMessage("ProcessHttpFiles: Process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				var tasklist = new List<Task>();
				var taskCount = 0;
				var runningTaskCount = 0;

				LogDebugMessage("ProcessHttpFiles: Uploading http files");

				HttpFilesConfig
				.Where(x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now && x.Upload)
				.ToList()
				.ForEach(item =>
				{
					Interlocked.Increment(ref taskCount);

					var downloadfile = item.Url;
					var remotefile = item.Remote;

#if DEBUG
					LogDebugMessage($"ProcessHttpFiles: Http file: {downloadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
					LogDebugMessage($"ProcessHttpFiles: Http file: {downloadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
					uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif

					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							var content = await httpFiles.DownloadHttpFileBase64String(item.Url);
							await UploadString(phpUploadHttpClient, false, null, content, item.Remote, -1, true);

							item.SetNextInterval(now);
						}
						catch (Exception ex) when (!(ex is TaskCanceledException))
						{
							LogExceptionMessage(ex, $"ProcessHttpFiles: Error uploading http file {downloadfile} to: {remotefile}");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"ProcessHttpFiles: Http file: {downloadfile} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}
						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				});

				LogDebugMessage("ProcessHttpFiles: Done uploading http files");

				// wait for all the files to start
				while (runningTaskCount < taskCount)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						LogDebugMessage("ProcessHttpFiles: Upload process aborted");
						return;
					}
					Thread.Sleep(10);
				}

				// wait for all the files to complete
				Task.WaitAll(tasklist.ToArray(), cancellationToken);
				//LogDebugMessage($"ProcessHttpFiles: Files upload complete, {tasklist.Count()} files processed");

				if (cancellationToken.IsCancellationRequested)
				{
					LogDebugMessage($"ProcessHttpFiles: Upload process aborted");
					return;
				}

				LogDebugMessage($"ProcessHttpFiles: Upload process complete, {tasklist.Count()} files processed");
				tasklist.Clear();
			}
		}

		public async Task DoIntervalUpload()
		{
			var remotePath = "";

			if (!FtpOptions.Enabled || !FtpOptions.IntervalEnabled)
				return;

			if (FtpOptions.Directory.Length > 0)
			{
				remotePath = (FtpOptions.Directory.EndsWith("/") ? FtpOptions.Directory : FtpOptions.Directory + "/");
			}

			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				// BUILD 3092 - added alternate SFTP authentication options
				ConnectionInfo connectionInfo;
				if (FtpOptions.SshAuthen == "password")
				{
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password));
					LogFtpDebugMessage("SFTP[Int]: Connecting using password authentication");
				}
				else if (FtpOptions.SshAuthen == "psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogFtpDebugMessage("SFTP[Int]: Connecting using PSK authentication");
				}
				else if (FtpOptions.SshAuthen == "password_psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password), new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogFtpDebugMessage("SFTP[Int]: Connecting using password or PSK authentication");
				}
				else
				{
					LogFtpMessage($"SFTP[Int]: Invalid SshftpAuthentication specified [{FtpOptions.SshAuthen}]");
					return;
				}

				try
				{
					using (SftpClient conn = new SftpClient(connectionInfo))
					{
						// If the ECDSA ciphers are not available - yes I'm talking about you Mono! - remove them from the list to be tried
						try
						{
							using (var ecdsa = new System.Security.Cryptography.ECDsaCng())
								_ = 0; // do nothing
						}
						catch (NotImplementedException)
						{
							var algsToRemove = connectionInfo.HostKeyAlgorithms.Keys.Where(algName => algName.StartsWith("ecdsa")).ToArray();
							foreach (var algName in algsToRemove)
								connectionInfo.HostKeyAlgorithms.Remove(algName);
						}

						try
						{
							LogFtpDebugMessage($"SFTP[Int]: CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}");
							conn.Connect();
							if (ServicePointManager.DnsRefreshTimeout == 0)
							{
								ServicePointManager.DnsRefreshTimeout = 120000; // two minutes default
							}
						}
						catch (Exception ex)
						{
							LogFtpMessage($"SFTP[Int]: Error connecting SFTP - {ex.Message}");

							if ((uint) ex.HResult == 0x80004005) // Could not resolve host
							{
								// Disable the DNS cache for the next query
								ServicePointManager.DnsRefreshTimeout = 0;
							}
							return;
						}

						if (conn.IsConnected)
						{
							if (NOAAconf.NeedFtp)
							{
								try
								{
									// upload NOAA reports
									LogFtpDebugMessage("SFTP[Int]: Uploading NOAA reports");

									var uploadfile = ReportPath + NOAAconf.LatestMonthReport;
									var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

									UploadFile(conn, uploadfile, remotefile, -1);

									uploadfile = ReportPath + NOAAconf.LatestYearReport;
									remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

									UploadFile(conn, uploadfile, remotefile, -1);

									LogFtpDebugMessage("SFTP[Int]: Done uploading NOAA reports");
								}
								catch (Exception e)
								{
									LogFtpMessage($"SFTP[Int]: Error uploading file - {e.Message}");
								}
								NOAAconf.NeedFtp = false;
							}

							// Extra files
							for (int i = 0; i < numextrafiles; i++)
							{
								var uploadfile = ExtraFiles[i].local;
								var remotefile = ExtraFiles[i].remote;

								if ((uploadfile.Length > 0) &&
									(remotefile.Length > 0) &&
									!ExtraFiles[i].realtime &&
									(!ExtraFiles[i].endofday || EODfilesNeedFTP == ExtraFiles[i].endofday) && // Either: It's not flagged as an EOD file, OR: It is flagged as EOD and EOD FTP is required
									ExtraFiles[i].FTP)
								{
									// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
									var logDay = ExtraFiles[i].endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

									uploadfile = GetUploadFilename(uploadfile, logDay);

									if (File.Exists(uploadfile))
									{
										LogFtpDebugMessage("SFTP[Int]: Uploading Extra file: " + uploadfile);

										remotefile = GetRemoteFileName(remotefile, logDay);

										// all checks OK, file needs to be uploaded
										try
										{
											if (ExtraFiles[i].process)
											{
												var data = ProcessTemplateFile2String(uploadfile, false, ExtraFiles[i].UTF8);
												using (var strm = GenerateStreamFromString(data))
												{
													UploadStream(conn, remotefile, strm, -1);
												}
											}
											else
											{
												UploadFile(conn, uploadfile, remotefile, -1);
											}
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

							// standard files
							for (var i = 0; i < StdWebFiles.Length; i++)
							{
								if (StdWebFiles[i].FTP)
								{
									try
									{
										var localFile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
										var remotefile = remotePath + StdWebFiles[i].RemoteFileName;
										LogFtpDebugMessage("SFTP[Int]: Uploading standard Data file: " + localFile);

										string data;

										if (StdWebFiles[i].LocalFileName == "wxnow.txt")
										{
											data = station.CreateWxnowFileString();
										}
										else
										{
											data = ProcessTemplateFile2String(StdWebFiles[i].TemplateFileName, true, true);
										}

										using (var dataStream = GenerateStreamFromString(data))
										{
											UploadStream(conn, remotefile, dataStream);
										}
									}
									catch (Exception e)
									{
										LogFtpMessage($"SFTP[Int]: Error uploading standard data file [{StdWebFiles[i].RemoteFileName}]");
										LogFtpMessage($"SFTP[Int]: Error = {e}");
									}
								}
							}

							for (int i = 0; i < GraphDataFiles.Length; i++)
							{
								if (GraphDataFiles[i].FTP && GraphDataFiles[i].FtpRequired)
								{
									var uploadfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].LocalFileName;
									var remotefile = remotePath + GraphDataFiles[i].RemoteFileName;

									try
									{
										LogFtpDebugMessage("SFTP[Int]: Uploading graph data file: " + uploadfile);

										var json = station.CreateGraphDataJson(GraphDataFiles[i].LocalFileName, false);

										using (var dataStream = GenerateStreamFromString(json))
										{
											UploadStream(conn, remotefile, dataStream);
										}

										// Uploaded OK, reset the upload required flag for the static files
										if (i == 0 || i == 1)
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

							for (int i = 0; i < GraphDataEodFiles.Length; i++)
							{
								if (GraphDataEodFiles[i].FTP && GraphDataEodFiles[i].FtpRequired)
								{
									var uploadfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].LocalFileName;
									var remotefile = remotePath + GraphDataEodFiles[i].RemoteFileName;
									try
									{
										LogFtpMessage("SFTP[Int]: Uploading daily graph data file: " + uploadfile);

										var json = station.CreateEodGraphDataJson(GraphDataEodFiles[i].LocalFileName);

										using (var dataStream = GenerateStreamFromString(json))
										{
											UploadStream(conn, remotefile, dataStream, -1);
										}
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

							if (MoonImage.Ftp && MoonImage.ReadyToFtp)
							{
								try
								{
									LogFtpMessage("SFTP[Int]: Uploading Moon image file");
									UploadFile(conn, "web" + DirectorySeparator + "moon.png", remotePath + MoonImage.FtpDest, -1);
									LogFtpMessage("SFTP[Int]: Done uploading Moon image file");
									// clear the image ready for FTP flag, only upload once an hour
									MoonImage.ReadyToFtp = false;
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
				}
				catch (Exception ex)
				{
					LogFtpMessage($"SFTP[Int]: Error using SFTP connection - {ex.Message}");
				}
				LogFtpDebugMessage("SFTP[Int]: Connection process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || (FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				using (FtpClient conn = new FtpClient())
				{
					LogFtpMessage(""); // insert a blank line
					LogFtpDebugMessage($"FTP[Int]: CumulusMX Connecting to " + FtpOptions.Hostname);
					conn.Host = FtpOptions.Hostname;
					conn.Port = FtpOptions.Port;
					conn.Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password);
					conn.LegacyLogger = LogFluentFtpMessage;

					if (!FtpOptions.AutoDetect)
					{

						if (FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							// Explicit = Current protocol - connects using FTP and switches to TLS
							// Implicit = Old depreciated protocol - connects using TLS
							conn.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
							conn.Config.DataConnectionEncryption = true;
							// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specify protocols
							// b3155 - switch to default again - this will use the highest version available in the OS
							//conn.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
						}

						if (FtpOptions.ActiveMode)
						{
							conn.Config.DataConnectionType = FtpDataConnectionType.PORT;
						}
						else if (FtpOptions.DisableEPSV)
						{
							conn.Config.DataConnectionType = FtpDataConnectionType.PASV;
						}
					}

					if (FtpOptions.FtpMode == FtpProtocols.FTPS)
					{
						conn.Config.ValidateAnyCertificate = FtpOptions.IgnoreCertErrors;
					}

					try
					{
						if (FtpOptions.AutoDetect)
						{
							conn.AutoConnect();
						}
						else
						{
							conn.Connect();
						}

						if (ServicePointManager.DnsRefreshTimeout == 0)
						{
							ServicePointManager.DnsRefreshTimeout = 120000; // two minutes default
						}
					}
					catch (Exception ex)
					{
						LogFtpMessage("FTP[Int]: Error connecting ftp - " + ex.Message);

						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"FTP[Int]: Base exception - {ex.Message}");
						}

						if ((uint) ex.HResult == 0x80004005) // Could not resolve host
						{
							// Disable the DNS cache for the next query
							ServicePointManager.DnsRefreshTimeout = 0;
						}
						return;
					}

					if (conn.IsConnected)
					{
						if (NOAAconf.NeedFtp)
						{
							try
							{
								// upload NOAA reports
								LogFtpMessage("");
								LogFtpDebugMessage("FTP[Int]: Uploading NOAA reports");

								var uploadfile = ReportPath + NOAAconf.LatestMonthReport;
								var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

								UploadFile(conn, uploadfile, remotefile);

								uploadfile = ReportPath + NOAAconf.LatestYearReport;
								remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

								UploadFile(conn, uploadfile, remotefile);
								LogFtpDebugMessage("FTP[Int]: Upload of NOAA reports complete");
							}
							catch (Exception e)
							{
								LogFtpMessage($"FTP[Int]: Error uploading NOAA files: {e.Message}");
							}
							NOAAconf.NeedFtp = false;
						}

						// Extra files
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
								// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
								var logDay = ExtraFiles[i].endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

								uploadfile = GetUploadFilename(uploadfile, logDay);

								if (File.Exists(uploadfile))
								{
									remotefile = GetRemoteFileName(remotefile, logDay);

									LogFtpMessage("");
									LogFtpDebugMessage("FTP[Int]: Uploading Extra file: " + uploadfile);

									// all checks OK, file needs to be uploaded

									try
									{
										if (ExtraFiles[i].process)
										{
											var data = ProcessTemplateFile2String(uploadfile, false, ExtraFiles[i].UTF8);
											using (var strm = GenerateStreamFromString(data))
											{
												UploadStream(conn, remotefile, strm, -1);
											}
										}
										else
										{
											UploadFile(conn, uploadfile, remotefile);
										}
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
						for (int i = 0; i < StdWebFiles.Length; i++)
						{
							if (StdWebFiles[i].FTP)
							{
								try
								{
									var localfile = StdWebFiles[i].LocalPath + StdWebFiles[i].LocalFileName;
									LogFtpDebugMessage("FTP[Int]: Uploading standard Data file: " + localfile);

									string data;

									if (StdWebFiles[i].LocalFileName == "wxnow.txt")
									{
										data = station.CreateWxnowFileString();
									}
									else
									{
										data = ProcessTemplateFile2String(StdWebFiles[i].TemplateFileName, true, true);
									}

									using (var dataStream = GenerateStreamFromString(data))
									{
										UploadStream(conn, remotePath + StdWebFiles[i].RemoteFileName, dataStream, -1);
									}

									// Uploaded OK, reset the upload required flag
									StdWebFiles[i].FtpRequired = false;
								}
								catch (Exception e)
								{
									LogFtpMessage($"FTP[Int]: Error uploading file {StdWebFiles[i].RemoteFileName}: {e}");
								}
							}
						}

						for (int i = 0; i < GraphDataFiles.Length; i++)
						{
							if (GraphDataFiles[i].FTP && GraphDataFiles[i].FtpRequired)
							{
								try
								{
									var localfile = GraphDataFiles[i].LocalPath + GraphDataFiles[i].LocalFileName;
									var remotefile = remotePath + GraphDataFiles[i].RemoteFileName;
									LogFtpDebugMessage("FTP[Int]: Uploading graph data file: " + localfile);

									var json = station.CreateGraphDataJson(GraphDataFiles[i].LocalFileName, false);

									using (var dataStream = GenerateStreamFromString(json))
									{
										UploadStream(conn, remotefile, dataStream);
									}

									// Uploaded OK, reset the upload required flag
									if (i == 0 || i == 1)
									{
										GraphDataFiles[i].FtpRequired = false;
									}
								}
								catch (Exception e)
								{
									LogFtpMessage($"FTP[Int]: Error uploading graph data file [{GraphDataFiles[i].RemoteFileName}]");
									LogFtpMessage($"FTP[Int]: Error = {e}");
								}
							}
						}

						for (int i = 0; i < GraphDataEodFiles.Length; i++)
						{
							if (GraphDataEodFiles[i].FTP && GraphDataEodFiles[i].FtpRequired)
							{
								var localfile = GraphDataEodFiles[i].LocalPath + GraphDataEodFiles[i].LocalFileName;
								var remotefile = remotePath + GraphDataEodFiles[i].RemoteFileName;
								try
								{
									LogFtpMessage("FTP[Int]: Uploading daily graph data file: " + localfile);

									var json = station.CreateEodGraphDataJson(GraphDataEodFiles[i].LocalFileName);

									using (var dataStream = GenerateStreamFromString(json))
									{
										UploadStream(conn, remotefile, dataStream);
									}

									// Uploaded OK, reset the upload required flag
									GraphDataEodFiles[i].FtpRequired = false;
								}
								catch (Exception e)
								{
									LogFtpMessage($"FTP[Int]: Error uploading daily graph data file [{GraphDataEodFiles[i].RemoteFileName}]");
									LogFtpMessage($"FTP[Int]: Error = {e}");
								}
							}
						}

						if (MoonImage.Ftp && MoonImage.ReadyToFtp)
						{
							try
							{
								LogFtpMessage("");
								LogFtpDebugMessage("FTP[Int]: Uploading Moon image file");
								UploadFile(conn, "web" + DirectorySeparator + "moon.png", remotePath + MoonImage.FtpDest);
								// clear the image ready for FTP flag, only upload once an hour
								MoonImage.ReadyToFtp = false;
							}
							catch (Exception e)
							{
								LogMessage($"FTP[Int]: Error uploading moon image - {e.Message}");
							}
						}
					}

					// b3045 - dispose of connection
					conn.Disconnect();
					LogFtpDebugMessage("FTP[Int]: Disconnected from " + FtpOptions.Hostname);
				}
				LogFtpMessage("FTP[Int]: Process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogDebugMessage("PHP[Int]: Upload process starting");

				var tasklist = new List<Task>();
				var taskCount = 0;
				var runningTaskCount = 0;

				if (NOAAconf.NeedFtp)
				{
					// upload NOAA Monthly report
					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: NOAA Month report waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
						LogDebugMessage($"PHP[Int]: NOAA Month report has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}

					Interlocked.Increment(ref taskCount);

					tasklist.Add(Task.Run(async () =>
					{
						if (cancellationToken.IsCancellationRequested)
							return false;

						try
						{

							LogDebugMessage("PHP[Int]: Uploading NOAA Month report");

							var uploadfile = ReportPath + NOAAconf.LatestMonthReport;
							var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

							_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, -1, false, NOAAconf.UseUtf8);

						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading NOAA files");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: NOAA Year report released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);

					// upload NOAA Annual report
					Interlocked.Increment(ref taskCount);

					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: NOAA Year report waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
						LogDebugMessage($"PHP[Int]: NOAA Year report has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
							await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}

					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							LogDebugMessage("PHP[Int]: Uploading NOAA Year report");

							var uploadfile = ReportPath + NOAAconf.LatestYearReport;
							var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

							_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, -1, false, NOAAconf.UseUtf8);

							LogDebugMessage("PHP[Int]: Upload of NOAA reports complete");
							NOAAconf.NeedFtp = false;
						}
						catch (OperationCanceledException)
						{
							return false;
						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading NOAA Year file");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: NOAA Year report released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				}

				// Extra files
				LogDebugMessage("PHP[Int]: Extra Files upload starting");

				ExtraFiles
				.Where(x =>
					x.local.Length > 0 &&
					x.remote.Length > 0 &&
					!x.realtime &&
					(EODfilesNeedFTP || (EODfilesNeedFTP == x.endofday)) &&
					x.FTP)
				.ToList()
				.ForEach(item =>
				{
					Interlocked.Increment(ref taskCount);

					var uploadfile = item.local;
					var remotefile = item.remote;

					if (!File.Exists(uploadfile))
					{
						LogMessage($"PHP[Int]: Extra web file - {uploadfile} - not found!");
						return;
					}

					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: Extra file: {uploadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
						LogDebugMessage($"PHP[Int]: Extra file: {uploadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}


					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
							var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

							uploadfile = GetUploadFilename(uploadfile, logDay);

							remotefile = GetRemoteFileName(remotefile, logDay);

							// all checks OK, file needs to be uploaded
							if (item.process)
							{
								LogDebugMessage($"PHP[Int]: Uploading Extra file: {uploadfile} to: {remotefile} (Processed)");

								var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
								_ = await UploadString(phpUploadHttpClient, false, string.Empty, data, remotefile, -1, false, item.UTF8);
							}
							else
							{
								LogDebugMessage($"PHP[Int]: Uploading Extra file: {uploadfile} to: {remotefile}");

								_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, -1, false, item.UTF8);
							}
						}
						catch (Exception ex) when (!(ex is TaskCanceledException))
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading file {uploadfile} to: {remotefile}");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: Extra file: {uploadfile} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				});

				// wait for all the extra files to start
				//while (runningTaskCount < taskCount)
				//{
				//	await Task.Delay(100);
				//}
				// wait for all the extra files to complete
				//await Task.WhenAll(tasklist);
				//LogDebugMessage($"PHP[Int]: Extra Files upload complete, {tasklist.Count()} files processed");

				//tasklist.Clear();
				//taskCount = 0;
				//runningTaskCount = 0;

				if (EODfilesNeedFTP)
				{
					EODfilesNeedFTP = false;
				}


				// standard files
				LogDebugMessage("PHP[Int]: Standard files upload starting");

				StdWebFiles
				.Where(x => x.FTP)
				.ToList()
				.ForEach(item =>
				{
					Interlocked.Increment(ref taskCount);
					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: Standard Data file: {item.LocalFileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
						LogDebugMessage($"PHP[Int]: Standard Data file: {item.LocalFileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}


					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							string data;
							LogDebugMessage("PHP[Int]: Uploading standard Data file: " + item.RemoteFileName);

							if (item.LocalFileName == "wxnow.txt")
							{
								data = station.CreateWxnowFileString();
							}
							else
							{
								data = await ProcessTemplateFile2StringAsync(item.TemplateFileName, true, true);
							}

							if (await UploadString(phpUploadHttpClient, false, string.Empty, data, item.RemoteFileName, -1, false, true))
							{
								// No standard files are "one offs" at present
								//StdWebFiles[i].FtpRequired = false;
							}
						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading file {item.RemoteFileName}");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: Standard Data file: {item.LocalFileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				});

				// wait for all the standard files to start
				//while (runningTaskCount < taskCount)
				//{
				//	await Task.Delay(100);
				//}
				// wait for all the standard files to complete
				//await Task.WhenAll(tasklist);
				//LogDebugMessage($"PHP[Int]: Standard files upload complete, {tasklist.Count()} files processed");

				//tasklist.Clear();
				//taskCount = 0;
				//runningTaskCount = 0;


				// Graph Data Files
				LogDebugMessage("PHP[Int]: Graph files upload starting");

				var oldest = DateTime.Now.AddHours(-GraphHours);
				// Munge date/time into UTC becuase we use local time as UTC for highCharts consistency across TZs
				var oldestTs = Utils.ToPseudoJSTime(oldest).ToString();
				var configFiles = new string[] { "graphconfig.json", "availabledata.json", "dailyrain.json", "dailytemp.json", "sunhours.json" };

				GraphDataFiles
				.Where(x => x.FTP && x.FtpRequired)
				.ToList()
				.ForEach(item =>
				{
					Interlocked.Increment(ref taskCount);
					try {
#if DEBUG
						LogDebugMessage($"PHP[Int]: Graph data file: {item.LocalFileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
						LogDebugMessage($"PHP[Int]: Graph data file: {item.LocalFileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}


					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							// we want incremental data for PHP
							var json = station.CreateGraphDataJson(item.LocalFileName, item.Incremental);
							var remotefile = item.RemoteFileName;
							LogDebugMessage("PHP[Int]: Uploading graph data file: " + item.LocalFileName);

							if (await UploadString(phpUploadHttpClient, item.Incremental, oldestTs, json, remotefile, -1, false, true))
							{
								// The config files only need uploading once per change
								// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
								if (configFiles.Any(item.LocalFileName.Contains))
								{
									item.FtpRequired = false;
								}
								else
								{
									item.LastDataTime = DateTime.Now;
									item.Incremental = true;
								}
							}
						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading graph data file [{item.RemoteFileName}]");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: Graph data file: {item.LocalFileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				});

				// wait for all the graph files to start
				//while (runningTaskCount < taskCount)
				//{
				//	await Task.Delay(100);
				//}
				// wait for all the graph files to complete
				//await Task.WhenAll(tasklist);
				//LogDebugMessage($"PHP[Int]: Graph files upload complete, {tasklist.Count()} files processed");

				//tasklist.Clear();
				//taskCount = 0;
				//runningTaskCount = 0;



				// EOD Graph Data Files
				LogDebugMessage("PHP[Int]: EOD Graph files upload starting");

				GraphDataEodFiles
				.Where(x => x.FTP && x.FtpRequired)
				.ToList()
				.ForEach(item =>
				{
					Interlocked.Increment(ref taskCount);

					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: Daily graph data file: {item.LocalFileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
						LogDebugMessage($"PHP[Int]: Daily graph data file: {item.LocalFileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}

					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							var remotefile = item.RemoteFileName;
							LogMessage("PHP[Int]: Uploading daily graph data file: " + item.LocalFileName);
							var json = station.CreateEodGraphDataJson(item.LocalFileName);

							if (await UploadString(phpUploadHttpClient, false, "", json, remotefile, -1, false, true))
							{
								// Uploaded OK, reset the upload required flag
								item.FtpRequired = false;
							}
						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading daily graph data file [{item.RemoteFileName}]");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: Daily graph data file: {item.LocalFileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				});

				// wait for all the EOD files to start
				//while (runningTaskCount < taskCount)
				//{
				//	await Task.Delay(100);
				//}
				// wait for all the EOD files to complete
				//await Task.WhenAll(tasklist);
				//LogDebugMessage($"PHP[Int]: EOD Graph files upload complete, {tasklist.Count()} files processed");
				//tasklist.Clear();



				// Moon image
				if (MoonImage.Ftp && MoonImage.ReadyToFtp)
				{
					Interlocked.Increment(ref taskCount);

					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: Moon image waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
						LogDebugMessage($"PHP[Int]: Moon image has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);

#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}

					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							LogDebugMessage("PHP[Int]: Uploading Moon image file");

							if (await UploadFile(phpUploadHttpClient, "web" + DirectorySeparator + "moon.png", MoonImage.FtpDest, -1, true))
							{
								// clear the image ready for FTP flag, only upload once an hour
								MoonImage.ReadyToFtp = false;
							}
						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, "PHP[Int]: Error uploading moon image");
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"PHP[Int]: Moon image released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				}

				// wait for all the EOD files to start
				while (runningTaskCount < taskCount)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						LogDebugMessage($"PHP[Int]: Upload process aborted");
						return;
					}
					await Task.Delay(10);
				}
				// wait for all the EOD files to complete
				if (tasklist.Count > 0)
				{
					try
					{
						Task.WaitAll(tasklist.ToArray(), cancellationToken);
					}
					catch(Exception ex)
					{
						LogExceptionMessage(ex, "PHP[Int]: Eror waiting on upload tasks");
					}
				}
				//LogDebugMessage($"PHP[Int]: EOD Graph files upload complete, {tasklist.Count()} files processed");

				if (cancellationToken.IsCancellationRequested)
				{
					LogDebugMessage($"PHP[Int]: Upload process aborted");
					return;
				}

				LogDebugMessage($"PHP[Int]: Upload process complete, {tasklist.Count()} files processed");
				tasklist.Clear();

				return;
			}

			LogDebugMessage("PHP[Int]: Upload process complete");
		}

		// Return True if the connection still exists
		// Return False if the connection is disposed, null, or not connected
		private bool UploadFile(FtpClient conn, string localfile, string remotefile, int cycle = -1)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			LogFtpMessage("");

			try
			{
				if (!File.Exists(localfile))
				{
					LogMessage($"FTP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
					return true;
				}

				using (Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					UploadStream(conn, remotefile, istream, cycle);
				}
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error reading {localfile} - {ex.Message}");
				if (ex.InnerException != null)
				{
					LogFtpMessage($"FTP[{cycleStr}]: Inner Exception: {ex.GetBaseException().Message}");
				}
			}

			return conn.IsConnected;
		}

		private bool UploadStream(FtpClient conn, string remotefile, Stream dataStream, int cycle = -1)
		{
			string remotefiletmp = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			try
			{
				if (dataStream.Length == 0)
				{
					LogMessage($"FTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
					return true;
				}

				if (FTPRename)
				{
					// delete the existing tmp file
					try
					{
						if (conn.FileExists(remotefiletmp))
						{
							conn.DeleteFile(remotefiletmp);
						}
					}
					catch
					{
						// continue on error
					}
				}

				if (DeleteBeforeUpload)
				{
					// delete the existing file
					try
					{
						LogFtpDebugMessage($"FTP[{cycleStr}]: Deleting {remotefile}");
						if (conn.FileExists(remotefile))
						{
							conn.DeleteFile(remotefile);
						}
						else
						{
							LogFtpDebugMessage($"FTP[{cycleStr}]: Cannot delete remote file {remotefile} as it does not exist");
						}
					}
					catch (Exception ex)
					{
						LogFtpMessage($"FTP[{cycleStr}]: Error deleting {remotefile} : {ex.Message}");
						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}");
						}
						// continue on error
					}
				}

				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {remotefiletmp}");

				FtpStatus status;
				status = conn.UploadStream(dataStream, remotefiletmp);

				if (status.IsFailure())
				{
					LogMessage($"FTP[{cycleStr}]: Upload of {remotefile} failed");
				}
				else if (FTPRename)
				{
					// rename the file
					LogFtpDebugMessage($"FTP[{cycleStr}]: Renaming {remotefiletmp} to {remotefile}");

					try
					{
						conn.Rename(remotefiletmp, remotefile);
						LogFtpDebugMessage($"FTP[{cycleStr}]: Renamed {remotefiletmp}");
					}
					catch (Exception ex)
					{
						LogFtpMessage($"FTP[{cycleStr}]: Error renaming {remotefiletmp} to {remotefile} : {ex.Message}");
						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}");
						}

						return conn.IsConnected;
					}
				}
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error uploading {remotefile} : {ex.Message}");
				if (ex.InnerException != null)
				{
					LogFtpMessage($"FTP[{cycleStr}]: Inner Exception: {ex.GetBaseException().Message}");
				}
			}

			return conn.IsConnected;
		}

		public Stream GenerateStreamFromString(string s)
		{
			MemoryStream stream = new MemoryStream();
			StreamWriter writer = new StreamWriter(stream);
			writer.Write(s);
			writer.Flush();
			stream.Position = 0;
			return stream;
		}

		// Return True if the connection still exists
		// Return False if the connection is disposed, null, or not connected
		private bool UploadFile(SftpClient conn, string localfile, string remotefile, int cycle=-1)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (!File.Exists(localfile))
			{
				LogMessage($"SFTP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
				return true;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {localfile}");
					RealtimeFTPReconnect();
					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {localfile}");
				RealtimeFTPReconnect();
				return false;
			}

			try
			{
				using (Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					UploadStream(conn, remotefile, istream, cycle);
				}
			}
			catch (Exception ex)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: Error reading {localfile} - {ex.Message}");
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogFtpMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}");
				}

			}
			return true;
		}

		private bool UploadStream(SftpClient conn, string remotefile, Stream dataStream, int cycle=-1)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (dataStream.Length == 0)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
				return false;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {remotefile}");
					RealtimeFTPReconnect();
					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {remotefile}");
				RealtimeFTPReconnect();
				return false;
			}

			try
			{
				// No delete before upload required for SFTP as we use the overwrite flag
				try
				{
					LogFtpDebugMessage($"SFTP[{cycleStr}]: Uploading {remotefilename}");

					conn.OperationTimeout = TimeSpan.FromSeconds(15);
					conn.UploadFile(dataStream, remotefilename, true);
					dataStream.Close();

					LogFtpDebugMessage($"SFTP[{cycleStr}]: Uploaded {remotefilename}");
				}
				catch (ObjectDisposedException)
				{
					LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
					return false;
				}
				catch (Exception ex)
				{
					LogFtpMessage($"SFTP[{cycleStr}]: Error uploading {remotefilename} : {ex.Message}");

					if (ex.Message.Contains("Permission denied")) // Non-fatal
						return true;

					if (ex.InnerException != null)
					{
						ex = Utils.GetOriginalException(ex);
						LogFtpMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}");
					}

					// Lets start again anyway! Too hard to tell if the error is recoverable
					conn.Dispose();
					return false;
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
					catch (ObjectDisposedException)
					{
						LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
						return false;
					}
					catch (Exception ex)
					{
						LogFtpMessage($"SFTP[{cycleStr}]: Error renaming {remotefilename} to {remotefile} : {ex.Message}");
						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}");
						}

						return true;
					}
				}
				LogFtpDebugMessage($"SFTP[{cycleStr}]: Completed uploading {remotefile}");
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
				return false;
			}
			catch (Exception ex)
			{
				LogFtpMessage($"SFTP[{cycleStr}]: Error uploading {remotefile} - {ex.Message}");
				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogFtpMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}");
				}

			}
			return true;
		}

		// Return True if the upload worked
		// Return False if the upload failed
		private async Task<bool> UploadFile(HttpClient httpclient, string localfile, string remotefile, int cycle = -1, bool binary = false, bool utf8=true)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			//return true;

			try
			{
				if (!File.Exists(localfile))
				{
					LogMessage($"PHP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
					return false;
				}

				var encoding = utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("iso-8859-1");

				string data;
				if (binary)
				{
					// change binary files to base64 string
					data = Convert.ToBase64String(await Utils.ReadAllBytesAsync(localfile));
				}
				else
				{
					data = await Utils.ReadAllTextAsync(localfile, encoding);
				}

				return await UploadString(httpclient, false, string.Empty, data, remotefile, cycle, binary, utf8);
			}
			catch (Exception ex)
			{
				LogDebugMessage($"PHP[{cycleStr}]: Error - {ex.Message}");
				return false;
			}
		}

		private async Task<bool> UploadString(HttpClient httpclient, bool incremental, string oldest, string data, string remotefile, int cycle = -1, bool binary = false, bool utf8=true)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (string.IsNullOrEmpty(data))
			{
				LogMessage($"PHP[{cycleStr}]: Uploading to {remotefile}. Error: The data string is empty, ignoring this upload");
			}

			LogDebugMessage($"PHP[{cycleStr}]: Uploading to {remotefile}");

			// we will try this twice incase the first attempt fails with a closed connection
			var retry = 0;
			do
			{
				MemoryStream outStream = null;
				StreamContent streamContent = null;

				try
				{
					var encoding = new UTF8Encoding(false);

					using (var request = new HttpRequestMessage(HttpMethod.Post, FtpOptions.PhpUrl))
					{
						var unixTs = Utils.ToUnixTime(DateTime.Now).ToString();
						var signature = Utils.GetSHA256Hash(FtpOptions.PhpSecret, unixTs + remotefile + data);

						// disable expect 100 - PHP doesn't support it
						request.Headers.ExpectContinue = false;
						request.Headers.Add("ACTION", incremental ? "append" : "replace");
						request.Headers.Add("FILE", remotefile);
						if (incremental)
						{
							request.Headers.Add("OLDEST", oldest);
						}
						request.Headers.Add("TS", unixTs);
						request.Headers.Add("SIGNATURE", signature);
						request.Headers.Add("BINARY", binary ? "1" : "0");
						request.Headers.Add("UTF8", utf8 ? "1" : "0");
						// Compress? if supported and payload exceeds 500 bytes
						if (data.Length < 500 || FtpOptions.PhpCompression == "none")
						{
							request.Content = new StringContent(data, encoding, "application/octet-stream");
						}
						else
						{
							using (var ms = new System.IO.MemoryStream())
							{
								if (FtpOptions.PhpCompression == "gzip")
								{
									using (var zipped = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress, true))
									{
										var byteData = encoding.GetBytes(data);
										zipped.Write(byteData, 0, byteData.Length);
									}
								}
								else if (FtpOptions.PhpCompression == "deflate")
								{
									using (var zipped = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true))
									{
										var byteData = encoding.GetBytes(data);
										zipped.Write(byteData, 0, byteData.Length);
									}
								}

								ms.Position = 0;
								byte[] compressed = new byte[ms.Length];
								ms.Read(compressed, 0, compressed.Length);

								outStream = new MemoryStream(compressed);
								streamContent = new StreamContent(outStream);
								streamContent.Headers.Add("Content-Encoding", FtpOptions.PhpCompression);
								streamContent.Headers.ContentLength = outStream.Length;

								request.Content = streamContent;
							}
						}

						using (var response = await httpclient.SendAsync(request))
						{
							//response.EnsureSuccessStatusCode();
							var responseBodyAsText = await response.Content.ReadAsStringAsync();
							if (response.StatusCode != HttpStatusCode.OK)
							{
								LogMessage($"PHP[{cycleStr}]: Upload to {remotefile}: Response code = {(int) response.StatusCode}: {response.StatusCode}");
								LogMessage($"PHP[{cycleStr}]: Upload to {remotefile}: Response text follows:\n{responseBodyAsText}");
							}
							else
							{
								LogDebugMessage($"PHP[{cycleStr}]: Upload to {remotefile}: Response code = {(int) response.StatusCode}: {response.StatusCode}");
								LogDataMessage($"PHP[{cycleStr}]: Upload to {remotefile}: Response text follows:\n{responseBodyAsText}");
							}

							if (outStream != null)
								outStream.Dispose();

							if (streamContent != null)
								streamContent.Dispose();

							return response.StatusCode == HttpStatusCode.OK;
						}
					}
				}
				catch (System.Net.Http.HttpRequestException ex)
				{
					LogExceptionMessage(ex, $"PHP[{cycleStr}]: Error uploading to {remotefile}");
					retry++;
					if (retry < 2)
					{
						LogMessage($"PHP[{cycleStr}] Retrying upload to {remotefile}");
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"PHP[{cycleStr}]: Error uploading to {remotefile}");
					retry = 99;
				}
				finally
				{
					if (outStream != null)
						outStream.Dispose();

					if (streamContent != null)
						streamContent.Dispose();
				}
			} while (retry < 2);

			return false;
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
			if (!string.IsNullOrEmpty(message))
				LogMessage(message);
			LogFluentFtpMessage(FtpTraceLevel.Info, message);
		}

		public void LogFtpDebugMessage(string message)
		{
			if (FtpOptions.Logging && (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.SFTP))
			{
				if (!string.IsNullOrEmpty(message))
					LogDebugMessage(message);
				LogFluentFtpMessage(FtpTraceLevel.Info, message);
			}
		}

		public void LogFluentFtpMessage(FtpTraceLevel level, string message)
		{
			if (FtpOptions.Logging && (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.SFTP))
			{
				try
				{
					if (string.IsNullOrEmpty(message))
						FtpTraceListener.WriteLine(string.Empty);
					else
						FtpTraceListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
					FtpTraceListener.Flush();
				}
				catch (Exception ex)
				{
					LogDebugMessage($"LogFluentFtpMessage: Error = {ex.Message}");
				}
			}
		}

		public void LogConsoleMessage(string message, ConsoleColor colour = ConsoleColor.White, bool LogDateTime = false)
		{
			if (!Program.service)
			{
				if (LogDateTime)
				{
					message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message;
				}
				Console.ForegroundColor = colour;
				Console.WriteLine(message);
				Console.ResetColor();
			}

			Program.svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			Program.svcTextListener.Flush();
		}

		public void LogExceptionMessage(Exception ex, string message)
		{
			LogMessage(message);
			LogMessage(message + " - " + Utils.ExceptionToString(ex));
		}

		/*
		public string ReplaceCommas(string AStr)
		{
			return AStr.Replace(',', '.');
		}
		*/

		private void CreateRealtimeFile(int cycle)
		{
			// Does the user want to create the realtime.txt file?
			if (!RealtimeFiles[0].Create)
			{
				return;
			}

			var filename = AppDir + RealtimeFile;

			try
			{
				LogDebugMessage($"Realtime[{cycle}]: Creating realtime.txt");
				File.WriteAllText(filename, CreateRealtimeFileString(cycle));
			}
			catch (Exception ex)
			{
				LogMessage("Error encountered during Realtime file update.");
				LogMessage(ex.Message);
			}
		}

		private string CreateRealtimeFileString(int cycle)
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
			13     6          wind speed (Beaufort)
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
			53     2040       Cloud base
			54     ft         Cloud base units
			55     12.3       Apparent Temp
			56     11.4       Sunshine hours today
			57     420        Current theoretical max solar radiation
			58     1          Is sunny?
			59     8.4        Feels Like temperature
		  */

			DateTime timestamp = DateTime.Now;

			try
			{
				var InvC = new CultureInfo("");
				var sb = new StringBuilder();

				sb.Append(timestamp.ToString("dd/MM/yy HH:mm:ss "));                          // 1, 2
				sb.Append(station.OutdoorTemperature.ToString(TempFormat, InvC) + ' ');       // 3
				sb.Append(station.OutdoorHumidity.ToString() + ' ');                          // 4
				sb.Append(station.OutdoorDewpoint.ToString(TempFormat, InvC) + ' ');          // 5
				sb.Append(station.WindAverage.ToString(WindAvgFormat, InvC) + ' ');           // 6
				sb.Append(station.WindLatest.ToString(WindFormat, InvC) + ' ');               // 7
				sb.Append(station.Bearing.ToString() + ' ');                                  // 8
				sb.Append(station.RainRate.ToString(RainFormat, InvC) + ' ');                 // 9
				sb.Append(station.RainToday.ToString(RainFormat, InvC) + ' ');                // 10
				sb.Append(station.Pressure.ToString(PressFormat, InvC) + ' ');                // 11
				sb.Append(station.CompassPoint(station.Bearing) + ' ');                       // 12
				sb.Append(Beaufort(station.WindAverage) + ' ');                               // 13
				sb.Append(Units.WindText + ' ');                                              // 14
				sb.Append(Units.TempText[1].ToString() + ' ');                                // 15
				sb.Append(Units.PressText + ' ');                                             // 16
				sb.Append(Units.RainText + ' ');                                              // 17
				sb.Append(station.WindRunToday.ToString(WindRunFormat, InvC) + ' ');          // 18
				sb.Append(station.presstrendval.ToString(PressTrendFormat, InvC) + ' ');      // 19
				sb.Append(station.RainMonth.ToString(RainFormat, InvC) + ' ');                // 20
				sb.Append(station.RainYear.ToString(RainFormat, InvC) + ' ');                 // 21
				sb.Append(station.RainYesterday.ToString(RainFormat, InvC) + ' ');            // 22
				sb.Append(station.IndoorTemperature.ToString(TempFormat, InvC) + ' ');        // 23
				sb.Append(station.IndoorHumidity.ToString() + ' ');                           // 24
				sb.Append(station.WindChill.ToString(TempFormat, InvC) + ' ');                // 25
				sb.Append(station.temptrendval.ToString(TempTrendFormat, InvC) + ' ');        // 26
				sb.Append(station.HiLoToday.HighTemp.ToString(TempFormat, InvC) + ' ');       // 27
				sb.Append(station.HiLoToday.HighTempTime.ToString("HH:mm "));                 // 28
				sb.Append(station.HiLoToday.LowTemp.ToString(TempFormat, InvC) + ' ');        // 29
				sb.Append(station.HiLoToday.LowTempTime.ToString("HH:mm "));                  // 30
				sb.Append(station.HiLoToday.HighWind.ToString(WindAvgFormat, InvC) + ' ');    // 31
				sb.Append(station.HiLoToday.HighWindTime.ToString("HH:mm "));                 // 32
				sb.Append(station.HiLoToday.HighGust.ToString(WindFormat, InvC) + ' ');       // 33
				sb.Append(station.HiLoToday.HighGustTime.ToString("HH:mm "));                 // 34
				sb.Append(station.HiLoToday.HighPress.ToString(PressFormat, InvC) + ' ');     // 35
				sb.Append(station.HiLoToday.HighPressTime.ToString("HH:mm "));                // 36
				sb.Append(station.HiLoToday.LowPress.ToString(PressFormat, InvC) + ' ');      // 37
				sb.Append(station.HiLoToday.LowPressTime.ToString("HH:mm "));                 // 38
				sb.Append(Version + ' ');                                                     // 39
				sb.Append(Build + ' ');                                                       // 40
				sb.Append(station.RecentMaxGust.ToString(WindFormat, InvC) + ' ');            // 41
				sb.Append(station.HeatIndex.ToString(TempFormat, InvC) + ' ');                // 42
				sb.Append(station.Humidex.ToString(TempFormat, InvC) + ' ');                  // 43
				sb.Append(station.UV.ToString(UVFormat, InvC) + ' ');                         // 44
				sb.Append(station.ET.ToString(ETFormat, InvC) + ' ');                         // 45
				sb.Append((Convert.ToInt32(station.SolarRad)).ToString() + ' ');              // 46
				sb.Append(station.AvgBearing.ToString() + ' ');                               // 47
				sb.Append(station.RainLastHour.ToString(RainFormat, InvC) + ' ');             // 48
				sb.Append(station.Forecastnumber.ToString() + ' ');                           // 49
				sb.Append(IsDaylight() ? "1 " : "0 ");                                        // 50
				sb.Append(station.SensorContactLost ? "1 " : "0 ");                           // 51
				sb.Append(station.CompassPoint(station.AvgBearing) + ' ');                    // 52
				sb.Append((Convert.ToInt32(station.CloudBase)).ToString() + ' ');             // 53
				sb.Append(CloudBaseInFeet ? "ft " : "m ");                                    // 54
				sb.Append(station.ApparentTemperature.ToString(TempFormat, InvC) + ' ');      // 55
				sb.Append(station.SunshineHours.ToString(SunFormat, InvC) + ' ');             // 56
				sb.Append(Convert.ToInt32(station.CurrentSolarMax).ToString() + ' ');         // 57
				sb.Append(station.IsSunny ? "1 " : "0 ");                                     // 58
				sb.AppendLine(station.FeelsLike.ToString(TempFormat, InvC));                  // 59
				return sb.ToString();
			}
			catch (Exception ex)
			{
				LogMessage("Error encountered during Realtime file update.");
				LogMessage(ex.Message);
			}
			return string.Empty;
		}


		public void MySqlRealtimeFile(int cycle, bool live, DateTime? logdate = null)
		{
			DateTime timestamp = (DateTime)(live ? DateTime.Now : logdate);

			if (!MySqlSettings.Realtime.Enabled)
				return;

			if (MySqlSettings.RealtimeLimit1Minute && MySqlLastRealtimeTime.Minute == timestamp.Minute)
				return;

			MySqlLastRealtimeTime = timestamp;

			var InvC = new CultureInfo("");

			StringBuilder values = new StringBuilder(RealtimeTable.StartOfInsert, 1024);
			values.Append(" Values('");
			values.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "',");
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
			values.Append(')');

			string valuesString = values.ToString();
			List<string> cmds = new List<string>() { valuesString };

			if (live)
			{
				if (!string.IsNullOrEmpty(MySqlSettings.RealtimeRetention))
				{
					cmds.Add($"DELETE IGNORE FROM {MySqlSettings.Realtime.TableName} WHERE LogDateTime < DATE_SUB('{DateTime.Now:yyyy-MM-dd HH:mm}', INTERVAL {MySqlSettings.RealtimeRetention});");
				}

				// do the update
				_ = CheckMySQLFailedUploads($"Realtime[{cycle}]", cmds);
			}
			else
			{
				// not live, buffer the command for later
				MySqlList.Enqueue(new SqlCache() { statement = cmds[0] });
			}
		}

		private void ProcessTemplateFile(string template, string outputfile, bool useAppDir)
		{

			var output = ProcessTemplateFile2String(template, useAppDir);

			if (output != string.Empty)
			{
				var utf8WithoutBom = new UTF8Encoding(false);
				var encoding = UTF8encode ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");

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

		private string ProcessTemplateFile2String(string template, bool useAppDir, bool utf8=false)
		{
			string templatefile = template;

			if (useAppDir)
			{
				templatefile = AppDir + template;
			}

			if (File.Exists(templatefile))
			{
				var parser = new TokenParser(TokenParserOnToken);
				parser.Encoding = utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("iso-8859-1");
				parser.SourceFile = templatefile;
				return parser.ToString();
			}
			else
			{
				LogMessage($"ProcessTemplateFile: Error, template file not found - {templatefile}");
			}
			return string.Empty;
		}

		private async Task<string> ProcessTemplateFile2StringAsync(string template, bool useAppDir, bool utf8 = false)
		{
			string templatefile = template;

			if (useAppDir)
			{
				templatefile = AppDir + template;
			}

			if (File.Exists(templatefile))
			{
				var parser = new TokenParser(TokenParserOnToken);
				parser.SourceFile = templatefile;
				parser.Encoding = utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("iso-8859-1");
				return await parser.ToStringAsync();
			}
			else
			{
				LogMessage($"ProcessTemplateFile: Error, template file not found - {templatefile}");
			}
			return string.Empty;
		}


		public void StartTimersAndSensors()
		{
			LogMessage("Start Extra Sensors");
			airLinkOut?.Start();
			airLinkIn?.Start();
			ecowittExtra?.Start();
			ambientExtra?.Start();

			LogMessage("Start Timers");
			// start the general one-minute timer
			LogMessage("Starting 1-minute timer");
			station.StartMinuteTimer();
			LogMessage($"Data logging interval = {DataLogInterval} ({logints[DataLogInterval]} mins)");


			if (RealtimeIntervalEnabled)
			{
				if (FtpOptions.Enabled && FtpOptions.RealtimeEnabled && FtpOptions.FtpMode != FtpProtocols.PHP)
				{
					LogConsoleMessage("Connecting real time FTP");

					if (FtpOptions.FtpMode == FtpProtocols.SFTP)
					{
						RealtimeSSHLogin();
					}
					else
					{
						RealtimeFTPLogin();
					}
				}

				LogMessage("Starting Realtime timer, interval = " + RealtimeInterval / 1000 + " seconds");
			}
			else
			{
				LogMessage("Realtime not enabled");
			}

			RealtimeTimer.Enabled = RealtimeIntervalEnabled;

			CustomMysqlSecondsTimer.Enabled = MySqlSettings.CustomSecs.Enabled;

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

			if (MySqlList.IsEmpty)
			{
				// No archived entries to upload
				//MySqlList = null;
				LogDebugMessage("MySqlList is Empty");
			}
			else
			{
				// start the archive upload thread
				LogMessage($"Starting MySQL catchup thread. Found {MySqlList.Count} commands to execute");
				_ = MySqlCommandAsync(MySqlList, "MySQL Archive", false);
			}

			WebTimer.Interval = UpdateInterval * 60 * 1000; // mins to millisecs
			WebTimer.Enabled = WebIntervalEnabled && !SynchronisedWebUpdate;

			AwekasTimer.Enabled = AWEKAS.Enabled && !AWEKAS.SynchronisedUpdate;

			EnableOpenWeatherMap();

			NormalRunning = true;
			LogMessage("Normal running");
			LogConsoleMessage("Normal running", ConsoleColor.Green);
		}

		private async void CustomMysqlSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"CustomMySqlTimedUpdate: Not all data is ready, aborting process");
				return;
			}

			if (!customMySqlSecondsUpdateInProgress)
			{
				customMySqlSecondsUpdateInProgress = true;

				var tokenParser = new TokenParser(TokenParserOnToken);

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(MySqlSettings.CustomSecs.Commands[i]))
						{
							tokenParser.InputText = MySqlSettings.CustomSecs.Commands[i];
							await CheckMySQLFailedUploads($"CustomSqlSecs[{i}]", tokenParser.ToStringFromString());
						}
					}
					catch (Exception ex)
					{
						LogMessage($"CustomSqlSecs[{i}]: Error - " + ex.Message);
					}
				}
				customMySqlSecondsUpdateInProgress = false;
			}
		}


		internal async void CustomMysqlMinutesTimerTick()
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"CustomMysqlMinutesTimerTick: Not all data is ready, aborting process");
				return;
			}

			if (!customMySqlMinutesUpdateInProgress)
			{
				customMySqlMinutesUpdateInProgress = true;

				var tokenParser = new TokenParser(TokenParserOnToken);

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(MySqlSettings.CustomMins.Commands[i]))
						{
							tokenParser.InputText = MySqlSettings.CustomMins.Commands[i];
							await CheckMySQLFailedUploads($"CustomSqlMins[{i}]", tokenParser.ToStringFromString());
						}
					}
					catch (Exception ex)
					{
						LogMessage($"CustomSqlMins[{i}]: Error - " + ex.Message);
					}
				}
				customMySqlMinutesUpdateInProgress = false;
			}
		}

		internal async void CustomMysqlRolloverTimerTick()
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if (!customMySqlRolloverUpdateInProgress)
			{
				customMySqlRolloverUpdateInProgress = true;

				var tokenParser = new TokenParser(TokenParserOnToken);

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(MySqlSettings.CustomRollover.Commands[i]))
						{
							tokenParser.InputText = MySqlSettings.CustomRollover.Commands[i];
							await CheckMySQLFailedUploads($"CustomSqlRollover[{i}]", tokenParser.ToStringFromString());
						}
					}
					catch (Exception ex)
					{
						LogMessage($"CustomSqlRollover[{i}]: Error - " + ex.Message);
					}
				}
				customMySqlRolloverUpdateInProgress = false;
			}
		}

		internal async void CustomMySqlTimedUpdate(DateTime now)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"CustomMySqlTimedUpdate: Not all data is ready, aborting process");
				return;
			}

			if (!customMySqlTimedUpdateInProgress)
			{
				customMySqlTimedUpdateInProgress = true;

				var tokenParser = new TokenParser(TokenParserOnToken);

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(MySqlSettings.CustomTimed.Commands[i]) && MySqlSettings.CustomTimed.NextUpdate[i] <= now)
						{
							tokenParser.InputText = MySqlSettings.CustomTimed.Commands[i];
							await CheckMySQLFailedUploads($"CustomSqlTimed[{i}]", tokenParser.ToStringFromString());
							MySqlSettings.CustomTimed.SetNextInterval(i, now);
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"CustomSqlTimed[{i}]: Error excuting: {MySqlSettings.CustomTimed.Commands[i]} ");
					}
				}
				customMySqlTimedUpdateInProgress = false;
			}
		}

		public async Task CheckMySQLFailedUploads(string callingFunction, string cmd)
		{
			await CheckMySQLFailedUploads(callingFunction, new List<string>() { cmd });
		}

		public async Task CheckMySQLFailedUploads(string callingFunction, List<string> cmds)
		{
			var connectionOK = true;

			try
			{
				if (!MySqlFailedList.IsEmpty)
				{
					// flag we are processing the queue so the next task doesn't try as well
					SqlCatchingUp = true;

					LogMessage($"{callingFunction}: Failed MySQL updates are present");
					if (MySqlCheckConnection())
					{
						Thread.Sleep(500);
						LogMessage($"{callingFunction}: Connection to MySQL server is OK, trying to upload {MySqlFailedList.Count} failed commands");

						await MySqlCommandAsync(MySqlFailedList, callingFunction, true);
						LogMessage($"{callingFunction}: Upload of failed MySQL commands complete");
					}
					else if (MySqlSettings.BufferOnfailure)
					{
						connectionOK = false;
						LogMessage($"{callingFunction}: Connection to MySQL server has failed, adding this update to the failed list");
						if (callingFunction.StartsWith("Realtime["))
						{
							var tmp = new SqlCache() { statement = cmds[0] };
							_ = station.RecentDataDb.Insert(tmp);

							// don't bother buffering the realtime deletes - if present
							MySqlFailedList.Enqueue(tmp);
						}
						else
						{
							for (var i = 0; i < cmds.Count; i++)
							{
								var tmp = new SqlCache() { statement = cmds[i] };

								_ = station.RecentDataDb.Insert(tmp);

								MySqlFailedList.Enqueue(tmp);
							}
						}
					}
					else
					{
						connectionOK = false;
					}
					SqlCatchingUp = false;
				}

				// now do what we came here to do
				if (connectionOK)
				{
					await MySqlCommandAsync(cmds, callingFunction);
				}
			}
			catch (Exception ex)
			{
				LogMessage($"{callingFunction}: Error - " + ex.Message);
				SqlCatchingUp = false;
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
						// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
						var logDay = DateTime.Now.AddDays(-1);

						uploadfile = GetUploadFilename(uploadfile, logDay);

						if (File.Exists(uploadfile))
						{
							remotefile = GetRemoteFileName(remotefile, logDay);

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
									if (ExtraFiles[i].process)
									{
										var data = ProcessTemplateFile2String(uploadfile, false, ExtraFiles[i].UTF8);
										File.WriteAllText(remotefile, data);
									}
									else
									{
										// just copy the file
										File.Copy(uploadfile, remotefile, true);
									}
								}
								catch (Exception ex)
								{
									LogExceptionMessage(ex, $"EOD: Error copying extra file {uploadfile} to {remotefile}");
								}
								//LogDebugMessage("Finished copying extra file " + uploadfile);
							}
						}
						else
						{
							LogMessage($"EOD: Error extra file {uploadfile} not found");
						}
					}
				}
			}
		}

		public void RealtimeFTPDisconnect()
		{
			// no disconnect required for PHP
			if (FtpOptions.FtpMode == FtpProtocols.PHP)
				return;

			try
			{
				if (FtpOptions.FtpMode == FtpProtocols.SFTP && RealtimeSSH != null)
				{
					RealtimeSSH.Disconnect();
				}
				else if (RealtimeFTP != null)
				{
					RealtimeFTP.Disconnect();
					RealtimeFTP.Dispose();
				}
				LogDebugMessage("Disconnected Realtime FTP session");
			}
			catch (Exception ex)
			{
				LogDebugMessage("RealtimeFTPDisconnect: Error disconnecting connection (can be ignored?) - " + ex.Message);
			}
		}

		private void RealtimeFTPLogin()
		{
			LogMessage($"RealtimeFTPLogin: Attempting realtime FTP connect to host {FtpOptions.Hostname} on port {FtpOptions.Port}");

			// dispose of the previous FTP client
			if (RealtimeFTP != null && !RealtimeFTP.IsDisposed)
			{
				RealtimeFTP.Dispose();
			}

			RealtimeFTP = new FtpClient();

			//RealtimeTimer.Enabled = false;
			RealtimeFTP.Host = FtpOptions.Hostname;
			RealtimeFTP.Port = FtpOptions.Port;
			RealtimeFTP.Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password);
			RealtimeFTP.Config.SocketPollInterval = 20000; // increase beyond the timeout values
			RealtimeFTP.LegacyLogger = LogFluentFtpMessage;

			if (!FtpOptions.AutoDetect)
			{
				if (FtpOptions.FtpMode == FtpProtocols.FTPS)
				{
					RealtimeFTP.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
					RealtimeFTP.Config.DataConnectionEncryption = true;
					// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specify protocols
					// b3155 - switch to default again - this will use the highest version available in the OS
					//RealtimeFTP.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
					LogDebugMessage($"RealtimeFTPLogin: Using FTPS protocol");
				}

				if (FtpOptions.ActiveMode)
				{
					RealtimeFTP.Config.DataConnectionType = FtpDataConnectionType.PORT;
					LogDebugMessage("RealtimeFTPLogin: Using Active FTP mode");
				}
				else if (FtpOptions.DisableEPSV)
				{
					RealtimeFTP.Config.DataConnectionType = FtpDataConnectionType.PASV;
					LogDebugMessage("RealtimeFTPLogin: Disabling EPSV mode");
				}
				else
				{
					RealtimeFTP.Config.DataConnectionType = FtpDataConnectionType.EPSV;
				}
			}

			if (FtpOptions.FtpMode == FtpProtocols.FTPS)
			{
				RealtimeFTP.Config.ValidateAnyCertificate = FtpOptions.IgnoreCertErrors;
			}

			if (FtpOptions.Enabled)
			{
				LogMessage($"RealtimeFTPLogin: Attempting realtime FTP connect to host {FtpOptions.Hostname} on port {FtpOptions.Port}");
				try
				{
					if (FtpOptions.AutoDetect)
					{
						RealtimeFTP.AutoConnect();
					}
					else
					{
						RealtimeFTP.Connect();
					}
					LogMessage("RealtimeFTPLogin: Realtime FTP connected");
					RealtimeFTP.Config.SocketKeepAlive = true;
				}
				catch (Exception ex)
				{
					LogMessage($"RealtimeFTPLogin: Error connecting ftp - {ex.Message}");
					if (ex.InnerException != null)
					{
						ex = Utils.GetOriginalException(ex);
						LogMessage($"RealtimeFTPLogin: Base exception - {ex.Message}");
					}
					RealtimeFTP.Disconnect();
				}
			}
			//RealtimeTimer.Enabled = true;
		}

		private void RealtimeSSHLogin()
		{
			if (FtpOptions.Enabled)
			{
				LogMessage($"RealtimeSSHLogin: Attempting realtime SFTP connect to host {FtpOptions.Hostname} on port {FtpOptions.Port}");
				try
				{
					// BUILD 3092 - added alternate SFTP authentication options
					ConnectionInfo connectionInfo;
					PrivateKeyFile pskFile;
					if (FtpOptions.SshAuthen == "password")
					{
						connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password));
						LogDebugMessage("RealtimeSSHLogin: Connecting using password authentication");
					}
					else if (FtpOptions.SshAuthen == "psk")
					{
						pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
						connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
						LogDebugMessage("RealtimeSSHLogin: Connecting using PSK authentication");
					}
					else if (FtpOptions.SshAuthen == "password_psk")
					{
						pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
						connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password), new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
						LogDebugMessage("RealtimeSSHLogin: Connecting using password or PSK authentication");
					}
					else
					{
						LogMessage($"RealtimeSSHLogin: Invalid SshftpAuthentication specified [{FtpOptions.SshAuthen}]");
						return;
					}

					// If the ECDSA ciphers are not available - yes I'm talking about you Mono! - remove them from the list to be tried
					try
					{
						using (var ecdsa = new System.Security.Cryptography.ECDsaCng())
						{/* Noop */}
					}
					catch (NotImplementedException)
					{
						var algsToRemove = connectionInfo.HostKeyAlgorithms.Keys.Where(algName => algName.StartsWith("ecdsa")).ToArray();
						foreach (var algName in algsToRemove)
							connectionInfo.HostKeyAlgorithms.Remove(algName);
					}

					RealtimeSSH = new SftpClient(connectionInfo);

					//if (RealtimeSSH != null) RealtimeSSH.Dispose();
					//RealtimeSSH = new SftpClient(ftp_host, ftp_port, ftp_user, ftp_password);

					RealtimeSSH.Connect();
					RealtimeSSH.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);  // 15 seconds to match FTP default timeout
					RealtimeSSH.KeepAliveInterval = new TimeSpan(0, 0, 31);         // 31 second keep-alive
					LogMessage("RealtimeSSHLogin: Realtime SFTP connected");
				}
				catch (Exception ex)
				{
					LogMessage($"RealtimeSSHLogin: Error connecting SFTP - {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Process the list of WU updates created at start-up from logger entries
		/// </summary>
		private async void WundCatchup()
		{
			Wund.Updating = true;
			for (int i = 0; i < WundList.Count; i++)
			{
				LogMessage("Uploading WU archive #" + (i + 1));
				try
				{
					using (var response = await MyHttpClient.GetAsync(WundList[i]))
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
					using (var response = await MyHttpClient.GetAsync(WindyList[i]))
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
		/// Process the list of PWS Weather updates created at start-up from logger entries
		/// </summary>
		private async void PWSCatchUp()
		{
			PWS.Updating = true;

			for (int i = 0; i < PWSList.Count; i++)
			{
				LogMessage("Uploading PWS archive #" + (i + 1));
				try
				{
					using (var response = await MyHttpClient.GetAsync(PWSList[i]))
					{
						var responseBodyAsText = await response.Content.ReadAsStringAsync();
						LogMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
					}
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
		/// Process the list of WOW updates created at start-up from logger entries
		/// </summary>
		private async void WOWCatchUp()
		{
			WOW.Updating = true;

			for (int i = 0; i < WOWList.Count; i++)
			{
				LogMessage("Uploading WOW archive #" + (i + 1));
				try
				{
					using (var response = await MyHttpClient.GetAsync(WOWList[i]))
					{
						var responseBodyAsText = await response.Content.ReadAsStringAsync();
						LogMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
					}
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
		/// Process the list of OpenWeatherMap updates created at start-up from logger entries
		/// </summary>
		private async void OpenWeatherMapCatchUp()
		{
			OpenWeatherMap.Updating = true;

			string url = "http://api.openweathermap.org/data/3.0/measurements?appid=" + OpenWeatherMap.PW;
			string logUrl = url.Replace(OpenWeatherMap.PW, "<key>");

			for (int i = 0; i < OWMList.Count; i++)
			{
				LogMessage("Uploading OpenWeatherMap archive #" + (i + 1));
				LogDebugMessage("OpenWeatherMap: URL = " + logUrl);
				LogDataMessage("OpenWeatherMap: Body = " + OWMList[i]);

				try
				{
					using (var data = new StringContent(OWMList[i], Encoding.UTF8, "application/json"))
					using (var response = await MyHttpClient.PostAsync(url, data))
					{
						var responseBodyAsText = await response.Content.ReadAsStringAsync();
						var status = response.StatusCode == HttpStatusCode.NoContent ? "OK" : "Error";  // Returns a 204 response for OK!
						LogDebugMessage($"OpenWeatherMap: Response code = {status} - {response.StatusCode}");
						if (response.StatusCode != HttpStatusCode.NoContent)
							LogDataMessage($"OpenWeatherMap: Response data = {responseBodyAsText}");
					}
				}
				catch (Exception ex)
				{
					LogMessage("OpenWeatherMap: Update error = " + ex.Message);
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
					using (var response = await MyHttpClient.GetAsync(URL))
					{
						var responseBodyAsText = await response.Content.ReadAsStringAsync();
						if (response.StatusCode != HttpStatusCode.OK)
						{
							LogMessage($"PWS Response: ERROR - Response code = {response.StatusCode},  Body = {responseBodyAsText}");
							HttpUploadAlarm.LastError = $"PWS: HTTP Response code = {response.StatusCode},  Body = {responseBodyAsText}";
							HttpUploadAlarm.Triggered = true;
						}
						else
						{
							LogDebugMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
							HttpUploadAlarm.Triggered = false;
						}
					}
				}
				catch (Exception ex)
				{
					LogMessage("PWS update: " + ex.Message);
					HttpUploadAlarm.LastError = "PWS: " + ex.Message;
					HttpUploadAlarm.Triggered = true;
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
				LogDebugMessage("WOW URL = " + LogURL);

				try
				{
					using (var response = await MyHttpClient.GetAsync(URL))
					{
						var responseBodyAsText = await response.Content.ReadAsStringAsync();
						if (response.StatusCode != HttpStatusCode.OK)
						{
							LogMessage($"WOW Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}");
							HttpUploadAlarm.LastError = $"WOW: HTTP response - Response code = {response.StatusCode}, body = {responseBodyAsText}";
							HttpUploadAlarm.Triggered = true;
						}
						else
						{
							LogDebugMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
							HttpUploadAlarm.Triggered = false;
						}
					}
				}
				catch (Exception ex)
				{
					LogMessage("WOW update: " + ex.Message);
					HttpUploadAlarm.LastError = "WOW: " + ex.Message;
					HttpUploadAlarm.Triggered = true;
				}
				finally
				{
					WOW.Updating = false;
				}
			}
		}

		public async Task MySqlCommandAsync(string Cmd, string CallingFunction)
		{
			var Cmds = new ConcurrentQueue<SqlCache>();
			Cmds.Enqueue(new SqlCache() { statement = Cmd });
			await MySqlCommandAsync(Cmds, CallingFunction, false);
		}

		public async Task MySqlCommandAsync(List<string> Cmds, string CallingFunction)
		{
			var tempQ = new ConcurrentQueue<SqlCache>();
			foreach (var cmd in Cmds)
			{
				tempQ.Enqueue(new SqlCache() { statement = cmd });
			}
			await MySqlCommandAsync(tempQ, CallingFunction, false);
		}

		public async Task MySqlCommandAsync(ConcurrentQueue<SqlCache> Cmds, string CallingFunction, bool UseFailedList)
		{
			await Task.Run(() =>
			{
				var queue = UseFailedList ? ref MySqlFailedList : ref Cmds;
				SqlCache cachedCmd = null;

				try
				{
					using (var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString()))
					{
						mySqlConn.Open();

						using (var transaction = Cmds.Count > 2 ? mySqlConn.BeginTransaction() : null)
						{
							do
							{
								// Do not remove the item from the stack until we know the command worked
								if (queue.TryPeek(out cachedCmd))
								{
									using (MySqlCommand cmd = new MySqlCommand(cachedCmd.statement, mySqlConn))
									{
										LogDebugMessage($"{CallingFunction}: MySQL executing - {cachedCmd.statement}");

										if (transaction != null)
										{
											cmd.Transaction = transaction;
										}

										int aff = cmd.ExecuteNonQuery();
										LogDebugMessage($"{CallingFunction}: MySQL {aff} rows were affected.");

										// Success, if using the failed list, delete from the database
										if (UseFailedList)
										{
											station.RecentDataDb.Delete<SqlCache>(cachedCmd.key);
										}

										// and pop the value from the queue
										queue.TryDequeue(out cachedCmd);
									}
								}
							} while (!queue.IsEmpty);

							if (transaction != null)
							{
								LogDebugMessage($"{CallingFunction}: Committing updates to DB");
								transaction.Commit();
								LogDebugMessage($"{CallingFunction}: Commit complete");
								transaction.Dispose();
							}
						}

						mySqlConn.Close();
						mySqlConn.Dispose();
					}

					MySqlUploadAlarm.Triggered = false;
				}
				catch (Exception ex)
				{
					LogMessage($"{CallingFunction}: Error encountered during MySQL operation = {ex.Message}");
					// if debug logging is disable, then log the failing statement anyway
					if (!DebuggingEnabled)
					{
						LogMessage($"{CallingFunction}: SQL = {cachedCmd.statement}");
					}


					MySqlUploadAlarm.LastError = ex.Message;
					MySqlUploadAlarm.Triggered = true;

					if (MySqlSettings.BufferOnfailure && !UseFailedList)
					{
						// do we save this command/commands on failure to be resubmitted?
						// if we have a syntax error, it is never going to work so do not save it for retry
						// A selection of the more common(?) errors to ignore...
						var errorCode = (int) ex.Data["Server Error Code"];
						MySqlCommandErrorHandler(CallingFunction, errorCode, queue);
					}
				}
			});
		}

		public void MySqlCommandSync(string Cmd, string CallingFunction)
		{
			var Cmds = new ConcurrentQueue<SqlCache>();
			Cmds.Enqueue(new SqlCache() { statement = Cmd });
			MySqlCommandSync(Cmds, CallingFunction, false);
		}

		public void MySqlCommandSync(ConcurrentQueue<SqlCache> Cmds, string CallingFunction, bool UseFailedList)
		{
			var queue = UseFailedList ? ref MySqlFailedList : ref Cmds;
			SqlCache cachedCmd = null;

			try
			{
				using (var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString()))
				{
					mySqlConn.Open();

					using (var transaction = Cmds.Count > 2 ? mySqlConn.BeginTransaction() : null)
					{
						do
						{
							// Do not remove the item from the stack until we know the command worked
							if (queue.TryPeek(out cachedCmd))
							{

								using (MySqlCommand cmd = new MySqlCommand(cachedCmd.statement, mySqlConn))
								{
									LogDebugMessage($"{CallingFunction}: MySQL executing - {cachedCmd.statement}");

									if (transaction != null)
									{
										cmd.Transaction = transaction;
									}

									int aff = cmd.ExecuteNonQuery();
									LogDebugMessage($"{CallingFunction}: MySQL {aff} rows were affected.");

									// Success, if using the failed list, delete from the databasec
									if (UseFailedList)
									{
										station.RecentDataDb.Delete<SqlCache>(cachedCmd.key);
									}
									// and pop the value from the queue
									queue.TryDequeue(out cachedCmd);
								}

								MySqlUploadAlarm.Triggered = false;
							}
						} while (!queue.IsEmpty);

						if (transaction != null)
						{
							LogDebugMessage($"{CallingFunction}: Committing updates to DB");
							transaction.Commit();
							LogDebugMessage($"{CallingFunction}: Commit complete");
							transaction.Dispose();
						}
					}

					mySqlConn.Close();
					mySqlConn.Dispose();
				}
			}
			catch (Exception ex)
			{
				LogMessage($"{CallingFunction}: Error encountered during MySQL operation = {ex.Message}");
				// if debug logging is disable, then log the failing statement anyway
				if (!DebuggingEnabled)
				{
					LogMessage($"{CallingFunction}: SQL = {cachedCmd}");
				}

				MySqlUploadAlarm.LastError = ex.Message;
				MySqlUploadAlarm.Triggered = true;

				// do we save this command/commands on failure to be resubmitted?
				// if we have a syntax error, it is never going to work so do not save it for retry
				if (MySqlSettings.BufferOnfailure && !UseFailedList)
				{
					// do we save this command/commands on failure to be resubmitted?
					// if we have a syntax error, it is never going to work so do not save it for retry
					// A selection of the more common(?) errors to ignore...
					var errorCode = (int) ex.Data["Server Error Code"];
					MySqlCommandErrorHandler(CallingFunction, errorCode, queue);
				}

				throw;
			}
		}

		internal void MySqlCommandErrorHandler(string CallingFunction, int ErrorCode, ConcurrentQueue<SqlCache> Cmds)
		{
			var ignore = ErrorCode == (int) MySqlErrorCode.ParseError ||
						 ErrorCode == (int) MySqlErrorCode.EmptyQuery ||
						 ErrorCode == (int) MySqlErrorCode.TooBigSelect ||
						 ErrorCode == (int) MySqlErrorCode.InvalidUseOfNull ||
						 ErrorCode == (int) MySqlErrorCode.MixOfGroupFunctionAndFields ||
						 ErrorCode == (int) MySqlErrorCode.SyntaxError ||
						 ErrorCode == (int) MySqlErrorCode.TooLongString ||
						 ErrorCode == (int) MySqlErrorCode.WrongColumnName ||
						 ErrorCode == (int) MySqlErrorCode.DuplicateUnique ||
						 ErrorCode == (int) MySqlErrorCode.PrimaryCannotHaveNull ||
						 ErrorCode == (int) MySqlErrorCode.DivisionByZero ||
						 ErrorCode == (int) MySqlErrorCode.DuplicateKeyEntry;

			if (ignore)
			{
				LogDebugMessage($"{CallingFunction}: Not buffering this command due to a problem with the query");
			}
			else
			{
				while (!Cmds.IsEmpty)
				{
					try
					{
						Cmds.TryDequeue(out var cmd);
						if (!cmd.statement.StartsWith("DELETE IGNORE FROM"))
						{
							LogDebugMessage($"{CallingFunction}: Buffering command to failed list");

							_ = station.RecentDataDb.Insert(cmd);
							MySqlFailedList.Enqueue(cmd);
						}
					}
					catch (Exception ex)
					{
						LogMessage($"{CallingFunction}: Error buffering command - " + ex.Message);
					}
				}
			}
		}

		public bool MySqlCheckConnection()
		{
			try
			{
				using (var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString()))
				{
					mySqlConn.Open();
					// get the database name to check 100% we have a connection
					var db = mySqlConn.Database;
					LogMessage("MySqlCheckConnection: Connected to server ok, default database = " + db);
					mySqlConn.Close();
					return true;
				}
			}
			catch
			{
				return false;
			}
		}

		public async void GetLatestVersion()
		{
			// Let this default to highest available version in the OS
			//ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
			try
			{
				using (var retVal = await MyHttpClient.GetAsync("https://github.com/cumulusmx/CumulusMX/releases/latest"))
				{
					var latestUri = retVal.RequestMessage.RequestUri.AbsolutePath;
					LatestBuild = new string(latestUri.Split('/').Last().Where(char.IsDigit).ToArray());
					if (int.Parse(Build) < int.Parse(LatestBuild))
					{
						var msg = $"You are not running the latest version of Cumulus MX, build {LatestBuild} is available.";
						LogConsoleMessage(msg, ConsoleColor.Cyan);
						LogMessage(msg);
						UpgradeAlarm.LastError = $"Build {LatestBuild} is available";
						UpgradeAlarm.Triggered = true;
					}
					else if (int.Parse(Build) == int.Parse(LatestBuild))
					{
						LogMessage("This Cumulus MX instance is running the latest version");
						UpgradeAlarm.Triggered = false;
					}
					else if (int.Parse(Build) > int.Parse(LatestBuild))
					{
						LogMessage($"This Cumulus MX instance appears to be running a beta/test version. This build = {Build}, latest released build = {LatestBuild}");
					}
					else
					{
						LogMessage($"Could not determine if you are running the latest Cumulus MX build or not. This build = {Build}, latest build = {LatestBuild}");
					}
				}
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Failed to get the latest build version from GitHub");
			}
		}

		public async void CustomHttpSecondsUpdate()
		{
			if (!updatingCustomHttpSeconds)
			{
				updatingCustomHttpSeconds = true;

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(CustomHttpSecondsStrings[i]))
						{
							var parser = new TokenParser(TokenParserOnToken);
							parser.InputText = CustomHttpSecondsStrings[i];
							var processedString = parser.ToStringFromString();
							LogDebugMessage($"CustomHttpSeconds[{i}]: Querying - {processedString}");
							using (var response = await MyHttpClient.GetAsync(processedString))
							{
								response.EnsureSuccessStatusCode();
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpSeconds[{i}]: Response - {response.StatusCode}");
								LogDataMessage($"CustomHttpSeconds[{i}]: Response Text - {responseBodyAsText}");
							}
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "CustomHttpSeconds: Error occurred");
					}
				}

				updatingCustomHttpSeconds = false;
			}
			else
			{
				LogDebugMessage("CustomHttpSeconds: Query already in progress, skipping this attempt");
			}
		}

		public async void CustomHttpMinutesUpdate()
		{
			if (!updatingCustomHttpMinutes)
			{
				updatingCustomHttpMinutes = true;

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(CustomHttpMinutesStrings[i]))
						{
							var parser = new TokenParser(TokenParserOnToken);
							parser.InputText = CustomHttpMinutesStrings[i];
							var processedString = parser.ToStringFromString();
							LogDebugMessage($"CustomHttpMinutes[{i}]: Querying - {processedString}");
							using (var response = await MyHttpClient.GetAsync(processedString))
							{
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpMinutes[{i}]: Response code - {response.StatusCode}");
								LogDataMessage($"CustomHttpMinutes[{i}]: Response text - {responseBodyAsText}");
							}
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "CustomHttpMinutes: Error ocurred");
					}
				}

				updatingCustomHttpMinutes = false;
			}
		}

		public async void CustomHttpRolloverUpdate()
		{
			if (!updatingCustomHttpRollover)
			{
				updatingCustomHttpRollover = true;

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (!string.IsNullOrEmpty(CustomHttpRolloverStrings[i]))
						{
							var parser = new TokenParser(TokenParserOnToken);
							parser.InputText = CustomHttpRolloverStrings[i];
							var processedString = parser.ToStringFromString();
							LogDebugMessage($"CustomHttpRollover[{i}]: Querying - {processedString}");
							using (var response = await MyHttpClient.GetAsync(processedString))
							{
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpRollover[{i}]: Response code - {response.StatusCode}");
								LogDataMessage($"CustomHttpRollover[{i}]: Response text - {responseBodyAsText}");
							}
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "CustomHttpRollover: Error occurred");
					}
				}

				updatingCustomHttpRollover = false;
			}
		}

		public void DegToDMS(decimal degrees, out int d, out int m, out int s)
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

		private string GetUploadFilename(string input, DateTime dat)
		{
			if (input == "<currentlogfile>")
			{
				return GetLogFileName(dat);
			}
			else if (input == "<currentextralogfile>")
			{
				return GetExtraLogFileName(dat);
			}
			else if (input == "<airlinklogfile>")
			{
				return GetAirLinkLogFileName(dat);
			}
			else if (input == "<noaayearfile>")
			{
				NOAAReports noaa = new NOAAReports(this, station);
				return noaa.GetLastNoaaYearReportFilename(dat, true);
			}
			else if (input == "<noaamonthfile>")
			{
				NOAAReports noaa = new NOAAReports(this, station);
				return noaa.GetLastNoaaMonthReportFilename(dat, true);
			}
			else if (input.StartsWith("<custinterval"))
			{
				try
				{
					Match match = Regex.Match(input, @"<custinterval([0-9]{1,2})>");

					if (match.Success)
					{
						var idx = int.Parse(match.Groups[1].Value) - 1; // we use a zero relative array

						return GetCustomIntvlLogFileName(idx, dat);
					}
					else
					{
						LogMessage("GetUploadFilename: No match found for <custinterval[1-10]> in " + input);
						return input;
					}
				}
				catch (Exception ex)
				{
					LogMessage($"GetUploadFilename: Error processing <custinterval[1-10]>, value='{input}', error: {ex.Message}");
				}
			}

			return input;
		}

		private string GetRemoteFileName(string input, DateTime dat)
		{
			if (input.Contains("<currentlogfile>"))
			{
				return input.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(dat)));
			}
			else if (input.Contains("<currentextralogfile>"))
			{
				return input.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(dat)));
			}
			else if (input.Contains("<airlinklogfile>"))
			{
				return input.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(dat)));
			}
			else if (input.Contains("<noaayearfile>"))
			{
				NOAAReports noaa = new NOAAReports(this, station);
				return input.Replace("<noaayearfile>", Path.GetFileName(noaa.GetLastNoaaYearReportFilename(dat, false)));
			}
			else if (input.Contains("<noaamonthfile>"))
			{
				NOAAReports noaa = new NOAAReports(this, station);
				return input.Replace("<noaamonthfile>", Path.GetFileName(noaa.GetLastNoaaMonthReportFilename(dat, false)));
			}
			else if (input.Contains("<custinterval"))
			{
				try
				{
					Match match = Regex.Match(input, @"<custinterval([0-9]{1,2})>");

					if (match.Success)
					{
						var idx = int.Parse(match.Groups[1].Value) - 1; // we use a zero relative array

						return input.Replace(match.Groups[0].Value, Path.GetFileName(GetCustomIntvlLogFileName(idx, dat)));
					}
					else
					{
						LogMessage("GetRemoteFileName: No match found for <custinterval[1-10]> in " + input);
						return input;
					}
				}
				catch (Exception ex)
				{
					LogMessage($"GetRemoteFileName: Error processing <custinterval[1-10]>, input='{input}', error: {ex.Message}");
				}
			}

			return input;
		}

		public void LogOffsetsMultipliers()
		{
			LogMessage("Offsets:");
			LogMessage($"P={Calib.Press.Offset:F3} T={Calib.Temp.Offset:F3} H={Calib.Hum.Offset} WD={Calib.WindDir.Offset} S={Calib.Solar.Offset:F3} UV={Calib.UV.Offset:F3} IT={Calib.InTemp.Offset:F3} IH={Calib.InHum.Offset:F3}");
			LogMessage("Multipliers:");
			LogMessage($"P={Calib.Press.Mult:F3} WS={Calib.WindSpeed.Mult:F3} WG={Calib.WindGust.Mult:F3} T={Calib.Temp.Mult:F3} H={Calib.Hum.Mult:F3} R={Calib.Rain.Mult:F3} S={Calib.Solar.Mult:F3} UV={Calib.UV.Mult:F3} IT={Calib.InTemp.Mult:F3} IH={Calib.InHum.Mult:F3}");
			LogMessage("Multipliers2:");
			LogMessage($"P={Calib.Press.Mult2:F3} WS={Calib.WindSpeed.Mult2:F3} WG={Calib.WindGust.Mult2:F3} T={Calib.Temp.Mult2:F3} H={Calib.Hum.Mult2:F3} S={Calib.Solar.Mult2:F3} UV={Calib.UV.Mult2:F3} IT={Calib.InTemp.Mult2:F3} IH={Calib.InHum.Mult2:F3}");
			LogMessage("Spike removal:");
			LogMessage($"TD={Spike.TempDiff:F3} GD={Spike.GustDiff:F3} WD={Spike.WindDiff:F3} HD={Spike.HumidityDiff:F3} PD={Spike.PressDiff:F3} MR={Spike.MaxRainRate:F3} MH={Spike.MaxHourlyRain:F3} ITD={Spike.InTempDiff:F3} IHD={Spike.InHumDiff:F3}");
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
					LogMessage("Primary AQ Sensor = AirLink Indoor");
					break;
				case (int)PrimaryAqSensor.AirLinkOutdoor:
					LogMessage("Primary AQ Sensor = AirLink Outdoor");
					break;
			}
		}

		private void PingCompletedCallback(object sender, PingCompletedEventArgs e)
		{
			// If an error occurred, display the exception to the user.
			if (e.Error != null)
			{
				LogMessage("Ping failed: " + e.Error.Message + " > " + e.Error.GetBaseException().Message);
			}
			else
			{
				LogMessage("Ping reply: " + e.Reply.Status);
			}

			//pingReply = e.Reply;
		}

		private void CreateRequiredFolders()
		{
			// The required folders are: /backup, backup/daily, /data, /Reports
			var folders = new string[4] { "backup", "backup/daily", "data", "Reports" };

			LogMessage("Checking required folders");

			foreach (var folder in folders)
			{
				try
				{
					if (!Directory.Exists(folder))
					{
						LogMessage("Creating required folder: /" + folder);
						Directory.CreateDirectory(folder);
					}
				}
				catch (UnauthorizedAccessException)
				{
					var msg = "Error, no permission to read/create folder: " + folder;
					LogConsoleMessage(msg, ConsoleColor.Red);
					LogErrorMessage(msg);
				}
				catch (Exception ex)
				{
					var msg = $"Error while attempting to read/create folder: {folder}, error message: {ex.Message}";
					LogConsoleMessage(msg, ConsoleColor.Red);
					LogErrorMessage(msg);
				}
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
		public const int HttpWund = 13;
		public const int HttpEcowitt = 14;
		public const int HttpAmbient = 15;
		public const int Tempest = 16;
		public const int Simulator = 17;
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
		public string StartupTask { get; set; }
		public string StartupTaskParams { get; set; }
		public bool StartupTaskWait { get; set; }
		public string ShutdownTask { get; set; }
		public string ShutdownTaskParams { get; set; }
		public bool DebugLogging { get; set; }
		public bool DataLogging { get; set; }
		public bool WarnMultiple { get; set; }
		public bool ListWebTags { get; set; }
		public CultureConfig Culture { get; set; }
		public bool DataStoppedExit { get; set; }
		public int DataStoppedMins { get; set; }
		public string TimeFormat { get; set; }
		public string TimeFormatLong { get; set; }
	}

	public class CultureConfig
	{
		public bool RemoveSpaceFromDateSeparator { get; set; }
	}

	public class StationUnits
	{
		/// <value> 0=m/s, 1=mph, 2=km/h, 3=knots</value>
		public int Wind { get; set; }
		/// <value> 0=mb, 1=hPa, 2=inHg </value>
		public int Press { get; set; }
		/// <value> 0=mm, 1=in </value>
		public int Rain { get; set; }
		/// <value> 0=C, 1=F </value>
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
		public string LeafWetnessUnitText { get; set; }

		public StationUnits()
		{
			AirQualityUnitText = "µg/m³";
			SoilMoistureUnitText = "cb";
			CO2UnitText = "ppm";
			LeafWetnessUnitText = "";  // Davis is unitless, Ecowitt uses %
		}
	}

	public class StationOptions
	{
		public bool UseZeroBearing { get; set; }
		public bool CalcuateAverageWindSpeed { get; set; }
		public bool UseSpeedForAvgCalc { get; set; }
		public bool UseSpeedForLatest { get; set; }
		public bool Humidity98Fix { get; set; }
		public bool CalculatedDP { get; set; }
		public bool CalculatedWC { get; set; }
		public bool CalculatedET { get; set; }
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
		public int UseRainForIsRaining { get; set; }
		public int LeafWetnessIsRainingIdx { get; set; }
		public double LeafWetnessIsRainingThrsh { get; set; }
	}

	public class FtpOptionsClass
	{
		public bool Enabled { get; set; }
		public string Hostname { get; set; }
		public int Port { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string Directory { get; set; }
		public bool IntervalEnabled { get; set; }
		public bool RealtimeEnabled { get; set; }
		public Cumulus.FtpProtocols FtpMode { get; set; }
		public bool AutoDetect { get; set; }
		public string SshAuthen { get; set; }
		public string SshPskFile { get; set; }
		public bool Logging { get; set; }
		public bool Utf8Encode { get; set; }
		public bool ActiveMode { get; set; }
		public bool DisableEPSV { get; set; }
		public bool DisableExplicit { get; set; }
		public bool IgnoreCertErrors { get; set; }

		public bool LocalCopyEnabled { get; set; }
		public string LocalCopyFolder { get; set; }
		public string PhpUrl { get; set; }
		public string PhpSecret { get; set; }
		public bool PhpIgnoreCertErrors { get; set; }
		public string PhpCompression { get; set; } = "none";
		public int MaxConcurrentUploads { get; set; }
	}

	public class FileGenerationOptions
	{
		public string TemplateFileName { get; set; }
		public string LocalFileName { get; set; }
		public string LocalPath { get; set; }
		public string RemoteFileName { get; set; }
		public bool Create { get; set; }
		public bool FTP { get; set; }
		public bool Copy { get; set; }
		public bool FtpRequired { get; set; } = true;
		public bool CopyRequired { get; set; } = true;
		public bool CreateRequired { get; set; } = true;
		public DateTime LastDataTime { get; set; } = DateTime.MinValue;
		public bool Incremental { get; set; }
	}

	public class MoonImageOptionsClass
	{
		public bool Enabled { get; set; }
		public int Size { get; set; }
		public bool Ftp { get; set; }
		public string FtpDest { get; set; }
		public bool Copy { get; set; }
		public string CopyDest { get; set; }
		public bool ReadyToFtp { get; set; }
		public bool ReadyToCopy { get; set; }
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

	public class WeatherFlowOptions
	{
		public int WFDeviceId { get; set; }
		public int WFTcpPort { get; set; }
		public string WFToken { get; set; }
		public int WFDaysHist { get; set; }

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
		/// <value>Delay to wait for a reply to a command (ms)</value>
		public int WaitTime { get; set; }
		/// <value>Delay between sending read live data commands (ms)</value>
		public int ReadDelay { get; set; }
		/// <value>Keep the logger pointer pointing at last data read</value>
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

	public class SolarOptions
	{
		public int SunThreshold { get; set; }
		public int SolarMinimum { get; set; }
		public double LuxToWM2 { get; set; }
		public bool UseBlakeLarsen { get; set; }
		public int SolarCalc { get; set; }
		public double RStransfactorJun { get; set; }
		public double RStransfactorDec { get; set; }
		public double BrasTurbidityJun { get; set; }
		public double BrasTurbidityDec { get; set; }
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
		public bool SendSoilTemp;
		public int SoilTempSensor;
		public bool SendSoilMoisture;
		public int SoilMoistureSensor;
		public bool CatchUp;
		public bool CatchingUp;
		public bool Updating;
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
		public int SendExtraTemp1;
		public int SendExtraTemp2;
		public int SendExtraTemp3;
		public int SendExtraTemp4;
		public int ErrorFlagCount;
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
		public bool SendLeafWetness;
	}

	public class WebUploadWCloud : WebUploadService
	{
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

	public class MqttTemplate
	{
		public List<MqttTemplateMember> topics { get; set; }
	}

	public class MqttTemplateMember
	{
		public string topic { get; set; }
		public string data { get; set; }
		public bool retain { get; set; }
	}

	public class MySqlGeneralSettings
	{
		public bool UpdateOnEdit { get; set; }
		public bool BufferOnfailure { get; set; }
		public string RealtimeRetention { get; set; }
		public bool RealtimeLimit1Minute { get; set; }
		public MySqlTableSettings Realtime { get; set; }
		public MySqlTableSettings Monthly { get; set; }
		public MySqlTableSettings Dayfile { get; set; }
		public MySqlTableSettings CustomSecs { get; set; }
		public MySqlTableSettings CustomMins { get; set; }
		public MySqlTableSettings CustomRollover { get; set; }
		public MySqlTableTimedSettings CustomTimed { get; set; }

		public MySqlGeneralSettings()
		{
			Realtime = new MySqlTableSettings();
			Monthly = new MySqlTableSettings();
			Dayfile = new MySqlTableSettings();
			CustomSecs = new MySqlTableSettings();
			CustomMins = new MySqlTableSettings();
			CustomRollover = new MySqlTableSettings();
			CustomTimed = new MySqlTableTimedSettings();

			CustomSecs.Commands = new string[10];
			CustomMins.Commands = new string[10];
			CustomRollover.Commands = new string[10];
			CustomTimed.Commands = new string[10];
			CustomTimed.Intervals = new int[10];
			CustomTimed.StartTimes = new TimeSpan[10];
			CustomTimed.NextUpdate = new DateTime[10];
		}
	}

	public class CustomLogSettings
	{
		public bool Enabled { get; set; }
		public string FileName { get; set; }
		public string ContentString { get; set; }
		public int Interval { get; set; }
		public int IntervalIdx { get; set; }
	}

	public class MySqlTableSettings
	{
		public bool Enabled { get; set; }
		public string TableName { get; set; }
		public string[] Commands { get; set; }
		public int Interval { get; set; }
	}

	public class MySqlTableTimedSettings : MySqlTableSettings
	{
		public TimeSpan[] StartTimes { get; set; }
		public int[] Intervals { get; set; }
		public DateTime[] NextUpdate { get; set; }


		public void SetStartTime(int idx, string val)
		{
			StartTimes[idx] = TimeSpan.ParseExact(val, "hh\\:mm", CultureInfo.InvariantCulture);
		}

		public string GetStartTimeString(int idx)
		{
			return StartTimes[idx].ToString("hh\\:mm", CultureInfo.InvariantCulture);
		}

		public void SetInitialNextInterval(int idx, DateTime now)
		{
			NextUpdate[idx] = now.Date + StartTimes[idx];
		}

		public void SetNextInterval(int idx, DateTime now)
		{
			// We always revert to the start time so we remain consistent across DST changes
			NextUpdate[idx] = now.Date + StartTimes[idx];

			if (Intervals[idx] == 0)
			{
				// timed but no repeat interval, add a day
				NextUpdate[idx] = NextUpdate[idx].AddDays(1);
			}
			else
			{
				// Timed and we have now set the start, add on intervals until we reach the future
				while (NextUpdate[idx] <= now)
				{
					NextUpdate[idx] = NextUpdate[idx].AddMinutes(Intervals[idx]);
				}

				// have we rolled over a day and the next interval would be prior to the start time?
				// if so, bump up the next interval to the daily start time
				if (NextUpdate[idx].TimeOfDay < StartTimes[idx])
					NextUpdate[idx] = NextUpdate[idx].Date + StartTimes[idx];
			}
		}

	}

	public class NOAAconfig
	{
		public string Name { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string MonthFile { get; set; }
		public string YearFile { get; set; }
		public bool Use12hour { get; set; }
		public bool UseUtf8 { get; set; }
		public bool UseMinMaxAvg { get; set; }
		public bool UseNoaaHeatCoolDays { get; set; }
		public bool UseDotDecimal { get; set; }
		public bool Create { get; set; }
		public bool AutoFtp { get; set; }
		public bool AutoCopy { get; set; }
		public bool NeedFtp { get; set; }
		public bool NeedCopy { get; set; }
		public string FtpFolder { get; set; }
		public string CopyFolder { get; set; }
		public double[] TempNorms { get; set; }
		public double[] RainNorms { get; set; }
		public double HeatThreshold { get; set; }
		public double CoolThreshold { get; set; }
		public double MaxTempComp1 { get; set; }
		public double MaxTempComp2 { get; set; }
		public double MinTempComp1 { get; set; }
		public double MinTempComp2 { get; set; }
		public double RainComp1 { get; set; }
		public double RainComp2 { get; set; }
		public double RainComp3 { get; set; }
		public string LatestMonthReport { get; set; }
		public string LatestYearReport { get; set; }

		public NOAAconfig()
		{
			TempNorms = new double[13];
			RainNorms = new double[13];
		}
	}
}
