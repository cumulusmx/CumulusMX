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
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Devart.Data.MySql;
using SQLite;
using Timer = System.Timers.Timer;
using System.Security.Cryptography;

namespace CumulusMX
{
	internal abstract class WeatherStation
	{
		public struct TAlltime
		{
			public int data_type;
			public double value;
			public DateTime timestamp;
		}

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
		public TAlltime[] alltimerecarray = new TAlltime[alltimerecbound + 1];
		// holds monthly all time highs and lows
		public TAlltime[,] monthlyrecarray = new TAlltime[alltimerecbound + 1, 13];

		public string[] alltimedescs = new[]
			{
				"High temperature", "Low temperature", "High gust", "High wind speed", "Low wind chill", "High rain rate", "High daily rain",
				"High hourly rain", "Low pressure", "High pressure", "Highest monthly rainfall", "Highest minimum temp", "Lowest maximum temp",
				"High humidity", "Low humidity", "High apparent temp", "Low apparent temp", "High heat index", "High dew point", "Low dew point",
				"High daily windrun", "Longest dry period", "Longest wet period", "High daily temp range", "Low daily temp range",
				"High feels like", "Low feels like", "High Humidex"
			};

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

		// Indexes into the AllTime records array
		public const int AT_HighTemp = 0;
		public const int AT_LowTemp = 1;
		public const int AT_HighGust = 2;
		public const int AT_HighWind = 3;
		public const int AT_LowChill = 4;
		public const int AT_HighRainRate = 5;
		public const int AT_DailyRain = 6;
		public const int AT_HourlyRain = 7;
		public const int AT_LowPress = 8;
		public const int AT_HighPress = 9;
		public const int AT_WetMonth = 10;
		public const int AT_HighMinTemp = 11;
		public const int AT_LowMaxTemp = 12;
		public const int AT_HighHumidity = 13;
		public const int AT_lowhumidity = 14;
		public const int AT_HighAppTemp = 15;
		public const int AT_LowAppTemp = 16;
		public const int AT_HighHeatIndex = 17;
		public const int AT_HighDewPoint = 18;
		public const int AT_LowDewpoint = 19;
		public const int AT_HighWindrun = 20;
		public const int AT_LongestDryPeriod = 21;
		public const int AT_LongestWetPeriod = 22;
		public const int AT_HighDailyTempRange = 23;
		public const int AT_LowDailyTempRange = 24;
		public const int AT_HighFeelsLike = 25;
		public const int AT_LowFeelsLike = 26;
		public const int AT_HighHumidex = 27;

		public DateTime AlltimeRecordTimestamp { get; set; }

		//private ProgressWindow progressWindow;

		//public historyProgressWindow histprog;
		public BackgroundWorker bw;

		//public bool importingData = false;

		public bool calculaterainrate = false;

		protected List<int> buffer = new List<int>();

		private readonly List<Last3HourData> Last3HourDataList = new List<Last3HourData>();
		private readonly List<LastHourData> LastHourDataList = new List<LastHourData>();
		private readonly List<GraphData> GraphDataList = new List<GraphData>();
		private readonly List<Last10MinWind> Last10MinWindList = new List<Last10MinWind>();
		private readonly List<RecentDailyData> RecentDailyDataList = new List<RecentDailyData>();

		public WeatherDataCollection weatherDataCollection = new WeatherDataCollection();

		// Current values

		public double THWIndex = 0;
		public double THSWIndex = 0;

		public double raindaystart = 0.0;
		public double Raincounter = 0.0;
		public bool gotraindaystart = false;
		protected double prevraincounter = 0.0;

		// highs and lows since last log entry
		/*
		private double hiOutdoorTemperature = -999.0;
		private double loOutdoorTemperature = 999;
		private double hiApparentTemperature = -999.0;
		private double loApparentTemperature = 999;
		private double hiIndoorTemperature = -999.0;
		private double loIndoorTemperature = 999;
		private int loIndoorHumidity = 100;
		private int hiIndoorHumidity = 0;
		private int loOutdoorHumidity = 100;
		private int hiOutdoorHumidity = 0;
		private double loPressure = 9999;
		private double hiPressure = 0.0;
		private double hiWind = 0.0;
		private double hiGust = 0.0;
		//private double gust10 = 0.0;
		private int hiWindBearing = 0;
		private int hiGustBearing = 0;
		private double hiRainRate = 0;
		private double loWindChill;
		private double loDewPoint = 999;
		private double hiDewPoint = -999;
		private double hiHeatIndex = -999;
		private double hiHumidex = -999;
		//private double hiUV = 0;
		//private double hiSolarRad = 0;
		*/

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
			public double HighRain;
			public DateTime HighRainTime;
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
			public double HighDewpoint;
			public DateTime HighDewpointTime;
			public double LowDewpoint;
			public DateTime LowDewpointTime;
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
			HighDewpoint = -500,
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewpoint = 999,
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
			HighDewpoint = -500,
			LowTemp = 999,
			LowAppTemp = 999,
			LowFeelsLike = 999,
			LowWindChill = 999,
			LowDewpoint = 999,
			LowPress = 9999,
			LowHumidity = 100
		};


		public int IndoorBattStatus;
		public int WindBattStatus;
		public int RainBattStatus;
		public int TempBattStatus;
		//public Window1 mainWindow;

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
		private const int alltimerecbound = 27;

		public bool WindReadyToPlot = false;
		public bool TempReadyToPlot = false;
		private bool first_temp = true;
		public double RG11RainYesterday { get; set; }

		public abstract void portDataReceived(object sender, SerialDataReceivedEventArgs e);
		public abstract void Start();

		public WeatherStation(Cumulus cumulus)
		{
			// save the reference to the owner
			this.cumulus = cumulus;

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

			GetRainCounter();
			GetRainFallTotals();

			RecentDataDb = new SQLiteConnection(":memory:");
			RecentDataDb.CreateTable<RecentData>();

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
					using (var sr = new StreamReader(LogFile))
					{
						// now process each record to get the last "raintoday" figure
						do
						{
							string Line = sr.ReadLine();
							linenum++;
							var st = new List<string>(Regex.Split(Line, listSep));
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
											cumulus.LogMessage(Line);
											raincount = Double.Parse(st[11]);
										}
									}
								}
								prevlogdate = logdate;
							}
						} while (!sr.EndOfStream);
					}
				}
				catch (Exception E)
				{
					cumulus.LogMessage("Error on line " + linenum + " of " + LogFile + ": " + E.Message);
				}
			}

			if (midnightrainfound)
			{
				if ((logdate.Substring(0, 2) == "01") && (logdate.Substring(3, 2) == "01") && (cumulus.Manufacturer == cumulus.DAVIS))
				{
					// special case: rain counter is about to be reset
					cumulus.LogMessage("Special case, Davis station on 1st Jan. Set midnight rain count to zero");
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

			if (cumulus.RolloverHour == 0)
			{
				Raincounter = midnightraincount + (RainToday / cumulus.Calib.Rain.Mult);
			}
			else
			{
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

		public DateTime ddmmyyStrToDate(string d)
		{
			// Converts a date string in UK order to a DateTime
			// Horrible hack, but we have localised separators, but UK sequence, so localised parsing may fail
			string[] date = d.Split(new string[] { CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator }, StringSplitOptions.None);

			int D = Convert.ToInt32(date[0]);
			int M = Convert.ToInt32(date[1]);
			int Y = Convert.ToInt32(date[2]);
			if (Y > 70)
			{
				Y += 1900;
			}
			else
			{
				Y += 2000;
			}

			return new DateTime(Y, M, D);
		}

		public DateTime ddmmyyhhmmStrToDate(string d, string t)
		{
			// Converts a date string in UK order to a DateTime
			// Horrible hack, but we have localised separators, but UK sequence, so localised parsing may fail
			string[] date = d.Split(new string[] { CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator }, StringSplitOptions.None);
			string[] time = t.Split(new string[] { CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator }, StringSplitOptions.None);

			int D = Convert.ToInt32(date[0]);
			int M = Convert.ToInt32(date[1]);
			int Y = Convert.ToInt32(date[2]);
			if (Y > 70)
			{
				Y += 1900;
			}
			else
			{
				Y += 2000;
			}
			int h = Convert.ToInt32(time[0]);
			int m = Convert.ToInt32(time[1]);

			return new DateTime(Y, M, D, h, m, 0);
		}

		public void GetRainFallTotals()
		{
			cumulus.LogMessage("Getting rain totals, rain season start = " + cumulus.RainSeasonStart);
			rainthismonth = 0;
			rainthisyear = 0;
			int linenum = 0;
			// get today"s date for month check; allow for 0900 rollover
			var hourInc = cumulus.GetHourInc();
			var ModifiedNow = DateTime.Now.AddHours(hourInc);
			// avoid any funny locale peculiarities on date formats
			string Today = ModifiedNow.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
			cumulus.LogMessage("Today = " + Today);
			// get today's date offset by rain season start for year check
			int offsetYearToday = ModifiedNow.AddMonths(-(cumulus.RainSeasonStart - 1)).Year;

			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					using (var sr = new StreamReader(cumulus.DayFile))
					{
						do
						{
							string Line = sr.ReadLine();
							linenum++;
							var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

							if (st.Count > 0)
							{
								string datestr = st[0];
								DateTime loggedDate = ddmmyyStrToDate(datestr);
								int offsetLoggedYear = loggedDate.AddMonths(-(cumulus.RainSeasonStart - 1)).Year;
								// This year?
								if (offsetLoggedYear == offsetYearToday)
								{
									rainthisyear += Double.Parse(st[14]);
								}
								// This month?
								if ((loggedDate.Month == ModifiedNow.Month) && (loggedDate.Year == ModifiedNow.Year))
								{
									rainthismonth += Double.Parse(st[14]);
								}
							}
						} while (!sr.EndOfStream);
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("GetRainfallTotals: Error on line " + linenum + " of dayfile.txt: " + ex.Message);
				}

				cumulus.LogMessage("Rainthismonth from dayfile.txt: " + rainthismonth);
				cumulus.LogMessage("Rainthisyear from dayfile.txt: " + rainthisyear);
			}

			// Add in year-to-date rain (if necessary)
			if (cumulus.YTDrainyear == Convert.ToInt32(Today.Substring(6, 2)) + 2000)
			{
				cumulus.LogMessage("Adding YTD rain: " + cumulus.YTDrain);

				rainthisyear += cumulus.YTDrain;
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
			if (cumulus.StationOptions.SyncFOReads)
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
			ChillHours = ini.GetValue("Temp", "ChillHours", 0.0);

			// NOAA report names
			cumulus.NOAALatestMonthlyReport = ini.GetValue("NOAA", "LatestMonthlyReport", "");
			cumulus.NOAALatestYearlyReport = ini.GetValue("NOAA", "LatestYearlyReport", "");

			// Solar
			HiLoToday.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0.0);
			HiLoToday.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			HiLoToday.HighUvTime = ini.GetValue("Solar", "HighUVTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			StartOfDaySunHourCounter = ini.GetValue("Solar", "SunStart", -999.0);
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
			// PressureHighDewpoint
			HiLoToday.LowPress = ini.GetValue("Pressure", "Low", 9999.0);
			HiLoToday.LowPressTime = ini.GetValue("Pressure", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoToday.HighPressTime = ini.GetValue("Pressure", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// rain
			HiLoToday.HighRain = ini.GetValue("Rain", "High", 0.0);
			HiLoToday.HighRainTime = ini.GetValue("Rain", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoToday.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			raindaystart = ini.GetValue("Rain", "Start", -1.0);
			RainYesterday = ini.GetValue("Rain", "Yesterday", 0.0);
			if (raindaystart >= 0)
			{
				cumulus.LogMessage("ReadTodayfile: set notraininit false");
				notraininit = false;
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
			HiLoToday.HighDewpoint = ini.GetValue("Dewpoint", "High", -999.0);
			HiLoToday.HighDewpointTime = ini.GetValue("Dewpoint", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HiLoToday.LowDewpoint = ini.GetValue("Dewpoint", "Low", 999.0);
			HiLoToday.LowDewpointTime = ini.GetValue("Dewpoint", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
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
				// Pressure
				ini.SetValue("Pressure", "Low", HiLoToday.LowPress);
				ini.SetValue("Pressure", "LTime", HiLoToday.LowPressTime.ToString("HH:mm"));
				ini.SetValue("Pressure", "High", HiLoToday.HighPress);
				ini.SetValue("Pressure", "HTime", HiLoToday.HighPressTime.ToString("HH:mm"));
				// rain
				ini.SetValue("Rain", "High", HiLoToday.HighRain);
				ini.SetValue("Rain", "HTime", HiLoToday.HighRainTime.ToString("HH:mm"));
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
				ini.SetValue("Dewpoint", "Low", HiLoToday.LowDewpoint);
				ini.SetValue("Dewpoint", "LTime", HiLoToday.LowDewpointTime.ToString("HH:mm"));
				ini.SetValue("Dewpoint", "High", HiLoToday.HighDewpoint);
				ini.SetValue("Dewpoint", "HTime", HiLoToday.HighDewpointTime.ToString("HH:mm"));

				// NOAA report names
				ini.SetValue("NOAA", "LatestMonthlyReport", cumulus.NOAALatestMonthlyReport);
				ini.SetValue("NOAA", "LatestYearlyReport", cumulus.NOAALatestYearlyReport);

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

			switch (cumulus.PressUnit)
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

		public void CheckMonthlyAlltime(int index, double value, bool higher, DateTime timestamp)
		{
			lock (monthlyalltimeIniThreadLock)
			{
				bool recordbroken;

				double epsilon = 0.001; // required difference for new record

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

				double oldvalue = monthlyrecarray[index, month].value;
				//DateTime oldts = monthlyrecarray[index, month].timestamp;

				if (higher)
				{
					// check new value is higher than existing record
					recordbroken = (value - oldvalue > epsilon);
				}
				else
				{
					// check new value is lower than existing record
					recordbroken = (oldvalue - value > epsilon);
				}

				if (recordbroken)
				{
					// records which apply to whole days or months need their timestamps adjusting
					if ((index == AT_WetMonth) || (index == AT_DailyRain))
					{
						DateTime CurrentMonthTS = new DateTime(year, month, day);
						SetMonthlyAlltime(index, value, CurrentMonthTS, CurrentMonthTS.Month);
					}
					else
					{
						SetMonthlyAlltime(index, value, timestamp, timestamp.Month);
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
			if (OutdoorTemperature < cumulus.NOAAheatingthreshold)
			{
				HeatingDegreeDays += (((cumulus.NOAAheatingthreshold - OutdoorTemperature) * interval) / 1440);
			}
			if (OutdoorTemperature > cumulus.NOAAcoolingthreshold)
			{
				CoolingDegreeDays += (((OutdoorTemperature - cumulus.NOAAcoolingthreshold) * interval) / 1440);
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

		public int LeafWetness1 { get; set; }

		public int LeafWetness2 { get; set; }

		public int LeafWetness3 { get; set; }

		public int LeafWetness4 { get; set; }

		public double SunshineHours { get; set; } = 0;

		public double YestSunshineHours { get; set; } = 0;

		public double SunshineToMidnight { get; set; }

		public double SunHourCounter { get; set; }

		public double StartOfDaySunHourCounter { get; set; }

		public double CurrentSolarMax { get; set; }

		public double RG11RainToday { get; set; }

		public double RainSinceMidnight { get; set; }

		/// <summary>
		/// Checks whether a new day has started and does a rollover if necessary
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
					TenMinuteChanged();
				}

				if (timeNow.Hour != lastHour)
				{
					lastHour = timeNow.Hour;
					HourChanged(timeNow);
				}

				MinuteChanged(timeNow);
			}

			if ((int)timeNow.TimeOfDay.TotalMilliseconds % 2500 <= 500)
			{
				// send current data to websocket every 3 seconds
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
						HiLoToday.HighGustBearing, cumulus.WindUnitText, BearingRangeFrom10, BearingRangeTo10, windRoseData.ToString(), HiLoToday.HighTemp, HiLoToday.LowTemp,
						HiLoToday.HighTempTime.ToString("HH:mm"), HiLoToday.LowTempTime.ToString("HH:mm"), HiLoToday.HighPress, HiLoToday.LowPress, HiLoToday.HighPressTime.ToString("HH:mm"),
						HiLoToday.LowPressTime.ToString("HH:mm"), HiLoToday.HighRain, HiLoToday.HighRainTime.ToString("HH:mm"), HiLoToday.HighHumidity, HiLoToday.LowHumidity,
						HiLoToday.HighHumidityTime.ToString("HH:mm"), HiLoToday.LowHumidityTime.ToString("HH:mm"), cumulus.PressUnitText, cumulus.TempUnitText, cumulus.RainUnitText,
						HiLoToday.HighDewpoint, HiLoToday.LowDewpoint, HiLoToday.HighDewpointTime.ToString("HH:mm"), HiLoToday.LowDewpointTime.ToString("HH:mm"), HiLoToday.LowWindChill,
						HiLoToday.LowWindChillTime.ToString("HH:mm"), (int)SolarRad, (int)HiLoToday.HighSolar, HiLoToday.HighSolarTime.ToString("HH:mm"), UV, HiLoToday.HighUv,
						HiLoToday.HighUvTime.ToString("HH:mm"), forecaststr, getTimeString(cumulus.SunRiseTime), getTimeString(cumulus.SunSetTime),
						getTimeString(cumulus.MoonRiseTime), getTimeString(cumulus.MoonSetTime), HiLoToday.HighHeatIndex, HiLoToday.HighHeatIndexTime.ToString("HH:mm"), HiLoToday.HighAppTemp,
						HiLoToday.LowAppTemp, HiLoToday.HighAppTempTime.ToString("HH:mm"), HiLoToday.LowAppTempTime.ToString("HH:mm"), (int)CurrentSolarMax,
						alltimerecarray[AT_HighPress].value, alltimerecarray[AT_LowPress].value, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
						HiLoToday.HighHourlyRain, HiLoToday.HighHourlyRainTime.ToString("HH:mm"), "F" + cumulus.Beaufort(HiLoToday.HighWind), "F" + cumulus.Beaufort(WindAverage), cumulus.BeaufortDesc(WindAverage),
						LastDataReadTimestamp.ToString("HH:mm:ss"), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
						cumulus.LowTempAlarm.Triggered, cumulus.HighTempAlarm.Triggered, cumulus.TempChangeAlarm.UpTriggered, cumulus.TempChangeAlarm.DownTriggered, cumulus.HighRainTodayAlarm.Triggered, cumulus.HighRainRateAlarm.Triggered,
						cumulus.LowPressAlarm.Triggered, cumulus.HighPressAlarm.Triggered, cumulus.PressChangeAlarm.UpTriggered, cumulus.PressChangeAlarm.DownTriggered, cumulus.HighGustAlarm.Triggered, cumulus.HighWindAlarm.Triggered,
						cumulus.SensorAlarm.Triggered, cumulus.BatteryLowAlarm.Triggered, cumulus.SpikeAlarm.Triggered, FeelsLike, HiLoToday.HighFeelsLike, HiLoToday.HighFeelsLikeTime.ToString("HH:mm"), HiLoToday.LowFeelsLike, HiLoToday.LowFeelsLikeTime.ToString("HH:mm"),
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

		private void HourChanged(DateTime now)
		{
			cumulus.LogMessage("Hour changed:" + now.Hour);
			cumulus.DoSunriseAndSunset();
			cumulus.DoMoonImage();

			if (cumulus.HourlyForecast)
			{
				DoForecast("", true);
			}

			if (now.Hour == 0)
			{
				ResetMidnightRain(now);
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

		private void RemoveOldRecentData(DateTime ts)
		{
			var deleteTime = ts.AddDays(-7);

			RecentDataDb.Execute("delete from RecentData where Timestamp < ?", deleteTime);
		}

		private void ClearAlarms()
		{
			if (cumulus.DataStoppedAlarm.Triggered && DateTime.Now > cumulus.DataStoppedAlarm.TriggeredTime.AddHours(cumulus.DataStoppedAlarm.LatchHours))
				cumulus.DataStoppedAlarm.Triggered = false;

			if (cumulus.BatteryLowAlarm.Triggered && DateTime.Now > cumulus.BatteryLowAlarm.TriggeredTime.AddHours(cumulus.BatteryLowAlarm.LatchHours))
				cumulus.BatteryLowAlarm.Triggered = false;

			if (cumulus.SensorAlarm.Triggered && DateTime.Now > cumulus.SensorAlarm.TriggeredTime.AddHours(cumulus.SensorAlarm.LatchHours))
				cumulus.SensorAlarm.Triggered = false;

			if (cumulus.SpikeAlarm.Triggered && DateTime.Now > cumulus.SpikeAlarm.TriggeredTime.AddHours(cumulus.SpikeAlarm.LatchHours))
				cumulus.SpikeAlarm.Triggered = false;

			if (cumulus.HighWindAlarm.Triggered && DateTime.Now > cumulus.HighWindAlarm.TriggeredTime.AddHours(cumulus.HighWindAlarm.LatchHours))
				cumulus.HighWindAlarm.Triggered = false;

			if (cumulus.HighGustAlarm.Triggered && DateTime.Now > cumulus.HighGustAlarm.TriggeredTime.AddHours(cumulus.HighGustAlarm.LatchHours))
				cumulus.HighGustAlarm.Triggered = false;

			if (cumulus.HighRainRateAlarm.Triggered && DateTime.Now > cumulus.HighRainRateAlarm.TriggeredTime.AddHours(cumulus.HighRainRateAlarm.LatchHours))
				cumulus.HighRainRateAlarm.Triggered = false;

			if (cumulus.HighRainTodayAlarm.Triggered && DateTime.Now > cumulus.HighRainTodayAlarm.TriggeredTime.AddHours(cumulus.HighRainTodayAlarm.LatchHours))
				cumulus.HighRainTodayAlarm.Triggered = false;

			if (cumulus.HighPressAlarm.Triggered && DateTime.Now > cumulus.HighPressAlarm.TriggeredTime.AddHours(cumulus.HighPressAlarm.LatchHours))
				cumulus.HighPressAlarm.Triggered = false;

			if (cumulus.LowPressAlarm.Triggered && DateTime.Now > cumulus.LowPressAlarm.TriggeredTime.AddHours(cumulus.LowPressAlarm.LatchHours))
				cumulus.LowPressAlarm.Triggered = false;

			if (cumulus.HighTempAlarm.Triggered && DateTime.Now > cumulus.HighTempAlarm.TriggeredTime.AddHours(cumulus.HighTempAlarm.LatchHours))
				cumulus.HighTempAlarm.Triggered = false;

			if (cumulus.LowTempAlarm.Triggered && DateTime.Now > cumulus.LowTempAlarm.TriggeredTime.AddHours(cumulus.LowTempAlarm.LatchHours))
				cumulus.LowTempAlarm.Triggered = false;

			if (cumulus.TempChangeAlarm.UpTriggered && DateTime.Now > cumulus.TempChangeAlarm.UpTriggeredTime.AddHours(cumulus.TempChangeAlarm.LatchHours))
				cumulus.TempChangeAlarm.UpTriggered = false;

			if (cumulus.TempChangeAlarm.DownTriggered && DateTime.Now > cumulus.TempChangeAlarm.DownTriggeredTime.AddHours(cumulus.TempChangeAlarm.LatchHours))
				cumulus.TempChangeAlarm.DownTriggered = false;

			if (cumulus.PressChangeAlarm.UpTriggered && DateTime.Now > cumulus.PressChangeAlarm.UpTriggeredTime.AddHours(cumulus.PressChangeAlarm.LatchHours))
				cumulus.PressChangeAlarm.UpTriggered = false;

			if (cumulus.PressChangeAlarm.DownTriggered && DateTime.Now > cumulus.PressChangeAlarm.DownTriggeredTime.AddHours(cumulus.PressChangeAlarm.LatchHours))
				cumulus.PressChangeAlarm.DownTriggered = false;
		}

		private void MinuteChanged(DateTime now)
		{
			CheckForDataStopped();

			if (!DataStopped)
			{
				CurrentSolarMax = AstroLib.SolarMax(now, cumulus.Longitude, cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.RStransfactor, cumulus.BrasTurbidity, cumulus.SolarCalc);
				if (((Pressure > 0) && TempReadyToPlot && WindReadyToPlot) || cumulus.NoSensorCheck)
				{
					// increment wind run by one minute's worth of average speed

					WindRunToday += (WindAverage * WindRunHourMult[cumulus.WindUnit] / 60.0);

					CheckForWindrunHighLow(now);

					CalculateDominantWindBearing(AvgBearing, WindAverage, 1);

					if (OutdoorTemperature < cumulus.ChillHourThreshold)
					{
						// add 1 minute to chill hours
						ChillHours += (1.0 / 60.0);
					}

					// update sunshine hours
					if (cumulus.UseBlakeLarsen)
					{
						ReadBlakeLarsenData();
					}
					else if ((SolarRad > (CurrentSolarMax * cumulus.SunThreshold / 100.0)) && (SolarRad >= cumulus.SolarMinimum))
					{
						SunshineHours += (1.0 / 60.0);
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

					AddLastHourDataEntry(now, Raincounter, OutdoorTemperature);
					RemoveOldLHData(now);
					AddLast3HourDataEntry(now, Pressure, OutdoorTemperature);
					RemoveOldL3HData(now);
					AddGraphDataEntry(now, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
						IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV, FeelsLike, Humidex);
					RemoveOldGraphData(now);
					DoTrendValues(now);
					AddRecentDataEntry(now, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity,
						Pressure, RainToday, SolarRad, UV, Raincounter, FeelsLike, Humidex);

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
					if (cumulus.CustomMySqlMinutesEnabled && now.Minute % cumulus.CustomMySqlMinutesInterval == 0)
					{
						cumulus.CustomMysqlMinutesTimerTick();
					}

					// Custom HTTP update - minutes interval
					if (cumulus.CustomHttpMinutesEnabled && now.Minute % cumulus.CustomHttpMinutesInterval == 0)
					{
						cumulus.CustomHttpMinutesUpdate();
					}

					if (cumulus.WebAutoUpdate && cumulus.SynchronisedWebUpdate && (now.Minute % cumulus.UpdateInterval == 0))
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

					if (cumulus.Wund.Enabled && (now.Minute % cumulus.Wund.Interval == 0) && cumulus.Wund.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.Wund.ID))
					{
						cumulus.UpdateWunderground(now);
					}

					if (cumulus.Windy.Enabled && (now.Minute % cumulus.Windy.Interval == 0) && cumulus.Windy.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.Windy.ApiKey))
					{
						cumulus.UpdateWindy(now);
					}

					if (cumulus.AWEKAS.Enabled && (now.Minute % ((double)cumulus.AWEKAS.Interval / 60) == 0) && cumulus.AWEKAS.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.AWEKAS.ID))
					{
						cumulus.UpdateAwekas(now);
					}

					if (cumulus.WCloud.Enabled && (now.Minute % cumulus.WCloud.Interval == 0) && cumulus.WCloud.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.WCloud.ID))
					{
						cumulus.UpdateWCloud(now);
					}

					if (cumulus.PWS.Enabled && (now.Minute % cumulus.PWS.Interval == 0) && cumulus.PWS.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.PWS.ID) && !String.IsNullOrWhiteSpace(cumulus.PWS.PW))
					{
						cumulus.UpdatePWSweather(now);
					}

					if (cumulus.WOW.Enabled && (now.Minute % cumulus.WOW.Interval == 0) && cumulus.WOW.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.WOW.ID) && !String.IsNullOrWhiteSpace(cumulus.WOW.PW))
					{
						cumulus.UpdateWOW(now);
					}

					if (cumulus.APRS.Enabled && (now.Minute % cumulus.APRS.Interval == 0) && cumulus.APRS.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.APRS.ID))
					{
						UpdateAPRS();
					}

					if (cumulus.Twitter.Enabled  && (now.Minute % cumulus.Twitter.Interval == 0) && cumulus.Twitter.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.Twitter.ID) && !String.IsNullOrWhiteSpace(cumulus.Twitter.PW))
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

					if (cumulus.CreateWxnowTxt)
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
					cumulus.LogDebugMessage($"Current CPU temp = {cumulus.CPUtemp.ToString(cumulus.TempFormat)}{cumulus.TempUnitText}");
				}
				catch (Exception ex)
				{
					cumulus.LogDebugMessage($"Error reading CPU temperature - {ex.Message}");
				}
			}
		}

		private void TenMinuteChanged()
		{
			cumulus.DoMoonPhase();
			cumulus.MoonAge = MoonriseMoonset.MoonAge();

			cumulus.RotateLogFiles();

			ClearAlarms();
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
						SunshineHours = Double.Parse(line);
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

		public void CreateGraphDataFiles()
		{
			// Chart data for Highcharts graphs
			// config
			var json = GetGraphConfig();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "graphconfig.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing graphconfig.json: " + ex.Message);
			}

			// Temperature
			json = GetTempGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "tempdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing tempdata.json: " + ex.Message);
			}

			// Pressure
			json = GetPressGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "pressdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing pressdata.json: " + ex.Message);
			}

			// Wind
			json = GetWindGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "winddata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing winddata.json: " + ex.Message);
			}

			// Wind direction
			json = GetWindDirGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "wdirdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing wdirdata.json: " + ex.Message);
			}

			// Humidity
			json = GetHumGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "humdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing humdata.json: " + ex.Message);
			}

			// Rain
			json = GetRainGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "raindata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing raindata.json: " + ex.Message);
			}

			// Solar
			json = GetSolarGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "solardata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing solardata.json: " + ex.Message);
			}

			// Daily rain
			json = GetDailyRainGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "dailyrain.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing dailyrain.json: " + ex.Message);
			}

			// Sun hours
			json = GetSunHoursGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "sunhours.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing sunhours.json: " + ex.Message);
			}


			// Daily temp
			json = GetDailyTempGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "dailytemp.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing dailytemp.json: " + ex.Message);
			}

			// Air Quality
			if (cumulus.StationOptions.PrimaryAqSensor >= 0)
			{
				json = GetAqGraphData();
				try
				{
					using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "airquality.json", false))
					{
						file.WriteLine(json);
						file.Close();
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error writing airquality.json: " + ex.Message);
				}
			}
		}

		public void CreateEodGraphDataFiles()
		{
			string json;

			// Temperature
			json = GetAllDailyTempGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "alldailytempdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing alldailytempdata.json: " + ex.Message);
			}

			// Pressure
			json = GetAllDailyPressGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "alldailypressdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing alldailypressdata.json: " + ex.Message);
			}

			// Wind
			json = GetAllDailyWindGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "alldailywinddata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing alldaily.json: " + ex.Message);
			}

			// Humidity
			json = GetAllDailyHumGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "alldailyhumdata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing alldailyhumdata.json: " + ex.Message);
			}

			// Rain
			json = GetAllDailyRainGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "alldailyraindata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing alldailyraindata.json: " + ex.Message);
			}

			// Solar
			json = GetAllDailySolarGraphData();
			try
			{
				using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "alldailysolardata.json", false))
				{
					file.WriteLine(json);
					file.Close();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing alldailysolardata.json: " + ex.Message);
			}
		}

		public string GetSolarGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			var append = false;

			lock (GraphDataList)
			{
				if (cumulus.GraphOptions.UVVisible)
				{
					sb.Append("\"UV\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].uvindex.ToString(cumulus.UVFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (append)
				{
					sb.Append(",");
				}

				if (cumulus.GraphOptions.SolarVisible)
				{
					sb.Append("\"SolarRad\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{(int)GraphDataList[i].solarrad}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}


					sb.Append("],\"CurrentSolarMax\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{(int)GraphDataList[i].solarmax}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
				}
			}
			sb.Append("}");
			return sb.ToString();
		}

		public string GetRainGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"rfall\":[");
			lock (GraphDataList)
			{
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].RainToday.ToString(cumulus.RainFormat, InvC)}]");
					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}

				sb.Append("],\"rrate\":[");
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].rainrate.ToString(cumulus.RainFormat, InvC)}]");
					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetHumGraphData()
		{
			var sb = new StringBuilder("{", 10240);
			var append = false;
			lock (GraphDataList)
			{
				if (cumulus.GraphOptions.OutHumVisible)
				{
					sb.Append("\"hum\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].humidity}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.InHumVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"inhum\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].inhumidity}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
				}
			}
			sb.Append("}");
			return sb.ToString();
		}

		public string GetWindDirGraphData()
		{
			var sb = new StringBuilder("{\"bearing\":[");
			lock (GraphDataList)
			{
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].winddir}]");
					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}

				sb.Append("],\"avgbearing\":[");
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].avgwinddir}]");
					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetWindGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"wgust\":[");
			lock (GraphDataList)
			{
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].windgust.ToString(cumulus.WindFormat, InvC)}]");
					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}

				sb.Append("],\"wspeed\":[");
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].windspeed.ToString(cumulus.WindAvgFormat, InvC)}]");
					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetPressGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"press\":[");
			lock (GraphDataList)
			{
				for (var i = 0; i < GraphDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].pressure.ToString(cumulus.PressFormat, InvC)}]");

					if (i < GraphDataList.Count - 1)
						sb.Append(",");
				}
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetTempGraphData()
		{
			var InvC = new CultureInfo("");
			var append = false;
			StringBuilder sb = new StringBuilder("{", 10240);
			lock (GraphDataList)
			{
				if (cumulus.GraphOptions.InTempVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"intemp\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].insidetemp.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.DPVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"dew\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].dewpoint.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.AppTempVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"apptemp\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].apptemp.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.FeelsLikeVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"feelslike\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp) * 1000 + "," + GraphDataList[i].feelslike.ToString(cumulus.TempFormat, InvC) + "]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.WCVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"wchill\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].windchill.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.HIVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"heatindex\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].heatindex.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.TempVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"temp\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].temperature.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}

				if (cumulus.GraphOptions.HumidexVisible)
				{
					if (append)
					{
						sb.Append(",");
					}
					sb.Append("\"humidex\":[");
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].humidex.ToString(cumulus.TempFormat, InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					append = true;
				}
			}
			sb.Append("}");
			return sb.ToString();
		}

		public string GetAqGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{");
			// Check if we are to generate AQ data at all
			if (cumulus.StationOptions.PrimaryAqSensor >= 0)
			{
				sb.Append("\"pm2p5\":[");
				lock (GraphDataList)
				{
					for (var i = 0; i < GraphDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].pm2p5.ToString("F1", InvC)}]");
						if (i < GraphDataList.Count - 1)
							sb.Append(",");
					}
					sb.Append("]");
					// Only the AirLink provides PM10 values at the moment
					if (cumulus.StationOptions.PrimaryAqSensor == 0)
					{
						sb.Append(",\"pm10\":[");
						for (var i = 0; i < GraphDataList.Count; i++)
						{
							sb.Append($"[{DateTimeToUnix(GraphDataList[i].timestamp) * 1000},{GraphDataList[i].pm10.ToString(cumulus.WindAvgFormat, InvC)}]");
							if (i < GraphDataList.Count - 1)
								sb.Append(",");
						}
						sb.Append("]");
					}
				}
			}

			sb.Append("}");
			return sb.ToString();
		}

		public void AddRecentDataEntry(DateTime timestamp, double windAverage, double recentMaxGust, double windLatest, int bearing, int avgBearing, double outsidetemp,
			double windChill, double dewpoint, double heatIndex, int humidity, double pressure, double rainToday, double solarRad, double uv, double rainCounter, double feelslike, double humidex)
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
					Humidex = humidex
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

			var filename = cumulus.AppDir + cumulus.wxnowfile;
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

			if (cumulus.TempUnit == 0)
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
			if (cumulus.RainUnit == 1)
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
			switch (cumulus.WindUnit)
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

		public void ResetSunshineHours() // called at midnight irrespective of rollover time
		{
			YestSunshineHours = SunshineHours;

			cumulus.LogMessage("Reset sunshine hours, yesterday = " + YestSunshineHours);

			SunshineToMidnight = SunshineHours;
			SunshineHours = 0;
			StartOfDaySunHourCounter = SunHourCounter;
			WriteYesterdayFile();
		}

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
			if (OutdoorHumidity > HighHumidityThisMonth)
			{
				HighHumidityThisMonth = OutdoorHumidity;
				HighHumidityThisMonthTS = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorHumidity < LowHumidityThisMonth)
			{
				LowHumidityThisMonth = OutdoorHumidity;
				LowHumidityThisMonthTS = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorHumidity > HighHumidityThisYear)
			{
				HighHumidityThisYear = OutdoorHumidity;
				HighHumidityThisYearTS = timestamp;
				WriteYearIniFile();
			}
			if (OutdoorHumidity < LowHumidityThisYear)
			{
				LowHumidityThisYear = OutdoorHumidity;
				LowHumidityThisYearTS = timestamp;
				WriteYearIniFile();
			}
			if (OutdoorHumidity > alltimerecarray[AT_HighHumidity].value)
			{
				SetAlltime(AT_HighHumidity, OutdoorHumidity, timestamp);
			}
			CheckMonthlyAlltime(AT_HighHumidity, OutdoorHumidity, true, timestamp);
			if (OutdoorHumidity < alltimerecarray[AT_lowhumidity].value)
			{
				SetAlltime(AT_lowhumidity, OutdoorHumidity, timestamp);
			}
			CheckMonthlyAlltime(AT_lowhumidity, OutdoorHumidity, false, timestamp);
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

			first_temp = false;

			// Does this reading set any records or trigger any alarms?
			if (OutdoorTemperature > alltimerecarray[AT_HighTemp].value)
				SetAlltime(AT_HighTemp, OutdoorTemperature, timestamp);

			cumulus.HighTempAlarm.Triggered = DoAlarm(OutdoorTemperature, cumulus.HighTempAlarm.Value, cumulus.HighTempAlarm.Enabled, true);

			if (OutdoorTemperature < alltimerecarray[AT_LowTemp].value)
				SetAlltime(AT_LowTemp, OutdoorTemperature, timestamp);

			cumulus.LowTempAlarm.Triggered = DoAlarm(OutdoorTemperature, cumulus.LowTempAlarm.Value, cumulus.LowTempAlarm.Enabled, false);

			CheckMonthlyAlltime(AT_HighTemp, OutdoorTemperature, true, timestamp);
			CheckMonthlyAlltime(AT_LowTemp, OutdoorTemperature, false, timestamp);

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

			if (OutdoorTemperature > HighTempThisMonth)
			{
				HighTempThisMonth = OutdoorTemperature;
				HighTempThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (OutdoorTemperature < LowTempThisMonth)
			{
				LowTempThisMonth = OutdoorTemperature;
				LowTempThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (OutdoorTemperature > HighTempThisYear)
			{
				HighTempThisYear = OutdoorTemperature;
				HighTempThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (OutdoorTemperature < LowTempThisYear)
			{
				LowTempThisYear = OutdoorTemperature;
				LowTempThisYearTS = timestamp;
				WriteYearIniFile();
			}

			// Calculate temperature range
			HiLoToday.TempRange = HiLoToday.HighTemp - HiLoToday.LowTemp;

			double tempinC;

			if ((cumulus.StationOptions.CalculatedDP || cumulus.DavisStation) && (OutdoorHumidity != 0) && (!cumulus.FineOffsetStation))
			{
				// Calculate DewPoint.
				tempinC = ConvertUserTempToC(OutdoorTemperature);
				// dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
				OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(tempinC, OutdoorHumidity));

				CheckForDewpointHighLow(timestamp);
			}

			// Calculate cloudbase
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

			tempinC = ConvertUserTempToC(OutdoorTemperature);
			HeatIndex = ConvertTempCToUser(MeteoLib.HeatIndex(tempinC, OutdoorHumidity));

			if (HeatIndex > HiLoToday.HighHeatIndex)
			{
				HiLoToday.HighHeatIndex = HeatIndex;
				HiLoToday.HighHeatIndexTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (HeatIndex > HighHeatIndexThisMonth)
			{
				HighHeatIndexThisMonth = HeatIndex;
				HighHeatIndexThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (HeatIndex > HighHeatIndexThisYear)
			{
				HighHeatIndexThisYear = HeatIndex;
				HighHeatIndexThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (HeatIndex > alltimerecarray[AT_HighHeatIndex].value)
				SetAlltime(AT_HighHeatIndex, HeatIndex, timestamp);

			CheckMonthlyAlltime(AT_HighHeatIndex, HeatIndex, true, timestamp);

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

			if (ApparentTemperature > HighAppTempThisMonth)
			{
				HighAppTempThisMonth = ApparentTemperature;
				HighAppTempThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (ApparentTemperature < LowAppTempThisMonth)
			{
				LowAppTempThisMonth = ApparentTemperature;
				LowAppTempThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (ApparentTemperature > HighAppTempThisYear)
			{
				HighAppTempThisYear = ApparentTemperature;
				HighAppTempThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (ApparentTemperature < LowAppTempThisYear)
			{
				LowAppTempThisYear = ApparentTemperature;
				LowAppTempThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (ApparentTemperature > alltimerecarray[AT_HighAppTemp].value)
				SetAlltime(AT_HighAppTemp, ApparentTemperature, timestamp);

			if (ApparentTemperature < alltimerecarray[AT_LowAppTemp].value)
				SetAlltime(AT_LowAppTemp, ApparentTemperature, timestamp);

			CheckMonthlyAlltime(AT_HighAppTemp, ApparentTemperature, true, timestamp);
			CheckMonthlyAlltime(AT_LowAppTemp, ApparentTemperature, false, timestamp);
			//}
		}

		public void DoWindChill(double chillpar, DateTime timestamp)
		{
			bool chillvalid = true;

			if (cumulus.StationOptions.CalculatedWC)
			{
				// don"t try to calculate windchill if we haven"t yet had wind and temp readings
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

				if (WindChill < LowWindChillThisMonth)
				{
					LowWindChillThisMonth = WindChill;
					LowWindChillThisMonthTS = timestamp;
					WriteMonthIniFile();
				}

				if (WindChill < LowWindChillThisYear)
				{
					LowWindChillThisYear = WindChill;
					LowWindChillThisYearTS = timestamp;
					WriteYearIniFile();
				}

				// All time wind chill
				if (WindChill < alltimerecarray[AT_LowChill].value)
				{
					SetAlltime(AT_LowChill, WindChill, timestamp);
				}

				CheckMonthlyAlltime(AT_LowChill, WindChill, false, timestamp);
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

			if (FeelsLike > HighFeelsLikeThisMonth)
			{
				HighFeelsLikeThisMonth = FeelsLike;
				HighFeelsLikeThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (FeelsLike < LowFeelsLikeThisMonth)
			{
				LowFeelsLikeThisMonth = FeelsLike;
				LowFeelsLikeThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (FeelsLike > HighFeelsLikeThisYear)
			{
				HighFeelsLikeThisYear = FeelsLike;
				HighFeelsLikeThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (FeelsLike < LowFeelsLikeThisYear)
			{
				LowFeelsLikeThisYear = FeelsLike;
				LowFeelsLikeThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (FeelsLike > alltimerecarray[AT_HighFeelsLike].value)
				SetAlltime(AT_HighFeelsLike, FeelsLike, timestamp);

			if (FeelsLike < alltimerecarray[AT_LowFeelsLike].value)
				SetAlltime(AT_LowFeelsLike, FeelsLike, timestamp);

			CheckMonthlyAlltime(AT_HighFeelsLike, FeelsLike, true, timestamp);
			CheckMonthlyAlltime(AT_LowFeelsLike, FeelsLike, false, timestamp);
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

			if (Humidex > HighHumidexThisMonth)
			{
				HighHumidexThisMonth = Humidex;
				HighHumidexThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (Humidex > HighHumidexThisYear)
			{
				HighHumidexThisYear = Humidex;
				HighHumidexThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (Humidex > alltimerecarray[AT_HighHumidex].value)
				SetAlltime(AT_HighHumidex, Humidex, timestamp);

			CheckMonthlyAlltime(AT_HighHumidex, Humidex, true, timestamp);
		}

		public void CheckForWindrunHighLow(DateTime timestamp)
		{
			DateTime adjustedtimestamp = timestamp.AddHours(cumulus.GetHourInc());

			if (WindRunToday > HighDailyWindrunThisMonth)
			{
				HighDailyWindrunThisMonth = WindRunToday;
				HighDailyWindrunThisMonthTS = adjustedtimestamp;
				WriteMonthIniFile();
			}

			if (WindRunToday > HighDailyWindrunThisYear)
			{
				HighDailyWindrunThisYear = WindRunToday;
				HighDailyWindrunThisYearTS = adjustedtimestamp;
				WriteYearIniFile();
			}

			if (WindRunToday > alltimerecarray[AT_HighWindrun].value)
			{
				SetAlltime(AT_HighWindrun, WindRunToday, adjustedtimestamp);
			}

			CheckMonthlyAlltime(AT_HighWindrun, WindRunToday, true, adjustedtimestamp);
		}

		public void CheckForDewpointHighLow(DateTime timestamp)
		{
			if (OutdoorDewpoint > HiLoToday.HighDewpoint)
			{
				HiLoToday.HighDewpoint = OutdoorDewpoint;
				HiLoToday.HighDewpointTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorDewpoint < HiLoToday.LowDewpoint)
			{
				HiLoToday.LowDewpoint = OutdoorDewpoint;
				HiLoToday.LowDewpointTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorDewpoint > HighDewpointThisMonth)
			{
				HighDewpointThisMonth = OutdoorDewpoint;
				HighDewpointThisMonthTS = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorDewpoint < LowDewpointThisMonth)
			{
				LowDewpointThisMonth = OutdoorDewpoint;
				LowDewpointThisMonthTS = timestamp;
				WriteMonthIniFile();
			}
			if (OutdoorDewpoint > HighDewpointThisYear)
			{
				HighDewpointThisYear = OutdoorDewpoint;
				HighDewpointThisYearTS = timestamp;
				WriteYearIniFile();
			}
			if (OutdoorDewpoint < LowDewpointThisYear)
			{
				LowDewpointThisYear = OutdoorDewpoint;
				LowDewpointThisYearTS = timestamp;
				WriteYearIniFile();
			}
			;
			if (OutdoorDewpoint > alltimerecarray[AT_HighDewPoint].value)
			{
				SetAlltime(AT_HighDewPoint, OutdoorDewpoint, timestamp);
			}
			if (OutdoorDewpoint < alltimerecarray[AT_LowDewpoint].value)
				SetAlltime(AT_LowDewpoint, OutdoorDewpoint, timestamp);

			CheckMonthlyAlltime(AT_HighDewPoint, OutdoorDewpoint, true, timestamp);
			CheckMonthlyAlltime(AT_LowDewpoint, OutdoorDewpoint, false, timestamp);
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
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousPress = pressMB;

			Pressure = sl * cumulus.Calib.Press.Mult + cumulus.Calib.Press.Offset;
			if (cumulus.Manufacturer == cumulus.DAVIS)
			{
				if (!cumulus.UseDavisLoop2)
				{
					// Loop2 data not available, just use sea level (for now, anyway)
					AltimeterPressure = Pressure;
				}
			}
			else
			{
				if (cumulus.Manufacturer == cumulus.OREGONUSB)
				{
					AltimeterPressure = ConvertPressMBToUser(StationToAltimeter(PressureHPa(StationPressure), AltitudeM(cumulus.Altitude)));
				}
				else
				{
					// For all other stations, altimeter is same as sea-level
					AltimeterPressure = Pressure;
				}
			}

			first_press = false;

			if (Pressure > alltimerecarray[AT_HighPress].value)
			{
				SetAlltime(AT_HighPress, Pressure, timestamp);
			}

			cumulus.HighPressAlarm.Triggered = DoAlarm(Pressure, cumulus.HighPressAlarm.Value, cumulus.HighPressAlarm.Enabled, true);

			if (Pressure < alltimerecarray[AT_LowPress].value)
			{
				SetAlltime(AT_LowPress, Pressure, timestamp);
			}

			cumulus.LowPressAlarm.Triggered = DoAlarm(Pressure, cumulus.LowPressAlarm.Value, cumulus.LowPressAlarm.Enabled, false);
			CheckMonthlyAlltime(AT_LowPress, Pressure, false, timestamp);
			CheckMonthlyAlltime(AT_HighPress, Pressure, true, timestamp);

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

			if (Pressure > HighPressThisMonth)
			{
				HighPressThisMonth = Pressure;
				HighPressThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (Pressure < LowPressThisMonth)
			{
				LowPressThisMonth = Pressure;
				LowPressThisMonthTS = timestamp;
				WriteMonthIniFile();
			}

			if (Pressure > HighPressThisYear)
			{
				HighPressThisYear = Pressure;
				HighPressThisYearTS = timestamp;
				WriteYearIniFile();
			}

			if (Pressure < LowPressThisYear)
			{
				LowPressThisYear = Pressure;
				LowPressThisYearTS = timestamp;
				WriteYearIniFile();
			}

			PressReadyToPlot = true;
			HaveReadData = true;
		}

		protected void DoPressTrend(string trend)
		{
			if (cumulus.StationOptions.UseCumulusPresstrendstr || cumulus.Manufacturer == cumulus.DAVIS)
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

			// Spike removal is in mm
			var rainRateMM = ConvertUserRainToMM(rate);
			if (rainRateMM > cumulus.Spike.MaxRainRate)
			{
				cumulus.LogSpikeRemoval("Rain rate greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Rate value = {rainRateMM:F2} SpikeMaxRainRate = {cumulus.Spike.MaxRainRate:F2}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			if ((CurrentDay != readingTS.Day) || (CurrentMonth != readingTS.Month) || (CurrentYear != readingTS.Year))
			{
				// A reading has apparently arrived at the start of a new day, but before we have done the rollover
				// Ignore it, as otherwise it may cause a new monthly record to be logged using last month's total
				return;
			}

			var previoustotal = Raincounter;

			double raintipthreshold;
			if (cumulus.Manufacturer == cumulus.DAVIS)  // Davis can have either 0.2mm or 0.01in buckets, and the user could select to measure in mm or inches!
			{
				// If the bucket size is set, use that, otherwise infer from rain units
				var bucketSize = cumulus.VPrainGaugeType == -1 ? cumulus.RainUnit : cumulus.VPrainGaugeType;

				if (bucketSize == 0) // 0.2 mm tips
				{
					// mm/mm (0.2) or mm/in (0.00787)
					raintipthreshold = cumulus.RainUnit == 0 ? 0.19 : 0.006;
				}
				else // 0.01 inch tips
				{
					// in/mm (0.254) or in/in (0.01)
					raintipthreshold = cumulus.RainUnit == 0 ? 0.2 : 0.009;
				}
			}
			else
			{
				if (cumulus.RainUnit == 0)
				{
					// mm
					raintipthreshold = cumulus.Manufacturer == cumulus.INSTROMET ? 0.009 : 0.09;
				}
				else
				{
					// in
					raintipthreshold = cumulus.Manufacturer == cumulus.INSTROMET ? 0.0003 : 0.009;
				}
			}

			if (total - Raincounter > raintipthreshold)
			{
				// rain has occurred
				LastRainTip = timestamp.ToString("yyyy-MM-dd HH:mm");
			}

			Raincounter = total;

			//first_rain = false;
			if (notraininit)
			{
				raindaystart = Raincounter;
				midnightraincount = Raincounter;
				cumulus.LogMessage(" First rain data, raindaystart = " + raindaystart);

				notraininit = false;
				WriteTodayFile(timestamp, false);
			}
			else
			{
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

						FirstChanceRainReset = false;
					}
					else
					{
						cumulus.LogMessage(" ****Rain reset? First chance: raindaystart = " + raindaystart + ", Raincounter = " + Raincounter);

						// reset the counter to ignore this reading
						Raincounter = previoustotal;
						cumulus.LogMessage("Leaving counter at " + Raincounter);

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

					if (RainRate > alltimerecarray[AT_HighRainRate].value)
						SetAlltime(AT_HighRainRate, RainRate, timestamp);

					CheckMonthlyAlltime(AT_HighRainRate, RainRate, true, timestamp);

					cumulus.HighRainRateAlarm.Triggered = DoAlarm(RainRate, cumulus.HighRainRateAlarm.Value, cumulus.HighRainRateAlarm.Enabled, true);

					if (RainRate > HiLoToday.HighRain)
					{
						HiLoToday.HighRain = RainRate;
						HiLoToday.HighRainTime = timestamp;
						WriteTodayFile(timestamp, false);
					}

					if (RainRate > HighRainThisMonth)
					{
						HighRainThisMonth = RainRate;
						HighRainThisMonthTS = timestamp;
						WriteMonthIniFile();
					}

					if (RainRate > HighRainThisYear)
					{
						HighRainThisYear = RainRate;
						HighRainThisYearTS = timestamp;
						WriteYearIniFile();
					}
				}

				if (!FirstChanceRainReset)
				{
					// Calculate today"s rainfall
					RainToday = Raincounter - raindaystart;

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

					if (RainToday > alltimerecarray[AT_DailyRain].value)
						SetAlltime(AT_DailyRain, RainToday, offsetdate);

					CheckMonthlyAlltime(AT_DailyRain, RainToday, true, timestamp);

					if (RainToday > HighDailyRainThisMonth)
					{
						HighDailyRainThisMonth = RainToday;
						HighDailyRainThisMonthTS = offsetdate;
						WriteMonthIniFile();
					}

					if (RainToday > HighDailyRainThisYear)
					{
						HighDailyRainThisYear = RainToday;
						HighDailyRainThisYearTS = offsetdate;
						WriteYearIniFile();
					}

					if (RainMonth > HighMonthlyRainThisYear)
					{
						HighMonthlyRainThisYear = RainMonth;
						HighMonthlyRainThisYearTS = offsetdate;
						WriteYearIniFile();
					}

					if (RainMonth > alltimerecarray[AT_WetMonth].value)
						SetAlltime(AT_WetMonth, RainMonth, offsetdate);

					CheckMonthlyAlltime(AT_WetMonth, RainMonth, true, timestamp);

					cumulus.HighRainTodayAlarm.Triggered = DoAlarm(RainToday, cumulus.HighRainTodayAlarm.Value, cumulus.HighRainTodayAlarm.Enabled, true);

					// Yesterday"s rain - Scale for units
					// rainyest = rainyesterday * RainMult;

					//RainReadyToPlot = true;
				}
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
					lastSpikeRemoval = DateTime.Now;
					cumulus.SpikeAlarm.Triggered = true;
					cumulus.LogSpikeRemoval($"Dew point greater than limit ({cumulus.Limit.DewHigh.ToString(cumulus.TempFormat)}); reading ignored: {dp.ToString(cumulus.TempFormat)}");
				}
			}
		}

		public string LastRainTip { get; set; }

		public void DoExtraHum(double hum, int channel)
		{
			if ((channel > 0) && (channel < 11))
			{
				ExtraHum[channel] = (int)hum;
			}
		}

		public void DoExtraTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < 11))
			{
				ExtraTemp[channel] = temp;
			}
		}

		public void DoUserTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < 11))
			{
				UserTemp[channel] = temp;
			}
		}


		public void DoExtraDP(double dp, int channel)
		{
			if ((channel > 0) && (channel < 11))
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

				CumulusForecast = BetelCast(PressureHPa(Pressure), DateTime.Now.Month, windDir, bartrend, cumulus.Latitude > 0, hp, lp);

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
		public double PressureHPa(double value)
		{
			if (cumulus.PressUnit == 2)
				return value / 0.0295333727;
			else
				return value;
		}

		public double StationToAltimeter(double pressureHPa, double elevationM)
		{
			// from MADIS API by NOAA Forecast Systems Lab, see http://madis.noaa.gov/madis_api.html

			double k1 = 0.190284; // discrepency with calculated k1 probably because Smithsonian used less precise gas constant and gravity values
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
			if (calibratedgust > HighGustThisMonth)
			{
				HighGustThisMonth = calibratedgust;
				HighGustThisMonthTS = timestamp;
				WriteMonthIniFile();
			}
			if (calibratedgust > HighGustThisYear)
			{
				HighGustThisYear = calibratedgust;
				HighGustThisYearTS = timestamp;
				WriteYearIniFile();
			}
			// All time high gust?
			if (calibratedgust > alltimerecarray[AT_HighGust].value)
			{
				SetAlltime(AT_HighGust, calibratedgust, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime(AT_HighGust, calibratedgust, true, timestamp);

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
			if (WindAverage > HighWindThisMonth)
			{
				HighWindThisMonth = WindAverage;
				HighWindThisMonthTS = timestamp;
				WriteMonthIniFile();
			}
			if (WindAverage > HighWindThisYear)
			{
				HighWindThisYear = WindAverage;
				HighWindThisYearTS = timestamp;
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
			if (WindAverage > alltimerecarray[AT_HighWind].value)
			{
				SetAlltime(AT_HighWind, WindAverage, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime(AT_HighWind, WindAverage, true, timestamp);

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

		protected void DoSolarRad(int value, DateTime timestamp)
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
			if (StartOfDaySunHourCounter < -998)
			{
				cumulus.LogMessage("No start of day sun counter. Start counting from now");
				StartOfDaySunHourCounter = hrs;
			}

			if (hrs < SunHourCounter)
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

		public void SetAlltime(int index, double value, DateTime timestamp)
		{
			lock (alltimeIniThreadLock)
			{
				double oldvalue = alltimerecarray[index].value;
				DateTime oldts = alltimerecarray[index].timestamp;

				alltimerecarray[index].data_type = index;
				alltimerecarray[index].value = value;

				alltimerecarray[index].timestamp = timestamp;

				WriteAlltimeIniFile();

				AlltimeRecordTimestamp = timestamp;

				// add an entry to the log. date/time/value/item/old date/old time/old value
				// dates in ISO format, times always have a colon. Example:
				// 2010-02-24 05:19 -7.6 "Lowest temperature" 2009-02-09 04:50 -6.5
				var sb = new StringBuilder("New all-time record: New time = ", 100);
				sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", alltimerecarray[index].timestamp));
				sb.Append(", new value = ");
				sb.Append(String.Format("{0,7:0.000}", value));
				sb.Append(" \"");
				sb.Append(alltimedescs[index]);
				sb.Append("\" prev time = ");
				sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", oldts));
				sb.Append(", prev value = ");
				sb.Append(String.Format("{0,7:0.000}", oldvalue));

				cumulus.LogMessage(sb.ToString());

				sb.Append(Environment.NewLine);
				File.AppendAllText(cumulus.Alltimelogfile, sb.ToString());
			}
		}

		public void SetMonthlyAlltime(int index, double value, DateTime timestamp, int month)
		{
			double oldvalue = monthlyrecarray[index, month].value;
			DateTime oldts = monthlyrecarray[index, month].timestamp;

			monthlyrecarray[index, month].data_type = index;
			monthlyrecarray[index, month].value = value;
			monthlyrecarray[index, month].timestamp = timestamp;

			WriteMonthlyAlltimeIniFile();

			var sb = new StringBuilder("New monthly record: month = ", 200);
			sb.Append(month.ToString("D2"));
			sb.Append(": New time = ");
			sb.Append(FormatDateTime("yyy-MM-dd HH:mm", timestamp));
			sb.Append(", new value = ");
			sb.Append(value.ToString("F3"));
			sb.Append(" \"");
			sb.Append(alltimedescs[index]);
			sb.Append("\" prev time = ");
			sb.Append(FormatDateTime("yyyy-MM-dd HH:mm", oldts));
			sb.Append(", prev value = ");
			sb.Append(oldvalue.ToString("F3"));
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

		// this month highs and lows
		public double HighGustThisMonth;
		public double HighWindThisMonth;
		public double HighTempThisMonth;
		public double LowTempThisMonth;
		public double HighAppTempThisMonth;
		public double LowAppTempThisMonth;
		public double HighFeelsLikeThisMonth;
		public double LowFeelsLikeThisMonth;
		public double HighHumidexThisMonth;
		public double HighPressThisMonth;
		public double LowPressThisMonth;
		public double HighRainThisMonth;
		public double HighHourlyRainThisMonth;
		public double HighDailyRainThisMonth;
		public int HighHumidityThisMonth;
		public int LowHumidityThisMonth;
		public double HighHeatIndexThisMonth;
		public double LowWindChillThisMonth;
		public double HighMinTempThisMonth;
		public double LowMaxTempThisMonth;
		public double HighDewpointThisMonth;
		public double LowDewpointThisMonth;
		public double HighDailyWindrunThisMonth;
		public int LongestDryPeriodThisMonth;
		public int LongestWetPeriodThisMonth;
		public double LowDailyTempRangeThisMonth;
		public double HighDailyTempRangeThisMonth;

		// this month highs and lows - timestamps
		public DateTime HighGustThisMonthTS;
		public DateTime HighWindThisMonthTS;
		public DateTime HighTempThisMonthTS;
		public DateTime LowTempThisMonthTS;
		public DateTime HighAppTempThisMonthTS;
		public DateTime LowAppTempThisMonthTS;
		public DateTime HighFeelsLikeThisMonthTS;
		public DateTime LowFeelsLikeThisMonthTS;
		public DateTime HighHumidexThisMonthTS;
		public DateTime HighPressThisMonthTS;
		public DateTime LowPressThisMonthTS;
		public DateTime HighRainThisMonthTS;
		public DateTime HighHourlyRainThisMonthTS;
		public DateTime HighDailyRainThisMonthTS;
		public DateTime HighHumidityThisMonthTS;
		public DateTime LowHumidityThisMonthTS;
		public DateTime HighHeatIndexThisMonthTS;
		public DateTime LowWindChillThisMonthTS;
		public DateTime HighMinTempThisMonthTS;
		public DateTime LowMaxTempThisMonthTS;
		public DateTime HighDewpointThisMonthTS;
		public DateTime LowDewpointThisMonthTS;
		public DateTime HighDailyWindrunThisMonthTS;
		public DateTime LongestDryPeriodThisMonthTS;
		public DateTime LongestWetPeriodThisMonthTS;
		public DateTime LowDailyTempRangeThisMonthTS;
		public DateTime HighDailyTempRangeThisMonthTS;

		// this Year highs and lows
		public double HighGustThisYear;
		public double HighWindThisYear;
		public double HighTempThisYear;
		public double LowTempThisYear;
		public double HighAppTempThisYear;
		public double LowAppTempThisYear;
		public double HighFeelsLikeThisYear;
		public double LowFeelsLikeThisYear;
		public double HighHumidexThisYear;
		public double HighPressThisYear;
		public double LowPressThisYear;
		public double HighRainThisYear;
		public double HighHourlyRainThisYear;
		public double HighDailyRainThisYear;
		public double HighMonthlyRainThisYear;
		public int HighHumidityThisYear;
		public int LowHumidityThisYear;
		public double HighHeatIndexThisYear;
		public double LowWindChillThisYear;
		public double HighMinTempThisYear;
		public double LowMaxTempThisYear;
		public double HighDewpointThisYear;
		public double LowDewpointThisYear;
		public double HighDailyWindrunThisYear;
		public int LongestDryPeriodThisYear;
		public int LongestWetPeriodThisYear;
		public double LowDailyTempRangeThisYear;
		public double HighDailyTempRangeThisYear;

		// this Year highs and lows - timestamps
		public DateTime HighGustThisYearTS;
		public DateTime HighWindThisYearTS;
		public DateTime HighTempThisYearTS;
		public DateTime LowTempThisYearTS;
		public DateTime HighAppTempThisYearTS;
		public DateTime LowAppTempThisYearTS;
		public DateTime HighFeelsLikeThisYearTS;
		public DateTime LowFeelsLikeThisYearTS;
		public DateTime HighHumidexThisYearTS;
		public DateTime HighPressThisYearTS;
		public DateTime LowPressThisYearTS;
		public DateTime HighRainThisYearTS;
		public DateTime HighHourlyRainThisYearTS;
		public DateTime HighDailyRainThisYearTS;
		public DateTime HighMonthlyRainThisYearTS;
		public DateTime HighHumidityThisYearTS;
		public DateTime LowHumidityThisYearTS;
		public DateTime HighHeatIndexThisYearTS;
		public DateTime LowWindChillThisYearTS;
		public DateTime HighMinTempThisYearTS;
		public DateTime LowMaxTempThisYearTS;
		public DateTime HighDewpointThisYearTS;
		public DateTime LowDewpointThisYearTS;
		public DateTime HighDailyWindrunThisYearTS;
		public DateTime LongestDryPeriodThisYearTS;
		public DateTime LongestWetPeriodThisYearTS;
		public DateTime LowDailyTempRangeThisYearTS;
		public DateTime HighDailyTempRangeThisYearTS;
		//private bool first_rain = true;
		private bool FirstChanceRainReset = false;
		public bool notraininit = true;
		//private bool RainReadyToPlot = false;
		private double rainthismonth = 0;
		private double rainthisyear = 0;
		//private bool WindChillReadyToPlot = false;
		public bool noET = false;
		private int DayResetDay = 0;
		protected bool FirstRun = false;
		public const int MaxWindRecent = 720;
		protected readonly double[] WindRunHourMult = { 3.6, 1.0, 1.0, 1.0 };
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
			ini.SetValue("Temp", "HeatingDegreeDays", YestHeatingDegreeDays);
			ini.SetValue("Temp", "CoolingDegreeDays", YestCoolingDegreeDays);
			ini.SetValue("Temp", "AvgTemp", YestAvgTemp);
			// Pressure
			ini.SetValue("Pressure", "Low", HiLoYest.LowPress);
			ini.SetValue("Pressure", "LTime", HiLoYest.LowPressTime.ToString("HH:mm"));
			ini.SetValue("Pressure", "High", HiLoYest.HighPress);
			ini.SetValue("Pressure", "HTime", HiLoYest.HighPressTime.ToString("HH:mm"));
			// rain rate
			ini.SetValue("Rain", "High", HiLoYest.HighRain);
			ini.SetValue("Rain", "HTime", HiLoYest.HighRainTime.ToString("HH:mm"));
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
			ini.SetValue("Dewpoint", "Low", HiLoYest.LowDewpoint);
			ini.SetValue("Dewpoint", "LTime", HiLoYest.LowDewpointTime.ToString("HH:mm"));
			ini.SetValue("Dewpoint", "High", HiLoYest.HighDewpoint);
			ini.SetValue("Dewpoint", "HTime", HiLoYest.HighDewpointTime.ToString("HH:mm"));
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
			HiLoYest.HighRain = ini.GetValue("Rain", "High", 0.0);
			HiLoYest.HighRainTime = ini.GetValue("Rain", "HTime", DateTime.MinValue);
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
			HiLoYest.LowDewpoint = ini.GetValue("Dewpoint", "Low", 0.0);
			HiLoYest.LowDewpointTime = ini.GetValue("Dewpoint", "LTime", DateTime.MinValue);
			HiLoYest.HighDewpoint = ini.GetValue("Dewpoint", "High", 0.0);
			HiLoYest.HighDewpointTime = ini.GetValue("Dewpoint", "HTime", DateTime.MinValue);
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

				if (cumulus.CustomMySqlRolloverEnabled)
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

				AddRecentDailyData(timestamp.AddDays(-1), RainYesterday, (cumulus.RolloverHour == 0 ? SunshineHours : SunshineToMidnight), HiLoToday.LowTemp, HiLoToday.HighTemp, YestAvgTemp);
				RemoveOldRecentDailyData();

				int rdthresh1000;
				if (cumulus.RainDayThreshold < 0)
				// default
				{
					if (cumulus.RainUnit == 0)
					{
						rdthresh1000 = 200; // 0.2mm *1000
					}
					else
					{
						rdthresh1000 = 10; // 0.01in *1000
					}
				}
				else
				{
					rdthresh1000 = Convert.ToInt32(cumulus.RainDayThreshold * 1000.0);
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
					if (ConsecutiveRainDays > LongestWetPeriodThisMonth)
					{
						LongestWetPeriodThisMonth = ConsecutiveRainDays;
						LongestWetPeriodThisMonthTS = yesterday;
						WriteMonthIniFile();
					}

					if (ConsecutiveRainDays > LongestWetPeriodThisYear)
					{
						LongestWetPeriodThisYear = ConsecutiveRainDays;
						LongestWetPeriodThisYearTS = yesterday;
						WriteYearIniFile();
					}

					if (ConsecutiveRainDays > alltimerecarray[AT_LongestWetPeriod].value)
						SetAlltime(AT_LongestWetPeriod, ConsecutiveRainDays, yesterday);

					CheckMonthlyAlltime(AT_LongestWetPeriod, ConsecutiveRainDays, true, yesterday);
				}
				else
				{
					// It didn't rain yesterday
					cumulus.LogMessage("Yesterday was a dry day");
					ConsecutiveDryDays++;
					ConsecutiveRainDays = 0;
					cumulus.LogMessage("Consecutive dry days = " + ConsecutiveDryDays);

					// check for highs
					if (ConsecutiveDryDays > LongestDryPeriodThisMonth)
					{
						LongestDryPeriodThisMonth = ConsecutiveDryDays;
						LongestDryPeriodThisMonthTS = yesterday;
						WriteMonthIniFile();
					}

					if (ConsecutiveDryDays > LongestDryPeriodThisYear)
					{
						LongestDryPeriodThisYear = ConsecutiveDryDays;
						LongestDryPeriodThisYearTS = yesterday;
						WriteYearIniFile();
					}

					if (ConsecutiveDryDays > alltimerecarray[AT_LongestDryPeriod].value)
						SetAlltime(AT_LongestDryPeriod, ConsecutiveDryDays, yesterday);

					CheckMonthlyAlltime(AT_LongestDryPeriod, ConsecutiveDryDays, true, yesterday);
				}

				// offset high temp today timestamp to allow for 0900 rollover
				int hr;
				int mn;
				DateTime ts;
				try
				{
					hr = HiLoToday.HighTempTime.Hour;
					mn = HiLoToday.HighTempTime.Minute;
					ts = timestamp.Date + new TimeSpan(hr, mn, 0);

					if (hr >= cumulus.RolloverHour)
						// time is between rollover hour && midnight
						// so subtract a day
						ts = ts.AddDays(-1);
				}
				catch
				{
					ts = timestamp.AddDays(-1);
				}

				if (HiLoToday.HighTemp < alltimerecarray[AT_LowMaxTemp].value)
				{
					SetAlltime(AT_LowMaxTemp, HiLoToday.HighTemp, ts);
				}

				CheckMonthlyAlltime(AT_LowMaxTemp, HiLoToday.HighTemp, false, ts);

				if (HiLoToday.HighTemp < LowMaxTempThisMonth)
				{
					LowMaxTempThisMonth = HiLoToday.HighTemp;
					try
					{
						hr = HiLoToday.HighTempTime.Hour;
						mn = HiLoToday.HighTempTime.Minute;
						LowMaxTempThisMonthTS = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between rollover hour && midnight
							// so subtract a day
							LowMaxTempThisMonthTS = LowMaxTempThisMonthTS.AddDays(-1);
					}
					catch
					{
						LowMaxTempThisMonthTS = timestamp.AddDays(-1);
					}

					WriteMonthIniFile();
				}

				if (HiLoToday.HighTemp < LowMaxTempThisYear)
				{
					LowMaxTempThisYear = HiLoToday.HighTemp;
					try
					{
						hr = HiLoToday.HighTempTime.Hour;
						mn = HiLoToday.HighTempTime.Minute;
						LowMaxTempThisYearTS = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between rollover hour && midnight
							// so subtract a day
							LowMaxTempThisYearTS = LowMaxTempThisYearTS.AddDays(-1);
					}
					catch
					{
						LowMaxTempThisYearTS = timestamp.AddDays(-1);
					}

					WriteYearIniFile();
				}

				// offset low temp today timestamp to allow for 0900 rollover
				try
				{
					hr = HiLoToday.LowTempTime.Hour;
					mn = HiLoToday.LowTempTime.Minute;
					ts = timestamp.Date + new TimeSpan(hr, mn, 0);

					if (hr >= cumulus.RolloverHour)
						// time is between rollover hour && midnight
						// so subtract a day
						ts = ts.AddDays(-1);
				}
				catch
				{
					ts = timestamp.AddDays(-1);
				}

				if (HiLoToday.LowTemp > alltimerecarray[AT_HighMinTemp].value)
				{
					SetAlltime(AT_HighMinTemp, HiLoToday.LowTemp, ts);
				}

				CheckMonthlyAlltime(AT_HighMinTemp, HiLoToday.LowTemp, true, ts);

				if (HiLoToday.LowTemp > HighMinTempThisMonth)
				{
					HighMinTempThisMonth = HiLoToday.LowTemp;
					try
					{
						hr = HiLoToday.LowTempTime.Hour;
						mn = HiLoToday.LowTempTime.Minute;
						HighMinTempThisMonthTS = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between rollover hour && midnight
							// so subtract a day
							HighMinTempThisMonthTS = HighMinTempThisMonthTS.AddDays(-1);
					}
					catch
					{
						HighMinTempThisMonthTS = timestamp.AddDays(-1);
					}
					WriteMonthIniFile();
				}

				if (HiLoToday.LowTemp > HighMinTempThisYear)
				{
					HighMinTempThisYear = HiLoToday.LowTemp;
					try
					{
						hr = HiLoToday.LowTempTime.Hour;
						mn = HiLoToday.LowTempTime.Minute;
						HighMinTempThisYearTS = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.RolloverHour)
							// time is between rollover hour && midnight
							// so subtract a day
							HighMinTempThisYearTS = HighMinTempThisYearTS.AddDays(-1);
					}
					catch
					{
						HighMinTempThisYearTS = timestamp.AddDays(-1);
					}
					WriteYearIniFile();
				}

				// check temp range for highs && lows
				if (HiLoToday.TempRange > alltimerecarray[AT_HighDailyTempRange].value)
					SetAlltime(AT_HighDailyTempRange, HiLoToday.TempRange, yesterday);

				if (HiLoToday.TempRange < alltimerecarray[AT_LowDailyTempRange].value)
					SetAlltime(AT_LowDailyTempRange, HiLoToday.TempRange, yesterday);

				CheckMonthlyAlltime(AT_HighDailyTempRange, HiLoToday.TempRange, true, yesterday);
				CheckMonthlyAlltime(AT_LowDailyTempRange, HiLoToday.TempRange, false, yesterday);

				if (HiLoToday.TempRange > HighDailyTempRangeThisMonth)
				{
					HighDailyTempRangeThisMonth = HiLoToday.TempRange;
					HighDailyTempRangeThisMonthTS = yesterday;
					WriteMonthIniFile();
				}

				if (HiLoToday.TempRange < LowDailyTempRangeThisMonth)
				{
					LowDailyTempRangeThisMonth = HiLoToday.TempRange;
					LowDailyTempRangeThisMonthTS = yesterday;
					WriteMonthIniFile();
				}

				if (HiLoToday.TempRange > HighDailyTempRangeThisYear)
				{
					HighDailyTempRangeThisYear = HiLoToday.TempRange;
					HighDailyTempRangeThisYearTS = yesterday;
					WriteYearIniFile();
				}

				if (HiLoToday.TempRange < LowDailyTempRangeThisYear)
				{
					LowDailyTempRangeThisYear = HiLoToday.TempRange;
					LowDailyTempRangeThisYearTS = yesterday;
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

					HighGustThisMonth = calibratedgust;
					HighWindThisMonth = WindAverage;
					HighTempThisMonth = OutdoorTemperature;
					LowTempThisMonth = OutdoorTemperature;
					HighAppTempThisMonth = ApparentTemperature;
					LowAppTempThisMonth = ApparentTemperature;
					HighFeelsLikeThisMonth = FeelsLike;
					LowFeelsLikeThisMonth = FeelsLike;
					HighHumidexThisMonth = Humidex;
					HighPressThisMonth = Pressure;
					LowPressThisMonth = Pressure;
					HighRainThisMonth = RainRate;
					HighHourlyRainThisMonth = RainLastHour;
					HighDailyRainThisMonth = 0;
					HighHumidityThisMonth = OutdoorHumidity;
					LowHumidityThisMonth = OutdoorHumidity;
					HighHeatIndexThisMonth = HeatIndex;
					LowWindChillThisMonth = WindChill;
					HighMinTempThisMonth = -999;
					LowMaxTempThisMonth = 999;
					HighDewpointThisMonth = OutdoorDewpoint;
					LowDewpointThisMonth = OutdoorDewpoint;
					HighDailyWindrunThisMonth = 0;
					LongestDryPeriodThisMonth = 0;
					LongestWetPeriodThisMonth = 0;
					HighDailyTempRangeThisMonth = -999;
					LowDailyTempRangeThisMonth = 999;

					// this month highs && lows - timestamps
					HighGustThisMonthTS = timestamp;
					HighWindThisMonthTS = timestamp;
					HighTempThisMonthTS = timestamp;
					LowTempThisMonthTS = timestamp;
					HighAppTempThisMonthTS = timestamp;
					LowAppTempThisMonthTS = timestamp;
					HighFeelsLikeThisMonthTS = timestamp;
					LowFeelsLikeThisMonthTS = timestamp;
					HighHumidexThisMonthTS = timestamp;
					HighPressThisMonthTS = timestamp;
					LowPressThisMonthTS = timestamp;
					HighRainThisMonthTS = timestamp;
					HighHourlyRainThisMonthTS = timestamp;
					HighDailyRainThisMonthTS = timestamp;
					HighHumidityThisMonthTS = timestamp;
					LowHumidityThisMonthTS = timestamp;
					HighHeatIndexThisMonthTS = timestamp;
					LowWindChillThisMonthTS = timestamp;
					HighMinTempThisMonthTS = timestamp;
					LowMaxTempThisMonthTS = timestamp;
					HighDewpointThisMonthTS = timestamp;
					LowDewpointThisMonthTS = timestamp;
					HighDailyWindrunThisMonthTS = timestamp;
					LongestDryPeriodThisMonthTS = timestamp;
					LongestWetPeriodThisMonthTS = timestamp;
					LowDailyTempRangeThisMonthTS = timestamp;
					HighDailyTempRangeThisMonthTS = timestamp;
				}
				else
					rainthismonth += RainYesterday;

				if ((day == 1) && (month == 1))
				{
					// new year starting
					cumulus.LogMessage(" New year starting");

					CopyYearIniFile(timestamp.AddDays(-1));

					HighGustThisYear = calibratedgust;
					HighWindThisYear = WindAverage;
					HighTempThisYear = OutdoorTemperature;
					LowTempThisYear = OutdoorTemperature;
					HighAppTempThisYear = ApparentTemperature;
					LowAppTempThisYear = ApparentTemperature;
					HighFeelsLikeThisYear = FeelsLike;
					LowFeelsLikeThisYear = FeelsLike;
					HighHumidexThisYear = Humidex;
					HighPressThisYear = Pressure;
					LowPressThisYear = Pressure;
					HighRainThisYear = RainRate;
					HighHourlyRainThisYear = RainLastHour;
					HighDailyRainThisYear = 0;
					HighMonthlyRainThisYear = 0;
					HighHumidityThisYear = OutdoorHumidity;
					LowHumidityThisYear = OutdoorHumidity;
					HighHeatIndexThisYear = HeatIndex;
					LowWindChillThisYear = WindChill;
					HighMinTempThisYear = -999;
					LowMaxTempThisYear = 999;
					HighDewpointThisYear = OutdoorDewpoint;
					LowDewpointThisYear = OutdoorDewpoint;
					HighDailyWindrunThisYear = 0;
					LongestDryPeriodThisYear = 0;
					LongestWetPeriodThisYear = 0;
					HighDailyTempRangeThisYear = -999;
					LowDailyTempRangeThisYear = 999;

					// this Year highs && lows - timestamps
					HighGustThisYearTS = timestamp;
					HighWindThisYearTS = timestamp;
					HighTempThisYearTS = timestamp;
					LowTempThisYearTS = timestamp;
					HighAppTempThisYearTS = timestamp;
					LowAppTempThisYearTS = timestamp;
					HighFeelsLikeThisYearTS = timestamp;
					LowFeelsLikeThisYearTS = timestamp;
					HighHumidexThisYearTS = timestamp;
					HighPressThisYearTS = timestamp;
					LowPressThisYearTS = timestamp;
					HighRainThisYearTS = timestamp;
					HighHourlyRainThisYearTS = timestamp;
					HighDailyRainThisYearTS = timestamp;
					HighMonthlyRainThisYearTS = timestamp;
					HighHumidityThisYearTS = timestamp;
					LowHumidityThisYearTS = timestamp;
					HighHeatIndexThisYearTS = timestamp;
					LowWindChillThisYearTS = timestamp;
					HighMinTempThisYearTS = timestamp;
					LowMaxTempThisYearTS = timestamp;
					HighDewpointThisYearTS = timestamp;
					LowDewpointThisYearTS = timestamp;
					HighDailyWindrunThisYearTS = timestamp;
					LongestDryPeriodThisYearTS = timestamp;
					LongestWetPeriodThisYearTS = timestamp;
					HighDailyTempRangeThisYearTS = timestamp;
					LowDailyTempRangeThisYearTS = timestamp;
				}

				if ((day == 1) && (month == cumulus.RainSeasonStart))
				{
					// new year starting
					cumulus.LogMessage(" New rain season starting");
					rainthisyear = 0;
				}
				else
					rainthisyear += RainYesterday;

				if ((day == 1) && (month == cumulus.ChillHourSeasonStart))
				{
					// new year starting
					cumulus.LogMessage(" Chill hour season starting");
					ChillHours = 0;
				}

				// Now reset all values to the current or default ones
				// We may be doing a rollover from the first logger entry,
				// && as we do the rollover before processing the entry, the
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
				HiLoYest.HighRain = HiLoToday.HighRain;
				HiLoYest.HighRainTime = HiLoToday.HighRainTime;
				// Reset today"s high rain rate settings
				HiLoToday.HighRain = RainRate;
				HiLoToday.HighRainTime = timestamp;

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
				HiLoYest.HighDewpoint = HiLoToday.HighDewpoint;
				HiLoYest.HighDewpointTime = HiLoToday.HighDewpointTime;
				HiLoToday.HighDewpoint = OutdoorDewpoint;
				HiLoToday.HighDewpointTime = timestamp;

				HiLoYest.LowDewpoint = HiLoToday.LowDewpoint;
				HiLoYest.LowDewpointTime = HiLoToday.LowDewpointTime;
				HiLoToday.LowDewpoint = OutdoorDewpoint;
				HiLoToday.LowDewpointTime = timestamp;

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

				// Save the current values in case of program restart
				WriteTodayFile(timestamp, true);
				WriteYesterdayFile();

				if (cumulus.NOAAAutoSave)
				{
					try
					{
						NOAA noaa = new NOAA(cumulus);
						var utf8WithoutBom = new System.Text.UTF8Encoding(false);
						var encoding = cumulus.NOAAUseUTF8 ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");

						List<string> report;

						DateTime noaats = timestamp.AddDays(-1);

						// do monthly NOAA report
						cumulus.LogMessage("Creating NOAA monthly report for " + noaats.ToLongDateString());
						report = noaa.CreateMonthlyReport(noaats);
						cumulus.NOAALatestMonthlyReport = FormatDateTime(cumulus.NOAAMonthFileFormat, noaats);
						string noaafile = cumulus.ReportPath + cumulus.NOAALatestMonthlyReport;
						cumulus.LogMessage("Saving monthly report as " + noaafile);
						File.WriteAllLines(noaafile, report, encoding);

						// do yearly NOAA report
						cumulus.LogMessage("Creating NOAA yearly report");
						report = noaa.CreateYearlyReport(noaats);
						cumulus.NOAALatestYearlyReport = FormatDateTime(cumulus.NOAAYearFileFormat, noaats);
						noaafile = cumulus.ReportPath + cumulus.NOAALatestYearlyReport;
						cumulus.LogMessage("Saving yearly report as " + noaafile);
						File.WriteAllLines(noaafile, report, encoding);
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("Error creating NOAA reports: " + ex.Message);
					}
				}

				// Do we need to upload NOAA reports on next FTP?
				cumulus.NOAANeedFTP = cumulus.NOAAAutoFTP;

				if (cumulus.NOAANeedFTP)
				{
					cumulus.LogMessage("NOAA reports will be uploaded at next web update");
				}

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

				// Do the End of day Extra files
				// This will set a flag to transfer on next FTP if required
				cumulus.DoExtraEndOfDayFiles();
				if (cumulus.EODfilesNeedFTP)
				{
					cumulus.LogMessage("Extra files will be uploaded at next web update");
				}

				// Do the Daily graph data files
				if (cumulus.IncludeGraphDataFiles)
				{
					//LogDebugMessage("Creating daily graph data files");
					CreateEodGraphDataFiles();
					cumulus.DailyGraphDataFilesNeedFTP = true;
					//LogDebugMessage("Done creating daily graph data files");
				}


				CurrentDay = timestamp.Day;
				CurrentMonth = timestamp.Month;
				CurrentYear = timestamp.Year;
				//Backupdata(true, timestamp);
				cumulus.StartOfDayBackupNeeded = true;
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

		private void DoDayfile(DateTime timestamp)
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

			// 52  Low Humidex
			// 53  Time of low Humidex

			double AvgTemp;
			if (tempsamplestoday > 0)
				AvgTemp = TempTotalToday / tempsamplestoday;
			else
				AvgTemp = 0;

			// save the value for yesterday
			YestAvgTemp = AvgTemp;

			string datestring = timestamp.AddDays(-1).ToString("dd/MM/yy"); ;
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
			strb.Append(HiLoToday.HighRain.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighRainTime.ToString("HH:mm") + cumulus.ListSeparator);
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
				// use existing current sunshinehour count
				strb.Append(SunshineHours.ToString(cumulus.SunFormat) + cumulus.ListSeparator);
			}
			else
			{
				// for non-midnight rollover, use new item
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
			strb.Append(HiLoToday.HighDewpoint.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.HighDewpointTime.ToString("HH:mm") + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowDewpoint.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
			strb.Append(HiLoToday.LowDewpointTime.ToString("HH:mm") + cumulus.ListSeparator);
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
			strb.Append(HiLoToday.HighHumidexTime.ToString("HH:mm"));

			cumulus.LogMessage("Dayfile.txt entry:");
			cumulus.LogMessage(strb.ToString());

			try
			{
				using (FileStream fs = new FileStream(cumulus.DayFile, FileMode.Append, FileAccess.Write, FileShare.Read))
				using (StreamWriter file = new StreamWriter(fs))
				{
					cumulus.LogMessage("Dayfile.txt opened for writing");

					if ((HiLoToday.HighTemp < -400) || (HiLoToday.LowTemp > 900))
					{
						cumulus.LogMessage("***Error: Daily values are still at default at end of day");
						cumulus.LogMessage("Data not logged to dayfile.txt");
					}
					else
					{
						cumulus.LogMessage("Writing entry to dayfile.txt");

						file.WriteLine(strb.ToString());
						file.Close();
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing to dayfile.txt: " + ex.Message);
			}

			if (cumulus.DayfileMySqlEnabled)
			{
				var mySqlConn = new MySqlConnection();
				mySqlConn.Host = cumulus.MySqlHost;
				mySqlConn.Port = cumulus.MySqlPort;
				mySqlConn.UserId = cumulus.MySqlUser;
				mySqlConn.Password = cumulus.MySqlPass;
				mySqlConn.Database = cumulus.MySqlDatabase;

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
				queryString.Append(HiLoToday.HighRain.ToString(cumulus.RainFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighRainTime.ToString("\\'HH:mm\\'") + ",");
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
				queryString.Append(HiLoToday.HighDewpoint.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.HighDewpointTime.ToString("\\'HH:mm\\'") + ",");
				queryString.Append(HiLoToday.LowDewpoint.ToString(cumulus.TempFormat, InvC) + ",");
				queryString.Append(HiLoToday.LowDewpointTime.ToString("\\'HH:mm\\'") + ",");
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
				Task.Run(() =>
				{
					try
					{
						MySqlCommand cmd = new MySqlCommand();
						cmd.CommandText = queryString.ToString();
						cmd.Connection = mySqlConn;
						cumulus.LogMessage($"MySQL Dayfile: {cmd.CommandText}");

						mySqlConn.Open();
						int aff = cmd.ExecuteNonQuery();
						cumulus.LogMessage($"MySQL Dayfile: Table {cumulus.MySqlDayfileTable} - {aff} rows were affected.");
					}
					catch (Exception ex)
					{
						cumulus.LogMessage("MySQL Dayfile: Error encountered during EOD MySQL operation.");
						cumulus.LogMessage(ex.Message);
					}
					finally
					{
						try
						{
							mySqlConn.Close();
						}
						catch {}
					}
				});
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
			if (cumulus.TempUnit == 1)
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
			if (cumulus.TempUnit == 0)
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
		///  Convert temp supplied in units in use to C
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in C</returns>
		public double ConvertUserTempToC(double value)
		{
			if (cumulus.TempUnit == 1)
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
		///  Convert temp supplied in units in use to F
		/// </summary>
		/// <param name="value">Temp in configured units</param>
		/// <returns>Temp in F</returns>
		public double ConvertUserTempToF(double value)
		{
			if (cumulus.TempUnit == 1)
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
		///  Converts wind supplied in m/s to units in use
		/// </summary>
		/// <param name="value">Wind in m/s</param>
		/// <returns>Wind in configured units</returns>
		public double ConvertWindMSToUser(double value)
		{
			switch (cumulus.WindUnit)
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
		///  Converts wind supplied in mph to units in use
		/// </summary>
		/// <param name="value">Wind in m/s</param>
		/// <returns>Wind in configured units</returns>
		public double ConvertWindMPHToUser(double value)
		{
			switch (cumulus.WindUnit)
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
		/// Converts wind in units in use to m/s
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public virtual double ConvertUserWindToMS(double value)
		{
			switch (cumulus.WindUnit)
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
			switch (cumulus.WindUnit)
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
		///  Converts windrun supplied in units in use to km
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in km</returns>
		public virtual double ConvertWindRunToKm(double value)
		{
			switch (cumulus.WindUnit)
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

		public double ConvertUserWindToKPH(double wind) // input is in WindUnit units, convert to km/h
		{
			switch (cumulus.WindUnit)
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
			return cumulus.RainUnit == 1 ? value * 0.0393700787 : value;
		}

		/// <summary>
		/// Converts rain in inches to units in use
		/// </summary>
		/// <param name="value">Rain in mm</param>
		/// <returns>Rain in configured units</returns>
		public virtual double ConvertRainINToUser(double value)
		{
			return cumulus.RainUnit == 1 ? value : value * 25.4;
		}

		/// <summary>
		/// Converts rain in units in use to mm
		/// </summary>
		/// <param name="value">Rain in configured units</param>
		/// <returns>Rain in mm</returns>
		public virtual double ConvertUserRainToMM(double value)
		{
			return cumulus.RainUnit == 1 ? value / 0.0393700787 : value;
		}

		/// <summary>
		/// Convert pressure in mb to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public double ConvertPressMBToUser(double value)
		{
			return cumulus.PressUnit == 2 ? value * 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure in inHg to units in use
		/// </summary>
		/// <param name="value">pressure in mb</param>
		/// <returns>pressure in configured units</returns>
		public double ConvertPressINHGToUser(double value)
		{
			return cumulus.PressUnit == 2 ? value : value * 33.8638866667;
		}

		/// <summary>
		/// Convert pressure in units in use to mb
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public double ConvertUserPressToMB(double value)
		{
			return cumulus.PressUnit == 2 ? value / 0.0295333727 : value;
		}

		/// <summary>
		/// Convert pressure in units in use to inHg
		/// </summary>
		/// <param name="value">pressure in configured units</param>
		/// <returns>pressure in mb</returns>
		public double ConvertUserPressToIN(double value)
		{
			return cumulus.PressUnit == 2 ? value : value * 0.0295333727;
		}

		public string CompassPoint(int bearing)
		{
			return bearing == 0 ? "-" : cumulus.compassp[(((bearing * 100) + 1125) % 36000) / 2250];
		}

		public void StartLoop()
		{
			t = new Thread(Start) { IsBackground = true };
			t.Start();
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

		public void AddRecentDailyData(DateTime ts, double rain, double sunhours, double mintemp, double maxtemp, double avgtemp)
		{
			RecentDailyData recentDailyData = new RecentDailyData(ts, rain, sunhours, mintemp, maxtemp, avgtemp);
			lock (RecentDailyDataList)
			{
				RecentDailyDataList.Add(recentDailyData);
			}
		}

		/// <summary>
		/// Adds a new entry to the list of data readings from the last 3 hours
		/// </summary>
		/// <param name="ts"></param>
		/// <param name="press"></param>
		/// <param name="temp"></param>
		public void AddLast3HourDataEntry(DateTime ts, double press, double temp)
		{
			Last3HourData last3hourdata = new Last3HourData(ts, press, temp);

			Last3HourDataList.Add(last3hourdata);
		}

		/// <summary>
		/// Adds a new entry to the list of data readings from the last hour
		/// </summary>
		/// <param name="ts"></param>
		/// <param name="press"></param>
		/// <param name="temp"></param>
		public void AddLastHourDataEntry(DateTime ts, double rain, double temp)
		{
			LastHourData lasthourdata = new LastHourData(ts, rain, temp);

			LastHourDataList.Add(lasthourdata);
		}

		/// <summary>
		/// Adds a new entry to the list of data readings for the graphs
		/// </summary>
		/// <param name="ts"></param>
		/// <param name="rain"></param>
		/// <param name="rrate"></param>
		/// <param name="temp"></param>
		public void AddGraphDataEntry(DateTime ts, double rain, double raintoday, double rrate, double temp, double dp, double appt, double chill, double heat, double intemp,
			double press, double speed, double gust, int avgdir, int wdir, int hum, int inhum, double solar, double smax, double uv, double feels, double humidx)
		{
			double pm2p5 = -1;
			double pm10 = -1;
			// Check for Air Quality readings
			switch (cumulus.StationOptions.PrimaryAqSensor)
			{
				case 0: // Davis AirLink Outdoor
					if (cumulus.airLinkDataOut != null)
					{
						pm2p5 = cumulus.airLinkDataOut.pm2p5;
						pm10 = cumulus.airLinkDataOut.pm10;
					}
					break;
				case 1: // Ecowitt sensor 1
					pm2p5 = AirQuality1;
					break;
				case 2: // Ecowitt sensor 2
					pm2p5 = AirQuality2;
					break;
				case 3: // Ecowitt sensor 3
					pm2p5 = AirQuality3;
					break;
				case 4: // Ecowitt sensor 4
					pm2p5 = AirQuality3;
					break;
				default: // Not enabled, use invalid values
					break;
			}
			var graphdata = new GraphData(ts, rain, raintoday, rrate, temp, dp, appt, chill, heat, intemp, press, speed, gust, avgdir, wdir, hum, inhum, solar, smax, uv, feels, humidx, pm2p5, pm10);
			lock (GraphDataList)
			{
				GraphDataList.Add(graphdata);
			}
		}

		public void UpdateGraphDataAqEntry(DateTime ts, double pm2p5, double pm10)
		{
			try
			{
				var toUpdate = GraphDataList.Single(x => x.timestamp == ts);
				if (toUpdate != null)
				{
					toUpdate.pm2p5 = pm2p5;
					toUpdate.pm10 = pm10;
				}
			}
			catch (InvalidOperationException e)
			{
				cumulus.LogDebugMessage($"UpdateGraphDataAqEntry: Failed to find a record matching ts: {ts}. Exception: {e.Message}");
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
		/// Removes entries from Last3HourDataList older than ts - 3 hours
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public void RemoveOldL3HData(DateTime ts)
		{
			DateTime threehoursago = ts.AddHours(-3);

			if (Last3HourDataList.Count > 0)
			{
				// there are entries to consider
				while ((Last3HourDataList.Count > 0) && (Last3HourDataList.First().timestamp < threehoursago))
				{
					// the oldest entry is older than 3 hours ago, delete it
					Last3HourDataList.RemoveAt(0);
				}
			}
		}

		/// <summary>
		/// Removes entries from GraphDataList older than ts - 24 hours
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public void RemoveOldGraphData(DateTime ts)
		{
			DateTime graphperiod = ts.AddHours(-cumulus.GraphHours);
			lock (GraphDataList)
			{
				if (GraphDataList.Count > 0)
				{
					// there are entries to consider
					while ((GraphDataList.Count > 0) && (GraphDataList.First().timestamp < graphperiod))
					{
						// the oldest entry is older than required, delete it
						GraphDataList.RemoveAt(0);
					}
				}
			}
		}

		public void RemoveOldRecentDailyData()
		{
			DateTime onemonthago = DateTime.Now.AddDays(-(cumulus.GraphDays + 1));
			lock (RecentDailyDataList)
			{
				if (RecentDailyDataList.Count > 0)
				{
					// there are entries to consider
					while ((RecentDailyDataList.Count > 0) && (RecentDailyDataList.First().timestamp < onemonthago))
					{
						// the oldest entry is older than a month ago
						RecentDailyDataList.RemoveAt(0);
					}
				}
			}
		}

		/// <summary>
		/// Removes entries from LastHourDataList older than ts - 1 hours
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public void RemoveOldLHData(DateTime ts)
		{
			DateTime onehourago = ts.AddHours(-1);

			if (LastHourDataList.Count > 0)
			{
				// there are entries to consider
				while ((LastHourDataList.Count > 0) && (LastHourDataList.First().timestamp < onehourago))
				{
					// the oldest entry is older than 1 hour ago, delete it
					LastHourDataList.RemoveAt(0);
				}
			}
		}

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
			if (Last3HourDataList.Count > 0)
			{
				// calculate and display the temp trend

				double firstval = Last3HourDataList.First().temperature;
				double lastval = Last3HourDataList.Last().temperature;

				double trendval = (lastval - firstval) / 3.0F;

				temptrendval = trendval;

				cumulus.TempChangeAlarm.UpTriggered = DoAlarm(temptrendval, cumulus.TempChangeAlarm.Value, cumulus.TempChangeAlarm.Enabled, true);
				cumulus.TempChangeAlarm.DownTriggered = DoAlarm(temptrendval, cumulus.TempChangeAlarm.Value * -1, cumulus.TempChangeAlarm.Enabled, false);

				if (LastHourDataList.Count > 0)
				{
					firstval = LastHourDataList.First().temperature;
					lastval = LastHourDataList.Last().temperature;

					TempChangeLastHour = lastval - firstval;

					// calculate and display rainfall in last hour
					firstval = LastHourDataList.First().raincounter;
					lastval = LastHourDataList.Last().raincounter;

					if (lastval < firstval)
					{
						// rain total has gone down, assume it was reset to zero, just use zero
						trendval = 0;
					}
					else
					{
						// normal case
						trendval = lastval - firstval;
					}

					// Round value as some values may have been read from log file and already rounded
					trendval = Math.Round(trendval, cumulus.RainDPlaces);

					var tempRainLastHour = trendval * cumulus.Calib.Rain.Mult;

					if (tempRainLastHour > cumulus.Spike.MaxHourlyRain)
					{
						// ignore
					}
					else
					{
						RainLastHour = tempRainLastHour;

						if (RainLastHour > alltimerecarray[AT_HourlyRain].value)
							SetAlltime(AT_HourlyRain, RainLastHour, ts);

						CheckMonthlyAlltime(AT_HourlyRain, RainLastHour, true, ts);

						if (RainLastHour > HiLoToday.HighHourlyRain)
						{
							HiLoToday.HighHourlyRain = RainLastHour;
							HiLoToday.HighHourlyRainTime = ts;
							WriteTodayFile(ts, false);
						}

						if (RainLastHour > HighHourlyRainThisMonth)
						{
							HighHourlyRainThisMonth = RainLastHour;
							HighHourlyRainThisMonthTS = ts;
							WriteMonthIniFile();
						}

						if (RainLastHour > HighHourlyRainThisYear)
						{
							HighHourlyRainThisYear = RainLastHour;
							HighHourlyRainThisYearTS = ts;
							WriteYearIniFile();
						}
					}
				}

				// calculate and display the pressure trend

				firstval = Last3HourDataList.First().pressure;
				lastval = Last3HourDataList.Last().pressure;

				// save pressure trend in internal units
				presstrendval = (lastval - firstval) / 3.0;

				cumulus.PressChangeAlarm.UpTriggered = DoAlarm(presstrendval, cumulus.PressChangeAlarm.Value, cumulus.PressChangeAlarm.Enabled, true);
				cumulus.PressChangeAlarm.DownTriggered = DoAlarm(presstrendval, cumulus.PressChangeAlarm.Value * -1, cumulus.PressChangeAlarm.Enabled, false);

				// Convert for display
				trendval = ConvertPressMBToUser(presstrendval);

				if (calculaterainrate)
				{
					// Station doesn't supply rain rate, calculate one based on rain in last 5 minutes

					DateTime fiveminutesago = ts.AddSeconds(-330);

					var requiredData = from p in LastHourDataList where p.timestamp > fiveminutesago select p;

					var fiveminutedata = requiredData as IList<LastHourData> ?? requiredData.ToList();
					if (fiveminutedata.Count() > 1)
					{
						// we have at least two values to compare

						TimeSpan span = fiveminutedata.Last().timestamp.Subtract(fiveminutedata.First().timestamp);

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

						if (tempRainRate > cumulus.Spike.MaxRainRate)
						{
							// ignore
						}
						else
						{
							RainRate = tempRainRate;

							if (RainRate > alltimerecarray[AT_HighRainRate].value)
								SetAlltime(AT_HighRainRate, RainRate, ts);

							CheckMonthlyAlltime(AT_HighRainRate, RainRate, true, ts);

							cumulus.HighRainRateAlarm.Triggered = DoAlarm(RainRate, cumulus.HighRainRateAlarm.Value, cumulus.HighRainRateAlarm.Enabled, true);

							if (RainRate > HiLoToday.HighRain)
							{
								HiLoToday.HighRain = RainRate;
								HiLoToday.HighRainTime = ts;
								WriteTodayFile(ts, false);
							}

							if (RainRate > HighRainThisMonth)
							{
								HighRainThisMonth = RainRate;
								HighRainThisMonthTS = ts;
								WriteMonthIniFile();
							}

							if (RainRate > HighRainThisYear)
							{
								HighRainThisYear = RainRate;
								HighRainThisYearTS = ts;
								WriteYearIniFile();
							}
						}
					}
				}

				// calculate and display rainfall in last 24 hour
				var onedayago = ts.AddDays(-1);
				var result = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? order by Timestamp limit 1", onedayago);

				if (result.Count == 0)
				{
					// Unable to retrieve rain counter from 24 hours ago
					trendval = 0;
				}
				else
				{
					firstval = result[0].raincounter;
					lastval = Raincounter;

					trendval = lastval - firstval;
					// Round value as some values may have been read from log file and already rounded
					trendval = Math.Round(trendval, cumulus.RainDPlaces);

					if (trendval < 0)
					{
						trendval = 0;
					}
				}

				RainLast24Hour = trendval * cumulus.Calib.Rain.Mult;
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
			LoadLastHourFromDataLogs(ts);
			LoadLast3HourFromDataLogs(ts);
			LoadGraphDataFromDataLogs(ts);
			LoadAqGraphDataFromDataLogs(ts);
			LoadRecentFromDataLogs(ts);
			LoadRecentDailyDataFromDayfile();
			LoadRecentWindRose();
		}

		private void LoadRecentFromDataLogs(DateTime ts)
		{
			// Recent data goes back a week
			var datefrom = ts.AddDays(-7);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;
			int numadded = 0;

			cumulus.LogMessage($"LoadRecent: Attempting to load 7 days of entries to recent data list");

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								try
								{
									// process each record in the file
									linenum++;
									string Line = sr.ReadLine();
									var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
									entrydate = ddmmyyhhmmStrToDate(st[0], st[1]);

									if (entrydate >= datefrom && entrydate <= dateto)
									{
										// entry is from required period
										var raintoday = Convert.ToDouble(st[9]);
										var gust = Convert.ToDouble(st[6]);
										var speed = Convert.ToDouble(st[5]);
										var wlatest = Convert.ToDouble(st[14]);
										var bearing = Convert.ToInt32(st[24]);
										var avgbearing = Convert.ToInt32(st[7]);
										var outsidetemp = Convert.ToDouble(st[2]);
										var dewpoint = Convert.ToDouble(st[4]);
										var chill = Convert.ToDouble(st[15]);
										var heat = Convert.ToDouble(st[16]);
										var pressure = Convert.ToDouble(st[10]);
										var hum = Convert.ToInt32(st[3]);
										var solar = Convert.ToDouble(st[18]);
										var uv = Convert.ToDouble(st[17]);
										var raincounter = Convert.ToDouble(st[11]);
										var feelslike = st.Count > 27 ? Convert.ToDouble(st[27]) : 0;
										var humidex = st.Count > 28 ? Convert.ToDouble(st[28]) : 0;

										AddRecentDataEntry(entrydate, speed, gust, wlatest, bearing, avgbearing, outsidetemp, chill, dewpoint, heat, hum, pressure, raintoday, solar, uv, raincounter, feelslike, humidex);
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
							} while (!(sr.EndOfStream || entrydate >= dateto || errorCount >= 10));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadRecent: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
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
			cumulus.LogMessage($"LoadRecent: Loaded {numadded} entries to recent data list");
		}

		private void LoadGraphDataFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-cumulus.GraphHours);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;

			cumulus.LogMessage($"LoadGraphData: Attempting to load {cumulus.GraphHours} hours of entries to graph data list");

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								try
								{
									// process each record in the file
									linenum++;
									string Line = sr.ReadLine();
									var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
									entrydate = ddmmyyhhmmStrToDate(st[0], st[1]);

									if (entrydate >= datefrom && entrydate <= dateto)
									{
										// entry is from required period
										var raintotal = Convert.ToDouble(st[11]);
										var raintoday = Convert.ToDouble(st[9]);
										var rainrate = Convert.ToDouble(st[8]);
										var gust = Convert.ToDouble(st[6]);
										var speed = Convert.ToDouble(st[5]);
										var avgbearing = Convert.ToInt32(st[7]);
										var bearing = Convert.ToInt32(st[24]);
										var outsidetemp = Convert.ToDouble(st[2]);
										var dewpoint = Convert.ToDouble(st[4]);
										var appt = Convert.ToDouble(st[21]);
										var chill = Convert.ToDouble(st[15]);
										var heat = Convert.ToDouble(st[16]);
										var insidetemp = Convert.ToDouble(st[12]);
										var pressure = Convert.ToDouble(st[10]);
										var hum = Convert.ToInt32(st[3]);
										var inhum = Convert.ToInt32(st[13]);
										var solar = Convert.ToDouble(st[18]);
										var solarmax = Convert.ToDouble(st[22]);
										var uv = Convert.ToDouble(st[17]);
										var feels = st.Count > 27 ? Convert.ToDouble(st[27]) : 0.0;
										var humidex = st.Count > 28 ? Convert.ToDouble(st[28]) : 0.0;

										AddGraphDataEntry(entrydate, raintotal, raintoday, rainrate, outsidetemp, dewpoint, appt, chill, heat, insidetemp, pressure, speed, gust,
											avgbearing, bearing, hum, inhum, solar, solarmax, uv, feels, humidex);
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"LoadGraphData: Error at line {linenum} of {logFile} : {e.Message}");
									cumulus.LogMessage("LoadGraphData: Please edit the file to correct the error");
									errorCount++;
									if (errorCount >= 10)
									{
										cumulus.LogMessage($"LoadGraphData: Too many errors reading {logFile} - aborting load of graph data");
									}
								}
							} while (!(sr.EndOfStream || entrydate >= dateto || errorCount >= 10));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
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
			cumulus.LogMessage($"LoadGraphData: Loaded {GraphDataList.Count} entries to graph data list");
		}

		private void LoadAqGraphDataFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-cumulus.GraphHours);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile;
			bool finished = false;
			int updatedCount = 0;

			if (cumulus.StationOptions.PrimaryAqSensor < 0) return;

			cumulus.LogMessage($"LoadAqGraphData: Attempting to load {cumulus.GraphHours} hours of entries to Air Quality graph data");

			if (cumulus.StationOptions.PrimaryAqSensor == 0) // AirLinkOutdoor
			{
				logFile = cumulus.GetAirLinkLogFileName(filedate);
			}
			else if (cumulus.StationOptions.PrimaryAqSensor > 0 && cumulus.StationOptions.PrimaryAqSensor <= 4) // Ecowitt
			{
				logFile = cumulus.GetExtraLogFileName(filedate);
			}
			else
			{
				cumulus.LogMessage($"LoadAqGraphData: Error - The primary AQ sensor is not set to a valid value [0-4], currently={cumulus.StationOptions.PrimaryAqSensor}");
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
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								try
								{
									// process each record in the file
									linenum++;
									string Line = sr.ReadLine();
									var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
									entrydate = ddmmyyhhmmStrToDate(st[0], st[1]);

									if (entrydate >= datefrom && entrydate <= dateto)
									{
										// entry is from required period
										double pm2p5, pm10;
										if (cumulus.StationOptions.PrimaryAqSensor == 0)
										{
											// AirLink
											pm2p5 = Convert.ToDouble(st[32]);
											pm10 = Convert.ToDouble(st[37]);
										}
										else
										{
											// Ecowitt sensor 1
											pm2p5 = Convert.ToDouble(st[66 + cumulus.StationOptions.PrimaryAqSensor]);
											pm10 = -1;
										}

										UpdateGraphDataAqEntry(entrydate, pm2p5, pm10);
										updatedCount++;
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"LoadAqGraphData: Error at line {linenum} of {logFile} : {e.Message}");
									cumulus.LogMessage("Please edit the file to correct the error");
									errorCount++;
									if (errorCount >= 10)
									{
										cumulus.LogMessage($"LoadAqGraphData: Too many errors reading {logFile} - aborting load of graph data");
									}
								}
							} while (!(sr.EndOfStream || entrydate >= dateto || errorCount >= 10));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadAqGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}

				if (entrydate >= dateto || filedate > dateto.AddMonths(1))
				{
					finished = true;
				}
				else
				{
					filedate = filedate.AddMonths(1);
					if (cumulus.StationOptions.PrimaryAqSensor == 0) // AirLinkOutdoor
					{
						logFile = cumulus.GetAirLinkLogFileName(filedate);
					}
					else if (cumulus.StationOptions.PrimaryAqSensor > 0 && cumulus.StationOptions.PrimaryAqSensor <= 4) // Ecowitt
					{
						logFile = cumulus.GetExtraLogFileName(filedate);
					}
				}
			}
			cumulus.LogMessage($"LoadAqGraphData: Loaded {updatedCount} entries to graph data list");
		}


		private void LoadRecentWindRose()
		{
			// We can now just query the recent data DB as it has been populated from the loags
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

		private void LoadLast3HourFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-3);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;

			cumulus.LogMessage($"LoadLast3Hour: Attempting to load 3 hour data list");

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;
					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								try
								{
									// process each record in the file
									linenum++;
									string Line = sr.ReadLine();
									var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
									entrydate = ddmmyyhhmmStrToDate(st[0], st[1]);

									if (entrydate >= datefrom && entrydate <= dateto)
									{
										// entry is from required period
										var outsidetemp = Convert.ToDouble(st[2]);
										var pressure = Convert.ToDouble(st[10]);

										AddLast3HourDataEntry(entrydate, pressure, outsidetemp);

										var gust = Convert.ToDouble(st[14]);
										var speed = Convert.ToDouble(st[5]);
										var bearing = Convert.ToInt32(st[7]);

										WindRecent[nextwind].Gust = gust;
										WindRecent[nextwind].Speed = speed;
										WindRecent[nextwind].Timestamp = entrydate;
										nextwind = (nextwind + 1) % MaxWindRecent;

										WindVec[nextwindvec].X = gust * Math.Sin(DegToRad(bearing));
										WindVec[nextwindvec].Y = gust * Math.Cos(DegToRad(bearing));
										WindVec[nextwindvec].Timestamp = entrydate;
										WindVec[nextwindvec].Bearing = Bearing; // savedBearing;
										nextwindvec = (nextwindvec + 1) % MaxWindRecent;
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"LoadLast3Hour: Error at line {linenum} of {logFile} : {e.Message}");
									cumulus.LogMessage("Please edit the file to correct the error");
									errorCount++;
									if (errorCount >= 10)
									{
										cumulus.LogMessage($"LoadLast3Hour: Too many errors reading {logFile} - aborting load of last hour data");
									}
								}
							} while (!(sr.EndOfStream || entrydate >= dateto || errorCount >= 10));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadLast3Hour: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
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
			cumulus.LogMessage($"LoadLast3Hour: Loaded {Last3HourDataList.Count} entries to last 3 hour data list");
		}

		private void LoadLastHourFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-1);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;

			cumulus.LogMessage("LoadLastHour: Attempting to load last hour entries");

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;
					int errorCount = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								try
								{
									// process each record in the file
									linenum++;
									string Line = sr.ReadLine();
									var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
									entrydate = ddmmyyhhmmStrToDate(st[0], st[1]);

									if (entrydate >= datefrom && entrydate <= dateto)
									{
										// entry is from required period
										var outsidetemp = Convert.ToDouble(st[2]);
										var raintotal = Convert.ToDouble(st[11]);

										AddLastHourDataEntry(entrydate, raintotal, outsidetemp);
									}
								}
								catch (Exception e)
								{
									cumulus.LogMessage($"LoadLastHour: Error at line {linenum} of {logFile} : {e.Message}");
									cumulus.LogMessage("Please edit the file to correct the error");
									errorCount++;
									if (errorCount >= 10)
									{
										cumulus.LogMessage($"LoadLastHour: Too many errors reading {logFile} - aborting load of last hour data");
									}
								}
							} while (!(sr.EndOfStream || entrydate >= dateto || errorCount >= 10));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"LoadLastHour: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
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
			cumulus.LogMessage($"LoadLastHour: Loaded {LastHourDataList.Count} entries to last hour data list");
		}

		private void LoadRecentDailyDataFromDayfile()
		{
			int addedEntries = 0;

			cumulus.LogMessage($"LoadRecentDaily: Attempting to load {cumulus.GraphDays} days to recent daily data list");

			if (File.Exists(cumulus.DayFile))
			{
				var datefrom = DateTime.Now.AddDays(-(cumulus.GraphDays + 1));

				int linenum = 0;
				int errorCount = 0;

				try
				{
					using (var sr = new StreamReader(cumulus.DayFile))
					{
						do
						{
							try
							{
								// process each record in the file
								linenum++;
								string Line = sr.ReadLine();
								var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

								var entrydate = ddmmyyStrToDate(st[0]);

								if (entrydate >= datefrom)
								{
									// entry is from required period
									var raintotal = Convert.ToDouble(st[14]);
									double sunhours;
									if ((st.Count > 24) && (!String.IsNullOrEmpty(st[24])))
									{
										sunhours = Convert.ToDouble(st[24]);
									}
									else
									{
										sunhours = 0;
									}

									double mintemp = Convert.ToDouble(st[4]);
									double maxtemp = Convert.ToDouble(st[6]);
									double avgtemp = Convert.ToDouble(st[15]);

									AddRecentDailyData(entrydate, raintotal, sunhours, mintemp, maxtemp, avgtemp);
									addedEntries++;
								}
							}
							catch (Exception e)
							{
								cumulus.LogMessage($"LoadRecentDaily: Error at line {linenum} of {cumulus.DayFile} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogMessage($"LoadRecentDaily: Too many errors reading {cumulus.DayFile} - aborting load of recent daily data");
								}
							}
						} while (!(sr.EndOfStream || errorCount >= 10));
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage($"LoadRecentDaily: Error at line {linenum} of {cumulus.DayFile} : {e.Message}");
					cumulus.LogMessage("Please edit the file to correct the error");
				}
				cumulus.LogMessage($"LoadRecentDaily: Loaded {addedEntries} entries to recent daily data list");
			}
			else
			{
				cumulus.LogMessage("LoadRecentDaily: No Dayfile found - No entries added to recent daily data list");
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

			alltimerecarray[AT_HighTemp].data_type = AT_HighTemp;
			alltimerecarray[AT_HighTemp].value = ini.GetValue("Temperature", "hightempvalue", -999.0);
			alltimerecarray[AT_HighTemp].timestamp = ini.GetValue("Temperature", "hightemptime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowTemp].data_type = AT_LowTemp;
			alltimerecarray[AT_LowTemp].value = ini.GetValue("Temperature", "lowtempvalue", 999.0);
			alltimerecarray[AT_LowTemp].timestamp = ini.GetValue("Temperature", "lowtemptime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowChill].data_type = AT_LowChill;
			alltimerecarray[AT_LowChill].value = ini.GetValue("Temperature", "lowchillvalue", 999.0);
			alltimerecarray[AT_LowChill].timestamp = ini.GetValue("Temperature", "lowchilltime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighMinTemp].data_type = AT_HighMinTemp;
			alltimerecarray[AT_HighMinTemp].value = ini.GetValue("Temperature", "highmintempvalue", -999.0);
			alltimerecarray[AT_HighMinTemp].timestamp = ini.GetValue("Temperature", "highmintemptime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowMaxTemp].data_type = AT_LowMaxTemp;
			alltimerecarray[AT_LowMaxTemp].value = ini.GetValue("Temperature", "lowmaxtempvalue", 999.0);
			alltimerecarray[AT_LowMaxTemp].timestamp = ini.GetValue("Temperature", "lowmaxtemptime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighAppTemp].data_type = AT_HighAppTemp;
			alltimerecarray[AT_HighAppTemp].value = ini.GetValue("Temperature", "highapptempvalue", -999.0);
			alltimerecarray[AT_HighAppTemp].timestamp = ini.GetValue("Temperature", "highapptemptime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowAppTemp].data_type = AT_LowAppTemp;
			alltimerecarray[AT_LowAppTemp].value = ini.GetValue("Temperature", "lowapptempvalue", 999.0);
			alltimerecarray[AT_LowAppTemp].timestamp = ini.GetValue("Temperature", "lowapptemptime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighFeelsLike].data_type = AT_HighFeelsLike;
			alltimerecarray[AT_HighFeelsLike].value = ini.GetValue("Temperature", "highfeelslikevalue", -999.0);
			alltimerecarray[AT_HighFeelsLike].timestamp = ini.GetValue("Temperature", "highfeelsliketime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowFeelsLike].data_type = AT_LowFeelsLike;
			alltimerecarray[AT_LowFeelsLike].value = ini.GetValue("Temperature", "lowfeelslikevalue", 999.0);
			alltimerecarray[AT_LowFeelsLike].timestamp = ini.GetValue("Temperature", "lowfeelsliketime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighHumidex].data_type = AT_HighHumidex;
			alltimerecarray[AT_HighHumidex].value = ini.GetValue("Temperature", "highhumidexvalue", -999.0);
			alltimerecarray[AT_HighHumidex].timestamp = ini.GetValue("Temperature", "highhumidextime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighHeatIndex].data_type = AT_HighHeatIndex;
			alltimerecarray[AT_HighHeatIndex].value = ini.GetValue("Temperature", "highheatindexvalue", -999.0);
			alltimerecarray[AT_HighHeatIndex].timestamp = ini.GetValue("Temperature", "highheatindextime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighDewPoint].data_type = AT_HighDewPoint;
			alltimerecarray[AT_HighDewPoint].value = ini.GetValue("Temperature", "highdewpointvalue", -999.0);
			alltimerecarray[AT_HighDewPoint].timestamp = ini.GetValue("Temperature", "highdewpointtime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowDewpoint].data_type = AT_LowDewpoint;
			alltimerecarray[AT_LowDewpoint].value = ini.GetValue("Temperature", "lowdewpointvalue", 999.0);
			alltimerecarray[AT_LowDewpoint].timestamp = ini.GetValue("Temperature", "lowdewpointtime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighDailyTempRange].data_type = AT_HighDailyTempRange;
			alltimerecarray[AT_HighDailyTempRange].value = ini.GetValue("Temperature", "hightemprangevalue", 0.0);
			alltimerecarray[AT_HighDailyTempRange].timestamp = ini.GetValue("Temperature", "hightemprangetime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowDailyTempRange].data_type = AT_LowDailyTempRange;
			alltimerecarray[AT_LowDailyTempRange].value = ini.GetValue("Temperature", "lowtemprangevalue", 999.0);
			alltimerecarray[AT_LowDailyTempRange].timestamp = ini.GetValue("Temperature", "lowtemprangetime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighWind].data_type = AT_HighWind;
			alltimerecarray[AT_HighWind].value = ini.GetValue("Wind", "highwindvalue", 0.0);
			alltimerecarray[AT_HighWind].timestamp = ini.GetValue("Wind", "highwindtime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighGust].data_type = AT_HighGust;
			alltimerecarray[AT_HighGust].value = ini.GetValue("Wind", "highgustvalue", 0.0);
			alltimerecarray[AT_HighGust].timestamp = ini.GetValue("Wind", "highgusttime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighWindrun].data_type = AT_HighWindrun;
			alltimerecarray[AT_HighWindrun].value = ini.GetValue("Wind", "highdailywindrunvalue", 0.0);
			alltimerecarray[AT_HighWindrun].timestamp = ini.GetValue("Wind", "highdailywindruntime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighRainRate].data_type = AT_HighRainRate;
			alltimerecarray[AT_HighRainRate].value = ini.GetValue("Rain", "highrainratevalue", 0.0);
			alltimerecarray[AT_HighRainRate].timestamp = ini.GetValue("Rain", "highrainratetime", cumulus.defaultRecordTS);

			alltimerecarray[AT_DailyRain].data_type = AT_DailyRain;
			alltimerecarray[AT_DailyRain].value = ini.GetValue("Rain", "highdailyrainvalue", 0.0);
			alltimerecarray[AT_DailyRain].timestamp = ini.GetValue("Rain", "highdailyraintime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HourlyRain].data_type = AT_HourlyRain;
			alltimerecarray[AT_HourlyRain].value = ini.GetValue("Rain", "highhourlyrainvalue", 0.0);
			alltimerecarray[AT_HourlyRain].timestamp = ini.GetValue("Rain", "highhourlyraintime", cumulus.defaultRecordTS);

			alltimerecarray[AT_WetMonth].data_type = AT_WetMonth;
			alltimerecarray[AT_WetMonth].value = ini.GetValue("Rain", "highmonthlyrainvalue", 0.0);
			alltimerecarray[AT_WetMonth].timestamp = ini.GetValue("Rain", "highmonthlyraintime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LongestDryPeriod].data_type = AT_LongestDryPeriod;
			alltimerecarray[AT_LongestDryPeriod].value = ini.GetValue("Rain", "longestdryperiodvalue", 0);
			alltimerecarray[AT_LongestDryPeriod].timestamp = ini.GetValue("Rain", "longestdryperiodtime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LongestWetPeriod].data_type = AT_LongestWetPeriod;
			alltimerecarray[AT_LongestWetPeriod].value = ini.GetValue("Rain", "longestwetperiodvalue", 0);
			alltimerecarray[AT_LongestWetPeriod].timestamp = ini.GetValue("Rain", "longestwetperiodtime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighPress].data_type = AT_HighPress;
			alltimerecarray[AT_HighPress].value = ini.GetValue("Pressure", "highpressurevalue", 0.0);
			alltimerecarray[AT_HighPress].timestamp = ini.GetValue("Pressure", "highpressuretime", cumulus.defaultRecordTS);

			alltimerecarray[AT_LowPress].data_type = AT_LowPress;
			alltimerecarray[AT_LowPress].value = ini.GetValue("Pressure", "lowpressurevalue", 9999.0);
			alltimerecarray[AT_LowPress].timestamp = ini.GetValue("Pressure", "lowpressuretime", cumulus.defaultRecordTS);

			alltimerecarray[AT_HighHumidity].data_type = AT_HighHumidity;
			alltimerecarray[AT_HighHumidity].value = ini.GetValue("Humidity", "highhumidityvalue", 0);
			alltimerecarray[AT_HighHumidity].timestamp = ini.GetValue("Humidity", "highhumiditytime", cumulus.defaultRecordTS);

			alltimerecarray[AT_lowhumidity].data_type = AT_lowhumidity;
			alltimerecarray[AT_lowhumidity].value = ini.GetValue("Humidity", "lowhumidityvalue", 999);
			alltimerecarray[AT_lowhumidity].timestamp = ini.GetValue("Humidity", "lowhumiditytime", cumulus.defaultRecordTS);

			cumulus.LogMessage("Alltime.ini file read");
		}

		public void WriteAlltimeIniFile()
		{
			try
			{
				IniFile ini = new IniFile(cumulus.AlltimeIniFile);

				ini.SetValue("Temperature", "hightempvalue", alltimerecarray[AT_HighTemp].value);
				ini.SetValue("Temperature", "hightemptime", alltimerecarray[AT_HighTemp].timestamp);
				ini.SetValue("Temperature", "lowtempvalue", alltimerecarray[AT_LowTemp].value);
				ini.SetValue("Temperature", "lowtemptime", alltimerecarray[AT_LowTemp].timestamp);
				ini.SetValue("Temperature", "lowchillvalue", alltimerecarray[AT_LowChill].value);
				ini.SetValue("Temperature", "lowchilltime", alltimerecarray[AT_LowChill].timestamp);
				ini.SetValue("Temperature", "highmintempvalue", alltimerecarray[AT_HighMinTemp].value);
				ini.SetValue("Temperature", "highmintemptime", alltimerecarray[AT_HighMinTemp].timestamp);
				ini.SetValue("Temperature", "lowmaxtempvalue", alltimerecarray[AT_LowMaxTemp].value);
				ini.SetValue("Temperature", "lowmaxtemptime", alltimerecarray[AT_LowMaxTemp].timestamp);
				ini.SetValue("Temperature", "highapptempvalue", alltimerecarray[AT_HighAppTemp].value);
				ini.SetValue("Temperature", "highapptemptime", alltimerecarray[AT_HighAppTemp].timestamp);
				ini.SetValue("Temperature", "lowapptempvalue", alltimerecarray[AT_LowAppTemp].value);
				ini.SetValue("Temperature", "lowapptemptime", alltimerecarray[AT_LowAppTemp].timestamp);
				ini.SetValue("Temperature", "highfeelslikevalue", alltimerecarray[AT_HighFeelsLike].value);
				ini.SetValue("Temperature", "highfeelsliketime", alltimerecarray[AT_HighFeelsLike].timestamp);
				ini.SetValue("Temperature", "lowfeelslikevalue", alltimerecarray[AT_LowFeelsLike].value);
				ini.SetValue("Temperature", "lowfeelsliketime", alltimerecarray[AT_LowFeelsLike].timestamp);
				ini.SetValue("Temperature", "highhumidexvalue", alltimerecarray[AT_HighHumidex].value);
				ini.SetValue("Temperature", "highhumidextime", alltimerecarray[AT_HighHumidex].timestamp);
				ini.SetValue("Temperature", "highheatindexvalue", alltimerecarray[AT_HighHeatIndex].value);
				ini.SetValue("Temperature", "highheatindextime", alltimerecarray[AT_HighHeatIndex].timestamp);
				ini.SetValue("Temperature", "highdewpointvalue", alltimerecarray[AT_HighDewPoint].value);
				ini.SetValue("Temperature", "highdewpointtime", alltimerecarray[AT_HighDewPoint].timestamp);
				ini.SetValue("Temperature", "lowdewpointvalue", alltimerecarray[AT_LowDewpoint].value);
				ini.SetValue("Temperature", "lowdewpointtime", alltimerecarray[AT_LowDewpoint].timestamp);
				ini.SetValue("Temperature", "hightemprangevalue", alltimerecarray[AT_HighDailyTempRange].value);
				ini.SetValue("Temperature", "hightemprangetime", alltimerecarray[AT_HighDailyTempRange].timestamp);
				ini.SetValue("Temperature", "lowtemprangevalue", alltimerecarray[AT_LowDailyTempRange].value);
				ini.SetValue("Temperature", "lowtemprangetime", alltimerecarray[AT_LowDailyTempRange].timestamp);
				ini.SetValue("Wind", "highwindvalue", alltimerecarray[AT_HighWind].value);
				ini.SetValue("Wind", "highwindtime", alltimerecarray[AT_HighWind].timestamp);
				ini.SetValue("Wind", "highgustvalue", alltimerecarray[AT_HighGust].value);
				ini.SetValue("Wind", "highgusttime", alltimerecarray[AT_HighGust].timestamp);
				ini.SetValue("Wind", "highdailywindrunvalue", alltimerecarray[AT_HighWindrun].value);
				ini.SetValue("Wind", "highdailywindruntime", alltimerecarray[AT_HighWindrun].timestamp);
				ini.SetValue("Rain", "highrainratevalue", alltimerecarray[AT_HighRainRate].value);
				ini.SetValue("Rain", "highrainratetime", alltimerecarray[AT_HighRainRate].timestamp);
				ini.SetValue("Rain", "highdailyrainvalue", alltimerecarray[AT_DailyRain].value);
				ini.SetValue("Rain", "highdailyraintime", alltimerecarray[AT_DailyRain].timestamp);
				ini.SetValue("Rain", "highhourlyrainvalue", alltimerecarray[AT_HourlyRain].value);
				ini.SetValue("Rain", "highhourlyraintime", alltimerecarray[AT_HourlyRain].timestamp);
				ini.SetValue("Rain", "highmonthlyrainvalue", alltimerecarray[AT_WetMonth].value);
				ini.SetValue("Rain", "highmonthlyraintime", alltimerecarray[AT_WetMonth].timestamp);
				ini.SetValue("Rain", "longestdryperiodvalue", alltimerecarray[AT_LongestDryPeriod].value);
				ini.SetValue("Rain", "longestdryperiodtime", alltimerecarray[AT_LongestDryPeriod].timestamp);
				ini.SetValue("Rain", "longestwetperiodvalue", alltimerecarray[AT_LongestWetPeriod].value);
				ini.SetValue("Rain", "longestwetperiodtime", alltimerecarray[AT_LongestWetPeriod].timestamp);
				ini.SetValue("Pressure", "highpressurevalue", alltimerecarray[AT_HighPress].value);
				ini.SetValue("Pressure", "highpressuretime", alltimerecarray[AT_HighPress].timestamp);
				ini.SetValue("Pressure", "lowpressurevalue", alltimerecarray[AT_LowPress].value);
				ini.SetValue("Pressure", "lowpressuretime", alltimerecarray[AT_LowPress].timestamp);
				ini.SetValue("Humidity", "highhumidityvalue", alltimerecarray[AT_HighHumidity].value);
				ini.SetValue("Humidity", "highhumiditytime", alltimerecarray[AT_HighHumidity].timestamp);
				ini.SetValue("Humidity", "lowhumidityvalue", alltimerecarray[AT_lowhumidity].value);
				ini.SetValue("Humidity", "lowhumiditytime", alltimerecarray[AT_lowhumidity].timestamp);

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

				monthlyrecarray[AT_HighTemp, month].data_type = AT_HighTemp;
				monthlyrecarray[AT_HighTemp, month].value = ini.GetValue("Temperature" + monthstr, "hightempvalue", -999.0);
				monthlyrecarray[AT_HighTemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "hightemptime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowTemp, month].data_type = AT_LowTemp;
				monthlyrecarray[AT_LowTemp, month].value = ini.GetValue("Temperature" + monthstr, "lowtempvalue", 999.0);
				monthlyrecarray[AT_LowTemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowtemptime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowChill, month].data_type = AT_LowChill;
				monthlyrecarray[AT_LowChill, month].value = ini.GetValue("Temperature" + monthstr, "lowchillvalue", 999.0);
				monthlyrecarray[AT_LowChill, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowchilltime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighMinTemp, month].data_type = AT_HighMinTemp;
				monthlyrecarray[AT_HighMinTemp, month].value = ini.GetValue("Temperature" + monthstr, "highmintempvalue", -999.0);
				monthlyrecarray[AT_HighMinTemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "highmintemptime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowMaxTemp, month].data_type = AT_LowMaxTemp;
				monthlyrecarray[AT_LowMaxTemp, month].value = ini.GetValue("Temperature" + monthstr, "lowmaxtempvalue", 999.0);
				monthlyrecarray[AT_LowMaxTemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowmaxtemptime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighAppTemp, month].data_type = AT_HighAppTemp;
				monthlyrecarray[AT_HighAppTemp, month].value = ini.GetValue("Temperature" + monthstr, "highapptempvalue", -999.0);
				monthlyrecarray[AT_HighAppTemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "highapptemptime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowAppTemp, month].data_type = AT_LowAppTemp;
				monthlyrecarray[AT_LowAppTemp, month].value = ini.GetValue("Temperature" + monthstr, "lowapptempvalue", 999.0);
				monthlyrecarray[AT_LowAppTemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowapptemptime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighFeelsLike, month].data_type = AT_HighFeelsLike;
				monthlyrecarray[AT_HighFeelsLike, month].value = ini.GetValue("Temperature" + monthstr, "highfeelslikevalue", -999.0);
				monthlyrecarray[AT_HighFeelsLike, month].timestamp = ini.GetValue("Temperature" + monthstr, "highfeelsliketime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowFeelsLike, month].data_type = AT_LowFeelsLike;
				monthlyrecarray[AT_LowFeelsLike, month].value = ini.GetValue("Temperature" + monthstr, "lowfeelslikevalue", 999.0);
				monthlyrecarray[AT_LowFeelsLike, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowfeelsliketime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighHumidex, month].data_type = AT_HighHumidex;
				monthlyrecarray[AT_HighHumidex, month].value = ini.GetValue("Temperature" + monthstr, "highhumidexvalue", -999.0);
				monthlyrecarray[AT_HighHumidex, month].timestamp = ini.GetValue("Temperature" + monthstr, "highhumidextime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighHeatIndex, month].data_type = AT_HighHeatIndex;
				monthlyrecarray[AT_HighHeatIndex, month].value = ini.GetValue("Temperature" + monthstr, "highheatindexvalue", -999.0);
				monthlyrecarray[AT_HighHeatIndex, month].timestamp = ini.GetValue("Temperature" + monthstr, "highheatindextime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighDewPoint, month].data_type = AT_HighDewPoint;
				monthlyrecarray[AT_HighDewPoint, month].value = ini.GetValue("Temperature" + monthstr, "highdewpointvalue", -999.0);
				monthlyrecarray[AT_HighDewPoint, month].timestamp = ini.GetValue("Temperature" + monthstr, "highdewpointtime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowDewpoint, month].data_type = AT_LowDewpoint;
				monthlyrecarray[AT_LowDewpoint, month].value = ini.GetValue("Temperature" + monthstr, "lowdewpointvalue", 999.0);
				monthlyrecarray[AT_LowDewpoint, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowdewpointtime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighDailyTempRange, month].data_type = AT_HighDailyTempRange;
				monthlyrecarray[AT_HighDailyTempRange, month].value = ini.GetValue("Temperature" + monthstr, "hightemprangevalue", 0.0);
				monthlyrecarray[AT_HighDailyTempRange, month].timestamp = ini.GetValue("Temperature" + monthstr, "hightemprangetime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowDailyTempRange, month].data_type = AT_LowDailyTempRange;
				monthlyrecarray[AT_LowDailyTempRange, month].value = ini.GetValue("Temperature" + monthstr, "lowtemprangevalue", 999.0);
				monthlyrecarray[AT_LowDailyTempRange, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowtemprangetime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighWind, month].data_type = AT_HighWind;
				monthlyrecarray[AT_HighWind, month].value = ini.GetValue("Wind" + monthstr, "highwindvalue", 0.0);
				monthlyrecarray[AT_HighWind, month].timestamp = ini.GetValue("Wind" + monthstr, "highwindtime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighGust, month].data_type = AT_HighGust;
				monthlyrecarray[AT_HighGust, month].value = ini.GetValue("Wind" + monthstr, "highgustvalue", 0.0);
				monthlyrecarray[AT_HighGust, month].timestamp = ini.GetValue("Wind" + monthstr, "highgusttime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighWindrun, month].data_type = AT_HighWindrun;
				monthlyrecarray[AT_HighWindrun, month].value = ini.GetValue("Wind" + monthstr, "highdailywindrunvalue", 0.0);
				monthlyrecarray[AT_HighWindrun, month].timestamp = ini.GetValue("Wind" + monthstr, "highdailywindruntime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighRainRate, month].data_type = AT_HighRainRate;
				monthlyrecarray[AT_HighRainRate, month].value = ini.GetValue("Rain" + monthstr, "highrainratevalue", 0.0);
				monthlyrecarray[AT_HighRainRate, month].timestamp = ini.GetValue("Rain" + monthstr, "highrainratetime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_DailyRain, month].data_type = AT_DailyRain;
				monthlyrecarray[AT_DailyRain, month].value = ini.GetValue("Rain" + monthstr, "highdailyrainvalue", 0.0);
				monthlyrecarray[AT_DailyRain, month].timestamp = ini.GetValue("Rain" + monthstr, "highdailyraintime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HourlyRain, month].data_type = AT_HourlyRain;
				monthlyrecarray[AT_HourlyRain, month].value = ini.GetValue("Rain" + monthstr, "highhourlyrainvalue", 0.0);
				monthlyrecarray[AT_HourlyRain, month].timestamp = ini.GetValue("Rain" + monthstr, "highhourlyraintime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_WetMonth, month].data_type = AT_WetMonth;
				monthlyrecarray[AT_WetMonth, month].value = ini.GetValue("Rain" + monthstr, "highmonthlyrainvalue", 0.0);
				monthlyrecarray[AT_WetMonth, month].timestamp = ini.GetValue("Rain" + monthstr, "highmonthlyraintime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LongestDryPeriod, month].data_type = AT_LongestDryPeriod;
				monthlyrecarray[AT_LongestDryPeriod, month].value = ini.GetValue("Rain" + monthstr, "longestdryperiodvalue", 0);
				monthlyrecarray[AT_LongestDryPeriod, month].timestamp = ini.GetValue("Rain" + monthstr, "longestdryperiodtime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LongestWetPeriod, month].data_type = AT_LongestWetPeriod;
				monthlyrecarray[AT_LongestWetPeriod, month].value = ini.GetValue("Rain" + monthstr, "longestwetperiodvalue", 0);
				monthlyrecarray[AT_LongestWetPeriod, month].timestamp = ini.GetValue("Rain" + monthstr, "longestwetperiodtime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighPress, month].data_type = AT_HighPress;
				monthlyrecarray[AT_HighPress, month].value = ini.GetValue("Pressure" + monthstr, "highpressurevalue", 0.0);
				monthlyrecarray[AT_HighPress, month].timestamp = ini.GetValue("Pressure" + monthstr, "highpressuretime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_LowPress, month].data_type = AT_LowPress;
				monthlyrecarray[AT_LowPress, month].value = ini.GetValue("Pressure" + monthstr, "lowpressurevalue", 9999.0);
				monthlyrecarray[AT_LowPress, month].timestamp = ini.GetValue("Pressure" + monthstr, "lowpressuretime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_HighHumidity, month].data_type = AT_HighHumidity;
				monthlyrecarray[AT_HighHumidity, month].value = ini.GetValue("Humidity" + monthstr, "highhumidityvalue", 0.0);
				monthlyrecarray[AT_HighHumidity, month].timestamp = ini.GetValue("Humidity" + monthstr, "highhumiditytime", cumulus.defaultRecordTS);

				monthlyrecarray[AT_lowhumidity, month].data_type = AT_lowhumidity;
				monthlyrecarray[AT_lowhumidity, month].value = ini.GetValue("Humidity" + monthstr, "lowhumidityvalue", 999.0);
				monthlyrecarray[AT_lowhumidity, month].timestamp = ini.GetValue("Humidity" + monthstr, "lowhumiditytime", cumulus.defaultRecordTS);
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

					ini.SetValue("Temperature" + monthstr, "hightempvalue", monthlyrecarray[AT_HighTemp, month].value);
					ini.SetValue("Temperature" + monthstr, "hightemptime", monthlyrecarray[AT_HighTemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowtempvalue", monthlyrecarray[AT_LowTemp, month].value);
					ini.SetValue("Temperature" + monthstr, "lowtemptime", monthlyrecarray[AT_LowTemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowchillvalue", monthlyrecarray[AT_LowChill, month].value);
					ini.SetValue("Temperature" + monthstr, "lowchilltime", monthlyrecarray[AT_LowChill, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highmintempvalue", monthlyrecarray[AT_HighMinTemp, month].value);
					ini.SetValue("Temperature" + monthstr, "highmintemptime", monthlyrecarray[AT_HighMinTemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowmaxtempvalue", monthlyrecarray[AT_LowMaxTemp, month].value);
					ini.SetValue("Temperature" + monthstr, "lowmaxtemptime", monthlyrecarray[AT_LowMaxTemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highapptempvalue", monthlyrecarray[AT_HighAppTemp, month].value);
					ini.SetValue("Temperature" + monthstr, "highapptemptime", monthlyrecarray[AT_HighAppTemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowapptempvalue", monthlyrecarray[AT_LowAppTemp, month].value);
					ini.SetValue("Temperature" + monthstr, "lowapptemptime", monthlyrecarray[AT_LowAppTemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highfeelslikevalue", monthlyrecarray[AT_HighFeelsLike, month].value);
					ini.SetValue("Temperature" + monthstr, "highfeelsliketime", monthlyrecarray[AT_HighFeelsLike, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowfeelslikevalue", monthlyrecarray[AT_LowFeelsLike, month].value);
					ini.SetValue("Temperature" + monthstr, "lowfeelsliketime", monthlyrecarray[AT_LowFeelsLike, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highhumidexvalue", monthlyrecarray[AT_HighHumidex, month].value);
					ini.SetValue("Temperature" + monthstr, "highhumidextime", monthlyrecarray[AT_HighHumidex, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highheatindexvalue", monthlyrecarray[AT_HighHeatIndex, month].value);
					ini.SetValue("Temperature" + monthstr, "highheatindextime", monthlyrecarray[AT_HighHeatIndex, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highdewpointvalue", monthlyrecarray[AT_HighDewPoint, month].value);
					ini.SetValue("Temperature" + monthstr, "highdewpointtime", monthlyrecarray[AT_HighDewPoint, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowdewpointvalue", monthlyrecarray[AT_LowDewpoint, month].value);
					ini.SetValue("Temperature" + monthstr, "lowdewpointtime", monthlyrecarray[AT_LowDewpoint, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "hightemprangevalue", monthlyrecarray[AT_HighDailyTempRange, month].value);
					ini.SetValue("Temperature" + monthstr, "hightemprangetime", monthlyrecarray[AT_HighDailyTempRange, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowtemprangevalue", monthlyrecarray[AT_LowDailyTempRange, month].value);
					ini.SetValue("Temperature" + monthstr, "lowtemprangetime", monthlyrecarray[AT_LowDailyTempRange, month].timestamp);
					ini.SetValue("Wind" + monthstr, "highwindvalue", monthlyrecarray[AT_HighWind, month].value);
					ini.SetValue("Wind" + monthstr, "highwindtime", monthlyrecarray[AT_HighWind, month].timestamp);
					ini.SetValue("Wind" + monthstr, "highgustvalue", monthlyrecarray[AT_HighGust, month].value);
					ini.SetValue("Wind" + monthstr, "highgusttime", monthlyrecarray[AT_HighGust, month].timestamp);
					ini.SetValue("Wind" + monthstr, "highdailywindrunvalue", monthlyrecarray[AT_HighWindrun, month].value);
					ini.SetValue("Wind" + monthstr, "highdailywindruntime", monthlyrecarray[AT_HighWindrun, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highrainratevalue", monthlyrecarray[AT_HighRainRate, month].value);
					ini.SetValue("Rain" + monthstr, "highrainratetime", monthlyrecarray[AT_HighRainRate, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highdailyrainvalue", monthlyrecarray[AT_DailyRain, month].value);
					ini.SetValue("Rain" + monthstr, "highdailyraintime", monthlyrecarray[AT_DailyRain, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highhourlyrainvalue", monthlyrecarray[AT_HourlyRain, month].value);
					ini.SetValue("Rain" + monthstr, "highhourlyraintime", monthlyrecarray[AT_HourlyRain, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highmonthlyrainvalue", monthlyrecarray[AT_WetMonth, month].value);
					ini.SetValue("Rain" + monthstr, "highmonthlyraintime", monthlyrecarray[AT_WetMonth, month].timestamp);
					ini.SetValue("Rain" + monthstr, "longestdryperiodvalue", monthlyrecarray[AT_LongestDryPeriod, month].value);
					ini.SetValue("Rain" + monthstr, "longestdryperiodtime", monthlyrecarray[AT_LongestDryPeriod, month].timestamp);
					ini.SetValue("Rain" + monthstr, "longestwetperiodvalue", monthlyrecarray[AT_LongestWetPeriod, month].value);
					ini.SetValue("Rain" + monthstr, "longestwetperiodtime", monthlyrecarray[AT_LongestWetPeriod, month].timestamp);
					ini.SetValue("Pressure" + monthstr, "highpressurevalue", monthlyrecarray[AT_HighPress, month].value);
					ini.SetValue("Pressure" + monthstr, "highpressuretime", monthlyrecarray[AT_HighPress, month].timestamp);
					ini.SetValue("Pressure" + monthstr, "lowpressurevalue", monthlyrecarray[AT_LowPress, month].value);
					ini.SetValue("Pressure" + monthstr, "lowpressuretime", monthlyrecarray[AT_LowPress, month].timestamp);
					ini.SetValue("Humidity" + monthstr, "highhumidityvalue", monthlyrecarray[AT_HighHumidity, month].value);
					ini.SetValue("Humidity" + monthstr, "highhumiditytime", monthlyrecarray[AT_HighHumidity, month].timestamp);
					ini.SetValue("Humidity" + monthstr, "lowhumidityvalue", monthlyrecarray[AT_lowhumidity, month].value);
					ini.SetValue("Humidity" + monthstr, "lowhumiditytime", monthlyrecarray[AT_lowhumidity, month].timestamp);
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
			HighGustThisMonth = 0;
			HighWindThisMonth = 0;
			HighTempThisMonth = -999;
			LowTempThisMonth = 999;
			HighAppTempThisMonth = -999;
			LowAppTempThisMonth = 999;
			HighFeelsLikeThisMonth = -999;
			LowFeelsLikeThisMonth = 999;
			HighHumidexThisMonth = -999;
			HighDewpointThisMonth = -999;
			LowDewpointThisMonth = 999;
			HighPressThisMonth = 0;
			LowPressThisMonth = 9999;
			HighRainThisMonth = 0;
			HighHourlyRainThisMonth = 0;
			HighDailyRainThisMonth = 0;
			HighHumidityThisMonth = 0;
			LowHumidityThisMonth = 999;
			HighHeatIndexThisMonth = -999;
			LowWindChillThisMonth = 999;
			HighMinTempThisMonth = -999;
			LowMaxTempThisMonth = 999;
			HighDailyWindrunThisMonth = 0;
			LowDailyTempRangeThisMonth = 999;
			HighDailyTempRangeThisMonth = -999;

			// this Month highs and lows - timestamps
			HighGustThisMonthTS = cumulus.defaultRecordTS;
			HighWindThisMonthTS = cumulus.defaultRecordTS;
			HighTempThisMonthTS = cumulus.defaultRecordTS;
			LowTempThisMonthTS = cumulus.defaultRecordTS;
			HighAppTempThisMonthTS = cumulus.defaultRecordTS;
			LowAppTempThisMonthTS = cumulus.defaultRecordTS;
			HighFeelsLikeThisMonthTS = cumulus.defaultRecordTS;
			LowFeelsLikeThisMonthTS = cumulus.defaultRecordTS;
			HighHumidexThisMonthTS = cumulus.defaultRecordTS;
			HighDewpointThisMonthTS = cumulus.defaultRecordTS;
			LowDewpointThisMonthTS = cumulus.defaultRecordTS;
			HighPressThisMonthTS = cumulus.defaultRecordTS;
			LowPressThisMonthTS = cumulus.defaultRecordTS;
			HighRainThisMonthTS = cumulus.defaultRecordTS;
			HighHourlyRainThisMonthTS = cumulus.defaultRecordTS;
			HighDailyRainThisMonthTS = cumulus.defaultRecordTS;
			HighHumidityThisMonthTS = cumulus.defaultRecordTS;
			LowHumidityThisMonthTS = cumulus.defaultRecordTS;
			HighHeatIndexThisMonthTS = cumulus.defaultRecordTS;
			LowWindChillThisMonthTS = cumulus.defaultRecordTS;
			HighMinTempThisMonthTS = cumulus.defaultRecordTS;
			LowMaxTempThisMonthTS = cumulus.defaultRecordTS;
			HighDailyRainThisMonthTS = cumulus.defaultRecordTS;
			LowDailyTempRangeThisMonthTS = cumulus.defaultRecordTS;
			HighDailyTempRangeThisMonthTS = cumulus.defaultRecordTS;
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

				HighWindThisMonth = ini.GetValue("Wind", "Speed", 0.0);
				HighWindThisMonthTS = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				HighGustThisMonth = ini.GetValue("Wind", "Gust", 0.0);
				HighGustThisMonthTS = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				HighDailyWindrunThisMonth = ini.GetValue("Wind", "Windrun", 0.0);
				HighDailyWindrunThisMonthTS = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				LowTempThisMonth = ini.GetValue("Temp", "Low", 999.0);
				LowTempThisMonthTS = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				HighTempThisMonth = ini.GetValue("Temp", "High", -999.0);
				HighTempThisMonthTS = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				LowMaxTempThisMonth = ini.GetValue("Temp", "LowMax", 999.0);
				LowMaxTempThisMonthTS = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				HighMinTempThisMonth = ini.GetValue("Temp", "HighMin", -999.0);
				HighMinTempThisMonthTS = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				LowDailyTempRangeThisMonth = ini.GetValue("Temp", "LowRange", 999.0);
				LowDailyTempRangeThisMonthTS = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				HighDailyTempRangeThisMonth = ini.GetValue("Temp", "HighRange", -999.0);
				HighDailyTempRangeThisMonthTS = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				LowPressThisMonth = ini.GetValue("Pressure", "Low", 9999.0);
				LowPressThisMonthTS = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				HighPressThisMonth = ini.GetValue("Pressure", "High", -9999.0);
				HighPressThisMonthTS = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				HighRainThisMonth = ini.GetValue("Rain", "High", 0.0);
				HighRainThisMonthTS = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				HighHourlyRainThisMonth = ini.GetValue("Rain", "HourlyHigh", 0.0);
				HighHourlyRainThisMonthTS = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				HighDailyRainThisMonth = ini.GetValue("Rain", "DailyHigh", 0.0);
				HighDailyRainThisMonthTS = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				LongestDryPeriodThisMonth = ini.GetValue("Rain", "LongestDryPeriod", 0);
				LongestDryPeriodThisMonthTS = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				LongestWetPeriodThisMonth = ini.GetValue("Rain", "LongestWetPeriod", 0);
				LongestWetPeriodThisMonthTS = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				LowHumidityThisMonth = ini.GetValue("Humidity", "Low", 999);
				LowHumidityThisMonthTS = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				HighHumidityThisMonth = ini.GetValue("Humidity", "High", -999);
				HighHumidityThisMonthTS = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				HighHeatIndexThisMonth = ini.GetValue("HeatIndex", "High", -999.0);
				HighHeatIndexThisMonthTS = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				LowAppTempThisMonth = ini.GetValue("AppTemp", "Low", 999.0);
				LowAppTempThisMonthTS = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				HighAppTempThisMonth = ini.GetValue("AppTemp", "High", -999.0);
				HighAppTempThisMonthTS = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				LowDewpointThisMonth = ini.GetValue("Dewpoint", "Low", 999.0);
				LowDewpointThisMonthTS = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				HighDewpointThisMonth = ini.GetValue("Dewpoint", "High", -999.0);
				HighDewpointThisMonthTS = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				LowWindChillThisMonth = ini.GetValue("WindChill", "Low", 999.0);
				LowWindChillThisMonthTS = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like temp
				LowFeelsLikeThisMonth = ini.GetValue("FeelsLike", "Low", 999.0);
				LowFeelsLikeThisMonthTS = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				HighFeelsLikeThisMonth = ini.GetValue("FeelsLike", "High", -999.0);
				HighFeelsLikeThisMonthTS = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				HighHumidexThisMonth = ini.GetValue("Humidex", "High", -999.0);
				HighHumidexThisMonthTS = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);

				cumulus.LogMessage("Month.ini file read");
			}
		}

		public void WriteMonthIniFile()
		{
			lock (monthIniThreadLock)
			{
				try
				{
					int hourInc = cumulus.GetHourInc();

					IniFile ini = new IniFile(cumulus.MonthIniFile);
					// Date
					ini.SetValue("General", "Date", DateTime.Now.AddHours(hourInc));
					// Wind
					ini.SetValue("Wind", "Speed", HighWindThisMonth);
					ini.SetValue("Wind", "SpTime", HighWindThisMonthTS);
					ini.SetValue("Wind", "Gust", HighGustThisMonth);
					ini.SetValue("Wind", "Time", HighGustThisMonthTS);
					ini.SetValue("Wind", "Windrun", HighDailyWindrunThisMonth);
					ini.SetValue("Wind", "WindrunTime", HighDailyWindrunThisMonthTS);
					// Temperature
					ini.SetValue("Temp", "Low", LowTempThisMonth);
					ini.SetValue("Temp", "LTime", LowTempThisMonthTS);
					ini.SetValue("Temp", "High", HighTempThisMonth);
					ini.SetValue("Temp", "HTime", HighTempThisMonthTS);
					ini.SetValue("Temp", "LowMax", LowMaxTempThisMonth);
					ini.SetValue("Temp", "LMTime", LowMaxTempThisMonthTS);
					ini.SetValue("Temp", "HighMin", HighMinTempThisMonth);
					ini.SetValue("Temp", "HMTime", HighMinTempThisMonthTS);
					ini.SetValue("Temp", "LowRange", LowDailyTempRangeThisMonth);
					ini.SetValue("Temp", "LowRangeTime", LowDailyTempRangeThisMonthTS);
					ini.SetValue("Temp", "HighRange", HighDailyTempRangeThisMonth);
					ini.SetValue("Temp", "HighRangeTime", HighDailyTempRangeThisMonthTS);
					// Pressure
					ini.SetValue("Pressure", "Low", LowPressThisMonth);
					ini.SetValue("Pressure", "LTime", LowPressThisMonthTS);
					ini.SetValue("Pressure", "High", HighPressThisMonth);
					ini.SetValue("Pressure", "HTime", HighPressThisMonthTS);
					// rain
					ini.SetValue("Rain", "High", HighRainThisMonth);
					ini.SetValue("Rain", "HTime", HighRainThisMonthTS);
					ini.SetValue("Rain", "HourlyHigh", HighHourlyRainThisMonth);
					ini.SetValue("Rain", "HHourlyTime", HighHourlyRainThisMonthTS);
					ini.SetValue("Rain", "DailyHigh", HighDailyRainThisMonth);
					ini.SetValue("Rain", "HDailyTime", HighDailyRainThisMonthTS);
					ini.SetValue("Rain", "LongestDryPeriod", LongestDryPeriodThisMonth);
					ini.SetValue("Rain", "LongestDryPeriodTime", LongestDryPeriodThisMonthTS);
					ini.SetValue("Rain", "LongestWetPeriod", LongestWetPeriodThisMonth);
					ini.SetValue("Rain", "LongestWetPeriodTime", LongestWetPeriodThisMonthTS);
					// humidity
					ini.SetValue("Humidity", "Low", LowHumidityThisMonth);
					ini.SetValue("Humidity", "LTime", LowHumidityThisMonthTS);
					ini.SetValue("Humidity", "High", HighHumidityThisMonth);
					ini.SetValue("Humidity", "HTime", HighHumidityThisMonthTS);
					// heat index
					ini.SetValue("HeatIndex", "High", HighHeatIndexThisMonth);
					ini.SetValue("HeatIndex", "HTime", HighHeatIndexThisMonthTS);
					// App temp
					ini.SetValue("AppTemp", "Low", LowAppTempThisMonth);
					ini.SetValue("AppTemp", "LTime", LowAppTempThisMonthTS);
					ini.SetValue("AppTemp", "High", HighAppTempThisMonth);
					ini.SetValue("AppTemp", "HTime", HighAppTempThisMonthTS);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", LowDewpointThisMonth);
					ini.SetValue("Dewpoint", "LTime", LowDewpointThisMonthTS);
					ini.SetValue("Dewpoint", "High", HighDewpointThisMonth);
					ini.SetValue("Dewpoint", "HTime", HighDewpointThisMonthTS);
					// wind chill
					ini.SetValue("WindChill", "Low", LowWindChillThisMonth);
					ini.SetValue("WindChill", "LTime", LowWindChillThisMonthTS);
					// feels like
					ini.SetValue("FeelsLike", "Low", LowFeelsLikeThisMonth);
					ini.SetValue("FeelsLike", "LTime", LowFeelsLikeThisMonthTS);
					ini.SetValue("FeelsLike", "High", HighFeelsLikeThisMonth);
					ini.SetValue("FeelsLike", "HTime", HighFeelsLikeThisMonthTS);
					// Humidex
					ini.SetValue("Humidex", "High", HighHumidexThisMonth);
					ini.SetValue("Humidex", "HTime", HighHumidexThisMonthTS);

					ini.Flush();
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error writing month.ini file: " + ex.Message);
				}
			}
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

				HighWindThisYear = ini.GetValue("Wind", "Speed", 0.0);
				HighWindThisYearTS = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				HighGustThisYear = ini.GetValue("Wind", "Gust", 0.0);
				HighGustThisYearTS = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				HighDailyWindrunThisYear = ini.GetValue("Wind", "Windrun", 0.0);
				HighDailyWindrunThisYearTS = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				LowTempThisYear = ini.GetValue("Temp", "Low", 999.0);
				LowTempThisYearTS = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				HighTempThisYear = ini.GetValue("Temp", "High", -999.0);
				HighTempThisYearTS = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				LowMaxTempThisYear = ini.GetValue("Temp", "LowMax", 999.0);
				LowMaxTempThisYearTS = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				HighMinTempThisYear = ini.GetValue("Temp", "HighMin", -999.0);
				HighMinTempThisYearTS = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				LowDailyTempRangeThisYear = ini.GetValue("Temp", "LowRange", 999.0);
				LowDailyTempRangeThisYearTS = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				HighDailyTempRangeThisYear = ini.GetValue("Temp", "HighRange", -999.0);
				HighDailyTempRangeThisYearTS = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				LowPressThisYear = ini.GetValue("Pressure", "Low", 9999.0);
				LowPressThisYearTS = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				HighPressThisYear = ini.GetValue("Pressure", "High", -9999.0);
				HighPressThisYearTS = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				HighRainThisYear = ini.GetValue("Rain", "High", 0.0);
				HighRainThisYearTS = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				HighHourlyRainThisYear = ini.GetValue("Rain", "HourlyHigh", 0.0);
				HighHourlyRainThisYearTS = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				HighDailyRainThisYear = ini.GetValue("Rain", "DailyHigh", 0.0);
				HighDailyRainThisYearTS = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				HighMonthlyRainThisYear = ini.GetValue("Rain", "MonthlyHigh", 0.0);
				HighMonthlyRainThisYearTS = ini.GetValue("Rain", "HMonthlyTime", cumulus.defaultRecordTS);
				LongestDryPeriodThisYear = ini.GetValue("Rain", "LongestDryPeriod", 0);
				LongestDryPeriodThisYearTS = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				LongestWetPeriodThisYear = ini.GetValue("Rain", "LongestWetPeriod", 0);
				LongestWetPeriodThisYearTS = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				LowHumidityThisYear = ini.GetValue("Humidity", "Low", 999);
				LowHumidityThisYearTS = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				HighHumidityThisYear = ini.GetValue("Humidity", "High", -999);
				HighHumidityThisYearTS = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				HighHeatIndexThisYear = ini.GetValue("HeatIndex", "High", -999.0);
				HighHeatIndexThisYearTS = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				LowAppTempThisYear = ini.GetValue("AppTemp", "Low", 999.0);
				LowAppTempThisYearTS = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				HighAppTempThisYear = ini.GetValue("AppTemp", "High", -999.0);
				HighAppTempThisYearTS = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				LowDewpointThisYear = ini.GetValue("Dewpoint", "Low", 999.0);
				LowDewpointThisYearTS = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				HighDewpointThisYear = ini.GetValue("Dewpoint", "High", -999.0);
				HighDewpointThisYearTS = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				LowWindChillThisYear = ini.GetValue("WindChill", "Low", 999.0);
				LowWindChillThisYearTS = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like
				LowFeelsLikeThisYear = ini.GetValue("FeelsLike", "Low", 999.0);
				LowFeelsLikeThisYearTS = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				HighFeelsLikeThisYear = ini.GetValue("FeelsLike", "High", -999.0);
				HighFeelsLikeThisYearTS = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				HighHumidexThisYear = ini.GetValue("Humidex", "High", -999.0);
				HighHumidexThisYearTS = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);

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
					ini.SetValue("Wind", "Speed", HighWindThisYear);
					ini.SetValue("Wind", "SpTime", HighWindThisYearTS);
					ini.SetValue("Wind", "Gust", HighGustThisYear);
					ini.SetValue("Wind", "Time", HighGustThisYearTS);
					ini.SetValue("Wind", "Windrun", HighDailyWindrunThisYear);
					ini.SetValue("Wind", "WindrunTime", HighDailyWindrunThisYearTS);
					// Temperature
					ini.SetValue("Temp", "Low", LowTempThisYear);
					ini.SetValue("Temp", "LTime", LowTempThisYearTS);
					ini.SetValue("Temp", "High", HighTempThisYear);
					ini.SetValue("Temp", "HTime", HighTempThisYearTS);
					ini.SetValue("Temp", "LowMax", LowMaxTempThisYear);
					ini.SetValue("Temp", "LMTime", LowMaxTempThisYearTS);
					ini.SetValue("Temp", "HighMin", HighMinTempThisYear);
					ini.SetValue("Temp", "HMTime", HighMinTempThisYearTS);
					ini.SetValue("Temp", "LowRange", LowDailyTempRangeThisYear);
					ini.SetValue("Temp", "LowRangeTime", LowDailyTempRangeThisYearTS);
					ini.SetValue("Temp", "HighRange", HighDailyTempRangeThisYear);
					ini.SetValue("Temp", "HighRangeTime", HighDailyTempRangeThisYearTS);
					// Pressure
					ini.SetValue("Pressure", "Low", LowPressThisYear);
					ini.SetValue("Pressure", "LTime", LowPressThisYearTS);
					ini.SetValue("Pressure", "High", HighPressThisYear);
					ini.SetValue("Pressure", "HTime", HighPressThisYearTS);
					// rain
					ini.SetValue("Rain", "High", HighRainThisYear);
					ini.SetValue("Rain", "HTime", HighRainThisYearTS);
					ini.SetValue("Rain", "HourlyHigh", HighHourlyRainThisYear);
					ini.SetValue("Rain", "HHourlyTime", HighHourlyRainThisYearTS);
					ini.SetValue("Rain", "DailyHigh", HighDailyRainThisYear);
					ini.SetValue("Rain", "HDailyTime", HighDailyRainThisYearTS);
					ini.SetValue("Rain", "MonthlyHigh", HighMonthlyRainThisYear);
					ini.SetValue("Rain", "HMonthlyTime", HighMonthlyRainThisYearTS);
					ini.SetValue("Rain", "LongestDryPeriod", LongestDryPeriodThisYear);
					ini.SetValue("Rain", "LongestDryPeriodTime", LongestDryPeriodThisYearTS);
					ini.SetValue("Rain", "LongestWetPeriod", LongestWetPeriodThisYear);
					ini.SetValue("Rain", "LongestWetPeriodTime", LongestWetPeriodThisYearTS);
					// humidity
					ini.SetValue("Humidity", "Low", LowHumidityThisYear);
					ini.SetValue("Humidity", "LTime", LowHumidityThisYearTS);
					ini.SetValue("Humidity", "High", HighHumidityThisYear);
					ini.SetValue("Humidity", "HTime", HighHumidityThisYearTS);
					// heat index
					ini.SetValue("HeatIndex", "High", HighHeatIndexThisYear);
					ini.SetValue("HeatIndex", "HTime", HighHeatIndexThisYearTS);
					// App temp
					ini.SetValue("AppTemp", "Low", LowAppTempThisYear);
					ini.SetValue("AppTemp", "LTime", LowAppTempThisYearTS);
					ini.SetValue("AppTemp", "High", HighAppTempThisYear);
					ini.SetValue("AppTemp", "HTime", HighAppTempThisYearTS);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", LowDewpointThisYear);
					ini.SetValue("Dewpoint", "LTime", LowDewpointThisYearTS);
					ini.SetValue("Dewpoint", "High", HighDewpointThisYear);
					ini.SetValue("Dewpoint", "HTime", HighDewpointThisYearTS);
					// wind chill
					ini.SetValue("WindChill", "Low", LowWindChillThisYear);
					ini.SetValue("WindChill", "LTime", LowWindChillThisYearTS);
					// Feels like
					ini.SetValue("FeelsLike", "Low", LowFeelsLikeThisYear);
					ini.SetValue("FeelsLike", "LTime", LowFeelsLikeThisYearTS);
					ini.SetValue("FeelsLike", "High", HighFeelsLikeThisYear);
					ini.SetValue("FeelsLike", "HTime", HighFeelsLikeThisYearTS);
					// Humidex
					ini.SetValue("Humidex", "High", HighHumidexThisYear);
					ini.SetValue("Humidex", "HTime", HighHumidexThisYearTS);

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
			HighGustThisYear = 0;
			HighWindThisYear = 0;
			HighTempThisYear = -999;
			LowTempThisYear = 999;
			HighAppTempThisYear = -999;
			LowAppTempThisYear = 999;
			HighFeelsLikeThisYear = -999;
			LowFeelsLikeThisYear = 999;
			HighHumidexThisYear = -999;
			HighDewpointThisYear = -999;
			LowDewpointThisYear = 999;
			HighPressThisYear = 0;
			LowPressThisYear = 9999;
			HighRainThisYear = 0;
			HighHourlyRainThisYear = 0;
			HighDailyRainThisYear = 0;
			HighMonthlyRainThisYear = 0;
			HighHumidityThisYear = 0;
			LowHumidityThisYear = 999;
			HighHeatIndexThisYear = -999;
			LowWindChillThisYear = 999;
			HighMinTempThisYear = -999;
			LowMaxTempThisYear = 999;
			HighDailyWindrunThisYear = 0;
			LowDailyTempRangeThisYear = 999;
			HighDailyTempRangeThisYear = -999;

			// this Year highs and lows - timestamps
			HighGustThisYearTS = cumulus.defaultRecordTS;
			HighWindThisYearTS = cumulus.defaultRecordTS;
			HighTempThisYearTS = cumulus.defaultRecordTS;
			LowTempThisYearTS = cumulus.defaultRecordTS;
			HighAppTempThisYearTS = cumulus.defaultRecordTS;
			LowAppTempThisYearTS = cumulus.defaultRecordTS;
			HighFeelsLikeThisYearTS = cumulus.defaultRecordTS;
			LowFeelsLikeThisYearTS = cumulus.defaultRecordTS;
			HighHumidexThisYearTS = cumulus.defaultRecordTS;
			HighDewpointThisYearTS = cumulus.defaultRecordTS;
			LowDewpointThisYearTS = cumulus.defaultRecordTS;
			HighPressThisYearTS = cumulus.defaultRecordTS;
			LowPressThisYearTS = cumulus.defaultRecordTS;
			HighRainThisYearTS = cumulus.defaultRecordTS;
			HighHourlyRainThisYearTS = cumulus.defaultRecordTS;
			HighDailyRainThisYearTS = cumulus.defaultRecordTS;
			HighMonthlyRainThisYearTS = cumulus.defaultRecordTS;
			HighHumidityThisYearTS = cumulus.defaultRecordTS;
			LowHumidityThisYearTS = cumulus.defaultRecordTS;
			HighHeatIndexThisYearTS = cumulus.defaultRecordTS;
			LowWindChillThisYearTS = cumulus.defaultRecordTS;
			HighMinTempThisYearTS = cumulus.defaultRecordTS;
			LowMaxTempThisYearTS = cumulus.defaultRecordTS;
			HighDailyRainThisYearTS = cumulus.defaultRecordTS;
			LowDailyTempRangeThisYearTS = cumulus.defaultRecordTS;
			HighDailyTempRangeThisYearTS = cumulus.defaultRecordTS;
		}

		public string GetWCloudURL(out string pwstring, DateTime timestamp)
		{
			pwstring = cumulus.WCloud.PW;
			StringBuilder sb = new StringBuilder($"http://api.weathercloud.net/v01/set?wid={cumulus.WCloud.ID}&key={pwstring}");

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

			// time
			sb.Append("&time=" + timestamp.ToString("HHmm"));

			// date
			sb.Append("&date=" + timestamp.ToString("yyyyMMdd"));

			// software identification
			sb.Append("&type=291&ver=" + cumulus.Version);

			return sb.ToString();
		}

		private string ByteArrayToString(byte[] ba)
		{
			StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}

		/*
		public string GetAwekasURL(out string pwstring, DateTime timestamp)
		{
			var InvC = new CultureInfo("");
			byte[] hashPW;
			string sep = ";";

			// password is sent as MD5 hash
			using (MD5 md5 = MD5.Create())
			{
				hashPW = md5.ComputeHash(Encoding.ASCII.GetBytes(cumulus.AwekasPW));
			}

			pwstring = ByteArrayToString(hashPW);

			int presstrend;

			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.PressUnit)
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

			StringBuilder sb = new StringBuilder("http://data.awekas.at/eingabe_pruefung.php?val=");
			sb.Append(cumulus.AwekasUser + sep); // 1
			sb.Append(pwstring + sep); //2
			sb.Append(timestamp.ToString("dd'.'MM'.'yyyy';'HH':'mm") + sep); // 3 + 4
			sb.Append(ConvertUserTempToC(OutdoorTemperature).ToString("F1", InvC) + sep); // 5
			sb.Append(OutdoorHumidity + sep); // 6
			sb.Append(ConvertUserPressToMB(Pressure).ToString("F1", InvC) + sep); // 7
			sb.Append(ConvertUserRainToMM(RainToday).ToString("F1", InvC) + sep); // 8
			sb.Append(ConvertUserWindToKPH(WindAverage).ToString("F1", InvC) + sep); // 9
			sb.Append(AvgBearing + sep); // 10
			sb.Append(sep + sep + sep); // 11/12/13 - condition and warning, snow height
			sb.Append(cumulus.AwekasLang + sep); // 14
			sb.Append(presstrend + sep); // 15
			sb.Append(ConvertUserWindToKPH(RecentMaxGust).ToString("F1", InvC) + sep); // 16

			if (cumulus.SendSolarToAwekas)
			{
				sb.Append(SolarRad.ToString("F1", InvC) + sep); // 17
			}
			else
			{
				sb.Append(sep);
			}

			if (cumulus.SendUVToAwekas)
			{
				sb.Append(UV.ToString("F1", InvC) + sep); // 18
			}
			else
			{
				sb.Append(sep);
			}

			if (cumulus.SendSolarToAwekas)
			{
				if (cumulus.StationType == StationTypes.FineOffsetSolar) {
					sb.Append(LightValue.ToString("F0", InvC) + sep); // 19
				}
				else
				{
					sb.Append(sep);
				}

				sb.Append(SunshineHours.ToString("F0", InvC) + sep); // 20
			}
			else
			{
				sb.Append(sep + sep);
			}

			if (cumulus.SendSoilTempToAwekas)
			{
				sb.Append(ConvertUserTempToC(SoilTemp1).ToString("F1", InvC) + sep); // 21
			}
			else
			{
				sb.Append(sep);
			}

			sb.Append(ConvertUserRainToMM(RainRate).ToString("F1", InvC) + sep); // 22

			sb.Append("Cum_" + cumulus.Version + sep); //23

			sb.Append(sep + sep); // 24/25 location for mobile

			sb.Append(ConvertUserTempToC(HiLoToday.LowTemp).ToString("F1", InvC) + sep); // 26

			sb.Append(ConvertUserTempToC(AvgTemp).ToString("F1", InvC) + sep); // 27

			sb.Append(ConvertUserTempToC(HiLoToday.HighTemp).ToString("F1", InvC) + sep); // 28

			sb.Append(ConvertUserTempToC(LowTempThisMonth).ToString("F1", InvC) + sep); // 29

			sb.Append(sep); // 30 avg temp this month

			sb.Append(ConvertUserTempToC(HighTempThisMonth).ToString("F1", InvC) + sep); // 31

			sb.Append(ConvertUserTempToC(LowTempThisYear).ToString("F1", InvC) + sep); // 32

			sb.Append(sep); // 33 avg temp this year

			sb.Append(ConvertUserTempToC(HighTempThisYear).ToString("F1", InvC) + sep); // 34

			sb.Append(HiLoToday.LowHumidity + sep); // 35

			sb.Append(sep); // 36 avg hum today

			sb.Append(HiLoToday.HighHumidity + sep); // 37

			sb.Append(LowHumidityThisMonth + sep); // 38

			sb.Append(sep); // 39 avg hum this month

			sb.Append(HighHumidityThisMonth + sep); // 40

			sb.Append(LowHumidityThisYear + sep); // 41

			sb.Append(sep); // 42 avg hum this year

			sb.Append(HighHumidityThisYear + sep); // 43

			sb.Append(ConvertUserPressToMB(HiLoToday.LowPress).ToString("F1", InvC) + sep); // 44

			sb.Append(sep); // 45 avg press today

			sb.Append(ConvertUserPressToMB(HiLoToday.HighPress).ToString("F1", InvC) + sep); // 46

			sb.Append(ConvertUserPressToMB(LowPressThisMonth).ToString("F1", InvC) + sep); // 47

			sb.Append(sep); // 48 avg press this month

			sb.Append(ConvertUserPressToMB(HighPressThisMonth).ToString("F1", InvC) + sep); // 49

			sb.Append(ConvertUserPressToMB(LowPressThisYear).ToString("F1", InvC) + sep); // 50

			sb.Append(sep); // 51 avg press this year

			sb.Append(ConvertUserPressToMB(HighPressThisYear).ToString("F1", InvC) + sep); // 52

			sb.Append(sep + sep); // 53/54 min/avg wind today

			sb.Append(ConvertUserWindToKPH(HiLoToday.HighWind).ToString("F1", InvC) + sep); // 55

			sb.Append(sep + sep); // 56/57 min/avg wind this month

			sb.Append(ConvertUserWindToKPH(HighWindThisMonth).ToString("F1", InvC) + sep); // 58

			sb.Append(sep + sep); // 59/60 min/avg wind this year

			sb.Append(ConvertUserWindToKPH(HighWindThisYear).ToString("F1", InvC) + sep); // 61

			sb.Append(sep + sep); // 62/63 min/avg gust today

			sb.Append(ConvertUserWindToKPH(HiLoToday.HighGust).ToString("F1", InvC) + sep); // 64

			sb.Append(sep + sep); // 65/66 min/avg gust this month

			sb.Append(ConvertUserWindToKPH(HighGustThisMonth).ToString("F1", InvC) + sep); // 67

			sb.Append(sep + sep); // 68/69 min/avg gust this year

			sb.Append(ConvertUserWindToKPH(HighGustThisYear).ToString("F1", InvC) + sep); // 70

			sb.Append(sep + sep + sep); // 71/72/73 avg wind bearing today/month/year

			sb.Append(ConvertUserRainToMM(RainLast24Hour).ToString("F1", InvC) + sep); // 74

			sb.Append(ConvertUserRainToMM(RainMonth).ToString("F1", InvC) + sep); // 75

			sb.Append(ConvertUserRainToMM(RainYear).ToString("F1", InvC) + sep); // 76

			sb.Append(sep); // 77 avg rain rate today

			sb.Append(ConvertUserRainToMM(HiLoToday.HighRain).ToString("F1", InvC) + sep); // 78

			sb.Append(sep); // 79 avg rain rate this month

			sb.Append(ConvertUserRainToMM(HighRainThisMonth).ToString("F1", InvC) + sep); // 80

			sb.Append(sep); // 81 avg rain rate this year

			sb.Append(ConvertUserRainToMM(HighRainThisYear).ToString("F1", InvC) + sep); // 82

			sb.Append(sep); // 83 avg solar today

			sb.Append(HiLoToday.HighSolar.ToString("F1", InvC)); // 84

			sb.Append(sep); // 85 avg solar this month

			sb.Append(sep); // 86 high solar this month

			sb.Append(sep); // 87 avg solar this year

			sb.Append(sep); // 88 high solar this year

			sb.Append(sep); // 89 avg uv today

			sb.Append(HiLoToday.HighUv.ToString("F1", InvC)); // 90

			sb.Append(sep); // 91 avg uv this month

			sb.Append(sep); // 92 high uv this month

			sb.Append(sep); // 93 avg uv this year

			sb.Append(sep); // 94 high uv this year

			sb.Append(sep + sep + sep + sep + sep + sep); // 95/96/97/98/99/100 avg/max lux today/month/year

			sb.Append(sep + sep); // 101/102 sun hours this month/year

			sb.Append(sep + sep + sep + sep + sep + sep + sep + sep + sep); //103-111 min/avg/max Soil temp today/month/year

			return sb.ToString();
		}
		*/

		public string GetAwekasURLv4(out string pwstring, DateTime timestamp)
		{
			var InvC = new CultureInfo("");
			byte[] hashPW;
			string sep = ";";

			// password is sent as MD5 hash
			using (MD5 md5 = MD5.Create())
			{
				hashPW = md5.ComputeHash(Encoding.ASCII.GetBytes(cumulus.AWEKAS.PW));
			}

			pwstring = ByteArrayToString(hashPW);

			int presstrend;

			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.PressUnit)
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

			StringBuilder sb = new StringBuilder("http://data.awekas.at/eingabe_pruefung.php?val=");
			// val
			sb.Append(cumulus.AWEKAS.ID + sep);											// 1
			sb.Append(pwstring + sep);														// 2
			sb.Append(timestamp.ToString("dd'.'MM'.'yyyy';'HH':'mm") + sep);				// 3 + 4
			sb.Append(ConvertUserTempToC(OutdoorTemperature).ToString("F1", InvC) + sep);	// 5
			sb.Append(OutdoorHumidity + sep);												// 6
			sb.Append(ConvertUserPressToMB(Pressure).ToString("F1", InvC) + sep);			// 7
			sb.Append(ConvertUserRainToMM(RainSinceMidnight).ToString("F1", InvC) + sep);	// 8   - was RainToday in v2
			sb.Append(ConvertUserWindToKPH(WindAverage).ToString("F1", InvC) + sep);		// 9
			sb.Append(AvgBearing + sep);													// 10
			sb.Append(sep + sep + sep);														// 11/12/13 - condition and warning, snow height
			sb.Append(cumulus.AWEKAS.Lang + sep);											// 14
			sb.Append(presstrend + sep);													// 15
			sb.Append(ConvertUserWindToKPH(RecentMaxGust).ToString("F1", InvC) + sep);		// 16

			if (cumulus.AWEKAS.SendSolar)
				sb.Append(SolarRad.ToString("F1", InvC) + sep);								// 17
			else
				sb.Append(sep);

			if (cumulus.AWEKAS.SendUV)
				sb.Append(UV.ToString("F1", InvC) + sep);									// 18
			else
				sb.Append(sep);

			if (cumulus.AWEKAS.SendSolar)
			{
				if (cumulus.StationType == StationTypes.FineOffsetSolar)
					sb.Append(LightValue.ToString("F0", InvC) + sep);						// 19
				else
					sb.Append(sep);

				sb.Append(SunshineHours.ToString("F2", InvC) + sep);						// 20
			}
			else
			{
				sb.Append(sep + sep);
			}

			if (cumulus.AWEKAS.SendSoilTemp)
				sb.Append(ConvertUserTempToC(SoilTemp1).ToString("F1", InvC) + sep);		// 21
			else
				sb.Append(sep);

			sb.Append(ConvertUserRainToMM(RainRate).ToString("F1", InvC) + sep);			// 22
			sb.Append("Cum_" + cumulus.Version + sep);										// 23
			sb.Append(sep + sep);															// 24/25 location for mobile
			sb.Append(ConvertUserTempToC(HiLoToday.LowTemp).ToString("F1", InvC) + sep);			// 26
			sb.Append(ConvertUserTempToC(AvgTemp).ToString("F1", InvC) + sep);				// 27
			sb.Append(ConvertUserTempToC(HiLoToday.HighTemp).ToString("F1", InvC) + sep);		// 28
			sb.Append(ConvertUserTempToC(LowTempThisMonth).ToString("F1", InvC) + sep);		// 29
			sb.Append(sep);																	// 30 avg temp this month
			sb.Append(ConvertUserTempToC(HighTempThisMonth).ToString("F1", InvC) + sep);	// 31
			sb.Append(ConvertUserTempToC(LowTempThisYear).ToString("F1", InvC) + sep);		// 32
			sb.Append(sep);																	// 33 avg temp this year
			sb.Append(ConvertUserTempToC(HighTempThisYear).ToString("F1", InvC) + sep);		// 34
			sb.Append(HiLoToday.LowHumidity + sep);												// 35
			sb.Append(sep);																	// 36 avg hum today
			sb.Append(HiLoToday.HighHumidity + sep);												// 37
			sb.Append(LowHumidityThisMonth + sep);											// 38
			sb.Append(sep);																	// 39 avg hum this month
			sb.Append(HighHumidityThisMonth + sep);											// 40
			sb.Append(LowHumidityThisYear + sep);											// 41
			sb.Append(sep);																	// 42 avg hum this year
			sb.Append(HighHumidityThisYear + sep);											// 43
			sb.Append(ConvertUserPressToMB(HiLoToday.LowPress).ToString("F1", InvC) + sep);		// 44
			sb.Append(sep);																	// 45 avg press today
			sb.Append(ConvertUserPressToMB(HiLoToday.HighPress).ToString("F1", InvC) + sep);		// 46
			sb.Append(ConvertUserPressToMB(LowPressThisMonth).ToString("F1", InvC) + sep);	// 47
			sb.Append(sep);																	// 48 avg press this month
			sb.Append(ConvertUserPressToMB(HighPressThisMonth).ToString("F1", InvC) + sep);	// 49
			sb.Append(ConvertUserPressToMB(LowPressThisYear).ToString("F1", InvC) + sep);	// 50
			sb.Append(sep);																	// 51 avg press this year
			sb.Append(ConvertUserPressToMB(HighPressThisYear).ToString("F1", InvC) + sep);	// 52
			sb.Append(sep + sep);															// 53/54 min/avg wind today
			sb.Append(ConvertUserWindToKPH(HiLoToday.HighWind).ToString("F1", InvC) + sep);		// 55
			sb.Append(sep + sep);															// 56/57 min/avg wind this month
			sb.Append(ConvertUserWindToKPH(HighWindThisMonth).ToString("F1", InvC) + sep);	// 58
			sb.Append(sep + sep);															// 59/60 min/avg wind this year
			sb.Append(ConvertUserWindToKPH(HighWindThisYear).ToString("F1", InvC) + sep);	// 61
			sb.Append(sep + sep);															// 62/63 min/avg gust today
			sb.Append(ConvertUserWindToKPH(HiLoToday.HighGust).ToString("F1", InvC) + sep);		// 64
			sb.Append(sep + sep);															// 65/66 min/avg gust this month
			sb.Append(ConvertUserWindToKPH(HighGustThisMonth).ToString("F1", InvC) + sep);	// 67
			sb.Append(sep + sep);															// 68/69 min/avg gust this year
			sb.Append(ConvertUserWindToKPH(HighGustThisYear).ToString("F1", InvC) + sep);	// 70
			sb.Append(sep + sep + sep);														// 71/72/73 avg wind bearing today/month/year
			sb.Append(ConvertUserRainToMM(RainLast24Hour).ToString("F1", InvC) + sep);		// 74
			sb.Append(ConvertUserRainToMM(RainMonth).ToString("F1", InvC) + sep);			// 75
			sb.Append(ConvertUserRainToMM(RainYear).ToString("F1", InvC) + sep);			// 76
			sb.Append(sep);																	// 77 avg rain rate today
			sb.Append(ConvertUserRainToMM(HiLoToday.HighRain).ToString("F1", InvC) + sep);		// 78
			sb.Append(sep);																	// 79 avg rain rate this month
			sb.Append(ConvertUserRainToMM(HighRainThisMonth).ToString("F1", InvC) + sep);	// 80
			sb.Append(sep);																	// 81 avg rain rate this year
			sb.Append(ConvertUserRainToMM(HighRainThisYear).ToString("F1", InvC) + sep);	// 82
			sb.Append(sep);																	// 83 avg solar today
			if (cumulus.AWEKAS.SendSolar)
				sb.Append(HiLoToday.HighSolar.ToString("F1", InvC));								// 84
			else
				sb.Append(sep);

			sb.Append(sep + sep);															// 85/86 avg/high solar this month
			sb.Append(sep + sep);															// 87/88 avg/high solar this year
			sb.Append(sep);																	// 89 avg uv today

			if (cumulus.AWEKAS.SendUV)
				sb.Append(HiLoToday.HighUv.ToString("F1", InvC));								// 90
			else
				sb.Append(sep);

			sb.Append(sep + sep);															// 91/92 avg/high uv this month
			sb.Append(sep + sep);															// 93/94 avg/high uv this year
			sb.Append(sep + sep + sep + sep + sep + sep);									// 95/96/97/98/99/100 avg/max lux today/month/year
			sb.Append(sep + sep);															// 101/102 sun hours this month/year
			sb.Append(sep + sep + sep + sep + sep + sep + sep + sep + sep);					// 103-111 min/avg/max Soil temp today/month/year


			// indoor temp/humidity
			if (cumulus.AWEKAS.SendIndoor)
			{
				sb.Append("&indoortemp=" + ConvertUserTempToC(IndoorTemperature).ToString("F1", InvC));
				sb.Append("&indoorhumidity=" + IndoorHumidity);
			}

			if (cumulus.AWEKAS.SendSoilTemp)
			{
				sb.Append("&soiltemp1=" + ConvertUserTempToC(SoilTemp1).ToString("F1", InvC));
				sb.Append("&soiltemp2=" + ConvertUserTempToC(SoilTemp2).ToString("F1", InvC));
				sb.Append("&soiltemp3=" + ConvertUserTempToC(SoilTemp3).ToString("F1", InvC));
				sb.Append("&soiltemp4=" + ConvertUserTempToC(SoilTemp4).ToString("F1", InvC));
			}

			if (cumulus.AWEKAS.SendSoilMoisture)
			{
				sb.Append("&soilmoisture1=" + SoilMoisture1);
				sb.Append("&soilmoisture2=" + SoilMoisture2);
				sb.Append("&soilmoisture3=" + SoilMoisture3);
				sb.Append("&soilmoisture4=" + SoilMoisture4);
			}

			if (cumulus.AWEKAS.SendLeafWetness)
			{
				sb.Append("&leafwetness1=" + LeafWetness1);
				sb.Append("&leafwetness2=" + LeafWetness2);
				sb.Append("&leafwetness3=" + LeafWetness3);
				sb.Append("&leafwetness4=" + LeafWetness4);
			}

			if (cumulus.AWEKAS.SendAirQuality)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case 0: // Davis AirLink Outdoor
						if (cumulus.airLinkDataOut != null)
						{
							sb.Append($"&AqPM1={cumulus.airLinkDataOut.pm1:F1}");
							sb.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5:F1}");
							sb.Append($"&AqPM10={cumulus.airLinkDataOut.pm10:F1}");
							sb.Append($"&AqPM2.5_avg_24h={cumulus.airLinkDataOut.pm2p5_24hr:F1}");
							sb.Append($"&AqPM10_avg_24h={cumulus.airLinkDataOut.pm10_24hr:F1}");
						}
						break;
					case 1: // Ecowitt sensor 1
						sb.Append($"&AqPM2.5={AirQuality1:F1}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg1:F1}");
						break;
					case 2: // Ecowitt sensor 2
						sb.Append($"&AqPM2.5={AirQuality2:F1}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg2:F1}");
						break;
					case 3: // Ecowitt sensor 3
						sb.Append($"&AqPM2.5={AirQuality3:F1}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg3:F1}");
						break;
					case 4: // Ecowitt sensor 4
						sb.Append($"&AqPM2.5={AirQuality4:F1}");
						sb.Append($"&AqPM2.5_avg_24h={AirQualityAvg4:F1}");
						break;
				}
			}

			sb.Append("&output=json");

			return sb.ToString();
		}


		public string GetWundergroundURL(out string pwstring, DateTime timestamp, bool catchup)
		{
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
				Data.Append($"&UV={UV.ToString(cumulus.UVFormat)}");
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

			if (cumulus.Wund.SendAirQuality && cumulus.StationOptions.PrimaryAqSensor >= 0)
			{
				switch (cumulus.StationOptions.PrimaryAqSensor)
				{
					case 0: //AirLink Outdoor
						if (cumulus.airLinkDataOut != null)
						{
							Data.Append($"&AqPM2.5={cumulus.airLinkDataOut.pm2p5:F1}&AqPM10={cumulus.airLinkDataOut.pm10:F1}");
						}
						break;
					case 1: // Ecowitt sensor 1
						Data.Append($"&AqPM2.5={AirQuality1:F1}");
						break;
					case 2: // Ecowitt sensor 2
						Data.Append($"&AqPM2.5={AirQuality2:F1}");
						break;
					case 3: // Ecowitt sensor 4
						Data.Append($"&AqPM2.5={AirQuality3:F1}");
						break;
					case 4: // Ecowitt sensor 4
						Data.Append($"&AqPM2.5={AirQuality4:F1}");
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
			Data.Append("&windspeedmph=" + WindMPHStr(WindAverage));
			Data.Append("&windgustmph=" + WindMPHStr(RecentMaxGust));
			Data.Append("&tempf=" + TempFstr(OutdoorTemperature));
			Data.Append("&rainin=" + RainINstr(RainLastHour));
			Data.Append("&baromin=" + PressINstr(Pressure));
			Data.Append("&dewptf=" + TempFstr(OutdoorDewpoint));
			Data.Append("&humidity=" + OutdoorHumidity);

			if (cumulus.Windy.SendUV)
				Data.Append("&uv=" + UV.ToString(cumulus.UVFormat));
			if (cumulus.Windy.SendSolar)
				Data.Append("&solarradiation=" + SolarRad.ToString("F0"));

			Data.Replace(",", ".");
			URL.Append(Data);

			return URL.ToString();
		}

		private string PressINstr(double pressure)
		{
			var pressIN = ConvertUserPressToIN(pressure);

			return pressIN.ToString("F3");
		}

		private string WindMPHStr(double wind)
		{
			var windMPH = ConvertUserWindToMPH(wind);
			if (cumulus.StationOptions.RoundWindSpeed)
				windMPH = Math.Round(windMPH);

			return windMPH.ToString("F1");
		}

		/// <summary>
		/// Convert rain in user units to inches for WU etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		private string RainINstr(double rain)
		{
			var rainIN = ConvertUserRainToIn(rain);

			return rainIN.ToString("F2");
		}

		/// <summary>
		/// Convert temp in user units to F for WU etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		private string TempFstr(double temp)
		{
			double tempf = ConvertUserTempToF(temp);

			return tempf.ToString("F1");
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
				Data.Append("&UV=" + UV.ToString(cumulus.UVFormat));
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
				Data.Append("&UV=" + UV.ToString(cumulus.UVFormat));
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
			if (cumulus.RainUnit == 0)
			{
				return rain * 0.0393700787;
			}
			else
			{
				return rain;
			}
		}

		private string alltimejsonformat(int item, string unit, string valueformat, string dateformat)
		{
			return $"[\"{alltimedescs[item]}\",\"{alltimerecarray[item].value.ToString(valueformat)} {unit}\",\"{alltimerecarray[item].timestamp.ToString(dateformat)}\"]";
		}

		public string GetTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 2048);

			json.Append(alltimejsonformat(AT_HighTemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_LowTemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighDewPoint, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_LowDewpoint, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighAppTemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_LowAppTemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighFeelsLike, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_LowFeelsLike, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighHumidex, "&nbsp;", cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_LowChill, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighHeatIndex, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighMinTemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_LowMaxTemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(AT_HighDailyTempRange, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D") + ",");
			json.Append(alltimejsonformat(AT_LowDailyTempRange, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AT_HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_lowhumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AT_HighPress, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_LowPress, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AT_HighGust, cumulus.WindUnitText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_HighWind, cumulus.WindUnitText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_HighWindrun, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(AT_HighRainRate, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_HourlyRain, cumulus.RainUnitText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_DailyRain, cumulus.RainUnitText, cumulus.RainFormat, "D"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_WetMonth, cumulus.RainUnitText, cumulus.RainFormat, "Y"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_LongestDryPeriod, "days", "f0", "D"));
			json.Append(",");
			json.Append(alltimejsonformat(AT_LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		private string monthlyjsonformat(int item, int month, string unit, string valueformat, string dateformat)
		{
			return $"[\"{alltimedescs[item]}\",\"{monthlyrecarray[item, month].value.ToString(valueformat)} {unit}\",\"{monthlyrecarray[item, month].timestamp.ToString(dateformat)}\"]";
		}

		public string GetMonthlyTempRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthlyjsonformat(AT_HighTemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowTemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighDewPoint, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowDewpoint, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighAppTemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowAppTemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighFeelsLike, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowFeelsLike, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighHumidex, month, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowChill, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighHeatIndex, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighMinTemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowMaxTemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighDailyTempRange, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowDailyTempRange, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyHumRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(AT_HighHumidity, month, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_lowhumidity, month, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyPressRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(AT_HighPress, month, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LowPress, month, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyWindRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(AT_HighGust, month, cumulus.WindUnitText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighWind, month, cumulus.WindUnitText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HighWindrun, month, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyRainRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthlyjsonformat(AT_HighRainRate, month, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_HourlyRain, month, cumulus.RainUnitText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_DailyRain, month, cumulus.RainUnitText, cumulus.RainFormat, "D"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_WetMonth, month, cumulus.RainUnitText, cumulus.RainFormat, "Y"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LongestDryPeriod, month, "days", "f0", "D"));
			json.Append(",");
			json.Append(monthlyjsonformat(AT_LongestWetPeriod, month, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		private string monthyearjsonformat(int item, double value, DateTime timestamp, string unit, string valueformat, string dateformat)
		{
			return $"[\"{alltimedescs[item]}\",\"{value.ToString(valueformat)} {unit}\",\"{timestamp.ToString(dateformat)}\"]";
		}

		public string GetThisMonthTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(AT_HighTemp, HighTempThisMonth, HighTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowTemp, LowTempThisMonth, LowTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighDewPoint, HighDewpointThisMonth, HighDewpointThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowDewpoint, LowDewpointThisMonth, LowDewpointThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighAppTemp, HighAppTempThisMonth, HighAppTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowAppTemp, LowAppTempThisMonth, LowAppTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighFeelsLike, HighFeelsLikeThisMonth, HighFeelsLikeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowFeelsLike, LowFeelsLikeThisMonth, LowFeelsLikeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighHumidex, HighHumidexThisMonth, HighHumidexThisMonthTS, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowChill, LowWindChillThisMonth, LowWindChillThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighHeatIndex, HighHeatIndexThisMonth, HighHeatIndexThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighMinTemp, HighMinTempThisMonth, HighMinTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowMaxTemp, LowMaxTempThisMonth, LowMaxTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighDailyTempRange, HighDailyTempRangeThisMonth, HighDailyTempRangeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowDailyTempRange, LowDailyTempRangeThisMonth, LowDailyTempRangeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(AT_HighHumidity, HighHumidityThisMonth, HighHumidityThisMonthTS, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_lowhumidity, LowHumidityThisMonth, LowHumidityThisMonthTS, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(AT_HighPress, HighPressThisMonth, HighPressThisMonthTS, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowPress, LowPressThisMonth, LowPressThisMonthTS, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(AT_HighGust, HighGustThisMonth, HighGustThisMonthTS, cumulus.WindUnitText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighWind, HighWindThisMonth, HighWindThisMonthTS, cumulus.WindUnitText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighWindrun, HighDailyWindrunThisMonth, HighDailyWindrunThisMonthTS, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(AT_HighRainRate, HighRainThisMonth, HighRainThisMonthTS, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HourlyRain, HighHourlyRainThisMonth, HighHourlyRainThisMonthTS, cumulus.RainUnitText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_DailyRain, HighDailyRainThisMonth, HighDailyRainThisMonthTS, cumulus.RainUnitText, cumulus.RainFormat, "D"));
			json.Append(",");
			//json.Append(monthyearjsonformat(AT_WetMonth, month, cumulus.RainUnitText, cumulus.RainFormat, "Y"));
			//json.Append(",");
			json.Append(monthyearjsonformat(AT_LongestDryPeriod, LongestDryPeriodThisMonth, LongestDryPeriodThisMonthTS, "days", "f0", "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LongestWetPeriod, LongestWetPeriodThisMonth, LongestWetPeriodThisMonthTS, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(AT_HighTemp, HighTempThisYear, HighTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowTemp, LowTempThisYear, LowTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighDewPoint, HighDewpointThisYear, HighDewpointThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowDewpoint, LowDewpointThisYear, LowDewpointThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighAppTemp, HighAppTempThisYear, HighAppTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowAppTemp, LowAppTempThisYear, LowAppTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighFeelsLike, HighFeelsLikeThisYear, HighFeelsLikeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowFeelsLike, LowFeelsLikeThisYear, LowFeelsLikeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighHumidex, HighHumidexThisYear, HighHumidexThisYearTS, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowChill, LowWindChillThisYear, LowWindChillThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighHeatIndex, HighHeatIndexThisYear, HighHeatIndexThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighMinTemp, HighMinTempThisYear, HighMinTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowMaxTemp, LowMaxTempThisYear, LowMaxTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighDailyTempRange, HighDailyTempRangeThisYear, HighDailyTempRangeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowDailyTempRange, LowDailyTempRangeThisYear, LowDailyTempRangeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(AT_HighHumidity, HighHumidityThisYear, HighHumidityThisYearTS, "%", cumulus.HumFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_lowhumidity, LowHumidityThisYear, LowHumidityThisYearTS, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(AT_HighPress, HighPressThisYear, HighPressThisYearTS, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LowPress, LowPressThisYear, LowPressThisYearTS, cumulus.PressUnitText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(AT_HighGust, HighGustThisYear, HighGustThisYearTS, cumulus.WindUnitText, cumulus.WindFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighWind, HighWindThisYear, HighWindThisYearTS, cumulus.WindUnitText, cumulus.WindAvgFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HighWindrun, HighDailyWindrunThisYear, HighDailyWindrunThisYearTS, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(AT_HighRainRate, HighRainThisYear, HighRainThisYearTS, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_HourlyRain, HighHourlyRainThisYear, HighHourlyRainThisYearTS, cumulus.RainUnitText, cumulus.RainFormat, "f"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_DailyRain, HighDailyRainThisYear, HighDailyRainThisYearTS, cumulus.RainUnitText, cumulus.RainFormat, "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_WetMonth, HighMonthlyRainThisYear, HighMonthlyRainThisYearTS, cumulus.RainUnitText, cumulus.RainFormat, "Y"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LongestDryPeriod, LongestDryPeriodThisYear, LongestDryPeriodThisYearTS, "days", "f0", "D"));
			json.Append(",");
			json.Append(monthyearjsonformat(AT_LongestWetPeriod, LongestWetPeriodThisYear, LongestWetPeriodThisYearTS, "days", "f0", "D"));
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
				json.Append(cumulus.TempUnitText[1].ToString());
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
				json.Append(cumulus.TempUnitText[1].ToString());
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
				json.Append(cumulus.TempUnitText[1].ToString());
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

			json.Append($"[\"{cumulus.SoilTempCaptions[1]}\",\"{SoilTemp1.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[2]}\",\"{SoilTemp2.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[3]}\",\"{SoilTemp3.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[4]}\",\"{SoilTemp4.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[5]}\",\"{SoilTemp5.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[6]}\",\"{SoilTemp6.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[7]}\",\"{SoilTemp7.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[8]}\",\"{SoilTemp8.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[9]}\",\"{SoilTemp9.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[10]}\",\"{SoilTemp10.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[11]}\",\"{SoilTemp11.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[12]}\",\"{SoilTemp12.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[13]}\",\"{SoilTemp13.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[14]}\",\"{SoilTemp14.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[15]}\",\"{SoilTemp15.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.SoilTempCaptions[16]}\",\"{SoilTemp16.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"]");
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

			json.Append($"[\"{cumulus.AirQualityCaptions[1]}\",\"{AirQuality1.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityCaptions[2]}\",\"{AirQuality2.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityCaptions[3]}\",\"{AirQuality3.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityCaptions[4]}\",\"{AirQuality4.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[1]}\",\"{AirQualityAvg1.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[2]}\",\"{AirQualityAvg2.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[3]}\",\"{AirQualityAvg3.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"],");
			json.Append($"[\"{cumulus.AirQualityAvgCaptions[4]}\",\"{AirQualityAvg4.ToString(cumulus.TempFormat)}\",\"{cumulus.AirQualityUnitText}\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLightning()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"Distance to last strike\",\"{LightningDistance.ToString(cumulus.WindRunFormat)}\",\"{cumulus.WindRunUnitText}\"],");
			json.Append($"[\"Time of last strike\",\"{LightningTime}\",\"\"],");
			json.Append($"[\"Number of strikes today\",\"{LightningStrikesToday}\",\"\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"{cumulus.LeafCaptions[1]}\",\"{LeafTemp1.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.LeafCaptions[2]}\",\"{LeafTemp2.ToString(cumulus.TempFormat)}\",\"&deg;{cumulus.TempUnitText[1]}\"],");
			json.Append($"[\"{cumulus.LeafCaptions[3]}\",\"{LeafWetness1}\",\"&nbsp;\"],");
			json.Append($"[\"{cumulus.LeafCaptions[4]}\",\"{LeafWetness2}\",\"&nbsp;\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf4()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"{cumulus.LeafCaptions[1]}\",\"{LeafTemp1.ToString(cumulus.TempFormat)}&nbsp;&deg;{cumulus.TempUnitText[1]}\",\"{LeafWetness1}\"],");
			json.Append($"[\"{cumulus.LeafCaptions[2]}\",\"{LeafTemp2.ToString(cumulus.TempFormat)}&nbsp;&deg;{cumulus.TempUnitText[1]}\",\"{LeafWetness2}\"],");
			json.Append($"[\"{cumulus.LeafCaptions[3]}\",\"{LeafTemp3.ToString(cumulus.TempFormat)}&nbsp;&deg;{cumulus.TempUnitText[1]}\",\"{LeafWetness3}\"],");
			json.Append($"[\"{cumulus.LeafCaptions[4]}\",\"{LeafTemp4.ToString(cumulus.TempFormat)}&nbsp;&deg;{cumulus.TempUnitText[1]}\",\"{LeafWetness4}\"]");
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
				json.Append($"[\"1 μm\",\"{cumulus.airLinkDataIn.pm1}\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataIn.pm2p5}\",\"{cumulus.airLinkDataIn.pm2p5_1hr}\",\"{cumulus.airLinkDataIn.pm2p5_3hr}\",\"{cumulus.airLinkDataIn.pm2p5_24hr}\",\"{cumulus.airLinkDataIn.pm2p5_nowcast}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataIn.pm10}\",\"{cumulus.airLinkDataIn.pm10_1hr}\",\"{cumulus.airLinkDataIn.pm10_3hr}\",\"{cumulus.airLinkDataIn.pm10_24hr}\",\"{cumulus.airLinkDataIn.pm10_nowcast}\"]");
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
			var tempUnitStr = "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + sepStr;

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
			json.Append(HiLoToday.HighDewpoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.HighDewpointTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighDewpoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.HighDewpointTime.ToShortTimeString());
			json.Append(closeStr);

			json.Append("[\"Low Dew Point\",\"");
			json.Append(HiLoToday.LowDewpoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoToday.LowDewpointTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.LowDewpoint.ToString(cumulus.TempFormat));
			json.Append(tempUnitStr);
			json.Append(HiLoYest.LowDewpointTime.ToShortTimeString());
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
			var unitStr = "&nbsp;" + cumulus.RainUnitText;

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
			json.Append(HiLoToday.HighRain.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(HiLoToday.HighRainTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighRain.ToString(cumulus.RainFormat));
			json.Append(unitStr + "/hr");
			json.Append(sepStr);
			json.Append(HiLoYest.HighRainTime.ToShortTimeString());
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
			json.Append("&nbsp;" + cumulus.WindUnitText);
			json.Append(sepStr);
			json.Append(HiLoToday.HighGustTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighGust.ToString(cumulus.WindFormat));
			json.Append("&nbsp;" + cumulus.WindUnitText);
			json.Append(sepStr);
			json.Append(HiLoYest.HighGustTime.ToShortTimeString());
			json.Append("\"],");

			json.Append("[\"Highest Speed\",\"");
			json.Append(HiLoToday.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.WindUnitText);
			json.Append(sepStr);
			json.Append(HiLoToday.HighWindTime.ToShortTimeString());
			json.Append(sepStr);
			json.Append(HiLoYest.HighWind.ToString(cumulus.WindAvgFormat));
			json.Append("&nbsp;" + cumulus.WindUnitText);
			json.Append(sepStr);
			json.Append(HiLoYest.HighWindTime.ToShortTimeString());
			json.Append("\"],");

			json.Append("[\"Wind Run\",\"");
			json.Append(WindRunToday.ToString(cumulus.WindRunFormat));
			json.Append("&nbsp;" + cumulus.WindRunUnitText);
			json.Append(sepStr);
			json.Append("&nbsp;");
			json.Append(sepStr);
			json.Append(YesterdayWindRun.ToString(cumulus.WindRunFormat));
			json.Append("&nbsp;" + cumulus.WindRunUnitText);
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
			var unitStr = "&nbsp;" + cumulus.PressUnitText;

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
				var allLines = File.ReadAllLines(cumulus.DayFile);
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
					var fields = line.Split(Convert.ToChar(cumulus.ListSeparator));
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
				json.Remove(json.Length - 1, 1);
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

		// Fetchs all days in the required month that have a diary entry
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
					json.Append(result[i].Timestamp.ToString("yyy-MM-dd"));
					json.Append("\",");
				}
				json.Remove(json.Length - 1, 1);
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
		public string GetLogfile(string date, string draw, int start, int length, bool extra)
		{
			try
			{
				// date will (hopefully) be in format "m-yyyy" or "mm-yyyy"
				int month = Convert.ToInt32(date.Split('-')[0]);
				int year = Convert.ToInt32(date.Split('-')[1]);

				// Get a timestamp, use 15th day to avoid wrap issues
				var ts = new DateTime(year, month, 15);

				var logfile = (extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts));
				var numFields = (extra ? Cumulus.NumExtraLogFileFields : Cumulus.NumLogFileFields);


				var allLines = File.ReadAllLines(logfile);
				var total = allLines.Length;
				var lines = allLines.Skip(start).Take(length);


				//var total = File.ReadLines(logfile).Count();
				var json = new StringBuilder(220 * lines.Count());

				json.Append("{\"draw\":");
				json.Append(draw);
				json.Append(",\"recordsTotal\":");
				json.Append(total);
				json.Append(",\"recordsFiltered\":");
				json.Append(total);
				json.Append(",\"data\":[");

				//var lines = File.ReadLines(logfile).Skip(start).Take(length);

				var lineNum = start + 1; // Start is zero relative

				foreach (var line in lines)
				{
					var fields = line.Split(Convert.ToChar(cumulus.ListSeparator));
					json.Append($"[{lineNum++},");
					for (var i = 0; i < numFields; i++)
					{
						if (i < fields.Length)
						{
							// field exists
							json.Append("\"");
							json.Append(fields[i]);
							json.Append("\"");
						}
						else
						{
							// add padding
							json.Append("\" \"");
						}

						if (i < numFields - 1)
						{
							json.Append(",");
						}
					}
					json.Append("],");
				}

				// trim trailing ","
				json.Remove(json.Length - 1, 1);
				json.Append("]}");

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
			return $"{{\"temp\":\"{cumulus.TempUnitText[1]}\",\"wind\":\"{cumulus.WindUnitText}\",\"rain\":\"{cumulus.RainUnitText}\",\"press\":\"{cumulus.PressUnitText}\"}}";
		}

		public string GetGraphConfig()
		{
			var json = new StringBuilder(200);
			json.Append("{");
			json.Append($"\"temp\":{{\"units\":\"{cumulus.TempUnitText[1]}\",\"decimals\":{cumulus.TempDPlaces}}},");
			json.Append($"\"wind\":{{\"units\":\"{cumulus.WindUnitText}\",\"decimals\":{cumulus.WindAvgDPlaces},\"rununits\":\"{cumulus.WindRunUnitText}\"}},");
			json.Append($"\"rain\":{{\"units\":\"{cumulus.RainUnitText}\",\"decimals\":{cumulus.RainDPlaces}}},");
			json.Append($"\"press\":{{\"units\":\"{cumulus.PressUnitText}\",\"decimals\":{cumulus.PressDPlaces}}},");
			json.Append($"\"hum\":{{\"decimals\":{cumulus.HumDPlaces}}},");
			json.Append($"\"uv\":{{\"decimals\":{cumulus.UVDPlaces}}}");
			json.Append("}");
			return json.ToString();
		}

		public string GetDailyRainGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"dailyrain\":[", 10000);
			lock (RecentDailyDataList)
			{
				for (var i = 0; i < RecentDailyDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000},{RecentDailyDataList[i].rain.ToString(cumulus.RainFormat, InvC)}]");

					if (i < RecentDailyDataList.Count - 1)
						sb.Append(",");
				}
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetSunHoursGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{", 10000);
			if (cumulus.GraphOptions.SolarVisible)
			{
				sb.Append("\"sunhours\":[");
				lock (RecentDailyDataList)
				{
					for (var i = 0; i < RecentDailyDataList.Count; i++)
					{
						sb.Append($"[{DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000},{RecentDailyDataList[i].sunhours.ToString(cumulus.SunFormat, InvC)}]");

						if (i < RecentDailyDataList.Count - 1)
							sb.Append(",");
					}
				}
				sb.Append("]");
			}
			sb.Append("}");
			return sb.ToString();
		}

		public string GetDailyTempGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"mintemp\":[");
			lock (RecentDailyDataList)
			{
				for (var i = 0; i < RecentDailyDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000},{RecentDailyDataList[i].mintemp.ToString(cumulus.TempFormat, InvC)}]");
					if (i < RecentDailyDataList.Count - 1)
						sb.Append(",");
				}

				sb.Append("],\"maxtemp\":[");
				for (var i = 0; i < RecentDailyDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000},{RecentDailyDataList[i].maxtemp.ToString(cumulus.TempFormat, InvC)}]");
					if (i < RecentDailyDataList.Count - 1)
						sb.Append(",");
				}

				sb.Append("],\"avgtemp\":[");
				for (var i = 0; i < RecentDailyDataList.Count; i++)
				{
					sb.Append($"[{DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000},{RecentDailyDataList[i].avgtemp.ToString(cumulus.TempFormat, InvC)}]");
					if (i < RecentDailyDataList.Count - 1)
						sb.Append(",");
				}
			}
			sb.Append("]}");
			return sb.ToString();
		}

		public string GetAllDailyTempGraphData()
		{
			var InvC = new CultureInfo("");
			int linenum = 0;
			double valDbl;

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

			var watch = Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						string datestr = st[0];
						long recDate = DateTimeToUnix(ddmmyyStrToDate(datestr)) * 1000;

						// lo temp
						if (double.TryParse(st[4], out valDbl))
							minTemp.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
						else
							minTemp.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							minTemp.Append(",");
						// hi temp
						if (double.TryParse(st[6], out valDbl))
							maxTemp.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
						else
							maxTemp.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							maxTemp.Append(",");

						// avg temp
						if (st.Count > 15 && double.TryParse(st[15], out valDbl))
							avgTemp.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
						else
							avgTemp.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							avgTemp.Append(",");

						if (cumulus.GraphOptions.HIVisible)
						{
							// hi heat index
							if (st.Count > 18 && double.TryParse(st[25], out valDbl))
								heatIdx.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								heatIdx.Append($"[{recDate},null]");
							if (linenum < dayfile.Length)
								heatIdx.Append(",");
						}
						if (cumulus.GraphOptions.AppTempVisible)
						{
							// hi app temp
							if (st.Count > 18 && double.TryParse(st[27], out valDbl))
								maxApp.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								maxApp.Append($"[{recDate},null]");
							// lo app temp
							if (st.Count > 18 && double.TryParse(st[29], out valDbl))
								minApp.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								minApp.Append($"[{recDate},null]");
							if (linenum < dayfile.Length)
							{
								maxApp.Append(",");
								minApp.Append(",");
							}
						}
						// lo wind chill
						if (cumulus.GraphOptions.WCVisible)
						{
							if (st.Count > 18 && double.TryParse(st[33], out valDbl))
								windChill.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								windChill.Append($"[{recDate},null]");
							if (linenum < dayfile.Length)
								windChill.Append(",");
						}

						if (cumulus.GraphOptions.DPVisible)
						{
							// hi dewpt
							if (st.Count > 35 && double.TryParse(st[35], out valDbl))
								maxDew.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								maxDew.Append($"[{recDate},null]");
							// lo dewpt
							if (st.Count > 35 && double.TryParse(st[37], out valDbl))
								minDew.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								maxDew.Append($"[{recDate},null]");
							if (linenum < dayfile.Length)
							{
								maxDew.Append(",");
								minDew.Append(",");
							}
						}

						if (cumulus.GraphOptions.FeelsLikeVisible)
						{
							// hi feels like
							if (st.Count > 46 && double.TryParse(st[46], out valDbl))
								maxFeels.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								maxFeels.Append($"[{recDate},null]");
							// lo feels like
							if (st.Count > 46 && double.TryParse(st[48], out valDbl))
								minFeels.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								minFeels.Append($"[{recDate},null]");
							if (linenum < dayfile.Length)
							{
								maxFeels.Append(",");
								minFeels.Append(",");
							}
						}

						if (cumulus.GraphOptions.HumidexVisible)
						{
							// hi humidex
							if (st.Count > 50 && double.TryParse(st[50], out valDbl))
								humidex.Append($"[{recDate},{valDbl.ToString(cumulus.TempFormat, InvC)}]");
							else
								humidex.Append($"[{recDate},null]");
						}
						if (linenum < dayfile.Length)
							humidex.Append(",");
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("GetAllDailyTempGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
				}
			}
			sb.Append("\"minTemp\":" + minTemp.ToString() + "],");
			sb.Append("\"maxTemp\":" + maxTemp.ToString() + "],");
			sb.Append("\"avgTemp\":" + avgTemp.ToString() + "]");
			if (cumulus.GraphOptions.HIVisible)
				sb.Append(",\"heatIndex\":" + heatIdx.ToString() + "]");
			if (cumulus.GraphOptions.AppTempVisible)
			{
				sb.Append(",\"maxApp\":" + maxApp.ToString() + "]");
				sb.Append(",\"minApp\":" + minApp.ToString() + "]");
			}
			if (cumulus.GraphOptions.WCVisible)
				sb.Append(",\"windChill\":" + windChill.ToString() + "]");
			if (cumulus.GraphOptions.DPVisible)
			{
				sb.Append(",\"maxDew\":" + maxDew.ToString() + "]");
				sb.Append(",\"minDew\":" + minDew.ToString() + "]");
			}
			if (cumulus.GraphOptions.FeelsLikeVisible)
			{
				sb.Append(",\"maxFeels\":" + maxFeels.ToString() + "]");
				sb.Append(",\"minFeels\":" + minFeels.ToString() + "]");
			}
			if (cumulus.GraphOptions.HumidexVisible)
				sb.Append(",\"humidex\":" + humidex.ToString() + "]");
			sb.Append("}");

			watch.Stop();
			cumulus.LogDebugMessage($"GetAllDailyTempGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

			return sb.ToString();
		}

		public string GetAllDailyWindGraphData()
		{
			var InvC = new CultureInfo("");
			int linenum = 0;
			double valDbl;

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

			var watch = Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						long recDate = DateTimeToUnix(ddmmyyStrToDate(st[0])) * 1000;

						// hi gust
						if (double.TryParse(st[1], out valDbl))
							maxGust.Append($"[{recDate},{valDbl.ToString(cumulus.WindFormat, InvC)}]");
						else
							maxGust.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							maxGust.Append(",");

						if (st.Count > 15)
						{
							// hi wind run
							if (double.TryParse(st[16], out valDbl))
								windRun.Append($"[{recDate},{valDbl.ToString(cumulus.WindRunFormat, InvC)}]");
							else
								windRun.Append($"[{recDate},null]");
						}
						if (linenum < dayfile.Length)
							windRun.Append(",");

						if (st.Count > 17)
						{
							// hi wind
							if (double.TryParse(st[17], out valDbl))
								maxWind.Append($"[{recDate},{valDbl.ToString(cumulus.WindAvgFormat, InvC)}]");
							else
								maxWind.Append($"[{recDate},null]");
						}
						if (linenum < dayfile.Length)
							maxWind.Append(",");
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("GetAllDailyWindGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
				}
			}
			sb.Append("\"maxGust\":" + maxGust.ToString() + "],");
			sb.Append("\"windRun\":" + windRun.ToString() + "],");
			sb.Append("\"maxWind\":" + maxWind.ToString() + "]");
			sb.Append("}");

			watch.Stop();
			cumulus.LogDebugMessage($"GetAllDailyTempGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

			return sb.ToString();
		}

		public string GetAllDailyRainGraphData()
		{
			var InvC = new CultureInfo("");
			int linenum = 0;
			double valDbl;

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

			var watch = Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						long recDate = DateTimeToUnix(ddmmyyStrToDate(st[0])) * 1000;

						// hi rain rate
						if (double.TryParse(st[12], out valDbl))
							maxRRate.Append($"[{recDate},{valDbl.ToString(cumulus.RainFormat, InvC)}]");
						else
							maxRRate.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							maxRRate.Append(",");
						// total rain
						if (double.TryParse(st[14], out valDbl))
							rain.Append($"[{recDate},{valDbl.ToString(cumulus.RainFormat, InvC)}]");
						else
							rain.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							rain.Append(",");
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("GetAllDailyRainGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
				}
			}
			sb.Append("\"maxRainRate\":" + maxRRate.ToString() + "],");
			sb.Append("\"rain\":" + rain.ToString() + "]");
			sb.Append("}");

			watch.Stop();
			cumulus.LogDebugMessage($"GetAllDailyRainGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

			return sb.ToString();
		}

		public string GetAllDailyPressGraphData()
		{
			var InvC = new CultureInfo("");
			int linenum = 0;
			double valDbl;

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

			var watch = Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						long recDate = DateTimeToUnix(ddmmyyStrToDate(st[0])) * 1000;

						// lo baro
						if (double.TryParse(st[8], out valDbl))
							minBaro.Append($"[{recDate},{valDbl.ToString(cumulus.PressFormat, InvC)}]");
						else
							minBaro.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							minBaro.Append(",");
						// hi baro
						if (double.TryParse(st[10], out valDbl))
							maxBaro.Append($"[{recDate},{valDbl.ToString(cumulus.PressFormat, InvC)}]");
						else
							maxBaro.Append($"[{recDate},null]");
						if (linenum < dayfile.Length)
							maxBaro.Append(",");
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("GetAllDailyRainGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
				}
			}
			sb.Append("\"minBaro\":" + minBaro.ToString() + "],");
			sb.Append("\"maxBaro\":" + maxBaro.ToString() + "]");
			sb.Append("}");

			watch.Stop();
			cumulus.LogDebugMessage($"GetAllDailyRainGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

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
			int linenum = 0;
			int valInt;

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

			var watch = Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						long recDate = DateTimeToUnix(ddmmyyStrToDate(st[0])) * 1000;

						if (st.Count > 18)
						{
							// lo humidity
							if (int.TryParse(st[19], out valInt))
								minHum.Append($"[{recDate},{valInt}]");
							else
								minHum.Append($"[{recDate},null]");
							// hi humidity
							if (int.TryParse(st[21], out valInt))
								maxHum.Append($"[{recDate},{valInt}]");
							else
								maxHum.Append($"[{recDate},null]");
						}
						if (linenum < dayfile.Length)
						{
							minHum.Append(",");
							maxHum.Append(",");
						}
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("GetAllDailyHumGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
				}
			}
			sb.Append("\"minHum\":" + minHum.ToString() + "],");
			sb.Append("\"maxHum\":" + maxHum.ToString() + "]");
			sb.Append("}");

			watch.Stop();
			cumulus.LogDebugMessage($"GetAllDailyHumGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

			return sb.ToString();
		}

		public string GetAllDailySolarGraphData()
		{
			var InvC = new CultureInfo("");
			int linenum = 0;
			double valDbl;
			int valInt;

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

			var watch = Stopwatch.StartNew();

			// Read the dayfile and extract the records from there
			if (File.Exists(cumulus.DayFile))
			{
				try
				{
					var dayfile = File.ReadAllLines(cumulus.DayFile);

					foreach (var line in dayfile)
					{
						linenum++;
						List<string> st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

						if (st.Count <= 0) continue;

						long recDate = DateTimeToUnix(ddmmyyStrToDate(st[0])) * 1000;

						if (cumulus.GraphOptions.SunshineVisible)
						{

							// sunshine hours
							if (st.Count > 24 && double.TryParse(st[24], out valDbl))
								sunHours.Append($"[{recDate},{valDbl.ToString(InvC)}]");
							else
								sunHours.Append($"[{recDate},null]");
						}
						if (linenum < dayfile.Length)
							sunHours.Append(",");

						if (cumulus.GraphOptions.SolarVisible)
						{
							// hi solar rad
							if (st.Count > 42 && int.TryParse(st[42], out valInt))
								solarRad.Append($"[{recDate},{valInt}]");
							else
								solarRad.Append($"[{recDate},null]");
						}

						if (cumulus.GraphOptions.UVVisible)
						{
							// hi UV-I
							if (st.Count > 42 && double.TryParse(st[44], out valDbl))
								uvi.Append($"[{recDate},{valDbl.ToString(cumulus.UVFormat, InvC)}]");
							else
								uvi.Append($"[{recDate},null]");
						}
						if (linenum < dayfile.Length)
						{
							solarRad.Append(",");
							uvi.Append(",");
						}
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("GetAllDailySolarGraphData: Error on line " + linenum + " of " + cumulus.DayFile + ": " + e.Message);
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

			watch.Stop();
			cumulus.LogDebugMessage($"GetAllDailySolarGraphData: Dayfile parse = {watch.ElapsedMilliseconds} ms");

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
				HiLoToday.HighGustBearing, cumulus.WindUnitText, BearingRangeFrom10, BearingRangeTo10, windRoseData.ToString(), HiLoToday.HighTemp, HiLoToday.LowTemp,
				HiLoToday.HighTempTime.ToString("HH:mm"), HiLoToday.LowTempTime.ToString("HH:mm"), HiLoToday.HighPress, HiLoToday.LowPress, HiLoToday.HighPressTime.ToString("HH:mm"),
				HiLoToday.LowPressTime.ToString("HH:mm"), HiLoToday.HighRain, HiLoToday.HighRainTime.ToString("HH:mm"), HiLoToday.HighHumidity, HiLoToday.LowHumidity,
				HiLoToday.HighHumidityTime.ToString("HH:mm"), HiLoToday.LowHumidityTime.ToString("HH:mm"), cumulus.PressUnitText, cumulus.TempUnitText, cumulus.RainUnitText,
				HiLoToday.HighDewpoint, HiLoToday.LowDewpoint, HiLoToday.HighDewpointTime.ToString("HH:mm"), HiLoToday.LowDewpointTime.ToString("HH:mm"), HiLoToday.LowWindChill,
				HiLoToday.LowWindChillTime.ToString("HH:mm"), (int)SolarRad, (int)HiLoToday.HighSolar, HiLoToday.HighSolarTime.ToString("HH:mm"), UV, HiLoToday.HighUv,
				HiLoToday.HighUvTime.ToString("HH:mm"), forecaststr, getTimeString(cumulus.SunRiseTime), getTimeString(cumulus.SunSetTime),
				getTimeString(cumulus.MoonRiseTime), getTimeString(cumulus.MoonSetTime), HiLoToday.HighHeatIndex, HiLoToday.HighHeatIndexTime.ToString("HH:mm"), HiLoToday.HighAppTemp,
				HiLoToday.LowAppTemp, HiLoToday.HighAppTempTime.ToString("HH:mm"), HiLoToday.LowAppTempTime.ToString("HH:mm"), (int)Math.Round(CurrentSolarMax),
				alltimerecarray[AT_HighPress].value, alltimerecarray[AT_LowPress].value, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
				HiLoToday.HighHourlyRain, HiLoToday.HighHourlyRainTime.ToString("HH:mm"), "F" + cumulus.Beaufort(HiLoToday.HighWind), "F" + cumulus.Beaufort(WindAverage),
				cumulus.BeaufortDesc(WindAverage), LastDataReadTimestamp.ToString("HH:mm:ss"), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
				cumulus.LowTempAlarm.Triggered, cumulus.HighTempAlarm.Triggered, cumulus.TempChangeAlarm.UpTriggered, cumulus.TempChangeAlarm.DownTriggered, cumulus.HighRainTodayAlarm.Triggered, cumulus.HighRainRateAlarm.Triggered,
				cumulus.LowPressAlarm.Triggered, cumulus.HighPressAlarm.Triggered, cumulus.PressChangeAlarm.UpTriggered, cumulus.PressChangeAlarm.DownTriggered, cumulus.HighGustAlarm.Triggered, cumulus.HighWindAlarm.Triggered,
				cumulus.SensorAlarm.Triggered, cumulus.BatteryLowAlarm.Triggered, cumulus.SpikeAlarm.Triggered,
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
				if (gust > HighGustThisMonth)
				{
					HighGustThisMonth = gust;
					HighGustThisMonthTS = timestamp;
					WriteMonthIniFile();
				}
				if (gust > HighGustThisYear)
				{
					HighGustThisYear = gust;
					HighGustThisYearTS = timestamp;
					WriteYearIniFile();
				}
				// All time high gust?
				if (gust > alltimerecarray[AT_HighGust].value)
				{
					SetAlltime(AT_HighGust, gust, timestamp);
				}

				// check for monthly all time records (and set)
				CheckMonthlyAlltime(AT_HighGust, gust, true, timestamp);
			}
			return true;
		}

		public void UpdateAPRS()
		{
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
		/// input is in WindUnit units, convert to mph for APRS
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
		/// input is in PressUnit units, convert to tenths of mb for APRS
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
}
