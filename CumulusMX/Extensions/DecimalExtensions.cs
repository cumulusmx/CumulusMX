using System.Globalization;

namespace CumulusMX
{
	public static class DecimalExtensions
	{
		/// <summary>
		/// Formats the specified decimal value as a string using the provided fixed-point format and invariant culture.
		/// </summary>
		/// <remarks>This method ensures that negative zero values are formatted as positive zero when using
		/// fixed-point formats (e.g., "F1", "F2"). The formatting uses invariant culture to ensure consistent results
		/// regardless of the current culture settings.</remarks>
		/// <param name="value">The decimal value to format.</param>
		/// <param name="format">A standard numeric format string that specifies the number of decimal places, such as "F2" for two decimal places.
		/// Must be a valid fixed-point format.</param>
		/// <returns>A string representation of the decimal value formatted according to the specified fixed-point format and using
		/// invariant culture. If the value is negative zero, returns the positive zero representation.</returns>
		public static string ToFixed(this decimal value, string format)
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

		/// <summary>
		/// Formats the specified nullable decimal value as a string using the provided format, or returns a default value if
		/// the input is null.
		/// </summary>
		/// <remarks>This method ensures that negative zero values are formatted as positive zero when using
		/// fixed-point formats (e.g., "F1", "F2"). The formatting uses invariant culture to ensure consistent results
		/// regardless of the current culture settings.</remarks>
		/// <param name="value">The nullable decimal value to format. If null, the method returns the value specified by the nullValue parameter.</param>
		/// <param name="format">A standard or custom numeric format string that defines the format of the returned value.</param>
		/// <param name="nullValue">The string to return if value is null. The default is an empty string.</param>
		/// <returns>A string representation of the decimal value formatted according to the specified format, or the nullValue string
		/// if value is null.</returns>
		public static string ToFixed(this decimal? value, string format, string nullValue = "")
		{
			if (value == null)
				return nullValue;

			return value.Value.ToFixed(format);
		}

		/// <summary>
		/// Converts the specified decimal value to its string representation using the given numeric format and the current
		/// culture's formatting conventions. Ensures that negative zero values are displayed as positive zero for fixed-point
		/// formats.
		/// </summary>
		/// <remarks>This method is intended for use with fixed-point format strings such as "F1", "F2", etc. When the
		/// value is negative zero and a fixed-point format is specified, the result will be the positive zero representation
		/// appropriate for the current culture. For other formats, the method behaves like
		/// decimal.ToString(format).</remarks>
		/// <param name="value">The decimal value to convert to a string.</param>
		/// <param name="format">A standard or custom numeric format string that defines the format of the returned value. For example, "F2" for
		/// fixed-point with two decimal places.</param>
		/// <returns>A string representation of the decimal value formatted according to the specified format and the current culture.
		/// For fixed-point formats, negative zero is displayed as positive zero.</returns>
		public static string ToFixedLocal(this decimal value, string format)
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

		/// <summary>
		/// Formats the specified nullable decimal value as a string using the given numeric format and the current culture.
		/// Returns a custom string if the value is null.
		/// </summary>
		/// <remarks>This method uses the current thread's culture settings when formatting the decimal value. To
		/// specify a different culture, use an overload that accepts an <see cref="IFormatProvider"/>. When the
		/// value is negative zero and a fixed-point format is specified, the result will be the positive zero representation
		/// appropriate for the current culture.</remarks>
		/// <param name="value">The nullable decimal value to format. If null, the method returns the value specified by <paramref
		/// name="nullValue"/>.</param>
		/// <param name="format">A standard or custom numeric format string that defines the format of the returned value.</param>
		/// <param name="nullValue">The string to return if <paramref name="value"/> is null. The default is an empty string.</param>
		/// <returns>A string representation of the decimal value formatted according to the specified format and current culture, or
		/// the <paramref name="nullValue"/> if the value is null.</returns>
		public static string ToFixedLocal(this decimal? value, string format, string nullValue = "")
		{
			if (value == null)
				return nullValue;

			return value.Value.ToFixedLocal(format);
		}

	}
}
