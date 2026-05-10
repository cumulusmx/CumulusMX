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
				MetData.ET = AnnualETTotal - StartofdayET;
				cumulus.LogMessage("ReadTodayFile: ET today = " + MetData.ET.ToString(cumulus.ETFormat));
			}
			MetData.ChillHours = ini.GetValue("Temp", "ChillHours", 0.0);

			// NOAA report names
			cumulus.NOAAconf.LatestMonthReport = ini.GetValue("NOAA", "LatestMonthlyReport", "");
			cumulus.NOAAconf.LatestYearReport = ini.GetValue("NOAA", "LatestYearlyReport", "");

			// Solar
			DailyHighLow.Today.HighSolar = ini.GetValue("Solar", "HighSolarRad", 0);
			DailyHighLow.Today.HighSolarTime = ini.GetValue("Solar", "HighSolarRadTime", todayDate);
			DailyHighLow.Today.HighUv = ini.GetValue("Solar", "HighUV", 0.0);
			DailyHighLow.Today.HighUvTime = ini.GetValue("Solar", "HighUVTime", meteoTodayDate);
			MetData.StartOfDaySunHourCounter = ini.GetValue("Solar", "SunStart", -9999.0);
			MetData.RG11RainToday = ini.GetValue("Rain", "RG11Today", 0.0);

			// Wind
			DailyHighLow.Today.HighWind = ini.GetValue("Wind", "Speed", 0.0);
			DailyHighLow.Today.HighWindTime = ini.GetValue("Wind", "SpTime", meteoTodayDate);
			DailyHighLow.Today.HighGust = ini.GetValue("Wind", "Gust", 0.0);
			DailyHighLow.Today.HighGustTime = ini.GetValue("Wind", "Time", meteoTodayDate);
			DailyHighLow.Today.HighGustBearing = ini.GetValue("Wind", "Bearing", 0);
			MetData.WindRunToday = ini.GetValue("Wind", "Windrun", 0.0);
			MetData.DominantWindBearing = ini.GetValue("Wind", "DominantWindBearing", 0);
			MetData.DominantWindBearingMinutes = ini.GetValue("Wind", "DominantWindBearingMinutes", 0);
			MetData.DominantWindBearingX = ini.GetValue("Wind", "DominantWindBearingX", 0.0);
			MetData.DominantWindBearingY = ini.GetValue("Wind", "DominantWindBearingY", 0.0);

			// Temperature
			DailyHighLow.Today.LowTemp = ini.GetValue("Temp", "Low", 999.0);
			DailyHighLow.Today.LowTempTime = ini.GetValue("Temp", "LTime", meteoTodayDate);
			DailyHighLow.Today.HighTemp = ini.GetValue("Temp", "High", -999.0);
			DailyHighLow.Today.HighTempTime = ini.GetValue("Temp", "HTime", meteoTodayDate);
			if (DailyHighLow.Today.HighTemp > -400 && DailyHighLow.Today.LowTemp < 400)
				DailyHighLow.Today.TempRange = DailyHighLow.Today.HighTemp - DailyHighLow.Today.LowTemp;
			else
				DailyHighLow.Today.TempRange = 0;
			MetData.TempTotalToday = ini.GetValue("Temp", "Total", 0.0);
			tempsamplestoday = ini.GetValue("Temp", "Samples", 1);
			MetData.HeatingDegreeDays = ini.GetValue("Temp", "HeatingDegreeDays", 0.0);
			MetData.CoolingDegreeDays = ini.GetValue("Temp", "CoolingDegreeDays", 0.0);
			MetData.GrowingDegreeDaysThisYear1 = ini.GetValue("Temp", "GrowingDegreeDaysThisYear1", 0.0);
			MetData.GrowingDegreeDaysThisYear2 = ini.GetValue("Temp", "GrowingDegreeDaysThisYear2", 0.0);

			// Temperature midnight rollover
			DailyHighLow.TodayMidnight.LowTemp = ini.GetValue("TempMidnight", "Low", 999.0);
			DailyHighLow.TodayMidnight.LowTempTime = ini.GetValue("TempMidnight", "LTime", meteoTodayDate);
			DailyHighLow.TodayMidnight.HighTemp = ini.GetValue("TempMidnight", "High", -999.0);
			DailyHighLow.TodayMidnight.HighTempTime = ini.GetValue("TempMidnight", "HTime", meteoTodayDate);

			// Temperature 9am rollover
			DailyHighLow.Today9am.LowTemp = ini.GetValue("Temp9am", "Low", 999.0);
			DailyHighLow.Today9am.LowTempTime = ini.GetValue("Temp9am", "LTime", meteoTodayDate);
			DailyHighLow.Today9am.HighTemp = ini.GetValue("Temp9am", "High", -999.0);
			DailyHighLow.Today9am.HighTempTime = ini.GetValue("Temp9am", "HTime", meteoTodayDate);

			// Pressure
			DailyHighLow.Today.LowPress = ini.GetValue("Pressure", "Low", 9999.0);
			DailyHighLow.Today.LowPressTime = ini.GetValue("Pressure", "LTime", meteoTodayDate);
			DailyHighLow.Today.HighPress = ini.GetValue("Pressure", "High", 0.0);
			DailyHighLow.Today.HighPressTime = ini.GetValue("Pressure", "HTime", meteoTodayDate);

			// rain
			DailyHighLow.Today.HighRainRate = ini.GetValue("Rain", "High", 0.0);
			DailyHighLow.Today.HighRainRateTime = ini.GetValue("Rain", "HTime", meteoTodayDate);
			DailyHighLow.Today.HighHourlyRain = ini.GetValue("Rain", "HourlyHigh", 0.0);
			DailyHighLow.Today.HighHourlyRainTime = ini.GetValue("Rain", "HHourlyTime", meteoTodayDate);
			DailyHighLow.Today.HighRain24h = ini.GetValue("Rain", "High24h", 0.0);
			DailyHighLow.Today.HighRain24hTime = ini.GetValue("Rain", "High24hTime", meteoTodayDate);
			MetData.RainYesterday = ini.GetValue("Rain", "Yesterday", 0.0);
			RainCounterDayStart = ini.GetValue("Rain", "Start", -1.0);
			MetData.MidnightRainCount = ini.GetValue("Rain", "Midnight", -1.0);
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

			if (MetData.MidnightRainCount < -0.5)
			{
				if (cumulus.RolloverHour == 0 && !initialiseRainDayStart)
				{
					// midnight and rollover are the same
					MetData.MidnightRainCount = RainCounterDayStart;
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

			cumulus.LogMessage($"ReadTodayfile: Rain day start: {RainCounterDayStart:F4}, midnight counter: {MetData.MidnightRainCount:F4}, last counter: {RainCounter:F4}");

			// humidity
			DailyHighLow.Today.LowHumidity = ini.GetValue("Humidity", "Low", 100);
			DailyHighLow.Today.HighHumidity = ini.GetValue("Humidity", "High", 0);
			DailyHighLow.Today.LowHumidityTime = ini.GetValue("Humidity", "LTime", meteoTodayDate);
			DailyHighLow.Today.HighHumidityTime = ini.GetValue("Humidity", "HTime", meteoTodayDate);

			// Solar
			MetData.SunshineHours = ini.GetValue("Solar", "SunshineHours", 0.0);
			MetData.SunshineToMidnight = ini.GetValue("Solar", "SunshineHoursToMidnight", 0.0);

			// heat index
			DailyHighLow.Today.HighHeatIndex = ini.GetValue("HeatIndex", "High", -999.0);
			DailyHighLow.Today.HighHeatIndexTime = ini.GetValue("HeatIndex", "HTime", meteoTodayDate);

			// Apparent temp
			DailyHighLow.Today.HighAppTemp = ini.GetValue("AppTemp", "High", -999.0);
			DailyHighLow.Today.HighAppTempTime = ini.GetValue("AppTemp", "HTime", meteoTodayDate);
			DailyHighLow.Today.LowAppTemp = ini.GetValue("AppTemp", "Low", 999.0);
			DailyHighLow.Today.LowAppTempTime = ini.GetValue("AppTemp", "LTime", meteoTodayDate);

			// wind chill
			DailyHighLow.Today.LowWindChill = ini.GetValue("WindChill", "Low", 999.0);
			DailyHighLow.Today.LowWindChillTime = ini.GetValue("WindChill", "LTime", meteoTodayDate);

			// Dew point
			DailyHighLow.Today.HighDewPoint = ini.GetValue("Dewpoint", "High", -999.0);
			DailyHighLow.Today.HighDewPointTime = ini.GetValue("Dewpoint", "HTime", meteoTodayDate);
			DailyHighLow.Today.LowDewPoint = ini.GetValue("Dewpoint", "Low", 999.0);
			DailyHighLow.Today.LowDewPointTime = ini.GetValue("Dewpoint", "LTime", meteoTodayDate);

			// Feels like
			DailyHighLow.Today.HighFeelsLike = ini.GetValue("FeelsLike", "High", -999.0);
			DailyHighLow.Today.HighFeelsLikeTime = ini.GetValue("FeelsLike", "HTime", meteoTodayDate);
			DailyHighLow.Today.LowFeelsLike = ini.GetValue("FeelsLike", "Low", 999.0);
			DailyHighLow.Today.LowFeelsLikeTime = ini.GetValue("FeelsLike", "LTime", meteoTodayDate);

			// Humidex
			DailyHighLow.Today.HighHumidex = ini.GetValue("Humidex", "High", -999.0);
			DailyHighLow.Today.HighHumidexTime = ini.GetValue("Humidex", "HTime", meteoTodayDate);

			// BGT
			DailyHighLow.Today.HighBgt = ini.GetValue("BGT", "High", Cumulus.DefaultHiVal);
			DailyHighLow.Today.HighBgtTime = ini.GetValue("BGT", "HTime", meteoTodayDate);

			// WBGT
			DailyHighLow.Today.HighWbgt = ini.GetValue("WBGT", "High", -999.0);
			DailyHighLow.Today.HighWbgtTime = ini.GetValue("WBGT", "HTime", meteoTodayDate);

			// Records
			AlltimeRecordTimestamp = ini.GetValue("Records", "Alltime", DateTime.MinValue);

			// Lightning (GW1000 for now)
			MetData.LightningDistance = ini.GetValue("Lightning", "Distance", -1.0);
			MetData.LightningStrikesToday = ini.GetValue("Lightning", "StrikesToday", 0);
			MetData.LightningCounter = ini.GetValue("Lightning", "Counter", MetData.LightningStrikesToday);
			MetData.LightningTime = ini.GetValue("Lightning", "LastStrike", DateTime.MinValue);
			if (MetData.LightningTime.Year == 1900)
			{
				// legacy - used to be 1/1/1900
				MetData.LightningTime = DateTime.MinValue;
			}

			// Snow accumulation
			for (var i = 1; i < MetData.Snow24h.Length; i++)
			{
				MetData.Snow24h[i] = ini.GetValue("Snow", "Snow24h" + i, (double?) null);
				MetData.LastLaserSnowDepth[i] = ini.GetValue("Snow", "LastLaserDepth" + i, (double?) null);
				MetData.SnowSeason[i] = ini.GetValue("Snow", "SnowSeason" + i, (double?) null);
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
				ini.SetValue("Wind", "Speed", DailyHighLow.Today.HighWind);
				ini.SetValue("Wind", "SpTime", DailyHighLow.Today.HighWindTime);
				ini.SetValue("Wind", "Gust", DailyHighLow.Today.HighGust);
				ini.SetValue("Wind", "Time", DailyHighLow.Today.HighGustTime);
				ini.SetValue("Wind", "Bearing", DailyHighLow.Today.HighGustBearing);
				ini.SetValue("Wind", "Direction", CompassPoint(DailyHighLow.Today.HighGustBearing));
				ini.SetValue("Wind", "Windrun", MetData.WindRunToday);
				ini.SetValue("Wind", "DominantWindBearing", MetData.DominantWindBearing);
				ini.SetValue("Wind", "DominantWindBearingMinutes", MetData.DominantWindBearingMinutes);
				ini.SetValue("Wind", "DominantWindBearingX", MetData.DominantWindBearingX);
				ini.SetValue("Wind", "DominantWindBearingY", MetData.DominantWindBearingY);
				// Temperature
				ini.SetValue("Temp", "Low", DailyHighLow.Today.LowTemp);
				ini.SetValue("Temp", "LTime", DailyHighLow.Today.LowTempTime);
				ini.SetValue("Temp", "High", DailyHighLow.Today.HighTemp);
				ini.SetValue("Temp", "HTime", DailyHighLow.Today.HighTempTime);
				ini.SetValue("Temp", "Total", MetData.TempTotalToday);
				ini.SetValue("Temp", "Samples", tempsamplestoday);
				ini.SetValue("Temp", "ChillHours", MetData.ChillHours);
				ini.SetValue("Temp", "HeatingDegreeDays", MetData.HeatingDegreeDays);
				ini.SetValue("Temp", "CoolingDegreeDays", MetData.CoolingDegreeDays);
				ini.SetValue("Temp", "GrowingDegreeDaysThisYear1", MetData.GrowingDegreeDaysThisYear1);
				ini.SetValue("Temp", "GrowingDegreeDaysThisYear2", MetData.GrowingDegreeDaysThisYear2);
				// Temperature midnight rollover
				ini.SetValue("TempMidnight", "Low", DailyHighLow.TodayMidnight.LowTemp);
				ini.SetValue("TempMidnight", "LTime", DailyHighLow.TodayMidnight.LowTempTime);
				ini.SetValue("TempMidnight", "High", DailyHighLow.TodayMidnight.HighTemp);
				ini.SetValue("TempMidnight", "HTime", DailyHighLow.TodayMidnight.HighTempTime);
				// Temperature 9am rollover
				ini.SetValue("Temp9am", "Low", DailyHighLow.Today9am.LowTemp);
				ini.SetValue("Temp9am", "LTime", DailyHighLow.Today9am.LowTempTime);
				ini.SetValue("Temp9am", "High", DailyHighLow.Today9am.HighTemp);
				ini.SetValue("Temp9am", "HTime", DailyHighLow.Today9am.HighTempTime);
				// Pressure
				ini.SetValue("Pressure", "Low", DailyHighLow.Today.LowPress);
				ini.SetValue("Pressure", "LTime", DailyHighLow.Today.LowPressTime);
				ini.SetValue("Pressure", "High", DailyHighLow.Today.HighPress);
				ini.SetValue("Pressure", "HTime", DailyHighLow.Today.HighPressTime);
				// rain
				ini.SetValue("Rain", "High", DailyHighLow.Today.HighRainRate);
				ini.SetValue("Rain", "HTime", DailyHighLow.Today.HighRainRateTime);
				ini.SetValue("Rain", "HourlyHigh", DailyHighLow.Today.HighHourlyRain);
				ini.SetValue("Rain", "HHourlyTime", DailyHighLow.Today.HighHourlyRainTime);
				ini.SetValue("Rain", "High24h", DailyHighLow.Today.HighRain24h);
				ini.SetValue("Rain", "High24hTime", DailyHighLow.Today.HighRain24hTime);
				ini.SetValue("Rain", "Yesterday", MetData.RainYesterday);
				ini.SetValue("Rain", "Start", RainCounterDayStart);
				ini.SetValue("Rain", "Midnight", MetData.MidnightRainCount);
				ini.SetValue("Rain", "Last", RainCounter);
				ini.SetValue("Rain", "LastTip", LastRainTip);
				ini.SetValue("Rain", "ConsecutiveRainDays", ConsecutiveRainDays);
				ini.SetValue("Rain", "ConsecutiveDryDays", ConsecutiveDryDays);
				ini.SetValue("Rain", "RG11Today", MetData.RG11RainToday);
				// ET
				ini.SetValue("ET", "Annual", AnnualETTotal);
				ini.SetValue("ET", "Startofday", StartofdayET);
				// humidity
				ini.SetValue("Humidity", "Low", DailyHighLow.Today.LowHumidity);
				ini.SetValue("Humidity", "High", DailyHighLow.Today.HighHumidity);
				ini.SetValue("Humidity", "LTime", DailyHighLow.Today.LowHumidityTime);
				ini.SetValue("Humidity", "HTime", DailyHighLow.Today.HighHumidityTime);
				// Solar
				ini.SetValue("Solar", "SunshineHours", MetData.SunshineHours);
				ini.SetValue("Solar", "SunshineHoursToMidnight", MetData.SunshineToMidnight);
				// heat index
				ini.SetValue("HeatIndex", "High", DailyHighLow.Today.HighHeatIndex);
				ini.SetValue("HeatIndex", "HTime", DailyHighLow.Today.HighHeatIndexTime);
				// App temp
				ini.SetValue("AppTemp", "Low", DailyHighLow.Today.LowAppTemp);
				ini.SetValue("AppTemp", "LTime", DailyHighLow.Today.LowAppTempTime);
				ini.SetValue("AppTemp", "High", DailyHighLow.Today.HighAppTemp);
				ini.SetValue("AppTemp", "HTime", DailyHighLow.Today.HighAppTempTime);
				// Feels like
				ini.SetValue("FeelsLike", "Low", DailyHighLow.Today.LowFeelsLike);
				ini.SetValue("FeelsLike", "LTime", DailyHighLow.Today.LowFeelsLikeTime);
				ini.SetValue("FeelsLike", "High", DailyHighLow.Today.HighFeelsLike);
				ini.SetValue("FeelsLike", "HTime", DailyHighLow.Today.HighFeelsLikeTime);
				// Humidex
				ini.SetValue("Humidex", "High", DailyHighLow.Today.HighHumidex);
				ini.SetValue("Humidex", "HTime", DailyHighLow.Today.HighHumidexTime);
				// wind chill
				ini.SetValue("WindChill", "Low", DailyHighLow.Today.LowWindChill);
				ini.SetValue("WindChill", "LTime", DailyHighLow.Today.LowWindChillTime);
				// Dewpoint
				ini.SetValue("Dewpoint", "Low", DailyHighLow.Today.LowDewPoint);
				ini.SetValue("Dewpoint", "LTime", DailyHighLow.Today.LowDewPointTime);
				ini.SetValue("Dewpoint", "High", DailyHighLow.Today.HighDewPoint);
				ini.SetValue("Dewpoint", "HTime", DailyHighLow.Today.HighDewPointTime);
				// BGT
				ini.SetValue("BGT", "High", DailyHighLow.Today.HighBgt);
				ini.SetValue("BGT", "HTime", DailyHighLow.Today.HighBgtTime);
				// WBGT
				ini.SetValue("WBGT", "High", DailyHighLow.Today.HighWbgt);
				ini.SetValue("WBGT", "HTime", DailyHighLow.Today.HighWbgtTime);

				// NOAA report names
				ini.SetValue("NOAA", "LatestMonthlyReport", cumulus.NOAAconf.LatestMonthReport);
				ini.SetValue("NOAA", "LatestYearlyReport", cumulus.NOAAconf.LatestYearReport);

				// Solar
				ini.SetValue("Solar", "HighSolarRad", DailyHighLow.Today.HighSolar);
				ini.SetValue("Solar", "HighSolarRadTime", DailyHighLow.Today.HighSolarTime);
				ini.SetValue("Solar", "HighUV", DailyHighLow.Today.HighUv);
				ini.SetValue("Solar", "HighUVTime", DailyHighLow.Today.HighUvTime);
				ini.SetValue("Solar", "SunStart", MetData.StartOfDaySunHourCounter);

				// Special Fine Offset data
				ini.SetValue("FineOffset", "FOSensorClockTime", FOSensorClockTime);
				ini.SetValue("FineOffset", "FOStationClockTime", FOStationClockTime);
				ini.SetValue("FineOffset", "FOSolarClockTime", FOSolarClockTime);

				// Records
				ini.SetValue("Records", "Alltime", AlltimeRecordTimestamp);

				// Lightning (GW1000 for now)
				ini.SetValue("Lightning", "Distance", MetData.LightningDistance);
				ini.SetValue("Lightning", "LastStrike", MetData.LightningTime);
				ini.SetValue("Lightning", "StrikesToday", MetData.LightningStrikesToday);
				ini.SetValue("Lightning", "Counter", MetData.LightningCounter);

				// Snow accumulation
				for (var i = 1; i < MetData.Snow24h.Length; i++)
				{
					if (cumulus.LaserIsSnowSensor[i])
					{
						ini.SetValue("Snow", "LastLaserDepth" + i, MetData.LastLaserSnowDepth[i]);
						ini.SetValue("Snow", "Snow24h" + i, MetData.Snow24h[i]);
						ini.SetValue("Snow", "SnowSeason" + i, MetData.SnowSeason[i]);
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
