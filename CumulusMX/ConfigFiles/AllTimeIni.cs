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

			Records.AllTime.HighTemp.Val = ini.GetValue("Temperature", "hightempvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighTemp.Ts = ini.GetValue("Temperature", "hightemptime", cumulus.defaultRecordTS);

			Records.AllTime.LowTemp.Val = ini.GetValue("Temperature", "lowtempvalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowTemp.Ts = ini.GetValue("Temperature", "lowtemptime", cumulus.defaultRecordTS);

			Records.AllTime.LowChill.Val = ini.GetValue("Temperature", "lowchillvalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowChill.Ts = ini.GetValue("Temperature", "lowchilltime", cumulus.defaultRecordTS);

			Records.AllTime.HighMinTemp.Val = ini.GetValue("Temperature", "highmintempvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighMinTemp.Ts = ini.GetValue("Temperature", "highmintemptime", cumulus.defaultRecordTS);

			Records.AllTime.LowMaxTemp.Val = ini.GetValue("Temperature", "lowmaxtempvalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowMaxTemp.Ts = ini.GetValue("Temperature", "lowmaxtemptime", cumulus.defaultRecordTS);

			Records.AllTime.HighAppTemp.Val = ini.GetValue("Temperature", "highapptempvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighAppTemp.Ts = ini.GetValue("Temperature", "highapptemptime", cumulus.defaultRecordTS);

			Records.AllTime.LowAppTemp.Val = ini.GetValue("Temperature", "lowapptempvalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowAppTemp.Ts = ini.GetValue("Temperature", "lowapptemptime", cumulus.defaultRecordTS);

			Records.AllTime.HighFeelsLike.Val = ini.GetValue("Temperature", "highfeelslikevalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighFeelsLike.Ts = ini.GetValue("Temperature", "highfeelsliketime", cumulus.defaultRecordTS);

			Records.AllTime.LowFeelsLike.Val = ini.GetValue("Temperature", "lowfeelslikevalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowFeelsLike.Ts = ini.GetValue("Temperature", "lowfeelsliketime", cumulus.defaultRecordTS);

			Records.AllTime.HighHumidex.Val = ini.GetValue("Temperature", "highhumidexvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighHumidex.Ts = ini.GetValue("Temperature", "highhumidextime", cumulus.defaultRecordTS);

			Records.AllTime.HighHeatIndex.Val = ini.GetValue("Temperature", "highheatindexvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighHeatIndex.Ts = ini.GetValue("Temperature", "highheatindextime", cumulus.defaultRecordTS);

			Records.AllTime.HighDewPoint.Val = ini.GetValue("Temperature", "highdewpointvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighDewPoint.Ts = ini.GetValue("Temperature", "highdewpointtime", cumulus.defaultRecordTS);

			Records.AllTime.LowDewPoint.Val = ini.GetValue("Temperature", "lowdewpointvalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowDewPoint.Ts = ini.GetValue("Temperature", "lowdewpointtime", cumulus.defaultRecordTS);

			Records.AllTime.HighDailyTempRange.Val = ini.GetValue("Temperature", "hightemprangevalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighDailyTempRange.Ts = ini.GetValue("Temperature", "hightemprangetime", cumulus.defaultRecordTS);

			Records.AllTime.LowDailyTempRange.Val = ini.GetValue("Temperature", "lowtemprangevalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowDailyTempRange.Ts = ini.GetValue("Temperature", "lowtemprangetime", cumulus.defaultRecordTS);

			Records.AllTime.HighWind.Val = ini.GetValue("Wind", "highwindvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighWind.Ts = ini.GetValue("Wind", "highwindtime", cumulus.defaultRecordTS);

			Records.AllTime.HighGust.Val = ini.GetValue("Wind", "highgustvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighGust.Ts = ini.GetValue("Wind", "highgusttime", cumulus.defaultRecordTS);

			Records.AllTime.HighWindRun.Val = ini.GetValue("Wind", "highdailywindrunvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighWindRun.Ts = ini.GetValue("Wind", "highdailywindruntime", cumulus.defaultRecordTS);

			Records.AllTime.HighRainRate.Val = ini.GetValue("Rain", "highrainratevalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighRainRate.Ts = ini.GetValue("Rain", "highrainratetime", cumulus.defaultRecordTS);

			Records.AllTime.DailyRain.Val = ini.GetValue("Rain", "highdailyrainvalue", Cumulus.DefaultHiVal);
			Records.AllTime.DailyRain.Ts = ini.GetValue("Rain", "highdailyraintime", cumulus.defaultRecordTS);

			Records.AllTime.HourlyRain.Val = ini.GetValue("Rain", "highhourlyrainvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HourlyRain.Ts = ini.GetValue("Rain", "highhourlyraintime", cumulus.defaultRecordTS);

			Records.AllTime.HighRain24Hours.Val = ini.GetValue("Rain", "high24hourrainvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighRain24Hours.Ts = ini.GetValue("Rain", "high24hourraintime", cumulus.defaultRecordTS);

			Records.AllTime.MonthlyRain.Val = ini.GetValue("Rain", "highmonthlyrainvalue", Cumulus.DefaultHiVal);
			Records.AllTime.MonthlyRain.Ts = ini.GetValue("Rain", "highmonthlyraintime", cumulus.defaultRecordTS);

			Records.AllTime.LongestDryPeriod.Val = ini.GetValue("Rain", "longestdryperiodvalue", 0);
			Records.AllTime.LongestDryPeriod.Ts = ini.GetValue("Rain", "longestdryperiodtime", cumulus.defaultRecordTS);

			Records.AllTime.LongestWetPeriod.Val = ini.GetValue("Rain", "longestwetperiodvalue", 0);
			Records.AllTime.LongestWetPeriod.Ts = ini.GetValue("Rain", "longestwetperiodtime", cumulus.defaultRecordTS);

			Records.AllTime.HighPress.Val = ini.GetValue("Pressure", "highpressurevalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighPress.Ts = ini.GetValue("Pressure", "highpressuretime", cumulus.defaultRecordTS);

			Records.AllTime.LowPress.Val = ini.GetValue("Pressure", "lowpressurevalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowPress.Ts = ini.GetValue("Pressure", "lowpressuretime", cumulus.defaultRecordTS);

			Records.AllTime.HighHumidity.Val = ini.GetValue("Humidity", "highhumidityvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighHumidity.Ts = ini.GetValue("Humidity", "highhumiditytime", cumulus.defaultRecordTS);

			Records.AllTime.LowHumidity.Val = ini.GetValue("Humidity", "lowhumidityvalue", Cumulus.DefaultLoVal);
			Records.AllTime.LowHumidity.Ts = ini.GetValue("Humidity", "lowhumiditytime", cumulus.defaultRecordTS);

			Records.AllTime.HighBgt.Val = ini.GetValue("Temperature", "highbgtvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighBgt.Ts = ini.GetValue("Temperature", "highbgttime", cumulus.defaultRecordTS);

			Records.AllTime.HighWbgt.Val = ini.GetValue("Temperature", "highwbgtvalue", Cumulus.DefaultHiVal);
			Records.AllTime.HighWbgt.Ts = ini.GetValue("Temperature", "highwbgttime", cumulus.defaultRecordTS);

			cumulus.LogMessage("Alltime.ini file read");
		}

		public void WriteAlltimeIniFile()
		{
			try
			{
				var ini = new IniFile(cumulus.AlltimeIniFile);

				ini.SetValue("Temperature", "hightempvalue", Records.AllTime.HighTemp.Val);
				ini.SetValue("Temperature", "hightemptime", Records.AllTime.HighTemp.Ts);
				ini.SetValue("Temperature", "lowtempvalue", Records.AllTime.LowTemp.Val);
				ini.SetValue("Temperature", "lowtemptime", Records.AllTime.LowTemp.Ts);
				ini.SetValue("Temperature", "lowchillvalue", Records.AllTime.LowChill.Val);
				ini.SetValue("Temperature", "lowchilltime", Records.AllTime.LowChill.Ts);
				ini.SetValue("Temperature", "highmintempvalue", Records.AllTime.HighMinTemp.Val);
				ini.SetValue("Temperature", "highmintemptime", Records.AllTime.HighMinTemp.Ts);
				ini.SetValue("Temperature", "lowmaxtempvalue", Records.AllTime.LowMaxTemp.Val);
				ini.SetValue("Temperature", "lowmaxtemptime", Records.AllTime.LowMaxTemp.Ts);
				ini.SetValue("Temperature", "highapptempvalue", Records.AllTime.HighAppTemp.Val);
				ini.SetValue("Temperature", "highapptemptime", Records.AllTime.HighAppTemp.Ts);
				ini.SetValue("Temperature", "lowapptempvalue", Records.AllTime.LowAppTemp.Val);
				ini.SetValue("Temperature", "lowapptemptime", Records.AllTime.LowAppTemp.Ts);
				ini.SetValue("Temperature", "highfeelslikevalue", Records.AllTime.HighFeelsLike.Val);
				ini.SetValue("Temperature", "highfeelsliketime", Records.AllTime.HighFeelsLike.Ts);
				ini.SetValue("Temperature", "lowfeelslikevalue", Records.AllTime.LowFeelsLike.Val);
				ini.SetValue("Temperature", "lowfeelsliketime", Records.AllTime.LowFeelsLike.Ts);
				ini.SetValue("Temperature", "highhumidexvalue", Records.AllTime.HighHumidex.Val);
				ini.SetValue("Temperature", "highhumidextime", Records.AllTime.HighHumidex.Ts);
				ini.SetValue("Temperature", "highheatindexvalue", Records.AllTime.HighHeatIndex.Val);
				ini.SetValue("Temperature", "highheatindextime", Records.AllTime.HighHeatIndex.Ts);
				ini.SetValue("Temperature", "highdewpointvalue", Records.AllTime.HighDewPoint.Val);
				ini.SetValue("Temperature", "highdewpointtime", Records.AllTime.HighDewPoint.Ts);
				ini.SetValue("Temperature", "lowdewpointvalue", Records.AllTime.LowDewPoint.Val);
				ini.SetValue("Temperature", "lowdewpointtime", Records.AllTime.LowDewPoint.Ts);
				ini.SetValue("Temperature", "hightemprangevalue", Records.AllTime.HighDailyTempRange.Val);
				ini.SetValue("Temperature", "hightemprangetime", Records.AllTime.HighDailyTempRange.Ts);
				ini.SetValue("Temperature", "lowtemprangevalue", Records.AllTime.LowDailyTempRange.Val);
				ini.SetValue("Temperature", "lowtemprangetime", Records.AllTime.LowDailyTempRange.Ts);
				ini.SetValue("Temperature", "highbgtvalue", Records.AllTime.HighBgt.Val);
				ini.SetValue("Temperature", "highbgttime", Records.AllTime.HighBgt.Ts);
				ini.SetValue("Temperature", "highwbgtvalue", Records.AllTime.HighWbgt.Val);
				ini.SetValue("Temperature", "highwbgttime", Records.AllTime.HighWbgt.Ts);
				ini.SetValue("Wind", "highwindvalue", Records.AllTime.HighWind.Val);
				ini.SetValue("Wind", "highwindtime", Records.AllTime.HighWind.Ts);
				ini.SetValue("Wind", "highgustvalue", Records.AllTime.HighGust.Val);
				ini.SetValue("Wind", "highgusttime", Records.AllTime.HighGust.Ts);
				ini.SetValue("Wind", "highdailywindrunvalue", Records.AllTime.HighWindRun.Val);
				ini.SetValue("Wind", "highdailywindruntime", Records.AllTime.HighWindRun.Ts);
				ini.SetValue("Rain", "highrainratevalue", Records.AllTime.HighRainRate.Val);
				ini.SetValue("Rain", "highrainratetime", Records.AllTime.HighRainRate.Ts);
				ini.SetValue("Rain", "highdailyrainvalue", Records.AllTime.DailyRain.Val);
				ini.SetValue("Rain", "highdailyraintime", Records.AllTime.DailyRain.Ts);
				ini.SetValue("Rain", "highhourlyrainvalue", Records.AllTime.HourlyRain.Val);
				ini.SetValue("Rain", "highhourlyraintime", Records.AllTime.HourlyRain.Ts);
				ini.SetValue("Rain", "high24hourrainvalue", Records.AllTime.HighRain24Hours.Val);
				ini.SetValue("Rain", "high24hourraintime", Records.AllTime.HighRain24Hours.Ts);
				ini.SetValue("Rain", "highmonthlyrainvalue", Records.AllTime.MonthlyRain.Val);
				ini.SetValue("Rain", "highmonthlyraintime", Records.AllTime.MonthlyRain.Ts);
				ini.SetValue("Rain", "longestdryperiodvalue", Records.AllTime.LongestDryPeriod.Val);
				ini.SetValue("Rain", "longestdryperiodtime", Records.AllTime.LongestDryPeriod.Ts);
				ini.SetValue("Rain", "longestwetperiodvalue", Records.AllTime.LongestWetPeriod.Val);
				ini.SetValue("Rain", "longestwetperiodtime", Records.AllTime.LongestWetPeriod.Ts);
				ini.SetValue("Pressure", "highpressurevalue", Records.AllTime.HighPress.Val);
				ini.SetValue("Pressure", "highpressuretime", Records.AllTime.HighPress.Ts);
				ini.SetValue("Pressure", "lowpressurevalue", Records.AllTime.LowPress.Val);
				ini.SetValue("Pressure", "lowpressuretime", Records.AllTime.LowPress.Ts);
				ini.SetValue("Humidity", "highhumidityvalue", Records.AllTime.HighHumidity.Val);
				ini.SetValue("Humidity", "highhumiditytime", Records.AllTime.HighHumidity.Ts);
				ini.SetValue("Humidity", "lowhumidityvalue", Records.AllTime.LowHumidity.Val);
				ini.SetValue("Humidity", "lowhumiditytime", Records.AllTime.LowHumidity.Ts);

				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error writing Records.AllTime.ini file: " + ex.Message);
			}
		}
	}
}
