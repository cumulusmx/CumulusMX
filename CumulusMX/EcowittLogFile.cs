using System;
using System.Collections.Generic;
using System.Globalization;

using static System.Collections.Specialized.BitVector32;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CumulusMX
{
	internal class EcowittLogFile
	{
		private TempUnits TempUnit;
		private WindUnits WindUnit;
		private PressUnits PressUnit;
		private RainUnits RainUnit;


		private const int fieldCount = 20;
		private readonly List<string> Data;
		private string[] Header;
		private readonly Cumulus cumulus;
		private DateTime lastLogTime = DateTime.MinValue;

		public EcowittLogFile(List<string> data, Cumulus cumul)
		{
			cumulus = cumul;
			Data = data;

			// parse the header
			HeaderParser(data[0]);
		}

		public SortedList<DateTime, EcowittApi.HistoricData> DataParser()
		{
			var invc = CultureInfo.InvariantCulture;
			var retList = new SortedList<DateTime, EcowittApi.HistoricData>();

			for (var index = 0; index < Data.Count; index++)
			{
				// split on commas
				var fields = Data[index].Split(',');

				//cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Processing record {fields[0]}");

				if (fields.Length < fieldCount)
				{
					cumulus.LogErrorMessage($"EcowittLogFile.DataParser: Error on record {index + 1} it contains {fields.Length} fields should be {fieldCount} or more");
					cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Record = " + Data[index]);
					continue;
				}

				// 2024-09-18 14:25,22.8,55,23.2,54,13.4,23.2,1.1,1.6,259,989.6,1013.1,519.34,4,5.47,4.84,1,0.0,0.0,0.0,0.0,0.0,0.0

				if (!DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm", invc, DateTimeStyles.AssumeLocal, out DateTime time))
				{
					cumulus.LogErrorMessage("EcowittLogFile.DataParser: Failed to parse datetime - " + fields[0]);
					continue;
				}

				if (retList.ContainsKey(time))
				{
					cumulus.LogErrorMessage("EcowittLogFile.DataParser: Duplicate timestamp found, ignoring second instance - " + fields[0]);
					continue;
				}

				//cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Preprocessing record {fields[0]}");

				var rec = new EcowittApi.HistoricData();

				if (lastLogTime == DateTime.MinValue)
				{
					rec.Interval = 1;
					lastLogTime = time;
				}
				else
				{
					rec.Interval = (time - lastLogTime).Minutes;
					lastLogTime = time;
				}

				decimal varDec;
				int varInt;

				if (decimal.TryParse(fields[1], invc, out varDec)) rec.IndoorTemp = varDec;
				if (int.TryParse(fields[2], out varInt)) rec.IndoorHum = varInt;
				if (decimal.TryParse(fields[3], invc, out varDec)) rec.Temp = varDec;
				if (int.TryParse(fields[4], out varInt)) rec.Humidity = varInt;
				if (decimal.TryParse(fields[5], invc, out varDec)) rec.DewPoint = varDec;
				if (decimal.TryParse(fields[6], invc, out varDec)) rec.FeelsLike = varDec;
				if (decimal.TryParse(fields[7], invc, out varDec)) rec.WindSpd = varDec;
				if (decimal.TryParse(fields[8], invc, out varDec)) rec.WindGust = varDec;
				if (int.TryParse(fields[9], out varInt)) rec.WindDir = varInt;
				if (decimal.TryParse(fields[10], invc, out varDec)) rec.StationPressure = varDec;
				if (decimal.TryParse(fields[11], invc, out varDec)) rec.Pressure = varDec;
				if (int.TryParse(fields[12], invc, out varInt)) rec.Solar = varInt;
				if (decimal.TryParse(fields[13], invc, out varDec)) rec.UVI = varDec;
				// These fields 14,15,16 do not appear in the GW3000 log files :(
				//if (decimal.TryParse(fields[14], invc, out varDec)) rec.ConsoleBattery = varDec;
				//if (decimal.TryParse(fields[15], invc, out varDec)) rec.ExternalSupplyBattery = varDec;
				//if (decimal.TryParse(fields[16], invc, out varDec)) rec.Charge = varDec;

				var offset = Header[14][..6] == "Hourly" ? 14 : 17; // could be "Hourly Rain(mm)" or "Console Battery (V)"

				if (cumulus.Gw1000PrimaryRainSensor == 0)
				{
					// Tipping bucket
					if (decimal.TryParse(fields[offset], invc, out varDec)) rec.RainRate = varDec; // really this is hourly rain from the file
					//if (decimal.TryParse(fields[offset + 1], invc, out varDec)) rec.EventRain = varDec;
					//if (decimal.TryParse(fields[offset + 2], invc, out varDec)) rec.DailyRain = varDec;
					//if (decimal.TryParse(fields[offset + 3], invc, out varDec)) rec.WeeklyRain = varDec;
					//if (decimal.TryParse(fields[offset + 4], invc, out varDec)) rec.MonthlyRain = varDec;
					if (decimal.TryParse(fields[offset + 5], invc, out varDec)) rec.RainYear = varDec;
				}
				else if (fields.Length >= offset + 12)
				{
					// piezo rain
					if (decimal.TryParse(fields[offset + 6], invc, out varDec)) rec.RainRate = varDec; // really this is hourly rain from the file
					//if (decimal.TryParse(fields[offset + 7], invc, out varDec)) rec.EventRain = varDec;
					//if (decimal.TryParse(fields[offset + 8], invc, out varDec)) rec.DailyRain = varDec;
					//if (decimal.TryParse(fields[offset + 9], invc, out varDec)) rec.WeeklyRain = varDec;
					//if (decimal.TryParse(fields[offset + 10], invc, out varDec)) rec.MonthlyRain = varDec;
					if (decimal.TryParse(fields[offset + 11], invc, out varDec)) rec.RainYear = varDec;
				}
				else
				{
					cumulus.LogErrorMessage($"EcowittLogFile.DataParser: Error processing piezo rain, the log entry does not contain sufficent records, it contains {fields.Length} fields should be {offset + 12} or more");
				}

				//cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Converting record {fields[0]} to MX units");


				if ((int) TempUnit != cumulus.Units.Temp)
				{
					// convert all the temperatures to user units
					if (cumulus.Units.Temp == 0)
					{
						// C
						rec.IndoorTemp = MeteoLib.FtoC(rec.IndoorTemp);
						rec.Temp = MeteoLib.FtoC(rec.Temp);
						rec.DewPoint = MeteoLib.FtoC(rec.DewPoint);
						rec.FeelsLike = MeteoLib.FtoC(rec.FeelsLike);
					}
					else
					{
						// F
						rec.IndoorTemp = MeteoLib.CToF(rec.IndoorTemp);
						rec.Temp = MeteoLib.CToF(rec.Temp);
						rec.DewPoint = MeteoLib.CToF(rec.DewPoint);
						rec.FeelsLike = MeteoLib.CToF(rec.FeelsLike);
					}

					// convert wind to user units
					if ((int) WindUnit != cumulus.Units.Wind)
					{
						switch (WindUnit)
						{
							case WindUnits.ms:
								rec.WindSpd = ConvertUnits.WindMSToUser(rec.WindSpd);
								rec.WindGust = ConvertUnits.WindMSToUser(rec.WindGust);
								break;
							case WindUnits.mph:
								rec.WindSpd = ConvertUnits.WindMPHToUser(rec.WindSpd);
								rec.WindGust = ConvertUnits.WindMPHToUser(rec.WindGust);
								break;
							case WindUnits.kph:
								rec.WindSpd = ConvertUnits.WindKPHToUser(rec.WindSpd);
								rec.WindGust = ConvertUnits.WindKPHToUser(rec.WindGust);
								break;
							case WindUnits.knots:
								rec.WindSpd = ConvertUnits.WindKnotsToUser(rec.WindSpd);
								rec.WindGust = ConvertUnits.WindKnotsToUser(rec.WindGust);
								break;
						}
					}

					// convert rain to user units
					if ((int) RainUnit != cumulus.Units.Rain)
					{
						if (RainUnit == RainUnits.mm)
						{
							rec.RainRate = ConvertUnits.RainMMToUser(rec.RainRate);
							//rec.EventRain = ConvertUnits.RainMMToUser(rec.EventRain);
							//rec.DailyRain = ConvertUnits.RainMMToUser(rec.DailyRain);
							//rec.WeeklyRain = ConvertUnits.RainMMToUser(rec.WeeklyRain);
							//rec.MonthlyRain = ConvertUnits.RainMMToUser(rec.MonthlyRain);
							rec.RainYear = ConvertUnits.RainMMToUser(rec.RainYear);
						}
						else
						{
							rec.RainRate = ConvertUnits.RainINToUser(rec.RainRate);
							//rec.EventRain = ConvertUnits.RainINToUser(rec.EventRain);
							//rec.DailyRain = ConvertUnits.RainINToUser(rec.DailyRain);
							//rec.WeeklyRain = ConvertUnits.RainINToUser(rec.WeeklyRain);
							//rec.MonthlyRain = ConvertUnits.RainINToUser(rec.MonthlyRain);
							rec.RainYear = ConvertUnits.RainINToUser(rec.RainYear);
						}
					}

					// convert pressure to user units
					var cumPress = (cumulus.Units.Press == 0 || cumulus.Units.Press == 1) ? PressUnits.hPa : (PressUnits) cumulus.Units.Press;
					if (PressUnit != cumPress)
					{
						if (PressUnit == PressUnits.hPa)
						{
							rec.StationPressure = ConvertUnits.PressMBToUser(rec.StationPressure);
							rec.Pressure = ConvertUnits.PressMBToUser(rec.Pressure);
						}
						else
						{
							rec.StationPressure = ConvertUnits.PressINHGToUser(rec.StationPressure);
							rec.Pressure = ConvertUnits.PressINHGToUser(rec.Pressure);
						}
					}
				}

				if (!retList.TryAdd(time, rec))
				{
					cumulus.LogErrorMessage("EcowittLogFile.DataParser: Error adding record to list - " + fields[0]);
				}

				//cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Record {fields[0]} added to history list");
			}

			return retList;
		}



		private void HeaderParser (string header)
		{
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(mph),Gust(mph),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(m/s),Gust(m/s),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(m/s),Gust(m/s),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)

			cumulus.LogDataMessage($"EcowittLogFile.HeaderParser: File header: {header}");

			// split on commas
			var fields = header.Split(',');

			if (fields.Length < fieldCount)
			{
				// invalid header
				throw new ArgumentException("Invalid header", nameof(header));
			}

			TempUnit = fields[1].Contains('C') || fields[1].Contains('℃') ? TempUnits.C : TempUnits.F;

			if (fields[7].Contains("m/s")) WindUnit = WindUnits.ms;
			else if (fields[7].Contains("mph")) WindUnit = WindUnits.mph;
			else if (fields[7].Contains("km/h")) WindUnit = WindUnits.kph;
			else if (fields[7].Contains("knots")) WindUnit = WindUnits.knots;
			else WindUnit = 0;


			if (fields[11].Contains("hPa")) PressUnit = PressUnits.hPa;
			else if (fields[11].Contains("inHg")) PressUnit = PressUnits.inHg;
			else if (fields[11].Contains("kPa")) PressUnit = PressUnits.kPa;
			else PressUnit = 0;

			RainUnit = fields[15].Contains("mm") ? RainUnits.mm : RainUnits.inch;

			// Save the header
			Header = fields;

			// remove header line from the data
			Data.RemoveAt(0);
		}


		private enum TempUnits
		{
			C = 0,
			F = 1
		}

		private enum WindUnits
		{
			ms = 0,
			mph = 1,
			kph = 2,
			knots = 3
		}

		private enum PressUnits
		{
			mb = 0,
			hPa = 1,
			inHg = 2,
			kPa = 3,
			mmHg = 4
		}

		private enum RainUnits
		{
			mm = 0,
			inch = 1
		}

		public class Record
		{
			public DateTime Time { get; set; }
			public double? IndoorTemp { get; set; }
			public int? IndoorHumidity { get; set; }
			public double? OutdoorTemp { get; set; }
			public int? OutdoorHumidity { get; set; }
			public double? DewPoint { get; set; }
			public double? FeelsLike { get; set; }
			public double? Wind { get; set; }
			public double? Gust { get; set; }
			public double? WindDirection { get; set; }
			public double? ABSPressure { get; set; }
			public double? RELPressure { get; set; }
			public int? SolarRad { get; set; }
			public double? UVIndex { get; set; }
			public double? ConsoleBattery { get; set; }
			public double? ExternalSupplyBattery { get; set; }
			public double? Charge { get; set; }
			public double? HourlyRain { get; set; }
			public double? EventRain { get; set; }
			public double? DailyRain { get; set; }
			public double? WeeklyRain { get; set; }
			public double? MonthlyRain { get; set; }
			public double? YearlyRain { get; set; }
		}
	}
}
