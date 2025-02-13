using System;
using System.Collections.Generic;

namespace CumulusMX
{
	internal class EcowittExtraLogFile
	{
		private TempUnits TempUnit;

		private const int fieldCount = 83;
		private readonly List<string> Data;
		private readonly Cumulus cumulus;

		public EcowittExtraLogFile(List<string> data, Cumulus cumul)
		{
			cumulus = cumul;
			Data = data;

			// parse the header
			HeaderParser(data[0]);
		}

		public SortedList<DateTime, EcowittApi.HistoricData> DataParser()
		{
			var invc = System.Globalization.CultureInfo.InvariantCulture;
			var retList = new SortedList<DateTime, EcowittApi.HistoricData>();

			for (var index = 0; index < Data.Count; index++)
			{
				// split on commas
				var fields = Data[index].Split(',');

				if (fields.Length < fieldCount)
				{
					cumulus.LogErrorMessage($"EcowittExtraLogFile.DataParser: Error on line {index + 1} it contains {fields.Length} fields should be {fieldCount} or more");
					cumulus.LogDebugMessage($"EcowittExtraLogFile.DataParser: Line = " + Data[index]);
					return retList;
				}

				// 2025-01-10 12:34,1.8,0.8,1.8,93,3.3,1.5,3.3,88,1.5,-0.1,1.5,89,1.6,-0.3,1.6,87,-19.3,--,--,--,3.9,2.7,3.9,92,7.0,-3.0,7.0,49,--,--,--,--,77,--,--,--,--,--,--,--,0,--,15.3,60,775,6.4,6.7,--,--,60,45,56,72,50,74,--,--,--,--,--,--,--,--,--,--,--,Normal,--,--,12.0,9.0,--,--,2.5,2.5,2.0,--,--,--,--,--,--,--,--,--


				var rec = new EcowittApi.HistoricData();

				if (!DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm", invc, System.Globalization.DateTimeStyles.AssumeLocal, out DateTime time))
				{
					cumulus.LogErrorMessage("EcowittExtraLogFile.DataParser: Failed to parse datetime - " + fields[0]);
					continue;
				}

				decimal varDec;
				int varInt;

				var field = 1;

				// Extra Temp/Hum sensors
				for (var i = 1; i <= 8; i++)
				{
					if (decimal.TryParse(fields[field++], invc, out varDec)) rec.ExtraTemp[i] = varDec;
					//if (decimal.TryParse(fields[field++], invc, out varDec)) rec.ExtraDewPoint[i] = varDec;
					field++;
					//if (decimal.TryParse(fields[field++], invc, out varDec)) rec.ExtraHeatIndex[i] = varDec;
					field++;
					if (int.TryParse(fields[field++], out varInt)) rec.ExtraHumidity[i] = varInt;
				}

				// Leaf Moisture Sensors
				for (var i = 1; i <= 8; i++)
				{
					if (int.TryParse(fields[field++], out varInt)) rec.LeafWetness[i] = varInt;
				}

				// Lightning
				if (int.TryParse(fields[field++], out varInt)) rec.LightningCount = varInt;
				if (int.TryParse(fields[field++], out varInt)) rec.LightningDist = varInt;

				// AQ Indoor
				if (decimal.TryParse(fields[field++], invc, out varDec)) rec.AqiComboTemp = varDec;
				if (int.TryParse(fields[field++], out varInt)) rec.AqiComboHum = varInt;
				if (int.TryParse(fields[field++], out varInt)) rec.AqiComboCO2 = varInt;
				if (decimal.TryParse(fields[field++], invc, out varDec)) rec.AqiComboPm25 = varDec;
				if (decimal.TryParse(fields[field++], invc, out varDec)) rec.AqiComboPm10 = varDec;
				//if (double.TryParse(fields[field++], invc, out varDec)) rec.AqiInPm1 = varDec;
				field++;
				//if (double.TryParse(fields[field++], invc, out varDec)) rec.AqiInPm4 = varDec;
				field++;

				// Soil Moisture
				for (int i = 1; i <= 16; i++)
				{
					if (int.TryParse(fields[field++], out varInt)) rec.SoilMoist[i] = varInt;
				}

				// Water
				for (int i = 1; i <= 4; i++)
				{
					//if (fields[field++] != "--") rec.Water[i] = fields[field++];
					field++;
				}

				// AQI
				for (int i = 1; i <= 4; i++)
				{
					if (decimal.TryParse(fields[field++], invc, out varDec)) rec.pm25[i] = varDec;
				}

				// User Temps
				for (var i = 1; i <= 8 ; i++)
				{
					if (decimal.TryParse(fields[field++], invc, out varDec)) rec.UserTemp[i] = varDec;
				}

				// LDS Air
				for (int i = 1; i <= 4; i++)
				{
					if (decimal.TryParse(fields[field++], invc, out varDec)) rec.LdsAir[i] = varDec;
				}

				// end of records

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

				retList.Add(time, rec);
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

			return baseRec;
		}

		private void HeaderParser (string header)
		{
			// Time,CH1 Temperature(℃),CH1 Dew point(℃),CH1 HeatIndex(℃),CH1 Humidity(%),CH2 Temperature(℃),CH2 Dew point(℃),CH2 HeatIndex(℃),CH2 Humidity(%),CH3 Temperature(℃),CH3 Dew point(℃),CH3 HeatIndex(℃),CH3 Humidity(%),CH4 Temperature(℃),CH4 Dew point(℃),CH4 HeatIndex(℃),CH4 Humidity(%),CH5 Temperature(℃),CH5 Dew point(℃),CH5 HeatIndex(℃),CH5 Humidity(%),CH6 Temperature(℃),CH6 Dew point(℃),CH6 HeatIndex(℃),CH6 Humidity(%),CH7 Temperature(℃),CH7 Dew point(℃),CH7 HeatIndex(℃),CH7 Humidity(%),CH8 Temperature(℃),CH8 Dew point(℃),CH8 HeatIndex(℃),CH8 Humidity(%),WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),Thunder count,Thunder distance(km),AQIN Temperature(℃),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),SoilMoisture CH2(%),SoilMoisture CH3(%),SoilMoisture CH4(%),SoilMoisture CH5(%),SoilMoisture CH6(%),SoilMoisture CH7(%),SoilMoisture CH8(%),SoilMoisture CH9(%),SoilMoisture CH10(%),SoilMoisture CH11(%),SoilMoisture CH12(%),SoilMoisture CH13(%),SoilMoisture CH14(%),SoilMoisture CH15(%),SoilMoisture CH16(%),Water CH1,Water CH2,Water CH3,Water CH4,Pm2.5 CH1(ug/m3),Pm2.5 CH2(ug/m3),Pm2.5 CH3(ug/m3),Pm2.5 CH4(ug/m3),WN34 CH1(℃),WN34 CH2(℃),WN34 CH3(℃),WN34 CH4(℃),WN34 CH5(℃),WN34 CH6(℃),WN34 CH7(℃),WN34 CH8(℃),LDS_Air CH1(mm),LDS_Air CH2(mm),LDS_Air CH3(mm),LDS_Air CH4(mm),

			cumulus.LogDataMessage($"EcowittExtraLogFile.HeaderParser: File header: {header}");

			// split on commas
			var fields = header.Split(',');

			if (fields.Length < fieldCount)
			{
				// invalid header
				throw new ArgumentException("Invalid header", nameof(header));
			}

			TempUnit = fields[1].Contains('℃') ? TempUnits.C : TempUnits.F;

			// remove header line from the data
			Data.RemoveAt(0);
		}


		private enum TempUnits
		{
			C = 0,
			F = 1
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
	}
}
