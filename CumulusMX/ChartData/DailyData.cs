using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CumulusMX
{
	public partial class Cumulus
	{
		public void CreateDailyGraphDataFiles()
		{
			// skip 0 & 1 = config files
			// daily rain = 8
			// daily temp = 9
			// sun hours = 11
			var eod = new int[] { (int) GraphFileIdx.DAILYRAIN, (int) GraphFileIdx.DAILYTEMP, (int) GraphFileIdx.SUNHOURS };

			foreach (var i in eod)
			{
				if (GraphDataFiles[i].Create)
				{
					var json = CreateGraphDataJson(GraphDataFiles[i].FileName, false);

					try
					{
						var dest = Path.Combine(GraphDataFiles[i].LocalPath, GraphDataFiles[i].FileName);
						File.WriteAllTextAsync(dest, json);
					}
					catch (Exception ex)
					{
						LogErrorMessage($"Error writing {GraphDataFiles[i].FileName}: {ex}");
					}
				}

				GraphDataFiles[i].CopyRequired = true;
				GraphDataFiles[i].FtpRequired = true;
			}
		}


		public void CreateEodGraphDataFiles()
		{
			for (var i = 0; i < GraphDataEodFiles.Length; i++)
			{
				if (GraphDataEodFiles[i].Create)
				{
					var json = CreateEodGraphDataJson(GraphDataEodFiles[i].FileName);

					try
					{
						var dest = Path.Combine(GraphDataEodFiles[i].LocalPath, GraphDataEodFiles[i].FileName);
						File.WriteAllTextAsync(dest, json);
					}
					catch (Exception ex)
					{
						LogErrorMessage($"Error writing {GraphDataEodFiles[i].FileName}: {ex}");
					}
				}

				// Now set the flag that upload is required (if enabled)
				GraphDataEodFiles[i].FtpRequired = true;
				GraphDataEodFiles[i].CopyRequired = true;
			}
		}


		public string CreateEodGraphDataJson(string filename)
		{
			return filename switch
			{
				"alldailytempdata.json" => GetAllDailyTempGraphData(false),
				"alldailypressdata.json" => GetAllDailyPressGraphData(),
				"alldailywinddata.json" => GetAllDailyWindGraphData(),
				"alldailyhumdata.json" => GetAllDailyHumGraphData(),
				"alldailyraindata.json" => GetAllDailyRainGraphData(),
				"alldailysolardata.json" => GetAllDailySolarGraphData(false),
				"alldailydegdaydata.json" => GetAllDegreeDaysGraphData(false),
				"alltempsumdata.json" => GetAllTempSumGraphData(false),
				"allchillhrsdata.json" => GetAllChillHrsGraphData(false),
				"alldailysnowdata.json" => GetAllSnowGraphData(false),
				_ => "{}",
			};
		}

		public string GetAllChillHrsGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;

			if (!GraphOptions.Visible.ChillHours.IsVisible(local))
			{
				return "{}";
			}

			var sb = new StringBuilder("{");
			var chillHrsYears = new StringBuilder("{", 32768);

			var chillhrs = new StringBuilder("[", 8600);

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = ChillHourSeasonStart < 3 ? 2000 : 1999;

			int startYear;

			var options = $"\"options\":{{\"threshold\":{ChillHourThreshold},\"basetemp\":{ChillHourBase},\"startMon\":{ChillHourSeasonStart}}}";

			// Read the day file list and extract the data from there
			if (MetData.DayFile.Count > 0)
			{
				// we have to detect a new year is starting
				nextYear = new DateTime(MetData.DayFile[0].Date.Year, ChillHourSeasonStart, 1, 0, 0, 0, DateTimeKind.Local);

				if (MetData.DayFile[0].Date >= nextYear)
				{
					nextYear = nextYear.AddYears(1);
				}

				// are we starting part way through a year that does not start in January?
				if (MetData.DayFile[0].Date.Year == nextYear.Year)
				{
					startYear = MetData.DayFile[0].Date.Year - 1;
				}
				else
				{
					startYear = MetData.DayFile[0].Date.Year;
				}

				chillHrsYears.Append($"\"{startYear}\":");

				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (MetData.DayFile[i].Date >= nextYear)
					{
						if (chillhrs.Length > 10)
						{
							// remove last comma
							chillhrs.Length--;
							// close the year data
							chillhrs.Append("],");
							// append to years array
							chillHrsYears.Append(chillhrs);

							chillhrs.Clear().Append($"\"{MetData.DayFile[i].Date.Year}\":[");
						}

						// reset the plot year for Southern hemisphere
						plotYear = ChillHourSeasonStart < 3 ? 2000 : 1999;

						do
						{
							nextYear = nextYear.AddYears(1);
						}
						while (MetData.DayFile[i].Date >= nextYear);
					}
					// make all series the same year so they plot together
					// 2000 was a leap year, so make sure February falls in 2000
					// for Southern hemisphere this means the start year must be 1999
					if (ChillHourSeasonStart > 2 && plotYear == 1999 && MetData.DayFile[i].Date.Month < ChillHourSeasonStart)
					{
						plotYear++;
					}

					var recDate = new DateTime(plotYear, MetData.DayFile[i].Date.Month, MetData.DayFile[i].Date.Day, 0, 0, 0, DateTimeKind.Local).ToUnixTimeMs();

					// annual accumulation
					chillhrs.Append($"[{recDate},{MetData.DayFile[i].ChillHours.ToString("F0", InvC)}],");
				}
			}

			// remove last commas from the years arrays and close them off
			if (chillhrs[^1] == ',')
			{
				chillhrs.Length--;
			}

			// have previous years been appended?
			if (chillHrsYears[^1] == ']')
			{
				chillHrsYears.Append(',');
			}

			chillHrsYears.Append(chillhrs + "]");

			// add to main json
			sb.Append("\"data\":" + chillHrsYears + "},");

			sb.Append(options);

			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllSnowGraphData(bool local)
		{
			/* returns:
			 *	snowdepth:[[date1,val1],[date2,val2]...],
			 *	snow24h:[[date1,val1],[date2,val2]...]
			 */

			var InvC = CultureInfo.InvariantCulture;

			var sb = new StringBuilder("{");
			var snowdepth = new StringBuilder("[", 32768);
			var snow24h = new StringBuilder("[", 32768);

			// Read the diary database
			// get the earlist record date
			var earliest = DiaryDB.Query<DiaryData>("select * from DiaryData order by Date limit 1");

			if (earliest.Count == 1)
			{
				var query = string.Format(
@"WITH RECURSIVE dates(date) AS (
  VALUES('{0}')
  UNION ALL
  SELECT date(date, '+1 day')
  FROM dates
  WHERE date < DATE('now')
)
SELECT rd.date, dd.snowDepth, dd.snow24h FROM dates rd
LEFT JOIN DiaryData dd ON date(dd.Date) = rd.date
ORDER BY rd.date ASC;", earliest[0].Date.ToString("yyyy-MM-dd"));

				var data = DiaryDB.Query<DiaryData>(query);

				if (data.Count > 0)
				{
					for (var i = 0; i < data.Count; i++)
					{
						var recDate = data[i].Date.ToUnixTimeMs();

						if (GraphOptions.Visible.SnowDepth.IsVisible(local))
						{
							// snow depth
							snowdepth.Append($"[{recDate},{(data[i].SnowDepth.HasValue ? data[i].SnowDepth.Value.ToString("F1", InvC) : "null")}],");
						}

						if (GraphOptions.Visible.Snow24h.IsVisible(local))
						{
							// snowfall 24h
							snow24h.Append($"[{recDate},{(data[i].Snow24h.HasValue ? data[i].Snow24h.Value.ToString("F1", InvC) : "null")}],");
						}
					}
				}
			}

			if (GraphOptions.Visible.SnowDepth.IsVisible(local))
			{
				if (snowdepth[^1] == ',')
					snowdepth.Length--;
				sb.Append("\"SnowDepth\":" + snowdepth.ToString() + "]");
			}

			if (GraphOptions.Visible.Snow24h.IsVisible(local))
			{
				if (GraphOptions.Visible.SnowDepth.IsVisible(local))
					sb.Append(',');

				if (snow24h[^1] == ',')
					snow24h.Length--;
				sb.Append("\"Snow24h\":" + snow24h.ToString() + "]");

			}

			sb.Append('}');

			return sb.ToString();
		}

		public string GetDailyRainGraphData()
		{
			var datefrom = DateTime.Now.AddDays(-GraphDays - 1);

			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"dailyrain\":[", 10000);

			var data = MetData.DayFile.Where(rec => rec.Date >= datefrom).ToList();
			for (var i = 0; i < data.Count; i++)
			{
				sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].TotalRain.ToString(RainFormat, InvC)}],");
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
			if (GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				var datefrom = DateTime.Now.AddDays(-GraphDays - 1);
				var data = MetData.DayFile.Where(rec => rec.Date >= datefrom).ToList();

				sb.Append("\"sunhours\":[");
				for (var i = 0; i < data.Count; i++)
				{
					var sunhrs = data[i].SunShineHours >= 0 ? data[i].SunShineHours : 0;
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{sunhrs.ToString(SunFormat, InvC)}],");
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
			var datefrom = DateTime.Now.AddDays(-GraphDays - 1);
			var data = MetData.DayFile.Where(rec => rec.Date >= datefrom).ToList();
			var append = false;
			var sb = new StringBuilder("{");

			if (GraphOptions.Visible.MinTemp.IsVisible(local))
			{
				sb.Append("\"mintemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].LowTemp.ToFixed(TempFormat)}],");
				}

				// remove trailing comma
				if (sb[^1] == ',')
					sb.Length--;

				sb.Append(']');
				append = true;
			}

			if (GraphOptions.Visible.MaxTemp.IsVisible(local))
			{
				if (append)
					sb.Append(',');

				sb.Append("\"maxtemp\":[");

				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].HighTemp.ToFixed(TempFormat)}],");
				}

				// remove trailing comma
				if (sb[^1] == ',')
					sb.Length--;

				sb.Append(']');
				append = true;
			}

			if (GraphOptions.Visible.AvgTemp.IsVisible(local))
			{
				if (append)
					sb.Append(',');

				sb.Append("\"avgtemp\":[");
				for (var i = 0; i < data.Count; i++)
				{
					sb.Append($"[{data[i].Date.ToUnixTimeMs()},{data[i].AvgTemp.ToFixed(TempFormat)}],");
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
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();
					// lo temp
					if (GraphOptions.Visible.MinTemp.IsVisible(local))
						minTemp.Append($"[{recDate},{MetData.DayFile[i].LowTemp.ToFixed(TempFormat)}],");
					// hi temp
					if (GraphOptions.Visible.MaxTemp.IsVisible(local))
						maxTemp.Append($"[{recDate},{MetData.DayFile[i].HighTemp.ToFixed(TempFormat)}],");
					// avg temp
					if (GraphOptions.Visible.AvgTemp.IsVisible(local))
						avgTemp.Append($"[{recDate},{MetData.DayFile[i].AvgTemp.ToFixed(TempFormat)}],");

					if (GraphOptions.Visible.HeatIndex.IsVisible(local))
					{
						// hi heat index
						if (MetData.DayFile[i].HighHeatIndex > -999)
							heatIdx.Append($"[{recDate},{MetData.DayFile[i].HighHeatIndex.ToFixed(TempFormat)}],");
						else
							heatIdx.Append($"[{recDate},null],");
					}
					if (GraphOptions.Visible.AppTemp.IsVisible(local))
					{
						// hi app temp
						if (MetData.DayFile[i].HighAppTemp > -999)
							maxApp.Append($"[{recDate},{MetData.DayFile[i].HighAppTemp.ToFixed(TempFormat)}],");
						else
							maxApp.Append($"[{recDate},null],");

						// lo app temp
						if (MetData.DayFile[i].LowAppTemp < 999)
							minApp.Append($"[{recDate},{MetData.DayFile[i].LowAppTemp.ToFixed(TempFormat)}],");
						else
							minApp.Append($"[{recDate},null],");
					}
					// lo wind chill
					if (GraphOptions.Visible.WindChill.IsVisible(local))
					{
						if (MetData.DayFile[i].LowWindChill < 999)
							windChill.Append($"[{recDate},{MetData.DayFile[i].LowWindChill.ToFixed(TempFormat)}],");
						else
							windChill.Append($"[{recDate},null],");
					}

					if (GraphOptions.Visible.DewPoint.IsVisible(local))
					{
						// hi dewpt
						if (MetData.DayFile[i].HighDewPoint > -999)
							maxDew.Append($"[{recDate},{MetData.DayFile[i].HighDewPoint.ToFixed(TempFormat)}],");
						else
							maxDew.Append($"[{recDate},null],");

						// lo dewpt
						if (MetData.DayFile[i].LowDewPoint < 999)
							minDew.Append($"[{recDate},{MetData.DayFile[i].LowDewPoint.ToFixed(TempFormat)}],");
						else
							minDew.Append($"[{recDate},null],");
					}

					if (GraphOptions.Visible.FeelsLike.IsVisible(local))
					{
						// hi feels like
						if (MetData.DayFile[i].HighFeelsLike > -999)
							maxFeels.Append($"[{recDate},{MetData.DayFile[i].HighFeelsLike.ToFixed(TempFormat)}],");
						else
							maxFeels.Append($"[{recDate},null],");

						// lo feels like
						if (MetData.DayFile[i].LowFeelsLike < 999)
							minFeels.Append($"[{recDate},{MetData.DayFile[i].LowFeelsLike.ToFixed(TempFormat)}],");
						else
							minFeels.Append($"[{recDate},null],");
					}

					if (GraphOptions.Visible.Humidex.IsVisible(local))
					{
						// hi humidex
						if (MetData.DayFile[i].HighHumidex > -999)
							humidex.Append($"[{recDate},{MetData.DayFile[i].HighHumidex.ToFixed(TempFormat)}],");
						else
							humidex.Append($"[{recDate},null],");
					}

					if (GraphOptions.Visible.BGT.IsVisible(local))
					{
						// hi BGT
						if (MetData.DayFile[i].HighBgt.HasValue)
							bgt.Append($"[{recDate},{MetData.DayFile[i].HighBgt.ToFixed(TempFormat)}],");
						else
							bgt.Append($"[{recDate},null],");
						// hi WBGT
						if (MetData.DayFile[i].HighWbgt.HasValue)
							wbgt.Append($"[{recDate},{MetData.DayFile[i].HighWbgt.ToFixed(TempFormat)}],");
						else
							wbgt.Append($"[{recDate},null],");
					}
				}
			}

			// remove trailing commas
			minTemp.Length--;
			maxTemp.Length--;
			avgTemp.Length--;


			if (GraphOptions.Visible.MinTemp.IsVisible(local))
				sb.Append("\"minTemp\":" + minTemp.ToString() + "],");
			if (GraphOptions.Visible.MaxTemp.IsVisible(local))
				sb.Append("\"maxTemp\":" + maxTemp.ToString() + "],");
			if (GraphOptions.Visible.AvgTemp.IsVisible(local))
				sb.Append("\"avgTemp\":" + avgTemp.ToString() + "],");
			if (GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				heatIdx.Length--;
				sb.Append("\"heatIndex\":" + heatIdx.ToString() + "],");
			}
			if (GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				maxApp.Length--;
				minApp.Length--;
				sb.Append("\"maxApp\":" + maxApp.ToString() + "],");
				sb.Append("\"minApp\":" + minApp.ToString() + "],");
			}
			if (GraphOptions.Visible.WindChill.IsVisible(local))
			{
				windChill.Length--;
				sb.Append("\"windChill\":" + windChill.ToString() + "],");
			}
			if (GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				maxDew.Length--;
				minDew.Length--;
				sb.Append("\"maxDew\":" + maxDew.ToString() + "],");
				sb.Append("\"minDew\":" + minDew.ToString() + "],");
			}
			if (GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				maxFeels.Length--;
				minFeels.Length--;
				sb.Append("\"maxFeels\":" + maxFeels.ToString() + "],");
				sb.Append("\"minFeels\":" + minFeels.ToString() + "],");
			}
			if (GraphOptions.Visible.Humidex.IsVisible(local))
			{
				humidex.Length--;
				sb.Append("\"humidex\":" + humidex.ToString() + "],");
			}

			if (GraphOptions.Visible.BGT.IsVisible(local))
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
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();

					// hi gust
					maxGust.Append($"[{recDate},{MetData.DayFile[i].HighGust.ToString(WindFormat, InvC)}],");
					// hi wind run
					windRun.Append($"[{recDate},{MetData.DayFile[i].WindRun.ToString(WindRunFormat, InvC)}],");
					// hi wind
					maxWind.Append($"[{recDate},{MetData.DayFile[i].HighAvgWind.ToString(WindAvgFormat, InvC)}],");
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
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{

					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();

					// hi rain rate
					maxRRate.Append($"[{recDate},{MetData.DayFile[i].HighRainRate.ToString(RainFormat, InvC)}],");
					// total rain
					rain.Append($"[{recDate},{MetData.DayFile[i].TotalRain.ToString(RainFormat, InvC)}],");
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
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{

					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();

					// lo baro
					minBaro.Append($"[{recDate},{MetData.DayFile[i].LowPress.ToString(PressFormat, InvC)}],");
					// hi baro
					maxBaro.Append($"[{recDate},{MetData.DayFile[i].HighPress.ToString(PressFormat, InvC)}],");
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

		public static string GetAllDailyWindDirGraphData()
		{

			/* returns:
			 *		highgust:[[date1,val1],[date2,val2]...],
			 *		mintemp:[[date1,val1],[date2,val2]...],
			 *		etc
			 */

			var sb = new StringBuilder("{");
			var windDir = new StringBuilder("[");


			// Read the dayfile and extract the records from there
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();

					windDir.Append($"[{recDate},{MetData.DayFile[i].DominantWindBearing}],");

				}
			}

			// Remove trailing commas
			windDir.Length--;
			sb.Append("\"windDir\":" + windDir.ToString() + "]");
			sb.Append('}');

			return sb.ToString();
		}

		public static string GetAllDailyHumGraphData()
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
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{

					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();

					// lo humidity
					minHum.Append($"[{recDate},{MetData.DayFile[i].LowHumidity}],");
					// hi humidity
					maxHum.Append($"[{recDate},{MetData.DayFile[i].HighHumidity}],");
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
			if (MetData.DayFile.Count > 0)
			{
				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					var recDate = MetData.DayFile[i].Date.ToUnixTimeMs();

					if (GraphOptions.Visible.Sunshine.IsVisible(local))
					{
						// sunshine hours
						sunHours.Append($"[{recDate},{(MetData.DayFile[i].SunShineHours > Cumulus.DefaultHiVal ? MetData.DayFile[i].SunShineHours.ToString(InvC) : "null")}],");
					}

					if (GraphOptions.Visible.Solar.IsVisible(local))
					{
						// hi solar rad
						solarRad.Append($"[{recDate},{(MetData.DayFile[i].HighSolar > Cumulus.DefaultHiVal ? MetData.DayFile[i].HighSolar : "null")}],");
					}

					if (GraphOptions.Visible.UV.IsVisible(local))
					{
						// hi UV-I
						uvi.Append($"[{recDate},{(MetData.DayFile[i].HighUv > Cumulus.DefaultHiVal ? MetData.DayFile[i].HighUv.ToString(UVFormat, InvC) : "null")}],");
					}
				}
			}

			if (GraphOptions.Visible.Sunshine.IsVisible(local))
			{
				sunHours.Length--;
				sb.Append("\"sunHours\":" + sunHours.ToString() + "]");
			}

			if (GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (GraphOptions.Visible.Sunshine.IsVisible(local))
					sb.Append(',');

				solarRad.Length--;
				sb.Append("\"solarRad\":" + solarRad.ToString() + "]");
			}

			if (GraphOptions.Visible.UV.IsVisible(local))
			{
				if (GraphOptions.Visible.Sunshine.IsVisible(local) || GraphOptions.Visible.Solar.IsVisible(local))
					sb.Append(',');

				uvi.Length--;
				sb.Append("\"uvi\":" + uvi.ToString() + "]");
			}
			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllDegreeDaysGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;

			var sb = new StringBuilder("{");
			var growdegdaysYears1 = new StringBuilder("{", 32768);
			var growdegdaysYears2 = new StringBuilder("{", 32768);

			var growYear1 = new StringBuilder("[", 8600);
			var growYear2 = new StringBuilder("[", 8600);

			var options = $"\"options\":{{\"gddBase1\":{GrowingBase1},\"gddBase2\":{GrowingBase2},\"startMon\":{GrowingYearStarts}}}";

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = GrowingYearStarts < 3 ? 2000 : 1999;

			int startYear;

			var annualGrowingDegDays1 = 0.0;
			var annualGrowingDegDays2 = 0.0;

			// Read the day file list and extract the data from there
			if (MetData.DayFile.Count > 0 && (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) || GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local)))
			{
				// we have to detect a new growing deg day year is starting
				nextYear = new DateTime(MetData.DayFile[0].Date.Year, GrowingYearStarts, 1, 0, 0, 0, DateTimeKind.Local);

				if (MetData.DayFile[0].Date >= nextYear)
				{
					nextYear = nextYear.AddYears(1);
				}

				// are we starting part way through a year that does not start in January?
				if (MetData.DayFile[0].Date.Year == nextYear.Year)
				{
					startYear = MetData.DayFile[0].Date.Year - 1;
				}
				else
				{
					startYear = MetData.DayFile[0].Date.Year;
				}

				if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
				{
					growdegdaysYears1.Append($"\"{startYear}\":");
				}
				if (GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
				{
					growdegdaysYears2.Append($"\"{startYear}\":");
				}


				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (MetData.DayFile[i].Date >= nextYear)
					{
						if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local) && growYear1.Length > 10)
						{
							// remove last comma
							growYear1.Length--;
							// close the year data
							growYear1.Append("],");
							// append to years array
							growdegdaysYears1.Append(growYear1);

							growYear1.Clear().Append($"\"{MetData.DayFile[i].Date.Year}\":[");
						}
						if (GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local) && growYear2.Length > 10)
						{
							// remove last comma
							growYear2.Length--;
							// close the year data
							growYear2.Append("],");
							// append to years array
							growdegdaysYears2.Append(growYear2);

							growYear2.Clear().Append($"\"{MetData.DayFile[i].Date.Year}\":[");
						}

						// reset the plot year for Southern hemisphere
						plotYear = GrowingYearStarts < 3 ? 2000 : 1999;

						annualGrowingDegDays1 = 0;
						annualGrowingDegDays2 = 0;
						do
						{
							nextYear = nextYear.AddYears(1);
						}
						while (MetData.DayFile[i].Date >= nextYear);
					}

					// make all series the same year so they plot together
					// 2000 was a leap year, so make sure February falls in 2000
					// for Southern hemisphere this means the start year must be 1999
					if (GrowingYearStarts > 2 && plotYear == 1999 && MetData.DayFile[i].Date.Month < GrowingYearStarts)
					{
						plotYear++;
					}

					// make all series the same year so they plot together
					var recDate = new DateTime(plotYear, MetData.DayFile[i].Date.Month, MetData.DayFile[i].Date.Day, 0, 0, 0, DateTimeKind.Local).ToUnixTimeMs();

					if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
					{
						// growing degree days
						var gdd = MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(MetData.DayFile[i].HighTemp), ConvertUnits.UserTempToC(MetData.DayFile[i].LowTemp), ConvertUnits.UserTempToC(GrowingBase1), GrowingCap30C);

						// annual accumulation
						annualGrowingDegDays1 += gdd;

						growYear1.Append($"[{recDate},{annualGrowingDegDays1.ToString("F1", InvC)}],");
					}

					if (GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
					{
						// growing degree days
						var gdd = MeteoLib.GrowingDegreeDays(ConvertUnits.UserTempToC(MetData.DayFile[i].HighTemp), ConvertUnits.UserTempToC(MetData.DayFile[i].LowTemp), ConvertUnits.UserTempToC(GrowingBase2), GrowingCap30C);

						// annual accumulation
						annualGrowingDegDays2 += gdd;

						growYear2.Append($"[{recDate},{annualGrowingDegDays2.ToString("F1", InvC)}],");
					}
				}
			}

			// remove last commas from the years arrays and close them off
			if (GraphOptions.Visible.GrowingDegreeDays1.IsVisible(local))
			{
				if (growYear1[^1] == ',')
				{
					growYear1.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears1[^1] == ']')
				{
					growdegdaysYears1.Append(',');
				}

				growdegdaysYears1.Append(growYear1 + "]");

				// add to main json
				sb.Append("\"GDD1\":" + growdegdaysYears1 + "},");
			}
			if (GraphOptions.Visible.GrowingDegreeDays2.IsVisible(local))
			{
				if (growYear2[^1] == ',')
				{
					growYear2.Length--;
				}

				// have previous years been appended?
				if (growdegdaysYears2[^1] == ']')
				{
					growdegdaysYears2.Append(',');
				}
				growdegdaysYears2.Append(growYear2 + "]");

				// add to main json
				sb.Append("\"GDD2\":" + growdegdaysYears2 + "},");
			}

			sb.Append(options);

			sb.Append('}');

			return sb.ToString();
		}

		public string GetAllTempSumGraphData(bool local)
		{
			var InvC = CultureInfo.InvariantCulture;

			var sb = new StringBuilder("{");
			var tempSumYears0 = new StringBuilder("{", 32768);
			var tempSumYears1 = new StringBuilder("{", 32768);
			var tempSumYears2 = new StringBuilder("{", 32768);

			var tempSum0 = new StringBuilder("[", 8600);
			var tempSum1 = new StringBuilder("[", 8600);
			var tempSum2 = new StringBuilder("[", 8600);

			DateTime nextYear;

			// 2000 was a leap year, so make sure February falls in 2000
			// for Southern hemisphere this means the start year must be 1999
			var plotYear = TempSumYearStarts < 3 ? 2000 : 1999;

			int startYear;
			var annualTempSum0 = 0.0;
			var annualTempSum1 = 0.0;
			var annualTempSum2 = 0.0;

			var options = $"\"options\":{{\"sumBase1\":{TempSumBase1},\"sumBase2\":{TempSumBase2},\"startMon\":{TempSumYearStarts}}}";

			// Read the day file list and extract the data from there
			if (MetData.DayFile.Count > 0 && (GraphOptions.Visible.TempSum0.IsVisible(local) || GraphOptions.Visible.TempSum1.IsVisible(local) || GraphOptions.Visible.TempSum2.IsVisible(local)))
			{
				// we have to detect a new year is starting
				nextYear = new DateTime(MetData.DayFile[0].Date.Year, TempSumYearStarts, 1, 0, 0, 0, DateTimeKind.Local);

				if (MetData.DayFile[0].Date >= nextYear)
				{
					nextYear = nextYear.AddYears(1);
				}

				// are we starting part way through a year that does not start in January?
				if (MetData.DayFile[0].Date.Year == nextYear.Year)
				{
					startYear = MetData.DayFile[0].Date.Year - 1;
				}
				else
				{
					startYear = MetData.DayFile[0].Date.Year;
				}

				if (GraphOptions.Visible.TempSum0.IsVisible(local))
				{
					tempSumYears0.Append($"\"{startYear}\":");
				}
				if (GraphOptions.Visible.TempSum1.IsVisible(local))
				{
					tempSumYears1.Append($"\"{startYear}\":");
				}
				if (GraphOptions.Visible.TempSum2.IsVisible(local))
				{
					tempSumYears2.Append($"\"{startYear}\":");
				}

				for (var i = 0; i < MetData.DayFile.Count; i++)
				{
					// we have rolled over into a new GDD year, write out what we have and reset
					if (MetData.DayFile[i].Date >= nextYear)
					{
						if (GraphOptions.Visible.TempSum0.IsVisible(local) && tempSum0.Length > 10)
						{
							// remove last comma
							tempSum0.Length--;
							// close the year data
							tempSum0.Append("],");
							// append to years array
							tempSumYears0.Append(tempSum0);

							tempSum0.Clear().Append($"\"{MetData.DayFile[i].Date.Year}\":[");
						}
						if (GraphOptions.Visible.TempSum1.IsVisible(local) && tempSum1.Length > 10)
						{
							// remove last comma
							tempSum1.Length--;
							// close the year data
							tempSum1.Append("],");
							// append to years array
							tempSumYears1.Append(tempSum1);

							tempSum1.Clear().Append($"\"{MetData.DayFile[i].Date.Year}\":[");
						}
						if (GraphOptions.Visible.TempSum2.IsVisible(local) && tempSum2.Length > 10)
						{
							// remove last comma
							tempSum2.Length--;
							// close the year data
							tempSum2.Append("],");
							// append to years array
							tempSumYears2.Append(tempSum2);

							tempSum2.Clear().Append($"\"{MetData.DayFile[i].Date.Year}\":[");
						}

						// reset the plot year for Southern hemisphere
						plotYear = TempSumYearStarts < 3 ? 2000 : 1999;

						annualTempSum0 = 0;
						annualTempSum1 = 0;
						annualTempSum2 = 0;

						do
						{
							nextYear = nextYear.AddYears(1);
						}
						while (MetData.DayFile[i].Date >= nextYear);
					}
					// make all series the same year so they plot together
					// 2000 was a leap year, so make sure February falls in 2000
					// for Southern hemisphere this means the start year must be 1999
					if (TempSumYearStarts > 2 && plotYear == 1999 && MetData.DayFile[i].Date.Month < TempSumYearStarts)
					{
						plotYear++;
					}

					var recDate = new DateTime(plotYear, MetData.DayFile[i].Date.Month, MetData.DayFile[i].Date.Day, 0, 0, 0, DateTimeKind.Local).ToUnixTimeMs();

					if (GraphOptions.Visible.TempSum0.IsVisible(local))
					{
						// annual accumulation
						annualTempSum0 += MetData.DayFile[i].AvgTemp;
						tempSum0.Append($"[{recDate},{annualTempSum0.ToString("F0", InvC)}],");
					}
					if (GraphOptions.Visible.TempSum1.IsVisible(local))
					{
						// annual accumulation
						annualTempSum1 += MetData.DayFile[i].AvgTemp - TempSumBase1;
						tempSum1.Append($"[{recDate},{annualTempSum1.ToString("F0", InvC)}],");
					}
					if (GraphOptions.Visible.TempSum2.IsVisible(local))
					{
						// annual accumulation
						annualTempSum2 += MetData.DayFile[i].AvgTemp - TempSumBase2;
						tempSum2.Append($"[{recDate},{annualTempSum2.ToString("F0", InvC)}],");
					}
				}
			}

			// remove last commas from the years arrays and close them off
			if (GraphOptions.Visible.TempSum0.IsVisible(local))
			{
				if (tempSum0[^1] == ',')
				{
					tempSum0.Length--;
				}

				// have previous years been appended?
				if (tempSumYears0[^1] == ']')
				{
					tempSumYears0.Append(',');
				}

				tempSumYears0.Append(tempSum0 + "]");

				// add to main json
				sb.Append("\"Sum0\":" + tempSumYears0 + "},");
			}
			if (GraphOptions.Visible.TempSum1.IsVisible(local))
			{
				if (tempSum1[^1] == ',')
				{
					tempSum1.Length--;
				}

				// have previous years been appended?
				if (tempSumYears1[^1] == ']')
				{
					tempSumYears1.Append(',');
				}

				tempSumYears1.Append(tempSum1 + "]");

				// add to main json
				sb.Append("\"Sum1\":" + tempSumYears1 + "},");
			}
			if (GraphOptions.Visible.TempSum2.IsVisible(local))
			{
				if (tempSum2[^1] == ',')
				{
					tempSum2.Length--;
				}

				// have previous years been appended?
				if (tempSumYears2[^1] == ']')
				{
					tempSumYears2.Append(',');
				}

				tempSumYears2.Append(tempSum2 + "]");

				// add to main json
				sb.Append("\"Sum2\":" + tempSumYears2 + "},");
			}

			sb.Append(options);

			sb.Append('}');

			return sb.ToString();
		}
	}
}
