using System;

namespace CumulusMX.Extensions.DataReporter
{
    public abstract class DataReporterSettingsBase : IDataReporterSettings
    {
        public abstract int GetValue(string key, int defaultValue);

        public abstract string GetValue(string key, string defaultValue);

        public abstract double GetValue(string key, double defaultValue);

        public abstract byte[] GetValue(string key, byte[] defaultValue);

        public abstract bool GetValue(string key, bool defaultValue);

        public Setting this[string key] => new Setting(GetValue(key,string.Empty));
    }
}
