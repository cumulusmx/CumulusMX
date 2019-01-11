using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CumulusMX.ExtensionMethods;

namespace CumulusMX.Configuration
{
    public class CumulusConfiguration
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        private readonly IniFile ini;

        public CumulusConfiguration(IniFile ini)
        {
            this.ini = ini;
            log.Info("Reading ini file");
            ReadIniFile();
        }

        #region System Configuration

        public bool WarnMultiple { get; set; }
        public string Platform { get; private set; }
        public string Datapath { get; private set; }
        public string ListSeparator { get; private set; }
        public char DirectorySeparator { get; private set; }

        /// <summary>
        /// Temperature unit currently in use
        /// </summary>
        public string TempUnitText { get; set; }

        /// <summary>
        /// Temperature trend unit in use, eg "°C/hr"
        /// </summary>
        public string TempTrendUnitText { get; set; }

        public string RainUnitText { get; set; }
        public string RainTrendUnitText { get; set; }
        public string PressUnitText { get; set; }
        public string PressTrendUnitText { get; set; }
        public string WindUnitText { get; set; }
        public string WindRunUnitText { get; set; }


        #endregion


        #region Station Settings

        public string RecordsBeganDate { get; set; }
        public string LocationName { get; set; }
        public string LocationDesc { get; set; }
        public int StationType { get; set; }
        public string StationModel { get; set; }

        public bool FineOffsetStation { get; set; }
        public bool DavisStation { get; set; }


        public string ComportName { get; set; }
        public int ImetBaudRate { get; set; }
        public int VendorID { get; set; }
        public int ProductID { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string LonTxt { get; set; }
        public string LatTxt { get; set; }
        public double Altitude { get; set; }
        public bool AltitudeInFeet { get; set; }

        public bool Humidity98Fix { get; set; }

        public bool UseWind10MinAve { get; set; }
        public bool UseSpeedForAvgCalc { get; set; }

        private int _avgBearingMinutes;
        public TimeSpan AvgBearingTime => new TimeSpan(_avgBearingMinutes / 60, _avgBearingMinutes % 60, 0);
        private int _avgSpeedMinutes;
        public TimeSpan AvgSpeedTime => new TimeSpan(_avgSpeedMinutes / 60, _avgSpeedMinutes % 60, 0);
        private int _peakGustMinutes;
        public TimeSpan PeakGustTime => new TimeSpan(_peakGustMinutes / 60, _peakGustMinutes % 60, 0);




        public bool NoSensorCheck { get; set; }
        public bool CalculatedDP { get; set; }
        public bool CalculatedWC { get; set; }

        public int RolloverHour { get; set; }

        public bool Use10amInSummer { get; set; }



        public bool SyncTime { get; set; }
        public int ClockSettingHour { get; set; }
        public bool WS2300IgnoreStationClock { get; set; }

        public bool LogExtraSensors { get; set; }


        public bool ReportDataStoppedErrors { get; set; }
        public bool ReportLostSensorContact { get; set; }
        public bool NoFlashWetDryDayRecords { get; set; }
        public bool ErrorLogSpikeRemoval { get; set; }
        public int DataLogInterval { get; set; }


        public bool SyncFOReads { get; set; }
        public int FOReadAvoidPeriod { get; set; }
        public int FineOffsetReadTime { get; set; }


        public int WindUnit { get; set; }
        public int PressUnit { get; set; }
        public int RainUnit { get; set; }
        public int TempUnit { get; set; }
        public bool RoundWindSpeed { get; set; }
        public bool CloudBaseInFeet { get; set; }

        public int WindDPlaces { get; set; }
        public int TempDPlaces { get; set; }
        public int PressDPlaces { get; set; }
        public int RainDPlaces { get; set; }


        #region EasyWeather Settings
        public double EWInterval { get; set; }
        public string EWFile { get; set; }
        public bool EWallowFF { get; set; }
        public bool EWdisablecheckinit { get; set; }
        public bool EWduplicatecheck { get; set; }
        public double EWtempdiff { get; set; }
        public double EWpressurediff { get; set; }
        public double EWhumiditydiff { get; set; }
        public double EWgustdiff { get; set; }
        public double EWwinddiff { get; set; }
        public double EWmaxRainRate { get; set; }
        public double EWmaxHourlyRain { get; set; }
        public int EWminpressureMB { get; set; }
        public int EWmaxpressureMB { get; set; }
        public int EWMaxRainTipDiff { get; set; }
        public double EWpressureoffset { get; set; }
        #endregion


        #region Lacrosse settings
        public int LCMaxWind { get; set; }
        #endregion


        #region Davis Station Settings
        public bool UseDavisLoop2 { get; set; }
        public bool DavisReadReceptionStats { get; set; }
        public int DavisInitWaitTime { get; set; }
        public int DavisIPResponseTime { get; set; }
        public int DavisReadTimeout { get; set; }
        public bool DavisIncrementPressureDP { get; set; }
        public bool ForceVPBarUpdate { get; set; }
        public bool DavisUseDLLBarCalData { get; set; }
        public bool DavisCalcAltPress { get; set; }
        public bool DavisConsoleHighGust { get; set; }
        public int VPrainGaugeType { get; set; }
        public bool UseDataLogger { get; set; }
        public int VP2ConnectionType { get; set; }
        public int VP2TCPPort { get; set; }
        public string VP2IPAddr { get; set; }
        public int VP2PeriodicDisconnectInterval { get; set; }

        #endregion


        #region Imet settings

        public int ImetWaitTime { get; set; }
        public bool ImetUpdateLogPointer { get; set; }
        #endregion


        #region WMR Settings
        public int WMR928TempChannel { get; set; }
        public int WMR200TempChannel { get; set; }
        #endregion


        public bool UseCumulusForecast { get; set; }
        public bool HourlyForecast { get; set; }
        public bool UseCumulusPresstrendstr { get; set; }
        public int SnowDepthHour { get; set; }
        public bool UseZeroBearing { get; set; }
        public double RainDayThreshold { get; set; }
        public bool FCpressinMB { get; set; }
        public double FClowpress { get; set; }
        public double FChighpress { get; set; }
        public double FCPressureThreshold { get; set; }
        public int RainSeasonStart { get; set; }
        public int ChillHourSeasonStart { get; set; }
        public double ChillHourThreshold { get; set; }



        public string RG11Port { get; set; }
        public bool RG11TBRmode { get; set; }
        public double RG11tipsize { get; set; }
        public bool RG11IgnoreFirst { get; set; }
        public bool RG11DTRmode { get; set; }
        public string RG11Port2 { get; set; }
        public bool RG11TBRmode2 { get; set; }
        public double RG11tipsize2 { get; set; }
        public bool RG11IgnoreFirst2 { get; set; }
        public bool RG11DTRmode2 { get; set; }



        public bool CreateWxnowTxt { get; set; }
        public bool ListWebTags { get; set; }
        #endregion


        #region Solar settings
        public int SunThreshold { get; set; }
        public double RStransfactor { get; set; }
        public int SolarMinimum { get; set; }
        public double LuxToWM2 { get; set; }
        public bool UseBlakeLarsen { get; set; }
        public int SolarCalc { get; set; }
        public double BrasTurbidity { get; set; }
        #endregion


        #region Calibration settings
        public double PressOffset { get; set; }
        public double TempOffset { get; set; }
        public int HumOffset { get; set; }
        public int WindDirOffset { get; set; }
        public double InTempoffset { get; set; }
        public double UVOffset { get; set; }
        public double WetBulbOffset { get; set; }

        public double WindSpeedMult { get; set; }
        public double WindGustMult { get; set; }
        public double TempMult { get; set; }
        public double TempMult2 { get; set; }
        public double HumMult { get; set; }
        public double HumMult2 { get; set; }
        public double RainMult { get; set; }
        public double UVMult { get; set; }
        public double WetBulbMult { get; set; }
        #endregion


        #region FTP Settings

        public string ftp_host { get; set; }
        public int ftp_port { get; set; }
        public string ftp_user { get; set; }
        public string ftp_password { get; set; }
        public string ftp_directory { get; set; }

        public bool WebAutoUpdate { get; set; }
        public bool ActiveFTPMode { get; set; }
        public bool Sslftp { get; set; }
        public bool DisableEPSV { get; set; }
        public bool FTPlogging { get; set; }

        public bool RealtimeEnabled { get; set; } // The timer is to be started
        public bool RealtimeFTPEnabled { get; set; } // The FTP connection is to be established
        public bool RealtimeTxtFTP { get; set; } // The realtime.txt file is to be uploaded
        public bool RealtimeGaugesTxtFTP { get; set; } // The realtimegauges.txt file is to be uploaded
        public int RealtimeInterval { get; set; }
        public int UpdateInterval { get; set; }
        public bool SynchronisedWebUpdate { get; set; }
        public bool IncludeStandardFiles { get; set; }
        public bool IncludeGraphDataFiles { get; set; }
        public bool FTPRename { get; set; }
        public bool UTF8encode { get; set; }
        public bool DeleteBeforeUpload { get; set; }
        public TExtraFiles[] ExtraFiles = new TExtraFiles[numextrafiles];
        public string ExternalProgram { get; set; }
        public string RealtimeProgram { get; set; }
        public string DailyProgram { get; set; }
        public string ExternalParams { get; set; }
        public string RealtimeParams { get; set; }
        public string DailyParams { get; set; }
        #endregion


        #region Web
        public string ForumURL { get; set; }
        public string WebcamURL { get; set; }
        #endregion


        #region Graphs
        public int GraphDays { get; set; }
        public int GraphHours { get; set; }

        #endregion


        #region Wunderground settings

        public string WundID { get; set; }
        public string WundPW { get; set; }
        public bool WundEnabled { get; set; }
        public bool WundRapidFireEnabled { get; set; }
        public int WundInterval { get; set; }
        public bool SendUVToWund { get; set; }
        public bool SendSRToWund { get; set; }
        public bool SendIndoorToWund { get; set; }
        public bool SendSoilTemp1ToWund { get; set; }
        public bool SendSoilTemp2ToWund { get; set; }
        public bool SendSoilTemp3ToWund { get; set; }
        public bool SendSoilTemp4ToWund { get; set; }
        public bool SendSoilMoisture1ToWund { get; set; }
        public bool SendSoilMoisture2ToWund { get; set; }
        public bool SendSoilMoisture3ToWund { get; set; }
        public bool SendSoilMoisture4ToWund { get; set; }
        public bool SendLeafWetness1ToWund { get; set; }
        public bool SendLeafWetness2ToWund { get; set; }
        public bool WundSendAverage { get; set; }
        public bool WundCatchUp { get; set; }
        public bool WundCatchingUp { get; set; }
        public bool SynchronisedWUUpdate { get; set; }
        #endregion


        #region Awekas settings
        public string AwekasUser { get; set; }
        public string AwekasPW { get; set; }
        public bool AwekasEnabled { get; set; }
        public int AwekasInterval { get; set; }
        public string AwekasLang { get; set; }
        public bool SendUVToAwekas { get; set; }
        public bool SendSolarToAwekas { get; set; }
        public bool SendSoilTempToAwekas { get; set; }
        public bool SynchronisedAwekasUpdate { get; set; }
        #endregion


        #region WeatherCloud settings
        public string WCloudWid { get; set; }
        public string WCloudKey { get; set; }
        public bool WCloudEnabled { get; set; }
        public int WCloudInterval { get; set; }
        public bool SendUVToWCloud { get; set; }
        public bool SendSolarToWCloud { get; set; }
        public bool SynchronisedWCloudUpdate { get; set; }
        #endregion 


        #region Twitter settings
        public string Twitteruser { get; set; }
        public string TwitterPW { get; set; }
        public bool TwitterEnabled { get; set; }
        public int TwitterInterval { get; set; }
        private string TwitterOauthToken { get; set; }
        private string TwitterOauthTokenSecret { get; set; }
        public bool TwitterSendLocation { get; set; }
        public bool SynchronisedTwitterUpdate { get; set; }
        #endregion


        #region PWSWeather settings
        public string PWSID { get; set; }
        public string PWSPW { get; set; }
        public bool PWSEnabled { get; set; }
        public int PWSInterval { get; set; }
        public bool SendUVToPWS { get; set; }
        public bool SendSRToPWS { get; set; }
        public bool PWSCatchUp { get; set; }
        public bool SynchronisedPWSUpdate { get; set; }
        #endregion


        #region WOW settings
        public string WOWID { get; set; }
        public string WOWPW { get; set; }
        public bool WOWEnabled { get; set; }
        public int WOWInterval { get; set; }
        public bool SendUVToWOW { get; set; }
        public bool SendSRToWOW { get; set; }
        public bool WOWCatchUp { get; set; }
        public bool SynchronisedWOWUpdate { get; set; }
        #endregion


        #region Weatherbug settings
        public string WeatherbugID { get; set; }
        public string WeatherbugNumber { get; set; }
        public string WeatherbugPW { get; set; }
        public bool WeatherbugEnabled { get; set; }
        public int WeatherbugInterval { get; set; }
        public bool SendUVToWeatherbug { get; set; }
        public bool SendSRToWeatherbug { get; set; }
        public bool WeatherbugCatchUp { get; set; }
        public bool SynchronisedWBUpdate { get; set; }
        #endregion


        #region APRS Settings
        public string APRSserver { get; set; }
        public string APRSID { get; set; }
        public string APRSpass { get; set; }
        public int APRSport { get; set; }
        public int APRSinterval { get; set; }
        public bool APRSenabled { get; set; }
        public bool APRSHumidityCutoff { get; set; }
        public bool SendSRToAPRS { get; set; }
        public bool SynchronisedAPRSUpdate { get; set; }
        #endregion


        #region xAP Settings
        public bool xapEnabled { get; set; }
        public string xapUID { get; set; }
        public int xapPort { get; set; }
        #endregion


        #region NOAA Settings
        public string NOAAname { get; set; }
        public string NOAAcity { get; set; }
        public string NOAAstate { get; set; }
        public bool NOAA12hourformat { get; set; }
        public double NOAAheatingthreshold { get; set; }
        public double NOAAcoolingthreshold { get; set; }
        public double NOAAmaxtempcomp1 { get; set; }
        public double NOAAmaxtempcomp2 { get; set; }
        public double NOAAmintempcomp1 { get; set; }
        public double NOAAmintempcomp2 { get; set; }
        public double NOAAraincomp1 { get; set; }
        public double NOAAraincomp2 { get; set; }
        public double NOAAraincomp3 { get; set; }
        public bool NOAAAutoSave { get; set; }
        public bool NOAAAutoFTP { get; set; }
        public bool NOAANeedFTP { get; set; }
        public string NOAAMonthFileFormat { get; set; }
        public string NOAAYearFileFormat { get; set; }
        public string NOAAFTPDirectory { get; set; }
        public string NOAALatestMonthlyReport { get; set; }
        public string NOAALatestYearlyReport { get; set; }
        public bool NOAAUseUTF8 { get; set; }

        public double NOAATempNormJan { get; set; }
        public double NOAATempNormFeb { get; set; }
        public double NOAATempNormMar { get; set; }
        public double NOAATempNormApr { get; set; }
        public double NOAATempNormMay { get; set; }
        public double NOAATempNormJun { get; set; }
        public double NOAATempNormJul { get; set; }
        public double NOAATempNormAug { get; set; }
        public double NOAATempNormSep { get; set; }
        public double NOAATempNormOct { get; set; }
        public double NOAATempNormNov { get; set; }
        public double NOAATempNormDec { get; set; }

        public double NOAARainNormJan { get; set; }
        public double NOAARainNormFeb { get; set; }
        public double NOAARainNormMar { get; set; }
        public double NOAARainNormApr { get; set; }
        public double NOAARainNormMay { get; set; }
        public double NOAARainNormJun { get; set; }
        public double NOAARainNormJul { get; set; }
        public double NOAARainNormAug { get; set; }
        public double NOAARainNormSep { get; set; }
        public double NOAARainNormOct { get; set; }
        public double NOAARainNormNov { get; set; }
        public double NOAARainNormDec { get; set; }
        #endregion


        #region Proxy Settings
        public string HTTPProxyName { get; set; }
        public int HTTPProxyPort { get; set; }
        public string HTTPProxyUser { get; set; }
        public string HTTPProxyPassword { get; set; }
        #endregion


        #region Display settings
        public int NumWindRosePoints { get; set; }
        public double WindRoseAngle { get; set; }
        #endregion


        #region MySql settings
        public string MySqlHost { get; set; }
        public int MySqlPort { get; set; }
        public string MySqlUser { get; set; }
        public string MySqlPass { get; set; }
        public string MySqlDatabase { get; set; }

        public bool RealtimeMySqlEnabled { get; set; }
        public string MySqlRealtimeTable { get; set; }
        public string MySqlRealtimeRetention { get; set; }

        public bool MonthlyMySqlEnabled { get; set; }
        public string MySqlMonthlyTable { get; set; }

        public bool DayfileMySqlEnabled { get; set; }
        public string MySqlDayfileTable { get; set; }

        public bool CustomMySqlSecondsEnabled { get; set; }
        public string CustomMySqlSecondsCommandString { get; set; }
        public int CustomMySqlSecondsInterval { get; set; }

        public bool CustomMySqlMinutesEnabled { get; set; }
        public string CustomMySqlMinutesCommandString { get; set; }
        public int CustomMySqlMinutesInterval { get; set; }
        public int CustomMySqlMinutesIntervalIndex { get; set; }

        public bool CustomMySqlRolloverEnabled { get; set; }
        public string CustomMySqlRolloverCommandString { get; set; }

        public string StartOfMonthlyInsertSQL { get; set; }
        public string StartOfDayfileInsertSQL { get; set; }
        public string StartOfRealtimeInsertSQL { get; set; }
        public string DeleteRealtimeSQL { get; set; }

        public string CreateMonthlySQL { get; set; }
        public string CreateDayfileSQL { get; set; }
        public string CreateRealtimeSQL { get; set; }
        #endregion


        #region CustomHttp Settings
        internal bool CustomHttpSecondsEnabled { get; set; }
        internal string CustomHttpSecondsString { get; set; }
        internal int CustomHttpSecondsInterval { get; set; }

        internal bool CustomHttpMinutesEnabled { get; set; }
        internal string CustomHttpMinutesString { get; set; }
        internal int CustomHttpMinutesInterval { get; set; }
        internal int CustomHttpMinutesIntervalIndex { get; set; }

        internal bool CustomHttpRolloverEnabled { get; set; }
        internal string CustomHttpRolloverString { get; set; }
        #endregion


        internal int[] FactorsOf60 = { 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60 };
                


        public int[] WindDPlace = { 1, 1, 1, 1 };
        public int[] TempDPlace = { 1, 1 };
        public int[] PressDPlace = { 1, 1, 2 };
        public int[] RainDPlace = { 1, 2 };
        public const int numextrafiles = 99;




        internal void ReadIniFile()
        {
            RecordsBeganDate = ini.GetValue("Station", "StartDate", DateTime.Now.ToLongDateString());

            WarnMultiple = ini.GetValue("Station", "WarnMultiple", false);

            LocationName = ini.GetValue("Station", "LocName", "");
            LocationDesc = ini.GetValue("Station", "LocDesc", "");
            StationType = ini.GetValue("Station", "Type", -1);
            StationModel = ini.GetValue("Station", "Model", "");

            FineOffsetStation = (StationType == StationTypes.FineOffset || StationType == StationTypes.FineOffsetSolar);
            DavisStation = (StationType == StationTypes.VantagePro || StationType == StationTypes.VantagePro2);


            string defaultComPort = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "COM1" : "/dev/ttyUSB0";

            ComportName = ini.GetValue("Station", "ComportName", defaultComPort);
            ImetBaudRate = ini.GetValue("Station", "ImetBaudRate", 19200);

            VendorID = ini.GetValue("Station", "VendorID", -1);
            ProductID = ini.GetValue("Station", "ProductID", -1);

            Latitude = ini.GetValue("Station", "Latitude", 0.0);
            Longitude = ini.GetValue("Station", "Longitude", 0.0);

            LatTxt = ini.GetValue("Station", "LatTxt", "");
            LatTxt = LatTxt.Replace(" ", "&nbsp;");
            LatTxt = LatTxt.Replace("°", "&#39;");
            LonTxt = ini.GetValue("Station", "LonTxt", "");
            LonTxt = LonTxt.Replace(" ", "&nbsp;");
            LonTxt = LonTxt.Replace("°", "&#39;");

            Altitude = ini.GetValue("Station", "Altitude", 0.0);
            AltitudeInFeet = ini.GetValue("Station", "AltitudeInFeet", true);

            Humidity98Fix = ini.GetValue("Station", "Humidity98Fix", false);
            UseWind10MinAve = ini.GetValue("Station", "Wind10MinAverage", false);
            UseSpeedForAvgCalc = ini.GetValue("Station", "UseSpeedForAvgCalc", false);

            _avgBearingMinutes = ini.GetValue("Station", "AvgBearingMinutes", 10).LimitToRange(1, 120);

            _avgSpeedMinutes = ini.GetValue("Station", "AvgSpeedMinutes", 10).LimitToRange(1, 120);

            log.Debug("ASM=" + _avgSpeedMinutes + " AST=" + AvgSpeedTime.ToString());

            _peakGustMinutes = ini.GetValue("Station", "PeakGustMinutes", 10).LimitToRange(1, 120);



            NoSensorCheck = ini.GetValue("Station", "NoSensorCheck", false);

            CalculatedDP = ini.GetValue("Station", "CalculatedDP", false);
            CalculatedWC = ini.GetValue("Station", "CalculatedWC", false);
            RolloverHour = ini.GetValue("Station", "RolloverHour", 0);
            Use10amInSummer = ini.GetValue("Station", "Use10amInSummer", true);

            SyncTime = ini.GetValue("Station", "SyncDavisClock", false);
            ClockSettingHour = ini.GetValue("Station", "ClockSettingHour", 4);
            WS2300IgnoreStationClock = ini.GetValue("Station", "WS2300IgnoreStationClock", false);
            LogExtraSensors = ini.GetValue("Station", "LogExtraSensors", false);
            ReportDataStoppedErrors = ini.GetValue("Station", "ReportDataStoppedErrors", true);
            ReportLostSensorContact = ini.GetValue("Station", "ReportLostSensorContact", true);
            NoFlashWetDryDayRecords = ini.GetValue("Station", "NoFlashWetDryDayRecords", false);
            ErrorLogSpikeRemoval = ini.GetValue("Station", "ErrorLogSpikeRemoval", false);
            DataLogInterval = ini.GetValue("Station", "DataLogInterval", 2);
            // this is now an index
            if (DataLogInterval > 5)
            {
                DataLogInterval = 2;
            }

            SyncFOReads = ini.GetValue("Station", "SyncFOReads", false);
            FOReadAvoidPeriod = ini.GetValue("Station", "FOReadAvoidPeriod", 3);
            FineOffsetReadTime = ini.GetValue("Station", "FineOffsetReadTime", 150);

            WindUnit = ini.GetValue("Station", "WindUnit", 0);
            PressUnit = ini.GetValue("Station", "PressureUnit", 0);
            RainUnit = ini.GetValue("Station", "RainUnit", 0);
            TempUnit = ini.GetValue("Station", "TempUnit", 0);

            RoundWindSpeed = ini.GetValue("Station", "RoundWindSpeed", false);
            CloudBaseInFeet = ini.GetValue("Station", "CloudBaseInFeet", true);

            WindDPlaces = RoundWindSpeed ? 0 : WindDPlace[WindUnit];

            TempDPlaces = TempDPlace[TempUnit];
            PressDPlaces = PressDPlace[PressUnit];
            if ((StationType == 0 || StationType == 1) && DavisIncrementPressureDP)
            {
                // Use one more DP for Davis stations
                ++PressDPlaces;
            }
            RainDPlaces = RainDPlace[RainUnit];


            //YTDrain = ini.GetValue("Station", "YTDrain", 0.0);
            //YTDrainyear = ini.GetValue("Station", "YTDrainyear", 0);

            EWInterval = ini.GetValue("Station", "EWInterval", 1.0);
            EWFile = ini.GetValue("Station", "EWFile", "");
            EWallowFF = ini.GetValue("Station", "EWFF", false);
            EWdisablecheckinit = ini.GetValue("Station", "EWdisablecheckinit", false);
            EWduplicatecheck = ini.GetValue("Station", "EWduplicatecheck", true);
            EWtempdiff = ini.GetValue("Station", "EWtempdiff", 999.0);
            EWpressurediff = ini.GetValue("Station", "EWpressurediff", 999.0);
            EWhumiditydiff = ini.GetValue("Station", "EWhumiditydiff", 999.0);
            EWgustdiff = ini.GetValue("Station", "EWgustdiff", 999.0);
            EWwinddiff = ini.GetValue("Station", "EWwinddiff", 999.0);
            EWmaxRainRate = ini.GetValue("Station", "EWmaxRainRate", 999.0);
            EWmaxHourlyRain = ini.GetValue("Station", "EWmaxHourlyRain", 999.0);
            EWminpressureMB = ini.GetValue("Station", "EWminpressureMB", 900);
            EWmaxpressureMB = ini.GetValue("Station", "EWmaxpressureMB", 1200);
            EWMaxRainTipDiff = ini.GetValue("Station", "EWMaxRainTipDiff", 30);
            EWpressureoffset = ini.GetValue("Station", "EWpressureoffset", 9999.0);


            LCMaxWind = ini.GetValue("Station", "LCMaxWind", 9999);

            // Davis Station Settings
            UseDavisLoop2 = ini.GetValue("Station", "UseDavisLoop2", true);
            DavisReadReceptionStats = ini.GetValue("Station", "DavisReadReceptionStats", false);
            DavisInitWaitTime = ini.GetValue("Station", "DavisInitWaitTime", 200);
            DavisIPResponseTime = ini.GetValue("Station", "DavisIPResponseTime", 1000);
            DavisReadTimeout = ini.GetValue("Station", "DavisReadTimeout", 1000);
            DavisIncrementPressureDP = ini.GetValue("Station", "DavisIncrementPressureDP", true);
            if (StationType == StationTypes.VantagePro)
            {
                UseDavisLoop2 = false;
            }
            ForceVPBarUpdate = ini.GetValue("Station", "ForceVPBarUpdate", false);
            DavisUseDLLBarCalData = ini.GetValue("Station", "DavisUseDLLBarCalData", false);
            DavisCalcAltPress = ini.GetValue("Station", "DavisCalcAltPress", true);
            DavisConsoleHighGust = ini.GetValue("Station", "DavisConsoleHighGust", false);
            VPrainGaugeType = ini.GetValue("Station", "VPrainGaugeType", -1);
            UseDataLogger = ini.GetValue("Station", "UseDataLogger", true);
            VP2ConnectionType = ini.GetValue("Station", "VP2ConnectionType", 0);
            VP2TCPPort = ini.GetValue("Station", "VP2TCPPort", 22222);
            VP2IPAddr = ini.GetValue("Station", "VP2IPAddr", "0.0.0.0");
            VP2PeriodicDisconnectInterval = ini.GetValue("Station", "VP2PeriodicDisconnectInterval", 0);



            // Imet settings
            ImetWaitTime = ini.GetValue("Station", "ImetWaitTime", 500);
            ImetUpdateLogPointer = ini.GetValue("Station", "ImetUpdateLogPointer", true);


            WMR928TempChannel = ini.GetValue("Station", "WMR928TempChannel", 0);
            WMR200TempChannel = ini.GetValue("Station", "WMR200TempChannel", 1);


            UseCumulusForecast = ini.GetValue("Station", "UseCumulusForecast", false);
            HourlyForecast = ini.GetValue("Station", "HourlyForecast", false);
            UseCumulusPresstrendstr = ini.GetValue("Station", "UseCumulusPresstrendstr", false);
            SnowDepthHour = ini.GetValue("Station", "SnowDepthHour", 0);
            UseZeroBearing = ini.GetValue("Station", "UseZeroBearing", false);
            RainDayThreshold = ini.GetValue("Station", "RainDayThreshold", -1.0);




            FCpressinMB = ini.GetValue("Station", "FCpressinMB", true);
            FClowpress = ini.GetValue("Station", "FClowpress", 950.0);
            FChighpress = ini.GetValue("Station", "FChighpress", 1050.0);
            FCPressureThreshold = ini.GetValue("Station", "FCPressureThreshold", -1.0);
            if (FCPressureThreshold < 0)
            {
                if (PressUnit == 2)
                {
                    FCPressureThreshold = 0.00295333727; // 0.1 mb in inHg
                }
                else
                {
                    FCPressureThreshold = 0.1;
                }
            }


            RainSeasonStart = ini.GetValue("Station", "RainSeasonStart", 1);
            ChillHourSeasonStart = ini.GetValue("Station", "ChillHourSeasonStart", 10);
            ChillHourThreshold = ini.GetValue("Station", "ChillHourThreshold", -999.0);
            if (ChillHourThreshold < -998)
            {
                if (TempUnit == 0)
                {
                    ChillHourThreshold = 7; // C
                }
                else
                {
                    ChillHourThreshold = 45; // F
                }
            }

            RG11Port = ini.GetValue("Station", "RG11portName", defaultComPort);
            RG11TBRmode = ini.GetValue("Station", "RG11TBRmode", false);
            RG11tipsize = ini.GetValue("Station", "RG11tipsize", 0.0);
            RG11IgnoreFirst = ini.GetValue("Station", "RG11IgnoreFirst", false);
            RG11DTRmode = ini.GetValue("Station", "RG11DTRmode", true);

            RG11Port2 = ini.GetValue("Station", "RG11port2Name", defaultComPort);
            RG11TBRmode2 = ini.GetValue("Station", "RG11TBRmode2", false);
            RG11tipsize2 = ini.GetValue("Station", "RG11tipsize2", 0.0);
            RG11IgnoreFirst2 = ini.GetValue("Station", "RG11IgnoreFirst2", false);
            RG11DTRmode2 = ini.GetValue("Station", "RG11DTRmode2", true);


            CreateWxnowTxt = ini.GetValue("Station", "CreateWxnowTxt", true);
            ListWebTags = ini.GetValue("Station", "ListWebTags", false);


            // FTP Settings
            ftp_host = ini.GetValue("FTP site", "Host", "");
            ftp_port = ini.GetValue("FTP site", "Port", 21);
            ftp_user = ini.GetValue("FTP site", "Username", "");
            ftp_password = ini.GetValue("FTP site", "Password", "");
            ftp_directory = ini.GetValue("FTP site", "Directory", "");

            WebAutoUpdate = ini.GetValue("FTP site", "AutoUpdate", false);
            ActiveFTPMode = ini.GetValue("FTP site", "ActiveFTP", false);
            Sslftp = ini.GetValue("FTP site", "Sslftp", false);
            DisableEPSV = ini.GetValue("FTP site", "DisableEPSV", false);
            FTPlogging = ini.GetValue("FTP site", "FTPlogging", false);

            RealtimeEnabled = ini.GetValue("FTP site", "EnableRealtime", false);
            RealtimeFTPEnabled = ini.GetValue("FTP site", "RealtimeFTPEnabled", false);
            RealtimeTxtFTP = ini.GetValue("FTP site", "RealtimeTxtFTP", false);
            RealtimeGaugesTxtFTP = ini.GetValue("FTP site", "RealtimeGaugesTxtFTP", false);
            RealtimeInterval = ini.GetValue("FTP site", "RealtimeInterval", 30000).LimitToRange(1, int.MaxValue);

            //RealtimeTimer.Change(0,RealtimeInterval);
            UpdateInterval = ini.GetValue("FTP site", "UpdateInterval", 15).LimitToRange(1, int.MaxValue);

            SynchronisedWebUpdate = (60 % UpdateInterval == 0);
            IncludeStandardFiles = ini.GetValue("FTP site", "IncludeSTD", true);
            IncludeGraphDataFiles = ini.GetValue("FTP site", "IncludeGraphDataFiles", true);

            FTPRename = ini.GetValue("FTP site", "FTPRename", false);
            UTF8encode = ini.GetValue("FTP site", "UTF8encode", true);
            DeleteBeforeUpload = ini.GetValue("FTP site", "DeleteBeforeUpload", false);

            for (int i = 0; i < ExtraFiles.Length; i++)
            {
                ExtraFiles[i].local = ini.GetValue("FTP site", "ExtraLocal" + i, "");
                ExtraFiles[i].remote = ini.GetValue("FTP site", "ExtraRemote" + i, "");
                ExtraFiles[i].process = ini.GetValue("FTP site", "ExtraProcess" + i, false);
                ExtraFiles[i].binary = ini.GetValue("FTP site", "ExtraBinary" + i, false);
                ExtraFiles[i].realtime = ini.GetValue("FTP site", "ExtraRealtime" + i, false);
                ExtraFiles[i].FTP = ini.GetValue("FTP site", "ExtraFTP" + i, true);
                ExtraFiles[i].UTF8 = ini.GetValue("FTP site", "ExtraUTF" + i, false);
            }

            ExternalProgram = ini.GetValue("FTP site", "ExternalProgram", "");
            RealtimeProgram = ini.GetValue("FTP site", "RealtimeProgram", "");
            DailyProgram = ini.GetValue("FTP site", "DailyProgram", "");
            ExternalParams = ini.GetValue("FTP site", "ExternalParams", "");
            RealtimeParams = ini.GetValue("FTP site", "RealtimeParams", "");
            DailyParams = ini.GetValue("FTP site", "DailyParams", "");


            // Web site settings
            ForumURL = ini.GetValue("Web Site", "ForumURL", "https://cumulus.hosiene.co.uk/");
            WebcamURL = ini.GetValue("Web Site", "WebcamURL", "");


            // Graph settings
            GraphDays = ini.GetValue("Graphs", "ChartMaxDays", 31);
            GraphHours = ini.GetValue("Graphs", "GraphHours", 24);


            // Wunderground settings
            WundID = ini.GetValue("Wunderground", "ID", "");
            WundPW = ini.GetValue("Wunderground", "Password", "");
            WundEnabled = ini.GetValue("Wunderground", "Enabled", false);
            WundRapidFireEnabled = ini.GetValue("Wunderground", "RapidFire", false);
            WundInterval = ini.GetValue("Wunderground", "Interval", 15);
            SendUVToWund = ini.GetValue("Wunderground", "SendUV", false);
            SendSRToWund = ini.GetValue("Wunderground", "SendSR", false);
            SendIndoorToWund = ini.GetValue("Wunderground", "SendIndoor", false);
            SendSoilTemp1ToWund = ini.GetValue("Wunderground", "SendSoilTemp1", false);
            SendSoilTemp2ToWund = ini.GetValue("Wunderground", "SendSoilTemp2", false);
            SendSoilTemp3ToWund = ini.GetValue("Wunderground", "SendSoilTemp3", false);
            SendSoilTemp4ToWund = ini.GetValue("Wunderground", "SendSoilTemp4", false);
            SendSoilMoisture1ToWund = ini.GetValue("Wunderground", "SendSoilMoisture1", false);
            SendSoilMoisture2ToWund = ini.GetValue("Wunderground", "SendSoilMoisture2", false);
            SendSoilMoisture3ToWund = ini.GetValue("Wunderground", "SendSoilMoisture3", false);
            SendSoilMoisture4ToWund = ini.GetValue("Wunderground", "SendSoilMoisture4", false);
            SendLeafWetness1ToWund = ini.GetValue("Wunderground", "SendLeafWetness1", false);
            SendLeafWetness2ToWund = ini.GetValue("Wunderground", "SendLeafWetness2", false);
            WundSendAverage = ini.GetValue("Wunderground", "SendAverage", false);
            WundCatchUp = ini.GetValue("Wunderground", "CatchUp", true);
            SynchronisedWUUpdate = (!WundRapidFireEnabled) && (60 % WundInterval == 0);

            // Awekas Settings
            AwekasUser = ini.GetValue("Awekas", "User", "");
            AwekasPW = ini.GetValue("Awekas", "Password", "");
            AwekasEnabled = ini.GetValue("Awekas", "Enabled", false);
            AwekasInterval = ini.GetValue("Awekas", "Interval", 15).LimitToRange(1, int.MaxValue);
            AwekasLang = ini.GetValue("Awekas", "Lang", "en");
            SendUVToAwekas = ini.GetValue("Awekas", "SendUV", false);
            SendSolarToAwekas = ini.GetValue("Awekas", "SendSR", false);
            SendSoilTempToAwekas = ini.GetValue("Awekas", "SendSoilTemp", false);
            SynchronisedAwekasUpdate = (60 % AwekasInterval == 0);

            // WeatherCloud settings
            WCloudWid = ini.GetValue("WeatherCloud", "Wid", "");
            WCloudKey = ini.GetValue("WeatherCloud", "Key", "");
            WCloudEnabled = ini.GetValue("WeatherCloud", "Enabled", false);
            WCloudInterval = ini.GetValue("WeatherCloud", "Interval", 10);
            SendUVToWCloud = ini.GetValue("WeatherCloud", "SendUV", false);
            SendSolarToWCloud = ini.GetValue("WeatherCloud", "SendSR", false);
            SynchronisedWCloudUpdate = (60 % WCloudInterval == 0);

            // Twitter settings
            Twitteruser = ini.GetValue("Twitter", "User", "");
            TwitterPW = ini.GetValue("Twitter", "Password", "");
            TwitterEnabled = ini.GetValue("Twitter", "Enabled", false);
            TwitterInterval = ini.GetValue("Twitter", "Interval", 60).LimitToRange(1, int.MaxValue);
            TwitterOauthToken = ini.GetValue("Twitter", "OauthToken", "unknown");
            TwitterOauthTokenSecret = ini.GetValue("Twitter", "OauthTokenSecret", "unknown");
            TwitterSendLocation = ini.GetValue("Twitter", "SendLocation", true);
            SynchronisedTwitterUpdate = (60 % TwitterInterval == 0);


            // PWSWeather settings
            PWSID = ini.GetValue("PWSweather", "ID", "");
            PWSPW = ini.GetValue("PWSweather", "Password", "");
            PWSEnabled = ini.GetValue("PWSweather", "Enabled", false);
            PWSInterval = ini.GetValue("PWSweather", "Interval", 15).LimitToRange(1, int.MaxValue);
            SendUVToPWS = ini.GetValue("PWSweather", "SendUV", false);
            SendSRToPWS = ini.GetValue("PWSweather", "SendSR", false);
            PWSCatchUp = ini.GetValue("PWSweather", "CatchUp", true);
            SynchronisedPWSUpdate = (60 % PWSInterval == 0);

            // WOW settings
            WOWID = ini.GetValue("WOW", "ID", "");
            WOWPW = ini.GetValue("WOW", "Password", "");
            WOWEnabled = ini.GetValue("WOW", "Enabled", false);
            WOWInterval = ini.GetValue("WOW", "Interval", 15).LimitToRange(1, int.MaxValue);
            SendUVToWOW = ini.GetValue("WOW", "SendUV", false);
            SendSRToWOW = ini.GetValue("WOW", "SendSR", false);
            WOWCatchUp = ini.GetValue("WOW", "CatchUp", true);
            SynchronisedWOWUpdate = (60 % WOWInterval == 0);

            // Weatherbug settings
            WeatherbugID = ini.GetValue("Weatherbug", "ID", "");
            WeatherbugNumber = ini.GetValue("Weatherbug", "Number", "");
            WeatherbugPW = ini.GetValue("Weatherbug", "Password", "");
            WeatherbugEnabled = ini.GetValue("Weatherbug", "Enabled", false);
            WeatherbugInterval = ini.GetValue("Weatherbug", "Interval", 15).LimitToRange(1, int.MaxValue);
            SendUVToWeatherbug = ini.GetValue("Weatherbug", "SendUV", false);
            SendSRToWeatherbug = ini.GetValue("Weatherbug", "SendSR", false);
            WeatherbugCatchUp = ini.GetValue("Weatherbug", "CatchUp", true);
            SynchronisedWBUpdate = (60 % WeatherbugInterval == 0);

            // APRS Settings
            APRSserver = ini.GetValue("APRS", "server", "cwop.aprs.net");
            APRSID = ini.GetValue("APRS", "ID", "");
            APRSpass = ini.GetValue("APRS", "pass", "-1");
            APRSport = ini.GetValue("APRS", "port", 14580);
            APRSinterval = ini.GetValue("APRS", "Interval", 9).LimitToRange(1, int.MaxValue);
            APRSenabled = ini.GetValue("APRS", "Enabled", false);
            APRSHumidityCutoff = ini.GetValue("APRS", "APRSHumidityCutoff", false);
            SendSRToAPRS = ini.GetValue("APRS", "SendSR", false);
            SynchronisedAPRSUpdate = (60 % APRSinterval == 0);


            //alarmlowtemp = ini.GetValue("Alarms", "alarmlowtemp", 0.0);
            //LowTempAlarmSet = ini.GetValue("Alarms", "LowTempAlarmSet", false);
            //LowTempAlarmSound = ini.GetValue("Alarms", "LowTempAlarmSound", false);
            //LowTempAlarmSoundFile = ini.GetValue("Alarms", "LowTempAlarmSoundFile", DefaultSoundFile);

            //alarmhightemp = ini.GetValue("Alarms", "alarmhightemp", 0.0);
            //HighTempAlarmSet = ini.GetValue("Alarms", "HighTempAlarmSet", false);
            //HighTempAlarmSound = ini.GetValue("Alarms", "HighTempAlarmSound", false);
            //HighTempAlarmSoundFile = ini.GetValue("Alarms", "HighTempAlarmSoundFile", DefaultSoundFile);

            //alarmtempchange = ini.GetValue("Alarms", "alarmtempchange", 0.0);
            //TempChangeAlarmSet = ini.GetValue("Alarms", "TempChangeAlarmSet", false);
            //TempChangeAlarmSound = ini.GetValue("Alarms", "TempChangeAlarmSound", false);
            //TempChangeAlarmSoundFile = ini.GetValue("Alarms", "TempChangeAlarmSoundFile", DefaultSoundFile);

            //alarmlowpress = ini.GetValue("Alarms", "alarmlowpress", 0.0);
            //LowPressAlarmSet = ini.GetValue("Alarms", "LowPressAlarmSet", false);
            //LowPressAlarmSound = ini.GetValue("Alarms", "LowPressAlarmSound", false);
            //LowPressAlarmSoundFile = ini.GetValue("Alarms", "LowPressAlarmSoundFile", DefaultSoundFile);

            //alarmhighpress = ini.GetValue("Alarms", "alarmhighpress", 0.0);
            //HighPressAlarmSet = ini.GetValue("Alarms", "HighPressAlarmSet", false);
            //HighPressAlarmSound = ini.GetValue("Alarms", "HighPressAlarmSound", false);
            //HighPressAlarmSoundFile = ini.GetValue("Alarms", "HighPressAlarmSoundFile", DefaultSoundFile);

            //alarmpresschange = ini.GetValue("Alarms", "alarmpresschange", 0.0);
            //PressChangeAlarmSet = ini.GetValue("Alarms", "PressChangeAlarmSet", false);
            //PressChangeAlarmSound = ini.GetValue("Alarms", "PressChangeAlarmSound", false);
            //PressChangeAlarmSoundFile = ini.GetValue("Alarms", "PressChangeAlarmSoundFile", DefaultSoundFile);

            //alarmhighraintoday = ini.GetValue("Alarms", "alarmhighraintoday", 0.0);
            //HighRainTodayAlarmSet = ini.GetValue("Alarms", "HighRainTodayAlarmSet", false);
            //HighRainTodayAlarmSound = ini.GetValue("Alarms", "HighRainTodayAlarmSound", false);
            //HighRainTodayAlarmSoundFile = ini.GetValue("Alarms", "HighRainTodayAlarmSoundFile", DefaultSoundFile);

            //alarmhighrainrate = ini.GetValue("Alarms", "alarmhighrainrate", 0.0);
            //HighRainRateAlarmSet = ini.GetValue("Alarms", "HighRainRateAlarmSet", false);
            //HighRainRateAlarmSound = ini.GetValue("Alarms", "HighRainRateAlarmSound", false);
            //HighRainRateAlarmSoundFile = ini.GetValue("Alarms", "HighRainRateAlarmSoundFile", DefaultSoundFile);

            //alarmhighgust = ini.GetValue("Alarms", "alarmhighgust", 0.0);
            //HighGustAlarmSet = ini.GetValue("Alarms", "HighGustAlarmSet", false);
            //HighGustAlarmSound = ini.GetValue("Alarms", "HighGustAlarmSound", false);
            //HighGustAlarmSoundFile = ini.GetValue("Alarms", "HighGustAlarmSoundFile", DefaultSoundFile);

            //alarmhighwind = ini.GetValue("Alarms", "alarmhighwind", 0.0);
            //HighWindAlarmSet = ini.GetValue("Alarms", "HighWindAlarmSet", false);
            //HighWindAlarmSound = ini.GetValue("Alarms", "HighWindAlarmSound", false);
            //HighWindAlarmSoundFile = ini.GetValue("Alarms", "HighWindAlarmSoundFile", DefaultSoundFile);

            //SensorAlarmSet = ini.GetValue("Alarms", "SensorAlarmSet", false);
            //SensorAlarmSound = ini.GetValue("Alarms", "SensorAlarmSound", false);
            //SensorAlarmSoundFile = ini.GetValue("Alarms", "SensorAlarmSoundFile", DefaultSoundFile);


            // Calibration settings
            PressOffset = ini.GetValue("Offsets", "PressOffset", 0.0);
            TempOffset = ini.GetValue("Offsets", "TempOffset", 0.0);
            HumOffset = ini.GetValue("Offsets", "HumOffset", 0);
            WindDirOffset = ini.GetValue("Offsets", "WindDirOffset", 0);
            InTempoffset = ini.GetValue("Offsets", "InTempOffset", 0.0);
            UVOffset = ini.GetValue("Offsets", "UVOffset", 0.0);
            WetBulbOffset = ini.GetValue("Offsets", "WetBulbOffset", 0.0);
            WindSpeedMult = ini.GetValue("Offsets", "WindSpeedMult", 1.0);
            WindGustMult = ini.GetValue("Offsets", "WindGustMult", 1.0);
            TempMult = ini.GetValue("Offsets", "TempMult", 1.0);
            TempMult2 = ini.GetValue("Offsets", "TempMult2", 0.0);
            HumMult = ini.GetValue("Offsets", "HumMult", 1.0);
            HumMult2 = ini.GetValue("Offsets", "HumMult2", 0.0);
            RainMult = ini.GetValue("Offsets", "RainMult", 1.0);
            UVMult = ini.GetValue("Offsets", "UVMult", 1.0);
            WetBulbMult = ini.GetValue("Offsets", "WetBulbMult", 1.0);

            // xAP settings
            xapEnabled = ini.GetValue("xAP", "Enabled", false);
            xapUID = ini.GetValue("xAP", "UID", "4375");
            xapPort = ini.GetValue("xAP", "Port", 3639);

            // Solar Settings
            SunThreshold = ini.GetValue("Solar", "SunThreshold", 75);
            RStransfactor = ini.GetValue("Solar", "RStransfactor", 0.8);
            SolarMinimum = ini.GetValue("Solar", "SolarMinimum", 0);
            LuxToWM2 = ini.GetValue("Solar", "LuxToWM2", 0.0079);
            UseBlakeLarsen = ini.GetValue("Solar", "UseBlakeLarsen", false);
            SolarCalc = ini.GetValue("Solar", "SolarCalc", 0);
            BrasTurbidity = ini.GetValue("Solar", "BrasTurbidity", 2.0);

            // NOAA Settings
            NOAAname = ini.GetValue("NOAA", "Name", " ");
            NOAAcity = ini.GetValue("NOAA", "City", " ");
            NOAAstate = ini.GetValue("NOAA", "State", " ");
            NOAA12hourformat = ini.GetValue("NOAA", "12hourformat", false);
            NOAAheatingthreshold = ini.GetValue("NOAA", "HeatingThreshold", -1000.0);
            if (NOAAheatingthreshold < -999)
            {
                if (TempUnit == 0)
                {
                    NOAAheatingthreshold = 18.3;
                }
                else
                {
                    NOAAheatingthreshold = 65;
                }
            }
            NOAAcoolingthreshold = ini.GetValue("NOAA", "CoolingThreshold", -1000.0);
            if (NOAAcoolingthreshold < -999)
            {
                if (TempUnit == 0)
                {
                    NOAAcoolingthreshold = 18.3;
                }
                else
                {
                    NOAAcoolingthreshold = 65;
                }
            }
            NOAAmaxtempcomp1 = ini.GetValue("NOAA", "MaxTempComp1", -1000.0);
            if (NOAAmaxtempcomp1 < -999)
            {
                if (TempUnit == 0)
                    NOAAmaxtempcomp1 = 27;
                else
                    NOAAmaxtempcomp1 = 80;
            }
            NOAAmaxtempcomp2 = ini.GetValue("NOAA", "MaxTempComp2", -1000.0);
            if (NOAAmaxtempcomp2 < -999)
            {
                if (TempUnit == 0)
                    NOAAmaxtempcomp2 = 0;
                else
                    NOAAmaxtempcomp2 = 32;
            }
            NOAAmintempcomp1 = ini.GetValue("NOAA", "MinTempComp1", -1000.0);
            if (NOAAmintempcomp1 < -999)
            {
                if (TempUnit == 0)
                    NOAAmintempcomp1 = 0;
                else
                    NOAAmintempcomp1 = 32;
            }
            NOAAmintempcomp2 = ini.GetValue("NOAA", "MinTempComp2", -1000.0);
            if (NOAAmintempcomp2 < -999)
            {
                if (TempUnit == 0)
                    NOAAmintempcomp2 = -18;
                else
                    NOAAmintempcomp2 = 0;
            }
            NOAAraincomp1 = ini.GetValue("NOAA", "RainComp1", -1000.0);
            if (NOAAraincomp1 < -999)
            {
                if (RainUnit == 0)
                    NOAAraincomp1 = 0.2;
                else
                    NOAAraincomp1 = 0.01;
            }
            NOAAraincomp2 = ini.GetValue("NOAA", "RainComp2", -1000.0);
            if (NOAAraincomp2 < -999)
            {
                if (RainUnit == 0)
                    NOAAraincomp2 = 2;
                else
                    NOAAraincomp2 = 0.1;
            }
            NOAAraincomp3 = ini.GetValue("NOAA", "RainComp3", -1000.0);
            if (NOAAraincomp3 < -999)
            {
                if (RainUnit == 0)
                    NOAAraincomp3 = 20;
                else
                    NOAAraincomp3 = 1;
            }

            NOAAAutoSave = ini.GetValue("NOAA", "AutoSave", false);
            NOAAAutoFTP = ini.GetValue("NOAA", "AutoFTP", false);
            NOAAMonthFileFormat = ini.GetValue("NOAA", "MonthFileFormat", "'NOAAMO'MMyy'.txt'");
            NOAAYearFileFormat = ini.GetValue("NOAA", "YearFileFormat", "'NOAAYR'yyyy'.txt'");
            NOAAFTPDirectory = ini.GetValue("NOAA", "FTPDirectory", "");
            NOAAUseUTF8 = ini.GetValue("NOAA", "NOAAUseUTF8", false);

            NOAATempNormJan = ini.GetValue("NOAA", "NOAATempNormJan", -1000.0);
            NOAATempNormFeb = ini.GetValue("NOAA", "NOAATempNormFeb", -1000.0);
            NOAATempNormMar = ini.GetValue("NOAA", "NOAATempNormMar", -1000.0);
            NOAATempNormApr = ini.GetValue("NOAA", "NOAATempNormApr", -1000.0);
            NOAATempNormMay = ini.GetValue("NOAA", "NOAATempNormMay", -1000.0);
            NOAATempNormJun = ini.GetValue("NOAA", "NOAATempNormJun", -1000.0);
            NOAATempNormJul = ini.GetValue("NOAA", "NOAATempNormJul", -1000.0);
            NOAATempNormAug = ini.GetValue("NOAA", "NOAATempNormAug", -1000.0);
            NOAATempNormSep = ini.GetValue("NOAA", "NOAATempNormSep", -1000.0);
            NOAATempNormOct = ini.GetValue("NOAA", "NOAATempNormOct", -1000.0);
            NOAATempNormNov = ini.GetValue("NOAA", "NOAATempNormNov", -1000.0);
            NOAATempNormDec = ini.GetValue("NOAA", "NOAATempNormDec", -1000.0);

            NOAARainNormJan = ini.GetValue("NOAA", "NOAARainNormJan", -1000.0);
            NOAARainNormFeb = ini.GetValue("NOAA", "NOAARainNormFeb", -1000.0);
            NOAARainNormMar = ini.GetValue("NOAA", "NOAARainNormMar", -1000.0);
            NOAARainNormApr = ini.GetValue("NOAA", "NOAARainNormApr", -1000.0);
            NOAARainNormMay = ini.GetValue("NOAA", "NOAARainNormMay", -1000.0);
            NOAARainNormJun = ini.GetValue("NOAA", "NOAARainNormJun", -1000.0);
            NOAARainNormJul = ini.GetValue("NOAA", "NOAARainNormJul", -1000.0);
            NOAARainNormAug = ini.GetValue("NOAA", "NOAARainNormAug", -1000.0);
            NOAARainNormSep = ini.GetValue("NOAA", "NOAARainNormSep", -1000.0);
            NOAARainNormOct = ini.GetValue("NOAA", "NOAARainNormOct", -1000.0);
            NOAARainNormNov = ini.GetValue("NOAA", "NOAARainNormNov", -1000.0);
            NOAARainNormDec = ini.GetValue("NOAA", "NOAARainNormDec", -1000.0);


            // Proxy settings
            HTTPProxyName = ini.GetValue("Proxies", "HTTPProxyName", "");
            HTTPProxyPort = ini.GetValue("Proxies", "HTTPProxyPort", 0);
            HTTPProxyUser = ini.GetValue("Proxies", "HTTPProxyUser", "");
            HTTPProxyPassword = ini.GetValue("Proxies", "HTTPProxyPassword", "");


            // Display settings
            NumWindRosePoints = ini.GetValue("Display", "NumWindRosePoints", 16);
            WindRoseAngle = 360.0 / NumWindRosePoints;



            // MySQL - common
            MySqlHost = ini.GetValue("MySQL", "Host", "127.0.0.1");
            MySqlPort = ini.GetValue("MySQL", "Port", 3306);
            MySqlUser = ini.GetValue("MySQL", "User", "");
            MySqlPass = ini.GetValue("MySQL", "Pass", "");
            MySqlDatabase = ini.GetValue("MySQL", "Database", "database");

            // MySQL - realtimne
            RealtimeMySqlEnabled = ini.GetValue("MySQL", "RealtimeMySqlEnabled", false);
            MySqlRealtimeTable = ini.GetValue("MySQL", "RealtimeTable", "Realtime");
            MySqlRealtimeRetention = ini.GetValue("MySQL", "RealtimeRetention", "");

            // MySQL - monthly log file
            MonthlyMySqlEnabled = ini.GetValue("MySQL", "MonthlyMySqlEnabled", false);
            MySqlMonthlyTable = ini.GetValue("MySQL", "MonthlyTable", "Monthly");

            // MySQL - dayfile
            DayfileMySqlEnabled = ini.GetValue("MySQL", "DayfileMySqlEnabled", false);
            MySqlDayfileTable = ini.GetValue("MySQL", "DayfileTable", "Dayfile");

            // MySQL - custom seconds
            CustomMySqlSecondsEnabled = ini.GetValue("MySQL", "CustomMySqlSecondsEnabled", false);
            CustomMySqlSecondsCommandString = ini.GetValue("MySQL", "CustomMySqlSecondsCommandString", "");
            CustomMySqlSecondsInterval = ini.GetValue("MySQL", "CustomMySqlSecondsInterval", 10).LimitToRange(1, int.MaxValue);

            // MySQL - custom minutes
            CustomMySqlMinutesEnabled = ini.GetValue("MySQL", "CustomMySqlMinutesEnabled", false);
            CustomMySqlMinutesCommandString = ini.GetValue("MySQL", "CustomMySqlMinutesCommandString", "");
            CustomMySqlMinutesIntervalIndex = ini.GetValue("MySQL", "CustomMySqlMinutesIntervalIndex", -1);
            if (CustomMySqlMinutesIntervalIndex >= 0 && CustomMySqlMinutesIntervalIndex < FactorsOf60.Length)
            {
                CustomMySqlMinutesInterval = FactorsOf60[CustomMySqlMinutesIntervalIndex];
            }
            else
            {
                CustomMySqlMinutesInterval = 10;
                CustomMySqlMinutesIntervalIndex = 6;
            }

            // MySQL - custom rollover
            CustomMySqlRolloverCommandString = ini.GetValue("MySQL", "CustomMySqlRolloverCommandString", "");
            CustomMySqlRolloverEnabled = ini.GetValue("MySQL", "CustomMySqlRolloverEnabled", false);


            // Custom HTTP - seconds
            CustomHttpSecondsEnabled = ini.GetValue("HTTP", "CustomHttpSecondsEnabled", false);
            CustomHttpSecondsString = ini.GetValue("HTTP", "CustomHttpSecondsString", "");
            CustomHttpSecondsInterval = ini.GetValue("HTTP", "CustomHttpSecondsInterval", 10).LimitToRange(1, int.MaxValue);

            // Custom HTTP - minutes
            CustomHttpMinutesEnabled = ini.GetValue("HTTP", "CustomHttpMinutesEnabled", false);
            CustomHttpMinutesString = ini.GetValue("HTTP", "CustomHttpMinutesString", "");
            CustomHttpMinutesIntervalIndex = ini.GetValue("HTTP", "CustomHttpMinutesIntervalIndex", -1);
            if (CustomHttpMinutesIntervalIndex >= 0 && CustomHttpMinutesIntervalIndex < FactorsOf60.Length)
            {
                CustomHttpMinutesInterval = FactorsOf60[CustomHttpMinutesIntervalIndex];
            }
            else
            {
                CustomHttpMinutesInterval = 10;
                CustomHttpMinutesIntervalIndex = 6;
            }
            // Http - custom rollover
            CustomHttpRolloverEnabled = ini.GetValue("HTTP", "CustomHttpRolloverEnabled", false);
            CustomHttpRolloverString = ini.GetValue("HTTP", "CustomHttpRolloverString", "");
        }

        internal void WriteIniFile()
        {
            log.Debug("Writing Cumulus.ini file");

            ini.SetValue("Station", "Type", StationType);
            ini.SetValue("Station", "Model", StationModel);
            ini.SetValue("Station", "ComportName", ComportName);
            ini.SetValue("Station", "Latitude", Latitude);
            ini.SetValue("Station", "Longitude", Longitude);
            ini.SetValue("Station", "LatTxt", LatTxt);
            ini.SetValue("Station", "LonTxt", LonTxt);
            ini.SetValue("Station", "Altitude", Altitude);
            ini.SetValue("Station", "AltitudeInFeet", AltitudeInFeet);
            ini.SetValue("Station", "Humidity98Fix", Humidity98Fix);
            ini.SetValue("Station", "Wind10MinAverage", UseWind10MinAve);
            ini.SetValue("Station", "UseSpeedForAvgCalc", UseSpeedForAvgCalc);
            ini.SetValue("Station", "DavisReadReceptionStats", DavisReadReceptionStats);
            ini.SetValue("Station", "CalculatedDP", CalculatedDP);
            ini.SetValue("Station", "CalculatedWC", CalculatedWC);
            ini.SetValue("Station", "RolloverHour", RolloverHour);
            ini.SetValue("Station", "Use10amInSummer", Use10amInSummer);
            ini.SetValue("Station", "SyncDavisClock", SyncTime);
            ini.SetValue("Station", "ClockSettingHour", ClockSettingHour);
            ini.SetValue("Station", "SyncFOReads", SyncFOReads);
            ini.SetValue("Station", "WS2300IgnoreStationClock", WS2300IgnoreStationClock);
            ini.SetValue("Station", "LogExtraSensors", LogExtraSensors);
            ini.SetValue("Station", "DataLogInterval", DataLogInterval);
            ini.SetValue("Station", "WindUnit", WindUnit);
            ini.SetValue("Station", "PressureUnit", PressUnit);
            ini.SetValue("Station", "RainUnit", RainUnit);
            ini.SetValue("Station", "TempUnit", TempUnit);
            ini.SetValue("Station", "LocName", LocationName);
            ini.SetValue("Station", "LocDesc", LocationDesc);
            ini.SetValue("Station", "StartDate", RecordsBeganDate);
            //ini.SetValue("Station", "YTDrain", YTDrain);
            //ini.SetValue("Station", "YTDrainyear", YTDrainyear);
            ini.SetValue("Station", "EWInterval", EWInterval);
            ini.SetValue("Station", "EWFile", EWFile);
            ini.SetValue("Station", "UseDataLogger", UseDataLogger);
            ini.SetValue("Station", "UseCumulusForecast", UseCumulusForecast);
            ini.SetValue("Station", "HourlyForecast", HourlyForecast);
            ini.SetValue("Station", "UseCumulusPresstrendstr", UseCumulusPresstrendstr);
            ini.SetValue("Station", "FCpressinMB", FCpressinMB);
            ini.SetValue("Station", "FClowpress", FClowpress);
            ini.SetValue("Station", "FChighpress", FChighpress);
            ini.SetValue("Station", "ForceVPBarUpdate", ForceVPBarUpdate);
            ini.SetValue("Station", "UseZeroBearing", UseZeroBearing);
            ini.SetValue("Station", "VP2ConnectionType", VP2ConnectionType);
            ini.SetValue("Station", "VP2TCPPort", VP2TCPPort);
            ini.SetValue("Station", "VP2IPAddr", VP2IPAddr);
            ini.SetValue("Station", "WarnMultiple", WarnMultiple);
            ini.SetValue("Station", "RoundWindSpeed", RoundWindSpeed);
            ini.SetValue("Station", "VP2PeriodicDisconnectInterval", VP2PeriodicDisconnectInterval);
            ini.SetValue("Station", "EWtempdiff", EWtempdiff);
            ini.SetValue("Station", "EWpressurediff", EWpressurediff);
            ini.SetValue("Station", "EWhumiditydiff", EWhumiditydiff);
            ini.SetValue("Station", "EWgustdiff", EWgustdiff);
            ini.SetValue("Station", "EWwinddiff", EWwinddiff);
            ini.SetValue("Station", "EWmaxHourlyRain", EWmaxHourlyRain);
            ini.SetValue("Station", "EWmaxRainRate", EWmaxRainRate);

            ini.SetValue("Station", "EWminpressureMB", EWminpressureMB);
            ini.SetValue("Station", "EWmaxpressureMB", EWmaxpressureMB);

            ini.SetValue("Station", "RainSeasonStart", RainSeasonStart);
            ini.SetValue("Station", "RainDayThreshold", RainDayThreshold);

            ini.SetValue("Station", "ErrorLogSpikeRemoval", ErrorLogSpikeRemoval);

            //ini.SetValue("Station", "ImetBaudRate", ImetBaudRate);

            ini.SetValue("Station", "RG11portName", RG11Port);
            ini.SetValue("Station", "RG11TBRmode", RG11TBRmode);
            ini.SetValue("Station", "RG11tipsize", RG11tipsize);
            ini.SetValue("Station", "RG11IgnoreFirst", RG11IgnoreFirst);
            ini.SetValue("Station", "RG11DTRmode", RG11DTRmode);

            ini.SetValue("Station", "RG11portName2", RG11Port2);
            ini.SetValue("Station", "RG11TBRmode2", RG11TBRmode2);
            ini.SetValue("Station", "RG11tipsize2", RG11tipsize2);
            ini.SetValue("Station", "RG11IgnoreFirst2", RG11IgnoreFirst2);
            ini.SetValue("Station", "RG11DTRmode2", RG11DTRmode2);

            ini.SetValue("Web Site", "ForumURL", ForumURL);
            ini.SetValue("Web Site", "WebcamURL", WebcamURL);

            ini.SetValue("FTP site", "Host", ftp_host);
            ini.SetValue("FTP site", "Port", ftp_port);
            ini.SetValue("FTP site", "Username", ftp_user);
            ini.SetValue("FTP site", "Password", ftp_password);
            ini.SetValue("FTP site", "Directory", ftp_directory);

            ini.SetValue("FTP site", "AutoUpdate", WebAutoUpdate);
            ini.SetValue("FTP site", "ActiveFTP", ActiveFTPMode);
            ini.SetValue("FTP site", "Sslftp", Sslftp);
            ini.SetValue("FTP site", "FTPlogging", FTPlogging);
            ini.SetValue("FTP site", "UTF8encode", UTF8encode);
            ini.SetValue("FTP site", "EnableRealtime", RealtimeEnabled);
            ini.SetValue("FTP site", "RealtimeFTPEnabled", RealtimeFTPEnabled);
            ini.SetValue("FTP site", "RealtimeTxtFTP", RealtimeTxtFTP);
            ini.SetValue("FTP site", "RealtimeGaugesTxtFTP", RealtimeGaugesTxtFTP);
            ini.SetValue("FTP site", "RealtimeInterval", RealtimeInterval);
            ini.SetValue("FTP site", "UpdateInterval", UpdateInterval);
            ini.SetValue("FTP site", "IncludeSTD", IncludeStandardFiles);
            ini.SetValue("FTP site", "IncludeGraphDataFiles", IncludeGraphDataFiles);
            //ini.SetValue("FTP site", "IncludeSTDImages", IncludeStandardImages);
            //ini.SetValue("FTP site", "IncludeSolarChart", IncludeSolarChart);
            //ini.SetValue("FTP site", "IncludeUVChart", IncludeUVChart);
            //ini.SetValue("FTP site", "IncludeSunshineChart", IncludeSunshineChart);
            ini.SetValue("FTP site", "FTPRename", FTPRename);
            ini.SetValue("FTP site", "DeleteBeforeUpload", DeleteBeforeUpload);
            //ini.SetValue("FTP site", "ResizeGraphs", ResizeGraphs);
            //ini.SetValue("FTP site", "GraphHeight", GraphHeight);
            //ini.SetValue("FTP site", "GraphWidth", GraphWidth);
            //ini.SetValue("FTP site", "ImageFolder", ImageFolder);
            //ini.SetValue("FTP site", "ImageCopyRealtime", ImageCopyRealtime);

            for (int i = 0; i < numextrafiles; i++)
            {
                ini.SetValue("FTP site", "ExtraLocal" + i, ExtraFiles[i].local);
                ini.SetValue("FTP site", "ExtraRemote" + i, ExtraFiles[i].remote);
                ini.SetValue("FTP site", "ExtraProcess" + i, ExtraFiles[i].process);
                ini.SetValue("FTP site", "ExtraBinary" + i, ExtraFiles[i].binary);
                ini.SetValue("FTP site", "ExtraRealtime" + i, ExtraFiles[i].realtime);
                ini.SetValue("FTP site", "ExtraFTP" + i, ExtraFiles[i].FTP);
                ini.SetValue("FTP site", "ExtraUTF" + i, ExtraFiles[i].UTF8);
            }

            ini.SetValue("FTP site", "ExternalProgram", ExternalProgram);
            ini.SetValue("FTP site", "RealtimeProgram", RealtimeProgram);
            ini.SetValue("FTP site", "DailyProgram", DailyProgram);
            ini.SetValue("FTP site", "ExternalParams", ExternalParams);
            ini.SetValue("FTP site", "RealtimeParams", RealtimeParams);
            ini.SetValue("FTP site", "DailyParams", DailyParams);


            ini.SetValue("Station", "CloudBaseInFeet", CloudBaseInFeet);

            ini.SetValue("Wunderground", "ID", WundID);
            ini.SetValue("Wunderground", "Password", WundPW);
            ini.SetValue("Wunderground", "Enabled", WundEnabled);
            ini.SetValue("Wunderground", "RapidFire", WundRapidFireEnabled);
            ini.SetValue("Wunderground", "Interval", WundInterval);
            ini.SetValue("Wunderground", "SendUV", SendUVToWund);
            ini.SetValue("Wunderground", "SendSR", SendSRToWund);
            ini.SetValue("Wunderground", "SendIndoor", SendIndoorToWund);
            ini.SetValue("Wunderground", "SendAverage", WundSendAverage);
            ini.SetValue("Wunderground", "CatchUp", WundCatchUp);

            ini.SetValue("Awekas", "User", AwekasUser);
            ini.SetValue("Awekas", "Password", AwekasPW);
            ini.SetValue("Awekas", "Enabled", AwekasEnabled);
            ini.SetValue("Awekas", "Interval", AwekasInterval);
            ini.SetValue("Awekas", "SendUV", SendUVToAwekas);
            ini.SetValue("Awekas", "SendSR", SendSolarToAwekas);
            ini.SetValue("Awekas", "SendSoilTemp", SendSoilTempToAwekas);

            ini.SetValue("WeatherCloud", "Wid", WCloudWid);
            ini.SetValue("WeatherCloud", "Key", WCloudKey);
            ini.SetValue("WeatherCloud", "Enabled", WCloudEnabled);
            //ini.SetValue("WeatherCloud", "Interval", AwekasInterval);
            ini.SetValue("WeatherCloud", "SendUV", SendUVToWCloud);
            ini.SetValue("WeatherCloud", "SendSR", SendSolarToWCloud);

            ini.SetValue("Twitter", "User", Twitteruser);
            ini.SetValue("Twitter", "Password", TwitterPW);
            ini.SetValue("Twitter", "Enabled", TwitterEnabled);
            ini.SetValue("Twitter", "Interval", TwitterInterval);
            ini.SetValue("Twitter", "OauthToken", TwitterOauthToken);
            ini.SetValue("Twitter", "OauthTokenSecret", TwitterOauthTokenSecret);
            ini.SetValue("Twitter", "TwitterSendLocation", TwitterSendLocation);

            ini.SetValue("PWSweather", "ID", PWSID);
            ini.SetValue("PWSweather", "Password", PWSPW);
            ini.SetValue("PWSweather", "Enabled", PWSEnabled);
            ini.SetValue("PWSweather", "Interval", PWSInterval);
            ini.SetValue("PWSweather", "SendUV", SendUVToPWS);
            ini.SetValue("PWSweather", "SendSR", SendSRToPWS);
            ini.SetValue("PWSweather", "CatchUp", PWSCatchUp);

            ini.SetValue("WOW", "ID", WOWID);
            ini.SetValue("WOW", "Password", WOWPW);
            ini.SetValue("WOW", "Enabled", WOWEnabled);
            ini.SetValue("WOW", "Interval", WOWInterval);
            ini.SetValue("WOW", "SendUV", SendUVToWOW);
            ini.SetValue("WOW", "SendSR", SendSRToWOW);
            ini.SetValue("WOW", "CatchUp", WOWCatchUp);

            ini.SetValue("Weatherbug", "ID", WeatherbugID);
            ini.SetValue("Weatherbug", "Number", WeatherbugNumber);
            ini.SetValue("Weatherbug", "Password", WeatherbugPW);
            ini.SetValue("Weatherbug", "Enabled", WeatherbugEnabled);
            ini.SetValue("Weatherbug", "Interval", WeatherbugInterval);
            ini.SetValue("Weatherbug", "SendUV", SendUVToWeatherbug);
            ini.SetValue("Weatherbug", "SendSR", SendSRToWeatherbug);
            ini.SetValue("Weatherbug", "CatchUp", WeatherbugCatchUp);

            ini.SetValue("APRS", "ID", APRSID);
            ini.SetValue("APRS", "pass", APRSpass);
            ini.SetValue("APRS", "server", APRSserver);
            ini.SetValue("APRS", "port", APRSport);
            ini.SetValue("APRS", "Enabled", APRSenabled);
            ini.SetValue("APRS", "Interval", APRSinterval);
            ini.SetValue("APRS", "SendSR", SendSRToAPRS);


            //ini.SetValue("Alarms", "alarmlowtemp", alarmlowtemp);
            //ini.SetValue("Alarms", "LowTempAlarmSet", LowTempAlarmSet);
            //ini.SetValue("Alarms", "LowTempAlarmSound", LowTempAlarmSound);
            //ini.SetValue("Alarms", "LowTempAlarmSoundFile", LowTempAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhightemp", alarmhightemp);
            //ini.SetValue("Alarms", "HighTempAlarmSet", HighTempAlarmSet);
            //ini.SetValue("Alarms", "HighTempAlarmSound", HighTempAlarmSound);
            //ini.SetValue("Alarms", "HighTempAlarmSoundFile", HighTempAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmtempchange", alarmtempchange);
            //ini.SetValue("Alarms", "TempChangeAlarmSet", TempChangeAlarmSet);
            //ini.SetValue("Alarms", "TempChangeAlarmSound", TempChangeAlarmSound);
            //ini.SetValue("Alarms", "TempChangeAlarmSoundFile", TempChangeAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmlowpress", alarmlowpress);
            //ini.SetValue("Alarms", "LowPressAlarmSet", LowPressAlarmSet);
            //ini.SetValue("Alarms", "LowPressAlarmSound", LowPressAlarmSound);
            //ini.SetValue("Alarms", "LowPressAlarmSoundFile", LowPressAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighpress", alarmhighpress);
            //ini.SetValue("Alarms", "HighPressAlarmSet", HighPressAlarmSet);
            //ini.SetValue("Alarms", "HighPressAlarmSound", HighPressAlarmSound);
            //ini.SetValue("Alarms", "HighPressAlarmSoundFile", HighPressAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmpresschange", alarmpresschange);
            //ini.SetValue("Alarms", "PressChangeAlarmSet", PressChangeAlarmSet);
            //ini.SetValue("Alarms", "PressChangeAlarmSound", PressChangeAlarmSound);
            //ini.SetValue("Alarms", "PressChangeAlarmSoundFile", PressChangeAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighraintoday", alarmhighraintoday);
            //ini.SetValue("Alarms", "HighRainTodayAlarmSet", HighRainTodayAlarmSet);
            //ini.SetValue("Alarms", "HighRainTodayAlarmSound", HighRainTodayAlarmSound);
            //ini.SetValue("Alarms", "HighRainTodayAlarmSoundFile", HighRainTodayAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighrainrate", alarmhighrainrate);
            //ini.SetValue("Alarms", "HighRainRateAlarmSet", HighRainRateAlarmSet);
            //ini.SetValue("Alarms", "HighRainRateAlarmSound", HighRainRateAlarmSound);
            //ini.SetValue("Alarms", "HighRainRateAlarmSoundFile", HighRainRateAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighgust", alarmhighgust);
            //ini.SetValue("Alarms", "HighGustAlarmSet", HighGustAlarmSet);
            //ini.SetValue("Alarms", "HighGustAlarmSound", HighGustAlarmSound);
            //ini.SetValue("Alarms", "HighGustAlarmSoundFile", HighGustAlarmSoundFile);

            //ini.SetValue("Alarms", "alarmhighwind", alarmhighwind);
            //ini.SetValue("Alarms", "HighWindAlarmSet", HighWindAlarmSet);
            //ini.SetValue("Alarms", "HighWindAlarmSound", HighWindAlarmSound);
            //ini.SetValue("Alarms", "HighWindAlarmSoundFile", HighWindAlarmSoundFile);

            //ini.SetValue("Alarms", "SensorAlarmSet", SensorAlarmSet);
            //ini.SetValue("Alarms", "SensorAlarmSound", SensorAlarmSound);
            //ini.SetValue("Alarms", "SensorAlarmSoundFile", SensorAlarmSoundFile);

            ini.SetValue("Offsets", "PressOffset", PressOffset);
            ini.SetValue("Offsets", "TempOffset", TempOffset);
            ini.SetValue("Offsets", "HumOffset", HumOffset);
            ini.SetValue("Offsets", "WindDirOffset", WindDirOffset);
            ini.SetValue("Offsets", "InTempOffset", InTempoffset);
            ini.SetValue("Offsets", "UVOffset", UVOffset);
            ini.SetValue("Offsets", "WetBulbOffset", WetBulbOffset);
            //ini.SetValue("Offsets", "DavisCalcAltPressOffset", DavisCalcAltPressOffset);

            ini.SetValue("Offsets", "WindSpeedMult", WindSpeedMult);
            ini.SetValue("Offsets", "WindGustMult", WindGustMult);
            ini.SetValue("Offsets", "TempMult", TempMult);
            ini.SetValue("Offsets", "HumMult", HumMult);
            ini.SetValue("Offsets", "RainMult", RainMult);
            ini.SetValue("Offsets", "UVMult", UVMult);
            ini.SetValue("Offsets", "WetBulbMult", WetBulbMult);

            ini.SetValue("xAP", "Enabled", xapEnabled);
            ini.SetValue("xAP", "UID", xapUID);
            ini.SetValue("xAP", "Port", xapPort);

            ini.SetValue("Solar", "SunThreshold", SunThreshold);
            ini.SetValue("Solar", "RStransfactor", RStransfactor);
            ini.SetValue("Solar", "SolarMinimum", SolarMinimum);
            ini.SetValue("Solar", "UseBlakeLarsen", UseBlakeLarsen);
            ini.SetValue("Solar", "SolarCalc", SolarCalc);
            ini.SetValue("Solar", "BrasTurbidity", BrasTurbidity);


            ini.SetValue("NOAA", "Name", NOAAname);
            ini.SetValue("NOAA", "City", NOAAcity);
            ini.SetValue("NOAA", "State", NOAAstate);
            ini.SetValue("NOAA", "12hourformat", NOAA12hourformat);
            ini.SetValue("NOAA", "HeatingThreshold", NOAAheatingthreshold);
            ini.SetValue("NOAA", "CoolingThreshold", NOAAcoolingthreshold);
            ini.SetValue("NOAA", "MaxTempComp1", NOAAmaxtempcomp1);
            ini.SetValue("NOAA", "MaxTempComp2", NOAAmaxtempcomp2);
            ini.SetValue("NOAA", "MinTempComp1", NOAAmintempcomp1);
            ini.SetValue("NOAA", "MinTempComp2", NOAAmintempcomp2);
            ini.SetValue("NOAA", "RainComp1", NOAAraincomp1);
            ini.SetValue("NOAA", "RainComp2", NOAAraincomp2);
            ini.SetValue("NOAA", "RainComp3", NOAAraincomp3);
            ini.SetValue("NOAA", "AutoSave", NOAAAutoSave);
            ini.SetValue("NOAA", "AutoFTP", NOAAAutoFTP);
            ini.SetValue("NOAA", "MonthFileFormat", NOAAMonthFileFormat);
            ini.SetValue("NOAA", "YearFileFormat", NOAAYearFileFormat);
            ini.SetValue("NOAA", "FTPDirectory", NOAAFTPDirectory);
            ini.SetValue("NOAA", "NOAAUseUTF8", NOAAUseUTF8);

            ini.SetValue("NOAA", "NOAATempNormJan", NOAATempNormJan);
            ini.SetValue("NOAA", "NOAATempNormFeb", NOAATempNormFeb);
            ini.SetValue("NOAA", "NOAATempNormMar", NOAATempNormMar);
            ini.SetValue("NOAA", "NOAATempNormApr", NOAATempNormApr);
            ini.SetValue("NOAA", "NOAATempNormMay", NOAATempNormMay);
            ini.SetValue("NOAA", "NOAATempNormJun", NOAATempNormJun);
            ini.SetValue("NOAA", "NOAATempNormJul", NOAATempNormJul);
            ini.SetValue("NOAA", "NOAATempNormAug", NOAATempNormAug);
            ini.SetValue("NOAA", "NOAATempNormSep", NOAATempNormSep);
            ini.SetValue("NOAA", "NOAATempNormOct", NOAATempNormOct);
            ini.SetValue("NOAA", "NOAATempNormNov", NOAATempNormNov);
            ini.SetValue("NOAA", "NOAATempNormDec", NOAATempNormDec);

            ini.SetValue("NOAA", "NOAARainNormJan", NOAARainNormJan);
            ini.SetValue("NOAA", "NOAARainNormFeb", NOAARainNormFeb);
            ini.SetValue("NOAA", "NOAARainNormMar", NOAARainNormMar);
            ini.SetValue("NOAA", "NOAARainNormApr", NOAARainNormApr);
            ini.SetValue("NOAA", "NOAARainNormMay", NOAARainNormMay);
            ini.SetValue("NOAA", "NOAARainNormJun", NOAARainNormJun);
            ini.SetValue("NOAA", "NOAARainNormJul", NOAARainNormJul);
            ini.SetValue("NOAA", "NOAARainNormAug", NOAARainNormAug);
            ini.SetValue("NOAA", "NOAARainNormSep", NOAARainNormSep);
            ini.SetValue("NOAA", "NOAARainNormOct", NOAARainNormOct);
            ini.SetValue("NOAA", "NOAARainNormNov", NOAARainNormNov);
            ini.SetValue("NOAA", "NOAARainNormDec", NOAARainNormDec);

            ini.SetValue("Proxies", "HTTPProxyName", HTTPProxyName);
            ini.SetValue("Proxies", "HTTPProxyPort", HTTPProxyPort);
            ini.SetValue("Proxies", "HTTPProxyUser", HTTPProxyUser);
            ini.SetValue("Proxies", "HTTPProxyPassword", HTTPProxyPassword);

            ini.SetValue("Display", "NumWindRosePoints", NumWindRosePoints);

            ini.SetValue("Graphs", "ChartMaxDays", GraphDays);
            ini.SetValue("Graphs", "GraphHours", GraphHours);

            ini.SetValue("MySQL", "Host", MySqlHost);
            ini.SetValue("MySQL", "Port", MySqlPort);
            ini.SetValue("MySQL", "User", MySqlUser);
            ini.SetValue("MySQL", "Pass", MySqlPass);
            ini.SetValue("MySQL", "Database", MySqlDatabase);
            ini.SetValue("MySQL", "MonthlyMySqlEnabled", MonthlyMySqlEnabled);
            ini.SetValue("MySQL", "RealtimeMySqlEnabled", RealtimeMySqlEnabled);
            ini.SetValue("MySQL", "DayfileMySqlEnabled", DayfileMySqlEnabled);
            ini.SetValue("MySQL", "MonthlyTable", MySqlMonthlyTable);
            ini.SetValue("MySQL", "DayfileTable", MySqlDayfileTable);
            ini.SetValue("MySQL", "RealtimeTable", MySqlRealtimeTable);
            ini.SetValue("MySQL", "RealtimeRetention", MySqlRealtimeRetention);
            ini.SetValue("MySQL", "CustomMySqlSecondsCommandString", CustomMySqlSecondsCommandString);
            ini.SetValue("MySQL", "CustomMySqlMinutesCommandString", CustomMySqlMinutesCommandString);
            ini.SetValue("MySQL", "CustomMySqlRolloverCommandString", CustomMySqlRolloverCommandString);

            ini.SetValue("MySQL", "CustomMySqlSecondsEnabled", CustomMySqlSecondsEnabled);
            ini.SetValue("MySQL", "CustomMySqlMinutesEnabled", CustomMySqlMinutesEnabled);
            ini.SetValue("MySQL", "CustomMySqlRolloverEnabled", CustomMySqlRolloverEnabled);

            ini.SetValue("MySQL", "CustomMySqlSecondsInterval", CustomMySqlSecondsInterval);
            ini.SetValue("MySQL", "CustomMySqlMinutesIntervalIndex", CustomMySqlMinutesIntervalIndex);

            ini.SetValue("HTTP", "CustomHttpSecondsString", CustomHttpSecondsString);
            ini.SetValue("HTTP", "CustomHttpMinutesString", CustomHttpMinutesString);
            ini.SetValue("HTTP", "CustomHttpRolloverString", CustomHttpRolloverString);

            ini.SetValue("HTTP", "CustomHttpSecondsEnabled", CustomHttpSecondsEnabled);
            ini.SetValue("HTTP", "CustomHttpMinutesEnabled", CustomHttpMinutesEnabled);
            ini.SetValue("HTTP", "CustomHttpRolloverEnabled", CustomHttpRolloverEnabled);

            ini.SetValue("HTTP", "CustomHttpSecondsInterval", CustomHttpSecondsInterval);
            ini.SetValue("HTTP", "CustomHttpMinutesIntervalIndex", CustomHttpMinutesIntervalIndex);

            ini.Flush();
        }

    }
}
