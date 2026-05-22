using System;
using System.Collections.Generic;
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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using CumulusMX.LogFiles;
using CumulusMX.Settings;
using CumulusMX.Stations;

using EmbedIO;
using EmbedIO.Files;
using EmbedIO.Utilities;
using EmbedIO.WebApi;

using FluentFTP;
using FluentFTP.Helpers;
using FluentFTP.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySqlConnector;

using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;

using Renci.SshNet;

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
			Sensor1 = 1,
			Sensor2 = 2,
			Sensor3 = 3,
			Sensor4 = 4,
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

		public const int DayfileFields = 59;

		public const int LogFileRetries = 3;

		private WeatherStation station;
		public bool HasExtraStation { get; set; } = false;

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
		internal PurpleAir purpleAir;

		internal DateTime LastUpdateTime; // use UTC to avoid DST issues

		internal WebTags WebTags;

		internal Lang Trans = new();

		internal bool NormalRunning = false;

		private Microsoft.Extensions.Logging.ILogger FtpLoggerRT;
		private Microsoft.Extensions.Logging.ILogger FtpLoggerMXRT;
		private Microsoft.Extensions.Logging.ILogger FtpLoggerIN;
		private Microsoft.Extensions.Logging.ILogger FtpLoggerMXIN;

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

		internal int SnowDPlaces = 1;
		internal string SnowFormat = "F1";

		internal string ETFormat;

		internal int LeafWetDPlaces = 0;
		internal string LeafWetFormat = "F0";

		internal int LaserDPlaces = 1;
		internal string LaserFormat = "F1";

		internal string ComportName;
		internal string DefaultComportName;

		internal string dbfile;

		internal string diaryfile;
		internal SQLiteConnection DiaryDB;

		internal int RolloverHour;
		internal bool Use10amInSummer;

		internal decimal Latitude;
		internal decimal Longitude;
		internal double Altitude;

		internal int wsPort;
		internal bool DebuggingEnabled;

		internal SerialPort cmprtRG11;
		internal SerialPort cmprt2RG11;

		private const int DefaultWebUpdateInterval = 15;

		internal int RecordSetTimeoutHrs = 24;

		internal string AlltimeIniFile;
		internal string Alltimelogfile;
		internal string MonthlyAlltimeIniFile;
		internal string MonthlyAlltimeLogFile;
		internal string DayFileName;
		internal string YesterdayFile;
		internal string TodayIniFile;
		internal string MonthIniFile;
		internal string YearIniFile;
		internal string WebTagFile;

		internal bool SynchronisedWebUpdate;

		internal MySqlFunctions MySqlFuncs = new();

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
		internal SensorMaps SensorMaps = new();

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

		// WOW-BE object
		internal ThirdParty.WebUploadWowBe WOW_BE;

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
			public string Username { get; set; }
			public string Password { get; set; }

			public int IpVersion { get; set; }
			public bool UseTLS { get; set; }
			public int ProtocolVersion { get; set; }

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
		internal Alarm ErrorAlarm;

		internal List<AlarmUser> UserAlarms = [];

		private const double DEFAULTFCLOWPRESS = 950.0;
		private const double DEFAULTFCHIGHPRESS = 1050.0;

		private const string ForumDefault = "https://cumulus.hosiene.co.uk/";

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
		private int phpMaxConnectionsPerServer;

		internal Thread ftpThread;

		internal string xapHeartbeat;
		internal string xapsource;

		internal string LatestBuild = "n/a";

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

		internal string[] DayfileFieldNames = ["Date", "HighWindGust", "HighGustBearing", "HighGustTime", "MinTemperature", "MinTempTime", "MaxTemperature", "MaxTempTime", "MinPressure", "MinPressureTime", "MaxPressure", "MaxPressureTime", "MaxRainfallRate", "MaxRainRateTime", "TotalRainfallToday", "AvgTemperatureToday", "TotalWindRun", "HighAverageWindSpeed", "HighAvgWindSpeedTime", "LowHumidity", "LowHumidityTime", "HighHumidity", "HighHumidityTime", "TotalEvapotranspiration", "TotalHoursOfSunshine", "HighHeatIndex", "HighHeatIndexTime", "HighApparentTemperature", "HighAppTempTime", "LowApparentTemperature", "LowAppTempTime", "High1hRain", "High1hRainTime", "LowWindChill", "LowWindChillTime", "HighDewPoint", "HighDewPointTime", "LowDewPoint", "LowDewPointTime", "DominantWindBearing", "HeatingDegreeDays", "CoolingDegreeDays", "HighSolarRad", "HighSolarRadTime", "HighUv-I", "HighUv-ITime", "HighFeelsLike", "HighFeelsLikeTime", "LowFeelsLike", "LowFeelsLikeTime", "HighHumidex", "HighHumidexTime", "ChillHours", "High24hRain", "High24hRainTime", "HighBgt", "HighBgtTime", "HighWbgt", "HighWbgtTime"];
		internal string[] LogFileFieldNames = ["DateTime", "TS", "Temperature", "Humidity", "DewPoint", "WindSpeed", "RecentHighGust", "AverageWindBearing", "RainfallRate", "RainfallSoFar", "SeaLevelPressure", "RainfallCounter", "InsideTemperature", "InsideHumidity", "CurrentGust", "WindChill", "HeatIndex", "UvIndex", "SolarRadiation", "Evapotranspiration", "AnnualEvapotranspiration", "ApparentTemperature", "MaxSolarRadiation", "HoursOfSunshine", "WindBearing", "Rg-11Rain", "RainSinceMidnight", "FeelsLike", "Humidex", "BGT", "WBGT"];
		internal string[] ExtraFileFieldNames = ["DateTime", "TS",
			"Temp1", "Temp2", "Temp3", "Temp4", "Temp5", "Temp6", "Temp7", "Temp8", "Temp9", "Temp10", "Hum1", "Hum2", "Hum3", "Hum4", "Hum5", "Hum6", "Hum7", "Hum8", "Hum9", "Hum10",
			"Dewpoint1", "Dewpoint2", "Dewpoint3", "Dewpoint4", "Dewpoint5", "Dewpoint6", "Dewpoint7", "Dewpoint8", "Dewpoint9", "Dewpoint10",
			"SoilTemp1", "SoilTemp2", "SoilTemp3", "SoilTemp4", "SoilMoist1", "SoilMoist2", "SoilMoist3", "SoilMoist4", "na1", "na2", "LeafWet1", "LeafWet2",
			"SoilTemp5", "SoilTemp6", "SoilTemp7", "SoilTemp8", "SoilTemp9", "SoilTemp10", "SoilTemp11", "SoilTemp12", "SoilTemp13", "SoilTemp14", "SoilTemp15", "SoilTemp16",
			"SoilMoist5", "SoilMoist6", "SoilMoist7", "SoilMoist8", "SoilMoist9", "SoilMoist10", "SoilMoist11", "SoilMoist12", "SoilMoist13", "SoilMoist14", "SoilMoist15", "SoilMoist16",
			"AQ1Pm", "AQ2Pm", "AQ3Pm", "AQ4Pm", "AQ1PmAvg", "AQ2PmAvg", "AQ3PmAvg", "AQ4PmAvg", "UserTemp1", "UserTemp2", "UserTemp3", "UserTemp4", "UserTemp5", "UserTemp6", "UserTemp7", "UserTemp8",
			"CO2", "CO2Avg", "CO2Pm25", "CO2Pm25Avg", "CO2Pm10", "CO2Pm10Avg", "CO2Temp", "CO2Hum", "LaserDist1", "LaserDist2", "LaserDist3", "LaserDist4", "Snow24h", "Temp11", "Temp12", "Temp13", "Temp14", "Temp15", "Temp16",
			"Hum11", "Hum12", "Hum13", "Hum14", "Hum15", "Hum16", "Dewpoint11", "Dewpoint12", "Dewpoint13", "Dewpoint14", "Dewpoint15", "Dewpoint16", "AQ1Pm10", "AQ2Pm10", "AQ3Pm10", "AQ4Pm10",
			"SoilEC1", "SoilEC2", "SoilEC3", "SoilEC4", "SoilEC5", "SoilEC6", "SoilEC7", "SoilEC8", "SoilEC9", "SoilEC10", "SoilEC11", "SoilEC12", "SoilEC13", "SoilEC14", "SoilEC15", "SoilEC16"
		];

		private static readonly Queue<string> queue = new(50);
		public static Queue<string> ErrorList
		{
			get => queue;
		}

		private SemaphoreSlim uploadCountLimitSemaphoreSlim;
		private readonly SemaphoreSlim realtimeFtpSemaphore = new(1, 1);
		private readonly SemaphoreSlim realtimeCopySemaphore = new(1, 1);
		private readonly SemaphoreSlim intervaltimeFtpSemaphore = new(1, 1);

		private SftpClientFactory sftpClientFactory;
		private FtpClientFactory ftpClientFactory;

		public Cumulus()
		{
		}

		public void Initialise(int HTTPport, bool DebugEnabled)
		{
			var fullVer = Assembly.GetExecutingAssembly().GetName().Version;
			Version = $"{fullVer.Major}.{fullVer.Minor}.{fullVer.Build}";
			Build = Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString();

			WebTagFile = Path.Combine(Directory.GetCurrentDirectory(), "WebTags.txt");

			//b3045>, use same port for WS...  WS port = HTTPS port
			wsPort = HTTPport;

			DebuggingEnabled = DebugEnabled;

			LogMessage("========================== Cumulus MX starting ==========================");

			LogMessage("Cumulus MX v." + Version + " build " + Build);
			LogMessage("Working Folder : " + Directory.GetCurrentDirectory());
			LogConsoleMessage("Cumulus MX v." + Version + " build " + Build);
			LogConsoleMessage("Working Dir: " + Directory.GetCurrentDirectory());

			IsOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

			string Platform;
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
			LogMessage("Dotnet Version: " + RuntimeInformation.FrameworkDescription);
			LogMessage("Running userid: " + Environment.UserName);

			LogMessage($"Running as a {(IntPtr.Size == 4 ? "32" : "64")} bit process");


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

			// Remove old MD5 hash files
			CleanUpHashFiles();

			var WebPath = "web";

			// initialise the third party uploads
			Wund = new ThirdParty.WebUploadWund(this, "WUnderground");
			Windy = new ThirdParty.WebUploadWindy(this, "Windy");
			WindGuru = new ThirdParty.WebUploadWindGuru(this, "WindGuru");
			PWS = new ThirdParty.WebUploadPws(this, "PWS");
			WOW = new ThirdParty.WebUploadWow(this, "WOW");
			WOW_BE = new ThirdParty.WebUploadWowBe(this, "WOW-BE");
			APRS = new ThirdParty.WebUploadAprs(this, "APRS");
			AWEKAS = new ThirdParty.WebUploadAwekas(this, "AWEKAS");
			WCloud = new ThirdParty.WebUploadWCloud(this, "WCloud");
			OpenWeatherMap = new ThirdParty.WebUploadOwm(this, "OpenWeatherMap");
			Bluesky = new ThirdParty.WebUploadBlueSky(this, "BlueSky")
			{
				DefaultInterval = 60
			};

			var blueskyFile = Path.Combine(WebPath, "Bluesky.txt");
			if (File.Exists(blueskyFile))
			{
				try
				{
					Bluesky.ContentTemplate = File.ReadAllText(blueskyFile, new System.Text.UTF8Encoding(false));
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
					TemplateFileName = Path.Combine(WebPath, "websitedataT.json"),
					LocalPath = WebPath,
					FileName = "websitedata.json"
				},
				new()
				{
					LocalPath = "",
					FileName = "wxnow.txt"
				}
			];

			RealtimeFiles =
			[
				new()
				{
					FileName = "realtime.txt"
				},
				new()
				{
					TemplateFileName = Path.Combine(WebPath, "realtimegaugesT.txt"),
					LocalPath = WebPath,
					FileName = "realtimegauges.txt"
				}
			];

			GraphDataFiles =
			[
				new()       // 0
				{
					LocalPath = WebPath,
					FileName = "graphconfig.json"
				},
				new()       // 1
				{
					LocalPath = WebPath,
					FileName = "availabledata.json"
				},
				new()       // 2
				{
					LocalPath = WebPath,
					FileName = "tempdata.json"
				},
				new()       // 3
				{
					LocalPath = WebPath,
					FileName = "pressdata.json"
				},
				new()       // 4
				{
					LocalPath = WebPath,
					FileName = "winddata.json"
				},
				new()       // 5
				{
					LocalPath = WebPath,
					FileName = "wdirdata.json"
				},
				new()       // 6
				{
					LocalPath = WebPath,
					FileName = "humdata.json"
				},
				new()       // 7
				{
					LocalPath = WebPath,
					FileName = "raindata.json"
				},
				new()       // 8
				{
					LocalPath = WebPath,
					FileName = "dailyrain.json"
				},
				new()       // 9
				{
					LocalPath = WebPath,
					FileName = "dailytemp.json"
				},
				new()       // 10
				{
					LocalPath = WebPath,
					FileName = "solardata.json"
				},
				new()       // 11
				{
					LocalPath = WebPath,
					FileName = "sunhours.json"
				},
				new()       // 12
				{
					LocalPath = WebPath,
					FileName = "airquality.json"
				},
				new()       // 13
				{
					LocalPath = WebPath,
					FileName = "extratempdata.json"
				},
				new()       // 14
				{
					LocalPath = WebPath,
					FileName = "extrahumdata.json"
				},
				new()       // 15
				{
					LocalPath = WebPath,
					FileName = "extradewdata.json"
				},
				new()       // 16
				{
					LocalPath = WebPath,
					FileName = "soiltempdata.json"
				},
				new()       // 17
				{
					LocalPath = WebPath,
					FileName = "soilmoistdata.json"
				},
				new()       // 18
				{
					LocalPath = WebPath,
					FileName = "usertempdata.json"
				},
				new()       // 19
				{
					LocalPath = WebPath,
					FileName = "co2sensordata.json"
				},
				new()     // 20
				{
					LocalPath = WebPath,
					FileName = "leafwetdata.json"
				},
				new()     // 21
				{
					LocalPath = WebPath,
					FileName = "laserdepthdata.json"
				},
				new()     // 22
				{
					LocalPath = WebPath,
					FileName = "snow24hdata.json"
				},
				new()     // 23
				{
					LocalPath = WebPath,
					FileName = "soilecdata.json"
				},
			];

			GraphDataEodFiles =
			[
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailytempdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailypressdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailywinddata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailyhumdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailyraindata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailysolardata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailydegdaydata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alltempsumdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "allchillhrsdata.json"
				},
				new()
				{
					LocalPath = WebPath,
					FileName = "alldailysnowdata.json"
				}
			];

			ProgramOptions.Culture = new CultureConfig();

			for (var i = 0; i < 10; i++)
			{
				CustomIntvlLogSettings[i] = new CustomLogSettings();
				CustomDailyLogSettings[i] = new CustomLogSettings();
			}

			// initialise the alarms
			DataStoppedAlarm = new Alarm(AlarmIds.DataStopped, AlarmTypes.Trigger, this);
			BatteryLowAlarm = new Alarm(AlarmIds.BatteryLow, AlarmTypes.Trigger, this);
			SensorAlarm = new Alarm(AlarmIds.Sensor, AlarmTypes.Trigger, this);
			SpikeAlarm = new Alarm(AlarmIds.Spike, AlarmTypes.Trigger, this);
			HighWindAlarm = new Alarm(AlarmIds.WindHigh, AlarmTypes.Above, this, Units.WindText);
			HighGustAlarm = new Alarm(AlarmIds.WindGust, AlarmTypes.Above, this, Units.WindText);
			HighRainRateAlarm = new Alarm(AlarmIds.RainRate, AlarmTypes.Above, this, Units.RainTrendText);
			HighRainTodayAlarm = new Alarm(AlarmIds.Rainfall, AlarmTypes.Above, this, Units.RainText);
			PressChangeAlarm = new AlarmChange(AlarmIds.PressUp, AlarmIds.PressDown, this, Units.PressTrendText);
			HighPressAlarm = new Alarm(AlarmIds.PressHigh, AlarmTypes.Above, this, Units.PressText);
			LowPressAlarm = new Alarm(AlarmIds.PressLow, AlarmTypes.Below, this, Units.PressText);
			TempChangeAlarm = new AlarmChange(AlarmIds.TempUp, AlarmIds.TempDown, this, Units.TempTrendText);
			HighTempAlarm = new Alarm(AlarmIds.TempHigh, AlarmTypes.Above, this, Units.TempText);
			LowTempAlarm = new Alarm(AlarmIds.TempLow, AlarmTypes.Below, this, Units.TempText);
			UpgradeAlarm = new Alarm(AlarmIds.Upgrade, AlarmTypes.Trigger, this);
			FirmwareAlarm = new Alarm(AlarmIds.Firmware, AlarmTypes.Trigger, this);
			ThirdPartyAlarm = new Alarm(AlarmIds.Thirdparty, AlarmTypes.Trigger, this);
			MySqlUploadAlarm = new Alarm(AlarmIds.MySQL, AlarmTypes.Trigger, this);
			IsRainingAlarm = new Alarm(AlarmIds.IsRaining, AlarmTypes.Trigger, this);
			NewRecordAlarm = new Alarm(AlarmIds.Record, AlarmTypes.Trigger, this);
			FtpAlarm = new Alarm(AlarmIds.FTP, AlarmTypes.Trigger, this);
			ErrorAlarm = new Alarm(AlarmIds.Error, AlarmTypes.Trigger, this);

			ReadIniFile();
			ReadConfigFile();

			if (ProgramOptions.ProcessLogFilesLevel == 0)
			{
				LogConsoleMessage("Converting log files to new format (this could take some time)...");
				LogMessage("Converting log files to new format (this could take some time)...");
				LogFileConverter.AddUnixTimestamp.ProcessLogFiles(ProgramOptions.DataPath, RecordsBeganDateTime);
				ProgramOptions.ProcessLogFilesLevel = 1;
				WriteIniFile();
				LogConsoleMessage("Log file conversion complete");
				LogMessage("Log file conversion complete");
			}

			for (var i = 2; i < GraphDataFiles.Length; i++)
			{
				GraphDataFiles[i].LastDataTime = RecordsBeganDateTime;
			}

			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				GraphDataEodFiles[i].LastDataTime = RecordsBeganDateTime;
			}

			// Check if all the folders required by CMX exist, if not create them
			CreateRequiredFolders();

			dbfile = Path.Combine(ProgramOptions.DataPath, "cumulusmx.db");
			diaryfile = Path.Combine(ProgramOptions.DataPath, "diary.db");

			AlltimeIniFile = Path.Combine(ProgramOptions.DataPath, "alltime.ini");
			Alltimelogfile = Path.Combine(ProgramOptions.DataPath, "alltimelog.txt");
			MonthlyAlltimeIniFile = Path.Combine(ProgramOptions.DataPath, "monthlyalltime.ini");
			MonthlyAlltimeLogFile = Path.Combine(ProgramOptions.DataPath, "monthlyalltimelog.txt");
			DayFileName = Path.Combine(ProgramOptions.DataPath, "dayfile.txt");
			YesterdayFile = Path.Combine(ProgramOptions.DataPath, "yesterday.ini");
			TodayIniFile = Path.Combine(ProgramOptions.DataPath, "today.ini");
			MonthIniFile = Path.Combine(ProgramOptions.DataPath, "month.ini");
			YearIniFile = Path.Combine(ProgramOptions.DataPath, "year.ini");


			// Do we prevent more than one copy of CumulusMX running?
			CheckForSingleInstance(System.OperatingSystem.IsWindows());

			if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogMessage("Maximum concurrent PHP Uploads = " + FtpOptions.MaxConcurrentUploads);
				LogMessage("PHP using GET = " + FtpOptions.PhpUseGet);
				LogMessage("PHP using Brotli = " + FtpOptions.PhpUseBrotli);
			}
			uploadCountLimitSemaphoreSlim = new SemaphoreSlim(FtpOptions.MaxConcurrentUploads, FtpOptions.MaxConcurrentUploads);

			LogMessage($"Directory separator=[{Path.DirectorySeparatorChar}] Decimal separator=[{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}] List separator=[{CultureInfo.CurrentCulture.TextInfo.ListSeparator}]");
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

			// Check uptime
			var ts = Environment.TickCount64;
			var uptime = TimeSpan.FromMilliseconds(ts);

			LogMessage($"System uptime = {ts/1000:F0} secs ({(int) uptime.TotalHours}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s)");

			// Do we delay the start of Cumulus MX for a fixed period?
			if (ProgramOptions.StartupDelaySecs > 0)
			{
				// Only delay if the delay uptime is undefined (0), or the current uptime is less than the user specified max uptime to apply the delay
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
				var escapeTime = DateTime.UtcNow.AddMinutes(ProgramOptions.StartupPingEscapeTime);
				var attempt = 1;
				var pingSuccess = false;
				// This is the timeout for "hung" attempts, we will double this at every failure so we do not create too many hung resources
				var pingTimeoutSecs = 10;
				var pingTimeoutDT = DateTime.UtcNow.AddSeconds(pingTimeoutSecs);
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
					} while (pingTask.Status == TaskStatus.Running && DateTime.UtcNow < pingTimeoutDT);

					LogDebugMessage($"PING #{attempt} task status: {pingTask.Status}");

					// did we timeout waiting for the task to end?
					if (DateTime.UtcNow >= pingTimeoutDT)
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
				} while (!pingSuccess && DateTime.UtcNow < escapeTime);

				if (DateTime.UtcNow >= escapeTime)
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
					LogWarningMessage($"Warning: Start-up task: '{ProgramOptions.StartupTask}' does not exist");
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

			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				CreateUpdateSftpClientFactory();
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS)
			{
				CreateUpdateFtpClientFactory();
			}


			LogMessage("Data path = " + ProgramOptions.DataPath);

			AppDomain.CurrentDomain.SetData("DataDirectory", ProgramOptions.DataPath);

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
					// run the create again to add any new columns
					DiaryDB.CreateTable<DiaryData>();

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
			LogMessage("Extra sensor logging: " + (StationOptions.LogExtraSensors ? "enabled" : "disabled"));
			LogMessage("NoSensorCheck = " + (StationOptions.NoSensorCheck ? "enabled" : "disabled"));

			LogMessage($"Uploads = {(FtpOptions.Enabled ? "enabled" : "disabled")} via '{(
				FtpOptions.FtpMode switch
				{
					FtpProtocols.FTP => "FTP",
					FtpProtocols.FTPS => "FTPS",
					FtpProtocols.SFTP => "SFTP",
					FtpProtocols.PHP => "PHP",
					_ => FtpOptions.FtpMode.ToString()
				})}', to folder '{FtpOptions.Directory}'");
			LogMessage($"Upload Copy = {(FtpOptions.LocalCopyEnabled ? "enabled" : "disabled")} to folder '{FtpOptions.LocalCopyFolder}'");

			LogMessage("Real time interval: " + (RealtimeIntervalEnabled ? "enabled" : "disabled") + ", uploads: " + (FtpOptions.RealtimeEnabled ? "enabled" : "disabled") + ", (" + RealtimeInterval / 1000 + " secs)");
			LogMessage("Interval          : " + (WebIntervalEnabled ? "enabled" : "disabled") + ", uploads: " + (FtpOptions.IntervalEnabled ? "enabled" : "disabled") + ", (" + UpdateInterval + " mins)");

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
			LaserFormat = "F" + Units.LaserDistance switch
			{
				0 => 1,
				1 => 2,
				2 => 0,
				_ => 2
			};

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

			LogMessage($"WindUnit={Units.WindText} RainUnit={Units.RainText} TempUnit={Units.TempText} PressureUnit={Units.PressText} LaserUnit={Units.LaserDistanceText} SnowUnit={Units.SnowText}");
			LogMessage($"Manual rainfall: YTDRain={YTDrain:F3}, Correction Year={YTDrainyear}");
			LogMessage($"RainDayThreshold={RainDayThreshold:F3}");
			LogMessage($"Roll over hour={RolloverHour:D2}");
			if (RolloverHour != 0)
			{
				LogMessage("Use 10am in summer=" + Use10amInSummer);
				LogMessage($"Current Roll over hour={GetRolloverHour(DateTime.Now):D2}");
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

			//var cert = new X509Certificate2(new X509Certificate("c:\\temp\\CumulusMX.pfx", "password", X509KeyStorageFlags.UserKeySet))

			LogMessage("Starting web server on port " + HTTPport);
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
				.WithStaticFolder("/css/", Path.Combine(htmlRootPath, "css"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/fonts/", Path.Combine(htmlRootPath, "fonts"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/img/", Path.Combine(htmlRootPath, "img"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/lib/", Path.Combine(htmlRootPath, "lib"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/sounds/", Path.Combine(htmlRootPath, "sounds"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/js/images/", Path.Combine(htmlRootPath, "js", "images"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/ai2/css/", Path.Combine(htmlRootPath, "ai2", "css"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/ai2/img/", Path.Combine(htmlRootPath, "ai2", "img"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/ai2/lib/", Path.Combine(htmlRootPath, "ai2", "lib"), true, m => m
					.WithoutContentCaching()
				)
				.WithStaticFolder("/custom/", Path.Combine(htmlRootPath, "custom"), true, m => m
					.WithoutContentCaching()
				)
				.WithWebApi("/", m => m
					.WithController<Api.DashboardController>()
					.WithController<Api.ScriptController>()
					.WithController<Api.JsonController>()
					.WithController<Api.Ai2DashboardController>()
					.WithController<Api.Ai2ScriptController>()
				);

			Swan.Logging.DebugLogger _Logger = Swan.Logging.DebugLogger.Instance;

			//httpServer.Listener.AddPrefix($"https://*:{HTTPport + 1000}/")

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
			Api.sensorMaps = new SensorMappings(this);

			_ = httpServer.RunAsync(Program.ExitSystemToken);

			Console.WriteLine();
			Console.Write("Cumulus running at: ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"http://localhost:{HTTPport}/");
			Console.ResetColor();

			// get the local v4 IP addresses
			try
			{
				var ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
				foreach (var ip in ips)
				{
					if (ip.AddressFamily == AddressFamily.InterNetwork)
					{
						LogConsoleMessage($"                    http://{ip}:{HTTPport}/", ConsoleColor.Yellow);
						LogMessage($"Cumulus running at: http://{ip}:{HTTPport}/");
					}
				}
			}
			catch
			{
				LogConsoleMessage("Unable to get local IP address", ConsoleColor.Red);
				LogMessage("DNS Error: Unable to get local IP address");
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

			Manufacturer = GetStationManufacturerFromType(StationType);
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
				WebTags = new WebTags(this, station);
				WebTags.InitialiseWebtags();

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
					HasExtraStation = true;
				}
				if (AmbientExtraEnabled)
				{
					LogMessage("Creating Ambient extra sensors station");
					LogConsoleMessage($"Opening Ambient extra sensors");
					ambientExtra = new HttpStationAmbient(this, station);
					Api.stationAmbientExtra = ambientExtra;
					HasExtraStation = true;
				}
				if (EcowittCloudExtraEnabled)
				{
					LogMessage("Creating Ecowitt cloud extra sensors station");
					LogConsoleMessage($"Opening Ecowitt cloud extra sensors");
					ecowittCloudExtra = new EcowittCloudStation(this, station);
					HasExtraStation = true;
				}
				if (JsonExtraStationOptions.ExtraSensorsEnabled)
				{
					LogMessage("Creating JSON station extra sensors station");
					LogConsoleMessage($"Opening JSON extra sensors");
					stationJsonExtra = new JsonStation(this, station);
					Api.stationJsonExtra = stationJsonExtra;
					HasExtraStation = true;
				}
				if (PurpleAirEnabled)
				{
					LogMessage("Creating PurpleAir station");
					LogConsoleMessage($"Opening PurpleAir");
					purpleAir = new PurpleAir(this, station);
				}


				// set the third party upload station
				Wund.station = station;
				Windy.station = station;
				WindGuru.station = station;
				PWS.station = station;
				WOW.station = station;
				WOW_BE.station = station;
				APRS.station = station;
				AWEKAS.station = station;
				WCloud.station = station;
				OpenWeatherMap.station = station;
				Bluesky.station = station;
				Bluesky.CancelToken = Program.ExitSystemToken;

				httpFiles = new HttpFiles(this, station);

				Api.httpFiles = httpFiles;

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
				if (MySqlFuncs.MySqlSettings.CustomStartUp.Enabled)
				{
					CustomMySqlStartUp();
				}

				if (station.timerStartNeeded)
				{
					StartTimersAndSensors();
				}

				if ((StationType == StationTypes.WMR100) || (StationType == StationTypes.EasyWeather) || (Manufacturer == StationManufacturer.OREGON) || StationType == StationTypes.Simulator)
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
				MaxConnectionsPerServer = phpMaxConnectionsPerServer,
				AllowAutoRedirect = false
			};

			phpUploadHttpClient = new HttpClient(phpUploadSocketHandler)
			{
				// 15 second timeout
				Timeout = TimeSpan.FromSeconds(15)
			};

			var header = new System.Net.Http.Headers.ProductHeaderValue("CumulusMX", $"{Version}.{Build}");
			var userAgent = new System.Net.Http.Headers.ProductInfoHeaderValue(header);
			phpUploadHttpClient.DefaultRequestHeaders.UserAgent.Add(userAgent);

			var uri = new Uri(FtpOptions.PhpUrl);
			phpUploadHttpClient.DefaultRequestHeaders.Referrer = new Uri(uri.Scheme + "://" + uri.Host);
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

			var header = new System.Net.Http.Headers.ProductHeaderValue("CumulusMX", $"{Version}.{Build}");
			var userAgent = new System.Net.Http.Headers.ProductInfoHeaderValue(header);

			MyHttpClient.DefaultRequestHeaders.UserAgent.Add(userAgent);
			MyHttpClient.DefaultRequestHeaders.Referrer = new Uri("https://cumulus.hosiene.co.uk/");
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
			RealtimeTable = new MySqlTable(MySqlFuncs.MySqlSettings.Realtime.TableName);
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
			MonthlyTable = new MySqlTable(MySqlFuncs.MySqlSettings.Monthly.TableName);
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
			MonthlyTable.AddColumn("SunshineHrs", "decimal(3," + SunshineDPlaces + ")");
			MonthlyTable.AddColumn("CurrWindBearing", "varchar(3)");
			MonthlyTable.AddColumn("RG11rain", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("RainSinceMidnight", "decimal(4," + RainDPlaces + ")");
			MonthlyTable.AddColumn("WindbearingSym", "varchar(3)");
			MonthlyTable.AddColumn("CurrWindBearingSym", "varchar(3)");
			MonthlyTable.AddColumn("FeelsLike", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("Humidex", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("BlackGlobeTemp", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.AddColumn("WetBulbGlobeTemp", "decimal(4," + TempDPlaces + ")");
			MonthlyTable.PrimaryKey = "LogDateTime";
			MonthlyTable.Comment = "\"Monthly logs from Cumulus\"";
		}

		internal void SetupDayfileMySqlTable()
		{
			DayfileTable = new MySqlTable(MySqlFuncs.MySqlSettings.Dayfile.TableName);
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
			DayfileTable.AddColumn("HighBgt", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("THighBgt", "varchar(5)");
			DayfileTable.AddColumn("HighWbgt", "decimal(5," + TempDPlaces + ")");
			DayfileTable.AddColumn("THighWbgt", "varchar(5)");

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

			Units.LaserDistanceText = Units.LaserDistance switch
			{
				0 => "cm",
				1 => "in",
				2 => "mm",
				_ => "??"
			};

			LaserDPlaces = Units.LaserDistance switch
			{
				0 => 1,
				1 => 2,
				2 => 0,
				_ => 0
			};

			LaserFormat = "F" + LaserDPlaces;
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
				if (FtpLoggerRT == null || FtpLoggerIN == null || FtpLoggerMXRT == null || FtpLoggerMXIN == null)
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
						MetData.RG11RainToday += RG11tipsize;
					}
				}
				else
				{
					MetData.IsRaining = isOn;
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
						MetData.RG11RainToday += RG11tipsize;
					}
				}
				else
				{
					MetData.IsRaining = isOn;
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
					var respJson = JsonSerializer.Deserialize<OpenWeatherMapStation[]>(responseBodyAsText);
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
				var datestr = DateTime.UtcNow.ToString("yyMMddHHmm");
				StringBuilder sb = new StringBuilder($"{{\"external_id\":\"CMX-{datestr}\",");
				sb.Append($"\"name\":\"{LocationName}\",");
				sb.Append($"\"latitude\":{Latitude.ToString(invC)},");
				sb.Append($"\"longitude\":{Longitude.ToString(invC)},");
				sb.Append($"\"altitude\":{(int) ConvertUnits.AltitudeM(Altitude)}}}");

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
					var respJson = JsonSerializer.Deserialize<OpenWeatherMapNewStation>(responseBodyAsText);

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
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			var cycle = RealtimeCycleCounter++;

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
				if (!realtimeCopySemaphore.Wait(1000, Program.ExitSystemToken))
				{
					LogWarningMessage($"Realtime[{cycle}]: Warning, a previous cycle is still processing local files. Skipping this interval.");
					return;
				}
				else
				{
					CreateRealtimeFile(cycle);
					CreateRealtimeHTMLfiles(cycle);

					if (FtpOptions.LocalCopyEnabled)
					{
						RealtimeLocalCopy(cycle);
					}
				}
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Realtime[{cycle}]: Error during file copies");
			}
			finally
			{
				realtimeCopySemaphore.Release();
			}

			if (FtpOptions.RealtimeEnabled && FtpOptions.Enabled)
			{
				if (!realtimeFtpSemaphore.Wait(1000, Program.ExitSystemToken))
				{
					// we cannot get the lock - abort
					LogDebugMessage($"Realtime[{cycle}]: Warning, could not get the upload lock, aborting upload for this cycle");
				}
				else
				{
					// We can do some FTP!
					try
					{
						RealtimeUpload(cycle).Wait();
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"Realtime[{cycle}]: Error during realtime upload.");
						// signal the wd to attmpt to reconnect
						RealtimeFtpWatchDogTokenSource.Cancel();
					}
					finally
					{
						realtimeFtpSemaphore.Release();
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

			try
			{
				MySqlRealtimeFile(cycle, true);
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Realtime[{cycle}]: Error in MySqlRealtimeFile()");
			}

			LogDebugMessage($"Realtime[{cycle}]: End cycle");
		}

		public void RealtimeFtpWatchDog()
		{
			if (FtpOptions.FtpMode == FtpProtocols.PHP)
				return;

			if (RealtimeFtpWatchDogTask != null && !RealtimeFtpWatchDogTask.IsCompleted)
			{
				// already running
				return;
			}

			RealtimeFtpWatchDogTask = Task.Run(() =>
			{
				bool connected = false;
				bool reinit = true;
				const string filename = "_cumulusmx_watchdog.txt";
				int connectCount = 0;
				bool weHaveSemaphore = false;

				RealtimeFtpWatchDogTaskTokenSource = new();

				do
				{
					if (!weHaveSemaphore)
					{
						LogDebugMessage("RealtimeFtpWatchDog: Attempting to get the semaphore (wait 20 seconds)");

						if (realtimeFtpSemaphore.Wait(20000, Program.ExitSystemToken))
						{
							LogDebugMessage("RealtimeFtpWatchDog: Got the semaphore");
							weHaveSemaphore = true;
						}
						else
						{
							LogDebugMessage("RealtimeFtpWatchDog: Timed out waiting for the semaphore, continue and try again later");
						}
					}

					do
					{
						if (reinit)
						{
							if (!weHaveSemaphore)
							{
								LogDebugMessage("RealtimeFtpWatchDog: Attempting to get the semaphore (wait 5 seconds)");

								if (realtimeFtpSemaphore.Wait(5000, Program.ExitSystemToken))
								{
									LogDebugMessage("RealtimeFtpWatchDog: Got the semaphore");
									weHaveSemaphore = true;
								}
								else
								{
									LogDebugMessage("RealtimeFtpWatchDog: Timed out waiting for the semaphore");
								}
							}

							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								try
								{
									if (RealtimeSSH != null)
									{
										LogMessage("RealtimeFtpWatchDog: Realtime ftp attempting disconnect");
										RealtimeSSH.Disconnect();
										LogMessage("RealtimeFtpWatchDog: Realtime ftp disconnected");
									}
								}
								catch (ObjectDisposedException)
								{
									LogDebugMessage($"RealtimeFtpWatchDog: Error, connection is disposed");
								}
								catch (Exception ex)
								{
									LogDebugMessage($"RealtimeFtpWatchDog: Error disconnecting from server - {ex.Message}");
								}
								// Attempt a simple reconnect
								try
								{
									LogMessage("RealtimeFtpWatchDog: Realtime ftp attempting to reconnect");
									RealtimeSSHLogin();
									connected = RealtimeSSH.ConnectionInfo.IsAuthenticated;
									LogMessage("RealtimeFtpWatchDog: Reconnected with server (we think)");
								}
								catch (ObjectDisposedException)
								{
									reinit = true;
									LogDebugMessage($"RealtimeFtpWatchDog: Error, connection is disposed");
								}
								catch (Exception ex)
								{
									reinit = true;
									LogErrorMessage($"RealtimeFtpWatchDog: Error reconnecting ftp server - {ex.Message}");
									if (ex.InnerException != null)
									{
										ex = Utils.GetOriginalException(ex);
										LogErrorMessage($"RealtimeFtpWatchDog: Base exception - {ex.Message}");
									}
								}
							}
							else // RealtimeXXXLogin() has its own error handling
							{
								LogFtpMessage("RealtimeFtpWatchDog: Realtime ftp attempting to reinitialise the connection", true);

								RealtimeFTPLogin();
								connected = RealtimeFTP.IsConnected;

								if (connected)
								{
									LogMessage("RealtimeFtpWatchDog: Realtime ftp connection reinitialised");
								}
								else
								{
									LogWarningMessage("RealtimeFtpWatchDog: Realtime ftp connection failed to connect after reinitialisation");
								}
							}
						}


						// We *think* we are connected, now try and do something!
						if (connected)
						{
							reinit = false;

							string tempFile;

							// redo this every time in case the path has been changed
							if (FtpOptions.Directory.Length > 0)
							{
								tempFile = (FtpOptions.Directory.EndsWith('/') ? FtpOptions.Directory : FtpOptions.Directory + '/') + filename;
							}
							else
							{
								tempFile = filename;
							}

							try
							{
								if (FtpOptions.FtpMode == FtpProtocols.SFTP)
								{
									LogDebugMessage("RealtimeFtpWatchDog: Realtime ftp testing the connection");
									LogDebugMessage("RealtimeFtpWatchDog: Attempting to create file - " + tempFile);

									// check we are still flagged as connected
									if (!RealtimeSSH.IsConnected)
									{
										connected = false;
										reinit = true;

										LogWarningMessage("RealtimeFtpWatchDog: Realtime sftp connection is flagged as not connected");
									}
									else
									{
										LogDebugMessage("RealtimeFtpWatchDog: Realtime sftp connection is flagged as connected OK");

										// create an read back a test file
										RealtimeSSH.WriteAllText(tempFile, "test");
										if (RealtimeSSH.ReadAllText(tempFile) != "test")
										{
											connected = false;
											reinit = true;
											LogWarningMessage("RealtimeFtpWatchDog: Realtime sftp failed to write and read a test file");
											LogMessage("RealtimeFtpWatchDog: Realtime sftp failed to write and read a test file - " + tempFile);
										}
										else
										{
											LogDebugMessage("RealtimeFtpWatchDog: Realtime sftp created a test file OK");

											RealtimeSSH.DeleteFile(tempFile);
										}
									}
								}
								else
								{
									// IsStillConnected performs an active check that the server responds
									// There is a problem in FluentFTP 52.1.0 that this does not work on all servers and causes a disconnect!
									//if (!RealtimeFTP.IsStillConnected())

									LogFtpMessage("RealtimeFtpWatchDog: Realtime ftp testing the connection", true);
									LogDebugMessage("RealtimeFtpWatchDog: Attempting to create file - " + tempFile);

									if (!RealtimeFTP.IsConnected || !RealtimeFTP.IsAuthenticated)
									{
										connected = false;
										reinit = true;
										LogWarningMessage("RealtimeFtpWatchDog: Realtime ftp connection flagged as not connected");
									}
									else
									{
										var testBytes = Encoding.ASCII.GetBytes("test");

										if (RealtimeFTP.UploadBytes(testBytes, tempFile, FtpRemoteExists.Overwrite).IsFailure())
										{
											connected = false;
											reinit = true;
											LogWarningMessage("RealtimeFtpWatchDog: Realtime ftp failed to write a test file");
											LogMessage("RealtimeFtpWatchDog: Realtime ftp failed to write a test file - " + tempFile);
										}
										else
										{
											if (!RealtimeFTP.DownloadBytes(out byte[] _bytes, tempFile) || !_bytes.SequenceEqual(testBytes))
											{
												connected = false;
												reinit = true;
												LogWarningMessage("RealtimeFtpWatchDog: Realtime ftp failed to read a test file");
												LogMessage("RealtimeFtpWatchDog: Realtime ftp failed to read a test file - " + tempFile);
											}
											else
											{
												RealtimeFTP.DeleteFile(tempFile);
												LogDebugMessage("RealtimeFtpWatchDog: Realtime ftp created a test file OK");
											}
										}
									}
								}
							}
							catch (Exception ex)
							{
								LogErrorMessage($"RealtimeFtpWatchDog: Realtime ftp connection test Failed - {ex.Message}");

								if (ex.InnerException != null)
								{
									ex = Utils.GetOriginalException(ex);
									LogExceptionMessage(ex, $"RealtimeFtpWatchDog: Base exception follows");
								}

								reinit = true;
								connected = false;
							}
						}

						if (!connected)
						{
							if (connectCount < 10) connectCount++;

							// fully disconnect the existing connection before waiting
							if (FtpOptions.FtpMode == FtpProtocols.SFTP)
							{
								try
								{
									if (RealtimeSSH != null)
									{
										RealtimeSSH.Disconnect();
										RealtimeSSH.Dispose();
									}
								}
								catch
								{
									// do nothing
								}

							}
							else
							{
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
							}

							// add a 30 second x connection attempt count delay between retries, up to 5 minutes
							LogMessage($"RealtimeFtpWatchDog: Connection failed - waiting {30 * connectCount} seconds before trying again");
							Thread.Sleep(30000 * connectCount);
							reinit = true;
						}
					} while (!connected && !Program.ExitSystemToken.IsCancellationRequested);

					// OK we are reconnected, let the FTP recommence
					LogFtpMessage("RealtimeFtpWatchDog: Realtime FTP OK, operations can be resumed", true);
					// reset the wd token
					RealtimeFtpWatchDogTokenSource.Dispose();
					RealtimeFtpWatchDogTokenSource = new();
					try
					{
						// release the semaphore
						weHaveSemaphore = false;
						realtimeFtpSemaphore.Release();
					}
					catch
					{
						// do nothing
					}
					FtpAlarm.LastMessage = "Realtime re-connected";
					FtpAlarm.Triggered = false;

					LogDebugMessage($"RealtimeFtpWatchDog: Sleeping for {realtimeFtpWdInterval} seconds before testing the connection again...");

					var signal = WaitHandle.WaitAny([Program.ExitSystemToken.WaitHandle, RealtimeFtpWatchDogTokenSource.Token.WaitHandle, RealtimeFtpWatchDogTaskTokenSource.Token.WaitHandle], realtimeFtpWdInterval * 1000);
					if (signal == WaitHandle.WaitTimeout)
					{
						// normal timeout, go round again and test
						LogDebugMessage("RealtimeFtpWatchDog: It's time go again");
					}
					else if (signal == 0)
					{
						LogMessage("RealtimeFtpWatchDog: Cumulus exiting, exiting watchdog");
					}
					else if (signal == 1)
					{
						LogMessage("RealtimeFtpWatchDog: FTP error detected!");
						// attempt to grab the semaphore
						LogDebugMessage("RealtimeFtpWatchDog: attempting to get the semaphore");
						if (realtimeFtpSemaphore.Wait(1000, Program.ExitSystemToken))
						{
							// we have the semaphore
							weHaveSemaphore = true;
							LogDebugMessage("RealtimeFtpWatchDog: we have the semaphore");
						}
						else
						{
							LogDebugMessage("RealtimeFtpWatchDog: failed to get the semaphore at this attempt");
						}
						LogMessage("RealtimeFtpWatchDog: Waiting 5 seconds before attempting to reconnect");
						Program.ExitSystemToken.WaitHandle.WaitOne(5 * 1000);
					}
					else if (signal == 2)
					{
						LogMessage("RealtimeFtpWatchDog: Watch dog termination requested");
					}

				} while (!Program.ExitSystemToken.IsCancellationRequested && !RealtimeFtpWatchDogTaskTokenSource.Token.IsCancellationRequested);

				RealtimeFTPDisconnect();

				LogMessage("RealtimeFtpWatchDog: Exiting Task");
			});
		}

		private void RealtimeLocalCopy(byte cycle)
		{
			var dstPath = string.Empty;
			var folderSep1 = Path.DirectorySeparatorChar.ToString();
			var folderSep2 = Path.AltDirectorySeparatorChar.ToString();


			if (FtpOptions.LocalCopyFolder.Length > 0)
			{
				dstPath = (FtpOptions.LocalCopyFolder.EndsWith(folderSep1) || FtpOptions.LocalCopyFolder.EndsWith(folderSep2) ? FtpOptions.LocalCopyFolder[..^1] : FtpOptions.LocalCopyFolder);
			}

			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].Copy)
				{
					var dstFile = Path.Combine(dstPath, RealtimeFiles[i].FileName);
					var srcFile = Path.Combine(RealtimeFiles[i].LocalPath ?? ".", RealtimeFiles[i].FileName);

					try
					{
						LogDebugMessage($"RealtimeLocalCopy[{cycle}]: Copying - {RealtimeFiles[i].FileName}");

						if (RealtimeFiles[i].Create)
						{
							File.Copy(srcFile, dstFile, true);
						}
						else
						{
							var text = String.Empty;

							if (i == (int) RealtimeFileIdx.REALTIME_TXT)
							{
								text = CreateRealtimeFileString(cycle);
							}
							else if (i == (int) RealtimeFileIdx.REALTIMEGAUGES_TXT)
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

			if (FtpOptions.Directory.Length > 0)
			{
				remotePath = (FtpOptions.Directory.EndsWith('/') ? FtpOptions.Directory : FtpOptions.Directory + '/');
			}

			LogFtpDebugMessage($"Realtime[{cycle}]: Real time upload of files starting", true);

			for (var i = 0; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].FTP)
				{
					var remoteFile = remotePath + RealtimeFiles[i].FileName;

					string data = string.Empty;

					if (FtpOptions.FtpMode != FtpProtocols.PHP)
					{
						// realtime file
						if (i == (int) RealtimeFileIdx.REALTIME_TXT)
						{
							data = CreateRealtimeFileString(cycle);
						}
						else if (i == (int) RealtimeFileIdx.REALTIMEGAUGES_TXT)
						{
							data = await ProcessTemplateFile2StringAsync(RealtimeFiles[i].TemplateFileName, true, true);
						}

						using var dataStream = GenerateStreamFromString(data);
						if (FtpOptions.FtpMode == FtpProtocols.SFTP)
						{
							LogDebugMessage($"Realtime[{cycle}]: Uploading - {RealtimeFiles[i].FileName}");
							if (!UploadStream(RealtimeSSH, remoteFile, dataStream, cycle))
							{
								// trigger the WD
								RealtimeFtpWatchDogTokenSource.Cancel();
								return;
							}
						}
						else if (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS)
						{
							LogFtpDebugMessage($"Realtime[{cycle}]: Uploading - {RealtimeFiles[i].FileName}", true);
							if (!UploadStream(RealtimeFTP, remoteFile, dataStream, cycle))
							{
								// trigger the WD
								RealtimeFtpWatchDogTokenSource.Cancel();
								return;
							}
						}
					}
					else // PHP
					{

						try
						{
#if DEBUG
							if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
							{
								LogDebugMessage($"Realtime[{cycle}]: Real time file {RealtimeFiles[i].FileName} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
							}
							await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
							LogDebugMessage($"Realtime[{cycle}]: Real time file {RealtimeFiles[i].FileName} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
							await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
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
							Interlocked.Increment(ref runningTaskCount);

#if DEBUG
							LogDebugMessage($"Realtime[{cycle}]: Processing Real time file [{idx}] - {RealtimeFiles[idx].FileName}");
#endif
							// realtime file
							if (idx == (int) RealtimeFileIdx.REALTIME_TXT)
							{
								data = CreateRealtimeFileString(cycle);
							}
							else if (idx == (int) RealtimeFileIdx.REALTIMEGAUGES_TXT)
							{
								data = await ProcessTemplateFile2StringAsync(RealtimeFiles[idx].TemplateFileName, true, true);
							}

							try
							{
								_ = await UploadString(phpUploadHttpClient, false, string.Empty, data, RealtimeFiles[idx].FileName, cycle);
								// no realtime files are incremental, so no need to update LastDataTime
							}
							finally
							{
								uploadCountLimitSemaphoreSlim.Release();
#if DEBUG
								LogDebugMessage($"Realtime[{cycle}]: Real time file [{idx}] {RealtimeFiles[idx].FileName} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
							}
							return true;
						}, Program.ExitSystemToken));
					}
				}
			}

			LogFtpDebugMessage($"Realtime[{cycle}]: Real time FTP upload files complete", true);


			// Extra files

			if (FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogDebugMessage($"Realtime[{cycle}]: Extra Files starting");

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
							LogDebugMessage($"Realtime[{cycle}]: Extra file: {uploadfile} - No incremental data found, skipping this upload");
							continue;
						}
					}

					try
					{
#if DEBUG
						if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
						{
							LogDebugMessage($"Realtime[{cycle}]: Extra File {uploadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
						}
						await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
						LogDebugMessage($"Realtime[{cycle}]: Extra File {uploadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
						uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
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
							Interlocked.Increment(ref runningTaskCount);

							if (Program.ExitSystemToken.IsCancellationRequested)
								return false;

							// all checks OK, file needs to be uploaded
							// Is this an incremental log file upload?
							if (item.incrementalLogfile && !item.binary)
							{
								LogDebugMessage($"Realtime[{cycle}]: Uploading extra web incremental file {uploadfile} to {remotefile} ({(incremental ? $"Incremental - {linesAdded} lines" : "Full file")})");
								if (await UploadString(phpUploadHttpClient, incremental, string.Empty, data, remotefile, cycle, item.binary, item.UTF8, true, item.logFileLastLineNumber))
								{
									ActiveExtraFiles[idx].logFileLastLineNumber += linesAdded;
								}
							}
							else
							{
								LogDebugMessage($"Realtime[{cycle}]: Uploading extra web file {uploadfile} to {remotefile}");

								if (item.process)
								{
									LogDebugMessage($"Realtime[{cycle}]: Processing extra web file {uploadfile}");
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
							LogDebugMessage($"Realtime[{cycle}]: Extra Web File {uploadfile} released semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#endif
						}

						// no void return which cannot be tracked
						return true;
					}, Program.ExitSystemToken));
				}

				// wait for all the tasks to start
				if (runningTaskCount < taskCount)
				{
					do
					{
						if (Program.ExitSystemToken.IsCancellationRequested)
							return;

						await Task.Delay(10);
					} while (runningTaskCount < taskCount);
				}

				// wait for all the tasks to complete
				if (tasklist.Count > 0)
				{
					try
					{
						// wait on the task to complete, but timeout after 20 seconds
						if (Task.WaitAll([.. tasklist], TimeSpan.FromSeconds(20)))
						{
							LogDebugMessage($"Realtime[{cycle}]: Real time files complete, {tasklist.Count} files uploaded");
						}
						else
						{
							LogDebugMessage($"Realtime[{cycle}]: Real time files timed out waiting for the uploads to complete");
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"Realtime[{cycle}]: Error waiting on upload tasks");
					}
				}

				LogDebugMessage($"Realtime[{cycle}]: Real time files process end");
				tasklist.Clear();
			}
			else // It's old fashioned FTP/FTPS/SFTP
			{
				LogFtpDebugMessage($"Realtime[{cycle}]: Real time FTP upload extra files starting", true);

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
						LogFtpMessage("", true);
						LogFtpDebugMessage($"Realtime[{cycle}]: Uploading extra web {uploadfile} to {remotefile}", true);
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
							LogDebugMessage($"Realtime[{cycle}]: Extra file: {uploadfile} - No incremental data found, skipping this upload");
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
							if (!UploadStream(RealtimeSSH, remotefile, strm, cycle))
							{
								// trigger WD
								RealtimeFtpWatchDogTokenSource.Cancel();
								return;
							}
						}
						else
						{
							if (!UploadStream(RealtimeFTP, remotefile, strm, cycle))
							{
								// trigger WD
								RealtimeFtpWatchDogTokenSource.Cancel();
								return;
							}
						}
					}
					else // its just a plain old file - upload it
					{
						if (FtpOptions.FtpMode == FtpProtocols.SFTP)
						{
							if (!UploadFile(RealtimeSSH, uploadfile, remotefile, cycle))
							{
								// trigger WD
								RealtimeFtpWatchDogTokenSource.Cancel();
								return;
							}
						}
						else
						{
							if (!UploadFile(RealtimeFTP, uploadfile, remotefile, cycle))
							{
								// trigger WD
								RealtimeFtpWatchDogTokenSource.Cancel();
								return;
							}
						}
					}
				}

				LogFtpDebugMessage($"Realtime[{cycle}]: Real time FTP upload extra files complete", true);

				// all done
			}
		}

		private void CreateRealtimeHTMLfiles(int cycle)
		{
			// file [0] is the realtime.txt - it does not need a template processing
			for (var i = 1; i < RealtimeFiles.Length; i++)
			{
				if (RealtimeFiles[i].Create && !string.IsNullOrWhiteSpace(RealtimeFiles[i].TemplateFileName))
				{
					var destFile = Path.Combine(RealtimeFiles[i].LocalPath, RealtimeFiles[i].FileName);
					LogDebugMessage($"Realtime[{cycle}]: Creating realtime file - {RealtimeFiles[i].FileName}");
					try
					{
						ProcessTemplateFile(RealtimeFiles[i].TemplateFileName, destFile, true, UTF8encode);
					}
					catch (Exception ex)
					{
						LogDebugMessage($"Realtime[{cycle}]: Error creating file [{RealtimeFiles[i].FileName}] to [{destFile}]. Error = {ex.Message}");
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

		private readonly Lock tokenParserLock = new();

		public void TokenParserOnToken(string strToken, ref string strReplacement)
		{
			lock (tokenParserLock)
			{
				var tagParams = new Dictionary<string, string>();
				var paramList = ParseParams(strToken);
				var webTag = paramList[0];

				tagParams.Add("webtag", webTag);
				for (int i = 1; i < paramList.Count; i += 2)
				{
					// odd numbered entries are keys
					string key = paramList[i];
					// even numbered entries are values
					string value = paramList[i + 1];
					tagParams.Add(key, value);
				}

				strReplacement = WebTags.GetWebTagText(webTag, tagParams);
			}
		}

		private static List<string> ParseParams(string line)
		{
			if (string.IsNullOrWhiteSpace(line))
				return [];

			var parts = new List<string>();

			int len = line.Length;
			int idx = 0;

			// skip any leading whitespace
			while (idx < len && char.IsWhiteSpace(line[idx]))
				idx++;

			// read initial token (everything up to first whitespace)
			int start = idx;
			while (idx < len && !char.IsWhiteSpace(line[idx]))
				idx++;
			var initial = line[start..idx].ToLowerInvariant();
			parts.Add(initial); // preserve initial token exactly as input

			// skip whitespace after initial token
			while (idx < len && char.IsWhiteSpace(line[idx]))
				idx++;

			// parse remaining key/value or standalone tokens
			while (idx < len)
			{
				// read key (up to '=' or whitespace)
				start = idx;
				while (idx < len && !char.IsWhiteSpace(line[idx]) && line[idx] != '=')
					idx++;

				if (idx >= len)
				{
					// trailing standalone token (no '=')
					var standalone = line[start..idx].ToLowerInvariant();
					parts.Add(standalone);
					break;
				}

				// If we hit whitespace, there might still be an '=' after some spaces (e.g. "key = value")
				int scan = idx;
				while (scan < len && char.IsWhiteSpace(line[scan]))
					scan++;

				if (scan < len && line[scan] == '=')
				{
					// key found
					var key = line[start..idx].Trim().ToLowerInvariant();
					parts.Add(key);

					// move idx to character after '='
					idx = scan + 1;

					// skip spaces before value
					while (idx < len && char.IsWhiteSpace(line[idx]))
						idx++;

					// parse value
					if (idx < len && line[idx] == '"')
					{
						// quoted value - capture inner text (allow empty "")
						idx++; // skip opening quote
						start = idx;
						while (idx < len && line[idx] != '"')
							idx++;
						var value = line[start..idx]; // excludes quotes
						parts.Add(value);
						// skip closing quote if present
						if (idx < len && line[idx] == '"')
							idx++;
					}
					else
					{
						// unquoted value - up to next whitespace
						start = idx;
						while (idx < len && !char.IsWhiteSpace(line[idx]))
							idx++;
						var value = line[start..idx];
						parts.Add(value);
					}
				}
				else if (line[idx] == '=')
				{
					// direct '=' encountered (no spaces): key is from start..idx
					var key = line[start..idx].Trim().ToLowerInvariant();
					parts.Add(key);
					// move past '='
					idx++;

					// skip spaces before value
					while (idx < len && char.IsWhiteSpace(line[idx]))
						idx++;

					// parse value (quoted or unquoted)
					if (idx < len && line[idx] == '"')
					{
						idx++;
						start = idx;
						while (idx < len && line[idx] != '"')
							idx++;
						var value = line[start..idx];
						parts.Add(value);
						if (idx < len && line[idx] == '"')
							idx++;
					}
					else
					{
						start = idx;
						while (idx < len && !char.IsWhiteSpace(line[idx]))
							idx++;
						var value = line[start..idx];
						parts.Add(value);
					}
				}
				else
				{
					// standalone token (no '=' found)
					var standalone = line[start..idx].ToLowerInvariant();
					parts.Add(standalone);
				}

				// skip whitespace before next pair
				while (idx < len && char.IsWhiteSpace(line[idx]))
					idx++;
			}

			return parts;
		}

		private void CleanUpHashFiles()
		{
			foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "hash_md5_*.txt"))
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
			DateTime utcNow = now.ToUniversalTime();
			double[] moonriseset = MoonriseMoonset.MoonRise(now.Year, now.Month, now.Day, TimeZoneInfo.Local.GetUtcOffset(now).TotalHours, (double) Latitude, (double) Longitude);
			MoonRiseTime = TimeSpan.FromHours(moonriseset[0]);
			MoonSetTime = TimeSpan.FromHours(moonriseset[1]);

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
			var nowDate = DateTime.Now.Date;

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
					sunrise = nowDate.Add(new TimeSpan(h, m, s));
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
					sunset = nowDate.Add(new TimeSpan(h, m, s));
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
			DateTime today = DateTime.Now.Date.AddHours(12);  // Use around midday to avoid DST change issues
			DateTime tomorrow = today.AddDays(1);
			try
			{
				GetSunriseSunset(today, out SunRiseTime, out SunSetTime, out SunAlwaysUp, out SunAlwaysDown);

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
					if (SunRiseTime == DateTime.MinValue)
					{
						LogMessage("Sunrise: --:--:--");
						LogMessage("Sunset : " + SunSetTime.ToString("HH:mm:ss"));
						DayLength = SunSetTime - today;
					}
					else if (SunSetTime == DateTime.MinValue)
					{
						LogMessage("Sunrise: " + SunRiseTime.ToString("HH:mm:ss"));
						LogMessage("Sunset : --:--:--");
						DayLength = tomorrow - SunRiseTime;
					}
					else if (SunSetTime > SunRiseTime)
					{
						LogMessage("Sunrise: " + SunRiseTime.ToString("HH:mm:ss"));
						LogMessage("Sunset : " + SunSetTime.ToString("HH:mm:ss"));
						DayLength = SunSetTime - SunRiseTime;
					}
					else
					{
						LogMessage("Sunrise: " + SunRiseTime.ToString("HH:mm:ss"));
						LogMessage("Sunset : " + SunSetTime.ToString("HH:mm:ss"));
						DayLength = new TimeSpan(24, 0, 0) - (SunRiseTime - SunSetTime);
					}
				}

				LogMessage($"Day length: {(int) DayLength.TotalHours}:" + DayLength.ToString(@"mm\:ss"));

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
					if (tomorrowSunRiseTime == DateTime.MinValue)
					{
						LogMessage("Tomorrow Sunrise: --:--:--");
						LogMessage("Tomorrow Sunset : " + tomorrowSunSetTime.ToString("HH:mm:ss"));
						tomorrowDayLength = tomorrowSunSetTime - today;
					}
					else if (tomorrowSunSetTime == DateTime.MinValue)
					{
						LogMessage("Tomorrow Sunrise: " + tomorrowSunRiseTime.ToString("HH:mm:ss"));
						LogMessage("Tomorrow Sunset : --:--:--");
						tomorrowDayLength = tomorrow - tomorrowSunRiseTime;
					}
					else if (tomorrowSunSetTime > SunRiseTime)
					{
						LogMessage("Tomorrow Sunrise: " + tomorrowSunRiseTime.ToString("HH:mm:ss"));
						LogMessage("Tomorrow Sunset : " + tomorrowSunSetTime.ToString("HH:mm:ss"));
						tomorrowDayLength = tomorrowSunSetTime - tomorrowSunRiseTime;
					}
					else
					{
						LogMessage("Tomorrow Sunrise: " + tomorrowSunRiseTime.ToString("HH:mm:ss"));
						LogMessage("Tomorrow Sunset : " + tomorrowSunSetTime.ToString("HH:mm:ss"));
						tomorrowDayLength = new TimeSpan(24, 0, 0) - (tomorrowSunRiseTime - tomorrowSunSetTime);
					}
				}

				LogMessage($"Tomorrow day length: {(int) tomorrowDayLength.TotalHours}:" + tomorrowDayLength.ToString(@"mm\:ss"));


				var tomorrowdiff = tomorrowDayLength - DayLength;
				LogDebugMessage("Tomorrow length diff: " + tomorrowdiff.ToString(@"hh\:mm\:ss"));

				bool tomorrowminus;

				if (tomorrowdiff.Ticks < 0)
				{
					tomorrowminus = true;
					tomorrowdiff = tomorrowdiff.Negate();
				}
				else
				{
					tomorrowminus = false;
				}

				if (tomorrowminus)
				{
					try
					{
						TomorrowDayLengthText = string.Format(Trans.thereWillBeMinSLessDaylightTomorrow, (int)tomorrowdiff.TotalMinutes, tomorrowdiff.Seconds);
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
						TomorrowDayLengthText = string.Format(Trans.thereWillBeMinSMoreDaylightTomorrow, (int)tomorrowdiff.TotalMinutes, tomorrowdiff.Seconds);
					}
					catch (Exception)
					{
						TomorrowDayLengthText = "Error in MoreDaylightTomorrow format string";
					}
				}
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Error calculating sunrise and sunset times");
				SunRiseTime = DateTime.MinValue;
				SunSetTime = DateTime.MinValue;
				SunAlwaysUp = false;
				SunAlwaysDown = false;
				DayLength = new TimeSpan(0, 0, 0);
				TomorrowDayLengthText = string.Empty;
			}

			try
			{
				GetDawnDusk(today, out Dawn, out Dusk, out TwilightAlways, out TwilightNever);

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
						DaylightLength = Dusk - today;
					}
					else if (Dusk == DateTime.MinValue)
					{
						DaylightLength = tomorrow - Dawn;
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
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Error calculating dawn and dusk times");
				Dawn = DateTime.MinValue;
				Dusk = DateTime.MinValue;
				TwilightAlways = false;
				TwilightNever = false;
				DaylightLength = new TimeSpan(0, 0, 0);
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

		private void ReadConfigFile()
		{
			try
			{
				phpMaxConnectionsPerServer = Program.configFile.runtimeOptions.configProperties.PhpMaxConnections;
				realtimeFtpWdInterval = Program.configFile.runtimeOptions.configProperties.RealtimeFtpWatchDogInterval;
				return;
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "Error reading CumulusMX.runtimeconfig.json Using defaults");

				phpMaxConnectionsPerServer = 3;
				realtimeFtpWdInterval = 300;
			}
		}


		public int xapPort { get; set; }

		public string xapUID { get; set; }

		public bool xapEnabled { get; set; }

		public bool CloudBaseInFeet { get; set; }

		public string[] WebcamURL { get; set; } = new string[4];

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

		public bool WebIntervalEnabled { get; set; }

		public bool WebAutoUpdate { get; set; }

		public int WMR200TempChannel { get; set; }

		public int WMR928TempChannel { get; set; }

		public int RTdisconnectcount { get; set; }

		public string AirLinkInIPAddr { get; set; }

		public string AirLinkOutIPAddr { get; set; }

		public bool AirLinkInEnabled { get; set; }

		public bool AirLinkOutEnabled { get; set; }

		public bool EcowittExtraEnabled { get; set; }
		public bool EcowittCloudExtraEnabled { get; set; }
		public string EcowittApplicationKey { get; set; }
		public string EcowittUserApiKey { get; set; }
		public string EcowittMacAddress { get; set; } = string.Empty;
		public List<string> EcowittCameraMacAddress { get; set; } = [];
		public bool EcowittSetCustomServer { get; set; }
		public string EcowittGatewayAddr { get; set; }
		public string EcowittLocalAddr { get; set; }
		public int EcowittCustomInterval { get; set; }
		public int EcowittCloudDataUpdateInterval { get; set; }
		public bool EcowittUseSdCard { get; set; }
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

		public bool PurpleAirEnabled { get; set; }
		public string[] PurpleAirIpAddress { get; set; } = new string[4];
		public int[] PurpleAirAlgorithm { get; set; } = new int[4];
		public int[] PurpleAirThSensor { get; set; } = new int[4];
		//public string PurpleAirApiKey { get; set; }
		//public int PurpleAirSensorIndex { get; set; }
		//public string PurpleAirReadKey { get; set; }

		public double[] LaserDepthBaseline { get; set; } = new double[5];
		public bool[] LaserIsSnowSensor { get; set; } = new bool[5];
		public int LaserPrimarySnowSensor { get; set; }

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
		public double SnowDepthMinInc { get; set; }
		public int SnowDepthMedianMins { get; set; }
		public double SnowDepthEmaTimeMins { get; set; }
		public double SnowDepthClipDelta { get; set; }


		public int SnowSeasonStart { get; set; }
		public bool SnowLogging { get; set; }


		public bool HourlyForecast { get; set; }

		/// <summary>
		///  0 = station, 1 = Cumulus, 2 = forecast.txt, 3 = None
		/// </summary>
		public int ForecastSource { get; set; }
		public DateTime LastForecastDotTxtReadTime { get; set; } = DateTime.MinValue;

		public bool DavisConsoleHighGust { get; set; }

		public bool DavisCalcAltPress { get; set; }

		public bool DavisUseDLLBarCalData { get; set; }

		public int LCMaxWind { get; set; }


		public DateTime RecordsBeganDateTime { get; set; }

		public int YTDrainyear { get; set; }

		public double YTDrain { get; set; }

		public string LocationDesc { get; set; } = string.Empty;

		public string LocationName { get; set; } = string.Empty;

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

		public bool FineOffsetStation { get; set; }

		public bool DavisStation { get; set; }
		public string TempTrendFormat { get; set; }
		public string PressTrendFormat { get; set; }

		public StationManufacturer Manufacturer { get; set; }
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
		internal int WllPrimarySunshine;

		internal int[] WllSoilTempTx = new int[17];
		internal int[] WllSoilTempIdx = [0, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];

		internal int[] WllSoilMoistureTx = new int[17];
		internal int[] WllSoilMoistureIdx = [0, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];

		internal int[] WllLeafWetTx = new int[9];
		internal int[] WllLeafWetIdx = [0, 1, 2, 1, 2, 1, 2, 1, 2];


		internal int[] WllExtraTempTx = new int[17];
		internal int[] WllExtraTempIdx = [0, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];

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
		/// <summary>
		/// 0 = Tipping bucket
		/// 1 = Piezo
		/// </summary>
		internal int Gw1000PrimaryRainSensor;

		internal Timer WebTimer = new();

		public enum StationManufacturer
		{
			UNKNOWN = -1,
			DAVIS = 0,
			OREGON = 1,
			EW = 2,
			LACROSSE = 3,
			OREGONUSB = 4,
			INSTROMET = 5,
			ECOWITT = 6,
			HTTPSTATION = 7,
			AMBIENT = 8,
			WEATHERFLOW = 9,
			SIMULATOR = 10,
			JSONSTATION = 11
		}

		public static string LatestError { get; set; }
		public static DateTime LatestErrorTS { get; set; } = DateTime.MinValue;
		internal WeatherStation Station { get => station; set => station = value; }
		public static CancellationTokenSource RealtimeFtpWatchDogTokenSource { get; set; } = new();
		private Task RealtimeFtpWatchDogTask;
		public static CancellationTokenSource RealtimeFtpWatchDogTaskTokenSource { get; set; }

		internal DateTime defaultRecordTS = DateTime.MinValue;
		internal const string WxnowFile = "wxnow.txt";
		private readonly string RealtimeFile = "realtime.txt";
		private FtpClient RealtimeFTP;
		private SftpClient RealtimeSSH;
		private int realtimeFtpWdInterval;
		private byte RealtimeCycleCounter;
		private byte IntervalCycleCounter;

		internal FileGenerationOptions[] StdWebFiles;
		internal FileGenerationOptions[] RealtimeFiles;
		internal FileGenerationOptions[] GraphDataFiles;
		internal FileGenerationOptions[] GraphDataEodFiles;

		public bool FirstRun
		{
			get
			{
				return StationType == -1 && !File.Exists(TodayIniFile);
			}
		}

		public string GetLogFileName(DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = MeteoDate(thedate);

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Path.Combine(ProgramOptions.DataPath, datestring + "log.txt");
		}

		public string GetExtraLogFileName(DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = MeteoDate(thedate);

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Path.Combine(ProgramOptions.DataPath, "ExtraLog" + datestring + ".txt");
		}

		public string GetAirLinkLogFileName(DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = MeteoDate(thedate);

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Path.Combine(ProgramOptions.DataPath, "AirLink" + datestring + "log.txt");
		}

		public string GetCustomIntvlLogFileName(int idx, DateTime thedate)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			DateTime logfiledate = MeteoDate(thedate);

			var datestring = logfiledate.ToString("yyyyMM");
			datestring = datestring.Replace(".", "");

			return Path.Combine(ProgramOptions.DataPath, CustomIntvlLogSettings[idx].FileName + "-" + datestring + ".txt");
		}

		public string GetCustomDailyLogFileName(int idx)
		{
			return Path.Combine(ProgramOptions.DataPath, CustomDailyLogSettings[idx].FileName + ".txt");
		}

		public const int NumLogFileFields = 31;

		public async Task DoLogFile(DateTime timestamp, bool live)
		{
			// Writes an entry to the n-minute log file. Fields are comma-separated:
			// 0  Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  MetData time - hh:mm
			// 2  MetData temperature
			// 3  MetData humidity
			// 4  MetData dewpoint
			// 5  MetData wind speed
			// 6  Recent (10-minute) high gust
			// 7  Average wind bearing
			// 8  MetData rainfall rate
			// 9  Total rainfall today so far
			// 10  MetData sea level pressure
			// 11  Total rainfall counter as held by the station
			// 12  Inside temperature
			// 13  Inside humidity
			// 14  MetData gust (i.e. 'Latest')
			// 15  Wind chill
			// 16  Heat Index
			// 17  UV Index
			// 18  Solar Radiation
			// 19  Evapotranspiration
			// 20  Annual Evapotranspiration
			// 21  Apparent temperature
			// 22  MetData theoretical max solar radiation
			// 23  Hours of sunshine so far today
			// 24  MetData wind bearing
			// 25  RG-11 rain total
			// 26  Rain since midnight
			// 27  Feels like
			// 28  Humidex
			// 29  BGT
			// 30  WBGT


			// make sure solar max is calculated for those stations without a solar sensor
			LogMessage("DoLogFile: Writing log entry for " + timestamp.ToCmxLogFormat());
			LogDebugMessage("DoLogFile: max gust: " + MetData.RecentMaxGust.ToString(WindFormat));

			MetData.CurrentSolarMax = AstroLib.SolarMax(timestamp, (double) Longitude, (double) Latitude, ConvertUnits.AltitudeM(Altitude), out station.SolarElevation, SolarOptions);

			var filename = GetLogFileName(timestamp);

			var line = LogFileRec.CurrentToCsv(timestamp, this, station);
			var inv = CultureInfo.InvariantCulture;
			var sep = ",";
			var success = false;
			var retries = LogFileRetries;
			//var charArr = sb.ToString().ToCharArray();
			var charArr = line.ToCharArray();

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
					LogMessage($"DoLogFile: log entry for {timestamp.ToCmxLogFormat()} written");
				}
				catch (Exception ex)
				{
					retries--;
					if (retries == 0)
					{
						LogExceptionMessage(ex, $"DoLogFile: Error writing entry for {timestamp.ToCmxLogFormat()}");
					}
					else
					{
						LogMessage($"DoLogFile: Error writing entry for {timestamp.ToCmxLogFormat()}, error = \"{ex.Message}\", will retry...");
					}

					await Task.Delay(250);
				}
			} while (!success && retries > 0);


			station.WriteTodayFile(timestamp, true);

			if (MySqlFuncs.MySqlSettings.Monthly.Enabled)
			{
				string queryString = string.Empty;
				try
				{
					MySqlILastntervalTime = timestamp;

					StringBuilder values = new StringBuilder(MonthlyTable.StartOfInsert, 600);
					values.Append(" Values('");
					values.Append(timestamp.ToString("yyyy-MM-dd HH:mm", inv) + "'");
					values.Append(sep + MetData.Temperature.ToFixed(TempFormat));
					values.Append(sep + MetData.Humidity.ToString());
					values.Append(sep + MetData.Dewpoint.ToFixed(TempFormat));
					values.Append(sep + MetData.WindAverage.ToString(WindAvgFormat, inv));
					values.Append(sep + MetData.RecentMaxGust.ToString(WindFormat, inv));
					values.Append(sep + MetData.WindAvgBearing.ToString());
					values.Append(sep + MetData.RainRate.ToString(RainFormat, inv));
					values.Append(sep + MetData.RainToday.ToString(RainFormat, inv));
					values.Append(sep + MetData.Pressure.ToString(PressFormat, inv));
					values.Append(sep + MetData.RainCounter.ToString(RainFormat, inv));
					values.Append(sep + MetData.TemperatureIn.ToFixed(TempFormat, "NULL"));
					values.Append(sep + MetData.HumidityIn.ToText("NULL"));
					values.Append(sep + MetData.WindLatest.ToString(WindFormat, inv));
					values.Append(sep + MetData.WindChill.ToFixed(TempFormat));
					values.Append(sep + MetData.HeatIndex.ToFixed(TempFormat));
					values.Append(sep + MetData.UV.ToFixed(UVFormat, "NULL"));
					values.Append(sep + MetData.SolarRad.ToText("NULL"));
					values.Append(sep + MetData.ET.ToFixed(ETFormat));
					values.Append(sep + MetData.AnnualETTotal.ToString(ETFormat, inv));
					values.Append(sep + MetData.ApparentTemperature.ToFixed(TempFormat));
					values.Append(sep + MetData.CurrentSolarMax.ToString());
					values.Append(sep + MetData.SunshineHours.ToString(SunFormat, inv));
					values.Append(sep + MetData.WindBearing.ToString());
					values.Append(sep + MetData.RG11RainToday.ToString(RainFormat, inv));
					values.Append(sep + MetData.RainSinceMidnight.ToFixed(RainFormat));
					values.Append(sep + "'" + station.CompassPoint(MetData.WindAvgBearing) + "'");
					values.Append(sep + "'" + station.CompassPoint(MetData.WindBearing) + "'");
					values.Append(sep + MetData.FeelsLike.ToFixed(TempFormat));
					values.Append(sep + MetData.Humidex.ToFixed(TempFormat));
					values.Append(sep + MetData.BlackGlobeTemp.ToFixed(TempFormat, "NULL"));
					values.Append(sep + MetData.WetBulbGlobeTemp.ToFixed(TempFormat, "NULL"));

					values.Append(')');

					queryString = values.ToString();

				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "DoLogFile: Error creating MySQL query string");
				}

				if (queryString != string.Empty)
				{
					if (live)
					{
						// do the update
						await MySqlFuncs.MySqlCommandAsync(queryString, "DoLogFile");
					}
					else
					{
						// save the string for later
						LogDebugMessage("DoLogFile: Buffering MySQL insert for later processing");
						MySqlFuncs.MySqlList.Enqueue(new SqlCache() { statement = queryString });
					}
				}
			}
		}

		public const int NumExtraLogFileFields = 144;

		public async Task DoExtraLogFile(DateTime timestamp)
		{
			// Writes an entry to the n-minute extralogfile. Fields are comma-separated:
			// 0  Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  MetData time - hh:mm
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
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16
			// 119-122 AQ PM10 1-4
			// 123-126 AQ PM10 Avg 1-4
			// 127-143 Soil EC 1-16

			var filename = GetExtraLogFileName(timestamp);

			LogDebugMessage($"DoExtraLogFile: Writing log entry for {timestamp}");

			var line = ExtraLogFileRec.CurrentToCsv(timestamp, this, station);

			var success = false;
			var retries = LogFileRetries;
			var charArr = line.ToCharArray();

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
					retries--;
					if (retries == 0)
					{
						LogExceptionMessage(ex, $"DoExtraLogFile: Error writing log entry {timestamp}");
					}
					else
					{
						LogMessage($"DoExtraLogFile: Error writing entry for {timestamp}, error = \"{ex.Message}\", will retry...");
					}

					await Task.Delay(250);
				}
			} while (!success && retries > 0);
		}

		public const int NumAirLinkLogFileFields = 56;

		public async Task DoAirLinkLogFile(DateTime timestamp)
		{
			// Writes an entry to the n-minute airlinklogfile. Fields are comma-separated:
			// 0  Date/Time in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
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
			// 17 Indoor Percent received 24-hour
			// 18 Indoor Percent received nowcast
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
			// 44 Outdoor Percent received 24-hour
			// 45 Outdoor Percent received nowcast
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
			var csv = AirLinkLogFileRec.CurrentToCsv(timestamp, this);

			var success = false;
			var retries = LogFileRetries;
			var charArr = csv.ToCharArray();

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
					retries--;
					if (retries == 0)
					{
						LogExceptionMessage(ex, "DoAirLinkLogFile: Error writing log entry for {timestamp}");
					}
					else
					{
						LogMessage($"DoAirLinkLogFile: Error writing entry for {timestamp}, error = \"{ex.Message}\", will retry...");
					}

					await Task.Delay(250);
				}
			} while (!success && retries > 0);
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
					retries--;
					if (retries == 0)
					{
						LogExceptionMessage(ex, $"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Error writing log entry for {timestamp}");
					}
					else
					{
						LogMessage($"DoCustomIntervalLog: {CustomIntvlLogSettings[idx].FileName} - Error writing log entry for {timestamp}, error = \"{ex.Message}\", will retry...");
					}

					await Task.Delay(250);
				}
			} while (!success && retries > 0);
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
					retries--;
					if (retries == 0)
					{
						LogExceptionMessage(ex, $"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Error writing log entry");
					}
					else
					{
						LogMessage($"DoCustomDailyLog: {CustomDailyLogSettings[idx].FileName} - Error writing log entry, error = \"{ex.Message}\", will retry...");
					}

					await Task.Delay(250);
				}
			} while (!success && retries > 0);
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
					var procId = Encoding.ASCII.GetBytes(thisProcess.Id.ToString());
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

			string dirpath = daily ? Path.Combine(ProgramOptions.BackupPath, "daily") : ProgramOptions.BackupPath;

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

				LogMessage("BackupData: Creating backup " + filename);

				var configbackup = "Cumulus.ini";
				var uniquebackup = "UniqueId.txt";
				var stringsbackup = "strings.ini";
				var alltimebackup = Path.Combine(ProgramOptions.DataPath, "alltime.ini");
				var monthlyAlltimebackup = Path.Combine(ProgramOptions.DataPath, "monthlyalltime.ini");
				var daybackup = Path.Combine(ProgramOptions.DataPath, "dayfile.txt");
				var yesterdaybackup = Path.Combine(ProgramOptions.DataPath, "yesterday.ini");
				var todaybackup = Path.Combine(ProgramOptions.DataPath, "today.ini");
				var monthbackup = Path.Combine(ProgramOptions.DataPath, "month.ini");
				var yearbackup = Path.Combine(ProgramOptions.DataPath, "year.ini");
				var diarybackup = Path.Combine(ProgramOptions.DataPath, "diary.db");
				var dbBackup = Path.Combine(ProgramOptions.DataPath, "cumulusmx.db");

				var LogFile = GetLogFileName(timestamp);
				var logbackup = Path.Combine("data", Path.GetFileName(LogFile));
				var extraFile = GetExtraLogFileName(timestamp);
				var extraBackup = Path.Combine("data", Path.GetFileName(extraFile));

				var AirLinkFile = GetAirLinkLogFileName(timestamp);
				//var AirLinkBackup = Path.Combine("data", Path.GetFileName(AirLinkFile));


				if (!File.Exists(Path.Combine(dirpath, filename)))
				{
					// create a zip archive file for the backup
					try
					{
						using (FileStream zipFile = new FileStream(Path.Combine(dirpath, filename), FileMode.Create))
						{
							using ZipArchive archive = new ZipArchive(zipFile, ZipArchiveMode.Create);
							try
							{
								if (File.Exists(AlltimeIniFile))
									archiveFile(archive, AlltimeIniFile, alltimebackup);
								if (File.Exists(MonthlyAlltimeIniFile))
									archiveFile(archive, MonthlyAlltimeIniFile, monthlyAlltimebackup);
								if (File.Exists(DayFileName))
									archiveFile(archive, DayFileName, daybackup);
								if (File.Exists(TodayIniFile))
									archiveFile(archive, TodayIniFile, todaybackup);
								if (File.Exists(YesterdayFile))
									archiveFile(archive, YesterdayFile, yesterdaybackup);
								if (File.Exists(LogFile))
									archiveFile(archive, LogFile, logbackup);
								if (File.Exists(MonthIniFile))
									archiveFile(archive, MonthIniFile, monthbackup);
								if (File.Exists(YearIniFile))
									archiveFile(archive, YearIniFile, yearbackup);
								if (File.Exists("Cumulus.ini"))
									archiveFile(archive, "Cumulus.ini", configbackup);
								if (File.Exists("UniqueId.txt"))
									archiveFile(archive, "UniqueId.txt", uniquebackup);
								if (File.Exists("strings.ini"))
									archiveFile(archive, "strings.ini", stringsbackup);
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
									var backUpDest = Path.Combine(dirpath, "cumulusmx.db");
									var zipLocation = Path.Combine("data", "cumulusmx.db");
									LogDebugMessage("Making backup copy of the database");
									station.RecentDataDb.Backup(backUpDest);
									LogDebugMessage("Completed backup copy of the database");

									LogDebugMessage("Archiving backup copy of the database");
									archive.CreateEntryFromFile(backUpDest, zipLocation);
									LogDebugMessage("Completed backup copy of the database");

									LogDebugMessage("Deleting backup copy of the database");
									File.Delete(backUpDest);

									backUpDest = Path.Combine(dirpath, "diary.db");
									zipLocation = Path.Combine("data", "diary.db");
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
										archiveFile(archive, dbfile, dbBackup);

									if (File.Exists(dbfile + "-journal"))
										archiveFile(archive, dbfile + "-journal", dbBackup + "-journal");

									if (File.Exists(diaryfile))
										archiveFile(archive, diaryfile, diarybackup);

									if (File.Exists(diaryfile + "-journal"))
										archiveFile(archive, diaryfile + "-journal", diarybackup + "-journal");

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
									archiveFile(archive, extraFile, Path.Combine("data", Path.GetFileName(extraBackup)));
								if (File.Exists(AirLinkFile))
									archiveFile(archive, AirLinkFile, Path.Combine("data", Path.GetFileName(AirLinkFile)));

								// custom logs
								for (var i = 0; i < 10; i++)
								{
									if (CustomIntvlLogSettings[i].Enabled)
									{
										var custfilename = GetCustomIntvlLogFileName(i, timestamp);
										if (File.Exists(custfilename))
											archiveFile(archive, custfilename, Path.Combine("data", Path.GetFileName(custfilename)));
									}

									if (CustomDailyLogSettings[i].Enabled)
									{
										var custfilename = GetCustomDailyLogFileName(i);
										if (File.Exists(custfilename))
											archiveFile(archive, custfilename, Path.Combine("data", Path.GetFileName(custfilename)));
									}
								}

								// Do not do this extra backup between 00:00 & Roll-over hour on the first of the month
								// as the month has not yet rolled over - only applies for start-up backups
								var rollover = RolloverHour;
								if (RolloverHour == 9 && Use10amInSummer && TimeZoneInfo.Local.IsDaylightSavingTime(timestamp))
								{
									rollover = 10;
								}

								if (timestamp.Day == 1 && timestamp.Hour >= rollover)
								{
									var newTime = timestamp.AddDays(-1);
									// on the first of month, we also need to backup last months files as well
									var LogFile2 = GetLogFileName(newTime);
									var logbackup2 = Path.Combine("data", Path.GetFileName(LogFile2));

									var extraFile2 = GetExtraLogFileName(newTime);
									var extraBackup2 = Path.Combine("data", Path.GetFileName(extraFile2));

									var AirLinkFile2 = GetAirLinkLogFileName(timestamp.AddDays(-1));
									var AirLinkBackup2 = Path.Combine("data", Path.GetFileName(AirLinkFile2));

									if (File.Exists(LogFile2))
										archiveFile(archive, LogFile2, logbackup2);
									if (File.Exists(extraFile2))
										archiveFile(archive, extraFile2, extraBackup2);
									if (File.Exists(AirLinkFile2))
										archiveFile(archive, AirLinkFile2, AirLinkBackup2);

									for (var i = 0; i < 10; i++)
									{
										if (CustomIntvlLogSettings[i].Enabled)
										{
											var custfilename = GetCustomIntvlLogFileName(i, newTime);
											if (File.Exists(custfilename))
												archiveFile(archive, custfilename, Path.Combine("data", Path.GetFileName(custfilename)));
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
						LogErrorMessage("BackupData: Error, no permission to create/write file: " + Path.Combine(dirpath, filename));
						LogConsoleMessage("Error, no permission to create/write file: " + Path.Combine(dirpath, filename), ConsoleColor.Yellow);
					}
					catch (Exception ex)
					{
						LogErrorMessage($"BackupData: Error while attempting to create/write file: {Path.Combine(dirpath, filename)}, error message: {ex.Message}");
						LogConsoleMessage($"Error while attempting to create/write file: {Path.Combine(dirpath, filename)}, error message: {ex.Message}", ConsoleColor.Yellow);
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

		private static void archiveFile(ZipArchive archive, string filename, string path)
		{
			// add the file to the archive, with a back-off and retry for file in use
			var maxAttempts = 2;
			var attempt = 0;
			while (true)
			{
				try
				{
					archive.CreateEntryFromFile(filename, path);
					break;
				}
				catch (IOException)
				{
					attempt++;
					if (attempt > maxAttempts)
					{
						// retry failed, give up
						throw;
					}

					// try again in a bit
					Thread.Sleep(100 * attempt);
				}
				catch
				{
					// just throw any other errors
					throw;
				}
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

		public int GetRolloverHour(DateTime timestamp)
		{
			return -GetHourInc(timestamp);
		}

		public DateTime MeteoDate()
		{
			return DateTime.Now.AddHours(GetHourInc());
		}

		public DateTime MeteoDate(DateTime dat)
		{
			return dat.AddHours(GetHourInc(dat));
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

		private void LatestErrorLog(string message, MxLogLevel level)
		{
			if (level >= ErrorListLoggingLevel)
			{
				if (ErrorList.Count >= 50)
				{
					int toRemove = ErrorList.Count - 49;
					for (int i = 0; i < toRemove; i++)
					{
						_ = ErrorList.Dequeue();
					}
				}

				ErrorList.Enqueue((DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss - ") + WebUtility.HtmlEncode(message)));

				LatestError = message;
				LatestErrorTS = DateTime.Now;

				ErrorAlarm.LastMessage = message;
				ErrorAlarm.Triggered = true;
			}
		}

		public void Stop()
		{
			LogMessage("Cumulus close requested");

			LogMessage("Stopping timers");
			try { RealtimeTimer.Stop(); } catch { /* do nothing */ }
			try { Wund.IntTimer.Stop(); } catch { /* do nothing */ }
			try { WebTimer.Stop(); } catch { /* do nothing */ }
			try { AWEKAS.IntTimer.Stop(); } catch { /* do nothing */ }
			try { CustomHttpSecondsTimer.Stop(); } catch { /* do nothing */ }

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

				purpleAir?.Stop();

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
			var cycle = IntervalCycleCounter++;

			try
			{
				LogDebugMessage($"Interval[{cycle}]: Process starting");

				if (!RealtimeIntervalEnabled)
				{
					CreateRealtimeFile(999);
					MySqlRealtimeFile(999, true);
				}

				LogDebugMessage($"Interval[{cycle}]: Creating standard web files");
				for (var i = 0; i < StdWebFiles.Length; i++)
				{
					if (StdWebFiles[i].Create && !string.IsNullOrWhiteSpace(StdWebFiles[i].TemplateFileName))
					{
						var destFile = Path.Combine(StdWebFiles[i].LocalPath, StdWebFiles[i].FileName);
						if (StdWebFiles[i].FileName == "wxnow.txt")
						{
							station.CreateWxnowFile();
						}
						else
						{
							ProcessTemplateFile(StdWebFiles[i].TemplateFileName, destFile, true, UTF8encode);
						}
					}
				}
				LogDebugMessage($"Interval[{cycle}]: Done creating standard Data file");

				LogDebugMessage($"Interval[{cycle}]: Creating graph data files");
				station.CreateGraphDataFiles();
				LogDebugMessage($"Interval[{cycle}]: Done creating graph data files");

				LogDebugMessage($"Interval[{cycle}]: Creating extra files");

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

						LogDebugMessage($"Interval[{cycle}]: Copying extra file {uploadfile} to {remotefile}");
						try
						{
							if (item.process)
							{
								LogDebugMessage($"Interval[{cycle}]: Processing extra file {uploadfile}");
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
							LogDebugMessage($"Interval[{cycle}]: Error copying extra file: " + ex.Message);
						}
					}
					else
					{
						LogWarningMessage($"Interval[{cycle}]: Warning, extra web file not found - {uploadfile}");
					}
				}

				LogDebugMessage($"Interval[{cycle}]: Done creating extra files");

				if (!string.IsNullOrEmpty(ExternalProgram))
				{
					if (!File.Exists(ExternalProgram))
					{
						LogWarningMessage($"Interval[{cycle}]: Warning - External program '{ExternalProgram}' does not exist");
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
							LogDebugMessage($"Interval[{cycle}]: Executing external program " + ExternalProgram + " " + args);
							_ = Utils.RunExternalTask(ExternalProgram, args, false);
							LogDebugMessage($"Interval[{cycle}]: External program started");
						}
						catch (FileNotFoundException)
						{
							LogWarningMessage($"Interval[{cycle}]: Error starting external program: file not found - " + ExternalProgram);
						}
						catch (Exception ex)
						{
							LogWarningMessage($"Interval[{cycle}]: Error starting external program: " + ex.Message);
						}
					}
				}

				DoLocalCopy();

				if (!intervaltimeFtpSemaphore.Wait(1000, Program.ExitSystemToken))
				{
					// we cannot get the lock - abort
					LogDebugMessage($"Interval[{cycle}]: Warning, could not get the upload lock, aborting upload for this cycle");
				}
				else
				{
					try
					{
						await DoIntervalUpload(cycle);
					}
					finally
					{
#if DEBUG
						LogDebugMessage($"Interval[{cycle}]: Releasing the upload lock");
#endif
						intervaltimeFtpSemaphore.Release();
					}
				}
			}
			finally
			{
				WebUpdating = 0;
				LogDebugMessage($"Interval[{cycle}]: Process complete");
			}
		}

		public void DoLocalCopy()
		{
			var remotePath = FtpOptions.LocalCopyFolder;

			try
			{
				if (!FtpOptions.LocalCopyEnabled)
					return;

				if (FtpOptions.LocalCopyFolder.Length > 0)
				{
					remotePath = FtpOptions.LocalCopyFolder;
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
			var dstfile = string.Empty;

			if (NOAAconf.NeedCopy)
			{
				// upload NOAA reports
				LogDebugMessage("LocalCopy: Copying NOAA reports");

				try
				{
					var dstPath = string.IsNullOrEmpty(NOAAconf.CopyFolder) ? remotePath : NOAAconf.CopyFolder;

					srcfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestMonthReport);
					dstfile = Path.Combine(dstPath, NOAAconf.LatestMonthReport);

					LogDebugMessage($"LocalCopy: NOAA report - {dstfile}");
					File.Copy(srcfile, dstfile, true);

					srcfile = Path.Combine(ProgramOptions.ReportsPath, NOAAconf.LatestYearReport);
					dstfile = Path.Combine(dstPath, NOAAconf.LatestYearReport);

					LogDebugMessage($"LocalCopy: NOAA report - {dstfile}");
					File.Copy(srcfile, dstfile, true);

					NOAAconf.NeedCopy = false;

					LogDebugMessage("LocalCopy: Done copying NOAA reports");
				}
				catch (Exception ex)
				{
					LogErrorMessage($"LocalCopy: Error copying NOAA report {srcfile} to {dstfile}- " + ex.Message);
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
					dstfile = remotePath + StdWebFiles[i].FileName;
					// the files are no longer always created, so gen them on the fly if required
					if (StdWebFiles[i].Create)
					{
						try
						{

							srcfile = Path.Combine(StdWebFiles[i].LocalPath, StdWebFiles[i].FileName);
							LogDebugMessage($"LocalCopy: Copying standard data file - {dstfile}");
							File.Copy(srcfile, dstfile, true);
							success++;
						}
						catch (Exception e)
						{
							LogErrorMessage($"LocalCopy: Error copying standard data file {srcfile} to {dstfile}");
							LogMessage($"LocalCopy: Error = {e.Message}");
							failed++;
						}
					}
					else
					{
						try
						{
							string text;
							if (StdWebFiles[i].FileName == "wxnow.txt")
							{
								text = station.CreateWxnowFileString();
							}
							else
							{
								text = ProcessTemplateFile2String(StdWebFiles[i].TemplateFileName, true);
							}

							LogDebugMessage($"LocalCopy: Copying standard data file - {dstfile}");
							File.WriteAllText(dstfile, text);
							success++;
						}
						catch (Exception e)
						{
							LogErrorMessage($"LocalCopy: Error creating standard data file {dstfile}");
							LogMessage($"LocalCopy: Error = {e.Message}");
							failed++;
						}
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
					dstfile = remotePath + GraphDataFiles[i].FileName;

					// the files are no longer created when using PHP upload, so gen them on the fly
					if (GraphDataFiles[i].Create)
					{
						try
						{

							srcfile = Path.Combine(GraphDataFiles[i].LocalPath, GraphDataFiles[i].FileName);
							LogDebugMessage($"LocalCopy: Copying graph data file - {dstfile}");
							File.Copy(srcfile, dstfile, true);
							success++;

							// The config files only need uploading once per change
							// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
							if (i == (int) GraphFileIdx.CONFIG || i == (int) GraphFileIdx.AVAILABLE || i == (int) GraphFileIdx.DAILYRAIN || i == (int) GraphFileIdx.DAILYTEMP || i == (int) GraphFileIdx.SUNHOURS)
							{
								GraphDataFiles[i].CopyRequired = false;
							}
						}
						catch (Exception e)
						{
							LogErrorMessage($"LocalCopy: Error copying graph data file {srcfile} to {dstfile}");
							LogMessage($"LocalCopy: Error = {e.Message}");
							failed++;
						}
					}
					else
					{
						try
						{
							var text = station.CreateGraphDataJson(GraphDataFiles[i].FileName, false);
							LogDebugMessage($"LocalCopy: Copying graph data file - {dstfile}");
							File.WriteAllText(dstfile, text);
							success++;

							// The config files only need uploading once per change
							// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
							if (i == (int) GraphFileIdx.CONFIG || i == (int) GraphFileIdx.AVAILABLE || i == (int) GraphFileIdx.DAILYRAIN || i == (int) GraphFileIdx.DAILYTEMP || i == (int) GraphFileIdx.SUNHOURS)
							{
								GraphDataFiles[i].CopyRequired = false;
							}
						}
						catch (Exception e)
						{
							LogErrorMessage($"LocalCopy: Error creating graph data file {dstfile}");
							LogMessage($"LocalCopy: Error = {e.Message}");
							failed++;
						}
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
					dstfile = remotePath + GraphDataEodFiles[i].FileName;

					// the files are no longer created when using PHP upload, so gen them on the fly
					if (GraphDataEodFiles[i].Create)
					{
						try
						{

							srcfile = Path.Combine(GraphDataEodFiles[i].LocalPath, GraphDataEodFiles[i].FileName);
							LogDebugMessage($"LocalCopy: Copying daily graph data file - {dstfile}");
							File.Copy(srcfile, dstfile, true);
							// Uploaded OK, reset the upload required flag
							GraphDataEodFiles[i].CopyRequired = false;
							success++;
						}
						catch (Exception e)
						{
							LogErrorMessage($"LocalCopy: Error copying daily graph data file {srcfile} to {dstfile}");
							LogMessage($"LocalCopy: Error = {e.Message}");
							failed++;
						}
					}
					else
					{
						try
						{
							var text = station.CreateEodGraphDataJson(GraphDataEodFiles[i].FileName);
							LogDebugMessage($"LocalCopy: Copying daily graph data file - {dstfile}");
							File.WriteAllText(dstfile, text);
							// Uploaded OK, reset the upload required flag
							GraphDataEodFiles[i].CopyRequired = false;
							success++;
						}
						catch (Exception e)
						{
							LogErrorMessage($"LocalCopy: Error creating daily graph data file {dstfile}");
							LogMessage($"LocalCopy: Error = {e.Message}");
							failed++;
						}
					}
				}
			}
			LogDebugMessage($"LocalCopy: Done copying daily graph data files - Success: {success}, Failed: {failed}");

			if (MoonImage.Copy && MoonImage.ReadyToCopy)
			{
				try
				{
					LogDebugMessage("LocalCopy: Copying Moon image file to " + MoonImage.CopyDest);
					File.Copy(Path.Combine("web", "moon.png"), MoonImage.CopyDest, true);
					// clear the image ready for copy flag, only upload once an hour
					MoonImage.ReadyToCopy = false;
				}
				catch (Exception e)
				{
					LogErrorMessage($"LocalCopy: Error copying moon image to {MoonImage.CopyDest} - {e.Message}");
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
				try
				{
					using SftpClient conn = sftpClientFactory.CreateClient().Result;
					try
					{
						LogFtpDebugMessage($"ProcessHttpFiles: CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}", false);
						conn.Connect();
					}
					catch (Exception ex)
					{
						LogFtpMessage($"ProcessHttpFiles: Error connecting SFTP - {ex.Message}", false);
						return;
					}

					foreach (var item in uploads)
					{
						Stream strm = null;

						try
						{

							if (Program.ExitSystemToken.IsCancellationRequested)
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
					LogFtpMessage($"ProcessHttpFiles: Error using SFTP connection - {ex.Message}", false);
				}
				LogFtpDebugMessage("ProcessHttpFiles: Connection process complete", false);
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS)
			{
				using FtpClient conn = ftpClientFactory.CreateClient().Result;

				if (FtpOptions.Logging)
				{
					conn.Logger = new FtpLogAdapter(FtpLoggerIN);
				}

				LogFtpMessage("", false); // insert a blank line
				LogFtpDebugMessage($"ProcessHttpFiles: CumulusMX Connecting to " + FtpOptions.Hostname, false);

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
				}
				catch (Exception ex)
				{
					LogFtpMessage("ProcessHttpFiles: Error connecting ftp - " + ex.Message, false);

					if (ex.InnerException != null)
					{
						ex = Utils.GetOriginalException(ex);
						LogFtpMessage($"ProcessHttpFiles: Base exception - {ex.Message}", false);
					}

					return;
				}

				LogDebugMessage("ProcessHttpFiles: Uploading http files");

				foreach (var item in uploads)
				{
					Stream strm = null;

					try
					{

						if (Program.ExitSystemToken.IsCancellationRequested)
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
					LogFtpDebugMessage("ProcessHttpFiles: Disconnected from " + FtpOptions.Hostname, false);
				}

				LogFtpMessage("ProcessHttpFiles: Process complete", false);
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
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"ProcessHttpFiles: Http file: {downloadfile} waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
					LogDebugMessage($"ProcessHttpFiles: Http file: {downloadfile} has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
					uploadCountLimitSemaphoreSlim.Wait(Program.ExitSystemToken);
#endif

					tasklist.Add(Task.Run(async () =>
					{
						try
						{
							if (Program.ExitSystemToken.IsCancellationRequested)
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
					}, Program.ExitSystemToken));

					Interlocked.Increment(ref runningTaskCount);
				});

				LogDebugMessage("ProcessHttpFiles: Done uploading http files");

				// wait for all the files to start
				if (runningTaskCount < taskCount)
				{
					do
					{
						if (Program.ExitSystemToken.IsCancellationRequested)
						{
							LogDebugMessage("ProcessHttpFiles: Upload process aborted");
							return;
						}
						Thread.Sleep(10);
					} while (runningTaskCount < taskCount);
				}

				// wait for all the files to complete
				Task.WaitAll([.. tasklist], Program.ExitSystemToken);

				if (Program.ExitSystemToken.IsCancellationRequested)
				{
					LogDebugMessage($"ProcessHttpFiles: Upload process aborted");
					return;
				}

				LogDebugMessage($"ProcessHttpFiles: Upload process complete, {tasklist.Count} files processed");
				tasklist.Clear();
			}
		}

		// Return True if the connection still exists
		// Return False if the connection is disposed, null, or not connected
		private bool UploadFile(FtpClient conn, string localfile, string remotefile, int cycle)
		{
			string cycleStr;
			bool realtime;
			if (cycle == 9999)
			{
				cycleStr = "NOAA";
				realtime = false;
			}
			else if (cycle >= 1000)
			{
				cycleStr = "Int-" + (cycle - 1000);
				realtime = false;
			}
			else
			{
				cycleStr = cycle.ToString();
				realtime = true;
			}

			LogFtpMessage("", realtime);

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
				LogFtpMessage($"FTP[{cycleStr}]: Error {localfile} - {ex.Message}", realtime);
				FtpAlarm.LastMessage = $"Error {localfile} - {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					LogFtpMessage($"FTP[{cycleStr}]: Inner Exception: {ex.GetBaseException().Message}", realtime);
					LogExceptionMessage(ex, $"FTP[{cycleStr}]: Error {localfile}");
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
				cycleStr = cycle >= 1000 ? "Int-" + (cycle - 1000) : (cycle.ToString());
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

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {localfile}");
				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {localfile}";
				FtpAlarm.Triggered = true;

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

		private async Task<bool> UploadFile(HttpClient httpclient, string localfile, string remotefile, int cycle, bool binary = false, bool utf8 = true)
		{
			string prefix;
			if (cycle == 9999)
			{
				prefix = "PHP[NOAA]";
			}
			else if (cycle >= 1000)
			{
				prefix = $"PHP[Int-{cycle - 1000}]";
			}
			else
			{
				prefix = $"PHP[{cycle}]";
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

		private bool UploadStream(FtpClient conn, string remotefile, Stream dataStream, int cycle)
		{
			string remotefiletmp = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr;
			bool realtime;
			if (cycle == 9999)
			{
				cycleStr = "NOAA";
				realtime = false;
			}
			else if (cycle >= 1000)
			{
				cycleStr = "Int-" + (cycle - 1000);
				realtime = false;
			}
			else
			{
				cycleStr = cycle.ToString();
				realtime = true;
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

				if (!conn.IsConnected)
				{
					LogDebugMessage($"FTP[{cycleStr}]: Not connected, skipping upload of {remotefile}");
					return false;
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

				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {remotefiletmp}", realtime);

				FtpStatus status;
				status = conn.UploadStream(dataStream, remotefiletmp, DeleteBeforeUpload ? FtpRemoteExists.Overwrite : FtpRemoteExists.NoCheck);

				if (status.IsFailure())
				{
					LogErrorMessage($"FTP[{cycleStr}]: Upload of {remotefile} failed");
					return false;
				}
				else if (FTPRename)
				{
					// rename the file
					LogFtpDebugMessage($"FTP[{cycleStr}]: Renaming {remotefiletmp} to {remotefile}", realtime);

					try
					{
						conn.Rename(remotefiletmp, remotefile);
						LogFtpDebugMessage($"FTP[{cycleStr}]: Renamed {remotefiletmp}", realtime);
					}
					catch (Exception ex)
					{
						LogFtpMessage($"FTP[{cycleStr}]: Error renaming {remotefiletmp} to {remotefile} : {ex.Message}", realtime);

						FtpAlarm.LastMessage = $"Error renaming {remotefiletmp} to {remotefile} : {ex.Message}";
						FtpAlarm.Triggered = true;

						if (ex.InnerException != null)
						{
							ex = Utils.GetOriginalException(ex);
							LogFtpMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}", realtime);
						}

						return false;
					}
				}
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error uploading {remotefile} : {ex.Message}", realtime);

				FtpAlarm.LastMessage = $"Error uploading {remotefile} : {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.InnerException != null)
				{
					LogFtpMessage($"FTP[{cycleStr}]: Inner Exception: {ex.GetBaseException().Message}", realtime);
				}

				return false;
			}

			return true;
		}

		// Return True if the connection still exists
		// Return False if the connection is disposed, null, or not connected
		private bool UploadStream(SftpClient conn, string remotefile, Stream dataStream, int cycle)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 1000 ? "Int-" + (cycle - 1000) : cycle.ToString();
			bool realtime = cycle < 1000;

			if (dataStream.Length == 0)
			{
				LogWarningMessage($"SFTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}");
				FtpAlarm.LastMessage = $"The data is empty - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return true;
			}

			if (!conn.IsConnected)
			{
				LogDebugMessage($"SFTP[{cycleStr}]: Not connected, skipping upload of {remotefile}");
				return false;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is null or not connected - skipping upload of {remotefile}");
					FtpAlarm.LastMessage = $"The SFTP object is null or not connected - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {remotefile}");

				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;

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
						LogFtpMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}", realtime);
					}

					// Lets start again anyway! Too hard to tell if the error is recoverable
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
							LogFtpMessage($"SFTP[{cycleStr}]: Base exception - {ex.Message}", realtime);
						}

						return false;
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

				return false;
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

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogErrorMessage($"SFTP[{cycleStr}]: The SFTP object is disposed - skipping upload of {remotefile}");

				FtpAlarm.LastMessage = $"The SFTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;

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
			bool realtime = cycle >= 0;

			if (text.Length == 0)
			{
				LogFtpMessage($"FTP[{cycleStr}]: The data is empty - skipping upload of {remotefile}", realtime);
				FtpAlarm.LastMessage = $"The data is empty - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}

			try
			{
				if (conn == null || !conn.IsConnected)
				{
					LogFtpMessage($"FTP[{cycleStr}]: The FTP object is null or not connected - skipping upload of {remotefile}", realtime);
					FtpAlarm.LastMessage = $"The FTP object is null or not connected - skipping upload of {remotefile}";
					FtpAlarm.Triggered = true;

					return false;
				}
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"FTP[{cycleStr}]: The FTP object is disposed - skipping upload of {remotefile}", realtime);

				FtpAlarm.LastMessage = $"The FTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;

				return false;
			}

			try
			{
				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploading {remotefile} [adding {linesadded} lines]", realtime);

				conn.UploadStream(GenerateStreamFromString(text), remotefile, FtpRemoteExists.AddToEnd);

				LogFtpDebugMessage($"FTP[{cycleStr}]: Uploaded {remotefile} [added {linesadded} lines]", realtime);
			}
			catch (ObjectDisposedException)
			{
				LogFtpMessage($"FTP[{cycleStr}]: The FTP object is disposed", realtime);
				FtpAlarm.LastMessage = $"The FTP object is disposed - skipping upload of {remotefile}";
				FtpAlarm.Triggered = true;
				return false;
			}
			catch (Exception ex)
			{
				LogFtpMessage($"FTP[{cycleStr}]: Error uploading {remotefile} : {ex.Message}", realtime);

				FtpAlarm.LastMessage = $"Error uploading {remotefile} : {ex.Message}";
				FtpAlarm.Triggered = true;

				if (ex.Message.Contains("Permission denied")) // Non-fatal
					return true;

				if (ex.InnerException != null)
				{
					ex = Utils.GetOriginalException(ex);
					LogFtpMessage($"FTP[{cycleStr}]: Base exception - {ex.Message}", realtime);
				}

				// Lets start again anyway! Too hard to tell if the error is recoverable
				conn.Dispose();
				return false;
			}

			return true;
		}


		// Return True if the upload worked
		// Return False if the upload failed
		private async Task<bool> UploadString(HttpClient httpclient, bool incremental, string oldest, string data, string remotefile, int cycle, bool binary = false, bool utf8 = true, bool logfile = false, int linecount = 0)
		{
			var prefix = cycle >= 1000 ? $"PHP[Int-{cycle-1000}]" : $"PHP[{cycle}]";

			if (string.IsNullOrEmpty(data))
			{
				LogWarningMessage($"{prefix}: Uploading to {remotefile}. Warning: No {(incremental ? "incremental" : "")} data found, skipping this upload");

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
					var unixTs = DateTime.Now.ToUnixTime().ToString();

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
								await zipped.WriteAsync(byteData.AsMemory(0, byteData.Length), Program.ExitSystemToken);
							}
							else if (FtpOptions.PhpCompression == "deflate")
							{
								using var zipped = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true);
								await zipped.WriteAsync(byteData.AsMemory(0, byteData.Length), Program.ExitSystemToken);
							}
							else if (FtpOptions.PhpCompression == "br")
							{
								using var zipped = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionMode.Compress, true);
								await zipped.WriteAsync(byteData.AsMemory(0, byteData.Length), Program.ExitSystemToken);
							}

							ms.Position = 0;
							byte[] compressed = new byte[ms.Length];
							_ = await ms.ReadAsync(compressed.AsMemory(0, compressed.Length), Program.ExitSystemToken);

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
						// 406 is reserved for trying to append data to a file that already exists (retry after inital attempt actually worked?)
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
			var uploadfile = Path.Combine(ProgramOptions.ReportsPath, filename);
			var remotefile = NOAAconf.FtpFolder + '/' + filename;

			if (!FtpOptions.Enabled)
				return;

			if (FtpOptions.FtpMode == FtpProtocols.SFTP)
			{
				try
				{
					using SftpClient conn = await sftpClientFactory.CreateClient();
					try
					{
						LogFtpDebugMessage($"SFTP[NOAA]: CumulusMX Connecting to {FtpOptions.Hostname} on port {FtpOptions.Port}", false);
						conn.Connect();
					}
					catch (Exception ex)
					{
						LogFtpMessage($"SFTP[NOAA]: Error connecting SFTP - {ex.Message}", false);

						FtpAlarm.LastMessage = "Error connecting SFTP - " + ex.Message;
						FtpAlarm.Triggered = true;
						return;
					}

					if (conn.IsConnected)
					{
						LogFtpDebugMessage($"SFTP[NOAA]: CumulusMX Connected to {FtpOptions.Hostname} OK", false);
						try
						{
							// upload NOAA reports
							LogFtpDebugMessage("SFTP[NOAA]: Uploading NOAA report " + filename, false);

							UploadFile(conn, uploadfile, remotefile, 9999);

							LogFtpDebugMessage("SFTP[NOAA]: Done uploading NOAA report " + filename, false);
						}
						catch (Exception e)
						{
							LogFtpMessage($"SFTP[NOAA]: Error uploading file {filename} - {e.Message}", false);
							FtpAlarm.LastMessage = "Error uploading NOAA report file - " + e.Message;
							FtpAlarm.Triggered = true;
						}
					}
				}
				catch (Exception ex)
				{
					LogFtpMessage($"SFTP[NOAA]: Error using SFTP connection - {ex.Message}", false);
				}
				LogFtpDebugMessage("SFTP[NOAA]: Upload process complete", false);
			}
			else if (FtpOptions.FtpMode == FtpProtocols.FTP || (FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
				using FtpClient conn = await ftpClientFactory.CreateClient();

				if (FtpOptions.Logging)
				{
					conn.Logger = new FtpLogAdapter(FtpLoggerIN);
				}

				LogFtpMessage("", false); // insert a blank line
				LogFtpDebugMessage($"FTP[NOAA]: CumulusMX Connecting to " + FtpOptions.Hostname, false);
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
				}
				catch (Exception ex)
				{
					LogFtpMessage("FTP[NOAA]: Error connecting ftp - " + ex.Message, false);

					FtpAlarm.LastMessage = "Error connecting ftp - " + ex.Message;
					FtpAlarm.Triggered = true;

					if (ex.InnerException != null)
					{
						ex = Utils.GetOriginalException(ex);
						LogFtpMessage($"FTP[NOAA]: Base exception - {ex.Message}", false);
					}

					return;
				}

				if (conn.IsConnected)
				{
					try
					{
						// upload NOAA reports
						LogFtpMessage("", false);
						LogFtpDebugMessage("FTP[NOAA]: Uploading NOAA report" + filename, false);

						UploadFile(conn, uploadfile, remotefile, 9999);

						LogFtpDebugMessage($"FTP[NOAA]: Upload of NOAA report {filename} complete", false);
					}
					catch (Exception e)
					{
						LogFtpMessage($"FTP[NOAA]: Error uploading NOAA file: {e.Message}", false);
						FtpAlarm.LastMessage = "Error connecting ftp - " + e.Message;
						FtpAlarm.Triggered = true;
					}
				}

				// b3045 - dispose of connection
				conn.Disconnect();
				LogFtpDebugMessage("FTP[NOAA]: Disconnected from " + FtpOptions.Hostname, false);
				LogFtpMessage("FTP[NOAA]: Process complete", false);
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
					if (uploadCountLimitSemaphoreSlim.CurrentCount == 0)
					{
						LogDebugMessage($"PHP[NOAA]: NOAA report waiting for semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
					}
					await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
					LogDebugMessage($"PHP[NOAA]: NOAA report has a semaphore [{uploadCountLimitSemaphoreSlim.CurrentCount}]");
#else
					await uploadCountLimitSemaphoreSlim.WaitAsync(Program.ExitSystemToken);
#endif
				}
				catch (OperationCanceledException)
				{
					return;
				}

				Interlocked.Increment(ref taskCount);

				tasklist.Add(Task.Run(async () =>
				{
					Interlocked.Increment(ref runningTaskCount);

					if (Program.ExitSystemToken.IsCancellationRequested)
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
				}, Program.ExitSystemToken));

				// wait for all the files to start
				if (runningTaskCount < taskCount)
				{
					do
					{
						if (Program.ExitSystemToken.IsCancellationRequested)
						{
							LogDebugMessage($"PHP[NOAA]: Upload process aborted");
							return;
						}
						await Task.Delay(10);
					} while (runningTaskCount < taskCount);
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

				if (Program.ExitSystemToken.IsCancellationRequested)
				{
					LogDebugMessage($"PHP[NOAA]: Upload process aborted");
					return;
				}

				LogDebugMessage($"PHP[NOAA]: Upload process complete");
				tasklist.Clear();

				return;
			}
		}

		public void LogMessage(string message, MxLogLevel level = MxLogLevel.Info)
		{
			Program.MxLogger.Info(message);

			LatestErrorLog(message, level);
		}

		public void LogDebugMessage(string message)
		{
			if (ProgramOptions.DebugLogging)
			{
				Program.MxLogger.Debug(message);
			}
		}

		public void LogDataMessage(string message)
		{
			if (ProgramOptions.DataLogging)
			{
				Program.MxLogger.Trace(message);
			}
		}

		public void LogExceptionMessage(Exception ex, string message, bool logError = true)
		{
			if (ProgramOptions.DebugLogging)
			{
				Program.MxLogger.Error(message + " - " + Utils.ExceptionToString(ex, out _));
			}
			else
			{
				Program.MxLogger.Error(message + " - " + Utils.ExceptionToString(ex));
			}

			if (logError)
			{
				LatestErrorLog(message + " - " + ex.GetBaseException().Message, MxLogLevel.Error);

				LatestError = message + " - " + ex.Message;
				LatestErrorTS = DateTime.Now;
				ErrorAlarm.LastMessage = message;
				ErrorAlarm.Triggered = true;
			}
		}

		public void LogCriticalMessage(string message)
		{
			Program.MxLogger.Error(message);
			LatestErrorLog(message, MxLogLevel.Critical);
		}

		public void LogErrorMessage(string message)
		{
			Program.MxLogger.Error(message);
			LatestErrorLog(message, MxLogLevel.Error);
		}

		public void LogWarningMessage(string message)
		{
			Program.MxLogger.Warn(message);
			LatestErrorLog(message, MxLogLevel.Warning);
		}

		public void LogSpikeRemoval(string message)
		{
			if (ErrorLogSpikeRemoval)
			{
				Program.MxLogger.Warn("Spike removal: " + message);
			}
		}


		public void LogFtpMessage(string message, bool realTime)
		{
			if (!string.IsNullOrEmpty(message))
			{
				LogMessage(message);
			}
			if (FtpOptions.Logging && (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
#pragma warning disable CA2254 // Template should be a static expression
				if (realTime && FtpLoggerMXRT != null)
				{
					FtpLoggerMXRT.LogInformation(message);
				}
				else if (FtpLoggerMXIN != null)
				{
					FtpLoggerMXIN.LogInformation(message);
				}
#pragma warning restore CA2254 // Template should be a static expression
			}
		}

		public void LogFtpDebugMessage(string message, bool realTime)
		{
			if (!string.IsNullOrEmpty(message))
			{
				LogDebugMessage(message);
			}

			if (FtpOptions.Logging && (FtpOptions.FtpMode == FtpProtocols.FTP || FtpOptions.FtpMode == FtpProtocols.FTPS))
			{
#pragma warning disable CA2254 // Template should be a static expression
				if (realTime && FtpLoggerMXRT != null)
				{
					FtpLoggerMXRT.LogDebug(message);
				}
				else if (FtpLoggerMXIN != null)
				{
					FtpLoggerMXIN.LogDebug(message);
				}
#pragma warning restore CA2254 // Template should be a static expression
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

		public static string GetErrorLog()
		{
			var arr = ErrorList.ToArray();

			if (arr.Length == 0)
			{
				return "[\"No errors recorded so far\"]";
			}
			else if (arr.Length > 2)
			{
				return arr.Reverse().ToJson();
			}
			else
			{
				return arr.ToJson();
			}
		}

		public string ClearErrorLog()
		{
			LatestError = string.Empty;
			LatestErrorTS = DateTime.MinValue;
			ErrorAlarm.Triggered = false;
			ErrorList.Clear();
			return GetErrorLog();
		}

		public string ResetAlarm(IHttpContext context)
		{
			var ret = "Reset";
			var data = new StreamReader(context.Request.InputStream).ReadToEnd();
			var name = data;

			try
			{
				if (Enum.TryParse(typeof(AlarmIds), data[5..], out var idObj) && idObj is AlarmIds id)
				{
					switch (id)
					{
						//case "ChangeAlarm":
						//	break;
						case AlarmIds.DataStopped:
							DataStoppedAlarm.ResetAlarm();
							break;
						case AlarmIds.BatteryLow:
							BatteryLowAlarm.ResetAlarm();
							break;
						case AlarmIds.Sensor:
							SensorAlarm.ResetAlarm();
							break;
						case AlarmIds.Spike:
							SpikeAlarm.ResetAlarm();
							break;
						case AlarmIds.WindHigh:
							HighWindAlarm.ResetAlarm();
							break;
						case AlarmIds.WindGust:
							HighGustAlarm.ResetAlarm();
							break;
						case AlarmIds.RainRate:
							HighRainRateAlarm.ResetAlarm();
							break;
						case AlarmIds.Rainfall:
							HighRainTodayAlarm.ResetAlarm();
							break;
						case AlarmIds.PressUp:
							PressChangeAlarm.ResetUpAlarm();
							break;
						case AlarmIds.PressDown:
							PressChangeAlarm.ResetDownAlarm();
							break;
						case AlarmIds.PressHigh:
							HighPressAlarm.ResetAlarm();
							break;
						case AlarmIds.PressLow:
							LowPressAlarm.ResetAlarm();
							break;
						case AlarmIds.TempUp:
							TempChangeAlarm.ResetUpAlarm();
							break;
						case AlarmIds.TempDown:
							TempChangeAlarm.ResetDownAlarm();
							break;
						case AlarmIds.TempLow:
							LowTempAlarm.ResetAlarm();
							break;
						case AlarmIds.TempHigh:
							HighTempAlarm.ResetAlarm();
							break;
						case AlarmIds.Upgrade:
							UpgradeAlarm.ResetAlarm();
							break;
						case AlarmIds.Firmware:
							FirmwareAlarm.ResetAlarm();
							break;
						case AlarmIds.Thirdparty:
							ThirdPartyAlarm.ResetAlarm();
							break;
						case AlarmIds.MySQL:
							MySqlUploadAlarm.ResetAlarm();
							break;
						case AlarmIds.IsRaining:
							IsRainingAlarm.ResetAlarm();
							break;
						case AlarmIds.Record:
							NewRecordAlarm.ResetAlarm();
							break;
						case AlarmIds.FTP:
							FtpAlarm.ResetAlarm();
							break;
						case AlarmIds.Error:
							ErrorAlarm.ResetAlarm();
							break;
						case AlarmIds.User1:
							UserAlarms[0].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User2:
							UserAlarms[1].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User3:
							UserAlarms[2].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User4:
							UserAlarms[3].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User5:
							UserAlarms[4].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User6:
							UserAlarms[5].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User7:
							UserAlarms[6].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User8:
							UserAlarms[7].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User9:
							UserAlarms[8].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						case AlarmIds.User10:
							UserAlarms[9].ResetAlarm();
							name += " - " + UserAlarms[0].Name;
							break;
						default:
							ret = "Alarm id '" + id + "' is illegal";
							break;
					}
				}
				else
				{
					ret = "Invalid id: '" + data + "'";
				}
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "ResetAlarm error");
				ret = "Error: " + ex.Message;
			}

			LogMessage($"ResetAlarm: Resetting alarm '{name}' - {ret}");

			return ret;
		}



		private void CreateRealtimeFile(int cycle)
		{
			// Does the user want to create the realtime.txt file?
			if (!RealtimeFiles[0].Create)
			{
				return;
			}

			var filename = Path.Combine(Directory.GetCurrentDirectory(), RealtimeFile);

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
			40     4107       Cumulus build
			41     36.0       10-minute high gust
			42     10.3       heat index
			43     10.5       humidex
			44     1.2        UV
			45     0.4        ET
			46     673        Solar radiation
			47     234        Average WindBearing (degrees)
			48     2.5        Rain last hour
			49     5          Forecast number
			50     1          Is daylight? (1 = yes)
			51     0          Sensor contact lost (1 = yes)
			52     NNW        wind direction (average)
			53     2040       Cloud base
			54     ft         Cloud base units
			55     12.3       Apparent Temp
			56     11.4       Sunshine hours today
			57     420        MetData theoretical max solar radiation
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
				sb.Append(MetData.Temperature.ToFixed(TempFormat) + ' ');              // 3
				sb.Append(MetData.Humidity.ToString() + ' ');                          // 4
				sb.Append(MetData.Dewpoint.ToFixed(TempFormat) + ' ');                 // 5
				sb.Append(MetData.WindAverage.ToString(WindAvgFormat, InvC) + ' ');           // 6
				sb.Append(MetData.WindLatest.ToString(WindFormat, InvC) + ' ');               // 7
				sb.Append(MetData.WindBearing.ToString() + ' ');                                  // 8
				sb.Append(MetData.RainRate.ToString(RainFormat, InvC) + ' ');                 // 9
				sb.Append(MetData.RainToday.ToString(RainFormat, InvC) + ' ');                // 10
				sb.Append(MetData.Pressure.ToString(PressFormat, InvC) + ' ');                // 11
				sb.Append(station.CompassPoint(MetData.WindBearing) + ' ');                       // 12
				sb.Append(Beaufort(MetData.WindAverage) + ' ');                               // 13
				sb.Append(Units.WindText + ' ');                                              // 14
				sb.Append(Units.TempText[1].ToString() + ' ');                                // 15
				sb.Append(Units.PressText + ' ');                                             // 16
				sb.Append(Units.RainText + ' ');                                              // 17
				sb.Append(MetData.WindRunToday.ToString(WindRunFormat, InvC) + ' ');          // 18
				sb.Append(station.PressTrendVal.ToFixed(PressTrendFormat) + ' ');             // 19
				sb.Append(MetData.RainMonth.ToString(RainFormat, InvC) + ' ');                // 20
				sb.Append(MetData.RainYear.ToString(RainFormat, InvC) + ' ');                 // 21
				sb.Append(MetData.RainYesterday.ToString(RainFormat, InvC) + ' ');            // 22
				sb.Append((MetData.TemperatureIn ?? 0).ToFixed(TempFormat) + ' ');        // 23
				sb.Append((MetData.HumidityIn ?? 0).ToString() + ' ');                    // 24
				sb.Append(MetData.WindChill.ToFixed(TempFormat) + ' ');                       // 25
				sb.Append(station.TempTrendVal.ToFixed(TempTrendFormat) + ' ');               // 26
				sb.Append(DailyHighLow.Today.HighTemp.ToFixed(TempFormat) + ' ');              // 27
				sb.Append(DailyHighLow.Today.HighTempTime.ToString("HH:mm "));                 // 28
				sb.Append(DailyHighLow.Today.LowTemp.ToFixed(TempFormat) + ' ');               // 29
				sb.Append(DailyHighLow.Today.LowTempTime.ToString("HH:mm "));                  // 30
				sb.Append(DailyHighLow.Today.HighWind.ToString(WindAvgFormat, InvC) + ' ');    // 31
				sb.Append(DailyHighLow.Today.HighWindTime.ToString("HH:mm "));                 // 32
				sb.Append(DailyHighLow.Today.HighGust.ToString(WindFormat, InvC) + ' ');       // 33
				sb.Append(DailyHighLow.Today.HighGustTime.ToString("HH:mm "));                 // 34
				sb.Append(DailyHighLow.Today.HighPress.ToString(PressFormat, InvC) + ' ');     // 35
				sb.Append(DailyHighLow.Today.HighPressTime.ToString("HH:mm "));                // 36
				sb.Append(DailyHighLow.Today.LowPress.ToString(PressFormat, InvC) + ' ');      // 37
				sb.Append(DailyHighLow.Today.LowPressTime.ToString("HH:mm "));                 // 38
				sb.Append(Version + ' ');                                                     // 39
				sb.Append(Build + ' ');                                                       // 40
				sb.Append(MetData.RecentMaxGust.ToString(WindFormat, InvC) + ' ');            // 41
				sb.Append(MetData.HeatIndex.ToFixed(TempFormat) + ' ');                       // 42
				sb.Append(MetData.Humidex.ToFixed(TempFormat) + ' ');                         // 43
				sb.Append((MetData.UV ?? 0).ToString(UVFormat, InvC) + ' ');                  // 44
				sb.Append(MetData.ET.ToString(ETFormat, InvC) + ' ');                         // 45
				sb.Append((MetData.SolarRad ?? 0).ToString() + ' ');                          // 46
				sb.Append(MetData.WindAvgBearing.ToString() + ' ');                               // 47
				sb.Append(MetData.RainLastHour.ToString(RainFormat, InvC) + ' ');             // 48
				sb.Append(MetData.Forecastnumber.ToString() + ' ');                           // 49
				sb.Append(IsDaylight() ? "1 " : "0 ");                                        // 50
				sb.Append(station.SensorContactLost ? "1 " : "0 ");                           // 51
				sb.Append(station.CompassPoint(MetData.WindAvgBearing) + ' ');                    // 52
				sb.Append(MetData.CloudBase.ToString() + ' ');                                // 53
				sb.Append(CloudBaseInFeet ? "ft " : "m ");                                    // 54
				sb.Append(MetData.ApparentTemperature.ToFixed(TempFormat) + ' ');             // 55
				sb.Append(MetData.SunshineHours.ToString(SunFormat, InvC) + ' ');             // 56
				sb.Append(Convert.ToInt32(MetData.CurrentSolarMax).ToString() + ' ');         // 57
				sb.Append(MetData.IsSunny ? "1 " : "0 ");                                     // 58
				sb.Append(MetData.FeelsLike.ToFixed(TempFormat) + ' ');                       // 59
				sb.AppendLine(MetData.RainWeek.ToString(RainFormat, InvC));                   // 60
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

			if (!MySqlFuncs.MySqlSettings.Realtime.Enabled)
				return;

			if (MySqlFuncs.MySqlSettings.RealtimeLimit1Minute && MySqlLastRealtimeTime.Minute == timestamp.Minute)
				return;

			MySqlLastRealtimeTime = timestamp;

			var InvC = CultureInfo.InvariantCulture;
			var sep = ",";

			StringBuilder values = new StringBuilder(RealtimeTable.StartOfInsert, 1024);
			values.Append(" Values(");
			values.Append(timestamp.ToString("\\'yyyy-MM-dd HH:mm:ss\\'", InvC));
			values.Append(sep + MetData.Temperature.ToFixed(TempFormat));
			values.Append(sep + MetData.Humidity.ToString());
			values.Append(sep + MetData.Dewpoint.ToFixed(TempFormat));
			values.Append(sep + MetData.WindAverage.ToString(WindAvgFormat, InvC));
			values.Append(sep + MetData.WindLatest.ToString(WindFormat, InvC));
			values.Append(sep + MetData.WindBearing.ToString());
			values.Append(sep + MetData.RainRate.ToString(RainFormat, InvC));
			values.Append(sep + MetData.RainToday.ToString(RainFormat, InvC));
			values.Append(sep + MetData.Pressure.ToString(PressFormat, InvC) );
			values.Append(sep + "'" + station.CompassPoint(MetData.WindBearing) + "'");
			values.Append(sep + Beaufort(MetData.WindAverage));
			values.Append(sep + "'" + Units.WindText + "'");
			values.Append(sep + "'" + Units.TempText[1].ToString() + "'");
			values.Append(sep + "'" + Units.PressText + "'");
			values.Append(sep + "'" + Units.RainText + "'");
			values.Append(sep + MetData.WindRunToday.ToString(WindRunFormat, InvC) );
			values.Append(sep + "'" + (station.PressTrendVal > 0 ? '+' + station.PressTrendVal.ToFixed(PressFormat) : station.PressTrendVal.ToFixed(PressFormat)) + "'");
			values.Append(sep + MetData.RainMonth.ToString(RainFormat, InvC));
			values.Append(sep + MetData.RainYear.ToString(RainFormat, InvC));
			values.Append(sep + MetData.RainYesterday.ToString(RainFormat, InvC));
			values.Append(sep + MetData.TemperatureIn.ToFixed(TempFormat, "NULL"));
			values.Append(sep + MetData.HumidityIn.ToText("NULL"));
			values.Append(sep + MetData.WindChill.ToFixed(TempFormat));
			values.Append(sep + "'" + station.TempTrendVal.ToFixed(TempTrendFormat) + "'");
			values.Append(sep + DailyHighLow.Today.HighTemp.ToFixed(TempFormat));
			values.Append(sep + DailyHighLow.Today.HighTempTime.ToString("\\'HH:mm\\'"));
			values.Append(sep + DailyHighLow.Today.LowTemp.ToFixed(TempFormat));
			values.Append(sep + DailyHighLow.Today.LowTempTime.ToString("\\'HH:mm\\'"));
			values.Append(sep + DailyHighLow.Today.HighWind.ToString(WindAvgFormat, InvC));
			values.Append(sep + DailyHighLow.Today.HighWindTime.ToString("\\'HH:mm\\'"));
			values.Append(sep + DailyHighLow.Today.HighGust.ToString(WindFormat, InvC) );
			values.Append(sep + DailyHighLow.Today.HighGustTime.ToString("\\'HH:mm\\'"));
			values.Append(sep + DailyHighLow.Today.HighPress.ToString(PressFormat, InvC));
			values.Append(sep + DailyHighLow.Today.HighPressTime.ToString("\\'HH:mm\\'"));
			values.Append(sep + DailyHighLow.Today.LowPress.ToString(PressFormat, InvC));
			values.Append(sep + DailyHighLow.Today.LowPressTime.ToString("\\'HH:mm\\'"));
			values.Append(sep + "'" + Version +"'");
			values.Append(sep + "'" + Build + "'");
			values.Append(sep + MetData.RecentMaxGust.ToString(WindFormat, InvC));
			values.Append(sep + MetData.HeatIndex.ToFixed(TempFormat));
			values.Append(sep + MetData.Humidex.ToFixed(TempFormat));
			values.Append(sep + MetData.UV.ToFixed(UVFormat, "NULL"));
			values.Append(sep + MetData.ET.ToString(ETFormat, InvC));
			values.Append(sep + MetData.SolarRad.ToText("NULL"));
			values.Append(sep + MetData.WindAvgBearing.ToString());
			values.Append(sep + MetData.RainLastHour.ToString(RainFormat, InvC));
			values.Append(sep + MetData.Forecastnumber.ToString());
			values.Append(sep + (IsDaylight() ? "'1'" : "'0'"));
			values.Append(sep + (station.SensorContactLost ? "'1'" : "'0'"));
			values.Append(sep + "'" + station.CompassPoint(MetData.WindAvgBearing) + "'");
			values.Append(sep + (MetData.CloudBase).ToString() );
			values.Append(sep + (CloudBaseInFeet ? "'ft'" : "'m'") );
			values.Append(sep + MetData.ApparentTemperature.ToFixed(TempFormat));
			values.Append(sep + MetData.SunshineHours.ToString(SunFormat, InvC));
			values.Append(sep + MetData.CurrentSolarMax);
			values.Append(sep + (MetData.IsSunny ? "'1'" : "'0'"));
			values.Append(sep + MetData.FeelsLike.ToFixed(TempFormat));
			values.Append(sep + MetData.RainWeek.ToString(RainFormat, InvC));
			values.Append(')');

			string valuesString = values.ToString();
			List<string> cmds = [valuesString];

			if (live)
			{
				if (!string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.RealtimeRetention))
				{
					cmds.Add($"DELETE IGNORE FROM {MySqlFuncs.MySqlSettings.Realtime.TableName} WHERE LogDateTime < DATE_SUB('{DateTime.Now:yyyy-MM-dd HH:mm}', INTERVAL {MySqlFuncs.MySqlSettings.RealtimeRetention});");
				}

				// do the update
				_ = MySqlFuncs.MySqlCommandAsync(cmds, $"Realtime[{cycle}]");
			}
			else
			{
				// not live, buffer the command for later
				MySqlFuncs.MySqlList.Enqueue(new SqlCache() { statement = cmds[0] });
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
				templatefile = Path.Combine(Directory.GetCurrentDirectory(), template);
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
				templatefile = Path.Combine(Directory.GetCurrentDirectory(), template);
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

			var extras = new object[] { airLinkOut, airLinkIn, ecowittExtra, ambientExtra, ecowittCloudExtra, stationJsonExtra, purpleAir };

			if (extras.Any(s => s != null))
			{
				LogMessage("Starting Extra Sensors");
				airLinkOut?.Start();
				airLinkIn?.Start();
				ecowittExtra?.Start();
				ambientExtra?.Start();
				ecowittCloudExtra?.Start();
				stationJsonExtra?.Start();
				purpleAir?.Start();
			}
			else
			{
				LogMessage("No Extra Sensors to start");
			}

			LogMessage("Start Timers");
			// start the general one-second timer
			LogMessage("Starting 1-second timer");
			station.StartSecondsTimer();
			LogMessage($"Data logging interval = {DataLogInterval} ({logints[DataLogInterval]} mins)");


			if (RealtimeIntervalEnabled)
			{
				if (FtpOptions.Enabled && FtpOptions.RealtimeEnabled && FtpOptions.FtpMode != FtpProtocols.PHP)
				{
					LogConsoleMessage("Connecting real time FTP");

					RealtimeFtpWatchDog();
				}

				LogMessage("Starting Realtime timer, interval = " + RealtimeInterval / 1000 + " seconds");
			}
			else
			{
				LogMessage("Realtime not enabled");
			}

			RealtimeTimer.Enabled = RealtimeIntervalEnabled;

			CustomHttpSecondsTimer.Enabled = CustomHttpSecondsEnabled;

			Wund.CatchUpIfRequired();

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

			if (MySqlFuncs.MySqlList.IsEmpty)
			{
				// No archived entries to upload
				LogDebugMessage("MySqlList is Empty");
			}
			else
			{
				// start the archive upload thread
				_ = MySqlFuncs.ProcessMySqlStartupBuffer();
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

		internal async void CustomMysqlSecondsChanged()
		{
			if (station.DataStopped)
			{
				// No data coming in, do not do anything
				return;
			}

			if ((!station.PressReadyToPlot || !station.TempReadyToPlot || !station.WindReadyToPlot) && !StationOptions.NoSensorCheck)
			{
				// not all the data is ready and NoSensorCheck is not enabled
				LogMessage($"CustomMysqlSecondsChanged: Not all data is ready, aborting process");
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
						if (!string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomSecs.Commands[i]))
						{
							tokenParser.InputText = MySqlFuncs.MySqlSettings.CustomSecs.Commands[i];
							await MySqlFuncs.MySqlCommandAsync(tokenParser.ToStringFromString(), $"CustomSqlSecs[{i}]");
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


		internal async Task CustomMysqlMinutesUpdate(DateTime now, bool live)
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
						if (!live && !MySqlFuncs.MySqlSettings.CustomMins.CatchUp[i])
						{
							// doing catch-up and catch-up is disabled for this entry
							continue;
						}

						if (!string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomMins.Commands[i]) && now.Minute % MySqlFuncs.MySqlSettings.CustomMins.Intervals[i] == 0)
						{

							tokenParser.InputText = MySqlFuncs.MySqlSettings.CustomMins.Commands[i];

							if (live)
							{
								// do the update
								await MySqlFuncs.MySqlCommandAsync(tokenParser.ToStringFromString(), $"CustomSqlMins[{i}]");
							}
							else
							{
								// save the string for later
								MySqlFuncs.MySqlList.Enqueue(new SqlCache() { statement = tokenParser.ToStringFromString() });
							}
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

		internal async Task CustomMysqlRollover(bool live)
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
						if (!live && !MySqlFuncs.MySqlSettings.CustomRollover.CatchUp[i])
						{
							// doing catch-up and catch-up is disabled for this entry
							continue;
						}

						if (!string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomRollover.Commands[i]))
						{
							tokenParser.InputText = MySqlFuncs.MySqlSettings.CustomRollover.Commands[i];
							await MySqlFuncs.MySqlCommandAsync(tokenParser.ToStringFromString(), $"CustomSqlRollover[{i}]");
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
						if (string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]))
						{
							continue;
						}

						// is this a one-off, or a repeater
						if (MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] == 1440)
						{
							if (MySqlFuncs.MySqlSettings.CustomTimed.StartTimes[i] == roundedTime)
							{
								tokenParser.InputText = MySqlFuncs.MySqlSettings.CustomTimed.Commands[i];
								var cmd = tokenParser.ToStringFromString();
								LogDebugMessage("MySQLTimed: Running - " + cmd);
								await MySqlFuncs.MySqlCommandAsync(cmd, $"CustomSqlTimed[{i}]");
								continue;
							}
						}
						else // it's a repeater
						{
							if (MySqlFuncs.MySqlSettings.CustomTimed.NextUpdate[i] <= now)
							{
								MySqlFuncs.MySqlSettings.CustomTimed.SetNextInterval(i, now);
								tokenParser.InputText = MySqlFuncs.MySqlSettings.CustomTimed.Commands[i];
								var cmd = tokenParser.ToStringFromString();
								LogDebugMessage("MySQLTimed: Running repeating - " + cmd);
								await MySqlFuncs.MySqlCommandAsync(cmd, $"CustomSqlTimed[{i}]");
							}
						}
					}
					catch (Exception ex)
					{
						LogExceptionMessage(ex, $"CustomSqlTimed[{i}]: Error excuting: {MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]} ");
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
					if (!string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i]))
					{
						tokenParser.InputText = MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i];
						// do not wait, just queue them up!
						_ = MySqlFuncs.MySqlCommandAsync(tokenParser.ToStringFromString(), "CustomMySqlStartUp");
					}
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, $"CustomSqlStartUp[{i}]: Error excuting: {MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i]} ");
				}
			}
			LogMessage("Custom start-up MySQL commands end");
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
				}
				LogDebugMessage("Disconnected Realtime FTP session");
			}
			catch (Exception ex)
			{
				LogDebugMessage("RealtimeFTPDisconnect: Error disconnecting connection (can be ignored?) - " + ex.Message);
			}
			finally
			{
				if (FtpOptions.FtpMode == FtpProtocols.SFTP && RealtimeSSH != null)
				{
					RealtimeSSH.Dispose();
				}
				else if (RealtimeFTP != null)
				{
					RealtimeFTP.Dispose();
				}
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

			RealtimeFTP = ftpClientFactory.CreateClient().Result;

			SetRealTimeFtpLogging(FtpOptions.Logging);

			if (FtpOptions.FtpMode == FtpProtocols.FTPS)
			{
				LogDebugMessage($"RealtimeFTPLogin: Using FTPS protocol");
			}
			else
			{
				LogDebugMessage($"RealtimeFTPLogin: Using FTP protocol");
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
				// dispose of the previous FTP client
				try
				{
					if (RealtimeSSH != null)
					{
						RealtimeSSH.Disconnect();
						RealtimeSSH.Dispose();
					}
				}
				catch
				{
					// do nothing
				}

				LogMessage($"RealtimeSSHLogin: Attempting realtime SFTP connect to host {FtpOptions.Hostname} on port {FtpOptions.Port}");
				try
				{

					RealtimeSSH = sftpClientFactory.CreateClient().Result;

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

		/*
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
		*/

		/*
		public async Task MySqlCommandAsync(string Cmd, string CallingFunction)
		{
			var Cmds = new ConcurrentQueue<SqlCache>();
			Cmds.Enqueue(new SqlCache() { statement = Cmd });
			await MySqlCommandAsync(Cmds, CallingFunction, false);
		}
		*/

		public void CreateUpdateSftpClientFactory()
		{
			if (sftpClientFactory != null)
			{
				sftpClientFactory.Host = FtpOptions.Hostname;
				sftpClientFactory.Port = FtpOptions.Port;
				sftpClientFactory.AuthMethod = FtpOptions.SshAuthen;
				sftpClientFactory.Username = FtpOptions.Username;
				sftpClientFactory.Password = FtpOptions.Password;
				sftpClientFactory.PskFile = FtpOptions.SshPskFile;
			}
			else
			{
				sftpClientFactory = new SftpClientFactory(
					host: FtpOptions.Hostname,
					port: FtpOptions.Port,
					authMethod: FtpOptions.SshAuthen,
					username: FtpOptions.Username,
					password: FtpOptions.Password,
					pskFile: FtpOptions.SshPskFile,
					dnsTtl: TimeSpan.FromMinutes(2)
				);
			}
		}

		public void CreateUpdateFtpClientFactory()
		{
			if (ftpClientFactory != null)
			{
				ftpClientFactory.Host = FtpOptions.Hostname;
				ftpClientFactory.Port = FtpOptions.Port;
				ftpClientFactory.Username = FtpOptions.Username;
				ftpClientFactory.Password = FtpOptions.Password;
				ftpClientFactory.Protocol = FtpOptions.FtpMode;
				ftpClientFactory.Autodetect = FtpOptions.AutoDetect;
				ftpClientFactory.DisableExplicit = FtpOptions.DisableExplicit;
				ftpClientFactory.ActiveMode = FtpOptions.ActiveMode;
				ftpClientFactory.DisableEpsv= FtpOptions.DisableEPSV;
				ftpClientFactory.IgnoreCertErrors = FtpOptions.IgnoreCertErrors;
			}
			else
			{
				ftpClientFactory = new FtpClientFactory(
					host: FtpOptions.Hostname,
					port: FtpOptions.Port,
					username: FtpOptions.Username,
					password: FtpOptions.Password,
					protocol: FtpOptions.FtpMode,
					autodetect: FtpOptions.AutoDetect,
					disableExplicit: FtpOptions.DisableExplicit,
					disableEpsv: FtpOptions.DisableEPSV,
					activeMode: FtpOptions.ActiveMode,
					ignoreCerts: FtpOptions.IgnoreCertErrors,
					dnsTtl: TimeSpan.FromMinutes(2)
				);
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

				if (retval.StatusCode != HttpStatusCode.OK)
				{
					LogWarningMessage("Error: Failed to get release information from GitHub, HTTP error: " + retval.StatusCode);
					LogDataMessage("Received: " + body);
					return;
				}

				if (body[0] != '[')
				{
					LogWarningMessage("Error: Failed to get release information from GitHub");
					LogDataMessage("Received: " + body);
					return;
				}

				var releases = JsonSerializer.Deserialize<List<GithubRelease>>(body);

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
						LogMessage($"This Cumulus MX beta instance is not running the latest beta version of Cumulsus MX, {latestBeta.name} is available.");
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
						var msg = $"You are not running the latest version of Cumulus MX, {latestLive.name} is available.";
						LogConsoleMessage(msg, ConsoleColor.Cyan);
						LogWarningMessage(msg);
						UpgradeAlarm.LastMessage = $"Release {latestLive.name} is available";
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
								LogDebugMessage($"CustomHttpMinutes[{i + 1}]: Querying - {processedString}");
								using var response = await MyHttpClient.GetAsync(processedString);
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpMinutes[{i + 1}]: Response code - {response.StatusCode}");
								LogDataMessage($"CustomHttpMinutes[{i + 1}]: Response text - {responseBodyAsText}");
							}
							else
							{
								Cumulus.LogConsoleMessage($"CustomHttpMinutes[{i + 1}]: Invalid URL - {CustomHttpMinutesStrings[i]}");
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
							if (CustomHttpRolloverStrings[i].StartsWith("http", StringComparison.OrdinalIgnoreCase))
							{
								var parser = new TokenParser(TokenParserOnToken)
								{
									InputText = CustomHttpRolloverStrings[i]
								};
								var processedString = parser.ToStringFromString();
								LogDebugMessage($"CustomHttpRollover[{i + 1}]: Querying - {processedString}");
								using var response = await MyHttpClient.GetAsync(processedString);
								var responseBodyAsText = await response.Content.ReadAsStringAsync();
								LogDebugMessage($"CustomHttpRollover[{i + 1}]: Response code - {response.StatusCode}");
								LogDataMessage($"CustomHttpRollover[{i + 1}]: Response text - {responseBodyAsText}");
							}
							else
							{
								Cumulus.LogConsoleMessage($"CustomHttpRollover[{i + 1}]: Invalid URL - {CustomHttpRolloverStrings[i]}");
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
			//Windy.AddToList(timestamp);
			PWS.AddToList(timestamp);
			//WOW.AddToList(timestamp)
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

		public void GetForecastTextFromFile()
		{

			string res = string.Empty;
			string fileName = Path.Combine(Directory.GetCurrentDirectory(), "forecast.txt");

			try
			{
				LogDebugMessage("GetForecastText: Reading - " + fileName);
				if (File.Exists(fileName))
				{
					using StreamReader streamReader = new StreamReader(fileName);
					res = streamReader.ReadToEnd();

					if (string.IsNullOrEmpty(res))
					{
						LogWarningMessage($"GetForecastText: MX configured to read \"{fileName}\" but the file is empty!");
					}
				}
				else
				{
					LogWarningMessage($"GetForecastText: MX configured to read \"{fileName}\" but the file does not exist");
				}
			}
			catch (Exception ex)
			{
				LogExceptionMessage(ex, "GetForecastText: Error processing file " + fileName);
			}

			MetData.ForecastStr = res;
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
			LogMessage($"TD={Spike.TempDiff:F3} GD={Spike.GustDiff:F3} WD={Spike.WindDiff:F3} HD={Spike.HumidityDiff:F3} PD={Spike.PressDiff:F3} MR={Spike.MaxRainRate:F3} MH={Spike.MaxHourlyRain:F3} ITD={Spike.InTempDiff:F3} IHD={Spike.InHumDiff:F3} Snow={Spike.SnowDiff:F2}");
			LogMessage("Limits:");
			LogMessage($"TH={Limit.TempHigh.ToString(TempFormat)} TL={Limit.TempLow.ToString(TempFormat)} DH={Limit.DewHigh.ToString(TempFormat)} GH={Limit.WindHigh:F3} SnowMinInc={SnowDepthMinInc:F2}");
			LogMessage($"PH={Limit.PressHigh.ToString(PressFormat)} PL={Limit.PressLow.ToString(PressFormat)} SPH={Limit.StationPressHigh.ToString(PressFormat)} SPL={Limit.StationPressLow.ToString(PressFormat)}");
		}

		private void LogPrimaryAqSensor()
		{
			switch (StationOptions.PrimaryAqSensor)
			{
				case (int) PrimaryAqSensor.Undefined:
					LogMessage("Primary AQ Sensor = Undefined");
					break;
				case (int) PrimaryAqSensor.Sensor1:
				case (int) PrimaryAqSensor.Sensor2:
				case (int) PrimaryAqSensor.Sensor3:
				case (int) PrimaryAqSensor.Sensor4:
					LogMessage("Primary AQ Sensor = AQ Sensor " + StationOptions.PrimaryAqSensor);
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
			var folders = new string[4] { ProgramOptions.BackupPath, Path.Combine(ProgramOptions.BackupPath, "daily"), ProgramOptions.DataPath, ProgramOptions.ReportsPath };

			LogMessage("Checking required folders");
			LogMessage(" Data path   : " + ProgramOptions.DataPath);
			LogMessage(" Backup path : " + ProgramOptions.BackupPath);
			LogMessage(" Reports path: " + ProgramOptions.ReportsPath);
			LogMessage(" Diags path  : " + ProgramOptions.DiagsPath);

			foreach (var folder in folders)
			{
				try
				{
					if (!Directory.Exists(folder))
					{
						LogMessage("Creating required folder: " + folder);
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
			if (LogManager.Configuration != null)
			{
				if (LogManager.Configuration.FindTargetByName("AsyncLogFileRT") != null)
				{
					LogManager.Configuration.RemoveTarget("AsyncLogFileRT");
				}

				if (LogManager.Configuration.FindTargetByName("AsyncLogFileIN") != null)
				{
					LogManager.Configuration.RemoveTarget("AsyncLogFileIN");
				}

				LogManager.ReconfigExistingLoggers();
			}

			if (!enable)
			{
				return;
			}

			// Create NLog configuration

			var layout = "${longdate}|${level}|${logger:shortName=true}|${message}";

			// Create targets for the log files
			var fileName = Path.Combine(Program.MxDiagsPath, "ftp-realtime.log");

			var logfileRT = new FileTarget("logfileRT")
			{
				FileName = fileName,
				ArchiveSuffixFormat = "{1:-yyMMdd-HHmmss}",
				ArchiveAboveSize = 5242880,
				ArchiveOldFileOnStartup = true,
				MaxArchiveFiles = 3,
				Layout = layout,
				Footer = "- - - - - - LOG CLOSED ${longdate} - - - - - -"
			};

			var asyncLogFileRT = new NLog.Targets.Wrappers.AsyncTargetWrapper(logfileRT)
			{
				Name = "AsyncLogFileRT",
				OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard,
				QueueLimit = 10000,
				BatchSize = 200,
				TimeToSleepBetweenBatches = 1
			};


			fileName = Path.Combine(Program.MxDiagsPath, "ftp-interval.log");

			var logfileIN = new FileTarget("logfileIN")
			{
				FileName = fileName,
				ArchiveSuffixFormat = "{1:-yyMMdd-HHmmss}",
				ArchiveAboveSize = 5242880,
				ArchiveOldFileOnStartup = true,
				MaxArchiveFiles = 3,
				Layout = layout,
				Footer = "- - - - - - LOG CLOSED ${longdate} - - - - - -"
			};

			var asyncLogFileIN = new NLog.Targets.Wrappers.AsyncTargetWrapper(logfileIN)
			{
				Name = "AsyncLogFileIN",
				OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard,
				QueueLimit = 10000,
				BatchSize = 200,
				TimeToSleepBetweenBatches = 1
			};

			//var config = new LoggingConfiguration();

			// Add targets to the configuration
			//config.AddTarget(asyncLogFileRT);
			//config.AddTarget(asyncLogFileIN);

			//LogManager.Configuration.AddTarget(logfileRT);
			//LogManager.Configuration.AddTarget(logfileIN);

			LogManager.Configuration.AddTarget(asyncLogFileRT);
			LogManager.Configuration.AddTarget(asyncLogFileIN);

			// Define rules for the loggers
			//config.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileRT, "FTPr.FTP");
			//config.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileRT, "CMXr.CMX");
			//config.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileIN, "FTPi.FTP");
			//config.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileIN, "CMXi.CMX");

			//LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, logfileRT, "FTPr.FTP");
			//LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, logfileRT, "CMXr.CMX");
			//LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, logfileIN, "FTPi.FTP");
			//LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, logfileIN, "CMXi.CMX");

			LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileRT, "FTPr.FTP");
			LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileRT, "CMXr.CMX");
			LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileIN, "FTPi.FTP");
			LogManager.Configuration.AddRule(NLog.LogLevel.FromOrdinal(FtpOptions.LoggingLevel), NLog.LogLevel.Fatal, asyncLogFileIN, "CMXi.CMX");

			// Apply configuration
			//LogManager.Configuration = config;

			var serviceProvider = new ServiceCollection()
				 .AddLogging(loggingBuilder =>
				 {
					 loggingBuilder.ClearProviders();
					 loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
					 loggingBuilder.AddNLog();
				 })
				 .BuildServiceProvider();

			FtpLoggerRT = serviceProvider.GetService<ILoggerFactory>().CreateLogger("FTPr.FTP");
			FtpLoggerMXRT = serviceProvider.GetService<ILoggerFactory>().CreateLogger("CMXr.CMX");
			FtpLoggerIN = serviceProvider.GetService<ILoggerFactory>().CreateLogger("FTPi.FTP");
			FtpLoggerMXIN = serviceProvider.GetService<ILoggerFactory>().CreateLogger("CMXi.CMX");
			// load the new config
			LogManager.ReconfigExistingLoggers();
		}

		private static StationManufacturer GetStationManufacturerFromType(int type)
		{
			return type switch
			{
				StationTypes.FineOffset or StationTypes.FineOffsetSolar or StationTypes.EasyWeather => StationManufacturer.EW,
				StationTypes.VantagePro or StationTypes.VantagePro2 or StationTypes.WLL or StationTypes.DavisCloudWll or StationTypes.DavisCloudVP2 => StationManufacturer.DAVIS,
				StationTypes.WMR928 or StationTypes.WM918 => StationManufacturer.OREGON,
				StationTypes.WMR200 or StationTypes.WMR100 => StationManufacturer.OREGONUSB,
				StationTypes.WS2300 => StationManufacturer.LACROSSE,
				StationTypes.Instromet => StationManufacturer.INSTROMET,
				StationTypes.GW1000 or StationTypes.HttpEcowitt or StationTypes.EcowittCloud or StationTypes.EcowittHttpApi => StationManufacturer.ECOWITT,
				StationTypes.Tempest => StationManufacturer.WEATHERFLOW,
				StationTypes.HttpWund => StationManufacturer.HTTPSTATION,
				StationTypes.HttpAmbient => StationManufacturer.AMBIENT,
				StationTypes.Simulator => StationManufacturer.SIMULATOR,
				StationTypes.JsonStation => StationManufacturer.JSONSTATION,
				_ => StationManufacturer.UNKNOWN,
			};
		}


		[GeneratedRegex(@"max[\s]*=[\s]*([\d]+)")]
		private static partial Regex regexMaxParam();

		[GeneratedRegex(@"[\\/]+\d{8}-\d{6}\.txt")]
		private static partial Regex regexLogFileName();

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
		public double? Snow24h { get; set; }
		public double? SnowDepth { get; set; }
		public bool Thunder { get; set; }
		public bool Hail { get; set; }
		public bool Fog { get; set; }
		public bool Gales { get; set; }

		public string ToCsvString()
		{
			var txt = new StringBuilder();
			txt.Append(Date.ToString("yyyy-MM-dd"));
			txt.Append(',');
			txt.Append(Time.ToString(@"hh\:mm"));
			txt.Append(',');
			txt.Append(SnowDepth.ToFixed("F1"));
			txt.Append(',');
			txt.Append(Snow24h.ToFixed("F1"));
			txt.Append(',');
			txt.Append(string.IsNullOrEmpty(Entry) ? string.Empty : $"\"{Entry}\"");
			txt.Append(',');
			txt.Append(Thunder ? 1 : 0);
			txt.Append(',');
			txt.Append(Hail ? 1 : 0);
			txt.Append(',');
			txt.Append(Fog ? 1 : 0);
			txt.Append(',');
			txt.Append(Gales ? 1 : 0);

			return txt.ToString();
		}

		public bool FromCSVString(string csv)
		{
			var parts = Utils.SplitCsv(csv);

			if (parts.Length < 9)
			{
				return false;
			}

			if (DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime dat))
			{
				if (DateTime.TryParseExact(parts[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime time))
				{
					Date = dat.Date;
					Time = time.TimeOfDay;
					SnowDepth = string.IsNullOrEmpty(parts[2]) ? null : double.Parse(parts[2], CultureInfo.InvariantCulture);
					Snow24h = string.IsNullOrEmpty(parts[3]) ? null : double.Parse(parts[3], CultureInfo.InvariantCulture);
					Entry = parts[4];
					Thunder = parts[5].Equals("1");
					Hail = parts[6].Equals("1");
					Fog = parts[7].Equals("1");
					Gales = parts[8].Equals("1");

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
		public string DisplayLanguage { get; set; }
		public string TimeFormat { get; set; }
		public string TimeFormatLong { get; set; }
		public bool TimeAmPmLowerCase { get; set; }
		public bool EncryptedCreds { get; set; }
		public bool SecureSettings { get; set; }
		public string SettingsUsername { get; set; }
		public string SettingsPassword { get; set; }
		public bool UseWebSockets { get; set; }
		public string DataPath { get; set; }
		public string BackupPath { get; set; }
		public string ReportsPath { get; set; }
		public string DiagsPath { get; set; }
		public int ProcessLogFilesLevel { get; set; }
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
		/// <value> 0=cm, 1=inch, 2=mm </value>
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
		public string[] AirQualityUnitText { get; set; } = new string[4];
		public string[] SoilMoistureUnitText { get; set; } = new string[16];
		public string CO2UnitText { get; set; }
		public string[] LeafWetnessUnitText { get; set; } = new string[8];

		public StationUnits()
		{
			Array.Fill(AirQualityUnitText, "µg/m³");
			Array.Fill(SoilMoistureUnitText, "cb");
			CO2UnitText = "ppm";
			Array.Fill(LeafWetnessUnitText, string.Empty);  // Davis is unitless, Ecowitt uses %
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
		public string TimeZoneId { get; set; }
	}

	public class SensorMaps
	{
		/// <summary>
		/// 0 - Default
		/// 1-16 - Extra TH
		/// 99 - Indoor
		/// </summary>
		public int PrimaryTempHum;
		/// <summary>
		/// 0 - Default
		/// 1-16 Extra TH
		/// </summary>
		public int PrimaryIndoorTempHum;
		public int IndoorTemp;
		public int IndoorHum;
		public int Temperature;
		public int DewPoint;
		public int Humidity;
		public int Wind;
		public int Pressure;
		public int Rain;
		public bool SolarEnabled;
		public int Solar;
		public bool UVEnabled;
		public int UV;
		public bool ExtraTempHumEnabled;
		public int[] ExtraTempHum = new int[16];
		public bool UserTempEnabled;
		public int[] UserTemp = new int[16];
		public bool SoilTempEnabled;
		public int[] SoilTemp = new int[16];
		public bool SoilMoistEnabled;
		public int[] SoilMoist = new int[16];
		public bool SoilEcEnabled;
		public int[] SoilEc = new int[16];
		public bool LeafWetEnabled;
		public int[] LeafWet = new int[8];
		public bool AirQualEnabled;
		public int[] AirQual = new int[4];
		public bool LightningEnabled;
		public int Lightning;
		public bool LaserDistEnabled;
		public int[] LaserDist = new int[4];
		public bool BlackGlobeEnabled;
		public int BlackGlobe;
		public bool CO2Enabled;
		public int CO2;
		public bool CameraEnabled;
		public int Camera;
		public bool LeakEnabled;
		public int[] Leak = new int[4];
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
		/// <summary>
		/// FTP=0
		/// FTPS=1
		/// SFTP=2
		/// PHP=3
		/// </summary>
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
		public string FileName { get; set; }
		public string LocalPath { get; set; }
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
		public bool CloudBroadcasts { get; set; }
	}


	public class JsonStationOptions
	{
		/// <value>0=file, 1=HTTP, 2=MQTT</value>
		public int Connectiontype { get; set; }
		public string SourceFile { get; set; }
		public int FileReadDelay { get; set; }
		public int FileIgnoreTime { get; set; }
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
		public string WFSerialNo { get; set; }

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
		public bool UseSunshineSensor { get; set; }
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
		public string fromDate { get; set; }
		public string toDate { get; set; }

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
		public MySqlTableIntervalSettings CustomRollover { get; set; }
		public MySqlTableTimedSettings CustomTimed { get; set; }
		public MySqlTableSettings CustomStartUp { get; set; }


		public MySqlGeneralSettings()
		{
			Realtime = new MySqlTableSettings();
			Monthly = new MySqlTableSettings();
			Dayfile = new MySqlTableSettings();
			CustomSecs = new MySqlTableSettings();
			CustomMins = new MySqlTableIntervalSettings();
			CustomRollover = new MySqlTableIntervalSettings();
			CustomTimed = new MySqlTableTimedSettings();
			CustomStartUp = new MySqlTableSettings();

			CustomSecs.Commands = new string[10];
			CustomMins.Commands = new string[10];
			CustomMins.IntervalIndexes = new int[10];
			CustomMins.Intervals = new int[10];
			CustomMins.CatchUp = new bool[10];
			CustomRollover.Commands = new string[10];
			CustomRollover.CatchUp = new bool[10];
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
		public bool[] CatchUp { get; set; }
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
			if (NextUpdate[idx] <= now)
			{
				do
				{
					NextUpdate[idx] = NextUpdate[idx].AddMinutes(Intervals[idx]);
				} while (NextUpdate[idx] <= now);
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
		public bool OutputText { get; set; }
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

	public enum RealtimeFileIdx
	{
		REALTIME_TXT,        // 0
		REALTIMEGAUGES_TXT  // 1
	}

	public enum GraphFileIdx
	{
		CONFIG,         // 0
		AVAILABLE,      // 1
		TEMP,           // 2
		PRESS,          // 3
		WIND,           // 4
		WINDDIR,        // 5
		HUM,            // 6
		RAIN,           // 7
		DAILYRAIN,      // 8
		DAILYTEMP,      // 9
		SOLAR,          // 10
		SUNHOURS,       // 11
		AIRQUAL,        // 12
		EXTRATEMP,      // 13
		EXTRAHUM,       // 14
		EXTRADEW,       // 15
		SOILTEMP,       // 16
		SOILMOIST,      // 17
		USERTEMP,       // 18
		CO2,            // 19
		LEAFWET,        // 20
		LASERDEPTH,     // 21
		SNOW24H,        // 22
		SOILEC          //23
	}

}
