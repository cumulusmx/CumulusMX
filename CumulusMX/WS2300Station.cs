using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using Timer = System.Timers.Timer;

namespace CumulusMX
{
    internal class WS2300Station : WeatherStation
    {
        private const int MAXWINDRETRIES = 20;
        private const int ERROR = -1000;
        private const byte WRITEACK = 0x10;
        private const byte SETBIT = 0x12;
        private const byte SETACK = 0x04;
        private const byte UNSETBIT = 0x32;
        private const byte UNSETACK = 0x0C;
        private const int MAXRETRIES = 50;

        private List<historyData> datalist;
        private double rainref;
        private double raincountref;
        private int previoushum = 999;
        private double previoustemp = 999;
        private double previouspress = 9999;
        private double previouswind = 999;

        public WS2300Station(Cumulus cumulus) : base(cumulus)
        {
            cumulus.Manufacturer = cumulus.LACROSSE;
            calculaterainrate = true;

            cumulus.LogMessage("WS2300: Attempting to open " + cumulus.ComportName);

            comport = new SerialPort(cumulus.ComportName, 2400, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = true,
                ReadTimeout = 500,
                WriteTimeout = 1000
            };

            try
            {
                comport.Open();
                cumulus.LogMessage("COM port opened");
            }
            catch (Exception ex)
            {
                cumulus.LogMessage(ex.Message);
                //MessageBox.Show(ex.Message);
            }

            if (comport.IsOpen)
            {
                // Read the data from the logger
                cumulus.CurrentActivity = "Reading archive data";
                startReadingHistoryData();
            }
        }

        public override void startReadingHistoryData()
        {
            cumulus.LogMessage("Start reading history data");
            //lastArchiveTimeUTC = getLastArchiveTime();

            LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

            bw = new BackgroundWorker();
            //histprog = new historyProgressWindow();
            //histprog.Owner = mainWindow;
            //histprog.Show();
            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
            //bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
            bw.WorkerReportsProgress = true;
            bw.RunWorkerAsync();
        }

        public override void Stop()
        {
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //histprog.histprogTB.Text = "Processed 100%";
            //histprog.histprogPB.Value = 100;
            //histprog.Close();
            //mainWindow.FillLastHourGraphData();
            cumulus.CurrentActivity = "Normal running";
            StartLoop();
            DoDayResetIfNeeded();
            DoTrendValues(DateTime.Now);
            cumulus.StartTimers();
        }


        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            getAndProcessHistoryData();
        }

        public override void getAndProcessHistoryData()
        {
            int interval;
            int countdown;
            timestamp ts;
            int numrecs;

            cumulus.LogMessage("Reading history info");
            int rec = ws2300ReadHistoryDetails(out interval, out countdown, out ts, out numrecs);

            if (rec < 0)
                cumulus.LogMessage("Failed to read history data");
            else
            {
                cumulus.LogMessage("History info obtained");
                datalist = new List<historyData>();

                double pressureoffset = ws2300PressureOffset();

                if (pressureoffset > -1000)
                    cumulus.LogMessage("Pressure offset = " + pressureoffset);
                else
                {
                    pressureoffset = 0;
                    cumulus.LogMessage("Failed to read pressure offset, using zero");
                }
                cumulus.LogMessage("Downloading history data");

                datalist.Clear();

                DateTime recordtime;// = ws2300TimestampToDateTime(ts);

                if (cumulus.WS2300IgnoreStationClock)
                {
                    // assume latest archive record is 'now'
                    recordtime = DateTime.Now;
                }
                else
                {
                    // use time from station
                    recordtime = ws2300TimestampToDateTime(ts);
                }

                while ((numrecs > 0) && (recordtime > cumulus.LastUpdateTime))
                {
                    int address, inhum, outhum;
                    double intemp, outtemp, press, raincount, windspeed, bearing, dewpoint, windchill;


                    if (
                        ws2300ReadHistoryRecord(rec, out address, out intemp, out outtemp, out press, out inhum, out outhum, out raincount, out windspeed, out bearing, out dewpoint,
                            out windchill) < 0)
                    {
                        cumulus.LogMessage("Error reading history record");
                        numrecs = 0;
                        datalist.Clear();
                    }
                    else
                    {
                        historyData histData = new historyData();

                        histData.timestamp = recordtime;
                        histData.interval = interval;
                        histData.address = address;
                        histData.inHum = inhum;
                        histData.inTemp = intemp;
                        histData.outHum = outhum;
                        histData.outTemp = outtemp;
                        histData.pressure = press;
                        histData.rainTotal = raincount;
                        histData.windBearing = (int)bearing;
                        histData.windGust = windspeed;
                        histData.windSpeed = windspeed;
                        histData.dewpoint = dewpoint;
                        histData.windchill = windchill;

                        datalist.Add(histData);
                        recordtime = recordtime.AddMinutes(-interval);
                        numrecs--;
                        rec--;

                        if (rec < 0) rec = 0xAE;

                        bw.ReportProgress(datalist.Count, "collecting");
                    }
                }
            }

            cumulus.LogMessage("Number of history entries = " + datalist.Count);

            if (datalist.Count > 0)
            {
                processHistoryData();
            }

            //using (cumulusEntities dataContext = new cumulusEntities())
            //{
            //    UpdateHighsAndLows(dataContext);
            //}
        }

        private void processHistoryData()
        {
            // history data is alread in correct units
            int totalentries = datalist.Count;
            int rollHour = Math.Abs(cumulus.GetHourInc());
            int luhour = cumulus.LastUpdateTime.Hour;

            bool rolloverdone = luhour == rollHour;

            bool midnightraindone = luhour == 0;

            double prevraintotal = -1;
            double raindiff, rainrate;

            double pressureoffset = ConvertPressMBToUser(ws2300PressureOffset());

            while (datalist.Count > 0)
            {
                historyData historydata = datalist[datalist.Count - 1];

                DateTime timestamp = historydata.timestamp;

                cumulus.LogMessage("Processing data for " + timestamp);
                // Check for rollover

                int h = timestamp.Hour;

                if (h != rollHour)
                {
                    rolloverdone = false;
                }

                if ((h == rollHour) && !rolloverdone)
                {
                    // do rollover
                    cumulus.LogMessage("WS2300: Day rollover " + timestamp);
                    DayReset(timestamp);

                    rolloverdone = true;
                }

                // handle rain since midnight reset
                if (h != 0)
                {
                    midnightraindone = false;
                }

                if ((h == 0) && !midnightraindone)
                {
                    ResetMidnightRain(timestamp);
                    ResetSunshineHours();
                    midnightraindone = true;
                }

                // Humidity ====================================================================
                if ((historydata.inHum > 0) && (historydata.inHum <= 100))
                    DoIndoorHumidity(historydata.inHum);
                if ((historydata.outHum > 0) && (historydata.outHum <= 100))
                    DoOutdoorHumidity(historydata.outHum, timestamp);

                // Wind ========================================================================
                if (historydata.windSpeed < cumulus.LCMaxWind)
                {
                    DoWind(historydata.windGust, historydata.windBearing, historydata.windSpeed, timestamp);
                }

                // Temperature ==================================================================
                if ((historydata.outTemp > -50) && (historydata.outTemp < 50))
                {
                    DoOutdoorTemp(historydata.outTemp, timestamp);

                    tempsamplestoday = tempsamplestoday + historydata.interval;
                    TempTotalToday = TempTotalToday + (OutdoorTemperature * historydata.interval);

                    if (OutdoorTemperature < cumulus.ChillHourThreshold)
                    // add 1 minute to chill hours
                    {
                        ChillHours += (historydata.interval / 60.0);
                    }
                }

                if ((historydata.inTemp > -50) && (historydata.inTemp < 50))
                    DoIndoorTemp(historydata.inTemp);

                // Rain ==========================================================================
                if (prevraintotal < 0)
                {
                    raindiff = 0;
                }
                else
                {
                    raindiff = historydata.rainTotal - prevraintotal;
                }

                if (historydata.interval > 0)
                {
                    rainrate = (raindiff) * (60 / historydata.interval);
                }
                else
                {
                    rainrate = 0;
                }

                cumulus.LogMessage("WS2300: History rain total = " + historydata.rainTotal);

                DoRain(historydata.rainTotal, rainrate, timestamp);

                prevraintotal = historydata.rainTotal;

                // Dewpoint ====================================================================
                if (cumulus.CalculatedDP)
                {
                    double tempC = ConvertUserTempToC(OutdoorTemperature);
                    DoOutdoorDewpoint(ConvertTempCToUser(MeteoLib.DewPoint(tempC, OutdoorHumidity)), timestamp);
                    CheckForDewpointHighLow(timestamp);
                }
                else
                {
                    if (historydata.dewpoint < ConvertUserTempToC(60))
                    {
                        DoOutdoorDewpoint(CalibrateTemp(historydata.dewpoint), timestamp);
                    }
                }

                // Windchill ==================================================================
                if (cumulus.CalculatedWC)
                {
                    if (ConvertUserWindToMS(WindAverage) < 1.5)
                    {
                        DoWindChill(OutdoorTemperature, timestamp);
                    }
                    else
                    {
                        // calculate wind chill from calibrated C temp and calibrated win in KPH
                        DoWindChill(ConvertTempCToUser(MeteoLib.WindChill(ConvertUserTempToC(OutdoorTemperature), ConvertUserWindToKPH(WindAverage))), timestamp);
                    }
                }
                else
                {
                    if (historydata.windchill < ConvertTempCToUser(60))
                    {
                        DoWindChill(historydata.windchill, timestamp);
                    }
                }

                // Wind run ======================================================================
                cumulus.LogMessage("Windrun: " + WindAverage.ToString(cumulus.WindFormat) + cumulus.WindUnitText + " for " + historydata.interval + " minutes = " +
                                (WindAverage * WindRunHourMult[cumulus.WindUnit] * historydata.interval / 60.0).ToString(cumulus.WindRunFormat) + cumulus.WindRunUnitText);

                WindRunToday += (WindAverage * WindRunHourMult[cumulus.WindUnit] * historydata.interval / 60.0);

                CheckForWindrunHighLow(timestamp);

                // Pressure ======================================================================
                double slpress = historydata.pressure + pressureoffset;

                if ((slpress > ConvertPressMBToUser(900)) && (slpress < ConvertPressMBToUser(1200)))
                {
                    DoPressure(slpress, timestamp);
                }

                // update heating/cooling degree days
                UpdateDegreeDays(historydata.interval);

                DoApparentTemp(timestamp);

                CalculateDominantWindBearing(Bearing, WindAverage, historydata.interval);

                bw.ReportProgress((totalentries - datalist.Count) * 100 / totalentries, "processing");

                //UpdateDatabase(timestamp.ToUniversalTime(), historydata.interval, false);

                cumulus.DoLogFile(timestamp, false);
                if (cumulus.LogExtraSensors)
                {
                    cumulus.DoExtraLogFile(timestamp);
                }

                AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
                AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex, IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV);
                AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
                AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex, OutdoorHumidity,
                            Pressure, RainToday, SolarRad, UV, Raincounter);
                RemoveOldLHData(timestamp);
                RemoveOldL3HData(timestamp);
                RemoveOldGraphData(timestamp);
                DoTrendValues(timestamp);
                UpdatePressureTrendString();
                UpdateStatusPanel(timestamp);
                cumulus.AddToWebServiceLists(timestamp);

                datalist.RemoveAt(datalist.Count - 1);
            }
        }

        private DateTime ws2300TimestampToDateTime(timestamp ts)
        {
            return new DateTime(ts.year, ts.month, ts.day, ts.hour, ts.minute, 0);
        }

        public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Opens the serial port and starts the reading loop
        /// </summary>
        public override void Start()
        {
            try
            {
                while (true)
                {
                    GetAndProcessData();
                    Thread.Sleep(5000);
                }
            }
            // Catch the ThreadAbortException
            catch (ThreadAbortException)
            {
            }
        }

        private struct timestamp
        {
            public int minute;
            public int hour;
            public int day;
            public int month;
            public int year;
        };


        /// <summary>
        /// Read history info - interval, countdown etc
        /// </summary>
        /// <param name="interval">Current interval in minutes</param>
        /// <param name="countdown">Countdown to next measurement</param>
        /// <param name="timelast">Time/Date for last measurement</param>
        /// <param name="numrecords">number of valid records</param>
        /// <returns>address of last written record</returns>
        private int ws2300ReadHistoryDetails(out int interval, out int countdown, out timestamp timelast, out int numrecords)
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x6B2;
            int bytes = 10;

            timestamp loctimelast = new timestamp();

            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
            {
                interval = 0;
                countdown = 0;
                timelast = new timestamp();
                numrecords = 0;
                return ERROR;
            }

            rainref = ws2300RainTotal();

            if (rainref < 0)
            {
                cumulus.LogMessage("WS2300: Unable to read current rain total");
                interval = 0;
                countdown = 0;
                timelast = new timestamp();
                numrecords = 0;
                return -1000;
            }
            else
            {
                cumulus.LogMessage("WS2300: current rain total from station = " + rainref);
                raincountref = ws2300RainHistoryRef();

                if (raincountref < 0)
                {
                    cumulus.LogMessage("WS2300: Unable to read current rain counter");
                    interval = 0;
                    countdown = 0;
                    timelast = new timestamp();
                    numrecords = 0;
                    return -1000;
                }
                else
                {
                    cumulus.LogMessage("WS2300: current rain counter from station = " + raincountref);

                    interval = (data[1] & 0xF) * 256 + data[0] + 1;
                    countdown = data[2] * 16 + (data[1] >> 4) + 1;
                    loctimelast.minute = ((data[3] >> 4) * 10) + (data[3] & 0xF);
                    loctimelast.hour = ((data[4] >> 4) * 10) + (data[4] & 0xF);
                    loctimelast.day = ((data[5] >> 4) * 10) + (data[5] & 0xF);
                    loctimelast.month = ((data[6] >> 4) * 10) + (data[6] & 0xF);
                    loctimelast.year = 2000 + ((data[7] >> 4) * 10) + (data[7] & 0xF);
                    numrecords = data[9];

                    timelast = loctimelast;
                    return data[8];
                }
            }
        }

        private double ws2300RainHistoryRef()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x440;
            int bytes = 2;

            cumulus.LogMessage("WS2300: Reading rain history ref");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return -1000;
            else
                return ((data[1] & 0x0F) << 8) + (data[0]);
        }

        private int ws2300ReadHistoryRecord(int record, out int address, out double tempindoor, out double tempoutdoor, out double pressure, out int humindoor, out int humoutdoor,
            out double raincount, out double windspeed, out double winddir, out double dewpoint, out double windchill)
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];

            int bytes = 10;
            int tempint;

            address = 0x6C6 + record * 19;
            cumulus.LogMessage("Reading history record " + record);
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
            {
                cumulus.LogMessage("Failed to read history record");
                tempindoor = 0;
                tempoutdoor = 0;
                pressure = 0;
                humindoor = 0;
                humoutdoor = 0;
                raincount = 0;
                windspeed = 0;
                winddir = 0;
                dewpoint = 0;
                windchill = 0;
                return -1000;
            }

            string msg = "History record read: ";
            for (int n = 0; n < bytes; n++)
            {
                msg += data[n].ToString("X2");
                msg += " ";
            }

            cumulus.LogMessage(msg);
            tempint = (data[4] << 12) + (data[3] << 4) + (data[2] >> 4);

            pressure = 1000 + (tempint % 10000) / 10.0;

            if (pressure >= 1502.2)
                pressure = pressure - 1000;

            pressure = ConvertPressMBToUser(pressure);

            humindoor = (int)((tempint - (tempint % 10000)) / 10000.0);

            humoutdoor = (data[5] >> 4) * 10 + (data[5] & 0xF);

            raincount = ConvertRainMMToUser(rainref + (((data[7] & 0xF) * 256 + data[6]) - raincountref) * 0.518);

            windspeed = (data[8] * 16 + (data[7] >> 4)) / 10.0;

            if (windspeed > 49.9)
            {
                // probably lost sensor contact
                windspeed = 0;
            }

            // need wind in kph for chill calc
            double windkmh = 3.6 * windspeed;

            tempint = ((data[2] & 0xF) << 16) + (data[1] << 8) + data[0];
            tempindoor = ConvertTempCToUser((tempint % 1000) / 10.0f - 30.0);
            tempoutdoor = (tempint - (tempint % 1000)) / 10000.0f - 30.0;

            windchill = ConvertTempCToUser(MeteoLib.WindChill(tempoutdoor, windkmh));
            dewpoint = ConvertTempCToUser(MeteoLib.DewPoint(tempoutdoor, humoutdoor));

            tempoutdoor = ConvertTempCToUser(tempoutdoor);

            windspeed = ConvertWindMSToUser(windspeed);
            winddir = (data[9] & 0xF) * 22.5;


            return (++record) % 0xAF;
        }


        private void GetAndProcessData()
        {
            string presstrend, forecast;
            double direction;

            DateTime now = DateTime.Now;

            // Indoor humidity =====================================================================
            int inhum = ws2300IndoorHumidity();
            if (inhum > -1 && inhum < 101)
            {
                DoIndoorHumidity(inhum);
            }

            // Outdoor humidity ====================================================================
            int outhum = ws2300OutdoorHumidity();
            if ((outhum > 0) && (outhum <= 100) && ((previoushum == 999) || (Math.Abs(outhum - previoushum) < cumulus.EWhumiditydiff)))
            {
                previoushum = outhum;
                DoOutdoorHumidity(outhum, now);
            }

            // Indoor temperature ==================================================================
            double intemp = ws2300IndoorTemperature();
            if (intemp > -20)
            {
                DoIndoorTemp(ConvertTempCToUser(intemp));
            }

            // Outdoor temperature ================================================================
            double outtemp = ws2300OutdoorTemperature();
            if ((outtemp > -60) && (outtemp < 60) && ((previoustemp == 999) || (Math.Abs(outtemp - previoustemp) < cumulus.EWtempdiff)))
            {
                previoustemp = outtemp;
                DoOutdoorTemp(ConvertTempCToUser(outtemp), now);
            }

            // Outdoor dewpoint ==================================================================
            double dp = ws2300OutdoorDewpoint();
            if (dp > -100 && dp < 60)
            {
                DoOutdoorDewpoint(ConvertTempCToUser(dp), now);
            }

            // Pressure ==========================================================================
            double pressure = ws2300RelativePressure();
            if ((pressure > 900) && (pressure < 1200) && ((previouspress == 9999) || (Math.Abs(pressure - previouspress) < cumulus.EWpressurediff)))
            {
                previouspress = pressure;
                DoPressure(ConvertPressMBToUser(pressure), now);
            }

            pressure = ws2300AbsolutePressure();

            if ((Pressure > 850) && (Pressure < 1200))
            {
                StationPressure = pressure + cumulus.PressOffset;
                // AltimeterPressure := ConvertOregonPress(StationToAltimeter(PressureHPa(StationPressure),AltitudeM(Altitude)));
            }

            // Pressure trend and forecast =======================================================
            int res = ws2300PressureTrendAndForecast(out presstrend, out forecast);
            if (res > ERROR)
            {
                DoForecast(forecast, false);
                DoPressTrend(presstrend);
            }

            // Wind ============================================================================
            double wind = ws2300CurrentWind(out direction);
            if ((wind > -1) && ((previouswind == 999) || (Math.Abs(wind - previouswind) < cumulus.EWwinddiff)))
            {
                previouswind = wind;
                DoWind(ConvertWindMSToUser(wind), (int)direction, ConvertWindMSToUser(wind), now);
            }
            else
            {
                cumulus.LogDebugMessage("Ignoring wind reading: wind=" + wind.ToString("F1") + " previouswind=" + previouswind.ToString("F1") + " sr=" +
                                        cumulus.EWwinddiff.ToString("F1"));
            }

            // wind chill
            if (cumulus.CalculatedWC)
            {
                DoWindChill(OutdoorTemperature, now);
            }
            else
            {
                double wc = ws2300WindChill();
                if (wc > -100 && wc < 60)
                {
                    DoWindChill(ConvertTempCToUser(wc), now);
                }
            }

            // Rain ===========================================================================
            double raintot = ws2300RainTotal();
            if (raintot > -1)
            {
                DoRain(ConvertRainMMToUser(raintot), -1, now);
            }

            DoApparentTemp(DateTime.Now);

            UpdateStatusPanel(DateTime.Now);
        }

        /// <summary>
        /// Read indoor temperature
        /// </summary>
        /// <returns>Indoor temp in C</returns>
        private double ws2300IndoorTemperature()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x346;
            int bytes = 2;
            cumulus.LogDataMessage("Reading indoor temp");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return ((data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10.0F + (data[0] & 0xF) / 100.0F) - 30.0;
        }

        /// <summary>
        /// Read outdoor temperature
        /// </summary>
        /// <returns>Outdoor temp in C</returns>
        private double ws2300OutdoorTemperature()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x373;
            int bytes = 2;
            cumulus.LogDataMessage("Reading outdoor temp");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return ((data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10.0F + (data[0] & 0xF) / 100.0F) - 30.0;
        }

        /// <summary>
        /// Read outdoor dew point
        /// </summary>
        /// <returns>dew point in C</returns>
        private double ws2300OutdoorDewpoint()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x3CE;
            int bytes = 2;

            cumulus.LogDataMessage("Reading outdoor dewpoint");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return ((data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10.0F + (data[0] & 0xF) / 100.0F) - 30.0;
        }

        /// <summary>
        /// Read indoor humidity
        /// </summary>
        /// <returns>humidity in %</returns>
        private int ws2300IndoorHumidity()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x3FB;
            int bytes = 1;

            cumulus.LogDataMessage("Reading indoor humidity");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return ((data[0] >> 4) * 10 + (data[0] & 0xF));
        }

        /// <summary>
        /// Read outdoor humidity
        /// </summary>
        /// <returns>humidity in %</returns>
        private int ws2300OutdoorHumidity()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x419;
            int bytes = 1;

            cumulus.LogDataMessage("Reading outdoor humidity");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return ((data[0] >> 4) * 10 + (data[0] & 0xF));
        }

        /// <summary>
        /// Get current wind speed and direction
        /// </summary>
        /// <param name="direction">returns direction in degrees</param>
        /// <returns>speed in m/s</returns>
        private double ws2300CurrentWind(out double direction)
        {
            // 0527  0 Wind overflow flag: 0 = normal, 5=wind sensor disconnected
            // 0528  0 Wind minimum code: 0=min, 1=--.-, 2=OFL (overflow)
            // 0529  1 Windspeed: binary nibble 0 [m/s * 10]
            // 052A  1 Windspeed: binary nibble 1 [m/s * 10]
            // 052B  2 Windspeed: binary nibble 2 [m/s * 10]
            // 052C  2 Wind Direction = nibble * 22.5 degrees, clockwise from North

            cumulus.LogDataMessage("Reading wind data");
            byte[] data = new byte[20];
            byte[] command = new byte[25];

            int address = 0x527; //Windspeed and direction
            int bytes = 3;

            direction = 0;

            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            if (((data[0] & 0xF7) != 0x00) || //Invalid wind data
                ((data[1] == 0xFF) && (((data[2] & 0xF) == 0) || ((data[2] & 0xF) == 1))))
                return ERROR;

            //Calculate wind direction
            direction = (data[2] >> 4) * 22.5;

            //Calculate wind speed
            return (((data[2] & 0xF) << 8) + (data[1])) / 10.0;
        }


        /// <summary>
        /// Read wind chill
        /// </summary>
        /// <returns>wind chill in C</returns>
        private double ws2300WindChill()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x3A0;
            int bytes = 2;

            cumulus.LogDataMessage("Reading wind chill");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return ((data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10F + (data[0] & 0xF) / 100F) - 30;
        }

        /// <summary>
        /// Read rain total
        /// </summary>
        /// <returns>Rain total in mm</returns>
        private double ws2300RainTotal()
        {
            //cumulus.LogMessage("Reading rain total");
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x4D2;
            int bytes = 3;

            cumulus.LogDataMessage("Reading rain total");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return (data[2] >> 4) * 1000 + (data[2] & 0xF) * 100 + (data[1] >> 4) * 10 + (data[1] & 0xF) + (data[0] >> 4) / 10F + (data[0] & 0xF) / 100.0;
        }

        /// <summary>
        /// Read sea-level pressure
        /// </summary>
        /// <returns>SLP in mb</returns>
        private double ws2300RelativePressure()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x5E2;
            int bytes = 3;

            cumulus.LogDataMessage("Reading relative pressure");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return (data[2] & 0xF) * 1000 + (data[1] >> 4) * 100 + (data[1] & 0xF) * 10 + (data[0] >> 4) + (data[0] & 0xF) / 10.0;
        }

        /// <summary>
        /// Read local pressure
        /// </summary>
        /// <returns>abs press in mb</returns>
        private double ws2300AbsolutePressure()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];
            int address = 0x5D8;
            int bytes = 3;

            cumulus.LogDataMessage("Reading absolute pressure");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;

            return (data[2] & 0xF) * 1000 + (data[1] >> 4) * 100 + (data[1] & 0xF) * 10 + (data[0] >> 4) + (data[0] & 0xF) / 10.0;
        }

        /// <summary>
        /// Get pressure offset (sea level - station)
        /// </summary>
        /// <returns>offset in mb</returns>
        private double ws2300PressureOffset()
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];

            int address = 0x5EC;
            int bytes = 3;

            cumulus.LogDataMessage("Reading pressure offset");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
                return ERROR;


            return (data[2] & 0xF) * 1000 + (data[1] >> 4) * 100 + (data[1] & 0xF) * 10 + (data[0] >> 4) + (data[0] & 0xF) / 10.0 - 1000;
        }

        /// <summary>
        /// Read pressure trend and forecast
        /// </summary>
        /// <param name="pressuretrend"></param>
        /// <param name="forecast"></param>
        /// <returns></returns>
        private int ws2300PressureTrendAndForecast(out string pressuretrend, out string forecast)
        {
            byte[] data = new byte[20];
            byte[] command = new byte[25];

            int address = 0x26B;
            int bytes = 1;
            string[] presstrendstrings = new string[] { "Steady", "Rising", "Falling" };
            string[] forecaststrings = new string[] { "Rainy", "Cloudy", "Sunny" };

            cumulus.LogDataMessage("Reading press trend and forecast");
            if (ws2300ReadWithRetries(address, bytes, data, command) != bytes)
            {
                pressuretrend = "";
                forecast = "";
                return ERROR;
            }

            pressuretrend = presstrendstrings[data[0] >> 4];
            forecast = forecaststrings[data[0] & 0xF];

            return 0;
        }

        /// <summary>
        /// Writes to serial port with retries
        /// </summary>
        /// <param name="address"></param>
        /// <param name="number"></param>
        /// <param name="encode_constant"></param>
        /// <param name="writedata"></param>
        /// <param name="commanddata"></param>
        /// <returns></returns>
        private int ws2300WriteWithRetries(int address, int number, byte encode_constant, byte[] writedata, byte[] commanddata)
        {
            int i;

            for (i = 0; i < MAXRETRIES; i++)
            {
                // reset before writing
                ws2300SendReset();

                // Read the data. If expected number of bytes read break out of loop.
                if (ws2300WriteData(address, number, encode_constant, writedata, commanddata) == number)
                {
                    break;
                }
            }

            // If we have tried MAXRETRIES times to read we expect not to have valid data
            if (i == MAXRETRIES)
            {
                return -1;
            }

            return number;
        }

        /// <summary>
        /// Writes data to the station
        /// </summary>
        /// <param name="address"></param>
        /// <param name="number"></param>
        /// <param name="encodeConstant"></param>
        /// <param name="writeData"></param>
        /// <param name="commandData"></param>
        /// <returns></returns>
        private int ws2300WriteData(int address, int number, byte encodeConstant, byte[] writeData, byte[] commandData)
        {
            byte answer;
            byte[] encodedData = new byte[80];
            int i = 0;
            byte ackConstant = WRITEACK;

            if (encodeConstant == SETBIT)
            {
                ackConstant = SETACK;
            }
            else if (encodeConstant == UNSETBIT)
            {
                ackConstant = UNSETACK;
            }

            cumulus.LogDataMessage("ws2300WriteData");
            // First 4 bytes are populated with converted address range 0000-13XX
            ws2300EncodeAddress(address, commandData);
            // populate the encoded_data array
            ws2300DataEncoder(number, encodeConstant, writeData, encodedData);

            //Write the 4 address bytes
            for (i = 0; i < 4; i++)
            {
                if (ws2300WriteSerial(commandData[i]) != 1)
                    return -1;
                if (ws2300ReadSerial(out answer) != 1)
                    return -1;
                if (answer != ws2300commandChecksum0to3(commandData[i], i))
                    return -1;
            }

            //Write the data nibbles or set/unset the bits
            for (i = 0; i < number; i++)
            {
                if (ws2300WriteSerial(encodedData[i]) != 1)
                    return -1;
                if (ws2300ReadSerial(out answer) != 1)
                    return -1;
                if (answer != (writeData[i] + ackConstant))
                    return -1;
                commandData[i + 4] = encodedData[i];
            }

            cumulus.LogDataMessage("Exit ws2300WriteData with success");

            return i;
        }

        /// <summary>
        /// Read data, retry until success or maxretries
        /// </summary>
        /// <param name="address"></param>
        /// <param name="number"></param>
        /// <param name="readdata"></param>
        /// <param name="commanddata"></param>
        /// <returns></returns>
        private int ws2300ReadWithRetries(int address, int number, byte[] readdata, byte[] commanddata)
        {
            int i;

            for (i = 0; i < MAXRETRIES; i++)
            {
                ws2300SendReset();

                // Read the data. If expected number of bytes read break out of loop.
                if (ws2300readData(address, number, readdata, commanddata) == number)
                {
                    break;
                }
            }

            // If we have tried MAXRETRIES times to read we expect not to
            // have valid data
            if (i == MAXRETRIES)
            {
                cumulus.LogDebugMessage("Max read retries exceeded");
                return -1;
            }

            string msg = "Data read: ";
            for (int n = 0; n < number; n++)
            {
                msg += readdata[n].ToString("X2");
                msg += " ";
            }

            cumulus.LogDataMessage(msg);

            return number;
        }

        /// <summary>
        /// Read data from the station
        /// </summary>
        /// <param name="address"></param>
        /// <param name="numberofbytes"></param>
        /// <param name="readData"></param>
        /// <param name="commandData"></param>
        /// <returns>number of bytes read</returns>
        private int ws2300readData(int address, int numberofbytes, byte[] readData, byte[] commandData)
        {
            byte answer;
            int i;

            // First 4 bytes are populated with converted address range 0000-13B0
            ws2300EncodeAddress(address, commandData);
            // Now populate the 5th byte with the converted number of bytes
            commandData[4] = ws2300encodeNumberOfBytes(numberofbytes);

            //cumulus.LogMessage("WS2300ReadData");
            for (i = 0; i < 4; i++)
            {
                if (ws2300WriteSerial(commandData[i]) != 1)
                    return -1;
                if (ws2300ReadSerial(out answer) != 1)
                    return -1;
                if (answer != ws2300commandChecksum0to3(commandData[i], i))
                    return -1;
            }

            // Send the final command that asks for number of bytes and check answer
            if (ws2300WriteSerial(commandData[4]) != 1)
                return -1;
            if (ws2300ReadSerial(out answer) != 1)
                return -1;
            if (answer != ws2300commandChecksum4(numberofbytes))
                return -1;

            // Read the data bytes
            for (i = 0; i < numberofbytes; i++)
            {
                if (ws2300ReadSerial(out readData[i]) != 1)
                    return -1;
            }

            // Read and verify checksum
            if (ws2300ReadSerial(out answer) != 1)
                return -1;
            if (answer != dataChecksum(readData, numberofbytes))
                return -1;

            return i;
        }

        /// <summary>
        /// Calculates checksum for final command byte
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        private byte ws2300commandChecksum4(int number)
        {
            return (byte)(number + 0x30);
        }

        /// <summary>
        /// Calculates the checksum for the data received from the station
        /// </summary>
        /// <param name="data"></param>
        /// <param name="numberofbytes"></param>
        /// <returns></returns>
        private byte dataChecksum(byte[] data, int numberofbytes)
        {
            int checksum = 0;

            for (int i = 0; i < numberofbytes; i++)
            {
                checksum += data[i];
            }

            return (byte)(checksum & 0xFF);
        }

        /// <summary>
        /// Converts 'number of bytes to read' to form expected by station
        /// </summary>
        /// <param name="number">number to be encoded</param>
        /// <returns></returns>
        private byte ws2300encodeNumberOfBytes(int number)
        {
            byte encodednumber;

            encodednumber = (byte)(0xC2 + number * 4);

            if (encodednumber > 0xfe)
                encodednumber = 0xfe;

            return encodednumber;
        }

        /// <summary>
        /// calculates the checksum for the first 4 commands sent to the station
        /// </summary>
        /// <param name="command"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private byte ws2300commandChecksum0to3(byte command, int sequence)
        {
            return (byte)(sequence * 16 + ((command) - 0x82) / 4);
        }

        /// <summary>
        /// Converts up to 15 data bytes to the form needed when sending write commands
        /// </summary>
        /// <param name="number"></param>
        /// <param name="encodeConstant"></param>
        /// <param name="dataIn"></param>
        /// <param name="dataOut"></param>
        private void ws2300DataEncoder(int number, byte encodeConstant, byte[] dataIn, byte[] dataOut)
        {
            for (int i = 0; i < number; i++)
            {
                dataOut[i] = (byte)(encodeConstant + (dataIn[i] * 4));
            }
        }

        /// <summary>
        /// Converts addresses to the form required by the station when sending commands
        /// </summary>
        /// <param name="addressIn">Address to be encoded</param>
        /// <param name="addressOut">Encoded address</param>
        private void ws2300EncodeAddress(int addressIn, byte[] addressOut)
        {
            const int numbytes = 4;

            for (int i = 0; i < numbytes; i++)
            {
                byte nibble = (byte)((addressIn >> (4 * (3 - i))) & 0x0F);
                addressOut[i] = (byte)(0x82 + (nibble * 4));
            }
        }

        /// <summary>
        /// Reset the station by sending command 06
        /// </summary>
        /// <returns>True if successful</returns>
        private bool ws2300SendReset()
        {
            byte command = 0x06;
            byte answer;

            cumulus.LogDataMessage("Sending reset");

            for (int i = 0; i < 100; i++)
            {
                comport.DiscardInBuffer();

                ws2300WriteSerial(command);

                // Occasionally 0, then 2 is returned.  If zero comes back, continue
                // reading as this is more efficient than sending an out-of sync
                // reset and letting the data reads restore synchronization.
                // Occasionally, multiple 2's are returned.  Read with a fast timeout
                // until all data is exhausted, if we got a two back at all, we
                // consider it a success

                while (ws2300ReadSerial(out answer) == 1)
                {
                    if (answer == 2)
                    {
                        // clear anything that might come after the response
                        comport.DiscardInBuffer();

                        cumulus.LogDataMessage("Reset done, retries = " + i);
                        return true;
                    }
                }

                Thread.Sleep(5 * i);
            }

            return false;
        }

        /// <summary>
        /// Read a byte from the serial port
        /// </summary>
        /// <param name="answer">The byte that was read</param>
        /// <returns>Number of bytes read</returns>
        private int ws2300ReadSerial(out byte answer)
        {
            cumulus.LogDataMessage("ReadSerial");
            try
            {
                answer = (byte)comport.ReadByte();
            }
            catch (Exception ex)
            {
                cumulus.LogDebugMessage("ReadSerial error " + ex.Message);
                answer = 0;
                return 0;
            }

            cumulus.LogDataMessage("ReadSerial success, data = " + answer.ToString("X2"));
            return 1;
        }

        /// <summary>
        /// Write a byte to the serial port
        /// </summary>
        /// <param name="command">The byte to be written</param>
        /// <returns>Number of bytes written</returns>
        private int ws2300WriteSerial(byte command)
        {
            cumulus.LogDataMessage("Writing command " + command.ToString("X2"));
            byte[] towrite = new byte[1];
            towrite[0] = command;

            try
            {
                comport.Write(towrite, 0, 1);
            }
            catch (Exception ex)
            {
                cumulus.LogDebugMessage("WriteSerial error " + ex.Message);
                return 0;
            }

            cumulus.LogDataMessage("WriteSerial success");
            return 1;
        }

        private class historyData
        {
            public DateTime timestamp;

            public int address;

            public int interval;

            public int inHum;

            public int outHum;

            public double inTemp;

            public double outTemp;

            public double windGust;

            public double windSpeed;

            public int windBearing;

            public double pressure;

            public double rainTotal;

            public double dewpoint;

            public double windchill;
        }
    }
}