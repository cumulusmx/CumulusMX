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

        /// <summary>
        /// Calculates Apparent Temperature
        /// See http://www.bom.gov.au/info/thermal_stress/#atapproximation
        /// </summary>
        /// <param name="tempC">Temp in C</param>
        /// <param name="windspeed">Wind speed in m/s</param>
        /// <param name="humidity">Relative humidity</param>
        /// <returns></returns>
        public static Temperature ApparentTemperature(Temperature temp, Speed windspeed, Ratio humidity)
        {
            double avp = humidity.DecimalFractions * 6.105 * Math.Exp(17.27 * temp.DegreesCelsius / (237.7 + temp.DegreesCelsius));
            var result = temp.DegreesCelsius + (0.33 * avp) - (0.7 * windspeed.MetersPerSecond) - 4.0;
            return Temperature.FromDegreesCelsius(result);
        }

        public static Temperature HeatIndex(Temperature temp, Ratio humidity)
        {
            // see http://www.hpc.ncep.noaa.gov/heat_index/hi_equation.html

            if (temp.DegreesFahrenheit < 80)
            {
                return temp;
            }

            double tempSqrd = temp.DegreesFahrenheit * temp.DegreesFahrenheit;

            double humSqrd = humidity.Percent * humidity.Percent;

            var result = (0 - 42.379 + (2.04901523 * temp.DegreesFahrenheit) + (10.14333127 * humidity.Percent) - (0.22475541 * temp.DegreesFahrenheit * humidity.Percent) - (0.00683783 * tempSqrd) - (0.05481717 * humSqrd) +
                             (0.00122874 * tempSqrd * humidity.Percent) + (0.00085282 * temp.DegreesFahrenheit * humSqrd) - (0.00000199 * tempSqrd * humSqrd));

            // Rothfusz adjustments
            if ((humidity.Percent < 13) && (temp.DegreesFahrenheit >= 80) && (temp.DegreesFahrenheit <= 112))
            {
                result = result - ((13 - humidity.Percent) / 4.0) * Math.Sqrt((17 - Math.Abs(temp.DegreesFahrenheit - 95)) / 17.0);
            }
            else if ((humidity.Percent > 85) && (temp.DegreesFahrenheit >= 80) && (temp.DegreesFahrenheit <= 87))
            {
                result = result + ((humidity.Percent - 85) / 10.0) * ((87 - temp.DegreesFahrenheit) / 5.0);
            }

            return Temperature.FromDegreesCelsius(result);
        }

        public static Temperature WindChill(Temperature temp, Speed windSpeed)
        {
            // see American Meteorological Society Journal
            // see http://www.msc.ec.gc.ca/education/windchill/science_equations_e.cfm
            // see http://www.weather.gov/os/windchill/index.shtml

            if ((temp.DegreesCelsius >= 10.0) || (windSpeed.KilometersPerHour <= 4.8))
                return temp;

            double windPow = Math.Pow(windSpeed.KilometersPerHour, 0.16);

            double wc = 13.12 + (0.6215 * temp.DegreesCelsius) - (11.37 * windPow) + (0.3965 * temp.DegreesCelsius * windPow);

            return wc > temp.DegreesCelsius ? temp : Temperature.FromDegreesCelsius(wc);
        }

        public static double Humidex(Temperature temp, Ratio humidity)
        {
            return temp.DegreesCelsius + ((5.0 / 9.0) * (ActualVapourPressure(temp, humidity) - 10.0));
        }

        public static double SaturationVapourPressure(Temperature temp)
        {
            return 6.112 * Math.Exp((17.62 * temp.DegreesCelsius) / (243.12 + temp.DegreesCelsius));
        }

        public static double ActualVapourPressure(Temperature temp, Ratio humidity)
        {
            return (humidity.Percent * SaturationVapourPressure(temp)) / 100.0;
        }

    }
}
