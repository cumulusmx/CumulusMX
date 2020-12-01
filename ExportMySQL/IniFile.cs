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

namespace CumulusMX
{

	internal class IniFile
	{

#region "Declarations"

		// *** Lock for thread-safe access to file and local cache ***
		private object mLock = new object();

		// *** File name ***
		private string mFileName = null;
		internal string FileName
		{
			get
			{
				return mFileName;
			}
		}

		// *** Lazy loading flag ***
		private bool mLazy = false;

		// *** Local cache ***
		private Dictionary<string, Dictionary<string, string>> mSections = new Dictionary<string,Dictionary<string, string>>();

		// *** Local cache modified flag ***
		private bool mCacheModified = false;

#endregion

#region "Methods"

		// *** Constructor ***
		public IniFile(string fileName)
		{
			Initialize(fileName, false);
		}

		public IniFile(string fileName, bool lazy)
		{
			Initialize(fileName, lazy);
		}

		// *** Initialization ***
		private void Initialize (string fileName, bool lazy)
		{
			mFileName = fileName;
			mLazy = lazy;
			if (!mLazy) Refresh();
		}

		// *** Read file contents into local cache ***
		internal void Refresh()
		{
			lock (mLock)
			{
				StreamReader sr = null;
				try
				{
					// *** Clear local cache ***
					mSections.Clear();

					// *** Open the INI file ***
					try
					{
						sr = new StreamReader(mFileName);
					}
					catch (FileNotFoundException)
					{
						return;
					}

					// *** Read up the file content ***
					Dictionary<string, string> currentSection = null;
					string s;
					while ((s = sr.ReadLine()) != null)
					{
						s = s.Trim();

						// *** Check for section names ***
						if (s.StartsWith("[") && s.EndsWith("]"))
						{
							if (s.Length > 2)
							{
								string sectionName = s.Substring(1,s.Length-2);

								// *** Only first occurrence of a section is loaded ***
								if (mSections.ContainsKey(sectionName))
								{
									currentSection = null;
								}
								else
								{
									currentSection = new Dictionary<string, string>();
									mSections.Add(sectionName,currentSection);
								}
							}
						}
						else if (currentSection != null)
						{
							// *** Check for key+value pair ***
							int i;
							if ((i=s.IndexOf('=')) > 0)
							{
								int j = s.Length - i - 1;
								string key = s.Substring(0,i).Trim();
								if (key.Length  > 0)
								{
									// *** Only first occurrence of a key is loaded ***
									if (!currentSection.ContainsKey(key))
									{
										string value = (j > 0) ? (s.Substring(i+1,j).Trim()) : ("");
										currentSection.Add(key,value);
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
					sr = null;
				}
			}
		}

		// *** Flush local cache content ***
		internal void Flush()
		{
			lock(mLock)
			{
				// *** If local cache was not modified, exit ***
				if (!mCacheModified) return;
				mCacheModified=false;

				// *** Open the file ***
				using (StreamWriter sw = new StreamWriter(mFileName))
				{
					// *** Cycle on all sections ***
					bool first = false;
					foreach (KeyValuePair<string, Dictionary<string, string>> sectionPair in mSections)
					{
						Dictionary<string, string> section = sectionPair.Value;
						if (first) sw.WriteLine();
						first = true;

						// *** Write the section name ***
						sw.Write('[');
						sw.Write(sectionPair.Key);
						sw.WriteLine(']');

						// *** Cycle on all key+value pairs in the section ***
						foreach (KeyValuePair<string, string> valuePair in section)
						{
							// *** Write the key+value pair ***
							sw.Write(valuePair.Key);
							sw.Write('=');
							sw.WriteLine(valuePair.Value);
						}
					}
				}
			}
		}

		// *** Read a value from local cache ***
		internal string GetValue(string sectionName, string key, string defaultValue)
		{
			// *** Lazy loading ***
			if (mLazy)
			{
				mLazy = false;
				Refresh();
			}

			lock (mLock)
			{
				// *** Check if the section exists ***
				Dictionary<string, string> section;
				if (!mSections.TryGetValue(sectionName, out section)) return defaultValue;

				// *** Check if the key exists ***
				string value;
				if (!section.TryGetValue(key, out value)) return defaultValue;

				// *** Return the found value ***
				return value;
			}
		}

		// *** Insert or modify a value in local cache ***
		internal void SetValue(string sectionName, string key, string value)
		{
			// *** Lazy loading ***
			if (mLazy)
			{
				mLazy = false;
				Refresh();
			}

			lock (mLock)
			{
				// *** Flag local cache modification ***
				mCacheModified = true;

				// *** Check if the section exists ***
				Dictionary<string, string> section;
				if (!mSections.TryGetValue(sectionName, out section))
				{
					// *** If it doesn't, add it ***
					section = new Dictionary<string, string>();
					mSections.Add(sectionName,section);
				}

				// *** Modify the value ***
				if (section.ContainsKey(key)) section.Remove(key);
				section.Add(key, value);
			}
		}

		// *** Encode byte array ***
		private string EncodeByteArray(byte[] value)
		{
			if (value == null) return null;

			StringBuilder sb = new StringBuilder();
			foreach (byte b in value)
			{
				string hex = Convert.ToString(b,16);
				int l = hex.Length;
				if (l > 2)
				{
					sb.Append(hex.Substring(l-2,2));
				}
				else
				{
					if (l < 2) sb.Append("0");
					sb.Append(hex);
				}
			}
			return sb.ToString();
		}

		// *** Decode byte array ***
		private byte[] DecodeByteArray(string value)
		{
			if (value == null) return null;

			int l = value.Length;
			if (l < 2) return new byte[] { };

			l /= 2;
			byte[] result = new byte[l];
			for (int i=0; i<l; i++) result[i] = Convert.ToByte(value.Substring(i*2,2),16);
			return result;
		}

		// *** Getters for various types ***
		internal bool GetValue(string sectionName, string key, bool defaultValue)
		{
			string stringValue=GetValue(sectionName, key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
			int value;
			if (int.TryParse(stringValue, out value)) return (value != 0);
			return defaultValue;
		}

		internal int GetValue(string sectionName, string key, int defaultValue)
		{
			string stringValue=GetValue(sectionName, key, defaultValue.ToString(CultureInfo.InvariantCulture));
			int value;
			if (int.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
			return defaultValue;
		}

		internal double GetValue(string sectionName, string key, double defaultValue)
		{
			string stringValue=GetValue(sectionName, key, defaultValue.ToString(CultureInfo.InvariantCulture));
			double value;
			if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
			return defaultValue;
		}

		internal byte[] GetValue(string sectionName, string key, byte[] defaultValue)
		{
			string stringValue = GetValue(sectionName, key, EncodeByteArray(defaultValue));
			try
			{
				return DecodeByteArray(stringValue);
			}
			catch (FormatException)
			{
				return defaultValue;
			}
		}

		internal DateTime GetValue(string sectionName, string key, DateTime defaultValue)
		{
			string stringValue = GetValue(sectionName, key, defaultValue.ToString(CultureInfo.InvariantCulture));
			DateTime value;
			if (DateTime.TryParse(stringValue, out value)) return value;
			return defaultValue;
		}

		// *** Setters for various types ***
		internal void SetValue(string sectionName, string key, bool value)
		{
			SetValue(sectionName, key, (value) ? ("1") : ("0"));
		}

		internal void SetValue(string sectionName, string key, int value)
		{
			SetValue(sectionName, key, value.ToString(CultureInfo.InvariantCulture));
		}

		internal void SetValue(string sectionName, string key, double value)
		{
			SetValue(sectionName, key, value.ToString(CultureInfo.InvariantCulture));
		}

		internal void SetValue(string sectionName, string key, byte[] value)
		{
			SetValue(sectionName, key, EncodeByteArray(value));
		}

		internal void SetValue(string sectionName, string key, DateTime value)
		{
			SetValue(sectionName, key, value.ToString());
		}

		#endregion
	}
}
