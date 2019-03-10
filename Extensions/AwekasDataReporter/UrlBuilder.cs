using System;
using System.Text;

namespace AwekasDataReporter
{
    internal class UrlBuilder
    {
        private readonly StringBuilder _builder;
        private readonly string _separator;

        public int DefaultDecimals { get; set; } = 0;

        public string DefaultDateFormat { get; set; } = "yyyy-MM-dd";

        public UrlBuilder(string baseUrl, string separator)
        {
            _builder = new StringBuilder(baseUrl);
            _separator = separator;
        }

        public void Append(string newString)
        {
            _builder.Append(newString);
            _builder.Append(_separator);
        }

        public void Append()
        {
            Append(string.Empty);
        }

        public void Append(double newValue,int decimalPlaces)
        {
            Append(newValue.ToString($"F{decimalPlaces}"));
        }

        public void Append(double newValue)
        {
            Append(newValue,DefaultDecimals);
        }

        public void Append(DateTime newDateTime, string dateFormat)
        {
            Append(newDateTime.ToString(dateFormat));
        }

        public void Append(DateTime newDateTime)
        {
            Append(newDateTime, DefaultDateFormat);
        }
    }
}