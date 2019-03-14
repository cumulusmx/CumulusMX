namespace CumulusMX.Extensions.Station
{
    public interface ICalibration
    {
        double ApplyCalibration(double readingValue);
    }
}