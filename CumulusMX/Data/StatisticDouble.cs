using System;
using System.Collections.Generic;
using System.Linq;
using CumulusMX.Extensions.Station;
using Unosquare.Swan;

namespace CumulusMX.Data
{
    public class StatisticDouble : IStatistic<double>
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly DateTime EARLY_DATE;

        private readonly Dictionary<DateTime, double> _sampleHistory = new Dictionary<DateTime, double>();
        private DateTime _lastSampleTime;
        private readonly MaxMinAverageDouble _day;
        private MaxMinAverageDouble _yesterday;
        private readonly MaxMinAverageDouble _month;
        private MaxMinAverageDouble _lastMonth;
        private readonly MaxMinAverageDouble _year;
        private MaxMinAverageDouble _lastYear;
        private double _oneHourChange;
        private double _threeHourChange;
        private double _24HourTotal;
        private double _oneHourMaximum;
        private double _threeHourMaximum;

        private TimeSpan _dayNonZero = TimeSpan.Zero;
        private TimeSpan _monthNonZero = TimeSpan.Zero;
        private TimeSpan _yearNonZero = TimeSpan.Zero;

        public StatisticDouble()
        {
            // Initialise runtime constants
            EARLY_DATE = DateTime.MinValue.AddYears(1);
            _day = new MaxMinAverageDouble();
            _month = new MaxMinAverageDouble();
            _year = new MaxMinAverageDouble();
            _lastSampleTime = EARLY_DATE; // A very old date we can still look earlier than
        }

        /// <summary>
        /// Adds a new sample to the statistics set, and updates aggregates. This will be called a lot, it needs to be fast.
        /// </summary>
        /// <param name="timestamp">The DateTime when the observation was taken.</param>
        /// <param name="sample">The observed value.</param>
        public void Add(DateTime timestamp,double sample)
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
            Latest = sample;

            _day.AddValue(sample);
            _month.AddValue(sample);
            _year.AddValue(sample);

            Updating24HourTotal(timestamp, sample);

            UpdateMaximumAndChange(3, sample, _lastSampleTime, timestamp, ref _threeHourChange, ref _threeHourMaximum);
            UpdateMaximumAndChange(1, sample, _lastSampleTime, timestamp, ref _oneHourChange, ref _oneHourMaximum);

            if (sample != 0 && _lastSampleTime > EARLY_DATE)
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

        private void Updating24HourTotal(DateTime timestamp, double sample)
        {
            // Update rolling 24 hour total
            var rolledOff = _sampleHistory
                .Where(x => x.Key >= _lastSampleTime.AddHours(-24) && x.Key < timestamp.AddHours(-24));

            foreach (var oldValue in rolledOff)
            {
                _24HourTotal -= oldValue.Value;
            }

            _24HourTotal += sample;
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

        private void UpdateMaximumAndChange(int hours, double sample, DateTime lastSample, DateTime thisSample, ref double change, ref double maximum)
        {
            var oldSamples = _sampleHistory.Where(x => x.Key >= thisSample.AddHours(-1 * hours)).OrderBy(x => x.Key);
            if (!oldSamples.Any())
                return;

            change = sample - oldSamples.First().Value;

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

        public double Latest { get; private set; }

        public double OneHourMaximum => _oneHourMaximum;

        public double ThreeHourMaximum => _threeHourMaximum;

        public double OneHourChange => _oneHourChange;

        public double ThreeHourChange => _threeHourChange;

        public double DayMaximum => _day.Maximum;

        public double DayMinimum => _day.Minimum;

        public double DayAverage => _day.Average;

        public double MonthMaximum => _month.Maximum;

        public double MonthMinimum => _month.Minimum;

        public double MonthAverage => _month.Average;

        public double YearMaximum => _year.Maximum;

        public double YearMinimum => _year.Minimum;

        public double YearAverage => _year.Average;

        public double Last24hTotal => _24HourTotal;

        public double DayTotal => _day.Total;

        public double MonthTotal => _month.Total;

        public double YearTotal => _year.Total;

        public TimeSpan DayNonZero => _dayNonZero;

        public TimeSpan MonthNonZero => _monthNonZero;

        public TimeSpan YearNonZero => _yearNonZero;

        public Dictionary<DateTime, double> ValueHistory => _sampleHistory;
    }
}
