using System;
using System.IO;
using System.Text;

namespace CumulusMX
{
	internal class NOAAReports
	{
		private readonly Cumulus cumulus;
		private readonly WeatherStation station;
		private string noaafile;

		public NOAAReports(Cumulus cumulus, WeatherStation station)
		{
			this.cumulus = cumulus;
			this.station = station;
		}

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
				cumulus.LogMessage($"Error creating NOAA yearly report: {e.Message}");
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
				cumulus.LogMessage($"Error creating NOAA yearly report '{reportName}': {e.Message}");
				throw;
			}
			return report;
		}

		public string GetNoaaYearReport(int year)
		{
			DateTime noaats = new DateTime(year, 1, 1);
			var reportName = string.Empty;
			var report = string.Empty;
			try
			{
				reportName = noaats.ToString(cumulus.NOAAconf.YearFile);
				noaafile = cumulus.ReportPath + reportName;
				var encoding = cumulus.NOAAconf.UseUtf8 ? Encoding.GetEncoding("utf-8") : Encoding.GetEncoding("iso-8859-1");
				report = File.Exists(noaafile) ? File.ReadAllText(noaafile, encoding) : "That report does not exist";
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error getting NOAA yearly report '{reportName}': {e.Message}");
				report = "Something went wrong!";
			}
			return report;
		}

		public string GetNoaaMonthReport(int year, int month)
		{
			DateTime noaats = new DateTime(year, month, 1);
			var reportName = string.Empty;
			var report = string.Empty;
			try
			{
				reportName = noaats.ToString(cumulus.NOAAconf.MonthFile);
				noaafile = cumulus.ReportPath + reportName;
				var encoding = cumulus.NOAAconf.UseUtf8 ? Encoding.GetEncoding("utf-8") : Encoding.GetEncoding("iso-8859-1");
				report = File.Exists(noaafile) ? File.ReadAllText(noaafile, encoding) : "That report does not exist";
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error getting NOAA monthly report '{reportName}': {e.Message}");
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
				TimeZone tz = TimeZone.CurrentTimeZone;

				if (cumulus.Use10amInSummer && tz.IsDaylightSavingTime(dat))
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
				TimeZone tz = TimeZone.CurrentTimeZone;

				if (cumulus.Use10amInSummer && tz.IsDaylightSavingTime(dat))
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
	}
}
