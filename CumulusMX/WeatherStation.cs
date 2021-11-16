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
using System.Timers;
using SQLite;
using Timer = System.Timers.Timer;
using ServiceStack.Text;
using System.Web;

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

		private readonly Object monthIniThreadLock = new Object();
		public readonly Object yearIniThreadLock = new Object();
		public readonly Object alltimeIniThreadLock = new Object();
		public readonly Object monthlyalltimeIniThreadLock = new Object();

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

		public bool[] WMR928ChannelPresent = new[] { false, false, false, false };
		public bool[] WMR928ExtraTempValueOnly = new[] { false, false, false, false };
		public double[] WMR928ExtraTempValues = new[] { 0.0, 0.0, 0.0, 0.0 };
		public double[] WMR928ExtraDPValues = new[] { 0.0, 0.0, 0.0, 0.0 };
		public int[] WMR928ExtraHumValues = new[] { 0, 0, 0, 0 };


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

		public double raindaystart = 0.0;
		public double Raincounter = 0.0;
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
			public double LowWindChill;
			public DateTime LowWindChillTime;
			public double HighDewPoint;
			public DateTime HighDewPointTime;
			public double LowDewPoint;
			public DateTime LowDewPointTime;
			public double HighSolar;
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
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewPoint = 999,
			LowPress = 9999,
			LowHumidity = 100
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

		private Thread t;

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

		public WeatherStation(Cumulus cumulus)
		{
			// save the reference to the owner
			this.cumulus = cumulus;

			// initialise the monthly array of records - element zero is not used
			for (var i = 1; i <= 12; i++)
			{
				MonthlyRecs[i] = new AllTimeRecords();
			}

			CumulusForecast = cumulus.ForecastNotAvailable;
			wsforecast = cumulus.ForecastNotAvailable;

			ExtraTemp = new double[11];
			ExtraHum = new double[11];
			ExtraDewPoint = new double[11];
			UserTemp = new double[9];

			windcounts = new double[16];
			WindRecent = new TWindRecent[MaxWindRecent];
			WindVec = new TWindVec[MaxWindRecent];

			ReadTodayFile();
			ReadYesterdayFile();
			ReadAlltimeIniFile();
			ReadMonthlyAlltimeIniFile();
			ReadMonthIniFile();
			ReadYearIniFile();
			LoadDayFile();

			GetRainCounter();
			GetRainFallTotals();

			//RecentDataDb = new SQLiteConnection(":memory:", true);
			RecentDataDb = new SQLiteConnection(cumulus.dbfile, false);
			RecentDataDb.CreateTable<RecentData>();
			// switch off full synchronisation - the data base isn't that critical and we get a performance boost
			RecentDataDb.Execute("PRAGMA synchronous = NORMAL");

			var rnd = new Random();
			versionCheckTime = new DateTime(1, 1, 1, rnd.Next(0, 23), rnd.Next(0, 59), 0);
		}

		private void GetRainCounter()
		{
			// Find today's rain so far from last record in log file
			bool midnightrainfound = false;
			//string LogFile = cumulus.Datapath + cumulus.LastUpdateTime.ToString("MMMyy") + "log.txt";
			string LogFile = cumulus.GetLogFileName(cumulus.LastUpdateTime);
			double raincount = 0;
			string logdate = "00/00/00";
			string prevlogdate = "00/00/00";
			string listSep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
			string todaydatestring = cumulus.LastUpdateTime.ToString("dd/MM/yy");

			cumulus.LogMessage("Finding raintoday from logfile " + LogFile);
			cumulus.LogMessage("Expecting listsep=" + listSep + " decimal=" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

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
							RainToday = Double.Parse(st[9]);
							// get date of this entry
							logdate = st[0];
							if (!midnightrainfound)
							{
								if (logdate != prevlogdate)
								{
									if (todaydatestring == logdate)
									{
										// this is the first entry of a new day AND the new day is today
										midnightrainfound = true;
										cumulus.LogMessage("Midnight rain found in the following entry:");
										cumulus.LogMessage(line);
										raincount = Double.Parse(st[11]);
									}
								}
							}
							prevlogdate = logdate;
						}
					}
				}
				catch (Exception E)
				{
					cumulus.LogMessage("Error on line " + linenum + " of " + LogFile + ": " + E.Message);
				}
			}

			if (midnightrainfound)
			{
				if ((logdate.Substring(0, 2) == "01") && (logdate.Substring(3, 2) == cumulus.RainSeasonStart.ToString("D2")) && (cumulus.Manufacturer == cumulus.DAVIS))
				{
					// special case: rain counter is about to be reset
					//TODO: MC: Hmm are there issues here, what if the console clock is wrong and it does not reset for another hour, or it already reset and we have had rain since?
					var month = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(cumulus.RainSeasonStart);
					cumulus.LogMessage($"Special case, Davis station on 1st of {month}. Set midnight rain count to zero");
					midnightraincount = 0;
				}
				else
				{
					cumulus.LogMessage("Midnight rain found, setting midnight rain count = " + raincount);
					midnightraincount = raincount;
				}
			}
			else
			{
				cumulus.LogMessage("Midnight rain not found, setting midnight count to raindaystart = " + raindaystart);
				midnightraincount = raindaystart;
			}

			// If we do not have a rain counter value for start of day from Today.ini, then use the midnight counter
			if (initialiseRainCounterOnFirstData)
			{
				Raincounter = midnightraincount + (RainToday / cumulus.Calib.Rain.Mult);
			}
			else
			{
				// Otherwise use the counter value from today.ini plus total so far today to infer the counter value
				Raincounter = raindaystart + (RainToday / cumulus.Calib.Rain.Mult);
			}

			cumulus.LogMessage("Checking rain counter = " + Raincounter);
			if (Raincounter < 0)
			{
				cumulus.LogMessage("Rain counter negative, setting to zero");
				Raincounter = 0;
			}
			else
			{
				cumulus.LogMessage("Rain counter set to = " + Raincounter);
			}
		}

		public void GetRainFallTotals()
		{
			cumulus.LogMessage("Getting rain totals, rain season start = " + cumulus.RainSeasonStart);
			rainthismonth = 0;
			rainthisyear = 0;
			// get today"s date for month check; allow for 0900 roll-over
			var hourInc = cumulus.GetHourInc();
			var ModifiedNow = DateTime.Now.AddHours(hourInc);
			// avoid any funny locale peculiarities on date formats
			string Today = ModifiedNow.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
			cumulus.LogMessage("Today = " + Today);
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
						rainthisyear += rec.TotalRain;
					}
					// This month?
					if ((rec.Date.Month == ModifiedNow.Month) && (rec.Date.Year == ModifiedNow.Year))
					{
						rainthismonth += rec.TotalRain;
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetRainfallTotals: Error - " + ex.Message);
			}

			cumulus.LogMessage("Rainthismonth from dayfile: " + rainthismonth);
			cumulus.LogMessage("Rainthisyear from dayfile: " + rainthisyear);

			// Add in year-to-date rain (if necessary)
			if (cumulus.YTDrainyear == Convert.ToInt32(Today.Substring(6, 2)) + 2000)
			{
				cumulus.LogMessage("Adding YTD rain: " + cumulus.YTDrain);
				rainthisyear += cumulus.YTDrain;
				cumulus.LogMessage("Rainthisyear: " + rainthisyear);
			}
		}

		public void ReadTodayFile()
		{
			if (!File.Exists(cumulus.TodayIniFile))
			{
				FirstRun = true;
			}

			IniFile ini = new IniFile(cumulus.TodayIniFile);

			cumulus.LogConsoleMessage("Today.ini = " + cumulus.TodayIniFile);

			var todayfiledate = ini.GetValue("General", "Date", "00/00/00");
			var timestampstr = ini.GetValue("General", "Timestamp", DateTime.Now.ToString("s"));

			cumulus.LogConsoleMessage("Last update=" + timestampstr);

			cumulus.LastUpdateTime = DateTime.Parse(timestampstr);
			cumulus.LogMessage("Last update time from today.ini: " + cumulus.LastUpdateTime);

			DateTime currentMonthTS = cumulus.LastUpdateTime.AddHours(cumulus.GetHourInc());

			int defaultyear = currentMonthTS.Year;
			int defaultmonth = currentMonthTS.Month;
			int defaultday = currentMonthTS.Day;

			CurrentYear = ini.GetValue("General", "CurrentYear", defaultyear);
			CurrentMonth = ini.GetValue("General", "CurrentMonth", defaultmonth);
			CurrentDay = ini.GetValue("General", "CurrentDay", defaultday);

			cumulus.LogMessage("Read today file: Date = " + todayfiledate + ", LastUpdateTime = " + cumulus.LastUpdateTime + ", Month = " + CurrentMonth);

			LastRainTip = ini.GetValue("Rain", "LastTip", "0000-00-00 00:00");

			FOSensorClockTime = ini.GetValue("FineOffset", "FOSensorClockTime", DateTime.MinValue);
			FOStationClockTime = ini.GetValue("FineOffset", "FOStationClockTime", DateTime.MinValue);
			if (cumulus.FineOffsetOptions.SyncReads)
			{
				cumulus.LogMessage("Sensor clock  " + FOSensorClockTime.ToLongTimeString());
				cumulus.LogMessage("Station clock " + FOStationClockTime.ToLongTimeString());
			}
			ConsecutiveRainDays = ini.GetValue("Rain", "ConsecutiveRainDays", 0);
			ConsecutiveDryDays = ini.GetValue("Rain", "ConsecutiveDryDays", 0);

			AnnualETTotal = ini.GetValue("ET", "Annual", 0.0);
			StartofdayET = ini.GetValue("ET", "Startofday", -1.0);
			if (StartofdayET < 0)
			{
				cumulus.LogMessage("ET not initialised");
				noET = true;
			}
			else
			{
				ET = AnnualETTotal - StartofdayET;
				cumulus.LogMessage("ET today = " + ET.ToString(cumulus.ETFormat));
			}
			ChillHours = ini.GetValue("Temp", "ChillHours", 0.0);

			// NOAA report names
			cumulus.NOAAconf.LatestMonthReport = ini.GetValue("NOAA", "LatestMonthlyReport", "");
			cumulus.NOAAconf.LatestYearReport = ini.GetValue("NOAA", "LatestYearlyReport", "");

			// Solar
			HiLoToday.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0.0);
			HiLoToday.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			HiLoToday.HighUvTime = ini.GetValue("Solar", "HighUVTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			StartOfDaySunHourCounter = ini.GetValue("Solar", "SunStart", -9999.0);
			RG11RainToday = ini.GetValue("Rain", "RG11Today", 0.0);

			// Wind
			HiLoToday.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			HiLoToday.HighWindTime = ini.GetValue("Wind", "SpTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			HiLoToday.HighGustTime = ini.GetValue("Wind", "Time", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);
			WindRunToday = ini.GetValue("Wind", "Windrun", 0.0);
			DominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			DominantWindBearingMinutes = ini.GetValue("Wind", "DominantWindBearingMinutes", 0);
			DominantWindBearingX = ini.GetValue("Wind", "DominantWindBearingX", 0.0);
			DominantWindBearingY = ini.GetValue("Wind", "DominantWindBearingY", 0.0);
			// Temperature
			HiLoToday.LowTemp = ini.GetValue("Temp", "Low", 999.0);
			HiLoToday.LowTempTime = ini.GetValue("Temp", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighTemp = ini.GetValue("Temp", "High", -999.0);
			HiLoToday.HighTempTime = ini.GetValue("Temp", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
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
			// PressureHighDewpoint
			HiLoToday.LowPress = ini.GetValue("Pressure", "Low", 9999.0);
			HiLoToday.LowPressTime = ini.GetValue("Pressure", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoToday.HighPressTime = ini.GetValue("Pressure", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// rain
			HiLoToday.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			HiLoToday.HighRainRateTime = ini.GetValue("Rain", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoToday.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			raindaystart = ini.GetValue("Rain", "Start", -1.0);
			cumulus.LogMessage($"ReadTodayfile: Rain day start = {raindaystart}");
			RainYesterday = ini.GetValue("Rain", "Yesterday", 0.0);
			if (raindaystart >= 0)
			{
				cumulus.LogMessage("ReadTodayfile: set initialiseRainCounterOnFirstData false");
				initialiseRainCounterOnFirstData = false;
			}
			// humidity
			HiLoToday.LowHumidity = ini.GetValue("Humidity", "Low", 100);
			HiLoToday.HighHumidity = ini.GetValue("Humidity", "High", 0);
			HiLoToday.LowHumidityTime = ini.GetValue("Humidity", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighHumidityTime = ini.GetValue("Humidity", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Solar
			SunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			SunshineToMidnight = ini.GetValue("Solar", "SunshineHoursToMidnight", 0.0);
			// heat index
			HiLoToday.HighHeatIndex = ini.GetValue("HeatIndex", "High", -999.0);
			HiLoToday.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Apparent temp
			HiLoToday.HighAppTemp = ini.GetValue("AppTemp", "High", -999.0);
			HiLoToday.HighAppTempTime = ini.GetValue("AppTemp", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.LowAppTemp = ini.GetValue("AppTemp", "Low", 999.0);
			HiLoToday.LowAppTempTime = ini.GetValue("AppTemp", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// wind chill
			HiLoToday.LowWindChill = ini.GetValue("WindChill", "Low", 999.0);
			HiLoToday.LowWindChillTime = ini.GetValue("WindChill", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Dew point
			HiLoToday.HighDewPoint = ini.GetValue("Dewpoint", "High", -999.0);
			HiLoToday.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.LowDewPoint = ini.GetValue("Dewpoint", "Low", 999.0);
			HiLoToday.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Feels like
			HiLoToday.HighFeelsLike = ini.GetValue("FeelsLike", "High", -999.0);
			HiLoToday.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 999.0);
			HiLoToday.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Humidex
			HiLoToday.HighHumidex = ini.GetValue("Humidex", "High", -999.0);
			HiLoToday.HighHumidexTime = ini.GetValue("Humidex", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));

			// Records
			AlltimeRecordTimestamp = ini.GetValue("Records", "Alltime", DateTime.MinValue);

			// Lightning (GW1000 for now)
			LightningDistance = ini.GetValue("Lightning", "Distance", -1);
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
				ini.SetValue("Rain", "Start", raindaystart);
				ini.SetValue("Rain", "Yesterday", RainYesterday);
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

				// Records
				ini.SetValue("Records", "Alltime", AlltimeRecordTimestamp);

				// Lightning (GW1000 for now)
				ini.SetValue("Lightning", "Distance", LightningDistance);
				ini.SetValue("Lightning", "LastStrike", LightningTime);


				if (Log)
				{
					cumulus.LogMessage("Writing today.ini, LastUpdateTime = " + cumulus.LastUpdateTime + " raindaystart = " + raindaystart.ToString() + " rain counter = " +
									   Raincounter.ToString());

					if (cumulus.FineOffsetStation)
					{
						cumulus.LogMessage("Latest reading: " + LatestFOReading);
					}
					else if (cumulus.StationType == StationTypes.Instromet)
					{
						cumulus.LogMessage("Latest reading: " + cumulus.LatestImetReading);
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
		public double SolarRad { get; set; } = 0;

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

			if (threeHourlyPressureChangeMb > 6) Presstrendstr = cumulus.Risingveryrapidly;
			else if (threeHourlyPressureChangeMb > 3.5) Presstrendstr = cumulus.Risingquickly;
			else if (threeHourlyPressureChangeMb > 1.5) Presstrendstr = cumulus.Rising;
			else if (threeHourlyPressureChangeMb > 0.1) Presstrendstr = cumulus.Risingslowly;
			else if (threeHourlyPressureChangeMb > -0.1) Presstrendstr = cumulus.Steady;
			else if (threeHourlyPressureChangeMb > -1.5) Presstrendstr = cumulus.Fallingslowly;
			else if (threeHourlyPressureChangeMb > -3.5) Presstrendstr = cumulus.Falling;
			else if (threeHourlyPressureChangeMb > -6) Presstrendstr = cumulus.Fallingquickly;
			else
				Presstrendstr = cumulus.Fallingveryrapidly;
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

		public double StationPressure { get; set; }

		public string Forecast { get; set; } = "Forecast: ";

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

		public double midnightraincount { get; set; }

		public int MidnightRainResetDay { get; set; }


		public DateTime lastSpikeRemoval = DateTime.MinValue;
		private double previousPress = 9999;
		public double previousGust = 999;
		private double previousWind = 999;
		private int previousHum = 999;
		private double previousTemp = 999;


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
		/// Soil Temp 1 in C
		/// </summary>
		public double SoilTemp1 { get; set; }

		/// <summary>
		/// Soil Temp 2 in C
		/// </summary>
		public double SoilTemp2 { get; set; }

		/// <summary>
		/// Soil Temp 3 in C
		/// </summary>
		public double SoilTemp3 { get; set; }

		/// <summary>
		/// Soil Temp 4 in C
		/// </summary>
		public double SoilTemp4 { get; set; }
		public double SoilTemp5 { get; set; }
		public double SoilTemp6 { get; set; }
		public double SoilTemp7 { get; set; }
		public double SoilTemp8 { get; set; }
		public double SoilTemp9 { get; set; }
		public double SoilTemp10 { get; set; }
		public double SoilTemp11 { get; set; }
		public double SoilTemp12 { get; set; }
		public double SoilTemp13 { get; set; }
		public double SoilTemp14 { get; set; }
		public double SoilTemp15 { get; set; }
		public double SoilTemp16 { get; set; }

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

		public double LeafTemp1 { get; set; }
		public double LeafTemp2 { get; set; }
		public double LeafTemp3 { get; set; }
		public double LeafTemp4 { get; set; }
		public double LeafTemp5 { get; set; }
		public double LeafTemp6 { get; set; }
		public double LeafTemp7 { get; set; }
		public double LeafTemp8 { get; set; }

		public int LeafWetness1 { get; set; }
		public int LeafWetness2 { get; set; }
		public int LeafWetness3 { get; set; }
		public int LeafWetness4 { get; set; }
		public int LeafWetness5 { get; set; }
		public int LeafWetness6 { get; set; }
		public int LeafWetness7 { get; set; }
		public int LeafWetness8 { get; set; }

		public double SunshineHours { get; set; } = 0;

		public double YestSunshineHours { get; set; } = 0;

		public double SunshineToMidnight { get; set; }

		public double SunHourCounter { get; set; }

		public double StartOfDaySunHourCounter { get; set; }

		public double CurrentSolarMax { get; set; }

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
					// No data coming in, do not do anything else
					return;
				}
			}

			if ((int)timeNow.TimeOfDay.TotalMilliseconds % 2500 <= 500)
			{
				// send current data to web-socket every 3 seconds
				try
				{
					StringBuilder windRoseData = new StringBuilder(80);

					lock (windcounts)
					{
						windRoseData.Append((windcounts[0] * cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));

						for (var i = 1; i < cumulus.NumWindRosePoints; i++)
						{
							windRoseData.Append(",");
							windRoseData.Append((windcounts[i] * cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
						}
					}

					string stormRainStart = StartOfStorm == DateTime.MinValue ? "-----" : StartOfStorm.ToString("d");

					var data = new DataStruct(cumulus, OutdoorTemperature, OutdoorHumidity, TempTotalToday / tempsamplestoday, IndoorTemperature, OutdoorDewpoint, WindChill, IndoorHumidity,
						Pressure, WindLatest, WindAverage, RecentMaxGust, WindRunToday, Bearing, AvgBearing, RainToday, RainYesterday, RainMonth, RainYear, RainRate,
						RainLastHour, HeatIndex, Humidex, ApparentTemperature, temptrendval, presstrendval, HiLoToday.HighGust, HiLoToday.HighGustTime.ToString("HH:mm"), HiLoToday.HighWind,
						HiLoToday.HighGustBearing, cumulus.Units.WindText, BearingRangeFrom10, BearingRangeTo10, windRoseData.ToString(), HiLoToday.HighTemp, HiLoToday.LowTemp,
						HiLoToday.HighTempTime.ToString("HH:mm"), HiLoToday.LowTempTime.ToString("HH:mm"), HiLoToday.HighPress, HiLoToday.LowPress, HiLoToday.HighPressTime.ToString("HH:mm"),
						HiLoToday.LowPressTime.ToString("HH:mm"), HiLoToday.HighRainRate, HiLoToday.HighRainRateTime.ToString("HH:mm"), HiLoToday.HighHumidity, HiLoToday.LowHumidity,
						HiLoToday.HighHumidityTime.ToString("HH:mm"), HiLoToday.LowHumidityTime.ToString("HH:mm"), cumulus.Units.PressText, cumulus.Units.TempText, cumulus.Units.RainText,
						HiLoToday.HighDewPoint, HiLoToday.LowDewPoint, HiLoToday.HighDewPointTime.ToString("HH:mm"), HiLoToday.LowDewPointTime.ToString("HH:mm"), HiLoToday.LowWindChill,
						HiLoToday.LowWindChillTime.ToString("HH:mm"), (int)SolarRad, (int)HiLoToday.HighSolar, HiLoToday.HighSolarTime.ToString("HH:mm"), UV, HiLoToday.HighUv,
						HiLoToday.HighUvTime.ToString("HH:mm"), forecaststr, getTimeString(cumulus.SunRiseTime), getTimeString(cumulus.SunSetTime),
						getTimeString(cumulus.MoonRiseTime), getTimeString(cumulus.MoonSetTime), HiLoToday.HighHeatIndex, HiLoToday.HighHeatIndexTime.ToString("HH:mm"), HiLoToday.HighAppTemp,
						HiLoToday.LowAppTemp, HiLoToday.HighAppTempTime.ToString("HH:mm"), HiLoToday.LowAppTempTime.ToString("HH:mm"), (int)CurrentSolarMax,
						AllTime.HighPress.Val, AllTime.LowPress.Val, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
						HiLoToday.HighHourlyRain, HiLoToday.HighHourlyRainTime.ToString("HH:mm"), "F" + cumulus.Beaufort(HiLoToday.HighWind), "F" + cumulus.Beaufort(WindAverage), cumulus.BeaufortDesc(WindAverage),
						LastDataReadTimestamp.ToString("HH:mm:ss"), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
						cumulus.LowTempAlarm.Triggered, cumulus.HighTempAlarm.Triggered, cumulus.TempChangeAlarm.UpTriggered, cumulus.TempChangeAlarm.DownTriggered, cumulus.HighRainTodayAlarm.Triggered, cumulus.HighRainRateAlarm.Triggered,
						cumulus.LowPressAlarm.Triggered, cumulus.HighPressAlarm.Triggered, cumulus.PressChangeAlarm.UpTriggered, cumulus.PressChangeAlarm.DownTriggered, cumulus.HighGustAlarm.Triggered, cumulus.HighWindAlarm.Triggered,
						cumulus.SensorAlarm.Triggered, cumulus.BatteryLowAlarm.Triggered, cumulus.SpikeAlarm.Triggered, cumulus.UpgradeAlarm.Triggered,
						cumulus.HttpUploadAlarm.Triggered, cumulus.MySqlUploadAlarm.Triggered,
						FeelsLike, HiLoToday.HighFeelsLike, HiLoToday.HighFeelsLikeTime.ToString("HH:mm"), HiLoToday.LowFeelsLike, HiLoToday.LowFeelsLikeTime.ToString("HH:mm"),
						HiLoToday.HighHumidex, HiLoToday.HighHumidexTime.ToString("HH:mm"));

					//var json = jss.Serialize(data);

					var ser = new DataContractJsonSerializer(typeof(DataStruct));

					var stream = new MemoryStream();

					ser.WriteObject(stream, data);

					stream.Position = 0;

					WebSocket.SendMessage(new StreamReader(stream).ReadToEnd());
				}
				catch (Exception ex)
				{
					cumulus.LogMessage(ex.Message);
				}
			}
		}

		private string getTimeString(DateTime time)
		{
			if (time <= DateTime.MinValue)
			{
				return "-----";
			}
			else
			{
				return time.ToString("HH:mm");
			}
		}

		private string getTimeString(TimeSpan timespan)
		{
			try
			{
				if (timespan.TotalSeconds < 0)
				{
					return "-----";
				}

				DateTime dt = DateTime.MinValue.Add(timespan);

				return getTimeString(dt);
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

			RecentDataDb.Execute("delete from RecentData where Timestamp < ?", deleteTime);
		}

		private void ClearAlarms()
		{
			if (cumulus.DataStoppedAlarm.Latch && cumulus.DataStoppedAlarm.Triggered && DateTime.Now > cumulus.DataStoppedAlarm.TriggeredTime.AddHours(cumulus.DataStoppedAlarm.LatchHours))
				cumulus.DataStoppedAlarm.Triggered = false;

			if (cumulus.BatteryLowAlarm.Latch && cumulus.BatteryLowAlarm.Triggered && DateTime.Now > cumulus.BatteryLowAlarm.TriggeredTime.AddHours(cumulus.BatteryLowAlarm.LatchHours))
				cumulus.BatteryLowAlarm.Triggered = false;

			if (cumulus.SensorAlarm.Latch && cumulus.SensorAlarm.Triggered && DateTime.Now > cumulus.SensorAlarm.TriggeredTime.AddHours(cumulus.SensorAlarm.LatchHours))
				cumulus.SensorAlarm.Triggered = false;

			if (cumulus.SpikeAlarm.Latch && cumulus.SpikeAlarm.Triggered && DateTime.Now > cumulus.SpikeAlarm.TriggeredTime.AddHours(cumulus.SpikeAlarm.LatchHours))
				cumulus.SpikeAlarm.Triggered = false;

			if (cumulus.UpgradeAlarm.Latch && cumulus.UpgradeAlarm.Triggered && DateTime.Now > cumulus.UpgradeAlarm.TriggeredTime.AddHours(cumulus.UpgradeAlarm.LatchHours))
				cumulus.UpgradeAlarm.Triggered = false;

			if (cumulus.HttpUploadAlarm.Latch && cumulus.HttpUploadAlarm.Triggered && DateTime.Now > cumulus.HttpUploadAlarm.TriggeredTime.AddHours(cumulus.HttpUploadAlarm.LatchHours))
				cumulus.HttpUploadAlarm.Triggered = false;

			if (cumulus.MySqlUploadAlarm.Latch && cumulus.MySqlUploadAlarm.Triggered && DateTime.Now > cumulus.MySqlUploadAlarm.TriggeredTime.AddHours(cumulus.MySqlUploadAlarm.LatchHours))
				cumulus.MySqlUploadAlarm.Triggered = false;

			if (cumulus.HighWindAlarm.Latch && cumulus.HighWindAlarm.Triggered && DateTime.Now > cumulus.HighWindAlarm.TriggeredTime.AddHours(cumulus.HighWindAlarm.LatchHours))
				cumulus.HighWindAlarm.Triggered = false;

			if (cumulus.HighGustAlarm.Latch && cumulus.HighGustAlarm.Triggered && DateTime.Now > cumulus.HighGustAlarm.TriggeredTime.AddHours(cumulus.HighGustAlarm.LatchHours))
				cumulus.HighGustAlarm.Triggered = false;

			if (cumulus.HighRainRateAlarm.Latch && cumulus.HighRainRateAlarm.Triggered && DateTime.Now > cumulus.HighRainRateAlarm.TriggeredTime.AddHours(cumulus.HighRainRateAlarm.LatchHours))
				cumulus.HighRainRateAlarm.Triggered = false;

			if (cumulus.HighRainTodayAlarm.Latch && cumulus.HighRainTodayAlarm.Triggered && DateTime.Now > cumulus.HighRainTodayAlarm.TriggeredTime.AddHours(cumulus.HighRainTodayAlarm.LatchHours))
				cumulus.HighRainTodayAlarm.Triggered = false;

			if (cumulus.HighPressAlarm.Latch && cumulus.HighPressAlarm.Triggered && DateTime.Now > cumulus.HighPressAlarm.TriggeredTime.AddHours(cumulus.HighPressAlarm.LatchHours))
				cumulus.HighPressAlarm.Triggered = false;

			if (cumulus.LowPressAlarm.Latch && cumulus.LowPressAlarm.Triggered && DateTime.Now > cumulus.LowPressAlarm.TriggeredTime.AddHours(cumulus.LowPressAlarm.LatchHours))
				cumulus.LowPressAlarm.Triggered = false;

			if (cumulus.HighTempAlarm.Latch && cumulus.HighTempAlarm.Triggered && DateTime.Now > cumulus.HighTempAlarm.TriggeredTime.AddHours(cumulus.HighTempAlarm.LatchHours))
				cumulus.HighTempAlarm.Triggered = false;

			if (cumulus.LowTempAlarm.Latch && cumulus.LowTempAlarm.Triggered && DateTime.Now > cumulus.LowTempAlarm.TriggeredTime.AddHours(cumulus.LowTempAlarm.LatchHours))
				cumulus.LowTempAlarm.Triggered = false;

			if (cumulus.TempChangeAlarm.Latch && cumulus.TempChangeAlarm.UpTriggered && DateTime.Now > cumulus.TempChangeAlarm.UpTriggeredTime.AddHours(cumulus.TempChangeAlarm.LatchHours))
				cumulus.TempChangeAlarm.UpTriggered = false;

			if (cumulus.TempChangeAlarm.Latch && cumulus.TempChangeAlarm.DownTriggered && DateTime.Now > cumulus.TempChangeAlarm.DownTriggeredTime.AddHours(cumulus.TempChangeAlarm.LatchHours))
				cumulus.TempChangeAlarm.DownTriggered = false;

			if (cumulus.PressChangeAlarm.Latch && cumulus.PressChangeAlarm.UpTriggered && DateTime.Now > cumulus.PressChangeAlarm.UpTriggeredTime.AddHours(cumulus.PressChangeAlarm.LatchHours))
				cumulus.PressChangeAlarm.UpTriggered = false;

			if (cumulus.PressChangeAlarm.Latch && cumulus.PressChangeAlarm.DownTriggered && DateTime.Now > cumulus.PressChangeAlarm.DownTriggeredTime.AddHours(cumulus.PressChangeAlarm.LatchHours))
				cumulus.PressChangeAlarm.DownTriggered = false;
		}

		private void MinuteChanged(DateTime now)
		{
			CheckForDataStopped();

			if (!DataStopped)
			{
				CurrentSolarMax = AstroLib.SolarMax(now, cumulus.Longitude, cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.RStransfactor, cumulus.BrasTurbidity, cumulus.SolarCalc);
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
					if (cumulus.UseBlakeLarsen)
					{
						ReadBlakeLarsenData();
					}
					else if ((SolarRad > (CurrentSolarMax * cumulus.SunThreshold / 100.0)) && (SolarRad >= cumulus.SolarMinimum))
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

					AddRecentDataWithAq(now, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity,
						Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex, ApparentTemperature, IndoorTemperature, IndoorHumidity, CurrentSolarMax, RainRate);
					DoTrendValues(now);
					DoPressTrend("Pressure trend");

					// calculate ET just before the hour so it is included in the correct day at roll over - only affects 9am met days really
					if (cumulus.StationOptions.CalculatedET && now.Minute == 59)
					{
						CalculateEvaoptranspiration(now);
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

					// Custom HTTP update - minutes interval
					if (cumulus.CustomHttpMinutesEnabled && now.Minute % cumulus.CustomHttpMinutesInterval == 0)
					{
						cumulus.CustomHttpMinutesUpdate();
					}

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
							cumulus.LogMessage("Warning, previous web update is still in progress,second chance, aborting connection");
							if (cumulus.ftpThread.ThreadState == System.Threading.ThreadState.Running)
								cumulus.ftpThread.Abort();
							cumulus.LogMessage("Trying new web update");
							cumulus.WebUpdating = 1;
							cumulus.ftpThread = new Thread(cumulus.DoHTMLFiles);
							cumulus.ftpThread.IsBackground = true;
							cumulus.ftpThread.Start();
						}
						else
						{
							cumulus.WebUpdating = 1;
							cumulus.ftpThread = new Thread(cumulus.DoHTMLFiles);
							cumulus.ftpThread.IsBackground = true;
							cumulus.ftpThread.Start();
						}
					}
					// We also want to kick off DoHTMLFiles if local copy is enabled
					else if (cumulus.FtpOptions.LocalCopyEnabled && cumulus.SynchronisedWebUpdate && (now.Minute % cumulus.UpdateInterval == 0))
					{
						cumulus.ftpThread = new Thread(cumulus.DoHTMLFiles);
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

					if (cumulus.AWEKAS.Enabled && (now.Minute % ((double)cumulus.AWEKAS.Interval / 60) == 0) && cumulus.AWEKAS.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.AWEKAS.ID))
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

					if (cumulus.Twitter.Enabled && (now.Minute % cumulus.Twitter.Interval == 0) && !String.IsNullOrWhiteSpace(cumulus.Twitter.ID) && !String.IsNullOrWhiteSpace(cumulus.Twitter.PW))
					{
						cumulus.UpdateTwitter();
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
							xapReport.Append($"WindM={ConvertUserWindToMPH(WindAverage):F1}\n");
							xapReport.Append($"WindK={ConvertUserWindToKPH(WindAverage):F1}\n");
							xapReport.Append($"WindGustsM={ConvertUserWindToMPH(RecentMaxGust):F1}\n");
							xapReport.Append($"WindGustsK={ConvertUserWindToKPH(RecentMaxGust):F1}\n");
							xapReport.Append($"WindDirD={Bearing}\n");
							xapReport.Append($"WindDirC={AvgBearing}\n");
							xapReport.Append($"TempC={ConvertUserTempToC(OutdoorTemperature):F1}\n");
							xapReport.Append($"TempF={ConvertUserTempToF(OutdoorTemperature):F1}\n");
							xapReport.Append($"DewC={ConvertUserTempToC(OutdoorDewpoint):F1}\n");
							xapReport.Append($"DewF={ConvertUserTempToF(OutdoorDewpoint):F1}\n");
							xapReport.Append($"AirPressure={ConvertUserPressToMB(Pressure):F1}\n");
							xapReport.Append($"Rain={ConvertUserRainToMM(RainToday):F1}\n");
							xapReport.Append("}");

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
				}
				else
				{
					cumulus.LogMessage("Minimum data set of pressure, temperature, and wind is not available and NoSensorCheck is not enabled. Skip processing");
				}
			}

			// Check for a new version of Cumulus once a day
			if (now.Minute == versionCheckTime.Minute && now.Hour == versionCheckTime.Hour)
			{
				cumulus.LogMessage("Checking for latest Cumulus MX version...");
				cumulus.GetLatestVersion();
			}

			// If not on windows, check for CPU temp
			if (Type.GetType("Mono.Runtime") != null && File.Exists("/sys/class/thermal/thermal_zone0/temp"))
			{
				try
				{
					var raw = File.ReadAllText(@"/sys/class/thermal/thermal_zone0/temp");
					cumulus.CPUtemp = ConvertTempCToUser(double.Parse(raw) / 1000);
					cumulus.LogDebugMessage($"Current CPU temp = {cumulus.CPUtemp.ToString(cumulus.TempFormat)}{cumulus.Units.TempText}");
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"Error reading CPU temperature - {ex.Message}");
				}
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


			if (DataStopped)
			{
				// No data coming in, do not do anything else
				return;
			}


			if (now.Hour == 0)
			{
				ResetMidnightRain(now);
				//RecalcSolarFactor(now);
			}

			int rollHour = Math.Abs(cumulus.GetHourInc());

			if (now.Hour == rollHour)
			{
				DayReset(now);
			}

			if (now.Hour == 0)
			{
				ResetSunshineHours();
			}

			RemoveOldRecentData(now);
		}

		private void CheckForDataStopped()
		{
			// Check whether we have read data since the last clock minute.
			if ((LastDataReadTimestamp != DateTime.MinValue) && (LastDataReadTimestamp == SavedLastDataReadTimestamp) && (LastDataReadTimestamp < DateTime.Now))
			{
				// Data input appears to have has stopped
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
					cumulus.LogMessage("*** Data input appears to have stopped");
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
						cumulus.LogMessage("Error reading SRsunshine.dat: " + ex.Message);
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

		private long DateTimeToUnix(DateTime timestamp)
		{
			var timeSpan = (timestamp - new DateTime(1970, 1, 1, 0, 0, 0));
			return (long)timeSpan.TotalSeconds;
		}

		/*
		private long DateTimeToJS(DateTime timestamp)
		{
			return DateTimeToUnix(timestamp) * 1000;
		}
		*/


		public void CalculateEvaoptranspiration(DateTime date)
		{
			cumulus.LogDebugMessage("Calculating ET from data");

			var dateFrom = date.AddHours(-1);

			// get the min and max temps, humidity, pressure, and mean solar rad and wind speed for the last hour
			var result = RecentDataDb.Query<EtData>("select max(OutsideTemp) maxTemp, min(OutsideTemp) minTemp, max(Humidity) maxHum, min(Humidity) minHum, max(Pressure) maxPress, min(Pressure) minPress, avg(SolarRad) avgSol, avg(WindSpeed) avgWind from RecentData where Timestamp >= ? order by Timestamp", dateFrom);

			// finally calculate the ETo
			var newET = MeteoLib.Evapotranspiration(
				ConvertUserTempToC(result[0].minTemp),
				ConvertUserTempToC(result[0].maxTemp),
				result[0].minHum,
				result[0].maxHum,
				result[0].avgSol,
				ConvertUserWindToMS(result[0].avgWind),
				cumulus.Latitude,
				cumulus.Longitude,
				AltitudeM(cumulus.Altitude),
				date
			);

			// convert to user units
			newET = ConvertRainMMToUser(newET);
			cumulus.LogDebugMessage($"Calculated ET for the last hour = {newET:F3}");

			// DoET expects the running annual total to be sent
			DoET(AnnualETTotal + newET, date);
		}

		public void CreateGraphDataFiles()
		{
			// Chart data for Highcharts graphs
			string json = "";
			for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
			{
				// We double up the meaning of .FtpRequired to creation as well.
				// The FtpRequired flag is only cleared for the config files that are pretty static so it is pointless
				// recreating them every update too.
				if (cumulus.GraphDataFiles[i].Create && cumulus.GraphDataFiles[i].CreateRequired)
				{
					switch (cumulus.GraphDataFiles[i].LocalFileName)
					{
						case "graphconfig.json":
							json = GetGraphConfig();
							break;
						case "availabledata.json":
							json = GetAvailGraphData();
							break;
						case "tempdata.json":
							json = GetTempGraphData();
							break;
						case "pressdata.json":
							json = GetPressGraphData();
							break;
						case "winddata.json":
							json = GetWindGraphData();
							break;
						case "wdirdata.json":
							json = GetWindDirGraphData();
							break;
						case "humdata.json":
							json = GetHumGraphData();
							break;
						case "raindata.json":
							json = GetRainGraphData();
							break;
						case "dailyrain.json":
							json = GetDailyRainGraphData();
							break;
						case "dailytemp.json":
							json = GetDailyTempGraphData();
							break;
						case "solardata.json":
							json = GetSolarGraphData();
							break;
						case "sunhours.json":
							json = GetSunHoursGraphData();
							break;
						case "airquality.json":
							json = GetAqGraphData();
							break;
					}

					try
					{
						var dest = cumulus.GraphDataFiles[i].LocalPath + cumulus.GraphDataFiles[i].LocalFileName;
						using (var file = new StreamWriter(dest, false))
						{
							file.WriteLine(json);
							file.Close();
						}

						// The config files only need creating once per change
						if (cumulus.GraphDataFiles[i].LocalFileName == "availabledata.json" || cumulus.GraphDataFiles[i].LocalFileName == "graphconfig.json")
						{
							cumulus.GraphDataFiles[i].CreateRequired = false;
						}
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"Error writing {cumulus.GraphDataFiles[i].LocalFileName}: {ex}");
					}
				}
			}
		}

		public void CreateEodGraphDataFiles()
		{
			string json = "";
			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				if (cumulus.GraphDataEodFiles[i].Create)
				{
					switch (cumulus.GraphDataEodFiles[i].LocalFileName)
					{
						case "alldailytempdata.json":
							json = GetAllDailyTempGraphData();
							break;
						case "alldailypressdata.json":
							json = GetAllDailyPressGraphData();
							break;
						case "alldailywinddata.json":
							json = GetAllDailyWindGraphData();
							break;
						case "alldailyhumdata.json":
							json = GetAllDailyHumGraphData();
							break;
						case "alldailyraindata.json":
							json = GetAllDailyRainGraphData();
							break;
						case "alldailysolardata.json":
							json = GetAllDailySolarGraphData();
							break;
						case "alldailydegdaydata.json":
							json = GetAllDegreeDaysGraphData();
							break;
						case "alltempsumdata.json":
							json = GetAllTempSumGraphData();
							break;
					}

					try
					{
						var dest = cumulus.GraphDataEodFiles[i].LocalPath + cumulus.GraphDataEodFiles[i].LocalFileName;
						using (var file = new StreamWriter(dest, false))
						{
							file.WriteLine(json);
							file.Close();
						}
						// Now set the flag that upload is required (if enabled)
						cumulus.GraphDataEodFiles[i].FtpRequired = true;
						cumulus.GraphDataEodFiles[i].CopyRequired = true;
					}
					catch (Exception ex)
					{
						cumulus.LogMessage($"Error writing {cumulus.GraphDataEodFiles[i].LocalFileName}: {ex}");
					}
				}
			}
		}

		public string GetSolarGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				if (cumulus.GraphOptions.UVVisible)
				{
					sbUv.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].UV.ToString(cumulus.UVFormat, InvC)}],");
				}

				if (cumulus.GraphOptions.SolarVisible)
				{
					sbSol.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{(int)data[i].SolarRad}],");

					sbMax.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{(int)data[i].SolarMax}],");
				}
			}


			if (cumulus.GraphOptions.UVVisible)
			{
				if (sbUv[sbUv.Length - 1] == ',')
					sbUv.Length--;

				sbUv.Append("]");
				sb.Append(sbUv);
			}
			if (cumulus.GraphOptions.SolarVisible)
			{
				if (sbSol[sbSol.Length - 1] == ',')
				{
					sbSol.Length--;
					sbMax.Length--;
				}

				sbSol.Append("]");
				sbMax.Append("]");
				if (cumulus.GraphOptions.UVVisible)
				{
					sb.Append(",");
				}
				sb.Append(sbSol);
				sb.Append(",");
				sb.Append(sbMax);
			}

			sb.Append("}");
			return sb.ToString();
		}


		public string GetRainGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				sbRain.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].RainToday.ToString(cumulus.RainFormat, InvC)}],");

				sbRate.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].RainRate.ToString(cumulus.RainFormat, InvC)}],");
			}

			if (sbRain[sbRain.Length-1] == ',')
			{
				sbRain.Length--;
				sbRate.Length--;
			}
			sbRain.Append("],");
			sbRate.Append("]");
			sb.Append(sbRain);
			sb.Append(sbRate);
			sb.Append("}");
			return sb.ToString();
		}

		public string GetHumGraphData()
		{
			var sb = new StringBuilder("{", 10240);
			var sbOut = new StringBuilder("\"hum\":[");
			var sbIn = new StringBuilder("\"inhum\":[");

			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				if (cumulus.GraphOptions.OutHumVisible)
				{
					sbOut.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].Humidity}],");
				}
				if (cumulus.GraphOptions.InHumVisible)
				{
					sbIn.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].IndoorHumidity}],");
				}
			}

			if (cumulus.GraphOptions.OutHumVisible)
			{
				if (sbOut[sbOut.Length - 1] == ',')
					sbOut.Length--;

				sbOut.Append("]");

				sb.Append(sbOut);
			}

			if (cumulus.GraphOptions.InHumVisible)
			{
				if (sbIn[sbIn.Length - 1] == ',')
					sbIn.Length--;

				sbIn.Append("]");

				if (cumulus.GraphOptions.OutHumVisible)
					sb.Append(",");

				sb.Append(sbIn);
			}

			sb.Append("}");
			return sb.ToString();
		}

		public string GetWindDirGraphData()
		{
			var sb = new StringBuilder("{\"bearing\":[");
			var sbAvg = new StringBuilder("\"avgbearing\":[");
			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].WindDir}],");

				sbAvg.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].WindAvgDir}],");
			}

			if (sb[sb.Length - 1] == ',')
			{
				sb.Length--;
				sbAvg.Length--;
				sbAvg.Append("]");
			}

			sb.Append("],");
			sb.Append(sbAvg);
			sb.Append("}");
			return sb.ToString();
		}

		public string GetWindGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");
			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].WindGust.ToString(cumulus.WindFormat, InvC)}],");

				sbSpd.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].WindSpeed.ToString(cumulus.WindAvgFormat, InvC)}],");
			}

			if (sb[sb.Length - 1] == ',')
			{
				sb.Length--;
				sbSpd.Length--;
				sbSpd.Append("]");
			}

			sb.Append("],");
			sb.Append(sbSpd);
			sb.Append("}");
			return sb.ToString();
		}

		public string GetPressGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"press\":[");
			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].Pressure.ToString(cumulus.PressFormat, InvC)}],");
			}

			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetTempGraphData()
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
			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

			for (var i = 0; i < data.Count; i++)
			{
				if (cumulus.GraphOptions.InTempVisible)
					sbIn.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].IndoorTemp.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.DPVisible)
					sbDew.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].DewPoint.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.AppTempVisible)
					sbApp.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].AppTemp.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.FeelsLikeVisible)
					sbFeel.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].FeelsLike.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.WCVisible)
					sbChill.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].WindChill.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.HIVisible)
					sbHeat.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].HeatIndex.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.TempVisible)
					sbTemp.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].OutsideTemp.ToString(cumulus.TempFormat, InvC)}],");

				if (cumulus.GraphOptions.HumidexVisible)
					sbHumidex.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{data[i].Humidex.ToString(cumulus.TempFormat, InvC)}],");
			}

			if (cumulus.GraphOptions.InTempVisible)
			{
				if (sbIn[sbIn.Length - 1] == ',')
					sbIn.Length--;

				sbIn.Append("]");
				sb.Append(sbIn);
				append = true;
			}

			if (cumulus.GraphOptions.DPVisible)
			{
				if (sbDew[sbDew.Length - 1] == ',')
					sbDew.Length--;

				sbDew.Append("]");
				sb.Append((append ? "," : "") + sbDew);
				append = true;
			}

			if (cumulus.GraphOptions.AppTempVisible)
			{
				if (sbApp[sbApp.Length - 1] == ',')
					sbApp.Length--;

				sbApp.Append("]");
				sb.Append((append ? "," : "") + sbApp);
				append = true;
			}

			if (cumulus.GraphOptions.FeelsLikeVisible)
			{
				if (sbFeel[sbFeel.Length - 1] == ',')
					sbFeel.Length--;

				sbFeel.Append("]");
				sb.Append((append ? "," : "") + sbFeel);
				append = true;
			}

			if (cumulus.GraphOptions.WCVisible)
			{
				if (sbChill[sbChill.Length - 1] == ',')
					sbChill.Length--;

				sbChill.Append("]");
				sb.Append((append ? "," : "") + sbChill);
				append = true;
			}

			if (cumulus.GraphOptions.HIVisible)
			{
				if (sbHeat[sbHeat.Length - 1] == ',')
					sbHeat.Length--;

				sbHeat.Append("]");
				sb.Append((append ? "," : "") + sbHeat);
				append = true;
			}

			if (cumulus.GraphOptions.TempVisible)
			{
				if (sbTemp[sbTemp.Length - 1] == ',')
					sbTemp.Length--;

				sbTemp.Append("]");
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.HumidexVisible)
			{
				if (sbHumidex[sbHumidex.Length - 1] == ',')
					sbHumidex.Length--;

				sbHumidex.Append("]");
				sb.Append((append ? "," : "") + sbHumidex);
			}

			sb.Append("}");
			return sb.ToString();
		}

		public string GetAqGraphData()
		{
			bool append = false;
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var sb2p5 = new StringBuilder("\"pm2p5\":[");
			var sb10 = new StringBuilder(",\"pm10\":[");
			var dataFrom = DateTime.Now.AddHours(-cumulus.GraphHours);


			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (cumulus.StationOptions.PrimaryAqSensor > (int)Cumulus.PrimaryAqSensor.Undefined
				&& cumulus.StationOptions.PrimaryAqSensor != (int)Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=?", dataFrom);

				for (var i = 0; i < data.Count; i++)
				{
					var val = data[i].Pm2p5 == -1 ? "null" : data[i].Pm2p5.ToString("F1", InvC);
					sb2p5.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{val}],");

					// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
					if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
						cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkIndoor ||
						cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.EcowittCO2)
					{
						append = true;
						val = data[i].Pm10 == -1 ? "null" : data[i].Pm10.ToString("F1", InvC);
						sb10.Append($"[{DateTimeToUnix(data[i].Timestamp) * 1000},{val}],");
					}
				}

				if (sb2p5[sb2p5.Length - 1] == ',')
					sb2p5.Length--;

				sb2p5.Append("]");
				sb.Append(sb2p5);

				if (append)
				{
					if (sb10[sb10.Length - 1] == ',')
						sb10.Length--;

					sb10.Append("]");
					sb.Append(sb10);
				}

			}

			sb.Append("}");
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
					SolarRad = (int)solarRad,
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
					SolarMax = (int)solarMax,
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

		private void CreateWxnowFile()
		{
			// Jun 01 2003 08:07
			// 272/000g006t069r010p030P020h61b10150

			// 272 - wind direction - 272 degrees
			// 010 - wind speed - 10 mph

			// g015 - wind gust - 15 mph
			// t069 - temperature - 69 degrees F
			// r010 - rain in last hour in hundredths of an inch - 0.1 inches
			// p030 - rain in last 24 hours in hundredths of an inch - 0.3 inches
			// P020 - rain since midnight in hundredths of an inch - 0.2 inches
			// h61 - humidity 61% (00 = 100%)
			// b10153 - barometric pressure in tenths of a millibar - 1015.3 millibars

			var filename = cumulus.AppDir + cumulus.WxnowFile;
			var timestamp = DateTime.Now.ToString(@"MMM dd yyyy HH\:mm");

			int mphwind = Convert.ToInt32(ConvertUserWindToMPH(WindAverage));
			int mphgust = Convert.ToInt32(ConvertUserWindToMPH(RecentMaxGust));
			// ftemp = trunc(TempF(OutsideTemp));
			string ftempstr = APRStemp(OutdoorTemperature);
			int in100rainlasthour = Convert.ToInt32(ConvertUserRainToIn(RainLastHour) * 100);
			int in100rainlast24hours = Convert.ToInt32(ConvertUserRainToIn(RainLast24Hour) * 100);
			int in100raintoday;
			if (cumulus.RolloverHour == 0)
				// use today's rain for safety
				in100raintoday = Convert.ToInt32(ConvertUserRainToIn(RainToday) * 100);
			else
				// 0900 day, use midnight calculation
				in100raintoday = Convert.ToInt32(ConvertUserRainToIn(RainSinceMidnight) * 100);
			int mb10press = Convert.ToInt32(ConvertUserPressToMB(AltimeterPressure) * 10);
			// For 100% humidity, send zero. For zero humidity, send 1
			int hum;
			if (OutdoorHumidity == 0)
				hum = 1;
			else if (OutdoorHumidity == 100)
				hum = 0;
			else
				hum = OutdoorHumidity;

			string data = String.Format("{0:000}/{1:000}g{2:000}t{3}r{4:000}p{5:000}P{6:000}h{7:00}b{8:00000}", AvgBearing, mphwind, mphgust, ftempstr, in100rainlasthour,
				in100rainlast24hours, in100raintoday, hum, mb10press);

			if (cumulus.APRS.SendSolar)
			{
				data += APRSsolarradStr(SolarRad);
			}

			using (StreamWriter file = new StreamWriter(filename, false))
			{
				file.WriteLine(timestamp);
				file.WriteLine(data);
				file.Close();
			}
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

		private double ConvertUserRainToIn(double value)
		{
			if (cumulus.Units.Rain == 1)
			{
				return value;
			}
			else
			{
				return value / 25.4;
			}
		}

		public double ConvertUserWindToMPH(double value)
		{
			switch (cumulus.Units.Wind)
			{
				case 0:
					return value * 2.23693629;
				case 1:
					return value;
				case 2:
					return value * 0.621371;
				case 3:
					return value * 1.15077945;
				default:
					return 0;
			}
		}

		public double ConvertUserWindToKnots(double value)
		{
			switch (cumulus.Units.Wind)
			{
				case 0:
					return value * 1.943844;
				case 1:
					return value * 0.8689758;
				case 2:
					return value * 0.5399565;
				case 3:
					return value;
				default:
					return 0;
			}
		}


		public void ResetSunshineHours() // called at midnight irrespective of roll-over time
		{
			YestSunshineHours = SunshineHours;

			cumulus.LogMessage("Reset sunshine hours, yesterday = " + YestSunshineHours);

			SunshineToMidnight = SunshineHours;
			SunshineHours = 0;
			StartOfDaySunHourCounter = SunHourCounter;
			WriteYesterdayFile();
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
			cumulus.CurrentActivity = "Normal running";

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
				midnightraincount = Raincounter;
				RainSinceMidnight = 0;
				MidnightRainResetDay = mrrday;
				cumulus.LogMessage("Midnight rain reset, count = " + Raincounter + " time = " + timestamp.ToShortTimeString());
				if ((mrrday == 1) && (mrrmonth == 1) && (cumulus.StationType == StationTypes.VantagePro))
				{
					// special case: rain counter is about to be reset
					cumulus.LogMessage("Special case, Davis station on 1st Jan. Set midnight rain count to zero");
					midnightraincount = 0;
				}
			}
		}

		public void DoIndoorHumidity(int hum)
		{
			IndoorHumidity = hum;
			HaveReadData = true;
		}

		public void DoIndoorTemp(double temp)
		{
			IndoorTemperature = temp + cumulus.Calib.InTemp.Offset;
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
				cumulus.SpikeAlarm.LastError = $"Humidity difference greater than spike value - NewVal={humpar} OldVal={previousHum}";
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
				OutdoorHumidity = humpar;
			}

			// apply offset and multipliers and round. This is different to C1, which truncates. I'm not sure why C1 does that
			OutdoorHumidity = (int)Math.Round((OutdoorHumidity * OutdoorHumidity * cumulus.Calib.Hum.Mult2) + (OutdoorHumidity * cumulus.Calib.Hum.Mult) + cumulus.Calib.Hum.Offset);

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

		public double CalibrateTemp(double temp)
		{
			return (temp * temp * cumulus.Calib.Temp.Mult2) + (temp * cumulus.Calib.Temp.Mult) + cumulus.Calib.Temp.Offset;
		}

		public void DoOutdoorTemp(double temp, DateTime timestamp)
		{
			// Spike removal is in Celsius
			var tempC = ConvertUserTempToC(temp);
			if (((Math.Abs(tempC - previousTemp) > cumulus.Spike.TempDiff) && (previousTemp != 999)) ||
				tempC >= cumulus.Limit.TempHigh || tempC <= cumulus.Limit.TempLow)
			{
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastError = $"Temp difference greater than spike value - NewVal={tempC.ToString(cumulus.TempFormat)} OldVal={previousTemp.ToString(cumulus.TempFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				cumulus.LogSpikeRemoval("Temp difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={tempC.ToString(cumulus.TempFormat)} OldVal={previousTemp.ToString(cumulus.TempFormat)} SpikeTempDiff={cumulus.Spike.TempDiff.ToString(cumulus.TempFormat)} HighLimit={cumulus.Limit.TempHigh.ToString(cumulus.TempFormat)} LowLimit={cumulus.Limit.TempLow.ToString(cumulus.TempFormat)}");
				return;
			}
			previousTemp = tempC;

			// UpdateStatusPanel;
			// update global temp
			OutdoorTemperature = CalibrateTemp(temp);

			double tempinF = ConvertUserTempToF(OutdoorTemperature);
			double tempinC = ConvertUserTempToC(OutdoorTemperature);

			first_temp = false;

			// Does this reading set any records or trigger any alarms?
			if (OutdoorTemperature > AllTime.HighTemp.Val)
				SetAlltime(AllTime.HighTemp, OutdoorTemperature, timestamp);

			cumulus.HighTempAlarm.Triggered = DoAlarm(OutdoorTemperature, cumulus.HighTempAlarm.Value, cumulus.HighTempAlarm.Enabled, true);

			if (OutdoorTemperature < AllTime.LowTemp.Val)
				SetAlltime(AllTime.LowTemp, OutdoorTemperature, timestamp);

			cumulus.LowTempAlarm.Triggered = DoAlarm(OutdoorTemperature, cumulus.LowTempAlarm.Value, cumulus.LowTempAlarm.Enabled, false);

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
				// dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
				OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(tempinC, OutdoorHumidity));

				CheckForDewpointHighLow(timestamp);
			}

			// Calculate cloud base
			if (cumulus.CloudBaseInFeet)
			{
				CloudBase = (int)Math.Floor(((tempinF - ConvertUserTempToF(OutdoorDewpoint)) / 4.4) * 1000);
				if (CloudBase < 0)
					CloudBase = 0;
			}
			else
			{
				CloudBase = (int)Math.Floor((((tempinF - ConvertUserTempToF(OutdoorDewpoint)) / 4.4) * 1000) / 3.2808399);
				if (CloudBase < 0)
					CloudBase = 0;
			}

			HeatIndex = ConvertTempCToUser(MeteoLib.HeatIndex(tempinC, OutdoorHumidity));

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

			//DoApparentTemp(timestamp);

			// Find estimated wet bulb temp. First time this is called, required variables may not have been set up yet
			try
			{
				WetBulb = ConvertTempCToUser(MeteoLib.CalculateWetBulbC(tempinC, ConvertUserTempToC(OutdoorDewpoint), ConvertUserPressToMB(Pressure)));
			}
			catch
			{
				WetBulb = OutdoorTemperature;
			}

			TempReadyToPlot = true;
			HaveReadData = true;
		}

		public void DoApparentTemp(DateTime timestamp)
		{
			// Calculates Apparent Temperature
			// See http://www.bom.gov.au/info/thermal_stress/#atapproximation

			// don't try to calculate apparent if we haven't yet had wind and temp readings
			//if (TempReadyToPlot && WindReadyToPlot)
			//{
			//ApparentTemperature =
			//ConvertTempCToUser(ConvertUserTempToC(OutdoorTemperature) + (0.33 * MeteoLib.ActualVapourPressure(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity)) -
			//				   (0.7 * ConvertUserWindToMS(WindAverage)) - 4);
			ApparentTemperature = ConvertTempCToUser(MeteoLib.ApparentTemperature(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToMS(WindAverage), OutdoorHumidity));


			// we will tag on the THW Index here
			THWIndex = ConvertTempCToUser(MeteoLib.THWIndex(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity, ConvertUserWindToKPH(WindAverage)));

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

			if (cumulus.StationOptions.CalculatedWC)
			{
				// don"t try to calculate wind chill if we haven"t yet had wind and temp readings
				if (TempReadyToPlot && WindReadyToPlot)
				{
					double TempinC = ConvertUserTempToC(OutdoorTemperature);
					double windinKPH = ConvertUserWindToKPH(WindAverage);
					WindChill = ConvertTempCToUser(MeteoLib.WindChill(TempinC, windinKPH));
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

				if (WindChill < MonthlyRecs[timestamp.Month].LowChill.Val)
				{
					SetMonthlyAlltime(MonthlyRecs[timestamp.Month].LowChill, WindChill, timestamp);
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
			FeelsLike = ConvertTempCToUser(MeteoLib.FeelsLike(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage), OutdoorHumidity));

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
			Humidex = MeteoLib.Humidex(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity);

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
			// Spike removal is in mb/hPa
			var pressMB = ConvertUserPressToMB(sl);
			if (((Math.Abs(pressMB - previousPress) > cumulus.Spike.PressDiff) && (previousPress != 9999)) ||
				pressMB >= cumulus.Limit.PressHigh || pressMB <= cumulus.Limit.PressLow)
			{
				cumulus.LogSpikeRemoval("Pressure difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={pressMB:F1} OldVal={previousPress:F1} SpikePressDiff={cumulus.Spike.PressDiff:F1} HighLimit={cumulus.Limit.PressHigh:F1} LowLimit={cumulus.Limit.PressLow:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastError = $"Pressure difference greater than spike value - NewVal={pressMB:F1} OldVal={previousPress:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousPress = pressMB;

			Pressure = sl * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
			if (cumulus.Manufacturer == cumulus.DAVIS)
			{
				if (!cumulus.DavisOptions.UseLoop2)
				{
					// Loop2 data not available, just use sea level (for now, anyway)
					AltimeterPressure = Pressure;
				}
			}
			else
			{
				if (cumulus.Manufacturer == cumulus.OREGONUSB)
				{
					AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(ConvertUserPressureToHPa(StationPressure), AltitudeM(cumulus.Altitude)));
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

			cumulus.HighPressAlarm.Triggered = DoAlarm(Pressure, cumulus.HighPressAlarm.Value, cumulus.HighPressAlarm.Enabled, true);

			if (Pressure < AllTime.LowPress.Val)
			{
				SetAlltime(AllTime.LowPress, Pressure, timestamp);
			}

			cumulus.LowPressAlarm.Triggered = DoAlarm(Pressure, cumulus.LowPressAlarm.Value, cumulus.LowPressAlarm.Enabled, false);
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

			// Spike removal is in mm
			var rainRateMM = ConvertUserRainToMM(rate);
			if (rainRateMM > cumulus.Spike.MaxRainRate)
			{
				cumulus.LogSpikeRemoval("Rain rate greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Rate value = {rainRateMM:F2} SpikeMaxRainRate = {cumulus.Spike.MaxRainRate:F2}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastError = $"Rain rate greater than spike value - value = {rainRateMM:F2}mm/hr";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			if ((CurrentDay != readingTS.Day) || (CurrentMonth != readingTS.Month) || (CurrentYear != readingTS.Year))
			{
				// A reading has apparently arrived at the start of a new day, but before we have done the roll-over
				// Ignore it, as otherwise it may cause a new monthly record to be logged using last month's total
				return;
			}

			var previoustotal = Raincounter;

			// This is just to stop rounding errors triggering phantom rain days
			double raintipthreshold = cumulus.Units.Rain == 0 ? 0.009 : 0.0003;

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

			Raincounter = total;

			//first_rain = false;
			if (initialiseRainCounterOnFirstData)
			{
				raindaystart = Raincounter;
				midnightraincount = Raincounter;
				cumulus.LogMessage(" First rain data, raindaystart = " + raindaystart);

				initialiseRainCounterOnFirstData = false;
				WriteTodayFile(timestamp, false);
				HaveReadData = true;
				return;
			}

			// Has the rain total in the station been reset?
			// raindaystart greater than current total, allow for rounding
			if (raindaystart - Raincounter > 0.1)
			{
				if (FirstChanceRainReset)
				// second consecutive reading with reset value
				{
					cumulus.LogMessage(" ****Rain counter reset confirmed: raindaystart = " + raindaystart + ", Raincounter = " + Raincounter);

					// set the start of day figure so it reflects the rain
					// so far today
					raindaystart = Raincounter - (RainToday / cumulus.Calib.Rain.Mult);
					cumulus.LogMessage("Setting raindaystart to " + raindaystart);

					midnightraincount = Raincounter;

					// update any data in the recent data db
					var counterChange = Raincounter - prevraincounter;
					RecentDataDb.Execute("update RecentData set raincounter=raincounter+?", counterChange);

					FirstChanceRainReset = false;
				}
				else
				{
					cumulus.LogMessage(" ****Rain reset? First chance: raindaystart = " + raindaystart + ", Raincounter = " + Raincounter);

					// reset the counter to ignore this reading
					Raincounter = previoustotal;
					cumulus.LogMessage("Leaving counter at " + Raincounter);

					// stash the previous rain counter
					prevraincounter = Raincounter;

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

				if (RainRate > AllTime.HighRainRate.Val)
					SetAlltime(AllTime.HighRainRate, RainRate, timestamp);

				CheckMonthlyAlltime("HighRainRate", RainRate, true, timestamp);

				cumulus.HighRainRateAlarm.Triggered = DoAlarm(RainRate, cumulus.HighRainRateAlarm.Value, cumulus.HighRainRateAlarm.Enabled, true);

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
				if (total - previoustotal > raintipthreshold)
				{
					// rain has occurred
					LastRainTip = timestamp.ToString("yyyy-MM-dd HH:mm");
				}

				// Calculate today"s rainfall
				RainToday = Raincounter - raindaystart;
				//cumulus.LogDebugMessage("Uncalibrated RainToday = " + RainToday);

				// scale for calibration
				RainToday *= cumulus.Calib.Rain.Mult;

				// Calculate rain since midnight for Wunderground etc
				double trendval = Raincounter - midnightraincount;

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
				RainMonth = rainthismonth + RainToday;

				// get correct date for rain records
				var offsetdate = timestamp.AddHours(cumulus.GetHourInc());

				// rain this year so far
				RainYear = rainthisyear + RainToday;

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

				cumulus.HighRainTodayAlarm.Triggered = DoAlarm(RainToday, cumulus.HighRainTodayAlarm.Value, cumulus.HighRainTodayAlarm.Enabled, true);

				// Yesterday"s rain - Scale for units
				// rainyest = rainyesterday * RainMult;

				//RainReadyToPlot = true;
			}
			HaveReadData = true;
		}

		public void DoOutdoorDewpoint(double dp, DateTime timestamp)
		{
			if (!cumulus.StationOptions.CalculatedDP)
			{
				if (ConvertUserTempToC(dp) <= cumulus.Limit.DewHigh)
				{
					OutdoorDewpoint = dp;
					CheckForDewpointHighLow(timestamp);
				}
				else
				{
					var msg = $"Dew point greater than limit ({cumulus.Limit.DewHigh.ToString(cumulus.TempFormat)}); reading ignored: {dp.ToString(cumulus.TempFormat)}";
					lastSpikeRemoval = DateTime.Now;
					cumulus.SpikeAlarm.LastError = msg;
					cumulus.SpikeAlarm.Triggered = true;
					cumulus.LogSpikeRemoval(msg);
				}
			}
		}

		public string LastRainTip { get; set; }

		public void DoExtraHum(double hum, int channel)
		{
			if ((channel > 0) && (channel < ExtraHum.Length - 1))
			{
				ExtraHum[channel] = (int)hum;
			}
		}

		public void DoExtraTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < ExtraTemp.Length - 1))
			{
				ExtraTemp[channel] = temp;
			}
		}

		public void DoUserTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < UserTemp.Length - 1))
			{
				UserTemp[channel] = temp;
			}
		}


		public void DoExtraDP(double dp, int channel)
		{
			if ((channel > 0) && (channel < ExtraDewPoint.Length - 1))
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

				CumulusForecast = BetelCast(ConvertUserPressureToHPa(Pressure), DateTime.Now.Month, windDir, bartrend, cumulus.Latitude > 0, hp, lp);

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

		/// <summary>
		/// Convert pressure from user units to hPa
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public double ConvertUserPressureToHPa(double value)
		{
			if (cumulus.Units.Press == 2)
				return value / 0.0295333727;
			else
				return value;
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

		public bool DoAlarm(double value, double threshold, bool enabled, bool testAbove)
		{
			if (enabled)
			{
				if (testAbove)
				{
					return value > threshold;
				}
				else
				{
					return value < threshold;
				}
			}
			return false;
		}

		public void DoWind(double gustpar, int bearingpar, double speedpar, DateTime timestamp)
		{
			// Spike removal is in m/s
			var windGustMS = ConvertUserWindToMS(gustpar);
			var windAvgMS = ConvertUserWindToMS(speedpar);

			if (((Math.Abs(windGustMS - previousGust) > cumulus.Spike.GustDiff) && (previousGust != 999)) ||
				((Math.Abs(windAvgMS - previousWind) > cumulus.Spike.WindDiff) && (previousWind != 999)) ||
				windGustMS >= cumulus.Limit.WindHigh
				)
			{
				cumulus.LogSpikeRemoval("Wind or gust difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={windGustMS:F1} OldVal={previousGust:F1} SpikeGustDiff={cumulus.Spike.GustDiff:F1} HighLimit={cumulus.Limit.WindHigh:F1}");
				cumulus.LogSpikeRemoval($"Wind: NewVal={windAvgMS:F1} OldVal={previousWind:F1} SpikeWindDiff={cumulus.Spike.WindDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastError = $"Wind or gust difference greater than spike/limit value - Gust: NewVal={windGustMS:F1}m/s OldVal={previousGust:F1}m/s - Wind: NewVal={windAvgMS:F1}m/s OldVal={previousWind:F1}m/s";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousGust = windGustMS;
			previousWind = windAvgMS;

			// use bearing of zero when calm
			if ((Math.Abs(gustpar) < 0.001) && cumulus.StationOptions.UseZeroBearing)
			{
				Bearing = 0;
			}
			else
			{
				Bearing = (bearingpar + (int)cumulus.Calib.WindDir.Offset) % 360;
				if (Bearing < 0)
				{
					Bearing = 360 + Bearing;
				}

				if (Bearing == 0)
				{
					Bearing = 360;
				}
			}
			var uncalibratedgust = gustpar;
			calibratedgust = uncalibratedgust * cumulus.Calib.WindGust.Mult;
			WindLatest = calibratedgust;
			windspeeds[nextwindvalue] = uncalibratedgust;
			windbears[nextwindvalue] = Bearing;

			// Recalculate wind rose data
			lock (windcounts)
			{
				for (int i = 0; i < cumulus.NumWindRosePoints; i++)
				{
					windcounts[i] = 0;
				}

				for (int i = 0; i < numwindvalues; i++)
				{
					int j = (((windbears[i] * 100) + 1125) % 36000) / (int)Math.Floor(cumulus.WindRoseAngle * 100);
					windcounts[j] += windspeeds[i];
				}
			}

			if (numwindvalues < maxwindvalues)
			{
				numwindvalues++;
			}

			nextwindvalue = (nextwindvalue + 1) % maxwindvalues;
			if (calibratedgust > HiLoToday.HighGust)
			{
				HiLoToday.HighGust = calibratedgust;
				HiLoToday.HighGustTime = timestamp;
				HiLoToday.HighGustBearing = Bearing;
				WriteTodayFile(timestamp, false);
			}
			if (calibratedgust > ThisMonth.HighGust.Val)
			{
				ThisMonth.HighGust.Val = calibratedgust;
				ThisMonth.HighGust.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (calibratedgust > ThisYear.HighGust.Val)
			{
				ThisYear.HighGust.Val = calibratedgust;
				ThisYear.HighGust.Ts = timestamp;
				WriteYearIniFile();
			}
			// All time high gust?
			if (calibratedgust > AllTime.HighGust.Val)
			{
				SetAlltime(AllTime.HighGust, calibratedgust, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighGust", calibratedgust, true, timestamp);

			WindRecent[nextwind].Gust = uncalibratedgust;
			WindRecent[nextwind].Speed = speedpar;
			WindRecent[nextwind].Timestamp = timestamp;
			nextwind = (nextwind + 1) % MaxWindRecent;

			if (cumulus.StationOptions.UseWind10MinAve)
			{
				int numvalues = 0;
				double totalwind = 0;
				for (int i = 0; i < MaxWindRecent; i++)
				{
					if (timestamp - WindRecent[i].Timestamp <= cumulus.AvgSpeedTime)
					{
						numvalues++;
						if (cumulus.StationOptions.UseSpeedForAvgCalc)
						{
							totalwind += WindRecent[i].Speed;
						}
						else
						{
							totalwind += WindRecent[i].Gust;
						}
					}
				}
				// average the values
				WindAverage = totalwind / numvalues;
				//cumulus.LogDebugMessage("next=" + nextwind + " wind=" + uncalibratedgust + " tot=" + totalwind + " numv=" + numvalues + " avg=" + WindAverage);
			}
			else
			{
				WindAverage = speedpar;
			}

			WindAverage *= cumulus.Calib.WindSpeed.Mult;

			cumulus.HighWindAlarm.Triggered = DoAlarm(WindAverage, cumulus.HighWindAlarm.Value, cumulus.HighWindAlarm.Enabled, true);


			if (CalcRecentMaxGust)
			{
				// Find recent max gust
				double maxgust = 0;
				for (int i = 0; i <= MaxWindRecent - 1; i++)
				{
					if (timestamp - WindRecent[i].Timestamp <= cumulus.PeakGustTime)
					{
						if (WindRecent[i].Gust > maxgust)
						{
							maxgust = WindRecent[i].Gust;
						}
					}
				}
				RecentMaxGust = maxgust * cumulus.Calib.WindGust.Mult;
			}

			cumulus.HighGustAlarm.Triggered = DoAlarm(RecentMaxGust, cumulus.HighGustAlarm.Value, cumulus.HighGustAlarm.Enabled, true);

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
			for (int i = 0; i < MaxWindRecent; i++)
			{
				if (timestamp - WindVec[i].Timestamp < cumulus.AvgBearingTime)
				{
					totalwindX += WindVec[i].X;
					totalwindY += WindVec[i].Y;
				}
			}
			if (totalwindX == 0)
			{
				AvgBearing = 0;
			}
			else
			{
				AvgBearing = (int)Math.Round(RadToDeg(Math.Atan(totalwindY / totalwindX)));

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

			int diffFrom = 0;
			int diffTo = 0;
			BearingRangeFrom = AvgBearing;
			BearingRangeTo = AvgBearing;
			if (AvgBearing != 0)
			{
				for (int i = 0; i <= MaxWindRecent - 1; i++)
				{
					if ((timestamp - WindVec[i].Timestamp < cumulus.AvgBearingTime) && (WindVec[i].Bearing != 0))
					{
						// this reading was within the last N minutes
						int difference = getShortestAngle(AvgBearing, WindVec[i].Bearing);
						if ((difference > diffTo))
						{
							diffTo = difference;
							BearingRangeTo = WindVec[i].Bearing;
							// Calculate rounded up value
							BearingRangeTo10 = (int)(Math.Ceiling(WindVec[i].Bearing / 10.0) * 10);
						}
						if ((difference < diffFrom))
						{
							diffFrom = difference;
							BearingRangeFrom = WindVec[i].Bearing;
							BearingRangeFrom10 = (int)(Math.Floor(WindVec[i].Bearing / 10.0) * 10);
						}
					}
				}
			}
			else
			{
				BearingRangeFrom10 = 0;
				BearingRangeTo10 = 0;
			}

			// All time high wind speed?
			if (WindAverage > AllTime.HighWind.Val)
			{
				SetAlltime(AllTime.HighWind, WindAverage, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighWind", WindAverage, true, timestamp);

			WindReadyToPlot = true;
			HaveReadData = true;
		}

		public void DoUV(double value, DateTime timestamp)
		{
			UV = (value * cumulus.Calib.UV.Mult) + cumulus.Calib.UV.Offset;
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
			SolarRad = (value * cumulus.Calib.Solar.Mult) + cumulus.Calib.Solar.Offset;
			// Update display

			if (SolarRad > HiLoToday.HighSolar)
			{
				HiLoToday.HighSolar = SolarRad;
				HiLoToday.HighSolarTime = timestamp;
			}
			CurrentSolarMax = AstroLib.SolarMax(timestamp, cumulus.Longitude, cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.RStransfactor, cumulus.BrasTurbidity, cumulus.SolarCalc);

			if (!cumulus.UseBlakeLarsen)
			{
				IsSunny = (SolarRad > (CurrentSolarMax * cumulus.SunThreshold / 100)) && (SolarRad >= cumulus.SolarMinimum);
			}
			HaveReadData = true;
		}

		protected void DoSunHours(double hrs)
		{
			if (StartOfDaySunHourCounter < -9998)
			{
				cumulus.LogMessage("No start of day sun counter. Start counting from now");
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
			WetBulb = ConvertTempCToUser(temp);
			WetBulb = (WetBulb * cumulus.Calib.WetBulb.Mult) + cumulus.Calib.WetBulb.Offset;

			// calculate RH
			double TempDry = ConvertUserTempToC(OutdoorTemperature);
			double Es = MeteoLib.SaturationVapourPressure1980(TempDry);
			double Ew = MeteoLib.SaturationVapourPressure1980(temp);
			double E = Ew - (0.00066 * (1 + 0.00115 * temp) * (TempDry - temp) * 1013);
			int hum = (int)(100 * (E / Es));
			DoOutdoorHumidity(hum, timestamp);
			// calculate DP
			// Calculate DewPoint

			// dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
			OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(TempDry, hum));

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
		public bool initialiseRainCounterOnFirstData = true;
		//private bool RainReadyToPlot = false;
		private double rainthismonth = 0;
		private double rainthisyear = 0;
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

		//private bool manualftp;

		public void WriteYesterdayFile()
		{
			cumulus.LogMessage("Writing yesterday.ini");
			var hourInc = cumulus.GetHourInc();

			IniFile ini = new IniFile(cumulus.YesterdayFile);

			ini.SetValue("General", "Date", DateTime.Now.AddHours(hourInc));
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
			// Pressure
			ini.SetValue("Pressure", "Low", HiLoYest.LowPress);
			ini.SetValue("Pressure", "LTime", HiLoYest.LowPressTime.ToString("HH:mm"));
			ini.SetValue("Pressure", "High", HiLoYest.HighPress);
			ini.SetValue("Pressure", "HTime", HiLoYest.HighPressTime.ToString("HH:mm"));
			// rain rate
			ini.SetValue("Rain", "High", HiLoYest.HighRainRate);
			ini.SetValue("Rain", "HTime", HiLoYest.HighRainRateTime.ToString("HH:mm"));
			ini.SetValue("Rain", "HourlyHigh", HiLoYest.HighHourlyRain);
			ini.SetValue("Rain", "HHourlyTime", HiLoYest.HighHourlyRainTime.ToString("HH:mm"));
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
			// Pressure
			HiLoYest.LowPress = ini.GetValue("Pressure", "Low", 0.0);
			HiLoYest.LowPressTime = ini.GetValue("Pressure", "LTime", DateTime.MinValue);
			HiLoYest.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoYest.HighPressTime = ini.GetValue("Pressure", "HTime", DateTime.MinValue);
			// rain rate
			HiLoYest.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			HiLoYest.HighRainRateTime = ini.GetValue("Rain", "HTime", DateTime.MinValue);
			HiLoYest.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoYest.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", DateTime.MinValue);
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
			HiLoYest.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0.0);
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

				if (cumulus.MySqlSettings.CustomRollover.Enabled)
				{
					cumulus.CustomMysqlRolloverTimerTick();
				}

				if (cumulus.CustomHttpRolloverEnabled)
				{
					cumulus.CustomHttpRolloverUpdate();
				}

				// First save today"s extremes
				DoDayfile(timestamp);
				cumulus.LogMessage("Raincounter = " + Raincounter + " Raindaystart = " + raindaystart);

				// Calculate yesterday"s rain, allowing for the multiplier -
				// raintotal && raindaystart are not calibrated
				RainYesterday = (Raincounter - raindaystart) * cumulus.Calib.Rain.Mult;
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

					rainthismonth = 0;

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
					ThisMonth.LongestDryPeriod.Val = Cumulus.DefaultHiVal;
					ThisMonth.LongestWetPeriod.Val = Cumulus.DefaultHiVal;
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
					rainthismonth += RainYesterday;

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
					ThisYear.LongestDryPeriod.Val = Cumulus.DefaultHiVal;
					ThisYear.LongestWetPeriod.Val = Cumulus.DefaultHiVal;
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
					rainthisyear = 0;
				}
				else
				{
					rainthisyear += RainYesterday;
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

				GrowingDegreeDaysThisYear1 += MeteoLib.GrowingDegreeDays(ConvertUserTempToC(HiLoToday.HighTemp), ConvertUserTempToC(HiLoToday.LowTemp), ConvertUserTempToC(cumulus.GrowingBase1), cumulus.GrowingCap30C);
				GrowingDegreeDaysThisYear2 += MeteoLib.GrowingDegreeDays(ConvertUserTempToC(HiLoToday.HighTemp), ConvertUserTempToC(HiLoToday.LowTemp), ConvertUserTempToC(cumulus.GrowingBase2), cumulus.GrowingCap30C);

				// Now reset all values to the current or default ones
				// We may be doing a roll-over from the first logger entry,
				// && as we do the roll-over before processing the entry, the
				// current items may not be set up.

				raindaystart = Raincounter;
				cumulus.LogMessage("Raindaystart set to " + raindaystart);

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
				WriteYesterdayFile();

				if (cumulus.NOAAconf.Create)
				{
					try
					{
						NOAA noaa = new NOAA(cumulus);
						var utf8WithoutBom = new System.Text.UTF8Encoding(false);
						var encoding = cumulus.NOAAconf.UseUtf8 ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");

						List<string> report;

						DateTime noaats = timestamp.AddDays(-1);

						// do monthly NOAA report
						cumulus.LogMessage("Creating NOAA monthly report for " + noaats.ToLongDateString());
						report = noaa.CreateMonthlyReport(noaats);
						cumulus.NOAAconf.LatestMonthReport = FormatDateTime(cumulus.NOAAconf.MonthFile, noaats);
						string noaafile = cumulus.ReportPath + cumulus.NOAAconf.LatestMonthReport;
						cumulus.LogMessage("Saving monthly report as " + noaafile);
						File.WriteAllLines(noaafile, report, encoding);

						// do yearly NOAA report
						cumulus.LogMessage("Creating NOAA yearly report");
						report = noaa.CreateYearlyReport(noaats);
						cumulus.NOAAconf.LatestYearReport = FormatDateTime(cumulus.NOAAconf.YearFile, noaats);
						noaafile = cumulus.ReportPath + cumulus.NOAAconf.LatestYearReport;
						cumulus.LogMessage("Saving yearly report as " + noaafile);
						File.WriteAllLines(noaafile, report, encoding);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("Error creating NOAA reports: " + ex.Message);
					}
				}

				// Do we need to upload NOAA reports on next FTP?
				cumulus.NOAAconf.NeedFtp = cumulus.NOAAconf.AutoFtp;
				cumulus.NOAAconf.NeedCopy = cumulus.NOAAconf.AutoCopy;

				if (cumulus.NOAAconf.NeedFtp || cumulus.NOAAconf.NeedCopy)
				{
					cumulus.LogMessage("NOAA reports will be uploaded at next web update");
				}

				// Do the End of day Extra files
				// This will set a flag to transfer on next FTP if required
				cumulus.DoExtraEndOfDayFiles();
				if (cumulus.EODfilesNeedFTP)
				{
					cumulus.LogMessage("Extra files will be uploaded at next web update");
				}

				// Do the Daily graph data files
				CreateEodGraphDataFiles();
				cumulus.LogMessage("If required the daily graph data files will be uploaded at next web update");


				if (!string.IsNullOrEmpty(cumulus.DailyProgram))
				{
					cumulus.LogMessage("Executing daily program: " + cumulus.DailyProgram + " params: " + cumulus.DailyParams);
					try
					{
						// Prepare the process to run
						ProcessStartInfo start = new ProcessStartInfo();
						// Enter in the command line arguments
						start.Arguments = cumulus.DailyParams;
						// Enter the executable to run, including the complete path
						start.FileName = cumulus.DailyProgram;
						// Don"t show a console window
						start.CreateNoWindow = true;
						// Run the external process
						Process.Start(start);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("Error executing external program: " + ex.Message);
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
			strb.Append((int)HiLoToday.HighSolar + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighSolarTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighUv.ToString(cumulus.UVFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighUvTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighFeelsLike.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighFeelsLikeTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowFeelsLike.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowFeelsLikeTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHumidex.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighHumidexTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(ChillHours.ToString(cumulus.TempFormat));

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
						cumulus.LogMessage("***Error: Daily values are still at default at end of day");
						cumulus.LogMessage("Data not logged to dayfile.txt");
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
					cumulus.LogMessage("Error writing to dayfile.txt: " + ex.Message);
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
				HighSolar = (int)HiLoToday.HighSolar,
				HighSolarTime = HiLoToday.HighSolarTime,
				HighUv = HiLoToday.HighUv,
				HighUvTime = HiLoToday.HighUvTime,
				HighFeelsLike = HiLoToday.HighFeelsLike,
				HighFeelsLikeTime = HiLoToday.HighFeelsLikeTime,
				LowFeelsLike = HiLoToday.LowFeelsLike,
				LowFeelsLikeTime = HiLoToday.LowFeelsLikeTime,
				HighHumidex = HiLoToday.HighHumidex,
				HighHumidexTime = HiLoToday.HighHumidexTime,
				ChillHours = ChillHours
			};

			DayFile.Add(newRec);



			if (cumulus.MySqlSettings.Dayfile.Enabled)
			{
				var InvC = new CultureInfo("");

				StringBuilder queryString = new StringBuilder(cumulus.StartOfDayfileInsertSQL, 1024);
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
				queryString.Append((int)HiLoToday.HighSolar + ",");
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
				queryString.Append(HiLoToday.HighFeelsLikeTime.ToString("\\'HH:mm\\'"));

				queryString.Append(")");

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

		/// <summary>
		///  Convert temp supplied in C to units in use
		/// </summary>
		/// <param name="value">Temp in C</param>
		/// <returns>Temp in configured units</returns>
		public double ConvertTempCToUser(double value)
		{
			if (cumulus.Units.Temp == 1)
			{
				return MeteoLib.CToF(value);
			}
			else
			{
				// C
				return value;
			}
		}

		/// <summary>
		///  Convert temp supplied in F to units in use
		/// </summary>
		/// <param name="value">Temp in F</param>
		/// <returns>Temp in configured units</returns>
		public double ConvertTempFToUser(double value)
		{
			if (cumulus.Units.Temp == 0)
			{
				return MeteoLib.FtoC(value);
			}
			else
			{
				// F
				return value;
			}
		}

		/// <summary>
		///  Convert temp supplied in user units to C
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in C</returns>
		public double ConvertUserTempToC(double value)
		{
			if (cumulus.Units.Temp == 1)
			{
				return MeteoLib.FtoC(value);
			}
			else
			{
				// C
				return value;
			}
		}

		/// <summary>
		///  Convert temp supplied in user units to F
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in F</returns>
		public double ConvertUserTempToF(double value)
		{
			if (cumulus.Units.Temp == 1)
			{
				return value;
			}
			else
			{
				// C
				return MeteoLib.CToF(value);
			}
		}

		/// <summary>
		///  Converts wind supplied in m/s to user units
		/// </summary>
		/// <param name="value">Wind in m/s</param>
		/// <returns>Wind in configured units</returns>
		public double ConvertWindMSToUser(double value)
		{
			switch (cumulus.Units.Wind)
			{
				case 0:
					return value;
				case 1:
					return value * 2.23693629;
				case 2:
					return value * 3.6;
				case 3:
					return value * 1.94384449;
				default:
					return 0;
			}
		}

		/// <summary>
		///  Converts wind supplied in mph to user units
		/// </summary>
		/// <param name="value">Wind in mph</param>
		/// <returns>Wind in configured units</returns>
		public double ConvertWindMPHToUser(double value)
		{
			switch (cumulus.Units.Wind)
			{
				case 0:
					return value * 0.44704;
				case 1:
					return value;
				case 2:
					return value * 1.60934;
				case 3:
					return value * 0.868976;
				default:
					return 0;
			}
		}

		/// <summary>
		/// Converts wind in user units to m/s
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public virtual double ConvertUserWindToMS(double value)
		{
			switch (cumulus.Units.Wind)
			{
				case 0:
					return value;
				case 1:
					return value / 2.23693629;
				case 2:
					return value / 3.6F;
				case 3:
					return value / 1.94384449;
				default:
					return 0;
			}
		}

		/// <summary>
		/// Converts value in kilometres to distance unit based on users configured wind units
		/// </summary>
		/// <param name="val"></param>
		/// <returns>Wind in configured units</returns>
		public double ConvertKmtoUserUnits(double val)
		{
			switch (cumulus.Units.Wind)
			{
				case 0: // m/s
				case 2: // km/h
					return val;
				case 1: // mph
					return val * 0.621371;
				case 3: // knots
					return val * 0.539957;
			}
			return val;
		}

		/// <summary>
		///  Converts windrun supplied in user units to km
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in km</returns>
		public virtual double ConvertWindRunToKm(double value)
		{
			switch (cumulus.Units.Wind)
			{
				case 0: // m/s
				case 2: // km/h
					return value;
				case 1: // mph
					return value / 0.621371192;
				case 3: // knots
					return value / 0.539956803;
				default:
					return 0;
			}
		}

		public double ConvertUserWindToKPH(double wind) // input is in Units.Wind units, convert to km/h
		{
			switch (cumulus.Units.Wind)
			{
				case 0: // m/s
					return wind * 3.6;
				case 1: // mph
					return wind * 1.609344;
				case 2: // kph
					return wind;
				case 3: // knots
					return wind * 1.852;
				default:
					return wind;
			}
		}

		/// <summary>
		/// Converts rain in mm to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public virtual double ConvertRainMMToUser(double value)
		{
			return cumulus.Units.Rain == 1 ? value * 0.0393700787 : value;
		}

		/// <summary>
		/// Converts rain in inches to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public virtual double ConvertRainINToUser(double value)
		{
			return cumulus.Units.Rain == 1 ? value : value * 25.4;
		}

		/// <summary>
		/// Converts rain in units in use to mm
		/// </summary>
		/// <param name="value">Rain in configured units</param>
		/// <returns>Rain in mm</returns>
		public virtual double ConvertUserRainToMM(double value)
		{
			return cumulus.Units.Rain == 1 ? value / 0.0393700787 : value;
		}

		/// <summary>
		/// Convert pressure in mb to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public double ConvertPressMBToUser(double value)
		{
			return cumulus.Units.Press == 2 ? value * 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure in inHg to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public double ConvertPressINHGToUser(double value)
		{
			return cumulus.Units.Press == 2 ? value : value * 33.8638866667;
		}

		/// <summary>
		/// Convert pressure in units in use to mb
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public double ConvertUserPressToMB(double value)
		{
			return cumulus.Units.Press == 2 ? value / 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure in units in use to inHg
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public double ConvertUserPressToIN(double value)
		{
			return cumulus.Units.Press == 2 ? value : value * 0.0295333727;
		}

		public string CompassPoint(int bearing)
		{
			return bearing == 0 ? "-" : cumulus.compassp[(((bearing * 100) + 1125) % 36000) / 2250];
		}

		public void StartLoop()
		{
			try
			{
				t = new Thread(Start) { IsBackground = true };
				t.Start();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("An error occurred during the station start-up: " + ex.Message);
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
			var avg = 90 - (int)(RadToDeg(Math.Atan2(y, x)));
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
				case (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor:
					if (cumulus.airLinkDataOut != null)
					{
						pm2p5 = cumulus.airLinkDataOut.pm2p5;
						pm10 = cumulus.airLinkDataOut.pm10;
					}
					break;
				case (int)Cumulus.PrimaryAqSensor.AirLinkIndoor:
					if (cumulus.airLinkDataIn != null)
					{
						pm2p5 = cumulus.airLinkDataIn.pm2p5;
						pm10 = cumulus.airLinkDataIn.pm10;
					}
					break;
				case (int)Cumulus.PrimaryAqSensor.Ecowitt1:
					pm2p5 = AirQuality1;
					break;
				case (int)Cumulus.PrimaryAqSensor.Ecowitt2:
					pm2p5 = AirQuality2;
					break;
				case (int)Cumulus.PrimaryAqSensor.Ecowitt3:
					pm2p5 = AirQuality3;
					break;
				case (int)Cumulus.PrimaryAqSensor.Ecowitt4:
					pm2p5 = AirQuality3;
					break;
				case (int)Cumulus.PrimaryAqSensor.EcowittCO2:
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
				cumulus.LogMessage($"UpdateGraphDataAqEntry: Exception caught: {e.Message}");
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

		public void DoTrendValues(DateTime ts)
		{
			List<RecentData> retVals;
			double trendval;

			try
			{
				// Do 3 hour trends
				retVals = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=? order by Timestamp", ts.AddHours(-3));

				if (retVals.Count > 1)
				{
					// calculate and display the temp trend
					temptrendval = (retVals.Last().OutsideTemp - retVals.First().OutsideTemp) / 3.0F;
					cumulus.TempChangeAlarm.UpTriggered = DoAlarm(temptrendval, cumulus.TempChangeAlarm.Value, cumulus.TempChangeAlarm.Enabled, true);
					cumulus.TempChangeAlarm.DownTriggered = DoAlarm(temptrendval, cumulus.TempChangeAlarm.Value * -1, cumulus.TempChangeAlarm.Enabled, false);


					// calculate and display the pressure trend
					presstrendval = (retVals.Last().Pressure - retVals.First().Pressure) / 3.0;
					cumulus.PressChangeAlarm.UpTriggered = DoAlarm(presstrendval, cumulus.PressChangeAlarm.Value, cumulus.PressChangeAlarm.Enabled, true);
					cumulus.PressChangeAlarm.DownTriggered = DoAlarm(presstrendval, cumulus.PressChangeAlarm.Value * -1, cumulus.PressChangeAlarm.Enabled, false);

					// Convert for display
					//trendval = ConvertPressMBToUser(presstrendval);
				}
				else
				{
					temptrendval = 0;
					presstrendval = 0;
				}

				// Do 1 hour trends
				retVals = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >=? order by Timestamp", ts.AddHours(-1));

				if (retVals.Count > 1)
				{
					// Calculate Temperature change in the last hour
					TempChangeLastHour = retVals.Last().OutsideTemp - retVals.First().OutsideTemp;


					// calculate and display rainfall in last hour
					if (Raincounter < retVals[0].raincounter)
					{
						// rain total is not available or has gone down, assume it was reset to zero, just use zero
						RainLastHour = 0;
					}
					else
					{
						// normal case
						trendval = retVals.Last().raincounter - retVals.First().raincounter;

						// Round value as some values may have been read from log file and already rounded
						trendval = Math.Round(trendval, cumulus.RainDPlaces);

						var tempRainLastHour = trendval * cumulus.Calib.Rain.Mult;

						if (ConvertUserRainToMM(tempRainLastHour) > cumulus.Spike.MaxHourlyRain)
						{
							// ignore
							cumulus.LogSpikeRemoval("Max hourly rainfall spike value exceed");
							lastSpikeRemoval = DateTime.Now;
							cumulus.SpikeAlarm.LastError = $"Max hourly rainfall greater than spike value - Value={tempRainLastHour:F1}";
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
								HiLoToday.HighHourlyRainTime = ts;
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
				else
				{
					TempChangeLastHour = 0;
					RainLastHour = 0;
				}


				if (calculaterainrate)
				{
					// Station doesn't supply rain rate, calculate one based on rain in last 5 minutes

					DateTime fiveminutesago = ts.AddSeconds(-330);


					var fiveminutedata = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp", fiveminutesago);

					if (fiveminutedata.Count() > 1)
					{
						// we have at least two values to compare

						TimeSpan span = fiveminutedata.Last().Timestamp.Subtract(fiveminutedata.First().Timestamp);

						double timediffhours = span.TotalHours;

						//cumulus.LogMessage("first time = " + fiveminutedata.First().timestamp + " last time = " + fiveminutedata.Last().timestamp);
						//cumulus.LogMessage("timediffhours = " + timediffhours);

						// if less than 5 minutes, use 5 minutes
						if (timediffhours < 1.0 / 12.0)
						{
							timediffhours = 1.0 / 12.0;
						}

						double raindiff = Math.Round(fiveminutedata.Last().raincounter, cumulus.RainDPlaces) - Math.Round(fiveminutedata.First().raincounter, cumulus.RainDPlaces);
						//cumulus.LogMessage("first value = " + fiveminutedata.First().raincounter + " last value = " + fiveminutedata.Last().raincounter);
						//cumulus.LogMessage("raindiff = " + raindiff);

						// Scale the counter values
						var tempRainRate = (double)(raindiff / timediffhours) * cumulus.Calib.Rain.Mult;

						if (tempRainRate < 0)
						{
							tempRainRate = 0;
						}

						if (ConvertUserRainToMM(tempRainRate) > cumulus.Spike.MaxRainRate)
						{
							// ignore
							cumulus.LogSpikeRemoval("Max rainfall rate spike value exceed");
							lastSpikeRemoval = DateTime.Now;
							cumulus.SpikeAlarm.LastError = $"Max rainfall rate greater than spike value - Value={tempRainRate:F1}";
							cumulus.SpikeAlarm.Triggered = true;

						}
						else
						{
							RainRate = tempRainRate;

							if (RainRate > AllTime.HighRainRate.Val)
								SetAlltime(AllTime.HighRainRate, RainRate, ts);

							CheckMonthlyAlltime("HighRainRate", RainRate, true, ts);

							cumulus.HighRainRateAlarm.Triggered = DoAlarm(RainRate, cumulus.HighRainRateAlarm.Value, cumulus.HighRainRateAlarm.Enabled, true);

							if (RainRate > HiLoToday.HighRainRate)
							{
								HiLoToday.HighRainRate = RainRate;
								HiLoToday.HighRainRateTime = ts;
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
					else
					{
						RainRate = 0;
					}
				}



				// calculate and display rainfall in last 24 hour
				var onedayago = ts.AddDays(-1);
				retVals = RecentDataDb.Query<RecentData>("select raincounter from RecentData where Timestamp >= ? order by Timestamp", onedayago);

				if (retVals.Count > 1)
				{
					trendval = retVals.Last().raincounter - retVals.First().raincounter;
					// Round value as some values may have been read from log file and already rounded
					trendval = Math.Round(trendval, cumulus.RainDPlaces);

					if (trendval < 0)
					{
						trendval = 0;
					}

					RainLast24Hour = trendval * cumulus.Calib.Rain.Mult;
				}
				else
				{
					// Unable to retrieve rain counter from 24 hours ago
					RainLast24Hour = 0;
				}
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"DoTrendValues: Error - {e.Message}");
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
					cumulus.LogMessage("Error in dominant wind direction calculation");
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
				ResetSunshineHours();
				//RecalcSolarFactor(DateTime.Now);
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
		public bool IsRaining { get; set; }

		public void LoadLastHoursFromDataLogs(DateTime ts)
		{
			cumulus.LogMessage("Loading last N hour data from data logs: " + ts);
			LoadRecentFromDataLogs(ts);
			LoadRecentAqFromDataLogs(ts);
			LoadLast3HourData(ts);
			LoadRecentWindRose();
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
			int numadded = 0;

			var rowsToAdd = new List<RecentData>();

			cumulus.LogMessage($"LoadRecent: Attempting to load {cumulus.RecentDataDays} days of entries to recent data list");

			// try and find the first entry in the database that has a "blank" AQ entry (PM2.5 or PM10 = -1)
			try
			{
				var start = RecentDataDb.ExecuteScalar<DateTime>("select MAX(Timestamp) from RecentData");
				if (datefrom < start)
					datefrom = start;
			}
			catch (Exception e)
			{
				cumulus.LogMessage("LoadRecent: Error querying database for latest record - " + e.Message);
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
										SolarRad = (int)rec.SolarRad,
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
										SolarMax = (int)rec.CurrentSolarMax,
										RainRate = rec.RainRate,
										Pm2p5 = -1,
										Pm10 = -1
									});
									++numadded;
								}
							}
							catch (Exception e)
							{
								cumulus.LogMessage($"LoadRecent: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogMessage($"LoadRecent: Too many errors reading {logFile} - aborting load of graph data");
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadRecent: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}

					try
					{
						if (rowsToAdd.Count > 0)
							RecentDataDb.InsertAllOrIgnore(rowsToAdd);
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadRecent: Error inserting recent data into database: {e.Message}");
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
			cumulus.LogMessage($"LoadRecent: Loaded {numadded} new entries to recent database");
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
				cumulus.LogMessage("LoadRecentAqFromDataLogs: Error querying database for oldest record without AQ data - " + e.Message);
			}

			if (cumulus.StationOptions.PrimaryAqSensor < 0) return;

			cumulus.LogMessage($"LoadRecentAqFromDataLogs: Attempting to load {cumulus.RecentDataDays} days of entries to Air Quality recent data");

			if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor
				|| cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				logFile = cumulus.GetAirLinkLogFileName(filedate);
			}
			else if ((cumulus.StationOptions.PrimaryAqSensor >= (int)Cumulus.PrimaryAqSensor.Ecowitt1 && cumulus.StationOptions.PrimaryAqSensor <= (int)Cumulus.PrimaryAqSensor.Ecowitt4) ||
					cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.EcowittCO2) // Ecowitt
			{
				logFile = cumulus.GetExtraLogFileName(filedate);
			}
			else
			{
				cumulus.LogMessage($"LoadRecentAqFromDataLogs: Error - The primary AQ sensor is not set to a valid value, currently={cumulus.StationOptions.PrimaryAqSensor}");
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
									if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkIndoor)
									{
										// AirLink Indoor
										pm2p5 = Convert.ToDouble(st[5]);
										pm10 = Convert.ToDouble(st[10]);
									}
									else if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor)
									{
										// AirLink Outdoor
										pm2p5 = Convert.ToDouble(st[32]);
										pm10 = Convert.ToDouble(st[37]);
									}
									else if (cumulus.StationOptions.PrimaryAqSensor >= (int)Cumulus.PrimaryAqSensor.Ecowitt1 && cumulus.StationOptions.PrimaryAqSensor <= (int)Cumulus.PrimaryAqSensor.Ecowitt4)
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
								cumulus.LogMessage($"LoadRecentAqFromDataLogs: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 20)
								{
									cumulus.LogMessage($"LoadRecentAqFromDataLogs: Too many errors reading {logFile} - aborting load of graph data");
								}
							}
						}

						RecentDataDb.Commit();
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadRecentAqFromDataLogs: Error at line {linenum} of {logFile} : {e.Message}");
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
					if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor
						|| cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkIndoor) // AirLink
					{
						logFile = cumulus.GetAirLinkLogFileName(filedate);
					}
					else if ((cumulus.StationOptions.PrimaryAqSensor >= (int)Cumulus.PrimaryAqSensor.Ecowitt1
						&& cumulus.StationOptions.PrimaryAqSensor <= (int)Cumulus.PrimaryAqSensor.Ecowitt4)
						|| cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.EcowittCO2) // Ecowitt
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

			foreach (var rec in result)
			{
				try
				{
					WindRecent[nextwind].Gust = rec.WindGust;
					WindRecent[nextwind].Speed = rec.WindSpeed;
					WindRecent[nextwind].Timestamp = rec.Timestamp;
					nextwind = (nextwind + 1) % MaxWindRecent;

					WindVec[nextwindvec].X = rec.WindGust * Math.Sin(DegToRad(rec.WindDir));
					WindVec[nextwindvec].Y = rec.WindGust * Math.Cos(DegToRad(rec.WindDir));
					WindVec[nextwindvec].Timestamp = rec.Timestamp;
					WindVec[nextwindvec].Bearing = Bearing; // savedBearing;
					nextwindvec = (nextwindvec + 1) % MaxWindRecent;
				}
				catch (Exception e)
				{
					cumulus.LogMessage($"LoadLast3Hour: Error loading data from database : {e.Message}");
				}
			}
			cumulus.LogMessage($"LoadLast3Hour: Loaded {result.Count} entries to last 3 hour data list");
		}

		private static DateTime GetDateTime(DateTime date, string time)
		{
			var tim = time.Split(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator.ToCharArray()[0]);
			return new DateTime(date.Year, date.Month, date.Day, int.Parse(tim[0]), int.Parse(tim[1]), 0);
		}

		public void LoadDayFile()
		{
			int addedEntries = 0;

			cumulus.LogMessage($"LoadDayFile: Attempting to load the day file");
			if (File.Exists(cumulus.DayFileName))
			{
				int linenum = 0;
				int errorCount = 0;

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
								DayFile.Add(ParseDayFileRec(Line));

								addedEntries++;
							}
							catch (Exception e)
							{
								cumulus.LogMessage($"LoadDayFile: Error at line {linenum} of {cumulus.DayFileName} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 20)
								{
									cumulus.LogMessage($"LoadDayFile: Too many errors reading {cumulus.DayFileName} - aborting load of daily data");
								}
							}
						} while (!(sr.EndOfStream || errorCount >= 20));
						sr.Close();
					}

					watch.Stop();
					cumulus.LogDebugMessage($"LoadDayFile: Dayfile parse = {watch.ElapsedMilliseconds} ms");

				}
				catch (Exception e)
				{
					cumulus.LogMessage($"LoadDayFile: Error at line {linenum} of {cumulus.DayFileName} : {e.Message}");
					cumulus.LogMessage("Please edit the file to correct the error");
				}
				cumulus.LogMessage($"LoadDayFile: Loaded {addedEntries} entries to recent daily data list");
			}
			else
			{
				cumulus.LogMessage("LoadDayFile: No Dayfile found - No entries added to recent daily data list");
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
					rec.LowHumidity = 9999;

				if (st.Count > idx++ && st[20].Length == 5)
					rec.LowHumidityTime = GetDateTime(rec.Date, st[20]);

				if (st.Count > idx++ && double.TryParse(st[21], out varDbl))
					rec.HighHumidity = Convert.ToInt32(varDbl);
				else
					rec.HighHumidity = -9999;

				if (st.Count > idx++ && st[22].Length == 5)
					rec.HighHumidityTime = GetDateTime(rec.Date, st[22]);

				if (st.Count > idx++ && double.TryParse(st[23], out varDbl))
					rec.ET = varDbl;

				if (st.Count > idx++ && double.TryParse(st[24], out varDbl))
					rec.SunShineHours = varDbl;

				if (st.Count > idx++ && double.TryParse(st[25], out varDbl))
					rec.HighHeatIndex = varDbl;
				else
					rec.HighHeatIndex = -9999;

				if (st.Count > idx++ && st[26].Length == 5)
					rec.HighHeatIndexTime = GetDateTime(rec.Date, st[26]);

				if (st.Count > idx++ && double.TryParse(st[27], out varDbl))
					rec.HighAppTemp = varDbl;
				else
					rec.HighAppTemp = -9999;

				if (st.Count > idx++ && st[28].Length == 5)
					rec.HighAppTempTime = GetDateTime(rec.Date, st[28]);

				if (st.Count > idx++ && double.TryParse(st[29], out varDbl))
					rec.LowAppTemp = varDbl;
				else
					rec.LowAppTemp = 9999;

				if (st.Count > idx++ && st[30].Length == 5)
					rec.LowAppTempTime = GetDateTime(rec.Date, st[30]);

				if (st.Count > idx++ && double.TryParse(st[31], out varDbl))
					rec.HighHourlyRain = varDbl;

				if (st.Count > idx++ && st[32].Length == 5)
					rec.HighHourlyRainTime = GetDateTime(rec.Date, st[32]);

				if (st.Count > idx++ && double.TryParse(st[33], out varDbl))
					rec.LowWindChill = varDbl;
				else
					rec.LowWindChill = 9999;

				if (st.Count > idx++ && st[34].Length == 5)
					rec.LowWindChillTime = GetDateTime(rec.Date, st[34]);

				if (st.Count > idx++ && double.TryParse(st[35], out varDbl))
					rec.HighDewPoint = varDbl;
				else
					rec.HighDewPoint = -9999;

				if (st.Count > idx++ && st[36].Length == 5)
					rec.HighDewPointTime = GetDateTime(rec.Date, st[36]);

				if (st.Count > idx++ && double.TryParse(st[37], out varDbl))
					rec.LowDewPoint = varDbl;
				else
					rec.LowDewPoint = 9999;

				if (st.Count > idx++ && st[38].Length == 5)
					rec.LowDewPointTime = GetDateTime(rec.Date, st[38]);

				if (st.Count > idx++ && double.TryParse(st[39], out varDbl))
					rec.DominantWindBearing = Convert.ToInt32(varDbl);

				if (st.Count > idx++ && double.TryParse(st[40], out varDbl))
					rec.HeatingDegreeDays = varDbl;

				if (st.Count > idx++ && double.TryParse(st[41], out varDbl))
					rec.CoolingDegreeDays = varDbl;

				if (st.Count > idx++ && double.TryParse(st[42], out varDbl))
					rec.HighSolar = Convert.ToInt32(varDbl);

				if (st.Count > idx++ && st[43].Length == 5)
					rec.HighSolarTime = GetDateTime(rec.Date, st[43]);

				if (st.Count > idx++ && double.TryParse(st[44], out varDbl))
					rec.HighUv = varDbl;

				if (st.Count > idx++ && st[45].Length == 5)
					rec.HighUvTime = GetDateTime(rec.Date, st[45]);

				if (st.Count > idx++ && double.TryParse(st[46], out varDbl))
					rec.HighFeelsLike = varDbl;
				else
					rec.HighFeelsLike = -9999;

				if (st.Count > idx++ && st[47].Length == 5)
					rec.HighFeelsLikeTime = GetDateTime(rec.Date, st[47]);

				if (st.Count > idx++ && double.TryParse(st[48], out varDbl))
					rec.LowFeelsLike = varDbl;
				else
					rec.LowFeelsLike = 9999;

				if (st.Count > idx++ && st[49].Length == 5)
					rec.LowFeelsLikeTime = GetDateTime(rec.Date, st[49]);

				if (st.Count > idx++ && double.TryParse(st[50], out varDbl))
					rec.HighHumidex = varDbl;
				else
					rec.HighHumidex = -9999;

				if (st.Count > idx++ && st[51].Length == 5)
					rec.HighHumidexTime = GetDateTime(rec.Date, st[51]);

				if (st.Count > idx++ && double.TryParse(st[52], out varDbl))
					rec.ChillHours = varDbl;
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage($"ParseDayFileRec: Error at record {idx} - {ex.Message}");
				var e = new Exception($"Error at record {idx} = \"{st[idx-1]}\" - {ex.Message}");
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
				var notPresent = 0.0;

				// is the record going to be used for min/max record determination?
				if (minMax)
				{
					notPresent = -99999;
				}
				var st = new List<string>(Regex.Split(data, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

				// We allow int values to have a decimal point becuase log files sometimes get mangled by Excel etc!
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
					WindLatest = Convert.ToDouble(st[14]),
					WindChill = st.Count > 15 ? Convert.ToDouble(st[15]) : notPresent,
					HeatIndex = st.Count > 16 ? Convert.ToDouble(st[16]) : notPresent,
					UV = st.Count > 17 ? Convert.ToDouble(st[17]) : notPresent,
					SolarRad = st.Count > 18 ? Convert.ToDouble(st[18]) : notPresent,
					ET = st.Count > 19 ? Convert.ToDouble(st[19]) : notPresent,
					AnnualETTotal = st.Count > 20 ? Convert.ToDouble(st[20]) : notPresent,
					ApparentTemperature = st.Count > 21 ? Convert.ToDouble(st[21]) : notPresent,
					CurrentSolarMax = st.Count > 22 ? Convert.ToDouble(st[22]) : notPresent,
					SunshineHours = st.Count > 23 ? Convert.ToDouble(st[23]) : notPresent,
					Bearing = st.Count > 24 ? Convert.ToInt32(Convert.ToDouble(st[24])) : 0,
					RG11RainToday = st.Count > 25 ? Convert.ToDouble(st[25]) : notPresent,
					RainSinceMidnight = st.Count > 26 ? Convert.ToDouble(st[26]) : notPresent,
					FeelsLike = st.Count > 27 ? Convert.ToDouble(st[27]) : notPresent,
					Humidex = st.Count > 28 ? Convert.ToDouble(st[28]) : notPresent
				};

				return rec;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error parsing log file record: " + ex.Message);
				cumulus.LogDataMessage("Log record: " + data);
				throw ex;
			}
		}

		protected void UpdateStatusPanel(DateTime timestamp)
		{
			LastDataReadTimestamp = timestamp;
		}


		protected void UpdateMQTT()
		{
			if (cumulus.MQTT.EnableDataUpdate)
			{
				MqttPublisher.UpdateMQTTfeed("DataUpdate");
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
					SoilMoisture1 = (int)value;
					break;
				case 2:
					SoilMoisture2 = (int)value;
					break;
				case 3:
					SoilMoisture3 = (int)value;
					break;
				case 4:
					SoilMoisture4 = (int)value;
					break;
				case 5:
					SoilMoisture5 = (int)value;
					break;
				case 6:
					SoilMoisture6 = (int)value;
					break;
				case 7:
					SoilMoisture7 = (int)value;
					break;
				case 8:
					SoilMoisture8 = (int)value;
					break;
				case 9:
					SoilMoisture9 = (int)value;
					break;
				case 10:
					SoilMoisture10 = (int)value;
					break;
				case 11:
					SoilMoisture11 = (int)value;
					break;
				case 12:
					SoilMoisture12 = (int)value;
					break;
				case 13:
					SoilMoisture13 = (int)value;
					break;
				case 14:
					SoilMoisture14 = (int)value;
					break;
				case 15:
					SoilMoisture15 = (int)value;
					break;
				case 16:
					SoilMoisture16 = (int)value;
					break;
			}
		}

		public void DoSoilTemp(double value, int index)
		{
			switch (index)
			{
				case 1:
					SoilTemp1 = value;
					break;
				case 2:
					SoilTemp2 = value;
					break;
				case 3:
					SoilTemp3 = value;
					break;
				case 4:
					SoilTemp4 = value;
					break;
				case 5:
					SoilTemp5 = value;
					break;
				case 6:
					SoilTemp6 = value;
					break;
				case 7:
					SoilTemp7 = value;
					break;
				case 8:
					SoilTemp8 = value;
					break;
				case 9:
					SoilTemp9 = value;
					break;
				case 10:
					SoilTemp10 = value;
					break;
				case 11:
					SoilTemp11 = value;
					break;
				case 12:
					SoilTemp12 = value;
					break;
				case 13:
					SoilTemp13 = value;
					break;
				case 14:
					SoilTemp14 = value;
					break;
				case 15:
					SoilTemp15 = value;
					break;
				case 16:
					SoilTemp16 = value;
					break;
			}
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
					LeafWetness1 = (int)value;
					break;
				case 2:
					LeafWetness2 = (int)value;
					break;
				case 3:
					LeafWetness3 = (int)value;
					break;
				case 4:
					LeafWetness4 = (int)value;
					break;
				case 5:
					LeafWetness5 = (int)value;
					break;
				case 6:
					LeafWetness6 = (int)value;
					break;
				case 7:
					LeafWetness7 = (int)value;
					break;
				case 8:
					LeafWetness8 = (int)value;
					break;
			}
		}

		public void DoLeafTemp(double value, int index)
		{
			switch (index)
			{
				case 1:
					LeafTemp1 = value;
					break;
				case 2:
					LeafTemp2 = value;
					break;
				case 3:
					LeafTemp3 = value;
					break;
				case 4:
					LeafTemp4 = value;
					break;
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
				if (z_wind == cumulus.compassp[0]) // N
				{
					z_hpa += 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[1]) // NNE
				{
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[2]) // NE
				{
					//			z_hpa += 4 ;
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[3]) // ENE
				{
					z_hpa += 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[4]) // E
				{
					z_hpa -= 0.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[5]) // ESE
				{
					//			z_hpa -= 3 ;
					z_hpa -= 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[6]) // SE
				{
					z_hpa -= 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[7]) // SSE
				{
					z_hpa -= 8.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[8]) // S
				{
					//			z_hpa -= 11 ;
					z_hpa -= 12F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[9]) // SSW
				{
					z_hpa -= 10F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[10]) // SW
				{
					z_hpa -= 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[11]) // WSW
				{
					z_hpa -= 4.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[12]) // W
				{
					z_hpa -= 3F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[13]) // WNW
				{
					z_hpa -= 0.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[14]) // NW
				{
					z_hpa += 1.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.compassp[15]) // NNW
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
				if (z_wind == cumulus.compassp[8]) // S
				{
					z_hpa += 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[9]) // SSW
				{
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[10]) // SW
				{
					//			z_hpa += 4 ;
					z_hpa += 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[11]) // WSW
				{
					z_hpa += 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[12]) // W
				{
					z_hpa -= 0.5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[13]) // WNW
				{
					//			z_hpa -= 3 ;
					z_hpa -= 2F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[14]) // NW
				{
					z_hpa -= 5F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[15]) // NNW
				{
					z_hpa -= 8.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.compassp[0]) // N
				{
					//			z_hpa -= 11 ;
					z_hpa -= 12F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[1]) // NNE
				{
					z_hpa -= 10F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[2]) // NE
				{
					z_hpa -= 6F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[3]) // ENE
				{
					z_hpa -= 4.5F / 100 * z_range; //
				}
				else if (z_wind == cumulus.compassp[4]) // E
				{
					z_hpa -= 3F / 100F * z_range;
				}
				else if (z_wind == cumulus.compassp[5]) // ESE
				{
					z_hpa -= 0.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.compassp[6]) // SE
				{
					z_hpa += 1.5F / 100 * z_range;
				}
				else if (z_wind == cumulus.compassp[7]) // SSE
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

			int z_option = (int)Math.Floor((z_hpa - z_baro_bottom) / z_constant);

			StringBuilder z_output = new StringBuilder(100);
			if (z_option < 0)
			{
				z_option = 0;
				z_output.Append($"{cumulus.exceptional}, ");
			}
			if (z_option > 21)
			{
				z_option = 21;
				z_output.Append($"{cumulus.exceptional}, ");
			}

			if (z_trend == 1)
			{
				// rising
				Forecastnumber = cumulus.riseOptions[z_option] + 1;
				z_output.Append(cumulus.zForecast[cumulus.riseOptions[z_option]]);
			}
			else if (z_trend == 2)
			{
				// falling
				Forecastnumber = cumulus.fallOptions[z_option] + 1;
				z_output.Append(cumulus.zForecast[cumulus.fallOptions[z_option]]);
			}
			else
			{
				// must be "steady"
				Forecastnumber = cumulus.steadyOptions[z_option] + 1;
				z_output.Append(cumulus.zForecast[cumulus.steadyOptions[z_option]]);
			}
			return z_output.ToString();
		}

		public int Forecastnumber { get; set; }

		/// <summary>
		/// Takes speed in user units, returns Bft number
		/// </summary>
		/// <param name="windspeed"></param>
		/// <returns></returns>
		public int Beaufort(double speed)
		{
			double windspeedMS = ConvertUserWindToMS(speed);
			if (windspeedMS < 0.3)
				return 0;
			else if (windspeedMS < 1.6)
				return 1;
			else if (windspeedMS < 3.4)
				return 2;
			else if (windspeedMS < 5.5)
				return 3;
			else if (windspeedMS < 8.0)
				return 4;
			else if (windspeedMS < 10.8)
				return 5;
			else if (windspeedMS < 13.9)
				return 6;
			else if (windspeedMS < 17.2)
				return 7;
			else if (windspeedMS < 20.8)
				return 8;
			else if (windspeedMS < 24.5)
				return 9;
			else if (windspeedMS < 28.5)
				return 10;
			else if (windspeedMS < 32.7)
				return 11;
			else return 12;
		}

		// This overridden in each station implementation
		public abstract void Stop();

		public void ReadAlltimeIniFile()
		{
			cumulus.LogMessage(Path.GetFullPath(cumulus.AlltimeIniFile));
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

			AllTime.MonthlyRain.Val = ini.GetValue("Rain", "highmonthlyrainvalue", Cumulus.DefaultHiVal);
			AllTime.MonthlyRain.Ts = ini.GetValue("Rain", "highmonthlyraintime", cumulus.defaultRecordTS);

			AllTime.LongestDryPeriod.Val = ini.GetValue("Rain", "longestdryperiodvalue", Cumulus.DefaultHiVal);
			AllTime.LongestDryPeriod.Ts = ini.GetValue("Rain", "longestdryperiodtime", cumulus.defaultRecordTS);

			AllTime.LongestWetPeriod.Val = ini.GetValue("Rain", "longestwetperiodvalue", Cumulus.DefaultHiVal);
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
				cumulus.LogMessage("Error writing alltime.ini file: " + ex.Message);
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
				cumulus.LogMessage("Error writing MonthlyAlltime.ini file: " + ex.Message);
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
				ThisMonth.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", Cumulus.DefaultHiVal);
				ThisMonth.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				ThisMonth.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", Cumulus.DefaultHiVal);
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
					cumulus.LogMessage("Error writing month.ini file: " + ex.Message);
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
				ThisYear.MonthlyRain.Val = ini.GetValue("Rain", "MonthlyHigh", Cumulus.DefaultHiVal);
				ThisYear.MonthlyRain.Ts = ini.GetValue("Rain", "HMonthlyTime", cumulus.defaultRecordTS);
				ThisYear.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", Cumulus.DefaultHiVal);
				ThisYear.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				ThisYear.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", Cumulus.DefaultHiVal);
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
					cumulus.LogMessage("Error writing year.ini file: " + ex.Message);
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
			sb.Append("&tempin=" + (int)Math.Round(ConvertUserTempToC(IndoorTemperature) * 10));
			sb.Append("&temp=" + (int)Math.Round(ConvertUserTempToC(OutdoorTemperature) * 10));
			sb.Append("&chill=" + (int)Math.Round(ConvertUserTempToC(WindChill) * 10));
			sb.Append("&dew=" + (int)Math.Round(ConvertUserTempToC(OutdoorDewpoint) * 10));
			sb.Append("&heat=" + (int)Math.Round(ConvertUserTempToC(HeatIndex) * 10));

			// Humidity
			sb.Append("&humin=" + IndoorHumidity);
			sb.Append("&hum=" + OutdoorHumidity);

			// Wind
			sb.Append("&wspd=" + (int)Math.Round(ConvertUserWindToMS(WindLatest) * 10));
			sb.Append("&wspdhi=" + (int)Math.Round(ConvertUserWindToMS(RecentMaxGust) * 10));
			sb.Append("&wspdavg=" + (int)Math.Round(ConvertUserWindToMS(WindAverage) * 10));

			// Wind Direction
			sb.Append("&wdir=" + Bearing);
			sb.Append("&wdiravg=" + AvgBearing);

			// Pressure
			sb.Append("&bar=" + (int)Math.Round(ConvertUserPressToMB(Pressure) * 10));

			// rain
			sb.Append("&rain=" + (int)Math.Round(ConvertUserRainToMM(RainToday) * 10));
			sb.Append("&rainrate=" + (int)Math.Round(ConvertUserRainToMM(RainRate) * 10));

			// ET
			if (cumulus.WCloud.SendSolar && cumulus.Manufacturer == cumulus.DAVIS)
			{
				sb.Append("&et=" + (int)Math.Round(ConvertUserRainToMM(ET) * 10));
			}

			// solar
			if (cumulus.WCloud.SendSolar)
			{
				sb.Append("&solarrad=" + (int)Math.Round(SolarRad * 10));
			}

			// uv
			if (cumulus.WCloud.SendUV)
			{
				sb.Append("&uvi=" + (int)Math.Round(UV * 10));
			}

			// aq
			if (cumulus.WCloud.SendAirQuality)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"&pm25={cumulus.airLinkDataOut.pm2p5:F0}");
							sb.Append($"&pm10={cumulus.airLinkDataOut.pm10:F0}");
							sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(cumulus.airLinkDataOut.pm2p5_24hr)}");
						}
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt1:
						sb.Append($"&pm25={AirQuality1:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg1)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt2:
						sb.Append($"&pm25={AirQuality2:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg2)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt3:
						sb.Append($"&pm25={AirQuality3:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg3)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt4:
						sb.Append($"&pm25={AirQuality4:F0}");
						sb.Append($"&aqi={AirQualityIndices.US_EPApm2p5(AirQualityAvg4)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.EcowittCO2:
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
				int wet = 0;

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

				sb.Append($"&leafwet={wet}");
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
				sb.Append("indoortemp=" + ConvertUserTempToC(IndoorTemperature).ToString("F1", InvC));
				sb.Append("&indoorhumidity=" + IndoorHumidity);
				started = true;
			}

			if (cumulus.AWEKAS.SendSoilTemp)
			{
				if (started) sb.Append("&"); else started = true;
				sb.Append("soiltemp1=" + ConvertUserTempToC(SoilTemp1).ToString("F1", InvC));
				sb.Append("&soiltemp2=" + ConvertUserTempToC(SoilTemp2).ToString("F1", InvC));
				sb.Append("&soiltemp3=" + ConvertUserTempToC(SoilTemp3).ToString("F1", InvC));
				sb.Append("&soiltemp4=" + ConvertUserTempToC(SoilTemp4).ToString("F1", InvC));
			}

			if (cumulus.AWEKAS.SendSoilMoisture)
			{
				if (started) sb.Append("&"); else started = true;
				sb.Append("soilmoisture1=" + SoilMoisture1);
				sb.Append("&soilmoisture2=" + SoilMoisture2);
				sb.Append("&soilmoisture3=" + SoilMoisture3);
				sb.Append("&soilmoisture4=" + SoilMoisture4);
			}

			if (cumulus.AWEKAS.SendLeafWetness)
			{
				if (started) sb.Append("&"); else started = true;
				sb.Append("leafwetness1=" + LeafWetness1);
				sb.Append("&leafwetness2=" + LeafWetness2);
				sb.Append("&leafwetness3=" + LeafWetness3);
				sb.Append("&leafwetness4=" + LeafWetness4);
			}

			if (cumulus.AWEKAS.SendAirQuality)
			{
				if (started) sb.Append("&"); else started = true;

				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"AqPM1={cumulus.airLinkDataOut.pm1.ToString("F1", InvC)}");
							sb.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5.ToString("F1", InvC)}");
							sb.Append($"&AqPM10={cumulus.airLinkDataOut.pm10.ToString("F1", InvC)}");
							sb.Append($"&AqPM2.5_avg_24h={cumulus.airLinkDataOut.pm2p5_24hr.ToString("F1", InvC)}");
							sb.Append($"&AqPM10_avg_24h={cumulus.airLinkDataOut.pm10_24hr.ToString("F1", InvC)}");
						}
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt1:
						sb.Append($"AqPM2.5={AirQuality1.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg1.ToString("F1", InvC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt2:
						sb.Append($"AqPM2.5={AirQuality2.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg2.ToString("F1", InvC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt3:
						sb.Append($"AqPM2.5={AirQuality3.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg3.ToString("F1", InvC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt4:
						sb.Append($"AqPM2.5={AirQuality4.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg4.ToString("F1", InvC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.EcowittCO2:
						sb.Append($"AqPM2.5={CO2_pm2p5.ToString("F1", InvC)}");
						sb.Append($"&AqPM2.5_avg_24h={CO2_pm2p5_24h.ToString("F1", InvC)}");
						sb.Append($"&AqPM10={CO2_pm10.ToString("F1", InvC)}");
						sb.Append($"&AqPM10_avg_24h={CO2_pm10_24h.ToString("F1", InvC)}");
						break;
				}
			}

			if (started) sb.Append("&");
			sb.Append("output=json&val=");

			//
			// Start of val
			//
			sb.Append(cumulus.AWEKAS.ID + sep);                                             // 1
			sb.Append(pwstring + sep);                                                      // 2
			sb.Append(timestamp.ToString("dd'.'MM'.'yyyy';'HH':'mm") + sep);                // 3 + 4
			sb.Append(ConvertUserTempToC(OutdoorTemperature).ToString("F1", InvC) + sep);   // 5
			sb.Append(OutdoorHumidity + sep);                                               // 6
			sb.Append(ConvertUserPressToMB(Pressure).ToString("F1", InvC) + sep);           // 7
			sb.Append(ConvertUserRainToMM(RainSinceMidnight).ToString("F1", InvC) + sep);   // 8   - was RainToday in v2
			sb.Append(ConvertUserWindToKPH(WindAverage).ToString("F1", InvC) + sep);        // 9
			sb.Append(AvgBearing + sep);                                                    // 10
			sb.Append(sep + sep + sep);                                                     // 11/12/13 - condition and warning, snow height
			sb.Append(cumulus.AWEKAS.Lang + sep);                                           // 14
			sb.Append(presstrend + sep);                                                    // 15
			sb.Append(ConvertUserWindToKPH(RecentMaxGust).ToString("F1", InvC) + sep);      // 16

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
				sb.Append(ConvertUserTempToC(SoilTemp1).ToString("F1", InvC) + sep);        // 21
			else
				sb.Append(sep);

			sb.Append(ConvertUserRainToMM(RainRate).ToString("F1", InvC) + sep);            // 22
			sb.Append("Cum_" + cumulus.Version + sep);                                      // 23
			sb.Append(sep + sep);                                                           // 24/25 location for mobile
			sb.Append(ConvertUserTempToC(HiLoToday.LowTemp).ToString("F1", InvC) + sep);    // 26
			sb.Append(ConvertUserTempToC(AvgTemp).ToString("F1", InvC) + sep);              // 27
			sb.Append(ConvertUserTempToC(HiLoToday.HighTemp).ToString("F1", InvC) + sep);   // 28
			sb.Append(ConvertUserTempToC(ThisMonth.LowTemp.Val).ToString("F1", InvC) + sep);// 29
			sb.Append(sep);                                                                 // 30 avg temp this month
			sb.Append(ConvertUserTempToC(ThisMonth.HighTemp.Val).ToString("F1", InvC) + sep);// 31
			sb.Append(ConvertUserTempToC(ThisYear.LowTemp.Val).ToString("F1", InvC) + sep); // 32
			sb.Append(sep);                                                                 // 33 avg temp this year
			sb.Append(ConvertUserTempToC(ThisYear.HighTemp.Val).ToString("F1", InvC) + sep);// 34
			sb.Append(HiLoToday.LowHumidity + sep);                                         // 35
			sb.Append(sep);                                                                 // 36 avg hum today
			sb.Append(HiLoToday.HighHumidity + sep);                                        // 37
			sb.Append(ThisMonth.LowHumidity.Val + sep);                                     // 38
			sb.Append(sep);                                                                 // 39 avg hum this month
			sb.Append(ThisMonth.HighHumidity.Val + sep);                                    // 40
			sb.Append(ThisYear.LowHumidity.Val + sep);                                      // 41
			sb.Append(sep);                                                                 // 42 avg hum this year
			sb.Append(ThisYear.HighHumidity.Val + sep);                                     // 43
			sb.Append(ConvertUserPressToMB(HiLoToday.LowPress).ToString("F1", InvC) + sep); // 44
			sb.Append(sep);                                                                 // 45 avg press today
			sb.Append(ConvertUserPressToMB(HiLoToday.HighPress).ToString("F1", InvC) + sep);// 46
			sb.Append(ConvertUserPressToMB(ThisMonth.LowPress.Val).ToString("F1", InvC) + sep); // 47
			sb.Append(sep);                                                                 // 48 avg press this month
			sb.Append(ConvertUserPressToMB(ThisMonth.HighPress.Val).ToString("F1", InvC) + sep); // 49
			sb.Append(ConvertUserPressToMB(ThisYear.LowPress.Val).ToString("F1", InvC) + sep); // 50
			sb.Append(sep);                                                                 // 51 avg press this year
			sb.Append(ConvertUserPressToMB(ThisYear.HighPress.Val).ToString("F1", InvC) + sep); // 52
			sb.Append(sep + sep);                                                           // 53/54 min/avg wind today
			sb.Append(ConvertUserWindToKPH(HiLoToday.HighWind).ToString("F1", InvC) + sep); // 55
			sb.Append(sep + sep);                                                           // 56/57 min/avg wind this month
			sb.Append(ConvertUserWindToKPH(ThisMonth.HighWind.Val).ToString("F1", InvC) + sep); // 58
			sb.Append(sep + sep);                                                           // 59/60 min/avg wind this year
			sb.Append(ConvertUserWindToKPH(ThisYear.HighWind.Val).ToString("F1", InvC) + sep); // 61
			sb.Append(sep + sep);                                                           // 62/63 min/avg gust today
			sb.Append(ConvertUserWindToKPH(HiLoToday.HighGust).ToString("F1", InvC) + sep); // 64
			sb.Append(sep + sep);                                                           // 65/66 min/avg gust this month
			sb.Append(ConvertUserWindToKPH(ThisMonth.HighGust.Val).ToString("F1", InvC) + sep); // 67
			sb.Append(sep + sep);                                                           // 68/69 min/avg gust this year
			sb.Append(ConvertUserWindToKPH(ThisYear.HighGust.Val).ToString("F1", InvC) + sep); // 70
			sb.Append(sep + sep + sep);                                                     // 71/72/73 avg wind bearing today/month/year
			sb.Append(ConvertUserRainToMM(RainLast24Hour).ToString("F1", InvC) + sep);      // 74
			sb.Append(ConvertUserRainToMM(RainMonth).ToString("F1", InvC) + sep);           // 75
			sb.Append(ConvertUserRainToMM(RainYear).ToString("F1", InvC) + sep);            // 76
			sb.Append(sep);                                                                 // 77 avg rain rate today
			sb.Append(ConvertUserRainToMM(HiLoToday.HighRainRate).ToString("F1", InvC) + sep); // 78
			sb.Append(sep);                                                                 // 79 avg rain rate this month
			sb.Append(ConvertUserRainToMM(ThisMonth.HighRainRate.Val).ToString("F1", InvC) + sep); // 80
			sb.Append(sep);                                                                 // 81 avg rain rate this year
			sb.Append(ConvertUserRainToMM(ThisYear.HighRainRate.Val).ToString("F1", InvC) + sep); // 82
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
				Data.Append($"&soiltempf={TempFstr(SoilTemp1)}");
			if (cumulus.Wund.SendSoilTemp2)
				Data.Append($"&soiltempf2={TempFstr(SoilTemp2)}");
			if (cumulus.Wund.SendSoilTemp3)
				Data.Append($"&soiltempf3={TempFstr(SoilTemp3)}");
			if (cumulus.Wund.SendSoilTemp4)
				Data.Append($"&soiltempf4={TempFstr(SoilTemp4)}");

			if (cumulus.Wund.SendSoilMoisture1)
				Data.Append($"&soilmoisture={SoilMoisture1}");
			if (cumulus.Wund.SendSoilMoisture2)
				Data.Append($"&soilmoisture2={SoilMoisture2}");
			if (cumulus.Wund.SendSoilMoisture3)
				Data.Append($"&soilmoisture3={SoilMoisture3}");
			if (cumulus.Wund.SendSoilMoisture4)
				Data.Append($"&soilmoisture4={SoilMoisture4}");

			if (cumulus.Wund.SendLeafWetness1)
				Data.Append($"&leafwetness={LeafWetness1}");
			if (cumulus.Wund.SendLeafWetness2)
				Data.Append($"&leafwetness2={LeafWetness2}");

			if (cumulus.Wund.SendAirQuality && cumulus.StationOptions.PrimaryAqSensor > (int)Cumulus.PrimaryAqSensor.Undefined)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor:
						if (cumulus.airLinkDataOut != null)
						{
							Data.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5:F1}&AqPM10={cumulus.airLinkDataOut.pm10.ToString("F1", invC)}");
						}
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt1:
						Data.Append($"&AqPM2.5={AirQuality1.ToString("F1", invC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt2:
						Data.Append($"&AqPM2.5={AirQuality2.ToString("F1", invC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt3:
						Data.Append($"&AqPM2.5={AirQuality3.ToString("F1", invC)}");
						break;
					case (int)Cumulus.PrimaryAqSensor.Ecowitt4:
						Data.Append($"&AqPM2.5={AirQuality4.ToString("F1", invC)}");
						break;
				}
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
			double avgwind = totalwind / numvalues * cumulus.Calib.WindSpeed.Mult;

			maxwind *= cumulus.Calib.WindGust.Mult;
			minwind *= cumulus.Calib.WindGust.Mult;


			StringBuilder URL = new StringBuilder("http://www.windguru.cz/upload/api.php?", 1024);

			URL.Append("uid=" + HttpUtility.UrlEncode(cumulus.WindGuru.ID));
			URL.Append("&salt=" + salt);
			URL.Append("&hash=" + hash);
			URL.Append("&interval=" + cumulus.WindGuru.Interval * 60);
			URL.Append("&wind_avg=" + ConvertUserWindToKnots(avgwind).ToString("F1", InvC));
			URL.Append("&wind_max=" + ConvertUserWindToKnots(maxwind).ToString("F1", InvC));
			URL.Append("&wind_min=" + ConvertUserWindToKnots(minwind).ToString("F1", InvC));
			URL.Append("&wind_direction=" + AvgBearing);
			URL.Append("&temperature=" + ConvertUserTempToC(OutdoorTemperature).ToString("F1", InvC));
			URL.Append("&rh=" + OutdoorHumidity);
			URL.Append("&mslp=" + ConvertUserPressureToHPa(Pressure).ToString("F1", InvC));
			if (cumulus.WindGuru.SendRain)
			{
				URL.Append("&precip=" + ConvertUserRainToMM(RainLastHour).ToString("F1", InvC));
				URL.Append("&precip_interval=3600");
			}

			return URL.ToString();
		}

		public string GetOpenWeatherMapData(DateTime timestamp)
		{
			StringBuilder sb = new StringBuilder($"[{{\"station_id\":\"{cumulus.OpenWeatherMap.ID}\",");
			var invC = new CultureInfo("");

			sb.Append($"\"dt\":{Utils.ToUnixTime(timestamp)},");
			sb.Append($"\"temperature\":{Math.Round(ConvertUserTempToC(OutdoorTemperature), 1).ToString(invC)},");
			sb.Append($"\"wind_deg\":{AvgBearing},");
			sb.Append($"\"wind_speed\":{Math.Round(ConvertUserWindToMS(WindAverage), 1).ToString(invC)},");
			sb.Append($"\"wind_gust\":{Math.Round(ConvertUserWindToMS(RecentMaxGust), 1).ToString(invC)},");
			sb.Append($"\"pressure\":{Math.Round(ConvertUserPressureToHPa(Pressure), 1).ToString(invC)},");
			sb.Append($"\"humidity\":{OutdoorHumidity},");
			sb.Append($"\"rain_1h\":{Math.Round(ConvertUserRainToMM(RainLastHour), 1).ToString(invC)},");
			sb.Append($"\"rain_24h\":{Math.Round(ConvertUserRainToMM(RainLast24Hour), 1).ToString(invC)}");
			sb.Append("}]");

			return sb.ToString();
		}

		private string PressINstr(double pressure)
		{
			return ConvertUserPressToIN(pressure).ToString("F3", CultureInfo.InvariantCulture);
		}

		private string PressPAstr(double pressure)
		{
			// return value to 0.1 hPa
			return (ConvertUserPressToMB(pressure) / 100).ToString("F4", CultureInfo.InvariantCulture);
		}

		private string WindMPHStr(double wind)
		{
			var windMPH = ConvertUserWindToMPH(wind);
			if (cumulus.StationOptions.RoundWindSpeed)
				windMPH = Math.Round(windMPH);

			return windMPH.ToString("F1", CultureInfo.InvariantCulture);
		}

		private string WindMSStr(double wind)
		{
			var windMS = ConvertUserWindToMS(wind);
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
			return ConvertUserRainToIn(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to mm for APIs etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		private string RainMMstr(double rain)
		{
			return ConvertUserRainToMM(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert temp in user units to F for WU etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		private string TempFstr(double temp)
		{
			return ConvertUserTempToF(temp).ToString("F1", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert temp in user units to C for APIs etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		private string TempCstr(double temp)
		{
			return ConvertUserTempToC(temp).ToString("F1", CultureInfo.InvariantCulture);
		}

		public string GetPWSURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			StringBuilder URL = new StringBuilder("http://www.pwsweather.com/pwsupdate/pwsupdate.php?ID=", 1024);

			pwstring = "&PASSWORD=" + cumulus.PWS.PW;
			URL.Append(cumulus.PWS.ID + pwstring);
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

			Data.Append("&softwaretype=Cumulus%20v" + cumulus.Version);
			Data.Append("&action=updateraw");

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}

		public double ConvertUserRainToIN(double rain)
		{
			if (cumulus.Units.Rain == 0)
			{
				return rain * 0.0393700787;
			}
			else
			{
				return rain;
			}
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
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AllTime.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(",");
			json.Append(alltimejsonformat(AllTime.LongestDryPeriod, "days", "f0", "D"));
			json.Append(",");
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
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyHumRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyPressRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyWindRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyRainRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthlyjsonformat(MonthlyRecs[month].HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(",");
			json.Append(monthlyjsonformat(MonthlyRecs[month].LongestDryPeriod, "days", "f0", "D"));
			json.Append(",");
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
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisMonth.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisMonth.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisMonth.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(ThisMonth.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(",");
			//json.Append(monthyearjsonformat(ThisMonth.WetMonth.Desc, month, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			//json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LongestDryPeriod, "days", "f0", "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisMonth.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(ThisYear.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisYear.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisYear.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(ThisYear.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(ThisYear.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LongestDryPeriod, "days", "f0", "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(ThisYear.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraTemp()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 11; sensor++)
			{
				json.Append("[\"");
				json.Append(cumulus.ExtraTempCaptions[sensor]);
				json.Append("\",\"");
				json.Append(ExtraTemp[sensor].ToString(cumulus.TempFormat));
				json.Append("\",\"&deg;");
				json.Append(cumulus.Units.TempText[1].ToString());
				json.Append("\"]");

				if (sensor < 10)
				{
					json.Append(",");
				}
			}

			json.Append("]}");
			return json.ToString();
		}

		public string GetUserTemp()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 9; sensor++)
			{
				json.Append("[\"");
				json.Append(cumulus.UserTempCaptions[sensor]);
				json.Append("\",\"");
				json.Append(UserTemp[sensor].ToString(cumulus.TempFormat));
				json.Append("\",\"&deg;");
				json.Append(cumulus.Units.TempText[1].ToString());
				json.Append("\"]");

				if (sensor < 8)
				{
					json.Append(",");
				}
			}

			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraHum()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 11; sensor++)
			{
				json.Append("[\"");
				json.Append(cumulus.ExtraHumCaptions[sensor]);
				json.Append("\",\"");
				json.Append(ExtraHum[sensor].ToString(cumulus.HumFormat));
				json.Append("\",\"%\"]");

				if (sensor < 10)
				{
					json.Append(",");
				}
			}

			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraDew()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (int sensor = 1; sensor < 11; sensor++)
			{
				json.Append("[\"");
				json.Append(cumulus.ExtraDPCaptions[sensor]);
				json.Append("\",\"");
				json.Append(ExtraDewPoint[sensor].ToString(cumulus.TempFormat));
				json.Append("\",\"&deg;");
				json.Append(cumulus.Units.TempText[1].ToString());
				json.Append("\"]");

				if (sensor < 10)
				{
					json.Append(",");
				}
			}

			json.Append("]}");
			return json.ToString();
		}

		public string GetSoilTemp()
		{
			var json = new StringBuilder("{\"data\":[", 2048);

			json.Append($"[\"{cumulus.SoilTempCaptions[1]}\",\"{SoilTemp1.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[2]}\",\"{SoilTemp2.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[3]}\",\"{SoilTemp3.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[4]}\",\"{SoilTemp4.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[5]}\",\"{SoilTemp5.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[6]}\",\"{SoilTemp6.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[7]}\",\"{SoilTemp7.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[8]}\",\"{SoilTemp8.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[9]}\",\"{SoilTemp9.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[10]}\",\"{SoilTemp10.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[11]}\",\"{SoilTemp11.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[12]}\",\"{SoilTemp12.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[13]}\",\"{SoilTemp13.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[14]}\",\"{SoilTemp14.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[15]}\",\"{SoilTemp15.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[16]}\",\"{SoilTemp16.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetSoilMoisture()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append($"[\"{cumulus.SoilMoistureCaptions[1]}\",\"{SoilMoisture1:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[2]}\",\"{SoilMoisture2:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[3]}\",\"{SoilMoisture3:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[4]}\",\"{SoilMoisture4:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[5]}\",\"{SoilMoisture5:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[6]}\",\"{SoilMoisture6:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[7]}\",\"{SoilMoisture7:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[8]}\",\"{SoilMoisture8:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[9]}\",\"{SoilMoisture9:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[10]}\",\"{SoilMoisture10:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[11]}\",\"{SoilMoisture11:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[12]}\",\"{SoilMoisture12:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[13]}\",\"{SoilMoisture13:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[14]}\",\"{SoilMoisture14:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[15]}\",\"{SoilMoisture15:F0}\",\"{cumulus.SoilMoistureUnitText}\"],");
			json.Append($"[\"{cumulus.SoilMoistureCaptions[16]}\",\"{SoilMoisture16:F0}\",\"{cumulus.SoilMoistureUnitText}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirQuality()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append($"[\"{cumulus.AirQualityCaptions[1]}\",\"{AirQuality1:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityCaptions[2]}\",\"{AirQuality2:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityCaptions[3]}\",\"{AirQuality3:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityCaptions[4]}\",\"{AirQuality4:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[1]}\",\"{AirQualityAvg1:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[2]}\",\"{AirQualityAvg2:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[3]}\",\"{AirQualityAvg3:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[4]}\",\"{AirQualityAvg4:F1}\",\"{cumulus.AirQualityUnitText}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetCO2sensor()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append($"[\"{cumulus.CO2_CurrentCaption}\",\"{CO2}\",\"{cumulus.CO2UnitText}\"],");
			json.Append($"[\"{cumulus.CO2_24HourCaption}\",\"{CO2_24h}\",\"{cumulus.CO2UnitText}\"],");
			json.Append($"[\"{cumulus.CO2_pm2p5Caption}\",\"{CO2_pm2p5:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.CO2_pm2p5_24hrCaption}\",\"{CO2_pm2p5_24h:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.CO2_pm10Caption}\",\"{CO2_pm10:F1}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.CO2_pm10_24hrCaption}\",\"{CO2_pm10_24h:F1}\",\"{cumulus.AirQualityUnitText}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLightning()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"Distance to last strike\",\"{LightningDistance.ToString(cumulus.WindRunFormat)}\",\"{cumulus.Units.WindRunText}\"],");
			json.Append($"[\"Time of last strike\",\"{LightningTime}\",\"\"],");
			json.Append($"[\"Number of strikes today\",\"{LightningStrikesToday}\",\"\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"{cumulus.LeafTempCaptions[1]}\",\"{LeafTemp1.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.LeafTempCaptions[2]}\",\"{LeafTemp2.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[1]}\",\"{LeafWetness1}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[2]}\",\"{LeafWetness2}\",\"{cumulus.LeafWetnessUnitText}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf4()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"{cumulus.LeafTempCaptions[1]}\",\"{LeafTemp1.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.LeafTempCaptions[2]}\",\"{LeafTemp2.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[1]}\",\"{LeafWetness1}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[2]}\",\"{LeafWetness2}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[3]}\",\"{LeafWetness3}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[4]}\",\"{LeafWetness4}\",\"{cumulus.LeafWetnessUnitText}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf8()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"{cumulus.LeafWetnessCaptions[1]}\",\"{LeafWetness1}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[2]}\",\"{LeafWetness2}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[3]}\",\"{LeafWetness3}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[4]}\",\"{LeafWetness4}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[5]}\",\"{LeafWetness5}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[6]}\",\"{LeafWetness6}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[7]}\",\"{LeafWetness7}\",\"{cumulus.LeafWetnessUnitText}\"],");
			json.Append($"[\"{cumulus.LeafWetnessCaptions[8]}\",\"{LeafWetness8}\",\"{cumulus.LeafWetnessUnitText}\"]");
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
			json.Append(HiLoToday.HighTempTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighTempTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Low Temperature\",\"");
			json.Append(HiLoToday.LowTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowTempTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowTempTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Temperature Range\",\"");
			json.Append((HiLoToday.HighTemp - HiLoToday.LowTemp).ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\",\"");
			json.Append((HiLoYest.HighTemp - HiLoYest.LowTemp).ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append("&nbsp;\"],");

			json.Append("[\"High Apparent Temperature\",\"");
			json.Append(HiLoToday.HighAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighAppTempTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighAppTempTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Low Apparent Temperature\",\"");
			json.Append(HiLoToday.LowAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowAppTempTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowAppTemp.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowAppTempTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"High Feels Like\",\"");
			json.Append(HiLoToday.HighFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighFeelsLikeTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighFeelsLikeTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Low Feels Like\",\"");
			json.Append(HiLoToday.LowFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowFeelsLikeTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowFeelsLike.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowFeelsLikeTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"High Humidex\",\"");
			json.Append(HiLoToday.HighHumidex.ToString(cumulus.TempFormat));
			json.Append("\",\"");
			json.Append(HiLoToday.HighHumidexTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighHumidex.ToString(cumulus.TempFormat));
			json.Append("\",\"");
			json.Append(HiLoYest.HighHumidexTime.ToShortTimeString());
			json.Append(closeStr);
			json.Append("[\"High Dew Point\",\"");
			json.Append(HiLoToday.HighDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighDewPointTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighDewPointTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Low Dew Point\",\"");
			json.Append(HiLoToday.LowDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowDewPointTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowDewPoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowDewPointTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Low Wind Chill\",\"");
			json.Append(HiLoToday.LowWindChill.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowWindChillTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowWindChill.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowWindChillTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"High Heat Index\",\"");
			json.Append(HiLoToday.HighHeatIndex.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighHeatIndexTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighHeatIndex.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighHeatIndexTime.ToShortTimeString());
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
			json.Append(HiLoToday.HighHumidityTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoYest.HighHumidityTime.ToShortTimeString());
			json.Append("\"],");

			json.Append("[\"Low Humidity\",\"");
			json.Append(HiLoToday.LowHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoToday.LowHumidityTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowHumidity.ToString(cumulus.HumFormat));
			json.Append(unitStr);
			json.Append(HiLoYest.LowHumidityTime.ToShortTimeString());
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
			json.Append(HiLoToday.HighRainRateTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighRainRate.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(HiLoYest.HighRainRateTime.ToShortTimeString());
			json.Append("\"],");

			json.Append("[\"High Hourly Rain\",\"");
			json.Append(HiLoToday.HighHourlyRain.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoToday.HighHourlyRainTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighHourlyRain.ToString(cumulus.RainFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.HighHourlyRainTime.ToShortTimeString());
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
			json.Append(HiLoToday.HighGustTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighGust.ToString(cumulus.WindFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoYest.HighGustTime.ToShortTimeString());
			json.Append("\"],");

			json.Append("[\"Highest Speed\",\"");
			json.Append(HiLoToday.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoToday.HighWindTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.Units.WindText);
			json.Append(sepStr);
			json.Append(HiLoYest.HighWindTime.ToShortTimeString());
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
			json.Append(HiLoToday.HighPressTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.HighPressTime.ToShortTimeString());
			json.Append("\"],");

			json.Append("[\"Low Pressure\",\"");
			json.Append(HiLoToday.LowPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoToday.LowPressTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowPress.ToString(cumulus.PressFormat));
			json.Append(unitStr);
			json.Append(sepStr);
			json.Append(HiLoYest.LowPressTime.ToShortTimeString());
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
			json.Append(HiLoToday.HighSolarTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighSolar.ToString("F0"));
			json.Append("&nbsp;W/m2");
			json.Append(sepStr);
			json.Append(HiLoYest.HighSolarTime.ToShortTimeString());
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
		public string GetDayfile(string draw, int start, int length)
		{
			try
			{
				//var total = File.ReadLines(cumulus.DayFile).Count();
				var allLines = File.ReadAllLines(cumulus.DayFileName);
				var total = allLines.Length;
				var lines = allLines.Skip(start).Take(length);

				var json = new StringBuilder(350 * lines.Count());

				json.Append("{\"draw\":" + draw);
				json.Append(",\"recordsTotal\":" + total);
				json.Append(",\"recordsFiltered\":" + total);
				json.Append(",\"data\":[");

				//var lines = File.ReadLines(cumulus.DayFile).Skip(start).Take(length);

				var lineNum = start + 1; // Start is zero relative

				foreach (var line in lines)
				{
					var sep = Utils.GetLogFileSeparator(line, cumulus.ListSeparator);
					var fields = line.Split(sep[0]);
					var numFields = fields.Length;
					json.Append($"[{lineNum++},");
					for (var i = 0; i < numFields; i++)
					{
						json.Append($"\"{fields[i]}\"");
						if (i < fields.Length - 1)
						{
							json.Append(",");
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

				// trim last ","
				json.Length--;
				json.Append("]}");

				return json.ToString();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.ToString());
			}

			return "";
		}

		internal string GetDiaryData(string date)
		{

			StringBuilder json = new StringBuilder("{\"entry\":\"", 1024);

			var result = cumulus.DiaryDB.Query<DiaryData>("select * from DiaryData where date(Timestamp,'utc') = ? order by Timestamp limit 1", date);

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

		// Fetches all days in the required month that have a diary entry
		//internal string GetDiarySummary(string year, string month)
		internal string GetDiarySummary()
		{
			var json = new StringBuilder(512);
			//var result = cumulus.DiaryDB.Query<DiaryData>("select Timestamp from DiaryData where strftime('%Y', Timestamp) = ? and strftime('%m', Timestamp) = ? order by Timestamp", year, month);
			var result = cumulus.DiaryDB.Query<DiaryData>("select Timestamp from DiaryData order by Timestamp");

			if (result.Count > 0)
			{
				json.Append("{\"dates\":[");
				for (int i = 0; i < result.Count; i++)
				{
					json.Append("\"");
					json.Append(result[i].Timestamp.ToUniversalTime().ToString("yyyy-MM-dd"));
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
		/// <param name="date"></param>
		/// <param name="draw"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <param name="extra"></param>
		/// <returns></returns>
		public string GetLogfile(string from, string to, bool extra)
		{
			try
			{
				// date will be in format "yyyy-mm-dd"
				var stDate = from.Split('-');
				var enDate = to.Split('-');

				var ts = new DateTime(int.Parse(stDate[0]), int.Parse(stDate[1]), int.Parse(stDate[2]));
				var te = new DateTime(int.Parse(enDate[0]), int.Parse(enDate[1]), int.Parse(enDate[2]));
				te = te.AddDays(1);
				var fileDate = new DateTime(ts.Year, ts.Month, 1);

				var logfile = extra ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetLogFileName(fileDate);
				var numFields = extra ? Cumulus.NumExtraLogFileFields : Cumulus.NumLogFileFields;

				if (!File.Exists(logfile))
				{
					cumulus.LogMessage($"GetLogFile: Error, file does not exist: {logfile}");
					return "";
				}

				var watch = System.Diagnostics.Stopwatch.StartNew();

				var finished = false;
				var total = 0;

				var json = new StringBuilder(220 * 2500);
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

								total++;
							}
						}

					}
					else
					{
						cumulus.LogDebugMessage($"GetLogfile: Log file  not found - {logfile}");
					}

					// have we run out of log entries?
					if (te <= fileDate.AddMonths(1))
					{
						finished = true;
						cumulus.LogDebugMessage("GetLogfile: Finished processing log files");
					}

					if (!finished)
					{
						cumulus.LogDebugMessage($"GetLogfile: Finished processing log file - {logfile}");
						fileDate = fileDate.AddMonths(1);
						logfile = extra ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetLogFileName(fileDate);
					}

				}
				// trim trailing ","
				if (total > 0)
					json.Length--;
				json.Append("],\"recordsTotal\":");
				json.Append(total);
				json.Append('}');

				watch.Stop();
				var elapsed = watch.ElapsedMilliseconds;
				cumulus.LogDebugMessage($"GetLogfile: Logfiles parse = {elapsed} ms");

				return json.ToString();
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.ToString());
			}

			return "";
		}

		public string GetUnits()
		{
			return $"{{\"temp\":\"{cumulus.Units.TempText[1]}\",\"wind\":\"{cumulus.Units.WindText}\",\"rain\":\"{cumulus.Units.RainText}\",\"press\":\"{cumulus.Units.PressText}\"}}";
		}

		public string GetGraphConfig()
		{
			var json = new StringBuilder(200);
			json.Append("{");
			json.Append($"\"temp\":{{\"units\":\"{cumulus.Units.TempText[1]}\",\"decimals\":{cumulus.TempDPlaces}}},");
			json.Append($"\"wind\":{{\"units\":\"{cumulus.Units.WindText}\",\"decimals\":{cumulus.WindAvgDPlaces},\"rununits\":\"{cumulus.Units.WindRunText}\"}},");
			json.Append($"\"rain\":{{\"units\":\"{cumulus.Units.RainText}\",\"decimals\":{cumulus.RainDPlaces}}},");
			json.Append($"\"press\":{{\"units\":\"{cumulus.Units.PressText}\",\"decimals\":{cumulus.PressDPlaces}}},");
			json.Append($"\"hum\":{{\"decimals\":{cumulus.HumDPlaces}}},");
			json.Append($"\"uv\":{{\"decimals\":{cumulus.UVDPlaces}}}");
			json.Append("}");
			return json.ToString();
		}

		public string GetAvailGraphData()
		{
			var json = new StringBuilder(200);

			// Temp values
			json.Append("{\"Temperature\":[");

			if (cumulus.GraphOptions.TempVisible)
				json.Append("\"Temperature\",");

			if (cumulus.GraphOptions.InTempVisible)
				json.Append("\"Indoor Temp\",");

			if (cumulus.GraphOptions.HIVisible)
				json.Append("\"Heat Index\",");

			if (cumulus.GraphOptions.DPVisible)
				json.Append("\"Dew Point\",");

			if (cumulus.GraphOptions.WCVisible)
				json.Append("\"Wind Chill\",");

			if (cumulus.GraphOptions.AppTempVisible)
				json.Append("\"Apparent Temp\",");

			if (cumulus.GraphOptions.FeelsLikeVisible)
				json.Append("\"Feels Like\",");

			//if (cumulus.GraphOptions.HumidexVisible)
			//	json.Append("\"Humidex\",");

			if (json[json.Length - 1] == ',')
				json.Length--;

			// humidity values
			json.Append("],\"Humidity\":[");

			if (cumulus.GraphOptions.OutHumVisible)
				json.Append("\"Humidity\",");

			if (cumulus.GraphOptions.InHumVisible)
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

			if (cumulus.GraphOptions.DailyAvgTempVisible || cumulus.GraphOptions.DailyMaxTempVisible || cumulus.GraphOptions.DailyMinTempVisible)
			{
				json.Append(",\"DailyTemps\":[");

				if (cumulus.GraphOptions.DailyAvgTempVisible)
					json.Append("\"AvgTemp\",");
				if (cumulus.GraphOptions.DailyMaxTempVisible)
					json.Append("\"MaxTemp\",");
				if (cumulus.GraphOptions.DailyMinTempVisible)
					json.Append("\"MinTemp\",");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append("]");
			}


			// solar values
			if (cumulus.GraphOptions.SolarVisible || cumulus.GraphOptions.UVVisible)
			{
				json.Append(",\"Solar\":[");

				if (cumulus.GraphOptions.SolarVisible)
					json.Append("\"Solar Rad\",");

				if (cumulus.GraphOptions.UVVisible)
					json.Append("\"UV Index\",");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append("]");
			}

			// Sunshine
			if (cumulus.GraphOptions.SunshineVisible)
			{
				json.Append(",\"Sunshine\":[\"sunhours\"]");
			}

			// air quality
			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (cumulus.StationOptions.PrimaryAqSensor > (int)Cumulus.PrimaryAqSensor.Undefined
				&& cumulus.StationOptions.PrimaryAqSensor != (int)Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				json.Append(",\"AirQuality\":[");
				json.Append("\"PM 2.5\"");

				// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
				if (cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
					cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.AirLinkIndoor ||
					cumulus.StationOptions.PrimaryAqSensor == (int)Cumulus.PrimaryAqSensor.EcowittCO2)
				{
					json.Append(",\"PM 10\"");
				}
				json.Append("]");
			}

			// Degree Days
			if (cumulus.GraphOptions.GrowingDegreeDaysVisible1 || cumulus.GraphOptions.GrowingDegreeDaysVisible2)
			{
				json.Append(",\"DegreeDays\":[");
				if (cumulus.GraphOptions.GrowingDegreeDaysVisible1)
					json.Append("\"GDD1\",");

				if (cumulus.GraphOptions.GrowingDegreeDaysVisible2)
					json.Append("\"GDD2\"");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append("]");
			}

			// Temp Sum
			if (cumulus.GraphOptions.TempSumVisible0 || cumulus.GraphOptions.TempSumVisible1 || cumulus.GraphOptions.TempSumVisible2)
			{
				json.Append(",\"TempSum\":[");
				if (cumulus.GraphOptions.TempSumVisible0)
					json.Append("\"Sum0\",");
				if (cumulus.GraphOptions.TempSumVisible1)
					json.Append("\"Sum1\",");
				if (cumulus.GraphOptions.TempSumVisible2)
					json.Append("\"Sum2\"");

				if (json[json.Length - 1] == ',')
					json.Length--;

				json.Append("]");
			}

			json.Append("}");
			return json.ToString();
		}


		public string GetSelectaChartOptions()
		{
			return JsonSerializer.SerializeToString(cumulus.SelectaChartOptions);
		}


		public string GetDailyRainGraphData()
		{
			var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);

			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"dailyrain\":[", 10000);

			var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();
			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{DateTimeToUnix(data[i].Date) * 1000},{data[i].TotalRain.ToString(cumulus.RainFormat, InvC)}]");

				if (i < data.Count - 1)
					sb.Append(",");
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetSunHoursGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{", 10000);
			if (cumulus.GraphOptions.SunshineVisible)
			{
				var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);
				var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();

				sb.Append("\"sunhours\":[");
				for (var i = 0; i < data.Count; i++)
				{
					var sunhrs = data[i].SunShineHours >= 0 ? data[i].SunShineHours : 0;
					sb.Append($"[{DateTimeToUnix(data[i].Date) * 1000},{sunhrs.ToString(cumulus.SunFormat, InvC)}]");

					if (i < data.Count - 1)
						sb.Append(",");
				}
				sb.Append("]");
			}
			sb.Append("}");
			return sb.ToString();
		}

		public string GetDailyTempGraphData()
		{
			var InvC = new CultureInfo("");
			var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);
			var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();
			var append = false;
			StringBuilder sb = new StringBuilder("{");

			if (cumulus.GraphOptions.DailyMinTempVisible)
			{
				sb.Append("\"mintemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(data[i].Date) * 1000},{data[i].LowTemp.ToString(cumulus.TempFormat, InvC)}]");
					if (i < data.Count - 1)
						sb.Append(",");
				}

				sb.Append("]");
				append = true;
			}

			if (cumulus.GraphOptions.DailyMaxTempVisible)
			{
				if (append)
					sb.Append(",");

				sb.Append("\"maxtemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(data[i].Date) * 1000},{data[i].HighTemp.ToString(cumulus.TempFormat, InvC)}]");
					if (i < data.Count - 1)
						sb.Append(",");
				}

				sb.Append("]");
				append = true;
			}

			if (cumulus.GraphOptions.DailyAvgTempVisible)
			{
				if (append)
					sb.Append(",");

				sb.Append("\"avgtemp\":[");
				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(data[i].Date) * 1000},{data[i].AvgTemp.ToString(cumulus.TempFormat, InvC)}]");
					if (i < data.Count - 1)
						sb.Append(",");
				}

				sb.Append("]");
			}

			sb.Append("}");
			return sb.ToString();
		}

		public string GetAllDailyTempGraphData()
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
				var len = DayFile.Count() - 1;

				for (var i = 0; i < DayFile.Count(); i++)
				{
					var recDate = DateTimeToUnix(DayFile[i].Date) * 1000;
					// lo temp
					if (cumulus.GraphOptions.DailyMinTempVisible)
						minTemp.Append($"[{recDate},{DayFile[i].LowTemp.ToString(cumulus.TempFormat, InvC)}]");
					// hi temp
					if (cumulus.GraphOptions.DailyMaxTempVisible)
						maxTemp.Append($"[{recDate},{DayFile[i].HighTemp.ToString(cumulus.TempFormat, InvC)}]");
					// avg temp
					if (cumulus.GraphOptions.DailyAvgTempVisible)
						avgTemp.Append($"[{recDate},{DayFile[i].AvgTemp.ToString(cumulus.TempFormat, InvC)}]");

					if (i < len)
					{
						minTemp.Append(",");
						maxTemp.Append(",");
						avgTemp.Append(",");
					}

					if (cumulus.GraphOptions.HIVisible)
					{
						// hi heat index
						if (DayFile[i].HighHeatIndex > -999)
							heatIdx.Append($"[{recDate},{DayFile[i].HighHeatIndex.ToString(cumulus.TempFormat, InvC)}]");
						else
							heatIdx.Append($"[{recDate},null]");

						if (i < len)
							heatIdx.Append(",");
					}
					if (cumulus.GraphOptions.AppTempVisible)
					{
						// hi app temp
						if (DayFile[i].HighAppTemp > -999)
							maxApp.Append($"[{recDate},{DayFile[i].HighAppTemp.ToString(cumulus.TempFormat, InvC)}]");
						else
							maxApp.Append($"[{recDate},null]");

						// lo app temp
						if (DayFile[i].LowAppTemp < 999)
							minApp.Append($"[{recDate},{DayFile[i].LowAppTemp.ToString(cumulus.TempFormat, InvC)}]");
						else
							minApp.Append($"[{recDate},null]");

						if (i < len)
						{
							maxApp.Append(",");
							minApp.Append(",");
						}
					}
					// lo wind chill
					if (cumulus.GraphOptions.WCVisible)
					{
						if (DayFile[i].LowWindChill < 999)
							windChill.Append($"[{recDate},{DayFile[i].LowWindChill.ToString(cumulus.TempFormat, InvC)}]");
						else
							windChill.Append($"[{recDate},null]");

						if (i < len)
							windChill.Append(",");
					}

					if (cumulus.GraphOptions.DPVisible)
					{
						// hi dewpt
						if (DayFile[i].HighDewPoint > -999)
							maxDew.Append($"[{recDate},{DayFile[i].HighDewPoint.ToString(cumulus.TempFormat, InvC)}]");
						else
							maxDew.Append($"[{recDate},null]");

						// lo dewpt
						if (DayFile[i].LowDewPoint < 999)
							minDew.Append($"[{recDate},{DayFile[i].LowDewPoint.ToString(cumulus.TempFormat, InvC)}]");
						else
							minDew.Append($"[{recDate},null]");

						if (i < len)
						{
							maxDew.Append(",");
							minDew.Append(",");
						}
					}

					if (cumulus.GraphOptions.FeelsLikeVisible)
					{
						// hi feels like
						if (DayFile[i].HighFeelsLike > -999)
							maxFeels.Append($"[{recDate},{DayFile[i].HighFeelsLike.ToString(cumulus.TempFormat, InvC)}]");
						else
							maxFeels.Append($"[{recDate},null]");

						// lo feels like
						if (DayFile[i].LowFeelsLike < 999)
							minFeels.Append($"[{recDate},{DayFile[i].LowFeelsLike.ToString(cumulus.TempFormat, InvC)}]");
						else
							minFeels.Append($"[{recDate},null]");

						if (i < len)
						{
							maxFeels.Append(",");
							minFeels.Append(",");
						}
					}

					if (cumulus.GraphOptions.HumidexVisible)
					{
						// hi humidex
						if (DayFile[i].HighHumidex > -999)
							humidex.Append($"[{recDate},{DayFile[i].HighHumidex.ToString(cumulus.TempFormat, InvC)}]");
						else
							humidex.Append($"[{recDate},null]");

						if (i < len)
							humidex.Append(",");
					}
				}
			}
			if (cumulus.GraphOptions.DailyMinTempVisible)
				sb.Append("\"minTemp\":" + minTemp.ToString() + "],");
			if (cumulus.GraphOptions.DailyMaxTempVisible)
				sb.Append("\"maxTemp\":" + maxTemp.ToString() + "],");
			if (cumulus.GraphOptions.DailyAvgTempVisible)
				sb.Append("\"avgTemp\":" + avgTemp.ToString() + "],");
			if (cumulus.GraphOptions.HIVisible)
				sb.Append("\"heatIndex\":" + heatIdx.ToString() + "],");
			if (cumulus.GraphOptions.AppTempVisible)
			{
				sb.Append("\"maxApp\":" + maxApp.ToString() + "],");
				sb.Append("\"minApp\":" + minApp.ToString() + "],");
			}
			if (cumulus.GraphOptions.WCVisible)
				sb.Append("\"windChill\":" + windChill.ToString() + "],");
			if (cumulus.GraphOptions.DPVisible)
			{
				sb.Append("\"maxDew\":" + maxDew.ToString() + "],");
				sb.Append("\"minDew\":" + minDew.ToString() + "],");
			}
			if (cumulus.GraphOptions.FeelsLikeVisible)
			{
				sb.Append("\"maxFeels\":" + maxFeels.ToString() + "],");
				sb.Append("\"minFeels\":" + minFeels.ToString() + "],");
			}
			if (cumulus.GraphOptions.HumidexVisible)
				sb.Append("\"humidex\":" + humidex.ToString() + "],");

			sb.Length--;
			sb.Append("}");

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
				var len = DayFile.Count() - 1;

				for (var i = 0; i < DayFile.Count(); i++)
				{
					var recDate = DateTimeToUnix(DayFile[i].Date) * 1000;

					// hi gust
					maxGust.Append($"[{recDate},{DayFile[i].HighGust.ToString(cumulus.WindFormat, InvC)}]");
					// hi wind run
					windRun.Append($"[{recDate},{DayFile[i].WindRun.ToString(cumulus.WindRunFormat, InvC)}]");
					// hi wind
					maxWind.Append($"[{recDate},{DayFile[i].HighAvgWind.ToString(cumulus.WindAvgFormat, InvC)}]");

					if (i < len)
					{
						maxGust.Append(",");
						windRun.Append(",");
						maxWind.Append(",");
					}
				}
			}
			sb.Append("\"maxGust\":" + maxGust.ToString() + "],");
			sb.Append("\"windRun\":" + windRun.ToString() + "],");
			sb.Append("\"maxWind\":" + maxWind.ToString() + "]");
			sb.Append("}");

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
				var len = DayFile.Count() - 1;

				for (var i = 0; i < DayFile.Count(); i++)
				{

					long recDate = DateTimeToUnix(DayFile[i].Date) * 1000;

					// hi rain rate
					maxRRate.Append($"[{recDate},{DayFile[i].HighRainRate.ToString(cumulus.RainFormat, InvC)}]");
					// total rain
					rain.Append($"[{recDate},{DayFile[i].TotalRain.ToString(cumulus.RainFormat, InvC)}]");

					if (i < len)
					{
						maxRRate.Append(",");
						rain.Append(",");
					}
				}
			}
			sb.Append("\"maxRainRate\":" + maxRRate.ToString() + "],");
			sb.Append("\"rain\":" + rain.ToString() + "]");
			sb.Append("}");

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
				var len = DayFile.Count() - 1;

				for (var i = 0; i < DayFile.Count(); i++)
				{

					long recDate = DateTimeToUnix(DayFile[i].Date) * 1000;

					// lo baro
					minBaro.Append($"[{recDate},{DayFile[i].LowPress.ToString(cumulus.PressFormat, InvC)}]");
					// hi baro
					maxBaro.Append($"[{recDate},{DayFile[i].HighPress.ToString(cumulus.PressFormat, InvC)}]");

					if (i < len)
					{
						maxBaro.Append(",");
						minBaro.Append(",");
					}
				}
			}
			sb.Append("\"minBaro\":" + minBaro.ToString() + "],");
			sb.Append("\"maxBaro\":" + maxBaro.ToString() + "]");
			sb.Append("}");

			return sb.ToString();
		}

		//public string GetAllDailyWindDirGraphData()
		//{
		//	int linenum = 0;
		//	int valInt;

		//	/* returns:
		//	 *	{
		//	 *		highgust:[[date1,val1],[date2,val2]...],
		//	 *		mintemp:[[date1,val1],[date2,val2]...],
		//	 *		etc
		//	 *	}
		//	 */

		//	StringBuilder sb = new StringBuilder("{");
		//	StringBuilder windDir = new StringBuilder("[");

		//	var watch = Stopwatch.StartNew();

		//	// Read the dayfile and extract the records from there
		//	if (File.Exists(cumulus.DayFile))
		//	{
		//		try
		//		{
		//			var dayfile = File.ReadAllLines(cumulus.DayFile);

		//			foreach (var line in dayfile)
		//			{
		//				linenum++;
		//				List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

		//				if (st.Count <= 0) continue;

		//				// dominant wind direction
		//				if (st.Count > 39)
		//				{
		//					long recDate = DateTimeToUnix(ddmmyyStrToDate(st[0])) * 1000;

		//					if (int.TryParse(st[39], out valInt))
		//						windDir.Append($"[{recDate},{valInt}]");
		//					else
		//						windDir.Append($"[{recDate},null]");
		//					if (linenum < dayfile.Length)
		//						windDir.Append(",");
		//				}
		//			}
		//		}
		//		catch (Exception e)
		//		{
		//			cumulus.LogMessage("GetAllDailyWindDirGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
		//		}
		//	}
		//	sb.Append("\"windDir\":" + windDir.ToString() + "]");
		//	sb.Append("}");

		//	watch.Stop();
		//	cumulus.LogDebugMessage($"GetAllDailyWindDirGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

		//	return sb.ToString();
		//}

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
				var len = DayFile.Count() - 1;

				for (var i = 0; i < DayFile.Count(); i++)
				{

					long recDate = DateTimeToUnix(DayFile[i].Date) * 1000;

					// lo humidity
					minHum.Append($"[{recDate},{DayFile[i].LowHumidity}]");
					// hi humidity
					maxHum.Append($"[{recDate},{DayFile[i].HighHumidity}]");

					if (i < len)
					{
						minHum.Append(",");
						maxHum.Append(",");
					}
				}
			}
			sb.Append("\"minHum\":" + minHum.ToString() + "],");
			sb.Append("\"maxHum\":" + maxHum.ToString() + "]");
			sb.Append("}");

			return sb.ToString();
		}

		public string GetAllDailySolarGraphData()
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
				var len = DayFile.Count() - 1;

				for (var i = 0; i < DayFile.Count(); i++)
				{
					long recDate = DateTimeToUnix(DayFile[i].Date) * 1000;

					if (cumulus.GraphOptions.SunshineVisible)
					{
						// sunshine hours
						sunHours.Append($"[{recDate},{DayFile[i].SunShineHours.ToString(InvC)}]");
						if (i < len)
							sunHours.Append(",");
					}

					if (cumulus.GraphOptions.SolarVisible)
					{
						// hi solar rad
						solarRad.Append($"[{recDate},{DayFile[i].HighSolar}]");
						if (i < len)
							solarRad.Append(",");
					}

					if (cumulus.GraphOptions.UVVisible)
					{
						// hi UV-I
						uvi.Append($"[{recDate},{DayFile[i].HighUv.ToString(cumulus.UVFormat, InvC)}]");
						if (i < len)
							uvi.Append(",");
					}
				}
			}
			if (cumulus.GraphOptions.SunshineVisible)
				sb.Append("\"sunHours\":" + sunHours.ToString() + "]");

			if (cumulus.GraphOptions.SolarVisible)
			{
				if (cumulus.GraphOptions.SunshineVisible)
					sb.Append(",");

				sb.Append("\"solarRad\":" + solarRad.ToString() + "]");
			}

			if (cumulus.GraphOptions.UVVisible)
			{
				if (cumulus.GraphOptions.SunshineVisible || cumulus.GraphOptions.SolarVisible)
					sb.Append(",");

				sb.Append("\"uvi\":" + uvi.ToString() + "]");
			}
			sb.Append("}");

			return sb.ToString();
		}

		public string GetAllDegreeDaysGraphData()
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
			if (DayFile.Count() > 0 && (cumulus.GraphOptions.GrowingDegreeDaysVisible1 || cumulus.GraphOptions.GrowingDegreeDaysVisible2))
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

				if (cumulus.GraphOptions.GrowingDegreeDaysVisible1)
				{
					growdegdaysYears1.Append($"\"{startYear}\":");
				}
				if (cumulus.GraphOptions.GrowingDegreeDaysVisible2)
				{
					growdegdaysYears2.Append($"\"{startYear}\":");
				}


				for (var i = 0; i < DayFile.Count(); i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (DayFile[i].Date >= nextYear)
					{
						if (cumulus.GraphOptions.GrowingDegreeDaysVisible1 && growYear1.Length > 10)
						{
							// remove last comma
							growYear1.Length--;
							// close the year data
							growYear1.Append("],");
							// append to years array
							growdegdaysYears1.Append(growYear1);

							growYear1.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}
						if (cumulus.GraphOptions.GrowingDegreeDaysVisible2 && growYear2.Length > 10)
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
					long recDate = DateTimeToUnix(new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day)) * 1000;

					if (cumulus.GraphOptions.GrowingDegreeDaysVisible1)
					{
						// growing degree days
						var gdd = MeteoLib.GrowingDegreeDays(ConvertUserTempToC(DayFile[i].HighTemp), ConvertUserTempToC(DayFile[i].LowTemp), ConvertUserTempToC(cumulus.GrowingBase1), cumulus.GrowingCap30C);

						// annual accumulation
						annualGrowingDegDays1 += gdd;

						growYear1.Append($"[{recDate},{annualGrowingDegDays1.ToString("F1", InvC)}],");
					}

					if (cumulus.GraphOptions.GrowingDegreeDaysVisible2)
					{
						// growing degree days
						var gdd = MeteoLib.GrowingDegreeDays(ConvertUserTempToC(DayFile[i].HighTemp), ConvertUserTempToC(DayFile[i].LowTemp), ConvertUserTempToC(cumulus.GrowingBase2), cumulus.GrowingCap30C);

						// annual accumulation
						annualGrowingDegDays2 += gdd;

						growYear2.Append($"[{recDate},{annualGrowingDegDays2.ToString("F1", InvC)}],");
					}
				}
			}

			// remove last commas from the years arrays and close them off
			if (cumulus.GraphOptions.GrowingDegreeDaysVisible1)
			{
				if (growYear1[growYear1.Length - 1] == ',')
				{
					growYear1.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears1[growdegdaysYears1.Length - 1] == ']')
				{
					growdegdaysYears1.Append(",");
				}

				growdegdaysYears1.Append(growYear1 + "]");

				// add to main json
				sb.Append("\"GDD1\":" + growdegdaysYears1 + "},");
			}
			if (cumulus.GraphOptions.GrowingDegreeDaysVisible2)
			{
				if (growYear2[growYear2.Length - 1] == ',')
				{
					growYear2.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears2[growdegdaysYears2.Length - 1] == ']')
				{
					growdegdaysYears2.Append(",");
				}
				growdegdaysYears2.Append(growYear2 + "]");

				// add to main json
				sb.Append("\"GDD2\":" + growdegdaysYears2 + "},");
			}

			sb.Append(options);

			sb.Append("}");

			return sb.ToString();
		}

		public string GetAllTempSumGraphData()
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
			if (DayFile.Count() > 0 && (cumulus.GraphOptions.TempSumVisible0 || cumulus.GraphOptions.TempSumVisible1 || cumulus.GraphOptions.TempSumVisible2))
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

				if (cumulus.GraphOptions.TempSumVisible0)
				{
					tempSumYears0.Append($"\"{startYear}\":");
				}
				if (cumulus.GraphOptions.TempSumVisible1)
				{
					tempSumYears1.Append($"\"{startYear}\":");
				}
				if (cumulus.GraphOptions.TempSumVisible2)
				{
					tempSumYears2.Append($"\"{startYear}\":");
				}

				for (var i = 0; i < DayFile.Count(); i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (DayFile[i].Date >= nextYear)
					{
						if (cumulus.GraphOptions.TempSumVisible0 && tempSum0.Length > 10)
						{
							// remove last comma
							tempSum0.Length--;
							// close the year data
							tempSum0.Append("],");
							// append to years array
							tempSumYears0.Append(tempSum0);

							tempSum0.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}
						if (cumulus.GraphOptions.TempSumVisible1 && tempSum1.Length > 10)
						{
							// remove last comma
							tempSum1.Length--;
							// close the year data
							tempSum1.Append("],");
							// append to years array
							tempSumYears1.Append(tempSum1);

							tempSum1.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}
						if (cumulus.GraphOptions.TempSumVisible2 && tempSum2.Length > 10)
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

					long recDate = DateTimeToUnix(new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day)) * 1000;

					if (cumulus.GraphOptions.TempSumVisible0)
					{
						// annual accumulation
						annualTempSum0 += DayFile[i].AvgTemp;
						tempSum0.Append($"[{recDate},{annualTempSum0.ToString("F0", InvC)}],");
					}
					if (cumulus.GraphOptions.TempSumVisible1)
					{
						// annual accumulation
						annualTempSum1 += DayFile[i].AvgTemp - cumulus.TempSumBase1;
						tempSum1.Append($"[{recDate},{annualTempSum1.ToString("F0", InvC)}],");
					}
					if (cumulus.GraphOptions.TempSumVisible2)
					{
						// annual accumulation
						annualTempSum2 += DayFile[i].AvgTemp - cumulus.TempSumBase2;
						tempSum2.Append($"[{recDate},{annualTempSum2.ToString("F0", InvC)}],");
					}
				}
			}

			// remove last commas from the years arrays and close them off
			if (cumulus.GraphOptions.TempSumVisible0)
			{
				if (tempSum0[tempSum0.Length - 1] == ',')
				{
					tempSum0.Length--;
				}

				// have previous years been appended?
				if (tempSumYears0[tempSumYears0.Length - 1] == ']')
				{
					tempSumYears0.Append(",");
				}

				tempSumYears0.Append(tempSum0 + "]");

				// add to main json
				sb.Append("\"Sum0\":" + tempSumYears0 + "}");

				if (cumulus.GraphOptions.TempSumVisible1 || cumulus.GraphOptions.TempSumVisible2)
					sb.Append(",");
			}
			if (cumulus.GraphOptions.TempSumVisible1)
			{
				if (tempSum1[tempSum1.Length - 1] == ',')
				{
					tempSum1.Length--;
				}

				// have previous years been appended?
				if (tempSumYears1[tempSumYears1.Length - 1] == ']')
				{
					tempSumYears1.Append(",");
				}

				tempSumYears1.Append(tempSum1 + "]");

				// add to main json
				sb.Append("\"Sum1\":" + tempSumYears1 + "},");
			}
			if (cumulus.GraphOptions.TempSumVisible2)
			{
				if (tempSum2[tempSum2.Length - 1] == ',')
				{
					tempSum2.Length--;
				}

				// have previous years been appended?
				if (tempSumYears2[tempSumYears2.Length - 1] == ']')
				{
					tempSumYears2.Append(",");
				}

				tempSumYears2.Append(tempSum2 + "]");

				// add to main json
				sb.Append("\"Sum2\":" + tempSumYears2 + "},");
			}

			sb.Append(options);

			sb.Append("}");

			return sb.ToString();
		}



		internal string GetCurrentData()
		{
			StringBuilder windRoseData = new StringBuilder((windcounts[0] * cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture), 4096);
			lock (windRoseData)
			{
				for (var i = 1; i < cumulus.NumWindRosePoints; i++)
				{
					windRoseData.Append(",");
					windRoseData.Append((windcounts[i] * cumulus.Calib.WindGust.Mult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
				}
			}
			string stormRainStart = StartOfStorm == DateTime.MinValue ? "-----" : StartOfStorm.ToString("d");

			var data = new DataStruct(cumulus, OutdoorTemperature, OutdoorHumidity, TempTotalToday / tempsamplestoday, IndoorTemperature, OutdoorDewpoint, WindChill, IndoorHumidity,
				Pressure, WindLatest, WindAverage, RecentMaxGust, WindRunToday, Bearing, AvgBearing, RainToday, RainYesterday, RainMonth, RainYear, RainRate,
				RainLastHour, HeatIndex, Humidex, ApparentTemperature, temptrendval, presstrendval, HiLoToday.HighGust, HiLoToday.HighGustTime.ToString("HH:mm"), HiLoToday.HighWind,
				HiLoToday.HighGustBearing, cumulus.Units.WindText, BearingRangeFrom10, BearingRangeTo10, windRoseData.ToString(), HiLoToday.HighTemp, HiLoToday.LowTemp,
				HiLoToday.HighTempTime.ToString("HH:mm"), HiLoToday.LowTempTime.ToString("HH:mm"), HiLoToday.HighPress, HiLoToday.LowPress, HiLoToday.HighPressTime.ToString("HH:mm"),
				HiLoToday.LowPressTime.ToString("HH:mm"), HiLoToday.HighRainRate, HiLoToday.HighRainRateTime.ToString("HH:mm"), HiLoToday.HighHumidity, HiLoToday.LowHumidity,
				HiLoToday.HighHumidityTime.ToString("HH:mm"), HiLoToday.LowHumidityTime.ToString("HH:mm"), cumulus.Units.PressText, cumulus.Units.TempText, cumulus.Units.RainText,
				HiLoToday.HighDewPoint, HiLoToday.LowDewPoint, HiLoToday.HighDewPointTime.ToString("HH:mm"), HiLoToday.LowDewPointTime.ToString("HH:mm"), HiLoToday.LowWindChill,
				HiLoToday.LowWindChillTime.ToString("HH:mm"), (int)SolarRad, (int)HiLoToday.HighSolar, HiLoToday.HighSolarTime.ToString("HH:mm"), UV, HiLoToday.HighUv,
				HiLoToday.HighUvTime.ToString("HH:mm"), forecaststr, getTimeString(cumulus.SunRiseTime), getTimeString(cumulus.SunSetTime),
				getTimeString(cumulus.MoonRiseTime), getTimeString(cumulus.MoonSetTime), HiLoToday.HighHeatIndex, HiLoToday.HighHeatIndexTime.ToString("HH:mm"), HiLoToday.HighAppTemp,
				HiLoToday.LowAppTemp, HiLoToday.HighAppTempTime.ToString("HH:mm"), HiLoToday.LowAppTempTime.ToString("HH:mm"), (int)Math.Round(CurrentSolarMax),
				AllTime.HighPress.Val, AllTime.LowPress.Val, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
				HiLoToday.HighHourlyRain, HiLoToday.HighHourlyRainTime.ToString("HH:mm"), "F" + cumulus.Beaufort(HiLoToday.HighWind), "F" + cumulus.Beaufort(WindAverage),
				cumulus.BeaufortDesc(WindAverage), LastDataReadTimestamp.ToString("HH:mm:ss"), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
				cumulus.LowTempAlarm.Triggered, cumulus.HighTempAlarm.Triggered, cumulus.TempChangeAlarm.UpTriggered, cumulus.TempChangeAlarm.DownTriggered, cumulus.HighRainTodayAlarm.Triggered, cumulus.HighRainRateAlarm.Triggered,
				cumulus.LowPressAlarm.Triggered, cumulus.HighPressAlarm.Triggered, cumulus.PressChangeAlarm.UpTriggered, cumulus.PressChangeAlarm.DownTriggered, cumulus.HighGustAlarm.Triggered, cumulus.HighWindAlarm.Triggered,
				cumulus.SensorAlarm.Triggered, cumulus.BatteryLowAlarm.Triggered, cumulus.SpikeAlarm.Triggered, cumulus.UpgradeAlarm.Triggered, cumulus.HttpUploadAlarm.Triggered, cumulus.MySqlUploadAlarm.Triggered,
				FeelsLike, HiLoToday.HighFeelsLike, HiLoToday.HighFeelsLikeTime.ToString("HH:mm:ss"), HiLoToday.LowFeelsLike, HiLoToday.LowFeelsLikeTime.ToString("HH:mm:ss"),
				HiLoToday.HighHumidex, HiLoToday.HighHumidexTime.ToString("HH:mm:ss"));

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
			// Spike check is in m/s
			var windGustMS = ConvertUserWindToMS(gust);
			if (((previousGust != 999) && (Math.Abs(windGustMS - previousGust) > cumulus.Spike.GustDiff)) || windGustMS >= cumulus.Limit.WindHigh)
			{
				cumulus.LogSpikeRemoval("Wind Gust difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={windGustMS:F1} OldVal={previousGust:F1} SpikeGustDiff={cumulus.Spike.GustDiff:F1} HighLimit={cumulus.Limit.WindHigh:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastError = $"Wind Gust difference greater than spike value - NewVal={windGustMS:F1}, OldVal={previousGust:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return false;
			}

			if (gust > RecentMaxGust)
			{
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
			}
			return true;
		}

		/// <summary>
		/// Calculates the Davis version (almost) of Evapotranspiration
		/// </summary>
		/// <returns>ET for past hour in mm</returns>
		/*
		public double CalulateEvapotranspiration(DateTime ts)
		{
			var onehourago = ts.AddHours(-1);

			// Mean temperature in Fahrenheit
			var result = RecentDataDb.Query<AvgData>("select avg(OutsideTemp) temp, avg(WindSpeed) wind, avg(SolarRad) solar, avg(Humidity) hum from RecentData where Timestamp >= ?", onehourago);

			var meanTempC = ConvertUserTempToC(result[0].temp);
			var meanTempK = meanTempC + 273.16;
			var meanWind = ConvertUserWindToMS(result[0].wind);
			var meanHum = result[0].hum;
			var meanSolar = result[0].solar;
			var pressure = ConvertUserPressToMB(AltimeterPressure) / 100;  // need kPa

			var satVapPress = MeteoLib.SaturationVapourPressure2008(meanTempC);
			var waterVapour = satVapPress * meanHum / 100;

			var delta = satVapPress / meanTempK * ((6790.4985 / meanTempK) - 5.02808);

			var gamma = 0.000646 * (1 + 0.000946 * meanTempC) * pressure;

			var weighting = delta / (delta + gamma);

			double windFunc;
			if (meanSolar > 0)
				windFunc = 0.030 + 0.0576 * meanWind;
			else
				windFunc = 0.125 + 0.0439 * meanWind;

			var lambda = 69.5 * (1 - 0.000946 * meanTempC);

			//TODO: Need to calculate the net radiation rather than use meanSolar - need mean theoretical solar value for this
			var meanSolarMax = 0.0; //TODO

			// clear sky - meanSolar/meanSolarMax >= 1, c <= 1, solar elevation > 10 deg
			var clearSky = (1.333 - 1.333 * meanSolar / meanSolarMax);

			var netSolar = 0.0; //TODO

			var ET = weighting * netSolar / lambda + (1 - weighting) * (satVapPress - waterVapour) * windFunc;

			return ET;
		}
		*/


		public void UpdateAPRS()
		{
			if (DataStopped)
			{
				// No data coming in, do nothing
				return;
			}

			cumulus.LogMessage("Updating CWOP");
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
						message.Append("P");
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
						if ((!cumulus.APRS.HumidityCutoff) || (ConvertUserTempToC(OutdoorTemperature) >= -10))
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
					cumulus.LogMessage("CWOP error: " + e.Message);
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
			double lat;
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
			int hh = (int) Math.Round(s*100/60.0);

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
			double lon;
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
			int hh = (int) Math.Round(s*100/60.0);

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
			var windMPH = Convert.ToInt32(ConvertUserWindToMPH(wind));
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
			var press10mb = Convert.ToInt32(ConvertUserPressToMB(press) * 10);
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
			var rain100IN = Convert.ToInt32(ConvertUserRainToIN(rain) * 100);
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
		public double maxTemp { get; set; }
		public double minTemp { get; set; }
		public int maxHum { get; set; }
		public int minHum { get; set; }
		public double avgSol { get; set; }
		public double avgWind { get; set; }
		public double maxPress { get; set; }
		public double minPress { get; set; }
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
		public AllTimeRec LowDewPoint{ get; set; } = new AllTimeRec(19);
		public AllTimeRec HighWindRun { get; set; } = new AllTimeRec(20);
		public AllTimeRec LongestDryPeriod { get; set; } = new AllTimeRec(21);
		public AllTimeRec LongestWetPeriod { get; set; } = new AllTimeRec(22);
		public AllTimeRec HighDailyTempRange { get; set; } = new AllTimeRec(23);
		public AllTimeRec LowDailyTempRange { get; set; } = new AllTimeRec(24);
		public AllTimeRec HighFeelsLike { get; set; } = new AllTimeRec(25);
		public AllTimeRec LowFeelsLike { get; set; } = new AllTimeRec(26);
		public AllTimeRec HighHumidex { get; set; } = new AllTimeRec(27);
	}

	public class AllTimeRec
	{
		private static string[] alltimedescs = new[]
		{
			"High temperature", "Low temperature", "High gust", "High wind speed", "Low wind chill", "High rain rate", "High daily rain",
			"High hourly rain", "Low pressure", "High pressure", "Highest monthly rainfall", "Highest minimum temp", "Lowest maximum temp",
			"High humidity", "Low humidity", "High apparent temp", "Low apparent temp", "High heat index", "High dew point", "Low dew point",
			"High daily windrun", "Longest dry period", "Longest wet period", "High daily temp range", "Low daily temp range",
			"High feels like", "Low feels like", "High Humidex"
		};
		private int idx;

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
