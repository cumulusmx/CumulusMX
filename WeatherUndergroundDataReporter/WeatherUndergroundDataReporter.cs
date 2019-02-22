using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace WeatherUndergroundDataReporter
{
    public class WeatherUndergroundDataReporter : IDataReporter
    {
        public string ServiceName => "Weather Underground";

        public string UserId => throw new NotImplementedException();

        public IDataReporterSettings Settings { get; private set; }

        public string Identifier => throw new NotImplementedException();

        private ILogger _logger;

        public WeatherUndergroundDataReporter()
        {
            //TODO: Implement
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void DoReport(WeatherDataModel currentData)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
