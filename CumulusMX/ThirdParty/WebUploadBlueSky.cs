using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using ServiceStack;
using ServiceStack.Text;

using Swan.Formatters;

using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static SQLite.SQLite3;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadBlueSky : WebUploadServiceBase
	{
		private string authToken;
		private string did;

		public CancellationToken CancelToken { get; set; }
		public string ContentTemplate { get; set; } = string.Empty;
		public string BaseUrl { get; set; }
		public string Language { get; set; }
		public TimeSpan[] TimedPosts { get; set; }
		public int TimedPostsCount {
			get
			{
				var cnt = 0;
				for (var i = 0; i < TimedPosts.Length; i++)
				{
					if (TimedPosts[i] < TimeSpan.MaxValue)
					{
						cnt++;
					}
				}
				return cnt;
			}
		}


		public WebUploadBlueSky(Cumulus cumulus, string name) : base(cumulus, name)
		{
			TimedPosts = new TimeSpan[5];
			for (var i = 0; i < TimedPosts.Length; i++)
			{
				TimedPosts[i] = TimeSpan.MaxValue;
			}
		}

		internal override async Task DoUpdate(DateTime timestamp)
		{
			// do nothing
		}

		internal async Task DoUpdate(string content)
		{
			Updating = true;
			var auth = await authenticate();

			if (!auth)
			{
				cumulus.LogMessage("BlueSky: Authentication failed");
				Updating = false;
				return;
			}

			await createPost(content);

			Updating = false;
		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = null;
			return null;
		}


		private async Task<bool> authenticate()
		{
			var url = BaseUrl + "/xrpc/com.atproto.server.createSession";
			var body = "{\"identifier\":\"" + ID + "\",\"password\":\"" + PW + "\"}";
			var data = new StringContent(body, Encoding.UTF8, "application/json");

			// we will try this twice in case the first attempt fails
			var maxRetryAttempts = 2;
			var delay = maxRetryAttempts * 5.0;

			for (int retryCount = maxRetryAttempts; retryCount >= 0; retryCount--)
			{
				try
				{
					using var response = await cumulus.MyHttpClient.PostAsync(url, data, CancelToken);
					var responseBodyAsText = await response.Content.ReadAsStringAsync(CancelToken);


					if (response.StatusCode == HttpStatusCode.OK)
					{
						cumulus.LogDebugMessage("BlueSky: Authentication Response: " + response.StatusCode);

						var respObj = responseBodyAsText.FromJson<AuthResponse>();

						if (respObj.accessJwt != null && respObj.did != null)
						{
							cumulus.LogDebugMessage("BlueSky: Authentication successful");
							authToken = respObj.accessJwt;
							did = respObj.did;
							cumulus.ThirdPartyAlarm.Triggered = false;
							Updating = false;
							return true;
						}
						else
						{
							return false;
						}
					}
					else if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						var err = responseBodyAsText.FromJson<ErrorResp>();

						cumulus.LogWarningMessage($"BlueSky: Error - Authentication failed. Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}");
						cumulus.ThirdPartyAlarm.LastMessage = "BlueSky: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return false;
					}
					else
					{
						var err = responseBodyAsText.FromJson<ErrorResp>();

						if (retryCount == 0)
						{
							cumulus.ThirdPartyAlarm.LastMessage = $"BlueSky: Authentication HTTP Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}";
							cumulus.ThirdPartyAlarm.Triggered = true;
						}
						else
						{
							cumulus.LogWarningMessage($"BlueSky Authentication Response: ERROR - Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}");
							cumulus.LogMessage($"BlueSky: Authentication Retrying in {delay / retryCount} seconds");

							await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
						}
					}
				}
				catch (Exception ex)
				{
					string msg;

					if (retryCount == 0)
					{
						if (ex.InnerException is TimeoutException)
						{
							msg = $"BlueSky: Authentication Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds";
							cumulus.LogWarningMessage(msg);
						}
						else
						{
							msg = "BlueSky: " + ex.Message;
							cumulus.LogExceptionMessage(ex, "BlueSky authentication error");
						}

						cumulus.ThirdPartyAlarm.LastMessage = msg;
						cumulus.ThirdPartyAlarm.Triggered = true;
					}
					else
					{
						if (ex.InnerException is TimeoutException)
						{
							cumulus.LogWarningMessage($"BlueSky: Authentication Request exceeded the response timeout of {cumulus.MyHttpClient.Timeout.TotalSeconds} seconds");
						}
						else
						{
							cumulus.LogWarningMessage("BlueSky: Authentication Error - " + ex.Message);
						}

						cumulus.LogMessage($"BlueSky: Authentication Retrying in {delay / retryCount} seconds");

						await Task.Delay(TimeSpan.FromSeconds(delay / retryCount));
					}
				}
			}

			Updating = false;
			return false;
		}

		private async Task createPost(string content)
		{
			cumulus.LogDebugMessage("BlueSky: Creating post");
			var url = BaseUrl + "/xrpc/com.atproto.repo.createRecord";

			try
			{
				var facets = detectLinkFacets(content, out var modContent);

				var body = new Content
				{
					repo = did,
					record = new Record
					{
						text = modContent,
						langs = [Language]
					}
				};

				var tagFacets = detectTagFacets(modContent);

				if (tagFacets.Count > 0)
				{
					facets.AddRange(tagFacets);
				}

				if (facets.Count > 0)
				{
					body.record.facets = facets.ToArray();
				}

				var bodyText = body.ToJson();

				cumulus.LogDataMessage("BlueSky: Post content: " + modContent);

				var data = new StringContent(bodyText, Encoding.UTF8, "application/json");

				var request = new HttpRequestMessage(HttpMethod.Post, url);
				request.Headers.Add("Authorization", "Bearer " + authToken);
				request.Content = data;

				using var response = await cumulus.MyHttpClient.SendAsync(request);
				var responseBodyAsText = await response.Content.ReadAsStringAsync(CancelToken);

				if (response.StatusCode == HttpStatusCode.OK)
				{
					cumulus.LogDebugMessage("BlueSky: Post response = OK");
				}
				else
				{
					var err = responseBodyAsText.FromJson<ErrorResp>();

					cumulus.LogWarningMessage($"BlueSky: Error - Post failed. Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}");
					cumulus.ThirdPartyAlarm.LastMessage = $"BlueSky: Post HTTP Response code = {response.StatusCode},  Error = {err.error}, Message = {err.message}";
					cumulus.ThirdPartyAlarm.Triggered = true;
				}

				cumulus.LogDebugMessage("BlueSky: Post ended");
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "BlueSky createPost error");
			}
		}

		private static List<Facet> detectLinkFacets(string content, out string modifiedContent)
		{
			var facets = new List<Facet>();

			var regex = new Regex(@"(https?:\/\/[\S]+)\|([\S\s]+?)\|", RegexOptions.Compiled);

			Match match;
			var pos = 0;

			while ((match = regex.Match(content[pos..])).Success)
			{
				if (match.Groups.Count == 3)
				{
					string url = match.Groups[1].Value;
					string label = match.Groups[2].Value;

					var facet = new Facet();

					int start = match.Groups[1].Index;

					// remove the URL plus pipe character from the start of the text
					content = content.Remove(start, url.Length + 1);
					// remove the pipe character from the end of the text
					content = content.Remove(start + label.Length, 1);

					facet.index.byteStart = GetUtf8BytePosition(content, start);
					facet.index.byteEnd = GetUtf8BytePosition(content, start + label.Length);


					facet.features[0].type = "app.bsky.richtext.facet#link";
					facet.features[0].uri = url;

					facets.Add(facet);

					pos = start + label.Length;

					if (pos >= content.Length)
					{
						break;
					}
				}
			}

			modifiedContent = content;

			return facets;
		}


		private static List<Facet> detectTagFacets(string content)
		{
			var facets = new List<Facet>();

			var regex = new Regex(@"#\w+");

			foreach (Match match in regex.Matches(content))
			{
				if (match.Groups.Count == 1)
				{
					string tag = match.Groups[0].Value;

					var facet = new Facet();

					int start = match.Groups[0].Index;

					facet.index.byteStart = GetUtf8BytePosition(content, start);
					facet.index.byteEnd = GetUtf8BytePosition(content, start + tag.Length);


					facet.features[0].type = "app.bsky.richtext.facet#tag";
					// remove the # from the start of the tag
					facet.features[0].tag = tag.Remove(0, 1);

					facets.Add(facet);
				}
			}

			return facets;
		}

		private static int GetUtf8BytePosition(string text, int index)
		{
			var substring = text[..index];

			return Encoding.UTF8.GetByteCount(substring);
		}

		private sealed class AuthResponse
		{
			public string accessJwt { get; set; }
			public string did { get; set; }
		}

		private sealed class FacetIndex
		{
			public int byteStart { get; set; }
			public int byteEnd { get; set; }
		}

		private sealed class FacetFeature
		{
			[IgnoreDataMember]
			public string type { get; set; }
			[DataMember(Name = "$type")]
			public string dollarType
			{
				get => type;
			}

			public string uri { get; set; }
			public string tag { get; set; }
		}

		private sealed class Facet
		{
			public FacetIndex index { get; set; }
			public FacetFeature[] features { get; set; }

			public Facet()
			{
				index = new FacetIndex();
				features = new FacetFeature[1];
				features[0] = new FacetFeature();
			}
		}

		private sealed class Record
		{
			public string text { get; set; }
			public string createdAt { get; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
			public string[] langs { get; set; }

			public Facet[] facets { get; set; }
		}

		private sealed class Content
		{
			public string repo { get; set; }

			[IgnoreDataMember]
			public string type { get; } = "app.bsky.feed.post";
			[DataMember(Name = "$type")]
			public string dollarType
			{
				get => type;
			}

			public string collection { get; } = "app.bsky.feed.post";
			public Record record { get; set; }
		}

		private sealed class ErrorResp
		{
			public string error { get; set; }
			public string message { get; set; }
		}
	}
}
