using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
                int value;
                if (int.TryParse(_value, out value)) return (value != 0);
                return default(bool);
            }
        }

        public int AsInt
        {
            get
            {
                int value;
                if (int.TryParse(_value, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
                return default(int);
            }
        }

        public double AsDouble
        {
            get
            {
                double value;
                if (double.TryParse(_value, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    return value;
                return default(double);
            }
        }

        public DateTime AsDateTime
        {
            get
            {
                DateTime value;
                if (DateTime.TryParse(_value, out value)) return value;
                return default(DateTime);
            }
        }
    }
}
