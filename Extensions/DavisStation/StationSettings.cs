using System.Collections.Generic;
using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using UnitsNet;
using UnitsNet.Units;

namespace DavisStation
{
    public class StationSettings : IStationSettings
    {
        private readonly IConfigurationProvider _baseConfiguration;

        public StationSettings(IConfigurationProvider baseConfiguration)
        {
            _baseConfiguration = baseConfiguration;
        }

        public StationSettings(IConfigurationProvider baseConfiguration,string configurationSectionName)
        {
            _baseConfiguration = baseConfiguration;
            _configurationSectionName = configurationSectionName;
            SettingsFactory.PopulateProperties(this, _baseConfiguration.GetSection(configurationSectionName));
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

        [ExtensionSetting("The USB device Vendor Id", "", 123)]
        public int VendorId { get; set; }
        [ExtensionSetting("The USB device Product Id", "", 123)]
        public int ProductId { get; set; }
        [ExtensionSetting("The USB device Product Id", "", 123)]
        public int ReadTime { get; set; }

        [ExtensionSetting("Does the station use a serial interface", "SerialInterface", true)]
        public bool UseSerialInterface { get; set; }
        [ExtensionSetting("Should Loop2 packets be used in place of Loop packets", "General", false)]
        public bool UseLoop2 { get; set; }
        [ExtensionSetting("Should details on the reception quality be read", "General", true)]
        public bool ReadReceptionStats { get; set; }
        [ExtensionSetting("Should Cumulus update the stations time", "General", false)]
        public bool SyncTime { get; set; }
        [ExtensionSetting("Which com port should be used for the serial interface", "SerialInterface", "COM1")]
        public string ComPort { get; set; }
        [ExtensionSetting("How long should Cumulus wait before disconnecting and reconnecting the service", "IpInterface", 1000)]
        public int DisconnectInterval { get; set; }
        [ExtensionSetting("What is the response time limit", "Interface", 1000)]
        public int ResponseTime { get; set; }
        [ExtensionSetting("How long should Cumulus wait when initialising the station", "Interface", 5)]
        public int InitWaitTime { get; set; }
        [ExtensionSetting("Should Cumulus force the station to do Bar updates", "Interface", false)]
        public bool ForceVPBarUpdate { get; set; }
        [ExtensionSetting("What is the station's altitude", "Location", 0.0)]
        public double Altitude { get; set; }
        [ExtensionSetting("At what hour of the day should the clock be set", "General", 2)]
        public int ClockSettingHour { get; set; }
        [ExtensionSetting("Is the station enabled", "General", false)]
        public bool? Enabled { get; set; }

        public Dictionary<string, string> Mappings { get; set; }
    }
}
