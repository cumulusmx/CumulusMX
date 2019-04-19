using System;
using CumulusMX.Extensions.Station;
using Newtonsoft.Json;

namespace CumulusMX.Data.Statistics.Double
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MaxMinAverageDouble : IRecordsAndAverage<double>
    {
        [JsonProperty]
        private int _count = 0;
        [JsonProperty]
        private int _nonZero = 0;
        [JsonProperty]
        private double _minimum;
        [JsonProperty]
        private double _maximum;

        public double Minimum
        {
            get => _count == 0 ? 0.0 : _minimum;
            private set => _minimum = value;
        }

        public double Maximum
        {
            get => _count == 0 ? 0.0 : _maximum;
            private set => _maximum = value;
        }

        [JsonProperty]
        public DateTime MinimumTime { get; private set; }
        [JsonProperty]
        public DateTime MaximumTime { get; private set; }
        
        public double Average => _count == 0 ? 0.0 : Total / _count;
        [JsonProperty]
        public double Total { get; private set; }

        [JsonProperty]
        public int NonZero => _nonZero;

        public MaxMinAverageDouble()
        {
            Reset();
        }

        public void Reset()
        {
            _count = 0;
            Total = 0;
            _nonZero = 0;
            MaximumTime = DateTime.Now;
            MinimumTime = DateTime.Now;
        }

        public void AddValue(DateTime newTime,double newValue)
        {
            _count++;
            Total += newValue;
            if (newValue > Maximum || _count == 1)
            {
                Maximum = newValue;
                MaximumTime = newTime;
            }
            if (newValue < Minimum || _count == 1)
            {
                Minimum = newValue;
                MinimumTime = newTime;
            }

            if (Math.Abs(newValue) > 0.00001)
                _nonZero++;
        }
    }
}