// ********************************************************************************
//     Document    :  TokenParser.cs
//     Version     :  0.1
//     Project     :  StrayIdeaz
//     Description :  This is a very simple HTML template parser. It takes a text file
//                    and replaces tokens with values supplied by the calling code
//                    via a delegate.
//     Author      :  StrayVision Software
//     Date        :  7/20/2008
// ********************************************************************************
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CumulusMX
{
	/// <summary>
	///     TokenParser is a class which implements a simple token replacement parser.
	/// </summary>
	/// <remarks>
	///     TokenParser is used by the calling code by implementing an event handler for
	///     the delegate TokenHandler(string strToken, ref string strReplacement)
	/// </remarks>
	internal partial class TokenParser
	{
		public string InputText;
		public string SourceFile { set; get; }

		public Encoding Encoding { set; get; }

		private string _AltList;
		public string AltResultNoParseList
		{
			set
			{
				_AltList = value;
				AltTags = value.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
			}

			get
			{
				return _AltList;
			}
		}

		private List<string> AltTags = null;

		public string AltResult { set; get; }


		public delegate void TokenHandler(string strToken, ref string strReplacement);
		public event TokenHandler OnToken;


		public TokenParser(TokenHandler tokenHandler)
		{
			OnToken = tokenHandler;
		}

		private string Parse3()
		{
			// Preallocate SB memory to double input size
			StringBuilder outText = new StringBuilder(InputText.Length * 2);
			StringBuilder altOutText = new StringBuilder(InputText.Length * 2);
			String token = string.Empty;
			String replacement = string.Empty;

			int i = 0;
			int len = InputText.Length;

			if (len == 0)
			{
				Program.cumulus.LogWarningMessage($"TokenParser error in file: {SourceFile}, InputString is zero length");
				return $"TokenParser error in file: {SourceFile}, InputString is zero length";
			}

			Regex rx = WebTagRegex();
			// Find matches.
			MatchCollection matches = rx.Matches(InputText);

			if (matches.Count > 0)
			{
				foreach (Match match in matches.Cast<Match>())
				{
					outText.Append(InputText.AsSpan(i, match.Index - i));
					if (AltTags != null)
						altOutText.Append(InputText.AsSpan(i, match.Index - i));

					try
					{
						// strip the "<#" ">" characters from the token string
						token = match.Value;
						token = token[2..^1];
						OnToken(token, ref replacement);
						outText.Append(replacement);
						if (AltTags != null)
						{
							string[] baseToken = token.Split(' ');
							if (AltTags.Contains(baseToken[0]))
								altOutText.Append(match.Value);
							else
								altOutText.Append(replacement);
						}
					}
					catch (Exception e)
					{
						Program.cumulus.LogWarningMessage($"Web tag error in file: {SourceFile}");
						Program.cumulus.LogMessage($"token={match.Value}");
						Program.cumulus.LogMessage($"Position in file (character)={match.Index}");
						Program.cumulus.LogMessage($"Exception: i={i} len={len}");
						Program.cumulus.LogMessage($"inputText.Length={InputText.Length}");
						Program.cumulus.LogMessage(e.ToString());
						Program.cumulus.LogMessage("** The output file will contain an error message starting \"**Web tag error\"");
						Cumulus.LogConsoleMessage($"*** web tag error in file '{SourceFile}' - see MXdiags file ***", ConsoleColor.Red);
						outText.Append($"**Web tag error, tag starting: <#{token[..(token.Length > 40 ? 39 : token.Length - 1)]}**");
					}
					i = match.Index + match.Length;
				}
				outText.Append(InputText.AsSpan(i, InputText.Length - i));
				if (AltTags != null)
					altOutText.Append(InputText.AsSpan(i, InputText.Length - i));
			}
			else
			{
				outText.Append(InputText);
				if (AltTags != null)
					altOutText.Append(InputText);
			}

			AltResult = altOutText.ToString();
			return outText.ToString();
		}

		public override string ToString()
		{
			string result;
			try
			{
				using (var reader = new StreamReader(File.OpenRead(SourceFile), Encoding))
				{
					InputText = reader.ReadToEnd();
				}
				result = Parse3();
			}
			catch (Exception e)
			{
				result = e.ToString();
			}
			return result;
		}

		public async Task<string> ToStringAsync()
		{
			string result;
			try
			{
				using (var reader = new StreamReader(File.OpenRead(SourceFile), Encoding))
				{
					InputText = await reader.ReadToEndAsync();
				}
				result = Parse3();
			}
			catch (Exception e)
			{
				result = e.ToString();
			}
			return result;
		}

		public string ToStringFromString()
		{
			string result;
			try
			{
				result = Parse3();
			}
			catch (Exception e)
			{
				result = e.ToString();
			}
			return result;
		}

		[GeneratedRegex("<#[^>]*?(?:(?:(\")[^\"]*?\\1)[^>]*?)*>", RegexOptions.Compiled)]
		private static partial Regex WebTagRegex();
	}
}
