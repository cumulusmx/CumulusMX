﻿using System;
using System.Collections.Generic;

namespace CumulusMX.Stations
{
	internal partial class EcowittExtraLogFile
	{
		private TempUnits TempUnit;
		private LaserUnits LaserUnit;
		private LightningDist LightningUnit;

		private const int fieldCount = 82;
		private readonly List<string> Data;
		private string[] Header;
		private readonly Cumulus cumulus;
		private readonly Dictionary<string, int> FieldIndex = [];
		private readonly int interval;

		public EcowittExtraLogFile(List<string> data, Cumulus cumul, int interval)
		{
			cumulus = cumul;
			Data = data;
			this.interval = interval;

			// parse the header
			HeaderParser(data[0]);
		}

		public SortedList<DateTime, EcowittApi.HistoricData> DataParser()
		{
			var invc = System.Globalization.CultureInfo.InvariantCulture;
			var retList = new SortedList<DateTime, EcowittApi.HistoricData>();

			var useTimestamp = FieldIndex.ContainsKey("timestamp");

			for (var index = 0; index < Data.Count; index++)
			{
				cumulus.LogDebugMessage($"EcowittExtraLogFile.DataParser: Preprocess record # {index + 1} of {Data.Count}");
				try
				{
					// split on commas
					var fields = Data[index].Split(',');

					if (fields.Length < fieldCount)
					{
						cumulus.LogErrorMessage($"EcowittExtraLogFile.DataParser: Error on line {index + 1} it contains {fields.Length} fields should be {fieldCount} or more");
						cumulus.LogDebugMessage($"EcowittExtraLogFile.DataParser: Line = " + Data[index]);
						return retList;
					}

					// 2025-01-10 12:34,           1.8,0.8,1.8,93,  3.3,1.5,3.3,88, 1.5,-0.1, 1.5,89, 1.6,-0.3, 1.6,87,-19.3, --,  --,--, 3.9, 2.7, 3.9,92,7.0,-3.0,7.0,49,--,--,--,--,77,--,--,--,--,--,--,--, 0,--,15.3,60,775,6.4,6.7,--,--,60,45,56,72,50,74,--,--,--,--,--,--,--,--,--,--,--,Normal,--,--,12.0,9.0,--,--,2.5,2.5,2.0,--,--,--,--,--,--,--,--,--
					// 2025-05-20 17:46,1747734370,1.8,0.8,1.8,93,  3.3,1.5,3.3,88, 1.5,-0.1, 1.5,89, 1.6,-0.3, 1.6,87,-19.3, --,  --,--, 3.9, 2.7, 3.9,92,7.0,-3.0,7.0,49,--,--,--,--,77,--,--,--,--,--,--,--, 0,--,15.3,60,775,6.4,6.7,--,--,60,45,56,72,50,74,--,--,--,--,--,--,--,--,--,--,--,Normal,--,--,12.0,9.0,--,--,2.5,2.5,2.0,--,--,--,--,--,--,--,--,--
					// 2025-06-12 13:44,1749732293,3.8,--,  --,--,-17.2, --, --,--,21.6,13.5,21.6,60,22.8,12.8,22.8,53,21.9,13.0,21.9,57,25.1,13.0,25.1,47, --,  --, --,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,  --,--, --, --, --,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,    --,--,--,  --,0.0,--,--, --, --, --,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,--,


					var rec = new EcowittApi.HistoricData();

					DateTime time;

					if (useTimestamp && long.TryParse(fields[1], invc, out long unix))
					{
						time = Utils.RoundDownUnixTimestamp(unix, interval).FromUnixTime();
					}
					else
					{
						if (DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm", invc, System.Globalization.DateTimeStyles.AssumeLocal, out time))
						{
							time = time.RoundTimeToInterval(interval);
						}
						else
						{
							cumulus.LogErrorMessage("EcowittExtraLogFile.DataParser: Failed to parse datetime - " + fields[0]);
							continue;
						}
					}

					cumulus.LogDebugMessage($"EcowittExtraLogFile.DataParser: Preprocessing record {fields[0]} - {time:yyyy-MM-dd HH:mm}");

					decimal varDec;
					int varInt;

					int idx;


					// Extra Temp/Hum sensors, fields 1 - 32
					for (var i = 1; i <= 8; i++)
					{
						if (FieldIndex.TryGetValue($"ch{i} temperature", out idx) && decimal.TryParse(fields[idx], invc, out varDec))
						{
							rec.ExtraTemp[i] = varDec;
						}

						if (FieldIndex.TryGetValue($"ch{i} humidity", out idx) && int.TryParse(fields[idx], out varInt))
						{
							rec.ExtraHumidity[i] = varInt;
						}
					}

					// Leaf Moisture Sensors
					for (var i = 1; i <= 8; i++)
					{
						if (FieldIndex.TryGetValue($"wh35 ch{i}hum", out idx) && int.TryParse(fields[idx], out varInt))
						{
							rec.LeafWetness[i] = varInt;
						}
					}

					// Lightning
					if (FieldIndex.TryGetValue("thunder time", out idx) && long.TryParse(fields[idx], invc, out long varLong)) rec.LightningTime = varLong.FromUnixTime();
					if (FieldIndex.TryGetValue("thunder count", out idx) && int.TryParse(fields[idx], out varInt)) rec.LightningCount = varInt;
					if (FieldIndex.TryGetValue("thunder distance", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.LightningDist = varDec;

					// AQ Indoor
					if (FieldIndex.TryGetValue("aqin temperature", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.AqiComboTemp = varDec;
					if (FieldIndex.TryGetValue("aqin humidity", out idx) && int.TryParse(fields[idx], out varInt)) rec.AqiComboHum = varInt;
					if (FieldIndex.TryGetValue("aqin co2", out idx) && int.TryParse(fields[idx], out varInt)) rec.AqiComboCO2 = varInt;
					if (FieldIndex.TryGetValue("aqin pm2.5", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.AqiComboPm25 = varDec;
					if (FieldIndex.TryGetValue("aqin pm10", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.AqiComboPm10 = varDec;
					//if (FieldIndex.TryGetValue("aqin pm1.0", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.AqiComboPm1 = varDec;
					//if (FieldIndex.TryGetValue("aqin pm4.0", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.AqiComboPm4 = varDec;

					// Soil Moisture
					for (int i = 1; i <= 16; i++)
					{
						if (FieldIndex.TryGetValue("soilmoisture ch" + i, out idx) && int.TryParse(fields[idx], out varInt)) rec.SoilMoist[i] = varInt;
					}

					// Water - unused
					/*
					for (int i = 1; i <= 4; i++)
					{
						if (FieldIndex.TryGetValue("water ch" + i, out idx) && int.TryParse(fields[idx], out varInt)) rec.Water[i] = varInt;
					}
					*/

					// AQI
					for (int i = 1; i <= 4; i++)
					{
						if (FieldIndex.TryGetValue("pm2.5 ch" + i, out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.pm25[i] = varDec;
					}

					// User Temps
					for (var i = 1; i <= 8 ; i++)
					{
						if (FieldIndex.TryGetValue("wn34 ch" + i, out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.UserTemp[i] = varDec;
					}

					// LDS Air
					// LDS Depth
					// LDS Heat
					for (int i = 1; i <= 4; i++)
					{
						if (FieldIndex.TryGetValue("lds_air ch" + i, out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.LdsAir[i] = varDec;
						if (FieldIndex.TryGetValue("lds_depth ch" + i, out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.LdsDepth[i] = varDec;
						//if (FieldIndex.TryGetValue("lds_heat ch" + i, out idx) && int.TryParse(fields[idx], out varInt)) rec.LdsHeat[i] = varInt;
					}

					// end of records

					// Do any required conversions

					if ((int) TempUnit != cumulus.Units.Temp)
					{
						// convert all the temperatures to user units
						if (cumulus.Units.Temp == 0)
						{
							// C
							for (var i = 0; i < 8; i++)
							{
								rec.ExtraTemp[i] = MeteoLib.FtoC(rec.ExtraTemp[i]);
								//rec.ExtraDewPoint[i] = MeteoLib.FtoC(rec.ExtraDewPoint[i]);
								//rec.ExtraHeatIndex[i] = MeteoLib.FtoC(rec.ExtraHeatIndex[i]);
								rec.UserTemp[i] = MeteoLib.FtoC(rec.UserTemp[i]);
							}

							rec.AqiComboTemp = MeteoLib.FtoC(rec.AqiComboTemp);
						}
						else
						{
							// F
							for (var i = 0; i < 8; i++)
							{
								rec.ExtraTemp[i] = MeteoLib.CToF(rec.ExtraTemp[i]);
								//rec.ExtraDewPoint[i] = MeteoLib.CToF(rec.ExtraDewPoint[i]);
								//rec.ExtraHeatIndex[i] = MeteoLib.CToF(rec.ExtraHeatIndex[i]);
								rec.UserTemp[i] = MeteoLib.CToF(rec.UserTemp[i]);
							}

							rec.AqiComboTemp = MeteoLib.CToF(rec.AqiComboTemp);
						}
					}

					switch (cumulus.Units.Wind)
					{
						case 0: // m/s
						case 2: // km/h
								// Convert miles to km if needed
							if (LightningUnit != LightningDist.km)
							{
								rec.LightningDist *= (decimal) 1.609344;
							}
							break;
						case 1: // mph
						case 3: // knots
								// Convert km to miles if needed
							if (LightningUnit != LightningDist.miles)
							{
								rec.LightningDist *= (decimal) 0.6213712;
							}
							break;
					}

					if ((int) LaserUnit != cumulus.Units.LaserDistance)
					{
						// convert all the laser distances to user units
						switch (LaserUnit)
						{
							case LaserUnits.mm:
								for (var i = 0; i < 4; i++)
								{
									rec.LdsAir[i] = ConvertUnits.LaserMmToUser(rec.LdsAir[i]);
									rec.LdsDepth[i] = ConvertUnits.LaserMmToUser(rec.LdsDepth[i]);
								}
								break;

							case LaserUnits.cm:
								for (var i = 0; i < 4; i++)
								{
									rec.LdsAir[i] = ConvertUnits.LaserMmToUser(rec.LdsAir[i] * 10);
									rec.LdsDepth[i] = ConvertUnits.LaserMmToUser(rec.LdsDepth[i] * 10);
								}
								break;

							case LaserUnits.inch:
								for (var i = 0; i < 4; i++)
								{
									rec.LdsAir[i] = ConvertUnits.LaserInchesToUser(rec.LdsAir[i]);
									rec.LdsDepth[i] = ConvertUnits.LaserInchesToUser(rec.LdsDepth[i]);
								}
								break;

							case LaserUnits.ft:
								for (var i = 0; i < 4; i++)
								{
									rec.LdsAir[i] = ConvertUnits.LaserInchesToUser(rec.LdsAir[i] / 12);
									rec.LdsDepth[i] = ConvertUnits.LaserInchesToUser(rec.LdsDepth[i] / 12);
								}
								break;

							case LaserUnits.m:
								for (var i = 0; i < 4; i++)
								{
									rec.LdsAir[i] = ConvertUnits.LaserMmToUser(rec.LdsAir[i] * 1000);
									rec.LdsDepth[i] = ConvertUnits.LaserMmToUser(rec.LdsDepth[i] * 1000);
								}
								break;
						}
					}

					if (!retList.TryAdd(time, rec))
					{
						cumulus.LogErrorMessage($"EcowittExtraLogFile.DataParser: Error duplicate record {index + 1} - {fields[0]}");
					}
					else
					{
						cumulus.LogDebugMessage($"EcowittExtraLogFile.DataParser: Record {fields[0]} - {time:yyyy-MM-dd HH:mm} added to the list");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("EcowittExtraLogFile.DataParser: Error processing record " + (index + 1) + " - " + ex.Message);
					cumulus.LogDebugMessage("EcowittExtraLogFile.DataParser: Record = " + Data[index]);
				}

			}

			return retList;
		}

		public static EcowittApi.HistoricData Merge(EcowittApi.HistoricData baseRec, EcowittApi.HistoricData extraRec)
		{
			baseRec.ExtraTemp = extraRec.ExtraTemp;
			baseRec.ExtraHumidity = extraRec.ExtraHumidity;
			baseRec.LeafWetness = extraRec.LeafWetness;
			baseRec.LightningCount = extraRec.LightningCount;
			baseRec.LightningDist = extraRec.LightningDist;
			baseRec.AqiComboTemp = extraRec.AqiComboTemp;
			baseRec.AqiComboHum = extraRec.AqiComboHum;
			baseRec.AqiComboCO2 = extraRec.AqiComboCO2;
			baseRec.AqiComboPm25 = extraRec.AqiComboPm25;
			baseRec.AqiComboPm10 = extraRec.AqiComboPm10;
			baseRec.SoilMoist = extraRec.SoilMoist;
			baseRec.pm25 = extraRec.pm25;
			baseRec.UserTemp = extraRec.UserTemp;
			baseRec.LdsAir = extraRec.LdsAir;
			baseRec.LdsDepth = extraRec.LdsDepth;

			return baseRec;
		}

		private void HeaderParser (string header)
		{
			// Time,          CH1 Temperature(℃),CH1 Dew point(℃),CH1 HeatIndex(℃),CH1 Humidity(%), CH2 Temperature(℃),CH2 Dew point(℃),CH2 HeatIndex(℃),CH2 Humidity(%), CH3 Temperature(℃),CH3 Dew point(℃),CH3 HeatIndex(℃),CH3 Humidity(%),CH4 Temperature(℃), CH4 Dew point(℃),CH4 HeatIndex(℃),CH4 Humidity(%),CH5 Temperature(℃), CH5 Dew point(℃),CH5 HeatIndex(℃),CH5 Humidity(%),CH6 Temperature(℃), CH6 Dew point(℃),CH6 HeatIndex(℃),CH6 Humidity(%),CH7 Temperature(℃),CH7 Dew point(℃),CH7 HeatIndex(℃),CH7 Humidity(%),CH8 Temperature(℃),CH8 Dew point(℃), CH8 HeatIndex(℃),CH8 Humidity(%), WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),             Thunder count,Thunder distance(km),AQIN Temperature(℃),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),                   SoilMoisture CH2(%),                   SoilMoisture CH3(%),                   SoilMoisture CH4(%),                   SoilMoisture CH5(%),                   SoilMoisture CH6(%),                   SoilMoisture CH7(%),                   SoilMoisture CH8(%),                   SoilMoisture CH9(%),                   SoilMoisture CH10(%),                    SoilMoisture CH11(%),                    SoilMoisture CH12(%),                    SoilMoisture CH13(%),                    SoilMoisture CH14(%),                    SoilMoisture CH15(%),                    SoilMoisture CH16(%),                    Water CH1,Water CH2,Water CH3,Water CH4,Pm2.5 CH1(ug/m3),Pm2.5 CH2(ug/m3),Pm2.5 CH3(ug/m3),Pm2.5 CH4(ug/m3),WN34 CH1(℃),WN34 CH2(℃),WN34 CH3(℃),WN34 CH4(℃),WN34 CH5(℃),WN34 CH6(℃),WN34 CH7(℃),WN34 CH8(℃),   LDS_Air CH1(mm),LDS_Air CH2(mm),LDS_Air CH3(mm),LDS_Air CH4(mm),
			// Time,          CH1 Temperature(℃),CH1 Dew point(℃),CH1 HeatIndex(℃),CH1 Humidity(%), CH2 Temperature(℃),CH2 Dew point(℃),CH2 HeatIndex(℃),CH2 Humidity(%), CH3 Temperature(℃),CH3 Dew point(℃),CH3 HeatIndex(℃),CH3 Humidity(%),CH4 Temperature(℃), CH4 Dew point(℃),CH4 HeatIndex(℃),CH4 Humidity(%),CH5 Temperature(℃), CH5 Dew point(℃),CH5 HeatIndex(℃),CH5 Humidity(%),CH6 Temperature(℃), CH6 Dew point(℃),CH6 HeatIndex(℃),CH6 Humidity(%),CH7 Temperature(℃),CH7 Dew point(℃),CH7 HeatIndex(℃),CH7 Humidity(%),CH8 Temperature(℃),CH8 Dew point(℃), CH8 HeatIndex(℃),CH8 Humidity(%), WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),             Thunder count,Thunder distance(km),AQIN Temperature(℃),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),                   SoilMoisture CH2(%),                   SoilMoisture CH3(%),                   SoilMoisture CH4(%),                   SoilMoisture CH5(%),                   SoilMoisture CH6(%),                   SoilMoisture CH7(%),                   SoilMoisture CH8(%),                   SoilMoisture CH9(%),                   SoilMoisture CH10(%),                    SoilMoisture CH11(%),                    SoilMoisture CH12(%),                    SoilMoisture CH13(%),                    SoilMoisture CH14(%),                    SoilMoisture CH15(%),                    SoilMoisture CH16(%),                    Water CH1,Water CH2,Water CH3,Water CH4,Pm2.5 CH1(ug/m3),Pm2.5 CH2(ug/m3),Pm2.5 CH3(ug/m3),Pm2.5 CH4(ug/m3),WN34 CH1(℃),WN34 CH2(℃),WN34 CH3(℃),WN34 CH4(℃),WN34 CH5(℃),WN34 CH6(℃),WN34 CH7(℃),WN34 CH8(℃),   LDS_Air CH1(mm),LDS_Depth CH1(mm),LDS_Heat CH1,LDS_Air CH2(mm),LDS_Depth CH2(mm),LDS_Heat CH2,LDS_Air CH3(mm),LDS_Depth CH3(mm),LDS_Heat CH3,LDS_Air CH4(mm),LDS_Depth CH4(mm),LDS_Heat CH4,
			// Time,Timestamp,CH1 Temperature(℃),CH1 Dew point(℃),CH1 HeatIndex(℃),CH1 Humidity(%), CH2 Temperature(℃), CH2 Dew point(℃),CH2 HeatIndex(℃),CH2 Humidity(%),CH3 Temperature(℃),CH3 Dew point(℃),CH3 HeatIndex(℃),CH3 Humidity(%),CH4 Temperature(℃), CH4 Dew point(℃),CH4 HeatIndex(℃),CH4 Humidity(%),CH5 Temperature(℃), CH5 Dew point(℃),CH5 HeatIndex(℃),CH5 Humidity(%),CH6 Temperature(℃), CH6 Dew point(℃),CH6 HeatIndex(℃),CH6 Humidity(%),CH7 Temperature(℃),CH7 Dew point(℃),CH7 HeatIndex(℃),CH7 Humidity(%),CH8 Temperature(℃), CH8 Dew point(℃),CH8 HeatIndex(℃),CH8 Humidity(%), WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),             Thunder count,Thunder distance(km),AQIN Temperature(℃),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),                   SoilMoisture CH2(%),                   SoilMoisture CH3(%),                   SoilMoisture CH4(%),                   SoilMoisture CH5(%),                   SoilMoisture CH6(%),                   SoilMoisture CH7(%),                   SoilMoisture CH8(%),                   SoilMoisture CH9(%),                   SoilMoisture CH10(%),                    SoilMoisture CH11(%),                    SoilMoisture CH12(%),                    SoilMoisture CH13(%),                    SoilMoisture CH14(%),                    SoilMoisture CH15(%),                    SoilMoisture CH16(%),                    Water CH1,Water CH2,Water CH3,Water CH4,Pm2.5 CH1(ug/m3),Pm2.5 CH2(ug/m3),Pm2.5 CH3(ug/m3),Pm2.5 CH4(ug/m3),WN34 CH1(℃),WN34 CH2(℃),WN34 CH3(℃),WN34 CH4(℃), WN34 CH5(℃),WN34 CH6(℃),WN34 CH7(℃),WN34 CH8(℃),  LDS_Air CH1(mm),LDS_Depth CH1(mm),LDS_Heat CH1,LDS_Air CH2(mm),LDS_Depth CH2(mm),LDS_Heat CH2,LDS_Air CH3(mm),LDS_Depth CH3(mm),LDS_Heat CH3,LDS_Air CH4(mm),LDS_Depth CH4(mm),LDS_Heat CH4,
			// Time,Timestamp,CH1 Temperature(°C),CH1 Dew point(°C),CH1 HeatIndex(°C),CH1 Humidity(%),CH2 Temperature(°C),CH2 Dew point(°C),CH2 HeatIndex(°C),CH2 Humidity(%),CH3 Temperature(°C),CH3 Dew point(°C),CH3 HeatIndex(°C),CH3 Humidity(%),CH4 Temperature(°C),CH4 Dew point(°C),CH4 HeatIndex(°C),CH4 Humidity(%),CH5 Temperature(°C),CH5 Dew point(°C),CH5 HeatIndex(°C),CH5 Humidity(%),CH6 Temperature(°C),CH6 Dew point(°C),CH6 HeatIndex(°C),CH6 Humidity(%),CH7 Temperature(°C),CH7 Dew point(°C),CH7 HeatIndex(°C),CH7 Humidity(%),CH8 Temperature(°C),CH8 Dew point(°C),CH8 HeatIndex(°C),CH8 Humidity(%),WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),Thunder time,Thunder count,Thunder distance(mi),AQIN Temperature(°C),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),                   SoilMoisture CH2(%),                   SoilMoisture CH3(%),                   SoilMoisture CH4(%),                   SoilMoisture CH5(%),                   SoilMoisture CH6(%),                   SoilMoisture CH7(%),                   SoilMoisture CH8(%),                   SoilMoisture CH9(%),                   SoilMoisture CH10(%),                    SoilMoisture CH11(%),                    SoilMoisture CH12(%),                    SoilMoisture CH13(%),                    SoilMoisture CH14(%),                    SoilMoisture CH15(%),                    SoilMoisture CH16(%),                    Water CH1,Water CH2,Water CH3,Water CH4,PM2.5 CH1(ug/m3),PM2.5 CH2(ug/m3),PM2.5 CH3(ug/m3),PM2.5 CH4(ug/m3),WN34 CH1(°C),WN34 CH2(°C),WN34 CH3(°C),WN34 CH4(°C),WN34 CH5(°C),WN34 CH6(°C),WN34 CH7(°C),WN34 CH8(°C),LDS_Air CH1(mm),LDS_Depth CH1(mm),LDS_Heat CH1,LDS_Air CH2(mm),LDS_Depth CH2(mm),LDS_Heat CH2,LDS_Air CH3(mm),LDS_Depth CH3(mm),LDS_Heat CH3,LDS_Air CH4(mm),LDS_Depth CH4(mm),LDS_Heat CH4,
			// Time,Timestamp,CH1 Temperature(°C),CH1 Dew point(°C),CH1 HeatIndex(°C),CH1 Humidity(%),CH2 Temperature(°C),CH2 Dew point(°C),CH2 HeatIndex(°C),CH2 Humidity(%),CH3 Temperature(°C),CH3 Dew point(°C),CH3 HeatIndex(°C),CH3 Humidity(%),CH4 Temperature(°C),CH4 Dew point(°C),CH4 HeatIndex(°C),CH4 Humidity(%),CH5 Temperature(°C),CH5 Dew point(°C),CH5 HeatIndex(°C),CH5 Humidity(%),CH6 Temperature(°C),CH6 Dew point(°C),CH6 HeatIndex(°C),CH6 Humidity(%),CH7 Temperature(°C),CH7 Dew point(°C),CH7 HeatIndex(°C),CH7 Humidity(%),CH8 Temperature(°C),CH8 Dew point(°C),CH8 HeatIndex(°C),CH8 Humidity(%),WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),Thunder time,Thunder count,Thunder distance(km),AQIN Temperature(°C),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),SoilMoistureAD CH1,SoilMoisture CH2(%),SoilMoistureAD CH2,SoilMoisture CH3(%),SoilMoistureAD CH3,SoilMoisture CH4(%),SoilMoistureAD CH4,SoilMoisture CH5(%),SoilMoistureAD CH5,SoilMoisture CH6(%),SoilMoistureAD CH6,SoilMoisture CH7(%),SoilMoistureAD CH7,SoilMoisture CH8(%),SoilMoistureAD CH8,SoilMoisture CH9(%),SoilMoistureAD CH9,SoilMoisture CH10(%),SoilMoistureAD CH10,SoilMoisture CH11(%),SoilMoistureAD CH11,SoilMoisture CH12(%),SoilMoistureAD CH12,SoilMoisture CH13(%),SoilMoistureAD CH13,SoilMoisture CH14(%),SoilMoistureAD CH14,SoilMoisture CH15(%),SoilMoistureAD CH15,SoilMoisture CH16(%),SoilMoistureAD CH16,Water CH1,Water CH2,Water CH3,Water CH4,PM2.5 CH1(ug/m3),PM2.5 CH2(ug/m3),PM2.5 CH3(ug/m3),PM2.5 CH4(ug/m3),WN34 CH1(°C),WN34 CH2(°C),WN34 CH3(°C),WN34 CH4(°C),WN34 CH5(°C),WN34 CH6(°C),WN34 CH7(°C),WN34 CH8(°C),LDS_Air CH1(mm),LDS_Depth CH1(mm),LDS_Heat CH1,LDS_Air CH2(mm),LDS_Depth CH2(mm),LDS_Heat CH2,LDS_Air CH3(mm),LDS_Depth CH3(mm),LDS_Heat CH3,LDS_Air CH4(mm),LDS_Depth CH4(mm),LDS_Heat CH4,
			cumulus.LogDataMessage($"EcowittExtraLogFile.HeaderParser: File header: {header}");

			// split on commas
			var fields = header.Split(',');

			// Save the header
			Header = fields;

			// remove header line from the data
			Data.RemoveAt(0);

			if (fields.Length < fieldCount)
			{
				// invalid header
				cumulus.LogErrorMessage("EcowittExtraLogFile.HeaderParser: Invalid header in file = " + header);
				return;
			}

			// create a fields index
			var unitRegex = UnitsRegEx();

			for (var i = 0; i < fields.Length; i++)
			{
				string cleanedHeader = unitRegex.Replace(fields[i], "").Trim();
				FieldIndex[cleanedHeader.ToLower()] = i;
			}

			if (FieldIndex.TryGetValue("ch1 temperature", out int idx))
			{
				TempUnit = fields[idx].ToLower().EndsWith("c)") || fields[idx].EndsWith("℃)") ? TempUnits.C : TempUnits.F;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittExtraLogFile.HeaderParser: Unable to determine temperature units, defaulting to Cumulus units");
				TempUnit = (TempUnits) cumulus.Units.Temp;
			}

			if (FieldIndex.TryGetValue("lds_air ch1", out idx))
			{
				switch (fields[idx].ToLower())
				{
					case string s when s.EndsWith("mm)"):
						LaserUnit = LaserUnits.mm;
						break;
					case string s when s.EndsWith("inch)"):
						LaserUnit = LaserUnits.inch;
						break;
					case string s when s.EndsWith("cm)"):
						LaserUnit = LaserUnits.cm;
						break;
					case string s when s.EndsWith("ft)"):
						LaserUnit = LaserUnits.ft;
						break;
					case string s when s.EndsWith("(m)"):
						LaserUnit = LaserUnits.m;
						break;
					default:
						cumulus.LogErrorMessage("EcowittExtraLogFile.HeaderParser: Invalid unit supplied for Laser = " + fields[idx]);
						break;
				}
			}
			else
			{
				cumulus.LogErrorMessage("EcowittExtraLogFile.HeaderParser: Unable to determine laser units, defaulting to Cumulus units");
				LaserUnit = (LaserUnits) cumulus.Units.LaserDistance;
			}

			if (FieldIndex.TryGetValue("thunder distance", out idx))
			{
				LightningUnit = fields[FieldIndex["thunder distance"]].ToLower().EndsWith("km)") ? LightningDist.km : LightningDist.miles;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittExtraLogFile.HeaderParser: Unable to determine thunder distance units, defaulting to Cumulus units");
				LightningUnit = cumulus.Units.Wind switch
				{
					0 or 2 => LightningDist.km,
					1 or 3 => LightningDist.miles,
					_ => LightningDist.km
				};
			}
		}


		private enum TempUnits
		{
			C = 0,
			F = 1
		}

		private enum LaserUnits
		{
			cm = 0,
			inch = 1,
			mm = 2,
			ft = 3,
			m = 4
		}

		private enum LightningDist
		{
			km = 0,
			miles = 1
		}

		public class Record
		{
			public DateTime Time { get; set; }
			public double?[] ExtraTemp { get; set; } = new double?[8];
			public double?[] ExtraDewPoint { get; set; } = new double?[8];
			public double?[] ExtraHeatIndex { get; set; } = new double?[8];
			public int?[] ExtraHumidity { get; set; } = new int?[8];
			public int?[] LeafMoist {  get; set; } = new int?[8];
			public int? LightningCount { get; set; }
			public double? LightningDist { get; set; }
			public double? AqiInTemp { get; set; }
			public int? AqiInHum { get; set; }
			public int? AqiInCO2 { get; set; }
			public double? AqiInPm2p5 { get; set; }
			public double? AqiInPm10 { get; set; }
			public double? AqiInPm1 { get; set; }
			public double? AqiInPm4 { get; set; }
			public int?[] SoilMoist { get; set; } = new int?[16];
			public string[] Water { get; set; } = new string[4];
			public double?[] AqiPm2p5 { get; set; } = new double?[4];
			public double?[] UserTemp { get; set; } = new double?[8];
			public double?[] LdsAir { get; set; } = new double?[4];
		}

		[System.Text.RegularExpressions.GeneratedRegex(@"\(([^)]*)\)")]
		private partial System.Text.RegularExpressions.Regex UnitsRegEx();
	}
}
