using System;
using System.Collections.Generic;
using System.Text;
using UnitsNet;

namespace CumulusMX.Stations.Calibration
{
    public class WeatherDataCalibrationSettings
    {
        public Pressure? PressureOffset { get; set; }
        public Temperature? IndoorTemperatureOffset { get; set; }
        public Temperature? OutdoorTemperatureOffset { get; set; }
        public Temperature? WetBulbTemperatureOffset { get; set; }
        public Ratio? HumidityOffset { get; set; }
        public Angle? WindBearingOffset { get; set; }
        public Irradiance? SolarRadiationOffset { get; set; }
        public double? UVIndexOffset { get; set; }


        public double PressureMultiplier { get; set; }
        public double IndoorTemperatureMultiplier { get; set; }
        public double OutdoorTemperatureMultiplier { get; set; }
        public double WetBulbTemperatureMultiplier { get; set; }
        public double WindSpeedMultiplier { get; set; }
        public double WindGustMultiplier { get; set; }
        public double HumidityMultiplier { get; set; }
        public double RainMultiplier { get; set; }
        public double UVMultiplier { get; set; }


        public double PressureSpikeDiff { get; set; }
        public double OutdoorTemperatureSpikeDiff { get; set; }
        public double IndoorTemperatureSpikeDiff { get; set; }
        public double WindSpeedSpikeDiff { get; set; }
        public double WindGustSpikeDiff { get; set; }


        public double MaxRainRate { get; set; }
        public double MaxHourlyRain { get; set; }
    }
}
