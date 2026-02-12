namespace CumulusMX
{
	public static class IntExtensions
	{
		/// <summary>
		/// Converts a nullable integer to its string representation, or returns a specified value if the integer is null.
		/// </summary>
		/// <param name="value">The nullable integer to convert to text. If null, the method returns the value specified by <paramref
		/// name="nullValue"/>.</param>
		/// <param name="nullValue">The string to return if <paramref name="value"/> is null. Defaults to an empty string.</param>
		/// <returns>A string containing the integer value if <paramref name="value"/> is not null; otherwise, the value of <paramref
		/// name="nullValue"/>.</returns>
		public static string ToText(this int? value, string nullValue = "")
		{
			return value.HasValue ? value.ToString() : nullValue;
		}
	}
}
