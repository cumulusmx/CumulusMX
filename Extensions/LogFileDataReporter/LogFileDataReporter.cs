using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace LogFileDataReporter
{
    public class LogFileDataReporter : DataReporterBase
    {
        public override string ServiceName => "Log File Data Reporter Service";
        
        public IDataReporterSettings Settings { get; private set; }

        public override void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public override string Identifier => "TBC"; //TODO

        public LogFileDataReporter(ILogger logger, DataReporterSettingsGeneric settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            Settings = settings as IDataReporterSettings;
        }

        public override void Initialise()
        {
        }


        
    }
}
