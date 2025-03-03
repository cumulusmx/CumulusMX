using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using MQTTnet;

using ServiceStack;
using ServiceStack.Text;

namespace CumulusMX
{
	public static class MqttPublisher
	{
		private static Cumulus cumulus;
		private static IMqttClient mqttClient;
		private static MQTTnet.MqttClientOptions options;
		public static bool configured { get; set; }
		private static readonly Dictionary<String, String> publishedTopics = [];
		private static MqttTemplate updateTemplate;
		private static MqttTemplate intervalTemplate;

		private static readonly bool starting = true;

		public static void Setup(Cumulus cumulus)
		{
			MqttPublisher.cumulus = cumulus;

			var mqttFactory = new MqttClientFactory();

			var protocolType = cumulus.MQTT.IpVersion switch
			{
				4 => System.Net.Sockets.ProtocolType.IPv4,
				6 => System.Net.Sockets.ProtocolType.IPv6,
				_ => System.Net.Sockets.ProtocolType.Unspecified,
			};

			options = new MqttClientOptionsBuilder()
				.WithClientId(Guid.NewGuid().ToString())
				.WithTcpServer(cumulus.MQTT.Server, cumulus.MQTT.Port)
				.WithProtocolType(protocolType)
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
				while (!cumulus.cancellationToken.IsCancellationRequested)
				{
					try
					{
						// This code will also do the very first connect! So no call to _ConnectAsync_ is required in the first place.
						if (!await mqttClient.TryPingAsync())
						{
							if (starting)
							{
								cumulus.LogMessage("MQTT: Connecting to server - " + cumulus.MQTT.Server);
							}
							else
							{
								// Do not log the first time
								cumulus.LogWarningMessage("MQTT: Error: MQTT disconnected from the server - " + cumulus.MQTT.Server);
							}

							await mqttClient.ConnectAsync(options, cumulus.cancellationToken);

							if (mqttClient.IsConnected)
							{
								cumulus.LogMessage("MQTT: Connected to server OK - " + cumulus.MQTT.Server);
							}
							else
							{
								cumulus.LogMessage("MQTT: Failed to connect to server - " + cumulus.MQTT.Server);
							}

						}
					}
					catch (Exception ex)
					{
						cumulus.LogExceptionMessage(ex, "MQTT: Error: Failed to connect to the server - " + cumulus.MQTT.Server);
					}
					finally
					{
						// Check the connection state every 20 seconds and perform a reconnect if required.
						await Task.Delay(TimeSpan.FromSeconds(20), cumulus.cancellationToken);
					}
				}
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
						updateTemplate = templateText.FromJson<MqttTemplate>();
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
						intervalTemplate = templateText.FromJson<MqttTemplate>();
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

				var res = await mqttClient.PublishAsync(mqttMsg, cumulus.cancellationToken);

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

			if (feedType == "Interval")
			{
				if (intervalTemplate == null)
					return;

				mqttTemplate = intervalTemplate;
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

					if (feedType == "Interval" && now.Value.ToUnixTime() % (topic.interval ?? 600) != 0)
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
