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
        private MeanRevertingRandomWalk _pressure;
        private MeanRevertingRandomWalk _rainRate;

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

            var tempMean = new Func<DateTime, double>((x) => 15 + 10 * Math.Cos(x.DayOfYear / 365.0 * 2 * Math.PI) - 10 * Math.Cos(x.TimeOfDay.TotalSeconds/(24*60*60)*2*Math.PI));
            _outsideTemp = new MeanRevertingRandomWalk(tempMean, (x) => 0.1, 0.01, -10, 50);
            _outsideHumidity = new MeanRevertingRandomWalk((x) => 50, (x) => 1, 0.01, 0, 100);
            _outsideWindSpeed = new MeanRevertingRandomWalk((x) => 25, (x) => 1, 0.01, 0, 50);
            _outsideWindDirection = new MeanRevertingRandomWalk((x) => 180000, (x) => 1000, 0.01, 0, 360000);
            _pressure = new MeanRevertingRandomWalk((x)=>1000,(x)=>.1,0.01,900,1100);
            _rainRate = new MeanRevertingRandomWalk((x)=>-10,(x)=>1,0.1,0,30);
            _insideTemp = new MeanRevertingRandomWalk((x) => 20, (x) => 0.1, 0.1, 15, 25);
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
                    _currentData.RainRate = Speed.FromMillimetersPerHour(_rainRate.GetValue(readTime));
                    _currentData.Pressure = Pressure.FromMillibars(_pressure.GetValue(readTime));
                    weatherStatistics.Add(_currentData);

                    _log.Debug($"Generated weather data - Temp:{_currentData.OutdoorTemperature.Value.DegreesCelsius:#.0} Press:{_currentData.Pressure.Value.Millibars:#.0} Wind:{_currentData.WindSpeed.Value.MetersPerSecond:#.0}");
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
