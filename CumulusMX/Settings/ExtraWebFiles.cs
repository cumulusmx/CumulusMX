using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using EmbedIO;


namespace CumulusMX.Settings
{
	internal class ExtraWebFiles(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetExtraWebFilesData()
		{
			var settings = new ExtraWebFilesSettings()
			{
				Accessible = cumulus.ProgramOptions.EnableAccessibility,
				Files = cumulus.ExtraFiles
			};

			return JsonSerializer.Serialize(settings);
		}

		public string UpdateExtraWebFiles(IHttpContext context)
		{
			var retVal = "success";
			var json = string.Empty;
			ExtraWebFilesSettings settings;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<ExtraWebFilesSettings>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Extra Web File Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Extra Web File Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			try
			{
				cumulus.ExtraFiles.Clear();
				cumulus.ActiveExtraFiles.Clear();

				for (var i = 0; i < settings.Files.Count; i++)
				{
					if (string.IsNullOrEmpty(settings.Files[i].LocalFilename) && string.IsNullOrEmpty(settings.Files[i].DestFilename))
					{
						// with no filenames defined, do not save the entry
						continue;
					}

					if (string.IsNullOrEmpty(settings.Files[i].LocalFilename) || string.IsNullOrEmpty(settings.Files[i].DestFilename))
					{
						settings.Files[i].Enabled = false;
					}

					if (settings.Files[i].Type == 3 | settings.Files[i].Type == 4)
					{
						settings.Files[i].SetInitialNextInterval(DateTime.Now);
					}

					cumulus.ExtraFiles.Add(settings.Files[i]);

					if (settings.Files[i].Enabled)
					{
						cumulus.ActiveExtraFiles.Add(settings.Files[i]);
					}
				}

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("Error updating Extra Web file settings: " + ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return retVal;
		}
	}

	public class ExtraWebFilesSettings
	{
		public bool Accessible { get; set; }
		public List<ExtaWebFilesItem> Files { get; set; }
	}

	public class ExtaWebFilesItem
	{
		public bool Enabled { get; set; }
		public string LocalFilename { get; set; }
		public string DestFilename { get; set; }
		public bool Process { get; set; }
		public bool Upload { get; set; }
		/// 0-Realtime
		/// 1-Interval
		/// 2-EoD
		/// 3-Custom Interval
		/// 4-Scheduled
		/// </summary>
		public int Type { get; set; }
		public int Interval { get; set; } = 10;
		[JsonIgnore]
		public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
		[JsonPropertyName("StartTime")]
		public string StartTimeString
		{
			get => StartTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
			set => StartTime = TimeSpan.ParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture);
		}
		public bool Utf8 { get; set; }
		public bool Binary { get; set; }
		public bool Incremental { get; set; }
		public DateTime NextUpload { get; set; }

		public ExtaWebFilesItem()
		{
			NextUpload = DateTime.MinValue;
			StartTime = TimeSpan.Zero;
		}

		public void SetInitialNextInterval(DateTime now)
		{
			// We need to set a specific time for custom intervals and scheduled
			NextUpload = now.Date + StartTime;

			while (NextUpload < now)
			{
				NextUpload = NextUpload.AddMinutes(Interval);
			}
		}

		public void SetNextInterval(DateTime now)
		{
			/*
			if (Type == 4)
			{
				// We always revert to the start time so we remain consistent across DST changes
				NextUpload = now.Date + StartTime;

				while (NextUpload < now)
				{
					NextUpload = NextUpload.AddMinutes(Interval);
				}
			}
			else
			{
				NextUpload = now.RoundTimeUpToInterval(TimeSpan.FromMinutes(Interval));
			}
			*/

			NextUpload = NextUpload.AddMinutes(Interval);

			// If timed and we have rolled over a day and the next interval would be prior to the start time?
			// if so, bump up the next interval to the daily start time
			if (Type == 4 && NextUpload.TimeOfDay < StartTime)
				NextUpload = NextUpload.Date + StartTime;
		}
		[JsonIgnore]
		public string logFileLastFileName { get; set; }
		[JsonIgnore]
		public int logFileLastLineNumber { get; set; }
	}
}
