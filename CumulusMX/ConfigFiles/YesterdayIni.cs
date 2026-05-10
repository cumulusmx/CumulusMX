using System;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadYesterdayFile()
		{
			var ini = new IniFile(cumulus.YesterdayFile);

			// Wind
			DailyHighLow.Yest.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			DailyHighLow.Yest.HighWindTime = ini.GetValue("Wind", "SpTime", DateTime.MinValue);
			DailyHighLow.Yest.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			DailyHighLow.Yest.HighGustTime = ini.GetValue("Wind", "Time", DateTime.MinValue);
			DailyHighLow.Yest.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);

			YesterdayWindRun = ini.GetValue("Wind", "Windrun", 0.0);
			MetData.YestDominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			// Temperature
			DailyHighLow.Yest.LowTemp = ini.GetValue("Temp", "Low", 0.0);
			DailyHighLow.Yest.LowTempTime = ini.GetValue("Temp", "LTime", DateTime.MinValue);
			DailyHighLow.Yest.HighTemp = ini.GetValue("Temp", "High", 0.0);
			DailyHighLow.Yest.HighTempTime = ini.GetValue("Temp", "HTime", DateTime.MinValue);
			MetData.YestChillHours = ini.GetValue("Temp", "ChillHours", -1.0);
			YestHeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			YestCoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			YestAvgTemp = ini.GetValue("Temp", "AvgTemp", 0.0);
			DailyHighLow.Yest.TempRange = DailyHighLow.Yest.HighTemp - DailyHighLow.Yest.LowTemp;
			// Temperature midnight
			DailyHighLow.YestMidnight.LowTemp = ini.GetValue("TempMidnight", "Low", 0.0);
			DailyHighLow.YestMidnight.LowTempTime = ini.GetValue("TempMidnight", "LTime", DateTime.MinValue);
			DailyHighLow.YestMidnight.HighTemp = ini.GetValue("TempMidnight", "High", 0.0);
			DailyHighLow.YestMidnight.HighTempTime = ini.GetValue("TempMidnight", "HTime", DateTime.MinValue);
			// Temperature 9am
			DailyHighLow.Yest9am.LowTemp = ini.GetValue("Temp9am", "Low", 0.0);
			DailyHighLow.Yest9am.LowTempTime = ini.GetValue("Temp9am", "LTime", DateTime.MinValue);
			DailyHighLow.Yest9am.HighTemp = ini.GetValue("Temp9am", "High", 0.0);
			DailyHighLow.Yest9am.HighTempTime = ini.GetValue("Temp9am", "HTime", DateTime.MinValue);
			// Pressure
			DailyHighLow.Yest.LowPress = ini.GetValue("Pressure", "Low", 0.0);
			DailyHighLow.Yest.LowPressTime = ini.GetValue("Pressure", "LTime", DateTime.MinValue);
			DailyHighLow.Yest.HighPress = ini.GetValue("Pressure", "High", 0.0);
			DailyHighLow.Yest.HighPressTime = ini.GetValue("Pressure", "HTime", DateTime.MinValue);
			// rain
			DailyHighLow.Yest.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			DailyHighLow.Yest.HighRainRateTime = ini.GetValue("Rain", "HTime", DateTime.MinValue);
			DailyHighLow.Yest.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			DailyHighLow.Yest.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", DateTime.MinValue);
			DailyHighLow.Yest.HighRain24h = ini.GetValue("Rain", "High24h", 0.0);
			DailyHighLow.Yest.HighRain24hTime = ini.GetValue("Rain", "High24hTime", DateTime.MinValue);
			MetData.RG11RainYesterday = ini.GetValue("Rain", "RG11Yesterday", 0.0);
			// humidity
			DailyHighLow.Yest.LowHumidity = ini.GetValue("Humidity", "Low", 0);
			DailyHighLow.Yest.HighHumidity = ini.GetValue("Humidity", "High", 0);
			DailyHighLow.Yest.LowHumidityTime = ini.GetValue("Humidity", "LTime", DateTime.MinValue);
			DailyHighLow.Yest.HighHumidityTime = ini.GetValue("Humidity", "HTime", DateTime.MinValue);
			// Solar
			MetData.YestSunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			// heat index
			DailyHighLow.Yest.HighHeatIndex = ini.GetValue("HeatIndex", "High", 0.0);
			DailyHighLow.Yest.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", DateTime.MinValue);
			// App temp
			DailyHighLow.Yest.LowAppTemp = ini.GetValue("AppTemp", "Low", 0.0);
			DailyHighLow.Yest.LowAppTempTime = ini.GetValue("AppTemp", "LTime", DateTime.MinValue);
			DailyHighLow.Yest.HighAppTemp = ini.GetValue("AppTemp", "High", 0.0);
			DailyHighLow.Yest.HighAppTempTime = ini.GetValue("AppTemp", "HTime", DateTime.MinValue);
			// wind chill
			DailyHighLow.Yest.LowWindChill = ini.GetValue("WindChill", "Low", 0.0);
			DailyHighLow.Yest.LowWindChillTime = ini.GetValue("WindChill", "LTime", DateTime.MinValue);
			// Dewpoint
			DailyHighLow.Yest.LowDewPoint = ini.GetValue("Dewpoint", "Low", 0.0);
			DailyHighLow.Yest.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", DateTime.MinValue);
			DailyHighLow.Yest.HighDewPoint = ini.GetValue("Dewpoint", "High", 0.0);
			DailyHighLow.Yest.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", DateTime.MinValue);
			// Solar
			DailyHighLow.Yest.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0);
			DailyHighLow.Yest.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", DateTime.MinValue);
			DailyHighLow.Yest.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			DailyHighLow.Yest.HighUvTime = ini.GetValue("Solar", "HighUVTime", DateTime.MinValue);
			// Feels like
			DailyHighLow.Yest.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 0.0);
			DailyHighLow.Yest.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", DateTime.MinValue);
			DailyHighLow.Yest.HighFeelsLike = ini.GetValue("FeelsLike", "High", 0.0);
			DailyHighLow.Yest.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", DateTime.MinValue);
			// Humidex
			DailyHighLow.Yest.HighHumidex = ini.GetValue("Humidex", "High", 0.0);
			DailyHighLow.Yest.HighHumidexTime = ini.GetValue("Humidex", "HTime", DateTime.MinValue);
			// BGT
			DailyHighLow.Yest.HighBgt = ini.GetValue("BGT", "High", 0.0);
			DailyHighLow.Yest.HighBgtTime = ini.GetValue("BGT", "HTime", DateTime.MinValue);
			// WBGT
			DailyHighLow.Yest.HighWbgt = ini.GetValue("WBGT", "High", 0.0);
			DailyHighLow.Yest.HighWbgtTime = ini.GetValue("WBGT", "HTime", DateTime.MinValue);
		}

		public void WriteYesterdayFile(DateTime logdate)
		{
			cumulus.LogMessage("Writing yesterday.ini");

			var ini = new IniFile(cumulus.YesterdayFile);

			ini.SetValue("General", "Date", cumulus.MeteoDate(logdate));
			// Wind
			ini.SetValue("Wind", "Speed", DailyHighLow.Yest.HighWind);
			ini.SetValue("Wind", "SpTime", DailyHighLow.Yest.HighWindTime);
			ini.SetValue("Wind", "Gust", DailyHighLow.Yest.HighGust);
			ini.SetValue("Wind", "Time", DailyHighLow.Yest.HighGustTime);
			ini.SetValue("Wind", "Bearing", DailyHighLow.Yest.HighGustBearing);
			ini.SetValue("Wind", "Direction", CompassPoint(DailyHighLow.Yest.HighGustBearing));
			ini.SetValue("Wind", "Windrun", YesterdayWindRun);
			ini.SetValue("Wind", "DominantWindBearing", MetData.YestDominantWindBearing);
			// Temperature
			ini.SetValue("Temp", "Low", DailyHighLow.Yest.LowTemp);
			ini.SetValue("Temp", "LTime", DailyHighLow.Yest.LowTempTime);
			ini.SetValue("Temp", "High", DailyHighLow.Yest.HighTemp);
			ini.SetValue("Temp", "HTime", DailyHighLow.Yest.HighTempTime);
			ini.SetValue("Temp", "ChillHours", MetData.YestChillHours);
			ini.SetValue("Temp", "HeatingDegreeDays", YestHeatingDegreeDays);
			ini.SetValue("Temp", "CoolingDegreeDays", YestCoolingDegreeDays);
			ini.SetValue("Temp", "AvgTemp", YestAvgTemp);
			// Temperature midnight
			ini.SetValue("TempMidnight", "Low", DailyHighLow.YestMidnight.LowTemp);
			ini.SetValue("TempMidnight", "LTime", DailyHighLow.YestMidnight.LowTempTime);
			ini.SetValue("TempMidnight", "High", DailyHighLow.YestMidnight.HighTemp);
			ini.SetValue("TempMidnight", "HTime", DailyHighLow.YestMidnight.HighTempTime);
			// Temperature 9am
			ini.SetValue("Temp9am", "Low", DailyHighLow.Yest9am.LowTemp);
			ini.SetValue("Temp9am", "LTime", DailyHighLow.Yest9am.LowTempTime);
			ini.SetValue("Temp9am", "High", DailyHighLow.Yest9am.HighTemp);
			ini.SetValue("Temp9am", "HTime", DailyHighLow.Yest9am.HighTempTime);
			// Pressure
			ini.SetValue("Pressure", "Low", DailyHighLow.Yest.LowPress);
			ini.SetValue("Pressure", "LTime", DailyHighLow.Yest.LowPressTime);
			ini.SetValue("Pressure", "High", DailyHighLow.Yest.HighPress);
			ini.SetValue("Pressure", "HTime", DailyHighLow.Yest.HighPressTime);
			// rain
			ini.SetValue("Rain", "High", DailyHighLow.Yest.HighRainRate);
			ini.SetValue("Rain", "HTime", DailyHighLow.Yest.HighRainRateTime);
			ini.SetValue("Rain", "HourlyHigh", DailyHighLow.Yest.HighHourlyRain);
			ini.SetValue("Rain", "HHourlyTime", DailyHighLow.Yest.HighHourlyRainTime);
			ini.SetValue("Rain", "High24h", DailyHighLow.Yest.HighRain24h);
			ini.SetValue("Rain", "High24hTime", DailyHighLow.Yest.HighRain24hTime);
			ini.SetValue("Rain", "RG11Yesterday", MetData.RG11RainYesterday);
			// humidity
			ini.SetValue("Humidity", "Low", DailyHighLow.Yest.LowHumidity);
			ini.SetValue("Humidity", "High", DailyHighLow.Yest.HighHumidity);
			ini.SetValue("Humidity", "LTime", DailyHighLow.Yest.LowHumidityTime);
			ini.SetValue("Humidity", "HTime", DailyHighLow.Yest.HighHumidityTime);
			// Solar
			ini.SetValue("Solar", "SunshineHours", MetData.YestSunshineHours);
			// heat index
			ini.SetValue("HeatIndex", "High", DailyHighLow.Yest.HighHeatIndex);
			ini.SetValue("HeatIndex", "HTime", DailyHighLow.Yest.HighHeatIndexTime);
			// App temp
			ini.SetValue("AppTemp", "Low", DailyHighLow.Yest.LowAppTemp);
			ini.SetValue("AppTemp", "LTime", DailyHighLow.Yest.LowAppTempTime);
			ini.SetValue("AppTemp", "High", DailyHighLow.Yest.HighAppTemp);
			ini.SetValue("AppTemp", "HTime", DailyHighLow.Yest.HighAppTempTime);
			// wind chill
			ini.SetValue("WindChill", "Low", DailyHighLow.Yest.LowWindChill);
			ini.SetValue("WindChill", "LTime", DailyHighLow.Yest.LowWindChillTime);
			// Dewpoint
			ini.SetValue("Dewpoint", "Low", DailyHighLow.Yest.LowDewPoint);
			ini.SetValue("Dewpoint", "LTime", DailyHighLow.Yest.LowDewPointTime);
			ini.SetValue("Dewpoint", "High", DailyHighLow.Yest.HighDewPoint);
			ini.SetValue("Dewpoint", "HTime", DailyHighLow.Yest.HighDewPointTime);
			// Solar
			ini.SetValue("Solar", "HighSolarRad", DailyHighLow.Yest.HighSolar);
			ini.SetValue("Solar", "HighSolarRadTime", DailyHighLow.Yest.HighSolarTime);
			ini.SetValue("Solar", "HighUV", DailyHighLow.Yest.HighUv);
			ini.SetValue("Solar", "HighUVTime", DailyHighLow.Yest.HighUvTime);
			// Feels like
			ini.SetValue("FeelsLike", "Low", DailyHighLow.Yest.LowFeelsLike);
			ini.SetValue("FeelsLike", "LTime", DailyHighLow.Yest.LowFeelsLikeTime);
			ini.SetValue("FeelsLike", "High", DailyHighLow.Yest.HighFeelsLike);
			ini.SetValue("FeelsLike", "HTime", DailyHighLow.Yest.HighFeelsLikeTime);
			// Humidex
			ini.SetValue("Humidex", "High", DailyHighLow.Yest.HighHumidex);
			ini.SetValue("Humidex", "HTime", DailyHighLow.Yest.HighHumidexTime);

			ini.Flush();

			cumulus.LogMessage("Written yesterday.ini");
		}
	}
}
