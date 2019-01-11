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

        IEnumerable<ExtensionSetting> ConfigurationSettings { get; }

        void Initialise(ILogger logger, ISettingsProvider settingsProvider);
        void Start();
        void Stop();
        WeatherDataModel GetCurrentData();

        void ReadPendingData();
    }
}
