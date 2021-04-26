using System;
using System.IO;
using System.Net;
using ServiceStack.Text;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class ProgramSettings
	{
		private readonly Cumulus cumulus;
		private readonly string optionsFile;
		private readonly string schemaFile;

		public ProgramSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;

			optionsFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "ProgramOptions.json";
			schemaFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "ProgramSchema.json";
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

			var options = new JsonProgramSettingsGeneralOptions()
			{
				debuglogging = cumulus.ProgramOptions.DebugLogging,
				datalogging = cumulus.ProgramOptions.DataLogging,
				ftplogging = cumulus.FTPlogging,
				emaillogging = cumulus.SmtpOptions.Logging,
				stopsecondinstance = cumulus.ProgramOptions.WarnMultiple
			};

			var settings = new JsonProgramSettings()
			{
				startup = startup,
				options = options
			};

			//return JsonConvert.SerializeObject(data);
			return JsonSerializer.SerializeToString(settings);
		}

		public string GetAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(optionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(schemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
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
				var msg = "Error deserializing Program Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Program Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.ProgramOptions.StartupPingHost = settings.startup.startuphostping;
				cumulus.ProgramOptions.StartupPingEscapeTime = settings.startup.startuppingescape;
				cumulus.ProgramOptions.StartupDelaySecs = settings.startup.startupdelay;
				cumulus.ProgramOptions.StartupDelayMaxUptime = settings.startup.startupdelaymaxuptime;
				cumulus.ProgramOptions.DebugLogging = settings.options.debuglogging;
				cumulus.ProgramOptions.DataLogging = settings.options.datalogging;
				cumulus.SmtpOptions.Logging = settings.options.emaillogging;
				cumulus.ProgramOptions.WarnMultiple = settings.options.stopsecondinstance;

				if (settings.options.ftplogging != cumulus.FTPlogging)
				{
					cumulus.FTPlogging = settings.options.ftplogging;
					cumulus.SetFtpLogging(cumulus.FTPlogging);
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
		public JsonProgramSettingsStartupOptions startup { get; set; }
		public JsonProgramSettingsGeneralOptions options { get; set; }
	}

	public class JsonProgramSettingsStartupOptions
	{
		public string startuphostping { get; set; }
		public int startuppingescape { get; set; }
		public int startupdelay { get; set; }
		public int startupdelaymaxuptime { get; set; }
	}

	public class JsonProgramSettingsGeneralOptions
	{
		public bool debuglogging { get; set; }
		public bool datalogging { get; set; }
		public bool ftplogging { get; set; }
		public bool emaillogging { get; set; }
		public bool stopsecondinstance { get; set; }
	}
}
