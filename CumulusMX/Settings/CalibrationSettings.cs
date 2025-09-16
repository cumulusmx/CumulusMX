using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;

using EmbedIO;

namespace CumulusMX.Settings
{
	public class CalibrationSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		//public string UpdateCalibrationConfig(HttpListenerContext context)
		public string UpdateConfig(IHttpContext context)
		{
			var json = string.Empty;
			JsonSettingsData settings;
			var invC = CultureInfo.InvariantCulture;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<JsonSettingsData>(json);
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
				cumulus.Calib.PressStn.Offset = Convert.ToDouble(settings.pressureStn.offset, invC);
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
				cumulus.Calib.PressStn.Mult = Convert.ToDouble(settings.pressureStn.multiplier, invC);
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
				cumulus.Calib.PressStn.Mult2 = Convert.ToDouble(settings.pressureStn.multiplier2, invC);
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
				cumulus.Spike.SnowDiff = Convert.ToDecimal(settings.snow.spike, invC);

				// limits
				cumulus.Limit.TempHigh = Convert.ToDouble(settings.temp.limitmax, invC);
				cumulus.Limit.TempLow = Convert.ToDouble(settings.temp.limitmin, invC);
				cumulus.Limit.DewHigh = Convert.ToDouble(settings.dewpt.limitmax, invC);
				cumulus.Limit.PressHigh = Convert.ToDouble(settings.pressure.limitmax, invC);
				cumulus.Limit.PressLow = Convert.ToDouble(settings.pressure.limitmin, invC);
				cumulus.Limit.WindHigh = Convert.ToDouble(settings.gust.limitmax, invC);
				cumulus.Limit.StationPressHigh = MeteoLib.SeaLevelToStation(cumulus.Limit.PressHigh, ConvertUnits.AltitudeM(cumulus.Altitude));
				cumulus.Limit.StationPressLow = MeteoLib.SeaLevelToStation(cumulus.Limit.PressLow, ConvertUnits.AltitudeM(cumulus.Altitude));

				// snow
				cumulus.SnowMinInc = Convert.ToDecimal(settings.snow.mininc, invC);

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
			var pressure = new JsonSettings()
			{
				offset = cumulus.Calib.Press.Offset,
				multiplier = cumulus.Calib.Press.Mult,
				multiplier2 = cumulus.Calib.Press.Mult2,
				spike = Math.Round(cumulus.Spike.PressDiff, cumulus.PressDPlaces),
				limitmax = Math.Round(cumulus.Limit.PressHigh, cumulus.PressDPlaces),
				limitmin = Math.Round(cumulus.Limit.PressLow, cumulus.PressDPlaces)
			};

			var pressurestn = new JsonSettings()
			{
				offset = cumulus.Calib.PressStn.Offset,
				multiplier = cumulus.Calib.PressStn.Mult,
				multiplier2 = cumulus.Calib.PressStn.Mult2
			};

			var temp = new JsonSettings()
			{
				offset = cumulus.Calib.Temp.Offset,
				multiplier = cumulus.Calib.Temp.Mult,
				multiplier2 = cumulus.Calib.Temp.Mult2,
				spike = Math.Round(cumulus.Spike.TempDiff, cumulus.TempDPlaces),
				limitmax = Math.Round(cumulus.Limit.TempHigh, cumulus.TempDPlaces),
				limitmin = Math.Round(cumulus.Limit.TempLow, cumulus.TempDPlaces),
			};

			var tempin = new JsonSettings()
			{
				offset = cumulus.Calib.InTemp.Offset,
				multiplier = cumulus.Calib.InTemp.Mult,
				multiplier2 = cumulus.Calib.InTemp.Mult2,
				spike = Math.Round(cumulus.Spike.InTempDiff, cumulus.TempDPlaces)
			};

			var hum = new JsonSettings()
			{
				offset = (int) cumulus.Calib.Hum.Offset,
				multiplier = cumulus.Calib.Hum.Mult,
				multiplier2 = cumulus.Calib.Hum.Mult2,
				spike = cumulus.Spike.HumidityDiff
			};

			var humin = new JsonSettings()
			{
				offset = (int) cumulus.Calib.InHum.Offset,
				multiplier = cumulus.Calib.InHum.Mult,
				multiplier2 = cumulus.Calib.InHum.Mult2,
				spike = cumulus.Spike.InHumDiff
			};

			var windspd = new JsonSettings()
			{
				multiplier = cumulus.Calib.WindSpeed.Mult,
				multiplier2 = cumulus.Calib.WindSpeed.Mult2,
				spike = Math.Round(cumulus.Spike.WindDiff, cumulus.WindAvgDPlaces)
			};

			var gust = new JsonSettings()
			{
				multiplier = cumulus.Calib.WindGust.Mult,
				multiplier2 = cumulus.Calib.WindGust.Mult2,
				spike = Math.Round(cumulus.Spike.GustDiff, cumulus.WindDPlaces),
				limitmax = Math.Round(cumulus.Limit.WindHigh, cumulus.WindDPlaces)
			};

			var winddir = new JsonSettings()
			{
				offset = (int) cumulus.Calib.WindDir.Offset
			};

			var rain = new JsonSettings()
			{
				multiplier = cumulus.Calib.Rain.Mult,
				spikehour = Math.Round(cumulus.Spike.MaxHourlyRain, cumulus.RainDPlaces),
				spikerate = Math.Round(cumulus.Spike.MaxRainRate, cumulus.RainDPlaces)
			};

			var solar = new JsonSettings()
			{
				offset = cumulus.Calib.Solar.Offset,
				multiplier = cumulus.Calib.Solar.Mult,
				multiplier2 = cumulus.Calib.Solar.Mult2
			};

			var uv = new JsonSettings()
			{
				offset = cumulus.Calib.UV.Offset,
				multiplier = cumulus.Calib.UV.Mult,
				multiplier2 = cumulus.Calib.UV.Mult2
			};

			var wetbulb = new JsonSettings()
			{
				offset = Math.Round(cumulus.Calib.WetBulb.Offset, cumulus.TempDPlaces),
				multiplier = cumulus.Calib.WetBulb.Mult,
				multiplier2 = cumulus.Calib.WetBulb.Mult2
			};

			var dewpt = new JsonSettings()
			{
				limitmax = Math.Round(cumulus.Limit.DewHigh, cumulus.TempDPlaces)
			};

			var snow = new SnowSettings()
			{
				spike = Math.Round(cumulus.Spike.SnowDiff, cumulus.LaserDPlaces),
				mininc = Math.Round(cumulus.SnowMinInc, cumulus.LaserDPlaces)
			};

			var data = new JsonSettingsData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				pressure = pressure,
				pressureStn = pressurestn,
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
				dewpt = dewpt,
				snow = snow
			};

			return JsonSerializer.Serialize(data);
		}

		private sealed class JsonSettingsData
		{
			public bool accessible { get; set; }
			public JsonSettings pressure { get; set; }
			public JsonSettings pressureStn { get; set; }
			public JsonSettings temp { get; set; }
			public JsonSettings tempin { get; set; }
			public JsonSettings hum { get; set; }
			public JsonSettings humin { get; set; }
			public JsonSettings windspd { get; set; }
			public JsonSettings gust { get; set; }
			public JsonSettings winddir { get; set; }
			public JsonSettings rain { get; set; }
			public JsonSettings solar { get; set; }
			public JsonSettings uv { get; set; }
			public JsonSettings wetbulb { get; set; }
			public JsonSettings dewpt { get; set; }
			public SnowSettings snow { get; set; }
		}

		private sealed class JsonSettings
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

		private sealed class SnowSettings
		{
			public decimal spike { get; set; }
			public decimal mininc { get; set; }
		}
	}
}
