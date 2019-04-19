using System;
using Antlr4.StringTemplate;
using Autofac;
using CumulusMX.Extensions;
using UnitsNet;
using CultureInfo = System.Globalization.CultureInfo;

namespace CumulusMX.Common.StringTemplate
{
    /** Based on StringTemplate4 NumberRendered class, this takes any valid format string for string.Format
     *  for formatting. Also takes a default value on construction - which will be used if no format parameter
     *  is specified on the individual template entries.
     */

    public abstract class DefaultUnitRenderer<TBase, TUnitType> : IAttributeRenderer
        where TUnitType : struct, Enum where TBase : IComparable, IQuantity<TUnitType>
    {
        private readonly string _defaultFormat;
        private readonly TUnitType _defaultUnit;

        protected static ValueTuple<string,TUnitType> GetDefaults(string globalDefaultFormat,TUnitType globalDefaultUnit, string formatSetting, string unitSetting)
        {
            var settings = AutofacWrapper.Instance.Scope.Resolve<IConfigurationProvider>();
            var temperatureFormat = settings?.GetValue("Defaults", formatSetting)?.AsString;
            if (temperatureFormat == null)
            {
                temperatureFormat = globalDefaultFormat;
            }

            var TUnitTypeString = settings?.GetValue("Defaults", unitSetting)?.AsString;
            TUnitType unitType;
            if (!Enum.TryParse(TUnitTypeString, true, out unitType))
                unitType = globalDefaultUnit;

            return (temperatureFormat,unitType);
        }

        public DefaultUnitRenderer(ValueTuple<string, TUnitType> defaults) : this (defaults.Item1,defaults.Item2)
        { }

        public DefaultUnitRenderer(string defaultFormat,TUnitType defaultUnit)
        {
            _defaultFormat = defaultFormat;
            _defaultUnit = defaultUnit;
        }

        public virtual string ToString(object o, string formatString, CultureInfo culture)
        {
            string[] tags;
            // o will be instance of Temperature
            var unitValue = (TBase)o;

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
                if (Enum.TryParse<TUnitType>(tags[1], true, out TUnitType returnUnit))
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

