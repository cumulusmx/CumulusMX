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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Devart.Data.MySql;
using FluentFTP;
using LinqToTwitter;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
using Unosquare.Labs.EmbedIO.Constants;
using Timer = System.Timers.Timer;
using SQLite;
using Renci.SshNet;

//using MQTTnet;

namespace CumulusMX
{
	public class Cumulus
	{
		/////////////////////////////////
		public string Version = "3.8.4";
		public string Build = "3094";
		/////////////////////////////////

		public static SemaphoreSlim syncInit = new SemaphoreSlim(1);

		public enum VPRainGaugeTypes
		{
			MM = 0,
			IN = 1
		}

		public enum VPConnTypes
		{
			Serial = 0,
			TCPIP = 1
		}

		public enum pressunits
		{
			MB,
			HPA,
			IN
		}

		public enum windunits
		{
			MS,
			MPH,
			KPH,
			KNOTS
		}

		public enum tempunits
		{
			C,
			F
		}

		public enum rainunits
		{
			MM,
			IN
		}

		public enum solarcalcTypes
		{
			RyanStolzenbach = 0,
			Bras = 1
		}

		public enum FtpProtocols
		{
			FTP = 0,
			FTPS = 1,
			SFTP = 2
		}

		private readonly string[] SshAuthenticationVals = { "password", "psk", "password_psk" };

		public struct Dataunits
		{
			public pressunits pressunit;
			public windunits windunit;
			public tempunits tempunit;
			public rainunits rainunit;
		}

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

		private WeatherStation station;

		private readonly StationSettings stationSettings;
		private readonly InternetSettings internetSettings;
		private readonly CalibrationSettings calibrationSettings;
		private readonly NOAASettings noaaSettings;
		private readonly AlarmSettings alarmSettings;
		private readonly MysqlSettings mySqlSettings;
		private readonly DataEditor dataEditor;
		private readonly ApiTagProcessor tagProcessor;

		public DateTime LastUpdateTime;

		private readonly WebTags webtags;
		private readonly TokenParser tokenParser;
		private readonly TokenParser realtimeTokenParser;
		private readonly TokenParser customMysqlSecondsTokenParser = new TokenParser();
		private readonly TokenParser customMysqlMinutesTokenParser = new TokenParser();
		private readonly TokenParser customMysqlRolloverTokenParser = new TokenParser();

		public string CurrentActivity = "Stopped";

		private static readonly TraceListener ftpTraceListener = new TextWriterTraceListener("ftplog.txt", "ftplog");

		/// <summary>
		/// Temperature unit currently in use
		/// </summary>
		public string TempUnitText;

		/// <summary>
		/// Temperature trend unit in use, eg "°C/hr"
		/// </summary>
		public string TempTrendUnitText;

		public string RainUnitText;

		public string RainTrendUnitText;

		public string PressUnitText;

		public string PressTrendUnitText;

		public string WindUnitText;

		public string WindRunUnitText;

		public string AirQualityUnitText = "µg/m³";
		public string SoilMoistureUnitText = "cb";

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
		public int[] rise_options = { 25, 25, 25, 24, 24, 19, 16, 12, 11, 9, 8, 6, 5, 2, 1, 1, 0, 0, 0, 0, 0, 0 };
		public int[] steady_options = { 25, 25, 25, 25, 25, 25, 23, 23, 22, 18, 15, 13, 10, 4, 1, 1, 0, 0, 0, 0, 0, 0 };
		public int[] fall_options = { 25, 25, 25, 25, 25, 25, 25, 25, 23, 23, 21, 20, 17, 14, 7, 3, 1, 1, 1, 0, 0, 0 };

		internal int[] FactorsOf60 = { 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60 };

		public bool UseWind10MinAve { get; set; }
		public bool UseSpeedForAvgCalc { get; set; }
		public bool UseZeroBearing { get; set; }
		public int MaxWindRecent = 720;
		public TimeSpan AvgSpeedTime { get; set; }
		public int AvgSpeedMinutes = 10;

		public int PeakGustMinutes = 10;
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

		private readonly int WindRunDPlaces = 1;
		public string WindRunFormat;

		public int RainDPlaces = 1;
		public string RainFormat;

		internal int PressDPlaces = 1;
		internal bool DavisIncrementPressureDP;
		public string PressFormat;

		internal int SunshineDPlaces = 1;
		public string SunFormat;

		internal int UVDPlaces = 1;
		public string UVFormat;

		public string ETFormat;

		public int VPrainGaugeType = -1;

		public string ComportName;
		public string DefaultComportName;
		public int ImetBaudRate;
		public int DavisBaudRate;

		public int VendorID;
		public int ProductID;

		public string IPaddress;

		public int TCPport;

		public VPConnTypes VPconntype;

		public string Platform;

		public string dbfile;
		public SQLiteConnection LogDB;

		public string diaryfile;
		public SQLiteConnection DiaryDB;

		public string Datapath;

		public string ListSeparator;
		public char DirectorySeparator;

		public bool ImportData;
		public string CumulusIniPath;

		public int LogInterval = 5;

		public bool LogExtraData;

		public int RolloverHour;
		public bool Use10amInSummer;

		public double Latitude = 0;
		public double Longitude = 0;
		public double Altitude = 0;

		public double RStransfactor = 0.8;

		private readonly int HttpPort = 8998;
		internal int wsPort = 8998;
		private readonly bool DebuggingEnabled = false;

		public bool LogExtraSensors = false;

		public bool UseDavisLoop2 = true;
		public bool DavisReadReceptionStats = false;
		public int DavisReadTimeout;

		public SerialPort cmprtRG11;
		public SerialPort cmprt2RG11;

		private const int DefaultWebUpdateInterval = 15;
		private const int DefaultWundInterval = 15;
		private const int DefaultWindyInterval = 15;
		private const int DefaultPWSInterval = 15;
		private const int DefaultAPRSInterval = 9;
		private const int DefaultAwekasInterval = 15 * 60;
		private const int DefaultWCloudInterval = 10;

		public int RecordSetTimeoutHrs = 24;

		private const int VP2SERIALCONNECTION = 0;
		//private const int VP2USBCONNECTION = 1;
		//private const int VP2TCPIPCONNECTION = 2;

		private readonly string twitterKey = "lQiGNdtlYUJ4wS3d7souPw";
		private readonly string twitterSecret = "AoB7OqimfoaSfGQAd47Hgatqdv3YeTTiqpinkje6Xg";

		public int FineOffsetReadTime;

		//private readonly string AlltimeFile;
		public string AlltimeIniFile;
		public string Alltimelogfile;
		public string MonthlyAlltimeIniFile;
		public string MonthlyAlltimeLogFile;
		private readonly string LogFilePath;
		public string DayFile;
		public string YesterdayFile;
		public string TodayIniFile;
		public string MonthIniFile;
		public string YearIniFile;
		//private readonly string stringsFile;
		private readonly string Backuppath;
		private readonly string WebPath;
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
		public bool SynchronisedWindyUpdate;
		public bool SynchronisedAwekasUpdate;
		public bool SynchronisedWCloudUpdate;
		public bool SynchronisedWOWUpdate;
		public bool SynchronisedPWSUpdate;
		public bool SynchronisedTwitterUpdate;
		public bool SynchronisedWBUpdate;
		public bool SynchronisedAPRSUpdate;

		private List<string> WundList = new List<string>();
		private List<string> WindyList = new List<string>();
		private List<string> PWSList = new List<string>();
		private List<string> WOWList = new List<string>();

		private List<string> MySqlList = new List<string>();

		// Calibration settings
		/// <summary>
		/// User pressure calibration
		/// </summary>
		public double PressOffset = 0.0;
		public double TempOffset = 0.0;
		public int HumOffset = 0;
		public int WindDirOffset = 0;
		public double InTempoffset = 0.0;
		public double SolarOffset = 0.0;
		public double UVOffset = 0.0;
		public double WetBulbOffset = 0.0;

		public double PressMult = 1.0;
		public double WindSpeedMult = 1.0;
		public double WindGustMult = 1.0;
		public double TempMult = 1.0;
		public double TempMult2 = 0.0;
		public double HumMult = 1.0;
		public double HumMult2 = 0.0;
		public double RainMult = 1.0;
		public double SolarMult = 1.0;
		public double UVMult = 1.0;
		public double WetBulbMult = 1.0;

		public double LimitTempHigh;
		public double LimitTempLow;
		public double LimitDewHigh;
		public double LimitPressHigh;
		public double LimitPressLow;
		public double LimitWindHigh;

		public GraphOptions GraphOptions = new GraphOptions();

		//private int CurrentYear;
		//private int CurrentMonth;
		//private int CurrentDay;

		public bool ListWebTags;

		public bool RealtimeEnabled; // The timer is to be started
		public bool RealtimeFTPEnabled; // The FTP connection is to be established
		public bool RealtimeTxtFTP; // The realtime.txt file is to be uploaded
		public bool RealtimeGaugesTxtFTP; // The realtimegauges.txt file is to be uploaded
		private int RealtimeFTPRetries; // Count of failed realtime FTP attempts

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
		//private bool WundHTTPLogging = false;
		public bool SendUVToWund = false;
		public bool SendSRToWund = false;
		public bool SendIndoorToWund = false;
		public bool WundSendAverage = false;
		public bool WundCatchUp = true;
		public bool WundCatchingUp = false;

		// Windy.com settings
		public string WindyApiKey = " ";
		public int WindyStationIdx = 0;
		public bool WindyEnabled = false;
		public int WindyInterval = 15;
		//private bool WindyHTTPLogging = false;
		public bool WindySendUV = false;
		public bool WindySendSolar = false;
		public bool WindyCatchUp = true;
		public bool WindyCatchingUp = false;

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
		public bool AwekasRateLimited = false;
		public int AwekasOriginalInterval;
		public string AwekasLang = "en";
		public bool SendUVToAwekas;
		public bool SendSolarToAwekas;
		public bool SendSoilTempToAwekas;
		public bool SendIndoorToAwekas;
		public bool SendSoilMoistureToAwekas;
		public bool SendLeafWetnessToAwekas;

		// WeatherCloud settings
		public string WCloudWid = " ";
		public string WCloudKey = " ";
		public bool WCloudEnabled = false;
		public int WCloudInterval = DefaultWCloudInterval;
		public bool SendUVToWCloud;
		public bool SendSolarToWCloud;

		// MQTT settings
		public string MQTTServer;
		public int MQTTPort;
		public int MQTTIpVersion;
		public bool MQTTUseTLS;
		public string MQTTUsername;
		public string MQTTPassword;
		public bool MQTTEnableDataUpdate;
		public string MQTTUpdateTopic;
		public string MQTTUpdateTemplate;
		public bool MQTTEnableInterval;
		public int MQTTIntervalTime;
		public string MQTTIntervalTopic;
		public string MQTTIntervalTemplate;

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

		public bool EODfilesNeedFTP = false;

		public bool IsOSX;

		private const double DEFAULTFCLOWPRESS = 950.0;
		private const double DEFAULTFCHIGHPRESS = 1050.0;

		private const string ForumDefault = "https://cumulus.hosiene.co.uk/";

		private const string WebcamDefault = "";

		private const string DefaultSoundFile = "alarm.mp3";
		private const string DefaultSoundFileOld = "alert.wav";

		public int RealtimeInterval;

		public string ForecastNotAvailable;

		public WebServer httpServer;
		//public WebSocket websock;

		//private Thread httpThread;

		private static readonly HttpClientHandler WUhttpHandler = new HttpClientHandler();
		private readonly HttpClient WUhttpClient = new HttpClient(WUhttpHandler);
		private bool UpdatingWU = false;

		private static readonly HttpClientHandler WindyhttpHandler = new HttpClientHandler();
		private readonly HttpClient WindyhttpClient = new HttpClient(WindyhttpHandler);
		private bool UpdatingWindy = false;

		private static readonly HttpClientHandler AwekashttpHandler = new HttpClientHandler();
		private readonly HttpClient AwekashttpClient = new HttpClient(AwekashttpHandler);
		private bool UpdatingAwekas = false;

		private static readonly HttpClientHandler WCloudhttpHandler = new HttpClientHandler();
		private readonly HttpClient WCloudhttpClient = new HttpClient(WCloudhttpHandler);
		private bool UpdatingWCloud = false;

		private static readonly HttpClientHandler PWShttpHandler = new HttpClientHandler();
		private readonly HttpClient PWShttpClient = new HttpClient(PWShttpHandler);
		private bool UpdatingPWS = false;

		private static readonly HttpClientHandler WOWhttpHandler = new HttpClientHandler();
		private readonly HttpClient WOWhttpClient = new HttpClient(WOWhttpHandler);
		private bool UpdatingWOW = false;

		// Custom HTTP - seconds
		private static readonly HttpClientHandler customHttpSecondsHandler = new HttpClientHandler();
		private readonly HttpClient customHttpSecondsClient = new HttpClient(customHttpSecondsHandler);
		private bool updatingCustomHttpSeconds = false;
		private readonly TokenParser customHttpSecondsTokenParser = new TokenParser();
		internal Timer CustomHttpSecondsTimer;
		internal bool CustomHttpSecondsEnabled;
		internal string CustomHttpSecondsString;
		internal int CustomHttpSecondsInterval;

		// Custom HTTP - minutes
		private static readonly HttpClientHandler customHttpMinutesHandler = new HttpClientHandler();
		private readonly HttpClient customHttpMinutesClient = new HttpClient(customHttpMinutesHandler);
		private bool updatingCustomHttpMinutes = false;
		private readonly TokenParser customHttpMinutesTokenParser = new TokenParser();
		internal bool CustomHttpMinutesEnabled;
		internal string CustomHttpMinutesString;
		internal int CustomHttpMinutesInterval;
		internal int CustomHttpMinutesIntervalIndex;

		// Custom HTTP - rollover
		private static readonly HttpClientHandler customHttpRolloverHandler = new HttpClientHandler();
		private readonly HttpClient customHttpRolloverClient = new HttpClient(customHttpRolloverHandler);
		private bool updatingCustomHttpRollover = false;
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
		public string MySqlHost;
		public int MySqlPort;
		public string MySqlUser;
		public string MySqlPass;
		public string MySqlDatabase;

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
			"LaCrosse WS2300", "Fine Offset with Solar", "Oregon Scientific WMR100", "Oregon Scientific WMR200", "Instromet", "Davis WLL", "GW1000"
		};

		public string[] APRSstationtype = { "DsVP", "DsVP", "WMR928", "WM918", "EW", "FO", "WS2300", "FOs", "WMR100", "WMR200", "Instromet", "DsVP", "Ecowitt" };

		public string loggingfile;

		/*
		CryptoLicense lic = new CryptoLicense();

		//create code for applicationsecret
		byte[] applicationSecret = Convert.FromBase64String("QpJGpsqWfkKu+yM8Ljp6+A==");
		//create code for public key
		byte[] publicKey = Convert.FromBase64String("BgIAAACkAABSU0ExAAIAAAEAAQBlt7KZEJ8lk7Pa+MSYzToupycyYtGKNmSBYEb2UTiGpDxsxH8vzGDyWv5ytW1qlaPwaVeJLtagn7/mep/Yr16m");
		private Habanero.Licensing.Validation.LicenseValidator Validator
		{
			get
			{
				//this version is for file system - Isolated storage is anther option
				return new Habanero.Licensing.Validation.LicenseValidator(Habanero.Licensing.Validation.LicenseLocation.File, "licence.lic", "Cumulus MX", publicKey, applicationSecret, ThisVersion);
			}
		}

		private static Version ThisVersion
		{
			get
			{
				//Get the executing files filesversion
				var fileversion = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
				var thisVersion = new Version(fileversion.FileMajorPart, fileversion.FileMinorPart, fileversion.FileBuildPart, fileversion.FilePrivatePart);

				return thisVersion;
			}
		}

		private void DoLicenseCheck()
		{
			LicenseValidationResult result = Validator.CheckLicense();
			if ((result.License.Product.LicenseName != null) && (result.License.Product.ProductName != null) && (result.License.LicensedTo != null) && (result.License.Product.MaxVersion != null))
			{
				Console.WriteLine(result.License.Product.LicenseName+" licence for "+result.License.Product.ProductName+" " + result.License.Product.MaxVersion +
				" for user "+result.License.LicensedTo);
			}

			if (result.ExpirationDate != null)
			{
				Console.WriteLine("Licence expiry date: "+result.ExpirationDate.Value.ToString("D"));
			}

			if (result.State == LicenseState.Invalid)
			{
				if (result.Issues.Contains(LicenseIssue.NoLicenseInfo))
				{
					//inform user there is no license info
					Console.WriteLine("No licence information, please obtain a licence");
					Environment.Exit(0);
				}
				else
				{
					if (result.Issues.Contains(LicenseIssue.ExpiredDateSoft))
					{
						//inform user that their license has expired but
						//that they may continue using the software for a period
						Console.WriteLine("Licence expired, please obtain a licence");
						Environment.Exit(0);
					}
					if (result.Issues.Contains(LicenseIssue.ExpiredDateHard))
					{
						//inform user that their license has expired
						Console.WriteLine("Licence expired, please obtain a licence");
						Environment.Exit(0);
					}
					if (result.Issues.Contains(LicenseIssue.ExpiredVersion))
					{
						//inform user that their license is for an earlier version
						Console.WriteLine("Licence is for an earlier version, please obtain a new licence");
						Environment.Exit(0);
					}
					//other messages
				}

				//prompt user for trial or to insert license info then decide what to do
				//activate trial
				result = Validator.ActivateTrial(45);
				//or save license
				string userLicense = "Get the license string from your user";
				result = Validator.CheckLicense(userLicense);
				//decide if you want to save the license...
				Validator.SaveLicense(userLicense);
			}
			if (result.State == LicenseState.Trial)
			{
				//activate trial features
				Console.WriteLine("Trial licence is valid");
			}
			if (result.State == LicenseState.Valid)
			{
				//activate product
				if (Validator.IsEdition("Pro"))
				{
					//activate pro features...
				}

				Console.WriteLine("Licence is valid");
			}
		}
		*/

		public Cumulus(int HTTPport, bool DebugEnabled, string startParms)
		{
			//DoLicenseCheck();

			/*lic.ValidationKey = "AMAAMACrfxYrYEOGd+D5ypZ32bnLCvviBrTlejReXNRdvgWzSgyvdfkLvNDvDX1WuMh2JIEDAAEAAQ==";

			// Load license from the file
			lic.StorageMode = LicenseStorageMode.ToFile;
			if (lic.Load("licence.lic") == false)
				throw new Exception("License could not be loaded");

			// Validate the license using .Status property
			if (lic.Status != LicenseStatus.Valid)
				throw new Exception("license validation failed");
			*/



			//string serial = CalculateMD5Hash(Environment.MachineName);
			//Console.WriteLine("Serial: " + serial);
			//File.WriteAllText("serial.txt", serial);
			/*
						try
						{
							using (TextReader reader = File.OpenText(@"licence.lic"))
							{
								var license = License.Load(reader);

								const string publicKey =
									"MIIBKjCB4wYHKoZIzj0CATCB1wIBATAsBgcqhkjOPQEBAiEA/////wAAAAEAAAAAAAAAAAAAAAD///////////////8wWwQg/////wAAAAEAAAAAAAAAAAAAAAD///////////////wEIFrGNdiqOpPns+u9VXaYhrxlHQawzFOw9jvOPD4n0mBLAxUAxJ02CIbnBJNqZnjhE50mt4GffpAEIQNrF9Hy4SxCR/i85uVjpEDydwN9gS3rM6D0oTlF2JjClgIhAP////8AAAAA//////////+85vqtpxeehPO5ysL8YyVRAgEBA0IABPhtz1xOwoXzDwPvV3Yv9p9vW2UUr3izy8yG6D2ptqAMJzFuu++47B6vckx+uY9Qw3e3VAR4NDePg4xE3KVdN6Y=";

								var validationFailures = license.Validate().ExpirationDate().When(lic => lic.Type == LicenseType.Trial).And().Signature(publicKey).AssertValidLicense().ToList();

								if (validationFailures.Any())
								{
									var messageBuilder = new StringBuilder();
									foreach (var validationFailure in validationFailures)
									{
										messageBuilder.AppendLine("Failure:");
										messageBuilder.AppendLine(validationFailure.Message);
										messageBuilder.AppendLine(" ");
										messageBuilder.AppendLine("Resolve issue by:");
										messageBuilder.AppendLine(validationFailure.HowToResolve);
										messageBuilder.AppendLine(" ");
										messageBuilder.AppendLine(" ");
									}

									Console.WriteLine(messageBuilder);
									Environment.Exit(0);
								}

								if (license.AdditionalAttributes.Contains("serial") && (license.AdditionalAttributes.Get("serial") != serial))
								{
									Console.WriteLine("Cumulus is not licensed on this machine. Please contact licensing@sandaysoft.com");
									Environment.Exit(0);
								}

								if (license.Type == LicenseType.Trial)
								{
									Console.WriteLine("Trial licence, expiry date " + license.Expiration.ToLongDateString());
								}
							}
						}
						catch (Exception)
						{
							Console.WriteLine("An error occurred while reading the licence file");
							Environment.Exit(0);
						}
			*/

			DirectorySeparator = Path.DirectorySeparatorChar;

			AppDir = Directory.GetCurrentDirectory() + DirectorySeparator;
			TwitterTxtFile = AppDir + "twitter.txt";
			WebTagFile = AppDir + "WebTags.txt";

			// interface port passed as param
			HttpPort = HTTPport;

			//b3045>, use same port for WS...  WS port = HTTPS port
			//wsPort = WSport;
			wsPort = HTTPport;

			DebuggingEnabled = DebugEnabled;

			// Set up the diagnostic tracing
			loggingfile = GetLoggingFileName("MXdiags" + DirectorySeparator);

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

			LogMessage("OS version: " + Environment.OSVersion.ToString());

			GetLatestVersion();

			Type type = Type.GetType("Mono.Runtime");
			if (type != null)
			{
				MethodInfo displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
				if (displayName != null)
					LogMessage("Mono version: "+displayName.Invoke(null, null));
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
			Backuppath = "backup" + DirectorySeparator;
			ReportPath = "Reports" + DirectorySeparator;
			WebPath = "web" + DirectorySeparator;

			dbfile = Datapath + "cumulusmx.db";
			diaryfile = Datapath + "diary.db";

			//AlltimeFile = Datapath + "alltime.rec";
			AlltimeIniFile = Datapath + "alltime.ini";
			Alltimelogfile = Datapath + "alltimelog.txt";
			MonthlyAlltimeIniFile = Datapath + "monthlyalltime.ini";
			MonthlyAlltimeLogFile = Datapath + "monthlyalltimelog.txt";
			LogFilePath = Datapath;
			DayFile = Datapath + "dayfile.txt";
			YesterdayFile = Datapath + "yesterday.ini";
			TodayIniFile = Datapath + "today.ini";
			MonthIniFile = Datapath + "month.ini";
			YearIniFile = Datapath + "year.ini";
			//stringsFile = "strings.ini";

			IndexTFile = WebPath + "indexT.htm";
			TodayTFile = WebPath + "todayT.htm";
			YesterdayTFile = WebPath + "yesterdayT.htm";
			RecordTFile = WebPath + "recordT.htm";
			TrendsTFile = WebPath + "trendsT.htm";
			GaugesTFile = WebPath + "gaugesT.htm";
			ThisMonthTFile = WebPath + "thismonthT.htm";
			ThisYearTFile = WebPath + "thisyearT.htm";
			MonthlyRecordTFile = WebPath + "monthlyrecordT.htm";
			RealtimeGaugesTxtTFile = WebPath + "realtimegaugesT.txt";

			Indexfile = WebPath + "index.htm";
			Todayfile = WebPath + "today.htm";
			Yesterfile = WebPath + "yesterday.htm";
			Recordfile = WebPath + "record.htm";
			Trendsfile = WebPath + "trends.htm";
			Gaugesfile = WebPath + "gauges.htm";
			ThisMonthfile = WebPath + "thismonth.htm";
			ThisYearfile = WebPath + "thisyear.htm";
			MonthlyRecordfile = WebPath + "monthlyrecord.htm";
			RealtimeGaugesTxtFile = WebPath + "realtimegauges.txt";

			localwebtextfiles = new[] { Indexfile, Todayfile, Yesterfile, Recordfile, Trendsfile, Gaugesfile, ThisMonthfile, ThisYearfile, MonthlyRecordfile };
			remotewebtextfiles = new[] { "index.htm", "today.htm", "yesterday.htm", "record.htm", "trends.htm", "gauges.htm", "thismonth.htm", "thisyear.htm", "monthlyrecord.htm" };

			//localgraphdatafiles = new[] {"units.json","tempdatad3.json", "pressdatad3.json", "winddatad3.json", "wdirdatad3.json", "humdatad3.json", "raindatad3.json", "solardatad3.json"};
			//remotegraphdatafiles = new[] {"units.json","tempdatad3.json", "pressdatad3.json", "winddatad3.json", "wdirdatad3.json", "humdatad3.json", "raindatad3.json", "solardatad3.json"};

			ReadIniFile();

			if (WarnMultiple && !Program.appMutex.WaitOne(0, false))
			{
				LogConsoleMessage("Cumulus is already running - terminating");
				LogConsoleMessage("Program exit");
				LogMessage("Cumulus is already running - terminating");
				LogMessage("Program exit");
				Environment.Exit(1);
			}

			GC.Collect();

			localgraphdatafiles = new[]
				{
					"web" + DirectorySeparator + "graphconfig.json",
					"web" + DirectorySeparator + "tempdata.json",
					"web" + DirectorySeparator + "pressdata.json",
					"web" + DirectorySeparator + "winddata.json",
					"web" + DirectorySeparator + "wdirdata.json",
					"web" + DirectorySeparator + "humdata.json",
					"web" + DirectorySeparator + "raindata.json",
					"web" + DirectorySeparator + "solardata.json",
					"web" + DirectorySeparator + "dailyrain.json",
					"web" + DirectorySeparator + "sunhours.json",
					"web" + DirectorySeparator + "dailytemp.json"
				};

			remotegraphdatafiles = new[]
				{
					"graphconfig.json", "tempdata.json", "pressdata.json", "winddata.json", "wdirdata.json", "humdata.json", "raindata.json", "solardata.json",
					"dailyrain.json", "sunhours.json", "dailytemp.json"
				};

			LogMessage("Data path = " + Datapath);

			AppDomain.CurrentDomain.SetData("DataDirectory", Datapath);

			// Open database (create file if it doesn't exist)
			SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
			LogDB = new SQLiteConnection(dbfile, flags);

			LogDB.CreateTable<StandardData>();

			// Open diary database (create file if it doesn't exist)
			DiaryDB = new SQLiteConnection(diaryfile, flags);

			DiaryDB.CreateTable<DiaryData>();

			Backupdata(false, DateTime.Now);

			LogMessage("Debug logging is " + (logging ? "enabled" : "disabled"));
			LogMessage("Data logging is " + (DataLogging ? "enabled" : "disabled"));
			LogMessage("FTP logging is " + (FTPlogging ? "enabled" : "disabled"));
			LogMessage("Spike logging is " + (ErrorLogSpikeRemoval ? "enabled" : "disabled"));
			LogMessage("Logging interval = " + logints[DataLogInterval] + " mins");
			LogMessage("Real time interval = " + RealtimeInterval / 1000 + " secs");
			LogMessage("NoSensorCheck = " + (NoSensorCheck ? "1" : "0"));

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
					RealtimeFTP.ValidateCertificate += Client_ValidateCertificate;
					// b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specifiy protocols
					RealtimeFTP.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
				}
			}

			ReadStringsFile();

			SetUpHttpProxy();

			if (MonthlyMySqlEnabled)
			{
				MonthlyMySqlConn.Host = MySqlHost;
				MonthlyMySqlConn.Port = MySqlPort;
				MonthlyMySqlConn.UserId = MySqlUser;
				MonthlyMySqlConn.Password = MySqlPass;
				MonthlyMySqlConn.Database = MySqlDatabase;

				SetStartOfMonthlyInsertSQL();
			}

			if (DayfileMySqlEnabled)
			{
				SetStartOfDayfileInsertSQL();
			}

			if (RealtimeMySqlEnabled)
			{
				RealtimeSqlConn.Host = MySqlHost;
				RealtimeSqlConn.Port = MySqlPort;
				RealtimeSqlConn.UserId = MySqlUser;
				RealtimeSqlConn.Password = MySqlPass;
				RealtimeSqlConn.Database = MySqlDatabase;

				SetStartOfRealtimeInsertSQL();

				if (!string.IsNullOrEmpty(MySqlRealtimeRetention))
				{
					DeleteRealtimeSQL = $"DELETE IGNORE FROM {MySqlRealtimeTable} WHERE LogDateTime < DATE_SUB(NOW(), INTERVAL {MySqlRealtimeRetention})";
				}
			}

			CustomMysqlSecondsConn.Host = MySqlHost;
			CustomMysqlSecondsConn.Port = MySqlPort;
			CustomMysqlSecondsConn.UserId = MySqlUser;
			CustomMysqlSecondsConn.Password = MySqlPass;
			CustomMysqlSecondsConn.Database = MySqlDatabase;
			customMysqlSecondsTokenParser.OnToken += TokenParserOnToken;
			CustomMysqlSecondsCommand.Connection = CustomMysqlSecondsConn;
			CustomMysqlSecondsTimer = new Timer { Interval = CustomMySqlSecondsInterval * 1000 };
			CustomMysqlSecondsTimer.Elapsed += CustomMysqlSecondsTimerTick;
			CustomMysqlSecondsTimer.AutoReset = true;

			CustomMysqlMinutesConn.Host = MySqlHost;
			CustomMysqlMinutesConn.Port = MySqlPort;
			CustomMysqlMinutesConn.UserId = MySqlUser;
			CustomMysqlMinutesConn.Password = MySqlPass;
			CustomMysqlMinutesConn.Database = MySqlDatabase;
			customMysqlMinutesTokenParser.OnToken += TokenParserOnToken;
			CustomMysqlMinutesCommand.Connection = CustomMysqlMinutesConn;

			CustomMysqlRolloverConn.Host = MySqlHost;
			CustomMysqlRolloverConn.Port = MySqlPort;
			CustomMysqlRolloverConn.UserId = MySqlUser;
			CustomMysqlRolloverConn.Password = MySqlPass;
			CustomMysqlRolloverConn.Database = MySqlDatabase;
			customMysqlRolloverTokenParser.OnToken += TokenParserOnToken;
			CustomMysqlRolloverCommand.Connection = CustomMysqlRolloverConn;

			CustomHttpSecondsTimer = new Timer { Interval = CustomHttpSecondsInterval * 1000 };
			CustomHttpSecondsTimer.Elapsed += CustomHttpSecondsTimerTick;
			CustomHttpSecondsTimer.AutoReset = true;

			customHttpSecondsTokenParser.OnToken += TokenParserOnToken;
			customHttpMinutesTokenParser.OnToken += TokenParserOnToken;
			customHttpRolloverTokenParser.OnToken += TokenParserOnToken;

			DoSunriseAndSunset();
			DoMoonPhase();
			MoonAge = MoonriseMoonset.MoonAge();
			DoMoonImage();

			LogMessage("Station type: " + (StationType == -1 ? "Undefined" : StationDesc[StationType]));

			SetupUnitText();

			LogMessage("WindUnit=" + WindUnitText + " RainUnit=" + RainUnitText + " TempUnit=" + TempUnitText + " PressureUnit=" + PressUnitText);
			LogMessage("YTDRain=" + YTDrain.ToString("F3") + " Year=" + YTDrainyear);
			LogMessage("RainDayThreshold=" + RainDayThreshold.ToString("F3"));

			LogOffsetsMultipliers();

			LogMessage("Cumulus Starting");

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
					break;
			}

			webtags = new WebTags(this, station);
			webtags.InitialiseWebtags();

			tokenParser = new TokenParser();
			tokenParser.OnToken += TokenParserOnToken;

			realtimeTokenParser = new TokenParser();
			realtimeTokenParser.OnToken += TokenParserOnToken;

			stationSettings = new StationSettings(this);
			internetSettings = new InternetSettings(this);
			calibrationSettings = new CalibrationSettings(this);
			noaaSettings = new NOAASettings(this);
			alarmSettings = new AlarmSettings(this);
			mySqlSettings = new MysqlSettings(this);
			dataEditor = new DataEditor(this, station, webtags);
			tagProcessor = new ApiTagProcessor(this, webtags);


			// switch off logging from Unosquare.Swan which underlies embedIO
			Unosquare.Swan.Terminal.Settings.DisplayLoggingMessageType = Unosquare.Swan.LogMessageType.Fatal;

			WebServer httpServer = new WebServer(HttpPort, RoutingStrategy.Wildcard);

			var assemblyPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
			var htmlRootPath = Path.Combine(assemblyPath, "interface");

			LogMessage("HTML root path = " + htmlRootPath);

			httpServer.RegisterModule(new StaticFilesModule(htmlRootPath));
			httpServer.Module<StaticFilesModule>().UseRamCache = true;

			// Set up the API web server
			Api.Setup(httpServer);
			Api.Station = station;
			Api.stationSettings = stationSettings;
			Api.internetSettings = internetSettings;
			Api.calibrationSettings = calibrationSettings;
			Api.noaaSettings = noaaSettings;
			Api.alarmSettings = alarmSettings;
			Api.mySqlSettings = mySqlSettings;
			Api.dataEditor = dataEditor;
			Api.tagProcessor = tagProcessor;

			// Set up the Web Socket server
			WebSocket.Setup(httpServer, this);

			httpServer.RunAsync();

			LogConsoleMessage("Cumulus running at: " + httpServer.Listener.Prefixes.First());
			LogConsoleMessage("  (Replace * with any IP address on this machine, or localhost)");
			LogConsoleMessage("  Open the admin interface by entering this URL in a browser.");


			RealtimeTimer.Interval = RealtimeInterval;
			RealtimeTimer.Elapsed += RealtimeTimerTick;
			RealtimeTimer.AutoReset = true;

			SetFtpLogging(FTPlogging);

			TwitterTimer.Elapsed += TwitterTimerTick;

			WundTimer.Elapsed += WundTimerTick;
			WindyTimer.Elapsed += WindyTimerTick;
			PWSTimer.Elapsed += PWSTimerTick;
			WOWTimer.Elapsed += WowTimerTick;
			AwekasTimer.Elapsed += AwekasTimerTick;
			WCloudTimer.Elapsed += WCloudTimerTick;
			APRStimer.Elapsed += APRSTimerTick;
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

			if (MQTTEnableDataUpdate || MQTTEnableInterval)
			{

				MqttPublisher.Setup(this);

				if (MQTTEnableInterval)
				{
					MQTTTimer.Elapsed += MQTTTimerTick;
				}
			}

			InitialiseRG11();

			if (station != null && station.timerStartNeeded)
			{
				StartTimers();
			}

			if (station != null && (StationType == StationTypes.WMR100) || (StationType == StationTypes.EasyWeather) || (Manufacturer == OREGON))
			{
				station.StartLoop();
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
					customHttpSecondsHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					customHttpMinutesHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
					customHttpRolloverHandler.Credentials = new NetworkCredential(HTTPProxyUser, HTTPProxyPassword);
				}
			}
		}

		private void CustomHttpSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
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
				"press decimal(6," + PressDPlaces +") NOT NULL," +
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
			switch (TempUnit)
			{
				case 0:
					TempUnitText = "°C";
					TempTrendUnitText = "°C/hr";
					break;
				case 1:
					TempUnitText = "°F";
					TempTrendUnitText = "°F/hr";
					break;
			}

			switch (RainUnit)
			{
				case 0:
					RainUnitText = "mm";
					RainTrendUnitText = "mm/hr";
					break;
				case 1:
					RainUnitText = "in";
					RainTrendUnitText = "in/hr";
					break;
			}

			switch (PressUnit)
			{
				case 0:
					PressUnitText = "mb";
					PressTrendUnitText = "mb/hr";
					break;
				case 1:
					PressUnitText = "hPa";
					PressTrendUnitText = "hPa/hr";
					break;
				case 2:
					PressUnitText = "in";
					PressTrendUnitText = "in/hr";
					break;
			}

			switch (WindUnit)
			{
				case 0:
					WindUnitText = "m/s";
					WindRunUnitText = "km";
					break;
				case 1:
					WindUnitText = "mph";
					WindRunUnitText = "miles";
					break;
				case 2:
					WindUnitText = "km/h";
					WindRunUnitText = "km";
					break;
				case 3:
					WindUnitText = "kts";
					WindRunUnitText = "nm";
					break;
			}
		}

		public void SetFtpLogging(bool isSet)
		{
			try
			{
				FtpTrace.RemoveListener(ftpTraceListener);
			}
			catch
			{
				// ignored
			}

			if (isSet)
			{
				FtpTrace.AddListener(ftpTraceListener);
				FtpTrace.FlushOnWrite = true;
			}
		}

		internal string CalculateMD5Hash(string input)
		{
			// step 1, calculate MD5 hash from input
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hash = md5.ComputeHash(inputBytes);

			// step 2, convert byte array to hex string
			StringBuilder sb = new StringBuilder();
			foreach (var t in hash)
			{
				sb.Append(t.ToString("X2"));
			}
			return sb.ToString();
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
			if (RG11Port.Length > 0)
			{
				cmprtRG11 = new SerialPort(RG11Port, 9600, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, RtsEnable = true, DtrEnable = true };

				cmprtRG11.PinChanged += RG11StateChange;
			}

			if (RG11Port2.Length > 0 && (!RG11Port2.Equals(RG11Port)))
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
			if (!string.IsNullOrEmpty(APRSID))
			{
				station.UpdateAPRS();
			}
		}

		private void WebTimerTick(object sender, ElapsedEventArgs e)
		{
			if (WebUpdating == 1)
			{
				LogMessage("Warning, previous web update is still in progress, first chance, skipping this interval");
				WebUpdating++;
			}
			else if (WebUpdating >= 2)
			{
				LogMessage("Warning, previous web update is still in progress,second chance, aborting connection");
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

		private void TwitterTimerTick(object sender, ElapsedEventArgs e)
		{
			UpdateTwitter();
		}

		internal async void UpdateTwitter()
		{
			LogDebugMessage("Starting Twitter update");
			var auth = new XAuthAuthorizer
			{
				CredentialStore = new XAuthCredentials { ConsumerKey = twitterKey, ConsumerSecret = twitterSecret, UserName = Twitteruser, Password = TwitterPW }
			};

			if (TwitterOauthToken == "unknown")
			{
				// need to get tokens using xauth
				LogDebugMessage("Obtaining Twitter tokens");
				await auth.AuthorizeAsync();

				TwitterOauthToken = auth.CredentialStore.OAuthToken;
				TwitterOauthTokenSecret = auth.CredentialStore.OAuthTokenSecret;
				//LogDebugMessage("Token=" + TwitterOauthToken);
				//LogDebugMessage("TokenSecret=" + TwitterOauthTokenSecret);
				LogDebugMessage("Tokens obtained");
			}
			else
			{
				auth.CredentialStore.OAuthToken = TwitterOauthToken;
				auth.CredentialStore.OAuthTokenSecret = TwitterOauthTokenSecret;
			}

			using (var twitterCtx = new TwitterContext(auth))
			{
				StringBuilder status = new StringBuilder(1024);

				if (File.Exists(TwitterTxtFile))
				{
					// use twitter.txt file
					LogDebugMessage("Using twitter.txt file");
					var twitterTokenParser = new TokenParser();
					var utf8WithoutBom = new System.Text.UTF8Encoding(false);
					var encoding = utf8WithoutBom;
					twitterTokenParser.encoding = encoding;
					twitterTokenParser.SourceFile = TwitterTxtFile;
					twitterTokenParser.OnToken += TokenParserOnToken;
					status.Append(twitterTokenParser.ToString());
				}
				else
				{
					// default message
					status.Append($"Wind {station.WindAverage.ToString(WindAvgFormat)} {WindUnitText} {station.AvgBearingText}.");
					status.Append($" Barometer {station.Pressure.ToString(PressFormat)} {PressUnitText}, {station.Presstrendstr}.");
					status.Append($" Temperature {station.OutdoorTemperature.ToString(TempFormat)} {TempUnitText}.");
					status.Append($" Rain today {station.RainToday.ToString(RainFormat)}{RainUnitText}.");
					status.Append($" Humidity {station.OutdoorHumidity}%");
				}

				LogDebugMessage($"Updating Twitter: {status}");

				var statusStr = status.ToString();
				Status tweet;

				try
				{
					if (TwitterSendLocation)
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
			if (!string.IsNullOrWhiteSpace(WundID))
				UpdateWunderground(DateTime.Now);
		}

		private void WindyTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(WindyApiKey))
				UpdateWindy(DateTime.Now);
		}

		private void AwekasTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(AwekasUser))
				UpdateAwekas(DateTime.Now);
		}

		private void WCloudTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(WCloudWid))
				UpdateWCloud(DateTime.Now);
		}

		public void MQTTTimerTick(object sender, ElapsedEventArgs e)
		{
			MqttPublisher.UpdateMQTTfeed("Interval");
		}


		private void PWSTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(PWSID))
				UpdatePWSweather(DateTime.Now);
		}

		private void WowTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(WOWID))
				UpdateWOW(DateTime.Now);
		}

		internal async void UpdateWunderground(DateTime timestamp)
		{
			if (!UpdatingWU)
			{
				UpdatingWU = true;

				string pwstring;
				string URL = station.GetWundergroundURL(out pwstring, timestamp, false);

				string starredpwstring = "&PASSWORD=" + new string('*', WundPW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);
				if (!WundRapidFireEnabled)
				{
					LogDebugMessage("WU URL: " + LogURL);
				}

				try
				{
					HttpResponseMessage response = await WUhttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					if (!WundRapidFireEnabled)
					{
						LogMessage("WU Response: " + response.StatusCode + ": " + responseBodyAsText);
					}
				}
				catch (Exception ex)
				{
					LogMessage("WU update: " + ex.Message);
				}
				finally
				{
					UpdatingWU = false;
				}
			}
		}

		internal async void UpdateWindy(DateTime timestamp)
		{
			if (!UpdatingWindy)
			{
				UpdatingWindy = true;

				string apistring;
				string URL = station.GetWindyURL(out apistring, timestamp);
				string LogURL = URL.Replace(apistring, "<<API_KEY>>");

				LogDebugMessage("Windy URL: " + LogURL);

				try
				{
					HttpResponseMessage response = await WindyhttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogMessage("Windy Response: " + response.StatusCode + ": " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogMessage("Windy update: " + ex.Message);
				}
				finally
				{
					UpdatingWindy = false;
				}
			}
		}

		internal async void UpdateAwekas(DateTime timestamp)
		{
			if (!UpdatingAwekas)
			{
				UpdatingAwekas = true;

				string pwstring;
				//string URL = station.GetAwekasURL(out pwstring, timestamp);
				string URL = station.GetAwekasURLv4(out pwstring, timestamp);

				string starredpwstring = "<password>";

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogDebugMessage("AWEKAS: URL = " + LogURL);

				try
				{
					HttpResponseMessage response = await AwekashttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogDebugMessage("AWEKAS Response code = " + response.StatusCode);
					LogDataMessage("AWEKAS: Response text = " + responseBodyAsText);
					var respJson = JsonConvert.DeserializeObject<AwekasResponse>(responseBodyAsText);
					//var jObject = JObject.Parse(responseBodyAsText);

					// Check the status response
					if (respJson.status == 2)
					//if (jObject.Value<int>("status") == 2)
						LogMessage("AWEKAS: Data stored OK");
					else if (respJson.status == 1)
					//else if (jObject.Value<int>("status") == 1)
					{
						LogMessage("AWEKAS: Data PARIALLY stored");
						// TODO: Check errors and disabled
					}
					else if (respJson.status == 0)  // Authenication error or rate limited
					//else if (jObject.Value<int>("status") == 0)
					{
						if (respJson.minuploadtime > 0 && respJson.authentication == 0)
						//if (!string.IsNullOrEmpty(jObject.Value<string>("authentication")) && jObject.Value<int>("authentication") == 0)
						{
							LogMessage("AWEKAS: Authentication error");
							if (AwekasInterval < 60)
							{
								AwekasRateLimited = true;
								AwekasOriginalInterval = AwekasInterval;
								AwekasInterval = 60;
								AwekasTimer.Enabled = false;
								SynchronisedAwekasUpdate = true;
								LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 60 seconds due to authenication error");
							}
						}
						else if (respJson.minuploadtime == 0)
						//else if (jObject.Value<int>("minuploadtime") == 0)
						{
							LogMessage("AWEKAS: Too many requests, rate limited");
							if (AwekasInterval < 60)
							{
								AwekasRateLimited = true;
								AwekasOriginalInterval = AwekasInterval;
								AwekasInterval = 60;
								AwekasTimer.Enabled = false;
								SynchronisedAwekasUpdate = true;
								LogMessage("AWEKAS: Temporarily increasing AWEKAS upload interval to 60 seconds due to rate limit");
							}
						}
						else
						{
							LogMessage("AWEKAS: Unknown error");
						}
					}

					// check the min upload time is greater than our upload time
					if (respJson.status > 0 && respJson.minuploadtime > AwekasOriginalInterval)
					//if (jObject.Value<int>("status") > 0 && jObject.Value<int>("minuploadtime") > AwekasInterval)
					{
						//LogMessage($"The minimum upload time to AWEKAS for your station is {jObject.Value<string>("minuploadtime")} sec, Cumulus is configured for {AwekasInterval} sec, increasing Cumulus interval to match AWEKAS");
						//AwekasInterval = jObject.Value<int>("minuploadtime") <= 60 ? 1 : jObject.Value<int>("minuploadtime");
						LogMessage($"AWEKAS: The minimum upload time to AWEKAS for your station is {respJson.minuploadtime} sec, Cumulus is configured for {AwekasOriginalInterval} sec, increasing Cumulus interval to match AWEKAS");
						AwekasInterval = respJson.minuploadtime;
						WriteIniFile();
						AwekasTimer.Interval = AwekasInterval * 1000;
						SynchronisedAwekasUpdate = AwekasInterval % 60 == 0;
						AwekasTimer.Enabled = !SynchronisedAwekasUpdate;
						// we got a successful upload, and reset the interval, so clear the rate limited values
						AwekasOriginalInterval = AwekasInterval;
						AwekasRateLimited = false;
					}
					else if (AwekasRateLimited && respJson.status > 0)
					{
						// We are currently rate limited, it could have been a transient thing because
						// we just got a valid response, and our interval is >= the minimum allowed.
						// So we just undo the limit, and resume as before
						LogMessage($"AWEKAS: Removing temporary increase in upload interval to 60 secs, resuming uploads every {AwekasOriginalInterval} secs");
						AwekasInterval = AwekasOriginalInterval;
						AwekasTimer.Interval = AwekasInterval * 1000;
						SynchronisedAwekasUpdate = AwekasInterval % 60 == 0;
						AwekasTimer.Enabled = !SynchronisedAwekasUpdate;
						AwekasRateLimited = false;
					}
				}
				catch (Exception ex)
				{
					LogMessage("AWEKAS: Exception = " + ex.Message);
				}
				finally
				{
					UpdatingAwekas = false;
				}
			}
		}

		internal async void UpdateWCloud(DateTime timestamp)
		{
			if (!UpdatingWCloud)
			{
				UpdatingWCloud = true;

				string pwstring;
				string URL = station.GetWCloudURL(out pwstring, timestamp);

				string starredpwstring = "<key>";

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogDebugMessage("WeatherCloud URL: " + LogURL);

				try
				{
					HttpResponseMessage response = await WCloudhttpClient.GetAsync(URL);
					var responseBodyAsText = await response.Content.ReadAsStringAsync();
					LogMessage("WeatherCloud Response: " + response.StatusCode + ": " + responseBodyAsText);
				}
				catch (Exception ex)
				{
					LogMessage("WeatherCloud update: " + ex.Message);
				}
				finally
				{
					UpdatingWCloud = false;
				}
			}
		}


		internal void RealtimeTimerTick(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			bool connectionFailed = false;
			var cycle = RealtimeCycleCounter++;

			LogDebugMessage($"Realtime[{cycle}]: Start cycle");
			try
			{
				// Process any files
				if (RealtimeCopyInProgress)
				{
					LogDebugMessage($"Realtime[{cycle}]: Warning, a previous cycle is still processing local files. Skipping this interval.");
				}
				else
				{
					RealtimeCopyInProgress = true;
					CreateRealtimeFile(cycle);
					CreateRealtimeHTMLfiles(cycle);
					RealtimeCopyInProgress = false;

					if (RealtimeFTPEnabled)
					{
						// Is a previous cycle still running?
						if (RealtimeFtpInProgress)
						{
							LogDebugMessage($"Realtime[{cycle}]: Warning, a previous cycle is still trying to connect to FTP server, skip count = {++RealtimeFTPRetries}");
							LogDebugMessage($"Realtime[{cycle}]: No FTP attempted this cycle");
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
								RealtimeFTPRetries = 0;
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
				LogDebugMessage($"Realtime[{cycle}]: Reconnected with server OK");
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
			// realtime.txt
			string filepath, gaugesfilepath;

			if (ftp_directory.Length == 0)
			{
				filepath = "realtime.txt";
				gaugesfilepath = "realtimegauges.txt";
			}
			else
			{
				filepath = ftp_directory + "/realtime.txt";
				gaugesfilepath = ftp_directory + "/realtimegauges.txt";
			}

			if (RealtimeTxtFTP)
			{
				LogFtpMessage($"Realtime[{cycle}]: Uploading - realtime.txt");
				if (Sslftp == FtpProtocols.SFTP)
				{
					UploadFile(RealtimeSSH, RealtimeFile, filepath, cycle);
				}
				else
				{
					UploadFile(RealtimeFTP, RealtimeFile, filepath, cycle);
				}
			}

			if (RealtimeGaugesTxtFTP)
			{
				ProcessTemplateFile(RealtimeGaugesTxtTFile, RealtimeGaugesTxtFile, realtimeTokenParser);
				LogFtpMessage($"Realtime[{cycle}]: Uploading - realtimegauges.txt");
				if (Sslftp == FtpProtocols.SFTP)
				{
					UploadFile(RealtimeSSH, RealtimeGaugesTxtFile, gaugesfilepath, cycle);
				}
				else
				{
					UploadFile(RealtimeFTP, RealtimeGaugesTxtFile, gaugesfilepath, cycle);
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

							if (ExtraFiles[i].process)
							{
								// process the file
								LogDebugMessage($"Realtime[{cycle}]: Processing extra file[{i}] - {uploadfile}");
								var utf8WithoutBom = new UTF8Encoding(false);
								var encoding = UTF8encode ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
								realtimeTokenParser.encoding = encoding;
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

		private bool MoonImageReady = false;

		private void getSunriseSunset(DateTime time, out DateTime sunrise, out DateTime sunset, out bool alwaysUp, out bool alwaysDown)
		{
			string rise = SunriseSunset.sunrise(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);
			string set = SunriseSunset.sunset(time, TimeZone.CurrentTimeZone.GetUtcOffset(time).TotalHours, Longitude, Latitude);

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

		private void getDawnDusk(DateTime time, out DateTime dawn, out DateTime dusk, out bool alwaysUp, out bool alwaysDown)
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
			getSunriseSunset(now, out SunRiseTime, out SunSetTime, out SunAlwaysUp, out SunAlwaysDown);

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

			getSunriseSunset(tomorrow, out tomorrowSunRiseTime, out tomorrowSunSetTime, out tomorrowSunAlwaysUp, out tomorrowSunAlwaysDown);

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

			getDawnDusk(now, out Dawn, out Dusk, out TwilightAlways, out TwilightNever);

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
			var ImetBaudRates = new List<int> { 19200, 115200 };

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

			StationType = ini.GetValue("Station", "Type", -1);

			StationModel = ini.GetValue("Station", "Model", "");

			FineOffsetStation = (StationType == StationTypes.FineOffset || StationType == StationTypes.FineOffsetSolar);
			DavisStation = (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2);

			UseDavisLoop2 = ini.GetValue("Station", "UseDavisLoop2", true);
			DavisReadReceptionStats = ini.GetValue("Station", "DavisReadReceptionStats", false);
			DavisInitWaitTime = ini.GetValue("Station", "DavisInitWaitTime", 2000);
			DavisIPResponseTime = ini.GetValue("Station", "DavisIPResponseTime", 500);
			DavisReadTimeout = ini.GetValue("Station", "DavisReadTimeout", 1000);
			DavisIncrementPressureDP = ini.GetValue("Station", "DavisIncrementPressureDP", true);
			if (StationType == StationTypes.VantagePro)
			{
				UseDavisLoop2 = false;
			}

			serial_port = ini.GetValue("Station", "Port", 0);

			ComportName = ini.GetValue("Station", "ComportName", DefaultComportName);
			ImetBaudRate = ini.GetValue("Station", "ImetBaudRate", 19200);
			// Check we have a valid value
			if (!ImetBaudRates.Contains(ImetBaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Error, the value for ImetBaudRate in the ini file " + ImetBaudRate + " is not valid, using default 19200.");
				ImetBaudRate = 19200;
			}

			DavisBaudRate = ini.GetValue("Station", "DavisBaudRate", 19200);
			// Check we have a valid value
			if (!DavisBaudRates.Contains(DavisBaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Error, the value for DavisBaudRate in the ini file " + DavisBaudRate + " is not valid, using default 19200.");
				DavisBaudRate = 19200;
			}

			VendorID = ini.GetValue("Station", "VendorID", -1);
			ProductID = ini.GetValue("Station", "ProductID", -1);

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

			LogMessage("ASM=" + AvgSpeedMinutes + " AST=" + AvgSpeedTime.ToString());

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
			ErrorLogSpikeRemoval = ini.GetValue("Station", "ErrorLogSpikeRemoval", true);
			DataLogInterval = ini.GetValue("Station", "DataLogInterval", 2);
			// this is now an index
			if (DataLogInterval > 5)
			{
				DataLogInterval = 2;
			}

			SyncFOReads = ini.GetValue("Station", "SyncFOReads", true);
			FOReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3);
			FineOffsetReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150);

			WS2300Sync = ini.GetValue("Station", "WS2300Sync", false);
			WindUnit = ini.GetValue("Station", "WindUnit", 0);
			PressUnit = ini.GetValue("Station", "PressureUnit", 0);

			RainUnit = ini.GetValue("Station", "RainUnit", 0);
			TempUnit = ini.GetValue("Station", "TempUnit", 0);

			RoundWindSpeed = ini.GetValue("Station", "RoundWindSpeed", false);

			WindDPlaces = RoundWindSpeed ? 0 : WindDPlace[WindUnit];
			WindAvgDPlaces = WindDPlaces;

			// Wind speed decimals overrides - readonly
			WindDPlaces = ini.GetValue("Station", "WindSpeedDecimals", WindDPlaces);
			WindAvgDPlaces = ini.GetValue("Station", "WindSpeedAvgDecimals", WindAvgDPlaces);

			TempDPlaces = TempDPlace[TempUnit];
			PressDPlaces = PressDPlace[PressUnit];
			if ((StationType == 0 || StationType == 1) && DavisIncrementPressureDP)
			{
				// Use one more DP for Davis stations
				++PressDPlaces;
			}
			RainDPlaces = RainDPlace[RainUnit];

			SunshineDPlaces = ini.GetValue("Station", "SunshineHrsDecimals", 1);

			LocationName = ini.GetValue("Station", "LocName", "");
			LocationDesc = ini.GetValue("Station", "LocDesc", "");

			YTDrain = ini.GetValue("Station", "YTDrain", 0.0);
			YTDrainyear = ini.GetValue("Station", "YTDrainyear", 0);

			EWInterval = ini.GetValue("Station", "EWInterval", 1.0);
			EWFile = ini.GetValue("Station", "EWFile", "");
			EWallowFF = ini.GetValue("Station", "EWFF", false);
			EWdisablecheckinit = ini.GetValue("Station", "EWdisablecheckinit", false);
			EWduplicatecheck = ini.GetValue("Station", "EWduplicatecheck", true);

			SpikeTempDiff = ini.GetValue("Station", "EWtempdiff", 999.0);
			SpikePressDiff = ini.GetValue("Station", "EWpressurediff", 999.0);
			SpikeHumidityDiff = ini.GetValue("Station", "EWhumiditydiff", 999.0);
			SpikeGustDiff = ini.GetValue("Station", "EWgustdiff", 999.0);
			SpikeWindDiff = ini.GetValue("Station", "EWwinddiff", 999.0);
			SpikeMaxRainRate = ini.GetValue("Station", "EWmaxRainRate", 999.0);
			SpikeMaxHourlyRain = ini.GetValue("Station", "EWmaxHourlyRain", 999.0);

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

			LogMessage("Cumulus start date: " + RecordsBeganDate);

			ImetWaitTime = ini.GetValue("Station", "ImetWaitTime", 500);
			ImetUpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);

			UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
			UseCumulusForecast = ini.GetValue("Station", "UseCumulusForecast", false);
			HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
			UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
			UseWindChillCutoff = ini.GetValue("Station", "UseWindChillCutoff", false);
			RecordSetTimeoutHrs = ini.GetValue("Station", "RecordSetTimeoutHrs", 24);

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
				ChillHourThreshold = TempUnit == 0 ? 7 : 45;
			}

			if (FCPressureThreshold < 0)
			{
				FCPressureThreshold = PressUnit == 2 ? 0.00295333727 : 0.1;
			}

			special_logging = ini.GetValue("Station", "SpecialLog", false);
			solar_logging = ini.GetValue("Station", "SolarLog", false);

			if (DebuggingEnabled)
			{
				logging = true;
				DataLogging = true;
			}
			else
			{
				logging = ini.GetValue("Station", "Logging", false);
				DataLogging = ini.GetValue("Station", "DataLogging", false);
			}

			VP2ConnectionType = ini.GetValue("Station", "VP2ConnectionType", VP2SERIALCONNECTION);
			VP2TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222);
			VP2IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");

			WarnMultiple = ini.GetValue("Station", "WarnMultiple", true);

			VPClosedownTime = ini.GetValue("Station", "VPClosedownTime", 99999999);

			VP2SleepInterval = ini.GetValue("Station", "VP2SleepInterval", 0);

			VP2PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0);

			RTdisconnectcount = ini.GetValue("Station", "RTdisconnectcount", 0);

			WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0);

			WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1);

			CreateWxnowTxt = ini.GetValue("Station", "CreateWxnowTxt", true);

			ListWebTags = ini.GetValue("Station", "ListWebTags", false);

			// WeatherLink Live device settings
			WllApiKey = ini.GetValue("WLL", "WLv2ApiKey", "");
			WllApiSecret = ini.GetValue("WLL", "WLv2ApiSecret", "");
			WllStationId = ini.GetValue("WLL", "WLStationId", "");
			if (WllStationId == "-1") WllStationId = "";
			WLLAutoUpdateIpAddress = ini.GetValue("WLL", "AutoUpdateIpAddress", true);
			WllBroadcastDuration = ini.GetValue("WLL", "BroadcastDuration", 300);      // Readonly setting, default 5 minutes
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
			Gw1000AutoUpdateIpAddress = ini.GetValue("GW1000", "AutoUpdateIpAddress", true);

			ftp_host = ini.GetValue("FTP site", "Host", "");
			ftp_port = ini.GetValue("FTP site", "Port", 21);
			ftp_user = ini.GetValue("FTP site", "Username", "");
			ftp_password = ini.GetValue("FTP site", "Password", "");
			ftp_directory = ini.GetValue("FTP site", "Directory", "");

			WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);
			ActiveFTPMode = ini.GetValue("FTP site", "ActiveFTP", false);
			Sslftp = (FtpProtocols)ini.GetValue("FTP site", "Sslftp", 0);
			// BUILD 3092 - added alternate SFTP authenication options
			SshftpAuthentication = ini.GetValue("FTP site", "SshFtpAuthentication", "password"); // valid options: password, psk, password_psk
			if (!SshAuthenticationVals.Any(SshftpAuthentication.Contains))
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
			RealtimeTxtFTP = ini.GetValue("FTP site", "RealtimeTxtFTP", false);
			RealtimeGaugesTxtFTP = ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);
			RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000);
			if (RealtimeInterval < 1) { RealtimeInterval = 1; }
			//RealtimeTimer.Change(0,RealtimeInterval);
			UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", DefaultWebUpdateInterval);
			if (UpdateInterval<1) { UpdateInterval = 1; }
			SynchronisedWebUpdate = (60 % UpdateInterval == 0);
			IncludeStandardFiles = ini.GetValue("FTP site", "IncludeSTD", true);
			IncludeGraphDataFiles = ini.GetValue("FTP site", "IncludeGraphDataFiles", true);
			IncludeMoonImage = ini.GetValue("FTP site", "IncludeMoonImage", false);

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
			GraphHours = ini.GetValue("Graphs", "GraphHours", 24);
			MoonImageEnabled = ini.GetValue("Graphs", "MoonImageEnabled", false);
			MoonImageSize = ini.GetValue("Graphs", "MoonImageSize", 100);
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


			WundID = ini.GetValue("Wunderground", "ID", "");
			WundPW = ini.GetValue("Wunderground", "Password", "");
			WundEnabled = ini.GetValue("Wunderground", "Enabled", false);
			WundRapidFireEnabled = ini.GetValue("Wunderground", "RapidFire", false);
			WundInterval = ini.GetValue("Wunderground", "Interval", DefaultWundInterval);
			//WundHTTPLogging = ini.GetValue("Wunderground", "Logging", false);
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

			WindyApiKey = ini.GetValue("Windy", "APIkey", "");
			WindyStationIdx = ini.GetValue("Windy", "StationIdx", 0);
			WindyEnabled = ini.GetValue("Windy", "Enabled", false);
			WindyInterval = ini.GetValue("Windy", "Interval", DefaultWindyInterval);
			if (WindyInterval < 5) { WindyInterval = 5; }
			//WindyHTTPLogging = ini.GetValue("Windy", "Logging", false);
			WindySendUV = ini.GetValue("Windy", "SendUV", false);
			WindySendSolar = ini.GetValue("Windy", "SendSolar", false);
			WindyCatchUp = ini.GetValue("Windy", "CatchUp", true);

			SynchronisedWindyUpdate = (60 % WindyInterval == 0);

			AwekasUser = ini.GetValue("Awekas", "User", "");
			AwekasPW = ini.GetValue("Awekas", "Password", "");
			AwekasEnabled = ini.GetValue("Awekas", "Enabled", false);
			AwekasInterval = ini.GetValue("Awekas", "Interval", DefaultAwekasInterval);
			if (AwekasInterval < 15) { AwekasInterval = 15; }
			AwekasLang = ini.GetValue("Awekas", "Language", "en");
			AwekasOriginalInterval = AwekasInterval;
			SendUVToAwekas = ini.GetValue("Awekas", "SendUV", false);
			SendSolarToAwekas = ini.GetValue("Awekas", "SendSR", false);
			SendSoilTempToAwekas = ini.GetValue("Awekas", "SendSoilTemp", false);
			SendIndoorToAwekas = ini.GetValue("Awekas", "SendIndoor", false);
			SendSoilMoistureToAwekas = ini.GetValue("Awekas", "SendSoilMoisture", false);
			SendLeafWetnessToAwekas = ini.GetValue("Awekas", "SendLeafWetness", false);

			SynchronisedAwekasUpdate = (AwekasInterval % 60 == 0);

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

			MQTTServer = ini.GetValue("MQTT", "Server", "");
			MQTTPort = ini.GetValue("MQTT", "Port", 1883);
			MQTTIpVersion = ini.GetValue("MQTT", "IPversion", 0); // 0 = unspecified, 4 = force IPv4, 6 = force IPv6
			if (MQTTIpVersion != 0 && MQTTIpVersion != 4 && MQTTIpVersion != 6)
				MQTTIpVersion = 0;
			MQTTUseTLS = ini.GetValue("MQTT", "UseTLS", false);
			MQTTUsername = ini.GetValue("MQTT", "Username", "");
			MQTTPassword = ini.GetValue("MQTT", "Password", "");
			MQTTEnableDataUpdate = ini.GetValue("MQTT", "EnableDataUpdate", false);
			MQTTUpdateTopic = ini.GetValue("MQTT", "UpdateTopic", "CumulusMX/DataUpdate");
			MQTTUpdateTemplate = ini.GetValue("MQTT", "UpdateTemplate", "DataUpdateTemplate.txt");
			MQTTEnableInterval = ini.GetValue("MQTT", "EnableInterval", false);
			MQTTIntervalTime = ini.GetValue("MQTT", "IntervalTime", 600); // default to 10 minutes
			MQTTIntervalTopic = ini.GetValue("MQTT", "IntervalTopic", "CumulusMX/Interval");
			MQTTIntervalTemplate = ini.GetValue("MQTT", "IntervalTemplate", "IntervalTemplate.txt");

			LowTempAlarmValue = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
			LowTempAlarmEnabled = ini.GetValue("Alarms", "LowTempAlarmSet", false);
			LowTempAlarmSound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
			LowTempAlarmSoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
			if (LowTempAlarmSoundFile.Contains(DefaultSoundFileOld)) LowTempAlarmSoundFile = DefaultSoundFile;

			HighTempAlarmValue = ini.GetValue("Alarms", "alarmhightemp", 0.0);
			HighTempAlarmEnabled = ini.GetValue("Alarms", "HighTempAlarmSet", false);
			HighTempAlarmSound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
			HighTempAlarmSoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
			if (HighTempAlarmSoundFile.Contains(DefaultSoundFileOld)) HighTempAlarmSoundFile = DefaultSoundFile;

			TempChangeAlarmValue = ini.GetValue("Alarms", "alarmtempchange", 0.0);
			TempChangeAlarmEnabled = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
			TempChangeAlarmSound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
			TempChangeAlarmSoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
			if (TempChangeAlarmSoundFile.Contains(DefaultSoundFileOld)) TempChangeAlarmSoundFile = DefaultSoundFile;

			LowPressAlarmValue = ini.GetValue("Alarms", "alarmlowpress", 0.0);
			LowPressAlarmEnabled = ini.GetValue("Alarms", "LowPressAlarmSet", false);
			LowPressAlarmSound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
			LowPressAlarmSoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
			if (LowPressAlarmSoundFile.Contains(DefaultSoundFileOld)) LowPressAlarmSoundFile = DefaultSoundFile;

			HighPressAlarmValue = ini.GetValue("Alarms", "alarmhighpress", 0.0);
			HighPressAlarmEnabled = ini.GetValue("Alarms", "HighPressAlarmSet", false);
			HighPressAlarmSound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
			HighPressAlarmSoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
			if (HighPressAlarmSoundFile.Contains(DefaultSoundFileOld)) HighPressAlarmSoundFile = DefaultSoundFile;

			PressChangeAlarmValue = ini.GetValue("Alarms", "alarmpresschange", 0.0);
			PressChangeAlarmEnabled = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
			PressChangeAlarmSound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
			PressChangeAlarmSoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
			if (PressChangeAlarmSoundFile.Contains(DefaultSoundFileOld)) PressChangeAlarmSoundFile = DefaultSoundFile;

			HighRainTodayAlarmValue = ini.GetValue("Alarms", "alarmhighraintoday", 0.0);
			HighRainTodayAlarmEnabled = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
			HighRainTodayAlarmSound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
			HighRainTodayAlarmSoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
			if (HighRainTodayAlarmSoundFile.Contains(DefaultSoundFileOld)) HighRainTodayAlarmSoundFile = DefaultSoundFile;

			HighRainRateAlarmValue = ini.GetValue("Alarms", "alarmhighrainrate", 0.0);
			HighRainRateAlarmEnabled = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
			HighRainRateAlarmSound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
			HighRainRateAlarmSoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
			if (HighRainRateAlarmSoundFile.Contains(DefaultSoundFileOld)) HighRainRateAlarmSoundFile = DefaultSoundFile;

			HighGustAlarmValue = ini.GetValue("Alarms", "alarmhighgust", 0.0);
			HighGustAlarmEnabled = ini.GetValue("Alarms", "HighGustAlarmSet", false);
			HighGustAlarmSound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
			HighGustAlarmSoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
			if (HighGustAlarmSoundFile.Contains(DefaultSoundFileOld)) HighGustAlarmSoundFile = DefaultSoundFile;

			HighWindAlarmValue = ini.GetValue("Alarms", "alarmhighwind", 0.0);
			HighWindAlarmEnabled = ini.GetValue("Alarms", "HighWindAlarmSet", false);
			HighWindAlarmSound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
			HighWindAlarmSoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
			if (HighWindAlarmSoundFile.Contains(DefaultSoundFileOld)) HighWindAlarmSoundFile = DefaultSoundFile;

			SensorAlarmEnabled = ini.GetValue("Alarms", "SensorAlarmSet", false);
			SensorAlarmSound = ini.GetValue("Alarms", "SensorAlarmSound", false);
			SensorAlarmSoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
			if (SensorAlarmSoundFile.Contains(DefaultSoundFileOld)) SensorAlarmSoundFile = DefaultSoundFile;

			DataStoppedAlarmEnabled = ini.GetValue("Alarms", "DataStoppedAlarmSet", false);
			DataStoppedAlarmSound = ini.GetValue("Alarms", "DataStoppedAlarmSound", false);
			DataStoppedAlarmSoundFile = ini.GetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
			if (DataStoppedAlarmSoundFile.Contains(DefaultSoundFileOld)) SensorAlarmSoundFile = DefaultSoundFile;

			BatteryLowAlarmEnabled = ini.GetValue("Alarms", "BatteryLowAlarmSet", false);
			BatteryLowAlarmSound = ini.GetValue("Alarms", "BatteryLowAlarmSound", false);
			BatteryLowAlarmSoundFile = ini.GetValue("Alarms", "BatteryLowAlarmSoundFile", DefaultSoundFile);

			SpikeAlarmEnabled = ini.GetValue("Alarms", "DataSpikeAlarmSet", false);
			SpikeAlarmSound = ini.GetValue("Alarms", "DataSpikeAlarmSound", false);
			SpikeAlarmSoundFile = ini.GetValue("Alarms", "DataSpikeAlarmSoundFile", DefaultSoundFile);

			PressOffset = ini.GetValue("Offsets", "PressOffset", 0.0);
			TempOffset = ini.GetValue("Offsets", "TempOffset", 0.0);
			HumOffset = ini.GetValue("Offsets", "HumOffset", 0);
			WindDirOffset = ini.GetValue("Offsets", "WindDirOffset", 0);
			InTempoffset = ini.GetValue("Offsets", "InTempOffset", 0.0);
			SolarOffset = ini.GetValue("Offsers", "SolarOffset", 0.0);
			UVOffset = ini.GetValue("Offsets", "UVOffset", 0.0);
			WetBulbOffset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);

			PressMult = ini.GetValue("Offsets", "PressMult", 1.0);
			WindSpeedMult = ini.GetValue("Offsets", "WindSpeedMult", 1.0);
			WindGustMult = ini.GetValue("Offsets", "WindGustMult", 1.0);
			TempMult = ini.GetValue("Offsets", "TempMult", 1.0);
			TempMult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
			HumMult = ini.GetValue("Offsets", "HumMult", 1.0);
			HumMult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
			RainMult = ini.GetValue("Offsets", "RainMult", 1.0);
			SolarMult = ini.GetValue("Offsets", "SolarMult", 1.0);
			UVMult = ini.GetValue("Offsets", "UVMult", 1.0);
			WetBulbMult = ini.GetValue("Offsets", "WetBulbMult", 1.0);

			LimitTempHigh = ini.GetValue("Limits", "TempHighC", 60.0);
			LimitTempLow = ini.GetValue("Limits", "TempLowC", -60.0);
			LimitDewHigh = ini.GetValue("Limits", "DewHighC", 40.0);
			LimitPressHigh = ini.GetValue("Limits", "PressHighMB", 1090.0);
			LimitPressLow = ini.GetValue("Limits", "PressLowMB", 870.0);
			LimitWindHigh = ini.GetValue("Limits", "WindHighMS", 90.0);

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
				NOAAheatingthreshold = TempUnit == 0 ? 18.3 : 65;
			}
			NOAAcoolingthreshold = ini.GetValue("NOAA", "CoolingThreshold", -1000.0);
			if (NOAAcoolingthreshold < -999)
			{
				NOAAcoolingthreshold = TempUnit == 0 ? 18.3 : 65;
			}
			NOAAmaxtempcomp1 = ini.GetValue("NOAA", "MaxTempComp1", -1000.0);
			if (NOAAmaxtempcomp1 < -999)
			{
				NOAAmaxtempcomp1 = TempUnit == 0 ? 27 : 80;
			}
			NOAAmaxtempcomp2 = ini.GetValue("NOAA", "MaxTempComp2", -1000.0);
			if (NOAAmaxtempcomp2 < -999)
			{
				NOAAmaxtempcomp2 = TempUnit == 0 ? 0 : 32;
			}
			NOAAmintempcomp1 = ini.GetValue("NOAA", "MinTempComp1", -1000.0);
			if (NOAAmintempcomp1 < -999)
			{
				NOAAmintempcomp1 = TempUnit == 0 ? 0 : 32;
			}
			NOAAmintempcomp2 = ini.GetValue("NOAA", "MinTempComp2", -1000.0);
			if (NOAAmintempcomp2 < -999)
			{
				NOAAmintempcomp2 = TempUnit == 0 ? -18 : 0;
			}
			NOAAraincomp1 = ini.GetValue("NOAA", "RainComp1", -1000.0);
			if (NOAAraincomp1 < -999)
			{
				NOAAraincomp1 = RainUnit == 0 ? 0.2 : 0.01;
			}
			NOAAraincomp2 = ini.GetValue("NOAA", "RainComp2", -1000.0);
			if (NOAAraincomp2 < -999)
			{
				NOAAraincomp2 = RainUnit == 0 ? 2 : 0.1;
			}
			NOAAraincomp3 = ini.GetValue("NOAA", "RainComp3", -1000.0);
			if (NOAAraincomp3 < -999)
			{
				NOAAraincomp3 = RainUnit == 0 ? 20 : 1;
			}

			NOAAAutoSave = ini.GetValue("NOAA", "AutoSave", false);
			NOAAAutoFTP = ini.GetValue("NOAA", "AutoFTP", false);
			NOAAMonthFileFormat = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
			// Check for Cumulus 1 default format - and update
			if (NOAAMonthFileFormat == "'NOAAMO'mmyy'.txt'")
			{
				NOAAMonthFileFormat = "'NOAAMO'MMyy'.txt'";
			}
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
			LogMessage("Writing Cumulus.ini file");

			IniFile ini = new IniFile("Cumulus.ini");

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
			ini.SetValue("Station", "EWtempdiff", SpikeTempDiff);
			ini.SetValue("Station", "EWpressurediff", SpikePressDiff);
			ini.SetValue("Station", "EWhumiditydiff", SpikeHumidityDiff);
			ini.SetValue("Station", "EWgustdiff", SpikeGustDiff);
			ini.SetValue("Station", "EWwinddiff", SpikeWindDiff);
			ini.SetValue("Station", "EWmaxHourlyRain", SpikeMaxHourlyRain);
			ini.SetValue("Station", "EWmaxRainRate", SpikeMaxRainRate);

			ini.SetValue("Station", "EWminpressureMB", EWminpressureMB);
			ini.SetValue("Station", "EWmaxpressureMB", EWmaxpressureMB);

			ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
			ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

			ini.SetValue("Station", "ErrorLogSpikeRemoval", ErrorLogSpikeRemoval);

			//ini.SetValue("Station", "ImetBaudRate", ImetBaudRate);
			//ini.SetValue("Station", "DavisBaudRate", DavisBaudRate);

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
			ini.SetValue("GW1000", "AutoUpdateIpAddress", Gw1000AutoUpdateIpAddress);

			ini.SetValue("Web Site", "ForumURL", ForumURL);
			ini.SetValue("Web Site", "WebcamURL", WebcamURL);

			ini.SetValue("FTP site", "Host", ftp_host);
			ini.SetValue("FTP site", "Port", ftp_port);
			ini.SetValue("FTP site", "Username", ftp_user);
			ini.SetValue("FTP site", "Password", ftp_password);
			ini.SetValue("FTP site", "Directory", ftp_directory);

			ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);
			ini.SetValue("FTP site", "ActiveFTP", ActiveFTPMode);
			ini.SetValue("FTP site", "Sslftp", (int)Sslftp);
			// BUILD 3092 - added alternate SFTP authenication options
			ini.SetValue("FTP site", "SshFtpAuthentication", SshftpAuthentication);
			ini.SetValue("FTP site", "SshFtpPskFile", SshftpPskFile);

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
			ini.SetValue("FTP site", "IncludeMoonImage", IncludeMoonImage);
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
				ini.SetValue("FTP site", "ExtraEOD" + i, ExtraFiles[i].endofday);
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

			ini.SetValue("Windy", "APIkey", WindyApiKey);
			ini.SetValue("Windy", "StationIdx", WindyStationIdx);
			ini.SetValue("Windy", "Enabled", WindyEnabled);
			ini.SetValue("Windy", "Interval", WindyInterval);
			ini.SetValue("Windy", "SendUV", WindySendUV);
			ini.SetValue("Windy", "CatchUp", WindyCatchUp);

			ini.SetValue("Awekas", "User", AwekasUser);
			ini.SetValue("Awekas", "Password", AwekasPW);
			ini.SetValue("Awekas", "Language", AwekasLang);
			ini.SetValue("Awekas", "Enabled", AwekasEnabled);
			ini.SetValue("Awekas", "Interval", AwekasInterval);
			ini.SetValue("Awekas", "SendUV", SendUVToAwekas);
			ini.SetValue("Awekas", "SendSR", SendSolarToAwekas);
			ini.SetValue("Awekas", "SendSoilTemp", SendSoilTempToAwekas);
			ini.SetValue("Awekas", "SendIndoor", SendIndoorToAwekas);
			ini.SetValue("Awekas", "SendSoilMoisture", SendSoilMoistureToAwekas);
			ini.SetValue("Awekas", "SendLeafWetness", SendLeafWetnessToAwekas);

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

			ini.SetValue("APRS", "ID", APRSID);
			ini.SetValue("APRS", "pass", APRSpass);
			ini.SetValue("APRS", "server", APRSserver);
			ini.SetValue("APRS", "port", APRSport);
			ini.SetValue("APRS", "Enabled", APRSenabled);
			ini.SetValue("APRS", "Interval", APRSinterval);
			ini.SetValue("APRS", "SendSR", SendSRToAPRS);

			ini.SetValue("MQTT", "Server", MQTTServer);
			ini.SetValue("MQTT", "Port", MQTTPort);
			ini.SetValue("MQTT", "UseTLS", MQTTUseTLS);
			ini.SetValue("MQTT", "Username", MQTTUsername);
			ini.SetValue("MQTT", "Password", MQTTPassword);
			ini.SetValue("MQTT", "EnableDataUpdate", MQTTEnableDataUpdate);
			ini.SetValue("MQTT", "UpdateTopic", MQTTUpdateTopic);
			ini.SetValue("MQTT", "UpdateTemplate", MQTTUpdateTemplate);
			ini.SetValue("MQTT", "EnableInterval", MQTTEnableInterval);
			ini.SetValue("MQTT", "IntervalTime", MQTTIntervalTime);
			ini.SetValue("MQTT", "IntervalTopic", MQTTIntervalTopic);
			ini.SetValue("MQTT", "IntervalTemplate", MQTTIntervalTemplate);

			ini.SetValue("Alarms", "alarmlowtemp", LowTempAlarmValue);
			ini.SetValue("Alarms", "LowTempAlarmSet", LowTempAlarmEnabled);
			ini.SetValue("Alarms", "LowTempAlarmSound", LowTempAlarmSound);
			ini.SetValue("Alarms", "LowTempAlarmSoundFile", LowTempAlarmSoundFile);

			ini.SetValue("Alarms", "alarmhightemp", HighTempAlarmValue);
			ini.SetValue("Alarms", "HighTempAlarmSet", HighTempAlarmEnabled);
			ini.SetValue("Alarms", "HighTempAlarmSound", HighTempAlarmSound);
			ini.SetValue("Alarms", "HighTempAlarmSoundFile", HighTempAlarmSoundFile);

			ini.SetValue("Alarms", "alarmtempchange", TempChangeAlarmValue);
			ini.SetValue("Alarms", "TempChangeAlarmSet", TempChangeAlarmEnabled);
			ini.SetValue("Alarms", "TempChangeAlarmSound", TempChangeAlarmSound);
			ini.SetValue("Alarms", "TempChangeAlarmSoundFile", TempChangeAlarmSoundFile);

			ini.SetValue("Alarms", "alarmlowpress", LowPressAlarmValue);
			ini.SetValue("Alarms", "LowPressAlarmSet", LowPressAlarmEnabled);
			ini.SetValue("Alarms", "LowPressAlarmSound", LowPressAlarmSound);
			ini.SetValue("Alarms", "LowPressAlarmSoundFile", LowPressAlarmSoundFile);

			ini.SetValue("Alarms", "alarmhighpress", HighPressAlarmValue);
			ini.SetValue("Alarms", "HighPressAlarmSet", HighPressAlarmEnabled);
			ini.SetValue("Alarms", "HighPressAlarmSound", HighPressAlarmSound);
			ini.SetValue("Alarms", "HighPressAlarmSoundFile", HighPressAlarmSoundFile);

			ini.SetValue("Alarms", "alarmpresschange", PressChangeAlarmValue);
			ini.SetValue("Alarms", "PressChangeAlarmSet", PressChangeAlarmEnabled);
			ini.SetValue("Alarms", "PressChangeAlarmSound", PressChangeAlarmSound);
			ini.SetValue("Alarms", "PressChangeAlarmSoundFile", PressChangeAlarmSoundFile);

			ini.SetValue("Alarms", "alarmhighraintoday", HighRainTodayAlarmValue);
			ini.SetValue("Alarms", "HighRainTodayAlarmSet", HighRainTodayAlarmEnabled);
			ini.SetValue("Alarms", "HighRainTodayAlarmSound", HighRainTodayAlarmSound);
			ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", HighRainTodayAlarmSoundFile);

			ini.SetValue("Alarms", "alarmhighrainrate", HighRainRateAlarmValue);
			ini.SetValue("Alarms", "HighRainRateAlarmSet", HighRainRateAlarmEnabled);
			ini.SetValue("Alarms", "HighRainRateAlarmSound", HighRainRateAlarmSound);
			ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", HighRainRateAlarmSoundFile);

			ini.SetValue("Alarms", "alarmhighgust", HighGustAlarmValue);
			ini.SetValue("Alarms", "HighGustAlarmSet", HighGustAlarmEnabled);
			ini.SetValue("Alarms", "HighGustAlarmSound", HighGustAlarmSound);
			ini.SetValue("Alarms", "HighGustAlarmSoundFile", HighGustAlarmSoundFile);

			ini.SetValue("Alarms", "alarmhighwind", HighWindAlarmValue);
			ini.SetValue("Alarms", "HighWindAlarmSet", HighWindAlarmEnabled);
			ini.SetValue("Alarms", "HighWindAlarmSound", HighWindAlarmSound);
			ini.SetValue("Alarms", "HighWindAlarmSoundFile", HighWindAlarmSoundFile);

			ini.SetValue("Alarms", "SensorAlarmSet", SensorAlarmEnabled);
			ini.SetValue("Alarms", "SensorAlarmSound", SensorAlarmSound);
			ini.SetValue("Alarms", "SensorAlarmSoundFile", SensorAlarmSoundFile);

			ini.SetValue("Alarms", "DataStoppedAlarmSet", DataStoppedAlarmEnabled);
			ini.SetValue("Alarms", "DataStoppedAlarmSound", DataStoppedAlarmSound);
			ini.SetValue("Alarms", "DataStoppedAlarmSoundFile", DataStoppedAlarmSoundFile);

			ini.SetValue("Alarms", "BatteryLowAlarmSet", BatteryLowAlarmEnabled);
			ini.SetValue("Alarms", "BatteryLowAlarmSound", BatteryLowAlarmSound);
			ini.SetValue("Alarms", "BatteryLowAlarmSoundFile", BatteryLowAlarmSoundFile);

			ini.SetValue("Alarms", "DataSpikeAlarmSet", SpikeAlarmEnabled);
			ini.SetValue("Alarms", "DataSpikeAlarmSound", SpikeAlarmSound);
			ini.SetValue("Alarms", "DataSpikeAlarmSoundFile", SpikeAlarmSoundFile);

			ini.SetValue("Offsets", "PressOffset", PressOffset);
			ini.SetValue("Offsets", "TempOffset", TempOffset);
			ini.SetValue("Offsets", "HumOffset", HumOffset);
			ini.SetValue("Offsets", "WindDirOffset", WindDirOffset);
			ini.SetValue("Offsets", "InTempOffset", InTempoffset);
			ini.SetValue("Offsets", "UVOffset", UVOffset);
			ini.SetValue("Offsets", "SolarOffset", SolarOffset);
			ini.SetValue("Offsets", "WetBulbOffset", WetBulbOffset);
			//ini.SetValue("Offsets", "DavisCalcAltPressOffset", DavisCalcAltPressOffset);

			ini.SetValue("Offsets", "PressMult", PressMult);
			ini.SetValue("Offsets", "WindSpeedMult", WindSpeedMult);
			ini.SetValue("Offsets", "WindGustMult", WindGustMult);
			ini.SetValue("Offsets", "TempMult", TempMult);
			ini.SetValue("Offsets", "HumMult", HumMult);
			ini.SetValue("Offsets", "RainMult", RainMult);
			ini.SetValue("Offsets", "SolarMult", SolarMult);
			ini.SetValue("Offsets", "UVMult", UVMult);
			ini.SetValue("Offsets", "WetBulbMult", WetBulbMult);

			ini.SetValue("Limits", "TempHighC", LimitTempHigh);
			ini.SetValue("Limits", "TempLowC", LimitTempLow);
			ini.SetValue("Limits", "DewHighC", LimitDewHigh);
			ini.SetValue("Limits", "PressHighMB", LimitPressHigh);
			ini.SetValue("Limits", "PressLowMB", LimitPressLow);
			ini.SetValue("Limits", "WindHighMS", LimitWindHigh);

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

			LogMessage("Completed writing Cumulus.ini file");
		}

		private void ReadStringsFile()
		{
			if (File.Exists("strings.ini"))
			{
				IniFile ini = new IniFile("strings.ini");

				// forecast

				ForecastNotAvailable = ini.GetValue("Forecast", "notavailable", "Not available");

				exceptional = ini.GetValue("Forecast", "exceptional", "Exceptional Weather");
				z_forecast[0] = ini.GetValue("Forecast", "forecast1", "Settled fine");
				z_forecast[1] = ini.GetValue("Forecast", "forecast2", "Fine weather");
				z_forecast[2] = ini.GetValue("Forecast", "forecast3", "Becoming fine");
				z_forecast[3] = ini.GetValue("Forecast", "forecast4", "Fine, becoming less settled");
				z_forecast[4] = ini.GetValue("Forecast", "forecast5", "Fine, possible showers");
				z_forecast[5] = ini.GetValue("Forecast", "forecast6", "Fairly fine, improving");
				z_forecast[6] = ini.GetValue("Forecast", "forecast7", "Fairly fine, possible showers early");
				z_forecast[7] = ini.GetValue("Forecast", "forecast8", "Fairly fine, showery later");
				z_forecast[8] = ini.GetValue("Forecast", "forecast9", "Showery early, improving");
				z_forecast[9] = ini.GetValue("Forecast", "forecast10", "Changeable, mending");
				z_forecast[10] = ini.GetValue("Forecast", "forecast11", "Fairly fine, showers likely");
				z_forecast[11] = ini.GetValue("Forecast", "forecast12", "Rather unsettled clearing later");
				z_forecast[12] = ini.GetValue("Forecast", "forecast13", "Unsettled, probably improving");
				z_forecast[13] = ini.GetValue("Forecast", "forecast14", "Showery, bright intervals");
				z_forecast[14] = ini.GetValue("Forecast", "forecast15", "Showery, becoming less settled");
				z_forecast[15] = ini.GetValue("Forecast", "forecast16", "Changeable, some precipitation");
				z_forecast[16] = ini.GetValue("Forecast", "forecast17", "Unsettled, short fine intervals");
				z_forecast[17] = ini.GetValue("Forecast", "forecast18", "Unsettled, precipitation later");
				z_forecast[18] = ini.GetValue("Forecast", "forecast19", "Unsettled, some precipitation");
				z_forecast[19] = ini.GetValue("Forecast", "forecast20", "Mostly very unsettled");
				z_forecast[20] = ini.GetValue("Forecast", "forecast21", "Occasional precipitation, worsening");
				z_forecast[21] = ini.GetValue("Forecast", "forecast22", "Precipitation at times, very unsettled");
				z_forecast[22] = ini.GetValue("Forecast", "forecast23", "Precipitation at frequent intervals");
				z_forecast[23] = ini.GetValue("Forecast", "forecast24", "Precipitation, very unsettled");
				z_forecast[24] = ini.GetValue("Forecast", "forecast25", "Stormy, may improve");
				z_forecast[25] = ini.GetValue("Forecast", "forecast26", "Stormy, much precipitation");
				// moon phases
				Newmoon = ini.GetValue("MoonPhases", "Newmoon", "New moon");
				WaxingCrescent = ini.GetValue("MoonPhases", "WaxingCrescent", "Waxing Crescent");
				FirstQuarter = ini.GetValue("MoonPhases", "FirstQuarter", "First Quarter");
				WaxingGibbous = ini.GetValue("MoonPhases", "WaxingGibbous", "Waxing Gibbous");
				Fullmoon = ini.GetValue("MoonPhases", "Fullmoon", "Full moon");
				WaningGibbous = ini.GetValue("MoonPhases", "WaningGibbous", "Waning Gibbous");
				LastQuarter = ini.GetValue("MoonPhases", "LastQuarter", "Last Quarter");
				WaningCrescent = ini.GetValue("MoonPhases", "WaningCrescent", "Waning Crescent");
				// beaufort
				Calm = ini.GetValue("Beaufort", "Calm", "Calm");
				Lightair = ini.GetValue("Beaufort", "Lightair", "Light air");
				Lightbreeze = ini.GetValue("Beaufort", "Lightbreeze", "Light breeze");
				Gentlebreeze = ini.GetValue("Beaufort", "Gentlebreeze", "Gentle breeze");
				Moderatebreeze = ini.GetValue("Beaufort", "Moderatebreeze", "Moderate breeze");
				Freshbreeze = ini.GetValue("Beaufort", "Freshbreeze", "Fresh breeze");
				Strongbreeze = ini.GetValue("Beaufort", "Strongbreeze", "Strong breeze");
				Neargale = ini.GetValue("Beaufort", "Neargale", "Near gale");
				Gale = ini.GetValue("Beaufort", "Gale", "Gale");
				Stronggale = ini.GetValue("Beaufort", "Stronggale", "Strong gale");
				Storm = ini.GetValue("Beaufort", "Storm", "Storm");
				Violentstorm = ini.GetValue("Beaufort", "Violentstorm", "Violent storm");
				Hurricane = ini.GetValue("Beaufort", "Hurricane", "Hurricane");
				// trends
				Risingveryrapidly = ini.GetValue("Trends", "Risingveryrapidly", "Rising very rapidly");
				Risingquickly = ini.GetValue("Trends", "Risingquickly", "Rising quickly");
				Rising = ini.GetValue("Trends", "Rising", "Rising");
				Risingslowly = ini.GetValue("Trends", "Risingslowly", "Rising slowly");
				Steady = ini.GetValue("Trends", "Steady", "Steady");
				Fallingslowly = ini.GetValue("Trends", "Fallingslowly", "Falling slowly");
				Falling = ini.GetValue("Trends", "Falling", "Falling");
				Fallingquickly = ini.GetValue("Trends", "Fallingquickly", "Falling quickly");
				Fallingveryrapidly = ini.GetValue("Trends", "Fallingveryrapidly", "Falling very rapidly");
				// compass points
				compassp[0] = ini.GetValue("Compass", "N", "N");
				compassp[1] = ini.GetValue("Compass", "NNE", "NNE");
				compassp[2] = ini.GetValue("Compass", "NE", "NE");
				compassp[3] = ini.GetValue("Compass", "ENE", "ENE");
				compassp[4] = ini.GetValue("Compass", "E", "E");
				compassp[5] = ini.GetValue("Compass", "ESE", "ESE");
				compassp[6] = ini.GetValue("Compass", "SE", "SE");
				compassp[7] = ini.GetValue("Compass", "SSE", "SSE");
				compassp[8] = ini.GetValue("Compass", "S", "S");
				compassp[9] = ini.GetValue("Compass", "SSW", "SSW");
				compassp[10] = ini.GetValue("Compass", "SW", "SW");
				compassp[11] = ini.GetValue("Compass", "WSW", "WSW");
				compassp[12] = ini.GetValue("Compass", "W", "W");
				compassp[13] = ini.GetValue("Compass", "WNW", "WNW");
				compassp[14] = ini.GetValue("Compass", "NW", "NW");
				compassp[15] = ini.GetValue("Compass", "NNW", "NNW");
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
				WMR200ExtraChannelCaptions[1] = ini.GetValue("ExtraSensorCaptions", "Solar", "Solar");
				WMR200ExtraChannelCaptions[2] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel2", "Extra Channel 2");
				WMR200ExtraChannelCaptions[3] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel3", "Extra Channel 3");
				WMR200ExtraChannelCaptions[4] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel4", "Extra Channel 4");
				WMR200ExtraChannelCaptions[5] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel5", "Extra Channel 5");
				WMR200ExtraChannelCaptions[6] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel6", "Extra Channel 6");
				WMR200ExtraChannelCaptions[7] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel7", "Extra Channel 7");
				WMR200ExtraChannelCaptions[8] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel8", "Extra Channel 8");
				WMR200ExtraChannelCaptions[9] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel9", "Extra Channel 9");
				WMR200ExtraChannelCaptions[10] = ini.GetValue("ExtraSensorCaptions", "ExtraChannel10", "Extra Channel 10");

				// Extra temperature captions (for Extra Sensor Data screen)
				ExtraTempCaptions[1] = ini.GetValue("ExtraTempCaptions", "Sensor1", "Sensor 1");
				ExtraTempCaptions[2] = ini.GetValue("ExtraTempCaptions", "Sensor2", "Sensor 2");
				ExtraTempCaptions[3] = ini.GetValue("ExtraTempCaptions", "Sensor3", "Sensor 3");
				ExtraTempCaptions[4] = ini.GetValue("ExtraTempCaptions", "Sensor4", "Sensor 4");
				ExtraTempCaptions[5] = ini.GetValue("ExtraTempCaptions", "Sensor5", "Sensor 5");
				ExtraTempCaptions[6] = ini.GetValue("ExtraTempCaptions", "Sensor6", "Sensor 6");
				ExtraTempCaptions[7] = ini.GetValue("ExtraTempCaptions", "Sensor7", "Sensor 7");
				ExtraTempCaptions[8] = ini.GetValue("ExtraTempCaptions", "Sensor8", "Sensor 8");
				ExtraTempCaptions[9] = ini.GetValue("ExtraTempCaptions", "Sensor9", "Sensor 9");
				ExtraTempCaptions[10] = ini.GetValue("ExtraTempCaptions", "Sensor10", "Sensor 10");

				// Extra humidity captions (for Extra Sensor Data screen)
				ExtraHumCaptions[1] = ini.GetValue("ExtraHumCaptions", "Sensor1", "Sensor 1");
				ExtraHumCaptions[2] = ini.GetValue("ExtraHumCaptions", "Sensor2", "Sensor 2");
				ExtraHumCaptions[3] = ini.GetValue("ExtraHumCaptions", "Sensor3", "Sensor 3");
				ExtraHumCaptions[4] = ini.GetValue("ExtraHumCaptions", "Sensor4", "Sensor 4");
				ExtraHumCaptions[5] = ini.GetValue("ExtraHumCaptions", "Sensor5", "Sensor 5");
				ExtraHumCaptions[6] = ini.GetValue("ExtraHumCaptions", "Sensor6", "Sensor 6");
				ExtraHumCaptions[7] = ini.GetValue("ExtraHumCaptions", "Sensor7", "Sensor 7");
				ExtraHumCaptions[8] = ini.GetValue("ExtraHumCaptions", "Sensor8", "Sensor 8");
				ExtraHumCaptions[9] = ini.GetValue("ExtraHumCaptions", "Sensor9", "Sensor 9");
				ExtraHumCaptions[10] = ini.GetValue("ExtraHumCaptions", "Sensor10", "Sensor 10");

				// Extra dew point captions (for Extra Sensor Data screen)
				ExtraDPCaptions[1] = ini.GetValue("ExtraDPCaptions", "Sensor1", "Sensor 1");
				ExtraDPCaptions[2] = ini.GetValue("ExtraDPCaptions", "Sensor2", "Sensor 2");
				ExtraDPCaptions[3] = ini.GetValue("ExtraDPCaptions", "Sensor3", "Sensor 3");
				ExtraDPCaptions[4] = ini.GetValue("ExtraDPCaptions", "Sensor4", "Sensor 4");
				ExtraDPCaptions[5] = ini.GetValue("ExtraDPCaptions", "Sensor5", "Sensor 5");
				ExtraDPCaptions[6] = ini.GetValue("ExtraDPCaptions", "Sensor6", "Sensor 6");
				ExtraDPCaptions[7] = ini.GetValue("ExtraDPCaptions", "Sensor7", "Sensor 7");
				ExtraDPCaptions[8] = ini.GetValue("ExtraDPCaptions", "Sensor8", "Sensor 8");
				ExtraDPCaptions[9] = ini.GetValue("ExtraDPCaptions", "Sensor9", "Sensor 9");
				ExtraDPCaptions[10] = ini.GetValue("ExtraDPCaptions", "Sensor10", "Sensor 10");

				// soil temp captions (for Extra Sensor Data screen)
				SoilTempCaptions[1] = ini.GetValue("SoilTempCaptions", "Sensor1", "Sensor 1");
				SoilTempCaptions[2] = ini.GetValue("SoilTempCaptions", "Sensor2", "Sensor 2");
				SoilTempCaptions[3] = ini.GetValue("SoilTempCaptions", "Sensor3", "Sensor 3");
				SoilTempCaptions[4] = ini.GetValue("SoilTempCaptions", "Sensor4", "Sensor 4");
				SoilTempCaptions[5] = ini.GetValue("SoilTempCaptions", "Sensor5", "Sensor 5");
				SoilTempCaptions[6] = ini.GetValue("SoilTempCaptions", "Sensor6", "Sensor 6");
				SoilTempCaptions[7] = ini.GetValue("SoilTempCaptions", "Sensor7", "Sensor 7");
				SoilTempCaptions[8] = ini.GetValue("SoilTempCaptions", "Sensor8", "Sensor 8");
				SoilTempCaptions[9] = ini.GetValue("SoilTempCaptions", "Sensor9", "Sensor 9");
				SoilTempCaptions[10] = ini.GetValue("SoilTempCaptions", "Sensor10", "Sensor 10");
				SoilTempCaptions[11] = ini.GetValue("SoilTempCaptions", "Sensor11", "Sensor 11");
				SoilTempCaptions[12] = ini.GetValue("SoilTempCaptions", "Sensor12", "Sensor 12");
				SoilTempCaptions[13] = ini.GetValue("SoilTempCaptions", "Sensor13", "Sensor 13");
				SoilTempCaptions[14] = ini.GetValue("SoilTempCaptions", "Sensor14", "Sensor 14");
				SoilTempCaptions[15] = ini.GetValue("SoilTempCaptions", "Sensor15", "Sensor 15");
				SoilTempCaptions[16] = ini.GetValue("SoilTempCaptions", "Sensor16", "Sensor 16");

				// soil moisture captions (for Extra Sensor Data screen)
				SoilMoistureCaptions[1] = ini.GetValue("SoilMoistureCaptions", "Sensor1", "Sensor 1");
				SoilMoistureCaptions[2] = ini.GetValue("SoilMoistureCaptions", "Sensor2", "Sensor 2");
				SoilMoistureCaptions[3] = ini.GetValue("SoilMoistureCaptions", "Sensor3", "Sensor 3");
				SoilMoistureCaptions[4] = ini.GetValue("SoilMoistureCaptions", "Sensor4", "Sensor 4");
				SoilMoistureCaptions[5] = ini.GetValue("SoilMoistureCaptions", "Sensor5", "Sensor 5");
				SoilMoistureCaptions[6] = ini.GetValue("SoilMoistureCaptions", "Sensor6", "Sensor 6");
				SoilMoistureCaptions[7] = ini.GetValue("SoilMoistureCaptions", "Sensor7", "Sensor 7");
				SoilMoistureCaptions[8] = ini.GetValue("SoilMoistureCaptions", "Sensor8", "Sensor 8");
				SoilMoistureCaptions[9] = ini.GetValue("SoilMoistureCaptions", "Sensor9", "Sensor 9");
				SoilMoistureCaptions[10] = ini.GetValue("SoilMoistureCaptions", "Sensor10", "Sensor 10");
				SoilMoistureCaptions[11] = ini.GetValue("SoilMoistureCaptions", "Sensor11", "Sensor 11");
				SoilMoistureCaptions[12] = ini.GetValue("SoilMoistureCaptions", "Sensor12", "Sensor 12");
				SoilMoistureCaptions[13] = ini.GetValue("SoilMoistureCaptions", "Sensor13", "Sensor 13");
				SoilMoistureCaptions[14] = ini.GetValue("SoilMoistureCaptions", "Sensor14", "Sensor 14");
				SoilMoistureCaptions[15] = ini.GetValue("SoilMoistureCaptions", "Sensor15", "Sensor 15");
				SoilMoistureCaptions[16] = ini.GetValue("SoilMoistureCaptions", "Sensor16", "Sensor 16");

				// leaf temp/wetness captions (for Extra Sensor Data screen)
				LeafCaptions[1] = ini.GetValue("LeafTempCaptions", "Sensor1", "Sensor 1");
				LeafCaptions[2] = ini.GetValue("LeafTempCaptions", "Sensor2", "Sensor 2");
				LeafCaptions[3] = ini.GetValue("LeafWetnessCaptions", "Sensor1", "Sensor 1");
				LeafCaptions[4] = ini.GetValue("LeafWetnessCaptions", "Sensor2", "Sensor 2");

				// air quality captions (for Extra Sensor Data screen)
				AirQualityCaptions[1] = ini.GetValue("AirQualityCaptions", "Sensor1", "Sensor 1");
				AirQualityCaptions[2] = ini.GetValue("AirQualityCaptions", "Sensor2", "Sensor 2");
				AirQualityCaptions[3] = ini.GetValue("AirQualityCaptions", "Sensor3", "Sensor 3");
				AirQualityCaptions[4] = ini.GetValue("AirQualityCaptions", "Sensor4", "Sensor 4");
				AirQualityAvgCaptions[1] = ini.GetValue("AirQualityCaptions", "SensorAvg1", "Sensor Avg 1");
				AirQualityAvgCaptions[2] = ini.GetValue("AirQualityCaptions", "SensorAvg2", "Sensor Avg 2");
				AirQualityAvgCaptions[3] = ini.GetValue("AirQualityCaptions", "SensorAvg3", "Sensor Avg 3");
				AirQualityAvgCaptions[4] = ini.GetValue("AirQualityCaptions", "SensorAvg4", "Sensor Avg 4");

				// User temperature captions (for Extra Sensor Data screen)
				UserTempCaptions[1] = ini.GetValue("UserTempCaptions", "Sensor1", "Sensor 1");
				UserTempCaptions[2] = ini.GetValue("UserTempCaptions", "Sensor2", "Sensor 2");
				UserTempCaptions[3] = ini.GetValue("UserTempCaptions", "Sensor3", "Sensor 3");
				UserTempCaptions[4] = ini.GetValue("UserTempCaptions", "Sensor4", "Sensor 4");
				UserTempCaptions[5] = ini.GetValue("UserTempCaptions", "Sensor5", "Sensor 5");
				UserTempCaptions[6] = ini.GetValue("UserTempCaptions", "Sensor6", "Sensor 6");
				UserTempCaptions[7] = ini.GetValue("UserTempCaptions", "Sensor7", "Sensor 7");
				UserTempCaptions[8] = ini.GetValue("UserTempCaptions", "Sensor8", "Sensor 8");

				thereWillBeMinSLessDaylightTomorrow = ini.GetValue("Solar", "LessDaylightTomorrow", "There will be {0}min {1}s less daylight tomorrow");
				thereWillBeMinSMoreDaylightTomorrow = ini.GetValue("Solar", "MoreDaylightTomorrow", "There will be {0}min {1}s more daylight tomorrow");

				DavisForecast1[0] = ini.GetValue("DavisForecast1", "forecast1", "FORECAST REQUIRES 3 HRS. OF RECENT DATA");
				DavisForecast1[1] = ini.GetValue("DavisForecast1", "forecast2", "Mostly cloudy with little temperature change. ");
				DavisForecast1[2] = ini.GetValue("DavisForecast1", "forecast3", "Mostly cloudy and cooler. ");
				DavisForecast1[3] = ini.GetValue("DavisForecast1", "forecast4", "Clearing, cooler and windy. ");
				DavisForecast1[4] = ini.GetValue("DavisForecast1", "forecast5", "Clearing and cooler. ");
				DavisForecast1[5] = ini.GetValue("DavisForecast1", "forecast6", "Increasing clouds and cooler. ");
				DavisForecast1[6] = ini.GetValue("DavisForecast1", "forecast7", "Increasing clouds with little temperature change. ");
				DavisForecast1[7] = ini.GetValue("DavisForecast1", "forecast8", "Increasing clouds and warmer. ");
				DavisForecast1[8] = ini.GetValue("DavisForecast1", "forecast9", "Mostly clear for 12 to 24 hours with little temperature change. ");
				DavisForecast1[9] = ini.GetValue("DavisForecast1", "forecast10", "Mostly clear for 6 to 12 hours with little temperature change. ");
				DavisForecast1[10] = ini.GetValue("DavisForecast1", "forecast11", "Mostly clear and warmer. ");
				DavisForecast1[11] = ini.GetValue("DavisForecast1", "forecast12", "Mostly clear for 12 to 24 hours and cooler. ");
				DavisForecast1[12] = ini.GetValue("DavisForecast1", "forecast13", "Mostly clear for 12 hours with little temperature change. ");
				DavisForecast1[13] = ini.GetValue("DavisForecast1", "forecast14", "Mostly clear with little temperature change. ");
				DavisForecast1[14] = ini.GetValue("DavisForecast1", "forecast15", "Mostly clear and cooler. ");
				DavisForecast1[15] = ini.GetValue("DavisForecast1", "forecast16", "Partially cloudy, Rain and/or snow possible or continuing. ");
				DavisForecast1[16] = ini.GetValue("DavisForecast1", "forecast17", "Partially cloudy, Snow possible or continuing. ");
				DavisForecast1[17] = ini.GetValue("DavisForecast1", "forecast18", "Partially cloudy, Rain possible or continuing. ");
				DavisForecast1[18] = ini.GetValue("DavisForecast1", "forecast19", "Mostly cloudy, Rain and/or snow possible or continuing. ");
				DavisForecast1[19] = ini.GetValue("DavisForecast1", "forecast20", "Mostly cloudy, Snow possible or continuing. ");
				DavisForecast1[20] = ini.GetValue("DavisForecast1", "forecast21", "Mostly cloudy, Rain possible or continuing. ");
				DavisForecast1[21] = ini.GetValue("DavisForecast1", "forecast22", "Mostly cloudy. ");
				DavisForecast1[22] = ini.GetValue("DavisForecast1", "forecast23", "Partially cloudy. ");
				DavisForecast1[23] = ini.GetValue("DavisForecast1", "forecast24", "Mostly clear. ");
				DavisForecast1[24] = ini.GetValue("DavisForecast1", "forecast25", "Partly cloudy with little temperature change. ");
				DavisForecast1[25] = ini.GetValue("DavisForecast1", "forecast26", "Partly cloudy and cooler. ");
				DavisForecast1[26] = ini.GetValue("DavisForecast1", "forecast27", "Unknown forecast rule.");

				DavisForecast2[0] = ini.GetValue("DavisForecast2", "forecast1", "");
				DavisForecast2[1] = ini.GetValue("DavisForecast2", "forecast2", "Precipitation possible within 48 hours. ");
				DavisForecast2[2] = ini.GetValue("DavisForecast2", "forecast3", "Precipitation possible within 24 to 48 hours. ");
				DavisForecast2[3] = ini.GetValue("DavisForecast2", "forecast4", "Precipitation possible within 24 hours. ");
				DavisForecast2[4] = ini.GetValue("DavisForecast2", "forecast5", "Precipitation possible within 12 to 24 hours. ");
				DavisForecast2[5] = ini.GetValue("DavisForecast2", "forecast6", "Precipitation possible within 12 hours, possibly heavy at times. ");
				DavisForecast2[6] = ini.GetValue("DavisForecast2", "forecast7", "Precipitation possible within 12 hours. ");
				DavisForecast2[7] = ini.GetValue("DavisForecast2", "forecast8", "Precipitation possible within 6 to 12 hours. ");
				DavisForecast2[8] = ini.GetValue("DavisForecast2", "forecast9", "Precipitation possible within 6 to 12 hours, possibly heavy at times. ");
				DavisForecast2[9] = ini.GetValue("DavisForecast2", "forecast10", "Precipitation possible and windy within 6 hours. ");
				DavisForecast2[10] = ini.GetValue("DavisForecast2", "forecast11", "Precipitation possible within 6 hours. ");
				DavisForecast2[11] = ini.GetValue("DavisForecast2", "forecast12", "Precipitation ending in 12 to 24 hours. ");
				DavisForecast2[12] = ini.GetValue("DavisForecast2", "forecast13", "Precipitation possibly heavy at times and ending within 12 hours. ");
				DavisForecast2[13] = ini.GetValue("DavisForecast2", "forecast14", "Precipitation ending within 12 hours. ");
				DavisForecast2[14] = ini.GetValue("DavisForecast2", "forecast15", "Precipitation ending within 6 hours. ");
				DavisForecast2[15] = ini.GetValue("DavisForecast2", "forecast16", "Precipitation likely, possibly heavy at times. ");
				DavisForecast2[16] = ini.GetValue("DavisForecast2", "forecast17", "Precipitation likely. ");
				DavisForecast2[17] = ini.GetValue("DavisForecast2", "forecast18", "Precipitation continuing, possibly heavy at times. ");
				DavisForecast2[18] = ini.GetValue("DavisForecast2", "forecast19", "Precipitation continuing. ");

				DavisForecast3[0] = ini.GetValue("DavisForecast3", "forecast1", "");
				DavisForecast3[1] = ini.GetValue("DavisForecast3", "forecast2", "Windy with possible wind shift to the W, SW, or S.");
				DavisForecast3[2] = ini.GetValue("DavisForecast3", "forecast3", "Possible wind shift to the W, SW, or S.");
				DavisForecast3[3] = ini.GetValue("DavisForecast3", "forecast4", "Windy with possible wind shift to the W, NW, or N.");
				DavisForecast3[4] = ini.GetValue("DavisForecast3", "forecast5", "Possible wind shift to the W, NW, or N.");
				DavisForecast3[5] = ini.GetValue("DavisForecast3", "forecast6", "Windy.");
				DavisForecast3[6] = ini.GetValue("DavisForecast3", "forecast7", "Increasing winds.");
			}
		}

		public string DataStoppedAlarmSoundFile { get; set; }
		public bool DataStoppedAlarmSound { get; set; }
		public bool DataStoppedAlarmEnabled { get; set; }

		public string BatteryLowAlarmSoundFile { get; set; }
		public bool BatteryLowAlarmSound { get; set; }
		public bool BatteryLowAlarmEnabled { get; set; }
		public bool BatteryLowAlarmState = false;

		public string SensorAlarmSoundFile { get; set; }
		public bool SensorAlarmSound { get; set; }
		public bool SensorAlarmEnabled { get; set; }
		public bool SensorAlarmState = false;

		public string SpikeAlarmSoundFile { get; set; }
		public bool SpikeAlarmSound { get; set; }
		public bool SpikeAlarmEnabled { get; set; }
		public bool SpikeAlarmState = false;

		public bool HighWindAlarmSound { get; set; }
		public string HighWindAlarmSoundFile { get; set; }
		public bool HighWindAlarmEnabled { get; set; }
		public double HighWindAlarmValue { get; set; }
		public bool HighWindAlarmState = false;

		public string HighGustAlarmSoundFile { get; set; }
		public bool HighGustAlarmSound { get; set; }
		public bool HighGustAlarmEnabled { get; set; }
		public double HighGustAlarmValue { get; set; }
		public bool HighGustAlarmState = false;

		public string HighRainRateAlarmSoundFile { get; set; }
		public bool HighRainRateAlarmSound { get; set; }
		public bool HighRainRateAlarmEnabled { get; set; }
		public double HighRainRateAlarmValue { get; set; }
		public bool HighRainRateAlarmState = false;

		public string HighRainTodayAlarmSoundFile { get; set; }
		public bool HighRainTodayAlarmSound { get; set; }
		public bool HighRainTodayAlarmEnabled { get; set; }
		public double HighRainTodayAlarmValue { get; set; }
		public bool HighRainTodayAlarmState = false;

		public string PressChangeAlarmSoundFile { get; set; }
		public bool PressChangeAlarmSound { get; set; }
		public bool PressChangeAlarmEnabled { get; set; }
		public double PressChangeAlarmValue { get; set; }

		public bool PressChangeUpAlarmState = false;
		public bool PressChangeDownAlarmState = false;

		public string HighPressAlarmSoundFile { get; set; }
		public bool HighPressAlarmSound { get; set; }
		public bool HighPressAlarmEnabled { get; set; }
		public double HighPressAlarmValue { get; set; }
		public bool HighPressAlarmState = false;

		public string LowPressAlarmSoundFile { get; set; }
		public bool LowPressAlarmSound { get; set; }
		public bool LowPressAlarmEnabled { get; set; }
		public double LowPressAlarmValue { get; set; }
		public bool LowPressAlarmState = false;

		public string TempChangeAlarmSoundFile { get; set; }
		public bool TempChangeAlarmSound { get; set; }
		public bool TempChangeAlarmEnabled { get; set; }
		public double TempChangeAlarmValue { get; set; }

		public bool TempChangeUpAlarmState = false;
		public bool TempChangeDownAlarmState = false;

		public string HighTempAlarmSoundFile { get; set; }
		public bool HighTempAlarmSound { get; set; }
		public bool HighTempAlarmEnabled { get; set; }
		public double HighTempAlarmValue { get; set; }
		public bool HighTempAlarmState = false;

		public string LowTempAlarmSoundFile { get; set; }
		public bool LowTempAlarmSound { get; set; }
		public bool LowTempAlarmEnabled { get; set; }
		public bool LowTempAlarmState = false;
		public double LowTempAlarmValue { get; set; }

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

		public Timer RealtimeTimer = new Timer();

		internal Timer CustomMysqlSecondsTimer;

		public bool ActiveFTPMode { get; set; }

		public FtpProtocols Sslftp { get; set; }

		public string SshftpAuthentication { get; set; }

		public string SshftpPskFile { get; set; }

		public bool DisableFtpsEPSV { get; set; }

		public bool DisableFtpsExplicit { get; set; }

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

		public double SpikeMaxHourlyRain { get; set; }

		public double SpikeMaxRainRate { get; set; }

		public double SpikeWindDiff { get; set; }

		public double SpikeGustDiff { get; set; }

		public double SpikeHumidityDiff { get; set; }

		public double SpikePressDiff { get; set; }

		public double SpikeTempDiff { get; set; }

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

		private WeatherStation Station
		{
			set { station = value; }
			get { return station; }

		}

		// WeatherLink Live transmitter Ids and indexes
		public string WllApiKey;
		public string WllApiSecret;
		public string WllStationId;

		public int WllBroadcastDuration = 300;
		public int WllBroadcastPort = 22222;
		public bool WLLAutoUpdateIpAddress = true;
		public int WllPrimaryWind = 1;
		public int WllPrimaryTempHum = 1;
		public int WllPrimaryRain = 1;
		public int WllPrimarySolar = 0;
		public int WllPrimaryUV = 0;

		public int WllExtraSoilTempTx1 = 0;
		public int WllExtraSoilTempIdx1 = 1;
		public int WllExtraSoilTempTx2 = 0;
		public int WllExtraSoilTempIdx2 = 2;
		public int WllExtraSoilTempTx3 = 0;
		public int WllExtraSoilTempIdx3 = 3;
		public int WllExtraSoilTempTx4 = 0;
		public int WllExtraSoilTempIdx4 = 4;

		public int WllExtraSoilMoistureTx1 = 0;
		public int WllExtraSoilMoistureIdx1 = 1;
		public int WllExtraSoilMoistureTx2 = 0;
		public int WllExtraSoilMoistureIdx2 = 2;
		public int WllExtraSoilMoistureTx3 = 0;
		public int WllExtraSoilMoistureIdx3 = 3;
		public int WllExtraSoilMoistureTx4 = 0;
		public int WllExtraSoilMoistureIdx4 = 4;

		public int WllExtraLeafTx1 = 0;
		public int WllExtraLeafIdx1 = 1;
		public int WllExtraLeafTx2 = 0;
		public int WllExtraLeafIdx2 = 2;

		public int[] WllExtraTempTx = { 0, 0, 0, 0, 0, 0, 0, 0 };

		public bool[] WllExtraHumTx = { false, false, false, false, false, false, false, false };

		public string Gw1000IpAddress;
		public bool Gw1000AutoUpdateIpAddress = true;

		public Timer WundTimer = new Timer();
		public Timer WindyTimer = new Timer();
		public Timer PWSTimer = new Timer();
		public Timer WOWTimer = new Timer();
		public Timer APRStimer = new Timer();
		public Timer WebTimer = new Timer();
		public Timer TwitterTimer = new Timer();
		public Timer AwekasTimer = new Timer();
		public Timer WCloudTimer = new Timer();
		public Timer MQTTTimer = new Timer();

		public int DAVIS = 0;
		public int OREGON = 1;
		public int EW = 2;
		public int LACROSSE = 3;
		public int OREGONUSB = 4;
		public int INSTROMET = 5;
		public int ECOWITT = 6;
		public bool startingup = true;
		public bool StartOfDayBackupNeeded = false;
		public string ReportPath = "Reports";
		public string LatestError;
		public DateTime LatestErrorTS = DateTime.MinValue;
		//public DateTime defaultRecordTS = new DateTime(2000, 1, 1, 0, 0, 0);
		public DateTime defaultRecordTS = DateTime.MinValue;
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
		public bool IncludeMoonImage;
		public bool TwitterSendLocation;
		private const int numwebtextfiles = 9;
		private readonly FtpClient RealtimeFTP = new FtpClient();
		private SftpClient RealtimeSSH;
		private volatile bool RealtimeFtpInProgress = false;
		private volatile bool RealtimeCopyInProgress = false;
		private byte RealtimeCycleCounter;
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
		private readonly string[] localgraphdatafiles;
		private readonly string[] remotegraphdatafiles;
		public string exceptional;
//		private WebSocketServer wsServer;
		public string[] WMR200ExtraChannelCaptions = new string[11];
		public string[] ExtraTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
		public string[] ExtraHumCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
		public string[] ExtraDPCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10" };
		public string[] SoilTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10", "Sensor 11", "Sensor 12", "Sensor 13", "Sensor 14", "Sensor 15", "Sensor 16" };
		public string[] SoilMoistureCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8", "Sensor 9", "Sensor 10", "Sensor 11", "Sensor 12", "Sensor 13", "Sensor 14", "Sensor 15", "Sensor 16" };
		public string[] AirQualityCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4" };
		public string[] AirQualityAvgCaptions = { "", "Sensor Avg 1", "Sensor Avg 2", "Sensor Avg 3", "Sensor Avg 4" };
		public string[] LeafCaptions = { "", "Temp 1", "Temp 2", "Wetness 1", "Wetness 2" };
		public string[] UserTempCaptions = { "", "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4", "Sensor 5", "Sensor 6", "Sensor 7", "Sensor 8" };
		private string thereWillBeMinSLessDaylightTomorrow = "There will be {0}min {1}s less daylight tomorrow";
		private string thereWillBeMinSMoreDaylightTomorrow = "There will be {0}min {1}s more daylight tomorrow";

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

		public const int NumLogFileFields = 29;

		public void DoLogFile(DateTime timestamp, bool live) // Writes an entry to the n-minute logfile. Fields are comma-separated:
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
		{
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

			if (StartOfDayBackupNeeded)
			{
				Backupdata(true, timestamp);
				StartOfDayBackupNeeded = false;
			}

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
					try
					{
						using (MySqlCommand cmd = new MySqlCommand())
						{
							cmd.CommandText = queryString;
							cmd.Connection = MonthlyMySqlConn;
							LogDebugMessage("DoLogFile: MySQL -  " + queryString);
							MonthlyMySqlConn.Open();
							int aff = cmd.ExecuteNonQuery();
							LogMessage("MySQL: Table " + MySqlMonthlyTable + " " + aff + " rows were affected.");
						}
					}
					catch (Exception ex)
					{
						LogMessage("DoLogFile: Error encountered during Monthly MySQL operation.");
						LogMessage(ex.Message);
					}
					finally
					{
						try
						{
							MonthlyMySqlConn.Close();
						}
						catch { }
					}
				}
				else
				{
					// save the string for later
					MySqlList.Add(queryString);
				}
			}
		}

		public const int NumExtraLogFileFields = 84;

		public void DoExtraLogFile(DateTime timestamp)
		{
			var filename = GetExtraLogFileName(timestamp);

			using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read))
			using (StreamWriter file = new StreamWriter(fs))
			{
				file.Write(timestamp.ToString("dd/MM/yy") + ListSeparator);
				file.Write(timestamp.ToString("HH:mm") + ListSeparator);

				for (int i = 1; i < 11; i++)
				{
					file.Write(station.ExtraTemp[i].ToString(TempFormat) + ListSeparator);
				}
				for (int i = 1; i < 11; i++)
				{
					file.Write(station.ExtraHum[i].ToString(HumFormat) + ListSeparator);
				}
				for (int i = 1; i < 11; i++)
				{
					file.Write(station.ExtraDewPoint[i].ToString(TempFormat) + ListSeparator);
				}

				file.Write(station.SoilTemp1.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp2.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp3.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp4.ToString(TempFormat) + ListSeparator);

				file.Write(station.SoilMoisture1 + ListSeparator);
				file.Write(station.SoilMoisture2 + ListSeparator);
				file.Write(station.SoilMoisture3 + ListSeparator);
				file.Write(station.SoilMoisture4 + ListSeparator);

				file.Write(station.LeafTemp1.ToString(TempFormat) + ListSeparator);
				file.Write(station.LeafTemp2.ToString(TempFormat) + ListSeparator);

				file.Write(station.LeafWetness1 + ListSeparator);
				file.Write(station.LeafWetness2 + ListSeparator);

				file.Write(station.SoilTemp5.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp6.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp7.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp8.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp9.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp10.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp11.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp12.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp13.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp14.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp15.ToString(TempFormat) + ListSeparator);
				file.Write(station.SoilTemp16.ToString(TempFormat) + ListSeparator);

				file.Write(station.SoilMoisture5 + ListSeparator);
				file.Write(station.SoilMoisture6 + ListSeparator);
				file.Write(station.SoilMoisture7 + ListSeparator);
				file.Write(station.SoilMoisture8 + ListSeparator);
				file.Write(station.SoilMoisture9 + ListSeparator);
				file.Write(station.SoilMoisture10 + ListSeparator);
				file.Write(station.SoilMoisture11 + ListSeparator);
				file.Write(station.SoilMoisture12 + ListSeparator);
				file.Write(station.SoilMoisture13 + ListSeparator);
				file.Write(station.SoilMoisture14 + ListSeparator);
				file.Write(station.SoilMoisture15 + ListSeparator);
				file.Write(station.SoilMoisture16 + ListSeparator);

				file.Write(station.AirQuality1.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQuality2.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQuality3.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQuality4.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQualityAvg1.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQualityAvg2.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQualityAvg3.ToString(AirQualityFormat) + ListSeparator);
				file.Write(station.AirQualityAvg4.ToString(AirQualityFormat) + ListSeparator);

				for (int i = 1; i < 8; i++)
				{
					file.Write(station.UserTemp[i].ToString(TempFormat) + ListSeparator);
				}
				file.Write(station.UserTemp[8].ToString(TempFormat));

				file.WriteLine();
				file.Close();
			}
		}

		private void Backupdata(bool daily, DateTime timestamp)
		{
			string dirpath = daily ? Backuppath + "daily" + DirectorySeparator : Backuppath;

			if (!Directory.Exists(dirpath))
			{
				LogMessage("*** Error - backup folder does not exist - " + dirpath);
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
						LogMessage("*** Error - the backup folder has unexpected contents");
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

				LogMessage("Creating backup folder " + foldername);

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
				var logbackup = foldername + LogFile.Replace(LogFilePath, "");

				var extraFile = GetExtraLogFileName(timestamp);
				var extraBackup = foldername + extraFile.Replace(LogFilePath, "");


				if (!Directory.Exists(foldername))
				{
					Directory.CreateDirectory(foldername);
					if (File.Exists(AlltimeIniFile))
					{
						File.Copy(AlltimeIniFile, alltimebackup);
					}
					if (File.Exists(MonthlyAlltimeIniFile))
					{
						File.Copy(MonthlyAlltimeIniFile, monthlyAlltimebackup);
					}
					if (File.Exists(DayFile))
					{
						File.Copy(DayFile, daybackup);
					}
					if (File.Exists(TodayIniFile))
					{
						File.Copy(TodayIniFile, todaybackup);
					}
					if (File.Exists(YesterdayFile))
					{
						File.Copy(YesterdayFile, yesterdaybackup);
					}
					if (File.Exists(LogFile))
					{
						File.Copy(LogFile, logbackup);
					}
					if (File.Exists(MonthIniFile))
					{
						File.Copy(MonthIniFile, monthbackup);
					}
					if (File.Exists(YearIniFile))
					{
						File.Copy(YearIniFile, yearbackup);
					}
					if (File.Exists(diaryfile))
					{
						File.Copy(diaryfile, diarybackup);
					}
					if (File.Exists("Cumulus.ini"))
					{
						File.Copy("Cumulus.ini", configbackup);
					}
					if (File.Exists(extraFile))
					{
						File.Copy(extraFile, extraBackup);
					}

					// Do not do this extra backup between 00:00 & Rollover hour on the first of the month
					// as the month has not yet rolled over - only applies for start-up backups
					if (timestamp.Day == 1 && timestamp.Hour >= RolloverHour)
					{
						// on the first of month, we also need to backup last months files as well
						var LogFile2 = GetLogFileName(timestamp.AddDays(-1));
						var logbackup2 = foldername + LogFile2.Replace(LogFilePath, "");

						var extraFile2 = GetExtraLogFileName(timestamp.AddDays(-1));
						var extraBackup2 = foldername + extraFile2.Replace(LogFilePath, "");

						if (File.Exists(LogFile2))
						{
							File.Copy(LogFile2, logbackup2, true);
						}
						if (File.Exists(extraFile2))
						{
							File.Copy(extraFile2, extraBackup2, true);
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

				data.MonthHigh = station.HighGustThisMonth;
				data.MonthHighDT = station.HighGustThisMonthTS.ToLocalTime();

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

			WriteIniFile();

			//httpServer.Stop();

			//if (httpServer != null) httpServer.Dispose();

			// Stop the timers
			if (RealtimeEnabled) {

			}

			if (station != null)
			{
				LogMessage("Station stopping");
				station.Stop();

				LogMessage("Station stopped");

				if (station.HaveReadData)
				{
					LogMessage("Writing today.ini file");
					station.WriteTodayFile(DateTime.Now, false);
					LogMessage("Completed writing today.ini file");
				}
				else
					LogMessage("No data read this session, today.ini not written");
			}
			LogMessage("Station shutdown complete");
		}

		public void ExecuteProgram(string externalProgram, string externalParams)
		{
			// Prepare the process to run
			ProcessStartInfo start = new ProcessStartInfo();
			// Enter in the command line arguments
			start.Arguments = externalParams;
			// Enter the executable to run, including the complete path
			start.FileName = externalProgram;
			// Dont show a console window
			start.CreateNoWindow = true;
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

				//LogDebugMessage("Creating standard HTML files");
				ProcessTemplateFile(IndexTFile, Indexfile, tokenParser);
				ProcessTemplateFile(TodayTFile, Todayfile, tokenParser);
				ProcessTemplateFile(YesterdayTFile, Yesterfile, tokenParser);
				ProcessTemplateFile(RecordTFile, Recordfile, tokenParser);
				ProcessTemplateFile(MonthlyRecordTFile, MonthlyRecordfile, tokenParser);
				ProcessTemplateFile(TrendsTFile, Trendsfile, tokenParser);
				ProcessTemplateFile(ThisMonthTFile, ThisMonthfile, tokenParser);
				ProcessTemplateFile(ThisYearTFile, ThisYearfile, tokenParser);
				ProcessTemplateFile(GaugesTFile, Gaugesfile, tokenParser);
				//LogDebugMessage("Done creating standard HTML files");
				if (IncludeGraphDataFiles)
				{
					//LogDebugMessage("Creating graph data files");
					station.CreateGraphDataFiles();
					//LogDebugMessage("Done creating graph data files");
				}

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

								if (ExtraFiles[i].process)
								{
									LogDebugMessage($"Interval: Processing extra file[{i}] - {uploadfile}");
									// process the file
									var utf8WithoutBom = new System.Text.UTF8Encoding(false);
									var encoding = UTF8encode ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
									tokenParser.encoding = encoding;
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

				if (!string.IsNullOrEmpty(ftp_host))
				{
					DoFTPLogin();
				}
			}
			finally
			{
				WebUpdating = 0;
			}
		}

		void Client_ValidateCertificate(FtpClient control, FtpSslValidationEventArgs e)
		{
			e.Accept = true; // Allow all
		}

		public void DoFTPLogin()
		{
			if (Sslftp == FtpProtocols.SFTP)
			{
				// BUILD 3092 - added alternate SFTP authenication options
				ConnectionInfo connectionInfo;
				if (SshftpAuthentication == "password")
				{
					connectionInfo = new ConnectionInfo(ftp_host, ftp_port, ftp_user, new PasswordAuthenticationMethod(ftp_user, ftp_password));
					LogDebugMessage("SFTP[Int]: Connecting using password authentication");
				}
				else if (SshftpAuthentication == "psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(SshftpPskFile);
					connectionInfo = new ConnectionInfo(ftp_host, ftp_port, ftp_user, new PrivateKeyAuthenticationMethod(ftp_user, pskFile));
					LogDebugMessage("SFTP[Int]: Connecting using PSK authentication");
				}
				else if (SshftpAuthentication == "password_psk")
				{
					PrivateKeyFile pskFile = new PrivateKeyFile(SshftpPskFile);
					connectionInfo = new ConnectionInfo(ftp_host, ftp_port, ftp_user, new PasswordAuthenticationMethod(ftp_user, ftp_password), new PrivateKeyAuthenticationMethod(ftp_user, pskFile));
					LogDebugMessage("SFTP[Int]: Connecting using password or PSK authentication");
				}
				else
				{
					LogMessage($"SFTP[Int]: Invalid SshftpAuthentication specified [{SshftpAuthentication}]");
					return;
				}

				using (SftpClient conn = new SftpClient(connectionInfo))
				{
					try
					{
						LogFtpMessage($"SFTP[Int]: CumulusMX Connecting to {ftp_host} on port {ftp_port}");
						conn.Connect();
					}
					catch (Exception ex)
					{
						LogMessage($"SFTP[Int]: Error connecting sftp - {ex.Message}");
					}

					if (conn.IsConnected)
					{
						string remotePath = (ftp_directory.EndsWith("/") ? ftp_directory : ftp_directory + "/");
						if (NOAANeedFTP)
						{
							try
							{
								// upload NOAA reports
								LogFtpMessage("SFTP[Int]: Uploading NOAA reports");

								var uploadfile = ReportPath + NOAALatestMonthlyReport;
								var remotefile = NOAAFTPDirectory + '/' + NOAALatestMonthlyReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								uploadfile = ReportPath + NOAALatestYearlyReport;
								remotefile = NOAAFTPDirectory + '/' + NOAALatestYearlyReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								LogFtpMessage("SFTP[Int]: Done uploading NOAA reports");
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
							NOAANeedFTP = false;
						}

						LogFtpMessage("SFTP[Int]: Uploading extra files");
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
										LogMessage(e.Message);
									}
								}
								else
								{
									LogMessage("SFTP[Int]: Extra web file #" + i + " [" + uploadfile + "] not found!");
								}
							}
						}
						if (EODfilesNeedFTP)
						{
							EODfilesNeedFTP = false;
						}
						LogFtpMessage("SFTP[Int]: Done uploading extra files");

						// standard files
						if (IncludeStandardFiles)
						{
							LogFtpMessage("SFTP[Int]: Uploading standard files");
							try
							{
								for (int i = 0; i < numwebtextfiles; i++)
								{
									var uploadfile = localwebtextfiles[i];
									var remotefile = remotePath + remotewebtextfiles[i];

									UploadFile(conn, uploadfile, remotefile, -1);
								}
								LogFtpMessage("SFTP[Int]: Done uploading standard files");
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
						}

						if (IncludeGraphDataFiles)
						{
							try
							{
								LogFtpMessage("SFTP[Int]: Uploading graph data files");
								for (int i = 0; i < localgraphdatafiles.Length; i++)
								{
									var uploadfile = localgraphdatafiles[i];
									var remotefile = remotePath + remotegraphdatafiles[i];

									UploadFile(conn, uploadfile, remotefile, -1);
								}
								LogFtpMessage("SFTP[Int]: Done uploading graph data files");
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
						}

						if (IncludeMoonImage && MoonImageReady)
						{
							try
							{
								LogFtpMessage("SFTP[Int]: Uploading Moon image file");
								UploadFile(conn, "web/moon.png", remotePath + MoonImageFtpDest, -1);
								LogFtpMessage("SFTP[Int]: Done uploading Moon image file");
								// clear the image ready for FTP flag, only upload once an hour
								MoonImageReady = false;
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
						}
					}
					conn.Disconnect();
				}
				LogDebugMessage($"SFTP[Int]: Process complete");
			}
			else
			{
				using (FtpClient conn = new FtpClient())
				{
					FtpTrace.WriteLine(""); // insert a blank line
					FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: CumulusMX Connecting to " + ftp_host);
					conn.Host = ftp_host;
					conn.Port = ftp_port;
					conn.Credentials = new NetworkCredential(ftp_user, ftp_password);

					if (Sslftp == FtpProtocols.FTPS)
					{
						// Explicit = Current protocol - connects using FTP and switches to TLS
						// Implicit = Old depreciated protcol - connects using TLS
						conn.EncryptionMode = DisableFtpsExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
						conn.DataConnectionEncryption = true;
						conn.ValidateCertificate += Client_ValidateCertificate;
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
						LogMessage("FTP[Int]: Error connecting ftp - " + ex.Message);
					}

					conn.EnableThreadSafeDataConnections = false; // use same connection for all transfers

					if (conn.IsConnected)
					{
						string remotePath = (ftp_directory.EndsWith("/") ? ftp_directory : ftp_directory + "/");
						if (NOAANeedFTP)
						{
							try
							{
								// upload NOAA reports
								LogMessage("FTP[Int]: Uploading NOAA reports");
								FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: Uploading NOAA reports");

								var uploadfile = ReportPath + NOAALatestMonthlyReport;
								var remotefile = NOAAFTPDirectory + '/' + NOAALatestMonthlyReport;

								UploadFile(conn, uploadfile, remotefile, -1);

								uploadfile = ReportPath + NOAALatestYearlyReport;
								remotefile = NOAAFTPDirectory + '/' + NOAALatestYearlyReport;

								UploadFile(conn, uploadfile, remotefile, -1);
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
							NOAANeedFTP = false;
						}

						//LogDebugMessage("Uploading extra files");
						// Extra files
						FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: Uploading Extra files");
						for (int i = 0; i < numextrafiles; i++)
						{
							var uploadfile = ExtraFiles[i].local;
							var remotefile = ExtraFiles[i].remote;

							if ((uploadfile.Length > 0) &&
								(remotefile.Length > 0) &&
								!ExtraFiles[i].realtime &&
								EODfilesNeedFTP == ExtraFiles[i].endofday &&
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
										LogMessage($"FTP[Int]: Error uploading file {uploadfile}: {e.Message}");
									}
								}
								else
								{
									LogMessage("FTP[Int]: Extra web file #" + i + " [" + uploadfile + "] not found!");
								}
							}
						}
						if (EODfilesNeedFTP)
						{
							EODfilesNeedFTP = false;
						}
						//LogDebugMessage("Done uploading extra files");
						// standard files
						if (IncludeStandardFiles)
						{
							//LogDebugMessage("Uploading standard files");
							string uploadfile = "";
							FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: Uploading standard files");
							try
							{
								for (int i = 0; i < numwebtextfiles; i++)
								{
									uploadfile = localwebtextfiles[i];
									var remotefile = remotePath + remotewebtextfiles[i];

									UploadFile(conn, uploadfile, remotefile, -1);
								}
								//LogDebugMessage("Done uploading standard files");
							}
							catch (Exception e)
							{
								LogMessage($"FTP[Int]: Error uploading file {uploadfile}: {e.Message}");
							}
						}

						if (IncludeGraphDataFiles)
						{
							try
							{
								//LogDebugMessage("Uploading graph data files");
								FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: Uploading graph data files");
								for (int i = 0; i < localgraphdatafiles.Length; i++)
								{
									var uploadfile = localgraphdatafiles[i];
									var remotefile = remotePath + remotegraphdatafiles[i];

									UploadFile(conn, uploadfile, remotefile, -1);
								}
								//LogDebugMessage("Done uploading graph data files");
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
						}

						if (IncludeMoonImage && MoonImageReady)
						{
							try
							{
								FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: Uploading Moon image file");
								UploadFile(conn, "web/moon.png", remotePath + MoonImageFtpDest, -1);
								// clear the image ready for FTP flag, only upload once an hour
								MoonImageReady = false;
							}
							catch (Exception e)
							{
								LogMessage(e.Message);
							}
						}
					}

					// b3045 - dispose of connection
					conn.Disconnect();
					FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " FTP[Int]: Disconnected from " + ftp_host);
				}
				LogFtpMessage($"FTP[Int]: Process complete");
			}
		}

		private void UploadFile(FtpClient conn, string localfile, string remotefile, int cycle)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			FtpTrace.WriteLine("");
			FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $"FTP[{cycleStr}]: Uploading {localfile} to {remotefile}");

			try
			{
				if (DeleteBeforeUpload)
				{
					// delete the existing file
					try
					{
						conn.DeleteFile(remotefile);
					}
					catch (Exception ex)
					{
						LogMessage($"FTP[{cycleStr}]: Error deleting {remotefile} : {ex.Message}");
					}
				}

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
					}
					catch (Exception ex)
					{
						LogMessage($"FTP[{cycleStr}]: Error uploading {localfile} to {remotefile} : {ex.Message}");
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
					try
					{
						conn.Rename(remotefilename, remotefile);
					}
					catch (Exception ex)
					{
						LogMessage($"FTP[{cycleStr}]: Error renaming {remotefilename} to {remotefile} : {ex.Message}");
					}
				}

				FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + $"FTP[{cycleStr}]: Completed uploading {localfile} to {remotefile}");
			}
			catch (Exception ex)
			{
				LogMessage($"FTP[{cycleStr}]: Error uploading {localfile} to {remotefile} : {ex.Message}");
			}
		}

		private void UploadFile(SftpClient conn, string localfile, string remotefile, int cycle)
		{
			string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;
			string cycleStr = cycle >= 0 ? cycle.ToString() : "Int";

			if (conn == null)
			{
				LogDebugMessage($"SFTP[{cycleStr}]: The SFTP object is null - skipping this upload");
			}
			else
			{
				LogFtpMessage($"SFTP[{cycleStr}]: Starting Upload of {localfile} to {remotefile}");

				try
				{
					/*
					if (DeleteBeforeUpload)
					{
						// delete the existing file
						// Not required for SFTP - we use the flag to always overwrite any existing file
						try
						{
							LogFtpMessage($"SFTP: Deleting {remotefile}");
							conn.DeleteFile(remotefile);
						}
						catch (Exception ex)
						{
							LogMessage("SFTP: Error deleting " + remotefile + " : " + ex.Message);
						}
					}
					*/

					//LogFtpMessage($"SFTP: Uploading {remotefilename}");
					using (Stream istream = new FileStream(localfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						try
						{
							conn.OperationTimeout = TimeSpan.FromSeconds(15);
							conn.UploadFile(istream, remotefilename, true);
						}
						catch (Exception ex)
						{
							LogMessage($"SFTP[{cycleStr}]: Error uploading {localfile} to {remotefile} : {ex.Message}");
							if (ex.Message.Contains("Safe handle has been closed"))
							{
								// This appears to be an unrecoverable internal error. Abort the whole connection.
								conn.Dispose();
							}
							return;
						}
					}

					if (FTPRename)
					{
						// rename the file
						try
						{
							LogFtpMessage($"SFTP[{cycleStr}]: Renaming {remotefilename} to {remotefile}");
							conn.RenameFile(remotefilename, remotefile, true);
						}
						catch (Exception ex)
						{
							LogMessage($"SFTP[{cycleStr}]: Error renaming {remotefilename} to {remotefile} : {ex.Message}");
							return;
						}
					}
					LogFtpMessage($"SFTP[{cycleStr}]: Completed uploading {localfile} to {remotefile}");
				}
				catch (Exception ex)
				{
					LogMessage($"SFTP[{cycleStr}]: Error uploading {localfile} to {remotefile} - {ex.Message}");
					return;
				}
			}
		}

		public void LogMessage(string message)
		{
			Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
		}

		public void LogDebugMessage(string message)
		{
			if (logging || DataLogging)
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogDataMessage(string message)
		{
			if (DataLogging)
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogFtpMessage(string message)
		{
			if (FTPlogging)
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
			}
		}

		public void LogConsoleMessage(string message)
		{
			if (Program.service)
			{
				Program.svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
				Program.svcTextListener.Flush();
			}
			else
			{
				Console.WriteLine(message);
			}
		}

		public string ReplaceCommas(string AStr)
		{
			return AStr.Replace(',', '.');
		}

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
			var filename = AppDir + RealtimeFile;
			DateTime timestamp = DateTime.Now;

			try
			{
				LogDebugMessage($"Realtime[{cycle}]: Creating realtime.txt");
				using (StreamWriter file = new StreamWriter(filename, false))
				{
					var InvC = new CultureInfo("");

					file.Write(timestamp.ToString("dd/MM/yy HH:mm:ss ")); // 1, 2
					file.Write(station.OutdoorTemperature.ToString(TempFormat, InvC) + ' '); // 3
					file.Write(station.OutdoorHumidity.ToString() + ' '); // 4
					file.Write(station.OutdoorDewpoint.ToString(TempFormat, InvC) + ' '); // 5
					file.Write(station.WindAverage.ToString(WindAvgFormat, InvC) + ' '); // 6
					file.Write(station.WindLatest.ToString(WindFormat, InvC) + ' '); // 7
					file.Write(station.Bearing.ToString() + ' '); // 8
					file.Write(station.RainRate.ToString(RainFormat, InvC) + ' '); // 9
					file.Write(station.RainToday.ToString(RainFormat, InvC) + ' '); // 10
					file.Write(station.Pressure.ToString(PressFormat, InvC) + ' '); // 11
					file.Write(station.CompassPoint(station.Bearing) + ' '); // 12
					file.Write(Beaufort(station.WindAverage) + ' '); // 13
					file.Write(WindUnitText + ' '); // 14
					file.Write(TempUnitText[1].ToString() + ' '); // 15
					file.Write(PressUnitText + ' '); // 16
					file.Write(RainUnitText + ' '); // 17
					file.Write(station.WindRunToday.ToString(WindRunFormat, InvC) + ' '); // 18
					if (station.presstrendval > 0)
						file.Write('+' + station.presstrendval.ToString(PressFormat, InvC) + ' '); // 19
					else
						file.Write(station.presstrendval.ToString(PressFormat, InvC) + ' ');
					file.Write(station.RainMonth.ToString(RainFormat, InvC) + ' '); // 20
					file.Write(station.RainYear.ToString(RainFormat, InvC) + ' '); // 21
					file.Write(station.RainYesterday.ToString(RainFormat, InvC) + ' '); // 22
					file.Write(station.IndoorTemperature.ToString(TempFormat, InvC) + ' '); // 23
					file.Write(station.IndoorHumidity.ToString() + ' '); // 24
					file.Write(station.WindChill.ToString(TempFormat, InvC) + ' '); // 25
					file.Write(station.temptrendval.ToString(TempTrendFormat, InvC) + ' '); // 26
					file.Write(station.HighTempToday.ToString(TempFormat, InvC) + ' '); // 27
					file.Write(station.HighTempTodayTime.ToString("HH:mm ") ); // 28
					file.Write(station.LowTempToday.ToString(TempFormat, InvC) + ' '); // 29
					file.Write(station.LowTempTodayTime.ToString("HH:mm ")); // 30
					file.Write(station.HighWindToday.ToString(WindAvgFormat, InvC) + ' '); // 31
					file.Write(station.HighWindTodayTime.ToString("HH:mm ")); // 32
					file.Write(station.HighGustToday.ToString(WindFormat, InvC) + ' '); // 33
					file.Write(station.HighGustTodayTime.ToString("HH:mm ")); // 34
					file.Write(station.HighPressToday.ToString(PressFormat, InvC) + ' '); // 35
					file.Write(station.HighPressTodayTime.ToString("HH:mm ")); // 36
					file.Write(station.LowPressToday.ToString(PressFormat, InvC) + ' '); // 37
					file.Write(station.LowPressTodayTime.ToString("HH:mm ")); // 38
					file.Write(Version + ' '); // 39
					file.Write(Build + ' '); // 40
					file.Write(station.RecentMaxGust.ToString(WindFormat, InvC) + ' '); // 41
					file.Write(station.HeatIndex.ToString(TempFormat, InvC) + ' '); // 42
					file.Write(station.Humidex.ToString(TempFormat, InvC) + ' '); // 43
					file.Write(station.UV.ToString(UVFormat, InvC) + ' '); // 44
					file.Write(station.ET.ToString(ETFormat, InvC) + ' '); // 45
					file.Write((Convert.ToInt32(station.SolarRad)).ToString() + ' '); // 46
					file.Write(station.AvgBearing.ToString() + ' '); // 47
					file.Write(station.RainLastHour.ToString(RainFormat, InvC) + ' '); // 48
					file.Write(station.Forecastnumber.ToString() + ' '); // 49
					file.Write(IsDaylight() ? "1 " : "0 ");
					file.Write(station.SensorContactLost ? "1 " : "0 ");
					file.Write(station.CompassPoint(station.AvgBearing) + ' '); // 52
					file.Write((Convert.ToInt32(station.CloudBase)).ToString() + ' '); // 53
					file.Write(CloudBaseInFeet ? "ft " : "m ");
					file.Write(station.ApparentTemperature.ToString(TempFormat, InvC) + ' '); // 55
					file.Write(station.SunshineHours.ToString(SunFormat, InvC) + ' '); // 56
					file.Write(Convert.ToInt32(station.CurrentSolarMax).ToString() + ' '); // 57
					file.Write(station.IsSunny ? "1 " : "0 "); // 58
					file.WriteLine(station.FeelsLike.ToString(TempFormat, InvC)); // 59

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
				values.Append(WindUnitText + "','");
				values.Append(TempUnitText[1].ToString() + "','");
				values.Append(PressUnitText + "','");
				values.Append(RainUnitText + "',");
				values.Append(station.WindRunToday.ToString(WindRunFormat, InvC) + ",'");
				values.Append((station.presstrendval > 0 ? '+' + station.presstrendval.ToString(PressFormat, InvC) : station.presstrendval.ToString(PressFormat, InvC)) + "',");
				values.Append(station.RainMonth.ToString(RainFormat, InvC) + ',');
				values.Append(station.RainYear.ToString(RainFormat, InvC) + ',');
				values.Append(station.RainYesterday.ToString(RainFormat, InvC) + ',');
				values.Append(station.IndoorTemperature.ToString(TempFormat, InvC) + ',');
				values.Append(station.IndoorHumidity.ToString() + ',');
				values.Append(station.WindChill.ToString(TempFormat, InvC) + ',');
				values.Append(station.temptrendval.ToString(TempTrendFormat, InvC) + ',');
				values.Append(station.HighTempToday.ToString(TempFormat, InvC) + ",'");
				values.Append(station.HighTempTodayTime.ToString("HH:mm") + "',");
				values.Append(station.LowTempToday.ToString(TempFormat, InvC) + ",'");
				values.Append(station.LowTempTodayTime.ToString("HH:mm") + "',");
				values.Append(station.HighWindToday.ToString(WindAvgFormat, InvC) + ",'");
				values.Append(station.HighWindTodayTime.ToString("HH:mm") + "',");
				values.Append(station.HighGustToday.ToString(WindFormat, InvC) + ",'");
				values.Append(station.HighGustTodayTime.ToString("HH:mm") + "',");
				values.Append(station.HighPressToday.ToString(PressFormat, InvC) + ",'");
				values.Append(station.HighPressTodayTime.ToString("HH:mm") + "',");
				values.Append(station.LowPressToday.ToString(PressFormat, InvC) + ",'");
				values.Append(station.LowPressTodayTime.ToString("HH:mm") + "','");
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
				values.Append(((int)station.CloudBase).ToString() + ",'");
				values.Append((CloudBaseInFeet ? "ft" : "m") + "',");
				values.Append(station.ApparentTemperature.ToString(TempFormat, InvC) + ',');
				values.Append(station.SunshineHours.ToString(SunFormat, InvC) + ',');
				values.Append(((int)Math.Round(station.CurrentSolarMax)).ToString() + ",'");
				values.Append((station.IsSunny ? "1" : "0") + "',");
				values.Append(station.FeelsLike.ToString(TempFormat, InvC));
				values.Append(")");

				string queryString = values.ToString();

				// do the update
				using (MySqlCommand cmd = new MySqlCommand())
				{
					try
					{
						cmd.CommandText = queryString;
						cmd.Connection = RealtimeSqlConn;
						LogDebugMessage($"Realtime[{cycle}]: Running SQL command: {queryString}");

						RealtimeSqlConn.Open();
						int aff1 = cmd.ExecuteNonQuery();
						LogDebugMessage($"Realtime[{cycle}]: {aff1} rows were affected.");

						if (!string.IsNullOrEmpty(MySqlRealtimeRetention))
						{
							// delete old entries
							cmd.CommandText = $"DELETE IGNORE FROM {MySqlRealtimeTable} WHERE LogDateTime < DATE_SUB('{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}', INTERVAL {MySqlRealtimeRetention})";
							LogDebugMessage($"Realtime[{cycle}]: Running SQL command: {cmd.CommandText}");

							try
							{
								RealtimeSqlConn.Open();
								int aff2 = cmd.ExecuteNonQuery();
								LogMessage($"Realtime[{cycle}]: {aff2} rows were affected.");
							}
							catch (Exception ex)
							{
								LogMessage($"Realtime[{cycle}]: Error encountered during Realtime delete MySQL operation.");
								LogMessage(ex.Message);
							}

						}
					}
					catch (Exception ex)
					{
						LogMessage($"Realtime[{cycle}]: Error encountered during Realtime MySQL operation.");
						LogMessage(ex.Message);
					}
					finally
					{
						try
						{
							RealtimeSqlConn.Close();
						}
						catch {}
					}
				}
			}
		}

		private void ProcessTemplateFile(string template, string outputfile, TokenParser parser)
		{
			string templatefile = AppDir + template;
			if (File.Exists(templatefile))
			{
				var utf8WithoutBom = new System.Text.UTF8Encoding(false);
				var encoding = UTF8encode ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
				parser.encoding = encoding;
				parser.SourceFile = template;
				var output = parser.ToString();

				using (StreamWriter file = new StreamWriter(outputfile, false, encoding))
				{
					file.Write(output);

					file.Close();
				}
			}
		}

		public void StartTimers()
		{
			LogMessage("Start Timers");
			// start the general one-minute timer
			LogMessage("Starting 1-minute timer");
			station.StartMinuteTimer();
			LogMessage($"Data logging interval = {DataLogInterval} ({logints[DataLogInterval]} mins)");

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

			if (RealtimeEnabled)
			{
				LogMessage("Starting Realtime timer, interval = " + RealtimeInterval / 1000 + " seconds");
			}
			else
			{
				LogMessage("Realtime not enabled");
			}

			RealtimeTimer.Enabled = RealtimeEnabled;

			CustomMysqlSecondsTimer.Enabled = CustomMySqlSecondsEnabled;

			CustomHttpSecondsTimer.Enabled = CustomHttpSecondsEnabled;

			if (WundRapidFireEnabled)
			{
				WundTimer.Interval = 5000; // 5 seconds in rapid-fire mode
			}
			else
			{
				WundTimer.Interval = WundInterval * 60 * 1000; // mins to millisecs
			}

			WindyTimer.Interval = WindyInterval * 60 * 1000; // mins to millisecs

			PWSTimer.Interval = PWSInterval * 60 * 1000; // mins to millisecs

			WOWTimer.Interval = WOWInterval * 60 * 1000; // mins to millisecs

			AwekasTimer.Interval = AwekasInterval * 1000;

			WCloudTimer.Interval = WCloudInterval * 60 * 1000;

			MQTTTimer.Interval = MQTTIntervalTime * 1000; // secs to millisecs
			if (MQTTEnableInterval)
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
				WundTimer.Enabled = WundEnabled && !SynchronisedWUUpdate;
			}
			else
			{
				// start the archive upload thread
				WundCatchingUp = true;
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
				WindyTimer.Enabled = WindyEnabled && !SynchronisedWindyUpdate;
			}
			else
			{
				// start the archive upload thread
				WindyCatchingUp = true;
				WindyCatchup();
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
				PWSTimer.Enabled = PWSEnabled && !SynchronisedPWSUpdate;
			}
			else
			{
				// start the archive upload thread
				PWSCatchingUp = true;
				PWSCatchup();
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
				WOWTimer.Enabled = WOWEnabled && !SynchronisedWOWUpdate;
			}
			else
			{
				// start the archive upload thread
				WOWCatchingUp = true;
				WOWCatchup();
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
				MySqlCatchupThread = new Thread(MySqlCatchup);
				MySqlCatchupThread.IsBackground = true;
				MySqlCatchupThread.Start();
			}

			TwitterTimer.Interval = TwitterInterval * 60 * 1000; // mins to millisecs
			TwitterTimer.Enabled = TwitterEnabled && !SynchronisedTwitterUpdate;

			APRStimer.Interval = APRSinterval * 60 * 1000; // mins to millisecs
			APRStimer.Enabled = APRSenabled && !SynchronisedAPRSUpdate;

			WebTimer.Interval = UpdateInterval * 60 * 1000; // mins to millisecs
			WebTimer.Enabled = WebAutoUpdate && !SynchronisedWebUpdate;

			AwekasTimer.Enabled = AwekasEnabled && !SynchronisedAwekasUpdate;

			LogMessage("Normal running");
			LogConsoleMessage("Normal running");
		}

		private void CustomMysqlSecondsTimerTick(object sender, ElapsedEventArgs e)
		{
			if (!customMySqlSecondsUpdateInProgress)
			{
				customMySqlSecondsUpdateInProgress = true;

				try
				{
					customMysqlSecondsTokenParser.InputText = CustomMySqlSecondsCommandString;
					CustomMysqlSecondsCommand.CommandText = customMysqlSecondsTokenParser.ToStringFromString();
					LogDebugMessage($"Custom SQL sec: {CustomMysqlSecondsCommand.CommandText}");
					CustomMysqlSecondsConn.Open();
					int aff = CustomMysqlSecondsCommand.ExecuteNonQuery();
					LogDebugMessage($"Custom SQL sec: {aff} rows were affected.");
				}
				catch (Exception ex)
				{
					LogMessage("Custom SQL sec: Error encountered during custom seconds MySQL operation.");
					LogMessage(ex.Message);
				}
				finally
				{
					try
					{
						CustomMysqlSecondsConn.Close();
					}
					catch {}
					customMySqlSecondsUpdateInProgress = false;
				}
			}
		}

		internal void CustomMysqlMinutesTimerTick()
		{
			if (!customMySqlMinutesUpdateInProgress)
			{
				customMySqlMinutesUpdateInProgress = true;

				try
				{
					customMysqlMinutesTokenParser.InputText = CustomMySqlMinutesCommandString;
					CustomMysqlMinutesCommand.CommandText = customMysqlMinutesTokenParser.ToStringFromString();
					LogDebugMessage(CustomMysqlMinutesCommand.CommandText);
					CustomMysqlMinutesConn.Open();
					int aff = CustomMysqlMinutesCommand.ExecuteNonQuery();
					LogDebugMessage("MySQL: Custom minutes update " + aff + " rows were affected.");
				}
				catch (Exception ex)
				{
					LogMessage("Error encountered during custom minutes MySQL operation.");
					LogMessage(ex.Message);
				}
				finally
				{
					try
					{
						CustomMysqlMinutesConn.Close();
					}
					catch {}
					customMySqlMinutesUpdateInProgress = false;
				}
			}
		}

		internal void CustomMysqlRolloverTimerTick()
		{
			if (!customMySqlRolloverUpdateInProgress)
			{
				customMySqlRolloverUpdateInProgress = true;
				var t = Task.Run(() =>
				{
					try
					{
						customMysqlRolloverTokenParser.InputText = CustomMySqlRolloverCommandString;
						CustomMysqlRolloverCommand.CommandText = customMysqlRolloverTokenParser.ToStringFromString();
						LogDebugMessage(CustomMysqlRolloverCommand.CommandText);
						CustomMysqlRolloverConn.Open();
						int aff = CustomMysqlRolloverCommand.ExecuteNonQuery();
						LogDebugMessage("MySQL: Custom rollover update " + aff + " rows were affected.");
					}
					catch (Exception ex)
					{
						LogMessage("Error encountered during custom Rollover MySQL operation.");
						LogMessage(ex.Message);
					}
					finally
					{
						try
						{
							CustomMysqlRolloverConn.Close();
						}
						catch {}
						customMySqlRolloverUpdateInProgress = false;
					}
				});
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

							if (ExtraFiles[i].process)
							{
								LogDebugMessage("EOD: Processing extra file " + uploadfile);
								// process the file
								var utf8WithoutBom = new System.Text.UTF8Encoding(false);
								var encoding = UTF8encode ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
								tokenParser.encoding = encoding;
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
			var mySqlConn = new MySqlConnection();
			mySqlConn.Host = MySqlHost;
			mySqlConn.Port = MySqlPort;
			mySqlConn.UserId = MySqlUser;
			mySqlConn.Password = MySqlPass;
			mySqlConn.Database = MySqlDatabase;

			try
			{
				mySqlConn.Open();

				for (int i = 0; i < MySqlList.Count; i++)
				{
					LogMessage("MySQL Archive: Uploading archive #" + (i + 1));
					try
					{
						using (MySqlCommand cmd = new MySqlCommand())
						{
							cmd.CommandText = MySqlList[i];
							cmd.Connection = mySqlConn;
							LogDebugMessage(MySqlList[i]);

							int aff = cmd.ExecuteNonQuery();
							LogMessage($"MySQL Archive: Table {MySqlMonthlyTable} - {aff} rows were affected.");
						}
					}
					catch (Exception ex)
					{
						LogMessage("MySQL Archive: Error encountered during catchup MySQL operation.");
						LogMessage(ex.Message);
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("MySQL Archive: Error encountered during catchup MySQL operation.");
				LogMessage(ex.Message);
			}
			finally
			{
				try
				{
					mySqlConn.Close();
				}
				catch {}
			}

			LogMessage("MySQL Archive: End of MySQL archive upload");
			MySqlList.Clear();
		}

		private void RealtimeFTPLogin(uint cycle)
		{
			//RealtimeTimer.Enabled = false;
			RealtimeFTP.Host = ftp_host;
			RealtimeFTP.Port = ftp_port;
			RealtimeFTP.Credentials = new NetworkCredential(ftp_user, ftp_password);

			if (Sslftp == FtpProtocols.FTPS)
			{
				RealtimeFTP.EncryptionMode = DisableFtpsExplicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
				RealtimeFTP.DataConnectionEncryption = true;
				RealtimeFTP.ValidateCertificate += Client_ValidateCertificate;
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

			if (ftp_host.Length > 0 && ftp_host.Length > 0)
			{
				LogMessage($"FTP[{ cycle}]: Attempting realtime FTP connect to host {ftp_host} on port {ftp_port}");
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
			if (ftp_host != "" && ftp_host != " ")
			{
				LogMessage($"SFTP[{cycle}]: Attempting realtime SFTP connect to host {ftp_host} on port {ftp_port}");
				try
				{
					// BUILD 3092 - added alternate SFTP authenication options
					ConnectionInfo connectionInfo;
					PrivateKeyFile pskFile;
					if (SshftpAuthentication == "password")
					{
						connectionInfo = new ConnectionInfo(ftp_host, ftp_port, ftp_user, new PasswordAuthenticationMethod(ftp_user, ftp_password));
						LogDebugMessage($"SFTP[{cycle}]: Connecting using password authentication");
					}
					else if (SshftpAuthentication == "psk")
					{
						pskFile = new PrivateKeyFile(SshftpPskFile);
						connectionInfo = new ConnectionInfo(ftp_host, ftp_port, ftp_user, new PrivateKeyAuthenticationMethod(ftp_user, pskFile));
						LogDebugMessage($"SFTP[{cycle}]: Connecting using PSK authentication");
					}
					else if (SshftpAuthentication == "password_psk")
					{
						pskFile = new PrivateKeyFile(SshftpPskFile);
						connectionInfo = new ConnectionInfo(ftp_host, ftp_port, ftp_user, new PasswordAuthenticationMethod(ftp_user, ftp_password), new PrivateKeyAuthenticationMethod(ftp_user, pskFile));
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
			UpdatingWU = true;
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
			WundCatchingUp = false;
			WundTimer.Enabled = WundEnabled && !SynchronisedWUUpdate;
			UpdatingWU = false;
		}

		private async void WindyCatchup()
		{
			UpdatingWindy = true;
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
			WindyCatchingUp = false;
			WindyTimer.Enabled = WindyEnabled && !SynchronisedWindyUpdate;
			UpdatingWindy = false;
		}

		/// <summary>
		/// Process the list of PWS Weather updates created at startup from logger entries
		/// </summary>
		private async void PWSCatchup()
		{
			UpdatingPWS = true;

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
			PWSCatchingUp = false;
			PWSTimer.Enabled = PWSEnabled && !SynchronisedPWSUpdate;
			UpdatingPWS = false;
		}

		/// <summary>
		/// Process the list of WOW updates created at startup from logger entries
		/// </summary>
		private async void WOWCatchup()
		{
			UpdatingWOW = true;

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
			WOWCatchingUp = false;
			WOWTimer.Enabled = WOWEnabled && !SynchronisedWOWUpdate;
			UpdatingWOW = false;
		}

		public async void UpdatePWSweather(DateTime timestamp)
		{
			if (!UpdatingPWS)
			{
				UpdatingPWS = true;

				string pwstring;
				string URL = station.GetPWSURL(out pwstring, timestamp);

				string starredpwstring = "&PASSWORD=" + new string('*', PWSPW.Length);

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
					UpdatingPWS = false;
				}
			}
		}

		public async void UpdateWOW(DateTime timestamp)
		{
			if (!UpdatingWOW)
			{
				UpdatingWOW = true;

				string pwstring;
				string URL = station.GetWOWURL(out pwstring, timestamp);

				string starredpwstring = "&siteAuthenticationKey=" + new string('*', WOWPW.Length);

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
					UpdatingWOW = false;
				}
			}
		}

		public async void GetLatestVersion()
		{
			var http = new HttpClient();
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			try
			{
				var retVal = await http.GetAsync("https://github.com/cumulusmx/CumulusMX/releases/latest");
				var latestUri = retVal.RequestMessage.RequestUri.AbsolutePath;
				LatestBuild = new String(latestUri.Split('/').Last().Where(Char.IsDigit).ToArray());
				if (int.Parse(Build) < int.Parse(LatestBuild))
				{
					var msg = $"You are not running the latest version of CumulusMX, build {LatestBuild} is available.";
					LogConsoleMessage(msg);
					LogMessage(msg);
				}
				else
				{
					LogMessage($"This Cumulus MX instance is running the latest version");
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
		}

		/// <summary>
		/// Add an archive entry to the WU 'catchup' list for sending to WU
		/// </summary>
		/// <param name="timestamp"></param>
		private void AddToWundList(DateTime timestamp)
		{
			if (WundEnabled && WundCatchUp)
			{
				string pwstring;
				string URL = station.GetWundergroundURL(out pwstring, timestamp, true);

				WundList.Add(URL);

				string starredpwstring = "&PASSWORD=" + new string('*', WundPW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogMessage("Creating WU URL #" + WundList.Count);

				LogMessage(LogURL);
			}
		}

		private void AddToWindyList(DateTime timestamp)
		{
			if (WindyEnabled && WindyCatchUp)
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
			if (PWSEnabled && PWSCatchUp)
			{
				string pwstring;
				string URL = station.GetPWSURL(out pwstring, timestamp);

				PWSList.Add(URL);

				string starredpwstring = "&PASSWORD=" + new string('*', PWSPW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogMessage("Creating PWS URL #" + PWSList.Count);

				LogMessage(LogURL);
			}
		}

		private void AddToWOWList(DateTime timestamp)
		{
			if (WOWEnabled && WOWCatchUp)
			{
				string pwstring;
				string URL = station.GetWOWURL(out pwstring, timestamp);

				WOWList.Add(URL);

				string starredpwstring = "&siteAuthenticationKey=" + new string('*', WOWPW.Length);

				string LogURL = URL.Replace(pwstring, starredpwstring);

				LogMessage("Creating WOW URL #" + WOWList.Count);

				LogMessage(LogURL);
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
			LogMessage($"PO={PressOffset.ToString("F3")} TO={TempOffset.ToString("F3")} HO={HumOffset} WDO={WindDirOffset} ITO={InTempoffset.ToString("F3")} SO={SolarOffset.ToString("F3")} UVO={UVOffset.ToString("F3")}");
			LogMessage($"PM={PressMult.ToString("F3")} WSM={WindSpeedMult.ToString("F3")} WGM={WindGustMult.ToString("F3")} TM={TempMult.ToString("F3")} TM2={TempMult2.ToString("F3")} " +
						$"HM={HumMult.ToString("F3")} HM2={HumMult2.ToString("F3")} RM={RainMult.ToString("F3")} SM={SolarMult.ToString("F3")} UVM={UVMult.ToString("F3")}");
			LogMessage("Spike removal:");
			LogMessage($"TD={SpikeTempDiff.ToString("F3")} GD={SpikeGustDiff.ToString("F3")} WD={SpikeWindDiff.ToString("F3")} HD={SpikeHumidityDiff.ToString("F3")} PD={SpikePressDiff.ToString("F3")} MR={SpikeMaxRainRate.ToString("F3")} MH={SpikeMaxHourlyRain.ToString("F3")}");
			LogMessage("Limits:");
			LogMessage($"TH={LimitTempHigh.ToString(TempFormat)} TL={LimitTempLow.ToString(TempFormat)} DH={LimitDewHigh.ToString(TempFormat)} PH={LimitPressHigh.ToString(PressFormat)} PL={LimitPressLow.ToString(PressFormat)} GH={LimitWindHigh.ToString("F3")}");

		}
	}


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

	public static class DoubleExtensions
	{
		public static string ToUKString(this double value)
		{
			return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
		}
	}

	public class DiaryData
	{
		[PrimaryKey]
		public DateTime Timestamp { get; set; }
		public string entry { get; set; }
		public int snowFalling { get; set; }
		public int snowLying { get; set; }
		public double snowDepth { get; set; }
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

}
