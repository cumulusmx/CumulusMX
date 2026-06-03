using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
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


			var data = new DataStruct(cumulus, windRoseData.ToString(), alarms);

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

		public static string GetTimeString(DateTime time, string format = "HH:mm")
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

		public static string GetTimeString(TimeSpan timespan, string format = "HH:mm")
		{
			try
			{
				if (timespan.TotalSeconds < 0)
				{
					return "-----";
				}

				var dt = DateTime.MinValue.Add(timespan);

				return GetTimeString(dt, format);
			}
			catch (Exception e)
			{
				Program.cumulus.LogMessage($"getTimeString: Exception caught - {e.Message}");
				return "-----";
			}
		}
	}
}
