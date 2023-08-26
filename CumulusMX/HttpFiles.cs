using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using EmbedIO;

using ServiceStack;
using ServiceStack.Text;


namespace CumulusMX
{
	internal class HttpFiles
	{
		private readonly Cumulus cumulus;
		private readonly WeatherStation station;

		public HttpFiles(Cumulus cumulus, WeatherStation station)
		{
			this.cumulus = cumulus;
			this.station = station;
		}

		public string GetAlpacaFormData()
		{
			var images = new List<HttpFileProps>();

			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.HttpFilesConfig[i].Url) || !string.IsNullOrEmpty(cumulus.HttpFilesConfig[i].Remote))
				{
					images.Add(new HttpFileProps()
					{
						Enabled = cumulus.HttpFilesConfig[i].Enabled,
						Url = cumulus.HttpFilesConfig[i].Url,
						Remote = cumulus.HttpFilesConfig[i].Remote,
						Interval = cumulus.HttpFilesConfig[i].Interval,
						Upload = cumulus.HttpFilesConfig[i].Upload,
						Timed = cumulus.HttpFilesConfig[i].Timed,
						StartTime = cumulus.HttpFilesConfig[i].StartTime
					});
				}
			}

			var settings = new HttpFileSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				files = images
			};

			return settings.ToJson();
		}

		public string UpdateConfig(IHttpContext context)
		{
			string json = "";
			HttpFileSettings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<HttpFileSettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Http File Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Http File Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating http file settings");

				for (var i = 0; i < 10; i++)
				{
					if (i < settings.files.Count)
					{
						cumulus.HttpFilesConfig[i].Enabled = settings.files[i].Enabled;
						cumulus.HttpFilesConfig[i].Url = string.IsNullOrEmpty(settings.files[i].Url) ? null : settings.files[i].Url.Trim();
						cumulus.HttpFilesConfig[i].Remote = string.IsNullOrEmpty(settings.files[i].Remote) ? null : settings.files[i].Remote.Trim();
						cumulus.HttpFilesConfig[i].Interval = settings.files[i].Interval;
						cumulus.HttpFilesConfig[i].Upload = settings.files[i].Upload;
						cumulus.HttpFilesConfig[i].Timed = settings.files[i].Timed;

						// disable uploads if either the source or destination are blank
						if (null == cumulus.HttpFilesConfig[i].Url || null == cumulus.HttpFilesConfig[i].Remote)
							cumulus.HttpFilesConfig[i].Enabled = false;

						// if timed uploads are required, and the start-time has changed, then reset the nextUpload time
						if (settings.files[i].Timed && cumulus.HttpFilesConfig[i].StartTime != settings.files[i].StartTime)
						{
							cumulus.HttpFilesConfig[i].SetInitialNextInterval(DateTime.Now);
							cumulus.HttpFilesConfig[i].StartTime = settings.files[i].StartTime;
						}
						else if (!settings.files[i].Timed)
						{
							// if timed uploads are not required, reset the start time
							cumulus.HttpFilesConfig[i].StartTime = TimeSpan.Zero;
						}

					}
					else
					{
						cumulus.HttpFilesConfig[i].Enabled = false;
						cumulus.HttpFilesConfig[i].Url = null;
						cumulus.HttpFilesConfig[i].Remote = null;
					}
				}

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				var msg = "Error processing settings: " + ex.Message;
				cumulus.LogMessage(msg);
				context.Response.StatusCode = 500;
				return msg;
			}
			return "success";
		}

		public async Task DownloadHttpFile(string url, string filename)
		{
			string modUrl;

			if (url == "<ecowittcameraurl>")
			{
				if (string.IsNullOrEmpty(station.EcowittCameraUrl))
				{
					cumulus.LogMessage("DownloadHttpFile: The Ecowitt Camera URL is not available");
					return;
				}
				else
				{
					url = station.EcowittCameraUrl;
					// do not append timestamp, it is already unique
					modUrl = url;
				}
			}
			else
			{
				modUrl = url + (url.Contains("?") ? "&" : "?") + "_=" + DateTime.Now.ToUnixTime();
			}

			cumulus.LogDebugMessage($"DownloadHttpFile: Downloading from {url} to {filename}");

			try
			{
				using (var response = await Cumulus.MyHttpClient.GetAsync(new Uri(modUrl)))
				using (var fileStream = new FileStream(filename, FileMode.Create))
				{
					response.EnsureSuccessStatusCode();
					await response.Content.CopyToAsync(fileStream);
				}

				cumulus.LogDebugMessage($"DownloadHttpFile: Download from {url} to {filename} complete");
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, $"DownloadHttpFile: Error downloading from {url} to {filename}");
			}
		}

		public async Task<string> DownloadHttpFileBase64String(string url)
		{
			var modUrl = url + (url.Contains("?") ? "&" : "?") + "_=" + DateTime.Now.ToUnixTime();

			cumulus.LogDebugMessage($"DownloadHttpFileString: Downloading from {url}");

			try
			{
				string ret = null;
				var request = new HttpRequestMessage(HttpMethod.Get, modUrl);
				var sendTask = Cumulus.MyHttpClient.SendAsync(request);
				using (var response = sendTask.Result.EnsureSuccessStatusCode())
				{
					var bytes = await response.Content.ReadAsByteArrayAsync();

					if (bytes != null)
					{
						ret = Convert.ToBase64String(bytes);
					}
				}
				return ret;
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, $"DownloadHttpFileString: Error downloading from {url}");
				return null;
			}
		}

		public async Task<Stream> DownloadHttpFileStream(string url)
		{
			var modUrl = url + (url.Contains("?") ? "&" : "?") + "_=" + DateTime.Now.ToUnixTime();

			cumulus.LogDebugMessage($"DownloadHttpFileStream: Downloading from {url}");

			try
			{
				using (var request = new HttpRequestMessage(HttpMethod.Get, modUrl))
				using (var response = await Cumulus.MyHttpClient.SendAsync(request))
				{
					response.EnsureSuccessStatusCode();
					return response.Content.ReadAsStreamAsync().Result;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, $"DownloadHttpFileStream: Error downloading from {url}");
				return null;
			}
		}

		private class HttpFileSettings
		{
			public bool accessible { get; set; }
			public List<HttpFileProps> files { get; set; }
		}

	}
	public class HttpFileProps
	{
		public bool Enabled { get; set; }
		public string Url { get; set; }
		public string Remote { get; set; }
		public int Interval { get; set; }
		public bool Upload { get; set; }
		public bool Timed { get; set; }

		[IgnoreDataMember]
		public TimeSpan StartTime { get; set; }

		[DataMember(Name = "StartTimeStr")]
		public string StartTimeString
		{
			get => StartTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
			set => StartTime = TimeSpan.ParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture);
		}

		public DateTime NextDownload { get; set; }

		public HttpFileProps()
		{
			NextDownload = DateTime.MinValue;
			StartTime = TimeSpan.Zero;
		}

		public void SetInitialNextInterval(DateTime now)
		{
			// We only need to set a specific time for timed downloads
			if (Timed)
			{
				NextDownload = now.Date + StartTime;
			}
		}

		public void SetNextInterval(DateTime now)
		{
			if (Timed)
			{
				// We always revert to the start time so we remain consistent across DST changes
				NextDownload = now.Date + StartTime;
			}
			else if (NextDownload == DateTime.MinValue)
			{
				NextDownload = now;
			}

			// Not timed or timed and we have now set the start, add on intervals until we reach the future
			while (NextDownload < now)
			{
				NextDownload = NextDownload.AddMinutes(Interval);
			}

			// If timed and we have rolled over a day and the next interval would be prior to the start time?
			// if so, bump up the next interval to the daily start time
			if (Timed && NextDownload.TimeOfDay < StartTime)
				NextDownload = NextDownload.Date + StartTime;
		}
	}
}
