using System.Collections.Generic;
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
        }

        private string _configurationSectionName;
        public string ConfigurationSectionName
        {
            get => _configurationSectionName;
            set
            {
                _configurationSectionName = value;
                SettingsFactory.PopulateProperties(this, _baseConfiguration.GetSection(_configurationSectionName));
            }
        }

        [ExtensionSetting("Is the station enabled", "General", true)]
        public bool? Enabled { get; set; }

        [ExtensionSetting("Mappings for station outputs.", "General", null)]
        public Dictionary<string, string> Mappings { get; set; }
    }
}
