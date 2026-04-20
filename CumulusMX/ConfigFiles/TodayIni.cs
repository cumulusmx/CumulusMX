using System;
using System.Globalization;
using System.IO;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void ReadTodayFile()
		{
			if (!File.Exists(cumulus.TodayIniFile))
			{
				FirstRun = true;
			}

			var ini = new IniFile(cumulus.TodayIniFile);

			var todayfiledate = ini.GetValue("General", "Date", "00/00/00");
			var timestampstr = ini.GetValue("General", "Timestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));

			Cumulus.LogConsoleMessage("Last update: " + timestampstr);

			cumulus.LastUpdateTime = DateTime.Parse(timestampstr, CultureInfo.InvariantCulture);
			var todayDate = cumulus.LastUpdateTime.Date;

			cumulus.LogMessage("ReadTodayFile: Last update time from today.ini: " + cumulus.LastUpdateTime);

			var meteoTodayDate = cumulus.MeteoDate(cumulus.LastUpdateTime).Date;

			var defaultyear = meteoTodayDate.Year;
			var defaultmonth = meteoTodayDate.Month;
			var defaultday = meteoTodayDate.Day;

			CurrentYear = ini.GetValue("General", "CurrentYear", defaultyear);
			CurrentMonth = ini.GetValue("General", "CurrentMonth", defaultmonth);
			CurrentDay = ini.GetValue("General", "CurrentDay", defaultday);
			CurrentDate = new DateTime(CurrentYear, CurrentMonth, CurrentDay, 0, 0, 0, DateTimeKind.Local);

			cumulus.LogMessage("ReadTodayFile: Date = " + todayfiledate + ", LastUpdateTime = " + cumulus.LastUpdateTime + ", Month = " + CurrentMonth);

			LastRainTip = ini.GetValue("Rain", "LastTip", "0000-00-00 00:00");

			FOSensorClockTime = ini.GetValue("FineOffset", "FOSensorClockTime", DateTime.MinValue);
			FOStationClockTime = ini.GetValue("FineOffset", "FOStationClockTime", DateTime.MinValue);
			FOSolarClockTime = ini.GetValue("FineOffset", "FOSolarClockTime", DateTime.MinValue);
			if (cumulus.FineOffsetOptions.SyncReads && (cumulus.StationType == StationTypes.FineOffset || cumulus.StationType == StationTypes.FineOffsetSolar))
			{
				cumulus.LogMessage("ReadTodayFile: Sensor clock  " + FOSensorClockTime.ToLongTimeString());
				cumulus.LogMessage("ReadTodayFile: Station clock " + FOStationClockTime.ToLongTimeString());
			}
			ConsecutiveRainDays = ini.GetValue("Rain", "ConsecutiveRainDays", 0);
			ConsecutiveDryDays = ini.GetValue("Rain", "ConsecutiveDryDays", 0);

			AnnualETTotal = ini.GetValue("ET", "Annual", 0.0);
			StartofdayET = ini.GetValue("ET", "Startofday", -1.0);
			if (StartofdayET < 0)
			{
				cumulus.LogMessage("ReadTodayFile: ET not initialised");
				noET = true;
			}
			else
			{
				ET = AnnualETTotal - StartofdayET;
				cumulus.LogMessage("ReadTodayFile: ET today = " + ET.ToString(cumulus.ETFormat));
			}
			ChillHours = ini.GetValue("Temp", "ChillHours", 0.0);

			// NOAA report names
			cumulus.NOAAconf.LatestMonthReport = ini.GetValue("NOAA", "LatestMonthlyReport", "");
			cumulus.NOAAconf.LatestYearReport = ini.GetValue("NOAA", "LatestYearlyReport", "");

			// Solar
			HiLoToday.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0);
			HiLoToday.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", todayDate);
			HiLoToday.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			HiLoToday.HighUvTime = ini.GetValue("Solar", "HighUVTime", meteoTodayDate);
			StartOfDaySunHourCounter = ini.GetValue("Solar", "SunStart", -9999.0);
			RG11RainToday = ini.GetValue("Rain", "RG11Today", 0.0);

			// Wind
			HiLoToday.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			HiLoToday.HighWindTime = ini.GetValue("Wind", "SpTime", meteoTodayDate);
			HiLoToday.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			HiLoToday.HighGustTime = ini.GetValue("Wind", "Time", meteoTodayDate);
			HiLoToday.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);
			WindRunToday = ini.GetValue("Wind", "Windrun", 0.0);
			DominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			DominantWindBearingMinutes = ini.GetValue("Wind", "DominantWindBearingMinutes", 0);
			DominantWindBearingX = ini.GetValue("Wind", "DominantWindBearingX", 0.0);
			DominantWindBearingY = ini.GetValue("Wind", "DominantWindBearingY", 0.0);

			// Temperature
			HiLoToday.LowTemp = ini.GetValue("Temp", "Low", 999.0);
			HiLoToday.LowTempTime = ini.GetValue("Temp", "LTime", meteoTodayDate);
			HiLoToday.HighTemp = ini.GetValue("Temp", "High", -999.0);
			HiLoToday.HighTempTime = ini.GetValue("Temp", "HTime", meteoTodayDate);
			if (HiLoToday.HighTemp > -400 && HiLoToday.LowTemp < 400)
				HiLoToday.TempRange = HiLoToday.HighTemp - HiLoToday.LowTemp;
			else
				HiLoToday.TempRange = 0;
			TempTotalToday = ini.GetValue("Temp", "Total", 0.0);
			tempsamplestoday = ini.GetValue("Temp", "Samples", 1);
			HeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			CoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			GrowingDegreeDaysThisYear1 = ini.GetValue("Temp", "GrowingDegreeDaysThisYear1", 0.0);
			GrowingDegreeDaysThisYear2 = ini.GetValue("Temp", "GrowingDegreeDaysThisYear2", 0.0);

			// Temperature midnight rollover
			HiLoTodayMidnight.LowTemp = ini.GetValue("TempMidnight", "Low", 999.0);
			HiLoTodayMidnight.LowTempTime = ini.GetValue("TempMidnight", "LTime", meteoTodayDate);
			HiLoTodayMidnight.HighTemp = ini.GetValue("TempMidnight", "High", -999.0);
			HiLoTodayMidnight.HighTempTime = ini.GetValue("TempMidnight", "HTime", meteoTodayDate);

			// Temperature 9am rollover
			HiLoToday9am.LowTemp = ini.GetValue("Temp9am", "Low", 999.0);
			HiLoToday9am.LowTempTime = ini.GetValue("Temp9am", "LTime", meteoTodayDate);
			HiLoToday9am.HighTemp = ini.GetValue("Temp9am", "High", -999.0);
			HiLoToday9am.HighTempTime = ini.GetValue("Temp9am", "HTime", meteoTodayDate);

			// Pressure
			HiLoToday.LowPress = ini.GetValue("Pressure", "Low", 9999.0);
			HiLoToday.LowPressTime = ini.GetValue("Pressure", "LTime", meteoTodayDate);
			HiLoToday.HighPress = ini.GetValue("Pressure", "High", 0.0);
			HiLoToday.HighPressTime = ini.GetValue("Pressure", "HTime", meteoTodayDate);

			// rain
			HiLoToday.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			HiLoToday.HighRainRateTime = ini.GetValue("Rain", "HTime", meteoTodayDate);
			HiLoToday.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			HiLoToday.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", meteoTodayDate);
			HiLoToday.HighRain24h = ini.GetValue("Rain", "High24h", 0.0);
			HiLoToday.HighRain24hTime = ini.GetValue("Rain", "High24hTime", meteoTodayDate);
			RainYesterday = ini.GetValue("Rain", "Yesterday", 0.0);
			RainCounterDayStart = ini.GetValue("Rain", "Start", -1.0);
			MidnightRainCount = ini.GetValue("Rain", "Midnight", -1.0);
			RainCounter = ini.GetValue("Rain", "Last", -1.0);

			if (RainCounterDayStart < -0.5)
			{
				cumulus.LogMessage("ReadTodayfile: set initialiseRainDayStart true");
				initialiseRainDayStart = true;
			}
			else
			{
				initialiseRainDayStart = false;
			}

			if (RainCounter < -0.5)
			{
				cumulus.LogMessage("ReadTodayfile: set initialiseRainCounterOnFirstData true");
				initialiseRainCounter = true;
			}
			else
			{
				initialiseRainCounter = false;
			}

			if (MidnightRainCount < -0.5)
			{
				if (cumulus.RolloverHour == 0 && !initialiseRainDayStart)
				{
					// midnight and rollover are the same
					MidnightRainCount = RainCounterDayStart;
					initialiseMidnightRain = false;
				}
				else
				{
					cumulus.LogMessage("ReadTodayfile: set initialiseMidnightRain true");
					initialiseMidnightRain = true;
				}
			}
			else
			{
				initialiseMidnightRain = false;
			}

			cumulus.LogMessage($"ReadTodayfile: Rain day start: {RainCounterDayStart:F4}, midnight counter: {MidnightRainCount:F4}, last counter: {RainCounter:F4}");

			// humidity
			HiLoToday.LowHumidity = ini.GetValue("Humidity", "Low", 100);
			HiLoToday.HighHumidity = ini.GetValue("Humidity", "High", 0);
			HiLoToday.LowHumidityTime = ini.GetValue("Humidity", "LTime", meteoTodayDate);
			HiLoToday.HighHumidityTime = ini.GetValue("Humidity", "HTime", meteoTodayDate);

			// Solar
			SunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			SunshineToMidnight = ini.GetValue("Solar", "SunshineHoursToMidnight", 0.0);

			// heat index
			HiLoToday.HighHeatIndex = ini.GetValue("HeatIndex", "High", -999.0);
			HiLoToday.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", meteoTodayDate);

			// Apparent temp
			HiLoToday.HighAppTemp = ini.GetValue("AppTemp", "High", -999.0);
			HiLoToday.HighAppTempTime = ini.GetValue("AppTemp", "HTime", meteoTodayDate);
			HiLoToday.LowAppTemp = ini.GetValue("AppTemp", "Low", 999.0);
			HiLoToday.LowAppTempTime = ini.GetValue("AppTemp", "LTime", meteoTodayDate);

			// wind chill
			HiLoToday.LowWindChill = ini.GetValue("WindChill", "Low", 999.0);
			HiLoToday.LowWindChillTime = ini.GetValue("WindChill", "LTime", meteoTodayDate);

			// Dew point
			HiLoToday.HighDewPoint = ini.GetValue("Dewpoint", "High", -999.0);
			HiLoToday.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", meteoTodayDate);
			HiLoToday.LowDewPoint = ini.GetValue("Dewpoint", "Low", 999.0);
			HiLoToday.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", meteoTodayDate);

			// Feels like
			HiLoToday.HighFeelsLike = ini.GetValue("FeelsLike", "High", -999.0);
			HiLoToday.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", meteoTodayDate);
			HiLoToday.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 999.0);
			HiLoToday.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", meteoTodayDate);

			// Humidex
			HiLoToday.HighHumidex = ini.GetValue("Humidex", "High", -999.0);
			HiLoToday.HighHumidexTime = ini.GetValue("Humidex", "HTime", meteoTodayDate);

			// BGT
			HiLoToday.HighBgt = ini.GetValue("BGT", "High", Cumulus.DefaultHiVal);
			HiLoToday.HighBgtTime = ini.GetValue("BGT", "HTime", meteoTodayDate);

			// WBGT
			HiLoToday.HighWbgt = ini.GetValue("WBGT", "High", -999.0);
			HiLoToday.HighWbgtTime = ini.GetValue("WBGT", "HTime", meteoTodayDate);

			// Records
			AlltimeRecordTimestamp = ini.GetValue("Records", "Alltime", DateTime.MinValue);

			// Lightning (GW1000 for now)
			LightningDistance = ini.GetValue("Lightning", "Distance", -1.0);
			LightningStrikesToday = ini.GetValue("Lightning", "StrikesToday", 0);
			LightningCounter = ini.GetValue("Lightning", "Counter", LightningStrikesToday);
			LightningTime = ini.GetValue("Lightning", "LastStrike", DateTime.MinValue);
			if (LightningTime.Year == 1900)
			{
				// legacy - used to be 1/1/1900
				LightningTime = DateTime.MinValue;
			}

			// Snow accumulation
			for (var i = 1; i < Snow24h.Length; i++)
			{
				Snow24h[i] = ini.GetValue("Snow", "Snow24h" + i, (double?) null);
				LastLaserSnowDepth[i] = ini.GetValue("Snow", "LastLaserDepth" + i, (double?) null);
				SnowSeason[i] = ini.GetValue("Snow", "SnowSeason" + i, (double?) null);
			}
		}

		public void WriteTodayFile(DateTime timestamp, bool Log)
		{
			try
			{
				var ini = new IniFile(cumulus.TodayIniFile);

				// Date
				ini.SetValue("General", "Date", cumulus.MeteoDate(timestamp).ToShortDateString());
				// Timestamp
				ini.SetValue("General", "Timestamp", cumulus.LastUpdateTime.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
				ini.SetValue("General", "CurrentYear", CurrentYear);
				ini.SetValue("General", "CurrentMonth", CurrentMonth);
				ini.SetValue("General", "CurrentDay", CurrentDay);
				// Wind
				ini.SetValue("Wind", "Speed", HiLoToday.HighWind);
				ini.SetValue("Wind", "SpTime", HiLoToday.HighWindTime);
				ini.SetValue("Wind", "Gust", HiLoToday.HighGust);
				ini.SetValue("Wind", "Time", HiLoToday.HighGustTime);
				ini.SetValue("Wind", "Bearing", HiLoToday.HighGustBearing);
				ini.SetValue("Wind", "Direction", CompassPoint(HiLoToday.HighGustBearing));
				ini.SetValue("Wind", "Windrun", WindRunToday);
				ini.SetValue("Wind", "DominantWindBearing", DominantWindBearing);
				ini.SetValue("Wind", "DominantWindBearingMinutes", DominantWindBearingMinutes);
				ini.SetValue("Wind", "DominantWindBearingX", DominantWindBearingX);
				ini.SetValue("Wind", "DominantWindBearingY", DominantWindBearingY);
				// Temperature
				ini.SetValue("Temp", "Low", HiLoToday.LowTemp);
				ini.SetValue("Temp", "LTime", HiLoToday.LowTempTime);
				ini.SetValue("Temp", "High", HiLoToday.HighTemp);
				ini.SetValue("Temp", "HTime", HiLoToday.HighTempTime);
				ini.SetValue("Temp", "Total", TempTotalToday);
				ini.SetValue("Temp", "Samples", tempsamplestoday);
				ini.SetValue("Temp", "ChillHours", ChillHours);
				ini.SetValue("Temp", "HeatingDegreeDays", HeatingDegreeDays);
				ini.SetValue("Temp", "CoolingDegreeDays", CoolingDegreeDays);
				ini.SetValue("Temp", "GrowingDegreeDaysThisYear1", GrowingDegreeDaysThisYear1);
				ini.SetValue("Temp", "GrowingDegreeDaysThisYear2", GrowingDegreeDaysThisYear2);
				// Temperature midnight rollover
				ini.SetValue("TempMidnight", "Low", HiLoTodayMidnight.LowTemp);
				ini.SetValue("TempMidnight", "LTime", HiLoTodayMidnight.LowTempTime);
				ini.SetValue("TempMidnight", "High", HiLoTodayMidnight.HighTemp);
				ini.SetValue("TempMidnight", "HTime", HiLoTodayMidnight.HighTempTime);
				// Temperature 9am rollover
				ini.SetValue("Temp9am", "Low", HiLoToday9am.LowTemp);
				ini.SetValue("Temp9am", "LTime", HiLoToday9am.LowTempTime);
				ini.SetValue("Temp9am", "High", HiLoToday9am.HighTemp);
				ini.SetValue("Temp9am", "HTime", HiLoToday9am.HighTempTime);
				// Pressure
				ini.SetValue("Pressure", "Low", HiLoToday.LowPress);
				ini.SetValue("Pressure", "LTime", HiLoToday.LowPressTime);
				ini.SetValue("Pressure", "High", HiLoToday.HighPress);
				ini.SetValue("Pressure", "HTime", HiLoToday.HighPressTime);
				// rain
				ini.SetValue("Rain", "High", HiLoToday.HighRainRate);
				ini.SetValue("Rain", "HTime", HiLoToday.HighRainRateTime);
				ini.SetValue("Rain", "HourlyHigh", HiLoToday.HighHourlyRain);
				ini.SetValue("Rain", "HHourlyTime", HiLoToday.HighHourlyRainTime);
				ini.SetValue("Rain", "High24h", HiLoToday.HighRain24h);
				ini.SetValue("Rain", "High24hTime", HiLoToday.HighRain24hTime);
				ini.SetValue("Rain", "Yesterday", RainYesterday);
				ini.SetValue("Rain", "Start", RainCounterDayStart);
				ini.SetValue("Rain", "Midnight", MidnightRainCount);
				ini.SetValue("Rain", "Last", RainCounter);
				ini.SetValue("Rain", "LastTip", LastRainTip);
				ini.SetValue("Rain", "ConsecutiveRainDays", ConsecutiveRainDays);
				ini.SetValue("Rain", "ConsecutiveDryDays", ConsecutiveDryDays);
				ini.SetValue("Rain", "RG11Today", RG11RainToday);
				// ET
				ini.SetValue("ET", "Annual", AnnualETTotal);
				ini.SetValue("ET", "Startofday", StartofdayET);
				// humidity
				ini.SetValue("Humidity", "Low", HiLoToday.LowHumidity);
				ini.SetValue("Humidity", "High", HiLoToday.HighHumidity);
				ini.SetValue("Humidity", "LTime", HiLoToday.LowHumidityTime);
				ini.SetValue("Humidity", "HTime", HiLoToday.HighHumidityTime);
				// Solar
				ini.SetValue("Solar", "SunshineHours", SunshineHours);
				ini.SetValue("Solar", "SunshineHoursToMidnight", SunshineToMidnight);
				// heat index
				ini.SetValue("HeatIndex", "High", HiLoToday.HighHeatIndex);
				ini.SetValue("HeatIndex", "HTime", HiLoToday.HighHeatIndexTime);
				// App temp
				ini.SetValue("AppTemp", "Low", HiLoToday.LowAppTemp);
				ini.SetValue("AppTemp", "LTime", HiLoToday.LowAppTempTime);
				ini.SetValue("AppTemp", "High", HiLoToday.HighAppTemp);
				ini.SetValue("AppTemp", "HTime", HiLoToday.HighAppTempTime);
				// Feels like
				ini.SetValue("FeelsLike", "Low", HiLoToday.LowFeelsLike);
				ini.SetValue("FeelsLike", "LTime", HiLoToday.LowFeelsLikeTime);
				ini.SetValue("FeelsLike", "High", HiLoToday.HighFeelsLike);
				ini.SetValue("FeelsLike", "HTime", HiLoToday.HighFeelsLikeTime);
				// Humidex
				ini.SetValue("Humidex", "High", HiLoToday.HighHumidex);
				ini.SetValue("Humidex", "HTime", HiLoToday.HighHumidexTime);
				// wind chill
				ini.SetValue("WindChill", "Low", HiLoToday.LowWindChill);
				ini.SetValue("WindChill", "LTime", HiLoToday.LowWindChillTime);
				// Dewpoint
				ini.SetValue("Dewpoint", "Low", HiLoToday.LowDewPoint);
				ini.SetValue("Dewpoint", "LTime", HiLoToday.LowDewPointTime);
				ini.SetValue("Dewpoint", "High", HiLoToday.HighDewPoint);
				ini.SetValue("Dewpoint", "HTime", HiLoToday.HighDewPointTime);
				// BGT
				ini.SetValue("BGT", "High", HiLoToday.HighBgt);
				ini.SetValue("BGT", "HTime", HiLoToday.HighBgtTime);
				// WBGT
				ini.SetValue("WBGT", "High", HiLoToday.HighWbgt);
				ini.SetValue("WBGT", "HTime", HiLoToday.HighWbgtTime);

				// NOAA report names
				ini.SetValue("NOAA", "LatestMonthlyReport", cumulus.NOAAconf.LatestMonthReport);
				ini.SetValue("NOAA", "LatestYearlyReport", cumulus.NOAAconf.LatestYearReport);

				// Solar
				ini.SetValue("Solar", "HighSolarRad", HiLoToday.HighSolar);
				ini.SetValue("Solar", "HighSolarRadTime", HiLoToday.HighSolarTime);
				ini.SetValue("Solar", "HighUV", HiLoToday.HighUv);
				ini.SetValue("Solar", "HighUVTime", HiLoToday.HighUvTime);
				ini.SetValue("Solar", "SunStart", StartOfDaySunHourCounter);

				// Special Fine Offset data
				ini.SetValue("FineOffset", "FOSensorClockTime", FOSensorClockTime);
				ini.SetValue("FineOffset", "FOStationClockTime", FOStationClockTime);
				ini.SetValue("FineOffset", "FOSolarClockTime", FOSolarClockTime);

				// Records
				ini.SetValue("Records", "Alltime", AlltimeRecordTimestamp);

				// Lightning (GW1000 for now)
				ini.SetValue("Lightning", "Distance", LightningDistance);
				ini.SetValue("Lightning", "LastStrike", LightningTime);
				ini.SetValue("Lightning", "StrikesToday", LightningStrikesToday);
				ini.SetValue("Lightning", "Counter", LightningCounter);

				// Snow accumulation
				for (var i = 1; i < Snow24h.Length; i++)
				{
					if (cumulus.LaserIsSnowSensor[i])
					{
						ini.SetValue("Snow", "LastLaserDepth" + i, LastLaserSnowDepth[i]);
						ini.SetValue("Snow", "Snow24h" + i, Snow24h[i]);
						ini.SetValue("Snow", "SnowSeason" + i, SnowSeason[i]);
					}
				}

				if (Log)
				{
					cumulus.LogMessage("Writing today.ini, LastUpdateTime = " + cumulus.LastUpdateTime + " raindaystart = " + RainCounterDayStart.ToString("F2") + " rain counter = " + RainCounter.ToString("F2"));

					if (cumulus.FineOffsetStation)
					{
						cumulus.LogMessage("WriteTodayFile: Latest FO reading: " + LatestFOReading);
					}
					else if (cumulus.StationType == StationTypes.Instromet)
					{
						cumulus.LogMessage("WriteTodayFile: Latest Instromet reading: " + cumulus.LatestImetReading);
					}
				}

				ini.Flush();
			}
			catch (Exception ex)
			{
				cumulus.LogDebugMessage("Error writing today.ini: " + ex.Message);
			}
		}
	}
}
