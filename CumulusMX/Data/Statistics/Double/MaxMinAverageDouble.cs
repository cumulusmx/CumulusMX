using System;
using CumulusMX.Extensions.Station;

namespace CumulusMX.Data.Statistics.Double
{
    public class MaxMinAverageDouble : IRecords<double>
    {
        private int _count = 0;
        private int _nonZero = 0;
        private double _minimum;
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

        public DateTime MinimumTime { get; private set; }
        public DateTime MaximumTime { get; private set; }
        public double Average { get; private set; }

        public double Total { get; private set; }

        public int NonZero => _nonZero;

        public MaxMinAverageDouble()
        {
            Reset();
        }

        public void Reset()
        {
            _count = 0;
            Total = 0;
            Average = 0;
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
            Average = Total / _count;
            if (Math.Abs(newValue) > 0.00001)
                _nonZero++;
        }
    }
}