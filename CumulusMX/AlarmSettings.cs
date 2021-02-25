using System;
using System.IO;
using System.Net;
using ServiceStack;
using Unosquare.Labs.EmbedIO;

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
				tempBelowEnabled = cumulus.LowTempAlarm.Enabled,
				tempBelowVal = cumulus.LowTempAlarm.Value,
				tempBelowSoundEnabled = cumulus.LowTempAlarm.Sound,
				tempBelowSound = cumulus.LowTempAlarm.SoundFile,
				tempBelowNotify = cumulus.LowTempAlarm.Notify,
				tempBelowLatches = cumulus.LowTempAlarm.Latch,
				tempBelowLatchHrs = cumulus.LowTempAlarm.LatchHours,

				tempAboveEnabled = cumulus.HighTempAlarm.Enabled,
				tempAboveVal = cumulus.HighTempAlarm.Value,
				tempAboveSoundEnabled = cumulus.HighTempAlarm.Sound,
				tempAboveSound = cumulus.HighTempAlarm.SoundFile,
				tempAboveNotify = cumulus.HighTempAlarm.Notify,
				tempAboveLatches = cumulus.HighTempAlarm.Latch,
				tempAboveLatchHrs = cumulus.HighTempAlarm.LatchHours,

				tempChangeEnabled = cumulus.TempChangeAlarm.Enabled,
				tempChangeVal = cumulus.TempChangeAlarm.Value,
				tempChangeSoundEnabled = cumulus.TempChangeAlarm.Sound,
				tempChangeSound = cumulus.TempChangeAlarm.SoundFile,
				tempChangeNotify = cumulus.TempChangeAlarm.Notify,
				tempChangeLatches = cumulus.TempChangeAlarm.Latch,
				tempChangeLatchHrs = cumulus.TempChangeAlarm.LatchHours,

				pressBelowEnabled = cumulus.LowPressAlarm.Enabled,
				pressBelowVal = cumulus.LowPressAlarm.Value,
				pressBelowSoundEnabled = cumulus.LowPressAlarm.Sound,
				pressBelowSound = cumulus.LowPressAlarm.SoundFile,
				pressBelowNotify = cumulus.LowPressAlarm.Notify,
				pressBelowLatches = cumulus.LowPressAlarm.Latch,
				pressBelowLatchHrs = cumulus.LowPressAlarm.LatchHours,

				pressAboveEnabled = cumulus.HighPressAlarm.Enabled,
				pressAboveVal = cumulus.HighPressAlarm.Value,
				pressAboveSoundEnabled = cumulus.HighPressAlarm.Sound,
				pressAboveSound = cumulus.HighPressAlarm.SoundFile,
				pressAboveNotify = cumulus.HighPressAlarm.Notify,
				pressAboveLatches = cumulus.HighPressAlarm.Latch,
				pressAboveLatchHrs = cumulus.HighPressAlarm.LatchHours,

				pressChangeEnabled = cumulus.PressChangeAlarm.Enabled,
				pressChangeVal = cumulus.PressChangeAlarm.Value,
				pressChangeSoundEnabled = cumulus.PressChangeAlarm.Sound,
				pressChangeSound = cumulus.PressChangeAlarm.SoundFile,
				pressChangeNotify = cumulus.PressChangeAlarm.Notify,
				pressChangeLatches = cumulus.PressChangeAlarm.Latch,
				pressChangeLatchHrs = cumulus.PressChangeAlarm.LatchHours,

				rainAboveEnabled = cumulus.HighRainTodayAlarm.Enabled,
				rainAboveVal = cumulus.HighRainTodayAlarm.Value,
				rainAboveSoundEnabled = cumulus.HighRainTodayAlarm.Sound,
				rainAboveSound = cumulus.HighRainTodayAlarm.SoundFile,
				rainAboveNotify = cumulus.HighRainTodayAlarm.Notify,
				rainAboveLatches = cumulus.HighRainTodayAlarm.Latch,
				rainAboveLatchHrs = cumulus.HighRainTodayAlarm.LatchHours,

				rainRateAboveEnabled = cumulus.HighRainRateAlarm.Enabled,
				rainRateAboveVal = cumulus.HighRainRateAlarm.Value,
				rainRateAboveSoundEnabled = cumulus.HighRainRateAlarm.Sound,
				rainRateAboveSound = cumulus.HighRainRateAlarm.SoundFile,
				rainRateAboveNotify = cumulus.HighRainRateAlarm.Notify,
				rainRateAboveLatches = cumulus.HighRainRateAlarm.Latch,
				rainRateAboveLatchHrs = cumulus.HighRainRateAlarm.LatchHours,

				gustAboveEnabled = cumulus.HighGustAlarm.Enabled,
				gustAboveVal = cumulus.HighGustAlarm.Value,
				gustAboveSoundEnabled = cumulus.HighGustAlarm.Sound,
				gustAboveSound = cumulus.HighGustAlarm.SoundFile,
				gustAboveNotify = cumulus.HighGustAlarm.Notify,
				gustAboveLatches = cumulus.HighGustAlarm.Latch,
				gustAboveLatchHrs = cumulus.HighGustAlarm.LatchHours,

				windAboveEnabled = cumulus.HighWindAlarm.Enabled,
				windAboveVal = cumulus.HighWindAlarm.Value,
				windAboveSoundEnabled = cumulus.HighWindAlarm.Sound,
				windAboveSound = cumulus.HighWindAlarm.SoundFile,
				windAboveNotify = cumulus.HighWindAlarm.Notify,
				windAboveLatches = cumulus.HighWindAlarm.Latch,
				windAboveLatchHrs = cumulus.HighWindAlarm.LatchHours,

				contactLostEnabled = cumulus.SensorAlarm.Enabled,
				contactLostSoundEnabled = cumulus.SensorAlarm.Sound,
				contactLostSound = cumulus.SensorAlarm.SoundFile,
				contactLostNotify = cumulus.SensorAlarm.Notify,
				contactLostLatches = cumulus.SensorAlarm.Latch,
				contactLostLatchHrs = cumulus.SensorAlarm.LatchHours,

				dataStoppedEnabled = cumulus.DataStoppedAlarm.Enabled,
				dataStoppedSoundEnabled = cumulus.DataStoppedAlarm.Sound,
				dataStoppedSound = cumulus.DataStoppedAlarm.SoundFile,
				dataStoppedNotify = cumulus.DataStoppedAlarm.Notify,
				dataStoppedLatches = cumulus.DataStoppedAlarm.Latch,
				dataStoppedLatchHrs = cumulus.DataStoppedAlarm.LatchHours,

				batteryLowEnabled = cumulus.BatteryLowAlarm.Enabled,
				batteryLowSoundEnabled = cumulus.BatteryLowAlarm.Sound,
				batteryLowSound = cumulus.BatteryLowAlarm.SoundFile,
				batteryLowNotify = cumulus.BatteryLowAlarm.Notify,
				batteryLowLatches = cumulus.BatteryLowAlarm.Latch,
				batteryLowLatchHrs = cumulus.BatteryLowAlarm.LatchHours,

				spikeEnabled = cumulus.SpikeAlarm.Enabled,
				spikeSoundEnabled = cumulus.SpikeAlarm.Sound,
				spikeSound = cumulus.SpikeAlarm.SoundFile,
				spikeNotify = cumulus.SpikeAlarm.Notify,
				spikeLatches = cumulus.SpikeAlarm.Latch,
				spikeLatchHrs = cumulus.SpikeAlarm.LatchHours,

				upgradeEnabled = cumulus.UpgradeAlarm.Enabled,
				upgradeSoundEnabled = cumulus.UpgradeAlarm.Sound,
				upgradeSound = cumulus.UpgradeAlarm.SoundFile,
				upgradeNotify = cumulus.UpgradeAlarm.Notify,
				upgradeLatches = cumulus.UpgradeAlarm.Latch,
				upgradeLatchHrs = cumulus.UpgradeAlarm.LatchHours,
			};

			var retObject = new JsonAlarmSettings()
			{
				data = data,
				units = alarmUnits
			};

			return retObject.ToJson();
		}

		public string UpdateAlarmSettings(IHttpContext context)
		{
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data);

				// de-serialize it to the settings structure
				//var settings = JsonConvert.DeserializeObject<JsonAlarmSettingsData>(json);
				//var settings = JsonSerializer.DeserializeFromString<JsonAlarmSettingsData>(json);

				var settings = json.FromJson<JsonAlarmSettingsData>();
				// process the settings
				cumulus.LogMessage("Updating Alarm settings");

				cumulus.LowTempAlarm.Enabled = settings.tempBelowEnabled;
				cumulus.LowTempAlarm.Value = settings.tempBelowVal;
				cumulus.LowTempAlarm.Sound = settings.tempBelowSoundEnabled;
				cumulus.LowTempAlarm.SoundFile = settings.tempBelowSound;
				cumulus.LowTempAlarm.Notify = settings.tempBelowNotify;
				cumulus.LowTempAlarm.Latch = settings.tempBelowLatches;
				cumulus.LowTempAlarm.LatchHours = settings.tempBelowLatchHrs;


				cumulus.HighTempAlarm.Enabled = settings.tempAboveEnabled;
				cumulus.HighTempAlarm.Value = settings.tempAboveVal;
				cumulus.HighTempAlarm.Sound = settings.tempAboveSoundEnabled;
				cumulus.HighTempAlarm.SoundFile = settings.tempAboveSound;
				cumulus.HighTempAlarm.Notify = settings.tempAboveNotify;
				cumulus.HighTempAlarm.Latch = settings.tempAboveLatches;
				cumulus.HighTempAlarm.LatchHours = settings.tempAboveLatchHrs;

				cumulus.TempChangeAlarm.Enabled = settings.tempChangeEnabled;
				cumulus.TempChangeAlarm.Value = settings.tempChangeVal;
				cumulus.TempChangeAlarm.Sound = settings.tempChangeSoundEnabled;
				cumulus.TempChangeAlarm.SoundFile = settings.tempChangeSound;
				cumulus.TempChangeAlarm.Notify = settings.tempChangeNotify;
				cumulus.TempChangeAlarm.Latch = settings.tempChangeLatches;
				cumulus.TempChangeAlarm.LatchHours = settings.tempChangeLatchHrs;

				cumulus.LowPressAlarm.Enabled = settings.pressBelowEnabled;
				cumulus.LowPressAlarm.Value = settings.pressBelowVal;
				cumulus.LowPressAlarm.Sound = settings.pressBelowSoundEnabled;
				cumulus.LowPressAlarm.SoundFile = settings.pressBelowSound;
				cumulus.LowPressAlarm.Notify = settings.pressBelowNotify;
				cumulus.LowPressAlarm.Latch = settings.pressBelowLatches;
				cumulus.LowPressAlarm.LatchHours = settings.pressBelowLatchHrs;

				cumulus.HighPressAlarm.Enabled = settings.pressAboveEnabled;
				cumulus.HighPressAlarm.Value = settings.pressAboveVal;
				cumulus.HighPressAlarm.Sound = settings.pressAboveSoundEnabled;
				cumulus.HighPressAlarm.SoundFile = settings.pressAboveSound;
				cumulus.HighPressAlarm.Notify = settings.pressAboveNotify;
				cumulus.HighPressAlarm.Latch = settings.pressAboveLatches;
				cumulus.HighPressAlarm.LatchHours = settings.pressAboveLatchHrs;

				cumulus.PressChangeAlarm.Enabled = settings.pressChangeEnabled;
				cumulus.PressChangeAlarm.Value = settings.pressChangeVal;
				cumulus.PressChangeAlarm.Sound = settings.pressChangeSoundEnabled;
				cumulus.PressChangeAlarm.SoundFile = settings.pressChangeSound;
				cumulus.PressChangeAlarm.Notify = settings.pressChangeNotify;
				cumulus.PressChangeAlarm.Latch = settings.pressChangeLatches;
				cumulus.PressChangeAlarm.LatchHours = settings.pressChangeLatchHrs;

				cumulus.HighRainTodayAlarm.Enabled = settings.rainAboveEnabled;
				cumulus.HighRainTodayAlarm.Value = settings.rainAboveVal;
				cumulus.HighRainTodayAlarm.Sound = settings.rainAboveSoundEnabled;
				cumulus.HighRainTodayAlarm.SoundFile = settings.rainAboveSound;
				cumulus.HighRainTodayAlarm.Notify = settings.rainAboveNotify;
				cumulus.HighRainTodayAlarm.Latch = settings.rainAboveLatches;
				cumulus.HighRainTodayAlarm.LatchHours = settings.rainAboveLatchHrs;

				cumulus.HighRainRateAlarm.Enabled = settings.rainRateAboveEnabled;
				cumulus.HighRainRateAlarm.Value = settings.rainRateAboveVal;
				cumulus.HighRainRateAlarm.Sound = settings.rainRateAboveSoundEnabled;
				cumulus.HighRainRateAlarm.SoundFile = settings.rainRateAboveSound;
				cumulus.HighRainRateAlarm.Notify = settings.rainRateAboveNotify;
				cumulus.HighRainRateAlarm.Latch = settings.rainRateAboveLatches;
				cumulus.HighRainRateAlarm.LatchHours = settings.rainRateAboveLatchHrs;

				cumulus.HighGustAlarm.Enabled = settings.gustAboveEnabled;
				cumulus.HighGustAlarm.Value = settings.gustAboveVal;
				cumulus.HighGustAlarm.Sound = settings.gustAboveSoundEnabled;
				cumulus.HighGustAlarm.SoundFile = settings.gustAboveSound;
				cumulus.HighGustAlarm.Notify = settings.gustAboveNotify;
				cumulus.HighGustAlarm.Latch = settings.gustAboveLatches;
				cumulus.HighGustAlarm.LatchHours = settings.gustAboveLatchHrs;

				cumulus.HighWindAlarm.Enabled = settings.windAboveEnabled;
				cumulus.HighWindAlarm.Value = settings.windAboveVal;
				cumulus.HighWindAlarm.Sound = settings.windAboveSoundEnabled;
				cumulus.HighWindAlarm.SoundFile = settings.windAboveSound;
				cumulus.HighWindAlarm.Notify = settings.windAboveNotify;
				cumulus.HighWindAlarm.Latch = settings.windAboveLatches;
				cumulus.HighWindAlarm.LatchHours = settings.windAboveLatchHrs;

				cumulus.SensorAlarm.Enabled = settings.contactLostEnabled;
				cumulus.SensorAlarm.Sound = settings.contactLostSoundEnabled;
				cumulus.SensorAlarm.SoundFile = settings.contactLostSound;
				cumulus.SensorAlarm.Notify = settings.contactLostNotify;
				cumulus.SensorAlarm.Latch = settings.contactLostLatches;
				cumulus.SensorAlarm.LatchHours = settings.contactLostLatchHrs;

				cumulus.DataStoppedAlarm.Enabled = settings.dataStoppedEnabled;
				cumulus.DataStoppedAlarm.Sound = settings.dataStoppedSoundEnabled;
				cumulus.DataStoppedAlarm.SoundFile = settings.dataStoppedSound;
				cumulus.DataStoppedAlarm.Notify = settings.dataStoppedNotify;
				cumulus.DataStoppedAlarm.Latch = settings.dataStoppedLatches;
				cumulus.DataStoppedAlarm.LatchHours = settings.dataStoppedLatchHrs;

				cumulus.BatteryLowAlarm.Enabled = settings.batteryLowEnabled;
				cumulus.BatteryLowAlarm.Sound = settings.batteryLowSoundEnabled;
				cumulus.BatteryLowAlarm.SoundFile = settings.batteryLowSound;
				cumulus.BatteryLowAlarm.Notify = settings.batteryLowNotify;
				cumulus.BatteryLowAlarm.Latch = settings.batteryLowLatches;
				cumulus.BatteryLowAlarm.LatchHours = settings.batteryLowLatchHrs;

				cumulus.SpikeAlarm.Enabled = settings.spikeEnabled;
				cumulus.SpikeAlarm.Sound = settings.spikeSoundEnabled;
				cumulus.SpikeAlarm.SoundFile = settings.spikeSound;
				cumulus.SpikeAlarm.Notify = settings.spikeNotify;
				cumulus.SpikeAlarm.Latch = settings.spikeLatches;
				cumulus.SpikeAlarm.LatchHours = settings.spikeLatchHrs;

				cumulus.UpgradeAlarm.Enabled = settings.upgradeEnabled;
				cumulus.UpgradeAlarm.Sound = settings.upgradeSoundEnabled;
				cumulus.UpgradeAlarm.SoundFile = settings.upgradeSound;
				cumulus.UpgradeAlarm.Notify = settings.upgradeNotify;
				cumulus.UpgradeAlarm.Latch = settings.upgradeLatches;
				cumulus.UpgradeAlarm.LatchHours = settings.upgradeLatchHrs;

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
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
		public bool tempBelowEnabled { get; set; }
		public double tempBelowVal { get; set; }
		public bool tempBelowSoundEnabled { get; set; }
		public string tempBelowSound { get; set; }
		public bool tempBelowNotify { get; set; }
		public bool tempBelowLatches { get; set; }
		public int tempBelowLatchHrs { get; set; }

		public bool tempAboveEnabled { get; set; }
		public double tempAboveVal { get; set; }
		public bool tempAboveSoundEnabled { get; set; }
		public string tempAboveSound { get; set; }
		public bool tempAboveNotify { get; set; }
		public bool tempAboveLatches { get; set; }
		public int tempAboveLatchHrs { get; set; }

		public bool tempChangeEnabled { get; set; }
		public double tempChangeVal { get; set; }
		public bool tempChangeSoundEnabled { get; set; }
		public string tempChangeSound { get; set; }
		public bool tempChangeNotify{ get; set; }
		public bool tempChangeLatches { get; set; }
		public int tempChangeLatchHrs { get; set; }

		public bool pressBelowEnabled { get; set; }
		public double pressBelowVal { get; set; }
		public bool pressBelowSoundEnabled { get; set; }
		public string pressBelowSound { get; set; }
		public bool pressBelowNotify { get; set; }
		public bool pressBelowLatches { get; set; }
		public int pressBelowLatchHrs { get; set; }

		public bool pressAboveEnabled { get; set; }
		public double pressAboveVal { get; set; }
		public bool pressAboveSoundEnabled { get; set; }
		public string pressAboveSound { get; set; }
		public bool pressAboveNotify { get; set; }
		public bool pressAboveLatches { get; set; }
		public int pressAboveLatchHrs { get; set; }

		public bool pressChangeEnabled { get; set; }
		public double pressChangeVal { get; set; }
		public bool pressChangeSoundEnabled { get; set; }
		public string pressChangeSound { get; set; }
		public bool pressChangeNotify { get; set; }
		public bool pressChangeLatches { get; set; }
		public int pressChangeLatchHrs { get; set; }

		public bool rainAboveEnabled { get; set; }
		public double rainAboveVal { get; set; }
		public bool rainAboveSoundEnabled { get; set; }
		public string rainAboveSound { get; set; }
		public bool rainAboveNotify { get; set; }
		public bool rainAboveLatches { get; set; }
		public int rainAboveLatchHrs { get; set; }

		public bool rainRateAboveEnabled { get; set; }
		public double rainRateAboveVal { get; set; }
		public bool rainRateAboveSoundEnabled { get; set; }
		public string rainRateAboveSound { get; set; }
		public bool rainRateAboveNotify { get; set; }
		public bool rainRateAboveLatches { get; set; }
		public int rainRateAboveLatchHrs { get; set; }

		public bool gustAboveEnabled { get; set; }
		public double gustAboveVal { get; set; }
		public bool gustAboveSoundEnabled { get; set; }
		public string gustAboveSound { get; set; }
		public bool gustAboveNotify { get; set; }
		public bool gustAboveLatches { get; set; }
		public int gustAboveLatchHrs { get; set; }

		public bool windAboveEnabled { get; set; }
		public double windAboveVal { get; set; }
		public bool windAboveSoundEnabled { get; set; }
		public string windAboveSound { get; set; }
		public bool windAboveNotify { get; set; }
		public bool windAboveLatches { get; set; }
		public int windAboveLatchHrs { get; set; }

		public bool contactLostEnabled { get; set; }
		public bool contactLostSoundEnabled { get; set; }
		public string contactLostSound { get; set; }
		public bool contactLostNotify { get; set; }
		public bool contactLostLatches { get; set; }
		public int contactLostLatchHrs { get; set; }

		public bool dataStoppedEnabled { get; set; }
		public bool dataStoppedSoundEnabled { get; set; }
		public string dataStoppedSound { get; set; }
		public bool dataStoppedNotify { get; set; }
		public bool dataStoppedLatches { get; set; }
		public int dataStoppedLatchHrs { get; set; }

		public bool batteryLowEnabled { get; set; }
		public bool batteryLowSoundEnabled { get; set; }
		public string batteryLowSound { get; set; }
		public bool batteryLowNotify { get; set; }
		public bool batteryLowLatches { get; set; }
		public int batteryLowLatchHrs { get; set; }

		public bool spikeEnabled { get; set; }
		public bool spikeSoundEnabled { get; set; }
		public string spikeSound { get; set; }
		public bool spikeNotify { get; set; }
		public bool spikeLatches { get; set; }
		public int spikeLatchHrs { get; set; }

		public bool upgradeEnabled { get; set; }
		public bool upgradeSoundEnabled { get; set; }
		public string upgradeSound { get; set; }
		public bool upgradeNotify { get; set; }
		public bool upgradeLatches { get; set; }
		public int upgradeLatchHrs { get; set; }

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
	}
}
