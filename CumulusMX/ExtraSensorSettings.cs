using System;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack;

namespace CumulusMX
{
	public class ExtraSensorSettings
	{
		private readonly Cumulus cumulus;
		private WeatherStation station;

		public ExtraSensorSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		internal void SetStation(WeatherStation station)
		{
			this.station = station;
		}

		public string GetAlpacaFormData()
		{
			var indoor = new JsonExtraSensorAirLinkDevice()
			{
				enabled = cumulus.AirLinkInEnabled,
				ipAddress = cumulus.AirLinkInIPAddr,
				hostname = cumulus.AirLinkInHostName,
				stationId = cumulus.AirLinkInStationId
			};

			var outdoor = new JsonExtraSensorAirLinkDevice()
			{
				enabled = cumulus.AirLinkOutEnabled,
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
				indoor = indoor,
				outdoor = outdoor
			};

			var ecowittwn34map = new JsonStationSettingsEcowittMappings()
			{
				primaryTHsensor = cumulus.Gw1000PrimaryTHSensor,

				wn34chan1 = cumulus.EcowittMapWN34[1],
				wn34chan2 = cumulus.EcowittMapWN34[2],
				wn34chan3 = cumulus.EcowittMapWN34[3],
				wn34chan4 = cumulus.EcowittMapWN34[4],
				wn34chan5 = cumulus.EcowittMapWN34[5],
				wn34chan6 = cumulus.EcowittMapWN34[6],
				wn34chan7 = cumulus.EcowittMapWN34[7],
				wn34chan8 = cumulus.EcowittMapWN34[8]
			};

			var ecowitt = new JsonExtraSensorEcowitt()
			{
				useSolar = cumulus.EcowittExtraUseSolar,
				useUv = cumulus.EcowittExtraUseUv,
				useTempHum = cumulus.EcowittExtraUseTempHum,
				//useSoilTemp = cumulus.EcowittExtraUseSoilTemp,
				useSoilMoist = cumulus.EcowittExtraUseSoilMoist,
				useLeafWet = cumulus.EcowittExtraUseLeafWet,
				useUserTemp = cumulus.EcowittExtraUseUserTemp,
				useAQI = cumulus.EcowittExtraUseAQI,
				useCo2 = cumulus.EcowittExtraUseCo2,
				useLightning = cumulus.EcowittExtraUseLightning,
				useLeak = cumulus.EcowittExtraUseLeak,

				setcustom = cumulus.EcowittExtraSetCustomServer,
				gwaddr = cumulus.EcowittExtraGatewayAddr,
				localaddr = cumulus.EcowittExtraLocalAddr,
				interval = cumulus.EcowittExtraCustomInterval,

				mappings = ecowittwn34map
			};

			var ambient = new JsonExtraSensorAmbient()
			{
				useSolar = cumulus.AmbientExtraUseSolar,
				useUv = cumulus.AmbientExtraUseUv,
				useTempHum = cumulus.AmbientExtraUseTempHum,
				//useSoilTemp = cumulus.AmbientExtraUseSoilTemp,
				useSoilMoist = cumulus.AmbientExtraUseSoilMoist,
				//useLeafWet = cumulus.AmbientExtraUseLeafWet,
				useAQI = cumulus.AmbientExtraUseAQI,
				useCo2 = cumulus.AmbientExtraUseCo2,
				useLightning = cumulus.AmbientExtraUseLightning,
				useLeak = cumulus.AmbientExtraUseLeak
			};

			var httpStation = new JsonExtraSensorHttp()
			{
				ecowitt = ecowitt,
				ambient = ambient
			};

			if (cumulus.EcowittExtraEnabled)
				httpStation.extraStation = 0;
			else if (cumulus.AmbientExtraEnabled)
				httpStation.extraStation = 1;
			else
				httpStation.extraStation = -1;


			var bl = new JsonExtraSensorBlakeLarsen()
			{
				enabled = cumulus.SolarOptions.UseBlakeLarsen
			};

			var rg11port1 = new JsonExtraSensorRG11device()
			{
				enabled = cumulus.RG11Enabled,
				commPort = cumulus.RG11Port,
				tipMode = cumulus.RG11TBRmode,
				tipSize = cumulus.RG11tipsize,
				dtrMode = cumulus.RG11TBRmode
			};

			var rg11port2 = new JsonExtraSensorRG11device()
			{
				enabled = cumulus.RG11Enabled2,
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
				httpSensors = httpStation,
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
				var msg = "Error de-serializing ExtraSensor Settings JSON: " + ex.Message;
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
					cumulus.AirLinkApiKey = (settings.airLink.apiKey ?? string.Empty).Trim();
					cumulus.AirLinkApiSecret = (settings.airLink.apiSecret ?? string.Empty).Trim();
					cumulus.AirLinkAutoUpdateIpAddress = settings.airLink.autoUpdateIp;

					cumulus.AirLinkInEnabled = settings.airLink.indoor.enabled;
					if (cumulus.AirLinkInEnabled)
					{
						cumulus.AirLinkInIPAddr = (settings.airLink.indoor.ipAddress ?? string.Empty).Trim();
						cumulus.AirLinkInHostName = (settings.airLink.indoor.hostname ?? string.Empty).Trim();
						cumulus.AirLinkInStationId = settings.airLink.indoor.stationId;
						if (cumulus.AirLinkInStationId < 10 && cumulus.AirLinkIsNode)
						{
							cumulus.AirLinkInStationId = cumulus.WllStationId;
						}
					}
					cumulus.AirLinkOutEnabled = settings.airLink.outdoor.enabled;
					if (cumulus.AirLinkOutEnabled)
					{
						cumulus.AirLinkOutIPAddr = (settings.airLink.outdoor.ipAddress ?? string.Empty).Trim();
						cumulus.AirLinkOutHostName = (settings.airLink.outdoor.hostname ?? string.Empty).Trim();
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

				// Ecowitt Extra settings
				try
				{
					if (settings.httpSensors.extraStation == 0)
					{
						cumulus.EcowittExtraEnabled = true;
						cumulus.EcowittExtraUseSolar = settings.httpSensors.ecowitt.useSolar;
						cumulus.EcowittExtraUseUv = settings.httpSensors.ecowitt.useUv;
						cumulus.EcowittExtraUseTempHum = settings.httpSensors.ecowitt.useTempHum;
						//cumulus.EcowittExtraUseSoilTemp = settings.httpSensors.ecowitt.useSoilTemp;
						cumulus.EcowittExtraUseSoilMoist = settings.httpSensors.ecowitt.useSoilMoist;
						cumulus.EcowittExtraUseLeafWet = settings.httpSensors.ecowitt.useLeafWet;
						cumulus.EcowittExtraUseUserTemp = settings.httpSensors.ecowitt.useUserTemp;
						cumulus.EcowittExtraUseAQI = settings.httpSensors.ecowitt.useAQI;
						cumulus.EcowittExtraUseCo2 = settings.httpSensors.ecowitt.useCo2;
						cumulus.EcowittExtraUseLightning = settings.httpSensors.ecowitt.useLightning;
						cumulus.EcowittExtraUseLeak = settings.httpSensors.ecowitt.useLeak;

						cumulus.EcowittExtraSetCustomServer = settings.httpSensors.ecowitt.setcustom;
						cumulus.EcowittExtraGatewayAddr = settings.httpSensors.ecowitt.gwaddr;
						cumulus.EcowittExtraLocalAddr = settings.httpSensors.ecowitt.localaddr;
						cumulus.EcowittExtraCustomInterval = settings.httpSensors.ecowitt.interval;

						cumulus.Gw1000PrimaryTHSensor = settings.httpSensors.ecowitt.mappings.primaryTHsensor;

						if (cumulus.EcowittMapWN34[1] != settings.httpSensors.ecowitt.mappings.wn34chan1)
						{
							if (cumulus.EcowittMapWN34[1] == 0)
								station.UserTemp[1] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[1]] = 0;

							cumulus.EcowittMapWN34[1] = settings.httpSensors.ecowitt.mappings.wn34chan1;
						}

						if (cumulus.EcowittMapWN34[2] != settings.httpSensors.ecowitt.mappings.wn34chan2)
						{
							if (cumulus.EcowittMapWN34[2] == 0)
								station.UserTemp[2] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[2]] = 0;

							cumulus.EcowittMapWN34[2] = settings.httpSensors.ecowitt.mappings.wn34chan2;
						}

						if (cumulus.EcowittMapWN34[3] != settings.httpSensors.ecowitt.mappings.wn34chan3)
						{
							if (cumulus.EcowittMapWN34[3] == 0)
								station.UserTemp[3] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[3]] = 0;

							cumulus.EcowittMapWN34[3] = settings.httpSensors.ecowitt.mappings.wn34chan3;
						}

						if (cumulus.EcowittMapWN34[4] != settings.httpSensors.ecowitt.mappings.wn34chan4)
						{
							if (cumulus.EcowittMapWN34[4] == 0)
								station.UserTemp[4] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[4]] = 0;

							cumulus.EcowittMapWN34[4] = settings.httpSensors.ecowitt.mappings.wn34chan4;
						}

						if (cumulus.EcowittMapWN34[5] != settings.httpSensors.ecowitt.mappings.wn34chan5)
						{
							if (cumulus.EcowittMapWN34[5] == 0)
								station.UserTemp[5] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[5]] = 0;

							cumulus.EcowittMapWN34[5] = settings.httpSensors.ecowitt.mappings.wn34chan5;
						}

						if (cumulus.EcowittMapWN34[6] != settings.httpSensors.ecowitt.mappings.wn34chan6)
						{
							if (cumulus.EcowittMapWN34[6] == 0)
								station.UserTemp[6] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[6]] = 0;

							cumulus.EcowittMapWN34[6] = settings.httpSensors.ecowitt.mappings.wn34chan6;
						}

						if (cumulus.EcowittMapWN34[7] != settings.httpSensors.ecowitt.mappings.wn34chan7)
						{
							if (cumulus.EcowittMapWN34[7] == 0)
								station.UserTemp[7] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[7]] = 0;

							cumulus.EcowittMapWN34[7] = settings.httpSensors.ecowitt.mappings.wn34chan7;
						}

						if (cumulus.EcowittMapWN34[8] != settings.httpSensors.ecowitt.mappings.wn34chan8)
						{
							if (cumulus.EcowittMapWN34[8] == 0)
								station.UserTemp[8] = 0;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[8]] = 0;

							cumulus.EcowittMapWN34[8] = settings.httpSensors.ecowitt.mappings.wn34chan8;
						}

						// Also enable extra logging if applicable
						if (cumulus.EcowittExtraUseTempHum || cumulus.EcowittExtraUseSoilTemp || cumulus.EcowittExtraUseSoilMoist || cumulus.EcowittExtraUseLeafWet || cumulus.EcowittExtraUseUserTemp || cumulus.EcowittExtraUseAQI || cumulus.EcowittExtraUseCo2)
						{
							cumulus.StationOptions.LogExtraSensors = true;
						}
					}
					else
						cumulus.EcowittExtraEnabled = false;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ambient Extra settings
				try
				{
					if (settings.httpSensors.extraStation == 1)
					{
						cumulus.AmbientExtraEnabled = true;
						cumulus.AmbientExtraUseSolar = settings.httpSensors.ambient.useSolar;
						cumulus.AmbientExtraUseUv = settings.httpSensors.ambient.useUv;
						cumulus.AmbientExtraUseTempHum = settings.httpSensors.ambient.useTempHum;
						//cumulus.AmbientExtraUseSoilTemp = settings.httpSensors.ambient.useSoilTemp;
						cumulus.AmbientExtraUseSoilMoist = settings.httpSensors.ambient.useSoilMoist;
						//cumulus.AmbientExtraUseLeafWet = settings.httpSensors.ambient.useLeafWet;
						cumulus.AmbientExtraUseAQI = settings.httpSensors.ambient.useAQI;
						cumulus.AmbientExtraUseCo2 = settings.httpSensors.ambient.useCo2;
						cumulus.AmbientExtraUseLightning = settings.httpSensors.ambient.useLightning;
						cumulus.AmbientExtraUseLeak = settings.httpSensors.ambient.useLeak;

						// Also enable extra logging if applicable
						if (cumulus.AmbientExtraUseTempHum || cumulus.AmbientExtraUseSoilTemp || cumulus.AmbientExtraUseSoilMoist || cumulus.AmbientExtraUseAQI || cumulus.AmbientExtraUseCo2)
						{
							cumulus.StationOptions.LogExtraSensors = true;
						}
					}
					else
						cumulus.AmbientExtraEnabled = false;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ambient settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Blake-Larsen settings
				try
				{
					cumulus.SolarOptions.UseBlakeLarsen = settings.blakeLarsen.enabled;
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
					cumulus.RG11Enabled = settings.rg11.port1.enabled;
					if (cumulus.RG11Enabled)
					{
						cumulus.RG11Port = (settings.rg11.port1.commPort ?? string.Empty).Trim();
						cumulus.RG11TBRmode = settings.rg11.port1.tipMode;
						cumulus.RG11tipsize = settings.rg11.port1.tipSize;
						cumulus.RG11IgnoreFirst = settings.rg11.port1.ignoreFirst;
						cumulus.RG11DTRmode = settings.rg11.port1.dtrMode;
					}

					cumulus.RG11Enabled2 = settings.rg11.port2.enabled;
					if (cumulus.RG11Enabled2)
					{
						cumulus.RG11Port2 = (settings.rg11.port2.commPort ?? string.Empty).Trim();
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
		public JsonExtraSensorHttp httpSensors { get; set; }
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
		public JsonExtraSensorAirLinkDevice indoor { get; set; }
		public JsonExtraSensorAirLinkDevice outdoor { get; set; }
	}

	public class JsonExtraSensorAirLinkDevice
	{
		public bool enabled { get; set; }
		public string ipAddress { get; set; }
		public string hostname { get; set; }
		public int stationId { get; set; }
	}

	public class JsonExtraSensorHttp
	{
		public int extraStation { get; set; }
		public JsonExtraSensorEcowitt ecowitt { get; set; }
		public JsonExtraSensorAmbient ambient { get; set; }
	}

	public class JsonExtraSensorAmbient
	{
		public bool useSolar { get; set; }
		public bool useUv { get; set; }
		public bool useTempHum { get; set; }
		//public bool useSoilTemp { get; set; }
		public bool useSoilMoist { get; set; }
		public bool useLeafWet { get; set; }
		public bool useUserTemp { get; set; }
		public bool useAQI { get; set; }
		public bool useCo2 { get; set; }
		public bool useLightning { get; set; }
		public bool useLeak { get; set; }
	}

	public class JsonExtraSensorEcowitt : JsonExtraSensorAmbient
	{
		public bool setcustom { get; set; }
		public string gwaddr { get; set; }
		public string localaddr { get; set; }
		public int interval { get; set; }
		public JsonStationSettingsEcowittMappings mappings { get; set; }
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
