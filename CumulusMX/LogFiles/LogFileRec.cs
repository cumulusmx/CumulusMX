using System;
using System.Collections.Generic;
using System.Globalization;

namespace CumulusMX.LogFiles
{
	internal class LogFileRec
	{
		public string DateTimeStr { get; set; }

		public long UnixTimestamp;
		public DateTime DateTime
		{
			get
			{
				return UnixTimestamp.LocalFromUnixTime();
			}
			set
			{
				UnixTimestamp = value.ToUnixTime();
				DateTimeStr = value.ToString("dd/MM/yy HH:mm", CultureInfo.InvariantCulture);
			}
		}
		public double OutdoorTemperature { get; set; }
		public double OutdoorHumidity { get; set; }
		public double OutdoorDewpoint { get; set; }
		public double WindAverage { get; set; }
		public double RecentMaxGust { get; set; }
		public int AvgBearing { get; set; }
		public double RainRate { get; set; }
		public double RainToday { get; set; }
		public double Pressure { get; set; }
		public double RainCounter { get; set; }
		public double? IndoorTemperature { get; set; }
		public int? IndoorHumidity { get; set; }
		public double WindLatest { get; set; }
		public double? WindChill { get; set; }
		public double? HeatIndex { get; set; }
		public double? UV { get; set; }
		public int? SolarRad { get; set; }
		public double? ET { get; set; }
		public double? AnnualETTotal { get; set; }
		public double? ApparentTemperature { get; set; }
		public int? CurrentSolarMax { get; set; }
		public double? SunshineHours { get; set; }
		public int? Bearing { get; set; }
		public double? RG11RainToday { get; set; }
		public double? RainSinceMidnight { get; set; }
		public double? FeelsLike { get; set; }
		public double? Humidex { get; set; }
		public double? BlackGlobeTemp { get; set; }
		public double? WetBulbGlobeTemp { get; set; }

		public LogFileRec()
		{
		}

		public LogFileRec(string csv)
		{
			ParseCsvRec(csv);
		}


		// errors are caught by the caller
		public void ParseCsvRec(string data)
		{
			// 0  Date/Time in the form dd/mm/yy hh:mm
			// 1  MetData Unix timestamp
			// 2  MetData temperature
			// 3  MetData humidity
			// 4  MetData dewpoint
			// 5  MetData wind speed
			// 6  Recent (10-minute) high gust
			// 7  Average wind bearing
			// 8  MetData rainfall rate
			// 9  Total rainfall today so far
			// 10  MetData sea level pressure
			// 11  Total rainfall counter as held by the Stations
			// 12  Inside temperature
			// 13  Inside humidity
			// 14  MetData gust (i.e. 'Latest')
			// 15  Wind chill
			// 16  Heat Index
			// 17  UV Index
			// 18  Solar Radiation
			// 19  Evapotranspiration
			// 20  Annual Evapotranspiration
			// 21  Apparent temperature
			// 22  MetData theoretical max solar radiation
			// 23  Hours of sunshine so far today
			// 24  MetData wind bearing
			// 25  RG-11 rain total
			// 26  Rain since midnight
			// 27  Feels like
			// 28  Humidex
			// 29  Black Globe Temp
			// 30  Wet bulb Globe Temp


			var inv = CultureInfo.InvariantCulture;
			var st = new List<string>(data.Split(','));
			double resultDbl;
			int resultInt;

			try
			{
				DateTimeStr = st[0];
				UnixTimestamp = Convert.ToInt64(st[1]);
				MetData.Temperature = Convert.ToDouble(st[2], inv);
				OutdoorHumidity = Convert.ToInt32(Convert.ToDouble(st[3], inv));
				OutdoorDewpoint = Convert.ToDouble(st[4], inv);
				WindAverage = Convert.ToDouble(st[5], inv);
				RecentMaxGust = Convert.ToDouble(st[6], inv);
				AvgBearing = Convert.ToInt32(Convert.ToDouble(st[7], inv));
				RainRate = Convert.ToDouble(st[8], inv);
				RainToday = Convert.ToDouble(st[9], inv);
				Pressure = Convert.ToDouble(st[10], inv);
				RainCounter = Convert.ToDouble(st[11], inv);
				IndoorTemperature = double.TryParse(st[12], inv, out resultDbl) ? resultDbl : null;
				IndoorHumidity = int.TryParse(st[13], out resultInt) ? resultInt : null;
				WindLatest = Convert.ToDouble(st[14], inv);

				if (st.Count > 15 && double.TryParse(st[15], inv, out resultDbl))
					WindChill = resultDbl;

				if (st.Count > 16 && double.TryParse(st[16], inv, out resultDbl))
					HeatIndex = resultDbl;

				if (st.Count > 17 && double.TryParse(st[17], inv, out resultDbl))
					UV = resultDbl;

				if (st.Count > 18 && int.TryParse(st[18], inv, out resultInt))
					SolarRad = resultInt;

				if (st.Count > 19 && double.TryParse(st[19], inv, out resultDbl))
					ET = resultDbl;

				if (st.Count > 20 && double.TryParse(st[20], inv, out resultDbl))
					AnnualETTotal = resultDbl;

				if (st.Count > 21 && double.TryParse(st[21], inv, out resultDbl))
					ApparentTemperature = resultDbl;

				if (st.Count > 22 && int.TryParse(st[22], inv, out resultInt))
					CurrentSolarMax = resultInt;

				if (st.Count > 23 && double.TryParse(st[23], inv, out resultDbl))
					SunshineHours = resultDbl;

				if (st.Count > 24 && int.TryParse(st[24], out resultInt))
					Bearing = resultInt;

				if (st.Count > 25 && double.TryParse(st[25], inv, out resultDbl))
					RG11RainToday = resultDbl;

				if (st.Count > 26 && double.TryParse(st[26], inv, out resultDbl))
					RainSinceMidnight = resultDbl;

				if (st.Count > 27 && double.TryParse(st[27], inv, out resultDbl))
					FeelsLike = resultDbl;

				if (st.Count > 28 && double.TryParse(st[28], inv, out resultDbl))
					Humidex = resultDbl;

				if (st.Count > 29 && double.TryParse(st[29], inv, out resultDbl))
					BlackGlobeTemp = resultDbl;

				if (st.Count > 30 && double.TryParse(st[30], inv, out resultDbl))
					WetBulbGlobeTemp = resultDbl;
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, $"LogFileParseCsvRec: Error");
				throw;
			}
		}

		public string ToCsv()
		{
			var inv = CultureInfo.InvariantCulture;
			return string.Join(",",
				DateTimeStr,
				UnixTimestamp.ToString(inv),
				MetData.Temperature.ToFixed("F1"),
				OutdoorHumidity.ToFixed("F1"),
				OutdoorDewpoint.ToFixed("F1"),
				WindAverage.ToFixed("F1"),
				RecentMaxGust.ToFixed("F1"),
				AvgBearing.ToString(),
				RainRate.ToFixed("F2"),
				RainToday.ToFixed("F2"),
				Pressure.ToFixed("F2"),
				RainCounter.ToFixed("F2"),
				IndoorTemperature.ToFixed("F1"),
				IndoorHumidity.ToText(),
				WindLatest.ToFixed("F1"),
				WindChill.ToFixed("F1"),
				HeatIndex.ToFixed("F1"),
				UV.ToFixed("F1"),
				SolarRad.ToText(),
				ET.ToFixed("F2"),
				AnnualETTotal.ToFixed("F1"),
				ApparentTemperature.ToFixed("F1"),
				CurrentSolarMax.ToText(),
				SunshineHours.ToFixed("F2"),
				Bearing.ToText(),
				RG11RainToday.ToFixed("F2"),
				RainSinceMidnight.ToFixed("F2"),
				FeelsLike.ToFixed("F1"),
				Humidex.ToFixed("F1"),
				BlackGlobeTemp.ToFixed("F1"),
				WetBulbGlobeTemp.ToFixed("F1")
			);
		}

		public static string CurrentToCsv(DateTime timestamp, Cumulus cumulus)
		{
			var inv = CultureInfo.InvariantCulture;
			return string.Join(",",
				timestamp.ToString("dd/MM/yy HH:mm", inv),
				timestamp.ToUnixTime(),
				MetData.Temperature.ToFixed(cumulus.TempFormat),
				MetData.Humidity,
				MetData.Dewpoint.ToFixed(cumulus.TempFormat),
				MetData.WindAverage.ToString(cumulus.WindAvgFormat, inv),
				MetData.RecentMaxGust.ToString(cumulus.WindAvgFormat, inv),
				MetData.WindAvgBearing.ToString(),
				MetData.RainRate.ToString(cumulus.RainFormat, inv),
				MetData.RainToday.ToString(cumulus.RainFormat, inv),
				MetData.Pressure.ToString(cumulus.PressFormat, inv),
				MetData.RainCounter.ToString(cumulus.RainFormat, inv),
				MetData.TemperatureIn.ToFixed(cumulus.TempFormat),
				MetData.HumidityIn.ToText(),
				MetData.WindLatest.ToString(cumulus.WindFormat, inv),
				MetData.WindChill.ToString(cumulus.TempFormat, inv),
				MetData.HeatIndex.ToFixed(cumulus.TempFormat),
				MetData.UV.ToFixed(cumulus.UVFormat),
				MetData.SolarRad.ToText(),
				MetData.ET.ToString(cumulus.ETFormat, inv),
				MetData.AnnualETTotal.ToString(cumulus.ETFormat, inv),
				MetData.ApparentTemperature.ToFixed(cumulus.TempFormat),
				MetData.CurrentSolarMax.ToString(),
				MetData.SunshineHours.ToFixed(cumulus.SunFormat),
				MetData.WindBearing.ToString(),
				MetData.RG11RainToday.ToString(cumulus.RainFormat, inv),
				MetData.RainSinceMidnight.ToFixed(cumulus.RainFormat),
				MetData.FeelsLike.ToFixed(cumulus.TempFormat),
				MetData.Humidex.ToFixed(cumulus.TempFormat),
				MetData.BlackGlobeTemp.ToFixed(cumulus.TempFormat),
				MetData.WetBulbGlobeTemp.ToFixed(cumulus.TempFormat)
			) + Environment.NewLine;
		}
	}
}
