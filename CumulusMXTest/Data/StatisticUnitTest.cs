using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Data;
using UnitsNet;
using UnitsNet.Units;
using Xunit;

namespace CumulusMXTest.Data
{
    public class StatisticUnitTest
    {
        [Fact]
        public void SimpleStatsTest()
        {
            var startTime = DateTime.Parse("2019-03-09");

            var statUnit = new StatisticUnit<Temperature, TemperatureUnit>();
            statUnit.Add(startTime, Temperature.FromDegreesCelsius(10));
            statUnit.Add(startTime.AddHours(1), Temperature.FromDegreesCelsius(11));
            statUnit.Add(startTime.AddHours(2), Temperature.FromDegreesCelsius(12));
            statUnit.Add(startTime.AddHours(3), Temperature.FromDegreesCelsius(13));
            statUnit.Add(startTime.AddHours(4), Temperature.FromDegreesCelsius(14));
            statUnit.Add(startTime.AddHours(5), Temperature.FromDegreesCelsius(15));
            statUnit.Add(startTime.AddHours(6), Temperature.FromDegreesCelsius(16));
            statUnit.Add(startTime.AddHours(7), Temperature.FromDegreesCelsius(16.9));
            statUnit.Add(startTime.AddHours(8), Temperature.FromDegreesCelsius(17));
            statUnit.Add(startTime.AddHours(9), Temperature.FromDegreesCelsius(18));
            statUnit.Add(startTime.AddHours(10), Temperature.FromDegreesCelsius(20));
            statUnit.Add(startTime.AddHours(11), Temperature.FromDegreesCelsius(19));

            
            Assert.Equal((10.0+11+12+13+14+15+16+16.9+17+18+20+19)/12, statUnit.DayAverage.DegreesCelsius, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.MonthAverage.DegreesCelsius, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.YearAverage.DegreesCelsius, 3);
            Assert.Equal(20.0, statUnit.ThreeHourMaximum.DegreesCelsius, 3);
            Assert.Equal(19.0, statUnit.OneHourMaximum.DegreesCelsius, 3); 
            Assert.Equal(2.0, statUnit.ThreeHourChange.DegreesCelsius, 3);
            Assert.Equal(-1.0, statUnit.OneHourChange.DegreesCelsius, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19), statUnit.MonthTotal.DegreesCelsius, 3);
            Assert.Equal(11.0, statUnit.DayNonZero.TotalHours, 3);
        }

        [Fact]
        public void DayRolloverTest()
        {
            var startTime = DateTime.Parse("2019-03-09 21:00");

            var statUnit = new StatisticUnit<Temperature, TemperatureUnit>();
            statUnit.Add(startTime, Temperature.FromDegreesCelsius(10));
            statUnit.Add(startTime.AddHours(1), Temperature.FromDegreesCelsius(11));
            statUnit.Add(startTime.AddHours(2), Temperature.FromDegreesCelsius(12));
            statUnit.Add(startTime.AddHours(3), Temperature.FromDegreesCelsius(13));
            statUnit.Add(startTime.AddHours(4), Temperature.FromDegreesCelsius(14));
            statUnit.Add(startTime.AddHours(5), Temperature.FromDegreesCelsius(15));
            statUnit.Add(startTime.AddHours(6), Temperature.FromDegreesCelsius(16));
            statUnit.Add(startTime.AddHours(7), Temperature.FromDegreesCelsius(16.9));
            statUnit.Add(startTime.AddHours(8), Temperature.FromDegreesCelsius(17));
            statUnit.Add(startTime.AddHours(9), Temperature.FromDegreesCelsius(18));
            statUnit.Add(startTime.AddHours(10), Temperature.FromDegreesCelsius(20));
            statUnit.Add(startTime.AddHours(11), Temperature.FromDegreesCelsius(19));
            
            Assert.Equal((13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 9, statUnit.DayAverage.DegreesCelsius, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.MonthAverage.DegreesCelsius, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.YearAverage.DegreesCelsius, 3);
            Assert.Equal(20.0, statUnit.DayMaximum.DegreesCelsius, 3);
            Assert.Equal(13.0, statUnit.DayMinimum.DegreesCelsius, 3);
            Assert.Equal(20.0, statUnit.MonthMaximum.DegreesCelsius, 3);
            Assert.Equal(10.0, statUnit.MonthMinimum.DegreesCelsius, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19), statUnit.MonthTotal.DegreesCelsius, 3);
            Assert.Equal((13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19), statUnit.DayTotal.DegreesCelsius, 3);
            Assert.Equal(8.0, statUnit.DayNonZero.TotalHours, 3);
            Assert.Equal(11.0, statUnit.MonthNonZero.TotalHours, 3);
        }


        [Fact]
        public void MonthAndYearRolloverTest()
        {
            var startTime = DateTime.Parse("2018-12-25 12:34:56");

            var statUnit = new StatisticUnit<Temperature, TemperatureUnit>();
            statUnit.Add(startTime.AddYears(-1), Temperature.FromDegreesCelsius(5)); // 2017-12-25
            statUnit.Add(startTime, Temperature.FromDegreesCelsius(10));             // 2018-12-25
            statUnit.Add(startTime.AddDays(7), Temperature.FromDegreesCelsius(11));  // 2019-01-01
            statUnit.Add(startTime.AddDays(14), Temperature.FromDegreesCelsius(12)); // 2019-01-08
            statUnit.Add(startTime.AddDays(21), Temperature.FromDegreesCelsius(13)); // 2019-01-15
            statUnit.Add(startTime.AddDays(28), Temperature.FromDegreesCelsius(14)); // 2019-01-22
            statUnit.Add(startTime.AddDays(35), Temperature.FromDegreesCelsius(15)); // 2019-01-29
            statUnit.Add(startTime.AddDays(42), Temperature.FromDegreesCelsius(21)); // 2019-02-05
            statUnit.Add(startTime.AddDays(49), Temperature.FromDegreesCelsius(16)); // 2019-02-12
            statUnit.Add(startTime.AddDays(56), Temperature.FromDegreesCelsius(17)); // 2019-02-19
            statUnit.Add(startTime.AddDays(63), Temperature.FromDegreesCelsius(18)); // 2019-02-26
            statUnit.Add(startTime.AddDays(70), Temperature.FromDegreesCelsius(20)); // 2019-03-05
            statUnit.Add(startTime.AddDays(77), Temperature.FromDegreesCelsius(19)); // 2019-03-12

            Assert.Equal(19.0, statUnit.DayAverage.DegreesCelsius, 3);
            Assert.Equal((19.0 + 20.0) / 2, statUnit.MonthAverage.DegreesCelsius, 3);
            Assert.Equal((11.0 + 12 + 13 + 14 + 15 + 16 + 21 + 17 + 18 + 20 + 19) / 11, statUnit.YearAverage.DegreesCelsius, 3);
            Assert.Equal(20.0, statUnit.MonthMaximum.DegreesCelsius, 3);
            Assert.Equal(19.0, statUnit.MonthMinimum.DegreesCelsius, 3);
            Assert.Equal(21.0, statUnit.YearMaximum.DegreesCelsius, 3);
            Assert.Equal(11.0, statUnit.YearMinimum.DegreesCelsius, 3);
            Assert.Equal(20.0 + 19, statUnit.MonthTotal.DegreesCelsius, 3);
            Assert.Equal(11.0 + 12 + 13 + 14 + 15 + 16 + 21 + 17 + 18 + 20 + 19, statUnit.YearTotal.DegreesCelsius, 3);
            Assert.Equal(startTime.TimeOfDay.TotalHours, statUnit.DayNonZero.TotalHours, 3);
            Assert.Equal(startTime.TimeOfDay.TotalHours + (11 * 24.0), statUnit.MonthNonZero.TotalHours, 3);
        }
    }
}
