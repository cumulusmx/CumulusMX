using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using EmbedIO;
using EmbedIO.Files;
using EmbedIO.Utilities;
using EmbedIO.WebApi;

using FluentFTP;
using FluentFTP.Helpers;
using FluentFTP.Logging;

using Microsoft.Extensions.Logging;

using MySqlConnector;

using NReco.Logging.File;

using Renci.SshNet;

using ServiceStack;
using ServiceStack.Text;

using SQLite;

using Swan;

using static CumulusMX.EmailSender;

using File = System.IO.File;
using Timer = System.Timers.Timer;

namespace CumulusMX
{
	public partial class Cumulus
	{
		/////////////////////////////////
		/// Now derived from app properties
		public string Version { get; private set; }
		public string Build { get; private set; }
		private static readonly SemaphoreSlim semaphoreSlim = new(1);

		/////////////////////////////////

		public static SemaphoreSlim SyncInit { get => semaphoreSlim; }

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

		public enum MxLogLevel
		{
			Info = 0,
			Warning = 1,
			Error = 2,
			Critical = 3
		}

		public MxLogLevel ErrorListLoggingLevel { get; set; } = MxLogLevel.Warning;

		private readonly string[] sshAuthenticationVals = ["password", "psk", "password_psk"];

		public class CExtraFiles
		{
			public bool enable { get; set; }
			public string local { get; set; }
			public string remote { get; set; }
			public bool process { get; set; }
			public bool binary { get; set; }
			public bool realtime { get; set; }
			public bool endofday { get; set; }
			public bool FTP { get; set; }
			public bool UTF8 { get; set; }
			public bool incrementalLogfile { get; set; }
			public int logFileLastLineNumber { get; set; }
			public string logFileLastFileName { get; set; }
		}

		private FileStream _lockFile;
		private string _lockFilename;

		public const double DefaultHiVal = -9999;
		public const double DefaultLoVal = 9999;

		public const int DayfileFields = 55;

		public const int LogFileRetries = 3;

		private WeatherStation station;

		internal DavisAirLink airLinkIn;
		public int airLinkInLsid { get; set; }
		public string AirLinkInHostName { get; set; }
		internal DavisAirLink airLinkOut { get; set; }
		public int airLinkOutLsid { get; set; }
		public string AirLinkOutHostName { get; set; }

		internal HttpStationEcowitt ecowittExtra;
		internal HttpStationAmbient ambientExtra;
		internal EcowittCloudStation ecowittCloudExtra;
		internal JsonStation stationJsonExtra;

		internal DateTime LastUpdateTime; // use UTC to avoid DST issues

		internal PerformanceCounter UpTime;

		internal WebTags WebTags;

		internal Lang Trans = new();

		internal bool NormalRunning = false;

		private LoggerFactory loggerFactory = new();
		private ILogger FtpLoggerRT;
		private ILogger FtpLoggerIN;
		private ILogger FtpLoggerMX;

		internal int WebUpdating;
		internal bool SqlCatchingUp;

		internal double WindRoseAngle;

		internal int NumWindRosePoints;

		internal static int[] logints { get; } = [1, 5, 10, 15, 20, 30];

		public int GraphDays { get; set; } = 31;

		internal static int[] FactorsOf60 { get; } = [1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60];

		internal TimeSpan AvgSpeedTime;

		internal TimeSpan PeakGustTime;
		internal TimeSpan AvgBearingTime;

		internal bool UTF8encode;

		internal int TempDPlaces = 1;
		internal string TempFormat;

		internal int WindDPlaces = 1;
		internal int WindAvgDPlaces = 1;
		public string WindFormat { get; set; }
		public string WindAvgFormat { get; set; }

		internal int HumDPlaces = 0;
		internal string HumFormat;

		internal int AirQualityDPlaces = 1;
		internal string AirQualityFormat;

		internal int WindRunDPlaces = 1;
		internal string WindRunFormat;

		internal int RainDPlaces = 1;
		internal string RainFormat;

		internal int PressDPlaces = 1;
		internal string PressFormat;

		internal int SunshineDPlaces = 1;
		internal string SunFormat;

		internal int UVDPlaces = 1;
		internal string UVFormat;

		internal int SnowDPlaces = 0;
		internal string SnowFormat = "F1";

		internal string ETFormat;

		internal int LeafWetDPlaces = 0;
		internal string LeafWetFormat = "F0";

		internal string ComportName;
		internal string DefaultComportName;

		internal string Platform;

		internal string dbfile;

		internal string diaryfile;
		internal SQLiteConnection DiaryDB;

		internal string Datapath;

		internal string ListSeparator;
		internal char DirectorySeparator;

		internal int RolloverHour;
		internal bool Use10amInSummer;

		internal decimal Latitude;
		internal decimal Longitude;
		internal double Altitude;

		internal int wsPort;
		private bool DebuggingEnabled;

		internal SerialPort cmprtRG11;
		internal SerialPort cmprt2RG11;

		private const int DefaultWebUpdateInterval = 15;

		internal int RecordSetTimeoutHrs = 24;

		internal string AlltimeIniFile;
		internal string Alltimelogfile;
		internal string MonthlyAlltimeIniFile;
		internal string MonthlyAlltimeLogFile;
		private string logFilePath;
		internal string DayFileName;
		internal string YesterdayFile;
		internal string TodayIniFile;
		internal string MonthIniFile;
		internal string YearIniFile;
		private string backupPath;
		internal string WebTagFile;

		internal bool SynchronisedWebUpdate;

		// Use thread safe queues for the MySQL command lists
		private readonly ConcurrentQueue<SqlCache> MySqlList = new();
		public readonly ConcurrentQueue<SqlCache> MySqlFailedList = new();

		// Calibration settings
		/// <summary>
		/// User value calibration settings
		/// </summary>
		internal Calibrations Calib = new();

		/// <summary>
		/// User extreme limit settings
		/// </summary>
		internal Limits Limit = new();

		/// <summary>
		/// User spike limit settings
		/// </summary>
		internal Spikes Spike = new();

		internal ProgramOptionsClass ProgramOptions = new();

		internal StationOptions StationOptions = new();
		internal FtpOptionsClass FtpOptions = new();

		internal StationUnits Units = new();

		internal DavisOptions DavisOptions = new();
		internal FineOffsetOptions FineOffsetOptions = new();
		internal ImetOptions ImetOptions = new();
		internal EasyWeatherOptions EwOptions = new();
		internal WeatherFlowOptions WeatherFlowOptions = new();
		internal JsonStationOptions JsonStationOptions = new();
		internal JsonStationOptions JsonExtraStationOptions = new();


		internal GraphOptions GraphOptions = new();

		internal SelectaChartOptions SelectaChartOptions = new();
		internal SelectaChartOptions SelectaPeriodOptions = new();


		internal DisplayOptions DisplayOptions = new();

		internal EmailSender emailer;
		internal EmailSender.SmtpOptions SmtpOptions = new();

		internal SolarOptions SolarOptions = new();

		internal string AlarmFromEmail;
		internal string[] AlarmDestEmail;
		internal bool AlarmEmailHtml;
		internal bool AlarmEmailUseBcc;

		internal bool RealtimeIntervalEnabled; // The timer is to be started
		private int realtimeFTPRetries; // Count of failed realtime FTP attempts

		// Wunderground object
		internal ThirdParty.WebUploadWund Wund;

		// Windy.com object
		internal ThirdParty.WebUploadWindy Windy;

		// Wind Guru object
		internal ThirdParty.WebUploadWindGuru WindGuru;

		// PWS Weather object
		internal ThirdParty.WebUploadServiceBase PWS;

		// WOW object
		internal ThirdParty.WebUploadWow WOW;

		// APRS object
		internal ThirdParty.WebUploadAprs APRS;

		// Awekas object
		internal ThirdParty.WebUploadAwekas AWEKAS;

		// WeatherCloud object
		internal ThirdParty.WebUploadWCloud WCloud;

		// BlueSky object
		internal ThirdParty.WebUploadBlueSky Bluesky;

		// OpenWeatherMap object
		internal ThirdParty.WebUploadOwm OpenWeatherMap;
		internal string WxnowComment = string.Empty;

		// MQTT settings
		public struct MqttConfig
		{
			public string Server { get; set; }
			public int Port { get; set; }
			public int IpVersion { get; set; }
			public bool UseTLS { get; set; }
			public string Username { get; set; }
			public string Password { get; set; }
			public bool EnableDataUpdate { get; set; }
			public string UpdateTemplate { get; set; }
			public bool EnableInterval { get; set; }
			public string IntervalTemplate { get; set; }
		}

		internal MqttConfig MQTT;

		// NOAA report settings
		internal NoaaConfig NOAAconf = new();

		// Growing Degree Days
		internal double GrowingBase1;
		internal double GrowingBase2;
		internal int GrowingYearStarts;
		internal bool GrowingCap30C;

		internal int TempSumYearStarts;
		internal double TempSumBase1;
		internal double TempSumBase2;

		internal bool EODfilesNeedFTP;

		internal bool IsOSX = false;
		internal double CPUtemp = -999;

		// Alarms
		internal Alarm DataStoppedAlarm;
		internal Alarm BatteryLowAlarm;
		internal Alarm SensorAlarm;
		internal Alarm SpikeAlarm;
		internal Alarm HighWindAlarm;
		internal Alarm HighGustAlarm;
		internal Alarm HighRainRateAlarm;
		internal Alarm HighRainTodayAlarm;
		internal AlarmChange PressChangeAlarm;
		internal Alarm HighPressAlarm;
		internal Alarm LowPressAlarm;
		internal AlarmChange TempChangeAlarm;
		internal Alarm HighTempAlarm;
		internal Alarm LowTempAlarm;
		internal Alarm UpgradeAlarm;
		internal Alarm FirmwareAlarm;
		internal Alarm ThirdPartyAlarm;
		internal Alarm MySqlUploadAlarm;
		internal Alarm IsRainingAlarm;
		internal Alarm NewRecordAlarm;
		internal Alarm FtpAlarm;

		internal List<AlarmUser> UserAlarms = [];

		private const double DEFAULTFCLOWPRESS = 950.0;
		private const double DEFAULTFCHIGHPRESS = 1050.0;

		private const string ForumDefault = "https://cumulus.hosiene.co.uk/";

		private const string WebcamDefault = "";

		private const string DefaultSoundFile = "alarm.mp3";
		private const string DefaultSoundFileOld = "alert.wav";

		internal int RecentDataDays = 7;

		internal int RealtimeInterval;

		internal MxWebSocket WebSock;

		internal SocketsHttpHandler MyHttpSocketsHttpHandler;
		internal HttpClient MyHttpClient;

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
		internal SocketsHttpHandler phpUploadSocketHandler;
		internal HttpClient phpUploadHttpClient;

		internal Thread ftpThread;

		internal string xapHeartbeat;
		internal string xapsource;

		internal string LatestBuild = "n/a";

		internal MySqlConnectionStringBuilder MySqlConnSettings = [];

		internal MySqlGeneralSettings MySqlSettings = new();

		internal DateTime MySqlLastRealtimeTime;
		internal DateTime MySqlILastntervalTime;

		internal MySqlTable RealtimeTable;
		internal MySqlTable MonthlyTable;
		internal MySqlTable DayfileTable;

		private bool customMySqlSecondsUpdateInProgress;
		private bool customMySqlMinutesUpdateInProgress;
		private bool customMySqlRolloverUpdateInProgress;
		private bool customMySqlTimedUpdateInProgress;

		private HttpFiles httpFiles;

		internal AirLinkData airLinkDataIn;
		internal AirLinkData airLinkDataOut;

		internal CustomLogSettings[] CustomIntvlLogSettings = new CustomLogSettings[10];
		internal CustomLogSettings[] CustomDailyLogSettings = new CustomLogSettings[10];

		internal string[] StationDesc =
		[
			"Davis Vantage Pro",            // 0
			"Davis Vantage Pro2",           // 1
			"Oregon Scientific WMR-928",    // 2
			"Oregon Scientific WM-918",     // 3
			"EasyWeather",                  // 4
			"Fine Offset",                  // 5
			"LaCrosse WS2300",              // 6
			"Fine Offset with Solar",       // 7
			"Oregon Scientific WMR100",     // 8
			"Oregon Scientific WMR200",     // 9
			"Instromet",                    // 10
			"Davis WLL",                    // 11
			"GW1000",                       // 12
			"HTTP WUnderground",            // 13
			"HTTP Ecowitt",                 // 14
			"HTTP Ambient",                 // 15
			"WeatherFlow Tempest",          // 16
			"Simulator",                    // 17
			"Ecowitt Cloud",                // 18
			"Davis Cloud (WLL/WLC)",        // 19
			"Davis Cloud (VP2)",            // 20
			"JSON Data",                    // 21
			"Ecowitt HTTP API"              // 22
		];

		internal string[] APRSstationtype = ["DsVP", "DsVP", "WMR928", "WM918", "EW", "FO", "WS2300", "FOs", "WMR100", "WMR200", "IMET", "DsVP", "Ecowitt", "Unknown", "Ecowitt", "Ambient", "Tempest", "Simulated", "Ecowitt", "DsVP", "DsVP", "Json", "Ecowitt"];

		internal string[] DayfileFieldNames = ["Date", "HighWindGust", "HighGustBearing", "HighGustTime", "MinTemperature", "MinTempTime", "MaxTemperature", "MaxTempTime", "MinPressure", "MinPressureTime", "MaxPressure", "MaxPressureTime", "MaxRainfallRate", "MaxRainRateTime", "TotalRainfallToday", "AvgTemperatureToday", "TotalWindRun", "HighAverageWindSpeed", "HighAvgWindSpeedTime", "LowHumidity", "LowHumidityTime", "HighHumidity", "HighHumidityTime", "TotalEvapotranspiration", "TotalHoursOfSunshine", "HighHeatIndex", "HighHeatIndexTime", "HighApparentTemperature", "HighAppTempTime", "LowApparentTemperature", "LowAppTempTime", "High1hRain", "High1hRainTime", "LowWindChill", "LowWindChillTime", "HighDewPoint", "HighDewPointTime", "LowDewPoint", "LowDewPointTime", "DominantWindBearing", "HeatingDegreeDays", "CoolingDegreeDays", "HighSolarRad", "HighSolarRadTime", "HighUv-I", "HighUv-ITime", "HighFeelsLike", "HighFeelsLikeTime", "LowFeelsLike", "LowFeelsLikeTime", "HighHumidex", "HighHumidexTime", "ChillHours", "High24hRain", "High24hRainTime"];
		internal string[] LogFileFieldNames = ["Date", "Time", "Temperature", "Humidity", "DewPoint", "WindSpeed", "RecentHighGust", "AverageWindBearing", "RainfallRate", "RainfallSoFar", "SeaLevelPressure", "RainfallCounter", "InsideTemperature", "InsideHumidity", "CurrentGust", "WindChill", "HeatIndex", "UvIndex", "SolarRadiation", "Evapotranspiration", "AnnualEvapotranspiration", "ApparentTemperature", "MaxSolarRadiation", "HoursOfSunshine", "WindBearing", "Rg-11Rain", "RainSinceMidnight", "FeelsLike", "Humidex"];
		internal string[] ExtraFileFieldNames = ["Date", "Time",
			"Temp1", "Temp2", "Temp3", "Temp4", "Temp5", "Temp6", "Temp7", "Temp8", "Temp9", "Temp10", "Hum1", "Hum2", "Hum3", "Hum4", "Hum5", "Hum6", "Hum7", "Hum8", "Hum9", "Hum10",
			"Dewpoint1", "Dewpoint2", "Dewpoint3", "Dewpoint4", "Dewpoint5", "Dewpoint6", "Dewpoint7", "Dewpoint8", "Dewpoint9", "Dewpoint10",
			"SoilTemp1", "SoilTemp2", "SoilTemp3", "SoilTemp4", "SoilMoist1", "SoilMoist2", "SoilMoist3", "SoilMoist4", "na1", "na2", "LeafWet1", "LeafWet2",
			"SoilTemp5", "SoilTemp6", "SoilTemp7", "SoilTemp8", "SoilTemp9", "SoilTemp10", "SoilTemp11", "SoilTemp12", "SoilTemp13", "SoilTemp14", "SoilTemp15", "SoilTemp16",
			"SoilMoist5", "SoilMoist6", "SoilMoist7", "SoilMoist8", "SoilMoist9", "SoilMoist10", "SoilMoist11", "SoilMoist12", "SoilMoist13", "SoilMoist14", "SoilMoist15", "SoilMoist16",
			"AQ1Pm", "AQ2Pm", "AQ3Pm", "AQ4Pm", "AQ1PmAvg", "AQ2PmAvg", "AQ3PmAvg", "AQ4PmAvg", "UserTemp1", "UserTemp2", "UserTemp3", "UserTemp4", "UserTemp5", "UserTemp6", "UserTemp7", "UserTemp8",
			"CO2", "CO2Avg", "CO2Pm25", "CO2Pm25Avg", "CO2Pm10", "CO2Pm10Avg", "CO2Temp", "CO2Hum"
		];

		private string loggingfile;
		private static readonly Queue<string> queue = new(50);
		public static Queue<string> ErrorList
		{
			get => queue;
		}

		private SemaphoreSlim uploadCountLimitSemaphoreSlim;

		// Global cancellation token for when CMX is stopping
		public readonly CancellationTokenSource tokenSource = new();
		internal CancellationToken cancellationToken;


		public Cumulus()
		{
			cancellationToken = tokenSource.Token;

			DirectorySeparator = Path.DirectorySeparatorChar;

			// Set up the diagnostic tracing
			loggingfile = RemoveOldDiagsFiles("CMX");
			_ = RemoveOldDiagsFiles("FTP");

			Program.svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Creating main MX log file - " + loggingfile);
			Program.svcTextListener.Flush();

			TextWriterTraceListener myTextListener = new TextWriterTraceListener(loggingfile, "MXlog");

			// the default trace writes to the debug log, but on Linux it also writes to the /var/log/user.log
			if (!Debugger.IsAttached)
			{
				Trace.Listeners.Clear();
			}

			Trace.Listeners.Add(myTextListener);
			Trace.AutoFlush = true;
		}
		public void Initialise(int HTTPport, bool DebugEnabled, string startParms)
		{
			var fullVer = Assembly.GetExecutingAssembly().GetName().Version;
			Version = $"{fullVer.Major}.{fullVer.Minor}.{fullVer.Build}";
			Build = Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString();

			AppDir = Directory.GetCurrentDirectory() + DirectorySeparator;
			WebTagFile = AppDir + "WebTags.txt";

			//b3045>, use same port for WS...  WS port = HTTPS port
			wsPort = HTTPport;

			DebuggingEnabled = DebugEnabled;

			LogMessage(" ========================== Cumulus MX starting ==========================");

			LogMessage("Command line: " + Environment.CommandLine + " " + startParms);

			LogMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Working Dir: " + AppDir);

			IsOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

			if (IsOSX)
				Platform = "Mac OS X";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				Platform = "Windows";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				Platform = "Linux";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
				Platform = "FreeBSD";
			else
				Platform = "Unknown";

			LogMessage("Platform       : " + Platform);

			try
			{
				LogMessage("OS Description : " + RuntimeInformation.OSDescription);
			}
			catch
			{
				LogMessage("OS Version     : " + Environment.OSVersion + " (possibly not accurate)");
			}

			LogMessage($"Current culture: {CultureInfo.CurrentCulture.DisplayName} [{CultureInfo.CurrentCulture.Name}]");

			LogMessage($"Running as a {(IntPtr.Size == 4 ? "32" : "64")} bit process");
			LogMessage("Running under userid: " + Environment.UserName);

			LogMessage("Dotnet Version: " + RuntimeInformation.FrameworkDescription);

			// Some .NET 8 clutures use a non-"standard" minus symbol, this causes all sorts of parsing issues down the line and for external scripts
			// the simplest solution is to override this and set all cultures to use the hypen-minus
			if (CultureInfo.CurrentCulture.NumberFormat.NegativeSign != "-")
			{
				// change the none hyphen-minus to a standard hypen
				CultureInfo newCulture = (CultureInfo) Thread.CurrentThread.CurrentCulture.Clone();
				newCulture.NumberFormat.NegativeSign = "-";

				// set current thread culture
				Thread.CurrentThread.CurrentCulture = newCulture;

				// set the default culture for other threads
				CultureInfo.DefaultThreadCurrentCulture = newCulture;
			}


			// Set the default comport name depending on platform
			DefaultComportName = System.OperatingSystem.IsWindows() ? "COM1" : "/dev/ttyUSB0";

			// determine system uptime based on OS
			if (System.OperatingSystem.IsWindows())
			{
				try
				{
					// Windows enable the performance counter method
#pragma warning disable CA1416 // Validate platform compatibility
					UpTime = new PerformanceCounter("System", "System Up Time");
#pragma warning restore CA1416 // Validate platform compatibility
				}
				catch (Exception e)
				{
					LogErrorMessage("Error: Unable to access the System Up Time performance counter. System up time will not be available");
					LogDebugMessage($"Error: {e}");
				}
			}

			// Check if all the folders required by CMX exist, if not create them
			CreateRequiredFolders();

			// Remove old MD5 hash files
			CleanUpHashFiles();

			Datapath = "data" + DirectorySeparator;
			backupPath = "backup" + DirectorySeparator;
			ReportPath = "Reports" + DirectorySeparator;
			var WebPath = "web" + DirectorySeparator;

			dbfile = Datapath + "cumulusmx.db";
			diaryfile = Datapath + "diary.db";

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

			// initialise the third party uploads
			Wund = new ThirdParty.WebUploadWund(this, "WUnderground");
			Windy = new ThirdParty.WebUploadWindy(this, "Windy");
			WindGuru = new ThirdParty.WebUploadWindGuru(this, "WindGuru");
			PWS = new ThirdParty.WebUploadPws(this, "PWS");
			WOW = new ThirdParty.WebUploadWow(this, "WOW");
			APRS = new ThirdParty.WebUploadAprs(this, "APRS");
			AWEKAS = new ThirdParty.WebUploadAwekas(this, "AWEKAS");
			WCloud = new ThirdParty.WebUploadWCloud(this, "WCloud");
			OpenWeatherMap = new ThirdParty.WebUploadOwm(this, "OpenWeatherMap");
			Bluesky = new ThirdParty.WebUploadBlueSky(this, "BlueSky")
			{
				DefaultInterval = 60
			};
			if (File.Exists(WebPath + "Bluesky.txt"))
			{
				try
				{
					Bluesky.ContentTemplate = File.ReadAllText(WebPath + "Bluesky.txt", new System.Text.UTF8Encoding(false));
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "Error reading Bluesky.txt file");
				}
			}


			// Set the default upload intervals for web services
			Wund.DefaultInterval = 15;
			Windy.DefaultInterval = 15;
			WindGuru.DefaultInterval = 1;
			PWS.DefaultInterval = 15;
			APRS.DefaultInterval = 9;
			AWEKAS.DefaultInterval = 15 * 60;
			WCloud.DefaultInterval = 10;
			OpenWeatherMap.DefaultInterval = 15;

			StdWebFiles =
			[
				new()
				{
					TemplateFileName = WebPath + "websitedataT.json",
					LocalPath = WebPath,
					LocalFileName = "websitedata.json",
					RemoteFileName = "websitedata.json"
				},
				new()
				{
					LocalPath = "",
					LocalFileName = "wxnow.txt",
					RemoteFileName = "wxnow.txt"
				}
			];

			RealtimeFiles =
			[
				new()
				{
					LocalFileName = "realtime.txt",
					RemoteFileName = "realtime.txt"
				},
				new()
				{
					TemplateFileName = WebPath + "realtimegaugesT.txt",
					LocalPath = WebPath,
					LocalFileName = "realtimegauges.txt",
					RemoteFileName = "realtimegauges.txt"
				}
			];

			GraphDataFiles =
			[
				new()       // 0
				{
					LocalPath = WebPath,
					LocalFileName = "graphconfig.json",
					RemoteFileName = "graphconfig.json"
				},
				new()       // 1
				{
					LocalPath = WebPath,
					LocalFileName = "availabledata.json",
					RemoteFileName = "availabledata.json"
				},
				new()       // 2
				{
					LocalPath = WebPath,
					LocalFileName = "tempdata.json",
					RemoteFileName = "tempdata.json"
				},
				new()       // 3
				{
					LocalPath = WebPath,
					LocalFileName = "pressdata.json",
					RemoteFileName = "pressdata.json"
				},
				new()       // 4
				{
					LocalPath = WebPath,
					LocalFileName = "winddata.json",
					RemoteFileName = "winddata.json"
				},
				new()       // 5
				{
					LocalPath = WebPath,
					LocalFileName = "wdirdata.json",
					RemoteFileName = "wdirdata.json"
				},
				new()       // 6
				{
					LocalPath = WebPath,
					LocalFileName = "humdata.json",
					RemoteFileName = "humdata.json"
				},
				new()       // 7
				{
					LocalPath = WebPath,
					LocalFileName = "raindata.json",
					RemoteFileName = "raindata.json"
				},
				new()       // 8
				{
					LocalPath = WebPath,
					LocalFileName = "dailyrain.json",
					RemoteFileName = "dailyrain.json"
				},
				new()       // 9
				{
					LocalPath = WebPath,
					LocalFileName = "dailytemp.json",
					RemoteFileName = "dailytemp.json"
				},
				new()       // 10
				{
					LocalPath = WebPath,
					LocalFileName = "solardata.json",
					RemoteFileName = "solardata.json"
				},
				new()       // 11
				{
					LocalPath = WebPath,
					LocalFileName = "sunhours.json",
					RemoteFileName = "sunhours.json"
				},
				new()       // 12
				{
					LocalPath = WebPath,
					LocalFileName = "airquality.json",
					RemoteFileName = "airquality.json"
				},
				new()       // 13
				{
					LocalPath = WebPath,
					LocalFileName = "extratempdata.json",
					RemoteFileName = "extratempdata.json"
				},
				new()       // 14
				{
					LocalPath = WebPath,
					LocalFileName = "extrahumdata.json",
					RemoteFileName = "extrahumdata.json"
				},
				new()       // 15
				{
					LocalPath = WebPath,
					LocalFileName = "extradewdata.json",
					RemoteFileName = "extradewdata.json"
				},
				new()       // 16
				{
					LocalPath = WebPath,
					LocalFileName = "soiltempdata.json",
					RemoteFileName = "soiltempdata.json"
				},
				new()       // 17
				{
					LocalPath = WebPath,
					LocalFileName = "soilmoistdata.json",
					RemoteFileName = "soilmoistdata.json"
				},
				new()       // 18
				{
					LocalPath = WebPath,
					LocalFileName = "usertempdata.json",
					RemoteFileName = "usertempdata.json"
				},
				new()       // 19
				{
					LocalPath = WebPath,
					LocalFileName = "co2sensordata.json",
					RemoteFileName = "co2sensordata.json"
				},
				new()     // 20
				{
					LocalPath = WebPath,
					LocalFileName = "leafwetdata.json",
					RemoteFileName = "leafwetdata.json"
				}
			];

			GraphDataEodFiles =
			[
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailytempdata.json",
					RemoteFileName = "alldailytempdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailypressdata.json",
					RemoteFileName = "alldailypressdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailywinddata.json",
					RemoteFileName = "alldailywinddata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailyhumdata.json",
					RemoteFileName = "alldailyhumdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailyraindata.json",
					RemoteFileName = "alldailyraindata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailysolardata.json",
					RemoteFileName = "alldailysolardata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailydegdaydata.json",
					RemoteFileName = "alldailydegdaydata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alltempsumdata.json",
					RemoteFileName = "alltempsumdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "allchillhrsdata.json",
					RemoteFileName = "allchillhrsdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					LocalFileName = "alldailysnowdata.json",
					RemoteFileName = "alldailysnowdata.json"
				}
			];

			ProgramOptions.Culture = new CultureConfig();

			for (var i = 0; i < 10; i++)
			{
				CustomIntvlLogSettings[i] = new CustomLogSettings();
				CustomDailyLogSettings[i] = new CustomLogSettings();
			}

			// initialise the alarms
			DataStoppedAlarm = new Alarm("AlarmData", AlarmTypes.Trigger, this);
			BatteryLowAlarm = new Alarm("AlarmBattery", AlarmTypes.Trigger, this);
			SensorAlarm = new Alarm("AlarmSensor", AlarmTypes.Trigger, this);
			SpikeAlarm = new Alarm("AlarmSpike", AlarmTypes.Trigger, this);
			HighWindAlarm = new Alarm("AlarmWind", AlarmTypes.Above, this, Units.WindText);
			HighGustAlarm = new Alarm("AlarmGust", AlarmTypes.Above, this, Units.WindText);
			HighRainRateAlarm = new Alarm("AlarmRainRate", AlarmTypes.Above, this, Units.RainTrendText);
			HighRainTodayAlarm = new Alarm("AlarmRain", AlarmTypes.Above, this, Units.RainText);
			PressChangeAlarm = new AlarmChange("AlarmPressUp", "AlarmPressDn", this, Units.PressTrendText);
			HighPressAlarm = new Alarm("AlarmHighPress", AlarmTypes.Above, this, Units.PressText);
			LowPressAlarm = new Alarm("AlarmLowPress", AlarmTypes.Below, this, Units.PressText);
			TempChangeAlarm = new AlarmChange("AlarmTempUp", "AlarmTempDn", this, Units.TempTrendText);
			HighTempAlarm = new Alarm("AlarmHighTemp", AlarmTypes.Above, this, Units.TempText);
			LowTempAlarm = new Alarm("AlarmLowTemp", AlarmTypes.Below, this, Units.TempText);
			UpgradeAlarm = new Alarm("AlarmUpgrade", AlarmTypes.Trigger, this);
			FirmwareAlarm = new Alarm("AlarmFirmware", AlarmTypes.Trigger, this);
			ThirdPartyAlarm = new Alarm("AlarmHttp", AlarmTypes.Trigger, this);
			MySqlUploadAlarm = new Alarm("AlarmMySql", AlarmTypes.Trigger, this);
			IsRainingAlarm = new Alarm("AlarmIsRaining", AlarmTypes.Trigger, this);
			NewRecordAlarm = new Alarm("AlarmNewRec", AlarmTypes.Trigger, this);
			FtpAlarm = new Alarm("AlarmFtp", AlarmTypes.Trigger, this);

			ReadIniFile();

			// Do we prevent more than one copy of CumulusMX running?
			CheckForSingleInstance(System.OperatingSystem.IsWindows());

			if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogMessage("Maximum concurrent PHP Uploads = " + FtpOptions.MaxConcurrentUploads);
				LogMessage("PHP using GET = " + FtpOptions.PhpUseGet);
				LogMessage("PHP using Brotli = " + FtpOptions.PhpUseBrotli);
			}
			uploadCountLimitSemaphoreSlim = new SemaphoreSlim(FtpOptions.MaxConcurrentUploads);

			ListSeparator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

			DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

			LogMessage($"Directory separator=[{DirectorySeparator}] Decimal separator=[{DecimalSeparator}] List separator=[{ListSeparator}]");
			LogMessage($"Date separator=[{CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator}] Time separator=[{CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator}]");

			LogMessage("Standard time zone name:   " + TimeZoneInfo.Local.StandardName);
			if (TimeZoneInfo.Local.SupportsDaylightSavingTime)
			{
				LogMessage("Daylight saving time name: " + TimeZoneInfo.Local.DaylightName);
				LogMessage("Daylight saving time? " + TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now));
			}
			else
			{
				LogMessage("Daylight saving time is not available for this TimeZone");
			}

			LogMessage("Locale date/time format: " + DateTime.Now.ToString("G"));

			// Take a backup of all the data before we start proper
			BackupData(false, DateTime.Now);

			// Do we delay the start of Cumulus MX for a fixed period?
			if (ProgramOptions.StartupDelaySecs > 0)
			{
				// Check uptime
				double ts = -1;
				if (System.OperatingSystem.IsWindows() && UpTime != null)
				{
					UpTime.NextValue();
					ts = UpTime.NextValue();
				}
				else if (File.Exists(@"/proc/uptime"))
				{
					var text = File.ReadAllText(@"/proc/uptime");
					var strTime = text.Split(' ')[0];
					if (!double.TryParse(strTime, out ts))
						ts = -1;
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
				var msg2 = $"Received PING response from {ProgramOptions.StartupPingHost}, continuing...";
				var msg3 = $"No PING response received in {ProgramOptions.StartupPingEscapeTime} minutes, continuing anyway";
				var escapeTime = DateTime.Now.AddMinutes(ProgramOptions.StartupPingEscapeTime);
				var attempt = 1;
				var pingSuccess = false;
				// This is the timeout for "hung" attempts, we will double this at every failure so we do not create too many hung resources
				var pingTimeoutSecs = 10;
				var pingTimeoutDT = DateTime.Now.AddSeconds(pingTimeoutSecs);
				using var pingTokenSource = new CancellationTokenSource(pingTimeoutSecs * 1000);
				var pingCancelToken = pingTokenSource.Token;

				do
				{
					LogDebugMessage($"Starting PING #{attempt} task with time-out of {pingTimeoutSecs} seconds");

					var pingTask = Task.Run(() =>
					{
						var cnt = attempt;
						using var ping = new Ping();
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
							LogErrorMessage($"PING #{cnt} to {ProgramOptions.StartupPingHost} failed with error: {e.InnerException.Message}");
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
						LogErrorMessage($"Nothing returned from PING #{attempt}, attempting the cancel the task");
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
								LogErrorMessage($"PING #{attempt}: Error with DNS refresh - {ex.Message}");
							}
						}
					}
				} while (!pingSuccess && DateTime.Now < escapeTime);

				if (DateTime.Now >= escapeTime)
				{
					LogConsoleMessage(msg3, ConsoleColor.Yellow);
					LogWarningMessage(msg3);
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
				if (!File.Exists(ProgramOptions.StartupTask))
				{
					LogWarningMessage($"Waring: Start-up task: '{ProgramOptions.StartupTask}' does not exist");
				}
				else
				{
					LogMessage($"Running start-up task: {ProgramOptions.StartupTask}, arguments: {ProgramOptions.StartupTaskParams}, wait: {ProgramOptions.StartupTaskWait}");
					try
					{
						var output = Utils.RunExternalTask(ProgramOptions.StartupTask, ProgramOptions.StartupTaskParams, ProgramOptions.StartupTaskWait);
						if (ProgramOptions.StartupTaskWait)
						{
							LogDebugMessage($"Start-up task output: {output}");
						}
					}
					catch (FileNotFoundException)
					{
						LogErrorMessage("Start-up task Error: File " + ProgramOptions.StartupTask + " does not exist");
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"Error running start-up task");
					}
				}
			}

			if (FtpOptions.Logging && (FtpOptions.RealtimeEnabled || FtpOptions.IntervalEnabled) && (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				SetupFtpLogging(true);
			}

			LogMessage("Data path = " + Datapath);

			AppDomain.CurrentDomain.SetData("DataDirectory", Datapath);

			// Open database (create file if it doesn't exist)
			SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite ;

			// Open diary database (create file if it doesn't exist)
			DiaryDB = new SQLiteConnection(new SQLiteConnectionString(diaryfile, flags, false, null, null, null, null, "yyyy-MM-dd", false));

			try
			{
				var dbVer = 2;

				if (DiaryDB.ExecuteScalar<int>("SELECT 1 FROM PRAGMA_TABLE_INFO('DiaryData') WHERE name='Timestamp'") == 1)
				{
					LogMessage("Migrating the weather diary database to version 2");

					// rename the old table, create the new version, migrate the data, and drop the old table
					LogMessage("Renaming old weather diary table");
					DiaryDB.Execute("ALTER TABLE DiaryData RENAME TO DiaryDataOld");
					LogMessage("Creating new weather diary table");
					DiaryDB.CreateTable<DiaryData>();

					var snowHr = new TimeSpan(SnowDepthHour, 0, 0);
					var res = DiaryDB.Execute("INSERT OR REPLACE INTO DiaryData (Date, Time, Entry, SnowDepth) SELECT date(Timestamp), ?, entry, snowDepth FROM DiaryDataOld WHERE Timestamp > \"1900-01-01\" ORDER BY Timestamp", snowHr);
					LogMessage("Migrated " + res + " weather diary records");

					LogMessage("Dropping the old weather diary table");
					DiaryDB.Execute("DROP TABLE DiaryDataOld");
					LogMessage("Dropped the old weather diary table");
					LogMessage("Weather diary database migration to version 2 complete");

				}
				else if (DiaryDB.ExecuteScalar<int>("SELECT 1 FROM sqlite_master WHERE type='table' AND name='DiaryData'") == 1)
				{
					// DiaryData table exists

					if (DiaryDB.ExecuteScalar<int>("SELECT 1 FROM sqlite_master WHERE type='table' AND name='dbversion'") == 0
						|| DiaryDB.ExecuteScalar<int>("SELECT max(ver) FROM dbversion") < dbVer)
					{
						// dbversion table does not exist, or the dbversion is less than current

						// Does the diary table contain any rows?
						if (DiaryDB.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM DiaryData)") == 1)
						{
							LogMessage("Fixing any previous version migration issues");
							var res = DiaryDB.Execute("DELETE FROM DiaryData WHERE Date IN (SELECT Date FROM DiaryData GROUP BY Date(Date) HAVING count(*) = 2) AND Date = date(Date)");
							if (res == 0)
							{
								LogMessage("No duplicate date entries to fix");
							}
							else
							{
								LogMessage("Fixed " + res + " duplicate date entries");
							}
							res = DiaryDB.Execute("UPDATE DiaryData SET Date = date(Date) WHERE Date != date(Date)");
							if (res == 0)
							{
								LogMessage("No old date format entries to fix");
							}
							else
							{
								LogMessage("Fixed " + res + " old date format entries");
							}
						}

						DiaryDB.Execute("CREATE TABLE dbversion (ver INTEGER PRIMARY KEY)");
						DiaryDB.Execute("INSERT INTO dbversion (ver) VALUES (?)", dbVer);

						LogMessage("Previous version migration issues fixes complete");
					}
				}
				else
				{
					// The table does not exist
					// try to create the table, could be new empty db
					DiaryDB.CreateTable<DiaryData>();
					DiaryDB.Execute("CREATE TABLE dbversion (ver INTEGER PRIMARY KEY)");

					// Check if the dbversion table exists, and if it does if the version is less than current
					if (DiaryDB.ExecuteScalar<int>("SELECT max(ver) FROM dbversion") < dbVer)
					{
						DiaryDB.Execute("INSERT INTO dbversion (ver) VALUES (?)", dbVer);
					}
				}
			}
			catch (Exception ex)
			{
				LogErrorMessage("Error migrating or creating the Diary DB, exception = " + ex.Message);
			}

			LogMessage("Debug logging :" + (ProgramOptions.DebugLogging ? "enabled" : "disabled"));
			LogMessage("Data logging  :" + (ProgramOptions.DataLogging ? "enabled" : "disabled"));
			LogMessage("FTP logging   :" + (FtpOptions.Logging ? "enabled" : "disabled"));
			LogMessage("Email logging :" + (SmtpOptions.Logging ? "enabled" : "disabled"));
			LogMessage("Spike logging :" + (ErrorLogSpikeRemoval ? "enabled" : "disabled"));
			LogMessage("Logging interval = " + logints[DataLogInterval] + " mins");
			LogMessage("Real time interval:" + (RealtimeIntervalEnabled ? "enabled" : "disabled") + ", uploads:" + (FtpOptions.RealtimeEnabled ? "enabled" : "disabled") + ", (" + RealtimeInterval / 1000 + " secs)");
			LogMessage("Interval          :" + (WebIntervalEnabled ? "enabled" : "disabled") + ", uploads:" + (FtpOptions.IntervalEnabled ? "enabled" : "disabled") + ", (" + UpdateInterval + " mins)");
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

			SetUpHttpProxy();

			SetupMyHttpClient();

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

			// set alarms units
			HighWindAlarm.Units = Units.WindText;
			HighGustAlarm.Units = Units.WindText;
			HighRainRateAlarm.Units = Units.RainTrendText;
			HighRainTodayAlarm.Units = Units.RainText;
			PressChangeAlarm.Units = Units.PressTrendText;
			HighPressAlarm.Units = Units.PressText;
			LowPressAlarm.Units = Units.PressText;
			TempChangeAlarm.Units = Units.TempTrendText;
			HighTempAlarm.Units = Units.TempText;
			LowTempAlarm.Units = Units.TempText;

			LogMessage($"WindUnit={Units.WindText} RainUnit={Units.RainText} TempUnit={Units.TempText} PressureUnit={Units.PressText}");
			LogMessage($"Manual rainfall: YTDRain={YTDrain:F3}, Correction Year={YTDrainyear}");
			LogMessage($"RainDayThreshold={RainDayThreshold:F3}");
			LogMessage($"Roll over hour={RolloverHour:D2}");
			if (RolloverHour != 0)
			{
				LogMessage("Use 10am in summer =" + Use10amInSummer);
			}

			LogOffsetsMultipliers();

			LogPrimaryAqSensor();

			_ = GetLatestVersion();

			LogMessage("Cumulus Starting");

			// switch off logging from Unosquare.Swan which underlies embedIO
			Swan.Logging.Logger.NoLogging();

			var assemblyPath = System.AppContext.BaseDirectory;
			var htmlRootPath = Path.Combine(assemblyPath, "interface");

			LogMessage("HTML root path = " + htmlRootPath);

			WebSock = new MxWebSocket("/ws/", this);

			//var cert = new X509Certificate2(new X509Certificate("c:\\temp\\CumulusMX.pfx", "password", X509KeyStorageFlags.UserKeySet));

			WebServer httpServer = new WebServer(o => o
					.WithUrlPrefix($"http://*:{HTTPport}/")
					.WithMode(HttpListenerMode.EmbedIO)
			//		.WithCertificate(cert)
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
					.WithController<Api.InfoController>()
				)
				.WithWebApi("/station", m => m
					.WithController<Api.HttpStation>()
				)
				.WithModule(WebSock)
				.WithStaticFolder("/", htmlRootPath, true, m => m
					.WithoutContentCaching()
				);

			//httpServer.Listener.AddPrefix($"https://*:{HTTPport + 1000}/");

			// Set up the API web server
			// Some APi functions require the station, so set them after station initialisation
			Api.cumulus = this;
			Api.programSettings = new ProgramSettings(this);
			Api.stationSettings = new StationSettings(this);
			Api.internetSettings = new InternetSettings(this);
			Api.thirdpartySettings = new ThirdPartySettings(this);
			Api.extraSensorSettings = new ExtraSensorSettings(this);
			Api.calibrationSettings = new CalibrationSettings(this);
			Api.noaaSettings = new NoaaSettings(this);
			Api.alarmSettings = new AlarmSettings(this);
			Api.alarmUserSettings = new AlarmUserSettings(this);
			Api.mySqlSettings = new MysqlSettings(this);
			Api.mqttSettings = new MqttSettings(this);
			Api.customLogs = new CustomLogs(this);
			Api.dataEditor = new DataEditor(this);
			Api.tagProcessor = new ApiTagProcessor(this);
			Api.wizard = new Wizard(this);
			Api.langSettings = new LangSettings(this);
			Api.displaySettings = new DisplaySettings(this);

			_ = httpServer.RunAsync();

			// get the local v4 IP addresses
			Console.WriteLine();
			var ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
			Console.Write("Cumulus running at: ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"http://localhost:{HTTPport}/");
			Console.ResetColor();
			foreach (var ip in ips)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					LogConsoleMessage($"                    http://{ip}:{HTTPport}/", ConsoleColor.Yellow);
					LogMessage($"Cumulus running at: http://{ip}:{HTTPport}/");
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

			SyncInit.Wait();

			if (StationType >= 0 && StationType < StationDesc.Length)
			{
				LogConsoleMessage($"Opening station type {StationType} - {StationDesc[StationType]}");
				LogMessage($"Opening station type {StationType} - {StationDesc[StationType]}");
			}
			else
			{
				LogMessage("Opening station type " + StationType);
			}

			Manufacturer = GetStationManufacturer(StationType);
			switch (StationType)
			{
				case StationTypes.FineOffset:
				case StationTypes.FineOffsetSolar:
					station = new FOStation(this);
					break;
				case StationTypes.VantagePro:
				case StationTypes.VantagePro2:
					station = new DavisStation(this);
					break;
				case StationTypes.WMR928:
					station = new WMR928Station(this);
					break;
				case StationTypes.WM918:
					station = new WM918Station(this);
					break;
				case StationTypes.WS2300:
					station = new WS2300Station(this);
					break;
				case StationTypes.WMR200:
					station = new WMR200Station(this);
					break;
				case StationTypes.Instromet:
					station = new ImetStation(this);
					break;
				case StationTypes.WMR100:
					station = new WMR100Station(this);
					break;
				case StationTypes.EasyWeather:
					station = new EasyWeather(this);
					station.LoadLastHoursFromDataLogs(DateTime.Now);
					break;
				case StationTypes.WLL:
					station = new DavisWllStation(this);
					break;
				case StationTypes.GW1000:
					station = new GW1000Station(this);
					break;
				case StationTypes.Tempest:
					station = new TempestStation(this);
					break;
				case StationTypes.HttpWund:
					station = new HttpStationWund(this);
					break;
				case StationTypes.HttpEcowitt:
					station = new HttpStationEcowitt(this);
					break;
				case StationTypes.HttpAmbient:
					station = new HttpStationAmbient(this);
					break;
				case StationTypes.Simulator:
					station = new Simulator(this);
					break;
				case StationTypes.EcowittCloud:
					station = new EcowittCloudStation(this);
					break;
				case StationTypes.DavisCloudWll:
				case StationTypes.DavisCloudVP2:
					station = new DavisCloudStation(this);
					break;
				case StationTypes.JsonStation:
					station = new JsonStation(this);
					break;
				case StationTypes.EcowittHttpApi:
					station = new EcowittHttpApiStation(this);
					break;

				default:
					LogConsoleMessage("Station type not set", ConsoleColor.Red);
					LogMessage("Station type not set = " + StationType);
					break;
			}

			LogMessage($"Wind settings: Calc avg speed={StationOptions.CalcuateAverageWindSpeed}, Use speed for avg={StationOptions.UseSpeedForLatest}, Gust time={StationOptions.PeakGustMinutes}, Avg time={StationOptions.AvgSpeedMinutes}");

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
				else if (StationType == StationTypes.JsonStation && JsonStationOptions.Connectiontype == 1)
				{
					Api.stationJson = (JsonStation) station;
				}

				if (AirLinkInEnabled)
				{
					LogMessage("Creating indoor AirLink station");
					LogConsoleMessage($"Opening indoor AirLink");
					airLinkDataIn = new AirLinkData();
					airLinkIn = new DavisAirLink(this, true, station);
				}
				if (AirLinkOutEnabled)
				{
					LogMessage("Creating outdoor AirLink station");
					LogConsoleMessage($"Opening outdoor AirLink");
					airLinkDataOut = new AirLinkData();
					airLinkOut = new DavisAirLink(this, false, station);
				}
				if (EcowittExtraEnabled)
				{
					LogMessage("Creating Ecowitt extra sensors station");
					LogConsoleMessage($"Opening Ecowitt extra sensors");
					ecowittExtra = new HttpStationEcowitt(this, station);
					Api.stationEcowittExtra = ecowittExtra;
				}
				if (AmbientExtraEnabled)
				{
					LogMessage("Creating Ambient extra sensors station");
					LogConsoleMessage($"Opening Ambient extra sensors");
					ambientExtra = new HttpStationAmbient(this, station);
					Api.stationAmbientExtra = ambientExtra;
				}
				if (EcowittCloudExtraEnabled)
				{
					LogMessage("Creating Ecowitt cloud extra sensors station");
					LogConsoleMessage($"Opening Ecowitt cloud extra sensors");
					ecowittCloudExtra = new EcowittCloudStation(this, station);
				}
				if (JsonExtraStationOptions.ExtraSensorsEnabled)
				{
					LogMessage("Creating JSON station extra sensors station");
					LogConsoleMessage($"Opening JSON extra sensors");
					stationJsonExtra = new JsonStation(this, station);
					Api.stationJsonExtra = stationJsonExtra;
				}


				// set the third party upload station
				Wund.station = station;
				Windy.station = station;
				WindGuru.station = station;
				PWS.station = station;
				WOW.station = station;
				APRS.station = station;
				AWEKAS.station = station;
				WCloud.station = station;
				OpenWeatherMap.station = station;
				Bluesky.station = station;
				Bluesky.CancelToken = cancellationToken;

				WebTags = new WebTags(this, station);
				WebTags.InitialiseWebtags();

				httpFiles = new HttpFiles(this, station);

				Api.httpFiles = httpFiles;

				if (FtpOptions.FtpMode != Cumulus.FtpProtocols.PHP && RealtimeIntervalEnabled && FtpOptions.RealtimeEnabled)
				{
					if (FtpOptions.FtpMode == Cumulus.FtpProtocols.SFTP)
					{
						RealtimeSSHLogin();
					}
					else
					{
						RealtimeFTPLogin();
					}
				}
				RealtimeTimer.Interval = RealtimeInterval;
				RealtimeTimer.Elapsed += RealtimeTimerTick;
				RealtimeTimer.AutoReset = true;

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
				}

				InitialiseRG11();


				// Do the start-up  MySQL commands before the station is started
				if (MySqlSettings.CustomStartUp.Enabled)
				{
					CustomMySqlStartUp();
				}

				if (station.timerStartNeeded)
				{
					StartTimersAndSensors();
				}

				if ((StationType == StationTypes.WMR100) || (StationType == StationTypes.EasyWeather) || (Manufacturer == OREGON) || StationType == StationTypes.Simulator)
				{
					station.StartLoop();
				}

				// let the web socket know about the station
				WebSock.SetSetStation(station);

				// If enabled generate the daily graph data files, and upload at first opportunity
				LogDebugMessage("Generating the daily graph data files");
				station.CreateEodGraphDataFiles();
				station.CreateDailyGraphDataFiles();
			}

			LogDebugMessage("Lock: Cumulus releasing the lock");
			SyncInit.Release();
		}

		internal void SetUpHttpProxy()
		{
			if (!string.IsNullOrEmpty(HTTPProxyName))
			{
				var proxy = new WebProxy(HTTPProxyName, HTTPProxyPort);
				MyHttpSocketsHttpHandler.Proxy = proxy;
				MyHttpSocketsHttpHandler.UseProxy = true;

				phpUploadSocketHandler.Proxy = proxy;
				phpUploadSocketHandler.UseProxy = true;

				if (!string.IsNullOrEmpty(HTTPProxyUser))
				{
					var creds = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					MyHttpSocketsHttpHandler.Credentials = creds;
					phpUploadSocketHandler.Credentials = creds;
				}
			}
		}

		internal void SetupPhpUploadClients()
		{
			var sslOptions = new SslClientAuthenticationOptions()
			{
				RemoteCertificateValidationCallback = new RemoteCertificateValidationCallback((sender, certificate, chain, policyErrors) => { return FtpOptions.PhpIgnoreCertErrors || policyErrors == SslPolicyErrors.None; })
			};


			phpUploadSocketHandler = new SocketsHttpHandler()
			{
				PooledConnectionLifetime = TimeSpan.FromMinutes(10),
				SslOptions = sslOptions,
				MaxConnectionsPerServer = Properties.Settings.Default.PhpMaxConnections,
				AllowAutoRedirect = false
			};

			phpUploadHttpClient = new HttpClient(phpUploadSocketHandler)
			{
				// 15 second timeout
				Timeout = TimeSpan.FromSeconds(15)
			};
		}

		internal void SetupMyHttpClient()
		{
			MyHttpSocketsHttpHandler = new SocketsHttpHandler()
			{
				PooledConnectionLifetime = TimeSpan.FromMinutes(2)
			};

			MyHttpClient = new HttpClient(MyHttpSocketsHttpHandler)
			{
				Timeout = TimeSpan.FromSeconds(30)
			};
		}


		internal void TestPhpUploadCompression()
		{
			LogDebugMessage($"Testing PHP upload compression: '{FtpOptions.PhpUrl}'");
			using var request = new HttpRequestMessage(HttpMethod.Get, FtpOptions.PhpUrl);
			try
			{
				request.Headers.Add("Accept", "text/html");
				request.Headers.Add("Accept-Encoding", "gzip, deflate" + (FtpOptions.PhpUseBrotli ? ", br" : ""));

				// we do this async
				var response = phpUploadHttpClient.SendAsync(request).Result;

				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					LogErrorMessage("TestPhpUploadCompression: Failed to find the upload.php script on your server - check the path");
					return;
				}

				if (response.StatusCode == HttpStatusCode.InternalServerError)
				{
					var rawMessage = response.Content.ReadAsStringAsync().Result;

					if (rawMessage == "You must change the default secret")
					{
						LogErrorMessage("TestPhpUploadCompression: The upload.php script on your server still has the default secret set in it");
						return;
					}
				}

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

				// Check the max requests
				CheckPhpMaxUploads(response.Headers);
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "TestPhpUploadCompression: Error");
			}
		}

		private void CheckPhpMaxUploads(System.Net.Http.Headers.HttpResponseHeaders headers)
		{
			if (headers.Connection.Contains("keep-alive"))
			{
				var keepalive = headers.Connection.ToString();
				if (keepalive.ContainsCI("max="))
				{
					var regex = regexMaxParam();
					var match = regex.Match(keepalive);
					if (regex.IsMatch(keepalive))
					{
						LogDebugMessage($"PHP upload - will reset connection after {match.Groups[1].Value} requests");
					}
				}

				if (keepalive.ContainsCI("max="))
				{
					LogDebugMessage("PHP upload - remote server has closed the connection");
				}
			}
		}

		private void CustomHttpSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!station.DataStopped)
				_ = CustomHttpSecondsUpdate();
		}


		internal void SetupRealtimeMySqlTable()
		{
			RealtimeTable = new MySqlTable(MySqlSettings.Realtime.TableName);
			RealtimeTable.AddColumn("LogDateTime", "DATETIME NOT NULL");
			RealtimeTable.AddColumn("temp", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("hum", "decimal(4," + HumDPlaces + ")");
			RealtimeTable.AddColumn("dew", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("wspeed", "decimal(4," + WindDPlaces + ")");
			RealtimeTable.AddColumn("wlatest", "decimal(4," + WindDPlaces + ")");
			RealtimeTable.AddColumn("bearing", "VARCHAR(3)");
			RealtimeTable.AddColumn("rrate", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("rfall", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("press", "decimal(6," + PressDPlaces + ")");
			RealtimeTable.AddColumn("currentwdir", "VARCHAR(3)");
			RealtimeTable.AddColumn("beaufortnumber", "varchar(2)");
			RealtimeTable.AddColumn("windunit", "varchar(4) NOT NULL");
			RealtimeTable.AddColumn("tempunitnodeg", "varchar(1) NOT NULL");
			RealtimeTable.AddColumn("pressunit", "varchar(3) NOT NULL");
			RealtimeTable.AddColumn("rainunit", "varchar(2) NOT NULL");
			RealtimeTable.AddColumn("windrun", "decimal(4," + WindRunDPlaces + ")");
			RealtimeTable.AddColumn("presstrendval", "varchar(6)");
			RealtimeTable.AddColumn("rmonth", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("ryear", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("rfallY", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("intemp", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("inhum", "decimal(4," + HumDPlaces + ")");
			RealtimeTable.AddColumn("wchill", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("temptrend", "varchar(5)");
			RealtimeTable.AddColumn("tempTH", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("TtempTH", "varchar(5)");
			RealtimeTable.AddColumn("tempTL", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("TtempTL", "varchar(5)");
			RealtimeTable.AddColumn("windTM", "decimal(4," + WindDPlaces + ")");
			RealtimeTable.AddColumn("TwindTM", "varchar(5)");
			RealtimeTable.AddColumn("wgustTM", "decimal(4," + WindDPlaces + ")");
			RealtimeTable.AddColumn("TwgustTM", "varchar(5)");
			RealtimeTable.AddColumn("pressTH", "decimal(6," + PressDPlaces + ")");
			RealtimeTable.AddColumn("TpressTH", "varchar(5)");
			RealtimeTable.AddColumn("pressTL", "decimal(6," + PressDPlaces + ")");
			RealtimeTable.AddColumn("TpressTL", "varchar(5)");
			RealtimeTable.AddColumn("version", "varchar(8)");
			RealtimeTable.AddColumn("build", "varchar(5)");
			RealtimeTable.AddColumn("wgust", "decimal(4," + WindDPlaces + ")");
			RealtimeTable.AddColumn("heatindex", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("humidex", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("UV", "decimal(3," + UVDPlaces + ")");
			RealtimeTable.AddColumn("ET", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("SolarRad", "decimal(5,1)");
			RealtimeTable.AddColumn("avgbearing", "varchar(3)");
			RealtimeTable.AddColumn("rhour", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.AddColumn("forecastnumber", "varchar(2)");
			RealtimeTable.AddColumn("isdaylight", "varchar(1)");
			RealtimeTable.AddColumn("SensorContactLost", "varchar(1)");
			RealtimeTable.AddColumn("wdir", "varchar(3)");
			RealtimeTable.AddColumn("cloudbasevalue", "varchar(5)");
			RealtimeTable.AddColumn("cloudbaseunit", "varchar(2) NOT NULL");
			RealtimeTable.AddColumn("apptemp", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("SunshineHours", "decimal(3," + SunshineDPlaces + ")");
			RealtimeTable.AddColumn("CurrentSolarMax", "decimal(5,1)");
			RealtimeTable.AddColumn("IsSunny", "varchar(1)");
			RealtimeTable.AddColumn("FeelsLike", "decimal(4," + TempDPlaces + ")");
			RealtimeTable.AddColumn("rweek", "decimal(4," + RainDPlaces + ")");
			RealtimeTable.PrimaryKey = "LogDateTime";
			RealtimeTable.Comment = "\"Realtime log\"";
		}

		internal void SetupMonthlyMySqlTable()
		{
			MonthlyTable = new MySqlTable(MySqlSettings.Monthly.TableName);
			MonthlyTable.AddColumn("LogDateTime", "DATETIME NOT NULL");
			MonthlyTable.AddColumn("Temp", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("Humidity", "decimal(4," + HumDPlaces + ")");
			MonthlyTable.AddColumn("Dewpoint", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("Windspeed", "decimal(4," + WindAvgDPlaces + ")");
			MonthlyTable.AddColumn("Windgust", "decimal(4," + WindDPlaces + ")");
			MonthlyTable.AddColumn("Windbearing", "VARCHAR(3)");
			MonthlyTable.AddColumn("RainRate", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("TodayRainSoFar", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("Pressure", "decimal(6," + PressDPlaces + ")");
			MonthlyTable.AddColumn("Raincounter", "decimal(6," + RainDPlaces + ")");
			MonthlyTable.AddColumn("InsideTemp", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("InsideHumidity", "decimal(4," + HumDPlaces + ")");
			MonthlyTable.AddColumn("LatestWindGust", "decimal(5," + WindDPlaces + ")");
			MonthlyTable.AddColumn("WindChill", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("HeatIndex", "decimal(4," + TempDPlaces + ")");
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
			DayfileTable.AddColumn("HighWindGust", "decimal(4," + WindDPlaces + ")");
			DayfileTable.AddColumn("HWindGBear", "varchar(3)");
			DayfileTable.AddColumn("THWindG", "varchar(5)");
			DayfileTable.AddColumn("MinTemp", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TMinTemp", "varchar(5)");
			DayfileTable.AddColumn("MaxTemp", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TMaxTemp", "varchar(5)");
			DayfileTable.AddColumn("MinPress", "decimal(6," + PressDPlaces + ")");
			DayfileTable.AddColumn("TMinPress", "varchar(5)");
			DayfileTable.AddColumn("MaxPress", "decimal(6," + PressDPlaces + ")");
			DayfileTable.AddColumn("TMaxPress", "varchar(5)");
			DayfileTable.AddColumn("MaxRainRate", "decimal(4," + RainDPlaces + ")");
			DayfileTable.AddColumn("TMaxRR", "varchar(5)");
			DayfileTable.AddColumn("TotRainFall", "decimal(6," + RainDPlaces + ")");
			DayfileTable.AddColumn("AvgTemp", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("TotWindRun", "decimal(5," + WindRunDPlaces + ")");
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
				case 3:
					Units.PressText = "kPa";
					Units.PressTrendText = "kPa/hr";
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

			Units.SnowText = Units.SnowDepth == 0 ? "cm" : "in";
			SnowDPlaces = Units.SnowDepth == 0 ? 1 : 2;
			SnowFormat = "F" + SnowDPlaces;
		Units.LaserDistanceText = Units.LaserDistance == 0 ? "cm" : "in";
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
			ChillHourBase = -99;

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

			FCPressureThreshold = Units.Press switch
			{
				0 => 0.1,
				1 => 0.1,
				2 => 0.00295333727,
				3 => 0.01,
				_ => 0
			};
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

		public void SetRealTimeFtpLogging(bool isSet)
		{
			try
			{
				if (RealtimeFTP == null || RealtimeFTP.IsDisposed)
					return;
			}
			catch
			{
				return;
			}

			if (isSet)
			{
				if (FtpLoggerRT == null || FtpLoggerIN == null || FtpLoggerMX == null)
				{
					SetupFtpLogging(true);
				}
				RealtimeFTP.Logger = new FtpLogAdapter(FtpLoggerRT);
			}
			else
			{
				RealtimeFTP.Logger = null;
			}
		}

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
			bool isDevice1 = (((SerialPort) sender).PortName == RG11Port) && ((isDSR && RG11DTRmode) || (isCTS && !RG11DTRmode));
			// Is this a trigger that the second RG11 is configured for?
			bool isDevice2 = (((SerialPort) sender).PortName == RG11Port2) && ((isDSR && RG11DTRmode2) || (isCTS && !RG11DTRmode2));

			// is the pin on or off?
			bool isOn = (isDSR && ((SerialPort) sender).DsrHolding) || (isCTS && ((SerialPort) sender).CtsHolding);

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

		private void WebTimerTick(object sender, ElapsedEventArgs e)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything else
				return;
			}

			if (WebUpdating == 1)
			{
				LogWarningMessage("Warning, previous web update is still in progress, first chance, skipping this interval");
				WebUpdating++;
			}
			else if (WebUpdating >= 2)
			{
				LogWarningMessage("Warning, previous web update is still in progress, second chance, aborting connection");
				if (ftpThread.ThreadState == System.Threading.ThreadState.Running)
					ftpThread.Interrupt();
				LogMessage("Trying new web update");
				WebUpdating = 1;
				ftpThread = new Thread(async () => await DoHTMLFiles()) { IsBackground = true };
				ftpThread.Start();
			}
			else
			{
				WebUpdating = 1;
				ftpThread = new Thread(async () => await DoHTMLFiles()) { IsBackground = true };
				ftpThread.Start();
			}
		}

		public void MQTTSecondChanged(DateTime now)
		{
			if (MQTT.EnableInterval && !station.DataStopped)
				MqttPublisher.UpdateMQTTfeed("Interval", now);
		}


		// Find all stations associated with the users API key
		internal OpenWeatherMapStation[] GetOpenWeatherMapStations()
		{
			OpenWeatherMapStation[] retVal = [];
			string url = "http://api.openweathermap.org/data/3.0/stations?appid=" + OpenWeatherMap.PW;
			try
			{
				using var response = MyHttpClient.GetAsync(url).Result;
				var responseBodyAsText = response.Content.ReadAsStringAsync().Result;
				LogDataMessage("OpenWeatherMap: Get Stations Response: " + response.StatusCode + ": " + responseBodyAsText);

				if (responseBodyAsText.Length > 10)
				{
					var respJson = JsonSerializer.DeserializeFromString<OpenWeatherMapStation[]>(responseBodyAsText);
					retVal = respJson;
				}
			}
			catch (Exception ex)
			{
				LogWarningMessage("OpenWeatherMap: Get Stations ERROR - " + ex.Message);
			}

			return retVal;
		}

		// Create a new OpenWeatherMap station
		internal void CreateOpenWeatherMapStation()
		{
			var invC = CultureInfo.InvariantCulture;

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

				using var response = MyHttpClient.PostAsync(url, data).Result;
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
					LogWarningMessage($"OpenWeatherMap: Failed to create new station. Error - {response.StatusCode}, text - {responseBodyAsText}");
				}
			}
			catch (Exception ex)
			{
				LogWarningMessage("OpenWeatherMap: Create station ERROR - " + ex.Message);
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
					LogWarningMessage("OpenWeatherMap: " + msg);
					foreach (var stn in stations)
					{
						msg = $"  Station Id = {stn.id}, Name = {stn.name}";
						LogConsoleMessage(msg);
						LogMessage("OpenWeatherMap: " + msg);
					}
				}
			}
		}

		internal void RealtimeTimerTick(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			var cycle = RealtimeCycleCounter++;

			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogWarningMessage($"Realtime[{cycle}]: Not all data is ready, aborting process");
				return;
			}

			LogDebugMessage($"Realtime[{cycle}]: Start cycle");
			try
			{
				// Process any files
				if (RealtimeCopyInProgress || RealtimeFtpInProgress)
				{
					LogWarningMessage($"Realtime[{cycle}]: Warning, a previous cycle is still processing local files. Skipping this interval.");
					return;
				}
				else
				{
					RealtimeCopyInProgress = true;
					CreateRealtimeFile(cycle);
					CreateRealtimeHTMLfiles(cycle);

					if (FtpOptions.LocalCopyEnabled)
					{
						RealtimeLocalCopy(cycle);
					}
					RealtimeCopyInProgress = false;

					if (FtpOptions.RealtimeEnabled && FtpOptions.Enabled && (!RealtimeFtpReconnecting || FtpOptions.FtpMode == FtpProtocols.PHP))
					{
						// Is a previous cycle still running?
						if (RealtimeFtpInProgress)
						{
							LogMessage($"Realtime[{cycle}]: Warning, a previous cycle is still trying to connect to upload server, skip count = {++realtimeFTPRetries}");
							// real time interval is in ms, if a session has been uploading for 3 minutes - abort it and reconnect
							if (realtimeFTPRetries * RealtimeInterval / 1000 > 3 * 60)
							{
								LogWarningMessage($"Realtime[{cycle}]: Realtime has been in progress for more than 3 minutes, attempting to reconnect.");
								_ = RealtimeFTPReconnect();
							}
							else
							{
								LogWarningMessage($"Realtime[{cycle}]: No FTP attempted this cycle");
							}
						}
						else
						{
							// This only happens if the user enables realtime FTP after starting Cumulus
							if (FtpOptions.FtpMode == FtpProtocols.SFTP && (RealtimeSSH == null || !RealtimeSSH.ConnectionInfo.IsAuthenticated))
							{
								RealtimeFTPReconnect().Wait();
							}
							if ((FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS) && !RealtimeFTP.IsConnected)
							{
								RealtimeFTPReconnect().Wait();
							}

							if (!RealtimeFtpReconnecting || FtpOptions.FtpMode == FtpProtocols.PHP)
							{
								// Finally we can do some FTP!
								try
								{
									RealtimeUpload(cycle).Wait();
									realtimeFTPRetries = 0;
								}
								catch (Exception ex)
								{
									LogExceptionMessage(ex, $"Realtime[{cycle}]: Error during realtime upload.");
								}
							}
						}
					}

					if (!string.IsNullOrEmpty(RealtimeProgram))
					{
						if (!File.Exists(RealtimeProgram))
						{
							LogWarningMessage($"Warning: Realtime program '{RealtimeProgram}' does not exist");
						}
						else
						{
							try
							{
								var args = string.Empty;

								if (!string.IsNullOrEmpty(RealtimeParams))
								{
									var parser = new TokenParser(TokenParserOnToken)
									{
										InputText = RealtimeParams
									};
									args = parser.ToStringFromString();
								}
								LogDebugMessage($"Realtime[{cycle}]: Execute realtime program - {RealtimeProgram}, with parameters - {args}");
								_ = Utils.RunExternalTask(RealtimeProgram, args, false);
							}
							catch (FileNotFoundException)
							{
								LogErrorMessage($"Realtime[{cycle}]: Error: Realtime program '{RealtimeProgram}' does not exist");
							}
							catch (Exception ex)
							{
								LogErrorMessage($"Realtime[{cycle}]: Error in realtime program - {RealtimeProgram}. Error: {ex.Message}");
							}
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
					RealtimeCopyInProgress = false;
					RealtimeFtpInProgress = false;
					LogDebugMessage($"Realtime[{cycle}]: End cycle");
					_ = RealtimeFTPReconnect();
					return;
				}
			}

			RealtimeCopyInProgress = false;
			RealtimeFtpInProgress = false;
			LogDebugMessage($"Realtime[{cycle}]: End cycle");
		}

		private async Task RealtimeFTPReconnect()
		{
			if (FtpOptions.FtpMode == FtpProtocols.PHP)
				return;

			if (RealtimeFtpReconnecting)
			{
				LogDebugMessage("RealtimeFTPReconnect: Already reconnecting, skipping this attempt");
				return;
			}

			RealtimeFtpReconnecting = true;
			RealtimeFtpInProgress = true;

			FtpAlarm.LastMessage = "Realtime re-connecting";
			FtpAlarm.Triggered = true;

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
							if (RealtimeSSH != null)
							{
								RealtimeSSH.Disconnect();
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
						// Attempt a simple reconnect
						try
						{
							LogMessage("RealtimeReconnect: Realtime ftp attempting to reconnect");
							RealtimeSSH.Connect();
							connected = RealtimeSSH.ConnectionInfo.IsAuthenticated;
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
							LogErrorMessage($"RealtimeReconnect: Error reconnecting ftp server - {ex.Message}");
							if (ex.InnerException != null)
							{
								ex = Utils.GetOriginalException(ex);
								LogErrorMessage($"RealtimeReconnect: Base exception - {ex.Message}");
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
							LogWarningMessage("RealtimeReconnect: Realtime ftp connection failed to connect after reinitialisation");
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
								LogWarningMessage("RealtimeReconnect: Realtime ftp connection test failed to get Present Working Directory");
							}
							else
							{
								LogMessage($"RealtimeReconnect: Realtime ftp connection test found Present Working Directory OK - [{pwd}]");
							}
						}
						catch (Exception ex)
						{
							LogErrorMessage($"RealtimeReconnect: Realtime ftp connection test Failed - {ex.Message}");

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
				FtpAlarm.LastMessage = "Realtime re-connected";
				FtpAlarm.Triggered = false;
				LogMessage("RealtimeReconnect: Realtime FTP now connected to server (tested)");
				LogMessage("RealtimeReconnect: Realtime FTP operations will be restarted");
			});
		}

		private void RealtimeLocalCopy(byte cycle)
		{
			var dstPath = string.Empty;
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
						LogErrorMessage($"RealtimeLocalCopy[{cycle}]: Error copying [{srcFile}] to [{dstFile}. Error = {ex.Message}");
					}
				}
			}
		}


		private async Task RealtimeUpload(byte cycle)
		{
			var remotePath = string.Empty;
			var tasklist = new List<Task>();

			var taskCount = 0;
			var runningTaskCount = 0;

			RealtimeFtpInProgress = true;

			if (FtpOptions.Directory.Length > 0)
			{
				remotePath = (FtpOptions.Directory.EndsWith('/') ? FtpOptions.Directory : FtpOptions.Directory + '/');
			}

			LogDebugMessage($"Realtime[{cycle}]: Real time upload files starting");

			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].FTP)
				{
					var remoteFile = remotePath + RealtimeFiles[i].RemoteFileName;

					string data = string.Empty;

					if (FtpOptions.FtpMode != FtpProtocols.PHP)
					{
						// realtime file
						if (RealtimeFiles[i].LocalFileName == "realtime.txt")
						{
							data = CreateRealtimeFileString(cycle);
						}
						else if (RealtimeFiles[i].LocalFileName == "realtimegauges.txt")
						{
							data = await ProcessTemplateFile2StringAsync(RealtimeFiles[i].TemplateFileName, true, true);
						}

						using var dataStream = GenerateStreamFromString(data);
						if (FtpOptions.FtpMode == FtpProtocols.SFTP)
						{
							LogDebugMessage($"Realtime[{cycle}]: Uploading - {RealtimeFiles[i].RemoteFileName}");
							_ = UploadStream(RealtimeSSH, remoteFile, dataStream, cycle);
						}
						else if (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							LogFtpDebugMessage($"Realtime[{cycle}]: Uploading - {RealtimeFiles[i].RemoteFileName}");
							_ = UploadStream(RealtimeFTP, remoteFile, dataStream, cycle);
						}
					}
					else // PHP
					{

						try
						{
#if DEBUG
							LogDebugMessage($"RealtimePHP[{cycle}]: Real time file {RealtimeFiles[i].RemoteFileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
							await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
							LogDebugMessage($"RealtimePHP[{cycle}]: Real time file {RealtimeFiles[i].RemoteFileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
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
#if DEBUG
							LogDebugMessage($"RealtimePHP[{cycle}]: Processing Real time file [{idx}] - {RealtimeFiles[idx].LocalFileName} to {RealtimeFiles[idx].RemoteFileName}");
#endif
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
								LogDebugMessage($"RealtimePHP[{cycle}]: Real time file [{idx}] {RealtimeFiles[idx].RemoteFileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
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
				LogDebugMessage($"RealtimePHP[{cycle}]: Extra Files starting");

				for (var i = 0; i < ActiveExtraFiles.Count; i++)
				{
					var item = ActiveExtraFiles[i];

					if (!item.realtime || !item.FTP)
					{
						continue;
					}

					var uploadfile = GetUploadFilename(item.local, DateTime.Now);

					if (!File.Exists(uploadfile))
					{
						LogWarningMessage($"RealtimePHP[{cycle}]: Warning, extra web file not found! - {uploadfile}");
						return;
					}

					bool incremental = false;
					var linesAdded = 0;
					var data = string.Empty;

					// Is this an incremental log file upload?
					if (item.incrementalLogfile && !item.binary)
					{
						// has the log file rolled over?
						if (item.logFileLastFileName != uploadfile)
						{
							ActiveExtraFiles[i].logFileLastFileName = uploadfile;
							ActiveExtraFiles[i].logFileLastLineNumber = 0;
						}

						incremental = item.logFileLastLineNumber > 0;

						data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

						if (linesAdded == 0)
						{
							LogDebugMessage($"RealtimePHP[{cycle}]: Extra file: {uploadfile} - No incremental data found");
							continue;
						}
					}

					try
					{
#if DEBUG
						LogDebugMessage($"RealtimePHP[{cycle}]: Extra File {uploadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
						LogDebugMessage($"RealtimePHP[{cycle}]: Extra File {uploadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(cancellationToken);
#endif
					}
					catch (OperationCanceledException)
					{
						return;
					}

					var remotefile = GetRemoteFileName(item.remote, DateTime.Now);
					var idx = i;

					Interlocked.Increment(ref taskCount);

					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return false;

							// all checks OK, file needs to be uploaded
							// Is this an incremental log file upload?
							if (item.incrementalLogfile && !item.binary)
							{
								LogDebugMessage($"RealtimePHP[{cycle}]: Uploading extra web incremental file {uploadfile} to {remotefile} ({(incremental ? $"Incrementally - {linesAdded} lines" : "Fully")})");
								if (await UploadString(phpUploadHttpClient, incremental, string.Empty, data, remotefile, cycle, item.binary, item.UTF8, true, item.logFileLastLineNumber))
								{
									ActiveExtraFiles[idx].logFileLastLineNumber += linesAdded;
								}
							}
							else
							{
								LogDebugMessage($"RealtimePHP[{cycle}]: Uploading extra web file {uploadfile} to {remotefile}");

								if (item.process)
								{
									LogDebugMessage($"RealtimePHP[{cycle}]: Processing extra web file {uploadfile}");
									var str = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);

									_ = await UploadString(phpUploadHttpClient, false, string.Empty, str, remotefile, cycle, item.binary, item.UTF8);
								}
								else
								{
									_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, cycle, item.binary, item.UTF8);
								}
							}
						}
						finally
						{
							uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
							LogDebugMessage($"RealtimePHP[{cycle}]: Extra Web File {uploadfile} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, cancellationToken));

					Interlocked.Increment(ref runningTaskCount);
				}

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
						// wait on the task to complete, but timeout after 20 seconds
						if (Task.WaitAll([.. tasklist], TimeSpan.FromSeconds(20)))
						{
							LogDebugMessage($"RealtimePHP[{cycle}]: Real time files complete, {tasklist.Count} files uploaded");
						}
						else
						{
							LogDebugMessage($"RealtimePHP[{cycle}]: Real time files timed out waiting for the uploads to complete");
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"RealtimePHP[{cycle}]: Error waiting on upload tasks");
					}
				}

				LogDebugMessage($"RealtimePHP[{cycle}]: Real time files process end");
				tasklist.Clear();

				RealtimeFtpInProgress = false;
			}
			else // It's old fashioned FTP/FTPS/SFTP
			{
				for (var i = 0; i < ActiveExtraFiles.Count; i++)
				{
					var item = ActiveExtraFiles[i];

					if (!item.realtime || !item.FTP)
					{
						continue;
					}

					var uploadfile = GetUploadFilename(item.local, DateTime.Now);

					if (!File.Exists(uploadfile))
					{
						LogWarningMessage($"Realtime[{cycle}]: Warning, extra web file not found! - {uploadfile}");
						continue;
					}

					var remotefile = GetRemoteFileName(item.remote, DateTime.Now);

					// all checks OK, file needs to be uploaded
					if (FtpOptions.FtpMode == FtpProtocols.SFTP)
					{
						LogDebugMessage($"Realtime[{cycle}]: Uploading extra web {uploadfile} to {remotefile}");
					}
					else
					{
						LogFtpMessage("");
						LogFtpDebugMessage($"Realtime[{cycle}]: Uploading extra web {uploadfile} to {remotefile}");
					}


					// Is this an incremental log file upload?
					if (item.incrementalLogfile && !item.binary)
					{
						// has the log file rolled over?
						if (item.logFileLastFileName != uploadfile)
						{
							ActiveExtraFiles[i].logFileLastFileName = uploadfile;
							ActiveExtraFiles[i].logFileLastLineNumber = 0;
						}

						var linesAdded = 0;
						var data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

						if (linesAdded == 0)
						{
							LogDebugMessage($"Realtime[{cycle}]: Extra file: {uploadfile} - No incremental data found");
							continue;
						}

						// have we already uploaded the base file?
						if (item.logFileLastLineNumber > 0)
						{
							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								if (AppendText(RealtimeSSH, remotefile, data, cycle, linesAdded))
								{
									ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
								}
							}
							else
							{
								if (AppendText(RealtimeFTP, remotefile, data, cycle, linesAdded))
								{
									ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
								}
							}
						}
						else // no, just upload the base file
						{
							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								if (UploadFile(RealtimeSSH, uploadfile, remotefile, cycle))
								{
									ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
								}
							}
							else
							{
								if (UploadFile(RealtimeFTP, uploadfile, remotefile, cycle))
								{
									ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
								}
							}
						}
					}
					else if (item.process) // does the file require processing first
					{
						LogDebugMessage($"Realtime[{cycle}]: Processing extra web {uploadfile}");
						var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);

						using var strm = GenerateStreamFromString(data);
						if (FtpOptions.FtpMode == FtpProtocols.SFTP)
						{
							UploadStream(RealtimeSSH, remotefile, strm, cycle);
						}
						else
						{
							UploadStream(RealtimeFTP, remotefile, strm, cycle);
						}
					}
					else // its just a plain old file - upload it
					{
						if (FtpOptions.FtpMode == FtpProtocols.SFTP)
						{
							UploadFile(RealtimeSSH, uploadfile, remotefile, cycle);
						}
						else
						{
							UploadFile(RealtimeFTP, uploadfile, remotefile, cycle);
						}
					}
				}
				// all done
				RealtimeFtpInProgress = false;
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
						ProcessTemplateFile(RealtimeFiles[i].TemplateFileName, destFile, true, UTF8encode);
					}
					catch (Exception ex)
					{
						LogDebugMessage($"Realtime[{cycle}]: Error creating file [{RealtimeFiles[i].LocalFileName}] to [{destFile}]. Error = {ex.Message}");
					}
				}
			}

			foreach (var item in ActiveExtraFiles)
			{
				if (item.realtime && !item.FTP)
				{
					var uploadfile = GetUploadFilename(item.local, DateTime.Now);

					if (File.Exists(uploadfile))
					{
						var remotefile = GetRemoteFileName(item.remote, DateTime.Now);

						// just copy the file
						try
						{
							LogDebugMessage($"Realtime[{cycle}]: Copying extra file {uploadfile} to {remotefile}");
							if (item.process)
							{
								LogDebugMessage($"Realtime[{cycle}]: Processing extra file {uploadfile}");
								ProcessTemplateFile(uploadfile, remotefile, false, item.UTF8);
							}
							else
							{
								File.Copy(uploadfile, remotefile, true);
							}
						}
						catch (Exception ex)
						{
							LogDebugMessage($"Realtime[{cycle}]: Error copying extra realtime file - {uploadfile}: {ex.Message}");
						}
					}
					else
					{
						LogWarningMessage($"Realtime[{cycle}]: Extra realtime web file not found - {uploadfile}");
					}
				}
			}
		}

		private readonly object tokenParserLockObj = new();

		public void TokenParserOnToken(string strToken, ref string strReplacement)
		{
			lock (tokenParserLockObj)
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

				strReplacement = WebTags.GetWebTagText(webTag, tagParams);
			}
		}

		private static List<string> ParseParams(string line)
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
						parts.Add(line[start..i]);
						start = -1;
					}
				}
				else if (line[i] == '"')
				{
					if (start != -1)
					{
						parts.Add(line[start..i]);
						start = -1;
					}
					insideQuotes = !insideQuotes;
				}
				else if (line[i] == '=')
				{
					if (!insideQuotes && start != -1)
					{
						parts.Add(line.Substring(start, (i - start) + 1));
						start = -1;
					}
				}
				else
				{
					if (start == -1)
						start = i;
				}
			}

			if (start != -1)
				parts.Add(line[start..]);

			return parts;
		}

		public string DecimalSeparator { get; set; }

		private void CleanUpHashFiles()
		{
			foreach (var file in Directory.EnumerateFiles(AppDir, "hash_md5_*.txt"))
			{
				if (!file.EndsWith(Build + ".txt"))
				{
					File.Delete(file);
				}
			}
		}

		internal void DoMoonPhase()
		{
			DateTime now = DateTime.Now;
			double[] moonriseset = MoonriseMoonset.MoonRise(now.Year, now.Month, now.Day, TimeZoneInfo.Local.GetUtcOffset(now).TotalHours, (double) Latitude, (double) Longitude);
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
				var ret = MoonriseMoonset.CreateMoonImage(MoonPhaseAngle, (double) Latitude, MoonImage.Size, MoonImage.Transparent);

				if (ret)
				{
					// set a flag to show file is ready for FTP
					MoonImage.ReadyToFtp = true;
					MoonImage.ReadyToCopy = true;
				}
			}
		}

		public double MoonAge { get; set; }

		public string MoonPhaseString { get; set; }

		public double MoonPhaseAngle { get; set; }

		public double MoonPercent { get; set; }

		public TimeSpan MoonSetTime { get; set; }

		public TimeSpan MoonRiseTime { get; set; }

		public MoonImageOptionsClass MoonImage { get; set; } = new();

		private void GetSunriseSunset(DateTime time, out DateTime sunrise, out DateTime sunset, out bool alwaysUp, out bool alwaysDown)
		{
			string rise = SunriseSunset.SunRise(time, TimeZoneInfo.Local.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);
			string set = SunriseSunset.SunSet(time, TimeZoneInfo.Local.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);

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
					int h = Convert.ToInt32(rise[..2]);
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
					int h = Convert.ToInt32(set[..2]);
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

		private void GetDawnDusk(DateTime time, out DateTime dawn, out DateTime dusk, out bool alwaysUp, out bool alwaysDown)
		{
			string dawnStr = SunriseSunset.CivilTwilightEnds(time, TimeZoneInfo.Local.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);
			string duskStr = SunriseSunset.CivilTwilightStarts(time, TimeZoneInfo.Local.GetUtcOffset(time).TotalHours, (double) Longitude, (double) Latitude);

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
					int h = Convert.ToInt32(dawnStr[..2]);
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
					int h = Convert.ToInt32(duskStr[..2]);
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

		internal DateTime SunSetTime;

		internal DateTime SunRiseTime;

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

		public bool IsDawn()
		{
			if (TwilightAlways)
			{
				return true;
			}
			if (TwilightNever)
			{
				return false;
			}
			// 'Normal' case where sun sets before midnight
			return (DateTime.Now >= Dawn) && (DateTime.Now < SunRiseTime);
		}

		public bool IsDusk()
		{
			if (TwilightAlways)
			{
				return true;
			}
			if (TwilightNever)
			{
				return false;
			}
			// 'Normal' case where sun sets before midnight
			return (DateTime.Now > SunSetTime) && (DateTime.Now <= Dusk);
		}

		public string RemoveOldDiagsFiles(string logType)
		{
			const int maxEntries = 12;

			var directory = "MXdiags" + DirectorySeparator;
			string filename = string.Empty;

			if (logType == "CMX")
			{
				try
				{
					List<string> fileEntries = new List<string>(Directory.GetFiles(directory).Where(f => regexLogFileName().Match(f).Success));

					fileEntries.Sort();

					while (fileEntries.Count >= maxEntries)
					{
						File.Delete(fileEntries[0]);
						fileEntries.RemoveAt(0);
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "Error removing old MXdiags files");
				}

				filename = $"{directory}{DateTime.Now:yyyyMMdd-HHmmss}.txt";
			}
			else if (logType == "FTP")
			{
				try
				{
					List<string> fileEntries = new List<string>(Directory.GetFiles(directory).Where(f => regexFtpLogFileName().Match(f).Success));

					fileEntries.Sort();

					while (fileEntries.Count >= maxEntries)
					{
						File.Delete(fileEntries[0]);
						fileEntries.RemoveAt(0);
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "Error removing old FTP log files");
				}

				filename = $"{directory}FTP-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
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
				LogMessage("Rotating log file, new log file will be: " + loggingfile.Split(DirectorySeparator)[^1]);
				TextWriterTraceListener myTextListener = new TextWriterTraceListener(loggingfile, "MXlog");
				Trace.Listeners.Remove("MXlog");
				Trace.Listeners.Add(myTextListener);
				LogMessage("Rotated log file, old log file was: " + oldfile.Split(DirectorySeparator)[^1]);
			}
		}

		private void ReadIniFile()
		{
			var DavisBaudRates = new List<int> { 1200, 2400, 4800, 9600, 14400, 19200 };
			ImetOptions.BaudRates = [19200, 115200];
			var rewriteRequired = false; // Do we need to re-save the ini file after migration processing or resetting options?
			var recreateRequired = false; // Required to encrypt the credentials the first time

			LogMessage("Reading Cumulus.ini file");

			IniFile ini = new IniFile("Cumulus.ini");

			// check for Cumulus 1 [FTP Site] and correct it
			if (ini.ValueExists("FTP Site", "Port"))
			{
				LogMessage("Cumulus.ini: Changing old [FTP Site] to [FTP site]");
				var contents = File.ReadAllText("Cumulus.ini");
				contents = contents.Replace("[FTP Site]", "[FTP site]");
				File.WriteAllText("Cumulus.ini", contents);
				ini.Refresh();
			}

			ProgramOptions.EnableAccessibility = ini.GetValue("Program", "EnableAccessibility", false);

			ProgramOptions.StartupPingHost = ini.GetValue("Program", "StartupPingHost", string.Empty);

			ProgramOptions.StartupPingEscapeTime = ini.GetValue("Program", "StartupPingEscapeTime", 999, 0);
			ProgramOptions.StartupDelaySecs = ini.GetValue("Program", "StartupDelaySecs", 0, 0);
			ProgramOptions.StartupDelayMaxUptime = ini.GetValue("Program", "StartupDelayMaxUptime", 300, 0);

			ProgramOptions.StartupTask = ini.GetValue("Program", "StartupTask", string.Empty);
			ProgramOptions.StartupTaskParams = ini.GetValue("Program", "StartupTaskParams", string.Empty);
			ProgramOptions.StartupTaskWait = ini.GetValue("Program", "StartupTaskWait", false);

			ProgramOptions.ShutdownTask = ini.GetValue("Program", "ShutdownTask", string.Empty);
			ProgramOptions.ShutdownTaskParams = ini.GetValue("Program", "ShutdownTaskParams", string.Empty);

			ProgramOptions.DataStoppedExit = ini.GetValue("Program", "DataStoppedExit", false);
			ProgramOptions.DataStoppedMins = ini.GetValue("Program", "DataStoppedMins", 10, 0);
			ProgramOptions.Culture.RemoveSpaceFromDateSeparator = ini.GetValue("Culture", "RemoveSpaceFromDateSeparator", false);
			// if the culture names match, then we apply the new date separator if change is enabled and it contains a space
			if (ProgramOptions.Culture.RemoveSpaceFromDateSeparator && CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator.Contains(' '))
			{
				// change the date separator
				var dateSep = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator.Replace(" ", string.Empty);
				var shortDate = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(" ", string.Empty);

				CultureInfo newCulture = (CultureInfo) Thread.CurrentThread.CurrentCulture.Clone();
				newCulture.DateTimeFormat.DateSeparator = dateSep;
				newCulture.DateTimeFormat.ShortDatePattern = shortDate;

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

			ProgramOptions.EncryptedCreds = ini.GetValue("Program", "EncryptedCreds", false);

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
			ErrorListLoggingLevel = (MxLogLevel) ini.GetValue("Program", "ErrorListLoggingLevel", (int) MxLogLevel.Warning);

			ProgramOptions.SecureSettings = ini.GetValue("Program", "SecureSettings", false);
			ProgramOptions.SettingsUsername = ini.GetValue("Program", "SettingsUsername", string.Empty);
			ProgramOptions.SettingsPassword = ini.GetValue("Program", "SettingsPassword", string.Empty);

			ComportName = ini.GetValue("Station", "ComportName", DefaultComportName);

			StationType = ini.GetValue("Station", "Type", -1);
			StationModel = ini.GetValue("Station", "Model", string.Empty);
			Manufacturer = GetStationManufacturer(StationType);

			FineOffsetStation = (StationType == StationTypes.FineOffset || StationType == StationTypes.FineOffsetSolar);
			DavisStation = (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2);

			// Davis Options
			DavisOptions.UseLoop2 = ini.GetValue("Station", "UseDavisLoop2", true);
			DavisOptions.ReadReceptionStats = ini.GetValue("Station", "DavisReadReceptionStats", true);
			DavisOptions.SetLoggerInterval = ini.GetValue("Station", "DavisSetLoggerInterval", false);
			DavisOptions.InitWaitTime = ini.GetValue("Station", "DavisInitWaitTime", 2000, 0);
			DavisOptions.IPResponseTime = ini.GetValue("Station", "DavisIPResponseTime", 500, 0);
			DavisOptions.IncrementPressureDP = ini.GetValue("Station", "DavisIncrementPressureDP", false);
			if (StationType == StationTypes.VantagePro && DavisOptions.UseLoop2)
			{
				LogMessage("Cumulus.ini: Disabling LOOP2 for old VP station");
				DavisOptions.UseLoop2 = false;
				ini.SetValue("Station", "UseDavisLoop2", DavisOptions.UseLoop2);
				rewriteRequired = true;
			}
			DavisOptions.BaudRate = ini.GetValue("Station", "DavisBaudRate", 19200, 1200, 19200);
			// Check we have a valid value
			if (!DavisBaudRates.Contains(DavisOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Cumulus.ini: Error, the value for DavisBaudRate in the ini file " + DavisOptions.BaudRate + " is not valid, using default 19200.");
				DavisOptions.BaudRate = 19200;
				ini.SetValue("Station", "DavisBaudRate", DavisOptions.BaudRate);
				rewriteRequired = true;
			}
			DavisOptions.ForceVPBarUpdate = ini.GetValue("Station", "ForceVPBarUpdate", false);
			DavisOptions.RainGaugeType = ini.GetValue("Station", "VPrainGaugeType", -1);
			if (DavisOptions.RainGaugeType > 3)
			{
				LogMessage("Cumulus.ini: Invalid Davis rain gauge type, defaulting to -1");
				DavisOptions.RainGaugeType = -1;
				ini.SetValue("Station", "VPrainGaugeType", DavisOptions.RainGaugeType);
				rewriteRequired = true;
			}
			DavisOptions.ConnectionType = ini.GetValue("Station", "VP2ConnectionType", 0, 0, 2);
			if (DavisOptions.ConnectionType == 1)
			{
				DavisOptions.ConnectionType = 2;
				ini.SetValue("Station", "VP2ConnectionType", DavisOptions.ConnectionType);
				rewriteRequired = true;
			}
			DavisOptions.TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222, 1, 65535);
			DavisOptions.IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");

			WeatherFlowOptions.WFDeviceId = ini.GetValue("Station", "WeatherFlowDeviceId", 0);
			WeatherFlowOptions.WFTcpPort = ini.GetValue("Station", "WeatherFlowTcpPort", 50222, 1, 65535);
			WeatherFlowOptions.WFToken = ini.GetValue("Station", "WeatherFlowToken", "api token");
			WeatherFlowOptions.WFDaysHist = ini.GetValue("Station", "WeatherFlowDaysHist", 0, 0);

			DavisOptions.PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0, 0);

			Latitude = ini.GetValue("Station", "Latitude", (decimal) 0.0);
			if (Latitude > 90 || Latitude < -90)
			{
				Latitude = 0;
				LogErrorMessage($"Cumulus.ini: Error, invalid latitude value [{Latitude}], defaulting to zero.");
				ini.SetValue("Station", "Latitude", Latitude);
				rewriteRequired = true;
			}
			Longitude = ini.GetValue("Station", "Longitude", (decimal) 0.0);
			if (Longitude > 180 || Longitude < -180)
			{
				Longitude = 0;
				LogErrorMessage($"Cumulus.ini: Error, invalid longitude value [{Longitude}], defaulting to zero.");
				ini.SetValue("Station", "Longitude", Longitude);
				rewriteRequired = true;
			}

			LatTxt = ini.GetValue("Station", "LatTxt", string.Empty);
			LatTxt = LatTxt.Replace(" ", "&nbsp;");
			LatTxt = LatTxt.Replace("°", "&#39;");
			LonTxt = ini.GetValue("Station", "LonTxt", string.Empty);
			LonTxt = LonTxt.Replace(" ", "&nbsp;");
			LonTxt = LonTxt.Replace("°", "&#39;");

			Altitude = ini.GetValue("Station", "Altitude", 0.0);
			AltitudeInFeet = ini.GetValue("Station", "AltitudeInFeet", false);

			StationOptions.Humidity98Fix = ini.GetValue("Station", "Humidity98Fix", false);
			StationOptions.CalcuateAverageWindSpeed = ini.GetValue("Station", "Wind10MinAverage", false);
			StationOptions.UseSpeedForAvgCalc = ini.GetValue("Station", "UseSpeedForAvgCalc", false);
			StationOptions.UseSpeedForLatest = ini.GetValue("Station", "UseSpeedForLatest", false);
			StationOptions.UseRainForIsRaining = ini.GetValue("Station", "UseRainForIsRaining", 1, 0, 2);  // 0=station, 1=rain sensor, 2=haptic sensor
			StationOptions.LeafWetnessIsRainingIdx = ini.GetValue("Station", "LeafWetnessIsRainingIdx", -1);
			StationOptions.LeafWetnessIsRainingThrsh = ini.GetValue("Station", "LeafWetnessIsRainingVal", 0.0, 0);

			StationOptions.AvgBearingMinutes = ini.GetValue("Station", "AvgBearingMinutes", 10, 1, 120);

			AvgBearingTime = new TimeSpan(StationOptions.AvgBearingMinutes / 60, StationOptions.AvgBearingMinutes % 60, 0);

			StationOptions.AvgSpeedMinutes = ini.GetValue("Station", "AvgSpeedMinutes", 10, 1, 120);

			AvgSpeedTime = new TimeSpan(StationOptions.AvgSpeedMinutes / 60, StationOptions.AvgSpeedMinutes % 60, 0);

			LogMessage("AvgSpdMins=" + StationOptions.AvgSpeedMinutes + " AvgSpdTime=" + AvgSpeedTime.ToString());

			StationOptions.PeakGustMinutes = ini.GetValue("Station", "PeakGustMinutes", 10, 1, 120);

			PeakGustTime = new TimeSpan(StationOptions.PeakGustMinutes / 60, StationOptions.PeakGustMinutes % 60, 0);

			StationOptions.NoSensorCheck = ini.GetValue("Station", "NoSensorCheck", false);

			StationOptions.CalculatedDP = ini.GetValue("Station", "CalculatedDP", false);
			StationOptions.CalculatedWC = ini.GetValue("Station", "CalculatedWC", false);
			StationOptions.CalculatedET = ini.GetValue("Station", "CalculatedET", false);
			StationOptions.CalculateSLP = ini.GetValue("Station", "CalculatedSLP", false);
			RolloverHour = ini.GetValue("Station", "RolloverHour", 0);
			Use10amInSummer = ini.GetValue("Station", "Use10amInSummer", true);
			//ConfirmClose = ini.GetValue("Station", "ConfirmClose", false)
			//CloseOnSuspend = ini.GetValue("Station", "CloseOnSuspend", false)
			//RestartIfUnplugged = ini.GetValue("Station", "RestartIfUnplugged", false)
			//RestartIfDataStops = ini.GetValue("Station", "RestartIfDataStops", false)
			StationOptions.SyncTime = ini.GetValue("Station", "SyncDavisClock", false);
			StationOptions.ClockSettingHour = ini.GetValue("Station", "ClockSettingHour", 4, 0, 23);
			StationOptions.WS2300IgnoreStationClock = ini.GetValue("Station", "WS2300IgnoreStationClock", false);
			StationOptions.LogExtraSensors = ini.GetValue("Station", "LogExtraSensors", false);
			ReportDataStoppedErrors = ini.GetValue("Station", "ReportDataStoppedErrors", true);
			ReportLostSensorContact = ini.GetValue("Station", "ReportLostSensorContact", true);
			ErrorLogSpikeRemoval = ini.GetValue("Station", "ErrorLogSpikeRemoval", true);
			// this is now an index
			DataLogInterval = ini.GetValue("Station", "DataLogInterval", 2, 0, 5);

			FineOffsetOptions.SyncReads = ini.GetValue("Station", "SyncFOReads", true);
			FineOffsetOptions.ReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3, 0);
			FineOffsetOptions.ReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150, 0);
			FineOffsetOptions.SetLoggerInterval = ini.GetValue("Station", "FineOffsetSetLoggerInterval", false);
			FineOffsetOptions.VendorID = ini.GetValue("Station", "VendorID", -1, -1);
			FineOffsetOptions.ProductID = ini.GetValue("Station", "ProductID", -1, -1);


			Units.Wind = ini.GetValue("Station", "WindUnit", 2, 0, 3);
			Units.Press = ini.GetValue("Station", "PressureUnit", 1, 0, 3);
			Units.Rain = ini.GetValue("Station", "RainUnit", 0, 0, 1);
			Units.Temp = ini.GetValue("Station", "TempUnit", 0, 0, 1);
			Units.SnowDepth = ini.GetValue("Station", "SnowDepthUnit", 0, 0, 1);
			Units.LaserDistance = ini.GetValue("Station", "LaserDistancehUnit", Units.SnowDepth, 0, 1);

			StationOptions.RoundWindSpeed = ini.GetValue("Station", "RoundWindSpeed", false);
			StationOptions.PrimaryAqSensor = ini.GetValue("Station", "PrimaryAqSensor", -1, -1);


			// Unit decimals
			RainDPlaces = RainDPlaceDefaults[Units.Rain];
			TempDPlaces = TempDPlaceDefaults[Units.Temp];
			PressDPlaces = PressDPlaceDefaults[Units.Press];
			WindDPlaces = StationOptions.RoundWindSpeed ? 0 : WindDPlaceDefaults[Units.Wind];
			WindAvgDPlaces = WindDPlaces;
			AirQualityDPlaces = 1;
			SnowDPlaces = Units.SnowDepth == 0 ? 1 : 2;
			SnowFormat = "F" + SnowDPlaces;


			// Unit decimal overrides
			WindDPlaces = ini.GetValue("Station", "WindSpeedDecimals", WindDPlaces, 0);
			WindAvgDPlaces = ini.GetValue("Station", "WindSpeedAvgDecimals", WindAvgDPlaces, 0);
			WindRunDPlaces = ini.GetValue("Station", "WindRunDecimals", WindRunDPlaces, 0);
			SunshineDPlaces = ini.GetValue("Station", "SunshineHrsDecimals", 1, 0);
			PressDPlaces = ini.GetValue("Station", "PressDecimals", PressDPlaces, 0);
			RainDPlaces = ini.GetValue("Station", "RainDecimals", RainDPlaces, 0);
			TempDPlaces = ini.GetValue("Station", "TempDecimals", TempDPlaces, 0);
			UVDPlaces = ini.GetValue("Station", "UVDecimals", UVDPlaces, 0);
			AirQualityDPlaces = ini.GetValue("Station", "AirQualityDecimals", AirQualityDPlaces, 0);

			if ((StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2) && DavisOptions.IncrementPressureDP)
			{
				++PressDPlaces;
			}


			LocationName = ini.GetValue("Station", "LocName", string.Empty);
			LocationDesc = ini.GetValue("Station", "LocDesc", string.Empty);

			YTDrain = ini.GetValue("Station", "YTDrain", 0.0, 0.0);
			YTDrainyear = ini.GetValue("Station", "YTDrainyear", 0, 0);

			EwOptions.Interval = ini.GetValue("Station", "EWInterval", 1.0, 0.01);
			EwOptions.Filename = ini.GetValue("Station", "EWFile", string.Empty);
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
			if (Spike.TempDiff < 999)
			{
				Spike.TempDiff = ConvertUnits.TempCToUser(Spike.TempDiff);
			}
			if (Spike.PressDiff < 999)
			{
				Spike.PressDiff = ConvertUnits.PressMBToUser(Spike.PressDiff);
			}
			if (Spike.GustDiff < 999)
			{
				Spike.GustDiff = ConvertUnits.WindMSToUser(Spike.GustDiff);
			}
			if (Spike.WindDiff < 999)
			{
				Spike.WindDiff = ConvertUnits.WindMSToUser(Spike.WindDiff);
			}
			if (Spike.MaxRainRate < 999)
			{
				Spike.MaxRainRate = ConvertUnits.RainMMToUser(Spike.MaxRainRate);
			}
			if (Spike.MaxHourlyRain < 999)
			{
				Spike.MaxHourlyRain = ConvertUnits.RainMMToUser(Spike.MaxHourlyRain);
			}
			if (Spike.InTempDiff < 999)
			{
				Spike.InTempDiff = ConvertUnits.TempCToUser(Spike.InTempDiff);
			}

			LCMaxWind = ini.GetValue("Station", "LCMaxWind", 9999);

			if (ini.ValueExists("Station", "StartDate"))
			{
				var RecordsBeganDate = ini.GetValue("Station", "StartDate", DateTime.Now.ToLongDateString());
				try
				{
					RecordsBeganDateTime = DateTime.Parse(RecordsBeganDate, CultureInfo.CurrentCulture);
					LogMessage($"Cumulus.ini: Changing old StartDate [{RecordsBeganDate}] to StartDateIso [{RecordsBeganDateTime:yyyy-MM-dd}]");
					ini.DeleteValue("Station", "StartDate");
					ini.SetValue("Station", "StartDateIso", RecordsBeganDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
					rewriteRequired = true;
				}
				catch (Exception ex)
				{
					LogErrorMessage($"Cumulus.ini: Error parsing the RecordsBegan date {RecordsBeganDate}: {ex.Message}");
				}
			}
			else
			{
				var RecordsBeganDate = ini.GetValue("Station", "StartDateIso", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
				RecordsBeganDateTime = DateTime.ParseExact(RecordsBeganDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
			}

			LogMessage($"Cumulus.ini: Start date Parsed: {RecordsBeganDateTime:yyyy-MM-dd}");

			ImetOptions.WaitTime = ini.GetValue("Station", "ImetWaitTime", 500, 0);
			ImetOptions.ReadDelay = ini.GetValue("Station", "ImetReadDelay", 500, 0);
			ImetOptions.UpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);
			ImetOptions.BaudRate = ini.GetValue("Station", "ImetBaudRate", 19200, 19200, 115200);
			// Check we have a valid value
			if (!ImetOptions.BaudRates.Contains(ImetOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Cumulus.ini: Error, the value for ImetOptions.ImetBaudRate " + ImetOptions.BaudRate + " is not valid, using default 19200.");
				ImetOptions.BaudRate = 19200;
				ini.SetValue("Station", "ImetBaudRate", ImetOptions.BaudRate);
				rewriteRequired = true;
			}

			StationOptions.UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
			UseCumulusForecast = ini.GetValue("Station", "UseCumulusForecast", false);
			HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
			StationOptions.UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
			//UseWindChillCutoff = ini.GetValue("Station", "UseWindChillCutoff", false)
			RecordSetTimeoutHrs = ini.GetValue("Station", "RecordSetTimeoutHrs", 24, 0);

			SnowDepthHour = ini.GetValue("Station", "SnowDepthHour", 9, 0, 23);
			SnowAutomated = ini.GetValue("Station", "SnowAutomated", 0, 0, 4);

			StationOptions.UseZeroBearing = ini.GetValue("Station", "UseZeroBearing", false);

			RainDayThreshold = ini.GetValue("Station", "RainDayThreshold", -1.0, -1.0);

			FCpressinMB = ini.GetValue("Station", "FCpressinMB", true);
			FClowpress = ini.GetValue("Station", "FClowpress", DEFAULTFCLOWPRESS);
			FChighpress = ini.GetValue("Station", "FChighpress", DEFAULTFCHIGHPRESS);
			FCPressureThreshold = ini.GetValue("Station", "FCPressureThreshold", -1.0, -1.0);

			RainSeasonStart = ini.GetValue("Station", "RainSeasonStart", 1, 1, 12);
			RainWeekStart = ini.GetValue("Station", "RainWeekStart", 1, 0, 1);
			ChillHourSeasonStart = ini.GetValue("Station", "ChillHourSeasonStart", Latitude >= 0 ? 10 : 4, 1, 12);
			ChillHourThreshold = ini.GetValue("Station", "ChillHourThreshold", Units.Temp == 0 ? 7 : 45);
			ChillHourBase = ini.GetValue("Station", "ChillHourBase", -99);

			RG11Enabled = ini.GetValue("Station", "RG11Enabled", false);
			RG11Port = ini.GetValue("Station", "RG11portName", DefaultComportName);
			RG11TBRmode = ini.GetValue("Station", "RG11TBRmode", false);
			RG11tipsize = ini.GetValue("Station", "RG11tipsize", 0.0, 0.0);
			RG11IgnoreFirst = ini.GetValue("Station", "RG11IgnoreFirst", false);
			RG11DTRmode = ini.GetValue("Station", "RG11DTRmode", true);

			RG11Enabled2 = ini.GetValue("Station", "RG11Enabled2", false);
			RG11Port2 = ini.GetValue("Station", "RG11port2Name", DefaultComportName);
			RG11TBRmode2 = ini.GetValue("Station", "RG11TBRmode2", false);
			RG11tipsize2 = ini.GetValue("Station", "RG11tipsize2", 0.0, 0.0);
			RG11IgnoreFirst2 = ini.GetValue("Station", "RG11IgnoreFirst2", false);
			RG11DTRmode2 = ini.GetValue("Station", "RG11DTRmode2", true);

			if (FCPressureThreshold < 0)
			{
				FCPressureThreshold = Units.Press switch
				{
					0 => 0.1,
					1 => 0.1,
					2 => 0.00295333727,
					3 => 0.01,
					_ => 0
				};
			}

			WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0, 0);
			WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1, 0);

			WxnowComment = ini.GetValue("Station", "WxnowComment.txt", string.Empty);

			// WeatherLink Live device settings
			WllApiKey = ini.GetValue("WLL", "WLv2ApiKey", string.Empty);
			WllApiSecret = ini.GetValue("WLL", "WLv2ApiSecret", string.Empty);
			WllStationId = ini.GetValue("WLL", "WLStationId", -1, -1);
			WllStationUuid = ini.GetValue("WLL", "WLStationUuid", "");
			WllTriggerDataStoppedOnBroadcast = ini.GetValue("WLL", "DataStoppedOnBroadcast", true);
			WLLAutoUpdateIpAddress = ini.GetValue("WLL", "AutoUpdateIpAddress", true);
			WllBroadcastDuration = ini.GetValue("WLL", "BroadcastDuration", WllBroadcastDuration);
			WllBroadcastPort = ini.GetValue("WLL", "BroadcastPort", WllBroadcastPort);
			WllPrimaryRain = ini.GetValue("WLL", "PrimaryRainTxId", 1, 1, 8);
			WllPrimaryTempHum = ini.GetValue("WLL", "PrimaryTempHumTxId", 1, 1, 8);
			WllPrimaryWind = ini.GetValue("WLL", "PrimaryWindTxId", 1, 1, 8);
			WllPrimaryRain = ini.GetValue("WLL", "PrimaryRainTxId", 1, 1, 8);
			WllPrimarySolar = ini.GetValue("WLL", "PrimarySolarTxId", 0, 0, 8);
			WllPrimaryUV = ini.GetValue("WLL", "PrimaryUvTxId", 0, 0, 8);
			WllExtraSoilTempTx1 = ini.GetValue("WLL", "ExtraSoilTempTxId1", 0, 0, 8);
			WllExtraSoilTempIdx1 = ini.GetValue("WLL", "ExtraSoilTempIdx1", 1, 1, 4);
			WllExtraSoilTempTx2 = ini.GetValue("WLL", "ExtraSoilTempTxId2", 0, 0, 8);
			WllExtraSoilTempIdx2 = ini.GetValue("WLL", "ExtraSoilTempIdx2", 2, 1, 4);
			WllExtraSoilTempTx3 = ini.GetValue("WLL", "ExtraSoilTempTxId3", 0, 0, 8);
			WllExtraSoilTempIdx3 = ini.GetValue("WLL", "ExtraSoilTempIdx3", 3, 1, 4);
			WllExtraSoilTempTx4 = ini.GetValue("WLL", "ExtraSoilTempTxId4", 0, 0, 8);
			WllExtraSoilTempIdx4 = ini.GetValue("WLL", "ExtraSoilTempIdx4", 4, 1, 4);
			WllExtraSoilMoistureTx1 = ini.GetValue("WLL", "ExtraSoilMoistureTxId1", 0, 0, 8);
			WllExtraSoilMoistureIdx1 = ini.GetValue("WLL", "ExtraSoilMoistureIdx1", 1, 1, 4);
			WllExtraSoilMoistureTx2 = ini.GetValue("WLL", "ExtraSoilMoistureTxId2", 0, 0, 8);
			WllExtraSoilMoistureIdx2 = ini.GetValue("WLL", "ExtraSoilMoistureIdx2", 2, 1, 4);
			WllExtraSoilMoistureTx3 = ini.GetValue("WLL", "ExtraSoilMoistureTxId3", 0, 0, 8);
			WllExtraSoilMoistureIdx3 = ini.GetValue("WLL", "ExtraSoilMoistureIdx3", 3, 1, 4);
			WllExtraSoilMoistureTx4 = ini.GetValue("WLL", "ExtraSoilMoistureTxId4", 0, 0, 8);
			WllExtraSoilMoistureIdx4 = ini.GetValue("WLL", "ExtraSoilMoistureIdx4", 4, 1, 4);
			WllExtraLeafTx1 = ini.GetValue("WLL", "ExtraLeafTxId1", 0, 0, 8);
			WllExtraLeafIdx1 = ini.GetValue("WLL", "ExtraLeafIdx1", 1, 1, 2);
			WllExtraLeafTx2 = ini.GetValue("WLL", "ExtraLeafTxId2", 0, 0, 8);
			WllExtraLeafIdx2 = ini.GetValue("WLL", "ExtraLeafIdx2", 2, 1, 2);
			for (int i = 1; i <= 8; i++)
			{
				WllExtraTempTx[i] = ini.GetValue("WLL", "ExtraTempTxId" + i, 0, 0, 8);
				WllExtraHumTx[i] = ini.GetValue("WLL", "ExtraHumOnTxId" + i, false);
			}

			// GW1000 settings
			Gw1000IpAddress = ini.GetValue("GW1000", "IPAddress", "0.0.0.0");
			Gw1000MacAddress = ini.GetValue("GW1000", "MACAddress", string.Empty).ToUpper();
			Gw1000AutoUpdateIpAddress = ini.GetValue("GW1000", "AutoUpdateIpAddress", true);
			Gw1000PrimaryTHSensor = ini.GetValue("GW1000", "PrimaryTHSensor", 0, 0, 99);  // 0=default, 1-8=extra t/h sensor number, 99=use indoor sensor
			Gw1000PrimaryRainSensor = ini.GetValue("GW1000", "PrimaryRainSensor", 0, 0, 1); //0=main station (tipping bucket) 1=piezo
			EcowittIsRainingUsePiezo = ini.GetValue("GW1000", "UsePiezoIsRaining", false);
			EcowittExtraEnabled = ini.GetValue("GW1000", "ExtraSensorDataEnabled", false);
			EcowittCloudExtraEnabled = ini.GetValue("GW1000", "ExtraCloudSensorDataEnabled", false);
			EcowittSetCustomServer = ini.GetValue("GW1000", "SetCustomServer", false);
			EcowittGatewayAddr = ini.GetValue("GW1000", "EcowittGwAddr", "0.0.0.0");
			var localIp = Utils.GetIpWithDefaultGateway();
			EcowittLocalAddr = ini.GetValue("GW1000", "EcowittLocalAddr", localIp.ToString());
			EcowittCustomInterval = ini.GetValue("GW1000", "EcowittCustomInterval", 16, 1);

			EcowittHttpPassword = ini.GetValue("GW1000", "HttpPassword", "");

			EcowittExtraSetCustomServer = ini.GetValue("GW1000", "ExtraSetCustomServer", false);
			EcowittExtraGatewayAddr = ini.GetValue("GW1000", "EcowittExtraGwAddr", "0.0.0.0");
			EcowittExtraLocalAddr = ini.GetValue("GW1000", "EcowittExtraLocalAddr", localIp.ToString());
			EcowittExtraCustomInterval = ini.GetValue("GW1000", "EcowittExtraCustomInterval", 16, 1);
			// api
			EcowittApplicationKey = ini.GetValue("GW1000", "EcowittAppKey", string.Empty);
			EcowittUserApiKey = ini.GetValue("GW1000", "EcowittUserKey", string.Empty);
			EcowittMacAddress = ini.GetValue("GW1000", "EcowittMacAddress", string.Empty).ToUpper();
			// For GW1000 stations, the Ecowitt MAC must be the same as the device MAC
			if (StationType == 12)
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
				EcowittForwarders[i] = ini.GetValue("GW1000", "Forwarder" + i, string.Empty);
			}
			EcowittExtraUseMainForwarders = ini.GetValue("GW1000", "ExtraUseMainForwarders", false);
			// extra forwarders
			for (int i = 0; i < EcowittExtraForwarders.Length; i++)
			{
				EcowittExtraForwarders[i] = ini.GetValue("GW1000", "ExtraForwarder" + i, string.Empty);
			}

			// Ambient settings
			AmbientExtraEnabled = ini.GetValue("Ambient", "ExtraSensorDataEnabled", false);
			AmbientExtraUseSolar = ini.GetValue("Ambient", "ExtraSensorUseSolar", true);
			AmbientExtraUseUv = ini.GetValue("Ambient", "ExtraSensorUseUv", true);
			AmbientExtraUseTempHum = ini.GetValue("Ambient", "ExtraSensorUseTempHum", true);
			AmbientExtraUseSoilTemp = ini.GetValue("Ambient", "ExtraSensorUseSoilTemp", true);
			AmbientExtraUseSoilMoist = ini.GetValue("Ambient", "ExtraSensorUseSoilMoist", true);
			AmbientExtraUseAQI = ini.GetValue("Ambient", "ExtraSensorUseAQI", true);
			AmbientExtraUseCo2 = ini.GetValue("Ambient", "ExtraSensorUseCo2", true);
			AmbientExtraUseLightning = ini.GetValue("Ambient", "ExtraSensorUseLightning", true);
			AmbientExtraUseLeak = ini.GetValue("Ambient", "ExtraSensorUseLeak", true);

			// JSON station options
			JsonStationOptions.Connectiontype = ini.GetValue("JsonStation", "ConnectionType", 1, 0, 2);
			JsonStationOptions.SourceFile = ini.GetValue("JsonStation", "SourceFile", string.Empty);
			JsonStationOptions.FileReadDelay = ini.GetValue("JsonStation", "FileDelay", 200, 0);
			JsonStationOptions.MqttServer = ini.GetValue("JsonStation", "MqttServer", string.Empty);
			JsonStationOptions.MqttPort = ini.GetValue("JsonStation", "MqttServerPort", 1883, 1, 65353);
			JsonStationOptions.MqttUsername = ini.GetValue("JsonStation", "MqttUsername", string.Empty);
			JsonStationOptions.MqttPassword = ini.GetValue("JsonStation", "MqttPassword", string.Empty);
			JsonStationOptions.MqttUseTls = ini.GetValue("JsonStation", "MqttUseTls", false);
			JsonStationOptions.MqttTopic = ini.GetValue("JsonStation", "MqttTopic", string.Empty);
			// JSON station Extra Sensors
			JsonExtraStationOptions.ExtraSensorsEnabled = ini.GetValue("JsonExtraStation", "ExtraSensorDataEnabled", false);
			JsonExtraStationOptions.Connectiontype = ini.GetValue("JsonExtraStation", "ConnectionType", 1, 0, 2);
			JsonExtraStationOptions.SourceFile = ini.GetValue("JsonExtraStation", "SourceFile", string.Empty);
			JsonExtraStationOptions.FileReadDelay = ini.GetValue("JsonExtraStation", "FileDelay", 200, 0);
			JsonExtraStationOptions.MqttServer = ini.GetValue("JsonExtraStation", "MqttServer", string.Empty);
			JsonExtraStationOptions.MqttPort = ini.GetValue("JsonExtraStation", "MqttServerPort", 1883, 1, 65353);
			JsonExtraStationOptions.MqttUsername = ini.GetValue("JsonExtraStation", "MqttUsername", string.Empty);
			JsonExtraStationOptions.MqttPassword = ini.GetValue("JsonExtraStation", "MqttPassword", string.Empty);
			JsonExtraStationOptions.MqttUseTls = ini.GetValue("JsonExtraStation", "MqttUseTls", false);
			JsonExtraStationOptions.MqttTopic = ini.GetValue("JsonExtraStation", "MqttTopic", string.Empty);

			// Extra Sensor Options
			if (ini.ValueExists("GW1000", "ExtraSensorUseSolar"))
			{
				ExtraSensorUseSolar = ini.GetValue("GW1000", "ExtraSensorUseSolar", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseSolar");
				ini.SetValue("ExtraSensors", "ExtraSensorUseSolar", ExtraSensorUseSolar);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseSolar = ini.GetValue("ExtraSensors", "ExtraSensorUseSolar", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseUv"))
			{
				ExtraSensorUseUv = ini.GetValue("GW1000", "ExtraSensorUseUv", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseUv");
				ini.SetValue("ExtraSensors", "ExtraSensorUseUv", ExtraSensorUseUv);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseUv = ini.GetValue("ExtraSensors", "ExtraSensorUseUv", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseTempHum"))
			{
				ExtraSensorUseTempHum = ini.GetValue("GW1000", "ExtraSensorUseTempHum", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseTempHum");
				ini.SetValue("ExtraSensors", "ExtraSensorUseTempHum", ExtraSensorUseTempHum);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseTempHum = ini.GetValue("ExtraSensors", "ExtraSensorUseTempHum", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseSoilTemp"))
			{
				ExtraSensorUseSoilTemp = ini.GetValue("GW1000", "ExtraSensorUseSoilTemp", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseSoilTemp");
				ini.SetValue("ExtraSensors", "ExtraSensorUseSoilTemp", ExtraSensorUseSoilTemp);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseSoilTemp = ini.GetValue("ExtraSensors", "ExtraSensorUseSoilTemp", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseSoilMoist"))
			{
				ExtraSensorUseSoilMoist = ini.GetValue("GW1000", "ExtraSensorUseSoilMoist", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseSoilMoist");
				ini.SetValue("ExtraSensors", "ExtraSensorUseSoilMoist", ExtraSensorUseSoilMoist);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseSoilMoist = ini.GetValue("ExtraSensors", "ExtraSensorUseSoilMoist", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseLeafWet"))
			{
				ExtraSensorUseLeafWet = ini.GetValue("GW1000", "ExtraSensorUseLeafWet", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseLeafWet");
				ini.SetValue("ExtraSensors", "ExtraSensorUseLeafWet", ExtraSensorUseLeafWet);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseLeafWet = ini.GetValue("ExtraSensors", "ExtraSensorUseLeafWet", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseUserTemp"))
			{
				ExtraSensorUseUserTemp = ini.GetValue("GW1000", "ExtraSensorUseUserTemp", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseUserTemp");
				ini.SetValue("ExtraSensors", "ExtraSensorUseUserTemp", ExtraSensorUseUserTemp);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseUserTemp = ini.GetValue("ExtraSensors", "ExtraSensorUseUserTemp", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseAQI"))
			{
				ExtraSensorUseAQI = ini.GetValue("GW1000", "ExtraSensorUseAQI", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseAQI");
				ini.SetValue("ExtraSensors", "ExtraSensorUseAQI", ExtraSensorUseAQI);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseAQI = ini.GetValue("ExtraSensors", "ExtraSensorUseAQI", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseCo2"))
			{
				ExtraSensorUseCo2 = ini.GetValue("GW1000", "ExtraSensorUseCo2", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseCo2");
				ini.SetValue("ExtraSensors", "ExtraSensorUseCo2", ExtraSensorUseCo2);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseCo2 = ini.GetValue("ExtraSensors", "ExtraSensorUseCo2", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseLightning"))
			{
				ExtraSensorUseLightning = ini.GetValue("GW1000", "ExtraSensorUseLightning", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseLightning");
				ini.SetValue("ExtraSensors", "ExtraSensorUseLightning", ExtraSensorUseLightning);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseLightning = ini.GetValue("ExtraSensors", "ExtraSensorUseLightning", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseLeak"))
			{
				ExtraSensorUseLeak = ini.GetValue("GW1000", "ExtraSensorUseLeak", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseLeak");
				ini.SetValue("ExtraSensors", "ExtraSensorUseLeak", ExtraSensorUseLeak);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseLeak = ini.GetValue("ExtraSensors", "ExtraSensorUseLeak", true);
			}
			if (ini.ValueExists("GW1000", "ExtraSensorUseCamera"))
			{
				ExtraSensorUseCamera = ini.GetValue("GW1000", "ExtraSensorUseCamera", true);
				ini.DeleteValue("GW1000", "ExtraSensorUseCamera");
				ini.SetValue("ExtraSensors", "ExtraSensorUseCamera", ExtraSensorUseCamera);
				rewriteRequired = true;
			}
			else
			{
				ExtraSensorUseCamera = ini.GetValue("ExtraSensors", "ExtraSensorUseCamera", true);
			}
			ExtraSensorUseLaserDist = ini.GetValue("ExtraSensors", "ExtraSensorUseLaserDist", true);


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
				ini.DeleteValue("AirLink", "In-IsNode");
				ini.DeleteValue("AirLink", "Out-IsNode");
				ini.SetValue("AirLink", "IsWllNode", AirLinkIsNode);
				rewriteRequired = true;
			}
			AirLinkApiKey = ini.GetValue("AirLink", "WLv2ApiKey", string.Empty);
			AirLinkApiSecret = ini.GetValue("AirLink", "WLv2ApiSecret", string.Empty);
			AirLinkAutoUpdateIpAddress = ini.GetValue("AirLink", "AutoUpdateIpAddress", true);
			AirLinkInEnabled = ini.GetValue("AirLink", "In-Enabled", false);
			AirLinkInIPAddr = ini.GetValue("AirLink", "In-IPAddress", "0.0.0.0");
			AirLinkInStationId = ini.GetValue("AirLink", "In-WLStationId", -1, -1);
			if (AirLinkInStationId == -1 && AirLinkIsNode)
			{
				AirLinkInStationId = WllStationId;
				LogMessage("Cumulus.ini: No AirLinkInStationId supplied, but AirlinkIsNode, so using main station id");
				ini.SetValue("AirLink", "In-WLStationId", AirLinkInStationId);
				rewriteRequired = true;
			}
			AirLinkInHostName = ini.GetValue("AirLink", "In-Hostname", string.Empty);

			AirLinkOutEnabled = ini.GetValue("AirLink", "Out-Enabled", false);
			AirLinkOutIPAddr = ini.GetValue("AirLink", "Out-IPAddress", "0.0.0.0");
			AirLinkOutStationId = ini.GetValue("AirLink", "Out-WLStationId", -1, -1);
			if (AirLinkOutStationId == -1 && AirLinkIsNode)
			{
				AirLinkOutStationId = WllStationId;
				LogMessage("Cumulus.ini: No AirLinkOutStationId supplied, but AirlinkIsNode, so using main station id");
				ini.SetValue("AirLink", "Out-WLStationId", AirLinkOutStationId);
				rewriteRequired = true;
			}
			AirLinkOutHostName = ini.GetValue("AirLink", "Out-Hostname", string.Empty);

			airQualityIndex = ini.GetValue("AirLink", "AQIformula", 0);

			FtpOptions.Enabled = ini.GetValue("FTP site", "Enabled", true);
			FtpOptions.Hostname = ini.GetValue("FTP site", "Host", string.Empty);
			FtpOptions.Port = ini.GetValue("FTP site", "Port", 21, 1, 65535);
			FtpOptions.Username = ini.GetValue("FTP site", "Username", string.Empty);
			FtpOptions.Password = ini.GetValue("FTP site", "Password", string.Empty);
			FtpOptions.Directory = ini.GetValue("FTP site", "Directory", string.Empty);
			FtpOptions.FtpMode = (FtpProtocols) ini.GetValue("FTP site", "Sslftp", 0, 0, 3);
			if (FtpOptions.Enabled && FtpOptions.Hostname == string.Empty && FtpOptions.FtpMode != FtpProtocols.PHP)
			{
				LogMessage("Cumulus.ini: FTP enabled, but no hostname supplied, disabling FTP");
				FtpOptions.Enabled = false;
				ini.SetValue("FTP site", "Enabled", FtpOptions.Enabled);
				rewriteRequired = true;
			}

			FtpOptions.AutoDetect = ini.GetValue("FTP site", "ConnectionAutoDetect", false);
			FtpOptions.IgnoreCertErrors = ini.GetValue("FTP site", "IgnoreCertErrors", false);
			FtpOptions.ActiveMode = ini.GetValue("FTP site", "ActiveFTP", false);
			// BUILD 3092 - added alternate SFTP authentication options
			FtpOptions.SshAuthen = ini.GetValue("FTP site", "SshFtpAuthentication", "password");
			if (!sshAuthenticationVals.Contains(FtpOptions.SshAuthen))
			{
				FtpOptions.SshAuthen = "password";
				LogWarningMessage($"Cumulus.ini: Error, invalid SshFtpAuthentication value [{FtpOptions.SshAuthen}], defaulting to Password.");
				ini.SetValue("FTP site", "SshFtpAuthentication", FtpOptions.SshAuthen);
				rewriteRequired = true;
			}
			FtpOptions.SshPskFile = ini.GetValue("FTP site", "SshFtpPskFile", string.Empty);
			if (FtpOptions.SshPskFile.Length > 0 && (FtpOptions.SshAuthen == "psk" || FtpOptions.SshAuthen == "password_psk") && !File.Exists(FtpOptions.SshPskFile))
			{
				FtpOptions.SshPskFile = string.Empty;
				LogErrorMessage($"Cumulus.ini: Error, file name specified by SshFtpPskFile does not exist [{FtpOptions.SshPskFile}], removing it.");
				ini.SetValue("FTP site", "SshFtpPskFile", FtpOptions.SshPskFile);
				rewriteRequired = true;
			}
			FtpOptions.DisableEPSV = ini.GetValue("FTP site", "DisableEPSV", false);
			FtpOptions.DisableExplicit = ini.GetValue("FTP site", "DisableFtpsExplicit", false);
			FtpOptions.Logging = ini.GetValue("FTP site", "FTPlogging", false);
			FtpOptions.LoggingLevel = ini.GetValue("FTP site", "FTPloggingLevel", 2);
			RealtimeIntervalEnabled = ini.GetValue("FTP site", "EnableRealtime", false);
			FtpOptions.RealtimeEnabled = ini.GetValue("FTP site", "RealtimeFTPEnabled", false);

			// Local Copy Options
			FtpOptions.LocalCopyEnabled = ini.GetValue("FTP site", "EnableLocalCopy", false);
			FtpOptions.LocalCopyFolder = ini.GetValue("FTP site", "LocalCopyFolder", string.Empty);
			var sep1 = Path.DirectorySeparatorChar.ToString();
			var sep2 = Path.AltDirectorySeparatorChar.ToString();
			if (FtpOptions.LocalCopyFolder.Length > 1 &&
				!(FtpOptions.LocalCopyFolder.EndsWith(sep1) || FtpOptions.LocalCopyFolder.EndsWith(sep2))
				)
			{
				LogMessage("Cumulus.ini: Local copy folder does not end with a directory separator, adding it");
				FtpOptions.LocalCopyFolder += sep1;
				ini.SetValue("FTP site", "LocalCopyFolder", FtpOptions.LocalCopyFolder);
				rewriteRequired = true;
			}

			// PHP upload options
			FtpOptions.PhpUrl = ini.GetValue("FTP site", "PHP-URL", string.Empty);
			FtpOptions.PhpSecret = ini.GetValue("FTP site", "PHP-Secret", string.Empty);
			if (FtpOptions.PhpSecret == string.Empty)
				FtpOptions.PhpSecret = Guid.NewGuid().ToString();
			FtpOptions.PhpIgnoreCertErrors = ini.GetValue("FTP site", "PHP-IgnoreCertErrors", false);
			FtpOptions.MaxConcurrentUploads = ini.GetValue("FTP site", "MaxConcurrentUploads", 2, 1);
			FtpOptions.PhpUseGet = ini.GetValue("FTP site", "PHP-UseGet", true);
			FtpOptions.PhpUseBrotli = ini.GetValue("FTP site", "PHP-UseBrotli", false);

			if (FtpOptions.Enabled && FtpOptions.PhpUrl == string.Empty && FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogMessage("Cumulus.ini: PHP upload enabled but the target URL is missing, disabling uploads");
				FtpOptions.Enabled = false;
				ini.SetValue("FTP site", "Enabled", FtpOptions.Enabled);
				rewriteRequired = true;
			}

			MoonImage.Ftp = ini.GetValue("FTP site", "IncludeMoonImage", false);
			MoonImage.Copy = ini.GetValue("FTP site", "CopyMoonImage", false);


			RealtimeFiles[0].Create = ini.GetValue("FTP site", "RealtimeTxtCreate", false);
			RealtimeFiles[0].FTP = ini.GetValue("FTP site", "RealtimeTxtFTP", false);
			RealtimeFiles[0].Copy = ini.GetValue("FTP site", "RealtimeTxtCopy", false);
			RealtimeFiles[1].Create = ini.GetValue("FTP site", "RealtimeGaugesTxtCreate", false);
			RealtimeFiles[1].FTP = ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);
			RealtimeFiles[1].Copy = ini.GetValue("FTP site", "RealtimeGaugesTxtCopy", false);

			RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000, 1);

			WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);  // Deprecated, to be remove at some future date
																			// Have to allow for upgrade, set interval enabled to old WebAutoUpdate
			WebIntervalEnabled = ini.GetValue("FTP site", "IntervalEnabled", WebAutoUpdate);
			FtpOptions.IntervalEnabled = ini.GetValue("FTP site", "IntervalFtpEnabled", WebAutoUpdate);

			UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", DefaultWebUpdateInterval);
			if (UpdateInterval < 1)
			{
				LogMessage("Cumulus.ini: Update interval invalid, resetting to 1");
				UpdateInterval = 1;
				ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
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

			for (int i = 0; i < numextrafiles; i++)
			{
				ExtraFiles[i] = new CExtraFiles
				{
					local = ini.GetValue("FTP site", "ExtraLocal" + i, string.Empty),
					remote = ini.GetValue("FTP site", "ExtraRemote" + i, string.Empty),
					process = ini.GetValue("FTP site", "ExtraProcess" + i, false),
					binary = ini.GetValue("FTP site", "ExtraBinary" + i, false),
					realtime = ini.GetValue("FTP site", "ExtraRealtime" + i, false),
					FTP = ini.GetValue("FTP site", "ExtraFTP" + i, false),
					UTF8 = ini.GetValue("FTP site", "ExtraUTF" + i, false),
					endofday = ini.GetValue("FTP site", "ExtraEOD" + i, false),
					incrementalLogfile = ini.GetValue("FTP site", "ExtraIncLogFile" + i, false)
				};

				if (ExtraFiles[i].binary)
				{
					ExtraFiles[i].incrementalLogfile = false;
				}

				if (ini.ValueExists("FTP site", "ExtraEnable" + i))
				{
					ExtraFiles[i].enable = ini.GetValue("FTP site", "ExtraEnable" + i, false);
				}
				else
				{
					ExtraFiles[i].enable = !string.IsNullOrEmpty(ExtraFiles[i].local) && !string.IsNullOrEmpty(ExtraFiles[i].remote);
				}

				if (ExtraFiles[i].enable && ExtraFiles[i].local != string.Empty && ExtraFiles[i].remote != string.Empty)
				{
					ActiveExtraFiles.Add(new CExtraFiles
					{
						enable = ExtraFiles[i].enable,
						local = ExtraFiles[i].local,
						remote = ExtraFiles[i].remote,
						process = ExtraFiles[i].process,
						binary = ExtraFiles[i].binary,
						realtime = ExtraFiles[i].realtime,
						FTP = ExtraFiles[i].FTP,
						UTF8 = ExtraFiles[i].UTF8,
						endofday = ExtraFiles[i].endofday,
						incrementalLogfile = ExtraFiles[i].incrementalLogfile,
						logFileLastFileName = string.Empty,
						logFileLastLineNumber = 0
					});
				}
			}

			ExternalProgram = ini.GetValue("FTP site", "ExternalProgram", string.Empty);
			RealtimeProgram = ini.GetValue("FTP site", "RealtimeProgram", string.Empty);
			DailyProgram = ini.GetValue("FTP site", "DailyProgram", string.Empty);
			ExternalParams = ini.GetValue("FTP site", "ExternalParams", string.Empty);
			RealtimeParams = ini.GetValue("FTP site", "RealtimeParams", string.Empty);
			DailyParams = ini.GetValue("FTP site", "DailyParams", string.Empty);

			ForumURL = ini.GetValue("Web Site", "ForumURL", ForumDefault);
			WebcamURL = ini.GetValue("Web Site", "WebcamURL", WebcamDefault);

			CloudBaseInFeet = ini.GetValue("Station", "CloudBaseInFeet", true);

			GraphDays = ini.GetValue("Graphs", "ChartMaxDays", 31, 1);
			GraphHours = ini.GetValue("Graphs", "GraphHours", 72, 1);
			RecentDataDays = (int) Math.Ceiling(Math.Max(7, GraphHours / 24.0));
			MoonImage.Enabled = ini.GetValue("Graphs", "MoonImageEnabled", false);
			MoonImage.Size = ini.GetValue("Graphs", "MoonImageSize", 100, 10);
			MoonImage.Transparent = ini.GetValue("Graphs", "MoonImageShadeTransparent", false);
			MoonImage.FtpDest = ini.GetValue("Graphs", "MoonImageFtpDest", "images/moon.png");
			MoonImage.CopyDest = ini.GetValue("Graphs", "MoonImageCopyDest", FtpOptions.LocalCopyFolder + "images" + sep1 + "moon.png");
			GraphOptions.Visible.Temp.Val = ini.GetValue("Graphs", "TempVisible", 1, 0, 2);
			GraphOptions.Visible.InTemp.Val = ini.GetValue("Graphs", "InTempVisible", 1, 0, 2);
			GraphOptions.Visible.HeatIndex.Val = ini.GetValue("Graphs", "HIVisible", 1, 0, 2);
			GraphOptions.Visible.DewPoint.Val = ini.GetValue("Graphs", "DPVisible", 1, 0, 2);
			GraphOptions.Visible.WindChill.Val = ini.GetValue("Graphs", "WCVisible", 1, 0, 2);
			GraphOptions.Visible.AppTemp.Val = ini.GetValue("Graphs", "AppTempVisible", 1, 0, 2);
			GraphOptions.Visible.FeelsLike.Val = ini.GetValue("Graphs", "FeelsLikeVisible", 1, 0, 2);
			GraphOptions.Visible.Humidex.Val = ini.GetValue("Graphs", "HumidexVisible", 1, 0, 2);
			GraphOptions.Visible.InHum.Val = ini.GetValue("Graphs", "InHumVisible", 1, 0, 2);
			GraphOptions.Visible.OutHum.Val = ini.GetValue("Graphs", "OutHumVisible", 1, 0, 2);
			GraphOptions.Visible.UV.Val = ini.GetValue("Graphs", "UVVisible", 1, 0, 2);
			GraphOptions.Visible.Solar.Val = ini.GetValue("Graphs", "SolarVisible", 1, 0, 2);
			GraphOptions.Visible.Sunshine.Val = ini.GetValue("Graphs", "SunshineVisible", 1, 0, 2);
			GraphOptions.Visible.AvgTemp.Val = ini.GetValue("Graphs", "DailyAvgTempVisible", 1, 0, 2);
			GraphOptions.Visible.MaxTemp.Val = ini.GetValue("Graphs", "DailyMaxTempVisible", 1, 0, 2);
			GraphOptions.Visible.MinTemp.Val = ini.GetValue("Graphs", "DailyMinTempVisible", 1, 0, 2);
			GraphOptions.Visible.GrowingDegreeDays1.Val = ini.GetValue("Graphs", "GrowingDegreeDaysVisible1", 1, 0, 2);
			GraphOptions.Visible.GrowingDegreeDays2.Val = ini.GetValue("Graphs", "GrowingDegreeDaysVisible2", 1, 0, 2);
			GraphOptions.Visible.TempSum0.Val = ini.GetValue("Graphs", "TempSumVisible0", 1, 0, 2);
			GraphOptions.Visible.TempSum1.Val = ini.GetValue("Graphs", "TempSumVisible1", 1, 0, 2);
			GraphOptions.Visible.TempSum2.Val = ini.GetValue("Graphs", "TempSumVisible2", 1, 0, 2);
			GraphOptions.Visible.ChillHours.Val = ini.GetValue("Graphs", "ChillHoursVisible", 1, 0, 2);
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
			GraphOptions.Visible.CO2Sensor.CO2.Val = ini.GetValue("Graphs", "CO2-CO2", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.CO2Avg.Val = ini.GetValue("Graphs", "CO2-CO2Avg", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm25.Val = ini.GetValue("Graphs", "CO2-Pm25", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm25Avg.Val = ini.GetValue("Graphs", "CO2-Pm25Avg", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm10.Val = ini.GetValue("Graphs", "CO2-Pm10", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm10Avg.Val = ini.GetValue("Graphs", "CO2-Pm10Avg", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Temp.Val = ini.GetValue("Graphs", "CO2-Temp", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Hum.Val = ini.GetValue("Graphs", "CO2-Hum", 0, 0, 2);
			GraphOptions.Visible.SnowDepth.Val = ini.GetValue("Graphs", "SnowDepth", 0, 0, 2);
			GraphOptions.Visible.Snow24h.Val = ini.GetValue("Graphs", "Snow24h", 0, 0, 2);


			GraphOptions.Colour.Temp = ini.GetValue("GraphColours", "TempColour", "#ff0000");
			GraphOptions.Colour.InTemp = ini.GetValue("GraphColours", "InTempColour", "#50b432");
			GraphOptions.Colour.HeatIndex = ini.GetValue("GraphColours", "HIColour", "#9161c9");
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
			GraphOptions.Colour.SnowDepth = ini.GetValue("GraphColours", "SnowDepthColour", "#6495ed");
			GraphOptions.Colour.Snow24h = ini.GetValue("GraphColours", "Snow24hColour", "#008000");

			Wund.ID = ini.GetValue("Wunderground", "ID", string.Empty);
			Wund.PW = ini.GetValue("Wunderground", "Password", string.Empty);
			Wund.Enabled = ini.GetValue("Wunderground", "Enabled", false);
			Wund.RapidFireEnabled = ini.GetValue("Wunderground", "RapidFire", false);
			Wund.Interval = ini.GetValue("Wunderground", "Interval", Wund.DefaultInterval);
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
			Wund.SendExtraTemp1 = ini.GetValue("Wunderground", "SendExtraTemp1", 0, 0, 10);
			Wund.SendExtraTemp2 = ini.GetValue("Wunderground", "SendExtraTemp2", 0, 0, 10);
			Wund.SendExtraTemp3 = ini.GetValue("Wunderground", "SendExtraTemp3", 0, 0, 10);
			Wund.SendExtraTemp4 = ini.GetValue("Wunderground", "SendExtraTemp4", 0, 0, 10);
			Wund.SendAverage = ini.GetValue("Wunderground", "SendAverage", false);
			Wund.CatchUp = ini.GetValue("Wunderground", "CatchUp", true);

			Wund.SynchronisedUpdate = !Wund.RapidFireEnabled;

			Windy.ApiKey = ini.GetValue("Windy", "APIkey", string.Empty);
			Windy.StationIdx = ini.GetValue("Windy", "StationIdx", 0, 0);
			Windy.Enabled = ini.GetValue("Windy", "Enabled", false);
			Windy.Interval = ini.GetValue("Windy", "Interval", Windy.DefaultInterval);
			if (Windy.Interval < 5)
			{
				LogMessage("Cumulus.ini: Windy upload interval set to less than 5 mins, resetting to 5");
				Windy.Interval = 5;
				ini.SetValue("Windy", "Interval", Windy.Interval);
				rewriteRequired = true;
			}
			Windy.SendUV = ini.GetValue("Windy", "SendUV", false);
			Windy.SendSolar = ini.GetValue("Windy", "SendSolar", false);
			Windy.CatchUp = ini.GetValue("Windy", "CatchUp", false);

			AWEKAS.ID = ini.GetValue("Awekas", "User", string.Empty);
			AWEKAS.PW = ini.GetValue("Awekas", "Password", string.Empty);
			AWEKAS.Enabled = ini.GetValue("Awekas", "Enabled", false);
			AWEKAS.Interval = ini.GetValue("Awekas", "Interval", AWEKAS.DefaultInterval, 15);
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

			WindGuru.ID = ini.GetValue("WindGuru", "StationUID", string.Empty);
			WindGuru.PW = ini.GetValue("WindGuru", "Password", string.Empty);
			WindGuru.Enabled = ini.GetValue("WindGuru", "Enabled", false);
			WindGuru.Interval = ini.GetValue("WindGuru", "Interval", WindGuru.DefaultInterval);
			if (WindGuru.Interval < 1)
			{
				LogMessage("Cumulus.ini: WindGuru update interval invalid, resetting to 1");
				WindGuru.Interval = 1;
				ini.SetValue("WindGuru", "Interval", WindGuru.Interval);
				rewriteRequired = true;
			}
			WindGuru.SendRain = ini.GetValue("WindGuru", "SendRain", false);

			WCloud.ID = ini.GetValue("WeatherCloud", "Wid", string.Empty);
			WCloud.PW = ini.GetValue("WeatherCloud", "Key", string.Empty);
			WCloud.Enabled = ini.GetValue("WeatherCloud", "Enabled", false);
			WCloud.Interval = ini.GetValue("WeatherCloud", "Interval", WCloud.DefaultInterval);
			WCloud.SendUV = ini.GetValue("WeatherCloud", "SendUV", false);
			WCloud.SendSolar = ini.GetValue("WeatherCloud", "SendSR", false);
			WCloud.SendAirQuality = ini.GetValue("WeatherCloud", "SendAirQuality", false);
			WCloud.SendSoilMoisture = ini.GetValue("WeatherCloud", "SendSoilMoisture", false);
			WCloud.SoilMoistureSensor = ini.GetValue("WeatherCloud", "SoilMoistureSensor", 1);
			WCloud.SendLeafWetness = ini.GetValue("WeatherCloud", "SendLeafWetness", false);
			WCloud.LeafWetnessSensor = ini.GetValue("WeatherCloud", "LeafWetnessSensor", 1, 1, 8);

			PWS.ID = ini.GetValue("PWSweather", "ID", string.Empty);
			PWS.PW = ini.GetValue("PWSweather", "Password", string.Empty);
			PWS.Enabled = ini.GetValue("PWSweather", "Enabled", false);
			PWS.Interval = ini.GetValue("PWSweather", "Interval", PWS.DefaultInterval, 1);
			PWS.SendUV = ini.GetValue("PWSweather", "SendUV", false);
			PWS.SendSolar = ini.GetValue("PWSweather", "SendSR", false);
			PWS.CatchUp = ini.GetValue("PWSweather", "CatchUp", true);

			WOW.ID = ini.GetValue("WOW", "ID", string.Empty);
			WOW.PW = ini.GetValue("WOW", "Password", string.Empty);
			WOW.Enabled = ini.GetValue("WOW", "Enabled", false);
			WOW.Interval = ini.GetValue("WOW", "Interval", WOW.DefaultInterval, 1);
			WOW.SendUV = ini.GetValue("WOW", "SendUV", false);
			WOW.SendSolar = ini.GetValue("WOW", "SendSR", false);
			WOW.SendSoilTemp = ini.GetValue("WOW", "SendSoilTemp", false);
			WOW.SoilTempSensor = ini.GetValue("WOW", "SoilTempSensor", 1, 1, 8);
			WOW.CatchUp = false;

			APRS.ID = ini.GetValue("APRS", "ID", string.Empty);
			APRS.PW = ini.GetValue("APRS", "pass", "-1");
			APRS.Server = ini.GetValue("APRS", "server", "cwop.aprs.net");
			APRS.Port = ini.GetValue("APRS", "port", 14580);
			APRS.Enabled = ini.GetValue("APRS", "Enabled", false);
			APRS.Interval = ini.GetValue("APRS", "Interval", APRS.DefaultInterval, 1);
			APRS.HumidityCutoff = ini.GetValue("APRS", "APRSHumidityCutoff", false);
			APRS.SendSolar = ini.GetValue("APRS", "SendSR", false);
			APRS.UseUtcInWxNowFile = ini.GetValue("APRS", "UseUtcInWxNowFile", false);

			OpenWeatherMap.Enabled = ini.GetValue("OpenWeatherMap", "Enabled", false);
			OpenWeatherMap.CatchUp = ini.GetValue("OpenWeatherMap", "CatchUp", true);
			OpenWeatherMap.PW = ini.GetValue("OpenWeatherMap", "APIkey", string.Empty);
			OpenWeatherMap.ID = ini.GetValue("OpenWeatherMap", "StationId", string.Empty);
			OpenWeatherMap.Interval = ini.GetValue("OpenWeatherMap", "Interval", OpenWeatherMap.DefaultInterval, 1);

			Bluesky.Enabled = ini.GetValue("Bluesky", "Enabled", false);
			Bluesky.ID = ini.GetValue("Bluesky", "ID", string.Empty);
			Bluesky.PW = ini.GetValue("Bluesky", "Password", string.Empty);
			Bluesky.Interval = ini.GetValue("Bluesky", "Interval", Bluesky.DefaultInterval, 0, 1440);
			if (Bluesky.Interval > 0 && Bluesky.Interval < 60) Bluesky.Interval = 60;
			Bluesky.Language = ini.GetValue("Bluesky", "Language", CultureInfo.CurrentCulture.Name);
			Bluesky.BaseUrl = ini.GetValue("Bluesky", "BaseUrl", "https://bsky.social");
			Bluesky.CatchUp = false;
			for (var i = 0; i < Bluesky.TimedPostsTime.Length; i++)
			{
				if (ini.ValueExists("Bluesky", "TimedPost" + i) )
				{
					Bluesky.TimedPostsTime[i] = DateTime.ParseExact(ini.GetValue("Bluesky", "TimedPost" + i, "00:00"), "HH:mm", System.Globalization.CultureInfo.InvariantCulture).TimeOfDay;
					Bluesky.TimedPostsFile[i] = ini.GetValue("Bluesky", "TimedPostFile" + i, "web" + DirectorySeparator + "Bluesky.txt");
				}
				else
				{
					Bluesky.TimedPostsTime[i] = TimeSpan.MaxValue;
					Bluesky.TimedPostsFile[i] = null;
				}
			}
			for (var i = 0; i < Bluesky.VariablePostsTime.Length; i++)
			{
				if (ini.ValueExists("Bluesky", "VariablePost" + i))
				{
					Bluesky.VariablePostsTime[i] = ini.GetValue("Bluesky", "VariablePost" + i, string.Empty);
					Bluesky.VariablePostsFile[i] = ini.GetValue("Bluesky", "VariablePostFile" + i, string.Empty);
				}
				else
				{
					Bluesky.VariablePostsTime[i] = null;
					Bluesky.VariablePostsFile[i] = null;
				}
			}


			MQTT.Server = ini.GetValue("MQTT", "Server", string.Empty);
			MQTT.Port = ini.GetValue("MQTT", "Port", 1883, 1, 65535);
			MQTT.IpVersion = ini.GetValue("MQTT", "IPversion", 0, 0, 6); // 0 = unspecified, 4 = force IPv4, 6 = force IPv6
			if (MQTT.IpVersion != 0 && MQTT.IpVersion != 4 && MQTT.IpVersion != 6)
			{
				LogMessage("Cumulus.ini: MQTT IP Version invalid, restting to unspecified");
				MQTT.IpVersion = 0;
				ini.SetValue("MQTT", "IPversion", MQTT.IpVersion);
				rewriteRequired = true;
			}
			MQTT.UseTLS = ini.GetValue("MQTT", "UseTLS", false);
			MQTT.Username = ini.GetValue("MQTT", "Username", string.Empty);
			MQTT.Password = ini.GetValue("MQTT", "Password", string.Empty);
			MQTT.EnableDataUpdate = ini.GetValue("MQTT", "EnableDataUpdate", false);
			MQTT.UpdateTemplate = ini.GetValue("MQTT", "UpdateTemplate", "DataUpdateTemplate.txt");
			MQTT.EnableInterval = ini.GetValue("MQTT", "EnableInterval", false);
			MQTT.IntervalTemplate = ini.GetValue("MQTT", "IntervalTemplate", "IntervalTemplate.txt");

			LowTempAlarm.Value = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
			LowTempAlarm.Enabled = ini.GetValue("Alarms", "LowTempAlarmSet", false);
			LowTempAlarm.Sound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
			LowTempAlarm.SoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
			if (LowTempAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				LowTempAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			LowTempAlarm.Notify = ini.GetValue("Alarms", "LowTempAlarmNotify", false);
			LowTempAlarm.Email = ini.GetValue("Alarms", "LowTempAlarmEmail", false);
			LowTempAlarm.Latch = ini.GetValue("Alarms", "LowTempAlarmLatch", false);
			LowTempAlarm.LatchHours = ini.GetValue("Alarms", "LowTempAlarmLatchHours", 24.0, 0.0);
			LowTempAlarm.Action = ini.GetValue("Alarms", "LowTempAlarmAction", string.Empty);
			LowTempAlarm.ActionParams = ini.GetValue("Alarms", "LowTempAlarmActionParams", string.Empty);
			LowTempAlarm.ShowWindow = ini.GetValue("Alarms", "LowTempAlarmActionWindow", false);
			LowTempAlarm.BskyFile = ini.GetValue("Alarms", "LowTempAlarmBlueskyFile", string.Empty);

			HighTempAlarm.Value = ini.GetValue("Alarms", "alarmhightemp", 0.0);
			HighTempAlarm.Enabled = ini.GetValue("Alarms", "HighTempAlarmSet", false);
			HighTempAlarm.Sound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
			HighTempAlarm.SoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
			if (HighTempAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighTempAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighTempAlarm.Notify = ini.GetValue("Alarms", "HighTempAlarmNotify", false);
			HighTempAlarm.Email = ini.GetValue("Alarms", "HighTempAlarmEmail", false);
			HighTempAlarm.Latch = ini.GetValue("Alarms", "HighTempAlarmLatch", false);
			HighTempAlarm.LatchHours = ini.GetValue("Alarms", "HighTempAlarmLatchHours", 24.0, 0.0);
			HighTempAlarm.Action = ini.GetValue("Alarms", "HighTempAlarmAction", string.Empty);
			HighTempAlarm.ActionParams = ini.GetValue("Alarms", "HighTempAlarmActionParams", string.Empty);
			HighTempAlarm.ShowWindow = ini.GetValue("Alarms", "HighTempAlarmActionWindow", false);
			HighTempAlarm.BskyFile = ini.GetValue("Alarms", "HighTempAlarmBlueskyFile", string.Empty);

			TempChangeAlarm.Value = ini.GetValue("Alarms", "alarmtempchange", 0.0);
			TempChangeAlarm.Enabled = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
			TempChangeAlarm.Sound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
			TempChangeAlarm.SoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
			if (TempChangeAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				TempChangeAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			TempChangeAlarm.Notify = ini.GetValue("Alarms", "TempChangeAlarmNotify", false);
			TempChangeAlarm.Email = ini.GetValue("Alarms", "TempChangeAlarmEmail", false);
			TempChangeAlarm.Latch = ini.GetValue("Alarms", "TempChangeAlarmLatch", false);
			TempChangeAlarm.LatchHours = ini.GetValue("Alarms", "TempChangeAlarmLatchHours", 24.0, 0.0);
			TempChangeAlarm.Action = ini.GetValue("Alarms", "TempChangeAlarmAction", string.Empty);
			TempChangeAlarm.ActionParams = ini.GetValue("Alarms", "TempChangeAlarmActionParams", string.Empty);
			TempChangeAlarm.ShowWindow = ini.GetValue("Alarms", "TempChangeAlarmActionWindow", false);
			TempChangeAlarm.BskyFile = ini.GetValue("Alarms", "TempChangeAlarmBlueskyFile", string.Empty);

			LowPressAlarm.Value = ini.GetValue("Alarms", "alarmlowpress", 0.0);
			LowPressAlarm.Enabled = ini.GetValue("Alarms", "LowPressAlarmSet", false);
			LowPressAlarm.Sound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
			LowPressAlarm.SoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
			if (LowPressAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				LowPressAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			LowPressAlarm.Notify = ini.GetValue("Alarms", "LowPressAlarmNotify", false);
			LowPressAlarm.Email = ini.GetValue("Alarms", "LowPressAlarmEmail", false);
			LowPressAlarm.Latch = ini.GetValue("Alarms", "LowPressAlarmLatch", false);
			LowPressAlarm.LatchHours = ini.GetValue("Alarms", "LowPressAlarmLatchHours", 24.0, 0.0);
			LowPressAlarm.Action = ini.GetValue("Alarms", "LowPressAlarmAction", string.Empty);
			LowPressAlarm.ActionParams = ini.GetValue("Alarms", "LowPressAlarmActionParams", string.Empty);
			LowPressAlarm.ShowWindow = ini.GetValue("Alarms", "LowPressAlarmActionWindow", false);
			LowPressAlarm.BskyFile = ini.GetValue("Alarms", "LowPressAlarmBlueskyFile", string.Empty);

			HighPressAlarm.Value = ini.GetValue("Alarms", "alarmhighpress", 0.0, 0.0);
			HighPressAlarm.Enabled = ini.GetValue("Alarms", "HighPressAlarmSet", false);
			HighPressAlarm.Sound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
			HighPressAlarm.SoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
			if (HighPressAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighPressAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighPressAlarm.Notify = ini.GetValue("Alarms", "HighPressAlarmNotify", false);
			HighPressAlarm.Email = ini.GetValue("Alarms", "HighPressAlarmEmail", false);
			HighPressAlarm.Latch = ini.GetValue("Alarms", "HighPressAlarmLatch", false);
			HighPressAlarm.LatchHours = ini.GetValue("Alarms", "HighPressAlarmLatchHours", 24.0, 0.0);
			HighPressAlarm.Action = ini.GetValue("Alarms", "HighPressAlarmAction", string.Empty);
			HighPressAlarm.ActionParams = ini.GetValue("Alarms", "HighPressAlarmActionParams", string.Empty);
			HighPressAlarm.ShowWindow = ini.GetValue("Alarms", "HighPressAlarmAlarmActionWindow", false);
			HighPressAlarm.BskyFile = ini.GetValue("Alarms", "HighPressAlarmBlueskyFile", string.Empty);

			PressChangeAlarm.Value = ini.GetValue("Alarms", "alarmpresschange", 0.0, 0.0);
			PressChangeAlarm.Enabled = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
			PressChangeAlarm.Sound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
			PressChangeAlarm.SoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
			if (PressChangeAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				PressChangeAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			PressChangeAlarm.Notify = ini.GetValue("Alarms", "PressChangeAlarmNotify", false);
			PressChangeAlarm.Email = ini.GetValue("Alarms", "PressChangeAlarmEmail", false);
			PressChangeAlarm.Latch = ini.GetValue("Alarms", "PressChangeAlarmLatch", false);
			PressChangeAlarm.LatchHours = ini.GetValue("Alarms", "PressChangeAlarmLatchHours", 24.0, 0.0);
			PressChangeAlarm.Action = ini.GetValue("Alarms", "PressChangeAlarmAction", string.Empty);
			PressChangeAlarm.ActionParams = ini.GetValue("Alarms", "PressChangeAlarmActionParams", string.Empty);
			PressChangeAlarm.ShowWindow = ini.GetValue("Alarms", "PressChangeAlarmActionWindow", false);
			PressChangeAlarm.BskyFile = ini.GetValue("Alarms", "PressChangeAlarmBlueskyFile", string.Empty);

			HighRainTodayAlarm.Value = ini.GetValue("Alarms", "alarmhighraintoday", 0.0, 0.0);
			HighRainTodayAlarm.Enabled = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
			HighRainTodayAlarm.Sound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
			HighRainTodayAlarm.SoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
			if (HighRainTodayAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighRainTodayAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighRainTodayAlarm.Notify = ini.GetValue("Alarms", "HighRainTodayAlarmNotify", false);
			HighRainTodayAlarm.Email = ini.GetValue("Alarms", "HighRainTodayAlarmEmail", false);
			HighRainTodayAlarm.Latch = ini.GetValue("Alarms", "HighRainTodayAlarmLatch", false);
			HighRainTodayAlarm.LatchHours = ini.GetValue("Alarms", "HighRainTodayAlarmLatchHours", 24.0, 0.0);
			HighRainTodayAlarm.Action = ini.GetValue("Alarms", "HighRainTodayAlarmAction", string.Empty);
			HighRainTodayAlarm.ActionParams = ini.GetValue("Alarms", "HighRainTodayAlarmActionParams", string.Empty);
			HighRainTodayAlarm.ShowWindow = ini.GetValue("Alarms", "HighRainTodayAlarmActionWindow", false);
			HighRainTodayAlarm.BskyFile = ini.GetValue("Alarms", "HighRainTodayAlarmBlueskyFile", string.Empty);

			HighRainRateAlarm.Value = ini.GetValue("Alarms", "alarmhighrainrate", 0.0, 0.0);
			HighRainRateAlarm.Enabled = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
			HighRainRateAlarm.Sound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
			HighRainRateAlarm.SoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
			if (HighRainRateAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighRainRateAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighRainRateAlarm.Notify = ini.GetValue("Alarms", "HighRainRateAlarmNotify", false);
			HighRainRateAlarm.Email = ini.GetValue("Alarms", "HighRainRateAlarmEmail", false);
			HighRainRateAlarm.Latch = ini.GetValue("Alarms", "HighRainRateAlarmLatch", false);
			HighRainRateAlarm.LatchHours = ini.GetValue("Alarms", "HighRainRateAlarmLatchHours", 24.0, 0.0);
			HighRainRateAlarm.Action = ini.GetValue("Alarms", "HighRainRateAlarmAction", string.Empty);
			HighRainRateAlarm.ActionParams = ini.GetValue("Alarms", "HighRainRateAlarmActionParams", string.Empty);
			HighRainRateAlarm.ShowWindow = ini.GetValue("Alarms", "HighRainRateAlarmActionWindow", false);
			HighRainRateAlarm.BskyFile = ini.GetValue("Alarms", "HighRainRateAlarmBlueskyFile", string.Empty);

			IsRainingAlarm.Enabled = ini.GetValue("Alarms", "IsRainingAlarmSet", false);
			IsRainingAlarm.Sound = ini.GetValue("Alarms", "IsRainingAlarmSound", false);
			IsRainingAlarm.SoundFile = ini.GetValue("Alarms", "IsRainingAlarmSoundFile", DefaultSoundFile);
			IsRainingAlarm.Notify = ini.GetValue("Alarms", "IsRainingAlarmNotify", false);
			IsRainingAlarm.Email = ini.GetValue("Alarms", "IsRainingAlarmEmail", false);
			IsRainingAlarm.Latch = ini.GetValue("Alarms", "IsRainingAlarmLatch", false);
			IsRainingAlarm.LatchHours = ini.GetValue("Alarms", "IsRainingAlarmLatchHours", 1.0, 0.0);
			IsRainingAlarm.Action = ini.GetValue("Alarms", "IsRainingAlarmAction", string.Empty);
			IsRainingAlarm.ActionParams = ini.GetValue("Alarms", "IsRainingAlarmActionParams", string.Empty);
			IsRainingAlarm.ShowWindow = ini.GetValue("Alarms", "IsRainingAlarmActionWindow", false);
			IsRainingAlarm.BskyFile = ini.GetValue("Alarms", "IsRainingAlarmBlueskyFile", string.Empty);

			HighGustAlarm.Value = ini.GetValue("Alarms", "alarmhighgust", 0.0, 0.0);
			HighGustAlarm.Enabled = ini.GetValue("Alarms", "HighGustAlarmSet", false);
			HighGustAlarm.Sound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
			HighGustAlarm.SoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
			if (HighGustAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighGustAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighGustAlarm.Notify = ini.GetValue("Alarms", "HighGustAlarmNotify", false);
			HighGustAlarm.Email = ini.GetValue("Alarms", "HighGustAlarmEmail", false);
			HighGustAlarm.Latch = ini.GetValue("Alarms", "HighGustAlarmLatch", false);
			HighGustAlarm.LatchHours = ini.GetValue("Alarms", "HighGustAlarmLatchHours", 24.0, 0.0);
			HighGustAlarm.Action = ini.GetValue("Alarms", "HighGustAlarmAction", string.Empty);
			HighGustAlarm.ActionParams = ini.GetValue("Alarms", "HighGustAlarmActionParams", string.Empty);
			HighGustAlarm.ShowWindow = ini.GetValue("Alarms", "HighGustActionWindow", false);
			HighGustAlarm.BskyFile = ini.GetValue("Alarms", "HighGustAlarmBlueskyFile", string.Empty);

			HighWindAlarm.Value = ini.GetValue("Alarms", "alarmhighwind", 0.0, 0.0);
			HighWindAlarm.Enabled = ini.GetValue("Alarms", "HighWindAlarmSet", false);
			HighWindAlarm.Sound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
			HighWindAlarm.SoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
			if (HighWindAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighWindAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighWindAlarm.Notify = ini.GetValue("Alarms", "HighWindAlarmNotify", false);
			HighWindAlarm.Email = ini.GetValue("Alarms", "HighWindAlarmEmail", false);
			HighWindAlarm.Latch = ini.GetValue("Alarms", "HighWindAlarmLatch", false);
			HighWindAlarm.LatchHours = ini.GetValue("Alarms", "HighWindAlarmLatchHours", 24.0, 0.0);
			HighWindAlarm.Action = ini.GetValue("Alarms", "HighWindAlarmAction", string.Empty);
			HighWindAlarm.ActionParams = ini.GetValue("Alarms", "HighWindAlarmActionParams", string.Empty);
			HighWindAlarm.ShowWindow = ini.GetValue("Alarms", "HighWindAlarmActionWindow", false);
			HighWindAlarm.BskyFile = ini.GetValue("Alarms", "HighWindAlarmBlueskyFile", string.Empty);

			SensorAlarm.Enabled = ini.GetValue("Alarms", "SensorAlarmSet", true);
			SensorAlarm.Sound = ini.GetValue("Alarms", "SensorAlarmSound", false);
			SensorAlarm.SoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
			if (SensorAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				SensorAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			SensorAlarm.Notify = ini.GetValue("Alarms", "SensorAlarmNotify", true);
			SensorAlarm.Email = ini.GetValue("Alarms", "SensorAlarmEmail", false);
			SensorAlarm.Latch = ini.GetValue("Alarms", "SensorAlarmLatch", true);
			SensorAlarm.LatchHours = ini.GetValue("Alarms", "SensorAlarmLatchHours", 1.0, 0.0);
			SensorAlarm.TriggerThreshold = ini.GetValue("Alarms", "SensorAlarmTriggerCount", 2, 0);
			SensorAlarm.Action = ini.GetValue("Alarms", "SensorAlarmAction", string.Empty);
			SensorAlarm.ActionParams = ini.GetValue("Alarms", "SensorAlarmActionParams", string.Empty);
			SensorAlarm.ShowWindow = ini.GetValue("Alarms", "SensorAlarmActionWindow", false);
			SensorAlarm.BskyFile = ini.GetValue("Alarms", "SensorAlarmBlueskyFile", "none");

			DataStoppedAlarm.Enabled = ini.GetValue("Alarms", "DataStoppedAlarmSet", true);
			DataStoppedAlarm.Sound = ini.GetValue("Alarms", "DataStoppedAlarmSound", false);
			DataStoppedAlarm.SoundFile = ini.GetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
			if (DataStoppedAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				SensorAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			DataStoppedAlarm.Notify = ini.GetValue("Alarms", "DataStoppedAlarmNotify", true);
			DataStoppedAlarm.Email = ini.GetValue("Alarms", "DataStoppedAlarmEmail", false);
			DataStoppedAlarm.Latch = ini.GetValue("Alarms", "DataStoppedAlarmLatch", true);
			DataStoppedAlarm.LatchHours = ini.GetValue("Alarms", "DataStoppedAlarmLatchHours", 1.0, 0.0);
			DataStoppedAlarm.TriggerThreshold = ini.GetValue("Alarms", "DataStoppedAlarmTriggerCount", 2, 0);
			DataStoppedAlarm.Action = ini.GetValue("Alarms", "DataStoppedAlarmAction", string.Empty);
			DataStoppedAlarm.ActionParams = ini.GetValue("Alarms", "DataStoppedAlarmActionParams", string.Empty);
			DataStoppedAlarm.ShowWindow = ini.GetValue("Alarms", "DataStoppedAlarmActionWindow", false);
			DataStoppedAlarm.BskyFile = ini.GetValue("Alarms", "DataStoppedAlarmBlueskyFile", "none");

			// Alarms below here were created after the change in default sound file, so no check required
			BatteryLowAlarm.Enabled = ini.GetValue("Alarms", "BatteryLowAlarmSet", false);
			BatteryLowAlarm.Sound = ini.GetValue("Alarms", "BatteryLowAlarmSound", false);
			BatteryLowAlarm.SoundFile = ini.GetValue("Alarms", "BatteryLowAlarmSoundFile", DefaultSoundFile);
			BatteryLowAlarm.Notify = ini.GetValue("Alarms", "BatteryLowAlarmNotify", false);
			BatteryLowAlarm.Email = ini.GetValue("Alarms", "BatteryLowAlarmEmail", false);
			BatteryLowAlarm.Latch = ini.GetValue("Alarms", "BatteryLowAlarmLatch", false);
			BatteryLowAlarm.LatchHours = ini.GetValue("Alarms", "BatteryLowAlarmLatchHours", 24.0, 0.0);
			BatteryLowAlarm.TriggerThreshold = ini.GetValue("Alarms", "BatteryLowAlarmTriggerCount", 1, 0);
			BatteryLowAlarm.Action = ini.GetValue("Alarms", "BatteryLowAlarmAction", string.Empty);
			BatteryLowAlarm.ActionParams = ini.GetValue("Alarms", "BatteryLowAlarmActionParams", string.Empty);
			BatteryLowAlarm.ShowWindow = ini.GetValue("Alarms", "BatteryLowAlarmActionWindow", false);
			BatteryLowAlarm.BskyFile = ini.GetValue("Alarms", "BatteryLowAlarmBlueskyFile", "none");

			SpikeAlarm.Enabled = ini.GetValue("Alarms", "DataSpikeAlarmSet", false);
			SpikeAlarm.Sound = ini.GetValue("Alarms", "DataSpikeAlarmSound", false);
			SpikeAlarm.SoundFile = ini.GetValue("Alarms", "DataSpikeAlarmSoundFile", DefaultSoundFile);
			SpikeAlarm.Notify = ini.GetValue("Alarms", "DataSpikeAlarmNotify", true);
			SpikeAlarm.Email = ini.GetValue("Alarms", "DataSpikeAlarmEmail", true);
			SpikeAlarm.Latch = ini.GetValue("Alarms", "DataSpikeAlarmLatch", true);
			SpikeAlarm.LatchHours = ini.GetValue("Alarms", "DataSpikeAlarmLatchHours", 24.0, 0.0);
			SpikeAlarm.TriggerThreshold = ini.GetValue("Alarms", "DataSpikeAlarmTriggerCount", 1, 0);
			SpikeAlarm.Action = ini.GetValue("Alarms", "DataSpikeAlarmAction", string.Empty);
			SpikeAlarm.ActionParams = ini.GetValue("Alarms", "DataSpikeAlarmActionParams", string.Empty);
			SpikeAlarm.ShowWindow = ini.GetValue("Alarms", "DataSpikeAlarmActionWindow", false);
			SpikeAlarm.BskyFile = ini.GetValue("Alarms", "DataSpikeAlarmBlueskyFile", "none");

			UpgradeAlarm.Enabled = ini.GetValue("Alarms", "UpgradeAlarmSet", true);
			UpgradeAlarm.Sound = ini.GetValue("Alarms", "UpgradeAlarmSound", false);
			UpgradeAlarm.SoundFile = ini.GetValue("Alarms", "UpgradeAlarmSoundFile", DefaultSoundFile);
			UpgradeAlarm.Notify = ini.GetValue("Alarms", "UpgradeAlarmNotify", true);
			UpgradeAlarm.Email = ini.GetValue("Alarms", "UpgradeAlarmEmail", false);
			UpgradeAlarm.Latch = ini.GetValue("Alarms", "UpgradeAlarmLatch", false);
			UpgradeAlarm.LatchHours = ini.GetValue("Alarms", "UpgradeAlarmLatchHours", 24.0, 0.0);
			UpgradeAlarm.Action = ini.GetValue("Alarms", "UpgradeAlarmAction", string.Empty);
			UpgradeAlarm.ActionParams = ini.GetValue("Alarms", "UpgradeAlarmActionParams", string.Empty);
			UpgradeAlarm.ShowWindow = ini.GetValue("Alarms", "UpgradeAlarmActionWindow", false);
			UpgradeAlarm.BskyFile = ini.GetValue("Alarms", "UpgradeAlarmBlueskyFile", "none");

			FirmwareAlarm.Enabled = ini.GetValue("Alarms", "FirmwareAlarmSet", true);
			FirmwareAlarm.Sound = ini.GetValue("Alarms", "FirmwareAlarmSound", false);
			FirmwareAlarm.SoundFile = ini.GetValue("Alarms", "FirmwareAlarmSoundFile", DefaultSoundFile);
			FirmwareAlarm.Notify = ini.GetValue("Alarms", "FirmwareAlarmNotify", true);
			FirmwareAlarm.Email = ini.GetValue("Alarms", "FirmwareAlarmEmail", false);
			FirmwareAlarm.Latch = ini.GetValue("Alarms", "FirmwareAlarmLatch", false);
			FirmwareAlarm.LatchHours = ini.GetValue("Alarms", "FirmwareAlarmLatchHours", 24.0, 0.0);
			FirmwareAlarm.Action = ini.GetValue("Alarms", "FirmwareAlarmAction", string.Empty);
			FirmwareAlarm.ActionParams = ini.GetValue("Alarms", "FirmwareAlarmActionParams", string.Empty);
			FirmwareAlarm.ShowWindow = ini.GetValue("Alarms", "FirmwareAlarmActionWindow", false);
			FirmwareAlarm.BskyFile = ini.GetValue("Alarms", "FirmwareAlarmBlueskyFile", "none");

			ThirdPartyAlarm.Enabled = ini.GetValue("Alarms", "HttpUploadAlarmSet", false);
			ThirdPartyAlarm.Sound = ini.GetValue("Alarms", "HttpUploadAlarmSound", false);
			ThirdPartyAlarm.SoundFile = ini.GetValue("Alarms", "HttpUploadAlarmSoundFile", DefaultSoundFile);
			ThirdPartyAlarm.Notify = ini.GetValue("Alarms", "HttpUploadAlarmNotify", false);
			ThirdPartyAlarm.Email = ini.GetValue("Alarms", "HttpUploadAlarmEmail", false);
			ThirdPartyAlarm.Latch = ini.GetValue("Alarms", "HttpUploadAlarmLatch", false);
			ThirdPartyAlarm.LatchHours = ini.GetValue("Alarms", "HttpUploadAlarmLatchHours", 24.0, 0.0);
			ThirdPartyAlarm.TriggerThreshold = ini.GetValue("Alarms", "HttpUploadAlarmTriggerCount", 1, 0);
			ThirdPartyAlarm.Action = ini.GetValue("Alarms", "HttpUploadAlarmAction", string.Empty);
			ThirdPartyAlarm.ActionParams = ini.GetValue("Alarms", "HttpUploadAlarmActionParams", string.Empty);
			ThirdPartyAlarm.ShowWindow = ini.GetValue("Alarms", "HttpUploadAlarmActionWindow", false);
			ThirdPartyAlarm.BskyFile = ini.GetValue("Alarms", "HttpUploadAlarmBlueskyFile", "none");

			MySqlUploadAlarm.Enabled = ini.GetValue("Alarms", "MySqlUploadAlarmSet", false);
			MySqlUploadAlarm.Sound = ini.GetValue("Alarms", "MySqlUploadAlarmSound", false);
			MySqlUploadAlarm.SoundFile = ini.GetValue("Alarms", "MySqlUploadAlarmSoundFile", DefaultSoundFile);
			MySqlUploadAlarm.Notify = ini.GetValue("Alarms", "MySqlUploadAlarmNotify", false);
			MySqlUploadAlarm.Email = ini.GetValue("Alarms", "MySqlUploadAlarmEmail", false);
			MySqlUploadAlarm.Latch = ini.GetValue("Alarms", "MySqlUploadAlarmLatch", false);
			MySqlUploadAlarm.LatchHours = ini.GetValue("Alarms", "MySqlUploadAlarmLatchHours", 24.0, 0.0);
			MySqlUploadAlarm.TriggerThreshold = ini.GetValue("Alarms", "MySqlUploadAlarmTriggerCount", 1, 0);
			MySqlUploadAlarm.Action = ini.GetValue("Alarms", "MySqlUploadAlarmAction", string.Empty);
			MySqlUploadAlarm.ActionParams = ini.GetValue("Alarms", "MySqlUploadAlarmActionParams", string.Empty);
			MySqlUploadAlarm.BskyFile = ini.GetValue("Alarms", "MySqlUploadAlarmBlueskyFile", "none");

			NewRecordAlarm.Enabled = ini.GetValue("Alarms", "NewRecordAlarmSet", true);
			NewRecordAlarm.Sound = ini.GetValue("Alarms", "NewRecordAlarmSound", false);
			NewRecordAlarm.SoundFile = ini.GetValue("Alarms", "NewRecordAlarmSoundFile", DefaultSoundFile);
			NewRecordAlarm.Notify = ini.GetValue("Alarms", "NewRecordAlarmNotify", false);
			NewRecordAlarm.Email = ini.GetValue("Alarms", "NewRecordAlarmEmail", false);
			NewRecordAlarm.Latch = ini.GetValue("Alarms", "NewRecordAlarmLatch", false);
			NewRecordAlarm.LatchHours = ini.GetValue("Alarms", "NewRecordAlarmLatchHours", 24.0, 0.0);
			NewRecordAlarm.Action = ini.GetValue("Alarms", "NewRecordAlarmAction", string.Empty);
			NewRecordAlarm.ActionParams = ini.GetValue("Alarms", "NewRecordAlarmActionParams", string.Empty);
			NewRecordAlarm.ShowWindow = ini.GetValue("Alarms", "NewRecordAlarmActionWindow", false);
			NewRecordAlarm.BskyFile = ini.GetValue("Alarms", "NewRecordAlarmBlueskyFile", string.Empty);

			FtpAlarm.Enabled = ini.GetValue("Alarms", "FtpAlarmSet", false);
			FtpAlarm.Sound = ini.GetValue("Alarms", "FtpAlarmSound", false);
			FtpAlarm.SoundFile = ini.GetValue("Alarms", "FtpAlarmSoundFile", DefaultSoundFile);
			FtpAlarm.Notify = ini.GetValue("Alarms", "FtpAlarmNotify", false);
			FtpAlarm.Email = ini.GetValue("Alarms", "FtpAlarmEmail", false);
			FtpAlarm.Latch = ini.GetValue("Alarms", "FtpAlarmLatch", false);
			FtpAlarm.LatchHours = ini.GetValue("Alarms", "FtpAlarmLatchHours", 24.0, 0.0);
			FtpAlarm.Action = ini.GetValue("Alarms", "FtpAlarmAction", string.Empty);
			FtpAlarm.ActionParams = ini.GetValue("Alarms", "FtpAlarmActionParams", string.Empty);
			FtpAlarm.ShowWindow = ini.GetValue("Alarms", "FtpAlarmActionWindow", false);
			FtpAlarm.BskyFile = ini.GetValue("Alarms", "FtpAlarmBlueskyFile", "none");

			AlarmFromEmail = ini.GetValue("Alarms", "FromEmail", string.Empty);
			AlarmDestEmail = ini.GetValue("Alarms", "DestEmail", string.Empty).Split(';');
			AlarmEmailHtml = ini.GetValue("Alarms", "UseHTML", false);
			AlarmEmailUseBcc = ini.GetValue("Alarms", "UseBCC", false);

			// User Alarm Settings
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("UserAlarms", "AlarmName" + i))
				{
					var name = ini.GetValue("UserAlarms", "AlarmName" + i, string.Empty);
					var tag = ini.GetValue("UserAlarms", "AlarmTag" + i, string.Empty);
					var type = ini.GetValue("UserAlarms", "AlarmType" + i, string.Empty);
					var value = ini.GetValue("UserAlarms", "AlarmValue" + i, 0.0);
					var enabled = ini.GetValue("UserAlarms", "AlarmEnabled" + i, false);
					var email = ini.GetValue("UserAlarms", "AlarmEmail" + i, false);
					var emailMsg = ini.GetValue("UserAlarms", "AlarmEmailMsg" + i, string.Empty);
					var bsky = ini.GetValue("UserAlarms", "AlarmBlueskyFile" + i, string.Empty);
					var latch = ini.GetValue("UserAlarms", "AlarmLatch" + i, false);
					var latchHours = ini.GetValue("UserAlarms", "AlarmLatchHours" + i, 24.0, 0.0);
					var action = ini.GetValue("UserAlarms", "AlarmAction" + i, string.Empty);
					var actionParams = ini.GetValue("UserAlarms", "AlarmActionParams" + i, string.Empty);
					var showWindow = ini.GetValue("UserAlarms", "AlarmActionWindow" + i, false);

					if (name != string.Empty && tag != string.Empty && type != string.Empty)
					{
						try
						{
							UserAlarms.Add(new AlarmUser(name, type, tag, this)
							{
								Value = value,
								Enabled = enabled,
								Email = email,
								EmailMsg = emailMsg,
								BskyFile = bsky,
								Latch = latch,
								LatchHours = latchHours,
								Action = action,
								ActionParams = actionParams,
								ShowWindow = showWindow
							});
						}
						catch (Exception ex)
						{
							LogErrorMessage($"Error loading user alarm {ini.GetValue("UserAlarms", "AlarmName" + i, string.Empty)}: {ex.Message}");
						}
					}
				}
			}

			Calib.Press.Offset = ini.GetValue("Offsets", "PressOffset", 0.0);
			Calib.PressStn.Offset = ini.GetValue("Offsets", "PressOffset", Calib.Press.Offset);
			Calib.Temp.Offset = ini.GetValue("Offsets", "TempOffset", 0.0);
			Calib.Hum.Offset = ini.GetValue("Offsets", "HumOffset", 0);
			Calib.WindDir.Offset = ini.GetValue("Offsets", "WindDirOffset", 0);
			Calib.Solar.Offset = ini.GetValue("Offsets", "SolarOffset", 0.0);
			Calib.UV.Offset = ini.GetValue("Offsets", "UVOffset", 0.0);
			Calib.WetBulb.Offset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);
			Calib.InTemp.Offset = ini.GetValue("Offsets", "InTempOffset", 0.0);
			Calib.InHum.Offset = ini.GetValue("Offsets", "InHumOffset", 0);

			Calib.Press.Mult = ini.GetValue("Offsets", "PressMult", 1.0);
			Calib.PressStn.Mult = ini.GetValue("Offsets", "PressMult", Calib.Press.Mult);
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
			Calib.PressStn.Mult2 = ini.GetValue("Offsets", "PressMult2", Calib.Press.Mult2);
			Calib.WindSpeed.Mult2 = ini.GetValue("Offsets", "WindSpeedMult2", 0.0);
			Calib.WindGust.Mult2 = ini.GetValue("Offsets", "WindGustMult2", 0.0);
			Calib.Temp.Mult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
			Calib.Hum.Mult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
			Calib.InTemp.Mult2 = ini.GetValue("Offsets", "InTempMult2", 0.0);
			Calib.InHum.Mult2 = ini.GetValue("Offsets", "InHumMult2", 0.0);
			Calib.Solar.Mult2 = ini.GetValue("Offsets", "SolarMult2", 0.0);
			Calib.UV.Mult2 = ini.GetValue("Offsets", "UVMult2", 0.0);

			Limit.TempHigh = ConvertUnits.TempCToUser(ini.GetValue("Limits", "TempHighC", 60.0));
			Limit.TempLow = ConvertUnits.TempCToUser(ini.GetValue("Limits", "TempLowC", -60.0));
			Limit.DewHigh = ConvertUnits.TempCToUser(ini.GetValue("Limits", "DewHighC", 40.0));
			Limit.PressHigh = ConvertUnits.PressMBToUser(ini.GetValue("Limits", "PressHighMB", 1090.0));
			Limit.PressLow = ConvertUnits.PressMBToUser(ini.GetValue("Limits", "PressLowMB", 870.0));
			Limit.WindHigh = ConvertUnits.WindMSToUser(ini.GetValue("Limits", "WindHighMS", 90.0));

			xapEnabled = ini.GetValue("xAP", "Enabled", false);
			xapUID = ini.GetValue("xAP", "UID", "4375");
			xapPort = ini.GetValue("xAP", "Port", 3639, 1, 65535);

			SolarOptions.SunThreshold = ini.GetValue("Solar", "SunThreshold", 75, 1, 200);
			SolarOptions.SolarMinimum = ini.GetValue("Solar", "SolarMinimum", 30, 0);
			SolarOptions.LuxToWM2 = ini.GetValue("Solar", "LuxToWM2", 0.0079);
			SolarOptions.UseBlakeLarsen = ini.GetValue("Solar", "UseBlakeLarsen", false);
			SolarOptions.SolarCalc = ini.GetValue("Solar", "SolarCalc", 0, 0, 1);

			// Migrate old single solar factors to the new dual scheme
			if (ini.ValueExists("Solar", "RStransfactor"))
			{
				SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactor", 0.8, 0.1);
				SolarOptions.RStransfactorDec = SolarOptions.RStransfactorJun;
				ini.DeleteValue("Solar", "RStransfactor");
				ini.SetValue("Solar", "RStransfactorJun", SolarOptions.RStransfactorJun);
				ini.SetValue("Solar", "RStransfactorDec", SolarOptions.RStransfactorDec);
				rewriteRequired = true;
			}
			else
			{
				if (ini.ValueExists("Solar", "RStransfactorJul"))
				{
					SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactorJul", 0.8, 0.1);
					ini.DeleteValue("Solar", "RStransfactorJul");
					ini.SetValue("Solar", "RStransfactorJun", SolarOptions.RStransfactorJun);
					rewriteRequired = true;
				}
				else
				{
					SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactorJun", 0.8, 0.1);
				}
				SolarOptions.RStransfactorDec = ini.GetValue("Solar", "RStransfactorDec", 0.8, 0.1);
			}
			if (ini.ValueExists("Solar", "BrasTurbidity"))
			{
				SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidity", 2.0);
				SolarOptions.BrasTurbidityDec = SolarOptions.BrasTurbidityJun;
				ini.DeleteValue("Solar", "BrasTurbidity");
				ini.SetValue("Solar", "BrasTurbidityJun", SolarOptions.BrasTurbidityJun);
				ini.SetValue("Solar", "BrasTurbidityDec", SolarOptions.BrasTurbidityDec);
				rewriteRequired = true;
			}
			else
			{
				if (ini.ValueExists("Solar", "BrasTurbidityJul"))
				{
					SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidityJul", 2.0);
					ini.DeleteValue("Solar", "BrasTurbidityJul");
					ini.SetValue("Solar", "BrasTurbidityJun", SolarOptions.BrasTurbidityJun);
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
			NOAAconf.HeatThreshold = ini.GetValue("NOAA", "HeatingThreshold", Units.Temp == 0 ? 18.3 : 65);
			if (NOAAconf.HeatThreshold < -99 || NOAAconf.HeatThreshold > 150)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.HeatThreshold, resetting it");
				NOAAconf.HeatThreshold = Units.Temp == 0 ? 18.3 : 65;
				ini.SetValue("NOAA", "HeatingThreshold", NOAAconf.HeatThreshold);
				rewriteRequired = true;
			}
			NOAAconf.CoolThreshold = ini.GetValue("NOAA", "CoolingThreshold", Units.Temp == 0 ? 18.3 : 65);
			if (NOAAconf.CoolThreshold < -99 || NOAAconf.CoolThreshold > 150)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.CoolThreshold, resetting it");
				NOAAconf.CoolThreshold = Units.Temp == 0 ? 18.3 : 65;
				ini.SetValue("NOAA", "CoolingThreshold", NOAAconf.CoolThreshold);
				rewriteRequired = true;
			}
			NOAAconf.MaxTempComp1 = ini.GetValue("NOAA", "MaxTempComp1", Units.Temp == 0 ? 27 : 80);
			if (NOAAconf.MaxTempComp1 < -99 || NOAAconf.MaxTempComp1 > 150)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MaxTempComp1, resetting it");
				NOAAconf.MaxTempComp1 = Units.Temp == 0 ? 27 : 80;
				ini.SetValue("NOAA", "MaxTempComp1", NOAAconf.MaxTempComp1);
				rewriteRequired = true;
			}
			NOAAconf.MaxTempComp2 = ini.GetValue("NOAA", "MaxTempComp2", Units.Temp == 0 ? 0 : 32);
			if (NOAAconf.MaxTempComp2 < -99 || NOAAconf.MaxTempComp2 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MaxTempComp2, resetting it");
				NOAAconf.MaxTempComp2 = Units.Temp == 0 ? 0 : 32;
				ini.SetValue("NOAA", "MaxTempComp2", NOAAconf.MaxTempComp2);
				rewriteRequired = true;
			}
			NOAAconf.MinTempComp1 = ini.GetValue("NOAA", "MinTempComp1", Units.Temp == 0 ? 0 : 32);
			if (NOAAconf.MinTempComp1 < -99 || NOAAconf.MinTempComp1 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MinTempComp1, resetting it");
				NOAAconf.MinTempComp1 = Units.Temp == 0 ? 0 : 32;
				ini.SetValue("NOAA", "MinTempComp1", NOAAconf.MinTempComp1);
				rewriteRequired = true;
			}
			NOAAconf.MinTempComp2 = ini.GetValue("NOAA", "MinTempComp2", Units.Temp == 0 ? -18 : 0);
			if (NOAAconf.MinTempComp2 < -99 || NOAAconf.MinTempComp2 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MinTempComp2, resetting it");
				NOAAconf.MinTempComp2 = Units.Temp == 0 ? -18 : 0;
				ini.SetValue("NOAA", "MinTempComp2", NOAAconf.MinTempComp2);
				rewriteRequired = true;
			}
			NOAAconf.RainComp1 = ini.GetValue("NOAA", "RainComp1", Units.Rain == 0 ? 0.2 : 0.01);
			if (NOAAconf.RainComp1 < 0 || NOAAconf.RainComp1 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.RainComp1, resetting it");
				NOAAconf.RainComp1 = Units.Rain == 0 ? 0.2 : 0.01;
				ini.SetValue("NOAA", "RainComp1", NOAAconf.RainComp1);
				rewriteRequired = true;
			}
			NOAAconf.RainComp2 = ini.GetValue("NOAA", "RainComp2", Units.Rain == 0 ? 2 : 0.1);
			if (NOAAconf.RainComp2 < 0 || NOAAconf.RainComp2 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.RainComp2, resetting it");
				NOAAconf.RainComp2 = Units.Rain == 0 ? 2 : 0.1;
				ini.SetValue("NOAA", "RainComp2", NOAAconf.RainComp2);
				rewriteRequired = true;
			}
			NOAAconf.RainComp3 = ini.GetValue("NOAA", "RainComp3", Units.Rain == 0 ? 20 : 1);
			if (NOAAconf.RainComp3 < 0 || NOAAconf.RainComp3 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.RainComp3, resetting it");
				NOAAconf.RainComp3 = Units.Rain == 0 ? 20 : 1;
				ini.SetValue("NOAA", "RainComp3", NOAAconf.RainComp3);
				rewriteRequired = true;
			}

			NOAAconf.Create = ini.GetValue("NOAA", "AutoSave", false);
			NOAAconf.AutoFtp = ini.GetValue("NOAA", "AutoFTP", false);
			NOAAconf.FtpFolder = ini.GetValue("NOAA", "FTPDirectory", string.Empty);
			NOAAconf.AutoCopy = ini.GetValue("NOAA", "AutoCopy", false);
			NOAAconf.CopyFolder = ini.GetValue("NOAA", "CopyDirectory", string.Empty);
			NOAAconf.MonthFile = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
			// Check for Cumulus 1 default format - and update
			if (NOAAconf.MonthFile == "'NOAAMO'mmyy'.txt'" || NOAAconf.MonthFile == "\"NOAAMO\"mmyy\".txt\"")
			{
				LogMessage("Cumulus.ini: Updating old Cumulus 1 NOAA monthly file name");
				NOAAconf.MonthFile = "'NOAAMO'MMyy'.txt'";
				ini.SetValue("NOAA", "MonthFileFormat", NOAAconf.MonthFile);
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

			HTTPProxyName = ini.GetValue("Proxies", "HTTPProxyName", string.Empty);
			HTTPProxyPort = ini.GetValue("Proxies", "HTTPProxyPort", 0);
			HTTPProxyUser = ini.GetValue("Proxies", "HTTPProxyUser", string.Empty);
			HTTPProxyPassword = ini.GetValue("Proxies", "HTTPProxyPassword", string.Empty);

			NumWindRosePoints = ini.GetValue("Display", "NumWindRosePoints", 16, 4, 32);
			WindRoseAngle = 360.0 / NumWindRosePoints;
			DisplayOptions.UseApparent = ini.GetValue("Display", "UseApparent", false);
			DisplayOptions.ShowSolar = ini.GetValue("Display", "DisplaySolarData", false);
			DisplayOptions.ShowUV = ini.GetValue("Display", "DisplayUvData", false);
			DisplayOptions.ShowSnow = ini.GetValue("Display", "DisplaySnowData", false);

			// MySQL - common
			MySqlConnSettings.Server = ini.GetValue("MySQL", "Host", "127.0.0.1");
			MySqlConnSettings.Port = (uint) ini.GetValue("MySQL", "Port", 3306, 1, 65535);
			MySqlConnSettings.UserID = ini.GetValue("MySQL", "User", string.Empty);
			MySqlConnSettings.Password = ini.GetValue("MySQL", "Pass", string.Empty);
			MySqlConnSettings.Database = ini.GetValue("MySQL", "Database", "database");
			MySqlSettings.UpdateOnEdit = ini.GetValue("MySQL", "UpdateOnEdit", true);
			MySqlSettings.BufferOnfailure = ini.GetValue("MySQL", "BufferOnFailure", false);

			// MySQL - monthly log file
			MySqlSettings.Monthly.Enabled = ini.GetValue("MySQL", "MonthlyMySqlEnabled", false);
			MySqlSettings.Monthly.TableName = ini.GetValue("MySQL", "MonthlyTable", "Monthly");
			// MySQL - real-time
			MySqlSettings.Realtime.Enabled = ini.GetValue("MySQL", "RealtimeMySqlEnabled", false);
			MySqlSettings.Realtime.TableName = ini.GetValue("MySQL", "RealtimeTable", "Realtime");
			MySqlSettings.RealtimeRetention = ini.GetValue("MySQL", "RealtimeRetention", string.Empty);
			MySqlSettings.RealtimeLimit1Minute = ini.GetValue("MySQL", "RealtimeMySql1MinLimit", false) && RealtimeInterval < 60000; // do not enable if real time interval is greater than 1 minute
																																	 // MySQL - dayfile
			MySqlSettings.Dayfile.Enabled = ini.GetValue("MySQL", "DayfileMySqlEnabled", false);
			MySqlSettings.Dayfile.TableName = ini.GetValue("MySQL", "DayfileTable", "Dayfile");

			// MySQL - custom seconds
			MySqlSettings.CustomSecs.Commands[0] = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlSecondsCommandString" + i))
					MySqlSettings.CustomSecs.Commands[i] = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString" + i, string.Empty);
			}

			MySqlSettings.CustomSecs.Enabled = ini.GetValue("MySQL", "CustomMySqlSecondsEnabled", false);
			MySqlSettings.CustomSecs.Interval = ini.GetValue("MySQL", "CustomMySqlSecondsInterval", 10);
			if (MySqlSettings.CustomSecs.Interval < 1) { MySqlSettings.CustomSecs.Interval = 1; }

			// MySQL - custom minutes
			MySqlSettings.CustomMins.Enabled = ini.GetValue("MySQL", "CustomMySqlMinutesEnabled", false);

			MySqlSettings.CustomMins.Commands[0] = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString", string.Empty);
			MySqlSettings.CustomMins.IntervalIndexes[0] = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIndex", 6);
			if (MySqlSettings.CustomMins.IntervalIndexes[0] < 0 && MySqlSettings.CustomMins.IntervalIndexes[0] >= FactorsOf60.Length)
			{
				MySqlSettings.CustomMins.IntervalIndexes[0] = 6;
			}
			MySqlSettings.CustomMins.Intervals[0] = FactorsOf60[MySqlSettings.CustomMins.IntervalIndexes[0]];
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlMinutesCommandString" + i))
				{
					MySqlSettings.CustomMins.Commands[i] = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString" + i, string.Empty);
					MySqlSettings.CustomMins.IntervalIndexes[i] = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIdx" + i, MySqlSettings.CustomMins.IntervalIndexes[0]);
					if (MySqlSettings.CustomMins.IntervalIndexes[i] < 0 && MySqlSettings.CustomMins.IntervalIndexes[i] > FactorsOf60.Length)
					{
						MySqlSettings.CustomMins.IntervalIndexes[i] = 6;
					}
					MySqlSettings.CustomMins.Intervals[i] = FactorsOf60[MySqlSettings.CustomMins.IntervalIndexes[i]];
				}
			}


			// MySql - Timed
			MySqlSettings.CustomTimed.Enabled = ini.GetValue("MySQL", "CustomMySqlTimedEnabled", false);
			for (var i = 0; i < 10; i++)
			{
				MySqlSettings.CustomTimed.Commands[i] = ini.GetValue("MySQL", "CustomMySqlTimedCommandString" + i, string.Empty);
				MySqlSettings.CustomTimed.SetStartTime(i, ini.GetValue("MySQL", "CustomMySqlTimedStartTime" + i, "00:00"));
				MySqlSettings.CustomTimed.Intervals[i] = ini.GetValue("MySQL", "CustomMySqlTimedInterval" + i, 1440, 1);

				if (!string.IsNullOrEmpty(MySqlSettings.CustomTimed.Commands[i]) && MySqlSettings.CustomTimed.Intervals[i] < 1440)
					MySqlSettings.CustomTimed.SetNextInterval(i, DateTime.Now);
			}

			// MySQL - custom roll-over
			MySqlSettings.CustomRollover.Enabled = ini.GetValue("MySQL", "CustomMySqlRolloverEnabled", false);
			MySqlSettings.CustomRollover.Commands[0] = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlRolloverCommandString" + i))
					MySqlSettings.CustomRollover.Commands[i] = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString" + i, string.Empty);
			}

			// MySQL - custom start-up
			MySqlSettings.CustomStartUp.Enabled = ini.GetValue("MySQL", "CustomMySqlStartUpEnabled", false);
			MySqlSettings.CustomStartUp.Commands[0] = ini.GetValue("MySQL", "CustomMySqlStartUpCommandString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlStartUpCommandString" + i))
					MySqlSettings.CustomStartUp.Commands[i] = ini.GetValue("MySQL", "CustomMySqlStartUpCommandString" + i, string.Empty);
			}


			// Custom HTTP - seconds
			CustomHttpSecondsStrings[0] = ini.GetValue("HTTP", "CustomHttpSecondsString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpSecondsString" + i))
					CustomHttpSecondsStrings[i] = ini.GetValue("HTTP", "CustomHttpSecondsString" + i, string.Empty);
			}

			CustomHttpSecondsEnabled = ini.GetValue("HTTP", "CustomHttpSecondsEnabled", false);
			CustomHttpSecondsInterval = ini.GetValue("HTTP", "CustomHttpSecondsInterval", 10, 1);

			// Custom HTTP - minutes
			CustomHttpMinutesStrings[0] = ini.GetValue("HTTP", "CustomHttpMinutesString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpMinutesString" + i))
					CustomHttpMinutesStrings[i] = ini.GetValue("HTTP", "CustomHttpMinutesString" + i, string.Empty);
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
				ini.SetValue("HTTP", "CustomHttpMinutesIntervalIndex", CustomHttpMinutesIntervalIndex);
				rewriteRequired = true;
			}

			// Http - custom roll-over
			CustomHttpRolloverEnabled = ini.GetValue("HTTP", "CustomHttpRolloverEnabled", false);
			CustomHttpRolloverStrings[0] = ini.GetValue("HTTP", "CustomHttpRolloverString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpRolloverString" + i))
					CustomHttpRolloverStrings[i] = ini.GetValue("HTTP", "CustomHttpRolloverString" + i, string.Empty);
			}

			// Http files
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "HttpFileUrl" + i))
				{
					HttpFilesConfig[i].Enabled = ini.GetValue("HTTP", "HttpFileEnabled" + i, false);
					HttpFilesConfig[i].Url = System.Web.HttpUtility.UrlDecode(ini.GetValue("HTTP", "HttpFileUrl" + i, string.Empty));
					HttpFilesConfig[i].Remote = ini.GetValue("HTTP", "HttpFileRemote" + i, string.Empty);
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
				SelectaChartOptions.colours[i] = ini.GetValue("Select-a-Chart", "Colour" + i, string.Empty);
			}

			// Select-a-Period settings
			for (int i = 0; i < SelectaPeriodOptions.series.Length; i++)
			{
				SelectaPeriodOptions.series[i] = ini.GetValue("Select-a-Period", "Series" + i, "0");
				SelectaPeriodOptions.colours[i] = ini.GetValue("Select-a-Period", "Colour" + i, string.Empty);
			}

			// Email settings
			SmtpOptions.Enabled = ini.GetValue("SMTP", "Enabled", false);
			SmtpOptions.Server = ini.GetValue("SMTP", "ServerName", string.Empty);
			SmtpOptions.Port = ini.GetValue("SMTP", "Port", 587, 1, 65535);
			SmtpOptions.SslOption = ini.GetValue("SMTP", "SSLOption", 1);
			SmtpOptions.AuthenticationMethod = ini.GetValue("SMTP", "RequiresAuthentication", 0, 0, 1);
			SmtpOptions.User = ini.GetValue("SMTP", "User", string.Empty);
			SmtpOptions.Password = ini.GetValue("SMTP", "Password", string.Empty);
			SmtpOptions.IgnoreCertErrors = ini.GetValue("SMTP", "IgnoreCertErrors", false);

			// Growing Degree Days
			GrowingBase1 = ini.GetValue("GrowingDD", "BaseTemperature1", (Units.Temp == 0 ? 5.0 : 40.0));
			GrowingBase2 = ini.GetValue("GrowingDD", "BaseTemperature2", (Units.Temp == 0 ? 10.0 : 50.0));
			GrowingYearStarts = ini.GetValue("GrowingDD", "YearStarts", (Latitude >= 0 ? 1 : 7), 1, 12);
			GrowingCap30C = ini.GetValue("GrowingDD", "Cap30C", true);

			// Temperature Sum
			TempSumYearStarts = ini.GetValue("TempSum", "TempSumYearStart", (Latitude >= 0 ? 1 : 7), 1, 12);
			TempSumBase1 = ini.GetValue("TempSum", "BaseTemperature1", GrowingBase1);
			TempSumBase2 = ini.GetValue("TempSum", "BaseTemperature2", GrowingBase2);

			// Custom Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("CustomLogs", "DailyFilename" + i))
					CustomDailyLogSettings[i].FileName = ini.GetValue("CustomLogs", "DailyFilename" + i, string.Empty);

				if (ini.ValueExists("CustomLogs", "DailyContent" + i))
					CustomDailyLogSettings[i].ContentString = ini.GetValue("CustomLogs", "DailyContent" + i, string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);

				if (string.IsNullOrEmpty(CustomDailyLogSettings[i].FileName) || string.IsNullOrEmpty(CustomDailyLogSettings[i].ContentString))
					CustomDailyLogSettings[i].Enabled = false;
				else
					CustomDailyLogSettings[i].Enabled = ini.GetValue("CustomLogs", "DailyEnabled" + i, false);



				if (ini.ValueExists("CustomLogs", "IntervalFilename" + i))
					CustomIntvlLogSettings[i].FileName = ini.GetValue("CustomLogs", "IntervalFilename" + i, string.Empty);

				if (ini.ValueExists("CustomLogs", "IntervalContent" + i))
					CustomIntvlLogSettings[i].ContentString = ini.GetValue("CustomLogs", "IntervalContent" + i, string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);

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
						ini.SetValue("CustomLogs", "IntervalIdx" + i, DataLogInterval);
						rewriteRequired = true;
					}
				}
				else
				{
					CustomIntvlLogSettings[i].Interval = FactorsOf60[DataLogInterval];
					CustomIntvlLogSettings[i].IntervalIdx = DataLogInterval;
				}
			}

			// do we need to decrypt creds?
			if (ProgramOptions.EncryptedCreds)
			{
				if (!Program.CheckInstanceId(false))
				{
					/*
					 (ProgramOptions.SettingsUsername == string.Empty && ProgramOptions.SettingsPassword == string.Empty &&
						WllApiKey == string.Empty && WllApiSecret == string.Empty &&
						AirLinkApiKey == string.Empty && AirLinkApiSecret == string.Empty &&
						FtpOptions.Username == string.Empty &&
						FtpOptions.Password == string.Empty &&
						FtpOptions.PhpSecret == string.Empty &&
						Wund.PW == string.Empty &&
						Windy.ApiKey == string.Empty &&
						AWEKAS.PW == string.Empty &&
						WindGuru.PW == string.Empty &&
						WCloud.PW == string.Empty &&
						WCloud.PW == string.Empty &&
						WOW.PW == string.Empty &&
						.. etc
						)
					[
						LogWarningMessage("No UniqueId.txt file found, but no credentials are encrptyed so creating a new UniqueId.txt file")
						Program.CheckInstanceId(true)
					]
					*/

					LogCriticalMessage("ERROR: The UniqueId.txt file is missing or corrupt, please restore it from a backup");
					LogConsoleMessage("ERROR: The UniqueId.txt file is missing or corrupt, please restore it from a backup", ConsoleColor.Red);
					Environment.Exit(1);
				}

				ProgramOptions.SettingsUsername = Crypto.DecryptString(ProgramOptions.SettingsUsername, Program.InstanceId, "SettingsUsername");
				ProgramOptions.SettingsPassword = Crypto.DecryptString(ProgramOptions.SettingsPassword, Program.InstanceId, "SettingsPassword");
				WllApiKey = Crypto.DecryptString(WllApiKey, Program.InstanceId, "WllApiKey");
				WllApiSecret = Crypto.DecryptString(WllApiSecret, Program.InstanceId, "WllApiSecret");
				JsonStationOptions.MqttUsername = Crypto.DecryptString(JsonStationOptions.MqttUsername, Program.InstanceId, "JsonStationMqttUsername");
				JsonStationOptions.MqttPassword = Crypto.DecryptString(JsonStationOptions.MqttPassword, Program.InstanceId, "JsonStationMqttPassword");
				AirLinkApiKey = Crypto.DecryptString(AirLinkApiKey, Program.InstanceId, "AirLinkApiKey");
				AirLinkApiSecret = Crypto.DecryptString(AirLinkApiSecret, Program.InstanceId, "AirLinkApiSecret");
				FtpOptions.Username = Crypto.DecryptString(FtpOptions.Username, Program.InstanceId, "FtpOptions.Username");
				FtpOptions.Password = Crypto.DecryptString(FtpOptions.Password, Program.InstanceId, "FtpOptions.Password");
				FtpOptions.PhpSecret = Crypto.DecryptString(FtpOptions.PhpSecret, Program.InstanceId, "FtpOptions.PhpSecret");
				Wund.PW = Crypto.DecryptString(Wund.PW, Program.InstanceId, "Wund.PW");
				Windy.ApiKey = Crypto.DecryptString(Windy.ApiKey, Program.InstanceId, "Windy.ApiKey");
				AWEKAS.PW = Crypto.DecryptString(AWEKAS.PW, Program.InstanceId, "AWEKAS.PW");
				WindGuru.PW = Crypto.DecryptString(WindGuru.PW, Program.InstanceId, "WindGuru.PW");
				WCloud.PW = Crypto.DecryptString(WCloud.PW, Program.InstanceId, "WCloud.PW");
				PWS.PW = Crypto.DecryptString(PWS.PW, Program.InstanceId, "PWS.PW");
				WOW.PW = Crypto.DecryptString(WOW.PW, Program.InstanceId, "WOW.PW");
				if (APRS.PW != "-1")
				{
					APRS.PW = Crypto.DecryptString(APRS.PW, Program.InstanceId, "APRS.PW");
				}
				OpenWeatherMap.PW = Crypto.DecryptString(OpenWeatherMap.PW, Program.InstanceId, "OpenWeatherMap.PW");
				Bluesky.PW = Crypto.DecryptString(Bluesky.PW, Program.InstanceId, "Bluesky.PW");
				MQTT.Username = Crypto.DecryptString(MQTT.Username, Program.InstanceId, "MQTT.Username");
				MQTT.Password = Crypto.DecryptString(MQTT.Password, Program.InstanceId, "MQTT.Password");
				MySqlConnSettings.UserID = Crypto.DecryptString(MySqlConnSettings.UserID, Program.InstanceId, "MySql UserID");
				MySqlConnSettings.Password = Crypto.DecryptString(MySqlConnSettings.Password, Program.InstanceId, "MySql Password");
				SmtpOptions.User = Crypto.DecryptString(SmtpOptions.User, Program.InstanceId, "SmtpOptions.User");
				SmtpOptions.Password = Crypto.DecryptString(SmtpOptions.Password, Program.InstanceId, "SmtpOptions.Password");
				HTTPProxyUser = Crypto.DecryptString(HTTPProxyUser, Program.InstanceId, "HTTPProxyUser");
				HTTPProxyPassword = Crypto.DecryptString(HTTPProxyPassword, Program.InstanceId, "HTTPProxyPassword");
				EcowittApplicationKey = Crypto.DecryptString(EcowittApplicationKey, Program.InstanceId, "EcowittSettings.AppKey");
				EcowittUserApiKey = Crypto.DecryptString(EcowittUserApiKey, Program.InstanceId, "EcowittSettings.UserApiKey");
				EcowittHttpPassword = Crypto.DecryptString(EcowittHttpPassword, Program.InstanceId, "EcowittSettings.HttpPassword");

				LogMessage("Reading Cumulus.ini file completed");
			}
			else
			{
				LogMessage("Reading Cumulus.ini file completed");
				LogMessage("Encrypting Cumulus.ini...");

				try
				{
					Program.CheckInstanceId(true);
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "Error creating UniqueId.txt");
					Environment.Exit(1);
				}

				recreateRequired = true;
			}

			if (recreateRequired)
			{
				LogMessage("Deleting existing Cumulus.ini");
				try
				{
					File.Delete("Cumulus.ini");
					LogMessage("Cumulus.ini deleted");
					// Add a pause to allow the file to be deleted
					Thread.Sleep(1000);
				}
				catch (Exception ex)
				{
					LogErrorMessage("Error deleting Cumulus.ini: " + ex.Message);
				}

				WriteIniFile();
			}
			else if (rewriteRequired && File.Exists("Cumulus.ini"))
			{
				LogMessage("Some values in Cumulus.ini had invalid values, are obsolete, or new required entries have been created");
				LogMessage("Rewriting Cumulus.ini to reflect the new configuration");
				ini.Flush();
				LogMessage("Cumulus.ini rewrite complete");
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

			ini.SetValue("Program", "ErrorListLoggingLevel", (int) ErrorListLoggingLevel);

			ini.SetValue("Program", "SecureSettings", ProgramOptions.SecureSettings);
			ini.SetValue("Program", "SettingsUsername", Crypto.EncryptString(ProgramOptions.SettingsUsername, Program.InstanceId, "SettingsUsername"));
			ini.SetValue("Program", "SettingsPassword", Crypto.EncryptString(ProgramOptions.SettingsPassword, Program.InstanceId, "SettingsPassword"));

			ini.SetValue("Program", "EncryptedCreds", true);

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
			ini.SetValue("Station", "SnowAutomated", SnowAutomated);
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
			ini.SetValue("Station", "CalculatedSLP", StationOptions.CalculateSLP);

			ini.SetValue("Station", "RolloverHour", RolloverHour);
			ini.SetValue("Station", "Use10amInSummer", Use10amInSummer);
			//ini.SetValue("Station", "ConfirmClose", ConfirmClose)
			//ini.SetValue("Station", "CloseOnSuspend", CloseOnSuspend)
			//ini.SetValue("Station", "RestartIfUnplugged", RestartIfUnplugged)
			//ini.SetValue("Station", "RestartIfDataStops", RestartIfDataStops)
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
			ini.SetValue("Station", "SnowDepthUnit", Units.SnowDepth);
			ini.SetValue("Station", "LaserDistancehUnit", Units.LaserDistance);

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
			ini.SetValue("Station", "StartDateIso", RecordsBeganDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			ini.SetValue("Station", "YTDrain", YTDrain);
			ini.SetValue("Station", "YTDrainyear", YTDrainyear);
			ini.SetValue("Station", "UseDataLogger", StationOptions.UseDataLogger);
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

			ini.SetValue("Station", "EWtempdiff", Spike.TempDiff < 999 ? ConvertUnits.UserTempToC(Spike.TempDiff) : 999.0);
			ini.SetValue("Station", "EWpressurediff", Spike.PressDiff < 999 ? ConvertUnits.UserPressToMB(Spike.PressDiff) : 999.00);
			ini.SetValue("Station", "EWhumiditydiff", Spike.HumidityDiff);
			ini.SetValue("Station", "EWgustdiff", Spike.GustDiff < 999 ? ConvertUnits.UserWindToMS(Spike.GustDiff) : 999.0);
			ini.SetValue("Station", "EWwinddiff", Spike.WindDiff < 999 ? ConvertUnits.UserWindToMS(Spike.WindDiff) : 999.0);
			ini.SetValue("Station", "EWmaxHourlyRain", Spike.MaxHourlyRain < 999 ? ConvertUnits.UserRainToMM(Spike.MaxHourlyRain) : 999.0);
			ini.SetValue("Station", "EWmaxRainRate", Spike.MaxRainRate < 999 ? ConvertUnits.UserRainToMM(Spike.MaxRainRate) : 999.0);
			ini.SetValue("Station", "EWinTempdiff", Spike.InTempDiff < 999 ? ConvertUnits.UserTempToC(Spike.InTempDiff) : 999.0);
			ini.SetValue("Station", "EWinHumiditydiff", Spike.InHumDiff);

			ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
			ini.SetValue("Station", "RainWeekStart", RainWeekStart);
			ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

			ini.SetValue("Station", "ChillHourSeasonStart", ChillHourSeasonStart);
			ini.SetValue("Station", "ChillHourThreshold", ChillHourThreshold);
			ini.SetValue("Station", "ChillHourBase", ChillHourBase);

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

			ini.SetValue("Station", "WxnowComment.txt", WxnowComment);

			// WeatherLink Live device settings
			ini.SetValue("WLL", "AutoUpdateIpAddress", WLLAutoUpdateIpAddress);
			ini.SetValue("WLL", "WLv2ApiKey", Crypto.EncryptString(WllApiKey, Program.InstanceId, "WllApiKey"));
			ini.SetValue("WLL", "WLv2ApiSecret", Crypto.EncryptString(WllApiSecret, Program.InstanceId, "WllApiSecret"));
			ini.SetValue("WLL", "WLStationId", WllStationId);
			ini.SetValue("WLL", "WLStationUuid", WllStationUuid);
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
			ini.SetValue("GW1000", "UsePiezoIsRaining", EcowittIsRainingUsePiezo);
			ini.SetValue("GW1000", "ExtraSensorDataEnabled", EcowittExtraEnabled);
			ini.SetValue("GW1000", "ExtraCloudSensorDataEnabled", EcowittCloudExtraEnabled);
			ini.SetValue("GW1000", "SetCustomServer", EcowittSetCustomServer);
			ini.SetValue("GW1000", "EcowittGwAddr", EcowittGatewayAddr);
			ini.SetValue("GW1000", "EcowittLocalAddr", EcowittLocalAddr);
			ini.SetValue("GW1000", "EcowittCustomInterval", EcowittCustomInterval);
			ini.SetValue("GW1000", "ExtraSetCustomServer", EcowittExtraSetCustomServer);
			ini.SetValue("GW1000", "EcowittExtraGwAddr", EcowittExtraGatewayAddr);
			ini.SetValue("GW1000", "EcowittExtraLocalAddr", EcowittExtraLocalAddr);
			ini.SetValue("GW1000", "EcowittExtraCustomInterval", EcowittExtraCustomInterval);

			ini.SetValue("GW1000", "HttpPassword", Crypto.EncryptString(EcowittHttpPassword, Program.InstanceId, "EcowittSettings.HttpPassword"));

			// api
			ini.SetValue("GW1000", "EcowittAppKey", Crypto.EncryptString(EcowittApplicationKey, Program.InstanceId, "EcowittSettings.AppKey"));
			ini.SetValue("GW1000", "EcowittUserKey", Crypto.EncryptString(EcowittUserApiKey, Program.InstanceId, "EcowittSettings.UserApiKey"));
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
			// extra forwarders
			ini.SetValue("GW1000", "ExtraUseMainForwarders", EcowittExtraUseMainForwarders);
			for (int i = 0; i < EcowittExtraForwarders.Length; i++)
			{
				if (string.IsNullOrEmpty(EcowittExtraForwarders[i]))
					ini.DeleteValue("GW1000", "ExtraForwarder" + i);
				else
					ini.SetValue("GW1000", "ExtraForwarder" + i, EcowittExtraForwarders[i].ToString());
			}

			// Ambient settings
			ini.SetValue("Ambient", "ExtraSensorDataEnabled", AmbientExtraEnabled);
			ini.SetValue("Ambient", "ExtraSensorUseSolar", AmbientExtraUseSolar);
			ini.SetValue("Ambient", "ExtraSensorUseUv", AmbientExtraUseUv);
			ini.SetValue("Ambient", "ExtraSensorUseTempHum", AmbientExtraUseTempHum);
			ini.SetValue("Ambient", "ExtraSensorUseSoilTemp", AmbientExtraUseSoilTemp);
			ini.SetValue("Ambient", "ExtraSensorUseSoilMoist", AmbientExtraUseSoilMoist);
			ini.SetValue("Ambient", "ExtraSensorUseAQI", AmbientExtraUseAQI);
			ini.SetValue("Ambient", "ExtraSensorUseCo2", AmbientExtraUseCo2);
			ini.SetValue("Ambient", "ExtraSensorUseLightning", AmbientExtraUseLightning);
			ini.SetValue("Ambient", "ExtraSensorUseLeak", AmbientExtraUseLeak);

			// JSON station options
			ini.SetValue("JsonStation", "ConnectionType", JsonStationOptions.Connectiontype);
			ini.SetValue("JsonStation", "SourceFile", JsonStationOptions.SourceFile);
			ini.SetValue("JsonStation", "FileDelay", JsonStationOptions.FileReadDelay);
			ini.SetValue("JsonStation", "MqttServer", JsonStationOptions.MqttServer);
			ini.SetValue("JsonStation", "MqttServerPort", JsonStationOptions.MqttPort);
			ini.SetValue("JsonStation", "MqttUsername", Crypto.EncryptString(JsonStationOptions.MqttUsername, Program.InstanceId, "JsonStationMqttUsername"));
			ini.SetValue("JsonStation", "MqttPassword", Crypto.EncryptString(JsonStationOptions.MqttPassword, Program.InstanceId, "JsonStationMqttPassword"));
			ini.SetValue("JsonStation", "MqttUseTls", JsonStationOptions.MqttUseTls);
			ini.SetValue("JsonStation", "MqttTopic", JsonStationOptions.MqttTopic);
			// JSON station Extra Sensors
			ini.SetValue("JsonExtraStation", "ExtraSensorDataEnabled", JsonExtraStationOptions.ExtraSensorsEnabled);
			ini.SetValue("JsonExtraStation", "ConnectionType", JsonExtraStationOptions.Connectiontype);
			ini.SetValue("JsonExtraStation", "SourceFile", JsonExtraStationOptions.SourceFile);
			ini.SetValue("JsonExtraStation", "FileDelay", JsonExtraStationOptions.FileReadDelay);
			ini.SetValue("JsonExtraStation", "MqttServer", JsonExtraStationOptions.MqttServer);
			ini.SetValue("JsonExtraStation", "MqttServerPort", JsonExtraStationOptions.MqttPort);
			ini.SetValue("JsonExtraStation", "MqttUsername", JsonExtraStationOptions.MqttUsername);
			ini.SetValue("JsonExtraStation", "MqttPassword", JsonExtraStationOptions.MqttPassword);
			ini.SetValue("JsonExtraStation", "MqttUseTls", JsonExtraStationOptions.MqttUseTls);
			ini.SetValue("JsonExtraStation", "MqttTopic", JsonExtraStationOptions.MqttTopic);

			// Extra Sensor Settings
			ini.SetValue("ExtraSensors", "ExtraSensorUseSolar", ExtraSensorUseSolar);
			ini.SetValue("ExtraSensors", "ExtraSensorUseUv", ExtraSensorUseUv);
			ini.SetValue("ExtraSensors", "ExtraSensorUseTempHum", ExtraSensorUseTempHum);
			ini.SetValue("ExtraSensors", "ExtraSensorUseSoilTemp", ExtraSensorUseSoilTemp);
			ini.SetValue("ExtraSensors", "ExtraSensorUseSoilMoist", ExtraSensorUseSoilMoist);
			ini.SetValue("ExtraSensors", "ExtraSensorUseLeafWet", ExtraSensorUseLeafWet);
			ini.SetValue("ExtraSensors", "ExtraSensorUseUserTemp", ExtraSensorUseUserTemp);
			ini.SetValue("ExtraSensors", "ExtraSensorUseAQI", ExtraSensorUseAQI);
			ini.SetValue("ExtraSensors", "ExtraSensorUseCo2", ExtraSensorUseCo2);
			ini.SetValue("ExtraSensors", "ExtraSensorUseLightning", ExtraSensorUseLightning);
			ini.SetValue("ExtraSensors", "ExtraSensorUseLeak", ExtraSensorUseLeak);
			ini.SetValue("ExtraSensors", "ExtraSensorUseCamera", ExtraSensorUseCamera);
			ini.SetValue("ExtraSensors", "ExtraSensorUseLaserDist", ExtraSensorUseLaserDist);

			// AirLink settings
			ini.SetValue("AirLink", "IsWllNode", AirLinkIsNode);
			ini.SetValue("AirLink", "WLv2ApiKey", Crypto.EncryptString(AirLinkApiKey, Program.InstanceId, "AirLinkApiKey"));
			ini.SetValue("AirLink", "WLv2ApiSecret", Crypto.EncryptString(AirLinkApiSecret, Program.InstanceId, "AirLinkApiSecret"));
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
			ini.SetValue("FTP site", "Username", Crypto.EncryptString(FtpOptions.Username, Program.InstanceId, "FtpOptions.Username"));
			ini.SetValue("FTP site", "Password", Crypto.EncryptString(FtpOptions.Password, Program.InstanceId, "FtpOptions.Password"));
			ini.SetValue("FTP site", "Directory", FtpOptions.Directory);

			//ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);  // Deprecated - now read-only
			ini.SetValue("FTP site", "Sslftp", (int) FtpOptions.FtpMode);
			// BUILD 3092 - added alternate SFTP authentication options
			ini.SetValue("FTP site", "SshFtpAuthentication", FtpOptions.SshAuthen);
			ini.SetValue("FTP site", "SshFtpPskFile", FtpOptions.SshPskFile);

			ini.SetValue("FTP site", "ConnectionAutoDetect", FtpOptions.AutoDetect);
			ini.SetValue("FTP site", "IgnoreCertErrors", FtpOptions.IgnoreCertErrors);

			ini.SetValue("FTP site", "FTPlogging", FtpOptions.Logging);
			ini.SetValue("FTP site", "FTPloggingLevel", FtpOptions.LoggingLevel);
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
			ini.SetValue("FTP site", "PHP-Secret", Crypto.EncryptString(FtpOptions.PhpSecret, Program.InstanceId, "FtpOptions.PhpSecret"));
			ini.SetValue("FTP site", "PHP-IgnoreCertErrors", FtpOptions.PhpIgnoreCertErrors);
			ini.SetValue("FTP site", "PHP-UseGet", FtpOptions.PhpUseGet);
			ini.SetValue("FTP site", "PHP-UseBrotli", FtpOptions.PhpUseBrotli);
			ini.SetValue("FTP site", "MaxConcurrentUploads", FtpOptions.MaxConcurrentUploads);

			for (int i = 0; i < numextrafiles; i++)
			{
				if (string.IsNullOrEmpty(ExtraFiles[i].local) && string.IsNullOrEmpty(ExtraFiles[i].remote))
				{
					ini.DeleteValue("FTP site", "ExtraEnable" + i);
					ini.DeleteValue("FTP site", "ExtraLocal" + i);
					ini.DeleteValue("FTP site", "ExtraRemote" + i);
					ini.DeleteValue("FTP site", "ExtraProcess" + i);
					ini.DeleteValue("FTP site", "ExtraBinary" + i);
					ini.DeleteValue("FTP site", "ExtraRealtime" + i);
					ini.DeleteValue("FTP site", "ExtraFTP" + i);
					ini.DeleteValue("FTP site", "ExtraUTF" + i);
					ini.DeleteValue("FTP site", "ExtraEOD" + i);
					ini.DeleteValue("FTP site", "ExtraIncLogFile" + i);
				}
				else
				{
					ini.SetValue("FTP site", "ExtraEnable" + i, ExtraFiles[i].enable);
					ini.SetValue("FTP site", "ExtraLocal" + i, ExtraFiles[i].local);
					ini.SetValue("FTP site", "ExtraRemote" + i, ExtraFiles[i].remote);
					ini.SetValue("FTP site", "ExtraProcess" + i, ExtraFiles[i].process);
					ini.SetValue("FTP site", "ExtraBinary" + i, ExtraFiles[i].binary);
					ini.SetValue("FTP site", "ExtraRealtime" + i, ExtraFiles[i].realtime);
					ini.SetValue("FTP site", "ExtraFTP" + i, ExtraFiles[i].FTP);
					ini.SetValue("FTP site", "ExtraUTF" + i, ExtraFiles[i].UTF8);
					ini.SetValue("FTP site", "ExtraEOD" + i, ExtraFiles[i].endofday);
					ini.SetValue("FTP site", "ExtraIncLogFile" + i, ExtraFiles[i].incrementalLogfile);
				}
			}

			ini.SetValue("Station", "CloudBaseInFeet", CloudBaseInFeet);

			ini.SetValue("Wunderground", "ID", Wund.ID);
			ini.SetValue("Wunderground", "Password", Crypto.EncryptString(Wund.PW, Program.InstanceId, "Wund.PW"));
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

			ini.SetValue("Windy", "APIkey", Crypto.EncryptString(Windy.ApiKey, Program.InstanceId, "Windy.ApiKey"));
			ini.SetValue("Windy", "StationIdx", Windy.StationIdx);
			ini.SetValue("Windy", "Enabled", Windy.Enabled);
			ini.SetValue("Windy", "Interval", Windy.Interval);
			ini.SetValue("Windy", "SendUV", Windy.SendUV);
			ini.SetValue("Windy", "CatchUp", Windy.CatchUp);

			ini.SetValue("Awekas", "User", AWEKAS.ID);
			ini.SetValue("Awekas", "Password", Crypto.EncryptString(AWEKAS.PW, Program.InstanceId, "AWEKAS.PW"));
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
			ini.SetValue("WeatherCloud", "Key", Crypto.EncryptString(WCloud.PW, Program.InstanceId, "WCloud.PW"));
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
			ini.SetValue("PWSweather", "Password", Crypto.EncryptString(PWS.PW, Program.InstanceId, "PWS.PW"));
			ini.SetValue("PWSweather", "Enabled", PWS.Enabled);
			ini.SetValue("PWSweather", "Interval", PWS.Interval);
			ini.SetValue("PWSweather", "SendUV", PWS.SendUV);
			ini.SetValue("PWSweather", "SendSR", PWS.SendSolar);
			ini.SetValue("PWSweather", "CatchUp", PWS.CatchUp);

			ini.SetValue("WOW", "ID", WOW.ID);
			ini.SetValue("WOW", "Password", Crypto.EncryptString(WOW.PW, Program.InstanceId, "WOW.PW"));
			ini.SetValue("WOW", "Enabled", WOW.Enabled);
			ini.SetValue("WOW", "Interval", WOW.Interval);
			ini.SetValue("WOW", "SendUV", WOW.SendUV);
			ini.SetValue("WOW", "SendSR", WOW.SendSolar);
			ini.SetValue("WOW", "SendSoilTemp", WOW.SendSoilTemp);
			ini.SetValue("WOW", "SoilTempSensor", WOW.SoilTempSensor);
			ini.SetValue("WOW", "CatchUp", WOW.CatchUp);

			ini.SetValue("APRS", "ID", APRS.ID);
			if (APRS.PW == "-1")
			{
				ini.SetValue("APRS", "pass", APRS.PW);
			}
			else
			{
				ini.SetValue("APRS", "pass", Crypto.EncryptString(APRS.PW, Program.InstanceId, "APRS.PW"));
			}
			ini.SetValue("APRS", "server", APRS.Server);
			ini.SetValue("APRS", "port", APRS.Port);
			ini.SetValue("APRS", "Enabled", APRS.Enabled);
			ini.SetValue("APRS", "Interval", APRS.Interval);
			ini.SetValue("APRS", "SendSR", APRS.SendSolar);
			ini.SetValue("APRS", "APRSHumidityCutoff", APRS.HumidityCutoff);
			ini.SetValue("APRS", "UseUtcInWxNowFile", APRS.UseUtcInWxNowFile);

			ini.SetValue("OpenWeatherMap", "Enabled", OpenWeatherMap.Enabled);
			ini.SetValue("OpenWeatherMap", "CatchUp", OpenWeatherMap.CatchUp);
			ini.SetValue("OpenWeatherMap", "APIkey", Crypto.EncryptString(OpenWeatherMap.PW, Program.InstanceId, "OpenWeatherMap.PW"));
			ini.SetValue("OpenWeatherMap", "StationId", OpenWeatherMap.ID);
			ini.SetValue("OpenWeatherMap", "Interval", OpenWeatherMap.Interval);

			ini.SetValue("WindGuru", "Enabled", WindGuru.Enabled);
			ini.SetValue("WindGuru", "StationUID", WindGuru.ID);
			ini.SetValue("WindGuru", "Password", Crypto.EncryptString(WindGuru.PW, Program.InstanceId, "WindGuru.PW"));
			ini.SetValue("WindGuru", "Interval", WindGuru.Interval);
			ini.SetValue("WindGuru", "SendRain", WindGuru.SendRain);

			ini.SetValue("Bluesky", "Enabled", Bluesky.Enabled);
			ini.SetValue("Bluesky", "ID", Bluesky.ID);
			ini.SetValue("Bluesky", "Password", Crypto.EncryptString(Bluesky.PW, Program.InstanceId, "Bluesky.PW"));
			ini.SetValue("Bluesky", "Interval", Bluesky.Interval);
			ini.SetValue("Bluesky", "Language", Bluesky.Language);
			ini.SetValue("Bluesky", "BaseUrl", Bluesky.BaseUrl);
			for (var i = 0; i < Bluesky.TimedPostsTime.Length; i++)
			{
				if (Bluesky.TimedPostsTime[i] < TimeSpan.MaxValue)
					ini.SetValue("Bluesky", "TimedPost" + i, Bluesky.TimedPostsTime[i].ToString(@"hh\:mm"));
				else
					ini.DeleteValue("Bluesky", "TimedPost" + i);
			}
			for (var i = 0; i < Bluesky.TimedPostsFile.Length; i++)
			{
				if (!string.IsNullOrEmpty(Bluesky.TimedPostsFile[i]))
					ini.SetValue("Bluesky", "TimedPostFile" + i, Bluesky.TimedPostsFile[i]);
				else
					ini.DeleteValue("Bluesky", "TimedPostFile" + i);
			}
			for (var i = 0; i < Bluesky.VariablePostsTime.Length; i++)
			{
				if (string.IsNullOrEmpty(Bluesky.VariablePostsTime[i]))
				{
					ini.DeleteValue("Bluesky", "VariablePost" + i);
					ini.DeleteValue("Bluesky", "VariablePostFile" + i);
				}
				else
				{
					ini.SetValue("Bluesky", "VariablePost" + i, Bluesky.VariablePostsTime[i]);
					ini.SetValue("Bluesky", "VariablePostFile" + i, Bluesky.VariablePostsFile[i]);
				}
			}

			ini.SetValue("MQTT", "Server", MQTT.Server);
			ini.SetValue("MQTT", "Port", MQTT.Port);
			ini.SetValue("MQTT", "UseTLS", MQTT.UseTLS);
			ini.SetValue("MQTT", "Username", Crypto.EncryptString(MQTT.Username, Program.InstanceId, "MQTT.Username,"));
			ini.SetValue("MQTT", "Password", Crypto.EncryptString(MQTT.Password, Program.InstanceId, "MQTT.Password"));
			ini.SetValue("MQTT", "EnableDataUpdate", MQTT.EnableDataUpdate);
			ini.SetValue("MQTT", "UpdateTemplate", MQTT.UpdateTemplate);
			ini.SetValue("MQTT", "EnableInterval", MQTT.EnableInterval);
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
			ini.SetValue("Alarms", "LowTempAlarmBlueskyFile", LowTempAlarm.BskyFile);


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
			ini.SetValue("Alarms", "HighTempAlarmBlueskyFile", HighTempAlarm.BskyFile);

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
			ini.SetValue("Alarms", "TempChangeAlarmBlueskyFile", TempChangeAlarm.BskyFile);

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
			ini.SetValue("Alarms", "LowPressAlarmBlueskyFile", LowPressAlarm.BskyFile);

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
			ini.SetValue("Alarms", "HighPressAlarmBlueskyFile", HighPressAlarm.BskyFile);

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
			ini.SetValue("Alarms", "PressChangeAlarmBlueskyFile", PressChangeAlarm.BskyFile);

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
			ini.SetValue("Alarms", "HighRainRateAlarmBlueskyFile", HighRainRateAlarm.BskyFile);

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
			ini.SetValue("Alarms", "IsRainingAlarmBlueskyFile", IsRainingAlarm.BskyFile);

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
			ini.SetValue("Alarms", "HighGustAlarmBlueskyFile", HighGustAlarm.BskyFile);

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
			ini.SetValue("Alarms", "HighWindAlarmBlueskyFile", HighWindAlarm.BskyFile);

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
			ini.SetValue("Alarms", "SensorAlarmBlueskyFile", SensorAlarm.BskyFile);

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
			ini.SetValue("Alarms", "DataStoppedAlarmBlueskyFile", DataStoppedAlarm.BskyFile);

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
			ini.SetValue("Alarms", "BatteryLowAlarmBlueskyFile", BatteryLowAlarm.BskyFile);

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
			ini.SetValue("Alarms", "DataSpikeAlarmBlueskyFile", SpikeAlarm.BskyFile);

			ini.SetValue("Alarms", "UpgradeAlarmSet", UpgradeAlarm.Enabled);
			ini.SetValue("Alarms", "UpgradeAlarmSound", UpgradeAlarm.Sound);
			ini.SetValue("Alarms", "UpgradeAlarmSoundFile", UpgradeAlarm.SoundFile);
			ini.SetValue("Alarms", "UpgradeAlarmNotify", UpgradeAlarm.Notify);
			ini.SetValue("Alarms", "UpgradeAlarmEmail", UpgradeAlarm.Email);
			ini.SetValue("Alarms", "UpgradeAlarmLatch", UpgradeAlarm.Latch);
			ini.SetValue("Alarms", "UpgradeAlarmLatchHours", UpgradeAlarm.LatchHours);
			ini.SetValue("Alarms", "UpgradeAlarmAction", UpgradeAlarm.Action);
			ini.SetValue("Alarms", "UpgradeAlarmActionParams", UpgradeAlarm.ActionParams);
			ini.SetValue("Alarms", "UpgradeAlarmBlueskyFile", UpgradeAlarm.BskyFile);

			ini.SetValue("Alarms", "FirmwareAlarmSet", FirmwareAlarm.Enabled);
			ini.SetValue("Alarms", "FirmwareAlarmSound", FirmwareAlarm.Sound);
			ini.SetValue("Alarms", "FirmwareAlarmSoundFile", FirmwareAlarm.SoundFile);
			ini.SetValue("Alarms", "FirmwareAlarmNotify", FirmwareAlarm.Notify);
			ini.SetValue("Alarms", "FirmwareAlarmEmail", FirmwareAlarm.Email);
			ini.SetValue("Alarms", "FirmwareAlarmLatch", FirmwareAlarm.Latch);
			ini.SetValue("Alarms", "FirmwareAlarmLatchHours", FirmwareAlarm.LatchHours);
			ini.SetValue("Alarms", "FirmwareAlarmAction", FirmwareAlarm.Action);
			ini.SetValue("Alarms", "FirmwareAlarmActionParams", FirmwareAlarm.ActionParams);
			ini.SetValue("Alarms", "FirmwareAlarmBlueskyFile", FirmwareAlarm.BskyFile);

			ini.SetValue("Alarms", "HttpUploadAlarmSet", ThirdPartyAlarm.Enabled);
			ini.SetValue("Alarms", "HttpUploadAlarmSound", ThirdPartyAlarm.Sound);
			ini.SetValue("Alarms", "HttpUploadAlarmSoundFile", ThirdPartyAlarm.SoundFile);
			ini.SetValue("Alarms", "HttpUploadAlarmNotify", ThirdPartyAlarm.Notify);
			ini.SetValue("Alarms", "HttpUploadAlarmEmail", ThirdPartyAlarm.Email);
			ini.SetValue("Alarms", "HttpUploadAlarmLatch", ThirdPartyAlarm.Latch);
			ini.SetValue("Alarms", "HttpUploadAlarmLatchHours", ThirdPartyAlarm.LatchHours);
			ini.SetValue("Alarms", "HttpUploadAlarmTriggerCount", ThirdPartyAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "HttpUploadAlarmAction", ThirdPartyAlarm.Action);
			ini.SetValue("Alarms", "HttpUploadAlarmActionParams", ThirdPartyAlarm.ActionParams);
			ini.SetValue("Alarms", "HttpUploadAlarmBlueskyFile", ThirdPartyAlarm.BskyFile);

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
			ini.SetValue("Alarms", "MySqlUploadAlarmBlueskyFile", MySqlUploadAlarm.BskyFile);

			ini.SetValue("Alarms", "NewRecordAlarmSet", NewRecordAlarm.Enabled);
			ini.SetValue("Alarms", "NewRecordAlarmSound", NewRecordAlarm.Sound);
			ini.SetValue("Alarms", "NewRecordAlarmSoundFile", NewRecordAlarm.SoundFile);
			ini.SetValue("Alarms", "NewRecordAlarmNotify", NewRecordAlarm.Notify);
			ini.SetValue("Alarms", "NewRecordAlarmEmail", NewRecordAlarm.Email);
			ini.SetValue("Alarms", "NewRecordAlarmLatch", NewRecordAlarm.Latch);
			ini.SetValue("Alarms", "NewRecordAlarmLatchHours", NewRecordAlarm.LatchHours);
			ini.SetValue("Alarms", "NewRecordAlarmAction", NewRecordAlarm.Action);
			ini.SetValue("Alarms", "NewRecordAlarmActionParams", NewRecordAlarm.ActionParams);
			ini.SetValue("Alarms", "NewRecordAlarmBlueskyFile", NewRecordAlarm.BskyFile);

			ini.SetValue("Alarms", "FtpAlarmSet", FtpAlarm.Enabled);
			ini.SetValue("Alarms", "FtpAlarmSound", FtpAlarm.Sound);
			ini.SetValue("Alarms", "FtpAlarmSoundFile", FtpAlarm.SoundFile);
			ini.SetValue("Alarms", "FtpAlarmNotify", FtpAlarm.Notify);
			ini.SetValue("Alarms", "FtpAlarmEmail", FtpAlarm.Email);
			ini.SetValue("Alarms", "FtpAlarmLatch", FtpAlarm.Latch);
			ini.SetValue("Alarms", "FtpAlarmLatchHours", FtpAlarm.LatchHours);
			ini.SetValue("Alarms", "FtpAlarmAction", FtpAlarm.Action);
			ini.SetValue("Alarms", "FtpAlarmActionParams", FtpAlarm.ActionParams);
			ini.SetValue("Alarms", "FtpAlarmBlueskyFile", FtpAlarm.BskyFile);

			ini.SetValue("Alarms", "FromEmail", AlarmFromEmail);
			ini.SetValue("Alarms", "DestEmail", string.Join(";", AlarmDestEmail));
			ini.SetValue("Alarms", "UseHTML", AlarmEmailHtml);
			ini.SetValue("Alarms", "UseBCC", AlarmEmailUseBcc);

			// User Alarms
			for (var i = 0; i < UserAlarms.Count; i++)
			{
				ini.SetValue("UserAlarms", "AlarmName" + i, UserAlarms[i].Name);
				ini.SetValue("UserAlarms", "AlarmTag" + i, UserAlarms[i].WebTag);
				ini.SetValue("UserAlarms", "AlarmType" + i, UserAlarms[i].Type);
				ini.SetValue("UserAlarms", "AlarmValue" + i, UserAlarms[i].Value);
				ini.SetValue("UserAlarms", "AlarmEnabled" + i, UserAlarms[i].Enabled);
				ini.SetValue("UserAlarms", "AlarmEmail" + i, UserAlarms[i].Email);
				ini.SetValue("UserAlarms", "AlarmEmailMsg" + i, UserAlarms[i].EmailMsg);
				ini.SetValue("UserAlarms", "AlarmBlueskyFile" + i, UserAlarms[i].BskyFile);
				ini.SetValue("UserAlarms", "AlarmLatch" + i, UserAlarms[i].Latch);
				ini.SetValue("UserAlarms", "AlarmLatchHours" + i, UserAlarms[i].LatchHours);
				ini.SetValue("UserAlarms", "AlarmAction" + i, UserAlarms[i].Action);
				ini.SetValue("UserAlarms", "AlarmActionParams" + i, UserAlarms[i].ActionParams);
			}
			// remove any old alarms
			for (var i = UserAlarms.Count; i < 10; i++)
			{
				ini.DeleteValue("UserAlarms", "AlarmName" + i);
				ini.DeleteValue("UserAlarms", "AlarmTag" + i);
				ini.DeleteValue("UserAlarms", "AlarmType" + i);
				ini.DeleteValue("UserAlarms", "AlarmValue" + i);
				ini.DeleteValue("UserAlarms", "AlarmEnabled" + i);
				ini.DeleteValue("UserAlarms", "AlarmEmail" + i);
				ini.DeleteValue("UserAlarms", "AlarmEmailMsg" + i);
				ini.DeleteValue("UserAlarms", "AlarmLatch" + i);
				ini.DeleteValue("UserAlarms", "AlarmLatchHours" + i);
				ini.DeleteValue("UserAlarms", "AlarmAction" + i);
				ini.DeleteValue("UserAlarms", "AlarmActionParams" + i);
			}

			ini.SetValue("Offsets", "PressOffset", Calib.Press.Offset);
			ini.SetValue("Offsets", "PressStnOffset", Calib.PressStn.Offset);
			ini.SetValue("Offsets", "TempOffset", Calib.Temp.Offset);
			ini.SetValue("Offsets", "HumOffset", Calib.Hum.Offset);
			ini.SetValue("Offsets", "WindDirOffset", Calib.WindDir.Offset);
			ini.SetValue("Offsets", "UVOffset", Calib.UV.Offset);
			ini.SetValue("Offsets", "SolarOffset", Calib.Solar.Offset);
			ini.SetValue("Offsets", "WetBulbOffset", Calib.WetBulb.Offset);
			ini.SetValue("Offsets", "InTempOffset", Calib.InTemp.Offset);
			ini.SetValue("Offsets", "InHumOffset", Calib.InHum.Offset);

			ini.SetValue("Offsets", "PressMult", Calib.Press.Mult);
			ini.SetValue("Offsets", "PressStnMult", Calib.PressStn.Mult);
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
			ini.SetValue("Offsets", "PressStnMult2", Calib.PressStn.Mult2);
			ini.SetValue("Offsets", "WindSpeedMult2", Calib.WindSpeed.Mult2);
			ini.SetValue("Offsets", "WindGustMult2", Calib.WindGust.Mult2);
			ini.SetValue("Offsets", "TempMult2", Calib.Temp.Mult2);
			ini.SetValue("Offsets", "HumMult2", Calib.Hum.Mult2);
			ini.SetValue("Offsets", "InTempMult2", Calib.InTemp.Mult2);
			ini.SetValue("Offsets", "InHumMult2", Calib.InHum.Mult2);
			ini.SetValue("Offsets", "SolarMult2", Calib.Solar.Mult2);
			ini.SetValue("Offsets", "UVMult2", Calib.UV.Mult2);

			ini.SetValue("Limits", "TempHighC", ConvertUnits.UserTempToC(Limit.TempHigh));
			ini.SetValue("Limits", "TempLowC", ConvertUnits.UserTempToC(Limit.TempLow));
			ini.SetValue("Limits", "DewHighC", ConvertUnits.UserTempToC(Limit.DewHigh));
			ini.SetValue("Limits", "PressHighMB", ConvertUnits.UserPressToMB(Limit.PressHigh));
			ini.SetValue("Limits", "PressLowMB", ConvertUnits.UserPressToMB(Limit.PressLow));
			ini.SetValue("Limits", "WindHighMS", ConvertUnits.UserWindToMS(Limit.WindHigh));

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
			ini.SetValue("Proxies", "HTTPProxyUser", Crypto.EncryptString(HTTPProxyUser, Program.InstanceId, "HTTPProxyUser"));
			ini.SetValue("Proxies", "HTTPProxyPassword", Crypto.EncryptString(HTTPProxyPassword, Program.InstanceId, "HTTPProxyPassword"));

			ini.SetValue("Display", "NumWindRosePoints", NumWindRosePoints);
			ini.SetValue("Display", "UseApparent", DisplayOptions.UseApparent);
			ini.SetValue("Display", "DisplaySolarData", DisplayOptions.ShowSolar);
			ini.SetValue("Display", "DisplayUvData", DisplayOptions.ShowUV);
			ini.SetValue("Display", "DisplaySnowData", DisplayOptions.ShowSnow);

			ini.SetValue("Graphs", "ChartMaxDays", GraphDays);
			ini.SetValue("Graphs", "GraphHours", GraphHours);
			ini.SetValue("Graphs", "MoonImageEnabled", MoonImage.Enabled);
			ini.SetValue("Graphs", "MoonImageSize", MoonImage.Size);
			ini.SetValue("Graphs", "MoonImageShadeTransparent", MoonImage.Transparent);
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
			ini.SetValue("Graphs", "ChillHoursVisible", GraphOptions.Visible.ChillHours.Val);
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
			ini.SetValue("Graphs", "SnowDepth", GraphOptions.Visible.SnowDepth.Val);
			ini.SetValue("Graphs", "Snow24h", GraphOptions.Visible.Snow24h.Val);

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
			ini.SetValue("GraphColours", "SnowDepthColour", GraphOptions.Colour.SnowDepth);
			ini.SetValue("GraphColours", "Snow24hColour", GraphOptions.Colour.Snow24h);


			ini.SetValue("MySQL", "Host", MySqlConnSettings.Server);
			ini.SetValue("MySQL", "Port", (int) MySqlConnSettings.Port);
			ini.SetValue("MySQL", "User", Crypto.EncryptString(MySqlConnSettings.UserID, Program.InstanceId, "MySql UserID"));
			ini.SetValue("MySQL", "Pass", Crypto.EncryptString(MySqlConnSettings.Password, Program.InstanceId, "MySql Password"));
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
			ini.SetValue("MySQL", "CustomMySqlStartUpEnabled", MySqlSettings.CustomStartUp.Enabled);

			ini.SetValue("MySQL", "CustomMySqlSecondsInterval", MySqlSettings.CustomSecs.Interval);

			ini.SetValue("MySQL", "CustomMySqlSecondsCommandString", MySqlSettings.CustomSecs.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlMinutesCommandString", MySqlSettings.CustomMins.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlRolloverCommandString", MySqlSettings.CustomRollover.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlStartUpCommandString", MySqlSettings.CustomStartUp.Commands[0]);

			ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIdx", MySqlSettings.CustomMins.IntervalIndexes[0]);

			for (var i = 1; i < 10; i++)
			{
				if (string.IsNullOrEmpty(MySqlSettings.CustomSecs.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlSecondsCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlSecondsCommandString" + i, MySqlSettings.CustomSecs.Commands[i]);

				if (string.IsNullOrEmpty(MySqlSettings.CustomMins.Commands[i]))
				{
					ini.DeleteValue("MySQL", "CustomMySqlMinutesCommandString" + i);
					ini.DeleteValue("MySQL", "CustomMySqlMinutesIntervalIdx" + i);
				}
				else
				{
					ini.SetValue("MySQL", "CustomMySqlMinutesCommandString" + i, MySqlSettings.CustomMins.Commands[i]);
					ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIdx" + i, MySqlSettings.CustomMins.IntervalIndexes[i]);
				}

				if (string.IsNullOrEmpty(MySqlSettings.CustomRollover.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlRolloverCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlRolloverCommandString" + i, MySqlSettings.CustomRollover.Commands[i]);

				if (string.IsNullOrEmpty(MySqlSettings.CustomStartUp.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlStartUpCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlStartUpCommandString" + i, MySqlSettings.CustomStartUp.Commands[i]);
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

			// Select-a-Chart
			for (int i = 0; i < SelectaChartOptions.series.Length; i++)
			{
				ini.SetValue("Select-a-Chart", "Series" + i, SelectaChartOptions.series[i]);
				ini.SetValue("Select-a-Chart", "Colour" + i, SelectaChartOptions.colours[i]);
			}

			// Select-a-Period
			for (int i = 0; i < SelectaPeriodOptions.series.Length; i++)
			{
				ini.SetValue("Select-a-Period", "Series" + i, SelectaPeriodOptions.series[i]);
				ini.SetValue("Select-a-Period", "Colour" + i, SelectaPeriodOptions.colours[i]);
			}

			// Email settings
			ini.SetValue("SMTP", "Enabled", SmtpOptions.Enabled);
			ini.SetValue("SMTP", "ServerName", SmtpOptions.Server);
			ini.SetValue("SMTP", "Port", SmtpOptions.Port);
			ini.SetValue("SMTP", "SSLOption", SmtpOptions.SslOption);
			ini.SetValue("SMTP", "RequiresAuthentication", SmtpOptions.AuthenticationMethod);
			ini.SetValue("SMTP", "User", Crypto.EncryptString(SmtpOptions.User, Program.InstanceId, "SmtpOptions.User"));
			ini.SetValue("SMTP", "Password", Crypto.EncryptString(SmtpOptions.Password, Program.InstanceId, "SmtpOptions.Password"));
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
				Trans.AirQualityAvgCaptions[i] = ini.GetValue("AirQualityCaptions", "SensorAvg" + (i + 1), Trans.AirQualityAvgCaptions[i]);
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
			Trans.CO2_TemperatureCaption = ini.GetValue("CO2Captions", "CO2-Temperature", "Temperature");
			Trans.CO2_HumidityCaption = ini.GetValue("CO2Captions", "CO2-Humidity", "Humidity");

			// Snow
			Trans.SnowDepth = ini.GetValue("Snow", "SnowDepth", "Snow Depth");
			Trans.Snow24h = ini.GetValue("Snow", "Snow24h", "Snowfall 24h");

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
			FirmwareAlarm.EmailMsg = ini.GetValue("AlarmEmails", "firmware", "A station firmware update is now available.");
			ThirdPartyAlarm.EmailMsg = ini.GetValue("AlarmEmails", "httpStopped", "Third party HTTP uploads are failing.");
			MySqlUploadAlarm.EmailMsg = ini.GetValue("AlarmEmails", "mySqlStopped", "MySQL uploads are failing.");
			IsRainingAlarm.EmailMsg = ini.GetValue("AlarmEmails", "isRaining", "It has started to rain.");
			NewRecordAlarm.EmailMsg = ini.GetValue("AlarmEmails", "newRecord", "A new all-time record has been set.");
			FtpAlarm.EmailMsg = ini.GetValue("AlarmEmails", "ftpStopped", "FTP uploads have stopped.");

			// alarm names
			HighGustAlarm.Name = ini.GetValue("AlarmNames", "windGustAbove", "High Gust");
			HighPressAlarm.Name = ini.GetValue("AlarmNames", "pressureAbove", "High Pressure");
			HighTempAlarm.Name = ini.GetValue("AlarmNames", "tempAbove", "High Temperature");
			LowPressAlarm.Name = ini.GetValue("AlarmNames", "pressBelow", "Low Pressure");
			LowTempAlarm.Name = ini.GetValue("AlarmNames", "tempBelow", "Low Temperature");
			PressChangeAlarm.NameDown = ini.GetValue("AlarmNames", "pressDown", "Pressure Falling");
			PressChangeAlarm.NameUp = ini.GetValue("AlarmNames", "pressUp", "Pressure Rising");
			HighRainTodayAlarm.Name = ini.GetValue("AlarmNames", "rainAbove", "Rainfall Today");
			HighRainRateAlarm.Name = ini.GetValue("AlarmNames", "rainRateAbove", "High Rainfall Rate");
			SensorAlarm.Name = ini.GetValue("AlarmNames", "sensorLost", "Sensor Data Stopped");
			TempChangeAlarm.NameDown = ini.GetValue("AlarmNames", "tempDown", "Temp Falling");
			TempChangeAlarm.NameUp = ini.GetValue("AlarmNames", "tempUp", "Temp Rising");
			HighWindAlarm.Name = ini.GetValue("AlarmNames", "windAbove", "High Wind");
			DataStoppedAlarm.Name = ini.GetValue("AlarmNames", "dataStopped", "Data Stopped");
			BatteryLowAlarm.Name = ini.GetValue("AlarmNames", "batteryLow", "Battery Low");
			SpikeAlarm.Name = ini.GetValue("AlarmNames", "dataSpike", "Data Spike");
			UpgradeAlarm.Name = ini.GetValue("AlarmNames", "upgrade", "CMX Upgrade");
			FirmwareAlarm.Name = ini.GetValue("AlarmNames", "firmware", "Firmware Upgrade");
			ThirdPartyAlarm.Name = ini.GetValue("AlarmNames", "httpStopped", "HTTP Upload");
			MySqlUploadAlarm.Name = ini.GetValue("AlarmNames", "mySqlStopped", "MySQL Upload");
			IsRainingAlarm.Name = ini.GetValue("AlarmNames", "isRaining", "Is Raining");
			NewRecordAlarm.Name = ini.GetValue("AlarmNames", "newRecord", "New Record");
			FtpAlarm.Name = ini.GetValue("AlarmNames", "ftpStopped", "Web Upload");

			// web tag defaults
			Trans.WebTagGenTimeDate = ini.GetValue("WebTags", "GeneralTimeDate", "HH:mm 'on' dd MMMM yyyy");
			Trans.WebTagRecDate = ini.GetValue("WebTags", "RecordDate", "'on' dd MMMM yyyy");
			Trans.WebTagRecTimeDate = ini.GetValue("WebTags", "RecordTimeDate", "'at' HH:mm 'on' dd MMMM yyyy");
			Trans.WebTagRecDryWetDate = ini.GetValue("WebTags", "RecordDryWetDate", "'to' dd MMMM yyyy");

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
				ini.SetValue("AirQualityCaptions", "SensorAvg" + (i + 1), Trans.AirQualityAvgCaptions[i]);
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
			ini.SetValue("CO2Captions", "CO2-Temperature", Trans.CO2_TemperatureCaption);
			ini.SetValue("CO2Captions", "CO2-Humidity", Trans.CO2_HumidityCaption);

			// Snow
			ini.SetValue("Snow", "SnowDepth", Trans.SnowDepth);
			ini.SetValue("Snow", "Snow24h", Trans.Snow24h);

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
			ini.SetValue("AlarmEmails", "firmware", FirmwareAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "httpStopped", ThirdPartyAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "mySqlStopped", MySqlUploadAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "isRaining", IsRainingAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "newRecord", NewRecordAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "ftpStopped", FtpAlarm.EmailMsg);

			// alarm names
			ini.SetValue("AlarmNames", "windGustAbove", HighGustAlarm.Name);
			ini.SetValue("AlarmNames", "pressureAbove", HighPressAlarm.Name);
			ini.SetValue("AlarmNames", "tempAbove", HighTempAlarm.Name);
			ini.SetValue("AlarmNames", "pressBelow", LowPressAlarm.Name);
			ini.SetValue("AlarmNames", "tempBelow", LowTempAlarm.Name);
			ini.SetValue("AlarmNames", "pressDown", PressChangeAlarm.NameDown);
			ini.SetValue("AlarmNames", "pressUp", PressChangeAlarm.NameUp);
			ini.SetValue("AlarmNames", "rainAbove", HighRainTodayAlarm.Name);
			ini.SetValue("AlarmNames", "rainRateAbove", HighRainRateAlarm.Name);
			ini.SetValue("AlarmNames", "sensorLost", SensorAlarm.Name);
			ini.SetValue("AlarmNames", "tempDown", TempChangeAlarm.NameDown);
			ini.SetValue("AlarmNames", "tempUp", TempChangeAlarm.NameUp);
			ini.SetValue("AlarmNames", "windAbove", HighWindAlarm.Name);
			ini.SetValue("AlarmNames", "dataStopped", DataStoppedAlarm.Name);
			ini.SetValue("AlarmNames", "batteryLow", BatteryLowAlarm.Name);
			ini.SetValue("AlarmNames", "dataSpike", SpikeAlarm.Name);
			ini.SetValue("AlarmNames", "upgrade", UpgradeAlarm.Name);
			ini.SetValue("AlarmNames", "firmware", FirmwareAlarm.Name);
			ini.SetValue("AlarmNames", "httpStopped", ThirdPartyAlarm.Name);
			ini.SetValue("AlarmNames", "mySqlStopped", MySqlUploadAlarm.Name);
			ini.SetValue("AlarmNames", "isRaining", IsRainingAlarm.Name);
			ini.SetValue("AlarmNames", "newRecord", NewRecordAlarm.Name);
			ini.SetValue("AlarmNames", "ftpStopped", FtpAlarm.Name);

			// web tag defaults
			ini.SetValue("WebTags", "GeneralTimeDate", Trans.WebTagGenTimeDate);
			ini.SetValue("WebTags", "RecordDate", Trans.WebTagRecDate);
			ini.SetValue("WebTags", "RecordTimeDate", Trans.WebTagRecTimeDate);
			ini.SetValue("WebTags", "RecordDryWetDate", Trans.WebTagRecDryWetDate);

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

		public CExtraFiles[] ExtraFiles { get; set; } = new CExtraFiles[numextrafiles];
		public List<CExtraFiles> ActiveExtraFiles { get; set; } = [];

		public HttpFileProps[] HttpFilesConfig { get; set; } = new HttpFileProps[10];

		public bool DeleteBeforeUpload { get; set; }

		public bool FTPRename { get; set; }

		public int UpdateInterval { get; set; }

		public Timer RealtimeTimer { get; set; } = new();

		internal Timer CustomMysqlSecondsTimer;

		public bool WebIntervalEnabled { get; set; }

		public bool WebAutoUpdate { get; set; }

		public int WMR200TempChannel { get; set; }

		public int WMR928TempChannel { get; set; }

		public int RTdisconnectcount { get; set; }

		public string AirLinkInIPAddr { get; set; }

		public string AirLinkOutIPAddr { get; set; }

		public bool AirLinkInEnabled { get; set; }

		public bool AirLinkOutEnabled { get; set; }

		public bool ExtraSensorUseSolar { get; set; }
		public bool ExtraSensorUseUv { get; set; }
		public bool ExtraSensorUseTempHum { get; set; }
		public bool ExtraSensorUseSoilTemp { get; set; }
		public bool ExtraSensorUseSoilMoist { get; set; }
		public bool ExtraSensorUseLeafWet { get; set; }
		public bool ExtraSensorUseAQI { get; set; }
		public bool ExtraSensorUseUserTemp { get; set; }
		public bool ExtraSensorUseLightning { get; set; }
		public bool ExtraSensorUseCo2 { get; set; }
		public bool ExtraSensorUseLeak { get; set; }
		public bool ExtraSensorUseCamera { get; set; }
		public bool ExtraSensorUseLaserDist { get; set; }


		public bool EcowittExtraEnabled { get; set; }
		public bool EcowittCloudExtraEnabled { get; set; }
		public string EcowittApplicationKey { get; set; }
		public string EcowittUserApiKey { get; set; }
		public string EcowittMacAddress { get; set; }
		public string EcowittCameraMacAddress { get; set; }
		public bool EcowittSetCustomServer { get; set; }
		public string EcowittGatewayAddr { get; set; }
		public string EcowittLocalAddr { get; set; }
		public int EcowittCustomInterval { get; set; }
		public bool EcowittExtraSetCustomServer { get; set; }
		public string EcowittExtraGatewayAddr { get; set; }
		public string EcowittExtraLocalAddr { get; set; }
		public int EcowittExtraCustomInterval { get; set; }
		public string[] EcowittForwarders { get; set; } = new string[10];
		public bool EcowittExtraUseMainForwarders { get; set; }
		public string[] EcowittExtraForwarders { get; set; } = new string[10];
		public string EcowittHttpPassword { get; set; }
		public int[] EcowittMapWN34 { get; set; } = new int[9];
		public bool EcowittIsRainingUsePiezo {  get; set; }

		public bool AmbientExtraEnabled { get; set; }
		public bool AmbientExtraUseSolar { get; set; }
		public bool AmbientExtraUseUv { get; set; }
		public bool AmbientExtraUseTempHum { get; set; }
		public bool AmbientExtraUseSoilTemp { get; set; }
		public bool AmbientExtraUseSoilMoist { get; set; }
		public bool AmbientExtraUseAQI { get; set; }
		public bool AmbientExtraUseCo2 { get; set; }
		public bool AmbientExtraUseLightning { get; set; }
		public bool AmbientExtraUseLeak { get; set; }

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
		public double ChillHourBase {  get; set; }

		public int ChillHourSeasonStart { get; set; }

		public int RainSeasonStart { get; set; }
		public int RainWeekStart { get; set; }


		public double FCPressureThreshold { get; set; }

		public double FChighpress { get; set; }

		public double FClowpress { get; set; }

		public bool FCpressinMB { get; set; }

		public double RainDayThreshold { get; set; }

		public int SnowDepthHour { get; set; }
		public int SnowAutomated { get; set; }


		public bool HourlyForecast { get; set; }

		public bool UseCumulusForecast { get; set; }

		public bool DavisConsoleHighGust { get; set; }

		public bool DavisCalcAltPress { get; set; }

		public bool DavisUseDLLBarCalData { get; set; }

		public int LCMaxWind { get; set; }


		public DateTime RecordsBeganDateTime { get; set; }

		public int YTDrainyear { get; set; }

		public double YTDrain { get; set; }

		public string LocationDesc { get; set; }

		public string LocationName { get; set; }

		public string HTTPProxyPassword { get; set; }

		public string HTTPProxyUser { get; set; }

		public int HTTPProxyPort { get; set; }

		public string HTTPProxyName { get; set; }

		public int[] WindDPlaceDefaults { get; } = [1, 0, 0, 0]; // m/s, mph, km/h, knots
		public int[] TempDPlaceDefaults { get; } = [1, 1];
		public int[] PressDPlaceDefaults { get; } = [1, 1, 2, 2];
		public int[] RainDPlaceDefaults { get; } = [1, 2];
		public const int numextrafiles = 99;
		public const int numOfSelectaChartSeries = 6;


		public bool ErrorLogSpikeRemoval { get; set; }

		public bool ReportLostSensorContact { get; set; }

		public bool ReportDataStoppedErrors { get; set; }

		//public bool RestartIfDataStops { get set }

		//public bool RestartIfUnplugged { get set }

		//public bool CloseOnSuspend { get set }

		//public bool ConfirmClose { get set }

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
		internal DateTime Dawn;
		internal DateTime Dusk;
		public TimeSpan DaylightLength { get; set; }
		public int GraphHours { get; set; }

		// WeatherLink Live transmitter Ids and indexes
		internal string WllApiKey;
		internal string WllApiSecret;
		internal int WllStationId;
		internal string WllStationUuid;
		internal int WllParentId;
		internal bool WllTriggerDataStoppedOnBroadcast; // trigger a data stopped state if broadcasts stop being received but current data is OK
		/// <value>Read-only setting, default 20 minutes (1200 sec)</value>
		internal int WllBroadcastDuration = 1200;
		/// <value>Read-only setting, default 22222</value>
		internal int WllBroadcastPort = 22222;
		internal bool WLLAutoUpdateIpAddress = true;
		internal int WllPrimaryWind = 1;
		internal int WllPrimaryTempHum = 1;
		internal int WllPrimaryRain = 1;
		internal int WllPrimarySolar;
		internal int WllPrimaryUV;

		internal int WllExtraSoilTempTx1;
		internal int WllExtraSoilTempIdx1 = 1;
		internal int WllExtraSoilTempTx2;
		internal int WllExtraSoilTempIdx2 = 2;
		internal int WllExtraSoilTempTx3;
		internal int WllExtraSoilTempIdx3 = 3;
		internal int WllExtraSoilTempTx4;
		internal int WllExtraSoilTempIdx4 = 4;

		internal int WllExtraSoilMoistureTx1;
		internal int WllExtraSoilMoistureIdx1 = 1;
		internal int WllExtraSoilMoistureTx2;
		internal int WllExtraSoilMoistureIdx2 = 2;
		internal int WllExtraSoilMoistureTx3;
		internal int WllExtraSoilMoistureIdx3 = 3;
		internal int WllExtraSoilMoistureTx4;
		internal int WllExtraSoilMoistureIdx4 = 4;

		internal int WllExtraLeafTx1;
		internal int WllExtraLeafIdx1 = 1;
		internal int WllExtraLeafTx2;
		internal int WllExtraLeafIdx2 = 2;

		internal int[] WllExtraTempTx = [0, 0, 0, 0, 0, 0, 0, 0, 0];

		internal bool[] WllExtraHumTx = [false, false, false, false, false, false, false, false, false];

		// WeatherLink Live transmitter Ids and indexes
		internal bool AirLinkIsNode;
		internal string AirLinkApiKey;
		internal string AirLinkApiSecret;
		internal int AirLinkInStationId;
		internal int AirLinkOutStationId;
		internal bool AirLinkAutoUpdateIpAddress = true;

		internal int airQualityIndex = -1;

		internal string Gw1000IpAddress;
		internal string Gw1000MacAddress;
		internal bool Gw1000AutoUpdateIpAddress = true;
		internal int Gw1000PrimaryTHSensor;
		internal int Gw1000PrimaryRainSensor;

		internal Timer WebTimer = new();

		public const int DAVIS = 0;
		public const int OREGON = 1;
		public const int EW = 2;
		public const int LACROSSE = 3;
		public const int OREGONUSB = 4;
		public const int INSTROMET = 5;
		public const int ECOWITT = 6;
		public const int HTTPSTATION = 7;
		public const int AMBIENT = 8;
		public const int WEATHERFLOW = 9;
		public const int SIMULATOR = 10;
		public const int JSONSTATION = 11;

		internal string ReportPath;
		public static string LatestError { get; set; }
		public static DateTime LatestErrorTS { get; set; } = DateTime.MinValue;
		internal DateTime defaultRecordTS = DateTime.MinValue;
		internal const string WxnowFile = "wxnow.txt";
		private readonly string RealtimeFile = "realtime.txt";
		private FtpClient RealtimeFTP;
		private SftpClient RealtimeSSH;
		private volatile bool RealtimeFtpInProgress;
		private volatile bool RealtimeCopyInProgress;
		private volatile bool RealtimeFtpReconnecting;
		private byte RealtimeCycleCounter;

		internal FileGenerationOptions[] StdWebFiles;
		internal FileGenerationOptions[] RealtimeFiles;
		internal FileGenerationOptions[] GraphDataFiles;
		internal FileGenerationOptions[] GraphDataEodFiles;


		public string GetLogFileName(DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = thedate.AddHours(GetHourInc(thedate));

			var datestring = logfiledate.ToString("yyyyMM");

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

		public async Task DoLogFile(DateTime timestamp, bool live)
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
			var inv = CultureInfo.InvariantCulture;
			var sep = ",";

			var sb = new StringBuilder(256);
			sb.Append(timestamp.ToString("dd/MM/yy", inv) + sep);
			sb.Append(timestamp.ToString("HH:mm", inv) + sep);
			sb.Append(station.OutdoorTemperature.ToString(TempFormat, inv) + sep);
			sb.Append(station.OutdoorHumidity + sep);
			sb.Append(station.OutdoorDewpoint.ToString(TempFormat, inv) + sep);
			sb.Append(station.WindAverage.ToString(WindAvgFormat, inv) + sep);
			sb.Append(station.RecentMaxGust.ToString(WindFormat, inv) + sep);
			sb.Append(station.AvgBearing + sep);
			sb.Append(station.RainRate.ToString(RainFormat, inv) + sep);
			sb.Append(station.RainToday.ToString(RainFormat, inv) + sep);
			sb.Append(station.Pressure.ToString(PressFormat, inv) + sep);
			sb.Append(station.RainCounter.ToString(RainFormat, inv) + sep);
			sb.Append((station.IndoorTemperature.HasValue ? station.IndoorTemperature.Value.ToString(TempFormat, inv) : string.Empty) + sep);
			sb.Append((station.IndoorHumidity.HasValue ? station.IndoorHumidity : string.Empty) + sep);
			sb.Append(station.WindLatest.ToString(WindFormat, inv) + sep);
			sb.Append(station.WindChill.ToString(TempFormat, inv) + sep);
			sb.Append(station.HeatIndex.ToString(TempFormat, inv) + sep);
			sb.Append((station.UV.HasValue ? station.UV.Value.ToString(UVFormat, inv) : string.Empty) + sep);
			sb.Append((station.SolarRad.HasValue ? station.SolarRad : string.Empty) + sep);
			sb.Append(station.ET.ToString(ETFormat, inv) + sep);
			sb.Append(station.AnnualETTotal.ToString(ETFormat, inv) + sep);
			sb.Append(station.ApparentTemperature.ToString(TempFormat, inv) + sep);
			sb.Append(station.CurrentSolarMax + sep);
			sb.Append(station.SunshineHours.ToString(SunFormat, inv) + sep);
			sb.Append(station.Bearing + sep);
			sb.Append(station.RG11RainToday.ToString(RainFormat, inv) + sep);
			sb.Append(station.RainSinceMidnight.ToString(RainFormat, inv) + sep);
			sb.Append(station.FeelsLike.ToString(TempFormat, inv) + sep);
			sb.AppendLine(station.Humidex.ToString(TempFormat, inv));

			var success = false;
			var retries = LogFileRetries;
			var charArr = sb.ToString().ToCharArray();

			do
			{
				try
				{
					using FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read, charArr.Length, FileOptions.WriteThrough);
					using StreamWriter file = new StreamWriter(fs);
					await file.WriteAsync(charArr, 0, charArr.Length);
					file.Close();
					fs.Close();

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
				MySqlILastntervalTime = timestamp;

				StringBuilder values = new StringBuilder(MonthlyTable.StartOfInsert, 600);
				values.Append(" Values('");
				values.Append(timestamp.ToString("yyyy-MM-dd HH:mm") + "',");
				values.Append(station.OutdoorTemperature.ToString(TempFormat, inv) + ",");
				values.Append(station.OutdoorHumidity + ",");
				values.Append(station.OutdoorDewpoint.ToString(TempFormat, inv) + ",");
				values.Append(station.WindAverage.ToString(WindAvgFormat, inv) + ",");
				values.Append(station.RecentMaxGust.ToString(WindFormat, inv) + ",");
				values.Append(station.AvgBearing + ",");
				values.Append(station.RainRate.ToString(RainFormat, inv) + ",");
				values.Append(station.RainToday.ToString(RainFormat, inv) + ",");
				values.Append(station.Pressure.ToString(PressFormat, inv) + ",");
				values.Append(station.RainCounter.ToString(RainFormat, inv) + ",");
				values.Append((station.IndoorTemperature.HasValue ? station.IndoorTemperature.Value.ToString(TempFormat, inv) : "NULL") + ",");
				values.Append((station.IndoorHumidity.HasValue ? station.IndoorHumidity : "NULL") + ",");
				values.Append(station.WindLatest.ToString(WindFormat, inv) + ",");
				values.Append(station.WindChill.ToString(TempFormat, inv) + ",");
				values.Append(station.HeatIndex.ToString(TempFormat, inv) + ",");
				values.Append((station.UV.HasValue ? station.UV.Value.ToString(UVFormat, inv) : "NULL") + ",");
				values.Append((station.SolarRad.HasValue ? station.SolarRad : "NULL" )+ ",");
				values.Append(station.ET.ToString(ETFormat, inv) + ",");
				values.Append(station.AnnualETTotal.ToString(ETFormat, inv) + ",");
				values.Append(station.ApparentTemperature.ToString(TempFormat, inv) + ",");
				values.Append(station.CurrentSolarMax + ",");
				values.Append(station.SunshineHours.ToString(SunFormat, inv) + ",");
				values.Append(station.Bearing + ",");
				values.Append(station.RG11RainToday.ToString(RainFormat, inv) + ",");
				values.Append(station.RainSinceMidnight.ToString(RainFormat, inv) + ",'");
				values.Append(station.CompassPoint(station.AvgBearing) + "','");
				values.Append(station.CompassPoint(station.Bearing) + "',");
				values.Append(station.FeelsLike.ToString(TempFormat, inv) + ",");
				values.Append(station.Humidex.ToString(TempFormat, inv));
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

		public async Task DoExtraLogFile(DateTime timestamp)
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
			var inv = CultureInfo.InvariantCulture;
			var sep = ",";

			LogDebugMessage($"DoExtraLogFile: Writing log entry for {timestamp}");

			var sb = new StringBuilder(512);
			sb.Append(timestamp.ToString("dd/MM/yy", inv) + sep);                     //0
			sb.Append(timestamp.ToString("HH:mm", inv) + sep);                        //1

			for (int i = 1; i <= 10; i++)
			{
				sb.Append((station.ExtraTemp[i].HasValue ? station.ExtraTemp[i].Value.ToString(TempFormat, inv) : string.Empty) + sep);       //2-11
			}
			for (int i = 1; i <= 10; i++)
			{
				sb.Append((station.ExtraHum[i].HasValue ? station.ExtraHum[i].Value.ToString(HumFormat, inv) : string.Empty) + sep);        //12-21
			}
			for (int i = 1; i <= 10; i++)
			{
				sb.Append((station.ExtraDewPoint[i].HasValue ? station.ExtraDewPoint[i].Value.ToString(TempFormat, inv) : string.Empty) + sep);  //22-31
			}
			for (int i = 1; i <= 4; i++)
			{
				sb.Append((station.SoilTemp[i].HasValue ? station.SoilTemp[i].Value.ToString(TempFormat, inv) : string.Empty) + sep);     //32-35
			}

			sb.Append((station.SoilMoisture1.HasValue ? station.SoilMoisture1 : string.Empty) + sep);                      //36
			sb.Append((station.SoilMoisture2.HasValue ? station.SoilMoisture2 : string.Empty) + sep);                      //37
			sb.Append((station.SoilMoisture3.HasValue ? station.SoilMoisture3 : string.Empty) + sep);                      //38
			sb.Append((station.SoilMoisture4.HasValue ? station.SoilMoisture4 : string.Empty) + sep);                      //39

			sb.Append(sep);     //40 - was leaf temp 1
			sb.Append(sep);     //41 - was leaf temp 2

			sb.Append((station.LeafWetness1.HasValue ? station.LeafWetness1.Value.ToString(LeafWetFormat, inv) : string.Empty) + sep);    //42
			sb.Append((station.LeafWetness2.HasValue ? station.LeafWetness2.Value.ToString(LeafWetFormat, inv) : string.Empty) + sep);    //43

			for (int i = 5; i <= 16; i++)
			{
				sb.Append((station.SoilTemp[i].HasValue ? station.SoilTemp[i].Value.ToString(TempFormat, inv) : string.Empty) + sep);     //44-55
			}

			sb.Append(station.SoilMoisture5 + sep);      //56
			sb.Append(station.SoilMoisture6 + sep);      //57
			sb.Append(station.SoilMoisture7 + sep);      //58
			sb.Append(station.SoilMoisture8 + sep);      //59
			sb.Append(station.SoilMoisture9 + sep);      //60
			sb.Append(station.SoilMoisture10 + sep);     //61
			sb.Append(station.SoilMoisture11 + sep);     //62
			sb.Append(station.SoilMoisture12 + sep);     //63
			sb.Append(station.SoilMoisture13 + sep);     //64
			sb.Append(station.SoilMoisture14 + sep);     //65
			sb.Append(station.SoilMoisture15 + sep);     //66
			sb.Append(station.SoilMoisture16 + sep);     //67

			sb.Append((station.AirQuality1.HasValue ? station.AirQuality1.Value.ToString("F1", inv) : string.Empty) + sep);     //68
			sb.Append((station.AirQuality2.HasValue ? station.AirQuality2.Value.ToString("F1", inv) : string.Empty) + sep);     //69
			sb.Append((station.AirQuality3.HasValue ? station.AirQuality3.Value.ToString("F1", inv) : string.Empty) + sep);     //70
			sb.Append((station.AirQuality4.HasValue ? station.AirQuality4.Value.ToString("F1", inv) : string.Empty) + sep);     //71
			sb.Append((station.AirQualityAvg1.HasValue ? station.AirQualityAvg1.Value.ToString("F1", inv) : string.Empty) + sep);  //72
			sb.Append((station.AirQualityAvg2.HasValue ? station.AirQualityAvg2.Value.ToString("F1", inv) : string.Empty) + sep);  //73
			sb.Append((station.AirQualityAvg3.HasValue ? station.AirQualityAvg3.Value.ToString("F1", inv) : string.Empty) + sep);  //74
			sb.Append((station.AirQualityAvg4.HasValue ? station.AirQualityAvg4.Value.ToString("F1", inv) : string.Empty) + sep);  //75

			for (int i = 1; i < 9; i++)
			{
				sb.Append((station.UserTemp[i].HasValue ? station.UserTemp[i].Value.ToString(TempFormat, inv) : string.Empty) + sep);   //76-83
			}

			sb.Append((station.CO2.HasValue ? station.CO2 : string.Empty) + sep);                                                           //84
			sb.Append((station.CO2_24h.HasValue ? station.CO2_24h : string.Empty) + sep);                                                   //85
			sb.Append((station.CO2_pm2p5.HasValue ? station.CO2_pm2p5.Value.ToString("F1", inv) : string.Empty) + sep);                     //86
			sb.Append((station.CO2_pm2p5_24h.HasValue ? station.CO2_pm2p5_24h.Value.ToString("F1", inv) : string.Empty) + sep);             //87
			sb.Append((station.CO2_pm10.HasValue ? station.CO2_pm10.Value.ToString("F1", inv) : string.Empty) + sep);                       //88
			sb.Append((station.CO2_pm10_24h.HasValue ? station.CO2_pm10_24h.Value.ToString("F1", inv) : string.Empty) + sep);               //89
			sb.Append((station.CO2_temperature.HasValue ? station.CO2_temperature.Value.ToString(TempFormat, inv) : string.Empty) + sep);   //90
			sb.Append((station.CO2_humidity.HasValue ? station.CO2_humidity.Value.ToString("F0") : string.Empty));                          //91
			sb.Append(Environment.NewLine);

			var success = false;
			var retries = LogFileRetries;
			var charArr = sb.ToString().ToCharArray();

			do
			{
				try
				{
					using FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read, charArr.Length, FileOptions.WriteThrough);
					using StreamWriter file = new StreamWriter(fs);
					await file.WriteAsync(charArr, 0, charArr.Length);
					file.Close();
					fs.Close();

					success = true;

					LogDebugMessage($"DoExtraLogFile: Log entry for {timestamp} written");
				}
				catch (Exception ex)
				{
					LogErrorMessage($"DoExtraLogFile: Error writing log entry {timestamp} - {ex.Message}");
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);
		}

		public const int NumAirLinkLogFileFields = 56;

		public async Task DoAirLinkLogFile(DateTime timestamp)
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
			var inv = CultureInfo.InvariantCulture;
			var sep = ',';

			var sb = new StringBuilder(256);

			sb.Append(timestamp.ToString("dd/MM/yy", inv) + sep);
			sb.Append(timestamp.ToString("HH:mm", inv) + sep);

			if (AirLinkInEnabled && airLinkDataIn != null && airLinkDataIn.dataValid)
			{
				sb.Append(airLinkDataIn.temperature.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.humidity.ToString() + sep);
				sb.Append(airLinkDataIn.pm1.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm2p5.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm2p5_1hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm2p5_3hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm2p5_24hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm2p5_nowcast.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm10.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm10_1hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm10_3hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm10_24hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pm10_nowcast.ToString("F1", inv) + sep);
				sb.Append(airLinkDataIn.pct_1hr.ToString() + sep);
				sb.Append(airLinkDataIn.pct_3hr.ToString() + sep);
				sb.Append(airLinkDataIn.pct_24hr.ToString() + sep);
				sb.Append(airLinkDataIn.pct_nowcast.ToString() + sep);
				if (AirQualityDPlaces > 0)
				{
					sb.Append(airLinkDataIn.aqiPm2p5.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm2p5_1hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm2p5_3hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm2p5_24hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm2p5_nowcast.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm10.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm10_1hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm10_3hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm10_24hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataIn.aqiPm10_nowcast.ToString(AirQualityFormat, inv) + sep);
				}
				else // Zero decimals - truncate value rather than round
				{
					sb.Append((int) airLinkDataIn.aqiPm2p5 + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm2p5_1hr + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm2p5_3hr + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm2p5_24hr + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm2p5_nowcast + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm10 + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm10_1hr + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm10_3hr + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm10_24hr + sep.ToString());
					sb.Append((int) airLinkDataIn.aqiPm10_nowcast + sep.ToString());
				}
			}
			else
			{
				// write zero values
				sb.Append(new String(sep, 27));
			}

			if (AirLinkOutEnabled && airLinkDataOut != null && airLinkDataOut.dataValid)
			{
				sb.Append(airLinkDataOut.temperature.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.humidity.ToString() + sep);
				sb.Append(airLinkDataOut.pm1.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm2p5.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm2p5_1hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm2p5_3hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm2p5_24hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm2p5_nowcast.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm10.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm10_1hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm10_3hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm10_24hr.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pm10_nowcast.ToString("F1", inv) + sep);
				sb.Append(airLinkDataOut.pct_1hr.ToString() + sep);
				sb.Append(airLinkDataOut.pct_3hr.ToString() + sep);
				sb.Append(airLinkDataOut.pct_24hr.ToString() + sep);
				sb.Append(airLinkDataOut.pct_nowcast.ToString() + sep);
				if (AirQualityDPlaces > 0)
				{
					sb.Append(airLinkDataOut.aqiPm2p5.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm2p5_1hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm2p5_3hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm2p5_24hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm2p5_nowcast.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm10.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm10_1hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm10_3hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm10_24hr.ToString(AirQualityFormat, inv) + sep);
					sb.Append(airLinkDataOut.aqiPm10_nowcast.ToString(AirQualityFormat, inv));
				}
				else // Zero decimals - truncate value rather than round
				{
					sb.Append((int) airLinkDataOut.aqiPm2p5 + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm2p5_1hr + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm2p5_3hr + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm2p5_24hr + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm2p5_nowcast + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm10 + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm10_1hr + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm10_3hr + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm10_24hr + sep.ToString());
					sb.Append((int) airLinkDataOut.aqiPm10_nowcast);
				}
			}
			else
			{
				// write null values
				sb.Append(new String(sep, 26));
			}
			sb.Append(Environment.NewLine);

			var success = false;
			var retries = LogFileRetries;
			var charArr = sb.ToString().ToCharArray();

			do
			{
				try
				{
					using FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read, charArr.Length, FileOptions.WriteThrough);
					using StreamWriter file = new StreamWriter(fs);
					await file.WriteAsync(charArr, 0, charArr.Length);
					file.Close();
					fs.Close();

					success = true;

					LogDebugMessage($"DoAirLinkLogFile: Log entry for {timestamp} written");
				}
				catch (Exception ex)
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
					_ = DoCustomIntervalLog(i, timestamp);
				}
			}
		}

		private async Task DoCustomIntervalLog(int idx, DateTime timestamp)
		{
			// Writes a custom log file

			// create the filename
			var filename = GetCustomIntvlLogFileName(idx, timestamp);

			LogDebugMessage($"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Writing log entry for {timestamp}");

			// create the line to be appended
			var sb = new StringBuilder(256);
			var inv = CultureInfo.InvariantCulture;
			var sep = ",";

			sb.Append(timestamp.ToString("dd/MM/yy", inv) + sep);
			sb.Append(timestamp.ToString("HH:mm", inv) + sep);

			var tokenParser = new TokenParser(TokenParserOnToken)
			{
				// process the webtags in the content string
				InputText = CustomIntvlLogSettings[idx].ContentString
			};
			sb.Append(tokenParser.ToStringFromString());

			LogDataMessage("DoCustomIntervalLog: entry: " + sb);

			sb.Append(Environment.NewLine);

			var success = false;
			var retries = LogFileRetries;
			var charArr = sb.ToString().ToCharArray();

			do
			{
				try
				{
					using FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read, charArr.Length, FileOptions.WriteThrough);
					using StreamWriter file = new StreamWriter(fs);
					await file.WriteAsync(charArr, 0, charArr.Length);
					file.Close();
					fs.Close();

					success = true;

					LogDebugMessage($"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Log entry for {timestamp} written");
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
					_ = DoCustomDailyLog(i, timestamp);
				}
			}
		}

		private async Task DoCustomDailyLog(int idx, DateTime timestamp)
		{
			LogDebugMessage($"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Writing log entry");

			// create the filename
			var filename = GetCustomDailyLogFileName(idx);

			string datestring = timestamp.AddDays(-1).ToString("dd/MM/yy", CultureInfo.InvariantCulture);
			// NB this string is just for logging, the dayfile update code is further down
			var sb = new StringBuilder(300);
			sb.Append(datestring + ",");

			var tokenParser = new TokenParser(TokenParserOnToken)
			{
				// process the webtags in the content string
				InputText = CustomDailyLogSettings[idx].ContentString
			};
			sb.Append(tokenParser.ToStringFromString());

			LogDataMessage("DoCustomDailyLog: entry: " + sb);

			sb.Append(Environment.NewLine);

			var success = false;
			var retries = LogFileRetries;
			var charArr = sb.ToString().ToCharArray();

			do
			{
				try
				{
					using FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read, charArr.Length, FileOptions.WriteThrough);
					using StreamWriter file = new StreamWriter(fs);
					await file.WriteAsync(charArr, 0, charArr.Length);
					file.Close();
					fs.Close();

					success = true;

					LogDebugMessage($"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Log entry written");
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
				// Cannot lock the file on MacOS, we have to rely on FileShare
				if (!IsOSX)
				{
#pragma warning disable CA1416 // Validate platform compatibility
					_lockFile.Lock(0, 0); // 0,0 has special meaning to lock entire file regardless of length
#pragma warning restore CA1416 // Validate platform compatibility
				}

				// give everyone access to the file
				if (Windows)
				{
#pragma warning disable CA1416 // Validate platform compatibility
					try
					{
						FileInfo fileInfo = new FileInfo(_lockFilename);
						FileSecurity accessControl = fileInfo.GetAccessControl();
						accessControl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
						fileInfo.SetAccessControl(accessControl);
					}
					catch (FileNotFoundException)
					{
						LogErrorMessage("Error setting lock file permissions - file not found");
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "Error setting lock file permissions");
					}
#pragma warning restore CA1416 // Validate platform compatibility
				}
				else
				{
					try
					{
						_ = Utils.RunExternalTask("/bin/bash", "-c \"chmod 755 " + _lockFilename, false, true);
					}
					catch (FileNotFoundException)
					{
						LogErrorMessage("Error setting lock file permissions - bash command not found");
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "Error setting lock file permissions");
					}
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
				}
				else
				{
					LogConsoleMessage("Cumulus is already running - but 'Stop second instance' is disabled", ConsoleColor.Yellow);
					LogWarningMessage("Stop second instance: Cumulus is already running but 'Stop second instance' is disabled - continuing");
				}
			}
			catch (Exception ex)
			{
				LogWarningMessage("Stop second instance: File Error! - " + ex);
				LogMessage("Stop second instance: File HResult - " + ex.HResult);
				LogMessage("Stop second instance: File HResult - " + (ex.HResult & 0x0000FFFF));

				if (ProgramOptions.WarnMultiple)
				{
					LogMessage("Stop second instance: Terminating this instance of Cumulus");
					LogConsoleMessage("An error occurred during second instance detection and 'Stop second instance' is enabled - terminating", ConsoleColor.Red);
					LogConsoleMessage("Program exit");
					Environment.Exit(3);
				}
				else
				{
					LogMessage("Stop second instance: 'Stop second instance' is disabled - continuing this instance of Cumulus");
				}
			}
		}

		public void BackupData(bool daily, DateTime timestamp)
		{
			LogMessage($"Creating {(daily ? "daily" : "start-up")} backup...");

			string dirpath = daily ? backupPath + "daily" + DirectorySeparator : backupPath;

			if (!Directory.Exists(dirpath))
			{
				LogMessage("BackupData: *** Error - backup folder does not exist - " + dirpath);
				CreateRequiredFolders();
			}


			if (Directory.Exists(dirpath))
			{
				string[] files = Directory.GetFiles(dirpath);
				Array.Sort(files);
				var filecnt = files.Length;

				foreach (var zip in files)
				{
					// leave the last 10 in place
					if (filecnt <= 10)
						break;

					try
					{
						File.Delete(zip);
						filecnt--;
					}
					catch (UnauthorizedAccessException)
					{
						LogErrorMessage("BackupData: Error, no permission to read/delete file: " + zip);
						LogConsoleMessage("Error, no permission to read/delete file: " + zip, ConsoleColor.Yellow);
						break;
					}
					catch (Exception ex)
					{
						LogErrorMessage($"BackupData: Error while attempting to read/delete file: {zip}, error message: {ex.Message}");
						LogConsoleMessage($"Error while attempting to read/delete file: {zip}, error message: {ex.Message}", ConsoleColor.Yellow);
						break;
					}
				}

				string filename = (daily ? timestamp.ToString("yyyyMMdd") : timestamp.ToString("yyyyMMddHHmmss")) + ".zip";
				string datafolder = "data" + DirectorySeparator;

				LogMessage("BackupData: Creating backup " + filename);

				var configbackup = "Cumulus.ini";
				var uniquebackup = "UniqueId.txt";
				var stringsbackup = "strings.ini";
				var alltimebackup = datafolder + "alltime.ini";
				var monthlyAlltimebackup = datafolder + "monthlyalltime.ini";
				var daybackup = datafolder + "dayfile.txt";
				var yesterdaybackup = datafolder + "yesterday.ini";
				var todaybackup = datafolder + "today.ini";
				var monthbackup = datafolder + "month.ini";
				var yearbackup = datafolder + "year.ini";
				var diarybackup = datafolder + "diary.db";
				var dbBackup = datafolder + "cumulusmx.db";

				var LogFile = GetLogFileName(timestamp);
				var logbackup = datafolder + LogFile.Replace(logFilePath, "");

				var extraFile = GetExtraLogFileName(timestamp);
				var extraBackup = datafolder + extraFile.Replace(logFilePath, "");

				var AirLinkFile = GetAirLinkLogFileName(timestamp);
				var AirLinkBackup = datafolder + AirLinkFile.Replace(logFilePath, "");


				if (!File.Exists(dirpath + DirectorySeparator + filename))
				{
					// create a zip archive file for the backup
					try
					{
						using (FileStream zipFile = new FileStream(dirpath + DirectorySeparator + filename, FileMode.Create))
						{
							using ZipArchive archive = new ZipArchive(zipFile, ZipArchiveMode.Create);
							try
							{
								if (File.Exists(AlltimeIniFile))
									archive.CreateEntryFromFile(AlltimeIniFile, alltimebackup);
								if (File.Exists(MonthlyAlltimeIniFile))
									archive.CreateEntryFromFile(MonthlyAlltimeIniFile, monthlyAlltimebackup);
								if (File.Exists(DayFileName))
									archive.CreateEntryFromFile(DayFileName, daybackup);
								if (File.Exists(TodayIniFile))
									archive.CreateEntryFromFile(TodayIniFile, todaybackup);
								if (File.Exists(YesterdayFile))
									archive.CreateEntryFromFile(YesterdayFile, yesterdaybackup);
								if (File.Exists(LogFile))
									archive.CreateEntryFromFile(LogFile, logbackup);
								if (File.Exists(MonthIniFile))
									archive.CreateEntryFromFile(MonthIniFile, monthbackup);
								if (File.Exists(YearIniFile))
									archive.CreateEntryFromFile(YearIniFile, yearbackup);
								if (File.Exists("Cumulus.ini"))
									archive.CreateEntryFromFile("Cumulus.ini", configbackup);
								if (File.Exists("UniqueId.txt"))
									archive.CreateEntryFromFile("UniqueId.txt", uniquebackup);
								if (File.Exists("strings.ini"))
									archive.CreateEntryFromFile("strings.ini", stringsbackup);
							}
							catch (Exception ex)
							{
								LogExceptionMessage(ex, "Backup: Error backing up the data files");
							}

							if (daily)
							{
								// for daily backup the db is in use, so use an online backup
								try
								{
									var backUpDest = dirpath + "cumulusmx.db";
									var zipLocation = datafolder + "cumulusmx.db";
									LogDebugMessage("Making backup copy of the database");
									station.RecentDataDb.Backup(backUpDest);
									LogDebugMessage("Completed backup copy of the database");

									LogDebugMessage("Archiving backup copy of the database");
									archive.CreateEntryFromFile(backUpDest, zipLocation);
									LogDebugMessage("Completed backup copy of the database");

									LogDebugMessage("Deleting backup copy of the database");
									File.Delete(backUpDest);

									backUpDest = dirpath + "diary.db";
									zipLocation = datafolder + "diary.db";
									LogDebugMessage("Making backup copy of the diary");
									DiaryDB.Backup(backUpDest);
									LogDebugMessage("Completed backup copy of the diary");

									LogDebugMessage("Archiving backup copy of the diary");
									archive.CreateEntryFromFile(backUpDest, zipLocation);
									LogDebugMessage("Completed backup copy of the diary");

									LogDebugMessage("Deleting backup copy of the diary");
									File.Delete(backUpDest);
								}
								catch (Exception ex)
								{
									LogExceptionMessage(ex, "Error making db backup");
								}
							}
							else
							{
								// start-up backup - the db is not yet in use, do a file copy including any recovery files
								try
								{
									LogDebugMessage("Archiving the database");
									if (File.Exists(dbfile))
										archive.CreateEntryFromFile(dbfile, dbBackup);

									if (File.Exists(dbfile + "-journal"))
										archive.CreateEntryFromFile(dbfile + "-journal", dbBackup + "-journal");

									if (File.Exists(diaryfile))
										archive.CreateEntryFromFile(diaryfile, diarybackup);

									if (File.Exists(diaryfile + "-journal"))
										archive.CreateEntryFromFile(diaryfile + "-journal", diarybackup + "-journal");

									LogDebugMessage("Completed archive of the database");
								}
								catch (Exception ex)
								{
									LogExceptionMessage(ex, "Backup: Error backing up the database files");
								}
							}

							try
							{
								if (File.Exists(extraFile))
									archive.CreateEntryFromFile(extraFile, extraBackup);
								if (File.Exists(AirLinkFile))
									archive.CreateEntryFromFile(AirLinkFile, AirLinkBackup);

								// custom logs
								for (var i = 0; i < 10; i++)
								{
									if (CustomIntvlLogSettings[i].Enabled)
									{
										var custfilename = GetCustomIntvlLogFileName(i, timestamp);
										if (File.Exists(custfilename))
											archive.CreateEntryFromFile(custfilename, datafolder + Path.GetFileName(custfilename));
									}

									if (CustomDailyLogSettings[i].Enabled)
									{
										var custfilename = GetCustomDailyLogFileName(i);
										if (File.Exists(custfilename))
											archive.CreateEntryFromFile(custfilename, datafolder + Path.GetFileName(custfilename));
									}
								}

								// Do not do this extra backup between 00:00 & Roll-over hour on the first of the month
								// as the month has not yet rolled over - only applies for start-up backups
								if (timestamp.Day == 1 && timestamp.Hour >= RolloverHour)
								{
									var newTime = timestamp.AddDays(-1);
									// on the first of month, we also need to backup last months files as well
									var LogFile2 = GetLogFileName(newTime);
									var logbackup2 = datafolder + Path.GetFileName(LogFile2);

									var extraFile2 = GetExtraLogFileName(newTime);
									var extraBackup2 = datafolder + Path.GetFileName(extraFile2);

									var AirLinkFile2 = GetAirLinkLogFileName(timestamp.AddDays(-1));
									var AirLinkBackup2 = datafolder + Path.GetFileName(AirLinkFile2);

									if (File.Exists(LogFile2))
										archive.CreateEntryFromFile(LogFile2, logbackup2);
									if (File.Exists(extraFile2))
										archive.CreateEntryFromFile(extraFile2, extraBackup2);
									if (File.Exists(AirLinkFile2))
										archive.CreateEntryFromFile(AirLinkFile2, AirLinkBackup2);

									for (var i = 0; i < 10; i++)
									{
										if (CustomIntvlLogSettings[i].Enabled)
										{
											var custfilename = GetCustomIntvlLogFileName(i, newTime);
											if (File.Exists(custfilename))
												archive.CreateEntryFromFile(custfilename, datafolder + Path.GetFileName(custfilename));
										}
									}
								}
							}
							catch (Exception ex)
							{
								LogExceptionMessage(ex, "Backup: Error backing up extra log files");
							}
						}

						LogMessage("Created backup file " + filename);
					}
					catch (UnauthorizedAccessException)
					{
						LogErrorMessage("BackupData: Error, no permission to create/write file: " + dirpath + DirectorySeparator + filename);
						LogConsoleMessage("Error, no permission to create/write file: " + dirpath + DirectorySeparator + filename, ConsoleColor.Yellow);
					}
					catch (Exception ex)
					{
						LogErrorMessage($"BackupData: Error while attempting to create/write file: {dirpath + DirectorySeparator + filename}, error message: {ex.Message}");
						LogConsoleMessage($"Error while attempting to create/write file: {dirpath + DirectorySeparator + filename}, error message: {ex.Message}", ConsoleColor.Yellow);
					}
				}
				else
				{
					LogMessage("Backup file " + filename + " already exists, skipping backup");
				}
			}
			else
			{
				LogMessage("BackupData: *** Error - Failed to create the backup folder - " + dirpath);
			}
		}


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
					if (Use10amInSummer && TimeZoneInfo.Local.SupportsDaylightSavingTime)
					{
						var dst = TimeZoneInfo.Local.IsDaylightSavingTime(timestamp);

						// Irish time zone is unique in it uses standard time in summer and -1 hour in winter. So winter is flagged as using DST!
						// So reverse the DST in Ireland TZ
						if (TimeZoneInfo.Local.StandardName == "IST")
						{
							dst = !dst;
						}

						// If in DST then return 10am, else 9am
						return dst ? -10 : -9;
					}
					else
					{
						// Either no "use 10am in summer", or TZ does not support DST
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

		public static string Beaufort(double Bspeed) // Takes speed in current unit, returns Bft number as text
		{
			return ConvertUnits.Beaufort(Bspeed).ToString();
		}

		public string BeaufortDesc(double Bspeed)
		{
			// Takes speed in current units, returns Bft description

			// Convert to Force
			var force = ConvertUnits.Beaufort(Bspeed);
			return force switch
			{
				0 => Trans.Calm,
				1 => Trans.Lightair,
				2 => Trans.Lightbreeze,
				3 => Trans.Gentlebreeze,
				4 => Trans.Moderatebreeze,
				5 => Trans.Freshbreeze,
				6 => Trans.Strongbreeze,
				7 => Trans.Neargale,
				8 => Trans.Gale,
				9 => Trans.Stronggale,
				10 => Trans.Storm,
				11 => Trans.Violentstorm,
				12 => Trans.Hurricane,
				_ => Trans.Unknown,
			};
		}


		public void LogCriticalMessage(string message)
		{
			LogMessage(message, MxLogLevel.Critical);
		}

		public void LogErrorMessage(string message)
		{
			LogMessage(message, MxLogLevel.Error);
		}

		public void LogWarningMessage(string message)
		{
			LogMessage(message, MxLogLevel.Warning);
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

			tokenSource.Cancel();
			LogMessage("Stopping timers");
			try { RealtimeTimer.Stop(); } catch { /* do nothing */ }
			try { Wund.IntTimer.Stop(); } catch { /* do nothing */ }
			try { WebTimer.Stop(); } catch { /* do nothing */ }
			try { AWEKAS.IntTimer.Stop(); } catch { /* do nothing */ }
			try { CustomHttpSecondsTimer.Stop(); } catch { /* do nothing */ }
			try { CustomMysqlSecondsTimer.Stop(); } catch { /* do nothing */ }

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
				// If we have a JSON Extra Sensors, stop it
				stationJsonExtra?.Stop();

				LogMessage("Extra sensors stopped");

			}
			catch
			{
				/* do nothing */
			}

			if (station != null)
			{
				LogMessage("Stopping station...");
				try
				{
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
					station.SaveWindData();

					station.Stop();
					LogMessage("Station stopped");
				}
				catch
				{
					/* do nothing */
				}
			}

			// do we have a shutdown task to run?
			if (!string.IsNullOrEmpty(ProgramOptions.ShutdownTask))
			{
				if (!File.Exists(ProgramOptions.ShutdownTask))
				{
					LogWarningMessage($"Warning: Shutdown eextrernal task: '{ProgramOptions.ShutdownTask}' does not exist");
				}
				else
				{
					try
					{
						var args = string.Empty;

						if (!string.IsNullOrEmpty(ProgramOptions.ShutdownTaskParams))
						{
							var parser = new TokenParser(TokenParserOnToken)
							{
								InputText = ProgramOptions.ShutdownTaskParams
							};
							args = parser.ToStringFromString();
						}
						LogMessage($"Running shutdown task: {ProgramOptions.ShutdownTask}, arguments: {args}");
						_ = Utils.RunExternalTask(ProgramOptions.ShutdownTask, args, false);
					}
					catch (FileNotFoundException)
					{
						LogErrorMessage("Error running shutdown task: file not found - " + ProgramOptions.ShutdownTask);
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "Error running shutdown task");
					}
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
			catch
			{
				/* do nothing */
			}

			LogMessage("Station shutdown complete");
		}

		public async Task DoHTMLFiles()
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
							ProcessTemplateFile(StdWebFiles[i].TemplateFileName, destFile, true, UTF8encode);
						}
					}
				}
				LogDebugMessage("Interval: Done creating standard Data file");

				LogDebugMessage("Interval: Creating graph data files");
				station.CreateGraphDataFiles();
				LogDebugMessage("Interval: Done creating graph data files");

				LogDebugMessage("Interval: Creating extra files");

				// handle any extra files
				foreach (var item in ActiveExtraFiles)
				{
					if (item.FTP || item.realtime || item.endofday) // eod files are copied in DoExtraEndOfDayFiles()
					{
						continue;
					}

					var uploadfile = GetUploadFilename(item.local, DateTime.Now);

					if (File.Exists(uploadfile))
					{
						var remotefile = GetRemoteFileName(item.remote, DateTime.Now);

						LogDebugMessage($"Interval: Copying extra file {uploadfile} to {remotefile}");
						try
						{
							if (item.process)
							{
								LogDebugMessage($"Interval: Processing extra file {uploadfile}");
								var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
								await File.WriteAllTextAsync(remotefile, data);
							}
							else
							{
								// just copy the file
								File.Copy(uploadfile, remotefile, true);
							}
						}
						catch (Exception ex)
						{
							LogDebugMessage($"Interval: Error copying extra file: " + ex.Message);
						}
					}
					else
					{
						LogWarningMessage($"Interval: Warning, extra web file not found - {uploadfile}");
					}
				}

				LogDebugMessage("Interval: Done creating extra files");

				if (!string.IsNullOrEmpty(ExternalProgram))
				{
					if (!File.Exists(ExternalProgram))
					{
						LogWarningMessage($"Warning: Interval: External program '{ExternalProgram}' does not exist");
					}
					else
					{
						try
						{
							var args = string.Empty;

							if (!string.IsNullOrEmpty(ExternalParams))
							{
								var parser = new TokenParser(TokenParserOnToken)
								{
									InputText = ExternalParams
								};
								args = parser.ToStringFromString();
							}
							LogDebugMessage("Interval: Executing external program " + ExternalProgram + " " + args);
							_ = Utils.RunExternalTask(ExternalProgram, args, false);
							LogDebugMessage("Interval: External program started");
						}
						catch (FileNotFoundException)
						{
							LogWarningMessage("Interval: Error starting external program: file not found - " + ExternalProgram);
						}
						catch (Exception ex)
						{
							LogWarningMessage("Interval: Error starting external program: " + ex.Message);
						}
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
				LogErrorMessage("LocalCopy: Error with paths - " + ex.Message);
			}


			var srcfile = string.Empty;
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
						LogErrorMessage("LocalCopy: Error copying NOAA reports - " + ex.Message);
					}
				}
				catch (Exception e)
				{
					LogErrorMessage($"LocalCopy: Error copying file {srcfile} - {e.Message}");
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
						LogErrorMessage($"LocalCopy: Error copying standard data file [{StdWebFiles[i].LocalFileName}]");
						LogMessage($"LocalCopy: Error = {e.Message}");
						failed++;
					}
				}
			}
			LogDebugMessage($"LocalCopy: Done copying standard web files - Success: {success}, Failed: {failed}");

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
						LogErrorMessage($"LocalCopy: Error copying graph data file [{srcfile}]");
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
						LogErrorMessage($"LocalCopy: Error copying daily graph data file [{srcfile}]");
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
					LogErrorMessage($"LocalCopy: Error copying moon image - {e.Message}");
				}
			}

			LogDebugMessage("LocalCopy: Copy process complete");
		}


		public void DoHttpFiles(DateTime now)
		{
			// sanity check - is there anything to do?
			if (!Array.Exists(HttpFilesConfig, x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now))
			{
#if DEBUG
				LogDebugMessage("ProcessHttpFiles: No files to process at this time");
#endif
				return;
			}

			// second sanity check, are there any local copies?
			var localOnly = HttpFilesConfig.Where(x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now && !x.Upload);
			if (localOnly.Any())
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
				}

				LogDebugMessage("ProcessHttpFiles: Done creating local http file tasks, not waiting for them to complete");
			}

			// third sanity check, are there any uploads?
			var uploads = HttpFilesConfig.Where(x => x.Enabled && x.Url.Length > 0 && x.Remote.Length > 0 && x.NextDownload <= now && x.Upload);
			if (!uploads.Any())
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
					using SftpClient conn = new SftpClient(connectionInfo);
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
						catch (Exception ex) when (ex is not TaskCanceledException)
						{
							LogExceptionMessage(ex, $"ProcessHttpFiles: Error uploading http file {item.Url} to: {item.Remote}");
						}
						finally
						{
							if (null != strm)
								strm.Dispose();
						}
					}

					LogDebugMessage("ProcessHttpFiles: Done uploading http files");


					if (conn.IsConnected)
					{
						try
						{
							// do not error on disconnect
							conn.Disconnect();
						}
						catch { /* do nothing */ }
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
					if (FtpOptions.Logging)
					{
						conn.Logger = new FtpLogAdapter(FtpLoggerIN);
					}

					LogFtpMessage(""); // insert a blank line
					LogFtpDebugMessage($"ProcessHttpFiles: CumulusMX Connecting to " + FtpOptions.Hostname);
					conn.Host = FtpOptions.Hostname;
					conn.Port = FtpOptions.Port;
					conn.Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password);
					conn.Config.LogPassword = false;

					if (!FtpOptions.AutoDetect)
					{

						if (FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							// Explicit = Current protocol - connects using FTP and switches to TLS
							// Implicit = Old depreciated protocol - connects using TLS
							conn.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
							conn.Config.DataConnectionEncryption = true;
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
						catch (Exception ex) when (ex is not TaskCanceledException)
						{
							LogExceptionMessage(ex, $"ProcessHttpFiles: Error uploading http file {item.Url} to: {item.Remote}");
						}
						finally
						{
							if (null != strm)
								strm.Dispose();
						}
					}

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

				// do we perform a second chance compresssion test?
				if (FtpOptions.PhpCompression == "notchecked")
				{
					TestPhpUploadCompression();
				}

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
						catch (Exception ex) when (ex is not TaskCanceledException)
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
				Task.WaitAll([.. tasklist], cancellationToken);

				if (cancellationToken.IsCancellationRequested)
				{
					LogDebugMessage($"ProcessHttpFiles: Upload process aborted");
					return;
				}

				LogDebugMessage($"ProcessHttpFiles: Upload process complete, {tasklist.Count} files processed");
				tasklist.Clear();
			}
		}

		public async Task DoIntervalUpload()
		{
			var remotePath = string.Empty;

			if (!FtpOptions.Enabled || !FtpOptions.IntervalEnabled)
				return;

			if (FtpOptions.Directory.Length > 0)
			{
				remotePath = FtpOptions.Directory.EndsWith('/') ? FtpOptions.Directory : FtpOptions.Directory + '/';
			}

			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				LogDebugMessage("SFTP[Int]: Process starting");
				// BUILD 3092 - added alternate SFTP authentication options
				ConnectionInfo connectionInfo;
				if (FtpOptions.SshAuthen == "password")
				{
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password));
					LogDebugMessage("SFTP[Int]: Connecting using password authentication");
				}
				else if (FtpOptions.SshAuthen == "psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogDebugMessage("SFTP[Int]: Connecting using PSK authentication");
				}
				else if (FtpOptions.SshAuthen == "password_psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password), new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogDebugMessage("SFTP[Int]: Connecting using password or PSK authentication");
				}
				else
				{
					LogErrorMessage($"SFTP[Int]: Invalid SshftpAuthentication specified [{FtpOptions.SshAuthen}]");
					return;
				}

				try
				{
					using SftpClient conn = new SftpClient(connectionInfo);
					try
					{
						LogDebugMessage($"SFTP[Int]: CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}");
						conn.Connect();
						if (ServicePointManager.DnsRefreshTimeout == 0)
						{
							ServicePointManager.DnsRefreshTimeout = 120000; // two minutes default
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"SFTP[Int]: Error connecting SFTP - {ex.Message}");

						FtpAlarm.LastMessage = "Error connecting SFTP - " + ex.Message;
						FtpAlarm.Triggered = true;

						if ((uint) ex.HResult == 0x80004005) // Could not resolve host
						{
							// Disable the DNS cache for the next query
							ServicePointManager.DnsRefreshTimeout = 0;
						}
						return;
					}

					if (conn.IsConnected)
					{
						LogDebugMessage($"SFTP[Int]: CumulusMX Connected to {FtpOptions.Hostname} OK");

						if (NOAAconf.NeedFtp)
						{
							try
							{
								// upload NOAA reports
								LogDebugMessage("SFTP[Int]: Uploading NOAA reports");

								var uploadfile = ReportPath + NOAAconf.LatestMonthReport;
								var remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestMonthReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								uploadfile = ReportPath + NOAAconf.LatestYearReport;
								remotefile = NOAAconf.FtpFolder + '/' + NOAAconf.LatestYearReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								LogDebugMessage("SFTP[Int]: Done uploading NOAA reports");
							}
							catch (Exception e)
							{
								LogErrorMessage($"SFTP[Int]: Error uploading file - {e.Message}");
								FtpAlarm.LastMessage = "Error uploading NOAA report file - " + e.Message;
								FtpAlarm.Triggered = true;
							}
							NOAAconf.NeedFtp = false;
						}

						// Extra files
						for (var i = 0; i < ActiveExtraFiles.Count; i++)
						{
							var item = ActiveExtraFiles[i];

							if (!item.FTP || item.realtime || (item.endofday && !EODfilesNeedFTP))
							{
								continue;
							}

							// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
							var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;
							var uploadfile = GetUploadFilename(item.local, logDay);

							if (!File.Exists(uploadfile))
							{
								LogWarningMessage($"SFTP[Int]: Extra web file [{uploadfile}] not found!");
								FtpAlarm.LastMessage = $"Error Extra web file [{uploadfile} not found";
								FtpAlarm.Triggered = true;
								continue;
							}

							var remotefile = GetRemoteFileName(item.remote, logDay);

							LogDebugMessage("SFTP[Int]: Uploading Extra web file: " + uploadfile);

							// all checks OK, file needs to be uploaded
							try
							{
								// Is this an incremental log file upload?
								if (item.incrementalLogfile && !item.binary)
								{
									// has the log file rolled over?
									if (item.logFileLastFileName != uploadfile)
									{
										ActiveExtraFiles[i].logFileLastFileName = uploadfile;
										ActiveExtraFiles[i].logFileLastLineNumber = 0;
									}

									var linesAdded = 0;
									var data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

									if (linesAdded == 0)
									{
										LogDebugMessage($"SFTP[Int]: Extra web file: {uploadfile} - No incremental data found");
										continue;
									}

									// have we already uploaded the base file?
									if (item.logFileLastLineNumber > 0)
									{
										if (AppendText(conn, remotefile, data, -1, linesAdded))
										{
											ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
										}
									}
									else // no, just upload the base file
									{
										if (UploadFile(conn, uploadfile, remotefile, -1))
										{
											ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
										}
									}
								}
								else if (item.process)
								{
									LogDebugMessage("SFTP[Int]: Processing Extra web file: " + uploadfile);
									var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
									using var strm = GenerateStreamFromString(data);
									UploadStream(conn, remotefile, strm, -1);
								}
								else
								{
									UploadFile(conn, uploadfile, remotefile, -1);
								}
							}
							catch (Exception e)
							{
								LogErrorMessage($"SFTP[Int]: Error uploading Extra web file [{uploadfile}]");
								LogMessage($"SFTP[Int]: Error = {e.Message}");
								FtpAlarm.LastMessage = $"Error uploading Extra web file [{uploadfile}";
								FtpAlarm.Triggered = true;
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
									LogDebugMessage("SFTP[Int]: Uploading standard Data file: " + localFile);

									string data;

									if (StdWebFiles[i].LocalFileName == "wxnow.txt")
									{
										data = station.CreateWxnowFileString();
									}
									else
									{
										data = await ProcessTemplateFile2StringAsync(StdWebFiles[i].TemplateFileName, true, true);
									}

									using var dataStream = GenerateStreamFromString(data);
									UploadStream(conn, remotefile, dataStream);
								}
								catch (Exception e)
								{
									LogErrorMessage($"SFTP[Int]: Error uploading standard data file [{StdWebFiles[i].RemoteFileName}]");
									LogMessage($"SFTP[Int]: Error = {e}");
									FtpAlarm.LastMessage = $"Error uploading standard web file {StdWebFiles[i].RemoteFileName} - {e.Message}";
									FtpAlarm.Triggered = true;
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
									LogDebugMessage("SFTP[Int]: Uploading graph data file: " + uploadfile);

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
									LogErrorMessage($"SFTP[Int]: Error uploading graph data file [{uploadfile}]");
									LogMessage($"SFTP[Int]: Error = {e}");
									FtpAlarm.LastMessage = $"Error uploading graph data file [{uploadfile}] - {e.Message}";
									FtpAlarm.Triggered = true;
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
									LogDebugMessage("SFTP[Int]: Uploading daily graph data file: " + uploadfile);

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
									LogErrorMessage($"SFTP[Int]: Error uploading daily graph data file [{uploadfile}]");
									LogMessage($"SFTP[Int]: Error = {e}");
									FtpAlarm.LastMessage = $"Error uploading daily graph data file [{uploadfile}] - {e.Message}";
									FtpAlarm.Triggered = true;
								}
							}
						}

						if (MoonImage.Ftp && MoonImage.ReadyToFtp)
						{
							try
							{
								LogDebugMessage("SFTP[Int]: Uploading Moon image file");
								UploadFile(conn, "web" + DirectorySeparator + "moon.png", remotePath + MoonImage.FtpDest, -1);
								LogDebugMessage("SFTP[Int]: Done uploading Moon image file");
								// clear the image ready for FTP flag, only upload once an hour
								MoonImage.ReadyToFtp = false;
							}
							catch (Exception e)
							{
								LogErrorMessage($"SFTP[Int]: Error uploading moon image - {e.Message}");
								FtpAlarm.LastMessage = $"Error uploading moon image - {e.Message}";
								FtpAlarm.Triggered = true;
							}
						}
					}

					try
					{
						// do not error on disconnect
						conn.Disconnect();
					}
					catch
					{
						// do nothing
					}
				}
				catch (Exception ex)
				{
					LogErrorMessage($"SFTP[Int]: Error using SFTP connection - {ex.Message}");
				}
				LogDebugMessage("SFTP[Int]: Process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || (FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				using (FtpClient conn = new FtpClient())
				{
					if (FtpOptions.Logging)
					{
						conn.Logger = new FtpLogAdapter(FtpLoggerIN);
					}

					LogFtpMessage(""); // insert a blank line
					LogFtpDebugMessage($"FTP[Int]: CumulusMX Connecting to " + FtpOptions.Hostname);
					conn.Host = FtpOptions.Hostname;
					conn.Port = FtpOptions.Port;
					conn.Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password);
					conn.Config.LogPassword = false;

					if (!FtpOptions.AutoDetect)
					{

						if (FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							// Explicit = Current protocol - connects using FTP and switches to TLS
							// Implicit = Old depreciated protocol - connects using TLS
							conn.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
							conn.Config.DataConnectionEncryption = true;
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

						FtpAlarm.LastMessage = "Error connecting ftp - " + ex.Message;
						FtpAlarm.Triggered = true;

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
								FtpAlarm.LastMessage = "Error connecting ftp - " + e.Message;
								FtpAlarm.Triggered = true;
							}
							NOAAconf.NeedFtp = false;
						}

						// Extra files
						for (var i = 0; i < ActiveExtraFiles.Count; i++)
						{
							var item = ActiveExtraFiles[i];

							if (!item.FTP || item.realtime || (item.endofday && !EODfilesNeedFTP))
							{
								continue;
							}

							// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
							var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;
							var uploadfile = GetUploadFilename(item.local, logDay);

							if (!File.Exists(uploadfile))
							{
								LogFtpMessage($"FTP[Int]: Extra web file [{uploadfile}] not found!");
								FtpAlarm.LastMessage = $"Error Extra web file [{uploadfile} not found";
								FtpAlarm.Triggered = true;
								continue;
							}

							var remotefile = GetRemoteFileName(item.remote, logDay);

							LogFtpMessage("");
							LogFtpDebugMessage("FTP[Int]: Uploading Extra web file: " + uploadfile);

							// all checks OK, file needs to be uploaded

							try
							{
								// Is this an incremental log file upload?
								if (item.incrementalLogfile && !item.binary)
								{
									// has the log file rolled over?
									if (item.logFileLastFileName != uploadfile)
									{
										ActiveExtraFiles[i].logFileLastFileName = uploadfile;
										ActiveExtraFiles[i].logFileLastLineNumber = 0;
									}

									var linesAdded = 0;
									var data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

									if (linesAdded == 0)
									{
										LogDebugMessage($"FTP[Int]: Extra web file: {uploadfile} - No incremental data found");
										continue;
									}

									// have we already uploaded the base file?
									if (item.logFileLastLineNumber > 0)
									{
										if (AppendText(conn, remotefile, data, -1, linesAdded))
										{
											ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
										}
									}
									else // no, just upload the base file
									{
										if (UploadFile(conn, uploadfile, remotefile, -1))
										{
											ActiveExtraFiles[i].logFileLastLineNumber += linesAdded;
										}
									}
								}
								else if (item.process)
								{
									LogFtpDebugMessage("FTP[Int]: Processing Extra web file: " + uploadfile);
									var data = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
									using var strm = GenerateStreamFromString(data);
									UploadStream(conn, remotefile, strm, -1);
								}
								else
								{
									UploadFile(conn, uploadfile, remotefile, -1);
								}
							}
							catch (Exception e)
							{
								LogFtpMessage($"FTP[Int]: Error uploading file {uploadfile}: {e.Message}");
								FtpAlarm.LastMessage = $"Error uploading extra file {uploadfile} - {e.Message}";
								FtpAlarm.Triggered = true;
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
										data = await ProcessTemplateFile2StringAsync(StdWebFiles[i].TemplateFileName, true, true);
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
									FtpAlarm.LastMessage = $"Error uploading file {StdWebFiles[i].RemoteFileName} - {e.Message}";
									FtpAlarm.Triggered = true;
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
									FtpAlarm.LastMessage = $"Error uploading file {GraphDataFiles[i].RemoteFileName} - {e.Message}";
									FtpAlarm.Triggered = true;
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
									FtpAlarm.LastMessage = $"Error uploading file {GraphDataEodFiles[i].RemoteFileName} - {e.Message}";
									FtpAlarm.Triggered = true;
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
								LogErrorMessage($"FTP[Int]: Error uploading moon image - {e.Message}");
								FtpAlarm.LastMessage = $"Error uploading moon image - {e.Message}";
								FtpAlarm.Triggered = true;
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

				// do we perform a second chance compresssion test?
				if (FtpOptions.PhpCompression == "notchecked")
				{
					TestPhpUploadCompression();
				}

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
							FtpAlarm.LastMessage = $"Error uploading NOAA files - {ex.Message}";
							FtpAlarm.Triggered = true;
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
							FtpAlarm.LastMessage = $"Error uploading NOAA files - {ex.Message}";
							FtpAlarm.Triggered = true;
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

				for (var i = 0; i < ActiveExtraFiles.Count; i++)
				{
					var item = ActiveExtraFiles[i];

					if (!item.FTP || item.realtime || (item.endofday && !EODfilesNeedFTP))
					{
						continue;
					}

					Interlocked.Increment(ref taskCount);

					var data = string.Empty;
					bool incremental = false;
					var linesAdded = 0;
					var idx = i;

					// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
					var logDay = item.endofday ? DateTime.Now.AddDays(-1) : DateTime.Now;

					var uploadfile = GetUploadFilename(item.local, logDay);
					var remotefile = GetRemoteFileName(item.remote, logDay);

					if (!File.Exists(uploadfile))
					{
						LogWarningMessage($"PHP[Int]: Extra web file - {uploadfile} - not found!");
						return;
					}


					// Is this an incremental log file upload?
					if (item.incrementalLogfile && !item.binary)
					{
						// has the log file rolled over?
						if (item.logFileLastFileName != uploadfile)
						{
							ActiveExtraFiles[i].logFileLastFileName = uploadfile;
							ActiveExtraFiles[i].logFileLastLineNumber = 0;
						}

						incremental = item.logFileLastLineNumber > 0;

						data = WeatherStation.GetIncrementalLogFileData(uploadfile, item.logFileLastLineNumber, out linesAdded);

						if (linesAdded == 0)
						{
							LogDebugMessage($"PHP[Int]: Extra file: {uploadfile} - No incremental data found");
							continue;
						}
					}

					try
					{
#if DEBUG
						LogDebugMessage($"PHP[Int]: Extra file: {uploadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
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

							// all checks OK, file needs to be uploaded
							// Is this an incremental log file upload?
							if (item.incrementalLogfile && !item.binary)
							{
								LogDebugMessage($"PHP[Int]: Uploading extra web incremental file {uploadfile} to {remotefile} ({(incremental ? $"Incrementally - {linesAdded} lines" : "Fully")})");
								if (await UploadString(phpUploadHttpClient, incremental, string.Empty, data, remotefile, -1, item.binary, item.UTF8, true, item.logFileLastLineNumber))
								{
									ActiveExtraFiles[idx].logFileLastLineNumber += linesAdded;
								}
							}
							else
							{
								if (item.process)
								{
									LogDebugMessage($"PHP[Int]: Uploading Extra file: {uploadfile} to: {remotefile} (Processed)");

									var str = await ProcessTemplateFile2StringAsync(uploadfile, false, item.UTF8);
									_ = await UploadString(phpUploadHttpClient, false, string.Empty, str, remotefile, -1, false, item.UTF8);
								}
								else
								{
									LogDebugMessage($"PHP[Int]: Uploading Extra file: {uploadfile} to: {remotefile}");

									_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, -1, false, item.UTF8);
								}
							}
						}
						catch (Exception ex) when (ex is not TaskCanceledException)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading file {uploadfile} to: {remotefile}");
							FtpAlarm.LastMessage = $"Error uploading file {uploadfile} to: {remotefile} - {ex.Message}";
							FtpAlarm.Triggered = true;
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
				}

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
								//StdWebFiles[i].FtpRequired = false
							}
						}
						catch (Exception ex)
						{
							LogExceptionMessage(ex, $"PHP[Int]: Error uploading file {item.RemoteFileName}");
							FtpAlarm.LastMessage = $"Error uploading file {item.RemoteFileName} - {ex.Message}";
							FtpAlarm.Triggered = true;
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
					try
					{
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
							LogDebugMessage($"PHP[Int]: Uploading graph data file ({(item.Incremental ? "full" : $"incremental from {item.LastDataTime:s}")}): {item.LocalFileName}");

							if (await UploadString(phpUploadHttpClient, item.Incremental, oldestTs, json, remotefile, -1, false, true))
							{
								// The config files only need uploading once per change
								// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
								if (Array.Exists(configFiles, item.LocalFileName.Contains))
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
							FtpAlarm.LastMessage = $"Error uploading graph data file [{item.RemoteFileName}] - {ex.Message}";
							FtpAlarm.Triggered = true;
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
							FtpAlarm.LastMessage = $"Error uploading daily graph data file [{item.RemoteFileName}] - {ex.Message}";
							FtpAlarm.Triggered = true;
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
							FtpAlarm.LastMessage = $"Error uploading moon image - {ex.Message}";
							FtpAlarm.Triggered = true;
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
						// wait for all the tasks to complete, or timeout
						if (Task.WaitAll([.. tasklist], TimeSpan.FromSeconds(30)))
						{
							LogDebugMessage($"PHP[Int]: Upload process complete, {tasklist.Count} files processed");
						}
						else
						{
							LogErrorMessage("PHP[Int]: Upload process complete timed out waiting for tasks to complete");
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, "PHP[Int]: Error waiting on upload tasks");
						FtpAlarm.LastMessage = "Error waiting on upload tasks";
						FtpAlarm.Triggered = true;
					}
				}

				if (cancellationToken.IsCancellationRequested)
				{
					LogDebugMessage($"PHP[Int]: Upload process aborted");
					return;
				}

				LogDebugMessage($"PHP[Int]: Upload process complete");
				tasklist.Clear();

				return;
			}
		}

		// Return True if the connection still exists
		// Return False if the connection is disposed, null, or not connected
		private bool UploadFile(FtpClient conn, string localfile, string remotefile, int cycle = -1)
		{
			string cycleStr;
			if (cycle == 9999)
			{
				cycleStr = "NOAA";
			}
			else
			{
				cycleStr = cycle == -1 ? "Int" : (cycle.ToString());
			}

			LogFtpMessage("");

			try
			{
				if (!File.Exists(localfile))
				{
					LogWarningMessage($"FTP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
					FtpAlarm.LastMessage = $"Error! Local file not found, aborting upload: {localfile}";
					FtpAlarm.Triggered = true;
					return true;
				}

				using Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				UploadStream(conn, remotefile, istream, cycle);
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error reading {localfile} - {ex.Message}");
				FtpAlarm.LastMessage = $"Error reading {localfile} - {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					LogFtpMessage($"FTP[{cycleStr}]: Inner Exception: {ex.GetBaseException().Message}");
				}
			}

			return conn.IsConnected;
		}

		private bool UploadFile(SftpClient conn, string localfile, string remotefile, int cycle = -1)
		{
			string cycleStr;
			if (cycle == 9999)
			{
				cycleStr = "NOAA";
			}
			else
			{
				cycleStr = cycle == -1 ? "Int" : (cycle.ToString());
			}

			if (!File.Exists(localfile))
			{
				LogWarningMessage($"SFTP[{cycleStr}]: Error! Local file not found, aborting upload: {localfile}");
				FtpAlarm.LastMessage = $"Error! Local file not found, aborting upload: {localfile}";
				FtpAlarm.Triggered = true;

				return true;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {localfile}");

					if (cycle >= 0)
						_ = RealtimeFTPReconnect();

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {localfile}");
				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {localfile}";
				FtpAlarm.Triggered = true;

				if (cycle >= 0)
					_ = RealtimeFTPReconnect();

				return false;
			}

			try
			{
				using Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				UploadStream(conn, remotefile, istream, cycle);
			}
			catch (Exception ex)
			{
				LogWarningMessage($"SFTP[{cycleStr}]: Error reading {localfile} - {ex.Message}");

				FtpAlarm.LastMessage = $"Error reading {localfile} - {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogDebugMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}");
				}

			}
			return true;
		}

		private async Task<bool> UploadFile(HttpClient httpclient, string localfile, string remotefile, int cycle = -1, bool binary = false, bool utf8 = true)
		{
			string prefix;
			if (cycle == -1)
			{
				prefix = "PHP[Int]";
			}
			else
			{
				prefix = cycle == 9999 ? "PHP[NOAA]" : $"RealtimePHP[{cycle}]";
			}

			if (!File.Exists(localfile))
			{
				LogWarningMessage($"{prefix}: Error! Local file not found, aborting upload: {localfile}");

				FtpAlarm.LastMessage = $"Error! Local file not found, aborting upload: {localfile}";
				FtpAlarm.Triggered = true;

				return false;
			}

			// we will try this twice in case the first attempt fails
			var retry = 0;
			do
			{
				try
				{
					string data;
					if (binary)
					{
						// change binary files to base64 string
						data = Convert.ToBase64String(await Utils.ReadAllBytesAsync(localfile));
					}
					else
					{
						var encoding = utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("iso-8859-1");
						data = await Utils.ReadAllTextAsync(localfile, encoding);
					}

					return await UploadString(httpclient, false, string.Empty, data, remotefile, cycle, binary, utf8);
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"{prefix}: Error uploading to {remotefile}", false);

					retry++;

					if (retry < 2)
					{
						LogDebugMessage($"{prefix}: Error uploading to {remotefile} - {ex.Message}");
						LogMessage($"{prefix}: Retrying upload to {remotefile}");
					}
					else
					{
						LogErrorMessage($"{prefix}: Error uploading to {remotefile} - {ex.Message}");
						FtpAlarm.LastMessage = $"{prefix}: Error uploading to {remotefile} - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
				}
			} while (retry < 2);

			return false;
		}

		private bool UploadStream(FtpClient conn, string remotefile, Stream dataStream, int cycle = -1)
		{
			string remotefiletmp = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr;
			if (cycle == 9999)
			{
				cycleStr = "NOAA";
			}
			else
			{
				cycleStr = cycle == -1 ? "Int" : (cycle.ToString());
			}

			try
			{
				if (dataStream.Length == 0)
				{
					LogWarningMessage($"FTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
					FtpAlarm.LastMessage = $"The data is empty - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;

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

				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {remotefiletmp}");

				FtpStatus status;
				status = conn.UploadStream(dataStream, remotefiletmp, DeleteBeforeUpload ? FtpRemoteExists.Overwrite : FtpRemoteExists.NoCheck);

				if (status.IsFailure())
				{
					LogErrorMessage($"FTP[{cycleStr}]: Upload of {remotefile} failed");
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

						FtpAlarm.LastMessage = $"Error renaming {remotefiletmp} to {remotefile} : {ex.Message}";
						FtpAlarm.Triggered = true;

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

				FtpAlarm.LastMessage = $"Error uploading {remotefile} : {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					LogFtpMessage($"FTP[{cycleStr}]: Inner Exception: {ex.GetBaseException().Message}");
				}
			}

			return conn.IsConnected;
		}

		// Return True if the connection still exists
		// Return False if the connection is disposed, null, or not connected
		private bool UploadStream(SftpClient conn, string remotefile, Stream dataStream, int cycle = -1)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (dataStream.Length == 0)
			{
				LogWarningMessage($"SFTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
				FtpAlarm.LastMessage = $"The data is empty - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {remotefile}");
					FtpAlarm.LastMessage = $"The SFTP object is null or not connected - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;

					if (cycle >= 0)
						_ = RealtimeFTPReconnect();

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {remotefile}");

				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;

				if (cycle >= 0)
					_ = RealtimeFTPReconnect();

				return false;
			}

			try
			{
				// No delete before upload required for SFTP as we use the overwrite flag
				try
				{
					LogDebugMessage($"SFTP[{cycleStr}]: Uploading {remotefilename}");

					conn.OperationTimeout = TimeSpan.FromSeconds(15);
					conn.UploadFile(dataStream, remotefilename); // defaults to CreateNewOrOpen
					dataStream.Close();

					LogDebugMessage($"SFTP[{cycleStr}]: Uploaded {remotefilename}");
				}
				catch (ObjectDisposedException)
				{
					LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
					FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;
					return false;
				}
				catch (Exception ex)
				{
					LogErrorMessage($"SFTP[{cycleStr}]: Error uploading {remotefilename} : {ex.Message}");

					FtpAlarm.LastMessage = $"Error uploading {remotefilename} : {ex.Message}";
					FtpAlarm.Triggered = true;

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
						LogDebugMessage($"SFTP[{cycleStr}]: Renaming {remotefilename} to {remotefile}");
						conn.RenameFile(remotefilename, remotefile, true);
						LogDebugMessage($"SFTP[{cycleStr}]: Renamed {remotefilename}");
					}
					catch (ObjectDisposedException)
					{
						LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
						FtpAlarm.LastMessage = $"The SFTP object is disposed during renaming of {remotefile}";
						FtpAlarm.Triggered = true;
						return false;
					}
					catch (Exception ex)
					{
						LogErrorMessage($"SFTP[{cycleStr}]: Error renaming {remotefilename} to {remotefile} : {ex.Message}");

						FtpAlarm.LastMessage = $"Error renaming {remotefilename} to {remotefile} : {ex.Message}";
						FtpAlarm.Triggered = true;

						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}");
						}

						return true;
					}
				}
				LogDebugMessage($"SFTP[{cycleStr}]: Completed uploading {remotefile}");
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
				FtpAlarm.LastMessage = "The SFTP object is disposed";
				FtpAlarm.Triggered = true;
				return false;
			}
			catch (Exception ex)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: Error uploading {remotefile} - {ex.Message}");

				FtpAlarm.LastMessage = $"Error uploading {remotefile} - {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogDebugMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}");
				}

			}
			return true;
		}

		public static Stream GenerateStreamFromString(string s)
		{
			MemoryStream stream = new MemoryStream();
			StreamWriter writer = new StreamWriter(stream);
			writer.Write(s);
			writer.Flush();
			stream.Position = 0;
			return stream;
		}

		private bool AppendText(SftpClient conn, string remotefile, string text, int cycle, int linesadded)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (text.Length == 0)
			{
				LogWarningMessage($"SFTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
				FtpAlarm.LastMessage = $"The data is empty - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {remotefile}");
					FtpAlarm.LastMessage = $"The SFTP object is null or not connected - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;

					if (cycle >= 0)
						_ = RealtimeFTPReconnect();

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {remotefile}");

				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;

				if (cycle >= 0)
					_ = RealtimeFTPReconnect();

				return false;
			}

			try
			{
				LogDebugMessage($"SFTP[{cycleStr}]: Uploading {remotefile} [adding {linesadded} lines]");

				conn.OperationTimeout = TimeSpan.FromSeconds(15);
				conn.AppendAllText(remotefile, text);

				LogDebugMessage($"SFTP[{cycleStr}]: Uploaded {remotefile} [added {linesadded} lines]");
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed");
				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}
			catch (Exception ex)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: Error uploading {remotefile} : {ex.Message}");

				FtpAlarm.LastMessage = $"Error uploading {remotefile} : {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.Message.Contains("Permission denied")) // Non-fatal
					return true;

				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogDebugMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}");
				}

				// Lets start again anyway! Too hard to tell if the error is recoverable
				conn.Dispose();
				return false;
			}

			return true;
		}

		private bool AppendText(FtpClient conn, string remotefile, string text, int cycle, int linesadded)
		{
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (text.Length == 0)
			{
				LogFtpMessage($"FTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
				FtpAlarm.LastMessage = $"The data is empty - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogFtpMessage($"FTP[{cycleStr}]: The FTP object is null or not connected - skipping upload of {remotefile}");
					FtpAlarm.LastMessage = $"The FTP object is null or not connected - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;

					if (cycle >= 0)
						_ = RealtimeFTPReconnect();

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"FTP[{cycleStr}]: The FTP object is disposed - skipping upload of {remotefile}");

				FtpAlarm.LastMessage = $"The FTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;

				if (cycle >= 0)
					_ = RealtimeFTPReconnect();

				return false;
			}

			try
			{
				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {remotefile} [adding {linesadded} lines]");

				conn.UploadStream(GenerateStreamFromString(text), remotefile, FtpRemoteExists.AddToEnd);

				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploaded {remotefile} [added {linesadded} lines]");
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"FTP[{cycleStr}]: The FTP object is disposed");
				FtpAlarm.LastMessage = $"The FTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error uploading {remotefile} : {ex.Message}");

				FtpAlarm.LastMessage = $"Error uploading {remotefile} : {ex.Message}";
				FtpAlarm.Triggered = true;

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

			return true;
		}


		// Return True if the upload worked
		// Return False if the upload failed
		private async Task<bool> UploadString(HttpClient httpclient, bool incremental, string oldest, string data, string remotefile, int cycle = -1, bool binary = false, bool utf8 = true, bool logfile = false, int linecount = 0)
		{
			var prefix = cycle >= 0 ? $"RealtimePHP[{cycle}]" : "PHP[Int]";

			if (string.IsNullOrEmpty(data))
			{
				LogWarningMessage($"{prefix}: Uploading to {remotefile}. Error: The data string is empty, ignoring this upload");

				return false;
			}

			LogDebugMessage($"{prefix}: Uploading to {remotefile}");

			// we will try this twice in case the first attempt fails
			var retry = -1;
			do
			{
				// retry == 0 is the initial upload
				retry++;

				MemoryStream outStream = null;
				StreamContent streamContent = null;

				try
				{
					var encoding = new UTF8Encoding(false);

					using var request = new HttpRequestMessage();
					var unixTs = Utils.ToUnixTime(DateTime.Now).ToString();

					request.RequestUri = new Uri(FtpOptions.PhpUrl);
					// disable expect 100 - PHP doesn't support it
					request.Headers.ExpectContinue = false;
					request.Headers.Add("FILE", remotefile);
					if (incremental)
					{
						request.Headers.Add("ACTION", "append");
						request.Headers.Add("OLDEST", oldest);
						request.Headers.Add("FILETYPE", logfile ? "logfile" : "json");
						if (logfile)
						{
							request.Headers.Add("LINECOUNT", linecount.ToString());
						}
					}
					else
					{
						request.Headers.Add("ACTION", "replace");
						request.Headers.Add("FILETYPE", "other");
					}

					var signature = Utils.GetSHA256Hash(FtpOptions.PhpSecret, unixTs + remotefile + data);
					request.Headers.Add("SIGNATURE", signature);

					request.Headers.Add("TS", unixTs);
					request.Headers.Add("BINARY", binary ? "1" : "0");
					request.Headers.Add("UTF8", utf8 ? "1" : "0");

					// binary data is already encoded as base 64 text
					string encData = binary ? data : Convert.ToBase64String(encoding.GetBytes(data));

					// if content < 7 KB-ish
					if (encData.Length < 7000 && FtpOptions.PhpUseGet)
					{
						// send data in GET headers
						request.Method = HttpMethod.Get;
						request.Headers.Add("DATA", encData);
					}
					// else > 7 kB or GET is disabled
					else
					{
						// send as POST
						request.Method = HttpMethod.Post;

						// Compress? if supported and payload exceeds 500 bytes
						if (data.Length >= 500 && (FtpOptions.PhpCompression == "gzip" || FtpOptions.PhpCompression == "deflate" || FtpOptions.PhpCompression == "br"))
						{
							using var ms = new MemoryStream();
							var byteData = encoding.GetBytes(data);

							if (FtpOptions.PhpCompression == "gzip")
							{
								using var zipped = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress, true);
								await zipped.WriteAsync(byteData.AsMemory(0, byteData.Length), cancellationToken);
							}
							else if (FtpOptions.PhpCompression == "deflate")
							{
								using var zipped = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true);
								await zipped.WriteAsync(byteData.AsMemory(0, byteData.Length), cancellationToken);
							}
							else if (FtpOptions.PhpCompression == "br")
							{
								using var zipped = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionMode.Compress, true);
								await zipped.WriteAsync(byteData.AsMemory(0, byteData.Length), cancellationToken);
							}

							ms.Position = 0;
							byte[] compressed = new byte[ms.Length];
							_ = await ms.ReadAsync(compressed.AsMemory(0, compressed.Length), cancellationToken);

							outStream = new MemoryStream(compressed);
							streamContent = new StreamContent(outStream);
							streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
							streamContent.Headers.Add("Content-Encoding", FtpOptions.PhpCompression);
							streamContent.Headers.ContentLength = outStream.Length;

							request.Content = streamContent;
						}
						else
						{
							request.Headers.Add("Content_Type", "text/plain");

							outStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
							streamContent = new StreamContent(outStream);
							streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
							streamContent.Headers.ContentLength = outStream.Length;

							request.Content = streamContent;
						}
					}

					LogDebugMessage($"{prefix}: Sending via {request.Method}");

					using var response = await httpclient.SendAsync(request);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();

					CheckPhpMaxUploads(response.Headers);

					if (response.StatusCode == HttpStatusCode.OK)
					{
						LogDebugMessage($"{prefix}: Upload to {remotefile}: Response code = {(int) response.StatusCode}: {response.StatusCode}");
						LogDataMessage($"{prefix}: Upload to {remotefile}: Response text follows:\n{responseBodyAsText}");

						return true;
					}
					else if (incremental && response.StatusCode == HttpStatusCode.NotAcceptable)
					{
						// 406 is reserved for trying to append data to a file that already exists (retry after inital attemp actually worked?)
						// In this case flag success to the increment moved on
						LogDebugMessage($"{prefix}: Upload to {remotefile}: Response code = {(int) response.StatusCode}: {response.StatusCode} - Skipping this increment");
						LogDataMessage($"{prefix}: Upload to {remotefile}: Response text follows:\n{responseBodyAsText}");

						return true;
					}
					else
					{
						LogMessage($"{prefix}: Upload to {remotefile}: Response text follows:\n{responseBodyAsText}");

						if (retry == 2)
						{
							LogWarningMessage($"{prefix}: HTTP Error uploading to {remotefile}: Response code = {(int) response.StatusCode}: {response.StatusCode}");
						}
						else
						{
							await Task.Delay(2000);
						}
					}
				}
				catch (HttpRequestException ex)
				{
					if (retry < 2)
					{
						LogDebugMessage($"{prefix}: HTTP Error uploading to {remotefile} - " + ex.Message);
						LogMessage($"{prefix}: Retrying upload to {remotefile}");
						await Task.Delay(2000);
					}
					else
					{
						LogErrorMessage($"{prefix}: HTTP Error uploading to {remotefile} - " + ex.Message);
						FtpAlarm.LastMessage = $"HTTP Error uploading to {remotefile} - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
				}
				catch (Exception ex)
				{
					if (retry < 2)
					{
						if (ex.InnerException is TimeoutException)
						{
							LogDebugMessage($"{prefix}: Timeout uploading to {remotefile}");
						}
						else
						{
							LogExceptionMessage(ex, $"{prefix}: General Error uploading to {remotefile}", false);
						}

						LogMessage($"{prefix}: Retrying upload to {remotefile}");
						await Task.Delay(2000);
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							LogErrorMessage($"{prefix}]: Timeout uploading to {remotefile}");
							FtpAlarm.LastMessage = $"Timeout uploading to {remotefile}";
						}
						else
						{
							LogExceptionMessage(ex, $"{prefix}: General error uploading to {remotefile}", true);
							FtpAlarm.LastMessage = $"General error uploading to {remotefile} - {ex.Message}";
						}
						FtpAlarm.Triggered = true;
					}
				}
				finally
				{
					if (outStream != null)
						await outStream.DisposeAsync();

					if (streamContent != null)
						streamContent.Dispose();
				}
			} while (retry < 2);

			return false;
		}


		public async Task DoSingleNoaaUpload(string filename)
		{
			var uploadfile = ReportPath + filename;
			var remotefile = NOAAconf.FtpFolder + '/' + filename;

			if (!FtpOptions.Enabled)
				return;

			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				ConnectionInfo connectionInfo;
				if (FtpOptions.SshAuthen == "password")
				{
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password));
					LogFtpDebugMessage("SFTP[NOAA]: Connecting using password authentication");
				}
				else if (FtpOptions.SshAuthen == "psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogFtpDebugMessage("SFTP[NOAA]: Connecting using PSK authentication");
				}
				else if (FtpOptions.SshAuthen == "password_psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(FtpOptions.SshPskFile);
					connectionInfo = new ConnectionInfo(FtpOptions.Hostname, FtpOptions.Port, FtpOptions.Username, new PasswordAuthenticationMethod(FtpOptions.Username, FtpOptions.Password), new PrivateKeyAuthenticationMethod(FtpOptions.Username, pskFile));
					LogFtpDebugMessage("SFTP[NOAA]: Connecting using password or PSK authentication");
				}
				else
				{
					LogFtpMessage($"SFTP[NOAA]: Invalid SshftpAuthentication specified [{FtpOptions.SshAuthen}]");
					return;
				}

				try
				{
					using SftpClient conn = new SftpClient(connectionInfo);
					try
					{
						LogFtpDebugMessage($"SFTP[NOAA]: CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}");
						conn.Connect();
						if (ServicePointManager.DnsRefreshTimeout == 0)
						{
							ServicePointManager.DnsRefreshTimeout = 120000; // two minutes default
						}
					}
					catch (Exception ex)
					{
						LogFtpMessage($"SFTP[NOAA]: Error connecting SFTP - {ex.Message}");

						FtpAlarm.LastMessage = "Error connecting SFTP - " + ex.Message;
						FtpAlarm.Triggered = true;

						if ((uint) ex.HResult == 0x80004005) // Could not resolve host
						{
							// Disable the DNS cache for the next query
							ServicePointManager.DnsRefreshTimeout = 0;
						}
						return;
					}

					if (conn.IsConnected)
					{
						LogFtpDebugMessage($"SFTP[NOAA]: CumulusMX Connected to {FtpOptions.Hostname} OK");
						try
						{
							// upload NOAA reports
							LogFtpDebugMessage("SFTP[NOAA]: Uploading NOAA report " + filename);

							UploadFile(conn, uploadfile, remotefile, 9999);

							LogFtpDebugMessage("SFTP[NOAA]: Done uploading NOAA report " + filename);
						}
						catch (Exception e)
						{
							LogFtpMessage($"SFTP[NOAA]: Error uploading file {filename} - {e.Message}");
							FtpAlarm.LastMessage = "Error uploading NOAA report file - " + e.Message;
							FtpAlarm.Triggered = true;
						}
					}
				}
				catch (Exception ex)
				{
					LogFtpMessage($"SFTP[NOAA]: Error using SFTP connection - {ex.Message}");
				}
				LogFtpDebugMessage("SFTP[NOAA]: Upload process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || (FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				using (FtpClient conn = new FtpClient())
				{
					if (FtpOptions.Logging)
					{
						conn.Logger = new FtpLogAdapter(FtpLoggerIN);
					}

					LogFtpMessage(""); // insert a blank line
					LogFtpDebugMessage($"FTP[NOAA]: CumulusMX Connecting to " + FtpOptions.Hostname);
					conn.Host = FtpOptions.Hostname;
					conn.Port = FtpOptions.Port;
					conn.Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password);
					conn.Config.LogPassword = false;

					if (!FtpOptions.AutoDetect)
					{

						if (FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							// Explicit = Current protocol - connects using FTP and switches to TLS
							// Implicit = Old depreciated protocol - connects using TLS
							conn.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
							conn.Config.DataConnectionEncryption = true;
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
						LogFtpMessage("FTP[NOAA]: Error connecting ftp - " + ex.Message);

						FtpAlarm.LastMessage = "Error connecting ftp - " + ex.Message;
						FtpAlarm.Triggered = true;

						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"FTP[NOAA]: Base exception - {ex.Message}");
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
						try
						{
							// upload NOAA reports
							LogFtpMessage("");
							LogFtpDebugMessage("FTP[NOAA]: Uploading NOAA report" + filename);

							UploadFile(conn, uploadfile, remotefile, 9999);

							LogFtpDebugMessage($"FTP[NOAA]: Upload of NOAA report {filename} complete");
						}
						catch (Exception e)
						{
							LogFtpMessage($"FTP[NOAA]: Error uploading NOAA file: {e.Message}");
							FtpAlarm.LastMessage = "Error connecting ftp - " + e.Message;
							FtpAlarm.Triggered = true;
						}
					}

					// b3045 - dispose of connection
					conn.Disconnect();
					LogFtpDebugMessage("FTP[NOAA]: Disconnected from " + FtpOptions.Hostname);
				}
				LogFtpMessage("FTP[NOAA]: Process complete");
			}
			else if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogDebugMessage("PHP[NOAA]: Upload process starting");

				var tasklist = new List<Task>();
				var taskCount = 0;
				var runningTaskCount = 0;

				// do we perform a second chance compresssion test?
				if (FtpOptions.PhpCompression == "notchecked")
				{
					TestPhpUploadCompression();
				}

				// upload NOAA report
				try
				{
#if DEBUG
					LogDebugMessage($"PHP[NOAA]: NOAA report waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					await uploadCountLimitSemaphoreSlim.WaitAsync(cancellationToken);
					LogDebugMessage($"PHP[NOAA]: NOAA report has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
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

						LogDebugMessage("PHP[NOAA]: Uploading NOAA report" + filename);

						_ = await UploadFile(phpUploadHttpClient, uploadfile, remotefile, 9999, false, NOAAconf.UseUtf8);

					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"PHP[NOAA]: Error uploading NOAA file " + filename);
						FtpAlarm.LastMessage = $"Error uploading NOAA file - {ex.Message}";
						FtpAlarm.Triggered = true;
					}
					finally
					{
						uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
						LogDebugMessage($"PHP[NOAA]: NOAA report released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
					}

					// no void return which cannot be tracked
					return true;
				}, cancellationToken));

				Interlocked.Increment(ref runningTaskCount);

				// wait for all the files to start
				while (runningTaskCount < taskCount)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						LogDebugMessage($"PHP[NOAA]: Upload process aborted");
						return;
					}
					await Task.Delay(10);
				}
				// wait for all the EOD files to complete
				try
				{
					// wait for all the tasks to complete, or timeout
					if (Task.WaitAll([.. tasklist], TimeSpan.FromSeconds(30)))
					{
						LogDebugMessage($"PHP[NOAA]: Upload process complete, {tasklist.Count} files processed");
					}
					else
					{
						LogErrorMessage("PHP[NOAA]: Upload process complete timed out waiting for tasks to complete");
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "PHP[NOAA]: Error waiting on upload tasks");
					FtpAlarm.LastMessage = "Error waiting on upload tasks";
					FtpAlarm.Triggered = true;
				}

				if (cancellationToken.IsCancellationRequested)
				{
					LogDebugMessage($"PHP[NOAA]: Upload process aborted");
					return;
				}

				LogDebugMessage($"PHP[NOAA]: Upload process complete");
				tasklist.Clear();

				return;
			}
		}



#pragma warning disable S6670 // "Trace.Write" and "Trace.WriteLine" should not be used

		public void LogMessage(string message, MxLogLevel level = MxLogLevel.Info)
		{
			Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);

			if (level >= ErrorListLoggingLevel)
			{
				while (ErrorList.Count >= 50)
				{
					_ = ErrorList.Dequeue();
				}
				ErrorList.Enqueue((DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss - ") + WebUtility.HtmlEncode(message)));
			}

			if (level >= ErrorListLoggingLevel)
			{
				LatestError = message;
				LatestErrorTS = DateTime.Now;
			}
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

#pragma warning restore S6670 // "Trace.Write" and "Trace.WriteLine" should not be used

		public void LogFtpMessage(string message)
		{
			if (!string.IsNullOrEmpty(message))
				LogMessage(message);

			if (FtpOptions.Logging)
			{
				FtpLoggerMX.LogInformation("{Msg}", message);
			}
		}

		public void LogFtpDebugMessage(string message)
		{
			if (FtpOptions.Logging)
			{
				if (!string.IsNullOrEmpty(message))
					LogDebugMessage(message);
				FtpLoggerMX.LogInformation("{Msg}", message);
			}
		}

		public static void LogConsoleMessage(string message, ConsoleColor colour = ConsoleColor.White, bool LogDateTime = false)
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

		public void LogExceptionMessage(Exception ex, string message, bool logError = true)
		{
			LogMessage(message);

			if (ProgramOptions.DebugLogging)
			{
				LogMessage(message + " - " + Utils.ExceptionToString(ex, out _));
			}
			else
			{
				LogMessage(message + " - " + Utils.ExceptionToString(ex));
			}

			if (logError)
			{
				while (ErrorList.Count >= 50)
				{
					_ = ErrorList.Dequeue();
				}
				ErrorList.Enqueue((DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss - ") + message + " - " + ex.GetInnerMostException().Message));
				LatestError = message + " - " + ex.Message;
				LatestErrorTS = DateTime.Now;
			}
		}

		public static string GetErrorLog()
		{
			var arr = ErrorList.ToArray();

			if (arr.Length == 0)
			{
				return "[\"No errors recorded so far\"]";
			}

			return arr.Reverse().ToJson();
		}

		public static string ClearErrorLog()
		{
			LatestError = string.Empty;
			LatestErrorTS = DateTime.MinValue;
			ErrorList.Clear();
			return GetErrorLog();
		}

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
				LogErrorMessage("Error encountered during Realtime file update.");
				LogMessage(ex.Message);
			}
		}

		private string CreateRealtimeFileString(int cycle)
		{
			/*
			Example: 18/10/08 16:03:45 8.4 84 5.8 24.2 33.0 261 0.0 1.0 999.7 W 6 mph C mb mm 146.6 +0.1 85.2 588.4 11.6 20.3 57 3.6 -0.7 10.9 12:00 7.8 14:41 37.4 14:38 44.0 14:28 999.8 16:01 998.4 12:06 1.8.2 448 36.0 10.3 10.5 0 9.3 6.8

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
			60     6.8        weekly rainfall
		  */

			DateTime timestamp = DateTime.Now;

			try
			{
				var InvC = CultureInfo.InvariantCulture;
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
				sb.Append((station.IndoorTemperature ?? 0).ToString(TempFormat, InvC) + ' '); // 23
				sb.Append((station.IndoorHumidity ?? 0).ToString() + ' ');                    // 24
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
				sb.Append((station.UV ?? 0).ToString(UVFormat, InvC) + ' ');                  // 44
				sb.Append(station.ET.ToString(ETFormat, InvC) + ' ');                         // 45
				sb.Append((station.SolarRad ?? 0).ToString() + ' ');                          // 46
				sb.Append(station.AvgBearing.ToString() + ' ');                               // 47
				sb.Append(station.RainLastHour.ToString(RainFormat, InvC) + ' ');             // 48
				sb.Append(station.Forecastnumber.ToString() + ' ');                           // 49
				sb.Append(IsDaylight() ? "1 " : "0 ");                                        // 50
				sb.Append(station.SensorContactLost ? "1 " : "0 ");                           // 51
				sb.Append(station.CompassPoint(station.AvgBearing) + ' ');                    // 52
				sb.Append(station.CloudBase.ToString() + ' ');                                // 53
				sb.Append(CloudBaseInFeet ? "ft " : "m ");                                    // 54
				sb.Append(station.ApparentTemperature.ToString(TempFormat, InvC) + ' ');      // 55
				sb.Append(station.SunshineHours.ToString(SunFormat, InvC) + ' ');             // 56
				sb.Append(Convert.ToInt32(station.CurrentSolarMax).ToString() + ' ');         // 57
				sb.Append(station.IsSunny ? "1 " : "0 ");                                     // 58
				sb.Append(station.FeelsLike.ToString(TempFormat, InvC) + ' ');                // 59
				sb.AppendLine(station.RainWeek.ToString(RainFormat, InvC));                   // 60
				return sb.ToString();
			}
			catch (Exception ex)
			{
				LogErrorMessage($"Realtime[{cycle}]: Error encountered during Realtime file update.");
				LogMessage(ex.Message);
			}
			return string.Empty;
		}


		public void MySqlRealtimeFile(int cycle, bool live, DateTime? logdate = null)
		{
			DateTime timestamp = (DateTime) (live ? DateTime.Now : logdate);

			if (!MySqlSettings.Realtime.Enabled)
				return;

			if (MySqlSettings.RealtimeLimit1Minute && MySqlLastRealtimeTime.Minute == timestamp.Minute)
				return;

			MySqlLastRealtimeTime = timestamp;

			var InvC = CultureInfo.InvariantCulture;

			StringBuilder values = new StringBuilder(RealtimeTable.StartOfInsert, 1024);
			values.Append(" Values('");
			values.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss", InvC) + "',");
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
			values.Append((station.IndoorTemperature.HasValue ? station.IndoorTemperature.Value.ToString(TempFormat, InvC) : "NULL") + ',');
			values.Append((station.IndoorHumidity.HasValue ? station.IndoorHumidity.ToString() : "NULL") + ',');
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
			values.Append((station.UV.HasValue ? station.UV.Value.ToString(UVFormat, InvC) : "NULL") + ',');
			values.Append(station.ET.ToString(ETFormat, InvC) + ',');
			values.Append((station.SolarRad.HasValue ? station.SolarRad.ToString() : "NULL") + ',');
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
			values.Append(station.CurrentSolarMax + ",'");
			values.Append((station.IsSunny ? "1" : "0") + "',");
			values.Append(station.FeelsLike.ToString(TempFormat, InvC) + ',');
			values.Append(station.RainWeek.ToString(RainFormat, InvC));
			values.Append(')');

			string valuesString = values.ToString();
			List<string> cmds = [valuesString];

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

		private void ProcessTemplateFile(string template, string outputfile, bool useAppDir, bool utf8)
		{

			var output = ProcessTemplateFile2String(template, useAppDir);

			if (output != string.Empty)
			{
				var utf8WithoutBom = new UTF8Encoding(false);
				var encoding = utf8 ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");

				try
				{
					using StreamWriter file = new StreamWriter(outputfile, false, encoding);
					file.Write(output);
					file.Close();
				}
				catch (Exception e)
				{
					LogErrorMessage($"ProcessTemplateFile: Error writing to file '{outputfile}', error was - {e}");
				}
			}
		}

		private string ProcessTemplateFile2String(string template, bool useAppDir, bool utf8 = false)
		{
			string templatefile = template;

			if (useAppDir)
			{
				templatefile = AppDir + template;
			}

			if (File.Exists(templatefile))
			{
				var parser = new TokenParser(TokenParserOnToken)
				{
					Encoding = utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("iso-8859-1"),
					SourceFile = templatefile
				};
				return parser.ToString();
			}
			else
			{
				LogWarningMessage($"ProcessTemplateFile: Error, template file not found - {templatefile}");
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
				var parser = new TokenParser(TokenParserOnToken)
				{
					SourceFile = templatefile,
					Encoding = utf8 ? new UTF8Encoding(false) : Encoding.GetEncoding("iso-8859-1")
				};
				return await parser.ToStringAsync();
			}
			else
			{
				LogWarningMessage($"ProcessTemplateFile: Error, template file not found - {templatefile}");
			}
			return string.Empty;
		}


		public void StartTimersAndSensors()
		{
			if (airLinkOut != null || airLinkIn != null || ecowittExtra != null || ambientExtra != null || ecowittCloudExtra != null || stationJsonExtra != null)
			{
				LogMessage("Starting Extra Sensors");
				airLinkOut?.Start();
				airLinkIn?.Start();
				ecowittExtra?.Start();
				ambientExtra?.Start();
				ecowittCloudExtra?.Start();
				stationJsonExtra?.Start();
			}

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

			Wund.CatchUpIfRequired();

			Windy.CatchUpIfRequired();

			PWS.CatchUpIfRequired();

			OpenWeatherMap.CatchUpIfRequired();

			if (Wund.RapidFireEnabled)
			{
				Wund.IntTimer.Interval = 5000; // 5 seconds in rapid-fire mode
				Wund.IntTimer.Enabled = Wund.Enabled;
			}
			else
			{
				Wund.IntTimer.Interval = Wund.Interval * 60 * 1000; // mins to millisecs
			}

			AWEKAS.IntTimer.Interval = AWEKAS.Interval * 1000;
			AWEKAS.IntTimer.Enabled = AWEKAS.Enabled && !AWEKAS.SynchronisedUpdate;

			if (MySqlList.IsEmpty)
			{
				// No archived entries to upload
				LogDebugMessage("MySqlList is Empty");
			}
			else
			{
				// start the archive upload thread
				LogMessage($"Starting MySQL catchup thread. Found {MySqlList.Count} commands to execute");
				_ = MySqlCommandAsync(MySqlList, "MySQL Archive", false);
			}

			WebTimer.Elapsed += WebTimerTick;
			WebTimer.Interval = UpdateInterval * 60 * 1000; // mins to millisecs
			WebTimer.Enabled = WebIntervalEnabled && !SynchronisedWebUpdate;

			EnableOpenWeatherMap();

			NormalRunning = true;
			LogMessage("Normal running");
			LogConsoleMessage("Normal running", ConsoleColor.Green);
		}

		internal async Task BlueskyTimedUpdate(DateTime now)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"BlueskyTimedUpdate: Not all data is ready, aborting process");
				return;
			}

			// Test if an interval update is due
			if (Bluesky.Interval > 0 && (int) now.TimeOfDay.TotalMinutes % Bluesky.Interval == 0)
			{
				LogDebugMessage("BlueskyTimedUpdate: Creating interval post for: " + now.ToString("HH:mm"));

				var parser = new TokenParser(TokenParserOnToken)
				{
					InputText = Bluesky.ContentTemplate
				};

				await Bluesky.DoUpdate(parser.ToStringFromString());
			}


			var roundedTime = new TimeSpan(now.Hour, now.Minute, 0);

			for (var i = 0; i < Bluesky.TimedPostsTime.Length; i++)
			{
				try
				{
					if (Bluesky.TimedPostsTime[i] == TimeSpan.MaxValue)
					{
						continue;
					}

					// is this the time?
					if (Bluesky.TimedPostsTime[i] == roundedTime)
					{
						string content;

						LogDebugMessage("BlueskyTimedUpdate: Creating timed post for: " + now.ToString("HH:mm"));

						if (Bluesky.TimedPostsFile[i] == "web\\Bluesky.txt" || Bluesky.TimedPostsFile[i] == "web/Bluesky.txt")
						{
							content = Bluesky.ContentTemplate;
						}
						else
						{
							content = await File.ReadAllTextAsync(Bluesky.TimedPostsFile[i]);
						}
						var parser = new TokenParser(TokenParserOnToken)
						{
							InputText = content
						};

						await Bluesky.DoUpdate(parser.ToStringFromString());
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"BlueskyTimedUpdate[{i}]: Error");
				}
			}

			for (var i = 0; i < Bluesky.VariablePostsTime.Length; i++)
			{
				try
				{
					var tim = Bluesky.VariablePostsTime[i] switch
					{
						"sunrise" => SunRiseTime,
						"sunset" => SunSetTime,
						"dawn" => Dawn,
						"dusk" => Dusk,
						_ => DateTime.MaxValue
					};


					if (tim == DateTime.MaxValue)
					{
						continue;
					}

					LogDebugMessage("BlueskyTimedUp: Created variable timed post for: " + Bluesky.VariablePostsTime[i]);

					if (tim.Hour == roundedTime.Hours && tim.Minute == roundedTime.Minutes)
					{
						string content;

						if (Bluesky.VariablePostsFile[i] == "web\\Bluesky.txt" || Bluesky.VariablePostsFile[i] == "web/Bluesky.txt")
						{
							content = Bluesky.ContentTemplate;
						}
						else
						{
							content = await File.ReadAllTextAsync(Bluesky.VariablePostsFile[i]);
						}

						var parser = new TokenParser(TokenParserOnToken)
						{
							InputText = content
						};

						await Bluesky.DoUpdate(parser.ToStringFromString());
					}
				}
				catch (FileNotFoundException)
				{
					LogWarningMessage("Bluesky variable timed post failed to find the template file: " + Bluesky.VariablePostsFile[i]);
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"BlueskyVariableUpdate[{i}]: Error");
				}
			}
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
						LogErrorMessage($"CustomSqlSecs[{i}]: Error - " + ex.Message);
					}
				}
				customMySqlSecondsUpdateInProgress = false;
			}
		}


		internal async Task CustomMysqlMinutesUpdate(DateTime now)
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"CustomMysqlMinutesUpdate: Not all data is ready, aborting process");
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
						if (!string.IsNullOrEmpty(MySqlSettings.CustomMins.Commands[i]) && now.Minute % MySqlSettings.CustomMins.Intervals[i] == 0)
						{
							tokenParser.InputText = MySqlSettings.CustomMins.Commands[i];
							await CheckMySQLFailedUploads($"CustomSqlMins[{i}]", tokenParser.ToStringFromString());
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"CustomSqlMins[{i}]: Error - " + ex.Message);
					}
				}
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
						LogErrorMessage($"CustomSqlRollover[{i}]: Error - " + ex.Message);
					}
				}
				customMySqlRolloverUpdateInProgress = false;
			}
		}

		internal async Task CustomMySqlTimedUpdate(DateTime now)
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

				var roundedTime = new TimeSpan(now.Hour, now.Minute, 0);

				for (var i = 0; i < 10; i++)
				{
					try
					{
						if (string.IsNullOrEmpty(MySqlSettings.CustomTimed.Commands[i]))
						{
							continue;
						}

						// is this a one-off, or a repeater
						if (MySqlSettings.CustomTimed.Intervals[i] == 1440)
						{
							if (MySqlSettings.CustomTimed.StartTimes[i] == roundedTime)
							{
								tokenParser.InputText = MySqlSettings.CustomTimed.Commands[i];
								var cmd = tokenParser.ToStringFromString();
								LogDebugMessage("MySQLTimed: Running - " + cmd);
								await CheckMySQLFailedUploads($"CustomSqlTimed[{i}]", cmd);
								continue;
							}
						}
						else // it's a repeater
						{
							if (MySqlSettings.CustomTimed.NextUpdate[i] <= now)
							{
								tokenParser.InputText = MySqlSettings.CustomTimed.Commands[i];
								var cmd = tokenParser.ToStringFromString();
								LogDebugMessage("MySQLTimed: Running repeating - " + cmd);
								await CheckMySQLFailedUploads($"CustomSqlTimed[{i}]", cmd);
								MySqlSettings.CustomTimed.SetNextInterval(i, now);
							}
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

		internal void CustomMySqlStartUp()
		{
			LogMessage("Starting custom start-up MySQL commands");

			var tokenParser = new TokenParser(TokenParserOnToken);

			for (var i = 0; i < 10; i++)
			{
				try
				{
					if (!string.IsNullOrEmpty(MySqlSettings.CustomStartUp.Commands[i]))
					{
						tokenParser.InputText = MySqlSettings.CustomStartUp.Commands[i];
						MySqlCommandSync(tokenParser.ToStringFromString(), "CustomMySqlStartUp");
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"CustomSqlStartUp[{i}]: Error excuting: {MySqlSettings.CustomStartUp.Commands[i]} ");
				}
			}
			LogMessage("Custom start-up MySQL commands end");
		}

		public async Task CheckMySQLFailedUploads(string callingFunction, string cmd)
		{
			await CheckMySQLFailedUploads(callingFunction, [cmd]);
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
				LogErrorMessage($"{callingFunction}: Error - " + ex.Message);
				SqlCatchingUp = false;
			}
		}

		public void DoExtraEndOfDayFiles()
		{
			// handle any extra files that only require EOD processing
			foreach (var item in ActiveExtraFiles)
			{
				if (item.endofday)
				{
					// For EOD files, we want the previous days log files since it is now just past the day roll-over time. Makes a difference on month roll-over
					var logDay = DateTime.Now.AddDays(-1);
					var uploadfile = GetUploadFilename(item.local, logDay);

					if (File.Exists(uploadfile))
					{
						var remotefile = GetRemoteFileName(item.remote, logDay);

						if (item.FTP)
						{
							// FTP the file at the next interval
							EODfilesNeedFTP = true;
						}
						else
						{
							// just copy the file
							LogDebugMessage($"EOD: Copying extra web file {uploadfile} to {remotefile}");
							try
							{
								if (item.process)
								{
									LogDebugMessage($"EOD: Processing extra web file {uploadfile} to {remotefile}");
									var data = ProcessTemplateFile2String(uploadfile, false, item.UTF8);
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
								LogExceptionMessage(ex, $"EOD: Error copying extra web file {uploadfile} to {remotefile}");
							}
						}
					}
					else
					{
						LogWarningMessage($"EOD: Error extra web file {uploadfile} not found");
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

		public void RealtimeFTPLogin()
		{
			LogMessage($"RealtimeFTPLogin: Attempting realtime FTP connect to host {FtpOptions.Hostname} on port {FtpOptions.Port}");

			// dispose of the previous FTP client
			try
			{
				if (RealtimeFTP != null && !RealtimeFTP.IsDisposed)
				{
					RealtimeFTP.Dispose();
				}
			}
			catch
			{
				// do nothing
			}

			RealtimeFTP = new FtpClient
			{
				//Enabled = false,
				Host = FtpOptions.Hostname,
				Port = FtpOptions.Port,
				Credentials = new NetworkCredential(FtpOptions.Username, FtpOptions.Password),
			};

			RealtimeFTP.Config.LogPassword = false;

			SetRealTimeFtpLogging(FtpOptions.Logging);

			if (!FtpOptions.AutoDetect)
			{
				if (FtpOptions.FtpMode == FtpProtocols.FTPS)
				{
					RealtimeFTP.Config.EncryptionMode = FtpOptions.DisableExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
					RealtimeFTP.Config.DataConnectionEncryption = true;
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

			if (FtpOptions.Enabled && FtpOptions.FtpMode != FtpProtocols.SFTP)
			{
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
					LogErrorMessage($"RealtimeFTPLogin: Error connecting ftp - {ex.Message}");
					if (ex.InnerException != null)
					{
						ex = Utils.GetOriginalException(ex);
						LogErrorMessage($"RealtimeFTPLogin: Base exception - {ex.Message}");
					}
					RealtimeFTP.Disconnect();
				}
			}
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
						LogWarningMessage($"RealtimeSSHLogin: Invalid SshftpAuthentication specified [{FtpOptions.SshAuthen}]");
						return;
					}

					RealtimeSSH = new SftpClient(connectionInfo);

					RealtimeSSH.Connect();
					RealtimeSSH.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);  // 15 seconds to match FTP default timeout
					RealtimeSSH.KeepAliveInterval = new TimeSpan(0, 0, 31);         // 31 second keep-alive
					LogMessage("RealtimeSSHLogin: Realtime SFTP connected");
				}
				catch (Exception ex)
				{
					LogErrorMessage($"RealtimeSSHLogin: Error connecting SFTP - {ex.Message}");
				}
			}
		}

		public async Task UpdateWOW(DateTime timestamp)
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
					using var response = await MyHttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (response.StatusCode != HttpStatusCode.OK)
					{
						var msg = $"WOW Response: ERROR - Response code = {response.StatusCode}, body = {responseBodyAsText}";
						LogWarningMessage(msg);
						ThirdPartyAlarm.LastMessage = msg;
						ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						LogDebugMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
						ThirdPartyAlarm.Triggered = false;
					}
				}
				catch (Exception ex)
				{
					var msg = "WOW update: " + ex.Message;
					LogWarningMessage(msg);
					ThirdPartyAlarm.LastMessage = msg;
					ThirdPartyAlarm.Triggered = true;
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
				var myQueue = UseFailedList ? ref MySqlFailedList : ref Cmds;
				SqlCache cachedCmd = null;

				try
				{
					using var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
					mySqlConn.Open();

					using var transaction = Cmds.Count > 2 ? mySqlConn.BeginTransaction() : null;
					do
					{
						// Do not remove the item from the stack until we know the command worked
						if (myQueue.TryPeek(out cachedCmd))
						{
							using MySqlCommand cmd = new MySqlCommand(cachedCmd.statement, mySqlConn);
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
							myQueue.TryDequeue(out cachedCmd);
						}
					} while (!myQueue.IsEmpty);

					if (transaction != null)
					{
						LogDebugMessage($"{CallingFunction}: Committing updates to DB");
						transaction.Commit();
						LogDebugMessage($"{CallingFunction}: Commit complete");
					}

					mySqlConn.Close();

					MySqlUploadAlarm.Triggered = false;
				}
				catch (Exception ex)
				{
					LogErrorMessage($"{CallingFunction}: Error encountered during MySQL operation = {ex.Message}");
					// if debug logging is disable, then log the failing statement anyway
					if (!DebuggingEnabled)
					{
						LogMessage($"{CallingFunction}: SQL = {cachedCmd.statement}");
					}


					MySqlUploadAlarm.LastMessage = ex.Message;
					MySqlUploadAlarm.Triggered = true;

					if (MySqlSettings.BufferOnfailure && !UseFailedList)
					{
						// do we save this command/commands on failure to be resubmitted?
						// if we have a syntax error, it is never going to work so do not save it for retry
						// A selection of the more common(?) errors to ignore...
						var errorCode = (int) ex.Data["Server Error Code"];
						MySqlCommandErrorHandler(CallingFunction, errorCode, myQueue);
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
			var myQueue = UseFailedList ? ref MySqlFailedList : ref Cmds;
			SqlCache cachedCmd = null;

			try
			{
				using var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
				mySqlConn.Open();

				using var transaction = Cmds.Count > 2 ? mySqlConn.BeginTransaction() : null;
				do
				{
					// Do not remove the item from the stack until we know the command worked
					if (myQueue.TryPeek(out cachedCmd))
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
							myQueue.TryDequeue(out cachedCmd);
						}

						MySqlUploadAlarm.Triggered = false;
					}
				} while (!myQueue.IsEmpty);

				if (transaction != null)
				{
					LogDebugMessage($"{CallingFunction}: Committing updates to DB");
					transaction.Commit();
					LogDebugMessage($"{CallingFunction}: Commit complete");
				}

				mySqlConn.Close();
			}
			catch (Exception ex)
			{
				LogErrorMessage($"{CallingFunction}: Error encountered during MySQL operation = {ex.Message}");
				// if debug logging is disable, then log the failing statement anyway
				if (!DebuggingEnabled)
				{
					LogMessage($"{CallingFunction}: SQL = {cachedCmd}");
				}

				MySqlUploadAlarm.LastMessage = ex.Message;
				MySqlUploadAlarm.Triggered = true;

				// do we save this command/commands on failure to be resubmitted?
				// if we have a syntax error, it is never going to work so do not save it for retry
				if (MySqlSettings.BufferOnfailure && !UseFailedList)
				{
					// do we save this command/commands on failure to be resubmitted?
					// if we have a syntax error, it is never going to work so do not save it for retry
					// A selection of the more common(?) errors to ignore...
					var errorCode = (int) ex.Data["Server Error Code"];
					MySqlCommandErrorHandler(CallingFunction, errorCode, myQueue);
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
						LogErrorMessage($"{CallingFunction}: Error buffering command - " + ex.Message);
					}
				}
			}
		}

		public bool MySqlCheckConnection()
		{
			try
			{
				using var mySqlConn = new MySqlConnection(MySqlConnSettings.ToString());
				mySqlConn.Open();
				// get the database name to check 100% we have a connection
				var db = mySqlConn.Database;
				LogMessage("MySqlCheckConnection: Connected to server ok, default database = " + db);
				mySqlConn.Close();
				return true;
			}
			catch
			{
				return false;
			}
		}

		public async Task GetLatestVersion()
		{
			try
			{
#if DEBUG
				bool beta = true;
#else
				bool beta = false;
#endif
				var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/cumulusmx/CumulusMX/releases");
				request.Headers.Add("User-Agent", "CumulusMX");

				using var retval = await MyHttpClient.SendAsync(request);

				var body = await retval.Content.ReadAsStringAsync();
				var releases = body.FromJson<List<GithubRelease>>();

				var latestBeta = releases.Find(x => !x.draft && x.prerelease);
				var latestLive = releases.Find(x => !x.draft && !x.prerelease);
				var cmxBuild = int.Parse(Build);
				int veryLatest;
				if (latestBeta == null)
					veryLatest = int.Parse(latestLive.tag_name[1..]);
				else
					veryLatest = Math.Max(int.Parse(latestBeta.tag_name[1..]), int.Parse(latestLive.tag_name[1..]));

				if (string.IsNullOrEmpty(latestLive.name))
				{
					if (releases.Count == 0)
					{
						LogMessage("Failed to get the latest build version from GitHub");
					}
					return;
				}
				else if (latestBeta != null && string.IsNullOrEmpty(latestBeta.name))
				{
					LogMessage("Failed to get the latest beta build version from GitHub");
					return;
				}

				if (beta)
				{
					if (int.Parse(latestLive.tag_name[1..]) > cmxBuild)
					{
						var msg = $"You are running a beta version of Cumulus MX, and a later release build {latestLive.name} is available.";
						LogConsoleMessage(msg, ConsoleColor.Cyan);
						LogWarningMessage(msg);
						UpgradeAlarm.LastMessage = $"Release build {latestLive.name} is available";
						UpgradeAlarm.Triggered = true;
						LatestBuild = latestLive.tag_name[1..];
					}
					else if (latestBeta != null && int.Parse(latestBeta.tag_name[1..]) == cmxBuild)
					{
						LogMessage($"This Cumulus MX instance is running the latest beta version");
						UpgradeAlarm.Triggered = false;
						LatestBuild = latestLive.tag_name[1..];
					}
					else if (latestBeta != null && int.Parse(latestBeta.tag_name[1..]) > cmxBuild)
					{
						LogMessage($"This Cumulus MX beta instance is not running the latest beta version of Cumulsus MX, build {latestBeta.name} is available.");
						UpgradeAlarm.Triggered = false;
						LatestBuild = latestLive.tag_name[1..];
					}
					else
					{
						LogWarningMessage($"This Cumulus MX instance appears to be running a beta test version. This build={Build}, latest available build={veryLatest}");
						LatestBuild = veryLatest.ToString();
					}
				}
				else // Live release
				{
					if (int.Parse(latestLive.tag_name[1..]) > cmxBuild)
					{
						var msg = $"You are not running the latest version of Cumulus MX, build {latestLive.name} is available.";
						LogConsoleMessage(msg, ConsoleColor.Cyan);
						LogWarningMessage(msg);
						UpgradeAlarm.LastMessage = $"Release build {latestLive.name} is available";
						UpgradeAlarm.Triggered = true;
						LatestBuild = latestLive.tag_name[1..];
					}
					else if (int.Parse(latestLive.tag_name[1..]) == cmxBuild)
					{
						LogMessage($"This Cumulus MX instance is running the latest release version");
						UpgradeAlarm.Triggered = false;
						LatestBuild = latestLive.tag_name[1..];
					}
					else
					{
						LogWarningMessage($"This Cumulus MX instance appears to be running a test version. This build={Build}, latest available build={veryLatest}");
						LatestBuild = veryLatest.ToString();
					}
				}
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Failed to get the latest build version from GitHub");
			}
		}

		public async Task CustomHttpSecondsUpdate()
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
							if (CustomHttpSecondsStrings[i].StartsWith("http", StringComparison.OrdinalIgnoreCase))
							{
								var parser = new TokenParser(TokenParserOnToken)
								{
									InputText = CustomHttpSecondsStrings[i]
								};
								var processedString = parser.ToStringFromString();
								LogDebugMessage($"CustomHttpSeconds[{i}]: Querying - {processedString}");
								using var response = await MyHttpClient.GetAsync(processedString);
								response.EnsureSuccessStatusCode();
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpSeconds[{i}]: Response - {response.StatusCode}");
								LogDataMessage($"CustomHttpSeconds[{i}]: Response Text - {responseBodyAsText}");
							}
							else
							{
								Cumulus.LogConsoleMessage($"CustomHttpSeconds[{i}]: Invalid URL - {CustomHttpSecondsStrings[i]}");
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

		public async Task CustomHttpMinutesUpdate()
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
							if (CustomHttpMinutesStrings[i].StartsWith("http", StringComparison.OrdinalIgnoreCase))
							{
								var parser = new TokenParser(TokenParserOnToken)
								{
									InputText = CustomHttpMinutesStrings[i]
								};
								var processedString = parser.ToStringFromString();
								LogDebugMessage($"CustomHttpMinutes[{i}]: Querying - {processedString}");
								using var response = await MyHttpClient.GetAsync(processedString);
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpMinutes[{i}]: Response code - {response.StatusCode}");
								LogDataMessage($"CustomHttpMinutes[{i}]: Response text - {responseBodyAsText}");
							}
							else
							{
								Cumulus.LogConsoleMessage($"CustomHttpMinutes[{i}]: Invalid URL - {CustomHttpMinutesStrings[i]}");
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

		public async Task CustomHttpRolloverUpdate()
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
							if (!string.IsNullOrEmpty(CustomHttpRolloverStrings[i]))
							{
								var parser = new TokenParser(TokenParserOnToken)
								{
									InputText = CustomHttpRolloverStrings[i]
								};
								var processedString = parser.ToStringFromString();
								LogDebugMessage($"CustomHttpRollover[{i}]: Querying - {processedString}");
								using var response = await MyHttpClient.GetAsync(processedString);
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpRollover[{i}]: Response code - {response.StatusCode}");
								LogDataMessage($"CustomHttpRollover[{i}]: Response text - {responseBodyAsText}");
							}
						}
						else
						{
							Cumulus.LogConsoleMessage($"CustomHttpRollover[{i}]: Invalid URL - {CustomHttpRolloverStrings[i]}");
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

		public static void DegToDMS(decimal degrees, out int d, out int m, out int s)
		{
			int secs = (int) (degrees * 60 * 60);

			s = secs % 60;

			secs = (secs - s) / 60;

			m = secs % 60;
			d = secs / 60;
		}

		public void AddToWebServiceLists(DateTime timestamp)
		{
			Wund.AddToList(timestamp);
			Windy.AddToList(timestamp);
			PWS.AddToList(timestamp);
			//WOW.AddToList(timestamp);
			OpenWeatherMap.AddToList(timestamp);
		}

		private string GetUploadFilename(string input, DateTime dat)
		{
			// we need to subtract the logging interval, otherwise the last log entry will be missed on the month rollover
			var logDate = dat.AddMinutes(-(logints[DataLogInterval] + 1));

			if (input == "<currentlogfile>")
			{
				return GetLogFileName(logDate);
			}
			else if (input == "<currentextralogfile>")
			{
				return GetExtraLogFileName(logDate);
			}
			else if (input == "<airlinklogfile>")
			{
				return GetAirLinkLogFileName(logDate);
			}
			else if (input == "<noaayearfile>")
			{
				NoaaReports noaa = new NoaaReports(this, station);
				return noaa.GetLastNoaaYearReportFilename(dat, true);
			}
			else if (input == "<noaamonthfile>")
			{
				NoaaReports noaa = new NoaaReports(this, station);
				return noaa.GetLastNoaaMonthReportFilename(dat, true);
			}
			else if (input.StartsWith("<custinterval"))
			{
				try
				{
					Match match = regexCustIntvlFileName().Match(input);

					if (match.Success)
					{
						var idx = int.Parse(match.Groups[1].Value) - 1; // we use a zero relative array
																		// we need to subtract the logging interval, otherwise the last log entry will be missed on the month rollover
						var custDate = dat.AddMinutes(-(CustomIntvlLogSettings[idx].Interval + 1));

						return GetCustomIntvlLogFileName(idx, custDate);
					}
					else
					{
						LogWarningMessage("GetUploadFilename: No match found for <custinterval[1-10]> in " + input);
						return input;
					}
				}
				catch (Exception ex)
				{
					LogErrorMessage($"GetUploadFilename: Error processing <custinterval[1-10]>, value='{input}', error: {ex.Message}");
				}
			}

			return input;
		}

		private string GetRemoteFileName(string input, DateTime dat)
		{
			// we need to subtract the logging interval, otherwise the last log entry will be missed on the month rollover
			var logDate = dat.AddMinutes(-(logints[DataLogInterval] + 1));

			if (input.Contains("<currentlogfile>"))
			{
				return input.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(logDate)));
			}
			else if (input.Contains("<currentextralogfile>"))
			{
				return input.Replace("<currentextralogfile>", Path.GetFileName(GetExtraLogFileName(logDate)));
			}
			else if (input.Contains("<airlinklogfile>"))
			{
				return input.Replace("<airlinklogfile>", Path.GetFileName(GetAirLinkLogFileName(logDate)));
			}
			else if (input.Contains("<noaayearfile>"))
			{
				NoaaReports noaa = new NoaaReports(this, station);
				return input.Replace("<noaayearfile>", Path.GetFileName(noaa.GetLastNoaaYearReportFilename(dat, false)));
			}
			else if (input.Contains("<noaamonthfile>"))
			{
				NoaaReports noaa = new NoaaReports(this, station);
				return input.Replace("<noaamonthfile>", Path.GetFileName(noaa.GetLastNoaaMonthReportFilename(dat, false)));
			}
			else if (input.Contains("<custinterval"))
			{
				try
				{
					Match match = regexCustIntvlFileName().Match(input);

					if (match.Success)
					{
						var idx = int.Parse(match.Groups[1].Value) - 1; // we use a zero relative array
																		// we need to subtract the logging interval, otherwise the last log entry will be missed on the month rollover
						var custDate = dat.AddMinutes(-(CustomIntvlLogSettings[idx].Interval + 1));

						return input.Replace(match.Groups[0].Value, Path.GetFileName(GetCustomIntvlLogFileName(idx, custDate)));
					}
					else
					{
						LogWarningMessage("GetRemoteFileName: No match found for <custinterval[1-10]> in " + input);
						return input;
					}
				}
				catch (Exception ex)
				{
					LogErrorMessage($"GetRemoteFileName: Error processing <custinterval[1-10]>, input='{input}', error: {ex.Message}");
				}
			}

			return input;
		}

		public void LogOffsetsMultipliers()
		{
			LogMessage("Offsets:");
			LogMessage($"P={Calib.Press.Offset:F3} Ps={Calib.PressStn.Offset:F3} T={Calib.Temp.Offset:F3} H={Calib.Hum.Offset} WD={Calib.WindDir.Offset} S={Calib.Solar.Offset:F3} UV={Calib.UV.Offset:F3} IT={Calib.InTemp.Offset:F3} IH={Calib.InHum.Offset:F3}");
			LogMessage("Multipliers:");
			LogMessage($"P={Calib.Press.Mult:F3} Ps={Calib.PressStn.Mult:F3} WS={Calib.WindSpeed.Mult:F3} WG={Calib.WindGust.Mult:F3} T={Calib.Temp.Mult:F3} H={Calib.Hum.Mult:F3} R={Calib.Rain.Mult:F3} S={Calib.Solar.Mult:F3} UV={Calib.UV.Mult:F3} IT={Calib.InTemp.Mult:F3} IH={Calib.InHum.Mult:F3}");
			LogMessage("Multipliers2:");
			LogMessage($"P={Calib.Press.Mult2:F3} Ps={Calib.PressStn.Mult2:F3} WS={Calib.WindSpeed.Mult2:F3} WG={Calib.WindGust.Mult2:F3} T={Calib.Temp.Mult2:F3} H={Calib.Hum.Mult2:F3} S={Calib.Solar.Mult2:F3} UV={Calib.UV.Mult2:F3} IT={Calib.InTemp.Mult2:F3} IH={Calib.InHum.Mult2:F3}");
			LogMessage("Spike removal:");
			LogMessage($"TD={Spike.TempDiff:F3} GD={Spike.GustDiff:F3} WD={Spike.WindDiff:F3} HD={Spike.HumidityDiff:F3} PD={Spike.PressDiff:F3} MR={Spike.MaxRainRate:F3} MH={Spike.MaxHourlyRain:F3} ITD={Spike.InTempDiff:F3} IHD={Spike.InHumDiff:F3}");
			LogMessage("Limits:");
			LogMessage($"TH={Limit.TempHigh.ToString(TempFormat)} TL={Limit.TempLow.ToString(TempFormat)} DH={Limit.DewHigh.ToString(TempFormat)} PH={Limit.PressHigh.ToString(PressFormat)} PL={Limit.PressLow.ToString(PressFormat)} GH={Limit.WindHigh:F3}");
		}

		private void LogPrimaryAqSensor()
		{
			switch (StationOptions.PrimaryAqSensor)
			{
				case (int) PrimaryAqSensor.Undefined:
					LogMessage("Primary AQ Sensor = Undefined");
					break;
				case (int) PrimaryAqSensor.Ecowitt1:
				case (int) PrimaryAqSensor.Ecowitt2:
				case (int) PrimaryAqSensor.Ecowitt3:
				case (int) PrimaryAqSensor.Ecowitt4:
					LogMessage("Primary AQ Sensor = Ecowitt" + StationOptions.PrimaryAqSensor);
					break;
				case (int) PrimaryAqSensor.EcowittCO2:
					LogMessage("Primary AQ Sensor = Ecowitt CO2");
					break;
				case (int) PrimaryAqSensor.AirLinkIndoor:
					LogMessage("Primary AQ Sensor = AirLink Indoor");
					break;
				case (int) PrimaryAqSensor.AirLinkOutdoor:
					LogMessage("Primary AQ Sensor = AirLink Outdoor");
					break;
			}
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

		public void SetupFtpLogging(bool enable)
		{
			if (loggerFactory != null)
			{
				loggerFactory.Dispose();
			}

			if (!enable)
			{
				return;
			}

			loggerFactory = new LoggerFactory();
			var fileLoggerOptions = new FileLoggerOptions()
			{
				Append = true,
				FileSizeLimitBytes = 5242880,
				MaxRollingFiles = 3,
				MinLevel = (LogLevel) FtpOptions.LoggingLevel,
				FormatLogEntry = (msg) =>
				{
					var logBuilder = new StringBuilder();
					if (!string.IsNullOrEmpty(msg.Message))
					{
						var loglevel = string.Empty;
						switch (msg.LogLevel)
						{
							case LogLevel.Trace:
								loglevel = "TRCE";
								break;
							case LogLevel.Debug:
								loglevel = "DBUG";
								break;
							case LogLevel.Information:
								loglevel = "INFO";
								break;
							case LogLevel.Warning:
								loglevel = "WARN";
								break;
							case LogLevel.Error:
								loglevel = "FAIL";
								break;
							case LogLevel.Critical:
								loglevel = "CRIT";
								break;
						}
						DateTime timeStamp = DateTime.Now;
						logBuilder.Append(timeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
						logBuilder.Append('\t');
						logBuilder.Append(loglevel);
						logBuilder.Append("\t[");
						logBuilder.Append(msg.LogName);
						logBuilder.Append(']');
						logBuilder.Append('\t');
						logBuilder.Append(msg.Message);
					}
					return logBuilder.ToString();
				}
			};
			var fileLogger = new FileLoggerProvider("MXdiags" + Path.DirectorySeparatorChar + "ftp.log", fileLoggerOptions);
			loggerFactory.AddProvider(fileLogger);
			FtpLoggerRT = loggerFactory.CreateLogger("R-T");
			FtpLoggerIN = loggerFactory.CreateLogger("INT");
			FtpLoggerMX = loggerFactory.CreateLogger("CMX");
		}

		private static int GetStationManufacturer(int type)
		{
			return type switch
			{
				StationTypes.FineOffset or StationTypes.FineOffsetSolar or StationTypes.EasyWeather => EW,
				StationTypes.VantagePro or StationTypes.VantagePro2 or StationTypes.WLL or StationTypes.DavisCloudWll or StationTypes.DavisCloudVP2 => DAVIS,
				StationTypes.WMR928 or StationTypes.WM918 => OREGON,
				StationTypes.WMR200 or StationTypes.WMR100 => OREGONUSB,
				StationTypes.WS2300 => LACROSSE,
				StationTypes.Instromet => INSTROMET,
				StationTypes.GW1000 or StationTypes.HttpEcowitt or StationTypes.EcowittCloud or StationTypes.EcowittHttpApi => ECOWITT,
				StationTypes.Tempest => WEATHERFLOW,
				StationTypes.HttpWund => HTTPSTATION,
				StationTypes.HttpAmbient => AMBIENT,
				StationTypes.Simulator => SIMULATOR,
				StationTypes.JsonStation => JSONSTATION,
				_ => -1,
			};
		}


		[GeneratedRegex(@"max[\s]*=[\s]*([\d]+)")]
		private static partial Regex regexMaxParam();
		[GeneratedRegex(@"[\\/]+\d{8}-\d{6}\.txt")]
		private static partial Regex regexLogFileName();
		[GeneratedRegex(@"[\\/]+FTP-\d{8}-\d{6}\.txt")]
		private static partial Regex regexFtpLogFileName();
		[GeneratedRegex(@"<custinterval([0-9]{1,2})>")]
		private static partial Regex regexCustIntvlFileName();
	}

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
		public const int EcowittCloud = 18;
		public const int DavisCloudWll = 19;
		public const int DavisCloudVP2 = 20;
		public const int JsonStation = 21;
		public const int EcowittHttpApi = 22;
	}


	public class DiaryData
	{
		[PrimaryKey]
		public DateTime Date { get; set; }
		[NotNullAttribute]
		public TimeSpan Time { get; set; }
		public string? Entry { get; set; }
		public decimal? Snow24h { get; set; }
		public decimal? SnowDepth { get; set; }

		public string ToCsvString()
		{
			var txt = new StringBuilder();
			txt.Append(Date.ToString("yyyy-MM-dd"));
			txt.Append(',');
			txt.Append(Time.ToString(@"hh\:mm"));
			txt.Append(',');
			txt.Append(SnowDepth.HasValue ? SnowDepth.Value.ToString("F1") : string.Empty);
			txt.Append(',');
			txt.Append(Snow24h.HasValue ? Snow24h.Value.ToString("F1") : string.Empty);
			txt.Append(",\"");
			txt.Append((Entry ?? string.Empty) + "\"");

			return txt.ToString();
		}

		public bool FromCSVString(string csv)
		{
			var parts = csv.Split(',');

			if (DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime dat))
			{
				if (DateTime.TryParseExact(parts[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime time))
				{
					if (parts.Length < 5)
					{
						return false;
					}

					Date = dat.Date;
					Time = time.TimeOfDay;
					SnowDepth = string.IsNullOrEmpty(parts[2]) ? null : decimal.Parse(parts[2], CultureInfo.InvariantCulture);
					Snow24h = string.IsNullOrEmpty(parts[3]) ? null : decimal.Parse(parts[3], CultureInfo.InvariantCulture);

					string entry;
					if (parts.Length > 5)
					{
						// we split on quoted commas in Entry, recombine them
						entry = string.Join(",", parts[4..]).Trim();
					}
					else
					{
						if (string.IsNullOrEmpty(parts[4]))
						{
							entry = null;
						}
						else
						{
							entry = parts[4].Trim();
						}
					}

					// check if it's quoted
					if (entry != null && entry[0] == '"')
					{
						entry = entry[1..^1];
					}

					Entry = entry;

					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}
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
		public bool EncryptedCreds { get; set; }
		public bool SecureSettings { get; set; }
		public string SettingsUsername { get; set; }
		public string SettingsPassword { get; set; }
	}

	public class CultureConfig
	{
		public bool RemoveSpaceFromDateSeparator { get; set; }
	}

	public class StationUnits
	{
		/// <value> 0=m/s, 1=mph, 2=km/h, 3=knots</value>
		public int Wind { get; set; }
		/// <value> 0=mb, 1=hPa, 2=inHg, 3=kPa</value>
		public int Press { get; set; }
		/// <value> 0=mm, 1=in </value>
		public int Rain { get; set; }
		/// <value> 0=C, 1=F </value>
		public int Temp { get; set; }
		/// <value> 0=cm, 1=inch </value>
		public int SnowDepth { get; set; }
		/// <value> 0=cm, 1=inch </value>
		public int LaserDistance { get; set; }

		public string WindText { get; set; }
		public string PressText { get; set; }
		public string RainText { get; set; }
		public string TempText { get; set; }
		public string SnowText { get; set; }
		public string LaserDistanceText { get; set; }

		public string TempTrendText { get; set; }
		public string RainTrendText { get; set; }
		public string PressTrendText { get; set; }
		public string WindRunText { get; set; }
		public string AirQualityUnitText { get; set; }
		public string[] SoilMoistureUnitText { get; set; } = new string[16];
		public string CO2UnitText { get; set; }
		public string LeafWetnessUnitText { get; set; }

		public StationUnits()
		{
			AirQualityUnitText = "µg/m³";
			Array.Fill(SoilMoistureUnitText, "cb");
			CO2UnitText = "ppm";
			LeafWetnessUnitText = string.Empty;  // Davis is unitless, Ecowitt uses %
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
		public bool CalculateSLP { get; set; }
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
		public bool UseDataLogger { get; set; }
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
		public int LoggingLevel { get; set; }
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
		public string PhpCompression { get; set; } = "notchecked";
		public int MaxConcurrentUploads { get; set; }
		public bool PhpUseGet { get; set; }
		public bool PhpUseBrotli { get; set;}
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
		public bool Incremental { get; set; } = false;
	}

	public class MoonImageOptionsClass
	{
		public bool Enabled { get; set; }
		public int Size { get; set; }
		public bool Transparent { get; set; }
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


	public class JsonStationOptions
	{
		/// <value>0=file, 1=HTTP, 2=MQTT</value>
		public int Connectiontype { get; set; }
		public string SourceFile { get; set; }
		public int FileReadDelay { get; set; }
		public string MqttServer { get; set; }
		public int MqttPort { get; set; }
		public string MqttUsername { get; set; }
		public string MqttPassword { get; set; }
		public bool MqttUseTls { get; set; }
		public string MqttTopic { get; set; }
		public bool ExtraSensorsEnabled { get; set; }
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
		public string Server { get; set; }
		public int Port { get; set; }
		public string ID { get; set; }
		public string PW { get; set; }
		public bool Enabled { get; set; }
		public int Interval { get; set; }
		public int DefaultInterval { get; set; }
		public bool SynchronisedUpdate { get; set; }
		public bool SendUV { get; set; }
		public bool SendSolar { get; set; }
		public bool SendIndoor { get; set; }
		public bool SendAirQuality { get; set; }
		public bool SendSoilTemp { get; set; }
		public int SoilTempSensor { get; set; }
		public bool SendSoilMoisture { get; set; }
		public int SoilMoistureSensor { get; set; }
		public bool CatchUp { get; set; }
		public bool CatchingUp { get; set; }
		public bool Updating { get; set; }
	}


	public class DisplayOptions
	{
		public bool UseApparent { get; set; }
		public bool ShowSolar { get; set; }
		public bool ShowUV { get; set; }
		public bool ShowSnow { get; set; }
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
		public string doNotTriggerOnTags { get; set; }
		public int? interval { get; set; }
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
		public MySqlTableIntervalSettings CustomMins { get; set; }
		public MySqlTableSettings CustomRollover { get; set; }
		public MySqlTableTimedSettings CustomTimed { get; set; }
		public MySqlTableSettings CustomStartUp { get; set; }


		public MySqlGeneralSettings()
		{
			Realtime = new MySqlTableSettings();
			Monthly = new MySqlTableSettings();
			Dayfile = new MySqlTableSettings();
			CustomSecs = new MySqlTableSettings();
			CustomMins = new MySqlTableIntervalSettings();
			CustomRollover = new MySqlTableSettings();
			CustomTimed = new MySqlTableTimedSettings();
			CustomStartUp = new MySqlTableSettings();

			CustomSecs.Commands = new string[10];
			CustomMins.Commands = new string[10];
			CustomMins.IntervalIndexes = new int[10];
			CustomMins.Intervals = new int[10];
			CustomRollover.Commands = new string[10];
			CustomTimed.Commands = new string[10];
			CustomTimed.Intervals = new int[10];
			CustomTimed.StartTimes = new TimeSpan[10];
			CustomTimed.NextUpdate = new DateTime[10];
			CustomStartUp.Commands = new string[10];
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

	public class MySqlTableIntervalSettings : MySqlTableSettings
	{
		public int[] IntervalIndexes { get; set; }
		public int[] Intervals { get; set; }
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

		public void SetNextInterval(int idx, DateTime now)
		{
			// We always revert to the start time so we remain consistent across DST changes
			NextUpdate[idx] = now.Date + StartTimes[idx];

			// Timed and we have now set the start, add on intervals until we reach the future
			while (NextUpdate[idx] <= now)
			{
				NextUpdate[idx] = NextUpdate[idx].AddMinutes(Intervals[idx]);
			}

			// have we rolled over a day and the next interval would be prior to the start time?
			// if so, bump up the next interval to the daily start time
			if (NextUpdate[idx].TimeOfDay < StartTimes[idx])
			{
				NextUpdate[idx] = NextUpdate[idx].Date + StartTimes[idx];
			}
		}
	}

	public class NoaaConfig
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

		public NoaaConfig()
		{
			TempNorms = new double[13];
			RainNorms = new double[13];
		}
	}

	sealed class GithubRelease
	{
		public string tag_name { get; set; }
		public string name { get; set; }
		public bool draft { get; set; }
		public bool prerelease { get; set; }
	}
}
