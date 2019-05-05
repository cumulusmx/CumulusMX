using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CumulusMX.Data.Statistics;
using UnitsNet;

namespace CumulusMX.Extensions.Station
{
    public interface IWeatherDataStatistics
    {
        IDayBooleanStatistic HeatingDegreeDays { get; }
        IDayBooleanStatistic CoolingDegreeDays { get; }
        IDayBooleanStatistic DryDays { get; }
        IDayBooleanStatistic RainDays { get; }
        
        DateTime Time { get; }
        DateTime Yesterday { get; }
        DateTime FirstRecord { get; }
        TimeSpan SinceFirstRecord { get; }

        void Add(WeatherDataModel data);

        void GetReadLock();

        void ReleaseReadLock();
        
        void Save();
        IStatistic this[string key] { get; }

        bool DefineStatistic(string statisticName, Type statisticType);
    }
}
