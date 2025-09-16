using System;
using System.IO;
using System.Net;
using System.Text.Json;

using EmbedIO;


namespace CumulusMX.Settings
{
	public class MqttSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = string.Empty;
			var json = string.Empty;
			MqttConfig settings;

			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<MqttConfig>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing MQTT Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("MQTT Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating internet settings");

				// MQTT
				try
				{
					cumulus.MQTT.Server = settings.server ?? string.Empty;
					cumulus.MQTT.Port = settings.port;
					cumulus.MQTT.Username = settings.username ?? string.Empty;
					cumulus.MQTT.Password = settings.password ?? string.Empty;

					cumulus.MQTT.UseTLS = settings.advanced.useTls;
					cumulus.MQTT.IpVersion = settings.advanced.ipVersion;
					cumulus.MQTT.ProtocolVersion = settings.advanced.protocolVersion;

					cumulus.MQTT.EnableDataUpdate = settings.dataUpdate.enabled;
					if (cumulus.MQTT.EnableDataUpdate)
					{
						cumulus.MQTT.UpdateTemplate = settings.dataUpdate.template ?? string.Empty;
					}

					cumulus.MQTT.EnableInterval = settings.interval.enabled;
					if (cumulus.MQTT.EnableInterval)
					{
						cumulus.MQTT.IntervalTemplate = settings.interval.template ?? string.Empty;
					}
				}
				catch (Exception ex)
				{
					var msg = "Error processing MQTT settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteIniFile();

				// Setup MQTT
				if (cumulus.MQTT.EnableDataUpdate || cumulus.MQTT.EnableInterval)
				{
					if (!MqttPublisher.configured)
					{
						MqttPublisher.Setup(cumulus);
					}
					else
					{
						MqttPublisher.ReadTemplateFiles();
					}
				}
			}
			catch (Exception ex)
			{
				var msg = "Error processing MQTT settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("MQTT data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		public string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it

			var mqttUpdate = new MqttData()
			{
				enabled = cumulus.MQTT.EnableDataUpdate,
				template = cumulus.MQTT.UpdateTemplate
			};

			var mqttInterval = new MqttData()
			{
				enabled = cumulus.MQTT.EnableInterval,
				template = cumulus.MQTT.IntervalTemplate
			};

			var mqttAdvanced = new MqttAdvanced()
			{
				useTls = cumulus.MQTT.UseTLS,
				ipVersion = cumulus.MQTT.IpVersion,
				protocolVersion = cumulus.MQTT.ProtocolVersion
			};

			var mqttsettings = new MqttConfig()
			{
				server = cumulus.MQTT.Server,
				port = cumulus.MQTT.Port,
				username = cumulus.MQTT.Username,
				password = cumulus.MQTT.Password,
				advanced = mqttAdvanced,
				dataUpdate = mqttUpdate,
				interval = mqttInterval
			};

			return JsonSerializer.Serialize(mqttsettings);
		}

		private sealed class MqttConfig
		{
			public string server { get; set; }
			public int port { get; set; }
			public string username { get; set; }
			public string password { get; set; }

			public MqttAdvanced advanced { get; set; }
			public MqttData dataUpdate { get; set; }
			public MqttData interval { get; set; }
		}

		private sealed class MqttAdvanced
		{
			public bool useTls { get; set; }
			public int ipVersion { get; set; }
			public int protocolVersion { get; set; }
		}

		private sealed class MqttData
		{
			public bool enabled { get; set; }
			public string template { get; set; }
		}
	}
}
