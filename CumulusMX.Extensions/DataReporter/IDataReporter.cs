using CumulusMX.Extensions.Station;

namespace CumulusMX.Extensions.DataReporter
{
    public interface IDataReporter : IExtension
    {
        string ServiceName { get; }

        IDataReporterSettings Settings { get; }

        void DoReport(IWeatherDataStatistics currentData);
    }
}
