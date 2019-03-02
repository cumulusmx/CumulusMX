using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions.Station;
using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Data
{
    public class WeatherDataStatistics : IWeatherDataStatistics
    {
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
        public IStatistic<double> UvIndex { get; set; }
        public Dictionary<string, IStatistic<IQuantity>> Extra { get; set; } = new Dictionary<string, IStatistic<IQuantity>>();
    }
}
