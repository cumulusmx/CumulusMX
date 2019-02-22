using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace FtpDataReporter
{
    public class FtpDataReporter : IDataReporter
    {
        private ILogger _logger;
        public string ServiceName => "FTP Service";

        public string UserId => "TBC"; //TODO

        public IDataReporterSettings Settings { get; private set; }

        public string Identifier => "TBC"; //TODO

        public FtpDataReporter()
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
