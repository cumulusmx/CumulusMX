using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{
	internal class NOAAReports(Cumulus cumulus, WeatherStation station)
	{
		private readonly Cumulus cumulus = cumulus;
		private readonly WeatherStation station = station;
		private string noaafile;

		public string GenerateNoaaYearReport(int year)
		{
			NOAA noaa = new NOAA(cumulus, station);
			DateTime noaats = new DateTime(year, 1, 1);

			cumulus.LogMessage("Creating NOAA yearly report");
			var report = noaa.CreateYearlyReport(noaats);
			try
			{
				// If not using UTF, then we have to convert the character set
				var utf8WithoutBom = new UTF8Encoding(false);
				var encoding = cumulus.NOAAconf.UseUtf8 ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
				var reportName = noaats.ToString(cumulus.NOAAconf.YearFile);
				noaafile = cumulus.ReportPath + reportName;
				cumulus.LogMessage("Saving yearly NOAA report as " + noaafile);
				File.WriteAllText(noaafile, report, encoding);
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"Error creating NOAA yearly report: {e.Message}");
				throw;
			}
			return report;
		}

		public string GenerateNoaaMonthReport(int year, int month)
		{
			NOAA noaa = new NOAA(cumulus, station);
			DateTime noaats = new DateTime(year, month, 1);

			cumulus.LogMessage("Creating NOAA monthly report");
			var report = noaa.CreateMonthlyReport(noaats);
			var reportName = String.Empty;
			try
			{
				// If not using UTF, then we have to convert the character set
				var utf8WithoutBom = new UTF8Encoding(false);
				var encoding = cumulus.NOAAconf.UseUtf8 ? utf8WithoutBom : Encoding.GetEncoding("iso-8859-1");
				reportName = noaats.ToString(cumulus.NOAAconf.MonthFile);
				noaafile = cumulus.ReportPath + reportName;
				cumulus.LogMessage("Saving monthly NOAA report as " + noaafile);

				File.WriteAllText(noaafile, report, encoding);
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"Error creating NOAA yearly report '{reportName}': {e.Message}");
				throw;
			}
			return report;
		}

		public string GenerateMissing()
		{
			var missingMonths = new List<DateTime>();
			var missingYears = new List<DateTime>();
			var checkDate = cumulus.RecordsBeganDateTime.Date;
			string reportName;
			var now = DateTime.Now;


			var lastRptDate = GetLastReportDate();
			var lastYear = 0;

			// iterate all years and months since records began date
			var doMore = true;
			while (doMore)
			{
				// first check the yearly report
				if (lastYear != checkDate.Year)
				{
					reportName = checkDate.ToString(cumulus.NOAAconf.YearFile);

					if (!File.Exists(cumulus.ReportPath + reportName))
					{
						missingYears.Add(checkDate);
					}
					lastYear = checkDate.Year;
				}

				// then check the monthly report
				reportName = checkDate.ToString(cumulus.NOAAconf.MonthFile);

				if (!File.Exists(cumulus.ReportPath + reportName))
				{
					missingMonths.Add(checkDate);
				}

				// increment the month
				// note this may reset the day
				checkDate = checkDate.AddMonths(1);

				if (checkDate.Year == lastRptDate.Year && checkDate.Month == lastRptDate.Month)
				{
					doMore = false;
				}
			}


			if (missingMonths.Count > 0 || missingYears.Count > 0)
			{
				// spawn a task to recreate the reports, but don't wait for it to complete

				Task.Run(() =>
				{
					// first do the months
					foreach (var month in missingMonths)
					{
						GenerateNoaaMonthReport(month.Year, month.Month);
					}

					// then the years
					foreach (var year in missingYears)
					{
						GenerateNoaaYearReport(year.Year);
					}
				});

				// report back how many reports are being created
				var sb = new StringBuilder("Recreating the following reports...\n");
				if (missingMonths.Count > 0)
				{
					sb.AppendLine("Monthly:");
					foreach (var rpt in missingMonths)
					{
						sb.AppendLine("\t" + rpt.ToString("MMM yyyy"));
					}
				}

				if (missingYears.Count > 0)
				{
					sb.AppendLine("\nYearly:");
					foreach (var rpt in missingYears)
					{
						sb.AppendLine("\t" + rpt.ToString("yyyy"));
					}
				}

				sb.Append("\nThis may take a little while, you can check the progress in the MX diags log");

				return sb.ToString();
			}
			else
			{
				return "There are no missing reports to recreate. If you want to recreate some existing reports you must first delete them from your Reports folder";
			}
		}

		public string GetNoaaYearReport(int year)
		{
			DateTime noaats = new DateTime(year, 1, 1);
			var reportName = string.Empty;
			string report;
			try
			{
				reportName = noaats.ToString(cumulus.NOAAconf.YearFile);
				noaafile = cumulus.ReportPath + reportName;
				var encoding = cumulus.NOAAconf.UseUtf8 ? Encoding.GetEncoding("utf-8") : Encoding.GetEncoding("iso-8859-1");
				report = File.Exists(noaafile) ? File.ReadAllText(noaafile, encoding) : "That report does not exist";
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"Error getting NOAA yearly report '{reportName}': {e.Message}");
				report = "Something went wrong!";
			}
			return report;
		}

		public string GetNoaaMonthReport(int year, int month)
		{
			DateTime noaats = new DateTime(year, month, 1);
			var reportName = string.Empty;
			string report;
			try
			{
				reportName = noaats.ToString(cumulus.NOAAconf.MonthFile);
				noaafile = cumulus.ReportPath + reportName;
				var encoding = cumulus.NOAAconf.UseUtf8 ? Encoding.GetEncoding("utf-8") : Encoding.GetEncoding("iso-8859-1");
				report = File.Exists(noaafile) ? File.ReadAllText(noaafile, encoding) : "That report does not exist";
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage($"Error getting NOAA monthly report '{reportName}': {e.Message}");
				report = "Something went wrong!";
			}
			return report;
		}

		public string GetLastNoaaYearReportFilename(DateTime dat, bool fullPath)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			// This assumes that the caller has already subtracted a day if required
			DateTime logfiledate;

			if (cumulus.RolloverHour == 0)
			{
				logfiledate = dat.AddDays(-1);
			}
			else
			{
				if (cumulus.Use10amInSummer && TimeZoneInfo.Local.IsDaylightSavingTime(dat))
				{
					// Locale is currently on Daylight (summer) time
					logfiledate = dat.AddHours(-10);
				}
				else
				{
					// Locale is currently on Standard time or unknown
					logfiledate = dat.AddHours(-9);
				}
			}

			if (fullPath)
				return cumulus.ReportPath + logfiledate.ToString(cumulus.NOAAconf.YearFile);
			else
				return logfiledate.ToString(cumulus.NOAAconf.YearFile);
		}

		public string GetLastNoaaMonthReportFilename(DateTime dat, bool fullPath)
		{
			// First determine the date for the log file.
			// If we're using 9am roll-over, the date should be 9 hours (10 in summer)
			// before 'Now'
			// This assumes that the caller has already subtracted a day if required
			DateTime logfiledate;

			if (cumulus.RolloverHour == 0)
			{
				logfiledate = dat.AddDays(-1);
			}
			else
			{
				if (cumulus.Use10amInSummer && TimeZoneInfo.Local.IsDaylightSavingTime(dat))
				{
					// Locale is currently on Daylight (summer) time
					logfiledate = dat.AddHours(-10);
				}
				else
				{
					// Locale is currently on Standard time or unknown
					logfiledate = dat.AddHours(-9);
				}
			}
			if (fullPath)
				return cumulus.ReportPath + logfiledate.AddHours(-1).ToString(cumulus.NOAAconf.MonthFile);
			else
				return logfiledate.AddHours(-1).ToString(cumulus.NOAAconf.MonthFile);
		}

		private DateTime GetLastReportDate()
		{
			// returns the datetime of the latest possible report
			var now = DateTime.Now;
			DateTime reportDate;

			if (cumulus.RolloverHour == 0)
			{
				reportDate = now.AddDays(-1);
			}
			else
			{
				if (cumulus.Use10amInSummer && TimeZoneInfo.Local.IsDaylightSavingTime(now))
				{
					// Locale is currently on Daylight (summer) time
					reportDate = now.AddHours(-10);
				}
				else
				{
					// Locale is currently on Standard time or unknown
					reportDate = now.AddHours(-9);
				}
			}

			return reportDate.Date;
		}
	}
}
