using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;

namespace TwitterDataReporter
{
    public class TwitterReporterSettings : DataReporterSettingsGeneric
    {
        public TwitterReporterSettings(IConfigurationProvider settings) : base(settings)
        {
            SectionName = "Twitter";
        }

        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string TemplateFilename { get; set; }
        public bool SendLocation { get; set; }
    }
}