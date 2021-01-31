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
		private readonly string programOptionsFile;
		private readonly string programSchemaFile;

		public ProgramSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;

			programOptionsFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "ProgramOptions.json";
			programSchemaFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "ProgramSchema.json";
		}

		public string GetProgramAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it
			var options = new JsonProgramSettingsOptions()
			{
				startuphostping = cumulus.ProgramOptions.StartupPingHost,
				startuppingescape = cumulus.ProgramOptions.StartupPingEscapeTime,
				startupdelay = cumulus.ProgramOptions.StartupDelaySecs,
				startupdelaymaxuptime = cumulus.ProgramOptions.StartupDelayMaxUptime,
				debuglogging = cumulus.ProgramOptions.DebugLogging,
				datalogging = cumulus.ProgramOptions.DataLogging,
				stopsecondinstance = cumulus.ProgramOptions.WarnMultiple
			};


			//return JsonConvert.SerializeObject(data);
			return JsonSerializer.SerializeToString(options);
		}

		public string GetProgramAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(programOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetProgramAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(programSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}


		public string UpdateProgramConfig(IHttpContext context)
		{
			var errorMsg = "";
			context.Response.StatusCode = 200;
			// get the response
			try
			{
				cumulus.LogMessage("Updating program settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = JsonSerializer.DeserializeFromString<JsonProgramSettingsOptions>(json);

				// process the settings
				try
				{
					cumulus.ProgramOptions.StartupPingHost = settings.startuphostping;
					cumulus.ProgramOptions.StartupPingEscapeTime = settings.startuppingescape;
					cumulus.ProgramOptions.StartupDelaySecs = settings.startupdelay;
					cumulus.ProgramOptions.StartupDelayMaxUptime = settings.startupdelaymaxuptime;
					cumulus.ProgramOptions.DebugLogging = settings.debuglogging;
					cumulus.ProgramOptions.DataLogging = settings.datalogging;
					cumulus.ProgramOptions.WarnMultiple = settings.stopsecondinstance;
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
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}
	}

	public class JsonProgramSettingsOptions
	{
		public string startuphostping { get; set; }
		public int startuppingescape { get; set; }
		public int startupdelay { get; set; }
		public int startupdelaymaxuptime { get; set; }
		public bool debuglogging { get; set; }
		public bool datalogging { get; set; }
		public bool stopsecondinstance { get; set; }
	}
}
