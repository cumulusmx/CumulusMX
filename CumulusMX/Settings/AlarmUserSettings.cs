using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack;


namespace CumulusMX.Settings
{
	internal class AlarmUserSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetAlpacaFormData()
		{

			var settings = new Settings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				alarms = cumulus.UserAlarms
			};

			return settings.ToJson();
		}

		public string UpdateConfig(IHttpContext context)
		{
			var json = string.Empty;
			Settings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = json.FromJson<Settings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing User Alarm Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("User Alarm Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating User Alarm settings");

				// clear the existing alarms
				cumulus.UserAlarms.Clear();

				for (var i = 0; i < settings.alarms.Count; i++)
				{
					cumulus.UserAlarms.Add(new AlarmUser((AlarmIds)(101 + i), settings.alarms[i].Name, settings.alarms[i].Type, settings.alarms[i].WebTag, cumulus)
					{
						Enabled = settings.alarms[i].Enabled,
						Value = settings.alarms[i].Value,
						Email = settings.alarms[i].Email,
						EmailMsg = settings.alarms[i].EmailMsg,
						BskyFile = settings.alarms[i].BskyFile,
						Action = settings.alarms[i].Action,
						ActionParams = settings.alarms[i].ActionParams,
						Latch = settings.alarms[i].Latch,
						LatchHours = settings.alarms[i].LatchHours,
						Units = settings.alarms[i].Units,
						TriggerThreshold = settings.alarms[i].TriggerThreshold
					});
				}

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				var msg = "HTTP file: Error processing settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				context.Response.StatusCode = 500;
				return msg;
			}
			return "success";
		}

		private sealed class Settings
		{
			public bool accessible { get; set; }
			public List<AlarmUser> alarms { get; set; }
		}
	}
}
