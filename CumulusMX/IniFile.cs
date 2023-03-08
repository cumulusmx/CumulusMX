// **************************
// *** IniFile class V1.0    ***
// **************************
// *** (C)2009 S.T.A. snc ***
// **************************
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FluentFTP.Helpers;

namespace CumulusMX
{

	internal class IniFile
	{

#region "Declarations"

		// *** Lock for thread-safe access to file and local cache ***
		private readonly object m_Lock = new object();

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
		private readonly Dictionary<string, Dictionary<string, string>> m_Sections = new Dictionary<string,Dictionary<string, string>>();

		// *** Local cache modified flag ***
		private bool m_CacheModified = false;

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

		// *** Initialization ***
		private void Initialize (string FileName, bool Lazy)
		{
			m_FileName = FileName;
			m_Lazy = Lazy;
			if (!m_Lazy) Refresh();
		}

		// *** Read file contents into local cache ***
		internal void Refresh()
		{
			lock (m_Lock)
			{
				FileStream fs = null;
				StreamReader sr = null;
				try
				{
					// *** Clear local cache ***
					m_Sections.Clear();

					// *** Open the INI file ***
					if (File.Exists(m_FileName))
					{
						fs = new FileStream(m_FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						sr = new StreamReader(fs);
					}
					else
					{
						return;
					}

					// *** Read up the file content ***
					Dictionary<string, string> CurrentSection = null;
					string s;
					while ((s = sr.ReadLine()) != null)
					{
						s = s.Trim();

						// *** Check for section names ***
						if (s.StartsWith("[") && s.EndsWith("]"))
						{
							if (s.Length > 2)
							{
								string SectionName = s.Substring(1, s.Length - 2);

								// *** Only first occurrence of a section is loaded ***
								if (m_Sections.ContainsKey(SectionName))
								{
									CurrentSection = null;
								}
								else
								{
									CurrentSection = new Dictionary<string, string>();
									m_Sections.Add(SectionName, CurrentSection);
								}
							}
						}
						else if (CurrentSection != null)
						{
							// *** Check for key+value pair ***
							int i;
							if (s.StartsWith("#"))
							{
								// It's a comment
								// *** Only first occurrence of a key is loaded ***
								if (!CurrentSection.ContainsKey(s))
								{
									CurrentSection.Add(s, "");
								}
							}
							else if ((i = s.IndexOf('=')) > 0)
							{
								// It's a value
								int j = s.Length - i - 1;
								string Key = s.Substring(0, i).Trim();
								if (Key.Length > 0)
								{
									// *** Only first occurrence of a key is loaded ***
									if (!CurrentSection.ContainsKey(Key))
									{
										string Value = (j > 0) ? (s.Substring(i + 1, j).Trim()) : ("");
										CurrentSection.Add(Key, Value);
									}
								}
							}
						}
					}
				}
				finally
				{
					// *** Cleanup: close file ***
					if (sr != null) sr.Close();
					if (fs != null) fs.Close();
					sr = null;
					fs = null;
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
									if (!ValuePair.Key.StartsWith("#"))
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
					catch
					{
						throw;
					}
				} while (!success && retries >= 0);
			}
		}

		// *** Read a value from local cache ***
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

		// *** Insert or modify a value in local cache ***
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
					Section = new Dictionary<string, string>();
					m_Sections.Add(SectionName,Section);
				}

				// *** Modify the value ***
				if (Section.ContainsKey(Key)) Section.Remove(Key);
				Section.Add(Key, Value);
			}
		}

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
		private string EncodeByteArray(byte[] Value)
		{
			if (Value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (byte b in Value)
			{
				string hex = Convert.ToString(b,16);
				int l = hex.Length;
				if (l > 2)
				{
					sb.Append(hex.Substring(l-2,2));
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
		private byte[] DecodeByteArray(string Value)
		{
			if (Value == null) return null;

			int l = Value.Length;
			if (l < 2) return new byte[] { };

			l /= 2;
			byte[] Result = new byte[l];
			for (int i=0; i<l; i++) Result[i] = Convert.ToByte(Value.Substring(i*2,2),16);
			return Result;
		}

		// *** Encode bool array
		private string EncodeBoolArray(bool[] Value)
		{
			if (Value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (bool b in Value)
			{
				string text = b ? "1," : "0,";
				sb.Append(b ? "1," : "0,");
			}
			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			return sb.ToString();
		}

		// *** Decode bool array
		private bool[] DecodeBoolArray(string Value, int Length)
		{
			if (Value == null) return null;

			var arr = Value.Split(',');
			var ret = new bool[Math.Max(arr.Length, Length)];
			for (var i = 0; i < ret.Length; i++)
			{
				ret[i] = Convert.ToBoolean(Convert.ToInt32(arr[i]));
			}

			return ret;
		}

		// *** Encode string array - very basic, no escaped quotes allowed
		private string EncodeStringArray(string[] Value)
		{
			if (Value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (string b in Value)
			{
				sb.Append($"\"{b}\",");
			}
			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			return sb.ToString();
		}

		// *** Decode string array - very basic, no escaped quotes allowed
		private string[] DecodeStringArray(string Value, int Length)
		{
			if (Value == null) return null;

			var x = Value.Substring(1, Value.Length - 2);

			return x.Split(new string[] { "\",\"" }, StringSplitOptions.None);
		}

		private string EncodeIntArray(int[] Value)
		{
			if (Value == null) return null;


			return string.Join(",", Value);
		}

		// *** Decode string array - very basic, no escaped quotes allowed
		private int[] DecodeIntArray(string Value, int Length)
		{
			if (Value == null) return null;

			var arr = Value.Split(',');
			var ret = new int[Math.Max(arr.Length, Length)];
			for (var i = 0; i < ret.Length; i++)
			{
				ret[i] = Convert.ToInt32(arr[i]);
			}

			return ret;
		}

		// *** Getters for various types ***
		internal bool GetValue(string SectionName, string Key, bool DefaultValue)
		{
			string StringValue=GetValue(SectionName, Key, DefaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
			int Value;
			if (int.TryParse(StringValue, out Value)) return (Value != 0);
			return DefaultValue;
		}

		internal int GetValue(string SectionName, string Key, int DefaultValue)
		{
			string StringValue=GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			int Value;
			if (int.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
			return DefaultValue;
		}

		internal double GetValue(string SectionName, string Key, double DefaultValue)
		{
			string StringValue=GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			double Value;
			if (double.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
			return DefaultValue;
		}

		internal decimal GetValue(string SectionName, string Key, decimal DefaultValue)
		{
			string StringValue = GetValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
			decimal Value;
			if (decimal.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
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
				return DecodeStringArray(StringValue, DefaultValue.Length);
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
			if (DateTime.TryParse(StringValue, out Value)) return Value;
			return DefaultValue;
		}

		// *** Setters for various types ***
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
