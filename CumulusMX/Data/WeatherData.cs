using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX.Data
{
    public class WeatherData
    {
        public double OutdoorTemperatureRaw { get; set; }
        public double OutdoorTemperature { get; set; }
        public double IndoorTemperatureRaw { get; set; }
        public double IndoorTemperature { get; set; }
        public double ApparentTemperature { get; set; }

        public double OutdoorDewpoint { get; set; }
        public double WindChill { get; set; }

        public int IndoorHumidityRaw { get; set; }
        public int IndoorHumidity { get; set; }
        public int OutdoorHumidityRaw { get; set; }
        public int OutdoorHumidity { get; set; }
        public double PressureRaw { get; set; }
        public double Pressure { get; set; }

        public double WindGustRaw { get; set; }
        public double WindGust { get; set; }
        public double WindSpeedRaw { get; set; }
        public double WindSpeed { get; set; }
        public int WindBearingRaw { get; set; }
        public int WindBearing { get; set; }
        public int WindAvgBearing { get; set; }



        public double RainRateRaw { get; set; }
        public double RainRate { get; set; }

        public double RainToday { get; set; }
        public double RainLastHour { get; set; }
        public double HeatIndex { get; set; }
        public double Humidex { get; set; }
        public double TempTrend { get; set; }
        public double PressTrend { get; set; }
    }
}
