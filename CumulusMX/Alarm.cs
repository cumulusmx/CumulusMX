using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{
	public class Alarm
	{
		public Cumulus cumulus { get; set; }

		public bool Enabled { get; set; }
		public double Value { get; set; }
		public bool Sound { get; set; }
		public string SoundFile { get; set; }

		bool triggered;
		public bool Triggered
		{
			get => triggered;
			set
			{
				if (value)
				{
					triggerCount++;
					TriggeredTime = DateTime.Now;

					// do we have a threshold value
					if (triggerCount >= TriggerThreshold)
					{
						// If we were not set before, so we need to send an email?
						if (!triggered && Enabled && Email && cumulus.SmtpOptions.Enabled)
						{
							// Construct the message - preamble, plus values
							var msg = cumulus.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsg, Value, Units);
							if (!string.IsNullOrEmpty(LastError))
							{
								msg += "\r\nLast error: " + LastError;
							}
							cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
						}

						// If we get a new trigger, record the time
						triggered = true;
					}
				}
				else
				{
					// If the trigger is cleared, check if we should be latching the value
					if (Latch)
					{
						if (DateTime.Now > TriggeredTime.AddHours(LatchHours))
						{
							// We are latching, but the latch period has expired, clear the trigger
							triggered = false;
							triggerCount = 0;
						}
					}
					else
					{
						// No latch, just clear the trigger
						triggered = false;
						triggerCount = 0;
					}
				}
			}
		}
		public DateTime TriggeredTime { get; set; }
		public bool Notify { get; set; }
		public bool Email { get; set; }
		public bool Latch { get; set; }
		public int LatchHours { get; set; }
		public string EmailMsg { get; set; }
		public string Units { get; set; }
		public string LastError { get; set; }
		int triggerCount = 0;
		public int TriggerThreshold { get; set; }
	}

	public class AlarmChange : Alarm
	{
		//public bool changeUp { get; set; }
		//public bool changeDown { get; set; }

		bool upTriggered;
		public bool UpTriggered
		{
			get => upTriggered;
			set
			{
				if (value)
				{
					// If we were not set before, so we need to send an email?
					if (!upTriggered && Enabled && Email && cumulus.SmtpOptions.Enabled)
					{
						// Construct the message - preamble, plus values
						var msg = Program.cumulus.AlarmEmailPreamble + "\r\n" + string.Format(EmailMsgUp, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					// If we get a new trigger, record the time
					upTriggered = true;
					UpTriggeredTime = DateTime.Now;
				}
				else
				{
					// If the trigger is cleared, check if we should be latching the value
					if (Latch)
					{
						if (DateTime.Now > UpTriggeredTime.AddHours(LatchHours))
						{
							// We are latching, but the latch period has expired, clear the trigger
							upTriggered = false;
						}
					}
					else
					{
						// No latch, just clear the trigger
						upTriggered = false;
					}
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
				if (value)
				{
					// If we were not set before, so we need to send an email?
					if (!downTriggered && Enabled && Email && cumulus.SmtpOptions.Enabled)
					{
						// Construct the message - preamble, plus values
						var msg = Program.cumulus.AlarmEmailPreamble + "\n" + string.Format(EmailMsgDn, Value, Units);
						cumulus.emailer.SendEmail(cumulus.AlarmDestEmail, cumulus.AlarmFromEmail, cumulus.AlarmEmailSubject, msg, cumulus.AlarmEmailHtml);
					}

					// If we get a new trigger, record the time
					downTriggered = true;
					DownTriggeredTime = DateTime.Now;
				}
				else
				{
					// If the trigger is cleared, check if we should be latching the value
					if (Latch)
					{
						if (DateTime.Now > DownTriggeredTime.AddHours(LatchHours))
						{
							// We are latching, but the latch period has expired, clear the trigger
							downTriggered = false;
						}
					}
					else
					{
						// No latch, just clear the trigger
						downTriggered = false;
					}
				}
			}
		}

		public DateTime DownTriggeredTime { get; set; }

		public string EmailMsgUp { get; set; }
		public string EmailMsgDn { get; set; }
	}
}
