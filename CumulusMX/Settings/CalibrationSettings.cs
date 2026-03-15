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
				cumulus.Calib.Press.Offset = settings.pressure.offset;
				cumulus.Calib.PressStn.Offset = settings.pressureStn.offset;
				cumulus.Calib.Temp.Offset = settings.temp.offset;
				cumulus.Calib.InTemp.Offset = settings.tempin.offset;
				cumulus.Calib.Hum.Offset = settings.hum.offset;
				cumulus.Calib.InHum.Offset = settings.humin.offset;
				cumulus.Calib.WindDir.Offset = settings.winddir.offset;
				cumulus.Calib.Solar.Offset = Convert.ToDouble(settings.solar.offset);
				cumulus.Calib.UV.Offset = settings.uv.offset;
				cumulus.Calib.WetBulb.Offset = settings.wetbulb.offset;

				// multipliers
				cumulus.Calib.Press.Mult = settings.pressure.multiplier;
				cumulus.Calib.PressStn.Mult = settings.pressureStn.multiplier;
				cumulus.Calib.WindSpeed.Mult = settings.windspd.multiplier;
				cumulus.Calib.WindGust.Mult = settings.gust.multiplier;
				cumulus.Calib.Temp.Mult = settings.temp.multiplier;
				cumulus.Calib.InTemp.Mult = settings.tempin.multiplier;
				cumulus.Calib.Hum.Mult = settings.hum.multiplier;
				cumulus.Calib.InHum.Mult = settings.humin.multiplier;
				cumulus.Calib.Rain.Mult = settings.rain.multiplier;
				cumulus.Calib.Solar.Mult = settings.solar.multiplier;
				cumulus.Calib.UV.Mult = settings.uv.multiplier;
				cumulus.Calib.WetBulb.Mult = settings.wetbulb.multiplier;

				//multipliers2
				cumulus.Calib.Press.Mult2 = settings.pressure.multiplier2;
				cumulus.Calib.PressStn.Mult2 = settings.pressureStn.multiplier2;
				cumulus.Calib.WindSpeed.Mult2 = settings.windspd.multiplier2;
				cumulus.Calib.WindGust.Mult2 = settings.gust.multiplier2;
				cumulus.Calib.Temp.Mult2 = settings.temp.multiplier2;
				cumulus.Calib.InTemp.Mult2 = settings.tempin.multiplier2;
				cumulus.Calib.Hum.Mult2 = settings.hum.multiplier2;
				cumulus.Calib.InHum.Mult2 = settings.humin.multiplier2;
				cumulus.Calib.Solar.Mult2 = settings.solar.multiplier2;
				cumulus.Calib.UV.Mult2 = settings.uv.multiplier2;
				cumulus.Calib.WetBulb.Mult2 = settings.wetbulb.multiplier2;

				// spike removal
				cumulus.Spike.TempDiff = settings.temp.spike;
				cumulus.Spike.HumidityDiff = settings.hum.spike;
				cumulus.Spike.WindDiff = settings.windspd.spike;
				cumulus.Spike.GustDiff = settings.gust.spike;
				cumulus.Spike.MaxHourlyRain = settings.rain.spikehour;
				cumulus.Spike.MaxRainRate = settings.rain.spikerate;
				cumulus.Spike.PressDiff = settings.pressure.spike;
				cumulus.Spike.InTempDiff = settings.tempin.spike;
				cumulus.Spike.InHumDiff = settings.humin.spike;
				cumulus.Spike.SnowDiff = settings.snow.spike;

				// limits
				cumulus.Limit.TempHigh = settings.temp.limitmax;
				cumulus.Limit.TempLow = settings.temp.limitmin;
				cumulus.Limit.DewHigh = settings.dewpt.limitmax;
				cumulus.Limit.PressHigh = settings.pressure.limitmax;
				cumulus.Limit.PressLow = settings.pressure.limitmin;
				cumulus.Limit.WindHigh = settings.gust.limitmax;
				cumulus.Limit.StationPressHigh = MeteoLib.SeaLevelToStation(cumulus.Limit.PressHigh, ConvertUnits.AltitudeM(cumulus.Altitude));
				cumulus.Limit.StationPressLow = MeteoLib.SeaLevelToStation(cumulus.Limit.PressLow, ConvertUnits.AltitudeM(cumulus.Altitude));

				// snow
				cumulus.SnowDepthMinInc = settings.snow.mininc;
				if (cumulus.SnowDepthMedianMins != settings.snow.filter.median)
				{
					cumulus.SnowDepthMedianMins = settings.snow.filter.median;
					for (int i = 1; i < Program.cumulus.Station.SnowDepthAverage.Length; i++)
					{
						Program.cumulus.Station.SnowDepthAverage[i].MedianWindow = cumulus.SnowDepthMedianMins;
					}
				}
				if (cumulus.SnowDepthClipDelta != settings.snow.filter.clip)
				{
					cumulus.SnowDepthClipDelta = settings.snow.filter.clip;
					for (int i = 1; i < Program.cumulus.Station.SnowDepthAverage.Length; i++)
					{
						Program.cumulus.Station.SnowDepthAverage[i].ClipDelta = cumulus.SnowDepthClipDelta;
					}
				}
				if (cumulus.SnowDepthEmaTimeMins != settings.snow.filter.ema)
				{
					cumulus.SnowDepthEmaTimeMins = settings.snow.filter.ema;
					for (int i = 1; i < Program.cumulus.Station.SnowDepthAverage.Length; i++)
					{
						Program.cumulus.Station.SnowDepthAverage[i].TimeConst = cumulus.SnowDepthEmaTimeMins;
					}
				}

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
				mininc = Math.Round(cumulus.SnowDepthMinInc, cumulus.LaserDPlaces),
				filter = new SnowFilter()
				{
					median = cumulus.SnowDepthMedianMins,
					clip = cumulus.SnowDepthClipDelta,
					ema = cumulus.SnowDepthEmaTimeMins
				}
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
			public SnowFilter filter { get; set; }
		}

		private sealed class SnowFilter
		{
			public int median { get; set; }
			public double clip {  get; set; }
			public double ema {  get; set; }
		}
	}
}
