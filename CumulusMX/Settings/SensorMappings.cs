using System;
using System.IO;
using System.Net;
using System.Text.Json;

using EmbedIO;

namespace CumulusMX.Settings
{
	internal class SensorMappings
	{
		private readonly Cumulus cumulus;

		internal SensorMappings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		internal string GetAlpacaFormData()
		{
			var data = new JsonSensorMaps()
			{
				primaryTH = cumulus.SensorMaps.PrimaryTempHum,
				primaryInTH = cumulus.SensorMaps.PrimaryIndoorTempHum,
				indoorTemp = cumulus.SensorMaps.IndoorTemp,
				indoorHum = cumulus.SensorMaps.IndoorHum,
				temperature = cumulus.SensorMaps.Temperature,
				humidity = cumulus.SensorMaps.Humidity,
				wind = cumulus.SensorMaps.Wind,
				pressure = cumulus.SensorMaps.Pressure,
				rain = cumulus.SensorMaps.Rain,
				solar = cumulus.SensorMaps.Solar,
				uv = cumulus.SensorMaps.UV,
				bgt = cumulus.SensorMaps.BlackGlobe,
				lightning = cumulus.SensorMaps.Lightning,
				camera = cumulus.SensorMaps.Camera,
				co2 = cumulus.SensorMaps.CO2,
				extraTempHum = new JsonSensors { sensors = cumulus.SensorMaps.ExtraTempHum },
				userTemp = new JsonSensors { sensors = cumulus.SensorMaps.UserTemp },
				soilTemp = new JsonSensors { sensors = cumulus.SensorMaps.SoilTemp },
				soilMoist = new JsonSensors { sensors = cumulus.SensorMaps.SoilMoist },
				soilEc = new JsonSensors { sensors = cumulus.SensorMaps.SoilEc },
				leafWet = new JsonSensors { sensors = cumulus.SensorMaps.LeafWet },
				leak = new JsonSensors { sensors = cumulus.SensorMaps.Leak },
				airQual = new JsonSensors { sensors = cumulus.SensorMaps.AirQual },
				laserDist = new JsonSensors { sensors = cumulus.SensorMaps.LaserDist }
			};

			return JsonSerializer.Serialize(data);
		}

		internal string UpdateConfig(IHttpContext context)
		{
			var errorMsg = string.Empty;
			var json = string.Empty;
			context.Response.StatusCode = 200;
			JsonSensorMaps settings;

			// get the response
			try
			{
				cumulus.LogMessage("Updating sensor mapping settings");

				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json=" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<JsonSensorMaps>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Sensor Mappings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Sensor Mappings: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.SensorMaps.PrimaryTempHum = settings.primaryTH;
				cumulus.SensorMaps.PrimaryIndoorTempHum = settings.primaryInTH;
				cumulus.SensorMaps.IndoorTemp = settings.indoorTemp;
				cumulus.SensorMaps.IndoorHum = settings.indoorHum;
				cumulus.SensorMaps.Temperature = settings.temperature;
				cumulus.SensorMaps.Humidity = settings.humidity;
				cumulus.SensorMaps.Wind = settings.wind;
				cumulus.SensorMaps.Pressure = settings.pressure;
				cumulus.SensorMaps.Rain = settings.rain;
				cumulus.SensorMaps.Solar = settings.solar;
				cumulus.SensorMaps.UV = settings.uv;
				cumulus.SensorMaps.BlackGlobe = settings.bgt;
				cumulus.SensorMaps.Lightning = settings.lightning;
				cumulus.SensorMaps.Camera = settings.camera;
				cumulus.SensorMaps.CO2 = settings.co2;
				cumulus.SensorMaps.ExtraTempHum = settings.extraTempHum.sensors;
				cumulus.SensorMaps.UserTemp = settings.userTemp.sensors;
				cumulus.SensorMaps.SoilTemp = settings.soilTemp.sensors;
				cumulus.SensorMaps.SoilMoist = settings.soilMoist.sensors;
				cumulus.SensorMaps.SoilEc = settings.soilEc.sensors;
				cumulus.SensorMaps.LeafWet = settings.leafWet.sensors;
				cumulus.SensorMaps.Leak = settings.leak.sensors;
				cumulus.SensorMaps.AirQual = settings.airQual.sensors;
				cumulus.SensorMaps.LaserDist = settings.laserDist.sensors;
			}
			catch (Exception ex)
			{
				var msg = "Error processing Display settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Display Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			// Save the settings
			cumulus.WriteIniFile();

			// Graph configs may have changed, so re-create and upload the json files - just flag everything!
			for (var i = 0; i < cumulus.GraphDataFiles.Length; i++)
			{
				cumulus.GraphDataFiles[i].CreateRequired = true;
				cumulus.GraphDataFiles[i].FtpRequired = true;
				cumulus.GraphDataFiles[i].CopyRequired = true;
				cumulus.GraphDataFiles[i].Incremental = false;
			}
			for (var i = 0; i < cumulus.GraphDataEodFiles.Length; i++)
			{
				cumulus.GraphDataEodFiles[i].CreateRequired = true;
				cumulus.GraphDataEodFiles[i].FtpRequired = true;
				cumulus.GraphDataEodFiles[i].CopyRequired = true;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;
		}

		private sealed class JsonSensors
		{
			public int[] sensors { get; set; }
		}


		private sealed class JsonSensorMaps
		{
			public int primaryTH { get; set; }
			public int primaryInTH { get; set; }
			public int indoorTemp { get; set; }
			public int indoorHum { get; set; }
			public int temperature { get; set; }
			public int humidity { get; set; }
			public int wind { get; set; }
			public int pressure { get; set; }
			public int rain { get; set; }
			public int solar { get; set; }
			public int uv { get; set; }
			public int bgt { get; set; }
			public int lightning { get; set; }
			public int camera { get; set; }
			public int co2 { get; set; }
			public JsonSensors extraTempHum { get; set; }
			public JsonSensors userTemp { get; set; }
			public JsonSensors soilTemp { get; set; }
			public JsonSensors soilMoist { get; set; }
			public JsonSensors soilEc { get; set; }
			public JsonSensors leafWet { get; set; }
			public JsonSensors leak { get; set; }
			public JsonSensors airQual { get; set; }
			public JsonSensors laserDist { get; set; }
		}
	}
}
