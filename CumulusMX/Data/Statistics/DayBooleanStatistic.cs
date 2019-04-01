using System;
using CumulusMX.Extensions.Station;
using Newtonsoft.Json;
using UnitsNet;
using UnitsNet.Units;

namespace CumulusMX.Data.Statistics
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DayBooleanStatistic<TBase> : IDayBooleanStatistic
    {
        private readonly IStatistic<TBase> _statistics;
        private Func<IStatistic<TBase>, bool> TheFunction { get; }
        [JsonProperty]
        private int _consecutive = 0;
        [JsonProperty]
        private int _monthMaximumConsecutive;
        [JsonProperty]
        private DateTime _monthMaximumConsecutiveDay;
        [JsonProperty]
        private int _yearMaximumConsecutive;
        [JsonProperty]
        private DateTime _yearMaximumConsecutiveDay;
        [JsonProperty]
        private int _recordMaximumConsecutive;
        [JsonProperty]
        private DateTime _recordMaximumConsecutiveDay = DateTime.MinValue;
        [JsonProperty]
        private int _monthCount;
        [JsonProperty]
        private int _yearCount;
        [JsonProperty]
        private int _allTimeCount;
        [JsonProperty]
        private int _yearTotalCount;
        [JsonProperty]
        private int _lastYear;
        [JsonProperty]
        private int _monthTotalCount;
        [JsonProperty]
        private int _lastMonth;
        [JsonProperty]
        private int _allTimeTotalCount;

        public DayBooleanStatistic(IStatistic<TBase> statistics, Func<IStatistic<TBase>, bool> theFunction)
        {
            _statistics = statistics;
            TheFunction = theFunction;
        }

        public void Add()
        {
            var year = _statistics.LastSample.Year;
            var month = year * 12 + _statistics.LastSample.Month;
            var isTrue = TheFunction(_statistics);
            if (year != _lastYear)
            {
                _lastYear = year;
                _yearCount = 0;
                _yearMaximumConsecutive = 0;
                _yearMaximumConsecutiveDay = _statistics.LastSample.Date;
                _yearTotalCount = 0;
            }
            if (month != _lastMonth)
            {
                _lastMonth = month;
                _monthCount = 0;
                _monthMaximumConsecutive = 0;
                _monthMaximumConsecutiveDay = _statistics.LastSample.Date;
                _monthTotalCount = 0;
            }

            _yearTotalCount++;
            _monthTotalCount++;
            _allTimeTotalCount++;
            if (isTrue)
            {
                _yearCount++;
                _monthCount++;
                _allTimeCount++;

                _consecutive++;
                if (_consecutive > _monthMaximumConsecutive)
                {
                    _monthMaximumConsecutive = _consecutive;
                    _monthMaximumConsecutiveDay = _statistics.LastSample.Date;
                }
                if (_consecutive > _yearMaximumConsecutive)
                {
                    _yearMaximumConsecutive = _consecutive;
                    _yearMaximumConsecutiveDay = _statistics.LastSample.Date;
                }
                if (_consecutive > _recordMaximumConsecutive)
                {
                    _recordMaximumConsecutive = _consecutive;
                    _recordMaximumConsecutiveDay = _statistics.LastSample.Date;
                }
            }
            else
            {
                _consecutive = 0;
            }
        }

        public int Consecutive => _consecutive;

        public int MonthMaximumConsecutive => _monthMaximumConsecutive;
        public DateTime MonthMaximumConsecutiveDay => _monthMaximumConsecutiveDay;
        public int YearMaximumConsecutive => _yearMaximumConsecutive;
        public DateTime YearMaximumConsecutiveDay => _yearMaximumConsecutiveDay;
        public int RecordMaximumConsecutive => _recordMaximumConsecutive;
        public DateTime RecordMaximumConsecutiveDay => _recordMaximumConsecutiveDay;
        public int MonthCount => _monthCount;
        public int YearCount => _yearCount;
        public int AllTimeCount => _allTimeCount;
        public Ratio MonthRatio => new Ratio((double)_monthCount/(double)_monthTotalCount,RatioUnit.DecimalFraction);
        public Ratio YearRatio => new Ratio((double)_yearCount / (double)_yearTotalCount, RatioUnit.DecimalFraction);
        public Ratio AllTimeRatio => new Ratio((double)_allTimeCount / (double)_allTimeTotalCount, RatioUnit.DecimalFraction);
    }
}