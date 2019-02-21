namespace CumulusMX.DataReporter
{
    public interface IDataReporterSettings
    {
        int GetValue(string key, int defaultValue);
        string GetValue(string key, string defaultValue);
        double GetValue(string key, double defaultValue);
        byte[] GetValue(string key, byte[] defaultValue);
    }
}
