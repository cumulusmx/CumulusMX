using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using ServiceStack;

namespace CumulusMX
{
	public class CustomLogs
	{
		private readonly Cumulus cumulus;

		public CustomLogs(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlpacaFormDataIntvl()
		{
			var interval = new List<CustomLogsIntervalSettings>();

			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomIntvlLogSettings[i].FileName) || !string.IsNullOrEmpty(cumulus.CustomIntvlLogSettings[i].ContentString))
				{
					interval.Add(new CustomLogsIntervalSettings()
					{
						enabled = cumulus.CustomIntvlLogSettings[i].Enabled,
						filename = cumulus.CustomIntvlLogSettings[i].FileName,
						content = cumulus.CustomIntvlLogSettings[i].ContentString,
						intervalidx = cumulus.CustomIntvlLogSettings[i].IntervalIdx
					});
				}
			}



			var settings = new CustomLogsSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				interval = interval
			};

			return settings.ToJson();
		}

		public string GetAlpacaFormDataDaily()
		{
			var daily = new List<CustomLogsDailySettings>();

			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomDailyLogSettings[i].FileName) || !string.IsNullOrEmpty(cumulus.CustomDailyLogSettings[i].ContentString))
				{
					daily.Add(new CustomLogsDailySettings()
					{
						enabled = cumulus.CustomDailyLogSettings[i].Enabled,
						filename = cumulus.CustomDailyLogSettings[i].FileName,
						content = cumulus.CustomDailyLogSettings[i].ContentString
					});
				}
			}

			var settings = new CustomLogsSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				daily = daily
			};

			return settings.ToJson();
		}

		public string UpdateConfigIntvl(IHttpContext context)
		{
			string json = "";
			CustomLogsSettings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<CustomLogsSettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Custom Interval Log Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Custom Interval Log Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating custom interval log settings");

				for (var i = 0; i < 10; i++)
				{
					if (i < settings.interval.Count)
					{	
						cumulus.CustomIntvlLogSettings[i].FileName = settings.interval[i].filename ?? null;
						cumulus.CustomIntvlLogSettings[i].ContentString = settings.interval[i].content ?? null;
						cumulus.CustomIntvlLogSettings[i].IntervalIdx = settings.interval[i].intervalidx;
						cumulus.CustomIntvlLogSettings[i].Interval = cumulus.FactorsOf60[settings.interval[i].intervalidx];

						if (string.IsNullOrEmpty(cumulus.CustomIntvlLogSettings[i].FileName) || string.IsNullOrEmpty(cumulus.CustomIntvlLogSettings[i].ContentString))
							cumulus.CustomIntvlLogSettings[i].Enabled = false;
						else
							cumulus.CustomIntvlLogSettings[i].Enabled = settings.interval[i].enabled;
					}
					else
					{
						cumulus.CustomIntvlLogSettings[i].Enabled = false;
						cumulus.CustomIntvlLogSettings[i].FileName = null;
						cumulus.CustomIntvlLogSettings[i].ContentString = null;
						cumulus.CustomIntvlLogSettings[i].IntervalIdx = cumulus.DataLogInterval;
						cumulus.CustomIntvlLogSettings[i].Interval = cumulus.FactorsOf60[cumulus.DataLogInterval];
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

		public string UpdateConfigDaily(IHttpContext context)
		{
			string json = "";
			CustomLogsSettings settings;
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<CustomLogsSettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Custom Daily Log Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Custom Daily Log Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating custom daily log settings");

				for (var i = 0; i < 10; i++)
				{
					if (i < settings.daily.Count)
					{
						cumulus.CustomDailyLogSettings[i].Enabled = settings.daily[i].enabled;
						cumulus.CustomDailyLogSettings[i].FileName = settings.daily[i].filename ?? null;
						cumulus.CustomDailyLogSettings[i].ContentString = settings.daily[i].content ?? null;

						if (string.IsNullOrEmpty(cumulus.CustomDailyLogSettings[i].FileName) || string.IsNullOrEmpty(cumulus.CustomDailyLogSettings[i].ContentString))
							cumulus.CustomDailyLogSettings[i].Enabled = false;
						else
							cumulus.CustomDailyLogSettings[i].Enabled = settings.daily[i].enabled;
					}
					else
					{
						cumulus.CustomDailyLogSettings[i].Enabled = false;
						cumulus.CustomDailyLogSettings[i].FileName = null;
						cumulus.CustomDailyLogSettings[i].ContentString = null;
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

	}

	public class CustomLogsSettings
	{
		public bool accessible { get; set; }
		public List<CustomLogsDailySettings> daily { get; set; }
		public List<CustomLogsIntervalSettings> interval { get; set; }
	}


	public class CustomLogsDailySettings
	{
		public bool enabled { get; set; }
		public string filename { get; set; }
		public string content { get; set; }
	}



	public class CustomLogsIntervalSettings
	{
		public bool enabled { get; set; }
		public string filename { get; set; }
		public string content { get; set; }
		public int intervalidx { get; set; }
	}
}
