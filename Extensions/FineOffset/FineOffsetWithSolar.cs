using System;
using System.Collections.Generic;
using System.Threading;
using CumulusMX.Extensions;
using CumulusMX.Extensions.Station;
using HidSharp;

namespace FineOffset
{
    public class FineOffsetWithSolar : FineOffset, IWeatherStation
    {
        public FineOffsetWithSolar(ILogger log, WithSolarSettings settings, IWeatherDataStatistics data) : base(log, settings, data)
        {
            this._log = log;
            this._settings = (StationSettings)settings;

            FO_ENTRY_SIZE = 0x10;
            FO_MAX_ADDR = 0xFFF0;
            MAX_HISTORY_ENTRIES = 4080;
            ConfigurationSettings = settings;
        }
        public override string Identifier => "FineOffset_Solar";
        public override string Manufacturer => "Fine Offset";
        public override string Model => "Fine Offset+Solar";
        public override string Description =>"Supports FineOffset compatible stations, with Solar sensors";

        public override IStationSettings ConfigurationSettings { get; }

        public override void Initialise()
        {
            var device = new FineOffsetDevice(_log, _settings.VendorId, _settings.ProductId, _settings.IsOSX);
            device.OpenDevice();
            dataReader = new DeviceDataReader(_log, device, false);
            pressureOffset = dataReader.GetPressureOffset();
            readPeriod = dataReader.GetReadPeriod();
        }

    }


    public class WithSolarSettings : StationSettings
    {
        public WithSolarSettings(IConfigurationProvider _config) : base(_config)
        {

        }
    }
}
