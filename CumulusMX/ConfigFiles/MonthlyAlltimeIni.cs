using System;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadMonthlyAlltimeIniFile()
		{
			var ini = new IniFile(cumulus.MonthlyAlltimeIniFile);
			for (var month = 1; month <= 12; month++)
			{
				var monthstr = month.ToString("D2");

				MonthlyRecs[month].HighTemp.Val = ini.GetValue("Temperature" + monthstr, "hightempvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighTemp.Ts = ini.GetValue("Temperature" + monthstr, "hightemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowTemp.Val = ini.GetValue("Temperature" + monthstr, "lowtempvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowTemp.Ts = ini.GetValue("Temperature" + monthstr, "lowtemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowChill.Val = ini.GetValue("Temperature" + monthstr, "lowchillvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowChill.Ts = ini.GetValue("Temperature" + monthstr, "lowchilltime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighMinTemp.Val = ini.GetValue("Temperature" + monthstr, "highmintempvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighMinTemp.Ts = ini.GetValue("Temperature" + monthstr, "highmintemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowMaxTemp.Val = ini.GetValue("Temperature" + monthstr, "lowmaxtempvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowMaxTemp.Ts = ini.GetValue("Temperature" + monthstr, "lowmaxtemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighAppTemp.Val = ini.GetValue("Temperature" + monthstr, "highapptempvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighAppTemp.Ts = ini.GetValue("Temperature" + monthstr, "highapptemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowAppTemp.Val = ini.GetValue("Temperature" + monthstr, "lowapptempvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowAppTemp.Ts = ini.GetValue("Temperature" + monthstr, "lowapptemptime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighFeelsLike.Val = ini.GetValue("Temperature" + monthstr, "highfeelslikevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighFeelsLike.Ts = ini.GetValue("Temperature" + monthstr, "highfeelsliketime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowFeelsLike.Val = ini.GetValue("Temperature" + monthstr, "lowfeelslikevalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowFeelsLike.Ts = ini.GetValue("Temperature" + monthstr, "lowfeelsliketime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighHumidex.Val = ini.GetValue("Temperature" + monthstr, "highhumidexvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighHumidex.Ts = ini.GetValue("Temperature" + monthstr, "highhumidextime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighHeatIndex.Val = ini.GetValue("Temperature" + monthstr, "highheatindexvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighHeatIndex.Ts = ini.GetValue("Temperature" + monthstr, "highheatindextime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighDewPoint.Val = ini.GetValue("Temperature" + monthstr, "highdewpointvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighDewPoint.Ts = ini.GetValue("Temperature" + monthstr, "highdewpointtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowDewPoint.Val = ini.GetValue("Temperature" + monthstr, "lowdewpointvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowDewPoint.Ts = ini.GetValue("Temperature" + monthstr, "lowdewpointtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighDailyTempRange.Val = ini.GetValue("Temperature" + monthstr, "hightemprangevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighDailyTempRange.Ts = ini.GetValue("Temperature" + monthstr, "hightemprangetime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowDailyTempRange.Val = ini.GetValue("Temperature" + monthstr, "lowtemprangevalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowDailyTempRange.Ts = ini.GetValue("Temperature" + monthstr, "lowtemprangetime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighWind.Val = ini.GetValue("Wind" + monthstr, "highwindvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighWind.Ts = ini.GetValue("Wind" + monthstr, "highwindtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighGust.Val = ini.GetValue("Wind" + monthstr, "highgustvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighGust.Ts = ini.GetValue("Wind" + monthstr, "highgusttime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighWindRun.Val = ini.GetValue("Wind" + monthstr, "highdailywindrunvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighWindRun.Ts = ini.GetValue("Wind" + monthstr, "highdailywindruntime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighRainRate.Val = ini.GetValue("Rain" + monthstr, "highrainratevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighRainRate.Ts = ini.GetValue("Rain" + monthstr, "highrainratetime", cumulus.defaultRecordTS);

				MonthlyRecs[month].DailyRain.Val = ini.GetValue("Rain" + monthstr, "highdailyrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].DailyRain.Ts = ini.GetValue("Rain" + monthstr, "highdailyraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HourlyRain.Val = ini.GetValue("Rain" + monthstr, "highhourlyrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HourlyRain.Ts = ini.GetValue("Rain" + monthstr, "highhourlyraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighRain24Hours.Val = ini.GetValue("Rain" + monthstr, "high24hourrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighRain24Hours.Ts = ini.GetValue("Rain" + monthstr, "high24hourraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].MonthlyRain.Val = ini.GetValue("Rain" + monthstr, "highmonthlyrainvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].MonthlyRain.Ts = ini.GetValue("Rain" + monthstr, "highmonthlyraintime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LongestDryPeriod.Val = ini.GetValue("Rain" + monthstr, "longestdryperiodvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].LongestDryPeriod.Ts = ini.GetValue("Rain" + monthstr, "longestdryperiodtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LongestWetPeriod.Val = ini.GetValue("Rain" + monthstr, "longestwetperiodvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].LongestWetPeriod.Ts = ini.GetValue("Rain" + monthstr, "longestwetperiodtime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighPress.Val = ini.GetValue("Pressure" + monthstr, "highpressurevalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighPress.Ts = ini.GetValue("Pressure" + monthstr, "highpressuretime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowPress.Val = ini.GetValue("Pressure" + monthstr, "lowpressurevalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowPress.Ts = ini.GetValue("Pressure" + monthstr, "lowpressuretime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighHumidity.Val = ini.GetValue("Humidity" + monthstr, "highhumidityvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighHumidity.Ts = ini.GetValue("Humidity" + monthstr, "highhumiditytime", cumulus.defaultRecordTS);

				MonthlyRecs[month].LowHumidity.Val = ini.GetValue("Humidity" + monthstr, "lowhumidityvalue", Cumulus.DefaultLoVal);
				MonthlyRecs[month].LowHumidity.Ts = ini.GetValue("Humidity" + monthstr, "lowhumiditytime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighBgt.Val = ini.GetValue("Bgt" + monthstr, "highbgtvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighBgt.Ts = ini.GetValue("Bgt" + monthstr, "highbgttime", cumulus.defaultRecordTS);

				MonthlyRecs[month].HighWbgt.Val = ini.GetValue("Wbgt" + monthstr, "highwbgtvalue", Cumulus.DefaultHiVal);
				MonthlyRecs[month].HighWbgt.Ts = ini.GetValue("Wbgt" + monthstr, "highwbgttime", cumulus.defaultRecordTS);
			}

			cumulus.LogMessage("MonthlyAlltime.ini file read");
		}

		public void WriteMonthlyAlltimeIniFile()
		{
			try
			{
				var ini = new IniFile(cumulus.MonthlyAlltimeIniFile);
				for (var month = 1; month <= 12; month++)
				{
					var monthstr = month.ToString("D2");

					ini.SetValue("Temperature" + monthstr, "hightempvalue", MonthlyRecs[month].HighTemp.Val);
					ini.SetValue("Temperature" + monthstr, "hightemptime", MonthlyRecs[month].HighTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowtempvalue", MonthlyRecs[month].LowTemp.Val);
					ini.SetValue("Temperature" + monthstr, "lowtemptime", MonthlyRecs[month].LowTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowchillvalue", MonthlyRecs[month].LowChill.Val);
					ini.SetValue("Temperature" + monthstr, "lowchilltime", MonthlyRecs[month].LowChill.Ts);
					ini.SetValue("Temperature" + monthstr, "highmintempvalue", MonthlyRecs[month].HighMinTemp.Val);
					ini.SetValue("Temperature" + monthstr, "highmintemptime", MonthlyRecs[month].HighMinTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowmaxtempvalue", MonthlyRecs[month].LowMaxTemp.Val);
					ini.SetValue("Temperature" + monthstr, "lowmaxtemptime", MonthlyRecs[month].LowMaxTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "highapptempvalue", MonthlyRecs[month].HighAppTemp.Val);
					ini.SetValue("Temperature" + monthstr, "highapptemptime", MonthlyRecs[month].HighAppTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "lowapptempvalue", MonthlyRecs[month].LowAppTemp.Val);
					ini.SetValue("Temperature" + monthstr, "lowapptemptime", MonthlyRecs[month].LowAppTemp.Ts);
					ini.SetValue("Temperature" + monthstr, "highfeelslikevalue", MonthlyRecs[month].HighFeelsLike.Val);
					ini.SetValue("Temperature" + monthstr, "highfeelsliketime", MonthlyRecs[month].HighFeelsLike.Ts);
					ini.SetValue("Temperature" + monthstr, "lowfeelslikevalue", MonthlyRecs[month].LowFeelsLike.Val);
					ini.SetValue("Temperature" + monthstr, "lowfeelsliketime", MonthlyRecs[month].LowFeelsLike.Ts);
					ini.SetValue("Temperature" + monthstr, "highhumidexvalue", MonthlyRecs[month].HighHumidex.Val);
					ini.SetValue("Temperature" + monthstr, "highhumidextime", MonthlyRecs[month].HighHumidex.Ts);
					ini.SetValue("Temperature" + monthstr, "highheatindexvalue", MonthlyRecs[month].HighHeatIndex.Val);
					ini.SetValue("Temperature" + monthstr, "highheatindextime", MonthlyRecs[month].HighHeatIndex.Ts);
					ini.SetValue("Temperature" + monthstr, "highdewpointvalue", MonthlyRecs[month].HighDewPoint.Val);
					ini.SetValue("Temperature" + monthstr, "highdewpointtime", MonthlyRecs[month].HighDewPoint.Ts);
					ini.SetValue("Temperature" + monthstr, "lowdewpointvalue", MonthlyRecs[month].LowDewPoint.Val);
					ini.SetValue("Temperature" + monthstr, "lowdewpointtime", MonthlyRecs[month].LowDewPoint.Ts);
					ini.SetValue("Temperature" + monthstr, "hightemprangevalue", MonthlyRecs[month].HighDailyTempRange.Val);
					ini.SetValue("Temperature" + monthstr, "hightemprangetime", MonthlyRecs[month].HighDailyTempRange.Ts);
					ini.SetValue("Temperature" + monthstr, "lowtemprangevalue", MonthlyRecs[month].LowDailyTempRange.Val);
					ini.SetValue("Temperature" + monthstr, "lowtemprangetime", MonthlyRecs[month].LowDailyTempRange.Ts);
					ini.SetValue("Temperature" + monthstr, "highbgtvalue", MonthlyRecs[month].HighBgt.Val);
					ini.SetValue("Temperature" + monthstr, "highbgttime", MonthlyRecs[month].HighBgt.Ts);
					ini.SetValue("Temperature" + monthstr, "highwbgtvalue", MonthlyRecs[month].HighWbgt.Val);
					ini.SetValue("Temperature" + monthstr, "highwbgttime", MonthlyRecs[month].HighWbgt.Ts);

					ini.SetValue("Wind" + monthstr, "highwindvalue", MonthlyRecs[month].HighWind.Val);
					ini.SetValue("Wind" + monthstr, "highwindtime", MonthlyRecs[month].HighWind.Ts);
					ini.SetValue("Wind" + monthstr, "highgustvalue", MonthlyRecs[month].HighGust.Val);
					ini.SetValue("Wind" + monthstr, "highgusttime", MonthlyRecs[month].HighGust.Ts);
					ini.SetValue("Wind" + monthstr, "highdailywindrunvalue", MonthlyRecs[month].HighWindRun.Val);
					ini.SetValue("Wind" + monthstr, "highdailywindruntime", MonthlyRecs[month].HighWindRun.Ts);
					ini.SetValue("Rain" + monthstr, "highrainratevalue", MonthlyRecs[month].HighRainRate.Val);
					ini.SetValue("Rain" + monthstr, "highrainratetime", MonthlyRecs[month].HighRainRate.Ts);
					ini.SetValue("Rain" + monthstr, "highdailyrainvalue", MonthlyRecs[month].DailyRain.Val);
					ini.SetValue("Rain" + monthstr, "highdailyraintime", MonthlyRecs[month].DailyRain.Ts);
					ini.SetValue("Rain" + monthstr, "highhourlyrainvalue", MonthlyRecs[month].HourlyRain.Val);
					ini.SetValue("Rain" + monthstr, "highhourlyraintime", MonthlyRecs[month].HourlyRain.Ts);
					ini.SetValue("Rain" + monthstr, "high24hourrainvalue", MonthlyRecs[month].HighRain24Hours.Val);
					ini.SetValue("Rain" + monthstr, "high24hourraintime", MonthlyRecs[month].HighRain24Hours.Ts);
					ini.SetValue("Rain" + monthstr, "highmonthlyrainvalue", MonthlyRecs[month].MonthlyRain.Val);
					ini.SetValue("Rain" + monthstr, "highmonthlyraintime", MonthlyRecs[month].MonthlyRain.Ts);
					ini.SetValue("Rain" + monthstr, "longestdryperiodvalue", MonthlyRecs[month].LongestDryPeriod.Val);
					ini.SetValue("Rain" + monthstr, "longestdryperiodtime", MonthlyRecs[month].LongestDryPeriod.Ts);
					ini.SetValue("Rain" + monthstr, "longestwetperiodvalue", MonthlyRecs[month].LongestWetPeriod.Val);
					ini.SetValue("Rain" + monthstr, "longestwetperiodtime", MonthlyRecs[month].LongestWetPeriod.Ts);
					ini.SetValue("Pressure" + monthstr, "highpressurevalue", MonthlyRecs[month].HighPress.Val);
					ini.SetValue("Pressure" + monthstr, "highpressuretime", MonthlyRecs[month].HighPress.Ts);
					ini.SetValue("Pressure" + monthstr, "lowpressurevalue", MonthlyRecs[month].LowPress.Val);
					ini.SetValue("Pressure" + monthstr, "lowpressuretime", MonthlyRecs[month].LowPress.Ts);
					ini.SetValue("Humidity" + monthstr, "highhumidityvalue", MonthlyRecs[month].HighHumidity.Val);
					ini.SetValue("Humidity" + monthstr, "highhumiditytime", MonthlyRecs[month].HighHumidity.Ts);
					ini.SetValue("Humidity" + monthstr, "lowhumidityvalue", MonthlyRecs[month].LowHumidity.Val);
					ini.SetValue("Humidity" + monthstr, "lowhumiditytime", MonthlyRecs[month].LowHumidity.Ts);
				}
				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error writing MonthlyAlltime.ini file: " + ex.Message);
			}
		}
	}
}
