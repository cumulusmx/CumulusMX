using System;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadYesterdayFile()
		{
			var ini = new IniFile(cumulus.YesterdayFile);

			// Wind
			HiLoYest.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			HiLoYest.HighWindTime = ini.GetValue("Wind", "SpTime", DateTime.MinValue);
			HiLoYest.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			HiLoYest.HighGustTime = ini.GetValue("Wind", "Time", DateTime.MinValue);
			HiLoYest.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);

			YesterdayWindRun = ini.GetValue("Wind", "Windrun", 0.0);
			YestDominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			// Temperature
			HiLoYest.LowTemp = ini.GetValue("Temp", "Low", 0.0);
			HiLoYest.LowTempTime = ini.GetValue("Temp", "LTime", DateTime.MinValue);
			HiLoYest.HighTemp = ini.GetValue("Temp", "High", 0.0);
			HiLoYest.HighTempTime = ini.GetValue("Temp", "HTime", DateTime.MinValue);
			YestChillHours = ini.GetValue("Temp", "ChillHours", -1.0);
			YestHeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			YestCoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			YestAvgTemp = ini.GetValue("Temp", "AvgTemp", 0.0);
			HiLoYest.TempRange = HiLoYest.HighTemp - HiLoYest.LowTemp;
			// Temperature midnight
			HiLoYestMidnight.LowTemp = ini.GetValue("TempMidnight", "Low", 0.0);
			HiLoYestMidnight.LowTempTime = ini.GetValue("TempMidnight", "LTime", DateTime.MinValue);
			HiLoYestMidnight.HighTemp = ini.GetValue("TempMidnight", "High", 0.0);
			HiLoYestMidnight.HighTempTime = ini.GetValue("TempMidnight", "HTime", DateTime.MinValue);
			// Temperature 9am
			HiLoYest9am.LowTemp = ini.GetValue("Temp9am", "Low", 0.0);
			HiLoYest9am.LowTempTime = ini.GetValue("Temp9am", "LTime", DateTime.MinValue);
			HiLoYest9am.HighTemp = ini.GetValue("Temp9am", "High", 0.0);
			HiLoYest9am.HighTempTime = ini.GetValue("Temp9am", "HTime", DateTime.MinValue);
			// Pressure
			HiLoYest.LowPress = ini.GetValue("Pressure", "Low", 0.0);
			HiLoYest.LowPressTime = ini.GetValue("Pressure", "LTime", DateTime.MinValue);
			HiLoYest.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoYest.HighPressTime = ini.GetValue("Pressure", "HTime", DateTime.MinValue);
			// rain
			HiLoYest.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			HiLoYest.HighRainRateTime = ini.GetValue("Rain", "HTime", DateTime.MinValue);
			HiLoYest.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoYest.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", DateTime.MinValue);
			HiLoYest.HighRain24h = ini.GetValue("Rain", "High24h", 0.0);
			HiLoYest.HighRain24hTime = ini.GetValue("Rain", "High24hTime", DateTime.MinValue);
			RG11RainYesterday = ini.GetValue("Rain", "RG11Yesterday", 0.0);
			// humidity
			HiLoYest.LowHumidity = ini.GetValue("Humidity", "Low", 0);
			HiLoYest.HighHumidity = ini.GetValue("Humidity", "High", 0);
			HiLoYest.LowHumidityTime = ini.GetValue("Humidity", "LTime", DateTime.MinValue);
			HiLoYest.HighHumidityTime = ini.GetValue("Humidity", "HTime", DateTime.MinValue);
			// Solar
			YestSunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			// heat index
			HiLoYest.HighHeatIndex = ini.GetValue("HeatIndex", "High", 0.0);
			HiLoYest.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", DateTime.MinValue);
			// App temp
			HiLoYest.LowAppTemp = ini.GetValue("AppTemp", "Low", 0.0);
			HiLoYest.LowAppTempTime = ini.GetValue("AppTemp", "LTime", DateTime.MinValue);
			HiLoYest.HighAppTemp = ini.GetValue("AppTemp", "High", 0.0);
			HiLoYest.HighAppTempTime = ini.GetValue("AppTemp", "HTime", DateTime.MinValue);
			// wind chill
			HiLoYest.LowWindChill = ini.GetValue("WindChill", "Low", 0.0);
			HiLoYest.LowWindChillTime = ini.GetValue("WindChill", "LTime", DateTime.MinValue);
			// Dewpoint
			HiLoYest.LowDewPoint = ini.GetValue("Dewpoint", "Low", 0.0);
			HiLoYest.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", DateTime.MinValue);
			HiLoYest.HighDewPoint = ini.GetValue("Dewpoint", "High", 0.0);
			HiLoYest.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", DateTime.MinValue);
			// Solar
			HiLoYest.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0);
			HiLoYest.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", DateTime.MinValue);
			HiLoYest.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			HiLoYest.HighUvTime = ini.GetValue("Solar", "HighUVTime", DateTime.MinValue);
			// Feels like
			HiLoYest.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 0.0);
			HiLoYest.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", DateTime.MinValue);
			HiLoYest.HighFeelsLike = ini.GetValue("FeelsLike", "High", 0.0);
			HiLoYest.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", DateTime.MinValue);
			// Humidex
			HiLoYest.HighHumidex = ini.GetValue("Humidex", "High", 0.0);
			HiLoYest.HighHumidexTime = ini.GetValue("Humidex", "HTime", DateTime.MinValue);
			// BGT
			HiLoYest.HighBgt = ini.GetValue("BGT", "High", 0.0);
			HiLoYest.HighBgtTime = ini.GetValue("BGT", "HTime", DateTime.MinValue);
			// WBGT
			HiLoYest.HighWbgt = ini.GetValue("WBGT", "High", 0.0);
			HiLoYest.HighWbgtTime = ini.GetValue("WBGT", "HTime", DateTime.MinValue);
		}

		public void WriteYesterdayFile(DateTime logdate)
		{
			cumulus.LogMessage("Writing yesterday.ini");

			var ini = new IniFile(cumulus.YesterdayFile);

			ini.SetValue("General", "Date", cumulus.MeteoDate(logdate));
			// Wind
			ini.SetValue("Wind", "Speed", HiLoYest.HighWind);
			ini.SetValue("Wind", "SpTime", HiLoYest.HighWindTime);
			ini.SetValue("Wind", "Gust", HiLoYest.HighGust);
			ini.SetValue("Wind", "Time", HiLoYest.HighGustTime);
			ini.SetValue("Wind", "Bearing", HiLoYest.HighGustBearing);
			ini.SetValue("Wind", "Direction", CompassPoint(HiLoYest.HighGustBearing));
			ini.SetValue("Wind", "Windrun", YesterdayWindRun);
			ini.SetValue("Wind", "DominantWindBearing", YestDominantWindBearing);
			// Temperature
			ini.SetValue("Temp", "Low", HiLoYest.LowTemp);
			ini.SetValue("Temp", "LTime", HiLoYest.LowTempTime);
			ini.SetValue("Temp", "High", HiLoYest.HighTemp);
			ini.SetValue("Temp", "HTime", HiLoYest.HighTempTime);
			ini.SetValue("Temp", "ChillHours", YestChillHours);
			ini.SetValue("Temp", "HeatingDegreeDays", YestHeatingDegreeDays);
			ini.SetValue("Temp", "CoolingDegreeDays", YestCoolingDegreeDays);
			ini.SetValue("Temp", "AvgTemp", YestAvgTemp);
			// Temperature midnight
			ini.SetValue("TempMidnight", "Low", HiLoYestMidnight.LowTemp);
			ini.SetValue("TempMidnight", "LTime", HiLoYestMidnight.LowTempTime);
			ini.SetValue("TempMidnight", "High", HiLoYestMidnight.HighTemp);
			ini.SetValue("TempMidnight", "HTime", HiLoYestMidnight.HighTempTime);
			// Temperature 9am
			ini.SetValue("Temp9am", "Low", HiLoYest9am.LowTemp);
			ini.SetValue("Temp9am", "LTime", HiLoYest9am.LowTempTime);
			ini.SetValue("Temp9am", "High", HiLoYest9am.HighTemp);
			ini.SetValue("Temp9am", "HTime", HiLoYest9am.HighTempTime);
			// Pressure
			ini.SetValue("Pressure", "Low", HiLoYest.LowPress);
			ini.SetValue("Pressure", "LTime", HiLoYest.LowPressTime);
			ini.SetValue("Pressure", "High", HiLoYest.HighPress);
			ini.SetValue("Pressure", "HTime", HiLoYest.HighPressTime);
			// rain
			ini.SetValue("Rain", "High", HiLoYest.HighRainRate);
			ini.SetValue("Rain", "HTime", HiLoYest.HighRainRateTime);
			ini.SetValue("Rain", "HourlyHigh", HiLoYest.HighHourlyRain);
			ini.SetValue("Rain", "HHourlyTime", HiLoYest.HighHourlyRainTime);
			ini.SetValue("Rain", "High24h", HiLoYest.HighRain24h);
			ini.SetValue("Rain", "High24hTime", HiLoYest.HighRain24hTime);
			ini.SetValue("Rain", "RG11Yesterday", RG11RainYesterday);
			// humidity
			ini.SetValue("Humidity", "Low", HiLoYest.LowHumidity);
			ini.SetValue("Humidity", "High", HiLoYest.HighHumidity);
			ini.SetValue("Humidity", "LTime", HiLoYest.LowHumidityTime);
			ini.SetValue("Humidity", "HTime", HiLoYest.HighHumidityTime);
			// Solar
			ini.SetValue("Solar", "SunshineHours", YestSunshineHours);
			// heat index
			ini.SetValue("HeatIndex", "High", HiLoYest.HighHeatIndex);
			ini.SetValue("HeatIndex", "HTime", HiLoYest.HighHeatIndexTime);
			// App temp
			ini.SetValue("AppTemp", "Low", HiLoYest.LowAppTemp);
			ini.SetValue("AppTemp", "LTime", HiLoYest.LowAppTempTime);
			ini.SetValue("AppTemp", "High", HiLoYest.HighAppTemp);
			ini.SetValue("AppTemp", "HTime", HiLoYest.HighAppTempTime);
			// wind chill
			ini.SetValue("WindChill", "Low", HiLoYest.LowWindChill);
			ini.SetValue("WindChill", "LTime", HiLoYest.LowWindChillTime);
			// Dewpoint
			ini.SetValue("Dewpoint", "Low", HiLoYest.LowDewPoint);
			ini.SetValue("Dewpoint", "LTime", HiLoYest.LowDewPointTime);
			ini.SetValue("Dewpoint", "High", HiLoYest.HighDewPoint);
			ini.SetValue("Dewpoint", "HTime", HiLoYest.HighDewPointTime);
			// Solar
			ini.SetValue("Solar", "HighSolarRad", HiLoYest.HighSolar);
			ini.SetValue("Solar", "HighSolarRadTime", HiLoYest.HighSolarTime);
			ini.SetValue("Solar", "HighUV", HiLoYest.HighUv);
			ini.SetValue("Solar", "HighUVTime", HiLoYest.HighUvTime);
			// Feels like
			ini.SetValue("FeelsLike", "Low", HiLoYest.LowFeelsLike);
			ini.SetValue("FeelsLike", "LTime", HiLoYest.LowFeelsLikeTime);
			ini.SetValue("FeelsLike", "High", HiLoYest.HighFeelsLike);
			ini.SetValue("FeelsLike", "HTime", HiLoYest.HighFeelsLikeTime);
			// Humidex
			ini.SetValue("Humidex", "High", HiLoYest.HighHumidex);
			ini.SetValue("Humidex", "HTime", HiLoYest.HighHumidexTime);

			ini.Flush();

			cumulus.LogMessage("Written yesterday.ini");
		}
	}
}
