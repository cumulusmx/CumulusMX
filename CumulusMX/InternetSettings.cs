using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using ServiceStack;
using EmbedIO;

namespace CumulusMX
{
	public class InternetSettings
	{
		private readonly Cumulus cumulus;

		public InternetSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			JsonInternetSettingsData settings;

			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonInternetSettingsData>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Internet Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Internet Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating internet settings");

				// website settings
				try
				{
					cumulus.FtpOptions.Enabled = settings.website.enabled;
					if (cumulus.FtpOptions.Enabled)
					{
						cumulus.FtpOptions.Directory = settings.website.directory ?? string.Empty;
						cumulus.FtpOptions.Port = settings.website.ftpport;
						cumulus.FtpOptions.Hostname = settings.website.hostname ?? string.Empty;
						cumulus.FtpOptions.FtpMode = (Cumulus.FtpProtocols)settings.website.sslftp;
						cumulus.FtpOptions.Password = settings.website.password ?? string.Empty;
						cumulus.FtpOptions.Username = settings.website.username ?? string.Empty;
						cumulus.FtpOptions.SshAuthen = settings.website.sshAuth ?? string.Empty;
						cumulus.FtpOptions.SshPskFile = settings.website.pskFile ?? string.Empty;

						cumulus.DeleteBeforeUpload = settings.website.general.ftpdelete;
						cumulus.FTPRename = settings.website.general.ftprename;
						cumulus.UTF8encode = settings.website.general.utf8encode;

						if (cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.FTP || cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.FTPS)
						{
							cumulus.FtpOptions.ActiveMode = settings.website.advanced.activeftp;
							cumulus.FtpOptions.DisableEPSV = settings.website.advanced.disableftpsepsv;
						}

						if (cumulus.FtpOptions.FtpMode == Cumulus.FtpProtocols.FTPS)
						{
							cumulus.FtpOptions.DisableExplicit = settings.website.advanced.disableftpsexplicit;
						}
					}

					cumulus.FtpOptions.LocalCopyEnabled = settings.website.localcopy;
					if (cumulus.FtpOptions.LocalCopyEnabled)
					{
						cumulus.FtpOptions.LocalCopyFolder = settings.website.localcopyfolder;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing website settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// web settings
				try
				{
					cumulus.RealtimeIntervalEnabled = settings.websettings.realtime.enabled;
					if (cumulus.RealtimeIntervalEnabled)
					{
						cumulus.FtpOptions.RealtimeEnabled = settings.websettings.realtime.enablerealtimeftp;
						cumulus.RealtimeInterval = settings.websettings.realtime.realtimeinterval * 1000;
						if (cumulus.RealtimeTimer.Interval != cumulus.RealtimeInterval)
							cumulus.RealtimeTimer.Interval = cumulus.RealtimeInterval;

						for (var i = 0; i < cumulus.RealtimeFiles.Length; i++)
						{
							cumulus.RealtimeFiles[i].Create = settings.websettings.realtime.files[i].create;
							cumulus.RealtimeFiles[i].FTP = cumulus.RealtimeFiles[i].Create && settings.websettings.realtime.files[i].ftp;
							cumulus.RealtimeFiles[i].Copy = cumulus.RealtimeFiles[i].Create && settings.websettings.realtime.files[i].copy;
						}
					}
					cumulus.RealtimeTimer.Enabled = cumulus.RealtimeIntervalEnabled;
					if (!cumulus.RealtimeTimer.Enabled || !cumulus.FtpOptions.RealtimeEnabled)
					{
						cumulus.RealtimeFTPDisconnect();
					}

					cumulus.WebIntervalEnabled = settings.websettings.interval.enabled;
					if (cumulus.WebIntervalEnabled)
					{
						cumulus.FtpOptions.IntervalEnabled = settings.websettings.interval.enableintervalftp;
						cumulus.UpdateInterval = settings.websettings.interval.ftpinterval;
						if (cumulus.WebTimer.Interval != cumulus.UpdateInterval * 60 * 1000)
							cumulus.WebTimer.Interval = cumulus.UpdateInterval * 60 * 1000;

						for (var i = 0; i < cumulus.StdWebFiles.Length; i++)
						{
							cumulus.StdWebFiles[i].Create = settings.websettings.interval.stdfiles.files[i].create;
							cumulus.StdWebFiles[i].FTP = cumulus.StdWebFiles[i].Create && settings.websettings.interval.stdfiles.files[i].ftp;
							cumulus.StdWebFiles[i].Copy = cumulus.StdWebFiles[i].Create && settings.websettings.interval.stdfiles.files[i].copy;
						}

						for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
						{
							cumulus.GraphDataFiles[i].Create = settings.websettings.interval.graphfiles.files[i].create;
							cumulus.GraphDataFiles[i].FTP = cumulus.GraphDataFiles[i].Create && settings.websettings.interval.graphfiles.files[i].ftp;
							cumulus.GraphDataFiles[i].Copy = cumulus.GraphDataFiles[i].Create && settings.websettings.interval.graphfiles.files[i].copy;
						}

						for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
						{
							cumulus.GraphDataEodFiles[i].Create = settings.websettings.interval.graphfileseod.files[i].create;
							cumulus.GraphDataEodFiles[i].FTP = cumulus.GraphDataEodFiles[i].Create && settings.websettings.interval.graphfileseod.files[i].ftp;
							cumulus.GraphDataEodFiles[i].Copy = cumulus.GraphDataEodFiles[i].Create && settings.websettings.interval.graphfileseod.files[i].copy;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing web settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// external programs
				try
				{
					if (settings.externalprograms != null)
					{
						cumulus.DailyProgram = settings.externalprograms.dailyprogram ?? string.Empty;
						cumulus.DailyParams = settings.externalprograms.dailyprogramparams ?? string.Empty;
						cumulus.ExternalProgram = settings.externalprograms.program ?? string.Empty;
						cumulus.ExternalParams = settings.externalprograms.programparams ?? string.Empty;
						cumulus.RealtimeProgram = settings.externalprograms.realtimeprogram ?? string.Empty;
						cumulus.RealtimeParams = settings.externalprograms.realtimeprogramparams ?? string.Empty;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing external programs: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// MQTT
				try
				{
					cumulus.MQTT.Server = settings.mqtt.server ?? string.Empty;
					cumulus.MQTT.Port = settings.mqtt.port;
					cumulus.MQTT.UseTLS = settings.mqtt.useTls;
					cumulus.MQTT.Username = settings.mqtt.username ?? string.Empty;
					cumulus.MQTT.Password = settings.mqtt.password ?? string.Empty;
					cumulus.MQTT.EnableDataUpdate = settings.mqtt.dataUpdate.enabled;
					if (cumulus.MQTT.EnableDataUpdate)
					{
						cumulus.MQTT.UpdateTemplate = settings.mqtt.dataUpdate.template ?? string.Empty;
					}
					cumulus.MQTT.EnableInterval = settings.mqtt.interval.enabled;
					if (cumulus.MQTT.EnableInterval)
					{
						cumulus.MQTT.IntervalTime = settings.mqtt.interval.time;
						cumulus.MQTT.IntervalTemplate = settings.mqtt.interval.template ?? string.Empty;

						cumulus.MQTTTimer.Interval = cumulus.MQTT.IntervalTime * 1000;
						cumulus.MQTTTimer.Enabled = cumulus.MQTT.EnableInterval && !string.IsNullOrWhiteSpace(cumulus.MQTT.IntervalTemplate);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing MQTT settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Moon Image
				try
				{
					cumulus.MoonImage.Enabled = settings.moonimage.enabled;
					if (cumulus.MoonImage.Enabled)
					{
						cumulus.MoonImage.Size = settings.moonimage.size;
						if (cumulus.MoonImage.Size < 10)
							cumulus.MoonImage.Size = 10;

						cumulus.MoonImage.Ftp = settings.moonimage.includemoonimage;
						if (cumulus.MoonImage.Ftp)
							cumulus.MoonImage.FtpDest = settings.moonimage.ftpdest;

						cumulus.MoonImage.Copy = settings.moonimage.copyimage;
						if (cumulus.MoonImage.Copy)
							cumulus.MoonImage.CopyDest = settings.moonimage.copydest;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Moon image settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
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
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// email settings
				try
				{

					cumulus.SmtpOptions.Enabled = settings.emailsettings.enabled;
					if (cumulus.SmtpOptions.Enabled)
					{
						cumulus.SmtpOptions.Server = settings.emailsettings.server;
						cumulus.SmtpOptions.Port = settings.emailsettings.port;
						cumulus.SmtpOptions.SslOption = settings.emailsettings.ssloption;
						cumulus.SmtpOptions.RequiresAuthentication = settings.emailsettings.authenticate;
						cumulus.SmtpOptions.User = settings.emailsettings.user;
						cumulus.SmtpOptions.Password = settings.emailsettings.password;

						if (cumulus.emailer == null)
						{
							cumulus.emailer = new EmailSender(cumulus);
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Email settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// misc settings
				try
				{
					cumulus.WebcamURL = settings.misc.webcamurl ?? string.Empty;
					cumulus.ForumURL = settings.misc.forumurl ?? string.Empty;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Misc settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// Save the settings
				cumulus.WriteIniFile();

				cumulus.SetUpHttpProxy();
				//cumulus.SetFtpLogging(cumulus.FTPlogging);

				// Setup MQTT
				if (cumulus.MQTT.EnableDataUpdate || cumulus.MQTT.EnableInterval)
				{
					if (!MqttPublisher.configured)
					{
						MqttPublisher.Setup(cumulus);
					}
					if (cumulus.MQTT.EnableInterval)
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
				var msg = "Error processing Internet settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Internet data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		public string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it

			var websettingsadvanced = new JsonInternetSettingsWebsiteAdvanced()
			{
				activeftp = cumulus.FtpOptions.ActiveMode,
				disableftpsepsv = cumulus.FtpOptions.DisableEPSV,
				disableftpsexplicit = cumulus.FtpOptions.DisableExplicit
			};

			var websettingsgeneral = new JsonInternetSettingsWebSettingsGeneral()
			{
				ftpdelete = cumulus.DeleteBeforeUpload,
				ftprename = cumulus.FTPRename,
				utf8encode = cumulus.UTF8encode,
			};

			var websitesettings = new JsonInternetSettingsWebsite()
			{
				localcopy = cumulus.FtpOptions.LocalCopyEnabled,
				localcopyfolder = cumulus.FtpOptions.LocalCopyFolder,
				enabled = cumulus.FtpOptions.Enabled,
				directory = cumulus.FtpOptions.Directory,
				ftpport = cumulus.FtpOptions.Port,
				sslftp = (int)cumulus.FtpOptions.FtpMode,
				hostname = cumulus.FtpOptions.Hostname,
				password = cumulus.FtpOptions.Password,
				username = cumulus.FtpOptions.Username,
				sshAuth = cumulus.FtpOptions.SshAuthen,
				pskFile = cumulus.FtpOptions.SshPskFile,
				general = websettingsgeneral,
				advanced = websettingsadvanced
			};

			var websettingsintervalstd = new JsonInternetSettingsWebSettingsIntervalFiles()
			{
				files = new JsonInternetSettingsFileSettings[cumulus.StdWebFiles.Length]
			};

			var websettingsintervalgraph = new JsonInternetSettingsWebSettingsIntervalFiles()
			{
				files = new JsonInternetSettingsFileSettings[cumulus.GraphDataFiles.Length]
			};

			var websettingsintervaleodgraph = new JsonInternetSettingsWebSettingsIntervalFiles()
			{
				files = new JsonInternetSettingsFileSettings[cumulus.GraphDataEodFiles.Length]
			};

			var websettingsinterval = new JsonInternetSettingsWebSettingsInterval()
			{
				enabled = cumulus.WebIntervalEnabled,
				enableintervalftp = cumulus.FtpOptions.IntervalEnabled,
				ftpinterval = cumulus.UpdateInterval,
				stdfiles = websettingsintervalstd,
				graphfiles = websettingsintervalgraph,
				graphfileseod = websettingsintervaleodgraph
			};

			for (var i = 0; i < cumulus.StdWebFiles.Length; i++)
			{
				websettingsinterval.stdfiles.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.StdWebFiles[i].LocalFileName,
					create = cumulus.StdWebFiles[i].Create,
					ftp = cumulus.StdWebFiles[i].FTP,
					copy = cumulus.StdWebFiles[i].Copy
				};
			}

			for (var i =0; i < cumulus.GraphDataFiles.Length; i++)
			{
				websettingsinterval.graphfiles.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.GraphDataFiles[i].LocalFileName,
					create = cumulus.GraphDataFiles[i].Create,
					ftp = cumulus.GraphDataFiles[i].FTP,
					copy = cumulus.GraphDataFiles[i].Copy
				};
			}

			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				websettingsinterval.graphfileseod.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.GraphDataEodFiles[i].LocalFileName,
					create = cumulus.GraphDataEodFiles[i].Create,
					ftp = cumulus.GraphDataEodFiles[i].FTP,
					copy = cumulus.GraphDataEodFiles[i].Copy
				};
			}

			var websettingsrealtime = new JsonInternetSettingsWebSettingsRealtime()
			{
				enabled = cumulus.RealtimeIntervalEnabled,
				enablerealtimeftp = cumulus.FtpOptions.RealtimeEnabled,
				realtimeinterval = cumulus.RealtimeInterval / 1000,
				files = new JsonInternetSettingsFileSettings[cumulus.RealtimeFiles.Length]
			};

			for (var i = 0; i < cumulus.RealtimeFiles.Length; i++)
			{
				websettingsrealtime.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.RealtimeFiles[i].LocalFileName,
					create = cumulus.RealtimeFiles[i].Create,
					ftp = cumulus.RealtimeFiles[i].FTP,
					copy = cumulus.RealtimeFiles[i].Copy
				};
			}

			var websettings = new JsonInternetSettingsWebSettings()
			{
				stdwebsite = false,
				interval = websettingsinterval,
				realtime = websettingsrealtime
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

			var mqttUpdate = new JsonInternetSettingsMqttDataupdate()
			{
				enabled = cumulus.MQTT.EnableDataUpdate,
				template = cumulus.MQTT.UpdateTemplate
			};

			var mqttInterval = new JsonInternetSettingsMqttInterval()
			{
				enabled = cumulus.MQTT.EnableInterval,
				time = cumulus.MQTT.IntervalTime,
				template = cumulus.MQTT.IntervalTemplate
			};

			var mqttsettings = new JsonInternetSettingsMqtt()
			{
				server = cumulus.MQTT.Server,
				port = cumulus.MQTT.Port,
				useTls = cumulus.MQTT.UseTLS,
				username = cumulus.MQTT.Username,
				password = cumulus.MQTT.Password,
				dataUpdate = mqttUpdate,
				interval = mqttInterval
			};

			var moonimagesettings = new JsonInternetSettingsMoonImage()
			{
				enabled = cumulus.MoonImage.Enabled,
				includemoonimage = cumulus.MoonImage.Ftp,
				size = cumulus.MoonImage.Size,
				ftpdest = cumulus.MoonImage.FtpDest,
				copyimage = cumulus.MoonImage.Copy,
				copydest = cumulus.MoonImage.CopyDest
			};

			var httpproxy = new JsonInternetSettingsHTTPproxySettings()
			{
				password = cumulus.HTTPProxyPassword,
				port = cumulus.HTTPProxyPort,
				proxy = cumulus.HTTPProxyName,
				user = cumulus.HTTPProxyUser
			};

			var proxy = new JsonInternetSettingsProxySettings() { httpproxy = httpproxy };

			var email = new JsonEmailSettings()
			{
				enabled = cumulus.SmtpOptions.Enabled,
				server = cumulus.SmtpOptions.Server,
				port = cumulus.SmtpOptions.Port,
				ssloption = cumulus.SmtpOptions.SslOption,
				authenticate = cumulus.SmtpOptions.RequiresAuthentication,
				user = cumulus.SmtpOptions.User,
				password = cumulus.SmtpOptions.Password
			};

			var misc = new JsonInternetSettingsMisc()
			{
				forumurl = cumulus.ForumURL,
				webcamurl = cumulus.WebcamURL
			};

			var data = new JsonInternetSettingsData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				website = websitesettings,
				websettings = websettings,
				externalprograms = externalprograms,
				mqtt = mqttsettings,
				moonimage = moonimagesettings,
				proxies = proxy,
				emailsettings = email,
				misc = misc
			};

			return data.ToJson();
		}

		public string GetExtraWebFilesData()
		{
			var json = new StringBuilder(10240);
			json.Append("{\"metadata\":[{\"name\":\"local\",\"label\":\"Local Filename\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"remote\",\"label\":\"Destination Filename\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"process\",\"label\":\"Process\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"realtime\",\"label\":\"Realtime\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"ftp\",\"label\":\"FTP\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"utf8\",\"label\":\"UTF8\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"binary\",\"label\":\"Binary\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"endofday\",\"label\":\"End of day\",\"datatype\":\"boolean\",\"editable\":true}],\"data\":[");

			for (int i = 0; i < Cumulus.numextrafiles; i++)
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

				if (i < Cumulus.numextrafiles - 1)
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
		public bool accessible { get; set; }
		public JsonInternetSettingsWebsite website { get; set; }
		public JsonInternetSettingsWebSettings websettings { get; set; }
		public JsonInternetSettingsExternalPrograms externalprograms { get; set; }
		public JsonInternetSettingsMqtt mqtt { get; set; }
		public JsonInternetSettingsMoonImage moonimage { get; set; }
		public JsonInternetSettingsProxySettings proxies { get; set; }
		public JsonEmailSettings emailsettings { get; set; }
		public JsonInternetSettingsMisc misc { get; set; }
	}

	public class JsonInternetSettingsWebsiteAdvanced
	{
		public bool activeftp { get; set; }
		public bool disableftpsepsv { get; set; }
		public bool disableftpsexplicit { get; set; }
	}

	public class JsonInternetSettingsWebsite
	{
		public bool localcopy { get; set; }
		public string localcopyfolder { get; set; }
		public bool enabled { get; set; }
		public string hostname { get; set; }
		public int ftpport { get; set; }
		public int sslftp { get; set; }
		public string directory { get; set; }
		public string username { get; set; }
		public string password { get; set; }
		public string sshAuth { get; set; }
		public string pskFile { get; set; }
		public JsonInternetSettingsWebSettingsGeneral general { get; set; }
		public JsonInternetSettingsWebsiteAdvanced advanced { get; set; }
	}

	public class JsonInternetSettingsWebSettings
	{
		public bool stdwebsite { get; set; }
		public JsonInternetSettingsWebSettingsInterval interval { get; set; }
		public JsonInternetSettingsWebSettingsRealtime realtime { get; set; }

	}

	public class JsonInternetSettingsWebSettingsGeneral
	{
		public bool ftprename { get; set; }
		public bool ftpdelete { get; set; }
		public bool utf8encode { get; set; }
	}

	public class JsonInternetSettingsFileSettings
	{
		public string filename { get; set; }
		public bool create { get; set; }
		public bool ftp { get; set; }
		public bool copy { get; set; }
	}

	public class JsonInternetSettingsWebSettingsInterval
	{
		public bool enabled { get; set; }
		public bool enableintervalftp { get; set; }
		public bool enablecopy { get; set; }
		public int ftpinterval { get; set; }
		public JsonInternetSettingsWebSettingsIntervalFiles stdfiles { get; set; }
		public JsonInternetSettingsWebSettingsIntervalFiles graphfiles { get; set; }
		public JsonInternetSettingsWebSettingsIntervalFiles graphfileseod { get; set; }
	}

	public class JsonInternetSettingsWebSettingsIntervalFiles
	{
		public JsonInternetSettingsFileSettings[] files { get; set; }

	}

	public class JsonInternetSettingsWebSettingsRealtime
	{
		public bool enabled { get; set; }
		public bool enablerealtimeftp { get; set; }
		//public bool enablecopy { get; set; }
		public int realtimeinterval { get; set; }
		public JsonInternetSettingsFileSettings[] files { get; set; }
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
		public string template { get; set; }
	}

	public class JsonInternetSettingsMqttInterval
	{
		public bool enabled { get; set; }
		public int time { get; set; }
		public string topic { get; set; }
		public string template { get; set; }
		public bool retained { get; set; }
	}

	public class JsonInternetSettingsMoonImage
	{
		public bool enabled { get; set; }
		public bool includemoonimage { get; set; }
		public int size { get; set; }
		public string ftpdest { get; set; }
		public bool copyimage { get; set; }
		public string copydest { get; set; }
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

	public class JsonEmailSettings
	{
		public bool enabled { get; set; }
		public string server { get; set; }
		public int port { get; set; }
		public int ssloption { get; set; }
		public bool authenticate { get; set; }
		public string user { get; set; }
		public string password { get; set; }
	}

	public class JsonInternetSettingsMisc
	{
		public string forumurl { get; set; }
		public string webcamurl { get; set; }
	}
}
