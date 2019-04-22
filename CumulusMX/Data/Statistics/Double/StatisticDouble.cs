using System;
using System.Collections.Generic;
using System.Linq;
using CumulusMX.Extensions.Station;
using Newtonsoft.Json;
using Unosquare.Swan;

namespace CumulusMX.Data.Statistics.Double
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StatisticDouble : IStatistic<double>
    {
        private static readonly log4net.ILog log;

        private readonly DateTime EARLY_DATE;
        [JsonProperty]
        private readonly Dictionary<DateTime, double> _sampleHistory = new Dictionary<DateTime, double>();
        [JsonProperty]
        private readonly MaxMinAverageDouble _day;
        [JsonProperty]
        private MaxMinAverageDouble _yesterday;
        [JsonProperty]
        private readonly MaxMinAverageDouble _month;
        [JsonProperty]
        private MaxMinAverageDouble _lastMonth;
        [JsonProperty]
        private readonly DayStatisticDouble _monthByDay;
        [JsonProperty]
        private DayStatisticDouble _lastMonthByDay;
        [JsonProperty]
        private readonly MaxMinAverageDouble _year;
        [JsonProperty]
        private MaxMinAverageDouble _lastYear;
        [JsonProperty]
        private readonly DayStatisticDouble _yearByDay;
        [JsonProperty]
        private DayStatisticDouble _lastYearByDay;
        [JsonProperty]
        private readonly MaxMinAverageDouble _allTime;
        [JsonProperty]
        private readonly DayStatisticDouble _allTimeByDay;
        [JsonProperty]
        private readonly IRecords<double>[] _monthRecords;

        [JsonProperty]
        private TimeSpan _dayNonZero = TimeSpan.Zero;
        [JsonProperty]
        private TimeSpan _monthNonZero = TimeSpan.Zero;
        [JsonProperty]
        private TimeSpan _yearNonZero = TimeSpan.Zero;
        [JsonProperty]
        private readonly RollingStatisticDouble _oneHour;
        [JsonProperty]
        private readonly RollingStatisticDouble _threeHours;
        [JsonProperty]
        private readonly RollingStatisticDouble _24Hours;

        [JsonProperty]
        public IRecordsAndAverage<double> Yesterday => _yesterday;
        [JsonProperty]
        public IRecordsAndAverage<double> LastMonth => _lastMonth;
        [JsonProperty]
        public IRecordsAndAverage<double> LastYear => _lastYear;

        private readonly List<IDayBooleanStatistic> _booleanStatistics;

        static StatisticDouble()
        {
            //log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }

        public StatisticDouble()
        {
            _booleanStatistics = new List<IDayBooleanStatistic>();
            // Initialise runtime constants
            EARLY_DATE = DateTime.MinValue.AddYears(1);
            _day = new MaxMinAverageDouble();
            _month = new MaxMinAverageDouble();
            _year = new MaxMinAverageDouble();
            _allTime = new MaxMinAverageDouble();
            _monthByDay = new DayStatisticDouble();
            _yearByDay = new DayStatisticDouble();
            _allTimeByDay = new DayStatisticDouble();

            _oneHour = new RollingStatisticDouble(1, _sampleHistory);
            _threeHours = new RollingStatisticDouble(3, _sampleHistory);
            _24Hours = new RollingStatisticDouble(24, _sampleHistory);

            _monthRecords = new IRecords<double>[12];
            for (int i=0;i<12;i++)
                _monthRecords[i]=new MaxMinAverageDouble();

            LastSample = EARLY_DATE; // A very old date we can still look earlier than
        }

        /// <summary>
        /// Adds a new sample to the statistics set, and updates aggregates. This will be called a lot, it needs to be fast.
        /// </summary>
        /// <param name="timestamp">The DateTime when the observation was taken.</param>
        /// <param name="sample">The observed value.</param>
        public void Add(DateTime timestamp,double sample)
        {
            if (timestamp < LastSample)
            {
                log.Warn($"Invalid attempt to load data timestamped {timestamp} when latest data is already {LastSample}");
                return;
            }

            if (timestamp.Year > LastSample.Year)
                ResetYearValues();
            if (timestamp.Year * 12 + timestamp.Month > LastSample.Year * 12 + LastSample.Month)
                ResetMonthValues();
            if (timestamp.DayOfYear != LastSample.DayOfYear)
            {
                _monthByDay.Add(_day);
                _yearByDay.Add(_day);
                _allTimeByDay.Add(_day);
                foreach (var booleanStatistic in _booleanStatistics)
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
            _monthRecords[timestamp.Month - 1].AddValue(timestamp,sample);

            _oneHour.AddValue(timestamp, sample);
            _threeHours.AddValue(timestamp, sample);
            _24Hours.AddValue(timestamp, sample);

            if (Math.Abs(sample) > 0.000001 && LastSample > EARLY_DATE)
            {
                UpdateNonZeroTimes(timestamp);
            }

            LastSample = timestamp;
        }

        [JsonProperty]
        public DateTime LastSample { get; private set; }

        private void UpdateNonZeroTimes(DateTime timestamp)
        {
            var newSpan = timestamp - LastSample;
            if (timestamp.DayOfYear == LastSample.DayOfYear)
            {
                _dayNonZero += newSpan;
                _monthNonZero += newSpan;
                _yearNonZero += newSpan;
            }
            else
            {
                _dayNonZero += timestamp.TimeOfDay;
                if (timestamp.Month == LastSample.Month)
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

        [JsonProperty]
        public double Latest { get; private set; }

        public double OneHourMaximum => _oneHour.Maximum;
        public double ThreeHourMaximum => _threeHours.Maximum;
        public DateTime OneHourMaximumTime => _oneHour.MaximumTime;
        public DateTime ThreeHourMaximumTime => _threeHours.MaximumTime;

        public double OneHourMinimum => _oneHour.Minimum;
        public double ThreeHourMinimum => _threeHours.Minimum;
        public DateTime OneHourMinimumTime => _oneHour.MinimumTime;
        public DateTime ThreeHourMinimumTime => _threeHours.MinimumTime;

        public double OneHourChange => _oneHour.Change;
        public double ThreeHourChange => _threeHours.Change;
        public double OneHourTotal => _oneHour.Total;
        public double ThreeHourTotal => _threeHours.Total;
        public double OneHourAverage => _oneHour.Average;
        public double ThreeHourAverage => _threeHours.Average;

        public double DayMaximum => _day.Maximum;
        public double DayMinimum => _day.Minimum;
        public DateTime DayMaximumTime => _day.MaximumTime;
        public DateTime DayMinimumTime => _day.MinimumTime;

        public double DayAverage => _day.Average;
        public double DayRange => _day.Maximum - _day.Minimum;

        public double MonthMaximum => _month.Maximum;
        public double MonthMinimum => _month.Minimum;
        public DateTime MonthMaximumTime => _month.MaximumTime;
        public DateTime MonthMinimumTime => _month.MinimumTime;
        public double MonthLowestMaximum => _monthByDay.LowestMaximum;
        public double MonthHighestMinimum => _monthByDay.HighestMinimum;
        public DateTime MonthLowestMaximumDay => _monthByDay.LowestMaximumDay;
        public DateTime MonthHighestMinimumDay => _monthByDay.HighestMinimumDay;

        public double MonthAverage => _month.Average;
        public double MonthRange => _month.Maximum - _month.Minimum;

        public double YearMaximum => _year.Maximum;
        public double YearMinimum => _year.Minimum;
        public DateTime YearMaximumTime => _year.MaximumTime;
        public DateTime YearMinimumTime => _year.MinimumTime;
        public double YearRange => _year.Maximum - _year.Minimum;
        public double YearAverage => _year.Average;

        public double YearLowestMaximum => _yearByDay.LowestMaximum;
        public double YearHighestMinimum => _yearByDay.HighestMinimum;
        public DateTime YearLowestMaximumDay => _yearByDay.LowestMaximumDay;
        public DateTime YearHighestMinimumDay => _yearByDay.HighestMinimumDay; 

        public double RecordMaximum => _allTime.Maximum;
        public double RecordMinimum => _allTime.Minimum;
        public DateTime RecordMaximumTime => _allTime.MaximumTime;
        public DateTime RecordMinimumTime => _allTime.MinimumTime;
        public bool RecordNow => (Math.Abs(_allTime.Maximum - Latest) < Latest * 0.001) ||
                                 (Math.Abs(_allTime.Minimum - Latest) < Latest * 0.001);
        public bool RecordLastHour => ((LastSample - _allTime.MaximumTime) < TimeSpan.FromHours(1)) || 
                                      ((LastSample - _allTime.MinimumTime) < TimeSpan.FromHours(1));

        public double RecordLowestMaximum => _allTimeByDay.LowestMaximum;
        public double RecordHighestMinimum => _allTimeByDay.HighestMinimum;
        public DateTime RecordLowestMaximumDay => _allTimeByDay.LowestMaximumDay;
        public DateTime RecordHighestMinimumDay => _allTimeByDay.HighestMinimumDay;

        public double Last24hMaximum => _24Hours.Maximum;
        public DateTime Last24hMaximumTime => _24Hours.MaximumTime;
        public double Last24hMinimum => _24Hours.Minimum;
        public DateTime Last24hMinimumTime => _24Hours.MinimumTime;
        public double Last24hTotal => _24Hours.Total;
        public double Last24hChange => _24Hours.Change;

        public double YearMaximumDayTotal => _yearByDay.HighestTotal;
        public double YearMinimumDayTotal => _yearByDay.LowestTotal;
        public double RecordMaximumDayTotal => _allTimeByDay.HighestTotal;
        public double RecordMinimumDayTotal => _allTimeByDay.LowestTotal;
        public DateTime YearMaximumDay => _yearByDay.HighestTotalDay;
        public DateTime YearMinimumDay => _yearByDay.LowestTotalDay;
        public DateTime RecordMaximumDay => _allTimeByDay.HighestTotalDay;
        public DateTime RecordMinimumDay => _allTimeByDay.LowestTotalDay;
        public double YearMaximumDayRange => _yearByDay.HighestRange;
        public double YearMinimumDayRange => _yearByDay.LowestRange;
        public double RecordMaximumDayRange => _allTimeByDay.HighestRange;
        public double RecordMinimumDayRange => _allTimeByDay.LowestRange;
        public DateTime YearMaximumDayRangeDay => _yearByDay.HighestRangeDay;
        public DateTime YearMinimumDayRangeDay => _yearByDay.LowestRangeDay;
        public DateTime RecordMaximumDayRangeDay => _allTimeByDay.HighestRangeDay;
        public DateTime RecordMinimumDayRangeDay => _allTimeByDay.LowestRangeDay;

        public double DayTotal => _day.Total;
        public double MonthTotal => _month.Total;
        public double YearTotal => _year.Total;

        public TimeSpan DayNonZero => _dayNonZero;
        public TimeSpan MonthNonZero => _monthNonZero;
        public TimeSpan YearNonZero => _yearNonZero;

        public IRecords<double>[] ByMonth => _monthRecords;
        public IRecords<double> CurrentMonth => _monthRecords[LastSample.Month - 1];

        public Dictionary<DateTime, double> ValueHistory => _sampleHistory;

        public void AddBooleanStatistics(IDayBooleanStatistic statistic)
        {
            _booleanStatistics.Add(statistic);
        }

    }
}
