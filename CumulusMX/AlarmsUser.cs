using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class AlarmUser
	{
		private readonly Cumulus cumulus;

		public string Name { get; set; }
		public virtual bool Enabled
		{
			get => enabled;
			set
			{
				enabled = value;

				// if we are disabled, clear any existing alarms
				if (!value)
				{
					triggered = false;
					triggerCount = 0;
					triggeredTime = DateTime.MinValue;
				}
			}
		}
		public string WebTag { get; set; }
		public double Value { get; set; }
		[IgnoreDataMember]
		public bool Triggered
		{
			get => triggered;
			set
			{
				if (enabled && cumulus.NormalRunning)
				{
					doTriggered(value);
				}
			}
		}

		[IgnoreDataMember]
		public DateTime TriggeredTime { get => triggeredTime; }
		public bool Email { get; set; }
		public string Action { get; set; }
		public string ActionParams { get; set; }
		public bool ShowWindow { get; set; }
		public bool Latch { get; set; }
		public double LatchHours { get; set; }
		public string EmailMsg { get; set; }
		public string BskyFile { get; set; }
		public string Units { get; set; }
		public int TriggerThreshold { get; set; }
		public string Type
		{
			get => type.ToString();
			set
			{
				type = value.ToLower() switch
				{
					"above" => AlarmTypes.Above,
					"below" => AlarmTypes.Below,
					_ => AlarmTypes.Above,
				};
			}
		}

		private AlarmTypes type;
		private protected bool enabled;
		bool triggered;
		int triggerCount = 0;
		DateTime triggeredTime;

		private readonly TokenParser tokenParser;
		private double tagValue;

		public AlarmUser(string AlarmName, string AlarmType, string webTag, Cumulus cuml)
		{
			Name = AlarmName;

			type = AlarmType.ToLower() switch
			{
				"above" => AlarmTypes.Above,
				"below" => AlarmTypes.Below,
				_ => AlarmTypes.Above,
			};
			cumulus = cuml;
			WebTag = webTag;
			tokenParser = new TokenParser(cumulus.TokenParserOnToken)
			{
				InputText = WebTag
			};
		}

		public void CheckAlarm()
		{
			if (enabled && cumulus.NormalRunning)
			{
				if (double.TryParse(tokenParser.ToStringFromString(), out tagValue))
				{
					doTriggered((type == AlarmTypes.Above && tagValue > Value) || (type == AlarmTypes.Below && tagValue < Value));
				}
				else
				{
					cumulus.LogErrorMessage($"User Alarm ({Name}): Error parsing web tag value: {WebTag}");
				}
			}
		}

		public void ClearAlarm()
		{
			if (Latch && triggered && DateTime.Now > triggeredTime.AddHours(LatchHours))
			{
				doTriggered(false);
			}
		}

		private void doTriggered(bool value)
		{
			if (value)
			{
				triggerCount++;
				triggeredTime = DateTime.Now;

				// do we have a threshold value
				if (triggerCount >= TriggerThreshold)
				{
					// If we were not set before, so we need to send an email?
					if (!triggered)
					{
						cumulus.LogMessage($"User Alarm ({Name}): Triggered, value = {tagValue}");

						if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
						{
							// Construct the message - preamble, plus values
							var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsg, tagValue);
							_ = Task.Run(async () =>
							{
								// try to send the email 3 times
								for (int i = 0; i < 3; i++)
								{
									// delay for 0, 60, 120 seconds
									System.Threading.Thread.Sleep(i * 60000);

									cumulus.LogMessage($"User Alarm ({Name}): Sending email - attempt {i + 1}");

									if (await cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.Trans.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml, cumulus.AlarmEmailUseBcc))
									{
										break;
									}
								}
							});
						}

						if (!string.IsNullOrEmpty(Action))
						{
							try
							{
								var args = string.Empty;
								// Prepare the process to run
								if (!string.IsNullOrEmpty(ActionParams))
								{
									var parser = new TokenParser(cumulus.TokenParserOnToken)
									{
										InputText = ActionParams
									};
									args = parser.ToStringFromString();
								}
								cumulus.LogMessage($"User Alarm ({Name}): Starting external program: '{Action}', with parameters: {args}");
								_ = Utils.RunExternalTask(Action, args, false, false, ShowWindow);
							}
							catch (FileNotFoundException ex)
							{
								cumulus.LogWarningMessage($"Warning: Alarm ({Name}): External program: '{Action}' does not exist - " + ex.Message);
							}
							catch (Exception ex)
							{
								cumulus.LogExceptionMessage(ex, $"User Alarm ({Name}): Error executing external program '{Action}'");
							}
						}
					}

					if (cumulus.Bluesky.Enabled && !string.IsNullOrEmpty(BskyFile))
					{
						if (File.Exists(BskyFile))
						{
							if (!string.IsNullOrEmpty(cumulus.Bluesky.ID) && !string.IsNullOrEmpty(cumulus.Bluesky.PW))
							{
								// read the template contents
								var template = File.ReadAllText(BskyFile);

								// check for including teh default alarm message
								if (template.Contains("|IncludeAlarmMessage|"))
								{
									// Construct the message - preamble, plus values
									var msg = string.Format(EmailMsg, Value, Units);

									template = template.Replace("|IncludeAlarmMessage|", msg);

									var parser = new TokenParser(cumulus.TokenParserOnToken)
									{
										InputText = template
									};
									template = parser.ToStringFromString();
								}

								_ = cumulus.Bluesky.DoUpdate(template);
							}
						}
						else
						{
							cumulus.LogWarningMessage($"Warning: Alarm ({Name}): Bluesky file: '{BskyFile}' does not exist");
						}
					}

					// record the state
					triggered = true;
				}
			}
			else if (triggered)
			{
				// If the trigger is cleared, check if we should be latching the value
				if (Latch)
				{
					if (DateTime.Now > TriggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						triggered = false;
						triggerCount = 0;
						cumulus.LogMessage($"User Alarm ({Name}): Trigger cleared, value = {tagValue}");
					}
				}
				else
				{
					// No latch, just clear the trigger
					triggered = false;
					triggerCount = 0;
					cumulus.LogMessage($"User Alarm ({Name}): Trigger cleared, value = {tagValue}");
				}
			}
		}
	}
}
