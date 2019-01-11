using System;
using System.Collections.Generic;
using System.Threading;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using HidSharp;

namespace FineOffset
{
    public class FineOffset : IWeatherStation
    {
        private const int FO_ENTRY_SIZE = 0x10;
        private const int FO_MAX_ADDR = 0xFFF0;
        private const int MAX_HISTORY_ENTRIES = 4080;

        private const double RAIN_COUNT_PER_TIP = 0.3;

        private HidDevice hidDevice;
        private HidStream stream;
        private ILogger log;
        private double pressureOffset;
        private StationSettings settings;
        private WeatherDataModel currentData;

        public FineOffset()
        {
            currentData = new WeatherDataModel();
        }

        public string Identifier => "FineOffset";
        public string Manufacturer => "Fine Offset";
        public string Model => "Fine Offset Compatible";
        public string Description => "Supports FineOffset compatible stations";

        public IEnumerable<ExtensionSetting> ConfigurationSettings { get; }


        public void Initialise(ILogger log, ISettingsProvider settingsProvider)
        {
            this.log = log;
            settings = new StationSettings(settingsProvider);

            var devicelist = DeviceList.Local;

            log.Info("Looking for Fine Offset station, VendorID=0x" + settings.VendorId.ToString("X4") + " ProductID=0x" + settings.ProductId.ToString("X4"));

            hidDevice = devicelist.GetHidDeviceOrNull(vendorID: settings.VendorId, productID: settings.ProductId);
            if (hidDevice == null)
            {
                log.Error("Fine Offset station not found");
                throw new Exception("Fine Offset station not found");
            }

            try
            {
                log.Info("Fine Offset station found");
                log.Debug("Opening stream");
                stream = hidDevice.Open();

                log.Debug("Stream opened");
                log.Info("Connected to station");

                var data = new byte[32];
                // Get the block of data containing the abs and rel pressures
                log.Info("Reading pressure offset");
                ReadAddress(0x20, data);
                double relpressure = ((data[1] * 256) + data[0]) / 10.0f;
                double abspressure = ((data[3] * 256) + data[2]) / 10.0f;
                pressureOffset = relpressure - abspressure;
                log.Info("Rel pressure = " + relpressure);
                log.Info("Abs pressure = " + abspressure);
                log.Info("Offset       = " + pressureOffset);
            }
            catch (Exception ex)
            {
                log.Debug("Failed to open stream", ex);
                log.Error("Unable to connect to station");
            }
        }


        public WeatherDataModel GetCurrentData()
        {
            return currentData ?? new WeatherDataModel();
        }



        public void ReadPendingData()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }





        /// <summary>
        /// Read the bytes starting at 'address'
        /// </summary>
        /// <param name="address">The address of the data</param>
        /// <param name="buff">Where to return the data</param>
        private void ReadAddress(int address, byte[] buff)
        {
            log.Debug("Reading address: " + address.ToString("X6"));
            var lowbyte = (byte)(address & 0xFF);
            var highbyte = (byte)(address >> 8);

            byte[] request;
            var response = new byte[9];
            int responseLength;
            int startByte;

            if (settings.IsOSX)
            {
                request = new byte[] { 0xa1, highbyte, lowbyte, 0x20, 0xa1, highbyte, lowbyte, 0x20 };
                responseLength = 8;
                startByte = 0;
            }
            else
            {
                request = new byte[] { 0, 0xa1, highbyte, lowbyte, 0x20, 0xa1, highbyte, lowbyte, 0x20 };
                responseLength = 9;
                startByte = 1;
            }

            int ptr = 0;
            String rec_data = "";


            if (hidDevice != null)
            {
                //response = device.WriteRead(0x00, request);
                stream.Write(request);
                Thread.Sleep(settings.FineOffsetReadTime);
                for (int i = 1; i < 5; i++)
                {
                    //cumulus.LogMessage("Reading 8 bytes");
                    try
                    {
                        int count = stream.Read(response, 0, responseLength);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error reading data from station - it may need resetting", ex);
                    }

                    rec_data = " Data" + i + ": ";
                    for (int j = startByte; j < responseLength; j++)
                    {
                        rec_data += response[j].ToString("X2");
                        rec_data += " ";
                        buff[ptr++] = response[j];
                    }
                    log.Debug("Received " + rec_data);
                }
            }
        }




        private void GetAndProcessHistoryData()
        {
            DateTime now = DateTime.Now;
            log.Debug(DateTime.Now.ToString("G"));
            log.Debug("Start reading history data");
            log.Debug("Last Update = " + settings.LastUpdateTime);

            DateTime timestamp = DateTime.Now;
            var data = new byte[32];
            ReadAddress(0, data);

            // get address of current location
            int addr = (data[31] * 256) + data[30];
            int previousaddress = addr;

            log.Debug("Reading current address " + addr.ToString("X4"));
            ReadAddress(addr, data);

            var histDataList = new List<HistoryData>();
            var interval = 0;
            var followingInterval = 0;
            bool moredata = true;

            while (moredata)
            {
                followingInterval = interval;
                interval = data[0];

                // calculate timestamp of previous history data
                timestamp = timestamp.AddMinutes(-interval);

                if ((interval != 255) && (timestamp > settings.LastUpdateTime) && (histDataList.Count < MAX_HISTORY_ENTRIES - 2))
                {
                    // Read previous data
                    addr = addr - FO_ENTRY_SIZE;
                    if (addr == 0xF0)
                        addr = FO_MAX_ADDR; // wrap around

                    ReadAddress(addr, data);

                    var histData = new HistoryData();
                    string msg = "Read logger entry for " + timestamp + " address " + addr.ToString("X4") + ": ";
                    int numBytes = 16;

                    for (int i = 0; i < numBytes; i++)
                    {
                        msg += data[i].ToString("X2");
                        msg += " ";
                    }
                    log.Debug(msg);

                    histData.timestamp = timestamp;
                    histData.interval = interval;
                    histData.followinginterval = followingInterval;
                    if (data[1] == 255)
                    {
                        histData.inHum = 10;
                    }
                    else
                    {
                        histData.inHum = data[1];
                    }

                    if (data[4] == 255)
                    {
                        histData.outHum = 10;
                    }
                    else
                    {
                        histData.outHum = data[4];
                    }
                    double outtemp = ((data[5]) + (data[6] & 0x7F) * 256) / 10.0f;
                    var sign = (byte)(data[6] & 0x80);
                    if (sign == 0x80) outtemp = -outtemp;
                    if (outtemp > -200) histData.outTemp = outtemp;
                    histData.windGust = (data[10] + ((data[11] & 0xF0) * 16)) / 10.0f;
                    histData.windSpeed = (data[9] + ((data[11] & 0x0F) * 256)) / 10.0f;
                    histData.windBearing = (int)(data[12] * 22.5f);
                    histData.rainCounter = data[13] + (data[14] * 256);

                    double intemp = ((data[2]) + (data[3] & 0x7F) * 256) / 10.0f;
                    sign = (byte)(data[3] & 0x80);
                    if (sign == 0x80) intemp = -intemp;
                    histData.inTemp = intemp;
                    // Get pressure and convert to sea level
                    histData.pressure = (data[7] + (data[8] * 256)) / 10.0f + pressureOffset;
                    histData.SensorContactLost = (data[15] & 0x40) == 0x40;

                    histDataList.Add(histData);
                }
                else
                {
                    moredata = false;
                }
            }

            log.Info("Number of history entries = " + histDataList.Count);

            if (histDataList.Count > 0)
            {
                ProcessHistoryData(histDataList);
            }

        }



        private void ProcessHistoryData(List<HistoryData> datalist)
        {
            int totalentries = datalist.Count;
            log.Info("Processing history data, number of entries = " + totalentries);

            int prevraintotal = 0;
            while (datalist.Count > 0)
            {
                HistoryData historydata = datalist[datalist.Count - 1];
                WeatherDataModel model = new WeatherDataModel();

                DateTime timestamp = historydata.timestamp;

                log.Info("Processing data for " + timestamp);

               
                // Indoor Humidity ======================================================
                if ((historydata.inHum > 100) && (historydata.inHum != 255))
                    log.Warn("Ignoring bad data: InsideHumidity = " + historydata.inHum);
                else if ((historydata.inHum > 0) && (historydata.inHum != 255)) // 255 is the overflow value, when RH gets below 10% - ignore
                    model.IndoorHumidity = historydata.inHum;


                // Indoor Temperature ===================================================
                if ((historydata.inTemp > -50) && (historydata.inTemp < 50))
                    model.IndoorTemperature = historydata.inTemp;
                else
                    log.Warn($"Ignoring bad data: InsideTemp = {historydata.inTemp}");

                // Pressure =============================================================
                if ((historydata.pressure < settings.MinPressureThreshold) || (historydata.pressure > settings.MaxPressureThreshold))
                {
                    log.Warn("Ignoring bad data: pressure = " + historydata.pressure);
                    log.Warn("                   offset = " + pressureOffset);
                }
                else
                {
                    model.AbsolutePressure = historydata.pressure;
                }

                if (historydata.SensorContactLost)
                {
                    log.Error("Sensor contact lost; ignoring outdoor data");
                }
                else
                {
                    // Outdoor Humidity =====================================================
                    if ((historydata.outHum > 100) && (historydata.outHum != 255))
                        log.Warn("Ignoring bad data: outhum = " + historydata.outHum);
                    else if ((historydata.outHum > 0) && (historydata.outHum != 255)) // 255 is the overflow value, when RH gets below 10% - ignore
                        model.OutdoorHumidity = historydata.outHum;


                    // Wind =================================================================
                    if ((historydata.windGust > 60) || (historydata.windGust < 0))
                        log.Warn("Ignoring bad data: gust = " + historydata.windGust);
                    else if ((historydata.windSpeed > 60) || (historydata.windSpeed < 0))
                        log.Warn("Ignoring bad data: speed = " + historydata.windSpeed);
                    else
                    {
                        model.WindBearing = historydata.windBearing;
                        model.WindSpeed = historydata.windSpeed;
                        model.WindGust = historydata.windGust;
                    }


                    // Outdoor Temperature ==================================================
                    if ((historydata.outTemp < -50) || (historydata.outTemp > 70))
                        log.Warn("Ignoring bad data: outtemp = " + historydata.outTemp);
                    else
                        model.OutdoorTemperature = historydata.outTemp;



                    int raindiff;
                    if (prevraintotal == -1)
                    {
                        raindiff = 0;
                    }
                    else
                    {
                        raindiff = historydata.rainCounter - prevraintotal;
                    }


                    double rainrate = 0;
                    if (raindiff > 100)
                    {
                        log.Warn("Warning: large increase in rain gauge tip count: " + raindiff);
                        rainrate = 0;
                    }
                    else if (historydata.interval > 0)
                        rainrate = (raindiff * RAIN_COUNT_PER_TIP) * (60.0 / historydata.interval);


                    model.RainRate = rainrate;
                    model.RainCounter = historydata.rainCounter * RAIN_COUNT_PER_TIP;
                    prevraintotal = historydata.rainCounter;                    
                }              
             
                datalist.RemoveAt(datalist.Count - 1);
            }

            log.Info("End processing history data");
        }

    }
}
