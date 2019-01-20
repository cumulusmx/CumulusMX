using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Stations.Calibration
{
    public interface IWeatherDataCalibrationService
    {
        WeatherDataModel GetCalibratedData(WeatherDataModel currentData, WeatherDataModel previousData);
    }
}
