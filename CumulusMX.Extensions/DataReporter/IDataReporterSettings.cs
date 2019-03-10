namespace CumulusMX.Extensions.DataReporter
{
    public interface IDataReporterSettings : ISettings
    {
        int GetValue(string key, int defaultValue);
        string GetValue(string key, string defaultValue);
        double GetValue(string key, double defaultValue);
        byte[] GetValue(string key, byte[] defaultValue);
        bool GetValue(string key, bool defaultValue);

        Setting this[string key]
        {
            get;
        }
    }
}
