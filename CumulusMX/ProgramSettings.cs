using System;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack.Text;

namespace CumulusMX
{
	public class ProgramSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it

			var startuptask = new JsonProgramSettingsTask()
			{
				task = cumulus.ProgramOptions.StartupTask,
				taskparams = cumulus.ProgramOptions.StartupTaskParams,
				wait = cumulus.ProgramOptions.StartupTaskWait
			};

			var startup = new JsonProgramSettingsStartupOptions()
			{
				startuphostping = cumulus.ProgramOptions.StartupPingHost,
				startuppingescape = cumulus.ProgramOptions.StartupPingEscapeTime,
				startupdelay = cumulus.ProgramOptions.StartupDelaySecs,
				startupdelaymaxuptime = cumulus.ProgramOptions.StartupDelayMaxUptime,
				startuptask = startuptask
			};

			var shutdowntask = new JsonProgramSettingsTask()
			{
				task = cumulus.ProgramOptions.ShutdownTask,
				taskparams = cumulus.ProgramOptions.ShutdownTaskParams
			};

			var shutdown = new JsonProgramSettingsShutdownOptions()
			{
				datastoppedexit = cumulus.ProgramOptions.DataStoppedExit,
				datastoppedmins = cumulus.ProgramOptions.DataStoppedMins,
				shutdowntask = shutdowntask
			};

			var logging = new JsonProgramSettingsLoggingOptions()
			{
				debuglogging = cumulus.ProgramOptions.DebugLogging,
				datalogging = cumulus.ProgramOptions.DataLogging,
				ftplogging = cumulus.FtpOptions.Logging,
				emaillogging = cumulus.SmtpOptions.Logging,
				spikelogging = cumulus.ErrorLogSpikeRemoval,
				errorlistlevel = (int) cumulus.ErrorListLoggingLevel
			};

			var options = new JsonProgramSettingsGeneralOptions()
			{
				stopsecondinstance = cumulus.ProgramOptions.WarnMultiple,
				listwebtags = cumulus.ProgramOptions.ListWebTags
			};

			var culture = new JsonProgramSettingsCultureOptions()
			{
				removespacefromdateseparator = cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator,
				timeFormat = cumulus.ProgramOptions.TimeFormat
			};

			var security = new JsonProgramSettingsSecurity()
			{
				securesettings = cumulus.ProgramOptions.SecureSettings,
				username = cumulus.ProgramOptions.SettingsUsername,
				password = cumulus.ProgramOptions.SettingsPassword,
			};

			var settings = new JsonProgramSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				startup = startup,
				shutdown = shutdown,
				logging = logging,
				options = options,
				culture = culture,
				security = security
			};

			//return JsonConvert.SerializeObject(data);
			return JsonSerializer.SerializeToString(settings);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			var returnMessage = "success";
			JsonProgramSettings settings;
			context.Response.StatusCode = 200;

			// get the response
			try
			{
				cumulus.LogMessage("Updating Program settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.DeserializeFromString<JsonProgramSettings>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Program Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Program Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.ProgramOptions.EnableAccessibility = settings.accessible;
				cumulus.ProgramOptions.StartupPingHost = (settings.startup.startuphostping ?? string.Empty).Trim();
				cumulus.ProgramOptions.StartupPingEscapeTime = settings.startup.startuppingescape;
				cumulus.ProgramOptions.StartupDelaySecs = settings.startup.startupdelay;
				cumulus.ProgramOptions.StartupDelayMaxUptime = settings.startup.startupdelaymaxuptime;

				cumulus.ProgramOptions.StartupTask = (settings.startup.startuptask.task ?? string.Empty).Trim();
				cumulus.ProgramOptions.StartupTaskParams = (settings.startup.startuptask.taskparams ?? string.Empty).Trim();
				cumulus.ProgramOptions.StartupTaskWait = settings.startup.startuptask.wait;

				cumulus.ProgramOptions.ShutdownTask = (settings.shutdown.shutdowntask.task ?? string.Empty).Trim();
				cumulus.ProgramOptions.ShutdownTaskParams = (settings.shutdown.shutdowntask.taskparams ?? string.Empty).Trim();

				cumulus.ProgramOptions.DataStoppedExit = settings.shutdown.datastoppedexit;
				cumulus.ProgramOptions.DataStoppedMins = settings.shutdown.datastoppedmins;

				cumulus.ProgramOptions.DebugLogging = settings.logging.debuglogging;
				cumulus.ProgramOptions.DataLogging = settings.logging.datalogging;
				cumulus.SmtpOptions.Logging = settings.logging.emaillogging;
				cumulus.ErrorLogSpikeRemoval = settings.logging.spikelogging;
				cumulus.ErrorListLoggingLevel = (Cumulus.LogLevel) settings.logging.errorlistlevel;

				cumulus.ProgramOptions.WarnMultiple = settings.options.stopsecondinstance;
				cumulus.ProgramOptions.ListWebTags = settings.options.listwebtags;
				cumulus.ProgramOptions.TimeFormat = settings.culture.timeFormat;

				// Does the culture need to be tweaked - either way
				if (cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator != settings.culture.removespacefromdateseparator)
				{
					cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator = settings.culture.removespacefromdateseparator;
					returnMessage = "You must restart Cumulus for the Locale setting change to take effect";
				}

				cumulus.ProgramOptions.SecureSettings = settings.security.securesettings;
				cumulus.ProgramOptions.SettingsUsername = (settings.security.username ?? string.Empty).Trim();
				cumulus.ProgramOptions.SettingsPassword = (settings.security.password ?? string.Empty).Trim();

				if (cumulus.ProgramOptions.TimeFormat == "t")
					cumulus.ProgramOptions.TimeFormatLong = "T";
				else if (cumulus.ProgramOptions.TimeFormat == "h:mm tt")
					cumulus.ProgramOptions.TimeFormatLong = "h:mm:ss tt";
				else
					cumulus.ProgramOptions.TimeFormatLong = "HH:mm:ss";


				if (settings.logging.ftplogging != cumulus.FtpOptions.Logging)
				{
					if (settings.logging.ftplogging)
					{
						cumulus.ftpLogfile = cumulus.RemoveOldDiagsFiles("FTP");
						cumulus.CreateFtpLogFile();
					}
					cumulus.FtpOptions.Logging = settings.logging.ftplogging;
				}
			}
			catch (Exception ex)
			{
				var msg = "Error processing Program Options: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				errorMsg += msg + "\n\n";
				context.Response.StatusCode = 500;
			}

			// Save the settings
			cumulus.WriteIniFile();

			return context.Response.StatusCode == 200 ? returnMessage : errorMsg;
		}
	}

	public class JsonProgramSettings
	{
		public bool accessible { get; set; }
		public JsonProgramSettingsStartupOptions startup { get; set; }
		public JsonProgramSettingsShutdownOptions shutdown { get; set; }
		public JsonProgramSettingsLoggingOptions logging { get; set; }
		public JsonProgramSettingsGeneralOptions options { get; set; }
		public JsonProgramSettingsCultureOptions culture { get; set; }
		public JsonProgramSettingsSecurity security { get; set; }
	}

	public class JsonProgramSettingsStartupOptions
	{
		public string startuphostping { get; set; }
		public int startuppingescape { get; set; }
		public int startupdelay { get; set; }
		public int startupdelaymaxuptime { get; set; }
		public JsonProgramSettingsTask startuptask { get; set; }
	}

	public class JsonProgramSettingsTask
	{
		public string task { get; set; }
		public string taskparams { get; set; }
		public bool wait { get; set; }
	}

	public class JsonProgramSettingsLoggingOptions
	{
		public bool debuglogging { get; set; }
		public bool datalogging { get; set; }
		public bool ftplogging { get; set; }
		public bool emaillogging { get; set; }
		public bool spikelogging { get; set; }
		public int errorlistlevel { get; set; }
	}
	public class JsonProgramSettingsGeneralOptions
	{
		public bool stopsecondinstance { get; set; }
		public bool listwebtags { get; set; }
	}
	public class JsonProgramSettingsCultureOptions
	{
		public bool removespacefromdateseparator { get; set; }
		public string timeFormat { get; set; }
	}
	public class JsonProgramSettingsShutdownOptions
	{
		public bool datastoppedexit { get; set; }
		public int datastoppedmins { get; set; }
		public JsonProgramSettingsTask shutdowntask { get; set; }
	}
	public class JsonProgramSettingsSecurity
	{
		public bool securesettings { get; set;}
		public string username { get; set; }
		public string password { get; set; }
	}
}
