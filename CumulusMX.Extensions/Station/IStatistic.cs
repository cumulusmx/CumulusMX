using System;
using System.Collections.Generic;
using CumulusMX.Data.Statistics;

namespace CumulusMX.Extensions.Station
{
    public interface IStatistic<out TBase> : IStatistic
    {
        DateTime LastSample { get; }
        TBase Latest { get; }
        TBase OneHourMaximum { get; }
        TBase ThreeHourMaximum { get; }
        DateTime OneHourMaximumTime { get; }
        DateTime ThreeHourMaximumTime { get; }
        TBase OneHourAverage { get; }
        TBase ThreeHourAverage { get; }
        TBase DayMaximum { get; }
        TBase DayMinimum { get; }
        DateTime DayMaximumTime { get; }
        DateTime DayMinimumTime { get; }
        TBase DayAverage { get; }
        TBase DayRange { get; }
        TBase MonthMaximum { get; }
        TBase MonthMinimum { get; }
        DateTime MonthMaximumTime { get; }
        DateTime MonthMinimumTime { get; }
        TBase MonthLowestMaximum { get; }
        TBase MonthHighestMinimum { get; }
        DateTime MonthLowestMaximumDay { get; }
        DateTime MonthHighestMinimumDay { get; }
        TBase MonthAverage { get; }
        TBase MonthRange { get; }
        TBase YearMaximum { get; }
        TBase YearMinimum { get; }
        DateTime YearMaximumTime { get; }
        DateTime YearMinimumTime { get; }
        TBase YearLowestMaximum { get; }
        TBase YearHighestMinimum { get; }
        DateTime YearLowestMaximumDay { get; }
        DateTime YearHighestMinimumDay { get; }
        TBase RecordMaximum { get; }
        TBase RecordMinimum { get; }
        bool RecordNow { get; }
        bool RecordLastHour { get; }
        DateTime RecordMaximumTime { get; }
        DateTime RecordMinimumTime { get; }
        TBase RecordLowestMaximum { get; }
        TBase RecordHighestMinimum { get; }
        DateTime RecordLowestMaximumDay { get; }
        DateTime RecordHighestMinimumDay { get; }
        TBase YearAverage { get; }
        TBase YearRange { get; }
        TBase OneHourChange { get; }
        TBase ThreeHourChange { get; }
        TBase Last24hTotal { get; }
        TBase DayTotal { get; }
        TBase YearMaximumDayTotal { get; }
        TBase YearMinimumDayTotal { get; }
        TBase RecordMaximumDayTotal { get; }
        TBase RecordMinimumDayTotal { get; }
        DateTime YearMaximumDay { get; }
        DateTime YearMinimumDay { get; }
        DateTime RecordMaximumDay { get; }
        DateTime RecordMinimumDay { get; }
        TBase YearMaximumDayRange { get; }
        TBase YearMinimumDayRange { get; }
        TBase RecordMaximumDayRange { get; }
        TBase RecordMinimumDayRange { get; }
        DateTime YearMaximumDayRangeDay { get; }
        DateTime YearMinimumDayRangeDay { get; }
        DateTime RecordMaximumDayRangeDay { get; }
        DateTime RecordMinimumDayRangeDay { get; }
        TBase MonthTotal { get; }
        TBase YearTotal { get; }
        TimeSpan DayNonZero { get; }
        TimeSpan MonthNonZero { get; }
        TimeSpan YearNonZero { get; }
        IRecords<TBase>[] ByMonth { get; }
        IRecords<TBase> CurrentMonth { get; }

        IRecordsAndAverage<TBase> Yesterday { get; }
        IRecordsAndAverage<TBase> LastMonth { get; }
        IRecordsAndAverage<TBase> LastYear { get; }

        Dictionary<DateTime, double> ValueHistory { get; }

        void AddBooleanStatistics(IDayBooleanStatistic heatingDegreeDays);
    }

    public interface IStatistic
    {
        object LatestObject { get; }
    }
}
