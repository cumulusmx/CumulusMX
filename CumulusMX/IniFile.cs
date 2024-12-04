// **************************
// *** IniFile class V1.0    ***
// **************************
// *** (C)2009 S.T.A. snc ***
// **************************
// 
// Lots of mods and extensions by M Crossley
//


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CumulusMX
{

	internal class IniFile
	{

		#region "Declarations"

		// *** Lock for thread-safe access to file and local cache ***
		private readonly object m_Lock = new();

		// *** File name ***
		private string m_FileName = null;
		internal string FileName
		{
			get
			{
				return m_FileName;
			}
		}

		// *** Lazy loading flag ***
		private bool m_Lazy = false;

		// *** Local cache ***
		private readonly Dictionary<string, Dictionary<string, string>> m_Sections = [];

		// *** Local cache modified flag ***
		private bool m_CacheModified = false;
		internal static readonly string[] quoteCommaQuote = ["\",\""];

		#endregion

		#region "Methods"

		// *** Constructor ***
		public IniFile(string FileName)
		{
			Initialize(FileName, false);
		}

		public IniFile(string FileName, bool Lazy)
		{
			Initialize(FileName, Lazy);
		}

		// Readonly - from string
		public IniFile()
		{
			m_Lazy = false;
		}


		// *** Initialization ***
		private void Initialize(string FileName, bool Lazy)
		{
			m_FileName = FileName;
			m_Lazy = Lazy;
			if (!m_Lazy) Refresh();
		}

		// Load a supplied string rather than read from file
		internal void LoadString(string[] inputStr)
		{
			// *** Clear local cache ***
			m_Sections.Clear();

			// *** Read up the array content ***
			Dictionary<string, string> CurrentSection = null;

			foreach (var line in inputStr)
			{
				var s = line.Trim();

				// *** Check for section names ***
				if (s.StartsWith('[') && s.EndsWith(']'))
				{
					if (s.Length > 2)
					{
						string SectionName = s[1..^1];

						// *** Only first occurrence of a section is loaded ***
						if (m_Sections.ContainsKey(SectionName))
						{
							CurrentSection = null;
						}
						else
						{
							CurrentSection = [];
							m_Sections.Add(SectionName, CurrentSection);
						}
					}
				}
				else if (CurrentSection != null)
				{
					// *** Check for key+value pair ***
					int i;
					if ((i = s.IndexOf('=')) > 0)
					{
						// It's a value
						int j = s.Length - i - 1;
						string Key = s[..i].Trim();
						if (Key.Length > 0 && !CurrentSection.ContainsKey(Key))
						{
							// *** Only first occurrence of a key is loaded ***
							string Value = (j > 0) ? (s.Substring(i + 1, j).Trim()) : ("");
							CurrentSection.Add(Key, Value);
						}
					}
				}
			}
		}

		// *** Read file contents into local cache ***
		internal void Refresh()
		{
			lock (m_Lock)
			{
				try
				{
					// *** Clear local cache ***
					m_Sections.Clear();

					// *** Open the INI file ***
					if (!File.Exists(m_FileName))
					{
						return;
					}
					else
					{

						using FileStream fs = new FileStream(m_FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						using StreamReader sr = new StreamReader(fs);

						// *** Read up the file content ***
						Dictionary<string, string> CurrentSection = null;
						string s;

						while ((s = sr.ReadLine()) != null)
						{
							s = s.Trim();

							// *** Check for section names ***
							if (s.StartsWith('[') && s.EndsWith(']'))
							{
								if (s.Length > 2)
								{
									string SectionName = s[1..^1];

									// *** Only first occurrence of a section is loaded ***
									if (m_Sections.ContainsKey(SectionName))
									{
										CurrentSection = null;
									}
									else
									{
										CurrentSection = [];
										m_Sections.Add(SectionName, CurrentSection);
									}
								}
							}
							else if (CurrentSection != null)
							{
								// *** Check for key+value pair ***
								int i;
								if (s.StartsWith('#'))
								{
									// It's a comment
									// *** Only first occurrence of a key is loaded ***
									CurrentSection.TryAdd(s, "");
								}
								else if ((i = s.IndexOf('=')) > 0)
								{
									// It's a value
									int j = s.Length - i - 1;
									string Key = s[..i].Trim();
									if (Key.Length > 0 && !CurrentSection.ContainsKey(Key))
									{
										// *** Only first occurrence of a key is loaded ***
										string Value = (j > 0) ? (s.Substring(i + 1, j).Trim()) : ("");
										CurrentSection.Add(Key, Value);
									}
								}
							}
						}
					}
				}
				catch
				{
					// ignore
				}
			}
		}

		// *** Flush local cache content ***
		internal void Flush()
		{
			lock (m_Lock)
			{
				// *** If local cache was not modified, exit ***
				if (!m_CacheModified) return;

				var success = false;
				var retries = Cumulus.LogFileRetries;
				do
				{
					try
					{
						// *** Open the file ***
						using (StreamWriter sw = new StreamWriter(m_FileName))
						{
							sw.WriteLine($"#Last updated: {DateTime.Now:G}");

							// *** Cycle on all sections ***
							bool First = false;
							foreach (KeyValuePair<string, Dictionary<string, string>> SectionPair in m_Sections)
							{
								Dictionary<string, string> Section = SectionPair.Value;
								if (First) sw.WriteLine();
								First = true;

								// *** Write the section name ***
								sw.Write('[');
								sw.Write(SectionPair.Key);
								sw.WriteLine(']');

								// *** Cycle on all key+value pairs in the section ***
								foreach (KeyValuePair<string, string> ValuePair in Section)
								{
									// *** Write the key+value pair ***
									sw.Write(ValuePair.Key);
									if (!ValuePair.Key.StartsWith('#'))
									{
										sw.Write('=');
										sw.Write(ValuePair.Value);
									}
									sw.WriteLine();
								}
							}
						}

						success = true;
						m_CacheModified = false;
					}
					catch (IOException ex)
					{
						if (ex.HResult == -2147024864) // 0x80070020
						{
							retries--;
							System.Threading.Thread.Sleep(250);
						}
						else
						{
							throw;
						}
					}
				} while (!success && retries >= 0);
			}
		}

		// *** Read a value from local cache ***
		// *** Insert or modify a value in local cache ***
		internal bool ValueExists(string SectionName, string Key)
		{
			// *** Lazy loading ***
			if (m_Lazy)
			{
				m_Lazy = false;
				Refresh();
			}

			lock (m_Lock)
			{
				// *** Check if the section exists ***
				Dictionary<string, string> Section;
				if (!m_Sections.TryGetValue(SectionName, out Section)) return false;

				// *** Check if the key exists ***
				string Value;
				if (Section.TryGetValue(Key, out Value))
					return true;
				else
					return false;
			}
		}

		internal void DeleteValue(string SectionName, string Key)
		{
			// *** Lazy loading ***
			if (m_Lazy)
			{
				m_Lazy = false;
				Refresh();
			}

			lock (m_Lock)
			{
				// *** Check if the section exists ***
				Dictionary<string, string> Section;
				if (!m_Sections.TryGetValue(SectionName, out Section)) return;

				// *** Check if the key exists ***
				string Value;
				if (Section.TryGetValue(Key, out Value))
				{
					m_CacheModified = true;
					Section.Remove(Key);
				}
			}

		}

		// *** Encode byte array ***
		private static string EncodeByteArray(byte[] Value)
		{
			if (Value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (byte b in Value)
			{
				string hex = Convert.ToString(b, 16);
				int l = hex.Length;
				if (l > 2)
				{
					sb.Append(hex.AsSpan(l - 2, 2));
				}
				else
				{
					if (l < 2) sb.Append('0');
					sb.Append(hex);
				}
			}
			return sb.ToString();
		}

		// *** Decode byte array ***
		private static byte[] DecodeByteArray(string Value)
		{
			if (Value == null) return [];

			int l = Value.Length;
			if (l < 2) return [];

			l /= 2;
			byte[] Result = new byte[l];
			for (int i = 0; i < l; i++) Result[i] = Convert.ToByte(Value.Substring(i * 2, 2), 16);
			return Result;
		}

		// *** Encode bool array
		private static string EncodeBoolArray(bool[] Value)
		{
			if (Value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (bool b in Value)
			{
				sb.Append(b ? "1," : "0,");
			}
			if (sb[^1] == ',')
				sb.Length--;

			return sb.ToString();
		}

		// *** Decode bool array
		private static bool[] DecodeBoolArray(string Value, int Length)
		{
			if (Value == null) return [];

			var arr = Value.Split(',');
			var ret = new bool[Math.Max(arr.Length, Length)];
			for (var i = 0; i < ret.Length; i++)
			{
				ret[i] = Convert.ToBoolean(Convert.ToInt32(arr[i]));
			}

			return ret;
		}

		// *** Encode string array - very basic, no escaped quotes allowed
		private static string EncodeStringArray(string[] Value)
		{
			if (Value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (string b in Value)
			{
				sb.Append($"\"{b}\",");
			}
			if (sb[^1] == ',')
				sb.Length--;

			return sb.ToString();
		}

		// *** Decode string array - very basic, no escaped quotes allowed
		private static string[] DecodeStringArray(string Value)
		{
			if (Value == null) return [];

			var x = Value[1..^1];

			return x.Split(quoteCommaQuote, StringSplitOptions.None);
		}

		private static string EncodeIntArray(int[] Value)
		{
			if (Value == null) return null;


			return string.Join(",", Value);
		}

		// *** Decode string array - very basic, no escaped quotes allowed
		private static int[] DecodeIntArray(string Value, int Length)
		{
			if (Value == null) return [];

			var arr = Value.Split(',');
			var ret = new int[Math.Max(arr.Length, Length)];
			for (var i = 0; i < ret.Length; i++)
			{
				ret[i] = Convert.ToInt32(arr[i]);
			}

			return ret;
		}

		// *** Getters for various types ***
		internal string GetValue(string SectionName, string Key, string DefaultValue)
		{
			// *** Lazy loading ***
			if (m_Lazy)
			{
				m_Lazy = false;
				Refresh();
			}

			lock (m_Lock)
			{
				// *** Check if the section exists ***
				Dictionary<string, string> Section;
				if (!m_Sections.TryGetValue(SectionName, out Section)) return DefaultValue;

				// *** Check if the key exists ***
				string Value;
				if (!Section.TryGetValue(Key, out Value)) return DefaultValue;

				// *** Check if the value is blank ***
				if (string.IsNullOrWhiteSpace(Value)) return DefaultValue;

				// *** Return the found value ***
				return Value;
			}
		}

		internal bool GetValue(string SectionName, string Key, bool DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, DefaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
			int Value;
			if (int.TryParse(StringValue, out Value)) return (Value != 0);
			return DefaultValue;
		}

		internal int GetValue(string SectionName, string Key, int DefaultValue, int MinValue = int.MinValue, int MaxValue = int.MaxValue)
		{
			string StringValue = GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			int Value;
			if (int.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value))
			{
				if (Value < MinValue) return MinValue;
				if (Value > MaxValue) return MaxValue;
				return Value;
			}
			return DefaultValue;
		}

		internal double GetValue(string SectionName, string Key, double DefaultValue, double MinValue = double.MinValue, double MaxValue = double.MaxValue)
		{
			string StringValue = GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			double Value;
			if (double.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value))
			{
				if (Value < MinValue) return MinValue;
				if (Value > MaxValue) return MaxValue;
				return Value;
			}
			return DefaultValue;
		}

		internal decimal GetValue(string SectionName, string Key, decimal DefaultValue, decimal MinValue = decimal.MinValue, decimal MaxValue = decimal.MaxValue)
		{
			string StringValue = GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			decimal Value;
			if (decimal.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value))
			{
				if (Value < MinValue) return MinValue;
				if (Value > MaxValue) return MaxValue;
				return Value;
			}
			return DefaultValue;
		}

		internal byte[] GetValue(string SectionName, string Key, byte[] DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, EncodeByteArray(DefaultValue));
			try
			{
				return DecodeByteArray(StringValue);
			}
			catch (FormatException)
			{
				return DefaultValue;
			}
		}

		internal bool[] GetValue(string SectionName, string Key, bool[] DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, EncodeBoolArray(DefaultValue));
			try
			{
				return DecodeBoolArray(StringValue, DefaultValue.Length);
			}
			catch (FormatException)
			{
				return DefaultValue;
			}
		}

		internal string[] GetValue(string SectionName, string Key, string[] DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, EncodeStringArray(DefaultValue));
			try
			{
				return DecodeStringArray(StringValue);
			}
			catch (FormatException)
			{
				return DefaultValue;
			}
		}

		internal int[] GetValue(string SectionName, string Key, int[] DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, EncodeIntArray(DefaultValue));
			try
			{
				return DecodeIntArray(StringValue, DefaultValue.Length);
			}
			catch (FormatException)
			{
				return DefaultValue;
			}
		}


		internal DateTime GetValue(string SectionName, string Key, DateTime DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			DateTime Value;
			if (DateTime.TryParse(StringValue, CultureInfo.InvariantCulture, out Value)) return Value;
			return DefaultValue;
		}

		// *** Setters for various types ***
		internal void SetValue(string SectionName, string Key, string Value)
		{
			// *** Lazy loading ***
			if (m_Lazy)
			{
				m_Lazy = false;
				Refresh();
			}

			lock (m_Lock)
			{
				// *** Flag local cache modification ***
				m_CacheModified = true;

				// *** Check if the section exists ***
				Dictionary<string, string> Section;
				if (!m_Sections.TryGetValue(SectionName, out Section))
				{
					// *** If it doesn't, add it ***
					Section = [];
					m_Sections.Add(SectionName, Section);
				}

				// *** Modify the value ***
				Section.Remove(Key);
				Section.Add(Key, Value);
			}
		}

		internal void SetValue(string SectionName, string Key, bool Value)
		{
			SetValue(SectionName, Key, (Value) ? ("1") : ("0"));
		}

		internal void SetValue(string SectionName, string Key, int Value)
		{
			SetValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
		}

		internal void SetValue(string SectionName, string Key, double Value)
		{
			SetValue(SectionName, Key, Value.ToString("G17", CultureInfo.InvariantCulture));
		}

		internal void SetValue(string SectionName, string Key, decimal Value)
		{
			SetValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
		}

		internal void SetValue(string SectionName, string Key, byte[] Value)
		{
			SetValue(SectionName, Key, EncodeByteArray(Value));
		}

		internal void SetValue(string SectionName, string Key, bool[] Value)
		{
			SetValue(SectionName, Key, EncodeBoolArray(Value));
		}

		internal void SetValue(string SectionName, string Key, string[] Value)
		{
			SetValue(SectionName, Key, EncodeStringArray(Value));
		}

		internal void SetValue(string SectionName, string Key, int[] Value)
		{
			SetValue(SectionName, Key, EncodeIntArray(Value));
		}

		internal void SetValue(string SectionName, string Key, DateTime Value)
		{
			// write datetimes in ISO 8601 ("sortable")
			SetValue(SectionName, Key, Value.ToString("s"));
		}

		#endregion
	}

}
