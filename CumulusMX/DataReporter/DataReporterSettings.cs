using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Configuration;
using CumulusMX.Extensions.DataReporter;

namespace CumulusMX.DataReporter
{
    internal class DataReporterSettings : DataReporterSettingsBase
    {
        private readonly IniFile _sourceFile;
        private readonly string _sectionName;

        internal DataReporterSettings(IniFile sourceFile, string sectionName)
        {
            _sourceFile = sourceFile;
            _sectionName = sectionName;
        }

        public int GetValue(string key, int defaultValue)
        {
            return _sourceFile.GetValue(_sectionName, key, defaultValue);
        }

        public string GetValue(string key, string defaultValue)
        {
            return _sourceFile.GetValue(_sectionName, key, defaultValue);
        }

        public double GetValue(string key, double defaultValue)
        {
            return _sourceFile.GetValue(_sectionName, key, defaultValue);
        }

        public byte[] GetValue(string key, byte[] defaultValue)
        {
            return _sourceFile.GetValue(_sectionName, key, defaultValue);
        }
    }
}
