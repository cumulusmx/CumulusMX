using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;

namespace AwekasDataReporter
{
    public class AwekasSettings : DataReporterSettingsGeneric
    {
        public AwekasSettings(IConfigurationProvider settings) : base(settings)
        {
            SectionName = "Awekas";
        }
    }
}
