using System;
using System.Collections.Generic;
using System.Text;

namespace FineOffset
{
    public class DataEntry
    {
        public DateTime Timestamp { get; set; }
        public int Interval { get; set; }
        public int InsideHumidity { get; set; }
        public double InsideTemperature { get; set; }
        public int OutsideHumidity { get; set; }
        public double OutsideTemperature { get; set; }
        public double Pressure { get; set; }
        public int RainCounter { get; set; }
        public int WindBearing { get; set; }
        public double WindGust { get; set; }
        public double WindSpeed { get; set; }
        public int UVValue { get; set; }
        public double SolarValue { get; set; }
        public bool SensorContactLost { get; set; }
    }
}
