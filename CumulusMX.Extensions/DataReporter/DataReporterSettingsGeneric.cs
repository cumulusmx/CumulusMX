using System;

namespace CumulusMX.Extensions.DataReporter
{
    public class DataReporterSettingsGeneric : DataReporterSettingsBase
    {
        private readonly IConfigurationProvider _baseConfiguration;

        public DataReporterSettingsGeneric(IConfigurationProvider baseConfiguration)
        {
            _baseConfiguration = baseConfiguration;
        }

        public string SectionName { get; set; } = "";

        public new Setting this[string key]
        {
            get => _baseConfiguration.GetValue(SectionName, key);
            set => _baseConfiguration.SetValue(SectionName,key,value);
        }

        public override string GetValue(string key, string defaultValue)
        {
            return _baseConfiguration.GetValue(SectionName, key).ToString();
        }
    }
}
