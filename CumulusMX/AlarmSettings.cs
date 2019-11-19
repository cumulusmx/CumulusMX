using System;
using System.Globalization;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;


namespace CumulusMX
{
	public class AlarmSettings
	{
		private Cumulus cumulus;

		public AlarmSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlarmSettings()
		{
			var InvC = new CultureInfo("");

			var alarmUnits = new JsonAlarmUnits()
			{
				tempUnits = cumulus.TempUnitText,
				pressUnits = cumulus.PressUnitText,
				rainUnits = cumulus.RainUnitText,
				windUnits = cumulus.WindUnitText
			};

			var data = new JsonAlarmSettingsData()
			{
				tempBelowEnabled = cumulus.LowTempAlarmEnabled,
				tempBelowVal = cumulus.LowTempAlarmValue,
				tempBelowSoundEnabled = cumulus.LowTempAlarmSound,
				tempBelowSound = cumulus.LowTempAlarmSoundFile,

				tempAboveEnabled = cumulus.HighTempAlarmEnabled,
				tempAboveVal = cumulus.HighTempAlarmValue,
				tempAboveSoundEnabled = cumulus.HighTempAlarmSound,
				tempAboveSound = cumulus.HighTempAlarmSoundFile,

				tempChangeEnabled = cumulus.TempChangeAlarmEnabled,
				tempChangeVal = cumulus.TempChangeAlarmValue,
				tempChangeSoundEnabled = cumulus.TempChangeAlarmSound,
				tempChangeSound = cumulus.TempChangeAlarmSoundFile,

				pressBelowEnabled = cumulus.LowPressAlarmEnabled,
				pressBelowVal = cumulus.LowPressAlarmValue,
				pressBelowSoundEnabled = cumulus.LowPressAlarmSound,
				pressBelowSound = cumulus.LowPressAlarmSoundFile,

				pressAboveEnabled = cumulus.HighPressAlarmEnabled,
				pressAboveVal = cumulus.HighPressAlarmValue,
				pressAboveSoundEnabled = cumulus.HighPressAlarmSound,
				pressAboveSound = cumulus.HighPressAlarmSoundFile,

				pressChangeEnabled = cumulus.PressChangeAlarmEnabled,
				pressChangeVal = cumulus.PressChangeAlarmValue,
				pressChangeSoundEnabled = cumulus.PressChangeAlarmSound,
				pressChangeSound = cumulus.PressChangeAlarmSoundFile,

				rainAboveEnabled = cumulus.HighRainTodayAlarmEnabled,
				rainAboveVal = cumulus.HighRainTodayAlarmValue,
				rainAboveSoundEnabled = cumulus.HighRainTodayAlarmSound,
				rainAboveSound = cumulus.HighRainTodayAlarmSoundFile,

				rainRateAboveEnabled = cumulus.HighRainRateAlarmEnabled,
				rainRateAboveVal = cumulus.HighRainRateAlarmValue,
				rainRateAboveSoundEnabled = cumulus.HighRainRateAlarmSound,
				rainRateAboveSound = cumulus.HighRainRateAlarmSoundFile,

				gustAboveEnabled = cumulus.HighGustAlarmEnabled,
				gustAboveVal = cumulus.HighGustAlarmValue,
				gustAboveSoundEnabled = cumulus.HighGustAlarmSound,
				gustAboveSound = cumulus.HighGustAlarmSoundFile,

				windAboveEnabled = cumulus.HighWindAlarmEnabled,
				windAboveVal = cumulus.HighWindAlarmValue,
				windAboveSoundEnabled = cumulus.HighWindAlarmSound,
				windAboveSound = cumulus.HighWindAlarmSoundFile,

				contactLostEnabled = cumulus.SensorAlarmEnabled,
				contactLostSoundEnabled = cumulus.SensorAlarmSound,
				contactLostSound = cumulus.SensorAlarmSoundFile
			};

			var retObject = new JsonAlarmSettings()
			{
				data = data,
				units = alarmUnits
			};

			return JsonConvert.SerializeObject(retObject);
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
				var settings = JsonConvert.DeserializeObject<JsonAlarmSettingsData>(json);
				// process the settings
				cumulus.LogMessage("Updating Alarm settings");
				var InvC = new CultureInfo("");

				cumulus.LowTempAlarmEnabled = settings.tempBelowEnabled;
				cumulus.LowTempAlarmValue = settings.tempBelowVal;
				cumulus.LowTempAlarmSound = settings.tempBelowSoundEnabled;
				cumulus.LowTempAlarmSoundFile = settings.tempBelowSound;

				cumulus.HighTempAlarmEnabled = settings.tempAboveEnabled;
				cumulus.HighTempAlarmValue = settings.tempAboveVal;
				cumulus.HighTempAlarmSound = settings.tempAboveSoundEnabled;
				cumulus.HighTempAlarmSoundFile = settings.tempAboveSound;

				cumulus.TempChangeAlarmEnabled = settings.tempChangeEnabled;
				cumulus.TempChangeAlarmValue = settings.tempChangeVal;
				cumulus.TempChangeAlarmSound = settings.tempChangeSoundEnabled;
				cumulus.TempChangeAlarmSoundFile = settings.tempChangeSound;

				cumulus.LowPressAlarmEnabled = settings.pressBelowEnabled;
				cumulus.LowPressAlarmValue = settings.pressBelowVal;
				cumulus.LowPressAlarmSound = settings.pressBelowSoundEnabled;
				cumulus.LowPressAlarmSoundFile = settings.pressBelowSound;

				cumulus.HighPressAlarmEnabled = settings.pressAboveEnabled;
				cumulus.HighPressAlarmValue = settings.pressAboveVal;
				cumulus.HighPressAlarmSound = settings.pressAboveSoundEnabled;
				cumulus.HighPressAlarmSoundFile = settings.pressAboveSound;

				cumulus.PressChangeAlarmEnabled = settings.pressChangeEnabled;
				cumulus.PressChangeAlarmValue = settings.pressChangeVal;
				cumulus.PressChangeAlarmSound = settings.pressChangeSoundEnabled;
				cumulus.PressChangeAlarmSoundFile = settings.pressChangeSound;

				cumulus.HighRainTodayAlarmEnabled = settings.rainAboveEnabled;
				cumulus.HighRainTodayAlarmValue = settings.rainAboveVal;
				cumulus.HighRainTodayAlarmSound = settings.rainAboveSoundEnabled;
				cumulus.HighRainTodayAlarmSoundFile = settings.rainAboveSound;

				cumulus.HighRainRateAlarmEnabled = settings.rainRateAboveEnabled;
				cumulus.HighRainRateAlarmValue = settings.rainRateAboveVal;
				cumulus.HighRainRateAlarmSound = settings.rainRateAboveSoundEnabled;
				cumulus.HighRainRateAlarmSoundFile = settings.rainRateAboveSound;

				cumulus.HighGustAlarmEnabled = settings.gustAboveEnabled;
				cumulus.HighGustAlarmValue = settings.gustAboveVal;
				cumulus.HighGustAlarmSound = settings.gustAboveSoundEnabled;
				cumulus.HighGustAlarmSoundFile = settings.gustAboveSound;

				cumulus.HighWindAlarmEnabled = settings.windAboveEnabled;
				cumulus.HighWindAlarmValue = settings.windAboveVal;
				cumulus.HighWindAlarmSound = settings.windAboveSoundEnabled;
				cumulus.HighWindAlarmSoundFile = settings.windAboveSound;

				cumulus.SensorAlarmEnabled = settings.contactLostEnabled;
				cumulus.SensorAlarmSound = settings.contactLostSoundEnabled;
				cumulus.SensorAlarmSoundFile = settings.contactLostSound;

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
		public bool tempBelowEnabled;
		public double tempBelowVal;
		public bool tempBelowSoundEnabled;
		public string tempBelowSound;

		public bool tempAboveEnabled;
		public double tempAboveVal;
		public bool tempAboveSoundEnabled;
		public string tempAboveSound;

		public bool tempChangeEnabled;
		public double tempChangeVal;
		public bool tempChangeSoundEnabled;
		public string tempChangeSound;

		public bool pressBelowEnabled;
		public double pressBelowVal;
		public bool pressBelowSoundEnabled;
		public string pressBelowSound;

		public bool pressAboveEnabled;
		public double pressAboveVal;
		public bool pressAboveSoundEnabled;
		public string pressAboveSound;

		public bool pressChangeEnabled;
		public double pressChangeVal;
		public bool pressChangeSoundEnabled;
		public string pressChangeSound;

		public bool rainAboveEnabled;
		public double rainAboveVal;
		public bool rainAboveSoundEnabled;
		public string rainAboveSound;

		public bool rainRateAboveEnabled;
		public double rainRateAboveVal;
		public bool rainRateAboveSoundEnabled;
		public string rainRateAboveSound;

		public bool gustAboveEnabled;
		public double gustAboveVal;
		public bool gustAboveSoundEnabled;
		public string gustAboveSound;

		public bool windAboveEnabled;
		public double windAboveVal;
		public bool windAboveSoundEnabled;
		public string windAboveSound;

		public bool contactLostEnabled;
		public bool contactLostSoundEnabled;
		public string contactLostSound;
	}

	public class JsonAlarmUnits
	{
		public string tempUnits;
		public string pressUnits;
		public string rainUnits;
		public string windUnits;
	}

	public class JsonAlarmSettings
	{
		public JsonAlarmSettingsData data;
		public JsonAlarmUnits units;
	}
}
