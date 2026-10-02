using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CumulusMX
{
	internal class SimpleReports
	{
		public static string TemperatureReport(List<LogFiles.DayFileRec> dayfile, int decimals, bool txtFormat)
		{
			static double Field(LogFiles.DayFileRec d) => d.AvgTemp;
			static bool Valid(LogFiles.DayFileRec d) => d.AvgTemp >= -999;
			return Averages(dayfile, Field, decimals, Valid, txtFormat, "AVERAGE_TEMPERATURE");
		}

		public static string RainfallReport(List<LogFiles.DayFileRec> dayfile, int decimals, bool txtFormat)
		{
			static double Field(LogFiles.DayFileRec d) => d.TotalRain;
			static bool Valid(LogFiles.DayFileRec d) => d.TotalRain >= 0;
			return Totals(dayfile, Field, decimals, Valid, txtFormat, "RAINFALL");
		}

		public static string WindRunReport(List<LogFiles.DayFileRec> dayfile, int decimals, bool txtFormat)
		{
			static double Field(LogFiles.DayFileRec d) => d.WindRun;
			static bool Valid(LogFiles.DayFileRec d) => d.WindRun >= 0;
			return Totals(dayfile, Field, decimals, Valid, txtFormat, "WIND_RUN");
		}

		public static string SunShineReport(List<LogFiles.DayFileRec> dayfile, bool txtFormat)
		{
			static double Field(LogFiles.DayFileRec d) => d.SunShineHours;
			static bool Valid(LogFiles.DayFileRec d) => d.SunShineHours >= 0;
			return Totals(dayfile, Field, 1, Valid, txtFormat, "SUNSHINE_HOURS");
		}

		public static string ETReport(List<LogFiles.DayFileRec> dayfile, int decimals, bool txtFormat)
		{
			static double Field(LogFiles.DayFileRec d) => d.ET;
			static bool Valid(LogFiles.DayFileRec d) => d.ET >= 0;
			return Totals(dayfile, Field, decimals, Valid, txtFormat, "EVAPOTRANSPIRATION");
		}

		public static string WetDryDaysReport(List<LogFiles.DayFileRec> dayfile, double threshold, bool dry, bool txtFormat)
		{
			if (dry)
			{
				bool Valid(LogFiles.DayFileRec d) => d.TotalRain < threshold;
				return CountDryWet(dayfile, Valid, txtFormat, "DRY_DAYS");
			}
			else
			{
				bool Valid(LogFiles.DayFileRec d) => d.TotalRain >= threshold;
				return CountDryWet(dayfile, Valid, txtFormat, "WET_DAYS");
			}
		}


		private static string Totals(List<LogFiles.DayFileRec> dayfile, Func<LogFiles.DayFileRec, double> valueSelector, int decimals, Func<LogFiles.DayFileRec, bool> validSelector, bool txtFormat, string title)
		{
			// --- Aggregate per-year/per-month totals with missing-month detection ---

			var perYear =
				dayfile
					.GroupBy(d => d.Date.Year)
					.OrderBy(g => g.Key)
					.Select(g =>
					{
						var monthly = Enumerable.Range(1, 12)
							.Select(m =>
							{
								var recs = g.Where(d => d.Date.Month == m);
								return new ReportMonthly
								{
									HasData = recs.Any() && recs.All(validSelector),
									Value = recs.Sum(valueSelector)
								};
							})
							.ToArray();

						return new ReportSummary
						{
							Year = g.Key,
							Monthly = monthly,
							YearValue = g.Where(validSelector).Sum(valueSelector)
						};
					})
					.ToList();

			// --- Monthly averages across years (only years with data for that month) ---

			var monthlyAverages =
				Enumerable.Range(1, 12)
					.Select(m =>
					{
						var perMonthTotals =
							dayfile
								.GroupBy(d => d.Date.Year)
								.Select(g =>
								{
									var recs = g.Where(d => d.Date.Month == m);
									return recs.All(validSelector) ? recs.Sum(valueSelector) : (double?) null;
								})
								.Where(v => v.HasValue)
								.Select(v => v.Value)
								.ToList();

						return perMonthTotals.Count > 0
							? perMonthTotals.Average()
							: double.NaN;   // no data for this month in any year
					})
					.ToArray();


			// --- Annual average value across all years ---

			double avgAnnual =
				dayfile
					.GroupBy(d => d.Date.Year)
					.Average(g => g.Where(validSelector).Sum(valueSelector));

			if (txtFormat)
				return BuildReportText(perYear, decimals, monthlyAverages, avgAnnual, decimals, title);
			else
				return BuildReportHtml(perYear, decimals, monthlyAverages, avgAnnual, decimals, title);
		}

		private static string Averages(List<LogFiles.DayFileRec> dayfile, Func<LogFiles.DayFileRec, double> valueSelector, int decimals, Func<LogFiles.DayFileRec, bool> validSelector, bool txtFormat, string title)
		{
			// --- Aggregate per-year/per-month totals with missing-month detection ---

			var perYear =
				dayfile
					.GroupBy(d => d.Date.Year)
					.OrderBy(g => g.Key)
					.Select(g =>
					{
						var monthly = Enumerable.Range(1, 12)
							.Select(m =>
							{
								var recs = g.Where(d => d.Date.Month == m);
								return new ReportMonthly
								{
									HasData = recs.Any() && recs.All(validSelector),
									Value = recs.Any(validSelector) ? recs.Average(valueSelector) : double.NaN
								};
							})
							.ToArray();

						return new ReportSummary
						{
							Year = g.Key,
							Monthly = monthly,
							YearValue = g.Where(validSelector).Average(valueSelector)
						};
					})
					.ToList();

			// --- Monthly averages across years (only years with data for that month) ---

			var monthlyAverages =
				Enumerable.Range(1, 12)
					.Select(m =>
					{
						var perMonthTotals =
							dayfile
								.GroupBy(d => d.Date.Year)
								.Select(g =>
								{
									var recs = g.Where(d => d.Date.Month == m);
									return recs.Any(validSelector) ? recs.Average(valueSelector) : (double?) null;
								})
								.Where(v => v.HasValue)
								.Select(v => v.Value)
								.ToList();

						return perMonthTotals.Count > 0
							? perMonthTotals.Average()
							: double.NaN;   // no data for this month in any year
					})
					.ToArray();


			// --- Annual average across all years ---

			double avgAnnual =
				dayfile
					.GroupBy(d => d.Date.Year)
					.Average(g => g.Where(validSelector).Average(valueSelector));

			if (txtFormat)
				return BuildReportText(perYear, decimals, monthlyAverages, avgAnnual, decimals, title);
			else
				return BuildReportHtml(perYear, decimals, monthlyAverages, avgAnnual, decimals, title);
		}

		private static string CountDryWet(List<LogFiles.DayFileRec> dayfile, Func<LogFiles.DayFileRec, bool> validSelector, bool txtFormat, string title)
		{
			// --- Aggregate per-year/per-month totals with missing-month detection ---

			var perYear =
				dayfile
					.GroupBy(d => d.Date.Year)
					.OrderBy(g => g.Key)
					.Select(g =>
					{
						var monthly = Enumerable.Range(1, 12)
							.Select(m =>
							{
								var recs = g.Where(d => d.Date.Month == m);
								return new ReportMonthly
								{
									HasData = recs.Any(),
									Value = recs.Count(validSelector)
								};
							})
							.ToArray();

						return new ReportSummary
						{
							Year = g.Key,
							Monthly = monthly,
							YearValue = g.Count(validSelector)
						};
					})
					.ToList();

			// --- Monthly values across years (only years with data for that month) ---

			var monthlyAverages =
				Enumerable.Range(1, 12)
					.Select(m =>
					{
						var perMonthTotals =
							dayfile
								.GroupBy(d => d.Date.Year)
								.Select(g =>
								{
									var recs = g.Where(d => d.Date.Month == m);
									return recs.Any() ? recs.Count(validSelector) : (double?) null;
								})
								.Where(v => v.HasValue)
								.Select(v => v.Value)
								.ToList();

						return perMonthTotals.Count > 0
							? perMonthTotals.Average()
							: double.NaN;   // no data for this month in any year
					})
					.ToArray();


			// --- Annual average count across all years ---

			double avgAnnual =
				dayfile
					.GroupBy(d => d.Date.Year)
					.Average(g => g.Count(validSelector));

			if (txtFormat)
				return BuildReportText(perYear, 0, monthlyAverages, avgAnnual, 1, title);
			else
				return BuildReportHtml(perYear, 0, monthlyAverages, avgAnnual, 1, title);
		}


		private static string BuildReportText(List<ReportSummary> perYear, int decimals, double[] monthlyAverages, double avgAnnual, int avgDecimals, string title, string yearTotal = "TOTAL")
		{
			// --- Build plain-text output table ---

			var sb = new StringBuilder();
			var mthNames = DateTimeFormatInfo.CurrentInfo.AbbreviatedMonthNames;
			sb.AppendLine($"{{{{{title}}}}}");
			sb.AppendLine($"Year {mthNames[0].PadLeft(9)}{mthNames[1].PadLeft(9)}{mthNames[2].PadLeft(9)}{mthNames[3].PadLeft(9)}{mthNames[4].PadLeft(9)}{mthNames[5].PadLeft(9)}{mthNames[6].PadLeft(9)}{mthNames[7].PadLeft(9)}{mthNames[8].PadLeft(9)}{mthNames[9].PadLeft(9)}{mthNames[10].PadLeft(9)}{mthNames[11].PadLeft(9)}{yearTotal.PadLeft(11)}");

			foreach (var y in perYear)
			{
				sb.Append($"{y.Year}  ");

				for (int i = 0; i < 12; i++)
				{
					var m = y.Monthly[i];
					string cell = m.HasData ? m.Value.ToString("F" + decimals) : "-";
					sb.Append($"{cell,8} ");
				}

				sb.AppendLine($"{y.YearValue.ToString("F" + decimals).PadLeft(10)}");
			}

			sb.Append("Avg:  ");

			for (int i = 0; i < 12; i++)
			{
				double v = monthlyAverages[i];
				string cell = double.IsNaN(v) ? "-" : v.ToString("F" + avgDecimals);
				sb.Append($"{cell,8} ");
			}

			sb.AppendLine($"{avgAnnual.ToString("F" + avgDecimals).PadLeft(10)}");

			return sb.ToString();
		}

		private static string BuildReportHtml(List<ReportSummary> perYear, int decimals, double[] monthlyAverages, double avgAnnual, int avgDecimals, string title, string yearTotal = "TOTAL")
		{
			// --- Build plain-text output table ---

			var sb = new StringBuilder();
			var mthNames = DateTimeFormatInfo.CurrentInfo.AbbreviatedMonthNames;

			sb.AppendLine("<table class=\"table-content-html\">");
			sb.AppendLine($"<caption>{{{{{title}}}}}</caption");
			sb.AppendLine($"<tr><th>{{{{YEAR}}}}</th><th>{mthNames[0]}</th><th>{mthNames[1]}</th><th>{mthNames[2]}</th><th>{mthNames[3]}</th><th>{mthNames[4]}</th><th>{mthNames[5]}</th><th>{mthNames[6]}</th><th>{mthNames[7]}</th><th>{mthNames[8]}</th><th>{mthNames[9]}</th><th>{mthNames[10]}</th><th>{mthNames[11]}</th><th>{{{{{yearTotal}}}}}</th></tr></thead>");
			//sb.AppendLine("<tbody>");
			foreach (var y in perYear)
			{
				sb.Append($"<tr><td>{y.Year}</td>");

				for (int i = 0; i < 12; i++)
				{
					var m = y.Monthly[i];
					string cell = m.HasData ? m.Value.ToString("F" + decimals) : "-";
					sb.Append($"<td>{cell}</td>");
				}

				sb.AppendLine($"<td>{y.YearValue.ToString("F" + decimals)}</td></tr>");
			}

			sb.Append("<tr><td>{{AVERAGE}}:</td>");

			for (int i = 0; i < 12; i++)
			{
				double v = monthlyAverages[i];
				string cell = double.IsNaN(v) ? "-" : v.ToString("F" + avgDecimals);
				sb.Append($"<td>{cell}</td>");
			}

			sb.AppendLine($"<td>{avgAnnual.ToString("F" + avgDecimals)}</td></tr>");
			//sb.AppendLine("</tbody></table>");
			sb.AppendLine("</table>");

			return sb.ToString();
		}


		private sealed class ReportMonthly
		{
			public bool HasData { get; init; }
			public double Value { get; init; }
		}

		private sealed class ReportSummary
		{
			public int Year { get; init; }
			public ReportMonthly[] Monthly { get; init; } = new ReportMonthly[12];
			public double YearValue { get; init; }
		}
	}
}
