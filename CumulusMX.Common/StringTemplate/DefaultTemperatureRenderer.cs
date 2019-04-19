using System;
using System.Globalization;
using Antlr4.StringTemplate;
using Autofac;
using CumulusMX.Extensions;
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

        public DefaultTemperatureRenderer() : this(GetDefaults())
        {
            
        }

        private static ValueTuple<string,TemperatureUnit> GetDefaults()
        {
            var settings = AutofacWrapper.Instance.Scope.Resolve<IConfigurationProvider>();
            var temperatureFormat = settings?.GetValue("Defaults", "TemperatureFormat")?.AsString;
            if (temperatureFormat == null)
            {
                temperatureFormat = CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;
            }

            var temperatureUnitString = settings?.GetValue("Defaults", "TemperatureUnit")?.AsString;
            if (!Enum.TryParse(temperatureUnitString, true, out TemperatureUnit temperatureUnit))
                temperatureUnit = TemperatureUnit.DegreeCelsius;

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
            // o will be instance of Temperature
            if (formatString == null)
                return string.Format(culture, _defaultFormat, o);

            return string.Format(culture, formatString, o);
        }
    }
}

