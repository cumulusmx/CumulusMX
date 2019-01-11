using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions;

namespace FineOffset
{
    public class StationSettings
    {
        private ISettingsProvider settingsProvider;

        public StationSettings(ISettingsProvider settingsProvider)
        {
            this.settingsProvider = settingsProvider;


            VendorId = settingsProvider.GetSetting<int>("VendorId");
            ProductId = settingsProvider.GetSetting<int>("ProductId");
            FineOffsetReadTime = settingsProvider.GetSetting<int>("FineOffsetReadTime");
            IsOSX = settingsProvider.GetSetting<bool>("FineOffsetReadTime");
            LastUpdateTime = settingsProvider.GetSetting<DateTime>("LastUpdateTime");
        }

        public int VendorId { get; }
        public int ProductId { get; }
        public int FineOffsetReadTime { get; }
        public bool IsOSX { get; }
        public DateTime LastUpdateTime { get; }
        public double MinPressureThreshold { get; internal set; }
        public double MaxPressureThreshold { get; internal set; }
    }
}
