namespace CumulusMX.Extensions.Station
{
    public class QuadraticCalibration : ICalibration
    {
        private readonly double _squaredScale;
        private readonly double _linearScale;
        private readonly double _offset;

        public QuadraticCalibration(double squaredScale, double linearScale, double offset)
        {
            _squaredScale = squaredScale;
            _linearScale = linearScale;
            _offset = offset;
        }
        public double ApplyCalibration(double readingValue)
        {
            return readingValue * readingValue * _squaredScale + readingValue * _linearScale + _offset;
        }
    }
}
