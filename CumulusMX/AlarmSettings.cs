using System;
using System.Globalization;
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
				tempUnits = cumulus.TempUnitText,
				pressUnits = cumulus.PressUnitText,
				rainUnits = cumulus.RainUnitText,
				windUnits = cumulus.WindUnitText
			};

			var data = new JsonAlarmSettingsData()
			{
				tempBelowEnabled = cumulus.LowTempAlarm.enabled,
				tempBelowVal = cumulus.LowTempAlarm.value,
				tempBelowSoundEnabled = cumulus.LowTempAlarm.sound,
				tempBelowSound = cumulus.LowTempAlarm.soundFile,
				tempBelowLatches = cumulus.LowTempAlarm.latch,
				tempBelowLatchHrs = cumulus.LowTempAlarm.latchHours,

				tempAboveEnabled = cumulus.HighTempAlarm.enabled,
				tempAboveVal = cumulus.HighTempAlarm.value,
				tempAboveSoundEnabled = cumulus.HighTempAlarm.sound,
				tempAboveSound = cumulus.HighTempAlarm.soundFile,
				tempAboveLatches = cumulus.HighTempAlarm.latch,
				tempAboveLatchHrs = cumulus.HighTempAlarm.latchHours,

				tempChangeEnabled = cumulus.TempChangeAlarm.enabled,
				tempChangeVal = cumulus.TempChangeAlarm.value,
				tempChangeSoundEnabled = cumulus.TempChangeAlarm.sound,
				tempChangeSound = cumulus.TempChangeAlarm.soundFile,
				tempChangeLatches = cumulus.TempChangeAlarm.latch,
				tempChangeLatchHrs = cumulus.TempChangeAlarm.latchHours,

				pressBelowEnabled = cumulus.LowPressAlarm.enabled,
				pressBelowVal = cumulus.LowPressAlarm.value,
				pressBelowSoundEnabled = cumulus.LowPressAlarm.sound,
				pressBelowSound = cumulus.LowPressAlarm.soundFile,
				pressBelowLatches = cumulus.LowPressAlarm.latch,
				pressBelowLatchHrs = cumulus.LowPressAlarm.latchHours,

				pressAboveEnabled = cumulus.HighPressAlarm.enabled,
				pressAboveVal = cumulus.HighPressAlarm.value,
				pressAboveSoundEnabled = cumulus.HighPressAlarm.sound,
				pressAboveSound = cumulus.HighPressAlarm.soundFile,
				pressAboveLatches = cumulus.HighPressAlarm.latch,
				pressAboveLatchHrs = cumulus.HighPressAlarm.latchHours,

				pressChangeEnabled = cumulus.PressChangeAlarm.enabled,
				pressChangeVal = cumulus.PressChangeAlarm.value,
				pressChangeSoundEnabled = cumulus.PressChangeAlarm.sound,
				pressChangeSound = cumulus.PressChangeAlarm.soundFile,
				pressChangeLatches = cumulus.PressChangeAlarm.latch,
				pressChangeLatchHrs = cumulus.PressChangeAlarm.latchHours,

				rainAboveEnabled = cumulus.HighRainTodayAlarm.enabled,
				rainAboveVal = cumulus.HighRainTodayAlarm.value,
				rainAboveSoundEnabled = cumulus.HighRainTodayAlarm.sound,
				rainAboveSound = cumulus.HighRainTodayAlarm.soundFile,
				rainAboveLatches = cumulus.HighRainTodayAlarm.latch,
				rainAboveLatchHrs = cumulus.HighRainTodayAlarm.latchHours,

				rainRateAboveEnabled = cumulus.HighRainRateAlarm.enabled,
				rainRateAboveVal = cumulus.HighRainRateAlarm.value,
				rainRateAboveSoundEnabled = cumulus.HighRainRateAlarm.sound,
				rainRateAboveSound = cumulus.HighRainRateAlarm.soundFile,
				rainRateAboveLatches = cumulus.HighRainRateAlarm.latch,
				rainRateAboveLatchHrs = cumulus.HighRainRateAlarm.latchHours,

				gustAboveEnabled = cumulus.HighGustAlarm.enabled,
				gustAboveVal = cumulus.HighGustAlarm.value,
				gustAboveSoundEnabled = cumulus.HighGustAlarm.sound,
				gustAboveSound = cumulus.HighGustAlarm.soundFile,
				gustAboveLatches = cumulus.HighGustAlarm.latch,
				gustAboveLatchHrs = cumulus.HighGustAlarm.latchHours,

				windAboveEnabled = cumulus.HighWindAlarm.enabled,
				windAboveVal = cumulus.HighWindAlarm.value,
				windAboveSoundEnabled = cumulus.HighWindAlarm.sound,
				windAboveSound = cumulus.HighWindAlarm.soundFile,
				windAboveLatches = cumulus.HighWindAlarm.latch,
				windAboveLatchHrs = cumulus.HighWindAlarm.latchHours,

				contactLostEnabled = cumulus.SensorAlarm.enabled,
				contactLostSoundEnabled = cumulus.SensorAlarm.sound,
				contactLostSound = cumulus.SensorAlarm.soundFile,
				contactLostLatches = cumulus.SensorAlarm.latch,
				contactLostLatchHrs = cumulus.SensorAlarm.latchHours,

				dataStoppedEnabled = cumulus.DataStoppedAlarm.enabled,
				dataStoppedSoundEnabled = cumulus.DataStoppedAlarm.sound,
				dataStoppedSound = cumulus.DataStoppedAlarm.soundFile,
				dataStoppedLatches = cumulus.DataStoppedAlarm.latch,
				dataStoppedLatchHrs = cumulus.DataStoppedAlarm.latchHours,

				batteryLowEnabled = cumulus.BatteryLowAlarm.enabled,
				batteryLowSoundEnabled = cumulus.BatteryLowAlarm.sound,
				batteryLowSound = cumulus.BatteryLowAlarm.soundFile,
				batteryLowLatches = cumulus.BatteryLowAlarm.latch,
				batteryLowLatchHrs = cumulus.BatteryLowAlarm.latchHours,

				spikeEnabled = cumulus.SpikeAlarm.enabled,
				spikeSoundEnabled = cumulus.SpikeAlarm.sound,
				spikeSound = cumulus.SpikeAlarm.soundFile,
				spikeLatches = cumulus.SpikeAlarm.latch,
				spikeLatchHrs = cumulus.SpikeAlarm.latchHours
			};

			var retObject = new JsonAlarmSettings()
			{
				data = data,
				units = alarmUnits
			};

			return retObject.ToJson();
		}

		//public string UpdateNoaaConfig(HttpListenerContext context)
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
				var InvC = new CultureInfo("");

				cumulus.LowTempAlarm.enabled = settings.tempBelowEnabled;
				cumulus.LowTempAlarm.value = settings.tempBelowVal;
				cumulus.LowTempAlarm.sound = settings.tempBelowSoundEnabled;
				cumulus.LowTempAlarm.soundFile = settings.tempBelowSound;
				cumulus.LowTempAlarm.latch = settings.tempBelowLatches;
				cumulus.LowTempAlarm.latchHours = settings.tempBelowLatchHrs;


				cumulus.HighTempAlarm.enabled = settings.tempAboveEnabled;
				cumulus.HighTempAlarm.value = settings.tempAboveVal;
				cumulus.HighTempAlarm.sound = settings.tempAboveSoundEnabled;
				cumulus.HighTempAlarm.soundFile = settings.tempAboveSound;
				cumulus.HighTempAlarm.latch = settings.tempAboveLatches;
				cumulus.HighTempAlarm.latchHours = settings.tempAboveLatchHrs;

				cumulus.TempChangeAlarm.enabled = settings.tempChangeEnabled;
				cumulus.TempChangeAlarm.value = settings.tempChangeVal;
				cumulus.TempChangeAlarm.sound = settings.tempChangeSoundEnabled;
				cumulus.TempChangeAlarm.soundFile = settings.tempChangeSound;
				cumulus.TempChangeAlarm.latch = settings.tempChangeLatches;
				cumulus.TempChangeAlarm.latchHours = settings.tempChangeLatchHrs;

				cumulus.LowPressAlarm.enabled = settings.pressBelowEnabled;
				cumulus.LowPressAlarm.value = settings.pressBelowVal;
				cumulus.LowPressAlarm.sound = settings.pressBelowSoundEnabled;
				cumulus.LowPressAlarm.soundFile = settings.pressBelowSound;
				cumulus.LowPressAlarm.latch = settings.pressBelowLatches;
				cumulus.LowPressAlarm.latchHours = settings.pressBelowLatchHrs;

				cumulus.HighPressAlarm.enabled = settings.pressAboveEnabled;
				cumulus.HighPressAlarm.value = settings.pressAboveVal;
				cumulus.HighPressAlarm.sound = settings.pressAboveSoundEnabled;
				cumulus.HighPressAlarm.soundFile = settings.pressAboveSound;
				cumulus.HighPressAlarm.latch = settings.pressAboveLatches;
				cumulus.HighPressAlarm.latchHours = settings.pressAboveLatchHrs;

				cumulus.PressChangeAlarm.enabled = settings.pressChangeEnabled;
				cumulus.PressChangeAlarm.value = settings.pressChangeVal;
				cumulus.PressChangeAlarm.sound = settings.pressChangeSoundEnabled;
				cumulus.PressChangeAlarm.soundFile = settings.pressChangeSound;
				cumulus.PressChangeAlarm.latch = settings.pressChangeLatches;
				cumulus.PressChangeAlarm.latchHours = settings.pressChangeLatchHrs;

				cumulus.HighRainTodayAlarm.enabled = settings.rainAboveEnabled;
				cumulus.HighRainTodayAlarm.value = settings.rainAboveVal;
				cumulus.HighRainTodayAlarm.sound = settings.rainAboveSoundEnabled;
				cumulus.HighRainTodayAlarm.soundFile = settings.rainAboveSound;
				cumulus.HighRainTodayAlarm.latch = settings.rainAboveLatches;
				cumulus.HighRainTodayAlarm.latchHours = settings.rainAboveLatchHrs;

				cumulus.HighRainRateAlarm.enabled = settings.rainRateAboveEnabled;
				cumulus.HighRainRateAlarm.value = settings.rainRateAboveVal;
				cumulus.HighRainRateAlarm.sound = settings.rainRateAboveSoundEnabled;
				cumulus.HighRainRateAlarm.soundFile = settings.rainRateAboveSound;
				cumulus.HighRainRateAlarm.latch = settings.rainRateAboveLatches;
				cumulus.HighRainRateAlarm.latchHours = settings.rainRateAboveLatchHrs;

				cumulus.HighGustAlarm.enabled = settings.gustAboveEnabled;
				cumulus.HighGustAlarm.value = settings.gustAboveVal;
				cumulus.HighGustAlarm.sound = settings.gustAboveSoundEnabled;
				cumulus.HighGustAlarm.soundFile = settings.gustAboveSound;
				cumulus.HighGustAlarm.latch = settings.gustAboveLatches;
				cumulus.HighGustAlarm.latchHours = settings.gustAboveLatchHrs;

				cumulus.HighWindAlarm.enabled = settings.windAboveEnabled;
				cumulus.HighWindAlarm.value = settings.windAboveVal;
				cumulus.HighWindAlarm.sound = settings.windAboveSoundEnabled;
				cumulus.HighWindAlarm.soundFile = settings.windAboveSound;
				cumulus.HighWindAlarm.latch = settings.windAboveLatches;
				cumulus.HighWindAlarm.latchHours = settings.windAboveLatchHrs;

				cumulus.SensorAlarm.enabled = settings.contactLostEnabled;
				cumulus.SensorAlarm.sound = settings.contactLostSoundEnabled;
				cumulus.SensorAlarm.soundFile = settings.contactLostSound;
				cumulus.SensorAlarm.latch = settings.contactLostLatches;
				cumulus.SensorAlarm.latchHours = settings.contactLostLatchHrs;

				cumulus.DataStoppedAlarm.enabled = settings.dataStoppedEnabled;
				cumulus.DataStoppedAlarm.sound = settings.dataStoppedSoundEnabled;
				cumulus.DataStoppedAlarm.soundFile = settings.dataStoppedSound;
				cumulus.DataStoppedAlarm.latch = settings.dataStoppedLatches;
				cumulus.DataStoppedAlarm.latchHours = settings.dataStoppedLatchHrs;

				cumulus.BatteryLowAlarm.enabled = settings.batteryLowEnabled;
				cumulus.BatteryLowAlarm.sound = settings.batteryLowSoundEnabled;
				cumulus.BatteryLowAlarm.soundFile = settings.batteryLowSound;
				cumulus.BatteryLowAlarm.latch = settings.batteryLowLatches;
				cumulus.BatteryLowAlarm.latchHours = settings.batteryLowLatchHrs;

				cumulus.SpikeAlarm.enabled = settings.spikeEnabled;
				cumulus.SpikeAlarm.sound = settings.spikeSoundEnabled;
				cumulus.SpikeAlarm.soundFile = settings.spikeSound;
				cumulus.SpikeAlarm.latch = settings.spikeLatches;
				cumulus.SpikeAlarm.latchHours = settings.spikeLatchHrs;

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
		public bool tempBelowLatches { get; set; }
		public int tempBelowLatchHrs { get; set; }

		public bool tempAboveEnabled { get; set; }
		public double tempAboveVal { get; set; }
		public bool tempAboveSoundEnabled { get; set; }
		public string tempAboveSound { get; set; }
		public bool tempAboveLatches { get; set; }
		public int tempAboveLatchHrs { get; set; }

		public bool tempChangeEnabled { get; set; }
		public double tempChangeVal { get; set; }
		public bool tempChangeSoundEnabled { get; set; }
		public string tempChangeSound { get; set; }
		public bool tempChangeLatches { get; set; }
		public int tempChangeLatchHrs { get; set; }

		public bool pressBelowEnabled { get; set; }
		public double pressBelowVal { get; set; }
		public bool pressBelowSoundEnabled { get; set; }
		public string pressBelowSound { get; set; }
		public bool pressBelowLatches { get; set; }
		public int pressBelowLatchHrs { get; set; }

		public bool pressAboveEnabled { get; set; }
		public double pressAboveVal { get; set; }
		public bool pressAboveSoundEnabled { get; set; }
		public string pressAboveSound { get; set; }
		public bool pressAboveLatches { get; set; }
		public int pressAboveLatchHrs { get; set; }

		public bool pressChangeEnabled { get; set; }
		public double pressChangeVal { get; set; }
		public bool pressChangeSoundEnabled { get; set; }
		public string pressChangeSound { get; set; }
		public bool pressChangeLatches { get; set; }
		public int pressChangeLatchHrs { get; set; }

		public bool rainAboveEnabled { get; set; }
		public double rainAboveVal { get; set; }
		public bool rainAboveSoundEnabled { get; set; }
		public string rainAboveSound { get; set; }
		public bool rainAboveLatches { get; set; }
		public int rainAboveLatchHrs { get; set; }

		public bool rainRateAboveEnabled { get; set; }
		public double rainRateAboveVal { get; set; }
		public bool rainRateAboveSoundEnabled { get; set; }
		public string rainRateAboveSound { get; set; }
		public bool rainRateAboveLatches { get; set; }
		public int rainRateAboveLatchHrs { get; set; }

		public bool gustAboveEnabled { get; set; }
		public double gustAboveVal { get; set; }
		public bool gustAboveSoundEnabled { get; set; }
		public string gustAboveSound { get; set; }
		public bool gustAboveLatches { get; set; }
		public int gustAboveLatchHrs { get; set; }

		public bool windAboveEnabled { get; set; }
		public double windAboveVal { get; set; }
		public bool windAboveSoundEnabled { get; set; }
		public string windAboveSound { get; set; }
		public bool windAboveLatches { get; set; }
		public int windAboveLatchHrs { get; set; }

		public bool contactLostEnabled { get; set; }
		public bool contactLostSoundEnabled { get; set; }
		public string contactLostSound { get; set; }
		public bool contactLostLatches { get; set; }
		public int contactLostLatchHrs { get; set; }

		public bool dataStoppedEnabled { get; set; }
		public bool dataStoppedSoundEnabled { get; set; }
		public string dataStoppedSound { get; set; }
		public bool dataStoppedLatches { get; set; }
		public int dataStoppedLatchHrs { get; set; }

		public bool batteryLowEnabled { get; set; }
		public bool batteryLowSoundEnabled { get; set; }
		public string batteryLowSound { get; set; }
		public bool batteryLowLatches { get; set; }
		public int batteryLowLatchHrs { get; set; }

		public bool spikeEnabled { get; set; }
		public bool spikeSoundEnabled { get; set; }
		public string spikeSound { get; set; }
		public bool spikeLatches { get; set; }
		public int spikeLatchHrs { get; set; }
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
