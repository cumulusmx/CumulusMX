using CumulusMX.Data;

namespace CumulusMX.DataReporter
{
    public interface IDataReporter
    {
        bool InitialiseReporter(IDataReporterSettings settings);
        bool StartScheduledReporting(WeatherData dataObject);
        bool DoReport(WeatherData currentData);
        bool StopScheduledReporting();
        bool ShutdownReporter();
    }
}
