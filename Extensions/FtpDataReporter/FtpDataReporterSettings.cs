using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;

namespace FtpDataReporter
{
    public class FtpDataReporterSettings : DataReporterSettingsGeneric
    {
        public FtpDataReporterSettings(IConfigurationProvider settings) : base(settings)
        {
            SectionName = "FtpDataReporter";
        }

        public string User { get; set; }
        public SecureString Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public bool RenameFiles { get; set; }
        public bool ActiveFtp { get; set; }
        public bool DisableEpsv { get; set; }
        public string FtpDirectory { get; set; }
        public bool IncludeGraphDataFiles { get; set; }
        public bool RealtimeEnabled { get; set; }
        public bool IncludeStandardFiles { get; set; }
        public bool DeleteBeforeUpload { get; set; }
        public bool RealtimeGauges { get; set; }
        public bool RealtimeText { get; set; }
        public string NoaaDirectory { get; set; }
        public List<UploadFileDetails> ExtraFiles { get; set; }
        public bool SaveLocal { get; internal set; }
        public string LocalSavePath { get; internal set; }
        public string ExternalProgram { get; internal set; }
        public string ExternalParams { get; internal set; }
        public string RealtimeParams { get; internal set; }
        public string RealtimeProgram { get; internal set; }
        public int RealtimeInterval { get; internal set; }
    }
}
