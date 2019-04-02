using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CumulusMX.Common;
using CumulusMX.Configuration;
using CumulusMX.Data.Statistics;
using CumulusMX.Data.Statistics.Double;
using CumulusMX.Data.Statistics.Unit;
using CumulusMX.Extensions.Station;
using Newtonsoft.Json;
using UnitsNet;
using UnitsNet.Serialization.JsonNet;
using UnitsNet.Units;

namespace CumulusMX.Data
{
    [JsonObject]
    public class WeatherDataStatistics : IWeatherDataStatistics
    {
        [JsonIgnore]
        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public IStatistic<Temperature> IndoorTemperature { get; set; } = new StatisticUnit<Temperature,TemperatureUnit>();
        public IStatistic<Temperature> OutdoorTemperature { get; set; } = new StatisticUnit<Temperature, TemperatureUnit>();
        public IStatistic<Temperature> ApparentTemperature { get; set; } = new StatisticUnit<Temperature, TemperatureUnit>();
        public IStatistic<Temperature> WindChill { get; set; } = new StatisticUnit<Temperature, TemperatureUnit>();
        public IStatistic<Temperature> HeatIndex { get; set; } = new StatisticUnit<Temperature, TemperatureUnit>();
        public IStatistic<double> Humidex { get; set; } = new StatisticDouble();
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

        public IDayBooleanStatistic HeatingDegreeDays { get; }
        public IDayBooleanStatistic CoolingDegreeDays { get; }
        public IDayBooleanStatistic DryDays { get; }
        public IDayBooleanStatistic RainDays { get; }
        // ? Forecast

        public DateTime Time { get; private set; } = DateTime.MinValue;
        [JsonIgnore]
        public DateTime Yesterday => Time.AddDays(-1);
        public DateTime FirstRecord { get; private set; } = DateTime.MinValue;
        [JsonIgnore]
        public TimeSpan SinceFirstRecord => Time - FirstRecord;

        public WeatherDataStatistics()
        {
            HeatingDegreeDays = new DayBooleanStatistic<Temperature>(OutdoorTemperature, (x) => x.DayAverage < Temperature.FromDegreesFahrenheit(65));
            CoolingDegreeDays = new DayBooleanStatistic<Temperature>(OutdoorTemperature, (x) => x.DayAverage > Temperature.FromDegreesFahrenheit(65));
            OutdoorTemperature.AddBooleanStatistics(HeatingDegreeDays);
            OutdoorTemperature.AddBooleanStatistics(CoolingDegreeDays);

            DryDays = new DayBooleanStatistic<Length>(Rain, (x) => x.DayTotal == Length.Zero);
            RainDays = new DayBooleanStatistic<Length>(Rain, (x) => x.DayTotal != Length.Zero);
            Rain.AddBooleanStatistics(DryDays);
            Rain.AddBooleanStatistics(RainDays);
        }

        public void Add(WeatherDataModel data)
        {
            _lock.EnterWriteLock();

            if (FirstRecord == DateTime.MinValue)
                FirstRecord = data.Timestamp;
            Time = data.Timestamp;

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

                if (data.ApparentTemperature.HasValue)
                    ApparentTemperature.Add(data.Timestamp, data.ApparentTemperature.Value);
                else
                {
                    if (data.OutdoorTemperature.HasValue && data.OutdoorHumidity.HasValue && data.WindSpeed.HasValue)
                        ApparentTemperature.Add(data.Timestamp, MeteoLib.ApparentTemperature(data.OutdoorTemperature.Value,data.WindSpeed.Value,data.OutdoorHumidity.Value));
                }

                if (data.WindChill.HasValue)
                    WindChill.Add(data.Timestamp, data.WindChill.Value);
                else
                {
                    if (data.OutdoorTemperature.HasValue && data.WindSpeed.HasValue)
                        WindChill.Add(data.Timestamp, MeteoLib.WindChill(data.OutdoorTemperature.Value, data.WindSpeed.Value));
                }

                if (data.HeatIndex.HasValue)
                    HeatIndex.Add(data.Timestamp, data.HeatIndex.Value);
                else
                {
                    if (data.OutdoorTemperature.HasValue && data.OutdoorHumidity.HasValue)
                        HeatIndex.Add(data.Timestamp, MeteoLib.HeatIndex(data.OutdoorTemperature.Value, data.OutdoorHumidity.Value));
                }

                if (data.Humidex.HasValue)
                    Humidex.Add(data.Timestamp, data.Humidex.Value);
                else
                {
                    if (data.OutdoorTemperature.HasValue && data.OutdoorHumidity.HasValue)
                        Humidex.Add(data.Timestamp, MeteoLib.Humidex(data.OutdoorTemperature.Value, data.OutdoorHumidity.Value));
                }

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

        public static WeatherDataStatistics TryLoad(string dataFile)
        {
            try
            {
                
                using (var fileReader = File.OpenText(dataFile))
                {
                    var serialiser = new JsonSerializer();
                    serialiser.Converters.Add(new UnitsNetJsonConverter());
                    serialiser.TypeNameHandling = TypeNameHandling.Auto;
                    var reader = new JsonTextReader(fileReader);
                    var newWds = serialiser.Deserialize<WeatherDataStatistics>(reader);
                    newWds.Filename = dataFile;
                    return newWds;
                }
            }
            catch (Exception e)
            {
                //TODO: Log a warning
                return new WeatherDataStatistics() {Filename = dataFile};
            }
        }

        [JsonIgnore]
        public string Filename { get; set; }

        public void Save()
        {
            SaveAs(Filename);
        }

        public void SaveAs(string filename)
        { 
            GetReadLock();
            try
            {
                var serialiser = new JsonSerializer();
                serialiser.Converters.Add(new UnitsNetJsonConverter());
                serialiser.TypeNameHandling = TypeNameHandling.Auto;
                using (var fileWriter = File.Create(filename))
                {
                    var writer = new JsonTextWriter(new StreamWriter(fileWriter));
                    serialiser.Serialize(writer, this);
                }
            }
            finally
            {
                ReleaseReadLock();
            }
        }
    }
}
