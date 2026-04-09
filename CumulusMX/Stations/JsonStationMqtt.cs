using System;
using System.Text.Json;
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

				mqttClient.DisconnectedAsync += async e =>
				{
					if (e.ClientWasConnected && !Program.ExitSystemToken.IsCancellationRequested)
					{
						var delay = 10.0;

						cumulus.LogErrorMessage($"{prefix}: MQTT disconnected from the server - {server}, retry in {delay} secs");

						do
						{
							await Task.Delay(TimeSpan.FromSeconds(delay), Program.ExitSystemToken);

							cumulus.LogMessage($"{prefix}: MQTT attempting to reconnect with server - {server}");

							if (await Connect(options))
							{
								cumulus.LogMessage($"{prefix}: MQTT reconnected OK");
								return;
							}

							delay = Math.Round(delay * 1.5);
							cumulus.LogErrorMessage($"{prefix}: MQTT reconnection to server failed, retry in {delay} secs");
						} while (!mqttClient.IsConnected);
					}
				};

				mqttClient.ApplicationMessageReceivedAsync += e =>
				{
					station.ReceiveDataFromMqtt(e.ApplicationMessage);

					return Task.CompletedTask;
				};

				// Synchronously attempt to connect until successful or cancelled
				while (!Connect(options).GetAwaiter().GetResult() && !Program.ExitSystemToken.IsCancellationRequested)
				{
					// brief delay to avoid tight loop
					try
					{
						Task.Delay(TimeSpan.FromSeconds(10), Program.ExitSystemToken).Wait();
					}
					catch(OperationCanceledException)
					{
						// cancelled, do nothing
					}
				}

				if (mqttClient.IsConnected)
				{
					SubscribeTopic(topic).Wait();
				}

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
			await mqttClient.SubscribeAsync(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce, Program.ExitSystemToken);
		}


		private static async Task<bool> Connect(MqttClientOptions options)
		{
			try
			{
				cumulus.LogMessage($"{prefix}: Connecting to MQTT server = " + (mainStation ? cumulus.JsonStationOptions.MqttServer : cumulus.JsonExtraStationOptions.MqttServer));
				var response = await mqttClient.ConnectAsync(options, Program.ExitSystemToken);
				var debugStr = "NULL";
				if (response != null)
				{
					//debugStr = JsonSerializer.Serialize(response, SerializerOptions);
					debugStr = JsonSerializer.Serialize(response);
				}
				cumulus.LogDebugMessage($"{prefix}: Connection result = {debugStr}");
			}
			catch (MQTTnet.Exceptions.MqttCommunicationTimedOutException)
			{
				cumulus.LogMessage($"{prefix}: MQTT reconnect to the host timed out");
			}
			catch (OperationCanceledException)
			{
				cumulus.LogMessage($"{prefix}: MQTT reconnect Cancelled");
			}
			catch (Exception e)
			{
				cumulus.LogExceptionMessage(e, $"{prefix}: MQTT failed to connect to the host");
			}

			return mqttClient.IsConnected;
		}

		public static void Disconnect()
		{
			mqttClient.DisconnectAsync().Wait();
			mqttClient.Dispose();
		}

		static readonly JsonSerializerOptions SerializerOptions = new()
		{
			WriteIndented = true
		};
	}
}
