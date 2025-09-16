using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;

using EmbedIO;

using static CumulusMX.Settings.StationSettings;

namespace CumulusMX.Settings
{
	public class ExtraSensorSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;
		private WeatherStation station;

		internal void SetStation(WeatherStation station)
		{
			this.station = station;
		}

		public string GetAlpacaFormData()
		{
			var indoor = new JsonAirLinkDevice
			{
				enabled = cumulus.AirLinkInEnabled,
				ipAddress = cumulus.AirLinkInIPAddr,
				hostname = cumulus.AirLinkInHostName,
				stationId = cumulus.AirLinkInStationId
			};

			var outdoor = new JsonAirLinkDevice
			{
				enabled = cumulus.AirLinkOutEnabled,
				ipAddress = cumulus.AirLinkOutIPAddr,
				hostname = cumulus.AirLinkOutHostName,
				stationId = cumulus.AirLinkOutStationId
			};

			var airlink = new JsonAirLinkSettings
			{
				isNode = cumulus.AirLinkIsNode,
				apiKey = cumulus.AirLinkApiKey,
				apiSecret = cumulus.AirLinkApiSecret,
				autoUpdateIp = cumulus.AirLinkAutoUpdateIpAddress,
				indoor = indoor,
				outdoor = outdoor
			};

			var ecowittwn34map = new JsonEcowittMappings
			{
				primaryTHsensor = cumulus.Gw1000PrimaryTHSensor,
				primaryIndoorTHsensor = cumulus.Gw1000PrimaryIndoorTHSensor,

				wn34chan1 = cumulus.EcowittMapWN34[1],
				wn34chan2 = cumulus.EcowittMapWN34[2],
				wn34chan3 = cumulus.EcowittMapWN34[3],
				wn34chan4 = cumulus.EcowittMapWN34[4],
				wn34chan5 = cumulus.EcowittMapWN34[5],
				wn34chan6 = cumulus.EcowittMapWN34[6],
				wn34chan7 = cumulus.EcowittMapWN34[7],
				wn34chan8 = cumulus.EcowittMapWN34[8]
			};

			var ecowitt = new JsonEcowitt
			{
				useSolar = cumulus.ExtraSensorUseSolar,
				useUv = cumulus.ExtraSensorUseUv,
				useTempHum = cumulus.ExtraSensorUseTempHum,
				//useSoilTemp = cumulus.EcowittExtraUseSoilTemp,
				useSoilMoist = cumulus.ExtraSensorUseSoilMoist,
				useLeafWet = cumulus.ExtraSensorUseLeafWet,
				useUserTemp = cumulus.ExtraSensorUseUserTemp,
				useAQI = cumulus.ExtraSensorUseAQI,
				useCo2 = cumulus.ExtraSensorUseCo2,
				useLightning = cumulus.ExtraSensorUseLightning,
				useLeak = cumulus.ExtraSensorUseLeak,
				useCamera = cumulus.ExtraSensorUseCamera,
				useLaserDist = cumulus.ExtraSensorUseLaserDist,

				setcustom = cumulus.EcowittExtraSetCustomServer,
				gwaddr = cumulus.EcowittExtraGatewayAddr,
				localaddr = cumulus.EcowittExtraLocalAddr,
				interval = cumulus.EcowittExtraCustomInterval,

				mappings = ecowittwn34map,
				forwarders = new JsonForwarders
				{
					usemain = cumulus.EcowittExtraUseMainForwarders
				}
			};

			ecowitt.forwarders.forward = [];
			for (var i = 0; i < 10; i++)
			{
				if (!string.IsNullOrEmpty(cumulus.EcowittExtraForwarders[i]))
				{
					ecowitt.forwarders.forward.Add(new JsonEcowittForwardList() { url = cumulus.EcowittExtraForwarders[i] });
				}
			}

			var ecowittapi = new JsonEcowittApi
			{
				applicationkey = cumulus.EcowittApplicationKey,
				userkey = cumulus.EcowittUserApiKey,
				mac = cumulus.EcowittMacAddress
			};

			var ambient = new JsonAmbient
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

			var jsonstnadv = new JsonJsonStationAdvanced()
			{
				filedelay = cumulus.JsonExtraStationOptions.FileReadDelay,
				fileignore = cumulus.JsonExtraStationOptions.FileIgnoreTime,
				mqtttls = cumulus.JsonExtraStationOptions.MqttUseTls
			};

			var json = new JsonJson
			{
				conntype = cumulus.JsonExtraStationOptions.Connectiontype,
				filename = cumulus.JsonExtraStationOptions.SourceFile,
				mqttserver = cumulus.JsonExtraStationOptions.MqttServer,
				mqttport = cumulus.JsonExtraStationOptions.MqttPort,
				mqttuser = cumulus.JsonExtraStationOptions.MqttUsername,
				mqttpass = cumulus.JsonExtraStationOptions.MqttPassword,
				mqtttopic = cumulus.JsonExtraStationOptions.MqttTopic,
				advanced = jsonstnadv,

				useSolar = cumulus.ExtraSensorUseSolar,
				useUv = cumulus.ExtraSensorUseUv,
				useTempHum = cumulus.ExtraSensorUseTempHum,
				useSoilMoist = cumulus.ExtraSensorUseSoilMoist,
				useLeafWet = cumulus.ExtraSensorUseLeafWet,
				useUserTemp = cumulus.ExtraSensorUseUserTemp,
				useAQI = cumulus.ExtraSensorUseAQI,
				useCo2 = cumulus.ExtraSensorUseCo2,
				useLaserDist = cumulus.ExtraSensorUseLaserDist
			};

			var httpStation = new JsonHttp
			{
				ecowitt = ecowitt,
				ecowittapi = ecowittapi,
				ambient = ambient,
				jsonstation = json
			};

			if (cumulus.EcowittExtraEnabled)
				httpStation.extraStation = 0;
			else if (cumulus.AmbientExtraEnabled)
				httpStation.extraStation = 1;
			else if (cumulus.EcowittCloudExtraEnabled)
				httpStation.extraStation = 2;
			else if (cumulus.JsonExtraStationOptions.ExtraSensorsEnabled)
				httpStation.extraStation = 3;
			else
				httpStation.extraStation = -1;


			var bl = new JsonBlakeLarsen
			{
				enabled = cumulus.SolarOptions.UseBlakeLarsen
			};

			var rg11port1 = new JsonRg11Device(cumulus.RG11Enabled, cumulus.RG11Port, cumulus.RG11TBRmode, cumulus.RG11tipsize, default, cumulus.RG11TBRmode);

			var rg11port2 = new JsonRg11Device(cumulus.RG11Enabled2, cumulus.RG11Port2, cumulus.RG11TBRmode2, cumulus.RG11tipsize2, default, cumulus.RG11TBRmode2);

			var rg11 = new JsonRG11
			{
				port1 = rg11port1,
				port2 = rg11port2
			};

			var aq = new JsonAirQuality
			{
				primaryaqsensor = cumulus.StationOptions.PrimaryAqSensor,
				aqi = cumulus.airQualityIndex,
			};

			var laser = new JsonLaser
			{
				sensor1 = new JsonLaserDevice
				{
					depth = cumulus.LaserDepthBaseline[1]
				},
				sensor2 = new JsonLaserDevice
				{
					depth = cumulus.LaserDepthBaseline[2]
				},
				sensor3 = new JsonLaserDevice
				{
					depth = cumulus.LaserDepthBaseline[3]
				},
				sensor4 = new JsonLaserDevice
				{
					depth = cumulus.LaserDepthBaseline[4]
				}
			};


			var pa = new JsonPurpleAir
			{
				enabled = cumulus.PurpleAirEnabled,
			};

			pa.sensors = new JsonPurpleAirSensor[4];

			for (var i = 0; i < 4; i++)
			{
				pa.sensors[i] = new JsonPurpleAirSensor
				{
					id = "Sensor " + (i+1),
					ipAddress = cumulus.PurpleAirIpAddress[i],
					algorithm = cumulus.PurpleAirAlgorithm[i],
					thsensor = cumulus.PurpleAirThSensor[i],
				};
			}

			var data = new JsonSettings
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				airquality = aq,
				airLink = airlink,
				httpSensors = httpStation,
				blakeLarsen = bl,
				laser = laser,
				rg11 = rg11,
				purpleAir = pa
			};

			return JsonSerializer.Serialize(data);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = string.Empty;
			var json = string.Empty;
			JsonSettings settings;
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<JsonSettings>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing ExtraSensor Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ecowitt Extra settings
				try
				{
					cumulus.EcowittExtraEnabled = settings.httpSensors.extraStation == 0;
					cumulus.EcowittCloudExtraEnabled = settings.httpSensors.extraStation == 2;

					if (cumulus.EcowittExtraEnabled || cumulus.EcowittCloudExtraEnabled)
					{
						cumulus.ExtraSensorUseSolar = settings.httpSensors.ecowitt.useSolar;
						cumulus.ExtraSensorUseUv = settings.httpSensors.ecowitt.useUv;
						cumulus.ExtraSensorUseTempHum = settings.httpSensors.ecowitt.useTempHum;
						//cumulus.EcowittExtraUseSoilTemp = settings.httpSensors.ecowitt.useSoilTemp
						cumulus.ExtraSensorUseSoilMoist = settings.httpSensors.ecowitt.useSoilMoist;
						cumulus.ExtraSensorUseLeafWet = settings.httpSensors.ecowitt.useLeafWet;
						cumulus.ExtraSensorUseUserTemp = settings.httpSensors.ecowitt.useUserTemp;
						cumulus.ExtraSensorUseAQI = settings.httpSensors.ecowitt.useAQI;
						cumulus.ExtraSensorUseCo2 = settings.httpSensors.ecowitt.useCo2;
						cumulus.ExtraSensorUseLightning = settings.httpSensors.ecowitt.useLightning;
						cumulus.ExtraSensorUseLeak = settings.httpSensors.ecowitt.useLeak;
						cumulus.ExtraSensorUseLaserDist = settings.httpSensors.ecowitt.useLaserDist;
						cumulus.ExtraSensorUseCamera = settings.httpSensors.ecowitt.useCamera;

						cumulus.EcowittExtraSetCustomServer = settings.httpSensors.ecowitt.setcustom;
						if (cumulus.EcowittExtraSetCustomServer)
						{
							cumulus.EcowittExtraGatewayAddr = settings.httpSensors.ecowitt.gwaddr;
							cumulus.EcowittExtraLocalAddr = settings.httpSensors.ecowitt.localaddr;
							cumulus.EcowittExtraCustomInterval = settings.httpSensors.ecowitt.interval;
						}

						cumulus.Gw1000PrimaryTHSensor = settings.httpSensors.ecowitt.mappings.primaryTHsensor;
						cumulus.Gw1000PrimaryIndoorTHSensor = settings.httpSensors.ecowitt.mappings.primaryIndoorTHsensor;

						if (cumulus.EcowittMapWN34[1] != settings.httpSensors.ecowitt.mappings.wn34chan1)
						{
							if (cumulus.EcowittMapWN34[1] == 0)
								station.UserTemp[1] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[1]] = null;

							cumulus.EcowittMapWN34[1] = settings.httpSensors.ecowitt.mappings.wn34chan1;
						}

						if (cumulus.EcowittMapWN34[2] != settings.httpSensors.ecowitt.mappings.wn34chan2)
						{
							if (cumulus.EcowittMapWN34[2] == 0)
								station.UserTemp[2] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[2]] = null;

							cumulus.EcowittMapWN34[2] = settings.httpSensors.ecowitt.mappings.wn34chan2;
						}

						if (cumulus.EcowittMapWN34[3] != settings.httpSensors.ecowitt.mappings.wn34chan3)
						{
							if (cumulus.EcowittMapWN34[3] == 0)
								station.UserTemp[3] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[3]] = null;

							cumulus.EcowittMapWN34[3] = settings.httpSensors.ecowitt.mappings.wn34chan3;
						}

						if (cumulus.EcowittMapWN34[4] != settings.httpSensors.ecowitt.mappings.wn34chan4)
						{
							if (cumulus.EcowittMapWN34[4] == 0)
								station.UserTemp[4] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[4]] = null;

							cumulus.EcowittMapWN34[4] = settings.httpSensors.ecowitt.mappings.wn34chan4;
						}

						if (cumulus.EcowittMapWN34[5] != settings.httpSensors.ecowitt.mappings.wn34chan5)
						{
							if (cumulus.EcowittMapWN34[5] == 0)
								station.UserTemp[5] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[5]] = null;

							cumulus.EcowittMapWN34[5] = settings.httpSensors.ecowitt.mappings.wn34chan5;
						}

						if (cumulus.EcowittMapWN34[6] != settings.httpSensors.ecowitt.mappings.wn34chan6)
						{
							if (cumulus.EcowittMapWN34[6] == 0)
								station.UserTemp[6] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[6]] = null;

							cumulus.EcowittMapWN34[6] = settings.httpSensors.ecowitt.mappings.wn34chan6;
						}

						if (cumulus.EcowittMapWN34[7] != settings.httpSensors.ecowitt.mappings.wn34chan7)
						{
							if (cumulus.EcowittMapWN34[7] == 0)
								station.UserTemp[7] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[7]] = null;

							cumulus.EcowittMapWN34[7] = settings.httpSensors.ecowitt.mappings.wn34chan7;
						}

						if (cumulus.EcowittMapWN34[8] != settings.httpSensors.ecowitt.mappings.wn34chan8)
						{
							if (cumulus.EcowittMapWN34[8] == 0)
								station.UserTemp[8] = null;
							else
								station.SoilTemp[cumulus.EcowittMapWN34[8]] = null;

							cumulus.EcowittMapWN34[8] = settings.httpSensors.ecowitt.mappings.wn34chan8;
						}

						cumulus.EcowittExtraUseMainForwarders = settings.httpSensors.ecowitt.forwarders == null || settings.httpSensors.ecowitt.forwarders.usemain;

						if (!cumulus.EcowittExtraUseMainForwarders)
						{
							for (var i = 0; i < 10; i++)
							{
								if (i < settings.httpSensors.ecowitt.forwarders.forward.Count)
								{
									cumulus.EcowittExtraForwarders[i] = string.IsNullOrWhiteSpace(settings.httpSensors.ecowitt.forwarders.forward[i].url) ? null : settings.httpSensors.ecowitt.forwarders.forward[i].url.Trim();
								}
								else
								{
									cumulus.EcowittExtraForwarders[i] = null;
								}
							}
						}

						// Also enable extra logging if applicable
						if (cumulus.ExtraSensorUseTempHum || cumulus.ExtraSensorUseSoilTemp || cumulus.ExtraSensorUseSoilMoist || cumulus.ExtraSensorUseLeafWet || cumulus.ExtraSensorUseUserTemp || cumulus.ExtraSensorUseAQI || cumulus.ExtraSensorUseCo2)
						{
							cumulus.StationOptions.LogExtraSensors = true;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Ecowitt API
				try
				{
					if (settings.httpSensors.ecowittapi != null)
					{
						cumulus.EcowittApplicationKey = string.IsNullOrWhiteSpace(settings.httpSensors.ecowittapi.applicationkey) ? null : settings.httpSensors.ecowittapi.applicationkey.Trim();
						cumulus.EcowittUserApiKey = string.IsNullOrWhiteSpace(settings.httpSensors.ecowittapi.userkey) ? null : settings.httpSensors.ecowittapi.userkey.Trim();
						cumulus.EcowittMacAddress = string.IsNullOrWhiteSpace(settings.httpSensors.ecowittapi.mac) ? null : settings.httpSensors.ecowittapi.mac.Trim().ToUpper();
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ecowitt API settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// Ambient Extra settings
				try
				{
					cumulus.AmbientExtraEnabled = settings.httpSensors.extraStation == 1;
					if (cumulus.AmbientExtraEnabled)
					{
						cumulus.AmbientExtraUseSolar = settings.httpSensors.ambient.useSolar;
						cumulus.AmbientExtraUseUv = settings.httpSensors.ambient.useUv;
						cumulus.AmbientExtraUseTempHum = settings.httpSensors.ambient.useTempHum;
						//cumulus.AmbientExtraUseSoilTemp = settings.httpSensors.ambient.useSoilTemp
						cumulus.AmbientExtraUseSoilMoist = settings.httpSensors.ambient.useSoilMoist;
						//cumulus.AmbientExtraUseLeafWet = settings.httpSensors.ambient.useLeafWet
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
				}
				catch (Exception ex)
				{
					var msg = "Error processing Ambient settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// JSON Station Extra settings
				try
				{
					cumulus.JsonExtraStationOptions.ExtraSensorsEnabled = settings.httpSensors.extraStation == 3;

					if (cumulus.JsonExtraStationOptions.ExtraSensorsEnabled)
					{
						cumulus.JsonExtraStationOptions.Connectiontype = settings.httpSensors.jsonstation.conntype;
						if (cumulus.JsonExtraStationOptions.Connectiontype == 0)
						{
							cumulus.JsonExtraStationOptions.SourceFile = string.IsNullOrWhiteSpace(settings.httpSensors.jsonstation.filename) ? null : settings.httpSensors.jsonstation.filename.Trim();
							cumulus.JsonExtraStationOptions.FileReadDelay = settings.httpSensors.jsonstation.advanced.filedelay;
							cumulus.JsonExtraStationOptions.FileIgnoreTime = settings.httpSensors.jsonstation.advanced.fileignore;
						}
						else if (cumulus.JsonExtraStationOptions.Connectiontype == 2)
						{
							cumulus.JsonExtraStationOptions.MqttServer = string.IsNullOrWhiteSpace(settings.httpSensors.jsonstation.mqttserver) ? null : settings.httpSensors.jsonstation.mqttserver.Trim();
							cumulus.JsonExtraStationOptions.MqttPort = settings.httpSensors.jsonstation.mqttport;
							cumulus.JsonExtraStationOptions.MqttUsername = string.IsNullOrWhiteSpace(settings.httpSensors.jsonstation.mqttuser) ? null : settings.httpSensors.jsonstation.mqttuser.Trim();
							cumulus.JsonExtraStationOptions.MqttPassword = string.IsNullOrWhiteSpace(settings.httpSensors.jsonstation.mqttpass) ? null : settings.httpSensors.jsonstation.mqttpass.Trim();
							cumulus.JsonExtraStationOptions.MqttTopic = string.IsNullOrWhiteSpace(settings.httpSensors.jsonstation.mqtttopic) ? null : settings.httpSensors.jsonstation.mqtttopic.Trim();

							cumulus.JsonExtraStationOptions.MqttUseTls = settings.httpSensors.jsonstation.advanced.mqtttls;
						}

						cumulus.ExtraSensorUseSolar = settings.httpSensors.jsonstation.useSolar;
						cumulus.ExtraSensorUseUv = settings.httpSensors.jsonstation.useUv;
						cumulus.ExtraSensorUseTempHum = settings.httpSensors.jsonstation.useTempHum;
						cumulus.ExtraSensorUseSoilMoist = settings.httpSensors.jsonstation.useSoilMoist;
						cumulus.ExtraSensorUseLeafWet = settings.httpSensors.jsonstation.useLeafWet;
						cumulus.ExtraSensorUseUserTemp = settings.httpSensors.jsonstation.useUserTemp;
						cumulus.ExtraSensorUseAQI = settings.httpSensors.jsonstation.useAQI;
						cumulus.ExtraSensorUseCo2 = settings.httpSensors.jsonstation.useCo2;
						cumulus.ExtraSensorUseLaserDist = settings.httpSensors.jsonstation.useLaserDist;

						// Also enable extra logging if applicable
						if (cumulus.ExtraSensorUseTempHum || cumulus.ExtraSensorUseSoilTemp || cumulus.ExtraSensorUseSoilMoist || cumulus.ExtraSensorUseLeafWet || cumulus.ExtraSensorUseUserTemp || cumulus.ExtraSensorUseAQI || cumulus.ExtraSensorUseCo2 || cumulus.ExtraSensorUseLaserDist)
						{
							cumulus.StationOptions.LogExtraSensors = true;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing JSON Station settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Laser settings
				try
				{
					cumulus.LaserDepthBaseline[1] = settings.laser.sensor1.depth;
					cumulus.LaserDepthBaseline[2] = settings.laser.sensor2.depth;
					cumulus.LaserDepthBaseline[3] = settings.laser.sensor3.depth;
					cumulus.LaserDepthBaseline[4] = settings.laser.sensor4.depth;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Laser settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
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
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Purple Air settings
				try
				{
					cumulus.PurpleAirEnabled = settings.purpleAir.enabled;
					if (cumulus.PurpleAirEnabled)
					{
						for (var i = 0; i < 4; i++)
						{
							cumulus.PurpleAirIpAddress[i] = (settings.purpleAir.sensors[i].ipAddress ?? string.Empty).Trim();
							cumulus.PurpleAirAlgorithm[i] = settings.purpleAir.sensors[i].algorithm;
							cumulus.PurpleAirThSensor[i] = settings.purpleAir.sensors[i].thsensor;
						}
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing Purple Air settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Extra Sensor settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Extra Sensor Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		private sealed class JsonSettings
		{
			public bool accessible { get; set; }
			public JsonAirQuality airquality { get; set; }
			public JsonAirLinkSettings airLink { get; set; }
			public JsonHttp httpSensors { get; set; }
			public JsonBlakeLarsen blakeLarsen { get; set; }
			public JsonLaser laser { get; set; }
			public JsonRG11 rg11 { get; set; }
			public JsonPurpleAir purpleAir { get; set; }
		}

		private sealed class JsonAirQuality
		{
			public int primaryaqsensor { get; set; }
			public int aqi { get; set; }
		}

		private sealed class JsonAirLinkSettings
		{
			public bool isNode { get; set; }
			public string apiKey { get; set; }
			public string apiSecret { get; set; }
			public bool autoUpdateIp { get; set; }
			public JsonAirLinkDevice indoor { get; set; }
			public JsonAirLinkDevice outdoor { get; set; }
		}

		private sealed class JsonAirLinkDevice
		{
			public bool enabled { get; set; }
			public string ipAddress { get; set; }
			public string hostname { get; set; }
			public int stationId { get; set; }
		}

		private sealed class JsonHttp
		{
			public int extraStation { get; set; }
			public JsonEcowitt ecowitt { get; set; }
			public JsonAmbient ambient { get; set; }
			public JsonEcowittApi ecowittapi { get; set; }
			public JsonJson jsonstation { get; set; }
		}

		private class JsonAmbient
		{
			public bool useSolar { get; set; }
			public bool useUv { get; set; }
			public bool useTempHum { get; set; }
			public bool useSoilMoist { get; set; }
			public bool useLeafWet { get; set; }
			public bool useUserTemp { get; set; }
			public bool useAQI { get; set; }
			public bool useCo2 { get; set; }
			public bool useLightning { get; set; }
			public bool useLeak { get; set; }
			public bool useCamera { get; set; }
			public bool useLaserDist { get; set; }
		}

		private sealed class JsonEcowitt : JsonAmbient
		{
			public bool setcustom { get; set; }
			public string gwaddr { get; set; }
			public string localaddr { get; set; }
			public int interval { get; set; }
			public JsonEcowittMappings mappings { get; set; }
			public JsonForwarders forwarders { get; set; }
		}

		private sealed class JsonJson : JsonAmbient
		{
			public int conntype { get; set; }
			public string filename { get; set; }
			public string mqttserver { get; set; }
			public int mqttport { get; set; }
			public string mqttuser { get; set; }
			public string mqttpass { get; set; }
			public string mqtttopic { get; set; }

			public JsonJsonStationAdvanced advanced { get; set; }
		}

		private sealed class JsonForwarders
		{
			public bool usemain { get; set; }
			public List<JsonEcowittForwardList> forward { get; set; }
		}

		private sealed class JsonBlakeLarsen
		{
			public bool enabled { get; set; }
		}

		private sealed class JsonLaser
		{
			public JsonLaserDevice sensor1 { get; set; }
			public JsonLaserDevice sensor2 { get; set; }
			public JsonLaserDevice sensor3 { get; set; }
			public JsonLaserDevice sensor4 { get; set; }

		}

		private sealed class JsonLaserDevice
		{
			public decimal depth { get; set; }
		}

		private sealed class JsonRG11
		{
			public JsonRg11Device port1 { get; set; }
			public JsonRg11Device port2 { get; set; }
		}

		private sealed record JsonRg11Device(bool enabled, string commPort, bool tipMode, double tipSize, bool ignoreFirst, bool dtrMode);

		private sealed class JsonPurpleAir
		{
			public bool enabled { get; set; }
			public JsonPurpleAirSensor[] sensors { get; set; }
		}

		private sealed class JsonPurpleAirSensor
		{
			public string id { get; set; }
			public string ipAddress { get; set; }
			public int algorithm { get; set; }
			public int thsensor { get; set; }
		}
	}
}
