using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;

using EmbedIO.Utilities;

using FluentFTP.Helpers;

using ServiceStack.Text;

using SQLite;

using Timer = System.Timers.Timer;

namespace CumulusMX
{
	internal abstract class WeatherStation
	{
		public struct TWindRecent
		{
			public double Gust; // uncalibrated "gust" as read from station
			public double Speed; // uncalibrated "speed" as read from station
			public DateTime Timestamp;
		}

		public struct TWindVec
		{
			public double X;
			public double Y;
			public int Bearing;
			public DateTime Timestamp;
		}

		public int DataTimeoutMins = 1;
		private readonly Object monthIniThreadLock = new Object();
		public readonly Object yearIniThreadLock = new Object();
		public readonly Object alltimeIniThreadLock = new Object();
		public readonly Object monthlyalltimeIniThreadLock = new Object();

		private static readonly SemaphoreSlim webSocketSemaphore = new SemaphoreSlim(1, 1);

		// holds all time highs and lows
		public AllTimeRecords AllTime = new AllTimeRecords();

		// holds monthly all time highs and lows
		private AllTimeRecords[] monthlyRecs = new AllTimeRecords[13];
		public AllTimeRecords[] MonthlyRecs
		{
			get
			{
				if (monthlyRecs == null)
				{
					monthlyRecs = new AllTimeRecords[13];
				}

				return monthlyRecs;
			}
		}

		public struct logfilerec
		{
			public DateTime Date;
			public double OutdoorTemperature;
			public int OutdoorHumidity;
			public double OutdoorDewpoint;
			public double WindAverage;
			public double RecentMaxGust;
			public int AvgBearing;
			public double RainRate;
			public double RainToday;
			public double Pressure;
			public double Raincounter;
			public double IndoorTemperature;
			public int IndoorHumidity;
			public double WindLatest;
			public double WindChill;
			public double HeatIndex;
			public double UV;
			public double SolarRad;
			public double ET;
			public double AnnualETTotal;
			public double ApparentTemperature;
			public double CurrentSolarMax;
			public double SunshineHours;
			public int Bearing;
			public double RG11RainToday;
			public double RainSinceMidnight;
			public double FeelsLike;
			public double Humidex;
		}

		public struct dayfilerec
		{
			public DateTime Date;
			public double HighGust;
			public int HighGustBearing;
			public DateTime HighGustTime;
			public double LowTemp;
			public DateTime LowTempTime;
			public double HighTemp;
			public DateTime HighTempTime;
			public double LowPress;
			public DateTime LowPressTime;
			public double HighPress;
			public DateTime HighPressTime;
			public double HighRainRate;
			public DateTime HighRainRateTime;
			public double TotalRain;
			public double AvgTemp;
			public double WindRun;
			public double HighAvgWind;
			public DateTime HighAvgWindTime;
			public int LowHumidity;
			public DateTime LowHumidityTime;
			public int HighHumidity;
			public DateTime HighHumidityTime;
			public double ET;
			public double SunShineHours;
			public double HighHeatIndex;
			public DateTime HighHeatIndexTime;
			public double HighAppTemp;
			public DateTime HighAppTempTime;
			public double LowAppTemp;
			public DateTime LowAppTempTime;
			public double HighHourlyRain;
			public DateTime HighHourlyRainTime;
			public double LowWindChill;
			public DateTime LowWindChillTime;
			public double HighDewPoint;
			public DateTime HighDewPointTime;
			public double LowDewPoint;
			public DateTime LowDewPointTime;
			public int DominantWindBearing;
			public double HeatingDegreeDays;
			public double CoolingDegreeDays;
			public int HighSolar;
			public DateTime HighSolarTime;
			public double HighUv;
			public DateTime HighUvTime;
			public double HighFeelsLike;
			public DateTime HighFeelsLikeTime;
			public double LowFeelsLike;
			public DateTime LowFeelsLikeTime;
			public double HighHumidex;
			public DateTime HighHumidexTime;
			public double ChillHours;
			public double HighRain24h;
			public DateTime HighRain24hTime;
		}

		public List<dayfilerec> DayFile = new List<dayfilerec>();


		// this month highs and lows
		public AllTimeRecords ThisMonth = new AllTimeRecords();

		public AllTimeRecords ThisYear = new AllTimeRecords();


		//public DateTime lastArchiveTimeUTC;

		public string LatestFOReading { get; set; }

		//public int LastDailySummaryOADate;

		public Cumulus cumulus;

		private int lastMinute;
		private int lastHour;
		private int lastSecond;

		public bool[] WMR928ChannelPresent = new[] { false, false, false, false };
		public bool[] WMR928ExtraTempValueOnly = new[] { false, false, false, false };
		public double[] WMR928ExtraTempValues = new[] { 0.0, 0.0, 0.0, 0.0 };
		public double[] WMR928ExtraDPValues = new[] { 0.0, 0.0, 0.0, 0.0 };
		public int[] WMR928ExtraHumValues = new[] { 0, 0, 0, 0 };

		// random number generator - used for things like random back-off delays
		internal Random random = new Random();

		public DateTime AlltimeRecordTimestamp { get; set; }

		//private ProgressWindow progressWindow;

		//public historyProgressWindow histprog;
		public BackgroundWorker bw;

		//public bool importingData = false;

		public bool calculaterainrate = false;

		protected List<int> buffer = new List<int>();

		//private readonly List<Last3HourData> Last3HourDataList = new List<Last3HourData>();
		//private readonly List<LastHourData> LastHourDataList = new List<LastHourData>();
		private readonly List<Last10MinWind> Last10MinWindList = new List<Last10MinWind>();
		//		private readonly List<RecentDailyData> RecentDailyDataList = new List<RecentDailyData>();

		public WeatherDataCollection weatherDataCollection = new WeatherDataCollection();

		// Current values

		public double THWIndex = 0;
		public double THSWIndex = 0;

		public double RainCounterDayStart = 0.0;
		public double RainCounter = 0.0;
		public bool gotraindaystart = false;
		protected double prevraincounter = 0.0;

		public struct DailyHighLow
		{
			public double HighGust;
			public int HighGustBearing;
			public DateTime HighGustTime;
			public double HighWind;
			public DateTime HighWindTime;
			public double HighTemp;
			public DateTime HighTempTime;
			public double LowTemp;
			public DateTime LowTempTime;
			public double TempRange;
			public double HighAppTemp;
			public DateTime HighAppTempTime;
			public double LowAppTemp;
			public DateTime LowAppTempTime;
			public double HighFeelsLike;
			public DateTime HighFeelsLikeTime;
			public double LowFeelsLike;
			public DateTime LowFeelsLikeTime;
			public double HighHumidex;
			public DateTime HighHumidexTime;
			public double HighPress;
			public DateTime HighPressTime;
			public double LowPress;
			public DateTime LowPressTime;
			public double HighRainRate;
			public DateTime HighRainRateTime;
			public double HighHourlyRain;
			public DateTime HighHourlyRainTime;
			public int HighHumidity;
			public DateTime HighHumidityTime;
			public int LowHumidity;
			public DateTime LowHumidityTime;
			public double HighHeatIndex;
			public DateTime HighHeatIndexTime;
			public double HighRain24h;
			public DateTime HighRain24hTime;
			public double LowWindChill;
			public DateTime LowWindChillTime;
			public double HighDewPoint;
			public DateTime HighDewPointTime;
			public double LowDewPoint;
			public DateTime LowDewPointTime;
			public int HighSolar;
			public DateTime HighSolarTime;
			public double HighUv;
			public DateTime HighUvTime;
		};

		// today highs and lows
		public DailyHighLow HiLoToday = new DailyHighLow()
		{
			HighTemp = -500,
			HighAppTemp = -500,
			HighFeelsLike = -500,
			HighHumidex = -500,
			HighHeatIndex = -500,
			HighDewPoint = -500,
			HighRain24h = -500,
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewPoint = 999,
			LowPress = 9999,
			LowHumidity = 100
		};

		// yesterdays highs and lows
		public DailyHighLow HiLoYest = new DailyHighLow()
		{
			HighTemp = -500,
			HighAppTemp = -500,
			HighFeelsLike = -500,
			HighHumidex = -500,
			HighHeatIndex = -500,
			HighDewPoint = -500,
			HighRain24h = -500,
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewPoint = 999,
			LowPress = 9999,
			LowHumidity = 100
		};

		// todays midnight highs and lows
		public DailyHighLow HiLoTodayMidnight = new DailyHighLow()
		{
			HighTemp = -500,
			LowTemp = 999
		};

		// todays midnight highs and lows
		public DailyHighLow HiLoYestMidnight = new DailyHighLow()
		{
			HighTemp = -500,
			LowTemp = 999
		};

		public int IndoorBattStatus;
		public int WindBattStatus;
		public int RainBattStatus;
		public int TempBattStatus;
		public int UVBattStatus;

		public double[] WMR200ExtraDPValues { get; set; }

		public bool[] WMR200ChannelPresent { get; set; }

		public double[] WMR200ExtraHumValues { get; set; }

		public double[] WMR200ExtraTempValues { get; set; }

		public DateTime lastDataReadTime;
		public bool haveReadData = false;

		public bool ExtraSensorsDetected = false;

		// Should Cumulus find the peak gust?
		// This gets set to false for Davis stations after logger download
		// if 10-minute gust period is in use, so we use the Davis value instead.
		public bool CalcRecentMaxGust = true;

		public SerialPort comport;

		//private TextWriterTraceListener myTextListener;

		private Thread mainThread;

		public Timer secondTimer;
		public double presstrendval;
		public double temptrendval;

		public int multicastsGood, multicastsBad;

		public bool timerStartNeeded = false;

		private readonly DateTime versionCheckTime;

		public SQLiteConnection RecentDataDb;
		// Extra sensors

		public double SolarElevation;

		public double SolarFactor = -1;  // used to adjust solar transmission factor (range 0-1), disabled = -1

		public bool WindReadyToPlot = false;
		public bool TempReadyToPlot = false;
		private bool first_temp = true;
		public double RG11RainYesterday { get; set; }

		public abstract void Start();

		public virtual string GetEcowittCameraUrl()
		{
			return string.Empty;
		}

		public virtual string GetEcowittVideoUrl()
		{
			return string.Empty;
		}


		public WeatherStation(Cumulus cumulus, bool extraStation = false)
		{
			// save the reference to the owner
			this.cumulus = cumulus;

			// if we are an extra station, then don't do any of the "normal" intialisation
			if (extraStation)
			{
				return;
			}

			// initialise the monthly array of records - element zero is not used
			for (var i = 1; i <= 12; i++)
			{
				MonthlyRecs[i] = new AllTimeRecords();
			}

			CumulusForecast = cumulus.Trans.ForecastNotAvailable;
			wsforecast = cumulus.Trans.ForecastNotAvailable;

			ExtraTemp = new double[11];
			ExtraHum = new double[11];
			ExtraDewPoint = new double[11];
			UserTemp = new double[9];
			SoilTemp = new double[17];

			windcounts = new double[16];
			WindRecent = new TWindRecent[MaxWindRecent];
			WindVec = new TWindVec[MaxWindRecent];

			ReadTodayFile();
			ReadYesterdayFile();
			ReadAlltimeIniFile();
			ReadMonthlyAlltimeIniFile();
			ReadMonthIniFile();
			ReadYearIniFile();
			_ = LoadDayFile();

			GetRainCounter();
			GetRainFallTotals();

			// Open database (create file if it doesn't exist)
			SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;

			//RecentDataDb = new SQLiteConnection(":memory:", true);
			//RecentDataDb = new SQLiteConnection(cumulus.dbfile, false);
			RecentDataDb = new SQLiteConnection(new SQLiteConnectionString(cumulus.dbfile, flags, false, null, null, null, null, "yyyy-MM-dd HH:mm:ss"));
			CheckSqliteDatabase(false);
			RecentDataDb.CreateTable<RecentData>();
			RecentDataDb.CreateTable<SqlCache>();
			RecentDataDb.CreateTable<CWindRecent>();
			RecentDataDb.Execute("create table if not exists WindRecentPointer (pntr INTEGER)");
			// switch off full synchronisation - the data base isn't that critical and we get a performance boost
			RecentDataDb.Execute("PRAGMA synchronous = NORMAL");

			// preload the failed sql cache - if any
			ReloadFailedMySQLCommands();

			var rnd = new Random();
			versionCheckTime = new DateTime(1, 1, 1, rnd.Next(0, 23), rnd.Next(0, 59), 0);

			SensorReception = new Dictionary<string, byte>();
		}

		private void CheckSqliteDatabase(bool giveup)
		{
			bool rebuild = false;
			int errorCount = 0;
			try
			{
				cumulus.LogMessage("Checking SQLite integrity...");
				var cmd = RecentDataDb.CreateCommand("PRAGMA quick_check;");
				var res = cmd.ExecuteQueryScalars<string>();

				errorCount = res.Count();

				if (errorCount == 1 && res.First() == "ok")
				{
					cumulus.LogMessage("SQLite integrity check OK");
					return;
				}

				foreach (var row in res)
				{
					cumulus.LogErrorMessage("SQLite integrity check result: " + row.Replace("\n", "\n    "));
					if (row == "database disk image is malformed")
						rebuild = true;
				}

			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("SQLite integrity check failed - " + ex.Message);
				rebuild = true;
			}

			if (rebuild || giveup)
			{
				cumulus.LogErrorMessage("Deleting RecentData database..");
				RecentDataDb.Close();
				File.Delete(cumulus.dbfile);
				// Open database (create file if it doesn't exist)
				SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
				RecentDataDb = new SQLiteConnection(new SQLiteConnectionString(cumulus.dbfile, flags, false, null, null, null, null, "yyyy-MM-dd HH:mm:ss"));
			}
			else if (errorCount > 0)
			{
				cumulus.LogErrorMessage("SQLite integrity check Failed, trying to compact database");
				try
				{
					RecentDataDb.Execute("vacuum;");
					cumulus.LogMessage("SQLite compact database complete, retesting integriry...");
					CheckSqliteDatabase(true);
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("SQLite compress failed - " + ex.Message);
					cumulus.LogErrorMessage("Deleting RecentData database..");
					RecentDataDb.Close();
					File.Delete(cumulus.dbfile);
					// Open database (create file if it doesn't exist)
					SQLiteOpenFlags flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
					RecentDataDb = new SQLiteConnection(new SQLiteConnectionString(cumulus.dbfile, flags, false, null, null, null, null, "yyyy-MM-dd HH:mm:ss"));
				}
			}
		}

		public void ReloadFailedMySQLCommands()
		{
			while (cumulus.MySqlFailedList.TryDequeue(out var _))
			{
				// do nothing
			};

			// preload the failed sql cache - if any
			var data = RecentDataDb.Query<SqlCache>("SELECT * FROM SqlCache ORDER BY key");

			foreach (var rec in data)
			{
				cumulus.MySqlFailedList.Enqueue(rec);
			}
		}

		private void GetRainCounter()
		{
			// do we need to do anything?
			if (!initialiseRainCounter && !initialiseMidnightRain && !initialiseRainDayStart)
			{
				cumulus.LogMessage("GetRainCounter: Nothing to do");
				return;
			}

			// Find today's rain so far from last record in log file
			bool raindaystartfound = false;
			bool raincounterfound = false;
			bool midnightrainfound = false;

			string LogFile = cumulus.GetLogFileName(cumulus.LastUpdateTime);
			double raintoday = 0;
			double raincounter = 0;
			double midnightraincounter = 0;
			double raindaystart = 0;

			string logdate = "00/00/00";
			string prevlogdate = "00/00/00";

			string listSep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
			string todaydatestring = cumulus.LastUpdateTime.ToString("dd/MM/yy");

			var lastDate = cumulus.LastUpdateTime.AddHours(cumulus.GetHourInc(cumulus.LastUpdateTime));
			var meteoDate = new DateTime(lastDate.Year, lastDate.Month, lastDate.Day, -cumulus.GetHourInc(cumulus.LastUpdateTime), 0, 0);

			cumulus.LogMessage("GetRainCounter: Finding raintoday from logfile " + LogFile);
			cumulus.LogMessage("GetRainCounter: Expecting listsep=" + listSep + " decimal=" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

			if (File.Exists(LogFile))
			{
				int linenum = 0;
				try
				{
					var lines = File.ReadAllLines(LogFile);

					foreach (var line in lines)
					{
						// now process each record to get the last "raintoday" figure
						linenum++;
						var st = new List<string>(Regex.Split(line, listSep));
						if (st.Count > 0)
						{
							raintoday = Double.Parse(st[9]);
							raincounter = Double.Parse(st[11]);

							raincounterfound = true;

							// get date of this entry
							logdate = st[0];

							if (logdate != prevlogdate && todaydatestring == logdate)
							{
								if ((initialiseMidnightRain && !midnightrainfound) || (cumulus.RolloverHour == 0 && initialiseRainDayStart && !raindaystartfound))
								{
									if (initialiseMidnightRain)
									{
										// this is the first entry of a new day AND the new day is today
										midnightrainfound = true;
										midnightraincounter = Double.Parse(st[11]) - Double.Parse(st[9]);
										cumulus.LogMessage($"GetRainCounter: Midnight rain counter {midnightraincounter:F4} found in the following entry:");
									}

									if (initialiseRainDayStart)
									{
										raindaystartfound = true;
										raindaystart = Double.Parse(st[11]) - Double.Parse(st[9]);
										cumulus.LogMessage($"GetRainCounter: Rain day start counter {raindaystart:F4} found in the following entry:");
									}
									cumulus.LogMessage(line);
								}
							}

							if (initialiseRainDayStart && !raindaystartfound && cumulus.RolloverHour != 0)
							{
								var logDateTime = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);
								if (logDateTime >= meteoDate)
								{
									raindaystartfound = true;
									raindaystart = Double.Parse(st[11]) - Double.Parse(st[9]);
									cumulus.LogMessage($"GetRainCounter: Rain day start counter {raindaystart:F4} found in the following entry:");
									cumulus.LogMessage(line);
								}
							}

							prevlogdate = logdate;
						}
					}
				}
				catch (Exception E)
				{
					cumulus.LogErrorMessage("GetRainCounter: Error on line " + linenum + " of " + LogFile + ": " + E.Message);
				}
			}

			if (initialiseMidnightRain)
			{
				if (midnightrainfound)
				{
					if (Math.Round(MidnightRainCount, cumulus.RainDPlaces) == Math.Round(midnightraincounter, cumulus.RainDPlaces))
					{
						cumulus.LogMessage($"GetRainCounter: Rain day start counter {RainCounterDayStart:F4} and midnight rain counter {midnightraincounter:F4} match within rounding error, setting midnight rain to rain day start value");
						MidnightRainCount = RainCounterDayStart;
					}
					else
					{
						cumulus.LogMessage($"GetRainCounter: Midnight rain counter found, setting existing midnight rain counter {MidnightRainCount:F4} to log file value {midnightraincounter:F4}");
						MidnightRainCount = midnightraincounter;
					}
				}
				else
				{
					cumulus.LogMessage("GetRainCounter: Midnight rain counter not found, setting midnight count to raindaystart = " + RainCounterDayStart);
					MidnightRainCount = RainCounterDayStart;
				}

				initialiseMidnightRain = false;
			}

			if ((logdate.Substring(0, 2) == "01") && (logdate.Substring(3, 2) == cumulus.RainSeasonStart.ToString("D2")) && (cumulus.Manufacturer == cumulus.DAVIS))
			{
				// special case: rain counter is about to be reset
				//TODO: MC: Hmm are there issues here, what if the console clock is wrong and it does not reset for another hour, or it already reset and we have had rain since?
				var month = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(cumulus.RainSeasonStart);
				cumulus.LogMessage($"GetRainCounter: Special case, Davis station on 1st of {month}. Set midnight rain count to zero");
				MidnightRainCount = 0;
			}

			if (initialiseRainDayStart && raindaystartfound)
			{
				cumulus.LogMessage($"GetRainCounter: Rain day start counter found, setting existing start rain counter {RainCounterDayStart:F4} to log file value {raindaystart:F4}");
				RainCounterDayStart = raindaystart;
				initialiseRainDayStart = false;
			}

			// If we do not have a rain counter value for start of day from Today.ini, then use the last value from the log file
			if (initialiseRainCounter && raincounterfound)
			{
				cumulus.LogMessage($"GetRainCounter: Rain counter found, setting existing rain counter {RainCounter:F4} to log file value {raincounter:F4}");
				RainCounter = raincounter;
				initialiseRainCounter = false;
			}

			if (RainCounter < 0)
			{
				cumulus.LogMessage("GetRainCounter: Rain counter negative, setting to zero");
				RainCounter = 0;
			}
		}

		public void GetRainFallTotals()
		{
			cumulus.LogMessage("GetRainFallTotals: Getting rain totals, rain season start = " + cumulus.RainSeasonStart);
			RainThisMonth = 0;
			RainThisYear = 0;
			// get today's date for month check; allow for 0900 roll-over
			var hourInc = cumulus.GetHourInc();
			var ModifiedNow = DateTime.Now.AddHours(hourInc);
			// avoid any funny locale peculiarities on date formats
			string Today = ModifiedNow.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
			cumulus.LogMessage("GetRainFallTotals: Today = " + Today);
			// get today's date offset by rain season start for year check
			int offsetYearToday = ModifiedNow.AddMonths(-(cumulus.RainSeasonStart - 1)).Year;

			try
			{
				foreach (var rec in DayFile)
				{
					int offsetLoggedYear = rec.Date.AddMonths(-(cumulus.RainSeasonStart - 1)).Year;
					// This year?
					if (offsetLoggedYear == offsetYearToday)
					{
						RainThisYear += rec.TotalRain;
					}
					// This month?
					if ((rec.Date.Month == ModifiedNow.Month) && (rec.Date.Year == ModifiedNow.Year))
					{
						RainThisMonth += rec.TotalRain;
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetRainfallTotals: Error - " + ex.Message);
			}

			cumulus.LogMessage("GetRainFallTotals: Rainthismonth from dayfile: " + RainThisMonth);
			cumulus.LogMessage("GetRainFallTotals: Rainthisyear from dayfile: " + RainThisYear);

			// Add in year-to-date rain (if necessary)
			if (cumulus.YTDrainyear == Convert.ToInt32(Today.Substring(6, 2)) + 2000)
			{
				cumulus.LogMessage($"GetRainFallTotals: Adding YTD rain: {cumulus.YTDrain}, new Rainthisyear: {RainThisYear}");
				RainThisYear += cumulus.YTDrain;
			}
			RainMonth = RainThisMonth;
			RainYear = RainThisYear;
		}

		public void UpdateYearMonthRainfall()
		{
			var _month = RainMonth;
			var _year = RainYear;
			RainMonth = RainThisMonth + RainToday;
			RainYear = RainThisYear + RainToday;
			cumulus.LogMessage($"Rainthismonth Updated from: {_month.ToString(cumulus.RainFormat)} to: {RainMonth.ToString(cumulus.RainFormat)}");
			cumulus.LogMessage($"Rainthisyear Updated from: {_year.ToString(cumulus.RainFormat)} to: {RainYear.ToString(cumulus.RainFormat)}");

		}

		public void ReadTodayFile()
		{
			if (!File.Exists(cumulus.TodayIniFile))
			{
				FirstRun = true;
			}

			IniFile ini = new IniFile(cumulus.TodayIniFile);

			var todayfiledate = ini.GetValue("General", "Date", "00/00/00");
			var timestampstr = ini.GetValue("General", "Timestamp", DateTime.Now.ToString("s"));

			cumulus.LogConsoleMessage("Last update: " + timestampstr);

			cumulus.LastUpdateTime = DateTime.Parse(timestampstr);
			var todayDate = cumulus.LastUpdateTime.Date;

			cumulus.LogMessage("ReadTodayFile: Last update time from today.ini: " + cumulus.LastUpdateTime);

			DateTime metoTodayDate = cumulus.LastUpdateTime.AddHours(cumulus.GetHourInc()).Date;

			int defaultyear = metoTodayDate.Year;
			int defaultmonth = metoTodayDate.Month;
			int defaultday = metoTodayDate.Day;

			CurrentYear = ini.GetValue("General", "CurrentYear", defaultyear);
			CurrentMonth = ini.GetValue("General", "CurrentMonth", defaultmonth);
			CurrentDay = ini.GetValue("General", "CurrentDay", defaultday);

			cumulus.LogMessage("ReadTodayFile: Date = " + todayfiledate + ", LastUpdateTime = " + cumulus.LastUpdateTime + ", Month = " + CurrentMonth);

			LastRainTip = ini.GetValue("Rain", "LastTip", "0000-00-00 00:00");

			FOSensorClockTime = ini.GetValue("FineOffset", "FOSensorClockTime", DateTime.MinValue);
			FOStationClockTime = ini.GetValue("FineOffset", "FOStationClockTime", DateTime.MinValue);
			FOSolarClockTime = ini.GetValue("FineOffset", "FOSolarClockTime", DateTime.MinValue);
			if (cumulus.FineOffsetOptions.SyncReads && (cumulus.StationType == StationTypes.FineOffset || cumulus.StationType == StationTypes.FineOffsetSolar))
			{
				cumulus.LogMessage("ReadTodayFile: Sensor clock  " + FOSensorClockTime.ToLongTimeString());
				cumulus.LogMessage("ReadTodayFile: Station clock " + FOStationClockTime.ToLongTimeString());
			}
			ConsecutiveRainDays = ini.GetValue("Rain", "ConsecutiveRainDays", 0);
			ConsecutiveDryDays = ini.GetValue("Rain", "ConsecutiveDryDays", 0);

			AnnualETTotal = ini.GetValue("ET", "Annual", 0.0);
			StartofdayET = ini.GetValue("ET", "Startofday", -1.0);
			if (StartofdayET < 0)
			{
				cumulus.LogMessage("ReadTodayFile: ET not initialised");
				noET = true;
			}
			else
			{
				ET = AnnualETTotal - StartofdayET;
				cumulus.LogMessage("ReadTodayFile: ET today = " + ET.ToString(cumulus.ETFormat));
			}
			ChillHours = ini.GetValue("Temp", "ChillHours", 0.0);

			// NOAA report names
			cumulus.NOAAconf.LatestMonthReport = ini.GetValue("NOAA", "LatestMonthlyReport", "");
			cumulus.NOAAconf.LatestYearReport = ini.GetValue("NOAA", "LatestYearlyReport", "");

			// Solar
			HiLoToday.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0);
			HiLoToday.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", todayDate);
			HiLoToday.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			HiLoToday.HighUvTime = ini.GetValue("Solar", "HighUVTime", metoTodayDate);
			StartOfDaySunHourCounter = ini.GetValue("Solar", "SunStart", -9999.0);
			RG11RainToday = ini.GetValue("Rain", "RG11Today", 0.0);

			// Wind
			HiLoToday.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			HiLoToday.HighWindTime = ini.GetValue("Wind", "SpTime", metoTodayDate);
			HiLoToday.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			HiLoToday.HighGustTime = ini.GetValue("Wind", "Time", metoTodayDate);
			HiLoToday.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);
			WindRunToday = ini.GetValue("Wind", "Windrun", 0.0);
			DominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			DominantWindBearingMinutes = ini.GetValue("Wind", "DominantWindBearingMinutes", 0);
			DominantWindBearingX = ini.GetValue("Wind", "DominantWindBearingX", 0.0);
			DominantWindBearingY = ini.GetValue("Wind", "DominantWindBearingY", 0.0);

			// Temperature
			HiLoToday.LowTemp = ini.GetValue("Temp", "Low", 999.0);
			HiLoToday.LowTempTime = ini.GetValue("Temp", "LTime", metoTodayDate);
			HiLoToday.HighTemp = ini.GetValue("Temp", "High", -999.0);
			HiLoToday.HighTempTime = ini.GetValue("Temp", "HTime", metoTodayDate);
			if ((HiLoToday.HighTemp > -400) && (HiLoToday.LowTemp < 400))
				HiLoToday.TempRange = HiLoToday.HighTemp - HiLoToday.LowTemp;
			else
				HiLoToday.TempRange = 0;
			TempTotalToday = ini.GetValue("Temp", "Total", 0.0);
			tempsamplestoday = ini.GetValue("Temp", "Samples", 1);
			HeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			CoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			GrowingDegreeDaysThisYear1 = ini.GetValue("Temp", "GrowingDegreeDaysThisYear1", 0.0);
			GrowingDegreeDaysThisYear2 = ini.GetValue("Temp", "GrowingDegreeDaysThisYear2", 0.0);

			// Temperature midnight rollover
			HiLoTodayMidnight.LowTemp = ini.GetValue("TempMidnight", "Low", 999.0);
			HiLoTodayMidnight.LowTempTime = ini.GetValue("TempMidnight", "LTime", metoTodayDate);
			HiLoTodayMidnight.HighTemp = ini.GetValue("TempMidnight", "High", -999.0);
			HiLoTodayMidnight.HighTempTime = ini.GetValue("TempMidnight", "HTime", metoTodayDate);

			// Pressure
			HiLoToday.LowPress = ini.GetValue("Pressure", "Low", 9999.0);
			HiLoToday.LowPressTime = ini.GetValue("Pressure", "LTime", metoTodayDate);
			HiLoToday.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoToday.HighPressTime = ini.GetValue("Pressure", "HTime", metoTodayDate);

			// rain
			HiLoToday.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			HiLoToday.HighRainRateTime = ini.GetValue("Rain", "HTime", metoTodayDate);
			HiLoToday.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoToday.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", metoTodayDate);
			HiLoToday.HighRain24h = ini.GetValue("Rain", "High24h", 0.0);
			HiLoToday.HighRain24hTime = ini.GetValue("Rain", "High24hTime", metoTodayDate);
			RainYesterday = ini.GetValue("Rain", "Yesterday", 0.0);
			RainCounterDayStart = ini.GetValue("Rain", "Start", -1.0);
			MidnightRainCount = ini.GetValue("Rain", "Midnight", -1.0);
			RainCounter = ini.GetValue("Rain", "Last", -1.0);

			if (RainCounterDayStart == -1.0)
			{
				cumulus.LogMessage("ReadTodayfile: set initialiseRainDayStart true");
				initialiseRainDayStart = true;
			}
			else
			{
				initialiseRainDayStart = false;
			}

			if (RainCounter == -1.0)
			{
				cumulus.LogMessage("ReadTodayfile: set initialiseRainCounterOnFirstData true");
				initialiseRainCounter = true;
			}
			else
			{
				initialiseRainCounter = false;
			}

			if (MidnightRainCount == -1.0)
			{
				if (cumulus.RolloverHour == 0 && !initialiseRainDayStart)
				{
					// midnight and rollover are the same
					MidnightRainCount = RainCounterDayStart;
					initialiseMidnightRain = false;
				}
				else
				{
					cumulus.LogMessage("ReadTodayfile: set initialiseMidnightRain true");
					initialiseMidnightRain = true;
				}
			}
			else
			{
				initialiseMidnightRain = false;
			}

			cumulus.LogMessage($"ReadTodayfile: Rain day start: {RainCounterDayStart:F4}, midnight counter: {MidnightRainCount:F4}, last counter: {RainCounter:F4}");

			// humidity
			HiLoToday.LowHumidity = ini.GetValue("Humidity", "Low", 100);
			HiLoToday.HighHumidity = ini.GetValue("Humidity", "High", 0);
			HiLoToday.LowHumidityTime = ini.GetValue("Humidity", "LTime", metoTodayDate);
			HiLoToday.HighHumidityTime = ini.GetValue("Humidity", "HTime", metoTodayDate);

			// Solar
			SunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			SunshineToMidnight = ini.GetValue("Solar", "SunshineHoursToMidnight", 0.0);

			// heat index
			HiLoToday.HighHeatIndex = ini.GetValue("HeatIndex", "High", -999.0);
			HiLoToday.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", metoTodayDate);

			// Apparent temp
			HiLoToday.HighAppTemp = ini.GetValue("AppTemp", "High", -999.0);
			HiLoToday.HighAppTempTime = ini.GetValue("AppTemp", "HTime", metoTodayDate);
			HiLoToday.LowAppTemp = ini.GetValue("AppTemp", "Low", 999.0);
			HiLoToday.LowAppTempTime = ini.GetValue("AppTemp", "LTime", metoTodayDate);

			// wind chill
			HiLoToday.LowWindChill = ini.GetValue("WindChill", "Low", 999.0);
			HiLoToday.LowWindChillTime = ini.GetValue("WindChill", "LTime", metoTodayDate);

			// Dew point
			HiLoToday.HighDewPoint = ini.GetValue("Dewpoint", "High", -999.0);
			HiLoToday.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", metoTodayDate);
			HiLoToday.LowDewPoint = ini.GetValue("Dewpoint", "Low", 999.0);
			HiLoToday.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", metoTodayDate);

			// Feels like
			HiLoToday.HighFeelsLike = ini.GetValue("FeelsLike", "High", -999.0);
			HiLoToday.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", metoTodayDate);
			HiLoToday.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 999.0);
			HiLoToday.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", metoTodayDate);

			// Humidex
			HiLoToday.HighHumidex = ini.GetValue("Humidex", "High", -999.0);
			HiLoToday.HighHumidexTime = ini.GetValue("Humidex", "HTime", metoTodayDate);

			// Records
			AlltimeRecordTimestamp = ini.GetValue("Records", "Alltime", DateTime.MinValue);

			// Lightning (GW1000 for now)
			LightningDistance = ini.GetValue("Lightning", "Distance", -1.0);
			LightningTime = ini.GetValue("Lightning", "LastStrike", DateTime.MinValue);
		}

		public void WriteTodayFile(DateTime timestamp, bool Log)
		{
			try
			{
				var hourInc = cumulus.GetHourInc();

				IniFile ini = new IniFile(cumulus.TodayIniFile);

				// Date
				ini.SetValue("General", "Date", timestamp.AddHours(hourInc).ToShortDateString());
				// Timestamp
				ini.SetValue("General", "Timestamp", cumulus.LastUpdateTime.ToString("s"));
				ini.SetValue("General", "CurrentYear", CurrentYear);
				ini.SetValue("General", "CurrentMonth", CurrentMonth);
				ini.SetValue("General", "CurrentDay", CurrentDay);
				// Wind
				ini.SetValue("Wind", "Speed", HiLoToday.HighWind);
				ini.SetValue("Wind", "SpTime", HiLoToday.HighWindTime.ToString("HH:mm"));
				ini.SetValue("Wind", "Gust", HiLoToday.HighGust);
				ini.SetValue("Wind", "Time", HiLoToday.HighGustTime.ToString("HH:mm"));
				ini.SetValue("Wind", "Bearing", HiLoToday.HighGustBearing);
				ini.SetValue("Wind", "Direction", CompassPoint(HiLoToday.HighGustBearing));
				ini.SetValue("Wind", "Windrun", WindRunToday);
				ini.SetValue("Wind", "DominantWindBearing", DominantWindBearing);
				ini.SetValue("Wind", "DominantWindBearingMinutes", DominantWindBearingMinutes);
				ini.SetValue("Wind", "DominantWindBearingX", DominantWindBearingX);
				ini.SetValue("Wind", "DominantWindBearingY", DominantWindBearingY);
				// Temperature
				ini.SetValue("Temp", "Low", HiLoToday.LowTemp);
				ini.SetValue("Temp", "LTime", HiLoToday.LowTempTime.ToString("HH:mm"));
				ini.SetValue("Temp", "High", HiLoToday.HighTemp);
				ini.SetValue("Temp", "HTime", HiLoToday.HighTempTime.ToString("HH:mm"));
				ini.SetValue("Temp", "Total", TempTotalToday);
				ini.SetValue("Temp", "Samples", tempsamplestoday);
				ini.SetValue("Temp", "ChillHours", ChillHours);
				ini.SetValue("Temp", "HeatingDegreeDays", HeatingDegreeDays);
				ini.SetValue("Temp", "CoolingDegreeDays", CoolingDegreeDays);
				ini.SetValue("Temp", "GrowingDegreeDaysThisYear1", GrowingDegreeDaysThisYear1);
				ini.SetValue("Temp", "GrowingDegreeDaysThisYear2", GrowingDegreeDaysThisYear2);
				// Temperature midnight rollover
				ini.SetValue("TempMidnight", "Low", HiLoTodayMidnight.LowTemp);
				ini.SetValue("TempMidnight", "LTime", HiLoTodayMidnight.LowTempTime);
				ini.SetValue("TempMidnight", "High", HiLoTodayMidnight.HighTemp);
				ini.SetValue("TempMidnight", "HTime", HiLoTodayMidnight.HighTempTime);
				// Pressure
				ini.SetValue("Pressure", "Low", HiLoToday.LowPress);
				ini.SetValue("Pressure", "LTime", HiLoToday.LowPressTime.ToString("HH:mm"));
				ini.SetValue("Pressure", "High", HiLoToday.HighPress);
				ini.SetValue("Pressure", "HTime", HiLoToday.HighPressTime.ToString("HH:mm"));
				// rain
				ini.SetValue("Rain", "High", HiLoToday.HighRainRate);
				ini.SetValue("Rain", "HTime", HiLoToday.HighRainRateTime.ToString("HH:mm"));
				ini.SetValue("Rain", "HourlyHigh", HiLoToday.HighHourlyRain);
				ini.SetValue("Rain", "HHourlyTime", HiLoToday.HighHourlyRainTime.ToString("HH:mm"));
				ini.SetValue("Rain", "High24h", HiLoToday.HighRain24h);
				ini.SetValue("Rain", "High24hTime", HiLoToday.HighRain24hTime.ToString("HH:mm"));
				ini.SetValue("Rain", "Yesterday", RainYesterday);
				ini.SetValue("Rain", "Start", RainCounterDayStart);
				ini.SetValue("Rain", "Midnight", MidnightRainCount);
				ini.SetValue("Rain", "Last", RainCounter);
				ini.SetValue("Rain", "LastTip", LastRainTip);
				ini.SetValue("Rain", "ConsecutiveRainDays", ConsecutiveRainDays);
				ini.SetValue("Rain", "ConsecutiveDryDays", ConsecutiveDryDays);
				ini.SetValue("Rain", "RG11Today", RG11RainToday);
				// ET
				ini.SetValue("ET", "Annual", AnnualETTotal);
				ini.SetValue("ET", "Startofday", StartofdayET);
				// humidity
				ini.SetValue("Humidity", "Low", HiLoToday.LowHumidity);
				ini.SetValue("Humidity", "High", HiLoToday.HighHumidity);
				ini.SetValue("Humidity", "LTime", HiLoToday.LowHumidityTime.ToString("HH:mm"));
				ini.SetValue("Humidity", "HTime", HiLoToday.HighHumidityTime.ToString("HH:mm"));
				// Solar
				ini.SetValue("Solar", "SunshineHours", SunshineHours);
				ini.SetValue("Solar", "SunshineHoursToMidnight", SunshineToMidnight);
				// heat index
				ini.SetValue("HeatIndex", "High", HiLoToday.HighHeatIndex);
				ini.SetValue("HeatIndex", "HTime", HiLoToday.HighHeatIndexTime.ToString("HH:mm"));
				// App temp
				ini.SetValue("AppTemp", "Low", HiLoToday.LowAppTemp);
				ini.SetValue("AppTemp", "LTime", HiLoToday.LowAppTempTime.ToString("HH:mm"));
				ini.SetValue("AppTemp", "High", HiLoToday.HighAppTemp);
				ini.SetValue("AppTemp", "HTime", HiLoToday.HighAppTempTime.ToString("HH:mm"));
				// Feels like
				ini.SetValue("FeelsLike", "Low", HiLoToday.LowFeelsLike);
				ini.SetValue("FeelsLike", "LTime", HiLoToday.LowFeelsLikeTime.ToString("HH:mm"));
				ini.SetValue("FeelsLike", "High", HiLoToday.HighFeelsLike);
				ini.SetValue("FeelsLike", "HTime", HiLoToday.HighFeelsLikeTime.ToString("HH:mm"));
				// Humidex
				ini.SetValue("Humidex", "High", HiLoToday.HighHumidex);
				ini.SetValue("Humidex", "HTime", HiLoToday.HighHumidexTime.ToString("HH:mm"));
				// wind chill
				ini.SetValue("WindChill", "Low", HiLoToday.LowWindChill);
				ini.SetValue("WindChill", "LTime", HiLoToday.LowWindChillTime.ToString("HH:mm"));
				// Dewpoint
				ini.SetValue("Dewpoint", "Low", HiLoToday.LowDewPoint);
				ini.SetValue("Dewpoint", "LTime", HiLoToday.LowDewPointTime.ToString("HH:mm"));
				ini.SetValue("Dewpoint", "High", HiLoToday.HighDewPoint);
				ini.SetValue("Dewpoint", "HTime", HiLoToday.HighDewPointTime.ToString("HH:mm"));

				// NOAA report names
				ini.SetValue("NOAA", "LatestMonthlyReport", cumulus.NOAAconf.LatestMonthReport);
				ini.SetValue("NOAA", "LatestYearlyReport", cumulus.NOAAconf.LatestYearReport);

				// Solar
				ini.SetValue("Solar", "HighSolarRad", HiLoToday.HighSolar);
				ini.SetValue("Solar", "HighSolarRadTime", HiLoToday.HighSolarTime.ToString("HH:mm"));
				ini.SetValue("Solar", "HighUV", HiLoToday.HighUv);
				ini.SetValue("Solar", "HighUVTime", HiLoToday.HighUvTime.ToString("HH:mm"));
				ini.SetValue("Solar", "SunStart", StartOfDaySunHourCounter);

				// Special Fine Offset data
				ini.SetValue("FineOffset", "FOSensorClockTime", FOSensorClockTime);
				ini.SetValue("FineOffset", "FOStationClockTime", FOStationClockTime);
				ini.SetValue("FineOffset", "FOSolarClockTime", FOSolarClockTime);

				// Records
				ini.SetValue("Records", "Alltime", AlltimeRecordTimestamp);

				// Lightning (GW1000 for now)
				ini.SetValue("Lightning", "Distance", LightningDistance);
				ini.SetValue("Lightning", "LastStrike", LightningTime);


				if (Log)
				{
					cumulus.LogMessage("Writing today.ini, LastUpdateTime = " + cumulus.LastUpdateTime + " raindaystart = " + RainCounterDayStart.ToString("F2") + " rain counter = " + RainCounter.ToString("F2"));

					if (cumulus.FineOffsetStation)
					{
						cumulus.LogMessage("WriteTodayFile: Latest FO reading: " + LatestFOReading);
					}
					else if (cumulus.StationType == StationTypes.Instromet)
					{
						cumulus.LogMessage("WriteTodayFile: Latest Instromet reading: " + cumulus.LatestImetReading);
					}
				}

				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("Error writing today.ini: " + ex.Message);
			}
		}


		/// <summary>
		/// calculate the start of today in UTC
		/// </summary>
		/// <returns>timestamp of start of today UTC</returns>
		/*
		private DateTime StartOfTodayUTC()
		{
			DateTime now = DateTime.Now;

			int y = now.Year;
			int m = now.Month;
			int d = now.Day;

			return new DateTime(y, m, d, 0, 0, 0).ToUniversalTime();
		}
		*/

		/// <summary>
		/// calculate the start of yesterday in UTC
		/// </summary>
		/// <returns>timestamp of start of yesterday UTC</returns>
		/*
		private DateTime StartOfYesterdayUTC()
		{
			DateTime yesterday = DateTime.Now.AddDays(-1);

			int y = yesterday.Year;
			int m = yesterday.Month;
			int d = yesterday.Day;

			return new DateTime(y, m, d, 0, 0, 0).ToUniversalTime();
		}
		*/

		/// <summary>
		/// calculate the start of this year in UTC
		/// </summary>
		/// <returns>timestamp of start of year in UTC</returns>
		/*
		private DateTime StartOfYearUTC()
		{
			DateTime now = DateTime.Now;
			int y = now.Year;

			return new DateTime(y, 1, 1, 0, 0, 0).ToUniversalTime();
		}
		*/

		/// <summary>
		/// calculate the start of this month in UTC
		/// </summary>
		/// <returns>timestamp of start of month in UTC</returns>
		/*
		private DateTime StartOfMonthUTC()
		{
			DateTime now = DateTime.Now;
			int y = now.Year;
			int m = now.Month;

			return new DateTime(y, m, 1, 0, 0, 0).ToUniversalTime();
		}
		*/

		/// <summary>
		/// calculate the start of this year in OAdate
		/// </summary>
		/// <returns>timestamp of start of year in OAdate</returns>
		/*
		private int StartOfYearOADate()
		{
			DateTime now = DateTime.Now;
			int y = now.Year;

			return (int) new DateTime(y, 1, 1, 0, 0, 0).ToOADate();
		}
		*/

		/// <summary>
		/// calculate the start of this month in OADate
		/// </summary>
		/// <returns>timestamp of start of month in OADate</returns>
		/*
		private int StartOfMonthOADate()
		{
			DateTime now = DateTime.Now;
			int y = now.Year;
			int m = now.Month;

			return (int) new DateTime(y, m, 1, 0, 0, 0).ToOADate();
		}
		*/

		/// <summary>
		/// Indoor temperature in C
		/// </summary>
		public double IndoorTemperature { get; set; } = 0;

		/// <summary>
		/// Solar Radiation in W/m2
		/// </summary>
		public int SolarRad { get; set; } = 0;

		/// <summary>
		/// UV index
		/// </summary>
		public double UV { get; set; } = 0;

		public void UpdatePressureTrendString()
		{
			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.Units.Press)
			{
				case 0:
				case 1:
					threeHourlyPressureChangeMb = presstrendval * 3;
					break;
				case 2:
					threeHourlyPressureChangeMb = presstrendval * 3 / 0.0295333727;
					break;
			}

			if (threeHourlyPressureChangeMb > 6) Presstrendstr = cumulus.Trans.Risingveryrapidly;
			else if (threeHourlyPressureChangeMb > 3.5) Presstrendstr = cumulus.Trans.Risingquickly;
			else if (threeHourlyPressureChangeMb > 1.5) Presstrendstr = cumulus.Trans.Rising;
			else if (threeHourlyPressureChangeMb > 0.1) Presstrendstr = cumulus.Trans.Risingslowly;
			else if (threeHourlyPressureChangeMb > -0.1) Presstrendstr = cumulus.Trans.Steady;
			else if (threeHourlyPressureChangeMb > -1.5) Presstrendstr = cumulus.Trans.Fallingslowly;
			else if (threeHourlyPressureChangeMb > -3.5) Presstrendstr = cumulus.Trans.Falling;
			else if (threeHourlyPressureChangeMb > -6) Presstrendstr = cumulus.Trans.Fallingquickly;
			else
				Presstrendstr = cumulus.Trans.Fallingveryrapidly;
		}

		public string Presstrendstr { get; set; }

		public void CheckMonthlyAlltime(string index, double value, bool higher, DateTime timestamp)
		{
			lock (monthlyalltimeIniThreadLock)
			{
				bool recordbroken;

				// Make the delta relate to the precision for derived values such as feels like
				string[] derivedVals = { "HighHeatIndex", "HighAppTemp", "LowAppTemp", "LowChill", "HighHumidex", "HighDewPoint", "LowDewPoint", "HighFeelsLike", "LowFeelsLike" };

				double epsilon = derivedVals.Contains(index) ? Math.Pow(10, -cumulus.TempDPlaces) : 0.001; // required difference for new record

				int month;
				int day;
				int year;


				// Determine month day and year
				if (cumulus.RolloverHour == 0)
				{
					month = timestamp.Month;
					day = timestamp.Day;
					year = timestamp.Year;
				}
				else
				{
					TimeZone tz = TimeZone.CurrentTimeZone;
					DateTime adjustedTS;

					if (cumulus.Use10amInSummer && tz.IsDaylightSavingTime(timestamp))
					{
						// Locale is currently on Daylight (summer) time
						adjustedTS = timestamp.AddHours(-10);
					}
					else
					{
						// Locale is currently on Standard time or unknown
						adjustedTS = timestamp.AddHours(-9);
					}

					month = adjustedTS.Month;
					day = adjustedTS.Day;
					year = adjustedTS.Year;
				}

				AllTimeRec rec = MonthlyRecs[month][index];

				double oldvalue = rec.Val;
				//DateTime oldts = monthlyrecarray[index, month].timestamp;

				if (higher)
				{
					// check new value is higher than existing record
					recordbroken = (value - oldvalue >= epsilon);
				}
				else
				{
					// check new value is lower than existing record
					recordbroken = (oldvalue - value >= epsilon);
				}

				if (recordbroken)
				{
					// records which apply to whole days or months need their timestamps adjusting
					if ((index == "MonthlyRain") || (index == "DailyRain"))
					{
						DateTime CurrentMonthTS = new DateTime(year, month, day);
						SetMonthlyAlltime(rec, value, CurrentMonthTS);
					}
					else
					{
						SetMonthlyAlltime(rec, value, timestamp);
					}
				}
			}
		}

		private string FormatDateTime(string fmt, DateTime timestamp)
		{
			return timestamp.ToString(fmt);
		}


		public int CurrentDay { get; set; }

		public int CurrentMonth { get; set; }

		public int CurrentYear { get; set; }

		/// <summary>
		/// Indoor relative humidity in %
		/// </summary>
		public int IndoorHumidity { get; set; } = 0;

		/// <summary>
		/// Sea-level pressure
		/// </summary>
		public double Pressure { get; set; } = 0;

		public double StationPressure { get; set; } = 0;

		/// <summary>
		/// Outdoor temp
		/// </summary>
		public double OutdoorTemperature { get; set; } = 0;

		/// <summary>
		/// Outdoor dew point
		/// </summary>
		public double OutdoorDewpoint { get; set; } = 0;

		/// <summary>
		/// Wind chill
		/// </summary>
		public double WindChill { get; set; } = 0;

		/// <summary>
		/// Outdoor relative humidity in %
		/// </summary>
		public int OutdoorHumidity { get; set; } = 0;

		/// <summary>
		/// Apparent temperature
		/// </summary>
		public double ApparentTemperature { get; set; }

		/// <summary>
		/// Heat index
		/// </summary>
		public double HeatIndex { get; set; } = 0;

		/// <summary>
		/// Humidex
		/// </summary>
		public double Humidex { get; set; } = 0;

		/// <summary>
		/// Feels like (JAG/TI)
		/// </summary>
		public double FeelsLike { get; set; } = 0;


		/// <summary>
		/// Latest wind speed/gust
		/// </summary>
		public double WindLatest { get; set; } = 0;

		/// <summary>
		/// Average wind speed
		/// </summary>
		public double WindAverage { get; set; } = 0;
		private double WindAverageUncalibrated;

		/// <summary>
		/// Peak wind gust in last 10 minutes
		/// </summary>
		public double RecentMaxGust { get; set; } = 0;

		/// <summary>
		/// Wind direction in degrees
		/// </summary>
		public int Bearing { get; set; } = 0;

		/// <summary>
		/// Wind direction as compass points
		/// </summary>
		public string BearingText { get; set; } = "---";

		/// <summary>
		/// Wind direction in degrees
		/// </summary>
		public int AvgBearing { get; set; } = 0;

		/// <summary>
		/// Wind direction as compass points
		/// </summary>
		public string AvgBearingText { get; set; } = "---";

		/// <summary>
		/// Rainfall today
		/// </summary>
		public double RainToday { get; set; } = 0;

		/// <summary>
		/// Rain this month
		/// </summary>
		public double RainMonth { get; set; } = 0;

		/// <summary>
		/// Rain this year
		/// </summary>
		public double RainYear { get; set; } = 0;

		/// <summary>
		/// Current rain rate
		/// </summary>
		public double RainRate { get; set; } = 0;

		public double ET { get; set; }

		public double LightValue { get; set; }

		public double HeatingDegreeDays { get; set; }

		public double CoolingDegreeDays { get; set; }

		public double GrowingDegreeDaysThisYear1 { get; set; }
		public double GrowingDegreeDaysThisYear2 { get; set; }

		public int tempsamplestoday { get; set; }

		public double TempTotalToday { get; set; }

		public double ChillHours { get; set; }

		public double MidnightRainCount { get; set; }

		public int MidnightRainResetDay { get; set; }


		public DateTime lastSpikeRemoval = DateTime.MinValue;
		private double previousPress = 9999;
		public double previousGust = 999;
		private double previousWind = 999;
		private int previousHum = 999;
		private int previousInHum = 999;
		private double previousTemp = 999;
		private double previousInTemp = 999;


		public void UpdateDegreeDays(int interval)
		{
			if (OutdoorTemperature < cumulus.NOAAconf.HeatThreshold)
			{
				HeatingDegreeDays += (((cumulus.NOAAconf.HeatThreshold - OutdoorTemperature) * interval) / 1440);
			}
			if (OutdoorTemperature > cumulus.NOAAconf.CoolThreshold)
			{
				CoolingDegreeDays += (((OutdoorTemperature - cumulus.NOAAconf.CoolThreshold) * interval) / 1440);
			}
		}

		/// <summary>
		/// Wind run for today
		/// </summary>
		public double WindRunToday { get; set; } = 0;

		public double GetWindRunMonth(int year, int month)
		{
			var startDate = new DateTime(year, month, 1);
			var enddate = startDate.AddMonths(1);
			return DayFile.Where(r => r.Date >= startDate && r.Date < enddate).Sum(r => r.WindRun);
		}

		/// <summary>
		/// Extra Temps
		/// </summary>
		public double[] ExtraTemp { get; set; }

		/// <summary>
		/// User allocated Temps
		/// </summary>
		public double[] UserTemp { get; set; }

		/// <summary>
		/// Extra Humidity
		/// </summary>
		public double[] ExtraHum { get; set; }

		/// <summary>
		/// Extra dewpoint
		/// </summary>
		public double[] ExtraDewPoint { get; set; }

		/// <summary>
		/// Soil Temp 1-16 in C
		/// </summary>
		public double[] SoilTemp { get; set; }

		public double RainYesterday { get; set; }

		public double RainLastHour { get; set; }

		public int SoilMoisture1 { get; set; }

		public int SoilMoisture2 { get; set; }

		public int SoilMoisture3 { get; set; }

		public int SoilMoisture4 { get; set; }

		public int SoilMoisture5 { get; set; }

		public int SoilMoisture6 { get; set; }

		public int SoilMoisture7 { get; set; }

		public int SoilMoisture8 { get; set; }

		public int SoilMoisture9 { get; set; }

		public int SoilMoisture10 { get; set; }

		public int SoilMoisture11 { get; set; }

		public int SoilMoisture12 { get; set; }

		public int SoilMoisture13 { get; set; }

		public int SoilMoisture14 { get; set; }

		public int SoilMoisture15 { get; set; }

		public int SoilMoisture16 { get; set; }

		public double AirQuality1 { get; set; }
		public double AirQuality2 { get; set; }
		public double AirQuality3 { get; set; }
		public double AirQuality4 { get; set; }
		public double AirQualityAvg1 { get; set; }
		public double AirQualityAvg2 { get; set; }
		public double AirQualityAvg3 { get; set; }
		public double AirQualityAvg4 { get; set; }

		public int CO2 { get; set; }
		public int CO2_24h { get; set; }
		public double CO2_pm2p5 { get; set; }
		public double CO2_pm2p5_24h { get; set; }
		public double CO2_pm10 { get; set; }
		public double CO2_pm10_24h { get; set; }
		public double CO2_temperature { get; set; }
		public double CO2_humidity { get; set; }

		public int LeakSensor1 { get; set; }
		public int LeakSensor2 { get; set; }
		public int LeakSensor3 { get; set; }
		public int LeakSensor4 { get; set; }

		public double LightningDistance { get; set; }
		public DateTime LightningTime { get; set; }
		public int LightningStrikesToday { get; set; }

		public double LeafWetness1 { get; set; }
		public double LeafWetness2 { get; set; }
		public double LeafWetness3 { get; set; }
		public double LeafWetness4 { get; set; }
		public double LeafWetness5 { get; set; }
		public double LeafWetness6 { get; set; }
		public double LeafWetness7 { get; set; }
		public double LeafWetness8 { get; set; }

		public double SunshineHours { get; set; } = 0;

		public double YestSunshineHours { get; set; } = 0;

		public double SunshineToMidnight { get; set; }

		public double SunHourCounter { get; set; }

		public double StartOfDaySunHourCounter { get; set; }

		public int CurrentSolarMax { get; set; }

		public double RG11RainToday { get; set; }

		public double RainSinceMidnight { get; set; }

		/// <summary>
		/// Checks whether a new day has started and does a roll-over if necessary
		/// </summary>
		/// <param name="oadate"></param>
		/*
		public void CheckForRollover(int oadate)
		{
			if (oadate != LastDailySummaryOADate)
			{
				DoRollover();
			}
		}
		*/

		/*
		private void DoRollover()
		{
			//throw new NotImplementedException();
		}
		*/

		/// <summary>
		///
		/// </summary>
		/// <param name="later"></param>
		/// <param name="earlier"></param>
		/// <returns>Difference in minutes</returns>
		/*
		private int TimeDiff(DateTime later, DateTime earlier)
		{
			TimeSpan diff = later - earlier;

			return (int) Math.Round(diff.TotalMinutes);
		}
		*/

		public void StartMinuteTimer()
		{
			lastMinute = DateTime.Now.Minute;
			lastHour = DateTime.Now.Hour;
			secondTimer = new Timer(500);
			secondTimer.Elapsed += SecondTimer;
			secondTimer.Start();
		}

		public void StopMinuteTimer()
		{
			if (secondTimer != null) secondTimer.Stop();
		}

		public void SecondTimer(object sender, ElapsedEventArgs e)
		{
			var timeNow = DateTime.Now; // b3085 change to using a single fixed point in time to make it independent of how long the process takes

			if (timeNow.Minute != lastMinute)
			{
				lastMinute = timeNow.Minute;

				if ((timeNow.Minute % 10) == 0)
				{
					TenMinuteChanged(timeNow);
				}

				if (timeNow.Hour != lastHour)
				{
					lastHour = timeNow.Hour;
					HourChanged(timeNow);
					MinuteChanged(timeNow);

					// If it is rollover do the backup
					if (timeNow.Hour == Math.Abs(cumulus.GetHourInc()))
					{
						cumulus.BackupData(true, timeNow);
					}
				}
				else
				{
					MinuteChanged(timeNow);
				}

				if (DataStopped)
				{
					// check if we want to exit on data stopped
					if (cumulus.ProgramOptions.DataStoppedExit && DataStoppedTime.AddMinutes(cumulus.ProgramOptions.DataStoppedMins) < DateTime.Now)
					{
						cumulus.LogMessage($"*** Exiting Cumulus due to Data Stopped condition for > {cumulus.ProgramOptions.DataStoppedMins} minutes");
						Program.exitSystem = true;
					}
					// No data coming in, do not do anything else
					return;
				}
			}

			if (timeNow.Second != lastSecond)
			{
				lastSecond = timeNow.Second;

				// send current data to web-socket every 5 seconds, unless it has already been sent within the 10 seconds
				if (LastDataReadTimestamp.AddSeconds(5) < timeNow && (int) timeNow.TimeOfDay.TotalMilliseconds % 10000 <= 500)
				{
					_ = sendWebSocketData();
				}

				// lets spread some the processing over the minute, 12 seconds past the minute...
				var millisecs = (int) timeNow.TimeOfDay.TotalMilliseconds % 60000;
				if (millisecs >= 12000 && millisecs < 12500)
				{
					MinutePlus12Changed(timeNow);
				}

				cumulus.MQTTSecondChanged(timeNow);
			}
		}

		internal async Task sendWebSocketData(bool wait = false)
		{
			// Don't do anything if there are no clients connected
			if (cumulus.WebSock.ConnectedClients == 0)
			{
				if (webSocketSemaphore.CurrentCount == 0)
				{
					webSocketSemaphore.Release();
				}
				return;
			}

			// Return control to the calling method immediately.
			await Task.Yield();

			// send current data to web-socket
			try
			{
				// if we already have an update queued, don't add to the wait queue. Otherwise we get hundreds queued up during catch-up
				// Zero wait time for the ws lock object unless wait = true
				if (!webSocketSemaphore.Wait(wait ? 0 : 600))
				{
					cumulus.LogDebugMessage("sendWebSocketData: Update already running, skipping this one");
					return;
				}

				cumulus.WebSock.SendMessage(GetCurrentData());

				// We can't be sure when the broadcast completes because it is async internally, so the best we can do is wait a short time
				await Task.Delay(500);
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("sendWebSocketData: Error - " + ex.Message);
			}

			try
			{
				webSocketSemaphore.Release();
			}
			catch { }
		}

		private string getTimeString(DateTime time, string format = "HH:mm")
		{
			if (time <= DateTime.MinValue)
			{
				return "-----";
			}
			else
			{
				return time.ToString(format);
			}
		}

		private string getTimeString(TimeSpan timespan, string format = "HH:mm")
		{
			try
			{
				if (timespan.TotalSeconds < 0)
				{
					return "-----";
				}

				DateTime dt = DateTime.MinValue.Add(timespan);

				return getTimeString(dt, format);
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"getTimeString: Exception caught - {e.Message}");
				return "-----";
			}
		}

		private void RemoveOldRecentData(DateTime ts)
		{
			var deleteTime = ts.AddDays(-7);
			try
			{
				RecentDataDb.Execute("delete from RecentData where Timestamp < ?", deleteTime);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("RemoveOldRecentData: Failed to delete - " + ex.Message);
			}
		}

		private void ClearAlarms()
		{
			cumulus.DataStoppedAlarm.ClearAlarm();
			cumulus.BatteryLowAlarm.ClearAlarm();
			cumulus.NewRecordAlarm.ClearAlarm();
			cumulus.SensorAlarm.ClearAlarm();
			cumulus.SpikeAlarm.ClearAlarm();
			cumulus.UpgradeAlarm.ClearAlarm();
			cumulus.ThirdPartyAlarm.ClearAlarm();
			cumulus.MySqlUploadAlarm.ClearAlarm();
			cumulus.HighWindAlarm.ClearAlarm();
			cumulus.HighGustAlarm.ClearAlarm();
			cumulus.HighRainRateAlarm.ClearAlarm();
			cumulus.HighRainTodayAlarm.ClearAlarm();
			cumulus.IsRainingAlarm.ClearAlarm();
			cumulus.HighPressAlarm.ClearAlarm();
			cumulus.LowPressAlarm.ClearAlarm();
			cumulus.HighTempAlarm.ClearAlarm();
			cumulus.LowTempAlarm.ClearAlarm();
			cumulus.TempChangeAlarm.ClearAlarm();
			cumulus.PressChangeAlarm.ClearAlarm();
			cumulus.FtpAlarm.ClearAlarm();
		}

		private void CheckUserAlarms()
		{
			foreach (var alarm in cumulus.UserAlarms)
			{
				alarm.CheckAlarm();
			}
		}

		private void MinuteChanged(DateTime now)
		{
			CheckForDataStopped();

			CurrentSolarMax = AstroLib.SolarMax(now, (double) cumulus.Longitude, (double) cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.SolarOptions);

			if (!DataStopped)
			{
				if (((Pressure > 0) && TempReadyToPlot && WindReadyToPlot) || cumulus.StationOptions.NoSensorCheck)
				{
					// increment wind run by one minute's worth of average speed

					WindRunToday += WindAverage * WindRunHourMult[cumulus.Units.Wind] / 60.0;

					CheckForWindrunHighLow(now);

					CalculateDominantWindBearing(AvgBearing, WindAverage, 1);

					if (OutdoorTemperature < cumulus.ChillHourThreshold)
					{
						// add 1 minute to chill hours
						ChillHours += 1.0 / 60.0;
					}

					// update sunshine hours
					if (cumulus.SolarOptions.UseBlakeLarsen)
					{
						ReadBlakeLarsenData();
					}
					else if ((SolarRad > (CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100.0)) && (SolarRad >= cumulus.SolarOptions.SolarMinimum))
					{
						SunshineHours += 1.0 / 60.0;
					}

					// update heating/cooling degree days
					UpdateDegreeDays(1);

					weatherDataCollection.Add(new WeatherData
					{
						//station = this,
						DT = System.DateTime.Now,
						WindSpeed = WindLatest,
						WindAverage = WindAverage,
						OutdoorTemp = OutdoorTemperature,
						Pressure = Pressure,
						Raintotal = RainToday
					});

					while (weatherDataCollection[0].DT < now.AddHours(-1))
					{
						weatherDataCollection.RemoveAt(0);
					}

					if (!first_temp)
					{
						// update temperature average items
						tempsamplestoday++;
						TempTotalToday += OutdoorTemperature;
					}

					DoTrendValues(now);
					DoPressTrend("Enable Cumulus pressure trend");
					AddRecentDataWithAq(now, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity,
						Pressure, RainToday, SolarRad, UV, RainCounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);

					// calculate ET just before the hour so it is included in the correct day at roll over - only affects 9am met days really
					if (cumulus.StationOptions.CalculatedET && now.Minute == 59)
					{
						CalculateEvapotranspiration(now);
					}


					if (now.Minute % cumulus.logints[cumulus.DataLogInterval] == 0)
					{
						cumulus.DoLogFile(now, true);

						if (cumulus.StationOptions.LogExtraSensors)
						{
							cumulus.DoExtraLogFile(now);
						}

						if (cumulus.AirLinkInEnabled || cumulus.AirLinkOutEnabled)
						{
							cumulus.DoAirLinkLogFile(now);
						}
					}

					// Custom MySQL update - minutes interval
					if (cumulus.MySqlSettings.CustomMins.Enabled && now.Minute % cumulus.MySqlSettings.CustomMins.Interval == 0)
					{
						cumulus.CustomMysqlMinutesTimerTick();
					}

					// Custom MySQL Timed interval
					if (cumulus.MySqlSettings.CustomTimed.Enabled)
					{
						cumulus.CustomMySqlTimedUpdate(now);
					}

					// Custom HTTP update - minutes interval
					if (cumulus.CustomHttpMinutesEnabled && now.Minute % cumulus.CustomHttpMinutesInterval == 0)
					{
						cumulus.CustomHttpMinutesUpdate();
					}

					// Custom Log files - interval logs
					cumulus.DoCustomIntervalLogs(now);

					if (cumulus.WebIntervalEnabled && cumulus.SynchronisedWebUpdate && (now.Minute % cumulus.UpdateInterval == 0))
					{
						if (cumulus.WebUpdating == 1)
						{
							// Skip this update interval
							cumulus.LogMessage("Warning, previous web update is still in progress, first chance, skipping this interval");
							cumulus.WebUpdating++;
						}
						else if (cumulus.WebUpdating >= 2)
						{
							cumulus.LogMessage("Warning, previous web update is still in progress, second chance, aborting connection");
							if (cumulus.ftpThread.ThreadState == System.Threading.ThreadState.Running)
								cumulus.ftpThread.Abort();
							cumulus.LogMessage("Trying new web update");
							cumulus.WebUpdating = 1;
							cumulus.ftpThread = new Thread(() => cumulus.DoHTMLFiles());
							cumulus.ftpThread.IsBackground = true;
							cumulus.ftpThread.Start();
						}
						else
						{
							cumulus.WebUpdating = 1;
							cumulus.ftpThread = new Thread(() => cumulus.DoHTMLFiles());
							cumulus.ftpThread.IsBackground = true;
							cumulus.ftpThread.Start();
						}
					}
					// We also want to kick off DoHTMLFiles if local copy is enabled
					else if (cumulus.FtpOptions.LocalCopyEnabled && cumulus.SynchronisedWebUpdate && (now.Minute % cumulus.UpdateInterval == 0))
					{
						cumulus.ftpThread = new Thread(() => cumulus.DoHTMLFiles());
						cumulus.ftpThread.IsBackground = true;
						cumulus.ftpThread.Start();
					}

					if (cumulus.Wund.Enabled && (now.Minute % cumulus.Wund.Interval == 0) && cumulus.Wund.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.Wund.ID))
					{
						cumulus.UpdateWunderground(now);
					}

					if (cumulus.Windy.Enabled && (now.Minute % cumulus.Windy.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.Windy.ApiKey))
					{
						cumulus.UpdateWindy(now);
					}

					if (cumulus.WindGuru.Enabled && (now.Minute % cumulus.WindGuru.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.WindGuru.ID))
					{
						cumulus.UpdateWindGuru(now);
					}

					if (cumulus.AWEKAS.Enabled && (now.Minute % ((double) cumulus.AWEKAS.Interval / 60) == 0) && cumulus.AWEKAS.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.AWEKAS.ID))
					{
						cumulus.UpdateAwekas(now);
					}

					if (cumulus.WCloud.Enabled && (now.Minute % cumulus.WCloud.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.WCloud.ID))
					{
						cumulus.UpdateWCloud(now);
					}

					if (cumulus.OpenWeatherMap.Enabled && (now.Minute % cumulus.OpenWeatherMap.Interval == 0) && !string.IsNullOrWhiteSpace(cumulus.OpenWeatherMap.ID))
					{
						cumulus.UpdateOpenWeatherMap(now);
					}

					if (cumulus.PWS.Enabled && (now.Minute % cumulus.PWS.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.PWS.ID) && !String.IsNullOrWhiteSpace(cumulus.PWS.PW))
					{
						cumulus.UpdatePWSweather(now);
					}

					if (cumulus.WOW.Enabled && (now.Minute % cumulus.WOW.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.WOW.ID) && !String.IsNullOrWhiteSpace(cumulus.WOW.PW))
					{
						cumulus.UpdateWOW(now);
					}

					if (cumulus.APRS.Enabled && (now.Minute % cumulus.APRS.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.APRS.ID))
					{
						UpdateAPRS();
					}

					if (cumulus.xapEnabled)
					{
						using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
						{
							IPEndPoint iep1 = new IPEndPoint(IPAddress.Broadcast, cumulus.xapPort);

							byte[] data = Encoding.ASCII.GetBytes(cumulus.xapHeartbeat);

							sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

							sock.SendTo(data, iep1);

							var timeUTC = now.ToUniversalTime().ToString("HH:mm");
							var dateISO = now.ToUniversalTime().ToString("yyyyMMdd");

							var xapReport = new StringBuilder("", 1024);
							xapReport.Append("xap-header\n{\nv=12\nhop=1\n");
							xapReport.Append($"uid=FF{cumulus.xapUID}00\n");
							xapReport.Append("class=weather.report\n");
							xapReport.Append($"source={cumulus.xapsource}\n");
							xapReport.Append("}\n");
							xapReport.Append("weather.report\n{\n");
							xapReport.Append($"UTC={timeUTC}\nDATE={dateISO}\n");
							xapReport.Append($"WindM={ConvertUnits.UserWindToMPH(WindAverage):F1}\n");
							xapReport.Append($"WindK={ConvertUnits.UserWindToKPH(WindAverage):F1}\n");
							xapReport.Append($"WindGustsM={ConvertUnits.UserWindToMPH(RecentMaxGust):F1}\n");
							xapReport.Append($"WindGustsK={ConvertUnits.UserWindToKPH(RecentMaxGust):F1}\n");
							xapReport.Append($"WindDirD={Bearing}\n");
							xapReport.Append($"WindDirC={AvgBearing}\n");
							xapReport.Append($"TempC={ConvertUnits.UserTempToC(OutdoorTemperature):F1}\n");
							xapReport.Append($"TempF={ConvertUnits.UserTempToF(OutdoorTemperature):F1}\n");
							xapReport.Append($"DewC={ConvertUnits.UserTempToC(OutdoorDewpoint):F1}\n");
							xapReport.Append($"DewF={ConvertUnits.UserTempToF(OutdoorDewpoint):F1}\n");
							xapReport.Append($"AirPressure={ConvertUnits.UserPressToMB(Pressure):F1}\n");
							xapReport.Append($"Rain={ConvertUnits.UserRainToMM(RainToday):F1}\n");
							xapReport.Append('}');

							data = Encoding.ASCII.GetBytes(xapReport.ToString());

							sock.SendTo(data, iep1);

							sock.Close();
						}
					}

					var wxfile = cumulus.StdWebFiles.SingleOrDefault(item => item.LocalFileName == "wxnow.txt");
					if (wxfile.Create)
					{
						CreateWxnowFile();
					}

					cumulus.DoHttpFiles(now);
				}
				else
				{
					cumulus.LogErrorMessage("Minimum data set of pressure, temperature, and wind is not available and NoSensorCheck is not enabled. Skip processing");
				}
			}

			// Check for a new version of Cumulus once a day
			if (now.Minute == versionCheckTime.Minute && now.Hour == versionCheckTime.Hour)
			{
				cumulus.LogMessage("Checking for latest Cumulus MX version...");
				cumulus.GetLatestVersion();
			}
		}

		private void MinutePlus12Changed(DateTime now)
		{
			if (!DataStopped)
			{
				CheckUserAlarms();
			}

			// If not on windows, check for CPU temp
			try
			{
				if (Type.GetType("Mono.Runtime") != null && File.Exists("/sys/class/thermal/thermal_zone0/temp"))
				{
					var raw = File.ReadAllText(@"/sys/class/thermal/thermal_zone0/temp");
					if (double.TryParse(raw, out var val))
					{
						cumulus.CPUtemp = ConvertUnits.TempCToUser(val / 1000);
						cumulus.LogDebugMessage($"Current CPU temp = {cumulus.CPUtemp.ToString(cumulus.TempFormat)}{cumulus.Units.TempText}");
					}
					else
					{
						cumulus.LogDebugMessage($"Current CPU temp file /sys/class/thermal/thermal_zone0/temp does not contain a number = [{raw}]");
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage($"Error reading CPU temperature - {ex.Message}");
			}
		}

		private void TenMinuteChanged(DateTime now)
		{
			cumulus.DoMoonPhase();
			cumulus.MoonAge = MoonriseMoonset.MoonAge();

			cumulus.RotateLogFiles();

			ClearAlarms();
		}

		private void HourChanged(DateTime now)
		{
			cumulus.LogMessage("Hour changed: " + now.Hour);
			cumulus.DoSunriseAndSunset();
			cumulus.DoMoonImage();

			if (cumulus.HourlyForecast)
			{
				DoForecast("", true);
			}

			int rollHour = Math.Abs(cumulus.GetHourInc());

			if (now.Hour == rollHour)
			{
				DayReset(now);
			}

			if (now.Hour == 0)
			{
				ResetMidnightRain(now);
				//RecalcSolarFactor(now);
				ResetSunshineHours(now);
				ResetMidnightTemperatures(now);
			}

			RemoveOldRecentData(now);
		}

		private void CheckForDataStopped()
		{
			// Check whether we have read data since the last clock minute.
			if ((LastDataReadTimestamp != DateTime.MinValue) && (LastDataReadTimestamp == SavedLastDataReadTimestamp) && (LastDataReadTimestamp < DateTime.Now) && (DateTime.Now.Subtract(LastDataReadTimestamp) > TimeSpan.FromMinutes(DataTimeoutMins)))
			{
				// Data input appears to have has stopped
				if (!DataStopped)
				{
					DataStoppedTime = DateTime.Now;
				}
				DataStopped = true;
				cumulus.DataStoppedAlarm.Triggered = true;
				/*if (RestartIfDataStops)
				{
					cumulus.LogMessage("*** Data input appears to have stopped, restarting");
					ApplicationExec(ParamStr(0), '', SW_SHOW);
					TerminateProcess(GetCurrentProcess, 0);
				}*/
				if (cumulus.ReportDataStoppedErrors)
				{
					cumulus.LogErrorMessage("*** Data input appears to have stopped");
				}
			}
			else
			{
				DataStopped = false;
				cumulus.DataStoppedAlarm.Triggered = false;
			}

			// save the time that data was last read so we can check in a minute's time that it's changed
			SavedLastDataReadTimestamp = LastDataReadTimestamp;
		}

		private void ReadBlakeLarsenData()
		{
			var blFile = cumulus.AppDir + "SRsunshine.dat";

			if (File.Exists(blFile))
			{
				using (var sr = new StreamReader(blFile))
				{
					try
					{
						string line = sr.ReadLine();
						SunshineHours = double.Parse(line, CultureInfo.InvariantCulture.NumberFormat);
						sr.ReadLine();
						sr.ReadLine();
						line = sr.ReadLine();
						IsSunny = (line == "True");
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("Error reading SRsunshine.dat: " + ex.Message);
					}
				}
			}
		}

		/*
		internal void UpdateDatabase(DateTime timestamp, int interval, bool updateHighsAndLows)
		// Add an entry to the database
		{
			double raininterval;

			if (prevraincounter == 0)
			{
				raininterval = 0;
			}
			else
			{
				raininterval = Raincounter - prevraincounter;
			}

			//using (cumulusEntities dataContext = new cumulusEntities())
			//{
			//    dataContext.AddToStandardData(newdata);

			//    // Submit the change to the database.
			//    try
			//    {
			//        dataContext.SaveChanges();
			//    }
			//    catch (Exception ex)
			//    {
			//        Trace.WriteLine(ex.ToString());
			//        Trace.Flush();
			//    }

			// reset highs and lows since last update
			loOutdoorTemperature = OutdoorTemperature;
			hiOutdoorTemperature = OutdoorTemperature;
			loIndoorTemperature = IndoorTemperature;
			hiIndoorTemperature = IndoorTemperature;
			loIndoorHumidity = IndoorHumidity;
			hiIndoorHumidity = IndoorHumidity;
			loOutdoorHumidity = OutdoorHumidity;
			hiOutdoorHumidity = OutdoorHumidity;
			loPressure = Pressure;
			hiPressure = Pressure;
			hiWind = WindAverage;
			hiGust = WindLatest;
			hiWindBearing = Bearing;
			hiGustBearing = Bearing;
			prevraincounter = Raincounter;
			hiRainRate = RainRate;
			hiDewPoint = OutdoorDewpoint;
			loDewPoint = OutdoorDewpoint;
			hiHeatIndex = HeatIndex;
			hiHumidex = Humidex;
			loWindChill = WindChill;
			hiApparentTemperature = ApparentTemperature;
			loApparentTemperature = ApparentTemperature;
		}
		*/


		public void CalculateEvapotranspiration(DateTime date)
		{
			cumulus.LogDebugMessage("Calculating ET from data");

			var dateFrom = date.AddHours(-1);

			// get the min and max temps, humidity, pressure, and mean solar rad and wind speed for the last hour
			var result = RecentDataDb.Query<EtData>("select avg(OutsideTemp) avgTemp, avg(Humidity) avgHum, avg(Pressure) avgPress, avg(SolarRad) avgSol, avg(SolarMax) avgSolMax, avg(WindSpeed) avgWind from RecentData where Timestamp >= ? order by Timestamp", dateFrom);

			// finally calculate the ETo
			var newET = MeteoLib.Evapotranspiration(
				ConvertUnits.UserTempToC(result[0].avgTemp),
				result[0].avgHum,
				result[0].avgSol,
				result[0].avgSolMax,
				ConvertUnits.UserWindToMS(result[0].avgWind),
				ConvertUnits.UserPressureToHPa(result[0].avgPress) / 10
			);

			// convert to user units
			newET = ConvertUnits.RainMMToUser(newET);
			cumulus.LogDebugMessage($"Calculated ET for the last hour = {newET:F3}");

			// DoET expects the running annual total to be sent
			DoET(AnnualETTotal + newET, date);
		}

		public void CreateGraphDataFiles()
		{
			// Chart data for Highcharts graphs
			string json;
			for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
			{
				if (cumulus.GraphDataFiles[i].Create && cumulus.GraphDataFiles[i].CreateRequired)
				{
					json = CreateGraphDataJson(cumulus.GraphDataFiles[i].LocalFileName, false);

					try
					{
						var dest = cumulus.GraphDataFiles[i].LocalPath + cumulus.GraphDataFiles[i].LocalFileName;
						using (var file = new StreamWriter(dest, false))
						{
							file.WriteLine(json);
							file.Close();
						}

						// The config files only need creating once per change
						// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
						if (i == 0 || i == 1 || i == 8 || i == 9 || i == 11)
						{
							cumulus.GraphDataFiles[i].CreateRequired = false;
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"Error writing {cumulus.GraphDataFiles[i].LocalFileName}: {ex}");
					}
				}
			}
		}

		public string CreateGraphDataJson(string filename, bool incremental)
		{
			// Chart data for Highcharts graphs

			switch (filename)
			{
				case "graphconfig.json":
					return GetGraphConfig(false);
				case "availabledata.json":
					return GetAvailGraphData(false);
				case "tempdata.json":
					return GetTempGraphData(incremental, false);
				case "pressdata.json":
					return GetPressGraphData(incremental);
				case "winddata.json":
					return GetWindGraphData(incremental);
				case "wdirdata.json":
					return GetWindDirGraphData(incremental);
				case "humdata.json":
					return GetHumGraphData(incremental, false);
				case "raindata.json":
					return GetRainGraphData(incremental);
				case "dailyrain.json":
					return GetDailyRainGraphData();
				case "dailytemp.json":
					return GetDailyTempGraphData(false);
				case "solardata.json":
					return GetSolarGraphData(incremental, false);
				case "sunhours.json":
					return GetSunHoursGraphData(false);
				case "airquality.json":
					return GetAqGraphData(incremental);
				case "extratempdata.json":
					return GetExtraTempGraphData(incremental, false);
				case "extrahumdata.json":
					return GetExtraHumGraphData(incremental, false);
				case "extradewdata.json":
					return GetExtraDewPointGraphData(incremental, false);
				case "soiltempdata.json":
					return GetSoilTempGraphData(incremental, false);
				case "soilmoistdata.json":
					return GetSoilMoistGraphData(incremental, false);
				case "leafwetdata.json":
					return GetLeafWetnessGraphData(incremental, false);
				case "usertempdata.json":
					return GetUserTempGraphData(incremental, false);
				case "co2sensordata.json":
					return GetCo2SensorGraphData(incremental, false);
			}
			return "{}";
		}


		public void CreateEodGraphDataFiles()
		{
			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				if (cumulus.GraphDataEodFiles[i].Create)
				{
					var json = CreateEodGraphDataJson(cumulus.GraphDataEodFiles[i].LocalFileName);

					try
					{
						var dest = cumulus.GraphDataEodFiles[i].LocalPath + cumulus.GraphDataEodFiles[i].LocalFileName;
						File.WriteAllText(dest, json);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"Error writing {cumulus.GraphDataEodFiles[i].LocalFileName}: {ex}");
					}
				}

				// Now set the flag that upload is required (if enabled)
				cumulus.GraphDataEodFiles[i].FtpRequired = true;
				cumulus.GraphDataEodFiles[i].CopyRequired = true;
			}
		}

		public void CreateDailyGraphDataFiles()
		{
			// skip 0 & 1 = config files
			// daily rain = 8
			// daily temp = 9
			// sun hours = 11
			var eod = new int[] { 8, 9, 11 };

			foreach (var i in eod)
			{
				if (cumulus.GraphDataFiles[i].Create)
				{
					var json = CreateGraphDataJson(cumulus.GraphDataFiles[i].LocalFileName, false);

					try
					{
						var dest = cumulus.GraphDataFiles[i].LocalPath + cumulus.GraphDataFiles[i].LocalFileName;
						File.WriteAllText(dest, json);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"Error writing {cumulus.GraphDataFiles[i].LocalFileName}: {ex}");
					}
				}

				cumulus.GraphDataFiles[i].CopyRequired = true;
				cumulus.GraphDataFiles[i].FtpRequired = true;
			}
		}

		public string CreateEodGraphDataJson(string filename)
		{
			switch (filename)
			{
				case "alldailytempdata.json":
					return GetAllDailyTempGraphData(false);
				case "alldailypressdata.json":
					return GetAllDailyPressGraphData();
				case "alldailywinddata.json":
					return GetAllDailyWindGraphData();
				case "alldailyhumdata.json":
					return GetAllDailyHumGraphData();
				case "alldailyraindata.json":
					return GetAllDailyRainGraphData();
				case "alldailysolardata.json":
					return GetAllDailySolarGraphData(false);
				case "alldailydegdaydata.json":
					return GetAllDegreeDaysGraphData(false);
				case "alltempsumdata.json":
					return GetAllTempSumGraphData(false);
			}
			return "{}";
		}


		public string GetSolarGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[10].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);

				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				{
					sbUv.Append($"[{jsTime},{data[i].UV.ToString(cumulus.UVFormat, InvC)}],");
				}

				if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
				{
					sbSol.Append($"[{jsTime},{(int) data[i].SolarRad}],");

					sbMax.Append($"[{jsTime},{(int) data[i].SolarMax}],");
				}
			}


			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (sbUv[sbUv.Length - 1] == ',')
					sbUv.Length--;

				sbUv.Append(']');
				sb.Append(sbUv);
			}
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (sbSol[sbSol.Length - 1] == ',')
				{
					sbSol.Length--;
					sbMax.Length--;
				}

				sbSol.Append(']');
				sbMax.Append(']');
				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				{
					sb.Append(',');
				}
				sb.Append(sbSol);
				sb.Append(',');
				sb.Append(sbMax);
			}

			sb.Append('}');
			return sb.ToString();
		}


		public string GetRainGraphData(bool incremental, DateTime? start = null)
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[7].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);

				sbRain.Append($"[{jsTime},{data[i].RainToday.ToString(cumulus.RainFormat, InvC)}],");

				sbRate.Append($"[{jsTime},{data[i].RainRate.ToString(cumulus.RainFormat, InvC)}],");
			}

			if (sbRain[sbRain.Length - 1] == ',')
			{
				sbRain.Length--;
				sbRate.Length--;
			}
			sbRain.Append("],");
			sbRate.Append(']');
			sb.Append(sbRain);
			sb.Append(sbRate);
			sb.Append('}');
			return sb.ToString();
		}

		public string GetHumGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var sb = new StringBuilder("{", 10240);
			var sbOut = new StringBuilder("\"hum\":[");
			var sbIn = new StringBuilder("\"inhum\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[6].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);

				if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				{
					sbOut.Append($"[{jsTime},{data[i].Humidity}],");
				}
				if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
				{
					sbIn.Append($"[{jsTime},{data[i].IndoorHumidity}],");
				}
			}

			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
			{
				if (sbOut[sbOut.Length - 1] == ',')
					sbOut.Length--;

				sbOut.Append(']');

				sb.Append(sbOut);
			}

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
			{
				if (sbIn[sbIn.Length - 1] == ',')
					sbIn.Length--;

				sbIn.Append(']');

				if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
					sb.Append(',');

				sb.Append(sbIn);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetWindDirGraphData(bool incremental, DateTime? start = null)
		{
			var sb = new StringBuilder("{\"bearing\":[");
			var sbAvg = new StringBuilder("\"avgbearing\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[5].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);

				sb.Append($"[{jsTime},{data[i].WindDir}],");

				sbAvg.Append($"[{jsTime},{data[i].WindAvgDir}],");
			}

			if (sb[sb.Length - 1] == ',')
			{
				sb.Length--;
				sbAvg.Length--;
				sbAvg.Append(']');
			}

			sb.Append("],");
			sb.Append(sbAvg);
			sb.Append('}');
			return sb.ToString();
		}

		public string GetWindGraphData(bool incremental, DateTime? start = null)
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[4].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);
				sb.Append($"[{jsTime},{data[i].WindGust.ToString(cumulus.WindFormat, InvC)}],");

				sbSpd.Append($"[{jsTime},{data[i].WindSpeed.ToString(cumulus.WindAvgFormat, InvC)}],");
			}

			if (sb[sb.Length - 1] == ',')
			{
				sb.Length--;
				sbSpd.Length--;
				sbSpd.Append(']');
			}

			sb.Append("],");
			sb.Append(sbSpd);
			sb.Append('}');
			return sb.ToString();
		}

		public string GetPressGraphData(bool incremental, DateTime? start = null)
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"press\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[3].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{Utils.ToPseudoJSTime(data[i].Timestamp)},{data[i].Pressure.ToString(cumulus.PressFormat, InvC)}],");
			}

			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetTempGraphData(bool incremental, bool local, DateTime? start = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);
			var sbIn = new StringBuilder("\"intemp\":[");
			var sbDew = new StringBuilder("\"dew\":[");
			var sbApp = new StringBuilder("\"apptemp\":[");
			var sbFeel = new StringBuilder("\"feelslike\":[");
			var sbChill = new StringBuilder("\"wchill\":[");
			var sbHeat = new StringBuilder("\"heatindex\":[");
			var sbTemp = new StringBuilder("\"temp\":[");
			var sbHumidex = new StringBuilder("\"humidex\":[");

			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[2].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);

				if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
					sbIn.Append($"[{jsTime},{data[i].IndoorTemp.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
					sbDew.Append($"[{jsTime},{data[i].DewPoint.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
					sbApp.Append($"[{jsTime},{data[i].AppTemp.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
					sbFeel.Append($"[{jsTime},{data[i].FeelsLike.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
					sbChill.Append($"[{jsTime},{data[i].WindChill.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
					sbHeat.Append($"[{jsTime},{data[i].HeatIndex.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
					sbTemp.Append($"[{jsTime},{data[i].OutsideTemp.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
					sbHumidex.Append($"[{jsTime},{data[i].Humidex.ToString(cumulus.TempFormat, InvC)}],");
			}

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
			{
				if (sbIn[sbIn.Length - 1] == ',')
					sbIn.Length--;

				sbIn.Append(']');
				sb.Append(sbIn);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				if (sbDew[sbDew.Length - 1] == ',')
					sbDew.Length--;

				sbDew.Append(']');
				sb.Append((append ? "," : "") + sbDew);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				if (sbApp[sbApp.Length - 1] == ',')
					sbApp.Length--;

				sbApp.Append(']');
				sb.Append((append ? "," : "") + sbApp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				if (sbFeel[sbFeel.Length - 1] == ',')
					sbFeel.Length--;

				sbFeel.Append(']');
				sb.Append((append ? "," : "") + sbFeel);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				if (sbChill[sbChill.Length - 1] == ',')
					sbChill.Length--;

				sbChill.Append(']');
				sb.Append((append ? "," : "") + sbChill);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				if (sbHeat[sbHeat.Length - 1] == ',')
					sbHeat.Length--;

				sbHeat.Append(']');
				sb.Append((append ? "," : "") + sbHeat);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
			{
				if (sbTemp[sbTemp.Length - 1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				if (sbHumidex[sbHumidex.Length - 1] == ',')
					sbHumidex.Length--;

				sbHumidex.Append(']');
				sb.Append((append ? "," : "") + sbHumidex);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetAqGraphData(bool incremental, DateTime? start = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sb2p5 = new StringBuilder("\"pm2p5\":[");
			var sb10 = new StringBuilder(",\"pm10\":[");


			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (cumulus.StationOptions.PrimaryAqSensor > (int) Cumulus.PrimaryAqSensor.Undefined
				&& cumulus.StationOptions.PrimaryAqSensor != (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				DateTime dateFrom;
				if (incremental)
				{
					dateFrom = start ?? cumulus.GraphDataFiles[12].LastDataTime;
				}
				else
				{
					dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				}

				var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom);

				for (var i = 0; i < data.Count; i++)
				{
					var jsTime = Utils.ToPseudoJSTime(data[i].Timestamp);
					var val = data[i].Pm2p5 == -1 ? "null" : data[i].Pm2p5.ToString("F1", InvC);
					sb2p5.Append($"[{jsTime},{val}],");

					// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
					if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
						cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
						cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
					{
						append = true;
						val = data[i].Pm10 == -1 ? "null" : data[i].Pm10.ToString("F1", InvC);
						sb10.Append($"[{jsTime},{val}],");
					}
				}

				if (sb2p5[sb2p5.Length - 1] == ',')
					sb2p5.Length--;

				sb2p5.Append(']');
				sb.Append(sb2p5);

				if (append)
				{
					if (sb10[sb10.Length - 1] == ',')
						sb10.Length--;

					sb10.Append(']');
					sb.Append(sb10);
				}

			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraTempCaptions[i]}\":[");
			}

			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[13].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(fileDate);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var temp = 0.0;
									var jsTime = Utils.ToPseudoJSTime(entrydate);
									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local) && double.TryParse(st[i + 2], out temp))
											sbExt[i].Append($"[{jsTime},{temp.ToString(cumulus.TempFormat, InvC)}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetExtraTempGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetExtraTempGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetExtraTempGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraDewPointGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraDPCaptions[i]}\":[");
			}

			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[15].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(fileDate);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var jsTime = Utils.ToPseudoJSTime(entrydate);
									var temp = 0.0;
									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local) && double.TryParse(st[i + 22], out temp))
											sbExt[i].Append($"[{jsTime},{temp.ToString(cumulus.TempFormat, InvC)}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetExtraDewpointGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetExtraDewpointGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetExtraDewpointGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraHumGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraHum.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraHumCaptions[i]}\":[");
			}


			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[14].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var temp = 0;
									var jsTime = Utils.ToPseudoJSTime(entrydate);
									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local) && int.TryParse(st[i + 12], out temp))
											sbExt[i].Append($"[{jsTime},{temp}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetExtraHumGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetExtraHumGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetExtraHumGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilTempCaptions[i]}\":[");
			}

			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[16].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var temp = 0.0;
									var jsTime = Utils.ToPseudoJSTime(entrydate);

									for (var i = 0; i < 4; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local) && double.TryParse(st[i + 32], out temp))
											sbExt[i].Append($"[{jsTime},{temp.ToString(cumulus.TempFormat, InvC)}],");
									}
									for (var i = 4; i < 16; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local) && double.TryParse(st[i + 40], out temp))
											sbExt[i].Append($"[{jsTime},{temp.ToString(cumulus.TempFormat, InvC)}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSoilTempGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSoilTempGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSoilTempGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilMoistGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilMoist.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilMoistureCaptions[i]}\":[");
			}

			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[17].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var temp = 0;
									var jsTime = Utils.ToPseudoJSTime(entrydate);

									for (var i = 0; i < 4; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local) && int.TryParse(st[i + 36], out temp))
											sbExt[i].Append($"[{jsTime},{temp}],");
									}
									for (var i = 4; i < 16; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local) && int.TryParse(st[i + 52], out temp))
											sbExt[i].Append($"[{jsTime},{temp}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSoilMoistGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSoilMoistGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSoilMoistGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetLeafWetnessGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.LeafWetness.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.LeafWetness.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.LeafWetnessCaptions[i]}\":[");
			}

			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[20].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var temp = 0.0;
									var jsTime = Utils.ToPseudoJSTime(entrydate);

									for (var i = 0; i < 2; i++)
									{
										if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local) && double.TryParse(st[i + 42], out temp))
											sbExt[i].Append($"[{jsTime},{temp}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetLeafWetnessGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetLeafWetnessGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetLeafWetnessGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < 2; i++)
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetUserTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],

			}
			*/

			StringBuilder[] sbExt = new StringBuilder[cumulus.GraphOptions.Visible.UserTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.UserTempCaptions[i]}\":[");
			}

			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[18].LastDataTime;
			}
			else if (start.HasValue && end.HasValue)
			{
				dateFrom = start.Value;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = end ?? DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									// entry is from required period
									var temp = 0.0;
									var jsTime = Utils.ToPseudoJSTime(entrydate);

									for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local) && double.TryParse(st[i + 76], out temp))
											sbExt[i].Append($"[{jsTime},{temp.ToString(cumulus.TempFormat, InvC)}],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetUserTempGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetUserTempGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetUserTempGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
				{
					if (sbExt[i][sbExt[i].Length - 1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append("]");
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetCo2SensorGraphData(bool incremental, bool local, DateTime? start = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
			{
				"CO2": [[time,val],[time,val],...],
				"CO2 Average": [[time,val],[time,val],...],

			}
			*/

			var sbCo2 = new StringBuilder($"\"CO2\":[");
			var sbCo2Avg = new StringBuilder($"\"CO2 Average\":[");
			var sbPm25 = new StringBuilder($"\"PM2.5\":[");
			var sbPm25Avg = new StringBuilder($"\"PM 2.5 Average\":[");
			var sbPm10 = new StringBuilder($"\"PM 10\":[");
			var sbPm10Avg = new StringBuilder($"\"PM 10 Average\":[");
			var sbTemp = new StringBuilder($"\"Temperature\":[");
			var sbHum = new StringBuilder($"\"Humidity\":[");


			var finished = false;
			var entrydate = new DateTime();
			DateTime dateFrom;
			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[19].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var dateto = DateTime.Now.AddMinutes(-(cumulus.logints[cumulus.DataLogInterval] + 1));
			var fileDate = dateFrom;

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);


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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;
					double temp;
					int tempInt;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate > dateFrom)
								{
									var jsTime = Utils.ToPseudoJSTime(entrydate);

									if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local) && double.TryParse(st[84], out temp))
										sbCo2.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local) && double.TryParse(st[85], out temp))
										sbCo2Avg.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local) && double.TryParse(st[86], out temp))
										sbPm25.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local) && double.TryParse(st[87], out temp))
										sbPm25Avg.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local) && double.TryParse(st[88], out temp))
										sbPm10.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local) && double.TryParse(st[89], out temp))
										sbPm10Avg.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local) && double.TryParse(st[90], out temp))
										sbTemp.Append($"[{jsTime},{temp.ToString(cumulus.TempFormat, InvC)}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local) && int.TryParse(st[91], out tempInt))
										sbHum.Append($"[{jsTime},{tempInt}],");
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetCo2SensorGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetCo2SensorGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetCo2SensorGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (entrydate >= dateto || fileDate > dateto)
				{
					finished = true;
				}
				else
				{
					fileDate = fileDate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(fileDate);
				}
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
			{
				if (sbCo2[sbCo2.Length - 1] == ',')
					sbCo2.Length--;

				sbCo2.Append("]");
				sb.Append(sbCo2);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
			{
				if (sbCo2Avg[sbCo2Avg.Length - 1] == ',')
					sbCo2Avg.Length--;

				sbCo2Avg.Append("]");
				sb.Append((append ? "," : "") + sbCo2Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
			{
				if (sbPm25[sbPm25.Length - 1] == ',')
					sbPm25.Length--;

				sbPm25.Append("]");
				sb.Append((append ? "," : "") + sbPm25);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
			{
				if (sbPm25Avg[sbPm25Avg.Length - 1] == ',')
					sbPm25Avg.Length--;

				sbPm25Avg.Append("]");
				sb.Append((append ? "," : "") + sbPm25Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
			{
				if (sbPm10[sbPm10.Length - 1] == ',')
					sbPm10.Length--;

				sbPm10.Append("]");
				sb.Append((append ? "," : "") + sbPm10);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
			{
				if (sbPm10Avg[sbPm10Avg.Length - 1] == ',')
					sbPm10Avg.Length--;

				sbPm10Avg.Append("]");
				sb.Append((append ? "," : "") + sbPm10Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
			{
				if (sbTemp[sbTemp.Length - 1] == ',')
					sbTemp.Length--;

				sbTemp.Append("]");
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
			{
				if (sbHum[sbHum.Length - 1] == ',')
					sbHum.Length--;

				sbHum.Append("]");
				sb.Append((append ? "," : "") + sbHum);
			}

			sb.Append('}');
			return sb.ToString();
		}


		public string GetIntervalTempGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{", 10240);
			var sbIn = new StringBuilder("\"intemp\":[");
			var sbDew = new StringBuilder("\"dew\":[");
			var sbApp = new StringBuilder("\"apptemp\":[");
			var sbFeel = new StringBuilder("\"feelslike\":[");
			var sbChill = new StringBuilder("\"wchill\":[");
			var sbHeat = new StringBuilder("\"heatindex\":[");
			var sbTemp = new StringBuilder("\"temp\":[");
			var sbHumidex = new StringBuilder("\"humidex\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalTempGraphData: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = ParseLogFileRec(line, true);

							if (rec.Date < dateFrom)
								continue;

							if (rec.Date > dateTo)
							{
								finished = true;
								cumulus.LogDebugMessage("GetIntervalTempGraphData: Finished processing the log files");
								break;
							}

							var jsTime = Utils.ToPseudoJSTime(rec.Date);

							if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
								sbIn.Append($"[{jsTime},{rec.IndoorTemperature.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
								sbDew.Append($"[{jsTime},{rec.OutdoorDewpoint.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
								sbApp.Append($"[{jsTime},{rec.ApparentTemperature.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
								sbFeel.Append($"[{jsTime},{rec.FeelsLike.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
								sbChill.Append($"[{jsTime},{rec.WindChill.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
								sbHeat.Append($"[{jsTime},{rec.HeatIndex.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
								sbTemp.Append($"[{jsTime},{rec.OutdoorTemperature.ToString(cumulus.TempFormat, InvC)}],");

							if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
								sbHumidex.Append($"[{jsTime},{rec.Humidex.ToString(cumulus.TempFormat, InvC)}],");
						}

					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalTempGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalTempGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalTempGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					fileDate = fileDate.AddMonths(1);

					if (fileDate.Year > dateTo.Year || (fileDate.Year == dateTo.Year && fileDate.Month > dateTo.Month))
						finished = true;

					logFile = cumulus.GetLogFileName(fileDate);
				}
			}

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
			{
				if (sbIn[sbIn.Length - 1] == ',')
					sbIn.Length--;

				sbIn.Append(']');
				sb.Append(sbIn);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				if (sbDew[sbDew.Length - 1] == ',')
					sbDew.Length--;

				sbDew.Append(']');
				sb.Append((append ? "," : "") + sbDew);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				if (sbApp[sbApp.Length - 1] == ',')
					sbApp.Length--;

				sbApp.Append(']');
				sb.Append((append ? "," : "") + sbApp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				if (sbFeel[sbFeel.Length - 1] == ',')
					sbFeel.Length--;

				sbFeel.Append(']');
				sb.Append((append ? "," : "") + sbFeel);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				if (sbChill[sbChill.Length - 1] == ',')
					sbChill.Length--;

				sbChill.Append(']');
				sb.Append((append ? "," : "") + sbChill);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				if (sbHeat[sbHeat.Length - 1] == ',')
					sbHeat.Length--;

				sbHeat.Append(']');
				sb.Append((append ? "," : "") + sbHeat);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
			{
				if (sbTemp[sbTemp.Length - 1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				if (sbHumidex[sbHumidex.Length - 1] == ',')
					sbHumidex.Length--;

				sbHumidex.Append(']');
				sb.Append((append ? "," : "") + sbHumidex);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetIntervalHumGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var sb = new StringBuilder("{", 10240);
			var sbOut = new StringBuilder("\"hum\":[");
			var sbIn = new StringBuilder("\"inhum\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;


			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalHumGraphData: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = ParseLogFileRec(line, true);

							if (rec.Date < dateFrom)
								continue;

							if (rec.Date > dateTo)
							{
								finished = true;
								cumulus.LogDebugMessage("GetIntervalHumGraphData: Finished processing the log files");
								break;
							}

							var jsTime = Utils.ToPseudoJSTime(rec.Date);

							if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
							{
								sbOut.Append($"[{jsTime},{rec.OutdoorHumidity}],");
							}
							if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
							{
								sbIn.Append($"[{jsTime},{rec.IndoorHumidity}],");
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalHumGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalHumGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalHumGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					fileDate = fileDate.AddMonths(1);

					if (fileDate.Year > dateTo.Year || (fileDate.Year == dateTo.Year && fileDate.Month > dateTo.Month))
						finished = true;

					logFile = cumulus.GetLogFileName(fileDate);
				}
			}


			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
			{
				if (sbOut[sbOut.Length - 1] == ',')
					sbOut.Length--;

				sbOut.Append(']');

				sb.Append(sbOut);
			}

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
			{
				if (sbIn[sbIn.Length - 1] == ',')
					sbIn.Length--;

				sbIn.Append(']');

				if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
					sb.Append(',');

				sb.Append(sbIn);
			}

			sb.Append('}');
			return sb.ToString();

		}

		public string GetIntervalSolarGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;


			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = ParseLogFileRec(line, true);

							if (rec.Date < dateFrom)
								continue;

							if (rec.Date > dateTo)
							{
								finished = true;
								cumulus.LogDebugMessage("GetIntervalSolarGraphData: Finished processing the log files");
								break;
							}

							var jsTime = Utils.ToPseudoJSTime(rec.Date);

							if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
							{
								sbUv.Append($"[{jsTime},{rec.UV.ToString(cumulus.UVFormat, InvC)}],");
							}

							if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
							{
								sbSol.Append($"[{jsTime},{(int) rec.SolarRad}],");

								sbMax.Append($"[{jsTime},{(int) rec.CurrentSolarMax}],");
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalSolarGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					fileDate = fileDate.AddMonths(1);

					if (fileDate.Year > dateTo.Year || (fileDate.Year == dateTo.Year && fileDate.Month > dateTo.Month))
						finished = true;

					logFile = cumulus.GetLogFileName(fileDate);
				}
			}

			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (sbUv[sbUv.Length - 1] == ',')
					sbUv.Length--;

				sbUv.Append(']');
				sb.Append(sbUv);
			}
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (sbSol[sbSol.Length - 1] == ',')
				{
					sbSol.Length--;
					sbMax.Length--;
				}

				sbSol.Append(']');
				sbMax.Append(']');
				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				{
					sb.Append(',');
				}
				sb.Append(sbSol);
				sb.Append(',');
				sb.Append(sbMax);
			}

			sb.Append('}');
			return sb.ToString();

		}

		public string GetIntervalPressGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"press\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;


			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervaPressGraphData: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = ParseLogFileRec(line, true);

							if (rec.Date < dateFrom)
								continue;

							if (rec.Date > dateTo)
							{
								finished = true;
								cumulus.LogDebugMessage("GetIntervaPressGraphData: Finished processing the log files");
								break;
							}

							sb.Append($"[{Utils.ToPseudoJSTime(rec.Date)},{rec.Pressure.ToString(cumulus.PressFormat, InvC)}],");
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervaPressGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervaPressGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervaPressGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					fileDate = fileDate.AddMonths(1);

					if (fileDate.Year > dateTo.Year || (fileDate.Year == dateTo.Year && fileDate.Month > dateTo.Month))
						finished = true;

					logFile = cumulus.GetLogFileName(fileDate);
				}
			}

			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();

		}

		public string GetIntervalWindGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");
			var sbBrg = new StringBuilder("\"bearing\":[");
			var sbAvgBrg = new StringBuilder("\"avgbearing\":[");


			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;


			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalWindGraphData: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = ParseLogFileRec(line, true);

							if (rec.Date < dateFrom)
								continue;

							if (rec.Date > dateTo)
							{
								finished = true;
								cumulus.LogDebugMessage("GetIntervalWindGraphData: Finished processing the log files");
								break;
							}

							var jsTime = Utils.ToPseudoJSTime(rec.Date);

							sb.Append($"[{jsTime},{rec.RecentMaxGust.ToString(cumulus.WindFormat, InvC)}],");

							sbSpd.Append($"[{jsTime},{rec.WindAverage.ToString(cumulus.WindAvgFormat, InvC)}],");

							sbBrg.Append($"[{jsTime},{rec.Bearing}],");

							sbAvgBrg.Append($"[{jsTime},{rec.AvgBearing}],");
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalWindGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalWindGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalWindGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					fileDate = fileDate.AddMonths(1);

					if (fileDate.Year > dateTo.Year || (fileDate.Year == dateTo.Year && fileDate.Month > dateTo.Month))
						finished = true;

					logFile = cumulus.GetLogFileName(fileDate);
				}
			}

			if (sb[sb.Length - 1] == ',')
			{
				sb.Length--;
				sbSpd.Length--;
				sbBrg.Length--;
				sbAvgBrg.Length--;
			}

			sb.Append("],");
			sb.Append(sbSpd);
			sb.Append("],");
			sb.Append(sbBrg);
			sb.Append("],");
			sb.Append(sbAvgBrg);
			sb.Append(']');
			sb.Append('}');
			return sb.ToString();
		}

		public string GetIntervalRainGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;


			while (!finished)
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervaRainGraphData: Processing log file - {logFile}");
					var linenum = 0;
					try
					{
						var logfile = File.ReadAllLines(logFile);

						foreach (var line in logfile)
						{
							// process each record in the file
							linenum++;

							var rec = ParseLogFileRec(line, true);

							if (rec.Date < dateFrom)
								continue;

							if (rec.Date > dateTo)
							{
								finished = true;
								cumulus.LogDebugMessage("GetIntervaRainGraphData: Finished processing the log files");
								break;
							}

							var jsTime = Utils.ToPseudoJSTime(rec.Date);

							sbRain.Append($"[{jsTime},{rec.RainToday.ToString(cumulus.RainFormat, InvC)}],");

							sbRate.Append($"[{jsTime},{rec.RainRate.ToString(cumulus.RainFormat, InvC)}],");
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervaRainGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervaRainGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervaRainGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					fileDate = fileDate.AddMonths(1);

					if (fileDate.Year > dateTo.Year || (fileDate.Year == dateTo.Year && fileDate.Month > dateTo.Month))
						finished = true;

					logFile = cumulus.GetLogFileName(fileDate);
				}
			}

			if (sbRain[sbRain.Length - 1] == ',')
			{
				sbRain.Length--;
				sbRate.Length--;
			}
			sbRain.Append("],");
			sbRate.Append(']');
			sb.Append(sbRain);
			sb.Append(sbRate);
			sb.Append('}');
			return sb.ToString();
		}


		public void AddRecentDataEntry(DateTime timestamp, double windAverage, double recentMaxGust, double windLatest, int bearing, int avgBearing, double outsidetemp,
			double windChill, double dewpoint, double heatIndex, int humidity, double pressure, double rainToday, double solarRad, double uv, double rainCounter, double feelslike, double humidex,
			double appTemp, double insideTemp, int insideHum, double solarMax, double rainrate, double pm2p5, double pm10)
		{
			try
			{
				RecentDataDb.InsertOrReplace(new RecentData()
				{
					Timestamp = timestamp,
					DewPoint = dewpoint,
					HeatIndex = heatIndex,
					Humidity = humidity,
					OutsideTemp = outsidetemp,
					Pressure = pressure,
					RainToday = rainToday,
					SolarRad = (int) solarRad,
					UV = uv,
					WindAvgDir = avgBearing,
					WindGust = recentMaxGust,
					WindLatest = windLatest,
					WindChill = windChill,
					WindDir = bearing,
					WindSpeed = windAverage,
					raincounter = rainCounter,
					FeelsLike = feelslike,
					Humidex = humidex,
					AppTemp = appTemp,
					IndoorTemp = insideTemp,
					IndoorHumidity = insideHum,
					SolarMax = (int) solarMax,
					RainRate = rainrate,
					Pm2p5 = pm2p5,
					Pm10 = pm10
				});
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("AddRecentDataEntry: " + ex.Message);
			}
		}

		public void CreateWxnowFile()
		{
			// Jun 01 2003 08:07
			// 272/000g006t069r010p030P020h61b10150CommentString

			// 272 - wind direction - 272 degrees
			// 010 - wind speed - 10 mph

			// g015 - wind gust - 15 mph
			// t069 - temperature - 69 degrees F
			// r010 - rain in last hour in hundredths of an inch - 0.1 inches
			// p030 - rain in last 24 hours in hundredths of an inch - 0.3 inches
			// P020 - rain since midnight in hundredths of an inch - 0.2 inches
			// h61 - humidity 61% (00 = 100%)
			// b10153 - barometric pressure in tenths of a millibar - 1015.3 millibars
			// CommentString - free format information text

			var filename = cumulus.AppDir + cumulus.WxnowFile;

			var data = CreateWxnowFileString();
			using (StreamWriter file = new StreamWriter(filename, false))
			{
				file.WriteLine(data);
				file.Close();
			}
		}

		public string CreateWxnowFileString()
		{
			// Jun 01 2003 08:07
			// 272/000g006t069r010p030P020h61b10150CommentString

			// 272 - wind direction - 272 degrees
			// 010 - wind speed - 10 mph

			// g015 - wind gust - 15 mph
			// t069 - temperature - 69 degrees F
			// r010 - rain in last hour in hundredths of an inch - 0.1 inches
			// p030 - rain in last 24 hours in hundredths of an inch - 0.3 inches
			// P020 - rain since midnight in hundredths of an inch - 0.2 inches
			// h61 - humidity 61% (00 = 100%)
			// b10153 - barometric pressure in tenths of a millibar - 1015.3 millibars
			// CommentString - free format information text

			var timestamp = DateTime.Now.ToString(@"MMM dd yyyy HH\:mm");

			int mphwind = Convert.ToInt32(ConvertUnits.UserWindToMPH(WindAverage));
			int mphgust = Convert.ToInt32(ConvertUnits.UserWindToMPH(RecentMaxGust));
			// ftemp = trunc(TempF(OutsideTemp));
			string ftempstr = APRStemp(OutdoorTemperature);
			int in100rainlasthour = Convert.ToInt32(ConvertUnits.UserRainToIN(RainLastHour) * 100);
			int in100rainlast24hours = Convert.ToInt32(ConvertUnits.UserRainToIN(RainLast24Hour) * 100);
			int in100raintoday;
			if (cumulus.RolloverHour == 0)
				// use today's rain for safety
				in100raintoday = Convert.ToInt32(ConvertUnits.UserRainToIN(RainToday) * 100);
			else
				// 0900 day, use midnight calculation
				in100raintoday = Convert.ToInt32(ConvertUnits.UserRainToIN(RainSinceMidnight) * 100);
			int mb10press = Convert.ToInt32(ConvertUnits.UserPressToMB(AltimeterPressure) * 10);
			// For 100% humidity, send zero. For zero humidity, send 1
			int hum;
			if (OutdoorHumidity == 0)
				hum = 1;
			else if (OutdoorHumidity == 100)
				hum = 0;
			else
				hum = OutdoorHumidity;

			string data = String.Format("{0}\n{1:000}/{2:000}g{3:000}t{4}r{5:000}p{6:000}P{7:000}h{8:00}b{9:00000}", timestamp, AvgBearing, mphwind, mphgust, ftempstr, in100rainlasthour,
				in100rainlast24hours, in100raintoday, hum, mb10press);

			if (cumulus.APRS.SendSolar)
			{
				data += APRSsolarradStr(SolarRad);
			}

			if (!String.IsNullOrWhiteSpace(cumulus.WxnowComment))
			{
				var tokenParser = new TokenParser(cumulus.TokenParserOnToken);

				// process the webtags in the content string
				tokenParser.InputText = cumulus.WxnowComment;
				data += tokenParser.ToStringFromString();
			}

			return data;
		}

		private string APRSsolarradStr(double solarRad)
		{
			if (solarRad < 1000)
			{
				return 'L' + (Convert.ToInt32(solarRad)).ToString("D3");
			}
			else
			{
				return 'l' + (Convert.ToInt32(solarRad - 1000)).ToString("D3");
			}
		}

		private string APRStemp(double temp)
		{
			// input is in TempUnit units, convert to F for APRS
			// and return three digits
			int num;

			if (cumulus.Units.Temp == 0)
			{
				num = Convert.ToInt32(((temp * 1.8) + 32));
			}

			else
			{
				num = Convert.ToInt32(temp);
			}

			if (num < 0)
			{
				num = -num;
				return '-' + num.ToString("00");
			}
			else
			{
				return num.ToString("000");
			}
		}


		public void ResetSunshineHours(DateTime logdate) // called at midnight irrespective of roll-over time
		{
			YestSunshineHours = SunshineHours;

			cumulus.LogMessage("Reset sunshine hours, yesterday = " + YestSunshineHours);

			SunshineToMidnight = SunshineHours;
			SunshineHours = 0;
			StartOfDaySunHourCounter = SunHourCounter;
			WriteYesterdayFile(logdate);
		}

		public void ResetMidnightTemperatures(DateTime logdate) // called at midnight irrespective of roll-over time
		{
			HiLoYestMidnight.LowTemp = HiLoTodayMidnight.LowTemp;
			HiLoYestMidnight.HighTemp = HiLoTodayMidnight.HighTemp;
			HiLoYestMidnight.LowTempTime = HiLoTodayMidnight.LowTempTime;
			HiLoYestMidnight.HighTempTime = HiLoTodayMidnight.HighTempTime;

			HiLoTodayMidnight.LowTemp = 999;
			HiLoTodayMidnight.HighTemp = -999;

			WriteYesterdayFile(logdate);
		}

		/*
		private void RecalcSolarFactor(DateTime now) // called at midnight irrespective of roll-over time
		{
			if (cumulus.SolarFactorSummer > 0 && cumulus.SolarFactorWinter > 0)
			{
				// Calculate the solar factor from the day of the year
				// Use a cosine of the difference between summer and winter values
				int doy = now.DayOfYear;
				// take summer solstice as June 21 or December 21 (N & S hemispheres) - ignore leap years
				// sol = day 172 (North)
				// sol = day 355 (South)
				int sol = cumulus.Latitude >= 0 ? 172 : 355;
				int daysSinceSol = (doy - sol) % 365;
				double multiplier = Math.Cos((daysSinceSol / 365) * 2 * Math.PI);  // range +1/-1
				SolarFactor = (multiplier + 1) / 2;  // bring it into the range 0-1
			}
			else
			{
				SolarFactor = -1;
			}
		}
		*/

		public void SwitchToNormalRunning()
		{
			cumulus.NormalRunning = true;

			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		public void ResetMidnightRain(DateTime timestamp)
		{
			int mrrday = timestamp.Day;

			int mrrmonth = timestamp.Month;

			if (mrrday != MidnightRainResetDay)
			{
				MidnightRainCount = RainCounter;
				RainSinceMidnight = 0;
				MidnightRainResetDay = mrrday;
				cumulus.LogMessage("Midnight rain reset, count = " + RainCounter + " time = " + timestamp.ToShortTimeString());
				if ((mrrday == 1) && (mrrmonth == 1) && (cumulus.StationType == StationTypes.VantagePro))
				{
					// special case: rain counter is about to be reset
					cumulus.LogMessage("Special case, Davis station on 1st Jan. Set midnight rain count to zero");
					MidnightRainCount = 0;
				}
			}
		}

		public void DoIndoorHumidity(int hum)
		{
			// Spike check
			if ((previousInHum != 999) && (Math.Abs(hum - previousInHum) > cumulus.Spike.InHumDiff))
			{
				cumulus.LogSpikeRemoval("Indoor humidity difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={hum} OldVal={previousInHum} SpikeDiff={cumulus.Spike.InHumDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Indoor humidity difference greater than spike value - NewVal={hum} OldVal={previousInHum}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousInHum = hum;
			IndoorHumidity = (int) cumulus.Calib.InHum.Calibrate(hum);

			if (IndoorHumidity < 0)
			{
				IndoorHumidity = 0;
			}
			if (IndoorHumidity > 100)
			{
				IndoorHumidity = 100;
			}

			HaveReadData = true;
		}

		public void DoIndoorTemp(double temp)
		{
			// Spike check
			if ((previousInTemp != 999) && (Math.Abs(temp - previousInTemp) > cumulus.Spike.InTempDiff))
			{
				cumulus.LogSpikeRemoval("Indoor temperature difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={temp} OldVal={previousInTemp} SpikeDiff={cumulus.Spike.InTempDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Indoor temperature difference greater than spike value - NewVal={temp} OldVal={previousInTemp}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousInTemp = temp;
			IndoorTemperature = cumulus.Calib.InTemp.Calibrate(temp);
			HaveReadData = true;
		}

		public void DoOutdoorHumidity(int humpar, DateTime timestamp)
		{
			// Spike check
			if ((previousHum != 999) && (Math.Abs(humpar - previousHum) > cumulus.Spike.HumidityDiff))
			{
				cumulus.LogSpikeRemoval("Humidity difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={humpar} OldVal={previousHum} SpikeHumidityDiff={cumulus.Spike.HumidityDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Humidity difference greater than spike value - NewVal={humpar} OldVal={previousHum}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			previousHum = humpar;

			if ((humpar >= 98) && cumulus.StationOptions.Humidity98Fix)
			{
				OutdoorHumidity = 100;
			}
			else
			{
				OutdoorHumidity = (int) cumulus.Calib.Hum.Calibrate(humpar);
			}

			if (OutdoorHumidity < 0)
			{
				OutdoorHumidity = 0;
			}
			if (OutdoorHumidity > 100)
			{
				OutdoorHumidity = 100;
			}

			if (OutdoorHumidity > HiLoToday.HighHumidity)
			{
				HiLoToday.HighHumidity = OutdoorHumidity;
				HiLoToday.HighHumidityTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorHumidity < HiLoToday.LowHumidity)
			{
				HiLoToday.LowHumidity = OutdoorHumidity;
				HiLoToday.LowHumidityTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorHumidity > ThisMonth.HighHumidity.Val)
			{
				ThisMonth.HighHumidity.Val = OutdoorHumidity;
				ThisMonth.HighHumidity.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorHumidity < ThisMonth.LowHumidity.Val)
			{
				ThisMonth.LowHumidity.Val = OutdoorHumidity;
				ThisMonth.LowHumidity.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorHumidity > ThisYear.HighHumidity.Val)
			{
				ThisYear.HighHumidity.Val = OutdoorHumidity;
				ThisYear.HighHumidity.Ts = timestamp;
				WriteYearIniFile();
			}
			if (OutdoorHumidity < ThisYear.LowHumidity.Val)
			{
				ThisYear.LowHumidity.Val = OutdoorHumidity;
				ThisYear.LowHumidity.Ts = timestamp;
				WriteYearIniFile();
			}
			if (OutdoorHumidity > AllTime.HighHumidity.Val)
			{
				SetAlltime(AllTime.HighHumidity, OutdoorHumidity, timestamp);
			}
			CheckMonthlyAlltime("HighHumidity", OutdoorHumidity, true, timestamp);
			if (OutdoorHumidity < AllTime.LowHumidity.Val)
			{
				SetAlltime(AllTime.LowHumidity, OutdoorHumidity, timestamp);
			}
			CheckMonthlyAlltime("LowHumidity", OutdoorHumidity, false, timestamp);
			HaveReadData = true;
		}


		public void DoOutdoorTemp(double temp, DateTime timestamp)
		{
			// Spike removal
			if (((Math.Abs(temp - previousTemp) > cumulus.Spike.TempDiff) && (previousTemp != 999)) ||
				temp >= cumulus.Limit.TempHigh || temp <= cumulus.Limit.TempLow)
			{
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Temp difference greater than spike value - NewVal={temp.ToString(cumulus.TempFormat)} OldVal={previousTemp.ToString(cumulus.TempFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				cumulus.LogSpikeRemoval("Temp difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={temp.ToString(cumulus.TempFormat)} OldVal={previousTemp.ToString(cumulus.TempFormat)} SpikeTempDiff={cumulus.Spike.TempDiff.ToString(cumulus.TempFormat)} HighLimit={cumulus.Limit.TempHigh.ToString(cumulus.TempFormat)} LowLimit={cumulus.Limit.TempLow.ToString(cumulus.TempFormat)}");
				return;
			}
			previousTemp = temp;

			// UpdateStatusPanel;
			// update global temp
			OutdoorTemperature = cumulus.Calib.Temp.Calibrate(temp);

			first_temp = false;

			// Does this reading set any records or trigger any alarms?
			if (OutdoorTemperature > AllTime.HighTemp.Val)
				SetAlltime(AllTime.HighTemp, OutdoorTemperature, timestamp);

			cumulus.HighTempAlarm.CheckAlarm(OutdoorTemperature);

			if (OutdoorTemperature < AllTime.LowTemp.Val)
				SetAlltime(AllTime.LowTemp, OutdoorTemperature, timestamp);

			cumulus.LowTempAlarm.CheckAlarm(OutdoorTemperature);

			CheckMonthlyAlltime("HighTemp", OutdoorTemperature, true, timestamp);
			CheckMonthlyAlltime("LowTemp", OutdoorTemperature, false, timestamp);

			if (OutdoorTemperature > HiLoToday.HighTemp)
			{
				HiLoToday.HighTemp = OutdoorTemperature;
				HiLoToday.HighTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (OutdoorTemperature < HiLoToday.LowTemp)
			{
				HiLoToday.LowTemp = OutdoorTemperature;
				HiLoToday.LowTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (OutdoorTemperature > HiLoTodayMidnight.HighTemp)
			{
				HiLoTodayMidnight.HighTemp = OutdoorTemperature;
				HiLoTodayMidnight.HighTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (OutdoorTemperature < HiLoTodayMidnight.LowTemp)
			{
				HiLoTodayMidnight.LowTemp = OutdoorTemperature;
				HiLoTodayMidnight.LowTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (OutdoorTemperature > ThisMonth.HighTemp.Val)
			{
				ThisMonth.HighTemp.Val = OutdoorTemperature;
				ThisMonth.HighTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (OutdoorTemperature < ThisMonth.LowTemp.Val)
			{
				ThisMonth.LowTemp.Val = OutdoorTemperature;
				ThisMonth.LowTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (OutdoorTemperature > ThisYear.HighTemp.Val)
			{
				ThisYear.HighTemp.Val = OutdoorTemperature;
				ThisYear.HighTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			if (OutdoorTemperature < ThisYear.LowTemp.Val)
			{
				ThisYear.LowTemp.Val = OutdoorTemperature;
				ThisYear.LowTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			// Calculate temperature range
			HiLoToday.TempRange = HiLoToday.HighTemp - HiLoToday.LowTemp;

			if ((cumulus.StationOptions.CalculatedDP || cumulus.DavisStation) && (OutdoorHumidity != 0) && (!cumulus.FineOffsetStation))
			{
				// Calculate DewPoint.
				double tempinC = ConvertUnits.UserTempToC(OutdoorTemperature);
				OutdoorDewpoint = ConvertUnits.TempCToUser(MeteoLib.DewPoint(tempinC, OutdoorHumidity));

				CheckForDewpointHighLow(timestamp);
			}

			TempReadyToPlot = true;
			HaveReadData = true;
		}


		public void DoCloudBaseHeatIndex(DateTime timestamp)
		{
			var tempinF = ConvertUnits.UserTempToF(OutdoorTemperature);
			var tempinC = ConvertUnits.UserTempToC(OutdoorTemperature);

			// Calculate cloud base
			CloudBase = (int) Math.Floor((tempinF - ConvertUnits.UserTempToF(OutdoorDewpoint)) / 4.4 * 1000 / (cumulus.CloudBaseInFeet ? 1 : 3.2808399));
			if (CloudBase < 0)
				CloudBase = 0;

			HeatIndex = ConvertUnits.TempCToUser(MeteoLib.HeatIndex(tempinC, OutdoorHumidity));

			if (HeatIndex > HiLoToday.HighHeatIndex)
			{
				HiLoToday.HighHeatIndex = HeatIndex;
				HiLoToday.HighHeatIndexTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (HeatIndex > ThisMonth.HighHeatIndex.Val)
			{
				ThisMonth.HighHeatIndex.Val = HeatIndex;
				ThisMonth.HighHeatIndex.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (HeatIndex > ThisYear.HighHeatIndex.Val)
			{
				ThisYear.HighHeatIndex.Val = HeatIndex;
				ThisYear.HighHeatIndex.Ts = timestamp;
				WriteYearIniFile();
			}

			if (HeatIndex > AllTime.HighHeatIndex.Val)
				SetAlltime(AllTime.HighHeatIndex, HeatIndex, timestamp);

			CheckMonthlyAlltime("HighHeatIndex", HeatIndex, true, timestamp);


			// Find estimated wet bulb temp. First time this is called, required variables may not have been set up yet
			try
			{
				WetBulb = ConvertUnits.TempCToUser(MeteoLib.CalculateWetBulbC(tempinC, ConvertUnits.UserTempToC(OutdoorDewpoint), ConvertUnits.UserPressToMB(Pressure)));
			}
			catch
			{
				WetBulb = OutdoorTemperature;
			}
		}

		public void DoApparentTemp(DateTime timestamp)
		{
			// Calculates Apparent Temperature
			// See http://www.bom.gov.au/info/thermal_stress/#atapproximation

			// don't try to calculate apparent if we haven't yet had wind and temp readings
			//if (TempReadyToPlot && WindReadyToPlot)
			//{
			//ApparentTemperature =
			//ConvertUnits.TempCToUser(ConvertUnits.UserTempToC(OutdoorTemperature) + (0.33 * MeteoLib.ActualVapourPressure(ConvertUnits.UserTempToC(OutdoorTemperature), OutdoorHumidity)) -
			//				   (0.7 * ConvertUnits.UserWindToMS(WindAverage)) - 4);
			ApparentTemperature = ConvertUnits.TempCToUser(MeteoLib.ApparentTemperature(ConvertUnits.UserTempToC(OutdoorTemperature), ConvertUnits.UserWindToMS(WindAverage), OutdoorHumidity));


			// we will tag on the THW Index here
			THWIndex = ConvertUnits.TempCToUser(MeteoLib.THWIndex(ConvertUnits.UserTempToC(OutdoorTemperature), OutdoorHumidity, ConvertUnits.UserWindToKPH(WindAverage)));

			if (ApparentTemperature > HiLoToday.HighAppTemp)
			{
				HiLoToday.HighAppTemp = ApparentTemperature;
				HiLoToday.HighAppTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (ApparentTemperature < HiLoToday.LowAppTemp)
			{
				HiLoToday.LowAppTemp = ApparentTemperature;
				HiLoToday.LowAppTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (ApparentTemperature > ThisMonth.HighAppTemp.Val)
			{
				ThisMonth.HighAppTemp.Val = ApparentTemperature;
				ThisMonth.HighAppTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (ApparentTemperature < ThisMonth.LowAppTemp.Val)
			{
				ThisMonth.LowAppTemp.Val = ApparentTemperature;
				ThisMonth.LowAppTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (ApparentTemperature > ThisYear.HighAppTemp.Val)
			{
				ThisYear.HighAppTemp.Val = ApparentTemperature;
				ThisYear.HighAppTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			if (ApparentTemperature < ThisYear.LowAppTemp.Val)
			{
				ThisYear.LowAppTemp.Val = ApparentTemperature;
				ThisYear.LowAppTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			if (ApparentTemperature > AllTime.HighAppTemp.Val)
				SetAlltime(AllTime.HighAppTemp, ApparentTemperature, timestamp);

			if (ApparentTemperature < AllTime.LowAppTemp.Val)
				SetAlltime(AllTime.LowAppTemp, ApparentTemperature, timestamp);

			CheckMonthlyAlltime("HighAppTemp", ApparentTemperature, true, timestamp);
			CheckMonthlyAlltime("LowAppTemp", ApparentTemperature, false, timestamp);
			//}
		}

		public void DoWindChill(double chillpar, DateTime timestamp)
		{
			bool chillvalid = true;

			if (cumulus.StationOptions.CalculatedWC || chillpar < -500)
			{
				// don"t try to calculate wind chill if we haven"t yet had wind and temp readings
				if (TempReadyToPlot && WindReadyToPlot)
				{
					double TempinC = ConvertUnits.UserTempToC(OutdoorTemperature);
					double windinKPH = ConvertUnits.UserWindToKPH(WindAverage);
					// no wind chill below 1.5 m/s = 5.4 km
					if (windinKPH >= 5.4)
					{
						WindChill = ConvertUnits.TempCToUser(MeteoLib.WindChill(TempinC, windinKPH));
					}
					else
					{
						WindChill = OutdoorTemperature;
					}
				}
				else
				{
					chillvalid = false;
				}
			}
			else
			{
				WindChill = chillpar;
			}

			if (chillvalid)
			{
				//WindChillReadyToPlot = true;

				if (WindChill < HiLoToday.LowWindChill)
				{
					HiLoToday.LowWindChill = WindChill;
					HiLoToday.LowWindChillTime = timestamp;
					WriteTodayFile(timestamp, false);
				}

				if (WindChill < ThisMonth.LowChill.Val)
				{
					ThisMonth.LowChill.Val = WindChill;
					ThisMonth.LowChill.Ts = timestamp;
					WriteMonthIniFile();
				}

				if (WindChill < ThisYear.LowChill.Val)
				{
					ThisYear.LowChill.Val = WindChill;
					ThisYear.LowChill.Ts = timestamp;
					WriteYearIniFile();
				}

				// All time wind chill
				if (WindChill < AllTime.LowChill.Val)
				{
					SetAlltime(AllTime.LowChill, WindChill, timestamp);
				}

				CheckMonthlyAlltime("LowChill", WindChill, false, timestamp);
			}
		}

		public void DoFeelsLike(DateTime timestamp)
		{
			FeelsLike = ConvertUnits.TempCToUser(MeteoLib.FeelsLike(ConvertUnits.UserTempToC(OutdoorTemperature), ConvertUnits.UserWindToKPH(WindAverage), OutdoorHumidity));

			if (FeelsLike > HiLoToday.HighFeelsLike)
			{
				HiLoToday.HighFeelsLike = FeelsLike;
				HiLoToday.HighFeelsLikeTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (FeelsLike < HiLoToday.LowFeelsLike)
			{
				HiLoToday.LowFeelsLike = FeelsLike;
				HiLoToday.LowFeelsLikeTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (FeelsLike > ThisMonth.HighFeelsLike.Val)
			{
				ThisMonth.HighFeelsLike.Val = FeelsLike;
				ThisMonth.HighFeelsLike.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (FeelsLike < ThisMonth.LowFeelsLike.Val)
			{
				ThisMonth.LowFeelsLike.Val = FeelsLike;
				ThisMonth.LowFeelsLike.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (FeelsLike > ThisYear.HighFeelsLike.Val)
			{
				ThisYear.HighFeelsLike.Val = FeelsLike;
				ThisYear.HighFeelsLike.Ts = timestamp;
				WriteYearIniFile();
			}

			if (FeelsLike < ThisYear.LowFeelsLike.Val)
			{
				ThisYear.LowFeelsLike.Val = FeelsLike;
				ThisYear.LowFeelsLike.Ts = timestamp;
				WriteYearIniFile();
			}

			if (FeelsLike > AllTime.HighFeelsLike.Val)
				SetAlltime(AllTime.HighFeelsLike, FeelsLike, timestamp);

			if (FeelsLike < AllTime.LowFeelsLike.Val)
				SetAlltime(AllTime.LowFeelsLike, FeelsLike, timestamp);

			CheckMonthlyAlltime("HighFeelsLike", FeelsLike, true, timestamp);
			CheckMonthlyAlltime("LowFeelsLike", FeelsLike, false, timestamp);
		}

		public void DoHumidex(DateTime timestamp)
		{
			Humidex = MeteoLib.Humidex(ConvertUnits.UserTempToC(OutdoorTemperature), OutdoorHumidity);

			if (Humidex > HiLoToday.HighHumidex)
			{
				HiLoToday.HighHumidex = Humidex;
				HiLoToday.HighHumidexTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (Humidex > ThisMonth.HighHumidex.Val)
			{
				ThisMonth.HighHumidex.Val = Humidex;
				ThisMonth.HighHumidex.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (Humidex > ThisYear.HighHumidex.Val)
			{
				ThisYear.HighHumidex.Val = Humidex;
				ThisYear.HighHumidex.Ts = timestamp;
				WriteYearIniFile();
			}

			if (Humidex > AllTime.HighHumidex.Val)
				SetAlltime(AllTime.HighHumidex, Humidex, timestamp);

			CheckMonthlyAlltime("HighHumidex", Humidex, true, timestamp);
		}

		public void CheckForWindrunHighLow(DateTime timestamp)
		{
			DateTime adjustedtimestamp = timestamp.AddHours(cumulus.GetHourInc());

			if (WindRunToday > ThisMonth.HighWindRun.Val)
			{
				ThisMonth.HighWindRun.Val = WindRunToday;
				ThisMonth.HighWindRun.Ts = adjustedtimestamp;
				WriteMonthIniFile();
			}

			if (WindRunToday > ThisYear.HighWindRun.Val)
			{
				ThisYear.HighWindRun.Val = WindRunToday;
				ThisYear.HighWindRun.Ts = adjustedtimestamp;
				WriteYearIniFile();
			}

			if (WindRunToday > AllTime.HighWindRun.Val)
			{
				SetAlltime(AllTime.HighWindRun, WindRunToday, adjustedtimestamp);
			}

			CheckMonthlyAlltime("HighWindRun", WindRunToday, true, adjustedtimestamp);
		}

		public void CheckForDewpointHighLow(DateTime timestamp)
		{
			if (OutdoorDewpoint > HiLoToday.HighDewPoint)
			{
				HiLoToday.HighDewPoint = OutdoorDewpoint;
				HiLoToday.HighDewPointTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorDewpoint < HiLoToday.LowDewPoint)
			{
				HiLoToday.LowDewPoint = OutdoorDewpoint;
				HiLoToday.LowDewPointTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorDewpoint > ThisMonth.HighDewPoint.Val)
			{
				ThisMonth.HighDewPoint.Val = OutdoorDewpoint;
				ThisMonth.HighDewPoint.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorDewpoint < ThisMonth.LowDewPoint.Val)
			{
				ThisMonth.LowDewPoint.Val = OutdoorDewpoint;
				ThisMonth.LowDewPoint.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorDewpoint > ThisYear.HighDewPoint.Val)
			{
				ThisYear.HighDewPoint.Val = OutdoorDewpoint;
				ThisYear.HighDewPoint.Ts = timestamp;
				WriteYearIniFile();
			}
			if (OutdoorDewpoint < ThisYear.LowDewPoint.Val)
			{
				ThisYear.LowDewPoint.Val = OutdoorDewpoint;
				ThisYear.LowDewPoint.Ts = timestamp;
				WriteYearIniFile();
			}
			;
			if (OutdoorDewpoint > AllTime.HighDewPoint.Val)
			{
				SetAlltime(AllTime.HighDewPoint, OutdoorDewpoint, timestamp);
			}
			if (OutdoorDewpoint < AllTime.LowDewPoint.Val)
				SetAlltime(AllTime.LowDewPoint, OutdoorDewpoint, timestamp);

			CheckMonthlyAlltime("HighDewPoint", OutdoorDewpoint, true, timestamp);
			CheckMonthlyAlltime("LowDewPoint", OutdoorDewpoint, false, timestamp);
		}

		public void DoPressure(double sl, DateTime timestamp)
		{
			// Spike removal
			if (((Math.Abs(sl - previousPress) > cumulus.Spike.PressDiff) && (previousPress != 9999)) ||
				sl >= cumulus.Limit.PressHigh || sl <= cumulus.Limit.PressLow)
			{
				cumulus.LogSpikeRemoval("Pressure difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sl:F1} OldVal={previousPress:F1} SpikePressDiff={cumulus.Spike.PressDiff:F1} HighLimit={cumulus.Limit.PressHigh:F1} LowLimit={cumulus.Limit.PressLow:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Pressure difference greater than spike value - NewVal={sl:F1} OldVal={previousPress:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousPress = sl;

			Pressure = cumulus.Calib.Press.Calibrate(sl);
			if (cumulus.Manufacturer == cumulus.DAVIS)
			{
				if ((cumulus.StationType == StationTypes.VantagePro2 && !cumulus.DavisOptions.UseLoop2) || cumulus.StationType == StationTypes.VantagePro)
				{
					// Loop2 data not available, just use sea level (for now, anyway)
					AltimeterPressure = Pressure;
				}
			}
			else
			{
				if (cumulus.Manufacturer == cumulus.OREGONUSB)
				{
					AltimeterPressure = ConvertUnits.PressMBToUser(StationToAltimeter(ConvertUnits.UserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
				}
				else
				{
					// For all other stations, altimeter is same as sea-level
					AltimeterPressure = Pressure;
				}
			}

			first_press = false;

			if (Pressure > AllTime.HighPress.Val)
			{
				SetAlltime(AllTime.HighPress, Pressure, timestamp);
			}

			cumulus.HighPressAlarm.CheckAlarm(Pressure);

			if (Pressure < AllTime.LowPress.Val)
			{
				SetAlltime(AllTime.LowPress, Pressure, timestamp);
			}

			cumulus.LowPressAlarm.CheckAlarm(Pressure);
			CheckMonthlyAlltime("LowPress", Pressure, false, timestamp);
			CheckMonthlyAlltime("HighPress", Pressure, true, timestamp);

			if (Pressure > HiLoToday.HighPress)
			{
				HiLoToday.HighPress = Pressure;
				HiLoToday.HighPressTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (Pressure < HiLoToday.LowPress)
			{
				HiLoToday.LowPress = Pressure;
				HiLoToday.LowPressTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (Pressure > ThisMonth.HighPress.Val)
			{
				ThisMonth.HighPress.Val = Pressure;
				ThisMonth.HighPress.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (Pressure < ThisMonth.LowPress.Val)
			{
				ThisMonth.LowPress.Val = Pressure;
				ThisMonth.LowPress.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (Pressure > ThisYear.HighPress.Val)
			{
				ThisYear.HighPress.Val = Pressure;
				ThisYear.HighPress.Ts = timestamp;
				WriteYearIniFile();
			}

			if (Pressure < ThisYear.LowPress.Val)
			{
				ThisYear.LowPress.Val = Pressure;
				ThisYear.LowPress.Ts = timestamp;
				WriteYearIniFile();
			}

			PressReadyToPlot = true;
			HaveReadData = true;
		}

		protected void DoPressTrend(string trend)
		{
			if (cumulus.StationOptions.UseCumulusPresstrendstr)
			{
				UpdatePressureTrendString();
			}
			else
			{
				Presstrendstr = trend;
			}
		}

		public void DoRain(double total, double rate, DateTime timestamp)
		{
			DateTime readingTS = timestamp.AddHours(cumulus.GetHourInc());

			//cumulus.LogDebugMessage($"DoRain: counter={total}, rate={rate}; RainToday={RainToday}, StartOfDay={raindaystart}");

			// Spike removal
			if (rate > cumulus.Spike.MaxRainRate)
			{
				cumulus.LogSpikeRemoval("Rain rate greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Rate value = {rate:F2} SpikeMaxRainRate = {cumulus.Spike.MaxRainRate:F2}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Rain rate greater than spike value - value = {rate:F2}mm/hr";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			if ((CurrentDay != readingTS.Day) || (CurrentMonth != readingTS.Month) || (CurrentYear != readingTS.Year))
			{
				// A reading has apparently arrived at the start of a new day, but before we have done the roll-over
				// Ignore it, as otherwise it may cause a new monthly record to be logged using last month's total
				cumulus.LogDebugMessage("DoRain: A reading arrived at the start of a new day, but before we have done the roll-over. Ignoring it");
				return;
			}

			var previoustotal = RainCounter;

			// This is just to stop rounding errors triggering phantom rain days
			//double raintipthreshold = cumulus.Units.Rain == 0 ? 0.009 : 0.0003;

			/*
			if (cumulus.Manufacturer == cumulus.DAVIS)  // Davis can have either 0.2mm or 0.01in buckets, and the user could select to measure in mm or inches!
			{
				// If the bucket size is set, use that, otherwise infer from rain units
				var bucketSize = cumulus.DavisOptions.RainGaugeType == -1 ? cumulus.Units.Rain : cumulus.DavisOptions.RainGaugeType;

				switch (bucketSize)
				{
					case 0: // 0.2 mm tips
						// mm/mm (0.2) or mm/in (0.00787)
						raintipthreshold = cumulus.Units.Rain == 0 ? 0.19 : 0.006;
						break;
					case 1: // 0.01 inch tips
						// in/mm (0.254) or in/in (0.01)
						raintipthreshold = cumulus.Units.Rain == 0 ? 0.2 : 0.009;
						break;
					case 2: // 0.01 mm tips
						// mm/mm (0.1) or mm/in (0.0394)
						raintipthreshold = cumulus.Units.Rain == 0 ? 0.09 : 0.003;
						break;
					case 3: // 0.001 inch tips
						// in/mm (0.0254) or in/in (0.001)
						raintipthreshold = cumulus.Units.Rain == 0 ? 0.02 : 0.0009;
						break;
				}
			}
			else
			{
				if (cumulus.Units.Rain == 0)
				{
					// mm
					raintipthreshold = cumulus.Manufacturer == cumulus.INSTROMET ? 0.009 : 0.09;
				}
				else
				{
					// in
					raintipthreshold = cumulus.Manufacturer == cumulus.INSTROMET ? 0.0003 : 0.003;
				}
			}
			*/

			RainCounter = total;

			if (initialiseRainDayStart || initialiseMidnightRain)
			{
				initialiseRainDayStart = false;
				initialiseMidnightRain = false;

				if (initialiseRainDayStart)
				{
					RainCounterDayStart = RainCounter;
					cumulus.LogMessage(" First rain data, raindaystart = " + RainCounterDayStart);
				}

				if (initialiseMidnightRain)
				{
					MidnightRainCount = RainCounter;
				}

				WriteTodayFile(timestamp, false);
				HaveReadData = true;
				return;
			}

			// Has the rain total in the station been reset?
			// raindaystart greater than current total, allow for rounding
			if (Math.Round(RainCounterDayStart, cumulus.RainDPlaces) - Math.Round(RainCounter, cumulus.RainDPlaces) > 0)
			{
				if (FirstChanceRainReset)
				// second consecutive reading with reset value
				{
					cumulus.LogWarningMessage(" ****Rain counter reset confirmed: raindaystart = " + RainCounterDayStart + ", Raincounter = " + RainCounter);

					// set the start of day figure so it reflects the rain
					// so far today
					RainCounterDayStart = RainCounter - (RainToday / cumulus.Calib.Rain.Mult);
					cumulus.LogMessage("Setting raindaystart to " + RainCounterDayStart);

					MidnightRainCount = RainCounter;
					previoustotal = total;

					// update any data in the recent data db
					var counterChange = RainCounter - prevraincounter;
					RecentDataDb.Execute("update RecentData set raincounter=raincounter+?", counterChange);

					FirstChanceRainReset = false;
				}
				else
				{
					cumulus.LogMessage(" ****Rain reset? First chance: raindaystart = " + RainCounterDayStart + ", Raincounter = " + RainCounter);

					// reset the counter to ignore this reading
					RainCounter = previoustotal;
					cumulus.LogMessage("Leaving counter at " + RainCounter);

					// stash the previous rain counter
					prevraincounter = RainCounter;

					FirstChanceRainReset = true;
				}
			}
			else
			{
				FirstChanceRainReset = false;
			}

			if (rate > -1)
			// Do rain rate
			{
				// scale rainfall rate
				RainRate = rate * cumulus.Calib.Rain.Mult;

				if (cumulus.StationOptions.UseRainForIsRaining == 1)
				{
					IsRaining = RainRate > 0;
					cumulus.IsRainingAlarm.Triggered = IsRaining;
				}

				if (RainRate > AllTime.HighRainRate.Val)
					SetAlltime(AllTime.HighRainRate, RainRate, timestamp);

				CheckMonthlyAlltime("HighRainRate", RainRate, true, timestamp);

				cumulus.HighRainRateAlarm.CheckAlarm(RainRate);

				if (RainRate > HiLoToday.HighRainRate)
				{
					HiLoToday.HighRainRate = RainRate;
					HiLoToday.HighRainRateTime = timestamp;
					WriteTodayFile(timestamp, false);
				}

				if (RainRate > ThisMonth.HighRainRate.Val)
				{
					ThisMonth.HighRainRate.Val = RainRate;
					ThisMonth.HighRainRate.Ts = timestamp;
					WriteMonthIniFile();
				}

				if (RainRate > ThisYear.HighRainRate.Val)
				{
					ThisYear.HighRainRate.Val = RainRate;
					ThisYear.HighRainRate.Ts = timestamp;
					WriteYearIniFile();
				}
			}

			if (!FirstChanceRainReset)
			{
				// Has a tip occurred?
				if (Math.Round(total, cumulus.RainDPlaces) - Math.Round(previoustotal, cumulus.RainDPlaces) > 0)
				{
					// rain has occurred
					LastRainTip = timestamp.ToString("yyyy-MM-dd HH:mm");

					if (cumulus.StationOptions.UseRainForIsRaining == 1)
					{
						IsRaining = true;
						cumulus.IsRainingAlarm.Triggered = true;
					}
				}
				else if (cumulus.StationOptions.UseRainForIsRaining == 1 && RainRate <= 0)
				{
					IsRaining = false;
					cumulus.IsRainingAlarm.Triggered = false;
				}

				// Calculate today"s rainfall
				RainToday = (RainCounter - RainCounterDayStart) * cumulus.Calib.Rain.Mult;
				//cumulus.LogDebugMessage("Uncalibrated RainToday = " + RainToday);

				// Calculate rain since midnight for Wunderground etc
				double trendval = RainCounter - MidnightRainCount;

				// Round value as some values may have been read from log file and already rounded
				trendval = Math.Round(trendval, cumulus.RainDPlaces);

				if (trendval < 0)
				{
					RainSinceMidnight = 0;
				}
				else
				{
					RainSinceMidnight = trendval * cumulus.Calib.Rain.Mult;
				}

				// rain this month so far
				RainMonth = RainThisMonth + RainToday;

				// get correct date for rain records
				var offsetdate = timestamp.AddHours(cumulus.GetHourInc());

				// rain this year so far
				RainYear = RainThisYear + RainToday;

				if (RainToday > AllTime.DailyRain.Val)
					SetAlltime(AllTime.DailyRain, RainToday, offsetdate);

				CheckMonthlyAlltime("DailyRain", RainToday, true, timestamp);

				if (RainToday > ThisMonth.DailyRain.Val)
				{
					ThisMonth.DailyRain.Val = RainToday;
					ThisMonth.DailyRain.Ts = offsetdate;
					WriteMonthIniFile();
				}

				if (RainToday > ThisYear.DailyRain.Val)
				{
					ThisYear.DailyRain.Val = RainToday;
					ThisYear.DailyRain.Ts = offsetdate;
					WriteYearIniFile();
				}

				if (RainMonth > ThisYear.MonthlyRain.Val)
				{
					ThisYear.MonthlyRain.Val = RainMonth;
					ThisYear.MonthlyRain.Ts = offsetdate;
					WriteYearIniFile();
				}

				if (RainMonth > AllTime.MonthlyRain.Val)
					SetAlltime(AllTime.MonthlyRain, RainMonth, offsetdate);

				CheckMonthlyAlltime("MonthlyRain", RainMonth, true, timestamp);

				cumulus.HighRainTodayAlarm.CheckAlarm(RainToday);

				// Yesterday"s rain - Scale for units
				// rainyest = rainyesterday * RainMult;

				//RainReadyToPlot = true;
			}
			HaveReadData = true;
		}

		public void DoOutdoorDewpoint(double dp, DateTime timestamp)
		{

			if (cumulus.StationOptions.CalculatedDP || dp < -500)
			{
				dp = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(OutdoorTemperature), OutdoorHumidity));

			}

			if (ConvertUnits.UserTempToC(dp) <= cumulus.Limit.DewHigh)
			{
				OutdoorDewpoint = dp;
				CheckForDewpointHighLow(timestamp);
			}
			else
			{
				var msg = $"Dew point greater than limit ({cumulus.Limit.DewHigh.ToString(cumulus.TempFormat)}); reading ignored: {dp.ToString(cumulus.TempFormat)}";
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = msg;
				cumulus.SpikeAlarm.Triggered = true;
				cumulus.LogSpikeRemoval(msg);
			}
		}

		public string LastRainTip { get; set; }

		public void DoExtraHum(double hum, int channel)
		{
			if ((channel > 0) && (channel < ExtraHum.Length))
			{
				ExtraHum[channel] = (int) hum;
			}
		}

		public void DoExtraTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < ExtraTemp.Length))
			{
				ExtraTemp[channel] = temp;
			}
		}

		public void DoUserTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < UserTemp.Length))
			{
				UserTemp[channel] = temp;
			}
		}


		public void DoExtraDP(double dp, int channel)
		{
			if ((channel > 0) && (channel < ExtraDewPoint.Length))
			{
				ExtraDewPoint[channel] = dp;
			}
		}

		public void DoForecast(string forecast, bool hourly)
		{
			// store weather station forecast if available

			if (forecast != "")
			{
				wsforecast = forecast;
			}

			if (!cumulus.UseCumulusForecast)
			{
				// user wants to display station forecast
				forecaststr = wsforecast;
			}

			// determine whether we need to update the Cumulus forecast; user may have chosen to only update once an hour, but
			// we still need to do that once to get an initial forecast
			if ((!FirstForecastDone) || (!cumulus.HourlyForecast) || (hourly && cumulus.HourlyForecast))
			{
				int bartrend;
				if ((presstrendval >= -cumulus.FCPressureThreshold) && (presstrendval <= cumulus.FCPressureThreshold))
					bartrend = 0;
				else if (presstrendval < 0)
					bartrend = 2;
				else
					bartrend = 1;

				string windDir;
				if (WindAverage < 0.1)
				{
					windDir = "calm";
				}
				else
				{
					windDir = AvgBearingText;
				}

				double lp;
				double hp;
				if (cumulus.FCpressinMB)
				{
					lp = cumulus.FClowpress;
					hp = cumulus.FChighpress;
				}
				else
				{
					lp = cumulus.FClowpress / 0.0295333727;
					hp = cumulus.FChighpress / 0.0295333727;
				}

				CumulusForecast = BetelCast(ConvertUnits.UserPressureToHPa(Pressure), DateTime.Now.Month, windDir, bartrend, cumulus.Latitude > 0, hp, lp);

				if (cumulus.UseCumulusForecast)
				{
					// user wants to display Cumulus forecast
					forecaststr = CumulusForecast;
				}
			}

			FirstForecastDone = true;
			HaveReadData = true;
		}

		public string forecaststr { get; set; }

		public string CumulusForecast { get; set; }

		public string wsforecast { get; set; }

		public bool FirstForecastDone = false;

		/// <summary>
		/// Convert altitude from user units to metres
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public double AltitudeM(double altitude)
		{
			if (cumulus.AltitudeInFeet)
			{
				return altitude * 0.3048;
			}
			else
			{
				return altitude;
			}
		}


		public double StationToAltimeter(double pressureHPa, double elevationM)
		{
			// from MADIS API by NOAA Forecast Systems Lab, see http://madis.noaa.gov/madis_api.html

			double k1 = 0.190284; // discrepancy with calculated k1 probably because Smithsonian used less precise gas constant and gravity values
			double k2 = 8.4184960528E-5; // (standardLapseRate / standardTempK) * (Power(standardSLP, k1)
			return Math.Pow(Math.Pow(pressureHPa - 0.3, k1) + (k2 * elevationM), 1 / k1);
		}

		public bool PressReadyToPlot { get; set; }

		public bool first_press { get; set; }

		/*
		private string TimeToStrHHMM(DateTime timestamp)
		{
			return timestamp.ToString("HHmm");
		}
		*/


		// Use -1 for the average if you want to feedback the current average for a calculated moving average
		public void DoWind(double gustpar, int bearingpar, double speedpar, DateTime timestamp)
		{
			cumulus.LogDebugMessage($"DoWind: latest={gustpar:F1}, speed={speedpar:F1} - Current: gust={RecentMaxGust:F1}, speed={WindAverage:F1}");
			// if we have a spike in wind speed or gust, ignore the reading
			// Spike removal
			if (previousGust != 999 && (Math.Abs(gustpar - previousGust) > cumulus.Spike.GustDiff || gustpar >= cumulus.Limit.WindHigh))
			{
				cumulus.LogSpikeRemoval("Gust difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={gustpar:F1} OldVal={previousGust:F1} SpikeGustDiff={cumulus.Spike.GustDiff:F1} HighLimit={cumulus.Limit.WindHigh:F1}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind or gust difference greater than spike/limit value - Gust: NewVal={gustpar:F1}m/s OldVal={previousGust:F1}m/s";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			if (previousWind != 999 && (Math.Abs(speedpar - previousWind) > cumulus.Spike.WindDiff || speedpar >= cumulus.Limit.WindHigh))
			{
				cumulus.LogSpikeRemoval("Wind difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Wind: NewVal={speedpar:F1} OldVal={previousWind:F1} SpikeWindDiff={cumulus.Spike.WindDiff:F1} HighLimit={cumulus.Limit.WindHigh:F1}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind or gust difference greater than spike/limit value -  Wind: NewVal={speedpar:F1}m/s OldVal={previousWind:F1}m/s";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousGust = gustpar;
			previousWind = speedpar;

			calibratedgust = cumulus.Calib.WindGust.Calibrate(gustpar);
			var uncalibratedspeed = speedpar < 0 ? WindAverageUncalibrated : speedpar;
			var calibratedspeed = cumulus.Calib.WindSpeed.Calibrate(uncalibratedspeed);

			// use bearing of zero when calm
			if ((Math.Abs(gustpar) < 0.001) && cumulus.StationOptions.UseZeroBearing)
			{
				Bearing = 0;
			}
			else
			{
				Bearing = (bearingpar + (int) cumulus.Calib.WindDir.Offset) % 360;
				if (Bearing < 0)
				{
					Bearing = 360 + Bearing;
				}

				if (Bearing == 0)
				{
					Bearing = 360;
				}
			}

			WindLatest = cumulus.StationOptions.UseSpeedForLatest ? calibratedspeed : calibratedgust;

			windspeeds[nextwindvalue] = calibratedgust;
			windbears[nextwindvalue] = Bearing;
			nextwindvalue = (nextwindvalue + 1) % maxwindvalues;

			// Recalculate wind rose data
			lock (windcounts)
			{
				for (int i = 0; i < cumulus.NumWindRosePoints; i++)
				{
					windcounts[i] = 0;
				}

				for (int i = 0; i < numwindvalues; i++)
				{
					int j = (((windbears[i] * 100) + 1125) % 36000) / (int) Math.Floor(cumulus.WindRoseAngle * 100);
					windcounts[j] += windspeeds[i];
				}
			}

			if (numwindvalues < maxwindvalues)
			{
				numwindvalues++;
			}

			CheckHighGust(calibratedgust, Bearing, timestamp);

			WindRecent[nextwind].Gust = gustpar; // We store uncalibrated gust values, so if we need to calculate the average from them we do not need to uncalibrate
			WindRecent[nextwind].Speed = uncalibratedspeed;
			WindRecent[nextwind].Timestamp = timestamp;
			nextwind = (nextwind + 1) % MaxWindRecent;

#if DEBUGWIND
			cumulus.LogDebugMessage($"Wind calc using speed: {cumulus.StationOptions.UseSpeedForAvgCalc}");
#endif

			if (cumulus.StationOptions.CalcuateAverageWindSpeed)
			{
				int numvalues = 0;
				double totalwind = 0;
				double avg = 0;
				var fromTime = timestamp - cumulus.AvgSpeedTime;
				for (int i = 0; i < MaxWindRecent; i++)
				{
					if (WindRecent[i].Timestamp >= fromTime)
					{
#if DEBUGWIND
//						cumulus.LogDebugMessage($"Wind Time:{WindRecent[i].Timestamp.ToLongTimeString()} Gust:{WindRecent[i].Gust:F1} Speed:{WindRecent[i].Speed:F1}");
#endif

						numvalues++;
						totalwind += cumulus.StationOptions.UseSpeedForAvgCalc ? WindRecent[i].Speed : WindRecent[i].Gust;
					}
				}
				// average the values, if we have enough samples
				if (numvalues > 3)
				{
					avg = totalwind / numvalues;
				}
				else
				{
					avg = totalwind / 3;
				}

				WindAverageUncalibrated = avg;

				// we want any calibration to be applied from uncalibrated values
				WindAverage = cumulus.Calib.WindSpeed.Calibrate(avg);

				//cumulus.LogDebugMessage("next=" + nextwind + " wind=" + uncalibratedgust + " tot=" + totalwind + " numv=" + numvalues + " avg=" + WindAverage);
			}
			else
			{
				WindAverage = calibratedspeed;
				WindAverageUncalibrated = uncalibratedspeed;
			}


			if (CalcRecentMaxGust)
			{
				// Find recent max gust
				var fromTime = timestamp - cumulus.PeakGustTime;

				double maxgust = 0;
				for (int i = 0; i <= MaxWindRecent - 1; i++)
				{
					if (WindRecent[i].Timestamp >= fromTime && WindRecent[i].Gust > maxgust)
					{
						maxgust = WindRecent[i].Gust;
					}
				}
				// wind gust is stored uncaligrated, so we need to calibrate now
				RecentMaxGust = cumulus.Calib.WindGust.Calibrate(maxgust);
			}
			else
			{
				RecentMaxGust = calibratedgust;
			}

			cumulus.LogDebugMessage($"DoWind: New: gust={RecentMaxGust:F1}, speed={WindAverage:F1}, latest:{WindLatest:F1}");

			CheckHighAvgSpeed(timestamp);

			WindVec[nextwindvec].X = calibratedgust * Math.Sin(DegToRad(Bearing));
			WindVec[nextwindvec].Y = calibratedgust * Math.Cos(DegToRad(Bearing));
			// save timestamp of this reading
			WindVec[nextwindvec].Timestamp = timestamp;
			// save bearing
			WindVec[nextwindvec].Bearing = Bearing; // savedBearing;
													// increment index for next reading
			nextwindvec = (nextwindvec + 1) % MaxWindRecent;

			// Now add up all the values within the required period
			double totalwindX = 0;
			double totalwindY = 0;
			int diffFrom = 0;
			int diffTo = 0;

			for (int i = 0; i < MaxWindRecent; i++)
			{
				if (timestamp - WindVec[i].Timestamp < cumulus.AvgBearingTime)
				{
					totalwindX += WindVec[i].X;
					totalwindY += WindVec[i].Y;

					if (WindVec[i].Bearing != 0)
					{
						// this reading was within the last N minutes
						int difference = getShortestAngle(AvgBearing, WindVec[i].Bearing);
						if ((difference > diffTo))
						{
							diffTo = difference;
							BearingRangeTo = WindVec[i].Bearing;
						}
						if ((difference < diffFrom))
						{
							diffFrom = difference;
							BearingRangeFrom = WindVec[i].Bearing;
						}
					}
				}
			}
			if (totalwindX == 0)
			{
				AvgBearing = 0;
			}
			else
			{
				AvgBearing = (int) Math.Round(RadToDeg(Math.Atan(totalwindY / totalwindX)));

				if (totalwindX < 0)
				{
					AvgBearing = 270 - AvgBearing;
				}
				else
				{
					AvgBearing = 90 - AvgBearing;
				}

				if (AvgBearing == 0)
				{
					AvgBearing = 360;
				}
			}

			if ((Math.Abs(WindAverage) < 0.01) && cumulus.StationOptions.UseZeroBearing)
			{
				AvgBearing = 0;
			}

			AvgBearingText = CompassPoint(AvgBearing);

			if (Math.Abs(WindAverage) < 0.01)
			{
				BearingRangeFrom = 0;
				BearingRangeFrom10 = 0;
				BearingRangeTo = 0;
				BearingRangeTo10 = 0;
			}
			else
			{
				// Calculate rounded up/down values
				BearingRangeFrom10 = (int) (Math.Floor(BearingRangeFrom / 10.0) * 10);
				BearingRangeTo10 = (int) (Math.Ceiling(BearingRangeTo / 10.0) * 10) % 360;
				if (cumulus.StationOptions.UseZeroBearing && BearingRangeFrom10 == 0)
				{
					BearingRangeFrom10 = 360;
				}
				if (cumulus.StationOptions.UseZeroBearing && BearingRangeTo10 == 0)
				{
					BearingRangeTo10 = 360;
				}
			}

			WindReadyToPlot = true;
			HaveReadData = true;
		}

		// called at start-up to initialise the gust and average speeds from the recent data to avoid zero values
		public void InitialiseWind()
		{
			// first the average
			var fromTime = cumulus.LastUpdateTime - cumulus.AvgSpeedTime;
			var numvalues = 0;
			var totalwind = 0.0;

			for (int i = 0; i < MaxWindRecent; i++)
			{
				if (WindRecent[i].Timestamp >= fromTime)
				{
					numvalues++;
					totalwind += WindRecent[i].Speed;
				}
			}
			// average the values, if we have enough samples
			if (numvalues > 3)
				WindAverage = totalwind / numvalues;
			else
				WindAverage = totalwind / 3;

			// now the gust
			fromTime = DateTime.Now - cumulus.PeakGustTime;

			for (int i = 0; i < MaxWindRecent; i++)
			{
				if (WindRecent[i].Timestamp >= fromTime)
				{
					if (WindRecent[i].Gust > RecentMaxGust)
					{
						RecentMaxGust = WindRecent[i].Gust;
					}
				}
			}

			cumulus.LogDebugMessage($"InitialiseWind: gust={RecentMaxGust:F1}, speed={WindAverage:F1}");
		}

		public void AddValuesToRecentWind(double gust, double speed, int bearing, DateTime start, DateTime end)
		{
			var calGust = cumulus.Calib.WindGust.Calibrate(gust);
			int calBearing;

			// use bearing of zero when calm
			if ((Math.Abs(gust) < 0.001) && cumulus.StationOptions.UseZeroBearing)
			{
				calBearing = 0;
			}
			else
			{
				calBearing = (bearing + (int) cumulus.Calib.WindDir.Offset) % 360;
				if (calBearing < 0)
				{
					calBearing = 360 + calBearing;
				}

				if (calBearing == 0)
				{
					calBearing = 360;
				}
			}


			for (DateTime ts = start; ts <= end; ts = ts.AddSeconds(3))
			{
				WindRecent[nextwind].Gust = gust;
				WindRecent[nextwind].Speed = speed;
				WindRecent[nextwind].Timestamp = ts;
				nextwind = (nextwind + 1) % MaxWindRecent;

				windspeeds[nextwindvalue] = calGust;
				windbears[nextwindvalue] = calBearing;
				nextwindvalue = (nextwindvalue + 1) % maxwindvalues;
			}
		}

		public void DoUV(double value, DateTime timestamp)
		{
			UV = cumulus.Calib.UV.Calibrate(value);
			if (UV < 0)
				UV = 0;
			if (UV > 16)
				UV = 16;

			if (UV > HiLoToday.HighUv)
			{
				HiLoToday.HighUv = UV;
				HiLoToday.HighUvTime = timestamp;
			}

			HaveReadData = true;
		}

		public void DoSolarRad(int value, DateTime timestamp)
		{
			try
			{
				SolarRad = (int) Math.Round(cumulus.Calib.Solar.Calibrate(value));
			}
			catch
			{
				SolarRad = 0;
			}
			if (SolarRad < 0)
				SolarRad = 0;

			if (SolarRad > HiLoToday.HighSolar)
			{
				HiLoToday.HighSolar = SolarRad;
				HiLoToday.HighSolarTime = timestamp;
			}

			if (!cumulus.SolarOptions.UseBlakeLarsen)
			{
				IsSunny = (SolarRad > (CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100)) && (SolarRad >= cumulus.SolarOptions.SolarMinimum);
			}
			HaveReadData = true;
		}

		protected void DoSunHours(double hrs)
		{
			if (StartOfDaySunHourCounter < -9998)
			{
				cumulus.LogWarningMessage("No start of day sun counter. Start counting from now");
				StartOfDaySunHourCounter = hrs;
			}

			// Has the counter reset to a value less than we were expecting. Or has it changed by some infeasibly large value?
			if (hrs < SunHourCounter || Math.Abs(hrs - SunHourCounter) > 20)
			{
				// counter reset
				cumulus.LogMessage("Sun hour counter reset. Old value = " + SunHourCounter + "New value = " + hrs);
				StartOfDaySunHourCounter = hrs - SunshineHours;
			}
			SunHourCounter = hrs;
			SunshineHours = hrs - StartOfDaySunHourCounter;
		}

		protected void DoWetBulb(double temp, DateTime timestamp) // Supplied in CELSIUS

		{
			WetBulb = ConvertUnits.TempCToUser(temp);
			WetBulb = cumulus.Calib.WetBulb.Calibrate(WetBulb);

			// calculate RH
			double TempDry = ConvertUnits.UserTempToC(OutdoorTemperature);
			double Es = MeteoLib.SaturationVapourPressure1980(TempDry);
			double Ew = MeteoLib.SaturationVapourPressure1980(temp);
			double E = Ew - (0.00066 * (1 + 0.00115 * temp) * (TempDry - temp) * 1013);
			int hum = (int) (100 * (E / Es));
			DoOutdoorHumidity(hum, timestamp);
			// calculate DP
			// Calculate DewPoint

			// dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
			OutdoorDewpoint = ConvertUnits.TempCToUser(MeteoLib.DewPoint(TempDry, hum));

			CheckForDewpointHighLow(timestamp);
		}

		public bool IsSunny { get; set; }

		public bool HaveReadData { get; set; } = false;

		public void SetAlltime(AllTimeRec rec, double value, DateTime timestamp)
		{
			lock (alltimeIniThreadLock)
			{
				double oldvalue = rec.Val;
				DateTime oldts = rec.Ts;

				rec.Val = value;

				rec.Ts = timestamp;

				WriteAlltimeIniFile();

				AlltimeRecordTimestamp = timestamp;

				// add an entry to the log. date/time/value/item/old date/old time/old value
				// dates in ISO format, times always have a colon. Example:
				// 2010-02-24 05:19 -7.6 "Lowest temperature" 2009-02-09 04:50 -6.5
				var sb = new StringBuilder("New all-time record: New time = ", 100);
				sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", rec.Ts));
				sb.Append(", new value = ");
				sb.Append(string.Format("{0,7:0.000}", value));
				sb.Append(" \"");
				sb.Append(rec.Desc);
				sb.Append("\" prev time = ");
				sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", oldts));
				sb.Append(", prev value = ");
				sb.Append(string.Format("{0,7:0.000}", oldvalue));

				cumulus.LogMessage(sb.ToString());

				sb.Append(Environment.NewLine);
				File.AppendAllText(cumulus.Alltimelogfile, sb.ToString());
			}

			cumulus.NewRecordAlarm.LastMessage = rec.Desc + " = " + string.Format("{0,7:0.000}", value);
			cumulus.NewRecordAlarm.Triggered = true;
		}

		public void SetMonthlyAlltime(AllTimeRec rec, double value, DateTime timestamp)
		{
			double oldvalue = rec.Val;
			DateTime oldts = rec.Ts;

			rec.Val = value;
			rec.Ts = timestamp;

			WriteMonthlyAlltimeIniFile();

			var sb = new StringBuilder("New monthly record: month = ", 200);
			sb.Append(timestamp.Month.ToString("D2"));
			sb.Append(": New time = ");
			sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", timestamp));
			sb.Append(", new value = ");
			sb.Append(value.ToString("F3"));
			sb.Append(" \"");
			sb.Append(rec.Desc);
			sb.Append("\" prev time = ");
			sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", oldts));
			sb.Append(", prev value = ");
			sb.Append(oldvalue.ToString("F3"));

			cumulus.LogMessage(sb.ToString());

			sb.Append(Environment.NewLine);
			File.AppendAllText(cumulus.MonthlyAlltimeLogFile, sb.ToString());
		}

		/// <summary>
		/// Returns the angle from bearing2 to bearing1, in the range -180 to +180 degrees
		/// </summary>
		/// <param name="bearing1"></param>
		/// <param name="bearing2"></param>
		/// <returns>the required angle</returns>
		private int getShortestAngle(int bearing1, int bearing2)
		{
			int diff = bearing2 - bearing1;

			if (diff >= 180)
			{
				// result is obtuse and positive, subtract 360 to go the other way
				diff -= 360;
			}
			else
			{
				if (diff <= -180)
				{
					// result is obtuse and negative, add 360 to go the other way
					diff += 360;
				}
			}
			return diff;
		}

		public int BearingRangeTo10 { get; set; }

		public int BearingRangeFrom10 { get; set; }

		public int BearingRangeTo { get; set; }

		public int BearingRangeFrom { get; set; }

		public const int maxwindvalues = 3600;

		public int[] windbears = new int[maxwindvalues];

		public int numwindvalues { get; set; }

		public double[] windspeeds = new double[maxwindvalues];

		public double[] windcounts { get; set; }

		public int nextwindvalue { get; set; }

		public double calibratedgust { get; set; }

		public int nextwind { get; set; } = 0;

		public int nextwindvec { get; set; } = 0;

		public TWindRecent[] WindRecent { get; set; }

		public TWindVec[] WindVec { get; set; }


		//private bool first_rain = true;
		private bool FirstChanceRainReset = false;
		private bool initialiseRainDayStart = true;
		private bool initialiseMidnightRain = true;
		private bool initialiseRainCounter = true;
		//private bool RainReadyToPlot = false;
		private double RainThisMonth = 0;
		private double RainThisYear = 0;
		//private bool WindChillReadyToPlot = false;
		public bool noET = false;
		private int DayResetDay = 0;
		protected bool FirstRun = false;
		public const int MaxWindRecent = 720;
		public readonly double[] WindRunHourMult = { 3.6, 1.0, 1.0, 1.0 };
		public DateTime LastDataReadTimestamp = DateTime.MinValue;
		public DateTime SavedLastDataReadTimestamp = DateTime.MinValue;
		// Create arrays with 9 entries, 0 = VP2, 1-8 = WLL TxIds
		public int DavisTotalPacketsReceived = 0;
		public int[] DavisTotalPacketsMissed = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		public int[] DavisNumberOfResynchs = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		public int[] DavisMaxInARow = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		public int[] DavisNumCRCerrors = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		public int[] DavisReceptionPct = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		public int[] DavisTxRssi = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
		public string DavisFirmwareVersion = "???";
		public string GW1000FirmwareVersion = "???";
		public string EcowittCameraUrl = string.Empty;
		public string EcowittVideoUrl = string.Empty;

		public static Dictionary<string, byte> SensorReception { get; set; }

		//private bool manualftp;

		public void WriteYesterdayFile(DateTime logdate)
		{
			cumulus.LogMessage("Writing yesterday.ini");
			var hourInc = cumulus.GetHourInc();

			IniFile ini = new IniFile(cumulus.YesterdayFile);

			ini.SetValue("General", "Date", logdate.AddHours(hourInc));
			// Wind
			ini.SetValue("Wind", "Speed", HiLoYest.HighWind);
			ini.SetValue("Wind", "SpTime", HiLoYest.HighWindTime.ToString("HH:mm"));
			ini.SetValue("Wind", "Gust", HiLoYest.HighGust);
			ini.SetValue("Wind", "Time", HiLoYest.HighGustTime.ToString("HH:mm"));
			ini.SetValue("Wind", "Bearing", HiLoYest.HighGustBearing);
			ini.SetValue("Wind", "Direction", CompassPoint(HiLoYest.HighGustBearing));
			ini.SetValue("Wind", "Windrun", YesterdayWindRun);
			ini.SetValue("Wind", "DominantWindBearing", YestDominantWindBearing);
			// Temperature
			ini.SetValue("Temp", "Low", HiLoYest.LowTemp);
			ini.SetValue("Temp", "LTime", HiLoYest.LowTempTime.ToString("HH:mm"));
			ini.SetValue("Temp", "High", HiLoYest.HighTemp);
			ini.SetValue("Temp", "HTime", HiLoYest.HighTempTime.ToString("HH:mm"));
			ini.SetValue("Temp", "ChillHours", YestChillHours);
			ini.SetValue("Temp", "HeatingDegreeDays", YestHeatingDegreeDays);
			ini.SetValue("Temp", "CoolingDegreeDays", YestCoolingDegreeDays);
			ini.SetValue("Temp", "AvgTemp", YestAvgTemp);
			// Temperature midnight
			ini.SetValue("TempMidnight", "Low", HiLoYestMidnight.LowTemp);
			ini.SetValue("TempMidnight", "LTime", HiLoYestMidnight.LowTempTime.ToString("HH:mm"));
			ini.SetValue("TempMidnight", "High", HiLoYestMidnight.HighTemp);
			ini.SetValue("TempMidnight", "HTime", HiLoYestMidnight.HighTempTime.ToString("HH:mm"));
			// Pressure
			ini.SetValue("Pressure", "Low", HiLoYest.LowPress);
			ini.SetValue("Pressure", "LTime", HiLoYest.LowPressTime.ToString("HH:mm"));
			ini.SetValue("Pressure", "High", HiLoYest.HighPress);
			ini.SetValue("Pressure", "HTime", HiLoYest.HighPressTime.ToString("HH:mm"));
			// rain
			ini.SetValue("Rain", "High", HiLoYest.HighRainRate);
			ini.SetValue("Rain", "HTime", HiLoYest.HighRainRateTime.ToString("HH:mm"));
			ini.SetValue("Rain", "HourlyHigh", HiLoYest.HighHourlyRain);
			ini.SetValue("Rain", "HHourlyTime", HiLoYest.HighHourlyRainTime.ToString("HH:mm"));
			ini.SetValue("Rain", "High24h", HiLoYest.HighRain24h);
			ini.SetValue("Rain", "High24hTime", HiLoYest.HighRain24hTime.ToString("HH:mm"));
			ini.SetValue("Rain", "RG11Yesterday", RG11RainYesterday);
			// humidity
			ini.SetValue("Humidity", "Low", HiLoYest.LowHumidity);
			ini.SetValue("Humidity", "High", HiLoYest.HighHumidity);
			ini.SetValue("Humidity", "LTime", HiLoYest.LowHumidityTime.ToString("HH:mm"));
			ini.SetValue("Humidity", "HTime", HiLoYest.HighHumidityTime.ToString("HH:mm"));
			// Solar
			ini.SetValue("Solar", "SunshineHours", YestSunshineHours);
			// heat index
			ini.SetValue("HeatIndex", "High", HiLoYest.HighHeatIndex);
			ini.SetValue("HeatIndex", "HTime", HiLoYest.HighHeatIndexTime.ToString("HH:mm"));
			// App temp
			ini.SetValue("AppTemp", "Low", HiLoYest.LowAppTemp);
			ini.SetValue("AppTemp", "LTime", HiLoYest.LowAppTempTime.ToString("HH:mm"));
			ini.SetValue("AppTemp", "High", HiLoYest.HighAppTemp);
			ini.SetValue("AppTemp", "HTime", HiLoYest.HighAppTempTime.ToString("HH:mm"));
			// wind chill
			ini.SetValue("WindChill", "Low", HiLoYest.LowWindChill);
			ini.SetValue("WindChill", "LTime", HiLoYest.LowWindChillTime.ToString("HH:mm"));
			// Dewpoint
			ini.SetValue("Dewpoint", "Low", HiLoYest.LowDewPoint);
			ini.SetValue("Dewpoint", "LTime", HiLoYest.LowDewPointTime.ToString("HH:mm"));
			ini.SetValue("Dewpoint", "High", HiLoYest.HighDewPoint);
			ini.SetValue("Dewpoint", "HTime", HiLoYest.HighDewPointTime.ToString("HH:mm"));
			// Solar
			ini.SetValue("Solar", "HighSolarRad", HiLoYest.HighSolar);
			ini.SetValue("Solar", "HighSolarRadTime", HiLoYest.HighSolarTime.ToString("HH:mm"));
			ini.SetValue("Solar", "HighUV", HiLoYest.HighUv);
			ini.SetValue("Solar", "HighUVTime", HiLoYest.HighUvTime.ToString("HH:mm"));
			// Feels like
			ini.SetValue("FeelsLike", "Low", HiLoYest.LowFeelsLike);
			ini.SetValue("FeelsLike", "LTime", HiLoYest.LowFeelsLikeTime.ToString("HH:mm"));
			ini.SetValue("FeelsLike", "High", HiLoYest.HighFeelsLike);
			ini.SetValue("FeelsLike", "HTime", HiLoYest.HighFeelsLikeTime.ToString("HH:mm"));
			// Humidex
			ini.SetValue("Humidex", "High", HiLoYest.HighHumidex);
			ini.SetValue("Humidex", "HTime", HiLoYest.HighHumidexTime.ToString("HH:mm"));

			ini.Flush();

			cumulus.LogMessage("Written yesterday.ini");
		}

		public void ReadYesterdayFile()
		{
			//var hourInc = cumulus.GetHourInc();

			IniFile ini = new IniFile(cumulus.YesterdayFile);

			// Wind
			HiLoYest.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			HiLoYest.HighWindTime = ini.GetValue("Wind", "SpTime", DateTime.MinValue);
			HiLoYest.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			HiLoYest.HighGustTime = ini.GetValue("Wind", "Time", DateTime.MinValue);
			HiLoYest.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);

			YesterdayWindRun = ini.GetValue("Wind", "Windrun", 0.0);
			YestDominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			// Temperature
			HiLoYest.LowTemp = ini.GetValue("Temp", "Low", 0.0);
			HiLoYest.LowTempTime = ini.GetValue("Temp", "LTime", DateTime.MinValue);
			HiLoYest.HighTemp = ini.GetValue("Temp", "High", 0.0);
			HiLoYest.HighTempTime = ini.GetValue("Temp", "HTime", DateTime.MinValue);
			YestChillHours = ini.GetValue("Temp", "ChillHours", -1.0);
			YestHeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			YestCoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			YestAvgTemp = ini.GetValue("Temp", "AvgTemp", 0.0);
			HiLoYest.TempRange = HiLoYest.HighTemp - HiLoYest.LowTemp;
			// Temperature midnight
			HiLoYestMidnight.LowTemp = ini.GetValue("TempMidnight", "Low", 0.0);
			HiLoYestMidnight.LowTempTime = ini.GetValue("TempMidnight", "LTime", DateTime.MinValue);
			HiLoYestMidnight.HighTemp = ini.GetValue("TempMidnight", "High", 0.0);
			HiLoYestMidnight.HighTempTime = ini.GetValue("TempMidnight", "HTime", DateTime.MinValue);
			// Pressure
			HiLoYest.LowPress = ini.GetValue("Pressure", "Low", 0.0);
			HiLoYest.LowPressTime = ini.GetValue("Pressure", "LTime", DateTime.MinValue);
			HiLoYest.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoYest.HighPressTime = ini.GetValue("Pressure", "HTime", DateTime.MinValue);
			// rain
			HiLoYest.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			HiLoYest.HighRainRateTime = ini.GetValue("Rain", "HTime", DateTime.MinValue);
			HiLoYest.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoYest.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", DateTime.MinValue);
			HiLoYest.HighRain24h = ini.GetValue("Rain", "High24h", 0.0);
			HiLoYest.HighRain24hTime = ini.GetValue("Rain", "High24hTime", DateTime.MinValue);
			RG11RainYesterday = ini.GetValue("Rain", "RG11Yesterday", 0.0);
			// humidity
			HiLoYest.LowHumidity = ini.GetValue("Humidity", "Low", 0);
			HiLoYest.HighHumidity = ini.GetValue("Humidity", "High", 0);
			HiLoYest.LowHumidityTime = ini.GetValue("Humidity", "LTime", DateTime.MinValue);
			HiLoYest.HighHumidityTime = ini.GetValue("Humidity", "HTime", DateTime.MinValue);
			// Solar
			YestSunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			// heat index
			HiLoYest.HighHeatIndex = ini.GetValue("HeatIndex", "High", 0.0);
			HiLoYest.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", DateTime.MinValue);
			// App temp
			HiLoYest.LowAppTemp = ini.GetValue("AppTemp", "Low", 0.0);
			HiLoYest.LowAppTempTime = ini.GetValue("AppTemp", "LTime", DateTime.MinValue);
			HiLoYest.HighAppTemp = ini.GetValue("AppTemp", "High", 0.0);
			HiLoYest.HighAppTempTime = ini.GetValue("AppTemp", "HTime", DateTime.MinValue);
			// wind chill
			HiLoYest.LowWindChill = ini.GetValue("WindChill", "Low", 0.0);
			HiLoYest.LowWindChillTime = ini.GetValue("WindChill", "LTime", DateTime.MinValue);
			// Dewpoint
			HiLoYest.LowDewPoint = ini.GetValue("Dewpoint", "Low", 0.0);
			HiLoYest.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", DateTime.MinValue);
			HiLoYest.HighDewPoint = ini.GetValue("Dewpoint", "High", 0.0);
			HiLoYest.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", DateTime.MinValue);
			// Solar
			HiLoYest.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0);
			HiLoYest.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", DateTime.MinValue);
			HiLoYest.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			HiLoYest.HighUvTime = ini.GetValue("Solar", "HighUVTime", DateTime.MinValue);
			// Feels like
			HiLoYest.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 0.0);
			HiLoYest.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", DateTime.MinValue);
			HiLoYest.HighFeelsLike = ini.GetValue("FeelsLike", "High", 0.0);
			HiLoYest.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", DateTime.MinValue);
			// Humidex
			HiLoYest.HighHumidex = ini.GetValue("Humidex", "High", 0.0);
			HiLoYest.HighHumidexTime = ini.GetValue("Humidex", "HTime", DateTime.MinValue);
		}

		public void DayReset(DateTime timestamp)
		{
			int drday = timestamp.Day;
			DateTime yesterday = timestamp.AddDays(-1);
			cumulus.LogMessage("=== Day reset, today = " + drday);
			if (drday != DayResetDay)
			{
				cumulus.LogMessage("=== Day reset for " + yesterday.Date);

				int day = timestamp.Day;
				int month = timestamp.Month;
				DayResetDay = drday;

				// any last updates?
				// subtract 1 minute to keep within the previous met day
				DoTrendValues(timestamp, true);

				if (cumulus.MySqlSettings.CustomRollover.Enabled)
				{
					cumulus.CustomMysqlRolloverTimerTick();
				}

				if (cumulus.CustomHttpRolloverEnabled)
				{
					cumulus.CustomHttpRolloverUpdate();
				}

				cumulus.DoCustomDailyLogs(timestamp);

				// First save today"s extremes
				DoDayfile(timestamp);
				cumulus.LogMessage("Raincounter = " + RainCounter + " Raindaystart = " + RainCounterDayStart);

				// Calculate yesterday"s rain, allowing for the multiplier -
				// raintotal && raindaystart are not calibrated
				RainYesterday = (RainCounter - RainCounterDayStart) * cumulus.Calib.Rain.Mult;
				cumulus.LogMessage("Rainyesterday (calibrated) set to " + RainYesterday);

				//AddRecentDailyData(timestamp.AddDays(-1), RainYesterday, (cumulus.RolloverHour == 0 ? SunshineHours : SunshineToMidnight), HiLoToday.LowTemp, HiLoToday.HighTemp, YestAvgTemp);
				//RemoveOldRecentDailyData();

				int rdthresh1000;
				if (cumulus.RainDayThreshold > 0)
				{
					rdthresh1000 = Convert.ToInt32(cumulus.RainDayThreshold * 1000.0);
				}
				else
				// default
				{
					if (cumulus.Units.Rain == 0)
					{
						rdthresh1000 = 200; // 0.2mm *1000
					}
					else
					{
						rdthresh1000 = 10; // 0.01in *1000
					}
				}

				// set up rain yesterday * 1000 for comparison
				int ryest1000 = Convert.ToInt32(RainYesterday * 1000.0);

				cumulus.LogMessage("RainDayThreshold = " + cumulus.RainDayThreshold);
				cumulus.LogMessage("rdt1000=" + rdthresh1000 + " ry1000=" + ryest1000);

				if (ryest1000 >= rdthresh1000)
				{
					// It rained yesterday
					cumulus.LogMessage("Yesterday was a rain day");
					ConsecutiveRainDays++;
					ConsecutiveDryDays = 0;
					cumulus.LogMessage("Consecutive rain days = " + ConsecutiveRainDays);
					// check for highs
					if (ConsecutiveRainDays > ThisMonth.LongestWetPeriod.Val)
					{
						ThisMonth.LongestWetPeriod.Val = ConsecutiveRainDays;
						ThisMonth.LongestWetPeriod.Ts = yesterday;
						WriteMonthIniFile();
					}

					if (ConsecutiveRainDays > ThisYear.LongestWetPeriod.Val)
					{
						ThisYear.LongestWetPeriod.Val = ConsecutiveRainDays;
						ThisYear.LongestWetPeriod.Ts = yesterday;
						WriteYearIniFile();
					}

					if (ConsecutiveRainDays > AllTime.LongestWetPeriod.Val)
						SetAlltime(AllTime.LongestWetPeriod, ConsecutiveRainDays, yesterday);

					CheckMonthlyAlltime("LongestWetPeriod", ConsecutiveRainDays, true, yesterday);
				}
				else
				{
					// It didn't rain yesterday
					cumulus.LogMessage("Yesterday was a dry day");
					ConsecutiveDryDays++;
					ConsecutiveRainDays = 0;
					cumulus.LogMessage("Consecutive dry days = " + ConsecutiveDryDays);

					// check for highs
					if (ConsecutiveDryDays > ThisMonth.LongestDryPeriod.Val)
					{
						ThisMonth.LongestDryPeriod.Val = ConsecutiveDryDays;
						ThisMonth.LongestDryPeriod.Ts = yesterday;
						WriteMonthIniFile();
					}

					if (ConsecutiveDryDays > ThisYear.LongestDryPeriod.Val)
					{
						ThisYear.LongestDryPeriod.Val = ConsecutiveDryDays;
						ThisYear.LongestDryPeriod.Ts = yesterday;
						WriteYearIniFile();
					}

					if (ConsecutiveDryDays > AllTime.LongestDryPeriod.Val)
						SetAlltime(AllTime.LongestDryPeriod, ConsecutiveDryDays, yesterday);

					CheckMonthlyAlltime("LongestDryPeriod", ConsecutiveDryDays, true, yesterday);
				}

				// offset high temp today timestamp to allow for 0900 roll-over
				int hr;
				int mn;
				DateTime ts;
				try
				{
					hr = HiLoToday.HighTempTime.Hour;
					mn = HiLoToday.HighTempTime.Minute;
					ts = timestamp.Date + new TimeSpan(hr, mn, 0);

					if (hr >= cumulus.RolloverHour)
						// time is between roll-over hour && midnight
						// so subtract a day
						ts = ts.AddDays(-1);
				}
				catch
				{
					ts = timestamp.AddDays(-1);
				}

				if (HiLoToday.HighTemp < AllTime.LowMaxTemp.Val)
				{
					SetAlltime(AllTime.LowMaxTemp, HiLoToday.HighTemp, ts);
				}

				CheckMonthlyAlltime("LowMaxTemp", HiLoToday.HighTemp, false, ts);

				if (HiLoToday.HighTemp < ThisMonth.LowMaxTemp.Val)
				{
					ThisMonth.LowMaxTemp.Val = HiLoToday.HighTemp;
					try
					{
						hr = HiLoToday.HighTempTime.Hour;
						mn = HiLoToday.HighTempTime.Minute;
						ThisMonth.LowMaxTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between roll-over hour && midnight
							// so subtract a day
							ThisMonth.LowMaxTemp.Ts = ThisMonth.LowMaxTemp.Ts.AddDays(-1);
					}
					catch
					{
						ThisMonth.LowMaxTemp.Ts = timestamp.AddDays(-1);
					}

					WriteMonthIniFile();
				}

				if (HiLoToday.HighTemp < ThisYear.LowMaxTemp.Val)
				{
					ThisYear.LowMaxTemp.Val = HiLoToday.HighTemp;
					try
					{
						hr = HiLoToday.HighTempTime.Hour;
						mn = HiLoToday.HighTempTime.Minute;
						ThisYear.LowMaxTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between roll-over hour && midnight
							// so subtract a day
							ThisYear.LowMaxTemp.Ts = ThisYear.LowMaxTemp.Ts.AddDays(-1);
					}
					catch
					{
						ThisYear.LowMaxTemp.Ts = timestamp.AddDays(-1);
					}

					WriteYearIniFile();
				}

				// offset low temp today timestamp to allow for 0900 roll-over
				try
				{
					hr = HiLoToday.LowTempTime.Hour;
					mn = HiLoToday.LowTempTime.Minute;
					ts = timestamp.Date + new TimeSpan(hr, mn, 0);

					if (hr >= cumulus.RolloverHour)
						// time is between roll-over hour && midnight
						// so subtract a day
						ts = ts.AddDays(-1);
				}
				catch
				{
					ts = timestamp.AddDays(-1);
				}

				if (HiLoToday.LowTemp > AllTime.HighMinTemp.Val)
				{
					SetAlltime(AllTime.HighMinTemp, HiLoToday.LowTemp, ts);
				}

				CheckMonthlyAlltime("HighMinTemp", HiLoToday.LowTemp, true, ts);

				if (HiLoToday.LowTemp > ThisMonth.HighMinTemp.Val)
				{
					ThisMonth.HighMinTemp.Val = HiLoToday.LowTemp;
					try
					{
						hr = HiLoToday.LowTempTime.Hour;
						mn = HiLoToday.LowTempTime.Minute;
						ThisMonth.HighMinTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between roll-over hour && midnight
							// so subtract a day
							ThisMonth.HighMinTemp.Ts = ThisMonth.HighMinTemp.Ts.AddDays(-1);
					}
					catch
					{
						ThisMonth.HighMinTemp.Ts = timestamp.AddDays(-1);
					}
					WriteMonthIniFile();
				}

				if (HiLoToday.LowTemp > ThisYear.HighMinTemp.Val)
				{
					ThisYear.HighMinTemp.Val = HiLoToday.LowTemp;
					try
					{
						hr = HiLoToday.LowTempTime.Hour;
						mn = HiLoToday.LowTempTime.Minute;
						ThisYear.HighMinTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between roll-over hour && midnight
							// so subtract a day
							ThisYear.HighMinTemp.Ts = ThisYear.HighMinTemp.Ts.AddDays(-1);
					}
					catch
					{
						ThisYear.HighMinTemp.Ts = timestamp.AddDays(-1);
					}
					WriteYearIniFile();
				}

				// check temp range for highs && lows
				if (HiLoToday.TempRange > AllTime.HighDailyTempRange.Val)
					SetAlltime(AllTime.HighDailyTempRange, HiLoToday.TempRange, yesterday);

				if (HiLoToday.TempRange < AllTime.LowDailyTempRange.Val)
					SetAlltime(AllTime.LowDailyTempRange, HiLoToday.TempRange, yesterday);

				CheckMonthlyAlltime("HighDailyTempRange", HiLoToday.TempRange, true, yesterday);
				CheckMonthlyAlltime("LowDailyTempRange", HiLoToday.TempRange, false, yesterday);

				if (HiLoToday.TempRange > ThisMonth.HighDailyTempRange.Val)
				{
					ThisMonth.HighDailyTempRange.Val = HiLoToday.TempRange;
					ThisMonth.HighDailyTempRange.Ts = yesterday;
					WriteMonthIniFile();
				}

				if (HiLoToday.TempRange < ThisMonth.LowDailyTempRange.Val)
				{
					ThisMonth.LowDailyTempRange.Val = HiLoToday.TempRange;
					ThisMonth.LowDailyTempRange.Ts = yesterday;
					WriteMonthIniFile();
				}

				if (HiLoToday.TempRange > ThisYear.HighDailyTempRange.Val)
				{
					ThisYear.HighDailyTempRange.Val = HiLoToday.TempRange;
					ThisYear.HighDailyTempRange.Ts = yesterday;
					WriteYearIniFile();
				}

				if (HiLoToday.TempRange < ThisYear.LowDailyTempRange.Val)
				{
					ThisYear.LowDailyTempRange.Val = HiLoToday.TempRange;
					ThisYear.LowDailyTempRange.Ts = yesterday;
					WriteYearIniFile();
				}

				RG11RainYesterday = RG11RainToday;
				RG11RainToday = 0;

				if (day == 1)
				{
					// new month starting
					cumulus.LogMessage(" New month starting - " + month);

					CopyMonthIniFile(timestamp.AddDays(-1));

					RainThisMonth = 0;

					ThisMonth.HighGust.Val = calibratedgust;
					ThisMonth.HighWind.Val = WindAverage;
					ThisMonth.HighTemp.Val = OutdoorTemperature;
					ThisMonth.LowTemp.Val = OutdoorTemperature;
					ThisMonth.HighAppTemp.Val = ApparentTemperature;
					ThisMonth.LowAppTemp.Val = ApparentTemperature;
					ThisMonth.HighFeelsLike.Val = FeelsLike;
					ThisMonth.LowFeelsLike.Val = FeelsLike;
					ThisMonth.HighHumidex.Val = Humidex;
					ThisMonth.HighPress.Val = Pressure;
					ThisMonth.LowPress.Val = Pressure;
					ThisMonth.HighRainRate.Val = RainRate;
					ThisMonth.HourlyRain.Val = RainLastHour;
					ThisMonth.HighRain24Hours.Val = RainLast24Hour;
					ThisMonth.DailyRain.Val = Cumulus.DefaultHiVal;
					ThisMonth.HighHumidity.Val = OutdoorHumidity;
					ThisMonth.LowHumidity.Val = OutdoorHumidity;
					ThisMonth.HighHeatIndex.Val = HeatIndex;
					ThisMonth.LowChill.Val = WindChill;
					ThisMonth.HighMinTemp.Val = Cumulus.DefaultHiVal;
					ThisMonth.LowMaxTemp.Val = Cumulus.DefaultLoVal;
					ThisMonth.HighDewPoint.Val = OutdoorDewpoint;
					ThisMonth.LowDewPoint.Val = OutdoorDewpoint;
					ThisMonth.HighWindRun.Val = Cumulus.DefaultHiVal;
					ThisMonth.LongestDryPeriod.Val = 0;
					ThisMonth.LongestWetPeriod.Val = 0;
					ThisMonth.HighDailyTempRange.Val = Cumulus.DefaultHiVal;
					ThisMonth.LowDailyTempRange.Val = Cumulus.DefaultLoVal;

					// this month highs && lows - timestamps
					ThisMonth.HighGust.Ts = timestamp;
					ThisMonth.HighWind.Ts = timestamp;
					ThisMonth.HighTemp.Ts = timestamp;
					ThisMonth.LowTemp.Ts = timestamp;
					ThisMonth.HighAppTemp.Ts = timestamp;
					ThisMonth.LowAppTemp.Ts = timestamp;
					ThisMonth.HighFeelsLike.Ts = timestamp;
					ThisMonth.LowFeelsLike.Ts = timestamp;
					ThisMonth.HighHumidex.Ts = timestamp;
					ThisMonth.HighPress.Ts = timestamp;
					ThisMonth.LowPress.Ts = timestamp;
					ThisMonth.HighRainRate.Ts = timestamp;
					ThisMonth.HourlyRain.Ts = timestamp;
					ThisMonth.HighRain24Hours.Ts = timestamp;
					ThisMonth.DailyRain.Ts = timestamp;
					ThisMonth.HighHumidity.Ts = timestamp;
					ThisMonth.LowHumidity.Ts = timestamp;
					ThisMonth.HighHeatIndex.Ts = timestamp;
					ThisMonth.LowChill.Ts = timestamp;
					ThisMonth.HighMinTemp.Ts = timestamp;
					ThisMonth.LowMaxTemp.Ts = timestamp;
					ThisMonth.HighDewPoint.Ts = timestamp;
					ThisMonth.LowDewPoint.Ts = timestamp;
					ThisMonth.HighWindRun.Ts = timestamp;
					ThisMonth.LongestDryPeriod.Ts = timestamp;
					ThisMonth.LongestWetPeriod.Ts = timestamp;
					ThisMonth.LowDailyTempRange.Ts = timestamp;
					ThisMonth.HighDailyTempRange.Ts = timestamp;
				}
				else
					RainThisMonth += RainYesterday;

				if ((day == 1) && (month == 1))
				{
					// new year starting
					cumulus.LogMessage(" New year starting");

					CopyYearIniFile(timestamp.AddDays(-1));

					ThisYear.HighGust.Val = calibratedgust;
					ThisYear.HighWind.Val = WindAverage;
					ThisYear.HighTemp.Val = OutdoorTemperature;
					ThisYear.LowTemp.Val = OutdoorTemperature;
					ThisYear.HighAppTemp.Val = ApparentTemperature;
					ThisYear.LowAppTemp.Val = ApparentTemperature;
					ThisYear.HighFeelsLike.Val = FeelsLike;
					ThisYear.LowFeelsLike.Val = FeelsLike;
					ThisYear.HighHumidex.Val = Humidex;
					ThisYear.HighPress.Val = Pressure;
					ThisYear.LowPress.Val = Pressure;
					ThisYear.HighRainRate.Val = RainRate;
					ThisYear.HourlyRain.Val = RainLastHour;
					ThisYear.HighRain24Hours.Val = RainLast24Hour;
					ThisYear.DailyRain.Val = Cumulus.DefaultHiVal;
					ThisYear.MonthlyRain.Val = Cumulus.DefaultHiVal;
					ThisYear.HighHumidity.Val = OutdoorHumidity;
					ThisYear.LowHumidity.Val = OutdoorHumidity;
					ThisYear.HighHeatIndex.Val = HeatIndex;
					ThisYear.LowChill.Val = WindChill;
					ThisYear.HighMinTemp.Val = Cumulus.DefaultHiVal;
					ThisYear.LowMaxTemp.Val = Cumulus.DefaultLoVal;
					ThisYear.HighDewPoint.Val = OutdoorDewpoint;
					ThisYear.LowDewPoint.Val = OutdoorDewpoint;
					ThisYear.HighWindRun.Val = Cumulus.DefaultHiVal;
					ThisYear.LongestDryPeriod.Val = 0;
					ThisYear.LongestWetPeriod.Val = 0;
					ThisYear.HighDailyTempRange.Val = Cumulus.DefaultHiVal;
					ThisYear.LowDailyTempRange.Val = Cumulus.DefaultLoVal;

					// this Year highs && lows - timestamps
					ThisYear.HighGust.Ts = timestamp;
					ThisYear.HighWind.Ts = timestamp;
					ThisYear.HighTemp.Ts = timestamp;
					ThisYear.LowTemp.Ts = timestamp;
					ThisYear.HighAppTemp.Ts = timestamp;
					ThisYear.LowAppTemp.Ts = timestamp;
					ThisYear.HighFeelsLike.Ts = timestamp;
					ThisYear.LowFeelsLike.Ts = timestamp;
					ThisYear.HighHumidex.Ts = timestamp;
					ThisYear.HighPress.Ts = timestamp;
					ThisYear.LowPress.Ts = timestamp;
					ThisYear.HighRainRate.Ts = timestamp;
					ThisYear.HourlyRain.Ts = timestamp;
					ThisYear.HighRain24Hours.Ts = timestamp;
					ThisYear.DailyRain.Ts = timestamp;
					ThisYear.MonthlyRain.Ts = timestamp;
					ThisYear.HighHumidity.Ts = timestamp;
					ThisYear.LowHumidity.Ts = timestamp;
					ThisYear.HighHeatIndex.Ts = timestamp;
					ThisYear.LowChill.Ts = timestamp;
					ThisYear.HighMinTemp.Ts = timestamp;
					ThisYear.LowMaxTemp.Ts = timestamp;
					ThisYear.HighDewPoint.Ts = timestamp;
					ThisYear.LowDewPoint.Ts = timestamp;
					ThisYear.HighWindRun.Ts = timestamp;
					ThisYear.LongestDryPeriod.Ts = timestamp;
					ThisYear.LongestWetPeriod.Ts = timestamp;
					ThisYear.HighDailyTempRange.Ts = timestamp;
					ThisYear.LowDailyTempRange.Ts = timestamp;

					// reset the ET annual total for Davis WLL stations only
					// because we mimic the annual total and it is not reset like VP2 stations
					if (cumulus.StationType == StationTypes.WLL || cumulus.StationOptions.CalculatedET)
					{
						cumulus.LogMessage(" Resetting Annual ET total");
						AnnualETTotal = 0;
					}
				}

				if ((day == 1) && (month == cumulus.RainSeasonStart))
				{
					// new year starting
					cumulus.LogMessage(" New rain season starting");
					RainThisYear = 0;
				}
				else
				{
					RainThisYear += RainYesterday;
				}

				if ((day == 1) && (month == cumulus.ChillHourSeasonStart))
				{
					// new year starting
					cumulus.LogMessage(" Chill hour season starting");
					ChillHours = 0;
				}

				if ((day == 1) && (month == cumulus.GrowingYearStarts))
				{
					cumulus.LogMessage(" New growing degree day season starting");
					GrowingDegreeDaysThisYear1 = 0;
					GrowingDegreeDaysThisYear2 = 0;
				}

				GrowingDegreeDaysThisYear1 += MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(HiLoToday.HighTemp), ConvertUnits.UserTempToC(HiLoToday.LowTemp), ConvertUnits.UserTempToC(cumulus.GrowingBase1), cumulus.GrowingCap30C);
				GrowingDegreeDaysThisYear2 += MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(HiLoToday.HighTemp), ConvertUnits.UserTempToC(HiLoToday.LowTemp), ConvertUnits.UserTempToC(cumulus.GrowingBase2), cumulus.GrowingCap30C);

				// Now reset all values to the current or default ones
				// We may be doing a roll-over from the first logger entry,
				// && as we do the roll-over before processing the entry, the
				// current items may not be set up.

				RainCounterDayStart = RainCounter;
				cumulus.LogMessage("Raindaystart set to " + RainCounterDayStart);

				RainToday = 0;

				TempTotalToday = OutdoorTemperature;
				tempsamplestoday = 1;

				// Copy today"s high wind settings to yesterday
				HiLoYest.HighWind = HiLoToday.HighWind;
				HiLoYest.HighWindTime = HiLoToday.HighWindTime;
				HiLoYest.HighGust = HiLoToday.HighGust;
				HiLoYest.HighGustTime = HiLoToday.HighGustTime;
				HiLoYest.HighGustBearing = HiLoToday.HighGustBearing;

				// Reset today"s high wind settings
				HiLoToday.HighGust = calibratedgust;
				HiLoToday.HighGustBearing = Bearing;
				HiLoToday.HighWind = WindAverage;

				HiLoToday.HighWindTime = timestamp;
				HiLoToday.HighGustTime = timestamp;

				// Copy today"s high temp settings to yesterday
				HiLoYest.HighTemp = HiLoToday.HighTemp;
				HiLoYest.HighTempTime = HiLoToday.HighTempTime;
				// Reset today"s high temp settings
				HiLoToday.HighTemp = OutdoorTemperature;
				HiLoToday.HighTempTime = timestamp;

				// Copy today"s low temp settings to yesterday
				HiLoYest.LowTemp = HiLoToday.LowTemp;
				HiLoYest.LowTempTime = HiLoToday.LowTempTime;
				// Reset today"s low temp settings
				HiLoToday.LowTemp = OutdoorTemperature;
				HiLoToday.LowTempTime = timestamp;

				HiLoYest.TempRange = HiLoToday.TempRange;
				HiLoToday.TempRange = 0;

				// Copy today"s low pressure settings to yesterday
				HiLoYest.LowPress = HiLoToday.LowPress;
				HiLoYest.LowPressTime = HiLoToday.LowPressTime;
				// Reset today"s low pressure settings
				HiLoToday.LowPress = Pressure;
				HiLoToday.LowPressTime = timestamp;

				// Copy today"s high pressure settings to yesterday
				HiLoYest.HighPress = HiLoToday.HighPress;
				HiLoYest.HighPressTime = HiLoToday.HighPressTime;
				// Reset today"s high pressure settings
				HiLoToday.HighPress = Pressure;
				HiLoToday.HighPressTime = timestamp;

				// Copy today"s high rain rate settings to yesterday
				HiLoYest.HighRainRate = HiLoToday.HighRainRate;
				HiLoYest.HighRainRateTime = HiLoToday.HighRainRateTime;
				// Reset today"s high rain rate settings
				HiLoToday.HighRainRate = RainRate;
				HiLoToday.HighRainRateTime = timestamp;

				HiLoYest.HighHourlyRain = HiLoToday.HighHourlyRain;
				HiLoYest.HighHourlyRainTime = HiLoToday.HighHourlyRainTime;
				HiLoToday.HighHourlyRain = RainLastHour;
				HiLoToday.HighHourlyRainTime = timestamp;

				HiLoYest.HighRain24h = HiLoToday.HighRain24h;
				HiLoYest.HighRain24hTime = HiLoToday.HighRain24hTime;
				HiLoToday.HighRain24h = RainLast24Hour;
				HiLoToday.HighRain24hTime = timestamp;

				YesterdayWindRun = WindRunToday;
				WindRunToday = 0;

				YestDominantWindBearing = DominantWindBearing;

				DominantWindBearing = 0;
				DominantWindBearingX = 0;
				DominantWindBearingY = 0;
				DominantWindBearingMinutes = 0;

				YestChillHours = ChillHours;
				YestHeatingDegreeDays = HeatingDegreeDays;
				YestCoolingDegreeDays = CoolingDegreeDays;
				HeatingDegreeDays = 0;
				CoolingDegreeDays = 0;

				// reset startofdayET value
				StartofdayET = AnnualETTotal;
				cumulus.LogMessage("StartofdayET set to " + StartofdayET);
				ET = 0;

				// Humidity
				HiLoYest.LowHumidity = HiLoToday.LowHumidity;
				HiLoYest.LowHumidityTime = HiLoToday.LowHumidityTime;
				HiLoToday.LowHumidity = OutdoorHumidity;
				HiLoToday.LowHumidityTime = timestamp;

				HiLoYest.HighHumidity = HiLoToday.HighHumidity;
				HiLoYest.HighHumidityTime = HiLoToday.HighHumidityTime;
				HiLoToday.HighHumidity = OutdoorHumidity;
				HiLoToday.HighHumidityTime = timestamp;

				// heat index
				HiLoYest.HighHeatIndex = HiLoToday.HighHeatIndex;
				HiLoYest.HighHeatIndexTime = HiLoToday.HighHeatIndexTime;
				HiLoToday.HighHeatIndex = HeatIndex;
				HiLoToday.HighHeatIndexTime = timestamp;

				// App temp
				HiLoYest.HighAppTemp = HiLoToday.HighAppTemp;
				HiLoYest.HighAppTempTime = HiLoToday.HighAppTempTime;
				HiLoToday.HighAppTemp = ApparentTemperature;
				HiLoToday.HighAppTempTime = timestamp;

				HiLoYest.LowAppTemp = HiLoToday.LowAppTemp;
				HiLoYest.LowAppTempTime = HiLoToday.LowAppTempTime;
				HiLoToday.LowAppTemp = ApparentTemperature;
				HiLoToday.LowAppTempTime = timestamp;

				// wind chill
				HiLoYest.LowWindChill = HiLoToday.LowWindChill;
				HiLoYest.LowWindChillTime = HiLoToday.LowWindChillTime;
				HiLoToday.LowWindChill = WindChill;
				HiLoToday.LowWindChillTime = timestamp;

				// dew point
				HiLoYest.HighDewPoint = HiLoToday.HighDewPoint;
				HiLoYest.HighDewPointTime = HiLoToday.HighDewPointTime;
				HiLoToday.HighDewPoint = OutdoorDewpoint;
				HiLoToday.HighDewPointTime = timestamp;

				HiLoYest.LowDewPoint = HiLoToday.LowDewPoint;
				HiLoYest.LowDewPointTime = HiLoToday.LowDewPointTime;
				HiLoToday.LowDewPoint = OutdoorDewpoint;
				HiLoToday.LowDewPointTime = timestamp;

				// solar
				HiLoYest.HighSolar = HiLoToday.HighSolar;
				HiLoYest.HighSolarTime = HiLoToday.HighSolarTime;
				HiLoToday.HighSolar = SolarRad;
				HiLoToday.HighSolarTime = timestamp;

				HiLoYest.HighUv = HiLoToday.HighUv;
				HiLoYest.HighUvTime = HiLoToday.HighUvTime;
				HiLoToday.HighUv = UV;
				HiLoToday.HighUvTime = timestamp;

				// Feels like
				HiLoYest.HighFeelsLike = HiLoToday.HighFeelsLike;
				HiLoYest.HighFeelsLikeTime = HiLoToday.HighFeelsLikeTime;
				HiLoToday.HighFeelsLike = FeelsLike;
				HiLoToday.HighFeelsLikeTime = timestamp;

				HiLoYest.LowFeelsLike = HiLoToday.LowFeelsLike;
				HiLoYest.LowFeelsLikeTime = HiLoToday.LowFeelsLikeTime;
				HiLoToday.LowFeelsLike = FeelsLike;
				HiLoToday.LowFeelsLikeTime = timestamp;

				// Humidex
				HiLoYest.HighHumidex = HiLoToday.HighHumidex;
				HiLoYest.HighHumidexTime = HiLoToday.HighHumidexTime;
				HiLoToday.HighHumidex = Humidex;
				HiLoToday.HighHumidexTime = timestamp;

				// Lightning
				LightningStrikesToday = 0;

				// Save the current values in case of program restart
				WriteTodayFile(timestamp, true);
				WriteYesterdayFile(timestamp);

				if (cumulus.NOAAconf.Create)
				{
					try
					{
						var noaa = new NOAAReports(cumulus, this);

						DateTime noaats = timestamp.AddDays(-1);

						// do monthly NOAA report
						cumulus.LogMessage("Creating NOAA monthly report for " + noaats.ToLongDateString());

						_ = noaa.GenerateNoaaMonthReport(noaats.Year, noaats.Month);
						cumulus.NOAAconf.LatestMonthReport = noaats.ToString(cumulus.NOAAconf.MonthFile);

						// do yearly NOAA report
						cumulus.LogMessage("Creating NOAA yearly report");
						_ = noaa.GenerateNoaaYearReport(noaats.Year);
						cumulus.NOAAconf.LatestYearReport = noaats.ToString(cumulus.NOAAconf.YearFile);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("Error creating NOAA reports: " + ex.Message);
					}
				}

				// Do we need to upload NOAA reports on next FTP?
				cumulus.NOAAconf.NeedFtp = cumulus.NOAAconf.AutoFtp;
				cumulus.NOAAconf.NeedCopy = cumulus.NOAAconf.AutoCopy;

				if (cumulus.NOAAconf.NeedFtp || cumulus.NOAAconf.NeedCopy)
				{
					cumulus.LogMessage("NOAA reports will be uploaded at next web update");
				}

				// Do the Daily graph data files
				CreateEodGraphDataFiles();
				CreateDailyGraphDataFiles();
				cumulus.LogMessage("If required the daily graph data files will be uploaded at next web update");

				// Do the End of day Extra files
				// This will set a flag to transfer on next FTP if required
				cumulus.DoExtraEndOfDayFiles();

				if (cumulus.EODfilesNeedFTP)
				{
					cumulus.LogMessage("Extra files will be uploaded at next web update");
				}

				if (!string.IsNullOrEmpty(cumulus.DailyProgram))
				{
					try
					{
						// Prepare the process to run
						var args = string.Empty;

						if (!string.IsNullOrEmpty(cumulus.DailyParams))
						{
							var parser = new TokenParser(cumulus.TokenParserOnToken);
							parser.InputText = cumulus.DailyParams;
							args = parser.ToStringFromString();
						}
						cumulus.LogMessage("Executing daily program: " + cumulus.DailyProgram + " params: " + args);
						Utils.RunExternalTask(cumulus.DailyProgram, args, false);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage("Error executing external program: " + ex.Message);
					}
				}

				CurrentDay = timestamp.Day;
				CurrentMonth = timestamp.Month;
				CurrentYear = timestamp.Year;
				cumulus.LogMessage("=== Day reset complete");
				cumulus.LogMessage("Now recording data for day=" + CurrentDay + " month=" + CurrentMonth + " year=" + CurrentYear);
			}
			else
			{
				cumulus.LogMessage("=== Day reset already done on day " + drday);
			}
		}

		private void CopyMonthIniFile(DateTime ts)
		{
			string year = ts.Year.ToString();
			string month = ts.Month.ToString("D2");
			string savedFile = cumulus.Datapath + "month" + year + month + ".ini";
			cumulus.LogMessage("Saving month.ini file as " + savedFile);
			try
			{
				File.Copy(cumulus.MonthIniFile, savedFile);
			}
			catch (Exception)
			{
				// ignore - probably just that it has already been copied
			}
		}

		private void CopyYearIniFile(DateTime ts)
		{
			string year = ts.Year.ToString();
			string savedFile = cumulus.Datapath + "year" + year + ".ini";
			cumulus.LogMessage("Saving year.ini file as " + savedFile);
			try
			{
				File.Copy(cumulus.YearIniFile, savedFile);
			}
			catch (Exception)
			{
				// ignore - probably just that it has already been copied
			}
		}

		private async void DoDayfile(DateTime timestamp)
		{
			// Writes an entry to the daily extreme log file. Fields are comma-separated.
			// 0   Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  Highest wind gust
			// 2  Bearing of highest wind gust
			// 3  Time of highest wind gust
			// 4  Minimum temperature
			// 5  Time of minimum temperature
			// 6  Maximum temperature
			// 7  Time of maximum temperature
			// 8  Minimum sea level pressure
			// 9  Time of minimum pressure
			// 10  Maximum sea level pressure
			// 11  Time of maximum pressure
			// 12  Maximum rainfall rate
			// 13  Time of maximum rainfall rate
			// 14  Total rainfall for the day
			// 15  Average temperature for the day
			// 16  Total wind run
			// 17  Highest average wind speed
			// 18  Time of highest average wind speed
			// 19  Lowest humidity
			// 20  Time of lowest humidity
			// 21  Highest humidity
			// 22  Time of highest humidity
			// 23  Total evapotranspiration
			// 24  Total hours of sunshine
			// 25  High heat index
			// 26  Time of high heat index
			// 27  High apparent temperature
			// 28  Time of high apparent temperature
			// 29  Low apparent temperature
			// 30  Time of low apparent temperature
			// 31  High hourly rain
			// 32  Time of high hourly rain
			// 33  Low wind chill
			// 34  Time of low wind chill
			// 35  High dew point
			// 36  Time of high dew point
			// 37  Low dew point
			// 38  Time of low dew point
			// 39  Dominant wind bearing
			// 40  Heating degree days
			// 41  Cooling degree days
			// 42  High solar radiation
			// 43  Time of high solar radiation
			// 44  High UV Index
			// 45  Time of high UV Index
			// 46  High Feels like
			// 47  Time of high feels like
			// 48  Low feels like
			// 49  Time of low feels like
			// 50  High Humidex
			// 51  Time of high Humidex
			// 52  Chill hours
			// 53  Max Rain 24 hours
			// 54  Max Rain 24 hours Time

			double AvgTemp;
			if (tempsamplestoday > 0)
				AvgTemp = TempTotalToday / tempsamplestoday;
			else
				AvgTemp = 0;

			// save the value for yesterday
			YestAvgTemp = AvgTemp;

			string datestring = timestamp.AddDays(-1).ToString("dd/MM/yy");
			// NB this string is just for logging, the dayfile update code is further down
			var strb = new StringBuilder(300);
			strb.Append(datestring + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighGust.ToString(cumulus.WindFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighGustBearing + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighGustTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowTempTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighTempTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowPress.ToString(cumulus.PressFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowPressTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighPress.ToString(cumulus.PressFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighPressTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighRainRate.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighRainRateTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(RainToday.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
			strb.Append(AvgTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(WindRunToday.ToString("F1") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighWind.ToString(cumulus.WindAvgFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighWindTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowHumidity + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowHumidityTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHumidity + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHumidityTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(ET.ToString(cumulus.ETFormat) + cumulus.ListSeparator);
			if (cumulus.RolloverHour == 0)
			{
				// use existing current sunshine hour count
				strb.Append(SunshineHours.ToString(cumulus.SunFormat) + cumulus.ListSeparator);
			}
			else
			{
				// for non-midnight roll-over, use new item
				strb.Append(SunshineToMidnight.ToString(cumulus.SunFormat) + cumulus.ListSeparator);
			}
			strb.Append(HiLoToday.HighHeatIndex.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHeatIndexTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighAppTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighAppTempTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowAppTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowAppTempTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHourlyRain.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHourlyRainTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowWindChill.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowWindChillTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighDewPoint.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighDewPointTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowDewPoint.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowDewPointTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(DominantWindBearing + cumulus.ListSeparator);
			strb.Append(HeatingDegreeDays.ToString("F1") + cumulus.ListSeparator);
			strb.Append(CoolingDegreeDays.ToString("F1") + cumulus.ListSeparator);
			strb.Append((int) HiLoToday.HighSolar + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighSolarTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighUv.ToString(cumulus.UVFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighUvTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighFeelsLike.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighFeelsLikeTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowFeelsLike.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowFeelsLikeTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHumidex.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHumidexTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(ChillHours.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighRain24h.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighRain24hTime.ToString("HH:mm"));

			cumulus.LogMessage("Dayfile.txt entry:");
			cumulus.LogMessage(strb.ToString());

			var success = false;
			var retries = Cumulus.LogFileRetries;
			do
			{
				try
				{
					cumulus.LogMessage("Dayfile.txt opened for writing");

					if ((HiLoToday.HighTemp < -400) || (HiLoToday.LowTemp > 900))
					{
						cumulus.LogErrorMessage("***Error: Daily values are still at default at end of day");
						cumulus.LogErrorMessage("Data not logged to dayfile.txt");
						return;
					}
					else
					{
						cumulus.LogMessage("Writing entry to dayfile.txt");

						using (FileStream fs = new FileStream(cumulus.DayFileName, FileMode.Append, FileAccess.Write, FileShare.Read))
						using (StreamWriter file = new StreamWriter(fs))
						{
							await file.WriteLineAsync(strb.ToString());
							file.Close();
							fs.Close();

							success = true;

							cumulus.LogMessage($"Dayfile log entry for {datestring} written");
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error writing to dayfile.txt: " + ex.Message);
					retries--;
					await System.Threading.Tasks.Task.Delay(250);
				}
			} while (!success && retries >= 0);

			// Add a new record to the in memory dayfile data
			var tim = timestamp.AddDays(-1);
			var newRec = new dayfilerec()
			{
				Date = new DateTime(tim.Year, tim.Month, tim.Day),
				HighGust = HiLoToday.HighGust,
				HighGustBearing = HiLoToday.HighGustBearing,
				HighGustTime = HiLoToday.HighGustTime,
				LowTemp = HiLoToday.LowTemp,
				LowTempTime = HiLoToday.LowTempTime,
				HighTemp = HiLoToday.HighTemp,
				HighTempTime = HiLoToday.HighTempTime,
				LowPress = HiLoToday.LowPress,
				LowPressTime = HiLoToday.LowPressTime,
				HighPress = HiLoToday.HighPress,
				HighPressTime = HiLoToday.HighPressTime,
				HighRainRate = HiLoToday.HighRainRate,
				HighRainRateTime = HiLoToday.HighRainRateTime,
				TotalRain = RainToday,
				AvgTemp = AvgTemp,
				WindRun = WindRunToday,
				HighAvgWind = HiLoToday.HighWind,
				HighAvgWindTime = HiLoToday.HighWindTime,
				LowHumidity = HiLoToday.LowHumidity,
				LowHumidityTime = HiLoToday.LowHumidityTime,
				HighHumidity = HiLoToday.HighHumidity,
				HighHumidityTime = HiLoToday.HighHumidityTime,
				ET = ET,
				SunShineHours = cumulus.RolloverHour == 0 ? SunshineHours : SunshineToMidnight,
				HighHeatIndex = HiLoToday.HighHeatIndex,
				HighHeatIndexTime = HiLoToday.HighHeatIndexTime,
				HighAppTemp = HiLoToday.HighAppTemp,
				HighAppTempTime = HiLoToday.HighAppTempTime,
				LowAppTemp = HiLoToday.LowAppTemp,
				LowAppTempTime = HiLoToday.LowAppTempTime,
				HighHourlyRain = HiLoToday.HighHourlyRain,
				HighHourlyRainTime = HiLoToday.HighHourlyRainTime,
				LowWindChill = HiLoToday.LowWindChill,
				LowWindChillTime = HiLoToday.LowWindChillTime,
				HighDewPoint = HiLoToday.HighDewPoint,
				HighDewPointTime = HiLoToday.HighDewPointTime,
				LowDewPoint = HiLoToday.LowDewPoint,
				LowDewPointTime = HiLoToday.LowDewPointTime,
				DominantWindBearing = DominantWindBearing,
				HeatingDegreeDays = HeatingDegreeDays,
				CoolingDegreeDays = CoolingDegreeDays,
				HighSolar = (int) HiLoToday.HighSolar,
				HighSolarTime = HiLoToday.HighSolarTime,
				HighUv = HiLoToday.HighUv,
				HighUvTime = HiLoToday.HighUvTime,
				HighFeelsLike = HiLoToday.HighFeelsLike,
				HighFeelsLikeTime = HiLoToday.HighFeelsLikeTime,
				LowFeelsLike = HiLoToday.LowFeelsLike,
				LowFeelsLikeTime = HiLoToday.LowFeelsLikeTime,
				HighHumidex = HiLoToday.HighHumidex,
				HighHumidexTime = HiLoToday.HighHumidexTime,
				ChillHours = ChillHours,
				HighRain24h = HiLoToday.HighRain24h,
				HighRain24hTime = HiLoToday.HighRain24hTime
			};

			DayFile.Add(newRec);



			if (cumulus.MySqlSettings.Dayfile.Enabled)
			{
				var InvC = new CultureInfo("");

				StringBuilder queryString = new StringBuilder(cumulus.DayfileTable.StartOfInsert, 1024);
				queryString.Append(" Values('");
				queryString.Append(timestamp.AddDays(-1).ToString("yy-MM-dd") + "',");
				queryString.Append(HiLoToday.HighGust.ToString(cumulus.WindFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighGustBearing + ",");
				queryString.Append(HiLoToday.HighGustTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowTemp.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowTempTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighTemp.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighTempTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowPress.ToString(cumulus.PressFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowPressTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighPress.ToString(cumulus.PressFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighPressTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighRainRate.ToString(cumulus.RainFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighRainRateTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(RainToday.ToString(cumulus.RainFormat, InvC) + ",");
				queryString.Append(AvgTemp.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(WindRunToday.ToString("F1", InvC) + ",");
				queryString.Append(HiLoToday.HighWind.ToString(cumulus.WindAvgFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighWindTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowHumidity + ",");
				queryString.Append(HiLoToday.LowHumidityTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighHumidity + ",");
				queryString.Append(HiLoToday.HighHumidityTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(ET.ToString(cumulus.ETFormat, InvC) + ",");
				queryString.Append((cumulus.RolloverHour == 0 ? SunshineHours.ToString(cumulus.SunFormat, InvC) : SunshineToMidnight.ToString(cumulus.SunFormat, InvC)) + ",");
				queryString.Append(HiLoToday.HighHeatIndex.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighHeatIndexTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighAppTemp.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighAppTempTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowAppTemp.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowAppTempTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighHourlyRain.ToString(cumulus.RainFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighHourlyRainTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowWindChill.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowWindChillTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighDewPoint.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighDewPointTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowDewPoint.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowDewPointTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(DominantWindBearing + ",");
				queryString.Append(HeatingDegreeDays.ToString("F1", InvC) + ",");
				queryString.Append(CoolingDegreeDays.ToString("F1", InvC) + ",");
				queryString.Append((int) HiLoToday.HighSolar + ",");
				queryString.Append(HiLoToday.HighSolarTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighUv.ToString(cumulus.UVFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighUvTime.ToString("\\'HH:mm\\'") + ",'");
				queryString.Append(CompassPoint(HiLoToday.HighGustBearing) + "','");
				queryString.Append(CompassPoint(DominantWindBearing) + "',");
				queryString.Append(HiLoToday.HighFeelsLike.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighFeelsLikeTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowFeelsLike.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowFeelsLikeTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.HighHumidex.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighHumidexTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(ChillHours.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighRain24h.ToString(cumulus.RainFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighRain24hTime.ToString("\\'HH:mm\\'"));

				queryString.Append(')');

				// run the query async so we do not block the main EOD processing
				_ = cumulus.MySqlCommandAsync(queryString.ToString(), "MySQL Dayfile");
			}
		}

		/// <summary>
		///  Calculate checksum of data received from serial port
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected int checksum(List<int> data)
		{
			int sum = 0;

			for (int i = 0; i < data.Count - 1; i++)
			{
				sum += data[i];
			}

			return sum % 256;
		}

		protected int BCDchartoint(int c)
		{
			return ((c / 16) * 10) + (c % 16);
		}

		public string CompassPoint(int bearing)
		{
			return bearing == 0 ? "-" : cumulus.Trans.compassp[(((bearing * 100) + 1125) % 36000) / 2250];
		}

		public void StartLoop()
		{
			try
			{
				mainThread = new Thread(Start) { IsBackground = true };
				mainThread.Start();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("An error occurred during the station start-up: " + ex.Message);
			}
		}

		public virtual void getAndProcessHistoryData()
		{
		}

		public virtual void startReadingHistoryData()
		{
		}

		/// <summary>
		/// Calculates average bearing for last 10 minutes
		/// </summary>
		/// <returns></returns>
		public int CalcAverageBearing()
		{
			double totalwindX = Last10MinWindList.Sum(o => o.gustX);
			double totalwindY = Last10MinWindList.Sum(o => o.gustY);

			if (totalwindX == 0)
			{
				return 0;
			}

			int avgbear = calcavgbear(totalwindX, totalwindY);

			if (avgbear == 0)
			{
				avgbear = 360;
			}

			return avgbear;
		}

		private int calcavgbear(double x, double y)
		{
			var avg = 90 - (int) (RadToDeg(Math.Atan2(y, x)));
			if (avg < 0)
			{
				avg = 360 + avg;
			}

			return avg;
		}

		public void AddRecentDataWithAq(DateTime timestamp, double windAverage, double recentMaxGust, double windLatest, int bearing, int avgBearing, double outsidetemp,
			double windChill, double dewpoint, double heatIndex, int humidity, double pressure, double rainToday, double solarRad, double uv, double rainCounter, double feelslike, double humidex,
			double appTemp, double insideTemp, int insideHum, double solarMax, double rainrate)
		{
			double pm2p5 = -1;
			double pm10 = -1;
			// Check for Air Quality readings
			switch (cumulus.StationOptions.PrimaryAqSensor)
			{
				case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
					if (cumulus.airLinkDataOut != null)
					{
						pm2p5 = cumulus.airLinkDataOut.pm2p5;
						pm10 = cumulus.airLinkDataOut.pm10;
					}
					break;
				case (int) Cumulus.PrimaryAqSensor.AirLinkIndoor:
					if (cumulus.airLinkDataIn != null)
					{
						pm2p5 = cumulus.airLinkDataIn.pm2p5;
						pm10 = cumulus.airLinkDataIn.pm10;
					}
					break;
				case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
					pm2p5 = AirQuality1;
					break;
				case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
					pm2p5 = AirQuality2;
					break;
				case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
					pm2p5 = AirQuality3;
					break;
				case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
					pm2p5 = AirQuality3;
					break;
				case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
					pm2p5 = CO2_pm2p5;
					pm10 = CO2_pm10;
					break;

				default: // Not enabled, use invalid values
					break;
			}

			AddRecentDataEntry(timestamp, windAverage, recentMaxGust, windLatest, bearing, avgBearing, outsidetemp, windChill, dewpoint, heatIndex, humidity, pressure, rainToday, solarRad, uv, rainCounter, feelslike, humidex, appTemp, insideTemp, insideHum, solarMax, rainrate, pm2p5, pm10);
		}


		public void UpdateRecentDataAqEntry(DateTime ts, double pm2p5, double pm10)
		{
			try
			{
				RecentDataDb.Execute("update RecentData set Pm2p5=?, Pm10=? where Timestamp=?", pm2p5, pm10, ts);
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"UpdateGraphDataAqEntry: Exception caught: {e.Message}");
			}
		}


		/// <summary>
		/// Adds a new entry to the list of wind readings from the last 10 minutes
		/// </summary>
		/// <param name="ts"></param>
		public void AddLast10MinWindEntry(DateTime ts, double windgust, double windspeed, double Xvec, double Yvec)
		{
			Last10MinWind last10minwind = new Last10MinWind(ts, windgust, windspeed, Xvec, Yvec);
			Last10MinWindList.Add(last10minwind);
		}

		public double DegToRad(int deg)
		{
			return deg * Math.PI / 180;
		}

		public double RadToDeg(double rad)
		{
			return rad * 180 / Math.PI;
		}

		/*
		public double getStartOfDayRainCounter(DateTime timestamp)
		{
			// TODO:
			return -1;
		}
		*/


		/// <summary>
		/// Removes entries from Last10MinWindList older than ts - 10 minutes
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public void RemoveOld10MinWindData(DateTime ts)
		{
			DateTime tenminutesago = ts.AddMinutes(-10);

			if (Last10MinWindList.Count > 0)
			{
				// there are entries to consider
				while ((Last10MinWindList.Count > 0) && (Last10MinWindList.First().timestamp < tenminutesago))
				{
					// the oldest entry is older than 10 mins ago, delete it
					Last10MinWindList.RemoveAt(0);
				}
			}
		}

		public void DoTrendValues(DateTime ts, bool rollover = false)
		{
			double trendval;
			List<RecentData> retVals;
			var recTs = ts;

			// if this is the special case of rollover processing, we want the High today record to on the previous day at 23:59 or 08:59
			if (rollover)
			{
				recTs = recTs.AddMinutes(-1);
			}

			// Do 3 hour trends
			try
			{
				retVals = RecentDataDb.Query<RecentData>("select OutsideTemp, Pressure from RecentData where Timestamp >=? order by Timestamp limit 1", ts.AddHours(-3));

				if (retVals.Count != 1)
				{
					temptrendval = 0;
					presstrendval = 0;
				}
				else
				{
					if (TempReadyToPlot)
					{
						// calculate and display the temp trend
						temptrendval = (OutdoorTemperature - retVals[0].OutsideTemp) / 3.0F;
						cumulus.TempChangeAlarm.CheckAlarm(temptrendval);
					}

					if (PressReadyToPlot)
					{
						// calculate and display the pressure trend
						presstrendval = (Pressure - retVals[0].Pressure) / 3.0;
						cumulus.PressChangeAlarm.CheckAlarm(presstrendval);
					}

					// Convert for display
					//trendval = ConvertUnits.PressMBToUser(presstrendval);
				}
			}
			catch
			{
				temptrendval = 0;
				presstrendval = 0;
			}

			try
			{
				// Do 1 hour trends
				retVals = RecentDataDb.Query<RecentData>("select OutsideTemp, raincounter from RecentData where Timestamp >=? order by Timestamp limit 1", ts.AddHours(-1));

				if (retVals.Count != 1)
				{
					TempChangeLastHour = 0;
					RainLastHour = 0;
				}
				else
				{
					// Calculate Temperature change in the last hour
					TempChangeLastHour = OutdoorTemperature - retVals[0].OutsideTemp;


					// calculate and display rainfall in last hour
					if (RainCounter < retVals[0].raincounter)
					{
						// rain total is not available or has gone down, assume it was reset to zero, just use zero
						RainLastHour = 0;
					}
					else
					{
						// normal case
						trendval = RainCounter - retVals[0].raincounter;

						// Round value as some values may have been read from log file and already rounded
						trendval = Math.Round(trendval, cumulus.RainDPlaces);

						var tempRainLastHour = trendval * cumulus.Calib.Rain.Mult;

						if (tempRainLastHour > cumulus.Spike.MaxHourlyRain)
						{
							// ignore
							cumulus.LogSpikeRemoval("Max hourly rainfall spike value exceed");
							lastSpikeRemoval = DateTime.Now;
							cumulus.SpikeAlarm.LastMessage = $"Max hourly rainfall greater than spike value - Value={tempRainLastHour:F1}";
							cumulus.SpikeAlarm.Triggered = true;
						}
						else
						{
							RainLastHour = tempRainLastHour;

							if (RainLastHour > AllTime.HourlyRain.Val)
								SetAlltime(AllTime.HourlyRain, RainLastHour, ts);

							CheckMonthlyAlltime("HourlyRain", RainLastHour, true, ts);

							if (RainLastHour > HiLoToday.HighHourlyRain)
							{
								HiLoToday.HighHourlyRain = RainLastHour;
								HiLoToday.HighHourlyRainTime = recTs;
								WriteTodayFile(ts, false);
							}

							if (RainLastHour > ThisMonth.HourlyRain.Val)
							{
								ThisMonth.HourlyRain.Val = RainLastHour;
								ThisMonth.HourlyRain.Ts = ts;
								WriteMonthIniFile();
							}

							if (RainLastHour > ThisYear.HourlyRain.Val)
							{
								ThisYear.HourlyRain.Val = RainLastHour;
								ThisYear.HourlyRain.Ts = ts;
								WriteYearIniFile();
							}
						}
					}
				}
			}
			catch
			{
				TempChangeLastHour = 0;
				RainLastHour = 0;
			}


			if (calculaterainrate)
			{
				// Station doesn't supply rain rate, calculate one based on rain in last 5 minutes

				try
				{
					retVals = RecentDataDb.Query<RecentData>("select raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", ts.AddMinutes(-5.5));

					if (retVals.Count != 1 || RainCounter < retVals[0].raincounter)
					{
						RainRate = 0;
					}
					else
					{
						var raindiff = Math.Round(RainCounter - retVals[0].raincounter, cumulus.RainDPlaces);
						//cumulus.LogMessage("raindiff = " + raindiff);

						var timediffhours = 1.0 / 12.0;

						// Scale the counter values
						var tempRainRate = Math.Round((double) (raindiff / timediffhours) * cumulus.Calib.Rain.Mult, cumulus.RainDPlaces);

						if (tempRainRate < 0)
						{
							tempRainRate = 0;
						}

						if (ConvertUnits.UserRainToMM(tempRainRate) > cumulus.Spike.MaxRainRate)
						{
							// ignore
							cumulus.LogSpikeRemoval("Max rainfall rate spike value exceed");
							cumulus.LogSpikeRemoval($"Rate value = {ConvertUnits.UserRainToMM(tempRainRate):F2} SpikeMaxRainRate = {cumulus.Spike.MaxRainRate:F2}");
							lastSpikeRemoval = DateTime.Now;
							cumulus.SpikeAlarm.LastMessage = $"Max rainfall rate greater than spike value - Value={tempRainRate:F1}";
							cumulus.SpikeAlarm.Triggered = true;

						}
						else
						{
							RainRate = tempRainRate;

							if (RainRate > AllTime.HighRainRate.Val)
								SetAlltime(AllTime.HighRainRate, RainRate, ts);

							CheckMonthlyAlltime("HighRainRate", RainRate, true, ts);

							cumulus.HighRainRateAlarm.CheckAlarm(RainRate);

							if (RainRate > HiLoToday.HighRainRate)
							{
								HiLoToday.HighRainRate = RainRate;
								HiLoToday.HighRainRateTime = recTs;
								WriteTodayFile(ts, false);
							}

							if (RainRate > ThisMonth.HighRainRate.Val)
							{
								ThisMonth.HighRainRate.Val = RainRate;
								ThisMonth.HighRainRate.Ts = ts;
								WriteMonthIniFile();
							}

							if (RainRate > ThisYear.HighRainRate.Val)
							{
								ThisYear.HighRainRate.Val = RainRate;
								ThisYear.HighRainRate.Ts = ts;
								WriteYearIniFile();
							}
						}
					}
				}
				catch
				{
					RainRate = 0;
				}
			}


			// calculate and display rainfall in last 24 hour
			try
			{
				retVals = RecentDataDb.Query<RecentData>("select raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", ts.AddDays(-1));

				if (retVals.Count != 1 || RainCounter < retVals[0].raincounter)
				{
					RainLast24Hour = 0;
				}
				else
				{
					trendval = Math.Round(RainCounter - retVals[0].raincounter, cumulus.RainDPlaces);

					if (trendval < 0)
					{
						trendval = 0;
					}

					RainLast24Hour = trendval * cumulus.Calib.Rain.Mult;

					if (RainLast24Hour > HiLoToday.HighRain24h)
					{
						HiLoToday.HighRain24h = RainLast24Hour;
						HiLoToday.HighRain24hTime = recTs;
						WriteTodayFile(recTs, false);
					}

					if (RainLast24Hour > AllTime.HighRain24Hours.Val)
					{
						SetAlltime(AllTime.HighRain24Hours, RainLast24Hour, ts);
					}

					CheckMonthlyAlltime("HighRain24Hours", RainLast24Hour, true, ts);

					if (RainLast24Hour > ThisMonth.HighRain24Hours.Val)
					{
						ThisMonth.HighRain24Hours.Val = RainLast24Hour;
						ThisMonth.HighRain24Hours.Ts = ts;
						WriteMonthIniFile();
					}

					if (RainLast24Hour > ThisYear.HighRain24Hours.Val)
					{
						ThisYear.HighRain24Hours.Val = RainLast24Hour;
						ThisYear.HighRain24Hours.Ts = ts;
						WriteYearIniFile();
					}
				}
			}
			catch
			{
				// Unable to retrieve rain counter from 24 hours ago
				RainLast24Hour = 0;
			}
		}

		/*
		private double ConvertTempTrendToDisplay(double trendval)
		{
			double num;

			if (cumulus.TempUnit == 1)
			{
				num = (trendval*1.8F);
			}
			else
			{
				// C
				num = trendval;
			}

			return num;
		}
		*/

		public void CalculateDominantWindBearing(int averageBearing, double averageSpeed, int minutes)
		{
			DominantWindBearingX += (minutes * averageSpeed * Math.Sin(DegToRad(averageBearing)));
			DominantWindBearingY += (minutes * averageSpeed * Math.Cos(DegToRad(averageBearing)));
			DominantWindBearingMinutes += minutes;

			if (DominantWindBearingX == 0)
			{
				DominantWindBearing = 0;
			}
			else
			{
				try
				{
					DominantWindBearing = calcavgbear(DominantWindBearingX, DominantWindBearingY);
					if (DominantWindBearing == 0)
					{
						DominantWindBearing = 360;
					}
				}
				catch
				{
					cumulus.LogErrorMessage("Error in dominant wind direction calculation");
				}
			}

			/*if (DominantWindBearingX < 0)
			{
				DominantWindBearing = 270 - DominantWindBearing;
			}
			else
			{
				DominantWindBearing = 90 - DominantWindBearing;
			}*/
		}

		public void DoDayResetIfNeeded()
		{
			int hourInc = cumulus.GetHourInc();

			if (cumulus.LastUpdateTime.AddHours(hourInc).Date != DateTime.Now.AddHours(hourInc).Date)
			{
				cumulus.LogMessage("Day reset required");
				DayReset(DateTime.Now);
			}

			if (cumulus.LastUpdateTime.Date != DateTime.Now.Date)
			{
				ResetMidnightRain(DateTime.Now);
				ResetSunshineHours(DateTime.Now);
				ResetMidnightTemperatures(DateTime.Now);
			}
		}

		public int DominantWindBearing { get; set; }

		public int DominantWindBearingMinutes { get; set; }

		public double DominantWindBearingY { get; set; }

		public double DominantWindBearingX { get; set; }

		public double YesterdayWindRun { get; set; }
		public double AnnualETTotal { get; set; }
		public double StartofdayET { get; set; }

		public int ConsecutiveRainDays { get; set; }
		public int ConsecutiveDryDays { get; set; }
		public DateTime FOSensorClockTime { get; set; }
		public DateTime FOStationClockTime { get; set; }
		public DateTime FOSolarClockTime { get; set; }
		public double YestAvgTemp { get; set; }
		public double AltimeterPressure { get; set; }
		public int YestDominantWindBearing { get; set; }
		public double RainLast24Hour { get; set; }
		public string ConBatText { get; set; }
		public string ConSupplyVoltageText { get; set; }
		public string TxBatText { get; set; }

		public double YestChillHours { get; set; }
		public double YestHeatingDegreeDays { get; set; }
		public double YestCoolingDegreeDays { get; set; }
		public double TempChangeLastHour { get; set; }
		public double WetBulb { get; set; }
		public int CloudBase { get; set; }
		public double StormRain { get; set; }
		public DateTime StartOfStorm { get; set; }
		public bool SensorContactLost { get; set; }
		public bool DataStopped { get; set; }
		public DateTime DataStoppedTime { get; set; }
		public bool IsRaining { get; set; }

		public void LoadLastHoursFromDataLogs(DateTime ts)
		{
			cumulus.LogMessage("Loading last N hour data from data logs: " + ts);
			LoadRecentFromDataLogs(ts);
			LoadRecentAqFromDataLogs(ts);
			LoadWindData();
			LoadLast3HourData(ts);
			LoadRecentWindRose();
			InitialiseWind();
		}

		private void LoadRecentFromDataLogs(DateTime ts)
		{
			// Recent data goes back a week
			var datefrom = ts.AddDays(-cumulus.RecentDataDays);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;
			int numtoadd = 0;
			int numadded = 0;

			var rowsToAdd = new List<RecentData>();

			cumulus.LogMessage($"LoadRecent: Attempting to load {cumulus.RecentDataDays} days of entries to recent data list");

			// try and find the first entry in the database
			try
			{
				var start = RecentDataDb.ExecuteScalar<DateTime>("select MAX(Timestamp) from RecentData");
				if (datefrom < start)
					datefrom = start;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("LoadRecent: Error querying database for latest record - " + e.Message);
			}


			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;
					rowsToAdd.Clear();

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;

								var rec = ParseLogFileRec(line, false);

								if (rec.Date >= datefrom && entrydate <= dateto)
								{
									rowsToAdd.Add(new RecentData()
									{
										Timestamp = rec.Date,
										DewPoint = rec.OutdoorDewpoint,
										HeatIndex = rec.HeatIndex,
										Humidity = rec.OutdoorHumidity,
										OutsideTemp = rec.OutdoorTemperature,
										Pressure = rec.Pressure,
										RainToday = rec.RainToday,
										SolarRad = (int) rec.SolarRad,
										UV = rec.UV,
										WindAvgDir = rec.AvgBearing,
										WindGust = rec.RecentMaxGust,
										WindLatest = rec.WindLatest,
										WindChill = rec.WindChill,
										WindDir = rec.Bearing,
										WindSpeed = rec.WindAverage,
										raincounter = rec.Raincounter,
										FeelsLike = rec.FeelsLike,
										Humidex = rec.Humidex,
										AppTemp = rec.ApparentTemperature,
										IndoorTemp = rec.IndoorTemperature,
										IndoorHumidity = rec.IndoorHumidity,
										SolarMax = (int) rec.CurrentSolarMax,
										RainRate = rec.RainRate,
										Pm2p5 = -1,
										Pm10 = -1
									});
									++numtoadd;
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"LoadRecent: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"LoadRecent: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"LoadRecent: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}

					try
					{
						if (rowsToAdd.Count > 0)
							numadded = RecentDataDb.InsertAll(rowsToAdd, "OR IGNORE");
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"LoadRecent: Error inserting recent data into database: {e.Message}");
					}

				}

				if (entrydate >= dateto || filedate > dateto.AddMonths(1))
				{
					finished = true;
				}
				else
				{
					filedate = filedate.AddMonths(1);
					logFile = cumulus.GetLogFileName(filedate);
				}
			}
			cumulus.LogMessage($"LoadRecent: Loaded {numadded} of {numtoadd} new entries to recent database");
		}

		private void LoadRecentAqFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddDays(-cumulus.RecentDataDays);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile;
			bool finished = false;
			int updatedCount = 0;

			// try and find the first entry in the database that has a "blank" AQ entry (PM2.5 or PM10 = -1)
			try
			{
				var start = RecentDataDb.ExecuteScalar<DateTime>("select Timestamp from RecentData where Pm2p5=-1 or Pm10=-1 order by Timestamp limit 1");
				if (start == DateTime.MinValue)
					return;

				if (datefrom < start)
					datefrom = start;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("LoadRecentAqFromDataLogs: Error querying database for oldest record without AQ data - " + e.Message);
			}

			if (cumulus.StationOptions.PrimaryAqSensor < 0) return;

			cumulus.LogMessage($"LoadRecentAqFromDataLogs: Attempting to load {cumulus.RecentDataDays} days of entries to Air Quality recent data");

			if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor
				|| cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				logFile = cumulus.GetAirLinkLogFileName(filedate);
			}
			else if ((cumulus.StationOptions.PrimaryAqSensor >= (int) Cumulus.PrimaryAqSensor.Ecowitt1 && cumulus.StationOptions.PrimaryAqSensor <= (int) Cumulus.PrimaryAqSensor.Ecowitt4) ||
					cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2) // Ecowitt
			{
				logFile = cumulus.GetExtraLogFileName(filedate);
			}
			else
			{
				cumulus.LogErrorMessage($"LoadRecentAqFromDataLogs: Error - The primary AQ sensor is not set to a valid value, currently={cumulus.StationOptions.PrimaryAqSensor}");
				return;
			}

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						RecentDataDb.BeginTransaction();
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = Utils.ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate >= datefrom && entrydate <= dateto)
								{
									// entry is from required period
									double pm2p5, pm10;
									if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
									{
										// AirLink Indoor
										pm2p5 = Convert.ToDouble(st[5]);
										pm10 = Convert.ToDouble(st[10]);
									}
									else if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor)
									{
										// AirLink Outdoor
										pm2p5 = Convert.ToDouble(st[32]);
										pm10 = Convert.ToDouble(st[37]);
									}
									else if (cumulus.StationOptions.PrimaryAqSensor >= (int) Cumulus.PrimaryAqSensor.Ecowitt1 && cumulus.StationOptions.PrimaryAqSensor <= (int) Cumulus.PrimaryAqSensor.Ecowitt4)
									{
										// Ecowitt sensor 1-4 - fields 68 -> 71
										pm2p5 = Convert.ToDouble(st[67 + cumulus.StationOptions.PrimaryAqSensor]);
										pm10 = -1;
									}
									else
									{
										// Ecowitt CO2 sensor
										pm2p5 = Convert.ToDouble(st[86]);
										pm10 = Convert.ToDouble(st[88]);
									}

									//UpdateGraphDataAqEntry(entrydate, pm2p5, pm10);
									UpdateRecentDataAqEntry(entrydate, pm2p5, pm10);
									updatedCount++;
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"LoadRecentAqFromDataLogs: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 20)
								{
									cumulus.LogErrorMessage($"LoadRecentAqFromDataLogs: Too many errors reading {logFile} - aborting load of graph data");
								}
							}
						}

						RecentDataDb.Commit();
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"LoadRecentAqFromDataLogs: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
						RecentDataDb.Rollback();
					}
				}

				if (entrydate >= dateto || filedate > dateto.AddMonths(1))
				{
					finished = true;
				}
				else
				{
					filedate = filedate.AddMonths(1);
					if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor
						|| cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor) // AirLink
					{
						logFile = cumulus.GetAirLinkLogFileName(filedate);
					}
					else if ((cumulus.StationOptions.PrimaryAqSensor >= (int) Cumulus.PrimaryAqSensor.Ecowitt1
						&& cumulus.StationOptions.PrimaryAqSensor <= (int) Cumulus.PrimaryAqSensor.Ecowitt4)
						|| cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2) // Ecowitt
					{
						logFile = cumulus.GetExtraLogFileName(filedate);
					}
				}
			}
			cumulus.LogMessage($"LoadRecentAqFromDataLogs: Loaded {updatedCount} new entries to recent database");
		}

		private void LoadRecentWindRose()
		{
			// We can now just query the recent data database as it has been populated from the logs
			var datefrom = DateTime.Now.AddHours(-24);

			var result = RecentDataDb.Query<RecentData>("select WindGust, WindDir from RecentData where Timestamp >= ? order by Timestamp", datefrom);

			foreach (var rec in result)
			{
				windspeeds[nextwindvalue] = rec.WindGust;
				windbears[nextwindvalue] = rec.WindDir;
				nextwindvalue = (nextwindvalue + 1) % MaxWindRecent;
				if (numwindvalues < maxwindvalues)
				{
					numwindvalues++;
				}
			}
		}

		private void LoadLast3HourData(DateTime ts)
		{
			var datefrom = ts.AddHours(-3);
			var dateto = ts;

			cumulus.LogMessage($"LoadLast3Hour: Attempting to load 3 hour data list");

			var result = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? and Timestamp <= ? order by Timestamp", datefrom, dateto);

			// get the min and max timestamps from the recent wind array
			var minWindTs = DateTime.MaxValue;
			var maxWindTs = DateTime.MinValue;
			foreach (var rec in WindRecent)
			{
				if (rec.Timestamp < minWindTs)
					minWindTs = rec.Timestamp;
				if (rec.Timestamp > maxWindTs)
					maxWindTs = rec.Timestamp;
			}

			foreach (var rec in result)
			{
				try
				{
					if (rec.Timestamp < minWindTs || rec.Timestamp > maxWindTs)
					{
						WindRecent[nextwind].Gust = rec.WindGust;
						WindRecent[nextwind].Speed = rec.WindSpeed;
						WindRecent[nextwind].Timestamp = rec.Timestamp;
						nextwind = (nextwind + 1) % MaxWindRecent;
					}

					WindVec[nextwindvec].X = rec.WindGust * Math.Sin(DegToRad(rec.WindDir));
					WindVec[nextwindvec].Y = rec.WindGust * Math.Cos(DegToRad(rec.WindDir));
					WindVec[nextwindvec].Timestamp = rec.Timestamp;
					WindVec[nextwindvec].Bearing = Bearing; // savedBearing;
					nextwindvec = (nextwindvec + 1) % MaxWindRecent;
				}
				catch (Exception e)
				{
					cumulus.LogErrorMessage($"LoadLast3Hour: Error loading data from database : {e.Message}");
				}
			}
			cumulus.LogMessage($"LoadLast3Hour: Loaded {result.Count} entries to last 3 hour data list");
		}

		private void LoadWindData()
		{
			cumulus.LogMessage($"LoadWindData: Attempting to reload the wind speeds array");
			var result = RecentDataDb.Query<CWindRecent>("select * from CWindRecent");

			try
			{
				for (var i = 0; i < result.Count; i++)
				{
					WindRecent[i].Gust = result[i].Gust;
					WindRecent[i].Speed = result[i].Speed;
					WindRecent[i].Timestamp = result[i].Timestamp;
				}
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"LoadWindData: Error loading data from database : {e.Message}");
			}

			try
			{
				nextwind = RecentDataDb.ExecuteScalar<int>("select * from WindRecentPointer limit 1");
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"LoadWindData: Error loading pointer from database : {e.Message}");
			}

			cumulus.LogMessage($"LoadWindData: Loaded {result.Count} entries to WindRecent data list");
		}

		public void SaveWindData()
		{
			cumulus.LogMessage($"SaveWindData: Attempting to save the wind speeds array");

			RecentDataDb.BeginTransaction();

			try
			{
				// first empty the tables
				RecentDataDb.DeleteAll<CWindRecent>();
				RecentDataDb.Execute("delete from WindRecentPointer");

				// save the type array
				for (int i = 0; i < WindRecent.Length; i++)
				{
					if (WindRecent[i].Timestamp > DateTime.MinValue)
						RecentDataDb.Execute("insert or replace into CWindRecent (Timestamp,Gust,Speed) values (?,?,?)", WindRecent[i].Timestamp, WindRecent[i].Gust, WindRecent[i].Speed);
				}

				// and save the pointer
				RecentDataDb.Execute("insert into WindRecentPointer (pntr) values (?)", nextwind);

				RecentDataDb.Commit();

				cumulus.LogMessage($"SaveWindData: Saved the wind speeds array");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"SaveWindData: Error saving RecentWind to the database : {ex.Message}");
				RecentDataDb.Rollback();
			}
		}

		private static DateTime GetDateTime(DateTime date, string time)
		{
			var tim = time.Split(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator.ToCharArray()[0]);
			return new DateTime(date.Year, date.Month, date.Day, int.Parse(tim[0]), int.Parse(tim[1]), 0);
		}

		public string LoadDayFile()
		{
			int addedEntries = 0;

			string msg = string.Empty;

			cumulus.LogMessage($"LoadDayFile: Attempting to load the day file");
			if (File.Exists(cumulus.DayFileName))
			{
				int linenum = 0;
				int errorCount = 0;
				int duplicateCount = 0;

				var watch = Stopwatch.StartNew();

				// Clear the existing list
				DayFile.Clear();

				try
				{
					using (var sr = new StreamReader(cumulus.DayFileName))
					{
						do
						{
							try
							{
								// process each record in the file

								linenum++;
								string Line = sr.ReadLine();
								var newRec = ParseDayFileRec(Line);

								if (DayFile.Any(x => x.Date == newRec.Date))
								{
									cumulus.LogErrorMessage($"ERROR: Duplicate entry in dayfile for {newRec.Date:d}");
									msg += $"ERROR: Duplicate entry in dayfile for {newRec.Date:d}<br>";
									duplicateCount++;
								}

								DayFile.Add(newRec);

								addedEntries++;
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"LoadDayFile: Error at line {linenum} of {cumulus.DayFileName} : {e.Message}");
								msg += $"Error at line {linenum} of {cumulus.DayFileName}<br>";
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 20)
								{
									cumulus.LogErrorMessage($"LoadDayFile: Too many errors reading {cumulus.DayFileName} - aborting load of daily data");
									msg += "Too many errors reading dayfile, aborting<br>";
								}
							}
						} while (!(sr.EndOfStream || errorCount >= 20));
						sr.Close();
					}

					watch.Stop();
					cumulus.LogDebugMessage($"LoadDayFile: Dayfile parse = {watch.ElapsedMilliseconds} ms");

					if (duplicateCount > 0)
					{
						msg += $"Found {duplicateCount} duplicate entries, please correct your dayfile and try again";
						cumulus.LogErrorMessage($"LoadDayFile: Found {duplicateCount} duplicate entries, please correct your dayfile and try again");
						return msg;
					}
				}
				catch (Exception e)
				{
					cumulus.LogErrorMessage($"LoadDayFile: Error at line {linenum} of {cumulus.DayFileName} : {e.Message}");
					cumulus.LogMessage("Please edit the file to correct the error");
					msg += $"Error at line {linenum} of {cumulus.DayFileName} : {e.Message}<br>";
				}

				cumulus.LogMessage($"LoadDayFile: Loaded {addedEntries} entries to recent daily data list");
				msg += $"Loaded {addedEntries} entries to recent daily data list";
				return msg;
			}
			else
			{
				msg = "LoadDayFile: No Dayfile found - No entries added to recent daily data list";
				cumulus.LogErrorMessage(msg);
				return msg;
			}
		}

		// errors are caught by the caller
		public dayfilerec ParseDayFileRec(string data)
		{
			var st = new List<string>(Regex.Split(data, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
			double varDbl;
			int idx = 0;

			var rec = new dayfilerec();
			try
			{
				rec.Date = Utils.ddmmyyStrToDate(st[idx++]);
				rec.HighGust = Convert.ToDouble(st[idx++]);
				rec.HighGustBearing = Convert.ToInt32(st[idx++]);
				rec.HighGustTime = GetDateTime(rec.Date, st[idx++]);
				rec.LowTemp = Convert.ToDouble(st[idx++]);
				rec.LowTempTime = GetDateTime(rec.Date, st[idx++]);
				rec.HighTemp = Convert.ToDouble(st[idx++]);
				rec.HighTempTime = GetDateTime(rec.Date, st[idx++]);
				rec.LowPress = Convert.ToDouble(st[idx++]);
				rec.LowPressTime = GetDateTime(rec.Date, st[idx++]);
				rec.HighPress = Convert.ToDouble(st[idx++]);
				rec.HighPressTime = GetDateTime(rec.Date, st[idx++]);
				rec.HighRainRate = Convert.ToDouble(st[idx++]);
				rec.HighRainRateTime = GetDateTime(rec.Date, st[idx++]);
				rec.TotalRain = Convert.ToDouble(st[idx++]);
				rec.AvgTemp = Convert.ToDouble(st[idx++]);

				if (st.Count > idx++ && double.TryParse(st[16], out varDbl))
					rec.WindRun = varDbl;

				if (st.Count > idx++ && double.TryParse(st[17], out varDbl))
					rec.HighAvgWind = varDbl;

				if (st.Count > idx++ && st[18].Length == 5)
					rec.HighAvgWindTime = GetDateTime(rec.Date, st[18]);

				if (st.Count > idx++ && double.TryParse(st[19], out varDbl))
					rec.LowHumidity = Convert.ToInt32(varDbl);
				else
					rec.LowHumidity = (int) Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[20].Length == 5)
					rec.LowHumidityTime = GetDateTime(rec.Date, st[20]);

				if (st.Count > idx++ && double.TryParse(st[21], out varDbl))
					rec.HighHumidity = Convert.ToInt32(varDbl);
				else
					rec.HighHumidity = (int) Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[22].Length == 5)
					rec.HighHumidityTime = GetDateTime(rec.Date, st[22]);

				if (st.Count > idx++ && double.TryParse(st[23], out varDbl))
					rec.ET = varDbl;
				else
					rec.ET = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[24], out varDbl))
					rec.SunShineHours = varDbl;

				if (st.Count > idx++ && double.TryParse(st[25], out varDbl))
					rec.HighHeatIndex = varDbl;
				else
					rec.HighHeatIndex = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[26].Length == 5)
					rec.HighHeatIndexTime = GetDateTime(rec.Date, st[26]);

				if (st.Count > idx++ && double.TryParse(st[27], out varDbl))
					rec.HighAppTemp = varDbl;
				else
					rec.HighAppTemp = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[28].Length == 5)
					rec.HighAppTempTime = GetDateTime(rec.Date, st[28]);

				if (st.Count > idx++ && double.TryParse(st[29], out varDbl))
					rec.LowAppTemp = varDbl;
				else
					rec.LowAppTemp = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[30].Length == 5)
					rec.LowAppTempTime = GetDateTime(rec.Date, st[30]);

				if (st.Count > idx++ && double.TryParse(st[31], out varDbl))
					rec.HighHourlyRain = varDbl;
				else
					rec.HighHourlyRain = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[32].Length == 5)
					rec.HighHourlyRainTime = GetDateTime(rec.Date, st[32]);

				if (st.Count > idx++ && double.TryParse(st[33], out varDbl))
					rec.LowWindChill = varDbl;
				else
					rec.LowWindChill = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[34].Length == 5)
					rec.LowWindChillTime = GetDateTime(rec.Date, st[34]);

				if (st.Count > idx++ && double.TryParse(st[35], out varDbl))
					rec.HighDewPoint = varDbl;
				else
					rec.HighDewPoint = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[36].Length == 5)
					rec.HighDewPointTime = GetDateTime(rec.Date, st[36]);

				if (st.Count > idx++ && double.TryParse(st[37], out varDbl))
					rec.LowDewPoint = varDbl;
				else
					rec.LowDewPoint = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[38].Length == 5)
					rec.LowDewPointTime = GetDateTime(rec.Date, st[38]);

				if (st.Count > idx++ && double.TryParse(st[39], out varDbl))
					rec.DominantWindBearing = Convert.ToInt32(varDbl);
				else
					rec.DominantWindBearing = (int) Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[40], out varDbl))
					rec.HeatingDegreeDays = varDbl;
				else
					rec.HeatingDegreeDays = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[41], out varDbl))
					rec.CoolingDegreeDays = varDbl;
				else
					rec.CoolingDegreeDays = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[42], out varDbl))
					rec.HighSolar = Convert.ToInt32(varDbl);
				else
					rec.HighSolar = (int) Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[43].Length == 5)
					rec.HighSolarTime = GetDateTime(rec.Date, st[43]);

				if (st.Count > idx++ && double.TryParse(st[44], out varDbl))
					rec.HighUv = varDbl;
				else
					rec.HighUv = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[45].Length == 5)
					rec.HighUvTime = GetDateTime(rec.Date, st[45]);

				if (st.Count > idx++ && double.TryParse(st[46], out varDbl))
					rec.HighFeelsLike = varDbl;
				else
					rec.HighFeelsLike = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[47].Length == 5)
					rec.HighFeelsLikeTime = GetDateTime(rec.Date, st[47]);

				if (st.Count > idx++ && double.TryParse(st[48], out varDbl))
					rec.LowFeelsLike = varDbl;
				else
					rec.LowFeelsLike = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[49].Length == 5)
					rec.LowFeelsLikeTime = GetDateTime(rec.Date, st[49]);

				if (st.Count > idx++ && double.TryParse(st[50], out varDbl))
					rec.HighHumidex = varDbl;
				else
					rec.HighHumidex = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[51].Length == 5)
					rec.HighHumidexTime = GetDateTime(rec.Date, st[51]);

				if (st.Count > idx++ && double.TryParse(st[52], out varDbl))
					rec.ChillHours = varDbl;
				else
					rec.ChillHours = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[53], out varDbl))
					rec.HighRain24h = varDbl;
				else
					rec.HighRain24h = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[54].Length == 5)
					rec.HighRain24hTime = GetDateTime(rec.Date, st[54]);
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage($"ParseDayFileRec: Error at record {idx} - {ex.Message}");
				var e = new Exception($"Error at record {idx} = \"{st[idx - 1]}\" - {ex.Message}");
				throw e;
			}
			return rec;
		}

		public logfilerec ParseLogFileRec(string data, bool minMax)
		{
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

			try
			{
				var st = new List<string>(Regex.Split(data, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

				// We allow int values to have a decimal point because log files sometimes get mangled by Excel etc!
				var rec = new logfilerec()
				{
					Date = Utils.ddmmyyhhmmStrToDate(st[0], st[1]),
					OutdoorTemperature = Convert.ToDouble(st[2]),
					OutdoorHumidity = Convert.ToInt32(Convert.ToDouble(st[3])),
					OutdoorDewpoint = Convert.ToDouble(st[4]),
					WindAverage = Convert.ToDouble(st[5]),
					RecentMaxGust = Convert.ToDouble(st[6]),
					AvgBearing = Convert.ToInt32(Convert.ToDouble(st[7])),
					RainRate = Convert.ToDouble(st[8]),
					RainToday = Convert.ToDouble(st[9]),
					Pressure = Convert.ToDouble(st[10]),
					Raincounter = Convert.ToDouble(st[11]),
					IndoorTemperature = Convert.ToDouble(st[12]),
					IndoorHumidity = Convert.ToInt32(Convert.ToDouble(st[13])),
					WindLatest = Convert.ToDouble(st[14])
				};


				if (st.Count > 15 && !string.IsNullOrWhiteSpace(st[15]))
					rec.WindChill = Convert.ToDouble(st[15]);
				else
					rec.WindChill = minMax ? Cumulus.DefaultLoVal : 0.0;

				if (st.Count > 16 && !string.IsNullOrWhiteSpace(st[16]))
					rec.HeatIndex = Convert.ToDouble(st[16]);
				else
					rec.HeatIndex = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 17 && !string.IsNullOrWhiteSpace(st[17]))
					rec.UV = Convert.ToDouble(st[17]);
				else
					rec.UV = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 18 && !string.IsNullOrWhiteSpace(st[18]))
					rec.SolarRad = Convert.ToDouble(st[18]);
				else
					rec.SolarRad = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 19 && !string.IsNullOrWhiteSpace(st[19]))
					rec.ET = Convert.ToDouble(st[19]);
				else
					rec.ET = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 20 && !string.IsNullOrWhiteSpace(st[20]))
					rec.AnnualETTotal = Convert.ToDouble(st[20]);
				else
					rec.AnnualETTotal = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 21 && !string.IsNullOrWhiteSpace(st[21]))
					rec.ApparentTemperature = Convert.ToDouble(st[21]);
				else
					rec.ApparentTemperature = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 22 && !string.IsNullOrWhiteSpace(st[22]))
					rec.CurrentSolarMax = Convert.ToDouble(st[22]);
				else
					rec.CurrentSolarMax = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 23 && !string.IsNullOrWhiteSpace(st[23]))
					rec.SunshineHours = Convert.ToDouble(st[23]);
				else
					rec.SunshineHours = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 24 && !string.IsNullOrWhiteSpace(st[24]))
					rec.Bearing = Convert.ToInt32(Convert.ToDouble(st[24]));
				else
					rec.Bearing = 0;

				if (st.Count > 25 && !string.IsNullOrWhiteSpace(st[25]))
					rec.RG11RainToday = Convert.ToDouble(st[25]);
				else
					rec.RG11RainToday = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 26 && !string.IsNullOrWhiteSpace(st[26]))
					rec.RainSinceMidnight = Convert.ToDouble(st[26]);
				else
					rec.RainSinceMidnight = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 27 && !string.IsNullOrWhiteSpace(st[27]))
					rec.FeelsLike = Convert.ToDouble(st[27]);
				else
					rec.FeelsLike = minMax ? Cumulus.DefaultHiVal : 0.0;

				if (st.Count > 28 && !string.IsNullOrWhiteSpace(st[28]))
					rec.Humidex = Convert.ToDouble(st[28]);
				else
					rec.Humidex = minMax ? Cumulus.DefaultHiVal : 0.0;

				return rec;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error parsing log file record: " + ex.Message);
				cumulus.LogDataMessage("Log record: " + data);
				throw ex;
			}
		}

		internal void UpdateStatusPanel(DateTime timestamp)
		{
			LastDataReadTimestamp = timestamp;
			_ = sendWebSocketData();
		}


		internal void UpdateMQTT()
		{
			if (cumulus.MQTT.EnableDataUpdate)
			{
				MqttPublisher.UpdateMQTTfeed("DataUpdate", null);
			}
		}

		/// <summary>
		/// Returns a plus sign if the supplied number is greater than zero, otherwise empty string
		/// </summary>
		/// <param name="num">The number to be tested</param>
		/// <returns>Plus sign or empty</returns>
		/*
		private string PlusSign(double num)
		{
			return num > 0 ? "+" : "";
		}
		*/

		public void DoET(double value, DateTime timestamp)
		{
			// Value is annual total

			if (noET)
			{
				// Start of day ET value not yet set
				cumulus.LogMessage("*** First ET reading. Set startofdayET to total: " + value);
				StartofdayET = value;
				noET = false;
			}

			//if ((value == 0) && (StartofdayET > 0))
			if (Math.Round(value, 3) < Math.Round(StartofdayET, 3)) // change b3046
			{
				// ET reset
				cumulus.LogMessage(String.Format("*** ET Reset *** AnnualET: {0:0.000}, StartofdayET: {1:0.000}, StationET: {2:0.000}, CurrentET: {3:0.000}", AnnualETTotal, StartofdayET, value, ET));
				AnnualETTotal = value; // add b3046
									   // set the start of day figure so it reflects the ET
									   // so far today
				StartofdayET = AnnualETTotal - ET;
				WriteTodayFile(timestamp, false);
				cumulus.LogMessage(String.Format("New ET values. AnnualET: {0:0.000}, StartofdayET: {1:0.000}, StationET: {2:0.000}, CurrentET: {3:0.000}", AnnualETTotal, StartofdayET, value, ET));
			}
			else
			{
				AnnualETTotal = value;
			}

			ET = AnnualETTotal - StartofdayET;

			HaveReadData = true;
		}

		public void DoSoilMoisture(double value, int index)
		{
			switch (index)
			{
				case 1:
					SoilMoisture1 = (int) value;
					break;
				case 2:
					SoilMoisture2 = (int) value;
					break;
				case 3:
					SoilMoisture3 = (int) value;
					break;
				case 4:
					SoilMoisture4 = (int) value;
					break;
				case 5:
					SoilMoisture5 = (int) value;
					break;
				case 6:
					SoilMoisture6 = (int) value;
					break;
				case 7:
					SoilMoisture7 = (int) value;
					break;
				case 8:
					SoilMoisture8 = (int) value;
					break;
				case 9:
					SoilMoisture9 = (int) value;
					break;
				case 10:
					SoilMoisture10 = (int) value;
					break;
				case 11:
					SoilMoisture11 = (int) value;
					break;
				case 12:
					SoilMoisture12 = (int) value;
					break;
				case 13:
					SoilMoisture13 = (int) value;
					break;
				case 14:
					SoilMoisture14 = (int) value;
					break;
				case 15:
					SoilMoisture15 = (int) value;
					break;
				case 16:
					SoilMoisture16 = (int) value;
					break;
			}
		}

		public void DoSoilTemp(double value, int index)
		{
			if (index > 0 && index < SoilTemp.Length)
				SoilTemp[index] = value;
		}

		public void DoAirQuality(double value, int index)
		{
			switch (index)
			{
				case 1:
					AirQuality1 = value;
					break;
				case 2:
					AirQuality2 = value;
					break;
				case 3:
					AirQuality3 = value;
					break;
				case 4:
					AirQuality4 = value;
					break;
			}
		}

		public void DoAirQualityAvg(double value, int index)
		{
			switch (index)
			{
				case 1:
					AirQualityAvg1 = value;
					break;
				case 2:
					AirQualityAvg2 = value;
					break;
				case 3:
					AirQualityAvg3 = value;
					break;
				case 4:
					AirQualityAvg4 = value;
					break;
			}
		}

		public void DoLeakSensor(int value, int index)
		{
			switch (index)
			{
				case 1:
					LeakSensor1 = value;
					break;
				case 2:
					LeakSensor2 = value;
					break;
				case 3:
					LeakSensor3 = value;
					break;
				case 4:
					LeakSensor4 = value;
					break;
			}
		}

		public void DoLeafWetness(double value, int index)
		{
			switch (index)
			{
				case 1:
					LeafWetness1 = value;
					break;
				case 2:
					LeafWetness2 = value;
					break;
				case 3:
					LeafWetness3 = value;
					break;
				case 4:
					LeafWetness4 = value;
					break;
				case 5:
					LeafWetness5 = value;
					break;
				case 6:
					LeafWetness6 = value;
					break;
				case 7:
					LeafWetness7 = value;
					break;
				case 8:
					LeafWetness8 = value;
					break;
			}

			if (cumulus.StationOptions.LeafWetnessIsRainingIdx == index)
			{
				IsRaining = value >= cumulus.StationOptions.LeafWetnessIsRainingThrsh;
				cumulus.IsRainingAlarm.Triggered = IsRaining;
			}
		}

		public string BetelCast(double z_hpa, int z_month, string z_wind, int z_trend, bool z_north, double z_baro_top, double z_baro_bottom)
		{
			double z_range = z_baro_top - z_baro_bottom;
			double z_constant = (z_range / 22.0F);

			bool z_summer = (z_month >= 4 && z_month <= 9); // true if "Summer"

			if (z_north)
			{
				// North hemisphere
				if (z_wind == cumulus.Trans.compassp[0]) // N
				{
					z_hpa += 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[1]) // NNE
				{
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[2]) // NE
				{
					//			z_hpa += 4 ;
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[3]) // ENE
				{
					z_hpa += 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[4]) // E
				{
					z_hpa -= 0.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[5]) // ESE
				{
					//			z_hpa -= 3 ;
					z_hpa -= 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[6]) // SE
				{
					z_hpa -= 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[7]) // SSE
				{
					z_hpa -= 8.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[8]) // S
				{
					//			z_hpa -= 11 ;
					z_hpa -= 12F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[9]) // SSW
				{
					z_hpa -= 10F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[10]) // SW
				{
					z_hpa -= 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[11]) // WSW
				{
					z_hpa -= 4.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[12]) // W
				{
					z_hpa -= 3F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[13]) // WNW
				{
					z_hpa -= 0.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[14]) // NW
				{
					z_hpa += 1.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[15]) // NNW
				{
					z_hpa += 3F / 100F * z_range;
				}
				if (z_summer)
				{
					// if Summer
					if (z_trend == 1)
					{
						// rising
						z_hpa += 7F / 100F * z_range;
					}
					else if (z_trend == 2)
					{
						//	falling
						z_hpa -= 7F / 100F * z_range;
					}
				}
			}
			else
			{
				// must be South hemisphere
				if (z_wind == cumulus.Trans.compassp[8]) // S
				{
					z_hpa += 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[9]) // SSW
				{
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[10]) // SW
				{
					//			z_hpa += 4 ;
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[11]) // WSW
				{
					z_hpa += 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[12]) // W
				{
					z_hpa -= 0.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[13]) // WNW
				{
					//			z_hpa -= 3 ;
					z_hpa -= 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[14]) // NW
				{
					z_hpa -= 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[15]) // NNW
				{
					z_hpa -= 8.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[0]) // N
				{
					//			z_hpa -= 11 ;
					z_hpa -= 12F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[1]) // NNE
				{
					z_hpa -= 10F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[2]) // NE
				{
					z_hpa -= 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[3]) // ENE
				{
					z_hpa -= 4.5F / 100 * z_range; //
				}
				else if (z_wind == cumulus.Trans.compassp[4]) // E
				{
					z_hpa -= 3F / 100F * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[5]) // ESE
				{
					z_hpa -= 0.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[6]) // SE
				{
					z_hpa += 1.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.Trans.compassp[7]) // SSE
				{
					z_hpa += 3F / 100F * z_range;
				}
				if (!z_summer)
				{
					// if Winter
					if (z_trend == 1)
					{
						// rising
						z_hpa += 7F / 100F * z_range;
					}
					else if (z_trend == 2)
					{
						// falling
						z_hpa -= 7F / 100F * z_range;
					}
				}
			} // END North / South

			if (z_hpa == z_baro_top)
			{
				z_hpa = z_baro_top - 1;
			}

			int z_option = (int) Math.Floor((z_hpa - z_baro_bottom) / z_constant);

			StringBuilder z_output = new StringBuilder(100);
			if (z_option < 0)
			{
				z_option = 0;
				z_output.Append($"{cumulus.Trans.Exceptional}, ");
			}
			if (z_option > 21)
			{
				z_option = 21;
				z_output.Append($"{cumulus.Trans.Exceptional}, ");
			}

			if (z_trend == 1)
			{
				// rising
				Forecastnumber = cumulus.riseOptions[z_option] + 1;
				z_output.Append(cumulus.Trans.zForecast[cumulus.riseOptions[z_option]]);
			}
			else if (z_trend == 2)
			{
				// falling
				Forecastnumber = cumulus.fallOptions[z_option] + 1;
				z_output.Append(cumulus.Trans.zForecast[cumulus.fallOptions[z_option]]);
			}
			else
			{
				// must be "steady"
				Forecastnumber = cumulus.steadyOptions[z_option] + 1;
				z_output.Append(cumulus.Trans.zForecast[cumulus.steadyOptions[z_option]]);
			}
			return z_output.ToString();
		}

		public int Forecastnumber { get; set; }


		// This overridden in each station implementation
		public abstract void Stop();

		public void ReadAlltimeIniFile()
		{
			IniFile ini = new IniFile(cumulus.AlltimeIniFile);

			AllTime.HighTemp.Val = ini.GetValue("Temperature", "hightempvalue", Cumulus.DefaultHiVal);
			AllTime.HighTemp.Ts = ini.GetValue("Temperature", "hightemptime", cumulus.defaultRecordTS);

			AllTime.LowTemp.Val = ini.GetValue("Temperature", "lowtempvalue", Cumulus.DefaultLoVal);
			AllTime.LowTemp.Ts = ini.GetValue("Temperature", "lowtemptime", cumulus.defaultRecordTS);

			AllTime.LowChill.Val = ini.GetValue("Temperature", "lowchillvalue", Cumulus.DefaultLoVal);
			AllTime.LowChill.Ts = ini.GetValue("Temperature", "lowchilltime", cumulus.defaultRecordTS);

			AllTime.HighMinTemp.Val = ini.GetValue("Temperature", "highmintempvalue", Cumulus.DefaultHiVal);
			AllTime.HighMinTemp.Ts = ini.GetValue("Temperature", "highmintemptime", cumulus.defaultRecordTS);

			AllTime.LowMaxTemp.Val = ini.GetValue("Temperature", "lowmaxtempvalue", Cumulus.DefaultLoVal);
			AllTime.LowMaxTemp.Ts = ini.GetValue("Temperature", "lowmaxtemptime", cumulus.defaultRecordTS);

			AllTime.HighAppTemp.Val = ini.GetValue("Temperature", "highapptempvalue", Cumulus.DefaultHiVal);
			AllTime.HighAppTemp.Ts = ini.GetValue("Temperature", "highapptemptime", cumulus.defaultRecordTS);

			AllTime.LowAppTemp.Val = ini.GetValue("Temperature", "lowapptempvalue", Cumulus.DefaultLoVal);
			AllTime.LowAppTemp.Ts = ini.GetValue("Temperature", "lowapptemptime", cumulus.defaultRecordTS);

			AllTime.HighFeelsLike.Val = ini.GetValue("Temperature", "highfeelslikevalue", Cumulus.DefaultHiVal);
			AllTime.HighFeelsLike.Ts = ini.GetValue("Temperature", "highfeelsliketime", cumulus.defaultRecordTS);

			AllTime.LowFeelsLike.Val = ini.GetValue("Temperature", "lowfeelslikevalue", Cumulus.DefaultLoVal);
			AllTime.LowFeelsLike.Ts = ini.GetValue("Temperature", "lowfeelsliketime", cumulus.defaultRecordTS);

			AllTime.HighHumidex.Val = ini.GetValue("Temperature", "highhumidexvalue", Cumulus.DefaultHiVal);
			AllTime.HighHumidex.Ts = ini.GetValue("Temperature", "highhumidextime", cumulus.defaultRecordTS);

			AllTime.HighHeatIndex.Val = ini.GetValue("Temperature", "highheatindexvalue", Cumulus.DefaultHiVal);
			AllTime.HighHeatIndex.Ts = ini.GetValue("Temperature", "highheatindextime", cumulus.defaultRecordTS);

			AllTime.HighDewPoint.Val = ini.GetValue("Temperature", "highdewpointvalue", Cumulus.DefaultHiVal);
			AllTime.HighDewPoint.Ts = ini.GetValue("Temperature", "highdewpointtime", cumulus.defaultRecordTS);

			AllTime.LowDewPoint.Val = ini.GetValue("Temperature", "lowdewpointvalue", Cumulus.DefaultLoVal);
			AllTime.LowDewPoint.Ts = ini.GetValue("Temperature", "lowdewpointtime", cumulus.defaultRecordTS);

			AllTime.HighDailyTempRange.Val = ini.GetValue("Temperature", "hightemprangevalue", Cumulus.DefaultHiVal);
			AllTime.HighDailyTempRange.Ts = ini.GetValue("Temperature", "hightemprangetime", cumulus.defaultRecordTS);

			AllTime.LowDailyTempRange.Val = ini.GetValue("Temperature", "lowtemprangevalue", Cumulus.DefaultLoVal);
			AllTime.LowDailyTempRange.Ts = ini.GetValue("Temperature", "lowtemprangetime", cumulus.defaultRecordTS);

			AllTime.HighWind.Val = ini.GetValue("Wind", "highwindvalue", Cumulus.DefaultHiVal);
			AllTime.HighWind.Ts = ini.GetValue("Wind", "highwindtime", cumulus.defaultRecordTS);

			AllTime.HighGust.Val = ini.GetValue("Wind", "highgustvalue", Cumulus.DefaultHiVal);
			AllTime.HighGust.Ts = ini.GetValue("Wind", "highgusttime", cumulus.defaultRecordTS);

			AllTime.HighWindRun.Val = ini.GetValue("Wind", "highdailywindrunvalue", Cumulus.DefaultHiVal);
			AllTime.HighWindRun.Ts = ini.GetValue("Wind", "highdailywindruntime", cumulus.defaultRecordTS);

			AllTime.HighRainRate.Val = ini.GetValue("Rain", "highrainratevalue", Cumulus.DefaultHiVal);
			AllTime.HighRainRate.Ts = ini.GetValue("Rain", "highrainratetime", cumulus.defaultRecordTS);

			AllTime.DailyRain.Val = ini.GetValue("Rain", "highdailyrainvalue", Cumulus.DefaultHiVal);
			AllTime.DailyRain.Ts = ini.GetValue("Rain", "highdailyraintime", cumulus.defaultRecordTS);

			AllTime.HourlyRain.Val = ini.GetValue("Rain", "highhourlyrainvalue", Cumulus.DefaultHiVal);
			AllTime.HourlyRain.Ts = ini.GetValue("Rain", "highhourlyraintime", cumulus.defaultRecordTS);

			AllTime.HighRain24Hours.Val = ini.GetValue("Rain", "high24hourrainvalue", Cumulus.DefaultHiVal);
			AllTime.HighRain24Hours.Ts = ini.GetValue("Rain", "high24hourraintime", cumulus.defaultRecordTS);

			AllTime.MonthlyRain.Val = ini.GetValue("Rain", "highmonthlyrainvalue", Cumulus.DefaultHiVal);
			AllTime.MonthlyRain.Ts = ini.GetValue("Rain", "highmonthlyraintime", cumulus.defaultRecordTS);

			AllTime.LongestDryPeriod.Val = ini.GetValue("Rain", "longestdryperiodvalue", 0);
			AllTime.LongestDryPeriod.Ts = ini.GetValue("Rain", "longestdryperiodtime", cumulus.defaultRecordTS);

			AllTime.LongestWetPeriod.Val = ini.GetValue("Rain", "longestwetperiodvalue", 0);
			AllTime.LongestWetPeriod.Ts = ini.GetValue("Rain", "longestwetperiodtime", cumulus.defaultRecordTS);

			AllTime.HighPress.Val = ini.GetValue("Pressure", "highpressurevalue", Cumulus.DefaultHiVal);
			AllTime.HighPress.Ts = ini.GetValue("Pressure", "highpressuretime", cumulus.defaultRecordTS);

			AllTime.LowPress.Val = ini.GetValue("Pressure", "lowpressurevalue", Cumulus.DefaultLoVal);
			AllTime.LowPress.Ts = ini.GetValue("Pressure", "lowpressuretime", cumulus.defaultRecordTS);

			AllTime.HighHumidity.Val = ini.GetValue("Humidity", "highhumidityvalue", Cumulus.DefaultHiVal);
			AllTime.HighHumidity.Ts = ini.GetValue("Humidity", "highhumiditytime", cumulus.defaultRecordTS);

			AllTime.LowHumidity.Val = ini.GetValue("Humidity", "lowhumidityvalue", Cumulus.DefaultLoVal);
			AllTime.LowHumidity.Ts = ini.GetValue("Humidity", "lowhumiditytime", cumulus.defaultRecordTS);

			cumulus.LogMessage("Alltime.ini file read");
		}

		public void WriteAlltimeIniFile()
		{
			try
			{
				IniFile ini = new IniFile(cumulus.AlltimeIniFile);

				ini.SetValue("Temperature", "hightempvalue", AllTime.HighTemp.Val);
				ini.SetValue("Temperature", "hightemptime", AllTime.HighTemp.Ts);
				ini.SetValue("Temperature", "lowtempvalue", AllTime.LowTemp.Val);
				ini.SetValue("Temperature", "lowtemptime", AllTime.LowTemp.Ts);
				ini.SetValue("Temperature", "lowchillvalue", AllTime.LowChill.Val);
				ini.SetValue("Temperature", "lowchilltime", AllTime.LowChill.Ts);
				ini.SetValue("Temperature", "highmintempvalue", AllTime.HighMinTemp.Val);
				ini.SetValue("Temperature", "highmintemptime", AllTime.HighMinTemp.Ts);
				ini.SetValue("Temperature", "lowmaxtempvalue", AllTime.LowMaxTemp.Val);
				ini.SetValue("Temperature", "lowmaxtemptime", AllTime.LowMaxTemp.Ts);
				ini.SetValue("Temperature", "highapptempvalue", AllTime.HighAppTemp.Val);
				ini.SetValue("Temperature", "highapptemptime", AllTime.HighAppTemp.Ts);
				ini.SetValue("Temperature", "lowapptempvalue", AllTime.LowAppTemp.Val);
				ini.SetValue("Temperature", "lowapptemptime", AllTime.LowAppTemp.Ts);
				ini.SetValue("Temperature", "highfeelslikevalue", AllTime.HighFeelsLike.Val);
				ini.SetValue("Temperature", "highfeelsliketime", AllTime.HighFeelsLike.Ts);
				ini.SetValue("Temperature", "lowfeelslikevalue", AllTime.LowFeelsLike.Val);
				ini.SetValue("Temperature", "lowfeelsliketime", AllTime.LowFeelsLike.Ts);
				ini.SetValue("Temperature", "highhumidexvalue", AllTime.HighHumidex.Val);
				ini.SetValue("Temperature", "highhumidextime", AllTime.HighHumidex.Ts);
				ini.SetValue("Temperature", "highheatindexvalue", AllTime.HighHeatIndex.Val);
				ini.SetValue("Temperature", "highheatindextime", AllTime.HighHeatIndex.Ts);
				ini.SetValue("Temperature", "highdewpointvalue", AllTime.HighDewPoint.Val);
				ini.SetValue("Temperature", "highdewpointtime", AllTime.HighDewPoint.Ts);
				ini.SetValue("Temperature", "lowdewpointvalue", AllTime.LowDewPoint.Val);
				ini.SetValue("Temperature", "lowdewpointtime", AllTime.LowDewPoint.Ts);
				ini.SetValue("Temperature", "hightemprangevalue", AllTime.HighDailyTempRange.Val);
				ini.SetValue("Temperature", "hightemprangetime", AllTime.HighDailyTempRange.Ts);
				ini.SetValue("Temperature", "lowtemprangevalue", AllTime.LowDailyTempRange.Val);
				ini.SetValue("Temperature", "lowtemprangetime", AllTime.LowDailyTempRange.Ts);
				ini.SetValue("Wind", "highwindvalue", AllTime.HighWind.Val);
				ini.SetValue("Wind", "highwindtime", AllTime.HighWind.Ts);
				ini.SetValue("Wind", "highgustvalue", AllTime.HighGust.Val);
				ini.SetValue("Wind", "highgusttime", AllTime.HighGust.Ts);
				ini.SetValue("Wind", "highdailywindrunvalue", AllTime.HighWindRun.Val);
				ini.SetValue("Wind", "highdailywindruntime", AllTime.HighWindRun.Ts);
				ini.SetValue("Rain", "highrainratevalue", AllTime.HighRainRate.Val);
				ini.SetValue("Rain", "highrainratetime", AllTime.HighRainRate.Ts);
				ini.SetValue("Rain", "highdailyrainvalue", AllTime.DailyRain.Val);
				ini.SetValue("Rain", "highdailyraintime", AllTime.DailyRain.Ts);
				ini.SetValue("Rain", "highhourlyrainvalue", AllTime.HourlyRain.Val);
				ini.SetValue("Rain", "highhourlyraintime", AllTime.HourlyRain.Ts);
				ini.SetValue("Rain", "high24hourrainvalue", AllTime.HighRain24Hours.Val);
				ini.SetValue("Rain", "high24hourraintime", AllTime.HighRain24Hours.Ts);
				ini.SetValue("Rain", "highmonthlyrainvalue", AllTime.MonthlyRain.Val);
				ini.SetValue("Rain", "highmonthlyraintime", AllTime.MonthlyRain.Ts);
				ini.SetValue("Rain", "longestdryperiodvalue", AllTime.LongestDryPeriod.Val);
				ini.SetValue("Rain", "longestdryperiodtime", AllTime.LongestDryPeriod.Ts);
				ini.SetValue("Rain", "longestwetperiodvalue", AllTime.LongestWetPeriod.Val);
				ini.SetValue("Rain", "longestwetperiodtime", AllTime.LongestWetPeriod.Ts);
				ini.SetValue("Pressure", "highpressurevalue", AllTime.HighPress.Val);
				ini.SetValue("Pressure", "highpressuretime", AllTime.HighPress.Ts);
				ini.SetValue("Pressure", "lowpressurevalue", AllTime.LowPress.Val);
				ini.SetValue("Pressure", "lowpressuretime", AllTime.LowPress.Ts);
				ini.SetValue("Humidity", "highhumidityvalue", AllTime.HighHumidity.Val);
				ini.SetValue("Humidity", "highhumiditytime", AllTime.HighHumidity.Ts);
				ini.SetValue("Humidity", "lowhumidityvalue", AllTime.LowHumidity.Val);
				ini.SetValue("Humidity", "lowhumiditytime", AllTime.LowHumidity.Ts);

				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error writing alltime.ini file: " + ex.Message);
			}
		}

		public void ReadMonthlyAlltimeIniFile()
		{
			IniFile ini = new IniFile(cumulus.MonthlyAlltimeIniFile);
			for (int month = 1; month <= 12; month++)
			{
				string monthstr = month.ToString("D2");

				MonthlyRecs[month].HighTemp.Val = ini.GetValue("Temperature" + monthstr, "hightempvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighTemp.Ts = ini.GetValue("Temperature" + monthstr, "hightemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowTemp.Val = ini.GetValue("Temperature" + monthstr, "lowtempvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowTemp.Ts = ini.GetValue("Temperature" + monthstr, "lowtemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowChill.Val = ini.GetValue("Temperature" + monthstr, "lowchillvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowChill.Ts = ini.GetValue("Temperature" + monthstr, "lowchilltime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighMinTemp.Val = ini.GetValue("Temperature" + monthstr, "highmintempvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighMinTemp.Ts = ini.GetValue("Temperature" + monthstr, "highmintemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowMaxTemp.Val = ini.GetValue("Temperature" + monthstr, "lowmaxtempvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowMaxTemp.Ts = ini.GetValue("Temperature" + monthstr, "lowmaxtemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighAppTemp.Val = ini.GetValue("Temperature" + monthstr, "highapptempvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighAppTemp.Ts = ini.GetValue("Temperature" + monthstr, "highapptemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowAppTemp.Val = ini.GetValue("Temperature" + monthstr, "lowapptempvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowAppTemp.Ts = ini.GetValue("Temperature" + monthstr, "lowapptemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighFeelsLike.Val = ini.GetValue("Temperature" + monthstr, "highfeelslikevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighFeelsLike.Ts = ini.GetValue("Temperature" + monthstr, "highfeelsliketime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowFeelsLike.Val = ini.GetValue("Temperature" + monthstr, "lowfeelslikevalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowFeelsLike.Ts = ini.GetValue("Temperature" + monthstr, "lowfeelsliketime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighHumidex.Val = ini.GetValue("Temperature" + monthstr, "highhumidexvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighHumidex.Ts = ini.GetValue("Temperature" + monthstr, "highhumidextime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighHeatIndex.Val = ini.GetValue("Temperature" + monthstr, "highheatindexvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighHeatIndex.Ts = ini.GetValue("Temperature" + monthstr, "highheatindextime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighDewPoint.Val = ini.GetValue("Temperature" + monthstr, "highdewpointvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighDewPoint.Ts = ini.GetValue("Temperature" + monthstr, "highdewpointtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowDewPoint.Val = ini.GetValue("Temperature" + monthstr, "lowdewpointvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowDewPoint.Ts = ini.GetValue("Temperature" + monthstr, "lowdewpointtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighDailyTempRange.Val = ini.GetValue("Temperature" + monthstr, "hightemprangevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighDailyTempRange.Ts = ini.GetValue("Temperature" + monthstr, "hightemprangetime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowDailyTempRange.Val = ini.GetValue("Temperature" + monthstr, "lowtemprangevalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowDailyTempRange.Ts = ini.GetValue("Temperature" + monthstr, "lowtemprangetime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighWind.Val = ini.GetValue("Wind" + monthstr, "highwindvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighWind.Ts = ini.GetValue("Wind" + monthstr, "highwindtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighGust.Val = ini.GetValue("Wind" + monthstr, "highgustvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighGust.Ts = ini.GetValue("Wind" + monthstr, "highgusttime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighWindRun.Val = ini.GetValue("Wind" + monthstr, "highdailywindrunvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighWindRun.Ts = ini.GetValue("Wind" + monthstr, "highdailywindruntime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighRainRate.Val = ini.GetValue("Rain" + monthstr, "highrainratevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighRainRate.Ts = ini.GetValue("Rain" + monthstr, "highrainratetime", cumulus.defaultRecordTS);

				MonthlyRecs[month].DailyRain.Val = ini.GetValue("Rain" + monthstr, "highdailyrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].DailyRain.Ts = ini.GetValue("Rain" + monthstr, "highdailyraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HourlyRain.Val = ini.GetValue("Rain" + monthstr, "highhourlyrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HourlyRain.Ts = ini.GetValue("Rain" + monthstr, "highhourlyraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighRain24Hours.Val = ini.GetValue("Rain" + monthstr, "high24hourrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighRain24Hours.Ts = ini.GetValue("Rain" + monthstr, "high24hourraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].MonthlyRain.Val = ini.GetValue("Rain" + monthstr, "highmonthlyrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].MonthlyRain.Ts = ini.GetValue("Rain" + monthstr, "highmonthlyraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LongestDryPeriod.Val = ini.GetValue("Rain" + monthstr, "longestdryperiodvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].LongestDryPeriod.Ts = ini.GetValue("Rain" + monthstr, "longestdryperiodtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LongestWetPeriod.Val = ini.GetValue("Rain" + monthstr, "longestwetperiodvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].LongestWetPeriod.Ts = ini.GetValue("Rain" + monthstr, "longestwetperiodtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighPress.Val = ini.GetValue("Pressure" + monthstr, "highpressurevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighPress.Ts = ini.GetValue("Pressure" + monthstr, "highpressuretime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowPress.Val = ini.GetValue("Pressure" + monthstr, "lowpressurevalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowPress.Ts = ini.GetValue("Pressure" + monthstr, "lowpressuretime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighHumidity.Val = ini.GetValue("Humidity" + monthstr, "highhumidityvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighHumidity.Ts = ini.GetValue("Humidity" + monthstr, "highhumiditytime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowHumidity.Val = ini.GetValue("Humidity" + monthstr, "lowhumidityvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowHumidity.Ts = ini.GetValue("Humidity" + monthstr, "lowhumiditytime", cumulus.defaultRecordTS);
			}

			cumulus.LogMessage("MonthlyAlltime.ini file read");
		}

		public void WriteMonthlyAlltimeIniFile()
		{
			try
			{
				IniFile ini = new IniFile(cumulus.MonthlyAlltimeIniFile);
				for (int month = 1; month <= 12; month++)
				{
					string monthstr = month.ToString("D2");

					ini.SetValue("Temperature" + monthstr, "hightempvalue", MonthlyRecs[month].HighTemp.Val);
					ini.SetValue("Temperature" + monthstr, "hightemptime", MonthlyRecs[month].HighTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowtempvalue", MonthlyRecs[month].LowTemp.Val);
					ini.SetValue("Temperature" + monthstr, "lowtemptime", MonthlyRecs[month].LowTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowchillvalue", MonthlyRecs[month].LowChill.Val);
					ini.SetValue("Temperature" + monthstr, "lowchilltime", MonthlyRecs[month].LowChill.Ts);
					ini.SetValue("Temperature" + monthstr, "highmintempvalue", MonthlyRecs[month].HighMinTemp.Val);
					ini.SetValue("Temperature" + monthstr, "highmintemptime", MonthlyRecs[month].HighMinTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowmaxtempvalue", MonthlyRecs[month].LowMaxTemp.Val);
					ini.SetValue("Temperature" + monthstr, "lowmaxtemptime", MonthlyRecs[month].LowMaxTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "highapptempvalue", MonthlyRecs[month].HighAppTemp.Val);
					ini.SetValue("Temperature" + monthstr, "highapptemptime", MonthlyRecs[month].HighAppTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowapptempvalue", MonthlyRecs[month].LowAppTemp.Val);
					ini.SetValue("Temperature" + monthstr, "lowapptemptime", MonthlyRecs[month].LowAppTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "highfeelslikevalue", MonthlyRecs[month].HighFeelsLike.Val);
					ini.SetValue("Temperature" + monthstr, "highfeelsliketime", MonthlyRecs[month].HighFeelsLike.Ts);
					ini.SetValue("Temperature" + monthstr, "lowfeelslikevalue", MonthlyRecs[month].LowFeelsLike.Val);
					ini.SetValue("Temperature" + monthstr, "lowfeelsliketime", MonthlyRecs[month].LowFeelsLike.Ts);
					ini.SetValue("Temperature" + monthstr, "highhumidexvalue", MonthlyRecs[month].HighHumidex.Val);
					ini.SetValue("Temperature" + monthstr, "highhumidextime", MonthlyRecs[month].HighHumidex.Ts);
					ini.SetValue("Temperature" + monthstr, "highheatindexvalue", MonthlyRecs[month].HighHeatIndex.Val);
					ini.SetValue("Temperature" + monthstr, "highheatindextime", MonthlyRecs[month].HighHeatIndex.Ts);
					ini.SetValue("Temperature" + monthstr, "highdewpointvalue", MonthlyRecs[month].HighDewPoint.Val);
					ini.SetValue("Temperature" + monthstr, "highdewpointtime", MonthlyRecs[month].HighDewPoint.Ts);
					ini.SetValue("Temperature" + monthstr, "lowdewpointvalue", MonthlyRecs[month].LowDewPoint.Val);
					ini.SetValue("Temperature" + monthstr, "lowdewpointtime", MonthlyRecs[month].LowDewPoint.Ts);
					ini.SetValue("Temperature" + monthstr, "hightemprangevalue", MonthlyRecs[month].HighDailyTempRange.Val);
					ini.SetValue("Temperature" + monthstr, "hightemprangetime", MonthlyRecs[month].HighDailyTempRange.Ts);
					ini.SetValue("Temperature" + monthstr, "lowtemprangevalue", MonthlyRecs[month].LowDailyTempRange.Val);
					ini.SetValue("Temperature" + monthstr, "lowtemprangetime", MonthlyRecs[month].LowDailyTempRange.Ts);
					ini.SetValue("Wind" + monthstr, "highwindvalue", MonthlyRecs[month].HighWind.Val);
					ini.SetValue("Wind" + monthstr, "highwindtime", MonthlyRecs[month].HighWind.Ts);
					ini.SetValue("Wind" + monthstr, "highgustvalue", MonthlyRecs[month].HighGust.Val);
					ini.SetValue("Wind" + monthstr, "highgusttime", MonthlyRecs[month].HighGust.Ts);
					ini.SetValue("Wind" + monthstr, "highdailywindrunvalue", MonthlyRecs[month].HighWindRun.Val);
					ini.SetValue("Wind" + monthstr, "highdailywindruntime", MonthlyRecs[month].HighWindRun.Ts);
					ini.SetValue("Rain" + monthstr, "highrainratevalue", MonthlyRecs[month].HighRainRate.Val);
					ini.SetValue("Rain" + monthstr, "highrainratetime", MonthlyRecs[month].HighRainRate.Ts);
					ini.SetValue("Rain" + monthstr, "highdailyrainvalue", MonthlyRecs[month].DailyRain.Val);
					ini.SetValue("Rain" + monthstr, "highdailyraintime", MonthlyRecs[month].DailyRain.Ts);
					ini.SetValue("Rain" + monthstr, "highhourlyrainvalue", MonthlyRecs[month].HourlyRain.Val);
					ini.SetValue("Rain" + monthstr, "highhourlyraintime", MonthlyRecs[month].HourlyRain.Ts);
					ini.SetValue("Rain" + monthstr, "high24hourrainvalue", MonthlyRecs[month].HighRain24Hours.Val);
					ini.SetValue("Rain" + monthstr, "high24hourraintime", MonthlyRecs[month].HighRain24Hours.Ts);
					ini.SetValue("Rain" + monthstr, "highmonthlyrainvalue", MonthlyRecs[month].MonthlyRain.Val);
					ini.SetValue("Rain" + monthstr, "highmonthlyraintime", MonthlyRecs[month].MonthlyRain.Ts);
					ini.SetValue("Rain" + monthstr, "longestdryperiodvalue", MonthlyRecs[month].LongestDryPeriod.Val);
					ini.SetValue("Rain" + monthstr, "longestdryperiodtime", MonthlyRecs[month].LongestDryPeriod.Ts);
					ini.SetValue("Rain" + monthstr, "longestwetperiodvalue", MonthlyRecs[month].LongestWetPeriod.Val);
					ini.SetValue("Rain" + monthstr, "longestwetperiodtime", MonthlyRecs[month].LongestWetPeriod.Ts);
					ini.SetValue("Pressure" + monthstr, "highpressurevalue", MonthlyRecs[month].HighPress.Val);
					ini.SetValue("Pressure" + monthstr, "highpressuretime", MonthlyRecs[month].HighPress.Ts);
					ini.SetValue("Pressure" + monthstr, "lowpressurevalue", MonthlyRecs[month].LowPress.Val);
					ini.SetValue("Pressure" + monthstr, "lowpressuretime", MonthlyRecs[month].LowPress.Ts);
					ini.SetValue("Humidity" + monthstr, "highhumidityvalue", MonthlyRecs[month].HighHumidity.Val);
					ini.SetValue("Humidity" + monthstr, "highhumiditytime", MonthlyRecs[month].HighHumidity.Ts);
					ini.SetValue("Humidity" + monthstr, "lowhumidityvalue", MonthlyRecs[month].LowHumidity.Val);
					ini.SetValue("Humidity" + monthstr, "lowhumiditytime", MonthlyRecs[month].LowHumidity.Ts);
				}
				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error writing MonthlyAlltime.ini file: " + ex.Message);
			}
		}

		public void SetDefaultMonthlyHighsAndLows()
		{
			// this Month highs and lows
			ThisMonth.HighGust.Val = Cumulus.DefaultHiVal;
			ThisMonth.HighWind.Val = Cumulus.DefaultHiVal;
			ThisMonth.HighTemp.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowTemp.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighAppTemp.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowAppTemp.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighFeelsLike.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowFeelsLike.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighHumidex.Val = Cumulus.DefaultHiVal;
			ThisMonth.HighDewPoint.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowDewPoint.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighPress.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowPress.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighRainRate.Val = Cumulus.DefaultHiVal;
			ThisMonth.HourlyRain.Val = Cumulus.DefaultHiVal;
			ThisMonth.HighRain24Hours.Val = Cumulus.DefaultHiVal;
			ThisMonth.DailyRain.Val = Cumulus.DefaultHiVal;
			ThisMonth.HighHumidity.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowHumidity.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighHeatIndex.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowChill.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighMinTemp.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowMaxTemp.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighWindRun.Val = Cumulus.DefaultHiVal;
			ThisMonth.LowDailyTempRange.Val = Cumulus.DefaultLoVal;
			ThisMonth.HighDailyTempRange.Val = Cumulus.DefaultHiVal;

			// this Month highs and lows - timestamps
			ThisMonth.HighGust.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighWind.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighTemp.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowTemp.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighAppTemp.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowAppTemp.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighFeelsLike.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowFeelsLike.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighHumidex.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighDewPoint.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowDewPoint.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighPress.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowPress.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighRainRate.Ts = cumulus.defaultRecordTS;
			ThisMonth.HourlyRain.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighRain24Hours.Ts = cumulus.defaultRecordTS;
			ThisMonth.DailyRain.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighHumidity.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowHumidity.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighHeatIndex.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowChill.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighMinTemp.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowMaxTemp.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighWindRun.Ts = cumulus.defaultRecordTS;
			ThisMonth.LowDailyTempRange.Ts = cumulus.defaultRecordTS;
			ThisMonth.HighDailyTempRange.Ts = cumulus.defaultRecordTS;
		}

		public void ReadMonthIniFile()
		{
			//DateTime timestamp;

			SetDefaultMonthlyHighsAndLows();

			if (File.Exists(cumulus.MonthIniFile))
			{
				//int hourInc = cumulus.GetHourInc();

				IniFile ini = new IniFile(cumulus.MonthIniFile);

				// Date
				//timestamp = ini.GetValue("General", "Date", cumulus.defaultRecordTS);

				ThisMonth.HighWind.Val = ini.GetValue("Wind", "Speed", Cumulus.DefaultHiVal);
				ThisMonth.HighWind.Ts = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				ThisMonth.HighGust.Val = ini.GetValue("Wind", "Gust", Cumulus.DefaultHiVal);
				ThisMonth.HighGust.Ts = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				ThisMonth.HighWindRun.Val = ini.GetValue("Wind", "Windrun", Cumulus.DefaultHiVal);
				ThisMonth.HighWindRun.Ts = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				ThisMonth.LowTemp.Val = ini.GetValue("Temp", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowTemp.Ts = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighTemp.Val = ini.GetValue("Temp", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighTemp.Ts = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				ThisMonth.LowMaxTemp.Val = ini.GetValue("Temp", "LowMax", Cumulus.DefaultLoVal);
				ThisMonth.LowMaxTemp.Ts = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				ThisMonth.HighMinTemp.Val = ini.GetValue("Temp", "HighMin", Cumulus.DefaultHiVal);
				ThisMonth.HighMinTemp.Ts = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				ThisMonth.LowDailyTempRange.Val = ini.GetValue("Temp", "LowRange", Cumulus.DefaultLoVal);
				ThisMonth.LowDailyTempRange.Ts = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				ThisMonth.HighDailyTempRange.Val = ini.GetValue("Temp", "HighRange", Cumulus.DefaultHiVal);
				ThisMonth.HighDailyTempRange.Ts = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				ThisMonth.LowPress.Val = ini.GetValue("Pressure", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowPress.Ts = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighPress.Val = ini.GetValue("Pressure", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighPress.Ts = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				ThisMonth.HighRainRate.Val = ini.GetValue("Rain", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighRainRate.Ts = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				ThisMonth.HourlyRain.Val = ini.GetValue("Rain", "HourlyHigh", Cumulus.DefaultHiVal);
				ThisMonth.HourlyRain.Ts = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				ThisMonth.DailyRain.Val = ini.GetValue("Rain", "DailyHigh", Cumulus.DefaultHiVal);
				ThisMonth.DailyRain.Ts = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				ThisMonth.HighRain24Hours.Val = ini.GetValue("Rain", "24Hour", Cumulus.DefaultHiVal);
				ThisMonth.HighRain24Hours.Ts = ini.GetValue("Rain", "24HourTime", cumulus.defaultRecordTS);
				ThisMonth.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", 0);
				ThisMonth.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				ThisMonth.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", 0);
				ThisMonth.LongestWetPeriod.Ts = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				ThisMonth.LowHumidity.Val = ini.GetValue("Humidity", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowHumidity.Ts = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighHumidity.Val = ini.GetValue("Humidity", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighHumidity.Ts = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				ThisMonth.HighHeatIndex.Val = ini.GetValue("HeatIndex", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighHeatIndex.Ts = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				ThisMonth.LowAppTemp.Val = ini.GetValue("AppTemp", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowAppTemp.Ts = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighAppTemp.Val = ini.GetValue("AppTemp", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighAppTemp.Ts = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				ThisMonth.LowDewPoint.Val = ini.GetValue("Dewpoint", "Low", 999.0);
				ThisMonth.LowDewPoint.Ts = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighDewPoint.Val = ini.GetValue("Dewpoint", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighDewPoint.Ts = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				ThisMonth.LowChill.Val = ini.GetValue("WindChill", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowChill.Ts = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like temp
				ThisMonth.LowFeelsLike.Val = ini.GetValue("FeelsLike", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowFeelsLike.Ts = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighFeelsLike.Val = ini.GetValue("FeelsLike", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighFeelsLike.Ts = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				ThisMonth.HighHumidex.Val = ini.GetValue("Humidex", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighHumidex.Ts = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);

				cumulus.LogMessage("Month.ini file read");
			}
		}

		public void WriteMonthIniFile()
		{
			cumulus.LogDebugMessage("Writing to Month.ini file");
			lock (monthIniThreadLock)
			{
				try
				{
					int hourInc = cumulus.GetHourInc();

					IniFile ini = new IniFile(cumulus.MonthIniFile);
					// Date
					ini.SetValue("General", "Date", DateTime.Now.AddHours(hourInc));
					// Wind
					ini.SetValue("Wind", "Speed", ThisMonth.HighWind.Val);
					ini.SetValue("Wind", "SpTime", ThisMonth.HighWind.Ts);
					ini.SetValue("Wind", "Gust", ThisMonth.HighGust.Val);
					ini.SetValue("Wind", "Time", ThisMonth.HighGust.Ts);
					ini.SetValue("Wind", "Windrun", ThisMonth.HighWindRun.Val);
					ini.SetValue("Wind", "WindrunTime", ThisMonth.HighWindRun.Ts);
					// Temperature
					ini.SetValue("Temp", "Low", ThisMonth.LowTemp.Val);
					ini.SetValue("Temp", "LTime", ThisMonth.LowTemp.Ts);
					ini.SetValue("Temp", "High", ThisMonth.HighTemp.Val);
					ini.SetValue("Temp", "HTime", ThisMonth.HighTemp.Ts);
					ini.SetValue("Temp", "LowMax", ThisMonth.LowMaxTemp.Val);
					ini.SetValue("Temp", "LMTime", ThisMonth.LowMaxTemp.Ts);
					ini.SetValue("Temp", "HighMin", ThisMonth.HighMinTemp.Val);
					ini.SetValue("Temp", "HMTime", ThisMonth.HighMinTemp.Ts);
					ini.SetValue("Temp", "LowRange", ThisMonth.LowDailyTempRange.Val);
					ini.SetValue("Temp", "LowRangeTime", ThisMonth.LowDailyTempRange.Ts);
					ini.SetValue("Temp", "HighRange", ThisMonth.HighDailyTempRange.Val);
					ini.SetValue("Temp", "HighRangeTime", ThisMonth.HighDailyTempRange.Ts);
					// Pressure
					ini.SetValue("Pressure", "Low", ThisMonth.LowPress.Val);
					ini.SetValue("Pressure", "LTime", ThisMonth.LowPress.Ts);
					ini.SetValue("Pressure", "High", ThisMonth.HighPress.Val);
					ini.SetValue("Pressure", "HTime", ThisMonth.HighPress.Ts);
					// rain
					ini.SetValue("Rain", "High", ThisMonth.HighRainRate.Val);
					ini.SetValue("Rain", "HTime", ThisMonth.HighRainRate.Ts);
					ini.SetValue("Rain", "HourlyHigh", ThisMonth.HourlyRain.Val);
					ini.SetValue("Rain", "HHourlyTime", ThisMonth.HourlyRain.Ts);
					ini.SetValue("Rain", "DailyHigh", ThisMonth.DailyRain.Val);
					ini.SetValue("Rain", "HDailyTime", ThisMonth.DailyRain.Ts);
					ini.SetValue("Rain", "24Hour", ThisMonth.HighRain24Hours.Val);
					ini.SetValue("Rain", "24HourTime", ThisMonth.HighRain24Hours.Ts);
					ini.SetValue("Rain", "LongestDryPeriod", ThisMonth.LongestDryPeriod.Val);
					ini.SetValue("Rain", "LongestDryPeriodTime", ThisMonth.LongestDryPeriod.Ts);
					ini.SetValue("Rain", "LongestWetPeriod", ThisMonth.LongestWetPeriod.Val);
					ini.SetValue("Rain", "LongestWetPeriodTime", ThisMonth.LongestWetPeriod.Ts);
					// humidity
					ini.SetValue("Humidity", "Low", ThisMonth.LowHumidity.Val);
					ini.SetValue("Humidity", "LTime", ThisMonth.LowHumidity.Ts);
					ini.SetValue("Humidity", "High", ThisMonth.HighHumidity.Val);
					ini.SetValue("Humidity", "HTime", ThisMonth.HighHumidity.Ts);
					// heat index
					ini.SetValue("HeatIndex", "High", ThisMonth.HighHeatIndex.Val);
					ini.SetValue("HeatIndex", "HTime", ThisMonth.HighHeatIndex.Ts);
					// App temp
					ini.SetValue("AppTemp", "Low", ThisMonth.LowAppTemp.Val);
					ini.SetValue("AppTemp", "LTime", ThisMonth.LowAppTemp.Ts);
					ini.SetValue("AppTemp", "High", ThisMonth.HighAppTemp.Val);
					ini.SetValue("AppTemp", "HTime", ThisMonth.HighAppTemp.Ts);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", ThisMonth.LowDewPoint.Val);
					ini.SetValue("Dewpoint", "LTime", ThisMonth.LowDewPoint.Ts);
					ini.SetValue("Dewpoint", "High", ThisMonth.HighDewPoint.Val);
					ini.SetValue("Dewpoint", "HTime", ThisMonth.HighDewPoint.Ts);
					// wind chill
					ini.SetValue("WindChill", "Low", ThisMonth.LowChill.Val);
					ini.SetValue("WindChill", "LTime", ThisMonth.LowChill.Ts);
					// feels like
					ini.SetValue("FeelsLike", "Low", ThisMonth.LowFeelsLike.Val);
					ini.SetValue("FeelsLike", "LTime", ThisMonth.LowFeelsLike.Ts);
					ini.SetValue("FeelsLike", "High", ThisMonth.HighFeelsLike.Val);
					ini.SetValue("FeelsLike", "HTime", ThisMonth.HighFeelsLike.Ts);
					// Humidex
					ini.SetValue("Humidex", "High", ThisMonth.HighHumidex.Val);
					ini.SetValue("Humidex", "HTime", ThisMonth.HighHumidex.Ts);

					ini.Flush();
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error writing month.ini file: " + ex.Message);
				}
			}
			cumulus.LogDebugMessage("End writing to Month.ini file");
		}

		public void ReadYearIniFile()
		{
			//DateTime timestamp;

			SetDefaultYearlyHighsAndLows();

			if (File.Exists(cumulus.YearIniFile))
			{
				//int hourInc = cumulus.GetHourInc();

				IniFile ini = new IniFile(cumulus.YearIniFile);

				// Date
				//timestamp = ini.GetValue("General", "Date", cumulus.defaultRecordTS);

				ThisYear.HighWind.Val = ini.GetValue("Wind", "Speed", Cumulus.DefaultHiVal);
				ThisYear.HighWind.Ts = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				ThisYear.HighGust.Val = ini.GetValue("Wind", "Gust", Cumulus.DefaultHiVal);
				ThisYear.HighGust.Ts = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				ThisYear.HighWindRun.Val = ini.GetValue("Wind", "Windrun", Cumulus.DefaultHiVal);
				ThisYear.HighWindRun.Ts = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				ThisYear.LowTemp.Val = ini.GetValue("Temp", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowTemp.Ts = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighTemp.Val = ini.GetValue("Temp", "High", Cumulus.DefaultHiVal);
				ThisYear.HighTemp.Ts = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				ThisYear.LowMaxTemp.Val = ini.GetValue("Temp", "LowMax", Cumulus.DefaultLoVal);
				ThisYear.LowMaxTemp.Ts = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				ThisYear.HighMinTemp.Val = ini.GetValue("Temp", "HighMin", Cumulus.DefaultHiVal);
				ThisYear.HighMinTemp.Ts = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				ThisYear.LowDailyTempRange.Val = ini.GetValue("Temp", "LowRange", Cumulus.DefaultLoVal);
				ThisYear.LowDailyTempRange.Ts = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				ThisYear.HighDailyTempRange.Val = ini.GetValue("Temp", "HighRange", Cumulus.DefaultHiVal);
				ThisYear.HighDailyTempRange.Ts = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				ThisYear.LowPress.Val = ini.GetValue("Pressure", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowPress.Ts = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighPress.Val = ini.GetValue("Pressure", "High", Cumulus.DefaultHiVal);
				ThisYear.HighPress.Ts = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				ThisYear.HighRainRate.Val = ini.GetValue("Rain", "High", Cumulus.DefaultHiVal);
				ThisYear.HighRainRate.Ts = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				ThisYear.HourlyRain.Val = ini.GetValue("Rain", "HourlyHigh", Cumulus.DefaultHiVal);
				ThisYear.HourlyRain.Ts = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				ThisYear.DailyRain.Val = ini.GetValue("Rain", "DailyHigh", Cumulus.DefaultHiVal);
				ThisYear.DailyRain.Ts = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				ThisYear.HighRain24Hours.Val = ini.GetValue("Rain", "24Hour", Cumulus.DefaultHiVal);
				ThisYear.HighRain24Hours.Ts = ini.GetValue("Rain", "24HourTime", cumulus.defaultRecordTS);
				ThisYear.MonthlyRain.Val = ini.GetValue("Rain", "MonthlyHigh", Cumulus.DefaultHiVal);
				ThisYear.MonthlyRain.Ts = ini.GetValue("Rain", "HMonthlyTime", cumulus.defaultRecordTS);
				ThisYear.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", 0);
				ThisYear.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				ThisYear.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", 0);
				ThisYear.LongestWetPeriod.Ts = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				ThisYear.LowHumidity.Val = ini.GetValue("Humidity", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowHumidity.Ts = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighHumidity.Val = ini.GetValue("Humidity", "High", Cumulus.DefaultHiVal);
				ThisYear.HighHumidity.Ts = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				ThisYear.HighHeatIndex.Val = ini.GetValue("HeatIndex", "High", Cumulus.DefaultHiVal);
				ThisYear.HighHeatIndex.Ts = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				ThisYear.LowAppTemp.Val = ini.GetValue("AppTemp", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowAppTemp.Ts = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighAppTemp.Val = ini.GetValue("AppTemp", "High", Cumulus.DefaultHiVal);
				ThisYear.HighAppTemp.Ts = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				ThisYear.LowDewPoint.Val = ini.GetValue("Dewpoint", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowDewPoint.Ts = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighDewPoint.Val = ini.GetValue("Dewpoint", "High", Cumulus.DefaultHiVal);
				ThisYear.HighDewPoint.Ts = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				ThisYear.LowChill.Val = ini.GetValue("WindChill", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowChill.Ts = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like
				ThisYear.LowFeelsLike.Val = ini.GetValue("FeelsLike", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowFeelsLike.Ts = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighFeelsLike.Val = ini.GetValue("FeelsLike", "High", Cumulus.DefaultHiVal);
				ThisYear.HighFeelsLike.Ts = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				ThisYear.HighHumidex.Val = ini.GetValue("Humidex", "High", Cumulus.DefaultHiVal);
				ThisYear.HighHumidex.Ts = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);

				cumulus.LogMessage("Year.ini file read");
			}
		}

		public void WriteYearIniFile()
		{
			lock (yearIniThreadLock)
			{
				try
				{
					int hourInc = cumulus.GetHourInc();

					IniFile ini = new IniFile(cumulus.YearIniFile);
					// Date
					ini.SetValue("General", "Date", DateTime.Now.AddHours(hourInc));
					// Wind
					ini.SetValue("Wind", "Speed", ThisYear.HighWind.Val);
					ini.SetValue("Wind", "SpTime", ThisYear.HighWind.Ts);
					ini.SetValue("Wind", "Gust", ThisYear.HighGust.Val);
					ini.SetValue("Wind", "Time", ThisYear.HighGust.Ts);
					ini.SetValue("Wind", "Windrun", ThisYear.HighWindRun.Val);
					ini.SetValue("Wind", "WindrunTime", ThisYear.HighWindRun.Ts);
					// Temperature
					ini.SetValue("Temp", "Low", ThisYear.LowTemp.Val);
					ini.SetValue("Temp", "LTime", ThisYear.LowTemp.Ts);
					ini.SetValue("Temp", "High", ThisYear.HighTemp.Val);
					ini.SetValue("Temp", "HTime", ThisYear.HighTemp.Ts);
					ini.SetValue("Temp", "LowMax", ThisYear.LowMaxTemp.Val);
					ini.SetValue("Temp", "LMTime", ThisYear.LowMaxTemp.Ts);
					ini.SetValue("Temp", "HighMin", ThisYear.HighMinTemp.Val);
					ini.SetValue("Temp", "HMTime", ThisYear.HighMinTemp.Ts);
					ini.SetValue("Temp", "LowRange", ThisYear.LowDailyTempRange.Val);
					ini.SetValue("Temp", "LowRangeTime", ThisYear.LowDailyTempRange.Ts);
					ini.SetValue("Temp", "HighRange", ThisYear.HighDailyTempRange.Val);
					ini.SetValue("Temp", "HighRangeTime", ThisYear.HighDailyTempRange.Ts);
					// Pressure
					ini.SetValue("Pressure", "Low", ThisYear.LowPress.Val);
					ini.SetValue("Pressure", "LTime", ThisYear.LowPress.Ts);
					ini.SetValue("Pressure", "High", ThisYear.HighPress.Val);
					ini.SetValue("Pressure", "HTime", ThisYear.HighPress.Ts);
					// rain
					ini.SetValue("Rain", "High", ThisYear.HighRainRate.Val);
					ini.SetValue("Rain", "HTime", ThisYear.HighRainRate.Ts);
					ini.SetValue("Rain", "HourlyHigh", ThisYear.HourlyRain.Val);
					ini.SetValue("Rain", "HHourlyTime", ThisYear.HourlyRain.Ts);
					ini.SetValue("Rain", "DailyHigh", ThisYear.DailyRain.Val);
					ini.SetValue("Rain", "HDailyTime", ThisYear.DailyRain.Ts);
					ini.SetValue("Rain", "24Hour", ThisYear.HighRain24Hours.Val);
					ini.SetValue("Rain", "24HourTime", ThisYear.HighRain24Hours.Ts);
					ini.SetValue("Rain", "MonthlyHigh", ThisYear.MonthlyRain.Val);
					ini.SetValue("Rain", "HMonthlyTime", ThisYear.MonthlyRain.Ts);
					ini.SetValue("Rain", "LongestDryPeriod", ThisYear.LongestDryPeriod.Val);
					ini.SetValue("Rain", "LongestDryPeriodTime", ThisYear.LongestDryPeriod.Ts);
					ini.SetValue("Rain", "LongestWetPeriod", ThisYear.LongestWetPeriod.Val);
					ini.SetValue("Rain", "LongestWetPeriodTime", ThisYear.LongestWetPeriod.Ts);
					// humidity
					ini.SetValue("Humidity", "Low", ThisYear.LowHumidity.Val);
					ini.SetValue("Humidity", "LTime", ThisYear.LowHumidity.Ts);
					ini.SetValue("Humidity", "High", ThisYear.HighHumidity.Val);
					ini.SetValue("Humidity", "HTime", ThisYear.HighHumidity.Ts);
					// heat index
					ini.SetValue("HeatIndex", "High", ThisYear.HighHeatIndex.Val);
					ini.SetValue("HeatIndex", "HTime", ThisYear.HighHeatIndex.Ts);
					// App temp
					ini.SetValue("AppTemp", "Low", ThisYear.LowAppTemp.Val);
					ini.SetValue("AppTemp", "LTime", ThisYear.LowAppTemp.Ts);
					ini.SetValue("AppTemp", "High", ThisYear.HighAppTemp.Val);
					ini.SetValue("AppTemp", "HTime", ThisYear.HighAppTemp.Ts);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", ThisYear.LowDewPoint.Val);
					ini.SetValue("Dewpoint", "LTime", ThisYear.LowDewPoint.Ts);
					ini.SetValue("Dewpoint", "High", ThisYear.HighDewPoint.Val);
					ini.SetValue("Dewpoint", "HTime", ThisYear.HighDewPoint.Ts);
					// wind chill
					ini.SetValue("WindChill", "Low", ThisYear.LowChill.Val);
					ini.SetValue("WindChill", "LTime", ThisYear.LowChill.Ts);
					// Feels like
					ini.SetValue("FeelsLike", "Low", ThisYear.LowFeelsLike.Val);
					ini.SetValue("FeelsLike", "LTime", ThisYear.LowFeelsLike.Ts);
					ini.SetValue("FeelsLike", "High", ThisYear.HighFeelsLike.Val);
					ini.SetValue("FeelsLike", "HTime", ThisYear.HighFeelsLike.Ts);
					// Humidex
					ini.SetValue("Humidex", "High", ThisYear.HighHumidex.Val);
					ini.SetValue("Humidex", "HTime", ThisYear.HighHumidex.Ts);

					ini.Flush();
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error writing year.ini file: " + ex.Message);
				}
			}
		}

		public void SetDefaultYearlyHighsAndLows()
		{
			// this Year highs and lows
			ThisYear.HighGust.Val = Cumulus.DefaultHiVal;
			ThisYear.HighWind.Val = Cumulus.DefaultHiVal;
			ThisYear.HighTemp.Val = Cumulus.DefaultHiVal;
			ThisYear.LowTemp.Val = Cumulus.DefaultLoVal;
			ThisYear.HighAppTemp.Val = Cumulus.DefaultHiVal;
			ThisYear.LowAppTemp.Val = Cumulus.DefaultLoVal;
			ThisYear.HighFeelsLike.Val = Cumulus.DefaultHiVal;
			ThisYear.LowFeelsLike.Val = Cumulus.DefaultLoVal;
			ThisYear.HighHumidex.Val = Cumulus.DefaultHiVal;
			ThisYear.HighDewPoint.Val = Cumulus.DefaultHiVal;
			ThisYear.LowDewPoint.Val = Cumulus.DefaultLoVal;
			ThisYear.HighPress.Val = Cumulus.DefaultHiVal;
			ThisYear.LowPress.Val = Cumulus.DefaultLoVal;
			ThisYear.HighRainRate.Val = Cumulus.DefaultHiVal;
			ThisYear.HourlyRain.Val = Cumulus.DefaultHiVal;
			ThisYear.HighRain24Hours.Val = Cumulus.DefaultHiVal;
			ThisYear.DailyRain.Val = Cumulus.DefaultHiVal;
			ThisYear.MonthlyRain.Val = Cumulus.DefaultHiVal;
			ThisYear.HighHumidity.Val = Cumulus.DefaultHiVal;
			ThisYear.LowHumidity.Val = Cumulus.DefaultLoVal;
			ThisYear.HighHeatIndex.Val = Cumulus.DefaultHiVal;
			ThisYear.LowChill.Val = Cumulus.DefaultLoVal;
			ThisYear.HighMinTemp.Val = Cumulus.DefaultHiVal;
			ThisYear.LowMaxTemp.Val = Cumulus.DefaultLoVal;
			ThisYear.HighWindRun.Val = Cumulus.DefaultHiVal;
			ThisYear.LowDailyTempRange.Val = Cumulus.DefaultLoVal;
			ThisYear.HighDailyTempRange.Val = Cumulus.DefaultHiVal;

			// this Year highs and lows - timestamps
			ThisYear.HighGust.Ts = cumulus.defaultRecordTS;
			ThisYear.HighWind.Ts = cumulus.defaultRecordTS;
			ThisYear.HighTemp.Ts = cumulus.defaultRecordTS;
			ThisYear.LowTemp.Ts = cumulus.defaultRecordTS;
			ThisYear.HighAppTemp.Ts = cumulus.defaultRecordTS;
			ThisYear.LowAppTemp.Ts = cumulus.defaultRecordTS;
			ThisYear.HighFeelsLike.Ts = cumulus.defaultRecordTS;
			ThisYear.LowFeelsLike.Ts = cumulus.defaultRecordTS;
			ThisYear.HighHumidex.Ts = cumulus.defaultRecordTS;
			ThisYear.HighDewPoint.Ts = cumulus.defaultRecordTS;
			ThisYear.LowDewPoint.Ts = cumulus.defaultRecordTS;
			ThisYear.HighPress.Ts = cumulus.defaultRecordTS;
			ThisYear.LowPress.Ts = cumulus.defaultRecordTS;
			ThisYear.HighRainRate.Ts = cumulus.defaultRecordTS;
			ThisYear.HourlyRain.Ts = cumulus.defaultRecordTS;
			ThisYear.DailyRain.Ts = cumulus.defaultRecordTS;
			ThisYear.HighRain24Hours.Ts = cumulus.defaultRecordTS;
			ThisYear.MonthlyRain.Ts = cumulus.defaultRecordTS;
			ThisYear.HighHumidity.Ts = cumulus.defaultRecordTS;
			ThisYear.LowHumidity.Ts = cumulus.defaultRecordTS;
			ThisYear.HighHeatIndex.Ts = cumulus.defaultRecordTS;
			ThisYear.LowChill.Ts = cumulus.defaultRecordTS;
			ThisYear.HighMinTemp.Ts = cumulus.defaultRecordTS;
			ThisYear.LowMaxTemp.Ts = cumulus.defaultRecordTS;
			ThisYear.DailyRain.Ts = cumulus.defaultRecordTS;
			ThisYear.LowDailyTempRange.Ts = cumulus.defaultRecordTS;
			ThisYear.HighDailyTempRange.Ts = cumulus.defaultRecordTS;
		}

		public string GetWCloudURL(out string pwstring, DateTime timestamp)
		{
			pwstring = cumulus.WCloud.PW;
			StringBuilder sb = new StringBuilder($"https://api.weathercloud.net/v01/set?wid={cumulus.WCloud.ID}&key={pwstring}");

			//Temperature
			sb.Append("&tempin=" + (int) Math.Round(ConvertUnits.UserTempToC(IndoorTemperature) * 10));
			sb.Append("&temp=" + (int) Math.Round(ConvertUnits.UserTempToC(OutdoorTemperature) * 10));
			sb.Append("&chill=" + (int) Math.Round(ConvertUnits.UserTempToC(WindChill) * 10));
			sb.Append("&dew=" + (int) Math.Round(ConvertUnits.UserTempToC(OutdoorDewpoint) * 10));
			sb.Append("&heat=" + (int) Math.Round(ConvertUnits.UserTempToC(HeatIndex) * 10));

			// Humidity
			sb.Append("&humin=" + IndoorHumidity);
			sb.Append("&hum=" + OutdoorHumidity);

			// Wind
			sb.Append("&wspd=" + (int) Math.Round(ConvertUnits.UserWindToMS(WindLatest) * 10));
			sb.Append("&wspdhi=" + (int) Math.Round(ConvertUnits.UserWindToMS(RecentMaxGust) * 10));
			sb.Append("&wspdavg=" + (int) Math.Round(ConvertUnits.UserWindToMS(WindAverage) * 10));

			// Wind Direction
			sb.Append("&wdir=" + Bearing);
			sb.Append("&wdiravg=" + AvgBearing);

			// Pressure
			sb.Append("&bar=" + (int) Math.Round(ConvertUnits.UserPressToMB(Pressure) * 10));

			// rain
			sb.Append("&rain=" + (int) Math.Round(ConvertUnits.UserRainToMM(RainToday) * 10));
			sb.Append("&rainrate=" + (int) Math.Round(ConvertUnits.UserRainToMM(RainRate) * 10));

			// ET
			if (cumulus.WCloud.SendSolar && cumulus.Manufacturer == cumulus.DAVIS)
			{
				sb.Append("&et=" + (int) Math.Round(ConvertUnits.UserRainToMM(ET) * 10));
			}

			// solar
			if (cumulus.WCloud.SendSolar)
			{
				sb.Append("&solarrad=" + SolarRad * 10);
			}

			// uv
			if (cumulus.WCloud.SendUV)
			{
				sb.Append("&uvi=" + (int) Math.Round(UV * 10));
			}

			// aq
			if (cumulus.WCloud.SendAirQuality)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"&pm25={cumulus.airLinkDataOut.pm2p5:F0}");
							sb.Append($"&pm10={cumulus.airLinkDataOut.pm10:F0}");
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(cumulus.airLinkDataOut.pm2p5_24hr)}");
						}
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
						sb.Append($"&pm25={AirQuality1:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg1)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						sb.Append($"&pm25={AirQuality2:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg2)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						sb.Append($"&pm25={AirQuality3:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg3)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						sb.Append($"&pm25={AirQuality4:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg4)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
						sb.Append($"&pm25={CO2_pm2p5:F0}");
						sb.Append($"&pm10={CO2_pm10:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(CO2_pm2p5_24h)}");
						break;
				}
			}

			// soil moisture
			if (cumulus.WCloud.SendSoilMoisture)
			{
				// Weathercloud wants soil moisture in centibar. Davis supplies this, but Ecowitt provide a percentage
				int moist = 0;

				switch (cumulus.WCloud.SoilMoistureSensor)
				{
					case 1:
						moist = SoilMoisture1;
						break;
					case 2:
						moist = SoilMoisture2;
						break;
					case 3:
						moist = SoilMoisture3;
						break;
					case 4:
						moist = SoilMoisture4;
						break;
					case 5:
						moist = SoilMoisture5;
						break;
					case 6:
						moist = SoilMoisture6;
						break;
					case 7:
						moist = SoilMoisture7;
						break;
					case 8:
						moist = SoilMoisture8;
						break;
					case 9:
						moist = SoilMoisture9;
						break;
					case 10:
						moist = SoilMoisture10;
						break;
					case 11:
						moist = SoilMoisture11;
						break;
					case 12:
						moist = SoilMoisture12;
						break;
					case 13:
						moist = SoilMoisture13;
						break;
					case 14:
						moist = SoilMoisture14;
						break;
					case 15:
						moist = SoilMoisture15;
						break;
					case 16:
						moist = SoilMoisture16;
						break;
				}

				if (cumulus.Manufacturer == cumulus.EW)
				{
					// very! approximate conversion from percentage to cb
					moist = (100 - SoilMoisture1) * 2;
				}

				sb.Append($"&soilmoist={moist}");
			}

			// leaf wetness
			if (cumulus.WCloud.SendLeafWetness)
			{
				// Weathercloud wants soil moisture in centibar. Davis supplies this, but Ecowitt provide a percentage
				double wet = 0;

				switch (cumulus.WCloud.LeafWetnessSensor)
				{
					case 1:
						wet = LeafWetness1;
						break;
					case 2:
						wet = LeafWetness2;
						break;
					case 3:
						wet = LeafWetness3;
						break;
					case 4:
						wet = LeafWetness4;
						break;
					case 5:
						wet = LeafWetness5;
						break;
					case 6:
						wet = LeafWetness6;
						break;
					case 7:
						wet = LeafWetness7;
						break;
					case 8:
						wet = LeafWetness8;
						break;
				}

				sb.Append($"&leafwet={wet.ToString(cumulus.LeafWetFormat)}");
			}

			// time - UTC
			sb.Append("&time=" + timestamp.ToUniversalTime().ToString("HHmm"));

			// date - UTC
			sb.Append("&date=" + timestamp.ToUniversalTime().ToString("yyyyMMdd"));

			// software identification
			//sb.Append("&type=291&ver=" + cumulus.Version);
			sb.Append($"&software=Cumulus_MX_v{cumulus.Version}&softwareid=142787ebe716");

			return sb.ToString();
		}

		public string GetAwekasURLv4(out string pwstring, DateTime timestamp)
		{
			var InvC = new CultureInfo("");
			string sep = ";";

			int presstrend;

			// password is passed as a MD5 hash - not very secure, but better than plain text I guess
			pwstring = Utils.GetMd5String(cumulus.AWEKAS.PW);

			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.Units.Press)
			{
				case 0:
				case 1:
					threeHourlyPressureChangeMb = presstrendval * 3;
					break;
				case 2:
					threeHourlyPressureChangeMb = presstrendval * 3 / 0.0295333727;
					break;
			}

			if (threeHourlyPressureChangeMb > 6) presstrend = 2;
			else if (threeHourlyPressureChangeMb > 3.5) presstrend = 2;
			else if (threeHourlyPressureChangeMb > 1.5) presstrend = 1;
			else if (threeHourlyPressureChangeMb > 0.1) presstrend = 1;
			else if (threeHourlyPressureChangeMb > -0.1) presstrend = 0;
			else if (threeHourlyPressureChangeMb > -1.5) presstrend = -1;
			else if (threeHourlyPressureChangeMb > -3.5) presstrend = -1;
			else if (threeHourlyPressureChangeMb > -6) presstrend = -2;
			else
				presstrend = -2;

			double AvgTemp;
			if (tempsamplestoday > 0)
				AvgTemp = TempTotalToday / tempsamplestoday;
			else
				AvgTemp = 0;

			StringBuilder sb = new StringBuilder("http://data.awekas.at/eingabe_pruefung.php?");

			var started = false;

			// indoor temp/humidity
			if (cumulus.AWEKAS.SendIndoor)
			{
				sb.Append("indoortemp=" + ConvertUnits.UserTempToC(IndoorTemperature).ToString("F1", InvC));
				sb.Append("&indoorhumidity=" + IndoorHumidity);
				started = true;
			}

			if (cumulus.AWEKAS.SendSoilTemp)
			{
				for (var i = 1; i <= 4; i++)
				{
					if (started) sb.Append('&'); else started = true;
					sb.Append($"soiltemp{i}=" + ConvertUnits.UserTempToC(SoilTemp[i]).ToString("F1", InvC));
				}
			}

			if (cumulus.AWEKAS.SendSoilMoisture)
			{
				if (started) sb.Append('&'); else started = true;
				sb.Append("soilmoisture1=" + SoilMoisture1);
				sb.Append("&soilmoisture2=" + SoilMoisture2);
				sb.Append("&soilmoisture3=" + SoilMoisture3);
				sb.Append("&soilmoisture4=" + SoilMoisture4);
			}

			if (cumulus.AWEKAS.SendLeafWetness)
			{
				if (started) sb.Append('&'); else started = true;
				sb.Append("leafwetness1=" + LeafWetness1.ToString(cumulus.LeafWetFormat));
				sb.Append("&leafwetness2=" + LeafWetness2.ToString(cumulus.LeafWetFormat));
				sb.Append("&leafwetness3=" + LeafWetness3.ToString(cumulus.LeafWetFormat));
				sb.Append("&leafwetness4=" + LeafWetness4.ToString(cumulus.LeafWetFormat));
			}

			if (cumulus.AWEKAS.SendAirQuality)
			{
				if (started) sb.Append('&'); else started = true;

				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"AqPM1={cumulus.airLinkDataOut.pm1.ToString("F1", InvC)}");
							sb.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5.ToString("F1", InvC)}");
							sb.Append($"&AqPM10={cumulus.airLinkDataOut.pm10.ToString("F1", InvC)}");
							sb.Append($"&AqPM2.5_avg_24h={cumulus.airLinkDataOut.pm2p5_24hr.ToString("F1", InvC)}");
							sb.Append($"&AqPM10_avg_24h={cumulus.airLinkDataOut.pm10_24hr.ToString("F1", InvC)}");
						}
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
						sb.Append($"AqPM2.5={AirQuality1.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg1.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						sb.Append($"AqPM2.5={AirQuality2.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg2.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						sb.Append($"AqPM2.5={AirQuality3.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg3.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						sb.Append($"AqPM2.5={AirQuality4.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg4.ToString("F1", InvC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
						sb.Append($"AqPM2.5={CO2_pm2p5.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={CO2_pm2p5_24h.ToString("F1", InvC)}");
						sb.Append($"&AqPM10={CO2_pm10.ToString("F1", InvC)}");
						sb.Append($"&AqPM10_avg_24h={CO2_pm10_24h.ToString("F1", InvC)}");
						break;
				}
			}

			if (started) sb.Append('&');
			sb.Append("output=json&val=");

			//
			// Start of val
			//
			sb.Append(cumulus.AWEKAS.ID + sep);                                             // 1
			sb.Append(pwstring + sep);                                                      // 2
			sb.Append(timestamp.ToString("dd'.'MM'.'yyyy';'HH':'mm") + sep);                // 3 + 4
			sb.Append(ConvertUnits.UserTempToC(OutdoorTemperature).ToString("F1", InvC) + sep);   // 5
			sb.Append(OutdoorHumidity + sep);                                               // 6
			sb.Append(ConvertUnits.UserPressToMB(Pressure).ToString("F1", InvC) + sep);           // 7
			sb.Append(ConvertUnits.UserRainToMM(RainSinceMidnight).ToString("F1", InvC) + sep);   // 8   - was RainToday in v2
			sb.Append(ConvertUnits.UserWindToKPH(WindAverage).ToString("F1", InvC) + sep);        // 9
			sb.Append(AvgBearing + sep);                                                    // 10
			sb.Append(sep + sep + sep);                                                     // 11/12/13 - condition and warning, snow height
			sb.Append(cumulus.AWEKAS.Lang + sep);                                           // 14
			sb.Append(presstrend + sep);                                                    // 15
			sb.Append(ConvertUnits.UserWindToKPH(RecentMaxGust).ToString("F1", InvC) + sep);      // 16

			if (cumulus.AWEKAS.SendSolar)
				sb.Append(SolarRad.ToString("F1", InvC) + sep);                             // 17
			else
				sb.Append(sep);

			if (cumulus.AWEKAS.SendUV)
				sb.Append(UV.ToString("F1", InvC) + sep);                                   // 18
			else
				sb.Append(sep);

			if (cumulus.AWEKAS.SendSolar)
			{
				if (cumulus.StationType == StationTypes.FineOffsetSolar)
					sb.Append(LightValue.ToString("F0", InvC) + sep);                       // 19
				else
					sb.Append(sep);

				sb.Append(SunshineHours.ToString("F2", InvC) + sep);                        // 20
			}
			else
			{
				sb.Append(sep + sep);
			}

			if (cumulus.AWEKAS.SendSoilTemp)
				sb.Append(ConvertUnits.UserTempToC(SoilTemp[1]).ToString("F1", InvC) + sep);        // 21
			else
				sb.Append(sep);

			sb.Append(ConvertUnits.UserRainToMM(RainRate).ToString("F1", InvC) + sep);            // 22
			sb.Append("Cum_" + cumulus.Version + sep);                                      // 23
			sb.Append(sep + sep);                                                           // 24/25 location for mobile
			sb.Append(ConvertUnits.UserTempToC(HiLoToday.LowTemp).ToString("F1", InvC) + sep);    // 26
			sb.Append(ConvertUnits.UserTempToC(AvgTemp).ToString("F1", InvC) + sep);              // 27
			sb.Append(ConvertUnits.UserTempToC(HiLoToday.HighTemp).ToString("F1", InvC) + sep);   // 28
			sb.Append(ConvertUnits.UserTempToC(ThisMonth.LowTemp.Val).ToString("F1", InvC) + sep);// 29
			sb.Append(sep);                                                                 // 30 avg temp this month
			sb.Append(ConvertUnits.UserTempToC(ThisMonth.HighTemp.Val).ToString("F1", InvC) + sep);// 31
			sb.Append(ConvertUnits.UserTempToC(ThisYear.LowTemp.Val).ToString("F1", InvC) + sep); // 32
			sb.Append(sep);                                                                 // 33 avg temp this year
			sb.Append(ConvertUnits.UserTempToC(ThisYear.HighTemp.Val).ToString("F1", InvC) + sep);// 34
			sb.Append(HiLoToday.LowHumidity + sep);                                         // 35
			sb.Append(sep);                                                                 // 36 avg hum today
			sb.Append(HiLoToday.HighHumidity + sep);                                        // 37
			sb.Append(ThisMonth.LowHumidity.Val + sep);                                     // 38
			sb.Append(sep);                                                                 // 39 avg hum this month
			sb.Append(ThisMonth.HighHumidity.Val + sep);                                    // 40
			sb.Append(ThisYear.LowHumidity.Val + sep);                                      // 41
			sb.Append(sep);                                                                 // 42 avg hum this year
			sb.Append(ThisYear.HighHumidity.Val + sep);                                     // 43
			sb.Append(ConvertUnits.UserPressToMB(HiLoToday.LowPress).ToString("F1", InvC) + sep); // 44
			sb.Append(sep);                                                                 // 45 avg press today
			sb.Append(ConvertUnits.UserPressToMB(HiLoToday.HighPress).ToString("F1", InvC) + sep);// 46
			sb.Append(ConvertUnits.UserPressToMB(ThisMonth.LowPress.Val).ToString("F1", InvC) + sep); // 47
			sb.Append(sep);                                                                 // 48 avg press this month
			sb.Append(ConvertUnits.UserPressToMB(ThisMonth.HighPress.Val).ToString("F1", InvC) + sep); // 49
			sb.Append(ConvertUnits.UserPressToMB(ThisYear.LowPress.Val).ToString("F1", InvC) + sep); // 50
			sb.Append(sep);                                                                 // 51 avg press this year
			sb.Append(ConvertUnits.UserPressToMB(ThisYear.HighPress.Val).ToString("F1", InvC) + sep); // 52
			sb.Append(sep + sep);                                                           // 53/54 min/avg wind today
			sb.Append(ConvertUnits.UserWindToKPH(HiLoToday.HighWind).ToString("F1", InvC) + sep); // 55
			sb.Append(sep + sep);                                                           // 56/57 min/avg wind this month
			sb.Append(ConvertUnits.UserWindToKPH(ThisMonth.HighWind.Val).ToString("F1", InvC) + sep); // 58
			sb.Append(sep + sep);                                                           // 59/60 min/avg wind this year
			sb.Append(ConvertUnits.UserWindToKPH(ThisYear.HighWind.Val).ToString("F1", InvC) + sep); // 61
			sb.Append(sep + sep);                                                           // 62/63 min/avg gust today
			sb.Append(ConvertUnits.UserWindToKPH(HiLoToday.HighGust).ToString("F1", InvC) + sep); // 64
			sb.Append(sep + sep);                                                           // 65/66 min/avg gust this month
			sb.Append(ConvertUnits.UserWindToKPH(ThisMonth.HighGust.Val).ToString("F1", InvC) + sep); // 67
			sb.Append(sep + sep);                                                           // 68/69 min/avg gust this year
			sb.Append(ConvertUnits.UserWindToKPH(ThisYear.HighGust.Val).ToString("F1", InvC) + sep); // 70
			sb.Append(sep + sep + sep);                                                     // 71/72/73 avg wind bearing today/month/year
			sb.Append(ConvertUnits.UserRainToMM(RainLast24Hour).ToString("F1", InvC) + sep);      // 74
			sb.Append(ConvertUnits.UserRainToMM(RainMonth).ToString("F1", InvC) + sep);           // 75
			sb.Append(ConvertUnits.UserRainToMM(RainYear).ToString("F1", InvC) + sep);            // 76
			sb.Append(sep);                                                                 // 77 avg rain rate today
			sb.Append(ConvertUnits.UserRainToMM(HiLoToday.HighRainRate).ToString("F1", InvC) + sep); // 78
			sb.Append(sep);                                                                 // 79 avg rain rate this month
			sb.Append(ConvertUnits.UserRainToMM(ThisMonth.HighRainRate.Val).ToString("F1", InvC) + sep); // 80
			sb.Append(sep);                                                                 // 81 avg rain rate this year
			sb.Append(ConvertUnits.UserRainToMM(ThisYear.HighRainRate.Val).ToString("F1", InvC) + sep); // 82
			sb.Append(sep);                                                                 // 83 avg solar today
			if (cumulus.AWEKAS.SendSolar)
				sb.Append(HiLoToday.HighSolar.ToString("F1", InvC));                        // 84
			else
				sb.Append(sep);

			sb.Append(sep + sep);                                                           // 85/86 avg/high solar this month
			sb.Append(sep + sep);                                                           // 87/88 avg/high solar this year
			sb.Append(sep);                                                                 // 89 avg uv today

			if (cumulus.AWEKAS.SendUV)
				sb.Append(HiLoToday.HighUv.ToString("F1", InvC));                           // 90
			else
				sb.Append(sep);

			sb.Append(sep + sep);                                                           // 91/92 avg/high uv this month
			sb.Append(sep + sep);                                                           // 93/94 avg/high uv this year
			sb.Append(sep + sep + sep + sep + sep + sep);                                   // 95/96/97/98/99/100 avg/max lux today/month/year
			sb.Append(sep + sep);                                                           // 101/102 sun hours this month/year
			sb.Append(sep + sep + sep + sep + sep + sep + sep + sep + sep);                 // 103-111 min/avg/max Soil temp today/month/year
																							//
																							// End of val fixed structure
																							//

			return sb.ToString();
		}


		public string GetWundergroundURL(out string pwstring, DateTime timestamp, bool catchup)
		{
			// API documentation: https://support.weather.com/s/article/PWS-Upload-Protocol?language=en_US

			var invC = new CultureInfo("");

			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder(1024);
			if (cumulus.Wund.RapidFireEnabled && !catchup)
			{
				URL.Append("http://rtupdate.wunderground.com/weatherstation/updateweatherstation.php?ID=");
			}
			else
			{
				URL.Append("http://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?ID=");
			}

			pwstring = $"&PASSWORD={cumulus.Wund.PW}";
			URL.Append(cumulus.Wund.ID);
			URL.Append(pwstring);
			URL.Append($"&dateutc={dateUTC}");
			StringBuilder Data = new StringBuilder(1024);
			if (cumulus.Wund.SendAverage)
			{
				// send average speed and bearing
				Data.Append($"&winddir={AvgBearing}&windspeedmph={WindMPHStr(WindAverage)}");
			}
			else
			{
				// send "instantaneous" speed (i.e. latest) and bearing
				Data.Append($"&winddir={Bearing}&windspeedmph={WindMPHStr(WindLatest)}");
			}
			Data.Append($"&windgustmph={WindMPHStr(RecentMaxGust)}");
			// may not strictly be a 2 min average!
			Data.Append($"&windspdmph_avg2m={WindMPHStr(WindAverage)}");
			Data.Append($"&winddir_avg2m={AvgBearing}");
			Data.Append($"&humidity={OutdoorHumidity}");
			Data.Append($"&tempf={TempFstr(OutdoorTemperature)}");
			Data.Append($"&rainin={RainINstr(RainLastHour)}");
			Data.Append("&dailyrainin=");
			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data.Append(RainINstr(RainToday));
			}
			else
			{
				Data.Append(RainINstr(RainSinceMidnight));
			}
			Data.Append($"&baromin={PressINstr(Pressure)}");
			Data.Append($"&dewptf={TempFstr(OutdoorDewpoint)}");
			if (cumulus.Wund.SendUV)
				Data.Append($"&UV={UV.ToString(cumulus.UVFormat, invC)}");
			if (cumulus.Wund.SendSolar)
				Data.Append($"&solarradiation={SolarRad:F0}");
			if (cumulus.Wund.SendIndoor)
			{
				Data.Append($"&indoortempf={TempFstr(IndoorTemperature)}");
				Data.Append($"&indoorhumidity={IndoorHumidity}");
			}
			// Davis soil and leaf sensors
			if (cumulus.Wund.SendSoilTemp1)
				Data.Append($"&soiltempf={TempFstr(SoilTemp[1])}");
			if (cumulus.Wund.SendSoilTemp2)
				Data.Append($"&soiltempf2={TempFstr(SoilTemp[2])}");
			if (cumulus.Wund.SendSoilTemp3)
				Data.Append($"&soiltempf3={TempFstr(SoilTemp[3])}");
			if (cumulus.Wund.SendSoilTemp4)
				Data.Append($"&soiltempf4={TempFstr(SoilTemp[4])}");

			if (cumulus.Wund.SendSoilMoisture1)
				Data.Append($"&soilmoisture={SoilMoisture1}");
			if (cumulus.Wund.SendSoilMoisture2)
				Data.Append($"&soilmoisture2={SoilMoisture2}");
			if (cumulus.Wund.SendSoilMoisture3)
				Data.Append($"&soilmoisture3={SoilMoisture3}");
			if (cumulus.Wund.SendSoilMoisture4)
				Data.Append($"&soilmoisture4={SoilMoisture4}");

			if (cumulus.Wund.SendLeafWetness1)
				Data.Append($"&leafwetness={LeafWetness1.ToString(cumulus.LeafWetFormat)}");
			if (cumulus.Wund.SendLeafWetness2)
				Data.Append($"&leafwetness2={LeafWetness2.ToString(cumulus.LeafWetFormat)}");

			if (cumulus.Wund.SendAirQuality && cumulus.StationOptions.PrimaryAqSensor > (int) Cumulus.PrimaryAqSensor.Undefined)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							Data.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5:F1}&AqPM10={cumulus.airLinkDataOut.pm10.ToString("F1", invC)}");
						}
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt1:
						Data.Append($"&AqPM2.5={AirQuality1.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt2:
						Data.Append($"&AqPM2.5={AirQuality2.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt3:
						Data.Append($"&AqPM2.5={AirQuality3.ToString("F1", invC)}");
						break;
					case (int) Cumulus.PrimaryAqSensor.Ecowitt4:
						Data.Append($"&AqPM2.5={AirQuality4.ToString("F1", invC)}");
						break;
				}
			}

			if (cumulus.Wund.SendExtraTemp1 > 0 && cumulus.Wund.SendExtraTemp1 <= 10)
			{
				Data.Append($"&temp2f={TempFstr(ExtraTemp[cumulus.Wund.SendExtraTemp1])}");
			}
			if (cumulus.Wund.SendExtraTemp2 > 0 && cumulus.Wund.SendExtraTemp2 <= 10)
			{
				Data.Append($"&temp3f={TempFstr(ExtraTemp[cumulus.Wund.SendExtraTemp2])}");
			}
			if (cumulus.Wund.SendExtraTemp3 > 0 && cumulus.Wund.SendExtraTemp3 <= 10)
			{
				Data.Append($"&temp4f={TempFstr(ExtraTemp[cumulus.Wund.SendExtraTemp3])}");
			}
			if (cumulus.Wund.SendExtraTemp4 > 0 && cumulus.Wund.SendExtraTemp4 <= 10)
			{
				Data.Append($"&temp5f={TempFstr(ExtraTemp[cumulus.Wund.SendExtraTemp4])}");
			}

			Data.Append($"&softwaretype=Cumulus%20v{cumulus.Version}");
			Data.Append("&action=updateraw");
			if (cumulus.Wund.RapidFireEnabled && !catchup)
				Data.Append("&realtime=1&rtfreq=5");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}

		// Documentation on the API can be found here...
		// https://community.windy.com/topic/8168/report-your-weather-station-data-to-windy
		//
		public string GetWindyURL(out string apistring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH':'mm':'ss");
			StringBuilder URL = new StringBuilder("https://stations.windy.com/pws/update/", 1024);

			apistring = cumulus.Windy.ApiKey;

			URL.Append(cumulus.Windy.ApiKey);
			URL.Append("?station=" + cumulus.Windy.StationIdx);
			URL.Append("&dateutc=" + dateUTC);
			StringBuilder Data = new StringBuilder(1024);
			Data.Append("&winddir=" + AvgBearing);
			Data.Append("&wind=" + WindMSStr(WindAverage));
			Data.Append("&gust=" + WindMSStr(RecentMaxGust));
			Data.Append("&temp=" + TempCstr(OutdoorTemperature));
			Data.Append("&precip=" + RainMMstr(RainLastHour));
			Data.Append("&pressure=" + PressPAstr(Pressure));
			Data.Append("&dewpoint=" + TempCstr(OutdoorDewpoint));
			Data.Append("&humidity=" + OutdoorHumidity);

			if (cumulus.Windy.SendUV)
				Data.Append("&uv=" + UV.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			if (cumulus.Windy.SendSolar)
				Data.Append("&solarradiation=" + SolarRad.ToString("F0"));

			URL.Append(Data);

			return URL.ToString();
		}


		// Documentation on the API can be found here...
		// https://stations.windguru.cz/upload_api.php
		//
		public string GetWindGuruURL(out string uidstring, DateTime timestamp)
		{
			var InvC = new CultureInfo("");

			string salt = timestamp.ToUnixTime().ToString();
			string hash = Utils.GetMd5String(salt + cumulus.WindGuru.ID + cumulus.WindGuru.PW);

			uidstring = cumulus.WindGuru.ID;

			int numvalues = 0;
			double totalwind = 0;
			double maxwind = 0;
			double minwind = 999;
			for (int i = 0; i < MaxWindRecent; i++)
			{
				if (WindRecent[i].Timestamp >= DateTime.Now.AddMinutes(-cumulus.WindGuru.Interval))
				{
					numvalues++;
					totalwind += WindRecent[i].Gust;

					if (WindRecent[i].Gust > maxwind)
					{
						maxwind = WindRecent[i].Gust;
					}

					if (WindRecent[i].Gust < minwind)
					{
						minwind = WindRecent[i].Gust;
					}
				}
			}
			// average the values
			double avgwind = totalwind / numvalues;

			StringBuilder URL = new StringBuilder("http://www.windguru.cz/upload/api.php?", 1024);

			URL.Append("uid=" + HttpUtility.UrlEncode(cumulus.WindGuru.ID));
			URL.Append("&salt=" + salt);
			URL.Append("&hash=" + hash);
			URL.Append("&interval=" + cumulus.WindGuru.Interval * 60);
			URL.Append("&wind_avg=" + ConvertUnits.UserWindToKnots(avgwind).ToString("F1", InvC));
			URL.Append("&wind_max=" + ConvertUnits.UserWindToKnots(maxwind).ToString("F1", InvC));
			URL.Append("&wind_min=" + ConvertUnits.UserWindToKnots(minwind).ToString("F1", InvC));
			URL.Append("&wind_direction=" + AvgBearing);
			URL.Append("&temperature=" + ConvertUnits.UserTempToC(OutdoorTemperature).ToString("F1", InvC));
			URL.Append("&rh=" + OutdoorHumidity);
			URL.Append("&mslp=" + ConvertUnits.UserPressureToHPa(Pressure).ToString("F1", InvC));
			if (cumulus.WindGuru.SendRain)
			{
				URL.Append("&precip=" + ConvertUnits.UserRainToMM(RainLastHour).ToString("F1", InvC));
				URL.Append("&precip_interval=3600");
			}

			return URL.ToString();
		}

		public string GetOpenWeatherMapData(DateTime timestamp)
		{
			StringBuilder sb = new StringBuilder($"[{{\"station_id\":\"{cumulus.OpenWeatherMap.ID}\",");
			var invC = new CultureInfo("");

			sb.Append($"\"dt\":{Utils.ToUnixTime(timestamp)},");
			sb.Append($"\"temperature\":{Math.Round(ConvertUnits.UserTempToC(OutdoorTemperature), 1).ToString(invC)},");
			sb.Append($"\"wind_deg\":{AvgBearing},");
			sb.Append($"\"wind_speed\":{Math.Round(ConvertUnits.UserWindToMS(WindAverage), 1).ToString(invC)},");
			sb.Append($"\"wind_gust\":{Math.Round(ConvertUnits.UserWindToMS(RecentMaxGust), 1).ToString(invC)},");
			sb.Append($"\"pressure\":{Math.Round(ConvertUnits.UserPressureToHPa(Pressure), 1).ToString(invC)},");
			sb.Append($"\"humidity\":{OutdoorHumidity},");
			sb.Append($"\"rain_1h\":{Math.Round(ConvertUnits.UserRainToMM(RainLastHour), 1).ToString(invC)},");
			sb.Append($"\"rain_24h\":{Math.Round(ConvertUnits.UserRainToMM(RainLast24Hour), 1).ToString(invC)}");
			sb.Append("}]");

			return sb.ToString();
		}

		private string PressINstr(double pressure)
		{
			return ConvertUnits.UserPressToIN(pressure).ToString("F3", CultureInfo.InvariantCulture);
		}

		private string PressPAstr(double pressure)
		{
			// return value to 0.1 hPa
			return (ConvertUnits.UserPressToMB(pressure) / 100).ToString("F4", CultureInfo.InvariantCulture);
		}

		private string WindMPHStr(double wind)
		{
			var windMPH = ConvertUnits.UserWindToMPH(wind);
			if (cumulus.StationOptions.RoundWindSpeed)
				windMPH = Math.Round(windMPH);

			return windMPH.ToString("F1", CultureInfo.InvariantCulture);
		}

		private string WindMSStr(double wind)
		{
			var windMS = ConvertUnits.UserWindToMS(wind);
			if (cumulus.StationOptions.RoundWindSpeed)
				windMS = Math.Round(windMS);

			return windMS.ToString("F1", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to inches for WU etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		private string RainINstr(double rain)
		{
			return ConvertUnits.UserRainToIN(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to mm for APIs etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		private string RainMMstr(double rain)
		{
			return ConvertUnits.UserRainToMM(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert temp in user units to F for WU etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		private string TempFstr(double temp)
		{
			return ConvertUnits.UserTempToF(temp).ToString("F1", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert temp in user units to C for APIs etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		private string TempCstr(double temp)
		{
			return ConvertUnits.UserTempToC(temp).ToString("F1", CultureInfo.InvariantCulture);
		}

		public string GetPWSURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder("http://www.pwsweather.com/pwsupdate/pwsupdate.php?ID=", 1024);
			StringBuilder Data = new StringBuilder(1024);

			pwstring = "&PASSWORD=" + HttpUtility.UrlEncode(cumulus.PWS.PW);
			URL.Append(cumulus.PWS.ID + pwstring);
			URL.Append("&dateutc=" + dateUTC);


			// send average speed and bearing
			Data.Append("&winddir=" + AvgBearing);
			Data.Append("&windspeedmph=" + WindMPHStr(WindAverage));
			Data.Append("&windgustmph=" + WindMPHStr(RecentMaxGust));
			Data.Append("&humidity=" + OutdoorHumidity);
			Data.Append("&tempf=" + TempFstr(OutdoorTemperature));
			Data.Append("&rainin=" + RainINstr(RainLastHour));
			Data.Append("&dailyrainin=");
			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data.Append(RainINstr(RainToday));
			}
			else
			{
				Data.Append(RainINstr(RainSinceMidnight));
			}
			Data.Append("&baromin=" + PressINstr(Pressure));
			Data.Append("&dewptf=" + TempFstr(OutdoorDewpoint));
			if (cumulus.PWS.SendUV)
			{
				Data.Append("&UV=" + UV.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			}

			if (cumulus.PWS.SendSolar)
			{
				Data.Append("&solarradiation=" + SolarRad.ToString("F0"));
			}

			Data.Append("&softwaretype=Cumulus%20v" + cumulus.Version);
			Data.Append("&action=updateraw");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}

		public string GetWOWURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder("http://wow.metoffice.gov.uk/automaticreading?siteid=", 1024);

			pwstring = "&siteAuthenticationKey=" + cumulus.WOW.PW;
			URL.Append(cumulus.WOW.ID);
			URL.Append(pwstring);
			URL.Append("&dateutc=" + dateUTC);

			StringBuilder Data = new StringBuilder(1024);

			// send average speed and bearing
			Data.Append("&winddir=" + AvgBearing);
			Data.Append("&windspeedmph=" + WindMPHStr(WindAverage));
			Data.Append("&windgustmph=" + WindMPHStr(RecentMaxGust));
			Data.Append("&humidity=" + OutdoorHumidity);
			Data.Append("&tempf=" + TempFstr(OutdoorTemperature));
			Data.Append("&rainin=" + RainINstr(RainLastHour));
			Data.Append("&dailyrainin=");
			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data.Append(RainINstr(RainToday));
			}
			else
			{
				Data.Append(RainINstr(RainSinceMidnight));
			}
			Data.Append("&baromin=" + PressINstr(Pressure));
			Data.Append("&dewptf=" + TempFstr(OutdoorDewpoint));
			if (cumulus.WOW.SendUV)
			{
				Data.Append("&UV=" + UV.ToString(cumulus.UVFormat, CultureInfo.InvariantCulture));
			}
			if (cumulus.WOW.SendSolar)
			{
				Data.Append("&solarradiation=" + SolarRad.ToString("F0"));
			}
			if (cumulus.WOW.SendSoilTemp)
			{
				Data.Append($"&soiltempf=" + TempFstr(SoilTemp[cumulus.WOW.SoilTempSensor]));
			}

			Data.Append("&softwaretype=Cumulus%20v" + cumulus.Version);
			Data.Append("&action=updateraw");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}

		private string alltimejsonformat(AllTimeRec item, string unit, string valueformat, string dateformat)
		{
			return $"[\"{item.Desc}\",\"{item.GetValString(valueformat)} {unit}\",\"{item.GetTsString(dateformat)}\"]";
		}

		public string GetTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 2048);

			json.Append(alltimejsonformat(AllTime.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighHumidex, "&nbsp;", cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AllTime.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D") + ",");
			json.Append(alltimejsonformat(AllTime.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(alltimejsonformat(AllTime.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		private string monthlyjsonformat(AllTimeRec item, string unit, string valueformat, string dateformat)
		{
			return $"[\"{item.Desc}\",\"{item.GetValString(valueformat)} {unit}\",\"{item.GetTsString(dateformat)}\"]";
		}

		public string GetMonthlyTempRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyHumRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyPressRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyWindRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyRainRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(MonthlyRecs[month].LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		private string monthyearjsonformat(AllTimeRec value, string unit, string valueformat, string dateformat)
		{
			return $"[\"{value.Desc}\",\"{value.GetValString(valueformat)} {unit}\",\"{value.GetTsString(dateformat)}\"]";
		}

		public string GetThisMonthTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(ThisMonth.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisMonth.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisMonth.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisMonth.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(ThisMonth.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			//json.Append(monthyearjsonformat(ThisMonth.WetMonth.Desc, month, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			//json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisMonth.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(ThisYear.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisYear.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisYear.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisYear.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(ThisYear.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(ThisYear.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraTemp()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 11; sensor++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.ExtraTempCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(ExtraTemp[sensor].ToString(cumulus.TempFormat));
					json.Append("\",\"&deg;");
					json.Append(cumulus.Units.TempText[1].ToString());
					json.Append("\"],");
				}
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetUserTemp()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 9; sensor++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.UserTempCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(UserTemp[sensor].ToString(cumulus.TempFormat));
					json.Append("\",\"&deg;");
					json.Append(cumulus.Units.TempText[1].ToString());
					json.Append("\"],");
				}
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraHum()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 11; sensor++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.ExtraHumCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(ExtraHum[sensor].ToString(cumulus.HumFormat));
					json.Append("\",\"%\"],");
				}
			}
			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraDew()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 11; sensor++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.ExtraDPCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(ExtraDewPoint[sensor].ToString(cumulus.TempFormat));
					json.Append("\",\"&deg;");
					json.Append(cumulus.Units.TempText[1].ToString());
					json.Append("\"],");
				}
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetSoilTemp()
		{
			var json = new StringBuilder("{\"data\":[", 2048);

			for (var i = 1; i <= 16; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i - 1, true))
				{
					json.Append($"[\"{cumulus.Trans.SoilTempCaptions[i - 1]}\",\"{SoilTemp[i].ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
				}
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetSoilMoisture()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(0, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[0]}\",\"{SoilMoisture1:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(1, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[1]}\",\"{SoilMoisture2:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(2, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[2]}\",\"{SoilMoisture3:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(3, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[3]}\",\"{SoilMoisture4:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(4, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[4]}\",\"{SoilMoisture5:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(5, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[5]}\",\"{SoilMoisture6:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(6, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[6]}\",\"{SoilMoisture7:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(7, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[7]}\",\"{SoilMoisture8:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(8, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[8]}\",\"{SoilMoisture9:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(9, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[9]}\",\"{SoilMoisture10:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(10, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[10]}\",\"{SoilMoisture11:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(11, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[11]}\",\"{SoilMoisture12:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(12, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[12]}\",\"{SoilMoisture13:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(13, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[13]}\",\"{SoilMoisture14:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(14, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[14]}\",\"{SoilMoisture15:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"],");
			if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(15, true))
				json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[15]}\",\"{SoilMoisture16:F0}\",\"{cumulus.Units.SoilMoistureUnitText}\"]");

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetAirQuality(bool local)
		{
			var json = new StringBuilder("{\"data\":[", 1024);
			if (cumulus.GraphOptions.Visible.AqSensor.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.AqSensor.Pm.ValVisible(0, local))
					json.Append($"[\"{cumulus.Trans.AirQualityCaptions[0]}\",\"{AirQuality1:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.Pm.ValVisible(1, local))
					json.Append($"[\"{cumulus.Trans.AirQualityCaptions[1]}\",\"{AirQuality2:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.Pm.ValVisible(2, local))
					json.Append($"[\"{cumulus.Trans.AirQualityCaptions[2]}\",\"{AirQuality3:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.Pm.ValVisible(3, local))
					json.Append($"[\"{cumulus.Trans.AirQualityCaptions[3]}\",\"{AirQuality4:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.PmAvg.ValVisible(0, local))
					json.Append($"[\"{cumulus.Trans.AirQualityAvgCaptions[0]}\",\"{AirQualityAvg1:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.PmAvg.ValVisible(1, local))
					json.Append($"[\"{cumulus.Trans.AirQualityAvgCaptions[1]}\",\"{AirQualityAvg2:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.PmAvg.ValVisible(2, local))
					json.Append($"[\"{cumulus.Trans.AirQualityAvgCaptions[2]}\",\"{AirQualityAvg3:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.AqSensor.PmAvg.ValVisible(3, local))
					json.Append($"[\"{cumulus.Trans.AirQualityAvgCaptions[3]}\",\"{AirQualityAvg4:F1}\",\"{cumulus.Units.AirQualityUnitText}\"]");
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetCO2sensor(bool local)
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			if (cumulus.GraphOptions.Visible.CO2Sensor.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_CurrentCaption}\",\"{CO2}\",\"{cumulus.Units.CO2UnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_24HourCaption}\",\"{CO2_24h}\",\"{cumulus.Units.CO2UnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm2p5Caption}\",\"{CO2_pm2p5:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm2p5_24hrCaption}\",\"{CO2_pm2p5_24h:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm10Caption}\",\"{CO2_pm10:F1}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm10_24hrCaption}\",\"{CO2_pm10_24h:F1}\",\"{cumulus.Units.AirQualityUnitText}\"]");
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetLightning()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"Distance to last strike\",\"{(LightningDistance == -1 ? "-" : LightningDistance.ToString(cumulus.WindRunFormat))}\",\"{cumulus.Units.WindRunText}\"],");
			json.Append($"[\"Time of last strike\",\"{(DateTime.Equals(LightningTime, DateTime.MinValue) ? "-" : LightningTime.ToString("g"))}\",\"\"],");
			json.Append($"[\"Number of strikes today\",\"{LightningStrikesToday}\",\"\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf8(bool local)
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.GraphOptions.Visible.LeafWetness.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(0, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[0]}\",\"{LeafWetness1.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(1, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[1]}\",\"{LeafWetness2.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(2, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[2]}\",\"{LeafWetness3.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(3, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[3]}\",\"{LeafWetness4.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(4, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[4]}\",\"{LeafWetness5.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(5, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[5]}\",\"{LeafWetness6.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(6, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[6]}\",\"{LeafWetness7.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"],");
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(7, local))
					json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[7]}\",\"{LeafWetness8.ToString(cumulus.LeafWetFormat)}\",\"{cumulus.Units.LeafWetnessUnitText}\"]");
			}

			if (json[json.Length - 1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}


		public string GetAirLinkCountsOut()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkOut != null)
			{
				json.Append($"[\"1 μm\",\"{cumulus.airLinkDataOut.pm1:F1}\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataOut.pm2p5:F1}\",\"{cumulus.airLinkDataOut.pm2p5_1hr:F1}\",\"{cumulus.airLinkDataOut.pm2p5_3hr:F1}\",\"{cumulus.airLinkDataOut.pm2p5_24hr:F1}\",\"{cumulus.airLinkDataOut.pm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataOut.pm10:F1}\",\"{cumulus.airLinkDataOut.pm10_1hr:F1}\",\"{cumulus.airLinkDataOut.pm10_3hr:F1}\",\"{cumulus.airLinkDataOut.pm10_24hr:F1}\",\"{cumulus.airLinkDataOut.pm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"1 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkAqiOut()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkOut != null)
			{
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataOut.aqiPm2p5:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_1hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_3hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_24hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataOut.aqiPm10:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_1hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_3hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_24hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkPctOut()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkOut != null)
			{
				json.Append($"[\"All sizes\",\"--\",\"{cumulus.airLinkDataOut.pct_1hr}%\",\"{cumulus.airLinkDataOut.pct_3hr}%\",\"{cumulus.airLinkDataOut.pct_24hr}%\",\"{cumulus.airLinkDataOut.pct_nowcast}%\"]");
			}
			else
			{
				json.Append("[\"All sizes\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkCountsIn()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkIn != null)
			{
				json.Append($"[\"1 μm\",\"{cumulus.airLinkDataIn.pm1:F1}\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataIn.pm2p5:F1}\",\"{cumulus.airLinkDataIn.pm2p5_1hr:F1}\",\"{cumulus.airLinkDataIn.pm2p5_3hr:F1}\",\"{cumulus.airLinkDataIn.pm2p5_24hr:F1}\",\"{cumulus.airLinkDataIn.pm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataIn.pm10:F1}\",\"{cumulus.airLinkDataIn.pm10_1hr:F1}\",\"{cumulus.airLinkDataIn.pm10_3hr:F1}\",\"{cumulus.airLinkDataIn.pm10_24hr:F1}\",\"{cumulus.airLinkDataIn.pm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"1 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkAqiIn()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkIn != null)
			{
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataIn.aqiPm2p5:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_1hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_3hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_24hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataIn.aqiPm10:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_1hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_3hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_24hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkPctIn()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkIn != null)
			{
				json.Append($"[\"All sizes\",\"--\",\"{cumulus.airLinkDataIn.pct_1hr}%\",\"{cumulus.airLinkDataIn.pct_3hr}%\",\"{cumulus.airLinkDataIn.pct_24hr}%\",\"{cumulus.airLinkDataIn.pct_nowcast}%\"]");
			}
			else
			{
				json.Append("[\"All sizes\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		/*
		private string extrajsonformat(int item, double value, DateTime timestamp, string unit, string valueformat, string dateformat)
		{
			return "[\"" + alltimedescs[item] + "\",\"" + value.ToString(valueformat) + " " + unit + "\",\"" + timestamp.ToString(dateformat) + "\"]";
		}
		*/

		// The Today/Yesterday data is in the form:
		// Name, today value + units, today time, yesterday value + units, yesterday time
		// It's used to automatically populate a DataTables table in the browser interface
		public string GetTodayYestTemp()
		{
			var json = new StringBuilder("{\"data\":[", 2048);
			var sepStr = "\",\"";
			var closeStr = "\"],";
			var tempUnitStr = "&nbsp;&deg;" + cumulus.Units.TempText[1].ToString() + sepStr;

			json.Append("[\"High Temperature\",\"");
			json.Append(HiLoToday.HighTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"Low Temperature\",\"");
			json.Append(HiLoToday.LowTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"Temperature Range\",\"");
			json.Append((HiLoToday.HighTemp - HiLoToday.LowTemp).ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\",\"");
			json.Append((HiLoYest.HighTemp - HiLoYest.LowTemp).ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\"],");

			json.Append("[\"Average Temperature\",\"");
			json.Append((TempTotalToday / tempsamplestoday).ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\",\"");
			json.Append(YestAvgTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\"],");


			json.Append("[\"High Apparent Temperature\",\"");
			json.Append(HiLoToday.HighAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"Low Apparent Temperature\",\"");
			json.Append(HiLoToday.LowAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"High Feels Like\",\"");
			json.Append(HiLoToday.HighFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"Low Feels Like\",\"");
			json.Append(HiLoToday.LowFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"High Humidex\",\"");
			json.Append(HiLoToday.HighHumidex.ToString(cumulus.TempFormat));
			json.Append("\",\"");
			json.Append(HiLoToday.HighHumidexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighHumidex.ToString(cumulus.TempFormat));
			json.Append("\",\"");
			json.Append(HiLoYest.HighHumidexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);
			json.Append("[\"High Dew Point\",\"");
			json.Append(HiLoToday.HighDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"Low Dew Point\",\"");
			json.Append(HiLoToday.LowDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"Low Wind Chill\",\"");
			json.Append(HiLoToday.LowWindChill.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowWindChillTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowWindChill.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowWindChillTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(closeStr);

			json.Append("[\"High Heat Index\",\"");
			json.Append(HiLoToday.HighHeatIndex.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighHeatIndexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighHeatIndex.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighHeatIndexTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestHum()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";
			var unitStr = "&nbsp;%" + sepStr;

			json.Append("[\"High Humidity\",\"");
			json.Append(HiLoToday.HighHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoToday.HighHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoYest.HighHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"Low Humidity\",\"");
			json.Append(HiLoToday.LowHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoToday.LowHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoYest.LowHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestRain()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";
			var unitStr = "&nbsp;" + cumulus.Units.RainText;

			json.Append("[\"Total Rain\",\"");
			json.Append(RainToday.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(RainYesterday.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"],");

			json.Append("[\"High Rain Rate\",\"");
			json.Append(HiLoToday.HighRainRate.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(HiLoToday.HighRainRateTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighRainRate.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(HiLoYest.HighRainRateTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"High Hourly Rain\",\"");
			json.Append(HiLoToday.HighHourlyRain.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoToday.HighHourlyRainTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighHourlyRain.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.HighHourlyRainTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"High 24 Hour Rain\",\"");
			json.Append(HiLoToday.HighRain24h.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoToday.HighRain24hTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighRain24h.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.HighRain24hTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");


			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestWind()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";

			json.Append("[\"Highest Gust\",\"");
			json.Append(HiLoToday.HighGust.ToString(cumulus.WindFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoToday.HighGustTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighGust.ToString(cumulus.WindFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoYest.HighGustTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"Highest Speed\",\"");
			json.Append(HiLoToday.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoToday.HighWindTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoYest.HighWindTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"Wind Run\",\"");
			json.Append(WindRunToday.ToString(cumulus.WindRunFormat));
			json.Append("&nbsp;" + cumulus.Units.WindRunText);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(YesterdayWindRun.ToString(cumulus.WindRunFormat));
			json.Append("&nbsp;" + cumulus.Units.WindRunText);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"],");

			json.Append("[\"Dominant Direction\",\"");
			json.Append(DominantWindBearing.ToString("F0"));
			json.Append("&nbsp;&deg;&nbsp;" + CompassPoint(DominantWindBearing));
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(YestDominantWindBearing.ToString("F0"));
			json.Append("&nbsp;&deg;&nbsp;" + CompassPoint(YestDominantWindBearing));
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestPressure()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";
			var unitStr = "&nbsp;" + cumulus.Units.PressText;

			json.Append("[\"High Pressure\",\"");
			json.Append(HiLoToday.HighPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoToday.HighPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.HighPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"Low Pressure\",\"");
			json.Append(HiLoToday.LowPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoToday.LowPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.LowPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.LowPressTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		public string GetTodayYestSolar()
		{
			var json = new StringBuilder("{\"data\":[", 512);
			var sepStr = "\",\"";

			json.Append("[\"High Solar Radiation\",\"");
			json.Append(HiLoToday.HighSolar.ToString("F0"));
			json.Append("&nbsp;W/m2");
			json.Append(sepStr);
			json.Append(HiLoToday.HighSolarTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighSolar.ToString("F0"));
			json.Append("&nbsp;W/m2");
			json.Append(sepStr);
			json.Append(HiLoYest.HighSolarTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"],");

			json.Append("[\"Hours of Sunshine\",\"");
			json.Append(SunshineHours.ToString(cumulus.SunFormat));
			json.Append("&nbsp;hrs");
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(YestSunshineHours.ToString(cumulus.SunFormat));
			json.Append("&nbsp;hrs");
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append("\"],");

			json.Append("[\"High UV-Index\",\"");
			json.Append(HiLoToday.HighUv.ToString("F1"));
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(HiLoToday.HighUvTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append(sepStr);
			json.Append(HiLoYest.HighUv.ToString("F1"));
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(HiLoYest.HighUvTime.ToString(cumulus.ProgramOptions.TimeFormat));
			json.Append("\"]");

			json.Append("]}");
			return json.ToString();
		}

		/// <summary>
		/// Return lines from dayfile.txt in json format
		/// </summary>
		/// <param name="draw"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <returns>JSON encoded section of the dayfile</returns>
		public string GetDayfile(string draw, int start, int length, string search)
		{
			try
			{
				//var total = File.ReadLines(cumulus.DayFile).Count();
				var lines = File.ReadAllLines(cumulus.DayFileName);
				var total = lines.Length;
				var filtered = 0;
				var thisDraw = 0;

				var json = new StringBuilder(350 * lines.Count());

				json.Append("{\"data\":[");

				//var lines = File.ReadLines(cumulus.DayFile).Skip(start).Take(length);

				var lineNum = 0;

				foreach (var line in lines)
				{
					lineNum++;

					// if we have a search string and no match, skip to next line
					if (!string.IsNullOrEmpty(search) && !line.Contains(search))
					{
						continue;
					}

					// this line either matches the search
					filtered++;

					// skip records until we get to the start entry
					if (filtered <= start)
					{
						continue;
					}

					// only send the number requested
					if (thisDraw < length)
					{
						// track the number of lines we have to return so far
						thisDraw++;

						var sep = Utils.GetLogFileSeparator(line, cumulus.ListSeparator);

						var fields = line.Split(sep[0]);
						var numFields = fields.Length;

						json.Append($"[{lineNum},");

						for (var i = 0; i < numFields; i++)
						{
							json.Append($"\"{fields[i]}\"");
							if (i < fields.Length - 1)
							{
								json.Append(',');
							}
						}

						if (numFields < Cumulus.DayfileFields)
						{
							// insufficient fields, pad with empty fields
							for (var i = numFields; i < Cumulus.DayfileFields; i++)
							{
								json.Append(",\"\"");
							}
						}
						json.Append("],");
					}
					else if (string.IsNullOrEmpty(search))
					{
						// no search so we can bail out as we already know the total number of records
						break;
					}
				}

				// trim last ","
				if (thisDraw > 0)
					json.Length--;
				json.Append("],\"recordsTotal\":");
				json.Append(total);
				json.Append(",\"draw\":");
				json.Append(draw);
				json.Append(",\"recordsFiltered\":");
				json.Append(string.IsNullOrEmpty(search) ? total : filtered);
				json.Append('}');

				return json.ToString();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetDayFile: Error - " + ex.ToString());
			}

			return "";
		}

		internal string GetDiaryData(string date)
		{

			StringBuilder json = new StringBuilder("{\"entry\":\"", 1024);

			var result = cumulus.DiaryDB.Query<DiaryData>("select * from DiaryData where date(Timestamp) = ? order by Timestamp limit 1", date);

			if (result.Count > 0)
			{
				json.Append(result[0].entry + "\",");
				json.Append("\"snowFalling\":");
				json.Append(result[0].snowFalling + ",");
				json.Append("\"snowLying\":");
				json.Append(result[0].snowLying + ",");
				json.Append("\"snowDepth\":\"");
				json.Append(result[0].snowDepth);
				json.Append("\"}");
			}
			else
			{
				json.Append("\",\"snowFalling\":0,\"snowLying\":0,\"snowDepth\":\"\"}");
			}

			return json.ToString();
		}

		// Fetches all days that have a diary entry
		internal string GetDiarySummary()
		{
			var json = new StringBuilder(512);
			var result = cumulus.DiaryDB.Query<DiaryData>("select Timestamp from DiaryData order by Timestamp");

			if (result.Count > 0)
			{
				json.Append("{\"dates\":[");
				for (int i = 0; i < result.Count; i++)
				{
					json.Append("\"");
					json.Append(result[i].Timestamp.ToString("yyyy-MM-dd"));
					json.Append("\",");
				}
				json.Length--;
				json.Append("]}");
			}
			else
			{
				json.Append("{\"dates\":[]}");
			}

			return json.ToString();
		}

		/// <summary>
		/// Return lines from log file in json format
		/// </summary>
		/// <returns></returns>
		public string GetLogfile(string from, string to, string draw, int start, int length, string search, bool extra)
		{
			try
			{
				// date will be in format "yyyy-mm-dd"
				var stDate = from.Split('-');
				var enDate = to.Split('-');

				var ts = new DateTime(int.Parse(stDate[0]), int.Parse(stDate[1]), int.Parse(stDate[2]));
				var te = new DateTime(int.Parse(enDate[0]), int.Parse(enDate[1]), int.Parse(enDate[2]));

				// adjust for 9 am rollover
				ts = ts.AddHours(-cumulus.GetHourInc(ts));
				te = te.AddDays(1);
				te = te.AddHours(-cumulus.GetHourInc(ts));

				var fileDate = new DateTime(ts.Year, ts.Month, 15);

				var logfile = extra ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetLogFileName(fileDate);
				var numFields = extra ? Cumulus.NumExtraLogFileFields : Cumulus.NumLogFileFields;

				if (!File.Exists(logfile))
				{
					cumulus.LogErrorMessage($"GetLogFile: Error, file does not exist: {logfile}");
					return "";
				}

				var watch = Stopwatch.StartNew();

				var finished = false;
				var total = 0;
				var filtered = 0;
				var thisDraw = 0;

				var json = new StringBuilder(220 * length);
				json.Append("{\"data\":[");

				while (!finished)
				{
					if (File.Exists(logfile))
					{
						cumulus.LogDebugMessage($"GetLogfile: Processing log file - {logfile}");

						var lines = File.ReadAllLines(logfile);
						var lineNum = 0;

						foreach (var line in lines)
						{
							lineNum++;

							var sep = Utils.GetLogFileSeparator(line, cumulus.ListSeparator);
							var fields = line.Split(sep[0]);

							var entryDate = Utils.ddmmyyhhmmStrToDate(fields[0], fields[1]);

							if (entryDate >= ts)
							{
								if (entryDate >= te)
								{
									// we are beyond the selected date range, bail out
									finished = true;
									break;
								}

								total++;

								// if we have a search string and no match, skip to next line
								if (!string.IsNullOrEmpty(search) && !line.Contains(search))
								{
									continue;
								}

								// this line either matches the search, or we do not have a search
								filtered++;

								// skip records until we get to the start entry
								if (filtered <= start)
								{
									continue;
								}

								// only send the number requested
								if (thisDraw < length)
								{
									// track the number of lines we have to return so far
									thisDraw++;

									json.Append($"[{lineNum},");

									for (var i = 0; i < numFields; i++)
									{
										if (i < fields.Length)
										{
											// field exists
											json.Append('"');
											json.Append(fields[i]);
											json.Append('"');
										}
										else
										{
											// add padding
											json.Append("\"-\"");
										}

										if (i < numFields - 1)
										{
											json.Append(',');
										}
									}
									json.Append("],");
								}
							}
						}
					}
					else
					{
						cumulus.LogDebugMessage($"GetLogfile: Log file  not found - {logfile}");
					}

					// might need the next months log
					fileDate = fileDate.AddMonths(1);

					// have we run out of log entries?
					// filedate is 15th on month, compare against the first
					if (te <= fileDate.AddDays(-14))
					{
						finished = true;
						cumulus.LogDebugMessage("GetLogfile: Finished processing log files");
					}

					if (!finished)
					{
						cumulus.LogDebugMessage($"GetLogfile: Finished processing log file - {logfile}");
						logfile = extra ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetLogFileName(fileDate);
					}

				}
				// trim trailing ","
				if (thisDraw > 0)
					json.Length--;
				json.Append("],\"recordsTotal\":");
				json.Append(total);
				json.Append(",\"draw\":");
				json.Append(draw);
				json.Append(",\"recordsFiltered\":");
				json.Append(string.IsNullOrEmpty(search) ? total : filtered);
				json.Append('}');

				watch.Stop();
				var elapsed = watch.ElapsedMilliseconds;
				cumulus.LogDebugMessage($"GetLogfile: Logfiles parse = {elapsed} ms");
				cumulus.LogDebugMessage($"GetLogfile: Found={total}, filtered={filtered} (filter='{search}'), return={thisDraw}");

				return json.ToString();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetLogfile: Error - " + ex.ToString());
			}

			return "";
		}


		public string GetCachedSqlCommands(string draw, int start, int length, string search)
		{
			try
			{
				var filtered = 0;
				var thisDraw = 0;


				var json = new StringBuilder(350 * cumulus.MySqlFailedList.Count);

				json.Append("{\"data\":[");

				//var lines = File.ReadLines(cumulus.DayFile).Skip(start).Take(length);

				foreach (var rec in cumulus.MySqlFailedList)
				{
					// if we have a search string and no match, skip to next line
					if (!string.IsNullOrEmpty(search) && !rec.statement.Contains(search))
					{
						continue;
					}

					// this line either matches the search
					filtered++;

					// skip records until we get to the start entry
					if (filtered <= start)
					{
						continue;
					}

					// only send the number requested
					if (thisDraw < length)
					{
						// track the number of lines we have to return so far
						thisDraw++;

						json.Append($"[{rec.key},\"{rec.statement}\"],");
					}
					else if (string.IsNullOrEmpty(search))
					{
						// no search so we can bail out as we already know the total number of records
						break;
					}
				}

				// trim last ","
				if (thisDraw > 0)
					json.Length--;
				json.Append("],\"recordsTotal\":");
				json.Append(cumulus.MySqlFailedList.Count);
				json.Append(",\"draw\":");
				json.Append(draw);
				json.Append(",\"recordsFiltered\":");
				json.Append(string.IsNullOrEmpty(search) ? cumulus.MySqlFailedList.Count : filtered);
				json.Append('}');

				return json.ToString();

			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetCachedSqlCommands: Error - " + ex.ToString());
			}

			return "";

		}

		public string GetUnits()
		{
			var json = new StringBuilder("{", 200);

			json.Append($"\"temp\":\"{cumulus.Units.TempText[1]}\",");
			json.Append($"\"wind\":\"{cumulus.Units.WindText}\",");
			json.Append($"\"windrun\":\"{cumulus.Units.WindRunText}\",");
			json.Append($"\"rain\":\"{cumulus.Units.RainText}\",");
			json.Append($"\"press\":\"{cumulus.Units.PressText}\",");
			json.Append($"\"soilmoisture\":\"{cumulus.Units.SoilMoistureUnitText}\",");
			json.Append($"\"co2\":\"{cumulus.Units.CO2UnitText}\",");
			json.Append($"\"leafwet\":\"{cumulus.Units.LeafWetnessUnitText}\",");
			json.Append($"\"aq\":\"{cumulus.Units.AirQualityUnitText}\"");
			json.Append('}');
			return json.ToString();
		}

		public string GetGraphConfig(bool local)
		{
			var json = new StringBuilder(200);
			json.Append('{');
			json.Append($"\"temp\":{{\"units\":\"{cumulus.Units.TempText[1]}\",\"decimals\":{cumulus.TempDPlaces}}},");
			json.Append($"\"wind\":{{\"units\":\"{cumulus.Units.WindText}\",\"decimals\":{cumulus.WindAvgDPlaces},\"rununits\":\"{cumulus.Units.WindRunText}\"}},");
			json.Append($"\"rain\":{{\"units\":\"{cumulus.Units.RainText}\",\"decimals\":{cumulus.RainDPlaces}}},");
			json.Append($"\"press\":{{\"units\":\"{cumulus.Units.PressText}\",\"decimals\":{cumulus.PressDPlaces}}},");
			json.Append($"\"hum\":{{\"decimals\":{cumulus.HumDPlaces}}},");
			json.Append($"\"uv\":{{\"decimals\":{cumulus.UVDPlaces}}},");
			json.Append($"\"soilmoisture\":{{\"units\":\"{cumulus.Units.SoilMoistureUnitText}\"}},");
			json.Append($"\"co2\":{{\"units\":\"{cumulus.Units.CO2UnitText}\"}},");
			json.Append($"\"leafwet\":{{\"units\":\"{cumulus.Units.LeafWetnessUnitText}\",\"decimals\":{cumulus.LeafWetDPlaces}}},");
			json.Append($"\"aq\":{{\"units\":\"{cumulus.Units.AirQualityUnitText}\"}},");

			#region data series

			json.Append("\"series\":{");

			#region recent

			// temp
			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
				json.Append($"\"temp\":{{\"name\":\"Temperature\",\"colour\":\"{cumulus.GraphOptions.Colour.Temp}\"}},");
			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
				json.Append($"\"apptemp\":{{\"name\":\"Apparent Temperature\",\"colour\":\"{cumulus.GraphOptions.Colour.AppTemp}\"}},");
			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
				json.Append($"\"feelslike\":{{\"name\":\"Feels Like\",\"colour\":\"{cumulus.GraphOptions.Colour.FeelsLike}\"}},");
			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append($"\"wchill\":{{\"name\":\"Wind Chill\",\"colour\":\"{cumulus.GraphOptions.Colour.WindChill}\"}},");
			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append($"\"heatindex\":{{\"name\":\"Heat Index\",\"colour\":\"{cumulus.GraphOptions.Colour.HeatIndex}\"}},");
			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
				json.Append($"\"dew\":{{\"name\":\"Dew Point\",\"colour\":\"{cumulus.GraphOptions.Colour.DewPoint}\"}},");
			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append($"\"humidex\":{{\"name\":\"Humidex\",\"colour\":\"{cumulus.GraphOptions.Colour.Humidex}\"}},");
			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
				json.Append($"\"intemp\":{{\"name\":\"Indoor Temp\",\"colour\":\"{cumulus.GraphOptions.Colour.InTemp}\"}},");
			// hum
			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				json.Append($"\"hum\":{{\"name\":\"Humidity\",\"colour\":\"{cumulus.GraphOptions.Colour.OutHum}\"}},");
			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
				json.Append($"\"inhum\":{{\"name\":\"Humidity\",\"colour\":\"{cumulus.GraphOptions.Colour.InHum}\"}},");
			// press
			json.Append($"\"press\":{{\"name\":\"Pressure\",\"colour\":\"{cumulus.GraphOptions.Colour.Press}\"}},");
			// wind
			json.Append($"\"wspeed\":{{\"name\":\"Wind Speed\",\"colour\":\"{cumulus.GraphOptions.Colour.WindAvg}\"}},");
			json.Append($"\"wgust\":{{\"name\":\"Wind Gust\",\"colour\":\"{cumulus.GraphOptions.Colour.WindGust}\"}},");
			json.Append($"\"windrun\":{{\"name\":\"Wind Run\",\"colour\":\"{cumulus.GraphOptions.Colour.WindRun}\"}},");
			json.Append($"\"bearing\":{{\"name\":\"Bearing\",\"colour\":\"{cumulus.GraphOptions.Colour.WindBearing}\"}},");
			json.Append($"\"avgbearing\":{{\"name\":\"Average Bearing\",\"colour\":\"{cumulus.GraphOptions.Colour.WindBearingAvg}\"}},");
			// rain
			json.Append($"\"rfall\":{{\"name\":\"Rainfall\",\"colour\":\"{cumulus.GraphOptions.Colour.Rainfall}\"}},");
			json.Append($"\"rrate\":{{\"name\":\"Rainfall Rate\",\"colour\":\"{cumulus.GraphOptions.Colour.RainRate}\"}},");
			// solar
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
				json.Append($"\"solarrad\":{{\"name\":\"Solar Irradiation\",\"colour\":\"{cumulus.GraphOptions.Colour.Solar}\"}},");
			json.Append($"\"currentsolarmax\":{{\"name\":\"Solar theoretical\",\"colour\":\"{cumulus.GraphOptions.Colour.SolarTheoretical}\"}},");
			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				json.Append($"\"uv\":{{\"name\":\"UV-I\",\"colour\":\"{cumulus.GraphOptions.Colour.UV}\"}},");
			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
				json.Append($"\"sunshine\":{{\"name\":\"Sunshine\",\"colour\":\"{cumulus.GraphOptions.Colour.Sunshine}\"}},");
			// aq
			json.Append($"\"pm2p5\":{{\"name\":\"PM 2.5\",\"colour\":\"{cumulus.GraphOptions.Colour.Pm2p5}\"}},");
			json.Append($"\"pm10\":{{\"name\":\"PM 10\",\"colour\":\"{cumulus.GraphOptions.Colour.Pm10}\"}},");

			#endregion recent

			#region daily

			// growing deg days
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
				json.Append($"\"growingdegreedays1\":{{\"name\":\"GDD#1\"}},");
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
				json.Append($"\"growingdegreedays2\":{{\"name\":\"GDD#2\"}},");
			// TODO: temp sum

			// daily temps
			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
				json.Append($"\"avgtemp\":{{\"name\":\"Average Temp\",\"colour\":\"{cumulus.GraphOptions.Colour.AvgTemp}\"}},");
			if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
				json.Append($"\"maxtemp\":{{\"name\":\"Maximum Temp\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxTemp}\"}},");
			if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
				json.Append($"\"mintemp\":{{\"name\":\"Minimum Temp\",\"colour\":\"{cumulus.GraphOptions.Colour.MinTemp}\"}},");

			json.Append($"\"maxpress\":{{\"name\":\"High Pressure\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxPress}\"}},");
			json.Append($"\"minpress\":{{\"name\":\"Low Pressure\",\"colour\":\"{cumulus.GraphOptions.Colour.MinPress}\"}},");

			json.Append($"\"maxhum\":{{\"name\":\"Maximum Humidity\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxOutHum}\"}},");
			json.Append($"\"minhum\":{{\"name\":\"Minimum Humidity\",\"colour\":\"{cumulus.GraphOptions.Colour.MinOutHum}\"}},");

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				json.Append($"\"mindew\":{{\"name\":\"Minumim Dew Point\",\"colour\":\"{cumulus.GraphOptions.Colour.MinDew}\"}},");
				json.Append($"\"maxdew\":{{\"name\":\"Maximum Dew Point\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxDew}\"}},");
			}
			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append($"\"minwindchill\":{{\"name\":\"Wind Chill\",\"colour\":\"{cumulus.GraphOptions.Colour.MinWindChill}\"}},");
			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				json.Append($"\"minapp\":{{\"name\":\"Minumim Apparent\",\"colour\":\"{cumulus.GraphOptions.Colour.MinApp}\"}},");
				json.Append($"\"maxapp\":{{\"name\":\"Maximum Apparent\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxApp}\"}},");
			}
			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				json.Append($"\"minfeels\":{{\"name\":\"Minumim Feels Like\",\"colour\":\"{cumulus.GraphOptions.Colour.MinFeels}\"}},");
				json.Append($"\"maxfeels\":{{\"name\":\"Maximum Feels Like\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxFeels}\"}},");
			}
			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append($"\"maxheatindex\":{{\"name\":\"Heat Index\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxHeatIndex}\"}},");
			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append($"\"maxhumidex\":{{\"name\":\"Humidex\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxHumidex}\"}},");

			#endregion daily

			#region extra sensors

			// extra temp
			if (cumulus.GraphOptions.Visible.ExtraTemp.IsVisible(local))
				json.Append($"\"extratemp\":{{\"name\":[\"{cumulus.Trans.ExtraTempCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.ExtraTemp.Join("\",\"")}\"]}},");
			// extra hum
			if (cumulus.GraphOptions.Visible.ExtraHum.IsVisible(local))
				json.Append($"\"extrahum\":{{\"name\":[\"{cumulus.Trans.ExtraHumCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.ExtraHum.Join("\",\"")}\"]}},");
			// extra dewpoint
			if (cumulus.GraphOptions.Visible.ExtraDewPoint.IsVisible(local))
				json.Append($"\"extradew\":{{\"name\":[\"{cumulus.Trans.ExtraDPCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.ExtraDewPoint.Join("\",\"")}\"]}},");
			// extra user temps
			if (cumulus.GraphOptions.Visible.UserTemp.IsVisible(local))
				json.Append($"\"usertemp\":{{\"name\":[\"{cumulus.Trans.UserTempCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.UserTemp.Join("\",\"")}\"]}},");
			// soil temps
			if (cumulus.GraphOptions.Visible.SoilTemp.IsVisible(local))
				json.Append($"\"soiltemp\":{{\"name\":[\"{cumulus.Trans.SoilTempCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.SoilTemp.Join("\",\"")}\"]}},");
			// soil temps
			if (cumulus.GraphOptions.Visible.SoilMoist.IsVisible(local))
				json.Append($"\"soilmoist\":{{\"name\":[\"{cumulus.Trans.SoilMoistureCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.SoilMoist.Join("\",\"")}\"]}},");
			// leaf wetness
			if (cumulus.GraphOptions.Visible.LeafWetness.IsVisible(local))
				json.Append($"\"leafwet\":{{\"name\":[\"{cumulus.Trans.LeafWetnessCaptions.Join("\",\"")}\"],\"colour\":[\"{cumulus.GraphOptions.Colour.LeafWetness.Join("\",\"")}\"]}},");

			// CO2
			json.Append("\"co2\":{");
			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
				json.Append($"\"co2\":{{\"name\":\"CO₂\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.CO2}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
				json.Append($"\"co2average\":{{\"name\":\"CO₂ Average\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.CO2Avg}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
				json.Append($"\"pm10\":{{\"name\":\"PM 10\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm10}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
				json.Append($"\"pm10average\":{{\"name\":\"PM 10 Avg\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm10Avg}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
				json.Append($"\"pm2.5\":{{\"name\":\"PM 2.5\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm25}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
				json.Append($"\"pm2.5average\":{{\"name\":\"PM 2.5 Avg\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm25Avg}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
				json.Append($"\"humidity\":{{\"name\":\"Humidity\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Hum}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
				json.Append($"\"temperature\":{{\"name\":\"Temperature\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Temp}\"}}");
			// remove trailing comma
			if (json[json.Length - 1] == ',')
				json.Length--;
			json.Append("},");

			#endregion extra sensors

			// remove trailing comma
			json.Length--;
			json.Append('}');

			#endregion data series

			json.Append("}");
			return json.ToString();
		}

		public string GetAvailGraphData(bool local)
		{
			var json = new StringBuilder(200);

			// Temp values
			json.Append("{\"Temperature\":[");

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
				json.Append("\"Temperature\",");

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
				json.Append("\"Indoor Temp\",");

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append("\"Heat Index\",");

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
				json.Append("\"Dew Point\",");

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append("\"Wind Chill\",");

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
				json.Append("\"Apparent Temp\",");

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
				json.Append("\"Feels Like\",");

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append("\"Humidex\",");

			if (json[json.Length - 1] == ',')
				json.Length--;

			// humidity values
			json.Append("],\"Humidity\":[");

			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				json.Append("\"Humidity\",");

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
				json.Append("\"Indoor Hum\",");

			if (json[json.Length - 1] == ',')
				json.Length--;

			// fixed values
			// pressure
			json.Append("],\"Pressure\":[\"Pressure\"],");

			// wind
			json.Append("\"Wind\":[\"Wind Speed\",\"Wind Gust\",\"Wind Bearing\"],");

			// rain
			json.Append("\"Rain\":[\"Rainfall\",\"Rainfall Rate\"]");

			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local) || cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local) || cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
			{
				json.Append(",\"DailyTemps\":[");

				if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
					json.Append("\"AvgTemp\",");
				if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
					json.Append("\"MaxTemp\",");
				if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
					json.Append("\"MinTemp\",");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}


			// solar values
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local) || cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				json.Append(",\"Solar\":[");

				if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
					json.Append("\"Solar Rad\",");

				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
					json.Append("\"UV Index\",");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Sunshine
			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				json.Append(",\"Sunshine\":[\"sunhours\"]");
			}

			// air quality
			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (cumulus.StationOptions.PrimaryAqSensor > (int) Cumulus.PrimaryAqSensor.Undefined
				&& cumulus.StationOptions.PrimaryAqSensor != (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				json.Append(",\"AirQuality\":[");
				json.Append("\"PM 2.5\"");

				// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
				if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
					cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
					cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
				{
					json.Append(",\"PM 10\"");
				}
				json.Append(']');
			}

			// Degree Days
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) || cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
			{
				json.Append(",\"DegreeDays\":[");
				if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
					json.Append("\"GDD1\",");

				if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
					json.Append("\"GDD2\"");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Temp Sum
			if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum1.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
			{
				json.Append(",\"TempSum\":[");
				if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local))
					json.Append("\"Sum0\",");
				if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
					json.Append("\"Sum1\",");
				if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
					json.Append("\"Sum2\"");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra temperature
			if (cumulus.GraphOptions.Visible.ExtraTemp.IsVisible(local))
			{
				json.Append(",\"ExtraTemp\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.ExtraTempCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra humidity
			if (cumulus.GraphOptions.Visible.ExtraHum.IsVisible(local))
			{
				json.Append(",\"ExtraHum\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.ExtraHumCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra dew point
			if (cumulus.GraphOptions.Visible.ExtraDewPoint.IsVisible(local))
			{
				json.Append(",\"ExtraDewPoint\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.ExtraDPCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}


			// Soil Temp
			if (cumulus.GraphOptions.Visible.SoilTemp.IsVisible(local))
			{
				json.Append(",\"SoilTemp\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.SoilTempCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Soil Moisture
			if (cumulus.GraphOptions.Visible.SoilMoist.IsVisible(local))
			{
				json.Append(",\"SoilMoist\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.SoilMoistureCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// User Temp
			if (cumulus.GraphOptions.Visible.UserTemp.IsVisible(local))
			{
				json.Append(",\"UserTemp\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.UserTempCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Leaf wetness
			if (cumulus.GraphOptions.Visible.LeafWetness.IsVisible(local))
			{
				json.Append(",\"LeafWetness\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.LeafWetness.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.LeafWetnessCaptions[i]}\",");
				}
				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			// CO2
			if (cumulus.GraphOptions.Visible.CO2Sensor.IsVisible(local))
			{
				json.Append(",\"CO2\":[");
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
					json.Append("\"CO2\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
					json.Append("\"CO2Avg\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
					json.Append("\"PM25\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
					json.Append("\"PM25Avg\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
					json.Append("\"PM10\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
					json.Append("\"PM10Avg\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
					json.Append("\"Temp\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
					json.Append("\"Hum\"");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append(']');
			}

			json.Append('}');
			return json.ToString();
		}


		public string GetSelectaChartOptions()
		{
			return JsonSerializer.SerializeToString(cumulus.SelectaChartOptions);
		}

		public string GetSelectaPeriodOptions()
		{
			return JsonSerializer.SerializeToString(cumulus.SelectaPeriodOptions);
		}


		public string GetDailyRainGraphData()
		{
			var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);

			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"dailyrain\":[", 10000);

			var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();
			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{Utils.ToPseudoJSTime(data[i].Date)},{data[i].TotalRain.ToString(cumulus.RainFormat, InvC)}],");
			}

			// remove trailing comma
			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetSunHoursGraphData(bool local)
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{", 10000);
			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);
				var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();

				sb.Append("\"sunhours\":[");
				for (var i = 0; i < data.Count; i++)
				{
					var sunhrs = data[i].SunShineHours >= 0 ? data[i].SunShineHours : 0;
					sb.Append($"[{Utils.ToPseudoJSTime(data[i].Date)},{sunhrs.ToString(cumulus.SunFormat, InvC)}],");
				}

				// remove trailing comma
				if (sb[sb.Length - 1] == ',')
					sb.Length--;

				sb.Append(']');
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetDailyTempGraphData(bool local)
		{
			var InvC = new CultureInfo("");
			var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);
			var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();
			var append = false;
			StringBuilder sb = new StringBuilder("{");

			if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
			{
				sb.Append("\"mintemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{Utils.ToPseudoJSTime(data[i].Date)},{data[i].LowTemp.ToString(cumulus.TempFormat, InvC)}],");
				}

				// remove trailing comma
				if (sb[sb.Length - 1] == ',')
					sb.Length--;

				sb.Append(']');
				append = true;
			}

			if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
			{
				if (append)
					sb.Append(',');

				sb.Append("\"maxtemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{Utils.ToPseudoJSTime(data[i].Date)},{data[i].HighTemp.ToString(cumulus.TempFormat, InvC)}],");
				}

				// remove trailing comma
				if (sb[sb.Length - 1] == ',')
					sb.Length--;

				sb.Append(']');
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
			{
				if (append)
					sb.Append(',');

				sb.Append("\"avgtemp\":[");
				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{Utils.ToPseudoJSTime(data[i].Date)},{data[i].AvgTemp.ToString(cumulus.TempFormat, InvC)}],");
				}

				// remove trailing comma
				if (sb[sb.Length - 1] == ',')
					sb.Length--;

				sb.Append(']');
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetAllDailyTempGraphData(bool local)
		{
			var InvC = new CultureInfo("");
			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder minTemp = new StringBuilder("[");
			StringBuilder maxTemp = new StringBuilder("[");
			StringBuilder avgTemp = new StringBuilder("[");
			StringBuilder heatIdx = new StringBuilder("[");
			StringBuilder maxApp = new StringBuilder("[");
			StringBuilder minApp = new StringBuilder("[");
			StringBuilder windChill = new StringBuilder("[");
			StringBuilder maxDew = new StringBuilder("[");
			StringBuilder minDew = new StringBuilder("[");
			StringBuilder maxFeels = new StringBuilder("[");
			StringBuilder minFeels = new StringBuilder("[");
			StringBuilder humidex = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{
					var recDate = Utils.ToPseudoJSTime(DayFile[i].Date);
					// lo temp
					if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
						minTemp.Append($"[{recDate},{DayFile[i].LowTemp.ToString(cumulus.TempFormat, InvC)}],");
					// hi temp
					if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
						maxTemp.Append($"[{recDate},{DayFile[i].HighTemp.ToString(cumulus.TempFormat, InvC)}],");
					// avg temp
					if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
						avgTemp.Append($"[{recDate},{DayFile[i].AvgTemp.ToString(cumulus.TempFormat, InvC)}],");

					if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
					{
						// hi heat index
						if (DayFile[i].HighHeatIndex > -999)
							heatIdx.Append($"[{recDate},{DayFile[i].HighHeatIndex.ToString(cumulus.TempFormat, InvC)}],");
						else
							heatIdx.Append($"[{recDate},null],");
					}
					if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
					{
						// hi app temp
						if (DayFile[i].HighAppTemp > -999)
							maxApp.Append($"[{recDate},{DayFile[i].HighAppTemp.ToString(cumulus.TempFormat, InvC)}],");
						else
							maxApp.Append($"[{recDate},null],");

						// lo app temp
						if (DayFile[i].LowAppTemp < 999)
							minApp.Append($"[{recDate},{DayFile[i].LowAppTemp.ToString(cumulus.TempFormat, InvC)}],");
						else
							minApp.Append($"[{recDate},null],");
					}
					// lo wind chill
					if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
					{
						if (DayFile[i].LowWindChill < 999)
							windChill.Append($"[{recDate},{DayFile[i].LowWindChill.ToString(cumulus.TempFormat, InvC)}],");
						else
							windChill.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
					{
						// hi dewpt
						if (DayFile[i].HighDewPoint > -999)
							maxDew.Append($"[{recDate},{DayFile[i].HighDewPoint.ToString(cumulus.TempFormat, InvC)}],");
						else
							maxDew.Append($"[{recDate},null],");

						// lo dewpt
						if (DayFile[i].LowDewPoint < 999)
							minDew.Append($"[{recDate},{DayFile[i].LowDewPoint.ToString(cumulus.TempFormat, InvC)}],");
						else
							minDew.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
					{
						// hi feels like
						if (DayFile[i].HighFeelsLike > -999)
							maxFeels.Append($"[{recDate},{DayFile[i].HighFeelsLike.ToString(cumulus.TempFormat, InvC)}],");
						else
							maxFeels.Append($"[{recDate},null],");

						// lo feels like
						if (DayFile[i].LowFeelsLike < 999)
							minFeels.Append($"[{recDate},{DayFile[i].LowFeelsLike.ToString(cumulus.TempFormat, InvC)}],");
						else
							minFeels.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
					{
						// hi humidex
						if (DayFile[i].HighHumidex > -999)
							humidex.Append($"[{recDate},{DayFile[i].HighHumidex.ToString(cumulus.TempFormat, InvC)}],");
						else
							humidex.Append($"[{recDate},null],");
					}
				}
			}

			// remove trailing commas
			minTemp.Length--;
			maxTemp.Length--;
			avgTemp.Length--;


			if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
				sb.Append("\"minTemp\":" + minTemp.ToString() + "],");
			if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
				sb.Append("\"maxTemp\":" + maxTemp.ToString() + "],");
			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
				sb.Append("\"avgTemp\":" + avgTemp.ToString() + "],");
			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				heatIdx.Length--;
				sb.Append("\"heatIndex\":" + heatIdx.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				maxApp.Length--;
				minApp.Length--;
				sb.Append("\"maxApp\":" + maxApp.ToString() + "],");
				sb.Append("\"minApp\":" + minApp.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				windChill.Length--;
				sb.Append("\"windChill\":" + windChill.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				maxDew.Length--;
				minDew.Length--;
				sb.Append("\"maxDew\":" + maxDew.ToString() + "],");
				sb.Append("\"minDew\":" + minDew.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				maxFeels.Length--;
				minFeels.Length--;
				sb.Append("\"maxFeels\":" + maxFeels.ToString() + "],");
				sb.Append("\"minFeels\":" + minFeels.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				humidex.Length--;
				sb.Append("\"humidex\":" + humidex.ToString() + "],");
			}

			sb.Length--;
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyWindGraphData()
		{
			var InvC = new CultureInfo("");

			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder maxGust = new StringBuilder("[");
			StringBuilder windRun = new StringBuilder("[");
			StringBuilder maxWind = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{
					var recDate = Utils.ToPseudoJSTime(DayFile[i].Date);

					// hi gust
					maxGust.Append($"[{recDate},{DayFile[i].HighGust.ToString(cumulus.WindFormat, InvC)}],");
					// hi wind run
					windRun.Append($"[{recDate},{DayFile[i].WindRun.ToString(cumulus.WindRunFormat, InvC)}],");
					// hi wind
					maxWind.Append($"[{recDate},{DayFile[i].HighAvgWind.ToString(cumulus.WindAvgFormat, InvC)}],");
				}
			}

			maxGust.Length--;
			windRun.Length--;
			maxWind.Length--;

			sb.Append("\"maxGust\":" + maxGust.ToString() + "],");
			sb.Append("\"windRun\":" + windRun.ToString() + "],");
			sb.Append("\"maxWind\":" + maxWind.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyRainGraphData()
		{
			var InvC = new CultureInfo("");

			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder maxRRate = new StringBuilder("[");
			StringBuilder rain = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{

					long recDate = Utils.ToPseudoJSTime(DayFile[i].Date);

					// hi rain rate
					maxRRate.Append($"[{recDate},{DayFile[i].HighRainRate.ToString(cumulus.RainFormat, InvC)}],");
					// total rain
					rain.Append($"[{recDate},{DayFile[i].TotalRain.ToString(cumulus.RainFormat, InvC)}],");
				}
			}

			maxRRate.Length--;
			rain.Length--;

			sb.Append("\"maxRainRate\":" + maxRRate.ToString() + "],");
			sb.Append("\"rain\":" + rain.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyPressGraphData()
		{
			var InvC = new CultureInfo("");

			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder minBaro = new StringBuilder("[");
			StringBuilder maxBaro = new StringBuilder("[");


			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{

					long recDate = Utils.ToPseudoJSTime(DayFile[i].Date);

					// lo baro
					minBaro.Append($"[{recDate},{DayFile[i].LowPress.ToString(cumulus.PressFormat, InvC)}],");
					// hi baro
					maxBaro.Append($"[{recDate},{DayFile[i].HighPress.ToString(cumulus.PressFormat, InvC)}],");
				}
			}

			// Remove trailing commas
			minBaro.Length--;
			maxBaro.Length--;
			sb.Append("\"minBaro\":" + minBaro.ToString() + "],");
			sb.Append("\"maxBaro\":" + maxBaro.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyWindDirGraphData()
		{

			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder windDir = new StringBuilder("[");


			// Read the dayfile and extract the records from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{
					long recDate = Utils.ToPseudoJSTime(DayFile[i].Date);

					windDir.Append($"[{recDate},{DayFile[i].DominantWindBearing}],");

				}
			}

			// Remove trailing commas
			windDir.Length--;
			sb.Append("\"windDir\":" + windDir.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyHumGraphData()
		{
			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder minHum = new StringBuilder("[");
			StringBuilder maxHum = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{

					long recDate = Utils.ToPseudoJSTime(DayFile[i].Date);

					// lo humidity
					minHum.Append($"[{recDate},{DayFile[i].LowHumidity}],");
					// hi humidity
					maxHum.Append($"[{recDate},{DayFile[i].HighHumidity}],");
				}
			}
			// Remove trailing commas
			minHum.Length--;
			maxHum.Length--;

			sb.Append("\"minHum\":" + minHum.ToString() + "],");
			sb.Append("\"maxHum\":" + maxHum.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailySolarGraphData(bool local)
		{
			var InvC = new CultureInfo("");

			/* returns:
			 *	{
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 *	}
			 */

			StringBuilder sb = new StringBuilder("{");
			StringBuilder sunHours = new StringBuilder("[");
			StringBuilder solarRad = new StringBuilder("[");
			StringBuilder uvi = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0)
			{
				for (var i = 0; i < DayFile.Count(); i++)
				{
					long recDate = Utils.ToPseudoJSTime(DayFile[i].Date);

					if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local) && DayFile[i].SunShineHours > Cumulus.DefaultHiVal)
					{
						// sunshine hours
						sunHours.Append($"[{recDate},{DayFile[i].SunShineHours.ToString(InvC)}],");
					}

					if (cumulus.GraphOptions.Visible.Solar.IsVisible(local) && DayFile[i].HighSolar > Cumulus.DefaultHiVal)
					{
						// hi solar rad
						solarRad.Append($"[{recDate},{DayFile[i].HighSolar}],");
					}

					if (cumulus.GraphOptions.Visible.UV.IsVisible(local) && DayFile[i].HighUv > Cumulus.DefaultHiVal)
					{
						// hi UV-I
						uvi.Append($"[{recDate},{DayFile[i].HighUv.ToString(cumulus.UVFormat, InvC)}],");
					}
				}
			}

			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				sunHours.Length--;
				sb.Append("\"sunHours\":" + sunHours.ToString() + "]");
			}

			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
					sb.Append(',');

				solarRad.Length--;
				sb.Append("\"solarRad\":" + solarRad.ToString() + "]");
			}

			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local) || cumulus.GraphOptions.Visible.Solar.IsVisible(local))
					sb.Append(',');

				uvi.Length--;
				sb.Append("\"uvi\":" + uvi.ToString() + "]");
			}
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDegreeDaysGraphData(bool local)
		{
			var InvC = new CultureInfo("");

			StringBuilder sb = new StringBuilder("{");
			StringBuilder growdegdaysYears1 = new StringBuilder("{", 32768);
			StringBuilder growdegdaysYears2 = new StringBuilder("{", 32768);

			StringBuilder growYear1 = new StringBuilder("[", 8600);
			StringBuilder growYear2 = new StringBuilder("[", 8600);

			var options = $"\"options\":{{\"gddBase1\":{cumulus.GrowingBase1},\"gddBase2\":{cumulus.GrowingBase2},\"startMon\":{cumulus.GrowingYearStarts}}}";

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = cumulus.GrowingYearStarts < 3 ? 2000 : 1999;

			int startYear;

			var annualGrowingDegDays1 = 0.0;
			var annualGrowingDegDays2 = 0.0;

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0 && (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) || cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local)))
			{
				// we have to detect a new growing deg day year is starting
				nextYear = new DateTime(DayFile[0].Date.Year, cumulus.GrowingYearStarts, 1);

				if (DayFile[0].Date >= nextYear)
				{
					nextYear = nextYear.AddYears(1);
				}

				// are we starting part way through a year that does not start in January?
				if (DayFile[0].Date.Year == nextYear.Year)
				{
					startYear = DayFile[0].Date.Year - 1;
				}
				else
				{
					startYear = DayFile[0].Date.Year;
				}

				if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
				{
					growdegdaysYears1.Append($"\"{startYear}\":");
				}
				if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
				{
					growdegdaysYears2.Append($"\"{startYear}\":");
				}


				for (var i = 0; i < DayFile.Count(); i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (DayFile[i].Date >= nextYear)
					{
						if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) && growYear1.Length > 10)
						{
							// remove last comma
							growYear1.Length--;
							// close the year data
							growYear1.Append("],");
							// append to years array
							growdegdaysYears1.Append(growYear1);

							growYear1.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}
						if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local) && growYear2.Length > 10)
						{
							// remove last comma
							growYear2.Length--;
							// close the year data
							growYear2.Append("],");
							// append to years array
							growdegdaysYears2.Append(growYear2);

							growYear2.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}

						// reset the plot year for Southern hemisphere
						plotYear = cumulus.GrowingYearStarts < 3 ? 2000 : 1999;

						annualGrowingDegDays1 = 0;
						annualGrowingDegDays2 = 0;
						do
						{
							nextYear = nextYear.AddYears(1);
						}
						while (DayFile[i].Date >= nextYear);
					}

					// make all series the same year so they plot together
					// 2000 was a leap year, so make sure February falls in 2000
					// for Southern hemisphere this means the start year must be 1999
					if (cumulus.GrowingYearStarts > 2 && plotYear == 1999 && DayFile[i].Date.Month == 1)
					{
						plotYear++;
					}

					// make all series the same year so they plot together
					long recDate = Utils.ToPseudoJSTime(new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day));

					if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
					{
						// growing degree days
						var gdd = MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(DayFile[i].HighTemp), ConvertUnits.UserTempToC(DayFile[i].LowTemp), ConvertUnits.UserTempToC(cumulus.GrowingBase1), cumulus.GrowingCap30C);

						// annual accumulation
						annualGrowingDegDays1 += gdd;

						growYear1.Append($"[{recDate},{annualGrowingDegDays1.ToString("F1", InvC)}],");
					}

					if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
					{
						// growing degree days
						var gdd = MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(DayFile[i].HighTemp), ConvertUnits.UserTempToC(DayFile[i].LowTemp), ConvertUnits.UserTempToC(cumulus.GrowingBase2), cumulus.GrowingCap30C);

						// annual accumulation
						annualGrowingDegDays2 += gdd;

						growYear2.Append($"[{recDate},{annualGrowingDegDays2.ToString("F1", InvC)}],");
					}
				}
			}

			// remove last commas from the years arrays and close them off
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
			{
				if (growYear1[growYear1.Length - 1] == ',')
				{
					growYear1.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears1[growdegdaysYears1.Length - 1] == ']')
				{
					growdegdaysYears1.Append(',');
				}

				growdegdaysYears1.Append(growYear1 + "]");

				// add to main json
				sb.Append("\"GDD1\":" + growdegdaysYears1 + "},");
			}
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
			{
				if (growYear2[growYear2.Length - 1] == ',')
				{
					growYear2.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears2[growdegdaysYears2.Length - 1] == ']')
				{
					growdegdaysYears2.Append(',');
				}
				growdegdaysYears2.Append(growYear2 + "]");

				// add to main json
				sb.Append("\"GDD2\":" + growdegdaysYears2 + "},");
			}

			sb.Append(options);

			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllTempSumGraphData(bool local)
		{
			var InvC = new CultureInfo("");

			StringBuilder sb = new StringBuilder("{");
			StringBuilder tempSumYears0 = new StringBuilder("{", 32768);
			StringBuilder tempSumYears1 = new StringBuilder("{", 32768);
			StringBuilder tempSumYears2 = new StringBuilder("{", 32768);

			StringBuilder tempSum0 = new StringBuilder("[", 8600);
			StringBuilder tempSum1 = new StringBuilder("[", 8600);
			StringBuilder tempSum2 = new StringBuilder("[", 8600);

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = cumulus.TempSumYearStarts < 3 ? 2000 : 1999;

			int startYear;
			var annualTempSum0 = 0.0;
			var annualTempSum1 = 0.0;
			var annualTempSum2 = 0.0;

			var options = $"\"options\":{{\"sumBase1\":{cumulus.TempSumBase1},\"sumBase2\":{cumulus.TempSumBase2},\"startMon\":{cumulus.TempSumYearStarts}}}";

			// Read the day file list and extract the data from there
			if (DayFile.Count() > 0 && (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum1.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum2.IsVisible(local)))
			{
				// we have to detect a new year is starting
				nextYear = new DateTime(DayFile[0].Date.Year, cumulus.TempSumYearStarts, 1);

				if (DayFile[0].Date >= nextYear)
				{
					nextYear = nextYear.AddYears(1);
				}

				// are we starting part way through a year that does not start in January?
				if (DayFile[0].Date.Year == nextYear.Year)
				{
					startYear = DayFile[0].Date.Year - 1;
				}
				else
				{
					startYear = DayFile[0].Date.Year;
				}

				if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local))
				{
					tempSumYears0.Append($"\"{startYear}\":");
				}
				if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
				{
					tempSumYears1.Append($"\"{startYear}\":");
				}
				if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
				{
					tempSumYears2.Append($"\"{startYear}\":");
				}

				for (var i = 0; i < DayFile.Count(); i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (DayFile[i].Date >= nextYear)
					{
						if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local) && tempSum0.Length > 10)
						{
							// remove last comma
							tempSum0.Length--;
							// close the year data
							tempSum0.Append("],");
							// append to years array
							tempSumYears0.Append(tempSum0);

							tempSum0.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}
						if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local) && tempSum1.Length > 10)
						{
							// remove last comma
							tempSum1.Length--;
							// close the year data
							tempSum1.Append("],");
							// append to years array
							tempSumYears1.Append(tempSum1);

							tempSum1.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}
						if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local) && tempSum2.Length > 10)
						{
							// remove last comma
							tempSum2.Length--;
							// close the year data
							tempSum2.Append("],");
							// append to years array
							tempSumYears2.Append(tempSum2);

							tempSum2.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}

						// reset the plot year for Southern hemisphere
						plotYear = cumulus.TempSumYearStarts < 3 ? 2000 : 1999;

						annualTempSum0 = 0;
						annualTempSum1 = 0;
						annualTempSum2 = 0;

						do
						{
							nextYear = nextYear.AddYears(1);
						}
						while (DayFile[i].Date >= nextYear);
					}
					// make all series the same year so they plot together
					// 2000 was a leap year, so make sure February falls in 2000
					// for Southern hemisphere this means the start year must be 1999
					if (cumulus.TempSumYearStarts > 2 && plotYear == 1999 && DayFile[i].Date.Month == 1)
					{
						plotYear++;
					}

					long recDate = Utils.ToPseudoJSTime(new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day));

					if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local))
					{
						// annual accumulation
						annualTempSum0 += DayFile[i].AvgTemp;
						tempSum0.Append($"[{recDate},{annualTempSum0.ToString("F0", InvC)}],");
					}
					if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
					{
						// annual accumulation
						annualTempSum1 += DayFile[i].AvgTemp - cumulus.TempSumBase1;
						tempSum1.Append($"[{recDate},{annualTempSum1.ToString("F0", InvC)}],");
					}
					if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
					{
						// annual accumulation
						annualTempSum2 += DayFile[i].AvgTemp - cumulus.TempSumBase2;
						tempSum2.Append($"[{recDate},{annualTempSum2.ToString("F0", InvC)}],");
					}
				}
			}

			// remove last commas from the years arrays and close them off
			if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local))
			{
				if (tempSum0[tempSum0.Length - 1] == ',')
				{
					tempSum0.Length--;
				}

				// have previous years been appended?
				if (tempSumYears0[tempSumYears0.Length - 1] == ']')
				{
					tempSumYears0.Append(',');
				}

				tempSumYears0.Append(tempSum0 + "]");

				// add to main json
				sb.Append("\"Sum0\":" + tempSumYears0 + "}");

				if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
					sb.Append(',');
			}
			if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
			{
				if (tempSum1[tempSum1.Length - 1] == ',')
				{
					tempSum1.Length--;
				}

				// have previous years been appended?
				if (tempSumYears1[tempSumYears1.Length - 1] == ']')
				{
					tempSumYears1.Append(',');
				}

				tempSumYears1.Append(tempSum1 + "]");

				// add to main json
				sb.Append("\"Sum1\":" + tempSumYears1 + "},");
			}
			if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
			{
				if (tempSum2[tempSum2.Length - 1] == ',')
				{
					tempSum2.Length--;
				}

				// have previous years been appended?
				if (tempSumYears2[tempSumYears2.Length - 1] == ']')
				{
					tempSumYears2.Append(',');
				}

				tempSumYears2.Append(tempSum2 + "]");

				// add to main json
				sb.Append("\"Sum2\":" + tempSumYears2 + "},");
			}

			sb.Append(options);

			sb.Append('}');

			return sb.ToString();
		}



		internal string GetCurrentData()
		{
			// no need to use multiplier as rose is all relative
			StringBuilder windRoseData = new StringBuilder(windcounts[0].ToString(cumulus.WindFormat, CultureInfo.InvariantCulture), 4096);
			lock (windRoseData)
			{
				for (var i = 1; i < cumulus.NumWindRosePoints; i++)
				{
					windRoseData.Append(',');
					windRoseData.Append(windcounts[i].ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
				}
			}
			string stormRainStart = StartOfStorm == DateTime.MinValue ? "-----" : StartOfStorm.ToString("d");

			var alarms = new List<DashboardAlarms>();
			if (cumulus.NewRecordAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.NewRecordAlarm.Id, cumulus.NewRecordAlarm.Triggered));
			if (cumulus.LowTempAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.LowTempAlarm.Id, cumulus.LowTempAlarm.Triggered));
			if (cumulus.HighTempAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.HighTempAlarm.Id, cumulus.HighTempAlarm.Triggered));
			if (cumulus.TempChangeAlarm.Enabled)
			{
				alarms.Add(new DashboardAlarms(cumulus.TempChangeAlarm.IdUp, cumulus.TempChangeAlarm.UpTriggered));
				alarms.Add(new DashboardAlarms(cumulus.TempChangeAlarm.IdDown, cumulus.TempChangeAlarm.DownTriggered));
			}
			if (cumulus.HighRainTodayAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.HighRainTodayAlarm.Id, cumulus.HighRainTodayAlarm.Triggered));
			if (cumulus.HighRainRateAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.HighRainRateAlarm.Id, cumulus.HighRainRateAlarm.Triggered));
			if (cumulus.IsRainingAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.IsRainingAlarm.Id, cumulus.IsRainingAlarm.Triggered));
			if (cumulus.DataStoppedAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.DataStoppedAlarm.Id, cumulus.DataStoppedAlarm.Triggered));
			if (cumulus.LowPressAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.LowPressAlarm.Id, cumulus.LowPressAlarm.Triggered));
			if (cumulus.HighPressAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.HighPressAlarm.Id, cumulus.HighPressAlarm.Triggered));
			if (cumulus.PressChangeAlarm.Enabled)
			{
				alarms.Add(new DashboardAlarms(cumulus.PressChangeAlarm.IdUp, cumulus.PressChangeAlarm.UpTriggered));
				alarms.Add(new DashboardAlarms(cumulus.PressChangeAlarm.IdDown, cumulus.PressChangeAlarm.DownTriggered));
			}
			if (cumulus.HighGustAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.HighGustAlarm.Id, cumulus.HighGustAlarm.Triggered));
			if (cumulus.HighWindAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.HighWindAlarm.Id, cumulus.HighWindAlarm.Triggered));
			if (cumulus.SensorAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.SensorAlarm.Id, cumulus.SensorAlarm.Triggered));
			if (cumulus.BatteryLowAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.BatteryLowAlarm.Id, cumulus.BatteryLowAlarm.Triggered));
			if (cumulus.SpikeAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.SpikeAlarm.Id, cumulus.SpikeAlarm.Triggered));
			if (cumulus.FtpAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.FtpAlarm.Id, cumulus.FtpAlarm.Triggered));
			if (cumulus.ThirdPartyAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.ThirdPartyAlarm.Id, cumulus.ThirdPartyAlarm.Triggered));
			if (cumulus.MySqlUploadAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.MySqlUploadAlarm.Id, cumulus.MySqlUploadAlarm.Triggered));
			if (cumulus.UpgradeAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.UpgradeAlarm.Id, cumulus.UpgradeAlarm.Triggered));

			for (var i = 0; i < cumulus.UserAlarms.Count; i++)
			{
				if (cumulus.UserAlarms[i].Enabled)
					alarms.Add(new DashboardAlarms("AlarmUser" + i, cumulus.UserAlarms[i].Triggered));
			}


			var data = new DataStruct(cumulus, OutdoorTemperature, OutdoorHumidity, TempTotalToday / tempsamplestoday, IndoorTemperature, OutdoorDewpoint, WindChill, IndoorHumidity,
				Pressure, WindLatest, WindAverage, RecentMaxGust, WindRunToday, Bearing, AvgBearing, RainToday, RainYesterday, RainMonth, RainYear, RainRate,
				RainLastHour, HeatIndex, Humidex, ApparentTemperature, temptrendval, presstrendval, HiLoToday.HighGust, HiLoToday.HighGustTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.HighWind,
				HiLoToday.HighGustBearing, cumulus.Units.WindText, BearingRangeFrom10, BearingRangeTo10, windRoseData.ToString(), HiLoToday.HighTemp, HiLoToday.LowTemp,
				HiLoToday.HighTempTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.LowTempTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.HighPress, HiLoToday.LowPress, HiLoToday.HighPressTime.ToString(cumulus.ProgramOptions.TimeFormat),
				HiLoToday.LowPressTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.HighRainRate, HiLoToday.HighRainRateTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.HighHumidity, HiLoToday.LowHumidity,
				HiLoToday.HighHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.LowHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat), cumulus.Units.PressText, cumulus.Units.TempText, cumulus.Units.RainText,
				HiLoToday.HighDewPoint, HiLoToday.LowDewPoint, HiLoToday.HighDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.LowDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.LowWindChill,
				HiLoToday.LowWindChillTime.ToString(cumulus.ProgramOptions.TimeFormat), (int) SolarRad, (int) HiLoToday.HighSolar, HiLoToday.HighSolarTime.ToString(cumulus.ProgramOptions.TimeFormat), UV, HiLoToday.HighUv,
				HiLoToday.HighUvTime.ToString(cumulus.ProgramOptions.TimeFormat), forecaststr, getTimeString(cumulus.SunRiseTime, cumulus.ProgramOptions.TimeFormat), getTimeString(cumulus.SunSetTime, cumulus.ProgramOptions.TimeFormat),
				getTimeString(cumulus.MoonRiseTime, cumulus.ProgramOptions.TimeFormat), getTimeString(cumulus.MoonSetTime, cumulus.ProgramOptions.TimeFormat), HiLoToday.HighHeatIndex, HiLoToday.HighHeatIndexTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.HighAppTemp,
				HiLoToday.LowAppTemp, HiLoToday.HighAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.LowAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat), CurrentSolarMax,
				AllTime.HighPress.Val, AllTime.LowPress.Val, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
				HiLoToday.HighHourlyRain, HiLoToday.HighHourlyRainTime.ToString(cumulus.ProgramOptions.TimeFormat), "F" + cumulus.Beaufort(HiLoToday.HighWind), "F" + cumulus.Beaufort(WindAverage),
				cumulus.BeaufortDesc(WindAverage), LastDataReadTimestamp.ToString(cumulus.ProgramOptions.TimeFormatLong), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
				FeelsLike, HiLoToday.HighFeelsLike, HiLoToday.HighFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat), HiLoToday.LowFeelsLike, HiLoToday.LowFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat),
				HiLoToday.HighHumidex, HiLoToday.HighHumidexTime.ToString(cumulus.ProgramOptions.TimeFormat), alarms);

			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(DataStruct));
					DataContractJsonSerializerSettings s = new DataContractJsonSerializerSettings();
					ds.WriteObject(stream, data);
					string jsonString = Encoding.UTF8.GetString(stream.ToArray());
					stream.Close();
					return jsonString;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogConsoleMessage(ex.Message);
				return "";
			}

		}

		// Returns true if the gust value exceeds current RecentMaxGust, false if it fails
		public bool CheckHighGust(double gust, int gustdir, DateTime timestamp)
		{
			if (gust >= cumulus.Limit.WindHigh)
			{
				cumulus.LogSpikeRemoval("Wind Gust greater than the limit; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={gust:F1} HighLimit={cumulus.Limit.WindHigh:F1}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind Gust greater than limit - NewVal={gust:F1}, OldVal={cumulus.Limit.WindHigh:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return false;
			}

			if (gust > HiLoToday.HighGust)
			{
				HiLoToday.HighGust = gust;
				HiLoToday.HighGustTime = timestamp;
				HiLoToday.HighGustBearing = gustdir;
				WriteTodayFile(timestamp, false);
			}
			if (gust > ThisMonth.HighGust.Val)
			{
				ThisMonth.HighGust.Val = gust;
				ThisMonth.HighGust.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (gust > ThisYear.HighGust.Val)
			{
				ThisYear.HighGust.Val = gust;
				ThisYear.HighGust.Ts = timestamp;
				WriteYearIniFile();
			}
			// All time high gust?
			if (gust > AllTime.HighGust.Val)
			{
				SetAlltime(AllTime.HighGust, gust, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighGust", gust, true, timestamp);

			cumulus.HighGustAlarm.CheckAlarm(gust);

			return gust > RecentMaxGust;
		}


		public void CheckHighAvgSpeed(DateTime timestamp)
		{
			if (WindAverage > HiLoToday.HighWind)
			{
				HiLoToday.HighWind = WindAverage;
				HiLoToday.HighWindTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (WindAverage > ThisMonth.HighWind.Val)
			{
				ThisMonth.HighWind.Val = WindAverage;
				ThisMonth.HighWind.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (WindAverage > ThisYear.HighWind.Val)
			{
				ThisYear.HighWind.Val = WindAverage;
				ThisYear.HighWind.Ts = timestamp;
				WriteYearIniFile();
			}

			// All time high wind speed?
			if (WindAverage > AllTime.HighWind.Val)
			{
				SetAlltime(AllTime.HighWind, WindAverage, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighWind", WindAverage, true, timestamp);

			cumulus.HighWindAlarm.CheckAlarm(WindAverage);
		}


		public void UpdateAPRS()
		{
			// See: http://aprs.net/vm/DOS/PROTOCOL.HTM

			if (DataStopped)
			{
				// No data coming in, do nothing
				return;
			}

			cumulus.LogDebugMessage("Updating CWOP");
			using (var client = new TcpClient(cumulus.APRS.Server, cumulus.APRS.Port))
			using (var ns = client.GetStream())
			{
				try
				{
					using (StreamWriter writer = new StreamWriter(ns))
					{
						StringBuilder message = new StringBuilder(256);
						message.Append($"user {cumulus.APRS.ID} pass {cumulus.APRS.PW} vers Cumulus {cumulus.Version}");

						//Byte[] data = Encoding.ASCII.GetBytes(message.ToString());

						cumulus.LogDebugMessage("Sending user and pass to CWOP");

						writer.WriteLine(message.ToString());
						writer.Flush();

						Thread.Sleep(3000);

						string timeUTC = DateTime.Now.ToUniversalTime().ToString("ddHHmm");

						message.Clear();
						message.Append($"{cumulus.APRS.ID}>APRS,TCPIP*:@{timeUTC}z{APRSLat(cumulus)}/{APRSLon(cumulus)}");
						// bearing _nnn
						message.Append($"_{AvgBearing:D3}");
						// wind speed mph /nnn
						message.Append($"/{APRSwind(WindAverage)}");
						// wind gust last 5 mins mph gnnn
						message.Append($"g{APRSwind(RecentMaxGust)}");
						// temp F tnnn
						message.Append($"t{APRStemp(OutdoorTemperature)}");
						// rain last hour 0.01 inches rnnn
						message.Append($"r{APRSrain(RainLastHour)}");
						// rain last 24 hours 0.01 inches pnnn
						message.Append($"p{APRSrain(RainLast24Hour)}");
						message.Append('P');
						if (cumulus.RolloverHour == 0)
						{
							// use today"s rain for safety
							message.Append(APRSrain(RainToday));
						}
						else
						{
							// 0900 day, use midnight calculation
							message.Append(APRSrain(RainSinceMidnight));
						}
						if ((!cumulus.APRS.HumidityCutoff) || (ConvertUnits.UserTempToC(OutdoorTemperature) >= -10))
						{
							// humidity Hnn
							message.Append($"h{APRShum(OutdoorHumidity)}");
						}
						// bar 0.1mb Bnnnnn
						message.Append($"b{APRSpress(AltimeterPressure)}");
						if (cumulus.APRS.SendSolar)
						{
							message.Append(APRSsolarradStr(Convert.ToInt32(SolarRad)));
						}

						// station type e<string>
						message.Append($"eCumulus{cumulus.APRSstationtype[cumulus.StationType]}");

						cumulus.LogDebugMessage($"Sending: {message}");

						//data = Encoding.ASCII.GetBytes(message.ToString());

						writer.WriteLine(message.ToString());
						writer.Flush();

						Thread.Sleep(3000);
						writer.Close();
					}
					cumulus.LogDebugMessage("End of CWOP update");
				}
				catch (Exception e)
				{
					cumulus.LogErrorMessage("CWOP error: " + e.Message);
				}
			}
		}

		/// <summary>
		/// Takes latitude in degrees and converts it to APRS format ddmm.hhX:
		/// (hh = hundredths of a minute)
		/// e.g. 5914.55N
		/// </summary>
		/// <returns></returns>
		private string APRSLat(Cumulus cumulus)
		{
			string dir;
			decimal lat;
			int d, m, s;
			if (cumulus.Latitude < 0)
			{
				lat = -cumulus.Latitude;
				dir = "S";
			}
			else
			{
				lat = cumulus.Latitude;
				dir = "N";
			}

			cumulus.DegToDMS(lat, out d, out m, out s);
			int hh = (int) Math.Round(s * 100 / 60.0);

			return String.Format("{0:D2}{1:D2}.{2:D2}{3}", d, m, hh, dir);
		}

		/// <summary>
		/// Takes longitude in degrees and converts it to APRS format dddmm.hhX:
		/// (hh = hundredths of a minute)
		/// e.g. 15914.55W
		/// </summary>
		/// <returns></returns>
		private string APRSLon(Cumulus cumulus)
		{
			string dir;
			decimal lon;
			int d, m, s;
			if (cumulus.Longitude < 0)
			{
				lon = -cumulus.Longitude;
				dir = "W";
			}
			else
			{
				lon = cumulus.Longitude;
				dir = "E";
			}

			cumulus.DegToDMS(lon, out d, out m, out s);
			int hh = (int) Math.Round(s * 100 / 60.0);

			return String.Format("{0:D3}{1:D2}.{2:D2}{3}", d, m, hh, dir);
		}

		/// <summary>
		/// input is in Units.Wind units, convert to mph for APRS
		/// and return 3 digits
		/// </summary>
		/// <param name="wind"></param>
		/// <returns></returns>
		private string APRSwind(double wind)
		{
			var windMPH = Convert.ToInt32(ConvertUnits.UserWindToMPH(wind));
			return windMPH.ToString("D3");
		}

		/// <summary>
		/// input is in Units.Press units, convert to tenths of mb for APRS
		/// return 5 digit string
		/// </summary>
		/// <param name="press"></param>
		/// <returns></returns>
		public string APRSpress(double press)
		{
			var press10mb = Convert.ToInt32(ConvertUnits.UserPressToMB(press) * 10);
			return press10mb.ToString("D5");
		}

		/// <summary>
		/// return humidity as 2-digit string
		/// represent 100 by 00
		/// send 1 instead of zero
		/// </summary>
		/// <param name="hum"></param>
		/// <returns></returns>
		public string APRShum(int hum)
		{
			if (hum == 100)
			{
				return "00";
			}

			if (hum == 0)
			{
				return "01";
			}

			return hum.ToString("D2");
		}

		/// <summary>
		/// input is in RainUnit units, convert to hundredths of inches for APRS
		/// and return 3 digits
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		public string APRSrain(double rain)
		{
			var rain100IN = Convert.ToInt32(ConvertUnits.UserRainToIN(rain) * 100);
			return rain100IN.ToString("D3");
		}

		public class CommTimer : IDisposable
		{
			public Timer tmrComm = new Timer();
			public bool timedout = false;
			public CommTimer()
			{
				timedout = false;
				tmrComm.AutoReset = false;
				tmrComm.Enabled = false;
				tmrComm.Interval = 1000; //default to 1 second
				tmrComm.Elapsed += new ElapsedEventHandler(OnTimedCommEvent);
			}

			public void OnTimedCommEvent(object source, ElapsedEventArgs e)
			{
				timedout = true;
				tmrComm.Stop();
			}

			public void Start(double timeoutperiod)
			{
				tmrComm.Interval = timeoutperiod;             //time to time out in milliseconds
				tmrComm.Stop();
				timedout = false;
				tmrComm.Start();
			}

			public void Stop()
			{
				tmrComm.Stop();
				timedout = true;
			}

			public void Dispose()
			{
				tmrComm.Close();
				tmrComm.Dispose();
			}
		}

	}

	//public partial class CumulusData : DataContext
	//{
	//   public Table<data> Datas;
	//   //public Table<extradata> ExtraData;
	//   public CumulusData(string connection) : base(connection) { }
	//}

	/*
	public class Last3HourData
	{
		public DateTime timestamp;
		public double pressure;
		public double temperature;

		public Last3HourData(DateTime ts, double press, double temp)
		{
			timestamp = ts;
			pressure = press;
			temperature = temp;
		}
	}
	*/

	public class CWindRecent
	{
		[PrimaryKey]
		public DateTime Timestamp { get; set; }
		public double Gust { get; set; }  // calibrated "gust" as read from station
		public double Speed { get; set; } // calibrated "speed" as read from station
	}

	public class LastHourData
	{
		public DateTime timestamp;
		public double raincounter;
		public double temperature;

		public LastHourData(DateTime ts, double rain, double temp)
		{
			timestamp = ts;
			raincounter = rain;
			temperature = temp;
		}
	}

	public class GraphData
	{
		public DateTime timestamp;
		public double raincounter;
		public double RainToday;
		public double rainrate;
		public double temperature;
		public double dewpoint;
		public double apptemp;
		public double feelslike;
		public double humidex;
		public double windchill;
		public double heatindex;
		public double insidetemp;
		public double pressure;
		public double windspeed;
		public double windgust;
		public int avgwinddir;
		public int winddir;
		public int humidity;
		public int inhumidity;
		public double solarrad;
		public double solarmax;
		public double uvindex;
		public double pm2p5;
		public double pm10;

		public GraphData(DateTime ts, double rain, double raintoday, double rrate, double temp, double dp, double appt, double chill, double heat, double intemp, double press,
			double speed, double gust, int avgdir, int wdir, int hum, int inhum, double solar, double smax, double uv, double feels, double humidx, double pm2p5, double pm10)
		{
			timestamp = ts;
			raincounter = rain;
			RainToday = raintoday;
			rainrate = rrate;
			temperature = temp;
			dewpoint = dp;
			apptemp = appt;
			windchill = chill;
			heatindex = heat;
			insidetemp = intemp;
			pressure = press;
			windspeed = speed;
			windgust = gust;
			avgwinddir = avgdir;
			winddir = wdir;
			humidity = hum;
			inhumidity = inhum;
			solarrad = solar;
			solarmax = smax;
			uvindex = uv;
			feelslike = feels;
			humidex = humidx;
			this.pm2p5 = pm2p5;
			this.pm10 = pm10;
		}
	}

	public class Last10MinWind
	{
		public DateTime timestamp;
		public double gust;
		public double speed;
		public double gustX;
		public double gustY;

		public Last10MinWind(DateTime ts, double windgust, double windspeed, double Xgust, double Ygust)
		{
			timestamp = ts;
			gust = windgust;
			speed = windspeed;
			gustX = Xgust;
			gustY = Ygust;
		}
	}

	public class RecentDailyData
	{
		public DateTime timestamp;
		public double rain;
		public double sunhours;
		public double mintemp;
		public double maxtemp;
		public double avgtemp;

		public RecentDailyData(DateTime ts, double dailyrain, double sunhrs, double mint, double maxt, double avgt)
		{
			timestamp = ts;
			rain = dailyrain;
			sunhours = sunhrs;
			mintemp = mint;
			maxtemp = maxt;
			avgtemp = avgt;
		}
	}

	public class RecentData
	{
		[PrimaryKey]
		public DateTime Timestamp { get; set; }

		public double WindSpeed { get; set; }
		public double WindGust { get; set; }
		public double WindLatest { get; set; }
		public int WindDir { get; set; }
		public int WindAvgDir { get; set; }
		public double OutsideTemp { get; set; }
		public double WindChill { get; set; }
		public double DewPoint { get; set; }
		public double HeatIndex { get; set; }
		public double Humidity { get; set; }
		public double Pressure { get; set; }
		public double RainToday { get; set; }
		public int SolarRad { get; set; }
		public double UV { get; set; }
		public double raincounter { get; set; }
		public double FeelsLike { get; set; }
		public double Humidex { get; set; }
		public double AppTemp { get; set; }

		public double IndoorTemp { get; set; }
		public int IndoorHumidity { get; set; }
		public int SolarMax { get; set; }
		public double Pm2p5 { get; set; }
		public double Pm10 { get; set; }
		public double RainRate { get; set; }
	}

	public class AvgData
	{
		public double temp { get; set; }
		public double wind { get; set; }
		public double solar { get; set; }
		public double hum { get; set; }
	}

	class EtData
	{
		public double avgTemp { get; set; }
		public int avgHum { get; set; }
		public double avgSol { get; set; }
		public double avgSolMax { get; set; }
		public double avgWind { get; set; }
		public double avgPress { get; set; }
	}

	public class SqlCache
	{
		[AutoIncrement, PrimaryKey]
		public int? key { get; set; }
		public string statement { get; set; }
	}

	/*
	public class StandardData
	{
		[PrimaryKey]
		public DateTime Timestamp { get; set; }

		public int Interval { get; set; }

		public double OutTemp { get; set; }
		public double LoOutTemp { get; set; }
		public double HiOutTemp { get; set; }

		public double DewPoint { get; set; }
		public double LoDewPoint { get; set; }
		public double HiDewPoint { get; set; }

		public double WindChill { get; set; }
		public double LoWindChill { get; set; }
		public double HiWindChill { get; set; }

		public double InTemp { get; set; }
		public double LoInTemp { get; set; }
		public double HiInTemp { get; set; }

		public double Pressure { get; set; }
		public double LoPressure { get; set; }
		public double HiPressure { get; set; }
	}
	*/

	public class AllTimeRecords
	{
		// Add an indexer so we can reference properties with a string
		public AllTimeRec this[string propertyName]
		{
			get
			{
				// probably faster without reflection:
				// like:  return Properties.Settings.Default.PropertyValues[propertyName]
				// instead of the following
				Type myType = typeof(AllTimeRecords);
				PropertyInfo myPropInfo = myType.GetProperty(propertyName);
				return (AllTimeRec) myPropInfo.GetValue(this, null);
			}
			set
			{
				Type myType = typeof(AllTimeRecords);
				PropertyInfo myPropInfo = myType.GetProperty(propertyName);
				myPropInfo.SetValue(this, value, null);
			}
		}

		public AllTimeRec HighTemp { get; set; } = new AllTimeRec(0);
		public AllTimeRec LowTemp { get; set; } = new AllTimeRec(1);
		public AllTimeRec HighGust { get; set; } = new AllTimeRec(2);
		public AllTimeRec HighWind { get; set; } = new AllTimeRec(3);
		public AllTimeRec LowChill { get; set; } = new AllTimeRec(4);
		public AllTimeRec HighRainRate { get; set; } = new AllTimeRec(5);
		public AllTimeRec DailyRain { get; set; } = new AllTimeRec(6);
		public AllTimeRec HourlyRain { get; set; } = new AllTimeRec(7);
		public AllTimeRec LowPress { get; set; } = new AllTimeRec(8);
		public AllTimeRec HighPress { get; set; } = new AllTimeRec(9);
		public AllTimeRec MonthlyRain { get; set; } = new AllTimeRec(10);
		public AllTimeRec HighMinTemp { get; set; } = new AllTimeRec(11);
		public AllTimeRec LowMaxTemp { get; set; } = new AllTimeRec(12);
		public AllTimeRec HighHumidity { get; set; } = new AllTimeRec(13);
		public AllTimeRec LowHumidity { get; set; } = new AllTimeRec(14);
		public AllTimeRec HighAppTemp { get; set; } = new AllTimeRec(15);
		public AllTimeRec LowAppTemp { get; set; } = new AllTimeRec(16);
		public AllTimeRec HighHeatIndex { get; set; } = new AllTimeRec(17);
		public AllTimeRec HighDewPoint { get; set; } = new AllTimeRec(18);
		public AllTimeRec LowDewPoint { get; set; } = new AllTimeRec(19);
		public AllTimeRec HighWindRun { get; set; } = new AllTimeRec(20);
		public AllTimeRec LongestDryPeriod { get; set; } = new AllTimeRec(21);
		public AllTimeRec LongestWetPeriod { get; set; } = new AllTimeRec(22);
		public AllTimeRec HighDailyTempRange { get; set; } = new AllTimeRec(23);
		public AllTimeRec LowDailyTempRange { get; set; } = new AllTimeRec(24);
		public AllTimeRec HighFeelsLike { get; set; } = new AllTimeRec(25);
		public AllTimeRec LowFeelsLike { get; set; } = new AllTimeRec(26);
		public AllTimeRec HighHumidex { get; set; } = new AllTimeRec(27);
		public AllTimeRec HighRain24Hours { get; set; } = new AllTimeRec(28);
	}

	public class AllTimeRec
	{
		private static readonly string[] alltimedescs = new[]
		{
			"High temperature", "Low temperature", "High gust", "High wind speed", "Low wind chill", "High rain rate", "High daily rain",
			"High hourly rain", "Low pressure", "High pressure", "Highest monthly rainfall", "Highest minimum temp", "Lowest maximum temp",
			"High humidity", "Low humidity", "High apparent temp", "Low apparent temp", "High heat index", "High dew point", "Low dew point",
			"High daily windrun", "Longest dry period", "Longest wet period", "High daily temp range", "Low daily temp range",
			"High feels like", "Low feels like", "High Humidex", "High 24 hour rain"
		};
		private readonly int idx;

		public AllTimeRec(int index)
		{
			idx = index;
		}
		public double Val { get; set; }
		public DateTime Ts { get; set; }
		public string Desc
		{
			get
			{
				return alltimedescs[idx];
			}
		}

		public string GetValString(string format = "")
		{
			if (Val == Cumulus.DefaultHiVal || Val == Cumulus.DefaultLoVal)
				return "-";
			else
				return Val.ToString(format);
		}

		public string GetTsString(string format = "")
		{
			if (Ts == DateTime.MinValue)
				return "-";
			else
				return Ts.ToString(format);
		}

	}
}
