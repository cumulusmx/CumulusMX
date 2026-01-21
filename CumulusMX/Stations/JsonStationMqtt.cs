using System;
using System.Threading;
using System.Threading.Tasks;

using MQTTnet;


namespace CumulusMX.Stations
{
	public static class JsonStationMqtt
	{
		private static Cumulus cumulus;
		private static JsonStation station;

		private static IMqttClient mqttClient;
		private static bool mainStation;
		private static string prefix;

		internal static void Setup(Cumulus cmx, JsonStation stn, bool main)
		{
			cumulus = cmx;
			station = stn;
			mainStation = main;
			prefix = main ? "JSON Station" : "JSON Extra Station";

			var mqttFactory = new MqttClientFactory();

			/*
			var protocolType = cumulus.MQTT.IpVersion switch
			{x
				4 => System.Net.Sockets.ProtocolType.IPv4,
				6 => System.Net.Sockets.ProtocolType.IPv6,
				_ => System.Net.Sockets.ProtocolType.Unspecified,
			}x
			*/

			try
			{
				var server = mainStation ? cumulus.JsonStationOptions.MqttServer : cumulus.JsonExtraStationOptions.MqttServer;
				var port = mainStation ? cumulus.JsonStationOptions.MqttPort : cumulus.JsonExtraStationOptions.MqttPort;
				var pass = mainStation ? cumulus.JsonStationOptions.MqttPassword : cumulus.JsonExtraStationOptions.MqttPassword;
				var user = mainStation ? cumulus.JsonStationOptions.MqttUsername : cumulus.JsonExtraStationOptions.MqttUsername;
				var tls = mainStation ? cumulus.JsonStationOptions.MqttUseTls : cumulus.JsonExtraStationOptions.MqttUseTls;
				var topic = mainStation ? cumulus.JsonStationOptions.MqttTopic : cumulus.JsonExtraStationOptions.MqttTopic;

				var options = new MqttClientOptionsBuilder()
					.WithClientId("CumulusMXJsonStn" + cumulus.wsPort)
					.WithTcpServer(server, port)
					//.WithProtocolType(protocolType)
					.WithCredentials(string.IsNullOrEmpty(pass) ? null : new MqttClientCredentials(user, System.Text.Encoding.UTF8.GetBytes(pass)))
					.WithTlsOptions(
						new MqttClientTlsOptions()
						{
							UseTls = tls
						}
					)
					.WithoutPacketFragmentation()
					.WithCleanSession()
					.Build();

				mqttClient = (MqttClient) mqttFactory.CreateMqttClient();

				Connect(options).Wait();

				SubscribeTopic(topic).Wait();

				mqttClient.DisconnectedAsync += async e =>
				{
					var delay = 10.0;
					do
					{
						cumulus.LogWarningMessage($"{prefix}: MQTT disconnected from the server - {server}");
						await Task.Delay(TimeSpan.FromSeconds(delay));

						cumulus.LogDebugMessage($"{prefix}: MQTT attempting to reconnect with server - {server}");
						try
						{
							Connect(options).Wait();
							cumulus.LogDebugMessage($"{prefix}: MQTT reconnected OK");
						}
						catch
						{
							cumulus.LogErrorMessage($"{prefix}: MQTT reconnection to server failed");
						}

						delay = Math.Round(delay * 1.5);
					} while (!mqttClient.IsConnected);
				};

				mqttClient.ApplicationMessageReceivedAsync += e =>
				{
					station.ReceiveDataFromMqtt(e.ApplicationMessage);

					return Task.CompletedTask;
				};
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex,$"{prefix}: MQTT failed to create or connect to the host");
				cumulus.LogMessage($"{prefix}: MQTT terminating...");
				Program.ExitSystemTokenSource.Cancel();
			}
		}


		private static async Task SubscribeTopic(string topic)
		{
			cumulus.LogMessage($"{prefix}: Waiting to receive data from MQTT topic = " + topic);
			await mqttClient.SubscribeAsync(topic);
		}


		private static async Task Connect(MqttClientOptions options)
		{
			try
			{
				cumulus.LogMessage($"{prefix}: Connecting to MQTT server = " + (mainStation ? cumulus.JsonStationOptions.MqttServer : cumulus.JsonExtraStationOptions.MqttServer));
				await mqttClient.ConnectAsync(options, CancellationToken.None);
			}
			catch (Exception e)
			{
				cumulus.LogExceptionMessage(e, $"{prefix}: MQTT failed to connect to the host");
			}
		}

		public static void Disconnect()
		{
			mqttClient.DisconnectAsync().Wait();
			mqttClient.Dispose();
		}
	}
}
