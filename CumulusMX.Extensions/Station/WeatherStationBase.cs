using System;
using System.Collections.Generic;
using System.Text;
using UnitsNet;

namespace CumulusMX.Extensions.Station
{
    public abstract class WeatherStationBase : IWeatherStation
    {
        protected Dictionary<string, string> _mapping;
        protected ILogger _log;
        protected IWeatherDataStatistics _weatherStatistics;

        public Dictionary<string, Type> _allOutputs;

        public WeatherStationBase(IWeatherDataStatistics weatherStatistics)
        {
            _mapping = new Dictionary<string, string>();
            _weatherStatistics = weatherStatistics;
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

        protected Dictionary<string, string> RegisterOutputs(Dictionary<string, string> baseMap)
        {
            var result = new Dictionary<string,string>();
            foreach (var output in _allOutputs)
            {
                var targetName = (baseMap.ContainsKey(output.Key)) ? baseMap[output.Key] : output.Key;
                if (_weatherStatistics.DefineStatistic(targetName,output.Value))
                    result.Add(output.Key,targetName);
            }

            return result;
        }
    }
}
