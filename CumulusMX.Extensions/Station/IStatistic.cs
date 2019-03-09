using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace CumulusMX.Extensions.Station
{
    public interface IStatistic<TBase>
    {
        void Add(DateTime timestamp, TBase sample);
        TBase Latest { get; }
        TBase OneHourMaximum { get; }
        TBase ThreeHourMaximum { get; }
        TBase DayMaximum { get; }
        TBase DayMinimum { get; }
        TBase DayAverage { get; }
        TBase MonthMaximum { get; }
        TBase MonthMinimum { get; }
        TBase MonthAverage { get; }
        TBase YearMaximum { get; }
        TBase YearMinimum { get; }
        TBase YearAverage { get; }
        TBase OneHourChange { get; }
        TBase ThreeHourChange { get; }
        TBase Last24hTotal { get; }
        TBase DayTotal { get; }
        TBase MonthTotal { get; }
        TBase YearTotal { get; }
        TimeSpan DayNonZero { get; }
        TimeSpan MonthNonZero { get; }
        TimeSpan YearNonZero { get; }
        Dictionary<DateTime,TBase> ValueHistory { get; }
    }
}
