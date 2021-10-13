﻿using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using ServiceStack.Text;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class ProgramSettings
	{
		private readonly Cumulus cumulus;

		public ProgramSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it
			var startup = new JsonProgramSettingsStartupOptions()
			{
				startuphostping = cumulus.ProgramOptions.StartupPingHost,
				startuppingescape = cumulus.ProgramOptions.StartupPingEscapeTime,
				startupdelay = cumulus.ProgramOptions.StartupDelaySecs,
				startupdelaymaxuptime = cumulus.ProgramOptions.StartupDelayMaxUptime
			};

			var logging = new JsonProgramSettingsLoggingOptions()
			{
				debuglogging = cumulus.ProgramOptions.DebugLogging,
				datalogging = cumulus.ProgramOptions.DataLogging,
				ftplogging = cumulus.FtpOptions.Logging,
				emaillogging = cumulus.SmtpOptions.Logging,
				spikelogging = cumulus.ErrorLogSpikeRemoval
			};

			var options = new JsonProgramSettingsGeneralOptions()
			{
				stopsecondinstance = cumulus.ProgramOptions.WarnMultiple,
				listwebtags = cumulus.ProgramOptions.ListWebTags
			};

			var culture = new JsonProgramSettingsCultureOptions()
			{
				dateseparator = "\"" + cumulus.ProgramOptions.Culture.DateSeparator + "\""
			};

			var settings = new JsonProgramSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				startup = startup,
				logging = logging,
				options = options,
				culture = culture
			};

			//return JsonConvert.SerializeObject(data);
			return JsonSerializer.SerializeToString(settings);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			JsonProgramSettings settings;
			context.Response.StatusCode = 200;

			// get the response
			try
			{
				cumulus.LogMessage("Updating Program settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = JsonSerializer.DeserializeFromString<JsonProgramSettings>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Program Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Program Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.ProgramOptions.EnableAccessibility = settings.accessible;
				cumulus.ProgramOptions.StartupPingHost = settings.startup.startuphostping;
				cumulus.ProgramOptions.StartupPingEscapeTime = settings.startup.startuppingescape;
				cumulus.ProgramOptions.StartupDelaySecs = settings.startup.startupdelay;
				cumulus.ProgramOptions.StartupDelayMaxUptime = settings.startup.startupdelaymaxuptime;
				cumulus.ProgramOptions.DebugLogging = settings.logging.debuglogging;
				cumulus.ProgramOptions.DataLogging = settings.logging.datalogging;
				cumulus.SmtpOptions.Logging = settings.logging.emaillogging;
				cumulus.ErrorLogSpikeRemoval = settings.logging.spikelogging;
				cumulus.ProgramOptions.WarnMultiple = settings.options.stopsecondinstance;
				cumulus.ProgramOptions.ListWebTags = settings.options.listwebtags;

				var dateSep = settings.culture.dateseparator.Replace("\"", "");
				if (dateSep != cumulus.ProgramOptions.Culture.DateSeparator)
				{
					// save the new setting to our config
					cumulus.ProgramOptions.Culture.DateSeparator = dateSep;
					// get the existing culture
					var newCulture = CultureInfo.CurrentCulture;
					// change the date separator
					newCulture.DateTimeFormat.DateSeparator = dateSep;
					// set current thread culture
					Thread.CurrentThread.CurrentCulture = newCulture;
					// set the default culture for other threads
					CultureInfo.DefaultThreadCurrentCulture = newCulture;
				}

				//cumulus.ProgramOptions.Culture.DateSeparator = settings.culture.dateseparator.Replace("\"", "");

				if (settings.logging.ftplogging != cumulus.FtpOptions.Logging)
				{
					cumulus.FtpOptions.Logging = settings.logging.ftplogging;
					cumulus.SetFtpLogging(cumulus.FtpOptions.Logging);
				}
			}
			catch (Exception ex)
			{
				var msg = "Error processing Program Options: " + ex.Message;
				cumulus.LogMessage(msg);
				errorMsg += msg + "\n\n";
				context.Response.StatusCode = 500;
			}

			// Save the settings
			cumulus.WriteIniFile();

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}
	}

	public class JsonProgramSettings
	{
		public bool accessible { get; set; }
		public JsonProgramSettingsStartupOptions startup { get; set; }
		public JsonProgramSettingsLoggingOptions logging { get; set; }
		public JsonProgramSettingsGeneralOptions options { get; set; }
		public JsonProgramSettingsCultureOptions culture { get; set; }
	}

	public class JsonProgramSettingsStartupOptions
	{
		public string startuphostping { get; set; }
		public int startuppingescape { get; set; }
		public int startupdelay { get; set; }
		public int startupdelaymaxuptime { get; set; }
	}

	public class JsonProgramSettingsLoggingOptions
	{
		public bool debuglogging { get; set; }
		public bool datalogging { get; set; }
		public bool ftplogging { get; set; }
		public bool emaillogging { get; set; }
		public bool spikelogging { get; set; }
	}
	public class JsonProgramSettingsGeneralOptions
	{
		public bool stopsecondinstance { get; set; }
		public bool listwebtags { get; set; }
	}
	public class JsonProgramSettingsCultureOptions
	{
		public string dateseparator { get; set; }
	}
}
