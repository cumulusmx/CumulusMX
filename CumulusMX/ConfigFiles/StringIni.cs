using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	public partial class Cumulus
	{
		public void WriteStringsFile()
		{
			LogMessage("Writing strings.ini file");

			IniFile ini = new IniFile("strings.ini");

			// forecast
			ini.SetValue("Forecast", "notavailable", Trans.ForecastNotAvailable);

			ini.SetValue("Forecast", "exceptional", Trans.Exceptional);
			for (var i = 0; i <= 25; i++)
			{
				ini.SetValue("Forecast", "forecast" + (i + 1), Trans.zForecast[i]);
			}
			// moon phases
			ini.SetValue("MoonPhases", "Newmoon", Trans.NewMoon);
			ini.SetValue("MoonPhases", "WaxingCrescent", Trans.WaxingCrescent);
			ini.SetValue("MoonPhases", "FirstQuarter", Trans.FirstQuarter);
			ini.SetValue("MoonPhases", "WaxingGibbous", Trans.WaxingGibbous);
			ini.SetValue("MoonPhases", "Fullmoon", Trans.FullMoon);
			ini.SetValue("MoonPhases", "WaningGibbous", Trans.WaningGibbous);
			ini.SetValue("MoonPhases", "LastQuarter", Trans.LastQuarter);
			ini.SetValue("MoonPhases", "WaningCrescent", Trans.WaningCrescent);
			// Beaufort
			ini.SetValue("Beaufort", "Calm", Trans.Calm);
			ini.SetValue("Beaufort", "Lightair", Trans.Lightair);
			ini.SetValue("Beaufort", "Lightbreeze", Trans.Lightbreeze);
			ini.SetValue("Beaufort", "Gentlebreeze", Trans.Gentlebreeze);
			ini.SetValue("Beaufort", "Moderatebreeze", Trans.Moderatebreeze);
			ini.SetValue("Beaufort", "Freshbreeze", Trans.Freshbreeze);
			ini.SetValue("Beaufort", "Strongbreeze", Trans.Strongbreeze);
			ini.SetValue("Beaufort", "Neargale", Trans.Neargale);
			ini.SetValue("Beaufort", "Gale", Trans.Gale);
			ini.SetValue("Beaufort", "Stronggale", Trans.Stronggale);
			ini.SetValue("Beaufort", "Storm", Trans.Storm);
			ini.SetValue("Beaufort", "Violentstorm", Trans.Violentstorm);
			ini.SetValue("Beaufort", "Hurricane", Trans.Hurricane);
			ini.SetValue("Beaufort", "Unknown", Trans.Unknown);
			// trends
			ini.SetValue("Trends", "Risingveryrapidly", Trans.Risingveryrapidly);
			ini.SetValue("Trends", "Risingquickly", Trans.Risingquickly);
			ini.SetValue("Trends", "Rising", Trans.Rising);
			ini.SetValue("Trends", "Risingslowly", Trans.Risingslowly);
			ini.SetValue("Trends", "Steady", Trans.Steady);
			ini.SetValue("Trends", "Fallingslowly", Trans.Fallingslowly);
			ini.SetValue("Trends", "Falling", Trans.Falling);
			ini.SetValue("Trends", "Fallingquickly", Trans.Fallingquickly);
			ini.SetValue("Trends", "Fallingveryrapidly", Trans.Fallingveryrapidly);
			// compass points
			ini.SetValue("Compass", "N", Trans.compassp[0]);
			ini.SetValue("Compass", "NNE", Trans.compassp[1]);
			ini.SetValue("Compass", "NE", Trans.compassp[2]);
			ini.SetValue("Compass", "ENE", Trans.compassp[3]);
			ini.SetValue("Compass", "E", Trans.compassp[4]);
			ini.SetValue("Compass", "ESE", Trans.compassp[5]);
			ini.SetValue("Compass", "SE", Trans.compassp[6]);
			ini.SetValue("Compass", "SSE", Trans.compassp[7]);
			ini.SetValue("Compass", "S", Trans.compassp[8]);
			ini.SetValue("Compass", "SSW", Trans.compassp[9]);
			ini.SetValue("Compass", "SW", Trans.compassp[10]);
			ini.SetValue("Compass", "WSW", Trans.compassp[11]);
			ini.SetValue("Compass", "W", Trans.compassp[12]);
			ini.SetValue("Compass", "WNW", Trans.compassp[13]);
			ini.SetValue("Compass", "NW", Trans.compassp[14]);
			ini.SetValue("Compass", "NNW", Trans.compassp[15]);

			for (var i = 0; i < 4; i++)
			{
				// air quality captions (for Extra Sensor Data screen)
				ini.SetValue("AirQualityCaptions", "Sensor" + (i + 1), Trans.AirQualityCaptions[i]);
				ini.SetValue("AirQualityCaptions", "SensorAvg" + (i + 1), Trans.AirQualityAvgCaptions[i]);
				ini.SetValue("AirQualityCaptions", "Sensor10-" + (i + 1), Trans.AirQuality10Captions[i]);
				ini.SetValue("AirQualityCaptions", "Sensor10Avg" + (i + 1), Trans.AirQuality10AvgCaptions[i]);
			}

			for (var i = 0; i < 8; i++)
			{
				// leaf wetness captions (for Extra Sensor Data screen)
				ini.SetValue("LeafWetnessCaptions", "Sensor" + (i + 1), Trans.LeafWetnessCaptions[i]);

				// User temperature captions (for Extra Sensor Data screen)
				ini.SetValue("UserTempCaptions", "Sensor" + (i + 1), Trans.UserTempCaptions[i]);
			}

			for (var i = 0; i < 16; i++)
			{
				var name = "Sensor" + (i + 1);
				// Extra temperature captions (for Extra Sensor Data screen)
				ini.SetValue("ExtraTempCaptions", name, Trans.ExtraTempCaptions[i]);

				// Extra humidity captions (for Extra Sensor Data screen)
				ini.SetValue("ExtraHumCaptions", name, Trans.ExtraHumCaptions[i]);

				// Extra dew point captions (for Extra Sensor Data screen)
				ini.SetValue("ExtraDPCaptions", name, Trans.ExtraDPCaptions[i]);
			}

			for (var i = 0; i < 16; i++)
			{
				var name = "Sensor" + (i + 1);
				// soil temp captions (for Extra Sensor Data screen)
				ini.SetValue("SoilTempCaptions", name, Trans.SoilTempCaptions[i]);

				// soil moisture captions (for Extra Sensor Data screen)
				ini.SetValue("SoilMoistureCaptions", name, Trans.SoilMoistureCaptions[i]);

				// soil EC captions (for Extra Sensor Data screen)
				ini.SetValue("SoilEcCaptions", name, Trans.SoilEcCaptions[i]);
			}

			for (var i = 0; i < 4; i++)
			{
				ini.SetValue("LaserCaptions", "Sensor" + (i + 1), Trans.LaserCaptions[i]);
			}


			// CO2 captions - Ecowitt WH45 sensor
			ini.SetValue("CO2Captions", "CO2-Current", Trans.CO2_CurrentCaption);
			ini.SetValue("CO2Captions", "CO2-24hr", Trans.CO2_24HourCaption);
			ini.SetValue("CO2Captions", "CO2-Pm2p5", Trans.CO2_pm2p5Caption);
			ini.SetValue("CO2Captions", "CO2-Pm2p5-24hr", Trans.CO2_pm2p5_24hrCaption);
			ini.SetValue("CO2Captions", "CO2-Pm10", Trans.CO2_pm10Caption);
			ini.SetValue("CO2Captions", "CO2-Pm10-24hr", Trans.CO2_pm10_24hrCaption);
			ini.SetValue("CO2Captions", "CO2-Temperature", Trans.CO2_TemperatureCaption);
			ini.SetValue("CO2Captions", "CO2-Humidity", Trans.CO2_HumidityCaption);

			// Snow
			ini.SetValue("Snow", "SnowDepth", Trans.SnowDepth);
			ini.SetValue("Snow", "Snow24h", Trans.Snow24h);

			ini.SetValue("Solar", "LessDaylightTomorrow", Trans.thereWillBeMinSLessDaylightTomorrow);
			ini.SetValue("Solar", "MoreDaylightTomorrow", Trans.thereWillBeMinSMoreDaylightTomorrow);

			// Davis forecast 1
			for (var i = 0; i <= 26; i++)
			{
				ini.SetValue("DavisForecast1", "forecast" + (i + 1), Trans.DavisForecast1[i]);
			}

			// Davis forecast 2
			for (var i = 0; i <= 18; i++)
			{
				ini.SetValue("DavisForecast2", "forecast" + (i + 1), Trans.DavisForecast2[i]);
			}

			// Davis forecast 3
			for (var i = 0; i <= 6; i++)
			{
				ini.SetValue("DavisForecast3", "forecast" + (i + 1), Trans.DavisForecast3[i]);
			}

			// alarm emails
			ini.SetValue("AlarmEmails", "subject", Trans.AlarmEmailSubject);
			ini.SetValue("AlarmEmails", "preamble", Trans.AlarmEmailPreamble);
			ini.SetValue("AlarmEmails", "windGustAbove", HighGustAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "pressureAbove", HighPressAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "tempAbove", HighTempAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "pressBelow", LowPressAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "tempBelow", LowTempAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "pressDown", PressChangeAlarm.EmailMsgDn);
			ini.SetValue("AlarmEmails", "pressUp", PressChangeAlarm.EmailMsgUp);
			ini.SetValue("AlarmEmails", "rainAbove", HighRainTodayAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "rainRateAbove", HighRainRateAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "sensorLost", SensorAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "tempDown", TempChangeAlarm.EmailMsgDn);
			ini.SetValue("AlarmEmails", "tempUp", TempChangeAlarm.EmailMsgUp);
			ini.SetValue("AlarmEmails", "windAbove", HighWindAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "dataStopped", DataStoppedAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "batteryLow", BatteryLowAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "dataSpike", SpikeAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "upgrade", UpgradeAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "firmware", FirmwareAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "httpStopped", ThirdPartyAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "mySqlStopped", MySqlUploadAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "isRaining", IsRainingAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "newRecord", NewRecordAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "ftpStopped", FtpAlarm.EmailMsg);
			ini.SetValue("AlarmEmails", "genError", ErrorAlarm.EmailMsg);

			// alarm names
			ini.SetValue("AlarmNames", "windGustAbove", HighGustAlarm.Name);
			ini.SetValue("AlarmNames", "pressureAbove", HighPressAlarm.Name);
			ini.SetValue("AlarmNames", "tempAbove", HighTempAlarm.Name);
			ini.SetValue("AlarmNames", "pressBelow", LowPressAlarm.Name);
			ini.SetValue("AlarmNames", "tempBelow", LowTempAlarm.Name);
			ini.SetValue("AlarmNames", "pressDown", PressChangeAlarm.NameDown);
			ini.SetValue("AlarmNames", "pressUp", PressChangeAlarm.NameUp);
			ini.SetValue("AlarmNames", "rainAbove", HighRainTodayAlarm.Name);
			ini.SetValue("AlarmNames", "rainRateAbove", HighRainRateAlarm.Name);
			ini.SetValue("AlarmNames", "sensorLost", SensorAlarm.Name);
			ini.SetValue("AlarmNames", "tempDown", TempChangeAlarm.NameDown);
			ini.SetValue("AlarmNames", "tempUp", TempChangeAlarm.NameUp);
			ini.SetValue("AlarmNames", "windAbove", HighWindAlarm.Name);
			ini.SetValue("AlarmNames", "dataStopped", DataStoppedAlarm.Name);
			ini.SetValue("AlarmNames", "batteryLow", BatteryLowAlarm.Name);
			ini.SetValue("AlarmNames", "dataSpike", SpikeAlarm.Name);
			ini.SetValue("AlarmNames", "upgrade", UpgradeAlarm.Name);
			ini.SetValue("AlarmNames", "firmware", FirmwareAlarm.Name);
			ini.SetValue("AlarmNames", "httpStopped", ThirdPartyAlarm.Name);
			ini.SetValue("AlarmNames", "mySqlStopped", MySqlUploadAlarm.Name);
			ini.SetValue("AlarmNames", "isRaining", IsRainingAlarm.Name);
			ini.SetValue("AlarmNames", "newRecord", NewRecordAlarm.Name);
			ini.SetValue("AlarmNames", "ftpStopped", FtpAlarm.Name);
			ini.SetValue("AlarmNames", "genError", ErrorAlarm.Name);

			// web tag defaults
			ini.SetValue("WebTags", "GeneralTimeDate", Trans.WebTagGenTimeDate);
			ini.SetValue("WebTags", "GeneralDate", Trans.WebTagGenDate);
			ini.SetValue("WebTags", "GeneralTime", Trans.WebTagGenTime);
			ini.SetValue("WebTags", "RecordDate", Trans.WebTagRecDate);
			ini.SetValue("WebTags", "RecordTimeDate", Trans.WebTagRecTimeDate);
			ini.SetValue("WebTags", "RecordDryWetDate", Trans.WebTagRecDryWetDate);
			ini.SetValue("WebTags", "ElapsedTime", Trans.WebTagElapsedTime);

			// Hi/Lo Captions
			foreach (var key in Trans.HiLoCaptions.Keys)
			{
				ini.SetValue("HiLoCaptions", key, Trans.HiLoCaptions[key]);
			}

			ini.Flush();

			LogMessage("Completed writing strings.ini file");
		}
	}
}
