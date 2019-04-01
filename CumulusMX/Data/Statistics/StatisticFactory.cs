using System;
using System.Collections.Generic;
using CumulusMX.Data.Statistics.Unit;
using CumulusMX.Extensions.Station;
using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Data.Statistics
{
    public static class StatisticFactory
    {
        private static readonly Dictionary<Type, Func<IStatistic<IQuantity>>> TypeConstructors =
            new Dictionary<Type, Func<IStatistic<IQuantity>>>
            {
                {typeof(Temperature), () => (IStatistic<IQuantity>)new StatisticUnit<Temperature, TemperatureUnit>() },
                {typeof(Ratio), () => (IStatistic<IQuantity>)new StatisticUnit<Ratio, RatioUnit>() },
                {typeof(Angle), () => (IStatistic<IQuantity>)new StatisticUnit<Angle, AngleUnit>() },
                {typeof(Pressure), () => (IStatistic<IQuantity>)new StatisticUnit<Pressure, PressureUnit>() },
                {typeof(Length), () => (IStatistic<IQuantity>)new StatisticUnit<Length, LengthUnit>() },
                {typeof(Irradiance), () => (IStatistic<IQuantity>)new StatisticUnit<Irradiance, IrradianceUnit>() }

            };

        public static IStatistic<IQuantity> Build(Type unitType)
        {
            if (!TypeConstructors.ContainsKey(unitType))
                throw new ArgumentException($"No Statistic Unit factory exists for unit {unitType}.");

            return TypeConstructors[unitType]();
        }
    }
}
