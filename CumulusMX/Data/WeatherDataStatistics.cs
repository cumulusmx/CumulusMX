using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using CumulusMX.Extensions.Station;
using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Data
{
    public class WeatherDataStatistics : IWeatherDataStatistics
    {
        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public IStatistic<Temperature> IndoorTemperature { get; set; } = new StatisticUnit<Temperature,TemperatureUnit>();
        public IStatistic<Temperature> OutdoorTemperature { get; set; } = new StatisticUnit<Temperature, TemperatureUnit>();
        public IStatistic<Ratio> IndoorHumidity { get; set; } = new StatisticUnit<Ratio, RatioUnit>();
        public IStatistic<Ratio> OutdoorHumidity { get; set; } = new StatisticUnit<Ratio, RatioUnit>();
        public IStatistic<Speed> WindGust { get; set; } = new StatisticUnit<Speed, SpeedUnit>();
        public IStatistic<Speed> WindSpeed { get; set; } = new StatisticUnit<Speed, SpeedUnit>();
        public IStatistic<Angle> WindBearing { get; set; } = new StatisticUnit<Angle, AngleUnit>();
        public IStatistic<Pressure> Pressure { get; set; } = new StatisticUnit<Pressure, PressureUnit>();
        public IStatistic<Pressure> AltimeterPressure { get; set; } = new StatisticUnit<Pressure, PressureUnit>();
        public IStatistic<Temperature> OutdoorDewpoint { get; set; } = new StatisticUnit<Temperature, TemperatureUnit>();
        public IStatistic<Speed> RainRate { get; set; } = new StatisticUnit<Speed, SpeedUnit>();
        public IStatistic<Length> Rain { get; set; } = new StatisticUnit<Length, LengthUnit>();
        public IStatistic<Irradiance> SolarRadiation { get; set; } = new StatisticUnit<Irradiance, IrradianceUnit>();
        public IStatistic<double> UvIndex { get; set; } = new StatisticDouble();
        public Dictionary<string, IStatistic<IQuantity>> Extra { get; set; } = new Dictionary<string, IStatistic<IQuantity>>();

        public void Add(WeatherDataModel data)
        {
            _lock.EnterWriteLock();
            try
            {
                if (data.IndoorTemperature.HasValue)
                    IndoorTemperature.Add(data.Timestamp, data.IndoorTemperature.Value);
                if (data.OutdoorTemperature.HasValue)
                    OutdoorTemperature.Add(data.Timestamp, data.OutdoorTemperature.Value);
                if (data.IndoorHumidity.HasValue)
                    IndoorHumidity.Add(data.Timestamp, data.IndoorHumidity.Value);
                if (data.OutdoorHumidity.HasValue)
                    OutdoorHumidity.Add(data.Timestamp, data.OutdoorHumidity.Value);
                if (data.WindGust.HasValue)
                    WindGust.Add(data.Timestamp, data.WindGust.Value);
                if (data.WindSpeed.HasValue)
                    WindSpeed.Add(data.Timestamp, data.WindSpeed.Value);
                if (data.WindBearing.HasValue)
                    WindBearing.Add(data.Timestamp, data.WindBearing.Value);
                if (data.Pressure.HasValue)
                    Pressure.Add(data.Timestamp, data.Pressure.Value);
                if (data.AltimeterPressure.HasValue)
                    AltimeterPressure.Add(data.Timestamp, data.AltimeterPressure.Value);
                if (data.OutdoorDewpoint.HasValue)
                    OutdoorDewpoint.Add(data.Timestamp, data.OutdoorDewpoint.Value);
                if (data.RainRate.HasValue)
                    RainRate.Add(data.Timestamp, data.RainRate.Value);
                if (data.RainCounter.HasValue)
                    Rain.Add(data.Timestamp, data.RainCounter.Value);
                if (data.SolarRadiation.HasValue)
                    SolarRadiation.Add(data.Timestamp, data.SolarRadiation.Value);
                if (data.UvIndex.HasValue)
                    UvIndex.Add(data.Timestamp, data.UvIndex.Value);

                foreach (var extraReading in data.Extra)
                {
                    if (Extra.ContainsKey(extraReading.Key))
                        Extra[extraReading.Key].Add(data.Timestamp, extraReading.Value);
                    else
                    {
                        Extra.Add(extraReading.Key, StatisticFactory.Build(extraReading.Value.GetType()));
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
        }

        public void GetReadLock()
        {
            _lock.EnterReadLock();
        }

        public void ReleaseReadLock()
        {
            _lock.ExitReadLock();
        }
    }

}
