using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CumulusMX.Data.Statistics.Double
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RollingStatisticDouble
    {
        [JsonProperty]
        private readonly int _rollingPeriod;
        private readonly Dictionary<DateTime, double> _sampleHistory;
        [JsonProperty]
        public double Total { get; private set; }
        [JsonProperty]
        public double Minimum { get; private set; }
        [JsonProperty]
        public DateTime MinimumTime { get; private set; }
        [JsonProperty]
        public double Maximum { get; private set; }
        [JsonProperty]
        public DateTime MaximumTime { get; private set; }
        [JsonProperty]
        public double Change { get; private set; }

        public double Average => Total / _sampleCount;

        [JsonProperty]
        public DateTime LastSample { get; private set; }

        [JsonProperty]
        private int _sampleCount;

        public RollingStatisticDouble(int rollingPeriod, Dictionary<DateTime, double> sampleHistory)
        {
            _rollingPeriod = rollingPeriod;
            _sampleHistory = sampleHistory;
            LastSample = _sampleHistory.Any() ? _sampleHistory.Keys.Max() : DateTime.Now;
            Maximum = double.MinValue;
            MaximumTime = DateTime.Now;
            Minimum = double.MaxValue;
            MinimumTime = DateTime.Now;
            _sampleCount = 0;
        }

        public void AddValue(in DateTime timestamp, double sample)
        {
            UpdateTotal(timestamp,sample);
            UpdateExtremaAndChange(timestamp,sample);
            LastSample = timestamp;
        }

        private void UpdateTotal(DateTime timestamp, double sample)
        {
            // Update rolling 24 hour total
            var rolledOff = _sampleHistory
                .Where(x => x.Key >= LastSample.AddHours(-1* _rollingPeriod) && x.Key < timestamp.AddHours(-1 * _rollingPeriod));

            foreach (var oldValue in rolledOff)
            {
                Total -= oldValue.Value;
                _sampleCount--;
            }

            Total += sample;
            _sampleCount++;
        }


        private void UpdateExtremaAndChange(DateTime thisSample, double sample)
        {
            var oldSamples = _sampleHistory.Where(x => x.Key >= thisSample.AddHours(-1 * _rollingPeriod)).OrderBy(x => x.Key);
            if (!oldSamples.Any())
                return;

            Change = sample - oldSamples.First().Value;

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