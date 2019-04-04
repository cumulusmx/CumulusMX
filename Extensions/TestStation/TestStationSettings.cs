using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using UnitsNet;
using UnitsNet.Units;

namespace TestStation
{
    public class TestStationSettings : IStationSettings
    {
        private readonly IConfigurationProvider _baseConfiguration;

        public TestStationSettings(IConfigurationProvider baseConfiguration)
        {
            _baseConfiguration = baseConfiguration;
            SettingsFactory.PopulateProperties(this, _baseConfiguration.GetSection("TestStation"));
        }

        [ExtensionSetting("Is the station enabled", "General", true)]
        public bool? Enabled { get; set; }
    }
}
