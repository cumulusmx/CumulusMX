using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class InternetSettings
	{
		private readonly Cumulus cumulus;
		private readonly string internetOptionsFile;
		private readonly string internetSchemaFile;

		public InternetSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			internetOptionsFile = AppDomain.CurrentDomain.BaseDirectory + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "InternetOptions.json";
			internetSchemaFile = AppDomain.CurrentDomain.BaseDirectory + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "InternetSchema.json";
		}

		//public string UpdateInternetConfig(HttpListenerContext context)
		public string UpdateInternetConfig(IHttpContext context)
		{
			var ErrorMsg = "";
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = JsonConvert.DeserializeObject<JsonInternetSettingsData>(json);
				// process the settings
				cumulus.LogMessage("Updating internet settings");

				// website settings
				try
				{
					cumulus.ftp_directory = settings.website.directory ?? string.Empty;
					cumulus.ForumURL = settings.website.forumurl ?? string.Empty;
					cumulus.ftp_port = settings.website.ftpport;
					cumulus.ftp_host = settings.website.hostname ?? string.Empty;
					cumulus.Sslftp = (Cumulus.FtpProtocols)settings.website.sslftp;
					cumulus.ftp_password = settings.website.password ?? string.Empty;
					cumulus.ftp_user = settings.website.username ?? string.Empty;
					cumulus.SshftpAuthentication = settings.website.sshAuth ?? string.Empty;
					cumulus.SshftpPskFile = settings.website.pskFile ?? string.Empty;
					cumulus.WebcamURL = settings.website.webcamurl ?? string.Empty;
				}
				catch (Exception ex)
				{
					var msg = "Error processing website settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// web settings
				try
				{
					cumulus.ActiveFTPMode = settings.websettings.activeftp;
					cumulus.WebAutoUpdate = settings.websettings.autoupdate;
					cumulus.RealtimeEnabled = settings.websettings.enablerealtime;
					cumulus.RealtimeFTPEnabled = settings.websettings.enablerealtimeftp;
					cumulus.RealtimeTxtFTP = settings.websettings.realtimetxtftp;
					cumulus.RealtimeGaugesTxtFTP = settings.websettings.realtimegaugestxtftp;
					cumulus.RealtimeInterval = settings.websettings.realtimeinterval * 1000;
					cumulus.DeleteBeforeUpload = settings.websettings.ftpdelete;
					cumulus.UpdateInterval = settings.websettings.ftpinterval;
					cumulus.FTPRename = settings.websettings.ftprename;
					cumulus.IncludeStandardFiles = settings.websettings.includestdfiles;
					cumulus.IncludeGraphDataFiles = settings.websettings.includegraphdatafiles;
					cumulus.IncludeMoonImage = settings.websettings.includemoonimage;
					cumulus.UTF8encode = settings.websettings.utf8encode;
					if (settings.websettings.ftplogging != cumulus.FTPlogging)
					{
						cumulus.FTPlogging = settings.websettings.ftplogging;
						cumulus.SetFtpLogging(cumulus.FTPlogging);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing web settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// external programs
				try
				{
					cumulus.DailyProgram = settings.externalprograms.dailyprogram ?? string.Empty;
					cumulus.DailyParams = settings.externalprograms.dailyprogramparams ?? string.Empty;
					cumulus.ExternalProgram = settings.externalprograms.program ?? string.Empty;
					cumulus.ExternalParams = settings.externalprograms.programparams ?? string.Empty;
					cumulus.RealtimeProgram = settings.externalprograms.realtimeprogram ?? string.Empty;
					cumulus.RealtimeParams = settings.externalprograms.realtimeprogramparams ?? string.Empty;
				}
				catch (Exception ex)
				{
					var msg = "Error processing external programs: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// twitter
				try
				{
					cumulus.TwitterEnabled = settings.twitter.enabled;
					cumulus.TwitterInterval = settings.twitter.interval;
					cumulus.TwitterPW = settings.twitter.password ?? string.Empty;
					cumulus.TwitterSendLocation = settings.twitter.sendlocation;
					cumulus.Twitteruser = settings.twitter.user ?? string.Empty;
					cumulus.SynchronisedTwitterUpdate = (60 % cumulus.TwitterInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing twitter settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// wunderground
				try
				{
					cumulus.WundCatchUp = settings.wunderground.catchup;
					cumulus.WundEnabled = settings.wunderground.enabled;
					cumulus.SendIndoorToWund = settings.wunderground.includeindoor;
					cumulus.SendSRToWund = settings.wunderground.includesolar;
					cumulus.SendUVToWund = settings.wunderground.includeuv;
					cumulus.WundInterval = settings.wunderground.interval;
					cumulus.WundPW = settings.wunderground.password ?? string.Empty;
					cumulus.WundRapidFireEnabled = settings.wunderground.rapidfire;
					cumulus.WundSendAverage = settings.wunderground.sendavgwind;
					cumulus.WundID = settings.wunderground.stationid ?? string.Empty;
					cumulus.SynchronisedWUUpdate = (!cumulus.WundRapidFireEnabled) && (60 % cumulus.WundInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing wunderground settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Windy
				try
				{
					cumulus.WindyCatchUp = settings.windy.catchup;
					cumulus.WindyEnabled = settings.windy.enabled;
					//cumulus.WindySendSolar = settings.windy.includesolar;
					cumulus.WindySendUV = settings.windy.includeuv;
					cumulus.WindyInterval = settings.windy.interval;
					cumulus.WindyApiKey = settings.windy.apikey;
					cumulus.WindyStationIdx = settings.windy.stationidx;
					cumulus.SynchronisedWindyUpdate = (60 % cumulus.WindyInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing Windy settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Awekas
				try
				{
					cumulus.AwekasEnabled = settings.awekas.enabled;
					cumulus.AwekasInterval = settings.awekas.interval;
					cumulus.AwekasLang = settings.awekas.lang;
					cumulus.AwekasPW = settings.awekas.password ?? string.Empty;
					cumulus.AwekasUser = settings.awekas.user ?? string.Empty;
					cumulus.SendSolarToAwekas = settings.awekas.includesolar;
					cumulus.SendUVToAwekas = settings.awekas.includeuv;
					cumulus.SendSoilTempToAwekas = settings.awekas.includesoiltemp;
					cumulus.SynchronisedAwekasUpdate = (60 % cumulus.AwekasInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing AWEKAS settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WeatherCloud
				try
				{
					cumulus.WCloudWid = settings.weathercloud.wid ?? string.Empty;
					cumulus.WCloudKey = settings.weathercloud.key ?? string.Empty;
					cumulus.WCloudEnabled = settings.weathercloud.enabled;
					cumulus.SendSolarToWCloud = settings.weathercloud.includesolar;
					cumulus.SendUVToWCloud = settings.weathercloud.includeuv;
					cumulus.SynchronisedWCloudUpdate = (60 % cumulus.WCloudInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing WeatherCloud settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// PWS weather
				try
				{
					cumulus.PWSCatchUp = settings.pwsweather.catchup;
					cumulus.PWSEnabled = settings.pwsweather.enabled;
					cumulus.PWSInterval = settings.pwsweather.interval;
					cumulus.SendSRToPWS = settings.pwsweather.includesolar;
					cumulus.SendUVToPWS = settings.pwsweather.includeuv;
					cumulus.PWSPW = settings.pwsweather.password ?? string.Empty;
					cumulus.PWSID = settings.pwsweather.stationid ?? string.Empty;
					cumulus.SynchronisedPWSUpdate = (60 % cumulus.PWSInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing PWS weather settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WOW
				try
				{
					cumulus.WOWCatchUp = settings.wow.catchup;
					cumulus.WOWEnabled = settings.wow.enabled;
					cumulus.SendSRToWOW = settings.wow.includesolar;
					cumulus.SendUVToWOW = settings.wow.includeuv;
					cumulus.WOWInterval = settings.wow.interval;
					cumulus.WOWPW = settings.wow.password ?? string.Empty; ;
					cumulus.WOWID = settings.wow.stationid ?? string.Empty; ;
					cumulus.SynchronisedWOWUpdate = (60 % cumulus.WOWInterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing WOW settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// CWOP
				try
				{
					cumulus.APRSenabled = settings.cwop.enabled;
					cumulus.APRSID = settings.cwop.id ?? string.Empty; ;
					cumulus.APRSinterval = settings.cwop.interval;
					cumulus.SendSRToAPRS = settings.cwop.includesolar;
					cumulus.APRSpass = settings.cwop.password ?? string.Empty; ;
					cumulus.APRSport = settings.cwop.port;
					cumulus.APRSserver = settings.cwop.server ?? string.Empty; ;
					cumulus.SynchronisedAPRSUpdate = (60 % cumulus.APRSinterval == 0);
				}
				catch (Exception ex)
				{
					var msg = "Error processing CWOP settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// MQTT
				try
				{
					cumulus.MQTTServer = settings.mqtt.server ?? string.Empty;
					cumulus.MQTTPort = settings.mqtt.port;
					cumulus.MQTTUseTLS = settings.mqtt.useTls;
					cumulus.MQTTUsername = settings.mqtt.username ?? string.Empty;
					cumulus.MQTTPassword = settings.mqtt.password ?? string.Empty;
					cumulus.MQTTEnableDataUpdate = settings.mqtt.dataUpdate.enabled;
					cumulus.MQTTUpdateTopic = settings.mqtt.dataUpdate.topic ?? string.Empty;
					cumulus.MQTTUpdateTemplate = settings.mqtt.dataUpdate.template ?? string.Empty;
					cumulus.MQTTEnableInterval = settings.mqtt.interval.enabled;
					cumulus.MQTTIntervalTime = settings.mqtt.interval.time;
					cumulus.MQTTIntervalTopic = settings.mqtt.interval.topic ?? string.Empty;
					cumulus.MQTTIntervalTemplate = settings.mqtt.interval.template ?? string.Empty;
				}
				catch (Exception ex)
				{
					var msg = "Error processing MQTT settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Moon Image
				try
				{
					cumulus.MoonImageEnabled = settings.moonimage.enabled;
					cumulus.MoonImageSize = settings.moonimage.size;
					cumulus.MoonImageFtpDest = settings.moonimage.ftpdest;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Moon image settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// HTTP proxy
				try
				{
					cumulus.HTTPProxyPassword = settings.proxies.httpproxy.password ?? string.Empty;
					cumulus.HTTPProxyPort = settings.proxies.httpproxy.port;
					cumulus.HTTPProxyName = settings.proxies.httpproxy.proxy ?? string.Empty;
					cumulus.HTTPProxyUser = settings.proxies.httpproxy.user ?? string.Empty;
				}
				catch (Exception ex)
				{
					var msg = "Error processing HTTP proxy settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Custom HTTP
				try
				{
					// custom seconds
					cumulus.CustomHttpSecondsString = settings.customhttp.customseconds.url ?? string.Empty;
					cumulus.CustomHttpSecondsEnabled = settings.customhttp.customseconds.enabled;
					cumulus.CustomHttpSecondsInterval = settings.customhttp.customseconds.interval;
					cumulus.CustomHttpSecondsTimer.Interval = cumulus.CustomHttpSecondsInterval * 1000;
					cumulus.CustomHttpSecondsTimer.Enabled = cumulus.CustomHttpSecondsEnabled;
					// custom minutes
					cumulus.CustomHttpMinutesString = settings.customhttp.customminutes.url ?? string.Empty;
					cumulus.CustomHttpMinutesEnabled = settings.customhttp.customminutes.enabled;
					cumulus.CustomHttpMinutesIntervalIndex = settings.customhttp.customminutes.intervalindex;
					if (cumulus.CustomHttpMinutesIntervalIndex >= 0 && cumulus.CustomHttpMinutesIntervalIndex < cumulus.FactorsOf60.Length)
					{
						cumulus.CustomHttpMinutesInterval = cumulus.FactorsOf60[cumulus.CustomHttpMinutesIntervalIndex];
					}
					else
					{
						cumulus.CustomHttpMinutesInterval = 10;
					}
					// custom rollover
					cumulus.CustomHttpRolloverString = settings.customhttp.customrollover.url ?? string.Empty;
					cumulus.CustomHttpRolloverEnabled = settings.customhttp.customrollover.enabled;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Custom settings: " + ex.Message;
					cumulus.LogMessage(msg);
					ErrorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();

				cumulus.SetUpHttpProxy();
				//cumulus.SetFtpLogging(cumulus.FTPlogging);

				// Setup MQTT
				if (cumulus.MQTTEnableDataUpdate || cumulus.MQTTEnableInterval)
				{
					if (!MqttPublisher.configured)
					{
						MqttPublisher.Setup(cumulus);
					}
					if (cumulus.MQTTEnableInterval)
					{
						cumulus.MQTTTimer.Elapsed -= cumulus.MQTTTimerTick;
						cumulus.MQTTTimer.Elapsed += cumulus.MQTTTimerTick;
						cumulus.MQTTTimer.Start();
					}
					else
					{
						cumulus.MQTTTimer.Stop();
					}
				}
				else
				{
					cumulus.MQTTTimer.Stop();
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			if (context.Response.StatusCode == 200)
				return "success";
			else
				return ErrorMsg;
		}

		public string GetInternetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it
			var websitesettings = new JsonInternetSettingsWebsite()
			{
				directory = cumulus.ftp_directory,
				forumurl = cumulus.ForumURL,
				ftpport = cumulus.ftp_port,
				sslftp = (int)cumulus.Sslftp,
				hostname = cumulus.ftp_host,
				password = cumulus.ftp_password,
				username = cumulus.ftp_user,
				sshAuth = cumulus.SshftpAuthentication,
				pskFile = cumulus.SshftpPskFile,
				webcamurl = cumulus.WebcamURL
			};

			var websettings = new JsonInternetSettingsWebSettings()
			{
				activeftp = cumulus.ActiveFTPMode,
				autoupdate = cumulus.WebAutoUpdate,
				enablerealtime = cumulus.RealtimeEnabled,
				enablerealtimeftp = cumulus.RealtimeFTPEnabled,
				realtimetxtftp = cumulus.RealtimeTxtFTP,
				realtimegaugestxtftp = cumulus.RealtimeGaugesTxtFTP,
				realtimeinterval = cumulus.RealtimeInterval / 1000,
				ftpdelete = cumulus.DeleteBeforeUpload,
				ftpinterval = cumulus.UpdateInterval,
				ftprename = cumulus.FTPRename,
				includestdfiles = cumulus.IncludeStandardFiles,
				includegraphdatafiles = cumulus.IncludeGraphDataFiles,
				includemoonimage = cumulus.IncludeMoonImage,
				utf8encode = cumulus.UTF8encode,
				ftplogging = cumulus.FTPlogging
			};

			var externalprograms = new JsonInternetSettingsExternalPrograms()
			{
				dailyprogram = cumulus.DailyProgram,
				dailyprogramparams = cumulus.DailyParams,
				program = cumulus.ExternalProgram,
				programparams = cumulus.ExternalParams,
				realtimeprogram = cumulus.RealtimeProgram,
				realtimeprogramparams = cumulus.RealtimeParams
			};

			var twittersettings = new JsonInternetSettingsTwitterSettings()
			{
				enabled = cumulus.TwitterEnabled,
				interval = cumulus.TwitterInterval,
				password = cumulus.TwitterPW,
				sendlocation = cumulus.TwitterSendLocation,
				user = cumulus.Twitteruser
			};

			var wusettings = new JsonInternetSettingsWunderground()
			{
				catchup = cumulus.WundCatchUp,
				enabled = cumulus.WundEnabled,
				includeindoor = cumulus.SendIndoorToWund,
				includesolar = cumulus.SendSRToWund,
				includeuv = cumulus.SendUVToWund,
				interval = cumulus.WundInterval,
				password = cumulus.WundPW,
				rapidfire = cumulus.WundRapidFireEnabled,
				sendavgwind = cumulus.WundSendAverage,
				stationid = cumulus.WundID
			};

			var windysettings = new JsonInternetSettingsWindy()
			{
				catchup = cumulus.WindyCatchUp,
				enabled = cumulus.WindyEnabled,
				includeuv = cumulus.WindySendUV,
				interval = cumulus.WindyInterval,
				apikey = cumulus.WindyApiKey,
				stationidx = cumulus.WindyStationIdx
			};

			var awekassettings = new JsonInternetSettingsAwekas()
			{
				enabled = cumulus.AwekasEnabled,
				includesolar = cumulus.SendSolarToAwekas,
				includesoiltemp = cumulus.SendSoilTempToAwekas,
				includeuv = cumulus.SendUVToAwekas,
				interval = cumulus.AwekasInterval,
				lang = cumulus.AwekasLang,
				password = cumulus.AwekasPW,
				user = cumulus.AwekasUser
			};

			var wcloudsettings = new JsonInternetSettingsWCloud()
			{
				enabled = cumulus.WCloudEnabled,
				includesolar = cumulus.SendSolarToWCloud,
				includeuv = cumulus.SendUVToWCloud,
				key = cumulus.WCloudKey,
				wid = cumulus.WCloudWid
			};

			var pwssettings = new JsonInternetSettingsPWSweather()
			{
				catchup = cumulus.PWSCatchUp,
				enabled = cumulus.PWSEnabled,
				interval = cumulus.PWSInterval,
				includesolar = cumulus.SendSRToPWS,
				includeuv = cumulus.SendUVToPWS,
				password = cumulus.PWSPW,
				stationid = cumulus.PWSID
			};

			var wowsettings = new JsonInternetSettingsWOW()
			{
				catchup = cumulus.WOWCatchUp,
				enabled = cumulus.WOWEnabled,
				includesolar = cumulus.SendSRToWOW,
				includeuv = cumulus.SendUVToWOW,
				interval = cumulus.WOWInterval,
				password = cumulus.WOWPW,
				stationid = cumulus.WOWID
			};

			var cwopsettings = new JsonInternetSettingsCwop()
			{
				enabled = cumulus.APRSenabled,
				id = cumulus.APRSID,
				interval = cumulus.APRSinterval,
				includesolar = cumulus.SendSRToAPRS,
				password = cumulus.APRSpass,
				port = cumulus.APRSport,
				server = cumulus.APRSserver
			};


			var mqttUpdate = new JsonInternetSettingsMqttDataupdate()
			{
				enabled = cumulus.MQTTEnableDataUpdate,
				topic = cumulus.MQTTUpdateTopic,
				template = cumulus.MQTTUpdateTemplate
			};

			var mqttInterval = new JsonInternetSettingsMqttInterval()
			{
				enabled = cumulus.MQTTEnableInterval,
				time = cumulus.MQTTIntervalTime,
				topic = cumulus.MQTTIntervalTopic,
				template = cumulus.MQTTIntervalTemplate
			};

			var mqttsettings = new JsonInternetSettingsMqtt()
			{
				server = cumulus.MQTTServer,
				port = cumulus.MQTTPort,
				useTls = cumulus.MQTTUseTLS,
				username = cumulus.MQTTUsername,
				password = cumulus.MQTTPassword,
				dataUpdate = mqttUpdate,
				interval = mqttInterval
			};

			var moonimagesettings = new JsonInternetSettingsMoonImage()
			{
				enabled = cumulus.MoonImageEnabled,
				size = cumulus.MoonImageSize,
				ftpdest = cumulus.MoonImageFtpDest
			};

			var httpproxy = new JsonInternetSettingsHTTPproxySettings()
			{
				password = cumulus.HTTPProxyPassword,
				port = cumulus.HTTPProxyPort,
				proxy = cumulus.HTTPProxyName,
				user = cumulus.HTTPProxyUser
			};

			var proxy = new JsonInternetSettingsProxySettings() { httpproxy = httpproxy };

			var customseconds = new JsonInternetSettingsCustomHttpSeconds()
			{
				enabled = cumulus.CustomHttpSecondsEnabled,
				interval = cumulus.CustomHttpSecondsInterval,
				url = cumulus.CustomHttpSecondsString
			};

			var customminutes = new JsonInternetSettingsCustomHttpMinutes()
			{
				enabled = cumulus.CustomHttpMinutesEnabled,
				intervalindex = cumulus.CustomHttpMinutesIntervalIndex,
				url = cumulus.CustomHttpMinutesString
			};

			var customrollover = new JsonInternetSettingsCustomHttpRollover()
			{
				enabled = cumulus.CustomHttpRolloverEnabled,
				url = cumulus.CustomHttpRolloverString
			};

			var customhttp = new JsonInternetSettingsCustomHttpSettings() { customseconds = customseconds, customminutes = customminutes, customrollover = customrollover };

			var data = new JsonInternetSettingsData()
			{
				website = websitesettings,
				websettings = websettings,
				externalprograms = externalprograms,
				twitter = twittersettings,
				wunderground = wusettings,
				windy = windysettings,
				awekas = awekassettings,
				weathercloud = wcloudsettings,
				pwsweather = pwssettings,
				wow = wowsettings,
				cwop = cwopsettings,
				mqtt = mqttsettings,
				moonimage = moonimagesettings,
				proxies = proxy,
				customhttp = customhttp
			};

			return JsonConvert.SerializeObject(data);
		}

		public string GetInternetAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(internetOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetInternetAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(internetSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetExtraWebFilesData()
		{
			var json = new StringBuilder(10240);
			json.Append("{\"metadata\":[{\"name\":\"local\",\"label\":\"LOCAL FILENAME\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"remote\",\"label\":\"REMOTE FILENAME\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"process\",\"label\":\"PROCESS\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"realtime\",\"label\":\"REALTIME\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"ftp\",\"label\":\"FTP\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"utf8\",\"label\":\"UTF8\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"binary\",\"label\":\"BINARY\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"endofday\",\"label\":\"END OF DAY\",\"datatype\":\"boolean\",\"editable\":true}],\"data\":[");

			int numfiles = Cumulus.numextrafiles;

			for (int i = 0; i < numfiles; i++)
			{
				var local = cumulus.ExtraFiles[i].local.Replace("\\", "\\\\").Replace("/", "\\/");
				var remote = cumulus.ExtraFiles[i].remote.Replace("\\", "\\\\").Replace("/", "\\/");

				string process = cumulus.ExtraFiles[i].process ? "true" : "false";
				string realtime = cumulus.ExtraFiles[i].realtime ? "true" : "false";
				string ftp = cumulus.ExtraFiles[i].FTP ? "true" : "false";
				string utf8 = cumulus.ExtraFiles[i].UTF8 ? "true" : "false";
				string binary = cumulus.ExtraFiles[i].binary ? "true" : "false";
				string endofday = cumulus.ExtraFiles[i].endofday ? "true" : "false";
				json.Append("{");
				json.Append($"\"id\":{(i + 1)},\"values\":[\"{local}\",\"{remote}\",\"{process}\",\"{realtime}\",\"{ftp}\",\"{utf8}\",\"{binary}\",\"{endofday}\"]");
				json.Append("}");

				if (i < numfiles - 1)
				{
					json.Append(",");
				}
			}

			json.Append("]}");
			return json.ToString();
		}

		//public string UpdateExtraWebFiles(HttpListenerContext context)
		public string UpdateExtraWebFiles(IHttpContext context)
		{
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				var pars = WebUtility.UrlDecode(data);

				NameValueCollection qscoll = HttpUtility.ParseQueryString(pars);

				var entry = Convert.ToInt32(qscoll["id"]) - 1;
				int col = Convert.ToInt32(qscoll["column"]);
				var value = qscoll["value"];

				switch (col)
				{
					case 0:
						// local filename
						cumulus.ExtraFiles[entry].local = value;
						break;
					case 1:
						// remote filename
						cumulus.ExtraFiles[entry].remote = value;
						break;
					case 2:
						// process
						cumulus.ExtraFiles[entry].process = value == "true";
						break;
					case 3:
						// realtime
						cumulus.ExtraFiles[entry].realtime = value == "true";
						break;
					case 4:
						// ftp
						cumulus.ExtraFiles[entry].FTP = value == "true";
						break;
					case 5:
						// utf8
						cumulus.ExtraFiles[entry].UTF8 = value == "true";
						break;
					case 6:
						// binary
						cumulus.ExtraFiles[entry].binary = value == "true";
						break;
					case 7:
						// end of day
						cumulus.ExtraFiles[entry].endofday = value == "true";
						break;
				}
				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return "success";
		}
	}

	public class JsonInternetSettingsData
	{
		public JsonInternetSettingsWebsite website { get; set; }
		public JsonInternetSettingsWebSettings websettings { get; set; }
		public JsonInternetSettingsExternalPrograms externalprograms { get; set; }
		public JsonInternetSettingsTwitterSettings twitter { get; set; }
		public JsonInternetSettingsWunderground wunderground { get; set; }
		public JsonInternetSettingsWindy windy { get; set; }
		public JsonInternetSettingsPWSweather pwsweather { get; set; }
		public JsonInternetSettingsWOW wow { get; set; }
		public JsonInternetSettingsCwop cwop { get; set; }
		public JsonInternetSettingsAwekas awekas { get; set; }
		public JsonInternetSettingsWCloud weathercloud { get; set; }
		public JsonInternetSettingsMqtt mqtt { get; set; }
		public JsonInternetSettingsMoonImage moonimage { get; set; }
		public JsonInternetSettingsProxySettings proxies { get; set; }
		public JsonInternetSettingsCustomHttpSettings customhttp { get; set; }
	}

	public class JsonInternetSettingsWebsite
	{
		public string hostname { get; set; }
		public int ftpport { get; set; }
		public int sslftp { get; set; }
		public string directory { get; set; }
		public string username { get; set; }
		public string password { get; set; }
		public string sshAuth { get; set; }
		public string pskFile { get; set; }
		public string forumurl { get; set; }
		public string webcamurl { get; set; }
	}

	public class JsonInternetSettingsWebSettings
	{
		public bool autoupdate { get; set; }
		public bool includestdfiles { get; set; }
		public bool includegraphdatafiles { get; set; }
		public bool includemoonimage { get; set; }
		public bool activeftp { get; set; }
		public bool ftprename { get; set; }
		public bool ftpdelete { get; set; }
		public bool utf8encode { get; set; }
		public bool ftplogging { get; set; }
		public int ftpinterval { get; set; }
		public bool enablerealtime { get; set; }
		public bool enablerealtimeftp { get; set; }
		public bool realtimetxtftp { get; set; }
		public bool realtimegaugestxtftp { get; set; }
		public int realtimeinterval { get; set; }
	}

	public class JsonInternetSettingsExternalPrograms
	{
		public string program { get; set; }
		public string programparams { get; set; }
		public string realtimeprogram { get; set; }
		public string realtimeprogramparams { get; set; }
		public string dailyprogram { get; set; }
		public string dailyprogramparams { get; set; }
	}

	public class JsonInternetSettingsTwitterSettings
	{
		public bool enabled { get; set; }
		public bool sendlocation { get; set; }
		public int interval { get; set; }
		public string user { get; set; }
		public string password { get; set; }
	}

	public class JsonInternetSettingsWunderground
	{
		public bool enabled { get; set; }
		public bool includeindoor { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool rapidfire { get; set; }
		public bool sendavgwind { get; set; }
		public bool catchup { get; set; }
		public string stationid { get; set; }
		public string password { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsWindy
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		//public bool includesolar { get; set; }
		public bool catchup { get; set; }
		public int interval { get; set; }
		public string apikey { get; set; }
		public int stationidx { get; set; }
	}

	public class JsonInternetSettingsAwekas
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool includesoiltemp { get; set; }
		public string user { get; set; }
		public string password { get; set; }
		public string lang { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsWCloud
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public string wid { get; set; }
		public string key { get; set; }
	}

	public class JsonInternetSettingsPWSweather
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool catchup { get; set; }
		public string stationid { get; set; }
		public string password { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsWOW
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool catchup { get; set; }
		public string stationid { get; set; }
		public string password { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsCwop
	{
		public bool enabled { get; set; }
		public bool includesolar { get; set; }
		public string id { get; set; }
		public string password { get; set; }
		public string server { get; set; }
		public int port { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsMqtt
	{
		public string server { get; set; }
		public int port { get; set; }
		public bool useTls { get; set; }
		public string username { get; set; }
		public string password { get; set; }
		public JsonInternetSettingsMqttDataupdate dataUpdate { get; set; }
		public JsonInternetSettingsMqttInterval interval { get; set; }
	}

	public class JsonInternetSettingsMqttDataupdate
	{
		public bool enabled { get; set; }
		public string topic { get; set; }
		public string template { get; set; }
	}

	public class JsonInternetSettingsMqttInterval
	{
		public bool enabled { get; set; }
		public int time { get; set; }
		public string topic { get; set; }
		public string template { get; set; }
	}

	public class JsonInternetSettingsMoonImage
	{
		public bool enabled { get; set; }
		public int size { get; set; }
		public string ftpdest { get; set; }
	}


	public class JsonInternetSettingsProxySettings
	{
		public JsonInternetSettingsHTTPproxySettings httpproxy { get; set; }
	}

	public class JsonInternetSettingsHTTPproxySettings
	{
		public string proxy { get; set; }
		public int port { get; set; }
		public string user { get; set; }
		public string password { get; set; }
	}

	public class JsonInternetSettingsCustomHttpSeconds
	{
		public string url { get; set; }
		public bool enabled { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsCustomHttpMinutes
	{
		public string url { get; set; }
		public bool enabled { get; set; }
		public int intervalindex { get; set; }
	}

	public class JsonInternetSettingsCustomHttpRollover
	{
		public string url { get; set; }
		public bool enabled { get; set; }
	}

	public class JsonInternetSettingsCustomHttpSettings
	{
		public JsonInternetSettingsCustomHttpSeconds customseconds { get; set; }
		public JsonInternetSettingsCustomHttpMinutes customminutes { get; set; }
		public JsonInternetSettingsCustomHttpRollover customrollover { get; set; }
	}
}
