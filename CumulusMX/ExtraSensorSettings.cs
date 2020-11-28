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
		private readonly string extraSensorOptionsFile;
		private readonly string extraSensorSchemaFile;

		public ExtraSensorSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			extraSensorOptionsFile = cumulus.AppDir + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "ExtraSensorOptions.json";
			extraSensorSchemaFile = cumulus.AppDir + "interface" + Path.DirectorySeparatorChar + "json" + Path.DirectorySeparatorChar + "ExtraSensorSchema.json";
		}

		public string GetExtraSensorAlpacaFormData()
		{
			var indoor = new JsonExtraSensorAirLinkDevice()
			{
				enabled = cumulus.AirLinkInEnabled,
				ipAddress = cumulus.AirLinkInIPAddr,
				isNode = cumulus.AirLinkInIsNode,
				stationId = cumulus.AirLinkInStationId
			};

			var outdoor = new JsonExtraSensorAirLinkDevice()
			{
				enabled = cumulus.AirLinkOutEnabled,
				ipAddress = cumulus.AirLinkOutIPAddr,
				isNode = cumulus.AirLinkOutIsNode,
				stationId = cumulus.AirLinkOutStationId
			};

			var airlink = new JsonExtraSensorAirLinkSettings()
			{
				apiKey = cumulus.AirLinkApiKey,
				apiSecret = cumulus.AirLinkApiSecret,
				autoUpdateIp = cumulus.AirLinkAutoUpdateIpAddress,
				indoor = indoor,
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
				port1 = rg11port1,
				port2 = rg11port2
			};

			var data = new JsonExtraSensorSettings()
			{
				primaryaqsensor = cumulus.StationOptions.PrimaryAqSensor,
				aqi = cumulus.airQualityIndex,
				airLink = airlink,
				blakeLarsen = bl,
				rg11 = rg11
			};

			return data.ToJson();
		}

		public string UpdateExtraSensorConfig(IHttpContext context)
		{
			var errorMsg = "";
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = json.FromJson<JsonExtraSensorSettings>();
				// process the settings
				cumulus.LogMessage("Updating extra sensor settings");

				// General settings
				try
				{
					cumulus.StationOptions.PrimaryAqSensor = settings.primaryaqsensor;
					cumulus.airQualityIndex = settings.aqi;
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
					cumulus.AirLinkApiKey = settings.airLink.apiKey;
					cumulus.AirLinkApiSecret = settings.airLink.apiSecret;
					cumulus.AirLinkAutoUpdateIpAddress = settings.airLink.autoUpdateIp;

					cumulus.AirLinkInEnabled = settings.airLink.indoor.enabled;
					cumulus.AirLinkInIsNode = settings.airLink.indoor.isNode;
					cumulus.AirLinkInIPAddr = settings.airLink.indoor.ipAddress;
					cumulus.AirLinkInStationId = settings.airLink.indoor.stationId;
					if (string.IsNullOrEmpty(cumulus.AirLinkInStationId) && cumulus.AirLinkInIsNode)
					{
						cumulus.AirLinkInStationId = cumulus.WllStationId;
					}

					cumulus.AirLinkOutEnabled = settings.airLink.outdoor.enabled;
					cumulus.AirLinkOutIsNode = settings.airLink.outdoor.isNode;
					cumulus.AirLinkOutIPAddr = settings.airLink.outdoor.ipAddress;
					cumulus.AirLinkOutStationId = settings.airLink.outdoor.stationId;
					if (string.IsNullOrEmpty(cumulus.AirLinkOutStationId) && cumulus.AirLinkOutIsNode)
					{
						cumulus.AirLinkOutStationId = cumulus.WllStationId;
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
					cumulus.RG11Port = settings.rg11.port1.commPort;
					cumulus.RG11TBRmode = settings.rg11.port1.tipMode;
					cumulus.RG11tipsize = settings.rg11.port1.tipSize;
					cumulus.RG11IgnoreFirst = settings.rg11.port1.ignoreFirst;
					cumulus.RG11DTRmode = settings.rg11.port1.dtrMode;

					cumulus.RG11Port2 = settings.rg11.port2.commPort;
					cumulus.RG11TBRmode2 = settings.rg11.port2.tipMode;
					cumulus.RG11tipsize2 = settings.rg11.port2.tipSize;
					cumulus.RG11IgnoreFirst2 = settings.rg11.port2.ignoreFirst;
					cumulus.RG11DTRmode2 = settings.rg11.port2.dtrMode;
				}
				catch (Exception ex)
				{
					var msg = "Error processing RG-11 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			// Save the settings
			cumulus.WriteIniFile();

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}


		public string GetExtraSensorAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(extraSensorOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetExtraSensorAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(extraSensorSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}
	}

	public class JsonExtraSensorSettings
	{
		public int primaryaqsensor { get; set; }
		public int aqi { get; set; }

		public JsonExtraSensorAirLinkSettings airLink { get; set; }
		public JsonExtraSensorBlakeLarsen blakeLarsen { get; set; }
		public JsonExtraSensorRG11 rg11 { get; set; }
}

	public class JsonExtraSensorAirLinkSettings
	{
		public string apiKey { get; set; }
		public string apiSecret { get; set; }
		public bool autoUpdateIp { get; set; }
		public JsonExtraSensorAirLinkDevice indoor { get; set; }
		public JsonExtraSensorAirLinkDevice outdoor { get; set; }
	}

	public class JsonExtraSensorAirLinkDevice
	{
		public bool enabled { get; set; }
		public string ipAddress { get; set; }
		public bool isNode { get; set; }
		public string stationId { get; set; }
	}

	public class JsonExtraSensorBlakeLarsen
	{
		public bool enabled { get; set; }
	}


	public class JsonExtraSensorRG11
	{
		public JsonExtraSensorRG11device port1 { get; set; }
		public JsonExtraSensorRG11device port2 { get; set; }
	}

	public class JsonExtraSensorRG11device
	{
		public bool enabled { get; set; }
		public string commPort { get; set; }
		public bool tipMode { get; set; }
		public double tipSize { get; set; }
		public bool ignoreFirst { get; set; }
		public bool dtrMode { get; set; }
	}
}
