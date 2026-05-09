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

				Records.ThisMonth.HighWind.Val = ini.GetValue("Wind", "Speed", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighWind.Ts = ini.GetValue("Wind", "SpTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighGust.Val = ini.GetValue("Wind", "Gust", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighGust.Ts = ini.GetValue("Wind", "Time", cumulus.defaultRecordTS);
				Records.ThisMonth.HighWindRun.Val = ini.GetValue("Wind", "Windrun", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighWindRun.Ts = ini.GetValue("Wind", "WindrunTime", cumulus.defaultRecordTS);
				// Temperature
				Records.ThisMonth.LowTemp.Val = ini.GetValue("Temp", "Low", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowTemp.Ts = ini.GetValue("Temp", "LTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighTemp.Val = ini.GetValue("Temp", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighTemp.Ts = ini.GetValue("Temp", "HTime", cumulus.defaultRecordTS);
				Records.ThisMonth.LowMaxTemp.Val = ini.GetValue("Temp", "LowMax", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowMaxTemp.Ts = ini.GetValue("Temp", "LMTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighMinTemp.Val = ini.GetValue("Temp", "HighMin", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighMinTemp.Ts = ini.GetValue("Temp", "HMTime", cumulus.defaultRecordTS);
				Records.ThisMonth.LowDailyTempRange.Val = ini.GetValue("Temp", "LowRange", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowDailyTempRange.Ts = ini.GetValue("Temp", "LowRangeTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighDailyTempRange.Val = ini.GetValue("Temp", "HighRange", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighDailyTempRange.Ts = ini.GetValue("Temp", "HighRangeTime", cumulus.defaultRecordTS);
				// Pressure
				Records.ThisMonth.LowPress.Val = ini.GetValue("Pressure", "Low", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowPress.Ts = ini.GetValue("Pressure", "LTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighPress.Val = ini.GetValue("Pressure", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighPress.Ts = ini.GetValue("Pressure", "HTime", cumulus.defaultRecordTS);
				// rain rate
				Records.ThisMonth.HighRainRate.Val = ini.GetValue("Rain", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighRainRate.Ts = ini.GetValue("Rain", "HTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HourlyRain.Val = ini.GetValue("Rain", "HourlyHigh", Cumulus.DefaultHiVal);
				Records.ThisMonth.HourlyRain.Ts = ini.GetValue("Rain", "HHourlyTime", cumulus.defaultRecordTS);
				Records.ThisMonth.DailyRain.Val = ini.GetValue("Rain", "DailyHigh", Cumulus.DefaultHiVal);
				Records.ThisMonth.DailyRain.Ts = ini.GetValue("Rain", "HDailyTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighRain24Hours.Val = ini.GetValue("Rain", "24Hour", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighRain24Hours.Ts = ini.GetValue("Rain", "24HourTime", cumulus.defaultRecordTS);
				Records.ThisMonth.LongestDryPeriod.Val = ini.GetValue("Rain", "LongestDryPeriod", 0);
				Records.ThisMonth.LongestDryPeriod.Ts = ini.GetValue("Rain", "LongestDryPeriodTime", cumulus.defaultRecordTS);
				Records.ThisMonth.LongestWetPeriod.Val = ini.GetValue("Rain", "LongestWetPeriod", 0);
				Records.ThisMonth.LongestWetPeriod.Ts = ini.GetValue("Rain", "LongestWetPeriodTime", cumulus.defaultRecordTS);
				// humidity
				Records.ThisMonth.LowHumidity.Val = ini.GetValue("Humidity", "Low", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowHumidity.Ts = ini.GetValue("Humidity", "LTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighHumidity.Val = ini.GetValue("Humidity", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighHumidity.Ts = ini.GetValue("Humidity", "HTime", cumulus.defaultRecordTS);
				// heat index
				Records.ThisMonth.HighHeatIndex.Val = ini.GetValue("HeatIndex", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighHeatIndex.Ts = ini.GetValue("HeatIndex", "HTime", cumulus.defaultRecordTS);
				// App temp
				Records.ThisMonth.LowAppTemp.Val = ini.GetValue("AppTemp", "Low", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowAppTemp.Ts = ini.GetValue("AppTemp", "LTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighAppTemp.Val = ini.GetValue("AppTemp", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighAppTemp.Ts = ini.GetValue("AppTemp", "HTime", cumulus.defaultRecordTS);
				// Dewpoint
				Records.ThisMonth.LowDewPoint.Val = ini.GetValue("Dewpoint", "Low", 999.0);
				Records.ThisMonth.LowDewPoint.Ts = ini.GetValue("Dewpoint", "LTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighDewPoint.Val = ini.GetValue("Dewpoint", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighDewPoint.Ts = ini.GetValue("Dewpoint", "HTime", cumulus.defaultRecordTS);
				// wind chill
				Records.ThisMonth.LowChill.Val = ini.GetValue("WindChill", "Low", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowChill.Ts = ini.GetValue("WindChill", "LTime", cumulus.defaultRecordTS);
				// Feels like temp
				Records.ThisMonth.LowFeelsLike.Val = ini.GetValue("FeelsLike", "Low", Cumulus.DefaultLoVal);
				Records.ThisMonth.LowFeelsLike.Ts = ini.GetValue("FeelsLike", "LTime", cumulus.defaultRecordTS);
				Records.ThisMonth.HighFeelsLike.Val = ini.GetValue("FeelsLike", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighFeelsLike.Ts = ini.GetValue("FeelsLike", "HTime", cumulus.defaultRecordTS);
				// Humidex
				Records.ThisMonth.HighHumidex.Val = ini.GetValue("Humidex", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighHumidex.Ts = ini.GetValue("Humidex", "HTime", cumulus.defaultRecordTS);
				// BGT
				Records.ThisMonth.HighBgt.Val = ini.GetValue("BGT", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighBgt.Ts = ini.GetValue("BGT", "HTime", cumulus.defaultRecordTS);
				// WBGT
				Records.ThisMonth.HighWbgt.Val = ini.GetValue("WBGT", "High", Cumulus.DefaultHiVal);
				Records.ThisMonth.HighWbgt.Ts = ini.GetValue("WBGT", "HTime", cumulus.defaultRecordTS);

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
					ini.SetValue("Wind", "Speed", Records.ThisMonth.HighWind.Val);
					ini.SetValue("Wind", "SpTime", Records.ThisMonth.HighWind.Ts);
					ini.SetValue("Wind", "Gust", Records.ThisMonth.HighGust.Val);
					ini.SetValue("Wind", "Time", Records.ThisMonth.HighGust.Ts);
					ini.SetValue("Wind", "Windrun", Records.ThisMonth.HighWindRun.Val);
					ini.SetValue("Wind", "WindrunTime", Records.ThisMonth.HighWindRun.Ts);
					// Temperature
					ini.SetValue("Temp", "Low", Records.ThisMonth.LowTemp.Val);
					ini.SetValue("Temp", "LTime", Records.ThisMonth.LowTemp.Ts);
					ini.SetValue("Temp", "High", Records.ThisMonth.HighTemp.Val);
					ini.SetValue("Temp", "HTime", Records.ThisMonth.HighTemp.Ts);
					ini.SetValue("Temp", "LowMax", Records.ThisMonth.LowMaxTemp.Val);
					ini.SetValue("Temp", "LMTime", Records.ThisMonth.LowMaxTemp.Ts);
					ini.SetValue("Temp", "HighMin", Records.ThisMonth.HighMinTemp.Val);
					ini.SetValue("Temp", "HMTime", Records.ThisMonth.HighMinTemp.Ts);
					ini.SetValue("Temp", "LowRange", Records.ThisMonth.LowDailyTempRange.Val);
					ini.SetValue("Temp", "LowRangeTime", Records.ThisMonth.LowDailyTempRange.Ts);
					ini.SetValue("Temp", "HighRange", Records.ThisMonth.HighDailyTempRange.Val);
					ini.SetValue("Temp", "HighRangeTime", Records.ThisMonth.HighDailyTempRange.Ts);
					// Pressure
					ini.SetValue("Pressure", "Low", Records.ThisMonth.LowPress.Val);
					ini.SetValue("Pressure", "LTime", Records.ThisMonth.LowPress.Ts);
					ini.SetValue("Pressure", "High", Records.ThisMonth.HighPress.Val);
					ini.SetValue("Pressure", "HTime", Records.ThisMonth.HighPress.Ts);
					// rain
					ini.SetValue("Rain", "High", Records.ThisMonth.HighRainRate.Val);
					ini.SetValue("Rain", "HTime", Records.ThisMonth.HighRainRate.Ts);
					ini.SetValue("Rain", "HourlyHigh", Records.ThisMonth.HourlyRain.Val);
					ini.SetValue("Rain", "HHourlyTime", Records.ThisMonth.HourlyRain.Ts);
					ini.SetValue("Rain", "DailyHigh", Records.ThisMonth.DailyRain.Val);
					ini.SetValue("Rain", "HDailyTime", Records.ThisMonth.DailyRain.Ts);
					ini.SetValue("Rain", "24Hour", Records.ThisMonth.HighRain24Hours.Val);
					ini.SetValue("Rain", "24HourTime", Records.ThisMonth.HighRain24Hours.Ts);
					ini.SetValue("Rain", "LongestDryPeriod", Records.ThisMonth.LongestDryPeriod.Val);
					ini.SetValue("Rain", "LongestDryPeriodTime", Records.ThisMonth.LongestDryPeriod.Ts);
					ini.SetValue("Rain", "LongestWetPeriod", Records.ThisMonth.LongestWetPeriod.Val);
					ini.SetValue("Rain", "LongestWetPeriodTime", Records.ThisMonth.LongestWetPeriod.Ts);
					// humidity
					ini.SetValue("Humidity", "Low", Records.ThisMonth.LowHumidity.Val);
					ini.SetValue("Humidity", "LTime", Records.ThisMonth.LowHumidity.Ts);
					ini.SetValue("Humidity", "High", Records.ThisMonth.HighHumidity.Val);
					ini.SetValue("Humidity", "HTime", Records.ThisMonth.HighHumidity.Ts);
					// heat index
					ini.SetValue("HeatIndex", "High", Records.ThisMonth.HighHeatIndex.Val);
					ini.SetValue("HeatIndex", "HTime", Records.ThisMonth.HighHeatIndex.Ts);
					// App temp
					ini.SetValue("AppTemp", "Low", Records.ThisMonth.LowAppTemp.Val);
					ini.SetValue("AppTemp", "LTime", Records.ThisMonth.LowAppTemp.Ts);
					ini.SetValue("AppTemp", "High", Records.ThisMonth.HighAppTemp.Val);
					ini.SetValue("AppTemp", "HTime", Records.ThisMonth.HighAppTemp.Ts);
					// Dewpoint
					ini.SetValue("Dewpoint", "Low", Records.ThisMonth.LowDewPoint.Val);
					ini.SetValue("Dewpoint", "LTime", Records.ThisMonth.LowDewPoint.Ts);
					ini.SetValue("Dewpoint", "High", Records.ThisMonth.HighDewPoint.Val);
					ini.SetValue("Dewpoint", "HTime", Records.ThisMonth.HighDewPoint.Ts);
					// wind chill
					ini.SetValue("WindChill", "Low", Records.ThisMonth.LowChill.Val);
					ini.SetValue("WindChill", "LTime", Records.ThisMonth.LowChill.Ts);
					// feels like
					ini.SetValue("FeelsLike", "Low", Records.ThisMonth.LowFeelsLike.Val);
					ini.SetValue("FeelsLike", "LTime", Records.ThisMonth.LowFeelsLike.Ts);
					ini.SetValue("FeelsLike", "High", Records.ThisMonth.HighFeelsLike.Val);
					ini.SetValue("FeelsLike", "HTime", Records.ThisMonth.HighFeelsLike.Ts);
					// Humidex
					ini.SetValue("Humidex", "High", Records.ThisMonth.HighHumidex.Val);
					ini.SetValue("Humidex", "HTime", Records.ThisMonth.HighHumidex.Ts);
					// BGT
					ini.SetValue("BGT", "High", Records.ThisMonth.HighBgt.Val);
					ini.SetValue("BGT", "HTime", Records.ThisMonth.HighBgt.Ts);
					// WBGT
					ini.SetValue("WBGT", "High", Records.ThisMonth.HighWbgt.Val);
					ini.SetValue("WBGT", "HTime", Records.ThisMonth.HighWbgt.Ts);

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
