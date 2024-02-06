using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CumulusMX
{
	// The annotations on this class are so it can be serialised as JSON
	[DataContract]
	public class DataStruct(Cumulus cumulus, double outdoorTemp, int outdoorHum, double avgTempToday, double indoorTemp, double outdoorDewpoint, double windChill,
						int indoorHum, double pressure, double windLatest, double windAverage, double recentmaxgust, double windRunToday, int bearing, int avgbearing,
						double rainToday, double rainYesterday, double rainMonth, double rainYear, double rainRate, double rainLastHour, double heatIndex, double humidex,
						double appTemp, double tempTrend, double pressTrend, double highGustToday, string highGustTodayTime, double highWindToday, int highGustBearingToday,
						string windUnit, int bearingRangeFrom10, int bearingRangeTo10, string windRoseData, double highTempToday, double lowTempToday, string highTempTodayToday,
						string lowTempTodayTime, double highPressToday, double lowPressToday, string highPressTodayTime, string lowPressTodayTime, double highRainRateToday,
						string highRainRateTodayTime, int highHumToday, int lowHumToday, string highHumTodayTime, string lowHumTodayTime, string pressUnit, string tempUnit,
						string rainUnit, double highDewpointToday, double lowDewpointToday, string highDewpointTodayTime, string lowDewpointTodayTime, double lowWindChillToday,
						string lowWindChillTodayTime, int solarRad, int highSolarRadToday, string highSolarRadTodayTime, double uvindex, double highUVindexToday,
						string highUVindexTodayTime, string forecast, string sunrise, string sunset, string moonrise, string moonset, double highHeatIndexToday,
						string highHeatIndexTodayTime, double highAppTempToday, double lowAppTempToday, string highAppTempTodayTime, string lowAppTempTodayTime,
						int currentSolarMax, double alltimeHighPressure, double alltimeLowPressure, double sunshineHours, string domWindDir, string lastRainTipISO,
						double highHourlyRainToday, string highHourlyRainTodayTime, string highBeaufortToday, string beaufort, string beaufortDesc, string lastDataRead,
						bool dataStopped, double stormRain, string stormRainStart, int cloudbase, string cloudbaseUnit, double last24hourRain,
						double feelsLike, double highFeelsLikeToday, string highFeelsLikeTodayTime, double lowFeelsLikeToday, string lowFeelsLikeTodayTime,
						double highHumidexToday, string highHumidexTodayTime, List<DashboardAlarms> alarms)
	{
		private readonly Cumulus cumulus = cumulus;

		[IgnoreDataMember]
		public double StormRain { get; set; } = stormRain;

		[DataMember(Name = "StormRain")]
		public string StormRainRounded
		{
			get => StormRain.ToString(cumulus.RainFormat);
			set { }
		}

		[DataMember]
		public string StormRainStart { get; set; } = stormRainStart;

		[DataMember]
		public int CurrentSolarMax { get; set; } = currentSolarMax;

		[IgnoreDataMember]
		public double HighHeatIndexToday { get; set; } = highHeatIndexToday;

		[DataMember(Name = "HighHeatIndexToday")]
		public string HighHeatIndexTodayRounded
		{
			get => HighHeatIndexToday.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string HighHeatIndexTodayTime { get; set; } = highHeatIndexTodayTime;

		[DataMember]
		public string Sunrise { get; set; } = sunrise;

		[DataMember]
		public string Sunset { get; set; } = sunset;

		[DataMember]
		public string Moonrise { get; set; } = moonrise;

		[DataMember]
		public string Moonset { get; set; } = moonset;

		[DataMember]
		public string Forecast { get; set; } = forecast;

		[IgnoreDataMember]
		public double UVindex { get; set; } = uvindex;

		[DataMember(Name = "UVindex")]
		public string UVindexRounded
		{
			get => UVindex.ToString(cumulus.UVFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HighUVindexToday { get; set; } = highUVindexToday;

		[DataMember(Name = "HighUVindexToday")]
		public string HighUVindexTodayRounded
		{
			get => HighUVindexToday.ToString(cumulus.UVFormat);
			set { }
		}

		[DataMember]
		public string HighUVindexTodayTime { get; set; } = highUVindexTodayTime;

		[DataMember]
		public string HighSolarRadTodayTime { get; set; } = highSolarRadTodayTime;

		[DataMember]
		public int HighSolarRadToday { get; set; } = highSolarRadToday;

		[DataMember]
		public int SolarRad { get; set; } = solarRad;

		[IgnoreDataMember]
		public double IndoorTemp = indoorTemp;

		[DataMember(Name = "IndoorTemp")]
		public string IndoorTempRounded
		{
			get => IndoorTemp.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double OutdoorDewpoint = outdoorDewpoint;

		[DataMember(Name = "OutdoorDewpoint")]
		public string OutdoorDewpointRounded
		{
			get => OutdoorDewpoint.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double LowDewpointToday { get; set; } = lowDewpointToday;

		[DataMember(Name = "LowDewpointToday")]
		public string LowDewpointTodayRounded
		{
			get => LowDewpointToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HighDewpointToday { get; set; } = highDewpointToday;

		[DataMember(Name = "HighDewpointToday")]
		public string HighDewpointTodayRounded
		{
			get => HighDewpointToday.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string LowDewpointTodayTime { get; set; } = lowDewpointTodayTime;

		[DataMember]
		public string HighDewpointTodayTime { get; set; } = highDewpointTodayTime;

		[IgnoreDataMember]
		public double WindChill = windChill;

		[DataMember(Name = "WindChill")]
		public string WindChillRounded
		{
			get => WindChill.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double LowWindChillToday { get; set; } = lowWindChillToday;

		[DataMember(Name = "LowWindChillToday")]
		public string LowWindChillTodayRounded
		{
			get => LowWindChillToday.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string LowWindChillTodayTime { get; set; } = lowWindChillTodayTime;

		[DataMember]
		public string WindUnit = windUnit;

		[DataMember]
		public string RainUnit { get; set; } = rainUnit;

		[DataMember]
		public string TempUnit { get; set; } = tempUnit;

		[DataMember]
		public string PressUnit { get; set; } = pressUnit;

		[DataMember]
		public string CloudbaseUnit { get; set; } = cloudbaseUnit;

		[DataMember]
		public int Cloudbase { get; set; } = cloudbase;

		[DataMember]
		public string LowHumTodayTime { get; set; } = lowHumTodayTime;

		[DataMember]
		public string HighHumTodayTime { get; set; } = highHumTodayTime;

		[DataMember]
		public int LowHumToday { get; set; } = lowHumToday;

		[DataMember]
		public int HighHumToday { get; set; } = highHumToday;

		[DataMember]
		public string HighRainRateTodayTime { get; set; } = highRainRateTodayTime;

		[IgnoreDataMember]
		public double HighRainRateToday { get; set; } = highRainRateToday;

		[DataMember(Name = "HighRainRateToday")]
		public string HighRainRateTodayRounded
		{
			get => HighRainRateToday.ToString(cumulus.RainFormat);
			set { }
		}

		[DataMember]
		public string HighHourlyRainTodayTime { get; set; } = highHourlyRainTodayTime;

		[IgnoreDataMember]
		public double HighHourlyRainToday { get; set; } = highHourlyRainToday;

		[DataMember(Name = "HighHourlyRainToday")]
		public string HighHourlyRainTodayRounded
		{
			get => HighHourlyRainToday.ToString(cumulus.RainFormat);
			set { }
		}

		[DataMember]
		public string LowPressTodayTime { get; set; } = lowPressTodayTime;

		[DataMember]
		public string HighPressTodayTime { get; set; } = highPressTodayTime;

		[IgnoreDataMember]
		public double LowPressToday { get; set; } = lowPressToday;

		[DataMember(Name = "LowPressToday")]
		public string LowPressTodayRounded
		{
			get => LowPressToday.ToString(cumulus.PressFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HighPressToday { get; set; } = highPressToday;

		[DataMember(Name = "HighPressToday")]
		public string HighPressTodayRounded
		{
			get => HighPressToday.ToString(cumulus.PressFormat);
			set { }
		}

		[DataMember]
		public string LowTempTodayTime { get; set; } = lowTempTodayTime;

		[DataMember]
		public string HighTempTodayTime { get; set; } = highTempTodayToday;

		[IgnoreDataMember]
		public double LowTempToday { get; set; } = lowTempToday;

		[DataMember(Name = "LowTempToday")]
		public string LowTempTodayRounded
		{
			get => LowTempToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HighTempToday { get; set; } = highTempToday;

		[DataMember(Name = "HighTempToday")]
		public string HighTempTodayRounded
		{
			get => HighTempToday.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string WindRoseData { get; set; } = windRoseData;

		[DataMember]
		public int BearingRangeTo10 { get; set; } = bearingRangeTo10;

		[DataMember]
		public int BearingRangeFrom10 { get; set; } = bearingRangeFrom10;

		[DataMember]
		public int HighGustBearingToday { get; set; } = highGustBearingToday;

		[IgnoreDataMember]
		public double HighWindToday { get; set; } = highWindToday;

		[DataMember(Name = "HighWindToday")]
		public string HighWindTodayRounded
		{
			get => HighWindToday.ToString(cumulus.WindAvgFormat);
			set { }
		}

		[DataMember]
		public string HighGustTodayTime { get; set; } = highGustTodayTime;

		[IgnoreDataMember]
		public double HighGustToday { get; set; } = highGustToday;

		[DataMember(Name = "HighGustToday")]
		public string HighGustTodayRounded
		{
			get => HighGustToday.ToString(cumulus.WindFormat);
			set { }
		}

		[IgnoreDataMember]
		public double OutdoorTemp { get; set; } = outdoorTemp;

		[DataMember(Name = "OutdoorTemp")]
		public string OutdoorTempRounded
		{
			get => OutdoorTemp.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public int OutdoorHum { get; set; } = outdoorHum;

		[IgnoreDataMember]
		public double AvgTempToday { get; set; } = avgTempToday;

		[DataMember(Name = "AvgTempToday")]
		public string AvgTempRounded
		{
			get => AvgTempToday.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public int IndoorHum { get; set; } = indoorHum;

		[IgnoreDataMember]
		public double Pressure { get; set; } = pressure;

		[DataMember(Name = "Pressure")]
		public string PressureRounded
		{
			get => Pressure.ToString(cumulus.PressFormat);
			set { }
		}

		[IgnoreDataMember]
		public double AlltimeHighPressure { get; set; } = alltimeHighPressure;

		[DataMember(Name = "AlltimeHighPressure")]
		public string AlltimeHighPressureRounded
		{
			get => AlltimeHighPressure.ToString(cumulus.PressFormat);
			set { }
		}

		[IgnoreDataMember]
		public double AlltimeLowPressure { get; set; } = alltimeLowPressure;

		[DataMember(Name = "AlltimeLowPressure")]
		public string AlltimeLowPressureRounded
		{
			get => AlltimeLowPressure.ToString(cumulus.PressFormat);
			set { }
		}

		[IgnoreDataMember]
		public double WindLatest { get; set; } = windLatest;

		[DataMember(Name = "WindLatest")]
		public string WindLatestRounded
		{
			get => WindLatest.ToString(cumulus.WindFormat);
			set { }
		}

		[IgnoreDataMember]
		public double WindAverage { get; set; } = windAverage;

		[DataMember(Name = "WindAverage")]
		public string WindAverageRounded
		{
			get => WindAverage.ToString(cumulus.WindAvgFormat);
			set { }
		}

		[IgnoreDataMember]
		public double Recentmaxgust { get; set; } = recentmaxgust;

		[DataMember(Name = "Recentmaxgust")]
		public string RecentmaxgustRounded
		{
			get => Recentmaxgust.ToString(cumulus.WindFormat);
			set { }
		}

		[IgnoreDataMember]
		public double WindRunToday { get; set; } = windRunToday;

		[DataMember(Name = "WindRunToday")]
		public string WindRunTodayRounded
		{
			get => WindRunToday.ToString(cumulus.WindRunFormat);
			set { }
		}

		[DataMember]
		public int Bearing { get; set; } = bearing;

		[DataMember]
		public int Avgbearing { get; set; } = avgbearing;

		[IgnoreDataMember]
		public double RainToday { get; set; } = rainToday;

		[DataMember(Name = "RainToday")]
		public string RainTodayRounded
		{
			get => RainToday.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double RainYesterday { get; set; } = rainYesterday;

		[DataMember(Name = "RainYesterday")]
		public string RainYesterdayRounded
		{
			get => RainYesterday.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double RainMonth { get; set; } = rainMonth;

		[DataMember(Name = "RainMonth")]
		public string RainMonthRounded
		{
			get => RainMonth.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double RainYear { get; set; } = rainYear;
		[DataMember(Name = "RainYear")]
		public string RainYearRounded
		{
			get => RainYear.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double RainRate { get; set; } = rainRate;

		[DataMember(Name = "RainRate")]
		public string RainRateRounded
		{
			get => RainRate.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double RainLastHour { get; set; } = rainLastHour;

		[DataMember(Name = "RainLastHour")]
		public string RainLastHourRounded
		{
			get => RainLastHour.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double RainLast24Hour { get; set; } = last24hourRain;

		[DataMember(Name = "RainLast24Hour")]
		public string RainLast24HourRounded
		{
			get => RainLast24Hour.ToString(cumulus.RainFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HeatIndex { get; set; } = heatIndex;

		[DataMember(Name = "HeatIndex")]
		public string HeatIndexRounded
		{
			get => HeatIndex.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double Humidex { get; set; } = humidex;

		[DataMember(Name = "Humidex")]
		public string HumidexRounded
		{
			get => Humidex.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string HighHumidexTodayTime { get; set; } = highHumidexTodayTime;

		[IgnoreDataMember]
		public double HighHumidexToday { get; set; } = highHumidexToday;

		[DataMember(Name = "HighHumidexToday")]
		public string HighHumidexTodayRounded
		{
			get => HighHumidexToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double AppTemp { get; set; } = appTemp;

		[DataMember(Name = "AppTemp")]
		public string AppTempRounded
		{
			get => AppTemp.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string LowAppTempTodayTime { get; set; } = lowAppTempTodayTime;

		[DataMember]
		public string HighAppTempTodayTime { get; set; } = highAppTempTodayTime;

		[IgnoreDataMember]
		public double LowAppTempToday { get; set; } = lowAppTempToday;

		[DataMember(Name = "LowAppTempToday")]
		public string LowAppTempTodayRounded
		{
			get => LowAppTempToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HighAppTempToday { get; set; } = highAppTempToday;

		[DataMember(Name = "HighAppTempToday")]
		public string HighAppTempTodayRounded
		{
			get => HighAppTempToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double FeelsLike { get; set; } = feelsLike;

		[DataMember(Name = "FeelsLike")]
		public string FeelsLikeRounded
		{
			get => FeelsLike.ToString(cumulus.TempFormat);
			set { }
		}

		[DataMember]
		public string LowFeelsLikeTodayTime { get; set; } = lowFeelsLikeTodayTime;

		[DataMember]
		public string HighFeelsLikeTodayTime { get; set; } = highFeelsLikeTodayTime;

		[IgnoreDataMember]
		public double LowFeelsLikeToday { get; set; } = lowFeelsLikeToday;

		[DataMember(Name = "LowFeelsLikeToday")]
		public string LowFeelsLikeTodayRounded
		{
			get => LowFeelsLikeToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double HighFeelsLikeToday { get; set; } = highFeelsLikeToday;

		[DataMember(Name = "HighFeelsLikeToday")]
		public string HighFeelsLikeTodayRounded
		{
			get => HighFeelsLikeToday.ToString(cumulus.TempFormat);
			set { }
		}

		[IgnoreDataMember]
		public double TempTrend { get; set; } = tempTrend;

		[DataMember(Name = "TempTrend")]
		public string TempTrendRounded
		{
			get => TempTrend.ToString(cumulus.TempTrendFormat);
			set { }
		}

		[IgnoreDataMember]
		public double PressTrend { get; set; } = pressTrend;

		[DataMember(Name = "PressTrend")]
		public string PressTrendRounded
		{
			get => PressTrend.ToString(cumulus.PressTrendFormat);
			set { }
		}

		[IgnoreDataMember]
		public double SunshineHours { get; set; } = sunshineHours;

		[DataMember(Name = "SunshineHours")]
		public string SunshineHoursRounded
		{
			get => SunshineHours.ToString(cumulus.SunFormat);
			set { }
		}

		[DataMember]
		public string Version
		{
			get => cumulus.Version;
			set { }
		}

		[DataMember]
		public string Build
		{
			get => cumulus.Build;
			set { }
		}

		[DataMember]
		public string DominantWindDirection { get; set; } = domWindDir;

		[DataMember]
		public string LastRainTipISO { get; set; } = lastRainTipISO;

		[DataMember]
		public string HighBeaufortToday { get; set; } = highBeaufortToday;

		[DataMember]
		public string Beaufort { get; set; } = beaufort;

		[DataMember]
		public string BeaufortDesc { get; set; } = beaufortDesc;

		[DataMember]
		public string LastDataRead { get; set; } = lastDataRead;

		[DataMember]
		public bool DataStopped { get; set; } = dataStopped;

		[DataMember]
		public List<DashboardAlarms> Alarms { get; set; } = alarms;
	}
}
