using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CumulusMX
{
	internal class SimpleReports
	{
		public static string TemperatureReport(List<LogFiles.DayFileRec> dayfile, int decimals)
		{
			static double Field(LogFiles.DayFileRec d) => d.AvgTemp;
			static bool Valid(LogFiles.DayFileRec d) => d.AvgTemp >= -999;
			return Averages(dayfile, Field, decimals, Valid);
		}

		public static string RainfallReport(List<LogFiles.DayFileRec> dayfile, int decimals)
		{
			static double Field(LogFiles.DayFileRec d) => d.TotalRain;
			static bool Valid(LogFiles.DayFileRec d) => d.TotalRain >= 0;
			return Totals(dayfile, Field, decimals, Valid);
		}

		public static string WindRunReport(List<LogFiles.DayFileRec> dayfile, int decimals)
		{
			static double Field(LogFiles.DayFileRec d) => d.WindRun;
			static bool Valid(LogFiles.DayFileRec d) => d.WindRun >= 0;
			return Totals(dayfile, Field, decimals, Valid);
		}

		public static string SunShineReport(List<LogFiles.DayFileRec> dayfile)
		{
			static double Field(LogFiles.DayFileRec d) => d.SunShineHours;
			static bool Valid(LogFiles.DayFileRec d) => d.SunShineHours >= 0;
			return Totals(dayfile, Field, 1, Valid);
		}

		public static string ETReport(List<LogFiles.DayFileRec> dayfile, int decimals)
		{
			static double Field(LogFiles.DayFileRec d) => d.ET;
			static bool Valid(LogFiles.DayFileRec d) => d.ET >= 0;
			return Totals(dayfile, Field, decimals, Valid);
		}

		public static string WetDryDaysReport(List<LogFiles.DayFileRec> dayfile, double threshold, bool dry)
		{
			if (dry)
			{
				bool Valid(LogFiles.DayFileRec d) => d.TotalRain < threshold;
				return CountDryWet(dayfile, Valid);
			}
			else
			{
				bool Valid(LogFiles.DayFileRec d) => d.TotalRain >= threshold;
				return CountDryWet(dayfile, Valid);
			}
		}


		private static string Totals(List<LogFiles.DayFileRec> dayfile, Func<LogFiles.DayFileRec, double> valueSelector, int decimals, Func<LogFiles.DayFileRec, bool> validSelector)
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

			return BuildReportText(perYear, decimals, monthlyAverages, avgAnnual, decimals);
		}

		private static string Averages(List<LogFiles.DayFileRec> dayfile, Func<LogFiles.DayFileRec, double> valueSelector, int decimals, Func<LogFiles.DayFileRec, bool> validSelector)
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

			return BuildReportText(perYear, decimals, monthlyAverages, avgAnnual, decimals, "Average");
		}

		private static string CountDryWet(List<LogFiles.DayFileRec> dayfile, Func<LogFiles.DayFileRec, bool> validSelector)
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

			return BuildReportText(perYear, 0, monthlyAverages, avgAnnual, 1);
		}


		private static string BuildReportText(List<ReportSummary> perYear, int decimals, double[] monthlyAverages, double avgAnnual, int avgDecimals, string yearTotal = "Total")
		{
			// --- Build plain-text output table ---

			var sb = new StringBuilder();
			var mthNames = DateTimeFormatInfo.CurrentInfo.AbbreviatedMonthNames;

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
