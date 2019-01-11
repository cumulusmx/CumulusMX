using System;
using System.Collections.Generic;
using System.Threading;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using HidSharp;

namespace FineOffset
{
    public class FineOffsetWithSolar : IWeatherStation
    {
        private HidDevice hidDevice;
        private HidStream stream;
        private ILogger log;
        private double pressureOffset;

        public FineOffsetWithSolar()
        {
        }
        public string Identifier => "FineOffset_Solar";
        public string Manufacturer => "Fine Offset";
        public string Model => "Fine Offset+Solar";
        public string Description =>"Supports FineOffset compatible stations, with Solar sensors";

        public IEnumerable<ExtensionSetting> ConfigurationSettings { get; }
        

        private StationSettings settings;

        public void Initialise(ILogger log, ISettingsProvider settingsProvider)
        {
            this.log = log;
            settings = new StationSettings(settingsProvider);

            var devicelist = DeviceList.Local;

            //cumulus.LogMessage("Looking for Fine Offset station, VendorID=0x" + vendorId.ToString("X4") + " ProductID=0x" + productId.ToString("X4"));
            //Console.WriteLine("Looking for Fine Offset station, VendorID=0x" + vendorId.ToString("X4") + " ProductID=0x" + productId.ToString("X4"));

            hidDevice = devicelist.GetHidDeviceOrNull(vendorID: settings.VendorId, productID: settings.ProductId);
            if (hidDevice == null)
            {
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
            throw new NotImplementedException();
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
        ///     Read the 32 bytes starting at 'address'
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
                }
            }
        }




        public override void getAndProcessHistoryData()
        {
            var data = new byte[32];
            log.Debug("Current culture: " + CultureInfo.CurrentCulture.DisplayName);
            DateTime now = DateTime.Now;
            log.Debug(DateTime.Now.ToString("G"));
            log.Debug("Start reading history data");
            DateTime timestamp = DateTime.Now;
            //LastUpdateTime = DateTime.Now; // lastArchiveTimeUTC.ToLocalTime();
            log.Debug("Last Update = " + cumulus.LastUpdateTime);
            ReadAddress(0, data);

            // get address of current location
            int addr = ((data[31]) * 256) + data[30];
            int previousaddress = addr;

            log.Debug("Reading current address " + addr.ToString("X4"));
            ReadAddress(addr, data);

            var datalist = new List<HistoryData>();
            var interval = 0;
            var followingInterval = 0;
            bool moredata = true;


            while (moredata)
            {
                followingInterval = interval;
                interval = data[0];

                // calculate timestamp of previous history data
                timestamp = timestamp.AddMinutes(-interval);

                if ((interval != 255) && (timestamp > cumulus.LastUpdateTime) && (datalist.Count < maxHistoryEntries - 2))
                {
                    // Read previous data
                    addr = addr - FOentrysize;
                    if (addr == 0xF0) addr = FOMaxAddr; // wrap around

                    ReadAddress(addr, data);


                    // add history data to collection

                    var histData = new HistoryData();
                    string msg = DateTime.Now.ToLongTimeString() + " Read logger entry for " + timestamp + " address " + addr.ToString("X4") + ": ";
                    int numBytes = hasSolar ? 20 : 16;

                    for (int i = 0; i < numBytes; i++)
                    {
                        msg += data[i].ToString("X2");
                        msg += " ";
                    }

                    cumulus.LogMessage(msg);

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
                    if (hasSolar)
                    {
                        histData.uvVal = data[19];

                        histData.solarVal = (data[16] + (data[17] * 256) + (data[18] * 65536)) / 10.0;
                    }

                    datalist.Add(histData);

                    bw.ReportProgress(datalist.Count, "collecting");
                }
                else
                {
                    moredata = false;
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
    }
}
