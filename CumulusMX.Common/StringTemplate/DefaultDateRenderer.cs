using System;
using System.Globalization;
using Antlr4.StringTemplate;
using Autofac;
using CumulusMX.Extensions;
using CultureInfo = System.Globalization.CultureInfo;

namespace CumulusMX.Common.StringTemplate
{
    /** Based on StringTemplate4 NumberRendered class, this takes any valid format string for string.Format
     *  for formatting. Also takes a default value on construction - which will be used if no format parameter
     *  is specified on the individual template entries.
     */

    public class DefaultDateRenderer : IAttributeRenderer
    {
        private readonly string _defaultFormat;

        public DefaultDateRenderer() : this(GetDefaultFormat())
        {
            
        }

        private static string GetDefaultFormat()
        {
            var settings = AutofacWrapper.Instance.Scope.Resolve<IConfigurationProvider>();
            var dateFormat = settings?.GetValue("Defaults", "DateFormat")?.AsString;
            if (dateFormat == null)
            {
                dateFormat = CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;
            }

            return dateFormat;
        }

        public DefaultDateRenderer(string defaultFormat)
        {
            _defaultFormat = defaultFormat;
        }

        public virtual string ToString(object o, string formatString, CultureInfo culture)
        {
            // o will be instance of Date
            if (formatString == null)
                return string.Format(culture, _defaultFormat, o);

        return string.Format(culture, formatString, o);
        }
    }
}

