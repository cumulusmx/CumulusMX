using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadCustomHttp
	{
	}

	public class CustomHttpSettings
	{
		public string Url { get; set; }
		public bool Post { get; set; }
		public string PostBody { get; set; }
		public bool PostJson { get; set; }
		public int Interval { get; set; }
	}
}
