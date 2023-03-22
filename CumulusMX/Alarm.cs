using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class Alarm
	{
		public Cumulus cumulus { get; set; }

		public string Name { get; }
		public virtual bool Enabled
		{
			get => enabled;
			set
			{
				enabled = value;

				// if we are disabled, clear any exisitng alarms
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
					doTriggered(value);
				}
			}
		}

		public DateTime TriggeredTime { get => triggeredTime; }
		public bool Notify { get; set; }
		public bool Email { get; set; }
		public string Action { get; set; }
		public string ActionParams { get; set; }
		public bool Latch { get; set; }
		public double LatchHours { get; set; }
		public string EmailMsg { get; set; }
		public string Units { get; set; }
		public string LastError { get; set; }
		public int TriggerThreshold { get; set; }

		AlarmTypes type;
		private protected bool enabled;
		bool triggered;
		int triggerCount = 0;
		DateTime triggeredTime;

		public Alarm(string AlarmName, AlarmTypes AlarmType)
		{
			Name = AlarmName;
			type = AlarmType;
		}

		public void CheckAlarm(double value)
		{
			if (enabled && cumulus.NormalRunning)
			{
				doTriggered((type == AlarmTypes.Above && value > Value) || (type == AlarmTypes.Below && value < Value));
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
						cumulus.LogMessage($"Alarm ({Name}): Triggered, value = {Value}");

						if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
						{
							// Construct the message - preamble, plus values
							var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsg, Value, Units);
							if (!string.IsNullOrEmpty(LastError))
							{
								msg += "\r\nLast error: " + LastError;
							}
							_ = Task.Run(async () =>
							{
								// try to send the email 3 times
								for (int i = 0; i < 3; i++)
								{
									// delay for 0, 60, 120 seconds
									System.Threading.Thread.Sleep(i * 60000);

									cumulus.LogMessage($"Alarm ({Name}): Sending email - attempt {i + 1}");

									if (await cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.Trans.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml))
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
								// Prepare the process to run
								var parser = new TokenParser();
								parser.InputText = ActionParams;
								var args = parser.ToStringFromString();
								cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {args}");
								Utils.RunExternalTask(Action, args, false);
							}
							catch (Exception ex)
							{
								cumulus.LogMessage($"Alarm ({Name}): Error executing external program '{Action}': {ex.Message}");
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
					if (DateTime.Now > TriggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						triggered = false;
						triggerCount = 0;
						cumulus.LogMessage($"Alarm ({Name}): Trigger cleared");
					}
				}
				else
				{
					// No latch, just clear the trigger
					triggered = false;
					triggerCount = 0;
					cumulus.LogMessage($"Alarm ({Name}): Trigger cleared");
				}
			}
		}
	}


	public class AlarmChange : Alarm
	{
		public AlarmChange(string AlarmName) : base(AlarmName, AlarmTypes.Change)
		{
		}

		public override bool Enabled
		{
			get => enabled;
			set
			{
				enabled = value;

				// if we are disabled, clear any exisitng alarms
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
					doUpTriggered(value);
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
					doDownTriggered(value);
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
					doUpTriggered(true);
					doDownTriggered(false);
				}
				else if (value < -Value)
				{
					doUpTriggered(false);
					doDownTriggered(true);
				}
				else
				{
					doUpTriggered(false);
					doDownTriggered(false);
				}
			}
		}

		private void doUpTriggered(bool value)
		{
			if (value)
			{
				// If we were not set before, so we need to send an email etc?
				if (!upTriggered)
				{
					cumulus.LogMessage($"Alarm ({Name}): Up triggered, value = {Value}");

					if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
					{
						// Construct the message - preamble, plus values
						var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsgUp, Value, Units);
						_ = Task.Run(async () =>
						{
							// try to send the email 3 times
							for (int i = 0; i < 3; i++)
							{
								// delay for 0, 60, 120 seconds
								System.Threading.Thread.Sleep(i * 60000);

								cumulus.LogMessage($"Alarm ({Name}): Sending email - attempt {i + 1}");

								if (await cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.Trans.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml))
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
							// Prepare the process to run
							var parser = new TokenParser();
							parser.InputText = ActionParams;
							var args = parser.ToStringFromString();
							cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {args}");
							Utils.RunExternalTask(Action, args, false);
						}
						catch (Exception ex)
						{
							cumulus.LogMessage($"Alarm: Error executing external program '{Action}': {ex.Message}");
						}
					}
				}

				// If we get a new trigger, record the time
				upTriggered = true;
				UpTriggeredTime = DateTime.Now;
			}
			else if (upTriggered)
			{
				// If the trigger is cleared, check if we should be latching the value
				if (Latch)
				{
					if (DateTime.Now > UpTriggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						upTriggered = false;
						cumulus.LogMessage($"Alarm ({Name}): Up trigger cleared");
					}
				}
				else
				{
					// No latch, just clear the trigger
					upTriggered = false;
					cumulus.LogMessage($"Alarm ({Name}): Up trigger cleared");
				}
			}
		}

		private void doDownTriggered(bool value)
		{
			if (value)
			{
				// If we were not set before, so we need to send an email?
				if (!downTriggered && Enabled)
				{
					cumulus.LogMessage($"Alarm ({Name}): Down triggered, value = {Value}");

					if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
					{
						// Construct the message - preamble, plus values
						var msg = cumulus.Trans.AlarmEmailPreamble + "\n" + string.Format(EmailMsgDn, Value, Units);
						_ = Task.Run(async () =>
						{
							// try to send the email 3 times
							for (int i = 0; i < 3; i++)
							{
								// delay for 0, 60, 120 seconds
								System.Threading.Thread.Sleep(i * 60000);

								cumulus.LogMessage($"Alarm ({Name}): Sending email - attempt {i + 1}");

								if (await cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.Trans.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml))
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
							// Prepare the process to run
							var parser = new TokenParser();
							parser.InputText = ActionParams;
							var args = parser.ToStringFromString();
							cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {args}");
							Utils.RunExternalTask(Action, args, false);
						}
						catch (Exception ex)
						{
							cumulus.LogMessage($"Alarm: Error executing external program '{Action}': {ex.Message}");
						}
					}
				}

				// If we get a new trigger, record the time
				downTriggered = true;
				DownTriggeredTime = DateTime.Now;
			}
			else if (downTriggered)
			{
				// If the trigger is cleared, check if we should be latching the value
				if (Latch)
				{
					if (DateTime.Now > DownTriggeredTime.AddHours(LatchHours))
					{
						// We are latching, but the latch period has expired, clear the trigger
						downTriggered = false;
						cumulus.LogMessage($"Alarm ({Name}): Down trigger cleared");
					}
				}
				else
				{
					// No latch, just clear the trigger
					downTriggered = false;
					cumulus.LogMessage($"Alarm ({Name}): Down trigger cleared");
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
		Change,
		Trigger
	}
}
