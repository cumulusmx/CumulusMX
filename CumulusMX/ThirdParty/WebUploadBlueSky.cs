using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using SixLabors.ImageSharp;


namespace CumulusMX.ThirdParty
{
	internal partial class WebUploadBlueSky : WebUploadServiceBase
	{
		private string authToken;
		private string did;
		private readonly Dictionary<string, string> dids = [];

		public CancellationToken CancelToken { get; set; }
		public string ContentTemplate { get; set; } = string.Empty;
		public string BaseUrl { get; set; }
		public string Language { get; set; }
		public TimeSpan[] TimedPostsTime { get; set; }
		public string[] TimedPostsFile { get; set; }
		public string[] VariablePostsTime { get; set; }
		public string[] VariablePostsFile { get; set; }
		public int TimedPostsCount {
			get
			{
				var cnt = 0;
				for (var i = 0; i < TimedPostsTime.Length; i++)
				{
					if (TimedPostsTime[i] < TimeSpan.MaxValue && !string.IsNullOrEmpty(TimedPostsFile[i]))
					{
						cnt++;
					}
				}
				return cnt;
			}
		}

		private readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);

		public WebUploadBlueSky(Cumulus cumulus, string name) : base(cumulus, name)
		{
			TimedPostsTime = new TimeSpan[10];
			TimedPostsFile = new string[10];
			VariablePostsTime = new string[4];
			VariablePostsFile = new string[4];

			for (var i = 0; i < TimedPostsTime.Length; i++)
			{
				TimedPostsTime[i] = TimeSpan.MaxValue;
				TimedPostsFile[i] = null;
			}
		}

		internal override async Task DoUpdate(DateTime timestamp)
		{
			await Task.CompletedTask;
		}

		internal async Task DoUpdate(string content)
		{
			Updating = true;

			await _updateSemaphore.WaitAsync();

			try
			{
				var auth = await authenticate();

				if (!auth)
				{
					cumulus.LogMessage("BlueSky: Authentication failed");
					Updating = false;
					return;
				}

				await createPost(content);
			}
			finally
			{
				await Task.Delay(5000);
				_updateSemaphore.Release();
				Updating = false;
			}
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

						var respObj = JsonSerializer.Deserialize<AuthResponse>(responseBodyAsText);

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
						var err = JsonSerializer.Deserialize<ErrorResp>(responseBodyAsText);

						cumulus.LogWarningMessage($"BlueSky: Error - Authentication failed. Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}");
						cumulus.ThirdPartyAlarm.LastMessage = "BlueSky: Unauthorized, check credentials";
						cumulus.ThirdPartyAlarm.Triggered = true;
						Updating = false;
						return false;
					}
					else
					{
						var err = JsonSerializer.Deserialize<ErrorResp>(responseBodyAsText);

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
				var body = new Content
				{
					repo = did,
					record = new Record
					{
						langs = [Language]
					}
				};

				// Must do images first as it affects the offsets for everything else
				var embed = detectImages(content, out var modContent);

				if (embed != null)
				{
					body.record.embed = embed;
				}

				// Must do links next as it affects the offsets for tags and mentions
				var facets = detectLinks(modContent, out modContent);

				// we have finsihed altering the content
				body.record.text = modContent;

				var tagFacets = detectTags(modContent);

				if (tagFacets.Count > 0)
				{
					facets.AddRange(tagFacets);
				}

				tagFacets = detectMentions(modContent);

				if (tagFacets.Count > 0)
				{
					facets.AddRange(tagFacets);
				}

				if (facets.Count > 0)
				{
					body.record.facets = facets.ToArray();
				}

				var bodyText = JsonSerializer.Serialize(body);

				cumulus.LogDataMessage("BlueSky: Post JSON: " + bodyText);

				var data = new StringContent(bodyText, Encoding.UTF8, "application/json");

				var request = new HttpRequestMessage(HttpMethod.Post, url);
				request.Headers.Add("Authorization", "Bearer " + authToken);
				request.Content = data;

				using var response = await cumulus.MyHttpClient.SendAsync(request, CancelToken);
				var responseBodyAsText = await response.Content.ReadAsStringAsync(CancelToken);

				if (response.StatusCode == HttpStatusCode.OK)
				{
					cumulus.LogDebugMessage("BlueSky: Post response = OK");
				}
				else
				{
					var err = JsonSerializer.Deserialize<ErrorResp>(responseBodyAsText);

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

		private static List<Facet> detectLinks(string content, out string modifiedContent)
		{
			// present in the text as "https://url|label|"

			var facets = new List<Facet>();

			var regex = RegexLink();

			Match match;
			while (true)
			{
				match = regex.Match(content);
				if (!match.Success) break;

				string url = match.Groups[1].Value;
				string label = match.Groups[2].Value;

				int start = match.Groups[1].Index;

				// remove the URL plus pipe character from the start of the text
				content = content.Remove(start, url.Length + 1);
				// remove the pipe character from the end of the text
				content = content.Remove(start + label.Length, 1);

				var facet = new Facet();
				facet.index.byteStart = GetUtf8BytePosition(content, start);
				facet.index.byteEnd = GetUtf8BytePosition(content, start + label.Length);
				facet.features[0].type = "app.bsky.richtext.facet#link";
				facet.features[0].uri = url;

				facets.Add(facet);

				if (start + label.Length >= content.Length) break;
			}

			modifiedContent = content;

			return facets;
		}


		private static List<Facet> detectTags(string content)
		{
			// present in the text as "#tagname"

			var facets = new List<Facet>();

			var regex = RegexHashtag();

			foreach (var (tag, start) in
				from Match match in regex.Matches(content)
				where match.Groups.Count == 1
				let tag = match.Groups[0].Value

				let start = match.Groups[0].Index
				select (tag, start))
			{
				var facet = new Facet();
				facet.index.byteStart = GetUtf8BytePosition(content, start);
				facet.index.byteEnd = GetUtf8BytePosition(content, start + tag.Length);
				facet.features[0].type = "app.bsky.richtext.facet#tag";
				// remove the # from the start of the tag
				facet.features[0].tag = tag.Remove(0, 1);
				facets.Add(facet);
			}

			return facets;
		}

		private List<Facet> detectMentions(string content)
		{
			// present in the text as "@username"

			var facets = new List<Facet>();

			var regex = RegexMention();

			foreach (var (id, start) in
				from Match match in regex.Matches(content)
				where match.Groups.Count == 2
				let id = match.Groups[1].Value

				let start = match.Groups[0].Index
				select (id, start))
			{
				var resolvedDid = resolveDid(id);
				if (string.IsNullOrEmpty(resolvedDid))
				{
					continue;
				}

				var facet = new Facet();
				facet.index.byteStart = GetUtf8BytePosition(content, start);
				facet.index.byteEnd = GetUtf8BytePosition(content, start + id.Length + 1);
				facet.features[0].type = "app.bsky.richtext.facet#mention";
				facet.features[0].did = resolvedDid;
				facets.Add(facet);
			}

			return facets;
		}


		private Embed detectImages(string content, out string modifiedContent)
		{
			// present in the text as "image:filepath|Alt text|

			var images = new List<Images>();
			int noOfImages = 0; // only allowed to attach 4 per post

			var regex = RegexImage();

			var embed = new Embed();

			Match match;
			while (true)
			{
				match = regex.Match(content);
				if (!match.Success) break;

				// groups
				// 1 = image:
				// 2 = filepath
				// 3 = alt text
				if (match.Groups.Count != 4) continue;

				if (++noOfImages > 4) break;

				string filepath = match.Groups[2].Value;
				string altText = match.Groups[3].Value;

				int start = match.Groups[1].Index;

				// remove the whole "image:url|alt text|"
				content = content.Remove(start, match.Length);

				var blob = uploadBlob(filepath);
				if (blob == null)
				{
					continue;
				}

				var image = new Images
				{
					alt = altText,
					image = blob
				};

				images.Add(image);

				if (start >= content.Length) break;
			}

			modifiedContent = content;

			if (images.Count > 0)
			{
				embed.images = images.ToArray();
				return embed;
			}

			return null;
		}


		private string resolveDid(string uid)
		{
			var url = BaseUrl + "/xrpc/com.atproto.identity.resolveHandle?handle=";

			try
			{

				if (dids.TryGetValue(uid, out var value))
				{
					return value;
				}

				var request = new HttpRequestMessage(HttpMethod.Get, url + uid);
				request.Headers.Add("Authorization", "Bearer " + authToken);

				using var response = cumulus.MyHttpClient.SendAsync(request, CancelToken).Result;
				var responseBodyAsText = response.Content.ReadAsStringAsync(CancelToken).Result;

				if (response.StatusCode == HttpStatusCode.OK)
				{
					cumulus.LogDebugMessage("BlueSky: Resolve Handle response = OK");

					var resp = JsonSerializer.Deserialize<ResolveHandleResp>(responseBodyAsText);

					if (string.IsNullOrEmpty(resp.did) || !resp.did.StartsWith("did:"))
					{
						return null;
					}

					cumulus.LogDataMessage("BlueSky: Resolve Handle response = " + responseBodyAsText);

					dids[uid] = resp.did;
					return resp.did;
				}
				else
				{
					var err = JsonSerializer.Deserialize<ErrorResp>(responseBodyAsText);

					cumulus.LogWarningMessage($"BlueSky: Error - Resolve Handle failed. Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}");
					cumulus.ThirdPartyAlarm.LastMessage = $"BlueSky: Resolve Handle HTTP Response code = {response.StatusCode},  Error = {err.error}, Message = {err.message}";
					cumulus.ThirdPartyAlarm.Triggered = true;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "BlueSky ResolveDid error");
			}
			return null;
		}

		private Blob uploadBlob(string filepath)
		{
			var apiUrl = BaseUrl + "/xrpc/com.atproto.repo.uploadBlob";
			byte[] imageData;
			string imageType;

			if (filepath.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) || filepath.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
			{
				// get file from URL
				try
				{
					using var httpStream = cumulus.MyHttpClient.GetStreamAsync(filepath).Result;
					var image = SixLabors.ImageSharp.Image.Load(httpStream);
					var format = image.Metadata.DecodedImageFormat;
					imageType = format.DefaultMimeType;
					using var ms = new MemoryStream();
					image.SaveAsync(ms, format, CancelToken).Wait();
					imageData = ms.ToArray();
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "BlueSky: Get File from URL error - " + filepath);
					return null;
				}
			}
			else
			{
				try
				{
					imageData = File.ReadAllBytes(filepath);

					imageType = filepath.Substring(filepath.LastIndexOf('.') + 1) switch
					{
						"jpg" => "image/jpeg",
						"jpeg" => "image/jpeg",
						"png" => "image/png",
						_ => string.Empty
					};
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "BlueSky: Get File error - " + filepath);
					return null;
				}
			}

			if (imageData.Length > 1000000)
			{
				cumulus.LogWarningMessage("Bluesky: Image too large - " + filepath);
				return null;
			}

			if (imageType == string.Empty)
			{
				cumulus.LogDataMessage("Bluesky: Unknown image type - " + filepath);
				return null;
			}

			var imageContent = new ByteArrayContent(imageData);
			imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageType);
			imageContent.Headers.ContentLength = imageData.Length;

			var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
			request.Headers.Add("Authorization", "Bearer " + authToken);
			request.Content = imageContent;

			var response = cumulus.MyHttpClient.SendAsync(request, CancelToken).Result;
			var responseBodyAsText = response.Content.ReadAsStringAsync(CancelToken).Result;

			if (response.StatusCode == HttpStatusCode.OK)
			{
				cumulus.LogDebugMessage("BlueSky: Upload image response = OK");

				cumulus.LogDataMessage("BlueSky: Upload image response = " + responseBodyAsText);

				var root = JsonSerializer.Deserialize<BlobRoot>(responseBodyAsText);
				return root.blob;
			}
			else
			{
				var err = JsonSerializer.Deserialize<ErrorResp>(responseBodyAsText);

				cumulus.LogWarningMessage($"BlueSky: Error - Upload image failed. Response code = {response.StatusCode}, Error = {err.error}, Message = {err.message}");
				cumulus.ThirdPartyAlarm.LastMessage = $"BlueSky: Upload image HTTP Response code = {response.StatusCode},  Error = {err.error}, Message = {err.message}";
				cumulus.ThirdPartyAlarm.Triggered = true;
			}

			return null;
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
			[DataMember(Name = "$type")]
			public string type { get; set; }

			public string uri { get; set; }
			public string tag { get; set; }
			public string did { get; set; }
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

		private sealed class ImageAspect
		{
			public int width { get; set; }
			public int height { get; set; }
		}


		private sealed class Images
		{
			public string alt { get; set; }
			public Blob image { get; set; }
		}


		private sealed class Embed
		{
			[DataMember(Name = "$type")]
			public string type { get; set; } = "app.bsky.embed.images";

			public Images[] images { get; set; }
		}

		private sealed class BlobRef
		{
			[DataMember(Name = "$link")]
			public string link { get; set; }
		}

		private sealed class Blob
		{
			[DataMember(Name = "$type")]
			public string type { get; set; } = "blob";

			[DataMember(Name = "ref")]
			public BlobRef reference { get; set; }

			public string mimeType { get; set; }
			public int size { get; set; }
		}

		private sealed class BlobRoot
		{
			public Blob blob { get; set; }
		}

		private sealed class Record
		{
			public string text { get; set; }
			public string createdAt { get; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
			public string[] langs { get; set; }

			public Facet[] facets { get; set; }

			public Embed embed { get; set; }
		}

		private sealed class Content
		{
			public string repo { get; set; }

			[DataMember(Name = "$type")]
			public string type { get; set; } = "app.bsky.feed.post";

			public string collection { get; } = "app.bsky.feed.post";
			public Record record { get; set; }
		}

		private sealed class ErrorResp
		{
			public string error { get; set; }
			public string message { get; set; }
		}

		private sealed class ResolveHandleResp
		{
			public string did { get; set; }
		}

		[GeneratedRegex(@"#\w+")]
		private static partial Regex RegexHashtag();

		[GeneratedRegex(@"(https?:\/\/[^|]+?)\|([^|]+?)\|")]
		private static partial Regex RegexLink();

		[GeneratedRegex(@"@([\S]+)")]
		private static partial Regex RegexMention();


		[GeneratedRegex(@"(image:)([^|]+)\|([^|]+?)\|")]
		private static partial Regex RegexImage();
	}
}
