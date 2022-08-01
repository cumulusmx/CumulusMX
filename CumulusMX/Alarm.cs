using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class Alarm
	{
		public Cumulus cumulus { get; set; }

		public string Name { get; }
		public bool Enabled { get; set; }
		public double Value { get; set; }
		public bool Sound { get; set; }
		public string SoundFile { get; set; }
		public bool Triggered
		{
			get => triggered;
			set
			{
				doTriggered(value);
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
			doTriggered((type == AlarmTypes.Above && value > Value) || (type == AlarmTypes.Below && value < Value));
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
					if (!triggered && Enabled)
					{
						cumulus.LogMessage($"Alarm ({Name}): Triggered");

						if (Email && cumulus.SmtpOptions.Enabled)
						{
							cumulus.LogMessage($"Alarm ({Name}): Sending email");

							// Construct the message - preamble, plus values
							var msg = cumulus.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsg, Value, Units);
							if (!string.IsNullOrEmpty(LastError))
							{
								msg += "\r\nLast error: " + LastError;
							}
							cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
						}

						if (!string.IsNullOrEmpty(Action))
						{
							try
							{
								cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {ActionParams}");
								// Prepare the process to run
								ProcessStartInfo start = new ProcessStartInfo();
								// Enter in the command line arguments
								start.Arguments = ActionParams;
								// Enter the executable to run, including the complete path
								start.FileName = Action;
								// Don"t show a console window
								start.CreateNoWindow = true;
								// Run the external process
								Process.Start(start);
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

		bool upTriggered;
		public bool UpTriggered
		{
			get => upTriggered;
			set
			{
				doUpTriggered(value);
			}
		}
		public DateTime UpTriggeredTime { get; set; }


		bool downTriggered;
		public bool DownTriggered
		{
			get => downTriggered;
			set
			{
				doDownTriggered(value);
			}
		}


		public DateTime DownTriggeredTime { get; set; }

		public new void CheckAlarm(double value)
		{
			if (value > Value)
			{
				doUpTriggered(true);
				doDownTriggered(false);
			}
			else if (value < Value)
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

		private void doUpTriggered(bool value)
		{
			if (value)
			{
				// If we were not set before, so we need to send an email etc?
				if (!upTriggered && Enabled)
				{
					cumulus.LogMessage($"Alarm ({Name}): Up triggered");

					if (Email && cumulus.SmtpOptions.Enabled)
					{
						cumulus.LogMessage($"Alarm ({Name}): Sending email");

						// Construct the message - preamble, plus values
						var msg = Program.cumulus.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsgUp, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					if (!string.IsNullOrEmpty(Action))
					{
						try
						{
							cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {ActionParams}");
							// Prepare the process to run
							ProcessStartInfo start = new ProcessStartInfo();
							// Enter in the command line arguments
							start.Arguments = ActionParams;
							// Enter the executable to run, including the complete path
							start.FileName = Action;
							// Don"t show a console window
							start.CreateNoWindow = true;
							// Run the external process
							Process.Start(start);
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
					cumulus.LogMessage($"Alarm ({Name}): Down triggered");

					if (Email && cumulus.SmtpOptions.Enabled)
					{
						cumulus.LogMessage($"Alarm ({Name}): Sending email");
						// Construct the message - preamble, plus values
						var msg = Program.cumulus.AlarmEmailPreamble + "\n" + string.Format(EmailMsgDn, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					if (!string.IsNullOrEmpty(Action))
					{
						try
						{
							cumulus.LogMessage($"Alarm ({Name}): Starting external program: '{Action}', with parameters: {ActionParams}");
							// Prepare the process to run
							ProcessStartInfo start = new ProcessStartInfo();
							// Enter in the command line arguments
							start.Arguments = ActionParams;
							// Enter the executable to run, including the complete path
							start.FileName = Action;
							// Don"t show a console window
							start.CreateNoWindow = true;
							// Run the external process
							Process.Start(start);
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
