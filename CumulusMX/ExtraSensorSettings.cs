using System;
using System.IO;
using System.Net;
using ServiceStack;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class ExtraSensorSettings
	{
		private readonly Cumulus cumulus;

		public ExtraSensorSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlpacaFormData()
		{
			var indoor = new JsonExtraSensorAirLinkDevice()
			{
				ipAddress = cumulus.AirLinkInIPAddr,
				hostname = cumulus.AirLinkInHostName,
				stationId = cumulus.AirLinkInStationId
			};

			var outdoor = new JsonExtraSensorAirLinkDevice()
			{
				ipAddress = cumulus.AirLinkOutIPAddr,
				hostname = cumulus.AirLinkOutHostName,
				stationId = cumulus.AirLinkOutStationId
			};

			var airlink = new JsonExtraSensorAirLinkSettings()
			{
				isNode = cumulus.AirLinkIsNode,
				apiKey = cumulus.AirLinkApiKey,
				apiSecret = cumulus.AirLinkApiSecret,
				autoUpdateIp = cumulus.AirLinkAutoUpdateIpAddress,
				indoorenabled = cumulus.AirLinkInEnabled,
				indoor = indoor,
				outdoorenabled = cumulus.AirLinkOutEnabled,
				outdoor = outdoor
			};

			var bl = new JsonExtraSensorBlakeLarsen()
			{
				enabled = cumulus.UseBlakeLarsen
			};

			var rg11port1 = new JsonExtraSensorRG11device()
			{
				commPort = cumulus.RG11Port,
				tipMode = cumulus.RG11TBRmode,
				tipSize = cumulus.RG11tipsize,
				dtrMode = cumulus.RG11TBRmode
			};

			var rg11port2 = new JsonExtraSensorRG11device()
			{
				commPort = cumulus.RG11Port2,
				tipMode = cumulus.RG11TBRmode2,
				tipSize = cumulus.RG11tipsize2,
				dtrMode = cumulus.RG11TBRmode2
			};

			var rg11 = new JsonExtraSensorRG11()
			{
				dev1enabled = cumulus.RG11Enabled,
				port1 = rg11port1,
				dev2enabled = cumulus.RG11Enabled2,
				port2 = rg11port2
			};

			var aq = new JsonExtraSensorAirQuality()
			{
				primaryaqsensor = cumulus.StationOptions.PrimaryAqSensor,
				aqi = cumulus.airQualityIndex,
			};

			var data = new JsonExtraSensorSettings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				airquality = aq,
				airLink = airlink,
				blakeLarsen = bl,
				rg11 = rg11
			};

			return data.ToJson();
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			JsonExtraSensorSettings settings;
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonExtraSensorSettings>();
			}
			catch (Exception ex)
			{
				var msg = "Error deserializing ExtraSensor Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("ExtraSensor Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating extra sensor settings");

				// General settings
				try
				{
					cumulus.StationOptions.PrimaryAqSensor = settings.airquality.primaryaqsensor;
					cumulus.airQualityIndex = settings.airquality.aqi;
				}
				catch (Exception ex)
				{
					var msg = "Error processing General settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// AirLink settings
				try
				{
					cumulus.AirLinkIsNode = settings.airLink.isNode;
					cumulus.AirLinkApiKey = settings.airLink.apiKey;
					cumulus.AirLinkApiSecret = settings.airLink.apiSecret;
					cumulus.AirLinkAutoUpdateIpAddress = settings.airLink.autoUpdateIp;

					cumulus.AirLinkInEnabled = settings.airLink.indoorenabled;
					if (cumulus.AirLinkInEnabled && settings.airLink.indoor != null)
					{
						cumulus.AirLinkInIPAddr = settings.airLink.indoor.ipAddress;
						cumulus.AirLinkInHostName = settings.airLink.indoor.hostname;
						cumulus.AirLinkInStationId = settings.airLink.indoor.stationId;
						if (cumulus.AirLinkInStationId < 10 && cumulus.AirLinkIsNode)
						{
							cumulus.AirLinkInStationId = cumulus.WllStationId;
						}
					}
					cumulus.AirLinkOutEnabled = settings.airLink.outdoorenabled;
					if (cumulus.AirLinkOutEnabled && settings.airLink.outdoor != null)
					{
						cumulus.AirLinkOutIPAddr = settings.airLink.outdoor.ipAddress;
						cumulus.AirLinkOutHostName = settings.airLink.outdoor.hostname;
						cumulus.AirLinkOutStationId = settings.airLink.outdoor.stationId;
						if (cumulus.AirLinkOutStationId < 10 && cumulus.AirLinkIsNode)
						{
							cumulus.AirLinkOutStationId = cumulus.WllStationId;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing AirLink settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Blake-Larsen settings
				try
				{
					cumulus.UseBlakeLarsen = settings.blakeLarsen.enabled;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Blake-Larsen settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// RG-11 settings
				try
				{
					cumulus.RG11Enabled = settings.rg11.dev1enabled;
					if (cumulus.RG11Enabled && settings.rg11.port1 != null)
					{
						cumulus.RG11Port = settings.rg11.port1.commPort;
						cumulus.RG11TBRmode = settings.rg11.port1.tipMode;
						cumulus.RG11tipsize = settings.rg11.port1.tipSize;
						cumulus.RG11IgnoreFirst = settings.rg11.port1.ignoreFirst;
						cumulus.RG11DTRmode = settings.rg11.port1.dtrMode;
					}

					cumulus.RG11Enabled2 = settings.rg11.dev2enabled;
					if (cumulus.RG11Enabled2 && settings.rg11.port2 != null)
					{
						cumulus.RG11Port2 = settings.rg11.port2.commPort;
						cumulus.RG11TBRmode2 = settings.rg11.port2.tipMode;
						cumulus.RG11tipsize2 = settings.rg11.port2.tipSize;
						cumulus.RG11IgnoreFirst2 = settings.rg11.port2.ignoreFirst;
						cumulus.RG11DTRmode2 = settings.rg11.port2.dtrMode;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing RG-11 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Extra Sensor settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Extra Sensor Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}
	}

	public class JsonExtraSensorSettings
	{
		public bool accessible { get; set; }
		public JsonExtraSensorAirQuality airquality { get; set; }
		public JsonExtraSensorAirLinkSettings airLink { get; set; }
		public JsonExtraSensorBlakeLarsen blakeLarsen { get; set; }
		public JsonExtraSensorRG11 rg11 { get; set; }
	}

	public class JsonExtraSensorAirQuality
	{
		public int primaryaqsensor { get; set; }
		public int aqi { get; set; }
	}

	public class JsonExtraSensorAirLinkSettings
	{
		public bool isNode { get; set; }
		public string apiKey { get; set; }
		public string apiSecret { get; set; }
		public bool autoUpdateIp { get; set; }
		public bool indoorenabled { get; set; }
		public JsonExtraSensorAirLinkDevice indoor { get; set; }
		public bool outdoorenabled { get; set; }
		public JsonExtraSensorAirLinkDevice outdoor { get; set; }
	}

	public class JsonExtraSensorAirLinkDevice
	{
		public string ipAddress { get; set; }
		public string hostname { get; set; }
		public int stationId { get; set; }
	}

	public class JsonExtraSensorBlakeLarsen
	{
		public bool enabled { get; set; }
	}

	public class JsonExtraSensorRG11
	{
		public bool dev1enabled { get; set; }
		public JsonExtraSensorRG11device port1 { get; set; }
		public bool dev2enabled { get; set; }
		public JsonExtraSensorRG11device port2 { get; set; }
	}

	public class JsonExtraSensorRG11device
	{
		public string commPort { get; set; }
		public bool tipMode { get; set; }
		public double tipSize { get; set; }
		public bool ignoreFirst { get; set; }
		public bool dtrMode { get; set; }
	}
}