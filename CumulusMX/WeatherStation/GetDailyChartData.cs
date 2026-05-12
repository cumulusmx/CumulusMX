using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public string GetDailyRainGraphData()
		{
			var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);

			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"dailyrain\":[", 10000);

			var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();
			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].TotalRain.ToString(cumulus.RainFormat, InvC)}],");
			}

			// remove trailing comma
			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetSunHoursGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10000);
			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);
				var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();

				sb.Append("\"sunhours\":[");
				for (var i = 0; i < data.Count; i++)
				{
					var sunhrs = data[i].SunShineHours >= 0 ? data[i].SunShineHours : 0;
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{sunhrs.ToString(cumulus.SunFormat, InvC)}],");
				}

				// remove trailing comma
				if (sb[^1] == ',')
					sb.Length--;

				sb.Append(']');
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetDailyTempGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;
			var datefrom = DateTime.Now.AddDays(-cumulus.GraphDays - 1);
			var data = DayFile.Where(rec => rec.Date >= datefrom).ToList();
			var append = false;
			var sb = new StringBuilder("{");

			if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
			{
				sb.Append("\"mintemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].LowTemp.ToFixed(cumulus.TempFormat)}],");
				}

				// remove trailing comma
				if (sb[^1] == ',')
					sb.Length--;

				sb.Append(']');
				append = true;
			}

			if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
			{
				if (append)
					sb.Append(',');

				sb.Append("\"maxtemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].HighTemp.ToFixed(cumulus.TempFormat)}],");
				}

				// remove trailing comma
				if (sb[^1] == ',')
					sb.Length--;

				sb.Append(']');
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
			{
				if (append)
					sb.Append(',');

				sb.Append("\"avgtemp\":[");
				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].AvgTemp.ToFixed(cumulus.TempFormat)}],");
				}

				// remove trailing comma
				if (sb[^1] == ',')
					sb.Length--;

				sb.Append(']');
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetAllDailyTempGraphData(bool local)
		{
			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var minTemp = new StringBuilder("[");
			var maxTemp = new StringBuilder("[");
			var avgTemp = new StringBuilder("[");
			var heatIdx = new StringBuilder("[");
			var maxApp = new StringBuilder("[");
			var minApp = new StringBuilder("[");
			var windChill = new StringBuilder("[");
			var maxDew = new StringBuilder("[");
			var minDew = new StringBuilder("[");
			var maxFeels = new StringBuilder("[");
			var minFeels = new StringBuilder("[");
			var humidex = new StringBuilder("[");
			var bgt = new StringBuilder("[");
			var wbgt = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{
					var recDate = DayFile[i].Date.ToUnixTimeMs();
					// lo temp
					if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
						minTemp.Append($"[{recDate},{DayFile[i].LowTemp.ToFixed(cumulus.TempFormat)}],");
					// hi temp
					if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
						maxTemp.Append($"[{recDate},{DayFile[i].HighTemp.ToFixed(cumulus.TempFormat)}],");
					// avg temp
					if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
						avgTemp.Append($"[{recDate},{DayFile[i].AvgTemp.ToFixed(cumulus.TempFormat)}],");

					if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
					{
						// hi heat index
						if (DayFile[i].HighHeatIndex > -999)
							heatIdx.Append($"[{recDate},{DayFile[i].HighHeatIndex.ToFixed(cumulus.TempFormat)}],");
						else
							heatIdx.Append($"[{recDate},null],");
					}
					if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
					{
						// hi app temp
						if (DayFile[i].HighAppTemp > -999)
							maxApp.Append($"[{recDate},{DayFile[i].HighAppTemp.ToFixed(cumulus.TempFormat)}],");
						else
							maxApp.Append($"[{recDate},null],");

						// lo app temp
						if (DayFile[i].LowAppTemp < 999)
							minApp.Append($"[{recDate},{DayFile[i].LowAppTemp.ToFixed(cumulus.TempFormat)}],");
						else
							minApp.Append($"[{recDate},null],");
					}
					// lo wind chill
					if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
					{
						if (DayFile[i].LowWindChill < 999)
							windChill.Append($"[{recDate},{DayFile[i].LowWindChill.ToFixed(cumulus.TempFormat)}],");
						else
							windChill.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
					{
						// hi dewpt
						if (DayFile[i].HighDewPoint > -999)
							maxDew.Append($"[{recDate},{DayFile[i].HighDewPoint.ToFixed(cumulus.TempFormat)}],");
						else
							maxDew.Append($"[{recDate},null],");

						// lo dewpt
						if (DayFile[i].LowDewPoint < 999)
							minDew.Append($"[{recDate},{DayFile[i].LowDewPoint.ToFixed(cumulus.TempFormat)}],");
						else
							minDew.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
					{
						// hi feels like
						if (DayFile[i].HighFeelsLike > -999)
							maxFeels.Append($"[{recDate},{DayFile[i].HighFeelsLike.ToFixed(cumulus.TempFormat)}],");
						else
							maxFeels.Append($"[{recDate},null],");

						// lo feels like
						if (DayFile[i].LowFeelsLike < 999)
							minFeels.Append($"[{recDate},{DayFile[i].LowFeelsLike.ToFixed(cumulus.TempFormat)}],");
						else
							minFeels.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
					{
						// hi humidex
						if (DayFile[i].HighHumidex > -999)
							humidex.Append($"[{recDate},{DayFile[i].HighHumidex.ToFixed(cumulus.TempFormat)}],");
						else
							humidex.Append($"[{recDate},null],");
					}

					if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
					{
						// hi BGT
						if (DayFile[i].HighBgt.HasValue)
							bgt.Append($"[{recDate},{DayFile[i].HighBgt.ToFixed(cumulus.TempFormat)}],");
						else
							bgt.Append($"[{recDate},null],");
						// hi WBGT
						if (DayFile[i].HighWbgt.HasValue)
							wbgt.Append($"[{recDate},{DayFile[i].HighWbgt.ToFixed(cumulus.TempFormat)}],");
						else
							wbgt.Append($"[{recDate},null],");
					}
				}
			}

			// remove trailing commas
			minTemp.Length--;
			maxTemp.Length--;
			avgTemp.Length--;


			if (cumulus.GraphOptions.Visible.MinTemp.IsVisible(local))
				sb.Append("\"minTemp\":" + minTemp.ToString() + "],");
			if (cumulus.GraphOptions.Visible.MaxTemp.IsVisible(local))
				sb.Append("\"maxTemp\":" + maxTemp.ToString() + "],");
			if (cumulus.GraphOptions.Visible.AvgTemp.IsVisible(local))
				sb.Append("\"avgTemp\":" + avgTemp.ToString() + "],");
			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				heatIdx.Length--;
				sb.Append("\"heatIndex\":" + heatIdx.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				maxApp.Length--;
				minApp.Length--;
				sb.Append("\"maxApp\":" + maxApp.ToString() + "],");
				sb.Append("\"minApp\":" + minApp.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				windChill.Length--;
				sb.Append("\"windChill\":" + windChill.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				maxDew.Length--;
				minDew.Length--;
				sb.Append("\"maxDew\":" + maxDew.ToString() + "],");
				sb.Append("\"minDew\":" + minDew.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				maxFeels.Length--;
				minFeels.Length--;
				sb.Append("\"maxFeels\":" + maxFeels.ToString() + "],");
				sb.Append("\"minFeels\":" + minFeels.ToString() + "],");
			}
			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				humidex.Length--;
				sb.Append("\"humidex\":" + humidex.ToString() + "],");
			}

			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
			{
				bgt.Length--;
				wbgt.Length--;
				sb.Append("\"bgt\":" + bgt.ToString() + "],");
				sb.Append("\"wbgt\":" + wbgt.ToString() + "],");
			}

			sb.Length--;
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyWindGraphData()
		{
			var InvC = CultureInfo.InvariantCulture;

			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var maxGust = new StringBuilder("[");
			var windRun = new StringBuilder("[");
			var maxWind = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{
					var recDate = DayFile[i].Date.ToUnixTimeMs();

					// hi gust
					maxGust.Append($"[{recDate},{DayFile[i].HighGust.ToString(cumulus.WindFormat, InvC)}],");
					// hi wind run
					windRun.Append($"[{recDate},{DayFile[i].WindRun.ToString(cumulus.WindRunFormat, InvC)}],");
					// hi wind
					maxWind.Append($"[{recDate},{DayFile[i].HighAvgWind.ToString(cumulus.WindAvgFormat, InvC)}],");
				}
			}

			maxGust.Length--;
			windRun.Length--;
			maxWind.Length--;

			sb.Append("\"maxGust\":" + maxGust.ToString() + "],");
			sb.Append("\"windRun\":" + windRun.ToString() + "],");
			sb.Append("\"maxWind\":" + maxWind.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyRainGraphData()
		{
			var InvC = CultureInfo.InvariantCulture;

			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var maxRRate = new StringBuilder("[");
			var rain = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{

					var recDate = DayFile[i].Date.ToUnixTimeMs();

					// hi rain rate
					maxRRate.Append($"[{recDate},{DayFile[i].HighRainRate.ToString(cumulus.RainFormat, InvC)}],");
					// total rain
					rain.Append($"[{recDate},{DayFile[i].TotalRain.ToString(cumulus.RainFormat, InvC)}],");
				}
			}

			maxRRate.Length--;
			rain.Length--;

			sb.Append("\"maxRainRate\":" + maxRRate.ToString() + "],");
			sb.Append("\"rain\":" + rain.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyPressGraphData()
		{
			var InvC = CultureInfo.InvariantCulture;

			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var minBaro = new StringBuilder("[");
			var maxBaro = new StringBuilder("[");


			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{

					var recDate = DayFile[i].Date.ToUnixTimeMs();

					// lo baro
					minBaro.Append($"[{recDate},{DayFile[i].LowPress.ToString(cumulus.PressFormat, InvC)}],");
					// hi baro
					maxBaro.Append($"[{recDate},{DayFile[i].HighPress.ToString(cumulus.PressFormat, InvC)}],");
				}
			}

			// Remove trailing commas
			minBaro.Length--;
			maxBaro.Length--;
			sb.Append("\"minBaro\":" + minBaro.ToString() + "],");
			sb.Append("\"maxBaro\":" + maxBaro.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyWindDirGraphData()
		{

			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var windDir = new StringBuilder("[");


			// Read the dayfile and extract the records from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{
					var recDate = DayFile[i].Date.ToUnixTimeMs();

					windDir.Append($"[{recDate},{DayFile[i].DominantWindBearing}],");

				}
			}

			// Remove trailing commas
			windDir.Length--;
			sb.Append("\"windDir\":" + windDir.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailyHumGraphData()
		{
			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var minHum = new StringBuilder("[");
			var maxHum = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{

					var recDate = DayFile[i].Date.ToUnixTimeMs();

					// lo humidity
					minHum.Append($"[{recDate},{DayFile[i].LowHumidity}],");
					// hi humidity
					maxHum.Append($"[{recDate},{DayFile[i].HighHumidity}],");
				}
			}
			// Remove trailing commas
			minHum.Length--;
			maxHum.Length--;

			sb.Append("\"minHum\":" + minHum.ToString() + "],");
			sb.Append("\"maxHum\":" + maxHum.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDailySolarGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;

			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var sunHours = new StringBuilder("[");
			var solarRad = new StringBuilder("[");
			var uvi = new StringBuilder("[");

			// Read the day file list and extract the data from there
			if (DayFile.Count > 0)
			{
				for (var i = 0; i < DayFile.Count; i++)
				{
					var recDate = DayFile[i].Date.ToUnixTimeMs();

					if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
					{
						// sunshine hours
						sunHours.Append($"[{recDate},{(DayFile[i].SunShineHours > Cumulus.DefaultHiVal ? DayFile[i].SunShineHours.ToString(InvC) : "null")}],");
					}

					if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
					{
						// hi solar rad
						solarRad.Append($"[{recDate},{(DayFile[i].HighSolar > Cumulus.DefaultHiVal ? DayFile[i].HighSolar : "null")}],");
					}

					if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
					{
						// hi UV-I
						uvi.Append($"[{recDate},{(DayFile[i].HighUv > Cumulus.DefaultHiVal ? DayFile[i].HighUv.ToString(cumulus.UVFormat, InvC) : "null")}],");
					}
				}
			}

			if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				sunHours.Length--;
				sb.Append("\"sunHours\":" + sunHours.ToString() + "]");
			}

			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local))
					sb.Append(',');

				solarRad.Length--;
				sb.Append("\"solarRad\":" + solarRad.ToString() + "]");
			}

			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (cumulus.GraphOptions.Visible.Sunshine.IsVisible(local) || cumulus.GraphOptions.Visible.Solar.IsVisible(local))
					sb.Append(',');

				uvi.Length--;
				sb.Append("\"uvi\":" + uvi.ToString() + "]");
			}
			sb.Append('}');

			return sb.ToString();
		}
	}
}
