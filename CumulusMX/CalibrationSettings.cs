using System;
using System.Globalization;
using System.IO;
using System.Net;
using ServiceStack;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class CalibrationSettings
	{
		private readonly Cumulus cumulus;
		private readonly string calibrationOptionsFile;
		private readonly string calibrationSchemaFile;

		public CalibrationSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			calibrationOptionsFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "CalibrationOptions.json";
			calibrationSchemaFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "CalibrationSchema.json";
		}

		//public string UpdateCalibrationConfig(HttpListenerContext context)
		public string UpdateCalibrationConfig(IHttpContext context)
		{
			try
			{
				var InvC = new CultureInfo("");
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = json.FromJson<JsonCalibrationSettingsData>();
				// process the settings
				cumulus.LogMessage("Updating calibration settings");

				// offsets
				cumulus.PressOffset = Convert.ToDouble(settings.offsets.pressure,InvC);
				cumulus.TempOffset = Convert.ToDouble(settings.offsets.temperature, InvC);
				cumulus.InTempoffset = Convert.ToDouble(settings.offsets.indoortemp, InvC);
				cumulus.HumOffset = settings.offsets.humidity;
				cumulus.WindDirOffset = settings.offsets.winddir;
				cumulus.SolarOffset = Convert.ToDouble(settings.offsets.solar);
				cumulus.UVOffset = Convert.ToDouble(settings.offsets.uv, InvC);
				cumulus.WetBulbOffset = Convert.ToDouble(settings.offsets.wetbulb, InvC);

				// multipliers
				cumulus.PressMult = Convert.ToDouble(settings.multipliers.pressure, InvC);
				cumulus.WindSpeedMult = Convert.ToDouble(settings.multipliers.windspeed, InvC);
				cumulus.WindGustMult = Convert.ToDouble(settings.multipliers.windgust, InvC);
				cumulus.TempMult = Convert.ToDouble(settings.multipliers.outdoortemp, InvC);
				cumulus.HumMult = Convert.ToDouble(settings.multipliers.humidity, InvC);
				cumulus.RainMult = Convert.ToDouble(settings.multipliers.rainfall, InvC);
				cumulus.SolarMult = Convert.ToDouble(settings.multipliers.solar, InvC);
				cumulus.UVMult = Convert.ToDouble(settings.multipliers.uv, InvC);
				cumulus.WetBulbMult = Convert.ToDouble(settings.multipliers.wetbulb, InvC);

				// spike removal
				cumulus.SpikeTempDiff = Convert.ToDouble(settings.spikeremoval.outdoortemp, InvC);
				cumulus.SpikeHumidityDiff = Convert.ToDouble(settings.spikeremoval.humidity, InvC);
				cumulus.SpikeWindDiff = Convert.ToDouble(settings.spikeremoval.windspeed, InvC);
				cumulus.SpikeGustDiff = Convert.ToDouble(settings.spikeremoval.windgust, InvC);
				cumulus.SpikeMaxHourlyRain = Convert.ToDouble(settings.spikeremoval.maxhourlyrain, InvC);
				cumulus.SpikeMaxRainRate = Convert.ToDouble(settings.spikeremoval.maxrainrate, InvC);
				cumulus.SpikePressDiff = Convert.ToDouble(settings.spikeremoval.pressure, InvC);

				// limits
				cumulus.LimitTempHigh = Convert.ToDouble(settings.limits.temphigh, InvC);
				cumulus.LimitTempLow = Convert.ToDouble(settings.limits.templow, InvC);
				cumulus.LimitDewHigh = Convert.ToDouble(settings.limits.dewhigh, InvC);
				cumulus.LimitPressHigh = Convert.ToDouble(settings.limits.presshigh, InvC);
				cumulus.LimitPressLow = Convert.ToDouble(settings.limits.presslow, InvC);
				cumulus.LimitWindHigh = Convert.ToDouble(settings.limits.windhigh, InvC);

				cumulus.ErrorLogSpikeRemoval = settings.log;

				// Save the settings
				cumulus.WriteIniFile();

				// Clear the spike alarm
				cumulus.SpikeAlarm.triggered = false;

				// Log the new values
				cumulus.LogMessage("Setting new calibration values...");
				cumulus.LogOffsetsMultipliers();

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

		public string GetCalibrationAlpacaFormData()
		{
			//var InvC = new CultureInfo("");
			var offsets = new JsonCalibrationSettingsOffsets()
					{
						pressure = cumulus.PressOffset,
						temperature = cumulus.TempOffset,
						indoortemp = cumulus.InTempoffset,
						humidity = cumulus.HumOffset,
						winddir = cumulus.WindDirOffset,
						solar = cumulus.SolarOffset,
						uv = cumulus.UVOffset,
						wetbulb = cumulus.WetBulbOffset
					};
			var multipliers = new JsonCalibrationSettingsMultipliers()
					{
						pressure = cumulus.PressMult,
						windspeed = cumulus.WindSpeedMult,
						windgust = cumulus.WindGustMult,
						humidity = cumulus.HumMult,
						outdoortemp = cumulus.TempMult,
						rainfall = cumulus.RainMult,
						solar = cumulus.SolarMult,
						uv = cumulus.UVMult,
						wetbulb = cumulus.WetBulbMult
					};

			var spikeremoval = new JsonCalibrationSettingsSpikeRemoval()
					{
						humidity = cumulus.SpikeHumidityDiff,
						windgust = cumulus.SpikeGustDiff,
						windspeed = cumulus.SpikeWindDiff,
						outdoortemp = cumulus.SpikeTempDiff,
						maxhourlyrain = cumulus.SpikeMaxHourlyRain,
						maxrainrate = cumulus.SpikeMaxRainRate,
						pressure = cumulus.SpikePressDiff
					};

			var limits = new JsonCalibrationSettingsLimits()
				{
					temphigh = cumulus.LimitTempHigh,
					templow = cumulus.LimitTempLow,
					dewhigh = cumulus.LimitDewHigh,
					presshigh = cumulus.LimitPressHigh,
					presslow = cumulus.LimitPressLow,
					windhigh = cumulus.LimitWindHigh
				};


			var data = new JsonCalibrationSettingsData()
					   {
						   offsets = offsets,
						   multipliers = multipliers,
						   spikeremoval = spikeremoval,
						   limits = limits,
						   log = cumulus.ErrorLogSpikeRemoval
			};

			return data.ToJson();
		}

		public string GetCalibrationAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(calibrationOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetCalibrationAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(calibrationSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}
	}

	public class JsonCalibrationSettingsData
	{
		public JsonCalibrationSettingsOffsets offsets { get; set; }
		public JsonCalibrationSettingsMultipliers multipliers { get; set; }
		public JsonCalibrationSettingsSpikeRemoval spikeremoval { get; set; }
		public JsonCalibrationSettingsLimits limits { get; set; }
		public bool log { get; set; }
	}

	public class JsonCalibrationSettingsOffsets
	{
		public double pressure { get; set; }
		public double temperature { get; set; }
		public double indoortemp { get; set; }
		public int humidity { get; set; }
		public int winddir { get; set; }
		public double solar { get; set; }
		public double uv { get; set; }
		public double wetbulb { get; set; }
	}

	public class JsonCalibrationSettingsMultipliers
	{
		public double pressure { get; set; }
		public double windspeed { get; set; }
		public double windgust { get; set; }
		public double outdoortemp { get; set; }
		public double humidity { get; set; }
		public double rainfall { get; set; }
		public double solar { get; set; }
		public double uv { get; set; }
		public double wetbulb { get; set; }
	}

	public class JsonCalibrationSettingsSpikeRemoval
	{
		public double windspeed { get; set; }
		public double windgust { get; set; }
		public double outdoortemp { get; set; }
		public double humidity { get; set; }
		public double pressure { get; set; }
		public double maxrainrate { get; set; }
		public double maxhourlyrain { get; set; }
	}

	public class JsonCalibrationSettingsLimits
	{
		public double temphigh { get; set; }
		public double templow { get; set; }
		public double dewhigh { get; set; }
		public double presshigh { get; set; }
		public double presslow { get; set; }
		public double windhigh { get; set; }
	}
}
