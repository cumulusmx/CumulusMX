using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Stations
{
    public class WeatherDataCalibrationSettings
    {
        public double PressureOffset { get; set; }
        public double IndoorTemperatureOffset { get; set; }
        public double OutdoorTemperatureOffset { get; set; }
        public double HumidityOffset { get; set; }
        public double WindBearingOffset { get; set; }
        public double UVOffset { get; set; }
        public double WetBulbOffset { get; set; }


        public double WindSpeedMultiplier { get; set; }
        public double WindGustMultiplier { get; set; }
        public double IndoorTemperatureMultiplier { get; set; }
        public double OutdoorTemperatureMultiplier { get; set; }
        public double HumidityMultiplier { get; set; }
        public double RainMultiplier { get; set; }
        public double UVMultiplier { get; set; }
        public double WetBulbMultiplier { get; set; }


        public double PressureSpikeDiff { get; set; }
        public double OutdoorTemperatureSpikeDiff { get; set; }
        public double IndoorTemperatureSpikeDiff { get; set; }
        public double WindSpeedSpikeDiff { get; set; }
        public double WindGustSpikeDiff { get; set; }


        public double MaxRainRate { get; set; }
        public double MaxHourlyRain { get; set; }
    }
}
