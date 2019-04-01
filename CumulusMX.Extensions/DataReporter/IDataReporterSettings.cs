namespace CumulusMX.Extensions.DataReporter
{
    public interface IDataReporterSettings : ISettings
    {
        string GetValue(string key, string defaultValue);

        Setting this[string key]
        {
            get;
        }
    }
}
