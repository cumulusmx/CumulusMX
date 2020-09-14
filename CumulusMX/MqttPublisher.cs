using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace CumulusMX
{
	public static class MqttPublisher
	{
		private static Cumulus cumulus;
		private static MqttClient mqttClient;
		private static string dataupdateTemplateFile;
		private static string dataupdateTemplateContent;
		public static bool configured = false;

		public static void Setup(Cumulus cumulus)
		{
			MqttPublisher.cumulus = cumulus;

			var mqttFactory = new MqttFactory();

			mqttClient = (MqttClient) mqttFactory.CreateMqttClient();

			var clientId = Guid.NewGuid().ToString();

			MQTTnet.Client.Options.MqttClientTcpOptions mqttTcpOptions = new MQTTnet.Client.Options.MqttClientTcpOptions
			{
				Server = cumulus.MQTTServer,
				Port = cumulus.MQTTPort,
				TlsOptions = new MQTTnet.Client.Options.MqttClientTlsOptions { UseTls = cumulus.MQTTUseTLS }
			};

			switch (cumulus.MQTTIpVersion)
			{
				case 4:
					mqttTcpOptions.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork;
					break;
				case 6:
					mqttTcpOptions.AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6;
					break;
				default:
					mqttTcpOptions.AddressFamily = System.Net.Sockets.AddressFamily.Unspecified;
					break;
			}

			var mqttOptions = new MQTTnet.Client.Options.MqttClientOptions();
			mqttOptions.ChannelOptions = mqttTcpOptions;
			mqttOptions.ClientId = clientId;
			mqttOptions.Credentials = String.IsNullOrEmpty(cumulus.MQTTPassword) ? null : new MQTTnet.Client.Options.MqttClientCredentials { Username = cumulus.MQTTUsername, Password = System.Text.Encoding.UTF8.GetBytes(cumulus.MQTTPassword) };
			mqttOptions.CleanSession = true;

			Connect(mqttOptions);

			mqttClient.UseDisconnectedHandler(async e =>
			{
				cumulus.LogMessage("Error: MQTT disconnected from the server");
				await Task.Delay(TimeSpan.FromSeconds(5));

				cumulus.LogDebugMessage("MQTT attempting to reconnect with server");
				try
				{
					Connect(mqttOptions);
					cumulus.LogDebugMessage("MQTT reconnected OK");
				}
				catch
				{
					cumulus.LogMessage("Error: MQTT reconnection to server failed");
				}
			});

			configured = true;
		}


		public static async Task SendMessageAsync(string topic, string message)
		{
			cumulus.LogDataMessage($"MQTT: publishing to topic '{topic}', message '{message}'");
			var mqttMsg = new MqttApplicationMessageBuilder()
				.WithTopic(topic)
				.WithPayload(message)
				.WithRetainFlag()
				.Build();

			await mqttClient.PublishAsync(mqttMsg, CancellationToken.None);
		}

		private static async void Connect(MQTTnet.Client.Options.IMqttClientOptions options)
		{
			try
			{
				await mqttClient.ConnectAsync(options, CancellationToken.None);
			}
			catch (Exception e)
			{
				cumulus.LogMessage("MQTT Error: failed to connect to the host");
				cumulus.LogMessage(e.Message);
			}
		}


		public static void UpdateMQTTfeed(string feedType)
		{
			string message, topic;
			var template = "mqtt/";

			if (feedType == "Interval")
			{
				template += cumulus.MQTTIntervalTemplate;
				topic = cumulus.MQTTIntervalTopic;
			}
			else
			{
				template += cumulus.MQTTUpdateTemplate;
				topic = cumulus.MQTTUpdateTopic;

				// Refresh our copy of the template contents if the filename has changed
				// We want to avoid reading the template file every few seconds if possible.
				if (cumulus.MQTTUpdateTemplate != dataupdateTemplateFile)
				{
					if (File.Exists(template))
					{
						try
						{
							using (TextReader reader = new StreamReader(template, new System.Text.UTF8Encoding(false)))
							{
								dataupdateTemplateContent = reader.ReadToEnd();

							}
						}
						catch (Exception e)
						{
							cumulus.LogMessage($"MQTT: Error reading template file {template} - {e.Message}");
							return;
						}
						dataupdateTemplateFile = cumulus.MQTTUpdateTemplate;
					}
					else
					{
						cumulus.LogMessage($"MQTT: Error, unable to find template file - {template}");
						return;
					}
				}
			}

			if (File.Exists(template))
			{
				// use template file
				cumulus.LogDebugMessage($"MQTT: Using template - {template}");
				var mqttTokenParser = new TokenParser();
				var encoding = new System.Text.UTF8Encoding(false);
				mqttTokenParser.encoding = encoding;
				mqttTokenParser.OnToken += cumulus.TokenParserOnToken;
				if (feedType == "Interval")
				{
					mqttTokenParser.SourceFile = template;
					message = mqttTokenParser.ToString();

				}
				else
				{
					mqttTokenParser.InputText = dataupdateTemplateContent;
					message = mqttTokenParser.ToStringFromString();
				}

				// send the message
				_ = SendMessageAsync(topic, message);
			}
		}
	}
}
