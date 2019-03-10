using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace FtpDataReporter
{
    public class FtpDataReporter : IDataReporter
    {
        private ILogger _logger;
        public string ServiceName => "FTP Reporter Service";

        public IDataReporterSettings Settings { get; private set; }

        public string Identifier => "TBC"; //TODO

        public FtpDataReporter()
        {
            //TODO: Implement
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }

        public void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        /* private void DoFTPLogin()
        {
            using (FtpClient conn = new FtpClient())
            {
                FtpTrace.WriteLine(""); // insert a blank line
                FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " CumulusMX Connecting to " + ftp_host);
                conn.Host = ftp_host;
                conn.Port = ftp_port;
                conn.Credentials = new NetworkCredential(ftp_user, ftp_password);

                if (Sslftp)
                {
                    conn.EncryptionMode = FtpEncryptionMode.Explicit;
                    conn.DataConnectionEncryption = true;
                    conn.ValidateCertificate += Client_ValidateCertificate;
                    // b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specifiy protocols
                    conn.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;
                }

                if (ActiveFTPMode)
                {
                    conn.DataConnectionType = FtpDataConnectionType.PORT;
                }
                else if (DisableEPSV)
                {
                    conn.DataConnectionType = FtpDataConnectionType.PASV;
                }

                try
                {
                    conn.Connect();
                }
                catch (Exception ex)
                {
                    LogMessage("Error connecting ftp - " + ex.Message);
                }

                conn.EnableThreadSafeDataConnections = false; // use same connection for all transfers

                if (conn.IsConnected)
                {
                    string remotePath = (ftp_directory.EndsWith("/") ? ftp_directory : ftp_directory + "/");
                    if (NOAANeedFTP)
                    {
                        // upload NOAA reports
                        LogMessage("Uploading NOAA reports");
                        FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Uploading NOAA reports");

                        var uploadfile = ReportPath + NOAALatestMonthlyReport;
                        var remotefile = NOAAFTPDirectory + '/' + NOAALatestMonthlyReport;

                        UploadFile(conn, uploadfile, remotefile);

                        uploadfile = ReportPath + NOAALatestYearlyReport;
                        remotefile = NOAAFTPDirectory + '/' + NOAALatestYearlyReport;

                        UploadFile(conn, uploadfile, remotefile);

                        NOAANeedFTP = false;
                    }

                    //LogDebugMessage("Uploading extra files");
                    // Extra files
                    for (int i = 0; i < numextrafiles; i++)
                    {
                        var uploadfile = ExtraFiles[i].local;
                        var remotefile = ExtraFiles[i].remote;

                        if (uploadfile == "<currentlogfile>")
                        {
                            uploadfile = GetLogFileName(DateTime.Now);
                        }

                        remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));

                        if ((uploadfile != "") && (File.Exists(uploadfile)) && (remotefile != "") && !ExtraFiles[i].realtime && ExtraFiles[i].FTP)
                        {
                            // all checks OK, file needs to be uploaded
                            if (ExtraFiles[i].process)
                            {
                                // we've already processed the file
                                uploadfile = uploadfile + "tmp";
                            }

                            UploadFile(conn, uploadfile, remotefile);
                        }
                    }
                    //LogDebugMessage("Done uploading extra files");
                    // standard files
                    if (IncludeStandardFiles)
                    {
                        //LogDebugMessage("Uploading standard files");
                        FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Uploading standard files");
                        try
                        {
                            for (int i = 0; i < numwebtextfiles; i++)
                            {
                                var uploadfile = localwebtextfiles[i];
                                var remotefile = remotePath + remotewebtextfiles[i];

                                UploadFile(conn, uploadfile, remotefile);
                            }
                            //LogDebugMessage("Done uploading standard files");
                        }
                        catch (Exception e)
                        {
                            LogMessage(e.Message);
                        }
                    }

                    if (IncludeGraphDataFiles)
                    {
                        try
                        {
                            //LogDebugMessage("Uploading graph data files");
                            FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Uploading graph data files");
                            for (int i = 0; i < localgraphdatafiles.Length; i++)
                            {
                                var uploadfile = localgraphdatafiles[i];
                                var remotefile = remotePath + remotegraphdatafiles[i];

                                UploadFile(conn, uploadfile, remotefile);
                            }
                            //LogDebugMessage("Done uploading graph data files");
                        }
                        catch (Exception e)
                        {
                            LogMessage(e.Message);
                        }
                    }
                }

                // b3045 - dispose of connection
                conn.Disconnect();
                FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Disconnected from " + ftp_host);
            }
        }

        private void RealtimeFTPLogin()
        {
            //RealtimeTimer.Enabled = false;
            RealtimeFTP.Host = ftp_host;
            RealtimeFTP.Port = ftp_port;
            RealtimeFTP.Credentials = new NetworkCredential(ftp_user, ftp_password);
            // b3045 - Reduce the default polling interval to try and keep the session alive
            RealtimeFTP.SocketKeepAlive = true;
            //RealtimeFTP.SocketPollInterval = 2000; // 2 seconds, defaults to 15 seconds


            if (Sslftp)
            {
                RealtimeFTP.EncryptionMode = FtpEncryptionMode.Explicit;
                RealtimeFTP.DataConnectionEncryption = true;
                RealtimeFTP.ValidateCertificate += Client_ValidateCertificate;
                // b3045 - switch from System.Net.Ftp.Client to FluentFTP allows us to specifiy protocols
                RealtimeFTP.SslProtocols = SslProtocols.Default | SslProtocols.Tls11 | SslProtocols.Tls12;

            }


            if (ftp_host != "" && ftp_host != " ")
            {
                LogMessage("Attempting realtime FTP connect");
                try
                {
                    RealtimeFTP.Connect();
                }
                catch (Exception ex)
                {
                    LogMessage("Error connecting ftp - " + ex.Message);
                }

                RealtimeFTP.EnableThreadSafeDataConnections = false; // use same connection for all transfers
            }
            //RealtimeTimer.Enabled = true;
        }

        private void UploadFile(FtpClient conn, string localfile, string remotefile)
        {
            string remotefilename = FTPRename ? remotefile + "tmp" : remotefile;

            FtpTrace.WriteLine("");
            FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Uploading " + localfile + " to " + remotefile);

            try
            {
                if (DeleteBeforeUpload)
                {
                    // delete the existing file
                    try
                    {
                        conn.DeleteFile(remotefile);
                    }
                    catch (Exception ex)
                    {
                        LogMessage("FTP error deleting " + remotefile + " : " + ex.Message);
                    }
                }

                using (Stream ostream = conn.OpenWrite(remotefilename))
                using (Stream istream = new FileStream(localfile, FileMode.Open))
                {
                    try
                    {
                        var buffer = new byte[4096];
                        int read;
                        while ((read = istream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ostream.Write(buffer, 0, read);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage("Error uploading " + localfile + " to " + remotefile + " : " + ex.Message);
                    }
                    finally
                    {
                        ostream.Close();
                        istream.Close();
                        conn.GetReply(); // required FluentFTP 19.2
                    }
                }


                if (FTPRename)
                {
                    // rename the file
                    conn.Rename(remotefilename, remotefile);
                }

                FtpTrace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Completed uploading " + localfile + " to " + remotefile);
            }
            catch (Exception ex)
            {
                LogMessage("Error uploading " + localfile + " to " + remotefile + " : " + ex.Message);
            }
        }


        internal void RealtimeTimerTick(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (!RealtimeInProgress)
            {
                try
                {
                    RealtimeInProgress = true;
                    if (RealtimeFTPEnabled)
                    {
                        if (!RealtimeFTP.IsConnected)
                        {
                            try
                            {
                                LogDebugMessage("Realtime ftp not connected - reconnecting");
                                RealtimeFTP.Connect();
                            }
                            catch (Exception ex)
                            {
                                LogMessage("Error connecting ftp - " + ex.Message);
                            }

                            //RealtimeFTP.EnableThreadSafeDataConnections = false; // use same connection for all transfers
                        }

                        try
                        {
                            //LogDebugMessage("Create realtime file");
                            CreateRealtimeFile();
                            //LogDebugMessage("Create extra realtime files");
                            CreateRealtimeHTMLfiles();
                            //LogDebugMessage("Upload realtime files");
                            RealtimeFTPUpload();
                        }
                        catch (Exception ex)
                        {
                            LogMessage("Error during realtime update: " + ex.Message);
                        }
                    }
                    else
                    {
                        // No FTP, just process files
                        CreateRealtimeFile();
                        CreateRealtimeHTMLfiles();
                    }

                    if (!string.IsNullOrEmpty(RealtimeProgram))
                    {
                        //LogDebugMessage("Execute realtime program");
                        ExecuteProgram(RealtimeProgram, RealtimeParams);
                    }
                }
                finally
                {
                    RealtimeInProgress = false;
                }
            }
        }

        private void RealtimeFTPUpload()
        {
            // realtime.txt
            string filepath, gaugesfilepath;

            if (ftp_directory == "")
            {
                filepath = "realtime.txt";
                gaugesfilepath = "realtimegauges.txt";
            }
            else
            {
                filepath = ftp_directory + "/realtime.txt";
                gaugesfilepath = ftp_directory + "/realtimegauges.txt";
            }

            if (RealtimeTxtFTP)
            {
                UploadFile(RealtimeFTP, RealtimeFile, filepath);
            }

            if (RealtimeGaugesTxtFTP)
            {
                ProcessTemplateFile(RealtimeGaugesTxtTFile, RealtimeGaugesTxtFile, realtimeTokenParser);
                UploadFile(RealtimeFTP, RealtimeGaugesTxtFile, gaugesfilepath);
            }

            // Extra files
            for (int i = 0; i < numextrafiles; i++)
            {
                var uploadfile = ExtraFiles[i].local;
                var remotefile = ExtraFiles[i].remote;

                if (uploadfile == "<currentlogfile>")
                {
                    uploadfile = GetLogFileName(DateTime.Now);
                }

                remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));

                if ((uploadfile != "") && (File.Exists(uploadfile)) && (remotefile != "") && ExtraFiles[i].realtime && ExtraFiles[i].FTP)
                {
                    // all checks OK, file needs to be uploaded
                    if (ExtraFiles[i].process)
                    {
                        // we've already processed the file
                        uploadfile = uploadfile + "tmp";
                    }

                    UploadFile(RealtimeFTP, uploadfile, remotefile);
                }
            }
        }

        private void CreateRealtimeHTMLfiles()
        {
            for (int i = 0; i < numextrafiles; i++)
            {
                if (ExtraFiles[i].realtime)
                {
                    var uploadfile = ExtraFiles[i].local;
                    if (uploadfile == "<currentlogfile>")
                    {
                        uploadfile = GetLogFileName(DateTime.Now);
                    }
                    var remotefile = ExtraFiles[i].remote;
                    remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));

                    if ((uploadfile != "") && (File.Exists(uploadfile)) && (remotefile != ""))
                    {
                        if (ExtraFiles[i].process)
                        {
                            // process the file
                            var utf8WithoutBom = new System.Text.UTF8Encoding(false);
                            var encoding = UTF8encode ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
                            realtimeTokenParser.encoding = encoding;
                            realtimeTokenParser.SourceFile = uploadfile;
                            var output = realtimeTokenParser.ToString();
                            uploadfile += "tmp";
                            using (StreamWriter file = new StreamWriter(uploadfile, false, encoding))
                            {
                                file.Write(output);

                                file.Close();
                            }
                        }

                        if (!ExtraFiles[i].FTP)
                        {
                            // just copy the file
                            try
                            {
                                File.Copy(uploadfile, remotefile, true);
                            }
                            catch (Exception ex)
                            {
                                LogDebugMessage("Copying extra realtime file: " + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        private void CreateRealtimeFile()
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
		      /*
		      
            var filename = AppDir + RealtimeFile;
            DateTime timestamp = DateTime.Now;

            using (StreamWriter file = new StreamWriter(filename, false))
            {
                file.Write(timestamp.ToString("dd/MM/yy HH:mm:ss "));
                file.Write(ReplaceCommas(station.OutdoorTemperature.ToString(TempFormat)) + ' '); // 3
                file.Write(station.OutdoorHumidity.ToString() + ' '); // 4
                file.Write(ReplaceCommas(station.OutdoorDewpoint.ToString(TempFormat)) + ' '); // 5
                file.Write(ReplaceCommas(station.WindAverage.ToString(WindFormat)) + ' '); // 6
                file.Write(ReplaceCommas(station.WindLatest.ToString(WindFormat)) + ' '); // 7
                file.Write(station.Bearing.ToString() + ' '); // 8
                file.Write(ReplaceCommas(station.RainRate.ToString(RainFormat)) + ' '); // 9
                file.Write(ReplaceCommas(station.RainToday.ToString(RainFormat)) + ' '); // 10
                file.Write(ReplaceCommas(station.Pressure.ToString(PressFormat)) + ' '); // 11
                file.Write(station.CompassPoint(station.Bearing) + ' '); // 12
                file.Write(Beaufort(station.WindAverage) + ' '); // 13
                file.Write(WindUnitText + ' '); // 14
                file.Write(TempUnitText[1].ToString() + ' '); // 15
                file.Write(PressUnitText + ' '); // 16
                file.Write(RainUnitText + ' '); // 17
                file.Write(ReplaceCommas(station.WindRunToday.ToString(WindRunFormat)) + ' '); // 18
                if (station.presstrendval > 0)
                    file.Write(ReplaceCommas('+' + station.presstrendval.ToString(PressFormat)) + ' '); // 19
                else
                    file.Write(ReplaceCommas(station.presstrendval.ToString(PressFormat)) + ' ');
                file.Write(ReplaceCommas(station.RainMonth.ToString(RainFormat)) + ' '); // 20
                file.Write(ReplaceCommas(station.RainYear.ToString(RainFormat)) + ' '); // 21
                file.Write(ReplaceCommas(station.RainYesterday.ToString(RainFormat)) + ' '); // 22
                file.Write(ReplaceCommas(station.IndoorTemperature.ToString(TempFormat)) + ' '); // 23
                file.Write(station.IndoorHumidity.ToString() + ' '); // 24
                file.Write(ReplaceCommas(station.WindChill.ToString(TempFormat)) + ' '); // 25
                file.Write(ReplaceCommas(station.temptrendval.ToString(TempTrendFormat)) + ' '); // 26
                file.Write(ReplaceCommas(station.HighTempToday.ToString(TempFormat)) + ' '); // 27
                file.Write(station.hightemptodaytime.ToString("HH:mm") + ' '); // 28
                file.Write(ReplaceCommas(station.LowTempToday.ToString(TempFormat)) + ' '); // 29
                file.Write(station.lowtemptodaytime.ToString("HH:mm") + ' '); // 30
                file.Write(ReplaceCommas(station.highwindtoday.ToString(WindFormat)) + ' '); // 31
                file.Write(station.highwindtodaytime.ToString("HH:mm") + ' '); // 32
                file.Write(ReplaceCommas(station.highgusttoday.ToString(WindFormat)) + ' '); // 33
                file.Write(station.highgusttodaytime.ToString("HH:mm") + ' '); // 34
                file.Write(ReplaceCommas(station.highpresstoday.ToString(PressFormat)) + ' '); // 35
                file.Write(station.highpresstodaytime.ToString("HH:mm") + ' '); // 36
                file.Write(ReplaceCommas(station.lowpresstoday.ToString(PressFormat)) + ' '); // 37
                file.Write(station.lowpresstodaytime.ToString("HH:mm") + ' '); // 38
                file.Write(Version + ' '); // 39
                file.Write(Build + ' '); // 40
                file.Write(ReplaceCommas(station.RecentMaxGust.ToString(WindFormat)) + ' '); // 41
                file.Write(ReplaceCommas(station.HeatIndex.ToString(TempFormat)) + ' '); // 42
                file.Write(ReplaceCommas(station.Humidex.ToString(TempFormat)) + ' '); // 43
                file.Write(ReplaceCommas(station.UV.ToString(UVFormat)) + ' '); // 44
                file.Write(ReplaceCommas(station.ET.ToString(ETFormat)) + ' '); // 45
                file.Write((Convert.ToInt32(station.SolarRad)).ToString() + ' '); // 46
                file.Write(station.AvgBearing.ToString() + ' '); // 47
                file.Write(ReplaceCommas(station.RainLastHour.ToString(RainFormat)) + ' '); // 48
                file.Write(station.Forecastnumber.ToString() + ' '); // 49
                file.Write(IsDaylight() ? "1 " : "0 ");
                file.Write(station.SensorContactLost ? "1 " : "0 ");
                file.Write(station.CompassPoint(station.AvgBearing) + ' '); // 52
                file.Write((Convert.ToInt32(station.CloudBase)).ToString() + ' '); // 53
                file.Write(CloudBaseInFeet ? "ft " : "m ");
                file.Write(ReplaceCommas(station.ApparentTemperature.ToString(TempFormat)) + ' '); // 55
                file.Write(ReplaceCommas(station.SunshineHours.ToString("F1")) + ' '); // 56
                file.Write((Convert.ToInt32(station.CurrentSolarMax)).ToString() + ' '); // 57
                file.WriteLine(station.IsSunny ? "1 " : "0 ");

                file.Close();
            }

            if (RealtimeMySqlEnabled)
            {
                var InvC = new CultureInfo("");

                string values = " Values('" + timestamp.ToString("yy-MM-dd HH:mm:ss") + "'," + station.OutdoorTemperature.ToString(TempFormat, InvC) + ',' +
                                station.OutdoorHumidity.ToString() + ',' + station.OutdoorDewpoint.ToString(TempFormat, InvC) + ',' + station.WindAverage.ToString(WindFormat, InvC) +
                                ',' + station.WindLatest.ToString(WindFormat, InvC) + ',' + station.Bearing.ToString() + ',' + station.RainRate.ToString(RainFormat, InvC) + ',' +
                                station.RainToday.ToString(RainFormat, InvC) + ',' + station.Pressure.ToString(PressFormat, InvC) + ",'" + station.CompassPoint(station.Bearing) +
                                "','" + Beaufort(station.WindAverage) + "','" + WindUnitText + "','" + TempUnitText[1].ToString() + "','" + PressUnitText + "','" + RainUnitText +
                                "'," + station.WindRunToday.ToString(WindRunFormat, InvC) + ",'" +
                                (station.presstrendval > 0 ? '+' + station.presstrendval.ToString(PressFormat, InvC) : station.presstrendval.ToString(PressFormat, InvC)) + "'," +
                                station.RainMonth.ToString(RainFormat, InvC) + ',' + station.RainYear.ToString(RainFormat, InvC) + ',' +
                                station.RainYesterday.ToString(RainFormat, InvC) + ',' + station.IndoorTemperature.ToString(TempFormat, InvC) + ',' +
                                station.IndoorHumidity.ToString() + ',' + station.WindChill.ToString(TempFormat, InvC) + ',' + station.temptrendval.ToString(TempTrendFormat, InvC) +
                                ',' + station.HighTempToday.ToString(TempFormat, InvC) + ",'" + station.hightemptodaytime.ToString("HH:mm") + "'," +
                                station.LowTempToday.ToString(TempFormat, InvC) + ",'" + station.lowtemptodaytime.ToString("HH:mm") + "'," +
                                station.highwindtoday.ToString(WindFormat, InvC) + ",'" + station.highwindtodaytime.ToString("HH:mm") + "'," +
                                station.highgusttoday.ToString(WindFormat, InvC) + ",'" + station.highgusttodaytime.ToString("HH:mm") + "'," +
                                station.highpresstoday.ToString(PressFormat, InvC) + ",'" + station.highpresstodaytime.ToString("HH:mm") + "'," +
                                station.lowpresstoday.ToString(PressFormat, InvC) + ",'" + station.lowpresstodaytime.ToString("HH:mm") + "','" + Version + "','" + Build + "'," +
                                station.RecentMaxGust.ToString(WindFormat, InvC) + ',' + station.HeatIndex.ToString(TempFormat, InvC) + ',' +
                                station.Humidex.ToString(TempFormat, InvC) + ',' + station.UV.ToString(UVFormat, InvC) + ',' + station.ET.ToString(ETFormat, InvC) + ',' +
                                ((int)station.SolarRad).ToString() + ',' + station.AvgBearing.ToString() + ',' + station.RainLastHour.ToString(RainFormat, InvC) + ',' +
                                station.Forecastnumber.ToString() + ",'" + (IsDaylight() ? "1" : "0") + "','" + (station.SensorContactLost ? "1" : "0") + "','" +
                                station.CompassPoint(station.AvgBearing) + "'," + ((int)station.CloudBase).ToString() + ",'" + (CloudBaseInFeet ? "ft" : "m") + "'," +
                                station.ApparentTemperature.ToString(TempFormat, InvC) + ',' + station.SunshineHours.ToString("F1", InvC) + ',' +
                                ((int)Math.Round(station.CurrentSolarMax)).ToString() + ",'" + (station.IsSunny ? "1" : "0") + "')";


                string queryString = StartOfRealtimeInsertSQL + values;


                // do the update
                MySqlCommand cmd = new MySqlCommand();
                cmd.CommandText = queryString;
                cmd.Connection = RealtimeSqlConn;
                //LogMessage(queryString);

                try
                {
                    RealtimeSqlConn.Open();
                    int aff = cmd.ExecuteNonQuery();
                    //LogMessage("MySQL: " + aff + " rows were affected.");
                }
                catch (Exception ex)
                {
                    LogMessage("Error encountered during Realtime MySQL operation.");
                    LogMessage(ex.Message);
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
                    //LogMessage(queryString);

                    try
                    {
                        RealtimeSqlConn.Open();
                        int aff = cmd.ExecuteNonQuery();
                        //LogMessage("MySQL: " + aff + " rows were affected.");
                    }
                    catch (Exception ex)
                    {
                        LogMessage("Error encountered during Realtime delete MySQL operation.");
                        LogMessage(ex.Message);
                    }
                    finally
                    {
                        RealtimeSqlConn.Close();
                    }
                }
            }
        }

        public void DoHTMLFiles()
        {
            if (!RealtimeEnabled)
            {
                CreateRealtimeFile();
            }

            //LogDebugMessage("Creating standard HTML files");
            ProcessTemplateFile(IndexTFile, Indexfile, tokenParser);
            ProcessTemplateFile(TodayTFile, Todayfile, tokenParser);
            ProcessTemplateFile(YesterdayTFile, Yesterfile, tokenParser);
            ProcessTemplateFile(RecordTFile, Recordfile, tokenParser);
            ProcessTemplateFile(MonthlyRecordTFile, MonthlyRecordfile, tokenParser);
            ProcessTemplateFile(TrendsTFile, Trendsfile, tokenParser);
            ProcessTemplateFile(ThisMonthTFile, ThisMonthfile, tokenParser);
            ProcessTemplateFile(ThisYearTFile, ThisYearfile, tokenParser);
            ProcessTemplateFile(GaugesTFile, Gaugesfile, tokenParser);
            //LogDebugMessage("Done creating standard HTML files");
            if (IncludeGraphDataFiles)
            {
                //LogDebugMessage("Creating graph data files");
                station.CreateGraphDataFiles();
                //LogDebugMessage("Done creating graph data files");
            }
            //LogDebugMessage("Creating extra files");
            // handle any extra files
            for (int i = 0; i < numextrafiles; i++)
            {
                if (!ExtraFiles[i].realtime)
                {
                    var uploadfile = ExtraFiles[i].local;
                    if (uploadfile == "<currentlogfile>")
                    {
                        uploadfile = GetLogFileName(DateTime.Now);
                    }
                    var remotefile = ExtraFiles[i].remote;
                    remotefile = remotefile.Replace("<currentlogfile>", Path.GetFileName(GetLogFileName(DateTime.Now)));

                    if ((uploadfile != "") && (File.Exists(uploadfile)) && (remotefile != ""))
                    {
                        if (ExtraFiles[i].process)
                        {
                            //LogDebugMessage("Processing extra file "+uploadfile);
                            // process the file
                            var utf8WithoutBom = new System.Text.UTF8Encoding(false);
                            var encoding = UTF8encode ? utf8WithoutBom : System.Text.Encoding.GetEncoding("iso-8859-1");
                            tokenParser.encoding = encoding;
                            tokenParser.SourceFile = uploadfile;
                            var output = tokenParser.ToString();
                            uploadfile += "tmp";
                            try
                            {
                                using (StreamWriter file = new StreamWriter(uploadfile, false, encoding))
                                {
                                    file.Write(output);

                                    file.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                LogDebugMessage("Error writing file " + uploadfile);
                                LogDebugMessage(ex.Message);
                            }
                            //LogDebugMessage("Finished processing extra file " + uploadfile);
                        }

                        if (!ExtraFiles[i].FTP)
                        {
                            // just copy the file
                            //LogDebugMessage("Copying extra file " + uploadfile);
                            try
                            {
                                File.Copy(uploadfile, remotefile, true);
                            }
                            catch (Exception ex)
                            {
                                LogDebugMessage("Error copying extra file: " + ex.Message);
                            }
                            //LogDebugMessage("Finished copying extra file " + uploadfile);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(ExternalProgram))
            {
                LogDebugMessage("Executing program " + ExternalProgram + " " + ExternalParams);
                try
                {
                    ExecuteProgram(ExternalProgram, ExternalParams);
                    LogDebugMessage("External program started");
                }
                catch (Exception ex)
                {
                    LogMessage("Error starting external program: " + ex.Message);
                }
            }

            //LogDebugMessage("Done creating extra files");

            if (!String.IsNullOrEmpty(ftp_host))
            {
                DoFTPLogin();
            }

            WebUpdating = false;
        }

    */
    }
}
