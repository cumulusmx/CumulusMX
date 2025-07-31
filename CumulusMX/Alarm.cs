using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class Alarm(AlarmIds id, AlarmTypes AlarmType, Cumulus cumul, string units = null)
	{
		public readonly Cumulus cumulus = cumul;

		public AlarmIds Id { get; } = id;

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
		public double Value { get; set; }
		public bool Sound { get; set; }
		public string SoundFile { get; set; }
		public bool Triggered
		{
			get => triggered;
			set
			{
				if (enabled && cumulus.NormalRunning)
				{
					DoTriggered(value);
				}
			}
		}

		public DateTime TriggeredTime { get => triggeredTime; }
		public bool Notify { get; set; }
		public bool Email { get; set; }
		public string Action { get; set; }
		public string ActionParams { get; set; }
		public bool ShowWindow { get; set; }
		public bool Latch { get; set; }
		public double LatchHours { get; set; }
		public string EmailMsg { get; set; }
		public string BskyFile { get; set; }
		public string Units { get; set; } = units;
		public string LastMessage { get; set; }
		public int TriggerThreshold { get; set; }

		private readonly AlarmTypes type = AlarmType;
		private protected bool enabled;
		bool triggered;
		int triggerCount = 0;
		DateTime triggeredTime;

		public void CheckAlarm(double value)
		{
			if (enabled && cumulus.NormalRunning)
			{
				DoTriggered((type == AlarmTypes.Above && value > Value) || (type == AlarmTypes.Below && value < Value));
			}
		}

		public void ClearAlarm()
		{
			if (Latch && triggered && DateTime.UtcNow > triggeredTime.AddHours(LatchHours))
				DoTriggered(false);
		}

		private void DoTriggered(bool value)
		{
			if (value)
			{
				triggerCount++;
				triggeredTime = DateTime.UtcNow;

				// do we have a threshold value
				if (triggerCount >= TriggerThreshold)
				{
					// If we were not set before, so we need to send an email?
					if (!triggered)
					{
						if (Id == AlarmIds.BatteryLow && cumulus.Station.LowBatteryDevices.Count > 0)
						{
							LastMessage += "\r\n" + string.Join(", ", cumulus.Station.LowBatteryDevices);
						}

						cumulus.LogMessage($"Alarm ({Name}): Triggered, value = {value}, threshold = {Value}" + (string.IsNullOrEmpty(LastMessage) ? "" : $", Message = {LastMessage}"));

						if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
						{
							// Construct the message - preamble, plus values
							var parser = new TokenParser(cumulus.TokenParserOnToken)
							{
								InputText = string.Format(EmailMsg, Value, Units)
							};
							var body = parser.ToStringFromString();
							var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + body;

							if (!string.IsNullOrEmpty(LastMessage))
							{
								msg += "\r\nLast message: " + LastMessage;
							}
							_ = Task.Run(async () =>
							{
								// try to send the email 3 times
								for (int i = 0; i < 3; i++)
								{
									// delay for 0, 60, 120 seconds
									System.Threading.Thread.Sleep(i * 60000);

									cumulus.LogMessage($"Alarm ({Name}): Sending email - attempt {i + 1}");

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
								cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {args}");
								_ = Utils.RunExternalTask(Action, args, false, false, ShowWindow);
							}
							catch (FileNotFoundException ex)
							{
								cumulus.LogWarningMessage($"Warning: Alarm ({Name}): External program: '{Action}' does not exist - " + ex.Message);
							}
							catch (Exception ex)
							{
								cumulus.LogExceptionMessage(ex, $"Alarm ({Name}): Error executing external program '{Action}' '{ActionParams}'");
							}
						}

						if (cumulus.Bluesky.Enabled && !string.IsNullOrEmpty(BskyFile) && BskyFile != "none")
						{
							if (File.Exists(BskyFile))
							{
								if (!string.IsNullOrEmpty(cumulus.Bluesky.ID) && !string.IsNullOrEmpty(cumulus.Bluesky.PW))
								{
									// read the template contents
									var template = File.ReadAllText(BskyFile);

									// check for including the default alarm message
									if (template.Contains("|IncludeAlarmMessage|"))
									{
										var msg = EmailMsg ?? string.Empty;

										if (!string.IsNullOrEmpty(LastMessage))
										{
											msg += "\r\nLast message: " + LastMessage;
										}

										template = template.Replace("|IncludeAlarmMessage|", string.Format(msg, Value, Units));
									}

									var parser = new TokenParser(cumulus.TokenParserOnToken)
									{
										InputText = template
									};
									template = parser.ToStringFromString();

									_ = cumulus.Bluesky.DoUpdate(template);
								}
							}
							else
							{
								cumulus.LogWarningMessage($"Warning: Alarm ({Name}): Bluesky file: '{BskyFile}' does not exist");
							}
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
					if (DateTime.UtcNow > triggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						triggered = false;
						triggerCount = 0;
						cumulus.LogMessage($"Alarm ({Name}): Trigger cleared, value = {value}");
					}
				}
				else
				{
					// No latch, just clear the trigger
					triggered = false;
					triggerCount = 0;
					cumulus.LogMessage($"Alarm ({Name}): Trigger cleared, value = {value}");
				}
			}
		}
	}


	public class AlarmChange(AlarmIds idUp, AlarmIds idDwn, Cumulus cumul, string units = null) : Alarm(AlarmIds.ChangeAlarm, AlarmTypes.Change, cumul, units)
	{
		public AlarmIds IdUp { get; } = idUp;
		public AlarmIds IdDown { get; } = idDwn;

		public string NameUp { get; set; }
		public string NameDown { get; set; }

		public override bool Enabled
		{
			get => enabled;
			set
			{
				enabled = value;

				// if we are disabled, clear any existing alarms
				if (!value)
				{
					upTriggered = false;
					UpTriggeredTime = DateTime.MinValue;

					downTriggered = false;
					DownTriggeredTime = DateTime.MinValue;
				}
			}
		}

		bool upTriggered;
		public bool UpTriggered
		{
			get => upTriggered;
			set
			{
				if (enabled && cumulus.NormalRunning)
				{
					DoUpTriggered(value);
				}
			}
		}
		public DateTime UpTriggeredTime { get; set; }


		bool downTriggered;
		public bool DownTriggered
		{
			get => downTriggered;
			set
			{
				if (enabled && cumulus.NormalRunning)
				{
					DoDownTriggered(value);
				}
			}
		}


		public DateTime DownTriggeredTime { get; set; }

		public new void CheckAlarm(double value)
		{
			if (enabled && cumulus.NormalRunning)
			{

				if (value > Value)
				{
					DoUpTriggered(true);
					DoDownTriggered(false);
				}
				else if (value < -Value)
				{
					DoUpTriggered(false);
					DoDownTriggered(true);
				}
				else
				{
					DoUpTriggered(false);
					DoDownTriggered(false);
				}
			}
		}

		public new void ClearAlarm()
		{
			if (Latch && upTriggered && DateTime.UtcNow > UpTriggeredTime.AddHours(LatchHours))
				DoUpTriggered(false);

			if (Latch && downTriggered && DateTime.UtcNow > DownTriggeredTime.AddHours(LatchHours))
				DoDownTriggered(false);
		}

		private void DoUpTriggered(bool value)
		{
			if (value)
			{
				// If we were not set before, so we need to send an email etc?
				if (!upTriggered)
				{
					cumulus.LogMessage($"Alarm ({NameUp}): Up triggered, value = {value}, threshold = {Value}" + (string.IsNullOrEmpty(LastMessage) ? "" : $", Message = {LastMessage}"));

					if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
					{
						// Construct the message - preamble, plus values
						var parser = new TokenParser(cumulus.TokenParserOnToken)
						{
							InputText = string.Format(EmailMsg, Value, Units)
						};
						var body = parser.ToStringFromString();
						var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + body;

						_ = Task.Run(async () =>
						{
							// try to send the email 3 times
							for (int i = 0; i < 3; i++)
							{
								// delay for 0, 60, 120 seconds
								System.Threading.Thread.Sleep(i * 60000);

								cumulus.LogMessage($"Alarm ({NameUp}): Sending email - attempt {i + 1}");

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
							cumulus.LogMessage($"Alarm ({NameUp}): Starting external program: '{Action}', with parameters: {args}");
							_ = Utils.RunExternalTask(Action, args, false, false, ShowWindow);
						}
						catch (FileNotFoundException ex)
						{
							cumulus.LogWarningMessage($"Warning: Alarm ({NameUp}): External program: '{Action}' does not exist - " + ex.Message);
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, $"Alarm ({NameUp}): Error executing external program '{Action}'");
						}
					}
				}

				if (cumulus.Bluesky.Enabled && !string.IsNullOrEmpty(BskyFile) && BskyFile != "none")
				{
					if (File.Exists(BskyFile))
					{
						if (!string.IsNullOrEmpty(cumulus.Bluesky.ID) && !string.IsNullOrEmpty(cumulus.Bluesky.PW))
						{
							// read the template contents
							var template = File.ReadAllText(BskyFile);

							// check for including the default alarm message
							if (template.Contains("|IncludeAlarmMessage|"))
							{
								var msg = EmailMsg ?? string.Empty;

								if (!string.IsNullOrEmpty(LastMessage))
								{
									msg += "\r\nLast message: " + LastMessage;
								}

								template = template.Replace("|IncludeAlarmMessage|", string.Format(msg, Value, Units));
							}

							var parser = new TokenParser(cumulus.TokenParserOnToken)
							{
								InputText = template
							};
							template = parser.ToStringFromString();

							_ = cumulus.Bluesky.DoUpdate(template);
						}
					}
					else
					{
						cumulus.LogWarningMessage($"Warning: Alarm ({NameUp}): Bluesky file: '{BskyFile}' does not exist");
					}
				}

				// If we get a new trigger, record the time
				upTriggered = true;
				UpTriggeredTime = DateTime.UtcNow;
			}
			else if (upTriggered)
			{
				// If the trigger is cleared, check if we should be latching the value
				if (Latch)
				{
					if (DateTime.UtcNow > UpTriggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						upTriggered = false;
						cumulus.LogMessage($"Alarm ({NameUp}): Up trigger cleared, value = {value}");
					}
				}
				else
				{
					// No latch, just clear the trigger
					upTriggered = false;
					cumulus.LogMessage($"Alarm ({NameUp}): Up trigger cleared, value = {value}");
				}
			}
		}

		private void DoDownTriggered(bool value)
		{
			if (value)
			{
				// If we were not set before, so we need to send an email?
				if (!downTriggered && Enabled)
				{
					cumulus.LogMessage($"Alarm ({NameDown}): Down triggered, value = {value}, threshold = {Value}" + (string.IsNullOrEmpty(LastMessage) ? "" : $", Message = {LastMessage}"));

					if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
					{
						// Construct the message - preamble, plus values
						var parser = new TokenParser(cumulus.TokenParserOnToken)
						{
							InputText = string.Format(EmailMsg, Value, Units)
						};
						var body = parser.ToStringFromString();
						var msg = cumulus.Trans.AlarmEmailPreamble + "\n" + body;

						_ = Task.Run(async () =>
						{
							// try to send the email 3 times
							for (int i = 0; i < 3; i++)
							{
								// delay for 0, 60, 120 seconds
								System.Threading.Thread.Sleep(i * 60000);

								cumulus.LogMessage($"Alarm ({NameDown}): Sending email - attempt {i + 1}");

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
							cumulus.LogMessage($"Alarm ({NameDown}): Starting external program: '{Action}', with parameters: {args}");
							_ = Utils.RunExternalTask(Action, args, false, false, ShowWindow);
						}
						catch (FileNotFoundException ex)
						{
							cumulus.LogWarningMessage($"Warning: Alarm ({NameDown}): External program: '{Action}' does not exist - " + ex.Message);
						}
						catch (Exception ex)
						{
							cumulus.LogExceptionMessage(ex, $"Alarm ({NameDown}): Error executing external program '{Action}'");
						}
					}

					if (cumulus.Bluesky.Enabled && !string.IsNullOrEmpty(BskyFile) && BskyFile != "none")
					{
						if (File.Exists(BskyFile))
						{
							if (!string.IsNullOrEmpty(cumulus.Bluesky.ID) && !string.IsNullOrEmpty(cumulus.Bluesky.PW))
							{
								// read the template contents
								var template = File.ReadAllText(BskyFile);

								// check for including the default alarm message
								if (template.Contains("|IncludeAlarmMessage|"))
								{
									var msg = EmailMsg ?? string.Empty;

									if (!string.IsNullOrEmpty(LastMessage))
									{
										msg += "\r\nLast message: " + LastMessage;
									}

									template = template.Replace("|IncludeAlarmMessage|", string.Format(msg, Value, Units));
								}

								var parser = new TokenParser(cumulus.TokenParserOnToken)
								{
									InputText = template
								};
								template = parser.ToStringFromString();

								_ = cumulus.Bluesky.DoUpdate(template);
							}
						}
						else
						{
							cumulus.LogWarningMessage($"Warning: Alarm ({NameDown}): Bluesky file: '{BskyFile}' does not exist");
						}
					}
				}

				// If we get a new trigger, record the time
				downTriggered = true;
				DownTriggeredTime = DateTime.UtcNow;
			}
			else if (downTriggered)
			{
				// If the trigger is cleared, check if we should be latching the value
				if (Latch)
				{
					if (DateTime.UtcNow > DownTriggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						downTriggered = false;
						cumulus.LogMessage($"Alarm ({NameDown}): Down trigger cleared, value = {value}");
					}
				}
				else
				{
					// No latch, just clear the trigger
					downTriggered = false;
					cumulus.LogMessage($"Alarm ({NameDown}): Down trigger cleared, value = {value}");
				}
			}
		}

		public string EmailMsgUp { get; set; }
		public string EmailMsgDn { get; set; }
	}

	public enum AlarmTypes
	{
		Above,
		Below,
		Equals,
		Change,
		Trigger
	}

	public enum AlarmIds
	{
		ChangeAlarm,
		DataStopped,
		BatteryLow,
		Sensor,
		Spike,
		WindHigh,
		WindGust,
		RainRate,
		Rainfall,
		PressUp,
		PressDown,
		PressHigh,
		PressLow,
		TempUp,
		TempDown,
		TempLow,
		TempHigh,
		Upgrade,
		Firmware,
		Thirdparty,
		MySQL,
		IsRaining,
		Record,
		FTP,
		Error,
		User1 = 101,
		User2 = 102,
		User3 = 103,
		User4 = 104,
		User5 = 105,
		User6 = 106,
		User7 = 107,
		User8 = 108,
		User9 = 109,
		User10 = 110
	}

	[DataContract]
	public class DashboardAlarms(AlarmIds Id, bool Triggered)
	{
		[DataMember]
		public string id { get; set; } = "Alarm" + Id.ToString();
		[DataMember]
		public bool triggered { get; set; } = Triggered;
	}
}
