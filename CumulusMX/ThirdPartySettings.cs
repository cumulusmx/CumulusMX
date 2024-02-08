using System;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack;


namespace CumulusMX
{
	public class ThirdPartySettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			JsonThirdPartySettings settings;
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonThirdPartySettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing 3rdParty Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("3rdParty Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating third party settings");

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
						cumulus.Wund.PW = string.IsNullOrWhiteSpace(settings.wunderground.password) ? string.Empty : settings.wunderground.password.Trim();
						cumulus.Wund.RapidFireEnabled = settings.wunderground.rapidfire;
						cumulus.Wund.SendAverage = settings.wunderground.sendavgwind;
						cumulus.Wund.ID = string.IsNullOrWhiteSpace(settings.wunderground.stationid) ? string.Empty : settings.wunderground.stationid.Trim();
						cumulus.Wund.CatchUp = settings.wunderground.catchup;
						cumulus.Wund.SynchronisedUpdate = (!cumulus.Wund.RapidFireEnabled) && (60 % cumulus.Wund.Interval == 0);
						cumulus.Wund.SendExtraTemp1 = settings.wunderground.extratemp1;
						cumulus.Wund.SendExtraTemp2 = settings.wunderground.extratemp2;
						cumulus.Wund.SendExtraTemp3 = settings.wunderground.extratemp3;
						cumulus.Wund.SendExtraTemp4 = settings.wunderground.extratemp4;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing wunderground settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						cumulus.Windy.ApiKey = string.IsNullOrWhiteSpace(settings.windy.apikey) ? string.Empty : settings.windy.apikey.Trim();
						cumulus.Windy.StationIdx = settings.windy.stationidx;
						cumulus.Windy.CatchUp = settings.windy.catchup;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Windy settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						cumulus.AWEKAS.Lang = string.IsNullOrWhiteSpace(settings.awekas.lang) ? string.Empty : settings.awekas.lang.Trim();
						cumulus.AWEKAS.PW = string.IsNullOrWhiteSpace(settings.awekas.password) ? string.Empty : settings.awekas.password.Trim();
						cumulus.AWEKAS.ID = string.IsNullOrWhiteSpace(settings.awekas.user) ? string.Empty : settings.awekas.user.Trim();
						cumulus.AWEKAS.SendSolar = settings.awekas.includesolar;
						cumulus.AWEKAS.SendUV = settings.awekas.includeuv;
						cumulus.AWEKAS.SendSoilTemp = settings.awekas.includesoiltemp;
						cumulus.AWEKAS.SendSoilMoisture = settings.awekas.includesoilmoisture;
						cumulus.AWEKAS.SendLeafWetness = settings.awekas.includeleafwetness;
						cumulus.AWEKAS.SendIndoor = settings.awekas.includeindoor;
						cumulus.AWEKAS.SendAirQuality = settings.awekas.includeaq;
						cumulus.AWEKAS.SynchronisedUpdate = (cumulus.AWEKAS.Interval % 60 == 0);
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing AWEKAS settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// WeatherCloud
				try
				{
					cumulus.WCloud.Enabled = settings.weathercloud.enabled;
					if (cumulus.WCloud.Enabled)
					{
						cumulus.WCloud.ID = string.IsNullOrWhiteSpace(settings.weathercloud.wid) ? string.Empty : settings.weathercloud.wid.Trim();
						cumulus.WCloud.PW = string.IsNullOrWhiteSpace(settings.weathercloud.key) ? string.Empty : settings.weathercloud.key.Trim();
						cumulus.WCloud.Interval = settings.weathercloud.interval;
						cumulus.WCloud.SendSolar = settings.weathercloud.includesolar;
						cumulus.WCloud.SendUV = settings.weathercloud.includeuv;
						cumulus.WCloud.SendAirQuality = settings.weathercloud.includeaqi;
						cumulus.WCloud.SendSoilMoisture = settings.weathercloud.includesoilmoist;
						cumulus.WCloud.SoilMoistureSensor = settings.weathercloud.moistsensor;
						cumulus.WCloud.SendLeafWetness = settings.weathercloud.includeleafwet;
						cumulus.WCloud.LeafWetnessSensor = settings.weathercloud.leafwetsensor;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WeatherCloud settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						cumulus.PWS.PW = string.IsNullOrWhiteSpace(settings.pwsweather.password) ? string.Empty : settings.pwsweather.password.Trim();
						cumulus.PWS.ID = string.IsNullOrWhiteSpace(settings.pwsweather.stationid) ? string.Empty : settings.pwsweather.stationid.Trim();
						cumulus.PWS.CatchUp = settings.pwsweather.catchup;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing PWS weather settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						cumulus.WOW.SendSoilTemp = settings.wow.includesoiltemp;
						cumulus.WOW.SoilTempSensor = settings.wow.soiltempsensor;
						cumulus.WOW.Interval = settings.wow.interval;
						cumulus.WOW.PW = string.IsNullOrWhiteSpace(settings.wow.password) ? string.Empty : settings.wow.password.Trim();
						cumulus.WOW.ID = string.IsNullOrWhiteSpace(settings.wow.stationid) ? string.Empty : settings.wow.stationid.Trim();
						cumulus.WOW.CatchUp = settings.wow.catchup;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WOW settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// CWOP
				try
				{
					cumulus.APRS.Enabled = settings.cwop.enabled;
					if (cumulus.APRS.Enabled)
					{
						cumulus.APRS.ID = string.IsNullOrWhiteSpace(settings.cwop.id) ? string.Empty : settings.cwop.id.Trim(); ;
						cumulus.APRS.Interval = settings.cwop.interval;
						cumulus.APRS.SendSolar = settings.cwop.includesolar;
						cumulus.APRS.PW = string.IsNullOrWhiteSpace(settings.cwop.password) ? string.Empty : settings.cwop.password.Trim(); ;
						cumulus.APRS.Port = settings.cwop.port;
						cumulus.APRS.Server = string.IsNullOrWhiteSpace(settings.cwop.server) ? string.Empty : settings.cwop.server.Trim(); ;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing CWOP settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
						cumulus.OpenWeatherMap.PW = string.IsNullOrWhiteSpace(settings.openweathermap.apikey) ? string.Empty : settings.openweathermap.apikey.Trim();
						cumulus.OpenWeatherMap.ID = string.IsNullOrWhiteSpace(settings.openweathermap.stationid) ? string.Empty : settings.openweathermap.stationid.Trim();
						cumulus.OpenWeatherMap.Interval = settings.openweathermap.interval;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing OpenWeatherMap settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Wind Guru
				try
				{
					cumulus.WindGuru.Enabled = settings.windguru.enabled;
					if (cumulus.WindGuru.Enabled)
					{
						cumulus.WindGuru.ID = string.IsNullOrWhiteSpace(settings.windguru.uid) ? string.Empty : settings.windguru.uid.Trim();
						cumulus.WindGuru.PW = string.IsNullOrWhiteSpace(settings.windguru.password) ? string.Empty : settings.windguru.password.Trim();
						cumulus.WindGuru.SendRain = settings.windguru.includerain;
						cumulus.WindGuru.Interval = settings.windguru.interval;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing WindGuru settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Custom HTTP
				try
				{
					// custom seconds
					cumulus.CustomHttpSecondsEnabled = settings.customhttp.customseconds.enabled;
					cumulus.CustomHttpSecondsTimer.Enabled = cumulus.CustomHttpSecondsEnabled;
					if (cumulus.CustomHttpSecondsEnabled)
					{
						for (var i = 0; i < 10; i++)
						{
							if (i < settings.customhttp.customseconds.url.Length)
								cumulus.CustomHttpSecondsStrings[i] = string.IsNullOrWhiteSpace(settings.customhttp.customseconds.url[i]) ? null : settings.customhttp.customseconds.url[i].Trim();
							else
								cumulus.CustomHttpSecondsStrings[i] = null;
						}

						cumulus.CustomHttpSecondsInterval = settings.customhttp.customseconds.interval;
						cumulus.CustomHttpSecondsTimer.Interval = cumulus.CustomHttpSecondsInterval * 1000;
					}
					// custom minutes
					cumulus.CustomHttpMinutesEnabled = settings.customhttp.customminutes.enabled;
					if (cumulus.CustomHttpMinutesEnabled)
					{
						for (var i = 0; i < 10; i++)
						{
							if (i < settings.customhttp.customminutes.url.Length)
								cumulus.CustomHttpMinutesStrings[i] = string.IsNullOrWhiteSpace(settings.customhttp.customminutes.url[i]) ? null : settings.customhttp.customminutes.url[i].Trim();
							else
								cumulus.CustomHttpMinutesStrings[i] = null;
						}

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
					// custom roll-over
					cumulus.CustomHttpRolloverEnabled = settings.customhttp.customrollover.enabled;
					if (cumulus.CustomHttpRolloverEnabled)
					{
						for (var i = 0; i < 10; i++)
						{
							if (i < settings.customhttp.customrollover.url.Length)
								cumulus.CustomHttpRolloverStrings[i] = string.IsNullOrWhiteSpace(settings.customhttp.customrollover.url[i]) ? null : settings.customhttp.customrollover.url[i].Trim();
							else
								cumulus.CustomHttpRolloverStrings[i] = null;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Custom settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();

				// Do OpenWeatherMap set-up
				cumulus.EnableOpenWeatherMap();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Third Party settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Third Party data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		public string GetAlpacaFormData()
		{
			var wusettings = new JsonThirdPartySettingsWunderground()
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
				includeaq = cumulus.Wund.SendAirQuality,
				extratemp1 = cumulus.Wund.SendExtraTemp1,
				extratemp2 = cumulus.Wund.SendExtraTemp2,
				extratemp3 = cumulus.Wund.SendExtraTemp3,
				extratemp4 = cumulus.Wund.SendExtraTemp4,
			};

			var windysettings = new JsonThirdPartySettingsWindy()
			{
				catchup = cumulus.Windy.CatchUp,
				enabled = cumulus.Windy.Enabled,
				includeuv = cumulus.Windy.SendUV,
				interval = cumulus.Windy.Interval,
				apikey = cumulus.Windy.ApiKey,
				stationidx = cumulus.Windy.StationIdx
			};

			var awekassettings = new JsonThirdPartySettingsAwekas()
			{
				enabled = cumulus.AWEKAS.Enabled,
				includesolar = cumulus.AWEKAS.SendSolar,
				includesoiltemp = cumulus.AWEKAS.SendSoilTemp,
				includesoilmoisture = cumulus.AWEKAS.SendSoilMoisture,
				includeleafwetness = cumulus.AWEKAS.SendLeafWetness,
				includeindoor = cumulus.AWEKAS.SendIndoor,
				includeuv = cumulus.AWEKAS.SendUV,
				includeaq = cumulus.AWEKAS.SendAirQuality,
				interval = cumulus.AWEKAS.Interval,
				lang = cumulus.AWEKAS.Lang,
				password = cumulus.AWEKAS.PW,
				user = cumulus.AWEKAS.ID
			};

			var wcloudsettings = new JsonThirdPartySettingsWCloud()
			{
				enabled = cumulus.WCloud.Enabled,
				interval = cumulus.WCloud.Interval,
				includesolar = cumulus.WCloud.SendSolar,
				includeuv = cumulus.WCloud.SendUV,
				includeaqi = cumulus.WCloud.SendAirQuality,
				includesoilmoist = cumulus.WCloud.SendSoilMoisture,
				moistsensor = cumulus.WCloud.SoilMoistureSensor,
				includeleafwet = cumulus.WCloud.SendLeafWetness,
				leafwetsensor = cumulus.WCloud.LeafWetnessSensor,
				key = cumulus.WCloud.PW,
				wid = cumulus.WCloud.ID
			};

			var pwssettings = new JsonThirdPartySettingsPWSweather()
			{
				catchup = cumulus.PWS.CatchUp,
				enabled = cumulus.PWS.Enabled,
				interval = cumulus.PWS.Interval,
				includesolar = cumulus.PWS.SendSolar,
				includeuv = cumulus.PWS.SendUV,
				password = cumulus.PWS.PW,
				stationid = cumulus.PWS.ID
			};

			var wowsettings = new JsonThirdPartySettingsWOW()
			{
				catchup = cumulus.WOW.CatchUp,
				enabled = cumulus.WOW.Enabled,
				includesolar = cumulus.WOW.SendSolar,
				includeuv = cumulus.WOW.SendUV,
				soiltempsensor = cumulus.WOW.SoilTempSensor,
				includesoiltemp = cumulus.WOW.SendSoilTemp,
				interval = cumulus.WOW.Interval,
				password = cumulus.WOW.PW,
				stationid = cumulus.WOW.ID
			};

			var cwopsettings = new JsonThirdPartySettingsCwop()
			{
				enabled = cumulus.APRS.Enabled,
				id = cumulus.APRS.ID,
				interval = cumulus.APRS.Interval,
				includesolar = cumulus.APRS.SendSolar,
				password = cumulus.APRS.PW,
				port = cumulus.APRS.Port,
				server = cumulus.APRS.Server
			};

			var openweathermapsettings = new JsonThirdPartySettingsOpenweatherMap()
			{
				enabled = cumulus.OpenWeatherMap.Enabled,
				catchup = cumulus.OpenWeatherMap.CatchUp,
				apikey = cumulus.OpenWeatherMap.PW,
				stationid = cumulus.OpenWeatherMap.ID,
				interval = cumulus.OpenWeatherMap.Interval
			};

			var windgurusettings = new JsonThirdPartySettingsWindGuru()
			{
				enabled = cumulus.WindGuru.Enabled,
				uid = cumulus.WindGuru.ID,
				password = cumulus.WindGuru.PW,
				includerain = cumulus.WindGuru.SendRain,
				interval = cumulus.WindGuru.Interval
			};

			var customseconds = new JsonThirdPartySettingsCustomHttpSeconds()
			{
				enabled = cumulus.CustomHttpSecondsEnabled,
				interval = cumulus.CustomHttpSecondsInterval
			};

			var urlCnt = 1;
			for (int i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomHttpSecondsStrings[i]))
					urlCnt++;
			}
			customseconds.url = new string[urlCnt];

			var index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomHttpSecondsStrings[i]))
					customseconds.url[index++] = cumulus.CustomHttpSecondsStrings[i];
			}

			var customminutes = new JsonThirdPartySettingsCustomHttpMinutes()
			{
				enabled = cumulus.CustomHttpMinutesEnabled,
				intervalindex = cumulus.CustomHttpMinutesIntervalIndex
			};

			urlCnt = 1;
			for (int i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomHttpMinutesStrings[i]))
					urlCnt++;
			}

			customminutes.url = new string[urlCnt];

			index = 0;
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomHttpMinutesStrings[i]))
					customminutes.url[index++] = cumulus.CustomHttpMinutesStrings[i];
			}

			var customrollover = new JsonThirdPartySettingsCustomHttpRollover()
			{
				enabled = cumulus.CustomHttpRolloverEnabled
			};

			urlCnt = 1;
			for (int i = 1; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomHttpRolloverStrings[i]))
					urlCnt++;
			}

			customrollover.url = new string[urlCnt];

			index = 0;
			for (var i = 0; i < urlCnt; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.CustomHttpRolloverStrings[i]))
					customrollover.url[index] = cumulus.CustomHttpRolloverStrings[i];
			}

			var customhttp = new JsonThirdPartySettingsCustomHttpSettings() { customseconds = customseconds, customminutes = customminutes, customrollover = customrollover };

			var data = new JsonThirdPartySettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				wunderground = wusettings,
				windy = windysettings,
				awekas = awekassettings,
				weathercloud = wcloudsettings,
				pwsweather = pwssettings,
				wow = wowsettings,
				cwop = cwopsettings,
				openweathermap = openweathermapsettings,
				windguru = windgurusettings,
				customhttp = customhttp
			};

			return data.ToJson();
		}
	}


	public class JsonThirdPartySettings
	{
		public bool accessible { get; set; }
		public JsonThirdPartySettingsWunderground wunderground { get; set; }
		public JsonThirdPartySettingsWindy windy { get; set; }
		public JsonThirdPartySettingsPWSweather pwsweather { get; set; }
		public JsonThirdPartySettingsWOW wow { get; set; }
		public JsonThirdPartySettingsCwop cwop { get; set; }
		public JsonThirdPartySettingsAwekas awekas { get; set; }
		public JsonThirdPartySettingsWCloud weathercloud { get; set; }
		public JsonThirdPartySettingsOpenweatherMap openweathermap { get; set; }
		public JsonThirdPartySettingsWindGuru windguru { get; set; }
		public JsonThirdPartySettingsCustomHttpSettings customhttp { get; set; }
	}

	public class JsonThirdPartySettingsWunderground
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
		public int extratemp1 { get; set; }
		public int extratemp2 { get; set; }
		public int extratemp3 { get; set; }
		public int extratemp4 { get; set; }
	}

	public class JsonThirdPartySettingsWindy
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		//public bool includesolar { get; set; }
		public bool catchup { get; set; }
		public int interval { get; set; }
		public string apikey { get; set; }
		public int stationidx { get; set; }
	}

	public class JsonThirdPartySettingsAwekas
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

	public class JsonThirdPartySettingsWCloud
	{
		public bool enabled { get; set; }
		public int interval { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool includeaqi { get; set; }
		public string wid { get; set; }
		public string key { get; set; }
		public bool includesoilmoist { get; set; }
		public int moistsensor { get; set; }
		public bool includeleafwet { get; set; }
		public int leafwetsensor { get; set; }

	}

	public class JsonThirdPartySettingsPWSweather
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool catchup { get; set; }
		public string stationid { get; set; }
		public string password { get; set; }
		public int interval { get; set; }
	}

	public class JsonThirdPartySettingsWOW
	{
		public bool enabled { get; set; }
		public bool includeuv { get; set; }
		public bool includesolar { get; set; }
		public bool includesoiltemp { get; set; }
		public int soiltempsensor { get; set; }
		public bool catchup { get; set; }
		public string stationid { get; set; }
		public string password { get; set; }
		public int interval { get; set; }
	}

	public class JsonThirdPartySettingsCwop
	{
		public bool enabled { get; set; }
		public bool includesolar { get; set; }
		public string id { get; set; }
		public string password { get; set; }
		public string server { get; set; }
		public int port { get; set; }
		public int interval { get; set; }
	}

	public class JsonThirdPartySettingsWindGuru
	{
		public bool enabled { get; set; }
		public string uid { get; set; }
		public string password { get; set; }
		public bool includerain { get; set; }
		public int interval { get; set; }
	}

	public class JsonThirdPartySettingsOpenweatherMap
	{
		public bool enabled { get; set; }
		public string apikey { get; set; }
		public string stationid { get; set; }
		public int interval { get; set; }
		public bool catchup { get; set; }
	}

	public class JsonThirdPartySettingsCustomHttpSeconds
	{
		public string[] url { get; set; }
		public bool enabled { get; set; }
		public int interval { get; set; }
	}

	public class JsonThirdPartySettingsCustomHttpMinutes
	{
		public string[] url { get; set; }
		public bool enabled { get; set; }
		public int intervalindex { get; set; }
	}

	public class JsonThirdPartySettingsCustomHttpRollover
	{
		public string[] url { get; set; }
		public bool enabled { get; set; }
	}

	public class JsonThirdPartySettingsCustomHttpSettings
	{
		public JsonThirdPartySettingsCustomHttpSeconds customseconds { get; set; }
		public JsonThirdPartySettingsCustomHttpMinutes customminutes { get; set; }
		public JsonThirdPartySettingsCustomHttpRollover customrollover { get; set; }
	}
}
