using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using CumulusMX.LogFiles;

using EmbedIO.Utilities;

using NLog;

using SQLite;

using Timer = System.Timers.Timer;

namespace CumulusMX
{
	internal abstract partial class WeatherStation
	{
		public int StationId { get; }

		public struct TWindRecent
		{
			public double GustUncal; // uncalibrated "gust" as read from Stations
			public double SpeedUncal; // uncalibrated "speed" as read from Stations
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
		private readonly Lock monthIniThreadLock = new();
		public readonly Lock yearIniThreadLock = new();
		public readonly Lock alltimeIniThreadLock = new();
		public readonly Lock monthlyalltimeIniThreadLock = new();
		public readonly Lock recentwindLock = new();

		private static readonly SemaphoreSlim webSocketSemaphore = new(1, 1);

		// equivalents of Zambretti "dial window" letters A - Z
		private readonly int[] riseOptions = [25, 25, 25, 24, 24, 19, 16, 12, 11, 9, 8, 6, 5, 2, 1, 1, 0, 0, 0, 0, 0, 0];
		private readonly int[] steadyOptions = [25, 25, 25, 25, 25, 25, 23, 23, 22, 18, 15, 13, 10, 4, 1, 1, 0, 0, 0, 0, 0, 0];
		private readonly int[] fallOptions = [25, 25, 25, 25, 25, 25, 25, 25, 23, 23, 21, 20, 17, 14, 7, 3, 1, 1, 1, 0, 0, 0];

		public Cumulus cumulus;

		private int lastMinute;
		private int lastHour;
		private long lastSecond;
		private int lastSnowMinute;

		public DateTime AlltimeRecordTimestamp { get; set; }

		public BackgroundWorker bw;

		public bool calculaterainrate = false;

		protected List<int> buffer = [];

		private readonly List<Last10MinWind> Last10MinWindList = [];

		public List<string> LowBatteryDevices { get; set; } = [];

		public bool gotraindaystart = false;
		protected double prevraincounter = 0.0;

		protected bool DayResetInProgress = false;

		public int IndoorBattStatus;
		public int WindBattStatus;
		public int RainBattStatus;
		public int TempBattStatus;
		public int UVBattStatus;

		public DateTime LastDataReadTime;
		public DateTime DataDateTime;
		public bool haveReadData = false;

		// Should Cumulus find the peak gust?
		// This gets set to false for Davis stations after logger download
		// if 10-minute gust period is in use, so we use the Davis value instead.
		public bool CalcRecentMaxGust = true;

		protected SerialPort comport;

		public Timer secondTimer;

		private double previousPressStation = 9999;

		public int multicastsGood, multicastsBad;

		public bool timerStartNeeded = false;

		private readonly DateTime versionCheckTime;

		public SQLiteConnection RecentDataDb;
		// Extra sensors

		public double SolarElevation;  // must be a field

		public bool WindReadyToPlot = false;
		public bool TempReadyToPlot = false;
		private bool first_temp = true;

		public abstract void Start();

		public virtual string GetEcowittCameraUrl(string mac)
		{
			cumulus.LogMessage("GetEcowittCameraUrl: Not implemented for this station");
			return string.Empty;
		}

		public virtual string GetEcowittVideoUrl(string mac)
		{
			cumulus.LogMessage("GetEcowittVideoUrl: Not implemented for this station");
			return string.Empty;
		}

		public int StationFreeMemory;
		public int ExtraStationFreeMemory;
		public int StationRuntime;
		public TimeSpan StationUptime;
		public TimeSpan StationLinkUptime;

		public QueryDayFile DayFileQuery;

		private Logger SnowLog;

		protected WeatherStation(Cumulus cumulus, int stationId)
		{
			// save the reference to the owner
			this.cumulus = cumulus;
			StationId = stationId;

			// if we are an extra Stations, then don't do any of the "normal" intialisation
			if (stationId > 0)
			{
				return;
			}

			// initialise the monthly array of records - element zero is not used
			for (var i = 1; i <= 12; i++)
			{
				Records.MonthlyRecs[i] = new Records();
			}

			MetData.CumulusForecast = cumulus.Trans.ForecastNotAvailable;
			MetData.WsForecast = cumulus.Trans.ForecastNotAvailable;

			MetData.ExtraTemp = new double?[17];
			MetData.ExtraHum = new double?[17];
			MetData.ExtraDewPoint = new double?[17];
			MetData.UserTemp = new double?[9];
			MetData.SoilTemp = new double?[17];
			MetData.SoilEc = new int?[17];

			windcounts = new double[16];
			WindRecent = new TWindRecent[MaxWindRecent];
			WindVec = new TWindVec[MaxWindRecent];

			// set some hi/lo descriptions
			Record.Captions = cumulus.Trans.DataCaptions;

			// Open database (create file if it doesn't exist)
			var flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;

			RecentDataDb = new SQLiteConnection(new SQLiteConnectionString(cumulus.dbfile, flags, false, null, null, null, null, "yyyy-MM-dd HH:mm:ss"));
			CheckSqliteDatabase(false);
			RecentDataDb.CreateTable<RecentData>();
			RecentDataDb.CreateTable<SqlCache>();
			RecentDataDb.CreateTable<CWindRecent>();
			RecentDataDb.Execute("create table if not exists WindRecentPointer (pntr INTEGER)");
			RecentDataDb.CreateTable<DayFileRec>();
			RecentDataDb.CreateTable<RecentAqData>();
			// switch off full synchronisation - the data base isn't that critical and we get a performance boost
			RecentDataDb.Execute("PRAGMA synchronous = NORMAL");

			// preload the failed sql cache - if any
			ReloadFailedMySQLCommands();

			ReadTodayFile();
			ReadYesterdayFile();
			ReadAlltimeIniFile();
			ReadMonthlyAlltimeIniFile();
			ReadMonthIniFile();
			ReadYearIniFile();
			_ = LoadDayFile();
			CheckMonthlyLogFile(cumulus.LastUpdateTime, 0);
			if (cumulus.StationOptions.LogExtraSensors)
			{
				CheckMonthlyLogFile(cumulus.LastUpdateTime, 1);
			}
			if (cumulus.AirLinkInEnabled)
			{
				CheckMonthlyLogFile(cumulus.LastUpdateTime, 2);
			}

			GetRainCounter();
			GetRainFallTotals();

			// Snow stuff
			for (var i = 1; i <= 4; i++)
			{
				SnowDepthAverage[i] = new SmoothingFilter(
					medianMins: cumulus.SnowDepthMedianMins,
					timeConstantMinutes: cumulus.SnowDepthEmaTimeMins,
					clipDelta: cumulus.SnowDepthClipDelta
				);
			}
			LoadSnowDepthAverage(cumulus.LastUpdateTime);

			versionCheckTime = new DateTime(1, 1, 1, Program.RandGenerator.Next(0, 23), Program.RandGenerator.Next(0, 59), 0, DateTimeKind.Local);

			StationData.SensorReception = [];
			StationData.SensorRssi = [];

			DayFileQuery = new QueryDayFile(RecentDataDb);
		}

		private void CheckSqliteDatabase(bool giveup)
		{
			var rebuild = false;
			var errorCount = 0;
			try
			{
				cumulus.LogMessage("Checking SQLite integrity...");
				var cmd = RecentDataDb.CreateCommand("PRAGMA quick_check;");
				var res = cmd.ExecuteQueryScalars<string>();

				errorCount = res.Count();

				if (errorCount == 1 && res.First() == "ok")
				{
					cumulus.LogMessage("SQLite integrity check OK");
					errorCount = 0;
				}
				else
				{
					foreach (var row in res)
					{
						cumulus.LogErrorMessage("SQLite integrity check result: " + row.Replace("\n", "\n    "));
						if (row == "database disk image is malformed")
							rebuild = true;
					}
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
				var flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
				RecentDataDb = new SQLiteConnection(new SQLiteConnectionString(cumulus.dbfile, flags, true));
			}
			else if (errorCount > 1)
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
					var flags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite;
					RecentDataDb = new SQLiteConnection(new SQLiteConnectionString(cumulus.dbfile, flags, true));
				}
			}

			// drop old format tables if they exist
			var tableInfo = RecentDataDb.GetTableInfo("RecentData");
			var timestampColumn = tableInfo.FirstOrDefault(col => col.Name == "Timestamp");
			if (timestampColumn != null && timestampColumn.ColumnType != "INTEGER")
			{
				RecentDataDb.DropTable<RecentData>();
			}

			tableInfo = RecentDataDb.GetTableInfo("RecentAqData");
			timestampColumn = tableInfo.FirstOrDefault(col => col.Name == "Timestamp");
			if (timestampColumn != null && timestampColumn.ColumnType != "INTEGER")
			{
				RecentDataDb.DropTable<RecentAqData>();
			}

			tableInfo = RecentDataDb.GetTableInfo("CWindRecent");
			timestampColumn = tableInfo.FirstOrDefault(col => col.Name == "Timestamp");
			if (timestampColumn != null && timestampColumn.ColumnType != "INTEGER")
			{
				RecentDataDb.DropTable<CWindRecent>();
			}
		}

		/// <summary>
		/// Checks monthly log files for corruption on the last line
		/// </summary>
		/// <param name="logDate">Log date to check</param>
		/// <param name="logtype">0: Monthly, 1:Extra, 2:AirLink</param>
		private void CheckMonthlyLogFile(DateTime logDate, int logtype)
		{
			// A crude check for corruption, just see if the last line starts with nulls
			// if it does resave the file missing the last line
			var fileName = string.Empty;
			var prefix = string.Empty;

			switch (logtype)
			{
				case 0:
					fileName = cumulus.GetLogFileName(logDate);
					prefix = "Monthly log file";
					break;
				case 1:
					fileName = cumulus.GetExtraLogFileName(logDate);
					prefix = "Monthly Extra log file";
					break;
				case 2:
					fileName = cumulus.GetAirLinkLogFileName(logDate);
					prefix = "AirLink log file";
					break;
			}


			if (File.Exists(fileName))
			{
				try
				{
					var rewrite = false;
					var lines = File.ReadAllLines(fileName);

					while (string.IsNullOrEmpty(lines[^1]))
					{
						cumulus.LogMessage($"{prefix} {fileName} empty line removed");
						lines = lines.Take(lines.Length - 1).ToArray();
						rewrite = true;
					}
					//Strip the "null line" from file
					while (lines[^1][0] < 32)
					{
						cumulus.LogMessage($"{prefix} {fileName} Removed last line of nul's from file");
						lines = lines.Take(lines.Length - 1).ToArray();
						rewrite = true;
					}

					if (rewrite)
					{
						File.WriteAllLines(fileName, lines);
					}
					else
					{
						cumulus.LogMessage($"{prefix} {fileName} Checked OK");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "CheckMonthlyLogFile: Error");
				}
			}
			else
			{
				cumulus.LogMessage($"{prefix} check skipped - no file exists");
			}
		}

		public void ReloadFailedMySQLCommands()
		{
			while (cumulus.MySqlFuncs.MySqlFailedList.TryDequeue(out var _))
			{
				// do nothing
			}

			// preload the failed sql cache - if any
			var data = RecentDataDb.Query<SqlCache>("SELECT * FROM SqlCache ORDER BY key");

			foreach (var rec in data)
			{
				cumulus.MySqlFuncs.MySqlFailedList.Enqueue(rec);
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
			var raindaystartfound = false;
			var raincounterfound = false;
			var midnightrainfound = false;

			var LogFile = cumulus.GetLogFileName(cumulus.LastUpdateTime);
			double raincounter = 0;
			double midnightraincounter = 0;
			double raindaystart = 0;

			var logdate = "00/00/00";
			var prevlogdate = "00/00/00";

			var todaydatestring = cumulus.LastUpdateTime.ToString("dd/MM/yy");

			var meteoDate = cumulus.MeteoDate(cumulus.LastUpdateTime);
			meteoDate = meteoDate.Date.AddHours(-cumulus.GetHourInc(cumulus.LastUpdateTime));

			var inv = CultureInfo.InvariantCulture.NumberFormat;

			cumulus.LogMessage("GetRainCounter: Finding raintoday from logfile " + LogFile);

			if (File.Exists(LogFile))
			{
				var linenum = 0;
				try
				{
					var lines = File.ReadAllLines(LogFile);

					foreach (var line in lines)
					{
						// now process each record to get the last "raintoday" figure
						linenum++;
						var st = new List<string>(line.Split(','));
						if (st.Count > 0)
						{
							var raintoday = double.Parse(st[9], inv);
							raincounter = double.Parse(st[11], inv);

							raincounterfound = true;

							// get date of this entry
							logdate = st[0][..8];

							if (logdate != prevlogdate && todaydatestring == logdate && initialiseMidnightRain && !midnightrainfound || cumulus.RolloverHour == 0 && initialiseRainDayStart && !raindaystartfound)
							{
								if (initialiseMidnightRain)
								{
									// this is the first entry of a new day AND the new day is today
									midnightrainfound = true;
									midnightraincounter = raincounter - raintoday;
									cumulus.LogMessage($"GetRainCounter: Midnight rain counter {midnightraincounter:F4} found in the following entry:");
								}

								if (initialiseRainDayStart)
								{
									raindaystartfound = true;
									raindaystart = raincounter - raintoday;
									cumulus.LogMessage($"GetRainCounter: Rain day start counter {raindaystart:F4} found in the following entry:");
								}
								cumulus.LogMessage(line);
							}

							if (initialiseRainDayStart && !raindaystartfound && cumulus.RolloverHour != 0)
							{
								var logDateTime = long.Parse(st[1]).LocalFromUnixTime();
								if (logDateTime >= meteoDate)
								{
									raindaystartfound = true;
									raindaystart = raincounter - raintoday;
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
					if (Math.Abs(MetData.MidnightRainCount - midnightraincounter) < Math.Pow(10, -cumulus.RainDPlaces))
					{
						cumulus.LogMessage($"GetRainCounter: Rain day start counter {MetData.RainCounterDayStart:F4} and midnight rain counter {midnightraincounter:F4} match within rounding error, setting midnight rain to rain day start value");
						MetData.MidnightRainCount = MetData.RainCounterDayStart;
					}
					else
					{
						cumulus.LogMessage($"GetRainCounter: Midnight rain counter found, setting existing midnight rain counter {MetData.MidnightRainCount:F4} to log file value {midnightraincounter:F4}");
						MetData.MidnightRainCount = midnightraincounter;
					}

					initialiseMidnightRain = false;
				}
				else
				{
					cumulus.LogMessage("GetRainCounter: Midnight rain counter not found, setting midnight count to raindaystart = " + MetData.RainCounterDayStart);
					MetData.MidnightRainCount = MetData.RainCounterDayStart;
				}

			}

			if (logdate[..2] == "01" && logdate.Substring(3, 2) == cumulus.RainSeasonStart.ToString("D2") && cumulus.Manufacturer == Cumulus.StationManufacturer.DAVIS)
			{
				// special case: rain counter is about to be reset
				//TODO: MC: Hmm are there issues here, what if the console clock is wrong and it does not reset for another hour, or it already reset and we have had rain since?
				var month = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(cumulus.RainSeasonStart);
				cumulus.LogMessage($"GetRainCounter: Special case, Davis station on 1st of {month}. Set midnight rain count to zero");
				MetData.MidnightRainCount = 0;
			}

			if (initialiseRainDayStart && raindaystartfound)
			{
				cumulus.LogMessage($"GetRainCounter: Rain day start counter found, setting existing start rain counter {MetData.RainCounterDayStart:F4} to log file value {raindaystart:F4}");
				MetData.RainCounterDayStart = raindaystart;
				initialiseRainDayStart = false;
			}

			// If we do not have a rain counter value for start of day from Today.ini, then use the last value from the log file
			if (initialiseRainCounter && raincounterfound)
			{
				cumulus.LogMessage($"GetRainCounter: Rain counter found, setting existing rain counter {MetData.RainCounter:F4} to log file value {raincounter:F4}");
				MetData.RainCounter = raincounter;
				initialiseRainCounter = false;
			}

			if (MetData.RainCounter < 0)
			{
				cumulus.LogMessage("GetRainCounter: Rain counter negative, setting to zero");
				MetData.RainCounter = 0;
			}
		}

		public void GetRainFallTotals()
		{
			cumulus.LogMessage($"GetRainFallTotals: Getting rain totals, rain season start = {cumulus.RainSeasonStart}, rain week start = {cumulus.RainWeekStart}-{(DayOfWeek) cumulus.RainWeekStart}");
			RainThisWeek = 0;
			RainThisMonth = 0;
			RainThisYear = 0;
			// get today's date for month check; allow for 0900 roll-over
			var hourInc = cumulus.GetHourInc();
			var ModifiedNow = DateTime.Now.AddHours(hourInc);
			// avoid any funny locale peculiarities on date formats
			var Today = ModifiedNow.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
			cumulus.LogMessage("GetRainFallTotals: Today = " + Today);
			// get today's date offset by rain season start for year check
			var offsetYearToday = ModifiedNow.AddMonths(-(cumulus.RainSeasonStart - 1)).Year;
			// get this weeks date offset
			var dasysSinceStartOfWeek = (int) ModifiedNow.DayOfWeek - cumulus.RainWeekStart;
			if (dasysSinceStartOfWeek < 0)
			{
				dasysSinceStartOfWeek += 7;
			}
			var offsetWeek = ModifiedNow.AddDays(-dasysSinceStartOfWeek);


			try
			{
				foreach (var rec in MetData.DayFile)
				{
					var offsetLoggedYear = rec.Date.AddMonths(-(cumulus.RainSeasonStart - 1)).Year;
					// This year?
					if (offsetLoggedYear == offsetYearToday)
					{
						RainThisYear += rec.TotalRain;
					}
					// This month?
					if (rec.Date.Month == ModifiedNow.Month && rec.Date.Year == ModifiedNow.Year)
					{
						RainThisMonth += rec.TotalRain;
					}
					// This Week?
					if (rec.Date >= offsetWeek.Date)
					{
						RainThisWeek += rec.TotalRain;
					}

				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("GetRainfallTotals: Error - " + ex.Message);
			}

			cumulus.LogMessage("GetRainFallTotals: Rainthisweek from dayfile: " + RainThisWeek);
			cumulus.LogMessage("GetRainFallTotals: Rainthismonth from dayfile: " + RainThisMonth);
			cumulus.LogMessage("GetRainFallTotals: Rainthisyear from dayfile: " + RainThisYear);

			// Add in year-to-date rain (if necessary)
			if (cumulus.YTDrainyear == Convert.ToInt32(Today.Substring(6, 2)) + 2000)
			{
				RainThisYear += cumulus.YTDrain;
				cumulus.LogMessage($"GetRainFallTotals: Adding YTD rain: {cumulus.YTDrain}, new Rainthisyear: {RainThisYear}");
			}
			MetData.RainWeek = RainThisWeek;
			MetData.RainMonth = RainThisMonth;
			MetData.RainYear = RainThisYear;
		}

		private void LoadSnowDepthAverage(DateTime ts)
		{
			if (cumulus.LaserPrimarySnowSensor == 0) return;

			var datefrom = ts.AddMinutes(-30);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			var logFile = cumulus.GetExtraLogFileName(filedate);
			var finished = false;

			cumulus.LogMessage("LoadSnowDepthAverage: Attempting to load last 30 minutes laser depth values");

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line))
									continue;

								var rec = new ExtraLogFileRec(line);
								entrydate = rec.DateTime;

								if (entrydate >= datefrom && entrydate <= dateto)
								{
									for (var i = 1; i <= 4; i++)
									{
										if (rec.LaserDepth[i].HasValue)
										{
											_ = SnowDepthAverage[i].Update(entrydate, rec.LaserDepth[i].Value);
										}
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"LoadSnowDepthAverage: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"LoadSnowDepthAverage: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"LoadSnowDepthAverage: Too many errors reading {logFile} - aborting load of data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"LoadSnowDepthAverage: Error reading the logfile {logFile} : {e.Message}");
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
					logFile = cumulus.GetExtraLogFileName(filedate);
				}
			} while (!finished);

			cumulus.LogMessage("LoadSnowDepthAverage: values loaded");
		}

		public void UpdateYearMonthRainfall()
		{
			var _week = MetData.RainWeek;
			var _month = MetData.RainMonth;
			var _year = MetData.RainYear;
			MetData.RainWeek += MetData.RainToday;
			MetData.RainMonth = RainThisMonth + MetData.RainToday;
			MetData.RainYear = RainThisYear + MetData.RainToday;
			cumulus.LogMessage($"Rainthisweek Updated from: {_week.ToString(cumulus.RainFormat)} to: {MetData.RainWeek.ToString(cumulus.RainFormat)}");
			cumulus.LogMessage($"Rainthismonth Updated from: {_month.ToString(cumulus.RainFormat)} to: {MetData.RainMonth.ToString(cumulus.RainFormat)}");
			cumulus.LogMessage($"Rainthisyear Updated from: {_year.ToString(cumulus.RainFormat)} to: {MetData.RainYear.ToString(cumulus.RainFormat)}");

		}

		public void UpdateWeekRainfall()
		{
			var _rainWeek = MetData.RainWeek;
			RainThisWeek = 0;
			// get this weeks date offset, allow for meteo
			// get the difference in days
			var diff = (7 + ((int) CurrentDate.DayOfWeek - cumulus.RainWeekStart)) % 7;
			var offsetWeek = CurrentDate.AddDays(-1 * diff).Date;
			// recalculate rain this week - we may have gone over a week boundary
			RainThisWeek = MetData.DayFile.Where(day => day.Date >= offsetWeek).Sum(day => day.TotalRain);
			MetData.RainWeek = RainThisWeek + MetData.RainToday;
			cumulus.LogMessage($"UpdateWeekRainfall: Updated RainWeek from {_rainWeek.ToString(cumulus.RainFormat)} to {MetData.RainWeek.ToString(cumulus.RainFormat)}");
		}

		public void UpdatePressureTrendString()
		{
			double threeHourlyPressureChangeMb = 0;

			switch (cumulus.Units.Press)
			{
				case 0:
				case 1:
					threeHourlyPressureChangeMb = MetData.PressTrendVal * 3;
					break;
				case 2:
					threeHourlyPressureChangeMb = MetData.PressTrendVal * 3 / 0.0295333727;
					break;
				case 3:
					threeHourlyPressureChangeMb = MetData.PressTrendVal * 30;
					break;
			}

			MetData.PressTrendStr = threeHourlyPressureChangeMb switch
			{
				> 6 => cumulus.Trans.Risingveryrapidly,
				> 3.5 => cumulus.Trans.Risingquickly,
				> 1.5 => cumulus.Trans.Rising,
				> 0.1 => cumulus.Trans.Risingslowly,
				> -0.1 => cumulus.Trans.Steady,
				> -1.5 => cumulus.Trans.Fallingslowly,
				> -3.5 => cumulus.Trans.Falling,
				> -6 => cumulus.Trans.Fallingquickly,
				_ => cumulus.Trans.Fallingveryrapidly,
			};
		}

		public void CheckMonthlyAlltime(string index, double value, bool higher, DateTime timestamp)
		{
			lock (monthlyalltimeIniThreadLock)
			{
				bool recordbroken;

				// Make the delta relate to the precision for derived values such as feels like
				string[] derivedVals = ["HighHeatIndex", "HighAppTemp", "LowAppTemp", "LowChill", "HighHumidex", "HighDewPoint", "LowDewPoint", "HighFeelsLike", "LowFeelsLike"];

				var epsilon = derivedVals.Contains(index) ? Math.Pow(10, -cumulus.TempDPlaces) : 0.001; // required difference for new record

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
					DateTime adjustedTS = timestamp.AddHours(cumulus.GetHourInc());

					month = adjustedTS.Month;
					day = adjustedTS.Day;
					year = adjustedTS.Year;
				}

				var rec = Records.MonthlyRecs[month][index];

				var oldvalue = rec.Val;

				if (higher)
				{
					// check new value is higher than existing record
					recordbroken = value - oldvalue >= epsilon;
				}
				else
				{
					// check new value is lower than existing record
					recordbroken = oldvalue - value >= epsilon;
				}

				if (recordbroken)
				{
					// records which apply to whole days or months need their timestamps adjusting
					if (index == "MonthlyRain" || index == "DailyRain")
					{
						var CurrentMonthTS = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local);
						SetMonthlyAlltime(rec, value, CurrentMonthTS);
					}
					else
					{
						SetMonthlyAlltime(rec, value, timestamp);
					}
				}
			}
		}

		private static string FormatDateTime(string fmt, DateTime timestamp)
		{
			return timestamp.ToString(fmt);
		}


		public int CurrentDay { get; set; }

		public int CurrentMonth { get; set; }

		public int CurrentYear { get; set; }

		public DateTime CurrentDate { get; set; }


		public double WindAverageUncalibrated { get; set; } = 0;


		public DateTime lastSpikeRemoval = DateTime.MinValue;
		private double previousPress = 9999;
		public double previousGust = 999;
		private double previousWind = 999;
		private int previousHum = 999;
		private int previousInHum = 999;
		private double previousTemp = 999;
		private double previousInTemp = 999;


		public readonly SmoothingFilter[] SnowDepthAverage = new SmoothingFilter[5];

		public void StartSecondsTimer()
		{
			lastSecond = DateTime.UtcNow.ToUnixTime();
			lastMinute = DateTime.Now.Minute;
			lastHour = DateTime.Now.Hour;
			secondTimer = new Timer(250);
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
			var nowSec = timeNow.ToUnixTime();
			if (nowSec == lastSecond)
			{
				// skip this interval it's the same second
				return;
			}
			else if (nowSec - lastSecond > 300)
			{
				// check for the clock skipping forward more than 5 minutes
				// if this happens it may be because the computer was suspended
				// we will terminate so that the program can be restarted and the data recovered
				// Exit code 999 is used to prevent a clean shutdown, it aborts the program, not saving the current state/datetime if it hasn't already been saved
				secondTimer.Stop();
				if (Program.ExitSystemToken.IsCancellationRequested)
				{
					cumulus.LogMessage($"**** Clock skipped forward more than 5 minutes, last second was {lastSecond}, now is {nowSec}. Already shutting down, so no more action required");
				}
				else
				{
					cumulus.LogMessage($"**** Clock skipped forward more than 5 minutes, last second was {lastSecond}, now is {nowSec}. Assuming we are resuming from an undetected computer sleep and aborting Cumulus ****");
					Environment.ExitCode = 999;
					Program.ExitSystemTokenSource.Cancel();
				}
				return;
			}

			DataDateTime = timeNow;
			lastSecond = nowSec;

			if (timeNow.Minute != lastMinute)
			{
				lastMinute = timeNow.Minute;

				if (timeNow.Minute % 10 == 0)
				{
					TenMinuteChanged();
				}

				if (timeNow.Hour != lastHour)
				{
					lastHour = timeNow.Hour;
					HourChanged(timeNow);
				}

				MinuteChanged(timeNow);

				if (DataStopped)
				{
					// check if we want to exit on data stopped
					if (cumulus.ProgramOptions.DataStoppedExit && DataStoppedTime.AddMinutes(cumulus.ProgramOptions.DataStoppedMins) < DateTime.UtcNow)
					{
						cumulus.LogMessage($"**** Exiting Cumulus due to Data Stopped condition for > {cumulus.ProgramOptions.DataStoppedMins} minutes ****");
						Program.ExitSystemTokenSource.Cancel();
					}
					// No data coming in, do not do anything else
					return;
				}
			}

			// send current data to web-socket every 5 seconds, unless it has already been sent within the 10 seconds
			if (LastDataReadTimestamp.AddSeconds(5) < timeNow.ToUniversalTime() && (int) timeNow.TimeOfDay.TotalMilliseconds % 10000 <= 500)
			{
				_ = sendWebSocketData();
			}

			// lets spread some the processing over the minute, 10 seconds past the minute...
			var millisecs = (int) timeNow.TimeOfDay.TotalMilliseconds % 60000;
			if (millisecs >= 10000 && millisecs < 10500)
			{
				MinutePlus10Changed();
			}

			if (cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Enabled && (int) timeNow.TimeOfDay.TotalSeconds % cumulus.MySqlFuncs.MySqlSettings.CustomSecs.Interval == 0)
			{
				cumulus.CustomMysqlSecondsChanged();
			}

			cumulus.MQTTSecondChanged(timeNow);
		}

		private async Task sendWebSocketData(bool wait = false)
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
				if (!await webSocketSemaphore.WaitAsync(wait ? 0 : 600))
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
				if (webSocketSemaphore.CurrentCount == 0)
				{
					webSocketSemaphore.Release();
				}
			}
			catch
			{
				// do nothing
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
			cumulus.FirmwareAlarm.ClearAlarm();
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

			foreach (var alarm in cumulus.UserAlarms)
			{
				alarm.ClearAlarm();
			}
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

			MetData.CurrentSolarMax = AstroLib.SolarMax(now, (double) cumulus.Longitude, (double) cumulus.Latitude, ConvertUnits.AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.SolarOptions);

			if (!DataStopped)
			{
				if (MetData.Pressure > 0 && TempReadyToPlot && WindReadyToPlot || cumulus.StationOptions.NoSensorCheck)
				{
					// increment wind run by one minute's worth of average speed

					MetData.WindRunToday += MetData.WindAverage * WindRunHourMult[cumulus.Units.Wind] / 60.0;

					CheckForWindrunHighLow(now);

					CalculateDominantWindBearing(MetData.WindAvgBearing, MetData.WindAverage, 1);

					if (MetData.Temperature < cumulus.ChillHourThreshold && MetData.Temperature > cumulus.ChillHourBase)
					{
						// add 1 minute to chill hours
						MetData.ChillHours += 1.0 / 60.0;
					}

					// update sunshine hours
					if (cumulus.SolarOptions.UseBlakeLarsen)
					{
						ReadBlakeLarsenData();
					}
					else if (cumulus.SolarOptions.UseSunshineSensor)
					{
						// do nothing, we have a separate sensor counting the sunshine hours
					}
					else if (MetData.SolarRad.HasValue && MetData.SolarRad > MetData.CurrentSolarMax * cumulus.SolarOptions.SunThreshold / 100.0 && MetData.SolarRad >= cumulus.SolarOptions.SolarMinimum)
					{
						MetData.SunshineHours += 1.0 / 60.0;
					}

					// update heating/cooling degree days
					UpdateDegreeDays(1);

					if (!first_temp)
					{
						// update temperature average items
						MetData.TempSamplesToday++;
						MetData.TempTotalToday += MetData.Temperature;
					}

					DoTrendValues(now);
					AddRecentDataWithAq(now);

					UpdateAirQualityDb();

					// calculate ET just before the hour so it is included in the correct day at roll over - only affects 9am met days really
					if (cumulus.StationOptions.CalculatedET && now.Minute == 59)
					{
						CalculateEvapotranspiration(now);
					}


					if (now.Minute % Cumulus.logints[cumulus.DataLogInterval] == 0)
					{
						// skip the log at rollover, it will be done by DayReset
						if (now.Hour != cumulus.GetRolloverHour(now) || now.Minute != 0)
						{
							_ = cumulus.DoLogFile(now, true);
						}

						if (cumulus.StationOptions.LogExtraSensors)
						{
							// Log the maximum 24hr snow accumulation before resetting it
							_ = cumulus.DoExtraLogFile(now);

							if (now.Hour == cumulus.SnowDepthHour && now.Minute == 0)
							{
								// reset the accumulated snow depth(s)
								for (var i = 0; i < MetData.Snow24h.Length; i++)
								{
									MetData.Snow24h[i] = MetData.LaserDepth[i].HasValue ? 0 : null;
								}
							}
						}

						if (cumulus.AirLinkInEnabled || cumulus.AirLinkOutEnabled)
						{
							_ = cumulus.DoAirLinkLogFile(now);
						}
					}

					// Custom MySQL update - minutes interval
					if (cumulus.MySqlFuncs.MySqlSettings.CustomMins.Enabled)
					{
						_ = cumulus.CustomMysqlMinutesUpdate(now, true);
					}

					// Custom MySQL Timed interval
					if (cumulus.MySqlFuncs.MySqlSettings.CustomTimed.Enabled)
					{
						_ = cumulus.CustomMySqlTimedUpdate(now);
					}

					// Custom HTTP update - minutes interval
					if (cumulus.CustomHttpMinutesEnabled && now.Minute % cumulus.CustomHttpMinutesInterval == 0)
					{
						_ = cumulus.CustomHttpMinutesUpdate();
					}

					// Custom Log files - interval logs
					cumulus.DoCustomIntervalLogs(now);

					if (cumulus.WebIntervalEnabled && cumulus.SynchronisedWebUpdate && now.Minute % cumulus.UpdateInterval == 0)
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
								cumulus.ftpThread.Interrupt();
							cumulus.LogMessage("Trying new web update");
							cumulus.WebUpdating = 1;
							cumulus.ftpThread = new Thread(() => _ = cumulus.DoHTMLFiles()) { IsBackground = true };
							cumulus.ftpThread.Start();
						}
						else
						{
							cumulus.WebUpdating = 1;
							cumulus.ftpThread = new Thread(() => _ = cumulus.DoHTMLFiles()) { IsBackground = true };
							cumulus.ftpThread.Start();
						}
					}
					// We also want to kick off DoHTMLFiles if local copy is enabled
					else if (cumulus.FtpOptions.LocalCopyEnabled && cumulus.SynchronisedWebUpdate && now.Minute % cumulus.UpdateInterval == 0)
					{
						cumulus.ftpThread = new Thread(() => _ = cumulus.DoHTMLFiles()) { IsBackground = true };
						cumulus.ftpThread.Start();
					}

					if (cumulus.Wund.Enabled && now.Minute % cumulus.Wund.Interval == 0 && cumulus.Wund.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.Wund.ID))
					{
						_ = cumulus.Wund.DoUpdate(now);
					}

					if (cumulus.Windy.Enabled && now.Minute % cumulus.Windy.Interval == 0 && (!string.IsNullOrWhiteSpace(cumulus.Windy.PW) || !string.IsNullOrWhiteSpace(cumulus.Windy.ApiKey)))
					{
						_ = cumulus.Windy.DoUpdate(now);
					}

					if (cumulus.WindGuru.Enabled && now.Minute % cumulus.WindGuru.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.WindGuru.ID))
					{
						_ = cumulus.WindGuru.DoUpdate(now);
					}

					if (cumulus.AWEKAS.Enabled && cumulus.AWEKAS.SynchronisedUpdate && now.Minute % (cumulus.AWEKAS.Interval / 60) == 0 && !string.IsNullOrWhiteSpace(cumulus.AWEKAS.ID))
					{
						_ = cumulus.AWEKAS.DoUpdate(now);
					}

					if (cumulus.WCloud.Enabled && now.Minute % cumulus.WCloud.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.WCloud.ID))
					{
						_ = cumulus.WCloud.DoUpdate(now);
					}

					if (cumulus.OpenWeatherMap.Enabled && now.Minute % cumulus.OpenWeatherMap.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.OpenWeatherMap.ID))
					{
						_ = cumulus.OpenWeatherMap.DoUpdate(now);
					}

					if (cumulus.PWS.Enabled && now.Minute % cumulus.PWS.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.PWS.ID) && !string.IsNullOrWhiteSpace(cumulus.PWS.PW))
					{
						_ = cumulus.PWS.DoUpdate(now);
					}

					if (cumulus.WOW.Enabled && now.Minute % cumulus.WOW.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.WOW.ID) && !string.IsNullOrWhiteSpace(cumulus.WOW.PW))
					{
						_ = cumulus.WOW.DoUpdate(now);
					}

					if (cumulus.WOW_BE.Enabled && now.Minute % cumulus.WOW_BE.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.WOW_BE.ID) && !string.IsNullOrWhiteSpace(cumulus.WOW_BE.PW))
					{
						_ = cumulus.WOW_BE.DoUpdate(now);
					}

					if (cumulus.APRS.Enabled && now.Minute % cumulus.APRS.Interval == 0 && !string.IsNullOrWhiteSpace(cumulus.APRS.ID))
					{
						_ = cumulus.APRS.DoUpdate(now);
					}

					if (cumulus.Bluesky.Enabled)
					{
						_ = cumulus.BlueskyTimedUpdate(now);
					}


					if (cumulus.xapEnabled)
					{
						using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

						var iep1 = new IPEndPoint(IPAddress.Broadcast, cumulus.xapPort);

						var data = Encoding.ASCII.GetBytes(cumulus.xapHeartbeat);

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
						xapReport.Append($"WindM={ConvertUnits.UserWindToMPH(MetData.WindAverage):F1}\n");
						xapReport.Append($"WindK={ConvertUnits.UserWindToKPH(MetData.WindAverage):F1}\n");
						xapReport.Append($"WindGustsM={ConvertUnits.UserWindToMPH(MetData.RecentMaxGust):F1}\n");
						xapReport.Append($"WindGustsK={ConvertUnits.UserWindToKPH(MetData.RecentMaxGust):F1}\n");
						xapReport.Append($"WindDirD={MetData.WindBearing}\n");
						xapReport.Append($"WindDirC={MetData.WindAvgBearing}\n");
						xapReport.Append($"TempC={ConvertUnits.UserTempToC(MetData.Temperature):F1}\n");
						xapReport.Append($"TempF={ConvertUnits.UserTempToF(MetData.Temperature):F1}\n");
						xapReport.Append($"DewC={ConvertUnits.UserTempToC(MetData.Dewpoint):F1}\n");
						xapReport.Append($"DewF={ConvertUnits.UserTempToF(MetData.Dewpoint):F1}\n");
						xapReport.Append($"AirPressure={ConvertUnits.UserPressToMB(MetData.Pressure):F1}\n");
						xapReport.Append($"Rain={ConvertUnits.UserRainToMM(MetData.RainToday):F1}\n");
						xapReport.Append('}');

						data = Encoding.ASCII.GetBytes(xapReport.ToString());

						sock.SendTo(data, iep1);

						sock.Close();
					}

					var wxfile = cumulus.StdWebFiles.SingleOrDefault(item => item.FileName == "wxnow.txt");
					if (wxfile.Create)
					{
						cumulus.CreateWxnowFile();
					}

					cumulus.DoHttpFiles(now);
				}
				else
				{
					cumulus.LogErrorMessage("Minimum data set of pressure, temperature, and wind is not available and NoSensorCheck is not enabled. Skip processing");
				}
			}

			if (!cumulus.HourlyForecast)
			{
				DoForecast(string.Empty, false);
			}

			// Check for a new version of Cumulus once a day
			if (now.Minute == versionCheckTime.Minute && now.Hour == versionCheckTime.Hour)
			{
				cumulus.LogMessage("Checking for latest Cumulus MX version...");
				_ = cumulus.GetLatestVersion();
			}
		}

		private void MinutePlus10Changed()
		{
			if (!DataStopped)
			{
				CheckUserAlarms();
			}

			// If not on windows, check for CPU temp
			try
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists("/sys/class/thermal/thermal_zone0/temp"))
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

		private void TenMinuteChanged()
		{
			cumulus.DoMoonPhase();
			cumulus.MoonAge = MoonriseMoonset.MoonAge();

			ClearAlarms();
		}

		private void HourChanged(DateTime now)
		{
			cumulus.LogMessage("Hour changed: " + now.Hour);
			cumulus.DoSunriseAndSunset();
			cumulus.DoMoonImage();

			if (cumulus.HourlyForecast)
			{
				DoForecast(string.Empty, true);
			}

			var rollHour = Math.Abs(cumulus.GetHourInc());

			if (now.Hour == rollHour)
			{
				DayReset(now);
				Task.Run(() => cumulus.BackupData(true, now));
			}

			if (now.Hour == 0)
			{
				ResetMidnightRain(now);
				ResetSunshineHours(now);
				ResetMidnightTemperatures(now);
			}

			// 9am rollover items
			if (now.Hour == 9)
			{
				Reset9amTemperatures(now);
			}

			// snow reading rollover
			if (now.Hour == cumulus.SnowDepthHour)
			{
				if (cumulus.SnowAutomated > 0)
				{
					CreateNewSnowRecord(now);
				}

				// Reset the 24 hr snow value in MinuteChanged() so we capture the max value for the day
			}

			RemoveOldRecentData(now);

			System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
			//GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false)
		}

		private void CheckForDataStopped()
		{
			// Check whether we have read data since the last clock minute.
			if (LastDataReadTimestamp != DateTime.MinValue && LastDataReadTimestamp == SavedLastDataReadTimestamp && LastDataReadTimestamp < DateTime.UtcNow && DateTime.UtcNow.Subtract(LastDataReadTimestamp) > TimeSpan.FromMinutes(DataTimeoutMins))
			{
				// Data input appears to have has stopped
				if (!DataStopped)
				{
					DataStoppedTime = DateTime.UtcNow;
				}
				DataStopped = true;
				cumulus.DataStoppedAlarm.Triggered = true;
				/*if (RestartIfDataStops)
					cumulus.LogMessage("*** Data input appears to have stopped, restarting")
					ApplicationExec(ParamStr(0), '', SW_SHOW)
					TerminateProcess(GetCurrentProcess, 0)
				*/
				if (cumulus.ReportDataStoppedErrors)
				{
					cumulus.LogErrorMessage("*** Data input appears to have stopped");
				}
			}       // Calculates evapotranspiration based on the data for the last hour and updates the running annual total.
			else
			{
				DataStopped = false;
				cumulus.DataStoppedAlarm.Triggered = false;
			}

			// save the time that data was last read so we can check in a minute's time that it's changed
			SavedLastDataReadTimestamp = LastDataReadTimestamp;
		}




		public static string GetIncrementalLogFileData(string fileName, int prevLastLine, out int newLines)
		{
			string[] data;
			if (prevLastLine == 0)
			{
				data = File.ReadAllLines(fileName);
			}
			else
			{
				data = File.ReadLines(fileName).Skip(prevLastLine).ToArray();
			}

			newLines = data.Length;
			return string.Join(Environment.NewLine, data) + Environment.NewLine;
		}



		public void ResetSunshineHours(DateTime logdate) // called at midnight irrespective of roll-over time
		{
			MetData.YestSunshineHours = MetData.SunshineHours;

			cumulus.LogMessage("Reset sunshine hours, yesterday = " + MetData.YestSunshineHours);

			MetData.SunshineToMidnight = MetData.SunshineHours;
			MetData.SunshineHours = 0;
			MetData.StartOfDaySunHourCounter = MetData.SunHourCounter;
			WriteYesterdayFile(logdate);
		}

		public void ResetMidnightTemperatures(DateTime logdate) // called at midnight irrespective of roll-over time
		{
			DailyHighLow.YestMidnight.LowTemp = DailyHighLow.TodayMidnight.LowTemp;
			DailyHighLow.YestMidnight.HighTemp = DailyHighLow.TodayMidnight.HighTemp;
			DailyHighLow.YestMidnight.LowTempTime = DailyHighLow.TodayMidnight.LowTempTime;
			DailyHighLow.YestMidnight.HighTempTime = DailyHighLow.TodayMidnight.HighTempTime;

			DailyHighLow.TodayMidnight.LowTemp = MetData.Temperature;
			DailyHighLow.TodayMidnight.HighTemp = MetData.Temperature;
			DailyHighLow.TodayMidnight.LowTempTime = logdate;
			DailyHighLow.TodayMidnight.HighTempTime = logdate;

			WriteYesterdayFile(logdate);
		}

		public void Reset9amTemperatures(DateTime logdate) // called at 9am irrespective of roll-over time
		{
			DailyHighLow.Yest9am.LowTemp = DailyHighLow.Today9am.LowTemp;
			DailyHighLow.Yest9am.HighTemp = DailyHighLow.Today9am.HighTemp;
			DailyHighLow.Yest9am.LowTempTime = DailyHighLow.Today9am.LowTempTime;
			DailyHighLow.Yest9am.HighTempTime = DailyHighLow.Today9am.HighTempTime;

			DailyHighLow.Today9am.LowTemp = MetData.Temperature;
			DailyHighLow.Today9am.HighTemp = MetData.Temperature;
			DailyHighLow.Today9am.LowTempTime = logdate;
			DailyHighLow.Today9am.HighTempTime = logdate;

			WriteYesterdayFile(logdate);
		}

		public void CreateNewSnowRecord(DateTime now)
		{
			try
			{
				double? depth = MetData.LaserDepth[cumulus.SnowAutomated].HasValue ? ConvertUnits.LaserToSnow(MetData.LaserDepth[cumulus.SnowAutomated].Value) : null;
				if (depth.HasValue && depth < 0)
				{
					depth = 0;
				}

				var record = new DiaryData
				{
					Date = now.Date,
					Time = now.TimeOfDay,
					SnowDepth = depth,
					Snow24h = MetData.Snow24h[cumulus.SnowAutomated],
					Entry = "Automated entry"
				};

				cumulus.DiaryDB.Insert(record);
				cumulus.LogMessage("Created new automated snow record: " + record.SnowDepth);
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Failed to create automated snow depth record");
			}
		}

		public void SwitchToNormalRunning()
		{
			cumulus.NormalRunning = true;

			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimersAndSensors();
		}

		public void ResetMidnightRain(DateTime timestamp)
		{
			var mrrday = timestamp.Day;

			var mrrmonth = timestamp.Month;

			if (mrrday != MetData.MidnightRainResetDay)
			{
				MetData.MidnightRainCount = MetData.RainCounter;
				MetData.RainSinceMidnight = 0;
				MetData.MidnightRainResetDay = mrrday;
				cumulus.LogMessage("Midnight rain reset, count = " + MetData.RainCounter + " time = " + timestamp.ToShortTimeString());
				if (mrrday == 1 && mrrmonth == 1 && cumulus.StationType == StationTypes.VantagePro)
				{
					// special case: rain counter is about to be reset
					cumulus.LogMessage("Special case, Davis station on 1st Jan. Set midnight rain count to zero");
					MetData.MidnightRainCount = 0;
				}
			}
		}


		public bool FirstForecastDone = false;

		public bool PressReadyToPlot { get; set; }

		public bool first_press { get; set; }


		public bool HaveReadData { get; set; } = false;

		public void SetAlltime(Record rec, double value, DateTime timestamp)
		{
			lock (alltimeIniThreadLock)
			{
				var oldvalue = rec.Val;
				var oldts = rec.Ts;

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

		public void SetMonthlyAlltime(Record rec, double value, DateTime timestamp)
		{
			var oldvalue = rec.Val;
			var oldts = rec.Ts;

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

		private DateTime snowSpikeTime;
		private int rainResetCount = 0;
		private bool SecondChanceRainReset = false;
		private bool initialiseRainDayStart = true;
		private bool initialiseMidnightRain = true;
		private bool initialiseRainCounter = true;
		private double RainThisWeek = 0;
		private double RainThisMonth = 0;
		private double RainThisYear = 0;
		public bool noET = false;
		private int DayResetDay = 0;
		protected bool FirstRun = false;
		public const int MaxWindRecent = 720;
		public readonly double[] WindRunHourMult = [3.6, 1.0, 1.0, 1.0];
		public DateTime LastDataReadTimestamp = DateTime.MinValue;              // Stored in UTC to avoid clock change issues
		public DateTime SavedLastDataReadTimestamp = DateTime.MinValue;         // Stored in UTC to avoid clock change issues
																				// Create arrays with 9 entries, 0 = VP2, 1-8 = WLL TxIds
		public int DavisTotalPacketsReceived = 0;
		public int[] DavisTotalPacketsMissed = [0, 0, 0, 0, 0, 0, 0, 0, 0];
		public int[] DavisNumberOfResynchs = [0, 0, 0, 0, 0, 0, 0, 0, 0];
		public int[] DavisMaxInARow = [0, 0, 0, 0, 0, 0, 0, 0, 0];
		public int[] DavisNumCRCerrors = [0, 0, 0, 0, 0, 0, 0, 0, 0];
		public int[] DavisReceptionPct = [0, 0, 0, 0, 0, 0, 0, 0, 0];
		public int[] DavisTxRssi = [0, 0, 0, 0, 0, 0, 0, 0, 0];
		public string DavisFirmwareVersion = "???";
		public string GW1000FirmwareVersion = "???";

		private bool dayfileReloading;

		public void DayReset(DateTime timestamp)
		{
			var drday = timestamp.Day;
			var yesterday = timestamp.AddDays(-1);
			cumulus.LogMessage("=== Day reset, today = " + drday);

			DayResetInProgress = true;

			if (drday != DayResetDay)
			{
				cumulus.LogMessage("=== Day reset for " + yesterday.Date);

				var day = timestamp.Day;
				var month = timestamp.Month;
				DayResetDay = drday;

				// any last updates?
				DoTrendValues(timestamp, true);

				if (cumulus.MySqlFuncs.MySqlSettings.CustomRollover.Enabled)
				{
					_ = cumulus.CustomMysqlRollover(cumulus.NormalRunning);
				}

				if (cumulus.CustomHttpRolloverEnabled)
				{
					_ = cumulus.CustomHttpRolloverUpdate();
				}

				cumulus.DoCustomDailyLogs(timestamp);

				// First save today's extremes, do not wait for this
				_ = DoDayfile(timestamp);

				// and the log file, but only if the Stations is initialised - do wait for this
				if (cumulus.Station != null)
				{
					cumulus.DoLogFile(timestamp, cumulus.NormalRunning).Wait();
				}

				cumulus.LogMessage("Raincounter = " + MetData.RainCounter + " Raindaystart = " + MetData.RainCounterDayStart);

				// Calculate yesterday"s rain, allowing for the multiplier -
				// raintotal && raindaystart are not calibrated
				MetData.RainYesterday = (MetData.RainCounter - MetData.RainCounterDayStart) * cumulus.Calib.Rain.Mult;
				cumulus.LogMessage("Rainyesterday (calibrated) set to " + MetData.RainYesterday);

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
				var ryest1000 = Convert.ToInt32(MetData.RainYesterday * 1000.0);

				cumulus.LogMessage("RainDayThreshold = " + cumulus.RainDayThreshold);
				cumulus.LogMessage("rdt1000=" + rdthresh1000 + " ry1000=" + ryest1000);

				if (ryest1000 >= rdthresh1000)
				{
					// It rained yesterday
					cumulus.LogMessage("Yesterday was a rain day");
					MetData.ConsecutiveRainDays++;
					MetData.ConsecutiveDryDays = 0;
					cumulus.LogMessage("Consecutive rain days = " + MetData.ConsecutiveRainDays);
					// check for highs
					if (MetData.ConsecutiveRainDays > Records.ThisMonth.LongestWetPeriod.Val)
					{
						Records.ThisMonth.LongestWetPeriod.Val = MetData.ConsecutiveRainDays;
						Records.ThisMonth.LongestWetPeriod.Ts = yesterday;
						WriteMonthIniFile();
					}

					if (MetData.ConsecutiveRainDays > Records.ThisYear.LongestWetPeriod.Val)
					{
						Records.ThisYear.LongestWetPeriod.Val = MetData.ConsecutiveRainDays;
						Records.ThisYear.LongestWetPeriod.Ts = yesterday;
						WriteYearIniFile();
					}

					if (MetData.ConsecutiveRainDays > Records.AllTime.LongestWetPeriod.Val)
					{
						SetAlltime(Records.AllTime.LongestWetPeriod, MetData.ConsecutiveRainDays, yesterday);
					}

					CheckMonthlyAlltime("LongestWetPeriod", MetData.ConsecutiveRainDays, true, yesterday);
				}
				else
				{
					// It didn't rain yesterday
					cumulus.LogMessage("Yesterday was a dry day");
					MetData.ConsecutiveDryDays++;
					MetData.ConsecutiveRainDays = 0;
					cumulus.LogMessage("Consecutive dry days = " + MetData.ConsecutiveDryDays);

					// check for highs
					if (MetData.ConsecutiveDryDays > Records.ThisMonth.LongestDryPeriod.Val)
					{
						Records.ThisMonth.LongestDryPeriod.Val = MetData.ConsecutiveDryDays;
						Records.ThisMonth.LongestDryPeriod.Ts = yesterday;
						WriteMonthIniFile();
					}

					if (MetData.ConsecutiveDryDays > Records.ThisYear.LongestDryPeriod.Val)
					{
						Records.ThisYear.LongestDryPeriod.Val = MetData.ConsecutiveDryDays;
						Records.ThisYear.LongestDryPeriod.Ts = yesterday;
						WriteYearIniFile();
					}

					if (MetData.ConsecutiveDryDays > Records.AllTime.LongestDryPeriod.Val)
					{
						SetAlltime(Records.AllTime.LongestDryPeriod, MetData.ConsecutiveDryDays, yesterday);
					}

					CheckMonthlyAlltime("LongestDryPeriod", MetData.ConsecutiveDryDays, true, yesterday);
				}

				// offset high temp today timestamp to allow for 0900 roll-over
				int hr;
				int mn;
				DateTime ts;
				try
				{
					hr = DailyHighLow.Today.HighTempTime.Hour;
					mn = DailyHighLow.Today.HighTempTime.Minute;
					ts = timestamp.Date + new TimeSpan(hr, mn, 0);

					if (hr >= cumulus.GetRolloverHour(ts))
						// time is between roll-over hour && midnight
						// so subtract a day
						ts = ts.AddDays(-1);
				}
				catch
				{
					ts = timestamp.AddDays(-1);
				}

				if (DailyHighLow.Today.HighTemp < Records.AllTime.LowMaxTemp.Val)
				{
					SetAlltime(Records.AllTime.LowMaxTemp, DailyHighLow.Today.HighTemp, ts);
				}

				CheckMonthlyAlltime("LowMaxTemp", DailyHighLow.Today.HighTemp, false, ts);

				if (DailyHighLow.Today.HighTemp < Records.ThisMonth.LowMaxTemp.Val)
				{
					Records.ThisMonth.LowMaxTemp.Val = DailyHighLow.Today.HighTemp;
					try
					{
						hr = DailyHighLow.Today.HighTempTime.Hour;
						mn = DailyHighLow.Today.HighTempTime.Minute;
						Records.ThisMonth.LowMaxTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.GetRolloverHour(timestamp))
							// time is between roll-over hour && midnight
							// so subtract a day
							Records.ThisMonth.LowMaxTemp.Ts = Records.ThisMonth.LowMaxTemp.Ts.AddDays(-1);
					}
					catch
					{
						Records.ThisMonth.LowMaxTemp.Ts = timestamp.AddDays(-1);
					}

					WriteMonthIniFile();
				}

				if (DailyHighLow.Today.HighTemp < Records.ThisYear.LowMaxTemp.Val)
				{
					Records.ThisYear.LowMaxTemp.Val = DailyHighLow.Today.HighTemp;
					try
					{
						hr = DailyHighLow.Today.HighTempTime.Hour;
						mn = DailyHighLow.Today.HighTempTime.Minute;
						Records.ThisYear.LowMaxTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.GetRolloverHour(timestamp))
							// time is between roll-over hour && midnight
							// so subtract a day
							Records.ThisYear.LowMaxTemp.Ts = Records.ThisYear.LowMaxTemp.Ts.AddDays(-1);
					}
					catch
					{
						Records.ThisYear.LowMaxTemp.Ts = timestamp.AddDays(-1);
					}

					WriteYearIniFile();
				}

				// offset low temp today timestamp to allow for 0900 roll-over
				try
				{
					hr = DailyHighLow.Today.LowTempTime.Hour;
					mn = DailyHighLow.Today.LowTempTime.Minute;
					ts = timestamp.Date + new TimeSpan(hr, mn, 0);

					if (hr >= cumulus.GetRolloverHour(timestamp))
						// time is between roll-over hour && midnight
						// so subtract a day
						ts = ts.AddDays(-1);
				}
				catch
				{
					ts = timestamp.AddDays(-1);
				}

				if (DailyHighLow.Today.LowTemp > Records.AllTime.HighMinTemp.Val)
				{
					SetAlltime(Records.AllTime.HighMinTemp, DailyHighLow.Today.LowTemp, ts);
				}

				CheckMonthlyAlltime("HighMinTemp", DailyHighLow.Today.LowTemp, true, ts);

				if (DailyHighLow.Today.LowTemp > Records.ThisMonth.HighMinTemp.Val)
				{
					Records.ThisMonth.HighMinTemp.Val = DailyHighLow.Today.LowTemp;
					try
					{
						hr = DailyHighLow.Today.LowTempTime.Hour;
						mn = DailyHighLow.Today.LowTempTime.Minute;
						Records.ThisMonth.HighMinTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.GetRolloverHour(timestamp))
							// time is between roll-over hour && midnight
							// so subtract a day
							Records.ThisMonth.HighMinTemp.Ts = Records.ThisMonth.HighMinTemp.Ts.AddDays(-1);
					}
					catch
					{
						Records.ThisMonth.HighMinTemp.Ts = timestamp.AddDays(-1);
					}
					WriteMonthIniFile();
				}

				if (DailyHighLow.Today.LowTemp > Records.ThisYear.HighMinTemp.Val)
				{
					Records.ThisYear.HighMinTemp.Val = DailyHighLow.Today.LowTemp;
					try
					{
						hr = DailyHighLow.Today.LowTempTime.Hour;
						mn = DailyHighLow.Today.LowTempTime.Minute;
						Records.ThisYear.HighMinTemp.Ts = timestamp.Date + new TimeSpan(hr, mn, 0);

						if (hr >= cumulus.GetRolloverHour(timestamp))
							// time is between roll-over hour && midnight
							// so subtract a day
							Records.ThisYear.HighMinTemp.Ts = Records.ThisYear.HighMinTemp.Ts.AddDays(-1);
					}
					catch
					{
						Records.ThisYear.HighMinTemp.Ts = timestamp.AddDays(-1);
					}
					WriteYearIniFile();
				}

				// check temp range for highs && lows
				if (DailyHighLow.Today.TempRange > Records.AllTime.HighDailyTempRange.Val)
					SetAlltime(Records.AllTime.HighDailyTempRange, DailyHighLow.Today.TempRange, yesterday);

				if (DailyHighLow.Today.TempRange < Records.AllTime.LowDailyTempRange.Val)
					SetAlltime(Records.AllTime.LowDailyTempRange, DailyHighLow.Today.TempRange, yesterday);

				CheckMonthlyAlltime("HighDailyTempRange", DailyHighLow.Today.TempRange, true, yesterday);
				CheckMonthlyAlltime("LowDailyTempRange", DailyHighLow.Today.TempRange, false, yesterday);

				if (DailyHighLow.Today.TempRange > Records.ThisMonth.HighDailyTempRange.Val)
				{
					Records.ThisMonth.HighDailyTempRange.Val = DailyHighLow.Today.TempRange;
					Records.ThisMonth.HighDailyTempRange.Ts = yesterday;
					WriteMonthIniFile();
				}

				if (DailyHighLow.Today.TempRange < Records.ThisMonth.LowDailyTempRange.Val)
				{
					Records.ThisMonth.LowDailyTempRange.Val = DailyHighLow.Today.TempRange;
					Records.ThisMonth.LowDailyTempRange.Ts = yesterday;
					WriteMonthIniFile();
				}

				if (DailyHighLow.Today.TempRange > Records.ThisYear.HighDailyTempRange.Val)
				{
					Records.ThisYear.HighDailyTempRange.Val = DailyHighLow.Today.TempRange;
					Records.ThisYear.HighDailyTempRange.Ts = yesterday;
					WriteYearIniFile();
				}

				if (DailyHighLow.Today.TempRange < Records.ThisYear.LowDailyTempRange.Val)
				{
					Records.ThisYear.LowDailyTempRange.Val = DailyHighLow.Today.TempRange;
					Records.ThisYear.LowDailyTempRange.Ts = yesterday;
					WriteYearIniFile();
				}

				MetData.RG11RainYesterday = MetData.RG11RainToday;
				MetData.RG11RainToday = 0;

				if (day == 1)
				{
					// new month starting
					cumulus.LogMessage(" New month starting - " + month);

					CopyMonthIniFile(timestamp.AddDays(-1));

					RainThisMonth = 0;

					Records.ThisMonth.HighGust.Val = calibratedgust;
					Records.ThisMonth.HighWind.Val = MetData.WindAverage;
					Records.ThisMonth.HighTemp.Val = MetData.Temperature;
					Records.ThisMonth.LowTemp.Val = MetData.Temperature;
					Records.ThisMonth.HighAppTemp.Val = MetData.ApparentTemperature;
					Records.ThisMonth.LowAppTemp.Val = MetData.ApparentTemperature;
					Records.ThisMonth.HighFeelsLike.Val = MetData.FeelsLike;
					Records.ThisMonth.LowFeelsLike.Val = MetData.FeelsLike;
					Records.ThisMonth.HighHumidex.Val = MetData.Humidex;
					Records.ThisMonth.HighPress.Val = MetData.Pressure;
					Records.ThisMonth.LowPress.Val = MetData.Pressure;
					Records.ThisMonth.HighRainRate.Val = MetData.RainRate;
					Records.ThisMonth.HourlyRain.Val = MetData.RainLastHour;
					Records.ThisMonth.HighRain24Hours.Val = MetData.RainLast24Hour;
					Records.ThisMonth.DailyRain.Val = Cumulus.DefaultHiVal;
					Records.ThisMonth.HighHumidity.Val = MetData.Humidity;
					Records.ThisMonth.LowHumidity.Val = MetData.Humidity;
					Records.ThisMonth.HighHeatIndex.Val = MetData.HeatIndex;
					Records.ThisMonth.LowChill.Val = MetData.WindChill;
					Records.ThisMonth.HighMinTemp.Val = Cumulus.DefaultHiVal;
					Records.ThisMonth.LowMaxTemp.Val = Cumulus.DefaultLoVal;
					Records.ThisMonth.HighDewPoint.Val = MetData.Dewpoint;
					Records.ThisMonth.LowDewPoint.Val = MetData.Dewpoint;
					Records.ThisMonth.HighWindRun.Val = Cumulus.DefaultHiVal;
					Records.ThisMonth.LongestDryPeriod.Val = 0;
					Records.ThisMonth.LongestWetPeriod.Val = 0;
					Records.ThisMonth.HighDailyTempRange.Val = Cumulus.DefaultHiVal;
					Records.ThisMonth.LowDailyTempRange.Val = Cumulus.DefaultLoVal;
					Records.ThisMonth.HighBgt.Val = MetData.BlackGlobeTemp ?? Cumulus.DefaultHiVal;
					Records.ThisMonth.HighWbgt.Val = MetData.WetBulbGlobeTemp ?? Cumulus.DefaultHiVal;

					// this month highs && lows - timestamps
					Records.ThisMonth.HighGust.Ts = timestamp;
					Records.ThisMonth.HighWind.Ts = timestamp;
					Records.ThisMonth.HighTemp.Ts = timestamp;
					Records.ThisMonth.LowTemp.Ts = timestamp;
					Records.ThisMonth.HighAppTemp.Ts = timestamp;
					Records.ThisMonth.LowAppTemp.Ts = timestamp;
					Records.ThisMonth.HighFeelsLike.Ts = timestamp;
					Records.ThisMonth.LowFeelsLike.Ts = timestamp;
					Records.ThisMonth.HighHumidex.Ts = timestamp;
					Records.ThisMonth.HighPress.Ts = timestamp;
					Records.ThisMonth.LowPress.Ts = timestamp;
					Records.ThisMonth.HighRainRate.Ts = timestamp;
					Records.ThisMonth.HourlyRain.Ts = timestamp;
					Records.ThisMonth.HighRain24Hours.Ts = timestamp;
					Records.ThisMonth.DailyRain.Ts = timestamp;
					Records.ThisMonth.HighHumidity.Ts = timestamp;
					Records.ThisMonth.LowHumidity.Ts = timestamp;
					Records.ThisMonth.HighHeatIndex.Ts = timestamp;
					Records.ThisMonth.LowChill.Ts = timestamp;
					Records.ThisMonth.HighMinTemp.Ts = timestamp;
					Records.ThisMonth.LowMaxTemp.Ts = timestamp;
					Records.ThisMonth.HighDewPoint.Ts = timestamp;
					Records.ThisMonth.LowDewPoint.Ts = timestamp;
					Records.ThisMonth.HighWindRun.Ts = timestamp;
					Records.ThisMonth.LongestDryPeriod.Ts = timestamp;
					Records.ThisMonth.LongestWetPeriod.Ts = timestamp;
					Records.ThisMonth.LowDailyTempRange.Ts = timestamp;
					Records.ThisMonth.HighDailyTempRange.Ts = timestamp;
					Records.ThisMonth.HighBgt.Ts = timestamp;
					Records.ThisMonth.HighWbgt.Ts = timestamp;
				}
				else
					RainThisMonth += MetData.RainYesterday;

				if (day == 1 && month == 1)
				{
					// new year starting
					cumulus.LogMessage(" New year starting");

					CopyYearIniFile(timestamp.AddDays(-1));

					Records.ThisYear.HighGust.Val = calibratedgust;
					Records.ThisYear.HighWind.Val = MetData.WindAverage;
					Records.ThisYear.HighTemp.Val = MetData.Temperature;
					Records.ThisYear.LowTemp.Val = MetData.Temperature;
					Records.ThisYear.HighAppTemp.Val = MetData.ApparentTemperature;
					Records.ThisYear.LowAppTemp.Val = MetData.ApparentTemperature;
					Records.ThisYear.HighFeelsLike.Val = MetData.FeelsLike;
					Records.ThisYear.LowFeelsLike.Val = MetData.FeelsLike;
					Records.ThisYear.HighHumidex.Val = MetData.Humidex;
					Records.ThisYear.HighPress.Val = MetData.Pressure;
					Records.ThisYear.LowPress.Val = MetData.Pressure;
					Records.ThisYear.HighRainRate.Val = MetData.RainRate;
					Records.ThisYear.HourlyRain.Val = MetData.RainLastHour;
					Records.ThisYear.HighRain24Hours.Val = MetData.RainLast24Hour;
					Records.ThisYear.DailyRain.Val = Cumulus.DefaultHiVal;
					Records.ThisYear.MonthlyRain.Val = Cumulus.DefaultHiVal;
					Records.ThisYear.HighHumidity.Val = MetData.Humidity;
					Records.ThisYear.LowHumidity.Val = MetData.Humidity;
					Records.ThisYear.HighHeatIndex.Val = MetData.HeatIndex;
					Records.ThisYear.LowChill.Val = MetData.WindChill;
					Records.ThisYear.HighMinTemp.Val = Cumulus.DefaultHiVal;
					Records.ThisYear.LowMaxTemp.Val = Cumulus.DefaultLoVal;
					Records.ThisYear.HighDewPoint.Val = MetData.Dewpoint;
					Records.ThisYear.LowDewPoint.Val = MetData.Dewpoint;
					Records.ThisYear.HighWindRun.Val = Cumulus.DefaultHiVal;
					Records.ThisYear.LongestDryPeriod.Val = 0;
					Records.ThisYear.LongestWetPeriod.Val = 0;
					Records.ThisYear.HighDailyTempRange.Val = Cumulus.DefaultHiVal;
					Records.ThisYear.LowDailyTempRange.Val = Cumulus.DefaultLoVal;
					Records.ThisYear.HighBgt.Val = MetData.BlackGlobeTemp ?? Cumulus.DefaultHiVal;
					Records.ThisYear.HighWbgt.Val = MetData.WetBulbGlobeTemp ?? Cumulus.DefaultHiVal;

					// this Year highs && lows - timestamps
					Records.ThisYear.HighGust.Ts = timestamp;
					Records.ThisYear.HighWind.Ts = timestamp;
					Records.ThisYear.HighTemp.Ts = timestamp;
					Records.ThisYear.LowTemp.Ts = timestamp;
					Records.ThisYear.HighAppTemp.Ts = timestamp;
					Records.ThisYear.LowAppTemp.Ts = timestamp;
					Records.ThisYear.HighFeelsLike.Ts = timestamp;
					Records.ThisYear.LowFeelsLike.Ts = timestamp;
					Records.ThisYear.HighHumidex.Ts = timestamp;
					Records.ThisYear.HighPress.Ts = timestamp;
					Records.ThisYear.LowPress.Ts = timestamp;
					Records.ThisYear.HighRainRate.Ts = timestamp;
					Records.ThisYear.HourlyRain.Ts = timestamp;
					Records.ThisYear.HighRain24Hours.Ts = timestamp;
					Records.ThisYear.DailyRain.Ts = timestamp;
					Records.ThisYear.MonthlyRain.Ts = timestamp;
					Records.ThisYear.HighHumidity.Ts = timestamp;
					Records.ThisYear.LowHumidity.Ts = timestamp;
					Records.ThisYear.HighHeatIndex.Ts = timestamp;
					Records.ThisYear.LowChill.Ts = timestamp;
					Records.ThisYear.HighMinTemp.Ts = timestamp;
					Records.ThisYear.LowMaxTemp.Ts = timestamp;
					Records.ThisYear.HighDewPoint.Ts = timestamp;
					Records.ThisYear.LowDewPoint.Ts = timestamp;
					Records.ThisYear.HighWindRun.Ts = timestamp;
					Records.ThisYear.LongestDryPeriod.Ts = timestamp;
					Records.ThisYear.LongestWetPeriod.Ts = timestamp;
					Records.ThisYear.HighDailyTempRange.Ts = timestamp;
					Records.ThisYear.LowDailyTempRange.Ts = timestamp;
					Records.ThisYear.HighBgt.Ts = timestamp;
					Records.ThisYear.HighWbgt.Ts = timestamp;

					// reset the ET annual total for Davis WLL stations only
					// because we mimic the annual total and it is not reset like VP2 stations
					if (cumulus.StationType == StationTypes.WLL || cumulus.StationOptions.CalculatedET)
					{
						cumulus.LogMessage(" Resetting Annual ET total");
						MetData.AnnualETTotal = 0;
					}
				}

				if (day == 1 && month == cumulus.RainSeasonStart)
				{
					// new year starting
					cumulus.LogMessage(" New rain season starting");
					RainThisYear = 0;
				}
				else
				{
					RainThisYear += MetData.RainYesterday;
				}

				if (day == 1 && month == cumulus.ChillHourSeasonStart)
				{
					// new year starting
					cumulus.LogMessage(" Chill hour season starting");
					MetData.ChillHours = 0;
				}

				if (day == 1 && month == cumulus.GrowingYearStarts)
				{
					cumulus.LogMessage(" New growing degree day season starting");
					MetData.GrowingDegreeDaysThisYear1 = 0;
					MetData.GrowingDegreeDaysThisYear2 = 0;
				}

				MetData.GrowingDegreeDaysThisYear1 += MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(DailyHighLow.Today.HighTemp), ConvertUnits.UserTempToC(DailyHighLow.Today.LowTemp), ConvertUnits.UserTempToC(cumulus.GrowingBase1), cumulus.GrowingCap30C);
				MetData.GrowingDegreeDaysThisYear2 += MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(DailyHighLow.Today.HighTemp), ConvertUnits.UserTempToC(DailyHighLow.Today.LowTemp), ConvertUnits.UserTempToC(cumulus.GrowingBase2), cumulus.GrowingCap30C);


				if (day == 1 && month == cumulus.SnowSeasonStart)
				{
					for (var i = 1; i <= 4; i++)
					{
						MetData.SnowSeason[i] = 0;
					}
				}

				// Now reset all values to the current or default ones
				// We may be doing a roll-over from the first logger entry,
				// && as we do the roll-over before processing the entry, the
				// current items may not be set up.

				MetData.RainCounterDayStart = MetData.RainCounter;
				cumulus.LogMessage("Raindaystart set to " + MetData.RainCounterDayStart);

				MetData.RainToday = 0;

				MetData.TempTotalToday = MetData.Temperature;
				MetData.TempSamplesToday = 1;

				// Copy today"s high wind settings to yesterday
				DailyHighLow.Yest.HighWind = DailyHighLow.Today.HighWind;
				DailyHighLow.Yest.HighWindTime = DailyHighLow.Today.HighWindTime;
				DailyHighLow.Yest.HighGust = DailyHighLow.Today.HighGust;
				DailyHighLow.Yest.HighGustTime = DailyHighLow.Today.HighGustTime;
				DailyHighLow.Yest.HighGustBearing = DailyHighLow.Today.HighGustBearing;

				// Reset today"s high wind settings
				DailyHighLow.Today.HighGust = calibratedgust;
				DailyHighLow.Today.HighGustBearing = MetData.WindBearing;
				DailyHighLow.Today.HighWind = MetData.WindAverage;

				DailyHighLow.Today.HighWindTime = timestamp;
				DailyHighLow.Today.HighGustTime = timestamp;

				// Copy today"s high temp settings to yesterday
				DailyHighLow.Yest.HighTemp = DailyHighLow.Today.HighTemp;
				DailyHighLow.Yest.HighTempTime = DailyHighLow.Today.HighTempTime;
				// Reset today"s high temp settings
				DailyHighLow.Today.HighTemp = MetData.Temperature;
				DailyHighLow.Today.HighTempTime = timestamp;

				// Copy today"s low temp settings to yesterday
				DailyHighLow.Yest.LowTemp = DailyHighLow.Today.LowTemp;
				DailyHighLow.Yest.LowTempTime = DailyHighLow.Today.LowTempTime;
				// Reset today"s low temp settings
				DailyHighLow.Today.LowTemp = MetData.Temperature;
				DailyHighLow.Today.LowTempTime = timestamp;

				DailyHighLow.Yest.TempRange = DailyHighLow.Today.TempRange;
				DailyHighLow.Today.TempRange = 0;

				// Copy today"s low pressure settings to yesterday
				DailyHighLow.Yest.LowPress = DailyHighLow.Today.LowPress;
				DailyHighLow.Yest.LowPressTime = DailyHighLow.Today.LowPressTime;
				// Reset today"s low pressure settings
				DailyHighLow.Today.LowPress = MetData.Pressure;
				DailyHighLow.Today.LowPressTime = timestamp;

				// Copy today"s high pressure settings to yesterday
				DailyHighLow.Yest.HighPress = DailyHighLow.Today.HighPress;
				DailyHighLow.Yest.HighPressTime = DailyHighLow.Today.HighPressTime;
				// Reset today"s high pressure settings
				DailyHighLow.Today.HighPress = MetData.Pressure;
				DailyHighLow.Today.HighPressTime = timestamp;

				// Copy today"s high rain rate settings to yesterday
				DailyHighLow.Yest.HighRainRate = DailyHighLow.Today.HighRainRate;
				DailyHighLow.Yest.HighRainRateTime = DailyHighLow.Today.HighRainRateTime;
				// Reset today"s high rain rate settings
				DailyHighLow.Today.HighRainRate = MetData.RainRate;
				DailyHighLow.Today.HighRainRateTime = timestamp;

				DailyHighLow.Yest.HighHourlyRain = DailyHighLow.Today.HighHourlyRain;
				DailyHighLow.Yest.HighHourlyRainTime = DailyHighLow.Today.HighHourlyRainTime;
				DailyHighLow.Today.HighHourlyRain = MetData.RainLastHour;
				DailyHighLow.Today.HighHourlyRainTime = timestamp;

				DailyHighLow.Yest.HighRain24h = DailyHighLow.Today.HighRain24h;
				DailyHighLow.Yest.HighRain24hTime = DailyHighLow.Today.HighRain24hTime;
				DailyHighLow.Today.HighRain24h = MetData.RainLast24Hour;
				DailyHighLow.Today.HighRain24hTime = timestamp;

				MetData.YesterdayWindRun = MetData.WindRunToday;
				MetData.WindRunToday = 0;

				MetData.YestDominantWindBearing = MetData.DominantWindBearing;

				MetData.DominantWindBearing = 0;
				MetData.DominantWindBearingX = 0;
				MetData.DominantWindBearingY = 0;
				MetData.DominantWindBearingMinutes = 0;

				MetData.YestChillHours = MetData.ChillHours;
				MetData.YestHeatingDegreeDays = MetData.HeatingDegreeDays;
				MetData.YestCoolingDegreeDays = MetData.CoolingDegreeDays;
				MetData.HeatingDegreeDays = 0;
				MetData.CoolingDegreeDays = 0;

				// reset startofdayET value
				MetData.StartofdayET = MetData.AnnualETTotal;
				cumulus.LogMessage("StartofdayET set to " + MetData.StartofdayET);
				MetData.ET = 0;

				// Humidity
				DailyHighLow.Yest.LowHumidity = DailyHighLow.Today.LowHumidity;
				DailyHighLow.Yest.LowHumidityTime = DailyHighLow.Today.LowHumidityTime;
				DailyHighLow.Today.LowHumidity = MetData.Humidity;
				DailyHighLow.Today.LowHumidityTime = timestamp;

				DailyHighLow.Yest.HighHumidity = DailyHighLow.Today.HighHumidity;
				DailyHighLow.Yest.HighHumidityTime = DailyHighLow.Today.HighHumidityTime;
				DailyHighLow.Today.HighHumidity = MetData.Humidity;
				DailyHighLow.Today.HighHumidityTime = timestamp;

				// heat index
				DailyHighLow.Yest.HighHeatIndex = DailyHighLow.Today.HighHeatIndex;
				DailyHighLow.Yest.HighHeatIndexTime = DailyHighLow.Today.HighHeatIndexTime;
				DailyHighLow.Today.HighHeatIndex = MetData.HeatIndex;
				DailyHighLow.Today.HighHeatIndexTime = timestamp;

				// App temp
				DailyHighLow.Yest.HighAppTemp = DailyHighLow.Today.HighAppTemp;
				DailyHighLow.Yest.HighAppTempTime = DailyHighLow.Today.HighAppTempTime;
				DailyHighLow.Today.HighAppTemp = MetData.ApparentTemperature;
				DailyHighLow.Today.HighAppTempTime = timestamp;

				DailyHighLow.Yest.LowAppTemp = DailyHighLow.Today.LowAppTemp;
				DailyHighLow.Yest.LowAppTempTime = DailyHighLow.Today.LowAppTempTime;
				DailyHighLow.Today.LowAppTemp = MetData.ApparentTemperature;
				DailyHighLow.Today.LowAppTempTime = timestamp;

				// wind chill
				DailyHighLow.Yest.LowWindChill = DailyHighLow.Today.LowWindChill;
				DailyHighLow.Yest.LowWindChillTime = DailyHighLow.Today.LowWindChillTime;
				DailyHighLow.Today.LowWindChill = MetData.WindChill;
				DailyHighLow.Today.LowWindChillTime = timestamp;

				// dew point
				DailyHighLow.Yest.HighDewPoint = DailyHighLow.Today.HighDewPoint;
				DailyHighLow.Yest.HighDewPointTime = DailyHighLow.Today.HighDewPointTime;
				DailyHighLow.Today.HighDewPoint = MetData.Dewpoint;
				DailyHighLow.Today.HighDewPointTime = timestamp;

				DailyHighLow.Yest.LowDewPoint = DailyHighLow.Today.LowDewPoint;
				DailyHighLow.Yest.LowDewPointTime = DailyHighLow.Today.LowDewPointTime;
				DailyHighLow.Today.LowDewPoint = MetData.Dewpoint;
				DailyHighLow.Today.LowDewPointTime = timestamp;

				// solar
				DailyHighLow.Yest.HighSolar = DailyHighLow.Today.HighSolar;
				DailyHighLow.Yest.HighSolarTime = DailyHighLow.Today.HighSolarTime;
				DailyHighLow.Today.HighSolar = MetData.SolarRad ?? 0;
				DailyHighLow.Today.HighSolarTime = timestamp;

				DailyHighLow.Yest.HighUv = DailyHighLow.Today.HighUv;
				DailyHighLow.Yest.HighUvTime = DailyHighLow.Today.HighUvTime;
				DailyHighLow.Today.HighUv = MetData.UV ?? 0;
				DailyHighLow.Today.HighUvTime = timestamp;

				// Feels like
				DailyHighLow.Yest.HighFeelsLike = DailyHighLow.Today.HighFeelsLike;
				DailyHighLow.Yest.HighFeelsLikeTime = DailyHighLow.Today.HighFeelsLikeTime;
				DailyHighLow.Today.HighFeelsLike = MetData.FeelsLike;
				DailyHighLow.Today.HighFeelsLikeTime = timestamp;

				DailyHighLow.Yest.LowFeelsLike = DailyHighLow.Today.LowFeelsLike;
				DailyHighLow.Yest.LowFeelsLikeTime = DailyHighLow.Today.LowFeelsLikeTime;
				DailyHighLow.Today.LowFeelsLike = MetData.FeelsLike;
				DailyHighLow.Today.LowFeelsLikeTime = timestamp;

				// Humidex
				DailyHighLow.Yest.HighHumidex = DailyHighLow.Today.HighHumidex;
				DailyHighLow.Yest.HighHumidexTime = DailyHighLow.Today.HighHumidexTime;
				DailyHighLow.Today.HighHumidex = MetData.Humidex;
				DailyHighLow.Today.HighHumidexTime = timestamp;

				// Lightning
				MetData.LightningStrikesToday = 0;

				// BGT
				DailyHighLow.Yest.HighBgt = DailyHighLow.Today.HighBgt;
				DailyHighLow.Yest.HighBgtTime = DailyHighLow.Today.HighBgtTime;
				DailyHighLow.Today.HighBgt = MetData.BlackGlobeTemp ?? Cumulus.DefaultHiVal;
				DailyHighLow.Today.HighBgtTime = timestamp;

				// WBGT
				DailyHighLow.Yest.HighWbgt = DailyHighLow.Today.HighWbgt;
				DailyHighLow.Yest.HighWbgtTime = DailyHighLow.Today.HighWbgtTime;
				DailyHighLow.Today.HighWbgt = MetData.WetBulbGlobeTemp ?? Cumulus.DefaultHiVal;
				DailyHighLow.Today.HighWbgtTime = timestamp;

				// Save the current values in case of program restart
				WriteTodayFile(timestamp, true);
				WriteYesterdayFile(timestamp);

				if (cumulus.NOAAconf.Create)
				{
					try
					{
						var noaa = new NoaaReports(cumulus, this);

						var noaats = timestamp.AddDays(-1);

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
				cumulus.CreateEodGraphDataFiles();
				cumulus.CreateDailyGraphDataFiles();
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
					if (!File.Exists(cumulus.DailyProgram))
					{
						cumulus.LogWarningMessage($"Warning: Daily external program '{cumulus.DailyProgram}' does not exist");
					}
					else
					{
						try
						{
							// Prepare the process to run
							var args = string.Empty;

							if (!string.IsNullOrEmpty(cumulus.DailyParams))
							{
								var parser = new TokenParser(cumulus.TokenParserOnToken) { InputText = cumulus.DailyParams };
								args = parser.ToStringFromString();
							}
							cumulus.LogMessage("Executing daily program: " + cumulus.DailyProgram + " params: " + args);
							_ = Utils.RunExternalTask(cumulus.DailyProgram, args, false);
						}
						catch (FileNotFoundException)
						{
							cumulus.LogErrorMessage("Error executing external program: File not found");
						}
						catch (Exception ex)
						{
							cumulus.LogErrorMessage("Error executing external program: " + ex.Message);
						}
					}
				}

				CurrentDay = timestamp.Day;
				CurrentMonth = timestamp.Month;
				CurrentYear = timestamp.Year;
				CurrentDate = timestamp.Date;

				// recalculate rain this week - we may have gone over a week boundary
				// this uses the CurrentDate
				UpdateWeekRainfall();

				DayResetInProgress = false;

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
			var year = ts.Year.ToString();
			var month = ts.Month.ToString("D2");
			var savedFile = Path.Combine(cumulus.ProgramOptions.DataPath, "month" + year + month + ".ini");
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
			var year = ts.Year.ToString();
			var savedFile = Path.Combine(cumulus.ProgramOptions.DataPath, "year" + year + ".ini");
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

		/// <summary>
		///  Calculate checksum of data received from serial port for Oregon Scientific WMR stations
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected static int checksum(List<int> data)
		{
			var sum = 0;

			for (var i = 0; i < data.Count - 1; i++)
			{
				sum += data[i];
			}

			return sum % 256;
		}

		protected static int BCDchartoint(int c)
		{
			return c / 16 * 10 + c % 16;
		}

		public void StartLoop()
		{
			try
			{
				var mainThread = new Thread(Start) { IsBackground = true };
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

		public void AddRecentDataWithAq(DateTime timestamp)
		{
			double? pm2p5 = -1;
			double? pm10 = -1;
			// Check for Air Quality readings
			switch (cumulus.StationOptions.PrimaryAqSensor)
			{
				case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
					if (cumulus.airLinkDataOut != null && cumulus.airLinkDataOut.dataValid)
					{
						pm2p5 = cumulus.airLinkDataOut.pm2p5;
						pm10 = cumulus.airLinkDataOut.pm10;
					}
					break;
				case (int) Cumulus.PrimaryAqSensor.AirLinkIndoor:
					if (cumulus.airLinkDataIn != null && cumulus.airLinkDataIn.dataValid)
					{
						pm2p5 = cumulus.airLinkDataIn.pm2p5;
						pm10 = cumulus.airLinkDataIn.pm10;
					}
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor1:
					pm2p5 = MetData.AirQuality[1];
					pm10 = MetData.AirQuality10[1];
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor2:
					pm2p5 = MetData.AirQuality[2];
					pm10 = MetData.AirQuality10[2];
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor3:
					pm2p5 = MetData.AirQuality[3];
					pm10 = MetData.AirQuality10[3];
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor4:
					pm2p5 = MetData.AirQuality[4];
					pm10 = MetData.AirQuality10[4];
					break;
				case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
					pm2p5 = MetData.CO2_pm2p5;
					pm10 = MetData.CO2_pm10;
					break;

				default: // Not enabled, use invalid values
					break;
			}

			AddRecentDataEntry(timestamp, pm2p5, pm10);
		}


		public void UpdateRecentDataAqEntry(DateTime ts, double pm2p5, double pm10)
		{
			try
			{
				RecentDataDb.Execute("update RecentData set Pm2p5=?, Pm10=? where Timestamp=?", pm2p5 < 0 ? "NULL" : pm2p5, pm10 < 0 ? "NULL" : pm10, ts.ToUnixTime());
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"UpdateGraphDataAqEntry: Exception caught: {e.Message}");
			}
		}


		public void DoDayResetIfNeeded()
		{
			var hourInc = cumulus.GetHourInc();

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
				Reset9amTemperatures(DateTime.Now);
			}

			cumulus.LastUpdateTime = DateTime.Now;
		}


		public DateTime FOSensorClockTime { get; set; }
		public DateTime FOStationClockTime { get; set; }
		public DateTime FOSolarClockTime { get; set; }
		public string ConBatText { get; set; }
		public string ConSupplyVoltageText { get; set; }
		public decimal CapacitorVolt { get; set; }
		public string TxBatText { get; set; }
		public bool SensorContactLost { get; set; }
		public bool DataStopped { get; set; }
		public DateTime DataStoppedTime { get; set; }

		internal static DateTime GetDateTime(DateTime date, string time, int rolloverHr)
		{
			var tim = time.Split(':');
			var timSpan = new TimeSpan(int.Parse(tim[0]), int.Parse(tim[1]), 0);
			var dat = date + timSpan;
			if (rolloverHr != 0 && timSpan.Hours < rolloverHr)
			{
				dat.AddDays(1);
			}
			return dat;
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


		// This overridden in each Stations implementation
		public abstract void Stop();

		public void SetDefaultMonthlyHighsAndLows()
		{
			// this Month highs and lows
			Records.ThisMonth.HighGust.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighWind.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighTemp.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowTemp.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighAppTemp.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowAppTemp.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighFeelsLike.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowFeelsLike.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighHumidex.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighDewPoint.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowDewPoint.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighPress.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowPress.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighRainRate.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HourlyRain.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighRain24Hours.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.DailyRain.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighHumidity.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowHumidity.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighHeatIndex.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowChill.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighMinTemp.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowMaxTemp.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighWindRun.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.LowDailyTempRange.Val = Cumulus.DefaultLoVal;
			Records.ThisMonth.HighDailyTempRange.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighBgt.Val = Cumulus.DefaultHiVal;
			Records.ThisMonth.HighWbgt.Val = Cumulus.DefaultHiVal;


			// this Month highs and lows - timestamps
			Records.ThisMonth.HighGust.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighWind.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighAppTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowAppTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighFeelsLike.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowFeelsLike.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighHumidex.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighDewPoint.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowDewPoint.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighPress.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowPress.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighRainRate.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HourlyRain.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighRain24Hours.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.DailyRain.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighHumidity.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowHumidity.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighHeatIndex.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowChill.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighMinTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowMaxTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighWindRun.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.LowDailyTempRange.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighDailyTempRange.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighBgt.Ts = cumulus.defaultRecordTS;
			Records.ThisMonth.HighWbgt.Ts = cumulus.defaultRecordTS;
		}

		public void SetDefaultYearlyHighsAndLows()
		{
			// this Year highs and lows
			Records.ThisYear.HighGust.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighWind.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighTemp.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowTemp.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighAppTemp.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowAppTemp.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighFeelsLike.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowFeelsLike.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighHumidex.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighDewPoint.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowDewPoint.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighPress.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowPress.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighRainRate.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HourlyRain.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighRain24Hours.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.DailyRain.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.MonthlyRain.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighHumidity.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowHumidity.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighHeatIndex.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowChill.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighMinTemp.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowMaxTemp.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighWindRun.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.LowDailyTempRange.Val = Cumulus.DefaultLoVal;
			Records.ThisYear.HighDailyTempRange.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighBgt.Val = Cumulus.DefaultHiVal;
			Records.ThisYear.HighWbgt.Val = Cumulus.DefaultHiVal;

			// this Year highs and lows - timestamps
			Records.ThisYear.HighGust.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighWind.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighAppTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowAppTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighFeelsLike.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowFeelsLike.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighHumidex.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighDewPoint.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowDewPoint.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighPress.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowPress.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighRainRate.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HourlyRain.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.DailyRain.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighRain24Hours.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.MonthlyRain.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighHumidity.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowHumidity.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighHeatIndex.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowChill.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighMinTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowMaxTemp.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.DailyRain.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.LowDailyTempRange.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighDailyTempRange.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighBgt.Ts = cumulus.defaultRecordTS;
			Records.ThisYear.HighWbgt.Ts = cumulus.defaultRecordTS;
		}


		public string GetUnits()
		{
			var json = new StringBuilder("{", 200);

			json.Append($"\"temp\":\"{cumulus.Units.TempText[1]}\",");
			json.Append($"\"wind\":\"{cumulus.Units.WindText}\",");
			json.Append($"\"windrun\":\"{cumulus.Units.WindRunText}\",");
			json.Append($"\"rain\":\"{cumulus.Units.RainText}\",");
			json.Append($"\"press\":\"{cumulus.Units.PressText}\",");
			json.Append($"\"soilmoisture\":[\"{string.Join("\",\"", cumulus.Units.SoilMoistureUnitText)}\"],");
			json.Append($"\"co2\":\"{cumulus.Units.CO2UnitText}\",");
			json.Append($"\"leafwet\":\"{string.Join("\",\"", cumulus.Units.LeafWetnessUnitText)}\",");
			json.Append($"\"aq\":\"{string.Join("\",\"", cumulus.Units.AirQualityUnitText)}\",");
			json.Append($"\"snow\":\"{cumulus.Units.SnowText}\",");
			json.Append($"\"laser\":\"{cumulus.Units.LaserDistanceText}\"");
			json.Append('}');
			return json.ToString();
		}

		public string GetSnowInfo()
		{
			return $"{{\"snowHour\":{cumulus.SnowDepthHour},\"automated\":{cumulus.SnowAutomated}}}";
		}


		public static double GetAverageByMonth<T>(int mon, Func<DayFileRec, T> selector) where T : struct
		{
			try
			{
				// Determine the first and last full months
				var firstDate = MetData.DayFile[0].Date;
				var lastDate = MetData.DayFile[^1].Date;
				var firstFullMonth = firstDate.Day == 1 ? firstDate : new DateTime(firstDate.Year, firstDate.Month, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1);
				var lastFullMonth = new DateTime(lastDate.Year, lastDate.Month, 1, 0, 0, 0, DateTimeKind.Local).AddDays(-1);

				if (lastFullMonth > firstFullMonth)
				{
					// Filter data to include only complete months and calculate the average
					var avg = MetData.DayFile
						.Where(d => d.Date >= firstFullMonth && d.Date <= lastFullMonth && d.Date.Month == mon)
						.Average(d => Convert.ToDouble(selector(d))); // Convert property to double

					return avg;
				}
				else
				{
					return -999;
				}
			}
			catch
			{
				return -999; // Error fallback value
			}
		}

		public static double GetAverageTotalByMonth<T>(int mon, Func<DayFileRec, T> selector) where T : struct
		{
			try
			{
				// Determine the first and last full months
				var firstDate = MetData.DayFile[0].Date;
				var lastDate = MetData.DayFile[^1].Date;
				var firstFullMonth = firstDate.Day == 1 ? firstDate : new DateTime(firstDate.Year, firstDate.Month, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1);
				var lastFullMonth = new DateTime(lastDate.Year, lastDate.Month, 1, 0, 0, 0, DateTimeKind.Local).AddDays(-1);

				if (lastFullMonth > firstFullMonth)
				{
					// Filter data to include only complete months
					var avgPerMonth = MetData.DayFile
						.Where(d => d.Date >= firstFullMonth && d.Date <= lastFullMonth && d.Date.Month == mon)
						.GroupBy(d => new { d.Date.Year, d.Date.Month })
						.Select(g => g.Sum(d => Convert.ToDouble(selector(d))))
						.DefaultIfEmpty(0)
						.Average();

					return avgPerMonth;
				}
				else
				{
					return -999;
				}
			}
			catch
			{
				return -999;
			}
		}

		public double GetAverageChillHoursByMonth(int mon)
		{
			try
			{
				// Determine the first and last full months
				var firstDate = MetData.DayFile[0].Date;
				var lastDate = MetData.DayFile[^1].Date;
				var firstFullMonth = firstDate.Day == 1 ? firstDate : new DateTime(firstDate.Year, firstDate.Month, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1);
				var lastFullMonth = new DateTime(lastDate.Year, lastDate.Month, 1, 0, 0, 0, DateTimeKind.Local).AddDays(-1);
				if (lastFullMonth > firstFullMonth)
				{
					var monthlyDiffs = MetData.DayFile
						.Where(d => d.Date >= firstFullMonth && d.Date <= lastFullMonth && d.Date.Month == mon)
						.GroupBy(d => new { d.Date.Year, d.Date.Month })
						.Select(g =>
						{
							var maxVal = g.Max(d => d.ChillHours);

							// Find last day's value from the previous month
							double lastDayVal;
							if (mon == cumulus.ChillHourSeasonStart)
							{
								lastDayVal = 0;
							}
							else
							{
								var firstDayOfMonth = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, 0, DateTimeKind.Local);
								var previousMonthData = MetData.DayFile.Where(d => d.Date < firstDayOfMonth);
								lastDayVal = previousMonthData.OrderByDescending(d => d.Date).FirstOrDefault()?.ChillHours ?? 0;
							}

							return maxVal - lastDayVal;
						});

					return monthlyDiffs.Average();
				}
				else
				{
					return -999;
				}
			}
			catch
			{
				return -999; // Error fallback value
			}
		}

		public int GetMonthDryRainDays(DateTime date, bool dry)
		{
			var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Local);
			var end = start.AddMonths(1);
			double thresh;
			if (cumulus.RainDayThreshold > 0)
			{
				thresh = Convert.ToInt32(cumulus.RainDayThreshold);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					thresh = 0.2; // 0.2 mm
				}
				else
				{
					thresh = 0.01;  // 0.01 in
				}
			}

			int days;
			if (dry)
			{
				days = MetData.DayFile.Count(d => d.Date >= start && d.Date < end && d.TotalRain < thresh);
			}
			else
			{
				days = MetData.DayFile.Count(d => d.Date >= start && d.Date < end && d.TotalRain >= thresh);
			}

			return days;
		}

		public (DateTime, int) GetByMonthRainDays(int month)
		{
			double thresh;
			if (cumulus.RainDayThreshold > 0)
			{
				thresh = Convert.ToInt32(cumulus.RainDayThreshold);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					thresh = 0.2; // 0.2 mm
				}
				else
				{
					thresh = 0.01;  // 0.01 in
				}
			}

			var rainDays = MetData.DayFile
				.Where(d => d.Date.Month == month && d.TotalRain >= thresh)
				.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1, 0, 0, 0, DateTimeKind.Local))
				.Select(g => new
				{
					date = g.Key,
					TotalCount = g.Count()
				})
				.OrderByDescending(g => g.TotalCount)
				.FirstOrDefault();

			return rainDays != null ? (rainDays.date, rainDays.TotalCount) : (default, 0);
		}

		public (DateTime, int) GetByMonthDryDays(int month)
		{
			double thresh;
			if (cumulus.RainDayThreshold > 0)
			{
				thresh = Convert.ToInt32(cumulus.RainDayThreshold);
			}
			else
			{
				// default
				if (cumulus.Units.Rain == 0)
				{
					thresh = 0.2; // 0.2 mm
				}
				else
				{
					thresh = 0.01;  // 0.01 in
				}
			}

			var dryDays = MetData.DayFile
				.Where(d => d.Date.Month == month && d.TotalRain < thresh)
				.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1, 0, 0, 0, DateTimeKind.Local))
				.Select(g => new
				{
					date = g.Key,
					TotalCount = g.Count()
				})
				.OrderByDescending(g => g.TotalCount)
				.FirstOrDefault();

			// Fix: Explicitly convert the anonymous type to the tuple (DateTime, int)
			return dryDays != null ? (dryDays.date, dryDays.TotalCount) : (default, 0);
		}

	}

	public class CWindRecent
	{
		[PrimaryKey]
		public long Timestamp { get; set; }

		[Ignore]
		public DateTime DateTime
		{
			get => Timestamp.LocalFromUnixTime();
			set => Timestamp = value.ToUnixTime();
		}
		public double Gust { get; set; }  // calibrated "gust" as read from Stations
		public double Speed { get; set; } // calibrated "speed" as read from Stations
	}

	public class Last10MinWind(DateTime ts, double windgust, double windspeed, double Xgust, double Ygust)
	{
		public DateTime timestamp { get; set; } = ts;
		public double gust { get; set; } = windgust;
		public double speed { get; set; } = windspeed;
		public double gustX { get; set; } = Xgust;
		public double gustY { get; set; } = Ygust;
	}

	public class RecentData
	{
		[PrimaryKey]
		public long Timestamp { get; set; }

		[Ignore]
		public DateTime DateTime
		{
			get => Timestamp.LocalFromUnixTime();
			set => Timestamp = value.ToUnixTime();
		}

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
		public int? SolarRad { get; set; }
		public double? UV { get; set; }
		public double raincounter { get; set; }
		public double FeelsLike { get; set; }
		public double Humidex { get; set; }
		public double AppTemp { get; set; }
		public double? IndoorTemp { get; set; }
		public int? IndoorHumidity { get; set; }
		public int SolarMax { get; set; }
		public double? Pm2p5 { get; set; }
		public double? Pm10 { get; set; }
		public double RainRate { get; set; }
		public double? BGT { get; set; }
		public double? WBGT { get; set; }

		public string ToCsv()
		{
			// NOTE data order changed to be more human friendly
			var inv = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("[");

			sb.Append($"\"{DateTime.ToString("dd/MM/yy HH:mm")}\",");
			sb.Append($"{Timestamp},");
			sb.Append($"{WindSpeed.ToString(Program.cumulus.WindAvgFormat, inv)},");
			sb.Append($"{WindGust.ToString(Program.cumulus.WindFormat, inv)},");
			sb.Append($"{WindLatest.ToString(Program.cumulus.WindFormat, inv)},");
			sb.Append($"{WindDir},");
			sb.Append($"{WindAvgDir},");
			sb.Append($"{OutsideTemp.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{WindChill.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{DewPoint.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{HeatIndex.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{FeelsLike.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{Humidex.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{AppTemp.ToFixed(Program.cumulus.TempFormat)},");
			sb.Append($"{Humidity.ToString(Program.cumulus.HumFormat, inv)},");
			sb.Append($"{Pressure.ToString(Program.cumulus.PressFormat, inv)},");
			sb.Append($"{RainToday.ToString(Program.cumulus.RainFormat, inv)},");
			sb.Append($"{RainRate.ToString(Program.cumulus.RainFormat, inv)},");
			sb.Append($"{raincounter.ToString(inv)},");
			sb.Append($"{SolarRad.ToText("null")},");
			sb.Append($"{SolarMax},");
			sb.Append($"{UV.ToFixed(Program.cumulus.UVFormat, "null")},");
			sb.Append($"{IndoorTemp.ToFixed(Program.cumulus.TempFormat, "null")},");
			sb.Append($"{IndoorHumidity.ToText("null")},");
			sb.Append($"{Pm2p5.ToFixed("F1", "null")},");
			sb.Append($"{Pm10.ToFixed("F1", "null")},");
			sb.Append($"{BGT.ToFixed(Program.cumulus.TempFormat, "null")},");
			sb.Append($"{WBGT.ToFixed(Program.cumulus.TempFormat, "null")}");
			sb.Append(']');

			return sb.ToString();
		}

		public void FromCsvArray(string[] csv)
		{
			// NOTE data order changed to be more human friendly
			try
			{
				var inv = CultureInfo.InvariantCulture;

				if (csv.Length < 25)
				{
					Program.cumulus.LogErrorMessage("RecentData.FromCsv: CSV input too short = {fields.Length} fields");
					return;
				}

				Timestamp = long.Parse(csv[0]);
				WindSpeed = double.Parse(csv[1], inv);
				WindGust = double.Parse(csv[2], inv);
				WindLatest = double.Parse(csv[3], inv);
				WindDir = int.Parse(csv[4]);
				WindAvgDir = int.Parse(csv[5]);
				OutsideTemp = double.Parse(csv[6], inv);
				WindChill = double.Parse(csv[7], inv);
				DewPoint = double.Parse(csv[8], inv);
				HeatIndex = double.Parse(csv[9], inv);
				FeelsLike = double.Parse(csv[10], inv);
				Humidex = double.Parse(csv[11], inv);
				AppTemp = double.Parse(csv[12], inv);
				Humidity = int.Parse(csv[13]);
				Pressure = double.Parse(csv[14], inv);
				RainToday = double.Parse(csv[15], inv);
				RainRate = double.Parse(csv[16], inv);
				raincounter = double.Parse(csv[17], inv);
				SolarRad = int.TryParse(csv[18], out int rad) ? rad : null;
				SolarMax = int.Parse(csv[19]);
				UV = double.TryParse(csv[20], out double uv) ? uv : null;
				IndoorTemp = double.TryParse(csv[21], out double intemp) ? intemp : null;
				IndoorHumidity = int.TryParse(csv[22], out int inhum) ? inhum : null;
				Pm2p5 = double.TryParse(csv[23], out double pm2) ? pm2 : null;
				Pm10 = double.TryParse(csv[24], out double pm10) ? pm10 : null;
				BGT = double.TryParse(csv[25], out double bgt) ? bgt : null;
				WBGT = double.TryParse(csv[26], out double wbgt) ? bgt : null;
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "RecentData.FromCsv: Error");
				throw;
			}
		}
	}

	public class RecentAqData
	{
		[PrimaryKey]
		public long Timestamp { get; set; }

		[Ignore]
		public DateTime DateTime
		{
			get => Timestamp.LocalFromUnixTime();
			set => Timestamp = value.ToUnixTime();
		}
		public double? Pm2p5_1 { get; set; }
		public double? Pm10_1 { get; set; }
		public double? Pm2p5_2 { get; set; }
		public double? Pm10_2 { get; set; }
		public double? Pm2p5_3 { get; set; }
		public double? Pm10_3 { get; set; }
		public double? Pm2p5_4 { get; set; }
		public double? Pm10_4 { get; set; }
	}

	public class RecentAqAvgs
	{
		public double? Pm2p5_1 { get; set; }
		public double? Pm2p5_2 { get; set; }
		public double? Pm2p5_3 { get; set; }
		public double? Pm2p5_4 { get; set; }
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
		public int key { get; set; }
		public string statement { get; set; }
	}
}
