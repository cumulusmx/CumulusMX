namespace CumulusMX.Data
{
    public class MaxMinAverageDouble
    {
        private int _count = 0;
        private int _nonZero = 0;

        public double Minimum { get; private set; }

        public double Maximum { get; private set; }

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
        }

        public void AddValue(double newValue)
        {
            _count++;
            Total += newValue;
            if (newValue > Maximum || _count == 1) Maximum = newValue;
            if (newValue < Minimum || _count == 1) Minimum = newValue;
            Average = Total / _count;
            if (newValue != 0.0)
                _nonZero++;
        }
    }
}