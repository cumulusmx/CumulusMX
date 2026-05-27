using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CumulusMX
{
	public partial class Cumulus
	{
		public string GetGraphConfig(bool local)
		{
			var timeformat = ProgramOptions.TimeFormat switch
			{
				"t" => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H') ? "%H:%M" : "%l:%M %p",
				"h:mm tt" => "%l:%M %p",
				"HH:MM" => "%H:%M",
				_ => "%H:%M"
			};

			var json = new StringBuilder(200);
			json.Append('{');
			json.Append($"\"tz\":\"{StationOptions.TimeZoneId}\",");
			json.Append($"\"locale\":\"{(string.IsNullOrWhiteSpace(CultureInfo.CurrentCulture.Name) ? "en-US" : CultureInfo.CurrentCulture.Name)}\","); // Handle Invariant Culture as the running language
			json.Append($"\"timeformat\":\"{timeformat}\",");
			json.Append($"\"temp\":{{\"units\":\"{Units.TempText[1]}\",\"decimals\":{TempDPlaces}}},");
			json.Append($"\"wind\":{{\"units\":\"{Units.WindText}\",\"avgdecimals\":{WindAvgDPlaces},\"gustdecimals\":{WindDPlaces},\"rununits\":\"{Units.WindRunText}\"}},");
			json.Append($"\"rain\":{{\"units\":\"{Units.RainText}\",\"decimals\":{RainDPlaces}}},");
			json.Append($"\"press\":{{\"units\":\"{Units.PressText}\",\"decimals\":{PressDPlaces}}},");
			json.Append($"\"hum\":{{\"decimals\":{HumDPlaces}}},");
			json.Append($"\"uv\":{{\"decimals\":{UVDPlaces}}},");
			json.Append($"\"snow\":{{\"units\":\"{Units.SnowText}\",\"decimals\":1}},");
			json.Append($"\"soilmoisture\":{{\"units\":[\"{string.Join("\",\"", Units.SoilMoistureUnitText)}\"]}},");
			json.Append($"\"co2\":{{\"units\":\"{Units.CO2UnitText}\"}},");
			json.Append($"\"leafwet\":{{\"units\":\"{Units.LeafWetnessUnitText} \",\"decimals\": {LeafWetDPlaces}}},");
			json.Append($"\"aq\":{{\"units\":\"{Units.AirQualityUnitText}\"}},");
			json.Append($"\"laser\":{{\"units\":\"{Units.LaserDistanceText} \",\"decimals\": {LaserDPlaces}}},");

			#region data series

			json.Append("\"series\":{");

			#region recent

			// temp
			if (GraphOptions.Visible.Temp.IsVisible(local))
				json.Append($"\"temp\":{{\"name\":\"{Trans.DataCaptions["Temp"]}\",\"colour\":\"{GraphOptions.Colour.Temp}\"}},");
			if (GraphOptions.Visible.AppTemp.IsVisible(local))
				json.Append($"\"apptemp\":{{\"name\":\"{Trans.DataCaptions["AppTemp"]}\",\"colour\":\"{GraphOptions.Colour.AppTemp}\"}},");
			if (GraphOptions.Visible.FeelsLike.IsVisible(local))
				json.Append($"\"feelslike\":{{\"name\":\"{Trans.DataCaptions["FeelsLike"]}\",\"colour\":\"{GraphOptions.Colour.FeelsLike}\"}},");
			if (GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append($"\"wchill\":{{\"name\":\"{Trans.DataCaptions["WindChill"]}\",\"colour\":\"{GraphOptions.Colour.WindChill}\"}},");
			if (GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append($"\"heatindex\":{{\"name\":\"{Trans.DataCaptions["HeatInd"]}\",\"colour\":\"{GraphOptions.Colour.HeatIndex}\"}},");
			if (GraphOptions.Visible.DewPoint.IsVisible(local))
				json.Append($"\"dew\":{{\"name\":\"{Trans.DataCaptions["DewPnt"]}\",\"colour\":\"{GraphOptions.Colour.DewPoint}\"}},");
			if (GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append($"\"humidex\":{{\"name\":\"{Trans.DataCaptions["Humidex"]}\",\"colour\":\"{GraphOptions.Colour.Humidex}\"}},");
			if (GraphOptions.Visible.InTemp.IsVisible(local))
				json.Append($"\"intemp\":{{\"name\":\"{Trans.DataCaptions["TempIn"]}\",\"colour\":\"{GraphOptions.Colour.InTemp}\"}},");
			if (GraphOptions.Visible.BGT.IsVisible(local))
			{
				json.Append($"\"bgt\":{{\"name\":\"BGT\",\"colour\":\"{GraphOptions.Colour.BGT}\"}},");
				json.Append($"\"wbgt\":{{\"name\":\"WBGT\",\"colour\":\"{GraphOptions.Colour.WBGT}\"}},");
			}
			// hum
			if (GraphOptions.Visible.OutHum.IsVisible(local))
				json.Append($"\"hum\":{{\"name\":\"{Trans.DataCaptions["Hum"]}\",\"colour\":\"{GraphOptions.Colour.OutHum}\"}},");
			if (GraphOptions.Visible.InHum.IsVisible(local))
				json.Append($"\"inhum\":{{\"name\":\"{Trans.DataCaptions["HumIn"]}\",\"colour\":\"{GraphOptions.Colour.InHum}\"}},");
			// press
			json.Append($"\"press\":{{\"name\":\"{Trans.DataCaptions["Press"]}\",\"colour\":\"{GraphOptions.Colour.Press}\"}},");
			// wind
			json.Append($"\"wspeed\":{{\"name\":\"{Trans.DataCaptions["WindSpeed"]}\",\"colour\":\"{GraphOptions.Colour.WindAvg}\"}},");
			json.Append($"\"wgust\":{{\"name\":\"{Trans.DataCaptions["WindGust"]}\",\"colour\":\"{GraphOptions.Colour.WindGust}\"}},");
			json.Append($"\"windrun\":{{\"name\":\"{Trans.DataCaptions["WindRun"]}\",\"colour\":\"{GraphOptions.Colour.WindRun}\"}},");
			json.Append($"\"bearing\":{{\"name\":\"{Trans.DataCaptions["LatestBearing"]}\",\"colour\":\"{GraphOptions.Colour.WindBearing}\"}},");
			json.Append($"\"avgbearing\":{{\"name\":\"{Trans.DataCaptions["AvgBearing"]}\",\"colour\":\"{GraphOptions.Colour.WindBearingAvg}\"}},");
			// rain
			json.Append($"\"rfall\":{{\"name\":\"{Trans.DataCaptions["TotalRain"]}\",\"colour\":\"{GraphOptions.Colour.Rainfall}\"}},");
			json.Append($"\"rrate\":{{\"name\":\"{Trans.DataCaptions["RainRate"]}\",\"colour\":\"{GraphOptions.Colour.RainRate}\"}},");
			// solar
			if (GraphOptions.Visible.Solar.IsVisible(local))
				json.Append($"\"solarrad\":{{\"name\":\"{Trans.DataCaptions["SolarIrrad"]}\",\"colour\":\"{GraphOptions.Colour.Solar}\"}},");
			json.Append($"\"currentsolarmax\":{{\"name\":\"Solar theoretical\",\"colour\":\"{GraphOptions.Colour.SolarTheoretical}\"}},");
			if (GraphOptions.Visible.UV.IsVisible(local))
				json.Append($"\"uv\":{{\"name\":\"UV-I\",\"colour\":\"{GraphOptions.Colour.UV}\"}},");
			if (GraphOptions.Visible.Sunshine.IsVisible(local))
				json.Append($"\"sunshine\":{{\"name\":\"{Trans.DataCaptions["SunshineHrs"]}\",\"colour\":\"{GraphOptions.Colour.Sunshine}\"}},");
			// aq
			json.Append($"\"pm2p5\":{{\"name\":\"PM 2.5\",\"colour\":\"{GraphOptions.Colour.Pm2p5}\"}},");
			json.Append($"\"pm10\":{{\"name\":\"PM 10\",\"colour\":\"{GraphOptions.Colour.Pm10}\"}},");

			#endregion recent

			#region daily

			// growing deg days
			if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
				json.Append($"\"growingdegreedays1\":{{\"name\":\"GDD#1\"}},");
			if (GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
				json.Append($"\"growingdegreedays2\":{{\"name\":\"GDD#2\"}},");

			// temp sum
			if (GraphOptions.Visible.TempSum0.IsVisible(local))
				json.Append($"\"tempsum0\":{{\"name\":\"Temp Sum#0\"}},");
			if (GraphOptions.Visible.TempSum1.IsVisible(local))
				json.Append($"\"tempsum1\":{{\"name\":\"Temp Sum#1\"}},");
			if (GraphOptions.Visible.TempSum2.IsVisible(local))
				json.Append($"\"tempsum2\":{{\"name\":\"Temp Sum#2\"}},");

			// chill hours
			if (GraphOptions.Visible.ChillHours.IsVisible(local))
				json.Append($"\"chillhours\":{{\"name\":\"{Trans.DataCaptions["ChillHrs"]}\"}},");

			// daily temps
			if (GraphOptions.Visible.AvgTemp.IsVisible(local))
				json.Append($"\"avgtemp\":{{\"name\":\"{Trans.DataCaptions["AvgTemp"]}\",\"colour\":\"{GraphOptions.Colour.AvgTemp}\"}},");
			if (GraphOptions.Visible.MaxTemp.IsVisible(local))
				json.Append($"\"maxtemp\":{{\"name\":\"{Trans.DataCaptions["HiTemp"]}\",\"colour\":\"{GraphOptions.Colour.MaxTemp}\"}},");
			if (GraphOptions.Visible.MinTemp.IsVisible(local))
				json.Append($"\"mintemp\":{{\"name\":\"{Trans.DataCaptions["LoTemp"]}\",\"colour\":\"{GraphOptions.Colour.MinTemp}\"}},");

			json.Append($"\"maxpress\":{{\"name\":\"{Trans.DataCaptions["HiPress"]}\",\"colour\":\"{GraphOptions.Colour.MaxPress}\"}},");
			json.Append($"\"minpress\":{{\"name\":\"{Trans.DataCaptions["LoPress"]}\",\"colour\":\"{GraphOptions.Colour.MinPress}\"}},");

			json.Append($"\"maxhum\":{{\"name\":\"{Trans.DataCaptions["HiHum"]}\",\"colour\":\"{GraphOptions.Colour.MaxOutHum}\"}},");
			json.Append($"\"minhum\":{{\"name\":\"{Trans.DataCaptions["LoHum"]}\",\"colour\":\"{GraphOptions.Colour.MinOutHum}\"}},");

			if (GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				json.Append($"\"mindew\":{{\"name\":\"{Trans.DataCaptions["LoDewPnt"]}\",\"colour\":\"{GraphOptions.Colour.MinDew}\"}},");
				json.Append($"\"maxdew\":{{\"name\":\"{Trans.DataCaptions["HiDewPnt"]}\",\"colour\":\"{GraphOptions.Colour.MaxDew}\"}},");
			}
			if (GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append($"\"minwindchill\":{{\"name\":\"{Trans.DataCaptions["LoWindChill"]}\",\"colour\":\"{GraphOptions.Colour.MinWindChill}\"}},");
			if (GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				json.Append($"\"minapp\":{{\"name\":\"{Trans.DataCaptions["LoAppTemp"]}\",\"colour\":\"{GraphOptions.Colour.MinApp}\"}},");
				json.Append($"\"maxapp\":{{\"name\":\"{Trans.DataCaptions["HiAppTemp"]}\",\"colour\":\"{GraphOptions.Colour.MaxApp}\"}},");
			}
			if (GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				json.Append($"\"minfeels\":{{\"name\":\"{Trans.DataCaptions["LoFeelsLike"]}\",\"colour\":\"{GraphOptions.Colour.MinFeels}\"}},");
				json.Append($"\"maxfeels\":{{\"name\":\"{Trans.DataCaptions["HiFeelsLike"]}\",\"colour\":\"{GraphOptions.Colour.MaxFeels}\"}},");
			}
			if (GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append($"\"maxheatindex\":{{\"name\":\"{Trans.DataCaptions["HiHeatInd"]}\",\"colour\":\"{GraphOptions.Colour.MaxHeatIndex}\"}},");
			if (GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append($"\"maxhumidex\":{{\"name\":\"{Trans.DataCaptions["HiHumidex"]}\",\"colour\":\"{GraphOptions.Colour.MaxHumidex}\"}},");

			// Snow
			if (GraphOptions.Visible.SnowDepth.IsVisible(local))
				json.Append($"\"snowdepth\":{{\"name\":\"{Trans.SnowDepth}\",\"colour\":\"{GraphOptions.Colour.SnowDepth}\"}},");
			if (GraphOptions.Visible.Snow24h.IsVisible(local))
				json.Append($"\"snow24h\":{{\"name\":\"{Trans.Snow24h}\",\"colour\":\"{GraphOptions.Colour.Snow24h}\"}},");

			#endregion daily

			#region extra sensors

			// extra temp
			if (GraphOptions.Visible.ExtraTemp.IsVisible(local))
				json.Append($"\"extratemp\":{{\"name\":[\"{string.Join("\",\"", Trans.ExtraTempCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.ExtraTemp)}\"]}},");
			// extra hum
			if (GraphOptions.Visible.ExtraHum.IsVisible(local))
				json.Append($"\"extrahum\":{{\"name\":[\"{string.Join("\",\"", Trans.ExtraHumCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.ExtraHum)}\"]}},");
			// extra dewpoint
			if (GraphOptions.Visible.ExtraDewPoint.IsVisible(local))
				json.Append($"\"extradew\":{{\"name\":[\"{string.Join("\",\"", Trans.ExtraDPCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.ExtraDewPoint)}\"]}},");
			// extra user temps
			if (GraphOptions.Visible.UserTemp.IsVisible(local))
				json.Append($"\"usertemp\":{{\"name\":[\"{string.Join("\",\"", Trans.UserTempCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.UserTemp)}\"]}},");
			// soil temps
			if (GraphOptions.Visible.SoilTemp.IsVisible(local))
				json.Append($"\"soiltemp\":{{\"name\":[\"{string.Join("\",\"", Trans.SoilTempCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.SoilTemp)}\"]}},");
			// soil temps
			if (GraphOptions.Visible.SoilMoist.IsVisible(local))
				json.Append($"\"soilmoist\":{{\"name\":[\"{string.Join("\",\"", Trans.SoilMoistureCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.SoilMoist)}\"]}},");
			// soil EC
			if (GraphOptions.Visible.SoilEc.IsVisible(local))
				json.Append($"\"soilec\":{{\"name\":[\"{string.Join("\",\"", Trans.SoilEcCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.SoilEc)}\"]}},");
			// leaf wetness
			if (GraphOptions.Visible.LeafWetness.IsVisible(local))
				json.Append($"\"leafwet\":{{\"name\":[\"{string.Join("\",\"", Trans.LeafWetnessCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.LeafWetness)}\"]}},");
			// laser depth
			if (GraphOptions.Visible.LaserDepth.IsVisible(local))
				json.Append($"\"laserdepth\":{{\"name\":[\"{string.Join("\",\"", Trans.LaserCaptions)}\"],\"colour\":[\"{string.Join("\",\"", GraphOptions.Colour.LaserDepth)}\"]}},");

			// CO2
			json.Append("\"co2\":{");
			if (GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
				json.Append($"\"co2\":{{\"name\":\"{Trans.CO2_CurrentCaption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.CO2}\"}},");
			if (GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
				json.Append($"\"co2average\":{{\"name\":\"{Trans.CO2_24HourCaption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.CO2Avg}\"}},");
			if (GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
				json.Append($"\"pm10\":{{\"name\":\"{Trans.CO2_pm10Caption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.Pm10}\"}},");
			if (GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
				json.Append($"\"pm10average\":{{\"name\":\"{Trans.CO2_pm10_24hrCaption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.Pm10Avg}\"}},");
			if (GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
				json.Append($"\"pm2.5\":{{\"name\":\"{Trans.CO2_pm2p5Caption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.Pm25}\"}},");
			if (GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
				json.Append($"\"pm2.5average\":{{\"name\":\"{Trans.CO2_pm2p5_24hrCaption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.Pm25Avg}\"}},");
			if (GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
				json.Append($"\"humidity\":{{\"name\":\"{Trans.CO2_HumidityCaption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.Hum}\"}},");
			if (GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
				json.Append($"\"temperature\":{{\"name\":\"{Trans.CO2_TemperatureCaption}\",\"colour\":\"{GraphOptions.Colour.CO2Sensor.Temp}\"}}");
			// remove trailing comma
			if (json[^1] == ',')
				json.Length--;
			json.Append("},");

			#endregion extra sensors

			// remove trailing comma
			json.Length--;
			json.Append('}');

			#endregion data series

			json.Append('}');
			return json.ToString();
		}

		public string GetAvailGraphData(bool local)
		{
			var json = new StringBuilder(200);

			// Temp values
			json.Append("{\"Temperature\":[");

			if (GraphOptions.Visible.Temp.IsVisible(local))
				json.Append("\"Temperature\",");

			if (GraphOptions.Visible.InTemp.IsVisible(local))
				json.Append("\"Indoor Temp\",");

			if (GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append("\"Heat Index\",");

			if (GraphOptions.Visible.DewPoint.IsVisible(local))
				json.Append("\"Dew Point\",");

			if (GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append("\"Wind Chill\",");

			if (GraphOptions.Visible.AppTemp.IsVisible(local))
				json.Append("\"Apparent Temp\",");

			if (GraphOptions.Visible.FeelsLike.IsVisible(local))
				json.Append("\"Feels Like\",");

			if (GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append("\"Humidex\",");

			if (GraphOptions.Visible.BGT.IsVisible(local))
				json.Append("\"BGT\",\"WBGT\",");

			if (json[^1] == ',')
				json.Length--;

			// humidity values
			json.Append("],\"Humidity\":[");

			if (GraphOptions.Visible.OutHum.IsVisible(local))
				json.Append("\"Humidity\",");

			if (GraphOptions.Visible.InHum.IsVisible(local))
				json.Append("\"Indoor Hum\",");

			if (json[^1] == ',')
				json.Length--;

			// fixed values
			// pressure
			json.Append("],\"Pressure\":[\"Pressure\"],");

			// wind
			json.Append("\"Wind\":[\"Wind Speed\",\"Wind Gust\",\"Wind Bearing\"],");

			// rain
			json.Append("\"Rain\":[\"Rainfall\",\"Rainfall Rate\"]");

			if (GraphOptions.Visible.AvgTemp.IsVisible(local) || GraphOptions.Visible.MaxTemp.IsVisible(local) || GraphOptions.Visible.MinTemp.IsVisible(local))
			{
				json.Append(",\"DailyTemps\":[");

				if (GraphOptions.Visible.AvgTemp.IsVisible(local))
					json.Append("\"AvgTemp\",");
				if (GraphOptions.Visible.MaxTemp.IsVisible(local))
					json.Append("\"MaxTemp\",");
				if (GraphOptions.Visible.MinTemp.IsVisible(local))
					json.Append("\"MinTemp\",");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}


			// solar values
			if (GraphOptions.Visible.Solar.IsVisible(local) || GraphOptions.Visible.UV.IsVisible(local))
			{
				json.Append(",\"Solar\":[");

				if (GraphOptions.Visible.Solar.IsVisible(local))
					json.Append("\"Solar Rad\",");

				if (GraphOptions.Visible.UV.IsVisible(local))
					json.Append("\"UV Index\",");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Sunshine
			if (GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				json.Append(",\"Sunshine\":[\"sunhours\"]");
			}

			// air quality
			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (StationOptions.PrimaryAqSensor > (int) PrimaryAqSensor.Undefined
				&& StationOptions.PrimaryAqSensor != (int) PrimaryAqSensor.AirLinkIndoor)
			{
				json.Append(",\"AirQuality\":[");
				json.Append("\"PM 2.5\"");

				// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
				if (StationOptions.PrimaryAqSensor == (int) PrimaryAqSensor.AirLinkOutdoor ||
					StationOptions.PrimaryAqSensor == (int) PrimaryAqSensor.AirLinkIndoor ||
					StationOptions.PrimaryAqSensor == (int) PrimaryAqSensor.EcowittCO2)
				{
					json.Append(",\"PM 10\"");
				}
				json.Append(']');
			}

			// Degree Days
			if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) || GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
			{
				json.Append(",\"DegreeDays\":[");
				if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
					json.Append("\"GDD1\",");

				if (GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
					json.Append("\"GDD2\"");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Temp Sum
			if (GraphOptions.Visible.TempSum0.IsVisible(local) || GraphOptions.Visible.TempSum1.IsVisible(local) || GraphOptions.Visible.TempSum2.IsVisible(local))
			{
				json.Append(",\"TempSum\":[");
				if (GraphOptions.Visible.TempSum0.IsVisible(local))
					json.Append("\"Sum0\",");
				if (GraphOptions.Visible.TempSum1.IsVisible(local))
					json.Append("\"Sum1\",");
				if (GraphOptions.Visible.TempSum2.IsVisible(local))
					json.Append("\"Sum2\"");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Chill Hours
			if (GraphOptions.Visible.ChillHours.IsVisible(local))
			{
				json.Append(",\"ChillHours\":[\"Chill Hours\"]");
			}

			// Extra temperature
			if (GraphOptions.Visible.ExtraTemp.IsVisible(local))
			{
				json.Append(",\"ExtraTemp\":[");
				for (var i = 0; i < GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
				{
					if (GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
						json.Append($"\"{Trans.ExtraTempCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra humidity
			if (GraphOptions.Visible.ExtraHum.IsVisible(local))
			{
				json.Append(",\"ExtraHum\":[");
				for (var i = 0; i < GraphOptions.Visible.ExtraHum.Vals.Length; i++)
				{
					if (GraphOptions.Visible.ExtraHum.ValVisible(i, local))
						json.Append($"\"{Trans.ExtraHumCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra dew point
			if (GraphOptions.Visible.ExtraDewPoint.IsVisible(local))
			{
				json.Append(",\"ExtraDewPoint\":[");
				for (var i = 0; i < GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
				{
					if (GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
						json.Append($"\"{Trans.ExtraDPCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}


			// Soil Temp
			if (GraphOptions.Visible.SoilTemp.IsVisible(local))
			{
				json.Append(",\"SoilTemp\":[");
				for (var i = 0; i < GraphOptions.Visible.SoilTemp.Vals.Length; i++)
				{
					if (GraphOptions.Visible.SoilTemp.ValVisible(i, local))
						json.Append($"\"{Trans.SoilTempCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Soil Moisture
			if (GraphOptions.Visible.SoilMoist.IsVisible(local))
			{
				json.Append(",\"SoilMoist\":[");
				for (var i = 0; i < GraphOptions.Visible.SoilMoist.Vals.Length; i++)
				{
					if (GraphOptions.Visible.SoilMoist.ValVisible(i, local))
						json.Append($"\"{Trans.SoilMoistureCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Soil EC
			if (GraphOptions.Visible.SoilEc.IsVisible(local))
			{
				json.Append(",\"SoilEc\":[");
				for (var i = 0; i < GraphOptions.Visible.SoilEc.Vals.Length; i++)
				{
					if (GraphOptions.Visible.SoilEc.ValVisible(i, local))
						json.Append($"\"{Trans.SoilEcCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// User Temp
			if (GraphOptions.Visible.UserTemp.IsVisible(local))
			{
				json.Append(",\"UserTemp\":[");
				for (var i = 0; i < GraphOptions.Visible.UserTemp.Vals.Length; i++)
				{
					if (GraphOptions.Visible.UserTemp.ValVisible(i, local))
						json.Append($"\"{Trans.UserTempCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Leaf wetness
			if (GraphOptions.Visible.LeafWetness.IsVisible(local))
			{
				json.Append(",\"LeafWetness\":[");
				for (var i = 0; i < GraphOptions.Visible.LeafWetness.Vals.Length; i++)
				{
					if (GraphOptions.Visible.LeafWetness.ValVisible(i, local))
						json.Append($"\"{Trans.LeafWetnessCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Snow
			if (GraphOptions.Visible.SnowDepth.IsVisible(local) || GraphOptions.Visible.Snow24h.IsVisible(local))
			{
				json.Append(",\"Snow\":[");
				//if (GraphOptions.Visible.SnowDepth.IsVisible(local))
				//	json.Append($"\"{Trans.SnowDepth}\",");
				if (GraphOptions.Visible.Snow24h.IsVisible(local))
					json.Append($"\"{Trans.Snow24h}\"");
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// CO2
			if (GraphOptions.Visible.CO2Sensor.IsVisible(local))
			{
				json.Append(",\"CO2\":[");
				if (GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
					json.Append($"\"{Trans.CO2_CurrentCaption}\",");
				if (GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
					json.Append($"\"{Trans.CO2_24HourCaption}\",");
				if (GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
					json.Append($"\"{Trans.CO2_pm2p5Caption}\",");
				if (GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
					json.Append($"\"{Trans.CO2_pm2p5_24hrCaption}\",");
				if (GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
					json.Append($"\"{Trans.CO2_pm10Caption}\",");
				if (GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
					json.Append($"\"{Trans.CO2_pm10_24hrCaption}\",");
				if (GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
					json.Append($"\"{Trans.CO2_TemperatureCaption}\",");
				if (GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
					json.Append($"\"{Trans.CO2_HumidityCaption}\"");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// LASER depth
			if (GraphOptions.Visible.LaserDepth.IsVisible(local))
			{
				json.Append(",\"LaserDepth\":[");
				for (var i = 0; i < GraphOptions.Visible.LaserDepth.Vals.Length; i++)
				{
					if (GraphOptions.Visible.LaserDepth.ValVisible(i, local))
						json.Append($"\"{Trans.LaserCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			json.Append('}');
			return json.ToString();
		}


		public string GetSelectaChartOptions()
		{
			return JsonSerializer.Serialize(SelectaChartOptions);
		}

		public string GetSelectaPeriodOptions()
		{
			return JsonSerializer.Serialize(SelectaPeriodOptions);
		}
	}
}
