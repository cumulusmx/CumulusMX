namespace CumulusMX
{
	public static class IntExtensions
	{
		public static string ToText(this int? value, string nullValue = "")
		{
			return value.HasValue ? value.ToString() : nullValue;
		}
	}
}
