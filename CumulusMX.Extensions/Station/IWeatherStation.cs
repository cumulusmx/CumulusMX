using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX.Extensions.Station
{
    public interface IWeatherStation : IExtension
    {
        string Manufacturer { get; }
        string Model { get; }
        string Description { get; }

        IStationSettings ConfigurationSettings { get; }

        void Initialise(ILogger logger, IStationSettings settings);
        void Start();
        void Stop();
        WeatherDataModel GetCurrentData();
        IEnumerable<WeatherDataModel> GetHistoryData(DateTime fromTimestamp);

    }
}
