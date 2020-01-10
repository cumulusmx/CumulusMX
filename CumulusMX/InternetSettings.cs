using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
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
			internetOptionsFile = AppDomain.CurrentDomain.BaseDirectory + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "InternetOptions.json";
			internetSchemaFile = AppDomain.CurrentDomain.BaseDirectory + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "InternetSchema.json";
		}

		//public string UpdateInternetConfig(HttpListenerContext context)
		public string UpdateInternetConfig(IHttpContext context)
		{
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
				cumulus.ftp_directory = settings.website.directory ?? string.Empty;
				cumulus.ForumURL = settings.website.forumurl ?? string.Empty;
				cumulus.ftp_port = settings.website.ftpport;
				cumulus.ftp_host = settings.website.hostname ?? string.Empty;
				cumulus.Sslftp = (Cumulus.FtpProtocols)settings.website.sslftp;
				cumulus.ftp_password = settings.website.password ?? string.Empty;
				cumulus.ftp_user = settings.website.username ?? string.Empty;
				cumulus.WebcamURL = settings.website.webcamurl ?? string.Empty;

				// web settings
				cumulus.ActiveFTPMode = settings.websettings.activeftp;
				cumulus.WebAutoUpdate = settings.websettings.autoupdate;
				cumulus.RealtimeEnabled = settings.websettings.enablerealtime;
				cumulus.RealtimeFTPEnabled = settings.websettings.enablerealtimeftp;
				cumulus.RealtimeTxtFTP = settings.websettings.realtimetxtftp;
				cumulus.RealtimeGaugesTxtFTP = settings.websettings.realtimegaugestxtftp;
				cumulus.RealtimeInterval = settings.websettings.realtimeinterval*1000;
				cumulus.DeleteBeforeUpload = settings.websettings.ftpdelete;
				cumulus.UpdateInterval = settings.websettings.ftpinterval;
				cumulus.FTPRename = settings.websettings.ftprename;
				cumulus.IncludeStandardFiles = settings.websettings.includestdfiles;
				cumulus.IncludeGraphDataFiles = settings.websettings.includegraphdatafiles;
				cumulus.UTF8encode = settings.websettings.utf8encode;
				if (settings.websettings.ftplogging != cumulus.FTPlogging)
				{
					cumulus.FTPlogging = settings.websettings.ftplogging;
					cumulus.SetFtpLogging(cumulus.FTPlogging);
				}

				// external programs
				cumulus.DailyProgram = settings.externalprograms.dailyprogram ?? string.Empty;
				cumulus.DailyParams = settings.externalprograms.dailyprogramparams ?? string.Empty;
				cumulus.ExternalProgram = settings.externalprograms.program ?? string.Empty;
				cumulus.ExternalParams = settings.externalprograms.programparams ?? string.Empty;
				cumulus.RealtimeProgram = settings.externalprograms.realtimeprogram ?? string.Empty;
				cumulus.RealtimeParams = settings.externalprograms.realtimeprogramparams ?? string.Empty;

				// twitter
				cumulus.TwitterEnabled = settings.twitter.enabled;
				cumulus.TwitterInterval = settings.twitter.interval;
				cumulus.TwitterPW = settings.twitter.password ?? string.Empty;
				cumulus.TwitterSendLocation = settings.twitter.sendlocation;
				cumulus.Twitteruser = settings.twitter.user ?? string.Empty;

				cumulus.SynchronisedTwitterUpdate = (60 % cumulus.TwitterInterval == 0);

				// wunderground
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

				// Windy
				cumulus.WindyCatchUp = settings.windy.catchup;
				cumulus.WindyEnabled = settings.windy.enabled;
				//cumulus.WindySendSolar = settings.windy.includesolar;
				cumulus.WindySendUV = settings.windy.includeuv;
				cumulus.WindyInterval = settings.windy.interval;
				cumulus.WindyApiKey = settings.windy.apikey;
				cumulus.WindyStationIdx = settings.windy.stationidx;

				cumulus.SynchronisedWindyUpdate = (60 % cumulus.WindyInterval == 0);

				// Awekas
				cumulus.AwekasEnabled = settings.awekas.enabled;
				cumulus.AwekasInterval = settings.awekas.interval;
				cumulus.AwekasLang = settings.awekas.lang;
				cumulus.AwekasPW = settings.awekas.password ?? string.Empty;
				cumulus.AwekasUser = settings.awekas.user ?? string.Empty;
				cumulus.SendSolarToAwekas = settings.awekas.includesolar;
				cumulus.SendUVToAwekas = settings.awekas.includeuv;
				cumulus.SendSoilTempToAwekas = settings.awekas.includesoiltemp;

				cumulus.SynchronisedAwekasUpdate = (60 % cumulus.AwekasInterval == 0);

				// WeatherCloud
				cumulus.WCloudWid = settings.weathercloud.wid ?? string.Empty;
				cumulus.WCloudKey = settings.weathercloud.key ?? string.Empty;
				cumulus.WCloudEnabled = settings.weathercloud.enabled;
				cumulus.SendSolarToWCloud = settings.weathercloud.includesolar;
				cumulus.SendUVToWCloud = settings.weathercloud.includeuv;

				cumulus.SynchronisedWCloudUpdate = (60 % cumulus.WCloudInterval == 0);

				// PWS weather
				cumulus.PWSCatchUp = settings.pwsweather.catchup;
				cumulus.PWSEnabled = settings.pwsweather.enabled;
				cumulus.PWSInterval = settings.pwsweather.interval;
				cumulus.SendSRToPWS = settings.pwsweather.includesolar;
				cumulus.SendUVToPWS = settings.pwsweather.includeuv;
				cumulus.PWSPW = settings.pwsweather.password ?? string.Empty;
				cumulus.PWSID = settings.pwsweather.stationid ?? string.Empty;

				cumulus.SynchronisedPWSUpdate = (60 % cumulus.PWSInterval == 0);

				// WOW
				cumulus.WOWCatchUp = settings.wow.catchup;
				cumulus.WOWEnabled = settings.wow.enabled;
				cumulus.SendSRToWOW = settings.wow.includesolar;
				cumulus.SendUVToWOW = settings.wow.includeuv;
				cumulus.WOWInterval = settings.wow.interval;
				cumulus.WOWPW = settings.wow.password ?? string.Empty; ;
				cumulus.WOWID = settings.wow.stationid ?? string.Empty; ;

				cumulus.SynchronisedWOWUpdate = (60 % cumulus.WOWInterval == 0);

				// Weatherbug
				cumulus.WeatherbugCatchUp = settings.weatherbug.catchup;
				cumulus.WeatherbugEnabled = settings.weatherbug.enabled;
				cumulus.SendSRToWeatherbug = settings.weatherbug.includesolar;
				cumulus.SendUVToWeatherbug = settings.weatherbug.includeuv;
				cumulus.WeatherbugInterval = settings.weatherbug.interval;
				cumulus.WeatherbugNumber = settings.weatherbug.number ?? string.Empty; ;
				cumulus.WeatherbugPW = settings.weatherbug.password ?? string.Empty; ;
				cumulus.WeatherbugID = settings.weatherbug.publisherid ?? string.Empty; ;

				cumulus.SynchronisedWBUpdate = (60 % cumulus.WeatherbugInterval == 0);

				// CWOP
				cumulus.APRSenabled = settings.cwop.enabled;
				cumulus.APRSID = settings.cwop.id ?? string.Empty; ;
				cumulus.APRSinterval = settings.cwop.interval;
				cumulus.SendSRToAPRS = settings.cwop.includesolar;
				cumulus.APRSpass = settings.cwop.password ?? string.Empty; ;
				cumulus.APRSport = settings.cwop.port;
				cumulus.APRSserver = settings.cwop.server ?? string.Empty; ;

				cumulus.SynchronisedAPRSUpdate = (60 % cumulus.APRSinterval == 0);

				// HTTP proxy
				cumulus.HTTPProxyPassword = settings.proxies.httpproxy.password ?? string.Empty;
				cumulus.HTTPProxyPort = settings.proxies.httpproxy.port;
				cumulus.HTTPProxyName = settings.proxies.httpproxy.proxy ?? string.Empty;
				cumulus.HTTPProxyUser = settings.proxies.httpproxy.user ?? string.Empty;

				// Custom HTTP
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

				// Save the settings
				cumulus.WriteIniFile();

				cumulus.SetUpHttpProxy();
				//cumulus.SetFtpLogging(cumulus.FTPlogging);

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
								  realtimeinterval = cumulus.RealtimeInterval/1000,
								  ftpdelete = cumulus.DeleteBeforeUpload,
								  ftpinterval = cumulus.UpdateInterval,
								  ftprename = cumulus.FTPRename,
								  includestdfiles = cumulus.IncludeStandardFiles,
								  includegraphdatafiles = cumulus.IncludeGraphDataFiles,
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

			var wbsettings = new JsonInternetSettingsWeatherbug()
							 {
								 catchup = cumulus.WeatherbugCatchUp,
								 enabled = cumulus.WeatherbugEnabled,
								 includesolar = cumulus.SendSRToWeatherbug,
								 includeuv = cumulus.SendUVToWeatherbug,
								 interval = cumulus.WeatherbugInterval,
								 number = cumulus.WeatherbugNumber,
								 password = cumulus.WeatherbugPW,
								 publisherid = cumulus.WeatherbugID
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

			var httpproxy = new JsonInternetSettingsHTTPproxySettings()
							{
								password = cumulus.HTTPProxyPassword,
								port = cumulus.HTTPProxyPort,
								proxy = cumulus.HTTPProxyName,
								user = cumulus.HTTPProxyUser
							};

			var proxy = new JsonInternetSettingsProxySettings() {httpproxy = httpproxy};

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

			var customhttp = new JsonInternetSettingsCustomHttpSettings() {customseconds = customseconds, customminutes = customminutes, customrollover = customrollover};

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
						   weatherbug = wbsettings,
						   cwop = cwopsettings,
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
			string json =
				"{\"metadata\":[{\"name\":\"local\",\"label\":\"LOCAL FILENAME\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"remote\",\"label\":\"REMOTE FILENAME\",\"datatype\":\"string\",\"editable\":true},{\"name\":\"process\",\"label\":\"PROCESS\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"realtime\",\"label\":\"REALTIME\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"ftp\",\"label\":\"FTP\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"utf8\",\"label\":\"UTF8\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"binary\",\"label\":\"BINARY\",\"datatype\":\"boolean\",\"editable\":true},{\"name\":\"endofday\",\"label\":\"END OF DAY\",\"datatype\":\"boolean\",\"editable\":true}],\"data\":[";

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
				json = json + "{\"id\":" + (i + 1) + ",\"values\":[\"" + local + "\",\"" + remote + "\",\"" + process + "\",\"" + realtime + "\",\"" + ftp + "\",\"" + utf8 + "\",\"" +
					   binary + "\",\"" + endofday + "\"]}";

				if (i < numfiles - 1)
				{
					json += ",";
				}
			}

			json += "]}";
			return json;
		}

		//public string UpdateExtraWebFiles(HttpListenerContext context)
		public string UpdateExtraWebFiles(IHttpContext context)
		{
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				var pars = WebUtility.UrlDecode(data);

				NameValueCollection qscoll = HttpUtility.ParseQueryString(pars);

				var entry = Convert.ToInt32(qscoll["id"])-1;
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
						cumulus.ExtraFiles[entry].process = value=="true";
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
		public JsonInternetSettingsWeatherbug weatherbug { get; set; }
		public JsonInternetSettingsCwop cwop { get; set; }
		public JsonInternetSettingsAwekas awekas { get; set; }
		public JsonInternetSettingsWCloud weathercloud { get; set; }
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
		public string forumurl { get; set; }
		public string webcamurl { get; set; }
	}

	public class JsonInternetSettingsWebSettings
	{
		public bool autoupdate { get; set; }
		public bool includestdfiles { get; set; }
		public bool includegraphdatafiles { get; set; }
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

	public class JsonInternetSettingsWeatherbug
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool catchup { get; set; }
		public string publisherid { get; set; }
		public string number { get; set; }
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
