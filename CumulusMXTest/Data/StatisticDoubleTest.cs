using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CumulusMX.Common;
using CumulusMX.Data;
using CumulusMX.Data.Statistics.Unit;
using CumulusMX.Extensions;
using UnitsNet;
using UnitsNet.Units;
using Xunit;

namespace CumulusMXTest.Data
{
    public class StatisticDoubleTest
    {
        public StatisticDoubleTest()
        {
            if (!log4net.LogManager.GetAllRepositories().Any(x => x.Name == "cumulus"))
            {
                try
                {
                    log4net.LogManager.CreateRepository("cumulus");
                }
                catch
                {
                }
            }
        }

        [Fact]
        public void SimpleStatsTest()
        {
            var startTime = DateTime.Parse("2019-03-09");

            var statUnit = new StatisticUnit<Number, NumberUnit>();
            statUnit.Add(startTime, 10.0);
            statUnit.Add(startTime.AddHours(1), (11.0));
            statUnit.Add(startTime.AddHours(2), (12.0));
            statUnit.Add(startTime.AddHours(3), (13.0));
            statUnit.Add(startTime.AddHours(4), (14.0));
            statUnit.Add(startTime.AddHours(5), (15.0));
            statUnit.Add(startTime.AddHours(6), (16.0));
            statUnit.Add(startTime.AddHours(7), (16.9));
            statUnit.Add(startTime.AddHours(8), (17.0));
            statUnit.Add(startTime.AddHours(9), (18.0));
            statUnit.Add(startTime.AddHours(10), (20.0));
            statUnit.Add(startTime.AddHours(11), (19.0));

            
            Assert.Equal((10.0+11+12+13+14+15+16+16.9+17+18+20+19)/12, statUnit.DayAverage, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.MonthAverage, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.YearAverage, 3);
            Assert.Equal(20.0, statUnit.ThreeHourMaximum, 3);
            Assert.Equal(19.0, statUnit.OneHourMaximum, 3); 
            Assert.Equal(2.0, statUnit.ThreeHourChange, 3);
            Assert.Equal(-1.0, statUnit.OneHourChange, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19), statUnit.MonthTotal, 3);
            Assert.Equal(11.0, statUnit.DayNonZero.TotalHours, 3);
        }

        [Fact]
        public void DayRolloverTest()
        {
            var startTime = DateTime.Parse("2019-03-09 21:00");
            
            var statUnit = new StatisticUnit<Number, NumberUnit>();
            statUnit.Add(startTime, (10.0));
            statUnit.Add(startTime.AddHours(1), (11.0));
            statUnit.Add(startTime.AddHours(2), (12.0));
            statUnit.Add(startTime.AddHours(3), (13.0));
            statUnit.Add(startTime.AddHours(4), (14.0));
            statUnit.Add(startTime.AddHours(5), (15.0));
            statUnit.Add(startTime.AddHours(6), (16.0));
            statUnit.Add(startTime.AddHours(7), (16.9));
            statUnit.Add(startTime.AddHours(8), (17.0));
            statUnit.Add(startTime.AddHours(9), (18.0));
            statUnit.Add(startTime.AddHours(10), (20.0));
            statUnit.Add(startTime.AddHours(11), (19.0));
            
            Assert.Equal((13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 9, statUnit.DayAverage, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.MonthAverage, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19) / 12, statUnit.YearAverage, 3);
            Assert.Equal(20.0, statUnit.DayMaximum, 3);
            Assert.Equal(13.0, statUnit.DayMinimum, 3);
            Assert.Equal(20.0, statUnit.MonthMaximum, 3);
            Assert.Equal(10.0, statUnit.MonthMinimum, 3);
            Assert.Equal((10.0 + 11 + 12 + 13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19), statUnit.MonthTotal, 3);
            Assert.Equal((13 + 14 + 15 + 16 + 16.9 + 17 + 18 + 20 + 19), statUnit.DayTotal, 3);
            Assert.Equal(8.0, statUnit.DayNonZero.TotalHours, 3);
            Assert.Equal(11.0, statUnit.MonthNonZero.TotalHours, 3);
        }


        [Fact]
        public void MonthAndYearRolloverTest()
        {
            var startTime = DateTime.Parse("2018-12-25 12:34:56");

            var statUnit = new StatisticUnit<Number, NumberUnit>();
            statUnit.Add(startTime.AddYears(-1), (5.0)); // 2017-12-25
            statUnit.Add(startTime, (10.0));             // 2018-12-25
            statUnit.Add(startTime.AddDays(7), (11.0));  // 2019-01-01
            statUnit.Add(startTime.AddDays(14), (12.0)); // 2019-01-08
            statUnit.Add(startTime.AddDays(21), (13.0)); // 2019-01-15
            statUnit.Add(startTime.AddDays(28), (14.0)); // 2019-01-22
            statUnit.Add(startTime.AddDays(35), (15.0)); // 2019-01-29
            statUnit.Add(startTime.AddDays(42), (21.0)); // 2019-02-05
            statUnit.Add(startTime.AddDays(49), (16.0)); // 2019-02-12
            statUnit.Add(startTime.AddDays(56), (17.0)); // 2019-02-19
            statUnit.Add(startTime.AddDays(63), (18.0)); // 2019-02-26
            statUnit.Add(startTime.AddDays(70), (20.0)); // 2019-03-05
            statUnit.Add(startTime.AddDays(77), (19.0)); // 2019-03-12

            Assert.Equal(19.0, statUnit.DayAverage, 3);
            Assert.Equal((19.0 + 20.0) / 2, statUnit.MonthAverage, 3);
            Assert.Equal((11.0 + 12 + 13 + 14 + 15 + 16 + 21 + 17 + 18 + 20 + 19) / 11, statUnit.YearAverage, 3);
            Assert.Equal(20.0, statUnit.MonthMaximum, 3);
            Assert.Equal(19.0, statUnit.MonthMinimum, 3);
            Assert.Equal(21.0, statUnit.YearMaximum, 3);
            Assert.Equal(11.0, statUnit.YearMinimum, 3);
            Assert.Equal(20.0 + 19, statUnit.MonthTotal, 3);
            Assert.Equal(11.0 + 12 + 13 + 14 + 15 + 16 + 21 + 17 + 18 + 20 + 19, statUnit.YearTotal, 3);
            Assert.Equal(startTime.TimeOfDay.TotalHours, statUnit.DayNonZero.TotalHours, 3);
            Assert.Equal(startTime.TimeOfDay.TotalHours + (11 * 24.0), statUnit.MonthNonZero.TotalHours, 3);
        }
    }
}
