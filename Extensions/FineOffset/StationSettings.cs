using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Common;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;

namespace FineOffset
{
    public class StationSettings : IStationSettings
    {
        private IConfigurationProvider _baseConfiguration;

        public StationSettings(IConfigurationProvider baseConfiguration)
        {
            _baseConfiguration = baseConfiguration;
            SettingsFactory.PopulateProperties(this,_baseConfiguration.GetSection("FineOffsetStation"));
        }

        [ExtensionSetting("The USB device Vendor Id", "", 123)]
        public int VendorId { get; set; }
        [ExtensionSetting("The USB device Product Id", "", 123)]
        public int ProductId { get; set; }
        [ExtensionSetting("The USB device Product Id", "", 123)]
        public int FineOffsetReadTime { get; set; }
        public bool IsOSX { get; set; }
        [ExtensionSetting("Minimum pressure", "", 950)]
        public double MinPressureThreshold { get; set; }
        [ExtensionSetting("Maximum pressure", "", 1050)]
        public double MaxPressureThreshold { get; set; }
        public bool SyncFOReads { get; internal set; }
        public bool IsEnabled { get; set; }
    }
}
