using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CumulusMX.Extensions.Station;
using UnitsNet;
using Unosquare.Swan;

namespace CumulusMX.Data
{
    public class StatisticUnit<TBase, TUnitType> : IStatistic<TBase> 
        where TBase : IComparable, IQuantity<TUnitType>
        where TUnitType : Enum
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly DateTime EARLY_DATE;
        private readonly TUnitType FIRST_UNIT_TYPE;
        private readonly TBase ZERO_QUANTITY;

        private readonly Dictionary<DateTime, TBase> _sampleHistory = new Dictionary<DateTime, TBase>();
        private DateTime _lastSampleTime;
        private TBase _lastValue;
        private readonly MaxMinAverageUnit<TBase, TUnitType> _day;
        private MaxMinAverageUnit<TBase, TUnitType> _yesterday;
        private readonly MaxMinAverageUnit<TBase, TUnitType> _month;
        private MaxMinAverageUnit<TBase, TUnitType> _lastMonth;
        private readonly MaxMinAverageUnit<TBase, TUnitType> _year;
        private MaxMinAverageUnit<TBase, TUnitType> _lastYear;
        private double _oneHourChange;
        private double _threeHourChange;
        private double _24HourTotal;
        private TBase _oneHourMaximum;
        private TBase _threeHourMaximum;

        private TimeSpan _dayNonZero = TimeSpan.Zero;
        private TimeSpan _monthNonZero = TimeSpan.Zero;
        private TimeSpan _yearNonZero = TimeSpan.Zero;

        public StatisticUnit()
        {
            // Initialise runtime constants
            EARLY_DATE = DateTime.MinValue.AddYears(1);
            FIRST_UNIT_TYPE = (TUnitType)Enum.ToObject(typeof(TUnitType), 1);
            ZERO_QUANTITY = (TBase)Activator.CreateInstance(typeof(TBase), 0, FIRST_UNIT_TYPE);
            _day = new MaxMinAverageUnit<TBase, TUnitType>();
            _month = new MaxMinAverageUnit<TBase, TUnitType>();
            _year = new MaxMinAverageUnit<TBase, TUnitType>();
            _lastSampleTime = EARLY_DATE; // A very old date we can still look earlier than
        }

        /// <summary>
        /// Adds a new sample to the statistics set, and updates aggregates. This will be called a lot, it needs to be fast.
        /// </summary>
        /// <param name="timestamp">The DateTime when the observation was taken.</param>
        /// <param name="sample">The observed value.</param>
        public void Add(DateTime timestamp,TBase sample)
        {
            if (timestamp < _lastSampleTime)
            {
                log.Warn($"Invalid attempt to load data timestamped {timestamp} when latest data is already {_lastSampleTime}");
                return;
            }

            if (timestamp.Year > _lastSampleTime.Year)
                ResetYearValues();
            if (timestamp.Year * 12 + timestamp.Month > _lastSampleTime.Year * 12 + _lastSampleTime.Month)
                ResetMonthValues();
            if (timestamp.DayOfYear != _lastSampleTime.DayOfYear)
            {
                ResetDayValues();
                RemoveOldSamples(timestamp);
            }

            _sampleHistory.Add(timestamp,sample);
            _lastValue = sample;

            _day.AddValue(sample);
            _month.AddValue(sample);
            _year.AddValue(sample);

            Updating24HourTotal(timestamp, sample);

            UpdateMaximumAndChange(3, sample, _lastSampleTime, timestamp, ref _threeHourChange, ref _threeHourMaximum);
            UpdateMaximumAndChange(1, sample, _lastSampleTime, timestamp, ref _oneHourChange, ref _oneHourMaximum);

            if (sample.CompareTo(ZERO_QUANTITY) != 0 && _lastSampleTime > EARLY_DATE)
            {
                UpdateNonZeroTimes(timestamp);
            }

            _lastSampleTime = timestamp;
        }

        private void UpdateNonZeroTimes(DateTime timestamp)
        {
            var newSpan = timestamp - _lastSampleTime;
            if (timestamp.DayOfYear == _lastSampleTime.DayOfYear)
            {
                _dayNonZero += newSpan;
                _monthNonZero += newSpan;
                _yearNonZero += newSpan;
            }
            else
            {
                _dayNonZero += timestamp.TimeOfDay;
                if (timestamp.Month == _lastSampleTime.Month)
                {
                    _monthNonZero += newSpan;
                    _yearNonZero += newSpan;
                }
                else
                {
                    var sinceStartOfMonth = timestamp.TimeOfDay.Add(TimeSpan.FromDays(timestamp.Day - 1));
                    _monthNonZero += sinceStartOfMonth;
                    _yearNonZero += sinceStartOfMonth; // Ignore the case where year is reset after January
                }
            }
        }

        private void Updating24HourTotal(DateTime timestamp, TBase sample)
        {
            // Update rolling 24 hour total
            var rolledOff = _sampleHistory
                .Where(x => x.Key >= _lastSampleTime.AddHours(-24) && x.Key < timestamp.AddHours(-24));

            foreach (var oldValue in rolledOff)
            {
                _24HourTotal -= oldValue.Value.As(FIRST_UNIT_TYPE);
            }

            _24HourTotal += sample.As(FIRST_UNIT_TYPE);
        }

        private void RemoveOldSamples(DateTime timestamp)
        {
            var cleanupHistory = _sampleHistory
                .Where(x => x.Key < timestamp.AddDays(-2))
                .Select(x => x.Key)
                .ToArray();

            foreach (var oldSample in cleanupHistory)
                _sampleHistory.Remove(oldSample);
        }

        private void UpdateMaximumAndChange(int hours, TBase sample, DateTime lastSample, DateTime thisSample, ref double change, ref TBase maximum)
        {
            var oldSamples = _sampleHistory.Where(x => x.Key >= thisSample.AddHours(-1 * hours)).OrderBy(x => x.Key);
            if (!oldSamples.Any())
                return;

            change = sample.As(FIRST_UNIT_TYPE) - oldSamples.First().Value.As(FIRST_UNIT_TYPE);

            var rolledOff = _sampleHistory
                .Where(x => x.Key > lastSample.AddHours(-1 * hours) && x.Key <= thisSample.AddHours(-1 * hours));

            if (sample.CompareTo(maximum) > 0)
                maximum = sample;
            else
            {
                bool maxInvalid = false;
                foreach (var oldValue in rolledOff)
                {
                    if (oldValue.Value.Equals(maximum))
                        maxInvalid = true;
                }

                if (!maxInvalid) return;

                var historyWindow = _sampleHistory
                    .Where(x => x.Key > thisSample.AddHours(-1 * hours) && x.Key <= thisSample);
                maximum = sample;
                foreach (var oldSample in historyWindow)
                {
                    if (oldSample.Value.CompareTo(maximum) > 0)
                        maximum = oldSample.Value;
                }
            }
        }

        private void ResetDayValues()
        {
            _yesterday = _day.DeepClone();
            _day.Reset();
            _dayNonZero = TimeSpan.Zero;
        }

        private void ResetMonthValues()
        {
            _lastMonth = _month.DeepClone();
            _month.Reset();
            _monthNonZero = TimeSpan.Zero;
        }

        private void ResetYearValues()
        {
            _lastYear = _year.DeepClone();
            _year.Reset();
            _yearNonZero = TimeSpan.Zero;
        }

        public TBase Latest => _lastValue;

        public TBase OneHourMaximum => _oneHourMaximum;

        public TBase ThreeHourMaximum => _threeHourMaximum;

        public TBase OneHourChange => 
            (TBase)Activator.CreateInstance(typeof(TBase), _oneHourChange, FIRST_UNIT_TYPE);

        public TBase ThreeHourChange => 
            (TBase)Activator.CreateInstance(typeof(TBase), _threeHourChange, FIRST_UNIT_TYPE);

        public TBase DayMaximum => _day.Maximum;

        public TBase DayMinimum => _day.Minimum;

        public TBase DayAverage => _day.Average;

        public TBase MonthMaximum => _month.Maximum;

        public TBase MonthMinimum => _month.Minimum;

        public TBase MonthAverage => _month.Average;

        public TBase YearMaximum => _year.Maximum;

        public TBase YearMinimum => _year.Minimum;

        public TBase YearAverage => _year.Average;

        public TBase Last24hTotal => 
            (TBase)Activator.CreateInstance(typeof(TBase), _24HourTotal, FIRST_UNIT_TYPE);

        public TBase DayTotal => _day.Total;

        public TBase MonthTotal => _month.Total;

        public TBase YearTotal => _year.Total;

        public TimeSpan DayNonZero => _dayNonZero;

        public TimeSpan MonthNonZero => _monthNonZero;

        public TimeSpan YearNonZero => _yearNonZero;

        public Dictionary<DateTime, TBase> ValueHistory => _sampleHistory;

        public Dictionary<DateTime, double> ValueHistoryAs(TUnitType unit)
        {
            return _sampleHistory.ToDictionary(x => x.Key, x => x.Value.As(unit));
        }
    }
}
