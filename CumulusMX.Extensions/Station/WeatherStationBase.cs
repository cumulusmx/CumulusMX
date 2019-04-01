using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.Extensions.Station
{
    public abstract class WeatherStationBase : IWeatherStation
    {
        public WeatherStationBase()
        {
        }


        public abstract string Identifier { get; }
        public abstract void Initialise();
        public abstract string Manufacturer { get; }
        public abstract string Model { get; }
        public abstract string Description { get; }
        public abstract IStationSettings ConfigurationSettings { get; }
        public abstract bool Enabled { get; }
        public abstract WeatherDataModel GetCurrentData();
        public abstract IEnumerable<WeatherDataModel> GetHistoryData(DateTime fromTimestamp);
        public abstract void Start();
        public abstract void Stop();

        protected Dictionary<string, ICalibration> GetCalibrationDictionary(IStationSettings settings)
        {
            // TODO: Build dictionary of calibrations from settings
            // Should this be in the Common library?
            return new Dictionary<string, ICalibration>();
        }
    }
}
