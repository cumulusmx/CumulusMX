﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ServiceStack;

using Swan;


// A rag tag of useful functions

namespace CumulusMX
{
	internal static partial class Utils
	{
		public static DateTime FromUnixTime(long unixTime)
		{
			// Cconvert Unix TS seconds to local time
			var utcTime = DateTime.UnixEpoch.AddSeconds(unixTime);
			return utcTime.ToLocalTime();
		}

		public static long ToUnixTime(DateTime dateTime)
		{
			return dateTime.ToUnixEpochDate();
		}

		public static long ToJsTime(DateTime dateTime)
		{
			return dateTime.ToUnixEpochDate() * 1000;
		}

		// SPECIAL Unix TS for graphs. It looks like a Unix TS, but is the local time as if it were UTC.
		// Used for the graph data, as HighCharts is going to display UTC date/times to be consistent across TZ
		public static long ToPseudoUnixTime(DateTime timestamp)
		{
			return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToUnixEpochDate();
		}

		// SPECIAL JS TS for graphs. It looks like a Unix TS, but is the local time as if it were UTC.
		// Used for the graph data, as HighCharts is going to display UTC date/times to be consistent across TZ
		public static long ToPseudoJSTime(DateTime timestamp)
		{
			return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToUnixEpochDate() * 1000;
		}


		public static DateTime RoundTimeUpToInterval(DateTime dateTime, TimeSpan intvl)
		{
			return new DateTime((dateTime.Ticks + intvl.Ticks - 1) / intvl.Ticks * intvl.Ticks, dateTime.Kind);
		}

		public static DateTime RoundTimeToInterval(DateTime dateTime, int intvl)
		{
			int minutes = dateTime.Minute;
			int roundedMinutes = (int)(Math.Round((decimal) minutes / intvl) * intvl);
			return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, roundedMinutes, 0);
		}

		public static string ByteArrayToHexString(byte[] ba)
		{
			System.Text.StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}

		public static byte[] HexStringToByteArray(string hexString)
		{
			if (hexString.Length % 2 != 0)
			{
				throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
			}

			byte[] data = new byte[hexString.Length / 2];
			for (int index = 0; index < data.Length; index++)
			{
				string byteValue = hexString.Substring(index * 2, 2);
				data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}

			return data;
		}

		public static string GetMd5String(byte[] bytes)
		{
			var hashBytes = MD5.HashData(bytes);
			return ByteArrayToHexString(hashBytes);
		}

		public static string GetMd5String(string str)
		{
			return GetMd5String(System.Text.Encoding.ASCII.GetBytes(str));
		}

		public static string GetSHA256Hash(string key, string data)
		{
			byte[] hashValue;
			// Initialize the keyed hash object.
			using HMACSHA256 hmac = new HMACSHA256(key.ToAsciiBytes());
			// convert string to stream
			byte[] byteArray = Encoding.UTF8.GetBytes(data);
			using (MemoryStream stream = new MemoryStream(byteArray))
			{
				// Compute the hash of the input string.
				hashValue = hmac.ComputeHash(stream);
			}
			return BitConverter.ToString(hashValue).Replace("-", string.Empty).ToLower();
		}

		public static bool ValidateIPv4(string ipString)
		{
			if (string.IsNullOrWhiteSpace(ipString) || ipString == "0.0.0.0")
			{
				return false;
			}

			string[] splitValues = ipString.Split('.');
			if (splitValues.Length != 4)
			{
				return false;
			}

			byte tempForParsing;

			return Array.TrueForAll(splitValues, r => byte.TryParse(r, out tempForParsing));
		}

		public static DateTime ddmmyyStrToDate(string d)
		{
			if (DateTime.TryParseExact(d, "dd/MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
			{
				return result;
			}
			return DateTime.MinValue;
		}

		public static DateTime ddmmyyhhmmStrToDate(string d, string t)
		{
			if (DateTime.TryParseExact(d + ' ' + t, "dd/MM/yy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
			{
				return result;
			}
			return DateTime.MinValue;
		}

		public static IPAddress GetIpWithDefaultGateway()
		{
			try
			{
				// First try and find the IPv4 address that also has the default gateway
				return NetworkInterface
					.GetAllNetworkInterfaces()
					.Where(n => n.OperationalStatus == OperationalStatus.Up)
					.Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
					.Where(n => n.GetIPProperties().GatewayAddresses.Count > 0)
					.SelectMany(n => n.GetIPProperties().UnicastAddresses)
					.Where(n => n.Address.AddressFamily == AddressFamily.InterNetwork)
					.Where(n => n.IPv4Mask.ToString() != "0.0.0.0")
					.Select(g => g.Address)
					.First();
			}
			catch
			{
				// do nothing
			}

			try
			{
				// next just return the first IPv4 address found
				var host = Dns.GetHostEntry(Dns.GetHostName());
				foreach (var ip in host.AddressList)
				{
					if (ip.AddressFamily == AddressFamily.InterNetwork)
					{
						return ip;
					}
				}
			}
			catch
			{
				// do nothing
			}

			// finally, give up and just return a 0.0.0.0 IP!
			return IPAddress.Any;
		}

		public static string ExceptionToString(Exception ex)
		{
			var sb = new StringBuilder();

			sb.AppendLine("Exception Type: " + ex.GetType().FullName);
			sb.AppendLine("Message: " + ex.Message);
			if (ex.Data.Keys.Count > 0)
			{
				foreach (var key in ex.Data.Keys)
				{
					sb.AppendLine(key.ToString() + ": " + (ex.Data[key] is null ? "null" : ex.Data[key].ToString()));
				}
			}

			if (ex.InnerException != null)
			{
				sb.AppendLine("Inner Exception... ");
				sb.AppendLine(ExceptionToString(ex.InnerException));
			}

			return sb.ToString();
		}

		public static string ExceptionToString(Exception ex, out string message)
		{
			var sb = new StringBuilder();

			message = ex.Message;
			sb.AppendLine("");
			sb.AppendLine("Exception Type: " + ex.GetType().FullName);
			sb.AppendLine("Message: " + ex.Message);
			sb.AppendLine("Source: " + ex.Source);
			foreach (var key in ex.Data.Keys)
			{
				sb.AppendLine(key.ToString() + ": " + ex.Data[key].ToString());
			}

			if (String.IsNullOrEmpty(ex.StackTrace))
			{
				sb.AppendLine("Environment Stack Trace: " + ex.StackTrace);
			}
			else
			{
				sb.AppendLine("Stack Trace: " + ex.StackTrace);
			}

			if (ex.InnerException != null)
			{
				sb.AppendLine("Inner Exception... ");
				sb.AppendLine(ExceptionToString(ex.InnerException, out message));
			}

			return sb.ToString();
		}


		public static string RunExternalTask(string task, string parameters, bool wait, bool redirectError = false, bool createwindow = false)
		{
			var file = new FileInfo(task);
			var output = string.Empty;

			using var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = file.FullName;
			process.StartInfo.Arguments = parameters;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardError = redirectError;
			process.StartInfo.RedirectStandardOutput = !createwindow && wait;
			process.StartInfo.WindowStyle = createwindow ? System.Diagnostics.ProcessWindowStyle.Normal : System.Diagnostics.ProcessWindowStyle.Hidden;
			process.StartInfo.CreateNoWindow = !createwindow;
			process.Start();


			if (wait)
			{
				output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
			}

			return output;
		}

		public static bool ByteArraysEqual(byte[] b1, byte[] b2)
		{
			if (b1 == b2) return true;
			if (b1 == null || b2 == null) return false;
			if (b1.Length != b2.Length) return false;
			for (int i = 0; i < b1.Length; i++)
			{
				if (b1[i] != b2[i]) return false;
			}
			return true;
		}

		public static Exception GetOriginalException(Exception ex)
		{
			while (ex.InnerException != null)
			{
				ex = ex.InnerException;
			}

			return ex;
		}

		public static async Task<string> ReadAllTextAsync(string path, Encoding encoding)
		{
			const int DefaultBufferSize = 4096;
			const FileOptions DefaultOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;

			var text = string.Empty;

			// Open the FileStream with the same FileMode, FileAccess
			// and FileShare as a call to File.OpenText would've done.
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, DefaultOptions))
			using (var reader = new StreamReader(stream, encoding))
			{
				text = await reader.ReadToEndAsync();
			}

			return text;
		}

		public static async Task<Byte[]> ReadAllBytesAsync(string path)
		{
			const int DefaultBufferSize = 4096;
			const FileOptions DefaultOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;

			Byte[] data;

			// Open the FileStream with the same FileMode, FileAccess
			// and FileShare as a call to File.OpenText would've done.
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, DefaultOptions))
			{
				data = await stream.ReadFullyAsync();
			}

			return data;
		}

		public static bool FilesEqual(string path1, string path2)
		{
			// very crude check - highly unlikey different versions will have the same file lengths
			// if one or both files do not exist, catch the error and fail the check
			try
			{
				var fi1 = new FileInfo(path1);
				var fi2 = new FileInfo(path2);

				if (fi1.Length != fi2.Length)
					return false;
				else
				{
					return System.Diagnostics.FileVersionInfo.GetVersionInfo(path1).FileVersion == System.Diagnostics.FileVersionInfo.GetVersionInfo(path2).FileVersion;
				}
			}
			catch
			{
				return false;
			}
		}

		public static string[] SplitCsv(string line)
		{
			List<string> result = [];
			StringBuilder currentStr = new StringBuilder("");
			bool inQuotes = false;
			for (int i = 0; i < line.Length; i++) // For each character
			{
				if (line[i] == '\"') // Quotes are closing or opening
					inQuotes = !inQuotes;
				else if (line[i] == ',') // Comma
				{
					if (!inQuotes) // If not in quotes, end of current string, add it to result
					{
						result.Add(currentStr.ToString());
						currentStr.Clear();
					}
					else
						currentStr.Append(line[i]); // If in quotes, just add it
				}
				else // Add any other character to current string
					currentStr.Append(line[i]);
			}
			result.Add(currentStr.ToString());
			return result.ToArray(); // Return array of all strings
		}
	}
}
