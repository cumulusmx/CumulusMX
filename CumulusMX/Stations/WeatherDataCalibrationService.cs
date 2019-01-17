using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Data;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Stations
{
    public class WeatherDataCalibrationService : IWeatherDataCalibrationService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly WeatherDataCalibrationSettings _settings;

        public WeatherDataCalibrationService(WeatherDataCalibrationSettings settings)
        {
            this._settings = settings;
        }

        public WeatherDataModel GetCalibratedData(WeatherDataModel currentData, WeatherDataModel previousData)
        {
            throw new NotImplementedException();
        }


        private double? CalibratePressure(double? rawPressure, double? previousRawPressure = null)
        {
            if (rawPressure == null)
            {
                if (previousRawPressure == null)
                    return null;
                else return previousRawPressure;
            }
            double? pressure = rawPressure + _settings.PressureOffset;
            return pressure;
        }

        private double? CalibrateIndoorTemperature(double? rawTemp, double? previousRawTemp = null)
        {
            if (rawTemp == null)
            {
                if (previousRawTemp == null)
                    return null;
                else return previousRawTemp;
            }
            double? pressure = rawTemp + _settings.PressureOffset;
            return pressure;
        }
    }
}
