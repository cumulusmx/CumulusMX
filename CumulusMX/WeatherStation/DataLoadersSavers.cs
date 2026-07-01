using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CumulusMX.LogFiles;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
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

							WindVec[nextwindvec].X = rec.WindGust * Math.Sin(Trig.DegToRad(rec.WindDir));
							WindVec[nextwindvec].Y = rec.WindGust * Math.Cos(Trig.DegToRad(rec.WindDir));
							WindVec[nextwindvec].Timestamp = rec.DateTime;
							WindVec[nextwindvec].Bearing = MetData.WindBearing; // savedBearing
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

		public void AddRecentDataEntry(DateTime timestamp, double? pm2p5, double? pm10)
		{
			try
			{
				RecentDataDb.InsertOrReplace(new RecentData()
				{
					DateTime = timestamp,
					DewPoint = MetData.Dewpoint,
					HeatIndex = MetData.HeatIndex,
					Humidity = MetData.Humidity,
					OutsideTemp = MetData.Temperature,
					Pressure = MetData.Pressure,
					RainToday = MetData.RainToday,
					SolarRad = MetData.SolarRad,
					UV = MetData.UV,
					WindAvgDir = MetData.WindAvgBearing,
					WindGust = MetData.RecentMaxGust,
					WindLatest = MetData.WindLatest,
					WindChill = MetData.WindChill,
					WindDir = MetData.WindBearing,
					WindSpeed = MetData.WindAverage,
					raincounter = MetData.RainCounter,
					FeelsLike = MetData.FeelsLike,
					Humidex = MetData.Humidex,
					AppTemp = MetData.ApparentTemperature,
					IndoorTemp = MetData.TemperatureIn,
					IndoorHumidity = MetData.HumidityIn,
					SolarMax = MetData.CurrentSolarMax,
					RainRate = MetData.RainRate,
					Pm2p5 = pm2p5,
					Pm10 = pm10,
					BGT = MetData.BlackGlobeTemp,
					WBGT = MetData.WetBulbGlobeTemp
				});
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "AddRecentDataEntry: Error");
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

		private async Task DoDayfile(DateTime timestamp)
		{
			// Writes an entry to the daily extreme log file. Fields are comma-separated.
			// 0   Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  Highest wind gust
			// 2  WindBearing of highest wind gust
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

			double AvgTemp = MetData.AverageTemp;

			// save the value for yesterday
			MetData.YestAvgTemp = AvgTemp;

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

			MetData.DayFile.Add(newRec);

			// add to SQLite
			RecentDataDb.Insert(newRec);

			if (cumulus.MySqlFuncs.MySqlSettings.Dayfile.Enabled)
			{
				var queryString = new StringBuilder(cumulus.DayfileTable.StartOfInsert, 1024);
				queryString.Append(" Values(");
				queryString.Append(timestamp.AddDays(-1).ToString("\\'yyyy-MM-dd\\'", inv));
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
				queryString.Append(sep + "'" + cumulus.CompassPoint(DailyHighLow.Today.HighGustBearing) + "'");
				queryString.Append(sep + "'" + cumulus.CompassPoint(MetData.DominantWindBearing) + "'");
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
					MetData.DayFile.Clear();
					RecentDataDb.Execute("DELETE FROM DayFileRec");

					var lines = File.ReadAllLines(cumulus.DayFileName);

					foreach (var line in lines)
					{
						try
						{
							// process each record in the file
							linenum++;
							var newRec = new DayFileRec(line);

							if (MetData.DayFile.Exists(x => x.Date == newRec.Date))
							{
								cumulus.LogErrorMessage($"ERROR: Duplicate entry in dayfile for {newRec.Date:d}");
								msg.Append($"ERROR: Duplicate entry in dayfile for {newRec.Date:d}<br>");
								duplicateCount++;
							}

							MetData.DayFile.Add(newRec);

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
							addedToDb = RecentDataDb.InsertAll(MetData.DayFile, true);
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
	}
}
