using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CumulusMX.Extensions.Station;
using UnitsNet;
using Unosquare.Swan;

namespace CumulusMX.Data
{
    public class StatisticUnit<TBase,TUnitType> : IStatistic<TBase> where TBase : IComparable, IQuantity<TUnitType> where TUnitType : Enum
    {
        private Dictionary<DateTime, TBase> _sampleHistory = new Dictionary<DateTime, TBase>();
        private DateTime _lastSampleTime = DateTime.Now;
        private TBase _lastValue;
        private MaxMinAverageUnit<TBase, TUnitType> _day;
        private MaxMinAverageUnit<TBase, TUnitType> _yesterday;
        private MaxMinAverageUnit<TBase, TUnitType> _month;
        private MaxMinAverageUnit<TBase, TUnitType> _lastMonth;
        private MaxMinAverageUnit<TBase, TUnitType> _year;
        private MaxMinAverageUnit<TBase, TUnitType> _lastYear;
        private double _oneHourChange;
        private double _threeHourChange;
        private double _24HourTotal;
        private TBase _oneHourMaximum;
        private TBase _threeHourMaximum;

        private TimeSpan _dayNonZero = TimeSpan.Zero;
        private TimeSpan _monthNonZero = TimeSpan.Zero;
        private TimeSpan _yearNonZero = TimeSpan.Zero;

        private readonly TUnitType _itemOne;
        private readonly TBase _zeroQuantity;

        public StatisticUnit()
        {
            _itemOne = (TUnitType)Enum.ToObject(typeof(TUnitType), 1);
            _zeroQuantity = (TBase)Activator.CreateInstance(typeof(TBase), 0, _itemOne);

        }

        /// <summary>
        /// Adds a new sample to the statistics set, and updates aggregates. This will be called a lot, it needs to be fast.
        /// </summary>
        /// <param name="timestamp">The DateTime when the observation was taken.</param>
        /// <param name="sample">The observed value.</param>
        public void Add(DateTime timestamp,TBase sample)
        {
            if (timestamp.Year > _lastSampleTime.Year)
                ResetYearValues();
            if (timestamp.Year * 12 + timestamp.Month > _lastSampleTime.Year * 12 + _lastSampleTime.Month)
                ResetMonthValues();
            if (timestamp.DayOfYear != _lastSampleTime.DayOfYear)
                ResetDayValues();

            _sampleHistory.Add(timestamp,sample);
            _lastValue = sample;

            _day.AddValue(sample);
            _month.AddValue(sample);
            _year.AddValue(sample);

            // Update rolling 24 hour total
            var rolledOff = _sampleHistory
                .Where(x => x.Key >= _lastSampleTime.AddHours(-24) && x.Key < timestamp.AddHours(-24));

            foreach (var oldValue in rolledOff)
            {
                _24HourTotal -= oldValue.Value.As(_itemOne);
            }
            _24HourTotal += sample.As(_itemOne);

            UpdateMaximumAndChange(3, sample, _lastSampleTime, timestamp, ref _threeHourChange, ref _threeHourMaximum);
            UpdateMaximumAndChange(1, sample, _lastSampleTime, timestamp, ref _oneHourChange, ref _oneHourMaximum);

            if (sample.CompareTo(_zeroQuantity) != 0)
            {
                var newSpan = timestamp - _lastSampleTime;
                _dayNonZero += newSpan;
                _monthNonZero += newSpan;
                _yearNonZero += newSpan;
            }

            _lastSampleTime = timestamp;
        }

        private void UpdateMaximumAndChange(int months, TBase sample, DateTime lastSample, DateTime thisSample, ref double change, ref TBase maximum)
        {
            var oldSamples = _sampleHistory.Where(x => x.Key >= lastSample.AddHours(-1 * months)).OrderBy(x => x.Key);
            if (!oldSamples.Any())
                return;

            change = sample.As(_itemOne) - oldSamples.First().Value.As(_itemOne);

            var rolledOff = _sampleHistory
                .Where(x => x.Key >= lastSample.AddHours(-1*months) && x.Key < thisSample.AddHours(-1*months));

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
                    .Where(x => x.Key > thisSample.AddHours(-1 * months) && x.Key <= thisSample);
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

        public TBase OneHourChange => (TBase)Activator.CreateInstance(typeof(TBase), _oneHourChange, _itemOne);

        public TBase ThreeHourChange => (TBase)Activator.CreateInstance(typeof(TBase), _threeHourChange, _itemOne);

        public TBase DayMaximum => _day.Maximum;

        public TBase DayMinimum => _day.Minimum;

        public TBase DayAverage => _day.Average;

        public TBase MonthMaximum => _month.Maximum;

        public TBase MonthMinimum => _month.Minimum;

        public TBase MonthAverage => _month.Average;

        public TBase YearMaximum => _year.Maximum;

        public TBase YearMinimum => _year.Minimum;

        public TBase YearAverage => _year.Average;

        public TBase Last24hTotal => (TBase)Activator.CreateInstance(typeof(TBase), _24HourTotal, _itemOne);

        public TBase DayTotal => _day.Total;

        public TBase MonthTotal => _month.Total;

        public TBase YearTotal => _year.Total;

        public TimeSpan DayNonZero => _dayNonZero;

        public TimeSpan MonthNonZero => _monthNonZero;

        public TimeSpan YearNonZero => _yearNonZero;
    }
}
