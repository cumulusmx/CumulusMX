using System;

namespace CumulusMX.Extensions.Station
{
    public interface IRecords<TBase>
    {
        TBase Maximum { get; }
        DateTime MaximumTime { get; }
        TBase Minimum { get; }
        DateTime MinimumTime { get; }

        void AddValue(DateTime timestamp, TBase sample);
    }
}