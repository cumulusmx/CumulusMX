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
				var invC = new CultureInfo("");
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = json.FromJson<JsonCalibrationSettingsData>();
				// process the settings
				cumulus.LogMessage("Updating calibration settings");

				// offsets
				cumulus.Calib.Press.Offset = Convert.ToDouble(settings.offsets.pressure,invC);
				cumulus.Calib.Temp.Offset = Convert.ToDouble(settings.offsets.temperature, invC);
				cumulus.Calib.InTemp.Offset = Convert.ToDouble(settings.offsets.indoortemp, invC);
				cumulus.Calib.Hum.Offset = settings.offsets.humidity;
				cumulus.Calib.WindDir.Offset = settings.offsets.winddir;
				cumulus.Calib.Solar.Offset = Convert.ToDouble(settings.offsets.solar);
				cumulus.Calib.UV.Offset = Convert.ToDouble(settings.offsets.uv, invC);
				cumulus.Calib.WetBulb.Offset = Convert.ToDouble(settings.offsets.wetbulb, invC);

				// multipliers
				cumulus.Calib.Press.Mult = Convert.ToDouble(settings.multipliers.pressure, invC);
				cumulus.Calib.WindSpeed.Mult = Convert.ToDouble(settings.multipliers.windspeed, invC);
				cumulus.Calib.WindGust.Mult = Convert.ToDouble(settings.multipliers.windgust, invC);
				cumulus.Calib.Temp.Mult = Convert.ToDouble(settings.multipliers.outdoortemp, invC);
				cumulus.Calib.Hum.Mult = Convert.ToDouble(settings.multipliers.humidity, invC);
				cumulus.Calib.Rain.Mult = Convert.ToDouble(settings.multipliers.rainfall, invC);
				cumulus.Calib.Solar.Mult = Convert.ToDouble(settings.multipliers.solar, invC);
				cumulus.Calib.UV.Mult = Convert.ToDouble(settings.multipliers.uv, invC);
				cumulus.Calib.WetBulb.Mult = Convert.ToDouble(settings.multipliers.wetbulb, invC);

				// spike removal
				cumulus.Spike.TempDiff = Convert.ToDouble(settings.spikeremoval.outdoortemp, invC);
				cumulus.Spike.HumidityDiff = Convert.ToDouble(settings.spikeremoval.humidity, invC);
				cumulus.Spike.WindDiff = Convert.ToDouble(settings.spikeremoval.windspeed, invC);
				cumulus.Spike.GustDiff = Convert.ToDouble(settings.spikeremoval.windgust, invC);
				cumulus.Spike.MaxHourlyRain = Convert.ToDouble(settings.spikeremoval.maxhourlyrain, invC);
				cumulus.Spike.MaxRainRate = Convert.ToDouble(settings.spikeremoval.maxrainrate, invC);
				cumulus.Spike.PressDiff = Convert.ToDouble(settings.spikeremoval.pressure, invC);

				// limits
				cumulus.Limit.TempHigh = Convert.ToDouble(settings.limits.temphigh, invC);
				cumulus.Limit.TempLow = Convert.ToDouble(settings.limits.templow, invC);
				cumulus.Limit.DewHigh = Convert.ToDouble(settings.limits.dewhigh, invC);
				cumulus.Limit.PressHigh = Convert.ToDouble(settings.limits.presshigh, invC);
				cumulus.Limit.PressLow = Convert.ToDouble(settings.limits.presslow, invC);
				cumulus.Limit.WindHigh = Convert.ToDouble(settings.limits.windhigh, invC);

				cumulus.ErrorLogSpikeRemoval = settings.log;

				// Save the settings
				cumulus.WriteIniFile();

				// Clear the spike alarm
				cumulus.SpikeAlarm.Triggered = false;

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
						pressure = cumulus.Calib.Press.Offset,
						temperature = cumulus.Calib.Temp.Offset,
						indoortemp = cumulus.Calib.InTemp.Offset,
						humidity = (int)cumulus.Calib.Hum.Offset,
						winddir = (int)cumulus.Calib.WindDir.Offset,
						solar = cumulus.Calib.Solar.Offset,
						uv = cumulus.Calib.UV.Offset,
						wetbulb = cumulus.Calib.WetBulb.Offset
					};
			var multipliers = new JsonCalibrationSettingsMultipliers()
					{
						pressure = cumulus.Calib.Press.Mult,
						windspeed = cumulus.Calib.WindSpeed.Mult,
						windgust = cumulus.Calib.WindGust.Mult,
						humidity = cumulus.Calib.Hum.Mult,
						outdoortemp = cumulus.Calib.Temp.Mult,
						rainfall = cumulus.Calib.Rain.Mult,
						solar = cumulus.Calib.Solar.Mult,
						uv = cumulus.Calib.UV.Mult,
						wetbulb = cumulus.Calib.WetBulb.Mult
					};

			var spikeremoval = new JsonCalibrationSettingsSpikeRemoval()
					{
						humidity = cumulus.Spike.HumidityDiff,
						windgust = cumulus.Spike.GustDiff,
						windspeed = cumulus.Spike.WindDiff,
						outdoortemp = cumulus.Spike.TempDiff,
						maxhourlyrain = cumulus.Spike.MaxHourlyRain,
						maxrainrate = cumulus.Spike.MaxRainRate,
						pressure = cumulus.Spike.PressDiff
					};

			var limits = new JsonCalibrationSettingsLimits()
				{
					temphigh = cumulus.Limit.TempHigh,
					templow = cumulus.Limit.TempLow,
					dewhigh = cumulus.Limit.DewHigh,
					presshigh = cumulus.Limit.PressHigh,
					presslow = cumulus.Limit.PressLow,
					windhigh = cumulus.Limit.WindHigh
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
