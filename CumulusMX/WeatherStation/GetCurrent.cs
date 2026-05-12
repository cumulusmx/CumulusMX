using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public string GetExtraTemp()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < MetData.ExtraTemp.Length; sensor++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.ExtraTempCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.ExtraTemp[sensor].ToFixed(cumulus.TempFormat, "-"));
					json.Append("\",\"&deg;");
					json.Append(cumulus.Units.TempText[1]);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetUserTemp()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < MetData.UserTemp.Length; sensor++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.UserTempCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.UserTemp[sensor].ToFixed(cumulus.TempFormat, "-"));
					json.Append("\",\"&deg;");
					json.Append(cumulus.Units.TempText[1]);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraHum()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < MetData.ExtraHum.Length; sensor++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.ExtraHumCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.ExtraHum[sensor].ToFixed(cumulus.HumFormat, "-"));
					json.Append("\",\"%\"],");
				}
			}
			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetExtraDew()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < MetData.ExtraDewPoint.Length; sensor++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.ExtraDPCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.ExtraDewPoint[sensor].ToFixed(cumulus.TempFormat, "-"));
					json.Append("\",\"&deg;");
					json.Append(cumulus.Units.TempText[1]);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetLaserDepth()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < MetData.LaserDepth.Length; sensor++)
			{
				if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.LaserCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.LaserDepth[sensor].ToFixed(cumulus.LaserFormat, "-"));
					json.Append("\",\"");
					json.Append(cumulus.Units.LaserDistanceText);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetLaserDistance()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < MetData.LaserDist.Length; sensor++)
			{
				if (cumulus.GraphOptions.Visible.LaserDist.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.LaserCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.LaserDist[sensor].ToFixed(cumulus.LaserFormat, "-"));
					json.Append("\",\"");
					json.Append(cumulus.Units.LaserDistanceText);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetSnow24h()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < 5; sensor++)
			{
				if (cumulus.GraphOptions.Visible.CurrSnow24h.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.LaserCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append($"{MetData.Snow24h[sensor].ToFixed(cumulus.SnowFormat, "-")}");
					json.Append("\",\"");
					json.Append(cumulus.Units.SnowText);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetSnowSeason()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var sensor = 1; sensor < 5; sensor++)
			{
				if (cumulus.GraphOptions.Visible.CurrSnow24h.ValVisible(sensor - 1, true))
				{
					json.Append("[\"");
					json.Append(cumulus.Trans.LaserCaptions[sensor - 1]);
					json.Append("\",\"");
					json.Append(MetData.SnowSeason[sensor].ToFixed(cumulus.SnowFormat, "-"));
					json.Append("\",\"");
					json.Append(cumulus.Units.SnowText);
					json.Append("\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetBGTsensor(bool local)
		{
			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
			{
				var json = new StringBuilder("{\"data\":[", 1024);

				json.Append("[\"BGT\",\"");
				json.Append(MetData.BlackGlobeTemp.ToFixed(cumulus.TempFormat, "-"));
				json.Append("\",\"");
				json.Append(cumulus.Units.TempText);
				json.Append("\"],[\"WBGT\",\"");
				json.Append(MetData.WetBulbGlobeTemp.ToFixed(cumulus.TempFormat, "-"));
				json.Append("\",\"");
				json.Append(cumulus.Units.TempText);
				json.Append("\"]]}");
				return json.ToString();
			}
			else
			{
				return "{\"data\":[]}";
			}
		}

		public string GetSoilTemp()
		{
			var json = new StringBuilder("{\"data\":[", 2048);

			for (var i = 1; i < MetData.SoilTemp.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i - 1, true))
				{
					json.Append($"[\"{cumulus.Trans.SoilTempCaptions[i - 1]}\",\"{MetData.SoilTemp[i].ToFixed(cumulus.TempFormat, "-")}\",\"&deg;{cumulus.Units.TempText[1]}\"],");
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetSoilMoisture()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var i = 1; i < MetData.SoilMoisture.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i - 1, true))
					json.Append($"[\"{cumulus.Trans.SoilMoistureCaptions[i - 1]}\",\"{MetData.SoilMoisture[i].ToText("-")}\",\"{cumulus.Units.SoilMoistureUnitText[i - 1]}\"],");
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetSoilEc()
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			for (var i = 1; i < MetData.SoilEc.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i - 1, true))
					json.Append($"[\"{cumulus.Trans.SoilEcCaptions[i - 1]}\",\"{MetData.SoilEc[i].ToText("-")}\",\"μS/cm\"],");
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetAirQuality(bool local)
		{
			var json = new StringBuilder("{\"data\":[", 1024);
			if (cumulus.GraphOptions.Visible.AqSensor.IsVisible(local))
			{
				for (var i = 1; i < MetData.AirQuality.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.AqSensor.Pm.ValVisible(i - 1, local))
					{
						json.Append($"[\"{cumulus.Trans.AirQualityCaptions[i - 1]}\",\"{MetData.AirQuality[i].ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
					}
				}
				for (var i = 1; i < MetData.AirQualityAvg.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.AqSensor.PmAvg.ValVisible(i - 1, local))
					{
						json.Append($"[\"{cumulus.Trans.AirQualityAvgCaptions[i - 1]}\",\"{MetData.AirQualityAvg[i].ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
					}
				}
				for (var i = 1; i < MetData.AirQuality10.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.AqSensor.Pm10.ValVisible(i - 1, local))
					{
						json.Append($"[\"{cumulus.Trans.AirQuality10Captions[i - 1]}\",\"{MetData.AirQuality10[i].ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
					}
				}
				for (var i = 1; i < MetData.AirQuality10Avg.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.AqSensor.Pm10Avg.ValVisible(i - 1, local))
					{
						json.Append($"[\"{cumulus.Trans.AirQuality10AvgCaptions[i - 1]}\",\"{MetData.AirQuality10Avg[i].ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
					}
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetCO2sensor(bool local)
		{
			var json = new StringBuilder("{\"data\":[", 1024);

			if (cumulus.GraphOptions.Visible.CO2Sensor.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_CurrentCaption}\",\"{MetData.CO2.ToText("-")}\",\"{cumulus.Units.CO2UnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_24HourCaption}\",\"{MetData.CO2_24h.ToText("-")}\",\"{cumulus.Units.CO2UnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm2p5Caption}\",\"{MetData.CO2_pm2p5.ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm2p5_24hrCaption}\",\"{MetData.CO2_pm2p5_24h.ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm10Caption}\",\"{MetData.CO2_pm10.ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_pm10_24hrCaption}\",\"{MetData.CO2_pm10_24h.ToFixed("F1", "-")}\",\"{cumulus.Units.AirQualityUnitText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_TemperatureCaption}\",\"{MetData.CO2_temperature.ToFixed(cumulus.TempFormat, "-")}\",\"{cumulus.Units.TempText}\"],");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
					json.Append($"[\"{cumulus.Trans.CO2_HumidityCaption}\",\"{MetData.CO2_humidity.ToFixed("F0", "-")}\",\"%\"]");
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}

		public string GetLightning()
		{
			var json = new StringBuilder("{\"data\":[", 256);

			json.Append($"[\"Distance to last strike\",\"{(MetData.LightningDistance < 0 ? "-" : MetData.LightningDistance.ToString(cumulus.WindRunFormat))}\",\"{cumulus.Units.WindRunText}\"],");
			json.Append($"[\"Time of last strike\",\"{(DateTime.Equals(MetData.LightningTime, DateTime.MinValue) ? "-" : MetData.LightningTime.ToString("g"))}\",\"\"],");
			json.Append($"[\"Number of strikes today\",\"{MetData.LightningStrikesToday}\",\"\"]");
			json.Append("]}");
			return json.ToString();
		}

		public string GetLeaf8(bool local)
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.GraphOptions.Visible.LeafWetness.IsVisible(local))
			{
				for (var i = 1; i < MetData.LeafWetness.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i - 1, local))
					{
						json.Append($"[\"{cumulus.Trans.LeafWetnessCaptions[i - 1]}\",\"{MetData.LeafWetness[i].ToFixed(cumulus.LeafWetFormat, "-")}\",\"{cumulus.Units.LeafWetnessUnitText[i - 1]}\"],");
					}
				}
			}

			if (json[^1] == ',')
				json.Length--;

			json.Append("]}");
			return json.ToString();
		}


		public string GetAirLinkCountsOut()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkOut != null && cumulus.airLinkDataOut.dataValid)
			{
				json.Append($"[\"1 μm\",\"{cumulus.airLinkDataOut.pm1:F1}\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataOut.pm2p5:F1}\",\"{cumulus.airLinkDataOut.pm2p5_1hr:F1}\",\"{cumulus.airLinkDataOut.pm2p5_3hr:F1}\",\"{cumulus.airLinkDataOut.pm2p5_24hr:F1}\",\"{cumulus.airLinkDataOut.pm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataOut.pm10:F1}\",\"{cumulus.airLinkDataOut.pm10_1hr:F1}\",\"{cumulus.airLinkDataOut.pm10_3hr:F1}\",\"{cumulus.airLinkDataOut.pm10_24hr:F1}\",\"{cumulus.airLinkDataOut.pm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"1 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkAqiOut()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkOut != null && cumulus.airLinkDataOut.dataValid)
			{
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataOut.aqiPm2p5:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_1hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_3hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_24hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataOut.aqiPm10:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_1hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_3hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_24hr:F1}\",\"{cumulus.airLinkDataOut.aqiPm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkPctOut()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkOut != null && cumulus.airLinkDataOut.dataValid)
			{
				json.Append($"[\"All sizes\",\"--\",\"{cumulus.airLinkDataOut.pct_1hr}%\",\"{cumulus.airLinkDataOut.pct_3hr}%\",\"{cumulus.airLinkDataOut.pct_24hr}%\",\"{cumulus.airLinkDataOut.pct_nowcast}%\"]");
			}
			else
			{
				json.Append("[\"All sizes\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkCountsIn()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkIn != null && cumulus.airLinkDataIn.dataValid)
			{
				json.Append($"[\"1 μm\",\"{cumulus.airLinkDataIn.pm1:F1}\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataIn.pm2p5:F1}\",\"{cumulus.airLinkDataIn.pm2p5_1hr:F1}\",\"{cumulus.airLinkDataIn.pm2p5_3hr:F1}\",\"{cumulus.airLinkDataIn.pm2p5_24hr:F1}\",\"{cumulus.airLinkDataIn.pm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataIn.pm10:F1}\",\"{cumulus.airLinkDataIn.pm10_1hr:F1}\",\"{cumulus.airLinkDataIn.pm10_3hr:F1}\",\"{cumulus.airLinkDataIn.pm10_24hr:F1}\",\"{cumulus.airLinkDataIn.pm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"1 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkAqiIn()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkIn != null && cumulus.airLinkDataIn.dataValid)
			{
				json.Append($"[\"2.5 μm\",\"{cumulus.airLinkDataIn.aqiPm2p5:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_1hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_3hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_24hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm2p5_nowcast:F1}\"],");
				json.Append($"[\"10 μm\",\"{cumulus.airLinkDataIn.aqiPm10:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_1hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_3hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_24hr:F1}\",\"{cumulus.airLinkDataIn.aqiPm10_nowcast:F1}\"]");
			}
			else
			{
				json.Append("[\"2.5 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"],");
				json.Append("[\"10 μm\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}

		public string GetAirLinkPctIn()
		{
			var json = new StringBuilder("{\"data\":[", 256);
			if (cumulus.airLinkIn != null && cumulus.airLinkDataIn.dataValid)
			{
				json.Append($"[\"All sizes\",\"--\",\"{cumulus.airLinkDataIn.pct_1hr}%\",\"{cumulus.airLinkDataIn.pct_3hr}%\",\"{cumulus.airLinkDataIn.pct_24hr}%\",\"{cumulus.airLinkDataIn.pct_nowcast}%\"]");
			}
			else
			{
				json.Append("[\"All sizes\",\"--\",\"--\",\"--\",\"--\",\"--\"]");
			}
			json.Append("]}");
			return json.ToString();
		}
	}
}
