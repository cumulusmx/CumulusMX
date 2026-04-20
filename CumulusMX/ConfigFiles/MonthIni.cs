using System;
using System.IO;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadMonthIniFile()
		{
			SetDefaultMonthlyHighsAndLows();

			if (File.Exists(cumulus.MonthIniFile))
			{
				var ini = new IniFile(cumulus.MonthIniFile);

				ThisMonth.HighWind.Val = ini.GetValue("Wind", "Speed", Cumulus.DefaultHiVal);
				ThisMonth.HighWind.Ts = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				ThisMonth.HighGust.Val = ini.GetValue("Wind", "Gust", Cumulus.DefaultHiVal);
				ThisMonth.HighGust.Ts = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				ThisMonth.HighWindRun.Val = ini.GetValue("Wind", "Windrun", Cumulus.DefaultHiVal);
				ThisMonth.HighWindRun.Ts = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				ThisMonth.LowTemp.Val = ini.GetValue("Temp", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowTemp.Ts = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighTemp.Val = ini.GetValue("Temp", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighTemp.Ts = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				ThisMonth.LowMaxTemp.Val = ini.GetValue("Temp", "LowMax", Cumulus.DefaultLoVal);
				ThisMonth.LowMaxTemp.Ts = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				ThisMonth.HighMinTemp.Val = ini.GetValue("Temp", "HighMin", Cumulus.DefaultHiVal);
				ThisMonth.HighMinTemp.Ts = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				ThisMonth.LowDailyTempRange.Val = ini.GetValue("Temp", "LowRange", Cumulus.DefaultLoVal);
				ThisMonth.LowDailyTempRange.Ts = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				ThisMonth.HighDailyTempRange.Val = ini.GetValue("Temp", "HighRange", Cumulus.DefaultHiVal);
				ThisMonth.HighDailyTempRange.Ts = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				ThisMonth.LowPress.Val = ini.GetValue("Pressure", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowPress.Ts = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighPress.Val = ini.GetValue("Pressure", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighPress.Ts = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				ThisMonth.HighRainRate.Val = ini.GetValue("Rain", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighRainRate.Ts = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				ThisMonth.HourlyRain.Val = ini.GetValue("Rain", "HourlyHigh", Cumulus.DefaultHiVal);
				ThisMonth.HourlyRain.Ts = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				ThisMonth.DailyRain.Val = ini.GetValue("Rain", "DailyHigh", Cumulus.DefaultHiVal);
				ThisMonth.DailyRain.Ts = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				ThisMonth.HighRain24Hours.Val = ini.GetValue("Rain", "24Hour", Cumulus.DefaultHiVal);
				ThisMonth.HighRain24Hours.Ts = ini.GetValue("Rain", "24HourTime", cumulus.defaultRecordTS);
				ThisMonth.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", 0);
				ThisMonth.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				ThisMonth.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", 0);
				ThisMonth.LongestWetPeriod.Ts = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				ThisMonth.LowHumidity.Val = ini.GetValue("Humidity", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowHumidity.Ts = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighHumidity.Val = ini.GetValue("Humidity", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighHumidity.Ts = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				ThisMonth.HighHeatIndex.Val = ini.GetValue("HeatIndex", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighHeatIndex.Ts = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				ThisMonth.LowAppTemp.Val = ini.GetValue("AppTemp", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowAppTemp.Ts = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighAppTemp.Val = ini.GetValue("AppTemp", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighAppTemp.Ts = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				ThisMonth.LowDewPoint.Val = ini.GetValue("Dewpoint", "Low", 999.0);
				ThisMonth.LowDewPoint.Ts = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighDewPoint.Val = ini.GetValue("Dewpoint", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighDewPoint.Ts = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				ThisMonth.LowChill.Val = ini.GetValue("WindChill", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowChill.Ts = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like temp
				ThisMonth.LowFeelsLike.Val = ini.GetValue("FeelsLike", "Low", Cumulus.DefaultLoVal);
				ThisMonth.LowFeelsLike.Ts = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				ThisMonth.HighFeelsLike.Val = ini.GetValue("FeelsLike", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighFeelsLike.Ts = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				ThisMonth.HighHumidex.Val = ini.GetValue("Humidex", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighHumidex.Ts = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);
				// BGT
				ThisMonth.HighBgt.Val = ini.GetValue("BGT", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighBgt.Ts = ini.GetValue("BGT", "HTime", cumulus.defaultRecordTS);
				// WBGT
				ThisMonth.HighWbgt.Val = ini.GetValue("WBGT", "High", Cumulus.DefaultHiVal);
				ThisMonth.HighWbgt.Ts = ini.GetValue("WBGT", "HTime", cumulus.defaultRecordTS);

				cumulus.LogMessage("Month.ini file read");
			}
		}

		public void WriteMonthIniFile()
		{
			cumulus.LogDebugMessage("Writing to Month.ini file");
			lock (monthIniThreadLock)
			{
				try
				{
					var hourInc = cumulus.GetHourInc();

					var ini = new IniFile(cumulus.MonthIniFile);
					// Date
					ini.SetValue("General", "Date", DateTime.Now.AddHours(hourInc));
					// Wind
					ini.SetValue("Wind", "Speed", ThisMonth.HighWind.Val);
					ini.SetValue("Wind", "SpTime", ThisMonth.HighWind.Ts);
					ini.SetValue("Wind", "Gust", ThisMonth.HighGust.Val);
					ini.SetValue("Wind", "Time", ThisMonth.HighGust.Ts);
					ini.SetValue("Wind", "Windrun", ThisMonth.HighWindRun.Val);
					ini.SetValue("Wind", "WindrunTime", ThisMonth.HighWindRun.Ts);
					// Temperature
					ini.SetValue("Temp", "Low", ThisMonth.LowTemp.Val);
					ini.SetValue("Temp", "LTime", ThisMonth.LowTemp.Ts);
					ini.SetValue("Temp", "High", ThisMonth.HighTemp.Val);
					ini.SetValue("Temp", "HTime", ThisMonth.HighTemp.Ts);
					ini.SetValue("Temp", "LowMax", ThisMonth.LowMaxTemp.Val);
					ini.SetValue("Temp", "LMTime", ThisMonth.LowMaxTemp.Ts);
					ini.SetValue("Temp", "HighMin", ThisMonth.HighMinTemp.Val);
					ini.SetValue("Temp", "HMTime", ThisMonth.HighMinTemp.Ts);
					ini.SetValue("Temp", "LowRange", ThisMonth.LowDailyTempRange.Val);
					ini.SetValue("Temp", "LowRangeTime", ThisMonth.LowDailyTempRange.Ts);
					ini.SetValue("Temp", "HighRange", ThisMonth.HighDailyTempRange.Val);
					ini.SetValue("Temp", "HighRangeTime", ThisMonth.HighDailyTempRange.Ts);
					// Pressure
					ini.SetValue("Pressure", "Low", ThisMonth.LowPress.Val);
					ini.SetValue("Pressure", "LTime", ThisMonth.LowPress.Ts);
					ini.SetValue("Pressure", "High", ThisMonth.HighPress.Val);
					ini.SetValue("Pressure", "HTime", ThisMonth.HighPress.Ts);
					// rain
					ini.SetValue("Rain", "High", ThisMonth.HighRainRate.Val);
					ini.SetValue("Rain", "HTime", ThisMonth.HighRainRate.Ts);
					ini.SetValue("Rain", "HourlyHigh", ThisMonth.HourlyRain.Val);
					ini.SetValue("Rain", "HHourlyTime", ThisMonth.HourlyRain.Ts);
					ini.SetValue("Rain", "DailyHigh", ThisMonth.DailyRain.Val);
					ini.SetValue("Rain", "HDailyTime", ThisMonth.DailyRain.Ts);
					ini.SetValue("Rain", "24Hour", ThisMonth.HighRain24Hours.Val);
					ini.SetValue("Rain", "24HourTime", ThisMonth.HighRain24Hours.Ts);
					ini.SetValue("Rain", "LongestDryPeriod", ThisMonth.LongestDryPeriod.Val);
					ini.SetValue("Rain", "LongestDryPeriodTime", ThisMonth.LongestDryPeriod.Ts);
					ini.SetValue("Rain", "LongestWetPeriod", ThisMonth.LongestWetPeriod.Val);
					ini.SetValue("Rain", "LongestWetPeriodTime", ThisMonth.LongestWetPeriod.Ts);
					// humidity
					ini.SetValue("Humidity", "Low", ThisMonth.LowHumidity.Val);
					ini.SetValue("Humidity", "LTime", ThisMonth.LowHumidity.Ts);
					ini.SetValue("Humidity", "High", ThisMonth.HighHumidity.Val);
					ini.SetValue("Humidity", "HTime", ThisMonth.HighHumidity.Ts);
					// heat index
					ini.SetValue("HeatIndex", "High", ThisMonth.HighHeatIndex.Val);
					ini.SetValue("HeatIndex", "HTime", ThisMonth.HighHeatIndex.Ts);
					// App temp
					ini.SetValue("AppTemp", "Low", ThisMonth.LowAppTemp.Val);
					ini.SetValue("AppTemp", "LTime", ThisMonth.LowAppTemp.Ts);
					ini.SetValue("AppTemp", "High", ThisMonth.HighAppTemp.Val);
					ini.SetValue("AppTemp", "HTime", ThisMonth.HighAppTemp.Ts);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", ThisMonth.LowDewPoint.Val);
					ini.SetValue("Dewpoint", "LTime", ThisMonth.LowDewPoint.Ts);
					ini.SetValue("Dewpoint", "High", ThisMonth.HighDewPoint.Val);
					ini.SetValue("Dewpoint", "HTime", ThisMonth.HighDewPoint.Ts);
					// wind chill
					ini.SetValue("WindChill", "Low", ThisMonth.LowChill.Val);
					ini.SetValue("WindChill", "LTime", ThisMonth.LowChill.Ts);
					// feels like
					ini.SetValue("FeelsLike", "Low", ThisMonth.LowFeelsLike.Val);
					ini.SetValue("FeelsLike", "LTime", ThisMonth.LowFeelsLike.Ts);
					ini.SetValue("FeelsLike", "High", ThisMonth.HighFeelsLike.Val);
					ini.SetValue("FeelsLike", "HTime", ThisMonth.HighFeelsLike.Ts);
					// Humidex
					ini.SetValue("Humidex", "High", ThisMonth.HighHumidex.Val);
					ini.SetValue("Humidex", "HTime", ThisMonth.HighHumidex.Ts);
					// BGT
					ini.SetValue("BGT", "High", ThisMonth.HighBgt.Val);
					ini.SetValue("BGT", "HTime", ThisMonth.HighBgt.Ts);
					// WBGT
					ini.SetValue("WBGT", "High", ThisMonth.HighWbgt.Val);
					ini.SetValue("WBGT", "HTime", ThisMonth.HighWbgt.Ts);

					ini.Flush();
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("Error writing month.ini file: " + ex.Message);
				}
			}
			cumulus.LogDebugMessage("End writing to Month.ini file");
		}
	}
}
