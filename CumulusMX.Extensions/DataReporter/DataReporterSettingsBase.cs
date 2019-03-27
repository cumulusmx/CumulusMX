namespace CumulusMX.Extensions.DataReporter
{
    public abstract class DataReporterSettingsBase : IDataReporterSettings
    {
        private bool? _isEnabled = null;
        private int? _reportInterval = null;

        public DataReporterSettingsBase()
        {

        }

        public abstract string GetValue(string key, string defaultValue);

        public Setting this[string key] => new Setting(GetValue(key,string.Empty));

        public bool IsEnabled
        {
            get
            {
                if (_isEnabled == null )
                {
                    _isEnabled = bool.TryParse(GetValue("Enabled", "false"), out bool temp) ? temp : false;
                }
                return (bool) _isEnabled;
            }
            set => _isEnabled = value;
        }

        public int ReportInterval
        {
            get
            {
                if (_reportInterval == null)
                {
                    _reportInterval = int.TryParse(GetValue("ReportInterval", "900"),out int temp2) ? temp2 : 900;
                }
                return (int)_reportInterval;
            }
            set => _reportInterval = value;
        }
    }
}
