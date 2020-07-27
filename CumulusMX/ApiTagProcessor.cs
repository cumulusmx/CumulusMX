using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class ApiTagProcessor
	{
		private readonly Cumulus cumulus;
		private readonly TokenParser tokenParser;
		private readonly WebTags webtags;

		internal ApiTagProcessor(Cumulus cumulus, WebTags webtags)
		{
			this.cumulus = cumulus;
			this.webtags = webtags;
			tokenParser = new TokenParser();
			tokenParser.OnToken += cumulus.TokenParserOnToken;
			tokenParser.encoding = new UTF8Encoding(false);
		}


		// Output the processed response as a JSON string
		public string ProcessJson(string query)
		{
			var rc = false;

			cumulus.LogDebugMessage("API tag: Processing API JSON tag request");
			cumulus.LogDataMessage("API tag: Input string = " + query);

			// remove leading "?" and split on "&"
			var input = new List<string>(query.Substring(1).Split('&'));
			var parms = new Dictionary<string, string>();
			if (input[0] == "rc")
			{
				input.RemoveAt(0);
				rc = true;
			}
			var output = new StringBuilder("{", query.Length * 2);

			foreach(var tag in input)
			{
				if (rc)
				{
					parms.Add("webtag", tag);
					parms.Add("rc", "y");
				}
				var val = webtags.GetWebTagText(tag, parms);
				if (val != "")
				{

					output.Append($"\"{tag}\":\"{val}\",");
				}
				if (rc)
				{
					parms.Clear();
				}
			}
			if (output.Length > 0)
			{
				// remove trailing ","
				output.Remove(output.Length - 1, 1);
			}
			output.Append("}");

			cumulus.LogDataMessage("API tag: Output string = " + output);

			return output.ToString();
		}

		// Just return the processed text as-is
		public string ProcessText(IHttpContext context)
		{
			cumulus.LogDebugMessage("API tag: Processing API Text tag request");

			var data = new StreamReader(context.Request.InputStream).ReadToEnd();

			cumulus.LogDataMessage("API tag: Input string = " + data);

			tokenParser.InputText = data;
			var output = tokenParser.ToStringFromString();

			cumulus.LogDataMessage("API tag: Output string = " + output);

			return output;
		}

	}
}
