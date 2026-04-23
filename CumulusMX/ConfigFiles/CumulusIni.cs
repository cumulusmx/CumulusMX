using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using CumulusMX.Settings;

using MySqlConnector;

namespace CumulusMX
{
	public partial class Cumulus
	{
		public void ReadIniFile()
		{
			var DavisBaudRates = new List<int> { 1200, 2400, 4800, 9600, 14400, 19200 };
			ImetOptions.BaudRates = [19200, 115200];
			var rewriteRequired = false; // Do we need to re-save the ini file after migration processing or resetting options?
			var recreateRequired = false; // Required to encrypt the credentials the first time

			LogMessage("Reading Cumulus.ini file");

			IniFile ini = new IniFile("Cumulus.ini");

			// check for Cumulus 1 [FTP Site] and correct it
			if (ini.ValueExists("FTP Site", "Port"))
			{
				LogMessage("Cumulus.ini: Changing old [FTP Site] to [FTP site]");
				var contents = File.ReadAllText("Cumulus.ini");
				contents = contents.Replace("[FTP Site]", "[FTP site]");
				File.WriteAllText("Cumulus.ini", contents);
				ini.Refresh();
			}

			#region Program Options
			ProgramOptions.ProcessLogFilesLevel = ini.GetValue("Program", "ProcessLogFiles", 0, 0, 1);

			ProgramOptions.EnableAccessibility = ini.GetValue("Program", "EnableAccessibility", false);

			ProgramOptions.StartupPingHost = ini.GetValue("Program", "StartupPingHost", string.Empty);

			ProgramOptions.StartupPingEscapeTime = ini.GetValue("Program", "StartupPingEscapeTime", 999, 0);
			ProgramOptions.StartupDelaySecs = ini.GetValue("Program", "StartupDelaySecs", 0, 0);
			ProgramOptions.StartupDelayMaxUptime = ini.GetValue("Program", "StartupDelayMaxUptime", 300, 0);

			ProgramOptions.StartupTask = ini.GetValue("Program", "StartupTask", string.Empty);
			ProgramOptions.StartupTaskParams = ini.GetValue("Program", "StartupTaskParams", string.Empty);
			ProgramOptions.StartupTaskWait = ini.GetValue("Program", "StartupTaskWait", false);

			ProgramOptions.ShutdownTask = ini.GetValue("Program", "ShutdownTask", string.Empty);
			ProgramOptions.ShutdownTaskParams = ini.GetValue("Program", "ShutdownTaskParams", string.Empty);

			ProgramOptions.DataStoppedExit = ini.GetValue("Program", "DataStoppedExit", false);
			ProgramOptions.DataStoppedMins = ini.GetValue("Program", "DataStoppedMins", 10, 0);
			ProgramOptions.Culture.RemoveSpaceFromDateSeparator = ini.GetValue("Culture", "RemoveSpaceFromDateSeparator", false);
			// if the culture names match, then we apply the new date separator if change is enabled and it contains a space
			if (ProgramOptions.Culture.RemoveSpaceFromDateSeparator && CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator.Contains(' '))
			{
				LogMessage("Cumulus.ini: Removing space from date formats");
				Utils.RemoveSpaceFromDateFormat(true);
			}

			// get the default display language
			var defaultLang = DashboardLocalisationManager.ThisLocaleAvailable(CultureInfo.CurrentCulture.TwoLetterISOLanguageName) ? CultureInfo.CurrentCulture.TwoLetterISOLanguageName : "en";
			if (!ini.ValueExists("Program", "DisplayLang"))
			{
				ini.SetValue("Program", "DisplayLang", defaultLang);
				rewriteRequired = true;
			}
			ProgramOptions.DisplayLanguage = ini.GetValue("Program", "DisplayLang", defaultLang);

			ProgramOptions.TimeFormat = ini.GetValue("Program", "TimeFormat", "t");
			if (ProgramOptions.TimeFormat == "t")
				ProgramOptions.TimeFormatLong = "T";
			else if (ProgramOptions.TimeFormat == "h:mm tt")
				ProgramOptions.TimeFormatLong = "h:mm:ss tt";
			else
				ProgramOptions.TimeFormatLong = "HH:mm:ss";

			ProgramOptions.TimeAmPmLowerCase = ini.GetValue("Program", "TimeAmPmLowerCase", false);

			if (ProgramOptions.TimeAmPmLowerCase)
			{
				LogMessage("Cumulus.ini: Forcing lower-case am/pm");
				Utils.SetDateTimeAmPmDesignators(true);
			}

			ProgramOptions.EncryptedCreds = ini.GetValue("Program", "EncryptedCreds", false);

			ProgramOptions.WarnMultiple = ini.GetValue("Station", "WarnMultiple", true);
			ProgramOptions.ListWebTags = ini.GetValue("Station", "ListWebTags", false);
			ProgramOptions.UseWebSockets = ini.GetValue("Program", "UseWebSockets", true);

			ProgramOptions.DataPath = ini.GetValue("Program", "DataPath", "data");
			ProgramOptions.BackupPath = ini.GetValue("Program", "BackupPath", "backup");
			ProgramOptions.ReportsPath = ini.GetValue("Program", "ReportsPath", "Reports");
			ProgramOptions.DiagsPath = ini.GetValue("Program", "DiagsPath", "MXdiags");


			SmtpOptions.Logging = ini.GetValue("SMTP", "Logging", false);
			if (DebuggingEnabled)
			{
				ProgramOptions.DebugLogging = true;
				ProgramOptions.DataLogging = true;
			}
			else
			{
				ProgramOptions.DebugLogging = ini.GetValue("Station", "Logging", false);
				ProgramOptions.DataLogging = ini.GetValue("Station", "DataLogging", false);
			}
			ErrorListLoggingLevel = (MxLogLevel) ini.GetValue("Program", "ErrorListLoggingLevel", (int) MxLogLevel.Warning);

			SnowLogging = ini.GetValue("Program", "SnowLogging", false);

			ProgramOptions.SecureSettings = ini.GetValue("Program", "SecureSettings", false);
			ProgramOptions.SettingsUsername = ini.GetValue("Program", "SettingsUsername", string.Empty);
			ProgramOptions.SettingsPassword = ini.GetValue("Program", "SettingsPassword", string.Empty);
			#endregion

			#region Station Options
			ComportName = ini.GetValue("Station", "ComportName", DefaultComportName);

			StationType = ini.GetValue("Station", "Type", -1);
			StationModel = ini.GetValue("Station", "Model", string.Empty);
			Manufacturer = GetStationManufacturerFromType(StationType);

			FineOffsetStation = (StationType == StationTypes.FineOffset || StationType == StationTypes.FineOffsetSolar);
			DavisStation = (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2);

			// Davis Options
			DavisOptions.UseLoop2 = ini.GetValue("Station", "UseDavisLoop2", true);
			DavisOptions.ReadReceptionStats = ini.GetValue("Station", "DavisReadReceptionStats", true);
			DavisOptions.SetLoggerInterval = ini.GetValue("Station", "DavisSetLoggerInterval", false);
			DavisOptions.InitWaitTime = ini.GetValue("Station", "DavisInitWaitTime", 2000, 0);
			DavisOptions.IPResponseTime = ini.GetValue("Station", "DavisIPResponseTime", 500, 0);
			DavisOptions.IncrementPressureDP = ini.GetValue("Station", "DavisIncrementPressureDP", false);
			if (StationType == StationTypes.VantagePro && DavisOptions.UseLoop2)
			{
				LogMessage("Cumulus.ini: Disabling LOOP2 for old VP station");
				DavisOptions.UseLoop2 = false;
				ini.SetValue("Station", "UseDavisLoop2", DavisOptions.UseLoop2);
				rewriteRequired = true;
			}
			DavisOptions.BaudRate = ini.GetValue("Station", "DavisBaudRate", 19200, 1200, 19200);
			// Check we have a valid value
			if (!DavisBaudRates.Contains(DavisOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Cumulus.ini: Error, the value for DavisBaudRate in the ini file " + DavisOptions.BaudRate + " is not valid, using default 19200.");
				DavisOptions.BaudRate = 19200;
				ini.SetValue("Station", "DavisBaudRate", DavisOptions.BaudRate);
				rewriteRequired = true;
			}
			DavisOptions.ForceVPBarUpdate = ini.GetValue("Station", "ForceVPBarUpdate", false);
			DavisOptions.RainGaugeType = ini.GetValue("Station", "VPrainGaugeType", -1);
			if (DavisOptions.RainGaugeType > 3)
			{
				LogMessage("Cumulus.ini: Invalid Davis rain gauge type, defaulting to -1");
				DavisOptions.RainGaugeType = -1;
				ini.SetValue("Station", "VPrainGaugeType", DavisOptions.RainGaugeType);
				rewriteRequired = true;
			}
			DavisOptions.ConnectionType = ini.GetValue("Station", "VP2ConnectionType", 0, 0, 2);
			if (DavisOptions.ConnectionType == 1)
			{
				DavisOptions.ConnectionType = 2;
				ini.SetValue("Station", "VP2ConnectionType", DavisOptions.ConnectionType);
				rewriteRequired = true;
			}
			DavisOptions.TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222, 1, 65535);
			DavisOptions.IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");
			DavisOptions.CloudBroadcasts = ini.GetValue("Station", "DavisCloudBroadcast", false);

			WeatherFlowOptions.WFDeviceId = ini.GetValue("Station", "WeatherFlowDeviceId", 0, 0, 1);
			WeatherFlowOptions.WFSerialNo = ini.GetValue("Station", "WeatherFlowSerialNo", string.Empty);
			WeatherFlowOptions.WFTcpPort = ini.GetValue("Station", "WeatherFlowTcpPort", 50222, 1, 65535);
			WeatherFlowOptions.WFToken = ini.GetValue("Station", "WeatherFlowToken", "api token");
			WeatherFlowOptions.WFDaysHist = ini.GetValue("Station", "WeatherFlowDaysHist", 0, 0);

			DavisOptions.PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0, 0);

			Latitude = ini.GetValue("Station", "Latitude", (decimal) 0.0);
			if (Latitude > 90 || Latitude < -90)
			{
				Latitude = 0;
				LogErrorMessage($"Cumulus.ini: Error, invalid latitude value [{Latitude}], defaulting to zero.");
				ini.SetValue("Station", "Latitude", Latitude);
				rewriteRequired = true;
			}
			Longitude = ini.GetValue("Station", "Longitude", (decimal) 0.0);
			if (Longitude > 180 || Longitude < -180)
			{
				Longitude = 0;
				LogErrorMessage($"Cumulus.ini: Error, invalid longitude value [{Longitude}], defaulting to zero.");
				ini.SetValue("Station", "Longitude", Longitude);
				rewriteRequired = true;
			}

			LatTxt = ini.GetValue("Station", "LatTxt", string.Empty);
			LatTxt = LatTxt.Replace(" ", "&nbsp;");
			LatTxt = LatTxt.Replace("°", "&#39;");
			LonTxt = ini.GetValue("Station", "LonTxt", string.Empty);
			LonTxt = LonTxt.Replace(" ", "&nbsp;");
			LonTxt = LonTxt.Replace("°", "&#39;");

			Altitude = ini.GetValue("Station", "Altitude", 0.0);
			AltitudeInFeet = ini.GetValue("Station", "AltitudeInFeet", false);

			if (!ini.ValueExists("Station", "TimeZone"))
			{
				ini.SetValue("Station", "TimeZone", Utils.GetTimeZoneId());
				rewriteRequired = true;
			}
			StationOptions.TimeZoneId = ini.GetValue("Station", "TimeZone", Utils.GetTimeZoneId());

			StationOptions.Humidity98Fix = ini.GetValue("Station", "Humidity98Fix", false);
			StationOptions.CalcuateAverageWindSpeed = ini.GetValue("Station", "Wind10MinAverage", false);
			StationOptions.UseSpeedForAvgCalc = ini.GetValue("Station", "UseSpeedForAvgCalc", false);
			StationOptions.UseSpeedForLatest = ini.GetValue("Station", "UseSpeedForLatest", false);
			StationOptions.UseRainForIsRaining = ini.GetValue("Station", "UseRainForIsRaining", 1, 0, 2);  // 0=station, 1=rain sensor, 2=haptic sensor
			StationOptions.LeafWetnessIsRainingIdx = ini.GetValue("Station", "LeafWetnessIsRainingIdx", -1);
			StationOptions.LeafWetnessIsRainingThrsh = ini.GetValue("Station", "LeafWetnessIsRainingVal", 0.0, 0);

			StationOptions.AvgBearingMinutes = ini.GetValue("Station", "AvgBearingMinutes", 10, 1, 120);

			AvgBearingTime = new TimeSpan(StationOptions.AvgBearingMinutes / 60, StationOptions.AvgBearingMinutes % 60, 0);

			StationOptions.AvgSpeedMinutes = ini.GetValue("Station", "AvgSpeedMinutes", 10, 1, 120);

			AvgSpeedTime = new TimeSpan(StationOptions.AvgSpeedMinutes / 60, StationOptions.AvgSpeedMinutes % 60, 0);

			LogMessage("AvgSpdMins=" + StationOptions.AvgSpeedMinutes + " AvgSpdTime=" + AvgSpeedTime.ToString());

			StationOptions.PeakGustMinutes = ini.GetValue("Station", "PeakGustMinutes", 10, 1, 120);

			PeakGustTime = new TimeSpan(StationOptions.PeakGustMinutes / 60, StationOptions.PeakGustMinutes % 60, 0);

			StationOptions.NoSensorCheck = ini.GetValue("Station", "NoSensorCheck", false);

			StationOptions.CalculatedDP = ini.GetValue("Station", "CalculatedDP", false);
			StationOptions.CalculatedWC = ini.GetValue("Station", "CalculatedWC", false);
			StationOptions.CalculatedET = ini.GetValue("Station", "CalculatedET", false);
			StationOptions.CalculateSLP = ini.GetValue("Station", "CalculatedSLP", false);
			RolloverHour = ini.GetValue("Station", "RolloverHour", 0, 0, 1);
			Use10amInSummer = ini.GetValue("Station", "Use10amInSummer", true);
			//ConfirmClose = ini.GetValue("Station", "ConfirmClose", false)
			//CloseOnSuspend = ini.GetValue("Station", "CloseOnSuspend", false)
			//RestartIfUnplugged = ini.GetValue("Station", "RestartIfUnplugged", false)
			//RestartIfDataStops = ini.GetValue("Station", "RestartIfDataStops", false)
			StationOptions.SyncTime = ini.GetValue("Station", "SyncDavisClock", false);
			StationOptions.ClockSettingHour = ini.GetValue("Station", "ClockSettingHour", 4, 0, 23);
			StationOptions.WS2300IgnoreStationClock = ini.GetValue("Station", "WS2300IgnoreStationClock", false);
			StationOptions.LogExtraSensors = ini.GetValue("Station", "LogExtraSensors", false);
			ReportDataStoppedErrors = ini.GetValue("Station", "ReportDataStoppedErrors", true);
			ReportLostSensorContact = ini.GetValue("Station", "ReportLostSensorContact", true);
			ErrorLogSpikeRemoval = ini.GetValue("Station", "ErrorLogSpikeRemoval", true);
			// this is now an index
			DataLogInterval = ini.GetValue("Station", "DataLogInterval", 2, 0, 5);

			FineOffsetOptions.SyncReads = ini.GetValue("Station", "SyncFOReads", true);
			FineOffsetOptions.ReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3, 0);
			FineOffsetOptions.ReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150, 0);
			FineOffsetOptions.SetLoggerInterval = ini.GetValue("Station", "FineOffsetSetLoggerInterval", false);
			FineOffsetOptions.VendorID = ini.GetValue("Station", "VendorID", -1, -1);
			FineOffsetOptions.ProductID = ini.GetValue("Station", "ProductID", -1, -1);
			#endregion

			#region Units
			Units.Wind = ini.GetValue("Station", "WindUnit", 2, 0, 3);
			Units.Press = ini.GetValue("Station", "PressureUnit", 1, 0, 3);
			Units.Rain = ini.GetValue("Station", "RainUnit", 0, 0, 1);
			Units.Temp = ini.GetValue("Station", "TempUnit", 0, 0, 1);
			Units.SnowDepth = ini.GetValue("Station", "SnowDepthUnit", 0, 0, 1);
			Units.LaserDistance = ini.GetValue("Station", "LaserDistancehUnit", Units.SnowDepth, 0, 2);

			StationOptions.RoundWindSpeed = ini.GetValue("Station", "RoundWindSpeed", false);
			StationOptions.PrimaryAqSensor = ini.GetValue("Station", "PrimaryAqSensor", -1, -1);


			// Unit decimals
			RainDPlaces = RainDPlaceDefaults[Units.Rain];
			TempDPlaces = TempDPlaceDefaults[Units.Temp];
			PressDPlaces = PressDPlaceDefaults[Units.Press];
			WindDPlaces = StationOptions.RoundWindSpeed ? 0 : WindDPlaceDefaults[Units.Wind];
			WindAvgDPlaces = WindDPlaces;
			AirQualityDPlaces = 1;
			SnowDPlaces = Units.SnowDepth == 0 ? 1 : 2;
			SnowFormat = "F" + SnowDPlaces;


			// Unit decimal overrides
			WindDPlaces = ini.GetValue("Station", "WindSpeedDecimals", WindDPlaces, 0);
			WindAvgDPlaces = ini.GetValue("Station", "WindSpeedAvgDecimals", WindAvgDPlaces, 0);
			WindRunDPlaces = ini.GetValue("Station", "WindRunDecimals", WindRunDPlaces, 0);
			SunshineDPlaces = ini.GetValue("Station", "SunshineHrsDecimals", 1, 0);
			PressDPlaces = ini.GetValue("Station", "PressDecimals", PressDPlaces, 0);
			RainDPlaces = ini.GetValue("Station", "RainDecimals", RainDPlaces, 0);
			TempDPlaces = ini.GetValue("Station", "TempDecimals", TempDPlaces, 0);
			UVDPlaces = ini.GetValue("Station", "UVDecimals", UVDPlaces, 0);
			AirQualityDPlaces = ini.GetValue("Station", "AirQualityDecimals", AirQualityDPlaces, 0);

			if ((StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2) && DavisOptions.IncrementPressureDP)
			{
				++PressDPlaces;
			}
			#endregion

			LocationName = ini.GetValue("Station", "LocName", string.Empty);
			LocationDesc = ini.GetValue("Station", "LocDesc", string.Empty);

			YTDrain = ini.GetValue("Station", "YTDrain", 0.0, 0.0);
			YTDrainyear = ini.GetValue("Station", "YTDrainyear", 0, 0);

			EwOptions.Interval = ini.GetValue("Station", "EWInterval", 1.0, 0.01);
			EwOptions.Filename = ini.GetValue("Station", "EWFile", string.Empty);
			EwOptions.MinPressMB = ini.GetValue("Station", "EWminpressureMB", 900);
			EwOptions.MaxPressMB = ini.GetValue("Station", "EWmaxpressureMB", 1200);
			EwOptions.MaxRainTipDiff = ini.GetValue("Station", "EWMaxRainTipDiff", 30);
			EwOptions.PressOffset = ini.GetValue("Station", "EWpressureoffset", 9999.0);

			#region Spike Settings
			Spike.TempDiff = ini.GetValue("Station", "EWtempdiff", 999.0);
			Spike.PressDiff = ini.GetValue("Station", "EWpressurediff", 999.0);
			Spike.HumidityDiff = ini.GetValue("Station", "EWhumiditydiff", 999.0);
			Spike.GustDiff = ini.GetValue("Station", "EWgustdiff", 999.0);
			Spike.WindDiff = ini.GetValue("Station", "EWwinddiff", 999.0);
			Spike.MaxRainRate = ini.GetValue("Station", "EWmaxRainRate", 999.0);
			Spike.MaxHourlyRain = ini.GetValue("Station", "EWmaxHourlyRain", 999.0);
			Spike.InTempDiff = ini.GetValue("Station", "EWinTempdiff", 999.0);
			Spike.InHumDiff = ini.GetValue("Station", "EWinHumiditydiff", 999.0);
			double maxSnowInc = Units.LaserDistance switch
			{
				0 => 5,
				1 => 2,
				2 => 50,
				_ => 999
			};
			Spike.SnowDiff = ini.GetValue("Station", "EWsnowdiff", maxSnowInc, 0, 999);
			double minSnowInc = Units.LaserDistance switch
			{
				0 => 0.2,
				1 => 0.2,
				2 => 2,
				_ => 0
			};
			SnowDepthMinInc = ini.GetValue("Station", "EWsnowMinInc", minSnowInc, 0);
			SnowDepthMedianMins = ini.GetValue("Station", "SnowMedianMins", 10, 1, 30);
			SnowDepthEmaTimeMins = ini.GetValue("Station", "SnowEmaTimeMins", 12.0, 0.01, 30.0);
			var defSnowClip = Units.LaserDistance switch
			{
				0 => 0.1,  // cm
				1 => 0.04, // in
				2 => 1.0,  // mm
				_ => 1.0
			};
			SnowDepthClipDelta = ini.GetValue("Station", "SnowClipDelta", defSnowClip, 0.0);

			if (Spike.TempDiff < 999)
			{
				Spike.TempDiff = ConvertUnits.TempCToUser(Spike.TempDiff);
			}
			if (Spike.PressDiff < 999)
			{
				Spike.PressDiff = ConvertUnits.PressMBToUser(Spike.PressDiff);
			}
			if (Spike.GustDiff < 999)
			{
				Spike.GustDiff = ConvertUnits.WindMSToUser(Spike.GustDiff);
			}
			if (Spike.WindDiff < 999)
			{
				Spike.WindDiff = ConvertUnits.WindMSToUser(Spike.WindDiff);
			}
			if (Spike.MaxRainRate < 999)
			{
				Spike.MaxRainRate = ConvertUnits.RainMMToUser(Spike.MaxRainRate);
			}
			if (Spike.MaxHourlyRain < 999)
			{
				Spike.MaxHourlyRain = ConvertUnits.RainMMToUser(Spike.MaxHourlyRain);
			}
			if (Spike.InTempDiff < 999)
			{
				Spike.InTempDiff = ConvertUnits.TempCToUser(Spike.InTempDiff);
			}

			LCMaxWind = ini.GetValue("Station", "LCMaxWind", 9999);
			#endregion

			if (ini.ValueExists("Station", "StartDate"))
			{
				var RecordsBeganDate = ini.GetValue("Station", "StartDate", DateTime.Now.ToLongDateString());
				try
				{
					RecordsBeganDateTime = DateTime.Parse(RecordsBeganDate, CultureInfo.CurrentCulture);
					LogMessage($"Cumulus.ini: Changing old StartDate [{RecordsBeganDate}] to StartDateIso [{RecordsBeganDateTime:yyyy-MM-dd}]");
					ini.DeleteValue("Station", "StartDate");
					ini.SetValue("Station", "StartDateIso", RecordsBeganDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
					rewriteRequired = true;
				}
				catch (Exception ex)
				{
					LogErrorMessage($"Cumulus.ini: Error parsing the RecordsBegan date {RecordsBeganDate}: {ex.Message}");
				}
			}
			else
			{
				var RecordsBeganDate = ini.GetValue("Station", "StartDateIso", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
				RecordsBeganDateTime = DateTime.ParseExact(RecordsBeganDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
			}

			LogMessage($"Cumulus.ini: Start date Parsed: {RecordsBeganDateTime:yyyy-MM-dd}");

			#region IMET Settings
			ImetOptions.WaitTime = ini.GetValue("Station", "ImetWaitTime", 500, 0);
			ImetOptions.ReadDelay = ini.GetValue("Station", "ImetReadDelay", 500, 0);
			ImetOptions.UpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);
			ImetOptions.BaudRate = ini.GetValue("Station", "ImetBaudRate", 19200, 19200, 115200);
			// Check we have a valid value
			if (!ImetOptions.BaudRates.Contains(ImetOptions.BaudRate))
			{
				// nope, that isn't allowed, set the default
				LogMessage("Cumulus.ini: Error, the value for ImetOptions.ImetBaudRate " + ImetOptions.BaudRate + " is not valid, using default 19200.");
				ImetOptions.BaudRate = 19200;
				ini.SetValue("Station", "ImetBaudRate", ImetOptions.BaudRate);
				rewriteRequired = true;
			}
			#endregion

			StationOptions.UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
			ForecastSource = ini.GetValue("Station", "UseCumulusForecast", 0, 0, 3);
			HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
			StationOptions.UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
			//UseWindChillCutoff = ini.GetValue("Station", "UseWindChillCutoff", false)
			RecordSetTimeoutHrs = ini.GetValue("Station", "RecordSetTimeoutHrs", 24, 0);

			SnowDepthHour = ini.GetValue("Station", "SnowDepthHour", 9, 0, 23);
			SnowAutomated = ini.GetValue("Station", "SnowAutomated", 0, 0, 4);
			SnowSeasonStart = ini.GetValue("Station", "SnowSeasonStart", Latitude >= 0 ? 10 : 4, 1, 12);

			StationOptions.UseZeroBearing = ini.GetValue("Station", "UseZeroBearing", false);

			RainDayThreshold = ini.GetValue("Station", "RainDayThreshold", -1.0, -1.0);

			FCpressinMB = ini.GetValue("Station", "FCpressinMB", true);
			FClowpress = ini.GetValue("Station", "FClowpress", DEFAULTFCLOWPRESS);
			FChighpress = ini.GetValue("Station", "FChighpress", DEFAULTFCHIGHPRESS);
			FCPressureThreshold = ini.GetValue("Station", "FCPressureThreshold", -1.0, -1.0);

			RainSeasonStart = ini.GetValue("Station", "RainSeasonStart", 1, 1, 12);
			RainWeekStart = ini.GetValue("Station", "RainWeekStart", 1, 0, 1);
			ChillHourSeasonStart = ini.GetValue("Station", "ChillHourSeasonStart", Latitude >= 0 ? 10 : 4, 1, 12);
			ChillHourThreshold = ini.GetValue("Station", "ChillHourThreshold", Units.Temp == 0 ? 7 : 45);
			ChillHourBase = ini.GetValue("Station", "ChillHourBase", -99);

			#region RG11 Settings
			RG11Enabled = ini.GetValue("Station", "RG11Enabled", false);
			RG11Port = ini.GetValue("Station", "RG11portName", DefaultComportName);
			RG11TBRmode = ini.GetValue("Station", "RG11TBRmode", false);
			RG11tipsize = ini.GetValue("Station", "RG11tipsize", 0.0, 0.0);
			RG11IgnoreFirst = ini.GetValue("Station", "RG11IgnoreFirst", false);
			RG11DTRmode = ini.GetValue("Station", "RG11DTRmode", true);

			RG11Enabled2 = ini.GetValue("Station", "RG11Enabled2", false);
			RG11Port2 = ini.GetValue("Station", "RG11port2Name", DefaultComportName);
			RG11TBRmode2 = ini.GetValue("Station", "RG11TBRmode2", false);
			RG11tipsize2 = ini.GetValue("Station", "RG11tipsize2", 0.0, 0.0);
			RG11IgnoreFirst2 = ini.GetValue("Station", "RG11IgnoreFirst2", false);
			RG11DTRmode2 = ini.GetValue("Station", "RG11DTRmode2", true);
			#endregion

			if (FCPressureThreshold < 0)
			{
				FCPressureThreshold = Units.Press switch
				{
					0 => 0.1,
					1 => 0.1,
					2 => 0.00295333727,
					3 => 0.01,
					_ => 0
				};
			}

			WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0, 0);
			WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1, 0);

			WxnowComment = ini.GetValue("Station", "WxnowComment.txt", string.Empty);

			#region Get WLL Settings
			// WeatherLink Live device settings
			WllApiKey = ini.GetValue("WLL", "WLv2ApiKey", string.Empty);
			WllApiSecret = ini.GetValue("WLL", "WLv2ApiSecret", string.Empty);
			WllStationId = ini.GetValue("WLL", "WLStationId", -1, -1);
			WllStationUuid = ini.GetValue("WLL", "WLStationUuid", "");
			WllTriggerDataStoppedOnBroadcast = ini.GetValue("WLL", "DataStoppedOnBroadcast", true);
			WLLAutoUpdateIpAddress = ini.GetValue("WLL", "AutoUpdateIpAddress", true);
			WllBroadcastDuration = ini.GetValue("WLL", "BroadcastDuration", WllBroadcastDuration);
			WllBroadcastPort = ini.GetValue("WLL", "BroadcastPort", WllBroadcastPort);
			WllPrimaryRain = ini.GetValue("WLL", "PrimaryRainTxId", 1, 1, 8);
			WllPrimaryTempHum = ini.GetValue("WLL", "PrimaryTempHumTxId", 1, 1, 8);
			WllPrimaryWind = ini.GetValue("WLL", "PrimaryWindTxId", 1, 1, 8);
			WllPrimaryRain = ini.GetValue("WLL", "PrimaryRainTxId", 1, 1, 8);
			WllPrimarySolar = ini.GetValue("WLL", "PrimarySolarTxId", 0, 0, 8);
			WllPrimaryUV = ini.GetValue("WLL", "PrimaryUvTxId", 0, 0, 8);
			WllPrimarySunshine = ini.GetValue("WLL", "PrimarySunshine", 0, 0, 8);
			SolarOptions.UseSunshineSensor = WllPrimarySunshine > 0;
			for (var i = 1; i <= 16; i++)
			{
				WllSoilTempTx[i] = ini.GetValue("WLL", "ExtraSoilTempTxId" + i, 0, 0, 8);
				WllSoilTempIdx[i] = ini.GetValue("WLL", "ExtraSoilTempIdx" + i, ((i - 1) % 4) + 1, 1, 4);
			}
			for (var i = 1; i <= 16; i++)
			{
				WllSoilMoistureTx[i] = ini.GetValue("WLL", "ExtraSoilMoistureTxId" + i, 0, 0, 8);
				WllSoilMoistureIdx[i] = ini.GetValue("WLL", "ExtraSoilMoistureIdx" + i, ((i - 1) % 4) + 1, 1, 4);
			}
			for (var i = 1; i <= 8; i++)
			{
				WllLeafWetTx[i] = ini.GetValue("WLL", "ExtraLeafTxId" + i, 0, 0, 8);
				WllLeafWetIdx[i] = ini.GetValue("WLL", "ExtraLeafIdx" + i, ((i - 1) % 2) + 1, 1, 2);
			}
			for (int i = 1; i <= 16; i++)
			{
				WllExtraTempTx[i] = ini.GetValue("WLL", "ExtraTempTxId" + i, 0, 0, 8);
				WllExtraTempIdx[i] = ini.GetValue("WLL", "ExtraTempIdx" + i, ((i - 1) % 4) + 1, 1, 4);
			}
			#endregion

			#region Get GW1000 Settings
			// GW1000 settings
			Gw1000IpAddress = ini.GetValue("GW1000", "IPAddress", "0.0.0.0");
			Gw1000MacAddress = (ini.GetValue("GW1000", "MACAddress", string.Empty) ?? string.Empty).ToUpper();
			Gw1000AutoUpdateIpAddress = ini.GetValue("GW1000", "AutoUpdateIpAddress", true);
			Gw1000PrimaryRainSensor = ini.GetValue("GW1000", "PrimaryRainSensor", 0, 0, 1); //0=main station (tipping bucket) 1=piezo
			EcowittIsRainingUsePiezo = ini.GetValue("GW1000", "UsePiezoIsRaining", false);
			EcowittExtraEnabled = ini.GetValue("GW1000", "ExtraSensorDataEnabled", false);
			EcowittCloudExtraEnabled = ini.GetValue("GW1000", "ExtraCloudSensorDataEnabled", false);
			EcowittSetCustomServer = ini.GetValue("GW1000", "SetCustomServer", false);
			EcowittGatewayAddr = ini.GetValue("GW1000", "EcowittGwAddr", "0.0.0.0");
			var localIp = Utils.GetIpWithDefaultGateway();
			EcowittLocalAddr = ini.GetValue("GW1000", "EcowittLocalAddr", localIp.ToString());
			EcowittCustomInterval = ini.GetValue("GW1000", "EcowittCustomInterval", 16, 1);
			EcowittUseSdCard = ini.GetValue("GW1000", "EcowittUseSdcard", false);
			EcowittCloudDataUpdateInterval = ini.GetValue("GW1000", "CloudDataUpdateInterval", 1, 1, 10);

			EcowittHttpPassword = ini.GetValue("GW1000", "HttpPassword", string.Empty);

			EcowittExtraSetCustomServer = ini.GetValue("GW1000", "ExtraSetCustomServer", false);
			EcowittExtraGatewayAddr = ini.GetValue("GW1000", "EcowittExtraGwAddr", "0.0.0.0");
			EcowittExtraLocalAddr = ini.GetValue("GW1000", "EcowittExtraLocalAddr", localIp.ToString());
			EcowittExtraCustomInterval = ini.GetValue("GW1000", "EcowittExtraCustomInterval", 16, 1);
			// api
			EcowittApplicationKey = ini.GetValue("GW1000", "EcowittAppKey", string.Empty);
			EcowittUserApiKey = ini.GetValue("GW1000", "EcowittUserKey", string.Empty);
			EcowittMacAddress = (ini.GetValue("GW1000", "EcowittMacAddress", string.Empty) ?? string.Empty).ToUpper();
			// For GW1000 stations, the Ecowitt MAC must be the same as the device MAC
			if (StationType == 12)
			{
				EcowittMacAddress = Gw1000MacAddress;
			}
			// WN34 sensor mapping
			for (int i = 1; i <= 8; i++)
			{
				EcowittMapWN34[i] = ini.GetValue("GW1000", "WN34MapChan" + i, 0);
			}
			// forwarders
			for (int i = 0; i < EcowittForwarders.Length; i++)
			{
				EcowittForwarders[i] = ini.GetValue("GW1000", "Forwarder" + i, string.Empty);
			}
			EcowittExtraUseMainForwarders = ini.GetValue("GW1000", "ExtraUseMainForwarders", false);
			// extra forwarders
			for (int i = 0; i < EcowittExtraForwarders.Length; i++)
			{
				EcowittExtraForwarders[i] = ini.GetValue("GW1000", "ExtraForwarder" + i, string.Empty);
			}
			#endregion

			#region Get PurpleAir Settings
			// PurpleAir settings
			PurpleAirEnabled = ini.GetValue("PurpleAir", "Enabled", false);
			for (var i = 1; i < 5; i++)
			{
				PurpleAirIpAddress[i - 1] = ini.GetValue("PurpleAir", "IpAddress" + i, string.Empty);
				PurpleAirAlgorithm[i - 1] = ini.GetValue("PurpleAir", "Algorithm" + i, 1, 0, 1);
				PurpleAirThSensor[i - 1] = ini.GetValue("PurpleAir", "TempHumSensor" + i, 0, 0, 16);
			}
			//PurpleAirApiKey = ini.GetValue("PurpleAir", "ApiKey", string.Empty);
			//PurpleAirSensorIndex = ini.GetValue("PurpleAir", "SensorId", 0, 0);
			//PurpleAirReadKey = ini.GetValue("PurpleAir", "ReadKey", string.Empty);
			#endregion

			#region Get Ambient Settings
			// Ambient settings
			AmbientExtraEnabled = ini.GetValue("Ambient", "ExtraSensorDataEnabled", false);
			#endregion

			#region JSON Station Settings
			// JSON station options
			JsonStationOptions.Connectiontype = ini.GetValue("JsonStation", "ConnectionType", 1, 0, 2);
			JsonStationOptions.SourceFile = ini.GetValue("JsonStation", "SourceFile", string.Empty);
			JsonStationOptions.FileReadDelay = ini.GetValue("JsonStation", "FileDelay", 200, 0);
			JsonStationOptions.FileIgnoreTime = ini.GetValue("JsonStation", "FileIgnore", 600, 0);
			JsonStationOptions.MqttServer = ini.GetValue("JsonStation", "MqttServer", string.Empty);
			JsonStationOptions.MqttPort = ini.GetValue("JsonStation", "MqttServerPort", 1883, 1, 65353);
			JsonStationOptions.MqttUsername = ini.GetValue("JsonStation", "MqttUsername", string.Empty);
			JsonStationOptions.MqttPassword = ini.GetValue("JsonStation", "MqttPassword", string.Empty);
			JsonStationOptions.MqttUseTls = ini.GetValue("JsonStation", "MqttUseTls", false);
			JsonStationOptions.MqttTopic = ini.GetValue("JsonStation", "MqttTopic", string.Empty);
			// JSON station Extra Sensors
			JsonExtraStationOptions.ExtraSensorsEnabled = ini.GetValue("JsonExtraStation", "ExtraSensorDataEnabled", false);
			JsonExtraStationOptions.Connectiontype = ini.GetValue("JsonExtraStation", "ConnectionType", 1, 0, 2);
			JsonExtraStationOptions.SourceFile = ini.GetValue("JsonExtraStation", "SourceFile", string.Empty);
			JsonExtraStationOptions.FileReadDelay = ini.GetValue("JsonExtraStation", "FileDelay", 200, 0);
			JsonExtraStationOptions.FileIgnoreTime = ini.GetValue("JsonExtraStation", "FileIgnore", 600, 0);
			JsonExtraStationOptions.MqttServer = ini.GetValue("JsonExtraStation", "MqttServer", string.Empty);
			JsonExtraStationOptions.MqttPort = ini.GetValue("JsonExtraStation", "MqttServerPort", 1883, 1, 65353);
			JsonExtraStationOptions.MqttUsername = ini.GetValue("JsonExtraStation", "MqttUsername", string.Empty);
			JsonExtraStationOptions.MqttPassword = ini.GetValue("JsonExtraStation", "MqttPassword", string.Empty);
			JsonExtraStationOptions.MqttUseTls = ini.GetValue("JsonExtraStation", "MqttUseTls", false);
			JsonExtraStationOptions.MqttTopic = ini.GetValue("JsonExtraStation", "MqttTopic", string.Empty);
			#endregion

			#region Sensor Maps
			// Sensor Mapping Options
			// Primary sensor remaps
			if (ini.ValueExists("GW1000", "PrimaryTHSensor"))
			{
				SensorMaps.PrimaryTempHum = ini.GetValue("GW1000", "PrimaryTHSensor", 0, 0, 99);  // 0=default, 1-8=extra t/h sensor number, 99=use indoor sensor
				ini.DeleteValue("GW1000", "PrimaryTHSensor");
				ini.SetValue("SensorMaps", "PrimaryTHSensor", SensorMaps.PrimaryTempHum);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.PrimaryTempHum = ini.GetValue("SensorMaps", "PrimaryTHSensor", 0, 0, 99);  // 0=default, 1-16=extra t/h sensor number, 99=use indoor sensor
			}

			if (ini.ValueExists("GW1000", "PrimaryIndoorTHSensor"))
			{
				SensorMaps.PrimaryIndoorTempHum = ini.GetValue("GW1000", "PrimaryIndoorTHSensor", 0, 0, 16);  // 0=default, 1-6=extra t/h sensor number
				ini.DeleteValue("GW1000", "PrimaryIndoorTHSensor");
				ini.SetValue("SensorMaps", "PrimaryIndoorTHSensor", SensorMaps.PrimaryIndoorTempHum);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.PrimaryIndoorTempHum = ini.GetValue("SensorMaps", "PrimaryIndoorTHSensor", 0, 0, 16);  // 0=default, 1-16=extra t/h sensor number
			}

			// 0 = Main Station, 1 = Secondary Station
			if (ini.ValueExists("GW1000", "ExtraSensorUseSolar"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseSolar", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseSolar");
				ini.SetValue("SensorMaps", "SolarEnabled", val == 1);
				SensorMaps.Solar = val;
				ini.SetValue("SensorMaps", "Solar", SensorMaps.Solar);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseSolar"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseSolar", 0, 0, 1);
				ini.SetValue("SensorMaps", "SolarEnabled", val == 1);
				SensorMaps.Solar = val;
				ini.SetValue("SensorMaps", "Solar", SensorMaps.Solar);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.SolarEnabled = ini.GetValue("SensorsMaps", "SolarEnabled", true);
				SensorMaps.Solar = ini.GetValue("SensorsMaps", "Solar", 0, 0, 1);
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseUv"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseUv", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseUv");
				ini.SetValue("SensorMaps", "UVEnabled", val == 1);
				SensorMaps.UV = val;
				ini.SetValue("SensorMaps", "UV", SensorMaps.UV);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseUv"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseUv", 0, 0, 1);
				ini.SetValue("SensorMaps", "UVEnabled", val);
				SensorMaps.UV = val;
				ini.SetValue("SensorMaps", "UV", SensorMaps.UV);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.UVEnabled = ini.GetValue("SensorsMaps", "UVEnabled", true);
				SensorMaps.UV = ini.GetValue("SensorsMaps", "UV", 0, 0, 1);
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseTempHum"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseTempHum", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseTempHum");
				ini.SetValue("SensorMaps", "ExtraTempHumEnabled", val == 1);
				SensorMaps.ExtraTempHum = Enumerable.Repeat(val, SensorMaps.ExtraTempHum.Length).ToArray();
				ini.SetValue("SensorMaps", "ExtraTempHum", SensorMaps.ExtraTempHum);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseTempHum"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseTempHum", 0, 0, 1);
				ini.SetValue("SensorMaps", "ExtraTempHumEnabled", val == 1);
				SensorMaps.ExtraTempHum = Enumerable.Repeat(val, SensorMaps.ExtraTempHum.Length).ToArray();
				ini.SetValue("SensorMaps", "ExtraTempHum", SensorMaps.ExtraTempHum);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.ExtraTempHumEnabled = ini.GetValue("SensorMaps", "ExtraTempHumEnabled", false);
				SensorMaps.ExtraTempHum = ini.GetValue("SensorMaps", "ExtraTempHum", Enumerable.Repeat(0, SensorMaps.ExtraTempHum.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseSoilTemp"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseSoilTemp", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseSoilTemp");
				ini.SetValue("SensorMaps", "SoilTempEnabled", val == 1);
				SensorMaps.SoilTemp = Enumerable.Repeat(val, SensorMaps.SoilTemp.Length).ToArray();
				ini.SetValue("SensorMaps", "SoilTemp", SensorMaps.SoilTemp);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseSoilTemp"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseSoilTemp", 0, 0, 1);
				ini.SetValue("SensorMaps", "SoilTempEnabled", val == 1);
				SensorMaps.SoilTemp = Enumerable.Repeat(val, SensorMaps.SoilTemp.Length).ToArray();
				ini.SetValue("SensorMaps", "SoilTemp", SensorMaps.SoilTemp);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.SoilTempEnabled = ini.GetValue("SensorMaps", "SoilTempEnabled", false);
				SensorMaps.SoilTemp = ini.GetValue("SensorMaps", "SoilTemp", Enumerable.Repeat(0, SensorMaps.SoilTemp.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseSoilMoist"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseSoilMoist", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseSoilMoist");
				ini.SetValue("SensorMaps", "SoilMoistEnabled", val == 1);
				SensorMaps.SoilMoist = Enumerable.Repeat(val, SensorMaps.SoilMoist.Length).ToArray();
				ini.SetValue("SensorMaps", "SoilMoist", SensorMaps.SoilMoist);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseSoilMoist"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseSoilMoist", 0, 0, 1);
				ini.SetValue("SensorMaps", "SoilMoistEnabled", val == 1);
				SensorMaps.SoilMoist = Enumerable.Repeat(val, SensorMaps.SoilMoist.Length).ToArray();
				ini.SetValue("SensorMaps", "SoilMoist", SensorMaps.SoilMoist);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.SoilMoistEnabled = ini.GetValue("SensorMaps", "SoilMoistEnabled", false);
				SensorMaps.SoilMoist = ini.GetValue("SensorMaps", "SoilMoist", Enumerable.Repeat(0, SensorMaps.SoilMoist.Length).ToArray());
			}

			if (ini.ValueExists("ExtraSensors", "ExtraSensorUseSoilEc"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseSoilEc", 0, 0, 1);
				ini.SetValue("SensorMaps", "SoilECEnabled", val == 1);
				SensorMaps.SoilEc = Enumerable.Repeat(val, SensorMaps.SoilEc.Length).ToArray();
				ini.SetValue("SensorMaps", "SoilEC", SensorMaps.SoilEc);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.SoilEcEnabled = ini.GetValue("SensorMaps", "SoilECEnabled", false);
				SensorMaps.SoilEc = ini.GetValue("SensorMaps", "SoilEC", Enumerable.Repeat(0, SensorMaps.SoilEc.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseLeafWet"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseLeafWet", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseLeafWet");
				ini.SetValue("SensorMaps", "LeafWetEnabled", val == 1);
				SensorMaps.LeafWet = Enumerable.Repeat(val, SensorMaps.LeafWet.Length).ToArray();
				ini.SetValue("SensorMaps", "LeafWet", SensorMaps.LeafWet);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseLeafWet"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseLeafWet", 0, 0, 1);
				SensorMaps.LeafWet = Enumerable.Repeat(val, SensorMaps.LeafWet.Length).ToArray();
				ini.SetValue("SensorMaps", "LeafWet", SensorMaps.LeafWet);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.LeafWetEnabled = ini.GetValue("SensorMaps", "LeafWetEnabled", false);
				SensorMaps.LeafWet = ini.GetValue("SensorMaps", "LeafWet", Enumerable.Repeat(0, SensorMaps.LeafWet.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseUserTemp"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseUserTemp", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseUserTemp");
				ini.SetValue("SensorMaps", "UserTempEnabled", val == 1);
				SensorMaps.UserTemp = Enumerable.Repeat(val, SensorMaps.UserTemp.Length).ToArray();
				ini.SetValue("SensorMaps", "UserTemp", SensorMaps.UserTemp);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseUserTemp"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseUserTemp", 0, 0, 1);
				ini.SetValue("SensorMaps", "UserTempEnabled", val == 1);
				SensorMaps.UserTemp = Enumerable.Repeat(val, SensorMaps.UserTemp.Length).ToArray();
				ini.SetValue("SensorMaps", "UserTemp", SensorMaps.UserTemp);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.UserTempEnabled = ini.GetValue("SensorMaps", "UserTempEnabled", false);
				SensorMaps.UserTemp = ini.GetValue("SensorMaps", "UserTemp", Enumerable.Repeat(0, SensorMaps.UserTemp.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseAQI"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseAQI", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseAQI");
				ini.SetValue("SensorMaps", "AirQualEnabled", val == 1);
				SensorMaps.AirQual = Enumerable.Repeat(val, SensorMaps.AirQual.Length).ToArray();
				ini.SetValue("SensorMaps", "AirQual", SensorMaps.AirQual);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseAQI"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseAQI", 0, 0, 1);
				ini.SetValue("SensorMaps", "AirQualEnabled", val == 1);
				SensorMaps.AirQual = Enumerable.Repeat(val, SensorMaps.AirQual.Length).ToArray();
				ini.SetValue("SensorMaps", "AirQual", SensorMaps.AirQual);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.AirQualEnabled = ini.GetValue("SensorMaps", "AirQualEnabled", false);
				SensorMaps.AirQual = ini.GetValue("SensorMaps", "AirQual", Enumerable.Repeat(0, SensorMaps.AirQual.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseCo2"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseCo2", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseCo2");
				ini.SetValue("SensorMaps", "CO2Enabled", val == 1);
				SensorMaps.CO2 = val;
				ini.SetValue("SensorMaps", "CO2", SensorMaps.CO2);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseCo2"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseCo2", 0, 0, 1);
				ini.SetValue("SensorMaps", "CO2Enabled", val == 1);
				SensorMaps.CO2 = val;
				ini.SetValue("SensorMaps", "CO2", SensorMaps.CO2);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.CO2Enabled = ini.GetValue("SensorMaps", "CO2Enabled", false);
				SensorMaps.CO2 = ini.GetValue("SensorMaps", "CO2", 0, 0, 1);
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseLightning"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseLightning", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseLightning");
				ini.SetValue("SensorMaps", "LightningEnabled", val == 1);
				SensorMaps.Lightning = val;
				ini.SetValue("SensorMaps", "Lightning", SensorMaps.Lightning);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseLightning"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseLightning", 0, 0, 1);
				ini.SetValue("SensorMaps", "LightningEnabled", val == 1);
				SensorMaps.Lightning = val;
				ini.SetValue("SensorMaps", "Lightning", SensorMaps.Lightning);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.LightningEnabled = ini.GetValue("SensorMaps", "LightningEnabled", false);
				SensorMaps.Lightning = ini.GetValue("SensorMaps", "Lightning", 0, 0, 1);
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseLeak"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseLeak", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseLeak");
				ini.SetValue("SensorMaps", "LeakEnabled", val == 1);
				SensorMaps.Leak = Enumerable.Repeat(val, SensorMaps.Leak.Length).ToArray();
				ini.SetValue("SensorMaps", "Leak", SensorMaps.Leak);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseLeak"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseLeak", 0, 0, 1);
				ini.SetValue("SensorMaps", "LeakEnabled", val == 1);
				SensorMaps.Leak = Enumerable.Repeat(val, SensorMaps.Leak.Length).ToArray();
				ini.SetValue("SensorMaps", "Leak", SensorMaps.Leak);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.LeakEnabled = ini.GetValue("SensorMaps", "LeakEnabled", false);
				SensorMaps.Leak = ini.GetValue("SensorMaps", "Leak", Enumerable.Repeat(0, SensorMaps.Leak.Length).ToArray());
			}

			if (ini.ValueExists("GW1000", "ExtraSensorUseCamera"))
			{
				var val = ini.GetValue("GW1000", "ExtraSensorUseCamera", 0, 0, 1);
				ini.DeleteValue("GW1000", "ExtraSensorUseCamera");
				ini.SetValue("SensorMaps", "CameraEnabled", val == 1);
				SensorMaps.Camera = val;
				ini.SetValue("SensorMaps", "Camera", SensorMaps.Camera);
				rewriteRequired = true;
			}
			else if (ini.ValueExists("ExtraSensors", "ExtraSensorUseCamera"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseCamera", 0, 0, 1);
				ini.SetValue("SensorMaps", "CameraEnabled", val == 1);
				SensorMaps.Camera = val;
				ini.SetValue("SensorMaps", "Camera", SensorMaps.Camera);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.CameraEnabled = ini.GetValue("SensorMaps", "CameraEnabled", false);
				SensorMaps.Camera = ini.GetValue("SensorMaps", "Camera", 0, 0, 1);
			}

			if (ini.ValueExists("ExtraSensors", "ExtraSensorUseLaserDist"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseLaserDist", 0, 0, 1);
				ini.SetValue("SensorMaps", "LaserDistEnabled", val == 1);
				SensorMaps.LaserDist = Enumerable.Repeat(val, SensorMaps.LaserDist.Length).ToArray();
				ini.SetValue("SensorMaps", "LaserDist", SensorMaps.LaserDist);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.LaserDistEnabled = ini.GetValue("SensorMaps", "LaserDistEnabled", false);
				SensorMaps.LaserDist = ini.GetValue("SensorMaps", "LaserDist", Enumerable.Repeat(0, SensorMaps.LaserDist.Length).ToArray());
			}

			if (ini.ValueExists("ExtraSensors", "ExtraSensorUseBGT"))
			{
				var val = ini.GetValue("ExtraSensors", "ExtraSensorUseBGT", 0, 0, 1);
				ini.SetValue("SensorMaps", "BlackGlobeEnabled", val == 1);
				SensorMaps.BlackGlobe = val;
				ini.SetValue("SensorMaps", "BlackGlobe", SensorMaps.BlackGlobe);
				rewriteRequired = true;
			}
			else
			{
				SensorMaps.BlackGlobeEnabled = ini.GetValue("SensorMaps", "BlackGlobeEnabled", false);
				SensorMaps.BlackGlobe = ini.GetValue("SensorMaps", "BlackGlobe", 0, 0, 1);
			}

			// Remove the defunct ExtraSensors section
			if (ini.SectionExists("ExtraSensors"))
			{
				ini.DeleteSection("ExtraSensors");
				rewriteRequired = true;
			}

			// Disable all the extra sensors if no extra station enabled (because the previous default was to enable all)
			if (!AmbientExtraEnabled && !JsonExtraStationOptions.ExtraSensorsEnabled && !EcowittExtraEnabled && !EcowittCloudExtraEnabled)
			{
				SensorMaps.Temperature = SensorMaps.Humidity = SensorMaps.DewPoint = SensorMaps.IndoorTemp = SensorMaps.IndoorHum = SensorMaps.Solar = SensorMaps.UV = SensorMaps.CO2 = SensorMaps.Lightning = SensorMaps.Camera = SensorMaps.BlackGlobe = 0;

				SensorMaps.ExtraTempHum = Enumerable.Repeat(0, SensorMaps.ExtraTempHum.Length).ToArray();
				SensorMaps.SoilTemp = Enumerable.Repeat(0, SensorMaps.SoilTemp.Length).ToArray();
				SensorMaps.SoilMoist = Enumerable.Repeat(0, SensorMaps.SoilMoist.Length).ToArray();
				SensorMaps.LeafWet = Enumerable.Repeat(0, SensorMaps.LeafWet.Length).ToArray();
				SensorMaps.UserTemp = Enumerable.Repeat(0, SensorMaps.UserTemp.Length).ToArray();
				SensorMaps.AirQual = Enumerable.Repeat(0, SensorMaps.AirQual.Length).ToArray();
				SensorMaps.Leak = Enumerable.Repeat(0, SensorMaps.Leak.Length).ToArray();
				SensorMaps.LaserDist = Enumerable.Repeat(0, SensorMaps.LaserDist.Length).ToArray();
				SensorMaps.SoilEc = Enumerable.Repeat(0, SensorMaps.SoilEc.Length).ToArray(); ;
			}
			#endregion

			#region AirLink Settings
			// AirLink settings
			// We have to convert previous per AL IsNode config to global
			// So check if the global value exists
			if (ini.ValueExists("AirLink", "IsWllNode"))
			{
				AirLinkIsNode = ini.GetValue("AirLink", "IsWllNode", false);
			}
			else
			{
				AirLinkIsNode = ini.GetValue("AirLink", "In-IsNode", false) || ini.GetValue("AirLink", "Out-IsNode", false);
				ini.DeleteValue("AirLink", "In-IsNode");
				ini.DeleteValue("AirLink", "Out-IsNode");
				ini.SetValue("AirLink", "IsWllNode", AirLinkIsNode);
				rewriteRequired = true;
			}
			AirLinkApiKey = ini.GetValue("AirLink", "WLv2ApiKey", string.Empty);
			AirLinkApiSecret = ini.GetValue("AirLink", "WLv2ApiSecret", string.Empty);
			AirLinkAutoUpdateIpAddress = ini.GetValue("AirLink", "AutoUpdateIpAddress", true);
			AirLinkInEnabled = ini.GetValue("AirLink", "In-Enabled", false);
			AirLinkInIPAddr = ini.GetValue("AirLink", "In-IPAddress", "0.0.0.0");
			AirLinkInStationId = ini.GetValue("AirLink", "In-WLStationId", -1, -1);
			if (AirLinkInStationId == -1 && AirLinkIsNode && WllStationId != -1)
			{
				AirLinkInStationId = WllStationId;
				LogMessage("Cumulus.ini: No AirLinkInStationId supplied, but AirlinkIsNode, so using main station id");
				ini.SetValue("AirLink", "In-WLStationId", AirLinkInStationId);
				rewriteRequired = true;
			}
			AirLinkInHostName = ini.GetValue("AirLink", "In-Hostname", string.Empty);

			AirLinkOutEnabled = ini.GetValue("AirLink", "Out-Enabled", false);
			AirLinkOutIPAddr = ini.GetValue("AirLink", "Out-IPAddress", "0.0.0.0");
			AirLinkOutStationId = ini.GetValue("AirLink", "Out-WLStationId", -1, -1);
			if (AirLinkOutStationId == -1 && AirLinkIsNode && WllStationId != -1)
			{
				AirLinkOutStationId = WllStationId;
				LogMessage("Cumulus.ini: No AirLinkOutStationId supplied, but AirlinkIsNode, so using main station id");
				ini.SetValue("AirLink", "Out-WLStationId", AirLinkOutStationId);
				rewriteRequired = true;
			}
			AirLinkOutHostName = ini.GetValue("AirLink", "Out-Hostname", string.Empty);

			airQualityIndex = ini.GetValue("AirLink", "AQIformula", 0, 0, 1);
			#endregion

			#region FTP Settings
			FtpOptions.Enabled = ini.GetValue("FTP site", "Enabled", true);
			FtpOptions.Hostname = ini.GetValue("FTP site", "Host", string.Empty);
			FtpOptions.Port = ini.GetValue("FTP site", "Port", 21, 1, 65535);
			FtpOptions.Username = ini.GetValue("FTP site", "Username", string.Empty);
			FtpOptions.Password = ini.GetValue("FTP site", "Password", string.Empty);
			FtpOptions.Directory = ini.GetValue("FTP site", "Directory", string.Empty);
			FtpOptions.FtpMode = (FtpProtocols) ini.GetValue("FTP site", "Sslftp", 0, 0, 3);
			if (FtpOptions.Enabled && FtpOptions.Hostname == string.Empty && FtpOptions.FtpMode != FtpProtocols.PHP)
			{
				LogMessage("Cumulus.ini: FTP enabled, but no hostname supplied, disabling FTP");
				FtpOptions.Enabled = false;
				ini.SetValue("FTP site", "Enabled", FtpOptions.Enabled);
				rewriteRequired = true;
			}

			FtpOptions.AutoDetect = ini.GetValue("FTP site", "ConnectionAutoDetect", false);
			FtpOptions.IgnoreCertErrors = ini.GetValue("FTP site", "IgnoreCertErrors", false);
			FtpOptions.ActiveMode = ini.GetValue("FTP site", "ActiveFTP", false);
			// BUILD 3092 - added alternate SFTP authentication options
			FtpOptions.SshAuthen = ini.GetValue("FTP site", "SshFtpAuthentication", "password");
			if (!sshAuthenticationVals.Contains(FtpOptions.SshAuthen))
			{
				FtpOptions.SshAuthen = "password";
				LogWarningMessage($"Cumulus.ini: Error, invalid SshFtpAuthentication value [{FtpOptions.SshAuthen}], defaulting to Password.");
				ini.SetValue("FTP site", "SshFtpAuthentication", FtpOptions.SshAuthen);
				rewriteRequired = true;
			}
			FtpOptions.SshPskFile = ini.GetValue("FTP site", "SshFtpPskFile", string.Empty);
			if (FtpOptions.SshPskFile.Length > 0 && (FtpOptions.SshAuthen == "psk" || FtpOptions.SshAuthen == "password_psk") && !File.Exists(FtpOptions.SshPskFile))
			{
				FtpOptions.SshPskFile = string.Empty;
				LogErrorMessage($"Cumulus.ini: Error, file name specified by SshFtpPskFile does not exist [{FtpOptions.SshPskFile}], removing it.");
				ini.SetValue("FTP site", "SshFtpPskFile", FtpOptions.SshPskFile);
				rewriteRequired = true;
			}
			FtpOptions.DisableEPSV = ini.GetValue("FTP site", "DisableEPSV", false);
			FtpOptions.DisableExplicit = ini.GetValue("FTP site", "DisableFtpsExplicit", false);
			FtpOptions.Logging = ini.GetValue("FTP site", "FTPlogging", false);
			FtpOptions.LoggingLevel = ini.GetValue("FTP site", "FTPloggingLevel", 2);
			RealtimeIntervalEnabled = ini.GetValue("FTP site", "EnableRealtime", false);
			FtpOptions.RealtimeEnabled = ini.GetValue("FTP site", "RealtimeFTPEnabled", false);

			// Local Copy Options
			FtpOptions.LocalCopyEnabled = ini.GetValue("FTP site", "EnableLocalCopy", false);
			FtpOptions.LocalCopyFolder = ini.GetValue("FTP site", "LocalCopyFolder", string.Empty);
			var sep1 = Path.DirectorySeparatorChar.ToString();
			var sep2 = Path.AltDirectorySeparatorChar.ToString();
			if (FtpOptions.LocalCopyFolder.Length > 1 &&
				!(FtpOptions.LocalCopyFolder.EndsWith(sep1) || FtpOptions.LocalCopyFolder.EndsWith(sep2))
				)
			{
				LogMessage("Cumulus.ini: Local copy folder does not end with a directory separator, adding it");
				FtpOptions.LocalCopyFolder += sep1;
				ini.SetValue("FTP site", "LocalCopyFolder", FtpOptions.LocalCopyFolder);
				rewriteRequired = true;
			}

			// PHP upload options
			FtpOptions.PhpUrl = ini.GetValue("FTP site", "PHP-URL", string.Empty);
			FtpOptions.PhpSecret = ini.GetValue("FTP site", "PHP-Secret", string.Empty);
			if (FtpOptions.PhpSecret == string.Empty)
				FtpOptions.PhpSecret = Guid.NewGuid().ToString();
			FtpOptions.PhpIgnoreCertErrors = ini.GetValue("FTP site", "PHP-IgnoreCertErrors", false);
			FtpOptions.MaxConcurrentUploads = ini.GetValue("FTP site", "MaxConcurrentUploads", 2, 1);
			FtpOptions.PhpUseGet = ini.GetValue("FTP site", "PHP-UseGet", true);
			FtpOptions.PhpUseBrotli = ini.GetValue("FTP site", "PHP-UseBrotli", false);

			if (FtpOptions.Enabled && FtpOptions.PhpUrl == string.Empty && FtpOptions.FtpMode == FtpProtocols.PHP)
			{
				LogMessage("Cumulus.ini: PHP upload enabled but the target URL is missing, disabling uploads");
				FtpOptions.Enabled = false;
				ini.SetValue("FTP site", "Enabled", FtpOptions.Enabled);
				rewriteRequired = true;
			}

			MoonImage.Ftp = ini.GetValue("FTP site", "IncludeMoonImage", false);
			MoonImage.Copy = ini.GetValue("FTP site", "CopyMoonImage", false);


			RealtimeFiles[0].Create = ini.GetValue("FTP site", "RealtimeTxtCreate", false);
			RealtimeFiles[0].FTP = ini.GetValue("FTP site", "RealtimeTxtFTP", false);
			RealtimeFiles[0].Copy = ini.GetValue("FTP site", "RealtimeTxtCopy", false);
			RealtimeFiles[1].Create = ini.GetValue("FTP site", "RealtimeGaugesTxtCreate", false);
			RealtimeFiles[1].FTP = ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);
			RealtimeFiles[1].Copy = ini.GetValue("FTP site", "RealtimeGaugesTxtCopy", false);

			RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000, 1);

			WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);  // Deprecated, to be remove at some future date
																			// Have to allow for upgrade, set interval enabled to old WebAutoUpdate
			WebIntervalEnabled = ini.GetValue("FTP site", "IntervalEnabled", WebAutoUpdate);
			FtpOptions.IntervalEnabled = ini.GetValue("FTP site", "IntervalFtpEnabled", WebAutoUpdate);

			UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", DefaultWebUpdateInterval);
			if (UpdateInterval < 1)
			{
				LogMessage("Cumulus.ini: Update interval invalid, resetting to 1");
				UpdateInterval = 1;
				ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
				rewriteRequired = true;
			}
			SynchronisedWebUpdate = (60 % UpdateInterval == 0);

			var IncludeStandardFiles = false;
			if (ini.ValueExists("FTP site", "IncludeSTD"))
			{
				IncludeStandardFiles = ini.GetValue("FTP site", "IncludeSTD", false);
			}
			for (var i = 0; i < StdWebFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				StdWebFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeStandardFiles);
				StdWebFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeStandardFiles);
				StdWebFiles[i].Copy = ini.GetValue("FTP site", keyNameCopy, IncludeStandardFiles);
			}

			var IncludeGraphDataFiles = false;
			if (ini.ValueExists("FTP site", "IncludeGraphDataFiles"))
			{
				IncludeGraphDataFiles = ini.GetValue("FTP site", "IncludeGraphDataFiles", true);
			}
			for (var i = 0; i < GraphDataFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				GraphDataFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeGraphDataFiles);
				GraphDataFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeGraphDataFiles);
				GraphDataFiles[i].Copy = ini.GetValue("FTP site", keyNameCopy, IncludeGraphDataFiles);
			}
			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				GraphDataEodFiles[i].Create = ini.GetValue("FTP site", keyNameCreate, IncludeGraphDataFiles);
				GraphDataEodFiles[i].FTP = ini.GetValue("FTP site", keyNameFTP, IncludeGraphDataFiles);
				GraphDataEodFiles[i].Copy = ini.GetValue("FTP site", keyNameCopy, IncludeGraphDataFiles);
			}

			for (var i = 0; i < HttpFilesConfig.Length; i++)
			{
				HttpFilesConfig[i] = new HttpFileProps();
			}

			FTPRename = ini.GetValue("FTP site", "FTPRename", true);
			UTF8encode = ini.GetValue("FTP site", "UTF8encode", true);
			DeleteBeforeUpload = ini.GetValue("FTP site", "DeleteBeforeUpload", false);

			for (int i = 0; i < numextrafiles; i++)
			{
				ExtraFiles[i] = new CExtraFiles
				{
					local = ini.GetValue("FTP site", "ExtraLocal" + i, string.Empty),
					remote = ini.GetValue("FTP site", "ExtraRemote" + i, string.Empty),
					process = ini.GetValue("FTP site", "ExtraProcess" + i, false),
					binary = ini.GetValue("FTP site", "ExtraBinary" + i, false),
					realtime = ini.GetValue("FTP site", "ExtraRealtime" + i, false),
					FTP = ini.GetValue("FTP site", "ExtraFTP" + i, false),
					UTF8 = ini.GetValue("FTP site", "ExtraUTF" + i, false),
					endofday = ini.GetValue("FTP site", "ExtraEOD" + i, false),
					incrementalLogfile = ini.GetValue("FTP site", "ExtraIncLogFile" + i, false)
				};

				if (ExtraFiles[i].binary)
				{
					ExtraFiles[i].incrementalLogfile = false;
				}

				if (ini.ValueExists("FTP site", "ExtraEnable" + i))
				{
					ExtraFiles[i].enable = ini.GetValue("FTP site", "ExtraEnable" + i, false);
				}
				else
				{
					ExtraFiles[i].enable = !string.IsNullOrEmpty(ExtraFiles[i].local) && !string.IsNullOrEmpty(ExtraFiles[i].remote);
				}

				if (ExtraFiles[i].enable && ExtraFiles[i].local != string.Empty && ExtraFiles[i].remote != string.Empty)
				{
					ActiveExtraFiles.Add(new CExtraFiles
					{
						enable = ExtraFiles[i].enable,
						local = ExtraFiles[i].local,
						remote = ExtraFiles[i].remote,
						process = ExtraFiles[i].process,
						binary = ExtraFiles[i].binary,
						realtime = ExtraFiles[i].realtime,
						FTP = ExtraFiles[i].FTP,
						UTF8 = ExtraFiles[i].UTF8,
						endofday = ExtraFiles[i].endofday,
						incrementalLogfile = ExtraFiles[i].incrementalLogfile,
						logFileLastFileName = string.Empty,
						logFileLastLineNumber = 0
					});
				}
			}

			ExternalProgram = ini.GetValue("FTP site", "ExternalProgram", string.Empty);
			RealtimeProgram = ini.GetValue("FTP site", "RealtimeProgram", string.Empty);
			DailyProgram = ini.GetValue("FTP site", "DailyProgram", string.Empty);
			ExternalParams = ini.GetValue("FTP site", "ExternalParams", string.Empty);
			RealtimeParams = ini.GetValue("FTP site", "RealtimeParams", string.Empty);
			DailyParams = ini.GetValue("FTP site", "DailyParams", string.Empty);
			#endregion

			ForumURL = ini.GetValue("Web Site", "ForumURL", ForumDefault);
			WebcamURL[0] = ini.GetValue("Web Site", "WebcamURL", string.Empty);
			WebcamURL[1] = ini.GetValue("Web Site", "WebcamURL1", string.Empty);
			WebcamURL[2] = ini.GetValue("Web Site", "WebcamURL2", string.Empty);
			WebcamURL[3] = ini.GetValue("Web Site", "WebcamURL3", string.Empty);

			CloudBaseInFeet = ini.GetValue("Station", "CloudBaseInFeet", true);

			#region Graph Options
			GraphDays = ini.GetValue("Graphs", "ChartMaxDays", 31, 1);
			GraphHours = ini.GetValue("Graphs", "GraphHours", 72, 1);
			RecentDataDays = (int) Math.Ceiling(Math.Max(7, GraphHours / 24.0));
			MoonImage.Enabled = ini.GetValue("Graphs", "MoonImageEnabled", false);
			MoonImage.Size = ini.GetValue("Graphs", "MoonImageSize", 100, 10);
			MoonImage.Transparent = ini.GetValue("Graphs", "MoonImageShadeTransparent", false);
			MoonImage.FtpDest = ini.GetValue("Graphs", "MoonImageFtpDest", "images/moon.png");
			MoonImage.CopyDest = ini.GetValue("Graphs", "MoonImageCopyDest", FtpOptions.LocalCopyFolder + "images" + sep1 + "moon.png");
			GraphOptions.Visible.Temp.Val = ini.GetValue("Graphs", "TempVisible", 1, 0, 2);
			GraphOptions.Visible.InTemp.Val = ini.GetValue("Graphs", "InTempVisible", 1, 0, 2);
			GraphOptions.Visible.HeatIndex.Val = ini.GetValue("Graphs", "HIVisible", 1, 0, 2);
			GraphOptions.Visible.DewPoint.Val = ini.GetValue("Graphs", "DPVisible", 1, 0, 2);
			GraphOptions.Visible.WindChill.Val = ini.GetValue("Graphs", "WCVisible", 1, 0, 2);
			GraphOptions.Visible.AppTemp.Val = ini.GetValue("Graphs", "AppTempVisible", 1, 0, 2);
			GraphOptions.Visible.FeelsLike.Val = ini.GetValue("Graphs", "FeelsLikeVisible", 1, 0, 2);
			GraphOptions.Visible.Humidex.Val = ini.GetValue("Graphs", "HumidexVisible", 1, 0, 2);
			GraphOptions.Visible.InHum.Val = ini.GetValue("Graphs", "InHumVisible", 1, 0, 2);
			GraphOptions.Visible.OutHum.Val = ini.GetValue("Graphs", "OutHumVisible", 1, 0, 2);
			GraphOptions.Visible.UV.Val = ini.GetValue("Graphs", "UVVisible", 1, 0, 2);
			GraphOptions.Visible.Solar.Val = ini.GetValue("Graphs", "SolarVisible", 1, 0, 2);
			GraphOptions.Visible.Sunshine.Val = ini.GetValue("Graphs", "SunshineVisible", 1, 0, 2);
			GraphOptions.Visible.AvgTemp.Val = ini.GetValue("Graphs", "DailyAvgTempVisible", 1, 0, 2);
			GraphOptions.Visible.MaxTemp.Val = ini.GetValue("Graphs", "DailyMaxTempVisible", 1, 0, 2);
			GraphOptions.Visible.MinTemp.Val = ini.GetValue("Graphs", "DailyMinTempVisible", 1, 0, 2);
			GraphOptions.Visible.BGT.Val = ini.GetValue("Graphs", "BGTVisible", 0, 0, 2);
			GraphOptions.Visible.GrowingDegreeDays1.Val = ini.GetValue("Graphs", "GrowingDegreeDaysVisible1", 1, 0, 2);
			GraphOptions.Visible.GrowingDegreeDays2.Val = ini.GetValue("Graphs", "GrowingDegreeDaysVisible2", 1, 0, 2);
			GraphOptions.Visible.TempSum0.Val = ini.GetValue("Graphs", "TempSumVisible0", 1, 0, 2);
			GraphOptions.Visible.TempSum1.Val = ini.GetValue("Graphs", "TempSumVisible1", 1, 0, 2);
			GraphOptions.Visible.TempSum2.Val = ini.GetValue("Graphs", "TempSumVisible2", 1, 0, 2);
			GraphOptions.Visible.ChillHours.Val = ini.GetValue("Graphs", "ChillHoursVisible", 1, 0, 2);
			GraphOptions.Visible.ExtraTemp.Vals = ini.GetValue("Graphs", "ExtraTempVisible", new int[16]);
			GraphOptions.Visible.ExtraHum.Vals = ini.GetValue("Graphs", "ExtraHumVisible", new int[16]);
			GraphOptions.Visible.ExtraDewPoint.Vals = ini.GetValue("Graphs", "ExtraDewPointVisible", new int[16]);
			GraphOptions.Visible.SoilTemp.Vals = ini.GetValue("Graphs", "SoilTempVisible", new int[16]);
			GraphOptions.Visible.SoilMoist.Vals = ini.GetValue("Graphs", "SoilMoistVisible", new int[16]);
			GraphOptions.Visible.SoilEc.Vals = ini.GetValue("Graphs", "SoilEcVisible", new int[16]);
			GraphOptions.Visible.UserTemp.Vals = ini.GetValue("Graphs", "UserTempVisible", new int[8]);
			GraphOptions.Visible.LeafWetness.Vals = ini.GetValue("Graphs", "LeafWetnessVisible", new int[8]);
			GraphOptions.Visible.AqSensor.Pm.Vals = ini.GetValue("Graphs", "Aq-PmVisible", new int[4]);
			GraphOptions.Visible.AqSensor.PmAvg.Vals = ini.GetValue("Graphs", "Aq-PmAvgVisible", new int[4]);
			GraphOptions.Visible.AqSensor.Pm10.Vals = ini.GetValue("Graphs", "Aq-Pm10Visible", GraphOptions.Visible.AqSensor.Pm.Vals);
			GraphOptions.Visible.AqSensor.Pm10Avg.Vals = ini.GetValue("Graphs", "Aq-Pm10AvgVisible", GraphOptions.Visible.AqSensor.PmAvg.Vals);
			GraphOptions.Visible.AqSensor.Temp.Vals = ini.GetValue("Graphs", "Aq-TempVisible", new int[4]);
			GraphOptions.Visible.AqSensor.Hum.Vals = ini.GetValue("Graphs", "Aq-HumVisible", new int[4]);
			GraphOptions.Visible.CO2Sensor.CO2.Val = ini.GetValue("Graphs", "CO2-CO2", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.CO2Avg.Val = ini.GetValue("Graphs", "CO2-CO2Avg", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm25.Val = ini.GetValue("Graphs", "CO2-Pm25", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm25Avg.Val = ini.GetValue("Graphs", "CO2-Pm25Avg", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm10.Val = ini.GetValue("Graphs", "CO2-Pm10", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Pm10Avg.Val = ini.GetValue("Graphs", "CO2-Pm10Avg", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Temp.Val = ini.GetValue("Graphs", "CO2-Temp", 0, 0, 2);
			GraphOptions.Visible.CO2Sensor.Hum.Val = ini.GetValue("Graphs", "CO2-Hum", 0, 0, 2);
			GraphOptions.Visible.SnowDepth.Val = ini.GetValue("Graphs", "SnowDepth", 0, 0, 2);
			GraphOptions.Visible.Snow24h.Val = ini.GetValue("Graphs", "Snow24h", 0, 0, 2);
			GraphOptions.Visible.CurrSnow24h.Vals = ini.GetValue("Graphs", "CurrSnow24h", new int[4]);

			GraphOptions.Visible.LaserDepth.Vals = ini.GetValue("Graphs", "LaserDepthVisible", new int[4]);
			GraphOptions.Visible.LaserDist.Vals = ini.GetValue("Graphs", "LaserDistanceVisible", new int[4]);

			GraphOptions.Colour.Temp = ini.GetValue("GraphColours", "TempColour", "#ff0000");
			GraphOptions.Colour.InTemp = ini.GetValue("GraphColours", "InTempColour", "#50b432");
			GraphOptions.Colour.HeatIndex = ini.GetValue("GraphColours", "HIColour", "#9161c9");
			GraphOptions.Colour.DewPoint = ini.GetValue("GraphColours", "DPColour", "#ffff00");
			GraphOptions.Colour.WindChill = ini.GetValue("GraphColours", "WCColour", "#ffa500");
			GraphOptions.Colour.AppTemp = ini.GetValue("GraphColours", "AppTempColour", "#00fffe");
			GraphOptions.Colour.FeelsLike = ini.GetValue("GraphColours", "FeelsLikeColour", "#00fffe");
			GraphOptions.Colour.Humidex = ini.GetValue("GraphColours", "HumidexColour", "#008000");
			GraphOptions.Colour.BGT = ini.GetValue("GraphColours", "BGTColour", "#6495ed");
			GraphOptions.Colour.WBGT = ini.GetValue("GraphColours", "WBGTColour", "#3dd457");
			GraphOptions.Colour.InHum = ini.GetValue("GraphColours", "InHumColour", "#008000");
			GraphOptions.Colour.OutHum = ini.GetValue("GraphColours", "OutHumColour", "#ff0000");
			GraphOptions.Colour.Press = ini.GetValue("GraphColours", "PressureColour", "#6495ed");
			GraphOptions.Colour.WindGust = ini.GetValue("GraphColours", "WindGustColour", "#ff0000");
			GraphOptions.Colour.WindAvg = ini.GetValue("GraphColours", "WindAvgColour", "#6495ed");
			GraphOptions.Colour.WindRun = ini.GetValue("GraphColours", "WindRunColour", "#3dd457");
			GraphOptions.Colour.WindBearing = ini.GetValue("GraphColours", "WindBearingColour", "#6495ed");
			GraphOptions.Colour.WindBearingAvg = ini.GetValue("GraphColours", "WindBearingAvgColour", "#ff0000");
			GraphOptions.Colour.Rainfall = ini.GetValue("GraphColours", "Rainfall", "#6495ed");
			GraphOptions.Colour.RainRate = ini.GetValue("GraphColours", "RainRate", "#ff0000");
			GraphOptions.Colour.UV = ini.GetValue("GraphColours", "UVColour", "#8a2be2");
			GraphOptions.Colour.Solar = ini.GetValue("GraphColours", "SolarColour", "#ff8c00");
			GraphOptions.Colour.SolarTheoretical = ini.GetValue("GraphColours", "SolarTheoreticalColour", "#6464ff");
			GraphOptions.Colour.Sunshine = ini.GetValue("GraphColours", "SunshineColour", "#ff8c00");
			GraphOptions.Colour.MaxTemp = ini.GetValue("GraphColours", "MaxTempColour", "#ff0000");
			GraphOptions.Colour.AvgTemp = ini.GetValue("GraphColours", "AvgTempColour", "#008000");
			GraphOptions.Colour.MinTemp = ini.GetValue("GraphColours", "MinTempColour", "#6495ed");
			GraphOptions.Colour.MaxPress = ini.GetValue("GraphColours", "MaxPressColour", "#6495ed");
			GraphOptions.Colour.MinPress = ini.GetValue("GraphColours", "MinPressColour", "#39ef74");
			GraphOptions.Colour.MaxOutHum = ini.GetValue("GraphColours", "MaxHumColour", "#6495ed");
			GraphOptions.Colour.MinOutHum = ini.GetValue("GraphColours", "MinHumColour", "#39ef74");
			GraphOptions.Colour.MaxHeatIndex = ini.GetValue("GraphColours", "MaxHIColour", "#ffa500");
			GraphOptions.Colour.MaxDew = ini.GetValue("GraphColours", "MaxDPColour", "#dada00");
			GraphOptions.Colour.MinDew = ini.GetValue("GraphColours", "MinDPColour", "#ffc0cb");
			GraphOptions.Colour.MaxFeels = ini.GetValue("GraphColours", "MaxFeelsLikeColour", "#00ffff");
			GraphOptions.Colour.MinFeels = ini.GetValue("GraphColours", "MinFeelsLikeColour", "#800080");
			GraphOptions.Colour.MaxApp = ini.GetValue("GraphColours", "MaxAppTempColour", "#808080");
			GraphOptions.Colour.MinApp = ini.GetValue("GraphColours", "MinAppTempColour", "#a52a2a");
			GraphOptions.Colour.MaxHumidex = ini.GetValue("GraphColours", "MaxHumidexColour", "#c7b72a");
			GraphOptions.Colour.Pm2p5 = ini.GetValue("GraphColours", "Pm2p5Colour", "#6495ed");
			GraphOptions.Colour.Pm10 = ini.GetValue("GraphColours", "Pm10Colour", "#008000");
			var colours16 = new List<string>(16) { "#ff0000", "#008000", "#0000ff", "#ffa500", "#dada00", "#ffc0cb", "#00ffff", "#800080", "#808080", "#a52a2a", "#c7b72a", "#7fffd4", "#adff2f", "#ff7f50", "#ff00ff", "#00b2ff" };
			var colours8 = colours16.Take(8).ToArray();
			var colours4 = colours16.Take(4).ToArray();
			var colours2 = colours16.Take(2).ToArray();
			GraphOptions.Colour.ExtraTemp = ini.GetValue("GraphColours", "ExtraTempColour", colours16.ToArray());
			GraphOptions.Colour.ExtraHum = ini.GetValue("GraphColours", "ExtraHumColour", colours16.ToArray());
			GraphOptions.Colour.ExtraDewPoint = ini.GetValue("GraphColours", "ExtraDewPointColour", colours16.ToArray());
			GraphOptions.Colour.SoilTemp = ini.GetValue("GraphColours", "SoilTempColour", colours16.ToArray());
			GraphOptions.Colour.SoilMoist = ini.GetValue("GraphColours", "SoilMoistColour", colours16.ToArray());
			GraphOptions.Colour.SoilEc = ini.GetValue("GraphColours", "SoilEcColour", colours16.ToArray());
			GraphOptions.Colour.LeafWetness = ini.GetValue("GraphColours", "LeafWetness", colours2);
			GraphOptions.Colour.UserTemp = ini.GetValue("GraphColours", "UserTempColour", colours8);
			GraphOptions.Colour.LaserDepth = ini.GetValue("GraphColours", "LaserDepthColour", colours4);
			GraphOptions.Colour.CO2Sensor.CO2 = ini.GetValue("GraphColours", "CO2-CO2Colour", "#dc143c");
			GraphOptions.Colour.CO2Sensor.CO2Avg = ini.GetValue("GraphColours", "CO2-CO2AvgColour", "#8b0000");
			GraphOptions.Colour.CO2Sensor.Pm25 = ini.GetValue("GraphColours", "CO2-Pm25Colour", "#00bfff");
			GraphOptions.Colour.CO2Sensor.Pm25Avg = ini.GetValue("GraphColours", "CO2-Pm25AvgColour", "#1e90ff");
			GraphOptions.Colour.CO2Sensor.Pm10 = ini.GetValue("GraphColours", "CO2-Pm10Colour", "#d2691e");
			GraphOptions.Colour.CO2Sensor.Pm10Avg = ini.GetValue("GraphColours", "CO2-Pm10AvgColour", "#b8860b");
			GraphOptions.Colour.CO2Sensor.Temp = ini.GetValue("GraphColours", "CO2-TempColour", "#ff0000");
			GraphOptions.Colour.CO2Sensor.Hum = ini.GetValue("GraphColours", "CO2-HumColour", "#008000");
			GraphOptions.Colour.SnowDepth = ini.GetValue("GraphColours", "SnowDepthColour", "#6495ed");
			GraphOptions.Colour.Snow24h = ini.GetValue("GraphColours", "Snow24hColour", "#008000");
			#endregion

			#region Third Party Uploads

			#region Weather Underground Options
			Wund.ID = ini.GetValue("Wunderground", "ID", string.Empty);
			Wund.PW = ini.GetValue("Wunderground", "Password", string.Empty);
			Wund.Enabled = ini.GetValue("Wunderground", "Enabled", false);
			Wund.RapidFireEnabled = ini.GetValue("Wunderground", "RapidFire", false);
			Wund.Interval = ini.GetValue("Wunderground", "Interval", Wund.DefaultInterval);
			Wund.SendUV = ini.GetValue("Wunderground", "SendUV", false);
			Wund.SendSolar = ini.GetValue("Wunderground", "SendSR", false);
			Wund.SendIndoor = ini.GetValue("Wunderground", "SendIndoor", false);
			Wund.SendSoilTemp1 = ini.GetValue("Wunderground", "SendSoilTemp1", false);
			Wund.SendSoilTemp2 = ini.GetValue("Wunderground", "SendSoilTemp2", false);
			Wund.SendSoilTemp3 = ini.GetValue("Wunderground", "SendSoilTemp3", false);
			Wund.SendSoilTemp4 = ini.GetValue("Wunderground", "SendSoilTemp4", false);
			Wund.SendSoilMoisture1 = ini.GetValue("Wunderground", "SendSoilMoisture1", false);
			Wund.SendSoilMoisture2 = ini.GetValue("Wunderground", "SendSoilMoisture2", false);
			Wund.SendSoilMoisture3 = ini.GetValue("Wunderground", "SendSoilMoisture3", false);
			Wund.SendSoilMoisture4 = ini.GetValue("Wunderground", "SendSoilMoisture4", false);
			Wund.SendLeafWetness1 = ini.GetValue("Wunderground", "SendLeafWetness1", false);
			Wund.SendLeafWetness2 = ini.GetValue("Wunderground", "SendLeafWetness2", false);
			Wund.SendAirQuality = ini.GetValue("Wunderground", "SendAirQuality", false);
			Wund.SendExtraTemp1 = ini.GetValue("Wunderground", "SendExtraTemp1", 0, 0, 10);
			Wund.SendExtraTemp2 = ini.GetValue("Wunderground", "SendExtraTemp2", 0, 0, 10);
			Wund.SendExtraTemp3 = ini.GetValue("Wunderground", "SendExtraTemp3", 0, 0, 10);
			Wund.SendExtraTemp4 = ini.GetValue("Wunderground", "SendExtraTemp4", 0, 0, 10);
			Wund.SendAverage = ini.GetValue("Wunderground", "SendAverage", false);
			Wund.CatchUp = ini.GetValue("Wunderground", "CatchUp", true);

			Wund.SynchronisedUpdate = !Wund.RapidFireEnabled;
			#endregion Weather Underground Options

			#region Windy Options
			Windy.PW = ini.GetValue("Windy", "Password", string.Empty);
			Windy.ApiKey = ini.GetValue("Windy", "APIkey", string.Empty);
			Windy.StationId = ini.GetValue("Windy", "StationId", string.Empty);
			Windy.Enabled = ini.GetValue("Windy", "Enabled", false);
			Windy.Interval = ini.GetValue("Windy", "Interval", Windy.DefaultInterval);
			if (Windy.Interval < 5)
			{
				LogMessage("Cumulus.ini: Windy upload interval set to less than 5 mins, resetting to 5");
				Windy.Interval = 5;
				ini.SetValue("Windy", "Interval", Windy.Interval);
				rewriteRequired = true;
			}
			Windy.SendUV = ini.GetValue("Windy", "SendUV", false);
			Windy.SendSolar = ini.GetValue("Windy", "SendSolar", false);
			Windy.CatchUp = false;
			#endregion Wind Options

			#region AWEKAS Options
			AWEKAS.ID = ini.GetValue("Awekas", "User", string.Empty);
			AWEKAS.PW = ini.GetValue("Awekas", "Password", string.Empty);
			AWEKAS.Enabled = ini.GetValue("Awekas", "Enabled", false);
			AWEKAS.Interval = ini.GetValue("Awekas", "Interval", AWEKAS.DefaultInterval, 15);
			AWEKAS.Lang = ini.GetValue("Awekas", "Language", "en");
			AWEKAS.OriginalInterval = AWEKAS.Interval;
			AWEKAS.SendUV = ini.GetValue("Awekas", "SendUV", false);
			AWEKAS.SendSolar = ini.GetValue("Awekas", "SendSR", false);
			AWEKAS.SendSoilTemp = ini.GetValue("Awekas", "SendSoilTemp", false);
			AWEKAS.SendIndoor = ini.GetValue("Awekas", "SendIndoor", false);
			AWEKAS.SendSoilMoisture = ini.GetValue("Awekas", "SendSoilMoisture", false);
			AWEKAS.SendLeafWetness = ini.GetValue("Awekas", "SendLeafWetness", false);
			AWEKAS.SendAirQuality = ini.GetValue("Awekas", "SendAirQuality", false);

			AWEKAS.SynchronisedUpdate = (AWEKAS.Interval % 60 == 0);
			#endregion AWEKAS Options

			#region Wind Guru Options
			WindGuru.ID = ini.GetValue("WindGuru", "StationUID", string.Empty);
			WindGuru.PW = ini.GetValue("WindGuru", "Password", string.Empty);
			WindGuru.Enabled = ini.GetValue("WindGuru", "Enabled", false);
			WindGuru.Interval = ini.GetValue("WindGuru", "Interval", WindGuru.DefaultInterval);
			if (WindGuru.Interval < 1)
			{
				LogMessage("Cumulus.ini: WindGuru update interval invalid, resetting to 1");
				WindGuru.Interval = 1;
				ini.SetValue("WindGuru", "Interval", WindGuru.Interval);
				rewriteRequired = true;
			}
			WindGuru.SendRain = ini.GetValue("WindGuru", "SendRain", false);
			#endregion Wind Guru Options

			#region WeatherCloud Options
			WCloud.ID = ini.GetValue("WeatherCloud", "Wid", string.Empty);
			WCloud.PW = ini.GetValue("WeatherCloud", "Key", string.Empty);
			WCloud.Enabled = ini.GetValue("WeatherCloud", "Enabled", false);
			WCloud.Interval = ini.GetValue("WeatherCloud", "Interval", WCloud.DefaultInterval);
			WCloud.SendUV = ini.GetValue("WeatherCloud", "SendUV", false);
			WCloud.SendSolar = ini.GetValue("WeatherCloud", "SendSR", false);
			WCloud.SendAirQuality = ini.GetValue("WeatherCloud", "SendAirQuality", false);
			WCloud.SendSoilMoisture = ini.GetValue("WeatherCloud", "SendSoilMoisture", false);
			WCloud.SoilMoistureSensor = ini.GetValue("WeatherCloud", "SoilMoistureSensor", 1);
			WCloud.SendLeafWetness = ini.GetValue("WeatherCloud", "SendLeafWetness", false);
			WCloud.LeafWetnessSensor = ini.GetValue("WeatherCloud", "LeafWetnessSensor", 1, 1, 8);
			#endregion WeatherCloud Options

			#region PWS Options
			PWS.ID = ini.GetValue("PWSweather", "ID", string.Empty);
			PWS.PW = ini.GetValue("PWSweather", "Password", string.Empty);
			PWS.Enabled = ini.GetValue("PWSweather", "Enabled", false);
			PWS.Interval = ini.GetValue("PWSweather", "Interval", PWS.DefaultInterval, 1);
			PWS.SendUV = ini.GetValue("PWSweather", "SendUV", false);
			PWS.SendSolar = ini.GetValue("PWSweather", "SendSR", false);
			PWS.CatchUp = ini.GetValue("PWSweather", "CatchUp", true);
			#endregion PWS Options

			#region WOW Options
			WOW.ID = ini.GetValue("WOW", "ID", string.Empty);
			WOW.PW = ini.GetValue("WOW", "Password", string.Empty);
			WOW.Enabled = ini.GetValue("WOW", "Enabled", false);
			WOW.Interval = ini.GetValue("WOW", "Interval", WOW.DefaultInterval, 1);
			WOW.SendUV = ini.GetValue("WOW", "SendUV", false);
			WOW.SendSolar = ini.GetValue("WOW", "SendSR", false);
			WOW.SendSoilTemp = ini.GetValue("WOW", "SendSoilTemp", false);
			WOW.SoilTempSensor = ini.GetValue("WOW", "SoilTempSensor", 1, 1, 16);
			WOW.SendSoilMoisture = ini.GetValue("WOW", "SendSoilMoist", false);
			WOW.SoilMoistureSensor = ini.GetValue("WOW", "SoilMoistSensor", 1, 1, 16);
			WOW.CatchUp = false;

			WOW_BE.ID = ini.GetValue("WOW-BE", "ID", string.Empty);
			WOW_BE.PW = ini.GetValue("WOW-BE", "Password", string.Empty);
			WOW_BE.Enabled = ini.GetValue("WOW-BE", "Enabled", false);
			WOW_BE.Interval = ini.GetValue("WOW-BE", "Interval", WOW_BE.DefaultInterval, 1);
			WOW_BE.SendUV = ini.GetValue("WOW-BE", "SendUV", false);
			WOW_BE.SendSolar = ini.GetValue("WOW-BE", "SendSR", false);
			WOW_BE.SendSoilTemp = ini.GetValue("WOW-BE", "SendSoilTemp", false);
			WOW_BE.SoilTempSensor = ini.GetValue("WOW-BE", "SoilTempSensor", 1, 1, 16);
			WOW_BE.SendSoilMoisture = ini.GetValue("WOW-BE", "SendSoilMoist", false);
			WOW_BE.SoilMoistureSensor = ini.GetValue("WOW-BE", "SoilMoistSensor", 1, 1, 16);
			WOW_BE.CatchUp = false;
			#endregion WOW Options

			#region APRS Options
			APRS.ID = ini.GetValue("APRS", "ID", string.Empty);
			APRS.PW = ini.GetValue("APRS", "pass", "-1");
			APRS.Server = ini.GetValue("APRS", "server", "cwop.aprs.net");
			APRS.Port = ini.GetValue("APRS", "port", 14580);
			APRS.Enabled = ini.GetValue("APRS", "Enabled", false);
			APRS.Interval = ini.GetValue("APRS", "Interval", APRS.DefaultInterval, 1);
			APRS.HumidityCutoff = ini.GetValue("APRS", "APRSHumidityCutoff", false);
			APRS.SendSolar = ini.GetValue("APRS", "SendSR", false);
			APRS.UseUtcInWxNowFile = ini.GetValue("APRS", "UseUtcInWxNowFile", false);
			#endregion APRS Options

			#region Open Weather Map Options
			OpenWeatherMap.Enabled = ini.GetValue("OpenWeatherMap", "Enabled", false);
			OpenWeatherMap.CatchUp = ini.GetValue("OpenWeatherMap", "CatchUp", true);
			OpenWeatherMap.PW = ini.GetValue("OpenWeatherMap", "APIkey", string.Empty);
			OpenWeatherMap.ID = ini.GetValue("OpenWeatherMap", "StationId", string.Empty);
			OpenWeatherMap.Interval = ini.GetValue("OpenWeatherMap", "Interval", OpenWeatherMap.DefaultInterval, 1);
			#endregion Open Weather Map Options

			#region BlueSky Options
			Bluesky.Enabled = ini.GetValue("Bluesky", "Enabled", false);
			Bluesky.ID = ini.GetValue("Bluesky", "ID", string.Empty);
			Bluesky.PW = ini.GetValue("Bluesky", "Password", string.Empty);
			Bluesky.Interval = ini.GetValue("Bluesky", "Interval", Bluesky.DefaultInterval, 0, 1440);
			if (Bluesky.Interval > 0 && Bluesky.Interval < 60) Bluesky.Interval = 60;
			Bluesky.Language = ini.GetValue("Bluesky", "Language", CultureInfo.CurrentCulture.Name);
			Bluesky.BaseUrl = ini.GetValue("Bluesky", "BaseUrl", "https://bsky.social");
			Bluesky.CatchUp = false;
			for (var i = 0; i < Bluesky.TimedPostsTime.Length; i++)
			{
				if (ini.ValueExists("Bluesky", "TimedPost" + i))
				{
					Bluesky.TimedPostsTime[i] = DateTime.ParseExact(ini.GetValue("Bluesky", "TimedPost" + i, "00:00"), "HH:mm", System.Globalization.CultureInfo.InvariantCulture).TimeOfDay;
					Bluesky.TimedPostsFile[i] = ini.GetValue("Bluesky", "TimedPostFile" + i, Path.Combine("web", "Bluesky.txt"));
				}
				else
				{
					Bluesky.TimedPostsTime[i] = TimeSpan.MaxValue;
					Bluesky.TimedPostsFile[i] = null;
				}
			}
			for (var i = 0; i < Bluesky.VariablePostsTime.Length; i++)
			{
				if (ini.ValueExists("Bluesky", "VariablePost" + i))
				{
					Bluesky.VariablePostsTime[i] = ini.GetValue("Bluesky", "VariablePost" + i, string.Empty);
					Bluesky.VariablePostsFile[i] = ini.GetValue("Bluesky", "VariablePostFile" + i, string.Empty);
				}
				else
				{
					Bluesky.VariablePostsTime[i] = null;
					Bluesky.VariablePostsFile[i] = null;
				}
			}
			#endregion BlueSky Options

			#endregion Third Party Uploads


			#region MQTT Settings
			MQTT.Server = ini.GetValue("MQTT", "Server", string.Empty);
			MQTT.Port = ini.GetValue("MQTT", "Port", 1883, 1, 65535);
			MQTT.Username = ini.GetValue("MQTT", "Username", string.Empty);
			MQTT.Password = ini.GetValue("MQTT", "Password", string.Empty);

			MQTT.UseTLS = ini.GetValue("MQTT", "UseTLS", false);
			MQTT.IpVersion = ini.GetValue("MQTT", "IPversion", 0, 0, 6); // 0 = unspecified, 4 = force IPv4, 6 = force IPv6
			if (MQTT.IpVersion != 0 && MQTT.IpVersion != 4 && MQTT.IpVersion != 6)
			{
				LogMessage("Cumulus.ini: MQTT IP Version invalid, restting to unspecified");
				MQTT.IpVersion = 0;
				ini.SetValue("MQTT", "IPversion", MQTT.IpVersion);
				rewriteRequired = true;
			}
			MQTT.ProtocolVersion = ini.GetValue("MQTT", "ProtocolVersion", 5, 3, 5); // 3 = MQTT 3.1.0, 4 = MQTT 3.1.1 with TLS, 5 = MQTT 5.0

			MQTT.EnableDataUpdate = ini.GetValue("MQTT", "EnableDataUpdate", false);
			MQTT.UpdateTemplate = ini.GetValue("MQTT", "UpdateTemplate", "DataUpdateTemplate.txt");
			MQTT.EnableInterval = ini.GetValue("MQTT", "EnableInterval", false);
			MQTT.IntervalTemplate = ini.GetValue("MQTT", "IntervalTemplate", "IntervalTemplate.txt");
			#endregion

			#region Alarm Settings
			LowTempAlarm.Value = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
			LowTempAlarm.Enabled = ini.GetValue("Alarms", "LowTempAlarmSet", false);
			LowTempAlarm.Sound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
			LowTempAlarm.SoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
			if (LowTempAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				LowTempAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			LowTempAlarm.Notify = ini.GetValue("Alarms", "LowTempAlarmNotify", false);
			LowTempAlarm.Email = ini.GetValue("Alarms", "LowTempAlarmEmail", false);
			LowTempAlarm.Latch = ini.GetValue("Alarms", "LowTempAlarmLatch", false);
			LowTempAlarm.LatchHours = ini.GetValue("Alarms", "LowTempAlarmLatchHours", 24.0, 0.0);
			LowTempAlarm.Action = ini.GetValue("Alarms", "LowTempAlarmAction", string.Empty);
			LowTempAlarm.ActionParams = ini.GetValue("Alarms", "LowTempAlarmActionParams", string.Empty);
			LowTempAlarm.ShowWindow = ini.GetValue("Alarms", "LowTempAlarmActionWindow", false);
			LowTempAlarm.BskyFile = ini.GetValue("Alarms", "LowTempAlarmBlueskyFile", string.Empty);

			HighTempAlarm.Value = ini.GetValue("Alarms", "alarmhightemp", 0.0);
			HighTempAlarm.Enabled = ini.GetValue("Alarms", "HighTempAlarmSet", false);
			HighTempAlarm.Sound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
			HighTempAlarm.SoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
			if (HighTempAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighTempAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighTempAlarm.Notify = ini.GetValue("Alarms", "HighTempAlarmNotify", false);
			HighTempAlarm.Email = ini.GetValue("Alarms", "HighTempAlarmEmail", false);
			HighTempAlarm.Latch = ini.GetValue("Alarms", "HighTempAlarmLatch", false);
			HighTempAlarm.LatchHours = ini.GetValue("Alarms", "HighTempAlarmLatchHours", 24.0, 0.0);
			HighTempAlarm.Action = ini.GetValue("Alarms", "HighTempAlarmAction", string.Empty);
			HighTempAlarm.ActionParams = ini.GetValue("Alarms", "HighTempAlarmActionParams", string.Empty);
			HighTempAlarm.ShowWindow = ini.GetValue("Alarms", "HighTempAlarmActionWindow", false);
			HighTempAlarm.BskyFile = ini.GetValue("Alarms", "HighTempAlarmBlueskyFile", string.Empty);

			TempChangeAlarm.Value = ini.GetValue("Alarms", "alarmtempchange", 0.0);
			TempChangeAlarm.Enabled = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
			TempChangeAlarm.Sound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
			TempChangeAlarm.SoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
			if (TempChangeAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				TempChangeAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			TempChangeAlarm.Notify = ini.GetValue("Alarms", "TempChangeAlarmNotify", false);
			TempChangeAlarm.Email = ini.GetValue("Alarms", "TempChangeAlarmEmail", false);
			TempChangeAlarm.Latch = ini.GetValue("Alarms", "TempChangeAlarmLatch", false);
			TempChangeAlarm.LatchHours = ini.GetValue("Alarms", "TempChangeAlarmLatchHours", 24.0, 0.0);
			TempChangeAlarm.Action = ini.GetValue("Alarms", "TempChangeAlarmAction", string.Empty);
			TempChangeAlarm.ActionParams = ini.GetValue("Alarms", "TempChangeAlarmActionParams", string.Empty);
			TempChangeAlarm.ShowWindow = ini.GetValue("Alarms", "TempChangeAlarmActionWindow", false);
			TempChangeAlarm.BskyFile = ini.GetValue("Alarms", "TempChangeAlarmBlueskyFile", string.Empty);

			LowPressAlarm.Value = ini.GetValue("Alarms", "alarmlowpress", 0.0);
			LowPressAlarm.Enabled = ini.GetValue("Alarms", "LowPressAlarmSet", false);
			LowPressAlarm.Sound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
			LowPressAlarm.SoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
			if (LowPressAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				LowPressAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			LowPressAlarm.Notify = ini.GetValue("Alarms", "LowPressAlarmNotify", false);
			LowPressAlarm.Email = ini.GetValue("Alarms", "LowPressAlarmEmail", false);
			LowPressAlarm.Latch = ini.GetValue("Alarms", "LowPressAlarmLatch", false);
			LowPressAlarm.LatchHours = ini.GetValue("Alarms", "LowPressAlarmLatchHours", 24.0, 0.0);
			LowPressAlarm.Action = ini.GetValue("Alarms", "LowPressAlarmAction", string.Empty);
			LowPressAlarm.ActionParams = ini.GetValue("Alarms", "LowPressAlarmActionParams", string.Empty);
			LowPressAlarm.ShowWindow = ini.GetValue("Alarms", "LowPressAlarmActionWindow", false);
			LowPressAlarm.BskyFile = ini.GetValue("Alarms", "LowPressAlarmBlueskyFile", string.Empty);

			HighPressAlarm.Value = ini.GetValue("Alarms", "alarmhighpress", 0.0, 0.0);
			HighPressAlarm.Enabled = ini.GetValue("Alarms", "HighPressAlarmSet", false);
			HighPressAlarm.Sound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
			HighPressAlarm.SoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
			if (HighPressAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighPressAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighPressAlarm.Notify = ini.GetValue("Alarms", "HighPressAlarmNotify", false);
			HighPressAlarm.Email = ini.GetValue("Alarms", "HighPressAlarmEmail", false);
			HighPressAlarm.Latch = ini.GetValue("Alarms", "HighPressAlarmLatch", false);
			HighPressAlarm.LatchHours = ini.GetValue("Alarms", "HighPressAlarmLatchHours", 24.0, 0.0);
			HighPressAlarm.Action = ini.GetValue("Alarms", "HighPressAlarmAction", string.Empty);
			HighPressAlarm.ActionParams = ini.GetValue("Alarms", "HighPressAlarmActionParams", string.Empty);
			HighPressAlarm.ShowWindow = ini.GetValue("Alarms", "HighPressAlarmAlarmActionWindow", false);
			HighPressAlarm.BskyFile = ini.GetValue("Alarms", "HighPressAlarmBlueskyFile", string.Empty);

			PressChangeAlarm.Value = ini.GetValue("Alarms", "alarmpresschange", 0.0, 0.0);
			PressChangeAlarm.Enabled = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
			PressChangeAlarm.Sound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
			PressChangeAlarm.SoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
			if (PressChangeAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				PressChangeAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			PressChangeAlarm.Notify = ini.GetValue("Alarms", "PressChangeAlarmNotify", false);
			PressChangeAlarm.Email = ini.GetValue("Alarms", "PressChangeAlarmEmail", false);
			PressChangeAlarm.Latch = ini.GetValue("Alarms", "PressChangeAlarmLatch", false);
			PressChangeAlarm.LatchHours = ini.GetValue("Alarms", "PressChangeAlarmLatchHours", 24.0, 0.0);
			PressChangeAlarm.Action = ini.GetValue("Alarms", "PressChangeAlarmAction", string.Empty);
			PressChangeAlarm.ActionParams = ini.GetValue("Alarms", "PressChangeAlarmActionParams", string.Empty);
			PressChangeAlarm.ShowWindow = ini.GetValue("Alarms", "PressChangeAlarmActionWindow", false);
			PressChangeAlarm.BskyFile = ini.GetValue("Alarms", "PressChangeAlarmBlueskyFile", string.Empty);

			HighRainTodayAlarm.Value = ini.GetValue("Alarms", "alarmhighraintoday", 0.0, 0.0);
			HighRainTodayAlarm.Enabled = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
			HighRainTodayAlarm.Sound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
			HighRainTodayAlarm.SoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
			if (HighRainTodayAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighRainTodayAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighRainTodayAlarm.Notify = ini.GetValue("Alarms", "HighRainTodayAlarmNotify", false);
			HighRainTodayAlarm.Email = ini.GetValue("Alarms", "HighRainTodayAlarmEmail", false);
			HighRainTodayAlarm.Latch = ini.GetValue("Alarms", "HighRainTodayAlarmLatch", false);
			HighRainTodayAlarm.LatchHours = ini.GetValue("Alarms", "HighRainTodayAlarmLatchHours", 24.0, 0.0);
			HighRainTodayAlarm.Action = ini.GetValue("Alarms", "HighRainTodayAlarmAction", string.Empty);
			HighRainTodayAlarm.ActionParams = ini.GetValue("Alarms", "HighRainTodayAlarmActionParams", string.Empty);
			HighRainTodayAlarm.ShowWindow = ini.GetValue("Alarms", "HighRainTodayAlarmActionWindow", false);
			HighRainTodayAlarm.BskyFile = ini.GetValue("Alarms", "HighRainTodayAlarmBlueskyFile", string.Empty);

			HighRainRateAlarm.Value = ini.GetValue("Alarms", "alarmhighrainrate", 0.0, 0.0);
			HighRainRateAlarm.Enabled = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
			HighRainRateAlarm.Sound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
			HighRainRateAlarm.SoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
			if (HighRainRateAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighRainRateAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighRainRateAlarm.Notify = ini.GetValue("Alarms", "HighRainRateAlarmNotify", false);
			HighRainRateAlarm.Email = ini.GetValue("Alarms", "HighRainRateAlarmEmail", false);
			HighRainRateAlarm.Latch = ini.GetValue("Alarms", "HighRainRateAlarmLatch", false);
			HighRainRateAlarm.LatchHours = ini.GetValue("Alarms", "HighRainRateAlarmLatchHours", 24.0, 0.0);
			HighRainRateAlarm.Action = ini.GetValue("Alarms", "HighRainRateAlarmAction", string.Empty);
			HighRainRateAlarm.ActionParams = ini.GetValue("Alarms", "HighRainRateAlarmActionParams", string.Empty);
			HighRainRateAlarm.ShowWindow = ini.GetValue("Alarms", "HighRainRateAlarmActionWindow", false);
			HighRainRateAlarm.BskyFile = ini.GetValue("Alarms", "HighRainRateAlarmBlueskyFile", string.Empty);

			IsRainingAlarm.Enabled = ini.GetValue("Alarms", "IsRainingAlarmSet", false);
			IsRainingAlarm.Sound = ini.GetValue("Alarms", "IsRainingAlarmSound", false);
			IsRainingAlarm.SoundFile = ini.GetValue("Alarms", "IsRainingAlarmSoundFile", DefaultSoundFile);
			IsRainingAlarm.Notify = ini.GetValue("Alarms", "IsRainingAlarmNotify", false);
			IsRainingAlarm.Email = ini.GetValue("Alarms", "IsRainingAlarmEmail", false);
			IsRainingAlarm.Latch = ini.GetValue("Alarms", "IsRainingAlarmLatch", false);
			IsRainingAlarm.LatchHours = ini.GetValue("Alarms", "IsRainingAlarmLatchHours", 1.0, 0.0);
			IsRainingAlarm.Action = ini.GetValue("Alarms", "IsRainingAlarmAction", string.Empty);
			IsRainingAlarm.ActionParams = ini.GetValue("Alarms", "IsRainingAlarmActionParams", string.Empty);
			IsRainingAlarm.ShowWindow = ini.GetValue("Alarms", "IsRainingAlarmActionWindow", false);
			IsRainingAlarm.BskyFile = ini.GetValue("Alarms", "IsRainingAlarmBlueskyFile", string.Empty);

			HighGustAlarm.Value = ini.GetValue("Alarms", "alarmhighgust", 0.0, 0.0);
			HighGustAlarm.Enabled = ini.GetValue("Alarms", "HighGustAlarmSet", false);
			HighGustAlarm.Sound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
			HighGustAlarm.SoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
			if (HighGustAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighGustAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighGustAlarm.Notify = ini.GetValue("Alarms", "HighGustAlarmNotify", false);
			HighGustAlarm.Email = ini.GetValue("Alarms", "HighGustAlarmEmail", false);
			HighGustAlarm.Latch = ini.GetValue("Alarms", "HighGustAlarmLatch", false);
			HighGustAlarm.LatchHours = ini.GetValue("Alarms", "HighGustAlarmLatchHours", 24.0, 0.0);
			HighGustAlarm.Action = ini.GetValue("Alarms", "HighGustAlarmAction", string.Empty);
			HighGustAlarm.ActionParams = ini.GetValue("Alarms", "HighGustAlarmActionParams", string.Empty);
			HighGustAlarm.ShowWindow = ini.GetValue("Alarms", "HighGustActionWindow", false);
			HighGustAlarm.BskyFile = ini.GetValue("Alarms", "HighGustAlarmBlueskyFile", string.Empty);

			HighWindAlarm.Value = ini.GetValue("Alarms", "alarmhighwind", 0.0, 0.0);
			HighWindAlarm.Enabled = ini.GetValue("Alarms", "HighWindAlarmSet", false);
			HighWindAlarm.Sound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
			HighWindAlarm.SoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
			if (HighWindAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				HighWindAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			HighWindAlarm.Notify = ini.GetValue("Alarms", "HighWindAlarmNotify", false);
			HighWindAlarm.Email = ini.GetValue("Alarms", "HighWindAlarmEmail", false);
			HighWindAlarm.Latch = ini.GetValue("Alarms", "HighWindAlarmLatch", false);
			HighWindAlarm.LatchHours = ini.GetValue("Alarms", "HighWindAlarmLatchHours", 24.0, 0.0);
			HighWindAlarm.Action = ini.GetValue("Alarms", "HighWindAlarmAction", string.Empty);
			HighWindAlarm.ActionParams = ini.GetValue("Alarms", "HighWindAlarmActionParams", string.Empty);
			HighWindAlarm.ShowWindow = ini.GetValue("Alarms", "HighWindAlarmActionWindow", false);
			HighWindAlarm.BskyFile = ini.GetValue("Alarms", "HighWindAlarmBlueskyFile", string.Empty);

			SensorAlarm.Enabled = ini.GetValue("Alarms", "SensorAlarmSet", true);
			SensorAlarm.Sound = ini.GetValue("Alarms", "SensorAlarmSound", false);
			SensorAlarm.SoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
			if (SensorAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				SensorAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			SensorAlarm.Notify = ini.GetValue("Alarms", "SensorAlarmNotify", true);
			SensorAlarm.Email = ini.GetValue("Alarms", "SensorAlarmEmail", false);
			SensorAlarm.Latch = ini.GetValue("Alarms", "SensorAlarmLatch", true);
			SensorAlarm.LatchHours = ini.GetValue("Alarms", "SensorAlarmLatchHours", 1.0, 0.0);
			SensorAlarm.TriggerThreshold = ini.GetValue("Alarms", "SensorAlarmTriggerCount", 2, 0);
			SensorAlarm.Action = ini.GetValue("Alarms", "SensorAlarmAction", string.Empty);
			SensorAlarm.ActionParams = ini.GetValue("Alarms", "SensorAlarmActionParams", string.Empty);
			SensorAlarm.ShowWindow = ini.GetValue("Alarms", "SensorAlarmActionWindow", false);
			SensorAlarm.BskyFile = ini.GetValue("Alarms", "SensorAlarmBlueskyFile", "none");

			DataStoppedAlarm.Enabled = ini.GetValue("Alarms", "DataStoppedAlarmSet", true);
			DataStoppedAlarm.Sound = ini.GetValue("Alarms", "DataStoppedAlarmSound", false);
			DataStoppedAlarm.SoundFile = ini.GetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
			if (DataStoppedAlarm.SoundFile.Contains(DefaultSoundFileOld))
			{
				SensorAlarm.SoundFile = DefaultSoundFile;
				ini.SetValue("Alarms", "DataStoppedAlarmSoundFile", DefaultSoundFile);
				rewriteRequired = true;
			}
			DataStoppedAlarm.Notify = ini.GetValue("Alarms", "DataStoppedAlarmNotify", true);
			DataStoppedAlarm.Email = ini.GetValue("Alarms", "DataStoppedAlarmEmail", false);
			DataStoppedAlarm.Latch = ini.GetValue("Alarms", "DataStoppedAlarmLatch", true);
			DataStoppedAlarm.LatchHours = ini.GetValue("Alarms", "DataStoppedAlarmLatchHours", 1.0, 0.0);
			DataStoppedAlarm.TriggerThreshold = ini.GetValue("Alarms", "DataStoppedAlarmTriggerCount", 2, 0);
			DataStoppedAlarm.Action = ini.GetValue("Alarms", "DataStoppedAlarmAction", string.Empty);
			DataStoppedAlarm.ActionParams = ini.GetValue("Alarms", "DataStoppedAlarmActionParams", string.Empty);
			DataStoppedAlarm.ShowWindow = ini.GetValue("Alarms", "DataStoppedAlarmActionWindow", false);
			DataStoppedAlarm.BskyFile = ini.GetValue("Alarms", "DataStoppedAlarmBlueskyFile", "none");

			// Alarms below here were created after the change in default sound file, so no check required
			BatteryLowAlarm.Enabled = ini.GetValue("Alarms", "BatteryLowAlarmSet", false);
			BatteryLowAlarm.Sound = ini.GetValue("Alarms", "BatteryLowAlarmSound", false);
			BatteryLowAlarm.SoundFile = ini.GetValue("Alarms", "BatteryLowAlarmSoundFile", DefaultSoundFile);
			BatteryLowAlarm.Notify = ini.GetValue("Alarms", "BatteryLowAlarmNotify", false);
			BatteryLowAlarm.Email = ini.GetValue("Alarms", "BatteryLowAlarmEmail", false);
			BatteryLowAlarm.Latch = ini.GetValue("Alarms", "BatteryLowAlarmLatch", false);
			BatteryLowAlarm.LatchHours = ini.GetValue("Alarms", "BatteryLowAlarmLatchHours", 24.0, 0.0);
			BatteryLowAlarm.TriggerThreshold = ini.GetValue("Alarms", "BatteryLowAlarmTriggerCount", 1, 0);
			BatteryLowAlarm.Action = ini.GetValue("Alarms", "BatteryLowAlarmAction", string.Empty);
			BatteryLowAlarm.ActionParams = ini.GetValue("Alarms", "BatteryLowAlarmActionParams", string.Empty);
			BatteryLowAlarm.ShowWindow = ini.GetValue("Alarms", "BatteryLowAlarmActionWindow", false);
			BatteryLowAlarm.BskyFile = ini.GetValue("Alarms", "BatteryLowAlarmBlueskyFile", "none");

			SpikeAlarm.Enabled = ini.GetValue("Alarms", "DataSpikeAlarmSet", false);
			SpikeAlarm.Sound = ini.GetValue("Alarms", "DataSpikeAlarmSound", false);
			SpikeAlarm.SoundFile = ini.GetValue("Alarms", "DataSpikeAlarmSoundFile", DefaultSoundFile);
			SpikeAlarm.Notify = ini.GetValue("Alarms", "DataSpikeAlarmNotify", true);
			SpikeAlarm.Email = ini.GetValue("Alarms", "DataSpikeAlarmEmail", true);
			SpikeAlarm.Latch = ini.GetValue("Alarms", "DataSpikeAlarmLatch", true);
			SpikeAlarm.LatchHours = ini.GetValue("Alarms", "DataSpikeAlarmLatchHours", 24.0, 0.0);
			SpikeAlarm.TriggerThreshold = ini.GetValue("Alarms", "DataSpikeAlarmTriggerCount", 1, 0);
			SpikeAlarm.Action = ini.GetValue("Alarms", "DataSpikeAlarmAction", string.Empty);
			SpikeAlarm.ActionParams = ini.GetValue("Alarms", "DataSpikeAlarmActionParams", string.Empty);
			SpikeAlarm.ShowWindow = ini.GetValue("Alarms", "DataSpikeAlarmActionWindow", false);
			SpikeAlarm.BskyFile = ini.GetValue("Alarms", "DataSpikeAlarmBlueskyFile", "none");

			UpgradeAlarm.Enabled = ini.GetValue("Alarms", "UpgradeAlarmSet", true);
			UpgradeAlarm.Sound = ini.GetValue("Alarms", "UpgradeAlarmSound", false);
			UpgradeAlarm.SoundFile = ini.GetValue("Alarms", "UpgradeAlarmSoundFile", DefaultSoundFile);
			UpgradeAlarm.Notify = ini.GetValue("Alarms", "UpgradeAlarmNotify", true);
			UpgradeAlarm.Email = ini.GetValue("Alarms", "UpgradeAlarmEmail", false);
			UpgradeAlarm.Latch = ini.GetValue("Alarms", "UpgradeAlarmLatch", false);
			UpgradeAlarm.LatchHours = ini.GetValue("Alarms", "UpgradeAlarmLatchHours", 24.0, 0.0);
			UpgradeAlarm.Action = ini.GetValue("Alarms", "UpgradeAlarmAction", string.Empty);
			UpgradeAlarm.ActionParams = ini.GetValue("Alarms", "UpgradeAlarmActionParams", string.Empty);
			UpgradeAlarm.ShowWindow = ini.GetValue("Alarms", "UpgradeAlarmActionWindow", false);
			UpgradeAlarm.BskyFile = ini.GetValue("Alarms", "UpgradeAlarmBlueskyFile", "none");

			FirmwareAlarm.Enabled = ini.GetValue("Alarms", "FirmwareAlarmSet", true);
			FirmwareAlarm.Sound = ini.GetValue("Alarms", "FirmwareAlarmSound", false);
			FirmwareAlarm.SoundFile = ini.GetValue("Alarms", "FirmwareAlarmSoundFile", DefaultSoundFile);
			FirmwareAlarm.Notify = ini.GetValue("Alarms", "FirmwareAlarmNotify", true);
			FirmwareAlarm.Email = ini.GetValue("Alarms", "FirmwareAlarmEmail", false);
			FirmwareAlarm.Latch = ini.GetValue("Alarms", "FirmwareAlarmLatch", false);
			FirmwareAlarm.LatchHours = ini.GetValue("Alarms", "FirmwareAlarmLatchHours", 24.0, 0.0);
			FirmwareAlarm.Action = ini.GetValue("Alarms", "FirmwareAlarmAction", string.Empty);
			FirmwareAlarm.ActionParams = ini.GetValue("Alarms", "FirmwareAlarmActionParams", string.Empty);
			FirmwareAlarm.ShowWindow = ini.GetValue("Alarms", "FirmwareAlarmActionWindow", false);
			FirmwareAlarm.BskyFile = ini.GetValue("Alarms", "FirmwareAlarmBlueskyFile", "none");

			ThirdPartyAlarm.Enabled = ini.GetValue("Alarms", "HttpUploadAlarmSet", false);
			ThirdPartyAlarm.Sound = ini.GetValue("Alarms", "HttpUploadAlarmSound", false);
			ThirdPartyAlarm.SoundFile = ini.GetValue("Alarms", "HttpUploadAlarmSoundFile", DefaultSoundFile);
			ThirdPartyAlarm.Notify = ini.GetValue("Alarms", "HttpUploadAlarmNotify", false);
			ThirdPartyAlarm.Email = ini.GetValue("Alarms", "HttpUploadAlarmEmail", false);
			ThirdPartyAlarm.Latch = ini.GetValue("Alarms", "HttpUploadAlarmLatch", false);
			ThirdPartyAlarm.LatchHours = ini.GetValue("Alarms", "HttpUploadAlarmLatchHours", 24.0, 0.0);
			ThirdPartyAlarm.TriggerThreshold = ini.GetValue("Alarms", "HttpUploadAlarmTriggerCount", 1, 0);
			ThirdPartyAlarm.Action = ini.GetValue("Alarms", "HttpUploadAlarmAction", string.Empty);
			ThirdPartyAlarm.ActionParams = ini.GetValue("Alarms", "HttpUploadAlarmActionParams", string.Empty);
			ThirdPartyAlarm.ShowWindow = ini.GetValue("Alarms", "HttpUploadAlarmActionWindow", false);
			ThirdPartyAlarm.BskyFile = ini.GetValue("Alarms", "HttpUploadAlarmBlueskyFile", "none");

			MySqlUploadAlarm.Enabled = ini.GetValue("Alarms", "MySqlUploadAlarmSet", false);
			MySqlUploadAlarm.Sound = ini.GetValue("Alarms", "MySqlUploadAlarmSound", false);
			MySqlUploadAlarm.SoundFile = ini.GetValue("Alarms", "MySqlUploadAlarmSoundFile", DefaultSoundFile);
			MySqlUploadAlarm.Notify = ini.GetValue("Alarms", "MySqlUploadAlarmNotify", false);
			MySqlUploadAlarm.Email = ini.GetValue("Alarms", "MySqlUploadAlarmEmail", false);
			MySqlUploadAlarm.Latch = ini.GetValue("Alarms", "MySqlUploadAlarmLatch", false);
			MySqlUploadAlarm.LatchHours = ini.GetValue("Alarms", "MySqlUploadAlarmLatchHours", 24.0, 0.0);
			MySqlUploadAlarm.TriggerThreshold = ini.GetValue("Alarms", "MySqlUploadAlarmTriggerCount", 1, 0);
			MySqlUploadAlarm.Action = ini.GetValue("Alarms", "MySqlUploadAlarmAction", string.Empty);
			MySqlUploadAlarm.ActionParams = ini.GetValue("Alarms", "MySqlUploadAlarmActionParams", string.Empty);
			MySqlUploadAlarm.BskyFile = ini.GetValue("Alarms", "MySqlUploadAlarmBlueskyFile", "none");

			NewRecordAlarm.Enabled = ini.GetValue("Alarms", "NewRecordAlarmSet", true);
			NewRecordAlarm.Sound = ini.GetValue("Alarms", "NewRecordAlarmSound", false);
			NewRecordAlarm.SoundFile = ini.GetValue("Alarms", "NewRecordAlarmSoundFile", DefaultSoundFile);
			NewRecordAlarm.Notify = ini.GetValue("Alarms", "NewRecordAlarmNotify", false);
			NewRecordAlarm.Email = ini.GetValue("Alarms", "NewRecordAlarmEmail", false);
			NewRecordAlarm.Latch = ini.GetValue("Alarms", "NewRecordAlarmLatch", false);
			NewRecordAlarm.LatchHours = ini.GetValue("Alarms", "NewRecordAlarmLatchHours", 24.0, 0.0);
			NewRecordAlarm.Action = ini.GetValue("Alarms", "NewRecordAlarmAction", string.Empty);
			NewRecordAlarm.ActionParams = ini.GetValue("Alarms", "NewRecordAlarmActionParams", string.Empty);
			NewRecordAlarm.ShowWindow = ini.GetValue("Alarms", "NewRecordAlarmActionWindow", false);
			NewRecordAlarm.BskyFile = ini.GetValue("Alarms", "NewRecordAlarmBlueskyFile", string.Empty);

			FtpAlarm.Enabled = ini.GetValue("Alarms", "FtpAlarmSet", false);
			FtpAlarm.Sound = ini.GetValue("Alarms", "FtpAlarmSound", false);
			FtpAlarm.SoundFile = ini.GetValue("Alarms", "FtpAlarmSoundFile", DefaultSoundFile);
			FtpAlarm.Notify = ini.GetValue("Alarms", "FtpAlarmNotify", false);
			FtpAlarm.Email = ini.GetValue("Alarms", "FtpAlarmEmail", false);
			FtpAlarm.Latch = ini.GetValue("Alarms", "FtpAlarmLatch", false);
			FtpAlarm.LatchHours = ini.GetValue("Alarms", "FtpAlarmLatchHours", 24.0, 0.0);
			FtpAlarm.Action = ini.GetValue("Alarms", "FtpAlarmAction", string.Empty);
			FtpAlarm.ActionParams = ini.GetValue("Alarms", "FtpAlarmActionParams", string.Empty);
			FtpAlarm.ShowWindow = ini.GetValue("Alarms", "FtpAlarmActionWindow", false);
			FtpAlarm.BskyFile = ini.GetValue("Alarms", "FtpAlarmBlueskyFile", "none");

			ErrorAlarm.Enabled = ini.GetValue("Alarms", "ErrorAlarmSet", false);
			ErrorAlarm.Sound = ini.GetValue("Alarms", "ErrorAlarmSound", false);
			ErrorAlarm.SoundFile = ini.GetValue("Alarms", "ErrorAlarmSoundFile", DefaultSoundFile);
			ErrorAlarm.Notify = ini.GetValue("Alarms", "ErrorAlarmNotify", false);
			ErrorAlarm.Email = ini.GetValue("Alarms", "ErrorAlarmEmail", false);
			ErrorAlarm.Action = ini.GetValue("Alarms", "ErrorAlarmAction", string.Empty);
			ErrorAlarm.ActionParams = ini.GetValue("Alarms", "ErrorAlarmActionParams", string.Empty);
			ErrorAlarm.ShowWindow = ini.GetValue("Alarms", "ErrorAlarmActionWindow", false);
			ErrorAlarm.BskyFile = ini.GetValue("Alarms", "ErrorAlarmBlueskyFile", "none");

			AlarmFromEmail = ini.GetValue("Alarms", "FromEmail", string.Empty);
			AlarmDestEmail = ini.GetValue("Alarms", "DestEmail", string.Empty).Split(';');
			AlarmEmailHtml = ini.GetValue("Alarms", "UseHTML", false);
			AlarmEmailUseBcc = ini.GetValue("Alarms", "UseBCC", false);
			#endregion

			#region User Alarms
			// User Alarm Settings
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("UserAlarms", "AlarmName" + i))
				{
					var name = ini.GetValue("UserAlarms", "AlarmName" + i, string.Empty);
					var tag = ini.GetValue("UserAlarms", "AlarmTag" + i, string.Empty);
					var type = ini.GetValue("UserAlarms", "AlarmType" + i, string.Empty);
					var value = ini.GetValue("UserAlarms", "AlarmValue" + i, (decimal) 0.0);
					var enabled = ini.GetValue("UserAlarms", "AlarmEnabled" + i, false);
					var email = ini.GetValue("UserAlarms", "AlarmEmail" + i, false);
					var emailMsg = ini.GetValue("UserAlarms", "AlarmEmailMsg" + i, string.Empty);
					var bsky = ini.GetValue("UserAlarms", "AlarmBlueskyFile" + i, string.Empty);
					var latch = ini.GetValue("UserAlarms", "AlarmLatch" + i, false);
					var latchHours = ini.GetValue("UserAlarms", "AlarmLatchHours" + i, 24.0, 0.0);
					var action = ini.GetValue("UserAlarms", "AlarmAction" + i, string.Empty);
					var actionParams = ini.GetValue("UserAlarms", "AlarmActionParams" + i, string.Empty);
					var showWindow = ini.GetValue("UserAlarms", "AlarmActionWindow" + i, false);

					if (name != string.Empty && tag != string.Empty && type != string.Empty)
					{
						try
						{
							UserAlarms.Add(new AlarmUser((AlarmIds) (101 + i), name, type, tag, this)
							{
								Value = value,
								Enabled = enabled,
								Email = email,
								EmailMsg = emailMsg,
								BskyFile = bsky,
								Latch = latch,
								LatchHours = latchHours,
								Action = action,
								ActionParams = actionParams,
								ShowWindow = showWindow
							});
						}
						catch (Exception ex)
						{
							LogErrorMessage($"Error loading user alarm {ini.GetValue("UserAlarms", "AlarmName" + i, string.Empty)}: {ex.Message}");
						}
					}
				}
			}
			#endregion

			#region Calibration Settings
			Calib.Press.Offset = ini.GetValue("Offsets", "PressOffset", 0.0);
			Calib.PressStn.Offset = ini.GetValue("Offsets", "PressStnOffset", 0, 0, 1);
			Calib.Temp.Offset = ini.GetValue("Offsets", "TempOffset", 0.0);
			Calib.Hum.Offset = ini.GetValue("Offsets", "HumOffset", 0, 0, 1);
			Calib.WindDir.Offset = ini.GetValue("Offsets", "WindDirOffset", 0, 0, 1);
			Calib.Solar.Offset = ini.GetValue("Offsets", "SolarOffset", 0.0);
			Calib.UV.Offset = ini.GetValue("Offsets", "UVOffset", 0.0);
			Calib.WetBulb.Offset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);
			Calib.InTemp.Offset = ini.GetValue("Offsets", "InTempOffset", 0.0);
			Calib.InHum.Offset = ini.GetValue("Offsets", "InHumOffset", 0, 0, 1);

			Calib.Press.Mult = ini.GetValue("Offsets", "PressMult", 1.0);
			Calib.PressStn.Mult = ini.GetValue("Offsets", "PressStnMult", 1.0);
			Calib.WindSpeed.Mult = ini.GetValue("Offsets", "WindSpeedMult", 1.0);
			Calib.WindGust.Mult = ini.GetValue("Offsets", "WindGustMult", 1.0);
			Calib.Temp.Mult = ini.GetValue("Offsets", "TempMult", 1.0);
			Calib.Hum.Mult = ini.GetValue("Offsets", "HumMult", 1.0);
			Calib.Rain.Mult = ini.GetValue("Offsets", "RainMult", 1.0);
			Calib.Solar.Mult = ini.GetValue("Offsets", "SolarMult", 1.0);
			Calib.UV.Mult = ini.GetValue("Offsets", "UVMult", 1.0);
			Calib.WetBulb.Mult = ini.GetValue("Offsets", "WetBulbMult", 1.0);
			Calib.InTemp.Mult = ini.GetValue("Offsets", "InTempMult", 1.0);
			Calib.InHum.Mult = ini.GetValue("Offsets", "InHumMult", 1.0);

			Calib.Press.Mult2 = ini.GetValue("Offsets", "PressMult2", 0.0);
			Calib.PressStn.Mult2 = ini.GetValue("Offsets", "PressStnMult2", 0.0);
			Calib.WindSpeed.Mult2 = ini.GetValue("Offsets", "WindSpeedMult2", 0.0);
			Calib.WindGust.Mult2 = ini.GetValue("Offsets", "WindGustMult2", 0.0);
			Calib.Temp.Mult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
			Calib.Hum.Mult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
			Calib.InTemp.Mult2 = ini.GetValue("Offsets", "InTempMult2", 0.0);
			Calib.InHum.Mult2 = ini.GetValue("Offsets", "InHumMult2", 0.0);
			Calib.Solar.Mult2 = ini.GetValue("Offsets", "SolarMult2", 0.0);
			Calib.UV.Mult2 = ini.GetValue("Offsets", "UVMult2", 0.0);

			Limit.TempHigh = ConvertUnits.TempCToUser(ini.GetValue("Limits", "TempHighC", 60.0));
			Limit.TempLow = ConvertUnits.TempCToUser(ini.GetValue("Limits", "TempLowC", -60.0));
			Limit.DewHigh = ConvertUnits.TempCToUser(ini.GetValue("Limits", "DewHighC", 40.0));
			Limit.PressHigh = ConvertUnits.PressMBToUser(ini.GetValue("Limits", "PressHighMB", 1090.0));
			Limit.PressLow = ConvertUnits.PressMBToUser(ini.GetValue("Limits", "PressLowMB", 870.0));
			Limit.WindHigh = ConvertUnits.WindMSToUser(ini.GetValue("Limits", "WindHighMS", 90.0));
			Limit.StationPressHigh = ConvertUnits.PressMBToUser(MeteoLib.SeaLevelToStation(ConvertUnits.UserPressToHpa(Limit.PressHigh), ConvertUnits.AltitudeM(Altitude)));
			Limit.StationPressLow = ConvertUnits.PressMBToUser(MeteoLib.SeaLevelToStation(ConvertUnits.UserPressToHpa(Limit.PressLow), ConvertUnits.AltitudeM(Altitude)));
			#endregion

			xapEnabled = ini.GetValue("xAP", "Enabled", false);
			xapUID = ini.GetValue("xAP", "UID", "4375");
			xapPort = ini.GetValue("xAP", "Port", 3639, 1, 65535);

			#region Solar Settings
			SolarOptions.SunThreshold = ini.GetValue("Solar", "SunThreshold", 75, 1, 200);
			SolarOptions.SolarMinimum = ini.GetValue("Solar", "SolarMinimum", 30, 0);
			SolarOptions.LuxToWM2 = ini.GetValue("Solar", "LuxToWM2", 0.0079);
			SolarOptions.UseBlakeLarsen = ini.GetValue("Solar", "UseBlakeLarsen", false);
			SolarOptions.SolarCalc = ini.GetValue("Solar", "SolarCalc", 0, 0, 1);

			// Migrate old single solar factors to the new dual scheme
			if (ini.ValueExists("Solar", "RStransfactor"))
			{
				SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactor", 0.8, 0.1);
				SolarOptions.RStransfactorDec = SolarOptions.RStransfactorJun;
				ini.DeleteValue("Solar", "RStransfactor");
				ini.SetValue("Solar", "RStransfactorJun", SolarOptions.RStransfactorJun);
				ini.SetValue("Solar", "RStransfactorDec", SolarOptions.RStransfactorDec);
				rewriteRequired = true;
			}
			else
			{
				if (ini.ValueExists("Solar", "RStransfactorJul"))
				{
					SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactorJul", 0.8, 0.1);
					ini.DeleteValue("Solar", "RStransfactorJul");
					ini.SetValue("Solar", "RStransfactorJun", SolarOptions.RStransfactorJun);
					rewriteRequired = true;
				}
				else
				{
					SolarOptions.RStransfactorJun = ini.GetValue("Solar", "RStransfactorJun", 0.8, 0.1);
				}
				SolarOptions.RStransfactorDec = ini.GetValue("Solar", "RStransfactorDec", 0.8, 0.1);
			}
			if (ini.ValueExists("Solar", "BrasTurbidity"))
			{
				SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidity", 2.0);
				SolarOptions.BrasTurbidityDec = SolarOptions.BrasTurbidityJun;
				ini.DeleteValue("Solar", "BrasTurbidity");
				ini.SetValue("Solar", "BrasTurbidityJun", SolarOptions.BrasTurbidityJun);
				ini.SetValue("Solar", "BrasTurbidityDec", SolarOptions.BrasTurbidityDec);
				rewriteRequired = true;
			}
			else
			{
				if (ini.ValueExists("Solar", "BrasTurbidityJul"))
				{
					SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidityJul", 2.0);
					ini.DeleteValue("Solar", "BrasTurbidityJul");
					ini.SetValue("Solar", "BrasTurbidityJun", SolarOptions.BrasTurbidityJun);
					rewriteRequired = true;
				}
				else
				{
					SolarOptions.BrasTurbidityJun = ini.GetValue("Solar", "BrasTurbidityJun", 2.0);
				}
				SolarOptions.BrasTurbidityDec = ini.GetValue("Solar", "BrasTurbidityDec", 2.0);
			}
			#endregion

			#region NOAA Settings
			NOAAconf.Name = ini.GetValue("NOAA", "Name", " ");
			NOAAconf.City = ini.GetValue("NOAA", "City", " ");
			NOAAconf.State = ini.GetValue("NOAA", "State", " ");
			NOAAconf.Use12hour = ini.GetValue("NOAA", "12hourformat", false);
			NOAAconf.HeatThreshold = ini.GetValue("NOAA", "HeatingThreshold", Units.Temp == 0 ? 18.3 : 65);
			if (NOAAconf.HeatThreshold < -99 || NOAAconf.HeatThreshold > 150)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.HeatThreshold, resetting it");
				NOAAconf.HeatThreshold = Units.Temp == 0 ? 18.3 : 65;
				ini.SetValue("NOAA", "HeatingThreshold", NOAAconf.HeatThreshold);
				rewriteRequired = true;
			}
			NOAAconf.CoolThreshold = ini.GetValue("NOAA", "CoolingThreshold", Units.Temp == 0 ? 18.3 : 65);
			if (NOAAconf.CoolThreshold < -99 || NOAAconf.CoolThreshold > 150)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.CoolThreshold, resetting it");
				NOAAconf.CoolThreshold = Units.Temp == 0 ? 18.3 : 65;
				ini.SetValue("NOAA", "CoolingThreshold", NOAAconf.CoolThreshold);
				rewriteRequired = true;
			}
			NOAAconf.MaxTempComp1 = ini.GetValue("NOAA", "MaxTempComp1", Units.Temp == 0 ? 27 : 80);
			if (NOAAconf.MaxTempComp1 < -99 || NOAAconf.MaxTempComp1 > 150)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MaxTempComp1, resetting it");
				NOAAconf.MaxTempComp1 = Units.Temp == 0 ? 27 : 80;
				ini.SetValue("NOAA", "MaxTempComp1", NOAAconf.MaxTempComp1);
				rewriteRequired = true;
			}
			NOAAconf.MaxTempComp2 = ini.GetValue("NOAA", "MaxTempComp2", Units.Temp == 0 ? 0 : 32);
			if (NOAAconf.MaxTempComp2 < -99 || NOAAconf.MaxTempComp2 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MaxTempComp2, resetting it");
				NOAAconf.MaxTempComp2 = Units.Temp == 0 ? 0 : 32;
				ini.SetValue("NOAA", "MaxTempComp2", NOAAconf.MaxTempComp2);
				rewriteRequired = true;
			}
			NOAAconf.MinTempComp1 = ini.GetValue("NOAA", "MinTempComp1", Units.Temp == 0 ? 0 : 32);
			if (NOAAconf.MinTempComp1 < -99 || NOAAconf.MinTempComp1 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MinTempComp1, resetting it");
				NOAAconf.MinTempComp1 = Units.Temp == 0 ? 0 : 32;
				ini.SetValue("NOAA", "MinTempComp1", NOAAconf.MinTempComp1);
				rewriteRequired = true;
			}
			NOAAconf.MinTempComp2 = ini.GetValue("NOAA", "MinTempComp2", Units.Temp == 0 ? -18 : 0);
			if (NOAAconf.MinTempComp2 < -99 || NOAAconf.MinTempComp2 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.MinTempComp2, resetting it");
				NOAAconf.MinTempComp2 = Units.Temp == 0 ? -18 : 0;
				ini.SetValue("NOAA", "MinTempComp2", NOAAconf.MinTempComp2);
				rewriteRequired = true;
			}
			NOAAconf.RainComp1 = ini.GetValue("NOAA", "RainComp1", Units.Rain == 0 ? 0.2 : 0.01);
			if (NOAAconf.RainComp1 < 0 || NOAAconf.RainComp1 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.RainComp1, resetting it");
				NOAAconf.RainComp1 = Units.Rain == 0 ? 0.2 : 0.01;
				ini.SetValue("NOAA", "RainComp1", NOAAconf.RainComp1);
				rewriteRequired = true;
			}
			NOAAconf.RainComp2 = ini.GetValue("NOAA", "RainComp2", Units.Rain == 0 ? 2 : 0.1);
			if (NOAAconf.RainComp2 < 0 || NOAAconf.RainComp2 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.RainComp2, resetting it");
				NOAAconf.RainComp2 = Units.Rain == 0 ? 2 : 0.1;
				ini.SetValue("NOAA", "RainComp2", NOAAconf.RainComp2);
				rewriteRequired = true;
			}
			NOAAconf.RainComp3 = ini.GetValue("NOAA", "RainComp3", Units.Rain == 0 ? 20 : 1);
			if (NOAAconf.RainComp3 < 0 || NOAAconf.RainComp3 > 99)
			{
				LogMessage("Cumulus.ini: Invalid NOAAconf.RainComp3, resetting it");
				NOAAconf.RainComp3 = Units.Rain == 0 ? 20 : 1;
				ini.SetValue("NOAA", "RainComp3", NOAAconf.RainComp3);
				rewriteRequired = true;
			}

			NOAAconf.Create = ini.GetValue("NOAA", "AutoSave", false);
			NOAAconf.AutoFtp = ini.GetValue("NOAA", "AutoFTP", false);
			NOAAconf.FtpFolder = ini.GetValue("NOAA", "FTPDirectory", string.Empty);
			NOAAconf.AutoCopy = ini.GetValue("NOAA", "AutoCopy", false);
			NOAAconf.CopyFolder = ini.GetValue("NOAA", "CopyDirectory", string.Empty);
			NOAAconf.MonthFile = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
			// Check for Cumulus 1 default format - and update
			if (NOAAconf.MonthFile == "'NOAAMO'mmyy'.txt'" || NOAAconf.MonthFile == "\"NOAAMO\"mmyy\".txt\"")
			{
				LogMessage("Cumulus.ini: Updating old Cumulus 1 NOAA monthly file name");
				NOAAconf.MonthFile = "'NOAAMO'MMyy'.txt'";
				ini.SetValue("NOAA", "MonthFileFormat", NOAAconf.MonthFile);
				rewriteRequired = true;
			}
			NOAAconf.YearFile = ini.GetValue("NOAA", "YearFileFormat", "'NOAAYR'yyyy'.txt'");
			NOAAconf.UseUtf8 = ini.GetValue("NOAA", "NOAAUseUTF8", true);
			NOAAconf.OutputText = ini.GetValue("NOAA", "NOAAUOutputText", true);
			NOAAconf.UseDotDecimal = ini.GetValue("NOAA", "UseDotDecimal", false);
			NOAAconf.UseNoaaHeatCoolDays = ini.GetValue("NOAA", "UseNoaaHeatCoolDays", false);
			NOAAconf.UseMinMaxAvg = ini.GetValue("NOAA", "UseMinMaxAvg", false);

			NOAAconf.TempNorms[1] = ini.GetValue("NOAA", "NOAATempNormJan", -1000.0);
			NOAAconf.TempNorms[2] = ini.GetValue("NOAA", "NOAATempNormFeb", -1000.0);
			NOAAconf.TempNorms[3] = ini.GetValue("NOAA", "NOAATempNormMar", -1000.0);
			NOAAconf.TempNorms[4] = ini.GetValue("NOAA", "NOAATempNormApr", -1000.0);
			NOAAconf.TempNorms[5] = ini.GetValue("NOAA", "NOAATempNormMay", -1000.0);
			NOAAconf.TempNorms[6] = ini.GetValue("NOAA", "NOAATempNormJun", -1000.0);
			NOAAconf.TempNorms[7] = ini.GetValue("NOAA", "NOAATempNormJul", -1000.0);
			NOAAconf.TempNorms[8] = ini.GetValue("NOAA", "NOAATempNormAug", -1000.0);
			NOAAconf.TempNorms[9] = ini.GetValue("NOAA", "NOAATempNormSep", -1000.0);
			NOAAconf.TempNorms[10] = ini.GetValue("NOAA", "NOAATempNormOct", -1000.0);
			NOAAconf.TempNorms[11] = ini.GetValue("NOAA", "NOAATempNormNov", -1000.0);
			NOAAconf.TempNorms[12] = ini.GetValue("NOAA", "NOAATempNormDec", -1000.0);

			NOAAconf.RainNorms[1] = ini.GetValue("NOAA", "NOAARainNormJan", -1000.0);
			NOAAconf.RainNorms[2] = ini.GetValue("NOAA", "NOAARainNormFeb", -1000.0);
			NOAAconf.RainNorms[3] = ini.GetValue("NOAA", "NOAARainNormMar", -1000.0);
			NOAAconf.RainNorms[4] = ini.GetValue("NOAA", "NOAARainNormApr", -1000.0);
			NOAAconf.RainNorms[5] = ini.GetValue("NOAA", "NOAARainNormMay", -1000.0);
			NOAAconf.RainNorms[6] = ini.GetValue("NOAA", "NOAARainNormJun", -1000.0);
			NOAAconf.RainNorms[7] = ini.GetValue("NOAA", "NOAARainNormJul", -1000.0);
			NOAAconf.RainNorms[8] = ini.GetValue("NOAA", "NOAARainNormAug", -1000.0);
			NOAAconf.RainNorms[9] = ini.GetValue("NOAA", "NOAARainNormSep", -1000.0);
			NOAAconf.RainNorms[10] = ini.GetValue("NOAA", "NOAARainNormOct", -1000.0);
			NOAAconf.RainNorms[11] = ini.GetValue("NOAA", "NOAARainNormNov", -1000.0);
			NOAAconf.RainNorms[12] = ini.GetValue("NOAA", "NOAARainNormDec", -1000.0);
			#endregion

			#region Proxy Settings
			HTTPProxyName = ini.GetValue("Proxies", "HTTPProxyName", string.Empty);
			HTTPProxyPort = ini.GetValue("Proxies", "HTTPProxyPort", 0, 0, 1);
			HTTPProxyUser = ini.GetValue("Proxies", "HTTPProxyUser", string.Empty);
			HTTPProxyPassword = ini.GetValue("Proxies", "HTTPProxyPassword", string.Empty);
			#endregion

			NumWindRosePoints = ini.GetValue("Display", "NumWindRosePoints", 16, 4, 32);
			WindRoseAngle = 360.0 / NumWindRosePoints;
			DisplayOptions.UseApparent = ini.GetValue("Display", "UseApparent", false);
			DisplayOptions.ShowSolar = ini.GetValue("Display", "DisplaySolarData", false);
			DisplayOptions.ShowUV = ini.GetValue("Display", "DisplayUvData", false);
			DisplayOptions.ShowSnow = ini.GetValue("Display", "DisplaySnowData", false);

			#region MySQL Settings
			// MySQL - common
			MySqlFuncs.MySqlConnSettings.Server = ini.GetValue("MySQL", "Host", "127.0.0.1");
			MySqlFuncs.MySqlConnSettings.Port = (uint) ini.GetValue("MySQL", "Port", 3306, 1, 65535);
			MySqlFuncs.MySqlConnSettings.UserID = ini.GetValue("MySQL", "User", string.Empty);
			MySqlFuncs.MySqlConnSettings.Password = ini.GetValue("MySQL", "Pass", string.Empty);
			MySqlFuncs.MySqlConnSettings.Database = ini.GetValue("MySQL", "Database", "");
			MySqlFuncs.MySqlConnSettings.SslMode = (MySqlSslMode) ini.GetValue("MySQL", "SSLmode", (int) MySqlSslMode.Preferred);
			MySqlFuncs.MySqlConnSettings.TlsVersion = ini.GetValue("MySQL", "TLSversions", "TLS 1.2, TLS 1.3");

			MySqlFuncs.MySqlSettings.UpdateOnEdit = ini.GetValue("MySQL", "UpdateOnEdit", true);
			MySqlFuncs.MySqlSettings.BufferOnfailure = ini.GetValue("MySQL", "BufferOnFailure", false);

			// MySQL - monthly log file
			MySqlFuncs.MySqlSettings.Monthly.Enabled = ini.GetValue("MySQL", "MonthlyMySqlEnabled", false);
			MySqlFuncs.MySqlSettings.Monthly.TableName = ini.GetValue("MySQL", "MonthlyTable", "Monthly");
			// MySQL - real-time
			MySqlFuncs.MySqlSettings.Realtime.Enabled = ini.GetValue("MySQL", "RealtimeMySqlEnabled", false);
			MySqlFuncs.MySqlSettings.Realtime.TableName = ini.GetValue("MySQL", "RealtimeTable", "Realtime");
			MySqlFuncs.MySqlSettings.RealtimeRetention = ini.GetValue("MySQL", "RealtimeRetention", string.Empty);
			MySqlFuncs.MySqlSettings.RealtimeLimit1Minute = ini.GetValue("MySQL", "RealtimeMySql1MinLimit", false) && RealtimeInterval < 60000; // do not enable if real time interval is greater than 1 minute
																																				// MySQL - dayfile
			MySqlFuncs.MySqlSettings.Dayfile.Enabled = ini.GetValue("MySQL", "DayfileMySqlEnabled", false);
			MySqlFuncs.MySqlSettings.Dayfile.TableName = ini.GetValue("MySQL", "DayfileTable", "Dayfile");

			// MySQL - custom seconds
			MySqlFuncs.MySqlSettings.CustomSecs.Commands[0] = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlSecondsCommandString" + i))
					MySqlFuncs.MySqlSettings.CustomSecs.Commands[i] = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString" + i, string.Empty);
			}

			MySqlFuncs.MySqlSettings.CustomSecs.Enabled = ini.GetValue("MySQL", "CustomMySqlSecondsEnabled", false);
			MySqlFuncs.MySqlSettings.CustomSecs.Interval = ini.GetValue("MySQL", "CustomMySqlSecondsInterval", 10);
			if (MySqlFuncs.MySqlSettings.CustomSecs.Interval < 1) { MySqlFuncs.MySqlSettings.CustomSecs.Interval = 1; }

			// MySQL - custom minutes
			MySqlFuncs.MySqlSettings.CustomMins.Enabled = ini.GetValue("MySQL", "CustomMySqlMinutesEnabled", false);

			MySqlFuncs.MySqlSettings.CustomMins.Commands[0] = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString", string.Empty);
			MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0] = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIndex", 6);
			MySqlFuncs.MySqlSettings.CustomMins.CatchUp[0] = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalCatchUp", false);
			if (MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0] < 0 && MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0] >= FactorsOf60.Length)
			{
				MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0] = 6;
			}
			MySqlFuncs.MySqlSettings.CustomMins.Intervals[0] = FactorsOf60[MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0]];
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlMinutesCommandString" + i))
				{
					MySqlFuncs.MySqlSettings.CustomMins.Commands[i] = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString" + i, string.Empty);
					MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIdx" + i, MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0]);
					MySqlFuncs.MySqlSettings.CustomMins.CatchUp[i] = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalCatchUp" + i, false);

					if (MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] < 0 && MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] > FactorsOf60.Length)
					{
						MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i] = 6;
					}
					MySqlFuncs.MySqlSettings.CustomMins.Intervals[i] = FactorsOf60[MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i]];
				}
			}


			// MySql - Timed
			MySqlFuncs.MySqlSettings.CustomTimed.Enabled = ini.GetValue("MySQL", "CustomMySqlTimedEnabled", false);
			for (var i = 0; i < 10; i++)
			{
				MySqlFuncs.MySqlSettings.CustomTimed.Commands[i] = ini.GetValue("MySQL", "CustomMySqlTimedCommandString" + i, string.Empty);
				MySqlFuncs.MySqlSettings.CustomTimed.SetStartTime(i, ini.GetValue("MySQL", "CustomMySqlTimedStartTime" + i, "00:00"));
				MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] = ini.GetValue("MySQL", "CustomMySqlTimedInterval" + i, 1440, 1);

				if (!string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]) && MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i] < 1440)
					MySqlFuncs.MySqlSettings.CustomTimed.SetNextInterval(i, DateTime.Now);
			}

			// MySQL - custom roll-over
			MySqlFuncs.MySqlSettings.CustomRollover.Enabled = ini.GetValue("MySQL", "CustomMySqlRolloverEnabled", false);
			MySqlFuncs.MySqlSettings.CustomRollover.Commands[0] = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString", string.Empty);
			MySqlFuncs.MySqlSettings.CustomRollover.CatchUp[0] = ini.GetValue("MySQL", "CustomMySqlRolloverCatchUp", true);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlRolloverCommandString" + i))
				{
					MySqlFuncs.MySqlSettings.CustomRollover.Commands[i] = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString" + i, string.Empty);
					MySqlFuncs.MySqlSettings.CustomRollover.CatchUp[i] = ini.GetValue("MySQL", "CustomMySqlRolloverCatchUp" + i, true);
				}
			}

			// MySQL - custom start-up
			MySqlFuncs.MySqlSettings.CustomStartUp.Enabled = ini.GetValue("MySQL", "CustomMySqlStartUpEnabled", false);
			MySqlFuncs.MySqlSettings.CustomStartUp.Commands[0] = ini.GetValue("MySQL", "CustomMySqlStartUpCommandString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("MySQL", "CustomMySqlStartUpCommandString" + i))
					MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i] = ini.GetValue("MySQL", "CustomMySqlStartUpCommandString" + i, string.Empty);
			}
			#endregion

			#region Custom HTTP Settings
			// Custom HTTP - seconds
			CustomHttpSecondsStrings[0] = ini.GetValue("HTTP", "CustomHttpSecondsString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpSecondsString" + i))
					CustomHttpSecondsStrings[i] = ini.GetValue("HTTP", "CustomHttpSecondsString" + i, string.Empty);
			}

			CustomHttpSecondsEnabled = ini.GetValue("HTTP", "CustomHttpSecondsEnabled", false);
			CustomHttpSecondsInterval = ini.GetValue("HTTP", "CustomHttpSecondsInterval", 10, 1);

			// Custom HTTP - minutes
			CustomHttpMinutesStrings[0] = ini.GetValue("HTTP", "CustomHttpMinutesString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpMinutesString" + i))
					CustomHttpMinutesStrings[i] = ini.GetValue("HTTP", "CustomHttpMinutesString" + i, string.Empty);
			}

			CustomHttpMinutesEnabled = ini.GetValue("HTTP", "CustomHttpMinutesEnabled", false);
			CustomHttpMinutesIntervalIndex = ini.GetValue("HTTP", "CustomHttpMinutesIntervalIndex", -1);
			if (CustomHttpMinutesIntervalIndex >= 0 && CustomHttpMinutesIntervalIndex < FactorsOf60.Length)
			{
				CustomHttpMinutesInterval = FactorsOf60[CustomHttpMinutesIntervalIndex];
			}
			else
			{
				CustomHttpMinutesInterval = 10;
				CustomHttpMinutesIntervalIndex = 6;
				ini.SetValue("HTTP", "CustomHttpMinutesIntervalIndex", CustomHttpMinutesIntervalIndex);
				rewriteRequired = true;
			}

			// Http - custom roll-over
			CustomHttpRolloverEnabled = ini.GetValue("HTTP", "CustomHttpRolloverEnabled", false);
			CustomHttpRolloverStrings[0] = ini.GetValue("HTTP", "CustomHttpRolloverString", string.Empty);
			for (var i = 1; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "CustomHttpRolloverString" + i))
					CustomHttpRolloverStrings[i] = ini.GetValue("HTTP", "CustomHttpRolloverString" + i, string.Empty);
			}
			#endregion

			#region HTTP Files
			// Http files
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("HTTP", "HttpFileUrl" + i))
				{
					HttpFilesConfig[i].Enabled = ini.GetValue("HTTP", "HttpFileEnabled" + i, false);
					HttpFilesConfig[i].Url = System.Web.HttpUtility.UrlDecode(ini.GetValue("HTTP", "HttpFileUrl" + i, string.Empty));
					HttpFilesConfig[i].Remote = ini.GetValue("HTTP", "HttpFileRemote" + i, string.Empty);
					HttpFilesConfig[i].Interval = ini.GetValue("HTTP", "HttpFileInterval" + i, 0);
					HttpFilesConfig[i].Upload = ini.GetValue("HTTP", "HttpFileUpload" + i, false);
					HttpFilesConfig[i].Timed = ini.GetValue("HTTP", "HttpFileTimed" + i, false);
					HttpFilesConfig[i].StartTimeString = ini.GetValue("HTTP", "HttpFileStartTime" + i, "00:00");
					if (HttpFilesConfig[i].Timed)
					{
						HttpFilesConfig[i].SetInitialNextInterval(DateTime.Now);
					}
				}
			}
			#endregion

			#region Dashboard Chart Options
			// Select-a-Chart settings
			for (int i = 0; i < SelectaChartOptions.series.Length; i++)
			{
				SelectaChartOptions.series[i] = ini.GetValue("Select-a-Chart", "Series" + i, "0");
				SelectaChartOptions.colours[i] = ini.GetValue("Select-a-Chart", "Colour" + i, string.Empty);
			}

			// Select-a-Period settings
			for (int i = 0; i < SelectaPeriodOptions.series.Length; i++)
			{
				SelectaPeriodOptions.series[i] = ini.GetValue("Select-a-Period", "Series" + i, "0");
				SelectaPeriodOptions.colours[i] = ini.GetValue("Select-a-Period", "Colour" + i, string.Empty);
			}
			SelectaPeriodOptions.fromDate = ini.GetValue("Select-a-Period", "FromDate", DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"));
			SelectaPeriodOptions.toDate = ini.GetValue("Select-a-Period", "ToDate", DateTime.Now.ToString("yyyy-MM-dd"));
			#endregion

			// Email settings
			SmtpOptions.Enabled = ini.GetValue("SMTP", "Enabled", false);
			SmtpOptions.Server = ini.GetValue("SMTP", "ServerName", string.Empty);
			SmtpOptions.Port = ini.GetValue("SMTP", "Port", 587, 1, 65535);
			SmtpOptions.SslOption = ini.GetValue("SMTP", "SSLOption", 1);
			SmtpOptions.AuthenticationMethod = ini.GetValue("SMTP", "RequiresAuthentication", 0, 0, 1);
			SmtpOptions.User = ini.GetValue("SMTP", "User", string.Empty);
			SmtpOptions.Password = ini.GetValue("SMTP", "Password", string.Empty);
			SmtpOptions.IgnoreCertErrors = ini.GetValue("SMTP", "IgnoreCertErrors", false);

			// Growing Degree Days
			GrowingBase1 = ini.GetValue("GrowingDD", "BaseTemperature1", (Units.Temp == 0 ? 5.0 : 40.0));
			GrowingBase2 = ini.GetValue("GrowingDD", "BaseTemperature2", (Units.Temp == 0 ? 10.0 : 50.0));
			GrowingYearStarts = ini.GetValue("GrowingDD", "YearStarts", (Latitude >= 0 ? 1 : 7), 1, 12);
			GrowingCap30C = ini.GetValue("GrowingDD", "Cap30C", true);

			// Temperature Sum
			TempSumYearStarts = ini.GetValue("TempSum", "TempSumYearStart", (Latitude >= 0 ? 1 : 7), 1, 12);
			TempSumBase1 = ini.GetValue("TempSum", "BaseTemperature1", GrowingBase1);
			TempSumBase2 = ini.GetValue("TempSum", "BaseTemperature2", GrowingBase2);

			#region Custom Log Settings
			// Custom Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (ini.ValueExists("CustomLogs", "DailyFilename" + i))
					CustomDailyLogSettings[i].FileName = ini.GetValue("CustomLogs", "DailyFilename" + i, string.Empty);

				if (ini.ValueExists("CustomLogs", "DailyContent" + i))
					CustomDailyLogSettings[i].ContentString = ini.GetValue("CustomLogs", "DailyContent" + i, string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);

				if (string.IsNullOrEmpty(CustomDailyLogSettings[i].FileName) || string.IsNullOrEmpty(CustomDailyLogSettings[i].ContentString))
					CustomDailyLogSettings[i].Enabled = false;
				else
					CustomDailyLogSettings[i].Enabled = ini.GetValue("CustomLogs", "DailyEnabled" + i, false);



				if (ini.ValueExists("CustomLogs", "IntervalFilename" + i))
					CustomIntvlLogSettings[i].FileName = ini.GetValue("CustomLogs", "IntervalFilename" + i, string.Empty);

				if (ini.ValueExists("CustomLogs", "IntervalContent" + i))
					CustomIntvlLogSettings[i].ContentString = ini.GetValue("CustomLogs", "IntervalContent" + i, string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);

				if (string.IsNullOrEmpty(CustomIntvlLogSettings[i].FileName) || string.IsNullOrEmpty(CustomIntvlLogSettings[i].ContentString))
					CustomIntvlLogSettings[i].Enabled = false;
				else
					CustomIntvlLogSettings[i].Enabled = ini.GetValue("CustomLogs", "IntervalEnabled" + i, false);

				if (ini.ValueExists("CustomLogs", "IntervalIdx" + i))
				{
					CustomIntvlLogSettings[i].IntervalIdx = ini.GetValue("CustomLogs", "IntervalIdx" + i, DataLogInterval);

					if (CustomIntvlLogSettings[i].IntervalIdx >= 0 && CustomIntvlLogSettings[i].IntervalIdx < FactorsOf60.Length)
					{
						CustomIntvlLogSettings[i].Interval = FactorsOf60[CustomIntvlLogSettings[i].IntervalIdx];
					}
					else
					{
						CustomIntvlLogSettings[i].Interval = FactorsOf60[DataLogInterval];
						CustomIntvlLogSettings[i].IntervalIdx = DataLogInterval;
						ini.SetValue("CustomLogs", "IntervalIdx" + i, DataLogInterval);
						rewriteRequired = true;
					}
				}
				else
				{
					CustomIntvlLogSettings[i].Interval = FactorsOf60[DataLogInterval];
					CustomIntvlLogSettings[i].IntervalIdx = DataLogInterval;
				}
			}
			#endregion

			// laser sensors
			LaserPrimarySnowSensor = ini.GetValue("Laser", "PrimarySnowSensor", SnowAutomated);
			for (var i = 1; i < LaserDepthBaseline.Length; i++)
			{
				LaserDepthBaseline[i] = ini.GetValue("Laser", "LaserDepthOffset" + i, -1);
				LaserIsSnowSensor[i] = ini.GetValue("Laser", "IsSnowSensor" + i, SnowAutomated == i);
			}

			#region Decrpytion
			// do we need to decrypt creds?
			if (ProgramOptions.EncryptedCreds)
			{
				if (!Program.CheckInstanceId(false))
				{
					LogCriticalMessage("ERROR: The UniqueId.txt file is missing or corrupt, please restore it from a backup");
					LogConsoleMessage("ERROR: The UniqueId.txt file is missing or corrupt, please restore it from a backup", ConsoleColor.Red);
					Environment.Exit(1);
				}

				ProgramOptions.SettingsUsername = Crypto.DecryptString(ProgramOptions.SettingsUsername, Program.InstanceId, "SettingsUsername");
				ProgramOptions.SettingsPassword = Crypto.DecryptString(ProgramOptions.SettingsPassword, Program.InstanceId, "SettingsPassword");
				WllApiKey = Crypto.DecryptString(WllApiKey, Program.InstanceId, "WllApiKey");
				//PurpleAirApiKey = Crypto.DecryptString(PurpleAirApiKey, Program.InstanceId, "PurpleAirApiKey");
				//PurpleAirReadKey = Crypto.DecryptString(PurpleAirReadKey, Program.InstanceId, "PurpleAirReadKey");

				WllApiSecret = Crypto.DecryptString(WllApiSecret, Program.InstanceId, "WllApiSecret");
				JsonStationOptions.MqttUsername = Crypto.DecryptString(JsonStationOptions.MqttUsername, Program.InstanceId, "JsonStationMqttUsername");
				JsonStationOptions.MqttPassword = Crypto.DecryptString(JsonStationOptions.MqttPassword, Program.InstanceId, "JsonStationMqttPassword");
				AirLinkApiKey = Crypto.DecryptString(AirLinkApiKey, Program.InstanceId, "AirLinkApiKey");
				AirLinkApiSecret = Crypto.DecryptString(AirLinkApiSecret, Program.InstanceId, "AirLinkApiSecret");
				FtpOptions.Username = Crypto.DecryptString(FtpOptions.Username, Program.InstanceId, "FtpOptions.Username");
				FtpOptions.Password = Crypto.DecryptString(FtpOptions.Password, Program.InstanceId, "FtpOptions.Password");
				FtpOptions.PhpSecret = Crypto.DecryptString(FtpOptions.PhpSecret, Program.InstanceId, "FtpOptions.PhpSecret");
				Wund.PW = Crypto.DecryptString(Wund.PW, Program.InstanceId, "Wund.PW");
				Windy.PW = Crypto.DecryptString(Windy.PW, Program.InstanceId, "Windy.PW");
				Windy.ApiKey = Crypto.DecryptString(Windy.ApiKey, Program.InstanceId, "Windy.ApiKey");
				AWEKAS.PW = Crypto.DecryptString(AWEKAS.PW, Program.InstanceId, "AWEKAS.PW");
				WindGuru.PW = Crypto.DecryptString(WindGuru.PW, Program.InstanceId, "WindGuru.PW");
				WCloud.PW = Crypto.DecryptString(WCloud.PW, Program.InstanceId, "WCloud.PW");
				PWS.PW = Crypto.DecryptString(PWS.PW, Program.InstanceId, "PWS.PW");
				WOW.PW = Crypto.DecryptString(WOW.PW, Program.InstanceId, "WOW.PW");
				WOW_BE.PW = Crypto.DecryptString(WOW_BE.PW, Program.InstanceId, "WOW-BE.PW");
				if (APRS.PW != "-1")
				{
					APRS.PW = Crypto.DecryptString(APRS.PW, Program.InstanceId, "APRS.PW");
				}
				OpenWeatherMap.PW = Crypto.DecryptString(OpenWeatherMap.PW, Program.InstanceId, "OpenWeatherMap.PW");
				Bluesky.PW = Crypto.DecryptString(Bluesky.PW, Program.InstanceId, "Bluesky.PW");
				MQTT.Username = Crypto.DecryptString(MQTT.Username, Program.InstanceId, "MQTT.Username");
				MQTT.Password = Crypto.DecryptString(MQTT.Password, Program.InstanceId, "MQTT.Password");
				MySqlFuncs.MySqlConnSettings.UserID = Crypto.DecryptString(MySqlFuncs.MySqlConnSettings.UserID, Program.InstanceId, "MySql UserID");
				MySqlFuncs.MySqlConnSettings.Password = Crypto.DecryptString(MySqlFuncs.MySqlConnSettings.Password, Program.InstanceId, "MySql Password");
				SmtpOptions.User = Crypto.DecryptString(SmtpOptions.User, Program.InstanceId, "SmtpOptions.User");
				SmtpOptions.Password = Crypto.DecryptString(SmtpOptions.Password, Program.InstanceId, "SmtpOptions.Password");
				HTTPProxyUser = Crypto.DecryptString(HTTPProxyUser, Program.InstanceId, "HTTPProxyUser");
				HTTPProxyPassword = Crypto.DecryptString(HTTPProxyPassword, Program.InstanceId, "HTTPProxyPassword");
				EcowittApplicationKey = Crypto.DecryptString(EcowittApplicationKey, Program.InstanceId, "EcowittSettings.AppKey");
				EcowittUserApiKey = Crypto.DecryptString(EcowittUserApiKey, Program.InstanceId, "EcowittSettings.UserApiKey");
				EcowittHttpPassword = Crypto.DecryptString(EcowittHttpPassword, Program.InstanceId, "EcowittSettings.HttpPassword");

				LogMessage("Reading Cumulus.ini file completed");
			}
			else
			{
				LogMessage("Reading Cumulus.ini file completed");
				LogMessage("Encrypting Cumulus.ini...");

				try
				{
					Program.CheckInstanceId(true);
				}
				catch (Exception ex)
				{
					LogExceptionMessage(ex, "Error creating UniqueId.txt");
					Environment.Exit(1);
				}

				recreateRequired = true;
			}
			#endregion

			if (recreateRequired)
			{
				LogMessage("Deleting existing Cumulus.ini");
				try
				{
					File.Delete("Cumulus.ini");
					LogMessage("Cumulus.ini deleted");
					// Add a pause to allow the file to be deleted
					Thread.Sleep(1000);
				}
				catch (Exception ex)
				{
					LogErrorMessage("Error deleting Cumulus.ini: " + ex.Message);
				}

				WriteIniFile();
			}
			else if (rewriteRequired && File.Exists("Cumulus.ini"))
			{
				LogMessage("Some values in Cumulus.ini had invalid values, are obsolete, or new required entries have been created");
				LogMessage("Rewriting Cumulus.ini to reflect the new configuration");
				ini.Flush();
				LogMessage("Cumulus.ini rewrite complete");
			}
		}


		internal void WriteIniFile()
		{
			LogMessage("Writing Cumulus.ini file");

			IniFile ini = new IniFile("Cumulus.ini");

			#region Program Options
			ini.SetValue("Program", "ProcessLogFiles", ProgramOptions.ProcessLogFilesLevel);

			ini.SetValue("Program", "EnableAccessibility", ProgramOptions.EnableAccessibility);

			ini.SetValue("Program", "StartupPingHost", ProgramOptions.StartupPingHost);
			ini.SetValue("Program", "StartupPingEscapeTime", ProgramOptions.StartupPingEscapeTime);

			ini.SetValue("Program", "StartupDelaySecs", ProgramOptions.StartupDelaySecs);
			ini.SetValue("Program", "StartupDelayMaxUptime", ProgramOptions.StartupDelayMaxUptime);

			ini.SetValue("Program", "StartupTask", ProgramOptions.StartupTask);
			ini.SetValue("Program", "StartupTaskParams", ProgramOptions.StartupTaskParams);
			ini.SetValue("Program", "StartupTaskWait", ProgramOptions.StartupTaskWait);

			ini.SetValue("Program", "ShutdownTask", ProgramOptions.ShutdownTask);
			ini.SetValue("Program", "ShutdownTaskParams", ProgramOptions.ShutdownTaskParams);

			ini.SetValue("Program", "DataStoppedExit", ProgramOptions.DataStoppedExit);
			ini.SetValue("Program", "DataStoppedMins", ProgramOptions.DataStoppedMins);
			ini.SetValue("Program", "DisplayLang", ProgramOptions.DisplayLanguage);

			ini.SetValue("Program", "TimeFormat", ProgramOptions.TimeFormat);
			ini.SetValue("Program", "TimeAmPmLowerCase", ProgramOptions.TimeAmPmLowerCase);
			ini.SetValue("Culture", "RemoveSpaceFromDateSeparator", ProgramOptions.Culture.RemoveSpaceFromDateSeparator);

			ini.SetValue("Station", "WarnMultiple", ProgramOptions.WarnMultiple);
			ini.SetValue("Station", "ListWebTags", ProgramOptions.ListWebTags);
			ini.SetValue("Program", "UseWebSockets", ProgramOptions.UseWebSockets);

			ini.SetValue("Program", "DataPath", ProgramOptions.DataPath);
			ini.SetValue("Program", "BackupPath", ProgramOptions.BackupPath);
			ini.SetValue("Program", "ReportsPath", ProgramOptions.ReportsPath);
			ini.SetValue("Program", "DiagsPath", ProgramOptions.DiagsPath);
			ini.SetValue("Program", "SnowLogging", SnowLogging);

			ini.SetValue("Program", "ErrorListLoggingLevel", (int) ErrorListLoggingLevel);

			ini.SetValue("Program", "SecureSettings", ProgramOptions.SecureSettings);
			ini.SetValue("Program", "SettingsUsername", Crypto.EncryptString(ProgramOptions.SettingsUsername, Program.InstanceId, "SettingsUsername"));
			ini.SetValue("Program", "SettingsPassword", Crypto.EncryptString(ProgramOptions.SettingsPassword, Program.InstanceId, "SettingsPassword"));

			ini.SetValue("Program", "EncryptedCreds", true);
			#endregion

			#region Station Options
			ini.SetValue("Station", "Type", StationType);
			ini.SetValue("Station", "Model", StationModel);
			ini.SetValue("Station", "ComportName", ComportName);
			ini.SetValue("Station", "Latitude", Latitude);
			ini.SetValue("Station", "Longitude", Longitude);
			ini.SetValue("Station", "LatTxt", LatTxt);
			ini.SetValue("Station", "LonTxt", LonTxt);
			ini.SetValue("Station", "Altitude", Altitude);
			ini.SetValue("Station", "AltitudeInFeet", AltitudeInFeet);
			ini.SetValue("Station", "TimeZone", StationOptions.TimeZoneId);
			ini.SetValue("Station", "Humidity98Fix", StationOptions.Humidity98Fix);
			ini.SetValue("Station", "Wind10MinAverage", StationOptions.CalcuateAverageWindSpeed);
			ini.SetValue("Station", "UseSpeedForAvgCalc", StationOptions.UseSpeedForAvgCalc);
			ini.SetValue("Station", "AvgBearingMinutes", StationOptions.AvgBearingMinutes);
			ini.SetValue("Station", "AvgSpeedMinutes", StationOptions.AvgSpeedMinutes);
			ini.SetValue("Station", "PeakGustMinutes", StationOptions.PeakGustMinutes);
			ini.SetValue("Station", "LCMaxWind", LCMaxWind);
			ini.SetValue("Station", "RecordSetTimeoutHrs", RecordSetTimeoutHrs);
			ini.SetValue("Station", "SnowDepthHour", SnowDepthHour);
			ini.SetValue("Station", "SnowAutomated", SnowAutomated);
			ini.SetValue("Station", "SnowSeasonStart", SnowSeasonStart);

			ini.SetValue("Station", "UseRainForIsRaining", StationOptions.UseRainForIsRaining);
			ini.SetValue("Station", "LeafWetnessIsRainingIdx", StationOptions.LeafWetnessIsRainingIdx);
			ini.SetValue("Station", "LeafWetnessIsRainingVal", StationOptions.LeafWetnessIsRainingThrsh);

			if (!DebuggingEnabled)
			{
				ini.SetValue("Station", "Logging", ProgramOptions.DebugLogging);
				ini.SetValue("Station", "DataLogging", ProgramOptions.DataLogging);
			}

			ini.SetValue("Station", "DavisReadReceptionStats", DavisOptions.ReadReceptionStats);
			ini.SetValue("Station", "DavisSetLoggerInterval", DavisOptions.SetLoggerInterval);
			ini.SetValue("Station", "UseDavisLoop2", DavisOptions.UseLoop2);
			ini.SetValue("Station", "DavisInitWaitTime", DavisOptions.InitWaitTime);
			ini.SetValue("Station", "DavisIPResponseTime", DavisOptions.IPResponseTime);
			ini.SetValue("Station", "DavisBaudRate", DavisOptions.BaudRate);
			ini.SetValue("Station", "VPrainGaugeType", DavisOptions.RainGaugeType);
			ini.SetValue("Station", "VP2ConnectionType", DavisOptions.ConnectionType);
			ini.SetValue("Station", "VP2TCPPort", DavisOptions.TCPPort);
			ini.SetValue("Station", "VP2IPAddr", DavisOptions.IPAddr);
			ini.SetValue("Station", "VP2PeriodicDisconnectInterval", DavisOptions.PeriodicDisconnectInterval);
			ini.SetValue("Station", "ForceVPBarUpdate", DavisOptions.ForceVPBarUpdate);
			ini.SetValue("Station", "DavisCloudBroadcast", DavisOptions.CloudBroadcasts);


			ini.SetValue("Station", "NoSensorCheck", StationOptions.NoSensorCheck);
			ini.SetValue("Station", "CalculatedDP", StationOptions.CalculatedDP);
			ini.SetValue("Station", "CalculatedWC", StationOptions.CalculatedWC);
			ini.SetValue("Station", "CalculatedET", StationOptions.CalculatedET);
			ini.SetValue("Station", "CalculatedSLP", StationOptions.CalculateSLP);

			ini.SetValue("Station", "RolloverHour", RolloverHour);
			ini.SetValue("Station", "Use10amInSummer", Use10amInSummer);
			//ini.SetValue("Station", "ConfirmClose", ConfirmClose)
			//ini.SetValue("Station", "CloseOnSuspend", CloseOnSuspend)
			//ini.SetValue("Station", "RestartIfUnplugged", RestartIfUnplugged)
			//ini.SetValue("Station", "RestartIfDataStops", RestartIfDataStops)
			ini.SetValue("Station", "SyncDavisClock", StationOptions.SyncTime);
			ini.SetValue("Station", "ClockSettingHour", StationOptions.ClockSettingHour);
			ini.SetValue("Station", "WS2300IgnoreStationClock", StationOptions.WS2300IgnoreStationClock);
			ini.SetValue("Station", "LogExtraSensors", StationOptions.LogExtraSensors);
			ini.SetValue("Station", "DataLogInterval", DataLogInterval);

			ini.SetValue("Station", "SyncFOReads", FineOffsetOptions.SyncReads);
			ini.SetValue("Station", "FOReadAvoidPeriod", FineOffsetOptions.ReadAvoidPeriod);
			ini.SetValue("Station", "FineOffsetReadTime", FineOffsetOptions.ReadTime);
			ini.SetValue("Station", "FineOffsetSetLoggerInterval", FineOffsetOptions.SetLoggerInterval);
			ini.SetValue("Station", "VendorID", FineOffsetOptions.VendorID);
			ini.SetValue("Station", "ProductID", FineOffsetOptions.ProductID);


			ini.SetValue("Station", "WindUnit", Units.Wind);
			ini.SetValue("Station", "PressureUnit", Units.Press);
			ini.SetValue("Station", "RainUnit", Units.Rain);
			ini.SetValue("Station", "TempUnit", Units.Temp);
			ini.SetValue("Station", "SnowDepthUnit", Units.SnowDepth);
			ini.SetValue("Station", "LaserDistancehUnit", Units.LaserDistance);

			ini.SetValue("Station", "WindSpeedDecimals", WindDPlaces);
			ini.SetValue("Station", "WindSpeedAvgDecimals", WindAvgDPlaces);
			ini.SetValue("Station", "WindRunDecimals", WindRunDPlaces);
			ini.SetValue("Station", "SunshineHrsDecimals", SunshineDPlaces);
			ini.SetValue("Station", "PressDecimals", PressDPlaces);
			ini.SetValue("Station", "RainDecimals", RainDPlaces);
			ini.SetValue("Station", "TempDecimals", TempDPlaces);
			ini.SetValue("Station", "UVDecimals", UVDPlaces);
			ini.SetValue("Station", "AirQualityDecimals", AirQualityDPlaces);


			ini.SetValue("Station", "LocName", LocationName);
			ini.SetValue("Station", "LocDesc", LocationDesc);
			ini.SetValue("Station", "StartDateIso", RecordsBeganDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			ini.SetValue("Station", "YTDrain", YTDrain);
			ini.SetValue("Station", "YTDrainyear", YTDrainyear);
			ini.SetValue("Station", "UseDataLogger", StationOptions.UseDataLogger);
			ini.SetValue("Station", "UseCumulusForecast", ForecastSource);

			ini.SetValue("Station", "HourlyForecast", HourlyForecast);
			ini.SetValue("Station", "UseCumulusPresstrendstr", StationOptions.UseCumulusPresstrendstr);
			ini.SetValue("Station", "FCpressinMB", FCpressinMB);
			ini.SetValue("Station", "FClowpress", FClowpress);
			ini.SetValue("Station", "FChighpress", FChighpress);
			ini.SetValue("Station", "UseZeroBearing", StationOptions.UseZeroBearing);
			ini.SetValue("Station", "RoundWindSpeed", StationOptions.RoundWindSpeed);
			ini.SetValue("Station", "PrimaryAqSensor", StationOptions.PrimaryAqSensor);

			ini.SetValue("Station", "EWInterval", EwOptions.Interval);
			ini.SetValue("Station", "EWFile", EwOptions.Filename);
			ini.SetValue("Station", "EWminpressureMB", EwOptions.MinPressMB);
			ini.SetValue("Station", "EWmaxpressureMB", EwOptions.MaxPressMB);
			ini.SetValue("Station", "EWMaxRainTipDiff", EwOptions.MaxRainTipDiff);
			ini.SetValue("Station", "EWpressureoffset", EwOptions.PressOffset);

			ini.SetValue("Station", "EWtempdiff", Spike.TempDiff < 999 ? ConvertUnits.UserTempToC(Spike.TempDiff) : 999.0);
			ini.SetValue("Station", "EWpressurediff", Spike.PressDiff < 999 ? ConvertUnits.UserPressToMB(Spike.PressDiff) : 999.00);
			ini.SetValue("Station", "EWhumiditydiff", Spike.HumidityDiff);
			ini.SetValue("Station", "EWgustdiff", Spike.GustDiff < 999 ? ConvertUnits.UserWindToMS(Spike.GustDiff) : 999.0);
			ini.SetValue("Station", "EWwinddiff", Spike.WindDiff < 999 ? ConvertUnits.UserWindToMS(Spike.WindDiff) : 999.0);
			ini.SetValue("Station", "EWmaxHourlyRain", Spike.MaxHourlyRain < 999 ? ConvertUnits.UserRainToMM(Spike.MaxHourlyRain) : 999.0);
			ini.SetValue("Station", "EWmaxRainRate", Spike.MaxRainRate < 999 ? ConvertUnits.UserRainToMM(Spike.MaxRainRate) : 999.0);
			ini.SetValue("Station", "EWinTempdiff", Spike.InTempDiff < 999 ? ConvertUnits.UserTempToC(Spike.InTempDiff) : 999.0);
			ini.SetValue("Station", "EWinHumiditydiff", Spike.InHumDiff);
			ini.SetValue("Station", "EWsnowdiff", Spike.SnowDiff);
			ini.SetValue("Station", "EWsnowMinInc", SnowDepthMinInc);
			ini.SetValue("Station", "SnowMedianMins", SnowDepthMedianMins);
			ini.SetValue("Station", "SnowClipDelta", SnowDepthClipDelta);
			ini.SetValue("Station", "SnowEmaTimeMins", SnowDepthEmaTimeMins);

			ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
			ini.SetValue("Station", "RainWeekStart", RainWeekStart);
			ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

			ini.SetValue("Station", "ChillHourSeasonStart", ChillHourSeasonStart);
			ini.SetValue("Station", "ChillHourThreshold", ChillHourThreshold);
			ini.SetValue("Station", "ChillHourBase", ChillHourBase);

			ini.SetValue("Station", "ErrorLogSpikeRemoval", ErrorLogSpikeRemoval);
			#endregion

			#region IMET Settings
			ini.SetValue("Station", "ImetBaudRate", ImetOptions.BaudRate);
			ini.SetValue("Station", "ImetWaitTime", ImetOptions.WaitTime);
			ini.SetValue("Station", "ImetReadDelay", ImetOptions.ReadDelay);
			ini.SetValue("Station", "ImetUpdateLogPointer", ImetOptions.UpdateLogPointer);
			#endregion

			#region RG11 Settings
			ini.SetValue("Station", "RG11Enabled", RG11Enabled);
			ini.SetValue("Station", "RG11portName", RG11Port);
			ini.SetValue("Station", "RG11TBRmode", RG11TBRmode);
			ini.SetValue("Station", "RG11tipsize", RG11tipsize);
			ini.SetValue("Station", "RG11IgnoreFirst", RG11IgnoreFirst);
			ini.SetValue("Station", "RG11DTRmode", RG11DTRmode);

			ini.SetValue("Station", "RG11Enabled2", RG11Enabled2);
			ini.SetValue("Station", "RG11portName2", RG11Port2);
			ini.SetValue("Station", "RG11TBRmode2", RG11TBRmode2);
			ini.SetValue("Station", "RG11tipsize2", RG11tipsize2);
			ini.SetValue("Station", "RG11IgnoreFirst2", RG11IgnoreFirst2);
			ini.SetValue("Station", "RG11DTRmode2", RG11DTRmode2);
			#endregion

			#region WeatherFlow Settings
			// WeatherFlow Options
			ini.SetValue("Station", "WeatherFlowDeviceId", WeatherFlowOptions.WFDeviceId);
			ini.SetValue("Station", "WeatherFlowSerialNo", WeatherFlowOptions.WFSerialNo);
			ini.SetValue("Station", "WeatherFlowTcpPort", WeatherFlowOptions.WFTcpPort);
			ini.SetValue("Station", "WeatherFlowToken", WeatherFlowOptions.WFToken);
			ini.SetValue("Station", "WeatherFlowDaysHist", WeatherFlowOptions.WFDaysHist);
			#endregion

			ini.SetValue("Station", "WxnowComment.txt", WxnowComment);

			#region WLL Settings
			// WeatherLink Live device settings
			ini.SetValue("WLL", "AutoUpdateIpAddress", WLLAutoUpdateIpAddress);
			ini.SetValue("WLL", "WLv2ApiKey", Crypto.EncryptString(WllApiKey, Program.InstanceId, "WllApiKey"));
			ini.SetValue("WLL", "WLv2ApiSecret", Crypto.EncryptString(WllApiSecret, Program.InstanceId, "WllApiSecret"));
			ini.SetValue("WLL", "WLStationId", WllStationId);
			ini.SetValue("WLL", "WLStationUuid", WllStationUuid);
			ini.SetValue("WLL", "DataStoppedOnBroadcast", WllTriggerDataStoppedOnBroadcast);
			ini.SetValue("WLL", "PrimaryRainTxId", WllPrimaryRain);
			ini.SetValue("WLL", "PrimaryTempHumTxId", WllPrimaryTempHum);
			ini.SetValue("WLL", "PrimaryWindTxId", WllPrimaryWind);
			ini.SetValue("WLL", "PrimaryRainTxId", WllPrimaryRain);
			ini.SetValue("WLL", "PrimarySolarTxId", WllPrimarySolar);
			ini.SetValue("WLL", "PrimaryUvTxId", WllPrimaryUV);
			ini.SetValue("WLL", "PrimarySunshine", WllPrimarySunshine);

			for (var i = 1; i <= 16; i++)
			{
				if (WllSoilTempTx[i] == 0)
				{
					ini.DeleteValue("WLL", "ExtraSoilTempTxId" + i);
					ini.DeleteValue("WLL", "ExtraSoilTempIdx" + i);
				}
				else
				{
					ini.SetValue("WLL", "ExtraSoilTempTxId" + i, WllSoilTempTx[i]);
					ini.SetValue("WLL", "ExtraSoilTempIdx" + i, WllSoilTempIdx[i]);
				}
			}
			for (var i = 1; i <= 16; i++)
			{
				if (WllSoilMoistureTx[i] == 0)
				{
					ini.DeleteValue("WLL", "ExtraSoilMoistureTxId" + i);
					ini.DeleteValue("WLL", "ExtraSoilMoistureIdx" + i);
				}
				else
				{
					ini.SetValue("WLL", "ExtraSoilMoistureTxId" + i, WllSoilMoistureTx[i]);
					ini.SetValue("WLL", "ExtraSoilMoistureIdx" + i, WllSoilMoistureIdx[i]);
				}
			}
			for (var i = 1; i <= 8; i++)
			{
				if (WllLeafWetTx[i] == 0)
				{
					ini.DeleteValue("WLL", "ExtraLeafTxId" + i);
					ini.DeleteValue("WLL", "ExtraLeafIdx" + i);
				}
				else
				{
					ini.SetValue("WLL", "ExtraLeafTxId" + i, WllLeafWetTx[i]);
					ini.SetValue("WLL", "ExtraLeafIdx" + i, WllSoilMoistureIdx[i]);
				}
			}
			for (int i = 1; i <= 8; i++)
			{
				if (WllExtraTempTx[i] == 0)
				{
					ini.DeleteValue("WLL", "ExtraTempTxId" + i);
					ini.DeleteValue("WLL", "ExtraTempIdx" + i);
					ini.DeleteValue("WLL", "ExtraHumOnTxId" + i);
				}
				else
				{
					ini.SetValue("WLL", "ExtraTempTxId" + i, WllExtraTempTx[i]);
					ini.SetValue("WLL", "ExtraTempIdx" + i, WllExtraTempIdx[i]);
				}
			}
			#endregion WLL Settings

			#region GW1000 Settings
			// GW1000 settings
			ini.SetValue("GW1000", "IPAddress", Gw1000IpAddress);
			ini.SetValue("GW1000", "MACAddress", Gw1000MacAddress);
			ini.SetValue("GW1000", "AutoUpdateIpAddress", Gw1000AutoUpdateIpAddress);
			ini.SetValue("GW1000", "PrimaryRainSensor", Gw1000PrimaryRainSensor);
			ini.SetValue("GW1000", "UsePiezoIsRaining", EcowittIsRainingUsePiezo);
			ini.SetValue("GW1000", "ExtraSensorDataEnabled", EcowittExtraEnabled);
			ini.SetValue("GW1000", "ExtraCloudSensorDataEnabled", EcowittCloudExtraEnabled);
			ini.SetValue("GW1000", "SetCustomServer", EcowittSetCustomServer);
			ini.SetValue("GW1000", "EcowittGwAddr", EcowittGatewayAddr);
			ini.SetValue("GW1000", "EcowittLocalAddr", EcowittLocalAddr);
			ini.SetValue("GW1000", "EcowittCustomInterval", EcowittCustomInterval);
			ini.SetValue("GW1000", "ExtraSetCustomServer", EcowittExtraSetCustomServer);
			ini.SetValue("GW1000", "EcowittExtraGwAddr", EcowittExtraGatewayAddr);
			ini.SetValue("GW1000", "EcowittExtraLocalAddr", EcowittExtraLocalAddr);
			ini.SetValue("GW1000", "EcowittExtraCustomInterval", EcowittExtraCustomInterval);
			ini.SetValue("GW1000", "EcowittUseSdcard", EcowittUseSdCard);
			ini.SetValue("GW1000", "CloudDataUpdateInterval", EcowittCloudDataUpdateInterval);

			ini.SetValue("GW1000", "HttpPassword", Crypto.EncryptString(EcowittHttpPassword, Program.InstanceId, "EcowittSettings.HttpPassword"));

			// api
			ini.SetValue("GW1000", "EcowittAppKey", Crypto.EncryptString(EcowittApplicationKey, Program.InstanceId, "EcowittSettings.AppKey"));
			ini.SetValue("GW1000", "EcowittUserKey", Crypto.EncryptString(EcowittUserApiKey, Program.InstanceId, "EcowittSettings.UserApiKey"));
			ini.SetValue("GW1000", "EcowittMacAddress", EcowittMacAddress);
			// WN34 sensor mapping
			for (int i = 1; i <= 8; i++)
			{
				ini.SetValue("GW1000", "WN34MapChan" + i, EcowittMapWN34[i]);
			}
			// forwarders
			for (int i = 0; i < EcowittForwarders.Length; i++)
			{
				if (string.IsNullOrEmpty(EcowittForwarders[i]))
					ini.DeleteValue("GW1000", "Forwarder" + i);
				else
					ini.SetValue("GW1000", "Forwarder" + i, EcowittForwarders[i].ToString());
			}
			// extra forwarders
			ini.SetValue("GW1000", "ExtraUseMainForwarders", EcowittExtraUseMainForwarders);
			for (int i = 0; i < EcowittExtraForwarders.Length; i++)
			{
				if (string.IsNullOrEmpty(EcowittExtraForwarders[i]))
					ini.DeleteValue("GW1000", "ExtraForwarder" + i);
				else
					ini.SetValue("GW1000", "ExtraForwarder" + i, EcowittExtraForwarders[i].ToString());
			}
			#endregion GW1000 Settings

			#region PurpleAir Settings
			// PurpleAir settings
			ini.SetValue("PurpleAir", "Enabled", PurpleAirEnabled);
			for (var i = 1; i < 5; i++)
			{
				if (string.IsNullOrEmpty(PurpleAirIpAddress[i - 1]))
				{
					ini.DeleteValue("PurpleAir", "IpAddress" + i);
					ini.DeleteValue("PurpleAir", "Algorithm" + i);
				}
				else
				{
					ini.SetValue("PurpleAir", "IpAddress" + i, PurpleAirIpAddress[i - 1]);
					ini.SetValue("PurpleAir", "Algorithm" + i, PurpleAirAlgorithm[i - 1]);
					ini.SetValue("PurpleAir", "TempHumSensor" + i, PurpleAirThSensor[i - 1]);
				}
			}
			//ini.SetValue("PurpleAir", "ApiKey", Crypto.EncryptString(PurpleAirApiKey, Program.InstanceId, "PurpleAirApiKey"));
			//ini.SetValue("PurpleAir", "SensorId", PurpleAirSensorIndex);
			//ini.SetValue("PurpleAir", "ReadKey", Crypto.EncryptString(PurpleAirReadKey, Program.InstanceId, "PurpleAirReadKey"));
			#endregion PurpleAir Settings

			// Ambient settings
			ini.SetValue("Ambient", "ExtraSensorDataEnabled", AmbientExtraEnabled);

			#region JSON Station Settings
			// JSON station options
			ini.SetValue("JsonStation", "ConnectionType", JsonStationOptions.Connectiontype);
			ini.SetValue("JsonStation", "SourceFile", JsonStationOptions.SourceFile);
			ini.SetValue("JsonStation", "FileDelay", JsonStationOptions.FileReadDelay);
			ini.SetValue("JsonStation", "FileIgnore", JsonStationOptions.FileIgnoreTime);
			ini.SetValue("JsonStation", "MqttServer", JsonStationOptions.MqttServer);
			ini.SetValue("JsonStation", "MqttServerPort", JsonStationOptions.MqttPort);
			ini.SetValue("JsonStation", "MqttUsername", Crypto.EncryptString(JsonStationOptions.MqttUsername, Program.InstanceId, "JsonStationMqttUsername"));
			ini.SetValue("JsonStation", "MqttPassword", Crypto.EncryptString(JsonStationOptions.MqttPassword, Program.InstanceId, "JsonStationMqttPassword"));
			ini.SetValue("JsonStation", "MqttUseTls", JsonStationOptions.MqttUseTls);
			ini.SetValue("JsonStation", "MqttTopic", JsonStationOptions.MqttTopic);
			// JSON station Extra Sensors
			ini.SetValue("JsonExtraStation", "ExtraSensorDataEnabled", JsonExtraStationOptions.ExtraSensorsEnabled);
			ini.SetValue("JsonExtraStation", "ConnectionType", JsonExtraStationOptions.Connectiontype);
			ini.SetValue("JsonExtraStation", "SourceFile", JsonExtraStationOptions.SourceFile);
			ini.SetValue("JsonExtraStation", "FileDelay", JsonExtraStationOptions.FileReadDelay);
			ini.SetValue("JsonExtraStation", "FileIgnore", JsonExtraStationOptions.FileIgnoreTime);
			ini.SetValue("JsonExtraStation", "MqttServer", JsonExtraStationOptions.MqttServer);
			ini.SetValue("JsonExtraStation", "MqttServerPort", JsonExtraStationOptions.MqttPort);
			ini.SetValue("JsonExtraStation", "MqttUsername", JsonExtraStationOptions.MqttUsername);
			ini.SetValue("JsonExtraStation", "MqttPassword", JsonExtraStationOptions.MqttPassword);
			ini.SetValue("JsonExtraStation", "MqttUseTls", JsonExtraStationOptions.MqttUseTls);
			ini.SetValue("JsonExtraStation", "MqttTopic", JsonExtraStationOptions.MqttTopic);
			#endregion JSON Station Settings

			#region Sensor Maps
			// Sensor Mappings
			ini.SetValue("SensorMaps", "PrimaryTHSensor", SensorMaps.PrimaryTempHum);  // 0=default, 1-8=extra t/h sensor number, 99=use indoor sensor
			ini.SetValue("SensorMaps", "PrimaryIndoorTHSensor", SensorMaps.PrimaryIndoorTempHum);  // 0=default, 1-8=extra t/h sensor number
			ini.SetValue("SensorMaps", "SolarEnabled", SensorMaps.SolarEnabled);
			ini.SetValue("SensorMaps", "Solar", SensorMaps.Solar);
			ini.SetValue("SensorMaps", "UVEnabled", SensorMaps.UVEnabled);
			ini.SetValue("SensorMaps", "UV", SensorMaps.UV);
			ini.SetValue("SensorMaps", "ExtraTempHumEnabled", SensorMaps.ExtraTempHumEnabled);
			ini.SetValue("SensorMaps", "ExtraTempHum", SensorMaps.ExtraTempHum);
			ini.SetValue("SensorMaps", "SoilTempEnabled", SensorMaps.SoilTempEnabled);
			ini.SetValue("SensorMaps", "SoilTemp", SensorMaps.SoilTemp);
			ini.SetValue("SensorMaps", "SoilMoistEnabled", SensorMaps.SoilMoistEnabled);
			ini.SetValue("SensorMaps", "SoilMoist", SensorMaps.SoilMoist);
			ini.SetValue("SensorMaps", "SoilECEnabled", SensorMaps.SoilEcEnabled);
			ini.SetValue("SensorMaps", "SoilEC", SensorMaps.SoilEc);
			ini.SetValue("SensorMaps", "LeafWetEnabled", SensorMaps.LeafWetEnabled);
			ini.SetValue("SensorMaps", "LeafWet", SensorMaps.LeafWet);
			ini.SetValue("SensorMaps", "UserTempEnabled", SensorMaps.UserTempEnabled);
			ini.SetValue("SensorMaps", "UserTemp", SensorMaps.UserTemp);
			ini.SetValue("SensorMaps", "AirQualEnabled", SensorMaps.AirQualEnabled);
			ini.SetValue("SensorMaps", "AirQual", SensorMaps.AirQual);
			ini.SetValue("SensorMaps", "CO2Enabled", SensorMaps.CO2Enabled);
			ini.SetValue("SensorMaps", "CO2", SensorMaps.CO2);
			ini.SetValue("SensorMaps", "LightningEnabled", SensorMaps.LightningEnabled);
			ini.SetValue("SensorMaps", "Lightning", SensorMaps.Lightning);
			ini.SetValue("SensorMaps", "LeakEnabled", SensorMaps.LeakEnabled);
			ini.SetValue("SensorMaps", "Leak", SensorMaps.Leak);
			ini.SetValue("SensorMaps", "CameraEnabled", SensorMaps.CameraEnabled);
			ini.SetValue("SensorMaps", "Camera", SensorMaps.Camera);
			ini.SetValue("SensorMaps", "LaserDistEnabled", SensorMaps.LaserDistEnabled);
			ini.SetValue("SensorMaps", "LaserDist", SensorMaps.LaserDist);
			ini.SetValue("SensorMaps", "BlackGlobeEnabled", SensorMaps.BlackGlobeEnabled);
			ini.SetValue("SensorMaps", "BlackGlobe", SensorMaps.BlackGlobe);
			#endregion Sensor Maps


			// AirLink settings
			ini.SetValue("AirLink", "IsWllNode", AirLinkIsNode);
			ini.SetValue("AirLink", "WLv2ApiKey", Crypto.EncryptString(AirLinkApiKey, Program.InstanceId, "AirLinkApiKey"));
			ini.SetValue("AirLink", "WLv2ApiSecret", Crypto.EncryptString(AirLinkApiSecret, Program.InstanceId, "AirLinkApiSecret"));
			ini.SetValue("AirLink", "AutoUpdateIpAddress", AirLinkAutoUpdateIpAddress);
			ini.SetValue("AirLink", "In-Enabled", AirLinkInEnabled);
			ini.SetValue("AirLink", "In-IPAddress", AirLinkInIPAddr);
			ini.SetValue("AirLink", "In-WLStationId", AirLinkInStationId);
			ini.SetValue("AirLink", "In-Hostname", AirLinkInHostName);

			ini.SetValue("AirLink", "Out-Enabled", AirLinkOutEnabled);
			ini.SetValue("AirLink", "Out-IPAddress", AirLinkOutIPAddr);
			ini.SetValue("AirLink", "Out-WLStationId", AirLinkOutStationId);
			ini.SetValue("AirLink", "Out-Hostname", AirLinkOutHostName);
			ini.SetValue("AirLink", "AQIformula", airQualityIndex);

			ini.SetValue("Web Site", "ForumURL", ForumURL);
			ini.SetValue("Web Site", "WebcamURL", WebcamURL[0]);
			if (!string.IsNullOrWhiteSpace(WebcamURL[1])) ini.SetValue("Web Site", "WebcamURL1", WebcamURL[1]);
			if (!string.IsNullOrWhiteSpace(WebcamURL[2])) ini.SetValue("Web Site", "WebcamURL2", WebcamURL[2]);
			if (!string.IsNullOrWhiteSpace(WebcamURL[3])) ini.SetValue("Web Site", "WebcamURL3", WebcamURL[3]);

			ini.SetValue("FTP site", "Enabled", FtpOptions.Enabled);
			ini.SetValue("FTP site", "Host", FtpOptions.Hostname);
			ini.SetValue("FTP site", "Port", FtpOptions.Port);
			ini.SetValue("FTP site", "Username", Crypto.EncryptString(FtpOptions.Username, Program.InstanceId, "FtpOptions.Username"));
			ini.SetValue("FTP site", "Password", Crypto.EncryptString(FtpOptions.Password, Program.InstanceId, "FtpOptions.Password"));
			ini.SetValue("FTP site", "Directory", FtpOptions.Directory);

			//ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);  // Deprecated - now read-only
			ini.SetValue("FTP site", "Sslftp", (int) FtpOptions.FtpMode);
			// BUILD 3092 - added alternate SFTP authentication options
			ini.SetValue("FTP site", "SshFtpAuthentication", FtpOptions.SshAuthen);
			ini.SetValue("FTP site", "SshFtpPskFile", FtpOptions.SshPskFile);

			ini.SetValue("FTP site", "ConnectionAutoDetect", FtpOptions.AutoDetect);
			ini.SetValue("FTP site", "IgnoreCertErrors", FtpOptions.IgnoreCertErrors);

			ini.SetValue("FTP site", "FTPlogging", FtpOptions.Logging);
			ini.SetValue("FTP site", "FTPloggingLevel", FtpOptions.LoggingLevel);
			ini.SetValue("FTP site", "UTF8encode", UTF8encode);
			ini.SetValue("FTP site", "EnableRealtime", RealtimeIntervalEnabled);
			ini.SetValue("FTP site", "RealtimeInterval", RealtimeInterval);
			ini.SetValue("FTP site", "RealtimeFTPEnabled", FtpOptions.RealtimeEnabled);
			ini.SetValue("FTP site", "RealtimeTxtCreate", RealtimeFiles[0].Create);
			ini.SetValue("FTP site", "RealtimeTxtFTP", RealtimeFiles[0].FTP);
			ini.SetValue("FTP site", "RealtimeTxtCopy", RealtimeFiles[0].Copy);

			ini.SetValue("FTP site", "RealtimeGaugesTxtCreate", RealtimeFiles[1].Create);
			ini.SetValue("FTP site", "RealtimeGaugesTxtFTP", RealtimeFiles[1].FTP);
			ini.SetValue("FTP site", "RealtimeGaugesTxtCopy", RealtimeFiles[1].Copy);

			ini.SetValue("FTP site", "IntervalEnabled", WebIntervalEnabled);
			ini.SetValue("FTP site", "IntervalFtpEnabled", FtpOptions.IntervalEnabled);

			ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
			for (var i = 0; i < StdWebFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + StdWebFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, StdWebFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, StdWebFiles[i].FTP);
				ini.SetValue("FTP site", keyNameCopy, StdWebFiles[i].Copy);
			}

			for (var i = 0; i < GraphDataFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, GraphDataFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, GraphDataFiles[i].FTP);
				ini.SetValue("FTP site", keyNameCopy, GraphDataFiles[i].Copy);
			}

			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				var keyNameCreate = "Create-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameFTP = "Ftp-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				var keyNameCopy = "Copy-" + GraphDataEodFiles[i].LocalFileName.Split('.')[0];
				ini.SetValue("FTP site", keyNameCreate, GraphDataEodFiles[i].Create);
				ini.SetValue("FTP site", keyNameFTP, GraphDataEodFiles[i].FTP);
				ini.SetValue("FTP site", keyNameCopy, GraphDataEodFiles[i].Copy);
			}

			ini.SetValue("FTP site", "IncludeMoonImage", MoonImage.Ftp);
			ini.SetValue("FTP site", "CopyMoonImage", MoonImage.Copy);
			ini.SetValue("FTP site", "FTPRename", FTPRename);
			ini.SetValue("FTP site", "DeleteBeforeUpload", DeleteBeforeUpload);
			ini.SetValue("FTP site", "ActiveFTP", FtpOptions.ActiveMode);
			ini.SetValue("FTP site", "DisableEPSV", FtpOptions.DisableEPSV);
			ini.SetValue("FTP site", "DisableFtpsExplicit", FtpOptions.DisableExplicit);

			ini.SetValue("FTP site", "ExternalProgram", ExternalProgram);
			ini.SetValue("FTP site", "RealtimeProgram", RealtimeProgram);
			ini.SetValue("FTP site", "DailyProgram", DailyProgram);
			ini.SetValue("FTP site", "ExternalParams", ExternalParams);
			ini.SetValue("FTP site", "RealtimeParams", RealtimeParams);
			ini.SetValue("FTP site", "DailyParams", DailyParams);

			// Local Copy Options
			ini.SetValue("FTP site", "EnableLocalCopy", FtpOptions.LocalCopyEnabled);
			ini.SetValue("FTP site", "LocalCopyFolder", FtpOptions.LocalCopyFolder);

			// PHP upload options
			ini.SetValue("FTP site", "PHP-URL", FtpOptions.PhpUrl);
			ini.SetValue("FTP site", "PHP-Secret", Crypto.EncryptString(FtpOptions.PhpSecret, Program.InstanceId, "FtpOptions.PhpSecret"));
			ini.SetValue("FTP site", "PHP-IgnoreCertErrors", FtpOptions.PhpIgnoreCertErrors);
			ini.SetValue("FTP site", "PHP-UseGet", FtpOptions.PhpUseGet);
			ini.SetValue("FTP site", "PHP-UseBrotli", FtpOptions.PhpUseBrotli);
			ini.SetValue("FTP site", "MaxConcurrentUploads", FtpOptions.MaxConcurrentUploads);

			for (int i = 0; i < numextrafiles; i++)
			{
				if (string.IsNullOrEmpty(ExtraFiles[i].local) && string.IsNullOrEmpty(ExtraFiles[i].remote))
				{
					ini.DeleteValue("FTP site", "ExtraEnable" + i);
					ini.DeleteValue("FTP site", "ExtraLocal" + i);
					ini.DeleteValue("FTP site", "ExtraRemote" + i);
					ini.DeleteValue("FTP site", "ExtraProcess" + i);
					ini.DeleteValue("FTP site", "ExtraBinary" + i);
					ini.DeleteValue("FTP site", "ExtraRealtime" + i);
					ini.DeleteValue("FTP site", "ExtraFTP" + i);
					ini.DeleteValue("FTP site", "ExtraUTF" + i);
					ini.DeleteValue("FTP site", "ExtraEOD" + i);
					ini.DeleteValue("FTP site", "ExtraIncLogFile" + i);
				}
				else
				{
					ini.SetValue("FTP site", "ExtraEnable" + i, ExtraFiles[i].enable);
					ini.SetValue("FTP site", "ExtraLocal" + i, ExtraFiles[i].local);
					ini.SetValue("FTP site", "ExtraRemote" + i, ExtraFiles[i].remote);
					ini.SetValue("FTP site", "ExtraProcess" + i, ExtraFiles[i].process);
					ini.SetValue("FTP site", "ExtraBinary" + i, ExtraFiles[i].binary);
					ini.SetValue("FTP site", "ExtraRealtime" + i, ExtraFiles[i].realtime);
					ini.SetValue("FTP site", "ExtraFTP" + i, ExtraFiles[i].FTP);
					ini.SetValue("FTP site", "ExtraUTF" + i, ExtraFiles[i].UTF8);
					ini.SetValue("FTP site", "ExtraEOD" + i, ExtraFiles[i].endofday);
					ini.SetValue("FTP site", "ExtraIncLogFile" + i, ExtraFiles[i].incrementalLogfile);
				}
			}

			ini.SetValue("Station", "CloudBaseInFeet", CloudBaseInFeet);

			ini.SetValue("Wunderground", "ID", Wund.ID);
			ini.SetValue("Wunderground", "Password", Crypto.EncryptString(Wund.PW, Program.InstanceId, "Wund.PW"));
			ini.SetValue("Wunderground", "Enabled", Wund.Enabled);
			ini.SetValue("Wunderground", "RapidFire", Wund.RapidFireEnabled);
			ini.SetValue("Wunderground", "Interval", Wund.Interval);
			ini.SetValue("Wunderground", "SendUV", Wund.SendUV);
			ini.SetValue("Wunderground", "SendSR", Wund.SendSolar);
			ini.SetValue("Wunderground", "SendIndoor", Wund.SendIndoor);
			ini.SetValue("Wunderground", "SendAverage", Wund.SendAverage);
			ini.SetValue("Wunderground", "CatchUp", Wund.CatchUp);
			ini.SetValue("Wunderground", "SendSoilTemp1", Wund.SendSoilTemp1);
			ini.SetValue("Wunderground", "SendSoilTemp2", Wund.SendSoilTemp2);
			ini.SetValue("Wunderground", "SendSoilTemp3", Wund.SendSoilTemp3);
			ini.SetValue("Wunderground", "SendSoilTemp4", Wund.SendSoilTemp4);
			ini.SetValue("Wunderground", "SendSoilMoisture1", Wund.SendSoilMoisture1);
			ini.SetValue("Wunderground", "SendSoilMoisture2", Wund.SendSoilMoisture2);
			ini.SetValue("Wunderground", "SendSoilMoisture3", Wund.SendSoilMoisture3);
			ini.SetValue("Wunderground", "SendSoilMoisture4", Wund.SendSoilMoisture4);
			ini.SetValue("Wunderground", "SendLeafWetness1", Wund.SendLeafWetness1);
			ini.SetValue("Wunderground", "SendLeafWetness2", Wund.SendLeafWetness2);
			ini.SetValue("Wunderground", "SendAirQuality", Wund.SendAirQuality);
			ini.SetValue("Wunderground", "SendExtraTemp1", Wund.SendExtraTemp1);
			ini.SetValue("Wunderground", "SendExtraTemp2", Wund.SendExtraTemp2);
			ini.SetValue("Wunderground", "SendExtraTemp3", Wund.SendExtraTemp3);
			ini.SetValue("Wunderground", "SendExtraTemp4", Wund.SendExtraTemp4);

			ini.SetValue("Windy", "Password", Crypto.EncryptString(Windy.PW, Program.InstanceId, "Windy.PW"));
			ini.SetValue("Windy", "APIkey", Crypto.EncryptString(Windy.ApiKey, Program.InstanceId, "Windy.ApiKey"));
			ini.SetValue("Windy", "StationId", Windy.StationId);
			ini.SetValue("Windy", "Enabled", Windy.Enabled);
			ini.SetValue("Windy", "Interval", Windy.Interval);
			ini.SetValue("Windy", "SendUV", Windy.SendUV);
			ini.SetValue("Windy", "SendSolar", Windy.SendSolar);

			ini.SetValue("Awekas", "User", AWEKAS.ID);
			ini.SetValue("Awekas", "Password", Crypto.EncryptString(AWEKAS.PW, Program.InstanceId, "AWEKAS.PW"));
			ini.SetValue("Awekas", "Language", AWEKAS.Lang);
			ini.SetValue("Awekas", "Enabled", AWEKAS.Enabled);
			ini.SetValue("Awekas", "Interval", AWEKAS.Interval);
			ini.SetValue("Awekas", "SendUV", AWEKAS.SendUV);
			ini.SetValue("Awekas", "SendSR", AWEKAS.SendSolar);
			ini.SetValue("Awekas", "SendSoilTemp", AWEKAS.SendSoilTemp);
			ini.SetValue("Awekas", "SendIndoor", AWEKAS.SendIndoor);
			ini.SetValue("Awekas", "SendSoilMoisture", AWEKAS.SendSoilMoisture);
			ini.SetValue("Awekas", "SendLeafWetness", AWEKAS.SendLeafWetness);
			ini.SetValue("Awekas", "SendAirQuality", AWEKAS.SendAirQuality);

			ini.SetValue("WeatherCloud", "Wid", WCloud.ID);
			ini.SetValue("WeatherCloud", "Key", Crypto.EncryptString(WCloud.PW, Program.InstanceId, "WCloud.PW"));
			ini.SetValue("WeatherCloud", "Enabled", WCloud.Enabled);
			ini.SetValue("WeatherCloud", "Interval", WCloud.Interval);
			ini.SetValue("WeatherCloud", "SendUV", WCloud.SendUV);
			ini.SetValue("WeatherCloud", "SendSR", WCloud.SendSolar);
			ini.SetValue("WeatherCloud", "SendAQI", WCloud.SendAirQuality);
			ini.SetValue("WeatherCloud", "SendSoilMoisture", WCloud.SendSoilMoisture);
			ini.SetValue("WeatherCloud", "SoilMoistureSensor", WCloud.SoilMoistureSensor);
			ini.SetValue("WeatherCloud", "SendLeafWetness", WCloud.SendLeafWetness);
			ini.SetValue("WeatherCloud", "LeafWetnessSensor", WCloud.LeafWetnessSensor);

			ini.SetValue("PWSweather", "ID", PWS.ID);
			ini.SetValue("PWSweather", "Password", Crypto.EncryptString(PWS.PW, Program.InstanceId, "PWS.PW"));
			ini.SetValue("PWSweather", "Enabled", PWS.Enabled);
			ini.SetValue("PWSweather", "Interval", PWS.Interval);
			ini.SetValue("PWSweather", "SendUV", PWS.SendUV);
			ini.SetValue("PWSweather", "SendSR", PWS.SendSolar);
			ini.SetValue("PWSweather", "CatchUp", PWS.CatchUp);

			ini.SetValue("WOW", "ID", WOW.ID);
			ini.SetValue("WOW", "Password", Crypto.EncryptString(WOW.PW, Program.InstanceId, "WOW.PW"));
			ini.SetValue("WOW", "Enabled", WOW.Enabled);
			ini.SetValue("WOW", "Interval", WOW.Interval);
			ini.SetValue("WOW", "SendUV", WOW.SendUV);
			ini.SetValue("WOW", "SendSR", WOW.SendSolar);
			ini.SetValue("WOW", "SendSoilTemp", WOW.SendSoilTemp);
			ini.SetValue("WOW", "SoilTempSensor", WOW.SoilTempSensor);
			ini.SetValue("WOW", "SendSoilMoist", WOW.SendSoilMoisture);
			ini.SetValue("WOW", "SoilMoistSensor", WOW.SoilMoistureSensor);
			ini.SetValue("WOW", "CatchUp", false);

			ini.SetValue("WOW-BE", "ID", WOW_BE.ID);
			ini.SetValue("WOW-BE", "Password", Crypto.EncryptString(WOW_BE.PW, Program.InstanceId, "WOW.PW"));
			ini.SetValue("WOW-BE", "Enabled", WOW_BE.Enabled);
			ini.SetValue("WOW-BE", "Interval", WOW_BE.Interval);
			ini.SetValue("WOW-BE", "SendUV", WOW_BE.SendUV);
			ini.SetValue("WOW-BE", "SendSR", WOW_BE.SendSolar);
			ini.SetValue("WOW-BE", "SendSoilTemp", WOW_BE.SendSoilTemp);
			ini.SetValue("WOW-BE", "SoilTempSensor", WOW_BE.SoilTempSensor);
			ini.SetValue("WOW-BE", "SendSoilMoist", WOW_BE.SendSoilMoisture);
			ini.SetValue("WOW-BE", "SoilMoistSensor", WOW_BE.SoilMoistureSensor);
			ini.SetValue("WOW-BE", "CatchUp", false);

			ini.SetValue("APRS", "ID", APRS.ID);
			ini.SetValue("APRS", "pass", APRS.PW == "-1" ? APRS.PW : Crypto.EncryptString(APRS.PW, Program.InstanceId, "APRS.PW"));
			ini.SetValue("APRS", "server", APRS.Server);
			ini.SetValue("APRS", "port", APRS.Port);
			ini.SetValue("APRS", "Enabled", APRS.Enabled);
			ini.SetValue("APRS", "Interval", APRS.Interval);
			ini.SetValue("APRS", "SendSR", APRS.SendSolar);
			ini.SetValue("APRS", "APRSHumidityCutoff", APRS.HumidityCutoff);
			ini.SetValue("APRS", "UseUtcInWxNowFile", APRS.UseUtcInWxNowFile);

			ini.SetValue("OpenWeatherMap", "Enabled", OpenWeatherMap.Enabled);
			ini.SetValue("OpenWeatherMap", "CatchUp", OpenWeatherMap.CatchUp);
			ini.SetValue("OpenWeatherMap", "APIkey", Crypto.EncryptString(OpenWeatherMap.PW, Program.InstanceId, "OpenWeatherMap.PW"));
			ini.SetValue("OpenWeatherMap", "StationId", OpenWeatherMap.ID);
			ini.SetValue("OpenWeatherMap", "Interval", OpenWeatherMap.Interval);

			ini.SetValue("WindGuru", "Enabled", WindGuru.Enabled);
			ini.SetValue("WindGuru", "StationUID", WindGuru.ID);
			ini.SetValue("WindGuru", "Password", Crypto.EncryptString(WindGuru.PW, Program.InstanceId, "WindGuru.PW"));
			ini.SetValue("WindGuru", "Interval", WindGuru.Interval);
			ini.SetValue("WindGuru", "SendRain", WindGuru.SendRain);

			ini.SetValue("Bluesky", "Enabled", Bluesky.Enabled);
			ini.SetValue("Bluesky", "ID", Bluesky.ID);
			ini.SetValue("Bluesky", "Password", Crypto.EncryptString(Bluesky.PW, Program.InstanceId, "Bluesky.PW"));
			ini.SetValue("Bluesky", "Interval", Bluesky.Interval);
			ini.SetValue("Bluesky", "Language", Bluesky.Language);
			ini.SetValue("Bluesky", "BaseUrl", Bluesky.BaseUrl);
			for (var i = 0; i < Bluesky.TimedPostsTime.Length; i++)
			{
				if (Bluesky.TimedPostsTime[i] < TimeSpan.MaxValue)
					ini.SetValue("Bluesky", "TimedPost" + i, Bluesky.TimedPostsTime[i].ToString(@"hh\:mm"));
				else
					ini.DeleteValue("Bluesky", "TimedPost" + i);
			}
			for (var i = 0; i < Bluesky.TimedPostsFile.Length; i++)
			{
				if (!string.IsNullOrEmpty(Bluesky.TimedPostsFile[i]))
					ini.SetValue("Bluesky", "TimedPostFile" + i, Bluesky.TimedPostsFile[i]);
				else
					ini.DeleteValue("Bluesky", "TimedPostFile" + i);
			}
			for (var i = 0; i < Bluesky.VariablePostsTime.Length; i++)
			{
				if (string.IsNullOrEmpty(Bluesky.VariablePostsTime[i]))
				{
					ini.DeleteValue("Bluesky", "VariablePost" + i);
					ini.DeleteValue("Bluesky", "VariablePostFile" + i);
				}
				else
				{
					ini.SetValue("Bluesky", "VariablePost" + i, Bluesky.VariablePostsTime[i]);
					ini.SetValue("Bluesky", "VariablePostFile" + i, Bluesky.VariablePostsFile[i]);
				}
			}

			ini.SetValue("MQTT", "Server", MQTT.Server);
			ini.SetValue("MQTT", "Port", MQTT.Port);
			ini.SetValue("MQTT", "Username", Crypto.EncryptString(MQTT.Username, Program.InstanceId, "MQTT.Username,"));
			ini.SetValue("MQTT", "Password", Crypto.EncryptString(MQTT.Password, Program.InstanceId, "MQTT.Password"));
			ini.SetValue("MQTT", "UseTLS", MQTT.UseTLS);
			ini.SetValue("MQTT", "IPversion", MQTT.IpVersion);
			ini.SetValue("MQTT", "ProtocolVersion", MQTT.ProtocolVersion);
			ini.SetValue("MQTT", "EnableDataUpdate", MQTT.EnableDataUpdate);
			ini.SetValue("MQTT", "UpdateTemplate", MQTT.UpdateTemplate);
			ini.SetValue("MQTT", "EnableInterval", MQTT.EnableInterval);
			ini.SetValue("MQTT", "IntervalTemplate", MQTT.IntervalTemplate);

			ini.SetValue("Alarms", "alarmlowtemp", LowTempAlarm.Value);
			ini.SetValue("Alarms", "LowTempAlarmSet", LowTempAlarm.Enabled);
			ini.SetValue("Alarms", "LowTempAlarmSound", LowTempAlarm.Sound);
			ini.SetValue("Alarms", "LowTempAlarmSoundFile", LowTempAlarm.SoundFile);
			ini.SetValue("Alarms", "LowTempAlarmNotify", LowTempAlarm.Notify);
			ini.SetValue("Alarms", "LowTempAlarmEmail", LowTempAlarm.Email);
			ini.SetValue("Alarms", "LowTempAlarmLatch", LowTempAlarm.Latch);
			ini.SetValue("Alarms", "LowTempAlarmLatchHours", LowTempAlarm.LatchHours);
			ini.SetValue("Alarms", "LowTempAlarmAction", LowTempAlarm.Action);
			ini.SetValue("Alarms", "LowTempAlarmActionParams", LowTempAlarm.ActionParams);
			ini.SetValue("Alarms", "LowTempAlarmBlueskyFile", LowTempAlarm.BskyFile);


			ini.SetValue("Alarms", "alarmhightemp", HighTempAlarm.Value);
			ini.SetValue("Alarms", "HighTempAlarmSet", HighTempAlarm.Enabled);
			ini.SetValue("Alarms", "HighTempAlarmSound", HighTempAlarm.Sound);
			ini.SetValue("Alarms", "HighTempAlarmSoundFile", HighTempAlarm.SoundFile);
			ini.SetValue("Alarms", "HighTempAlarmNotify", HighTempAlarm.Notify);
			ini.SetValue("Alarms", "HighTempAlarmEmail", HighTempAlarm.Email);
			ini.SetValue("Alarms", "HighTempAlarmLatch", HighTempAlarm.Latch);
			ini.SetValue("Alarms", "HighTempAlarmLatchHours", HighTempAlarm.LatchHours);
			ini.SetValue("Alarms", "HighTempAlarmAction", HighTempAlarm.Action);
			ini.SetValue("Alarms", "HighTempAlarmActionParams", HighTempAlarm.ActionParams);
			ini.SetValue("Alarms", "HighTempAlarmBlueskyFile", HighTempAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmtempchange", TempChangeAlarm.Value);
			ini.SetValue("Alarms", "TempChangeAlarmSet", TempChangeAlarm.Enabled);
			ini.SetValue("Alarms", "TempChangeAlarmSound", TempChangeAlarm.Sound);
			ini.SetValue("Alarms", "TempChangeAlarmSoundFile", TempChangeAlarm.SoundFile);
			ini.SetValue("Alarms", "TempChangeAlarmNotify", TempChangeAlarm.Notify);
			ini.SetValue("Alarms", "TempChangeAlarmEmail", TempChangeAlarm.Email);
			ini.SetValue("Alarms", "TempChangeAlarmLatch", TempChangeAlarm.Latch);
			ini.SetValue("Alarms", "TempChangeAlarmLatchHours", TempChangeAlarm.LatchHours);
			ini.SetValue("Alarms", "TempChangeAlarmAction", TempChangeAlarm.Action);
			ini.SetValue("Alarms", "TempChangeAlarmActionParams", TempChangeAlarm.ActionParams);
			ini.SetValue("Alarms", "TempChangeAlarmBlueskyFile", TempChangeAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmlowpress", LowPressAlarm.Value);
			ini.SetValue("Alarms", "LowPressAlarmSet", LowPressAlarm.Enabled);
			ini.SetValue("Alarms", "LowPressAlarmSound", LowPressAlarm.Sound);
			ini.SetValue("Alarms", "LowPressAlarmSoundFile", LowPressAlarm.SoundFile);
			ini.SetValue("Alarms", "LowPressAlarmNotify", LowPressAlarm.Notify);
			ini.SetValue("Alarms", "LowPressAlarmEmail", LowPressAlarm.Email);
			ini.SetValue("Alarms", "LowPressAlarmLatch", LowPressAlarm.Latch);
			ini.SetValue("Alarms", "LowPressAlarmLatchHours", LowPressAlarm.LatchHours);
			ini.SetValue("Alarms", "LowPressAlarmAction", LowPressAlarm.Action);
			ini.SetValue("Alarms", "LowPressAlarmActionParams", LowPressAlarm.ActionParams);
			ini.SetValue("Alarms", "LowPressAlarmBlueskyFile", LowPressAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmhighpress", HighPressAlarm.Value);
			ini.SetValue("Alarms", "HighPressAlarmSet", HighPressAlarm.Enabled);
			ini.SetValue("Alarms", "HighPressAlarmSound", HighPressAlarm.Sound);
			ini.SetValue("Alarms", "HighPressAlarmSoundFile", HighPressAlarm.SoundFile);
			ini.SetValue("Alarms", "HighPressAlarmNotify", HighPressAlarm.Notify);
			ini.SetValue("Alarms", "HighPressAlarmEmail", HighPressAlarm.Email);
			ini.SetValue("Alarms", "HighPressAlarmLatch", HighPressAlarm.Latch);
			ini.SetValue("Alarms", "HighPressAlarmLatchHours", HighPressAlarm.LatchHours);
			ini.SetValue("Alarms", "HighPressAlarmAction", HighPressAlarm.Action);
			ini.SetValue("Alarms", "HighPressAlarmActionParams", HighPressAlarm.ActionParams);
			ini.SetValue("Alarms", "HighPressAlarmBlueskyFile", HighPressAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmpresschange", PressChangeAlarm.Value);
			ini.SetValue("Alarms", "PressChangeAlarmSet", PressChangeAlarm.Enabled);
			ini.SetValue("Alarms", "PressChangeAlarmSound", PressChangeAlarm.Sound);
			ini.SetValue("Alarms", "PressChangeAlarmSoundFile", PressChangeAlarm.SoundFile);
			ini.SetValue("Alarms", "PressChangeAlarmNotify", PressChangeAlarm.Notify);
			ini.SetValue("Alarms", "PressChangeAlarmEmail", PressChangeAlarm.Email);
			ini.SetValue("Alarms", "PressChangeAlarmLatch", PressChangeAlarm.Latch);
			ini.SetValue("Alarms", "PressChangeAlarmLatchHours", PressChangeAlarm.LatchHours);
			ini.SetValue("Alarms", "PressChangeAlarmAction", PressChangeAlarm.Action);
			ini.SetValue("Alarms", "PressChangeAlarmActionParams", PressChangeAlarm.ActionParams);
			ini.SetValue("Alarms", "PressChangeAlarmBlueskyFile", PressChangeAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmhighraintoday", HighRainTodayAlarm.Value);
			ini.SetValue("Alarms", "HighRainTodayAlarmSet", HighRainTodayAlarm.Enabled);
			ini.SetValue("Alarms", "HighRainTodayAlarmSound", HighRainTodayAlarm.Sound);
			ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", HighRainTodayAlarm.SoundFile);
			ini.SetValue("Alarms", "HighRainTodayAlarmNotify", HighRainTodayAlarm.Notify);
			ini.SetValue("Alarms", "HighRainTodayAlarmEmail", HighRainTodayAlarm.Email);
			ini.SetValue("Alarms", "HighRainTodayAlarmLatch", HighRainTodayAlarm.Latch);
			ini.SetValue("Alarms", "HighRainTodayAlarmLatchHours", HighRainTodayAlarm.LatchHours);
			ini.SetValue("Alarms", "HighRainTodayAlarmAction", HighRainTodayAlarm.Action);
			ini.SetValue("Alarms", "HighRainTodayAlarmActionParams", HighRainTodayAlarm.ActionParams);

			ini.SetValue("Alarms", "alarmhighrainrate", HighRainRateAlarm.Value);
			ini.SetValue("Alarms", "HighRainRateAlarmSet", HighRainRateAlarm.Enabled);
			ini.SetValue("Alarms", "HighRainRateAlarmSound", HighRainRateAlarm.Sound);
			ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", HighRainRateAlarm.SoundFile);
			ini.SetValue("Alarms", "HighRainRateAlarmNotify", HighRainRateAlarm.Notify);
			ini.SetValue("Alarms", "HighRainRateAlarmEmail", HighRainRateAlarm.Email);
			ini.SetValue("Alarms", "HighRainRateAlarmLatch", HighRainRateAlarm.Latch);
			ini.SetValue("Alarms", "HighRainRateAlarmLatchHours", HighRainRateAlarm.LatchHours);
			ini.SetValue("Alarms", "HighRainRateAlarmAction", HighRainRateAlarm.Action);
			ini.SetValue("Alarms", "HighRainRateAlarmActionParams", HighRainRateAlarm.ActionParams);
			ini.SetValue("Alarms", "HighRainRateAlarmBlueskyFile", HighRainRateAlarm.BskyFile);

			ini.SetValue("Alarms", "IsRainingAlarmSet", IsRainingAlarm.Enabled);
			ini.SetValue("Alarms", "IsRainingAlarmSound", IsRainingAlarm.Sound);
			ini.SetValue("Alarms", "IsRainingAlarmSoundFile", IsRainingAlarm.SoundFile);
			ini.SetValue("Alarms", "IsRainingAlarmNotify", IsRainingAlarm.Notify);
			ini.SetValue("Alarms", "IsRainingAlarmEmail", IsRainingAlarm.Email);
			ini.SetValue("Alarms", "IsRainingAlarmLatch", IsRainingAlarm.Latch);
			ini.SetValue("Alarms", "IsRainingAlarmLatchHours", IsRainingAlarm.LatchHours);
			ini.SetValue("Alarms", "IsRainingAlarmTriggerCount", IsRainingAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "IsRainingAlarmAction", IsRainingAlarm.Action);
			ini.SetValue("Alarms", "IsRainingAlarmActionParams", IsRainingAlarm.ActionParams);
			ini.SetValue("Alarms", "IsRainingAlarmBlueskyFile", IsRainingAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmhighgust", HighGustAlarm.Value);
			ini.SetValue("Alarms", "HighGustAlarmSet", HighGustAlarm.Enabled);
			ini.SetValue("Alarms", "HighGustAlarmSound", HighGustAlarm.Sound);
			ini.SetValue("Alarms", "HighGustAlarmSoundFile", HighGustAlarm.SoundFile);
			ini.SetValue("Alarms", "HighGustAlarmNotify", HighGustAlarm.Notify);
			ini.SetValue("Alarms", "HighGustAlarmEmail", HighGustAlarm.Email);
			ini.SetValue("Alarms", "HighGustAlarmLatch", HighGustAlarm.Latch);
			ini.SetValue("Alarms", "HighGustAlarmLatchHours", HighGustAlarm.LatchHours);
			ini.SetValue("Alarms", "HighGustAlarmAction", HighGustAlarm.Action);
			ini.SetValue("Alarms", "HighGustAlarmActionParams", HighGustAlarm.ActionParams);
			ini.SetValue("Alarms", "HighGustAlarmBlueskyFile", HighGustAlarm.BskyFile);

			ini.SetValue("Alarms", "alarmhighwind", HighWindAlarm.Value);
			ini.SetValue("Alarms", "HighWindAlarmSet", HighWindAlarm.Enabled);
			ini.SetValue("Alarms", "HighWindAlarmSound", HighWindAlarm.Sound);
			ini.SetValue("Alarms", "HighWindAlarmSoundFile", HighWindAlarm.SoundFile);
			ini.SetValue("Alarms", "HighWindAlarmNotify", HighWindAlarm.Notify);
			ini.SetValue("Alarms", "HighWindAlarmEmail", HighWindAlarm.Email);
			ini.SetValue("Alarms", "HighWindAlarmLatch", HighWindAlarm.Latch);
			ini.SetValue("Alarms", "HighWindAlarmLatchHours", HighWindAlarm.LatchHours);
			ini.SetValue("Alarms", "HighWindAlarmAction", HighWindAlarm.Action);
			ini.SetValue("Alarms", "HighWindAlarmActionParams", HighWindAlarm.ActionParams);
			ini.SetValue("Alarms", "HighWindAlarmBlueskyFile", HighWindAlarm.BskyFile);

			ini.SetValue("Alarms", "SensorAlarmSet", SensorAlarm.Enabled);
			ini.SetValue("Alarms", "SensorAlarmSound", SensorAlarm.Sound);
			ini.SetValue("Alarms", "SensorAlarmSoundFile", SensorAlarm.SoundFile);
			ini.SetValue("Alarms", "SensorAlarmNotify", SensorAlarm.Notify);
			ini.SetValue("Alarms", "SensorAlarmEmail", SensorAlarm.Email);
			ini.SetValue("Alarms", "SensorAlarmLatch", SensorAlarm.Latch);
			ini.SetValue("Alarms", "SensorAlarmLatchHours", SensorAlarm.LatchHours);
			ini.SetValue("Alarms", "SensorAlarmTriggerCount", SensorAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "SensorAlarmAction", SensorAlarm.Action);
			ini.SetValue("Alarms", "SensorAlarmActionParams", SensorAlarm.ActionParams);
			ini.SetValue("Alarms", "SensorAlarmBlueskyFile", SensorAlarm.BskyFile);

			ini.SetValue("Alarms", "DataStoppedAlarmSet", DataStoppedAlarm.Enabled);
			ini.SetValue("Alarms", "DataStoppedAlarmSound", DataStoppedAlarm.Sound);
			ini.SetValue("Alarms", "DataStoppedAlarmSoundFile", DataStoppedAlarm.SoundFile);
			ini.SetValue("Alarms", "DataStoppedAlarmNotify", DataStoppedAlarm.Notify);
			ini.SetValue("Alarms", "DataStoppedAlarmEmail", DataStoppedAlarm.Email);
			ini.SetValue("Alarms", "DataStoppedAlarmLatch", DataStoppedAlarm.Latch);
			ini.SetValue("Alarms", "DataStoppedAlarmLatchHours", DataStoppedAlarm.LatchHours);
			ini.SetValue("Alarms", "DataStoppedAlarmTriggerCount", DataStoppedAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "DataStoppedAlarmAction", DataStoppedAlarm.Action);
			ini.SetValue("Alarms", "DataStoppedAlarmActionParams", DataStoppedAlarm.ActionParams);
			ini.SetValue("Alarms", "DataStoppedAlarmBlueskyFile", DataStoppedAlarm.BskyFile);

			ini.SetValue("Alarms", "BatteryLowAlarmSet", BatteryLowAlarm.Enabled);
			ini.SetValue("Alarms", "BatteryLowAlarmSound", BatteryLowAlarm.Sound);
			ini.SetValue("Alarms", "BatteryLowAlarmSoundFile", BatteryLowAlarm.SoundFile);
			ini.SetValue("Alarms", "BatteryLowAlarmNotify", BatteryLowAlarm.Notify);
			ini.SetValue("Alarms", "BatteryLowAlarmEmail", BatteryLowAlarm.Email);
			ini.SetValue("Alarms", "BatteryLowAlarmLatch", BatteryLowAlarm.Latch);
			ini.SetValue("Alarms", "BatteryLowAlarmLatchHours", BatteryLowAlarm.LatchHours);
			ini.SetValue("Alarms", "BatteryLowAlarmTriggerCount", BatteryLowAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "BatteryLowAlarmAction", BatteryLowAlarm.Action);
			ini.SetValue("Alarms", "BatteryLowAlarmActionParams", BatteryLowAlarm.ActionParams);
			ini.SetValue("Alarms", "BatteryLowAlarmBlueskyFile", BatteryLowAlarm.BskyFile);

			ini.SetValue("Alarms", "DataSpikeAlarmSet", SpikeAlarm.Enabled);
			ini.SetValue("Alarms", "DataSpikeAlarmSound", SpikeAlarm.Sound);
			ini.SetValue("Alarms", "DataSpikeAlarmSoundFile", SpikeAlarm.SoundFile);
			ini.SetValue("Alarms", "DataSpikeAlarmNotify", SpikeAlarm.Notify);
			ini.SetValue("Alarms", "DataSpikeAlarmEmail", SpikeAlarm.Email);
			ini.SetValue("Alarms", "DataSpikeAlarmLatch", SpikeAlarm.Latch);
			ini.SetValue("Alarms", "DataSpikeAlarmLatchHours", SpikeAlarm.LatchHours);
			ini.SetValue("Alarms", "DataSpikeAlarmTriggerCount", SpikeAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "DataSpikeAlarmAction", SpikeAlarm.Action);
			ini.SetValue("Alarms", "DataSpikeAlarmActionParams", SpikeAlarm.ActionParams);
			ini.SetValue("Alarms", "DataSpikeAlarmBlueskyFile", SpikeAlarm.BskyFile);

			ini.SetValue("Alarms", "UpgradeAlarmSet", UpgradeAlarm.Enabled);
			ini.SetValue("Alarms", "UpgradeAlarmSound", UpgradeAlarm.Sound);
			ini.SetValue("Alarms", "UpgradeAlarmSoundFile", UpgradeAlarm.SoundFile);
			ini.SetValue("Alarms", "UpgradeAlarmNotify", UpgradeAlarm.Notify);
			ini.SetValue("Alarms", "UpgradeAlarmEmail", UpgradeAlarm.Email);
			ini.SetValue("Alarms", "UpgradeAlarmLatch", UpgradeAlarm.Latch);
			ini.SetValue("Alarms", "UpgradeAlarmLatchHours", UpgradeAlarm.LatchHours);
			ini.SetValue("Alarms", "UpgradeAlarmAction", UpgradeAlarm.Action);
			ini.SetValue("Alarms", "UpgradeAlarmActionParams", UpgradeAlarm.ActionParams);
			ini.SetValue("Alarms", "UpgradeAlarmBlueskyFile", UpgradeAlarm.BskyFile);

			ini.SetValue("Alarms", "FirmwareAlarmSet", FirmwareAlarm.Enabled);
			ini.SetValue("Alarms", "FirmwareAlarmSound", FirmwareAlarm.Sound);
			ini.SetValue("Alarms", "FirmwareAlarmSoundFile", FirmwareAlarm.SoundFile);
			ini.SetValue("Alarms", "FirmwareAlarmNotify", FirmwareAlarm.Notify);
			ini.SetValue("Alarms", "FirmwareAlarmEmail", FirmwareAlarm.Email);
			ini.SetValue("Alarms", "FirmwareAlarmLatch", FirmwareAlarm.Latch);
			ini.SetValue("Alarms", "FirmwareAlarmLatchHours", FirmwareAlarm.LatchHours);
			ini.SetValue("Alarms", "FirmwareAlarmAction", FirmwareAlarm.Action);
			ini.SetValue("Alarms", "FirmwareAlarmActionParams", FirmwareAlarm.ActionParams);
			ini.SetValue("Alarms", "FirmwareAlarmBlueskyFile", FirmwareAlarm.BskyFile);

			ini.SetValue("Alarms", "HttpUploadAlarmSet", ThirdPartyAlarm.Enabled);
			ini.SetValue("Alarms", "HttpUploadAlarmSound", ThirdPartyAlarm.Sound);
			ini.SetValue("Alarms", "HttpUploadAlarmSoundFile", ThirdPartyAlarm.SoundFile);
			ini.SetValue("Alarms", "HttpUploadAlarmNotify", ThirdPartyAlarm.Notify);
			ini.SetValue("Alarms", "HttpUploadAlarmEmail", ThirdPartyAlarm.Email);
			ini.SetValue("Alarms", "HttpUploadAlarmLatch", ThirdPartyAlarm.Latch);
			ini.SetValue("Alarms", "HttpUploadAlarmLatchHours", ThirdPartyAlarm.LatchHours);
			ini.SetValue("Alarms", "HttpUploadAlarmTriggerCount", ThirdPartyAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "HttpUploadAlarmAction", ThirdPartyAlarm.Action);
			ini.SetValue("Alarms", "HttpUploadAlarmActionParams", ThirdPartyAlarm.ActionParams);
			ini.SetValue("Alarms", "HttpUploadAlarmBlueskyFile", ThirdPartyAlarm.BskyFile);

			ini.SetValue("Alarms", "MySqlUploadAlarmSet", MySqlUploadAlarm.Enabled);
			ini.SetValue("Alarms", "MySqlUploadAlarmSound", MySqlUploadAlarm.Sound);
			ini.SetValue("Alarms", "MySqlUploadAlarmSoundFile", MySqlUploadAlarm.SoundFile);
			ini.SetValue("Alarms", "MySqlUploadAlarmNotify", MySqlUploadAlarm.Notify);
			ini.SetValue("Alarms", "MySqlUploadAlarmEmail", MySqlUploadAlarm.Email);
			ini.SetValue("Alarms", "MySqlUploadAlarmLatch", MySqlUploadAlarm.Latch);
			ini.SetValue("Alarms", "MySqlUploadAlarmLatchHours", MySqlUploadAlarm.LatchHours);
			ini.SetValue("Alarms", "MySqlUploadAlarmTriggerCount", MySqlUploadAlarm.TriggerThreshold);
			ini.SetValue("Alarms", "MySqlUploadAlarmAction", MySqlUploadAlarm.Action);
			ini.SetValue("Alarms", "MySqlUploadAlarmActionParams", MySqlUploadAlarm.ActionParams);
			ini.SetValue("Alarms", "MySqlUploadAlarmBlueskyFile", MySqlUploadAlarm.BskyFile);

			ini.SetValue("Alarms", "NewRecordAlarmSet", NewRecordAlarm.Enabled);
			ini.SetValue("Alarms", "NewRecordAlarmSound", NewRecordAlarm.Sound);
			ini.SetValue("Alarms", "NewRecordAlarmSoundFile", NewRecordAlarm.SoundFile);
			ini.SetValue("Alarms", "NewRecordAlarmNotify", NewRecordAlarm.Notify);
			ini.SetValue("Alarms", "NewRecordAlarmEmail", NewRecordAlarm.Email);
			ini.SetValue("Alarms", "NewRecordAlarmLatch", NewRecordAlarm.Latch);
			ini.SetValue("Alarms", "NewRecordAlarmLatchHours", NewRecordAlarm.LatchHours);
			ini.SetValue("Alarms", "NewRecordAlarmAction", NewRecordAlarm.Action);
			ini.SetValue("Alarms", "NewRecordAlarmActionParams", NewRecordAlarm.ActionParams);
			ini.SetValue("Alarms", "NewRecordAlarmBlueskyFile", NewRecordAlarm.BskyFile);

			ini.SetValue("Alarms", "FtpAlarmSet", FtpAlarm.Enabled);
			ini.SetValue("Alarms", "FtpAlarmSound", FtpAlarm.Sound);
			ini.SetValue("Alarms", "FtpAlarmSoundFile", FtpAlarm.SoundFile);
			ini.SetValue("Alarms", "FtpAlarmNotify", FtpAlarm.Notify);
			ini.SetValue("Alarms", "FtpAlarmEmail", FtpAlarm.Email);
			ini.SetValue("Alarms", "FtpAlarmLatch", FtpAlarm.Latch);
			ini.SetValue("Alarms", "FtpAlarmLatchHours", FtpAlarm.LatchHours);
			ini.SetValue("Alarms", "FtpAlarmAction", FtpAlarm.Action);
			ini.SetValue("Alarms", "FtpAlarmActionParams", FtpAlarm.ActionParams);
			ini.SetValue("Alarms", "FtpAlarmBlueskyFile", FtpAlarm.BskyFile);

			ini.SetValue("Alarms", "ErrorAlarmSet", ErrorAlarm.Enabled);
			ini.SetValue("Alarms", "ErrorAlarmSound", ErrorAlarm.Sound);
			ini.SetValue("Alarms", "ErrorAlarmSoundFile", ErrorAlarm.SoundFile);
			ini.SetValue("Alarms", "ErrorAlarmNotify", ErrorAlarm.Notify);
			ini.SetValue("Alarms", "ErrorAlarmEmail", ErrorAlarm.Email);
			ini.SetValue("Alarms", "ErrorAlarmAction", ErrorAlarm.Action);
			ini.SetValue("Alarms", "ErrorAlarmActionParams", ErrorAlarm.ActionParams);
			ini.SetValue("Alarms", "ErrorAlarmActionWindow", ErrorAlarm.ShowWindow);
			ini.SetValue("Alarms", "ErrorAlarmBlueskyFile", ErrorAlarm.BskyFile);

			ini.SetValue("Alarms", "FromEmail", AlarmFromEmail);
			ini.SetValue("Alarms", "DestEmail", string.Join(";", AlarmDestEmail));
			ini.SetValue("Alarms", "UseHTML", AlarmEmailHtml);
			ini.SetValue("Alarms", "UseBCC", AlarmEmailUseBcc);

			// User Alarms
			for (var i = 0; i < UserAlarms.Count; i++)
			{
				ini.SetValue("UserAlarms", "AlarmName" + i, UserAlarms[i].Name);
				ini.SetValue("UserAlarms", "AlarmTag" + i, UserAlarms[i].WebTag);
				ini.SetValue("UserAlarms", "AlarmType" + i, UserAlarms[i].Type);
				ini.SetValue("UserAlarms", "AlarmValue" + i, UserAlarms[i].Value);
				ini.SetValue("UserAlarms", "AlarmEnabled" + i, UserAlarms[i].Enabled);
				ini.SetValue("UserAlarms", "AlarmEmail" + i, UserAlarms[i].Email);
				ini.SetValue("UserAlarms", "AlarmEmailMsg" + i, UserAlarms[i].EmailMsg);
				ini.SetValue("UserAlarms", "AlarmBlueskyFile" + i, UserAlarms[i].BskyFile);
				ini.SetValue("UserAlarms", "AlarmLatch" + i, UserAlarms[i].Latch);
				ini.SetValue("UserAlarms", "AlarmLatchHours" + i, UserAlarms[i].LatchHours);
				ini.SetValue("UserAlarms", "AlarmAction" + i, UserAlarms[i].Action);
				ini.SetValue("UserAlarms", "AlarmActionParams" + i, UserAlarms[i].ActionParams);
			}
			// remove any old alarms
			for (var i = UserAlarms.Count; i < 10; i++)
			{
				ini.DeleteValue("UserAlarms", "AlarmName" + i);
				ini.DeleteValue("UserAlarms", "AlarmTag" + i);
				ini.DeleteValue("UserAlarms", "AlarmType" + i);
				ini.DeleteValue("UserAlarms", "AlarmValue" + i);
				ini.DeleteValue("UserAlarms", "AlarmEnabled" + i);
				ini.DeleteValue("UserAlarms", "AlarmEmail" + i);
				ini.DeleteValue("UserAlarms", "AlarmEmailMsg" + i);
				ini.DeleteValue("UserAlarms", "AlarmLatch" + i);
				ini.DeleteValue("UserAlarms", "AlarmLatchHours" + i);
				ini.DeleteValue("UserAlarms", "AlarmAction" + i);
				ini.DeleteValue("UserAlarms", "AlarmActionParams" + i);
			}

			ini.SetValue("Offsets", "PressOffset", Calib.Press.Offset);
			ini.SetValue("Offsets", "PressStnOffset", Calib.PressStn.Offset);
			ini.SetValue("Offsets", "TempOffset", Calib.Temp.Offset);
			ini.SetValue("Offsets", "HumOffset", Calib.Hum.Offset);
			ini.SetValue("Offsets", "WindDirOffset", Calib.WindDir.Offset);
			ini.SetValue("Offsets", "UVOffset", Calib.UV.Offset);
			ini.SetValue("Offsets", "SolarOffset", Calib.Solar.Offset);
			ini.SetValue("Offsets", "WetBulbOffset", Calib.WetBulb.Offset);
			ini.SetValue("Offsets", "InTempOffset", Calib.InTemp.Offset);
			ini.SetValue("Offsets", "InHumOffset", Calib.InHum.Offset);

			ini.SetValue("Offsets", "PressMult", Calib.Press.Mult);
			ini.SetValue("Offsets", "PressStnMult", Calib.PressStn.Mult);
			ini.SetValue("Offsets", "WindSpeedMult", Calib.WindSpeed.Mult);
			ini.SetValue("Offsets", "WindGustMult", Calib.WindGust.Mult);
			ini.SetValue("Offsets", "TempMult", Calib.Temp.Mult);
			ini.SetValue("Offsets", "HumMult", Calib.Hum.Mult);
			ini.SetValue("Offsets", "RainMult", Calib.Rain.Mult);
			ini.SetValue("Offsets", "SolarMult", Calib.Solar.Mult);
			ini.SetValue("Offsets", "UVMult", Calib.UV.Mult);
			ini.SetValue("Offsets", "WetBulbMult", Calib.WetBulb.Mult);
			ini.SetValue("Offsets", "InTempMult", Calib.InTemp.Mult);
			ini.SetValue("Offsets", "InHumMult", Calib.InHum.Mult);

			ini.SetValue("Offsets", "PressMult2", Calib.Press.Mult2);
			ini.SetValue("Offsets", "PressStnMult2", Calib.PressStn.Mult2);
			ini.SetValue("Offsets", "WindSpeedMult2", Calib.WindSpeed.Mult2);
			ini.SetValue("Offsets", "WindGustMult2", Calib.WindGust.Mult2);
			ini.SetValue("Offsets", "TempMult2", Calib.Temp.Mult2);
			ini.SetValue("Offsets", "HumMult2", Calib.Hum.Mult2);
			ini.SetValue("Offsets", "InTempMult2", Calib.InTemp.Mult2);
			ini.SetValue("Offsets", "InHumMult2", Calib.InHum.Mult2);
			ini.SetValue("Offsets", "SolarMult2", Calib.Solar.Mult2);
			ini.SetValue("Offsets", "UVMult2", Calib.UV.Mult2);

			ini.SetValue("Limits", "TempHighC", ConvertUnits.UserTempToC(Limit.TempHigh));
			ini.SetValue("Limits", "TempLowC", ConvertUnits.UserTempToC(Limit.TempLow));
			ini.SetValue("Limits", "DewHighC", ConvertUnits.UserTempToC(Limit.DewHigh));
			ini.SetValue("Limits", "PressHighMB", ConvertUnits.UserPressToMB(Limit.PressHigh));
			ini.SetValue("Limits", "PressLowMB", ConvertUnits.UserPressToMB(Limit.PressLow));
			ini.SetValue("Limits", "WindHighMS", ConvertUnits.UserWindToMS(Limit.WindHigh));

			ini.SetValue("xAP", "Enabled", xapEnabled);
			ini.SetValue("xAP", "UID", xapUID);
			ini.SetValue("xAP", "Port", xapPort);

			ini.SetValue("Solar", "SunThreshold", SolarOptions.SunThreshold);
			ini.SetValue("Solar", "SolarMinimum", SolarOptions.SolarMinimum);
			ini.SetValue("Solar", "UseBlakeLarsen", SolarOptions.UseBlakeLarsen);
			ini.SetValue("Solar", "SolarCalc", SolarOptions.SolarCalc);
			ini.SetValue("Solar", "LuxToWM2", SolarOptions.LuxToWM2);
			ini.SetValue("Solar", "RStransfactorJun", SolarOptions.RStransfactorJun);
			ini.SetValue("Solar", "RStransfactorDec", SolarOptions.RStransfactorDec);
			ini.SetValue("Solar", "BrasTurbidityJun", SolarOptions.BrasTurbidityJun);
			ini.SetValue("Solar", "BrasTurbidityDec", SolarOptions.BrasTurbidityDec);

			ini.SetValue("NOAA", "Name", NOAAconf.Name);
			ini.SetValue("NOAA", "City", NOAAconf.City);
			ini.SetValue("NOAA", "State", NOAAconf.State);
			ini.SetValue("NOAA", "12hourformat", NOAAconf.Use12hour);
			ini.SetValue("NOAA", "HeatingThreshold", NOAAconf.HeatThreshold);
			ini.SetValue("NOAA", "CoolingThreshold", NOAAconf.CoolThreshold);
			ini.SetValue("NOAA", "MaxTempComp1", NOAAconf.MaxTempComp1);
			ini.SetValue("NOAA", "MaxTempComp2", NOAAconf.MaxTempComp2);
			ini.SetValue("NOAA", "MinTempComp1", NOAAconf.MinTempComp1);
			ini.SetValue("NOAA", "MinTempComp2", NOAAconf.MinTempComp2);
			ini.SetValue("NOAA", "RainComp1", NOAAconf.RainComp1);
			ini.SetValue("NOAA", "RainComp2", NOAAconf.RainComp2);
			ini.SetValue("NOAA", "RainComp3", NOAAconf.RainComp3);
			ini.SetValue("NOAA", "AutoSave", NOAAconf.Create);
			ini.SetValue("NOAA", "AutoFTP", NOAAconf.AutoFtp);
			ini.SetValue("NOAA", "FTPDirectory", NOAAconf.FtpFolder);
			ini.SetValue("NOAA", "AutoCopy", NOAAconf.AutoCopy);
			ini.SetValue("NOAA", "CopyDirectory", NOAAconf.CopyFolder);
			ini.SetValue("NOAA", "MonthFileFormat", NOAAconf.MonthFile);
			ini.SetValue("NOAA", "YearFileFormat", NOAAconf.YearFile);
			ini.SetValue("NOAA", "NOAAUseUTF8", NOAAconf.UseUtf8);
			ini.SetValue("NOAA", "NOAAUOutputText", NOAAconf.OutputText);
			ini.SetValue("NOAA", "UseDotDecimal", NOAAconf.UseDotDecimal);
			ini.SetValue("NOAA", "UseNoaaHeatCoolDays", NOAAconf.UseNoaaHeatCoolDays);
			ini.SetValue("NOAA", "UseMinMaxAvg", NOAAconf.UseMinMaxAvg);

			ini.SetValue("NOAA", "NOAATempNormJan", NOAAconf.TempNorms[1]);
			ini.SetValue("NOAA", "NOAATempNormFeb", NOAAconf.TempNorms[2]);
			ini.SetValue("NOAA", "NOAATempNormMar", NOAAconf.TempNorms[3]);
			ini.SetValue("NOAA", "NOAATempNormApr", NOAAconf.TempNorms[4]);
			ini.SetValue("NOAA", "NOAATempNormMay", NOAAconf.TempNorms[5]);
			ini.SetValue("NOAA", "NOAATempNormJun", NOAAconf.TempNorms[6]);
			ini.SetValue("NOAA", "NOAATempNormJul", NOAAconf.TempNorms[7]);
			ini.SetValue("NOAA", "NOAATempNormAug", NOAAconf.TempNorms[8]);
			ini.SetValue("NOAA", "NOAATempNormSep", NOAAconf.TempNorms[9]);
			ini.SetValue("NOAA", "NOAATempNormOct", NOAAconf.TempNorms[10]);
			ini.SetValue("NOAA", "NOAATempNormNov", NOAAconf.TempNorms[11]);
			ini.SetValue("NOAA", "NOAATempNormDec", NOAAconf.TempNorms[12]);

			ini.SetValue("NOAA", "NOAARainNormJan", NOAAconf.RainNorms[1]);
			ini.SetValue("NOAA", "NOAARainNormFeb", NOAAconf.RainNorms[2]);
			ini.SetValue("NOAA", "NOAARainNormMar", NOAAconf.RainNorms[3]);
			ini.SetValue("NOAA", "NOAARainNormApr", NOAAconf.RainNorms[4]);
			ini.SetValue("NOAA", "NOAARainNormMay", NOAAconf.RainNorms[5]);
			ini.SetValue("NOAA", "NOAARainNormJun", NOAAconf.RainNorms[6]);
			ini.SetValue("NOAA", "NOAARainNormJul", NOAAconf.RainNorms[7]);
			ini.SetValue("NOAA", "NOAARainNormAug", NOAAconf.RainNorms[8]);
			ini.SetValue("NOAA", "NOAARainNormSep", NOAAconf.RainNorms[9]);
			ini.SetValue("NOAA", "NOAARainNormOct", NOAAconf.RainNorms[10]);
			ini.SetValue("NOAA", "NOAARainNormNov", NOAAconf.RainNorms[11]);
			ini.SetValue("NOAA", "NOAARainNormDec", NOAAconf.RainNorms[12]);

			ini.SetValue("Proxies", "HTTPProxyName", HTTPProxyName);
			ini.SetValue("Proxies", "HTTPProxyPort", HTTPProxyPort);
			ini.SetValue("Proxies", "HTTPProxyUser", Crypto.EncryptString(HTTPProxyUser, Program.InstanceId, "HTTPProxyUser"));
			ini.SetValue("Proxies", "HTTPProxyPassword", Crypto.EncryptString(HTTPProxyPassword, Program.InstanceId, "HTTPProxyPassword"));

			ini.SetValue("Display", "NumWindRosePoints", NumWindRosePoints);
			ini.SetValue("Display", "UseApparent", DisplayOptions.UseApparent);
			ini.SetValue("Display", "DisplaySolarData", DisplayOptions.ShowSolar);
			ini.SetValue("Display", "DisplayUvData", DisplayOptions.ShowUV);
			ini.SetValue("Display", "DisplaySnowData", DisplayOptions.ShowSnow);

			ini.SetValue("Graphs", "ChartMaxDays", GraphDays);
			ini.SetValue("Graphs", "GraphHours", GraphHours);
			ini.SetValue("Graphs", "MoonImageEnabled", MoonImage.Enabled);
			ini.SetValue("Graphs", "MoonImageSize", MoonImage.Size);
			ini.SetValue("Graphs", "MoonImageShadeTransparent", MoonImage.Transparent);
			ini.SetValue("Graphs", "MoonImageFtpDest", MoonImage.FtpDest);
			ini.SetValue("Graphs", "MoonImageCopyDest", MoonImage.CopyDest);
			ini.SetValue("Graphs", "TempVisible", GraphOptions.Visible.Temp.Val);
			ini.SetValue("Graphs", "InTempVisible", GraphOptions.Visible.InTemp.Val);
			ini.SetValue("Graphs", "HIVisible", GraphOptions.Visible.HeatIndex.Val);
			ini.SetValue("Graphs", "DPVisible", GraphOptions.Visible.DewPoint.Val);
			ini.SetValue("Graphs", "WCVisible", GraphOptions.Visible.WindChill.Val);
			ini.SetValue("Graphs", "AppTempVisible", GraphOptions.Visible.AppTemp.Val);
			ini.SetValue("Graphs", "FeelsLikeVisible", GraphOptions.Visible.FeelsLike.Val);
			ini.SetValue("Graphs", "HumidexVisible", GraphOptions.Visible.Humidex.Val);
			ini.SetValue("Graphs", "InHumVisible", GraphOptions.Visible.InHum.Val);
			ini.SetValue("Graphs", "OutHumVisible", GraphOptions.Visible.OutHum.Val);
			ini.SetValue("Graphs", "UVVisible", GraphOptions.Visible.UV.Val);
			ini.SetValue("Graphs", "SolarVisible", GraphOptions.Visible.Solar.Val);
			ini.SetValue("Graphs", "SunshineVisible", GraphOptions.Visible.Sunshine.Val);
			ini.SetValue("Graphs", "DailyAvgTempVisible", GraphOptions.Visible.AvgTemp.Val);
			ini.SetValue("Graphs", "DailyMaxTempVisible", GraphOptions.Visible.MaxTemp.Val);
			ini.SetValue("Graphs", "DailyMinTempVisible", GraphOptions.Visible.MinTemp.Val);
			ini.SetValue("Graphs", "BGTVisible", GraphOptions.Visible.BGT.Val);
			ini.SetValue("Graphs", "GrowingDegreeDaysVisible1", GraphOptions.Visible.GrowingDegreeDays1.Val);
			ini.SetValue("Graphs", "GrowingDegreeDaysVisible2", GraphOptions.Visible.GrowingDegreeDays2.Val);
			ini.SetValue("Graphs", "TempSumVisible0", GraphOptions.Visible.TempSum0.Val);
			ini.SetValue("Graphs", "TempSumVisible1", GraphOptions.Visible.TempSum1.Val);
			ini.SetValue("Graphs", "TempSumVisible2", GraphOptions.Visible.TempSum2.Val);
			ini.SetValue("Graphs", "ChillHoursVisible", GraphOptions.Visible.ChillHours.Val);
			ini.SetValue("Graphs", "ExtraTempVisible", GraphOptions.Visible.ExtraTemp.Vals);
			ini.SetValue("Graphs", "ExtraHumVisible", GraphOptions.Visible.ExtraHum.Vals);
			ini.SetValue("Graphs", "ExtraDewPointVisible", GraphOptions.Visible.ExtraDewPoint.Vals);
			ini.SetValue("Graphs", "SoilTempVisible", GraphOptions.Visible.SoilTemp.Vals);
			ini.SetValue("Graphs", "SoilMoistVisible", GraphOptions.Visible.SoilMoist.Vals);
			ini.SetValue("Graphs", "SoilEcVisible", GraphOptions.Visible.SoilEc.Vals);
			ini.SetValue("Graphs", "UserTempVisible", GraphOptions.Visible.UserTemp.Vals);
			ini.SetValue("Graphs", "LeafWetnessVisible", GraphOptions.Visible.LeafWetness.Vals);
			ini.SetValue("Graphs", "Aq-PmVisible", GraphOptions.Visible.AqSensor.Pm.Vals);
			ini.SetValue("Graphs", "Aq-PmAvgVisible", GraphOptions.Visible.AqSensor.PmAvg.Vals);
			ini.SetValue("Graphs", "Aq-Pm10Visible", GraphOptions.Visible.AqSensor.Pm10.Vals);
			ini.SetValue("Graphs", "Aq-Pm10AvgVisible", GraphOptions.Visible.AqSensor.Pm10Avg.Vals);
			ini.SetValue("Graphs", "Aq-TempVisible", GraphOptions.Visible.AqSensor.Temp.Vals);
			ini.SetValue("Graphs", "Aq-HumVisible", GraphOptions.Visible.AqSensor.Hum.Vals);
			ini.SetValue("Graphs", "CO2-CO2", GraphOptions.Visible.CO2Sensor.CO2.Val);
			ini.SetValue("Graphs", "CO2-CO2Avg", GraphOptions.Visible.CO2Sensor.CO2Avg.Val);
			ini.SetValue("Graphs", "CO2-Pm25", GraphOptions.Visible.CO2Sensor.Pm25.Val);
			ini.SetValue("Graphs", "CO2-Pm25Avg", GraphOptions.Visible.CO2Sensor.Pm25Avg.Val);
			ini.SetValue("Graphs", "CO2-Pm10", GraphOptions.Visible.CO2Sensor.Pm10.Val);
			ini.SetValue("Graphs", "CO2-Pm10Avg", GraphOptions.Visible.CO2Sensor.Pm10Avg.Val);
			ini.SetValue("Graphs", "CO2-Temp", GraphOptions.Visible.CO2Sensor.Temp.Val);
			ini.SetValue("Graphs", "CO2-Hum", GraphOptions.Visible.CO2Sensor.Hum.Val);
			ini.SetValue("Graphs", "SnowDepth", GraphOptions.Visible.SnowDepth.Val);
			ini.SetValue("Graphs", "Snow24h", GraphOptions.Visible.Snow24h.Val);
			ini.SetValue("Graphs", "CurrSnow24h", GraphOptions.Visible.CurrSnow24h.Vals);

			ini.SetValue("Graphs", "LaserDepthVisible", GraphOptions.Visible.LaserDepth.Vals);
			ini.SetValue("Graphs", "LaserDistanceVisible", GraphOptions.Visible.LaserDist.Vals);

			ini.SetValue("GraphColours", "TempColour", GraphOptions.Colour.Temp);
			ini.SetValue("GraphColours", "InTempColour", GraphOptions.Colour.InTemp);
			ini.SetValue("GraphColours", "HIColour", GraphOptions.Colour.HeatIndex);
			ini.SetValue("GraphColours", "DPColour", GraphOptions.Colour.DewPoint);
			ini.SetValue("GraphColours", "WCColour", GraphOptions.Colour.WindChill);
			ini.SetValue("GraphColours", "AppTempColour", GraphOptions.Colour.AppTemp);
			ini.SetValue("GraphColours", "FeelsLikeColour", GraphOptions.Colour.FeelsLike);
			ini.SetValue("GraphColours", "HumidexColour", GraphOptions.Colour.Humidex);
			ini.SetValue("GraphColours", "BGTColour", GraphOptions.Colour.BGT);
			ini.SetValue("GraphColours", "WBGTColour", GraphOptions.Colour.WBGT);

			ini.SetValue("GraphColours", "InHumColour", GraphOptions.Colour.InHum);
			ini.SetValue("GraphColours", "OutHumColour", GraphOptions.Colour.OutHum);
			ini.SetValue("GraphColours", "PressureColour", GraphOptions.Colour.Press);
			ini.SetValue("GraphColours", "WindGustColour", GraphOptions.Colour.WindGust);
			ini.SetValue("GraphColours", "WindAvgColour", GraphOptions.Colour.WindAvg);
			ini.SetValue("GraphColours", "WindRunColour", GraphOptions.Colour.WindRun);
			ini.SetValue("GraphColours", "Rainfall", GraphOptions.Colour.Rainfall);
			ini.SetValue("GraphColours", "RainRate", GraphOptions.Colour.RainRate);
			ini.SetValue("GraphColours", "UVColour", GraphOptions.Colour.UV);
			ini.SetValue("GraphColours", "SolarColour", GraphOptions.Colour.Solar);
			ini.SetValue("GraphColours", "SolarTheoreticalColour", GraphOptions.Colour.SolarTheoretical);
			ini.SetValue("GraphColours", "SunshineColour", GraphOptions.Colour.Sunshine);
			ini.SetValue("GraphColours", "MaxTempColour", GraphOptions.Colour.MaxTemp);
			ini.SetValue("GraphColours", "AvgTempColour", GraphOptions.Colour.AvgTemp);
			ini.SetValue("GraphColours", "MinTempColour", GraphOptions.Colour.MinTemp);
			ini.SetValue("GraphColours", "MaxPressColour", GraphOptions.Colour.MaxPress);
			ini.SetValue("GraphColours", "MinPressColour", GraphOptions.Colour.MinPress);
			ini.SetValue("GraphColours", "MaxHumColour", GraphOptions.Colour.MaxOutHum);
			ini.SetValue("GraphColours", "MinHumColour", GraphOptions.Colour.MinOutHum);
			ini.SetValue("GraphColours", "MaxHIColour", GraphOptions.Colour.MaxHeatIndex);
			ini.SetValue("GraphColours", "MaxDPColour", GraphOptions.Colour.MaxDew);
			ini.SetValue("GraphColours", "MinDPColour", GraphOptions.Colour.MinDew);
			ini.SetValue("GraphColours", "MaxFeelsLikeColour", GraphOptions.Colour.MaxFeels);
			ini.SetValue("GraphColours", "MinFeelsLikeColour", GraphOptions.Colour.MinFeels);
			ini.SetValue("GraphColours", "MaxAppTempColour", GraphOptions.Colour.MaxApp);
			ini.SetValue("GraphColours", "MinAppTempColour", GraphOptions.Colour.MinApp);
			ini.SetValue("GraphColours", "MaxHumidexColour", GraphOptions.Colour.MaxHumidex);
			ini.SetValue("GraphColours", "Pm2p5Colour", GraphOptions.Colour.Pm2p5);
			ini.SetValue("GraphColours", "Pm10Colour", GraphOptions.Colour.Pm10);
			ini.SetValue("GraphColours", "ExtraTempColour", GraphOptions.Colour.ExtraTemp);
			ini.SetValue("GraphColours", "ExtraHumColour", GraphOptions.Colour.ExtraHum);
			ini.SetValue("GraphColours", "ExtraDewPointColour", GraphOptions.Colour.ExtraDewPoint);
			ini.SetValue("GraphColours", "SoilTempColour", GraphOptions.Colour.SoilTemp);
			ini.SetValue("GraphColours", "SoilMoistColour", GraphOptions.Colour.SoilMoist);
			ini.SetValue("GraphColours", "SoilEcColour", GraphOptions.Colour.SoilEc);
			ini.SetValue("GraphColours", "LeafWetness", GraphOptions.Colour.LeafWetness);
			ini.SetValue("GraphColours", "UserTempColour", GraphOptions.Colour.UserTemp);
			ini.SetValue("GraphColours", "LaserDepthColour", GraphOptions.Colour.LaserDepth);

			ini.SetValue("GraphColours", "CO2-CO2Colour", GraphOptions.Colour.CO2Sensor.CO2);
			ini.SetValue("GraphColours", "CO2-CO2AvgColour", GraphOptions.Colour.CO2Sensor.CO2Avg);
			ini.SetValue("GraphColours", "CO2-Pm25Colour", GraphOptions.Colour.CO2Sensor.Pm25);
			ini.SetValue("GraphColours", "CO2-Pm25AvgColour", GraphOptions.Colour.CO2Sensor.Pm25Avg);
			ini.SetValue("GraphColours", "CO2-Pm10Colour", GraphOptions.Colour.CO2Sensor.Pm10);
			ini.SetValue("GraphColours", "CO2-Pm10AvgColour", GraphOptions.Colour.CO2Sensor.Pm10Avg);
			ini.SetValue("GraphColours", "CO2-TempColour", GraphOptions.Colour.CO2Sensor.Temp);
			ini.SetValue("GraphColours", "CO2-HumColour", GraphOptions.Colour.CO2Sensor.Hum);
			ini.SetValue("GraphColours", "SnowDepthColour", GraphOptions.Colour.SnowDepth);
			ini.SetValue("GraphColours", "Snow24hColour", GraphOptions.Colour.Snow24h);


			ini.SetValue("MySQL", "Host", MySqlFuncs.MySqlConnSettings.Server);
			ini.SetValue("MySQL", "Port", (int) MySqlFuncs.MySqlConnSettings.Port);
			ini.SetValue("MySQL", "User", Crypto.EncryptString(MySqlFuncs.MySqlConnSettings.UserID, Program.InstanceId, "MySql UserID"));
			ini.SetValue("MySQL", "Pass", Crypto.EncryptString(MySqlFuncs.MySqlConnSettings.Password, Program.InstanceId, "MySql Password"));
			ini.SetValue("MySQL", "Database", MySqlFuncs.MySqlConnSettings.Database);
			ini.SetValue("MySQL", "SSLmode", (int) MySqlFuncs.MySqlConnSettings.SslMode);
			ini.SetValue("MySQL", "TLSversions", MySqlFuncs.MySqlConnSettings.TlsVersion);

			ini.SetValue("MySQL", "MonthlyMySqlEnabled", MySqlFuncs.MySqlSettings.Monthly.Enabled);
			ini.SetValue("MySQL", "RealtimeMySqlEnabled", MySqlFuncs.MySqlSettings.Realtime.Enabled);
			ini.SetValue("MySQL", "RealtimeMySql1MinLimit", MySqlFuncs.MySqlSettings.RealtimeLimit1Minute);
			ini.SetValue("MySQL", "DayfileMySqlEnabled", MySqlFuncs.MySqlSettings.Dayfile.Enabled);
			ini.SetValue("MySQL", "UpdateOnEdit", MySqlFuncs.MySqlSettings.UpdateOnEdit);
			ini.SetValue("MySQL", "BufferOnFailure", MySqlFuncs.MySqlSettings.BufferOnfailure);


			ini.SetValue("MySQL", "MonthlyTable", MySqlFuncs.MySqlSettings.Monthly.TableName);
			ini.SetValue("MySQL", "DayfileTable", MySqlFuncs.MySqlSettings.Dayfile.TableName);
			ini.SetValue("MySQL", "RealtimeTable", MySqlFuncs.MySqlSettings.Realtime.TableName);
			ini.SetValue("MySQL", "RealtimeRetention", MySqlFuncs.MySqlSettings.RealtimeRetention);

			ini.SetValue("MySQL", "CustomMySqlSecondsEnabled", MySqlFuncs.MySqlSettings.CustomSecs.Enabled);
			ini.SetValue("MySQL", "CustomMySqlMinutesEnabled", MySqlFuncs.MySqlSettings.CustomMins.Enabled);
			ini.SetValue("MySQL", "CustomMySqlRolloverEnabled", MySqlFuncs.MySqlSettings.CustomRollover.Enabled);
			ini.SetValue("MySQL", "CustomMySqlStartUpEnabled", MySqlFuncs.MySqlSettings.CustomStartUp.Enabled);

			ini.SetValue("MySQL", "CustomMySqlSecondsInterval", MySqlFuncs.MySqlSettings.CustomSecs.Interval);

			ini.SetValue("MySQL", "CustomMySqlSecondsCommandString", MySqlFuncs.MySqlSettings.CustomSecs.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlMinutesCommandString", MySqlFuncs.MySqlSettings.CustomMins.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlRolloverCommandString", MySqlFuncs.MySqlSettings.CustomRollover.Commands[0]);
			ini.SetValue("MySQL", "CustomMySqlStartUpCommandString", MySqlFuncs.MySqlSettings.CustomStartUp.Commands[0]);

			ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIndex", MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[0]);
			ini.SetValue("MySQL", "CustomMySqlMinutesIntervalCatchUp", MySqlFuncs.MySqlSettings.CustomMins.CatchUp[0]);
			ini.SetValue("MySQL", "CustomMySqlRolloverCatchUp", MySqlFuncs.MySqlSettings.CustomRollover.CatchUp[0]);

			for (var i = 1; i < 10; i++)
			{
				if (string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomSecs.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlSecondsCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlSecondsCommandString" + i, MySqlFuncs.MySqlSettings.CustomSecs.Commands[i]);

				if (string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomMins.Commands[i]))
				{
					ini.DeleteValue("MySQL", "CustomMySqlMinutesCommandString" + i);
					ini.DeleteValue("MySQL", "CustomMySqlMinutesIntervalIdx" + i);
					ini.DeleteValue("MySQL", "CustomMySqlMinutesIntervalCatchUp" + i);
				}
				else
				{
					ini.SetValue("MySQL", "CustomMySqlMinutesCommandString" + i, MySqlFuncs.MySqlSettings.CustomMins.Commands[i]);
					ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIdx" + i, MySqlFuncs.MySqlSettings.CustomMins.IntervalIndexes[i]);
					ini.SetValue("MySQL", "CustomMySqlMinutesIntervalCatchUp" + i, MySqlFuncs.MySqlSettings.CustomMins.CatchUp[i]);
				}

				if (string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomRollover.Commands[i]))
				{
					ini.DeleteValue("MySQL", "CustomMySqlRolloverCommandString" + i);
					ini.DeleteValue("MySQL", "CustomMySqlRolloverCatchUp" + i);
				}
				else
				{
					ini.SetValue("MySQL", "CustomMySqlRolloverCommandString" + i, MySqlFuncs.MySqlSettings.CustomRollover.Commands[i]);
					ini.SetValue("MySQL", "CustomMySqlRolloverCatchUp" + i, MySqlFuncs.MySqlSettings.CustomRollover.CatchUp[i]);
				}

				if (string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i]))
					ini.DeleteValue("MySQL", "CustomMySqlStartUpCommandString" + i);
				else
					ini.SetValue("MySQL", "CustomMySqlStartUpCommandString" + i, MySqlFuncs.MySqlSettings.CustomStartUp.Commands[i]);
			}

			// MySql - Timed
			ini.SetValue("MySQL", "CustomMySqlTimedEnabled", MySqlFuncs.MySqlSettings.CustomTimed.Enabled);

			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]))
				{
					ini.DeleteValue("MySQL", "CustomMySqlTimedCommandString" + i);
					ini.DeleteValue("MySQL", "CustomMySqlTimedStartTime" + i);
					ini.DeleteValue("MySQL", "CustomMySqlTimedInterval" + i);
				}
				else
				{
					ini.SetValue("MySQL", "CustomMySqlTimedCommandString" + i, MySqlFuncs.MySqlSettings.CustomTimed.Commands[i]);
					ini.SetValue("MySQL", "CustomMySqlTimedStartTime" + i, MySqlFuncs.MySqlSettings.CustomTimed.GetStartTimeString(i));
					ini.SetValue("MySQL", "CustomMySqlTimedInterval" + i, MySqlFuncs.MySqlSettings.CustomTimed.Intervals[i]);
				}
			}


			ini.SetValue("HTTP", "CustomHttpSecondsString", CustomHttpSecondsStrings[0]);
			ini.SetValue("HTTP", "CustomHttpMinutesString", CustomHttpMinutesStrings[0]);
			ini.SetValue("HTTP", "CustomHttpRolloverString", CustomHttpRolloverStrings[0]);

			for (var i = 1; i < 10; i++)
			{
				if (string.IsNullOrEmpty(CustomHttpSecondsStrings[i]))
					ini.DeleteValue("HTTP", "CustomHttpSecondsString" + i);
				else
					ini.SetValue("HTTP", "CustomHttpSecondsString" + i, CustomHttpSecondsStrings[i]);

				if (string.IsNullOrEmpty(CustomHttpMinutesStrings[i]))
					ini.DeleteValue("HTTP", "CustomHttpMinutesString" + i);
				else
					ini.SetValue("HTTP", "CustomHttpMinutesString" + i, CustomHttpMinutesStrings[i]);

				if (string.IsNullOrEmpty(CustomHttpRolloverStrings[i]))
					ini.DeleteValue("HTTP", "CustomHttpRolloverString" + i);
				else
					ini.SetValue("HTTP", "CustomHttpRolloverString" + i, CustomHttpRolloverStrings[i]);
			}

			ini.SetValue("HTTP", "CustomHttpSecondsEnabled", CustomHttpSecondsEnabled);
			ini.SetValue("HTTP", "CustomHttpMinutesEnabled", CustomHttpMinutesEnabled);
			ini.SetValue("HTTP", "CustomHttpRolloverEnabled", CustomHttpRolloverEnabled);

			ini.SetValue("HTTP", "CustomHttpSecondsInterval", CustomHttpSecondsInterval);
			ini.SetValue("HTTP", "CustomHttpMinutesIntervalIndex", CustomHttpMinutesIntervalIndex);

			// Http files
			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(HttpFilesConfig[i].Url) && string.IsNullOrEmpty(HttpFilesConfig[i].Remote))
				{
					ini.DeleteValue("HTTP", "HttpFileEnabled" + i);
					ini.DeleteValue("HTTP", "HttpFileUrl" + i);
					ini.DeleteValue("HTTP", "HttpFileRemote" + i);
					ini.DeleteValue("HTTP", "HttpFileInterval" + i);
					ini.DeleteValue("HTTP", "HttpFileUpload" + i);
					ini.DeleteValue("HTTP", "HttpFileTimed" + i);
					ini.DeleteValue("HTTP", "HttpFileStartTime" + i);
				}
				else
				{
					ini.SetValue("HTTP", "HttpFileEnabled" + i, HttpFilesConfig[i].Enabled);
					ini.SetValue("HTTP", "HttpFileUrl" + i, HttpFilesConfig[i].Url);
					ini.SetValue("HTTP", "HttpFileRemote" + i, HttpFilesConfig[i].Remote);
					ini.SetValue("HTTP", "HttpFileInterval" + i, HttpFilesConfig[i].Interval);
					ini.SetValue("HTTP", "HttpFileUpload" + i, HttpFilesConfig[i].Upload);
					ini.SetValue("HTTP", "HttpFileTimed" + i, HttpFilesConfig[i].Timed);
					ini.SetValue("HTTP", "HttpFileStartTime" + i, HttpFilesConfig[i].StartTimeString);
				}
			}

			// Select-a-Chart
			for (int i = 0; i < SelectaChartOptions.series.Length; i++)
			{
				ini.SetValue("Select-a-Chart", "Series" + i, SelectaChartOptions.series[i]);
				ini.SetValue("Select-a-Chart", "Colour" + i, SelectaChartOptions.colours[i]);
			}

			// Select-a-Period
			for (int i = 0; i < SelectaPeriodOptions.series.Length; i++)
			{
				ini.SetValue("Select-a-Period", "Series" + i, SelectaPeriodOptions.series[i]);
				ini.SetValue("Select-a-Period", "Colour" + i, SelectaPeriodOptions.colours[i]);
			}
			ini.SetValue("Select-a-Period", "FromDate", SelectaPeriodOptions.fromDate);
			ini.SetValue("Select-a-Period", "ToDate", SelectaPeriodOptions.toDate);

			// Email settings
			ini.SetValue("SMTP", "Enabled", SmtpOptions.Enabled);
			ini.SetValue("SMTP", "ServerName", SmtpOptions.Server);
			ini.SetValue("SMTP", "Port", SmtpOptions.Port);
			ini.SetValue("SMTP", "SSLOption", SmtpOptions.SslOption);
			ini.SetValue("SMTP", "RequiresAuthentication", SmtpOptions.AuthenticationMethod);
			ini.SetValue("SMTP", "User", Crypto.EncryptString(SmtpOptions.User, Program.InstanceId, "SmtpOptions.User"));
			ini.SetValue("SMTP", "Password", Crypto.EncryptString(SmtpOptions.Password, Program.InstanceId, "SmtpOptions.Password"));
			ini.SetValue("SMTP", "Logging", SmtpOptions.Logging);
			ini.SetValue("SMTP", "IgnoreCertErrors", SmtpOptions.IgnoreCertErrors);

			// Growing Degree Days
			ini.SetValue("GrowingDD", "BaseTemperature1", GrowingBase1);
			ini.SetValue("GrowingDD", "BaseTemperature2", GrowingBase2);
			ini.SetValue("GrowingDD", "YearStarts", GrowingYearStarts);
			ini.SetValue("GrowingDD", "Cap30C", GrowingCap30C);

			// Temperature Sum
			ini.SetValue("TempSum", "TempSumYearStart", TempSumYearStarts);
			ini.SetValue("TempSum", "BaseTemperature1", TempSumBase1);
			ini.SetValue("TempSum", "BaseTemperature2", TempSumBase2);


			// Custom Daily Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(CustomDailyLogSettings[i].FileName) && string.IsNullOrEmpty(CustomDailyLogSettings[i].ContentString))
				{
					ini.DeleteValue("CustomLogs", "DailyEnabled" + i);
					ini.DeleteValue("CustomLogs", "DailyFilename" + i);
					ini.DeleteValue("CustomLogs", "DailyContent" + i);
				}
				else
				{
					ini.SetValue("CustomLogs", "DailyEnabled" + i, CustomDailyLogSettings[i].Enabled);
					ini.SetValue("CustomLogs", "DailyFilename" + i, CustomDailyLogSettings[i].FileName);
					ini.SetValue("CustomLogs", "DailyContent" + i, CustomDailyLogSettings[i].ContentString);
				}
			}

			// Custom Interval Log Settings
			for (var i = 0; i < 10; i++)
			{
				if (string.IsNullOrEmpty(CustomIntvlLogSettings[i].FileName) && string.IsNullOrEmpty(CustomIntvlLogSettings[i].ContentString))
				{
					ini.DeleteValue("CustomLogs", "IntervalEnabled" + i);
					ini.DeleteValue("CustomLogs", "IntervalFilename" + i);
					ini.DeleteValue("CustomLogs", "IntervalContent" + i);
					ini.DeleteValue("CustomLogs", "IntervalIdx" + i);
				}
				else
				{
					ini.SetValue("CustomLogs", "IntervalEnabled" + i, CustomIntvlLogSettings[i].Enabled);
					ini.SetValue("CustomLogs", "IntervalFilename" + i, CustomIntvlLogSettings[i].FileName);
					ini.SetValue("CustomLogs", "IntervalContent" + i, CustomIntvlLogSettings[i].ContentString);
					ini.SetValue("CustomLogs", "IntervalIdx" + i, CustomIntvlLogSettings[i].IntervalIdx);
				}
			}

			// laser sensors
			ini.SetValue("Laser", "PrimarySnowSensor", LaserPrimarySnowSensor);
			for (var i = 1; i < LaserDepthBaseline.Length; i++)
			{
				ini.SetValue("Laser", "LaserDepthOffset" + i, LaserDepthBaseline[i]);
				ini.SetValue("Laser", "IsSnowSensor" + i, LaserIsSnowSensor[i]);
			}


			ini.Flush();

			LogMessage("Completed writing Cumulus.ini file");
		}
	}
}
