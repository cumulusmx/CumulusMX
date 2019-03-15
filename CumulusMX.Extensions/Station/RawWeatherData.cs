using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions.Station
{
    public abstract class RawWeatherData
    {
        public Dictionary<string,ICalibration> Calibrations { get; set; }

        public abstract WeatherDataModel GetDataModel();

        protected double ApplyCalibration(string readingName, double readingValue)
        {
            if (!Calibrations.ContainsKey(readingName)) return readingValue;

            return Calibrations[readingName].ApplyCalibration(readingValue);
        }
    }
}
