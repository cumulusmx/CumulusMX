using Antlr4.StringTemplate;
using CultureInfo = System.Globalization.CultureInfo;

namespace CumulusMX.Common.StringTemplate
{
        /** Based on StringTemplate4 NumberRendered class, this takes any valid format string for string.Format
         *  for formatting. Also takes a default value on construction - which will be used if no format parameter
         *  is specified on the individual template entries.
         */

        public class DefaultNumberRenderer : IAttributeRenderer
        {
            private readonly string _defaultFormat;

            public DefaultNumberRenderer(string defaultFormat)
            {
                _defaultFormat = defaultFormat;
            }

            public virtual string ToString(object o, string formatString, CultureInfo culture)
            {
                // o will be instanceof Number
                if (formatString == null)
                    return string.Format(culture, _defaultFormat, o);

            return string.Format(culture, formatString, o);
            }
        }
    }

