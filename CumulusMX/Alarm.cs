using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class Alarm(string id, AlarmTypes AlarmType, Cumulus cumul, string units = null)
	{
		public readonly Cumulus cumulus = cumul;

		public string Id { get; } = id;

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
				doTriggered((type == AlarmTypes.Above && value > Value) || (type == AlarmTypes.Below && value < Value));
			}
		}

		public void ClearAlarm()
		{
			if (Latch && triggered && DateTime.Now > triggeredTime.AddHours(LatchHours))
				doTriggered(false);
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
						cumulus.LogMessage($"Alarm ({Name}): Triggered, value = {value}, threshold = {Value}" + (string.IsNullOrEmpty(LastMessage) ? "" : $", Message = {LastMessage}"));

						if (Email && cumulus.SmtpOptions.Enabled && cumulus.emailer != null)
						{
							// Construct the message - preamble, plus values
							var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsg, Value, Units);
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
								// Prepare the process to run
								var parser = new TokenParser(cumulus.TokenParserOnToken)
								{
									InputText = ActionParams
								};
								var args = parser.ToStringFromString();
								cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {args}");
								Utils.RunExternalTask(Action, args, false);
							}
							catch (Exception ex)
							{
								cumulus.LogErrorMessage($"Alarm ({Name}): Error executing external program '{Action}': {ex.Message}");
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


	public class AlarmChange(string idUp, string idDwn, Cumulus cumul, string units = null) : Alarm("", AlarmTypes.Change, cumul, units)
	{
		public string IdUp { get; } = idUp;
		public string IdDown { get; } = idDwn;

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

		public new void ClearAlarm()
		{
			if (Latch && upTriggered && DateTime.Now > UpTriggeredTime.AddHours(LatchHours))
				doUpTriggered(false);

			if (Latch && downTriggered && DateTime.Now > DownTriggeredTime.AddHours(LatchHours))
				doDownTriggered(false);
		}

		private void doUpTriggered(bool value)
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
						var msg = cumulus.Trans.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsgUp, Value, Units);
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
							// Prepare the process to run
							var parser = new TokenParser(cumulus.TokenParserOnToken)
							{
								InputText = ActionParams
							};
							var args = parser.ToStringFromString();
							cumulus.LogMessage($"Alarm ({NameUp}): Starting external program: '{Action}', with parameters: {args}");
							Utils.RunExternalTask(Action, args, false);
						}
						catch (Exception ex)
						{
							cumulus.LogErrorMessage($"Alarm ({NameUp}): Error executing external program '{Action}': {ex.Message}");
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

		private void doDownTriggered(bool value)
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
						var msg = cumulus.Trans.AlarmEmailPreamble + "\n" + string.Format(EmailMsgDn, Value, Units);
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
							// Prepare the process to run
							var parser = new TokenParser(cumulus.TokenParserOnToken)
							{
								InputText = ActionParams
							};
							var args = parser.ToStringFromString();
							cumulus.LogMessage($"Alarm ({NameDown}): Starting external program: '{Action}', with parameters: {args}");
							Utils.RunExternalTask(Action, args, false);
						}
						catch (Exception ex)
						{
							cumulus.LogErrorMessage($"Alarm ({NameDown}): Error executing external program '{Action}': {ex.Message}");
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
		Change,
		Trigger
	}

	[DataContract]
	public class DashboardAlarms(string Id, bool Triggered)
	{
		[DataMember]
		public string id { get; set; } = Id;
		[DataMember]
		public bool triggered { get; set; } = Triggered;
	}
}
