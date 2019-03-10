using CumulusMX.Extensions.Station;

namespace CumulusMX.Extensions.DataReporter
{
    public interface IDataReporter : IExtension
    {
        string ServiceName { get; }
        string UserId { get; }

        IDataReporterSettings Settings { get; }

        void InitialiseReporter(ILogger logger, IDataReporterSettings settings);
        void Start(IWeatherStation station);
        void DoReport(WeatherDataModel currentData);
        void Stop();
    }
}
