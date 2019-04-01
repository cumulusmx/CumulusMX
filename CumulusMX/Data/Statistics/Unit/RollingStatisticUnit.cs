using System;
using System.Collections.Generic;
using System.Linq;
using UnitsNet;

namespace CumulusMX.Data.Statistics.Unit
{
    public class RollingStatisticUnit<TBase, TUnitType>
        where TBase : IComparable, IQuantity<TUnitType>
        where TUnitType : Enum
    {
        private readonly int _rollingPeriod;
        private readonly Dictionary<DateTime, TBase> _sampleHistory;
        private TBase _zeroValue;
        private double _total;
        private TBase _unitTotal;
        private TUnitType _firstUnit;
        private bool _totalValid;

        public TBase Total
        {
            get
            {
                if (!_totalValid)
                {
                    _unitTotal = (TBase) Activator.CreateInstance(
                        typeof(TBase),
                        _total,
                        _firstUnit
                    );
                    _totalValid = true;
                }

                return _unitTotal;
            }
        }

        public TBase Minimum { get; private set; }
        public DateTime MinimumTime { get; private set; }
        public TBase Maximum { get; private set; }
        public DateTime MaximumTime { get; private set; }
        public TBase Change { get; private set; }

        public DateTime LastSample { get; private set; }

        public RollingStatisticUnit(int rollingPeriod, Dictionary<DateTime, TBase> sampleHistory)
        {
            _firstUnit = UnitTools.FirstUnit<TUnitType>();
            _zeroValue = UnitTools.ZeroQuantity<TBase,TUnitType>();
            _rollingPeriod = rollingPeriod;
            _sampleHistory = sampleHistory;
            LastSample = _sampleHistory.Any() ? _sampleHistory.Keys.Max() : DateTime.Now;
            Maximum = _zeroValue;
            MaximumTime = DateTime.Now;
            Minimum = _zeroValue;
            MinimumTime = DateTime.Now;
            _unitTotal = _zeroValue;
            _total = 0;
            _totalValid = true;
        }

        public void AddValue(in DateTime timestamp, TBase sample)
        {
            UpdateTotal(timestamp,sample);
            UpdateExtremaAndChange(timestamp,sample);
            LastSample = timestamp;
        }

        private void UpdateTotal(DateTime timestamp, TBase sample)
        {
            // Update rolling 24 hour total
            var rolledOff = _sampleHistory
                .Where(x => x.Key >= LastSample.AddHours(-1* _rollingPeriod) && x.Key < timestamp.AddHours(-1 * _rollingPeriod));

            foreach (var oldValue in rolledOff)
            {
                _total -= oldValue.Value.As(_firstUnit);
            }

            _total += sample.As(_firstUnit);
            _totalValid = false;
        }

        private void UpdateExtremaAndChange(DateTime thisSample, TBase sample)
        {
            var oldSamples = _sampleHistory.Where(x => x.Key >= thisSample.AddHours(-1 * _rollingPeriod)).OrderBy(x => x.Key);
            if (!oldSamples.Any())
                return;

            Change = UnitTools.Subtract<TBase, TUnitType>(sample, oldSamples.First().Value);

            var rolledOff = _sampleHistory
                .Where(x => x.Key > LastSample.AddHours(-1 * _rollingPeriod) && x.Key <= thisSample.AddHours(-1 * _rollingPeriod));

            if (sample.CompareTo(Maximum) > 0)
            {
                Maximum = sample;
                MaximumTime = LastSample;
            }
            else
            {
                bool maxInvalid = false;
                foreach (var oldValue in rolledOff)
                {
                    if (oldValue.Value.Equals(Maximum))
                        maxInvalid = true;
                }

                if (maxInvalid)
                {
                    var historyWindow = _sampleHistory
                        .Where(x => x.Key > thisSample.AddHours(-1 * _rollingPeriod) && x.Key <= thisSample);
                    Maximum = sample;
                    MaximumTime = LastSample;
                    foreach (var oldSample in historyWindow)
                    {
                        if (oldSample.Value.CompareTo(Maximum) > 0)
                        {
                            Maximum = oldSample.Value;
                            MaximumTime = oldSample.Key;
                        }
                    }
                }
            }

            if (sample.CompareTo(Minimum) < 0)
            {
                Minimum = sample;
                MinimumTime = LastSample;
            }
            else
            {
                bool minInvalid = false;
                foreach (var oldValue in rolledOff)
                {
                    if (oldValue.Value.Equals(Minimum))
                        minInvalid = true;
                }

                if (minInvalid)
                {
                    var historyWindow = _sampleHistory
                        .Where(x => x.Key > thisSample.AddHours(-1 * _rollingPeriod) && x.Key <= thisSample);
                    Minimum = sample;
                    MinimumTime = LastSample;
                    foreach (var oldSample in historyWindow)
                    {
                        if (oldSample.Value.CompareTo(Minimum) < 0)
                        {
                            Minimum = oldSample.Value;
                            MinimumTime = oldSample.Key;
                        }
                    }
                }
            }
        }
    }
}