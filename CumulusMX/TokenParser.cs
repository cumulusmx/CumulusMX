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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CumulusMX
{

	/// <summary>
	///     TokenParser is a class which implements a simple token replacement parser.
	/// </summary>
	/// <remarks>
	///     TokenParser is used by the calling code by implementing an event handler for
	///     the delegate TokenHandler(string strToken, ref string strReplacement)
	/// </remarks>
	internal class TokenParser
	{
		public string InputText;
		public string SourceFile { set; get; }

		public Encoding encoding { set; get; }

		public delegate void TokenHandler(string strToken, ref string strReplacement);
		public event TokenHandler OnToken;

		public TokenParser()
		{

		}


		/// <summary>
		///     ExtractToken parses a token in the format "<#TOKENNAME>".
		/// </summary>
		/// <param name="strToken" type="string">
		///     <para>
		///         This is a token parsed from a text file in the format "<#TOKENNAME>".
		///     </para>
		/// </param>
		/// <returns>
		///     It returns the string between the tokens "<#" and ">"
		/// </returns>
		/*private string ExtractToken(string strToken)
		{
			int firstPos = strToken.IndexOf("<#") + 2;
			int secondPos = strToken.LastIndexOf(">");
			string result = strToken.Substring(firstPos, secondPos - firstPos);

			return result.Trim();
		}*/


		/// <summary>
		///     Parse() iterates through each character of the class variable "inputText"
		/// </summary>
		/// <returns>
		///     Parse() returns a string representing inputText with its tokens exchanged
		///     for the calling code's values.
		/// </returns>
		/*private String Parse()
		{
			const string tokenStart = "<";
			const string tokenNext = "#";
			const string tokenEnd = ">";

			String outText = String.Empty;
			String token = String.Empty;
			String replacement = String.Empty;

			int i = 0;
			string tok;
			string tok2;
			int len = inputText.Length;

			while (i < len)
			{
				tok = inputText[i].ToString();
				if (tok == tokenStart)
				{
					i++;
					tok2 = inputText[i].ToString();
					if (tok2 == tokenNext)
					{
						i--;
						while (i < len & tok2 != tokenEnd)
						{
							tok2 = inputText[i].ToString();
							token += tok2;
							i++;
						}
						try
						{
							OnToken(ExtractToken(token), ref replacement);
							outText += replacement;
						}
						catch (Exception e)
						{
							cumulus.LogMessage("Exception: i="+i+" len="+len);
							cumulus.LogMessage("inputText.Length="+inputText.Length);
							cumulus.LogMessage("token="+token);
							cumulus.LogMessage(e.ToString());
							cumulus.LogMessage(inputText);
							Console.WriteLine("*** token error ***");
							outText += e.Message;
						}
						token = String.Empty;
						i--;
					}
					else
					{
						outText += tok;
						outText += tok2;
					}
				}
				else
				{
					outText += tok;
				}
				i++;
			}
			return outText;
		}*/

		/*private String Parse2()
		{
			const string tokenStart = "<#";
			const string tokenEnd = ">";

			String outText = String.Empty;
			String token = String.Empty;
			String replacement = String.Empty;

			int i = 0;
			int len = InputText.Length;

			while (i < len)
			{
				int nextTagPos = InputText.IndexOf(tokenStart, i);

				if (nextTagPos == -1)
				{
					// no more tokens, copy remainder of string
					outText += InputText.Substring(i);
					i = len; // to cause loop to terminate
				}
				else
				{
					// copy from where we were, up to char before token
					outText += InputText.Substring(i, nextTagPos - i);

					// look for the end of the token
					int endPos = InputText.IndexOf(tokenEnd, nextTagPos);

					if (endPos == -1)
					{
						// no end of token, copy remainder of string
						outText += InputText.Substring(nextTagPos);
						i = len; // to cause loop to terminate
					}
					else
					{
						// found end of token, process it
						token = InputText.Substring(nextTagPos + 2, endPos - (nextTagPos + 2));

						try
						{
							OnToken(token, ref replacement);
							outText += replacement;


						}
						catch (Exception e)
						{
							Trace.WriteLine("Web tag error");
							Trace.WriteLine("Exception: i=" + i + " len=" + len);
							Trace.WriteLine("inputText.Length=" + InputText.Length);
							Trace.WriteLine("token=" + token);
							Trace.WriteLine(e.ToString());
							//cumulus.LogMessage(InputText);
							Console.WriteLine("*** web tag error - see MXdiags file ***");
							outText += e.Message;
						}

						// move past the token
						i = endPos + 1;
					}
				}
			}
			return outText;
		}*/

		private String Parse3()
		{
			String outText = String.Empty;
			String token = String.Empty;
			String replacement = String.Empty;

			int i = 0;
			int len = InputText.Length;

			Regex rx = new Regex("<#[^>]*?(?:(?:(\")[^\"]*?\\1)[^>]*?)*>", RegexOptions.Compiled);
			// Find matches.
			MatchCollection matches = rx.Matches(InputText);

			if (matches.Count > 0)
			{
				foreach (Match match in matches)
				{
					outText += InputText.Substring(i, match.Index - i);
					try
					{
						// strip the "<#" ">" characters from the token string
						token = match.Value;
						token = token.Substring(2, token.Length - 3);
						OnToken(token, ref replacement);
						outText += replacement;
					}
					catch (Exception e)
					{
						Trace.WriteLine("Web tag error");
						Trace.WriteLine("Exception: i=" + i + " len=" + len);
						Trace.WriteLine("inputText.Length=" + InputText.Length);
						Trace.WriteLine("token=" + match.Value);
						Trace.WriteLine(e.ToString());
						//cumulus.LogMessage(InputText);
						Console.WriteLine("*** web tag error - see MXdiags file ***");
						//outText += e.Message;
						outText += "**Web tag error, tag starting: #" + token.Substring(0, token.Length > 40 ? 39 : token.Length - 1) + "**";
					}
					i = match.Index + match.Length;
				}
				outText += InputText.Substring(i, InputText.Length - i);
			}
			else
			{
				outText = InputText;
			}

			return outText;
		}

		private static string Utf16ToUtf8(string utf16String)
		{
			/**************************************************************
			 * Every .NET string will store text with the UTF16 encoding, *
			 * known as Encoding.Unicode. Other encodings may exist as    *
			 * Byte-Array or incorrectly stored with the UTF16 encoding.  *
			 *                                                            *
			 * UTF8 = 1 bytes per char                                    *
			 *    ["100" for the ansi 'd']                                *
			 *    ["206" and "186" for the russian 'κ']                   *
			 *                                                            *
			 * UTF16 = 2 bytes per char                                   *
			 *    ["100, 0" for the ansi 'd']                             *
			 *    ["186, 3" for the russian 'κ']                          *
			 *                                                            *
			 * UTF8 inside UTF16                                          *
			 *    ["100, 0" for the ansi 'd']                             *
			 *    ["206, 0" and "186, 0" for the russian 'κ']             *
			 *                                                            *
			 * We can use the convert encoding function to convert an     *
			 * UTF16 Byte-Array to an UTF8 Byte-Array. When we use UTF8   *
			 * encoding to string method now, we will get a UTF16 string. *
			 *                                                            *
			 * So we imitate UTF16 by filling the second byte of a char   *
			 * with a 0 byte (binary 0) while creating the string.        *
			 **************************************************************/

			// Storage for the UTF8 string
			string utf8String = String.Empty;

			// Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
			byte[] utf16Bytes = Encoding.Unicode.GetBytes(utf16String);
			byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

			// Fill UTF8 bytes inside UTF8 string
			for (int i = 0; i < utf8Bytes.Length; i++)
			{
				// Because char always saves 2 bytes, fill char with 0
				byte[] utf8Container = new byte[2] { utf8Bytes[i], 0 };
				utf8String += BitConverter.ToChar(utf8Container, 0);
			}

			// Return UTF8
			return utf8String;
		}

		/// <summary>
		///     Content() reads the text file specified in the constructor and returns the unparsed text.
		/// </summary>
		/// <returns>
		///     A string representing the unparsed text file.
		/// </returns>
		public String Content()
		{
			string result;
			try
			{
				using (TextReader reader = new StreamReader(SourceFile,encoding))
				{
					InputText = reader.ReadToEnd();

				}
				result = InputText;
			}
			catch (Exception e)
			{
				result = e.Message;
			}
			return result;
		}


		/// <summary>
		///     This is called to return the parsed text file.
		/// </summary>
		/// <returns>
		///     A string representing the text file with all its tokens replaced by data
		///     supplied by the calling code through the Tokenhandler delegate
		/// </returns>
		public override string ToString()
		{
			//TextReader reader;
			string result;
			try
			{
				using (TextReader reader = new StreamReader(SourceFile, encoding))
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

		public string ToStringFromString()
		{
			//TextReader reader;
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



	}
}
