using System;

namespace CumulusMX
{
	public static class DateTimeExtensions
	{
		// Convert datetime to UNIX time
		public static long ToUnixTime(this DateTime dateTime)
		{
			try
			{
				var dateTimeOffset = new DateTimeOffset(dateTime);
				return dateTimeOffset.ToUnixTimeSeconds();
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "ToUnixTime: Exception");
				return 0;
			}
		}

		// Convert datetime to UNIX time including miliseconds
		public static long ToUnixTimeMs(this DateTime dateTime)
		{
			try
			{
				var dateTimeOffset = new DateTimeOffset(dateTime);
				return dateTimeOffset.ToUnixTimeMilliseconds();
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "ToUnixTimeMs: Exception");
				return 0;
			}
		}

		// Cconvert Unix TS seconds to local time
		public static DateTime FromUnixTime(this long unixTime)
		{
			try
			{
				var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime);
				return utcTime.ToLocalTime();
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "FromUnixTime: Exception");
				return DateTime.MinValue;
			}
		}

		public static DateTime FromUnixTime(this long? unixTime)
		{
			return unixTime == null ? DateTime.MinValue : FromUnixTime(unixTime.Value);
		}

		public static DateTime FromUnixTime(this int unixTime)
		{
			try
			{
				var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime);
				return utcTime.ToLocalTime();
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "FromUnixTime: Exception");
				return DateTime.MinValue;
			}
		}

		public static DateTime FromUnixTime(this int? unixTime)
		{
			return unixTime == null ? DateTime.MinValue : FromUnixTime(unixTime.Value);
		}

		public static DateTime RoundTimeUpToInterval(this DateTime dateTime, TimeSpan intvl)
		{
			try
			{
				if (intvl <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(intvl), "Interval must be positive.");

				return new DateTime((dateTime.Ticks + intvl.Ticks - 1) / intvl.Ticks * intvl.Ticks, dateTime.Kind);
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "RoundTimeUpToInterval: Exception");
				return dateTime;
			}
		}

		public static DateTime RoundTimeDownToInterval(this DateTime dateTime, TimeSpan intvl)
		{
			try
			{
				if (intvl <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(intvl), "Interval must be positive.");

				// Simple version (works for non-negative DateTime.Ticks)
				long ticks = dateTime.Ticks / intvl.Ticks * intvl.Ticks;
				return new DateTime(ticks, dateTime.Kind);
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "RoundTimeDownToInterval: Exception");
				return dateTime;
			}

		}

		public static DateTime RoundTimeToInterval(this DateTime dateTime, int intvl)
		{
			try
			{
				if (intvl <= 0)
					throw new ArgumentOutOfRangeException(nameof(intvl), "Interval must be positive.");

				int minutes = dateTime.Minute;
				int roundedMinutes = (int) (Math.Round((decimal) minutes / intvl) * intvl);
				return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, roundedMinutes, 0);
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, "RoundTimeToInterval: Exception");
				return dateTime;
			}
		}
	}
}
