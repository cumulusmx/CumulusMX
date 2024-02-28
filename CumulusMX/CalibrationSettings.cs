using System;
using System.Globalization;
using System.IO;
using System.Net;

using EmbedIO;

using ServiceStack;

namespace CumulusMX
{
	public class CalibrationSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		//public string UpdateCalibrationConfig(HttpListenerContext context)
		public string UpdateConfig(IHttpContext context)
		{
			var json = string.Empty;
			JsonCalibrationSettingsData settings;
			var invC = CultureInfo.InvariantCulture;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonCalibrationSettingsData>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Calibration Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Calibration Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			try
			{
				// process the settings
				cumulus.LogMessage("Updating calibration settings");

				// offsets
				cumulus.Calib.Press.Offset = Convert.ToDouble(settings.pressure.offset, invC);
				cumulus.Calib.Temp.Offset = Convert.ToDouble(settings.temp.offset, invC);
				cumulus.Calib.InTemp.Offset = Convert.ToDouble(settings.tempin.offset, invC);
				cumulus.Calib.Hum.Offset = settings.hum.offset;
				cumulus.Calib.InHum.Offset = Convert.ToDouble(settings.humin.offset, invC);
				cumulus.Calib.WindDir.Offset = settings.winddir.offset;
				cumulus.Calib.Solar.Offset = Convert.ToDouble(settings.solar.offset);
				cumulus.Calib.UV.Offset = Convert.ToDouble(settings.uv.offset, invC);
				cumulus.Calib.WetBulb.Offset = Convert.ToDouble(settings.wetbulb.offset, invC);

				// multipliers
				cumulus.Calib.Press.Mult = Convert.ToDouble(settings.pressure.multiplier, invC);
				cumulus.Calib.WindSpeed.Mult = Convert.ToDouble(settings.windspd.multiplier, invC);
				cumulus.Calib.WindGust.Mult = Convert.ToDouble(settings.gust.multiplier, invC);
				cumulus.Calib.Temp.Mult = Convert.ToDouble(settings.temp.multiplier, invC);
				cumulus.Calib.InTemp.Mult = Convert.ToDouble(settings.tempin.multiplier, invC);
				cumulus.Calib.Hum.Mult = Convert.ToDouble(settings.hum.multiplier, invC);
				cumulus.Calib.InHum.Mult = Convert.ToDouble(settings.humin.multiplier, invC);
				cumulus.Calib.Rain.Mult = Convert.ToDouble(settings.rain.multiplier, invC);
				cumulus.Calib.Solar.Mult = Convert.ToDouble(settings.solar.multiplier, invC);
				cumulus.Calib.UV.Mult = Convert.ToDouble(settings.uv.multiplier, invC);
				cumulus.Calib.WetBulb.Mult = Convert.ToDouble(settings.wetbulb.multiplier, invC);

				//multipliers2
				cumulus.Calib.Press.Mult2 = Convert.ToDouble(settings.pressure.multiplier2, invC);
				cumulus.Calib.WindSpeed.Mult2 = Convert.ToDouble(settings.windspd.multiplier2, invC);
				cumulus.Calib.WindGust.Mult2 = Convert.ToDouble(settings.gust.multiplier2, invC);
				cumulus.Calib.Temp.Mult2 = Convert.ToDouble(settings.temp.multiplier2, invC);
				cumulus.Calib.InTemp.Mult2 = Convert.ToDouble(settings.tempin.multiplier2, invC);
				cumulus.Calib.Hum.Mult2 = Convert.ToDouble(settings.hum.multiplier2, invC);
				cumulus.Calib.InHum.Mult2 = Convert.ToDouble(settings.humin.multiplier2, invC);
				cumulus.Calib.Solar.Mult2 = Convert.ToDouble(settings.solar.multiplier2, invC);
				cumulus.Calib.UV.Mult2 = Convert.ToDouble(settings.uv.multiplier2, invC);
				cumulus.Calib.WetBulb.Mult2 = Convert.ToDouble(settings.wetbulb.multiplier2, invC);

				// spike removal
				cumulus.Spike.TempDiff = Convert.ToDouble(settings.temp.spike, invC);
				cumulus.Spike.HumidityDiff = Convert.ToDouble(settings.hum.spike, invC);
				cumulus.Spike.WindDiff = Convert.ToDouble(settings.windspd.spike, invC);
				cumulus.Spike.GustDiff = Convert.ToDouble(settings.gust.spike, invC);
				cumulus.Spike.MaxHourlyRain = Convert.ToDouble(settings.rain.spikehour, invC);
				cumulus.Spike.MaxRainRate = Convert.ToDouble(settings.rain.spikerate, invC);
				cumulus.Spike.PressDiff = Convert.ToDouble(settings.pressure.spike, invC);
				cumulus.Spike.InTempDiff = Convert.ToDouble(settings.tempin.spike, invC);
				cumulus.Spike.InHumDiff = Convert.ToDouble(settings.humin.spike, invC);

				// limits
				cumulus.Limit.TempHigh = Convert.ToDouble(settings.temp.limitmax, invC);
				cumulus.Limit.TempLow = Convert.ToDouble(settings.temp.limitmin, invC);
				cumulus.Limit.DewHigh = Convert.ToDouble(settings.dewpt.limitmax, invC);
				cumulus.Limit.PressHigh = Convert.ToDouble(settings.pressure.limitmax, invC);
				cumulus.Limit.PressLow = Convert.ToDouble(settings.pressure.limitmin, invC);
				cumulus.Limit.WindHigh = Convert.ToDouble(settings.gust.limitmax, invC);

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
				cumulus.LogErrorMessage("Error setting Calibration settings: " + ex.Message);
				cumulus.LogDebugMessage("Calibration Data: " + json);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return "success";
		}

		public string GetAlpacaFormData()
		{
			var pressure = new JsonCalibrationSettings()
			{
				offset = cumulus.Calib.Press.Offset,
				multiplier = cumulus.Calib.Press.Mult,
				multiplier2 = cumulus.Calib.Press.Mult2,
				spike = Math.Round(cumulus.Spike.PressDiff, cumulus.PressDPlaces),
				limitmax = Math.Round(cumulus.Limit.PressHigh, cumulus.PressDPlaces),
				limitmin = Math.Round(cumulus.Limit.PressLow, cumulus.PressDPlaces)
			};

			var temp = new JsonCalibrationSettings()
			{
				offset = cumulus.Calib.Temp.Offset,
				multiplier = cumulus.Calib.Temp.Mult,
				multiplier2 = cumulus.Calib.Temp.Mult2,
				spike = Math.Round(cumulus.Spike.TempDiff, cumulus.TempDPlaces),
				limitmax = Math.Round(cumulus.Limit.TempHigh, cumulus.TempDPlaces),
				limitmin = Math.Round(cumulus.Limit.TempLow, cumulus.TempDPlaces),
			};

			var tempin = new JsonCalibrationSettings()
			{
				offset = cumulus.Calib.InTemp.Offset,
				multiplier = cumulus.Calib.InTemp.Mult,
				multiplier2 = cumulus.Calib.InTemp.Mult2,
				spike = Math.Round(cumulus.Spike.InTempDiff, cumulus.TempDPlaces)
			};

			var hum = new JsonCalibrationSettings()
			{
				offset = (int) cumulus.Calib.Hum.Offset,
				multiplier = cumulus.Calib.Hum.Mult,
				multiplier2 = cumulus.Calib.Hum.Mult2,
				spike = cumulus.Spike.HumidityDiff
			};

			var humin = new JsonCalibrationSettings()
			{
				offset = (int) cumulus.Calib.InHum.Offset,
				multiplier = cumulus.Calib.InHum.Mult,
				multiplier2 = cumulus.Calib.InHum.Mult2,
				spike = cumulus.Spike.InHumDiff
			};

			var windspd = new JsonCalibrationSettings()
			{
				multiplier = cumulus.Calib.WindSpeed.Mult,
				multiplier2 = cumulus.Calib.WindSpeed.Mult2,
				spike = Math.Round(cumulus.Spike.WindDiff, cumulus.WindAvgDPlaces)
			};

			var gust = new JsonCalibrationSettings()
			{
				multiplier = cumulus.Calib.WindGust.Mult,
				multiplier2 = cumulus.Calib.WindGust.Mult2,
				spike = Math.Round(cumulus.Spike.GustDiff, cumulus.WindDPlaces),
				limitmax = Math.Round(cumulus.Limit.WindHigh, cumulus.WindDPlaces)
			};

			var winddir = new JsonCalibrationSettings()
			{
				offset = (int) cumulus.Calib.WindDir.Offset
			};

			var rain = new JsonCalibrationSettings()
			{
				multiplier = cumulus.Calib.Rain.Mult,
				spikehour = Math.Round(cumulus.Spike.MaxHourlyRain, cumulus.RainDPlaces),
				spikerate = Math.Round(cumulus.Spike.MaxRainRate, cumulus.RainDPlaces)
			};

			var solar = new JsonCalibrationSettings()
			{
				offset = cumulus.Calib.Solar.Offset,
				multiplier = cumulus.Calib.Solar.Mult,
				multiplier2 = cumulus.Calib.Solar.Mult2
			};

			var uv = new JsonCalibrationSettings()
			{
				offset = cumulus.Calib.UV.Offset,
				multiplier = cumulus.Calib.UV.Mult,
				multiplier2 = cumulus.Calib.UV.Mult2
			};

			var wetbulb = new JsonCalibrationSettings()
			{
				offset = Math.Round(cumulus.Calib.WetBulb.Offset, cumulus.TempDPlaces),
				multiplier = cumulus.Calib.WetBulb.Mult,
				multiplier2 = cumulus.Calib.WetBulb.Mult2
			};

			var dewpt = new JsonCalibrationSettings()
			{
				limitmax = Math.Round(cumulus.Limit.DewHigh, cumulus.TempDPlaces)
			};

			var data = new JsonCalibrationSettingsData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				pressure = pressure,
				temp = temp,
				tempin = tempin,
				hum = hum,
				humin = humin,
				windspd = windspd,
				gust = gust,
				winddir = winddir,
				rain = rain,
				solar = solar,
				uv = uv,
				wetbulb = wetbulb,
				dewpt = dewpt
			};

			return data.ToJson();
		}
	}

	public class JsonCalibrationSettingsData
	{
		public bool accessible { get; set; }
		public JsonCalibrationSettings pressure { get; set; }
		public JsonCalibrationSettings temp { get; set; }
		public JsonCalibrationSettings tempin { get; set; }
		public JsonCalibrationSettings hum { get; set; }
		public JsonCalibrationSettings humin { get; set; }
		public JsonCalibrationSettings windspd { get; set; }
		public JsonCalibrationSettings gust { get; set; }
		public JsonCalibrationSettings winddir { get; set; }
		public JsonCalibrationSettings rain { get; set; }
		public JsonCalibrationSettings solar { get; set; }
		public JsonCalibrationSettings uv { get; set; }
		public JsonCalibrationSettings wetbulb { get; set; }
		public JsonCalibrationSettings dewpt { get; set; }
	}


	public class JsonCalibrationSettings
	{
		public double offset { get; set; }
		public double multiplier { get; set; }
		public double multiplier2 { get; set; }
		public double spike { get; set; }
		public double spikehour { get; set; }
		public double spikerate { get; set; }
		public double limitmin { get; set; }
		public double limitmax { get; set; }
	}


}
