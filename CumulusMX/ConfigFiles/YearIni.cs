using System;
using System.IO;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadYearIniFile()
		{
			SetDefaultYearlyHighsAndLows();

			if (File.Exists(cumulus.YearIniFile))
			{
				var ini = new IniFile(cumulus.YearIniFile);

				ThisYear.HighWind.Val = ini.GetValue("Wind", "Speed", Cumulus.DefaultHiVal);
				ThisYear.HighWind.Ts = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				ThisYear.HighGust.Val = ini.GetValue("Wind", "Gust", Cumulus.DefaultHiVal);
				ThisYear.HighGust.Ts = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				ThisYear.HighWindRun.Val = ini.GetValue("Wind", "Windrun", Cumulus.DefaultHiVal);
				ThisYear.HighWindRun.Ts = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				ThisYear.LowTemp.Val = ini.GetValue("Temp", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowTemp.Ts = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighTemp.Val = ini.GetValue("Temp", "High", Cumulus.DefaultHiVal);
				ThisYear.HighTemp.Ts = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				ThisYear.LowMaxTemp.Val = ini.GetValue("Temp", "LowMax", Cumulus.DefaultLoVal);
				ThisYear.LowMaxTemp.Ts = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				ThisYear.HighMinTemp.Val = ini.GetValue("Temp", "HighMin", Cumulus.DefaultHiVal);
				ThisYear.HighMinTemp.Ts = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				ThisYear.LowDailyTempRange.Val = ini.GetValue("Temp", "LowRange", Cumulus.DefaultLoVal);
				ThisYear.LowDailyTempRange.Ts = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				ThisYear.HighDailyTempRange.Val = ini.GetValue("Temp", "HighRange", Cumulus.DefaultHiVal);
				ThisYear.HighDailyTempRange.Ts = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				ThisYear.LowPress.Val = ini.GetValue("Pressure", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowPress.Ts = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighPress.Val = ini.GetValue("Pressure", "High", Cumulus.DefaultHiVal);
				ThisYear.HighPress.Ts = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				ThisYear.HighRainRate.Val = ini.GetValue("Rain", "High", Cumulus.DefaultHiVal);
				ThisYear.HighRainRate.Ts = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				ThisYear.HourlyRain.Val = ini.GetValue("Rain", "HourlyHigh", Cumulus.DefaultHiVal);
				ThisYear.HourlyRain.Ts = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				ThisYear.DailyRain.Val = ini.GetValue("Rain", "DailyHigh", Cumulus.DefaultHiVal);
				ThisYear.DailyRain.Ts = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				ThisYear.HighRain24Hours.Val = ini.GetValue("Rain", "24Hour", Cumulus.DefaultHiVal);
				ThisYear.HighRain24Hours.Ts = ini.GetValue("Rain", "24HourTime", cumulus.defaultRecordTS);
				ThisYear.MonthlyRain.Val = ini.GetValue("Rain", "MonthlyHigh", Cumulus.DefaultHiVal);
				ThisYear.MonthlyRain.Ts = ini.GetValue("Rain", "HMonthlyTime", cumulus.defaultRecordTS);
				ThisYear.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", 0);
				ThisYear.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				ThisYear.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", 0);
				ThisYear.LongestWetPeriod.Ts = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				ThisYear.LowHumidity.Val = ini.GetValue("Humidity", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowHumidity.Ts = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighHumidity.Val = ini.GetValue("Humidity", "High", Cumulus.DefaultHiVal);
				ThisYear.HighHumidity.Ts = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				ThisYear.HighHeatIndex.Val = ini.GetValue("HeatIndex", "High", Cumulus.DefaultHiVal);
				ThisYear.HighHeatIndex.Ts = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				ThisYear.LowAppTemp.Val = ini.GetValue("AppTemp", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowAppTemp.Ts = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighAppTemp.Val = ini.GetValue("AppTemp", "High", Cumulus.DefaultHiVal);
				ThisYear.HighAppTemp.Ts = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				ThisYear.LowDewPoint.Val = ini.GetValue("Dewpoint", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowDewPoint.Ts = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighDewPoint.Val = ini.GetValue("Dewpoint", "High", Cumulus.DefaultHiVal);
				ThisYear.HighDewPoint.Ts = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				ThisYear.LowChill.Val = ini.GetValue("WindChill", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowChill.Ts = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like
				ThisYear.LowFeelsLike.Val = ini.GetValue("FeelsLike", "Low", Cumulus.DefaultLoVal);
				ThisYear.LowFeelsLike.Ts = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				ThisYear.HighFeelsLike.Val = ini.GetValue("FeelsLike", "High", Cumulus.DefaultHiVal);
				ThisYear.HighFeelsLike.Ts = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				ThisYear.HighHumidex.Val = ini.GetValue("Humidex", "High", Cumulus.DefaultHiVal);
				ThisYear.HighHumidex.Ts = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);
				// BGT
				ThisYear.HighBgt.Val = ini.GetValue("BGT", "High", Cumulus.DefaultHiVal);
				ThisYear.HighBgt.Ts = ini.GetValue("BGT", "HTime", cumulus.defaultRecordTS);
				// WBGT
				ThisYear.HighWbgt.Val = ini.GetValue("WBGT", "High", Cumulus.DefaultHiVal);
				ThisYear.HighWbgt.Ts = ini.GetValue("WBGT", "HTime", cumulus.defaultRecordTS);

				cumulus.LogMessage("Year.ini file read");
			}
		}

		public void WriteYearIniFile()
		{
			lock (yearIniThreadLock)
			{
				try
				{
					var hourInc = cumulus.GetHourInc();

					var ini = new IniFile(cumulus.YearIniFile);
					// Date
					ini.SetValue("General", "Date", DateTime.Now.AddHours(hourInc));
					// Wind
					ini.SetValue("Wind", "Speed", ThisYear.HighWind.Val);
					ini.SetValue("Wind", "SpTime", ThisYear.HighWind.Ts);
					ini.SetValue("Wind", "Gust", ThisYear.HighGust.Val);
					ini.SetValue("Wind", "Time", ThisYear.HighGust.Ts);
					ini.SetValue("Wind", "Windrun", ThisYear.HighWindRun.Val);
					ini.SetValue("Wind", "WindrunTime", ThisYear.HighWindRun.Ts);
					// Temperature
					ini.SetValue("Temp", "Low", ThisYear.LowTemp.Val);
					ini.SetValue("Temp", "LTime", ThisYear.LowTemp.Ts);
					ini.SetValue("Temp", "High", ThisYear.HighTemp.Val);
					ini.SetValue("Temp", "HTime", ThisYear.HighTemp.Ts);
					ini.SetValue("Temp", "LowMax", ThisYear.LowMaxTemp.Val);
					ini.SetValue("Temp", "LMTime", ThisYear.LowMaxTemp.Ts);
					ini.SetValue("Temp", "HighMin", ThisYear.HighMinTemp.Val);
					ini.SetValue("Temp", "HMTime", ThisYear.HighMinTemp.Ts);
					ini.SetValue("Temp", "LowRange", ThisYear.LowDailyTempRange.Val);
					ini.SetValue("Temp", "LowRangeTime", ThisYear.LowDailyTempRange.Ts);
					ini.SetValue("Temp", "HighRange", ThisYear.HighDailyTempRange.Val);
					ini.SetValue("Temp", "HighRangeTime", ThisYear.HighDailyTempRange.Ts);
					// Pressure
					ini.SetValue("Pressure", "Low", ThisYear.LowPress.Val);
					ini.SetValue("Pressure", "LTime", ThisYear.LowPress.Ts);
					ini.SetValue("Pressure", "High", ThisYear.HighPress.Val);
					ini.SetValue("Pressure", "HTime", ThisYear.HighPress.Ts);
					// rain
					ini.SetValue("Rain", "High", ThisYear.HighRainRate.Val);
					ini.SetValue("Rain", "HTime", ThisYear.HighRainRate.Ts);
					ini.SetValue("Rain", "HourlyHigh", ThisYear.HourlyRain.Val);
					ini.SetValue("Rain", "HHourlyTime", ThisYear.HourlyRain.Ts);
					ini.SetValue("Rain", "DailyHigh", ThisYear.DailyRain.Val);
					ini.SetValue("Rain", "HDailyTime", ThisYear.DailyRain.Ts);
					ini.SetValue("Rain", "24Hour", ThisYear.HighRain24Hours.Val);
					ini.SetValue("Rain", "24HourTime", ThisYear.HighRain24Hours.Ts);
					ini.SetValue("Rain", "MonthlyHigh", ThisYear.MonthlyRain.Val);
					ini.SetValue("Rain", "HMonthlyTime", ThisYear.MonthlyRain.Ts);
					ini.SetValue("Rain", "LongestDryPeriod", ThisYear.LongestDryPeriod.Val);
					ini.SetValue("Rain", "LongestDryPeriodTime", ThisYear.LongestDryPeriod.Ts);
					ini.SetValue("Rain", "LongestWetPeriod", ThisYear.LongestWetPeriod.Val);
					ini.SetValue("Rain", "LongestWetPeriodTime", ThisYear.LongestWetPeriod.Ts);
					// humidity
					ini.SetValue("Humidity", "Low", ThisYear.LowHumidity.Val);
					ini.SetValue("Humidity", "LTime", ThisYear.LowHumidity.Ts);
					ini.SetValue("Humidity", "High", ThisYear.HighHumidity.Val);
					ini.SetValue("Humidity", "HTime", ThisYear.HighHumidity.Ts);
					// heat index
					ini.SetValue("HeatIndex", "High", ThisYear.HighHeatIndex.Val);
					ini.SetValue("HeatIndex", "HTime", ThisYear.HighHeatIndex.Ts);
					// App temp
					ini.SetValue("AppTemp", "Low", ThisYear.LowAppTemp.Val);
					ini.SetValue("AppTemp", "LTime", ThisYear.LowAppTemp.Ts);
					ini.SetValue("AppTemp", "High", ThisYear.HighAppTemp.Val);
					ini.SetValue("AppTemp", "HTime", ThisYear.HighAppTemp.Ts);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", ThisYear.LowDewPoint.Val);
					ini.SetValue("Dewpoint", "LTime", ThisYear.LowDewPoint.Ts);
					ini.SetValue("Dewpoint", "High", ThisYear.HighDewPoint.Val);
					ini.SetValue("Dewpoint", "HTime", ThisYear.HighDewPoint.Ts);
					// wind chill
					ini.SetValue("WindChill", "Low", ThisYear.LowChill.Val);
					ini.SetValue("WindChill", "LTime", ThisYear.LowChill.Ts);
					// Feels like
					ini.SetValue("FeelsLike", "Low", ThisYear.LowFeelsLike.Val);
					ini.SetValue("FeelsLike", "LTime", ThisYear.LowFeelsLike.Ts);
					ini.SetValue("FeelsLike", "High", ThisYear.HighFeelsLike.Val);
					ini.SetValue("FeelsLike", "HTime", ThisYear.HighFeelsLike.Ts);
					// Humidex
					ini.SetValue("Humidex", "High", ThisYear.HighHumidex.Val);
					ini.SetValue("Humidex", "HTime", ThisYear.HighHumidex.Ts);
					// BGT
					ini.SetValue("BGT", "High", ThisYear.HighBgt.Val);
					ini.SetValue("BGT", "HTime", ThisYear.HighBgt.Ts);
					// WBGT
					ini.SetValue("WBGT", "High", ThisYear.HighWbgt.Val);
					ini.SetValue("WBGT", "HTime", ThisYear.HighWbgt.Ts);

					ini.Flush();
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error writing year.ini file: " + ex.Message);
				}
			}
		}
	}
}
