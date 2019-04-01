using CumulusMX.Extensions.Station;

namespace CumulusMX.Extensions.DataReporter
{
    public interface IDataReporter : IExtension
    {
        string ServiceName { get; }

        IDataReporterSettings Settings { get; }
        bool Enabled { get; }

        void DoReport(IWeatherDataStatistics currentData);

        void Start();
        void Stop();
    }
}
