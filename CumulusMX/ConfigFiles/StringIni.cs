using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CumulusMX
{
	public partial class Cumulus
	{
		private void ReadStringsFile()
		{
			IniFile ini = new IniFile("strings.ini");

			// forecast
			Trans.ForecastNotAvailable = ini.GetValue("Forecast", "notavailable", "Not available");

			Trans.Exceptional = ini.GetValue("Forecast", "exceptional", "Exceptional Weather");
			for (var i = 0; i <= 25; i++)
			{
				Trans.zForecast[i] = ini.GetValue("Forecast", "forecast" + (i + 1), Trans.zForecast[i]);
			}
			// moon phases
			Trans.NewMoon = ini.GetValue("MoonPhases", "Newmoon", "New Moon");
			Trans.WaxingCrescent = ini.GetValue("MoonPhases", "WaxingCrescent", "Waxing Crescent");
			Trans.FirstQuarter = ini.GetValue("MoonPhases", "FirstQuarter", "First Quarter");
			Trans.WaxingGibbous = ini.GetValue("MoonPhases", "WaxingGibbous", "Waxing Gibbous");
			Trans.FullMoon = ini.GetValue("MoonPhases", "Fullmoon", "Full Moon");
			Trans.WaningGibbous = ini.GetValue("MoonPhases", "WaningGibbous", "Waning Gibbous");
			Trans.LastQuarter = ini.GetValue("MoonPhases", "LastQuarter", "Last Quarter");
			Trans.WaningCrescent = ini.GetValue("MoonPhases", "WaningCrescent", "Waning Crescent");
			// Beaufort
			Trans.Calm = ini.GetValue("Beaufort", "Calm", "Calm");
			Trans.Lightair = ini.GetValue("Beaufort", "Lightair", "Light air");
			Trans.Lightbreeze = ini.GetValue("Beaufort", "Lightbreeze", "Light breeze");
			Trans.Gentlebreeze = ini.GetValue("Beaufort", "Gentlebreeze", "Gentle breeze");
			Trans.Moderatebreeze = ini.GetValue("Beaufort", "Moderatebreeze", "Moderate breeze");
			Trans.Freshbreeze = ini.GetValue("Beaufort", "Freshbreeze", "Fresh breeze");
			Trans.Strongbreeze = ini.GetValue("Beaufort", "Strongbreeze", "Strong breeze");
			Trans.Neargale = ini.GetValue("Beaufort", "Neargale", "Near gale");
			Trans.Gale = ini.GetValue("Beaufort", "Gale", "Gale");
			Trans.Stronggale = ini.GetValue("Beaufort", "Stronggale", "Strong gale");
			Trans.Storm = ini.GetValue("Beaufort", "Storm", "Storm");
			Trans.Violentstorm = ini.GetValue("Beaufort", "Violentstorm", "Violent storm");
			Trans.Hurricane = ini.GetValue("Beaufort", "Hurricane", "Hurricane");
			Trans.Unknown = ini.GetValue("Beaufort", "Unknown", "UNKNOWN");
			// trends
			Trans.Risingveryrapidly = ini.GetValue("Trends", "Risingveryrapidly", "Rising very rapidly");
			Trans.Risingquickly = ini.GetValue("Trends", "Risingquickly", "Rising quickly");
			Trans.Rising = ini.GetValue("Trends", "Rising", "Rising");
			Trans.Risingslowly = ini.GetValue("Trends", "Risingslowly", "Rising slowly");
			Trans.Steady = ini.GetValue("Trends", "Steady", "Steady");
			Trans.Fallingslowly = ini.GetValue("Trends", "Fallingslowly", "Falling slowly");
			Trans.Falling = ini.GetValue("Trends", "Falling", "Falling");
			Trans.Fallingquickly = ini.GetValue("Trends", "Fallingquickly", "Falling quickly");
			Trans.Fallingveryrapidly = ini.GetValue("Trends", "Fallingveryrapidly", "Falling very rapidly");
			// compass points
			Trans.compassp[0] = ini.GetValue("Compass", "N", "N");
			Trans.compassp[1] = ini.GetValue("Compass", "NNE", "NNE");
			Trans.compassp[2] = ini.GetValue("Compass", "NE", "NE");
			Trans.compassp[3] = ini.GetValue("Compass", "ENE", "ENE");
			Trans.compassp[4] = ini.GetValue("Compass", "E", "E");
			Trans.compassp[5] = ini.GetValue("Compass", "ESE", "ESE");
			Trans.compassp[6] = ini.GetValue("Compass", "SE", "SE");
			Trans.compassp[7] = ini.GetValue("Compass", "SSE", "SSE");
			Trans.compassp[8] = ini.GetValue("Compass", "S", "S");
			Trans.compassp[9] = ini.GetValue("Compass", "SSW", "SSW");
			Trans.compassp[10] = ini.GetValue("Compass", "SW", "SW");
			Trans.compassp[11] = ini.GetValue("Compass", "WSW", "WSW");
			Trans.compassp[12] = ini.GetValue("Compass", "W", "W");
			Trans.compassp[13] = ini.GetValue("Compass", "WNW", "WNW");
			Trans.compassp[14] = ini.GetValue("Compass", "NW", "NW");
			Trans.compassp[15] = ini.GetValue("Compass", "NNW", "NNW");

			for (var i = 0; i < 4; i++)
			{
				// air quality captions (for Extra Sensor Data screen)
				Trans.AirQualityCaptions[i] = ini.GetValue("AirQualityCaptions", "Sensor" + (i + 1), "pm2.5 Sensor " + (i + 1));
				Trans.AirQualityAvgCaptions[i] = ini.GetValue("AirQualityCaptions", "SensorAvg" + (i + 1), "pm2.5 Avg Sensor " + (i + 1));
				Trans.AirQuality10Captions[i] = ini.GetValue("AirQualityCaptions", "Sensor10-" + (i + 1), "pm10 Sensor " + (i + 1));
				Trans.AirQuality10AvgCaptions[i] = ini.GetValue("AirQualityCaptions", "Sensor10Avg" + (i + 1), $"pm10 Sensor Avg " + (i + 1));

				Trans.LaserCaptions[i] = ini.GetValue("LaserCaptions", "Sensor" + (i + 1), "Sensor " + (i + 1));
			}

			for (var i = 0; i < 8; i++)
			{
				var name = "Sensor" + (i + 1);
				var caption = "Sensor " + (i + 1);

				// leaf wetness captions (for Extra Sensor Data screen)
				Trans.LeafWetnessCaptions[i] = ini.GetValue("LeafWetnessCaptions", name, caption);

				// User temperature captions (for Extra Sensor Data screen)
				Trans.UserTempCaptions[i] = ini.GetValue("UserTempCaptions", name, caption);
			}

			for (var i = 0; i < 16; i++)
			{
				var name = "Sensor" + (i + 1);
				var caption = "Sensor " + (i + 1);

				// Extra temperature captions (for Extra Sensor Data screen)
				Trans.ExtraTempCaptions[i] = ini.GetValue("ExtraTempCaptions", name, caption);

				// Extra humidity captions (for Extra Sensor Data screen)
				Trans.ExtraHumCaptions[i] = ini.GetValue("ExtraHumCaptions", name, caption);

				// Extra dew point captions (for Extra Sensor Data screen)
				Trans.ExtraDPCaptions[i] = ini.GetValue("ExtraDPCaptions", name, caption);

				// soil temp captions (for Extra Sensor Data screen)
				Trans.SoilTempCaptions[i] = ini.GetValue("SoilTempCaptions", name, caption);

				// soil moisture captions (for Extra Sensor Data screen)
				Trans.SoilMoistureCaptions[i] = ini.GetValue("SoilMoistureCaptions", name, caption);

				// soil EC captions (for Extra Sensor Data screen)
				Trans.SoilEcCaptions[i] = ini.GetValue("SoilEcCaptions", name, caption);
			}

			// CO2 captions - Ecowitt WH45 sensor
			Trans.CO2_CurrentCaption = ini.GetValue("CO2Captions", "CO2-Current", "CO&#8322 Current");
			Trans.CO2_24HourCaption = ini.GetValue("CO2Captions", "CO2-24hr", "CO&#8322 24h avg");
			Trans.CO2_pm2p5Caption = ini.GetValue("CO2Captions", "CO2-Pm2p5", "PM 2.5");
			Trans.CO2_pm2p5_24hrCaption = ini.GetValue("CO2Captions", "CO2-Pm2p5-24hr", "PM 2.5 24h avg");
			Trans.CO2_pm10Caption = ini.GetValue("CO2Captions", "CO2-Pm10", "PM 10");
			Trans.CO2_pm10_24hrCaption = ini.GetValue("CO2Captions", "CO2-Pm10-24hr", "PM 10 24h avg");
			Trans.CO2_TemperatureCaption = ini.GetValue("CO2Captions", "CO2-Temperature", "Temperature");
			Trans.CO2_HumidityCaption = ini.GetValue("CO2Captions", "CO2-Humidity", "Humidity");

			// Snow
			Trans.SnowDepth = ini.GetValue("Snow", "SnowDepth", "Snow Depth");
			Trans.Snow24h = ini.GetValue("Snow", "Snow24h", "Snowfall 24h");

			Trans.thereWillBeMinSLessDaylightTomorrow = ini.GetValue("Solar", "LessDaylightTomorrow", "There will be {0}min {1}s less daylight tomorrow");
			Trans.thereWillBeMinSMoreDaylightTomorrow = ini.GetValue("Solar", "MoreDaylightTomorrow", "There will be {0}min {1}s more daylight tomorrow");

			// Davis forecast 1
			Trans.DavisForecast1[0] = ini.GetValue("DavisForecast1", "forecast1", Trans.DavisForecast1[0]);
			for (var i = 1; i <= 25; i++)
			{
				Trans.DavisForecast1[i] = ini.GetValue("DavisForecast1", "forecast" + (i + 1), Trans.DavisForecast1[i]) + " ";
			}
			Trans.DavisForecast1[26] = ini.GetValue("DavisForecast1", "forecast27", Trans.DavisForecast1[26]);

			// Davis forecast 2
			Trans.DavisForecast2[0] = ini.GetValue("DavisForecast2", "forecast1", Trans.DavisForecast2[0]);
			for (var i = 1; i <= 18; i++)
			{
				Trans.DavisForecast2[i] = ini.GetValue("DavisForecast2", "forecast" + (i + 1), Trans.DavisForecast2[i]) + " ";
			}

			// Davis forecast 3
			for (var i = 0; i <= 6; i++)
			{
				Trans.DavisForecast3[i] = ini.GetValue("DavisForecast3", "forecast" + (i + 1), Trans.DavisForecast3[i]);
			}

			// alarm emails
			Trans.AlarmEmailSubject = ini.GetValue("AlarmEmails", "subject", "Cumulus MX Alarm");
			Trans.AlarmEmailPreamble = ini.GetValue("AlarmEmails", "preamble", "A Cumulus MX alarm has been triggered.");

			HighGustAlarm.EmailMsg = ini.GetValue("AlarmEmails", "windGustAbove", "A wind gust above {0} {1} has occurred.");
			HighPressAlarm.EmailMsg = ini.GetValue("AlarmEmails", "pressureAbove", "The pressure has risen above {0} {1}.");
			HighTempAlarm.EmailMsg = ini.GetValue("AlarmEmails", "tempAbove", "The temperature has risen above {0} {1}.");
			LowPressAlarm.EmailMsg = ini.GetValue("AlarmEmails", "pressBelow", "The pressure has fallen below {0} {1}.");
			LowTempAlarm.EmailMsg = ini.GetValue("AlarmEmails", "tempBelow", "The temperature has fallen below {0} {1}.");
			PressChangeAlarm.EmailMsgDn = ini.GetValue("AlarmEmails", "pressDown", "The pressure has decreased by more than {0} {1}.");
			PressChangeAlarm.EmailMsgUp = ini.GetValue("AlarmEmails", "pressUp", "The pressure has increased by more than {0} {1}.");
			HighRainTodayAlarm.EmailMsg = ini.GetValue("AlarmEmails", "rainAbove", "The rainfall today has exceeded {0} {1}.");
			HighRainRateAlarm.EmailMsg = ini.GetValue("AlarmEmails", "rainRateAbove", "The rainfall rate has exceeded {0} {1}.");
			SensorAlarm.EmailMsg = ini.GetValue("AlarmEmails", "sensorLost", "Contact has been lost with a remote sensor,");
			TempChangeAlarm.EmailMsgDn = ini.GetValue("AlarmEmails", "tempDown", "The temperature decreased by more than {0} {1}.");
			TempChangeAlarm.EmailMsgUp = ini.GetValue("AlarmEmails", "tempUp", "The temperature has increased by more than {0} {1}.");
			HighWindAlarm.EmailMsg = ini.GetValue("AlarmEmails", "windAbove", "The average wind speed has exceeded {0} {1}.");
			DataStoppedAlarm.EmailMsg = ini.GetValue("AlarmEmails", "dataStopped", "Cumulus has stopped receiving data from your weather station.");
			BatteryLowAlarm.EmailMsg = ini.GetValue("AlarmEmails", "batteryLow", "A low battery condition has been detected.");
			SpikeAlarm.EmailMsg = ini.GetValue("AlarmEmails", "dataSpike", "A data spike from your weather station has been suppressed.");
			UpgradeAlarm.EmailMsg = ini.GetValue("AlarmEmails", "upgrade", "An upgrade to Cumulus MX is now available.");
			FirmwareAlarm.EmailMsg = ini.GetValue("AlarmEmails", "firmware", "A station firmware update is now available.");
			ThirdPartyAlarm.EmailMsg = ini.GetValue("AlarmEmails", "httpStopped", "Third party HTTP uploads are failing.");
			MySqlUploadAlarm.EmailMsg = ini.GetValue("AlarmEmails", "mySqlStopped", "MySQL uploads are failing.");
			IsRainingAlarm.EmailMsg = ini.GetValue("AlarmEmails", "isRaining", "It has started to rain.");
			NewRecordAlarm.EmailMsg = ini.GetValue("AlarmEmails", "newRecord", "A new all-time record has been set.");
			FtpAlarm.EmailMsg = ini.GetValue("AlarmEmails", "ftpStopped", "FTP uploads have stopped.");
			ErrorAlarm.EmailMsg = ini.GetValue("AlarmEmails", "genError", "An error has occurred in Cumulus MX.");


			// alarm names
			HighGustAlarm.Name = ini.GetValue("AlarmNames", "windGustAbove", "High Gust");
			HighPressAlarm.Name = ini.GetValue("AlarmNames", "pressureAbove", "High Pressure");
			HighTempAlarm.Name = ini.GetValue("AlarmNames", "tempAbove", "High Temperature");
			LowPressAlarm.Name = ini.GetValue("AlarmNames", "pressBelow", "Low Pressure");
			LowTempAlarm.Name = ini.GetValue("AlarmNames", "tempBelow", "Low Temperature");
			PressChangeAlarm.NameDown = ini.GetValue("AlarmNames", "pressDown", "Pressure Falling");
			PressChangeAlarm.NameUp = ini.GetValue("AlarmNames", "pressUp", "Pressure Rising");
			HighRainTodayAlarm.Name = ini.GetValue("AlarmNames", "rainAbove", "Rainfall Today");
			HighRainRateAlarm.Name = ini.GetValue("AlarmNames", "rainRateAbove", "High Rainfall Rate");
			SensorAlarm.Name = ini.GetValue("AlarmNames", "sensorLost", "Sensor Data Stopped");
			TempChangeAlarm.NameDown = ini.GetValue("AlarmNames", "tempDown", "Temp Falling");
			TempChangeAlarm.NameUp = ini.GetValue("AlarmNames", "tempUp", "Temp Rising");
			HighWindAlarm.Name = ini.GetValue("AlarmNames", "windAbove", "High Wind");
			DataStoppedAlarm.Name = ini.GetValue("AlarmNames", "dataStopped", "Data Stopped");
			BatteryLowAlarm.Name = ini.GetValue("AlarmNames", "batteryLow", "Battery Low");
			SpikeAlarm.Name = ini.GetValue("AlarmNames", "dataSpike", "Data Spike");
			UpgradeAlarm.Name = ini.GetValue("AlarmNames", "upgrade", "CMX Upgrade");
			FirmwareAlarm.Name = ini.GetValue("AlarmNames", "firmware", "Firmware Upgrade");
			ThirdPartyAlarm.Name = ini.GetValue("AlarmNames", "httpStopped", "HTTP Upload");
			MySqlUploadAlarm.Name = ini.GetValue("AlarmNames", "mySqlStopped", "MySQL Upload");
			IsRainingAlarm.Name = ini.GetValue("AlarmNames", "isRaining", "Is Raining");
			NewRecordAlarm.Name = ini.GetValue("AlarmNames", "newRecord", "New Record");
			FtpAlarm.Name = ini.GetValue("AlarmNames", "ftpStopped", "Web Upload");
			ErrorAlarm.Name = ini.GetValue("AlarmNames", "genError", "Cumulus MX Error");

			// web tag defaults
			Trans.WebTagGenTimeDate = ini.GetValue("WebTags", "GeneralTimeDate", "HH:mm 'on' dd MMMM yyyy");
			Trans.WebTagGenDate = ini.GetValue("WebTags", "GeneralDate", "dd MMMM yyyy");
			Trans.WebTagGenTime = ini.GetValue("WebTags", "GeneralTime", "HH:mm");
			Trans.WebTagRecDate = ini.GetValue("WebTags", "RecordDate", "'on' dd MMMM yyyy");
			Trans.WebTagRecTimeDate = ini.GetValue("WebTags", "RecordTimeDate", "'at' HH:mm 'on' dd MMMM yyyy");
			Trans.WebTagRecDryWetDate = ini.GetValue("WebTags", "RecordDryWetDate", "'to' dd MMMM yyyy");
			Trans.WebTagElapsedTime = ini.GetValue("WebTags", "ElapsedTime", "{0:%d} days {0:%h} hours");

			// Hi/Lo Captions
			foreach (var key in Trans.DataCaptions.Keys)
			{
				Trans.DataCaptions[key] = ini.GetValue("DataCaptions", key, Trans.DataCaptions[key]);
			}

			if (!File.Exists("strings.ini"))
			{
				WriteStringsFile();
			}
		}


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
			foreach (var key in Trans.DataCaptions.Keys)
			{
				ini.SetValue("DataCaptions", key, Trans.DataCaptions[key]);
			}

			ini.Flush();

			LogMessage("Completed writing strings.ini file");
		}
	}
}
