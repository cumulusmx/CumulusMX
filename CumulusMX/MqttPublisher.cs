using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Formatter;

namespace CumulusMX
{
	public static class MqttPublisher
	{
		private static Cumulus cumulus;
		private static IMqttClient mqttClient;
		private static MqttClientOptions options;
		public static bool configured { get; set; }
		private static readonly Dictionary<String, String> publishedTopics = [];
		private static MqttTemplate updateTemplate;
		private static MqttTemplate intervalTemplate;

		private static readonly bool starting = true;

		public static void Setup(Cumulus cumulus)
		{
			MqttPublisher.cumulus = cumulus;

			var mqttFactory = new MqttClientFactory();

			var addressFamily = cumulus.MQTT.IpVersion switch
			{
				4 => AddressFamily.InterNetwork,
				6 => AddressFamily.InterNetworkV6,
				_ => AddressFamily.Unspecified,
			};

			var protocolVersion = cumulus.MQTT.ProtocolVersion switch
			{
				3 => MqttProtocolVersion.V310,
				4 => MqttProtocolVersion.V311,
				5 => MqttProtocolVersion.V500,
				_ => MqttProtocolVersion.V311,
			};

			options = new MqttClientOptionsBuilder()
				.WithClientId(Guid.NewGuid().ToString())
				.WithTcpServer(cumulus.MQTT.Server, cumulus.MQTT.Port)
				.WithAddressFamily(addressFamily)
				.WithProtocolVersion(protocolVersion)
				.WithCredentials(string.IsNullOrEmpty(cumulus.MQTT.Password) ? null : new MqttClientCredentials(cumulus.MQTT.Username, System.Text.Encoding.UTF8.GetBytes(cumulus.MQTT.Password)))
				.WithTlsOptions(
					new MqttClientTlsOptions()
					{
						UseTls = cumulus.MQTT.UseTLS
					}
				)
				.WithCleanSession()
				.Build();

			mqttClient = (MqttClient) mqttFactory.CreateMqttClient();


			// Start the task to check the connection state
			_ = Task.Run(async () =>
			{
				do
				{
					try
					{
						// This code will also do the very first connect! So no call to _ConnectAsync_ is required in the first place.

						if (!(await mqttClient.TryPingAsync()))
						{

							if (starting)
							{
								cumulus.LogMessage("MQTT: Connecting to server - " + cumulus.MQTT.Server);
							}
							else
							{
								// Do not log the first time
								cumulus.LogMessage("MQTT: Error, ping failed to server - " + cumulus.MQTT.Server);
								cumulus.LogMessage("MQTT: Attempting to reconnect to server - " + cumulus.MQTT.Server);
							}

							cumulus.LogDebugMessage($"MQTT: Connection parameters - IP={cumulus.MQTT.IpVersion}, TLS={cumulus.MQTT.UseTLS}, Protocol={cumulus.MQTT.ProtocolVersion}");

							var response = await mqttClient.ConnectAsync(options, Program.ExitSystemToken);

							if (mqttClient.IsConnected)
							{
								cumulus.LogMessage("MQTT: Connected to server OK - " + cumulus.MQTT.Server);
							}
							else
							{
								cumulus.LogMessage("MQTT: Failed to connect to server - " + cumulus.MQTT.Server);
								cumulus.LogMessage(JsonSerializer.Serialize(response));
							}
						}
						else
						{
							cumulus.LogDataMessage("MQTT: Server ping OK");
						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "MQTT: Error: Failed to connect to the server - " + cumulus.MQTT.Server);
					}
					finally
					{
						// Check the connection state every 20 seconds and perform a reconnect if required.
						await Task.Delay(TimeSpan.FromSeconds(20), Program.ExitSystemToken);
					}
				} while (!Program.ExitSystemToken.IsCancellationRequested);
			});

			ReadTemplateFiles();

			configured = true;
		}


		public static void ReadTemplateFiles()
		{
			try
			{
				updateTemplate = null;

				if (cumulus.MQTT.EnableDataUpdate && !string.IsNullOrEmpty(cumulus.MQTT.UpdateTemplate))
				{
					// read the config file into memory
					var template = "mqtt/" + cumulus.MQTT.UpdateTemplate;

					if (File.Exists(template))
					{
						// use template file
						cumulus.LogMessage($"MQTT: Reading template file - {template}");

						// read the file
						var templateText = File.ReadAllText(template);
						updateTemplate = JsonSerializer.Deserialize<MqttTemplate>(templateText);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"MQTT: Error reading update template file {cumulus.MQTT.UpdateTemplate}. Message: {ex.Message}");
			}

			try
			{
				intervalTemplate = null;

				if (cumulus.MQTT.EnableInterval && !string.IsNullOrEmpty(cumulus.MQTT.IntervalTemplate))
				{
					// read the config file into memory
					var template = "mqtt/" + cumulus.MQTT.IntervalTemplate;

					if (File.Exists(template))
					{
						// use template file
						cumulus.LogMessage($"MQTT: Reading template file - {template}");

						// read the file
						var templateText = File.ReadAllText(template);
						intervalTemplate = JsonSerializer.Deserialize<MqttTemplate>(templateText);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"MQTT: Error reading interval template file {cumulus.MQTT.IntervalTemplate}. Message: {ex.Message}");
			}
		}

		private static async Task SendMessageAsync(string topic, string message, bool retain)
		{
			if (mqttClient.IsConnected)
			{
				cumulus.LogDataMessage($"MQTT.SendMessageAsync: Publishing to topic '{topic}', message '{message}'");

				var mqttMsg = new MqttApplicationMessageBuilder()
					.WithTopic(topic)
					.WithPayload(message)
					.WithRetainFlag(retain)
					.Build();

				var res = await mqttClient.PublishAsync(mqttMsg, Program.ExitSystemToken);

				if (res.IsSuccess)
				{
					cumulus.LogDebugMessage($"MQTT.SendMessageAsync: Topic '{topic}' published OK");
				}
				else
				{
					cumulus.LogErrorMessage($"MQTT.SendMessageAsync: Error - Topic '{topic}' failed to publish");
				}
			}
			else
			{
				cumulus.LogErrorMessage("MQTT.SendMessageAsync: Error - Not connected to MQTT server - message not sent");
			}
		}


		public static void UpdateMQTTfeed(string feedType, DateTime? now)
		{
			MqttTemplate mqttTemplate;
			int secondsToday = 0;

			if (feedType == "Interval")
			{
				if (intervalTemplate == null)
					return;

				mqttTemplate = intervalTemplate;
				secondsToday = (int) (now.Value.TimeOfDay.Ticks / TimeSpan.TicksPerSecond);
			}
			else
			{
				if (updateTemplate == null)
					return;

				mqttTemplate = updateTemplate;
			}


			// process each of the topics in turn
			try
			{

				foreach (var topic in mqttTemplate.topics)
				{
					if (topic == null) continue;

					if (feedType == "Interval" && secondsToday % (topic.interval ?? 600) != 0)
					{
						// this topic is not ready to update
						continue;
					}

					cumulus.LogDebugMessage($"MQTT: Processing {feedType} Topic: {topic.topic}");

					bool useAltResult = false;
					var mqttTokenParser = new TokenParser(cumulus.TokenParserOnToken) { Encoding = new System.Text.UTF8Encoding(false) };

					if ((feedType == "DataUpdate") && (topic.doNotTriggerOnTags != null))
					{
						useAltResult = true;
						mqttTokenParser.AltResultNoParseList = topic.doNotTriggerOnTags;
					}

					mqttTokenParser.InputText = topic.data;
					string message = mqttTokenParser.ToStringFromString();

					if (useAltResult)
					{
						if (!(publishedTopics.ContainsKey(topic.data) && (publishedTopics[topic.data] == mqttTokenParser.AltResult)))
						{
							// send the message
							_ = SendMessageAsync(topic.topic, message, topic.retain);

							if (publishedTopics.ContainsKey(topic.data))
								publishedTopics[topic.data] = mqttTokenParser.AltResult;
							else
								publishedTopics.Add(topic.data, mqttTokenParser.AltResult);
						}
					}
					else
					{
						_ = SendMessageAsync(topic.topic, message, topic.retain);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage($"UpdateMQTTfeed: Error processing the template file for [{feedType}], error = {ex.Message}");
			}
		}
	}
}
