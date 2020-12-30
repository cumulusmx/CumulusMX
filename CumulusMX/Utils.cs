using System;
using Unosquare.Swan;

namespace CumulusMX
{
	internal class Utils
	{
		public static DateTime FromUnixTime(long unixTime)
		{
			// WWL uses UTC ticks, convert to local time
			var utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
			return utcTime.ToLocalTime();
		}

		public static int ToUnixTime(DateTime dateTime)
		{
			return (int)dateTime.ToUniversalTime().ToUnixEpochDate();
		}
	}
}
