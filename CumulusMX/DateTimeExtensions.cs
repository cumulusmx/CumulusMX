using System;

public static class DateTimeExtensions
{
	// Convert datetime to UNIX time
	public static long ToUnixTime(this DateTime dateTime)
	{
		var dateTimeOffset = new DateTimeOffset(dateTime);
		return dateTimeOffset.ToUnixTimeSeconds();
	}

	// Convert datetime to UNIX time including miliseconds
	public static long ToUnixTimeMs(this DateTime dateTime)
	{
		var dateTimeOffset = new DateTimeOffset(dateTime);
		return dateTimeOffset.ToUnixTimeMilliseconds();
	}

	// Cconvert Unix TS seconds to local time
	public static DateTime FromUnixTime(this long unixTime)
	{
		var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime);
		return utcTime.ToLocalTime();
	}

	public static DateTime FromUnixTime(this long? unixTime)
	{
		if (unixTime == null)
			return DateTime.MinValue;

		var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime.Value);
		return utcTime.ToLocalTime();
	}

	public static DateTime FromUnixTime(this int unixTime)
	{
		var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime);
		return utcTime.ToLocalTime();
	}

	public static DateTime FromUnixTime(this int? unixTime)
	{
		if (unixTime == null)
			return DateTime.MinValue;

		var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime.Value);
		return utcTime.ToLocalTime();
	}

	public static DateTime RoundTimeUpToInterval(this DateTime dateTime, TimeSpan intvl)
	{
		return new DateTime((dateTime.Ticks + intvl.Ticks - 1) / intvl.Ticks * intvl.Ticks, dateTime.Kind);
	}

	public static DateTime RoundTimeDownToInterval(this DateTime dateTime, TimeSpan intvl)
	{
		if (intvl <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(intvl), "Interval must be positive.");

		// Simple version (works for non-negative DateTime.Ticks)
		long ticks = dateTime.Ticks / intvl.Ticks * intvl.Ticks;
		return new DateTime(ticks, dateTime.Kind);
	}

	public static DateTime RoundTimeToInterval(this DateTime dateTime, int intvl)
	{
		int minutes = dateTime.Minute;
		int roundedMinutes = (int) (Math.Round((decimal) minutes / intvl) * intvl);
		return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, roundedMinutes, 0);
	}
}
