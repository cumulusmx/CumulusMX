using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;

namespace FineOffset
{
    public class StationSettings : IStationSettings
    {
        public StationSettings()
        {
        }

        [ExtensionSetting("VendorId", "The USB device Vendor Id", "", 123)]
        public int VendorId { get; set; }
        [ExtensionSetting("ProductId", "The USB device Product Id", "", 123)]
        public int ProductId { get; set; }
        [ExtensionSetting("ProductId", "The USB device Product Id", "", 123)]
        public int FineOffsetReadTime { get; set; }
        public bool IsOSX { get; set; }
        [ExtensionSetting("MinPressureThreshold", "Minimum pressure", "", 950)]
        public double MinPressureThreshold { get; set; }
        [ExtensionSetting("MaxPressureThreshold", "Minimum pressure", "", 1050)]
        public double MaxPressureThreshold { get; set; }
        public bool SyncFOReads { get; internal set; }
    }
}
