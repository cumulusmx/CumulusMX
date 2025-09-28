using System;
using System.IO;
using System.Net;
using System.Text.Json;

using EmbedIO;


namespace CumulusMX.Settings
{
	public class ProgramSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it

			var startuptask = new SettingsTask()
			{
				task = cumulus.ProgramOptions.StartupTask,
				taskparams = cumulus.ProgramOptions.StartupTaskParams,
				wait = cumulus.ProgramOptions.StartupTaskWait
			};

			var startup = new SettingsStartupOptions()
			{
				startuphostping = cumulus.ProgramOptions.StartupPingHost,
				startuppingescape = cumulus.ProgramOptions.StartupPingEscapeTime,
				startupdelay = cumulus.ProgramOptions.StartupDelaySecs,
				startupdelaymaxuptime = cumulus.ProgramOptions.StartupDelayMaxUptime,
				startuptask = startuptask
			};

			var shutdowntask = new SettingsTask()
			{
				task = cumulus.ProgramOptions.ShutdownTask,
				taskparams = cumulus.ProgramOptions.ShutdownTaskParams
			};

			var shutdown = new SettingsShutdownOptions()
			{
				datastoppedexit = cumulus.ProgramOptions.DataStoppedExit,
				datastoppedmins = cumulus.ProgramOptions.DataStoppedMins,
				shutdowntask = shutdowntask
			};

			var logging = new SettingsLoggingOptions()
			{
				debuglogging = cumulus.ProgramOptions.DebugLogging,
				datalogging = cumulus.ProgramOptions.DataLogging,
				ftplogging = cumulus.FtpOptions.Logging,
				ftplogginglevel = cumulus.FtpOptions.LoggingLevel,
				emaillogging = cumulus.SmtpOptions.Logging,
				spikelogging = cumulus.ErrorLogSpikeRemoval,
				errorlistlevel = (int) cumulus.ErrorListLoggingLevel
			};

			var paths = new SettingsPathOptions()
			{
				datapath = cumulus.ProgramOptions.DataPath,
				backuppath = cumulus.ProgramOptions.BackupPath
			};

			var options = new SettingsGeneralOptions()
			{
				stopsecondinstance = cumulus.ProgramOptions.WarnMultiple,
				listwebtags = cumulus.ProgramOptions.ListWebTags,
				usewebsockets = cumulus.ProgramOptions.UseWebSockets
			};

			var culture = new SettingsCultureOptions()
			{
				displayLang = cumulus.ProgramOptions.DisplayLanguage,
				removespacefromdateseparator = cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator,
				timeFormat = cumulus.ProgramOptions.TimeFormat,
				amPmLowerCase = cumulus.ProgramOptions.TimeAmPmLowerCase
			};

			var security = new SettingsSecurityOptions()
			{
				securesettings = cumulus.ProgramOptions.SecureSettings,
				username = cumulus.ProgramOptions.SettingsUsername,
				password = cumulus.ProgramOptions.SettingsPassword,
			};

			var settings = new Settings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				startup = startup,
				shutdown = shutdown,
				logging = logging,
				paths = paths,
				options = options,
				culture = culture,
				security = security
			};

			return JsonSerializer.Serialize(settings);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = string.Empty;
			var json = string.Empty;
			var returnMessage = "success";
			Settings settings;
			context.Response.StatusCode = 200;

			// get the response
			try
			{
				cumulus.LogMessage("Updating Program settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<Settings>(json);
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
				cumulus.ErrorListLoggingLevel = (Cumulus.MxLogLevel) settings.logging.errorlistlevel;

				cumulus.ProgramOptions.WarnMultiple = settings.options.stopsecondinstance;
				cumulus.ProgramOptions.ListWebTags = settings.options.listwebtags;
				cumulus.ProgramOptions.UseWebSockets = settings.options.usewebsockets;

				cumulus.ProgramOptions.DisplayLanguage = settings.culture.displayLang;

				cumulus.ProgramOptions.DataPath = settings.paths.datapath;
				cumulus.ProgramOptions.BackupPath = settings.paths.backuppath;

				cumulus.ProgramOptions.TimeFormat = settings.culture.timeFormat;
				if (settings.culture.amPmLowerCase != cumulus.ProgramOptions.TimeAmPmLowerCase)
				{
					cumulus.ProgramOptions.TimeAmPmLowerCase = settings.culture.amPmLowerCase;
					Utils.SetDateTimeAmPmDesignators(cumulus.ProgramOptions.TimeAmPmLowerCase);
				}

				// Does the culture need to be tweaked - either way
				if (cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator != settings.culture.removespacefromdateseparator)
				{
					cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator = settings.culture.removespacefromdateseparator;
					Utils.RemoveSpaceFromDateFormat(cumulus.ProgramOptions.Culture.RemoveSpaceFromDateSeparator);
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
					cumulus.FtpOptions.Logging = settings.logging.ftplogging;

					if (settings.logging.ftplogginglevel.HasValue)
					{
						cumulus.FtpOptions.LoggingLevel = settings.logging.ftplogginglevel.Value;
					}
					cumulus.SetupFtpLogging(cumulus.FtpOptions.Logging);
					cumulus.SetRealTimeFtpLogging(cumulus.FtpOptions.Logging);
				}
				else if (settings.logging.ftplogginglevel.HasValue && cumulus.FtpOptions.LoggingLevel != settings.logging.ftplogginglevel.Value)
				{
					cumulus.FtpOptions.LoggingLevel = settings.logging.ftplogginglevel.Value;
					cumulus.SetupFtpLogging(cumulus.FtpOptions.Logging);
					cumulus.SetRealTimeFtpLogging(cumulus.FtpOptions.Logging);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "Error processing Program Options");
				errorMsg += "Error processing Program Options: " + ex.Message + "\n\n";
				context.Response.StatusCode = 500;
			}

			// Save the settings
			cumulus.WriteIniFile();

			return context.Response.StatusCode == 200 ? returnMessage : errorMsg;
		}

		private class Settings
		{
			public bool accessible { get; set; }
			public SettingsStartupOptions startup { get; set; }
			public SettingsShutdownOptions shutdown { get; set; }
			public SettingsLoggingOptions logging { get; set; }
			public SettingsPathOptions paths { get; set; }
			public SettingsGeneralOptions options { get; set; }
			public SettingsCultureOptions culture { get; set; }
			public SettingsSecurityOptions security { get; set; }
		}

		private class SettingsStartupOptions
		{
			public string startuphostping { get; set; }
			public int startuppingescape { get; set; }
			public int startupdelay { get; set; }
			public int startupdelaymaxuptime { get; set; }
			public SettingsTask startuptask { get; set; }
		}

		private class SettingsTask
		{
			public string task { get; set; }
			public string taskparams { get; set; }
			public bool wait { get; set; }
		}

		private class SettingsLoggingOptions
		{
			public bool debuglogging { get; set; }
			public bool datalogging { get; set; }
			public bool ftplogging { get; set; }
			public int? ftplogginglevel { get; set; }
			public bool emaillogging { get; set; }
			public bool spikelogging { get; set; }
			public int errorlistlevel { get; set; }
		}

		private class SettingsPathOptions
		{
			public string datapath { get; set; }
			public string backuppath { get; set; }
		}

		private class SettingsGeneralOptions
		{
			public bool stopsecondinstance { get; set; }
			public bool listwebtags { get; set; }
			public bool usewebsockets { get; set; }
		}
		private class SettingsCultureOptions
		{
			public string displayLang { get; set; }
			public bool removespacefromdateseparator { get; set; }
			public string timeFormat { get; set; }
			public bool amPmLowerCase { get; set; }
		}
		private class SettingsShutdownOptions
		{
			public bool datastoppedexit { get; set; }
			public int datastoppedmins { get; set; }
			public SettingsTask shutdowntask { get; set; }
		}
		private class SettingsSecurityOptions
		{
			public bool securesettings { get; set; }
			public string username { get; set; }
			public string password { get; set; }
		}
	}
}
