using System;
using System.Collections.Generic;
using System.Globalization;

namespace CumulusMX
{
	internal class LogFileRec
	{
		public string DateTimeStr { get; set; }

		public long UnixTimestamp;
		public DateTime DateTime
		{
			get
			{
				return UnixTimestamp.FromUnixTime();
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
			// 1  Current Unix timestamp
			// 2  Current temperature
			// 3  Current humidity
			// 4  Current dewpoint
			// 5  Current wind speed
			// 6  Recent (10-minute) high gust
			// 7  Average wind bearing
			// 8  Current rainfall rate
			// 9  Total rainfall today so far
			// 10  Current sea level pressure
			// 11  Total rainfall counter as held by the station
			// 12  Inside temperature
			// 13  Inside humidity
			// 14  Current gust (i.e. 'Latest')
			// 15  Wind chill
			// 16  Heat Index
			// 17  UV Index
			// 18  Solar Radiation
			// 19  Evapotranspiration
			// 20  Annual Evapotranspiration
			// 21  Apparent temperature
			// 22  Current theoretical max solar radiation
			// 23  Hours of sunshine so far today
			// 24  Current wind bearing
			// 25  RG-11 rain total
			// 26  Rain since midnight
			// 27  Feels like
			// 28  Humidex


			var inv = CultureInfo.InvariantCulture;
			var st = new List<string>(data.Split(','));
			double resultDbl;
			int resultInt;

			try
			{
				DateTimeStr = st[0];
				UnixTimestamp = Convert.ToInt64(st[1]);
				OutdoorTemperature = Convert.ToDouble(st[2], inv);
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
				OutdoorTemperature.ToString("F1", inv),
				OutdoorHumidity.ToString("F0", inv),
				OutdoorDewpoint.ToString("F1", inv),
				WindAverage.ToString("F1", inv),
				RecentMaxGust.ToString("F1", inv),
				AvgBearing.ToString("F0", inv),
				RainRate.ToString("F2", inv),
				RainToday.ToString("F2", inv),
				Pressure.ToString("F1", inv),
				RainCounter.ToString("F2", inv),
				IndoorTemperature.HasValue ? IndoorTemperature.Value.ToString("F1", inv) : "",
				IndoorHumidity.HasValue ? IndoorHumidity.Value.ToString("F0", inv) : "",
				WindLatest.ToString("F1", inv),
				WindChill.HasValue ? WindChill.Value.ToString("F1", inv) : "",
				HeatIndex.HasValue ? HeatIndex.Value.ToString("F1", inv) : "",
				UV.HasValue ? UV.Value.ToString("F1", inv) : "",
				SolarRad.HasValue ? SolarRad.Value.ToString("F0", inv) : "",
				ET.HasValue ? ET.Value.ToString("F2", inv) : "",
				AnnualETTotal.HasValue ? AnnualETTotal.Value.ToString("F1", inv) : "",
				ApparentTemperature.HasValue ? ApparentTemperature.Value.ToString("F1", inv) : "",
				CurrentSolarMax.HasValue ? CurrentSolarMax.Value.ToString("F0", inv) : "",
				SunshineHours.HasValue ? SunshineHours.Value.ToString("F2", inv) : "",
				Bearing.HasValue ? Bearing.Value.ToString("F0", inv) : "",
				RG11RainToday.HasValue ? RG11RainToday.Value.ToString("F2", inv) : "",
				RainSinceMidnight.HasValue ? RainSinceMidnight.Value.ToString("F2", inv) : "",
				FeelsLike.HasValue ? FeelsLike.Value.ToString("F1", inv) : "",
				Humidex.HasValue ? Humidex.Value.ToString("F1", inv) : ""
				);
		}

		public static string CurrentToCsv(DateTime timestamp, Cumulus cumulus, WeatherStation station)
		{ 
			var inv = CultureInfo.InvariantCulture;
			return string.Join(",",
				timestamp.ToString("dd/MM/yy HH:mm", inv),
				timestamp.ToUnixTime(),
				station.OutdoorTemperature.ToString(cumulus.TempFormat, inv),
				station.OutdoorHumidity,
				station.OutdoorDewpoint.ToString(cumulus.TempFormat, inv),
				station.WindAverage.ToString(cumulus.WindAvgFormat, inv),
				station.RecentMaxGust.ToString(cumulus.WindFormat, inv),
				station.AvgBearing,
				station.RainRate.ToString(cumulus.RainFormat, inv),
				station.RainToday.ToString(cumulus.RainFormat, inv),
				station.Pressure.ToString(cumulus.PressFormat, inv),
				station.RainCounter.ToString(cumulus.RainFormat, inv),
				station.IndoorTemperature.HasValue ? station.IndoorTemperature.Value.ToString(cumulus.TempFormat, inv) : "",
				station.IndoorHumidity.HasValue ? station.IndoorHumidity.Value : "",
				station.WindLatest.ToString(cumulus.WindFormat, inv),
				station.WindChill.ToString(cumulus.TempFormat, inv),
				station.HeatIndex.ToString(cumulus.TempFormat, inv),
				station.UV.HasValue ? station.UV.Value.ToString(cumulus.UVFormat, inv) : "",
				station.SolarRad.HasValue ? station.SolarRad.Value : "",
				station.ET.ToString(cumulus.ETFormat, inv),
				station.AnnualETTotal.ToString(cumulus.ETFormat, inv),
				station.ApparentTemperature.ToString(cumulus.TempFormat, inv),
				station.CurrentSolarMax,
				station.SunshineHours.ToString(cumulus.SunFormat, inv),
				station.Bearing,
				station.RG11RainToday.ToString(cumulus.RainFormat, inv),
				station.RainSinceMidnight.ToString(cumulus.RainFormat, inv),
				station.FeelsLike.ToString(cumulus.TempFormat, inv),
				station.Humidex.ToString(cumulus.TempFormat, inv)
			) + Environment.NewLine;
		}
	}
}
