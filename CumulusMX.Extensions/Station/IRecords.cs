using System;

namespace CumulusMX.Extensions.Station
{
    public interface IRecords<out TBase>
    {
        TBase Maximum { get; }
        DateTime MaximumTime { get; }
        TBase Minimum { get; }
        DateTime MinimumTime { get; }

        void AddValue(DateTime timestamp, object sample);
    }
}