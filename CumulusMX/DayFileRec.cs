using System;
using System.Collections.Generic;
using System.Globalization;

using SQLite;

using static CumulusMX.WeatherStation;

namespace CumulusMX
{
	internal class DayFileRec
	{
		[PrimaryKey]
		public DateTime Date { get; set; }
		public double HighGust { get; set; }
		public int HighGustBearing { get; set; }
		public DateTime HighGustTime { get; set; }
		public double LowTemp { get; set; }
		public DateTime LowTempTime { get; set; }
		public double HighTemp { get; set; }
		public DateTime HighTempTime { get; set; }
		public double LowPress { get; set; }
		public DateTime LowPressTime { get; set; }
		public double HighPress { get; set; }
		public DateTime HighPressTime { get; set; }
		public double HighRainRate { get; set; }
		public DateTime HighRainRateTime { get; set; }
		public double TotalRain { get; set; }
		public double AvgTemp { get; set; }
		public double WindRun { get; set; }
		public double HighAvgWind { get; set; }
		public DateTime HighAvgWindTime { get; set; }
		public int LowHumidity { get; set; }
		public DateTime LowHumidityTime { get; set; }
		public int HighHumidity { get; set; }
		public DateTime HighHumidityTime { get; set; }
		public double ET { get; set; }
		public double SunShineHours { get; set; }
		public double HighHeatIndex { get; set; }
		public DateTime HighHeatIndexTime { get; set; }
		public double HighAppTemp { get; set; }
		public DateTime HighAppTempTime { get; set; }
		public double LowAppTemp { get; set; }
		public DateTime LowAppTempTime { get; set; }
		public double HighHourlyRain { get; set; }
		public DateTime HighHourlyRainTime { get; set; }
		public double LowWindChill { get; set; }
		public DateTime LowWindChillTime { get; set; }
		public double HighDewPoint { get; set; }
		public DateTime HighDewPointTime { get; set; }
		public double LowDewPoint { get; set; }
		public DateTime LowDewPointTime { get; set; }
		public int DominantWindBearing { get; set; }
		public double HeatingDegreeDays { get; set; }
		public double CoolingDegreeDays { get; set; }
		public int HighSolar { get; set; }
		public DateTime HighSolarTime { get; set; }
		public double HighUv { get; set; }
		public DateTime HighUvTime { get; set; }
		public double HighFeelsLike { get; set; }
		public DateTime HighFeelsLikeTime { get; set; }
		public double LowFeelsLike { get; set; }
		public DateTime LowFeelsLikeTime { get; set; }
		public double HighHumidex { get; set; }
		public DateTime HighHumidexTime { get; set; }
		public double ChillHours { get; set; }
		public double HighRain24h { get; set; }
		public DateTime HighRain24hTime { get; set; }


		public DayFileRec()
		{
		}

		public DayFileRec(string csv)
		{ 
			ParseCsvRec(csv);
		}


		// errors are caught by the caller
		public void ParseCsvRec(string data)
		{
			// 0   Date in the form dd/mm/yy (the slash may be replaced by a dash in some cases)
			// 1  Highest wind gust
			// 2  Bearing of highest wind gust
			// 3  Time of highest wind gust
			// 4  Minimum temperature
			// 5  Time of minimum temperature
			// 6  Maximum temperature
			// 7  Time of maximum temperature
			// 8  Minimum sea level pressure
			// 9  Time of minimum pressure
			// 10  Maximum sea level pressure
			// 11  Time of maximum pressure
			// 12  Maximum rainfall rate
			// 13  Time of maximum rainfall rate
			// 14  Total rainfall for the day
			// 15  Average temperature for the day
			// 16  Total wind run
			// 17  Highest average wind speed
			// 18  Time of highest average wind speed
			// 19  Lowest humidity
			// 20  Time of lowest humidity
			// 21  Highest humidity
			// 22  Time of highest humidity
			// 23  Total evapotranspiration
			// 24  Total hours of sunshine
			// 25  High heat index
			// 26  Time of high heat index
			// 27  High apparent temperature
			// 28  Time of high apparent temperature
			// 29  Low apparent temperature
			// 30  Time of low apparent temperature
			// 31  High hourly rain
			// 32  Time of high hourly rain
			// 33  Low wind chill
			// 34  Time of low wind chill
			// 35  High dew point
			// 36  Time of high dew point
			// 37  Low dew point
			// 38  Time of low dew point
			// 39  Dominant wind bearing
			// 40  Heating degree days
			// 41  Cooling degree days
			// 42  High solar radiation
			// 43  Time of high solar radiation
			// 44  High UV Index
			// 45  Time of high UV Index
			// 46  High Feels like
			// 47  Time of high feels like
			// 48  Low feels like
			// 49  Time of low feels like
			// 50  High Humidex
			// 51  Time of high Humidex
			// 52  Chill hours
			// 53  Max Rain 24 hours
			// 54  Max Rain 24 hours Time

			var inv = CultureInfo.InvariantCulture;
			var st = new List<string>(data.Split(','));
			double varDbl;
			int idx = 0;

			try
			{
				Date = Utils.ddmmyyStrToDate(st[idx++]);
				HighGust = Convert.ToDouble(st[idx++], inv);
				HighGustBearing = Convert.ToInt32(st[idx++]);
				HighGustTime = GetDateTime(Date, st[idx++], Program.cumulus.RolloverHour);
				LowTemp = Convert.ToDouble(st[idx++], inv);
				LowTempTime = GetDateTime(Date, st[idx++], Program.cumulus.RolloverHour);
				HighTemp = Convert.ToDouble(st[idx++], inv);
				HighTempTime = GetDateTime(Date, st[idx++], Program.cumulus.RolloverHour);
				LowPress = Convert.ToDouble(st[idx++], inv);
				LowPressTime = GetDateTime(Date, st[idx++], Program.cumulus.RolloverHour);
				HighPress = Convert.ToDouble(st[idx++], inv);
				HighPressTime = GetDateTime(Date, st[idx++], Program.cumulus.RolloverHour);
				HighRainRate = Convert.ToDouble(st[idx++], inv);
				HighRainRateTime = GetDateTime(Date, st[idx++], Program.cumulus.RolloverHour);
				TotalRain = Convert.ToDouble(st[idx++], inv);
				AvgTemp = Convert.ToDouble(st[idx++], inv);

				if (st.Count > idx++ && double.TryParse(st[16], inv, out varDbl))
					WindRun = varDbl;

				if (st.Count > idx++ && double.TryParse(st[17], inv, out varDbl))
					HighAvgWind = varDbl;

				if (st.Count > idx++ && st[18].Length == 5)
					HighAvgWindTime = GetDateTime(Date, st[18], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[19], inv, out varDbl))
					LowHumidity = Convert.ToInt32(varDbl);
				else
					LowHumidity = (int) Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[20].Length == 5)
					LowHumidityTime = GetDateTime(Date, st[20], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[21], inv, out varDbl))
					HighHumidity = Convert.ToInt32(varDbl);
				else
					HighHumidity = (int) Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[22].Length == 5)
					HighHumidityTime = GetDateTime(Date, st[22], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[23], inv, out varDbl))
					ET = varDbl;
				else
					ET = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[24], inv, out varDbl))
					SunShineHours = varDbl;

				if (st.Count > idx++ && double.TryParse(st[25], inv, out varDbl))
					HighHeatIndex = varDbl;
				else
					HighHeatIndex = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[26].Length == 5)
					HighHeatIndexTime = GetDateTime(Date, st[26], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[27], inv, out varDbl))
					HighAppTemp = varDbl;
				else
					HighAppTemp = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[28].Length == 5)
					HighAppTempTime = GetDateTime(Date, st[28], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[29], inv, out varDbl))
					LowAppTemp = varDbl;
				else
					LowAppTemp = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[30].Length == 5)
					LowAppTempTime = GetDateTime(Date, st[30], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[31], inv, out varDbl))
					HighHourlyRain = varDbl;
				else
					HighHourlyRain = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[32].Length == 5)
					HighHourlyRainTime = GetDateTime(Date, st[32], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[33], inv, out varDbl))
					LowWindChill = varDbl;
				else
					LowWindChill = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[34].Length == 5)
					LowWindChillTime = GetDateTime(Date, st[34], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[35], inv, out varDbl))
					HighDewPoint = varDbl;
				else
					HighDewPoint = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[36].Length == 5)
					HighDewPointTime = GetDateTime(Date, st[36], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[37], inv, out varDbl))
					LowDewPoint = varDbl;
				else
					LowDewPoint = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[38].Length == 5)
					LowDewPointTime = GetDateTime(Date, st[38], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[39], inv, out varDbl))
					DominantWindBearing = Convert.ToInt32(varDbl);
				else
					DominantWindBearing = (int) Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[40], inv, out varDbl))
					HeatingDegreeDays = varDbl;
				else
					HeatingDegreeDays = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[41], inv, out varDbl))
					CoolingDegreeDays = varDbl;
				else
					CoolingDegreeDays = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[42], inv, out varDbl))
					HighSolar = Convert.ToInt32(varDbl);
				else
					HighSolar = (int) Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[43].Length == 5)
					HighSolarTime = GetDateTime(Date, st[43], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[44], inv, out varDbl))
					HighUv = varDbl;
				else
					HighUv = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[45].Length == 5)
					HighUvTime = GetDateTime(Date, st[45], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[46], inv, out varDbl))
					HighFeelsLike = varDbl;
				else
					HighFeelsLike = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[47].Length == 5)
					HighFeelsLikeTime = GetDateTime(Date, st[47], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[48], inv, out varDbl))
					LowFeelsLike = varDbl;
				else
					LowFeelsLike = Cumulus.DefaultLoVal;

				if (st.Count > idx++ && st[49].Length == 5)
					LowFeelsLikeTime = GetDateTime(Date, st[49], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[50], inv, out varDbl))
					HighHumidex = varDbl;
				else
					HighHumidex = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[51].Length == 5)
					HighHumidexTime = GetDateTime(Date, st[51], Program.cumulus.RolloverHour);

				if (st.Count > idx++ && double.TryParse(st[52], inv, out varDbl))
					ChillHours = varDbl;
				else
					ChillHours = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && double.TryParse(st[53], inv, out varDbl))
					HighRain24h = varDbl;
				else
					HighRain24h = Cumulus.DefaultHiVal;

				if (st.Count > idx++ && st[54].Length == 5)
					HighRain24hTime = GetDateTime(Date, st[54], Program.cumulus.RolloverHour);
			}
			catch (Exception ex)
			{
				Program.cumulus.LogDebugMessage($"ParseDayFileRec: Error at record {idx} - {ex.Message}");
				var e = new Exception($"Error at record {idx} = \"{st[idx - 1]}\" - {ex.Message}");
				throw e;
			}
		}
	}
}
