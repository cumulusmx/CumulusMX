using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadAlltimeIniFile()
		{
			var ini = new IniFile(cumulus.AlltimeIniFile);

			AllTime.HighTemp.Val = ini.GetValue("Temperature", "hightempvalue", Cumulus.DefaultHiVal);
			AllTime.HighTemp.Ts = ini.GetValue("Temperature", "hightemptime", cumulus.defaultRecordTS);

			AllTime.LowTemp.Val = ini.GetValue("Temperature", "lowtempvalue", Cumulus.DefaultLoVal);
			AllTime.LowTemp.Ts = ini.GetValue("Temperature", "lowtemptime", cumulus.defaultRecordTS);

			AllTime.LowChill.Val = ini.GetValue("Temperature", "lowchillvalue", Cumulus.DefaultLoVal);
			AllTime.LowChill.Ts = ini.GetValue("Temperature", "lowchilltime", cumulus.defaultRecordTS);

			AllTime.HighMinTemp.Val = ini.GetValue("Temperature", "highmintempvalue", Cumulus.DefaultHiVal);
			AllTime.HighMinTemp.Ts = ini.GetValue("Temperature", "highmintemptime", cumulus.defaultRecordTS);

			AllTime.LowMaxTemp.Val = ini.GetValue("Temperature", "lowmaxtempvalue", Cumulus.DefaultLoVal);
			AllTime.LowMaxTemp.Ts = ini.GetValue("Temperature", "lowmaxtemptime", cumulus.defaultRecordTS);

			AllTime.HighAppTemp.Val = ini.GetValue("Temperature", "highapptempvalue", Cumulus.DefaultHiVal);
			AllTime.HighAppTemp.Ts = ini.GetValue("Temperature", "highapptemptime", cumulus.defaultRecordTS);

			AllTime.LowAppTemp.Val = ini.GetValue("Temperature", "lowapptempvalue", Cumulus.DefaultLoVal);
			AllTime.LowAppTemp.Ts = ini.GetValue("Temperature", "lowapptemptime", cumulus.defaultRecordTS);

			AllTime.HighFeelsLike.Val = ini.GetValue("Temperature", "highfeelslikevalue", Cumulus.DefaultHiVal);
			AllTime.HighFeelsLike.Ts = ini.GetValue("Temperature", "highfeelsliketime", cumulus.defaultRecordTS);

			AllTime.LowFeelsLike.Val = ini.GetValue("Temperature", "lowfeelslikevalue", Cumulus.DefaultLoVal);
			AllTime.LowFeelsLike.Ts = ini.GetValue("Temperature", "lowfeelsliketime", cumulus.defaultRecordTS);

			AllTime.HighHumidex.Val = ini.GetValue("Temperature", "highhumidexvalue", Cumulus.DefaultHiVal);
			AllTime.HighHumidex.Ts = ini.GetValue("Temperature", "highhumidextime", cumulus.defaultRecordTS);

			AllTime.HighHeatIndex.Val = ini.GetValue("Temperature", "highheatindexvalue", Cumulus.DefaultHiVal);
			AllTime.HighHeatIndex.Ts = ini.GetValue("Temperature", "highheatindextime", cumulus.defaultRecordTS);

			AllTime.HighDewPoint.Val = ini.GetValue("Temperature", "highdewpointvalue", Cumulus.DefaultHiVal);
			AllTime.HighDewPoint.Ts = ini.GetValue("Temperature", "highdewpointtime", cumulus.defaultRecordTS);

			AllTime.LowDewPoint.Val = ini.GetValue("Temperature", "lowdewpointvalue", Cumulus.DefaultLoVal);
			AllTime.LowDewPoint.Ts = ini.GetValue("Temperature", "lowdewpointtime", cumulus.defaultRecordTS);

			AllTime.HighDailyTempRange.Val = ini.GetValue("Temperature", "hightemprangevalue", Cumulus.DefaultHiVal);
			AllTime.HighDailyTempRange.Ts = ini.GetValue("Temperature", "hightemprangetime", cumulus.defaultRecordTS);

			AllTime.LowDailyTempRange.Val = ini.GetValue("Temperature", "lowtemprangevalue", Cumulus.DefaultLoVal);
			AllTime.LowDailyTempRange.Ts = ini.GetValue("Temperature", "lowtemprangetime", cumulus.defaultRecordTS);

			AllTime.HighWind.Val = ini.GetValue("Wind", "highwindvalue", Cumulus.DefaultHiVal);
			AllTime.HighWind.Ts = ini.GetValue("Wind", "highwindtime", cumulus.defaultRecordTS);

			AllTime.HighGust.Val = ini.GetValue("Wind", "highgustvalue", Cumulus.DefaultHiVal);
			AllTime.HighGust.Ts = ini.GetValue("Wind", "highgusttime", cumulus.defaultRecordTS);

			AllTime.HighWindRun.Val = ini.GetValue("Wind", "highdailywindrunvalue", Cumulus.DefaultHiVal);
			AllTime.HighWindRun.Ts = ini.GetValue("Wind", "highdailywindruntime", cumulus.defaultRecordTS);

			AllTime.HighRainRate.Val = ini.GetValue("Rain", "highrainratevalue", Cumulus.DefaultHiVal);
			AllTime.HighRainRate.Ts = ini.GetValue("Rain", "highrainratetime", cumulus.defaultRecordTS);

			AllTime.DailyRain.Val = ini.GetValue("Rain", "highdailyrainvalue", Cumulus.DefaultHiVal);
			AllTime.DailyRain.Ts = ini.GetValue("Rain", "highdailyraintime", cumulus.defaultRecordTS);

			AllTime.HourlyRain.Val = ini.GetValue("Rain", "highhourlyrainvalue", Cumulus.DefaultHiVal);
			AllTime.HourlyRain.Ts = ini.GetValue("Rain", "highhourlyraintime", cumulus.defaultRecordTS);

			AllTime.HighRain24Hours.Val = ini.GetValue("Rain", "high24hourrainvalue", Cumulus.DefaultHiVal);
			AllTime.HighRain24Hours.Ts = ini.GetValue("Rain", "high24hourraintime", cumulus.defaultRecordTS);

			AllTime.MonthlyRain.Val = ini.GetValue("Rain", "highmonthlyrainvalue", Cumulus.DefaultHiVal);
			AllTime.MonthlyRain.Ts = ini.GetValue("Rain", "highmonthlyraintime", cumulus.defaultRecordTS);

			AllTime.LongestDryPeriod.Val = ini.GetValue("Rain", "longestdryperiodvalue", 0);
			AllTime.LongestDryPeriod.Ts = ini.GetValue("Rain", "longestdryperiodtime", cumulus.defaultRecordTS);

			AllTime.LongestWetPeriod.Val = ini.GetValue("Rain", "longestwetperiodvalue", 0);
			AllTime.LongestWetPeriod.Ts = ini.GetValue("Rain", "longestwetperiodtime", cumulus.defaultRecordTS);

			AllTime.HighPress.Val = ini.GetValue("Pressure", "highpressurevalue", Cumulus.DefaultHiVal);
			AllTime.HighPress.Ts = ini.GetValue("Pressure", "highpressuretime", cumulus.defaultRecordTS);

			AllTime.LowPress.Val = ini.GetValue("Pressure", "lowpressurevalue", Cumulus.DefaultLoVal);
			AllTime.LowPress.Ts = ini.GetValue("Pressure", "lowpressuretime", cumulus.defaultRecordTS);

			AllTime.HighHumidity.Val = ini.GetValue("Humidity", "highhumidityvalue", Cumulus.DefaultHiVal);
			AllTime.HighHumidity.Ts = ini.GetValue("Humidity", "highhumiditytime", cumulus.defaultRecordTS);

			AllTime.LowHumidity.Val = ini.GetValue("Humidity", "lowhumidityvalue", Cumulus.DefaultLoVal);
			AllTime.LowHumidity.Ts = ini.GetValue("Humidity", "lowhumiditytime", cumulus.defaultRecordTS);

			AllTime.HighBgt.Val = ini.GetValue("Temperature", "highbgtvalue", Cumulus.DefaultHiVal);
			AllTime.HighBgt.Ts = ini.GetValue("Temperature", "highbgttime", cumulus.defaultRecordTS);

			AllTime.HighWbgt.Val = ini.GetValue("Temperature", "highwbgtvalue", Cumulus.DefaultHiVal);
			AllTime.HighWbgt.Ts = ini.GetValue("Temperature", "highwbgttime", cumulus.defaultRecordTS);

			cumulus.LogMessage("Alltime.ini file read");
		}

		public void WriteAlltimeIniFile()
		{
			try
			{
				var ini = new IniFile(cumulus.AlltimeIniFile);

				ini.SetValue("Temperature", "hightempvalue", AllTime.HighTemp.Val);
				ini.SetValue("Temperature", "hightemptime", AllTime.HighTemp.Ts);
				ini.SetValue("Temperature", "lowtempvalue", AllTime.LowTemp.Val);
				ini.SetValue("Temperature", "lowtemptime", AllTime.LowTemp.Ts);
				ini.SetValue("Temperature", "lowchillvalue", AllTime.LowChill.Val);
				ini.SetValue("Temperature", "lowchilltime", AllTime.LowChill.Ts);
				ini.SetValue("Temperature", "highmintempvalue", AllTime.HighMinTemp.Val);
				ini.SetValue("Temperature", "highmintemptime", AllTime.HighMinTemp.Ts);
				ini.SetValue("Temperature", "lowmaxtempvalue", AllTime.LowMaxTemp.Val);
				ini.SetValue("Temperature", "lowmaxtemptime", AllTime.LowMaxTemp.Ts);
				ini.SetValue("Temperature", "highapptempvalue", AllTime.HighAppTemp.Val);
				ini.SetValue("Temperature", "highapptemptime", AllTime.HighAppTemp.Ts);
				ini.SetValue("Temperature", "lowapptempvalue", AllTime.LowAppTemp.Val);
				ini.SetValue("Temperature", "lowapptemptime", AllTime.LowAppTemp.Ts);
				ini.SetValue("Temperature", "highfeelslikevalue", AllTime.HighFeelsLike.Val);
				ini.SetValue("Temperature", "highfeelsliketime", AllTime.HighFeelsLike.Ts);
				ini.SetValue("Temperature", "lowfeelslikevalue", AllTime.LowFeelsLike.Val);
				ini.SetValue("Temperature", "lowfeelsliketime", AllTime.LowFeelsLike.Ts);
				ini.SetValue("Temperature", "highhumidexvalue", AllTime.HighHumidex.Val);
				ini.SetValue("Temperature", "highhumidextime", AllTime.HighHumidex.Ts);
				ini.SetValue("Temperature", "highheatindexvalue", AllTime.HighHeatIndex.Val);
				ini.SetValue("Temperature", "highheatindextime", AllTime.HighHeatIndex.Ts);
				ini.SetValue("Temperature", "highdewpointvalue", AllTime.HighDewPoint.Val);
				ini.SetValue("Temperature", "highdewpointtime", AllTime.HighDewPoint.Ts);
				ini.SetValue("Temperature", "lowdewpointvalue", AllTime.LowDewPoint.Val);
				ini.SetValue("Temperature", "lowdewpointtime", AllTime.LowDewPoint.Ts);
				ini.SetValue("Temperature", "hightemprangevalue", AllTime.HighDailyTempRange.Val);
				ini.SetValue("Temperature", "hightemprangetime", AllTime.HighDailyTempRange.Ts);
				ini.SetValue("Temperature", "lowtemprangevalue", AllTime.LowDailyTempRange.Val);
				ini.SetValue("Temperature", "lowtemprangetime", AllTime.LowDailyTempRange.Ts);
				ini.SetValue("Temperature", "highbgtvalue", AllTime.HighBgt.Val);
				ini.SetValue("Temperature", "highbgttime", AllTime.HighBgt.Ts);
				ini.SetValue("Temperature", "highwbgtvalue", AllTime.HighWbgt.Val);
				ini.SetValue("Temperature", "highwbgttime", AllTime.HighWbgt.Ts);
				ini.SetValue("Wind", "highwindvalue", AllTime.HighWind.Val);
				ini.SetValue("Wind", "highwindtime", AllTime.HighWind.Ts);
				ini.SetValue("Wind", "highgustvalue", AllTime.HighGust.Val);
				ini.SetValue("Wind", "highgusttime", AllTime.HighGust.Ts);
				ini.SetValue("Wind", "highdailywindrunvalue", AllTime.HighWindRun.Val);
				ini.SetValue("Wind", "highdailywindruntime", AllTime.HighWindRun.Ts);
				ini.SetValue("Rain", "highrainratevalue", AllTime.HighRainRate.Val);
				ini.SetValue("Rain", "highrainratetime", AllTime.HighRainRate.Ts);
				ini.SetValue("Rain", "highdailyrainvalue", AllTime.DailyRain.Val);
				ini.SetValue("Rain", "highdailyraintime", AllTime.DailyRain.Ts);
				ini.SetValue("Rain", "highhourlyrainvalue", AllTime.HourlyRain.Val);
				ini.SetValue("Rain", "highhourlyraintime", AllTime.HourlyRain.Ts);
				ini.SetValue("Rain", "high24hourrainvalue", AllTime.HighRain24Hours.Val);
				ini.SetValue("Rain", "high24hourraintime", AllTime.HighRain24Hours.Ts);
				ini.SetValue("Rain", "highmonthlyrainvalue", AllTime.MonthlyRain.Val);
				ini.SetValue("Rain", "highmonthlyraintime", AllTime.MonthlyRain.Ts);
				ini.SetValue("Rain", "longestdryperiodvalue", AllTime.LongestDryPeriod.Val);
				ini.SetValue("Rain", "longestdryperiodtime", AllTime.LongestDryPeriod.Ts);
				ini.SetValue("Rain", "longestwetperiodvalue", AllTime.LongestWetPeriod.Val);
				ini.SetValue("Rain", "longestwetperiodtime", AllTime.LongestWetPeriod.Ts);
				ini.SetValue("Pressure", "highpressurevalue", AllTime.HighPress.Val);
				ini.SetValue("Pressure", "highpressuretime", AllTime.HighPress.Ts);
				ini.SetValue("Pressure", "lowpressurevalue", AllTime.LowPress.Val);
				ini.SetValue("Pressure", "lowpressuretime", AllTime.LowPress.Ts);
				ini.SetValue("Humidity", "highhumidityvalue", AllTime.HighHumidity.Val);
				ini.SetValue("Humidity", "highhumiditytime", AllTime.HighHumidity.Ts);
				ini.SetValue("Humidity", "lowhumidityvalue", AllTime.LowHumidity.Val);
				ini.SetValue("Humidity", "lowhumiditytime", AllTime.LowHumidity.Ts);

				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error writing alltime.ini file: " + ex.Message);
			}
		}
	}
}
