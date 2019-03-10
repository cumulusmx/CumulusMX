using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Data;
using CumulusMX.Extensions.Station;
using UnitsNet;

namespace CumulusMX.Stations.Calibration
{
    public class WeatherDataCalibrationService : IWeatherDataCalibrationService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly WeatherDataCalibrationSettings _settings;

        public WeatherDataCalibrationService(WeatherDataCalibrationSettings settings)
        {
            this._settings = settings;
        }

        public WeatherDataModel GetCalibratedData(WeatherDataModel currentData, WeatherDataModel previousData)
        {
            var model = new WeatherDataModel();
            model.Pressure = CalibratePressure(currentData.Pressure, previousData.Pressure);
            model.AltimeterPressure = CalibratePressure(currentData.AltimeterPressure, previousData.AltimeterPressure);

            model.IndoorHumidity = CalibrateIndoorHumidity(currentData.IndoorHumidity, previousData.IndoorHumidity);
            model.OutdoorHumidity = CalibrateOutdoorHumidity(currentData.OutdoorHumidity, previousData.OutdoorHumidity);

            model.IndoorTemperature = CalibrateIndoorTemperature(currentData.IndoorTemperature, previousData.IndoorTemperature);
            model.OutdoorTemperature = CalibrateOutdoorTemperature(currentData.OutdoorTemperature, previousData.OutdoorTemperature);

            model.OutdoorDewpoint = CalibrateOutdoorDewpoint(currentData.OutdoorDewpoint, previousData.OutdoorDewpoint);
            model.RainCounter = CalibrateRainCounter(currentData.RainCounter, previousData.RainCounter);

            model.RainRate = CalibrateRainRate(currentData.RainRate, previousData.RainRate);
            model.SolarRadiation = CalibrateSolarRadiation(currentData.SolarRadiation, previousData.SolarRadiation);
            model.UvIndex = CalibrateUVIndex(currentData.UvIndex, previousData.UvIndex);

            model.WindBearing = CalibrateWindBearing(currentData.WindBearing, previousData.WindBearing);
            model.WindGust = CalibrateWindGust(currentData.WindGust, previousData.WindGust);
            model.WindSpeed = CalibrateWindSpeed(currentData.WindSpeed, previousData.WindSpeed);



            throw new NotImplementedException();
        }

        private Speed? CalibrateWindSpeed(Speed? windSpeed1, Speed? windSpeed2)
        {
            throw new NotImplementedException();
        }

        private Speed? CalibrateWindGust(Speed? windGust1, Speed? windGust2)
        {
            throw new NotImplementedException();
        }

        private Angle? CalibrateWindBearing(Angle? windBearing1, Angle? windBearing2)
        {
            throw new NotImplementedException();
        }

        private int? CalibrateUVIndex(int? uVIndex1, int? uVIndex2)
        {
            throw new NotImplementedException();
        }

        private Irradiance? CalibrateSolarRadiation(Irradiance? solarRadiation1, Irradiance? solarRadiation2)
        {
            throw new NotImplementedException();
        }

        private Speed? CalibrateRainRate(Speed? rainRate1, Speed? rainRate2)
        {
            throw new NotImplementedException();
        }

        private Length? CalibrateRainCounter(Length? rainCounter1, Length? rainCounter2)
        {
            throw new NotImplementedException();
        }

        private Temperature? CalibrateOutdoorDewpoint(Temperature? outdoorDewpoint1, Temperature? outdoorDewpoint2)
        {
            throw new NotImplementedException();
        }

        private Temperature? CalibrateOutdoorTemperature(Temperature? outdoorTemperature1, Temperature? outdoorTemperature2)
        {
            throw new NotImplementedException();
        }

        private Temperature? CalibrateIndoorTemperature(Temperature? indoorTemperature1, Temperature? indoorTemperature2)
        {
            throw new NotImplementedException();
        }

        private Ratio? CalibrateOutdoorHumidity(Ratio? outdoorHumidity1, Ratio? outdoorHumidity2)
        {
            throw new NotImplementedException();
        }

        private Ratio? CalibrateIndoorHumidity(Ratio? indoorHumidity1, Ratio? indoorHumidity2)
        {
            throw new NotImplementedException();
        }

        private Pressure? CalibratePressure(Pressure? rawPressure, Pressure? previousRawPressure)
        {
            if (rawPressure == null)
                return null;

            double pressure = (double)rawPressure?.Millibars;
            if (_settings.PressureOffset != null)
                pressure += (double)_settings.PressureOffset?.Millibars;

            pressure *= _settings.PressureMultiplier;


            return Pressure.FromMillibars(pressure);
        }

       
    }
}
