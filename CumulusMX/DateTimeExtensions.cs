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

}
