using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace LogFileDataReporter
{
    public class LogFileDataReporter : IDataReporter
    {
        private ILogger _logger;
        public string ServiceName => "Log File Data Reporter Service";


        public IDataReporterSettings Settings { get; private set; }

        public void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public string Identifier => "TBC"; //TODO

        public LogFileDataReporter()
        {
            //TODO: Implement
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }


        
    }
}
