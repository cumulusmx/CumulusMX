using System;
using System.IO;
using System.Net;
using ServiceStack;
using EmbedIO;

namespace CumulusMX
{
	public class AlarmSettings
	{
		private readonly Cumulus cumulus;

		public AlarmSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlarmSettings()
		{
			//var InvC = new CultureInfo("");

			var alarmUnits = new JsonAlarmUnits()
			{
				tempUnits = cumulus.Units.TempText,
				pressUnits = cumulus.Units.PressText,
				rainUnits = cumulus.Units.RainText,
				windUnits = cumulus.Units.WindText
			};

			var data = new JsonAlarmSettingsData()
			{
				tempBelow = new JsonAlarmValues()
				{
					Enabled = cumulus.LowTempAlarm.Enabled,
					Val = cumulus.LowTempAlarm.Value,
					SoundEnabled = cumulus.LowTempAlarm.Sound,
					Sound = cumulus.LowTempAlarm.SoundFile,
					Notify = cumulus.LowTempAlarm.Notify,
					Email = cumulus.LowTempAlarm.Email,
					Latches = cumulus.LowTempAlarm.Latch,
					LatchHrs = cumulus.LowTempAlarm.LatchHours
				},
				tempAbove = new JsonAlarmValues()
				{
					Enabled = cumulus.HighTempAlarm.Enabled,
					Val = cumulus.HighTempAlarm.Value,
					SoundEnabled = cumulus.HighTempAlarm.Sound,
					Sound = cumulus.HighTempAlarm.SoundFile,
					Notify = cumulus.HighTempAlarm.Notify,
					Email = cumulus.HighTempAlarm.Email,
					Latches = cumulus.HighTempAlarm.Latch,
					LatchHrs = cumulus.HighTempAlarm.LatchHours
				},
				tempChange = new JsonAlarmValues()
				{
					Enabled = cumulus.TempChangeAlarm.Enabled,
					Val = cumulus.TempChangeAlarm.Value,
					SoundEnabled = cumulus.TempChangeAlarm.Sound,
					Sound = cumulus.TempChangeAlarm.SoundFile,
					Notify = cumulus.TempChangeAlarm.Notify,
					Email = cumulus.TempChangeAlarm.Email,
					Latches = cumulus.TempChangeAlarm.Latch,
					LatchHrs = cumulus.TempChangeAlarm.LatchHours
				},
				pressBelow = new JsonAlarmValues()
				{
					Enabled = cumulus.LowPressAlarm.Enabled,
					Val = cumulus.LowPressAlarm.Value,
					SoundEnabled = cumulus.LowPressAlarm.Sound,
					Sound = cumulus.LowPressAlarm.SoundFile,
					Notify = cumulus.LowPressAlarm.Notify,
					Email = cumulus.LowPressAlarm.Email,
					Latches = cumulus.LowPressAlarm.Latch,
					LatchHrs = cumulus.LowPressAlarm.LatchHours
				},
				pressAbove = new JsonAlarmValues()
				{
					Enabled = cumulus.HighPressAlarm.Enabled,
					Val = cumulus.HighPressAlarm.Value,
					SoundEnabled = cumulus.HighPressAlarm.Sound,
					Sound = cumulus.HighPressAlarm.SoundFile,
					Notify = cumulus.HighPressAlarm.Notify,
					Email = cumulus.HighPressAlarm.Email,
					Latches = cumulus.HighPressAlarm.Latch,
					LatchHrs = cumulus.HighPressAlarm.LatchHours
				},
				pressChange = new JsonAlarmValues()
				{
					Enabled = cumulus.PressChangeAlarm.Enabled,
					Val = cumulus.PressChangeAlarm.Value,
					SoundEnabled = cumulus.PressChangeAlarm.Sound,
					Sound = cumulus.PressChangeAlarm.SoundFile,
					Notify = cumulus.PressChangeAlarm.Notify,
					Email = cumulus.PressChangeAlarm.Email,
					Latches = cumulus.PressChangeAlarm.Latch,
					LatchHrs = cumulus.PressChangeAlarm.LatchHours
				},
				rainAbove = new JsonAlarmValues()
				{
					Enabled = cumulus.HighRainTodayAlarm.Enabled,
					Val = cumulus.HighRainTodayAlarm.Value,
					SoundEnabled = cumulus.HighRainTodayAlarm.Sound,
					Sound = cumulus.HighRainTodayAlarm.SoundFile,
					Notify = cumulus.HighRainTodayAlarm.Notify,
					Email = cumulus.HighRainTodayAlarm.Email,
					Latches = cumulus.HighRainTodayAlarm.Latch,
					LatchHrs = cumulus.HighRainTodayAlarm.LatchHours
				},
				rainRateAbove = new JsonAlarmValues()
				{
					Enabled = cumulus.HighRainRateAlarm.Enabled,
					Val = cumulus.HighRainRateAlarm.Value,
					SoundEnabled = cumulus.HighRainRateAlarm.Sound,
					Sound = cumulus.HighRainRateAlarm.SoundFile,
					Notify = cumulus.HighRainRateAlarm.Notify,
					Email = cumulus.HighRainRateAlarm.Email,
					Latches = cumulus.HighRainRateAlarm.Latch,
					LatchHrs = cumulus.HighRainRateAlarm.LatchHours
				},
				isRaining = new JsonAlarmValues()
				{
					Enabled = cumulus.IsRainingAlarm.Enabled,
					SoundEnabled = cumulus.IsRainingAlarm.Sound,
					Sound = cumulus.IsRainingAlarm.SoundFile,
					Notify = cumulus.IsRainingAlarm.Notify,
					Email = cumulus.IsRainingAlarm.Email,
					Latches = cumulus.IsRainingAlarm.Latch,
					LatchHrs = cumulus.IsRainingAlarm.LatchHours,
					Threshold = cumulus.IsRainingAlarm.TriggerThreshold
				},
				gustAbove = new JsonAlarmValues()
				{
					Enabled = cumulus.HighGustAlarm.Enabled,
					Val = cumulus.HighGustAlarm.Value,
					SoundEnabled = cumulus.HighGustAlarm.Sound,
					Sound = cumulus.HighGustAlarm.SoundFile,
					Notify = cumulus.HighGustAlarm.Notify,
					Email = cumulus.HighGustAlarm.Email,
					Latches = cumulus.HighGustAlarm.Latch,
					LatchHrs = cumulus.HighGustAlarm.LatchHours
				},
				windAbove = new JsonAlarmValues()
				{
					Enabled = cumulus.HighWindAlarm.Enabled,
					Val = cumulus.HighWindAlarm.Value,
					SoundEnabled = cumulus.HighWindAlarm.Sound,
					Sound = cumulus.HighWindAlarm.SoundFile,
					Notify = cumulus.HighWindAlarm.Notify,
					Email = cumulus.HighWindAlarm.Email,
					Latches = cumulus.HighWindAlarm.Latch,
					LatchHrs = cumulus.HighWindAlarm.LatchHours
				},
				contactLost = new JsonAlarmValues()
				{
					Enabled = cumulus.SensorAlarm.Enabled,
					SoundEnabled = cumulus.SensorAlarm.Sound,
					Sound = cumulus.SensorAlarm.SoundFile,
					Notify = cumulus.SensorAlarm.Notify,
					Email = cumulus.SensorAlarm.Email,
					Latches = cumulus.SensorAlarm.Latch,
					LatchHrs = cumulus.SensorAlarm.LatchHours,
					Threshold = cumulus.SensorAlarm.TriggerThreshold
				},
				dataStopped = new JsonAlarmValues()
				{
					Enabled = cumulus.DataStoppedAlarm.Enabled,
					SoundEnabled = cumulus.DataStoppedAlarm.Sound,
					Sound = cumulus.DataStoppedAlarm.SoundFile,
					Notify = cumulus.DataStoppedAlarm.Notify,
					Email = cumulus.DataStoppedAlarm.Email,
					Latches = cumulus.DataStoppedAlarm.Latch,
					LatchHrs = cumulus.DataStoppedAlarm.LatchHours,
					Threshold = cumulus.DataStoppedAlarm.TriggerThreshold
				},
				batteryLow = new JsonAlarmValues()
				{
					Enabled = cumulus.BatteryLowAlarm.Enabled,
					SoundEnabled = cumulus.BatteryLowAlarm.Sound,
					Sound = cumulus.BatteryLowAlarm.SoundFile,
					Notify = cumulus.BatteryLowAlarm.Notify,
					Email = cumulus.BatteryLowAlarm.Email,
					Latches = cumulus.BatteryLowAlarm.Latch,
					LatchHrs = cumulus.BatteryLowAlarm.LatchHours,
					Threshold = cumulus.BatteryLowAlarm.TriggerThreshold
				},
				spike = new JsonAlarmValues()
				{
					Enabled = cumulus.SpikeAlarm.Enabled,
					SoundEnabled = cumulus.SpikeAlarm.Sound,
					Sound = cumulus.SpikeAlarm.SoundFile,
					Notify = cumulus.SpikeAlarm.Notify,
					Email = cumulus.SpikeAlarm.Email,
					Latches = cumulus.SpikeAlarm.Latch,
					LatchHrs = cumulus.SpikeAlarm.LatchHours,
					Threshold = cumulus.SpikeAlarm.TriggerThreshold
				},
				upgrade = new JsonAlarmValues()
				{
					Enabled = cumulus.UpgradeAlarm.Enabled,
					SoundEnabled = cumulus.UpgradeAlarm.Sound,
					Sound = cumulus.UpgradeAlarm.SoundFile,
					Notify = cumulus.UpgradeAlarm.Notify,
					Email = cumulus.UpgradeAlarm.Email,
					Latches = cumulus.UpgradeAlarm.Latch,
					LatchHrs = cumulus.UpgradeAlarm.LatchHours
				},
				httpUpload = new JsonAlarmValues()
				{
					Enabled = cumulus.HttpUploadAlarm.Enabled,
					SoundEnabled = cumulus.HttpUploadAlarm.Sound,
					Sound = cumulus.HttpUploadAlarm.SoundFile,
					Notify = cumulus.HttpUploadAlarm.Notify,
					Email = cumulus.HttpUploadAlarm.Email,
					Latches = cumulus.HttpUploadAlarm.Latch,
					LatchHrs = cumulus.HttpUploadAlarm.LatchHours,
					Threshold = cumulus.HttpUploadAlarm.TriggerThreshold
				},
				mySqlUpload = new JsonAlarmValues()
				{
					Enabled = cumulus.MySqlUploadAlarm.Enabled,
					SoundEnabled = cumulus.MySqlUploadAlarm.Sound,
					Sound = cumulus.MySqlUploadAlarm.SoundFile,
					Notify = cumulus.MySqlUploadAlarm.Notify,
					Email = cumulus.MySqlUploadAlarm.Email,
					Latches = cumulus.MySqlUploadAlarm.Latch,
					LatchHrs = cumulus.MySqlUploadAlarm.LatchHours,
					Threshold = cumulus.MySqlUploadAlarm.TriggerThreshold
				}
			};

			var email = new JsonAlarmEmail()
			{
				fromEmail = cumulus.AlarmFromEmail,
				destEmail = cumulus.AlarmDestEmail.Join(";"),
				useHtml = cumulus.AlarmEmailHtml
			};

			var retObject = new JsonAlarmSettings()
			{
				data = data,
				units = alarmUnits,
				email = email
			};

			return retObject.ToJson();
		}

		public string UpdateAlarmSettings(IHttpContext context)
		{
			var json = "";
			JsonAlarmSettings result;
			JsonAlarmSettingsData settings;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data);

				// de-serialize it to the settings structure
				//var settings = JsonConvert.DeserializeObject<JsonAlarmSettingsData>(json);
				//var settings = JsonSerializer.DeserializeFromString<JsonAlarmSettingsData>(json);

				result = json.FromJson<JsonAlarmSettings>();
				settings = result.data;
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Alarm Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Alarm Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			try
			{
				// process the settings
				cumulus.LogMessage("Updating Alarm settings");

				var emailRequired = false;

				cumulus.LowTempAlarm.Enabled = settings.tempBelow.Enabled;
				cumulus.LowTempAlarm.Value = settings.tempBelow.Val;
				cumulus.LowTempAlarm.Sound = settings.tempBelow.SoundEnabled;
				cumulus.LowTempAlarm.SoundFile = settings.tempBelow.Sound;
				cumulus.LowTempAlarm.Notify = settings.tempBelow.Notify;
				cumulus.LowTempAlarm.Email = settings.tempBelow.Email;
				cumulus.LowTempAlarm.Latch = settings.tempBelow.Latches;
				cumulus.LowTempAlarm.LatchHours = settings.tempBelow.LatchHrs;
				emailRequired = cumulus.LowTempAlarm.Email && cumulus.LowTempAlarm.Enabled;

				cumulus.HighTempAlarm.Enabled = settings.tempAbove.Enabled;
				cumulus.HighTempAlarm.Value = settings.tempAbove.Val;
				cumulus.HighTempAlarm.Sound = settings.tempAbove.SoundEnabled;
				cumulus.HighTempAlarm.SoundFile = settings.tempAbove.Sound;
				cumulus.HighTempAlarm.Notify = settings.tempAbove.Notify;
				cumulus.HighTempAlarm.Email = settings.tempAbove.Email;
				cumulus.HighTempAlarm.Latch = settings.tempAbove.Latches;
				cumulus.HighTempAlarm.LatchHours = settings.tempAbove.LatchHrs;
				emailRequired = emailRequired || (cumulus.HighTempAlarm.Email && cumulus.HighTempAlarm.Enabled);

				cumulus.TempChangeAlarm.Enabled = settings.tempChange.Enabled;
				cumulus.TempChangeAlarm.Value = settings.tempChange.Val;
				cumulus.TempChangeAlarm.Sound = settings.tempChange.SoundEnabled;
				cumulus.TempChangeAlarm.SoundFile = settings.tempChange.Sound;
				cumulus.TempChangeAlarm.Notify = settings.tempChange.Notify;
				cumulus.TempChangeAlarm.Email = settings.tempChange.Email;
				cumulus.TempChangeAlarm.Latch = settings.tempChange.Latches;
				cumulus.TempChangeAlarm.LatchHours = settings.tempChange.LatchHrs;
				emailRequired = emailRequired || (cumulus.TempChangeAlarm.Email && cumulus.TempChangeAlarm.Enabled);

				cumulus.LowPressAlarm.Enabled = settings.pressBelow.Enabled;
				cumulus.LowPressAlarm.Value = settings.pressBelow.Val;
				cumulus.LowPressAlarm.Sound = settings.pressBelow.SoundEnabled;
				cumulus.LowPressAlarm.SoundFile = settings.pressBelow.Sound;
				cumulus.LowPressAlarm.Notify = settings.pressBelow.Notify;
				cumulus.LowPressAlarm.Email = settings.pressBelow.Email;
				cumulus.LowPressAlarm.Latch = settings.pressBelow.Latches;
				cumulus.LowPressAlarm.LatchHours = settings.pressBelow.LatchHrs;
				emailRequired = emailRequired || (cumulus.LowPressAlarm.Email && cumulus.LowPressAlarm.Enabled);

				cumulus.HighPressAlarm.Enabled = settings.pressAbove.Enabled;
				cumulus.HighPressAlarm.Value = settings.pressAbove.Val;
				cumulus.HighPressAlarm.Sound = settings.pressAbove.SoundEnabled;
				cumulus.HighPressAlarm.SoundFile = settings.pressAbove.Sound;
				cumulus.HighPressAlarm.Notify = settings.pressAbove.Notify;
				cumulus.HighPressAlarm.Email = settings.pressAbove.Email;
				cumulus.HighPressAlarm.Latch = settings.pressAbove.Latches;
				cumulus.HighPressAlarm.LatchHours = settings.pressAbove.LatchHrs;
				emailRequired = emailRequired || (cumulus.HighPressAlarm.Email && cumulus.HighPressAlarm.Enabled);

				cumulus.PressChangeAlarm.Enabled = settings.pressChange.Enabled;
				cumulus.PressChangeAlarm.Value = settings.pressChange.Val;
				cumulus.PressChangeAlarm.Sound = settings.pressChange.SoundEnabled;
				cumulus.PressChangeAlarm.SoundFile = settings.pressChange.Sound;
				cumulus.PressChangeAlarm.Notify = settings.pressChange.Notify;
				cumulus.PressChangeAlarm.Email = settings.pressChange.Email;
				cumulus.PressChangeAlarm.Latch = settings.pressChange.Latches;
				cumulus.PressChangeAlarm.LatchHours = settings.pressChange.LatchHrs;
				emailRequired = emailRequired || (cumulus.PressChangeAlarm.Email && cumulus.PressChangeAlarm.Enabled);

				cumulus.HighRainTodayAlarm.Enabled = settings.rainAbove.Enabled;
				cumulus.HighRainTodayAlarm.Value = settings.rainAbove.Val;
				cumulus.HighRainTodayAlarm.Sound = settings.rainAbove.SoundEnabled;
				cumulus.HighRainTodayAlarm.SoundFile = settings.rainAbove.Sound;
				cumulus.HighRainTodayAlarm.Notify = settings.rainAbove.Notify;
				cumulus.HighRainTodayAlarm.Email = settings.rainAbove.Email;
				cumulus.HighRainTodayAlarm.Latch = settings.rainAbove.Latches;
				cumulus.HighRainTodayAlarm.LatchHours = settings.rainAbove.LatchHrs;
				emailRequired = emailRequired || (cumulus.HighRainTodayAlarm.Email && cumulus.HighRainTodayAlarm.Enabled);

				cumulus.HighRainRateAlarm.Enabled = settings.rainRateAbove.Enabled;
				cumulus.HighRainRateAlarm.Value = settings.rainRateAbove.Val;
				cumulus.HighRainRateAlarm.Sound = settings.rainRateAbove.SoundEnabled;
				cumulus.HighRainRateAlarm.SoundFile = settings.rainRateAbove.Sound;
				cumulus.HighRainRateAlarm.Notify = settings.rainRateAbove.Notify;
				cumulus.HighRainRateAlarm.Email = settings.rainRateAbove.Email;
				cumulus.HighRainRateAlarm.Latch = settings.rainRateAbove.Latches;
				cumulus.HighRainRateAlarm.LatchHours = settings.rainRateAbove.LatchHrs;
				emailRequired = emailRequired || (cumulus.HighRainRateAlarm.Email && cumulus.HighRainRateAlarm.Enabled);

				cumulus.IsRainingAlarm.Enabled = settings.isRaining.Enabled;
				cumulus.IsRainingAlarm.Sound = settings.isRaining.SoundEnabled;
				cumulus.IsRainingAlarm.SoundFile = settings.isRaining.Sound;
				cumulus.IsRainingAlarm.Notify = settings.isRaining.Notify;
				cumulus.IsRainingAlarm.Email = settings.isRaining.Email;
				cumulus.IsRainingAlarm.Latch = settings.isRaining.Latches;
				cumulus.IsRainingAlarm.LatchHours = settings.isRaining.LatchHrs;
				emailRequired = emailRequired || (cumulus.IsRainingAlarm.Email && cumulus.IsRainingAlarm.Enabled);

				cumulus.HighGustAlarm.Enabled = settings.gustAbove.Enabled;
				cumulus.HighGustAlarm.Value = settings.gustAbove.Val;
				cumulus.HighGustAlarm.Sound = settings.gustAbove.SoundEnabled;
				cumulus.HighGustAlarm.SoundFile = settings.gustAbove.Sound;
				cumulus.HighGustAlarm.Notify = settings.gustAbove.Notify;
				cumulus.HighGustAlarm.Email = settings.gustAbove.Email;
				cumulus.HighGustAlarm.Latch = settings.gustAbove.Latches;
				cumulus.HighGustAlarm.LatchHours = settings.gustAbove.LatchHrs;
				emailRequired = emailRequired || (cumulus.HighGustAlarm.Email && cumulus.HighGustAlarm.Enabled);

				cumulus.HighWindAlarm.Enabled = settings.windAbove.Enabled;
				cumulus.HighWindAlarm.Value = settings.windAbove.Val;
				cumulus.HighWindAlarm.Sound = settings.windAbove.SoundEnabled;
				cumulus.HighWindAlarm.SoundFile = settings.windAbove.Sound;
				cumulus.HighWindAlarm.Notify = settings.windAbove.Notify;
				cumulus.HighWindAlarm.Email = settings.windAbove.Email;
				cumulus.HighWindAlarm.Latch = settings.windAbove.Latches;
				cumulus.HighWindAlarm.LatchHours = settings.windAbove.LatchHrs;
				emailRequired = emailRequired || (cumulus.HighWindAlarm.Email && cumulus.HighWindAlarm.Enabled);

				cumulus.SensorAlarm.Enabled = settings.contactLost.Enabled;
				cumulus.SensorAlarm.Sound = settings.contactLost.SoundEnabled;
				cumulus.SensorAlarm.SoundFile = settings.contactLost.Sound;
				cumulus.SensorAlarm.Notify = settings.contactLost.Notify;
				cumulus.SensorAlarm.Email = settings.contactLost.Email;
				cumulus.SensorAlarm.Latch = settings.contactLost.Latches;
				cumulus.SensorAlarm.LatchHours = settings.contactLost.LatchHrs;
				cumulus.SensorAlarm.TriggerThreshold = settings.contactLost.Threshold;
				emailRequired = emailRequired || (cumulus.SensorAlarm.Email && cumulus.SensorAlarm.Enabled);

				cumulus.DataStoppedAlarm.Enabled = settings.dataStopped.Enabled;
				cumulus.DataStoppedAlarm.Sound = settings.dataStopped.SoundEnabled;
				cumulus.DataStoppedAlarm.SoundFile = settings.dataStopped.Sound;
				cumulus.DataStoppedAlarm.Notify = settings.dataStopped.Notify;
				cumulus.DataStoppedAlarm.Email = settings.dataStopped.Email;
				cumulus.DataStoppedAlarm.Latch = settings.dataStopped.Latches;
				cumulus.DataStoppedAlarm.LatchHours = settings.dataStopped.LatchHrs;
				cumulus.DataStoppedAlarm.TriggerThreshold = settings.dataStopped.Threshold;
				emailRequired = emailRequired || (cumulus.DataStoppedAlarm.Email && cumulus.DataStoppedAlarm.Enabled);

				cumulus.BatteryLowAlarm.Enabled = settings.batteryLow.Enabled;
				cumulus.BatteryLowAlarm.Sound = settings.batteryLow.SoundEnabled;
				cumulus.BatteryLowAlarm.SoundFile = settings.batteryLow.Sound;
				cumulus.BatteryLowAlarm.Notify = settings.batteryLow.Notify;
				cumulus.BatteryLowAlarm.Email = settings.batteryLow.Email;
				cumulus.BatteryLowAlarm.Latch = settings.batteryLow.Latches;
				cumulus.BatteryLowAlarm.LatchHours = settings.batteryLow.LatchHrs;
				cumulus.BatteryLowAlarm.TriggerThreshold = settings.batteryLow.Threshold;
				emailRequired = emailRequired || (cumulus.BatteryLowAlarm.Email && cumulus.BatteryLowAlarm.Enabled);

				cumulus.SpikeAlarm.Enabled = settings.spike.Enabled;
				cumulus.SpikeAlarm.Sound = settings.spike.SoundEnabled;
				cumulus.SpikeAlarm.SoundFile = settings.spike.Sound;
				cumulus.SpikeAlarm.Notify = settings.spike.Notify;
				cumulus.SpikeAlarm.Email = settings.spike.Email;
				cumulus.SpikeAlarm.Latch = settings.spike.Latches;
				cumulus.SpikeAlarm.LatchHours = settings.spike.LatchHrs;
				cumulus.SpikeAlarm.TriggerThreshold = settings.spike.Threshold;
				emailRequired = emailRequired || (cumulus.SpikeAlarm.Email && cumulus.SpikeAlarm.Enabled);

				cumulus.UpgradeAlarm.Enabled = settings.upgrade.Enabled;
				cumulus.UpgradeAlarm.Sound = settings.upgrade.SoundEnabled;
				cumulus.UpgradeAlarm.SoundFile = settings.upgrade.Sound;
				cumulus.UpgradeAlarm.Notify = settings.upgrade.Notify;
				cumulus.UpgradeAlarm.Email = settings.upgrade.Email;
				cumulus.UpgradeAlarm.Latch = settings.upgrade.Latches;
				cumulus.UpgradeAlarm.LatchHours = settings.upgrade.LatchHrs;
				emailRequired = emailRequired || (cumulus.UpgradeAlarm.Email && cumulus.UpgradeAlarm.Enabled);

				cumulus.HttpUploadAlarm.Enabled = settings.httpUpload.Enabled;
				cumulus.HttpUploadAlarm.Sound = settings.httpUpload.SoundEnabled;
				cumulus.HttpUploadAlarm.SoundFile = settings.httpUpload.Sound;
				cumulus.HttpUploadAlarm.Notify = settings.httpUpload.Notify;
				cumulus.HttpUploadAlarm.Email = settings.httpUpload.Email;
				cumulus.HttpUploadAlarm.Latch = settings.httpUpload.Latches;
				cumulus.HttpUploadAlarm.LatchHours = settings.httpUpload.LatchHrs;
				cumulus.HttpUploadAlarm.TriggerThreshold = settings.httpUpload.Threshold;
				emailRequired = emailRequired || (cumulus.HttpUploadAlarm.Email && cumulus.HttpUploadAlarm.Enabled);

				cumulus.MySqlUploadAlarm.Enabled = settings.mySqlUpload.Enabled;
				cumulus.MySqlUploadAlarm.Sound = settings.mySqlUpload.SoundEnabled;
				cumulus.MySqlUploadAlarm.SoundFile = settings.mySqlUpload.Sound;
				cumulus.MySqlUploadAlarm.Notify = settings.mySqlUpload.Notify;
				cumulus.MySqlUploadAlarm.Email = settings.mySqlUpload.Email;
				cumulus.MySqlUploadAlarm.Latch = settings.mySqlUpload.Latches;
				cumulus.MySqlUploadAlarm.LatchHours = settings.mySqlUpload.LatchHrs;
				cumulus.MySqlUploadAlarm.TriggerThreshold = settings.mySqlUpload.Threshold;
				emailRequired = emailRequired || (cumulus.MySqlUploadAlarm.Email && cumulus.MySqlUploadAlarm.Enabled);

				// validate the from email
				if (emailRequired && !EmailSender.CheckEmailAddress(result.email.fromEmail.Trim()))
				{
					var msg = "ERROR: Alarm email option enabled and an invalid Alarm from email address entered";
					cumulus.LogMessage(msg);
					context.Response.StatusCode = 500;
					return msg;
				}
				else
				{
					cumulus.AlarmFromEmail = result.email.fromEmail.Trim();
				}

				// validate the destination email(s)
				var emails = result.email.destEmail.Trim().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				for (var i = 0; i < emails.Length; i++)
				{
					emails[i] = emails[i].Trim();
					if (!EmailSender.CheckEmailAddress(emails[i]))
					{
						var msg = "ERROR: Invalid Alarm destination email address entered";
						cumulus.LogMessage(msg);
						context.Response.StatusCode = 500;
						return msg;
					}
				}
				cumulus.AlarmDestEmail = emails;
				cumulus.AlarmEmailHtml = result.email.useHtml;

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error processing Alarm settings: " + ex.Message);
				cumulus.LogDebugMessage("Alarm Data: " + json);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return "success";
		}

		public string TestEmail(IHttpContext context)
		{
			try
			{

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data);

				var result = json.FromJson<JsonAlarmEmail>();
				// process the settings
				cumulus.LogMessage("Sending test email...");

				// validate the from email
				if (!EmailSender.CheckEmailAddress(result.fromEmail.Trim()))
				{
					var msg = "ERROR: Invalid Alarm from email address entered";
					cumulus.LogMessage(msg);
					context.Response.StatusCode = 500;
					return msg;
				}
				var from = result.fromEmail.Trim();

				// validate the destination email(s)
				var dest = result.destEmail.Trim().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				for (var i = 0; i < dest.Length; i++)
				{
					dest[i] = dest[i].Trim();
					if (!EmailSender.CheckEmailAddress(dest[i]))
					{
						var msg = "ERROR: Invalid Alarm destination email address entered";
						cumulus.LogMessage(msg);
						context.Response.StatusCode = 500;
						return msg;
					}
				}

				var ret = cumulus.emailer.SendTestEmail(dest, from, "Cumulus MX Test Email", "A test email from Cumulus MX.", result.useHtml);

				if (ret == "OK")
				{
					cumulus.LogMessage("Test email sent without error");
				}
				else
				{
					context.Response.StatusCode = 500;
					return ret;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}

			return "success";
		}
	}

	public class JsonAlarmSettingsData
	{
		public JsonAlarmValues tempBelow { get; set; }
		public JsonAlarmValues tempAbove { get; set; }
		public JsonAlarmValues tempChange { get; set; }
		public JsonAlarmValues pressBelow { get; set; }
		public JsonAlarmValues pressAbove { get; set; }
		public JsonAlarmValues pressChange { get; set; }
		public JsonAlarmValues rainAbove { get; set; }
		public JsonAlarmValues rainRateAbove { get; set; }
		public JsonAlarmValues gustAbove { get; set; }
		public JsonAlarmValues windAbove { get; set; }
		public JsonAlarmValues contactLost { get; set; }
		public JsonAlarmValues dataStopped { get; set; }
		public JsonAlarmValues batteryLow { get; set; }
		public JsonAlarmValues spike { get; set; }
		public JsonAlarmValues upgrade { get; set; }
		public JsonAlarmValues httpUpload { get; set; }
		public JsonAlarmValues mySqlUpload { get; set; }
		public JsonAlarmValues isRaining { get; set; }
	}

	public class JsonAlarmValues
	{
		public bool Enabled { get; set; }
		public double Val { get; set; }
		public bool SoundEnabled { get; set; }
		public string Sound { get; set; }
		public bool Notify { get; set; }
		public bool Email { get; set; }
		public bool Latches { get; set; }
		public int LatchHrs { get; set; }
		public int Threshold { get; set; }
	}

	public class JsonAlarmEmail
	{
		public string fromEmail { get; set; }
		public string destEmail { get; set; }
		public bool useHtml { get; set; }
	}

	public class JsonAlarmUnits
	{
		public string tempUnits { get; set; }
		public string pressUnits { get; set; }
		public string rainUnits { get; set; }
		public string windUnits { get; set; }
	}

	public class JsonAlarmSettings
	{
		public JsonAlarmSettingsData data { get; set; }
		public JsonAlarmUnits units { get; set; }
		public JsonAlarmEmail email { get; set; }
	}
}
