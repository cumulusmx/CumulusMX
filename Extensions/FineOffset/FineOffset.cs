using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using HidSharp;
using UnitsNet;

namespace FineOffset
{
    public class FineOffset : IWeatherStation
    {
        protected int FO_ENTRY_SIZE;
        protected int FO_MAX_ADDR;
        protected int MAX_HISTORY_ENTRIES;

        private const double RAIN_COUNT_PER_TIP = 0.3;

        protected ILogger log;
        protected double pressureOffset;
        protected int readPeriod;
        protected StationSettings _settings;
        protected WeatherDataModel currentData;
        protected Task _backgroundTask;
        protected CancellationTokenSource _cts;
        protected DeviceDataReader dataReader;

        public FineOffset()
        {
            currentData = new WeatherDataModel();
            FO_ENTRY_SIZE = 0x10;
            FO_MAX_ADDR = 0xFFF0;
            MAX_HISTORY_ENTRIES = 4080;
            ConfigurationSettings = new StationSettings();

            _cts = new CancellationTokenSource();
        }

        public virtual string Identifier => "FineOffset";
        public virtual string Manufacturer => "Fine Offset";
        public virtual string Model => "Fine Offset Compatible";
        public virtual string Description => "Supports FineOffset compatible stations";

        public virtual IStationSettings ConfigurationSettings { get; }


        public virtual void Initialise(ILogger log, ISettings settings)
        {
            this.log = log;
            this._settings = (StationSettings)settings;
            var device = new FineOffsetDevice(log, _settings.VendorId, _settings.ProductId, _settings.IsOSX);
            device.OpenDevice();
            dataReader = new DeviceDataReader(log, device, false);
            pressureOffset = dataReader.GetPressureOffset();
            readPeriod = dataReader.GetReadPeriod();
        }


        public WeatherDataModel GetCurrentData()
        {
            return currentData ?? new WeatherDataModel();
        }

        public IEnumerable<WeatherDataModel> GetHistoryData(DateTime fromTimestamp)
        {
            var data = GetHistoricalData(fromTimestamp);
            var models = ProcessDataEntries(data);
            return models;
        }


        public void Start(IWeatherDataStatistics data)
        {
            log.Info("Starting station background task");
            this._backgroundTask = Task.Factory.StartNew(() =>
                PollForNewData(_cts.Token, data)
            , _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        public void Stop()
        {
            log.Info("Stopping station background task");
            if (_cts != null)
                _cts.Cancel();
            log.Info("Waiting for background task to complete");
            _backgroundTask.Wait();
        }


        /// <summary>
        /// Read current data and process it
        /// </summary>
        private async Task PollForNewData(CancellationToken ct, IWeatherDataStatistics weatherDataStatistics)
        {
            int rainCounter = 0;
            while (!ct.IsCancellationRequested)
            {
                var position = dataReader.GetCurrentDataPosition();
                var data = dataReader.GetDataEntry(position, DateTime.UtcNow, pressureOffset);

                var newData = ProcessDataEntry(data, rainCounter);
                currentData = newData;
                weatherDataStatistics.Add(newData);
                rainCounter = data.RainCounter;
                await Task.Delay(3000);
            }


        }

        /// <summary>
        /// Gets all history items
        /// </summary>
        /// <param name="fromTimestamp"></param>
        /// <returns></returns>
        private List<DataEntry> GetHistoricalData(DateTime fromTimestamp)
        {
            var histDataList = new List<DataEntry>();
            DateTime currentTimestamp = DateTime.UtcNow;
            ushort startPosition = dataReader.GetCurrentDataPosition();
            var data = dataReader.GetDataEntry(startPosition, currentTimestamp, pressureOffset);
            histDataList.Add(data);

            ushort currentPosition;
            while ((currentPosition = dataReader.GetNextDataPosition(startPosition)) != startPosition)
            {
                if ((data.Interval != 255) &&
                    (currentTimestamp > fromTimestamp) &&
                    (histDataList.Count < MAX_HISTORY_ENTRIES - 2))
                {
                    currentTimestamp = currentTimestamp.AddMinutes(-data.Interval);
                    data = dataReader.GetDataEntry(currentPosition, currentTimestamp, pressureOffset);
                    histDataList.Add(data);
                }
                else
                    break;
            }
            return histDataList;
        }

        private List<WeatherDataModel> ProcessDataEntries(List<DataEntry> datalist)
        {
            int totalentries = datalist.Count;
            log.Info("Processing history data, number of entries = " + totalentries);
            var items = new List<WeatherDataModel>();
            int prevraintotal = 0;
            while (datalist.Count > 0)
            {
                DataEntry historydata = datalist[datalist.Count - 1];
                prevraintotal = historydata.RainCounter;
                WeatherDataModel model = ProcessDataEntry(historydata, prevraintotal);
                items.Add(model);
                datalist.RemoveAt(datalist.Count - 1);
            }

            log.Info("End processing history data");
            return items;
        }

        private WeatherDataModel ProcessDataEntry(DataEntry dataEntry, int prevraintotal)
        {
            WeatherDataModel model = new WeatherDataModel();
            DateTime timestamp = dataEntry.Timestamp;
            log.Info("Processing data for " + timestamp);

            // Indoor Humidity ======================================================
            if ((dataEntry.InsideHumidity > 100) && (dataEntry.InsideHumidity != 255))
                log.Warn("Ignoring bad data: InsideHumidity = " + dataEntry.InsideHumidity);
            else if ((dataEntry.InsideHumidity > 0) && (dataEntry.InsideHumidity != 255)) // 255 is the overflow value, when RH gets below 10% - ignore
                model.IndoorHumidity = Ratio.FromPercent(dataEntry.InsideHumidity);


            // Indoor Temperature ===================================================
            if ((dataEntry.InsideTemperature > -50) && (dataEntry.InsideTemperature < 50))
                model.IndoorTemperature = Temperature.FromDegreesCelsius(dataEntry.InsideTemperature);
            else
                log.Warn($"Ignoring bad data: InsideTemp = {dataEntry.InsideTemperature}");

            // Pressure =============================================================
            if ((dataEntry.Pressure < _settings.MinPressureThreshold) || (dataEntry.Pressure > _settings.MaxPressureThreshold))
            {
                log.Warn("Ignoring bad data: pressure = " + dataEntry.Pressure);
                log.Warn("                   offset = " + pressureOffset);
            }
            else
            {
                model.Pressure = Pressure.FromMillibars(dataEntry.Pressure);
            }

            if (dataEntry.SensorContactLost)
            {
                log.Error("Sensor contact lost; ignoring outdoor data");
            }
            else
            {
                // Outdoor Humidity =====================================================
                if ((dataEntry.OutsideHumidity > 100) && (dataEntry.OutsideHumidity != 255))
                    log.Warn("Ignoring bad data: outhum = " + dataEntry.OutsideHumidity);
                else if ((dataEntry.OutsideHumidity > 0) && (dataEntry.OutsideHumidity != 255)) // 255 is the overflow value, when RH gets below 10% - ignore
                    model.OutdoorHumidity = Ratio.FromPercent(dataEntry.OutsideHumidity);


                // Wind =================================================================
                if ((dataEntry.WindGust > 60) || (dataEntry.WindGust < 0))
                    log.Warn("Ignoring bad data: gust = " + dataEntry.WindGust);
                else if ((dataEntry.WindSpeed > 60) || (dataEntry.WindSpeed < 0))
                    log.Warn("Ignoring bad data: speed = " + dataEntry.WindSpeed);
                else
                {
                    model.WindBearing = Angle.FromDegrees(dataEntry.WindBearing);
                    model.WindSpeed = Speed.FromKilometersPerHour(dataEntry.WindSpeed);
                    model.WindGust = Speed.FromKilometersPerHour(dataEntry.WindGust);
                }


                // Outdoor Temperature ==================================================
                if ((dataEntry.OutsideTemperature < -50) || (dataEntry.OutsideTemperature > 70))
                    log.Warn("Ignoring bad data: outtemp = " + dataEntry.OutsideTemperature);
                else
                    model.OutdoorTemperature = Temperature.FromDegreesCelsius(dataEntry.OutsideTemperature);



                int raindiff;
                if (prevraintotal == -1)
                {
                    raindiff = 0;
                }
                else
                {
                    raindiff = dataEntry.RainCounter - prevraintotal;
                }


                double rainrate = 0;
                if (raindiff > 100)
                {
                    log.Warn("Warning: large increase in rain gauge tip count: " + raindiff);
                    rainrate = 0;
                }
                else if (dataEntry.Interval > 0)
                    rainrate = (raindiff * RAIN_COUNT_PER_TIP) * (60.0 / dataEntry.Interval);


                model.RainRate = Speed.FromMillimetersPerHour(rainrate);
                model.RainCounter = Length.FromMillimeters(dataEntry.RainCounter * RAIN_COUNT_PER_TIP);

                model.SolarRadiation = Irradiance.FromWattsPerSquareMeter(dataEntry.SolarRadiation);
                model.UvIndex = dataEntry.UVIndex;

            }
            return model;
        }



    }
}
