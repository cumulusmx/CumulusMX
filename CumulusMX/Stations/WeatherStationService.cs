using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Stations
{
    public class WeatherStationService
    {
        private readonly IStationSettingsService _settingsService;

        public WeatherStationService(IStationSettingsService settingsService)
        {
            this._settingsService = settingsService;
        }
    }
}
