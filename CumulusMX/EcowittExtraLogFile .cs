using System;
using System.Collections.Generic;

namespace CumulusMX
{
	internal class EcowittExtraLogFile
	{
		private TempUnits TempUnit;

		private const int fieldCount = 83;
		private List<string> Data { get; set; }
		private readonly Cumulus cumulus;

		public EcowittExtraLogFile(List<string> data, Cumulus cumul)
		{
			cumulus = cumul;
			Data = data;

			// parse the header
			HeaderParser(data[0]);
		}

		public Record DataParser(int index)
		{
			var invc = System.Globalization.CultureInfo.InvariantCulture;

			if (index >= Data.Count)
			{
				cumulus.LogErrorMessage("EcowittLogFile: Index out of range - " + index);
			}
			// split on commas
			var fields = Data[index].Split(',');

			if (fields.Length < fieldCount)
			{
				cumulus.LogErrorMessage($"EcowittLogFile: Error on line {index + 1} it contains {fields.Length} fields should be {fieldCount}");
				return null;
			}

			// 2025-01-10 12:34,1.8,0.8,1.8,93,3.3,1.5,3.3,88,1.5,-0.1,1.5,89,1.6,-0.3,1.6,87,-19.3,--,--,--,3.9,2.7,3.9,92,7.0,-3.0,7.0,49,--,--,--,--,77,--,--,--,--,--,--,--,0,--,15.3,60,775,6.4,6.7,--,--,60,45,56,72,50,74,--,--,--,--,--,--,--,--,--,--,--,Normal,--,--,12.0,9.0,--,--,2.5,2.5,2.0,--,--,--,--,--,--,--,--,--


			var rec = new Record()
			{
				Time = DateTime.ParseExact(fields[0], "yyyy-MM-dd HH:mm", invc)
			};

			double varDbl;
			int varInt;

			var field = 1;

			// Extra Temp/Hum sensors
			for (var i = 0; i < 8; i++)
			{
				if (double.TryParse(fields[field++], invc, out varDbl)) rec.ExtraTemp[i] = varDbl;
				if (double.TryParse(fields[field++], invc, out varDbl)) rec.ExtraDewPoint[i] = varDbl;
				if (double.TryParse(fields[field++], invc, out varDbl)) rec.ExtraHeatIndex[i] = varDbl;
				if (int.TryParse(fields[field++], out varInt)) rec.ExtraHumidity[i] = varInt;
			}

			// Leaf Moisture Sensors
			for (var i = 0; i < 8; i++)
			{
				if (int.TryParse(fields[field++], out varInt)) rec.LeafMoist[i] = varInt;
			}

			// Lightning
			if (int.TryParse(fields[field++], out varInt)) rec.LightningCount = varInt;
			if (int.TryParse(fields[field++], out varInt)) rec.LightningDist = varInt;

			// AQ Indoor
			if (double.TryParse(fields[field++], invc, out varDbl)) rec.AqiInTemp = varDbl;
			if (int.TryParse(fields[field++], out varInt)) rec.AqiInHum = varInt;
			if (int.TryParse(fields[field++], out varInt)) rec.AqiInCO2 = varInt;
			if (double.TryParse(fields[field++], invc, out varDbl)) rec.AqiInPm2p5 = varDbl;
			if (double.TryParse(fields[field++], invc, out varDbl)) rec.AqiInPm10 = varDbl;
			if (double.TryParse(fields[field++], invc, out varDbl)) rec.AqiInPm1 = varDbl;
			if (double.TryParse(fields[field++], invc, out varDbl)) rec.AqiInPm4 = varDbl;

			// Soil Moisture
			for (int i = 0; i < 16; i++)
			{
				if (int.TryParse(fields[field++], out varInt)) rec.SoilMoist[i] = varInt;
			}

			// Water
			for (int i = 0; i < 4; i++)
			{
				if (fields[field++] != "--") rec.Water[i] = fields[field++];
			}

			// AQI
			for (int i = 0; i < 4; i++)
			{
				if (double.TryParse(fields[field++], invc, out varDbl)) rec.AqiPm2p5[i] = varDbl;
			}

			// User Temps
			for (var i = 0; i < 8; i++)
			{
				if (double.TryParse(fields[field++], invc, out varDbl)) rec.UserTemp[i] = varDbl;
			}

			// LDS Air
			for (int i = 0; i < 4; i++)
			{
				if (double.TryParse(fields[field++], invc, out varDbl)) rec.LdsAir[i] = varDbl;
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
						rec.ExtraDewPoint[i] = MeteoLib.FtoC(rec.ExtraDewPoint[i]);
						rec.ExtraHeatIndex[i] = MeteoLib.FtoC(rec.ExtraHeatIndex[i]);
						rec.UserTemp[i] = MeteoLib.FtoC(rec.UserTemp[i]);
					}

					rec.AqiInTemp = MeteoLib.FtoC(rec.AqiInTemp);
				}
				else
				{
					// F
					for (var i = 0; i < 8; i++)
					{
						rec.ExtraTemp[i] = MeteoLib.CToF(rec.ExtraTemp[i]);
						rec.ExtraDewPoint[i] = MeteoLib.CToF(rec.ExtraDewPoint[i]);
						rec.ExtraHeatIndex[i] = MeteoLib.CToF(rec.ExtraHeatIndex[i]);
						rec.UserTemp[i] = MeteoLib.CToF(rec.UserTemp[i]);
					}

					rec.AqiInTemp = MeteoLib.CToF(rec.AqiInTemp);
				}
			}

			return rec;
		}



		private void HeaderParser (string header)
		{
			// Time,CH1 Temperature(℃),CH1 Dew point(℃),CH1 HeatIndex(℃),CH1 Humidity(%),CH2 Temperature(℃),CH2 Dew point(℃),CH2 HeatIndex(℃),CH2 Humidity(%),CH3 Temperature(℃),CH3 Dew point(℃),CH3 HeatIndex(℃),CH3 Humidity(%),CH4 Temperature(℃),CH4 Dew point(℃),CH4 HeatIndex(℃),CH4 Humidity(%),CH5 Temperature(℃),CH5 Dew point(℃),CH5 HeatIndex(℃),CH5 Humidity(%),CH6 Temperature(℃),CH6 Dew point(℃),CH6 HeatIndex(℃),CH6 Humidity(%),CH7 Temperature(℃),CH7 Dew point(℃),CH7 HeatIndex(℃),CH7 Humidity(%),CH8 Temperature(℃),CH8 Dew point(℃),CH8 HeatIndex(℃),CH8 Humidity(%),WH35 CH1hum(%),WH35 CH2hum(%),WH35 CH3hum(%),WH35 CH4hum(%),WH35 CH5hum(%),WH35 CH6hum(%),WH35 CH7hum(%),WH35 CH8hum(%),Thunder count,Thunder distance(km),AQIN Temperature(℃),AQIN Humidity(%),AQIN CO2(ppm),AQIN PM2.5(ug/m3),AQIN PM10(ug/m3),AQIN PM1.0(ug/m3),AQIN PM4.0(ug/m3),SoilMoisture CH1(%),SoilMoisture CH2(%),SoilMoisture CH3(%),SoilMoisture CH4(%),SoilMoisture CH5(%),SoilMoisture CH6(%),SoilMoisture CH7(%),SoilMoisture CH8(%),SoilMoisture CH9(%),SoilMoisture CH10(%),SoilMoisture CH11(%),SoilMoisture CH12(%),SoilMoisture CH13(%),SoilMoisture CH14(%),SoilMoisture CH15(%),SoilMoisture CH16(%),Water CH1,Water CH2,Water CH3,Water CH4,Pm2.5 CH1(ug/m3),Pm2.5 CH2(ug/m3),Pm2.5 CH3(ug/m3),Pm2.5 CH4(ug/m3),WN34 CH1(℃),WN34 CH2(℃),WN34 CH3(℃),WN34 CH4(℃),WN34 CH5(℃),WN34 CH6(℃),WN34 CH7(℃),WN34 CH8(℃),LDS_Air CH1(mm),LDS_Air CH2(mm),LDS_Air CH3(mm),LDS_Air CH4(mm),


			// split on commas
			var fields = header.Split(',');

			if (fields.Length < fieldCount)
			{
				// invalid header
				throw new ArgumentException("Invalid header", nameof(header));
			}

			TempUnit = fields[1].Contains("C") ? TempUnits.C : TempUnits.F;
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
