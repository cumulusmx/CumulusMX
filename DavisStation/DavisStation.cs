using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using UnitsNet;

namespace DavisStation
{
    public class DavisStation : IWeatherStation
    {
        private ILogger _log;
        private WeatherDataModel _currentData = new WeatherDataModel();
        private object _dataUpdateLock = new object();

        public string Identifier => "DavisStation";
        public string Manufacturer => "Davis";
        public string Model => "Davis Models"; //TODO: Report actual model
        public string Description => "Supports weather stations manufactured by Davis.";
        private StationSettings _configurationSettings;
        private DavisStationInterface _interface;
        private bool _useLoop2;
        private double _firmwareVersion;
        public IStationSettings ConfigurationSettings => _configurationSettings;

        public void Initialise(ILogger logger, IStationSettings settings)
        {
            _configurationSettings = settings as StationSettings;
            if (_configurationSettings == null)
            {
                throw new ArgumentException($"Invalid configuration settings passed to Davis Station.");
            }

            _log = logger;

            _log.Info("Station type = Davis");

            var useSerial = _configurationSettings.UseSerialInterface;
            if (useSerial)
            {
                string comPortName = _configurationSettings.ComPort;
                _interface = new DavisStationInterfaceSerial(logger, comPortName);
            }
            else
            {
                int disconnectInterval = _configurationSettings.DisconnectInterval;
                int responseTime = _configurationSettings.ResponseTime;
                int initWait = _configurationSettings.InitWaitTime;
                IPAddress address = IPAddress.Parse("127.0.0.1");
                int port = 80;

                _interface = new DavisStationInterfaceIp(logger,address,port,disconnectInterval,responseTime,initWait);
            }

            _interface.Connect();

            if (_interface.Connected)
            {
                _log.Info("Connected OK");
            }
            else
            {
                _log.Error("Unable to connect to station");
                return;
            }

            var firmwareString = _interface.GetFirmwareVersion();

            if (!double.TryParse(firmwareString, out _firmwareVersion))
            {
                _firmwareVersion = 0.0;
                _log.Warn($"Davis firmware version unknown.");
            }
            else
                _log.Info($"Firmware version: {_firmwareVersion:F1}");

            _useLoop2 = _configurationSettings.UseLoop2;

            if ( _firmwareVersion < 1.9 && _useLoop2)
            {
                _useLoop2 = false;
                _log.Warn(
                    "LOOP2 disabled. It is enabled in configuration but this firmware version does not support it.");
            }
            else
                _log.Info("LOOP2 format :" + (_useLoop2 ? "enabled" : "disabled"));

            if (_configurationSettings.ReadReceptionStats)
            {
                _interface.GetReceptionStats();
                _log.Info(_interface.ReceptionStatistics);
            }

            //TODO cumulus.LogMessage("Last update time = " + cumulus.LastUpdateTime);

            if (_configurationSettings.SyncTime) _interface.SetTime();

            var consoleClock = _interface.GetTime();

            if (consoleClock > DateTime.MinValue)
                _log.Info($"Console clock: {consoleClock:O}");

            var tooOld = new DateTime(0);

            //TODO: Convert this logic - but does it go here???
            /*if (cumulus.LastUpdateTime <= tooOld || !cumulus.UseDataLogger)
            {
                // there's nothing in the database, so we haven't got a rain counter
                // we can't load the history data, so we'll just have to go live

                //if (cumulus.UseDavisLoop2 && cumulus.PeakGustMinutes == 10)
                //{
                //    CalcRecentMaxGust = false;
                //}
                timerStartNeeded = true;
                StartLoop();
                DoDayResetIfNeeded();
                DoTrendValues(DateTime.Now);
            }
            else
            {
                // Read the data from the logger
                startReadingHistoryData();
            }*/

        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public WeatherDataModel GetCurrentData()
        {
            lock(_dataUpdateLock)
            {
                return _currentData;
            }
        }

        public IEnumerable<WeatherDataModel> GetHistoryData(DateTime fromTimestamp)
        {
            throw new NotImplementedException();
        }



        /*public override void startReadingHistoryData()
        {
            cumulus.CurrentActivity = "Reading archive data";
            //lastArchiveTimeUTC = getLastArchiveTime();
            cumulus.LogMessage("Reading history data from log files");

            LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

            cumulus.LogMessage("Reading archive data from logger");
            bw = new BackgroundWorker();
            //histprog = new historyProgressWindow();
            //histprog.Owner = mainWindow;
            //histprog.Show();
            bw.DoWork += bw_DoWork;
            //bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerCompleted += bw_RunWorkerCompleted;
            bw.WorkerReportsProgress = true;
            bw.RunWorkerAsync();
        }

        public override void Stop()
        {
            cumulus.LogMessage("Closing connection");
            try
            {
                if (isSerial)
                {
                    comport.WriteLine("");
                    comport.Close();
                }
                else
                {
                    socket.GetStream().WriteByte(10);
                    socket.Close();
                }
            }
            catch
            {
            }
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //histprog.histprogTB.Text = "Processed 100%";
            //histprog.histprogPB.Value = 100;
            //histprog.Close();
            //mainWindow.FillLastHourGraphData();
            cumulus.LogMessage("Logger archive reading thread completed");
            if (e.Error != null)
                cumulus.LogMessage("Archive reading thread apparently terminated with an error: " + e.Error.Message);
            cumulus.LogMessage("Updating highs and lows");
            //using (cumulusEntities dataContext = new cumulusEntities())
            //{
            //    UpdateHighsAndLows(dataContext);
            //}
            cumulus.CurrentActivity = "Normal running";
            //if (cumulus.UseDavisLoop2 && cumulus.PeakGustMinutes == 10)
            //{
            //    CalcRecentMaxGust = false;
            //}
            // restore this setting
            cumulus.UseSpeedForAvgCalc = savedUseSpeedForAvgCalc;
            StartLoop();
            DoDayResetIfNeeded();
            DoTrendValues(DateTime.Now);
            cumulus.StartTimers();
        }


        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // set this temporarily, so speed is done from average and not peak gust from logger
                savedUseSpeedForAvgCalc = cumulus.UseSpeedForAvgCalc;
                cumulus.UseSpeedForAvgCalc = true;
                GetArchiveData();
                // and again, in case it took a long time and there are new entries
                GetArchiveData();
            }
            catch (Exception ex)
            {
                cumulus.LogMessage("Exception occurred reading archive data: " + ex.Message);
            }
        }


        private int calculateCRC(byte[] data)
        {
            ushort crc = 0;
            ushort[] crc_table =
            {
                0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7, // 0x00
                0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef, // 0x08  
                0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6, // 0x10
                0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de, // 0x18
                0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485, // 0x20
                0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d, // 0x28
                0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4, // 0x30
                0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc, // 0x38
                0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823, // 0x40
                0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b, // 0x48
                0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12, // 0x50
                0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a, // 0x58
                0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41, // 0x60
                0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49, // 0x68
                0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70, // 0x70
                0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78, // 0x78
                0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f, // 0x80
                0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067, // 0x88
                0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e, // 0x90
                0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256, // 0x98
                0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d, // 0xA0
                0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405, // 0xA8
                0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c, // 0xB0
                0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634, // 0xB8
                0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab, // 0xC0
                0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3, // 0xC8
                0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a, // 0xD0
                0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92, // 0xD8
                0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9, // 0xE0
                0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1, // 0xE8
                0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8, // 0xF0
                0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0 // 0xF8
            };

            foreach (var databyte in data) crc = (ushort)(crc_table[(crc >> 8) ^ databyte] ^ (crc << 8));

            return crc;
        }

        private bool crcOK(byte[] data)
        {
            return calculateCRC(data) == 0;
        }



        public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var port = sender as SerialPort;

            // Obtain the number of bytes waiting in the port's buffer
            var bytes = port.BytesToRead;
        }

        public override void Start()
        {
            cumulus.LogMessage("Start normal reading loop");
            var loopcount = cumulus.ForceVPBarUpdate ? 20 : 50;
            const int loop2count = 1;

            try
            {
                while (true)
                {
                    if (_clockSetNeeded)
                    {
                        // set the console clock
                        setTime();
                        _clockSetNeeded = false;
                    }

                    if (isSerial)
                    {
                        if (cumulus.UseDavisLoop2 && SendLoopCommand(comport, "LPS 2 " + loop2count))
                            GetAndProcessLoop2Data(loop2count);

                        if (SendLoopCommand(comport, "LOOP " + loopcount)) GetAndProcessLoopData(loopcount);
                    }
                    else
                    {
                        if (cumulus.UseDavisLoop2 && SendLoopCommand(socket, "LPS 2 " + loop2count + NEW_LINE))
                            GetAndProcessLoop2Data(loop2count);

                        if (SendLoopCommand(socket, "LOOP " + loopcount + NEW_LINE)) GetAndProcessLoopData(loopcount);
                    }

                    if (cumulus.ForceVPBarUpdate) SendBarRead();

                    if (cumulus.DavisReadReceptionStats && _lastReceptionStatsTime.AddMinutes(15) < DateTime.Now)
                    {
                        var recepStats = GetReceptionStats();
                        cumulus.LogDebugMessage(recepStats);
                        DecodeReceptionStats(recepStats);
                    }
                }
            }
            // Catch the ThreadAbortException
            catch (ThreadAbortException)
            {
            }
            finally
            {
                if (isSerial)
                {
                    comport.WriteLine("");
                    comport.Close();
                }
                else
                {
                    socket.GetStream().WriteByte(10);
                    socket.Close();
                }
            }
        }

        private void SendBarRead()
        {
            cumulus.LogDebugMessage("Sending BARREAD");

            var response = "";

            if (isSerial)
            {
                var commandString = "BARREAD";
                if (WakeVP(comport))
                {
                    comport.WriteLine(commandString);

                    Thread.Sleep(200);

                    // Read the response
                    var bytesRead = 0;
                    var buffer = new byte[64];
                    try
                    {
                        while (comport.BytesToRead > 0)
                        {
                            // Read the current character
                            var ch = comport.ReadChar();
                            response += Convert.ToChar(ch);
                            buffer[bytesRead] = (byte)ch;
                            bytesRead++;
                            //cumulus.LogMessage("Received " + ch.ToString("X2"));
                        }
                    }
                    catch (Exception ex)
                    {
                        cumulus.LogMessage(ex.Message);
                    }

                    cumulus.LogDebugMessage(BitConverter.ToString(buffer));
                }
            }
            else
            {
                var commandString = "BARREAD\n";
                if (WakeVP(socket))
                    try
                    {
                        var stream = socket.GetStream();
                        stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                        Thread.Sleep(cumulus.DavisIPResponseTime);

                        var bytesRead = 0;
                        var buffer = new byte[64];

                        while (stream.DataAvailable)
                        {
                            // Read the current character
                            var ch = stream.ReadByte();
                            response += Convert.ToChar(ch);
                            buffer[bytesRead] = (byte)ch;
                            bytesRead++;
                            //cumulus.LogMessage("Received " + ch.ToString("X2"));
                        }

                        cumulus.LogDataMessage("Recieved 0x" + BitConverter.ToString(buffer));
                    }
                    catch (Exception ex)
                    {
                        cumulus.LogDebugMessage("SendBarRead: Error - " + ex.Message);
                    }
            }
        }

        private bool SendLoopCommand(SerialPort serialPort, string commandString)
        {
            if (serialPort.IsOpen)
            {
                WakeVP(serialPort);
                //cumulus.LogMessage("Sending command: " + commandString);
                var Found_ACK = false;

                var passCount = 1;
                const int maxPasses = 4;

                // Clear the input buffer 
                serialPort.DiscardInBuffer();
                // Clear the output buffer
                serialPort.DiscardOutBuffer();

                // Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
                // a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
                // we get nothing back, we assume that the connection is broken
                while (!Found_ACK && passCount < maxPasses)
                {
                    // send the LOOP n command
                    cumulus.LogDebugMessage("Sending command " + commandString + " - pass " + passCount);
                    serialPort.WriteLine(commandString);

                    Thread.Sleep(500);

                    // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
                    // in the buffer first or no response is given.  If all else fails, try again.
                    cumulus.LogDebugMessage("Wait for ACK");
                    while (serialPort.BytesToRead > 0 && !Found_ACK)
                        // Read the current character
                        if (serialPort.ReadChar() == ACK)
                        {
                            Found_ACK = true;
                            cumulus.LogDebugMessage("ACK received");
                        }

                    passCount++;
                }

                // return result to indicate success or otherwise
                if (!Found_ACK) cumulus.LogMessage("!!! No ack received in response to " + commandString);
                return passCount < maxPasses;
            }

            cumulus.LogDebugMessage("!!! Serial port closed");
            return false;
        }

        private bool SendLoopCommand(TcpClient tcpPort, string commandString)
        {
            var Found_ACK = false;
            const int ACK = 6; // ASCII 6
            var passCount = 1;
            const int maxPasses = 4;

            try
            {
                var stream = tcpPort.GetStream();

                // Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
                // a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
                // we get nothing back, we assume that the connection is broken
                while (!Found_ACK && passCount < maxPasses)
                {
                    // send the LOOP n command
                    cumulus.LogDebugMessage("Sending command: " + commandString.Replace("\n", "") + ", attempt " +
                                            passCount);
                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);
                    Thread.Sleep(cumulus.DavisIPResponseTime);
                    cumulus.LogDebugMessage("Wait for ACK");
                    // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
                    // in the buffer first or no response is given.  If all else fails, try again.
                    while (stream.DataAvailable && !Found_ACK)
                    {
                        // Read the current character
                        var data = stream.ReadByte();
                        cumulus.LogDataMessage("Received 0x" + data.ToString("X2"));
                        if (data == ACK)
                        {
                            cumulus.LogDebugMessage("Received ACK");
                            Found_ACK = true;
                        }
                    }

                    passCount++;
                }
            }
            catch (Exception ex)
            {
                cumulus.LogDataMessage("Error sending LOOP command: " + ex.Message);
            }

            // return result to indicate success or otherwise
            return Found_ACK;
        }

        private void GetAndProcessLoopData(int number)
        {
            //cumulus.LogMessage("processing loop data");
            const int loopDataLength = 99;


            for (var i = 0; i < number; i++)
            {
                // Allocate a byte array to hold the loop data 
                var loopString = new byte[loopDataLength];
                VPLoopData loopData;

                var min = DateTime.Now.Minute;

                if (min != previousMinuteSetClock)
                {
                    previousMinuteSetClock = min;
                    if (cumulus.SyncTime && DateTime.Now.Hour == cumulus.ClockSettingHour && min == 0)
                        _clockSetNeeded = true;
                }

                if (isSerial)
                {
                    // Wait until the buffer is full - we've received all the characters from the LOOP response, 
                    // including the final '\n' 

                    try
                    {
                        var loopcount = 1;
                        while (loopcount < 20 && comport.BytesToRead < loopDataLength)
                        {
                            // Wait a short period to allow more data into the buffer
                            Thread.Sleep(250);
                            loopcount++;
                        }

                        if (comport.BytesToRead < loopDataLength)
                        {
                            // all data not received
                            cumulus.LogMessage("!!! loop data not received, bytes received = " + comport.BytesToRead);

                            return;
                        }

                        // Read the data from the buffer into the array
                        comport.Read(loopString, 0, loopDataLength);
                    }
                    catch (Exception ex)
                    {
                        cumulus.LogMessage(ex.ToString());
                        return;
                    }
                }
                else
                {
                    // See if we need to disconnect to allow Weatherlink IP to upload
                    if (cumulus.VP2PeriodicDisconnectInterval > 0)
                    {
                        min = DateTime.Now.Minute;

                        if (min != previousMinuteDisconnect)
                        {
                            try
                            {
                                previousMinuteDisconnect = min;

                                cumulus.LogDebugMessage("Periodic disconnect from logger");
                                // time to disconnect - first stop the loop data by sending a newline
                                socket.GetStream().WriteByte(10);
                                //socket.Client.Shutdown(SocketShutdown.Both);
                                //socket.Client.Disconnect(false);
                            }
                            catch (Exception ex)
                            {
                                cumulus.LogMessage("Periodic disconnect: " + ex.Message);
                            }
                            finally
                            {
                                socket.Client.Close(0);
                            }

                            // Wait
                            Thread.Sleep(cumulus.VP2PeriodicDisconnectInterval * 1000);

                            cumulus.LogDebugMessage("Attempting reconnect to logger");
                            // open a new connection
                            socket = OpenTcpPort();
                            if (socket == null) cumulus.LogMessage("Unable to reconnect to logger");
                            return;
                        }
                    }

                    // Wait until the buffer is full - we've received returnLength characters from the command response
                    var loopcount = 1;
                    while (loopcount < 100 && socket.Available < loopDataLength)
                    {
                        // Wait a short period to let more data load into the buffer
                        Thread.Sleep(200);
                        loopcount++;
                    }

                    if (loopcount == 100)
                    {
                        // all data not received
                        cumulus.LogMessage("!!! loop data not received");
                        return;
                    }

                    // Read the first 99 bytes of the buffer into the array
                    socket.GetStream().Read(loopString, 0, loopDataLength);
                }

                if (cumulus.DataLogging)
                    cumulus.LogMessage("Data " + (i + 1) + ": " + BitConverter.ToString(loopString));

                if (!(loopString[0] == 'L' && loopString[1] == 'O' && loopString[2] == 'O'))
                {
                    cumulus.LogDebugMessage("invalid LOOP packet");
                    // Stop the sending of LOOP packets so we can resynch
                    if (isSerial)
                    {
                        comport.WriteLine("");
                        Thread.Sleep(3000);
                        // read off all data in the pipeline

                        cumulus.LogDebugMessage("Discarding bytes from pipeline: " + comport.BytesToRead);
                        while (comport.BytesToRead > 0) comport.ReadByte();
                    }
                    else
                    {
                        socket.GetStream().WriteByte(10);
                        Thread.Sleep(3000);
                        // read off all data in the pipeline
                        var avail = socket.Available;
                        cumulus.LogDebugMessage("Discarding bytes from pipeline: " + avail);
                        for (var b = 0; b < avail; b++) socket.GetStream().ReadByte();
                    }

                    return;
                }

                if (!crcOK(loopString))
                {
                    cumulus.LogDebugMessage("LOOP packet CRC invalid");
                    continue;
                }

                // Allocate a structure for the data
                loopData = new VPLoopData();

                // ...and load the data into it
                loopData.Load(loopString);

                // Process it
                //cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " Processing Data, i=" + i);
                //Trace.Flush();

                var now = DateTime.Now;

                if (loopData.InsideHumidity >= 0 && loopData.InsideHumidity <= 100)
                    DoIndoorHumidity(loopData.InsideHumidity);

                if (loopData.OutsideHumidity >= 0 && loopData.OutsideHumidity <= 100)
                    DoOutdoorHumidity(loopData.OutsideHumidity, now);

                if (loopData.InsideTemperature > -200 && loopData.InsideTemperature < 300)
                    DoIndoorTemp(ConvertTempFToUser(loopData.InsideTemperature));

                if (loopData.OutsideTemperature > -200 && loopData.OutsideTemperature < 300)
                    DoOutdoorTemp(ConvertTempFToUser(loopData.OutsideTemperature), now);

                if (loopData.Pressure > 0 && loopData.Pressure < 40)
                    DoPressure(ConvertPressINHGToUser(loopData.Pressure), now);

                DoPressTrend("Pressure trend");

                var wind = ConvertWindMPHToUser(loopData.CurrentWindSpeed);
                var avgwind = ConvertWindMPHToUser(loopData.AvgWindSpeed);

                // Check for sensible figures (spec says max for large cups is 175mph)
                if (loopData.CurrentWindSpeed < 200 && loopData.AvgWindSpeed < 200)
                {
                    var winddir = loopData.WindDirection;

                    if (winddir > 360)
                    {
                        cumulus.LogMessage("Wind direction = " + winddir + ", using zero instead");

                        winddir = 0;
                    }

                    DoWind(wind, winddir, avgwind, now);

                    if (!CalcRecentMaxGust)
                    {
                        // See if the current speed is higher than the current 10-min max
                        // We can then update the figure before the next LOOP2 packet is read

                        CheckHighGust(WindLatest, winddir, now);

                        if (WindLatest > RecentMaxGust)
                        {
                            RecentMaxGust = WindLatest;
                            cumulus.LogDebugMessage("Setting max gust from loop value: " +
                                                    RecentMaxGust.ToString(cumulus.WindFormat));
                        }
                    }
                }
                else
                {
                    cumulus.LogDebugMessage("Ignoring wind data. Speed=" + loopData.CurrentWindSpeed + " mph, Avg=" +
                                            loopData.AvgWindSpeed + " mph.");
                }

                var rain = ConvertRainClicksToUser(loopData.YearRain);
                var rainrate = ConvertRainClicksToUser(loopData.RainRate);

                if (rainrate < 0) rainrate = 0;

                DoRain(rain, rainrate, now);

                StormRain = ConvertRainClicksToUser(loopData.StormRain);
                StartOfStorm = loopData.StormRainStart;

                if (loopData.UVIndex >= 0 && loopData.UVIndex < 25) DoUV(loopData.UVIndex, now);

                if (loopData.SolarRad >= 0 && loopData.SolarRad < 5000) DoSolarRad(loopData.SolarRad, now);

                if (loopData.AnnualET >= 0 && loopData.AnnualET < 32000)
                    DoET(ConvertRainINToUser(loopData.AnnualET), now);

                if (ConvertUserWindToMS(WindAverage) < 1.5)
                    DoWindChill(OutdoorTemperature, now);
                else
                    DoWindChill(
                        ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature),
                            ConvertUserWindToKPH(WindAverage))), now);

                DoApparentTemp(now);


                var forecastRule = loopData.ForecastRule < cumulus.DavisForecastLookup.Length
                    ? loopData.ForecastRule
                    : cumulus.DavisForecastLookup.Length - 1;

                var key1 = cumulus.DavisForecastLookup[forecastRule, 0];
                var key2 = cumulus.DavisForecastLookup[forecastRule, 1];
                var key3 = cumulus.DavisForecastLookup[forecastRule, 2];

                // Adjust for S hemisphere
                if (cumulus.Latitude < 0)
                {
                    if (key3 == 3)
                        key3 = 1;
                    else if (key3 == 4) key3 = 2;
                }

                var forecast = cumulus.DavisForecast1[key1] + cumulus.DavisForecast2[key2] +
                               cumulus.DavisForecast3[key3];

                DoForecast(forecast, false);

                ConBatText = loopData.ConBatVoltage.ToString("F2");
                //cumulus.LogDebugMessage("Con batt="+ConBatText);

                TxBatText = ProcessTxBatt(loopData.TXbattStatus);
                //cumulus.LogDebugMessage("TX batt=" + TxBatText);

                if (cumulus.LogExtraSensors)
                {
                    if (loopData.ExtraTemp1 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp1 - 90), 1);

                    if (loopData.ExtraTemp2 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp2 - 90), 2);

                    if (loopData.ExtraTemp3 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp3 - 90), 3);

                    if (loopData.ExtraTemp4 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp4 - 90), 4);

                    if (loopData.ExtraTemp5 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp5 - 90), 5);

                    if (loopData.ExtraTemp6 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp6 - 90), 6);

                    if (loopData.ExtraTemp7 < 255) DoExtraTemp(ConvertTempFToUser(loopData.ExtraTemp7 - 90), 7);

                    if (loopData.ExtraHum1 >= 0 && loopData.ExtraHum1 <= 100)
                    {
                        DoExtraHum(loopData.ExtraHum1, 1);
                        if (loopData.ExtraTemp1 < 255)
                            ExtraDewPoint[1] =
                                ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(ExtraTemp[1]), ExtraHum[1]));
                    }

                    if (loopData.ExtraHum2 >= 0 && loopData.ExtraHum2 <= 100)
                    {
                        DoExtraHum(loopData.ExtraHum2, 2);
                        if (loopData.ExtraTemp2 < 255)
                            ExtraDewPoint[2] =
                                ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(ExtraTemp[2]), ExtraHum[2]));
                    }

                    if (loopData.ExtraHum3 >= 0 && loopData.ExtraHum3 <= 100)
                    {
                        DoExtraHum(loopData.ExtraHum3, 3);
                        if (loopData.ExtraTemp3 < 255)
                            ExtraDewPoint[3] =
                                ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(ExtraTemp[3]), ExtraHum[3]));
                    }

                    if (loopData.ExtraHum4 >= 0 && loopData.ExtraHum4 <= 100)
                    {
                        DoExtraHum(loopData.ExtraHum4, 4);
                        if (loopData.ExtraTemp4 < 255)
                            ExtraDewPoint[4] =
                                ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(ExtraTemp[4]), ExtraHum[4]));
                    }

                    if (loopData.ExtraHum5 >= 0 && loopData.ExtraHum5 <= 100) DoExtraHum(loopData.ExtraHum5, 5);

                    if (loopData.ExtraHum6 >= 0 && loopData.ExtraHum6 <= 100) DoExtraHum(loopData.ExtraHum6, 6);

                    if (loopData.ExtraHum7 >= 0 && loopData.ExtraHum7 <= 100) DoExtraHum(loopData.ExtraHum7, 7);

                    if (loopData.SoilMoisture1 >= 0 && loopData.SoilMoisture1 <= 250)
                        DoSoilMoisture(loopData.SoilMoisture1, 1);

                    if (loopData.SoilMoisture2 >= 0 && loopData.SoilMoisture2 <= 250)
                        DoSoilMoisture(loopData.SoilMoisture2, 2);

                    if (loopData.SoilMoisture3 >= 0 && loopData.SoilMoisture3 <= 250)
                        DoSoilMoisture(loopData.SoilMoisture3, 3);

                    if (loopData.SoilMoisture4 >= 0 && loopData.SoilMoisture4 <= 250)
                        DoSoilMoisture(loopData.SoilMoisture4, 4);

                    if (loopData.SoilTemp1 < 255) DoSoilTemp(ConvertTempFToUser(loopData.SoilTemp1 - 90), 1);

                    if (loopData.SoilTemp2 < 255) DoSoilTemp(ConvertTempFToUser(loopData.SoilTemp2 - 90), 2);

                    if (loopData.SoilTemp3 < 255) DoSoilTemp(ConvertTempFToUser(loopData.SoilTemp3 - 90), 3);

                    if (loopData.SoilTemp4 < 255) DoSoilTemp(ConvertTempFToUser(loopData.SoilTemp4 - 90), 4);

                    if (loopData.LeafWetness1 >= 0 && loopData.LeafWetness1 < 16)
                        DoLeafWetness(loopData.LeafWetness1, 1);

                    if (loopData.LeafWetness2 >= 0 && loopData.LeafWetness2 < 16)
                        DoLeafWetness(loopData.LeafWetness2, 2);

                    if (loopData.LeafWetness3 >= 0 && loopData.LeafWetness3 < 16)
                        DoLeafWetness(loopData.LeafWetness3, 3);

                    if (loopData.LeafWetness4 >= 0 && loopData.LeafWetness4 < 16)
                        DoLeafWetness(loopData.LeafWetness4, 4);

                    if (loopData.LeafTemp1 < 255) DoLeafTemp(ConvertTempFToUser(loopData.LeafTemp1 - 90), 1);

                    if (loopData.LeafTemp2 < 255) DoLeafTemp(ConvertTempFToUser(loopData.LeafTemp2 - 90), 2);

                    if (loopData.LeafTemp3 < 255) DoLeafTemp(ConvertTempFToUser(loopData.LeafTemp3 - 90), 3);

                    if (loopData.LeafTemp4 < 255) DoLeafTemp(ConvertTempFToUser(loopData.LeafTemp4 - 90), 4);
                }

                UpdateStatusPanel(DateTime.Now);
            }

            //cumulus.LogMessage("end processing loop data");
        }

        private string ProcessTxBatt(byte TxStatus)
        {
            var response = "";

            for (var i = 0; i < 8; i++)
            {
                var status = (TxStatus & (1 << i)) == 0 ? "-ok " : "-LOW ";
                response = response + (i + 1) + status;
            }

            return response.Trim();
        }

        private void GetAndProcessLoop2Data(int number)
        {
            //cumulus.LogMessage("processing loop2 data");

            const int loopDataLength = 99;


            for (var i = 0; i < number; i++)
            {
                // Allocate a byte array to hold the loop data 
                var loopString = new byte[loopDataLength];
                VPLoop2Data loopData;

                if (isSerial)
                    try
                    {
                        var loopcount = 1;
                        while (loopcount < 100 && comport.BytesToRead < loopDataLength)
                        {
                            // Wait a short period to allow more data into the buffer
                            Thread.Sleep(200);
                            loopcount++;
                        }

                        if (loopcount == 100)
                        {
                            // all data not received
                            cumulus.LogMessage("!!! loop2 data not received");
                            return;
                        }

                        // Read the data from the buffer into the array
                        comport.Read(loopString, 0, loopDataLength);
                    }
                    catch (Exception ex)
                    {
                        cumulus.LogMessage("GetAndProcessLoop2Data: Error - " + ex);
                    }
                else
                    try
                    {
                        // Wait until the buffer is full
                        var loopcount = 1;
                        while (loopcount < 100 && socket.Available < loopDataLength)
                        {
                            // Wait a short period to let more data load into the buffer
                            Thread.Sleep(200);
                            loopcount++;
                        }

                        if (loopcount == 100)
                        {
                            // all data not received
                            cumulus.LogMessage("!!! loop2 data not received");
                            return;
                        }

                        // Read the first 99 bytes of the buffer into the array
                        socket.GetStream().Read(loopString, 0, loopDataLength);
                    }
                    catch (Exception ex)
                    {
                        cumulus.LogDebugMessage("Loop2 data: Error - " + ex.Message);
                    }

                if (!(loopString[0] == 'L' && loopString[1] == 'O' && loopString[2] == 'O'))
                {
                    cumulus.LogDebugMessage("invalid LOOP2 packet");
                    continue;
                }

                if (!crcOK(loopString)) continue;

                cumulus.LogDataMessage("Loop2: " + BitConverter.ToString(loopString));

                // Allocate a structure for the data
                loopData = new VPLoop2Data();

                // ...and load the data into it
                loopData.Load(loopString);

                // Process it
                //cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " Processing Data, i=" + i);
                //Trace.Flush();

                var now = DateTime.Now;

                // Extract station pressure, and use it to calculate altimeter pressure
                StationPressure = ConvertPressINHGToUser(loopData.AbsolutePressure);
                AltimeterPressure =
                    ConvertPressMBToUser(StationToAltimeter(PressureHPa(StationPressure), AltitudeM(cumulus.Altitude)));

                var wind = ConvertWindMPHToUser(loopData.CurrentWindSpeed);

                // Use current average as we don't have a new value in LOOP2. Allow for calibration.
                if (loopData.CurrentWindSpeed < 200)
                    DoWind(wind, loopData.WindDirection, WindAverage / cumulus.WindSpeedMult, now);
                else
                    cumulus.LogDebugMessage("Ignoring LOOP2 wind speed: " + loopData.CurrentWindSpeed + " mph");

                if (loopData.WindGust10Min < 200)
                {
                    // Extract 10-min gust and see if it is higher than we have recorded.
                    var gust10min = ConvertWindMPHToUser(loopData.WindGust10Min) * cumulus.WindGustMult;
                    var gustdir = loopData.WindGustDir;

                    cumulus.LogDebugMessage("10-min gust from loop2: " + gust10min.ToString(cumulus.WindFormat));

                    if (gust10min > RecentMaxGust)
                    {
                        cumulus.LogDebugMessage("Using 10-min gust from loop2");
                        CheckHighGust(gust10min, gustdir, now);

                        // add to recent values so normal calculation includes this value
                        WindRecent[nextwind].Gust = ConvertWindMPHToUser(loopData.WindGust10Min);
                        WindRecent[nextwind].Speed = WindAverage / cumulus.WindSpeedMult;
                        WindRecent[nextwind].Timestamp = now;
                        nextwind = (nextwind + 1) % cumulus.MaxWindRecent;

                        RecentMaxGust = gust10min;
                    }
                }

                //cumulus.LogDebugMessage("LOOP2 wind average: "+loopData.WindAverage);

                if (loopData.THSWindex < 32000) THSWIndex = ConvertTempFToUser(loopData.THSWindex);


                //UpdateStatusPanel(DateTime.Now);
            }

            //cumulus.LogMessage("end processing loop2 data");
        }

        private void CheckHighGust(double gust, int gustdir, DateTime timestamp)
        {
            if (gust > RecentMaxGust)
            {
                if (gust > highgusttoday)
                {
                    highgusttoday = gust;
                    highgusttodaytime = timestamp;
                    highgustbearing = gustdir;
                    WriteTodayFile(timestamp, false);
                }

                if (gust > HighGustThisMonth)
                {
                    HighGustThisMonth = gust;
                    HighGustThisMonthTS = timestamp;
                    WriteMonthIniFile();
                }

                if (gust > HighGustThisYear)
                {
                    HighGustThisYear = gust;
                    HighGustThisYearTS = timestamp;
                    WriteYearIniFile();
                }

                // All time high gust?
                if (gust > alltimerecarray[AT_highgust].value) SetAlltime(AT_highgust, gust, timestamp);

                // check for monthly all time records (and set)
                CheckMonthlyAlltime(AT_highgust, gust, true, timestamp);
            }
        }

        private void GetArchiveData()
        {
            cumulus.LogMessage("Get Archive Data");

            Console.WriteLine("Downloading Archive Data");

            var badCRC = false;
            const int ACK = 6;
            const int NAK = 0x21;
            const int ESC = 0x1b;
            int passCount;
            const int maxPasses = 4;
            byte[] ACKstring = { ACK };
            byte[] NAKstring = { NAK };
            byte[] ESCstring = { ESC };
            const int pageSize = 267;
            const int recordSize = 52;

            NetworkStream stream = null;

            if (!isSerial) stream = socket.GetStream();

            lastDataReadTime = cumulus.LastUpdateTime;
            var luhour = lastDataReadTime.Hour;

            var rollHour = Math.Abs(cumulus.GetHourInc());

            cumulus.LogMessage("Rollover hour = " + rollHour);

            var rolloverdone = luhour == rollHour;

            var midnightraindone = luhour == 0;

            // construct date and time of last record read
            var vantageDateStamp = cumulus.LastUpdateTime.Day + cumulus.LastUpdateTime.Month * 32 +
                                   (cumulus.LastUpdateTime.Year - 2000) * 512;
            var vantageTimeStamp = 100 * cumulus.LastUpdateTime.Hour + cumulus.LastUpdateTime.Minute;

            cumulus.LogMessage(string.Format("Last Archive Date: {0}", cumulus.LastUpdateTime));
            cumulus.LogMessage("Date: " + vantageDateStamp);
            cumulus.LogMessage("Time: " + vantageTimeStamp);

            int currChar;

            if (isSerial)
            {
                comport.DiscardInBuffer();

                if (!WakeVP(comport))
                    cumulus.LogMessage("Unable to wake VP");

                // send the command
                comport.DiscardInBuffer();

                cumulus.LogMessage("Sending DMPAFT");
                comport.WriteLine("DMPAFT");

                // wait for the ACK
                currChar = comport.ReadChar();

                if (currChar != ACK)
                {
                    cumulus.LogMessage("No Ack in response to DMPAFT, received 0x" + currChar.ToString("X2"));
                    return;
                }
            }
            else
            {
                WakeVP(socket);
                var dmpaft = "DMPAFT\n";
                stream.Write(Encoding.ASCII.GetBytes(dmpaft), 0, dmpaft.Length);
                Thread.Sleep(cumulus.DavisIPResponseTime);

                var Found_ACK = false;

                while (stream.DataAvailable && !Found_ACK)
                    // Read the current character
                    if (stream.ReadByte() == ACK)
                        Found_ACK = true;

                if (!Found_ACK)
                {
                    cumulus.LogMessage("No Ack in response to DMPAFT");
                    return;
                }
            }

            cumulus.LogMessage("Received response to DMPAFT, sending date and time");
            Trace.Flush();

            // Construct date time string to send next
            byte[] data =
            {
                (byte) (vantageDateStamp % 256), (byte) (vantageDateStamp / 256), (byte) (vantageTimeStamp % 256),
                (byte) (vantageTimeStamp / 256), 0, 0
            };

            // calculate and insert CRC

            var datacopy = new byte[4];

            Array.Copy(data, datacopy, 4);
            var crc = calculateCRC(datacopy);

            data[4] = (byte)(crc / 256);
            data[5] = (byte)(crc % 256);

            cumulus.LogMessage(BitConverter.ToString(data));

            if (isSerial)
            {
                // send the data
                comport.Write(data, 0, 6);

                // wait for the ACK
                cumulus.LogMessage("Wait for ACK...");
                currChar = comport.ReadChar();

                if (currChar != ACK)
                {
                    cumulus.LogMessage("No ACK, received: 0x" + currChar.ToString("X2"));
                    return;
                }

                cumulus.LogMessage("ACK received");

                cumulus.LogMessage("Waiting for response");
                // wait for the response
                while (comport.BytesToRead < 6)
                    // Wait a short period to let more data load into the buffer
                    Thread.Sleep(200);

                // Read the response
                comport.Read(data, 0, 6);

                var resp = "Response: ";

                for (var i = 0; i < 6; i++) resp = resp + " " + data[i].ToString("X2");
                cumulus.LogMessage(resp);
            }
            else
            {
                stream.Write(data, 0, 6);

                Thread.Sleep(cumulus.DavisIPResponseTime);

                var Found_ACK = false;

                while (stream.DataAvailable && !Found_ACK)
                {
                    // Read the current character
                    currChar = stream.ReadByte();
                    cumulus.LogMessage("Received 0x" + currChar.ToString("X2"));
                    if (currChar == ACK)
                        Found_ACK = true;
                }

                if (!Found_ACK)
                {
                    cumulus.LogMessage("No ACK");
                    return;
                }

                cumulus.LogMessage("ACK received");

                // Wait until the buffer is full 
                while (socket.Available < 6)
                    // Wait a short period to let more data load into the buffer
                    Thread.Sleep(200);

                // Read the response
                stream.Read(data, 0, 6);

                cumulus.LogMessage("Response:" + BitConverter.ToString(data));
            }

            // extract number of pages and offset into first page
            var numPages = data[1] * 256 + data[0];
            var offset = data[3] * 256 + data[2];
            var bytesToRead = numPages * pageSize;
            var dataOffset = offset * recordSize + 1;
            var buff = new byte[pageSize];

            cumulus.LogMessage("Reading data: " + numPages + " pages , offset = " + offset);
            Trace.Flush();

            // keep track of how many records processed for percentage display
            // but there may be some old entries in the last page
            var numtodo = numPages * 5 - offset;
            var numdone = 0;

            for (var p = 0; p < numPages; p++)
            {
                cumulus.LogMessage("Reading archive page " + p);
                passCount = 0;

                // send ACK to get next page
                if (isSerial)
                    comport.Write(ACKstring, 0, 1);
                else
                    stream.Write(ACKstring, 0, 1);

                do
                {
                    passCount++;

                    cumulus.LogMessage("Waiting for response");
                    var responsePasses = 0;
                    if (isSerial)
                    {
                        // wait for the response

                        while (comport.BytesToRead < pageSize && responsePasses < 20)
                        {
                            // Wait a short period to let more data load into the buffer
                            Thread.Sleep(200);
                            responsePasses++;
                        }

                        if (responsePasses == 20)
                        {
                            cumulus.LogMessage("The station has stopped sending archive data");
                            return;
                        }

                        // Read the response
                        cumulus.LogMessage("Reading response");
                        comport.Read(buff, 0, pageSize);

                        if (cumulus.DataLogging) cumulus.LogMessage("Data: " + BitConverter.ToString(buff));

                        if (crcOK(buff))
                        {
                            badCRC = false;
                        }
                        else
                        {
                            badCRC = true;
                            // send NAK to get page again
                            comport.Write(NAKstring, 0, 1);
                        }
                    }
                    else
                    {
                        // wait for the response
                        while (socket.Available < pageSize && responsePasses < 20)
                        {
                            // Wait a short period to let more data load into the buffer
                            Thread.Sleep(cumulus.DavisIPResponseTime);
                            responsePasses++;
                        }

                        if (responsePasses == 20)
                        {
                            cumulus.LogMessage("The station has stopped sending archive data");
                            return;
                        }

                        // Read the response
                        stream.Read(buff, 0, pageSize);

                        if (cumulus.DataLogging) cumulus.LogMessage("Data: " + BitConverter.ToString(buff));

                        if (crcOK(buff))
                        {
                            badCRC = false;
                        }
                        else
                        {
                            badCRC = true;
                            // send NAK to get page again
                            stream.Write(NAKstring, 0, 1);
                        }
                    }
                } while (passCount < maxPasses && badCRC);

                // if we still got bad data after maxPasses, give up
                if (badCRC)
                {
                    cumulus.LogMessage("bad CRC");
                    Trace.Flush();
                    if (isSerial)
                        comport.Write(ESCstring, 0, 1);
                    else
                        stream.Write(ESCstring, 0, 1);

                    return;
                }

                // use the offset on the first page only
                var start = p == 0 ? offset : 0;

                for (var r = start; r < 5; r++)
                {
                    var archiveData = new VPArchiveData();

                    var record = new byte[recordSize];

                    DateTime timestamp;

                    // Copy the next record from the buffer...
                    Array.Copy(buff, r * recordSize + 1, record, 0, recordSize);

                    // ...and load it into the archive data...
                    archiveData.Load(record, out timestamp);

                    cumulus.LogMessage("Loaded archive record for Page=" + p + " Record=" + r + " Timestamp=" +
                                       archiveData.Timestamp);

                    if (timestamp > lastDataReadTime)
                    {
                        cumulus.LogMessage("Processing archive record for " + timestamp);

                        var h = timestamp.Hour;

                        if (h != rollHour) rolloverdone = false;

                        if (h != 0) midnightraindone = false;

                        // ..and then process it


                        var interval = (int)(timestamp - lastDataReadTime).TotalMinutes;

                        if (archiveData.InsideTemperature > -200 && archiveData.InsideTemperature < 300)
                            DoIndoorTemp(ConvertTempFToUser(archiveData.InsideTemperature));

                        if (archiveData.InsideHumidity >= 0 && archiveData.InsideHumidity <= 100)
                            DoIndoorHumidity(archiveData.InsideHumidity);

                        if (archiveData.OutsideHumidity >= 0 && archiveData.OutsideHumidity <= 100)
                            DoOutdoorHumidity(archiveData.OutsideHumidity, timestamp);

                        if (archiveData.OutsideTemperature > -200 && archiveData.OutsideTemperature < 300)
                        {
                            DoOutdoorTemp(ConvertTempFToUser(archiveData.OutsideTemperature), timestamp);
                            // add in 'archivePeriod' minutes worth of temperature to the temp samples
                            tempsamplestoday = tempsamplestoday + interval;
                            TempTotalToday = TempTotalToday + OutdoorTemperature * interval;

                            // update chill hours
                            if (OutdoorTemperature < cumulus.ChillHourThreshold) ChillHours += interval / 60.0;

                            // update heating/cooling degree days
                            UpdateDegreeDays(interval);
                        }

                        var wind = ConvertWindMPHToUser(archiveData.HiWindSpeed);
                        var avgwind = ConvertWindMPHToUser(archiveData.AvgWindSpeed);
                        if (archiveData.HiWindSpeed < 250 && archiveData.AvgWindSpeed < 250)
                        {
                            var bearing = archiveData.WindDirection;
                            if (bearing == 255) bearing = 0;

                            DoWind(wind, (int)(bearing * 22.5), avgwind, timestamp);

                            if (ConvertUserWindToMS(WindAverage) < 1.5)
                                DoWindChill(OutdoorTemperature, timestamp);
                            else
                                DoWindChill(
                                    ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature),
                                        ConvertUserWindToKPH(WindAverage))), timestamp);

                            // update dominant wind bearing
                            CalculateDominantWindBearing((int)(bearing * 22.5), WindAverage, interval);
                        }

                        DoApparentTemp(timestamp);

                        // add in 'archivePeriod' minutes worth of wind speed to windrun
                        WindRunToday += WindAverage * WindRunHourMult[cumulus.WindUnit] * interval / 60.0;

                        DateTime windruncheckTS;
                        if (h == rollHour && timestamp.Minute == 0)
                            windruncheckTS = timestamp.AddMinutes(-1);
                        else
                            windruncheckTS = timestamp;

                        CheckForWindrunHighLow(windruncheckTS);

                        var rain = ConvertRainClicksToUser(archiveData.Rainfall) + Raincounter;
                        var rainrate = ConvertRainClicksToUser(archiveData.HiRainRate);

                        if (rainrate < 0) rainrate = 0;

                        DoRain(rain, rainrate, timestamp);

                        if (archiveData.Pressure > 0 && archiveData.Pressure < 40)
                            DoPressure(ConvertPressINHGToUser(archiveData.Pressure), timestamp);

                        if (archiveData.HiUVIndex >= 0 && archiveData.HiUVIndex < 25)
                            DoUV(archiveData.HiUVIndex, timestamp);

                        if (archiveData.SolarRad >= 0 && archiveData.SolarRad < 5000)
                        {
                            DoSolarRad(archiveData.SolarRad, timestamp);

                            // add in archive period worth of sunshine, if sunny
                            if (SolarRad > CurrentSolarMax * cumulus.SunThreshold / 100.00 &&
                                SolarRad >= cumulus.SolarMinimum)
                                SunshineHours = SunshineHours + interval / 60.0;
                        }

                        if (archiveData.ET >= 0 && archiveData.ET < 32000)
                            DoET(ConvertRainINToUser(archiveData.ET) + AnnualETTotal, timestamp);

                        if (cumulus.LogExtraSensors)
                        {
                            if (archiveData.ExtraTemp1 < 255)
                                DoExtraTemp(ConvertTempFToUser(archiveData.ExtraTemp1 - 90), 1);

                            if (archiveData.ExtraTemp2 < 255)
                                DoExtraTemp(ConvertTempFToUser(archiveData.ExtraTemp2 - 90), 2);

                            if (archiveData.ExtraTemp3 < 255)
                                DoExtraTemp(ConvertTempFToUser(archiveData.ExtraTemp3 - 90), 3);

                            if (archiveData.ExtraHum1 >= 0 && archiveData.ExtraHum1 <= 100)
                            {
                                DoExtraHum(archiveData.ExtraHum1, 1);
                                if (archiveData.ExtraTemp1 < 255)
                                    ExtraDewPoint[1] =
                                        ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(ExtraTemp[1]),
                                            ExtraHum[1]));
                            }

                            if (archiveData.ExtraHum2 >= 0 && archiveData.ExtraHum2 <= 100)
                            {
                                DoExtraHum(archiveData.ExtraHum2, 2);
                                if (archiveData.ExtraTemp2 < 255)
                                    ExtraDewPoint[2] =
                                        ConvertTempCToUser(MeteoLib.DewPoint(ConvertUserTempToC(ExtraTemp[2]),
                                            ExtraHum[2]));
                            }

                            if (archiveData.SoilMoisture1 >= 0 && archiveData.SoilMoisture1 <= 250)
                                DoSoilMoisture(archiveData.SoilMoisture1, 1);

                            if (archiveData.SoilMoisture2 >= 0 && archiveData.SoilMoisture2 <= 250)
                                DoSoilMoisture(archiveData.SoilMoisture2, 2);

                            if (archiveData.SoilMoisture3 >= 0 && archiveData.SoilMoisture3 <= 250)
                                DoSoilMoisture(archiveData.SoilMoisture3, 3);

                            if (archiveData.SoilMoisture4 >= 0 && archiveData.SoilMoisture4 <= 250)
                                DoSoilMoisture(archiveData.SoilMoisture4, 4);

                            if (archiveData.SoilTemp1 < 255)
                                DoSoilTemp(ConvertTempFToUser(archiveData.SoilTemp1 - 90), 1);

                            if (archiveData.SoilTemp2 < 255)
                                DoSoilTemp(ConvertTempFToUser(archiveData.SoilTemp2 - 90), 2);

                            if (archiveData.SoilTemp3 < 255)
                                DoSoilTemp(ConvertTempFToUser(archiveData.SoilTemp3 - 90), 3);

                            if (archiveData.SoilTemp4 < 255)
                                DoSoilTemp(ConvertTempFToUser(archiveData.SoilTemp4 - 90), 4);

                            if (archiveData.LeafWetness1 >= 0 && archiveData.LeafWetness1 < 16)
                                DoLeafWetness(LeafWetness1, 1);

                            if (archiveData.LeafWetness2 >= 0 && archiveData.LeafWetness2 < 16)
                                DoLeafWetness(LeafWetness2, 2);

                            if (archiveData.LeafTemp1 < 255)
                                DoLeafTemp(ConvertTempFToUser(archiveData.LeafTemp1 - 90), 1);

                            if (archiveData.LeafTemp2 < 255)
                                DoLeafTemp(ConvertTempFToUser(archiveData.LeafTemp2 - 90), 2);
                        }

                        cumulus.LogMessage("Page=" + p + " Record=" + r + " Timestamp=" + archiveData.Timestamp);
                        Trace.Flush();


                        Humidex = ConvertTempCToUser(MeteoLib.Humidex(ConvertUserTempToC(OutdoorTemperature),
                            OutdoorHumidity));

                        DoWindChill(
                            ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature),
                                ConvertUserWindToKPH(WindAverage))), timestamp);

                        DoApparentTemp(timestamp);

                        lastDataReadTime = timestamp;

                        //UpdateDatabase(now, interval, false);
                        cumulus.DoLogFile(timestamp, false);
                        cumulus.LogMessage("Log file entry written");

                        if (cumulus.LogExtraSensors) cumulus.DoExtraLogFile(timestamp);

                        AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
                        AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
                        AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature,
                            OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
                            IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing,
                            OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV);
                        AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing,
                            OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
                            OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter);
                        RemoveOldLHData(timestamp);
                        RemoveOldL3HData(timestamp);
                        RemoveOldGraphData(timestamp);
                        DoTrendValues(timestamp);
                        UpdateStatusPanel(timestamp);
                        cumulus.AddToWebServiceLists(timestamp);

                        //  if outside rollover hour, rollover yet to be done
                        if (h != rollHour) rolloverdone = false;

                        // In rollover hour and rollover not yet done
                        if (h == rollHour && !rolloverdone)
                        {
                            // do rollover
                            cumulus.LogMessage("Day rollover " + timestamp.ToShortTimeString());
                            DayReset(timestamp);

                            rolloverdone = true;
                        }

                        // Not in midnight hour, midnight rain yet to be done
                        if (h != 0) midnightraindone = false;

                        // In midnight hour and midnight rain (and sun) not yet done
                        if (h == 0 && !midnightraindone)
                        {
                            ResetMidnightRain(timestamp);
                            ResetSunshineHours();
                            midnightraindone = true;
                        }
                    }
                    else
                    {
                        cumulus.LogMessage("Ignoring old archive data");
                    }

                    numdone++;

                    cumulus.LogMessage(numdone + " archive entries processed");

                    //bw.ReportProgress(numdone*100/numtodo, "processing");
                }
            }
        }

        /// <summary>
        ///     Converts VP rain gauge tips/clicks to user units
        ///     Assumes
        /// </summary>
        /// <param name="clicks"></param>
        /// <returns></returns>
        internal double ConvertRainClicksToUser(double clicks)
        {
            // One click is either 0.01 inches or 0.2 mm
            if (cumulus.VPrainGaugeType == -1)
            {
                // Rain gauge type not configured, assume from units
                if (cumulus.RainUnit == 0)
                    return clicks * 0.2;
                return clicks * 0.01;
            }

            if (cumulus.VPrainGaugeType == 0)
                // Rain gauge is metric, convert to user unit
                return ConvertRainMMToUser(clicks * 0.2);
            return ConvertRainINToUser(clicks * 0.01);
        }
        */
    }
}
