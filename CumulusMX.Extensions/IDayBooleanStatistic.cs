using System;
using UnitsNet;

namespace CumulusMX.Data.Statistics
{
    public interface IDayBooleanStatistic
    {
        void Add();
        int Consecutive { get; }
        int MonthMaximumConsecutive { get; }
        DateTime MonthMaximumConsecutiveDay { get; }
        int YearMaximumConsecutive { get; }
        DateTime YearMaximumConsecutiveDay { get; }
        int RecordMaximumConsecutive { get; }
        DateTime RecordMaximumConsecutiveDay { get; }
        int MonthCount { get; }
        int YearCount { get; }
        int AllTimeCount { get; }
        Ratio MonthRatio { get; }
        Ratio YearRatio { get; }
        Ratio AllTimeRatio { get; }
    }
}