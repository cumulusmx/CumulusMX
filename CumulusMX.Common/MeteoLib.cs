using System;
using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Common
{
    public static class MeteoLib
    {
        public static Temperature CalculateDewpoint(Temperature temperature,Ratio humidity)
        {
            // dewpoint = TempinC + ((0.13 * TempinC) + 13.6) * Ln(humidity / 100);
            var TempInC = temperature.DegreesCelsius;
            var humidityDecimal = humidity.DecimalFractions;
            return new Temperature
                (
                TempInC + ((0.13 * TempInC) + 13.6) * Math.Log(humidityDecimal),
                TemperatureUnit.DegreeCelsius
                );
        }

        public static Pressure AdjustPressureForAltitude(Pressure pressure, Length elevation)
        {
            // from MADIS API by NOAA Forecast Systems Lab, see http://madis.noaa.gov/madis_api.html

            double k1 = 0.190284; // discrepancy with calculated k1 probably because Smithsonian used less precise gas constant and gravity values
            double k2 = 8.4184960528E-5; // (standardLapseRate / standardTempK) * (Power(standardSLP, k1)
            var adjustedPressure = Math.Pow(Math.Pow(pressure.Hectopascals - 0.3, k1) + (k2 * elevation.Meters), 1 / k1);
            return Pressure.FromHectopascals(adjustedPressure);
        }
    }
}
