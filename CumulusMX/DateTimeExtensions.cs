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

}
