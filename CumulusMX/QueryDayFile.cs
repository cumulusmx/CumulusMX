using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

using EmbedIO;

using Org.BouncyCastle.Ocsp;

using ServiceStack.Text;

using SQLite;

namespace CumulusMX
{

	internal class QueryDayFile(SQLiteConnection databaseConnection)
	{
		private readonly SQLiteConnection db = databaseConnection;
		private readonly static CultureInfo inv = CultureInfo.InvariantCulture;
		internal static readonly string[] funcNameArray = { "min", "max", "sum", "avg", "count" };
		private static readonly Dictionary<string, string> properties = new()
		{
			{"HighGust", "HighGustTime"},
			{"WindRun", "Date"},
			{"HighAvgWind", "HighAvgWindTime"},
			{"LowTemp", "LowTempTime"},
			{"HighTemp", "HighTempTime"},
			{"AvgTemp", "Date"},
			{"HighHeatIndex", "HighHeatIndexTime"},
			{"HighAppTemp", "HighAppTempTime"},
			{"LowAppTemp", "LowAppTempTime"},
			{"LowWindChill", "LowWindChillTime"},
			{"HighDewPoint", "HighDewPointTime"},
			{"LowDewPoint", "LowDewPoint"},
			{"HighFeelsLike", "HighFeelsLikeTime"},
			{"LowFeelsLike", "LowFeelsLikeTime"},
			{"HighHumidex", "HighHumidexTime"},
			{"LowPress", "LowPressTime"},
			{"HighPress", "HighPressTime"},
			{"HighRainRate", "HighRainRateTime"},
			{"TotalRain", "Date"},
			{"HighHourlyRain", "HighHourlyRainTime"},
			{"HighRain24h", "HighRain24hTime"},
			{"LowHumidity", "LowHumidityTime"},
			{"HighHumidity", "HighHumidityTime"},
			{"SunShineHours", "Date"},
			{"HighSolar", "HighSolarTime"},
			{"HighUv", "HighUvTime"},
			{"ET", "Date"},
			{"HeatingDegreeDays", "Date"},
			{"CoolingDegreeDays", "Date"},
			{"ChillHours", "Date"}
		};
		private static readonly int[] daysInMonth = { 0, 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };


		public (double value, DateTime time) DayFile(string propertyName, string function, string where, string from, string to, string resfunc)
		{
			var fromDate = DateTime.MinValue;
			var toDate = DateTime.MinValue;
			var byDay = string.Empty;
			var byMonth = string.Empty;
			var yearly = false;

			if ((from != "ThisDay" && from[0..3] != "Day") && !funcNameArray.Contains(function))
			{
				throw new ArgumentException($"Invalid function name - '{function}'");
			}

			if (!properties.TryGetValue(propertyName, out string timeProp))
			{
				throw new ArgumentException($"Invalid property name - '{propertyName}'");
			}

			if (function == "sum")
			{
				timeProp = "Date";
			}

			try
			{
				switch (from)
				{
					case "ThisYear":
						fromDate = new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);
						toDate = DateTime.Now;
						break;

					case "ThisMonth":
						fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Local);
						toDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month), 0, 0, 0, DateTimeKind.Local);
						break;

					case "ThisDay":
						byDay = DateTime.Now.ToString("MM-dd");
						break;

					case string s when s.StartsWith("Day-"):
						var rel = int.Parse(s.Split('-')[1]);
						byDay = DateTime.Now.AddDays(-rel).ToString("MM-dd");
						break;

					case string s when s.StartsWith("Month-"):
						rel = int.Parse(s.Split('-')[1]);
						fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(-rel);
						toDate = fromDate.AddMonths(1).AddDays(-1);
						break;

					case string s when s.StartsWith("Year-"):
						rel = int.Parse(s.Split('-')[1]);
						fromDate = new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local).AddYears(-rel);
						toDate = fromDate.AddYears(1).AddDays(-1);
						break;

					case string s when s.StartsWith("Day"):
						// by specified day
						if (s.Length != 7)
						{
							throw new ArgumentException(s + " invalid length");
						}
						var mon = s[3..5];
						var day = s[5..];
						if (int.Parse(mon) > 12 || int.Parse(mon) < 1)
						{
							throw new ArgumentException(s + " exceeds month range 1-12!");
						}
						if (int.Parse(day) < 1 || int.Parse(day) > daysInMonth[int.Parse(mon)])
						{
							throw new ArgumentException($"{s} exceeds day range 1-{daysInMonth[int.Parse(mon)]}");
						}

						byDay = $"{mon}-{day}";
						break;

					case string s when s.Length > 5 && s.StartsWith("Month"):
						// by month
						byMonth = ("0" + s[5..])[^2..];

						if (int.Parse(byMonth) > 12 || int.Parse(byMonth) < 1)
						{
							throw new ArgumentException(s + " exceeds month range 1-12!");
						}
						break;

					case "Yearly":
						yearly = true;
						break;

					default:
						// assume a date range
						if (DateTime.TryParseExact(from, "yyyy-MM-dd", inv, DateTimeStyles.AssumeLocal, out fromDate))
						{
							if (!DateTime.TryParseExact(to, "yyyy-MM-dd", inv, DateTimeStyles.AssumeLocal, out toDate))
							{
								throw new ArgumentException($"Invalid toDate format - '{to}'");
							}
						}
						else
						{
							throw new ArgumentException($"Invalid fromDate format - '{from}'");
						}
						break;
				}
			}
			catch (Exception ex)
			{
				throw new ArgumentException("Error parsing from/to dates: " + ex.Message);
			}

			Program.cumulus.LogDebugMessage($"QueryDayFile: resFunc={resfunc}, prop={propertyName}, func={function}, where={where}, from={from}, to={to}, start={fromDate:yyyy-MM-dd}, end={toDate:yyyy-MM-dd}");


			DateTime logTime = DateTime.MinValue;
			double value = -9999;

			// Combinations:-
			// count:
			//    where [mandatory]
			//    date range
			//    monthly - this requires the addition of the count function to return the highest or lowest count

			if (function == "count")
			{
				if (byMonth == string.Empty && !yearly)
				{
					value = db.ExecuteScalar<int>($"SELECT COUNT({propertyName}) FROM DayFileRec WHERE {propertyName} {where} AND Date BETWEEN ? AND ?", fromDate, toDate);
					logTime = fromDate;
				}
				else
				{
					var sort = resfunc == "max" ? "DESC" : "ASC";

					if (yearly)
					{
						var ret = db.Query<RetValString>($"SELECT value, year FROM (SELECT strftime('%Y', Date) AS year, COUNT(*) AS value FROM DayFileRec WHERE {propertyName} {where} GROUP BY year) AS grouped_data ORDER BY value {sort} LIMIT 1");

						if (ret.Count == 1)
						{
							value = ret[0].value;
							logTime = new DateTime(int.Parse(ret[0].groupBy), 1, 1, 0, 0, 0, DateTimeKind.Local);
						}
					}
					else
					{
						var ret = db.Query<RetValString>($"SELECT value, year_month FROM (SELECT strftime('%Y-%m', Date) AS year_month, COUNT(*) AS value FROM DayFileRec WHERE {propertyName} {where} AND strftime('%m', Date) = '{byMonth}' GROUP BY year_month) AS grouped_data ORDER BY value {sort} LIMIT 1");

						if (ret.Count == 1)
						{
							value = ret[0].value;
							var arr = ret[0].groupBy.Split("-");
							logTime = new DateTime(int.Parse(arr[0]), int.Parse(arr[1]), 1, 0, 0, 0, DateTimeKind.Local);
						}
					}
				}
			}
			else
			{
				if (byDay == string.Empty && byMonth == string.Empty && !yearly)
				{
					List<RetValTime> ret;

					if (function == "avg" || function == "sum")
					{
						ret = db.Query<RetValTime>($"SELECT {function}({propertyName}) value, {timeProp} time FROM DayFileRec WHERE Date BETWEEN ? AND ?", fromDate, toDate);
					}
					else
					{
						ret = db.Query<RetValTime>($"SELECT {propertyName} value, {timeProp} time FROM DayFileRec WHERE {propertyName} = (SELECT {function}({propertyName}) FROM DayFileRec WHERE Date BETWEEN ? AND ?) AND Date BETWEEN ? AND ? LIMIT 1", fromDate, toDate, fromDate, toDate);
					}
					
					if (ret.Count == 1)
					{
						value = ret[0].value;
						logTime = ret[0].time;
					}
				}
				else
				{
					var sort = resfunc == "max" ? "DESC" : "ASC";

					if (byDay != string.Empty)
					{
						var ret = db.Query<RetValTime>($"SELECT {timeProp} AS time, {propertyName} AS value FROM DayFileRec WHERE {(string.IsNullOrEmpty(where) ? string.Empty : $"{propertyName} {where} AND")} strftime(\"%m-%d\", Date) = \"{byDay}\" ORDER BY value {sort} LIMIT 1");
						if (ret.Count == 1)
						{
							value = ret[0].value;
							logTime = ret[0].time;
						}
					}
					else if (yearly)
					{
						var ret = db.Query<RetValTime>($"SELECT {function}({propertyName}) value, {timeProp} time, strftime('%Y', Date) year FROM DayFileRec GROUP BY year ORDER BY value {sort} LIMIT 1");
						
						if (ret.Count == 1)
						{
							value = ret[0].value;
							logTime = ret[0].time;
						}
					}
					else
					{
						if (function == "sum" || function == "avg")
						{
							var ret = db.Query<RetValString>($"SELECT {function}({propertyName}) value, strftime('%Y-%m', Date) year_month FROM DayFileRec WHERE strftime('%m', Date) = '{byMonth}' GROUP BY year_month ORDER BY value {sort} LIMIT 1");
							
							if (ret.Count == 1)
							{
								value = ret[0].value;
								var arr = ret[0].groupBy.Split("-");
								logTime = new DateTime(int.Parse(arr[0]), int.Parse(arr[1]), 1, 0, 0, 0, DateTimeKind.Local);
							}
						}
						else
						{
							var ret = db.Query<RetValTime>($"SELECT {function}({propertyName}) value, {timeProp} time FROM DayFileRec WHERE strftime('%m', Date) = '{byMonth}' GROUP BY strftime('%Y-%m', Date) ORDER BY value {sort} LIMIT 1");

							if (ret.Count == 1)
							{
								value = ret[0].value;
								logTime = ret[0].time;
							}
						}
					}
				}
			}

			var valStr = value.ToString("0.000", inv);
			Program.cumulus.LogDebugMessage($"QueryDayFile: Result = [{valStr}, {logTime:g}]");

			return (value, logTime);
		}


		public string WebQuery(IHttpContext context)
		{
			var errorMsg = string.Empty;
			context.Response.StatusCode = 200;
			// get the response
			try
			{
				Program.cumulus.LogMessage("API Querying day file...");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json=" prefix
				var json = WebUtility.UrlDecode(data)[5..];

				// de-serialize it
				var req = JsonSerializer.DeserializeFromString<WebReq>(json);

				// process the settings
				try
				{
					var from = req.startsel == "User" ? req.start : req.startsel;
					var format = "g";

					var (value, time) = DayFile(req.dataname, req.function, req.where, from, req.end, req.countfunction);

					Program.cumulus.LogMessage("API Querying day file complete");

					if (value < -9998)
					{
						return "{\"value\": \"n/a\", \"time\": \"n/a\"}";
					}

					var valStr = value.ToString("0.000", inv);

					if (time == DateTime.MinValue)
					{
						return $"{{\"value\": {valStr}, \"time\":\"n/a\"}}";
					}

					return $"{{\"value\": {valStr}, \"time\":\"{time.ToString(format)}\"}}";
				}
				catch (Exception ex)
				{
					var msg = "Error day file query Options: " + ex.Message;
					Program.cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}
			}
			catch (Exception ex)
			{
				Program.cumulus.LogErrorMessage("Query day file error: " + ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}


		private sealed class RetValTime
		{
			public double value { get; set; }
			public DateTime time { get; set; }
		}

		private sealed class RetValString
		{
			public int value { get; set; }
			public string groupBy { get; set; }
		}

		private sealed class WebReq
		{
			public string dataname { get; set; }
			public string function { get; set; }
			public string where { get; set; }
			public string startsel { get; set; }
			public string start { get; set; }
			public string end { get; set; }
			public string countfunction { get; set; }
		}
	}
}

