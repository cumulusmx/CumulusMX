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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;

using CumulusMX.LogFiles;

using EmbedIO.Utilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

using SQLite;

using Timer = System.Timers.Timer;

namespace CumulusMX
{
	internal abstract partial class WeatherStation
	{
		public struct TWindRecent
		{
			public double GustUncal; // uncalibrated "gust" as read from station
			public double SpeedUncal; // uncalibrated "speed" as read from station
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



		public List<DayFileRec> DayFile = [];


		public string LatestFOReading { get; set; }

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

		public double RainCounterDayStart = 0.0;
		public double RainCounter = 0.0;
		public bool gotraindaystart = false;
		protected double prevraincounter = 0.0;

		protected bool DayResetInProgress = false;

		public int IndoorBattStatus;
		public int WindBattStatus;
		public int RainBattStatus;
		public int TempBattStatus;
		public int UVBattStatus;

		public double[] WMR200ExtraDPValues { get; set; }

		public bool[] WMR200ChannelPresent { get; set; }

		public double[] WMR200ExtraHumValues { get; set; }

		public DateTime LastDataReadTime;
		public DateTime DataDateTime;
		public bool haveReadData = false;

		public bool ExtraSensorsDetected = false;

		// Should Cumulus find the peak gust?
		// This gets set to false for Davis stations after logger download
		// if 10-minute gust period is in use, so we use the Davis value instead.
		public bool CalcRecentMaxGust = true;

		protected SerialPort comport;

		public Timer secondTimer;
		public double presstrendval;
		public double temptrendval;

		private double previousPressStation = 9999;

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

		protected WeatherStation(Cumulus cumulus, bool extraStation = false)
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

			SensorReception = [];

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
						cumulus.LogMessage($"GetRainCounter: Rain day start counter {RainCounterDayStart:F4} and midnight rain counter {midnightraincounter:F4} match within rounding error, setting midnight rain to rain day start value");
						MetData.MidnightRainCount = RainCounterDayStart;
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
					cumulus.LogMessage("GetRainCounter: Midnight rain counter not found, setting midnight count to raindaystart = " + RainCounterDayStart);
					MetData.MidnightRainCount = RainCounterDayStart;
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
				foreach (var rec in DayFile)
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
			RainThisWeek = DayFile.Where(day => day.Date >= offsetWeek).Sum(day => day.TotalRain);
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
					threeHourlyPressureChangeMb = presstrendval * 3;
					break;
				case 2:
					threeHourlyPressureChangeMb = presstrendval * 3 / 0.0295333727;
					break;
				case 3:
					threeHourlyPressureChangeMb = presstrendval * 30;
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

		public int tempsamplestoday { get; set; }


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
			if (MetData.Temperature < cumulus.NOAAconf.HeatThreshold)
			{
				MetData.HeatingDegreeDays += (cumulus.NOAAconf.HeatThreshold - MetData.Temperature) * interval / 1440;
			}
			if (MetData.Temperature > cumulus.NOAAconf.CoolThreshold)
			{
				MetData.CoolingDegreeDays += (MetData.Temperature - cumulus.NOAAconf.CoolThreshold) * interval / 1440;
			}
		}

		public double GetWindRunMonth(int year, int month)
		{
			var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Local);
			var enddate = startDate.AddMonths(1);

			var now = cumulus.MeteoDate();

			if (now.Day == 1 && now.Date == startDate.Date)
			{
				// This month, and first day so no day file entries
				// return windrun so far today
				return MetData.WindRunToday;
			}

			var dayfile = DayFile.Where(r => r.Date >= startDate && r.Date < enddate).Sum(r => r.WindRun);

			// if the current month add todays windrun
			if (year == now.Year && month == now.Month)
			{
				dayfile += MetData.WindRunToday;
			}

			return dayfile;
		}

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

		private static string getTimeString(DateTime time, string format = "HH:mm")
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

				var dt = DateTime.MinValue.Add(timespan);

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
				RecentDataDb.Execute("delete from RecentData where Timestamp < ?", deleteTime.ToUnixTime());
				RecentDataDb.Execute("delete from RecentAqData where Timestamp = ?", deleteTime.ToUnixTime());
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

					CalculateDominantWindBearing(MetData.AvgBearing, MetData.WindAverage, 1);

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
						tempsamplestoday++;
						MetData.TempTotalToday += MetData.Temperature;
					}

					DoTrendValues(now);
					AddRecentDataWithAq(now, MetData.WindAverage, MetData.RecentMaxGust, MetData.WindLatest, MetData.Bearing, MetData.AvgBearing, MetData.Temperature, MetData.WindChill, MetData.Dewpoint, MetData.HeatIndex, MetData.Humidity,
						MetData.Pressure, MetData.RainToday, MetData.SolarRad, MetData.UV, RainCounter, MetData.FeelsLike, MetData.Humidex, MetData.ApparentTemperature, MetData.TemperatureIn, MetData.HumidityIn, MetData.CurrentSolarMax, MetData.RainRate, MetData.BlackGlobeTemp, MetData.WetBulbGlobeTemp);

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
						xapReport.Append($"WindDirD={MetData.Bearing}\n");
						xapReport.Append($"WindDirC={MetData.AvgBearing}\n");
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

		private void ReadBlakeLarsenData()
		{
			var blFile = Path.Combine(Directory.GetCurrentDirectory(), "SRsunshine.dat");

			if (File.Exists(blFile))
			{
				try
				{
					using var sr = new StreamReader(blFile);
					var line = sr.ReadLine();
					MetData.SunshineHours = double.Parse(line, CultureInfo.InvariantCulture.NumberFormat);
					sr.ReadLine();
					sr.ReadLine();
					line = sr.ReadLine();
					IsSunny = line == "True";
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error reading SRsunshine.dat: " + ex.Message);
				}
			}
		}


		// Calculates evapotranspiration based on the data for the last hour and updates the running annual total.
		public void CalculateEvapotranspiration(DateTime date)
		{
			cumulus.LogDebugMessage("Calculating ET from data");

			var dateFrom = date.AddHours(-1);

			// get the min and max temps, humidity, pressure, and mean solar rad and wind speed for the last hour
			var result = RecentDataDb.Query<EtData>("select avg(OutsideTemp) avgTemp, avg(Humidity) avgHum, avg(Pressure) avgPress, avg(SolarRad) avgSol, avg(SolarMax) avgSolMax, avg(WindSpeed) avgWind from RecentData where Timestamp >= ? order by Timestamp", dateFrom.ToUnixTime());

			// finally calculate the ETo
			var newET = MeteoLib.Evapotranspiration(
				ConvertUnits.UserTempToC(result[0].avgTemp),
				result[0].avgHum,
				result[0].avgSol,
				result[0].avgSolMax,
				ConvertUnits.UserWindToMS(result[0].avgWind),
				ConvertUnits.UserPressToHpa(result[0].avgPress) / 10
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
			// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
			GraphFileIdx[] createReqOnce = [GraphFileIdx.CONFIG, GraphFileIdx.AVAILABLE, GraphFileIdx.DAILYRAIN, GraphFileIdx.DAILYTEMP, GraphFileIdx.SUNHOURS];

			for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
			{
				if (cumulus.GraphDataFiles[i].Create && cumulus.GraphDataFiles[i].CreateRequired)
				{
#if DEBUG
					cumulus.LogDebugMessage("CreateGraphDataFiles: Creating " + cumulus.GraphDataFiles[i].FileName);
#endif
					try
					{
						json = CreateGraphDataJson(cumulus.GraphDataFiles[i].FileName, false);

						cumulus.LogDebugMessage("CreateGraphDataFiles: Writing " + cumulus.GraphDataFiles[i].FileName);
						var dest = Path.Combine(cumulus.GraphDataFiles[i].LocalPath, cumulus.GraphDataFiles[i].FileName);
						using (var file = new StreamWriter(dest, false))
						{
							file.WriteLine(json);
							file.Close();
						}

						// The config and daily files only need creating once per change
						// 0=graphconfig, 1=availabledata, 8=dailyrain, 9=dailytemp, 11=sunhours
						if (createReqOnce.Contains((GraphFileIdx) i))
						{
							cumulus.GraphDataFiles[i].CreateRequired = false;
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"Error creating/writing {cumulus.GraphDataFiles[i].FileName}: {ex}");
					}
#if DEBUG
					cumulus.LogDebugMessage("CreateGraphDataFiles: Completed " + cumulus.GraphDataFiles[i].FileName);
#endif
				}
			}
		}

		public string CreateGraphDataJson(string filename, bool incremental)
		{
			// Chart data for Highcharts graphs

			return filename switch
			{
				"graphconfig.json" => GetGraphConfig(false),
				"availabledata.json" => GetAvailGraphData(false),
				"tempdata.json" => GetTempGraphData(incremental, false),
				"pressdata.json" => GetPressGraphData(incremental),
				"winddata.json" => GetWindGraphData(incremental),
				"wdirdata.json" => GetWindDirGraphData(incremental),
				"humdata.json" => GetHumGraphData(incremental, false),
				"raindata.json" => GetRainGraphData(incremental),
				"dailyrain.json" => GetDailyRainGraphData(),
				"dailytemp.json" => GetDailyTempGraphData(false),
				"solardata.json" => GetSolarGraphData(incremental, false),
				"sunhours.json" => GetSunHoursGraphData(false),
				"airquality.json" => GetAqGraphData(incremental),
				"extratempdata.json" => GetExtraTempGraphData(incremental, false),
				"extrahumdata.json" => GetExtraHumGraphData(incremental, false),
				"extradewdata.json" => GetExtraDewPointGraphData(incremental, false),
				"soiltempdata.json" => GetSoilTempGraphData(incremental, false),
				"soilmoistdata.json" => GetSoilMoistGraphData(incremental, false),
				"soilecdata.json" => GetSoilEcGraphData(incremental, false),
				"leafwetdata.json" => GetLeafWetnessGraphData(incremental, false),
				"usertempdata.json" => GetUserTempGraphData(incremental, false),
				"co2sensordata.json" => GetCo2SensorGraphData(incremental, false),
				"laserdepthdata.json" => GetLaserDepthGraphData(incremental, false),
				"snow24data.json" => GetSnow24hGraphData(incremental, false),
				_ => "{}",
			};
		}


		public void CreateEodGraphDataFiles()
		{
			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				if (cumulus.GraphDataEodFiles[i].Create)
				{
					var json = CreateEodGraphDataJson(cumulus.GraphDataEodFiles[i].FileName);

					try
					{
						var dest = Path.Combine(cumulus.GraphDataEodFiles[i].LocalPath, cumulus.GraphDataEodFiles[i].FileName);
						File.WriteAllTextAsync(dest, json);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"Error writing {cumulus.GraphDataEodFiles[i].FileName}: {ex}");
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
			var eod = new int[] { (int) GraphFileIdx.DAILYRAIN, (int) GraphFileIdx.DAILYTEMP, (int) GraphFileIdx.SUNHOURS };

			foreach (var i in eod)
			{
				if (cumulus.GraphDataFiles[i].Create)
				{
					var json = CreateGraphDataJson(cumulus.GraphDataFiles[i].FileName, false);

					try
					{
						var dest = Path.Combine(cumulus.GraphDataFiles[i].LocalPath, cumulus.GraphDataFiles[i].FileName);
						File.WriteAllTextAsync(dest, json);
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"Error writing {cumulus.GraphDataFiles[i].FileName}: {ex}");
					}
				}

				cumulus.GraphDataFiles[i].CopyRequired = true;
				cumulus.GraphDataFiles[i].FtpRequired = true;
			}
		}

		public string CreateEodGraphDataJson(string filename)
		{
			return filename switch
			{
				"alldailytempdata.json" => GetAllDailyTempGraphData(false),
				"alldailypressdata.json" => GetAllDailyPressGraphData(),
				"alldailywinddata.json" => GetAllDailyWindGraphData(),
				"alldailyhumdata.json" => GetAllDailyHumGraphData(),
				"alldailyraindata.json" => GetAllDailyRainGraphData(),
				"alldailysolardata.json" => GetAllDailySolarGraphData(false),
				"alldailydegdaydata.json" => GetAllDegreeDaysGraphData(false),
				"alltempsumdata.json" => GetAllTempSumGraphData(false),
				"allchillhrsdata.json" => GetAllChillHrsGraphData(false),
				"alldailysnowdata.json" => GetAllSnowGraphData(false),
				_ => "{}",
			};
		}


		public string GetSolarGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOLAR].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				{
					sbUv.Append($"[{jsTime},{(data[i].UV.HasValue ? data[i].UV.Value.ToString(cumulus.UVFormat, InvC) : "null")}],");
				}

				if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
				{
					sbSol.Append($"[{jsTime},{(data[i].SolarRad.HasValue ? data[i].SolarRad : "null")}],");

					sbMax.Append($"[{jsTime},{data[i].SolarMax}],");
				}
			}

			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (sbUv[^1] == ',')
					sbUv.Length--;

				sbUv.Append(']');
				sb.Append(sbUv);
			}
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (sbSol[^1] == ',')
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
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.RAIN].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sbRain.Append($"[{jsTime},{data[i].RainToday.ToString(cumulus.RainFormat, InvC)}],");

				sbRate.Append($"[{jsTime},{data[i].RainRate.ToString(cumulus.RainFormat, InvC)}],");
			}

			if (sbRain[^1] == ',')
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
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.HUM].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				{
					sbOut.Append($"[{jsTime},{data[i].Humidity}],");
				}
				if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
				{
					sbIn.Append($"[{jsTime},{(data[i].IndoorHumidity.HasValue ? data[i].IndoorHumidity.Value : "null")}],");
				}
			}

			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
			{
				if (sbOut[^1] == ',')
					sbOut.Length--;

				sbOut.Append(']');

				sb.Append(sbOut);
			}

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
			{
				if (sbIn[^1] == ',')
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
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.WINDDIR].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sb.Append($"[{jsTime},{data[i].WindDir}],");

				sbAvg.Append($"[{jsTime},{data[i].WindAvgDir}],");
			}

			if (sb[^1] == ',')
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
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.WIND].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sb.Append($"[{jsTime},{data[i].WindGust.ToString(cumulus.WindFormat, InvC)}],");

				sbSpd.Append($"[{jsTime},{data[i].WindSpeed.ToString(cumulus.WindAvgFormat, InvC)}],");
			}

			if (sb[^1] == ',')
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
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"press\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.PRESS].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sb.Append($"[{jsTime},{data[i].Pressure.ToString(cumulus.PressFormat, InvC)}],");
			}

			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
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

		public string GetTempGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);
			var sbIn = new StringBuilder("\"intemp\":[");
			var sbDew = new StringBuilder("\"dew\":[");
			var sbApp = new StringBuilder("\"apptemp\":[");
			var sbFeel = new StringBuilder("\"feelslike\":[");
			var sbChill = new StringBuilder("\"wchill\":[");
			var sbHeat = new StringBuilder("\"heatindex\":[");
			var sbTemp = new StringBuilder("\"temp\":[");
			var sbHumidex = new StringBuilder("\"humidex\":[");
			var sbBgt = new StringBuilder("\"bgt\":[");
			var sbWbgt = new StringBuilder("\"wbgt\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.TEMP].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
				{
					var val = data[i].IndoorTemp.ToFixed(cumulus.TempFormat, "null");
					sbIn.Append($"[{jsTime},{val}],");
				}

				if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
					sbDew.Append($"[{jsTime},{data[i].DewPoint.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
					sbApp.Append($"[{jsTime},{data[i].AppTemp.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
					sbFeel.Append($"[{jsTime},{data[i].FeelsLike.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
					sbChill.Append($"[{jsTime},{data[i].WindChill.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
					sbHeat.Append($"[{jsTime},{data[i].HeatIndex.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
					sbTemp.Append($"[{jsTime},{data[i].OutsideTemp.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
					sbHumidex.Append($"[{jsTime},{data[i].Humidex.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
				{
					sbBgt.Append($"[{jsTime},{data[i].BGT.ToFixed(cumulus.TempFormat, "null")}],");
					sbWbgt.Append($"[{jsTime},{data[i].WBGT.ToFixed(cumulus.TempFormat, "null")}],");
				}
			}

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
			{
				if (sbIn[^1] == ',')
					sbIn.Length--;

				sbIn.Append(']');
				sb.Append(sbIn);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				if (sbDew[^1] == ',')
					sbDew.Length--;

				sbDew.Append(']');
				sb.Append((append ? "," : "") + sbDew);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				if (sbApp[^1] == ',')
					sbApp.Length--;

				sbApp.Append(']');
				sb.Append((append ? "," : "") + sbApp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				if (sbFeel[^1] == ',')
					sbFeel.Length--;

				sbFeel.Append(']');
				sb.Append((append ? "," : "") + sbFeel);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				if (sbChill[^1] == ',')
					sbChill.Length--;

				sbChill.Append(']');
				sb.Append((append ? "," : "") + sbChill);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				if (sbHeat[^1] == ',')
					sbHeat.Length--;

				sbHeat.Append(']');
				sb.Append((append ? "," : "") + sbHeat);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
			{
				if (sbTemp[^1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				if (sbHumidex[^1] == ',')
					sbHumidex.Length--;

				sbHumidex.Append(']');
				sb.Append((append ? "," : "") + sbHumidex);
			}

			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
			{
				if (sbBgt[^1] == ',')
					sbBgt.Length--;

				sbBgt.Append(']');
				sb.Append((append ? "," : "") + sbBgt);

				if (sbWbgt[^1] == ',')
					sbWbgt.Length--;

				sbWbgt.Append(']');
				sb.Append((append ? "," : "") + sbWbgt);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetAqGraphData(bool incremental, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
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
					dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.AIRQUAL].LastDataTime;
				}
				else if (start.HasValue && end.HasValue)
				{
					dateFrom = start.Value;
				}
				else
				{
					dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				}

				var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

				// no incremental data, send null to supress the upload
				if (incremental && data.Count == 0)
					return null;

				for (var i = 0; i < data.Count; i++)
				{
					var jsTime = data[i].Timestamp * 1000;

					if (data[i].Pm2p5.HasValue)
					{
						var val = data[i].Pm2p5 < -0.5 ? "null" : data[i].Pm2p5.Value.ToString("F1", InvC);
						sb2p5.Append($"[{jsTime},{val}],");

						// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
						if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
						{
							append = true;
							val = (data[i].Pm10 ?? -1) < -0.5 ? "null" : data[i].Pm10.Value.ToString("F1", InvC);
							sb10.Append($"[{jsTime},{val}],");
						}
					}
					else
					{
						sb2p5.Append($"[{jsTime},null],");
						// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
						if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
						{
							append = true;
							sb10.Append($"[{jsTime},null],");
						}
					}
				}

				if (sb2p5[^1] == ',')
					sb2p5.Length--;

				sb2p5.Append(']');
				sb.Append(sb2p5);

				if (append)
				{
					if (sb10[^1] == ',')
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
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraTempCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.EXTRATEMP].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;

			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(fileDate);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
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

			int[] fields = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 101, 102, 103, 104, 105, 106];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraDewPointGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraDPCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.EXTRADEW].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(fileDate);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int[] fields = [22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 113, 114, 115, 116, 117, 118];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraHumGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraHum.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraHumCaptions[i]}\":[");
			}


			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.EXTRAHUM].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int[] fields = [12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 107, 108, 109, 110, 111, 112];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = int.TryParse(st[fields[i]], out int temp) ? temp.ToString() : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilTempCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOILTEMP].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int[] fields = [32, 33, 34, 35, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilMoistGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilMoist.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilMoistureCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOILMOIST].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int[] fields = [36, 37, 38, 39, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = int.TryParse(st[fields[i]], out int temp) ? temp.ToString() : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilEcGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilEc.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilEcCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOILEC].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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
			// 119-122 AQ PM10
			// 123-126 AQ PM10 Avg
			// 127-143 Soil EC 1-16

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
										{
											var val = "null";
											if (st.Count > 127 + i)
											{
												val = int.TryParse(st[127 + i], out int temp) ? temp.ToString() : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSoilEcGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSoilEcGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSoilEcGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetLaserDepthGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.LaserDepth.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.LaserCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.LASERDEPTH].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int[] fields = [96, 97, 98, 99];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToString(cumulus.LaserFormat, InvC) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetLaserDepthGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetLaserDepthGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetLaserDepthGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}


		public string GetSnow24hGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			if (!cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
			{
				return "{}";
			}

			/* returns data in the form of an object with properties for each data series
				"snow24h": [[time,val],[time,val],...]
			*/

			sb.Append($"\"{cumulus.Trans.Snow24h}\":[");

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SNOW24H].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int field = 100;

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									var val = "null";
									if (field < st.Count)
									{
										val = double.TryParse(st[field], InvC, out temp) ? temp.ToString(cumulus.SnowFormat, InvC) : "null";
									}
									sb.Append($"[{jsTime},{val}],");
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSnow24hGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSnow24hGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSnow24hGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetLeafWetnessGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.LeafWetness.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.LeafWetness.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.LeafWetnessCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.LEAFWET].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
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

			int[] fields = [42, 43];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < 2; i++)
									{
										if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], out temp) ? temp.ToString("F1", InvC) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < 2; i++)
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetUserTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.UserTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.UserTempCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.USERTEMP].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
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

			int[] fields = [76, 77, 78, 79, 80, 81, 82, 83];

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
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetCo2SensorGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"CO2": [[time,val],[time,val],...],
				"CO2 Average": [[time,val],[time,val],...],
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
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.CO2].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
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

								var rec = new ExtraLogFileRec(line);
								entryTs = rec.UnixTimestamp;

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
										sbCo2.Append($"[{jsTime},{rec.CO2.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
										sbCo2Avg.Append($"[{jsTime},{rec.CO2_24h.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
										sbPm25.Append($"[{jsTime},{rec.CO2_pm2p5.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
										sbPm25Avg.Append($"[{jsTime},{rec.CO2_pm2p5_24h.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
										sbPm10.Append($"[{jsTime},{rec.CO2_pm10.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
										sbPm10Avg.Append($"[{jsTime},{rec.CO2_pm10_24h.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
										sbTemp.Append($"[{jsTime},{rec.CO2_temperature.ToFixed(cumulus.TempFormat, "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
										sbHum.Append($"[{jsTime},{rec.CO2_humidity.ToText("null")}],");
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

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
			{
				if (sbCo2[^1] == ',')
					sbCo2.Length--;

				sbCo2.Append(']');
				sb.Append(sbCo2);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
			{
				if (sbCo2Avg[^1] == ',')
					sbCo2Avg.Length--;

				sbCo2Avg.Append(']');
				sb.Append((append ? "," : "") + sbCo2Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
			{
				if (sbPm25[^1] == ',')
					sbPm25.Length--;

				sbPm25.Append(']');
				sb.Append((append ? "," : "") + sbPm25);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
			{
				if (sbPm25Avg[^1] == ',')
					sbPm25Avg.Length--;

				sbPm25Avg.Append(']');
				sb.Append((append ? "," : "") + sbPm25Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
			{
				if (sbPm10[^1] == ',')
					sbPm10.Length--;

				sbPm10.Append(']');
				sb.Append((append ? "," : "") + sbPm10);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
			{
				if (sbPm10Avg[^1] == ',')
					sbPm10Avg.Length--;

				sbPm10Avg.Append(']');
				sb.Append((append ? "," : "") + sbPm10Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
			{
				if (sbTemp[^1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
			{
				if (sbHum[^1] == ',')
					sbHum.Length--;

				sbHum.Append(']');
				sb.Append((append ? "," : "") + sbHum);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetIntervalAqGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sb2p5 = new StringBuilder("\"pm2p5\":[");
			var sb10 = new StringBuilder("\"pm10\":[");

			var useExtraSensorLogFile = true; // use the extra log file or the AirLink log file

			var pm25idx = 0; // index of the PM2.5 sensor in the log file

			var pm10idx = 0; // index of the PM10 sensor in the log file

			// first determine which if the sensors - if any - to use
			switch (cumulus.StationOptions.PrimaryAqSensor)
			{
				case (int) Cumulus.PrimaryAqSensor.Undefined:
					return "{}"; // no sensors defined
				case (int) Cumulus.PrimaryAqSensor.Sensor1:
					pm25idx = 68;
					pm10idx = 119;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor2:
					pm25idx = 69;
					pm10idx = 120;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor3:
					pm25idx = 70;
					pm10idx = 121;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor4:
					pm25idx = 71;
					pm10idx = 122;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
					pm25idx = 86;
					pm10idx = 88;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.AirLinkIndoor:
					pm25idx = 5;
					pm10idx = 10;
					useExtraSensorLogFile = false;
					break;
				case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
					pm25idx = 32;
					pm10idx = 37;
					useExtraSensorLogFile = false;
					break;
			}

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));

			var fileDate = dateFrom;
			var logFile = useExtraSensorLogFile ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetAirLinkLogFileName(fileDate);

			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalAqGraphData: Processing log file - {logFile}");
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
								var st = new List<string>(line.Split(','));
								var entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									if (st.Count > pm25idx && double.TryParse(st[pm25idx], InvC, out temp))
									{
										sb2p5.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");
									}
									else
									{
										sb2p5.Append($"[{jsTime},null],");
									}

									if (st.Count > pm10idx && double.TryParse(st[pm10idx], InvC, out temp))
									{
										sb10.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");
									}
									else
									{
										sb10.Append($"[{jsTime},null],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetIntervalAqGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetIntervalAqGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetIntervalAqGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);
					logFile = useExtraSensorLogFile ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetAirLinkLogFileName(fileDate);
				}
			} while (!finished);

			if (sb2p5[^1] == ',')
				sb2p5.Length--;

			sb2p5.Append("],");
			sb.Append(sb2p5);

			if (sb10[^1] == ',')
				sb10.Length--;

			sb10.Append(']');
			sb.Append(sb10);

			sb.Append('}');
			return sb.ToString();
		}


		public string GetIntervalTempGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);
			var sbIn = new StringBuilder("\"intemp\":[");
			var sbDew = new StringBuilder("\"dew\":[");
			var sbApp = new StringBuilder("\"apptemp\":[");
			var sbFeel = new StringBuilder("\"feelslike\":[");
			var sbChill = new StringBuilder("\"wchill\":[");
			var sbHeat = new StringBuilder("\"heatindex\":[");
			var sbTemp = new StringBuilder("\"temp\":[");
			var sbHumidex = new StringBuilder("\"humidex\":[");
			var sbBgt = new StringBuilder("\"bgt\":[");
			var sbWbgt = new StringBuilder("\"wbgt\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalTempGraphData: Processing log file - {logFile}");
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

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalTempGraphData: Finished processing the log files");
									break;
								}

								var jsTime = recTs * 1000;

								if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
									sbIn.Append($"[{jsTime},{rec.IndoorTemperature.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
									sbDew.Append($"[{jsTime},{rec.OutdoorDewpoint.ToFixed(cumulus.TempFormat)}],");

								if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
									sbApp.Append($"[{jsTime},{rec.ApparentTemperature.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
									sbFeel.Append($"[{jsTime},{rec.FeelsLike.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
									sbChill.Append($"[{jsTime},{rec.WindChill.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
									sbHeat.Append($"[{jsTime},{rec.HeatIndex.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
									sbTemp.Append($"[{jsTime},{rec.OutdoorTemperature.ToFixed(cumulus.TempFormat)}],");

								if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
									sbHumidex.Append($"[{jsTime},{rec.Humidex.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
								{
									sbBgt.Append($"[{jsTime},{rec.BlackGlobeTemp.ToFixed(cumulus.TempFormat, "null")}],");
									sbWbgt.Append($"[{jsTime},{rec.WetBulbGlobeTemp.ToFixed(cumulus.TempFormat, "null")}],");
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalTempGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalTempGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalTempGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalTempGraphData: Error reading the logfile {logFile} : {e.Message}");
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
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
			{
				if (sbIn[^1] == ',')
					sbIn.Length--;

				sbIn.Append(']');
				sb.Append(sbIn);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				if (sbDew[^1] == ',')
					sbDew.Length--;

				sbDew.Append(']');
				sb.Append((append ? "," : "") + sbDew);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				if (sbApp[^1] == ',')
					sbApp.Length--;

				sbApp.Append(']');
				sb.Append((append ? "," : "") + sbApp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				if (sbFeel[^1] == ',')
					sbFeel.Length--;

				sbFeel.Append(']');
				sb.Append((append ? "," : "") + sbFeel);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				if (sbChill[^1] == ',')
					sbChill.Length--;

				sbChill.Append(']');
				sb.Append((append ? "," : "") + sbChill);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				if (sbHeat[^1] == ',')
					sbHeat.Length--;

				sbHeat.Append(']');
				sb.Append((append ? "," : "") + sbHeat);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
			{
				if (sbTemp[^1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				if (sbHumidex[^1] == ',')
					sbHumidex.Length--;

				sbHumidex.Append(']');
				sb.Append((append ? "," : "") + sbHumidex);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
			{
				if (sbBgt[^1] == ',')
					sbBgt.Length--;

				sbBgt.Append(']');
				sb.Append((append ? "," : "") + sbBgt);

				if (sbWbgt[^1] == ',')
					sbWbgt.Length--;

				sbWbgt.Append(']');
				sb.Append((append ? "," : "") + sbWbgt);
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

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalHumGraphData: Processing log file - {logFile}");
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

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalHumGraphData: Finished processing the log files");
									break;
								}

								var jsTime = recTs * 1000;

								if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
								{
									sbOut.Append($"[{jsTime},{rec.OutdoorHumidity}],");
								}
								if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
								{
									sbIn.Append($"[{jsTime},{(rec.IndoorHumidity.HasValue ? rec.IndoorHumidity.Value : "null")}],");
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalHumGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalHumGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalHumGraphData: Too many errors reading {logFile} - aborting load of data");
									break;
								}
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
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);


			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
			{
				if (sbOut[^1] == ',')
					sbOut.Length--;

				sbOut.Append(']');

				sb.Append(sbOut);
			}

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
			{
				if (sbIn[^1] == ',')
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
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Processing log file - {logFile}");
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
								if (string.IsNullOrWhiteSpace(line)) continue;

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalSolarGraphData: Finished processing the log files");
									break;
								}

								var jsTime = recTs * 1000;

								if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
								{
									sbUv.Append($"[{jsTime},{rec.UV.ToFixed(cumulus.UVFormat, "null")}],");
								}

								if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
								{
									sbSol.Append($"[{jsTime},{(rec.SolarRad.HasValue ? (int) rec.SolarRad.Value : "null")}],");

									sbMax.Append($"[{jsTime},{(int) rec.CurrentSolarMax}],");
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalSolarGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalSolarGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
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
					if (cumulus.MeteoDate(fileDate) > cumulus.MeteoDate())
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (sbUv[^1] == ',')
					sbUv.Length--;

				sbUv.Append(']');
				sb.Append(sbUv);
			}
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (sbSol[^1] == ',')
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
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"press\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervaPressGraphData: Processing log file - {logFile}");
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

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervaPressGraphData: Finished processing the log files");
									break;
								}

								sb.Append($"[{rec.UnixTimestamp * 1000},{rec.Pressure.ToString(cumulus.PressFormat, InvC)}],");
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervaPressGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervaPressGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervaPressGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
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
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();

		}

		public string GetIntervalWindGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");
			var sbBrg = new StringBuilder("\"bearing\":[");
			var sbAvgBrg = new StringBuilder("\"avgbearing\":[");


			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalWindGraphData: Processing log file - {logFile}");
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

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalWindGraphData: Finished processing the log files");
									break;
								}

								var jsTime = rec.UnixTimestamp * 1000;

								sb.Append($"[{jsTime},{rec.RecentMaxGust.ToString(cumulus.WindFormat, InvC)}],");

								sbSpd.Append($"[{jsTime},{rec.WindAverage.ToString(cumulus.WindAvgFormat, InvC)}],");

								sbBrg.Append($"[{jsTime},{rec.Bearing}],");

								sbAvgBrg.Append($"[{jsTime},{rec.AvgBearing}],");
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalWindGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalWindGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalWindGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
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
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (sb[^1] == ',')
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
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervaRainGraphData: Processing log file - {logFile}");
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

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervaRainGraphData: Finished processing the log files");
									break;
								}

								var jsTime = rec.UnixTimestamp * 1000;

								sbRain.Append($"[{jsTime},{rec.RainToday.ToString(cumulus.RainFormat, InvC)}],");

								sbRate.Append($"[{jsTime},{rec.RainRate.ToString(cumulus.RainFormat, InvC)}],");
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervaRainGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervaRainGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervaRainGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
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
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (sbRain[^1] == ',')
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
			double windChill, double dewpoint, double heatIndex, int humidity, double pressure, double rainToday, int? solarRad, double? uv, double rainCounter, double feelslike, double humidex,
			double appTemp, double? insideTemp, int? insideHum, int solarMax, double rainrate, double? pm2p5, double? pm10, double? bgt, double? wbgt)
		{
			try
			{
				RecentDataDb.InsertOrReplace(new RecentData()
				{
					DateTime = timestamp,
					DewPoint = dewpoint,
					HeatIndex = heatIndex,
					Humidity = humidity,
					OutsideTemp = outsidetemp,
					Pressure = pressure,
					RainToday = rainToday,
					SolarRad = solarRad,
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
					SolarMax = solarMax,
					RainRate = rainrate,
					Pm2p5 = pm2p5,
					Pm10 = pm10,
					BGT = bgt,
					WBGT = wbgt
				});
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "AddRecentDataEntry: Error");
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

			var filename = Path.Combine(Directory.GetCurrentDirectory(), Cumulus.WxnowFile);

			var data = CreateWxnowFileString();
			using var file = new StreamWriter(filename, false);
			file.WriteLine(data);
			file.Close();
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

			var timestamp = cumulus.APRS.UseUtcInWxNowFile ? DateTime.UtcNow.ToUniversalTime().ToString(@"MMM dd yyyy HH\:mm") : DateTime.Now.ToString(@"MMM dd yyyy HH\:mm");

			var mphwind = Convert.ToInt32(ConvertUnits.UserWindToMPH(MetData.WindAverage));
			var mphgust = Convert.ToInt32(ConvertUnits.UserWindToMPH(MetData.RecentMaxGust));
			var ftempstr = APRStemp(MetData.Temperature);
			var in100rainlasthour = Convert.ToInt32(ConvertUnits.UserRainToIN(MetData.RainLastHour) * 100);
			var in100rainlast24hours = Convert.ToInt32(ConvertUnits.UserRainToIN(RainLast24Hour) * 100);
			int in100raintoday;
			// use today's rain for safety
			// 0900 day, use midnight calculation
			in100raintoday = Convert.ToInt32(ConvertUnits.UserRainToIN(cumulus.RolloverHour == 0 ? MetData.RainToday : MetData.RainSinceMidnight) * 100);
			var mb10press = Convert.ToInt32(ConvertUnits.UserPressToMB(MetData.AltimeterPressure) * 10);
			// For 100% humidity, send zero. For zero humidity, send 1
			int hum;
			if (MetData.Humidity == 0)
				hum = 1;
			else if (MetData.Humidity == 100)
				hum = 0;
			else
				hum = MetData.Humidity;

			var data = string.Format("{0}\n{1:000}/{2:000}g{3:000}t{4}r{5:000}p{6:000}P{7:000}h{8:00}b{9:00000}", timestamp, MetData.AvgBearing, mphwind, mphgust, ftempstr, in100rainlasthour,
				in100rainlast24hours, in100raintoday, hum, mb10press);

			if (cumulus.APRS.SendSolar && MetData.SolarRad.HasValue)
			{
				data += APRSsolarradStr(MetData.SolarRad.Value);
			}

			if (!string.IsNullOrWhiteSpace(cumulus.WxnowComment))
			{
				var tokenParser = new TokenParser(cumulus.TokenParserOnToken) { InputText = cumulus.WxnowComment };

				// process the webtags in the content string
				data += tokenParser.ToStringFromString();
			}

			return data;
		}

		private static string APRSsolarradStr(double solarRad)
		{
			if (solarRad < 1000)
			{
				return 'L' + Convert.ToInt32(solarRad).ToString("D3");
			}
			else
			{
				return 'l' + Convert.ToInt32(solarRad - 1000).ToString("D3");
			}
		}

		private string APRStemp(double temp)
		{
			// input is in TempUnit units, convert to F for APRS
			// and return three digits
			int num;

			if (cumulus.Units.Temp == 0)
			{
				num = Convert.ToInt32(temp * 1.8 + 32);
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
				MetData.MidnightRainCount = RainCounter;
				MetData.RainSinceMidnight = 0;
				MetData.MidnightRainResetDay = mrrday;
				cumulus.LogMessage("Midnight rain reset, count = " + RainCounter + " time = " + timestamp.ToShortTimeString());
				if (mrrday == 1 && mrrmonth == 1 && cumulus.StationType == StationTypes.VantagePro)
				{
					// special case: rain counter is about to be reset
					cumulus.LogMessage("Special case, Davis station on 1st Jan. Set midnight rain count to zero");
					MetData.MidnightRainCount = 0;
				}
			}
		}

		public void DoIndoorHumidity(int hum)
		{
			// Spike check
			if (previousInHum != 999 && Math.Abs(hum - previousInHum) > cumulus.Spike.InHumDiff)
			{
				cumulus.LogSpikeRemoval("Indoor humidity difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={hum} OldVal={previousInHum} SpikeDiff={cumulus.Spike.InHumDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Indoor humidity difference greater than spike value - NewVal={hum} OldVal={previousInHum} SpikeDiff={cumulus.Spike.InHumDiff:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousInHum = hum;
			MetData.HumidityIn = (int) cumulus.Calib.InHum.Calibrate(hum);

			if (MetData.HumidityIn < 0)
			{
				MetData.HumidityIn = 0;
			}
			if (MetData.HumidityIn > 100)
			{
				MetData.HumidityIn = 100;
			}
			HaveReadData = true;
		}

		public void DoOutdoorHumidity(int humpar, DateTime timestamp)
		{
			// Spike check
			if (previousHum < 998 && Math.Abs(humpar - previousHum) > cumulus.Spike.HumidityDiff)
			{
				cumulus.LogSpikeRemoval("Humidity difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={humpar} OldVal={previousHum} SpikeHumidityDiff={cumulus.Spike.HumidityDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Humidity difference greater than spike value - NewVal={humpar} OldVal={previousHum} SpikeHumidityDiff={cumulus.Spike.HumidityDiff:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			previousHum = humpar;

			if (humpar >= 98 && cumulus.StationOptions.Humidity98Fix)
			{
				MetData.Humidity = 100;
			}
			else
			{
				MetData.Humidity = (int) cumulus.Calib.Hum.Calibrate(humpar);
			}

			if (MetData.Humidity < 0)
			{
				MetData.Humidity = 0;
			}
			if (MetData.Humidity > 100)
			{
				MetData.Humidity = 100;
			}

			if (MetData.Humidity > DailyHighLow.Today.HighHumidity)
			{
				DailyHighLow.Today.HighHumidity = MetData.Humidity;
				DailyHighLow.Today.HighHumidityTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.Humidity < DailyHighLow.Today.LowHumidity)
			{
				DailyHighLow.Today.LowHumidity = MetData.Humidity;
				DailyHighLow.Today.LowHumidityTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.Humidity > Records.ThisMonth.HighHumidity.Val)
			{
				Records.ThisMonth.HighHumidity.Val = MetData.Humidity;
				Records.ThisMonth.HighHumidity.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.Humidity < Records.ThisMonth.LowHumidity.Val)
			{
				Records.ThisMonth.LowHumidity.Val = MetData.Humidity;
				Records.ThisMonth.LowHumidity.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.Humidity > Records.ThisYear.HighHumidity.Val)
			{
				Records.ThisYear.HighHumidity.Val = MetData.Humidity;
				Records.ThisYear.HighHumidity.Ts = timestamp;
				WriteYearIniFile();
			}
			if (MetData.Humidity < Records.ThisYear.LowHumidity.Val)
			{
				Records.ThisYear.LowHumidity.Val = MetData.Humidity;
				Records.ThisYear.LowHumidity.Ts = timestamp;
				WriteYearIniFile();
			}
			if (MetData.Humidity > Records.AllTime.HighHumidity.Val)
			{
				SetAlltime(Records.AllTime.HighHumidity, MetData.Humidity, timestamp);
			}
			CheckMonthlyAlltime("HighHumidity", MetData.Humidity, true, timestamp);
			if (MetData.Humidity < Records.AllTime.LowHumidity.Val)
			{
				SetAlltime(Records.AllTime.LowHumidity, MetData.Humidity, timestamp);
			}
			CheckMonthlyAlltime("LowHumidity", MetData.Humidity, false, timestamp);
			HaveReadData = true;
		}


		public void DoCloudBaseHeatIndex(DateTime timestamp)
		{
			var tempinF = ConvertUnits.UserTempToF(MetData.Temperature);
			var tempinC = ConvertUnits.UserTempToC(MetData.Temperature);

			// Calculate cloud base
			CloudBase = (int) Math.Floor((tempinF - ConvertUnits.UserTempToF(MetData.Dewpoint)) / 4.4 * 1000 / (cumulus.CloudBaseInFeet ? 1 : 3.2808399));
			if (CloudBase < 0)
				CloudBase = 0;

			MetData.HeatIndex = ConvertUnits.TempCToUser(MeteoLib.HeatIndex(tempinC, MetData.Humidity));

			if (MetData.HeatIndex > DailyHighLow.Today.HighHeatIndex)
			{
				DailyHighLow.Today.HighHeatIndex = MetData.HeatIndex;
				DailyHighLow.Today.HighHeatIndexTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.HeatIndex > Records.ThisMonth.HighHeatIndex.Val)
			{
				Records.ThisMonth.HighHeatIndex.Val = MetData.HeatIndex;
				Records.ThisMonth.HighHeatIndex.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.HeatIndex > Records.ThisYear.HighHeatIndex.Val)
			{
				Records.ThisYear.HighHeatIndex.Val = MetData.HeatIndex;
				Records.ThisYear.HighHeatIndex.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.HeatIndex > Records.AllTime.HighHeatIndex.Val)
				SetAlltime(Records.AllTime.HighHeatIndex, MetData.HeatIndex, timestamp);

			CheckMonthlyAlltime("HighHeatIndex", MetData.HeatIndex, true, timestamp);


			// Find estimated wet bulb temp. First time this is called, required variables may not have been set up yet
			try
			{
				WetBulb = ConvertUnits.TempCToUser(MeteoLib.CalculateWetBulbC(tempinC, ConvertUnits.UserTempToC(MetData.Dewpoint), ConvertUnits.UserPressToMB(MetData.Pressure)));
			}
			catch
			{
				WetBulb = MetData.Temperature;
			}
		}


		public string LastRainTip { get; set; }

		public void DoForecast(string forecast, bool hourly)
		{
			// store weather station forecast if available
			MetData.WsForecast = forecast;

			if (cumulus.ForecastSource == 3)
			{
				MetData.ForecastStr = string.Empty;
			}
			else if (cumulus.ForecastSource == 2)
			{
				if ((DateTime.UtcNow - cumulus.LastForecastDotTxtReadTime).TotalMinutes > 10)
				{
					cumulus.GetForecastTextFromFile();
					cumulus.LastForecastDotTxtReadTime = DateTime.UtcNow;
				}
			}
			else if (cumulus.ForecastSource == 0)
			{
				// user wants to display station forecast
				MetData.ForecastStr = MetData.WsForecast;
			}

			// 1 = cumulus forecast

			// determine whether we need to update the Cumulus forecast; user may have chosen to only update once an hour, but
			// we still need to do that once to get an initial forecast
			if (!FirstForecastDone || !cumulus.HourlyForecast || hourly && cumulus.HourlyForecast)
			{
				int bartrend;
				if (presstrendval >= -cumulus.FCPressureThreshold && presstrendval <= cumulus.FCPressureThreshold)
					bartrend = 0;
				else if (presstrendval < 0)
					bartrend = 2;
				else
					bartrend = 1;

				string windDir;
				if (MetData.WindAverage < 0.1)
				{
					windDir = "calm";
				}
				else
				{
					windDir = MetData.AvgBearingText;
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

				MetData.CumulusForecast = BetelCast(ConvertUnits.UserPressToHpa(MetData.Pressure), DateTime.Now.Month, windDir, bartrend, cumulus.Latitude > 0, hp, lp);

				// user wants to display Cumulus forecast
				if (cumulus.ForecastSource == 1)
				{
					MetData.ForecastStr = MetData.CumulusForecast;
				}
			}

			FirstForecastDone = true;
			HaveReadData = true;
		}


		public bool FirstForecastDone = false;

		public bool PressReadyToPlot { get; set; }

		public bool first_press { get; set; }


		public bool IsSunny { get; set; }

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

		/// <summary>
		/// Returns the angle from bearing2 to bearing1, in the range -180 to +180 degrees
		/// </summary>
		/// <param name="bearing1"></param>
		/// <param name="bearing2"></param>
		/// <returns>the required angle</returns>
		private static int getShortestAngle(int bearing1, int bearing2)
		{
			var diff = bearing2 - bearing1;

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

		public static Dictionary<string, byte> SensorReception { get; set; }

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

				// and the log file, but only if the station is initialised - do wait for this
				if (cumulus.Station != null)
				{
					cumulus.DoLogFile(timestamp, cumulus.NormalRunning).Wait();
				}

				cumulus.LogMessage("Raincounter = " + RainCounter + " Raindaystart = " + RainCounterDayStart);

				// Calculate yesterday"s rain, allowing for the multiplier -
				// raintotal && raindaystart are not calibrated
				MetData.RainYesterday = (RainCounter - RainCounterDayStart) * cumulus.Calib.Rain.Mult;
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
					ConsecutiveRainDays++;
					ConsecutiveDryDays = 0;
					cumulus.LogMessage("Consecutive rain days = " + ConsecutiveRainDays);
					// check for highs
					if (ConsecutiveRainDays > Records.ThisMonth.LongestWetPeriod.Val)
					{
						Records.ThisMonth.LongestWetPeriod.Val = ConsecutiveRainDays;
						Records.ThisMonth.LongestWetPeriod.Ts = yesterday;
						WriteMonthIniFile();
					}

					if (ConsecutiveRainDays > Records.ThisYear.LongestWetPeriod.Val)
					{
						Records.ThisYear.LongestWetPeriod.Val = ConsecutiveRainDays;
						Records.ThisYear.LongestWetPeriod.Ts = yesterday;
						WriteYearIniFile();
					}

					if (ConsecutiveRainDays > Records.AllTime.LongestWetPeriod.Val)
					{
						SetAlltime(Records.AllTime.LongestWetPeriod, ConsecutiveRainDays, yesterday);
					}

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
					if (ConsecutiveDryDays > Records.ThisMonth.LongestDryPeriod.Val)
					{
						Records.ThisMonth.LongestDryPeriod.Val = ConsecutiveDryDays;
						Records.ThisMonth.LongestDryPeriod.Ts = yesterday;
						WriteMonthIniFile();
					}

					if (ConsecutiveDryDays > Records.ThisYear.LongestDryPeriod.Val)
					{
						Records.ThisYear.LongestDryPeriod.Val = ConsecutiveDryDays;
						Records.ThisYear.LongestDryPeriod.Ts = yesterday;
						WriteYearIniFile();
					}

					if (ConsecutiveDryDays > Records.AllTime.LongestDryPeriod.Val)
					{
						SetAlltime(Records.AllTime.LongestDryPeriod, ConsecutiveDryDays, yesterday);
					}

					CheckMonthlyAlltime("LongestDryPeriod", ConsecutiveDryDays, true, yesterday);
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
					Records.ThisMonth.HighRain24Hours.Val = RainLast24Hour;
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
					Records.ThisYear.HighRain24Hours.Val = RainLast24Hour;
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
						AnnualETTotal = 0;
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

				RainCounterDayStart = RainCounter;
				cumulus.LogMessage("Raindaystart set to " + RainCounterDayStart);

				MetData.RainToday = 0;

				MetData.TempTotalToday = MetData.Temperature;
				tempsamplestoday = 1;

				// Copy today"s high wind settings to yesterday
				DailyHighLow.Yest.HighWind = DailyHighLow.Today.HighWind;
				DailyHighLow.Yest.HighWindTime = DailyHighLow.Today.HighWindTime;
				DailyHighLow.Yest.HighGust = DailyHighLow.Today.HighGust;
				DailyHighLow.Yest.HighGustTime = DailyHighLow.Today.HighGustTime;
				DailyHighLow.Yest.HighGustBearing = DailyHighLow.Today.HighGustBearing;

				// Reset today"s high wind settings
				DailyHighLow.Today.HighGust = calibratedgust;
				DailyHighLow.Today.HighGustBearing = MetData.Bearing;
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
				DailyHighLow.Today.HighRain24h = RainLast24Hour;
				DailyHighLow.Today.HighRain24hTime = timestamp;

				YesterdayWindRun = MetData.WindRunToday;
				MetData.WindRunToday = 0;

				MetData.YestDominantWindBearing = MetData.DominantWindBearing;

				MetData.DominantWindBearing = 0;
				MetData.DominantWindBearingX = 0;
				MetData.DominantWindBearingY = 0;
				MetData.DominantWindBearingMinutes = 0;

				MetData.YestChillHours = MetData.ChillHours;
				YestHeatingDegreeDays = MetData.HeatingDegreeDays;
				YestCoolingDegreeDays = MetData.CoolingDegreeDays;
				MetData.HeatingDegreeDays = 0;
				MetData.CoolingDegreeDays = 0;

				// reset startofdayET value
				StartofdayET = AnnualETTotal;
				cumulus.LogMessage("StartofdayET set to " + StartofdayET);
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

		private async Task DoDayfile(DateTime timestamp)
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
			// 55  High BGT
			// 56  High BGT time
			// 57  High WBGT
			// 58  High WBGT time

			double AvgTemp;
			if (tempsamplestoday > 0)
				AvgTemp = MetData.TempTotalToday / tempsamplestoday;
			else
				AvgTemp = 0;

			// save the value for yesterday
			YestAvgTemp = AvgTemp;

			var inv = CultureInfo.InvariantCulture;
			var sep = ",";

			var datestring = timestamp.AddDays(-1).ToString("dd/MM/yy", inv);
			// NB this string is just for logging, the dayfile update code is further down
			var strb = new StringBuilder(300);
			strb.Append(datestring);
			strb.Append(sep + DailyHighLow.Today.HighGust.ToString(cumulus.WindFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighGustBearing);
			strb.Append(sep + DailyHighLow.Today.HighGustTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowTemp.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.LowTempTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighTemp.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.HighTempTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowPress.ToString(cumulus.PressFormat, inv));
			strb.Append(sep + DailyHighLow.Today.LowPressTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighPress.ToString(cumulus.PressFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighPressTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighRainRate.ToString(cumulus.RainFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighRainRateTime.ToString("HH:mm", inv));
			strb.Append(sep + MetData.RainToday.ToString(cumulus.RainFormat, inv));
			strb.Append(sep + AvgTemp.ToFixed(cumulus.TempFormat));
			strb.Append(sep + MetData.WindRunToday.ToString("F1", inv));
			strb.Append(sep + DailyHighLow.Today.HighWind.ToString(cumulus.WindAvgFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighWindTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowHumidity);
			strb.Append(sep + DailyHighLow.Today.LowHumidityTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighHumidity);
			strb.Append(sep + DailyHighLow.Today.HighHumidityTime.ToString("HH:mm", inv));
			strb.Append(sep + MetData.ET.ToFixed(cumulus.ETFormat));
			if (cumulus.RolloverHour == 0)
			{
				// use existing current sunshine hour count
				strb.Append(sep + MetData.SunshineHours.ToString(cumulus.SunFormat, inv));
			}
			else
			{
				// for non-midnight roll-over, use midnight
				strb.Append(sep + MetData.SunshineToMidnight.ToString(cumulus.SunFormat, inv));
			}
			strb.Append(sep + DailyHighLow.Today.HighHeatIndex.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.HighHeatIndexTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighAppTemp.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.HighAppTempTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowAppTemp.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.LowAppTempTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighHourlyRain.ToFixed(cumulus.RainFormat));
			strb.Append(sep + DailyHighLow.Today.HighHourlyRainTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowWindChill.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.LowWindChillTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighDewPoint.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.HighDewPointTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowDewPoint.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.LowDewPointTime.ToString("HH:mm", inv));
			strb.Append(sep + MetData.DominantWindBearing.ToString());
			strb.Append(sep + MetData.HeatingDegreeDays.ToString("F1", inv));
			strb.Append(sep + MetData.CoolingDegreeDays.ToString("F1", inv));
			strb.Append(sep + DailyHighLow.Today.HighSolar.ToString());
			strb.Append(sep + DailyHighLow.Today.HighSolarTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighUv.ToString(cumulus.UVFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighUvTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighFeelsLike.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.HighFeelsLikeTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.LowFeelsLike.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.LowFeelsLikeTime.ToString("HH:mm", inv));
			strb.Append(sep + DailyHighLow.Today.HighHumidex.ToFixed(cumulus.TempFormat));
			strb.Append(sep + DailyHighLow.Today.HighHumidexTime.ToString("HH:mm", inv));
			strb.Append(sep + MetData.ChillHours.ToString(cumulus.TempFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighRain24h.ToString(cumulus.RainFormat, inv));
			strb.Append(sep + DailyHighLow.Today.HighRain24hTime.ToString("HH:mm", inv));
			strb.Append(sep + (DailyHighLow.Today.HighBgt == Cumulus.DefaultHiVal ? string.Empty : DailyHighLow.Today.HighBgt.ToFixed(cumulus.TempFormat)));
			strb.Append(sep + (DailyHighLow.Today.HighBgt == Cumulus.DefaultHiVal ? string.Empty : DailyHighLow.Today.HighBgtTime.ToString("HH:mm", inv)));
			strb.Append(sep + (DailyHighLow.Today.HighWbgt == Cumulus.DefaultHiVal ? string.Empty : DailyHighLow.Today.HighWbgt.ToFixed(cumulus.TempFormat)));
			strb.Append(sep + (DailyHighLow.Today.HighWbgt == Cumulus.DefaultHiVal ? string.Empty : DailyHighLow.Today.HighWbgtTime.ToString("HH:mm", inv)));

			var entry = strb.ToString();

			cumulus.LogMessage("DoDayfile: Dayfile.txt entry:");
			cumulus.LogMessage(entry);

			var success = false;
			var retries = Cumulus.LogFileRetries;
			var charArr = (entry + Environment.NewLine).ToCharArray();

			do
			{
				try
				{
					cumulus.LogMessage("DoDayfile:Dayfile.txt opened for writing");

					if (DailyHighLow.Today.HighTemp < -400 || DailyHighLow.Today.LowTemp > 900)
					{
						cumulus.LogErrorMessage("DoDayfile: *** Error: Daily values are still at default at end of day");
						cumulus.LogErrorMessage("DoDayfile: Data not logged to dayfile.txt");
						return;
					}
					else
					{
						cumulus.LogMessage("DoDayfile: Writing entry to dayfile.txt");

						using var fs = new FileStream(cumulus.DayFileName, FileMode.Append, FileAccess.Write, FileShare.Read, charArr.Length, FileOptions.WriteThrough);
						using var file = new StreamWriter(fs);
						await file.WriteAsync(charArr, 0, charArr.Length);
						file.Close();
						fs.Close();

						success = true;

						cumulus.LogMessage($"DoDayfile: Log entry for {datestring} written");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("DoDayfile: Error writing to dayfile.txt: " + ex.Message);
					retries--;
					await Task.Delay(250);
				}
			} while (!success && retries >= 0);

			// Add a new record to the in memory dayfile data
			var tim = timestamp.AddDays(-1);
			var newRec = new DayFileRec()
			{
				Date = new DateTime(tim.Year, tim.Month, tim.Day, 0, 0, 0, DateTimeKind.Local),
				HighGust = DailyHighLow.Today.HighGust,
				HighGustBearing = DailyHighLow.Today.HighGustBearing,
				HighGustTime = DailyHighLow.Today.HighGustTime,
				LowTemp = DailyHighLow.Today.LowTemp,
				LowTempTime = DailyHighLow.Today.LowTempTime,
				HighTemp = DailyHighLow.Today.HighTemp,
				HighTempTime = DailyHighLow.Today.HighTempTime,
				LowPress = DailyHighLow.Today.LowPress,
				LowPressTime = DailyHighLow.Today.LowPressTime,
				HighPress = DailyHighLow.Today.HighPress,
				HighPressTime = DailyHighLow.Today.HighPressTime,
				HighRainRate = DailyHighLow.Today.HighRainRate,
				HighRainRateTime = DailyHighLow.Today.HighRainRateTime,
				TotalRain = MetData.RainToday,
				AvgTemp = AvgTemp,
				WindRun = MetData.WindRunToday,
				HighAvgWind = DailyHighLow.Today.HighWind,
				HighAvgWindTime = DailyHighLow.Today.HighWindTime,
				LowHumidity = DailyHighLow.Today.LowHumidity,
				LowHumidityTime = DailyHighLow.Today.LowHumidityTime,
				HighHumidity = DailyHighLow.Today.HighHumidity,
				HighHumidityTime = DailyHighLow.Today.HighHumidityTime,
				ET = MetData.ET,
				SunShineHours = cumulus.RolloverHour == 0 ? MetData.SunshineHours : MetData.SunshineToMidnight,
				HighHeatIndex = DailyHighLow.Today.HighHeatIndex,
				HighHeatIndexTime = DailyHighLow.Today.HighHeatIndexTime,
				HighAppTemp = DailyHighLow.Today.HighAppTemp,
				HighAppTempTime = DailyHighLow.Today.HighAppTempTime,
				LowAppTemp = DailyHighLow.Today.LowAppTemp,
				LowAppTempTime = DailyHighLow.Today.LowAppTempTime,
				HighHourlyRain = DailyHighLow.Today.HighHourlyRain,
				HighHourlyRainTime = DailyHighLow.Today.HighHourlyRainTime,
				LowWindChill = DailyHighLow.Today.LowWindChill,
				LowWindChillTime = DailyHighLow.Today.LowWindChillTime,
				HighDewPoint = DailyHighLow.Today.HighDewPoint,
				HighDewPointTime = DailyHighLow.Today.HighDewPointTime,
				LowDewPoint = DailyHighLow.Today.LowDewPoint,
				LowDewPointTime = DailyHighLow.Today.LowDewPointTime,
				DominantWindBearing = MetData.DominantWindBearing,
				HeatingDegreeDays = MetData.HeatingDegreeDays,
				CoolingDegreeDays = MetData.CoolingDegreeDays,
				HighSolar = DailyHighLow.Today.HighSolar,
				HighSolarTime = DailyHighLow.Today.HighSolarTime,
				HighUv = DailyHighLow.Today.HighUv,
				HighUvTime = DailyHighLow.Today.HighUvTime,
				HighFeelsLike = DailyHighLow.Today.HighFeelsLike,
				HighFeelsLikeTime = DailyHighLow.Today.HighFeelsLikeTime,
				LowFeelsLike = DailyHighLow.Today.LowFeelsLike,
				LowFeelsLikeTime = DailyHighLow.Today.LowFeelsLikeTime,
				HighHumidex = DailyHighLow.Today.HighHumidex,
				HighHumidexTime = DailyHighLow.Today.HighHumidexTime,
				ChillHours = MetData.ChillHours,
				HighRain24h = DailyHighLow.Today.HighRain24h,
				HighRain24hTime = DailyHighLow.Today.HighRain24hTime,
				HighBgt = DailyHighLow.Today.HighBgt == Cumulus.DefaultHiVal ? null : DailyHighLow.Today.HighBgt,
				HighBgtTime = DailyHighLow.Today.HighBgt == Cumulus.DefaultHiVal ? null : DailyHighLow.Today.HighBgtTime,
				HighWbgt = DailyHighLow.Today.HighWbgt == Cumulus.DefaultHiVal ? null : DailyHighLow.Today.HighWbgt,
				HighWbgtTime = DailyHighLow.Today.HighWbgt == Cumulus.DefaultHiVal ? null : DailyHighLow.Today.HighWbgtTime
			};

			DayFile.Add(newRec);

			// add to SQLite
			RecentDataDb.Insert(newRec);

			if (cumulus.MySqlFuncs.MySqlSettings.Dayfile.Enabled)
			{
				var queryString = new StringBuilder(cumulus.DayfileTable.StartOfInsert, 1024);
				queryString.Append(" Values(");
				queryString.Append(timestamp.AddDays(-1).ToString("\\'yy-MM-dd\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighGust.ToString(cumulus.WindFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighGustBearing);
				queryString.Append(sep + DailyHighLow.Today.HighGustTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowTemp.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.LowTempTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighTemp.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighTempTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowPress.ToString(cumulus.PressFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.LowPressTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighPress.ToString(cumulus.PressFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighPressTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighRainRate.ToString(cumulus.RainFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighRainRateTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + MetData.RainToday.ToString(cumulus.RainFormat, inv));
				queryString.Append(sep + AvgTemp.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + MetData.WindRunToday.ToString("F1", inv));
				queryString.Append(sep + DailyHighLow.Today.HighWind.ToString(cumulus.WindAvgFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighWindTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowHumidity);
				queryString.Append(sep + DailyHighLow.Today.LowHumidityTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighHumidity);
				queryString.Append(sep + DailyHighLow.Today.HighHumidityTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + MetData.ET.ToString(cumulus.ETFormat, inv));
				queryString.Append(sep + (cumulus.RolloverHour == 0 ? MetData.SunshineHours.ToString(cumulus.SunFormat, inv) : MetData.SunshineToMidnight.ToString(cumulus.SunFormat, inv)));
				queryString.Append(sep + DailyHighLow.Today.HighHeatIndex.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighHeatIndexTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighAppTemp.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighAppTempTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowAppTemp.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.LowAppTempTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighHourlyRain.ToString(cumulus.RainFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighHourlyRainTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowWindChill.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.LowWindChillTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighDewPoint.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighDewPointTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowDewPoint.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.LowDewPointTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + MetData.DominantWindBearing.ToString());
				queryString.Append(sep + MetData.HeatingDegreeDays.ToString("F1", inv));
				queryString.Append(sep + MetData.CoolingDegreeDays.ToString("F1", inv));
				queryString.Append(sep + DailyHighLow.Today.HighSolar.ToString());
				queryString.Append(sep + DailyHighLow.Today.HighSolarTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighUv.ToString(cumulus.UVFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighUvTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + "'" + CompassPoint(DailyHighLow.Today.HighGustBearing) + "'");
				queryString.Append(sep + "'" + CompassPoint(MetData.DominantWindBearing) + "'");
				queryString.Append(sep + DailyHighLow.Today.HighFeelsLike.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighFeelsLikeTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.LowFeelsLike.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.LowFeelsLikeTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + DailyHighLow.Today.HighHumidex.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighHumidexTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + MetData.ChillHours.ToFixed(cumulus.TempFormat));
				queryString.Append(sep + DailyHighLow.Today.HighRain24h.ToString(cumulus.RainFormat, inv));
				queryString.Append(sep + DailyHighLow.Today.HighRain24hTime.ToString("\\'HH:mm\\'", inv));
				queryString.Append(sep + (DailyHighLow.Today.HighBgt == Cumulus.DefaultHiVal ? "NULL" : DailyHighLow.Today.HighBgt.ToFixed(cumulus.TempFormat)));
				queryString.Append(sep + (DailyHighLow.Today.HighBgt == Cumulus.DefaultHiVal ? "NULL" : DailyHighLow.Today.HighBgtTime.ToString("\\'HH:mm\\'", inv)));
				queryString.Append(sep + (DailyHighLow.Today.HighWbgt == Cumulus.DefaultHiVal ? "NULL" : DailyHighLow.Today.HighWbgt.ToFixed(cumulus.TempFormat)));
				queryString.Append(sep + (DailyHighLow.Today.HighWbgt == Cumulus.DefaultHiVal ? "NULL" : DailyHighLow.Today.HighWbgtTime.ToString("\\'HH:mm\\'", inv)));
				queryString.Append(')');

				if (cumulus.NormalRunning)
				{
					// run the query async so we do not block the main EOD processing
					await cumulus.MySqlFuncs.MySqlCommandAsync(queryString.ToString(), "DoDayfile");
				}
				else
				{
					// save the string for later
					cumulus.LogDebugMessage("DoDayfile:: Buffering MySQL insert for later processing");
					cumulus.MySqlFuncs.MySqlList.Enqueue(new SqlCache() { statement = queryString.ToString() });
				}
			}
		}

		/// <summary>
		///  Calculate checksum of data received from serial port
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

		public string CompassPoint(int bearing)
		{
			return bearing == 0 ? "-" : cumulus.Trans.compassp[(bearing * 100 + 1125) % 36000 / 2250];
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

		public void AddRecentDataWithAq(DateTime timestamp, double windAverage, double recentMaxGust, double windLatest, int bearing, int avgBearing, double outsidetemp,
			double windChill, double dewpoint, double heatIndex, int humidity, double pressure, double rainToday, int? solarRad, double? uv, double rainCounter, double feelslike, double humidex,
			double appTemp, double? insideTemp, int? insideHum, int solarMax, double rainrate, double? bgt, double? wbgt)
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

			AddRecentDataEntry(timestamp, windAverage, recentMaxGust, windLatest, bearing, avgBearing, outsidetemp, windChill, dewpoint, heatIndex, humidity, pressure, rainToday, solarRad, uv, rainCounter, feelslike, humidex, appTemp, insideTemp, insideHum, solarMax, rainrate, pm2p5, pm10, bgt, wbgt);
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


		public static double DegToRad(int deg)
		{
			return deg * Math.PI / 180;
		}

		public static double RadToDeg(double rad)
		{
			return rad * 180 / Math.PI;
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

		public double YesterdayWindRun { get; set; }
		public double AnnualETTotal { get; set; }
		public double StartofdayET { get; set; }

		public int ConsecutiveRainDays { get; set; }
		public int ConsecutiveDryDays { get; set; }
		public DateTime FOSensorClockTime { get; set; }
		public DateTime FOStationClockTime { get; set; }
		public DateTime FOSolarClockTime { get; set; }
		public double YestAvgTemp { get; set; }
		public double RainLast24Hour { get; set; }
		public string ConBatText { get; set; }
		public string ConSupplyVoltageText { get; set; }
		public decimal CapacitorVolt { get; set; }
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
		public DateTime DataStoppedTime { get; set; }
		public bool IsRaining { get; set; }

		public void LoadLastHoursFromDataLogs(DateTime ts)
		{
			cumulus.LogMessage("Loading last N hour data from data logs: " + ts.ToCmxLogFormat());
			LoadRecentFromDataLogs(ts);
			LoadRecentAqFromDataLogs(ts);
			LoadRecentAqFromDataLogsNew(ts);
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
			var logFile = cumulus.GetLogFileName(filedate);
			var finished = false;
			var numtoadd = 0;
			var numadded = 0;

			var rowsToAdd = new List<RecentData>();

			cumulus.LogMessage($"LoadRecent: Attempting to load {cumulus.RecentDataDays} days of entries to recent data list");

			// try and find the first entry in the database
			try
			{
				var start = RecentDataDb.ExecuteScalar<long>("select MAX(Timestamp) from RecentData").LocalFromUnixTime();
				if (datefrom < start)
					datefrom = start;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("LoadRecent: Error querying database for latest record - " + e.Message);
			}


			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;
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

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line))
									continue;

								var rec = new LogFileRec(line);
								entrydate = rec.DateTime;

								if (entrydate >= datefrom && entrydate <= dateto)
								{
									rowsToAdd.Add(new RecentData()
									{
										DateTime = entrydate,
										DewPoint = rec.OutdoorDewpoint,
										HeatIndex = rec.HeatIndex ?? 0,
										Humidity = rec.OutdoorHumidity,
										OutsideTemp = rec.OutdoorTemperature,
										Pressure = rec.Pressure,
										RainToday = rec.RainToday,
										SolarRad = rec.SolarRad,
										UV = rec.UV,
										WindAvgDir = rec.AvgBearing,
										WindGust = rec.RecentMaxGust,
										WindLatest = rec.WindLatest,
										WindChill = rec.WindChill ?? 0,
										WindDir = rec.Bearing ?? 0,
										WindSpeed = rec.WindAverage,
										raincounter = rec.RainCounter,
										FeelsLike = rec.FeelsLike ?? 0,
										Humidex = rec.Humidex ?? 0,
										AppTemp = rec.ApparentTemperature ?? 0,
										IndoorTemp = rec.IndoorTemperature,
										IndoorHumidity = rec.IndoorHumidity,
										SolarMax = (int) rec.CurrentSolarMax,
										RainRate = rec.RainRate,
										Pm2p5 = -1,
										Pm10 = -1,
										BGT = rec.BlackGlobeTemp,
										WBGT = rec.WetBulbGlobeTemp
									});
									++numtoadd;
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"LoadRecent: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"LoadRecent: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"LoadRecent: Too many errors reading {logFile} - aborting load of data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"LoadRecent: Error reading the logfile {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}

					try
					{
						if (rowsToAdd.Count > 0)
							numadded = RecentDataDb.InsertAll(rowsToAdd, "OR IGNORE", true);
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
			} while (!finished); ;

			cumulus.LogMessage($"LoadRecent: Loaded {numadded} of {numtoadd} new entries to recent database");
		}

		private void LoadRecentAqFromDataLogs(DateTime ts)
		{
			var datefrom = ts.AddDays(-cumulus.RecentDataDays);
			var dateto = ts;
			var entrydate = datefrom;
			var filedate = datefrom;
			string logFile;
			var finished = false;
			var updatedCount = 0;
			var inv = CultureInfo.InvariantCulture;

			if (cumulus.StationOptions.PrimaryAqSensor < 0) return;

			// try and find the first entry in the database that has a "blank" AQ entry (PM2.5 or PM10 = -1)
			try
			{
				var start = RecentDataDb.ExecuteScalar<long>("select Timestamp from RecentData where Pm2p5=-1 or Pm10=-1 order by Timestamp limit 1").LocalFromUnixTime();
				if (start == DateTime.MinValue)
					return;

				if (datefrom < start)
					datefrom = start;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("LoadRecentAqFromDataLogs: Error querying database for oldest record without AQ data - " + e.Message);
			}

			cumulus.LogMessage($"LoadRecentAqFromDataLogs: Attempting to load {cumulus.RecentDataDays} days of entries to Air Quality recent data");

			if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor
				|| cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				logFile = cumulus.GetAirLinkLogFileName(filedate);
			}
			else if (cumulus.StationOptions.PrimaryAqSensor >= (int) Cumulus.PrimaryAqSensor.Sensor1 && cumulus.StationOptions.PrimaryAqSensor <= (int) Cumulus.PrimaryAqSensor.Sensor4 ||
					cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2) // Ecowitt
			{
				logFile = cumulus.GetExtraLogFileName(filedate);
			}
			else
			{
				cumulus.LogErrorMessage($"LoadRecentAqFromDataLogs: Error - The primary AQ sensor is not set to a valid value, currently={cumulus.StationOptions.PrimaryAqSensor}");
				return;
			}

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

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
								var st = new List<string>(line.Split(','));
								entrydate = long.Parse(st[1]).LocalFromUnixTime();

								if (entrydate >= datefrom && entrydate <= dateto)
								{
									// entry is from required period
									double pm2p5, pm10;
									string str2p5, str10;
									if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
									{
										// AirLink Indoor
										str2p5 = st[5];
										str10 = st[10];
									}
									else if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor)
									{
										// AirLink Outdoor
										str2p5 = st[32];
										str10 = st[37];
									}
									else if (cumulus.StationOptions.PrimaryAqSensor >= (int) Cumulus.PrimaryAqSensor.Sensor1 && cumulus.StationOptions.PrimaryAqSensor <= (int) Cumulus.PrimaryAqSensor.Sensor4)
									{
										// Sensor 1-4 - fields 68 -> 71
										str2p5 = st[67 + cumulus.StationOptions.PrimaryAqSensor];
										str10 = "-1";
									}
									else
									{
										// Ecowitt CO2 sensor
										str2p5 = st[86];
										str10 = st[88];
									}

									if (double.TryParse(str2p5, NumberStyles.Number, inv, out pm2p5) && double.TryParse(str10, NumberStyles.Number, inv, out pm10))
									{
										UpdateRecentDataAqEntry(entrydate, pm2p5, pm10);
										updatedCount++;
									}
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
					else if (cumulus.StationOptions.PrimaryAqSensor >= (int) Cumulus.PrimaryAqSensor.Sensor1
						&& cumulus.StationOptions.PrimaryAqSensor <= (int) Cumulus.PrimaryAqSensor.Sensor4
						|| cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2) // Ecowitt
					{
						logFile = cumulus.GetExtraLogFileName(filedate);
					}
				}
			} while (!finished);

			cumulus.LogMessage($"LoadRecentAqFromDataLogs: Loaded {updatedCount} new entries to recent database");
		}

		private void LoadRecentAqFromDataLogsNew(DateTime ts)
		{
			var datefrom = ts.AddDays(-cumulus.RecentDataDays).ToUnixTime();
			var dateto = ts.ToUnixTime();
			var entrydate = datefrom;
			var filedate = ts.AddDays(-cumulus.RecentDataDays);
			var filedateto = filedate.AddMonths(1);
			string logFile;
			var finished = false;
			var updatedCount = 0;
			var inv = CultureInfo.InvariantCulture;

			if (cumulus.StationOptions.PrimaryAqSensor < 0) return;

			// try and find the first entry in the database that has a "blank" AQ entry (PM2.5 or PM10 = -1)
			try
			{
				var start = RecentDataDb.ExecuteScalar<long>("select max(Timestamp) from RecentAqData");
				if (start >= cumulus.LastUpdateTime.ToUnixTime())
					return;

				if (datefrom < start)
					datefrom = start;
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("LoadRecentAqFromDataLogsNew: Error querying database for last record - " + e.Message);
			}


			cumulus.LogMessage($"LoadRecentAqFromDataLogsNew: Attempting to load {cumulus.RecentDataDays} days of entries to Air Quality recent data");

			logFile = cumulus.GetExtraLogFileName(filedate);

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

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
								var st = new List<string>(line.Split(','));
								entrydate = long.Parse(st[1]);

								if (st.Count >= 123 && entrydate >= datefrom && entrydate <= dateto)
								{
									// entry is from required period

									// Standard sensor 1-4 - fields 68 -> 71
									// 119-122 AQ PM10

									var rec = new RecentAqData
									{
										DateTime = entrydate.LocalFromUnixTime(),
										Pm2p5_1 = string.IsNullOrEmpty(st[68]) ? null : double.Parse(st[68], NumberStyles.Number, inv),
										Pm2p5_2 = string.IsNullOrEmpty(st[69]) ? null : double.Parse(st[69], NumberStyles.Number, inv),
										Pm2p5_3 = string.IsNullOrEmpty(st[70]) ? null : double.Parse(st[70], NumberStyles.Number, inv),
										Pm2p5_4 = string.IsNullOrEmpty(st[71]) ? null : double.Parse(st[71], NumberStyles.Number, inv),
										Pm10_1 = string.IsNullOrEmpty(st[119]) ? null : double.Parse(st[119], NumberStyles.Number, inv),
										Pm10_2 = string.IsNullOrEmpty(st[120]) ? null : double.Parse(st[120], NumberStyles.Number, inv),
										Pm10_3 = string.IsNullOrEmpty(st[121]) ? null : double.Parse(st[121], NumberStyles.Number, inv),
										Pm10_4 = string.IsNullOrEmpty(st[122]) ? null : double.Parse(st[122], NumberStyles.Number, inv),

									};

									if (null != (rec.Pm2p5_1 ?? rec.Pm2p5_2 ?? rec.Pm2p5_3 ?? rec.Pm2p5_4 ?? rec.Pm10_1 ?? rec.Pm10_2 ?? rec.Pm10_3 ?? rec.Pm10_4))
									{
										updatedCount += RecentDataDb.Insert(rec, "or ignore");
									}
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"LoadRecentAqFromDataLogsNew: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 20)
								{
									cumulus.LogErrorMessage($"LoadRecentAqFromDataLogsNew: Too many errors reading {logFile} - aborting load of AQ data");
								}
							}
						}

						RecentDataDb.Commit();
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"LoadRecentAqFromDataLogsNew: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
						RecentDataDb.Rollback();
					}
				}

				if (entrydate >= dateto || filedate > filedateto)
				{
					finished = true;
				}
				else
				{
					filedate = filedate.AddMonths(1);
					logFile = cumulus.GetExtraLogFileName(filedate);
				}
			} while (!finished);

			cumulus.LogMessage($"LoadRecentAqFromDataLogsNew: Loaded {updatedCount} new entries to recent database");
		}


		private void LoadRecentWindRose()
		{
			// We can now just query the recent data database as it has been populated from the logs
			var datefrom = DateTime.Now.AddHours(-24);

			var result = RecentDataDb.Query<RecentData>("select WindGust, WindDir from RecentData where Timestamp >= ? order by Timestamp", datefrom.ToUnixTime());

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

			var result = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp >= ? and Timestamp <= ? order by Timestamp", datefrom.ToUnixTime(), dateto.ToUnixTime());

			if (result.Count != 0)
			{
				lock (recentwindLock)
				{
					var timestamps = WindRecent.Select(rec => rec.Timestamp);

					// get the min and max timestamps from the recent wind array
					var minWindTs = timestamps.Min();
					var maxWindTs = timestamps.Max();

					foreach (var rec in result)
					{
						try
						{
							if (rec.DateTime < minWindTs || rec.DateTime > maxWindTs)
							{
								WindRecent[nextwind].GustUncal = cumulus.Calib.WindGust.UnCalibatrate(rec.WindGust);
								WindRecent[nextwind].SpeedUncal = cumulus.Calib.WindSpeed.UnCalibatrate(rec.WindSpeed);
								WindRecent[nextwind].Timestamp = rec.DateTime;
								nextwind = (nextwind + 1) % MaxWindRecent;
							}

							WindVec[nextwindvec].X = rec.WindGust * Math.Sin(DegToRad(rec.WindDir));
							WindVec[nextwindvec].Y = rec.WindGust * Math.Cos(DegToRad(rec.WindDir));
							WindVec[nextwindvec].Timestamp = rec.DateTime;
							WindVec[nextwindvec].Bearing = MetData.Bearing; // savedBearing
							nextwindvec = (nextwindvec + 1) % MaxWindRecent;
						}
						catch (Exception e)
						{
							cumulus.LogErrorMessage($"LoadLast3Hour: Error loading data from database : {e.Message}");
						}
					}
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
					WindRecent[i].GustUncal = result[i].Gust;
					WindRecent[i].SpeedUncal = result[i].Speed;
					WindRecent[i].Timestamp = result[i].DateTime;
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
				lock (recentwindLock)
				{
					for (var i = 0; i < WindRecent.Length; i++)
					{
						if (WindRecent[i].Timestamp > DateTime.MinValue)
							RecentDataDb.Execute("insert or replace into CWindRecent (Timestamp,Gust,Speed) values (?,?,?)", WindRecent[i].Timestamp.ToUnixTime(), WindRecent[i].GustUncal, WindRecent[i].SpeedUncal);
					}

					// and save the pointer
					RecentDataDb.Execute("insert into WindRecentPointer (pntr) values (?)", nextwind);

					RecentDataDb.Commit();
				}
				cumulus.LogMessage($"SaveWindData: Saved the wind speeds array");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"SaveWindData: Error saving RecentWind to the database : {ex.Message}");
				RecentDataDb.Rollback();
			}
		}

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

		public string LoadDayFile()
		{
			var addedToList = 0;
			var addedToDb = 0;

			StringBuilder msg = new();

			cumulus.LogMessage("LoadDayFile: Attempting to load the day file");
			if (dayfileReloading)
			{
				cumulus.LogMessage("LoadDayFile: A reload is already in progress, ignoring this request");
				return "A reload is already in progress, ignoring this request";
			}

			dayfileReloading = true;

			try
			{

				if (File.Exists(cumulus.DayFileName))
				{
					var linenum = 0;
					var errorCount = 0;
					var duplicateCount = 0;

					var watch = Stopwatch.StartNew();

					// Clear the existing list
					DayFile.Clear();
					RecentDataDb.Execute("DELETE FROM DayFileRec");

					var lines = File.ReadAllLines(cumulus.DayFileName);

					foreach (var line in lines)
					{
						try
						{
							// process each record in the file
							linenum++;
							var newRec = new DayFileRec(line);

							if (DayFile.Exists(x => x.Date == newRec.Date))
							{
								cumulus.LogErrorMessage($"ERROR: Duplicate entry in dayfile for {newRec.Date:d}");
								msg.Append($"ERROR: Duplicate entry in dayfile for {newRec.Date:d}<br>");
								duplicateCount++;
							}

							DayFile.Add(newRec);

							addedToList++;
						}
						catch (Exception e)
						{
							if (errorCount < 20)
							{
								cumulus.LogErrorMessage($"LoadDayFile: Error at line {linenum} of {cumulus.DayFileName} : {e.Message}");
								msg.Append($"Error at line {linenum} of {cumulus.DayFileName}<br>");
								cumulus.LogMessage("Please edit the file to correct the error");
							}

							errorCount++;
						}
					}

					if (duplicateCount == 0)
					{
						try
						{
							addedToDb = RecentDataDb.InsertAll(DayFile, true);
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, "Error adding day file entries to SQLite");
						}
					}

					watch.Stop();
					cumulus.LogDebugMessage($"LoadDayFile: Dayfile parse = {watch.ElapsedMilliseconds} ms");

					cumulus.LogMessage($"LoadDayFile: Loaded {addedToList} entries to recent daily data list");
					cumulus.LogMessage($"LoadDayFile: Loaded {addedToDb} entries to SQLite database");
					msg.Append($"Loaded {addedToList} entries to recent daily data list<br>");
					msg.Append($"Loaded {addedToDb} entries to SQLite database");

					if (errorCount > 20)
					{
						cumulus.LogErrorMessage($"LoadDayFile: Lines not loaded due to errors {errorCount}");
						msg.Append($"Lines not loaded due to errors {errorCount}<br>");
					}

					if (duplicateCount > 0)
					{
						cumulus.LogErrorMessage($"LoadDayFile: Found {duplicateCount} duplicate entries, please correct your dayfile and try again");
						msg.Append($"Found {duplicateCount} duplicate entries<br>");
					}

					if (errorCount > 0 || duplicateCount > 0)
					{
						msg.Append("Correct your dayfile and try again");
					}

					dayfileReloading = false;
					return msg.ToString();
				}
				else if (cumulus.RecordsBeganDateTime.Date == DateTime.Now.Date)
				{
					var msg1 = "LoadDayFile: No Dayfile has been created yet";
					cumulus.LogMessage(msg1);
					dayfileReloading = false;
					return msg1;
				}
				else
				{
					var msg1 = "LoadDayFile: No Dayfile found - No entries added to recent daily data list";
					if (File.Exists(cumulus.YesterdayFile))
					{
						cumulus.LogErrorMessage(msg1);
					}
					dayfileReloading = false;
					return msg1;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "LoadDayFile: Error");
				dayfileReloading = false;
				return "Error processing dayfile: " + ex.Message;
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


		// This overridden in each station implementation
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

		public static string PressINstr(double pressure)
		{
			return ConvertUnits.UserPressToIN(pressure).ToString("F3", CultureInfo.InvariantCulture);
		}

		public static string PressPAstr(double pressure)
		{
			// return value to 100 * hPa
			return (ConvertUnits.UserPressToMB(pressure) * 100).ToString("F0", CultureInfo.InvariantCulture);
		}

		public string WindMPHStr(double wind)
		{
			var windMPH = ConvertUnits.UserWindToMPH(wind);
			if (cumulus.StationOptions.RoundWindSpeed)
			{
				windMPH = Math.Round(windMPH);
				return windMPH.ToString("F0", CultureInfo.InvariantCulture);
			}

			return windMPH.ToString("F1", CultureInfo.InvariantCulture);
		}

		public string WindMSStr(double wind)
		{
			var windMS = ConvertUnits.UserWindToMS(wind);
			if (cumulus.StationOptions.RoundWindSpeed)
			{
				windMS = Math.Round(windMS);
				return windMS.ToString("F0", CultureInfo.InvariantCulture);
			}

			return windMS.ToString("F1", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to inches for WU etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		public static string RainINstr(double rain)
		{
			return ConvertUnits.UserRainToIN(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert rain in user units to mm for APIs etc
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		public static string RainMMstr(double rain)
		{
			return ConvertUnits.UserRainToMM(rain).ToString("F2", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Convert temp in user units to F for WU etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		public static string TempFstr(double temp)
		{
			return ConvertUnits.UserTempToF(temp).ToFixed("F1");
		}

		/// <summary>
		/// Convert temp in user units to C for APIs etc
		/// </summary>
		/// <param name="temp"></param>
		/// <returns></returns>
		public static string TempCstr(double temp)
		{
			return ConvertUnits.UserTempToC(temp).ToFixed("F1");
		}


		/// <summary>
		/// Return lines from dayfile.txt in json format
		/// </summary>
		/// <param name="draw"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <returns>JSON encoded section of the dayfile</returns>
		public string ReadDayfile(string draw, int start, int length, string search)
		{
			// sanity check for first day of data - there is no dayfile!
			if (cumulus.RecordsBeganDateTime.Date == DateTime.Now.Date)
			{
				return "";
			}

			try
			{
				var lines = File.ReadAllLines(cumulus.DayFileName);
				var total = lines.Length;
				var filtered = 0;
				var thisDraw = 0;

				var json = new StringBuilder(350 * lines.Length);

				json.Append("{\"data\":[");

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

						var fields = line.Split(',');
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
			catch (FieldAccessException)
			{
				cumulus.LogErrorMessage("GetDayFile: Error: Dayfile is not not found!");
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "GetDayFile: Error");
			}

			return "";
		}


		/// <summary>
		/// Return rows from recent data database in json format
		/// </summary>
		/// <param name="draw"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <returns>JSON encoded section of the recent data</returns>
		public string ReadRecentData(string draw, int start, int length, string search)
		{
			try
			{
				var data = RecentDataDb.Query<RecentData>("select * from RecentData order by Timestamp");

				var total = data.Count;
				var filtered = 0;
				var thisDraw = 0;

				var json = new StringBuilder(350 * total);

				json.Append("{\"data\":[");


				foreach (var row in data)
				{
					var csv = row.ToCsv();

					// if we have a search string and no match, skip to next line
					if (!string.IsNullOrEmpty(search) && !csv.Contains(search))
					{
						continue;
					}

					// this line either matches the search or these is no search
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

						json.Append(csv);
						json.Append(',');
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
				cumulus.LogExceptionMessage(ex, "ReadRecentData: Error");
			}

			return "";
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

				var ts = new DateTime(int.Parse(stDate[0]), int.Parse(stDate[1]), int.Parse(stDate[2]), 0, 0, 0, DateTimeKind.Local);
				var te = new DateTime(int.Parse(enDate[0]), int.Parse(enDate[1]), int.Parse(enDate[2]), 0, 0, 0, DateTimeKind.Local);

				// we want the records up to but not including the end date at midnight
				te = te.AddDays(1);

				// allow for 9am start time
				ts = ts.AddHours(-cumulus.GetHourInc(ts));
				te = te.AddHours(-cumulus.GetHourInc(te));

				var startTs = ts.ToUnixTime();
				var endTs = te.ToUnixTime();

				var fileDate = ts;

				var logfile = extra ? cumulus.GetExtraLogFileName(ts) : cumulus.GetLogFileName(ts);
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

				do
				{
					if (File.Exists(logfile))
					{
						cumulus.LogDebugMessage($"GetLogfile: Processing log file - {logfile}");

						var lines = File.ReadAllLines(logfile);
						var lineNum = 0;

						foreach (var line in lines)
						{
							lineNum++;

							var fields = line.Split(',');

							var entryDate = long.Parse(fields[1]);

							if (entryDate >= startTs)
							{
								if (entryDate >= endTs)
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
					if (te <= new DateTime(fileDate.Year, fileDate.Month, 1, -cumulus.GetHourInc(te), 0, 0, DateTimeKind.Local))
					{
						finished = true;
						cumulus.LogDebugMessage("GetLogfile: Finished processing log files");
					}

					if (!finished)
					{
						cumulus.LogDebugMessage($"GetLogfile: Finished processing log file - {logfile}");
						logfile = extra ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetLogFileName(fileDate);
					}

				} while (!finished);

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


		public string GetIntervalData(string from, string to, string fields)
		{
			var fromDate = long.Parse(from).LocalFromUnixTime();
			var toDate = long.Parse(to).LocalFromUnixTime();
			var flds = (fields ?? "").Split(',').Select(int.Parse).ToArray();

			if (flds.Length == 0)
			{
				return "[]";
			}

			var ts = fromDate;
			var te = toDate;
			var fileDate = new DateTime(ts.Year, ts.Month, 15, 0, 0, 0, DateTimeKind.Local);

			var data = new Dictionary<string, List<string>>();

			// add the headers
			var fldNames = new List<string>();
			var useLogFile = false;
			var useExtraFile = false;

			try
			{
				foreach (var fld in flds)
				{
					if (fld < 1000)
					{
						fldNames.Add(cumulus.LogFileFieldNames[fld]);
						useLogFile = true;
					}
					else
					{
						fldNames.Add(cumulus.ExtraFileFieldNames[fld - 1000]);
						useExtraFile = true;
					}
				}
				data.Add("Date/Time", fldNames);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetIntervalData: Error processing input fields: " + ex.Message);
				return "[]";
			}

			var finished = false;
			var json = new StringBuilder("[", flds.Length * 512);

			try
			{
				do
				{
					if (useLogFile)
					{
						var logfile = cumulus.GetLogFileName(fileDate);

						if (!File.Exists(logfile))
						{
							cumulus.LogErrorMessage($"GetIntervalData: Error, file does not exist: {logfile}");
							return "[]";
						}

						cumulus.LogDebugMessage($"GetIntervalData: Processing log file - {logfile}");

						// read the log file into a List
						var lines = File.ReadAllLines(logfile).ToList();

						foreach (var line in lines)
						{
							var vars = line.Split(',');
							var date = long.Parse(vars[1]).LocalFromUnixTime();
							var dateStr = vars[0];

							if (date >= fromDate && date <= toDate)
							{
								var fieldList = (from indx in flds
												 where indx < 1000
												 select vars[indx]).ToList();

								data.TryAdd(dateStr, fieldList);
							}
						}
					}

					if (useExtraFile)
					{
						var logfile = cumulus.GetExtraLogFileName(fileDate);

						if (!File.Exists(logfile))
						{
							cumulus.LogErrorMessage($"GetIntervalData: Error, file does not exist: {logfile}");
							return "[]";
						}

						cumulus.LogDebugMessage($"GetIntervalData: Processing extra log file - {logfile}");

						// read the log file into a List
						var lines = File.ReadAllLines(logfile).ToList();

						foreach (var line in lines)
						{
							var fieldList = new List<string>();
							var vars = line.Split(',');
							var date = long.Parse(vars[1]).LocalFromUnixTime();
							var dateStr = vars[0];

							if (date >= fromDate && date <= toDate)
							{
								fieldList.AddRange(from indx in flds
												   where indx >= 1000
												   select vars[indx - 1000]);

								if (data.TryGetValue(dateStr, out var value))
								{
									value.AddRange(fieldList);
								}
								else
								{
									data.TryAdd(dateStr, fieldList);
								}
							}
						}
					}

					// might need the next months log
					fileDate = fileDate.AddMonths(1);

					// have we run out of log entries?
					// filedate is 15th on month, compare against the first
					if (te <= fileDate.AddDays(-14))
					{
						finished = true;
						cumulus.LogDebugMessage("GetIntervalData: Finished processing log files");
					}
				} while (!finished);

				foreach (var rec in data)
				{
					json.Append($"[\"{rec.Key}\",\"{string.Join($"\",\"", rec.Value)}\"],");
				}

				json.Length -= 1;
				json.Append(']');

				return json.ToString();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetIntervalData: Error - " + ex.ToString());
			}

			return "[]";
		}


		public string GetDailyData(string from, string to, string fields)
		{
			var fromDate = long.Parse(from).LocalFromUnixTime();
			var toDate = long.Parse(to).LocalFromUnixTime();
			var flds = (fields ?? "").Split(',').Select(int.Parse).ToArray();

			if (flds.Length == 0)
			{
				return "[]";
			}

			var data = new Dictionary<string, List<string>>();

			// add the headers
			var fldNames = new List<string>();
			try
			{
				foreach (var fld in flds)
				{
					fldNames.Add(cumulus.DayfileFieldNames[fld]);
				}
				data.Add("Date", fldNames);
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetDailyData: Error processing input fields: " + ex.Message);
				return "[]";
			}

			var json = new StringBuilder("[", flds.Length * 512);

			try
			{
				cumulus.LogDebugMessage("GetDailyData: Processing day file records");

				if (!File.Exists(cumulus.DayFileName))
				{
					cumulus.LogErrorMessage("GetDailyData: Error, day file does not exist");
					return "[]";
				}

				cumulus.LogDebugMessage("GetDailyData: Processing day file");

				// read the log file into a List
				var lines = File.ReadAllLines(cumulus.DayFileName).ToList();

				foreach (var line in lines)
				{
					var vars = line.Split(',');
					var date = Utils.ddmmyyStrToDate(vars[0]);

					if (date >= fromDate && date <= toDate)
					{
						var fieldList = new List<string>();

						foreach (var indx in flds)
						{
							fieldList.Add(vars[indx]);
						}

						data.TryAdd(vars[0], fieldList);
					}
				}

				foreach (var rec in data)
				{
					json.Append($"[\"{rec.Key}\",\"{string.Join($"\",\"", rec.Value)}\"],");
				}

				json.Length -= 1;
				json.Append(']');

				return json.ToString();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("GetDailyData: Error - " + ex.ToString());
			}

			return "[]";

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



		public string GetAllDegreeDaysGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;

			var sb = new StringBuilder("{");
			var growdegdaysYears1 = new StringBuilder("{", 32768);
			var growdegdaysYears2 = new StringBuilder("{", 32768);

			var growYear1 = new StringBuilder("[", 8600);
			var growYear2 = new StringBuilder("[", 8600);

			var options = $"\"options\":{{\"gddBase1\":{cumulus.GrowingBase1},\"gddBase2\":{cumulus.GrowingBase2},\"startMon\":{cumulus.GrowingYearStarts}}}";

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = cumulus.GrowingYearStarts < 3 ? 2000 : 1999;

			int startYear;

			var annualGrowingDegDays1 = 0.0;
			var annualGrowingDegDays2 = 0.0;

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0 && (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) || cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local)))
			{
				// we have to detect a new growing deg day year is starting
				nextYear = new DateTime(DayFile[0].Date.Year, cumulus.GrowingYearStarts, 1, 0, 0, 0, DateTimeKind.Local);

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


				for (var i = 0; i < DayFile.Count; i++)
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
					if (cumulus.GrowingYearStarts > 2 && plotYear == 1999 && DayFile[i].Date.Month < cumulus.GrowingYearStarts)
					{
						plotYear++;
					}

					// make all series the same year so they plot together
					var recDate = new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day, 0, 0, 0, DateTimeKind.Local).ToUnixTimeMs();

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
				if (growYear1[^1] == ',')
				{
					growYear1.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears1[^1] == ']')
				{
					growdegdaysYears1.Append(',');
				}

				growdegdaysYears1.Append(growYear1 + "]");

				// add to main json
				sb.Append("\"GDD1\":" + growdegdaysYears1 + "},");
			}
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
			{
				if (growYear2[^1] == ',')
				{
					growYear2.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears2[^1] == ']')
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
			var InvC = CultureInfo.InvariantCulture;

			var sb = new StringBuilder("{");
			var tempSumYears0 = new StringBuilder("{", 32768);
			var tempSumYears1 = new StringBuilder("{", 32768);
			var tempSumYears2 = new StringBuilder("{", 32768);

			var tempSum0 = new StringBuilder("[", 8600);
			var tempSum1 = new StringBuilder("[", 8600);
			var tempSum2 = new StringBuilder("[", 8600);

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
			if (DayFile.Count > 0 && (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum1.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum2.IsVisible(local)))
			{
				// we have to detect a new year is starting
				nextYear = new DateTime(DayFile[0].Date.Year, cumulus.TempSumYearStarts, 1, 0, 0, 0, DateTimeKind.Local);

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

				for (var i = 0; i < DayFile.Count; i++)
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
					if (cumulus.TempSumYearStarts > 2 && plotYear == 1999 && DayFile[i].Date.Month < cumulus.TempSumYearStarts)
					{
						plotYear++;
					}

					var recDate = new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day, 0, 0, 0, DateTimeKind.Local).ToUnixTimeMs();

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
				if (tempSum0[^1] == ',')
				{
					tempSum0.Length--;
				}

				// have previous years been appended?
				if (tempSumYears0[^1] == ']')
				{
					tempSumYears0.Append(',');
				}

				tempSumYears0.Append(tempSum0 + "]");

				// add to main json
				sb.Append("\"Sum0\":" + tempSumYears0 + "},");
			}
			if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
			{
				if (tempSum1[^1] == ',')
				{
					tempSum1.Length--;
				}

				// have previous years been appended?
				if (tempSumYears1[^1] == ']')
				{
					tempSumYears1.Append(',');
				}

				tempSumYears1.Append(tempSum1 + "]");

				// add to main json
				sb.Append("\"Sum1\":" + tempSumYears1 + "},");
			}
			if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
			{
				if (tempSum2[^1] == ',')
				{
					tempSum2.Length--;
				}

				// have previous years been appended?
				if (tempSumYears2[^1] == ']')
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

		public string GetAllChillHrsGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;

			if (!cumulus.GraphOptions.Visible.ChillHours.IsVisible(local))
			{
				return "{}";
			}

			var sb = new StringBuilder("{");
			var chillHrsYears = new StringBuilder("{", 32768);

			var chillhrs = new StringBuilder("[", 8600);

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = cumulus.ChillHourSeasonStart < 3 ? 2000 : 1999;

			int startYear;

			var options = $"\"options\":{{\"threshold\":{cumulus.ChillHourThreshold},\"basetemp\":{cumulus.ChillHourBase},\"startMon\":{cumulus.ChillHourSeasonStart}}}";

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				// we have to detect a new year is starting
				nextYear = new DateTime(DayFile[0].Date.Year, cumulus.ChillHourSeasonStart, 1, 0, 0, 0, DateTimeKind.Local);

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

				chillHrsYears.Append($"\"{startYear}\":");

				for (var i = 0; i < DayFile.Count; i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (DayFile[i].Date >= nextYear)
					{
						if (chillhrs.Length > 10)
						{
							// remove last comma
							chillhrs.Length--;
							// close the year data
							chillhrs.Append("],");
							// append to years array
							chillHrsYears.Append(chillhrs);

							chillhrs.Clear().Append($"\"{DayFile[i].Date.Year}\":[");
						}

						// reset the plot year for Southern hemisphere
						plotYear = cumulus.ChillHourSeasonStart < 3 ? 2000 : 1999;

						do
						{
							nextYear = nextYear.AddYears(1);
						}
						while (DayFile[i].Date >= nextYear);
					}
					// make all series the same year so they plot together
					// 2000 was a leap year, so make sure February falls in 2000
					// for Southern hemisphere this means the start year must be 1999
					if (cumulus.ChillHourSeasonStart > 2 && plotYear == 1999 && DayFile[i].Date.Month < cumulus.ChillHourSeasonStart)
					{
						plotYear++;
					}

					var recDate = new DateTime(plotYear, DayFile[i].Date.Month, DayFile[i].Date.Day, 0, 0, 0, DateTimeKind.Local).ToUnixTimeMs();

					// annual accumulation
					chillhrs.Append($"[{recDate},{DayFile[i].ChillHours.ToString("F0", InvC)}],");
				}
			}

			// remove last commas from the years arrays and close them off
			if (chillhrs[^1] == ',')
			{
				chillhrs.Length--;
			}

			// have previous years been appended?
			if (chillHrsYears[^1] == ']')
			{
				chillHrsYears.Append(',');
			}

			chillHrsYears.Append(chillhrs + "]");

			// add to main json
			sb.Append("\"data\":" + chillHrsYears + "},");

			sb.Append(options);

			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllSnowGraphData(bool local)
		{
			/* returns:
			 *	snowdepth:[[date1,val1],[date2,val2]...],
			 *	snow24h:[[date1,val1],[date2,val2]...]
			 */

			var InvC = CultureInfo.InvariantCulture;

			var sb = new StringBuilder("{");
			var snowdepth = new StringBuilder("[", 32768);
			var snow24h = new StringBuilder("[", 32768);

			// Read the diary database
			// get the earlist record date
			var earliest = cumulus.DiaryDB.Query<DiaryData>("select * from DiaryData order by Date limit 1");

			if (earliest.Count == 1)
			{
				var query = string.Format(
@"WITH RECURSIVE dates(date) AS (
  VALUES('{0}')
  UNION ALL
  SELECT date(date, '+1 day')
  FROM dates
  WHERE date < DATE('now')
)
SELECT rd.date, dd.snowDepth, dd.snow24h FROM dates rd
LEFT JOIN DiaryData dd ON date(dd.Date) = rd.date
ORDER BY rd.date ASC;", earliest[0].Date.ToString("yyyy-MM-dd"));

				var data = cumulus.DiaryDB.Query<DiaryData>(query);

				if (data.Count > 0)
				{
					for (var i = 0; i < data.Count; i++)
					{
						var recDate = data[i].Date.ToUnixTimeMs();

						if (cumulus.GraphOptions.Visible.SnowDepth.IsVisible(local))
						{
							// snow depth
							snowdepth.Append($"[{recDate},{(data[i].SnowDepth.HasValue ? data[i].SnowDepth.Value.ToString("F1", InvC) : "null")}],");
						}

						if (cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
						{
							// snowfall 24h
							snow24h.Append($"[{recDate},{(data[i].Snow24h.HasValue ? data[i].Snow24h.Value.ToString("F1", InvC) : "null")}],");
						}
					}
				}
			}

			if (cumulus.GraphOptions.Visible.SnowDepth.IsVisible(local))
			{
				if (snowdepth[^1] == ',')
					snowdepth.Length--;
				sb.Append("\"SnowDepth\":" + snowdepth.ToString() + "]");
			}

			if (cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.SnowDepth.IsVisible(local))
					sb.Append(',');

				if (snow24h[^1] == ',')
					snow24h.Length--;
				sb.Append("\"Snow24h\":" + snow24h.ToString() + "]");

			}

			sb.Append('}');

			return sb.ToString();
		}


		internal string GetCurrentData()
		{
			// no need to use multiplier as rose is all relative
			var windRoseData = new StringBuilder(windcounts[0].ToString(cumulus.WindFormat, CultureInfo.InvariantCulture), 4096);
			lock (windRoseData)
			{
				for (var i = 1; i < cumulus.NumWindRosePoints; i++)
				{
					windRoseData.Append(',');
					windRoseData.Append(windcounts[i].ToString(cumulus.WindFormat, CultureInfo.InvariantCulture));
				}
			}
			var stormRainStart = StartOfStorm == DateTime.MinValue ? "-----" : StartOfStorm.ToString("d");

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
			if (cumulus.FirmwareAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.FirmwareAlarm.Id, cumulus.FirmwareAlarm.Triggered));
			if (cumulus.ErrorAlarm.Enabled)
				alarms.Add(new DashboardAlarms(cumulus.ErrorAlarm.Id, cumulus.ErrorAlarm.Triggered));

			for (var i = 0; i < cumulus.UserAlarms.Count; i++)
			{
				if (cumulus.UserAlarms[i].Enabled)
					alarms.Add(new DashboardAlarms(cumulus.UserAlarms[i].Id, cumulus.UserAlarms[i].Triggered));
			}


			var data = new DataStruct(cumulus, MetData.Temperature, MetData.Humidity, MetData.TempTotalToday / tempsamplestoday, MetData.TemperatureIn, MetData.Dewpoint, MetData.WindChill, MetData.HumidityIn,
				MetData.Pressure, MetData.WindLatest, MetData.WindAverage, MetData.RecentMaxGust, MetData.WindRunToday, MetData.Bearing, MetData.AvgBearing, MetData.RainToday, MetData.RainYesterday, MetData.RainWeek, MetData.RainMonth, MetData.RainYear, MetData.RainRate,
				MetData.RainLastHour, MetData.HeatIndex, MetData.Humidex, MetData.ApparentTemperature, temptrendval, presstrendval, DailyHighLow.Today.HighGust, DailyHighLow.Today.HighGustTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.HighWind,
				DailyHighLow.Today.HighGustBearing, cumulus.Units.WindText, cumulus.Units.WindRunText, MetData.BearingRangeFrom10, MetData.BearingRangeTo10, windRoseData.ToString(), DailyHighLow.Today.HighTemp, DailyHighLow.Today.LowTemp,
				DailyHighLow.Today.HighTempTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.LowTempTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.HighPress, DailyHighLow.Today.LowPress, DailyHighLow.Today.HighPressTime.ToString(cumulus.ProgramOptions.TimeFormat),
				DailyHighLow.Today.LowPressTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.HighRainRate, DailyHighLow.Today.HighRainRateTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.HighHumidity, DailyHighLow.Today.LowHumidity,
				DailyHighLow.Today.HighHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.LowHumidityTime.ToString(cumulus.ProgramOptions.TimeFormat), cumulus.Units.PressText, cumulus.Units.TempText, cumulus.Units.RainText,
				DailyHighLow.Today.HighDewPoint, DailyHighLow.Today.LowDewPoint, DailyHighLow.Today.HighDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.LowDewPointTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.LowWindChill,
				DailyHighLow.Today.LowWindChillTime.ToString(cumulus.ProgramOptions.TimeFormat), MetData.SolarRad, DailyHighLow.Today.HighSolar, DailyHighLow.Today.HighSolarTime.ToString(cumulus.ProgramOptions.TimeFormat), MetData.UV, DailyHighLow.Today.HighUv,
				DailyHighLow.Today.HighUvTime.ToString(cumulus.ProgramOptions.TimeFormat), MetData.ForecastStr, getTimeString(cumulus.SunRiseTime, cumulus.ProgramOptions.TimeFormat), getTimeString(cumulus.SunSetTime, cumulus.ProgramOptions.TimeFormat),
				getTimeString(cumulus.MoonRiseTime, cumulus.ProgramOptions.TimeFormat), getTimeString(cumulus.MoonSetTime, cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.HighHeatIndex, DailyHighLow.Today.HighHeatIndexTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.HighAppTemp,
				DailyHighLow.Today.LowAppTemp, DailyHighLow.Today.HighAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.LowAppTempTime.ToString(cumulus.ProgramOptions.TimeFormat), MetData.CurrentSolarMax,
				Records.AllTime.HighPress.Val, Records.AllTime.LowPress.Val, MetData.SunshineHours, CompassPoint(MetData.DominantWindBearing), LastRainTip,
				DailyHighLow.Today.HighHourlyRain, DailyHighLow.Today.HighHourlyRainTime.ToString(cumulus.ProgramOptions.TimeFormat), "F" + Cumulus.Beaufort(DailyHighLow.Today.HighWind), "F" + Cumulus.Beaufort(MetData.WindAverage),
				cumulus.BeaufortDesc(MetData.WindAverage), LastDataReadTimestamp, DataStopped, StormRain, stormRainStart, CloudBase, cumulus.CloudBaseInFeet ? "ft" : "m", RainLast24Hour,
				MetData.FeelsLike, DailyHighLow.Today.HighFeelsLike, DailyHighLow.Today.HighFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat), DailyHighLow.Today.LowFeelsLike, DailyHighLow.Today.LowFeelsLikeTime.ToString(cumulus.ProgramOptions.TimeFormat),
				DailyHighLow.Today.HighHumidex, DailyHighLow.Today.HighHumidexTime.ToString(cumulus.ProgramOptions.TimeFormat), alarms);

			try
			{
				return JsonSerializer.Serialize(data);
			}
			catch (Exception ex)
			{
				Cumulus.LogConsoleMessage(ex.Message);
				return "";
			}

		}

		// Returns true if the gust value exceeds current RecentMaxGust, false if it fails
		public bool CheckHighGust(double gust, int gustdir, DateTime timestamp)
		{
			if (gust >= cumulus.Limit.WindHigh)
			{
				cumulus.LogSpikeRemoval("Wind Gust greater than the limit; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={gust.ToString(cumulus.WindFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind Gust greater than limit - NewVal={gust.ToString(cumulus.WindFormat)}, OldVal={cumulus.Limit.WindHigh.ToString(cumulus.WindFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return false;
			}

			if (gust > DailyHighLow.Today.HighGust)
			{
				DailyHighLow.Today.HighGust = gust;
				DailyHighLow.Today.HighGustTime = timestamp;
				DailyHighLow.Today.HighGustBearing = gustdir;
				WriteTodayFile(timestamp, false);
			}
			if (gust > Records.ThisMonth.HighGust.Val)
			{
				Records.ThisMonth.HighGust.Val = gust;
				Records.ThisMonth.HighGust.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (gust > Records.ThisYear.HighGust.Val)
			{
				Records.ThisYear.HighGust.Val = gust;
				Records.ThisYear.HighGust.Ts = timestamp;
				WriteYearIniFile();
			}
			// All time high gust?
			if (gust > Records.AllTime.HighGust.Val)
			{
				SetAlltime(Records.AllTime.HighGust, gust, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighGust", gust, true, timestamp);

			cumulus.HighGustAlarm.CheckAlarm(gust);

			return gust > MetData.RecentMaxGust;
		}


		public void CheckHighAvgSpeed(DateTime timestamp)
		{
			if (MetData.WindAverage > DailyHighLow.Today.HighWind)
			{
				DailyHighLow.Today.HighWind = MetData.WindAverage;
				DailyHighLow.Today.HighWindTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.WindAverage > Records.ThisMonth.HighWind.Val)
			{
				Records.ThisMonth.HighWind.Val = MetData.WindAverage;
				Records.ThisMonth.HighWind.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.WindAverage > Records.ThisYear.HighWind.Val)
			{
				Records.ThisYear.HighWind.Val = MetData.WindAverage;
				Records.ThisYear.HighWind.Ts = timestamp;
				WriteYearIniFile();
			}

			// All time high wind speed?
			if (MetData.WindAverage > Records.AllTime.HighWind.Val)
			{
				SetAlltime(Records.AllTime.HighWind, MetData.WindAverage, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighWind", MetData.WindAverage, true, timestamp);

			cumulus.HighWindAlarm.CheckAlarm(MetData.WindAverage);
		}


		public double GetAverageByMonth<T>(int mon, Func<DayFileRec, T> selector) where T : struct
		{
			try
			{
				// Determine the first and last full months
				var firstDate = DayFile[0].Date;
				var lastDate = DayFile[^1].Date;
				var firstFullMonth = firstDate.Day == 1 ? firstDate : new DateTime(firstDate.Year, firstDate.Month, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1);
				var lastFullMonth = new DateTime(lastDate.Year, lastDate.Month, 1, 0, 0, 0, DateTimeKind.Local).AddDays(-1);

				if (lastFullMonth > firstFullMonth)
				{
					// Filter data to include only complete months and calculate the average
					var avg = DayFile
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

		public double GetAverageTotalByMonth<T>(int mon, Func<DayFileRec, T> selector) where T : struct
		{
			try
			{
				// Determine the first and last full months
				var firstDate = DayFile[0].Date;
				var lastDate = DayFile[^1].Date;
				var firstFullMonth = firstDate.Day == 1 ? firstDate : new DateTime(firstDate.Year, firstDate.Month, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1);
				var lastFullMonth = new DateTime(lastDate.Year, lastDate.Month, 1, 0, 0, 0, DateTimeKind.Local).AddDays(-1);

				if (lastFullMonth > firstFullMonth)
				{
					// Filter data to include only complete months
					var avgPerMonth = DayFile
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
				var firstDate = DayFile[0].Date;
				var lastDate = DayFile[^1].Date;
				var firstFullMonth = firstDate.Day == 1 ? firstDate : new DateTime(firstDate.Year, firstDate.Month, 1, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1);
				var lastFullMonth = new DateTime(lastDate.Year, lastDate.Month, 1, 0, 0, 0, DateTimeKind.Local).AddDays(-1);
				if (lastFullMonth > firstFullMonth)
				{
					var monthlyDiffs = DayFile
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
								var previousMonthData = DayFile.Where(d => d.Date < firstDayOfMonth);
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
				days = DayFile.Count(d => d.Date >= start && d.Date < end && d.TotalRain < thresh);
			}
			else
			{
				days = DayFile.Count(d => d.Date >= start && d.Date < end && d.TotalRain >= thresh);
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

			var rainDays = DayFile
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

			var dryDays = DayFile
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
		public double Gust { get; set; }  // calibrated "gust" as read from station
		public double Speed { get; set; } // calibrated "speed" as read from station
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
