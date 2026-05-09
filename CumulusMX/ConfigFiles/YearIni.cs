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

				 Records.ThisYear.HighWind.Val = ini.GetValue("Wind", "Speed", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighWind.Ts = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighGust.Val = ini.GetValue("Wind", "Gust", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighGust.Ts = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				 Records.ThisYear.HighWindRun.Val = ini.GetValue("Wind", "Windrun", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighWindRun.Ts = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				 Records.ThisYear.LowTemp.Val = ini.GetValue("Temp", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowTemp.Ts = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighTemp.Val = ini.GetValue("Temp", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighTemp.Ts = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				 Records.ThisYear.LowMaxTemp.Val = ini.GetValue("Temp", "LowMax", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowMaxTemp.Ts = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighMinTemp.Val = ini.GetValue("Temp", "HighMin", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighMinTemp.Ts = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				 Records.ThisYear.LowDailyTempRange.Val = ini.GetValue("Temp", "LowRange", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowDailyTempRange.Ts = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighDailyTempRange.Val = ini.GetValue("Temp", "HighRange", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighDailyTempRange.Ts = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				 Records.ThisYear.LowPress.Val = ini.GetValue("Pressure", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowPress.Ts = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighPress.Val = ini.GetValue("Pressure", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighPress.Ts = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				 Records.ThisYear.HighRainRate.Val = ini.GetValue("Rain", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighRainRate.Ts = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HourlyRain.Val = ini.GetValue("Rain", "HourlyHigh", Cumulus.DefaultHiVal);
				 Records.ThisYear.HourlyRain.Ts = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				 Records.ThisYear.DailyRain.Val = ini.GetValue("Rain", "DailyHigh", Cumulus.DefaultHiVal);
				 Records.ThisYear.DailyRain.Ts = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighRain24Hours.Val = ini.GetValue("Rain", "24Hour", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighRain24Hours.Ts = ini.GetValue("Rain", "24HourTime", cumulus.defaultRecordTS);
				 Records.ThisYear.MonthlyRain.Val = ini.GetValue("Rain", "MonthlyHigh", Cumulus.DefaultHiVal);
				 Records.ThisYear.MonthlyRain.Ts = ini.GetValue("Rain", "HMonthlyTime", cumulus.defaultRecordTS);
				 Records.ThisYear.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", 0);
				 Records.ThisYear.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				 Records.ThisYear.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", 0);
				 Records.ThisYear.LongestWetPeriod.Ts = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				 Records.ThisYear.LowHumidity.Val = ini.GetValue("Humidity", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowHumidity.Ts = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighHumidity.Val = ini.GetValue("Humidity", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighHumidity.Ts = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				 Records.ThisYear.HighHeatIndex.Val = ini.GetValue("HeatIndex", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighHeatIndex.Ts = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				 Records.ThisYear.LowAppTemp.Val = ini.GetValue("AppTemp", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowAppTemp.Ts = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighAppTemp.Val = ini.GetValue("AppTemp", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighAppTemp.Ts = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				 Records.ThisYear.LowDewPoint.Val = ini.GetValue("Dewpoint", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowDewPoint.Ts = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighDewPoint.Val = ini.GetValue("Dewpoint", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighDewPoint.Ts = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				 Records.ThisYear.LowChill.Val = ini.GetValue("WindChill", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowChill.Ts = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like
				 Records.ThisYear.LowFeelsLike.Val = ini.GetValue("FeelsLike", "Low", Cumulus.DefaultLoVal);
				 Records.ThisYear.LowFeelsLike.Ts = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				 Records.ThisYear.HighFeelsLike.Val = ini.GetValue("FeelsLike", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighFeelsLike.Ts = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				 Records.ThisYear.HighHumidex.Val = ini.GetValue("Humidex", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighHumidex.Ts = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);
				// BGT
				 Records.ThisYear.HighBgt.Val = ini.GetValue("BGT", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighBgt.Ts = ini.GetValue("BGT", "HTime", cumulus.defaultRecordTS);
				// WBGT
				 Records.ThisYear.HighWbgt.Val = ini.GetValue("WBGT", "High", Cumulus.DefaultHiVal);
				 Records.ThisYear.HighWbgt.Ts = ini.GetValue("WBGT", "HTime", cumulus.defaultRecordTS);

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
					ini.SetValue("Wind", "Speed",  Records.ThisYear.HighWind.Val);
					ini.SetValue("Wind", "SpTime",  Records.ThisYear.HighWind.Ts);
					ini.SetValue("Wind", "Gust",  Records.ThisYear.HighGust.Val);
					ini.SetValue("Wind", "Time",  Records.ThisYear.HighGust.Ts);
					ini.SetValue("Wind", "Windrun",  Records.ThisYear.HighWindRun.Val);
					ini.SetValue("Wind", "WindrunTime",  Records.ThisYear.HighWindRun.Ts);
					// Temperature
					ini.SetValue("Temp", "Low",  Records.ThisYear.LowTemp.Val);
					ini.SetValue("Temp", "LTime",  Records.ThisYear.LowTemp.Ts);
					ini.SetValue("Temp", "High",  Records.ThisYear.HighTemp.Val);
					ini.SetValue("Temp", "HTime",  Records.ThisYear.HighTemp.Ts);
					ini.SetValue("Temp", "LowMax",  Records.ThisYear.LowMaxTemp.Val);
					ini.SetValue("Temp", "LMTime",  Records.ThisYear.LowMaxTemp.Ts);
					ini.SetValue("Temp", "HighMin",  Records.ThisYear.HighMinTemp.Val);
					ini.SetValue("Temp", "HMTime",  Records.ThisYear.HighMinTemp.Ts);
					ini.SetValue("Temp", "LowRange",  Records.ThisYear.LowDailyTempRange.Val);
					ini.SetValue("Temp", "LowRangeTime",  Records.ThisYear.LowDailyTempRange.Ts);
					ini.SetValue("Temp", "HighRange",  Records.ThisYear.HighDailyTempRange.Val);
					ini.SetValue("Temp", "HighRangeTime",  Records.ThisYear.HighDailyTempRange.Ts);
					// Pressure
					ini.SetValue("Pressure", "Low",  Records.ThisYear.LowPress.Val);
					ini.SetValue("Pressure", "LTime",  Records.ThisYear.LowPress.Ts);
					ini.SetValue("Pressure", "High",  Records.ThisYear.HighPress.Val);
					ini.SetValue("Pressure", "HTime",  Records.ThisYear.HighPress.Ts);
					// rain
					ini.SetValue("Rain", "High",  Records.ThisYear.HighRainRate.Val);
					ini.SetValue("Rain", "HTime",  Records.ThisYear.HighRainRate.Ts);
					ini.SetValue("Rain", "HourlyHigh",  Records.ThisYear.HourlyRain.Val);
					ini.SetValue("Rain", "HHourlyTime",  Records.ThisYear.HourlyRain.Ts);
					ini.SetValue("Rain", "DailyHigh",  Records.ThisYear.DailyRain.Val);
					ini.SetValue("Rain", "HDailyTime",  Records.ThisYear.DailyRain.Ts);
					ini.SetValue("Rain", "24Hour",  Records.ThisYear.HighRain24Hours.Val);
					ini.SetValue("Rain", "24HourTime",  Records.ThisYear.HighRain24Hours.Ts);
					ini.SetValue("Rain", "MonthlyHigh",  Records.ThisYear.MonthlyRain.Val);
					ini.SetValue("Rain", "HMonthlyTime",  Records.ThisYear.MonthlyRain.Ts);
					ini.SetValue("Rain", "LongestDryPeriod",  Records.ThisYear.LongestDryPeriod.Val);
					ini.SetValue("Rain", "LongestDryPeriodTime",  Records.ThisYear.LongestDryPeriod.Ts);
					ini.SetValue("Rain", "LongestWetPeriod",  Records.ThisYear.LongestWetPeriod.Val);
					ini.SetValue("Rain", "LongestWetPeriodTime",  Records.ThisYear.LongestWetPeriod.Ts);
					// humidity
					ini.SetValue("Humidity", "Low",  Records.ThisYear.LowHumidity.Val);
					ini.SetValue("Humidity", "LTime",  Records.ThisYear.LowHumidity.Ts);
					ini.SetValue("Humidity", "High",  Records.ThisYear.HighHumidity.Val);
					ini.SetValue("Humidity", "HTime",  Records.ThisYear.HighHumidity.Ts);
					// heat index
					ini.SetValue("HeatIndex", "High",  Records.ThisYear.HighHeatIndex.Val);
					ini.SetValue("HeatIndex", "HTime",  Records.ThisYear.HighHeatIndex.Ts);
					// App temp
					ini.SetValue("AppTemp", "Low",  Records.ThisYear.LowAppTemp.Val);
					ini.SetValue("AppTemp", "LTime",  Records.ThisYear.LowAppTemp.Ts);
					ini.SetValue("AppTemp", "High",  Records.ThisYear.HighAppTemp.Val);
					ini.SetValue("AppTemp", "HTime",  Records.ThisYear.HighAppTemp.Ts);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low",  Records.ThisYear.LowDewPoint.Val);
					ini.SetValue("Dewpoint", "LTime",  Records.ThisYear.LowDewPoint.Ts);
					ini.SetValue("Dewpoint", "High",  Records.ThisYear.HighDewPoint.Val);
					ini.SetValue("Dewpoint", "HTime",  Records.ThisYear.HighDewPoint.Ts);
					// wind chill
					ini.SetValue("WindChill", "Low",  Records.ThisYear.LowChill.Val);
					ini.SetValue("WindChill", "LTime",  Records.ThisYear.LowChill.Ts);
					// Feels like
					ini.SetValue("FeelsLike", "Low",  Records.ThisYear.LowFeelsLike.Val);
					ini.SetValue("FeelsLike", "LTime",  Records.ThisYear.LowFeelsLike.Ts);
					ini.SetValue("FeelsLike", "High",  Records.ThisYear.HighFeelsLike.Val);
					ini.SetValue("FeelsLike", "HTime",  Records.ThisYear.HighFeelsLike.Ts);
					// Humidex
					ini.SetValue("Humidex", "High",  Records.ThisYear.HighHumidex.Val);
					ini.SetValue("Humidex", "HTime",  Records.ThisYear.HighHumidex.Ts);
					// BGT
					ini.SetValue("BGT", "High",  Records.ThisYear.HighBgt.Val);
					ini.SetValue("BGT", "HTime",  Records.ThisYear.HighBgt.Ts);
					// WBGT
					ini.SetValue("WBGT", "High",  Records.ThisYear.HighWbgt.Val);
					ini.SetValue("WBGT", "HTime",  Records.ThisYear.HighWbgt.Ts);

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
