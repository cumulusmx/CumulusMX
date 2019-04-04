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
        IStatistic<Temperature> IndoorTemperature { get; set; }
        IStatistic<Temperature> OutdoorTemperature { get; set; }
        IStatistic<Temperature> ApparentTemperature { get; set; }
        IStatistic<Temperature> WindChill { get; set; }
        IStatistic<Temperature> HeatIndex { get; set; }
        IStatistic<double> Humidex { get; set; }
        IStatistic<Ratio> IndoorHumidity { get; set; }
        IStatistic<Ratio> OutdoorHumidity { get; set; }
        IStatistic<Speed> WindGust { get; set; }
        IStatistic<Speed> WindSpeed { get; set; }
        IStatistic<Angle> WindBearing { get; set; }
        IStatistic<Pressure> Pressure { get; set; }
        IStatistic<Pressure> AltimeterPressure { get; set; }
        IStatistic<Temperature> OutdoorDewpoint { get; set; }
        IStatistic<Speed> RainRate { get; set; }
        IStatistic<Length> Rain { get; set; }
        IStatistic<Irradiance> SolarRadiation { get; set; }
        IStatistic<double> UvIndex { get; set; }
        Dictionary<string,IStatistic<IQuantity>> Extra { get; set; }

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
    }
}
