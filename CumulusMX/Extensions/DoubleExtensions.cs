using System.Globalization;

namespace CumulusMX
{
	public static class DoubleExtensions
	{
		public static string ToFixed(this double value, string format)
		{
			string s = value.ToString(format, CultureInfo.InvariantCulture);

			// Only handle formats like "F1", "F2", "F3", etc.
			if (format.Length == 2 && format[0] == 'F' && char.IsDigit(format[1]))
			{
				int decimals = format[1] - '0';

				string positiveZero = "0" + (decimals > 0 ? "." + new string('0', decimals) : "");

				if (s == "-" + positiveZero)
					return positiveZero;
			}

			return s;
		}

		public static string ToFixed(this double? value, string format, string nullValue = "")
		{
			if (value == null)
				return nullValue;

			return value.Value.ToFixed(format);
		}

		public static string ToFixedLocal(this double value, string format)
		{
			string s = value.ToString(format);

			// Only handle formats like "F1", "F2", "F3", etc.
			if (format.Length == 2 && format[0] == 'F' && char.IsDigit(format[1]))
			{
				int decimals = format[1] - '0';

				var nfi = CultureInfo.CurrentCulture.NumberFormat;

				// Build locale‑correct positive zero, e.g. "0.00" or "0,00" string positiveZero =

				string positiveZero = "0" + (decimals > 0 ? nfi.NumberDecimalSeparator + new string('0', decimals) : "");

				if (s == nfi.NegativeSign + positiveZero)
					return positiveZero;
			}

			return s;
		}

		public static string ToFixedLocal(this double? value, string format, string nullValue = "")
		{
			if (value == null)
				return nullValue;

			return value.Value.ToFixedLocal(format);
		}
	}
}
