using System;

namespace CumulusMX
{
    internal class MeteoLib
    {
        public static double WindChill(double tempC, double windSpeedKph)
        {
            // see American Meteorological Society Journal
            // see http://www.msc.ec.gc.ca/education/windchill/science_equations_e.cfm
            // see http://www.weather.gov/os/windchill/index.shtml

            if ((tempC >= 10.0) || (windSpeedKph <= 4.8))
                return tempC;

            double windPow = Math.Pow(windSpeedKph, 0.16);

            double wc = 13.12 + (0.6215*tempC) - (11.37*windPow) + (0.3965*tempC*windPow);

            if (wc > tempC) return tempC;

            return wc;
        }

        /// <summary>
        /// Calculates Apparent Temperature
        /// See http://www.bom.gov.au/info/thermal_stress/#atapproximation
        /// </summary>
        /// <param name="tempC">Temp in C</param>
        /// <param name="windspeed">Wind speed in m/s</param>
        /// <param name="humidity">Relative humidity</param>
        /// <returns></returns>
        public static double ApparentTemperature(double tempC, double windspeedMS, int humidity)
        {
            double avp = (humidity/100.0)*6.105*Math.Exp(17.27*tempC/(237.7 + tempC));
            return tempC + (0.33*avp) - (0.7*windspeedMS) - 4.0;
        }

        public static double HeatIndex(double tempC, int humidity)
        {
            // see http://www.hpc.ncep.noaa.gov/heat_index/hi_equation.html

            double tempF = CToF(tempC);

            if (tempF < 80)
            {
                return FtoC(tempF);
            }
            else
            {
                double result;
                double tempSqrd = tempF*tempF;

                double humSqrd = humidity*humidity;

                result =
                    FtoC(0 - 42.379 + (2.04901523*tempF) + (10.14333127*humidity) - (0.22475541*tempF*humidity) - (0.00683783*tempSqrd) - (0.05481717*humSqrd) +
                         (0.00122874*tempSqrd*humidity) + (0.00085282*tempF*humSqrd) - (0.00000199*tempSqrd*humSqrd));

              // Rothfusz adjustments
              if ((humidity < 13) && (tempF >= 80) && (tempF <= 112))
              {
                result = result - ((13 - humidity) / 4.0) * Math.Sqrt((17 - Math.Abs(tempF - 95)) / 17.0);
              }
              else if ((humidity > 85) && (tempF >= 80) && (tempF <= 87))
              {
                result = result + ((humidity - 85) / 10.0) * ((87 - tempF) / 5.0);
              }

              return result;
            }
        }

        public static double CalculateWetBulbC(double TempC, double DewPointC, double PressureMB)

        {
            double svpDP = SaturationVaporPressure(DewPointC);

            return (((0.00066*PressureMB)*TempC) + ((4098*svpDP)/(Sqr(DewPointC + 237.7))*DewPointC))/((0.00066*PressureMB) + (4098*svpDP)/(Sqr(DewPointC + 237.7)));
            // WBc =     (((0.00066 * P         ) * Tc   ) + ((4098 * E    ) / (    (Tdc + 237.7          ) ^ 2) * Tdc      )) / ((0.00066 * P         ) + (4098 * E    ) / (   (Tdc + 237.7      ) ^ 2))
        }

        public static double SaturationVaporPressure(double tempC)
        {
            return 6.112*Math.Exp(17.67*tempC/(tempC + 243.5)); // Bolton(1980)
        }

        private static double Sqr(double num)
        {
            return num*num;
        }

        public static double DewPoint(double tempC, double humidity)
        {
            //return tempC + ((0.13*tempC) + 13.6)*Math.Log(humidity/100.0);
            // Davis algorithm
            double lnVapor = Math.Log(ActualVapourPressure(tempC, (int) humidity));
            return ((243.12 * lnVapor) - 440.1) / (19.43 - lnVapor);
        }

        public static double SaturationVapourPressure(double tempC)
        {
            return 6.112*Math.Exp((17.62*tempC)/(243.12 + tempC));
        }

        public static double ActualVapourPressure(double tempC, int humidity)
        {
            return (humidity*SaturationVapourPressure(tempC))/100.0;
        }

        public static double Humidex(double tempC, int humidity)
        {
            return tempC + ((5.0/9.0)*(ActualVapourPressure(tempC, humidity) - 10.0));
        }

        public static double CToF(double tempC)
        {
            return ((tempC*9.0)/5.0) + 32;
        }

        public static double FtoC(double tempF)
        {
            return ((tempF - 32)*5.0)/9.0;
        }
    }
}