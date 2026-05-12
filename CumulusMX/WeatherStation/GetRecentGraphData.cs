using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using CumulusMX.LogFiles;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public string GetSolarGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOLAR].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				{
					sbUv.Append($"[{jsTime},{(data[i].UV.HasValue ? data[i].UV.Value.ToString(cumulus.UVFormat, InvC) : "null")}],");
				}

				if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
				{
					sbSol.Append($"[{jsTime},{(data[i].SolarRad.HasValue ? data[i].SolarRad : "null")}],");

					sbMax.Append($"[{jsTime},{data[i].SolarMax}],");
				}
			}

			if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
			{
				if (sbUv[^1] == ',')
					sbUv.Length--;

				sbUv.Append(']');
				sb.Append(sbUv);
			}
			if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
			{
				if (sbSol[^1] == ',')
				{
					sbSol.Length--;
					sbMax.Length--;
				}

				sbSol.Append(']');
				sbMax.Append(']');
				if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
				{
					sb.Append(',');
				}
				sb.Append(sbSol);
				sb.Append(',');
				sb.Append(sbMax);
			}

			sb.Append('}');
			return sb.ToString();
		}


		public string GetRainGraphData(bool incremental, DateTime? start = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.RAIN].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sbRain.Append($"[{jsTime},{data[i].RainToday.ToString(cumulus.RainFormat, InvC)}],");

				sbRate.Append($"[{jsTime},{data[i].RainRate.ToString(cumulus.RainFormat, InvC)}],");
			}

			if (sbRain[^1] == ',')
			{
				sbRain.Length--;
				sbRate.Length--;
			}
			sbRain.Append("],");
			sbRate.Append(']');
			sb.Append(sbRain);
			sb.Append(sbRate);
			sb.Append('}');
			return sb.ToString();
		}

		public string GetHumGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var sb = new StringBuilder("{", 10240);
			var sbOut = new StringBuilder("\"hum\":[");
			var sbIn = new StringBuilder("\"inhum\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.HUM].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
				{
					sbOut.Append($"[{jsTime},{data[i].Humidity}],");
				}
				if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
				{
					sbIn.Append($"[{jsTime},{(data[i].IndoorHumidity.HasValue ? data[i].IndoorHumidity.Value : "null")}],");
				}
			}

			if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
			{
				if (sbOut[^1] == ',')
					sbOut.Length--;

				sbOut.Append(']');

				sb.Append(sbOut);
			}

			if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
			{
				if (sbIn[^1] == ',')
					sbIn.Length--;

				sbIn.Append(']');

				if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
					sb.Append(',');

				sb.Append(sbIn);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetWindDirGraphData(bool incremental, DateTime? start = null)
		{
			var sb = new StringBuilder("{\"bearing\":[");
			var sbAvg = new StringBuilder("\"avgbearing\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.WINDDIR].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sb.Append($"[{jsTime},{data[i].WindDir}],");

				sbAvg.Append($"[{jsTime},{data[i].WindAvgDir}],");
			}

			if (sb[^1] == ',')
			{
				sb.Length--;
				sbAvg.Length--;
				sbAvg.Append(']');
			}

			sb.Append("],");
			sb.Append(sbAvg);
			sb.Append('}');
			return sb.ToString();
		}

		public string GetWindGraphData(bool incremental, DateTime? start = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.WIND].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sb.Append($"[{jsTime},{data[i].WindGust.ToString(cumulus.WindFormat, InvC)}],");

				sbSpd.Append($"[{jsTime},{data[i].WindSpeed.ToString(cumulus.WindAvgFormat, InvC)}],");
			}

			if (sb[^1] == ',')
			{
				sb.Length--;
				sbSpd.Length--;
				sbSpd.Append(']');
			}

			sb.Append("],");
			sb.Append(sbSpd);
			sb.Append('}');
			return sb.ToString();
		}

		public string GetPressGraphData(bool incremental, DateTime? start = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"press\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.PRESS].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}


			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				sb.Append($"[{jsTime},{data[i].Pressure.ToString(cumulus.PressFormat, InvC)}],");
			}

			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetTempGraphData(bool incremental, bool local, DateTime? start = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);
			var sbIn = new StringBuilder("\"intemp\":[");
			var sbDew = new StringBuilder("\"dew\":[");
			var sbApp = new StringBuilder("\"apptemp\":[");
			var sbFeel = new StringBuilder("\"feelslike\":[");
			var sbChill = new StringBuilder("\"wchill\":[");
			var sbHeat = new StringBuilder("\"heatindex\":[");
			var sbTemp = new StringBuilder("\"temp\":[");
			var sbHumidex = new StringBuilder("\"humidex\":[");
			var sbBgt = new StringBuilder("\"bgt\":[");
			var sbWbgt = new StringBuilder("\"wbgt\":[");

			DateTime dateFrom;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.TEMP].LastDataTime;
			}
			else
			{
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
			}

			var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

			// no incremental data, send null to supress the upload
			if (incremental && data.Count == 0)
				return null;

			for (var i = 0; i < data.Count; i++)
			{
				var jsTime = data[i].Timestamp * 1000;

				if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
				{
					var val = data[i].IndoorTemp.ToFixed(cumulus.TempFormat, "null");
					sbIn.Append($"[{jsTime},{val}],");
				}

				if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
					sbDew.Append($"[{jsTime},{data[i].DewPoint.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
					sbApp.Append($"[{jsTime},{data[i].AppTemp.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
					sbFeel.Append($"[{jsTime},{data[i].FeelsLike.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
					sbChill.Append($"[{jsTime},{data[i].WindChill.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
					sbHeat.Append($"[{jsTime},{data[i].HeatIndex.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
					sbTemp.Append($"[{jsTime},{data[i].OutsideTemp.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
					sbHumidex.Append($"[{jsTime},{data[i].Humidex.ToFixed(cumulus.TempFormat)}],");

				if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
				{
					sbBgt.Append($"[{jsTime},{data[i].BGT.ToFixed(cumulus.TempFormat, "null")}],");
					sbWbgt.Append($"[{jsTime},{data[i].WBGT.ToFixed(cumulus.TempFormat, "null")}],");
				}
			}

			if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
			{
				if (sbIn[^1] == ',')
					sbIn.Length--;

				sbIn.Append(']');
				sb.Append(sbIn);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
			{
				if (sbDew[^1] == ',')
					sbDew.Length--;

				sbDew.Append(']');
				sb.Append((append ? "," : "") + sbDew);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
			{
				if (sbApp[^1] == ',')
					sbApp.Length--;

				sbApp.Append(']');
				sb.Append((append ? "," : "") + sbApp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
			{
				if (sbFeel[^1] == ',')
					sbFeel.Length--;

				sbFeel.Append(']');
				sb.Append((append ? "," : "") + sbFeel);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
			{
				if (sbChill[^1] == ',')
					sbChill.Length--;

				sbChill.Append(']');
				sb.Append((append ? "," : "") + sbChill);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
			{
				if (sbHeat[^1] == ',')
					sbHeat.Length--;

				sbHeat.Append(']');
				sb.Append((append ? "," : "") + sbHeat);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
			{
				if (sbTemp[^1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
			{
				if (sbHumidex[^1] == ',')
					sbHumidex.Length--;

				sbHumidex.Append(']');
				sb.Append((append ? "," : "") + sbHumidex);
			}

			if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
			{
				if (sbBgt[^1] == ',')
					sbBgt.Length--;

				sbBgt.Append(']');
				sb.Append((append ? "," : "") + sbBgt);

				if (sbWbgt[^1] == ',')
					sbWbgt.Length--;

				sbWbgt.Append(']');
				sb.Append((append ? "," : "") + sbWbgt);
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetAqGraphData(bool incremental, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sb2p5 = new StringBuilder("\"pm2p5\":[");
			var sb10 = new StringBuilder(",\"pm10\":[");


			// Check if we are to generate AQ data at all. Only if a primary sensor is defined and it isn't the Indoor AirLink
			if (cumulus.StationOptions.PrimaryAqSensor > (int) Cumulus.PrimaryAqSensor.Undefined
				&& cumulus.StationOptions.PrimaryAqSensor != (int) Cumulus.PrimaryAqSensor.AirLinkIndoor)
			{
				DateTime dateFrom;

				if (incremental)
				{
					dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.AIRQUAL].LastDataTime;
				}
				else if (start.HasValue && end.HasValue)
				{
					dateFrom = start.Value;
				}
				else
				{
					dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				}

				var data = RecentDataDb.Query<RecentData>("select * from RecentData where Timestamp > ? order by Timestamp", dateFrom.ToUnixTime());

				// no incremental data, send null to supress the upload
				if (incremental && data.Count == 0)
					return null;

				for (var i = 0; i < data.Count; i++)
				{
					var jsTime = data[i].Timestamp * 1000;

					if (data[i].Pm2p5.HasValue)
					{
						var val = data[i].Pm2p5 < -0.5 ? "null" : data[i].Pm2p5.Value.ToString("F1", InvC);
						sb2p5.Append($"[{jsTime},{val}],");

						// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
						if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
						{
							append = true;
							val = (data[i].Pm10 ?? -1) < -0.5 ? "null" : data[i].Pm10.Value.ToString("F1", InvC);
							sb10.Append($"[{jsTime},{val}],");
						}
					}
					else
					{
						sb2p5.Append($"[{jsTime},null],");
						// Only the AirLink and Ecowitt CO2 servers provide PM10 values at the moment
						if (cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.AirLinkIndoor ||
							cumulus.StationOptions.PrimaryAqSensor == (int) Cumulus.PrimaryAqSensor.EcowittCO2)
						{
							append = true;
							sb10.Append($"[{jsTime},null],");
						}
					}
				}

				if (sb2p5[^1] == ',')
					sb2p5.Length--;

				sb2p5.Append(']');
				sb.Append(sb2p5);

				if (append)
				{
					if (sb10[^1] == ',')
						sb10.Length--;

					sb10.Append(']');
					sb.Append(sb10);
				}

			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraTempCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.EXTRATEMP].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;

			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(fileDate);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 101, 102, 103, 104, 105, 106];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetExtraTempGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetExtraTempGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetExtraTempGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraTemp.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraDewPointGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraDPCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.EXTRADEW].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(fileDate);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 113, 114, 115, 116, 117, 118];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetExtraDewpointGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetExtraDewpointGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetExtraDewpointGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraDewPoint.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraDewPoint.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetExtraHumGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.ExtraHum.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.ExtraHumCaptions[i]}\":[");
			}


			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.EXTRAHUM].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 107, 108, 109, 110, 111, 112];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = int.TryParse(st[fields[i]], out int temp) ? temp.ToString() : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetExtraHumGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetExtraHumGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetExtraHumGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.ExtraHum.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.ExtraHum.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilTempCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOILTEMP].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [32, 33, 34, 35, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSoilTempGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSoilTempGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSoilTempGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilTemp.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilMoistGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilMoist.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilMoistureCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOILMOIST].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [36, 37, 38, 39, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = int.TryParse(st[fields[i]], out int temp) ? temp.ToString() : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSoilMoistGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSoilMoistGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSoilMoistGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilMoist.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilMoist.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetSoilEcGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.SoilEc.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.SoilEcCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SOILEC].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16
			// 119-122 AQ PM10
			// 123-126 AQ PM10 Avg
			// 127-143 Soil EC 1-16

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
										{
											var val = "null";
											if (st.Count > 127 + i)
											{
												val = int.TryParse(st[127 + i], out int temp) ? temp.ToString() : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSoilEcGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSoilEcGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSoilEcGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.SoilEc.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.SoilEc.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetLaserDepthGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.LaserDepth.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.LaserCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.LASERDEPTH].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [96, 97, 98, 99];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToString(cumulus.LaserFormat, InvC) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetLaserDepthGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetLaserDepthGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetLaserDepthGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.LaserDepth.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LaserDepth.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}


		public string GetSnow24hGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			if (!cumulus.GraphOptions.Visible.Snow24h.IsVisible(local))
			{
				return "{}";
			}

			/* returns data in the form of an object with properties for each data series
				"snow24h": [[time,val],[time,val],...]
			*/

			sb.Append($"\"{cumulus.Trans.Snow24h}\":[");

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.SNOW24H].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int field = 100;

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									var val = "null";
									if (field < st.Count)
									{
										val = double.TryParse(st[field], InvC, out temp) ? temp.ToString(cumulus.SnowFormat, InvC) : "null";
									}
									sb.Append($"[{jsTime},{val}],");
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetSnow24hGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetSnow24hGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetSnow24hGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();
		}

		public string GetLeafWetnessGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.LeafWetness.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.LeafWetness.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.LeafWetnessCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.LEAFWET].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [42, 43];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < 2; i++)
									{
										if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], out temp) ? temp.ToString("F1", InvC) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetLeafWetnessGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetLeafWetnessGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetLeafWetnessGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < 2; i++)
			{
				if (cumulus.GraphOptions.Visible.LeafWetness.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetUserTempGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sbExt = new StringBuilder[cumulus.GraphOptions.Visible.UserTemp.Vals.Length];

			for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
					sbExt[i] = new StringBuilder($"\"{cumulus.Trans.UserTempCaptions[i]}\":[");
			}

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.USERTEMP].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum
			// 92-95 Laser Distance 1-4
			// 96-99 Laser Depth 1-4
			// 100 Snowfall Accumulation 24h
			// 101-106 Temperature 11-16
			// 107-112 Humidity 11-16
			// 113-118 Dew point 11-16

			int[] fields = [76, 77, 78, 79, 80, 81, 82, 83];

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;
								var st = new List<string>(line.Split(','));
								entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
									{
										if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
										{
											var val = "null";
											if (fields[i] < st.Count)
											{
												val = double.TryParse(st[fields[i]], InvC, out temp) ? temp.ToFixed(cumulus.TempFormat) : "null";
											}
											sbExt[i].Append($"[{jsTime},{val}],");
										}
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetUserTempGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetUserTempGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetUserTempGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			for (var i = 0; i < cumulus.GraphOptions.Visible.UserTemp.Vals.Length; i++)
			{
				if (cumulus.GraphOptions.Visible.UserTemp.ValVisible(i, local))
				{
					if (sbExt[i][^1] == ',')
						sbExt[i].Length--;

					sbExt[i].Append(']');
					sb.Append((append ? "," : "") + sbExt[i]);
					append = true;
				}
			}

			sb.Append('}');
			return sb.ToString();
		}

		public string GetCo2SensorGraphData(bool incremental, bool local, DateTime? start = null, DateTime? end = null)
		{
			var append = false;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"CO2": [[time,val],[time,val],...],
				"CO2 Average": [[time,val],[time,val],...],
			*/

			var sbCo2 = new StringBuilder($"\"CO2\":[");
			var sbCo2Avg = new StringBuilder($"\"CO2 Average\":[");
			var sbPm25 = new StringBuilder($"\"PM2.5\":[");
			var sbPm25Avg = new StringBuilder($"\"PM 2.5 Average\":[");
			var sbPm10 = new StringBuilder($"\"PM 10\":[");
			var sbPm10Avg = new StringBuilder($"\"PM 10 Average\":[");
			var sbTemp = new StringBuilder($"\"Temperature\":[");
			var sbHum = new StringBuilder($"\"Humidity\":[");

			var finished = false;
			var dataAdded = false;
			var entryTs = 0L;
			DateTime dateFrom;
			DateTime dateTo;

			if (incremental)
			{
				dateFrom = start ?? cumulus.GraphDataFiles[(int) GraphFileIdx.CO2].LastDataTime;
				dateTo = DateTime.Now;
			}
			else if (start.HasValue && end.HasValue)
			{
				// selected period in whole days
				dateFrom = start.Value;
				dateTo = end.Value.AddDays(1);

				// convert start/end to meteo date/times if required
				dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
				dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			}
			else
			{
				// all data in the range
				dateFrom = DateTime.Now.AddHours(-cumulus.GraphHours);
				dateTo = DateTime.Now;
			}

			var fileDate = dateFrom;
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			// get the log file name to start
			var logFile = cumulus.GetExtraLogFileName(dateFrom);

			// 0  Date in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
			// 2-11  Temperature 1-10
			// 12-21 Humidity 1-10
			// 22-31 Dew point 1-10
			// 32-35 Soil temp 1-4
			// 36-39 Soil moisture 1-4
			// 40-41 Leaf temp 1-2
			// 42-43 Leaf wetness 1-2
			// 44-55 Soil temp 5-16
			// 56-67 Soil moisture 5-16
			// 68-71 Air quality 1-4
			// 72-75 Air quality avg 1-4
			// 76-83 User temperature 1-8
			// 84  CO2
			// 85  CO2 avg
			// 86  CO2 pm2.5
			// 87  CO2 pm2.5 avg
			// 88  CO2 pm10
			// 89  CO2 pm10 avg
			// 90  CO2 temp
			// 91  CO2 hum

			do
			{
				if (File.Exists(logFile))
				{
					var linenum = 0;
					var errorCount = 0;

					try
					{
						var lines = File.ReadAllLines(logFile);

						foreach (var line in lines)
						{
							try
							{
								// process each record in the file
								linenum++;

								var rec = new ExtraLogFileRec(line);
								entryTs = rec.UnixTimestamp;

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									dataAdded = true;
									var jsTime = entryTs * 1000;

									if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
										sbCo2.Append($"[{jsTime},{rec.CO2.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
										sbCo2Avg.Append($"[{jsTime},{rec.CO2_24h.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
										sbPm25.Append($"[{jsTime},{rec.CO2_pm2p5.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
										sbPm25Avg.Append($"[{jsTime},{rec.CO2_pm2p5_24h.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
										sbPm10.Append($"[{jsTime},{rec.CO2_pm10.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
										sbPm10Avg.Append($"[{jsTime},{rec.CO2_pm10_24h.ToFixed("F1", "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
										sbTemp.Append($"[{jsTime},{rec.CO2_temperature.ToFixed(cumulus.TempFormat, "null")}],");

									if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
										sbHum.Append($"[{jsTime},{rec.CO2_humidity.ToText("null")}],");
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetCo2SensorGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetCo2SensorGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetCo2SensorGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (entryTs >= tsTo || cumulus.MeteoDate(fileDate) > cumulus.MeteoDate(dateTo))
					{
						finished = true;
					}
					else
					{
						fileDate = fileDate.AddMonths(1);
						logFile = cumulus.GetExtraLogFileName(fileDate);
					}
				}
			} while (!finished);

			// no incremental data, send null to supress the upload
			if (incremental && !dataAdded)
				return null;

			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2.IsVisible(local))
			{
				if (sbCo2[^1] == ',')
					sbCo2.Length--;

				sbCo2.Append(']');
				sb.Append(sbCo2);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.CO2Avg.IsVisible(local))
			{
				if (sbCo2Avg[^1] == ',')
					sbCo2Avg.Length--;

				sbCo2Avg.Append(']');
				sb.Append((append ? "," : "") + sbCo2Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25.IsVisible(local))
			{
				if (sbPm25[^1] == ',')
					sbPm25.Length--;

				sbPm25.Append(']');
				sb.Append((append ? "," : "") + sbPm25);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm25Avg.IsVisible(local))
			{
				if (sbPm25Avg[^1] == ',')
					sbPm25Avg.Length--;

				sbPm25Avg.Append(']');
				sb.Append((append ? "," : "") + sbPm25Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10.IsVisible(local))
			{
				if (sbPm10[^1] == ',')
					sbPm10.Length--;

				sbPm10.Append(']');
				sb.Append((append ? "," : "") + sbPm10);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Pm10Avg.IsVisible(local))
			{
				if (sbPm10Avg[^1] == ',')
					sbPm10Avg.Length--;

				sbPm10Avg.Append(']');
				sb.Append((append ? "," : "") + sbPm10Avg);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Temp.IsVisible(local))
			{
				if (sbTemp[^1] == ',')
					sbTemp.Length--;

				sbTemp.Append(']');
				sb.Append((append ? "," : "") + sbTemp);
				append = true;
			}

			if (cumulus.GraphOptions.Visible.CO2Sensor.Hum.IsVisible(local))
			{
				if (sbHum[^1] == ',')
					sbHum.Length--;

				sbHum.Append(']');
				sb.Append((append ? "," : "") + sbHum);
			}

			sb.Append('}');
			return sb.ToString();
		}
	}
}
