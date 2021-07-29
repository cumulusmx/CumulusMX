using System;
using System.Collections.Generic;
using System.Text;
using UnitsNet;

namespace CumulusMX.Stations.Calibration
{
    public class PressureCalibrator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly WeatherDataCalibrationSettings _settings;

        public PressureCalibrator(WeatherDataCalibrationSettings settings)
        {
            this._settings = settings;
        }

        public Pressure? ApplyPressureOffset(Pressure? rawPressure)
        {
            if (rawPressure == null)
                return null;

            double pressure = (double)rawPressure?.Millibars;
            if (_settings.PressureOffset != null)
                pressure += (double)_settings.PressureOffset?.Millibars;

            return Pressure.FromMillibars(pressure);
        }


        public Pressure? ApplyPressureMultiplier(Pressure? rawPressure)
        {
            if (rawPressure == null)
                return null;

            double pressure = (double)rawPressure?.Millibars;
            pressure *= _settings.PressureMultiplier;

            return Pressure.FromMillibars(pressure);
        }


        private Pressure? RemovePressureSpike(Pressure? rawPressure, Pressure? previousRawPressure)
        {
            if (rawPressure == null)
                return null;
            if (previousRawPressure == null)
                return rawPressure;

            double pressure = (double)rawPressure?.Millibars;
            double previousPressure = (double)previousRawPressure?.Millibars;
            if (_settings.PressureSpikeDiff < Math.Abs(pressure - previousPressure))
                return previousRawPressure;   // On a data spike, do we return previous pressure, or null? Cumulus MX uses previous value.

            pressure *= _settings.PressureMultiplier;

            pressure += (double)_settings.PressureOffset?.Millibars;

            return Pressure.FromMillibars(pressure);
        }
    }
}
