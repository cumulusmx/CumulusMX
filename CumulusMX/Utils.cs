using System;
using System.Linq;
using System.Text.RegularExpressions;
using Unosquare.Swan;

// A rag tag of useful functions

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


		public static string ByteArrayToHexString(byte[] ba)
		{
			System.Text.StringBuilder hex = new System.Text.StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}


		public static string GetMd5String(byte[] bytes)
		{
			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				var hashBytes = md5.ComputeHash(bytes);
				return ByteArrayToHexString(hashBytes);
			}
		}

		public static string GetMd5String(string str)
		{
			return GetMd5String(System.Text.Encoding.ASCII.GetBytes(str));
		}

		public static bool ValidateIPv4(string ipString)
		{
			if (string.IsNullOrWhiteSpace(ipString))
			{
				return false;
			}

			string[] splitValues = ipString.Split('.');
			if (splitValues.Length != 4)
			{
				return false;
			}

			byte tempForParsing;

			return splitValues.All(r => byte.TryParse(r, out tempForParsing));
		}

		public static DateTime ddmmyyStrToDate(string d)
		{
			// Horrible hack, but we have localised separators, but UK sequence, so localised parsing may fail
			// Determine separators from the strings, allow for multi-byte!
			var datSep = Regex.Match(d, @"[^0-9]+").Value;

			// Converts a date string in UK order to a DateTime
			string[] date = d.Split(new string[] { datSep }, StringSplitOptions.None);

			int D = Convert.ToInt32(date[0]);
			int M = Convert.ToInt32(date[1]);
			int Y = Convert.ToInt32(date[2]);
			if (Y > 70)
			{
				Y += 1900;
			}
			else
			{
				Y += 2000;
			}

			return new DateTime(Y, M, D);
		}

		public static DateTime ddmmyyhhmmStrToDate(string d, string t)
		{
			// Horrible hack, but we have localised separators, but UK sequence, so localised parsing may fail
			// Determine separators from the strings, allow for multi-byte!
			var datSep = Regex.Match(d, @"[^0-9]+").Value;
			var timSep = Regex.Match(t, @"[^0-9]+").Value;

			// Converts a date string in UK order to a DateTime
			string[] date = d.Split(new string[] { datSep }, StringSplitOptions.None);
			string[] time = t.Split(new string[] { timSep }, StringSplitOptions.None);

			int D = Convert.ToInt32(date[0]);
			int M = Convert.ToInt32(date[1]);
			int Y = Convert.ToInt32(date[2]);

			// Double check - just in case we get a four digit year!
			if (Y < 1900)
			{
				Y += Y > 70 ? 1900 : 2000;
			}
			int h = Convert.ToInt32(time[0]);
			int m = Convert.ToInt32(time[1]);

			return new DateTime(Y, M, D, h, m, 0);
		}

		public static string GetLogFileSeparator(string line, string defSep)
		{
			// we know the dayfile and monthly log files start with
			// dd/MM/yy,NN,...
			// dd/MM/yy,hh:mm,N.N,....
			// so we just need to find the first separator after the date before a number

			var reg = Regex.Match(line, @"\d{2}[^\d]+\d{2}[^\d]+\d{2}([^\d])");
			if (reg.Success)
				return reg.Groups[1].Value;
			else
				return defSep;
		}
	}
}
