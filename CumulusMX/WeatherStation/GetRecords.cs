using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		private static string alltimejsonformat(Record item, string unit, string valueformat, string dateformat)
		{
			return $"[\"{item.Desc}\",\"{item.GetValString(valueformat)} {unit}\",\"{item.GetTsString(dateformat)}\"]";
		}

		public string GetTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 2048);

			json.Append(alltimejsonformat(Records.AllTime.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighHumidex, "&nbsp;", cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D") + ",");
			json.Append(alltimejsonformat(Records.AllTime.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighBgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f") + ",");
			json.Append(alltimejsonformat(Records.AllTime.HighWbgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(Records.AllTime.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(Records.AllTime.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(Records.AllTime.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(alltimejsonformat(Records.AllTime.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(alltimejsonformat(Records.AllTime.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		private static string monthlyjsonformat(Record item, string unit, string valueformat, string dateformat)
		{
			return $"[\"{item.Desc}\",\"{item.GetValString(valueformat)} {unit}\",\"{item.GetTsString(dateformat)}\"]";
		}

		public string GetMonthlyTempRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighBgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighWbgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyHumRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyPressRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyWindRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetMonthlyRainRecords(int month)
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(',');
			var dry = GetByMonthDryDays(month);
			string val, date;
			if (dry.Item1 == default)
			{
				val = date = "-";
			}
			else
			{
				date = dry.Item1.ToString("Y");
				val = dry.Item2.ToString();
			}
			json.Append($"[\"Most dry days\",\"{val} days\",\"{date}\"]");
			json.Append(',');
			dry = GetByMonthRainDays(month);
			if (dry.Item1 == default)
			{
				val = date = "-";
			}
			else
			{
				date = dry.Item1.ToString("Y");
				val = dry.Item2.ToString();
			}
			json.Append($"[\"Most rain days\",\"{val} days\",\"{date}\"]");
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(monthlyjsonformat(Records.MonthlyRecs[month].LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		private static string monthyearjsonformat(Record value, string unit, string valueformat, string dateformat)
		{
			return $"[\"{value.Desc}\",\"{value.GetValString(valueformat)} {unit}\",\"{value.GetTsString(dateformat)}\"]";
		}

		public string GetThisMonthTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(Records.ThisMonth.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighBgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighWbgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));

			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(Records.ThisMonth.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(Records.ThisMonth.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(Records.ThisMonth.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisMonthRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(Records.ThisMonth.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			var dry = GetMonthDryRainDays(DateTime.Now, true);
			json.Append($"[\"Dry days\",\"{dry} days\",\"\"]");
			json.Append(',');
			var wet = GetMonthDryRainDays(DateTime.Now, false);
			json.Append($"[\"Rain days\",\"{wet} days\",\"\"]");
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisMonth.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearTempRecords()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			json.Append(monthyearjsonformat(Records.ThisYear.HighTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowDewPoint, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowAppTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowFeelsLike, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighHumidex, "&nbsp;", cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowChill, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighHeatIndex, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighMinTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowMaxTemp, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowDailyTempRange, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighBgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighWbgt, "&deg;" + cumulus.Units.TempText[1].ToString(), cumulus.TempFormat, "f"));

			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearHumRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(Records.ThisYear.HighHumidity, "%", cumulus.HumFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowHumidity, "%", cumulus.HumFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearPressRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(Records.ThisYear.HighPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LowPress, cumulus.Units.PressText, cumulus.PressFormat, "f"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearWindRecords()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append(monthyearjsonformat(Records.ThisYear.HighGust, cumulus.Units.WindText, cumulus.WindFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighWind, cumulus.Units.WindText, cumulus.WindAvgFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighWindRun, cumulus.Units.WindRunText, cumulus.WindRunFormat, "D"));
			json.Append("]}");
			return json.ToString();
		}

		public string GetThisYearRainRecords()
		{
			var json = new StringBuilder("{\"data\":[", 512);

			json.Append(monthyearjsonformat(Records.ThisYear.HighRainRate, cumulus.Units.RainText + "/hr", cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HourlyRain, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.DailyRain, cumulus.Units.RainText, cumulus.RainFormat, "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.HighRain24Hours, cumulus.Units.RainText, cumulus.RainFormat, "f"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.MonthlyRain, cumulus.Units.RainText, cumulus.RainFormat, "Y"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LongestDryPeriod, "days", "f0", "D"));
			json.Append(',');
			json.Append(monthyearjsonformat(Records.ThisYear.LongestWetPeriod, "days", "f0", "D"));
			json.Append("]}");
			return json.ToString();
		}
	}
}
