using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CumulusMX
{
	public class NOAAReports
	{
		private readonly Cumulus cumulus;
		//private WeatherStation station;
		private List<string> report;
		//private string[] report;
		private string noaafile;

		public NOAAReports(Cumulus cumulus)
		//public NOAAReports()
		{
			this.cumulus = cumulus;
			//this.station = station;
		}

		public List<string> GenerateNoaaYearReport(int year)
		{
			NOAA noaa = new NOAA(cumulus);
			DateTime noaats = new DateTime(year, 1, 1);

			cumulus.LogMessage("Creating NOAA yearly report");
			report = noaa.CreateYearlyReport(noaats);
			try
			{
				// If not using UTF, then we have to convert the character set
				var utf8WithoutBom = new System.Text.UTF8Encoding(false);
				var encoding = cumulus.NOAAUseUTF8 ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
				var reportName = noaats.ToString(cumulus.NOAAYearFileFormat);
				noaafile = cumulus.ReportPath + reportName;
				cumulus.LogMessage("Saving yearly NOAA report as " + noaafile);
				File.WriteAllLines(noaafile, report, encoding);
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error creating NOAA yearly report: {e.Message}");
				throw;
			}
			return report;
		}

		public List<string> GenerateNoaaMonthReport(int year, int month)
		{
			NOAA noaa = new NOAA(cumulus);
			DateTime noaats = new DateTime(year, month, 1);

			cumulus.LogMessage("Creating NOAA monthly report");
			var report = noaa.CreateMonthlyReport(noaats);
			var reportName = String.Empty;
			try
			{
				// If not using UTF, then we have to convert the character set
				var utf8WithoutBom = new System.Text.UTF8Encoding(false);
				var encoding = cumulus.NOAAUseUTF8 ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
				reportName = noaats.ToString(cumulus.NOAAMonthFileFormat);
				noaafile = cumulus.ReportPath + reportName;
				cumulus.LogMessage("Saving monthly NOAA report as " + noaafile);
				File.WriteAllLines(noaafile, report, encoding);
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error creating NOAA yearly report '{reportName}': {e.Message}");
				throw;
			}
			return report;
		}

		public List<string> GetNoaaYearReport(int year)
		{
			DateTime noaats = new DateTime(year, 1, 1);
			var reportName = String.Empty;
			try
			{
				reportName = noaats.ToString(cumulus.NOAAYearFileFormat);
				noaafile = cumulus.ReportPath + reportName;
				report = File.Exists(noaafile) ? new List<string>(File.ReadAllLines(noaafile)) : new List<String> { "That report does not exist" };
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error getting NOAA yearly report '{reportName}': {e.Message}");
				report = new List<string> { "Something went wrong!" };
			}
			return report;
		}

		public List<string> GetNoaaMonthReport(int year, int month)
		{
			DateTime noaats = new DateTime(year, month, 1);
			var reportName = String.Empty;
			try
			{
				reportName = noaats.ToString(cumulus.NOAAMonthFileFormat);
				noaafile = cumulus.ReportPath + reportName;
				var encoding = cumulus.NOAAUseUTF8 ? Encoding.GetEncoding("utf-8") : Encoding.GetEncoding("iso-8859-1");
				report = File.Exists(noaafile) ? new List<string> (File.ReadAllLines(noaafile, encoding)) : new List<string> { "That report does not exist" };
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"Error getting NOAA monthly report '{reportName}': {e.Message}");
				report = new List<string> { "Something went wrong!" };
			}
			return report;
		}

		public string GetLastNoaaYearReportFilename(DateTime dat, bool fullPath)
		{
			// First determine the date for the logfile.
			// If we're using 9am rollover, the date should be 9 hours (10 in summer)
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
				return cumulus.ReportPath + logfiledate.ToString(cumulus.NOAAYearFileFormat);
			else
				return logfiledate.ToString(cumulus.NOAAYearFileFormat);
		}

		public string GetLastNoaaMonthReportFilename (DateTime dat, bool fullPath)
		{
			// First determine the date for the logfile.
			// If we're using 9am rollover, the date should be 9 hours (10 in summer)
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
				return cumulus.ReportPath + logfiledate.AddHours(-1).ToString(cumulus.NOAAMonthFileFormat);
			else
				return logfiledate.AddHours(-1).ToString(cumulus.NOAAMonthFileFormat);
		}
	}
}
