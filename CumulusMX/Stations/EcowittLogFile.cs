using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace CumulusMX.Stations
{
	internal partial class EcowittLogFile
	{
		private TempUnits TempUnit;
		private WindUnits WindUnit;
		private PressUnits PressUnit;
		private RainUnits RainUnit;
		private SolarUnits SolarUnit;


		private const int fieldCount = 20;
		private readonly List<string> Data;
		//private string[] Header;
		private readonly Cumulus cumulus;
		private DateTime lastLogTime = DateTime.MinValue;
		private readonly Dictionary<string, int> FieldIndex = [];
		private readonly int interval;

		public EcowittLogFile(List<string> data, Cumulus cumul, int interval)
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
				cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Preprocess record # {index + 1} of {Data.Count}");

				try
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
					// 2025-05-20 17:46,1747734370,22.8,55,23.2,54,13.4,23.2,1.1,1.6,259,989.6,1013.1,519.34,4,5.47,4.84,1,0.0,0.0,0.0,0.0,0.0,0.0
					// 2025-06-12 13:34,1749731693,20.1,60,22.4,57,13.5,22.4,1.16,2.01,5.14,224,1000.2,1008.5,214.05,2,0.0,0.0,0.0,0.0,5.7,38.8,261.9,0.0,0.0,0.0,0.0,0.0,0.0,0.0
					// 2025-07-29 14:23,1753795382,21.0,64,19.8,79,16.1,19.8,0.48,0.00,2.46,179,298,1009.4,1017.8,245.86,2,0.0,0.0,0.0,0.0,2.5,2.5,54.0,341.7,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0

					DateTime time;

					if (useTimestamp && long.TryParse(fields[1], invc, out long unix))
					{
						time = Utils.RoundDownUnixTimestamp(unix, interval).FromUnixTime();
					}
					else
					{
						if (DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm", invc, System.Globalization.DateTimeStyles.AssumeLocal, out time))
						{
							time = time.RoundTimeDownToInterval(TimeSpan.FromMinutes(interval));
						}
						else
						{
							cumulus.LogErrorMessage("EcowittLogFile.DataParser: Failed to parse datetime - " + fields[0]);
							continue;
						}
					}

					if (retList.ContainsKey(time))
					{
						cumulus.LogErrorMessage("EcowittLogFile.DataParser: Duplicate timestamp found, ignoring second instance - " + fields[0]);
						continue;
					}

					cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Preprocessing record {fields[0]} - {time:yyyy-MM-dd HH:mm}");

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

					cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Record interval = {rec.Interval} minutes");

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
					//if (FieldIndex.TryGetValue("windDir_10min_avg", out idx) && int.TryParse(fields[idx], out varInt)) rec.WindDirAvg = varInt;
					if (FieldIndex.TryGetValue("wind direction", out idx) && int.TryParse(fields[idx], out varInt)) rec.WindDir = varInt;
					if (FieldIndex.TryGetValue("abs pressure", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.StationPressure = varDec;
					if (FieldIndex.TryGetValue("rel pressure", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Pressure = varDec;
					if (FieldIndex.TryGetValue("solar rad", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Solar = (double) varDec;
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
						//if (FieldIndex.TryGetValue("24h rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Rain24h = varDec;
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
						//if (FieldIndex.TryGetValue("piezo 24h rain", out idx) && decimal.TryParse(fields[idx], invc, out varDec)) rec.Rain24h = varDec;
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
							//rec.Rain24h = ConvertUnits.RainMMToUser(rec.Rain24h);
							//rec.EventRain = ConvertUnits.RainMMToUser(rec.EventRain);
							//rec.DailyRain = ConvertUnits.RainMMToUser(rec.DailyRain);
							//rec.WeeklyRain = ConvertUnits.RainMMToUser(rec.WeeklyRain);
							//rec.MonthlyRain = ConvertUnits.RainMMToUser(rec.MonthlyRain);
							rec.RainYear = ConvertUnits.RainMMToUser(rec.RainYear);
						}
						else
						{
							rec.RainRate = ConvertUnits.RainINToUser(rec.RainRate);
							//rec.Rain24h = ConvertUnits.RainMMToUser(rec.Rain24h);
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

					// convert solar to W/m2
					if (rec.Solar.HasValue && SolarUnit != SolarUnits.wm2)
					{
						if (SolarUnit == SolarUnits.klux)
						{
							rec.Solar = (int) (rec.Solar.Value * 1000.0 * cumulus.SolarOptions.LuxToWM2);
						}
						else if (SolarUnit == SolarUnits.kfc)
						{
							rec.Solar = (int) (rec.Solar.Value * 1000 * 0.015759751708199);
						}
					}

					if (!retList.TryAdd(time, rec))
					{
						cumulus.LogErrorMessage($"EcowittLogFile.DataParser: Error adding record to list {index + 1} - {fields[0]}");
					}
					else
					{
						cumulus.LogDebugMessage($"EcowittLogFile.DataParser: Record {fields[0]} - {time:yyyy-MM-dd HH:mm} added to history list");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogErrorMessage("EcowittLogFile.DataParser: Error processing record " + (index + 1) + " - " + ex.Message);
					cumulus.LogDebugMessage("EcowittLogFile.DataParser: Record = " + Data[index]);
				}
			}

			return retList;
		}


		private void HeaderParser (string header)
		{
			// Time,          Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃), Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),                          Wind(mph),Gust(mph),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,                                                                                     Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// Time,          Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃), Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),                          Wind(m/s),Gust(m/s),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,                              Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm)
			// Time,          Indoor Temperature(℃),Indoor Humidity(%),Outdoor Temperature(℃), Outdoor Humidity(%),Dew Point(℃),Feels Like(℃),                          Wind(m/s),Gust(m/s),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,                              Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),                                     Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)
			// Time,          Indoor Temperature(°C),Indoor Humidity(%),Outdoor Temperature(°C),Outdoor Humidity(%),Dew Point(°C),Feels Like(°C),                 VPD(kPa),Wind(m/s),Gust(m/s),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Rain Rate(mm/Hr),             Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Rate(mm/Hr),                   Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)
			// Time,          Indoor Temperature(°C),Indoor Humidity(%),Outdoor Temperature(°C),Outdoor Humidity(%),Dew Point(°C),Feels Like(°C),                 VPD(kPa),Wind(m/s),Gust(m/s),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(w/m2),UV-Index,                                                       Rain Rate(mm/Hr),             Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Rate(mm/Hr),                   Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)
			// Time,          Indoor Temperature(°C),Indoor Humidity(%),Outdoor Temperature(°C),Outdoor Humidity(%),Dew Point(°C),Feels Like(°C),                 VPD(kPa),Wind(mph),Gust(mph),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(W/m2),UV-Index,                                                       Rain Rate(mm/Hr),             Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Rate(mm/Hr),                   Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)
			// Time,Timestamp,Indoor Temperature(°C),Indoor Humidity(%),Outdoor Temperature(°C),Outdoor Humidity(%),Dew Point(°C),Feels Like(°C),                 VPD(kPa),Wind(mph),Gust(mph),                       Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(W/m2),UV-Index,                                                       Rain Rate(mm/Hr),             Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Rate(mm/Hr),                   Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)
			// Time,Timestamp,Indoor Temperature(°C),Indoor Humidity(%),Outdoor Temperature(°C),Outdoor Humidity(%),Dew Point(°C),Feels Like(°C),BGT(°C),WBGT(°C),VPD(kPa),Wind(m/s),Gust(m/s),windDir_10min_avg(deg),Wind Direction(deg),ABS Pressure(hPa),REL Pressure(hPa),Solar Rad(W/m2),UV-Index,Console Battery (V),External Supply Battery (V),Charge,Rain Rate(mm/Hr),24h Rain(mm),Hourly Rain(mm),Event Rain(mm),Daily Rain(mm),Weekly Rain(mm),Monthly Rain(mm),Yearly Rain(mm),Piezo Rate(mm/Hr),Piezo 24h Rain(mm),Piezo Hourly Rain(mm),Piezo Event Rain(mm),Piezo Daily Rain(mm),Piezo Weekly Rain(mm),Piezo Monthly Rain(mm),Piezo Yearly Rain(mm)

			cumulus.LogDataMessage($"EcowittLogFile.HeaderParser: File header: {header}");

			// split on commas
			var fields = header.Split(',');

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
				var fld = fields[idx].ToLower();

				if (fld.EndsWith("m/s)")) WindUnit = WindUnits.ms;
				else if (fld.EndsWith("mph)")) WindUnit = WindUnits.mph;
				else if (fld.EndsWith("km/h)")) WindUnit = WindUnits.kph;
				else if (fld.EndsWith("knots)")) WindUnit = WindUnits.knots;
				else WindUnit = (WindUnits) cumulus.Units.Wind;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Unable to determine wind units, defaulting to Cumulus units");
				WindUnit = (WindUnits) cumulus.Units.Wind;
			}

			if (FieldIndex.TryGetValue("abs pressure", out idx))
			{
				var fld = fields[idx].ToLower();

				if (fld.EndsWith("hpa)")) PressUnit = PressUnits.hPa;
				else if (fld.EndsWith("inhg)")) PressUnit = PressUnits.inHg;
				else if (fld.EndsWith("kpa)")) PressUnit = PressUnits.kPa;
				else if (fld.EndsWith("mmhg)")) PressUnit = PressUnits.mmHg;
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

			if (FieldIndex.TryGetValue("solar rad", out idx))
			{
				var fld = fields[idx].ToLower();
				if (fld.EndsWith("w/m2)")) SolarUnit = SolarUnits.wm2;
				else if (fld.EndsWith("klux)")) SolarUnit = SolarUnits.klux;
				else if (fld.EndsWith("kfc)")) SolarUnit = SolarUnits.kfc;
				else SolarUnit = SolarUnits.wm2;
			}
			else
			{
				cumulus.LogErrorMessage("EcowittLogFile.HeaderParser: Unable to determine solar units, defaulting to Cumulus units");
				SolarUnit = SolarUnits.wm2;
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

		private enum SolarUnits
		{
			wm2 = 0,
			klux = 1,
			kfc = 2
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
