using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;
using FluentFTP;

namespace FtpDataReporter
{
    public class FtpDataReporter : DataReporterBase
    {
        private FtpClient RealtimeFTP;
        public override string ServiceName => "FTP Reporter Service";

        public FtpDataReporterSettings Settings { get; private set; }

        public override string Identifier => "FtpReporter";
        #region Contsants
        private const string LOG_FILE_TAG = "<currentlogfile>";

        private static string DirectorySeparator = "/";
        string IndexTFile = "web" + DirectorySeparator + "indexT.htm";
        string TodayTFile = "web" + DirectorySeparator + "todayT.htm";
        string YesterdayTFile = "web" + DirectorySeparator + "yesterdayT.htm";
        string RecordTFile = "web" + DirectorySeparator + "recordT.htm";
        string TrendsTFile = "web" + DirectorySeparator + "trendsT.htm";
        string GaugesTFile = "web" + DirectorySeparator + "gaugesT.htm";
        string ThisMonthTFile = "web" + DirectorySeparator + "thismonthT.htm";
        string ThisYearTFile = "web" + DirectorySeparator + "thisyearT.htm";
        string MonthlyRecordTFile = "web" + DirectorySeparator + "monthlyrecordT.htm";
        string RealtimeGaugesTxtTFile = "web" + DirectorySeparator + "realtimegaugesT.txt";

        static string Indexfile = "web" + DirectorySeparator + "index.htm";
        static string Todayfile = "web" + DirectorySeparator + "today.htm";
        static string Yesterfile = "web" + DirectorySeparator + "yesterday.htm";
        static string Recordfile = "web" + DirectorySeparator + "record.htm";
        static string Trendsfile = "web" + DirectorySeparator + "trends.htm";
        static string Gaugesfile = "web" + DirectorySeparator + "gauges.htm";
        static string ThisMonthfile = "web" + DirectorySeparator + "thismonth.htm";
        static string ThisYearfile = "web" + DirectorySeparator + "thisyear.htm";
        static string MonthlyRecordfile = "web" + DirectorySeparator + "monthlyrecord.htm";
        string RealtimeGaugesTxtFile = "web" + DirectorySeparator + "realtimegauges.txt";

        private List<UploadFileDetails> WebTextFiles = new List<UploadFileDetails>()
        {
            new UploadFileDetails() { local = Indexfile, remote = "index.htm"},
            new UploadFileDetails() { local = Todayfile, remote = "today.htm"},
            new UploadFileDetails() { local = Yesterfile, remote = "yesterday.htm"},
            new UploadFileDetails() { local = Recordfile, remote = "record.htm"},
            new UploadFileDetails() { local = Trendsfile, remote = "trends.htm"},
            new UploadFileDetails() { local = Gaugesfile, remote = "gauges.htm"},
            new UploadFileDetails() { local = ThisMonthfile, remote = "thismonth.htm"},
            new UploadFileDetails() { local = ThisYearfile, remote = "thisyear.htm"},
            new UploadFileDetails() { local = MonthlyRecordfile, remote = "monthlyrecord.htm"}
        };

        private List<UploadFileDetails> GraphDataFiles = new List<UploadFileDetails>()
        {
            new UploadFileDetails() { local = "web" + DirectorySeparator + "graphconfig.json", remote = "graphconfig.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "tempdata.json", remote = "tempdata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "pressdata.json", remote = "pressdata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "winddata.json", remote = "winddata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "wdirdata.json", remote = "wdirdata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "humdata.json", remote = "humdata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "raindata.json", remote = "raindata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "solardata.json", remote = "solardata.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "dailyrain.json", remote = "dailyrain.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "sunhours.json", remote = "sunhours.json"},
            new UploadFileDetails() { local = "web" + DirectorySeparator + "dailytemp.json", remote = "dailytemp.json"}
        };
        #endregion

        private List<UploadFileDetails> ExtraFiles;

        public FtpDataReporter(ILogger logger, FtpDataReporterSettings settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            Settings = settings;
            ExtraFiles = Settings.ExtraFiles;
        }

        protected CancellationTokenSource _realtimeCts;
        private int RealtimeInterval;
        public override void Initialise()
        {
            if (Settings.RealtimeEnabled)
            {
                RealtimeFTP = new FtpClient();
                RealtimeInterval = Settings.RealtimeInterval;
                RealtimeFtpLogin();

                _realtimeCts = new CancellationTokenSource();
                _log.Info($"Starting realtime FTP data reporter background task");
                _backgroundTask = Task.Factory.StartNew(() =>
                        RealtimeWorker(this, null)
                    , _realtimeCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);

            }
        }

        public override void DoReport(IWeatherDataStatistics currentData)
        {
            var connection = CreateConnection();
            if (_dataObject == null)
                _dataObject = currentData;  // Cache the data object - for realtime use
            
            UploadRegularFiles(currentData,connection);
            connection.Dispose();
        }

        private FtpClient CreateConnection()
        {
            FtpClient conn = new FtpClient();

            _log.Info("CumulusMX Connecting to " + Settings.Host);
            conn.Host = Settings.Host;
            conn.Port = Settings.Port;
            conn.Credentials = new NetworkCredential(Settings.User, Settings.Password);

            if (Settings.UseSsl)
            {
                conn.EncryptionMode = FtpEncryptionMode.Explicit;
                conn.DataConnectionEncryption = true;
                conn.ValidateCertificate += Client_ValidateCertificate;
                
                conn.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
            }

            if (Settings.ActiveFtp)
            {
                conn.DataConnectionType = FtpDataConnectionType.PORT;
            }
            else if (Settings.DisableEpsv)
            {
                conn.DataConnectionType = FtpDataConnectionType.PASV;
            }

            try
            {
                conn.Connect();
            }
            catch (Exception ex)
            {
                _log.Info("Error connecting ftp - " + ex.Message);
            }

            conn.EnableThreadSafeDataConnections = false; // use same connection for all transfers
            return conn;
        }

        void Client_ValidateCertificate(FtpClient control, FtpSslValidationEventArgs e)
        {
            e.Accept = true; // Allow all
        }

        private void RealtimeFtpLogin()
        {
            //RealtimeTimer.Enabled = false;
            RealtimeFTP.Host = Settings.Host;
            RealtimeFTP.Port = Settings.Port;
            RealtimeFTP.Credentials = new NetworkCredential(Settings.User, Settings.Password);
            // b3045 - Reduce the default polling interval to try and keep the session alive
            RealtimeFTP.SocketKeepAlive = true;
            //RealtimeFTP.SocketPollInterval = 2000; // 2 seconds, defaults to 15 seconds

            if (Settings.UseSsl)
            {
                RealtimeFTP.EncryptionMode = FtpEncryptionMode.Explicit;
                RealtimeFTP.DataConnectionEncryption = true;
                RealtimeFTP.ValidateCertificate += Client_ValidateCertificate;
                // b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specifiy protocols
                RealtimeFTP.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;

            }

            if (!string.IsNullOrWhiteSpace(Settings.Host))
            {
                _log.Info("Attempting realtime FTP connect");
                try
                {
                    RealtimeFTP.Connect();
                }
                catch (Exception ex)
                {
                    _log.Info("Error connecting ftp - " + ex.Message);
                }

                RealtimeFTP.EnableThreadSafeDataConnections = false; // use same connection for all transfers
            }
            //RealtimeTimer.Enabled = true;
        }

        private void UploadFile(FtpClient conn, string localFile, string remoteFile)
        {
            
            _log.Info(" Uploading " + localFile + " to " + remoteFile);

            Stream inputStream = Stream.Null;
            inputStream = new FileStream(localFile, FileMode.Open);
            UploadStream(conn, remoteFile, inputStream, localFile);

            inputStream.Dispose();
        }

        private void UploadString(FtpClient connection, string content, string remoteFile)
        {

            _log.Info(" Uploading to " + remoteFile);

            UploadStream(connection, remoteFile,
                new MemoryStream(Encoding.UTF8.GetBytes(content)), remoteFile);
        }

        private void UploadStream(FtpClient conn, string remoteFile, Stream inputStream, string description)
        {
            try
            {
                string uploadFilename = Settings.RenameFiles ? remoteFile + "tmp" : remoteFile;

                if (Settings.DeleteBeforeUpload)
                {
                    // delete the existing file
                    try
                    {
                        conn.DeleteFile(remoteFile);
                    }
                    catch (Exception ex)
                    {
                        _log.Info("FTP error deleting " + remoteFile + " : " + ex.Message);
                    }
                }

                using (Stream outputStream = conn.OpenWrite(uploadFilename))
                {
                    try
                    {
                        var buffer = new byte[4096];
                        int read;
                        while ((read = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outputStream.Write(buffer, 0, read);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Info("Error uploading " + description + " to " + remoteFile + " : " + ex.Message);
                    }
                    finally
                    {
                        outputStream.Close();
                        inputStream.Close();
                        conn.GetReply(); // required FluentFTP 19.2
                    }
                }

                if (Settings.RenameFiles)
                {
                    // rename the file
                    conn.Rename(uploadFilename, remoteFile);
                }

                _log.Info( "Completed uploading " + description + " to " + remoteFile);
            }
            catch (Exception ex)
            {
                _log.Info("Error uploading " + description + " to " + remoteFile + " : " + ex.Message);
            }
        }

        internal async void RealtimeWorker(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (!RealtimeInProgress && _dataObject != null)
                    {
                        try
                        {
                            if (!RealtimeFTP.IsConnected)
                            {
                                try
                                {
                                    _log.Debug("Realtime ftp not connected - reconnecting");
                                    RealtimeFTP.Connect();
                                }
                                catch (Exception ex)
                                {
                                    _log.Info("Error connecting ftp - " + ex.Message);
                                }

                                //RealtimeFTP.EnableThreadSafeDataConnections = false; // use same connection for all transfers
                            }

                            try
                            {
                                _log.Debug("Uploading realtime files");
                                UploadRealtimeFiles(_dataObject, RealtimeFTP); //TODO: Pass data file
                            }
                            catch (Exception ex)
                            {
                                _log.Info("Error during realtime update: " + ex.Message);
                            }

                            if (!string.IsNullOrEmpty(Settings.RealtimeProgram))
                            {
                                //_log.Debug("Execute realtime program");
                                ExecuteProgram(Settings.RealtimeProgram, Settings.RealtimeParams);
                            }
                        }
                        finally
                        {
                            RealtimeInProgress = false;
                        }
                    }

                    await Task.Delay(RealtimeInterval);
                }
            }
            catch (ThreadAbortException)
            {
            }
        }

        private bool RealtimeInProgress;
        private bool WebUpdating;
        private IWeatherDataStatistics _dataObject;

        private void UploadRealtimeFiles(IWeatherDataStatistics statistics, FtpClient connection)
        {
            /*
			Example: 18/10/08 16:03:45 8.4 84 5.8 24.2 33.0 261 0.0 1.0 999.7 W 6 mph C mb mm 146.6 +0.1 85.2 588.4 11.6 20.3 57 3.6 -0.7 10.9 12:00 7.8 14:41 37.4 14:38 44.0 14:28 999.8 16:01 998.4 12:06 1.8.2 448 36.0 10.3 10.5 0

			Field  Example    Description
			1      18/10/08   date (always dd/mm/yy)
			2      16:03:45   time (always hh:mm:ss)
			3      8.4        outside temperature
			4      84         relative humidity
			5      5.8        dewpoint
			6      24.2       wind speed (average)
			7      33.0       latest wind speed
			8      261        wind bearing
			9      0.0        current rain rate
			10     1.0        rain today
			11     999.7      barometer
			12     W          wind direction
			13     6          wind speed (beaufort)
			14     mph        wind units
			15     C          temperature units
			16     mb         pressure units
			17     mm         rain units
			18     146.6      wind run (today)
			19     +0.1       pressure trend value
			20     85.2       monthly rain
			21     588.4      yearly rain
			22     11.6       yesterday's rainfall
			23     20.3       inside temperature
			24     57         inside humidity
			25     3.6        wind chill
			26     -0.7       temperature trend value
			27     10.9       today's high temp
			28     12:00      time of today's high temp (hh:mm)
			29     7.8        today's low temp
			30     14:41      time of today's low temp (hh:mm)
			31     37.4       today's high wind speed (average)
			32     14:38      time of today's high wind speed (average) (hh:mm)
			33     44.0       today's high wind gust
			34     14:28      time of today's high wind gust (hh:mm)
			35     999.8      today's high pressure
			36     16:01      time of today's high pressure (hh:mm)
			37     998.4      today's low pressure
			38     12:06      time of today's low pressure (hh:mm)
			39     1.8.2      Cumulus version
			40     448        Cumulus build
			41     36.0       10-minute high gust
			42     10.3       heat index
			43     10.5       humidex
			44                UV
			45                ET
			46                Solar radiation
			47     234        Average Bearing (degrees)
			48     2.5        Rain last hour
			49     5          Forecast number
			50     1          Is daylight? (1 = yes)
			51     0          Sensor contact lost (1 = yes)
			52     NNW        wind direction (average)
			53     2040       Cloudbase
			54     ft         Cloudbase units
			55     12.3       Apparent Temp
			56     11.4       Sunshine hours today
			57     420        Current theoretical max solar radiation
			58     1          Is sunny?
		  */ 
		      
		      
            //var filename = Path.Combine(AppDir, RealtimeFile);
            //DateTime timestamp = DateTime.Now;

            var renderer = new TemplateRenderer(File.OpenText("Realtime.StringTemplate"),statistics, Settings, new Dictionary<string, object>(), _log);
            var realtimeFileContent = renderer.Render();
            var realtimeFileRemote = Path.Combine(Settings.FtpDirectory, "realtime.txt");
            UploadString(connection,realtimeFileContent, realtimeFileRemote);

            var gaugesRenderer = new TemplateRenderer(File.OpenText("RealtimeGauges.StringTemplate"), statistics, Settings, new Dictionary<string, object>(), _log);
            var gaugesRealtimeFileContent = renderer.Render();
            var gaugesRealtimeFileRemote = Path.Combine(Settings.FtpDirectory, "realtimegauges.txt");
            UploadString(connection, realtimeFileContent, realtimeFileRemote);

            foreach (var extraFile in ExtraFiles)
            {
                if (!extraFile.realtime) continue;

                var uploadfile = extraFile.local;
                var remotefile = extraFile.remote;
                if (uploadfile == LOG_FILE_TAG)
                {
                    uploadfile = _log.GetFileName();
                    remotefile = remotefile.Replace(LOG_FILE_TAG, Path.GetFileName(uploadfile));
                }

                string content;
                if (extraFile.process)
                {
                    var parser = new TemplateRenderer(File.OpenText(uploadfile), statistics, Settings,
                        new Dictionary<string, object>(), _log);
                    content = parser.Render();
                }
                else
                {
                    content = File.ReadAllText(uploadfile);
                }

                if (extraFile.FTP && connection != null)
                {
                    UploadString(connection, content, remotefile);
                }

                if (Settings.SaveLocal)
                {
                    File.WriteAllText(Path.Combine(Settings.LocalSavePath, Path.GetFileName(remotefile)), content);
                }
            }

            //TODO: This should be in the SQL Reporter - not the FTP reporter
            /*if (Settings.RealtimeEnabled)
            {
                var sqlRenderer = new TemplateRenderer("RealtimeSql.StringTemplate", statistics, Settings, new Dictionary<string, object>(), _log);

                string queryString = sqlRenderer.Render();


                // do the update
                MySqlCommand cmd = new MySqlCommand();
                cmd.CommandText = queryString;
                cmd.Connection = RealtimeSqlConn;
                //_log.Info(queryString);

                try
                {
                    RealtimeSqlConn.Open();
                    int aff = cmd.ExecuteNonQuery();
                    //_log.Info("MySQL: " + aff + " rows were affected.");
                }
                catch (Exception ex)
                {
                    _log.Info("Error encountered during Realtime MySQL operation.");
                    _log.Info(ex.Message);
                }
                finally
                {
                    RealtimeSqlConn.Close();
                }

                if (!string.IsNullOrEmpty(MySqlRealtimeRetention))
                {
                    // delete old entries
                    cmd.CommandText = "DELETE IGNORE FROM " + MySqlRealtimeTable + " WHERE LogDateTime < DATE_SUB('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', INTERVAL " +
                                      MySqlRealtimeRetention + ")";
                    //_log.Info(queryString);

                    try
                    {
                        RealtimeSqlConn.Open();
                        int aff = cmd.ExecuteNonQuery();
                        //_log.Info("MySQL: " + aff + " rows were affected.");
                    }
                    catch (Exception ex)
                    {
                        _log.Info("Error encountered during Realtime delete MySQL operation.");
                        _log.Info(ex.Message);
                    }
                    finally
                    {
                        RealtimeSqlConn.Close();
                    }
                }
            }*/
        }

        public void UploadRegularFiles(IWeatherDataStatistics statistics,FtpClient connection)
        {
            WebUpdating = true;

            if (!Settings.RealtimeEnabled)
            {
                UploadRealtimeFiles(statistics,connection);
            }

            foreach (var ftpFile in WebTextFiles)
            {
                string content;
                if (ftpFile.process)
                {
                    var parser = new TemplateRenderer(File.OpenText(ftpFile.local), statistics, Settings,
                        new Dictionary<string, object>(), _log);
                    content = parser.Render();
                }
                else
                {
                    content = File.ReadAllText(ftpFile.local);
                }

                if (connection != null)
                {
                    UploadString(connection, ftpFile.remote,content);
                }

                if (Settings.SaveLocal)
                {
                    File.WriteAllText(Path.Combine(Settings.LocalSavePath, Path.GetFileName(ftpFile.remote)), content);
                }
            }
            _log.Debug("Done creating standard HTML files");

            if (Settings.IncludeGraphDataFiles)
            {
                foreach (var graphFile in GraphDataFiles)
                {
                    var parser = new TemplateRenderer(File.OpenText(graphFile.local), statistics, Settings, new Dictionary<string, object>(), _log);
                    string content = parser.Render();
                    if (connection != null)
                    {
                        UploadString(connection, graphFile.remote, content);
                    }

                    if (Settings.SaveLocal)
                    {
                        File.WriteAllText(Path.Combine(Settings.LocalSavePath, Path.GetFileName(graphFile.remote)), content);
                    }
                }
            }
            _log.Debug("Done creating graph data files.");
            
            foreach (var extraFile in ExtraFiles)
            {
                if (extraFile.realtime) continue;

                var uploadfile = extraFile.local;
                var remotefile = extraFile.remote;
                if (uploadfile == LOG_FILE_TAG)
                {
                    uploadfile = _log.GetFileName();
                    remotefile = remotefile.Replace(LOG_FILE_TAG, Path.GetFileName(uploadfile));
                }

                string content;
                if (extraFile.process)
                {
                    var parser = new TemplateRenderer(File.OpenText(uploadfile), statistics, Settings,
                        new Dictionary<string, object>(), _log);
                    content = parser.Render();
                }
                else
                {
                    content = File.ReadAllText(uploadfile);
                }

                if (extraFile.FTP && connection != null)
                {
                    UploadString(connection,content,remotefile);
                }

                if (Settings.SaveLocal)
                {
                    File.WriteAllText(Path.Combine(Settings.LocalSavePath, Path.GetFileName(remotefile)),content);
                }
            }

            if (!string.IsNullOrEmpty(Settings.ExternalProgram))
            {
                _log.Debug("Executing program " + Settings.ExternalProgram + " " + Settings.ExternalParams);
                try
                {
                    
                    ExecuteProgram(Settings.ExternalProgram, Settings.ExternalParams);
                    _log.Debug("External program started");
                }
                catch (Exception ex)
                {
                    _log.Info("Error starting external program: " + ex.Message);
                }
            }
            
            WebUpdating = false;
        }

        private void ExecuteProgram(string externalProgram, string externalParams)
        {
            throw new NotImplementedException();
        }
    }
}
