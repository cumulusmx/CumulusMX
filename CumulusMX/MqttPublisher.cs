using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using ServiceStack;

namespace CumulusMX
{
	public static class MqttPublisher
	{
		private static Cumulus cumulus;
		private static MqttClient mqttClient;
		public static bool configured;

		public static void Setup(Cumulus cumulus)
		{
			MqttPublisher.cumulus = cumulus;

			var mqttFactory = new MqttFactory();

			mqttClient = (MqttClient)mqttFactory.CreateMqttClient();

			var clientId = Guid.NewGuid().ToString();

			var mqttTcpOptions = new MQTTnet.Client.Options.MqttClientTcpOptions
			{
				Server = cumulus.MQTT.Server,
				Port = cumulus.MQTT.Port,
				TlsOptions = new MQTTnet.Client.Options.MqttClientTlsOptions { UseTls = cumulus.MQTT.UseTLS }
			};

			switch (cumulus.MQTT.IpVersion)
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

			var mqttOptions = new MQTTnet.Client.Options.MqttClientOptions
			{
				ChannelOptions = mqttTcpOptions,
				ClientId = clientId,
				Credentials = string.IsNullOrEmpty(cumulus.MQTT.Password)
					? null
					: new MQTTnet.Client.Options.MqttClientCredentials
					{
						Username = cumulus.MQTT.Username,
						Password = System.Text.Encoding.UTF8.GetBytes(cumulus.MQTT.Password)
					},
				CleanSession = true
			};

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


		private static async Task SendMessageAsync(string topic, string message, bool retain)
		{
			cumulus.LogDataMessage($"MQTT: publishing to topic '{topic}', message '{message}'");
			if (mqttClient.IsConnected)
			{
				var mqttMsg = new MqttApplicationMessageBuilder()
					.WithTopic(topic)
					.WithPayload(message)
					.WithRetainFlag(retain)
					.Build();

				await mqttClient.PublishAsync(mqttMsg, CancellationToken.None);
			}
			else
			{
				cumulus.LogMessage("MQTT: Error - Not connected to MQTT server - message not sent");
			}
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
			var template = "mqtt/";

			if (feedType == "Interval")
			{
				template += cumulus.MQTT.IntervalTemplate;
			}
			else
			{
				template += cumulus.MQTT.UpdateTemplate;
			}

			if (!File.Exists(template))
				return;

			// use template file
			cumulus.LogDebugMessage($"MQTT: Using template - {template}");

			// read the file
			var templateText = File.ReadAllText(template);
			var templateObj = templateText.FromJson<MqttTemplate>();

			// process each of the topics in turn
			foreach(var feed in templateObj.topics)
			{
				var mqttTokenParser = new TokenParser { Encoding = new System.Text.UTF8Encoding(false) };
				mqttTokenParser.OnToken += cumulus.TokenParserOnToken;
				mqttTokenParser.InputText = feed.data;
				string message = mqttTokenParser.ToStringFromString();

				// send the message
				_ = SendMessageAsync(feed.topic, message, feed.retain);
			}
		}
	}
}
