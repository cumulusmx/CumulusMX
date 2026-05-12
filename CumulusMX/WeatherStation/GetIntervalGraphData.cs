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
		public string GetIntervalAqGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{", 10240);

			/* returns data in the form of an object with properties for each data series
				"sensor 1": [[time,val],[time,val],...],
				"sensor 4": [[time,val],[time,val],...],
			*/

			var sb2p5 = new StringBuilder("\"pm2p5\":[");
			var sb10 = new StringBuilder("\"pm10\":[");

			var useExtraSensorLogFile = true; // use the extra log file or the AirLink log file

			var pm25idx = 0; // index of the PM2.5 sensor in the log file

			var pm10idx = 0; // index of the PM10 sensor in the log file

			// first determine which if the sensors - if any - to use
			switch (cumulus.StationOptions.PrimaryAqSensor)
			{
				case (int) Cumulus.PrimaryAqSensor.Undefined:
					return "{}"; // no sensors defined
				case (int) Cumulus.PrimaryAqSensor.Sensor1:
					pm25idx = 68;
					pm10idx = 119;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor2:
					pm25idx = 69;
					pm10idx = 120;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor3:
					pm25idx = 70;
					pm10idx = 121;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.Sensor4:
					pm25idx = 71;
					pm10idx = 122;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.EcowittCO2:
					pm25idx = 86;
					pm10idx = 88;
					useExtraSensorLogFile = true;
					break;
				case (int) Cumulus.PrimaryAqSensor.AirLinkIndoor:
					pm25idx = 5;
					pm10idx = 10;
					useExtraSensorLogFile = false;
					break;
				case (int) Cumulus.PrimaryAqSensor.AirLinkOutdoor:
					pm25idx = 32;
					pm10idx = 37;
					useExtraSensorLogFile = false;
					break;
			}

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));

			var fileDate = dateFrom;
			var logFile = useExtraSensorLogFile ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetAirLinkLogFileName(fileDate);

			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalAqGraphData: Processing log file - {logFile}");
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
								var entryTs = long.Parse(st[1]);

								if (entryTs > tsFrom)
								{
									if (entryTs > tsTo)
									{
										finished = true;
										break;
									}

									// entry is from required period
									var temp = 0.0;
									var jsTime = entryTs * 1000;

									if (st.Count > pm25idx && double.TryParse(st[pm25idx], InvC, out temp))
									{
										sb2p5.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");
									}
									else
									{
										sb2p5.Append($"[{jsTime},null],");
									}

									if (st.Count > pm10idx && double.TryParse(st[pm10idx], InvC, out temp))
									{
										sb10.Append($"[{jsTime},{temp.ToString("F1", InvC)}],");
									}
									else
									{
										sb10.Append($"[{jsTime},null],");
									}
								}
							}
							catch (Exception ex)
							{
								errorCount++;
								cumulus.LogErrorMessage($"GetIntervalAqGraphData: Error at line {linenum} of {logFile}. Error - {ex.Message}");
								if (errorCount > 10)
								{
									cumulus.LogMessage($"GetIntervalAqGraphData: More than 10 errors in the file {logFile}, aborting processing");
									finished = true;
									break;
								}
							}
						}
					}
					catch (Exception ex)
					{
						cumulus.LogErrorMessage($"GetIntervalAqGraphData: Error reading {logFile}. Error - {ex.Message}");
					}
				}

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);
					logFile = useExtraSensorLogFile ? cumulus.GetExtraLogFileName(fileDate) : cumulus.GetAirLinkLogFileName(fileDate);
				}
			} while (!finished);

			if (sb2p5[^1] == ',')
				sb2p5.Length--;

			sb2p5.Append("],");
			sb.Append(sb2p5);

			if (sb10[^1] == ',')
				sb10.Length--;

			sb10.Append(']');
			sb.Append(sb10);

			sb.Append('}');
			return sb.ToString();
		}


		public string GetIntervalTempGraphData(bool local, DateTime? start = null, DateTime? end = null)
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

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalTempGraphData: Processing log file - {logFile}");
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

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line))
									continue;

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalTempGraphData: Finished processing the log files");
									break;
								}

								var jsTime = recTs * 1000;

								if (cumulus.GraphOptions.Visible.InTemp.IsVisible(local))
									sbIn.Append($"[{jsTime},{rec.IndoorTemperature.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.DewPoint.IsVisible(local))
									sbDew.Append($"[{jsTime},{rec.OutdoorDewpoint.ToFixed(cumulus.TempFormat)}],");

								if (cumulus.GraphOptions.Visible.AppTemp.IsVisible(local))
									sbApp.Append($"[{jsTime},{rec.ApparentTemperature.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.FeelsLike.IsVisible(local))
									sbFeel.Append($"[{jsTime},{rec.FeelsLike.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.WindChill.IsVisible(local))
									sbChill.Append($"[{jsTime},{rec.WindChill.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.HeatIndex.IsVisible(local))
									sbHeat.Append($"[{jsTime},{rec.HeatIndex.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.Temp.IsVisible(local))
									sbTemp.Append($"[{jsTime},{rec.OutdoorTemperature.ToFixed(cumulus.TempFormat)}],");

								if (cumulus.GraphOptions.Visible.Humidex.IsVisible(local))
									sbHumidex.Append($"[{jsTime},{rec.Humidex.ToFixed(cumulus.TempFormat, "null")}],");

								if (cumulus.GraphOptions.Visible.BGT.IsVisible(local))
								{
									sbBgt.Append($"[{jsTime},{rec.BlackGlobeTemp.ToFixed(cumulus.TempFormat, "null")}],");
									sbWbgt.Append($"[{jsTime},{rec.WetBulbGlobeTemp.ToFixed(cumulus.TempFormat, "null")}],");
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalTempGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalTempGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalTempGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalTempGraphData: Error reading the logfile {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalTempGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalTempGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

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
				append = true;
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

		public string GetIntervalHumGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var sb = new StringBuilder("{", 10240);
			var sbOut = new StringBuilder("\"hum\":[");
			var sbIn = new StringBuilder("\"inhum\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalHumGraphData: Processing log file - {logFile}");
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

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line))
									continue;

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalHumGraphData: Finished processing the log files");
									break;
								}

								var jsTime = recTs * 1000;

								if (cumulus.GraphOptions.Visible.OutHum.IsVisible(local))
								{
									sbOut.Append($"[{jsTime},{rec.OutdoorHumidity}],");
								}
								if (cumulus.GraphOptions.Visible.InHum.IsVisible(local))
								{
									sbIn.Append($"[{jsTime},{(rec.IndoorHumidity.HasValue ? rec.IndoorHumidity.Value : "null")}],");
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalHumGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalHumGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalHumGraphData: Too many errors reading {logFile} - aborting load of data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalHumGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalHumGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalHumGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);


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

		public string GetIntervalSolarGraphData(bool local, DateTime? start = null, DateTime? end = null)
		{
			var sb = new StringBuilder("{");
			var sbUv = new StringBuilder("\"UV\":[");
			var sbSol = new StringBuilder("\"SolarRad\":[");
			var sbMax = new StringBuilder("\"CurrentSolarMax\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Processing log file - {logFile}");
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

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line)) continue;

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalSolarGraphData: Finished processing the log files");
									break;
								}

								var jsTime = recTs * 1000;

								if (cumulus.GraphOptions.Visible.UV.IsVisible(local))
								{
									sbUv.Append($"[{jsTime},{rec.UV.ToFixed(cumulus.UVFormat, "null")}],");
								}

								if (cumulus.GraphOptions.Visible.Solar.IsVisible(local))
								{
									sbSol.Append($"[{jsTime},{(rec.SolarRad.HasValue ? (int) rec.SolarRad.Value : "null")}],");

									sbMax.Append($"[{jsTime},{(int) rec.CurrentSolarMax}],");
								}
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalSolarGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalSolarGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalSolarGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalSolarGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					if (cumulus.MeteoDate(fileDate) > cumulus.MeteoDate())
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

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

		public string GetIntervalPressGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"press\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervaPressGraphData: Processing log file - {logFile}");
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

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line))
									continue;

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervaPressGraphData: Finished processing the log files");
									break;
								}

								sb.Append($"[{rec.UnixTimestamp * 1000},{rec.Pressure.ToString(cumulus.PressFormat, InvC)}],");
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervaPressGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervaPressGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervaPressGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervaPressGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervaPressGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervaPressGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (sb[^1] == ',')
				sb.Length--;

			sb.Append("]}");
			return sb.ToString();

		}

		public string GetIntervalWindGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{\"wgust\":[");
			var sbSpd = new StringBuilder("\"wspeed\":[");
			var sbBrg = new StringBuilder("\"bearing\":[");
			var sbAvgBrg = new StringBuilder("\"avgbearing\":[");


			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervalWindGraphData: Processing log file - {logFile}");
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

								// skip empty lines
								if (string.IsNullOrWhiteSpace(line))
									continue;

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervalWindGraphData: Finished processing the log files");
									break;
								}

								var jsTime = rec.UnixTimestamp * 1000;

								sb.Append($"[{jsTime},{rec.RecentMaxGust.ToString(cumulus.WindFormat, InvC)}],");

								sbSpd.Append($"[{jsTime},{rec.WindAverage.ToString(cumulus.WindAvgFormat, InvC)}],");

								sbBrg.Append($"[{jsTime},{rec.Bearing}],");

								sbAvgBrg.Append($"[{jsTime},{rec.AvgBearing}],");
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervalWindGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervalWindGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervalWindGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervalWindGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervalWindGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervalWindGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

			if (sb[^1] == ',')
			{
				sb.Length--;
				sbSpd.Length--;
				sbBrg.Length--;
				sbAvgBrg.Length--;
			}

			sb.Append("],");
			sb.Append(sbSpd);
			sb.Append("],");
			sb.Append(sbBrg);
			sb.Append("],");
			sb.Append(sbAvgBrg);
			sb.Append(']');
			sb.Append('}');
			return sb.ToString();
		}

		public string GetIntervalRainGraphData(DateTime? start = null, DateTime? end = null)
		{
			var InvC = CultureInfo.InvariantCulture;
			var sb = new StringBuilder("{");
			var sbRain = new StringBuilder("\"rfall\":[");
			var sbRate = new StringBuilder("\"rrate\":[");

			var dateFrom = start ?? cumulus.RecordsBeganDateTime;
			var dateTo = end ?? DateTime.Now.Date;
			dateTo = dateTo.AddDays(1);

			// convert start/end to meteo date/times if required
			dateFrom = dateFrom.AddHours(-cumulus.GetHourInc(dateFrom));
			dateTo = dateTo.AddHours(-cumulus.GetHourInc(dateTo));
			var tsFrom = dateFrom.ToUnixTime();
			var tsTo = dateTo.ToUnixTime();

			var fileDate = dateFrom;
			var logFile = cumulus.GetLogFileName(fileDate);

			var finished = false;

			do
			{
				if (File.Exists(logFile))
				{
					cumulus.LogDebugMessage($"GetIntervaRainGraphData: Processing log file - {logFile}");
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

								var rec = new LogFileRec(line);
								var recTs = rec.UnixTimestamp;

								if (recTs < tsFrom)
									continue;

								if (recTs > tsTo)
								{
									finished = true;
									cumulus.LogDebugMessage("GetIntervaRainGraphData: Finished processing the log files");
									break;
								}

								var jsTime = rec.UnixTimestamp * 1000;

								sbRain.Append($"[{jsTime},{rec.RainToday.ToString(cumulus.RainFormat, InvC)}],");

								sbRate.Append($"[{jsTime},{rec.RainRate.ToString(cumulus.RainFormat, InvC)}],");
							}
							catch (Exception e)
							{
								cumulus.LogWarningMessage($"GetIntervaRainGraphData: Error at line {linenum} of {logFile} : {e.Message}");
								cumulus.LogDebugMessage($"GetIntervaRainGraphData: Error at line {linenum}, content: {lines[linenum]}");
								cumulus.LogMessage("Please edit the file to correct the error");
								errorCount++;
								if (errorCount >= 10)
								{
									cumulus.LogErrorMessage($"GetIntervaRainGraphData: Too many errors reading {logFile} - aborting load of graph data");
									break;
								}
							}
						}
					}
					catch (Exception e)
					{
						cumulus.LogErrorMessage($"GetIntervaRainGraphData: Error at line {linenum} of {logFile} : {e.Message}");
						cumulus.LogMessage("Please edit the file to correct the error");
					}
				}
				else
				{
					cumulus.LogDebugMessage($"GetIntervaRainGraphData: Log file  not found - {logFile}");
				}

				cumulus.LogDebugMessage($"GetIntervaRainGraphData: Finished processing log file - {logFile}");

				if (!finished)
				{
					if (fileDate > dateTo)
						break;

					fileDate = fileDate.AddMonths(1);

					logFile = cumulus.GetLogFileName(fileDate);
				}
			} while (!finished);

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

	}
}
