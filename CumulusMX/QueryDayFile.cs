using System;
using System.Globalization;
using System.Linq;

using SQLite;


namespace CumulusMX
{

	internal class QueryDayFile(SQLiteConnection databaseConnection)
	{
		private readonly SQLiteConnection db = databaseConnection;
		private readonly static CultureInfo inv = CultureInfo.InvariantCulture;
		internal static readonly string[] funcNameArray = { "min", "max", "sum", "avg", "count" };


		public (double value, DateTime time) DayFile(string propertyName, string function, string where, string from, string to)
		{
			var fromDate = DateTime.MinValue;
			var toDate = DateTime.MinValue;
			var byMonth = string.Empty;

			if (!funcNameArray.Contains(function))
			{
				throw new ArgumentException($"Invalid function name - '{to}'");
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
						toDate = fromDate.AddMonths(1);
						break;

					case string s when s.StartsWith("Month-"):
						var rel = int.Parse(s.Split('-')[1]);
						fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(-rel);
						toDate = fromDate.AddMonths(1);
						break;

					case string s when s.StartsWith("Year-"):
						rel = int.Parse(s.Split('-')[1]);
						fromDate = new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local).AddYears(-rel);
						toDate = fromDate.AddYears(1);
						break;

					case string s when s.Length > 5 && s.StartsWith("Month"):
						// by month
						byMonth = ("0" + s[5..])[^2..];

						if (int.Parse(byMonth) > 12 || int.Parse(byMonth) < 1)
						{
							throw new ArgumentException(s + " exceeds month range 1-12!");
						}
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

			DateTime logTime = DateTime.MinValue;
			double value = -9999;

			var timeProp = propToTime(propertyName);

			if (function == "count")
			{
				if (byMonth == string.Empty)
				{
					value = db.ExecuteScalar<int>($"SELECT COUNT({propertyName}) FROM DayFileRec WHERE {propertyName} {where} AND Date >= ? AND Date < ?", fromDate, toDate);
				}
				else
				{
					var ret = db.Query<retValString>($"SELECT value, year_month FROM (SELECT strftime('%Y-%m', Date) AS year_month, COUNT(*) AS value FROM DayFileRec WHERE {propertyName} {where} AND strftime('%m', Date) = '{byMonth}' GROUP BY year_month) AS grouped_data ORDER BY value DESC LIMIT 1");
					if (ret.Count == 1)
					{
						value = ret[0].value;
						var arr = ret[0].year_month.Split("-");
						logTime = new DateTime(int.Parse(arr[0]), int.Parse(arr[1]), 1, 0, 0, 0, DateTimeKind.Local);
					}
				}
			}
			else
			{
				if (byMonth == string.Empty)
				{
					var ret = db.Query<retValTime>($"SELECT {propertyName} value, {timeProp} time FROM DayFileRec WHERE {propertyName} = (SELECT {function}({propertyName}) FROM DayFileRec WHERE Date >= ? AND Date < ?) AND Date >= ? AND Date < ?", fromDate, toDate, fromDate, toDate);
					if (ret.Count == 1)
					{
						value = ret[0].value;
						logTime = ret[0].time;
					}
				}
				else
				{
					//value = db.ExecuteScalar<double>($"SELECT {function}({propertyName}) FROM DayFileRec WHERE strftime('%m', Date) = '?' ORDER BY Date", byMonth);
					var sort = function == "min" ? "ASC" : "DESC";
					//var ret = db.Query<retValTime>($"SELECT {propertyName} value, {timeProp} time FROM DayFileRec WHERE {propertyName} = (SELECT {function}({propertyName}) FROM DayFileRec WHERE strftime('%m', Date) = '{byMonth}') AS grouped_data ORDER BY Date LIMIT 1");

					var ret = db.Query<retValTime>($"SELECT {propertyName} value, {timeProp} time FROM DayFileRec WHERE {propertyName} = (SELECT {function}({propertyName}) FROM DayFileRec WHERE strftime('%m', Date) = '{byMonth}') AND strftime('%m', Date) = '{byMonth}'"); 

					if (ret.Count == 1)
					{
						value = ret[0].value;
						logTime = ret[0].time;
					}
				}
			}

			return (value, logTime);
		}

		private static string propToTime(string prop)
		{
			return prop switch
			{
				"HighGust" => "HighGustTime",
				"HighGustBearing" => "Date",
				"WindRun" => "Date",
				"HighAvgWind" => "HighAvgWindTime",
				"DominantWindBearing" => "Date",

				"LowTemp" => "LowTempTime",
				"HighTemp" => "HighTempTime",
				"AvgTemp" => "Date",
				"HighHeatIndex" => "HighHeatIndexTime",
				"HighAppTemp" => "HighAppTempTime",
				"LowAppTemp" => "LowAppTempTime",
				"LowWindChill" => "LowWindChillTime",
				"HighDewPoint" => "HighDewPointTime",
				"LowDewPoint" => "LowDewPoint",
				"HighFeelsLike" => "HighFeelsLikeTime",
				"LowFeelsLike" => "LowFeelsLikeTime",
				"HighHumidex" => "HighHumidexTime",

				"LowPress" => "LowPressTime",
				"HighPress" => "HighPressTime",
				"HighRainRate" => "HighRainRateTime",
				"TotalRain" => "Date",
				"HighHourlyRain" => "HighHourlyRainTime",
				"HighRain24h" => "HighRain24hTime",

				"LowHumidity" => "LowHumidityTime",
				"HighHumidity" => "HighHumidityTime",

				"SunShineHours" => "Date",
				"HighSolar" => "HighSolarTime",
				"HighUv" => "HighUvTime",
				"ET" => "Date",

				"HeatingDegreeDays" => "Date",
				"CoolingDegreeDays" => "Date",
				"ChillHours" => "Date",

				_ => "Invalid"
			};
		}

		private class retValTime
		{
			public double value { get; set; }
			public DateTime time { get; set; }
		}

		private class retValString
		{
			public int value { get; set; }
			public string year_month { get; set; }
		}

	}
}

