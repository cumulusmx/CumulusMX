using System;
using System.Threading;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Client;


namespace CumulusMX
{
	public static class JsonStationMqtt
	{
		private static Cumulus cumulus;
		private static JsonStation station;

		private static IMqttClient mqttClient;

		internal static void Setup(Cumulus cmx, JsonStation stn)
		{
			cumulus = cmx;
			station = stn;

			var mqttFactory = new MqttFactory();

			/*
			var protocolType = cumulus.MQTT.IpVersion switch
			{x
				4 => System.Net.Sockets.ProtocolType.IPv4,
				6 => System.Net.Sockets.ProtocolType.IPv6,
				_ => System.Net.Sockets.ProtocolType.Unspecified,
			}x
			*/

			var options = new MqttClientOptionsBuilder()
				.WithClientId("CumulusMXJsonStn" + cumulus.wsPort)
				.WithTcpServer(cumulus.JsonStationOptions.MqttServer, cumulus.JsonStationOptions.MqttPort)
				//.WithProtocolType(protocolType)
				.WithCredentials(string.IsNullOrEmpty(cumulus.JsonStationOptions.MqttPassword) ? null : new MqttClientCredentials(cumulus.JsonStationOptions.MqttUsername, System.Text.Encoding.UTF8.GetBytes(cumulus.JsonStationOptions.MqttPassword)))
				.WithTlsOptions(
					new MqttClientTlsOptions()
					{
						UseTls = cumulus.JsonStationOptions.MqttUseTls
					}
				)
				.WithoutPacketFragmentation()
				.WithCleanSession()
				.Build();

			mqttClient = (MqttClient) mqttFactory.CreateMqttClient();

			Connect(options).Wait();

			SubscribeTopic(cumulus.JsonStationOptions.MqttTopic).Wait();

			mqttClient.DisconnectedAsync += (async e =>
			{
				var delay = 10.0;
				do
				{
					cumulus.LogWarningMessage("Error: JSON Station MQTT disconnected from the server");
					await Task.Delay(TimeSpan.FromSeconds(delay));

					cumulus.LogDebugMessage("JSON Station MQTT attempting to reconnect with server");
					try
					{
						Connect(options).Wait();
						cumulus.LogDebugMessage("JSON Station MQTT reconnected OK");
					}
					catch
					{
						cumulus.LogErrorMessage("Error: JSON Station MQTT reconnection to server failed");
					}

					delay = Math.Round(delay * 1.5);
				} while (!mqttClient.IsConnected);
			});

			mqttClient.ApplicationMessageReceivedAsync += e =>
			{
				station.ReceiveDataFromMqtt(e.ApplicationMessage);

				return Task.CompletedTask;
			};
		}


		private static async Task SubscribeTopic(string topic)
		{
			cumulus.LogMessage("JSON Station: Waiting to receive data from MQTT topic = " + topic);
			await mqttClient.SubscribeAsync(topic);
		}


		private static async Task Connect(MQTTnet.Client.MqttClientOptions options)
		{
			try
			{
				cumulus.LogMessage("JSON Station: Connecting to MQTT server = " + cumulus.JsonStationOptions.MqttServer);
				await mqttClient.ConnectAsync(options, CancellationToken.None);
			}
			catch (Exception e)
			{
				cumulus.LogErrorMessage("JSON MQTT Error: failed to connect to the host");
				cumulus.LogMessage(e.Message);
			}
		}

		public static void Disconnect()
		{
			mqttClient.DisconnectAsync().Wait();
			mqttClient.Dispose();
		}
	}
}
