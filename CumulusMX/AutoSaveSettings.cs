using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;

namespace CumulusMX
{
    public class AutoSaveSettings : DataReporterSettingsGeneric
    {
        public AutoSaveSettings(IConfigurationProvider settings) : base(settings)
        {
            SectionName = "AutoSave";
        }
    }
}
