using System;
using System.Collections.Generic;
using System.Linq;
using CumulusMX.Extensions.Station;
using UnitsNet;
using Unosquare.Swan;

namespace CumulusMX.Data.Statistics.Unit
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
        private readonly MaxMinAverageUnit<TBase, TUnitType> _day;
        private MaxMinAverageUnit<TBase, TUnitType> _yesterday;
        private readonly MaxMinAverageUnit<TBase, TUnitType> _month;
        private MaxMinAverageUnit<TBase, TUnitType> _lastMonth;
        private readonly DayStatisticUnit<TBase, TUnitType> _monthByDay;
        private DayStatisticUnit<TBase, TUnitType> _lastMonthByDay;
        private readonly MaxMinAverageUnit<TBase, TUnitType> _year;
        private MaxMinAverageUnit<TBase, TUnitType> _lastYear;
        private readonly DayStatisticUnit<TBase, TUnitType> _yearByDay;
        private DayStatisticUnit<TBase, TUnitType> _lastYearByDay;
        private readonly MaxMinAverageUnit<TBase, TUnitType> _allTime;
        private readonly DayStatisticUnit<TBase, TUnitType> _allTimeByDay;
        private readonly IRecords<TBase>[] _monthRecords;

        private TimeSpan _dayNonZero = TimeSpan.Zero;
        private TimeSpan _monthNonZero = TimeSpan.Zero;
        private TimeSpan _yearNonZero = TimeSpan.Zero;
        private readonly RollingStatisticUnit<TBase, TUnitType> _oneHour;
        private readonly RollingStatisticUnit<TBase, TUnitType> _threeHours;
        private readonly RollingStatisticUnit<TBase, TUnitType> _24Hours;

        private readonly List<IDayBooleanStatistic> _booleanStatistics;

        public StatisticUnit()
        {
            _booleanStatistics = new List<IDayBooleanStatistic>();
            // Initialise runtime constants
            EARLY_DATE = DateTime.MinValue.AddYears(1);
            FIRST_UNIT_TYPE = UnitTools.FirstUnit<TUnitType>();
            ZERO_QUANTITY = UnitTools.ZeroQuantity<TBase, TUnitType>();
            _day = new MaxMinAverageUnit<TBase, TUnitType>();
            _month = new MaxMinAverageUnit<TBase, TUnitType>();
            _year = new MaxMinAverageUnit<TBase, TUnitType>();
            _allTime = new MaxMinAverageUnit<TBase, TUnitType>();
            _monthByDay = new DayStatisticUnit<TBase, TUnitType>();
            _yearByDay = new DayStatisticUnit<TBase, TUnitType>();
            _allTimeByDay = new DayStatisticUnit<TBase, TUnitType>();

            _oneHour = new RollingStatisticUnit<TBase, TUnitType>(1, _sampleHistory);
            _threeHours = new RollingStatisticUnit<TBase, TUnitType>(3, _sampleHistory);
            _24Hours = new RollingStatisticUnit<TBase, TUnitType>(24, _sampleHistory);

            _monthRecords = new IRecords<TBase>[12];
            for (int i = 0; i < 12; i++)
                _monthRecords[i] = new MaxMinAverageUnit<TBase, TUnitType>();

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
                _monthByDay.Add(_day);
                _yearByDay.Add(_day);
                _allTimeByDay.Add(_day);
                foreach(var booleanStatistic in _booleanStatistics)
                    booleanStatistic.Add();

                ResetDayValues();
                RemoveOldSamples(timestamp);
            }

            _sampleHistory.Add(timestamp,sample);
            Latest = sample;

            _day.AddValue(timestamp, sample);
            _month.AddValue(timestamp, sample);
            _year.AddValue(timestamp, sample);
            _allTime.AddValue(timestamp, sample);
            _monthRecords[timestamp.Month - 1].AddValue(timestamp, sample);

            _oneHour.AddValue(timestamp, sample);
            _threeHours.AddValue(timestamp, sample);
            _24Hours.AddValue(timestamp, sample);

            if (sample.CompareTo(ZERO_QUANTITY) != 0 && _lastSampleTime > EARLY_DATE)
            {
                UpdateNonZeroTimes(timestamp);
            }

            _lastSampleTime = timestamp;
        }

        public DateTime LastSample => _lastSampleTime;

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

        private void RemoveOldSamples(DateTime timestamp)
        {
            var cleanupHistory = _sampleHistory
                .Where(x => x.Key < timestamp.AddDays(-2))
                .Select(x => x.Key)
                .ToArray();

            foreach (var oldSample in cleanupHistory)
                _sampleHistory.Remove(oldSample);
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
            _lastMonthByDay = _monthByDay.DeepClone();
            _month.Reset();
            _monthByDay.Reset();
            _monthNonZero = TimeSpan.Zero;
        }

        private void ResetYearValues()
        {
            _lastYear = _year.DeepClone();
            _lastYearByDay = _yearByDay.DeepClone();
            _year.Reset();
            _yearByDay.Reset();
            _yearNonZero = TimeSpan.Zero;
        }

        public TBase Latest { get; private set; }

        public TBase OneHourMaximum => _oneHour.Maximum;
        public TBase ThreeHourMaximum => _threeHours.Maximum;
        public DateTime OneHourMaximumTime => _oneHour.MaximumTime;
        public DateTime ThreeHourMaximumTime => _threeHours.MaximumTime;

        public TBase OneHourMinimum => _oneHour.Minimum;
        public TBase ThreeHourMinimum => _threeHours.Minimum;
        public DateTime OneHourMinimumTime => _oneHour.MaximumTime;
        public DateTime ThreeHourMinimumTime => _threeHours.MaximumTime;

        public TBase OneHourChange => _oneHour.Change;
        public TBase ThreeHourChange => _threeHours.Change;
        public TBase OneHourTotal => _oneHour.Total;
        public TBase ThreeHourTotal => _threeHours.Total;

        public TBase DayMaximum => _day.Maximum;
        public TBase DayMinimum => _day.Minimum;
        public DateTime DayMaximumTime => _day.MaximumTime;
        public DateTime DayMinimumTime => _day.MinimumTime;

        public TBase DayAverage => _day.Average;
        public TBase DayRange => UnitTools.Subtract<TBase, TUnitType>(_day.Maximum,_day.Minimum);

        public TBase MonthMaximum => _month.Maximum;
        public TBase MonthMinimum => _month.Minimum;
        public DateTime MonthMaximumTime => _month.MaximumTime;
        public DateTime MonthMinimumTime => _month.MinimumTime;
        public TBase MonthLowestMaximum => _monthByDay.LowestMaximum;
        public TBase MonthHighestMinimum => _monthByDay.HighestMinimum;
        public DateTime MonthLowestMaximumDay => _monthByDay.LowestMaximumDay;
        public DateTime MonthHighestMinimumDay => _monthByDay.HighestMinimumDay;

        public TBase MonthAverage => _month.Average;
        public TBase MonthRange => UnitTools.Subtract<TBase, TUnitType>(_month.Maximum, _month.Minimum);

        public TBase YearMaximum => _year.Maximum;
        public TBase YearMinimum => _year.Minimum;
        public DateTime YearMaximumTime => _year.MaximumTime;
        public DateTime YearMinimumTime => _year.MinimumTime;
        public TBase YearRange => UnitTools.Subtract<TBase, TUnitType>(_year.Maximum, _year.Minimum);
        public TBase YearAverage => _year.Average;

        public TBase YearLowestMaximum => _yearByDay.LowestMaximum;
        public TBase YearHighestMinimum => _yearByDay.HighestMinimum;
        public DateTime YearLowestMaximumDay => _yearByDay.LowestMaximumDay;
        public DateTime YearHighestMinimumDay => _yearByDay.HighestMinimumDay;

        public TBase RecordMaximum => _allTime.Maximum;
        public TBase RecordMinimum => _allTime.Minimum;
        public DateTime RecordMaximumTime => _allTime.MaximumTime;
        public DateTime RecordMinimumTime => _allTime.MinimumTime;
        public bool RecordNow => _allTime.Maximum.CompareTo(Latest) == 0 ||
                                 _allTime.Minimum.CompareTo(Latest) == 0;
        public bool RecordLastHour => ((LastSample - _allTime.MaximumTime) < TimeSpan.FromHours(1)) ||
                                      ((LastSample - _allTime.MinimumTime) < TimeSpan.FromHours(1));

        public TBase RecordLowestMaximum => _allTimeByDay.LowestMaximum;
        public TBase RecordHighestMinimum => _allTimeByDay.HighestMinimum;
        public DateTime RecordLowestMaximumDay => _allTimeByDay.LowestMaximumDay;
        public DateTime RecordHighestMinimumDay => _allTimeByDay.HighestMinimumDay;

        public TBase Last24hMaximum => _24Hours.Maximum;
        public DateTime Last24hMaximumTime => _24Hours.MaximumTime;
        public TBase Last24hMinimum => _24Hours.Minimum;
        public DateTime Last24hMinimumTime => _24Hours.MinimumTime;
        public TBase Last24hTotal => _24Hours.Total;
        public TBase Last24hChange => _24Hours.Change;

        public TBase YearMaximumDayTotal => _yearByDay.HighestTotal;
        public TBase YearMinimumDayTotal => _yearByDay.LowestTotal;
        public TBase RecordMaximumDayTotal => _allTimeByDay.HighestTotal;
        public TBase RecordMinimumDayTotal => _allTimeByDay.LowestTotal;
        public DateTime YearMaximumDay => _yearByDay.HighestTotalDay;
        public DateTime YearMinimumDay => _yearByDay.LowestTotalDay;
        public DateTime RecordMaximumDay => _allTimeByDay.HighestTotalDay;
        public DateTime RecordMinimumDay => _allTimeByDay.LowestTotalDay;
        public TBase YearMaximumDayRange => _yearByDay.HighestRange;
        public TBase YearMinimumDayRange => _yearByDay.LowestRange;
        public TBase RecordMaximumDayRange => _allTimeByDay.HighestRange;
        public TBase RecordMinimumDayRange => _allTimeByDay.LowestRange;
        public DateTime YearMaximumDayRangeDay => _yearByDay.HighestRangeDay;
        public DateTime YearMinimumDayRangeDay => _yearByDay.LowestRangeDay;
        public DateTime RecordMaximumDayRangeDay => _allTimeByDay.HighestRangeDay;
        public DateTime RecordMinimumDayRangeDay => _allTimeByDay.LowestRangeDay;

        public TBase DayTotal => _day.Total;
        public TBase MonthTotal => _month.Total;
        public TBase YearTotal => _year.Total;

        public TimeSpan DayNonZero => _dayNonZero;
        public TimeSpan MonthNonZero => _monthNonZero;
        public TimeSpan YearNonZero => _yearNonZero;

        public IRecords<TBase>[] ByMonth => _monthRecords;
        public IRecords<TBase> CurrentMonth => _monthRecords[LastSample.Month - 1];

        public Dictionary<DateTime, TBase> ValueHistory => _sampleHistory;

        public void AddBooleanStatistics(IDayBooleanStatistic statistic)
        {
            _booleanStatistics.Add(statistic);
        }

        public Dictionary<DateTime, double> ValueHistoryAs(TUnitType unit)
        {
            return _sampleHistory.ToDictionary(x => x.Key, x => x.Value.As(unit));
        }

    }
}
