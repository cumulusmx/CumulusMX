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
				"High feels like", "Low feels like"
			};

		//public DateTime lastArchiveTimeUTC;

		public string LatestFOReading { get; set; }

		//public int LastDailySummaryOADate;

		public Cumulus cumulus;

		private int lastMinute;
		private int lastHour;

		public bool[] WMR928ChannelPresent = new[] {false, false, false, false};
		public bool[] WMR928ExtraTempValueOnly = new[] {false, false, false, false};
		public double[] WMR928ExtraTempValues = new[] {0.0, 0.0, 0.0, 0.0};
		public double[] WMR928ExtraDPValues = new[] {0.0, 0.0, 0.0, 0.0};
		public int[] WMR928ExtraHumValues = new[] {0, 0, 0, 0};

		public const int AT_hightemp = 0;
		public const int AT_lowtemp = 1;
		public const int AT_highgust = 2;
		public const int AT_highwind = 3;
		public const int AT_lowchill = 4;
		public const int AT_highrainrate = 5;
		public const int AT_dailyrain = 6;
		public const int AT_hourlyrain = 7;
		public const int AT_lowpress = 8;
		public const int AT_highpress = 9;
		public const int AT_wetmonth = 10;
		public const int AT_highmintemp = 11;
		public const int AT_lowmaxtemp = 12;
		public const int AT_highhumidity = 13;
		public const int AT_lowhumidity = 14;
		public const int AT_highapptemp = 15;
		public const int AT_lowapptemp = 16;
		public const int AT_highheatindex = 17;
		public const int AT_highdewpoint = 18;
		public const int AT_lowdewpoint = 19;
		public const int AT_highwindrun = 20;
		public const int AT_longestdryperiod = 21;
		public const int AT_longestwetperiod = 22;
		public const int AT_highdailytemprange = 23;
		public const int AT_lowdailytemprange = 24;
		public const int AT_highfeelslike = 25;
		public const int AT_lowfeelslike = 26;

		public DateTime AlltimeRecordTimestamp { get; set; }

		//private ProgressWindow progressWindow;

		//public historyProgressWindow histprog;
		public BackgroundWorker bw;

		//public bool importingData = false;

		public bool calculaterainrate = false;

		protected List<int> buffer = new List<int>();

		public List<Last3HourData> Last3HourDataList = new List<Last3HourData>();
		public List<LastHourData> LastHourDataList = new List<LastHourData>();
		public List<GraphData> GraphDataList = new List<GraphData>();
		public List<Last10MinWind> Last10MinWindList = new List<Last10MinWind>();
		public List<RecentDailyData> RecentDailyDataList = new List<RecentDailyData>();

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

		// today highs and lows
		public double highgusttoday;
		public double highwindtoday = 0;
		public int highgustbearing = 0;
		public double HighTempToday = -500;
		public double LowTempToday = 999;
		public double TempRangeToday = 0;
		public double HighAppTempToday = -500;
		public double LowAppTempToday = 999;
		public double HighFeelsLikeToday = -500;
		public double LowFeelsLikeToday = 99;
		public double highpresstoday = 0;
		public double lowpresstoday = 9999;
		public double highraintoday = 0;
		public double highhourlyraintoday = 0;
		public int highhumiditytoday = 0;
		public int lowhumiditytoday = 100;
		public double HighHeatIndexToday = -500;
		public double LowWindChillToday = 999;
		public double HighDewpointToday = -500;
		public double LowDewpointToday = 999;
		public double HighSolarToday = 0;
		public double HighUVToday = 0;

		// yesterday highs and lows
		public double highgustyesterday;
		public double highwindyesterday = 0;
		public int highgustbearingyesterday = 0;
		public double highrainyesterday = 0;
		public double highpressyesterday = 0;
		public double lowpressyesterday = 9999;
		public double HighTempYesterday = -500;
		public double LowTempYesterday = 999;
		public int highhumidityyesterday = 0;
		public int lowhumidityyesterday = 100;
		public double HighHeatIndexYesterday = -500;
		public double HighAppTempYesterday = -500;
		public double LowAppTempYesterday = 900;
		public double HighFeelsLikeYesterday = -500;
		public double LowFeelsLikeYesterday = 900;
		public double highhourlyrainyesterday = 0;
		public double lowwindchillyesterday = 0;
		public double HighDewpointYesterday = -500;
		public double LowDewpointYesterday = 999;
		public double TempRangeYesterday = 0;
		public double HighSolarYesterday = 0;
		public double HighUVYesterday = 0;

		// today high and low times
		internal DateTime highgusttodaytime;
		public DateTime highwindtodaytime;
		public DateTime lowtemptodaytime;
		public DateTime hightemptodaytime;
		public DateTime lowhumiditytodaytime;
		public DateTime highhumiditytodaytime;
		public DateTime highheatindextodaytime;
		public DateTime highapptemptodaytime;
		public DateTime lowapptemptodaytime;
		public DateTime highfeelsliketodaytime;
		public DateTime lowfeelsliketodaytime;
		public DateTime highhourlyraintodaytime;
		public DateTime lowwindchilltodaytime;
		public DateTime HighDewpointTodayTime;
		public DateTime LowDewpointTodayTime;

		public DateTime highraintodaytime;
		public DateTime highpresstodaytime;
		public DateTime lowpresstodaytime;
		public DateTime highsolartodaytime;
		public DateTime highuvtodaytime;

		// yesterday high and low times
		public DateTime highgustyesterdaytime;
		public DateTime highwindyesterdaytime;
		public DateTime highrainyesterdaytime;
		public DateTime lowtempyesterdaytime;

		public DateTime hightempyesterdaytime;
		public DateTime highpressyesterdaytime;
		public DateTime lowpressyesterdaytime;
		public DateTime lowhumidityyesterdaytime;
		public DateTime highhumidityyesterdaytime;
		public DateTime highheatindexyesterdaytime;
		public DateTime highapptempyesterdaytime;
		public DateTime lowapptempyesterdaytime;
		public DateTime highfeelslikeyesterdaytime;
		public DateTime lowfeelslikeyesterdaytime;
		public DateTime highhourlyrainyesterdaytime;
		public DateTime lowwindchillyesterdaytime;
		public DateTime HighDewpointYesterdayTime;
		public DateTime LowDewpointYesterdayTime;
		public DateTime highsolaryesterdaytime;
		public DateTime highuvyesterdaytime;

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

		public Thread t;

		public Timer secondTimer;
		public double presstrendval;
		public double temptrendval;

		public bool timerStartNeeded = false;

		public SQLiteConnection RecentDataDb;
		// Extra sensors

		public double SolarElevation;
		private const int alltimerecbound = 26;

		public bool WindReadyToPlot = false;
		public bool TempReadyToPlot = false;
		private bool first_temp = true;
		public double RG11RainYesterday { get; set; }

		public abstract void portDataReceived(object sender, SerialDataReceivedEventArgs e);
		public abstract void Start();

		public WeatherStation(Cumulus cumulus)
		{
			OutdoorTemperature = 0.0;
			IndoorHumidity = 0;
			Pressure = 0.0;
			SunshineHours = 0.0F;
			WindRunToday = 0.0F;
			RainYear = 0.0F;
			RainMonth = 0.0F;
			RainToday = 0.0F;
			WindAverage = 0.0F;
			RecentMaxGust = 0.0F;
			IndoorTemperature = 0.0F;
			AvgBearingText = "---";
			BearingText = "---";
			Bearing = 0;
			AvgBearing = 0;
			WindLatest = 0.0F;
			OutdoorHumidity = 0;
			SolarRad = 0.0F;
			UV = 0.0F;
			WindChill = 0.0F;
			HeatIndex = 0.0F;
			Humidex = 0.0F;
			FeelsLike = 0.0F;
			RainRate = 0.0F;
			Forecast = "Forecast: ";
			// save the reference to the owner
			this.cumulus = cumulus;

			CumulusForecast = cumulus.ForecastNotAvailable;
			wsforecast = cumulus.ForecastNotAvailable;

			ExtraTemp = new double[11];
			ExtraHum = new double[11];
			ExtraDewPoint = new double[11];

			WindReadyToPlot = false;
			HaveReadData = false;

			nextwind = 0;
			nextwindvalue = 0;
			nextwindvec = 0;

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
				Raincounter = midnightraincount + (RainToday / cumulus.RainMult);
			}
			else
			{
				Raincounter = raindaystart + (RainToday / cumulus.RainMult);
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

			Console.WriteLine("Today.ini = " + cumulus.TodayIniFile);

			var todayfiledate = ini.GetValue("General", "Date", "00/00/00");
			var timestampstr = ini.GetValue("General", "Timestamp", DateTime.Now.ToString("s"));

			Console.WriteLine("Last update=" + timestampstr);

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
			if (cumulus.SyncFOReads)
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
			HighSolarToday = ini.GetValue("Solar", "HighSolarRad", 0.0);
			highsolartodaytime = ini.GetValue("Solar", "HighSolarRadTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HighUVToday = ini.GetValue("Solar", "HighUV", 0.0);
			highuvtodaytime = ini.GetValue("Solar", "HighUVTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			StartOfDaySunHourCounter = ini.GetValue("Solar", "SunStart", -999.0);
			RG11RainToday = ini.GetValue("Rain", "RG11Today", 0.0);

			// Wind
			highwindtoday = ini.GetValue("Wind", "Speed", 0.0);
			highwindtodaytime = ini.GetValue("Wind", "SpTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			highgusttoday = ini.GetValue("Wind", "Gust", 0.0);
			highgusttodaytime = ini.GetValue("Wind", "Time", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			highgustbearing = ini.GetValue("Wind", "Bearing", 0);
			WindRunToday = ini.GetValue("Wind", "Windrun", 0.0);
			DominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			DominantWindBearingMinutes = ini.GetValue("Wind", "DominantWindBearingMinutes", 0);
			DominantWindBearingX = ini.GetValue("Wind", "DominantWindBearingX", 0.0);
			DominantWindBearingY = ini.GetValue("Wind", "DominantWindBearingY", 0.0);
			// Temperature
			LowTempToday = ini.GetValue("Temp", "Low", 999.0);
			lowtemptodaytime = ini.GetValue("Temp", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			HighTempToday = ini.GetValue("Temp", "High", -999.0);
			hightemptodaytime = ini.GetValue("Temp", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			if ((HighTempToday > -400) && (LowTempToday < 400))
				TempRangeToday = HighTempToday - LowTempToday;
			else
				TempRangeToday = 0;
			TempTotalToday = ini.GetValue("Temp", "Total", 0.0);
			tempsamplestoday = ini.GetValue("Temp", "Samples", 1);
			HeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			CoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			// Pressure
			lowpresstoday = ini.GetValue("Pressure", "Low", 9999.0);
			lowpresstodaytime = ini.GetValue("Pressure", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			highpresstoday = ini.GetValue("Pressure", "High", 0.0);
			highpresstodaytime = ini.GetValue("Pressure", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// rain
			highraintoday = ini.GetValue("Rain", "High", 0.0);
			highraintodaytime = ini.GetValue("Rain", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			highhourlyraintoday = ini.GetValue("Rain", "HourlyHigh", 0.0);
			highhourlyraintodaytime = ini.GetValue("Rain", "HHourlyTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			raindaystart = ini.GetValue("Rain", "Start", -1.0);
			RainYesterday = ini.GetValue("Rain", "Yesterday", 0.0);
			if (raindaystart >= 0)
			{
				cumulus.LogMessage("ReadTodayfile: set notraininit false");
				notraininit = false;
			}
			// humidity
			lowhumiditytoday = ini.GetValue("Humidity", "Low", 100);
			highhumiditytoday = ini.GetValue("Humidity", "High", 0);
			lowhumiditytodaytime = ini.GetValue("Humidity", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			highhumiditytodaytime = ini.GetValue("Humidity", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Solar
			SunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			SunshineToMidnight = ini.GetValue("Solar", "SunshineHoursToMidnight", 0.0);
			// heat index
			HighHeatIndexToday = ini.GetValue("HeatIndex", "High", -999.0);
			highheatindextodaytime = ini.GetValue("HeatIndex", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Apparent temp
			HighAppTempToday = ini.GetValue("AppTemp", "High", -999.0);
			highapptemptodaytime = ini.GetValue("AppTemp", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			LowAppTempToday = ini.GetValue("AppTemp", "Low", 999.0);
			lowapptemptodaytime = ini.GetValue("AppTemp", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// wind chill
			LowWindChillToday = ini.GetValue("WindChill", "Low", 999.0);
			lowwindchilltodaytime = ini.GetValue("WindChill", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Dew point
			HighDewpointToday = ini.GetValue("Dewpoint", "High", -999.0);
			HighDewpointTodayTime = ini.GetValue("Dewpoint", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			LowDewpointToday = ini.GetValue("Dewpoint", "Low", 999.0);
			LowDewpointTodayTime = ini.GetValue("Dewpoint", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			// Feels like
			HighFeelsLikeToday = ini.GetValue("FeelsLike", "High", -999.0);
			highfeelsliketodaytime = ini.GetValue("FeelsLike", "HTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));
			LowFeelsLikeToday = ini.GetValue("FeelsLike", "Low", 999.0);
			lowfeelsliketodaytime = ini.GetValue("FeelsLike", "LTime", new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0));

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
				ini.SetValue("Wind", "Speed", highwindtoday);
				ini.SetValue("Wind", "SpTime", highwindtodaytime.ToString("HH:mm"));
				ini.SetValue("Wind", "Gust", highgusttoday);
				ini.SetValue("Wind", "Time", highgusttodaytime.ToString("HH:mm"));
				ini.SetValue("Wind", "Bearing", highgustbearing);
				ini.SetValue("Wind", "Direction", CompassPoint(highgustbearing));
				ini.SetValue("Wind", "Windrun", WindRunToday);
				ini.SetValue("Wind", "DominantWindBearing", DominantWindBearing);
				ini.SetValue("Wind", "DominantWindBearingMinutes", DominantWindBearingMinutes);
				ini.SetValue("Wind", "DominantWindBearingX", DominantWindBearingX);
				ini.SetValue("Wind", "DominantWindBearingY", DominantWindBearingY);
				// Temperature
				ini.SetValue("Temp", "Low", LowTempToday);
				ini.SetValue("Temp", "LTime", lowtemptodaytime.ToString("HH:mm"));
				ini.SetValue("Temp", "High", HighTempToday);
				ini.SetValue("Temp", "HTime", hightemptodaytime.ToString("HH:mm"));
				ini.SetValue("Temp", "Total", TempTotalToday);
				ini.SetValue("Temp", "Samples", tempsamplestoday);
				ini.SetValue("Temp", "ChillHours", ChillHours);
				ini.SetValue("Temp", "HeatingDegreeDays", HeatingDegreeDays);
				ini.SetValue("Temp", "CoolingDegreeDays", CoolingDegreeDays);
				// Pressure
				ini.SetValue("Pressure", "Low", lowpresstoday);
				ini.SetValue("Pressure", "LTime", lowpresstodaytime.ToString("HH:mm"));
				ini.SetValue("Pressure", "High", highpresstoday);
				ini.SetValue("Pressure", "HTime", highpresstodaytime.ToString("HH:mm"));
				// rain
				ini.SetValue("Rain", "High", highraintoday);
				ini.SetValue("Rain", "HTime", highraintodaytime.ToString("HH:mm"));
				ini.SetValue("Rain", "HourlyHigh", highhourlyraintoday);
				ini.SetValue("Rain", "HHourlyTime", highhourlyraintodaytime.ToString("HH:mm"));
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
				ini.SetValue("Humidity", "Low", lowhumiditytoday);
				ini.SetValue("Humidity", "High", highhumiditytoday);
				ini.SetValue("Humidity", "LTime", lowhumiditytodaytime.ToString("HH:mm"));
				ini.SetValue("Humidity", "HTime", highhumiditytodaytime.ToString("HH:mm"));
				// Solar
				ini.SetValue("Solar", "SunshineHours", SunshineHours);
				ini.SetValue("Solar", "SunshineHoursToMidnight", SunshineToMidnight);
				// heat index
				ini.SetValue("HeatIndex", "High", HighHeatIndexToday);
				ini.SetValue("HeatIndex", "HTime", highheatindextodaytime.ToString("HH:mm"));
				// App temp
				ini.SetValue("AppTemp", "Low", LowAppTempToday);
				ini.SetValue("AppTemp", "LTime", lowapptemptodaytime.ToString("HH:mm"));
				ini.SetValue("AppTemp", "High", HighAppTempToday);
				ini.SetValue("AppTemp", "HTime", highapptemptodaytime.ToString("HH:mm"));
				// Feels like
				ini.SetValue("FeelsLike", "Low", LowFeelsLikeToday);
				ini.SetValue("FeelsLike", "LTime", lowfeelsliketodaytime.ToString("HH:mm"));
				ini.SetValue("FeelsLike", "High", HighFeelsLikeToday);
				ini.SetValue("FeelsLike", "HTime", highfeelsliketodaytime.ToString("HH:mm"));
				// wind chill
				ini.SetValue("WindChill", "Low", LowWindChillToday);
				ini.SetValue("WindChill", "LTime", lowwindchilltodaytime.ToString("HH:mm"));
				// Dewpoint
				ini.SetValue("Dewpoint", "Low", LowDewpointToday);
				ini.SetValue("Dewpoint", "LTime", LowDewpointTodayTime.ToString("HH:mm"));
				ini.SetValue("Dewpoint", "High", HighDewpointToday);
				ini.SetValue("Dewpoint", "HTime", HighDewpointTodayTime.ToString("HH:mm"));

				// NOAA report names
				ini.SetValue("NOAA", "LatestMonthlyReport", cumulus.NOAALatestMonthlyReport);
				ini.SetValue("NOAA", "LatestYearlyReport", cumulus.NOAALatestYearlyReport);

				// Solar
				ini.SetValue("Solar", "HighSolarRad", HighSolarToday);
				ini.SetValue("Solar", "HighSolarRadTime", highsolartodaytime.ToString("HH:mm"));
				ini.SetValue("Solar", "HighUV", HighUVToday);
				ini.SetValue("Solar", "HighUVTime", highuvtodaytime.ToString("HH:mm"));
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
		public double IndoorTemperature { get; set; }

		/// <summary>
		/// Solar Radiation in W/m2
		/// </summary>
		public double SolarRad { get; set; }

		/// <summary>
		/// UV index
		/// </summary>
		public double UV { get; set; }

		public void UpdatePressureTrendString()
		{
			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.PressUnit)
			{
				case 0:
				case 1:
					threeHourlyPressureChangeMb = presstrendval*3;
					break;
				case 2:
					threeHourlyPressureChangeMb = presstrendval*3/0.0295333727;
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
					if ((index == AT_wetmonth) || (index == AT_dailyrain))
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
		public int IndoorHumidity { get; set; }

		/// <summary>
		/// Sea-level pressure
		/// </summary>
		public double Pressure { get; set; }

		public double StationPressure { get; set; }

		public string Forecast { get; set; }

		/// <summary>
		/// Outdoor temp
		/// </summary>
		public double OutdoorTemperature { get; set; }

		/// <summary>
		/// Outdoor dew point
		/// </summary>
		public double OutdoorDewpoint { get; set; }

		/// <summary>
		/// Wind chill
		/// </summary>
		public double WindChill { get; set; }

		/// <summary>
		/// Outdoor relative humidity in %
		/// </summary>
		public int OutdoorHumidity { get; set; }

		/// <summary>
		/// Apparent temperature
		/// </summary>
		public double ApparentTemperature { get; set; }

		/// <summary>
		/// Heat index
		/// </summary>
		public double HeatIndex { get; set; }

		/// <summary>
		/// Humidex
		/// </summary>
		public double Humidex { get; set; }

		/// <summary>
		/// Feels like (JAG/TI)
		/// </summary>
		public double FeelsLike { get; set; }


		/// <summary>
		/// Latest wind speed/gust
		/// </summary>
		public double WindLatest { get; set; }

		/// <summary>
		/// Average wind speed
		/// </summary>
		public double WindAverage { get; set; }

		/// <summary>
		/// Peak wind gust in last 10 minutes
		/// </summary>
		public double RecentMaxGust { get; set; }

		/// <summary>
		/// Wind direction in degrees
		/// </summary>
		public int Bearing { get; set; }

		/// <summary>
		/// Wind direction as compass points
		/// </summary>
		public string BearingText { get; set; }

		/// <summary>
		/// Wind direction in degrees
		/// </summary>
		public int AvgBearing { get; set; }

		/// <summary>
		/// Wind direction as compass points
		/// </summary>
		public string AvgBearingText { get; set; }

		/// <summary>
		/// Rainfall today
		/// </summary>
		public double RainToday { get; set; }

		/// <summary>
		/// Rain this month
		/// </summary>
		public double RainMonth { get; set; }

		/// <summary>
		/// Rain this year
		/// </summary>
		public double RainYear { get; set; }

		/// <summary>
		/// Current rain rate
		/// </summary>
		public double RainRate { get; set; }

		public double ET { get; set; }

		public double LightValue { get; set; }

		public double HeatingDegreeDays { get; set; }

		public double CoolingDegreeDays { get; set; }

		public int tempsamplestoday { get; set; }

		public double TempTotalToday { get; set; }

		public double ChillHours { get; set; }

		public double midnightraincount { get; set; }

		public int MidnightRainResetDay { get; set; }

		public void UpdateDegreeDays(int interval)
		{
			if (OutdoorTemperature < cumulus.NOAAheatingthreshold)
			{
				HeatingDegreeDays += (((cumulus.NOAAheatingthreshold - OutdoorTemperature)*interval)/1440);
			}
			if (OutdoorTemperature > cumulus.NOAAcoolingthreshold)
			{
				CoolingDegreeDays += (((OutdoorTemperature - cumulus.NOAAcoolingthreshold)*interval)/1440);
			}
		}

		/// <summary>
		/// Wind run for today
		/// </summary>
		public double WindRunToday { get; set; }

		/// <summary>
		/// Extra Temps
		/// </summary>
		public double[] ExtraTemp { get; set; }

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

		public double SunshineHours { get; set; }

		public double YestSunshineHours { get; set; }

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
			secondTimer = new Timer(1000);
			secondTimer.Elapsed += SecondTimer;
			secondTimer.Start();
		}

		public void StopMinuteTimer()
		{
			if (secondTimer != null) secondTimer.Stop();
		}

		public void SecondTimer(object sender, ElapsedEventArgs e)
		{
			var minute = DateTime.Now.Minute;

			if (minute != lastMinute)
			{
				lastMinute = minute;

				if ((minute % 10) == 0)
				{
					TenMinuteChanged();
				}

				var hour = DateTime.Now.Hour;

				if (hour != lastHour)
				{
					lastHour = hour;

					HourChanged(hour);
				}

				MinuteChanged();
			}

			if (DateTime.Now.Second%3 == 0)
			{
				// send current data to websocket every 3 seconds for now
				try
				{
					String windRoseData = (windcounts[0] * cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture);

					for (var i = 1; i < cumulus.NumWindRosePoints; i++)
					{
						windRoseData = windRoseData + "," + (windcounts[i] * cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture);
					}

					string stormRainStart = StartOfStorm == DateTime.MinValue ? "-----" : StartOfStorm.ToString("d");

					var data = new DataStruct(cumulus, OutdoorTemperature, OutdoorHumidity, TempTotalToday / tempsamplestoday, IndoorTemperature, OutdoorDewpoint, WindChill, IndoorHumidity,
						Pressure, WindLatest, WindAverage, RecentMaxGust, WindRunToday, Bearing, AvgBearing, RainToday, RainYesterday, RainMonth, RainYear, RainRate,
						RainLastHour, HeatIndex, Humidex, ApparentTemperature, temptrendval, presstrendval, highgusttoday, highgusttodaytime.ToString("HH:mm"), highwindtoday,
						highgustbearing, cumulus.WindUnitText, BearingRangeFrom10, BearingRangeTo10, windRoseData, HighTempToday, LowTempToday,
						hightemptodaytime.ToString("HH:mm"), lowtemptodaytime.ToString("HH:mm"), highpresstoday, lowpresstoday, highpresstodaytime.ToString("HH:mm"),
						lowpresstodaytime.ToString("HH:mm"), highraintoday, highraintodaytime.ToString("HH:mm"), highhumiditytoday, lowhumiditytoday,
						highhumiditytodaytime.ToString("HH:mm"), lowhumiditytodaytime.ToString("HH:mm"), cumulus.PressUnitText, cumulus.TempUnitText, cumulus.RainUnitText,
						HighDewpointToday, LowDewpointToday, HighDewpointTodayTime.ToString("HH:mm"), LowDewpointTodayTime.ToString("HH:mm"), LowWindChillToday,
						lowwindchilltodaytime.ToString("HH:mm"), (int)SolarRad, (int)HighSolarToday, highsolartodaytime.ToString("HH:mm"), UV, HighUVToday,
						highuvtodaytime.ToString("HH:mm"), forecaststr, getTimeString(cumulus.SunRiseTime), getTimeString(cumulus.SunSetTime),
						getTimeString(cumulus.MoonRiseTime), getTimeString(cumulus.MoonSetTime), HighHeatIndexToday, highheatindextodaytime.ToString("HH:mm"), HighAppTempToday,
						LowAppTempToday, highapptemptodaytime.ToString("HH:mm"), lowapptemptodaytime.ToString("HH:mm"), (int)CurrentSolarMax,
						alltimerecarray[AT_highpress].value, alltimerecarray[AT_lowpress].value, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
						highhourlyraintoday, highhourlyraintodaytime.ToString("HH:mm"), "F" + cumulus.Beaufort(highwindtoday), "F" + cumulus.Beaufort(WindAverage), cumulus.BeaufortDesc(WindAverage),
						LastDataReadTimestamp.ToString("HH:mm:ss"), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
						cumulus.LowTempAlarmState, cumulus.HighTempAlarmState, cumulus.TempChangeUpAlarmState, cumulus.TempChangeDownAlarmState, cumulus.HighRainTodayAlarmState, cumulus.HighRainRateAlarmState,
						cumulus.LowPressAlarmState, cumulus.HighPressAlarmState, cumulus.PressChangeUpAlarmState, cumulus.PressChangeDownAlarmState, cumulus.HighGustAlarmState, cumulus.HighWindAlarmState,
						cumulus.SensorAlarmState, cumulus.BatteryLowAlarmState, FeelsLike, HighFeelsLikeToday, highfeelsliketodaytime.ToString("HH:mm"), LowFeelsLikeToday, lowfeelsliketodaytime.ToString("HH:mm"));

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

		private void HourChanged(int hour)
		{
			cumulus.LogMessage("Hour changed:" + hour);
			cumulus.DoSunriseAndSunset();
			cumulus.DoMoonImage();

			if (cumulus.HourlyForecast)
			{
				DoForecast("", true);
			}

			if (hour == 0)
			{
				ResetMidnightRain(DateTime.Now);
			}

			int rollHour = Math.Abs(cumulus.GetHourInc());

			if (hour == rollHour)
			{
				DayReset(DateTime.Now);
			}

			if (hour == 0)
			{
				ResetSunshineHours();
			}

			RemoveOldRecentData(DateTime.Now);
		}

		private void RemoveOldRecentData(DateTime ts)
		{
			var deleteTime = ts.AddDays(-7);

			RecentDataDb.Execute("delete from RecentData where Timestamp < ?", deleteTime);
		}

		private void MinuteChanged()
		{
			DateTime now = DateTime.Now;

			CheckForDataStopped();

			if (!DataStopped)
			{
				CurrentSolarMax = AstroLib.SolarMax(now, cumulus.Longitude, cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.RStransfactor, cumulus.BrasTurbidity);
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

					while (weatherDataCollection[0].DT < DateTime.Now.AddHours(-1))
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
					AddLast3HourDataEntry(DateTime.Now, Pressure, OutdoorTemperature);
					RemoveOldL3HData(now);
					AddGraphDataEntry(DateTime.Now, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
						IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV, FeelsLike);
					RemoveOldGraphData(now);
					DoTrendValues(DateTime.Now);
					AddRecentDataEntry(now, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity,
						Pressure, RainToday, SolarRad, UV, Raincounter);

					if (now.Minute % cumulus.logints[cumulus.DataLogInterval] == 0)
					{
						cumulus.DoLogFile(now, true);

						if (cumulus.LogExtraSensors)
						{
							cumulus.DoExtraLogFile(now);
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

					if (!String.IsNullOrEmpty(cumulus.WundID) && (cumulus.WundID != " ") && cumulus.WundEnabled && cumulus.SynchronisedWUUpdate &&
						(now.Minute % cumulus.WundInterval == 0))
					{
						cumulus.UpdateWunderground(now);
					}

					if (!String.IsNullOrEmpty(cumulus.WindyApiKey) && (cumulus.WindyApiKey != " ") && cumulus.WindyEnabled && cumulus.SynchronisedWindyUpdate &&
						(now.Minute % cumulus.WindyInterval == 0))
					{
						cumulus.UpdateWindy(now);
					}

					if (!String.IsNullOrEmpty(cumulus.AwekasUser) && (cumulus.AwekasUser != " ") && cumulus.AwekasEnabled && cumulus.SynchronisedAwekasUpdate &&
						(now.Minute % cumulus.AwekasInterval == 0))
					{
						cumulus.UpdateAwekas(now);
					}

					if (!String.IsNullOrEmpty(cumulus.WCloudWid) && (cumulus.WCloudWid != " ") && cumulus.WCloudEnabled && cumulus.SynchronisedWCloudUpdate &&
						(now.Minute % cumulus.WCloudInterval == 0))
					{
						cumulus.UpdateWCloud(now);
					}

					if (!String.IsNullOrEmpty(cumulus.PWSID) && (cumulus.PWSID != " ") && (cumulus.PWSPW != " ") && (cumulus.PWSPW != "") && cumulus.PWSEnabled &&
						cumulus.SynchronisedPWSUpdate && (now.Minute % cumulus.PWSInterval == 0))
					{
						cumulus.UpdatePWSweather(now);
					}

					if (!String.IsNullOrEmpty(cumulus.WOWID) && (cumulus.WOWID != " ") && (cumulus.WOWPW != " ") && (cumulus.WOWPW != "") && cumulus.WOWEnabled &&
						cumulus.SynchronisedWOWUpdate && (now.Minute % cumulus.WOWInterval == 0))
					{
						cumulus.UpdateWOW(now);
					}

					if (!String.IsNullOrEmpty(cumulus.WeatherbugID) && (cumulus.WeatherbugID != " ") && (cumulus.WeatherbugPW != " ") && (cumulus.WeatherbugPW != "") &&
						cumulus.WeatherbugEnabled && cumulus.SynchronisedWBUpdate && (now.Minute % cumulus.WeatherbugInterval == 0))
					{
						cumulus.UpdateWeatherbug(now);
					}

					if (!String.IsNullOrEmpty(cumulus.APRSID) && cumulus.APRSenabled && cumulus.SynchronisedAPRSUpdate && (now.Minute % cumulus.APRSinterval == 0))
					{
						UpdateAPRS();
					}

					if (!String.IsNullOrEmpty(cumulus.Twitteruser) && (cumulus.Twitteruser != " ") && (cumulus.TwitterPW != " ") && (cumulus.TwitterPW != "") && cumulus.TwitterEnabled &&
						cumulus.SynchronisedTwitterUpdate && (now.Minute % cumulus.TwitterInterval == 0))
					{
						cumulus.UpdateTwitter();
					}

					if (cumulus.xapEnabled)
					{
						Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
						IPEndPoint iep1 = new IPEndPoint(IPAddress.Broadcast, cumulus.xapPort);

						byte[] data = Encoding.ASCII.GetBytes(cumulus.xapHeartbeat);

						sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
						sock.SendTo(data, iep1);

						var timeUTC = DateTime.Now.ToUniversalTime().ToString("HH:mm");
						var dateISO = DateTime.Now.ToUniversalTime().ToString("yyyyMMdd");

						var xapReport = "xap-header\n{\nv=12\nhop=1\nuid=FF" + cumulus.xapUID + "00\nclass=weather.report\nsource=" + cumulus.xapsource + "\n}\n";
						xapReport += "weather.report\n{\nUTC=" + timeUTC + "\nDATE=" + dateISO + "\nWindM=" + ConvertUserWindToMPH(WindAverage).ToString("F1") + "\nWindK=" +
									 ConvertUserWindToKPH(WindAverage).ToString("F1") + "\nWindGustsM=" + ConvertUserWindToMPH(RecentMaxGust).ToString("F1") + "\nWindGustsK=" +
									 ConvertUserWindToKPH(RecentMaxGust).ToString("F1") + "\nWindDirD=" + Bearing + "\nWindDirC=" + AvgBearing + "\nTempC=" +
									 ConvertUserTempToC(OutdoorTemperature).ToString("F1") + "\nTempF=" + ConvertUserTempToF(OutdoorTemperature).ToString("F1") + "\nDewC=" +
									 ConvertUserTempToC(OutdoorDewpoint).ToString("F1") + "\nDewF=" + ConvertUserTempToF(OutdoorDewpoint).ToString("F1") + "\nAirPressure=" +
									 ConvertUserPressToMB(Pressure).ToString("F1") + "\nRain=" + ConvertUserRainToMM(RainToday).ToString("F1") + "\n}";

						data = Encoding.ASCII.GetBytes(xapReport);

						sock.SendTo(data, iep1);

						sock.Close();
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
		}

		private void TenMinuteChanged()
		{
			cumulus.DoMoonPhase();
			cumulus.MoonAge = MoonriseMoonset.MoonAge();
		}

		private void CheckForDataStopped()
		{
			// Check whether we have read data since the last clock minute.
			if ((LastDataReadTimestamp != DateTime.MinValue) && (LastDataReadTimestamp == SavedLastDataReadTimestamp) && (LastDataReadTimestamp < DateTime.Now))
			{
				// Data input appears to have has stopped
				DataStopped = true;
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
			return (long) timeSpan.TotalSeconds;
		}

		private long DateTimeToJS(DateTime timestamp)
		{
			var timeSpan = (timestamp - new DateTime(1970, 1, 1, 0, 0, 0));
			return (long) timeSpan.TotalSeconds*1000;
		}

		public void CreateGraphDataFiles()
		{
			// Chart data for Highcharts graphs
			// config
			var json = GetGraphConfig();
			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "graphconfig.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			// Temperature
			json = GetTempGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "tempdata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// temperature for nvd3 charts
			json = GetTempGraphDataD3();

			using (var file = new StreamWriter("tempdatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Pressure
			json = GetPressGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "pressdata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// Pressure for nvd3 charts
			json = GetPressGraphDataD3();

			using (var file = new StreamWriter("pressdatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Wind
			json = GetWindGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "winddata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// Wind for nvd3 charts
			json = GetWindGraphDataD3();

			using (var file = new StreamWriter("winddatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Wind direction
			json = GetWindDirGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "wdirdata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// Wind direction for nvd3 charts
			json = GetWindDirGraphDataD3();

			using (var file = new StreamWriter("wdirdatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Humidity
			json = GetHumGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "humdata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// Humidity for nvd3 charts
			json = GetHumGraphDataD3();

			using (var file = new StreamWriter("humdatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Rain
			json = GetRainGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "raindata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// Rain for nvd3 charts
			json = GetRainGraphDataD3();

			using (var file = new StreamWriter("raindatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Solar
			json = GetSolarGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "solardata.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			/*// Solar for nvd3 charts
			json = GetSolarGraphDataD3();

			using (var file = new StreamWriter("solardatad3.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}*/

			// Daily rain
			json = GetDailyRainGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "dailyrain.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			// Sun hours
			json = GetSunHoursGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "sunhours.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}

			// Daily temp
			json = GetDailyTempGraphData();

			using (var file = new StreamWriter("web" + cumulus.DirectorySeparator + "dailytemp.json", false))
			{
				file.WriteLine(json);
				file.Close();
			}
		}

		public string GetSolarGraphDataD3()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("[{\"key\":\"UV\",\"type\":\"line\",\"yAxis\":2,\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].uvindex.ToString(cumulus.UVFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Solar radiation\",\"type\":\"area\",\"yAxis\":1,\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + (int) GraphDataList[i].solarrad + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Theoretical solar max\",\"type\":\"line\",\"yAxis\":1,\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + (int) GraphDataList[i].solarmax + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetSolarGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"UV\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].uvindex.ToString(cumulus.UVFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"SolarRad\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + (int) GraphDataList[i].solarrad + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"CurrentSolarMax\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + (int) GraphDataList[i].solarmax + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetRainGraphDataD3()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("[{\"key\":\"Rain today\",\"area\":\"true\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].RainToday.ToString(cumulus.RainFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Rain rate\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].rainrate.ToString(cumulus.RainFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetRainGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"rfall\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].RainToday.ToString(cumulus.RainFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"rrate\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].rainrate.ToString(cumulus.RainFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetHumGraphDataD3()
		{
			var sb = new StringBuilder("[{\"key\":\"Outdoor humidity\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].humidity + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Indoor humidity\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].inhumidity + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetHumGraphData()
		{
			var sb = new StringBuilder("{\"hum\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].humidity + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"inhum\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].inhumidity + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetWindDirGraphDataD3()
		{
			var sb = new StringBuilder("[{\"key\":\"Direction\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].winddir + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Average direction\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].avgwinddir + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetWindDirGraphData()
		{
			var sb = new StringBuilder("{\"bearing\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].winddir + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"avgbearing\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp) * 1000 + "," + GraphDataList[i].avgwinddir + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetWindGraphDataD3()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("[{\"key\":\"Wind gust\",\"values\":[");

			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].windgust.ToString(cumulus.WindFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Wind speed\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].windspeed.ToString(cumulus.WindFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetWindGraphData()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("{\"wgust\":[");

			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].windgust.ToString(cumulus.WindFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"wspeed\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].windspeed.ToString(cumulus.WindFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetPressGraphDataD3()
		{
			var InvC = new CultureInfo("");
			var sb = new StringBuilder("[{\"key\":\"Pressure\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].pressure.ToString(cumulus.PressFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetPressGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"press\":[");

			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].pressure.ToString(cumulus.PressFormat, InvC) + "]");

				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetTempGraphDataD3()
		{
			var InvC = new CultureInfo("");
			//var json = "[{\"key\":\"Dew point\",\"values\":[";
			StringBuilder sb = new StringBuilder("[{\"key\":\"Dew point\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				//json += "[" + DateTimeToJS(Last24HourDataList[i].timestamp) + "," + Last24HourDataList[i].dewpoint.ToString(cumulus.TempFormat) + "]";
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].dewpoint.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
				{
					sb.Append(",");
				}
				//json += ",";
			}

			sb.Append("]},{\"key\":\"Apparent temp\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].apptemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Feels like\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].feelslike.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Indoor\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].insidetemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Wind chill\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].windchill.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]},{\"key\":\"Temperature\",\"values\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToJS(GraphDataList[i].timestamp) + "," + GraphDataList[i].temperature.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}]");
			return sb.ToString();
		}

		public string GetTempGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"intemp\":[");

			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].insidetemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"dew\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].dewpoint.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"apptemp\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].apptemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"feelslike\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp) * 1000 + "," + GraphDataList[i].feelslike.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"wchill\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].windchill.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"heatindex\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp) * 1000 + "," + GraphDataList[i].heatindex.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"temp\":[");
			for (var i = 0; i < GraphDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(GraphDataList[i].timestamp)*1000 + "," + GraphDataList[i].temperature.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < GraphDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public void AddRecentDataEntry(DateTime timestamp, double windAverage, double recentMaxGust, double windLatest, int bearing, int avgBearing, double outsidetemp,
			double windChill, double dewpoint, double heatIndex, int humidity, double pressure, double rainToday, double solarRad, double uv, double rainCounter)
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
					raincounter = rainCounter
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
			int in100rainlasthour = Convert.ToInt32(ConvertUserRainToIn(RainLastHour)*100);
			int in100rainlast24hours = Convert.ToInt32(ConvertUserRainToIn(RainLast24Hour)*100);
			int in100raintoday;
			if (cumulus.RolloverHour == 0)
				// use today's rain for safety
				in100raintoday = Convert.ToInt32(ConvertUserRainToIn(RainToday)*100);
			else
				// 0900 day, use midnight calculation
				in100raintoday = Convert.ToInt32(ConvertUserRainToIn(RainSinceMidnight)*100);
			int mb10press = Convert.ToInt32(ConvertUserPressToMB(AltimeterPressure)*10);
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

			if (cumulus.SendSRToAPRS)
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
				num = Convert.ToInt32(((temp*1.8) + 32));
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
				return value/25.4;
			}
		}

		public double ConvertUserWindToMPH(double value)
		{
			switch (cumulus.WindUnit)
			{
				case 0:
					return value*2.23693629;
				case 1:
					return value;
				case 2:
					return value*0.621371;
				case 3:
					return value*1.15077945;
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
			cumulus.StartTimers();
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
			IndoorTemperature = temp + cumulus.InTempoffset;
			HaveReadData = true;
		}

		public void DoOutdoorHumidity(int humpar, DateTime timestamp)
		{
			if ((humpar == 98) && cumulus.Humidity98Fix)
			{
				OutdoorHumidity = 100;
			}
			else
			{
				OutdoorHumidity = humpar;
			}

			// apply offset and multipliers and round. This is different to C1, which truncates. I'm not sure why C1 does that
			OutdoorHumidity = (int) Math.Round((OutdoorHumidity * OutdoorHumidity * cumulus.HumMult2) + (OutdoorHumidity * cumulus.HumMult) + cumulus.HumOffset);

			if (OutdoorHumidity < 0)
			{
				OutdoorHumidity = 0;
			}
			if (OutdoorHumidity > 100)
			{
				OutdoorHumidity = 100;
			}

			if (OutdoorHumidity > highhumiditytoday)
			{
				highhumiditytoday = OutdoorHumidity;
				highhumiditytodaytime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorHumidity < lowhumiditytoday)
			{
				lowhumiditytoday = OutdoorHumidity;
				lowhumiditytodaytime = timestamp;
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
			if (OutdoorHumidity > alltimerecarray[AT_highhumidity].value)
			{
				SetAlltime(AT_highhumidity, OutdoorHumidity, timestamp);
			}
			CheckMonthlyAlltime(AT_highhumidity, OutdoorHumidity, true, timestamp);
			if (OutdoorHumidity < alltimerecarray[AT_lowhumidity].value)
			{
				SetAlltime(AT_lowhumidity, OutdoorHumidity, timestamp);
			}
			CheckMonthlyAlltime(AT_lowhumidity, OutdoorHumidity, false, timestamp);
			HaveReadData = true;
		}

		public double CalibrateTemp(double temp)
		{
			return (temp * temp * cumulus.TempMult2) + (temp * cumulus.TempMult) + cumulus.TempOffset;
		}

		public void DoOutdoorTemp(double temp, DateTime timestamp)

		{
			// UpdateStatusPanel;
			// update global temp
			OutdoorTemperature = CalibrateTemp(temp);

			double tempinF = ConvertUserTempToF(OutdoorTemperature);

			first_temp = false;

			// Does this reading set any records or trigger any alarms?
			if (OutdoorTemperature > alltimerecarray[AT_hightemp].value)
				SetAlltime(AT_hightemp, OutdoorTemperature, timestamp);

			DoAlarm(OutdoorTemperature, cumulus.HighTempAlarmValue, cumulus.HighTempAlarmEnabled, true, ref cumulus.HighTempAlarmState);

			if (OutdoorTemperature < alltimerecarray[AT_lowtemp].value)
				SetAlltime(AT_lowtemp, OutdoorTemperature, timestamp);

			DoAlarm(OutdoorTemperature, cumulus.LowTempAlarmValue, cumulus.LowTempAlarmEnabled, false, ref cumulus.LowTempAlarmState);

			CheckMonthlyAlltime(AT_hightemp, OutdoorTemperature, true, timestamp);
			CheckMonthlyAlltime(AT_lowtemp, OutdoorTemperature, false, timestamp);

			if (OutdoorTemperature > HighTempToday)
			{
				HighTempToday = OutdoorTemperature;
				hightemptodaytime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (OutdoorTemperature < LowTempToday)
			{
				LowTempToday = OutdoorTemperature;
				lowtemptodaytime = timestamp;
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
			TempRangeToday = HighTempToday - LowTempToday;

			double tempinC;

			if ((cumulus.CalculatedDP || cumulus.DavisStation) && (OutdoorHumidity != 0) && (!cumulus.FineOffsetStation))
			{
				// Calculate DewPoint.
				tempinC = ConvertUserTempToC(OutdoorTemperature);
				// dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
				OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(tempinC, OutdoorHumidity));

				CheckForDewpointHighLow(timestamp);
			}

			Humidex = ConvertTempCToUser(MeteoLib.Humidex(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity));

			// Calculate cloudbase
			if (cumulus.CloudBaseInFeet)
			{
				CloudBase = (int) Math.Floor(((tempinF - ConvertUserTempToF(OutdoorDewpoint))/4.4)*1000);
				if (CloudBase < 0)
					CloudBase = 0;
			}
			else
			{
				CloudBase = (int) Math.Floor((((tempinF - ConvertUserTempToF(OutdoorDewpoint))/4.4)*1000)/3.2808399);
				if (CloudBase < 0)
					CloudBase = 0;
			}

			tempinC = ConvertUserTempToC(OutdoorTemperature);
			HeatIndex = ConvertTempCToUser(MeteoLib.HeatIndex(tempinC, OutdoorHumidity));

			if (HeatIndex > HighHeatIndexToday)
			{
				HighHeatIndexToday = HeatIndex;
				highheatindextodaytime = timestamp;
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

			if (HeatIndex > alltimerecarray[AT_highheatindex].value)
				SetAlltime(AT_highheatindex, HeatIndex, timestamp);

			CheckMonthlyAlltime(AT_highheatindex, HeatIndex, true, timestamp);

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

			// don't try to calculate windchill if we haven't yet had wind and temp readings
			//if (TempReadyToPlot && WindReadyToPlot)
			//{
				ApparentTemperature =
				ConvertTempCToUser(ConvertUserTempToC(OutdoorTemperature) + (0.33 * MeteoLib.ActualVapourPressure(ConvertUserTempToC(OutdoorTemperature), OutdoorHumidity)) -
								   (0.7 * ConvertUserWindToMS(WindAverage)) - 4);

				if (ApparentTemperature > HighAppTempToday)
				{
					HighAppTempToday = ApparentTemperature;
					highapptemptodaytime = timestamp;
					WriteTodayFile(timestamp, false);
				}

				if (ApparentTemperature < LowAppTempToday)
				{
					LowAppTempToday = ApparentTemperature;
					lowapptemptodaytime = timestamp;
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

				if (ApparentTemperature > alltimerecarray[AT_highapptemp].value)
					SetAlltime(AT_highapptemp, ApparentTemperature, timestamp);

				if (ApparentTemperature < alltimerecarray[AT_lowapptemp].value)
					SetAlltime(AT_lowapptemp, ApparentTemperature, timestamp);

				CheckMonthlyAlltime(AT_highapptemp, ApparentTemperature, true, timestamp);
				CheckMonthlyAlltime(AT_lowapptemp, ApparentTemperature, false, timestamp);
			//}
		}

		public void DoWindChill(double chillpar, DateTime timestamp)
		{
			bool chillvalid = true;

			if (cumulus.CalculatedWC)
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

				if (WindChill < LowWindChillToday)
				{
					LowWindChillToday = WindChill;
					lowwindchilltodaytime = timestamp;
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
				if (WindChill < alltimerecarray[AT_lowchill].value)
				{
					SetAlltime(AT_lowchill, WindChill, timestamp);
				}

				CheckMonthlyAlltime(AT_lowchill, WindChill, false, timestamp);
			}
		}

		public void DoFeelsLike(DateTime timestamp)
		{
			// For now just provide a current value
			FeelsLike = ConvertTempCToUser(MeteoLib.FeelsLike(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage), OutdoorHumidity));

			if (FeelsLike > HighFeelsLikeToday)
			{
				HighFeelsLikeToday = FeelsLike;
				highfeelsliketodaytime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (FeelsLike < LowFeelsLikeToday)
			{
				LowFeelsLikeToday = FeelsLike;
				lowfeelsliketodaytime = timestamp;
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

			if (FeelsLike > alltimerecarray[AT_highfeelslike].value)
				SetAlltime(AT_highfeelslike, FeelsLike, timestamp);

			if (FeelsLike < alltimerecarray[AT_lowfeelslike].value)
				SetAlltime(AT_lowfeelslike, FeelsLike, timestamp);

			CheckMonthlyAlltime(AT_highfeelslike, FeelsLike, true, timestamp);
			CheckMonthlyAlltime(AT_lowfeelslike, FeelsLike, false, timestamp);
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

			if (WindRunToday > alltimerecarray[AT_highwindrun].value)
			{
				SetAlltime(AT_highwindrun, WindRunToday, adjustedtimestamp);
			}

			CheckMonthlyAlltime(AT_highwindrun, WindRunToday, true, adjustedtimestamp);
		}

		public void CheckForDewpointHighLow(DateTime timestamp)
		{
			if (OutdoorDewpoint > HighDewpointToday)
			{
				HighDewpointToday = OutdoorDewpoint;
				HighDewpointTodayTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (OutdoorDewpoint < LowDewpointToday)
			{
				LowDewpointToday = OutdoorDewpoint;
				LowDewpointTodayTime = timestamp;
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
			if (OutdoorDewpoint > alltimerecarray[AT_highdewpoint].value)
			{
				SetAlltime(AT_highdewpoint, OutdoorDewpoint, timestamp);
			}
			if (OutdoorDewpoint < alltimerecarray[AT_lowdewpoint].value)
				SetAlltime(AT_lowdewpoint, OutdoorDewpoint, timestamp);

			CheckMonthlyAlltime(AT_highdewpoint, OutdoorDewpoint, true, timestamp);
			CheckMonthlyAlltime(AT_lowdewpoint, OutdoorDewpoint, false, timestamp);
		}

		public void DoPressure(double sl, DateTime timestamp)
		{
			Pressure = sl * cumulus.PressMult + cumulus.PressOffset;
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

			if (Pressure > alltimerecarray[AT_highpress].value)
			{
				SetAlltime(AT_highpress, Pressure, timestamp);
			}

			DoAlarm(Pressure, cumulus.HighPressAlarmValue , cumulus.HighPressAlarmEnabled , true, ref cumulus.HighPressAlarmState);

			if (Pressure < alltimerecarray[AT_lowpress].value)
			{
				SetAlltime(AT_lowpress, Pressure, timestamp);
			}

			DoAlarm(Pressure, cumulus.LowPressAlarmValue , cumulus.LowPressAlarmEnabled , false, ref cumulus.LowPressAlarmState);
			CheckMonthlyAlltime(AT_lowpress, Pressure, false, timestamp);
			CheckMonthlyAlltime(AT_highpress, Pressure, true, timestamp);

			if (Pressure > highpresstoday)
			{
				highpresstoday = Pressure;
				highpresstodaytime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (Pressure < lowpresstoday)
			{
				lowpresstoday = Pressure;
				lowpresstodaytime = timestamp;
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
			if (cumulus.UseCumulusPresstrendstr || cumulus.Manufacturer == cumulus.DAVIS)
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

			if ((CurrentDay != readingTS.Day) || (CurrentMonth != readingTS.Month) || (CurrentYear != readingTS.Year) )
			{
				// A reading has apparently arrived at the start of a new day, but before we have done the rollover
				// Ignore it, as otherwise it may cause a new monthly record to be logged using last month's total
				return;
			}

			var previoustotal = Raincounter;

			double raintipthreshold;
			if (cumulus.RainUnit == 0)
				// mm
				raintipthreshold = cumulus.Manufacturer == cumulus.INSTROMET ? 0.009 : 0.09;
			else
				// in
				raintipthreshold = cumulus.Manufacturer == cumulus.INSTROMET ? 0.0003 : 0.009;

			if (total - Raincounter > raintipthreshold)
				// rain has occurred
				LastRainTip = timestamp.ToString("yyyy-MM-dd HH:mm");

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
						raindaystart = Raincounter - (RainToday/cumulus.RainMult);
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
					RainRate = rate*cumulus.RainMult;

					if (RainRate > alltimerecarray[AT_highrainrate].value)
						SetAlltime(AT_highrainrate, RainRate, timestamp);

					CheckMonthlyAlltime(AT_highrainrate, RainRate, true, timestamp);

					DoAlarm(RainRate, cumulus.HighRainRateAlarmValue, cumulus.HighRainRateAlarmEnabled, true, ref cumulus.HighRainRateAlarmState);

					if (RainRate > highraintoday)
					{
						highraintoday = RainRate;
						highraintodaytime = timestamp;
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
					RainToday *= cumulus.RainMult;

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
						RainSinceMidnight = trendval * cumulus.RainMult;
					}

					// rain this month so far
					RainMonth = rainthismonth + RainToday;

					// get correct date for rain records
					var offsetdate = timestamp.AddHours(cumulus.GetHourInc());

					// rain this year so far
					RainYear = rainthisyear + RainToday;

					if (RainToday > alltimerecarray[AT_dailyrain].value)
						SetAlltime(AT_dailyrain, RainToday, offsetdate);

					CheckMonthlyAlltime(AT_dailyrain, RainToday, true, timestamp);

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

					if (RainMonth > alltimerecarray[AT_wetmonth].value)
						SetAlltime(AT_wetmonth, RainMonth, offsetdate);

					CheckMonthlyAlltime(AT_wetmonth, RainMonth, true, timestamp);

					DoAlarm(RainToday, cumulus.HighRainTodayAlarmValue, cumulus.HighRainTodayAlarmEnabled, true, ref cumulus.HighRainTodayAlarmState);

					// Yesterday"s rain - Scale for units
					// rainyest = rainyesterday * RainMult;

					//RainReadyToPlot = true;
				}
			}
			HaveReadData = true;
		}

		public void DoOutdoorDewpoint(double dp, DateTime timestamp)
		{
			if (!cumulus.CalculatedDP)
			{
				OutdoorDewpoint = dp;
				CheckForDewpointHighLow(timestamp);
			}
		}

		public string LastRainTip { get; set; }

		public void DoExtraHum(double hum, int channel)
		{
			if ((channel > 0) && (channel < 11))
			{
				ExtraHum[channel] = (int) hum;
			}
		}

		public void DoExtraTemp(double temp, int channel)
		{
			if ((channel > 0) && (channel < 11))
			{
				ExtraTemp[channel] = temp;
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
					lp = cumulus.FClowpress/0.0295333727;
					hp = cumulus.FChighpress/0.0295333727;
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

		public double AltitudeM(double altitude)
		{
			if (cumulus.AltitudeInFeet)
			{
				return altitude*0.3048;
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
				return value/0.0295333727;
			else
				return value;
		}

		public double StationToAltimeter(double pressureHPa, double elevationM)
		{
			// from MADIS API by NOAA Forecast Systems Lab, see http://madis.noaa.gov/madis_api.html

			double k1 = 0.190284; // discrepency with calculated k1 probably because Smithsonian used less precise gas constant and gravity values
			double k2 = 8.4184960528E-5; // (standardLapseRate / standardTempK) * (Power(standardSLP, k1)
			return Math.Pow(Math.Pow(pressureHPa - 0.3, k1) + (k2*elevationM), 1/k1);
		}

		public bool PressReadyToPlot { get; set; }

		public bool first_press { get; set; }

		/*
		private string TimeToStrHHMM(DateTime timestamp)
		{
			return timestamp.ToString("HHmm");
		}
		*/

		public void DoAlarm(double value, double threshold, bool enabled, bool testAbove, ref bool alarmState)
		{
			if (enabled)
			{
				if (testAbove)
				{
					alarmState = value > threshold;
				}
				else
				{
					alarmState = value < threshold;
				}
			}
		}

		public void DoWind(double gustpar, int bearingpar, double speedpar, DateTime timestamp)
		{

			// use bearing of zero when calm
			if ((Math.Abs(gustpar) < 0.001) && cumulus.UseZeroBearing)
			{
				Bearing = 0;
			}
			else
			{
				Bearing = (bearingpar + cumulus.WindDirOffset) % 360;
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
			calibratedgust = uncalibratedgust*cumulus.WindGustMult;
			WindLatest = calibratedgust;
			windspeeds[nextwindvalue] = uncalibratedgust;
			windbears[nextwindvalue] = Bearing;

			// Recalculate wind rose data
			for (int i = 0; i < cumulus.NumWindRosePoints; i++)
			{
				windcounts[i] = 0;
			}

			for (int i = 0; i < numwindvalues; i++)
			{
				int j = (((windbears[i]*100) + 1125)%36000)/(int) Math.Floor(cumulus.WindRoseAngle*100);
				windcounts[j] += windspeeds[i];
			}

			if (numwindvalues < maxwindvalues)
			{
				numwindvalues++;
			}

			nextwindvalue = (nextwindvalue + 1)%maxwindvalues;
			if (calibratedgust > highgusttoday)
			{
				highgusttoday = calibratedgust;
				highgusttodaytime = timestamp;
				highgustbearing = Bearing;
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
			if (calibratedgust > alltimerecarray[AT_highgust].value)
			{
				SetAlltime(AT_highgust, calibratedgust, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime(AT_highgust, calibratedgust, true, timestamp);

			WindRecent[nextwind].Gust = uncalibratedgust;
			WindRecent[nextwind].Speed = speedpar;
			WindRecent[nextwind].Timestamp = timestamp;
			nextwind = (nextwind + 1)%cumulus.MaxWindRecent;
			if (cumulus.UseWind10MinAve)
			{
				int numvalues = 0;
				double totalwind = 0;
				for (int i = 0; i < cumulus.MaxWindRecent; i++)
				{
					if (timestamp - WindRecent[i].Timestamp <= cumulus.AvgSpeedTime)
					{
						numvalues++;
						if (cumulus.UseSpeedForAvgCalc)
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
				WindAverage = totalwind/numvalues;
				//cumulus.LogDebugMessage("next=" + nextwind + " wind=" + uncalibratedgust + " tot=" + totalwind + " numv=" + numvalues + " avg=" + WindAverage);
			}
			else
			{
				WindAverage = speedpar;
			}

			WindAverage *= cumulus.WindSpeedMult;

			DoAlarm(WindAverage, cumulus.HighWindAlarmValue, cumulus.HighWindAlarmEnabled, true, ref cumulus.HighWindAlarmState);

			if (CalcRecentMaxGust)
			{
				// Find recent max gust
				double maxgust = 0;
				for (int i = 0; i <= cumulus.MaxWindRecent - 1; i++)
				{
					if (timestamp - WindRecent[i].Timestamp <= cumulus.PeakGustTime)
					{
						if (WindRecent[i].Gust > maxgust)
						{
							maxgust = WindRecent[i].Gust;
						}
					}
				}
				RecentMaxGust = maxgust*cumulus.WindGustMult;
			}

			DoAlarm(RecentMaxGust, cumulus.HighGustAlarmValue, cumulus.HighGustAlarmEnabled, true, ref cumulus.HighGustAlarmState);

			if (WindAverage > highwindtoday)
			{
				highwindtoday = WindAverage;
				highwindtodaytime = timestamp;
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

			WindVec[nextwindvec].X = calibratedgust*Math.Sin(DegToRad(Bearing));
			WindVec[nextwindvec].Y = calibratedgust*Math.Cos(DegToRad(Bearing));
			// save timestamp of this reading
			WindVec[nextwindvec].Timestamp = timestamp;
			// save bearing
			WindVec[nextwindvec].Bearing = Bearing; // savedBearing;
			// increment index for next reading
			nextwindvec = (nextwindvec + 1)%MaxWindRecent;

			// Now add up all the values within the required period
			double totalwindX = 0;
			double totalwindY = 0;
			for (int i = 0; i < cumulus.MaxWindRecent; i++)
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
				AvgBearing = (int) Math.Round(RadToDeg(Math.Atan(totalwindY/totalwindX)));

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

			if ((Math.Abs(WindAverage) < 0.01) && cumulus.UseZeroBearing)
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
				for (int i = 0; i <= cumulus.MaxWindRecent - 1; i++)
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
							BearingRangeTo10 = (int) (Math.Ceiling(WindVec[i].Bearing/10.0)*10);
						}
						if ((difference < diffFrom))
						{
							diffFrom = difference;
							BearingRangeFrom = WindVec[i].Bearing;
							BearingRangeFrom10 = (int) (Math.Floor(WindVec[i].Bearing/10.0)*10);
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
			if (WindAverage > alltimerecarray[AT_highwind].value)
			{
				SetAlltime(AT_highwind, WindAverage, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime(AT_highwind, WindAverage, true, timestamp);

			WindReadyToPlot = true;
			HaveReadData = true;
		}

		public void DoUV(double value, DateTime timestamp)
		{
			UV = (value*cumulus.UVMult) + cumulus.UVOffset;
			if (UV < 0)
				UV = 0;
			if (UV > 16)
				UV = 16;

			if (UV > HighUVToday)
			{
				HighUVToday = UV;
				highuvtodaytime = timestamp;
			}

			HaveReadData = true;
		}

		protected void DoSolarRad(int value, DateTime timestamp)
		{
			SolarRad = (value * cumulus.SolarMult) + cumulus.SolarOffset;
			// Update display

			if (SolarRad > HighSolarToday)
			{
				HighSolarToday = SolarRad;
				highsolartodaytime = timestamp;
			}
			CurrentSolarMax = AstroLib.SolarMax(timestamp, cumulus.Longitude, cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.RStransfactor, cumulus.BrasTurbidity);

			if (!cumulus.UseBlakeLarsen)
			{
				IsSunny = (SolarRad > (CurrentSolarMax*cumulus.SunThreshold/100)) && (SolarRad >= cumulus.SolarMinimum);
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
			WetBulb = (WetBulb*cumulus.WetBulbMult) + cumulus.WetBulbOffset;

			// calculate RH
			double TempDry = ConvertUserTempToC(OutdoorTemperature);
			double Es = MeteoLib.SaturationVaporPressure(TempDry);
			double Ew = MeteoLib.SaturationVaporPressure(temp);
			double E = Ew - (0.00066*(1 + 0.00115*temp)*(TempDry - temp)*1013);
			int hum = (int) (100*(E/Es));
			DoOutdoorHumidity(hum, timestamp);
			// calculate DP
			// Calculate DewPoint

			// dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
			OutdoorDewpoint = ConvertTempCToUser(MeteoLib.DewPoint(TempDry, hum));

			CheckForDewpointHighLow(timestamp);
		}

		public bool IsSunny { get; set; }

		public bool HaveReadData { get; set; }

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
				string s = FormatDateTime("yyyy-MM-dd", alltimerecarray[index].timestamp) + FormatDateTime(" HH", alltimerecarray[index].timestamp) + ":" +
						   FormatDateTime("mm ", alltimerecarray[index].timestamp) + String.Format("{0,7:0.000}", value) + " \"" + alltimedescs[index] + "\" " +
						   FormatDateTime("yyyy-MM-dd", oldts) + FormatDateTime(" HH", oldts) + ":" + FormatDateTime("mm ", oldts) + String.Format("{0,7:0.000}", oldvalue) +
						   Environment.NewLine;

				cumulus.LogMessage(s);
				File.AppendAllText(cumulus.Alltimelogfile, s);
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

			string s = "month = " + month.ToString("D2") + ": ";
			s = s + timestamp.ToString("yyyy-MM-dd") + FormatDateTime(" HH", timestamp) + ":" + FormatDateTime("mm ", timestamp);
			s = s + value.ToString("F3") + " \"" + alltimedescs[index] + "\" ";
			s = s + FormatDateTime("yyyy-MM-dd", oldts) + FormatDateTime(" HH", oldts) + ":" + FormatDateTime("mm ", oldts);
			s = s + oldvalue.ToString("F3") + Environment.NewLine;

			File.AppendAllText(cumulus.MonthlyAlltimeLogFile, s);
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

		public int nextwind { get; set; }

		public int nextwindvec { get; set; }

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
		private const int MaxWindRecent = 720;
		protected readonly double[] WindRunHourMult = {3.6, 1.0, 1.0, 1.0};
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
			ini.SetValue("Wind", "Speed", highwindyesterday);
			ini.SetValue("Wind", "SpTime", highwindyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Wind", "Gust", highgustyesterday);
			ini.SetValue("Wind", "Time", highgustyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Wind", "Bearing", highgustbearingyesterday);
			ini.SetValue("Wind", "Direction", CompassPoint(highgustbearingyesterday));
			ini.SetValue("Wind", "Windrun", YesterdayWindRun);
			ini.SetValue("Wind", "DominantWindBearing", YestDominantWindBearing);
			// Temperature
			ini.SetValue("Temp", "Low", LowTempYesterday);
			ini.SetValue("Temp", "LTime", lowtempyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Temp", "High", HighTempYesterday);
			ini.SetValue("Temp", "HTime", hightempyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Temp", "HeatingDegreeDays", YestHeatingDegreeDays);
			ini.SetValue("Temp", "CoolingDegreeDays", YestCoolingDegreeDays);
			ini.SetValue("Temp", "AvgTemp", YestAvgTemp);
			// Pressure
			ini.SetValue("Pressure", "Low", lowpressyesterday);
			ini.SetValue("Pressure", "LTime", lowpressyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Pressure", "High", highpressyesterday);
			ini.SetValue("Pressure", "HTime", highpressyesterdaytime.ToString("HH:mm"));
			// rain rate
			ini.SetValue("Rain", "High", highrainyesterday);
			ini.SetValue("Rain", "HTime", highrainyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Rain", "HourlyHigh", highhourlyrainyesterday);
			ini.SetValue("Rain", "HHourlyTime", highhourlyrainyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Rain", "RG11Yesterday", RG11RainYesterday);
			// humidity
			ini.SetValue("Humidity", "Low", lowhumidityyesterday);
			ini.SetValue("Humidity", "High", highhumidityyesterday);
			ini.SetValue("Humidity", "LTime", lowhumidityyesterdaytime.ToString("HH:mm"));
			ini.SetValue("Humidity", "HTime", highhumidityyesterdaytime.ToString("HH:mm"));
			// Solar
			ini.SetValue("Solar", "SunshineHours", YestSunshineHours);
			// heat index
			ini.SetValue("HeatIndex", "High", HighHeatIndexYesterday);
			ini.SetValue("HeatIndex", "HTime", highheatindexyesterdaytime.ToString("HH:mm"));
			// App temp
			ini.SetValue("AppTemp", "Low", LowAppTempYesterday);
			ini.SetValue("AppTemp", "LTime", lowapptempyesterdaytime.ToString("HH:mm"));
			ini.SetValue("AppTemp", "High", HighAppTempYesterday);
			ini.SetValue("AppTemp", "HTime", highapptempyesterdaytime.ToString("HH:mm"));
			// wind chill
			ini.SetValue("WindChill", "Low", lowwindchillyesterday);
			ini.SetValue("WindChill", "LTime", lowwindchillyesterdaytime.ToString("HH:mm"));
			// Dewpoint
			ini.SetValue("Dewpoint", "Low", LowDewpointYesterday);
			ini.SetValue("Dewpoint", "LTime", LowDewpointYesterdayTime.ToString("HH:mm"));
			ini.SetValue("Dewpoint", "High", HighDewpointYesterday);
			ini.SetValue("Dewpoint", "HTime", HighDewpointYesterdayTime.ToString("HH:mm"));
			// Solar
			ini.SetValue("Solar", "HighSolarRad", HighSolarYesterday);
			ini.SetValue("Solar", "HighSolarRadTime", highsolaryesterdaytime.ToString("HH:mm"));
			ini.SetValue("Solar", "HighUV", HighUVYesterday);
			ini.SetValue("Solar", "HighUVTime", highuvyesterdaytime.ToString("HH:mm"));
			// Feels like
			ini.SetValue("FeelsLike", "Low", LowFeelsLikeYesterday);
			ini.SetValue("FeelsLike", "LTime", lowfeelslikeyesterdaytime.ToString("HH:mm"));
			ini.SetValue("FeelsLike", "High", HighFeelsLikeYesterday);
			ini.SetValue("FeelsLike", "HTime", highfeelslikeyesterdaytime.ToString("HH:mm"));

			ini.Flush();

			cumulus.LogMessage("Written yesterday.ini");
		}

		public void ReadYesterdayFile()
		{
			//var hourInc = cumulus.GetHourInc();

			IniFile ini = new IniFile(cumulus.YesterdayFile);

			// Wind
			highwindyesterday = ini.GetValue("Wind", "Speed", 0.0);
			highwindyesterdaytime = ini.GetValue("Wind", "SpTime", DateTime.MinValue);
			highgustyesterday = ini.GetValue("Wind", "Gust", 0.0);
			highgustyesterdaytime = ini.GetValue("Wind", "Time", DateTime.MinValue);
			highgustbearingyesterday = ini.GetValue("Wind", "Bearing", 0);

			YesterdayWindRun = ini.GetValue("Wind", "Windrun", 0.0);
			YestDominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			// Temperature
			LowTempYesterday = ini.GetValue("Temp", "Low", 0.0);
			lowtempyesterdaytime = ini.GetValue("Temp", "LTime", DateTime.MinValue);
			HighTempYesterday = ini.GetValue("Temp", "High", 0.0);
			hightempyesterdaytime = ini.GetValue("Temp", "HTime", DateTime.MinValue);
			YestHeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			YestCoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			YestAvgTemp = ini.GetValue("Temp", "AvgTemp", 0.0);
			TempRangeYesterday = HighTempYesterday - LowTempYesterday;
			// Pressure
			lowpressyesterday = ini.GetValue("Pressure", "Low", 0.0);
			lowpressyesterdaytime = ini.GetValue("Pressure", "LTime", DateTime.MinValue);
			highpressyesterday = ini.GetValue("Pressure", "High", 0.0);
			highpressyesterdaytime = ini.GetValue("Pressure", "HTime", DateTime.MinValue);
			// rain rate
			highrainyesterday = ini.GetValue("Rain", "High", 0.0);
			highrainyesterdaytime = ini.GetValue("Rain", "HTime", DateTime.MinValue);
			highhourlyrainyesterday = ini.GetValue("Rain", "HourlyHigh", 0.0);
			highhourlyrainyesterdaytime = ini.GetValue("Rain", "HHourlyTime", DateTime.MinValue);
			RG11RainYesterday = ini.GetValue("Rain", "RG11Yesterday", 0.0);
			// humidity
			lowhumidityyesterday = ini.GetValue("Humidity", "Low", 0);
			highhumidityyesterday = ini.GetValue("Humidity", "High", 0);
			lowhumidityyesterdaytime = ini.GetValue("Humidity", "LTime", DateTime.MinValue);
			highhumidityyesterdaytime = ini.GetValue("Humidity", "HTime", DateTime.MinValue);
			// Solar
			YestSunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			// heat index
			HighHeatIndexYesterday = ini.GetValue("HeatIndex", "High", 0.0);
			highheatindexyesterdaytime = ini.GetValue("HeatIndex", "HTime", DateTime.MinValue);
			// App temp
			LowAppTempYesterday = ini.GetValue("AppTemp", "Low", 0.0);
			lowapptempyesterdaytime = ini.GetValue("AppTemp", "LTime", DateTime.MinValue);
			HighAppTempYesterday = ini.GetValue("AppTemp", "High", 0.0);
			highapptempyesterdaytime = ini.GetValue("AppTemp", "HTime", DateTime.MinValue);
			// wind chill
			lowwindchillyesterday = ini.GetValue("WindChill", "Low", 0.0);
			lowwindchillyesterdaytime = ini.GetValue("WindChill", "LTime", DateTime.MinValue);
			// Dewpoint
			LowDewpointYesterday = ini.GetValue("Dewpoint", "Low", 0.0);
			LowDewpointYesterdayTime = ini.GetValue("Dewpoint", "LTime", DateTime.MinValue);
			HighDewpointYesterday = ini.GetValue("Dewpoint", "High", 0.0);
			HighDewpointYesterdayTime = ini.GetValue("Dewpoint", "HTime", DateTime.MinValue);
			// Solar
			HighSolarYesterday = ini.GetValue("Solar", "HighSolarRad", 0.0);
			highsolaryesterdaytime = ini.GetValue("Solar", "HighSolarRadTime", DateTime.MinValue);
			HighUVYesterday = ini.GetValue("Solar", "HighUV", 0.0);
			highuvyesterdaytime = ini.GetValue("Solar", "HighUVTime", DateTime.MinValue);
			// Feels like
			LowFeelsLikeYesterday = ini.GetValue("FeelsLike", "Low", 0.0);
			lowfeelslikeyesterdaytime = ini.GetValue("FeelsLike", "LTime", DateTime.MinValue);
			HighFeelsLikeYesterday = ini.GetValue("FeelsLike", "High", 0.0);
			highfeelslikeyesterdaytime = ini.GetValue("FeelsLike", "HTime", DateTime.MinValue);
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
				RainYesterday = (Raincounter - raindaystart)*cumulus.RainMult;
				cumulus.LogMessage("Rainyesterday (calibrated) set to " + RainYesterday);

				AddRecentDailyData(timestamp.AddDays(-1),RainYesterday,(cumulus.RolloverHour == 0?SunshineHours:SunshineToMidnight), LowTempToday,HighTempToday,YestAvgTemp);
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
					rdthresh1000 = Convert.ToInt32(cumulus.RainDayThreshold*1000.0);
				}

				// set up rain yesterday * 1000 for comparison
				int ryest1000 = Convert.ToInt32(RainYesterday*1000.0);

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

					if (ConsecutiveRainDays > alltimerecarray[AT_longestwetperiod].value)
						SetAlltime(AT_longestwetperiod, ConsecutiveRainDays, yesterday);

					CheckMonthlyAlltime(AT_longestwetperiod, ConsecutiveRainDays, true, yesterday);
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

					if (ConsecutiveDryDays > alltimerecarray[AT_longestdryperiod].value)
						SetAlltime(AT_longestdryperiod, ConsecutiveDryDays, yesterday);

					CheckMonthlyAlltime(AT_longestdryperiod, ConsecutiveDryDays, true, yesterday);
				}

				// offset high temp today timestamp to allow for 0900 rollover
				int hr;
				int mn;
				DateTime ts;
				try
				{
					hr = hightemptodaytime.Hour;
					mn = hightemptodaytime.Minute;
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

				if (HighTempToday < alltimerecarray[AT_lowmaxtemp].value)
				{
					SetAlltime(AT_lowmaxtemp, HighTempToday, ts);
				}

				CheckMonthlyAlltime(AT_lowmaxtemp, HighTempToday, false, ts);

				if (HighTempToday < LowMaxTempThisMonth)
				{
					LowMaxTempThisMonth = HighTempToday;
					try
					{
						hr = hightemptodaytime.Hour;
						mn = hightemptodaytime.Minute;
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

				if (HighTempToday < LowMaxTempThisYear)
				{
					LowMaxTempThisYear = HighTempToday;
					try
					{
						hr = hightemptodaytime.Hour;
						mn = hightemptodaytime.Minute;
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
					hr = lowtemptodaytime.Hour;
					mn = lowtemptodaytime.Minute;
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

				if (LowTempToday > alltimerecarray[AT_highmintemp].value)
				{
					SetAlltime(AT_highmintemp, LowTempToday, ts);
				}

				CheckMonthlyAlltime(AT_highmintemp, LowTempToday, true, ts);

				if (LowTempToday > HighMinTempThisMonth)
				{
					HighMinTempThisMonth = LowTempToday;
					try
					{
						hr = lowtemptodaytime.Hour;
						mn = lowtemptodaytime.Minute;
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

				if (LowTempToday > HighMinTempThisYear)
				{
					HighMinTempThisYear = LowTempToday;
					try
					{
						hr = lowtemptodaytime.Hour;
						mn = lowtemptodaytime.Minute;
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
				if (TempRangeToday > alltimerecarray[AT_highdailytemprange].value)
					SetAlltime(AT_highdailytemprange, TempRangeToday, yesterday);

				if (TempRangeToday < alltimerecarray[AT_lowdailytemprange].value)
					SetAlltime(AT_lowdailytemprange, TempRangeToday, yesterday);

				CheckMonthlyAlltime(AT_highdailytemprange, TempRangeToday, true, yesterday);
				CheckMonthlyAlltime(AT_lowdailytemprange, TempRangeToday, false, yesterday);

				if (TempRangeToday > HighDailyTempRangeThisMonth)
				{
					HighDailyTempRangeThisMonth = TempRangeToday;
					HighDailyTempRangeThisMonthTS = yesterday;
					WriteMonthIniFile();
				}

				if (TempRangeToday < LowDailyTempRangeThisMonth)
				{
					LowDailyTempRangeThisMonth = TempRangeToday;
					LowDailyTempRangeThisMonthTS = yesterday;
					WriteMonthIniFile();
				}

				if (TempRangeToday > HighDailyTempRangeThisYear)
				{
					HighDailyTempRangeThisYear = TempRangeToday;
					HighDailyTempRangeThisYearTS = yesterday;
					WriteYearIniFile();
				}

				if (TempRangeToday < LowDailyTempRangeThisYear)
				{
					LowDailyTempRangeThisYear = TempRangeToday;
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
				highwindyesterday = highwindtoday;
				highwindyesterdaytime = highwindtodaytime;
				highgustyesterday = highgusttoday;
				highgustyesterdaytime = highgusttodaytime;
				highgustbearingyesterday = highgustbearing;

				// Reset today"s high wind settings
				highgusttoday = calibratedgust;
				highgustbearing = Bearing;
				highwindtoday = WindAverage;

				highwindtodaytime = timestamp;
				highgusttodaytime = timestamp;

				// Copy today"s high temp settings to yesterday
				HighTempYesterday = HighTempToday;
				hightempyesterdaytime = hightemptodaytime;
				// Reset today"s high temp settings
				HighTempToday = OutdoorTemperature;
				hightemptodaytime = timestamp;

				// Copy today"s low temp settings to yesterday
				LowTempYesterday = LowTempToday;
				lowtempyesterdaytime = lowtemptodaytime;
				// Reset today"s low temp settings
				LowTempToday = OutdoorTemperature;
				lowtemptodaytime = timestamp;

				TempRangeYesterday = TempRangeToday;
				TempRangeToday = 0;

				// Copy today"s low pressure settings to yesterday
				lowpressyesterday = lowpresstoday;
				lowpressyesterdaytime = lowpresstodaytime;
				// Reset today"s low pressure settings
				lowpresstoday = Pressure;
				lowpresstodaytime = timestamp;

				// Copy today"s high pressure settings to yesterday
				highpressyesterday = highpresstoday;
				highpressyesterdaytime = highpresstodaytime;
				// Reset today"s high pressure settings
				highpresstoday = Pressure;
				highpresstodaytime = timestamp;

				// Copy today"s high rain rate settings to yesterday
				highrainyesterday = highraintoday;
				highrainyesterdaytime = highraintodaytime;
				// Reset today"s high rain rate settings
				highraintoday = RainRate;
				highraintodaytime = timestamp;

				highhourlyrainyesterday = highhourlyraintoday;
				highhourlyrainyesterdaytime = highhourlyraintodaytime;
				highhourlyraintoday = RainLastHour;
				highhourlyraintodaytime = timestamp;

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
				lowhumidityyesterday = lowhumiditytoday;
				lowhumidityyesterdaytime = lowhumiditytodaytime;
				lowhumiditytoday = OutdoorHumidity;
				lowhumiditytodaytime = timestamp;

				highhumidityyesterday = highhumiditytoday;
				highhumidityyesterdaytime = highhumiditytodaytime;
				highhumiditytoday = OutdoorHumidity;
				highhumiditytodaytime = timestamp;

				// heat index
				HighHeatIndexYesterday = HighHeatIndexToday;
				highheatindexyesterdaytime = highheatindextodaytime;
				HighHeatIndexToday = HeatIndex;
				highheatindextodaytime = timestamp;

				// App temp
				HighAppTempYesterday = HighAppTempToday;
				highapptempyesterdaytime = highapptemptodaytime;
				HighAppTempToday = ApparentTemperature;
				highapptemptodaytime = timestamp;

				LowAppTempYesterday = LowAppTempToday;
				lowapptempyesterdaytime = lowapptemptodaytime;
				LowAppTempToday = ApparentTemperature;
				lowapptemptodaytime = timestamp;

				// wind chill
				lowwindchillyesterday = LowWindChillToday;
				lowwindchillyesterdaytime = lowwindchilltodaytime;
				LowWindChillToday = WindChill;
				lowwindchilltodaytime = timestamp;

				// dew point
				HighDewpointYesterday = HighDewpointToday;
				HighDewpointYesterdayTime = HighDewpointTodayTime;
				HighDewpointToday = OutdoorDewpoint;
				HighDewpointTodayTime = timestamp;

				LowDewpointYesterday = LowDewpointToday;
				LowDewpointYesterdayTime = LowDewpointTodayTime;
				LowDewpointToday = OutdoorDewpoint;
				LowDewpointTodayTime = timestamp;

				// solar
				HighSolarYesterday = HighSolarToday;
				highsolaryesterdaytime = highsolartodaytime;
				HighSolarToday = SolarRad;
				highsolartodaytime = timestamp;

				HighUVYesterday = HighUVToday;
				highuvyesterdaytime = highuvtodaytime;
				HighUVToday = UV;
				highuvtodaytime = timestamp;

				// Feels like
				HighFeelsLikeYesterday = HighFeelsLikeToday;
				highfeelslikeyesterdaytime = highfeelsliketodaytime;
				HighFeelsLikeToday = FeelsLike;
				highfeelsliketodaytime = timestamp;

				LowFeelsLikeYesterday = LowFeelsLikeToday;
				lowfeelslikeyesterdaytime = lowfeelsliketodaytime;
				LowFeelsLikeToday = FeelsLike;
				lowfeelsliketodaytime = timestamp;


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
			cumulus.LogMessage("Saving month.ini file as "+savedFile);
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

			double AvgTemp;
			if (tempsamplestoday > 0)
				AvgTemp = TempTotalToday/tempsamplestoday;
			else
				AvgTemp = 0;

			// save the value for yesterday
			YestAvgTemp = AvgTemp;

			string datestring = timestamp.AddDays(-1).ToString("dd/MM/yy");;
			// NB this string is just for logging, the dayfile update code is further down
			var str = datestring + cumulus.ListSeparator;
			str += highgusttoday.ToString(cumulus.WindFormat) + cumulus.ListSeparator;
			str += highgustbearing + cumulus.ListSeparator;
			str += highgusttodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += LowTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += lowtemptodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += HighTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += hightemptodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += lowpresstoday.ToString(cumulus.PressFormat) + cumulus.ListSeparator;
			str += lowpresstodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += highpresstoday.ToString(cumulus.PressFormat) + cumulus.ListSeparator;
			str += highpresstodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += highraintoday.ToString(cumulus.RainFormat) + cumulus.ListSeparator;
			str += highraintodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += RainToday.ToString(cumulus.RainFormat) + cumulus.ListSeparator;
			str += AvgTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += WindRunToday.ToString("F1") + cumulus.ListSeparator;
			str += highwindtoday.ToString(cumulus.WindFormat) + cumulus.ListSeparator;
			str += highwindtodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += lowhumiditytoday + cumulus.ListSeparator;
			str += lowhumiditytodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += highhumiditytoday + cumulus.ListSeparator;
			str += highhumiditytodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += ET.ToString(cumulus.ETFormat) + cumulus.ListSeparator;
			if (cumulus.RolloverHour == 0)
			{
				// use existing current sunshinehour count
				str += SunshineHours.ToString(cumulus.SunFormat) + cumulus.ListSeparator;
			}
			else
			{
				// for non-midnight rollover, use new item
				str += SunshineToMidnight.ToString(cumulus.SunFormat) + cumulus.ListSeparator;
			}
			str += HighHeatIndexToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += highheatindextodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += HighAppTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += highapptemptodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += LowAppTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += lowapptemptodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += highhourlyraintoday.ToString(cumulus.RainFormat) + cumulus.ListSeparator;
			str += highhourlyraintodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += LowWindChillToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += lowwindchilltodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += HighDewpointToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += HighDewpointTodayTime.ToString("HH:mm") + cumulus.ListSeparator;
			str += LowDewpointToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += LowDewpointTodayTime.ToString("HH:mm") + cumulus.ListSeparator;
			str += DominantWindBearing + cumulus.ListSeparator;
			str += HeatingDegreeDays.ToString("F1") + cumulus.ListSeparator;
			str += CoolingDegreeDays.ToString("F1") + cumulus.ListSeparator;
			str += (int)HighSolarToday + cumulus.ListSeparator;
			str += highsolartodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += HighUVToday.ToString(cumulus.UVFormat) + cumulus.ListSeparator;
			str += highuvtodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += HighFeelsLikeToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += highfeelsliketodaytime.ToString("HH:mm") + cumulus.ListSeparator;
			str += LowFeelsLikeToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator;
			str += lowfeelsliketodaytime.ToString("HH:mm");

			cumulus.LogMessage("Dayfile.txt entry:");
			cumulus.LogMessage(str);

			try
			{
				using (FileStream fs = new FileStream(cumulus.DayFile, FileMode.Append, FileAccess.Write, FileShare.Read))
				using (StreamWriter file = new StreamWriter(fs))
				{
					cumulus.LogMessage("Dayfile.txt opened for writing");

					if ((HighTempToday < -400) || (LowTempToday > 900))
					{
						cumulus.LogMessage("***Error: Daily values are still at default at end of day");
						cumulus.LogMessage("Data not logged to dayfile.txt");
					}
					else
					{
						cumulus.LogMessage("Writing entry to dayfile.txt");

						file.Write(datestring + cumulus.ListSeparator);
						file.Write(highgusttoday.ToString(cumulus.WindFormat) + cumulus.ListSeparator);
						file.Write(highgustbearing + cumulus.ListSeparator);
						file.Write(highgusttodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(LowTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(lowtemptodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(HighTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(hightemptodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(lowpresstoday.ToString(cumulus.PressFormat) + cumulus.ListSeparator);
						file.Write(lowpresstodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(highpresstoday.ToString(cumulus.PressFormat) + cumulus.ListSeparator);
						file.Write(highpresstodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(highraintoday.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
						file.Write(highraintodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(RainToday.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
						file.Write(AvgTemp.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(WindRunToday.ToString("F1") + cumulus.ListSeparator);
						file.Write(highwindtoday.ToString(cumulus.WindFormat) + cumulus.ListSeparator);
						file.Write(highwindtodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(lowhumiditytoday + cumulus.ListSeparator);
						file.Write(lowhumiditytodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(highhumiditytoday + cumulus.ListSeparator);
						file.Write(highhumiditytodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(ET.ToString(cumulus.ETFormat) + cumulus.ListSeparator);
						if (cumulus.RolloverHour == 0)
						{
							// use existing current sunshinehour count to minimise risk
							file.Write(SunshineHours.ToString(cumulus.SunFormat) + cumulus.ListSeparator);
						}
						else
						{
							// for non-midnight rollover, use new item
							file.Write(SunshineToMidnight.ToString(cumulus.SunFormat) + cumulus.ListSeparator);
						}
						file.Write(HighHeatIndexToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(highheatindextodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(HighAppTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(highapptemptodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(LowAppTempToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(lowapptemptodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(highhourlyraintoday.ToString(cumulus.RainFormat) + cumulus.ListSeparator);
						file.Write(highhourlyraintodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(LowWindChillToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(lowwindchilltodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(HighDewpointToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(HighDewpointTodayTime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(LowDewpointToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(LowDewpointTodayTime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(DominantWindBearing + cumulus.ListSeparator);
						file.Write(HeatingDegreeDays.ToString("F1") + cumulus.ListSeparator);
						file.Write(CoolingDegreeDays.ToString("F1") + cumulus.ListSeparator);
						file.Write((int)HighSolarToday + cumulus.ListSeparator);
						file.Write(highsolartodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(HighUVToday.ToString(cumulus.UVFormat) + cumulus.ListSeparator);
						file.Write(highuvtodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(HighFeelsLikeToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.Write(highfeelsliketodaytime.ToString("HH:mm") + cumulus.ListSeparator);
						file.Write(LowFeelsLikeToday.ToString(cumulus.TempFormat) + cumulus.ListSeparator);
						file.WriteLine(lowfeelsliketodaytime.ToString("HH:mm"));
						file.Close();
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error writing to dayfile.txt: "+ex.Message);
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

				string values = " Values('" + timestamp.AddDays(-1).ToString("yy-MM-dd") + "'," +
					highgusttoday.ToString(cumulus.WindFormat, InvC) + "," +
				highgustbearing + "," +
				highgusttodaytime.ToString("\\'HH:mm\\'") + "," +
				LowTempToday.ToString(cumulus.TempFormat, InvC) + "," +
				lowtemptodaytime.ToString("\\'HH:mm\\'") + "," +
				HighTempToday.ToString(cumulus.TempFormat, InvC) + "," +
				hightemptodaytime.ToString("\\'HH:mm\\'") + "," +
				lowpresstoday.ToString(cumulus.PressFormat, InvC) + "," +
				lowpresstodaytime.ToString("\\'HH:mm\\'") + "," +
				highpresstoday.ToString(cumulus.PressFormat, InvC) + "," +
				highpresstodaytime.ToString("\\'HH:mm\\'") + "," +
				highraintoday.ToString(cumulus.RainFormat, InvC) + "," +
				highraintodaytime.ToString("\\'HH:mm\\'") + "," +
				RainToday.ToString(cumulus.RainFormat, InvC) + "," +
				AvgTemp.ToString(cumulus.TempFormat, InvC) + "," +
				WindRunToday.ToString("F1",InvC) + "," +
				highwindtoday.ToString(cumulus.WindFormat, InvC) + "," +
				highwindtodaytime.ToString("\\'HH:mm\\'") + "," +
				lowhumiditytoday + "," +
				lowhumiditytodaytime.ToString("\\'HH:mm\\'") + "," +
				highhumiditytoday + "," +
				highhumiditytodaytime.ToString("\\'HH:mm\\'") + "," +
				ET.ToString(cumulus.ETFormat, InvC) + "," +
				(cumulus.RolloverHour == 0 ? SunshineHours.ToString(cumulus.SunFormat, InvC) : SunshineToMidnight.ToString(cumulus.SunFormat, InvC)) + "," +
				HighHeatIndexToday.ToString(cumulus.TempFormat, InvC) + "," +
				highheatindextodaytime.ToString("\\'HH:mm\\'") + "," +
				HighAppTempToday.ToString(cumulus.TempFormat, InvC) + "," +
				highapptemptodaytime.ToString("\\'HH:mm\\'") + "," +
				LowAppTempToday.ToString(cumulus.TempFormat, InvC) + "," +
				lowapptemptodaytime.ToString("\\'HH:mm\\'") + "," +
				highhourlyraintoday.ToString(cumulus.RainFormat, InvC) + "," +
				highhourlyraintodaytime.ToString("\\'HH:mm\\'") + "," +
				LowWindChillToday.ToString(cumulus.TempFormat, InvC) + "," +
				lowwindchilltodaytime.ToString("\\'HH:mm\\'") + "," +
				HighDewpointToday.ToString(cumulus.TempFormat, InvC) + "," +
				HighDewpointTodayTime.ToString("\\'HH:mm\\'") + "," +
				LowDewpointToday.ToString(cumulus.TempFormat, InvC) + "," +
				LowDewpointTodayTime.ToString("\\'HH:mm\\'") + "," +
				DominantWindBearing + "," +
				HeatingDegreeDays.ToString("F1",InvC) + "," +
				CoolingDegreeDays.ToString("F1",InvC) + "," +
				(int)HighSolarToday + "," +
				highsolartodaytime.ToString("\\'HH:mm\\'") + "," +
				HighUVToday.ToString(cumulus.UVFormat, InvC) + "," +
				highuvtodaytime.ToString("\\'HH:mm\\'") + ",'" +
				CompassPoint(highgustbearing) + "','" +
				CompassPoint(DominantWindBearing) + "'," +
				HighFeelsLikeToday.ToString(cumulus.TempFormat, InvC) + "," +
				highfeelsliketodaytime.ToString("\\'HH:mm\\'") + "," +
				LowFeelsLikeToday.ToString(cumulus.TempFormat, InvC) + "," +
				lowfeelsliketodaytime.ToString("\\'HH:mm\\'") +
				")";

				string queryString = cumulus.StartOfDayfileInsertSQL + values;

				MySqlCommand cmd = new MySqlCommand();
				cmd.CommandText = queryString;
				cmd.Connection = mySqlConn;
				cumulus.LogMessage(queryString);

				try
				{
					mySqlConn.Open();
					int aff = cmd.ExecuteNonQuery();
					cumulus.LogMessage("MySQL: " + aff + " rows were affected.");
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error encountered during MySQL operation.");
					cumulus.LogMessage(ex.Message);
				}
				finally
				{
					mySqlConn.Close();
				}
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

			return sum%256;
		}

		protected int BCDchartoint(int c)
		{
			return ((c/16)*10) + (c%16);
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
					return value*2.23693629;
				case 2:
					return value*3.6;
				case 3:
					return value*1.94384449;
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
					return value*0.44704;
				case 1:
					return value;
				case 2:
					return value*1.60934;
				case 3:
					return value*0.868976;
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
					return value/2.23693629;
				case 2:
					return value/3.6F;
				case 3:
					return value/1.94384449;
				default:
					return 0;
			}
		}

		/// <summary>
		///  Converts windrun supplied in km to units in use
		/// </summary>
		/// <param name="value">Windrun in km</param>
		/// <returns>Wind in configured units</returns>
		public virtual double ConvertWindRunToDisplay(double value)
		{
			switch (cumulus.WindUnit)
			{
				case 0:
					return value;
				case 1:
					return value*0.621371192;
				case 2:
					return value;
				case 3:
					return value*0.539956803;
				default:
					return 0;
			}
		}

		/// <summary>
		///  Converts windrun supplied in units in use to km
		/// </summary>
		/// <param name="value">Windrun in configured units</param>
		/// <returns>Wind in km</returns>
		public virtual double ConvertWindRunToDB(double value)
		{
			switch (cumulus.WindUnit)
			{
				case 0:
					return value;
				case 1:
					return value/0.621371192;
				case 2:
					return value;
				case 3:
					return value/0.539956803;
				default:
					return 0;
			}
		}

		public double ConvertUserWindToKPH(double wind) // input is in WindUnit units, convert to km/h
		{
			switch (cumulus.WindUnit)
			{
					// m/s
				case 0:
					return wind*3.6;
					// mph
				case 1:
					return wind*1.609344;
					// kph
				case 2:
					return wind;
					// knots
				case 3:
					return wind*1.852;
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
			return bearing == 0 ? "-" : cumulus.compassp[(((bearing*100) + 1125)%36000)/2250];
		}

		public void StartLoop()
		{
			t = new Thread(Start) {IsBackground = true};
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
			var avg = 90 - (int) (RadToDeg(Math.Atan2(y, x)));
			if (avg < 0)
			{
				avg = 360 + avg;
			}

			return avg;
		}

		public void AddRecentDailyData(DateTime ts, double rain, double sunhours, double mintemp, double maxtemp, double avgtemp)
		{
			RecentDailyData recentDailyData = new RecentDailyData(ts,rain,sunhours,mintemp,maxtemp,avgtemp);

			RecentDailyDataList.Add(recentDailyData);
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
			double press, double speed, double gust, int avgdir, int wdir, int hum, int inhum, double solar, double smax, double uv, double feels)
		{
			var graphdata = new GraphData(ts, rain, raintoday, rrate, temp, dp, appt, chill, heat, intemp, press, speed, gust, avgdir, wdir, hum, inhum, solar, smax, uv, feels);

			GraphDataList.Add(graphdata);
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
			return deg*Math.PI/180;
		}

		public double RadToDeg(double rad)
		{
			return rad*180/Math.PI;
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

		public void RemoveOldRecentDailyData()
		{
			DateTime onemonthago = DateTime.Now.AddDays(-(cumulus.GraphDays+1));

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

				double trendval = (lastval - firstval)/3.0F;

				temptrendval = trendval;

				DoAlarm(temptrendval, cumulus.TempChangeAlarmValue, cumulus.TempChangeAlarmEnabled, true, ref cumulus.TempChangeUpAlarmState);
				DoAlarm(temptrendval, cumulus.TempChangeAlarmValue * -1, cumulus.TempChangeAlarmEnabled, false, ref cumulus.TempChangeDownAlarmState);

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

					var tempRainLastHour = trendval * cumulus.RainMult;

					if (tempRainLastHour > cumulus.EWmaxHourlyRain)
					{
						// ignore
					}
					else
					{
						RainLastHour = tempRainLastHour;

						if (RainLastHour > alltimerecarray[AT_hourlyrain].value)
							SetAlltime(AT_hourlyrain, RainLastHour, ts);

						CheckMonthlyAlltime(AT_hourlyrain, RainLastHour, true, ts);

						if (RainLastHour > highhourlyraintoday)
						{
							highhourlyraintoday = RainLastHour;
							highhourlyraintodaytime = ts;
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
				presstrendval = (lastval - firstval)/3.0;

				DoAlarm(presstrendval, cumulus.PressChangeAlarmValue, cumulus.PressChangeAlarmEnabled, true, ref cumulus.PressChangeUpAlarmState);
				DoAlarm(presstrendval, cumulus.PressChangeAlarmValue * -1, cumulus.PressChangeAlarmEnabled, false, ref cumulus.PressChangeDownAlarmState);

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
						if (timediffhours < 1.0/12.0)
						{
							timediffhours = 1.0/12.0;
						}

						double raindiff = Math.Round(fiveminutedata.Last().raincounter, cumulus.RainDPlaces) - Math.Round(fiveminutedata.First().raincounter, cumulus.RainDPlaces);
						//cumulus.LogMessage("first value = " + fiveminutedata.First().raincounter + " last value = " + fiveminutedata.Last().raincounter);
						//cumulus.LogMessage("raindiff = " + raindiff);

						var tempRainRate = (double) (raindiff/timediffhours);

						if (tempRainRate < 0)
						{
							tempRainRate = 0;
						}

						if (tempRainRate > cumulus.EWmaxRainRate)
						{
							// ignore
						}
						else
						{
							RainRate = tempRainRate;

							if (RainRate > alltimerecarray[AT_highrainrate].value)
								SetAlltime(AT_highrainrate, RainRate, ts);

							CheckMonthlyAlltime(AT_highrainrate, RainRate, true, ts);

							DoAlarm(RainRate, cumulus.HighRainRateAlarmValue, cumulus.HighRainRateAlarmEnabled, true, ref cumulus.HighRainRateAlarmState);

							if (RainRate > highraintoday)
							{
								highraintoday = RainRate;
								highraintodaytime = ts;
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

				RainLast24Hour = trendval*cumulus.RainMult;
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
			DominantWindBearingX += (minutes*averageSpeed*Math.Sin(DegToRad(averageBearing)));
			DominantWindBearingY += (minutes*averageSpeed*Math.Cos(DegToRad(averageBearing)));
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
			LoadRecentFromDataLogs(ts);
			LoadRecentDailyDataFromDayfile();
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

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								// process each record in the file
								string Line = sr.ReadLine();
								linenum++;
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

									AddRecentDataEntry(entrydate, speed, gust, wlatest, bearing, avgbearing, outsidetemp, chill, dewpoint, heat, hum, pressure, raintoday, solar, uv, raincounter);
									++numadded;
								}
							} while (!(sr.EndOfStream || entrydate >= dateto));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage("Error at line " + linenum + " of " + logFile + " : " + e.Message);
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
			cumulus.LogMessage("Loaded " + numadded + " entries to recent data list");
		}

		private void LoadGraphDataFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-cumulus.GraphHours);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								// process each record in the file
								string Line = sr.ReadLine();
								linenum++;
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
									var feels = st.Count == 28 ? Convert.ToDouble(st[27]) : 0.0;


									AddGraphDataEntry(entrydate, raintotal, raintoday, rainrate, outsidetemp, dewpoint, appt, chill, heat, insidetemp, pressure, speed, gust,
										avgbearing, bearing, hum, inhum, solar, solarmax, uv, feels);
								}
							} while (!(sr.EndOfStream || entrydate >= dateto));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage("Error at line " + linenum + " of " + logFile + " : " + e.Message);
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
			cumulus.LogMessage("Loaded " + GraphDataList.Count + " entries to graph data list");
		}

		private void LoadLast3HourFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-3);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								// process each record in the file
								string Line = sr.ReadLine();
								linenum++;
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
									nextwind = (nextwind + 1)%cumulus.MaxWindRecent;

									WindVec[nextwindvec].X = gust*Math.Sin(DegToRad(bearing));
									WindVec[nextwindvec].Y = gust*Math.Cos(DegToRad(bearing));
									WindVec[nextwindvec].Timestamp = entrydate;
									WindVec[nextwindvec].Bearing = Bearing; // savedBearing;
									nextwindvec = (nextwindvec + 1)%MaxWindRecent;
								}
							} while (!(sr.EndOfStream || entrydate >= dateto));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage("Error at line " + linenum + " of " + logFile + " : " + e.Message);
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
			cumulus.LogMessage("Loaded " + Last3HourDataList.Count + " entries to last 3 hour data list");
		}

		private void LoadLastHourFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddHours(-1);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile = cumulus.GetLogFileName(filedate);
			bool finished = false;

			while (!finished)
			{
				if (File.Exists(logFile))
				{
					int linenum = 0;

					try
					{
						using (var sr = new StreamReader(logFile))
						{
							do
							{
								// process each record in the file
								string Line = sr.ReadLine();
								linenum++;
								var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
								entrydate = ddmmyyhhmmStrToDate(st[0], st[1]);

								if (entrydate >= datefrom && entrydate <= dateto)
								{
									// entry is from required period
									var outsidetemp = Convert.ToDouble(st[2]);
									var raintotal = Convert.ToDouble(st[11]);

									AddLastHourDataEntry(entrydate, raintotal, outsidetemp);
								}
							} while (!(sr.EndOfStream || entrydate >= dateto));
						}
					}
					catch (Exception e)
					{
						cumulus.LogMessage("Error at line " + linenum + " of " + logFile + " : " + e.Message);
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
			cumulus.LogMessage("Loaded " + LastHourDataList.Count + " entries to last hour data list");
		}

		private void LoadRecentDailyDataFromDayfile()
		{
			if (File.Exists(cumulus.DayFile))
			{
				var datefrom = DateTime.Now.AddDays(-(cumulus.GraphDays+1));

				int linenum = 0;

				try
				{
					using (var sr = new StreamReader(cumulus.DayFile))
					{
						do
						{
							// process each record in the file
							string Line = sr.ReadLine();
							linenum++;
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
							}
						} while (!(sr.EndOfStream));
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("Error at line " + linenum + " of " + cumulus.DayFile + " : " + e.Message);
					cumulus.LogMessage("Please edit the file to correct the error");
				}
			}

			cumulus.LogMessage("Loaded " + RecentDailyDataList.Count + " entries to daily data list");
		}

		protected void UpdateStatusPanel(DateTime timestamp)
		{
			LastDataReadTimestamp = timestamp;
		}


		protected void UpdateMQTT()
		{
			if (cumulus.MQTTEnableDataUpdate)
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
			if (Math.Round(value, 3) < Math.Round(StartofdayET,3)) // change b3046
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
					LeafWetness1 = (int) value;
					break;
				case 2:
					LeafWetness2 = (int) value;
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
			double z_constant = (z_range/22.0F);

			bool z_summer = (z_month >= 4 && z_month <= 9); // true if "Summer"

			if (z_north)
			{
				// North hemisphere
				if (z_wind == "N")
				{
					z_hpa += 6F/100F*z_range;
				}
				else if (z_wind == "NNE")
				{
					z_hpa += 5F/100F*z_range;
				}
				else if (z_wind == "NE")
				{
					//			z_hpa += 4 ;
					z_hpa += 5F/100F*z_range;
				}
				else if (z_wind == "ENE")
				{
					z_hpa += 2F/100F*z_range;
				}
				else if (z_wind == "E")
				{
					z_hpa -= 0.5F/100F*z_range;
				}
				else if (z_wind == "ESE")
				{
					//			z_hpa -= 3 ;
					z_hpa -= 2F/100F*z_range;
				}
				else if (z_wind == "SE")
				{
					z_hpa -= 5F/100F*z_range;
				}
				else if (z_wind == "SSE")
				{
					z_hpa -= 8.5F/100F*z_range;
				}
				else if (z_wind == "S")
				{
					//			z_hpa -= 11 ;
					z_hpa -= 12F/100F*z_range;
				}
				else if (z_wind == "SSW")
				{
					z_hpa -= 10F/100F*z_range; //
				}
				else if (z_wind == "SW")
				{
					z_hpa -= 6F/100F*z_range;
				}
				else if (z_wind == "WSW")
				{
					z_hpa -= 4.5F/100F*z_range; //
				}
				else if (z_wind == "W")
				{
					z_hpa -= 3F/100F*z_range;
				}
				else if (z_wind == "WNW")
				{
					z_hpa -= 0.5F/100F*z_range;
				}
				else if (z_wind == "NW")
				{
					z_hpa += 1.5F/100*z_range;
				}
				else if (z_wind == "NNW")
				{
					z_hpa += 3F/100F*z_range;
				}
				if (z_summer)
				{
					// if Summer
					if (z_trend == 1)
					{
						// rising
						z_hpa += 7F/100F*z_range;
					}
					else if (z_trend == 2)
					{
						//	falling
						z_hpa -= 7F/100F*z_range;
					}
				}
			}
			else
			{
				// must be South hemisphere
				if (z_wind == "S")
				{
					z_hpa += 6F/100F*z_range;
				}
				else if (z_wind == "SSW")
				{
					z_hpa += 5F/100F*z_range;
				}
				else if (z_wind == "SW")
				{
					//			z_hpa += 4 ;
					z_hpa += 5F/100F*z_range;
				}
				else if (z_wind == "WSW")
				{
					z_hpa += 2F/100F*z_range;
				}
				else if (z_wind == "W")
				{
					z_hpa -= 0.5F/100F*z_range;
				}
				else if (z_wind == "WNW")
				{
					//			z_hpa -= 3 ;
					z_hpa -= 2F/100F*z_range;
				}
				else if (z_wind == "NW")
				{
					z_hpa -= 5F/100F*z_range;
				}
				else if (z_wind == "NNW")
				{
					z_hpa -= 8.5F/100*z_range;
				}
				else if (z_wind == "N")
				{
					//			z_hpa -= 11 ;
					z_hpa -= 12F/100F*z_range;
				}
				else if (z_wind == "NNE")
				{
					z_hpa -= 10F/100F*z_range; //
				}
				else if (z_wind == "NE")
				{
					z_hpa -= 6F/100F*z_range;
				}
				else if (z_wind == "ENE")
				{
					z_hpa -= 4.5F/100*z_range; //
				}
				else if (z_wind == "E")
				{
					z_hpa -= 3F/100F*z_range;
				}
				else if (z_wind == "ESE")
				{
					z_hpa -= 0.5F/100*z_range;
				}
				else if (z_wind == "SE")
				{
					z_hpa += 1.5F/100*z_range;
				}
				else if (z_wind == "SSE")
				{
					z_hpa += 3F/100F*z_range;
				}
				if (!z_summer)
				{
					// if Winter
					if (z_trend == 1)
					{
						// rising
						z_hpa += 7F/100F*z_range;
					}
					else if (z_trend == 2)
					{
						// falling
						z_hpa -= 7F/100F*z_range;
					}
				}
			} // END North / South

			if (z_hpa == z_baro_top)
			{
				z_hpa = z_baro_top - 1;
			}

			int z_option = (int) Math.Floor((z_hpa - z_baro_bottom)/z_constant);

			string z_output = "";
			if (z_option < 0)
			{
				z_option = 0;
				z_output = cumulus.exceptional + ", ";
			}
			if (z_option > 21)
			{
				z_option = 21;
				z_output = cumulus.exceptional + ", ";
			}

			if (z_trend == 1)
			{
				// rising
				Forecastnumber = cumulus.rise_options[z_option] + 1;
				z_output += cumulus.z_forecast[cumulus.rise_options[z_option]];
			}
			else if (z_trend == 2)
			{
				// falling
				Forecastnumber = cumulus.fall_options[z_option] + 1;
				z_output += cumulus.z_forecast[cumulus.fall_options[z_option]];
			}
			else
			{
				// must be "steady"
				Forecastnumber = cumulus.steady_options[z_option] + 1;
				z_output += cumulus.z_forecast[cumulus.steady_options[z_option]];
			}
			return z_output;
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

		public abstract void Stop();

		public void ReadAlltimeIniFile()
		{
			cumulus.LogMessage(Path.GetFullPath(cumulus.AlltimeIniFile));
			IniFile ini = new IniFile(cumulus.AlltimeIniFile);

			alltimerecarray[WeatherStation.AT_hightemp].data_type = WeatherStation.AT_hightemp;
			alltimerecarray[WeatherStation.AT_hightemp].value = ini.GetValue("Temperature", "hightempvalue", -999.0);
			alltimerecarray[WeatherStation.AT_hightemp].timestamp = ini.GetValue("Temperature", "hightemptime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowtemp].data_type = WeatherStation.AT_lowtemp;
			alltimerecarray[WeatherStation.AT_lowtemp].value = ini.GetValue("Temperature", "lowtempvalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowtemp].timestamp = ini.GetValue("Temperature", "lowtemptime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowchill].data_type = WeatherStation.AT_lowchill;
			alltimerecarray[WeatherStation.AT_lowchill].value = ini.GetValue("Temperature", "lowchillvalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowchill].timestamp = ini.GetValue("Temperature", "lowchilltime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highmintemp].data_type = WeatherStation.AT_highmintemp;
			alltimerecarray[WeatherStation.AT_highmintemp].value = ini.GetValue("Temperature", "highmintempvalue", -999.0);
			alltimerecarray[WeatherStation.AT_highmintemp].timestamp = ini.GetValue("Temperature", "highmintemptime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowmaxtemp].data_type = WeatherStation.AT_lowmaxtemp;
			alltimerecarray[WeatherStation.AT_lowmaxtemp].value = ini.GetValue("Temperature", "lowmaxtempvalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowmaxtemp].timestamp = ini.GetValue("Temperature", "lowmaxtemptime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highapptemp].data_type = WeatherStation.AT_highapptemp;
			alltimerecarray[WeatherStation.AT_highapptemp].value = ini.GetValue("Temperature", "highapptempvalue", -999.0);
			alltimerecarray[WeatherStation.AT_highapptemp].timestamp = ini.GetValue("Temperature", "highapptemptime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowapptemp].data_type = WeatherStation.AT_lowapptemp;
			alltimerecarray[WeatherStation.AT_lowapptemp].value = ini.GetValue("Temperature", "lowapptempvalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowapptemp].timestamp = ini.GetValue("Temperature", "lowapptemptime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highfeelslike].data_type = WeatherStation.AT_highfeelslike;
			alltimerecarray[WeatherStation.AT_highfeelslike].value = ini.GetValue("Temperature", "highfeelslikevalue", -999.0);
			alltimerecarray[WeatherStation.AT_highfeelslike].timestamp = ini.GetValue("Temperature", "highfeelsliketime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowfeelslike].data_type = WeatherStation.AT_lowfeelslike;
			alltimerecarray[WeatherStation.AT_lowfeelslike].value = ini.GetValue("Temperature", "lowfeelslikevalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowfeelslike].timestamp = ini.GetValue("Temperature", "lowfeelsliketime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highheatindex].data_type = WeatherStation.AT_highheatindex;
			alltimerecarray[WeatherStation.AT_highheatindex].value = ini.GetValue("Temperature", "highheatindexvalue", -999.0);
			alltimerecarray[WeatherStation.AT_highheatindex].timestamp = ini.GetValue("Temperature", "highheatindextime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highdewpoint].data_type = WeatherStation.AT_highdewpoint;
			alltimerecarray[WeatherStation.AT_highdewpoint].value = ini.GetValue("Temperature", "highdewpointvalue", -999.0);
			alltimerecarray[WeatherStation.AT_highdewpoint].timestamp = ini.GetValue("Temperature", "highdewpointtime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowdewpoint].data_type = WeatherStation.AT_lowdewpoint;
			alltimerecarray[WeatherStation.AT_lowdewpoint].value = ini.GetValue("Temperature", "lowdewpointvalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowdewpoint].timestamp = ini.GetValue("Temperature", "lowdewpointtime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highdailytemprange].data_type = WeatherStation.AT_highdailytemprange;
			alltimerecarray[WeatherStation.AT_highdailytemprange].value = ini.GetValue("Temperature", "hightemprangevalue", 0.0);
			alltimerecarray[WeatherStation.AT_highdailytemprange].timestamp = ini.GetValue("Temperature", "hightemprangetime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowdailytemprange].data_type = WeatherStation.AT_lowdailytemprange;
			alltimerecarray[WeatherStation.AT_lowdailytemprange].value = ini.GetValue("Temperature", "lowtemprangevalue", 999.0);
			alltimerecarray[WeatherStation.AT_lowdailytemprange].timestamp = ini.GetValue("Temperature", "lowtemprangetime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highwind].data_type = WeatherStation.AT_highwind;
			alltimerecarray[WeatherStation.AT_highwind].value = ini.GetValue("Wind", "highwindvalue", 0.0);
			alltimerecarray[WeatherStation.AT_highwind].timestamp = ini.GetValue("Wind", "highwindtime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highgust].data_type = WeatherStation.AT_highgust;
			alltimerecarray[WeatherStation.AT_highgust].value = ini.GetValue("Wind", "highgustvalue", 0.0);
			alltimerecarray[WeatherStation.AT_highgust].timestamp = ini.GetValue("Wind", "highgusttime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highwindrun].data_type = WeatherStation.AT_highwindrun;
			alltimerecarray[WeatherStation.AT_highwindrun].value = ini.GetValue("Wind", "highdailywindrunvalue", 0.0);
			alltimerecarray[WeatherStation.AT_highwindrun].timestamp = ini.GetValue("Wind", "highdailywindruntime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highrainrate].data_type = WeatherStation.AT_highrainrate;
			alltimerecarray[WeatherStation.AT_highrainrate].value = ini.GetValue("Rain", "highrainratevalue", 0.0);
			alltimerecarray[WeatherStation.AT_highrainrate].timestamp = ini.GetValue("Rain", "highrainratetime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_dailyrain].data_type = WeatherStation.AT_dailyrain;
			alltimerecarray[WeatherStation.AT_dailyrain].value = ini.GetValue("Rain", "highdailyrainvalue", 0.0);
			alltimerecarray[WeatherStation.AT_dailyrain].timestamp = ini.GetValue("Rain", "highdailyraintime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_hourlyrain].data_type = WeatherStation.AT_hourlyrain;
			alltimerecarray[WeatherStation.AT_hourlyrain].value = ini.GetValue("Rain", "highhourlyrainvalue", 0.0);
			alltimerecarray[WeatherStation.AT_hourlyrain].timestamp = ini.GetValue("Rain", "highhourlyraintime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_wetmonth].data_type = WeatherStation.AT_wetmonth;
			alltimerecarray[WeatherStation.AT_wetmonth].value = ini.GetValue("Rain", "highmonthlyrainvalue", 0.0);
			alltimerecarray[WeatherStation.AT_wetmonth].timestamp = ini.GetValue("Rain", "highmonthlyraintime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_longestdryperiod].data_type = WeatherStation.AT_longestdryperiod;
			alltimerecarray[WeatherStation.AT_longestdryperiod].value = ini.GetValue("Rain", "longestdryperiodvalue", 0);
			alltimerecarray[WeatherStation.AT_longestdryperiod].timestamp = ini.GetValue("Rain", "longestdryperiodtime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_longestwetperiod].data_type = WeatherStation.AT_longestwetperiod;
			alltimerecarray[WeatherStation.AT_longestwetperiod].value = ini.GetValue("Rain", "longestwetperiodvalue", 0);
			alltimerecarray[WeatherStation.AT_longestwetperiod].timestamp = ini.GetValue("Rain", "longestwetperiodtime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highpress].data_type = WeatherStation.AT_highpress;
			alltimerecarray[WeatherStation.AT_highpress].value = ini.GetValue("Pressure", "highpressurevalue", 0.0);
			alltimerecarray[WeatherStation.AT_highpress].timestamp = ini.GetValue("Pressure", "highpressuretime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowpress].data_type = WeatherStation.AT_lowpress;
			alltimerecarray[WeatherStation.AT_lowpress].value = ini.GetValue("Pressure", "lowpressurevalue", 9999.0);
			alltimerecarray[WeatherStation.AT_lowpress].timestamp = ini.GetValue("Pressure", "lowpressuretime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_highhumidity].data_type = WeatherStation.AT_highhumidity;
			alltimerecarray[WeatherStation.AT_highhumidity].value = ini.GetValue("Humidity", "highhumidityvalue", 0);
			alltimerecarray[WeatherStation.AT_highhumidity].timestamp = ini.GetValue("Humidity", "highhumiditytime", cumulus.defaultRecordTS);

			alltimerecarray[WeatherStation.AT_lowhumidity].data_type = WeatherStation.AT_lowhumidity;
			alltimerecarray[WeatherStation.AT_lowhumidity].value = ini.GetValue("Humidity", "lowhumidityvalue", 999);
			alltimerecarray[WeatherStation.AT_lowhumidity].timestamp = ini.GetValue("Humidity", "lowhumiditytime", cumulus.defaultRecordTS);

			cumulus.LogMessage("Alltime.ini file read");
		}

		public void WriteAlltimeIniFile()
		{
			try
			{
				IniFile ini = new IniFile(cumulus.AlltimeIniFile);

				ini.SetValue("Temperature", "hightempvalue", alltimerecarray[WeatherStation.AT_hightemp].value);
				ini.SetValue("Temperature", "hightemptime", alltimerecarray[WeatherStation.AT_hightemp].timestamp);
				ini.SetValue("Temperature", "lowtempvalue", alltimerecarray[WeatherStation.AT_lowtemp].value);
				ini.SetValue("Temperature", "lowtemptime", alltimerecarray[WeatherStation.AT_lowtemp].timestamp);
				ini.SetValue("Temperature", "lowchillvalue", alltimerecarray[WeatherStation.AT_lowchill].value);
				ini.SetValue("Temperature", "lowchilltime", alltimerecarray[WeatherStation.AT_lowchill].timestamp);
				ini.SetValue("Temperature", "highmintempvalue", alltimerecarray[WeatherStation.AT_highmintemp].value);
				ini.SetValue("Temperature", "highmintemptime", alltimerecarray[WeatherStation.AT_highmintemp].timestamp);
				ini.SetValue("Temperature", "lowmaxtempvalue", alltimerecarray[WeatherStation.AT_lowmaxtemp].value);
				ini.SetValue("Temperature", "lowmaxtemptime", alltimerecarray[WeatherStation.AT_lowmaxtemp].timestamp);
				ini.SetValue("Temperature", "highapptempvalue", alltimerecarray[WeatherStation.AT_highapptemp].value);
				ini.SetValue("Temperature", "highapptemptime", alltimerecarray[WeatherStation.AT_highapptemp].timestamp);
				ini.SetValue("Temperature", "lowapptempvalue", alltimerecarray[WeatherStation.AT_lowapptemp].value);
				ini.SetValue("Temperature", "lowapptemptime", alltimerecarray[WeatherStation.AT_lowapptemp].timestamp);
				ini.SetValue("Temperature", "highfeelslikevalue", alltimerecarray[WeatherStation.AT_highfeelslike].value);
				ini.SetValue("Temperature", "highfeelsliketime", alltimerecarray[WeatherStation.AT_highfeelslike].timestamp);
				ini.SetValue("Temperature", "lowfeelslikevalue", alltimerecarray[WeatherStation.AT_lowfeelslike].value);
				ini.SetValue("Temperature", "lowfeelsliketime", alltimerecarray[WeatherStation.AT_lowfeelslike].timestamp);
				ini.SetValue("Temperature", "highheatindexvalue", alltimerecarray[WeatherStation.AT_highheatindex].value);
				ini.SetValue("Temperature", "highheatindextime", alltimerecarray[WeatherStation.AT_highheatindex].timestamp);
				ini.SetValue("Temperature", "highdewpointvalue", alltimerecarray[WeatherStation.AT_highdewpoint].value);
				ini.SetValue("Temperature", "highdewpointtime", alltimerecarray[WeatherStation.AT_highdewpoint].timestamp);
				ini.SetValue("Temperature", "lowdewpointvalue", alltimerecarray[WeatherStation.AT_lowdewpoint].value);
				ini.SetValue("Temperature", "lowdewpointtime", alltimerecarray[WeatherStation.AT_lowdewpoint].timestamp);
				ini.SetValue("Temperature", "hightemprangevalue", alltimerecarray[WeatherStation.AT_highdailytemprange].value);
				ini.SetValue("Temperature", "hightemprangetime", alltimerecarray[WeatherStation.AT_highdailytemprange].timestamp);
				ini.SetValue("Temperature", "lowtemprangevalue", alltimerecarray[WeatherStation.AT_lowdailytemprange].value);
				ini.SetValue("Temperature", "lowtemprangetime", alltimerecarray[WeatherStation.AT_lowdailytemprange].timestamp);
				ini.SetValue("Wind", "highwindvalue", alltimerecarray[WeatherStation.AT_highwind].value);
				ini.SetValue("Wind", "highwindtime", alltimerecarray[WeatherStation.AT_highwind].timestamp);
				ini.SetValue("Wind", "highgustvalue", alltimerecarray[WeatherStation.AT_highgust].value);
				ini.SetValue("Wind", "highgusttime", alltimerecarray[WeatherStation.AT_highgust].timestamp);
				ini.SetValue("Wind", "highdailywindrunvalue", alltimerecarray[WeatherStation.AT_highwindrun].value);
				ini.SetValue("Wind", "highdailywindruntime", alltimerecarray[WeatherStation.AT_highwindrun].timestamp);
				ini.SetValue("Rain", "highrainratevalue", alltimerecarray[WeatherStation.AT_highrainrate].value);
				ini.SetValue("Rain", "highrainratetime", alltimerecarray[WeatherStation.AT_highrainrate].timestamp);
				ini.SetValue("Rain", "highdailyrainvalue", alltimerecarray[WeatherStation.AT_dailyrain].value);
				ini.SetValue("Rain", "highdailyraintime", alltimerecarray[WeatherStation.AT_dailyrain].timestamp);
				ini.SetValue("Rain", "highhourlyrainvalue", alltimerecarray[WeatherStation.AT_hourlyrain].value);
				ini.SetValue("Rain", "highhourlyraintime", alltimerecarray[WeatherStation.AT_hourlyrain].timestamp);
				ini.SetValue("Rain", "highmonthlyrainvalue", alltimerecarray[WeatherStation.AT_wetmonth].value);
				ini.SetValue("Rain", "highmonthlyraintime", alltimerecarray[WeatherStation.AT_wetmonth].timestamp);
				ini.SetValue("Rain", "longestdryperiodvalue", alltimerecarray[WeatherStation.AT_longestdryperiod].value);
				ini.SetValue("Rain", "longestdryperiodtime", alltimerecarray[WeatherStation.AT_longestdryperiod].timestamp);
				ini.SetValue("Rain", "longestwetperiodvalue", alltimerecarray[WeatherStation.AT_longestwetperiod].value);
				ini.SetValue("Rain", "longestwetperiodtime", alltimerecarray[WeatherStation.AT_longestwetperiod].timestamp);
				ini.SetValue("Pressure", "highpressurevalue", alltimerecarray[WeatherStation.AT_highpress].value);
				ini.SetValue("Pressure", "highpressuretime", alltimerecarray[WeatherStation.AT_highpress].timestamp);
				ini.SetValue("Pressure", "lowpressurevalue", alltimerecarray[WeatherStation.AT_lowpress].value);
				ini.SetValue("Pressure", "lowpressuretime", alltimerecarray[WeatherStation.AT_lowpress].timestamp);
				ini.SetValue("Humidity", "highhumidityvalue", alltimerecarray[WeatherStation.AT_highhumidity].value);
				ini.SetValue("Humidity", "highhumiditytime", alltimerecarray[WeatherStation.AT_highhumidity].timestamp);
				ini.SetValue("Humidity", "lowhumidityvalue", alltimerecarray[WeatherStation.AT_lowhumidity].value);
				ini.SetValue("Humidity", "lowhumiditytime", alltimerecarray[WeatherStation.AT_lowhumidity].timestamp);

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

				monthlyrecarray[WeatherStation.AT_hightemp, month].data_type = WeatherStation.AT_hightemp;
				monthlyrecarray[WeatherStation.AT_hightemp, month].value = ini.GetValue("Temperature" + monthstr, "hightempvalue", -999.0);
				monthlyrecarray[WeatherStation.AT_hightemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "hightemptime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowtemp, month].data_type = WeatherStation.AT_lowtemp;
				monthlyrecarray[WeatherStation.AT_lowtemp, month].value = ini.GetValue("Temperature" + monthstr, "lowtempvalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowtemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowtemptime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowchill, month].data_type = WeatherStation.AT_lowchill;
				monthlyrecarray[WeatherStation.AT_lowchill, month].value = ini.GetValue("Temperature" + monthstr, "lowchillvalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowchill, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowchilltime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highmintemp, month].data_type = WeatherStation.AT_highmintemp;
				monthlyrecarray[WeatherStation.AT_highmintemp, month].value = ini.GetValue("Temperature" + monthstr, "highmintempvalue", -999.0);
				monthlyrecarray[WeatherStation.AT_highmintemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "highmintemptime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].data_type = WeatherStation.AT_lowmaxtemp;
				monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].value = ini.GetValue("Temperature" + monthstr, "lowmaxtempvalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowmaxtemptime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highapptemp, month].data_type = WeatherStation.AT_highapptemp;
				monthlyrecarray[WeatherStation.AT_highapptemp, month].value = ini.GetValue("Temperature" + monthstr, "highapptempvalue", -999.0);
				monthlyrecarray[WeatherStation.AT_highapptemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "highapptemptime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowapptemp, month].data_type = WeatherStation.AT_lowapptemp;
				monthlyrecarray[WeatherStation.AT_lowapptemp, month].value = ini.GetValue("Temperature" + monthstr, "lowapptempvalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowapptemp, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowapptemptime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highfeelslike, month].data_type = WeatherStation.AT_highfeelslike;
				monthlyrecarray[WeatherStation.AT_highfeelslike, month].value = ini.GetValue("Temperature" + monthstr, "highfeelslikevalue", -999.0);
				monthlyrecarray[WeatherStation.AT_highfeelslike, month].timestamp = ini.GetValue("Temperature" + monthstr, "highfeelsliketime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowfeelslike, month].data_type = WeatherStation.AT_lowfeelslike;
				monthlyrecarray[WeatherStation.AT_lowfeelslike, month].value = ini.GetValue("Temperature" + monthstr, "lowfeelslikevalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowfeelslike, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowfeelsliketime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highheatindex, month].data_type = WeatherStation.AT_highheatindex;
				monthlyrecarray[WeatherStation.AT_highheatindex, month].value = ini.GetValue("Temperature" + monthstr, "highheatindexvalue", -999.0);
				monthlyrecarray[WeatherStation.AT_highheatindex, month].timestamp = ini.GetValue("Temperature" + monthstr, "highheatindextime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highdewpoint, month].data_type = WeatherStation.AT_highdewpoint;
				monthlyrecarray[WeatherStation.AT_highdewpoint, month].value = ini.GetValue("Temperature" + monthstr, "highdewpointvalue", -999.0);
				monthlyrecarray[WeatherStation.AT_highdewpoint, month].timestamp = ini.GetValue("Temperature" + monthstr, "highdewpointtime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowdewpoint, month].data_type = WeatherStation.AT_lowdewpoint;
				monthlyrecarray[WeatherStation.AT_lowdewpoint, month].value = ini.GetValue("Temperature" + monthstr, "lowdewpointvalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowdewpoint, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowdewpointtime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highdailytemprange, month].data_type = WeatherStation.AT_highdailytemprange;
				monthlyrecarray[WeatherStation.AT_highdailytemprange, month].value = ini.GetValue("Temperature" + monthstr, "hightemprangevalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highdailytemprange, month].timestamp = ini.GetValue("Temperature" + monthstr, "hightemprangetime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].data_type = WeatherStation.AT_lowdailytemprange;
				monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].value = ini.GetValue("Temperature" + monthstr, "lowtemprangevalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].timestamp = ini.GetValue("Temperature" + monthstr, "lowtemprangetime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highwind, month].data_type = WeatherStation.AT_highwind;
				monthlyrecarray[WeatherStation.AT_highwind, month].value = ini.GetValue("Wind" + monthstr, "highwindvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highwind, month].timestamp = ini.GetValue("Wind" + monthstr, "highwindtime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highgust, month].data_type = WeatherStation.AT_highgust;
				monthlyrecarray[WeatherStation.AT_highgust, month].value = ini.GetValue("Wind" + monthstr, "highgustvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highgust, month].timestamp = ini.GetValue("Wind" + monthstr, "highgusttime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highwindrun, month].data_type = WeatherStation.AT_highwindrun;
				monthlyrecarray[WeatherStation.AT_highwindrun, month].value = ini.GetValue("Wind" + monthstr, "highdailywindrunvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highwindrun, month].timestamp = ini.GetValue("Wind" + monthstr, "highdailywindruntime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highrainrate, month].data_type = WeatherStation.AT_highrainrate;
				monthlyrecarray[WeatherStation.AT_highrainrate, month].value = ini.GetValue("Rain" + monthstr, "highrainratevalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highrainrate, month].timestamp = ini.GetValue("Rain" + monthstr, "highrainratetime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_dailyrain, month].data_type = WeatherStation.AT_dailyrain;
				monthlyrecarray[WeatherStation.AT_dailyrain, month].value = ini.GetValue("Rain" + monthstr, "highdailyrainvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_dailyrain, month].timestamp = ini.GetValue("Rain" + monthstr, "highdailyraintime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_hourlyrain, month].data_type = WeatherStation.AT_hourlyrain;
				monthlyrecarray[WeatherStation.AT_hourlyrain, month].value = ini.GetValue("Rain" + monthstr, "highhourlyrainvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_hourlyrain, month].timestamp = ini.GetValue("Rain" + monthstr, "highhourlyraintime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_wetmonth, month].data_type = WeatherStation.AT_wetmonth;
				monthlyrecarray[WeatherStation.AT_wetmonth, month].value = ini.GetValue("Rain" + monthstr, "highmonthlyrainvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_wetmonth, month].timestamp = ini.GetValue("Rain" + monthstr, "highmonthlyraintime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_longestdryperiod, month].data_type = WeatherStation.AT_longestdryperiod;
				monthlyrecarray[WeatherStation.AT_longestdryperiod, month].value = ini.GetValue("Rain" + monthstr, "longestdryperiodvalue", 0);
				monthlyrecarray[WeatherStation.AT_longestdryperiod, month].timestamp = ini.GetValue("Rain" + monthstr, "longestdryperiodtime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_longestwetperiod, month].data_type = WeatherStation.AT_longestwetperiod;
				monthlyrecarray[WeatherStation.AT_longestwetperiod, month].value = ini.GetValue("Rain" + monthstr, "longestwetperiodvalue", 0);
				monthlyrecarray[WeatherStation.AT_longestwetperiod, month].timestamp = ini.GetValue("Rain" + monthstr, "longestwetperiodtime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highpress, month].data_type = WeatherStation.AT_highpress;
				monthlyrecarray[WeatherStation.AT_highpress, month].value = ini.GetValue("Pressure" + monthstr, "highpressurevalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highpress, month].timestamp = ini.GetValue("Pressure" + monthstr, "highpressuretime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowpress, month].data_type = WeatherStation.AT_lowpress;
				monthlyrecarray[WeatherStation.AT_lowpress, month].value = ini.GetValue("Pressure" + monthstr, "lowpressurevalue", 9999.0);
				monthlyrecarray[WeatherStation.AT_lowpress, month].timestamp = ini.GetValue("Pressure" + monthstr, "lowpressuretime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_highhumidity, month].data_type = WeatherStation.AT_highhumidity;
				monthlyrecarray[WeatherStation.AT_highhumidity, month].value = ini.GetValue("Humidity" + monthstr, "highhumidityvalue", 0.0);
				monthlyrecarray[WeatherStation.AT_highhumidity, month].timestamp = ini.GetValue("Humidity" + monthstr, "highhumiditytime", cumulus.defaultRecordTS);

				monthlyrecarray[WeatherStation.AT_lowhumidity, month].data_type = WeatherStation.AT_lowhumidity;
				monthlyrecarray[WeatherStation.AT_lowhumidity, month].value = ini.GetValue("Humidity" + monthstr, "lowhumidityvalue", 999.0);
				monthlyrecarray[WeatherStation.AT_lowhumidity, month].timestamp = ini.GetValue("Humidity" + monthstr, "lowhumiditytime", cumulus.defaultRecordTS);
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

					ini.SetValue("Temperature" + monthstr, "hightempvalue", monthlyrecarray[WeatherStation.AT_hightemp, month].value);
					ini.SetValue("Temperature" + monthstr, "hightemptime", monthlyrecarray[WeatherStation.AT_hightemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowtempvalue", monthlyrecarray[WeatherStation.AT_lowtemp, month].value);
					ini.SetValue("Temperature" + monthstr, "lowtemptime", monthlyrecarray[WeatherStation.AT_lowtemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowchillvalue", monthlyrecarray[WeatherStation.AT_lowchill, month].value);
					ini.SetValue("Temperature" + monthstr, "lowchilltime", monthlyrecarray[WeatherStation.AT_lowchill, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highmintempvalue", monthlyrecarray[WeatherStation.AT_highmintemp, month].value);
					ini.SetValue("Temperature" + monthstr, "highmintemptime", monthlyrecarray[WeatherStation.AT_highmintemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowmaxtempvalue", monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].value);
					ini.SetValue("Temperature" + monthstr, "lowmaxtemptime", monthlyrecarray[WeatherStation.AT_lowmaxtemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highapptempvalue", monthlyrecarray[WeatherStation.AT_highapptemp, month].value);
					ini.SetValue("Temperature" + monthstr, "highapptemptime", monthlyrecarray[WeatherStation.AT_highapptemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowapptempvalue", monthlyrecarray[WeatherStation.AT_lowapptemp, month].value);
					ini.SetValue("Temperature" + monthstr, "lowapptemptime", monthlyrecarray[WeatherStation.AT_lowapptemp, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highfeelslikevalue", monthlyrecarray[WeatherStation.AT_highfeelslike, month].value);
					ini.SetValue("Temperature" + monthstr, "highfeelsliketime", monthlyrecarray[WeatherStation.AT_highfeelslike, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowfeelslikevalue", monthlyrecarray[WeatherStation.AT_lowfeelslike, month].value);
					ini.SetValue("Temperature" + monthstr, "lowfeelsliketime", monthlyrecarray[WeatherStation.AT_lowfeelslike, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highheatindexvalue", monthlyrecarray[WeatherStation.AT_highheatindex, month].value);
					ini.SetValue("Temperature" + monthstr, "highheatindextime", monthlyrecarray[WeatherStation.AT_highheatindex, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "highdewpointvalue", monthlyrecarray[WeatherStation.AT_highdewpoint, month].value);
					ini.SetValue("Temperature" + monthstr, "highdewpointtime", monthlyrecarray[WeatherStation.AT_highdewpoint, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowdewpointvalue", monthlyrecarray[WeatherStation.AT_lowdewpoint, month].value);
					ini.SetValue("Temperature" + monthstr, "lowdewpointtime", monthlyrecarray[WeatherStation.AT_lowdewpoint, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "hightemprangevalue", monthlyrecarray[WeatherStation.AT_highdailytemprange, month].value);
					ini.SetValue("Temperature" + monthstr, "hightemprangetime", monthlyrecarray[WeatherStation.AT_highdailytemprange, month].timestamp);
					ini.SetValue("Temperature" + monthstr, "lowtemprangevalue", monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].value);
					ini.SetValue("Temperature" + monthstr, "lowtemprangetime", monthlyrecarray[WeatherStation.AT_lowdailytemprange, month].timestamp);
					ini.SetValue("Wind" + monthstr, "highwindvalue", monthlyrecarray[WeatherStation.AT_highwind, month].value);
					ini.SetValue("Wind" + monthstr, "highwindtime", monthlyrecarray[WeatherStation.AT_highwind, month].timestamp);
					ini.SetValue("Wind" + monthstr, "highgustvalue", monthlyrecarray[WeatherStation.AT_highgust, month].value);
					ini.SetValue("Wind" + monthstr, "highgusttime", monthlyrecarray[WeatherStation.AT_highgust, month].timestamp);
					ini.SetValue("Wind" + monthstr, "highdailywindrunvalue", monthlyrecarray[WeatherStation.AT_highwindrun, month].value);
					ini.SetValue("Wind" + monthstr, "highdailywindruntime", monthlyrecarray[WeatherStation.AT_highwindrun, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highrainratevalue", monthlyrecarray[WeatherStation.AT_highrainrate, month].value);
					ini.SetValue("Rain" + monthstr, "highrainratetime", monthlyrecarray[WeatherStation.AT_highrainrate, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highdailyrainvalue", monthlyrecarray[WeatherStation.AT_dailyrain, month].value);
					ini.SetValue("Rain" + monthstr, "highdailyraintime", monthlyrecarray[WeatherStation.AT_dailyrain, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highhourlyrainvalue", monthlyrecarray[WeatherStation.AT_hourlyrain, month].value);
					ini.SetValue("Rain" + monthstr, "highhourlyraintime", monthlyrecarray[WeatherStation.AT_hourlyrain, month].timestamp);
					ini.SetValue("Rain" + monthstr, "highmonthlyrainvalue", monthlyrecarray[WeatherStation.AT_wetmonth, month].value);
					ini.SetValue("Rain" + monthstr, "highmonthlyraintime", monthlyrecarray[WeatherStation.AT_wetmonth, month].timestamp);
					ini.SetValue("Rain" + monthstr, "longestdryperiodvalue", monthlyrecarray[WeatherStation.AT_longestdryperiod, month].value);
					ini.SetValue("Rain" + monthstr, "longestdryperiodtime", monthlyrecarray[WeatherStation.AT_longestdryperiod, month].timestamp);
					ini.SetValue("Rain" + monthstr, "longestwetperiodvalue", monthlyrecarray[WeatherStation.AT_longestwetperiod, month].value);
					ini.SetValue("Rain" + monthstr, "longestwetperiodtime", monthlyrecarray[WeatherStation.AT_longestwetperiod, month].timestamp);
					ini.SetValue("Pressure" + monthstr, "highpressurevalue", monthlyrecarray[WeatherStation.AT_highpress, month].value);
					ini.SetValue("Pressure" + monthstr, "highpressuretime", monthlyrecarray[WeatherStation.AT_highpress, month].timestamp);
					ini.SetValue("Pressure" + monthstr, "lowpressurevalue", monthlyrecarray[WeatherStation.AT_lowpress, month].value);
					ini.SetValue("Pressure" + monthstr, "lowpressuretime", monthlyrecarray[WeatherStation.AT_lowpress, month].timestamp);
					ini.SetValue("Humidity" + monthstr, "highhumidityvalue", monthlyrecarray[WeatherStation.AT_highhumidity, month].value);
					ini.SetValue("Humidity" + monthstr, "highhumiditytime", monthlyrecarray[WeatherStation.AT_highhumidity, month].timestamp);
					ini.SetValue("Humidity" + monthstr, "lowhumidityvalue", monthlyrecarray[WeatherStation.AT_lowhumidity, month].value);
					ini.SetValue("Humidity" + monthstr, "lowhumiditytime", monthlyrecarray[WeatherStation.AT_lowhumidity, month].timestamp);
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
			pwstring = cumulus.WCloudKey;

			StringBuilder sb = new StringBuilder($"http://api.weathercloud.net/v01/set?wid={cumulus.WCloudWid}&key={cumulus.WCloudKey}");

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
			sb.Append("&wspd=" + (int)Math.Round(ConvertUserWindToMS(WindLatest)*10));
			sb.Append("&wspdhi=" + (int)Math.Round(ConvertUserWindToMS(RecentMaxGust)*10));
			sb.Append("&wspdavg=" + (int)Math.Round(ConvertUserWindToMS(WindAverage)*10));

			// Wind Direction
			sb.Append("&wdir=" + Bearing);
			sb.Append("&wdiravg=" + AvgBearing);

			// Pressure
			sb.Append("&bar=" + (int)Math.Round(ConvertUserPressToMB(Pressure)*10));

			// rain
			sb.Append("&rain=" + (int)Math.Round(ConvertUserRainToMM(RainToday) * 10));
			sb.Append("&rainrate=" + (int)Math.Round(ConvertUserRainToMM(RainRate) * 10));

			// ET
			if (cumulus.SendSolarToWCloud && cumulus.Manufacturer == cumulus.DAVIS)
			{
				sb.Append("&et=" + (int)Math.Round(ConvertUserRainToMM(ET) * 10));
			}

			// solar
			if (cumulus.SendSolarToWCloud)
			{
				sb.Append("&solarrad=" + (int)Math.Round(SolarRad * 10));
			}

			// uv
			if (cumulus.SendUVToWCloud)
			{
				sb.Append("&uvi=" + (int)Math.Round(UV*10));
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
			sb.Append(sep+sep+sep); // 11/12/13 - condition and warning, snow height
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
				sb.Append(sep+sep);
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

			sb.Append(ConvertUserTempToC(LowTempToday).ToString("F1", InvC) + sep); // 26

			sb.Append(ConvertUserTempToC(AvgTemp).ToString("F1", InvC) + sep); // 27

			sb.Append(ConvertUserTempToC(HighTempToday).ToString("F1", InvC) + sep); // 28

			sb.Append(ConvertUserTempToC(LowTempThisMonth).ToString("F1", InvC) + sep); // 29

			sb.Append(sep); // 30 avg temp this month

			sb.Append(ConvertUserTempToC(HighTempThisMonth).ToString("F1", InvC) + sep); // 31

			sb.Append(ConvertUserTempToC(LowTempThisYear).ToString("F1", InvC) + sep); // 32

			sb.Append(sep); // 33 avg temp this year

			sb.Append(ConvertUserTempToC(HighTempThisYear).ToString("F1", InvC) + sep); // 34

			sb.Append(lowhumiditytoday + sep); // 35

			sb.Append(sep); // 36 avg hum today

			sb.Append(highhumiditytoday + sep); // 37

			sb.Append(LowHumidityThisMonth + sep); // 38

			sb.Append(sep); // 39 avg hum this month

			sb.Append(HighHumidityThisMonth + sep); // 40

			sb.Append(LowHumidityThisYear + sep); // 41

			sb.Append(sep); // 42 avg hum this year

			sb.Append(HighHumidityThisYear + sep); // 43

			sb.Append(ConvertUserPressToMB(lowpresstoday).ToString("F1", InvC) + sep); // 44

			sb.Append(sep); // 45 avg press today

			sb.Append(ConvertUserPressToMB(highpresstoday).ToString("F1", InvC) + sep); // 46

			sb.Append(ConvertUserPressToMB(LowPressThisMonth).ToString("F1", InvC) + sep); // 47

			sb.Append(sep); // 48 avg press this month

			sb.Append(ConvertUserPressToMB(HighPressThisMonth).ToString("F1", InvC) + sep); // 49

			sb.Append(ConvertUserPressToMB(LowPressThisYear).ToString("F1", InvC) + sep); // 50

			sb.Append(sep); // 51 avg press this year

			sb.Append(ConvertUserPressToMB(HighPressThisYear).ToString("F1", InvC) + sep); // 52

			sb.Append(sep + sep); // 53/54 min/avg wind today

			sb.Append(ConvertUserWindToKPH(highwindtoday).ToString("F1", InvC) + sep); // 55

			sb.Append(sep + sep); // 56/57 min/avg wind this month

			sb.Append(ConvertUserWindToKPH(HighWindThisMonth).ToString("F1", InvC) + sep); // 58

			sb.Append(sep + sep); // 59/60 min/avg wind this year

			sb.Append(ConvertUserWindToKPH(HighWindThisYear).ToString("F1", InvC) + sep); // 61

			sb.Append(sep + sep); // 62/63 min/avg gust today

			sb.Append(ConvertUserWindToKPH(highgusttoday).ToString("F1", InvC) + sep); // 64

			sb.Append(sep + sep); // 65/66 min/avg gust this month

			sb.Append(ConvertUserWindToKPH(HighGustThisMonth).ToString("F1", InvC) + sep); // 67

			sb.Append(sep + sep); // 68/69 min/avg gust this year

			sb.Append(ConvertUserWindToKPH(HighGustThisYear).ToString("F1", InvC) + sep); // 70

			sb.Append(sep + sep + sep); // 71/72/73 avg wind bearing today/month/year

			sb.Append(ConvertUserRainToMM(RainLast24Hour).ToString("F1", InvC) + sep); // 74

			sb.Append(ConvertUserRainToMM(RainMonth).ToString("F1", InvC) + sep); // 75

			sb.Append(ConvertUserRainToMM(RainYear).ToString("F1", InvC) + sep); // 76

			sb.Append(sep); // 77 avg rain rate today

			sb.Append(ConvertUserRainToMM(highraintoday).ToString("F1", InvC) + sep); // 78

			sb.Append(sep); // 79 avg rain rate this month

			sb.Append(ConvertUserRainToMM(HighRainThisMonth).ToString("F1", InvC) + sep); // 80

			sb.Append(sep); // 81 avg rain rate this year

			sb.Append(ConvertUserRainToMM(HighRainThisYear).ToString("F1", InvC) + sep); // 82

			sb.Append(sep); // 83 avg solar today

			sb.Append(HighSolarToday.ToString("F1", InvC)); // 84

			sb.Append(sep); // 85 avg solar this month

			sb.Append(sep); // 86 high solar this month

			sb.Append(sep); // 87 avg solar this year

			sb.Append(sep); // 88 high solar this year

			sb.Append(sep); // 89 avg uv today

			sb.Append(HighUVToday.ToString("F1", InvC)); // 90

			sb.Append(sep); // 91 avg uv this month

			sb.Append(sep); // 92 high uv this month

			sb.Append(sep); // 93 avg uv this year

			sb.Append(sep); // 94 high uv this year

			sb.Append(sep + sep + sep + sep + sep + sep); // 95/96/97/98/99/100 avg/max lux today/month/year

			sb.Append(sep + sep); // 101/102 sun hours this month/year

			sb.Append(sep + sep + sep + sep + sep + sep + sep + sep + sep); //103-111 min/avg/max Soil temp today/month/year

			return sb.ToString();
		}

		public string GetWundergroundURL(out string pwstring, DateTime timestamp, bool catchup)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			string URL;
			if (cumulus.WundRapidFireEnabled && !catchup)
			{
				URL = "http://rtupdate.wunderground.com/weatherstation/updateweatherstation.php?ID=";
			}
			else
			{
				URL = "http://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?ID=";
			}

			pwstring = "&PASSWORD=" + cumulus.WundPW;
			URL = URL + cumulus.WundID + pwstring + "&dateutc=" + dateUTC;
			string Data = "";
			if (cumulus.WundSendAverage)
			{
				// send average speed and bearing
				Data += "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);
			}
			else
			{
				// send "instantaneous" speed (i.e. latest) and bearing
				Data += "&winddir=" + Bearing + "&windspeedmph=" + WindMPHStr(WindLatest);
			}
			Data += "&windgustmph=" + WindMPHStr(RecentMaxGust);
			// may not strictly be a 2 min average!
			Data += "&windspdmph_avg2m=" + WindMPHStr(WindAverage);
			Data += "&winddir_avg2m=" + AvgBearing;
			Data += "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";
			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data += RainINstr(RainToday);
			}
			else
			{
				Data += RainINstr(RainSinceMidnight);
			}
			Data = Data + "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);
			if (cumulus.SendUVToWund)
				Data += "&UV=" + UV.ToString(cumulus.UVFormat);
			if (cumulus.SendSRToWund)
				Data += "&solarradiation=" + SolarRad.ToString("F0");
			if (cumulus.SendIndoorToWund)
				Data += "&indoortempf=" + TempFstr(IndoorTemperature) + "&indoorhumidity=" + IndoorHumidity;
			// Davis soil and leaf sensors
			if (cumulus.SendSoilTemp1ToWund)
				Data += "&soiltempf=" + TempFstr(SoilTemp1);
			if (cumulus.SendSoilTemp2ToWund)
				Data += "&soiltempf2=" + TempFstr(SoilTemp2);
			if (cumulus.SendSoilTemp3ToWund)
				Data += "&soiltempf3=" + TempFstr(SoilTemp3);
			if (cumulus.SendSoilTemp4ToWund)
				Data += "&soiltempf4=" + TempFstr(SoilTemp4);

			if (cumulus.SendSoilMoisture1ToWund)
				Data += "&soilmoisture=" + SoilMoisture1;
			if (cumulus.SendSoilMoisture2ToWund)
				Data += "&soilmoisture2=" + SoilMoisture2;
			if (cumulus.SendSoilMoisture3ToWund)
				Data += "&soilmoisture3=" + SoilMoisture3;
			if (cumulus.SendSoilMoisture4ToWund)
				Data += "&soilmoisture4=" + SoilMoisture4;

			if (cumulus.SendLeafWetness1ToWund)
				Data += "&leafwetness=" + LeafWetness1;
			if (cumulus.SendLeafWetness2ToWund)
				Data += "&leafwetness2=" + LeafWetness2;

			Data += "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";
			if (cumulus.WundRapidFireEnabled && !catchup)
				Data += "&realtime=1&rtfreq=5";
			//MainForm.SystemLog.WriteLogString(TimeToStr(Now) + " Updating Wunderground");
			Data = cumulus.ReplaceCommas(Data);
			URL += Data;

			return URL;
		}

		public string GetWindyURL(out string apistring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH':'mm':'ss");
			string URL;

			apistring = cumulus.WindyApiKey;

			URL = "https://stations.windy.com/pws/update/";

			URL = URL + cumulus.WindyApiKey + "?station=" + cumulus.WindyStationIdx + "&dateutc=" + dateUTC;
			string Data = "";
			Data = Data + "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);
			Data = Data + "&windgustmph=" + WindMPHStr(RecentMaxGust);
			Data = Data + "&tempf=" + TempFstr(OutdoorTemperature);
			Data = Data + "&rainin=" + RainINstr(RainLastHour);
			Data = Data + "&baromin=" + PressINstr(Pressure);
			Data = Data + "&dewptf=" + TempFstr(OutdoorDewpoint);
			Data = Data + "&humidity=" + OutdoorHumidity;

			if (cumulus.WindySendUV)
				Data = Data + "&uv=" + UV.ToString(cumulus.UVFormat);
			if (cumulus.WindySendSolar)
				Data = Data + "&solarradiation=" + SolarRad.ToString("F0");

			Data = cumulus.ReplaceCommas(Data);
			URL += Data;

			return URL;
		}

		private string PressINstr(double pressure)
		{
			var pressIN = ConvertUserPressToIN(pressure);

			return pressIN.ToString("F3");
		}

		private string WindMPHStr(double wind)
		{
			var windMPH = ConvertUserWindToMPH(wind);

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
			string URL = "http://www.pwsweather.com/pwsupdate/pwsupdate.php?ID=";

			pwstring = "&PASSWORD=" + cumulus.PWSPW;
			URL += cumulus.PWSID + pwstring + "&dateutc=" + dateUTC;
			string Data = "";

			// send average speed and bearing
			Data += "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);

			Data += "&windgustmph=" + WindMPHStr(RecentMaxGust);

			Data += "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";

			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data += RainINstr(RainToday);
			}
			else
			{
				Data += RainINstr(RainSinceMidnight);
			}

			Data += "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);

			if (cumulus.SendUVToPWS)
			{
				Data += "&UV=" + UV.ToString(cumulus.UVFormat);
			}

			if (cumulus.SendSRToPWS)
			{
				Data += "&solarradiation=" + SolarRad.ToString("F0");
			}

			Data += "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";

			Data = cumulus.ReplaceCommas(Data);
			URL += Data;

			return URL;
		}

		public string GetWOWURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			string URL = "http://wow.metoffice.gov.uk/automaticreading?siteid=";

			pwstring = "&siteAuthenticationKey=" + cumulus.WOWPW;
			URL += cumulus.WOWID + pwstring + "&dateutc=" + dateUTC;
			string Data = "";

			// send average speed and bearing
			Data += "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);

			Data += "&windgustmph=" + WindMPHStr(RecentMaxGust);

			Data += "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";

			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data += RainINstr(RainToday);
			}
			else
			{
				Data += RainINstr(RainSinceMidnight);
			}

			Data += "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);

			if (cumulus.SendUVToWOW)
			{
				Data += "&UV=" + UV.ToString(cumulus.UVFormat);
			}

			if (cumulus.SendSRToWOW)
			{
				Data += "&solarradiation=" + SolarRad.ToString("F0");
			}

			Data += "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";

			Data = cumulus.ReplaceCommas(Data);
			URL += Data;

			return URL;
		}

		public string GetWeatherbugURL(out string pwstring, DateTime timestamp)
		{
			string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
			string URL = "http://data.backyard2.weatherbug.com/data/livedata.aspx?ID=";

			pwstring = "&Key=" + cumulus.WeatherbugPW;
			URL += cumulus.WeatherbugID + pwstring + "&num=" + cumulus.WeatherbugNumber + "&dateutc=" + dateUTC;
			string Data = "";

			// send average speed and bearing
			Data += "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);

			Data += "&windgustmph=" + WindMPHStr(RecentMaxGust);

			Data += "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";

			if (cumulus.RolloverHour == 0)
			{
				// use today"s rain
				Data += RainINstr(RainToday);
			}
			else
			{
				Data += RainINstr(RainSinceMidnight);
			}

			Data += "&monthlyrainin=" + RainINstr(RainMonth);
			Data += "&yearlyrainin=" + RainINstr(RainYear);

			Data += "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);

			if (cumulus.SendUVToWeatherbug)
			{
				Data += "&UV=" + UV.ToString(cumulus.UVFormat);
			}

			if (cumulus.SendSRToWeatherbug)
			{
				Data += "&solarradiation=" + SolarRad.ToString("F0");
			}

			Data += "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";

			Data = cumulus.ReplaceCommas(Data);
			URL += Data;

			return URL;
		}

		public double ConvertUserRainToIN(double rain)
		{
			if (cumulus.RainUnit == 0)
			{
				return rain*0.0393700787;
			}
			else
			{
				return rain;
			}
		}

		private string alltimejsonformat(int item, string unit, string valueformat, string dateformat)
		{
			return "[\"" + alltimedescs[item] + "\",\"" + alltimerecarray[item].value.ToString(valueformat) + " " + unit + "\",\"" +
				   alltimerecarray[item].timestamp.ToString(dateformat) + "\"]";
		}

		public string GetTempRecords()
		{
			var json = "{\"data\":[";

			json += alltimejsonformat(AT_hightemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_lowtemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_highdewpoint, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_lowdewpoint, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_highapptemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_lowapptemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_highfeelslike, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_lowfeelslike, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_lowchill, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_highheatindex, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_highmintemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_lowmaxtemp, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += alltimejsonformat(AT_highdailytemprange, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D") + ",";
			json += alltimejsonformat(AT_lowdailytemprange, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D");

			json += "]}";
			return json;
		}

		public string GetHumRecords()
		{
			var json = "{\"data\":[";

			json += alltimejsonformat(AT_highhumidity, "%", cumulus.HumFormat, "f") + ",";
			json += alltimejsonformat(AT_lowhumidity, "%", cumulus.HumFormat, "f");

			json += "]}";
			return json;
		}

		public string GetPressRecords()
		{
			var json = "{\"data\":[";

			json += alltimejsonformat(AT_highpress, cumulus.PressUnitText, cumulus.PressFormat, "f") + ",";
			json += alltimejsonformat(AT_lowpress, cumulus.PressUnitText, cumulus.PressFormat, "f");

			json += "]}";
			return json;
		}

		public string GetWindRecords()
		{
			var json = "{\"data\":[";

			json += alltimejsonformat(AT_highgust, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += alltimejsonformat(AT_highwind, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += alltimejsonformat(AT_highwindrun, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D");

			json += "]}";
			return json;
		}

		public string GetRainRecords()
		{
			var json = "{\"data\":[";

			json += alltimejsonformat(AT_highrainrate, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f") + ",";
			json += alltimejsonformat(AT_hourlyrain, cumulus.RainUnitText, cumulus.RainFormat, "f") + ",";
			json += alltimejsonformat(AT_dailyrain, cumulus.RainUnitText, cumulus.RainFormat, "D") + ",";
			json += alltimejsonformat(AT_wetmonth, cumulus.RainUnitText, cumulus.RainFormat, "Y") + ",";
			json += alltimejsonformat(AT_longestdryperiod, "days", "f0", "D") + ",";
			json += alltimejsonformat(AT_longestwetperiod, "days", "f0", "D");

			json += "]}";
			return json;
		}

		private string monthlyjsonformat(int item, int month, string unit, string valueformat, string dateformat)
		{
			return "[\"" + alltimedescs[item] + "\",\"" + monthlyrecarray[item, month].value.ToString(valueformat) + " " + unit + "\",\"" +
				   monthlyrecarray[item, month].timestamp.ToString(dateformat) + "\"]";
		}

		public string GetMonthlyTempRecords(int month)
		{
			var json = "{\"data\":[";

			json += monthlyjsonformat(AT_hightemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowtemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_highdewpoint, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowdewpoint, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_highapptemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowapptemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_highfeelslike, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowfeelslike, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowchill, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_highheatindex, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_highmintemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowmaxtemp, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthlyjsonformat(AT_highdailytemprange, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D") + ",";
			json += monthlyjsonformat(AT_lowdailytemprange, month, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D");

			json += "]}";
			return json;
		}

		public string GetMonthlyHumRecords(int month)
		{
			var json = "{\"data\":[";

			json += monthlyjsonformat(AT_highhumidity, month, "%", cumulus.HumFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowhumidity, month, "%", cumulus.HumFormat, "f");

			json += "]}";
			return json;
		}

		public string GetMonthlyPressRecords(int month)
		{
			var json = "{\"data\":[";

			json += monthlyjsonformat(AT_highpress, month, cumulus.PressUnitText, cumulus.PressFormat, "f") + ",";
			json += monthlyjsonformat(AT_lowpress, month, cumulus.PressUnitText, cumulus.PressFormat, "f");

			json += "]}";
			return json;
		}

		public string GetMonthlyWindRecords(int month)
		{
			var json = "{\"data\":[";

			json += monthlyjsonformat(AT_highgust, month, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += monthlyjsonformat(AT_highwind, month, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += monthlyjsonformat(AT_highwindrun, month, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D");

			json += "]}";
			return json;
		}

		public string GetMonthlyRainRecords(int month)
		{
			var json = "{\"data\":[";

			json += monthlyjsonformat(AT_highrainrate, month, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f") + ",";
			json += monthlyjsonformat(AT_hourlyrain, month, cumulus.RainUnitText, cumulus.RainFormat, "f") + ",";
			json += monthlyjsonformat(AT_dailyrain, month, cumulus.RainUnitText, cumulus.RainFormat, "D") + ",";
			json += monthlyjsonformat(AT_wetmonth, month, cumulus.RainUnitText, cumulus.RainFormat, "Y") + ",";
			json += monthlyjsonformat(AT_longestdryperiod, month, "days", "f0", "D") + ",";
			json += monthlyjsonformat(AT_longestwetperiod, month, "days", "f0", "D");

			json += "]}";
			return json;
		}

		private string monthyearjsonformat(int item, double value, DateTime timestamp, string unit, string valueformat, string dateformat)
		{
			return "[\"" + alltimedescs[item] + "\",\"" + value.ToString(valueformat) + " " + unit + "\",\"" + timestamp.ToString(dateformat) + "\"]";
		}

		public string GetThisMonthTempRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_hightemp, HighTempThisMonth, HighTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowtemp, LowTempThisMonth, LowTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highdewpoint, HighDewpointThisMonth, HighDewpointThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowdewpoint, LowDewpointThisMonth, LowDewpointThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highapptemp, HighAppTempThisMonth, HighAppTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowapptemp, LowAppTempThisMonth, LowAppTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highfeelslike, HighFeelsLikeThisMonth, HighFeelsLikeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowfeelslike, LowFeelsLikeThisMonth, LowFeelsLikeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowchill, LowWindChillThisMonth, LowWindChillThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highheatindex, HighHeatIndexThisMonth, HighHeatIndexThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highmintemp, HighMinTempThisMonth, HighMinTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowmaxtemp, LowMaxTempThisMonth, LowMaxTempThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highdailytemprange, HighDailyTempRangeThisMonth, HighDailyTempRangeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D") + ",";
			json += monthyearjsonformat(AT_lowdailytemprange, LowDailyTempRangeThisMonth, LowDailyTempRangeThisMonthTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D");

			json += "]}";
			return json;
		}

		public string GetThisMonthHumRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highhumidity, HighHumidityThisMonth, HighHumidityThisMonthTS, "%", cumulus.HumFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowhumidity, LowHumidityThisMonth, LowHumidityThisMonthTS, "%", cumulus.HumFormat, "f");

			json += "]}";
			return json;
		}

		public string GetThisMonthPressRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highpress, HighPressThisMonth, HighPressThisMonthTS, cumulus.PressUnitText, cumulus.PressFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowpress, LowPressThisMonth, LowPressThisMonthTS, cumulus.PressUnitText, cumulus.PressFormat, "f");

			json += "]}";
			return json;
		}

		public string GetThisMonthWindRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highgust, HighGustThisMonth, HighGustThisMonthTS, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += monthyearjsonformat(AT_highwind, HighWindThisMonth, HighWindThisMonthTS, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += monthyearjsonformat(AT_highwindrun, HighDailyWindrunThisMonth, HighDailyWindrunThisMonthTS, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D");

			json += "]}";
			return json;
		}

		public string GetThisMonthRainRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highrainrate, HighRainThisMonth, HighRainThisMonthTS, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f") + ",";
			json += monthyearjsonformat(AT_hourlyrain, HighHourlyRainThisMonth, HighHourlyRainThisMonthTS, cumulus.RainUnitText, cumulus.RainFormat, "f") + ",";
			json += monthyearjsonformat(AT_dailyrain, HighDailyRainThisMonth, HighDailyRainThisMonthTS, cumulus.RainUnitText, cumulus.RainFormat, "D") + ",";
			//json += monthyearjsonformat(AT_wetmonth, month, cumulus.RainUnitText, cumulus.RainFormat, "Y") + ",";
			json += monthyearjsonformat(AT_longestdryperiod, LongestDryPeriodThisMonth, LongestDryPeriodThisMonthTS, "days", "f0", "D") + ",";
			json += monthyearjsonformat(AT_longestwetperiod, LongestWetPeriodThisMonth, LongestWetPeriodThisMonthTS, "days", "f0", "D");

			json += "]}";
			return json;
		}

		public string GetThisYearTempRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_hightemp, HighTempThisYear, HighTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowtemp, LowTempThisYear, LowTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highdewpoint, HighDewpointThisYear, HighDewpointThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowdewpoint, LowDewpointThisYear, LowDewpointThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highapptemp, HighAppTempThisYear, HighAppTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowapptemp, LowAppTempThisYear, LowAppTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highfeelslike, HighFeelsLikeThisYear, HighFeelsLikeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowfeelslike, LowFeelsLikeThisYear, LowFeelsLikeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowchill, LowWindChillThisYear, LowWindChillThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highheatindex, HighHeatIndexThisYear, HighHeatIndexThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highmintemp, HighMinTempThisYear, HighMinTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowmaxtemp, LowMaxTempThisYear, LowMaxTempThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "f") + ",";
			json += monthyearjsonformat(AT_highdailytemprange, HighDailyTempRangeThisYear, HighDailyTempRangeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D") + ",";
			json += monthyearjsonformat(AT_lowdailytemprange, LowDailyTempRangeThisYear, LowDailyTempRangeThisYearTS, "&deg;" + cumulus.TempUnitText[1].ToString(), cumulus.TempFormat, "D");

			json += "]}";
			return json;
		}

		public string GetThisYearHumRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highhumidity, HighHumidityThisYear, HighHumidityThisYearTS, "%", cumulus.HumFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowhumidity, LowHumidityThisYear, LowHumidityThisYearTS, "%", cumulus.HumFormat, "f");

			json += "]}";
			return json;
		}

		public string GetThisYearPressRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highpress, HighPressThisYear, HighPressThisYearTS, cumulus.PressUnitText, cumulus.PressFormat, "f") + ",";
			json += monthyearjsonformat(AT_lowpress, LowPressThisYear, LowPressThisYearTS, cumulus.PressUnitText, cumulus.PressFormat, "f");

			json += "]}";
			return json;
		}

		public string GetThisYearWindRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highgust, HighGustThisYear, HighGustThisYearTS, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += monthyearjsonformat(AT_highwind, HighWindThisYear, HighWindThisYearTS, cumulus.WindUnitText, cumulus.WindFormat, "f") + ",";
			json += monthyearjsonformat(AT_highwindrun, HighDailyWindrunThisYear, HighDailyWindrunThisYearTS, cumulus.WindRunUnitText, cumulus.WindRunFormat, "D");

			json += "]}";
			return json;
		}

		public string GetThisYearRainRecords()
		{
			var json = "{\"data\":[";

			json += monthyearjsonformat(AT_highrainrate, HighRainThisYear, HighRainThisYearTS, cumulus.RainUnitText + "/hr", cumulus.RainFormat, "f") + ",";
			json += monthyearjsonformat(AT_hourlyrain, HighHourlyRainThisYear, HighHourlyRainThisYearTS, cumulus.RainUnitText, cumulus.RainFormat, "f") + ",";
			json += monthyearjsonformat(AT_dailyrain, HighDailyRainThisYear, HighDailyRainThisYearTS, cumulus.RainUnitText, cumulus.RainFormat, "D") + ",";
			json += monthyearjsonformat(AT_wetmonth, HighMonthlyRainThisYear, HighMonthlyRainThisYearTS, cumulus.RainUnitText, cumulus.RainFormat, "Y") + ",";
			json += monthyearjsonformat(AT_longestdryperiod, LongestDryPeriodThisYear, LongestDryPeriodThisYearTS, "days", "f0", "D") + ",";
			json += monthyearjsonformat(AT_longestwetperiod, LongestWetPeriodThisYear, LongestWetPeriodThisYearTS, "days", "f0", "D");

			json += "]}";
			return json;
		}

		public string GetExtraTemp()
		{
			var json = "{\"data\":[";

			for (int sensor = 1; sensor < 11; sensor++)
			{
				json += "[\"" + cumulus.ExtraTempCaptions[sensor] + "\",\"" + ExtraTemp[sensor].ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() +
						"\"]";

				if (sensor < 10)
				{
					json += ",";
				}
			}

			json += "]}";
			return json;
		}

		public string GetExtraHum()
		{
			var json = "{\"data\":[";

			for (int sensor = 1; sensor < 11; sensor++)
			{
				json += "[\"" + cumulus.ExtraHumCaptions[sensor] + "\",\"" + ExtraHum[sensor].ToString(cumulus.HumFormat) + "\",\"%\"]";

				if (sensor < 10)
				{
					json += ",";
				}
			}

			json += "]}";
			return json;
		}

		public string GetExtraDew()
		{
			var json = "{\"data\":[";

			for (int sensor = 1; sensor < 11; sensor++)
			{
				json += "[\"" + cumulus.ExtraDPCaptions[sensor] + "\",\"" + ExtraDewPoint[sensor].ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() +
						"\"]";

				if (sensor < 10)
				{
					json += ",";
				}
			}

			json += "]}";
			return json;
		}

		public string GetSoilTemp()
		{
			var json = "{\"data\":[";

			json += "[\"" + cumulus.SoilTempCaptions[1] + "\",\"" + SoilTemp1.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[2] + "\",\"" + SoilTemp2.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[3] + "\",\"" + SoilTemp3.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[4] + "\",\"" + SoilTemp4.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[5] + "\",\"" + SoilTemp5.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[6] + "\",\"" + SoilTemp6.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[7] + "\",\"" + SoilTemp7.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[8] + "\",\"" + SoilTemp8.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[9] + "\",\"" + SoilTemp9.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[10] + "\",\"" + SoilTemp10.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[11] + "\",\"" + SoilTemp11.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[12] + "\",\"" + SoilTemp12.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[13] + "\",\"" + SoilTemp13.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[14] + "\",\"" + SoilTemp14.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[15] + "\",\"" + SoilTemp15.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.SoilTempCaptions[16] + "\",\"" + SoilTemp16.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"]";

			json += "]}";
			return json;
		}

		public string GetSoilMoisture()
		{
			var json = "{\"data\":[";

			json += "[\"" + cumulus.SoilMoistureCaptions[1] + "\",\"" + SoilMoisture1.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[2] + "\",\"" + SoilMoisture2.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[3] + "\",\"" + SoilMoisture3.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[4] + "\",\"" + SoilMoisture4.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[5] + "\",\"" + SoilMoisture5.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[6] + "\",\"" + SoilMoisture6.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[7] + "\",\"" + SoilMoisture7.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[8] + "\",\"" + SoilMoisture8.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[9] + "\",\"" + SoilMoisture9.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[10] + "\",\"" + SoilMoisture10.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[11] + "\",\"" + SoilMoisture11.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[12] + "\",\"" + SoilMoisture12.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[13] + "\",\"" + SoilMoisture13.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[14] + "\",\"" + SoilMoisture14.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[15] + "\",\"" + SoilMoisture15.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"],";
			json += "[\"" + cumulus.SoilMoistureCaptions[16] + "\",\"" + SoilMoisture16.ToString("F0") + "\",\"" + cumulus.SoilMoistureUnitText + "\"]";

			json += "]}";
			return json;
		}

		public string GetAirQuality()
		{
			var json = "{\"data\":[";

			json += "[\"" + cumulus.AirQualityCaptions[1] + "\",\"" + AirQuality1.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityCaptions[2] + "\",\"" + AirQuality2.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityCaptions[3] + "\",\"" + AirQuality3.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityCaptions[4] + "\",\"" + AirQuality4.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityAvgCaptions[1] + "\",\"" + AirQualityAvg1.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityAvgCaptions[2] + "\",\"" + AirQualityAvg2.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityAvgCaptions[3] + "\",\"" + AirQualityAvg3.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"],";
			json += "[\"" + cumulus.AirQualityAvgCaptions[4] + "\",\"" + AirQualityAvg4.ToString(cumulus.TempFormat) + "\",\"" + cumulus.AirQualityUnitText + "\"]";

			json += "]}";
			return json;
		}

		public string GetLightning()
		{
			var json = "{\"data\":[";

			json += "[\"Distance to last strike\",\"" + LightningDistance.ToString(cumulus.WindRunFormat) + "\",\"" + cumulus.WindRunUnitText + "\"],";
			json += "[\"Time of last strike\",\"" + LightningTime.ToString() + "\",\"\"],";
			json += "[\"Number of strikes today\",\"" + LightningStrikesToday.ToString() + "\",\"\"]";

			json += "]}";
			return json;
		}

		public string GetLeaf()
		{
			var json = "{\"data\":[";

			json += "[\"" + cumulus.LeafCaptions[1] + "\",\"" + LeafTemp1.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.LeafCaptions[2] + "\",\"" + LeafTemp2.ToString(cumulus.TempFormat) + "\",\"&deg;" + cumulus.TempUnitText[1].ToString() + "\"],";
			json += "[\"" + cumulus.LeafCaptions[3] + "\",\"" + LeafWetness1.ToString() + "\",\"&nbsp;" + "\"],";
			json += "[\"" + cumulus.LeafCaptions[4] + "\",\"" + LeafWetness2.ToString() + "\",\"&nbsp;" + "\"]";

			json += "]}";
			return json;
		}

		public string GetLeaf4()
		{
			var json = "{\"data\":[";

			json += "[\"" + cumulus.LeafCaptions[1] + "\",\"" + LeafTemp1.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" + LeafWetness1.ToString() + "\"],";
			json += "[\"" + cumulus.LeafCaptions[2] + "\",\"" + LeafTemp2.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" + LeafWetness2.ToString() + "\"],";
			json += "[\"" + cumulus.LeafCaptions[3] + "\",\"" + LeafTemp3.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" + LeafWetness3.ToString() + "\"],";
			json += "[\"" + cumulus.LeafCaptions[4] + "\",\"" + LeafTemp4.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" + LeafWetness4.ToString() + "\"]";

			json += "]}";
			return json;
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
			var json = "{\"data\":[";

			json += "[\"" + "High Temperature" + "\",\"" + HighTempToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					hightemptodaytime.ToShortTimeString() + "\",\"" + HighTempYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					hightempyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Temperature" + "\",\"" + LowTempToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					lowtemptodaytime.ToShortTimeString() + "\",\"" + LowTempYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					lowtempyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Temperature Range" + "\",\"" + (HighTempToday - LowTempToday).ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + "&nbsp;" + "\",\"" + (HighTempYesterday - LowTempYesterday).ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + "&nbsp;" + "\"],";
			json += "[\"" + "High Apparent Temperature" + "\",\"" + HighAppTempToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					highapptemptodaytime.ToShortTimeString() + "\",\"" + HighAppTempYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + highapptempyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Apparent Temperature" + "\",\"" + LowAppTempToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					lowapptemptodaytime.ToShortTimeString() + "\",\"" + LowAppTempYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + lowapptempyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "High Feels Like" + "\",\"" + HighFeelsLikeToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					highfeelsliketodaytime.ToShortTimeString() + "\",\"" + HighFeelsLikeYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + highfeelslikeyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Feels Like" + "\",\"" + LowFeelsLikeToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					lowfeelsliketodaytime.ToShortTimeString() + "\",\"" + LowAppTempYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + lowapptempyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "High Dew Point" + "\",\"" + HighDewpointToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					HighDewpointTodayTime.ToShortTimeString() + "\",\"" + HighDewpointYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + HighDewpointYesterdayTime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Dew Point" + "\",\"" + LowDewpointToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					LowDewpointTodayTime.ToShortTimeString() + "\",\"" + LowDewpointYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + LowDewpointYesterdayTime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Wind Chill" + "\",\"" + LowWindChillToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					lowwindchilltodaytime.ToShortTimeString() + "\",\"" + lowwindchillyesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + lowwindchillyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "High Heat Index" + "\",\"" + HighHeatIndexToday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() + "\",\"" +
					highheatindextodaytime.ToShortTimeString() + "\",\"" + HighHeatIndexYesterday.ToString(cumulus.TempFormat) + "&nbsp;&deg;" + cumulus.TempUnitText[1].ToString() +
					"\",\"" + highheatindexyesterdaytime.ToShortTimeString() + "\"]";

			json += "]}";
			return json;
		}

		public string GetTodayYestHum()
		{
			var json = "{\"data\":[";

			json += "[\"" + "High Humidity" + "\",\"" + highhumiditytoday.ToString(cumulus.HumFormat) + "&nbsp;%" + "\",\"" + highhumiditytodaytime.ToShortTimeString() + "\",\"" +
					highhumidityyesterday.ToString(cumulus.HumFormat) + "&nbsp;%" + "\",\"" + highhumidityyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Humidity" + "\",\"" + lowhumiditytoday.ToString(cumulus.HumFormat) + "&nbsp;%" + "\",\"" + lowhumiditytodaytime.ToShortTimeString() + "\",\"" +
					lowhumidityyesterday.ToString(cumulus.HumFormat) + "&nbsp;%" + "\",\"" + lowhumidityyesterdaytime.ToShortTimeString() + "\"]";

			json += "]}";
			return json;
		}

		public string GetTodayYestRain()
		{
			var json = "{\"data\":[";

			json += "[\"" + "Total Rain" + "\",\"" + RainToday.ToString(cumulus.RainFormat) + "&nbsp;" + cumulus.RainUnitText + "\",\"" + "&nbsp;" + "\",\"" +
					RainYesterday.ToString(cumulus.RainFormat) + "&nbsp;" + cumulus.RainUnitText + "\",\"" + "&nbsp;" + "\"],";
			json += "[\"" + "High Rain Rate" + "\",\"" + highraintoday.ToString(cumulus.RainFormat) + "&nbsp;" + cumulus.RainUnitText + "/hr" + "\",\"" +
					highraintodaytime.ToShortTimeString() + "\",\"" + highrainyesterday.ToString(cumulus.RainFormat) + "&nbsp;" + cumulus.RainUnitText + "/hr" + "\",\"" +
					highrainyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "High Hourly Rain" + "\",\"" + highhourlyraintoday.ToString(cumulus.RainFormat) + "&nbsp;" + cumulus.RainUnitText + "\",\"" +
					highhourlyraintodaytime.ToShortTimeString() + "\",\"" + highhourlyrainyesterday.ToString(cumulus.RainFormat) + "&nbsp;" + cumulus.RainUnitText + "\",\"" +
					highhourlyrainyesterdaytime.ToShortTimeString() + "\"]";

			json += "]}";
			return json;
		}

		public string GetTodayYestWind()
		{
			var json = "{\"data\":[";

			json += "[\"" + "Highest Gust" + "\",\"" + highgusttoday.ToString(cumulus.WindFormat) + "&nbsp;" + cumulus.WindUnitText + "\",\"" +
					highgusttodaytime.ToShortTimeString() + "\",\"" + highgustyesterday.ToString(cumulus.WindFormat) + "&nbsp;" + cumulus.WindUnitText + "\",\"" +
					highgustyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Highest Speed" + "\",\"" + highwindtoday.ToString(cumulus.WindFormat) + "&nbsp;" + cumulus.WindUnitText + "\",\"" +
					highwindtodaytime.ToShortTimeString() + "\",\"" + highwindyesterday.ToString(cumulus.WindFormat) + "&nbsp;" + cumulus.WindUnitText + "\",\"" +
					highwindyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Wind Run" + "\",\"" + WindRunToday.ToString(cumulus.WindRunFormat) + "&nbsp;" + cumulus.WindRunUnitText + "\",\"" + "&nbsp;" + "\",\"" +
					YesterdayWindRun.ToString(cumulus.WindRunFormat) + "&nbsp;" + cumulus.WindRunUnitText + "\",\"" + "&nbsp;" + "\"],";
			json += "[\"" + "Dominant Direction" + "\",\"" + DominantWindBearing.ToString("F0") + "&nbsp;&deg;&nbsp;" + CompassPoint(DominantWindBearing) + "\",\"" + "&nbsp;" +
					"\",\"" + YestDominantWindBearing.ToString("F0") + "&nbsp;&deg;&nbsp;" + CompassPoint(YestDominantWindBearing) + "\",\"" + "&nbsp;" + "\"]";

			json += "]}";
			return json;
		}

		public string GetTodayYestPressure()
		{
			var json = "{\"data\":[";

			json += "[\"" + "High Pressure" + "\",\"" + highpresstoday.ToString(cumulus.PressFormat) + "&nbsp;" + cumulus.PressUnitText + "\",\"" +
					highpresstodaytime.ToShortTimeString() + "\",\"" + highpressyesterday.ToString(cumulus.PressFormat) + "&nbsp;" + cumulus.PressUnitText + "\",\"" +
					highpressyesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Low Pressure" + "\",\"" + lowpresstoday.ToString(cumulus.PressFormat) + "&nbsp;" + cumulus.PressUnitText + "\",\"" +
					lowpresstodaytime.ToShortTimeString() + "\",\"" + lowpressyesterday.ToString(cumulus.PressFormat) + "&nbsp;" + cumulus.PressUnitText + "\",\"" +
					lowpressyesterdaytime.ToShortTimeString() + "\"]";

			json += "]}";
			return json;
		}

		public string GetTodayYestSolar()
		{
			var json = "{\"data\":[";

			json += "[\"" + "High Solar Radiation" + "\",\"" + HighSolarToday.ToString("F0") + "&nbsp" + "W/m2" + "\",\"" + highsolartodaytime.ToShortTimeString() + "\",\"" +
					HighSolarYesterday.ToString("F0") + "&nbsp;" + "W/m2" + "\",\"" + highsolaryesterdaytime.ToShortTimeString() + "\"],";
			json += "[\"" + "Hours of Sunshine" + "\",\"" + SunshineHours.ToString(cumulus.SunFormat) + "&nbsp;hrs" + "\",\"" + "&nbsp;" + "\",\"" + YestSunshineHours.ToString(cumulus.SunFormat) +
					"&nbsp;hrs" + "\",\"" + "&nbsp;" + "\"]";

			json += "]}";
			return json;
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
				var total = File.ReadLines(cumulus.DayFile).Count();

				var json = "{\"draw\":" + draw + ",\"recordsTotal\":" + total + ",\"recordsFiltered\":" + total + ",\"data\":[";

				var lines = File.ReadLines(cumulus.DayFile).Skip(start).Take(length);

				var lineNum = start + 1; // Start is zero relative

				foreach (var line in lines)
				{
					var fields = line.Split(Convert.ToChar(cumulus.ListSeparator));
					var numFields = fields.Length;
					json += $"[{lineNum++},";
					for (var i = 0; i < numFields; i++)
					{
						json = json + "\"" + fields[i] + "\"";
						if (i < fields.Length - 1)
						{
							json += ",";
						}
					}

					if (numFields < Cumulus.DayfileFields)
					{
						// insufficient fields, pad with empty fields
						for (var i = numFields; i < Cumulus.DayfileFields; i++)
						{
							json += ",\"\"";
						}
					}
					json += "],";
				}

				json = json.TrimEnd(',');
				json += "]}";

				return json;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.ToString());
			}

			return "";
		}

		internal string GetDiaryData(string date)
		{

			string json;

			var result = cumulus.DiaryDB.Query<DiaryData>("select * from DiaryData where date(Timestamp) = ? order by Timestamp limit 1", date);

			if (result.Count > 0)
			{
				json = "{\"entry\":\"" + result[0].entry + "\"," +
					"\"snowFalling\":" + result[0].snowFalling + "," +
					"\"snowLying\":" + result[0].snowLying + "," +
					"\"snowDepth\":\"" + result[0].snowDepth + "\"}";
			}
			else
			{
				json = "{\"entry\":\"\"," +
					"\"snowFalling\":0," +
					"\"snowLying\":0," +
					"\"snowDepth\":\"\"}";
			}

			return json;
		}

		// Fetchs all days in the required month that have a diary entry
		//internal string GetDiarySummary(string year, string month)
		internal string GetDiarySummary()
		{
			string json;
			//var result = cumulus.DiaryDB.Query<DiaryData>("select Timestamp from DiaryData where strftime('%Y', Timestamp) = ? and strftime('%m', Timestamp) = ? order by Timestamp", year, month);
			var result = cumulus.DiaryDB.Query<DiaryData>("select Timestamp from DiaryData order by Timestamp");

			if (result.Count > 0)
			{
				json = "{\"dates\":[";
				for (int i = 0; i < result.Count; i++)
				{
					json += "\"" + result[i].Timestamp.ToString("yyy-MM-dd") + "\",";
				}
				json = json.Remove(json.Length - 1);
				json += "]}";
			}
			else
			{
				json = "{\"dates\":[]}";
			}

			return json;
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

				var logfile = (extra?cumulus.GetExtraLogFileName(ts):cumulus.GetLogFileName(ts));
				var numFields = (extra ? Cumulus.NumExtraLogFileFields : Cumulus.NumLogFileFields);

				var total = File.ReadLines(logfile).Count();
				var json = "{\"draw\":" + draw + ",\"recordsTotal\":" + total + ",\"recordsFiltered\":" + total + ",\"data\":[";

				var lines = File.ReadLines(logfile).Skip(start).Take(length);

				var lineNum = start + 1; // Start is zero relative

				foreach (var line in lines)
				{
					var fields = line.Split(Convert.ToChar(cumulus.ListSeparator));
					json += $"[{lineNum++},";
					for (var i = 0; i < numFields; i++)
					{
						if (i < fields.Length)
						{
							// field exists
							json += "\"" + fields[i] + "\"";
						}
						else
						{
							// add padding
							json += "\" \"";
						}

						if (i < numFields - 1)
						{
							json += ",";
						}
					}
					json += "],";
				}

				json = json.TrimEnd(',');
				json += "]}";

				return json;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.ToString());
			}

			return "";
		}

		public string GetUnits()
		{
			var json = "{\"temp\":\"" + cumulus.TempUnitText[1] + "\",\"wind\":\"" + cumulus.WindUnitText + "\",\"rain\":\"" + cumulus.RainUnitText + "\",\"press\":\"" +
					   cumulus.PressUnitText + "\"}";
			return json;
		}

		public string GetGraphConfig()
		{
			string json = "{\"temp\":{\"units\":\"" + cumulus.TempUnitText[1] + "\",\"decimals\":" + cumulus.TempDPlaces + "},\"wind\":{\"units\":\"" + cumulus.WindUnitText +
						  "\",\"decimals\":" + cumulus.WindDPlaces + "},\"rain\":{\"units\":\"" + cumulus.RainUnitText + "\",\"decimals\":" + cumulus.RainDPlaces +
						  "},\"press\":{\"units\":\"" + cumulus.PressUnitText + "\",\"decimals\":" + cumulus.PressDPlaces + "},\"hum\":{\"decimals\":" + cumulus.HumDPlaces + "},\"uv\":{\"decimals\":" + cumulus.UVDPlaces + "}}";
			return json;
		}

		public string GetDailyRainGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"dailyrain\":[");

			for (var i = 0; i < RecentDailyDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000 + "," + RecentDailyDataList[i].rain.ToString(cumulus.RainFormat, InvC) + "]");

				if (i < RecentDailyDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetSunHoursGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"sunhours\":[");

			for (var i = 0; i < RecentDailyDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000 + "," + RecentDailyDataList[i].sunhours.ToString(cumulus.SunFormat, InvC) + "]");

				if (i < RecentDailyDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetDailyTempGraphData()
		{
			var InvC = new CultureInfo("");
			StringBuilder sb = new StringBuilder("{\"mintemp\":[");

			for (var i = 0; i < RecentDailyDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000 + "," + RecentDailyDataList[i].mintemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < RecentDailyDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"maxtemp\":[");
			for (var i = 0; i < RecentDailyDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000 + "," + RecentDailyDataList[i].maxtemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < RecentDailyDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("],\"avgtemp\":[");
			for (var i = 0; i < RecentDailyDataList.Count; i++)
			{
				sb.Append("[" + DateTimeToUnix(RecentDailyDataList[i].timestamp) * 1000 + "," + RecentDailyDataList[i].avgtemp.ToString(cumulus.TempFormat, InvC) + "]");
				if (i < RecentDailyDataList.Count - 1)
					sb.Append(",");
			}

			sb.Append("]}");
			return sb.ToString();
		}

		internal string GetCurrentData()
		{
			String windRoseData = (windcounts[0] * cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture);

			for (var i = 1; i < cumulus.NumWindRosePoints; i++)
			{
				windRoseData = windRoseData + "," + (windcounts[i] * cumulus.WindGustMult).ToString(cumulus.WindFormat, CultureInfo.InvariantCulture);
			}

			string stormRainStart = StartOfStorm == DateTime.MinValue ? "-----" : StartOfStorm.ToString("d");

			var data = new DataStruct(cumulus, OutdoorTemperature, OutdoorHumidity, TempTotalToday / tempsamplestoday, IndoorTemperature, OutdoorDewpoint, WindChill, IndoorHumidity,
				Pressure, WindLatest, WindAverage, RecentMaxGust, WindRunToday, Bearing, AvgBearing, RainToday, RainYesterday, RainMonth, RainYear, RainRate,
				RainLastHour, HeatIndex, Humidex, ApparentTemperature, temptrendval, presstrendval, highgusttoday, highgusttodaytime.ToString("HH:mm"), highwindtoday,
				highgustbearing, cumulus.WindUnitText, BearingRangeFrom10, BearingRangeTo10, windRoseData, HighTempToday, LowTempToday,
				hightemptodaytime.ToString("HH:mm"), lowtemptodaytime.ToString("HH:mm"), highpresstoday, lowpresstoday, highpresstodaytime.ToString("HH:mm"),
				lowpresstodaytime.ToString("HH:mm"), highraintoday, highraintodaytime.ToString("HH:mm"), highhumiditytoday, lowhumiditytoday,
				highhumiditytodaytime.ToString("HH:mm"), lowhumiditytodaytime.ToString("HH:mm"), cumulus.PressUnitText, cumulus.TempUnitText, cumulus.RainUnitText,
				HighDewpointToday, LowDewpointToday, HighDewpointTodayTime.ToString("HH:mm"), LowDewpointTodayTime.ToString("HH:mm"), LowWindChillToday,
				lowwindchilltodaytime.ToString("HH:mm"), (int)SolarRad, (int)HighSolarToday, highsolartodaytime.ToString("HH:mm"), UV, HighUVToday,
				highuvtodaytime.ToString("HH:mm"), forecaststr, getTimeString(cumulus.SunRiseTime), getTimeString(cumulus.SunSetTime),
				getTimeString(cumulus.MoonRiseTime), getTimeString(cumulus.MoonSetTime), HighHeatIndexToday, highheatindextodaytime.ToString("HH:mm"), HighAppTempToday,
				LowAppTempToday, highapptemptodaytime.ToString("HH:mm"), lowapptemptodaytime.ToString("HH:mm"), (int)Math.Round(CurrentSolarMax),
				alltimerecarray[AT_highpress].value, alltimerecarray[AT_lowpress].value, SunshineHours, CompassPoint(DominantWindBearing), LastRainTip,
				highhourlyraintoday, highhourlyraintodaytime.ToString("HH:mm"), "F" + cumulus.Beaufort(highwindtoday), "F" + cumulus.Beaufort(WindAverage),
				cumulus.BeaufortDesc(WindAverage), LastDataReadTimestamp.ToString("HH:mm:ss"), DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
				cumulus.LowTempAlarmState, cumulus.HighTempAlarmState, cumulus.TempChangeUpAlarmState, cumulus.TempChangeDownAlarmState, cumulus.HighRainTodayAlarmState, cumulus.HighRainRateAlarmState,
				cumulus.LowPressAlarmState, cumulus.HighPressAlarmState, cumulus.PressChangeUpAlarmState, cumulus.PressChangeDownAlarmState, cumulus.HighGustAlarmState, cumulus.HighWindAlarmState,
				cumulus.SensorAlarmState, cumulus.BatteryLowAlarmState,
				FeelsLike, HighFeelsLikeToday, highfeelsliketodaytime.ToString("HH:mm:ss"), LowFeelsLikeToday, lowfeelsliketodaytime.ToString("HH:mm:ss"));

			try
			{
				MemoryStream stream = new MemoryStream();
				DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(DataStruct));
				DataContractJsonSerializerSettings s = new DataContractJsonSerializerSettings();
				ds.WriteObject(stream, data);

				string jsonString = Encoding.UTF8.GetString(stream.ToArray());
				stream.Close();

				return jsonString;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);

				return "";
			}

		}

		public void UpdateAPRS()
		{
			cumulus.LogMessage("Updating CWOP");
			using (var client = new TcpClient(cumulus.APRSserver, cumulus.APRSport))
			using (var ns = client.GetStream())
			{
				try
				{
					StreamWriter writer = new StreamWriter(ns);

					string message = "user " + cumulus.APRSID + " pass " + cumulus.APRSpass + " vers Cumulus " + cumulus.Version;

					Byte[] data = Encoding.ASCII.GetBytes(message);

					cumulus.LogDebugMessage("Sending user and pass to CWOP");

					writer.WriteLine(message);
					writer.Flush();

					Thread.Sleep(3000);

					string timeUTC = DateTime.Now.ToUniversalTime().ToString("ddHHmm");

					message = cumulus.APRSID + ">APRS,TCPIP*:@" + timeUTC + "z" + APRSLat(cumulus) + "/" + APRSLon(cumulus);
					// bearing _nnn
					message += "_" + AvgBearing.ToString("D3");
					// wind speed mph /nnn
					message += "/" + APRSwind(WindAverage);
					// wind gust last 5 mins mph gnnn
					message += "g" + APRSwind(RecentMaxGust);
					// temp F tnnn
					message += "t" + APRStemp(OutdoorTemperature);
					// rain last hour 0.01 inches rnnn
					message += "r" + APRSrain(RainLastHour);
					// rain last 24 hours 0.01 inches pnnn
					message += "p" + APRSrain(RainLast24Hour);
					if (cumulus.RolloverHour == 0)
					{
						// use today"s rain for safety
						message += "P" + APRSrain(RainToday);
					}
					else
					{
						// 0900 day, use midnight calculation
						message += "P" + APRSrain(RainSinceMidnight);
					}
					if ((!cumulus.APRSHumidityCutoff) || (ConvertUserTempToC(OutdoorTemperature) >= -10))
					{
						// humidity Hnn
						message += "h" + APRShum(OutdoorHumidity);
					}
					// bar 0.1mb Bnnnnn
					message += "b" + APRSpress(AltimeterPressure);
					if (cumulus.SendSRToAPRS)
					{
						message += APRSsolarradStr(Convert.ToInt32(SolarRad));
					}

					// station type e<string>
					message += "eCumulus" + cumulus.APRSstationtype[cumulus.StationType];

					cumulus.LogDebugMessage("Sending: " + message);

					data = Encoding.ASCII.GetBytes(message);

					writer.WriteLine(message);
					writer.Flush();

					Thread.Sleep(3000);
					writer.Close();
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

		public GraphData(DateTime ts, double rain, double raintoday, double rrate, double temp, double dp, double appt, double chill, double heat, double intemp, double press,
			double speed, double gust, int avgdir, int wdir, int hum, int inhum, double solar, double smax, double uv, double feels)
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
	}

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
}
