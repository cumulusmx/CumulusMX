using System;
using System.Globalization;
using Antlr4.StringTemplate;
using Autofac;
using CumulusMX.Extensions;
using UnitsNet;
using UnitsNet.Units;
using CultureInfo = System.Globalization.CultureInfo;

namespace CumulusMX.Common.StringTemplate
{
    /** Based on StringTemplate4 NumberRendered class, this takes any valid format string for string.Format
     *  for formatting. Also takes a default value on construction - which will be used if no format parameter
     *  is specified on the individual template entries.
     */

    public class DefaultTemperatureRenderer : IAttributeRenderer
    {
        private readonly string _defaultFormat;
        private readonly TemperatureUnit _defaultUnit;

        private const string GLOBAL_DEFAULT_FORMAT = "F1";
        private const TemperatureUnit GLOBAL_DEFAULT_UNIT = TemperatureUnit.DegreeCelsius;

        public DefaultTemperatureRenderer() : this(GetDefaults())
        {
            
        }

        private static ValueTuple<string, TemperatureUnit> GetGlobalDefaults()
        {
            return (GLOBAL_DEFAULT_FORMAT, GLOBAL_DEFAULT_UNIT);
        }

        private static ValueTuple<string,TemperatureUnit> GetDefaults()
        {
            var globalDefaults = GetGlobalDefaults();
            var settings = AutofacWrapper.Instance.Scope.Resolve<IConfigurationProvider>();
            var temperatureFormat = settings?.GetValue("Defaults", "TemperatureFormat")?.AsString;
            if (temperatureFormat == null)
            {
                temperatureFormat = globalDefaults.Item1;
            }

            var temperatureUnitString = settings?.GetValue("Defaults", "TemperatureUnit")?.AsString;
            if (!Enum.TryParse(temperatureUnitString, true, out TemperatureUnit temperatureUnit))
                temperatureUnit = globalDefaults.Item2;

            return (temperatureFormat,temperatureUnit);
        }

        public DefaultTemperatureRenderer(ValueTuple<string, TemperatureUnit> defaults) : this (defaults.Item1,defaults.Item2)
        { }

        public DefaultTemperatureRenderer(string defaultFormat,TemperatureUnit defaultUnit)
        {
            _defaultFormat = defaultFormat;
            _defaultUnit = defaultUnit;
        }

        public virtual string ToString(object o, string formatString, CultureInfo culture)
        {
            string[] tags;
            // o will be instance of Temperature
            var unitValue = (Temperature)o;

            if (formatString == null)
                return string.Format(culture, _defaultFormat, unitValue.As(_defaultUnit));

            if (formatString.Contains("|"))
                tags = formatString.Split('|');
            else
                tags = new[] {formatString};

            double value;
            if (tags.Length < 2)
                value = unitValue.As(_defaultUnit);
            else
            {
                if (Enum.TryParse(tags[1], true, out TemperatureUnit returnUnit))
                    value = unitValue.As(returnUnit);
                else
                {
                    value = unitValue.As(_defaultUnit);
                    //_log.Warning($"Unable to parse Temperature unit {tags[1]}.");
                }
            }

            if (string.IsNullOrWhiteSpace(tags[0]))
                return string.Format(culture, _defaultFormat, value);
            else
                return string.Format(culture, formatString, value);
        }
    }
}

