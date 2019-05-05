using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CumulusMX.Extensions
{
    /// <summary>
    /// A class to use accessing settings from StringTemplate4.
    /// </summary>
    public class Setting
    {
        private readonly string _value;

        public Setting(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return _value;
        }

        public string AsString => _value;

        // *** Getters for various types ***
        public bool AsBool
        {
            get
            {
                if (bool.TryParse(_value, out bool value)) return (value);
                if (int.TryParse(_value, out int valueInt)) return (valueInt != 0);
                if (_value.ToUpper() == "Y" || _value.ToUpper() == "YES") return true;
                if (_value.ToUpper() == "N" || _value.ToUpper() == "NO") return false;
                return default;
            }
        }

        public int AsInt
        {
            get
            {
                int value;
                if (int.TryParse(_value, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
                return default;
            }
        }

        public double AsDouble
        {
            get
            {
                double value;
                if (double.TryParse(_value, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    return value;
                return default;
            }
        }

        public DateTime AsDateTime
        {
            get
            {
                DateTime value;
                if (DateTime.TryParse(_value, out value)) return value;
                return default;
            }
        }

        public Dictionary<string, Setting> AsSection
        {
            get
            {
                if (IniFile != null)
                {
                    return IniFile.GetSection($"{Section}:{_value}");
                }

                var temp = JsonConvert.DeserializeObject<Dictionary<string, string>>(_value);
                return temp.ToDictionary(x => x.Key, x => new Setting(x.Value));
            }
        }

        public List<string> AsList
        {
            get
            {
                if (IniFile != null)
                {
                    return _value.Split(':').ToList();
                }

                return JsonConvert.DeserializeObject<List<string>>(_value);
            }

        }

        public string Section { get; set; }
        public IConfigurationProvider IniFile { get; set; }

        public object AsType(Type outputType)
        {
            outputType = Nullable.GetUnderlyingType(outputType) ?? outputType;

            if (outputType == typeof(string))
                return AsString;

            if (outputType == typeof(bool))
                return AsBool;

            if (outputType == typeof(double))
                return AsDouble;

            if (outputType == typeof(int))
                return AsInt;

            if (outputType == typeof(DateTime))
                return AsDateTime;

            return null;
        }
    }
}
