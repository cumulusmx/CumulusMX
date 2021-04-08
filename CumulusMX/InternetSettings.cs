using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using ServiceStack;
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
			internetOptionsFile = cumulus.AppDir + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "InternetOptions.json";
			internetSchemaFile = cumulus.AppDir + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "InternetSchema.json";
		}

		public string UpdateInternetConfig(IHttpContext context)
		{
			var errorMsg = "";
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = json.FromJson<JsonInternetSettingsData>();
				// process the settings
				cumulus.LogMessage("Updating internet settings");

				// website settings
				try
				{
					cumulus.FtpDirectory = settings.website.directory ?? string.Empty;
					cumulus.ForumURL = settings.website.forumurl ?? string.Empty;
					cumulus.FtpHostPort = settings.website.ftpport;
					cumulus.FtpHostname = settings.website.hostname ?? string.Empty;
					cumulus.Sslftp = (Cumulus.FtpProtocols)settings.website.sslftp;
					cumulus.FtpPassword = settings.website.password ?? string.Empty;
					cumulus.FtpUsername = settings.website.username ?? string.Empty;
					cumulus.SshftpAuthentication = settings.website.sshAuth ?? string.Empty;
					cumulus.SshftpPskFile = settings.website.pskFile ?? string.Empty;
					cumulus.WebcamURL = settings.website.webcamurl ?? string.Empty;

					if (cumulus.Sslftp == Cumulus.FtpProtocols.FTP || cumulus.Sslftp == Cumulus.FtpProtocols.FTPS) {
						cumulus.ActiveFTPMode = settings.website.advanced.activeftp;
						cumulus.DisableFtpsEPSV = settings.website.advanced.disableftpsepsv;
					}

					if (cumulus.Sslftp == Cumulus.FtpProtocols.FTPS)
					{
						cumulus.DisableFtpsExplicit = settings.website.advanced.disableftpsexplicit;
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
					cumulus.DeleteBeforeUpload = settings.websettings.general.ftpdelete;
					cumulus.FTPRename = settings.websettings.general.ftprename;
					cumulus.UTF8encode = settings.websettings.general.utf8encode;

					cumulus.RealtimeEnabled = settings.websettings.realtime.enabled;
					if (cumulus.RealtimeEnabled)
					{
						cumulus.RealtimeFTPEnabled = settings.websettings.realtime.enablerealtimeftp;
						cumulus.RealtimeInterval = settings.websettings.realtime.realtimeinterval * 1000;

						for (var i = 0; i < cumulus.RealtimeFiles.Length; i++)
						{
							cumulus.RealtimeFiles[i].Create = settings.websettings.realtime.files[i].create;
							cumulus.RealtimeFiles[i].FTP = settings.websettings.realtime.files[i].ftp;
						}
					}
					cumulus.RealtimeTimer.Enabled = cumulus.RealtimeEnabled;
					if (!cumulus.RealtimeTimer.Enabled || !cumulus.RealtimeFTPEnabled)
					{
						cumulus.RealtimeFTPDisconnect();
					}

					cumulus.WebIntervalEnabled = settings.websettings.interval.enabled;
					if (cumulus.WebIntervalEnabled)
					{
						cumulus.WebAutoUpdate = settings.websettings.interval.autoupdate;
						cumulus.UpdateInterval = settings.websettings.interval.ftpinterval;

						for (var i = 0; i < cumulus.StdWebFiles.Length; i++)
						{
							cumulus.StdWebFiles[i].Create = settings.websettings.interval.stdfiles.files[i].create;
							cumulus.StdWebFiles[i].FTP = cumulus.StdWebFiles[i].Create && settings.websettings.interval.stdfiles.files[i].ftp;
						}

						for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
						{
							cumulus.GraphDataFiles[i].Create = settings.websettings.interval.graphfiles.files[i].create;
							cumulus.GraphDataFiles[i].FTP = settings.websettings.interval.graphfiles.files[i].ftp;
						}

						for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
						{
							cumulus.GraphDataEodFiles[i].Create = settings.websettings.interval.graphfileseod.files[i].create;
							cumulus.GraphDataEodFiles[i].FTP = settings.websettings.interval.graphfileseod.files[i].ftp;
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

				// twitter
				try
				{
					cumulus.Twitter.Enabled = settings.twitter.enabled;
					if (cumulus.Twitter.Enabled)
					{
						cumulus.Twitter.Interval = settings.twitter.interval;
						cumulus.Twitter.PW = settings.twitter.password ?? string.Empty;
						cumulus.Twitter.SendLocation = settings.twitter.sendlocation;
						cumulus.Twitter.ID = settings.twitter.user ?? string.Empty;
						cumulus.Twitter.SynchronisedUpdate = (60 % cumulus.Twitter.Interval == 0);

						cumulus.TwitterTimer.Interval = cumulus.Twitter.Interval * 60 * 1000;
						cumulus.TwitterTimer.Enabled = cumulus.Twitter.Enabled && !cumulus.Twitter.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.Twitter.ID) && !string.IsNullOrWhiteSpace(cumulus.Twitter.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing twitter settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// wunderground
				try
				{
					cumulus.Wund.Enabled = settings.wunderground.enabled;
					if (cumulus.Wund.Enabled)
					{
						cumulus.Wund.SendIndoor = settings.wunderground.includeindoor;
						cumulus.Wund.SendSolar = settings.wunderground.includesolar;
						cumulus.Wund.SendUV = settings.wunderground.includeuv;
						cumulus.Wund.SendAirQuality = settings.wunderground.includeaq;
						cumulus.Wund.Interval = settings.wunderground.interval;
						cumulus.Wund.PW = settings.wunderground.password ?? string.Empty;
						cumulus.Wund.RapidFireEnabled = settings.wunderground.rapidfire;
						cumulus.Wund.SendAverage = settings.wunderground.sendavgwind;
						cumulus.Wund.ID = settings.wunderground.stationid ?? string.Empty;
						cumulus.Wund.CatchUp = settings.wunderground.catchup;
						cumulus.Wund.SynchronisedUpdate = (!cumulus.Wund.RapidFireEnabled) && (60 % cumulus.Wund.Interval == 0);

						cumulus.WundTimer.Interval = cumulus.Wund.RapidFireEnabled ? 5000 : cumulus.Wund.Interval * 60 * 1000;
						cumulus.WundTimer.Enabled = cumulus.Wund.Enabled && !cumulus.Wund.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.Wund.ID) && !string.IsNullOrWhiteSpace(cumulus.Wund.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing wunderground settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Windy
				try
				{
					cumulus.Windy.Enabled = settings.windy.enabled;
					if (cumulus.Windy.Enabled)
					{
						//cumulus.WindySendSolar = settings.windy.includesolar;
						cumulus.Windy.SendUV = settings.windy.includeuv;
						cumulus.Windy.Interval = settings.windy.interval;
						cumulus.Windy.ApiKey = settings.windy.apikey;
						cumulus.Windy.StationIdx = settings.windy.stationidx;
						cumulus.Windy.CatchUp = settings.windy.catchup;
						cumulus.Windy.SynchronisedUpdate = (60 % cumulus.Windy.Interval == 0);

						cumulus.WindyTimer.Interval = cumulus.Windy.Interval * 60 * 1000;
						cumulus.WindyTimer.Enabled = cumulus.Windy.Enabled && !cumulus.Windy.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.Windy.ApiKey);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Windy settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Awekas
				try
				{
					cumulus.AWEKAS.Enabled = settings.awekas.enabled;
					if (cumulus.AWEKAS.Enabled)
					{
						cumulus.AWEKAS.Interval = settings.awekas.interval;
						cumulus.AWEKAS.Lang = settings.awekas.lang;
						cumulus.AWEKAS.PW = settings.awekas.password ?? string.Empty;
						cumulus.AWEKAS.ID = settings.awekas.user ?? string.Empty;
						cumulus.AWEKAS.SendSolar = settings.awekas.includesolar;
						cumulus.AWEKAS.SendUV = settings.awekas.includeuv;
						cumulus.AWEKAS.SendSoilTemp = settings.awekas.includesoiltemp;
						cumulus.AWEKAS.SendSoilMoisture = settings.awekas.includesoilmoisture;
						cumulus.AWEKAS.SendLeafWetness = settings.awekas.includeleafwetness;
						cumulus.AWEKAS.SendIndoor = settings.awekas.includeindoor;
						cumulus.AWEKAS.SendAirQuality = settings.awekas.includeaq;
						cumulus.AWEKAS.SynchronisedUpdate = (cumulus.AWEKAS.Interval % 60 == 0);

						cumulus.AwekasTimer.Interval = cumulus.AWEKAS.Interval * 1000;
						cumulus.AwekasTimer.Enabled = cumulus.AWEKAS.Enabled && !cumulus.AWEKAS.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.AWEKAS.ID) && !string.IsNullOrWhiteSpace(cumulus.AWEKAS.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing AWEKAS settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WeatherCloud
				try
				{
					cumulus.WCloud.Enabled = settings.weathercloud.enabled;
					if (cumulus.WCloud.Enabled)
					{
						cumulus.WCloud.ID = settings.weathercloud.wid ?? string.Empty;
						cumulus.WCloud.PW = settings.weathercloud.key ?? string.Empty;
						cumulus.WCloud.Interval = settings.weathercloud.interval;
						cumulus.WCloud.SendSolar = settings.weathercloud.includesolar;
						cumulus.WCloud.SendUV = settings.weathercloud.includeuv;
						cumulus.WCloud.SendAQI = settings.weathercloud.includeaqi;
						cumulus.WCloud.SynchronisedUpdate = (60 % cumulus.WCloud.Interval == 0);

						cumulus.WCloudTimer.Interval = cumulus.WCloud.Interval * 60 * 1000;
						cumulus.WCloudTimer.Enabled = cumulus.WCloud.Enabled && !cumulus.WCloud.SynchronisedUpdate && !String.IsNullOrWhiteSpace(cumulus.WCloud.ID) && !String.IsNullOrWhiteSpace(cumulus.WCloud.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WeatherCloud settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// PWS weather
				try
				{
					cumulus.PWS.Enabled = settings.pwsweather.enabled;
					if (cumulus.PWS.Enabled)
					{
						cumulus.PWS.Interval = settings.pwsweather.interval;
						cumulus.PWS.SendSolar = settings.pwsweather.includesolar;
						cumulus.PWS.SendUV = settings.pwsweather.includeuv;
						cumulus.PWS.PW = settings.pwsweather.password ?? string.Empty;
						cumulus.PWS.ID = settings.pwsweather.stationid ?? string.Empty;
						cumulus.PWS.CatchUp = settings.pwsweather.catchup;
						cumulus.PWS.SynchronisedUpdate = (60 % cumulus.PWS.Interval == 0);

						cumulus.PWSTimer.Interval = cumulus.PWS.Interval * 60 * 1000;
						cumulus.PWSTimer.Enabled = cumulus.PWS.Enabled && !cumulus.PWS.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.PWS.ID) && !string.IsNullOrWhiteSpace(cumulus.PWS.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing PWS weather settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WOW
				try
				{
					cumulus.WOW.Enabled = settings.wow.enabled;
					if (cumulus.WOW.Enabled)
					{
						cumulus.WOW.SendSolar = settings.wow.includesolar;
						cumulus.WOW.SendUV = settings.wow.includeuv;
						cumulus.WOW.Interval = settings.wow.interval;
						cumulus.WOW.PW = settings.wow.password ?? string.Empty; ;
						cumulus.WOW.ID = settings.wow.stationid ?? string.Empty; ;
						cumulus.WOW.CatchUp = settings.wow.catchup;
						cumulus.WOW.SynchronisedUpdate = (60 % cumulus.WOW.Interval == 0);

						cumulus.WOWTimer.Interval = cumulus.WOW.Interval * 60 * 1000;
						cumulus.WOWTimer.Enabled = cumulus.WOW.Enabled && !cumulus.WOW.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.WOW.ID) && !string.IsNullOrWhiteSpace(cumulus.WOW.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WOW settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// CWOP
				try
				{
					cumulus.APRS.Enabled = settings.cwop.enabled;
					if (cumulus.APRS.Enabled)
					{
						cumulus.APRS.ID = settings.cwop.id ?? string.Empty; ;
						cumulus.APRS.Interval = settings.cwop.interval;
						cumulus.APRS.SendSolar = settings.cwop.includesolar;
						cumulus.APRS.PW = settings.cwop.password ?? string.Empty; ;
						cumulus.APRS.Port = settings.cwop.port;
						cumulus.APRS.Server = settings.cwop.server ?? string.Empty; ;
						cumulus.APRS.SynchronisedUpdate = (60 % cumulus.APRS.Interval == 0);

						cumulus.APRStimer.Interval = cumulus.APRS.Interval * 60 * 1000;
						cumulus.APRStimer.Enabled = cumulus.APRS.Enabled && !cumulus.APRS.SynchronisedUpdate && !string.IsNullOrWhiteSpace(cumulus.APRS.ID) && !string.IsNullOrWhiteSpace(cumulus.APRS.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing CWOP settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// OpenWeatherMap
				try
				{
					cumulus.OpenWeatherMap.Enabled = settings.openweathermap.enabled;
					if (cumulus.OpenWeatherMap.Enabled)
					{
						cumulus.OpenWeatherMap.CatchUp = settings.openweathermap.catchup;
						cumulus.OpenWeatherMap.PW = settings.openweathermap.apikey;
						cumulus.OpenWeatherMap.ID = settings.openweathermap.stationid;
						cumulus.OpenWeatherMap.Interval = settings.openweathermap.interval;
						cumulus.OpenWeatherMap.SynchronisedUpdate = (60 % cumulus.OpenWeatherMap.Interval == 0);

						cumulus.OpenWeatherMapTimer.Interval = cumulus.OpenWeatherMap.Interval * 60 * 1000;
						cumulus.OpenWeatherMapTimer.Enabled = cumulus.OpenWeatherMap.Enabled && !string.IsNullOrWhiteSpace(cumulus.OpenWeatherMap.PW);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing OpenWeatherMap settings: " + ex.Message;
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
						cumulus.MQTT.UpdateTopic = settings.mqtt.dataUpdate.topic ?? string.Empty;
						cumulus.MQTT.UpdateTemplate = settings.mqtt.dataUpdate.template ?? string.Empty;
						cumulus.MQTT.UpdateRetained = settings.mqtt.dataUpdate.retained;
					}
					cumulus.MQTT.EnableInterval = settings.mqtt.interval.enabled;
					if (cumulus.MQTT.EnableInterval)
					{
						cumulus.MQTT.IntervalTime = settings.mqtt.interval.time;
						cumulus.MQTT.IntervalTopic = settings.mqtt.interval.topic ?? string.Empty;
						cumulus.MQTT.IntervalTemplate = settings.mqtt.interval.template ?? string.Empty;
						cumulus.MQTT.IntervalRetained = settings.mqtt.interval.retained;

						cumulus.MQTTTimer.Interval = cumulus.MQTT.IntervalTime * 1000;
						cumulus.MQTTTimer.Enabled = cumulus.MQTT.EnableInterval && !string.IsNullOrWhiteSpace(cumulus.MQTT.IntervalTopic) && !string.IsNullOrWhiteSpace(cumulus.MQTT.IntervalTemplate);
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
					cumulus.MoonImageEnabled = settings.moonimage.enabled;
					if (cumulus.MoonImageEnabled)
					{
						cumulus.MoonImageSize = settings.moonimage.size;
						cumulus.IncludeMoonImage = settings.moonimage.includemoonimage;
						if (cumulus.IncludeMoonImage)
							cumulus.MoonImageFtpDest = settings.moonimage.ftpdest;
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

				// Custom HTTP
				try
				{
					// custom seconds
					cumulus.CustomHttpSecondsEnabled = settings.customhttp.customseconds.enabled;
					if (cumulus.CustomHttpSecondsEnabled)
					{
						cumulus.CustomHttpSecondsString = settings.customhttp.customseconds.url ?? string.Empty;
						cumulus.CustomHttpSecondsInterval = settings.customhttp.customseconds.interval;
						cumulus.CustomHttpSecondsTimer.Interval = cumulus.CustomHttpSecondsInterval * 1000;
						cumulus.CustomHttpSecondsTimer.Enabled = cumulus.CustomHttpSecondsEnabled;
					}
					// custom minutes
					cumulus.CustomHttpMinutesEnabled = settings.customhttp.customminutes.enabled;
					if (cumulus.CustomHttpMinutesEnabled)
					{
						cumulus.CustomHttpMinutesString = settings.customhttp.customminutes.url ?? string.Empty;
						cumulus.CustomHttpMinutesIntervalIndex = settings.customhttp.customminutes.intervalindex;
						if (cumulus.CustomHttpMinutesIntervalIndex >= 0 && cumulus.CustomHttpMinutesIntervalIndex < cumulus.FactorsOf60.Length)
						{
							cumulus.CustomHttpMinutesInterval = cumulus.FactorsOf60[cumulus.CustomHttpMinutesIntervalIndex];
						}
						else
						{
							cumulus.CustomHttpMinutesInterval = 10;
						}
					}
					// custom rollover
					cumulus.CustomHttpRolloverEnabled = settings.customhttp.customrollover.enabled;
					if (cumulus.CustomHttpRolloverEnabled)
					{
						cumulus.CustomHttpRolloverString = settings.customhttp.customrollover.url ?? string.Empty;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Custom settings: " + ex.Message;
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
						cumulus.SmtpOptions.UseSsl = settings.emailsettings.usessl;
						cumulus.SmtpOptions.RequiresAuthentication = settings.emailsettings.authenticate;
						cumulus.SmtpOptions.User = settings.emailsettings.user;
						cumulus.SmtpOptions.Password = settings.emailsettings.password;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Email settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();

				// Do OpenWeatherMap setup
				cumulus.EnableOpenWeatherMap();

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
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		public string GetInternetAlpacaFormData()
		{
			var websettingsadvanced = new JsonInternetSettingsWebsiteAdvanced()
			{
				activeftp = cumulus.ActiveFTPMode,
				disableftpsepsv = cumulus.DisableFtpsEPSV,
				disableftpsexplicit = cumulus.DisableFtpsExplicit
			};

			// Build the settings data, convert to JSON, and return it
			var websitesettings = new JsonInternetSettingsWebsite()
			{
				directory = cumulus.FtpDirectory,
				forumurl = cumulus.ForumURL,
				ftpport = cumulus.FtpHostPort,
				sslftp = (int)cumulus.Sslftp,
				hostname = cumulus.FtpHostname,
				password = cumulus.FtpPassword,
				username = cumulus.FtpUsername,
				sshAuth = cumulus.SshftpAuthentication,
				pskFile = cumulus.SshftpPskFile,
				webcamurl = cumulus.WebcamURL,
				advanced = websettingsadvanced
			};

			var websettingsgeneral = new JsonInternetSettingsWebSettingsGeneral()
			{
				ftpdelete = cumulus.DeleteBeforeUpload,
				ftprename = cumulus.FTPRename,
				utf8encode = cumulus.UTF8encode,
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
				autoupdate = cumulus.WebAutoUpdate,
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
					ftp = cumulus.StdWebFiles[i].FTP
				};
			}

			for (var i =0; i < cumulus.GraphDataFiles.Length; i++)
			{
				websettingsinterval.graphfiles.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.GraphDataFiles[i].LocalFileName,
					create = cumulus.GraphDataFiles[i].Create,
					ftp = cumulus.GraphDataFiles[i].FTP
				};
			}

			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				websettingsinterval.graphfileseod.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.GraphDataEodFiles[i].LocalFileName,
					create = cumulus.GraphDataEodFiles[i].Create,
					ftp = cumulus.GraphDataEodFiles[i].FTP
				};
			}

			var websettingsrealtime = new JsonInternetSettingsWebSettingsRealtime()
			{
				enabled = cumulus.RealtimeEnabled,
				enablerealtimeftp = cumulus.RealtimeFTPEnabled,
				realtimeinterval = cumulus.RealtimeInterval / 1000,
				files = new JsonInternetSettingsFileSettings[cumulus.RealtimeFiles.Length]
			};

			for (var i = 0; i < cumulus.RealtimeFiles.Length; i++)
			{
				websettingsrealtime.files[i] = new JsonInternetSettingsFileSettings()
				{
					filename = cumulus.RealtimeFiles[i].LocalFileName,
					create = cumulus.RealtimeFiles[i].Create,
					ftp = cumulus.RealtimeFiles[i].FTP
				};
			}

			var websettings = new JsonInternetSettingsWebSettings()
			{
				stdwebsite = false,
				general = websettingsgeneral,
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

			var twittersettings = new JsonInternetSettingsTwitterSettings()
			{
				enabled = cumulus.Twitter.Enabled,
				interval = cumulus.Twitter.Interval,
				password = cumulus.Twitter.PW,
				sendlocation = cumulus.Twitter.SendLocation,
				user = cumulus.Twitter.ID
			};

			var wusettings = new JsonInternetSettingsWunderground()
			{
				catchup = cumulus.Wund.CatchUp,
				enabled = cumulus.Wund.Enabled,
				includeindoor = cumulus.Wund.SendIndoor,
				includesolar = cumulus.Wund.SendSolar,
				includeuv = cumulus.Wund.SendUV,
				interval = cumulus.Wund.Interval,
				password = cumulus.Wund.PW,
				rapidfire = cumulus.Wund.RapidFireEnabled,
				sendavgwind = cumulus.Wund.SendAverage,
				stationid = cumulus.Wund.ID,
				includeaq = cumulus.Wund.SendAirQuality
			};

			var windysettings = new JsonInternetSettingsWindy()
			{
				catchup = cumulus.Windy.CatchUp,
				enabled = cumulus.Windy.Enabled,
				includeuv = cumulus.Windy.SendUV,
				interval = cumulus.Windy.Interval,
				apikey = cumulus.Windy.ApiKey,
				stationidx = cumulus.Windy.StationIdx
			};

			var awekassettings = new JsonInternetSettingsAwekas()
			{
				enabled = cumulus.AWEKAS.Enabled,
				includesolar = cumulus.AWEKAS.SendSolar,
				includesoiltemp = cumulus.AWEKAS.SendSoilTemp,
				includesoilmoisture = cumulus.AWEKAS.SendSoilMoisture,
				includeleafwetness = cumulus.AWEKAS.SendLeafWetness,
				includeindoor = cumulus.AWEKAS.SendIndoor,
				includeuv = cumulus.AWEKAS.SendUV,
				includeaq =  cumulus.AWEKAS.SendAirQuality,
				interval = cumulus.AWEKAS.Interval,
				lang = cumulus.AWEKAS.Lang,
				password = cumulus.AWEKAS.PW,
				user = cumulus.AWEKAS.ID
			};

			var wcloudsettings = new JsonInternetSettingsWCloud()
			{
				enabled = cumulus.WCloud.Enabled,
				interval = cumulus.WCloud.Interval,
				includesolar = cumulus.WCloud.SendSolar,
				includeuv = cumulus.WCloud.SendUV,
				includeaqi = cumulus.WCloud.SendAQI,
				key = cumulus.WCloud.PW,
				wid = cumulus.WCloud.ID
			};

			var pwssettings = new JsonInternetSettingsPWSweather()
			{
				catchup = cumulus.PWS.CatchUp,
				enabled = cumulus.PWS.Enabled,
				interval = cumulus.PWS.Interval,
				includesolar = cumulus.PWS.SendSolar,
				includeuv = cumulus.PWS.SendUV,
				password = cumulus.PWS.PW,
				stationid = cumulus.PWS.ID
			};

			var wowsettings = new JsonInternetSettingsWOW()
			{
				catchup = cumulus.WOW.CatchUp,
				enabled = cumulus.WOW.Enabled,
				includesolar = cumulus.WOW.SendSolar,
				includeuv = cumulus.WOW.SendUV,
				interval = cumulus.WOW.Interval,
				password = cumulus.WOW.PW,
				stationid = cumulus.WOW.ID
			};

			var cwopsettings = new JsonInternetSettingsCwop()
			{
				enabled = cumulus.APRS.Enabled,
				id = cumulus.APRS.ID,
				interval = cumulus.APRS.Interval,
				includesolar = cumulus.APRS.SendSolar,
				password = cumulus.APRS.PW,
				port = cumulus.APRS.Port,
				server = cumulus.APRS.Server
			};

			var openweathermapsettings = new JsonInternetSettingsOpenweatherMap()
			{
				enabled = cumulus.OpenWeatherMap.Enabled,
				catchup = cumulus.OpenWeatherMap.CatchUp,
				apikey = cumulus.OpenWeatherMap.PW,
				stationid = cumulus.OpenWeatherMap.ID,
				interval = cumulus.OpenWeatherMap.Interval
			};

			var mqttUpdate = new JsonInternetSettingsMqttDataupdate()
			{
				enabled = cumulus.MQTT.EnableDataUpdate,
				topic = cumulus.MQTT.UpdateTopic,
				template = cumulus.MQTT.UpdateTemplate,
				retained = cumulus.MQTT.UpdateRetained
			};

			var mqttInterval = new JsonInternetSettingsMqttInterval()
			{
				enabled = cumulus.MQTT.EnableInterval,
				time = cumulus.MQTT.IntervalTime,
				topic = cumulus.MQTT.IntervalTopic,
				template = cumulus.MQTT.IntervalTemplate,
				retained = cumulus.MQTT.UpdateRetained
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
				enabled = cumulus.MoonImageEnabled,
				includemoonimage = cumulus.IncludeMoonImage,
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

			var email = new JsonEmailSettings()
			{
				enabled = cumulus.SmtpOptions.Enabled,
				server = cumulus.SmtpOptions.Server,
				port = cumulus.SmtpOptions.Port,
				usessl = cumulus.SmtpOptions.UseSsl,
				authenticate = cumulus.SmtpOptions.RequiresAuthentication,
				user = cumulus.SmtpOptions.User,
				password = cumulus.SmtpOptions.Password
			};

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
				openweathermap = openweathermapsettings,
				mqtt = mqttsettings,
				moonimage = moonimagesettings,
				proxies = proxy,
				customhttp = customhttp,
				emailsettings = email
			};

			return data.ToJson();
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
			json.Append("{\"metadata\":[{\"name\":\"local\",\"label\":\"Local Filename\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"remote\",\"label\":\"Destination Filename\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"process\",\"label\":\"Process\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"realtime\",\"label\":\"Realtime\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"ftp\",\"label\":\"FTP\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"utf8\",\"label\":\"UTF-8\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"binary\",\"label\":\"Binary\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"endofday\",\"label\":\"End of day\",\"datatype\":\"boolean\",\"editable\":true}],\"data\":[");

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
		public JsonInternetSettingsOpenweatherMap openweathermap { get; set; }
		public JsonInternetSettingsMqtt mqtt { get; set; }
		public JsonInternetSettingsMoonImage moonimage { get; set; }
		public JsonInternetSettingsProxySettings proxies { get; set; }
		public JsonInternetSettingsCustomHttpSettings customhttp { get; set; }
		public JsonEmailSettings emailsettings { get; set; }
	}

	public class JsonInternetSettingsWebsiteAdvanced
	{
		public bool activeftp { get; set; }
		public bool disableftpsepsv { get; set; }
		public bool disableftpsexplicit { get; set; }
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
		public JsonInternetSettingsWebsiteAdvanced advanced { get; set; }
	}

	public class JsonInternetSettingsWebSettings
	{
		public bool stdwebsite { get; set; }
		public JsonInternetSettingsWebSettingsGeneral general { get; set; }
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
	}

	public class JsonInternetSettingsWebSettingsInterval
	{
		public bool enabled { get; set; }
		public bool autoupdate { get; set; }
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
		public bool includeaq { get; set; }
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
		public bool includesoilmoisture { get; set; }
		public bool includeleafwetness { get; set; }
		public bool includeindoor { get; set; }
		public bool includeaq { get; set; }
		public string user { get; set; }
		public string password { get; set; }
		public string lang { get; set; }
		public int interval { get; set; }
	}

	public class JsonInternetSettingsWCloud
	{
		public bool enabled { get; set; }
		public int interval { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool includeaqi { get; set; }
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

	public class JsonInternetSettingsOpenweatherMap
	{
		public bool enabled { get; set; }
		public string apikey { get; set; }
		public string stationid { get; set; }
		public int interval { get; set; }
		public bool catchup { get; set; }
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
		public bool retained { get; set; }
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
		public bool usessl { get; set; }
		public bool authenticate { get; set; }
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
