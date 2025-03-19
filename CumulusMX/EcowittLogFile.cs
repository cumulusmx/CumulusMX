using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using Org.BouncyCastle.Ocsp;

namespace CumulusMX
{
	internal partial class EcowittLogFile
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
		private readonly Dictionary<string, int> FieldIndex = [];


		public EcowittLogFile(List<string> data, Cumulus cumul)
		{
			cumulus = cumul;
			Data = data;

			// parse the header
			HeaderParser(data[0]);
		}

#pragma warning disable S125 // Sections of code should not be commented out

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

				cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Preprocessing record {fields[0]}");

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
				int idx;

				if (FieldIndex.TryGetValue("indoor temperature", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.IndoorTemp = varDec;
				if (FieldIndex.TryGetValue("indoor humidity", out idx) && int.TryParse(fields[idx], out varInt)) rec.IndoorHum = varInt;
				if (FieldIndex.TryGetValue("outdoor temperature", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Temp = varDec;
				if (FieldIndex.TryGetValue("outdoor humidity", out idx) && int.TryParse(fields[idx], out varInt)) rec.Humidity = varInt;
				if (FieldIndex.TryGetValue("dew point", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.DewPoint = varDec;
				if (FieldIndex.TryGetValue("feels like", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.FeelsLike = varDec;
				if (FieldIndex.TryGetValue("wind", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.WindSpd = varDec;
				if (FieldIndex.TryGetValue("gust", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.WindGust = varDec;
				if (FieldIndex.TryGetValue("wind direction", out idx) && int.TryParse(fields[idx], out varInt)) rec.WindDir = varInt;
				if (FieldIndex.TryGetValue("abs pressure", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.StationPressure = varDec;
				if (FieldIndex.TryGetValue("rel pressure", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Pressure = varDec;
				if (FieldIndex.TryGetValue("solar rad", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Solar = (int?) varDec;
				if (FieldIndex.TryGetValue("uv-index", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.UVI = varDec;
				// These fields 14,15,16 do not appear in the GW3000 log files :(
				//if (FieldIndex.TryGetValue("console battery", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.ConsoleBattery = varDec;
				//if (FieldIndex.TryGetValue("external supply battery", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.ExternalSupplyBattery = varDec;
				//if (FieldIndex.TryGetValue("charge", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Charge = varDec;


				if (cumulus.Gw1000PrimaryRainSensor == 0)
				{
					// Tipping bucket
					if (FieldIndex.TryGetValue("rain rate", out idx) && decimal.TryParse(fields[idx], invc, out varDec))
					{
						rec.RainRate = varDec;
					}
					else if (FieldIndex.TryGetValue("hourly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec))
					{
						rec.RainRate = varDec; // really this is hourly rain from the file
					}
					//if (FieldIndex.TryGetValue("event rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.EventRain = varDec;
					//if (FieldIndex.TryGetValue("daily rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.DailyRain = varDec;
					//if (FieldIndex.TryGetValue("weekly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.WeeklyRain = varDec;
					//if (FieldIndex.TryGetValue("monthly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.MonthlyRain = varDec;
					if (FieldIndex.TryGetValue("yearly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.RainYear = varDec;
				}
				else if (fields.Length >= 25)
				{
					// piezo rain
					if (FieldIndex.TryGetValue("piezo rate", out idx) && decimal.TryParse(fields[idx], invc, out varDec))
					{
						rec.RainRate = varDec;
					}
					else if (FieldIndex.TryGetValue("piezo hourly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec))
					{
						rec.RainRate = varDec; // really this is hourly rain from the file
					}
					//if (FieldIndex.TryGetValue("piezo event rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.EventRain = varDec;
					//if (FieldIndex.TryGetValue("piezo daily rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.DailyRain = varDec;
					//if (FieldIndex.TryGetValue("piezo weekly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.WeeklyRain = varDec;
					//if ((FieldIndex.TryGetValue("piezo monthly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.MonthlyRain = varDec;
					if (FieldIndex.TryGetValue("piezo yearly rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.RainYear = varDec;
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

				if (!retList.TryAdd(time, rec))
				{
					cumulus.LogErrorMessage("EcowittLogFile.DataParser: Error adding record to list - " + fields[0]);
				}

				//cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Record {fields[0]} added to history list");
			}

			return retList;
		}

#pragma warning restore S125 // Sections of code should not be commented out


		private void HeaderParser (string header)
		{
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(mph),Gust(mph),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(m/s),Gust(m/s),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(m/s),Gust(m/s),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)

			cumulus.LogDataMessage($"EcowittLogFile.HeaderParser: File header: {header}");

			// split on commas
			var fields = header.Split(',');

			// Save the header
			Header = fields;

			// remove header line from the data
			Data.RemoveAt(0);

			if (fields.Length < fieldCount)
			{
				// invalid header
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Invalid header in file = " + header);
				return;
			}

			// create a fields index
			var unitRegex = UnitRegEx();

			for (var i = 0; i < fields.Length; i++)
			{
				string cleanedHeader = unitRegex.Replace(fields[i], "").Trim();
				FieldIndex[cleanedHeader.ToLower()] = i; // Ecowitt tend to mess around with the case!
			}

			if (FieldIndex.TryGetValue("indoor temperature", out int idx))
			{
				TempUnit = fields[idx].ToLower().EndsWith("c)") || fields[idx].EndsWith("℃)") ? TempUnits.C : TempUnits.F;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Unable to determine temperature units, defaulting to Cumulus units");
				TempUnit = (TempUnits) cumulus.Units.Temp;
			}

			if (FieldIndex.TryGetValue("wind", out idx))
			{

				if (fields[idx].ToLower().EndsWith("m/s)")) WindUnit = WindUnits.ms;
				else if (fields[idx].ToLower().EndsWith("mph)")) WindUnit = WindUnits.mph;
				else if (fields[idx].ToLower().EndsWith("km/h)")) WindUnit = WindUnits.kph;
				else if (fields[idx].ToLower().EndsWith("knots)")) WindUnit = WindUnits.knots;
				else WindUnit = (WindUnits) cumulus.Units.Wind;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Unable to determine wind units, defaulting to Cumulus units");
				WindUnit = (WindUnits) cumulus.Units.Wind;
			}

			if (FieldIndex.TryGetValue("abs pressure", out idx))
			{
				if (fields[idx].ToLower().EndsWith("hpa)")) PressUnit = PressUnits.hPa;
				else if (fields[idx].ToLower().EndsWith("inhg)")) PressUnit = PressUnits.inHg;
				else if (fields[idx].ToLower().EndsWith("kpa)")) PressUnit = PressUnits.kPa;
				else if (fields[idx].ToLower().EndsWith("mmhg)")) PressUnit = PressUnits.mmHg;
				else PressUnit = cumulus.Units.Press switch
				{
					0 or 1 => PressUnits.hPa,
					2 => PressUnits.inHg,
					3 => PressUnits.kPa,
					_ => PressUnits.hPa
				};
			}
			else
			{
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Unable to determine pressure units, defaulting to Cumulus units");
				PressUnit = cumulus.Units.Press switch
				{
					0 or 1 => PressUnits.hPa,
					2 => PressUnits.inHg,
					3 => PressUnits.kPa,
					_ => PressUnits.hPa
				};
			}

			if (FieldIndex.TryGetValue("hourly rain", out idx))
			{
				RainUnit = fields[idx].ToLower().EndsWith("mm)") ? RainUnits.mm : RainUnits.inch;
			}
			else if (FieldIndex.TryGetValue("piezo hourly rain", out idx))
			{
				RainUnit = fields[idx].ToLower().EndsWith("mm)") ? RainUnits.mm : RainUnits.inch;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Unable to determine rain units, defaulting to Cumulus units");
				RainUnit = cumulus.Units.Rain switch
				{
					0 => RainUnits.mm,
					1 => RainUnits.inch,
					_ => RainUnits.mm
				};
			}
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

		[GeneratedRegex(@"\(.*?\)")]
		private partial Regex UnitRegEx();
	}
}
