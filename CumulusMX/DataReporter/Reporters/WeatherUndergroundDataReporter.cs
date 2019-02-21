using System;
using CumulusMX.Data;

namespace CumulusMX.DataReporter.Reporters
{
    public class WeatherUndergroundDataReporter : IDataReporter
    {
        public WeatherUndergroundDataReporter()
        {
            //TODO: Implement
        }

        public bool InitialiseReporter(IDataReporterSettings settings)
        {
            throw new NotImplementedException();
        }

        public bool StartScheduledReporting(WeatherData dataObject)
        {
            throw new NotImplementedException();
        }

        public bool DoReport(WeatherData currentData)
        {
            throw new NotImplementedException();
        }

        public bool StopScheduledReporting()
        {
            throw new NotImplementedException();
        }

        public bool ShutdownReporter()
        {
            throw new NotImplementedException();
        }
    }
}
