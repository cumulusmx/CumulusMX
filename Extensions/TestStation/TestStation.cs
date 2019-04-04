using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using UnitsNet;

namespace TestStation
{
    public class TestStation : WeatherStationBase
    {
        private ILogger _log;
        private readonly IWeatherDataStatistics _weatherStatistics;
        private WeatherDataModel _currentData = new WeatherDataModel();
        private object _dataUpdateLock = new object();

        public override string Identifier => "TestStation";
        public override string Manufacturer => "Test";
        public override string Model => "Test Data";
        public override string Description => "Provides dummy data to test functionality.";
        private TestStationSettings _configurationSettings;
        private bool _enabled;
        public override IStationSettings ConfigurationSettings => _configurationSettings;
        public override bool Enabled => _enabled;
        
        protected Task _backgroundTask;
        protected CancellationTokenSource _cts;
        private MeanRevertingRandomWalk _outsideTemp;
        private MeanRevertingRandomWalk _outsideHumidity;
        private MeanRevertingRandomWalk _outsideWindSpeed;
        private MeanRevertingRandomWalk _outsideWindDirection;
        private MeanRevertingRandomWalk _insideTemp;

        public TestStation(ILogger logger, TestStationSettings settings, IWeatherDataStatistics weatherStatistics)
        {
            _configurationSettings = settings;
            if (_configurationSettings == null)
            {
                throw new ArgumentException($"Invalid configuration settings passed to Test Station.");
            }

            _enabled = settings.Enabled ?? false;
            _log = logger;
            _weatherStatistics = weatherStatistics;
            _cts = new CancellationTokenSource();
        }

        public override void Initialise()
        {
            _log.Info("Station type = Test Data");

            _outsideTemp = new MeanRevertingRandomWalk((x) => 20, (x) => 1, .01, -10, 50);
            _outsideHumidity = new MeanRevertingRandomWalk((x) => 20, (x) => 1, .01, -10, 50);
            _outsideWindSpeed = new MeanRevertingRandomWalk((x) => 20, (x) => 1, .01, -10, 50);
            _outsideWindDirection = new MeanRevertingRandomWalk((x) => 20, (x) => 1, .01, -10, 50);
            _insideTemp = new MeanRevertingRandomWalk((x) => 20, (x) => 1, .01, -10, 50);
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
            // Nothing to see here
            return new List<WeatherDataModel>();
        }


        public override void Start()
        {
            _log.Info("Starting station background task");
            _backgroundTask = Task.Factory.StartNew(() =>
                    PollForNewData(_cts.Token, _weatherStatistics)
                , _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private async Task PollForNewData(CancellationToken ct, IWeatherDataStatistics weatherStatistics)
        {
            _log.Info("Start normal generation loop");
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var readTime = DateTime.Now;
                    _currentData.Timestamp = readTime;
                    _currentData.OutdoorTemperature = Temperature.FromDegreesCelsius(_outsideTemp.GetValue(readTime));
                    _currentData.IndoorTemperature = Temperature.FromDegreesCelsius(_insideTemp.GetValue(readTime));
                    _currentData.OutdoorHumidity = Ratio.FromPercent(_outsideHumidity.GetValue(readTime));
                    _currentData.WindSpeed = Speed.FromKilometersPerHour(_outsideWindSpeed.GetValue(readTime));
                    _currentData.WindBearing = Angle.FromMillidegrees(_outsideWindDirection.GetValue(readTime));

                    weatherStatistics.Add(_currentData);

                    await Task.Delay(10000,ct);
                }
            }
            // Catch the ThreadAbortException
            catch (ThreadAbortException)
            {
            }
        }


        public override void Stop()
        {
            _log.Info("Stopping data generation task");
            if (_cts != null)
                _cts.Cancel();
            _log.Info("Waiting for data generation to complete");
            _backgroundTask.Wait();
        }       
    }
}
