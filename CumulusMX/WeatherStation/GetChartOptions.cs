using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public string GetGraphConfig(bool local)
		{
			var timeformat = cumulus.ProgramOptions.TimeFormat switch
			{
				"t" => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H') ? "%H:%M" : "%l:%M %p",
				"h:mm tt" => "%l:%M %p",
				"HH:MM" => "%H:%M",
				_ => "%H:%M"
			};

			var json = new StringBuilder(200);
			json.Append('{');
			json.Append($"\"tz\":\"{cumulus.StationOptions.TimeZoneId}\",");
			json.Append($"\"locale\":\"{(string.IsNullOrWhiteSpace(CultureInfo.CurrentCulture.Name) ? "en-US" : CultureInfo.CurrentCulture.Name)}\","); // Handle Invariant Culture as the running language
			json.Append($"\"timeformat\":\"{timeformat}\",");
			json.Append($"\"temp\":{{\"units\":\"{cumulus.Units.TempText[1]}\",\"decimals\":{cumulus.TempDPlaces}}},");
			json.Append($"\"wind\":{{\"units\":\"{cumulus.Units.WindText}\",\"avgdecimals\":{cumulus.WindAvgDPlaces},\"gustdecimals\":{cumulus.WindDPlaces},\"rununits\":\"{cumulus.Units.WindRunText}\"}},");
			json.Append($"\"rain\":{{\"units\":\"{cumulus.Units.RainText}\",\"decimals\":{cumulus.RainDPlaces}}},");
			json.Append($"\"press\":{{\"units\":\"{cumulus.Units.PressText}\",\"decimals\":{cumulus.PressDPlaces}}},");
			json.Append($"\"hum\":{{\"decimals\":{cumulus.HumDPlaces}}},");
			json.Append($"\"uv\":{{\"decimals\":{cumulus.UVDPlaces}}},");
			json.Append($"\"snow\":{{\"units\":\"{cumulus.Units.SnowText}\",\"decimals\":1}},");
			json.Append($"\"soilmoisture\":{{\"units\":[\"{string.Join("\",\"", cumulus.Units.SoilMoistureUnitText)}\"]}},");
			json.Append($"\"co2\":{{\"units\":\"{cumulus.Units.CO2UnitText}\"}},");
			json.Append($"\"leafwet\":{{\"units\":\"{cumulus.Units.LeafWetnessUnitText}\",\"decimals\":{cumulus.LeafWetDPlaces}}},");
			json.Append($"\"aq\":{{\"units\":\"{cumulus.Units.AirQualityUnitText}\"}},");
			json.Append($"\"laser\":{{\"units\":\"{cumulus.Units.LaserDistanceText}\",\"decimals\":{cumulus.LaserDPlaces}}},");

			#region data series

			json.Append("\"series\":{");

			#region recent

			// temp
			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
				json.Append($"\"temp\":{{\"name\":\"{cumulus.Trans.DataCaptions["Temp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.Temp}\"}},");
			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
				json.Append($"\"apptemp\":{{\"name\":\"{cumulus.Trans.DataCaptions["AppTemp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.AppTemp}\"}},");
			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
				json.Append($"\"feelslike\":{{\"name\":\"{cumulus.Trans.DataCaptions["FeelsLike"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.FeelsLike}\"}},");
			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append($"\"wchill\":{{\"name\":\"{cumulus.Trans.DataCaptions["WindChill"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.WindChill}\"}},");
			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append($"\"heatindex\":{{\"name\":\"{cumulus.Trans.DataCaptions["HeatInd"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.HeatIndex}\"}},");
			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
				json.Append($"\"dew\":{{\"name\":\"{cumulus.Trans.DataCaptions["DewPnt"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.DewPoint}\"}},");
			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append($"\"humidex\":{{\"name\":\"{cumulus.Trans.DataCaptions["Humidex"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.Humidex}\"}},");
			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
				json.Append($"\"intemp\":{{\"name\":\"{cumulus.Trans.DataCaptions["TempIn"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.InTemp}\"}},");
			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
			{
				json.Append($"\"bgt\":{{\"name\":\"BGT\",\"colour\":\"{cumulus.GraphOptions.Colour.BGT}\"}},");
				json.Append($"\"wbgt\":{{\"name\":\"WBGT\",\"colour\":\"{cumulus.GraphOptions.Colour.WBGT}\"}},");
			}
			// hum
			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				json.Append($"\"hum\":{{\"name\":\"{cumulus.Trans.DataCaptions["Hum"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.OutHum}\"}},");
			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
				json.Append($"\"inhum\":{{\"name\":\"{cumulus.Trans.DataCaptions["HumIn"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.InHum}\"}},");
			// press
			json.Append($"\"press\":{{\"name\":\"{cumulus.Trans.DataCaptions["Press"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.Press}\"}},");
			// wind
			json.Append($"\"wspeed\":{{\"name\":\"{cumulus.Trans.DataCaptions["WindSpeed"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.WindAvg}\"}},");
			json.Append($"\"wgust\":{{\"name\":\"{cumulus.Trans.DataCaptions["WindGust"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.WindGust}\"}},");
			json.Append($"\"windrun\":{{\"name\":\"{cumulus.Trans.DataCaptions["WindRun"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.WindRun}\"}},");
			json.Append($"\"bearing\":{{\"name\":\"{cumulus.Trans.DataCaptions["LatestBearing"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.WindBearing}\"}},");
			json.Append($"\"avgbearing\":{{\"name\":\"{cumulus.Trans.DataCaptions["AvgBearing"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.WindBearingAvg}\"}},");
			// rain
			json.Append($"\"rfall\":{{\"name\":\"{cumulus.Trans.DataCaptions["TotalRain"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.Rainfall}\"}},");
			json.Append($"\"rrate\":{{\"name\":\"{cumulus.Trans.DataCaptions["RainRate"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.RainRate}\"}},");
			// solar
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
				json.Append($"\"solarrad\":{{\"name\":\"{cumulus.Trans.DataCaptions["SolarIrrad"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.Solar}\"}},");
			json.Append($"\"currentsolarmax\":{{\"name\":\"Solar theoretical\",\"colour\":\"{cumulus.GraphOptions.Colour.SolarTheoretical}\"}},");
			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				json.Append($"\"uv\":{{\"name\":\"UV-I\",\"colour\":\"{cumulus.GraphOptions.Colour.UV}\"}},");
			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
				json.Append($"\"sunshine\":{{\"name\":\"{cumulus.Trans.DataCaptions["SunshineHrs"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.Sunshine}\"}},");
			// aq
			json.Append($"\"pm2p5\":{{\"name\":\"PM 2.5\",\"colour\":\"{cumulus.GraphOptions.Colour.Pm2p5}\"}},");
			json.Append($"\"pm10\":{{\"name\":\"PM 10\",\"colour\":\"{cumulus.GraphOptions.Colour.Pm10}\"}},");

			#endregion recent

			#region daily

			// growing deg days
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
				json.Append($"\"growingdegreedays1\":{{\"name\":\"GDD#1\"}},");
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
				json.Append($"\"growingdegreedays2\":{{\"name\":\"GDD#2\"}},");

			// temp sum
			if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local))
				json.Append($"\"tempsum0\":{{\"name\":\"Temp Sum#0\"}},");
			if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
				json.Append($"\"tempsum1\":{{\"name\":\"Temp Sum#1\"}},");
			if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
				json.Append($"\"tempsum2\":{{\"name\":\"Temp Sum#2\"}},");

			// chill hours
			if (cumulus.GraphOptions.Visible.ChillHours.IsVisible(local))
				json.Append($"\"chillhours\":{{\"name\":\"{cumulus.Trans.DataCaptions["ChillHrs"]}\"}},");

			// daily temps
			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
				json.Append($"\"avgtemp\":{{\"name\":\"{cumulus.Trans.DataCaptions["AvgTemp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.AvgTemp}\"}},");
			if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
				json.Append($"\"maxtemp\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiTemp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxTemp}\"}},");
			if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
				json.Append($"\"mintemp\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoTemp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinTemp}\"}},");

			json.Append($"\"maxpress\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiPress"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxPress}\"}},");
			json.Append($"\"minpress\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoPress"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinPress}\"}},");

			json.Append($"\"maxhum\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiHum"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxOutHum}\"}},");
			json.Append($"\"minhum\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoHum"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinOutHum}\"}},");

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				json.Append($"\"mindew\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoDewPnt"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinDew}\"}},");
				json.Append($"\"maxdew\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiDewPnt"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxDew}\"}},");
			}
			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append($"\"minwindchill\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoWindChill"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinWindChill}\"}},");
			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				json.Append($"\"minapp\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoAppTemp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinApp}\"}},");
				json.Append($"\"maxapp\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiAppTemp"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxApp}\"}},");
			}
			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				json.Append($"\"minfeels\":{{\"name\":\"{cumulus.Trans.DataCaptions["LoFeelsLike"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MinFeels}\"}},");
				json.Append($"\"maxfeels\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiFeelsLike"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxFeels}\"}},");
			}
			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append($"\"maxheatindex\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiHeatInd"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxHeatIndex}\"}},");
			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append($"\"maxhumidex\":{{\"name\":\"{cumulus.Trans.DataCaptions["HiHumidex"]}\",\"colour\":\"{cumulus.GraphOptions.Colour.MaxHumidex}\"}},");

			// Snow
			if (cumulus.GraphOptions.Visible.SnowDepth.IsVisible(local))
				json.Append($"\"snowdepth\":{{\"name\":\"{cumulus.Trans.SnowDepth}\",\"colour\":\"{cumulus.GraphOptions.Colour.SnowDepth}\"}},");
			if (cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
				json.Append($"\"snow24h\":{{\"name\":\"{cumulus.Trans.Snow24h}\",\"colour\":\"{cumulus.GraphOptions.Colour.Snow24h}\"}},");

			#endregion daily

			#region extra sensors

			// extra temp
			if (cumulus.GraphOptions.Visible.ExtraTemp.IsVisible(local))
				json.Append($"\"extratemp\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.ExtraTempCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.ExtraTemp)}\"]}},");
			// extra hum
			if (cumulus.GraphOptions.Visible.ExtraHum.IsVisible(local))
				json.Append($"\"extrahum\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.ExtraHumCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.ExtraHum)}\"]}},");
			// extra dewpoint
			if (cumulus.GraphOptions.Visible.ExtraDewPoint.IsVisible(local))
				json.Append($"\"extradew\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.ExtraDPCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.ExtraDewPoint)}\"]}},");
			// extra user temps
			if (cumulus.GraphOptions.Visible.UserTemp.IsVisible(local))
				json.Append($"\"usertemp\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.UserTempCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.UserTemp)}\"]}},");
			// soil temps
			if (cumulus.GraphOptions.Visible.SoilTemp.IsVisible(local))
				json.Append($"\"soiltemp\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.SoilTempCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.SoilTemp)}\"]}},");
			// soil temps
			if (cumulus.GraphOptions.Visible.SoilMoist.IsVisible(local))
				json.Append($"\"soilmoist\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.SoilMoistureCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.SoilMoist)}\"]}},");
			// soil EC
			if (cumulus.GraphOptions.Visible.SoilEc.IsVisible(local))
				json.Append($"\"soilec\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.SoilEcCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.SoilEc)}\"]}},");
			// leaf wetness
			if (cumulus.GraphOptions.Visible.LeafWetness.IsVisible(local))
				json.Append($"\"leafwet\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.LeafWetnessCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.LeafWetness)}\"]}},");
			// laser depth
			if (cumulus.GraphOptions.Visible.LaserDepth.IsVisible(local))
				json.Append($"\"laserdepth\":{{\"name\":[\"{string.Join("\",\"", cumulus.Trans.LaserCaptions)}\"],\"colour\":[\"{string.Join("\",\"", cumulus.GraphOptions.Colour.LaserDepth)}\"]}},");

			// CO2
			json.Append("\"co2\":{");
			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
				json.Append($"\"co2\":{{\"name\":\"{cumulus.Trans.CO2_CurrentCaption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.CO2}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
				json.Append($"\"co2average\":{{\"name\":\"{cumulus.Trans.CO2_24HourCaption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.CO2Avg}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
				json.Append($"\"pm10\":{{\"name\":\"{cumulus.Trans.CO2_pm10Caption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm10}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
				json.Append($"\"pm10average\":{{\"name\":\"{cumulus.Trans.CO2_pm10_24hrCaption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm10Avg}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
				json.Append($"\"pm2.5\":{{\"name\":\"{cumulus.Trans.CO2_pm2p5Caption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm25}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
				json.Append($"\"pm2.5average\":{{\"name\":\"{cumulus.Trans.CO2_pm2p5_24hrCaption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Pm25Avg}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
				json.Append($"\"humidity\":{{\"name\":\"{cumulus.Trans.CO2_HumidityCaption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Hum}\"}},");
			if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
				json.Append($"\"temperature\":{{\"name\":\"{cumulus.Trans.CO2_TemperatureCaption}\",\"colour\":\"{cumulus.GraphOptions.Colour.CO2Sensor.Temp}\"}}");
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

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
				json.Append("\"Temperature\",");

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
				json.Append("\"Indoor Temp\",");

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
				json.Append("\"Heat Index\",");

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
				json.Append("\"Dew Point\",");

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
				json.Append("\"Wind Chill\",");

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
				json.Append("\"Apparent Temp\",");

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
				json.Append("\"Feels Like\",");

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
				json.Append("\"Humidex\",");

			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
				json.Append("\"BGT\",\"WBGT\",");

			if (json[^1] == ',')
				json.Length--;

			// humidity values
			json.Append("],\"Humidity\":[");

			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				json.Append("\"Humidity\",");

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
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

			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local) || cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local) || cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
			{
				json.Append(",\"DailyTemps\":[");

				if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
					json.Append("\"AvgTemp\",");
				if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
					json.Append("\"MaxTemp\",");
				if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
					json.Append("\"MinTemp\",");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}


			// solar values
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local) || cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				json.Append(",\"Solar\":[");

				if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
					json.Append("\"Solar Rad\",");

				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
					json.Append("\"UV Index\",");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Sunshine
			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				json.Append(",\"Sunshine\":[\"sunhours\"]");
			}

			// air quality
			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (cumulus.StationOptions.PrimaryAqSensor > (int) Cumulus.PrimaryAqSensor.Undefined
				&& cumulus.StationOptions.PrimaryAqSensor != (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				json.Append(",\"AirQuality\":[");
				json.Append("\"PM 2.5\"");

				// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
				if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
					cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
					cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
				{
					json.Append(",\"PM 10\"");
				}
				json.Append(']');
			}

			// Degree Days
			if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) || cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
			{
				json.Append(",\"DegreeDays\":[");
				if (cumulus.GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
					json.Append("\"GDD1\",");

				if (cumulus.GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
					json.Append("\"GDD2\"");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Temp Sum
			if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum1.IsVisible(local) || cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
			{
				json.Append(",\"TempSum\":[");
				if (cumulus.GraphOptions.Visible.TempSum0.IsVisible(local))
					json.Append("\"Sum0\",");
				if (cumulus.GraphOptions.Visible.TempSum1.IsVisible(local))
					json.Append("\"Sum1\",");
				if (cumulus.GraphOptions.Visible.TempSum2.IsVisible(local))
					json.Append("\"Sum2\"");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Chill Hours
			if (cumulus.GraphOptions.Visible.ChillHours.IsVisible(local))
			{
				json.Append(",\"ChillHours\":[\"Chill Hours\"]");
			}

			// Extra temperature
			if (cumulus.GraphOptions.Visible.ExtraTemp.IsVisible(local))
			{
				json.Append(",\"ExtraTemp\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.ExtraTempCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra humidity
			if (cumulus.GraphOptions.Visible.ExtraHum.IsVisible(local))
			{
				json.Append(",\"ExtraHum\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.ExtraHumCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Extra dew point
			if (cumulus.GraphOptions.Visible.ExtraDewPoint.IsVisible(local))
			{
				json.Append(",\"ExtraDewPoint\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.ExtraDPCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}


			// Soil Temp
			if (cumulus.GraphOptions.Visible.SoilTemp.IsVisible(local))
			{
				json.Append(",\"SoilTemp\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.SoilTempCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Soil Moisture
			if (cumulus.GraphOptions.Visible.SoilMoist.IsVisible(local))
			{
				json.Append(",\"SoilMoist\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.SoilMoistureCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Soil EC
			if (cumulus.GraphOptions.Visible.SoilEc.IsVisible(local))
			{
				json.Append(",\"SoilEc\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.SoilEcCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// User Temp
			if (cumulus.GraphOptions.Visible.UserTemp.IsVisible(local))
			{
				json.Append(",\"UserTemp\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.UserTempCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Leaf wetness
			if (cumulus.GraphOptions.Visible.LeafWetness.IsVisible(local))
			{
				json.Append(",\"LeafWetness\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.LeafWetness.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.LeafWetnessCaptions[i]}\",");
				}
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// Snow
			if (cumulus.GraphOptions.Visible.SnowDepth.IsVisible(local) || cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
			{
				json.Append(",\"Snow\":[");
				//if (cumulus.GraphOptions.Visible.SnowDepth.IsVisible(local))
				//	json.Append($"\"{cumulus.Trans.SnowDepth}\",");
				if (cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
					json.Append($"\"{cumulus.Trans.Snow24h}\"");
				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// CO2
			if (cumulus.GraphOptions.Visible.CO2Sensor.IsVisible(local))
			{
				json.Append(",\"CO2\":[");
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_CurrentCaption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_24HourCaption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_pm2p5Caption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_pm2p5_24hrCaption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_pm10Caption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_pm10_24hrCaption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_TemperatureCaption}\",");
				if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
					json.Append($"\"{cumulus.Trans.CO2_HumidityCaption}\"");

				if (json[^1] == ',')
					json.Length--;

				json.Append(']');
			}

			// LASER depth
			if (cumulus.GraphOptions.Visible.LaserDepth.IsVisible(local))
			{
				json.Append(",\"LaserDepth\":[");
				for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
				{
					if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
						json.Append($"\"{cumulus.Trans.LaserCaptions[i]}\",");
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
			return JsonSerializer.Serialize(cumulus.SelectaChartOptions);
		}

		public string GetSelectaPeriodOptions()
		{
			return JsonSerializer.Serialize(cumulus.SelectaPeriodOptions);
		}

	}
}
