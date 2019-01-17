using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Stations
{
    public interface IWeatherDataCalibrationService
    {
        WeatherDataModel GetCalibratedData(WeatherDataModel currentData, WeatherDataModel previousData);
    }
}
