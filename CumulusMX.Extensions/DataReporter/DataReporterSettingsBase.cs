using System;

namespace CumulusMX.Extensions.DataReporter
{
    public class DataReporterSettingsBase : IDataReporterSettings
    {
        public int GetValue(string key, int defaultValue)
        {
            throw new System.NotImplementedException();
        }

        public string GetValue(string key, string defaultValue)
        {
            throw new System.NotImplementedException();
        }

        public double GetValue(string key, double defaultValue)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetValue(string key, byte[] defaultValue)
        {
            throw new System.NotImplementedException();
        }

        public bool GetValue(string key, bool defaultValue)
        {
            throw new System.NotImplementedException();
        }

        public Setting this[string key] => new Setting(GetValue(key,string.Empty));
    }
}
