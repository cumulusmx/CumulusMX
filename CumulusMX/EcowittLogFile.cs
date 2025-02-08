using System;
using System.Collections.Generic;

namespace CumulusMX
{
	internal class EcowittLogFile
	{
		private TempUnits TempUnit;
		private WindUnits WindUnit;
		private PressUnits PressUnit;
		private RainUnits RainUnit;


		private const int fieldCount = 23;
		private List<string> Data { get; set; }
		private Cumulus cumulus;

		public EcowittLogFile(List<string> data, Cumulus cumul)
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

			// 2024-09-18 14:25,22.8,55,23.2,54,13.4,23.2,1.1,1.6,259,989.6,1013.1,519.34,4,5.47,4.84,1,0.0,0.0,0.0,0.0,0.0,0.0

			var rec = new Record()
			{
				Time = DateTime.ParseExact(fields[0], "yyyy-MM-dd HH:mm", invc)
			};

			double varDbl;
			int varInt;

			if (double.TryParse(fields[1], invc, out varDbl))	rec.IndoorTemp = varDbl;
			if (int.TryParse(fields[2], out varInt))			rec.IndoorHumidity = varInt;
			if (double.TryParse(fields[3], invc, out varDbl))	rec.OutdoorTemp = varDbl;
			if (int.TryParse(fields[4], out varInt))			rec.OutdoorHumidity = varInt;
			if (double.TryParse(fields[5], invc, out varDbl))	rec.DewPoint = varDbl;
			if (double.TryParse(fields[6], invc, out varDbl))	rec.FeelsLike = varDbl;
			if (double.TryParse(fields[7], invc, out varDbl))	rec.Wind = varDbl;
			if (double.TryParse(fields[8], invc, out varDbl))	rec.Gust = varDbl;
			if (int.TryParse(fields[9], out varInt))			rec.WindDirection = varInt;
			if (double.TryParse(fields[10], invc, out varDbl))	rec.ABSPressure = varDbl;
			if (double.TryParse(fields[11], invc, out varDbl))	rec.RELPressure = varDbl;
			if (int.TryParse(fields[12], invc, out varInt))		rec.SolarRad = varInt;
			if (double.TryParse(fields[13], invc, out varDbl))	rec.UVIndex = varDbl;
			if (double.TryParse(fields[14], invc, out varDbl))	rec.ConsoleBattery = varDbl;
			if (double.TryParse(fields[15], invc, out varDbl))	rec.ExternalSupplyBattery = varDbl;
			if (double.TryParse(fields[16], invc, out varDbl))	rec.Charge = varDbl;
			if (double.TryParse(fields[17], invc, out varDbl))	rec.HourlyRain = varDbl;
			if (double.TryParse(fields[18], invc, out varDbl))	rec.EventRain = varDbl;
			if (double.TryParse(fields[19], invc, out varDbl))	rec.DailyRain = varDbl;
			if (double.TryParse(fields[20], invc, out varDbl))	rec.WeeklyRain = varDbl;
			if (double.TryParse(fields[21], invc, out varDbl))	rec.MonthlyRain = varDbl;
			if (double.TryParse(fields[22], invc, out varDbl))	rec.YearlyRain = varDbl;

			if ((int) TempUnit != cumulus.Units.Temp)
			{
				// convert all the temperatures to user units
				if (cumulus.Units.Temp == 0)
				{
					// C
					rec.IndoorTemp = MeteoLib.FtoC(rec.IndoorTemp);
					rec.OutdoorTemp = MeteoLib.FtoC(rec.OutdoorTemp);
					rec.DewPoint = MeteoLib.FtoC(rec.DewPoint);
					rec.FeelsLike = MeteoLib.FtoC(rec.FeelsLike);
				}
				else
				{
					// F
					rec.IndoorTemp = MeteoLib.CToF(rec.IndoorTemp);
					rec.OutdoorTemp = MeteoLib.CToF(rec.OutdoorTemp);
					rec.DewPoint = MeteoLib.CToF(rec.DewPoint);
					rec.FeelsLike = MeteoLib.CToF(rec.FeelsLike);
				}

				// convert wind to user units
				if ((int) WindUnit != cumulus.Units.Wind)
				{
					switch (WindUnit)
					{
						case WindUnits.ms:
							rec.Wind = ConvertUnits.WindMSToUser(rec.Wind);
							rec.Gust = ConvertUnits.WindMSToUser(rec.Gust);
							break;
						case WindUnits.mph:
							rec.Wind = ConvertUnits.WindMPHToUser(rec.Wind);
							rec.Gust = ConvertUnits.WindMPHToUser(rec.Gust);
							break;
						case WindUnits.kph:
							rec.Wind = ConvertUnits.WindKPHToUser(rec.Wind);
							rec.Gust = ConvertUnits.WindKPHToUser(rec.Gust);
							break;
						case WindUnits.knots:
							rec.Wind = ConvertUnits.WindKnotsToUser(rec.Wind);
							rec.Gust = ConvertUnits.WindKnotsToUser(rec.Gust);
							break;
					}
				}

				// convert rain to user units
				if ((int) RainUnit != cumulus.Units.Rain)
				{
					if (RainUnit == RainUnits.mm)
					{
						rec.HourlyRain = ConvertUnits.RainMMToUser(rec.HourlyRain);
						rec.EventRain = ConvertUnits.RainMMToUser(rec.EventRain);
						rec.DailyRain = ConvertUnits.RainMMToUser(rec.DailyRain);
						rec.WeeklyRain = ConvertUnits.RainMMToUser(rec.WeeklyRain);
						rec.MonthlyRain = ConvertUnits.RainMMToUser(rec.MonthlyRain);
						rec.YearlyRain = ConvertUnits.RainMMToUser(rec.YearlyRain);
					}
					else
					{
						rec.HourlyRain = ConvertUnits.RainINToUser(rec.HourlyRain);
						rec.EventRain = ConvertUnits.RainINToUser(rec.EventRain);
						rec.DailyRain = ConvertUnits.RainINToUser(rec.DailyRain);
						rec.WeeklyRain = ConvertUnits.RainINToUser(rec.WeeklyRain);
						rec.MonthlyRain = ConvertUnits.RainINToUser(rec.MonthlyRain);
						rec.YearlyRain = ConvertUnits.RainINToUser(rec.YearlyRain);
					}
				}

				// convert pressure to user units
				var cumPress = (cumulus.Units.Press == 0 || cumulus.Units.Press == 1) ? PressUnits.hPa : (PressUnits) cumulus.Units.Press;
				if (PressUnit != cumPress)
				{
					if (PressUnit == PressUnits.hPa)
					{
						rec.ABSPressure = ConvertUnits.PressMBToUser(rec.ABSPressure);
						rec.RELPressure = ConvertUnits.PressMBToUser(rec.RELPressure);
					}
					else
					{
						rec.ABSPressure = ConvertUnits.PressINHGToUser(rec.ABSPressure);
						rec.RELPressure = ConvertUnits.PressINHGToUser(rec.RELPressure);
					}
				}
			}

			return rec;
		}



		private void HeaderParser (string header)
		{
			// Time,Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃),Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),Wind(m/s),Gust(m/s),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)

			// split on commas
			var fields = header.Split(',');

			if (fields.Length < fieldCount)
			{
				// invalid header
				throw new ArgumentException("Invalid header", nameof(header));
			}

			TempUnit = fields[1].Contains("C") ? TempUnits.C : TempUnits.F;

			WindUnit =  fields[7] switch
			{
				"m/s" => WindUnits.ms,
				"mph" => WindUnits.mph,
				"km/h" => WindUnits.kph,
				"knots" => WindUnits.knots,
				_ => 0
			};

			PressUnit = fields[11] switch
			{
				"hPa" => PressUnits.hPa,
				"kPa" => PressUnits.kPa,
				"inHg" => PressUnits.inHg,
				_ => 0
			};

			RainUnit = fields[15].Contains("mm") ? RainUnits.mm : RainUnits.inch;
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
