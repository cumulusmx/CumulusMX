using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using UnitsNet;

namespace DavisStation
{
    public class StationSettings : IStationSettings
    {
        //private readonly ICumulusConfiguration _baseConfiguration;

        //public StationSettings(ICumulusConfiguration baseConfiguration)
        //{
        //    _baseConfiguration = baseConfiguration;
        //}

        [ExtensionSetting("The USB device Vendor Id", "", 123)]
        public int VendorId { get; set; }
        [ExtensionSetting("The USB device Product Id", "", 123)]
        public int ProductId { get; set; }
        [ExtensionSetting("The USB device Product Id", "", 123)]
        public int ReadTime { get; set; }

        public bool UseSerialInterface { get; set; }
        public bool UseLoop2 { get; set; }
        public bool ReadReceptionStats { get; set; }
        public bool SyncTime { get; set; }
        public string ComPort { get; set; }
        public int DisconnectInterval { get; set; }
        public int ResponseTime { get; set; }
        public int InitWaitTime { get; set; }
        public bool ForceVPBarUpdate { get; set; }
        public Length Altitude { get; set; }
        public int ClockSettingHour { get; set; }
        public bool? Enabled { get; set; }
    }
}
