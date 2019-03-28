using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using Force.Crc32;
using Microsoft.VisualBasic.CompilerServices;
using UnitsNet;

namespace DavisStation
{
    public class DavisStation : WeatherStationBase
    {
        private ILogger _log;
        private readonly IWeatherDataStatistics _weatherStatistics;
        private WeatherDataModel _currentData = new WeatherDataModel();
        private object _dataUpdateLock = new object();

        public override string Identifier => "DavisStation";
        public override string Manufacturer => "Davis";
        public override string Model => "Davis Models"; //TODO: Report actual model
        public override string Description => "Supports weather stations manufactured by Davis.";
        private StationSettings _configurationSettings;
        private DavisStationInterface _interface;
        private bool _useLoop2;
        private bool _enabled;
        private double _firmwareVersion;
        public override IStationSettings ConfigurationSettings => _configurationSettings;
        public override bool Enabled => _enabled;
        private Dictionary<string, ICalibration> _calibrationDictionary;

        protected Task _backgroundTask;
        protected CancellationTokenSource _cts;

        public DavisStation(ILogger logger, StationSettings settings, IWeatherDataStatistics weatherStatistics)
        {
            _configurationSettings = settings;
            if (_configurationSettings == null)
            {
                throw new ArgumentException($"Invalid configuration settings passed to Davis Station.");
            }

            _enabled = settings.Enabled ?? false;
            _log = logger;
            _weatherStatistics = weatherStatistics;
        }

        public override void Initialise()
        {

            _log.Info("Station type = Davis");

            var useSerial = _configurationSettings.UseSerialInterface;
            if (useSerial)
            {
                string comPortName = _configurationSettings.ComPort;
                _interface = new DavisStationInterfaceSerial(_log, comPortName);
            }
            else
            {
                int disconnectInterval = _configurationSettings.DisconnectInterval;
                int responseTime = _configurationSettings.ResponseTime;
                int initWait = _configurationSettings.InitWaitTime;
                IPAddress address = IPAddress.Parse("127.0.0.1");
                int port = 80;

                _interface = new DavisStationInterfaceIp(_log, address,port,disconnectInterval,responseTime,initWait);
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

            //TODO _log.Info("Last update time = " + cumulus.LastUpdateTime);

            if (_configurationSettings.SyncTime) _interface.SetTime();

            var consoleClock = _interface.GetTime();

            if (consoleClock > DateTime.MinValue)
                _log.Info($"Console clock: {consoleClock:O}");

            _calibrationDictionary = GetCalibrationDictionary(_configurationSettings);

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


        public override WeatherDataModel GetCurrentData()
        {
            lock(_dataUpdateLock)
            {
                return _currentData;
            }
        }

        public override IEnumerable<WeatherDataModel> GetHistoryData(DateTime fromTimestamp)
        {
            throw new NotImplementedException();
        }


        #region LoadLogHistory
        /*public override void startReadingHistoryData()
        {
            cumulus.CurrentActivity = "Reading archive data";
            //lastArchiveTimeUTC = getLastArchiveTime();
            _log.Info("Reading history data from log files");

            LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

            _log.Info("Reading archive data from logger");
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
                _log.Info("Exception occurred reading archive data: " + ex.Message);
            }

                    private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //histprog.histprogTB.Text = "Processed 100%";
            //histprog.histprogPB.Value = 100;
            //histprog.Close();
            //mainWindow.FillLastHourGraphData();
            _log.Info("Logger archive reading thread completed");
            if (e.Error != null)
                _log.Info("Archive reading thread apparently terminated with an error: " + e.Error.Message);
            _log.Info("Updating highs and lows");
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

        }*/

        #endregion

        public override void Start()
        {
            _log.Info("Starting station background task");
            _backgroundTask = Task.Factory.StartNew(() =>
                    PollForNewData(_cts.Token, _weatherStatistics)
                , _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private async Task PollForNewData(CancellationToken ct, IWeatherDataStatistics weatherStatistics)
        {
            _log.Info("Start normal reading loop");
            var loopCount = _configurationSettings.ForceVPBarUpdate ? 20 : 50;
            const int loop2Count = 1;
            
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _interface.SetTimeIfNeeded();

                    if (_interface.SendLoopCommand("LOOP " + loopCount))
                        GetAndProcessLoopData(loopCount,weatherStatistics);

                    if (_configurationSettings.UseLoop2)
                        if (_interface.SendLoopCommand("LPS 2 " + loop2Count))
                            GetAndProcessLoop2Data(loop2Count,weatherStatistics);

                    if (_configurationSettings.ForceVPBarUpdate)
                        _interface.SendBarRead();

                    if (_configurationSettings.ReadReceptionStats)
                    {
                        _interface.GetReceptionStatsIfDue();
                        _log.Debug(_interface.ReceptionStatistics);
                    }
                }
            }
            // Catch the ThreadAbortException
            catch (ThreadAbortException)
            {
            }
            finally
            {
                _interface.CloseConnection();
            }
        }


        public override void Stop()
        {
            _log.Info("Closing connection");

            _log.Info("Stopping station background task");
            if (_cts != null)
                _cts.Cancel();
            _log.Info("Waiting for background task to complete");
            _backgroundTask.Wait();
        }

        private void GetAndProcessLoopData(int number, IWeatherDataStatistics weatherStatistics)
        {

            for (var i = 0; i < number; i++)
            {
                var min = DateTime.Now.Minute;

                if (min == 0)
                {
                    if (_configurationSettings.SyncTime && DateTime.Now.Hour == _configurationSettings.ClockSettingHour)
                        _interface.TimeSetNeeded = true;
                }

                var loopBytes = _interface.GetLoopData();

                var loopString = BitConverter.ToString(loopBytes);

                _log.Debug("Data " + (i + 1) + ": " + loopString);

                if (!loopString.StartsWith("LOO"))
                {
                    _log.Debug("Invalid LOOP packet");
                    // Stop the sending of LOOP packets so we can resync
                    _interface.Clear();

                    return;
                }

                // Allocate a structure for the data
                var loopData = new LoopData();

                // ...and load the data into it
                loopData.Load(loopBytes);
                loopData.Calibrations = _calibrationDictionary;

                // Process it
                _log.Debug(DateTime.Now.ToLongTimeString() + " Processing Data, i=" + i);
                //Trace.Flush();

                var dataModel = loopData.GetDataModel();

                weatherStatistics.Add(dataModel);
            }

            _log.Debug("end processing loop data");
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

        private void GetAndProcessLoop2Data(int number, IWeatherDataStatistics weatherStatistics)
        {
            _log.Debug("Processing loop2 data");

            for (var i = 0; i < number; i++)
            {
                var loopBytes = _interface.GetLoop2Data();

                var loopString = BitConverter.ToString(loopBytes);

                _log.Debug("Data " + (i + 1) + ": " + loopString);

                if (!loopString.StartsWith("LOO"))
                {
                    _log.Debug("Invalid LOOP 2 packet. Resyncing.");
                    // Stop the sending of LOOP packets so we can resync
                    _interface.Clear();

                    return;
                }

                if (Crc32Algorithm.Compute(loopBytes) != 0)
                {
                    _log.Debug("LOOP2 packet CRC invalid");
                    continue;
                }

                // Allocate a structure for the data
                var loopData = new Loop2Data(Length.FromMeters(_configurationSettings.Altitude));

                // ...and load the data into it
                loopData.Load(loopBytes);
                loopData.Calibrations = _calibrationDictionary;

                var dataModel = loopData.GetDataModel();

                weatherStatistics.Add(dataModel);
            }

            _log.Debug("End processing loop2 data");
        }

        //TODO: Move this functionality
        /*
        private void GetArchiveData()
        {
            _log.Info("Get Archive Data");

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

            _log.Info("Rollover hour = " + rollHour);

            var rolloverdone = luhour == rollHour;

            var midnightraindone = luhour == 0;

            // construct date and time of last record read
            var vantageDateStamp = cumulus.LastUpdateTime.Day + cumulus.LastUpdateTime.Month * 32 +
                                   (cumulus.LastUpdateTime.Year - 2000) * 512;
            var vantageTimeStamp = 100 * cumulus.LastUpdateTime.Hour + cumulus.LastUpdateTime.Minute;

            _log.Info(string.Format("Last Archive Date: {0}", cumulus.LastUpdateTime));
            _log.Info("Date: " + vantageDateStamp);
            _log.Info("Time: " + vantageTimeStamp);

            int currChar;

            if (isSerial)
            {
                comport.DiscardInBuffer();

                if (!WakeVP(comport))
                    _log.Info("Unable to wake VP");

                // send the command
                comport.DiscardInBuffer();

                _log.Info("Sending DMPAFT");
                comport.WriteLine("DMPAFT");

                // wait for the ACK
                currChar = comport.ReadChar();

                if (currChar != ACK)
                {
                    _log.Info("No Ack in response to DMPAFT, received 0x" + currChar.ToString("X2"));
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
                    _log.Info("No Ack in response to DMPAFT");
                    return;
                }
            }

            _log.Info("Received response to DMPAFT, sending date and time");
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

            _log.Info(BitConverter.ToString(data));

            if (isSerial)
            {
                // send the data
                comport.Write(data, 0, 6);

                // wait for the ACK
                _log.Info("Wait for ACK...");
                currChar = comport.ReadChar();

                if (currChar != ACK)
                {
                    _log.Info("No ACK, received: 0x" + currChar.ToString("X2"));
                    return;
                }

                _log.Info("ACK received");

                _log.Info("Waiting for response");
                // wait for the response
                while (comport.BytesToRead < 6)
                    // Wait a short period to let more data load into the buffer
                    Thread.Sleep(200);

                // Read the response
                comport.Read(data, 0, 6);

                var resp = "Response: ";

                for (var i = 0; i < 6; i++) resp = resp + " " + data[i].ToString("X2");
                _log.Info(resp);
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
                    _log.Info("Received 0x" + currChar.ToString("X2"));
                    if (currChar == ACK)
                        Found_ACK = true;
                }

                if (!Found_ACK)
                {
                    _log.Info("No ACK");
                    return;
                }

                _log.Info("ACK received");

                // Wait until the buffer is full 
                while (socket.Available < 6)
                    // Wait a short period to let more data load into the buffer
                    Thread.Sleep(200);

                // Read the response
                stream.Read(data, 0, 6);

                _log.Info("Response:" + BitConverter.ToString(data));
            }

            // extract number of pages and offset into first page
            var numPages = data[1] * 256 + data[0];
            var offset = data[3] * 256 + data[2];
            var bytesToRead = numPages * pageSize;
            var dataOffset = offset * recordSize + 1;
            var buff = new byte[pageSize];

            _log.Info("Reading data: " + numPages + " pages , offset = " + offset);
            Trace.Flush();

            // keep track of how many records processed for percentage display
            // but there may be some old entries in the last page
            var numtodo = numPages * 5 - offset;
            var numdone = 0;

            for (var p = 0; p < numPages; p++)
            {
                _log.Info("Reading archive page " + p);
                passCount = 0;

                // send ACK to get next page
                if (isSerial)
                    comport.Write(ACKstring, 0, 1);
                else
                    stream.Write(ACKstring, 0, 1);

                do
                {
                    passCount++;

                    _log.Info("Waiting for response");
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
                            _log.Info("The station has stopped sending archive data");
                            return;
                        }

                        // Read the response
                        _log.Info("Reading response");
                        comport.Read(buff, 0, pageSize);

                        if (cumulus.DataLogging) _log.Info("Data: " + BitConverter.ToString(buff));

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
                            _log.Info("The station has stopped sending archive data");
                            return;
                        }

                        // Read the response
                        stream.Read(buff, 0, pageSize);

                        if (cumulus.DataLogging) _log.Info("Data: " + BitConverter.ToString(buff));

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
                    _log.Info("bad CRC");
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

                    _log.Info("Loaded archive record for Page=" + p + " Record=" + r + " Timestamp=" +
                                       archiveData.Timestamp);

                    if (timestamp > lastDataReadTime)
                    {
                        _log.Info("Processing archive record for " + timestamp);

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

                        _log.Info("Page=" + p + " Record=" + r + " Timestamp=" + archiveData.Timestamp);
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
                        _log.Info("Log file entry written");

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
                            _log.Info("Day rollover " + timestamp.ToShortTimeString());
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
                        _log.Info("Ignoring old archive data");
                    }

                    numdone++;

                    _log.Info(numdone + " archive entries processed");

                    //bw.ReportProgress(numdone*100/numtodo, "processing");
                }
            }
        }
        */
        
    }
}
